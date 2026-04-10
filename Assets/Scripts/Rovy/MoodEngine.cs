using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Represents the weather state used by the mood calculation.
/// </summary>
public enum WeatherType
{
    Sunny,
    Cloudy,
    Rainy
}

/// <summary>
/// Defines the data contract used to read recent walk history.
/// </summary>
public interface ISimulationLogger
{
    /// <summary>
    /// Gets the number of walks recorded during the last seven days.
    /// </summary>
    int WalkCountLastWeek { get; }
}

/// <summary>
/// Defines the data contract used to read the daily name call count.
/// </summary>
public interface IPlayerInteraction
{
    /// <summary>
    /// Gets the number of times the player called Rovy's name during the current day.
    /// </summary>
    int NameCallCountToday { get; }
}

/// <summary>
/// Broadcasts the recalculated mood values in the order Energy, Curiosity, Comfort.
/// </summary>
[Serializable]
public sealed class MoodChangedEvent : UnityEvent<float, float, float>
{
}

/// <summary>
/// Calculates Rovy's mood values from weather, temperature, walk history, and name calls.
/// </summary>
public sealed class MoodEngine : MonoBehaviour
{
    private const float DefaultMoodValue = 0.5f;
    private const float DefaultTemperatureCelsius = 20.0f;
    private const float DefaultWeatherWeight = 0.30f;
    private const float DefaultTemperatureWeight = 0.30f;
    private const float DefaultWalkHistoryWeight = 0.20f;
    private const float DefaultNameCallWeight = 0.20f;
    private const int DefaultWalkHistoryThreshold = 5;
    private const float DefaultComfortTempMin = 15.0f;
    private const float DefaultComfortTempMax = 28.0f;
    private const int NameCallScoreCap = 10;
    private const float SunnyWeatherScore = 1.00f;
    private const float CloudyWeatherScore = 0.60f;
    private const float RainyWeatherScore = 0.25f;
    private const float MoodChangeEpsilon = 0.0001f;

    [Header("Dependencies")]
    [SerializeField] private MoodConfig moodConfig;
    [SerializeField] private MonoBehaviour simulationLoggerSource;
    [SerializeField] private MonoBehaviour playerInteractionSource;

    [Header("Inputs")]
    [SerializeField] private WeatherType weather = WeatherType.Sunny;
    [SerializeField] private float temperature = DefaultTemperatureCelsius;
    [SerializeField] private int walkCountLastWeek;
    [SerializeField] private int nameCallCountToday;

    [Header("Mood Outputs")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float energy = DefaultMoodValue;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float curiosity = DefaultMoodValue;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float comfort = DefaultMoodValue;

    [Header("Events")]
    [SerializeField] private MoodChangedEvent onMoodChanged = new MoodChangedEvent();

    private ISimulationLogger simulationLogger;
    private IPlayerInteraction playerInteraction;

    /// <summary>
    /// Gets the current energy value in the 0-1 range.
    /// </summary>
    public float Energy => energy;

    /// <summary>
    /// Gets the current curiosity value in the 0-1 range.
    /// </summary>
    public float Curiosity => curiosity;

    /// <summary>
    /// Gets the current comfort value in the 0-1 range.
    /// </summary>
    public float Comfort => comfort;

    /// <summary>
    /// Gets the UnityEvent raised whenever at least one mood output changes.
    /// </summary>
    public MoodChangedEvent OnMoodChanged => onMoodChanged;

    private void Awake()
    {
        ResolveDependencies();
        ClampSerializedValues();
        SyncDependencyInputs();
        RecalculateMood(false);
    }

    private void OnValidate()
    {
        ResolveDependencies();
        ClampSerializedValues();
        SyncDependencyInputs();
        RecalculateMood(false);
    }

    /// <summary>
    /// Recalculates all mood values for the current simulation tick.
    /// </summary>
    public void Tick()
    {
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Updates the current weather and recalculates the mood immediately.
    /// </summary>
    /// <param name="nextWeather">The latest weather state.</param>
    public void SetWeather(WeatherType nextWeather)
    {
        if (weather == nextWeather)
        {
            return;
        }

        weather = nextWeather;
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Updates the current temperature and recalculates the mood immediately.
    /// </summary>
    /// <param name="nextTemperature">The latest ambient temperature in Celsius.</param>
    public void SetTemperature(float nextTemperature)
    {
        if (Mathf.Abs(temperature - nextTemperature) <= MoodChangeEpsilon)
        {
            return;
        }

        temperature = nextTemperature;
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Updates the cached walk count and recalculates the mood immediately.
    /// </summary>
    /// <param name="count">The number of walks completed during the last seven days.</param>
    public void SetWalkCountLastWeek(int count)
    {
        walkCountLastWeek = Mathf.Max(0, count);
        RecalculateMood(true);
    }

    /// <summary>
    /// Updates the cached daily name call count and recalculates the mood immediately.
    /// </summary>
    /// <param name="count">The number of name calls recorded for the current day.</param>
    public void SetNameCallCountToday(int count)
    {
        nameCallCountToday = Mathf.Max(0, count);
        RecalculateMood(true);
    }

    /// <summary>
    /// Refreshes cached input data when a new day starts and recalculates the mood.
    /// </summary>
    public void NotifyNewDayStarted()
    {
        if (playerInteraction == null)
        {
            nameCallCountToday = 0;
        }

        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Refreshes cached input data when the player calls Rovy's name and recalculates the mood.
    /// </summary>
    public void NotifyNameCalled()
    {
        if (playerInteraction == null)
        {
            nameCallCountToday += 1;
        }

        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Replaces the active mood config asset and recalculates the mood.
    /// </summary>
    /// <param name="config">The config asset used for all mood weights and clamp values.</param>
    public void SetMoodConfig(MoodConfig config)
    {
        moodConfig = config;
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Assigns the component used to read walk history data.
    /// </summary>
    /// <param name="source">A component implementing <see cref="ISimulationLogger"/>.</param>
    public void SetSimulationLoggerSource(MonoBehaviour source)
    {
        simulationLoggerSource = source;
        ResolveDependencies();
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Assigns the component used to read daily name call data.
    /// </summary>
    /// <param name="source">A component implementing <see cref="IPlayerInteraction"/>.</param>
    public void SetPlayerInteractionSource(MonoBehaviour source)
    {
        playerInteractionSource = source;
        ResolveDependencies();
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    /// <summary>
    /// Forces an immediate mood recalculation from the inspector.
    /// </summary>
    [ContextMenu("Recalculate Mood")]
    public void RecalculateNow()
    {
        SyncDependencyInputs();
        RecalculateMood(true);
    }

    private void ResolveDependencies()
    {
        simulationLogger = simulationLoggerSource as ISimulationLogger;
        playerInteraction = playerInteractionSource as IPlayerInteraction;
    }

    private void SyncDependencyInputs()
    {
        if (simulationLogger != null)
        {
            walkCountLastWeek = Mathf.Max(0, simulationLogger.WalkCountLastWeek);
        }

        if (playerInteraction != null)
        {
            nameCallCountToday = Mathf.Max(0, playerInteraction.NameCallCountToday);
        }
    }

    private void ClampSerializedValues()
    {
        walkCountLastWeek = Mathf.Max(0, walkCountLastWeek);
        nameCallCountToday = Mathf.Max(0, nameCallCountToday);
        energy = Mathf.Clamp01(energy);
        curiosity = Mathf.Clamp01(curiosity);
        comfort = Mathf.Clamp01(comfort);
    }

    private void RecalculateMood(bool notifyListeners)
    {
        ClampSerializedValues();

        float nextEnergy = ClampMood(CalculateEnergy());
        float nextCuriosity = ClampMood(CalculateCuriosity());
        float nextComfort = ClampMood(CalculateComfort());

        bool hasChanged =
            !Approximately(energy, nextEnergy) ||
            !Approximately(curiosity, nextCuriosity) ||
            !Approximately(comfort, nextComfort);

        energy = nextEnergy;
        curiosity = nextCuriosity;
        comfort = nextComfort;

        if (notifyListeners && hasChanged)
        {
            onMoodChanged.Invoke(energy, curiosity, comfort);
        }
    }

    private float CalculateEnergy()
    {
        float weatherScore = GetWeatherScore(weather);
        float temperatureScore = GetTemperatureScore(temperature);
        float weightedInput =
            (weatherScore * GetWeatherWeight()) +
            (temperatureScore * GetTemperatureWeight());

        return Mathf.Lerp(0.0f, 1.0f, weightedInput);
    }

    private float CalculateCuriosity()
    {
        float nameCallScore = GetNameCallScore(nameCallCountToday);
        float historyPenalty = GetHistoryPenalty(walkCountLastWeek);
        float weightedInput =
            (nameCallScore * GetNameCallWeight()) -
            (historyPenalty * GetWalkHistoryWeight());

        return Mathf.Lerp(0.0f, 1.0f, weightedInput);
    }

    private float CalculateComfort()
    {
        float tempComfortScore = GetTemperatureComfortScore(temperature);
        return Mathf.Lerp(0.0f, 1.0f, tempComfortScore);
    }

    private float ClampMood(float value)
    {
        float min = GetMoodMin();
        float max = GetMoodMax(min);
        return Mathf.Clamp(value, min, max);
    }

    private float GetWeatherScore(WeatherType currentWeather)
    {
        switch (currentWeather)
        {
            case WeatherType.Sunny:
                return SunnyWeatherScore;
            case WeatherType.Cloudy:
                return CloudyWeatherScore;
            case WeatherType.Rainy:
                return RainyWeatherScore;
            default:
                return CloudyWeatherScore;
        }
    }

    private float GetTemperatureScore(float currentTemperature)
    {
        return GetTemperatureComfortScore(currentTemperature);
    }

    private float GetTemperatureComfortScore(float currentTemperature)
    {
        float comfortMin = GetComfortTempMin();
        float comfortMax = GetComfortTempMax(comfortMin);

        if (currentTemperature >= comfortMin && currentTemperature <= comfortMax)
        {
            return 1.0f;
        }

        if (currentTemperature < comfortMin)
        {
            float coldRange = Mathf.Max(1.0f, comfortMin);
            return 1.0f - Mathf.Clamp01((comfortMin - currentTemperature) / coldRange);
        }

        float hotRange = Mathf.Max(1.0f, 40.0f - comfortMax);
        return 1.0f - Mathf.Clamp01((currentTemperature - comfortMax) / hotRange);
    }

    private float GetNameCallScore(int count)
    {
        return Mathf.Clamp01(count / (float)NameCallScoreCap);
    }

    private float GetHistoryPenalty(int count)
    {
        int threshold = Mathf.Max(1, GetWalkHistoryThreshold());

        if (count <= threshold)
        {
            return 0.0f;
        }

        return Mathf.Clamp01((count - threshold) / (float)threshold);
    }

    private float GetWeatherWeight()
    {
        return moodConfig != null ? Mathf.Max(0.0f, moodConfig.WeatherWeight) : DefaultWeatherWeight;
    }

    private float GetTemperatureWeight()
    {
        return moodConfig != null ? Mathf.Max(0.0f, moodConfig.TemperatureWeight) : DefaultTemperatureWeight;
    }

    private float GetWalkHistoryWeight()
    {
        return moodConfig != null ? Mathf.Max(0.0f, moodConfig.WalkHistoryWeight) : DefaultWalkHistoryWeight;
    }

    private float GetNameCallWeight()
    {
        return moodConfig != null ? Mathf.Max(0.0f, moodConfig.NameCallWeight) : DefaultNameCallWeight;
    }

    private int GetWalkHistoryThreshold()
    {
        return moodConfig != null ? Mathf.Max(0, moodConfig.WalkHistoryThreshold) : DefaultWalkHistoryThreshold;
    }

    private float GetComfortTempMin()
    {
        return moodConfig != null ? moodConfig.ComfortTempMin : DefaultComfortTempMin;
    }

    private float GetComfortTempMax(float comfortMin)
    {
        float comfortMax = moodConfig != null ? moodConfig.ComfortTempMax : DefaultComfortTempMax;
        return Mathf.Max(comfortMin, comfortMax);
    }

    private float GetMoodMin()
    {
        if (moodConfig == null)
        {
            return 0.0f;
        }

        return Mathf.Clamp01(moodConfig.MoodMin);
    }

    private float GetMoodMax(float min)
    {
        if (moodConfig == null)
        {
            return 1.0f;
        }

        return Mathf.Clamp(moodConfig.MoodMax, min, 1.0f);
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) <= MoodChangeEpsilon;
    }
}
