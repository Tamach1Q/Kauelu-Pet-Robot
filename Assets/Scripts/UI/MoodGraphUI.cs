using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Draws a real-time scrolling mood graph for Rovy using a uGUI RawImage-backed texture.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class MoodGraphUI : MonoBehaviour
{
    private const int SeriesCount = 7;
    private const float DefaultMoodValue = 0.5f;
    private const float VisibleDurationSeconds = 60.0f;
    private const float IdealTemperatureCelsius = 18.0f;
    private const float TemperatureComfortRange = 18.0f;
    private const int NameCallSoftCap = 10;
    private const float MinGraphHeight = 96.0f;
    private const float GraphPadding = 8.0f;
    private const BindingFlags MemberFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private static readonly string[] LegendLabels =
    {
        "天気",
        "気温",
        "散歩履歴",
        "呼びかけ",
        "エネルギー",
        "好奇心",
        "快適度"
    };

    [Serializable]
    private struct GraphSample
    {
        public float time;
        public float weatherContribution;
        public float temperatureContribution;
        public float walkHistoryContribution;
        public float nameCallContribution;
        public float energy;
        public float curiosity;
        public float comfort;

        public GraphSample(
            float time,
            float weatherContribution,
            float temperatureContribution,
            float walkHistoryContribution,
            float nameCallContribution,
            float energy,
            float curiosity,
            float comfort)
        {
            this.time = time;
            this.weatherContribution = weatherContribution;
            this.temperatureContribution = temperatureContribution;
            this.walkHistoryContribution = walkHistoryContribution;
            this.nameCallContribution = nameCallContribution;
            this.energy = energy;
            this.curiosity = curiosity;
            this.comfort = comfort;
        }

        public float GetValue(int index)
        {
            return index switch
            {
                0 => weatherContribution,
                1 => temperatureContribution,
                2 => walkHistoryContribution,
                3 => nameCallContribution,
                4 => energy,
                5 => curiosity,
                6 => comfort,
                _ => DefaultMoodValue
            };
        }
    }

    private readonly struct MoodInputs
    {
        public readonly float WeatherScore;
        public readonly float TemperatureCelsius;
        public readonly int WalkHistory;
        public readonly int NameCallCount;

        public MoodInputs(float weatherScore, float temperatureCelsius, int walkHistory, int nameCallCount)
        {
            WeatherScore = weatherScore;
            TemperatureCelsius = temperatureCelsius;
            WalkHistory = walkHistory;
            NameCallCount = nameCallCount;
        }
    }

    private readonly struct ConfigValues
    {
        public readonly float WeatherWeight;
        public readonly float TemperatureWeight;
        public readonly float WalkHistoryWeight;
        public readonly float NameCallWeight;
        public readonly int WalkHistoryThreshold;

        public ConfigValues(
            float weatherWeight,
            float temperatureWeight,
            float walkHistoryWeight,
            float nameCallWeight,
            int walkHistoryThreshold)
        {
            WeatherWeight = Mathf.Max(0.0f, weatherWeight);
            TemperatureWeight = Mathf.Max(0.0f, temperatureWeight);
            WalkHistoryWeight = Mathf.Max(0.0f, walkHistoryWeight);
            NameCallWeight = Mathf.Max(0.0f, nameCallWeight);
            WalkHistoryThreshold = Mathf.Max(1, walkHistoryThreshold);
        }
    }

    [Header("Data Source")]
    [SerializeField] private MoodEngine moodEngine;

    [Header("Graph")]
    [SerializeField] private Color[] variableColors =
    {
        new Color(0.29f, 0.69f, 0.95f, 1.0f),
        new Color(0.98f, 0.70f, 0.24f, 1.0f),
        new Color(0.44f, 0.83f, 0.47f, 1.0f),
        new Color(0.96f, 0.43f, 0.67f, 1.0f),
        new Color(0.95f, 0.34f, 0.30f, 1.0f),
        new Color(0.62f, 0.49f, 0.95f, 1.0f),
        new Color(0.36f, 0.91f, 0.87f, 1.0f)
    };
    [SerializeField] private float graphHeight = 150.0f;
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Appearance")]
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.08f, 0.10f, 0.92f);
    [SerializeField] private Color gridColor = new Color(1.0f, 1.0f, 1.0f, 0.10f);
    [SerializeField] private Color centerLineColor = new Color(1.0f, 1.0f, 1.0f, 0.25f);

    [Header("Legend")]
    [SerializeField] private Font legendFont;
    [SerializeField] private int legendFontSize = 12;

    private readonly List<GraphSample> samples = new List<GraphSample>(128);
    private readonly List<Text> legendEntries = new List<Text>(SeriesCount);

    private RawImage rawImage;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform legendRoot;
    private GridLayoutGroup legendGrid;
    private Texture2D graphTexture;
    private Color32[] pixelBuffer;
    private float nextUpdateTime;
    private bool isVisible = true;

    /// <summary>
    /// Gets the current mood engine reference used to sample graph data.
    /// </summary>
    public MoodEngine MoodEngine => moodEngine;

    private void Awake()
    {
        ResolveReferences();
        ClampSerializedFields();
        //ApplyGraphHeight();
        EnsureCanvasGroup();
        EnsureLegend();
        RecreateTextureIfNeeded();
    }

    private void Start()
    {
        SampleAndRedraw(Time.unscaledTime);
    }

    private void Update()
    {
        if (WasToggleKeyPressedThisFrame())
        {
            TogglePanel();
        }

        float now = Time.unscaledTime;

        if (now < nextUpdateTime)
        {
            return;
        }

        SampleAndRedraw(now);
    }

    private static bool WasToggleKeyPressedThisFrame()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.tabKey.wasPressedThisFrame;
    }

    private void OnValidate()
    {
        ClampSerializedFields();

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        ApplyGraphHeight();
    }

    private void OnDestroy()
    {
        ReleaseTexture();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        ApplyGraphHeight();
        UpdateLegendLayout();
        RecreateTextureIfNeeded();
    }

    /// <summary>
    /// Assigns a new mood engine at runtime and refreshes the graph immediately.
    /// </summary>
    /// <param name="source">The mood engine to sample.</param>
    public void SetMoodEngine(MoodEngine source)
    {
        moodEngine = source;
        samples.Clear();
        SampleAndRedraw(Time.unscaledTime);
    }

    /// <summary>
    /// Toggles the panel visibility while keeping data sampling active.
    /// </summary>
    public void TogglePanel()
    {
        isVisible = !isVisible;
        ApplyVisibility();
    }

    private void ResolveReferences()
    {
        if (moodEngine == null)
        {
            moodEngine = FindFirstObjectByType<MoodEngine>();
        }

        if (rawImage == null)
        {
            rawImage = GetComponent<RawImage>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (legendFont == null)
        {
            legendFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (rawImage != null)
        {
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
        }
    }

    private void ClampSerializedFields()
    {
        graphHeight = Mathf.Max(MinGraphHeight, graphHeight);
        updateInterval = Mathf.Max(0.1f, updateInterval);
        legendFontSize = Mathf.Clamp(legendFontSize, 10, 24);

        if (variableColors == null || variableColors.Length != SeriesCount)
        {
            Array.Resize(ref variableColors, SeriesCount);
        }

        for (int i = 0; i < variableColors.Length; i++)
        {
            if (variableColors[i].a <= 0.0f)
            {
                variableColors[i] = GetDefaultColor(i);
            }
        }

        if (legendEntries.Count > 0)
        {
            UpdateLegendVisuals();
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = isVisible ? 1.0f : 0.0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    private void ApplyGraphHeight()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, graphHeight);

        LayoutElement layoutElement = GetComponent<LayoutElement>();

        if (layoutElement != null)
        {
            layoutElement.preferredHeight = graphHeight;
            layoutElement.minHeight = graphHeight;
        }
    }

    private void EnsureLegend()
    {
        if (legendRoot == null)
        {
            Transform existingLegend = transform.Find("Legend");

            if (existingLegend != null)
            {
                legendRoot = existingLegend as RectTransform;
            }
        }

        if (legendRoot == null)
        {
            GameObject legendObject = new GameObject("Legend", typeof(RectTransform));
            legendObject.transform.SetParent(transform, false);
            legendRoot = legendObject.GetComponent<RectTransform>();
        }

        legendRoot.anchorMin = new Vector2(0.0f, 1.0f);
        legendRoot.anchorMax = new Vector2(1.0f, 1.0f);
        legendRoot.pivot = new Vector2(0.5f, 1.0f);
        legendRoot.anchoredPosition = Vector2.zero;
        legendRoot.sizeDelta = new Vector2(0.0f, GetLegendHeight());

        if (legendGrid == null)
        {
            legendGrid = legendRoot.GetComponent<GridLayoutGroup>();
        }

        if (legendGrid == null)
        {
            legendGrid = legendRoot.gameObject.AddComponent<GridLayoutGroup>();
        }

        legendGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        legendGrid.constraintCount = 4;
        legendGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        legendGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        legendGrid.childAlignment = TextAnchor.UpperLeft;
        legendGrid.spacing = new Vector2(6.0f, 2.0f);
        legendGrid.padding = new RectOffset(8, 8, 6, 4);

        while (legendRoot.childCount < SeriesCount)
        {
            int index = legendRoot.childCount;
            GameObject labelObject = new GameObject($"LegendItem{index}", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(legendRoot, false);
        }

        for (int i = legendRoot.childCount - 1; i >= SeriesCount; i--)
        {
            DestroyUnityObject(legendRoot.GetChild(i).gameObject);
        }

        legendEntries.Clear();

        for (int i = 0; i < SeriesCount; i++)
        {
            Text label = legendRoot.GetChild(i).GetComponent<Text>();

            if (label == null)
            {
                label = legendRoot.GetChild(i).gameObject.AddComponent<Text>();
            }

            label.raycastTarget = false;
            label.font = legendFont;
            label.fontSize = legendFontSize;
            label.supportRichText = false;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;

            legendEntries.Add(label);
        }

        UpdateLegendLayout();
        UpdateLegendVisuals();
    }

    private void UpdateLegendLayout()
    {
        if (legendGrid == null || rectTransform == null)
        {
            return;
        }

        float availableWidth = Mathf.Max(160.0f, rectTransform.rect.width - legendGrid.padding.left - legendGrid.padding.right);
        float cellWidth = Mathf.Max(56.0f, (availableWidth - (legendGrid.spacing.x * 3.0f)) / 4.0f);
        float cellHeight = Mathf.Max(16.0f, legendFontSize + 4.0f);
        legendGrid.cellSize = new Vector2(cellWidth, cellHeight);
        legendRoot.sizeDelta = new Vector2(0.0f, GetLegendHeight());
    }

    private void UpdateLegendVisuals()
    {
        for (int i = 0; i < legendEntries.Count && i < SeriesCount; i++)
        {
            Text label = legendEntries[i];

            if (label == null)
            {
                continue;
            }

            label.font = legendFont;
            label.fontSize = legendFontSize;
            label.color = variableColors[i];
            label.text = $"■ {LegendLabels[i]}";
        }
    }

    private void SampleAndRedraw(float now)
    {
        ResolveReferences();
        RecreateTextureIfNeeded();

        if (moodEngine != null)
        {
            samples.Add(BuildSample(now));
        }

        TrimSamples(now);
        DrawGraph(now);
        nextUpdateTime = now + updateInterval;
    }

    private GraphSample BuildSample(float timestamp)
    {
        MoodInputs inputs = ResolveInputs();
        ConfigValues configValues = ResolveConfigValues();

        float weatherRaw = GetCenteredScore(inputs.WeatherScore);
        float temperatureRaw = GetCenteredScore(GetTemperatureComfortScore(inputs.TemperatureCelsius));
        float walkHistoryRaw = GetWalkHistoryContribution(inputs.WalkHistory, configValues.WalkHistoryThreshold);
        float nameCallRaw = GetNameCallContribution(inputs.NameCallCount);

        float energyTotal = configValues.WeatherWeight + configValues.TemperatureWeight;
        float curiosityTotal = configValues.NameCallWeight + configValues.WalkHistoryWeight;
        float comfortTotal = configValues.TemperatureWeight + configValues.WalkHistoryWeight;

        float weatherContribution = EffectToLineValue(
            GetOutputEffect(weatherRaw, configValues.WeatherWeight, energyTotal));

        float temperatureContribution = EffectToLineValue(
            (GetOutputEffect(temperatureRaw, configValues.TemperatureWeight, energyTotal) +
             GetOutputEffect(temperatureRaw, configValues.TemperatureWeight, comfortTotal)) * 0.5f);

        float walkHistoryContribution = EffectToLineValue(
            (GetOutputEffect(walkHistoryRaw, configValues.WalkHistoryWeight, curiosityTotal) +
             GetOutputEffect(walkHistoryRaw, configValues.WalkHistoryWeight, comfortTotal)) * 0.5f);

        float nameCallContribution = EffectToLineValue(
            GetOutputEffect(nameCallRaw, configValues.NameCallWeight, curiosityTotal));

        return new GraphSample(
            timestamp,
            weatherContribution,
            temperatureContribution,
            walkHistoryContribution,
            nameCallContribution,
            moodEngine.Energy,
            moodEngine.Curiosity,
            moodEngine.Comfort);
    }

    private MoodInputs ResolveInputs()
    {
        if (moodEngine == null)
        {
            return new MoodInputs(DefaultMoodValue, IdealTemperatureCelsius, 0, 0);
        }

        return new MoodInputs(
            ReadFloatMember(moodEngine, "weatherScore", DefaultMoodValue),
            ReadFloatMember(moodEngine, "temperatureCelsius", IdealTemperatureCelsius),
            ReadIntMember(moodEngine, "walkHistory", 0),
            ReadIntMember(moodEngine, "nameCallCount", 0));
    }

    private ConfigValues ResolveConfigValues()
    {
        if (moodEngine == null)
        {
            return GetFallbackConfigValues(null);
        }

        ScriptableObject configAsset = ReadObjectMember<ScriptableObject>(moodEngine, "moodConfig", null);

        if (configAsset is MoodConfig typedConfig)
        {
            return new ConfigValues(
                typedConfig.WeatherWeight,
                typedConfig.TemperatureWeight,
                typedConfig.WalkHistoryWeight,
                typedConfig.NameCallWeight,
                typedConfig.WalkHistoryThreshold);
        }

        if (configAsset != null)
        {
            return new ConfigValues(
                ReadFloatMember(configAsset, "weatherWeight", ReadFloatMember(moodEngine, "fallbackWeatherWeight", 1.0f)),
                ReadFloatMember(configAsset, "temperatureWeight", ReadFloatMember(moodEngine, "fallbackTemperatureWeight", 1.0f)),
                ReadFloatMember(configAsset, "walkHistoryWeight", ReadFloatMember(moodEngine, "fallbackWalkHistoryWeight", 1.0f)),
                ReadFloatMember(configAsset, "nameCallWeight", ReadFloatMember(moodEngine, "fallbackNameCallWeight", 1.0f)),
                ReadIntMember(configAsset, "walkHistoryThreshold", ReadIntMember(moodEngine, "fallbackWalkHistoryThreshold", 3)));
        }

        return GetFallbackConfigValues(moodEngine);
    }

    private static ConfigValues GetFallbackConfigValues(MoodEngine source)
    {
        if (source == null)
        {
            return new ConfigValues(1.0f, 1.0f, 1.0f, 1.0f, 3);
        }

        return new ConfigValues(
            ReadFloatMember(source, "fallbackWeatherWeight", 1.0f),
            ReadFloatMember(source, "fallbackTemperatureWeight", 1.0f),
            ReadFloatMember(source, "fallbackWalkHistoryWeight", 1.0f),
            ReadFloatMember(source, "fallbackNameCallWeight", 1.0f),
            ReadIntMember(source, "fallbackWalkHistoryThreshold", 3));
    }

    private void TrimSamples(float now)
    {
        float minVisibleTime = now - VisibleDurationSeconds;

        while (samples.Count > 0 && samples[0].time < minVisibleTime)
        {
            samples.RemoveAt(0);
        }
    }

    private void RecreateTextureIfNeeded()
    {
        if (rawImage == null || rectTransform == null)
        {
            return;
        }

        int width = Mathf.Max(256, Mathf.RoundToInt(rectTransform.rect.width));
        int height = Mathf.Max(Mathf.RoundToInt(graphHeight), Mathf.RoundToInt(MinGraphHeight));

        if (graphTexture != null && graphTexture.width == width && graphTexture.height == height)
        {
            return;
        }

        ReleaseTexture();

        graphTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        pixelBuffer = new Color32[width * height];
        rawImage.texture = graphTexture;
        DrawGraph(Time.unscaledTime);
    }

    private void ReleaseTexture()
    {
        if (graphTexture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(graphTexture);
        }
        else
        {
            DestroyImmediate(graphTexture);
        }

        if (rawImage != null)
        {
            rawImage.texture = null;
        }

        graphTexture = null;
        pixelBuffer = null;
    }

    private void DrawGraph(float now)
    {
        if (graphTexture == null || pixelBuffer == null)
        {
            return;
        }

        FillBackground(backgroundColor);

        int width = graphTexture.width;
        int height = graphTexture.height;
        int plotLeft = Mathf.RoundToInt(GraphPadding);
        int plotRight = Mathf.Max(plotLeft + 1, width - Mathf.RoundToInt(GraphPadding) - 1);
        int plotBottom = Mathf.RoundToInt(GraphPadding);
        int plotTop = Mathf.Max(plotBottom + 1, height - Mathf.RoundToInt(GetLegendHeight()) - Mathf.RoundToInt(GraphPadding) - 1);

        DrawGrid(plotLeft, plotRight, plotBottom, plotTop, now);

        for (int seriesIndex = 0; seriesIndex < SeriesCount; seriesIndex++)
        {
            DrawSeries(seriesIndex, plotLeft, plotRight, plotBottom, plotTop, now, variableColors[seriesIndex]);
        }

        graphTexture.SetPixels32(pixelBuffer);
        graphTexture.Apply(false, false);
    }

    private void FillBackground(Color color)
    {
        Color32 color32 = color;

        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = color32;
        }
    }

    private void DrawGrid(int plotLeft, int plotRight, int plotBottom, int plotTop, float now)
    {
        const int HorizontalDivisions = 4;
        const int VerticalSecondsStep = 10;

        for (int i = 0; i <= HorizontalDivisions; i++)
        {
            float normalized = i / (float)HorizontalDivisions;
            int y = Mathf.RoundToInt(Mathf.Lerp(plotBottom, plotTop, normalized));
            Color lineColor = i == HorizontalDivisions / 2 ? centerLineColor : gridColor;
            DrawHorizontalLine(plotLeft, plotRight, y, lineColor);
        }

        float windowStart = now - VisibleDurationSeconds;

        for (int second = VerticalSecondsStep; second < VisibleDurationSeconds; second += VerticalSecondsStep)
        {
            float timestamp = windowStart + second;
            int x = Mathf.RoundToInt(Mathf.Lerp(plotLeft, plotRight, Mathf.InverseLerp(windowStart, now, timestamp)));
            DrawVerticalLine(x, plotBottom, plotTop, gridColor);
        }

        DrawRectangle(plotLeft, plotRight, plotBottom, plotTop, centerLineColor);
    }

    private void DrawSeries(
        int seriesIndex,
        int plotLeft,
        int plotRight,
        int plotBottom,
        int plotTop,
        float now,
        Color color)
    {
        if (samples.Count < 2)
        {
            return;
        }

        float windowStart = now - VisibleDurationSeconds;
        GraphSample previousSample = samples[0];
        Vector2Int previousPoint = GetPlotPoint(previousSample.time, previousSample.GetValue(seriesIndex), windowStart, now, plotLeft, plotRight, plotBottom, plotTop);

        for (int i = 1; i < samples.Count; i++)
        {
            GraphSample currentSample = samples[i];
            Vector2Int currentPoint = GetPlotPoint(currentSample.time, currentSample.GetValue(seriesIndex), windowStart, now, plotLeft, plotRight, plotBottom, plotTop);
            DrawLine(previousPoint.x, previousPoint.y, currentPoint.x, currentPoint.y, color);
            previousPoint = currentPoint;
        }
    }

    private static Vector2Int GetPlotPoint(
        float timestamp,
        float value,
        float windowStart,
        float now,
        int plotLeft,
        int plotRight,
        int plotBottom,
        int plotTop)
    {
        float normalizedX = Mathf.InverseLerp(windowStart, now, timestamp);
        float normalizedY = Mathf.Clamp01(value);

        int x = Mathf.RoundToInt(Mathf.Lerp(plotLeft, plotRight, normalizedX));
        int y = Mathf.RoundToInt(Mathf.Lerp(plotBottom, plotTop, normalizedY));

        return new Vector2Int(x, y);
    }

    private void DrawRectangle(int left, int right, int bottom, int top, Color color)
    {
        DrawHorizontalLine(left, right, bottom, color);
        DrawHorizontalLine(left, right, top, color);
        DrawVerticalLine(left, bottom, top, color);
        DrawVerticalLine(right, bottom, top, color);
    }

    private void DrawHorizontalLine(int xMin, int xMax, int y, Color color)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            SetPixel(x, y, color);
        }
    }

    private void DrawVerticalLine(int x, int yMin, int yMax, Color color)
    {
        for (int y = yMin; y <= yMax; y++)
        {
            SetPixel(x, y, color);
        }
    }

    private void DrawLine(int x0, int y0, int x1, int y1, Color color)
    {
        int deltaX = Mathf.Abs(x1 - x0);
        int deltaY = Mathf.Abs(y1 - y0);
        int stepX = x0 < x1 ? 1 : -1;
        int stepY = y0 < y1 ? 1 : -1;
        int error = deltaX - deltaY;

        while (true)
        {
            SetPixel(x0, y0, color);

            if (x0 == x1 && y0 == y1)
            {
                return;
            }

            int doubledError = error * 2;

            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x0 += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    private void SetPixel(int x, int y, Color color)
    {
        if (graphTexture == null)
        {
            return;
        }

        if (x < 0 || x >= graphTexture.width || y < 0 || y >= graphTexture.height)
        {
            return;
        }

        pixelBuffer[(y * graphTexture.width) + x] = color;
    }

    private static float GetOutputEffect(float rawContribution, float weight, float totalWeight)
    {
        if (totalWeight <= Mathf.Epsilon || weight <= Mathf.Epsilon)
        {
            return 0.0f;
        }

        return Mathf.Clamp(rawContribution * (weight / totalWeight), -1.0f, 1.0f);
    }

    private static float EffectToLineValue(float effect)
    {
        return Mathf.Clamp01(DefaultMoodValue + (effect * DefaultMoodValue));
    }

    private static float GetCenteredScore(float normalizedValue)
    {
        return (Mathf.Clamp01(normalizedValue) * 2.0f) - 1.0f;
    }

    private static float GetTemperatureComfortScore(float temperature)
    {
        float distanceFromIdeal = Mathf.Abs(temperature - IdealTemperatureCelsius);
        return 1.0f - Mathf.Clamp01(distanceFromIdeal / TemperatureComfortRange);
    }

    private static float GetWalkHistoryContribution(int recentWalkHistory, int walkHistoryThreshold)
    {
        int safeThreshold = Mathf.Max(1, walkHistoryThreshold);

        if (recentWalkHistory <= safeThreshold)
        {
            return 1.0f - (recentWalkHistory / (float)safeThreshold);
        }

        float excessRatio = (recentWalkHistory - safeThreshold) / (float)safeThreshold;
        return -Mathf.Clamp01(excessRatio);
    }

    private static float GetNameCallContribution(int recentNameCallCount)
    {
        return Mathf.Clamp01(recentNameCallCount / (float)NameCallSoftCap);
    }

    private static float ReadFloatMember(object target, string memberName, float fallbackValue)
    {
        if (!TryReadMemberValue(target, memberName, out object value))
        {
            return fallbackValue;
        }

        return value switch
        {
            float floatValue => floatValue,
            int intValue => intValue,
            _ => fallbackValue
        };
    }

    private static int ReadIntMember(object target, string memberName, int fallbackValue)
    {
        if (!TryReadMemberValue(target, memberName, out object value))
        {
            return fallbackValue;
        }

        return value switch
        {
            int intValue => intValue,
            float floatValue => Mathf.RoundToInt(floatValue),
            _ => fallbackValue
        };
    }

    private static T ReadObjectMember<T>(object target, string memberName, T fallbackValue)
        where T : class
    {
        if (!TryReadMemberValue(target, memberName, out object value))
        {
            return fallbackValue;
        }

        return value as T ?? fallbackValue;
    }

    private static bool TryReadMemberValue(object target, string memberName, out object value)
    {
        value = null;

        if (target == null)
        {
            return false;
        }

        Type targetType = target.GetType();
        FieldInfo fieldInfo = targetType.GetField(memberName, MemberFlags);

        if (fieldInfo != null)
        {
            value = fieldInfo.GetValue(target);
            return true;
        }

        PropertyInfo propertyInfo = targetType.GetProperty(memberName, MemberFlags);

        if (propertyInfo != null && propertyInfo.CanRead)
        {
            value = propertyInfo.GetValue(target);
            return true;
        }

        return false;
    }

    private float GetLegendHeight()
    {
        float cellHeight = Mathf.Max(16.0f, legendFontSize + 4.0f);
        float topPadding = legendGrid != null ? legendGrid.padding.top : 6.0f;
        float bottomPadding = legendGrid != null ? legendGrid.padding.bottom : 4.0f;
        float verticalSpacing = legendGrid != null ? legendGrid.spacing.y : 2.0f;
        return (cellHeight * 2.0f) + topPadding + bottomPadding + verticalSpacing;
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static Color GetDefaultColor(int index)
    {
        return index switch
        {
            0 => new Color(0.29f, 0.69f, 0.95f, 1.0f),
            1 => new Color(0.98f, 0.70f, 0.24f, 1.0f),
            2 => new Color(0.44f, 0.83f, 0.47f, 1.0f),
            3 => new Color(0.96f, 0.43f, 0.67f, 1.0f),
            4 => new Color(0.95f, 0.34f, 0.30f, 1.0f),
            5 => new Color(0.62f, 0.49f, 0.95f, 1.0f),
            6 => new Color(0.36f, 0.91f, 0.87f, 1.0f),
            _ => Color.white
        };
    }
}
