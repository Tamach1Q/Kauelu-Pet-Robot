using UnityEngine;

namespace Rovy.Control
{
    // 汎用PID制御器。MonoBehaviour ではないため単体テストが容易。
    public sealed class PidController
    {
        private float integral;
        private float previousError;
        private bool hasPrevious;

        public float Update(
            float setpoint, float measured,
            float kp, float ki, float kd,
            float dt,
            float outputMin, float outputMax)
        {
            float error = setpoint - measured;
            integral += error * dt;

            float derivative = hasPrevious ? (error - previousError) / dt : 0f;
            previousError = error;
            hasPrevious = true;

            float raw = kp * error + ki * integral + kd * derivative;
            float clamped = Mathf.Clamp(raw, outputMin, outputMax);

            // アンチワインドアップ: 飽和時は積分の増加分を戻す
            if (!Mathf.Approximately(clamped, raw))
                integral -= error * dt;

            return clamped;
        }

        public void Reset()
        {
            integral = 0f;
            previousError = 0f;
            hasPrevious = false;
        }
    }
}
