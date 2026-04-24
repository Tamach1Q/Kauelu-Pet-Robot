using System;
using Rovy.Actuators;
using Rovy.Sensors;
using UnityEngine;

namespace Rovy.Control
{
    public struct SensorSnapshot
    {
        public float time;
        public float forwardTension;
        public float lateralTension;
        public float forwardVelocity;
        public float angularVelocity;
        public float joystickHorizontal;
    }

    public struct ControlCommand
    {
        public float time;
        public float targetSpeed;
        public float targetSteer;
        public float throttle;
        public float steer;
    }

    // 仮想RaspberryPiに相当する制御本体。
    // NavMeshAgent と排他的に動作し、有効時は Rigidbody 駆動に切り替える。
    [DisallowMultipleComponent]
    public sealed class LeadFollowController : MonoBehaviour
    {
        [SerializeField] private SensorHub sensorHub;
        [SerializeField] private SimulatedMotorDriver motorDriver;
        [SerializeField] private PidConfig config;

        private PidController speedPid;
        private PidController steerPid;

        public event Action<SensorSnapshot> OnSensorSampled;
        public event Action<ControlCommand> OnControlCommand;

        private void Awake()
        {
            speedPid = new PidController();
            steerPid = new PidController();
        }

        private void FixedUpdate()
        {
            if (sensorHub == null || motorDriver == null || config == null)
                return;

            // 1. センサー読み取り
            var snapshot = new SensorSnapshot
            {
                time = Time.time,
                forwardTension = sensorHub.Tension.GetForwardTension(),
                lateralTension = sensorHub.Tension.GetLateralTension(),
                forwardVelocity = sensorHub.Encoder.GetForwardVelocity(),
                angularVelocity = sensorHub.Encoder.GetAngularVelocity(),
                joystickHorizontal = sensorHub.Joystick.GetHorizontal(),
            };
            OnSensorSampled?.Invoke(snapshot);

            // 2. 目標値生成
            float targetSpeed = Mathf.Clamp(
                snapshot.forwardTension * config.tensionToSpeedGain,
                -config.maxTargetSpeed,
                config.maxTargetSpeed);
            float targetSteer = snapshot.joystickHorizontal * config.joystickToSteerGain;

            // 3. PID計算
            float throttleCmd = speedPid.Update(
                targetSpeed, snapshot.forwardVelocity,
                config.speedKp, config.speedKi, config.speedKd,
                Time.fixedDeltaTime, -1f, 1f);

            float steerCmd = steerPid.Update(
                targetSteer, snapshot.angularVelocity,
                config.steerKp, config.steerKi, config.steerKd,
                Time.fixedDeltaTime, -1f, 1f);

            // 4. モーター指令
            motorDriver.SetThrottle(throttleCmd);
            motorDriver.SetSteer(steerCmd);

            OnControlCommand?.Invoke(new ControlCommand
            {
                time = Time.time,
                targetSpeed = targetSpeed,
                targetSteer = targetSteer,
                throttle = throttleCmd,
                steer = steerCmd,
            });
        }

        private void OnDisable()
        {
            motorDriver?.Stop();
            speedPid?.Reset();
            steerPid?.Reset();
        }
    }
}
