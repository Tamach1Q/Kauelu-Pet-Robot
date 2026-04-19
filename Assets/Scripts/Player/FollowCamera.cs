using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class FollowCamera : MonoBehaviour
{
    private const float MinimumFollowDistance = 0.1f;
    private const float MinimumSmoothSpeed = 0.0f;
    private const float MinimumRotationSensitivity = 0.0f;
    private const float MinimumPitch = -10.0f;
    private const float MaximumPitch = 45.0f;
    private const float DefaultPitch = 10.0f;

    [SerializeField] private Transform target;
    [SerializeField] private float followDistance = 4.0f;
    [SerializeField] private float followHeight = 2.0f;
    [SerializeField] private float smoothSpeed = 5.0f;
    [SerializeField] private float rotationSensitivity = 2.0f;

    private float currentYaw;
    private float currentPitch = DefaultPitch;

    private void Start()
    {
        InitializeOrbitAngles();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateOrbitAngles();
        UpdateCameraTransform();
    }

    private void OnValidate()
    {
        followDistance = Mathf.Max(MinimumFollowDistance, followDistance);
        followHeight = Mathf.Max(0.0f, followHeight);
        smoothSpeed = Mathf.Max(MinimumSmoothSpeed, smoothSpeed);
        rotationSensitivity = Mathf.Max(MinimumRotationSensitivity, rotationSensitivity);
    }

    /// <summary>
    /// Assigns a new follow target for the camera.
    /// </summary>
    /// <param name="newTarget">The transform that the camera should follow.</param>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        InitializeOrbitAngles();
        SnapToTarget();
    }

    /// <summary>
    /// Immediately moves the camera to its desired follow position.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = GetDesiredPosition();
        Quaternion desiredRotation = GetDesiredRotation(desiredPosition);

        transform.SetPositionAndRotation(desiredPosition, desiredRotation);
    }

    private void InitializeOrbitAngles()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
        }
        else
        {
            currentYaw = transform.eulerAngles.y;
        }

        currentPitch = DefaultPitch;
    }

    private void UpdateOrbitAngles()
    {
        if (TryReadOrbitMouseDelta(out Vector2 mouseDelta))
        {
            float mouseX = mouseDelta.x;
            float mouseY = mouseDelta.y;

            currentYaw += mouseX * rotationSensitivity;
            currentPitch = Mathf.Clamp(
                currentPitch - (mouseY * rotationSensitivity),
                MinimumPitch,
                MaximumPitch);

            return;
        }

        if (smoothSpeed <= 0.0f)
        {
            currentYaw = target.eulerAngles.y;
            return;
        }

        currentYaw = Mathf.LerpAngle(currentYaw, target.eulerAngles.y, smoothSpeed * Time.deltaTime);
    }

    private static bool TryReadOrbitMouseDelta(out Vector2 mouseDelta)
    {
        if (Mouse.current == null)
        {
            mouseDelta = Vector2.zero;
            return false;
        }

        Mouse mouse = Mouse.current;

        if (!mouse.rightButton.isPressed)
        {
            mouseDelta = Vector2.zero;
            return false;
        }

        mouseDelta = mouse.delta.ReadValue() * 0.1f;
        return true;
    }

    private void UpdateCameraTransform()
    {
        Vector3 desiredPosition = GetDesiredPosition();
        Quaternion desiredRotation = GetDesiredRotation(desiredPosition);

        if (smoothSpeed <= 0.0f)
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            return;
        }

        float interpolation = 1.0f - Mathf.Exp(-smoothSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, interpolation);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, interpolation);
    }

    private Vector3 GetDesiredPosition()
    {
        Quaternion orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0.0f);
        Vector3 distanceOffset = orbitRotation * (Vector3.back * followDistance);
        return target.position + (Vector3.up * followHeight) + distanceOffset;
    }

    private Quaternion GetDesiredRotation(Vector3 cameraPosition)
    {
        Vector3 lookTarget = target.position + (Vector3.up * followHeight);
        Vector3 lookDirection = lookTarget - cameraPosition;

        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return transform.rotation;
        }

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }
}
