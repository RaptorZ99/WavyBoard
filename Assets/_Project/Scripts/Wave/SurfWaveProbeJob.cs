using WavyBoard.Ocean;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WavyBoard.Wave
{
    /// <summary>What the water is at one world point: inside it or not, the floor under it, the lip above it.</summary>
    public struct WaterProbe
    {
        public bool inside;     // under the face, inside the lip, or under the sea (the inside of a tube is air)
        public bool hasRoof;    // a thrown lip hangs above the point
        public float height;    // world height of the surface a board would ride here (the face under a lip)
        public float roofY;     // world height of the lip underside (valid when hasRoof)
    }

    /// <summary>Something that answers batches of water probes: write up to <see cref="Capacity"/> points into
    /// <see cref="ProbePoints"/>, call <see cref="Probe"/>, read <see cref="ProbeResults"/>. Main thread only.</summary>
    public interface IWaterProbe
    {
        int Capacity { get; }
        NativeArray<float3> ProbePoints { get; }
        NativeArray<WaterProbe> ProbeResults { get; }
        void Probe(int count, double time);
    }

    /// <summary>
    /// Burst batch of water queries against one wave (camera spring arm, render-time heights): the same profile code as
    /// the mesh and the gameplay sampler, a whole cross-section evaluated per point. Run synchronously (<c>.Run()</c>):
    /// it only reads the tables the mesh jobs read too.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Default, CompileSynchronously = true)]
    public struct SurfWaveProbeJob : IJob
    {
        public SurfWaveParams P;
        [ReadOnly] public NativeArray<float4> Sections;
        [ReadOnly] public NativeArray<float2> Keys;
        [ReadOnly] public NativeArray<float2> VMap;
        public SwellParams Swell;
        public float SwellT;
        public float TimeW;
        public int Count;
        [ReadOnly] public NativeArray<float3> Points;
        public NativeArray<WaterProbe> Results;
        public NativeArray<float2> Scratch;   // one section

        public void Execute()
        {
            var pts = Scratch.Slice(0, Scratch.Length);
            float crest = P.CrestOffset(TimeW);
            for (int i = 0; i < Count; i++)
            {
                float3 w = Points[i];
                float2 x0 = OceanSwell.Undeform(Swell, w.xz, SwellT);
                float hA = OceanSwell.Displacement(Swell, x0, SwellT).y;
                float3 rel = new float3(x0.x, 0f, x0.y) - P.origin;
                float s = math.dot(rel, P.crestDir);
                float xi = math.dot(rel, P.travelDir) - crest;
                float y = w.y - hA;
                var r = new WaterProbe { height = hA };
                if (s <= P.sMin || s >= P.sMax || xi <= P.xiMin || xi >= P.xiMax)
                {
                    r.inside = y < 0f;
                    Results[i] = r;
                    continue;
                }
                var ri = SurfWaveMath.Row(P, Sections, s, TimeW, out _);
                WaveProfile.Section(Keys, VMap, ri, pts);
                var L = WaveProfile.Find(pts);
                float yf = WaveProfile.HeightAt(pts, L, xi, out bool roof, out float roofY, out _);
                r.inside = WaveProfile.IsInside(pts, xi, y);
                r.height = hA + yf;
                r.hasRoof = roof;
                r.roofY = hA + roofY;
                Results[i] = r;
            }
        }
    }
}
