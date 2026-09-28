using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WavyBoard.Wave
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SurfVertex
    {
        public float3 pos;
        public float3 nrm;
        public float4 tan;   // xyz: surface direction in which the flow coordinate U grows
        public float4 col;   // r = thin water, g = foam, b = tube occlusion, a = face
        public float2 uv0;   // flow coordinates (world metres on the flats, carried along the sheet on the wave)
        public float2 uv1;   // travel direction D (xz)
    }

    /// <summary>Pass 1: one cross-section per row (positions, shading attributes, flow coordinates).</summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Default)]
    public struct SurfWaveRowJob : IJobParallelFor
    {
        public SurfWaveParams P;
        [ReadOnly] public NativeArray<float4> Sections;
        [ReadOnly] public NativeArray<float2> Keys;
        [ReadOnly] public NativeArray<float2> VMap;
        public float TimeW;
        public int Ns, Nu;
        [NativeDisableParallelForRestriction] public NativeArray<float2> Scratch;   // Ns * Nu
        [NativeDisableParallelForRestriction] public NativeArray<float3> Pos;       // Ns * Nu
        [NativeDisableParallelForRestriction] public NativeArray<float4> Col;
        [NativeDisableParallelForRestriction] public NativeArray<float2> Flow;

        public float SAt(int row) => math.lerp(P.sMin, P.sMax, (float)row / (Ns - 1));

        public void Execute(int row)
        {
            float s = SAt(row);
            var ri = SurfWaveMath.Row(P, Sections, s, TimeW, out _);
            var pts = Scratch.Slice(row * Nu, Nu);
            WaveProfile.Section(Keys, VMap, ri, pts);
            var L = WaveProfile.Find(pts);

            float crest = P.CrestOffset(TimeW);
            float3 basePos = P.origin + P.crestDir * s + P.travelDir * crest;

            // arc length, for the flow coordinates
            float total = 0f;
            for (int j = 1; j < Nu; j++) total += math.distance(pts[j], pts[j - 1]);
            float xFront = crest + pts[0].x, xBack = crest + pts[Nu - 1].x;
            float2 o = P.origin.xz, D = P.travelDir.xz, T = P.crestDir.xz;

            // a hair of per-wave offset on the flats, so two waves of a set never z-fight where their footprints meet
            float flatBias = 0.003f * (P.id % 6);
            float arc = 0f;
            for (int j = 0; j < Nu; j++)
            {
                if (j > 0) arc += math.distance(pts[j], pts[j - 1]);
                float2 m = VMap[j];
                float r = m.x + m.y;
                float2 p = pts[j];
                float flat = (1f - math.smoothstep(0.6f, 1.6f, r)) + math.smoothstep(12.4f, 13.4f, r);
                int i = row * Nu + j;
                // whitewater is a boiling pile, not a smooth hump: lumps that churn and roll forward with the bore
                float churn = SurfWaveMath.Churn(p.y, ri);
                float lump = 0f;
                if (churn > 0.01f)
                {
                    float3 q = new float3(s * 0.42f, p.x * 0.42f - TimeW * 1.1f, TimeW * 0.7f);
                    lump = (noise.snoise(q) * 0.65f + noise.snoise(q * 2.3f + 11.7f) * 0.35f) * churn * 0.1f * ri.xScale;
                }
                Pos[i] = basePos + P.travelDir * p.x + new float3(0f, p.y + lump - flatBias * flat, 0f);
                Col[i] = SurfWaveMath.Attributes(r, p.x, p.y, ri, L, P.celerity);
                // U: anchored to the world on the front flat, and on the back flat, blended across the wave
                float uf = xFront - arc;
                float ub = xBack + (total - arc);
                float u = math.lerp(uf, ub, math.saturate((r - WaveProfile.F1) / (WaveProfile.B1 - WaveProfile.F1)));
                Flow[i] = o + D * u + T * s;
            }
        }
    }

    /// <summary>Pass 2: normals and tangents from the grid neighbours, packed into the vertex buffer.</summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Default)]
    public struct SurfWaveVertexJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Pos;
        [ReadOnly] public NativeArray<float4> Col;
        [ReadOnly] public NativeArray<float2> Flow;
        public float2 TravelXZ;
        public int Ns, Nu;
        [WriteOnly] public NativeArray<SurfVertex> Verts;

        public void Execute(int i)
        {
            int row = i / Nu, j = i % Nu;
            int r0 = math.max(row - 1, 0), r1 = math.min(row + 1, Ns - 1);
            int j0 = math.max(j - 1, 0), j1 = math.min(j + 1, Nu - 1);
            float3 dS = Pos[r1 * Nu + j] - Pos[r0 * Nu + j];
            float3 dU = Pos[row * Nu + j1] - Pos[row * Nu + j0];
            float3 n = math.cross(dS, dU);
            float nl = math.length(n);
            n = nl > 1e-8f ? n / nl : new float3(0f, 1f, 0f);
            float ul = math.length(dU);
            float3 tU = ul > 1e-8f ? -dU / ul : new float3(TravelXZ.x, 0f, TravelXZ.y);

            SurfVertex v;
            v.pos = Pos[i];
            v.nrm = n;
            v.tan = new float4(tU, 1f);
            v.col = Col[i];
            v.uv0 = Flow[i];
            v.uv1 = TravelXZ;
            Verts[i] = v;
        }
    }
}
