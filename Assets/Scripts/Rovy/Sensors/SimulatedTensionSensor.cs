using UnityEngine;

namespace Rovy.Sensors
{
    // SpringJoint.currentForce の代わりに位置差からバネモデルで張力を計算する。
    // leashRestLength を超えた分だけ springConstant 倍の力が発生する。
    [DisallowMultipleComponent]
    public sealed class SimulatedTensionSensor : MonoBehaviour, ITensionSensor
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform rovyBody;
        [SerializeField] private float leashRestLength = 2.0f;
        [SerializeField] private float springConstant = 5.0f;
        [SerializeField] private float noiseAmplitude = 0.05f;

        public float GetForwardTension()
        {
            Vector3 force = ComputeForceVector();
            return Vector3.Dot(force, rovyBody.forward)
                   + Random.Range(-noiseAmplitude, noiseAmplitude);
        }

        public float GetLateralTension()
        {
            Vector3 force = ComputeForceVector();
            return Vector3.Dot(force, rovyBody.right)
                   + Random.Range(-noiseAmplitude, noiseAmplitude);
        }

        private Vector3 ComputeForceVector()
        {
            if (player == null || rovyBody == null)
                return Vector3.zero;

            Vector3 toPlayer = player.position - rovyBody.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            float stretch = Mathf.Max(0f, distance - leashRestLength);

            if (stretch <= 0f)
                return Vector3.zero;

            return toPlayer.normalized * (springConstant * stretch);
        }
    }
}
