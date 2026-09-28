using Unity.Collections;
using Unity.Mathematics;

namespace WavyBoard.Wave
{
    /// <summary>Blittable description of one surf wave. Times are wave-relative (0 at spawn), distances in metres.</summary>
    public struct SurfWaveParams
    {
        public float3 origin;           // world position of s = 0 (the peak) on the crest line (y = sea level)
        public float3 travelDir;        // D, unit horizontal, toward the beach
        public float3 crestDir;         // T = cross(up, D), along the crest toward the ride side
        public float celerity;          // c (m/s)
        public float crestStartOffset;  // crest position along D at spawn
        public float firstBreakTime;    // wave time when the peak starts to pitch
        public float timeScale;         // bigger waves live slower (sqrt of the size)
        public float tubeScale;         // size of the curl / inside of the tube relative to the key shapes
        public float barrelHold;        // extra seconds (key time) the tube stays open: a longer barrel along the crest
        public float height;            // reference height H of this wave (m)
        public float breakLength;       // length of crest that breaks (m), rows beyond run into the channel
        public float endTaper;          // channel: the unbroken shoulder backs off over this length (m)
        public float sMin, sMax;        // mesh extent along the crest
        public float xiMin, xiMax;      // footprint along D relative to the crest
        public float spawnFade;         // the swell rises out of the sea over this time
        public float shoalDuration;     // swell -> steep face, just before the first break
        public float endTime;           // everything has settled: despawn
        public int id;

        public float CrestOffset(float tw) => crestStartOffset + celerity * tw;
        public float ShoalEnd => firstBreakTime + WaveProfile.TPre * timeScale;
    }

    /// <summary>The per-crest-coordinate description of a wave: (break delay after the peak, height, heaviness, lipless).</summary>
    public static class SurfWaveMath
    {
        public const float NeverBreaks = 1e5f;

        public static float4 SampleSections(in NativeArray<float4> sec, in SurfWaveParams p, float s)
        {
            int n = sec.Length;
            float u = math.saturate((s - p.sMin) / math.max(1e-3f, p.sMax - p.sMin)) * (n - 1);
            int i0 = (int)math.floor(u);
            int i1 = math.min(i0 + 1, n - 1);
            return math.lerp(sec[i0], sec[i1], u - i0);
        }

        /// <summary>Everything that shapes the cross-section at crest coordinate s and wave time tw.</summary>
        public static WaveProfile.RowInput Row(in SurfWaveParams p, in NativeArray<float4> sec, float s, float tw, out float4 k)
        {
            k = SampleSections(sec, p, s);
            float amp = math.smoothstep(0f, p.spawnFade, tw) * (1f - math.smoothstep(p.endTime - 6f, p.endTime, tw));
            // the unbroken shoulder that runs into the channel backs off into a rolling swell
            float channel = 1f - math.smoothstep(p.breakLength, p.breakLength + p.endTaper * 0.7f, s);
            float shoal = math.smoothstep(p.ShoalEnd - p.shoalDuration, p.ShoalEnd, tw) * channel;
            var r = new WaveProfile.RowInput
            {
                tau = WaveProfile.HoldBarrel((tw - p.firstBreakTime - k.x) / p.timeScale, p.barrelHold * (1f - k.w)),
                shoal = shoal,
                heavy = k.z,
                lipless = k.w,
                tubeScale = p.tubeScale,
                xScale = math.max(k.y, 0.55f * p.height),
                yScale = k.y * amp,
                xiMin = p.xiMin,
                xiMax = p.xiMax,
            };
            return r;
        }

        /// <summary>Crest coordinate where the curl is pitching at wave time tw.</summary>
        public static float PeelS(in SurfWaveParams p, in NativeArray<float4> sections, float tw)
        {
            float d = tw - p.firstBreakTime;
            if (d <= 0f) return 0f;
            int n = sections.Length;
            for (int i = 1; i < n; i++)
            {
                float s1 = math.lerp(p.sMin, p.sMax, (float)i / (n - 1));
                if (s1 <= 0f) continue;
                if (sections[i].x >= d)
                {
                    float s0 = math.lerp(p.sMin, p.sMax, (float)(i - 1) / (n - 1));
                    float d0 = sections[i - 1].x, d1 = sections[i].x;
                    return math.max(0f, math.lerp(s0, s1, math.saturate((d - d0) / math.max(1e-4f, d1 - d0))));
                }
            }
            return p.sMax;
        }

        /// <summary>Stage of the break at a point of the crest: 0 swell, 1 pitching, 2 barrel, 3 whitewater.</summary>
        public static float BreakPhase(in WaveProfile.RowInput ri)
        {
            float tau = ri.tau;
            float pre = 0.25f + 0.6f * ri.shoal;
            float phase;
            if (tau < WaveProfile.TPre) phase = pre;
            else if (tau < 0f) phase = math.lerp(pre, 1f, (tau - WaveProfile.TPre) / -WaveProfile.TPre);
            else if (tau < WaveProfile.TBarrel) phase = 1f + tau / WaveProfile.TBarrel;
            else if (tau < WaveProfile.TBarrel2) phase = 2f + 0.15f * (tau - WaveProfile.TBarrel) / (WaveProfile.TBarrel2 - WaveProfile.TBarrel);
            else if (tau < WaveProfile.TMound) phase = 2.15f + 0.85f * (tau - WaveProfile.TBarrel2) / (WaveProfile.TMound - WaveProfile.TBarrel2);
            else phase = 3f;
            if (ri.lipless > 0f && tau >= 0f) phase = math.lerp(phase, 1f + 2f * math.saturate(tau / WaveProfile.TCrumbleMound), ri.lipless);
            return phase;
        }

        /// <summary>How much of a point is churning whitewater (0..1): the broken wave, everything that stands up.</summary>
        public static float Churn(float y, in WaveProfile.RowInput ri)
        {
            float collapseK = math.lerp(math.smoothstep(3.3f, 4.9f, ri.tau), math.smoothstep(0.6f, 2.6f, ri.tau), ri.lipless);
            float yN = y / math.max(ri.yScale, 0.05f);
            return collapseK * math.smoothstep(0.03f, 0.22f, yN) * math.exp(-math.max(0f, ri.tau - 7f) / 6f);
        }

        /// <summary>
        /// Shading attributes of a point of the section: (thin water, foam, tube occlusion, face). r is the control-space
        /// coordinate of the point along the curve (0 front flat .. 14 back flat), x/y its local position in metres.
        /// </summary>
        public static float4 Attributes(float r, float x, float y, in WaveProfile.RowInput ri, in WaveProfile.Landmarks L, float celerity)
        {
            float tau = ri.tau;
            float H = math.max(ri.xScale, 0.3f);
            float yN = y / math.max(ri.yScale, 0.05f);
            float lipless = ri.lipless;

            float throwK = math.smoothstep(-0.3f, 1.2f, tau) * (1f - lipless);
            float barrelK = math.smoothstep(0.8f, 1.7f, tau) * (1f - math.smoothstep(3.5f, 4.5f, tau)) * (1f - lipless);
            float collapseK = math.lerp(math.smoothstep(3.3f, 4.9f, tau), math.smoothstep(0.6f, 2.6f, tau), lipless);
            float foamLife = math.exp(-math.max(0f, tau - 7f) / 4.5f);
            float amp = math.saturate(ri.yScale / math.max(0.3f, 0.3f * H));

            // thin water: the upper face of a steep wave and the whole thrown lip let the light through
            float onFront = r < 10.6f ? 1f : 0.3f;
            float thinFace = math.smoothstep(0.25f, 0.95f, yN) * onFront * math.lerp(0.3f, 1f, ri.shoal);
            float lipRole = math.saturate(1f - math.abs(r - 8.3f) / 3.3f);
            float thin = math.saturate(math.max(thinFace, lipRole * throwK * 1.1f)) * (1f - 0.85f * collapseK) * amp;

            // foam: never a smear, always something that is actually white on a real wave
            // 1. spray feathering off the crest while it pitches (offshore wind)
            float feather = math.smoothstep(-1.6f, -0.3f, tau) * (1f - math.smoothstep(0.9f, 2.0f, tau))
                            * math.saturate(1f - math.abs(r - 10.4f) / 1.1f) * 0.45f * (1f - lipless);
            // 2. the outside of the lip shatters, from its tip up, the longer it keeps throwing (the inside stays glassy)
            float lipReach = WaveProfile.Tip + 0.2f + 2.4f * math.saturate((tau - 1f) / 3f);
            float lipOuter = math.smoothstep(WaveProfile.Tip + 0.05f, WaveProfile.Tip + 0.6f, r) * (1f - math.smoothstep(lipReach - 0.6f, lipReach, r));
            float lipFoam = math.smoothstep(0.9f, 3.4f, tau) * lipOuter * 0.85f * (1f - lipless);
            // 3. the splash where the lip lands, on the water in front of the tube
            float dxT = (x - L.xTip) / (0.3f * H + 0.8f);
            float impact = math.smoothstep(1.2f, 1.9f, tau) * math.exp(-dxT * dxT) * (r < 4.5f ? 1f : 0f) * (1f - lipless);
            // 4. once collapsed, everything that stands up is whitewater; behind it, a trail of lace fading away
            float standing = math.smoothstep(0.03f, 0.22f, yN);
            float whitewater = collapseK * standing;
            float behind = math.max(0f, L.xRef - x);
            float trail = collapseK * math.exp(-behind / (celerity * 3.5f)) * (r > 9f ? 0.85f : 0.55f);
            float foam = math.max(math.max(feather, lipFoam), math.max(impact, math.max(whitewater, trail)));
            foam *= math.lerp(1f, foamLife, math.saturate(tau - 5f));
            foam *= amp;
            // foam never reaches the border of the mesh, where it meets the ambient sea
            foam *= math.smoothstep(ri.xiMin, ri.xiMin + 12f, x) * (1f - math.smoothstep(ri.xiMax - 8f, ri.xiMax, x));

            // occlusion inside the barrel: the face under the lip and the ceiling
            float ao = 0f;
            if (L.curl && L.xTip > L.xRef + 0.1f)
            {
                float under = math.saturate((L.xTip - x) / (L.xTip - L.xRef));
                if (r < WaveProfile.Wall + 0.5f) ao = under * math.smoothstep(1.5f, 3.5f, r);
                else if (r < WaveProfile.Tip) ao = 0.85f * math.saturate((WaveProfile.Tip - r) / 1.2f);
                ao *= barrelK;
            }

            float face = math.saturate(yN * 2.5f);
            return new float4(thin, math.saturate(foam), math.saturate(ao), face);
        }
    }
}
