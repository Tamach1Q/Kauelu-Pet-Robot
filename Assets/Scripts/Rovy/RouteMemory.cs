using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Describes the surface category associated with a learned waypoint.
/// </summary>
public enum SurfaceType
{
    /// <summary>
    /// The surface could not be identified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A flat sidewalk surface.
    /// </summary>
    Sidewalk = 1,

    /// <summary>
    /// A pedestrian crosswalk.
    /// </summary>
    Crosswalk = 2,

    /// <summary>
    /// A park path or trail.
    /// </summary>
    ParkPath = 3,

    /// <summary>
    /// A general road surface.
    /// </summary>
    Road = 4,

    /// <summary>
    /// A gravel or loose stone surface.
    /// </summary>
    Gravel = 5,

    /// <summary>
    /// A grass surface.
    /// </summary>
    Grass = 6,

    /// <summary>
    /// An obstructed area that should be strongly discouraged.
    /// </summary>
    Obstacle = 7,

    /// <summary>
    /// A staircase surface.
    /// </summary>
    Stairs = 8
}

/// <summary>
/// Represents a single learned waypoint in Rovy's route memory.
/// </summary>
[Serializable]
public struct WaypointNode
{
    /// <summary>
    /// The snapped world-space position used as the waypoint key.
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// The learned surface classification for this waypoint.
    /// </summary>
    public SurfaceType surface;

    /// <summary>
    /// The number of times Rovy has passed through this waypoint.
    /// </summary>
    public float visitCount;

    /// <summary>
    /// The novelty remaining at this waypoint, where 1 is fully novel.
    /// </summary>
    public float noveltyScore;

    /// <summary>
    /// Creates a new waypoint node value.
    /// </summary>
    /// <param name="position">The snapped world-space position of the node.</param>
    /// <param name="surface">The detected surface type.</param>
    /// <param name="visitCount">The accumulated visit count.</param>
    /// <param name="noveltyScore">The cached novelty score.</param>
    public WaypointNode(Vector3 position, SurfaceType surface, float visitCount, float noveltyScore)
    {
        this.position = position;
        this.surface = surface;
        this.visitCount = visitCount;
        this.noveltyScore = noveltyScore;
    }
}

/// <summary>
/// Stores learned waypoints, ranks them from Rovy's current mood, and persists route memory as JSON.
/// </summary>
[DisallowMultipleComponent]
public sealed class RouteMemory : MonoBehaviour
{
    [Serializable]
    private struct WaypointSaveRecord
    {
        public Vector3 position;
        public int surface;
        public float visitCount;
        public float noveltyScore;
        public int firstSeenDay;
        public int lastVisitedDay;
    }

    [Serializable]
    private sealed class RouteMemorySaveData
    {
        public int currentSimulationDay;
        public long savedAtUtcTicks;
        public List<WaypointSaveRecord> nodes = new List<WaypointSaveRecord>();
    }

    private struct WaypointMetadata
    {
        public int FirstSeenDay;
        public int LastVisitedDay;

        public WaypointMetadata(int firstSeenDay, int lastVisitedDay)
        {
            FirstSeenDay = firstSeenDay;
            LastVisitedDay = lastVisitedDay;
        }
    }

    private const float DefaultNovelty = 1.0f;
    private const float MinGridSize = 0.05f;
    private const float DistanceEpsilon = 0.0001f;
    private const float MinimumAutoSaveInterval = 0.5f;
    private const string DefaultSaveFileName = "rovy_route_memory.json";

    [Header("Node Sampling")]
    [SerializeField] [Min(MinGridSize)] private float nodeGridSize = 1.0f;
    [SerializeField] [Min(0.1f)] private float visitRadius = 0.9f;
    [SerializeField] [Min(0.1f)] private float minimumWaypointDistance = 0.75f;
    [SerializeField] [Min(0.01f)] private float noveltyDecayRate = 0.35f;

    [Header("Scoring")]
    [SerializeField] [Min(0.0f)] private float familiarityBonusWeight = 0.12f;
    [SerializeField] [Min(0.0f)] private float favoriteRouteBonus = 0.45f;

    [Header("Favorites")]
    [SerializeField] [Min(0)] private int currentSimulationDay;
    [SerializeField] [Min(1)] private int favoriteRouteAgeDays = 7;
    [SerializeField] [Min(1.0f)] private float favoriteRouteVisitThreshold = 6.0f;

    [Header("Persistence")]
    [SerializeField] private string saveFileName = DefaultSaveFileName;
    [SerializeField] [Min(MinimumAutoSaveInterval)] private float autoSaveIntervalSeconds = 5.0f;

    private readonly Dictionary<Vector3, WaypointNode> knownNodes = new Dictionary<Vector3, WaypointNode>();
    private readonly Dictionary<Vector3, WaypointMetadata> nodeMetadata = new Dictionary<Vector3, WaypointMetadata>();

    private bool isDirty;
    private float lastSaveRealtime;
    private Vector3? lastVisitedNodeKey;

    /// <summary>
    /// Gets the number of currently learned waypoint nodes.
    /// </summary>
    public int KnownNodeCount => knownNodes.Count;

    /// <summary>
    /// Updates the simulation day used for favorite-route aging and persistence.
    /// </summary>
    /// <param name="day">The current simulation day index.</param>
    public void SetSimulationDay(int day)
    {
        int nextDay = Mathf.Max(0, day);

        if (currentSimulationDay == nextDay)
        {
            return;
        }

        currentSimulationDay = nextDay;
        isDirty = true;
    }

    private void Awake()
    {
        ClampSerializedValues();

        if (Application.isPlaying)
        {
            LoadFromDisk();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !isDirty)
        {
            return;
        }

        if ((Time.unscaledTime - lastSaveRealtime) < autoSaveIntervalSeconds)
        {
            return;
        }

        SaveToDisk();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SaveToDisk();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveToDisk();
        }
    }

    private void OnApplicationQuit()
    {
        SaveToDisk();
    }

    private void OnValidate()
    {
        ClampSerializedValues();
    }

    /// <summary>
    /// Registers a newly observed waypoint hit from the environment scan.
    /// </summary>
    /// <param name="worldPos">The world-space hit position.</param>
    /// <param name="type">The detected surface type.</param>
    public void RegisterHit(Vector3 worldPos, SurfaceType type)
    {
        Vector3 key = NormalizePosition(worldPos);

        if (knownNodes.TryGetValue(key, out WaypointNode existingNode))
        {
            if (type != SurfaceType.Unknown || existingNode.surface == SurfaceType.Unknown)
            {
                existingNode.surface = type;
                knownNodes[key] = existingNode;
                isDirty = true;
            }

            if (!nodeMetadata.ContainsKey(key))
            {
                nodeMetadata[key] = new WaypointMetadata(currentSimulationDay, currentSimulationDay);
                isDirty = true;
            }

            return;
        }

        WaypointNode node = new WaypointNode(
            key,
            type,
            0.0f,
            DefaultNovelty);

        knownNodes.Add(key, node);
        nodeMetadata[key] = new WaypointMetadata(currentSimulationDay, currentSimulationDay);
        isDirty = true;
    }

    /// <summary>
    /// Returns the highest-scoring next waypoint based on mood, difficulty, novelty, and route familiarity.
    /// </summary>
    /// <param name="currentPos">Rovy's current world-space position.</param>
    /// <param name="energy">The current energy mood value in the range 0-1.</param>
    /// <param name="curiosity">The current curiosity mood value in the range 0-1.</param>
    /// <param name="comfort">The current comfort mood value in the range 0-1.</param>
    /// <returns>The best next waypoint, or the current position when no useful node exists.</returns>
    public Vector3 GetNextWaypoint(Vector3 currentPos, float energy, float curiosity, float comfort)
    {
        if (knownNodes.Count == 0)
        {
            return currentPos;
        }

        UpdateVisitProgress(currentPos);

        energy = Mathf.Clamp01(energy);
        curiosity = Mathf.Clamp01(curiosity);
        comfort = Mathf.Clamp01(comfort);

        float maxDistance = GetMaxDistanceFrom(currentPos);

        if (maxDistance <= DistanceEpsilon)
        {
            return currentPos;
        }

        float bestScore = float.NegativeInfinity;
        Vector3 bestWaypoint = currentPos;
        bool foundWaypoint = false;

        foreach (KeyValuePair<Vector3, WaypointNode> pair in knownNodes)
        {
            WaypointNode node = pair.Value;
            float distance = Vector3.Distance(currentPos, node.position);

            if (distance < minimumWaypointDistance)
            {
                continue;
            }

            float distanceScore = Mathf.Clamp01(distance / maxDistance);
            float difficultyScore = GetDifficultyScore(node.surface);
            float visitBonus = CalculateVisitBonus(pair.Key, node);
            float score =
                (energy * distanceScore) -
                ((1.0f - comfort) * difficultyScore) +
                (curiosity * node.noveltyScore) +
                visitBonus;

            if (!foundWaypoint || score > bestScore)
            {
                bestScore = score;
                bestWaypoint = node.position;
                foundWaypoint = true;
            }
        }

        return foundWaypoint ? bestWaypoint : currentPos;
    }

    /// <summary>
    /// Returns the most important learned routes for UI display.
    /// </summary>
    /// <param name="count">The maximum number of routes to return.</param>
    /// <returns>A sorted list of the top route nodes.</returns>
    public List<WaypointNode> GetTopRoutes(int count)
    {
        if (count <= 0 || knownNodes.Count == 0)
        {
            return new List<WaypointNode>();
        }

        List<WaypointNode> routes = new List<WaypointNode>(knownNodes.Values);
        routes.Sort(CompareRoutesForDisplay);

        if (routes.Count > count)
        {
            routes.RemoveRange(count, routes.Count - count);
        }

        return routes;
    }

    [ContextMenu("Save Route Memory")]
    private void SaveRouteMemoryNow()
    {
        SaveToDisk();
    }

    [ContextMenu("Load Route Memory")]
    private void LoadRouteMemoryNow()
    {
        LoadFromDisk();
    }

    private void ClampSerializedValues()
    {
        nodeGridSize = Mathf.Max(MinGridSize, nodeGridSize);
        visitRadius = Mathf.Max(0.1f, visitRadius);
        minimumWaypointDistance = Mathf.Max(0.1f, minimumWaypointDistance);
        noveltyDecayRate = Mathf.Max(0.01f, noveltyDecayRate);
        familiarityBonusWeight = Mathf.Max(0.0f, familiarityBonusWeight);
        favoriteRouteBonus = Mathf.Max(0.0f, favoriteRouteBonus);
        currentSimulationDay = Mathf.Max(0, currentSimulationDay);
        favoriteRouteAgeDays = Mathf.Max(1, favoriteRouteAgeDays);
        favoriteRouteVisitThreshold = Mathf.Max(1.0f, favoriteRouteVisitThreshold);
        autoSaveIntervalSeconds = Mathf.Max(MinimumAutoSaveInterval, autoSaveIntervalSeconds);

        if (string.IsNullOrWhiteSpace(saveFileName))
        {
            saveFileName = DefaultSaveFileName;
        }
    }

    private void UpdateVisitProgress(Vector3 currentPos)
    {
        if (knownNodes.Count == 0)
        {
            lastVisitedNodeKey = null;
            return;
        }

        Vector3 nearestKey = default;
        float nearestDistance = float.MaxValue;
        bool hasNearest = false;

        foreach (KeyValuePair<Vector3, WaypointNode> pair in knownNodes)
        {
            float distance = Vector3.Distance(currentPos, pair.Key);

            if (distance > visitRadius || distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestKey = pair.Key;
            hasNearest = true;
        }

        if (!hasNearest)
        {
            lastVisitedNodeKey = null;
            return;
        }

        if (lastVisitedNodeKey.HasValue && AreSamePosition(lastVisitedNodeKey.Value, nearestKey))
        {
            return;
        }

        IncrementVisitCount(nearestKey);
        lastVisitedNodeKey = nearestKey;
    }

    private void IncrementVisitCount(Vector3 key)
    {
        if (!knownNodes.TryGetValue(key, out WaypointNode node))
        {
            return;
        }

        node.visitCount += 1.0f;
        node.noveltyScore = CalculateNoveltyScore(node.visitCount);
        knownNodes[key] = node;

        WaypointMetadata metadata = GetOrCreateMetadata(key);
        metadata.LastVisitedDay = currentSimulationDay;
        nodeMetadata[key] = metadata;

        isDirty = true;
    }

    private float GetMaxDistanceFrom(Vector3 currentPos)
    {
        float maxDistance = 0.0f;

        foreach (WaypointNode node in knownNodes.Values)
        {
            float distance = Vector3.Distance(currentPos, node.position);

            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        return maxDistance;
    }

    private float CalculateVisitBonus(Vector3 key, WaypointNode node)
    {
        float familiarityBonus = Mathf.Log(1.0f + node.visitCount, 2.0f) * familiarityBonusWeight;

        if (IsFavoriteRoute(key, node))
        {
            familiarityBonus += favoriteRouteBonus;
        }

        return familiarityBonus;
    }

    private bool IsFavoriteRoute(Vector3 key, WaypointNode node)
    {
        WaypointMetadata metadata = GetMetadataOrDefault(key);
        int ageInDays = Mathf.Max(0, currentSimulationDay - metadata.FirstSeenDay);
        return ageInDays >= favoriteRouteAgeDays && node.visitCount >= favoriteRouteVisitThreshold;
    }

    private float CalculateNoveltyScore(float visitCount)
    {
        return Mathf.Clamp01(Mathf.Exp(-visitCount * noveltyDecayRate));
    }

    private float GetDifficultyScore(SurfaceType surface)
    {
        switch (surface)
        {
            case SurfaceType.Sidewalk:
                return 0.15f;
            case SurfaceType.Crosswalk:
                return 0.25f;
            case SurfaceType.ParkPath:
                return 0.35f;
            case SurfaceType.Road:
                return 0.60f;
            case SurfaceType.Gravel:
                return 0.70f;
            case SurfaceType.Grass:
                return 0.75f;
            case SurfaceType.Stairs:
                return 0.85f;
            case SurfaceType.Obstacle:
                return 1.00f;
            default:
                return 0.50f;
        }
    }

    private int CompareRoutesForDisplay(WaypointNode left, WaypointNode right)
    {
        bool leftIsFavorite = IsFavoriteRoute(left.position, left);
        bool rightIsFavorite = IsFavoriteRoute(right.position, right);

        if (leftIsFavorite != rightIsFavorite)
        {
            return rightIsFavorite.CompareTo(leftIsFavorite);
        }

        int visitComparison = right.visitCount.CompareTo(left.visitCount);

        if (visitComparison != 0)
        {
            return visitComparison;
        }

        return right.noveltyScore.CompareTo(left.noveltyScore);
    }

    private WaypointMetadata GetOrCreateMetadata(Vector3 key)
    {
        if (nodeMetadata.TryGetValue(key, out WaypointMetadata metadata))
        {
            return metadata;
        }

        WaypointMetadata createdMetadata = new WaypointMetadata(currentSimulationDay, currentSimulationDay);
        nodeMetadata[key] = createdMetadata;
        isDirty = true;
        return createdMetadata;
    }

    private WaypointMetadata GetMetadataOrDefault(Vector3 key)
    {
        if (nodeMetadata.TryGetValue(key, out WaypointMetadata metadata))
        {
            return metadata;
        }

        return new WaypointMetadata(currentSimulationDay, currentSimulationDay);
    }

    private Vector3 NormalizePosition(Vector3 worldPos)
    {
        float inverseGrid = 1.0f / nodeGridSize;

        return new Vector3(
            Mathf.Round(worldPos.x * inverseGrid) / inverseGrid,
            Mathf.Round(worldPos.y * inverseGrid) / inverseGrid,
            Mathf.Round(worldPos.z * inverseGrid) / inverseGrid);
    }

    private string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    private void SaveToDisk()
    {
        if (!isDirty && Application.isPlaying)
        {
            return;
        }

        ClampSerializedValues();

        RouteMemorySaveData saveData = new RouteMemorySaveData
        {
            currentSimulationDay = currentSimulationDay,
            savedAtUtcTicks = DateTime.UtcNow.Ticks
        };

        foreach (KeyValuePair<Vector3, WaypointNode> pair in knownNodes)
        {
            WaypointMetadata metadata = GetMetadataOrDefault(pair.Key);
            WaypointNode node = pair.Value;

            saveData.nodes.Add(new WaypointSaveRecord
            {
                position = node.position,
                surface = (int)node.surface,
                visitCount = node.visitCount,
                noveltyScore = node.noveltyScore,
                firstSeenDay = metadata.FirstSeenDay,
                lastVisitedDay = metadata.LastVisitedDay
            });
        }

        string path = GetSavePath();

        try
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(path, json);
            isDirty = false;

            if (Application.isPlaying)
            {
                lastSaveRealtime = Time.unscaledTime;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                string.Format(
                    "RouteMemory failed to save route data to '{0}'. {1}",
                    path,
                    exception.Message),
                this);
        }
    }

    private void LoadFromDisk()
    {
        ClampSerializedValues();

        string path = GetSavePath();

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            RouteMemorySaveData saveData = JsonUtility.FromJson<RouteMemorySaveData>(json);

            if (saveData == null)
            {
                return;
            }

            knownNodes.Clear();
            nodeMetadata.Clear();

            currentSimulationDay = Mathf.Max(0, saveData.currentSimulationDay);

            if (saveData.nodes != null)
            {
                for (int i = 0; i < saveData.nodes.Count; i++)
                {
                    WaypointSaveRecord record = saveData.nodes[i];
                    Vector3 key = NormalizePosition(record.position);
                    float visitCount = Mathf.Max(0.0f, record.visitCount);
                    float noveltyScore = record.noveltyScore > 0.0f
                        ? Mathf.Clamp01(record.noveltyScore)
                        : CalculateNoveltyScore(visitCount);

                    WaypointNode node = new WaypointNode(
                        key,
                        ToSurfaceType(record.surface),
                        visitCount,
                        noveltyScore);

                    knownNodes[key] = node;
                    nodeMetadata[key] = new WaypointMetadata(
                        Mathf.Max(0, record.firstSeenDay),
                        Mathf.Max(0, record.lastVisitedDay));
                }
            }

            lastVisitedNodeKey = null;
            isDirty = false;

            if (Application.isPlaying)
            {
                lastSaveRealtime = Time.unscaledTime;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                string.Format(
                    "RouteMemory failed to load route data from '{0}'. {1}",
                    path,
                    exception.Message),
                this);
        }
    }

    private SurfaceType ToSurfaceType(int value)
    {
        if (value < (int)SurfaceType.Unknown || value > (int)SurfaceType.Stairs)
        {
            return SurfaceType.Unknown;
        }

        return (SurfaceType)value;
    }

    private static bool AreSamePosition(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude <= DistanceEpsilon;
    }
}
