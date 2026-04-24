using UnityEngine;

namespace Rovy.Sensors
{
    public interface ISensorHub
    {
        ITensionSensor Tension { get; }
        IEncoder Encoder { get; }
        IJoystickInput Joystick { get; }
        ILiDAR LiDAR { get; }
    }

    [DisallowMultipleComponent]
    public sealed class SensorHub : MonoBehaviour, ISensorHub
    {
        [SerializeField] private MonoBehaviour tensionSource;
        [SerializeField] private MonoBehaviour encoderSource;
        [SerializeField] private MonoBehaviour joystickSource;
        [SerializeField] private MonoBehaviour lidarSource;

        public ITensionSensor Tension => (ITensionSensor)tensionSource;
        public IEncoder Encoder => (IEncoder)encoderSource;
        public IJoystickInput Joystick => (IJoystickInput)joystickSource;
        public ILiDAR LiDAR => (ILiDAR)lidarSource;
    }
}
