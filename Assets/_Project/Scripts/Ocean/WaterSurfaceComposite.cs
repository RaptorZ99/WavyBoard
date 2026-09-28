using System.Collections.Generic;
using Unity.Collections;
using WavyBoard.Wave;
using Unity.Mathematics;
using UnityEngine;

namespace WavyBoard.Ocean
{
    /// <summary>
    /// Single entry point to query the water: inside a surf wave's footprint the wave answers, the ambient sea elsewhere.
    /// Where footprints overlap (a set coming in behind a ridden wave), the point belongs to the wave it is nearest to
    /// (see <see cref="SurfWave.Ownership"/>).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class WaterSurfaceComposite : MonoBehaviour, IWaterSurface, IWaterProbe
    {
        public static WaterSurfaceComposite Instance { get; private set; }

        readonly List<SurfWave> active = new List<SurfWave>(8);
        public IReadOnlyList<SurfWave> ActiveSurfWaves => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public const int ProbeCapacity = 64;
        NativeArray<float3> probePts, subPts;
        NativeArray<WaterProbe> probeRes, subRes;
        readonly int[] subIdx = new int[ProbeCapacity];
        readonly int[] owners = new int[ProbeCapacity];

        void OnEnable()
        {
            Instance = this;
            EnsureBuffers();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (probePts.IsCreated) probePts.Dispose();
            if (probeRes.IsCreated) probeRes.Dispose();
            if (subPts.IsCreated) subPts.Dispose();
            if (subRes.IsCreated) subRes.Dispose();
        }

        // also used outside Play mode (the ride simulation), where OnEnable never runs
        void EnsureBuffers()
        {
            if (probePts.IsCreated) return;
            probePts = new NativeArray<float3>(ProbeCapacity, Allocator.Persistent);
            probeRes = new NativeArray<WaterProbe>(ProbeCapacity, Allocator.Persistent);
            subPts = new NativeArray<float3>(ProbeCapacity, Allocator.Persistent);
            subRes = new NativeArray<WaterProbe>(ProbeCapacity, Allocator.Persistent);
        }

        /// <summary>Releases the probe buffers of a composite that never went through OnEnable/OnDisable (simulation).</summary>
        public void ReleaseBuffers() => OnDisable();

        public int Capacity => ProbeCapacity;
        public NativeArray<float3> ProbePoints { get { EnsureBuffers(); return probePts; } }
        public NativeArray<WaterProbe> ProbeResults { get { EnsureBuffers(); return probeRes; } }

        /// <summary>
        /// Answers the batch in <see cref="ProbePoints"/>[0..count): each point goes to the wave that owns it (as
        /// <see cref="Sample(float3, double)"/>), the rest to the ambient sea. Burst, allocation-free.
        /// </summary>
        public void Probe(int count, double time)
        {
            EnsureBuffers();
            count = math.min(count, ProbeCapacity);
            for (int i = 0; i < count; i++) owners[i] = OwnerIndex(probePts[i], time);
            for (int wi = 0; wi < active.Count; wi++)
            {
                int n = 0;
                for (int i = 0; i < count; i++)
                {
                    if (owners[i] != wi) continue;
                    subIdx[n] = i; subPts[n] = probePts[i]; n++;
                }
                if (n == 0) continue;
                active[wi].RunProbe(subPts, subRes, n, time);
                for (int k = 0; k < n; k++) probeRes[subIdx[k]] = subRes[k];
            }
            var ocean = OceanSurface.Instance;
            for (int i = 0; i < count; i++)
            {
                if (owners[i] >= 0) continue;
                float h = ocean != null ? ocean.Height(probePts[i], time) : 0f;
                probeRes[i] = new WaterProbe { inside = probePts[i].y < h, height = h };
            }
        }

        /// <summary>One probe at a world point: inside the water or not, the ridable surface and the lip above it.</summary>
        public WaterProbe ProbeOne(float3 worldPos, double time)
        {
            EnsureBuffers();
            probePts[0] = worldPos;
            Probe(1, time);
            return probeRes[0];
        }

        public void Register(SurfWave w) { if (!active.Contains(w)) active.Add(w); }
        public void Unregister(SurfWave w) { active.Remove(w); }

        /// <summary>Index in <see cref="ActiveSurfWaves"/> of the wave that owns the point at that time, or -1 (ambient sea).</summary>
        public int OwnerIndex(float3 worldPos, double time)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < active.Count; i++)
            {
                var w = active[i];
                if (w == null || !w.Ownership(worldPos, time, out float score) || score >= bestScore) continue;
                best = i; bestScore = score;
            }
            return best;
        }

        public WaterSample Sample(float3 worldPos, double time)
        {
            int oi = OwnerIndex(worldPos, time);
            var owner = oi >= 0 ? active[oi] : null;
            if (owner != null) return owner.Sample(worldPos, time);

            WaterSample s = default;
            s.WaveId = -1;
            s.BreakPhase = -1f;
            s.TravelDir = new float3(0f, 0f, 1f);
            s.CrestDir = new float3(1f, 0f, 0f);
            var ocean = OceanSurface.Instance;
            if (ocean == null) { s.Normal = new float3(0f, 1f, 0f); return s; }
            ocean.Sample(worldPos, time, out s.Height, out s.Normal, out s.Velocity);
            s.SeaLevel = s.FaceTop = s.Height;
            return s;
        }

        /// <summary>The live surf wave with this id, or null.</summary>
        public SurfWave FindWave(int id)
        {
            if (id < 0) return null;
            for (int i = 0; i < active.Count; i++)
            {
                var w = active[i];
                if (w != null && w.IsAlive && w.Params.id == id) return w;
            }
            return null;
        }

        /// <summary>True when the world point is under water: inside a surf wave (the inside of a tube is air), or under
        /// the ambient sea. Same precedence as <see cref="Sample(float3, double)"/>.</summary>
        public bool IsInsideWater(float3 worldPos, double time) => ProbeOne(worldPos, time).inside;
    }
}
