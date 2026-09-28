using System;
using Unity.Mathematics;

namespace Biscotte.Ocean
{
    /// <summary>One component of the ambient sea, as authored (metres, degrees).</summary>
    [Serializable]
    public struct SwellComponent
    {
        [UnityEngine.Tooltip("Crest-to-crest distance (m)")] public float wavelength;
        [UnityEngine.Tooltip("Half the crest-to-trough height (m)")] public float amplitude;
        [UnityEngine.Tooltip("Direction the crests travel to, degrees around world up (0 = +Z)")] public float directionDeg;
        [UnityEngine.Tooltip("0 = round sine, 1 = sharpest Gerstner crest")] [UnityEngine.Range(0f, 1f)] public float steepness;
        public float phase;
    }

    /// <summary>
    /// Blittable ambient sea: a sum of up to <see cref="Max"/> Gerstner components. The exact same formulas run in
    /// Shaders/Water/WaterCore.hlsl (SwellDisplace / SwellNormal), so what gameplay samples is what the player sees.
    /// Per component: a = (dir.x, dir.z, k, amplitude), b = (omega, phase, horizontal amplitude QA, LOD fade distance).
    /// </summary>
    public struct SwellParams
    {
        public const int Max = 6;
        public float4 a0, a1, a2, a3, a4, a5;
        public float4 b0, b1, b2, b3, b4, b5;
        public int count;

        public void Get(int i, out float4 a, out float4 b)
        {
            switch (i)
            {
                case 0: a = a0; b = b0; return;
                case 1: a = a1; b = b1; return;
                case 2: a = a2; b = b2; return;
                case 3: a = a3; b = b3; return;
                case 4: a = a4; b = b4; return;
                default: a = a5; b = b5; return;
            }
        }

        public void Set(int i, float4 a, float4 b)
        {
            switch (i)
            {
                case 0: a0 = a; b0 = b; return;
                case 1: a1 = a; b1 = b; return;
                case 2: a2 = a; b2 = b; return;
                case 3: a3 = a; b3 = b; return;
                case 4: a4 = a; b4 = b; return;
                default: a5 = a; b5 = b; return;
            }
        }

        /// <summary>Bakes authored components. The horizontal (Gerstner) amplitudes are scaled so the surface can
        /// never fold over itself (sum of k * QA stays below 0.9), which also guarantees the CPU inversion converges.</summary>
        public static SwellParams Build(SwellComponent[] comps, float lodSamplesPerWave)
        {
            var p = new SwellParams();
            if (comps == null) return p;
            int n = math.min(comps.Length, Max);
            for (int i = 0; i < n; i++)
            {
                var c = comps[i];
                float k = 2f * math.PI / math.max(0.5f, c.wavelength);
                float omega = math.sqrt(9.81f * k);
                float dir = math.radians(c.directionDeg);
                float qa = math.min(math.saturate(c.steepness) / (k * n), 4f * c.amplitude);
                // vertex LOD: beyond this distance the ambient mesh is too coarse for this component (the pixel
                // shader still shades it through the normal)
                float fade = math.max(20f, c.wavelength * lodSamplesPerWave);
                p.Set(i, new float4(math.sin(dir), math.cos(dir), k, c.amplitude), new float4(omega, c.phase, qa, fade));
                p.count++;
            }
            return p;
        }
    }

    public static class OceanSwell
    {
        /// <summary>Displacement of the undeformed point x0 at time t (horizontal Gerstner + height).</summary>
        public static float3 Displacement(in SwellParams p, float2 x0, float t)
        {
            float3 d = float3.zero;
            for (int i = 0; i < p.count; i++)
            {
                p.Get(i, out float4 a, out float4 b);
                float th = a.z * math.dot(a.xy, x0) - b.x * t + b.y;
                math.sincos(th, out float s, out float c);
                d.x += a.x * b.z * c;
                d.z += a.y * b.z * c;
                d.y += a.w * s;
            }
            return d;
        }

        /// <summary>Surface normal at the undeformed point x0 (GPU Gems 1, ch. 1).</summary>
        public static float3 Normal(in SwellParams p, float2 x0, float t)
        {
            float3 n = new float3(0f, 1f, 0f);
            for (int i = 0; i < p.count; i++)
            {
                p.Get(i, out float4 a, out float4 b);
                float th = a.z * math.dot(a.xy, x0) - b.x * t + b.y;
                math.sincos(th, out float s, out float c);
                float wa = a.z * a.w;
                n.x -= a.x * wa * c;
                n.z -= a.y * wa * c;
                n.y -= a.z * b.z * s;
            }
            return math.normalize(n);
        }

        /// <summary>Velocity of the water surface at the undeformed point x0.</summary>
        public static float3 Velocity(in SwellParams p, float2 x0, float t)
        {
            float3 v = float3.zero;
            for (int i = 0; i < p.count; i++)
            {
                p.Get(i, out float4 a, out float4 b);
                float th = a.z * math.dot(a.xy, x0) - b.x * t + b.y;
                math.sincos(th, out float s, out float c);
                v.x += a.x * b.z * b.x * s;
                v.z += a.y * b.z * b.x * s;
                v.y -= a.w * b.x * c;
            }
            return v;
        }

        /// <summary>The undeformed point whose displaced position lies above the world point xz (fixed point; the
        /// Gerstner amplitudes are bounded so it is a contraction).</summary>
        public static float2 Undeform(in SwellParams p, float2 xz, float t)
        {
            float2 x0 = xz;
            for (int it = 0; it < 4; it++)
            {
                float3 d = Displacement(p, x0, t);
                x0 = xz - d.xz;
            }
            return x0;
        }

        /// <summary>Height, normal and velocity of the ambient sea above the world point xz.</summary>
        public static void Sample(in SwellParams p, float2 xz, float t, out float height, out float3 normal, out float3 velocity, out float2 x0)
        {
            x0 = Undeform(p, xz, t);
            height = Displacement(p, x0, t).y;
            normal = Normal(p, x0, t);
            velocity = Velocity(p, x0, t);
        }
    }
}
