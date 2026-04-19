using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives Rovy's pet-like idle expressions (Sniff, LookBack, Wag, SlowDown, PullForward)
/// based on the current mood state and the player's behavior.
/// </summary>
[RequireComponent(typeof(RovyController))]
[RequireComponent(typeof(MoodEngine))]
public sealed class BehaviorExpressions : MonoBehaviour
{
    private const float LookBackCheckInterval = 0.5f;
    private const float SniffDurationMin = 1.0f;
    private const float SniffDurationMax = 2.0f;
    private const float SniffHeadLowerAngle = 35.0f;
    private const float WagDuration = 1.5f;
    private const float WagTiltAngle = 15.0f;
    private const float WagFrequency = 6.0f;
    private const float LookBackDuration = 1.0f;
    private const float LookBackRotationSpeed = 6.0f;
    private const float PullForwardDuration = 1.5f;
    private const float PullForwardDistance = 1.5f;
    private const float WaypointProximity = 1.5f;

    [Header("Behavior Tuning")]
    [SerializeField] private float behaviorCooldown = 8.0f;
    [SerializeField] private float sniffThreshold = 0.6f;
    [SerializeField] private float pullEnergyThreshold = 0.8f;
    [SerializeField] private float lowComfortThreshold = 0.3f;

    [Header("Speed Presets")]
    [SerializeField] private float normalSpeed = 1.2f;
    [SerializeField] private float slowSpeed = 0.7f;
    [SerializeField] private float pullSpeed = 1.8f;

    [Header("Look Back Tuning")]
    [Tooltip("The minimum player distance delta over the sample window that triggers a LookBack.")]
    [SerializeField] private float lookBackDistanceDelta = 1.5f;

    private RovyController rovyController;
    private MoodEngine moodEngine;
    private NavMeshAgent navMeshAgent;
    private Transform playerTransform;
    private MonoBehaviour playerInteractionBehaviour;

    private bool isBehaviorActive;
    private float nextAvailableTime;
    private float playerDistanceSampleTime;
    private float playerDistanceSample;
    private bool isSlowedByComfort;

    /// <summary>
    /// Gets a value indicating whether an expressive behavior is currently running.
    /// </summary>
    public bool IsBehaviorActive => isBehaviorActive;

    private void Awake()
    {
        rovyController = GetComponent<RovyController>();
        moodEngine = GetComponent<MoodEngine>();
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        ResolvePlayerReferences();

        if (playerTransform != null)
        {
            playerDistanceSample = Vector3.Distance(transform.position, playerTransform.position);
        }

        playerDistanceSampleTime = Time.time;
        ApplyBaseSpeed();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            ResolvePlayerReferences();
        }

        UpdateComfortSpeed();
        TryTriggerLookBack();
        TryTriggerSniff();
        TryTriggerPullForward();
    }

    /// <summary>
    /// Invoked via UnityEvent (e.g. from <see cref="MoodEngine.OnMoodChanged"/>
    /// or a player interaction component) when the player calls Rovy's name.
    /// Triggers the Wag behavior if cooldown allows.
    /// </summary>
    public void NotifyNameCalled()
    {
        if (!CanStartBehavior())
        {
            return;
        }

        StartCoroutine(WagRoutine());
    }

    private void ResolvePlayerReferences()
    {
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlayerInteraction)
            {
                playerInteractionBehaviour = behaviours[i];
                break;
            }
        }
    }

    private void UpdateComfortSpeed()
    {
        if (isBehaviorActive)
        {
            return;
        }

        bool shouldSlow = moodEngine.Comfort < lowComfortThreshold;
        if (shouldSlow == isSlowedByComfort)
        {
            return;
        }

        isSlowedByComfort = shouldSlow;
        ApplyBaseSpeed();
    }

    private void ApplyBaseSpeed()
    {
        float target = isSlowedByComfort ? slowSpeed : normalSpeed;
        rovyController.SetMoveSpeed(target);
    }

    private void TryTriggerSniff()
    {
        if (!CanStartBehavior())
        {
            return;
        }

        if (moodEngine.Curiosity <= sniffThreshold)
        {
            return;
        }

        if (!IsNearKnownWaypoint())
        {
            return;
        }

        StartCoroutine(SniffRoutine());
    }

    private void TryTriggerLookBack()
    {
        if (playerTransform == null)
        {
            return;
        }

        if (Time.time - playerDistanceSampleTime < LookBackCheckInterval)
        {
            return;
        }

        float currentDistance = Vector3.Distance(transform.position, playerTransform.position);
        float delta = currentDistance - playerDistanceSample;

        playerDistanceSample = currentDistance;
        playerDistanceSampleTime = Time.time;

        if (delta < lookBackDistanceDelta)
        {
            return;
        }

        if (!CanStartBehavior())
        {
            return;
        }

        StartCoroutine(LookBackRoutine());
    }

    private void TryTriggerPullForward()
    {
        if (!CanStartBehavior())
        {
            return;
        }

        if (moodEngine.Energy <= pullEnergyThreshold)
        {
            return;
        }

        if (playerTransform == null)
        {
            return;
        }

        StartCoroutine(PullForwardRoutine());
    }

    private bool CanStartBehavior()
    {
        return !isBehaviorActive && Time.time >= nextAvailableTime;
    }

    private bool IsNearKnownWaypoint()
    {
        RouteMemory routeMemory = GetComponent<RouteMemory>();
        if (routeMemory == null || routeMemory.KnownNodeCount == 0)
        {
            return false;
        }

        Vector3 nextWaypoint = routeMemory.GetNextWaypoint(
            transform.position,
            moodEngine.Energy,
            moodEngine.Curiosity,
            moodEngine.Comfort);

        float distance = Vector3.Distance(transform.position, nextWaypoint);
        return distance <= WaypointProximity;
    }

    private void StartBehaviorLock()
    {
        isBehaviorActive = true;
    }

    private void EndBehaviorLock()
    {
        isBehaviorActive = false;
        nextAvailableTime = Time.time + behaviorCooldown;
        ApplyBaseSpeed();
    }

    private IEnumerator SniffRoutine()
    {
        StartBehaviorLock();

        bool hadPath = navMeshAgent.isOnNavMesh && navMeshAgent.hasPath;
        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
        }

        Quaternion originalRotation = transform.localRotation;
        Quaternion loweredRotation = originalRotation * Quaternion.Euler(SniffHeadLowerAngle, 0.0f, 0.0f);
        float duration = Random.Range(SniffDurationMin, SniffDurationMax);
        float elapsed = 0.0f;

        while (elapsed < duration * 0.3f)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(originalRotation, loweredRotation, elapsed / (duration * 0.3f));
            yield return null;
        }

        yield return new WaitForSeconds(duration * 0.4f);

        elapsed = 0.0f;
        while (elapsed < duration * 0.3f)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(loweredRotation, originalRotation, elapsed / (duration * 0.3f));
            yield return null;
        }

        transform.localRotation = originalRotation;

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
        }

        _ = hadPath;
        EndBehaviorLock();
    }

    private IEnumerator LookBackRoutine()
    {
        StartBehaviorLock();

        float elapsed = 0.0f;
        while (elapsed < LookBackDuration)
        {
            if (playerTransform == null)
            {
                break;
            }

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0.0f;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target,
                    LookBackRotationSpeed * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        EndBehaviorLock();
    }

    private IEnumerator WagRoutine()
    {
        StartBehaviorLock();

        Quaternion originalRotation = transform.localRotation;
        float elapsed = 0.0f;

        while (elapsed < WagDuration)
        {
            float tilt = Mathf.Sin(elapsed * WagFrequency * Mathf.PI) * WagTiltAngle;
            transform.localRotation = originalRotation * Quaternion.Euler(0.0f, 0.0f, tilt);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localRotation = originalRotation;
        EndBehaviorLock();
    }

    private IEnumerator PullForwardRoutine()
    {
        StartBehaviorLock();
        rovyController.SetMoveSpeed(pullSpeed);

        float elapsed = 0.0f;
        while (elapsed < PullForwardDuration)
        {
            if (playerTransform == null)
            {
                break;
            }

            Vector3 forward = playerTransform.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = transform.forward;
                forward.y = 0.0f;
            }

            Vector3 aheadPoint = playerTransform.position + forward.normalized * PullForwardDistance;

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.SetDestination(aheadPoint);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
        }

        EndBehaviorLock();
    }
}
