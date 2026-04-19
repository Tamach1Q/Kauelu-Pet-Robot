using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Raised when the simulation enters a new day. The payload is the day index.
/// </summary>
[Serializable]
public sealed class NewDayStartedEvent : UnityEvent<int>
{
}

/// <summary>
/// Raised when the displayed hour changes. The payload is the current hour (0-24).
/// </summary>
[Serializable]
public sealed class HourChangedEvent : UnityEvent<float>
{
}

/// <summary>
/// Drives simulation time, the day/night cycle, and notifies dependent systems
/// whenever the hour or day changes. One real second advances the simulation clock
/// by <see cref="timeScale"/> simulation seconds.
/// </summary>
public sealed class TimeManager : MonoBehaviour
{
    private const float HoursPerDay = 24.0f;
    private const float SecondsPerHour = 3600.0f;
    private const float HourChangeEpsilon = 0.0001f;

    [Header("Time Scale")]

    /// <summary>
    /// Number of simulation seconds that elapse per real-time second.
    /// Default: 60 (one real second equals one simulation minute).
    /// </summary>
    [SerializeField]
    [Min(0.0f)]
    private float timeScale = 60.0f;

    [Header("Clock State")]

    /// <summary>
    /// The current in-simulation hour in the range [0, 24).
    /// </summary>
    [SerializeField]
    [Range(0.0f, 24.0f)]
    private float currentHour = 7.0f;

    /// <summary>
    /// The current simulation day index (0-based).
    /// </summary>
    [SerializeField]
    [Min(0)]
    private int currentDay = 0;

    /// <summary>
    /// The total number of days the simulation will run for.
    /// </summary>
    [SerializeField]
    [Min(1)]
    private int totalDays = 30;

    /// <summary>
    /// Whether the simulation clock is advancing each frame.
    /// </summary>
    [SerializeField]
    private bool isRunning = true;

    [Header("Dependencies")]

    /// <summary>
    /// MoodEngine that will be notified when a new day starts.
    /// </summary>
    [SerializeField]
    private MoodEngine moodEngine;

    /// <summary>
    /// RouteMemory that will receive the updated simulation day.
    /// </summary>
    [SerializeField]
    private RouteMemory routeMemory;

    /// <summary>
    /// WeatherManager that will receive a random weather/temperature roll each new day.
    /// </summary>
    [SerializeField]
    private WeatherManager weatherManager;

    [Header("Daily Weather Range")]

    /// <summary>
    /// Minimum temperature (Celsius) that can be rolled for a new day.
    /// </summary>
    [SerializeField]
    private float minDailyTemperature = 5.0f;

    /// <summary>
    /// Maximum temperature (Celsius) that can be rolled for a new day.
    /// </summary>
    [SerializeField]
    private float maxDailyTemperature = 32.0f;

    [Header("Events")]

    /// <summary>
    /// Invoked whenever the day index advances. Payload: new day index.
    /// </summary>
    [SerializeField]
    private NewDayStartedEvent onNewDayStarted = new NewDayStartedEvent();

    /// <summary>
    /// Invoked once all <see cref="totalDays"/> have elapsed.
    /// </summary>
    [SerializeField]
    private UnityEvent onSimulationCompleted = new UnityEvent();

    /// <summary>
    /// Invoked when the hour value changes appreciably. Payload: current hour.
    /// </summary>
    [SerializeField]
    private HourChangedEvent onHourChanged = new HourChangedEvent();

    private float lastBroadcastHour;

    /// <summary>
    /// Gets the current simulation hour in the range [0, 24).
    /// </summary>
    public float CurrentHour => currentHour;

    /// <summary>
    /// Gets the current simulation day index (0-based).
    /// </summary>
    public int CurrentDay => currentDay;

    /// <summary>
    /// Gets whether the simulation clock is currently running.
    /// </summary>
    public bool IsRunning => isRunning;

    /// <summary>
    /// Gets the configured total number of days.
    /// </summary>
    public int TotalDays => totalDays;

    /// <summary>
    /// Gets the effective simulation-seconds-per-real-second scale.
    /// </summary>
    public float TimeScale => timeScale;

    /// <summary>
    /// Event raised when a new day begins.
    /// </summary>
    public NewDayStartedEvent OnNewDayStarted => onNewDayStarted;

    /// <summary>
    /// Event raised when the simulation has completed all scheduled days.
    /// </summary>
    public UnityEvent OnSimulationCompleted => onSimulationCompleted;

    /// <summary>
    /// Event raised when the hour changes.
    /// </summary>
    public HourChangedEvent OnHourChanged => onHourChanged;

    private void Start()
    {
        lastBroadcastHour = currentHour;
        NotifyDayStarted(currentDay);
        onHourChanged.Invoke(currentHour);
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        float deltaSimSeconds = Time.deltaTime * timeScale;
        float deltaHours = deltaSimSeconds / SecondsPerHour;

        AdvanceTime(deltaHours);
    }

    /// <summary>
    /// Pauses the simulation clock. Update ticks stop advancing time until
    /// <see cref="Resume"/> is called.
    /// </summary>
    public void Pause()
    {
        isRunning = false;
    }

    /// <summary>
    /// Resumes the simulation clock.
    /// </summary>
    public void Resume()
    {
        isRunning = true;
    }

    /// <summary>
    /// Updates the simulation time scale.
    /// </summary>
    /// <param name="scale">Simulation seconds elapsed per real-time second. Negative values are clamped to zero.</param>
    public void SetTimeScale(float scale)
    {
        timeScale = Mathf.Max(0.0f, scale);
    }

    private void AdvanceTime(float deltaHours)
    {
        if (deltaHours <= 0.0f)
        {
            return;
        }

        currentHour += deltaHours;

        while (currentHour >= HoursPerDay)
        {
            currentHour -= HoursPerDay;
            AdvanceDay();

            if (!isRunning)
            {
                return;
            }
        }

        if (Mathf.Abs(currentHour - lastBroadcastHour) > HourChangeEpsilon)
        {
            lastBroadcastHour = currentHour;
            onHourChanged.Invoke(currentHour);
        }
    }

    private void AdvanceDay()
    {
        currentDay += 1;

        if (currentDay >= totalDays)
        {
            currentDay = totalDays;
            isRunning = false;
            onSimulationCompleted.Invoke();
            return;
        }

        NotifyDayStarted(currentDay);
    }

    private void NotifyDayStarted(int day)
    {
        if (moodEngine != null)
        {
            moodEngine.NotifyNewDayStarted();
        }

        if (routeMemory != null)
        {
            routeMemory.SetSimulationDay(day);
        }

        RollDailyWeather();

        onNewDayStarted.Invoke(day);
    }

    private void RollDailyWeather()
    {
        if (weatherManager == null)
        {
            return;
        }

        WeatherType rolledWeather = (WeatherType)UnityEngine.Random.Range(0, 3);

        float minTemperature = Mathf.Min(minDailyTemperature, maxDailyTemperature);
        float maxTemperature = Mathf.Max(minDailyTemperature, maxDailyTemperature);
        float rolledTemperature = UnityEngine.Random.Range(minTemperature, maxTemperature);

        weatherManager.SetWeather(rolledWeather);
        weatherManager.SetTemperature(rolledTemperature);
    }

    private void OnValidate()
    {
        timeScale = Mathf.Max(0.0f, timeScale);
        currentHour = Mathf.Clamp(currentHour, 0.0f, HoursPerDay);

        if (currentHour >= HoursPerDay)
        {
            currentHour = 0.0f;
        }

        currentDay = Mathf.Max(0, currentDay);
        totalDays = Mathf.Max(1, totalDays);
        currentDay = Mathf.Min(currentDay, totalDays);
    }
}
