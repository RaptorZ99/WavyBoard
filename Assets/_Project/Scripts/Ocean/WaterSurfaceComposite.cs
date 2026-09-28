using System.Collections.Generic;
using Unity.Collections;
using WavyBoard.Wave;
using Unity.Mathematics;
using UnityEngine;

namespace WavyBoard.Ocean
{
    /// <summary>Single entry point to query the water: surf waves take precedence inside their footprint, the ambient sea elsewhere.</summary>
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
        readonly bool[] claimed = new bool[ProbeCapacity];

        void OnEnable()
        {
            Instance = this;
            probePts = new NativeArray<float3>(ProbeCapacity, Allocator.Persistent);
            probeRes = new NativeArray<WaterProbe>(ProbeCapacity, Allocator.Persistent);
            subPts = new NativeArray<float3>(ProbeCapacity, Allocator.Persistent);
            subRes = new NativeArray<WaterProbe>(ProbeCapacity, Allocator.Persistent);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (probePts.IsCreated) probePts.Dispose();
            if (probeRes.IsCreated) probeRes.Dispose();
            if (subPts.IsCreated) subPts.Dispose();
            if (subRes.IsCreated) subRes.Dispose();
        }

        public int Capacity => ProbeCapacity;
        public NativeArray<float3> ProbePoints => probePts;
        public NativeArray<WaterProbe> ProbeResults => probeRes;

        /// <summary>
        /// Answers the batch in <see cref="ProbePoints"/>[0..count): each point goes to the wave whose footprint holds it
        /// (the newest first, as <see cref="Sample(float3, double)"/>), the rest to the ambient sea. Burst, allocation-free.
        /// </summary>
        public void Probe(int count, double time)
        {
            count = math.min(count, ProbeCapacity);
            for (int i = 0; i < count; i++) claimed[i] = false;
            for (int wi = active.Count - 1; wi >= 0; wi--)
            {
                var w = active[wi];
                if (w == null || !w.IsAlive) continue;
                int n = 0;
                for (int i = 0; i < count; i++)
                {
                    if (claimed[i] || !w.Contains(probePts[i])) continue;
                    claimed[i] = true; subIdx[n] = i; subPts[n] = probePts[i]; n++;
                }
                if (n == 0) continue;
                w.RunProbe(subPts, subRes, n, time);
                for (int k = 0; k < n; k++) probeRes[subIdx[k]] = subRes[k];
            }
            var ocean = OceanSurface.Instance;
            for (int i = 0; i < count; i++)
            {
                if (claimed[i]) continue;
                float h = ocean != null ? ocean.Height(probePts[i], time) : 0f;
                probeRes[i] = new WaterProbe { inside = probePts[i].y < h, height = h };
            }
        }

        /// <summary>One probe at a world point: inside the water or not, the ridable surface and the lip above it.</summary>
        public WaterProbe ProbeOne(float3 worldPos, double time)
        {
            probePts[0] = worldPos;
            Probe(1, time);
            return probeRes[0];
        }

        public void Register(SurfWave w) { if (!active.Contains(w)) active.Add(w); }
        public void Unregister(SurfWave w) { active.Remove(w); }

        public WaterSample Sample(float3 worldPos, float time) => Sample(worldPos, (double)time);

        public WaterSample Sample(float3 worldPos, double time)
        {
            // surf waves first (footprint test), the newest wave wins if two overlap
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var w = active[i];
                if (w != null && w.IsAlive && w.Contains(worldPos)) return w.Sample(worldPos, time);
            }

            WaterSample s = default;
            s.WaveId = -1;
            s.BreakPhase = -1f;
            s.SeabedDepth = 200f;
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

        /// <summary>Ambient-only height (fast path for camera / VFX).</summary>
        public float AmbientHeight(float3 worldPos)
        {
            var ocean = OceanSurface.Instance;
            return ocean != null ? ocean.Height(worldPos, Time.timeAsDouble) : 0f;
        }

        public float HeightAt(float3 worldPos, float time) => Sample(worldPos, time).Height;
    }
}
