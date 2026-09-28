using System.Collections.Generic;
using WavyBoard.Wave;
using Unity.Mathematics;
using UnityEngine;

namespace WavyBoard.Ocean
{
    /// <summary>Single entry point to query the water: surf waves take precedence inside their footprint, the ambient sea elsewhere.</summary>
    [DefaultExecutionOrder(-200)]
    public class WaterSurfaceComposite : MonoBehaviour, IWaterSurface
    {
        public static WaterSurfaceComposite Instance { get; private set; }

        readonly List<SurfWave> active = new List<SurfWave>(8);
        public IReadOnlyList<SurfWave> ActiveSurfWaves => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

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
            return s;
        }

        /// <summary>Ambient-only height (fast path for camera / VFX).</summary>
        public float AmbientHeight(float3 worldPos)
        {
            var ocean = OceanSurface.Instance;
            return ocean != null ? ocean.Height(worldPos, Time.timeAsDouble) : 0f;
        }

        public float HeightAt(float3 worldPos, float time) => Sample(worldPos, time).Height;
    }
}
