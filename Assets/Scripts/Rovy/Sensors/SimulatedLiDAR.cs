using UnityEngine;

namespace Rovy.Sensors
{
    // 既存の LiDARScanner が持つスキャン結果を ILiDAR インターフェースに変換するアダプタ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LiDARScanner))]
    public sealed class SimulatedLiDAR : MonoBehaviour, ILiDAR
    {
        [SerializeField] private int rayCount = 36;

        private LiDARScanner scanner;
        private float[] ranges = System.Array.Empty<float>();

        public int RayCount => rayCount;

        private void Awake()
        {
            scanner = GetComponent<LiDARScanner>();
            ranges = new float[rayCount];
            ResetRanges();
        }

        private void Update()
        {
            if (scanner == null)
                return;

            ScanResult[] results = scanner.LastScanResults;
            ResetRanges();

            foreach (ScanResult hit in results)
            {
                // 水平角からバケットインデックスを計算
                Vector3 dir = hit.direction;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                    continue;

                float angle = Vector3.SignedAngle(Vector3.forward, dir.normalized, Vector3.up);
                if (angle < 0f)
                    angle += 360f;

                int bucket = Mathf.FloorToInt(angle / (360f / rayCount)) % rayCount;

                // 近い方を残す
                if (hit.distance < ranges[bucket])
                    ranges[bucket] = hit.distance;
            }
        }

        public float[] GetRanges() => ranges;

        private void ResetRanges()
        {
            for (int i = 0; i < ranges.Length; i++)
                ranges[i] = float.PositiveInfinity;
        }
    }
}
