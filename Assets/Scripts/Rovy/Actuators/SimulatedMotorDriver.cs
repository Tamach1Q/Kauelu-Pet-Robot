using UnityEngine;

namespace Rovy.Actuators
{
    [DisallowMultipleComponent]
    public sealed class SimulatedMotorDriver : MonoBehaviour, IMotorDriver
    {
        [SerializeField] private Rigidbody rovyRigidbody;
        [SerializeField] private Transform rovyBody;
        [SerializeField] private float maxForce = 10.0f;
        [SerializeField] private float maxTorque = 3.0f;

        private float currentThrottle;
        private float currentSteer;

        public void SetThrottle(float throttle)
        {
            currentThrottle = Mathf.Clamp(throttle, -1f, 1f);
        }

        public void SetSteer(float steer)
        {
            currentSteer = Mathf.Clamp(steer, -1f, 1f);
        }

        public void Stop()
        {
            currentThrottle = 0f;
            currentSteer = 0f;

            if (rovyRigidbody == null)
                return;

            rovyRigidbody.linearVelocity = Vector3.zero;
            rovyRigidbody.angularVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (rovyRigidbody == null || rovyBody == null)
                return;

            rovyRigidbody.AddForce(rovyBody.forward * (currentThrottle * maxForce));
            rovyRigidbody.AddTorque(Vector3.up * (currentSteer * maxTorque));
        }
    }
}
