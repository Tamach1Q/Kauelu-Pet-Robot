using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records Rovy's daily walk statistics and exposes recent walk history to
/// <see cref="MoodEngine"/> through the <see cref="ISimulationLogger"/> contract.
/// </summary>
public sealed class SimulationLogger : MonoBehaviour, ISimulationLogger
{
    private const int HistoryWindowDays = 7;

    [Header("Dependencies")]

    /// <summary>
    /// The player transform whose frame-to-frame displacement is accumulated
    /// as the daily walked distance.
    /// </summary>
    [SerializeField]
    private Transform playerTransform;

    /// <summary>
    /// MoodEngine that will be wired up as a simulation logger source at startup.
    /// </summary>
    [SerializeField]
    private MoodEngine moodEngine;

    [Header("Walk Definition")]

    /// <summary>
    /// Minimum distance (meters) that must be travelled in a single day for it
    /// to count as a completed walk.
    /// </summary>
    [SerializeField]
    [Min(0.0f)]
    private float minimumWalkDistance = 100.0f;

    [Header("Today")]

    /// <summary>
    /// Total distance (meters) travelled by the player since the current day started.
    /// </summary>
    [SerializeField]
    private float totalDistanceToday;

    /// <summary>
    /// Total elapsed real time (seconds) since the current day started.
    /// </summary>
    [SerializeField]
    private float totalTimeToday;

    [Header("History (Read-Only)")]

    /// <summary>
    /// Rolling history of daily walked distances for the last seven days.
    /// Index 0 is the most recent completed day.
    /// </summary>
    [SerializeField]
    private List<float> lastSevenDaysDistances = new List<float>();

    /// <summary>
    /// Number of days in <see cref="lastSevenDaysDistances"/> that meet or exceed
    /// <see cref="minimumWalkDistance"/>.
    /// </summary>
    [SerializeField]
    private int walkCountLastWeek;

    private Vector3 lastPlayerPosition;
    private bool hasLastPosition;

    /// <summary>
    /// Gets the number of walks recorded during the last seven days.
    /// </summary>
    public int WalkCountLastWeek => walkCountLastWeek;

    /// <summary>
    /// Gets the distance (meters) accumulated for the current day.
    /// </summary>
    public float TotalDistanceToday => totalDistanceToday;

    /// <summary>
    /// Gets the elapsed real time (seconds) for the current day.
    /// </summary>
    public float TotalTimeToday => totalTimeToday;

    private void Start()
    {
        if (moodEngine != null)
        {
            moodEngine.SetSimulationLoggerSource(this);
        }

        CachePlayerPosition();
    }

    private void Update()
    {
        totalTimeToday += Time.deltaTime;

        if (playerTransform == null)
        {
            hasLastPosition = false;
            return;
        }

        Vector3 currentPosition = playerTransform.position;

        if (!hasLastPosition)
        {
            lastPlayerPosition = currentPosition;
            hasLastPosition = true;
            return;
        }

        float delta = Vector3.Distance(currentPosition, lastPlayerPosition);
        totalDistanceToday += delta;
        lastPlayerPosition = currentPosition;
    }

    /// <summary>
    /// Handler for <see cref="TimeManager.OnNewDayStarted"/>. Saves the completed
    /// day's record, resets the daily counters, and recomputes
    /// <see cref="WalkCountLastWeek"/> from the rolling seven-day history.
    /// </summary>
    /// <param name="day">The index of the day that just started.</param>
    public void OnNewDayStarted(int day)
    {
        PushDailyRecord(totalDistanceToday);
        RecalculateWalkCountLastWeek();
        ResetDailyCounters();
    }

    /// <summary>
    /// Manually resets the rolling seven-day history and counters. Primarily used
    /// for debugging and editor tooling.
    /// </summary>
    [ContextMenu("Reset History")]
    public void ResetHistory()
    {
        lastSevenDaysDistances.Clear();
        walkCountLastWeek = 0;
        ResetDailyCounters();
    }

    private void PushDailyRecord(float distance)
    {
        lastSevenDaysDistances.Insert(0, distance);

        while (lastSevenDaysDistances.Count > HistoryWindowDays)
        {
            lastSevenDaysDistances.RemoveAt(lastSevenDaysDistances.Count - 1);
        }
    }

    private void RecalculateWalkCountLastWeek()
    {
        int count = 0;

        for (int i = 0; i < lastSevenDaysDistances.Count; i++)
        {
            if (lastSevenDaysDistances[i] >= minimumWalkDistance)
            {
                count += 1;
            }
        }

        walkCountLastWeek = count;
    }

    private void ResetDailyCounters()
    {
        totalDistanceToday = 0.0f;
        totalTimeToday = 0.0f;
        CachePlayerPosition();
    }

    private void CachePlayerPosition()
    {
        if (playerTransform == null)
        {
            hasLastPosition = false;
            return;
        }

        lastPlayerPosition = playerTransform.position;
        hasLastPosition = true;
    }

    private void OnValidate()
    {
        minimumWalkDistance = Mathf.Max(0.0f, minimumWalkDistance);
        totalDistanceToday = Mathf.Max(0.0f, totalDistanceToday);
        totalTimeToday = Mathf.Max(0.0f, totalTimeToday);
        walkCountLastWeek = Mathf.Clamp(walkCountLastWeek, 0, HistoryWindowDays);
    }
}
