using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class LeashSystem : MonoBehaviour
{
    private const float LineWidth = 0.02f;
    private const int MinimumLineSegments = 2;
    private const float SagDistanceMultiplier = 5.0f;

    [SerializeField] private Transform rovyAttachPoint;
    [SerializeField] private Transform playerAttachPoint;
    [SerializeField] private int lineSegments = 12;
    [SerializeField] private float sagAmount = 0.4f;
    [SerializeField] private Color leashColor = Color.gray;

    private LineRenderer lineRenderer;
    private Material runtimeLineMaterial;
    private Vector3[] linePositions;

    private void Reset()
    {
        rovyAttachPoint = transform;

        if (playerAttachPoint == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();

            if (playerController != null)
            {
                playerAttachPoint = playerController.transform;
            }
        }
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (rovyAttachPoint == null)
        {
            rovyAttachPoint = transform;
        }

        if (playerAttachPoint == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();

            if (playerController != null)
            {
                playerAttachPoint = playerController.transform;
            }
        }
    }

    private void Start()
    {
        ConfigureLineRenderer();
        UpdateLeashPositions();
    }

    private void LateUpdate()
    {
        UpdateLeashPositions();
    }

    private void OnValidate()
    {
        lineSegments = Mathf.Max(MinimumLineSegments, lineSegments);
        sagAmount = Mathf.Max(0.0f, sagAmount);

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            return;
        }

        EnsureLineBuffer();
        ApplyLineAppearance();
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeLineMaterial);
            return;
        }

        DestroyImmediate(runtimeLineMaterial);
    }

    private void ConfigureLineRenderer()
    {
        EnsureLineBuffer();

        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = LineWidth;
        lineRenderer.loop = false;

        if (runtimeLineMaterial == null)
        {
            runtimeLineMaterial = CreateDefaultUrpLineMaterial();
        }

        if (runtimeLineMaterial != null)
        {
            lineRenderer.sharedMaterial = runtimeLineMaterial;
        }

        ApplyLineAppearance();
    }

    private void ApplyLineAppearance()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = LineWidth;
        lineRenderer.startColor = leashColor;
        lineRenderer.endColor = leashColor;

        if (runtimeLineMaterial == null)
        {
            return;
        }

        if (runtimeLineMaterial.HasProperty("_BaseColor"))
        {
            runtimeLineMaterial.SetColor("_BaseColor", leashColor);
        }

        if (runtimeLineMaterial.HasProperty("_Color"))
        {
            runtimeLineMaterial.SetColor("_Color", leashColor);
        }
    }

    private void EnsureLineBuffer()
    {
        int positionCount = lineSegments + 1;

        if (linePositions == null || linePositions.Length != positionCount)
        {
            linePositions = new Vector3[positionCount];
        }

        lineRenderer.positionCount = positionCount;
    }

    private void UpdateLeashPositions()
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (rovyAttachPoint == null || playerAttachPoint == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        EnsureLineBuffer();

        Vector3 startPoint = rovyAttachPoint.position;
        Vector3 endPoint = playerAttachPoint.position;
        float distance = Vector3.Distance(startPoint, endPoint);
        float dynamicSag = CalculateDynamicSag(distance);
        Vector3 sagDirection = GetSagDirection();
        int lastIndex = linePositions.Length - 1;

        for (int i = 0; i <= lastIndex; i++)
        {
            float t = lastIndex == 0 ? 0.0f : i / (float)lastIndex;
            Vector3 point = Vector3.Lerp(startPoint, endPoint, t);
            float sagOffset = Mathf.Sin(t * Mathf.PI) * dynamicSag;

            linePositions[i] = point + (sagDirection * sagOffset);
        }

        lineRenderer.SetPositions(linePositions);
    }

    private float CalculateDynamicSag(float distance)
    {
        if (sagAmount <= 0.0f)
        {
            return 0.0f;
        }

        float maximumSagDistance = Mathf.Max(0.01f, sagAmount * SagDistanceMultiplier);
        float slackRatio = 1.0f - Mathf.Clamp01(distance / maximumSagDistance);
        float sagFactor = slackRatio * slackRatio;
        return sagAmount * sagFactor;
    }

    private static Vector3 GetSagDirection()
    {
        if (Physics.gravity.sqrMagnitude > 0.0001f)
        {
            return Physics.gravity.normalized;
        }

        return Vector3.down;
    }

    private static Material CreateDefaultUrpLineMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = "LeashSystem_RuntimeLineMaterial"
        };

        return material;
    }
}
