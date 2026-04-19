using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages the current weather state and ambient temperature.
/// Forwards updated values to <see cref="MoodEngine"/> so that mood is
/// recalculated automatically, and toggles URP post-processing effects
/// (e.g. Vignette) to match the active weather.
/// </summary>
public sealed class WeatherManager : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Inspector Fields
    // ──────────────────────────────────────────────

    [Header("Weather State")]

    /// <summary>
    /// The current weather condition exposed in the Inspector.
    /// </summary>
    [SerializeField]
    private WeatherType currentWeather = WeatherType.Sunny;

    /// <summary>
    /// The current ambient temperature in degrees Celsius.
    /// </summary>
    [SerializeField]
    private float currentTemperature = 20.0f;

    [Header("Dependencies")]

    /// <summary>
    /// Reference to the <see cref="MoodEngine"/> that receives
    /// weather and temperature updates.
    /// </summary>
    [SerializeField]
    private MoodEngine moodEngine;

    [Header("URP Volume")]

    /// <summary>
    /// The URP <see cref="Volume"/> whose profile contains a
    /// <see cref="Vignette"/> override used for rainy-weather visuals.
    /// </summary>
    [SerializeField]
    private Volume skyVolume;

    // Cached Vignette override resolved from the Volume profile.
    private Vignette vignette;

    // ──────────────────────────────────────────────
    //  Unity Lifecycle
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resolves the Vignette override from the Volume profile and
    /// pushes the initial weather / temperature values to the MoodEngine.
    /// </summary>
    private void Start()
    {
        ResolveVignetteOverride();
        ApplyVignetteForWeather(currentWeather);

        if (moodEngine != null)
        {
            moodEngine.SetWeather(currentWeather);
            moodEngine.SetTemperature(currentTemperature);
        }
        else
        {
            Debug.LogWarning(
                $"[{nameof(WeatherManager)}] MoodEngine reference is not assigned.", this);
        }
    }

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Changes the current weather, updates the URP Vignette effect,
    /// and notifies the <see cref="MoodEngine"/>.
    /// </summary>
    /// <param name="weather">The new weather state to apply.</param>
    public void SetWeather(WeatherType weather)
    {
        currentWeather = weather;
        ApplyVignetteForWeather(currentWeather);

        if (moodEngine != null)
        {
            moodEngine.SetWeather(currentWeather);
        }
    }

    /// <summary>
    /// Changes the current ambient temperature and notifies
    /// the <see cref="MoodEngine"/>.
    /// </summary>
    /// <param name="temperature">The new temperature in degrees Celsius.</param>
    public void SetTemperature(float temperature)
    {
        currentTemperature = temperature;

        if (moodEngine != null)
        {
            moodEngine.SetTemperature(currentTemperature);
        }
    }

    // ──────────────────────────────────────────────
    //  URP Volume Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Attempts to resolve a <see cref="Vignette"/> override from the
    /// assigned <see cref="skyVolume"/> profile. Logs a warning if the
    /// override is missing or the Volume is unassigned.
    /// </summary>
    private void ResolveVignetteOverride()
    {
        if (skyVolume == null)
        {
            Debug.LogWarning(
                $"[{nameof(WeatherManager)}] Sky Volume is not assigned. " +
                "Vignette effects will be skipped.", this);
            return;
        }

        VolumeProfile profile = skyVolume.profile;
        if (profile == null || !profile.TryGet(out vignette))
        {
            Debug.LogWarning(
                $"[{nameof(WeatherManager)}] The Volume profile does not " +
                "contain a Vignette override.", this);
        }
    }

    /// <summary>
    /// Enables the Vignette effect when it is rainy and disables it
    /// for sunny weather. Other weather types leave the Vignette unchanged.
    /// </summary>
    /// <param name="weather">The weather state used to decide the effect.</param>
    private void ApplyVignetteForWeather(WeatherType weather)
    {
        if (vignette == null)
        {
            return;
        }

        switch (weather)
        {
            case WeatherType.Rainy:
                vignette.active = true;
                break;

            case WeatherType.Sunny:
                vignette.active = false;
                break;

            // Cloudy and any future types: leave as-is.
            default:
                break;
        }
    }
}
