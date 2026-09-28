using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WavyBoard.Wave
{
    /// <summary>
    /// Spawns sets of surf waves from a pool. Every set has its own size (small, medium, big, bomb), the waves of a set
    /// vary a little around it. The rider is the centre of the world: every wave is built around where he is, and
    /// keeps following him while it is still a round swell far out, so there is no fixed spot to drift away from.
    ///
    /// Waves never crowd each other. Aiming at the rider places a new crest relative to where he is NOW — and a rider on a
    /// wave is being carried toward the beach at its speed, so the next wave would be born right behind the one in
    /// between. So every new crest is started far enough out to sea to stay <see cref="minCrestSpacing"/> (or more, for
    /// big waves) behind every wave still alive, for as long as that wave lives, even when it is the faster of the two
    /// (bigger sets are faster). It still breaks where the rider is: it just arrives a little later. The waves of a set
    /// all travel at the set's speed, so they keep their spacing.
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
        [Min(1)] public int poolSize = 6;
        public float setInterval = 42f;
        public int wavesPerSet = 3;
        [Tooltip("Seconds between the waves of a set")] public float wavePeriod = 11f;
        public float firstSetDelay = 1f;
        public bool autoSpawn = true;
        [Tooltip("Scales every wave (difficulty / session setting)")] public float heightScale = 1f;
        [Tooltip("Crest to crest, never less than this between two waves (m), whatever their speeds and where the rider is.\nThe length of a wave's footprint (SurfSpotConfig xiMax - xiMin) keeps them from overlapping at all")]
        public float minCrestSpacing = 78f;
        [Tooltip("... and never less than this many times the sum of the two waves' heights (big waves need more room)")]
        public float spacingPerHeight = 4f;
        [Tooltip("A wave whose crest is this far past the rider toward the beach is of no more use: it fades out and frees\nits slot, so the next set is never skipped for want of one")]
        public float retireDistance = 150f;
        [Tooltip("Seconds a retired wave takes to fade out")]
        public float retireFade = 6f;
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
            RetireWavesLeftBehind();
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
                return;
            }
            setScale = 1f;
        }

        public SurfWave SpawnOne()
        {
            SurfWave free = null;
            foreach (var w in pool) if (!w.IsAlive) { free = w; break; }
            if (free == null) return null;
            float hs = heightScale * setScale * Random.Range(0.92f, 1.08f);
            float cs = heightScale * setScale;   // the set travels together: one speed for all its waves
            Vector3 aim = AimPosition();
            free.Spawn(spot, hs, nextId++, aim, cs, ExtraLeadFor(aim, spot.CelerityFor(cs), spot.height * hs));
            return free;
        }

        /// <summary>
        /// How far further out to sea (m) a new wave has to start so that, from now until every wave ahead of it is
        /// gone, its crest stays at least the required spacing behind theirs.
        /// </summary>
        float ExtraLeadFor(Vector3 aim, float c, float H)
        {
            float crest = spot.SpawnCrestAlongD(aim, c);
            float limit = crest;
            foreach (var w in pool)
            {
                if (w == null || !w.IsAlive) continue;
                var p = w.Params;
                float remaining = Mathf.Max(0f, p.endTime - w.WaveTime);
                float gap = Mathf.Max(minCrestSpacing, spacingPerHeight * (p.height + H));
                // a faster new wave closes in on it for the rest of its life
                gap += Mathf.Max(0f, (c - p.celerity) * remaining);
                limit = Mathf.Min(limit, w.CrestAlongD - gap);
            }
            return Mathf.Max(0f, crest - limit);
        }

        public SurfWave SpawnNow() => SpawnOne();

        Vector3 AimPosition() => aimAt != null ? aimAt.position : spot.LineupPosition();

        /// <summary>Fades out the waves that rolled on far past the rider toward the beach.</summary>
        void RetireWavesLeftBehind()
        {
            if (aimAt == null) return;
            float riderD = Vector3.Dot(aimAt.position, (Vector3)spot.TravelDir);
            foreach (var w in pool)
            {
                if (w == null || !w.IsAlive || w.CrestAlongD - riderD < retireDistance) continue;
                w.Retire(retireFade);
            }
        }

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
