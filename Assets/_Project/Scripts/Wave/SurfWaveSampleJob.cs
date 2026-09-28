using WavyBoard.Ocean;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WavyBoard.Wave
{
    /// <summary>
    /// The gameplay sampler of a surf wave, in Burst: height, normal, water velocity, break stage, tube, pocket energy
    /// at one world point, answered from the same analytic profile the mesh is built from (so the board rides exactly
    /// the water that is drawn). Run synchronously by <see cref="SurfWave.Sample"/>.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Default, CompileSynchronously = true)]
    public struct SurfWaveSampleJob : IJob
    {
        public SurfWaveParams P;
        [ReadOnly] public NativeArray<float4> Sections;
        [ReadOnly] public NativeArray<float2> Keys;
        [ReadOnly] public NativeArray<float2> VMap;
        public SwellParams Swell;
        public float SwellT;
        public float TimeW;
        public float3 Point;
        public int FootVertex;                // WaveProfile.VertexOfControl(Foot): a managed table, resolved outside Burst
        public NativeArray<WaterSample> Out;
        public NativeArray<float2> A, B, C;   // three cross-sections of scratch

        WaveProfile.Landmarks EvalRow(float s, float tw, NativeArray<float2> buf, out WaveProfile.RowInput ri)
        {
            ri = SurfWaveMath.Row(P, Sections, s, tw, out _);
            var slice = new NativeSlice<float2>(buf);
            WaveProfile.Section(Keys, VMap, ri, slice);
            return WaveProfile.Find(slice);
        }

        public void Execute()
        {
            float3 worldPos = Point;
            OceanSwell.Sample(Swell, worldPos.xz, SwellT, out float hA, out float3 nA, out float3 vA, out float2 x0);

            float tw = TimeW;
            float3 rel = new float3(x0.x, 0f, x0.y) - P.origin;
            float s = math.dot(rel, P.crestDir);
            float xi = math.dot(rel, P.travelDir) - P.CrestOffset(tw);
            var slice = new NativeSlice<float2>(A);
            var L = EvalRow(s, tw, A, out var ri);
            float y = WaveProfile.HeightAt(slice, L, xi, out bool hasRoof, out float roofY, out float hit);

            // normal from the analytic surface: across the section on this row, along the crest on two more rows
            const float e = 0.25f;
            float yx1 = WaveProfile.HeightAt(slice, L, xi + e, out _, out _, out _);
            float yx0 = WaveProfile.HeightAt(slice, L, xi - e, out _, out _, out _);
            var L1 = EvalRow(s + e, tw, B, out _);
            var L0 = EvalRow(s - e, tw, C, out _);
            float ys1 = WaveProfile.HeightAt(new NativeSlice<float2>(B), L1, xi, out _, out _, out _);
            float ys0 = WaveProfile.HeightAt(new NativeSlice<float2>(C), L0, xi, out _, out _, out _);
            float3 D = P.travelDir, T = P.crestDir;
            float3 nW = math.normalize(new float3(0f, 1f, 0f) - T * ((ys1 - ys0) / (2f * e)) - D * ((yx1 - yx0) / (2f * e)));
            float3 n = math.normalize(nW + (nA - new float3(0f, 1f, 0f)));

            int hi = math.clamp((int)hit, 0, VMap.Length - 1);
            float2 m = VMap[hi];
            float4 attr = SurfWaveMath.Attributes(m.x + m.y, xi, y, ri, L, P.celerity);
            float phase = SurfWaveMath.BreakPhase(ri);
            float cd = xi - L.xRef;
            float faceWidth = math.max(1f, A[FootVertex].x - L.xRef);
            float waveH = math.max(0f, L.yTop);

            WaterSample r = default;
            r.Height = hA + y;
            float2 faceTop = L.curl ? A[L.faceEnd] : new float2(L.xTop, L.yTop);
            r.FaceTop = hA + faceTop.y;
            r.OnBack = xi < faceTop.x;
            r.SeaLevel = hA;
            r.Normal = n;
            r.BreakPhase = phase;
            r.TravelDir = D;
            r.CrestDir = T;
            r.CrestDistance = cd;
            r.FaceWidth = faceWidth;
            r.WaveHeight = waveH;
            float whitewater = phase >= 2f || ri.lipless > 0.5f ? attr.y : 0f;
            r.WhitewaterAmount = whitewater;
            r.WaveId = P.id;
            float peelS = SurfWaveMath.PeelS(P, Sections, tw);
            r.PeelDistance = s - peelS;
            r.PeelSpeed = SurfWaveMath.PeelSpeed(P, Sections, peelS);
            float pocket = math.saturate(1f - math.abs(cd - 0.35f * faceWidth) / (0.8f * faceWidth));
            r.Energy = pocket * math.smoothstep(0.35f, 1f, math.min(phase, 1f)) * (1f - math.smoothstep(2.2f, 2.8f, phase))
                       * math.saturate(waveH / 0.8f);
            if (L.curl && L.xTip > L.xRef + 0.3f)
            {
                r.LipWidth = L.xTip - L.xRef;
                r.LipHeight = math.max(0f, L.yTop - L.yTip);
            }

            // push of the moving water: the face carries a rider along D; the whitewater shoves him. Only on the body of
            // the wave (or in its foam): the flat water around it does not move (it used to push a paddler to the beach
            // from 30 m in front of a wave)
            float hNorm = waveH > 0.05f ? math.saturate(y / waveH) : 0f;
            float body = math.max(math.saturate(y / math.max(0.3f, 0.2f * waveH)), whitewater);
            float push = (phase >= 2.3f ? P.celerity * 0.85f : P.celerity * (0.55f + 0.45f * hNorm) * math.smoothstep(0.15f, 0.9f, phase)) * body;
            const float dt = 0.05f;
            var Lf = EvalRow(s, tw + dt, B, out _);
            float yFuture = WaveProfile.HeightAt(new NativeSlice<float2>(B), Lf, xi - P.celerity * dt, out _, out _, out _);
            r.Velocity = D * push + new float3(0f, (yFuture - y) / dt, 0f) + vA * 0.5f;

            // the tube: riding the face under the thrown lip with some headroom, or flying under it
            if (hasRoof)
            {
                r.HasLipRoof = true;
                r.LipRoofY = hA + roofY;
                float yRel = worldPos.y - hA;
                float headroom = roofY - y;
                bool onFace = math.abs(yRel - y) < 0.8f;
                if (headroom > 0.35f && (onFace || yRel < roofY))
                {
                    r.InTube = true;
                    r.TubeDepth = math.saturate(1f - cd / math.max(0.05f, r.LipWidth));
                }
            }
            Out[0] = r;
        }
    }
}
