using UnityEngine;

/// <summary>
/// Drives <see cref="TimeManager"/> at a very high time scale so the full month
/// of simulation can be replayed in seconds for demos or automated testing.
/// </summary>
public sealed class MonthSimulator : MonoBehaviour
{
    [Header("Dependencies")]

    /// <summary>
    /// The TimeManager that will be fast-forwarded.
    /// </summary>
    [SerializeField]
    private TimeManager timeManager;

    /// <summary>
    /// Optional simulation logger used for the end-of-run summary.
    /// </summary>
    [SerializeField]
    private SimulationLogger simulationLogger;

    [Header("Simulation Settings")]

    /// <summary>
    /// Time scale applied to the TimeManager while the fast-forward simulation runs.
    /// </summary>
    [SerializeField]
    [Min(0.0f)]
    private float fastForwardScale = 600.0f;

    /// <summary>
    /// When true, the fast-forward simulation kicks off automatically on Start.
    /// </summary>
    [SerializeField]
    private bool autoStartOnPlay = false;

    [Header("Progress (Read-Only)")]

    /// <summary>
    /// Inspector display string showing current progress, formatted "day X / 30".
    /// </summary>
    [SerializeField]
    private string dayProgressDisplay = "day 0 / 30";

    /// <summary>
    /// Whether the fast-forward simulation is currently active.
    /// </summary>
    [SerializeField]
    private bool isSimulating;

    private float originalTimeScale;
    private bool hasCachedTimeScale;
    private bool isListeningToCompletion;

    /// <summary>
    /// Gets whether the month simulator is currently driving the TimeManager.
    /// </summary>
    public bool IsSimulating => isSimulating;

    /// <summary>
    /// Gets the current "day X / total" display string.
    /// </summary>
    public string DayProgressDisplay => dayProgressDisplay;

    private void Start()
    {
        HookCompletionListener();

        if (autoStartOnPlay)
        {
            StartSimulation();
        }

        RefreshDisplay();
    }

    private void OnDestroy()
    {
        UnhookCompletionListener();
    }

    private void Update()
    {
        RefreshDisplay();
    }

    /// <summary>
    /// Switches the TimeManager to <see cref="fastForwardScale"/> and resumes it so
    /// the remaining simulation days play out rapidly.
    /// </summary>
    public void StartSimulation()
    {
        if (timeManager == null)
        {
            Debug.LogWarning(
                $"[{nameof(MonthSimulator)}] TimeManager reference is not assigned.", this);
            return;
        }

        if (!hasCachedTimeScale)
        {
            originalTimeScale = timeManager.TimeScale;
            hasCachedTimeScale = true;
        }

        HookCompletionListener();
        timeManager.SetTimeScale(fastForwardScale);
        timeManager.Resume();
        isSimulating = true;
        RefreshDisplay();
    }

    /// <summary>
    /// Pauses the TimeManager and restores its original time scale. Safe to call
    /// even when the simulator was never started.
    /// </summary>
    public void StopSimulation()
    {
        if (timeManager == null)
        {
            isSimulating = false;
            return;
        }

        timeManager.Pause();

        if (hasCachedTimeScale)
        {
            timeManager.SetTimeScale(originalTimeScale);
            hasCachedTimeScale = false;
        }

        isSimulating = false;
        RefreshDisplay();
    }

    /// <summary>
    /// Jumps the simulation to the specified day by repeatedly invoking
    /// TimeManager's new-day notifications. Clamped to [0, TotalDays].
    /// </summary>
    /// <param name="day">The target day index.</param>
    public void SetDay(int day)
    {
        if (timeManager == null)
        {
            return;
        }

        int clamped = Mathf.Clamp(day, 0, timeManager.TotalDays);
        int current = timeManager.CurrentDay;

        while (current < clamped)
        {
            current += 1;
            timeManager.OnNewDayStarted.Invoke(current);
        }

        RefreshDisplay();
    }

    private void HookCompletionListener()
    {
        if (isListeningToCompletion || timeManager == null)
        {
            return;
        }

        timeManager.OnSimulationCompleted.AddListener(HandleSimulationCompleted);
        isListeningToCompletion = true;
    }

    private void UnhookCompletionListener()
    {
        if (!isListeningToCompletion || timeManager == null)
        {
            return;
        }

        timeManager.OnSimulationCompleted.RemoveListener(HandleSimulationCompleted);
        isListeningToCompletion = false;
    }

    private void HandleSimulationCompleted()
    {
        int totalWalks = simulationLogger != null ? simulationLogger.WalkCountLastWeek : 0;
        float totalDistance = simulationLogger != null ? simulationLogger.TotalDistanceToday : 0.0f;

        Debug.Log(
            string.Format(
                "[MonthSimulator] Simulation complete. Walks (last 7 days): {0}. Final-day distance: {1:0.0}m.",
                totalWalks,
                totalDistance),
            this);

        if (hasCachedTimeScale && timeManager != null)
        {
            timeManager.SetTimeScale(originalTimeScale);
            hasCachedTimeScale = false;
        }

        isSimulating = false;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (timeManager == null)
        {
            dayProgressDisplay = "day 0 / 0";
            return;
        }

        dayProgressDisplay = string.Format(
            "day {0} / {1}",
            timeManager.CurrentDay,
            timeManager.TotalDays);
    }

    private void OnValidate()
    {
        fastForwardScale = Mathf.Max(0.0f, fastForwardScale);
        RefreshDisplay();
    }
}
