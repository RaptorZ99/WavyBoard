using System.Collections.Generic;
using Biscotte.Wave;
using Unity.Mathematics;
using UnityEngine;

namespace Biscotte.Ocean
{
    /// <summary>Single entry point to query the water: surf waves take precedence inside their footprint, ambient ocean elsewhere.</summary>
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

        public WaterSample Sample(float3 worldPos, float time)
        {
            var amb = OceanAmbient.Instance;
            OceanParams p = amb != null ? amb.Params : default;
            float groundDepth = amb != null ? amb.GroundDepth(worldPos) : 200f;
            float ambientTime = amb != null ? amb.AmbientTime : time;

            // Surf waves first (footprint test), the newest wave wins if two overlap.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var w = active[i];
                if (w != null && w.IsAlive && w.Contains(worldPos))
                    return w.Sample(worldPos, time, in p, ambientTime, groundDepth);
            }

            // Ambient ocean.
            WaterSample s = default;
            s.WaveId = -1;
            s.BreakPhase = -1f;
            s.SeabedDepth = groundDepth;
            float3 und = new float3(worldPos.x, 0f, worldPos.z);
            float h = OceanMath.GetHeight(in p, ambientTime, worldPos, ref und, out float3 def, groundDepth);
            s.Height = h;
            s.Normal = OceanMath.GetNormal(in p, ambientTime, und, def, groundDepth, 0.25f);
            s.Velocity = OceanMath.GetVelocity(in p, ambientTime, und, def, groundDepth, 0.1f);
            s.TravelDir = new float3(0f, 0f, 1f);
            s.CrestDir = new float3(1f, 0f, 0f);
            return s;
        }

        /// <summary>Ambient-only height (fast path for camera / VFX).</summary>
        public float AmbientHeight(float3 worldPos)
        {
            var amb = OceanAmbient.Instance;
            if (amb == null) return 0f;
            return OceanMath.QuickHeight(in amb.Params, amb.AmbientTime, worldPos, amb.GroundDepth(worldPos));
        }

        public float HeightAt(float3 worldPos, float time)
        {
            return Sample(worldPos, time).Height;
        }
    }
}
