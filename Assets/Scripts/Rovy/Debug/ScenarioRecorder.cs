using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rovy.Debug
{
    // プレイヤーの位置・回転を記録し、同じ動作を再生することで
    // 異なる PID パラメータを同一条件で比較できる。
    [DisallowMultipleComponent]
    public sealed class ScenarioRecorder : MonoBehaviour
    {
        public enum RecorderMode { Idle, Recording, Replaying }

        [Serializable]
        private struct Sample
        {
            public float t;
            public Vector3 pos;
            public float rotY;
        }

        [Serializable]
        private struct ScenarioData
        {
            public int version;
            public string startedAt;
            public Sample[] samples;
        }

        [SerializeField] private Transform player;
        [SerializeField] private string scenarioFile = "scenario_01.json";
        [SerializeField] private RecorderMode mode = RecorderMode.Idle;

        private readonly List<Sample> samples = new List<Sample>();
        private float startTime;
        private PlayerController playerController;

        public RecorderMode Mode => mode;

        private void Awake()
        {
            playerController = player != null ? player.GetComponent<PlayerController>() : null;
        }

        public void StartRecording()
        {
            samples.Clear();
            startTime = Time.time;
            mode = RecorderMode.Recording;
        }

        public void StopRecording()
        {
            if (mode != RecorderMode.Recording)
                return;

            mode = RecorderMode.Idle;
            SaveScenario();
        }

        public void StartReplay()
        {
            if (!LoadScenario())
                return;

            startTime = Time.time;
            mode = RecorderMode.Replaying;

            if (playerController != null)
                playerController.enabled = false;
        }

        public void StopReplay()
        {
            mode = RecorderMode.Idle;

            if (playerController != null)
                playerController.enabled = true;
        }

        private void FixedUpdate()
        {
            if (player == null)
                return;

            if (mode == RecorderMode.Recording)
            {
                samples.Add(new Sample
                {
                    t = Time.time - startTime,
                    pos = player.position,
                    rotY = player.eulerAngles.y,
                });
            }
            else if (mode == RecorderMode.Replaying && samples.Count >= 2)
            {
                float elapsed = Time.time - startTime;

                if (elapsed >= samples[samples.Count - 1].t)
                {
                    StopReplay();
                    return;
                }

                (Sample a, Sample b, float alpha) = FindInterpolationPair(elapsed);
                player.position = Vector3.Lerp(a.pos, b.pos, alpha);
                player.eulerAngles = new Vector3(
                    0f,
                    Mathf.LerpAngle(a.rotY, b.rotY, alpha),
                    0f);
            }
        }

        private (Sample a, Sample b, float alpha) FindInterpolationPair(float elapsed)
        {
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].t >= elapsed)
                {
                    Sample a = samples[i - 1];
                    Sample b = samples[i];
                    float span = b.t - a.t;
                    float alpha = span > 0f ? (elapsed - a.t) / span : 0f;
                    return (a, b, alpha);
                }
            }

            Sample last = samples[samples.Count - 1];
            return (last, last, 0f);
        }

        private void SaveScenario()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Scenarios");
            Directory.CreateDirectory(dir);

            var data = new ScenarioData
            {
                version = 1,
                startedAt = DateTime.Now.ToString("o"),
                samples = samples.ToArray(),
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(Path.Combine(dir, scenarioFile), json);
        }

        private bool LoadScenario()
        {
            string path = Path.Combine(Application.persistentDataPath, "Scenarios", scenarioFile);

            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogWarning($"[ScenarioRecorder] File not found: {path}");
                return false;
            }

            var data = JsonUtility.FromJson<ScenarioData>(File.ReadAllText(path));
            samples.Clear();
            samples.AddRange(data.samples);
            return samples.Count >= 2;
        }
    }
}
