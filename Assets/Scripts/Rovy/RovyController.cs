using Rovy.Control;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class RovyController : MonoBehaviour
{
    public enum RovyState
    {
        Idle,
        Following,
        Waiting,
        Exploring
    }

    // NavMesh 追従モードと PID 制御モードを切り替える。
    // PID モード時は NavMeshAgent を無効化し、Rigidbody 駆動に移譲する。
    public enum FollowMode { NavMesh, Pid }

    [Header("Follow Mode")]
    [SerializeField] private FollowMode followMode = FollowMode.NavMesh;
    [SerializeField] private LeadFollowController leadFollowController;

    [SerializeField] private Transform playerTransform;
    [SerializeField] private float leashLength = 2.0f;
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private RovyState currentState = RovyState.Idle;

    [SerializeField] private RouteMemory routeMemory;
    [SerializeField] private MoodEngine moodEngine;
    [SerializeField] private float exploreRadius = 15.0f;
    [SerializeField] private float waypointReachedDistance = 1.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float exploreEnergyThreshold = 0.7f;

    private const float RotationSpeed = 8.0f;
    private const float RotationThreshold = 0.0001f;

    private NavMeshAgent navMeshAgent;

    /// <summary>
    /// Gets the current follow state of Rovy.
    /// </summary>
    public RovyState CurrentState => currentState;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.updateRotation = false;
    }

    private void Start()
    {
        ApplyAgentSettings();
        ApplyFollowMode();
    }

    private void OnValidate()
    {
        leashLength = Mathf.Max(0.0f, leashLength);
        moveSpeed = Mathf.Max(0.0f, moveSpeed);

        if (navMeshAgent != null)
        {
            ApplyAgentSettings();
        }
    }

    private void Update()
    {
        if (followMode == FollowMode.Pid)
            return;

        if (!navMeshAgent.isOnNavMesh)
        {
            StopMovement();
            SetState(RovyState.Idle);
            return;
        }

        if (TryApplyLeashSafetyOverride())
        {
            UpdateRotation();
            return;
        }

        if (TryUpdateExploreBehavior())
        {
            UpdateRotation();
            return;
        }

        UpdateFollowBehavior();
        UpdateRotation();
    }

    // PID / NavMesh モードを切り替える（外部からも呼び出し可）
    public void SetFollowMode(FollowMode mode)
    {
        followMode = mode;
        ApplyFollowMode();
    }

    private void ApplyFollowMode()
    {
        bool isPid = followMode == FollowMode.Pid;

        navMeshAgent.enabled = !isPid;

        if (leadFollowController != null)
            leadFollowController.enabled = isPid;

        if (isPid)
            StopMovementIfActive();
    }

    private void StopMovementIfActive()
    {
        if (navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh && navMeshAgent.hasPath)
            navMeshAgent.ResetPath();
    }

    /// <summary>
    /// Assigns the player transform that Rovy should follow.
    /// </summary>
    /// <param name="target">The player transform to follow.</param>
    public void SetPlayerTransform(Transform target)
    {
        playerTransform = target;

        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            UpdateFollowBehavior();
        }
    }

    /// <summary>
    /// Updates the movement speed used by the NavMeshAgent.
    /// </summary>
    /// <param name="speed">The new movement speed.</param>
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0.0f, speed);
        ApplyAgentSettings();
    }

    /// <summary>
    /// Updates the leash distance that determines when Rovy follows the player.
    /// </summary>
    /// <param name="length">The new leash length.</param>
    public void SetLeashLength(float length)
    {
        leashLength = Mathf.Max(0.0f, length);

        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            UpdateFollowBehavior();
        }
    }

    private void ApplyAgentSettings()
    {
        if (navMeshAgent == null)
        {
            return;
        }

        navMeshAgent.speed = moveSpeed;
    }

    private void UpdateFollowBehavior()
    {
        if (playerTransform == null)
        {
            StopMovement();
            SetState(RovyState.Idle);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > leashLength)
        {
            navMeshAgent.SetDestination(playerTransform.position);
            SetState(RovyState.Following);
            return;
        }

        StopMovement();
        SetState(RovyState.Waiting);
    }

    private void UpdateRotation()
    {
        Vector3 movementDirection = navMeshAgent.desiredVelocity;
        movementDirection.y = 0.0f;

        if (movementDirection.sqrMagnitude <= RotationThreshold)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(movementDirection.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            RotationSpeed * Time.deltaTime);
    }

    private void StopMovement()
    {
        if (navMeshAgent.hasPath)
        {
            navMeshAgent.ResetPath();
        }
    }

    private void SetState(RovyState nextState)
    {
        currentState = nextState;
    }

    /// <summary>
    /// Forces Rovy back into the Following state when the player moves beyond twice the leash length.
    /// </summary>
    /// <returns>True when the safety override took control this frame.</returns>
    private bool TryApplyLeashSafetyOverride()
    {
        if (playerTransform == null)
        {
            return false;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= leashLength * 2.0f)
        {
            return false;
        }

        navMeshAgent.SetDestination(playerTransform.position);
        SetState(RovyState.Following);
        return true;
    }

    /// <summary>
    /// Drives autonomous exploration toward a RouteMemory waypoint when energy is sufficient.
    /// </summary>
    /// <returns>True when explore behavior controlled movement this frame.</returns>
    private bool TryUpdateExploreBehavior()
    {
        if (routeMemory == null || moodEngine == null)
        {
            return false;
        }

        if (routeMemory.KnownNodeCount <= 0)
        {
            return false;
        }

        if (moodEngine.Energy <= exploreEnergyThreshold)
        {
            return false;
        }

        bool needsNewWaypoint =
            currentState != RovyState.Exploring ||
            !navMeshAgent.hasPath ||
            HasReachedCurrentWaypoint();

        if (needsNewWaypoint)
        {
            Vector3 nextWaypoint = routeMemory.GetNextWaypoint(
                transform.position,
                moodEngine.Energy,
                moodEngine.Curiosity,
                moodEngine.Comfort);

            navMeshAgent.SetDestination(nextWaypoint);
        }

        SetState(RovyState.Exploring);
        return true;
    }

    /// <summary>
    /// Determines whether Rovy has arrived at the active NavMesh destination within the reached threshold.
    /// </summary>
    /// <returns>True when the remaining distance is below <see cref="waypointReachedDistance"/>.</returns>
    private bool HasReachedCurrentWaypoint()
    {
        if (!navMeshAgent.hasPath)
        {
            return true;
        }

        Vector3 destination = navMeshAgent.destination;
        return Vector3.Distance(transform.position, destination) < waypointReachedDistance;
    }
}
