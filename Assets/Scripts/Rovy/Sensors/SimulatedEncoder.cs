using UnityEngine;

namespace Rovy.Sensors
{
    // 車輪エンコーダーの代替: Rigidbody の速度・位置から距離・速度を算出する。
    [DisallowMultipleComponent]
    public sealed class SimulatedEncoder : MonoBehaviour, IEncoder
    {
        [SerializeField] private Rigidbody rovyRigidbody;
        [SerializeField] private Transform rovyBody;

        private float accumulatedDistance;
        private Vector3 lastPosition;

        private void Awake()
        {
            lastPosition = rovyRigidbody != null ? rovyRigidbody.position : transform.position;
        }

        private void FixedUpdate()
        {
            if (rovyRigidbody == null)
                return;

            float delta = Vector3.Distance(rovyRigidbody.position, lastPosition);
            accumulatedDistance += delta;
            lastPosition = rovyRigidbody.position;
        }

        public float GetDistance() => accumulatedDistance;

        public float GetForwardVelocity()
        {
            if (rovyRigidbody == null || rovyBody == null)
                return 0f;
            return Vector3.Dot(rovyRigidbody.linearVelocity, rovyBody.forward);
        }

        public float GetAngularVelocity()
        {
            if (rovyRigidbody == null)
                return 0f;
            return rovyRigidbody.angularVelocity.y;
        }
    }
}
