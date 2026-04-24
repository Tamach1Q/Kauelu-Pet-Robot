using System.Collections.Generic;
using Rovy.Control;
using UnityEngine;
using UnityEngine.UI;

namespace Rovy.Debug
{
    // MoodGraphUI と同じ RawImage + Texture2D パターンで3段グラフを描画する。
    // ① 目標速度 vs 実速度  ② 張力  ③ スロットル出力
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class PidGraphUI : MonoBehaviour
    {
        private const int GraphRows = 3;
        private const float GraphPadding = 4f;

        private static readonly Color ColorTargetSpeed = new Color(0.36f, 0.91f, 0.87f, 1f);
        private static readonly Color ColorActualSpeed = new Color(0.95f, 0.34f, 0.30f, 1f);
        private static readonly Color ColorTension = new Color(0.98f, 0.70f, 0.24f, 1f);
        private static readonly Color ColorThrottle = new Color(0.44f, 0.83f, 0.47f, 1f);
        private static readonly Color BackgroundColor = new Color(0.06f, 0.08f, 0.10f, 0.92f);
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.10f);

        [SerializeField] private LeadFollowController controller;
        [SerializeField] private int maxSamples = 300;
        [SerializeField] private float graphHeight = 200f;

        private readonly Queue<float> targetSpeedHistory = new Queue<float>();
        private readonly Queue<float> actualSpeedHistory = new Queue<float>();
        private readonly Queue<float> tensionHistory = new Queue<float>();
        private readonly Queue<float> throttleHistory = new Queue<float>();

        private RawImage rawImage;
        private RectTransform rectTransform;
        private Texture2D graphTexture;
        private Color32[] pixelBuffer;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            rectTransform = GetComponent<RectTransform>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, graphHeight);
            RecreateTexture();
        }

        private void OnEnable()
        {
            if (controller == null)
                return;
            controller.OnSensorSampled += HandleSensor;
            controller.OnControlCommand += HandleCommand;
        }

        private void OnDisable()
        {
            if (controller == null)
                return;
            controller.OnSensorSampled -= HandleSensor;
            controller.OnControlCommand -= HandleCommand;
        }

        private void OnDestroy()
        {
            DestroyTexture();
        }

        private void OnRectTransformDimensionsChange()
        {
            RecreateTexture();
        }

        private void HandleSensor(SensorSnapshot s)
        {
            Enqueue(actualSpeedHistory, s.forwardVelocity);
            Enqueue(tensionHistory, s.forwardTension);
        }

        private void HandleCommand(ControlCommand c)
        {
            Enqueue(targetSpeedHistory, c.targetSpeed);
            Enqueue(throttleHistory, c.throttle);
            DrawGraph();
        }

        private void Enqueue(Queue<float> q, float v)
        {
            q.Enqueue(v);
            while (q.Count > maxSamples)
                q.Dequeue();
        }

        private void RecreateTexture()
        {
            if (rawImage == null || rectTransform == null)
                return;

            int w = Mathf.Max(256, Mathf.RoundToInt(rectTransform.rect.width));
            int h = Mathf.Max(64, Mathf.RoundToInt(graphHeight));

            if (graphTexture != null && graphTexture.width == w && graphTexture.height == h)
                return;

            DestroyTexture();
            graphTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            pixelBuffer = new Color32[w * h];
            rawImage.texture = graphTexture;
        }

        private void DestroyTexture()
        {
            if (graphTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(graphTexture);
            else
                DestroyImmediate(graphTexture);

            if (rawImage != null)
                rawImage.texture = null;

            graphTexture = null;
            pixelBuffer = null;
        }

        private void DrawGraph()
        {
            if (graphTexture == null || pixelBuffer == null)
                return;

            int w = graphTexture.width;
            int h = graphTexture.height;

            Fill(BackgroundColor);

            int rowHeight = (h - Mathf.RoundToInt(GraphPadding * (GraphRows + 1))) / GraphRows;

            // row 0: 目標速度 vs 実速度 [-1, 1]
            DrawRow(0, rowHeight, w, targetSpeedHistory, ColorTargetSpeed, -1f, 1f);
            DrawRow(0, rowHeight, w, actualSpeedHistory, ColorActualSpeed, -1f, 1f);

            // row 1: 張力 [0, 10]
            DrawRow(1, rowHeight, w, tensionHistory, ColorTension, 0f, 10f);

            // row 2: スロットル [-1, 1]
            DrawRow(2, rowHeight, w, throttleHistory, ColorThrottle, -1f, 1f);

            graphTexture.SetPixels32(pixelBuffer);
            graphTexture.Apply(false, false);
        }

        private void DrawRow(
            int rowIndex, int rowHeight, int width,
            Queue<float> data, Color color,
            float valueMin, float valueMax)
        {
            int pad = Mathf.RoundToInt(GraphPadding);
            int bottom = pad + rowIndex * (rowHeight + pad);
            int top = bottom + rowHeight;

            DrawHLine(pad, width - pad - 1, bottom, GridColor);
            DrawHLine(pad, width - pad - 1, top, GridColor);

            if (data.Count < 2)
                return;

            float[] arr = data.ToArray();
            int plotWidth = width - pad * 2;

            int prevX = pad;
            int prevY = ValueToY(arr[0], bottom, top, valueMin, valueMax);

            for (int i = 1; i < arr.Length; i++)
            {
                int x = pad + Mathf.RoundToInt(i / (float)(maxSamples - 1) * plotWidth);
                int y = ValueToY(arr[i], bottom, top, valueMin, valueMax);
                DrawLine(prevX, prevY, x, y, color);
                prevX = x;
                prevY = y;
            }
        }

        private static int ValueToY(float v, int bottom, int top, float vMin, float vMax)
        {
            float n = Mathf.InverseLerp(vMin, vMax, v);
            return Mathf.RoundToInt(Mathf.Lerp(bottom, top, n));
        }

        private void Fill(Color c)
        {
            Color32 c32 = c;
            for (int i = 0; i < pixelBuffer.Length; i++)
                pixelBuffer[i] = c32;
        }

        private void DrawHLine(int x0, int x1, int y, Color c)
        {
            for (int x = x0; x <= x1; x++)
                SetPixel(x, y, c);
        }

        private void DrawLine(int x0, int y0, int x1, int y1, Color c)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetPixel(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private void SetPixel(int x, int y, Color c)
        {
            if (graphTexture == null) return;
            if (x < 0 || x >= graphTexture.width || y < 0 || y >= graphTexture.height) return;
            pixelBuffer[y * graphTexture.width + x] = c;
        }
    }
}
