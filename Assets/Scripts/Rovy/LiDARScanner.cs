using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Identifies the semantic surface category detected by the LiDAR scanner.
/// </summary>
public enum SurfaceType
{
    Unknown,
    Sidewalk,
    Obstacle,
    ParkPath,
    Crosswalk,
    Stairs
}

/// <summary>
/// Represents a single LiDAR hit captured during the most recent scan pass.
/// </summary>
[Serializable]
public struct ScanResult
{
    public Vector3 origin;
    public Vector3 direction;
    public Vector3 worldPos;
    public float distance;
    public SurfaceType surfaceType;

    /// <summary>
    /// Creates a scan result from one LiDAR hit.
    /// </summary>
    /// <param name="origin">The ray origin in world space.</param>
    /// <param name="direction">The normalized ray direction in world space.</param>
    /// <param name="worldPos">The hit position in world space.</param>
    /// <param name="distance">The hit distance from the origin.</param>
    /// <param name="surfaceType">The classified surface type.</param>
    public ScanResult(Vector3 origin, Vector3 direction, Vector3 worldPos, float distance, SurfaceType surfaceType)
    {
        this.origin = origin;
        this.direction = direction;
        this.worldPos = worldPos;
        this.distance = distance;
        this.surfaceType = surfaceType;
    }
}

/// <summary>
/// Simulates a multi-layer LiDAR scanner for Rovy and forwards classified hits to RouteMemory.
/// </summary>
[DisallowMultipleComponent]
public sealed class LiDARScanner : MonoBehaviour
{
    private const float DefaultScanRange = 8.0f;
    private const float DefaultScanInterval = 0.5f;
    private const float HalfVerticalScanAngle = 20.0f;
    private const float MinimumScanInterval = 0.01f;
    private const float SelfHitThreshold = 0.0001f;
    private const string RouteMemoryTypeName = "RouteMemory";

    [Serializable]
    private struct DebugRay
    {
        public Vector3 start;
        public Vector3 end;
        public bool hasHit;
        public SurfaceType surfaceType;

        public DebugRay(Vector3 start, Vector3 end, bool hasHit, SurfaceType surfaceType)
        {
            this.start = start;
            this.end = end;
            this.hasHit = hasHit;
            this.surfaceType = surfaceType;
        }
    }

    [Header("Scan Settings")]
    [SerializeField] private int horizontalRayCount = 36;
    [SerializeField] private int verticalLayers = 3;
    [SerializeField] private float scanRange = DefaultScanRange;
    [SerializeField] private float scanInterval = DefaultScanInterval;
    [SerializeField] private LayerMask scanLayers;
    [SerializeField] private bool showGizmos = true;

    [Header("Integration")]
    [SerializeField] private MonoBehaviour routeMemory;

    private Coroutine scanCoroutine;
    private MethodInfo registerHitMethod;
    private Type routeMemoryType;
    private DebugRay[] lastDebugRays = Array.Empty<DebugRay>();
    private Collider[] selfColliders = Array.Empty<Collider>();
    private int sidewalkLayer = -1;
    private int obstacleLayer = -1;
    private int parkPathLayer = -1;
    private int crosswalkLayer = -1;
    private int stairsLayer = -1;
    private bool warnedAboutMissingRegisterMethod;

    /// <summary>
    /// Stores the hit results captured during the latest scan pass.
    /// </summary>
    public ScanResult[] LastScanResults = Array.Empty<ScanResult>();

    private void Reset()
    {
        scanLayers = CreateDefaultScanMask();
        routeMemory = FindRouteMemoryComponent();
    }

    private void Awake()
    {
        ClampSettings();
        CacheLayerIds();
        CacheSelfColliders();

        if (routeMemory == null)
        {
            routeMemory = FindRouteMemoryComponent();
        }
    }

    private void OnEnable()
    {
        StartScanning();
    }

    private void OnDisable()
    {
        StopScanning();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheLayerIds();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        if (lastDebugRays != null && lastDebugRays.Length > 0)
        {
            for (int i = 0; i < lastDebugRays.Length; i++)
            {
                DebugRay debugRay = lastDebugRays[i];
                Gizmos.color = GetDebugColor(debugRay.surfaceType, debugRay.hasHit);
                Gizmos.DrawLine(debugRay.start, debugRay.end);
            }

            return;
        }

        DrawPreviewGizmos();
    }

    /// <summary>
    /// Performs a LiDAR scan immediately and refreshes <see cref="LastScanResults"/>.
    /// </summary>
    public void ScanNow()
    {
        PerformScan();
    }

    private void StartScanning()
    {
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
        }

        PerformScan();
        scanCoroutine = StartCoroutine(ScanLoop());
    }

    private void StopScanning()
    {
        if (scanCoroutine == null)
        {
            return;
        }

        StopCoroutine(scanCoroutine);
        scanCoroutine = null;
    }

    private IEnumerator ScanLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(scanInterval);
            PerformScan();
        }
    }

    private void PerformScan()
    {
        ClampSettings();
        CacheLayerIds();
        CacheSelfColliders();

        if (routeMemory == null)
        {
            routeMemory = FindRouteMemoryComponent();
        }

        Vector3 origin = transform.position;
        int rayCount = horizontalRayCount * verticalLayers;
        List<ScanResult> results = new List<ScanResult>(rayCount);
        List<DebugRay> debugRays = new List<DebugRay>(rayCount);

        for (int verticalIndex = 0; verticalIndex < verticalLayers; verticalIndex++)
        {
            float verticalAngle = GetVerticalAngle(verticalIndex);

            for (int horizontalIndex = 0; horizontalIndex < horizontalRayCount; horizontalIndex++)
            {
                float horizontalAngle = horizontalIndex * (360.0f / horizontalRayCount);
                Vector3 direction = GetWorldDirection(horizontalAngle, verticalAngle);
                RaycastHit[] hits = Physics.RaycastAll(
                    origin,
                    direction,
                    scanRange,
                    scanLayers,
                    QueryTriggerInteraction.Ignore);

                Array.Sort(hits, CompareByDistance);

                Vector3 debugEnd = origin + (direction * scanRange);
                bool hasDebugHit = false;
                SurfaceType debugSurface = SurfaceType.Unknown;

                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    RaycastHit hit = hits[hitIndex];

                    if (ShouldIgnoreHit(hit))
                    {
                        continue;
                    }

                    SurfaceType surfaceType = ClassifySurface(hit.collider.gameObject.layer);
                    results.Add(new ScanResult(origin, direction, hit.point, hit.distance, surfaceType));
                    RegisterHit(hit.point, surfaceType);

                    if (!hasDebugHit)
                    {
                        debugEnd = hit.point;
                        debugSurface = surfaceType;
                        hasDebugHit = true;
                    }
                }

                debugRays.Add(new DebugRay(origin, debugEnd, hasDebugHit, debugSurface));
            }
        }

        LastScanResults = results.ToArray();
        lastDebugRays = debugRays.ToArray();
    }

    private void ClampSettings()
    {
        horizontalRayCount = Mathf.Max(1, horizontalRayCount);
        verticalLayers = Mathf.Max(1, verticalLayers);
        scanRange = Mathf.Max(0.0f, scanRange);
        scanInterval = Mathf.Max(MinimumScanInterval, scanInterval);
    }

    private void CacheLayerIds()
    {
        sidewalkLayer = LayerMask.NameToLayer("Sidewalk");
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        parkPathLayer = LayerMask.NameToLayer("ParkPath");
        crosswalkLayer = LayerMask.NameToLayer("Crosswalk");
        stairsLayer = LayerMask.NameToLayer("Stairs");
    }

    private void CacheSelfColliders()
    {
        selfColliders = GetComponentsInChildren<Collider>(true);
    }

    private Vector3 GetWorldDirection(float horizontalAngle, float verticalAngle)
    {
        Vector3 horizontalDirection = Quaternion.AngleAxis(horizontalAngle, transform.up) * transform.forward;
        Vector3 pitchAxis = Vector3.Cross(horizontalDirection, transform.up);

        if (pitchAxis.sqrMagnitude <= SelfHitThreshold)
        {
            pitchAxis = transform.right;
        }

        Vector3 direction = Quaternion.AngleAxis(verticalAngle, pitchAxis.normalized) * horizontalDirection;
        return direction.normalized;
    }

    private float GetVerticalAngle(int verticalIndex)
    {
        if (verticalLayers <= 1)
        {
            return 0.0f;
        }

        float t = verticalIndex / (float)(verticalLayers - 1);
        return Mathf.Lerp(-HalfVerticalScanAngle, HalfVerticalScanAngle, t);
    }

    private bool ShouldIgnoreHit(RaycastHit hit)
    {
        if (hit.distance <= SelfHitThreshold)
        {
            return true;
        }

        for (int i = 0; i < selfColliders.Length; i++)
        {
            if (hit.collider == selfColliders[i])
            {
                return true;
            }
        }

        return false;
    }

    private SurfaceType ClassifySurface(int layer)
    {
        if (layer == sidewalkLayer)
        {
            return SurfaceType.Sidewalk;
        }

        if (layer == obstacleLayer)
        {
            return SurfaceType.Obstacle;
        }

        if (layer == parkPathLayer)
        {
            return SurfaceType.ParkPath;
        }

        if (layer == crosswalkLayer)
        {
            return SurfaceType.Crosswalk;
        }

        if (layer == stairsLayer)
        {
            return SurfaceType.Stairs;
        }

        return SurfaceType.Unknown;
    }

    private void RegisterHit(Vector3 worldPos, SurfaceType surfaceType)
    {
        if (routeMemory == null)
        {
            return;
        }

        MethodInfo method = ResolveRegisterHitMethod();

        if (method == null)
        {
            if (!warnedAboutMissingRegisterMethod)
            {
                Debug.LogWarning(
                    $"{nameof(LiDARScanner)} could not find RegisterHit(Vector3, SurfaceType) on {routeMemory.GetType().Name}.",
                    this);
                warnedAboutMissingRegisterMethod = true;
            }

            return;
        }

        method.Invoke(routeMemory, new object[] { worldPos, surfaceType });
    }

    private MethodInfo ResolveRegisterHitMethod()
    {
        if (routeMemory == null)
        {
            return null;
        }

        Type currentType = routeMemory.GetType();

        if (registerHitMethod != null && routeMemoryType == currentType)
        {
            return registerHitMethod;
        }

        routeMemoryType = currentType;
        registerHitMethod = currentType.GetMethod(
            "RegisterHit",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(Vector3), typeof(SurfaceType) },
            null);

        if (registerHitMethod != null)
        {
            warnedAboutMissingRegisterMethod = false;
        }

        return registerHitMethod;
    }

    private MonoBehaviour FindRouteMemoryComponent()
    {
        MonoBehaviour localRouteMemory = FindByTypeName(GetComponents<MonoBehaviour>(), RouteMemoryTypeName);

        if (localRouteMemory != null)
        {
            return localRouteMemory;
        }

        localRouteMemory = FindByTypeName(GetComponentsInParent<MonoBehaviour>(true), RouteMemoryTypeName);

        if (localRouteMemory != null)
        {
            return localRouteMemory;
        }

        localRouteMemory = FindByTypeName(GetComponentsInChildren<MonoBehaviour>(true), RouteMemoryTypeName);

        if (localRouteMemory != null)
        {
            return localRouteMemory;
        }

        MonoBehaviour[] sceneBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return FindByTypeName(sceneBehaviours, RouteMemoryTypeName);
    }

    private static MonoBehaviour FindByTypeName(MonoBehaviour[] behaviours, string typeName)
    {
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == typeName)
            {
                return behaviour;
            }
        }

        return null;
    }

    private void DrawPreviewGizmos()
    {
        int clampedHorizontalRayCount = Mathf.Max(1, horizontalRayCount);
        int clampedVerticalLayers = Mathf.Max(1, verticalLayers);
        float clampedScanRange = Mathf.Max(0.0f, scanRange);
        Vector3 origin = transform.position;

        Gizmos.color = new Color(1.0f, 0.85f, 0.25f, 0.6f);

        for (int verticalIndex = 0; verticalIndex < clampedVerticalLayers; verticalIndex++)
        {
            float verticalAngle = clampedVerticalLayers <= 1
                ? 0.0f
                : Mathf.Lerp(-HalfVerticalScanAngle, HalfVerticalScanAngle, verticalIndex / (float)(clampedVerticalLayers - 1));

            for (int horizontalIndex = 0; horizontalIndex < clampedHorizontalRayCount; horizontalIndex++)
            {
                float horizontalAngle = horizontalIndex * (360.0f / clampedHorizontalRayCount);
                Vector3 direction = GetWorldDirection(horizontalAngle, verticalAngle);
                Gizmos.DrawLine(origin, origin + (direction * clampedScanRange));
            }
        }
    }

    private LayerMask CreateDefaultScanMask()
    {
        int mask = 0;
        AddLayerToMask(ref mask, "Sidewalk");
        AddLayerToMask(ref mask, "Obstacle");
        AddLayerToMask(ref mask, "ParkPath");
        AddLayerToMask(ref mask, "Crosswalk");
        AddLayerToMask(ref mask, "Stairs");
        return mask;
    }

    private static void AddLayerToMask(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);

        if (layer >= 0)
        {
            mask |= 1 << layer;
        }
    }

    private static int CompareByDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }

    private static Color GetDebugColor(SurfaceType surfaceType, bool hasHit)
    {
        if (!hasHit)
        {
            return new Color(1.0f, 0.85f, 0.25f, 0.35f);
        }

        switch (surfaceType)
        {
            case SurfaceType.Sidewalk:
                return Color.green;
            case SurfaceType.Obstacle:
                return Color.red;
            case SurfaceType.ParkPath:
                return new Color(0.15f, 0.7f, 1.0f);
            case SurfaceType.Crosswalk:
                return Color.white;
            case SurfaceType.Stairs:
                return new Color(1.0f, 0.25f, 1.0f);
            default:
                return Color.yellow;
        }
    }
}
