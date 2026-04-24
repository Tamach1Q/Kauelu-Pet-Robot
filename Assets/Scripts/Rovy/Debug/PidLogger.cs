using System;
using System.IO;
using Rovy.Control;
using UnityEngine;

namespace Rovy.Debug
{
    // FixedUpdate 毎のセンサー値・制御出力を CSV に書き出す。
    // 出力先: <プロジェクトルート>/Logs/pid_yyyyMMdd_HHmmss.csv
    [DisallowMultipleComponent]
    public sealed class PidLogger : MonoBehaviour
    {
        [SerializeField] private LeadFollowController controller;
        [SerializeField] private string outputDirectory = "Logs";

        private StreamWriter writer;
        private SensorSnapshot latestSensor;

        private void OnEnable()
        {
            if (controller == null)
                return;

            string dir = Path.Combine(Application.dataPath, "..", outputDirectory);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"pid_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            writer = new StreamWriter(path);
            writer.WriteLine("time,fwd_tension,lat_tension,fwd_vel,ang_vel,joy_h,tgt_speed,tgt_steer,throttle,steer");

            controller.OnSensorSampled += HandleSensor;
            controller.OnControlCommand += HandleCommand;
        }

        private void HandleSensor(SensorSnapshot s)
        {
            latestSensor = s;
        }

        private void HandleCommand(ControlCommand c)
        {
            if (writer == null)
                return;

            writer.WriteLine(
                $"{c.time:F3}," +
                $"{latestSensor.forwardTension:F3},{latestSensor.lateralTension:F3}," +
                $"{latestSensor.forwardVelocity:F3},{latestSensor.angularVelocity:F3}," +
                $"{latestSensor.joystickHorizontal:F3}," +
                $"{c.targetSpeed:F3},{c.targetSteer:F3},{c.throttle:F3},{c.steer:F3}");
        }

        private void OnDisable()
        {
            writer?.Flush();
            writer?.Close();
            writer = null;

            if (controller != null)
            {
                controller.OnSensorSampled -= HandleSensor;
                controller.OnControlCommand -= HandleCommand;
            }
        }
    }
}
