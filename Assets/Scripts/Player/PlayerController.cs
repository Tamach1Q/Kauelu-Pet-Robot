using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerController : MonoBehaviour
{
    private const float GravityAcceleration = -9.81f;
    private const float GroundedVerticalVelocity = -2.0f;

    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float rotationSpeed = 8.0f;
    [SerializeField] private LeashSystem leashSystem;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        Vector2 input = ReadMovementInput(keyboard);
        Vector3 moveDirection = GetCameraRelativeMoveDirection(input);

        if (moveDirection.sqrMagnitude > 0.0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        ApplyGravity();

        Vector3 leashPull = leashSystem != null ? leashSystem.CurrentPullVector : Vector3.zero;
        Vector3 horizontalMotion = (moveDirection * moveSpeed) + leashPull;
        Vector3 verticalMotion = Vector3.up * verticalVelocity;
        Vector3 motion = (horizontalMotion + verticalMotion) * Time.deltaTime;

        characterController.Move(motion);
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity < 0.0f)
        {
            verticalVelocity = GroundedVerticalVelocity;
            return;
        }

        verticalVelocity += GravityAcceleration * Time.deltaTime;
    }

    // ReadMovementInput と GetCameraRelativeMoveDirection は既存のまま
    private static Vector2 ReadMovementInput(Keyboard keyboard)
    {
        float horizontal =
            (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1.0f : 0.0f) -
            (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1.0f : 0.0f);
        float vertical =
            (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1.0f : 0.0f) -
            (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1.0f : 0.0f);

        Vector2 input = new Vector2(horizontal, vertical);
        return Vector2.ClampMagnitude(input, 1.0f);
    }

    private static Vector3 GetCameraRelativeMoveDirection(Vector2 input)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return new Vector3(input.x, 0.0f, input.y);
        }

        Transform cameraTransform = mainCamera.transform;
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0.0f;
        right.y = 0.0f;

        forward.Normalize();
        right.Normalize();

        return (forward * input.y) + (right * input.x);
    }
}