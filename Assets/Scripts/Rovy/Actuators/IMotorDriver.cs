namespace Rovy.Actuators
{
    public interface IMotorDriver
    {
        // 前進/後退の推力 [-1, 1]
        void SetThrottle(float throttle);
        // 旋回の指令 [-1, 1]. 正=右旋回
        void SetSteer(float steer);
        // 緊急停止
        void Stop();
    }
}
