using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biscotte.Wave
{
    /// <summary>
    /// Spawns sets of surf waves from a pool. Every set has its own size (small, medium, big, bomb), the waves of a set
    /// vary a little around it. The rider is the centre of the world: every wave is built around where he is, and
    /// keeps following him while it is still a round swell far out, so there is no fixed spot to drift away from.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class WaveSetScheduler : MonoBehaviour
    {
        [Serializable]
        public struct SetSize
        {
            public string name;
            [Tooltip("Height relative to the spot's medium wave")] public float scale;
            [Tooltip("Relative chance of a set of this size")] public float weight;
        }

        public SurfSpotConfig spot;
        public SurfWave wavePrefab;
        [Min(1)] public int poolSize = 4;
        public float setInterval = 42f;
        public int wavesPerSet = 3;
        [Tooltip("Seconds between the waves of a set")] public float wavePeriod = 11f;
        public float firstSetDelay = 1f;
        public bool autoSpawn = true;
        [Tooltip("Scales every wave (difficulty / session setting)")] public float heightScale = 1f;
        public SetSize[] sizes =
        {
            new SetSize { name = "petite", scale = 0.68f, weight = 2f },
            new SetSize { name = "moyenne", scale = 0.95f, weight = 4f },
            new SetSize { name = "grosse", scale = 1.2f, weight = 2.5f },
            new SetSize { name = "bombe", scale = 1.45f, weight = 1f },
        };

        [Header("Aim (every wave comes to the rider)")]
        [Tooltip("Waves are built around this transform and follow it. Set by the rider at start; empty = the spot's lineup.")]
        public Transform aimAt;
        [Tooltip("Seconds before it starts to stand up that a wave stops following the rider: the take-off is set.")]
        public float commitLead = 1f;
        [Tooltip("How fast (m/s) a wave slides along its crest to follow the rider. Along its travel it follows\nfreely: moving the break line of a round swell changes nothing you can see.")]
        public float slideSpeed = 5f;

        /// <summary>Size of the set being spawned (for the HUD).</summary>
        public string CurrentSetName { get; private set; } = "";

        readonly List<SurfWave> pool = new List<SurfWave>();
        float nextSetTime;
        int nextId = 1;
        int pendingInSet;
        float nextWaveTime;
        float setScale = 1f;

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
            if (spot == null) return;
            AimWavesAtTheRider(Time.deltaTime);
            if (!autoSpawn) return;
            if (pendingInSet > 0 && Time.time >= nextWaveTime)
            {
                SpawnOne();
                pendingInSet--;
                nextWaveTime = Time.time + wavePeriod;
            }
            else if (pendingInSet == 0 && Time.time >= nextSetTime)
            {
                PickSetSize();
                pendingInSet = Mathf.Max(1, wavesPerSet);
                nextWaveTime = Time.time;
                nextSetTime = Time.time + setInterval;
            }
        }

        void PickSetSize()
        {
            float total = 0f;
            foreach (var s in sizes) total += Mathf.Max(0f, s.weight);
            float r = Random.value * total;
            foreach (var s in sizes)
            {
                r -= Mathf.Max(0f, s.weight);
                if (r > 0f) continue;
                setScale = s.scale;
                CurrentSetName = s.name;
                return;
            }
            setScale = 1f;
        }

        public SurfWave SpawnOne()
        {
            foreach (var w in pool)
            {
                if (w.IsAlive) continue;
                float hs = heightScale * setScale * Random.Range(0.92f, 1.08f);
                w.Spawn(spot, hs, nextId++, AimPosition());
                return w;
            }
            return null;
        }

        public SurfWave SpawnNow() => SpawnOne();

        Vector3 AimPosition() => aimAt != null ? aimAt.position : spot.LineupPosition();

        /// <summary>
        /// Keeps each wave aimed at where the rider IS, not where he was when it spawned: after a ride he is far
        /// down the line, and a player paddling about while the set comes in should still find the peak in front of
        /// him. Stops a moment before the wave starts to stand up — getting into position is the player's job then.
        /// </summary>
        void AimWavesAtTheRider(float dt)
        {
            if (aimAt == null || dt <= 0f) return;
            Vector3 target = aimAt.position;
            foreach (var w in pool)
            {
                if (w == null || !w.IsAlive) continue;
                float lead = w.SwellLead;
                if (lead <= commitLead) continue;
                var p = w.Params;
                Vector3 delta = target - w.AimPoint;
                float dS = Mathf.Clamp(Vector3.Dot(delta, (Vector3)p.crestDir), -slideSpeed * dt, slideSpeed * dt);
                // never pull the break so close that it would start to stand up right away
                float dD = Mathf.Max(Vector3.Dot(delta, (Vector3)p.travelDir), (commitLead - lead) * p.celerity);
                if (Mathf.Abs(dS) > 1e-3f || Mathf.Abs(dD) > 1e-3f) w.Retarget(dS, dD);
            }
        }
    }
}
