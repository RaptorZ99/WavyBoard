using System.Runtime.InteropServices;
using Biscotte.Ocean;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Biscotte.Wave
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SurfVertex
    {
        public float3 pos;
        public float3 nrm;
        public float4 col;   // r = sss (translucency glow), g = foam, b = tubeAO, a = whitewater
        public float2 uv0;   // s/L, xi/lambda
        public float2 uv1;   // phase, sss
    }

    /// <summary>Generates the face grid + lip ribbon vertices for one surf wave (spec §5.3).</summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct SurfWaveMeshJob : IJobParallelFor
    {
        public SurfWaveParams P;
        [ReadOnly] public NativeArray<float4> Profile;
        public OceanParams Ambient;
        public float TimeW;        // wave-relative time
        public float AmbientTime;  // Storm Breakers time (Time.time)
        public int Ns, Nxi, Nv;
        public float GroundDepth;
        [NativeDisableParallelForRestriction] public NativeArray<SurfVertex> Verts;

        public void Execute(int i)
        {
            int faceCount = Ns * Nxi;
            if (i < faceCount) Face(i);
            else Lip(i - faceCount);
        }

        float SAt(int row) { return -P.sPad + (P.length + 2f * P.sPad) * row / (Ns - 1); }

        float3 World(float s, float xiWorld, float y)
        {
            return P.origin + P.crestDir * s + P.travelDir * xiWorld + new float3(0f, y, 0f);
        }

        void Face(int i)
        {
            int row = i / Nxi;
            int col = i % Nxi;
            float s = SAt(row);
            float u = (float)col / (Nxi - 1);
            // densify near the crest: cubic warp around u0 where xi = 0
            float u0 = (0f - P.xiMin) / (P.xiMax - P.xiMin);
            float d = u - u0;
            float uw = u0 + d * (0.55f + 0.45f * math.abs(d) / math.max(u0, 1f - u0));
            float xi = math.lerp(P.xiMin, P.xiMax, math.saturate(uw));
            float crest = P.CrestOffset(TimeW);

            SurfLocal L = SurfWaveMath.Evaluate(P, Profile, s, xi, TimeW);

            // The mesh holds the surf wave shape only, on the UNDEFORMED ambient plane: the Storm Breakers shader
            // (SurfWaveOcean graph) adds the ambient swell deformation and its normal on the GPU, exactly like the
            // ambient ocean plane, so both surfaces match without a seam.
            const float e = 0.2f;
            float hs1 = SurfWaveMath.HeightAt(P, Profile, s + e, xi, TimeW);
            float hs0 = SurfWaveMath.HeightAt(P, Profile, s - e, xi, TimeW);
            float hx1 = SurfWaveMath.HeightAt(P, Profile, s, xi + e, TimeW);
            float hx0 = SurfWaveMath.HeightAt(P, Profile, s, xi - e, TimeW);
            float dhs = (hs1 - hs0) / (2f * e);
            float dhx = (hx1 - hx0) / (2f * e);
            float3 nW = math.normalize(new float3(0f, 1f, 0f) - P.crestDir * dhs - P.travelDir * dhx);

            // The ambient plane has an alpha-clipped hole under this grid (inset 1 m from the grid border, see OceanAmbient),
            // so the grid IS the water surface inside. Only the outer ring (overlapping the plane) dips a few cm under it.
            float edgeDist = math.min(math.min(s + P.sPad, P.length + P.sPad - s), math.min(xi - P.xiMin, P.xiMax - xi));
            float ring = 1f - math.smoothstep(0f, 1.5f, edgeDist);
            float y = L.height - 0.04f * ring;

            SurfVertex v;
            v.pos = World(s, crest + xi, y);
            v.nrm = nW;
            v.col = new float4(L.sss, L.foam, L.tubeAO, L.whitewater);
            v.uv0 = new float2(s / math.max(1f, P.length), xi / P.wavelength);
            v.uv1 = new float2(L.phase, L.sss);
            Verts[i] = v;
        }

        void Lip(int j)
        {
            int perSide = Ns * Nv;
            int side = j / perSide;      // 0 = top surface, 1 = underside
            int rem = j % perSide;
            int row = rem / Nv;
            int col = rem % Nv;
            float s = SAt(row);
            float vv = (float)col / (Nv - 1);
            float crest = P.CrestOffset(TimeW);

            SurfLocal L = SurfWaveMath.Evaluate(P, Profile, s, 0f, TimeW);

            SurfWaveMath.LipPoint(L, vv, out float2 pt, out float2 nrm2, out float th);
            float sign = side == 0 ? 1f : -1f;
            float2 pos2 = pt + nrm2 * (th * 0.5f * sign);
            float3 nrm = P.travelDir * nrm2.x + new float3(0f, nrm2.y, 0f);
            nrm = math.normalize(nrm) * sign;

            // the thrown lip is translucent water; foam only where the tip shatters (last part of the curl)
            float tipFoam = math.smoothstep(0.6f, 1f, vv) * 0.6f * L.lipAmount;
            SurfVertex v;
            v.pos = World(s, crest + pos2.x, pos2.y);
            v.nrm = nrm;
            v.col = new float4(0.35f, tipFoam, side == 1 ? 0.6f * L.lipAmount : 0f, 0f);
            v.uv0 = new float2(s / math.max(1f, P.length), 0.5f + vv * 0.2f);
            v.uv1 = new float2(L.phase, 1f);
            int baseIndex = Ns * Nxi;
            Verts[baseIndex + j] = v;
        }
    }
}
