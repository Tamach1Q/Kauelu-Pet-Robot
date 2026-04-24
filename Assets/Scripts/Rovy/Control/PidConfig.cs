using UnityEngine;

namespace Rovy.Control
{
    [CreateAssetMenu(fileName = "PidConfig", menuName = "Rovy/PidConfig")]
    public sealed class PidConfig : ScriptableObject
    {
        [Header("Speed PID (前進速度)")]
        [Range(0f, 5f)] public float speedKp = 1.0f;
        [Range(0f, 2f)] public float speedKi = 0.1f;
        [Range(0f, 2f)] public float speedKd = 0.05f;

        [Header("Steer PID (旋回)")]
        [Range(0f, 5f)] public float steerKp = 1.5f;
        [Range(0f, 2f)] public float steerKi = 0.0f;
        [Range(0f, 2f)] public float steerKd = 0.2f;

        [Header("目標値生成")]
        [Tooltip("張力→目標速度の変換係数. 張力1Nあたりの速度指令")]
        public float tensionToSpeedGain = 0.3f;

        [Tooltip("目標速度の上限 [m/s]")]
        public float maxTargetSpeed = 1.0f;

        [Tooltip("ジョイスティック→目標旋回の変換係数")]
        public float joystickToSteerGain = 1.0f;
    }
}
