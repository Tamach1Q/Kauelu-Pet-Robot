using Rovy.Control;
using UnityEngine;
using UnityEngine.UI;

namespace Rovy.Debug
{
    // インスペクターに頼らず実行時に PID パラメータを調整できるパネル。
    // Canvas 配下の uGUI スライダーを自動検索して PidConfig に双方向バインドする。
    [DisallowMultipleComponent]
    public sealed class PidDebugPanel : MonoBehaviour
    {
        [SerializeField] private PidConfig config;
        [SerializeField] private LeadFollowController controller;
        [SerializeField] private PidLogger logger;

        [Header("Speed PID Sliders")]
        [SerializeField] private Slider speedKpSlider;
        [SerializeField] private Slider speedKiSlider;
        [SerializeField] private Slider speedKdSlider;

        [Header("Steer PID Sliders")]
        [SerializeField] private Slider steerKpSlider;
        [SerializeField] private Slider steerKiSlider;
        [SerializeField] private Slider steerKdSlider;

        [Header("Gain Sliders")]
        [SerializeField] private Slider tensionToSpeedGainSlider;
        [SerializeField] private Slider joystickToSteerGainSlider;

        private void Start()
        {
            if (config == null)
                return;

            InitSlider(speedKpSlider, 0f, 5f, config.speedKp, v => config.speedKp = v);
            InitSlider(speedKiSlider, 0f, 2f, config.speedKi, v => config.speedKi = v);
            InitSlider(speedKdSlider, 0f, 2f, config.speedKd, v => config.speedKd = v);
            InitSlider(steerKpSlider, 0f, 5f, config.steerKp, v => config.steerKp = v);
            InitSlider(steerKiSlider, 0f, 2f, config.steerKi, v => config.steerKi = v);
            InitSlider(steerKdSlider, 0f, 2f, config.steerKd, v => config.steerKd = v);
            InitSlider(tensionToSpeedGainSlider, 0f, 2f, config.tensionToSpeedGain, v => config.tensionToSpeedGain = v);
            InitSlider(joystickToSteerGainSlider, 0f, 3f, config.joystickToSteerGain, v => config.joystickToSteerGain = v);
        }

        // PID リセット（インスペクターの ContextMenu または UI Button から呼ぶ）
        public void ResetPid()
        {
            if (controller != null)
            {
                controller.enabled = false;
                controller.enabled = true;
            }
        }

        // ロガーの有効/無効トグル
        public void ToggleLogger()
        {
            if (logger != null)
                logger.enabled = !logger.enabled;
        }

        private static void InitSlider(Slider slider, float min, float max, float initial, System.Action<float> onChanged)
        {
            if (slider == null)
                return;

            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(v => onChanged(v));
        }
    }
}
