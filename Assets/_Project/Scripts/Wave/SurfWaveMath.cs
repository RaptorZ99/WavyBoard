using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Biscotte.Wave
{
    /// <summary>Blittable description of one surf wave (spec §5.1). Times are wave-relative (0 at spawn).</summary>
    public struct SurfWaveParams
    {
        public float3 origin;          // world position of s = 0 on the crest line at spawn (y = sea level)
        public float3 travelDir;       // D, unit horizontal
        public float3 crestDir;        // T, unit horizontal (= cross(up, D))
        public float celerity;         // c (m/s)
        public float wavelength;       // lambda (m)
        public float length;           // L, crest length used by the profile (m)
        public float crestStartOffset; // crest position along D at spawn (negative = out at sea)
        public float shoalTime, pitchTime, collapseTime, decayTime;
        public float spawnFadeTime;    // fade-in duration
        public float endTaper;         // taper length at s = 0 and s = L (m)
        public float sPad;             // mesh/footprint padding beyond [0, L] (m)
        public float xiMin, xiMax;     // footprint along D relative to the crest
        public int id;

        public float CrestOffset(float tw) => crestStartOffset + celerity * tw;
        public float BreakTime(float breakOffset) => (breakOffset - crestStartOffset) / celerity;
    }

    /// <summary>Result of evaluating the surf wave at (s, xi, t) in wave-local coordinates.</summary>
    public struct SurfLocal
    {
        public float height;      // wave height contribution (>= 0, no ambient)
        public float phase;       // phi
        public float tau;         // t - tBreak(s)
        public float H;           // local wave height parameter
        public float I;           // vortex ratio
        public float crestShift;  // ξ of the crest
        public float faceWidth;   // Lf
        public float lipAmount;   // 0..1
        public float lipW, lipH;  // wv, hv
        public float energy;
        public float foam;
        public float tubeAO;
        public float sss;
        public float whitewater;
        public float envelope;    // taper * fade
    }

    [BurstCompile]
    public static class SurfWaveMath
    {
        // --- profile sampling: float4(breakOffset f(s), height H(s), intensity I(s), ramp) uniformly sampled over [0, L]
        public static float4 SampleProfile(in NativeArray<float4> prof, float length, float s)
        {
            int n = prof.Length;
            if (n == 0) return new float4(0f, 1f, 2f, 0f);
            float u = math.saturate(s / math.max(1e-3f, length)) * (n - 1);
            int i0 = (int)math.floor(u);
            int i1 = math.min(i0 + 1, n - 1);
            float f = u - i0;
            return math.lerp(prof[i0], prof[i1], f);
        }

        public static float Phase(in SurfWaveParams p, float tau)
        {
            if (tau < -p.shoalTime) return 0f;
            if (tau < 0f) return (tau + p.shoalTime) / p.shoalTime;
            if (tau < p.pitchTime) return 1f + tau / p.pitchTime;
            float tc = tau - p.pitchTime;
            if (tc < p.collapseTime) return 2f + tc / p.collapseTime;
            return 3f;
        }

        public static float Envelope(in SurfWaveParams p, float s, float tw)
        {
            float taper = math.smoothstep(0f, p.endTaper, s + p.sPad * 0.5f) * math.smoothstep(0f, p.endTaper, p.length + p.sPad * 0.5f - s);
            float fade = math.smoothstep(0f, p.spawnFadeTime, tw);
            return taper * fade;
        }

        /// <summary>Core evaluation (spec §5.2). xi is measured from the (unshifted) crest along D.</summary>
        public static SurfLocal Evaluate(in SurfWaveParams p, in NativeArray<float4> prof, float s, float xi, float tw)
        {
            SurfLocal r = default;
            float4 k = SampleProfile(prof, p.length, s);
            float breakOffset = k.x;
            float H = math.max(0.05f, k.y);
            float I = math.clamp(k.z, 1.2f, 4f);
            float tBreak = p.BreakTime(breakOffset);
            float tau = tw - tBreak;
            float phi = Phase(p, tau);
            float env = Envelope(p, s, tw);
            float lambda = p.wavelength;

            float kSteep = math.smoothstep(0.15f, 1f, math.min(phi, 1f));
            float Lb = 0.5f * lambda * (1f + 0.15f * kSteep);
            float Lf = 0.5f * lambda * (1f - 0.72f * kSteep);
            float crestShift = 0.06f * lambda * kSteep;
            float x = xi - crestShift;

            float h;
            if (x <= 0f)
            {
                float xb = math.clamp(-x / Lb, 0f, 1f);
                h = 0.5f * (1f + math.cos(math.PI * xb));
            }
            else
            {
                float xf = math.clamp(x / Lf, 0f, 1f);
                float swell = 0.5f * (1f + math.cos(math.PI * xf));
                float a = 1f + 2.2f * kSteep;
                float steep = math.pow(1f - xf, a);
                h = math.lerp(swell, steep, kSteep);
            }

            // lip geometry (present while pitching / barrelling)
            float lipAmount = math.smoothstep(1f, 1.3f, phi) * (1f - math.smoothstep(2.0f, 2.45f, phi));
            float hv = 0.85f * H * math.saturate((phi - 1f) / 0.8f) * math.smoothstep(1f, 1.15f, phi);
            float wv = hv * I * 0.9f;

            // collapse to whitewater
            float whitewater = 0f;
            if (phi > 2f)
            {
                float c = math.saturate(phi - 2f);
                float tAfter = math.max(0f, tau - (p.pitchTime + p.collapseTime));
                float Hw = 0.55f * math.exp(-tAfter / p.decayTime) + 0.15f;
                float xw = (x - 0.18f * lambda) / (0.45f * lambda);
                float pile = math.exp(-xw * xw);
                h = math.lerp(h, Hw * pile, c);
                whitewater = c * math.saturate(pile * 1.6f) * math.saturate(Hw * 1.6f);
            }

            float height = H * h * env;

            // whitewater is a rolling, turbulent pile, not a smooth bump: low-frequency noise travelling with the foam
            if (whitewater > 0.01f)
            {
                float2 np = new float2(s * 0.22f + tau * 0.4f, x * 0.3f - tau * 1.1f);
                float turb = noise.snoise(np) * 0.65f + noise.snoise(np * 2.3f + 7.1f) * 0.35f;
                height += 0.15f * H * whitewater * env * turb;
            }

            // pocket energy: strongest just in front of the crest on a steep face
            float pocket = math.saturate(1f - math.abs(x - 0.35f * Lf) / (0.8f * Lf));
            float energy = pocket * math.smoothstep(0.35f, 1f, math.min(phi, 1f)) * (1f - math.smoothstep(2.0f, 2.6f, phi)) * env;

            // foam: feathering crest before/at pitch, lip impact zone, whitewater
            // feathering: a thin line of spray right at the crest while the wave pitches (1 m in front, 0.5 m behind)
            float sigF = x >= 0f ? 1.0f : 0.5f;
            float feather = math.smoothstep(0.95f, 1.35f, phi) * (1f - math.smoothstep(2.0f, 2.5f, phi)) * math.exp(-(x * x) / (2f * sigF * sigF));
            float impact = lipAmount * math.exp(-math.pow((x - wv) / math.max(0.5f, 0.35f * wv + 0.4f), 2f));
            float foam = math.saturate(feather * 0.45f + impact * 0.9f);   // whitewater is carried separately (vertex colour A)

            // tube ambient occlusion on the face under the lip
            float tubeAO = 0f;
            if (lipAmount > 0f && x > 0f && x < wv) tubeAO = lipAmount * (1f - x / wv);

            // SSS thickness proxy: thin water near the top of a steep face
            float sss = math.saturate(h * h) * kSteep * (1f - whitewater);

            r.height = height; r.phase = phi; r.tau = tau; r.H = H; r.I = I; r.crestShift = crestShift; r.faceWidth = Lf;
            r.lipAmount = lipAmount; r.lipW = wv; r.lipH = hv; r.energy = energy; r.foam = foam; r.tubeAO = tubeAO; r.sss = sss;
            r.whitewater = whitewater; r.envelope = env;
            return r;
        }

        /// <summary>Lip centerline point (x, y) in the (D, up) plane for v in [0,1], plus its 2D normal and thickness. Local to the crest shift.</summary>
        public static void LipPoint(in SurfLocal L, float v, out float2 pt, out float2 nrm, out float thickness)
        {
            float2 p0 = new float2(L.crestShift, L.H * L.envelope);
            float wv = L.lipW * L.envelope, hv = L.lipH * L.envelope;
            float2 p3 = new float2(L.crestShift + wv, L.H * L.envelope - hv);
            float2 p1 = p0 + new float2(0.5f * wv, 0.25f * hv);
            float2 p2 = new float2(L.crestShift + wv * 1.15f, L.H * L.envelope - 0.35f * hv);
            float u = 1f - v;
            pt = u * u * u * p0 + 3f * u * u * v * p1 + 3f * u * v * v * p2 + v * v * v * p3;
            float2 tan = 3f * u * u * (p1 - p0) + 6f * u * v * (p2 - p1) + 3f * v * v * (p3 - p2);
            float tl = math.length(tan);
            tan = tl > 1e-5f ? tan / tl : new float2(1f, 0f);
            nrm = new float2(-tan.y, tan.x); // left normal of the curve: points up/back at the start
            thickness = 0.14f * L.H * L.envelope * (1f - 0.7f * v) * L.lipAmount;
        }

        /// <summary>Approximate lip underside height at local x (0..wv). Returns false if outside the lip span.</summary>
        public static bool LipUndersideY(in SurfLocal L, float x, out float y)
        {
            y = 0f;
            float wv = L.lipW * L.envelope;
            if (L.lipAmount <= 0.02f || wv <= 0.05f || x < 0f || x > wv) return false;
            float v = math.saturate(x / wv);
            LipPoint(L, v, out float2 pt, out float2 nrm, out float th);
            y = pt.y - th * 0.5f;
            return true;
        }

        /// <summary>Total surface height at wave-local (s, xi) including the ambient component provided by the caller.</summary>
        public static float HeightAt(in SurfWaveParams p, in NativeArray<float4> prof, float s, float xi, float tw)
        {
            return Evaluate(p, prof, s, xi, tw).height;
        }

        /// <summary>Surface velocity in world space (translation + push + vertical motion).</summary>
        public static float3 Velocity(in SurfWaveParams p, in NativeArray<float4> prof, float s, float xi, float tw, in SurfLocal L)
        {
            float hNorm = L.H > 0f ? math.saturate(L.height / (L.H * math.max(L.envelope, 1e-3f))) : 0f;
            float push;
            if (L.phase >= 2f) push = p.celerity * 0.85f;
            else push = p.celerity * (0.55f + 0.45f * hNorm) * math.smoothstep(0.15f, 0.9f, L.phase);
            float dt = 0.05f;
            float h2 = HeightAt(p, prof, s, xi - p.celerity * dt, tw + dt); // same world point, wave moved forward
            float vy = (h2 - L.height) / dt;
            return p.travelDir * push + new float3(0f, vy, 0f);
        }
    }
}
