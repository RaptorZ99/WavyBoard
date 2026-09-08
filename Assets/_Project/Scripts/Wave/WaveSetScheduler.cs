using System.Collections.Generic;
using UnityEngine;

namespace Biscotte.Wave
{
    /// <summary>Spawns sets of surf waves from a pool (spec §5.5).</summary>
    [DefaultExecutionOrder(-150)]
    public class WaveSetScheduler : MonoBehaviour
    {
        public SurfSpotConfig spot;
        public SurfWave wavePrefab;
        [Min(1)] public int poolSize = 4;
        public float setInterval = 45f;
        public int wavesPerSet = 2;
        public float wavePeriod = 11f;
        public float heightScale = 1f;
        public float firstSetDelay = 6f;
        public bool autoSpawn = true;

        readonly List<SurfWave> pool = new List<SurfWave>();
        float nextSetTime;
        int nextId = 1;
        int pendingInSet;
        float nextWaveTime;

        void Start()
        {
            for (int i = 0; i < poolSize; i++)
            {
                var w = Instantiate(wavePrefab, transform);
                w.name = $"SurfWave_{i}";
                w.gameObject.SetActive(true);
                pool.Add(w);
            }
            nextSetTime = Time.time + firstSetDelay;
        }

        void Update()
        {
            if (!autoSpawn || spot == null) return;
            if (pendingInSet > 0 && Time.time >= nextWaveTime)
            {
                SpawnOne();
                pendingInSet--;
                nextWaveTime = Time.time + wavePeriod;
            }
            else if (pendingInSet == 0 && Time.time >= nextSetTime)
            {
                pendingInSet = Mathf.Max(1, wavesPerSet);
                nextWaveTime = Time.time;
                nextSetTime = Time.time + setInterval;
            }
        }

        public SurfWave SpawnOne()
        {
            foreach (var w in pool)
            {
                if (!w.IsAlive)
                {
                    float hs = heightScale * Random.Range(0.85f, 1.15f);
                    w.Spawn(spot, hs, nextId++);
                    return w;
                }
            }
            return null;
        }

        public SurfWave SpawnNow() => SpawnOne();
    }
}
