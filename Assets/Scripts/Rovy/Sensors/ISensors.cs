namespace Rovy.Sensors
{
    public interface ITensionSensor
    {
        // 前後方向の張力 [N]. 正=前方に引かれている
        float GetForwardTension();
        // 横方向の張力 [N]. 正=右に引かれている
        float GetLateralTension();
    }

    public interface IEncoder
    {
        // 累積走行距離 [m]
        float GetDistance();
        // 現在の前進速度 [m/s]. 後退時は負
        float GetForwardVelocity();
        // 現在の旋回角速度 [rad/s]
        float GetAngularVelocity();
    }

    public interface IJoystickInput
    {
        // -1.0 (左) ～ 1.0 (右)
        float GetHorizontal();
        // -1.0 (引き) ～ 1.0 (押し)
        float GetVertical();
    }

    public interface ILiDAR
    {
        // 360度分の距離配列 [m]. 未検出は float.PositiveInfinity
        float[] GetRanges();
        int RayCount { get; }
    }
}
