using UnityEngine;

namespace Rovy.Sensors
{
    // リード（ひも）の角度からユーザーの左右意図を推定し、実機ジョイスティック入力を模擬する。
    [DisallowMultipleComponent]
    public sealed class SimulatedJoystick : MonoBehaviour, IJoystickInput
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform rovyBody;
        [SerializeField] private float deadZoneAngleDeg = 5.0f;
        [SerializeField] private float maxAngleDeg = 45.0f;

        public float GetHorizontal()
        {
            if (player == null || rovyBody == null)
                return 0f;

            Vector3 toPlayer = player.position - rovyBody.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.0001f)
                return 0f;

            // Rovyから見てプレイヤーが右にいれば正
            float signedAngle = Vector3.SignedAngle(rovyBody.forward, -toPlayer, Vector3.up);

            float abs = Mathf.Abs(signedAngle);
            if (abs < deadZoneAngleDeg)
                return 0f;

            float safeRange = Mathf.Max(0.001f, maxAngleDeg - deadZoneAngleDeg);
            float normalized = Mathf.Clamp01((abs - deadZoneAngleDeg) / safeRange);
            return Mathf.Sign(signedAngle) * normalized;
        }

        // 前後方向は張力センサーが担うため常に 0
        public float GetVertical() => 0f;
    }
}
