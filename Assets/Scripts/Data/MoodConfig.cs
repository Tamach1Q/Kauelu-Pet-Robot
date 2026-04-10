using UnityEngine;

[CreateAssetMenu(menuName = "Rovy/MoodConfig")]
public sealed class MoodConfig : ScriptableObject
{
    [Header("Mood Variable Weights")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Controls how strongly the current weather affects Rovy's mood.")]
    private float weatherWeight = 0.30f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Controls how strongly the current temperature affects Rovy's mood.")]
    private float temperatureWeight = 0.30f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Controls how strongly recent walk frequency affects Rovy's mood.")]
    private float walkHistoryWeight = 0.20f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Controls how strongly the owner's name calls affect Rovy's mood.")]
    private float nameCallWeight = 0.20f;

    [Header("Thresholds")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Walks per week above this value start causing a negative mood effect.")]
    private int walkHistoryThreshold = 5;

    [SerializeField]
    [Tooltip("Minimum comfortable temperature in Celsius. Values below this reduce comfort.")]
    private float comfortTempMin = 15f;

    [SerializeField]
    [Tooltip("Maximum comfortable temperature in Celsius. Values above this reduce comfort.")]
    private float comfortTempMax = 28f;

    [Header("Mood Output Clamp")]
    [SerializeField]
    [Tooltip("Minimum allowed mood value after calculation.")]
    private float moodMin = 0f;

    [SerializeField]
    [Tooltip("Maximum allowed mood value after calculation.")]
    private float moodMax = 1f;

    public float WeatherWeight => weatherWeight;

    public float TemperatureWeight => temperatureWeight;

    public float WalkHistoryWeight => walkHistoryWeight;

    public float NameCallWeight => nameCallWeight;

    public int WalkHistoryThreshold => walkHistoryThreshold;

    public float ComfortTempMin => comfortTempMin;

    public float ComfortTempMax => comfortTempMax;

    public float MoodMin => moodMin;

    public float MoodMax => moodMax;

    private void OnValidate()
    {
        weatherWeight = Mathf.Clamp01(weatherWeight);
        temperatureWeight = Mathf.Clamp01(temperatureWeight);
        walkHistoryWeight = Mathf.Clamp01(walkHistoryWeight);
        nameCallWeight = Mathf.Clamp01(nameCallWeight);

        walkHistoryThreshold = Mathf.Max(0, walkHistoryThreshold);
        comfortTempMax = Mathf.Max(comfortTempMin, comfortTempMax);
        moodMax = Mathf.Max(moodMin, moodMax);
    }
}
