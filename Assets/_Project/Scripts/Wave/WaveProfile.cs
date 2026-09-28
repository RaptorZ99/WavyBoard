using Unity.Collections;
using Unity.Mathematics;

namespace Biscotte.Wave
{
    /// <summary>
    /// Cross-section of the surf wave: ONE continuous curve from the flat water in front of the wave, up the face, round
    /// the back wall of the tube, along the ceiling (lip underside), round the lip tip, back over the lip and the crest
    /// and down the back of the wave. It is a centripetal Catmull-Rom spline through 15 control points keyframed over
    /// the life of a point of the crest (swell, steep, pitch, throw, barrel, impact, mound, bore, flat). Units of the
    /// keys: wave height H; x toward the beach, x = 0 at the back wall of the tube. The table is shaped against the
    /// reference photos (Teahupoo) and graphed by Tools/wave_profile_lab.py; keep both identical.
    /// </summary>
    public static class WaveProfile
    {
        public const int M = 15;
        // control point roles, front (beach) -> back (sea)
        public const int F2 = 0, F1 = 1, Foot = 2, FaceLo = 3, FaceMid = 4, Wall = 5, Ceil = 6, LipIn = 7, Tip = 8,
                         LipOut = 9, LipTop = 10, Crest = 11, Back = 12, B1 = 13, B2 = 14;

        // keys
        public const int KSwell = 0, KSteep = 1, KPitch = 2, KThrow = 3, KBarrel = 4, KBarrel2 = 5, KImpact = 6, KMound = 7,
                         KBore = 8, KFlat = 9, KeyCount = 10;
        /// <summary>Pseudo key: the pre-break shape, lerp(swell, steep, shoal).</summary>
        public const int KPre = -1;

        // life of a point of the crest, relative to its break (s)
        public const float TPre = -1.6f, TPitch = 0f, TThrow = 0.75f, TBarrel = 1.5f, TBarrel2 = 3.3f, TImpact = 4.1f,
                           TMound = 5.2f, TBore = 7.5f, TFlat = 16f;
        // a point that never throws a lip (behind the peak, closing section) crumbles instead
        public const float TCrumbleMound = 2.2f, TCrumbleBore = 4.8f, TCrumbleFlat = 14f;

        /// <summary>Samples per spline segment (segment i joins control points i and i+1): denser round the lip.</summary>
        static readonly int[] kSegmentSamples = { 10, 8, 8, 8, 8, 10, 10, 9, 9, 10, 10, 10, 8, 10 };
        public static readonly int VertexCount = Sum(kSegmentSamples) + 1;

        static int Sum(int[] a) { int s = 0; foreach (int v in a) s += v; return s; }

        /// <summary>The hand-shaped keys (steep .. impact), 15 (x, y) pairs each; the round ones are built by Bump.</summary>
        static readonly float[] kShaped =
        {
            // steep
            7f, 0f, 3.4f, 0f, 2.1f, 0.03f, 1.45f, 0.19f, 0.95f, 0.45f, 0.58f, 0.67f, 0.36f, 0.78f, 0.2f, 0.83f, 0.07f, 0.855f,
            -0.05f, 0.86f, -0.2f, 0.845f, -0.5f, 0.78f, -1.45f, 0.44f, -3.6f, 0f, -7f, 0f,
            // pitch
            7f, 0f, 3.2f, 0f, 1.7f, 0.02f, 0.95f, 0.18f, 0.42f, 0.47f, 0.17f, 0.72f, 0.16f, 0.9f, 0.3f, 0.99f, 0.46f, 1.01f,
            0.4f, 1.08f, 0.18f, 1.1f, -0.25f, 1.02f, -1.25f, 0.55f, -3.6f, 0f, -7f, 0f,
            // throw
            7f, 0f, 3.1f, 0f, 1.55f, 0.015f, 0.85f, 0.14f, 0.32f, 0.4f, 0.04f, 0.66f, 0.2f, 0.92f, 0.62f, 0.87f, 0.9f, 0.62f,
            1.02f, 0.83f, 0.66f, 1.05f, 0.12f, 1.08f, -1.1f, 0.6f, -3.6f, 0f, -7f, 0f,
            // barrel (the lip has just landed)
            7f, 0f, 3.0f, 0f, 1.55f, 0.005f, 0.8f, 0.08f, 0.24f, 0.31f, 0f, 0.6f, 0.24f, 0.9f, 0.92f, 0.68f, 1.16f, 0.03f,
            1.4f, 0.4f, 1.06f, 0.93f, 0.3f, 1.1f, -1f, 0.64f, -3.6f, 0f, -7f, 0f,
            // barrel2 (end of the open tube, the lip plunging)
            7f, 0f, 3.0f, 0f, 1.6f, 0f, 0.82f, 0.075f, 0.26f, 0.3f, 0.02f, 0.58f, 0.26f, 0.88f, 0.98f, 0.64f, 1.24f, -0.07f,
            1.47f, 0.37f, 1.1f, 0.9f, 0.33f, 1.08f, -1f, 0.63f, -3.6f, 0f, -7f, 0f,
            // impact (the tube closes)
            7f, 0f, 3.0f, 0f, 1.62f, 0f, 0.85f, 0.07f, 0.32f, 0.26f, 0.12f, 0.44f, 0.36f, 0.63f, 0.92f, 0.47f, 1.38f, -0.12f,
            1.62f, 0.3f, 1.12f, 0.76f, 0.42f, 0.9f, -0.9f, 0.55f, -3.6f, 0f, -7f, 0f,
        };

        static readonly float[] kSwellX = { 7f, 3.6f, 2.4f, 1.75f, 1.25f, 0.85f, 0.55f, 0.32f, 0.14f, -0.02f, -0.2f, -0.5f, -1.4f, -3.6f, -7f };
        static readonly float[] kMoundX = { 7f, 3.6f, 2.5f, 2.05f, 1.72f, 1.45f, 1.22f, 1.03f, 0.86f, 0.68f, 0.48f, 0.18f, -0.75f, -3.5f, -7f };
        static readonly float[] kBoreX = { 7f, 4.2f, 3.2f, 2.7f, 2.35f, 2.05f, 1.8f, 1.58f, 1.38f, 1.16f, 0.9f, 0.5f, -0.5f, -3.4f, -7f };
        static readonly float[] kFlatX = { 7f, 4.4f, 3.4f, 2.9f, 2.55f, 2.25f, 2f, 1.78f, 1.58f, 1.36f, 1.1f, 0.7f, -0.3f, -3.3f, -7f };

        static float Bump(float x, float h, float wf, float wb, float xc)
        {
            float u = (x - xc) / (x >= xc ? wf : wb);
            return h * (0.5f + 0.5f * math.cos(math.PI * math.min(1f, math.abs(u))));
        }

        /// <summary>Allocates the key table (KeyCount * M points) used by the jobs and the gameplay sampler.</summary>
        public static NativeArray<float2> CreateKeys(Allocator allocator)
        {
            var keys = new NativeArray<float2>(KeyCount * M, allocator);
            for (int k = KSteep; k <= KImpact; k++)
            for (int j = 0; j < M; j++)
            {
                int i = ((k - KSteep) * M + j) * 2;
                keys[k * M + j] = new float2(kShaped[i], kShaped[i + 1]);
            }
            for (int j = 0; j < M; j++)
            {
                keys[KSwell * M + j] = new float2(kSwellX[j], Bump(kSwellX[j], 0.42f, 3.0f, 3.2f, 0f));
                keys[KMound * M + j] = new float2(kMoundX[j], Bump(kMoundX[j], 0.6f, 1.9f, 2.6f, 0.9f));
                keys[KBore * M + j] = new float2(kBoreX[j], Bump(kBoreX[j], 0.34f, 2.1f, 2.8f, 1.35f));
                keys[KFlat * M + j] = new float2(kFlatX[j], Bump(kFlatX[j], 0.03f, 2.3f, 3.0f, 1.55f));
            }
            return keys;
        }

        /// <summary>Per curve vertex: (segment index, parameter in the segment).</summary>
        public static NativeArray<float2> CreateVertexMap(Allocator allocator)
        {
            var map = new NativeArray<float2>(VertexCount, allocator);
            int n = 0;
            for (int i = 0; i < kSegmentSamples.Length; i++)
                for (int k = 0; k < kSegmentSamples[i]; k++)
                    map[n++] = new float2(i, (float)k / kSegmentSamples[i]);
            map[n] = new float2(M - 2, 1f);
            return map;
        }

        /// <summary>Curve vertex index of a control point (the vertex that sits exactly on it).</summary>
        public static int VertexOfControl(int control)
        {
            int n = 0;
            for (int i = 0; i < control && i < kSegmentSamples.Length; i++) n += kSegmentSamples[i];
            return n;
        }

        // ------------------------------------------------------------------ evaluation (Burst-compatible)

        static float2 KeyPoint(in NativeArray<float2> keys, int key, int j, float shoal)
        {
            if (key == KPre) return math.lerp(keys[KSwell * M + j], keys[KSteep * M + j], shoal);
            return keys[key * M + j];
        }

        static int NormalKey(int i)
        {
            switch (i)
            {
                case 0: return KPre;
                case 1: return KPitch;
                case 2: return KThrow;
                case 3: return KBarrel;
                case 4: return KBarrel2;
                case 5: return KImpact;
                case 6: return KMound;
                case 7: return KBore;
                default: return KFlat;
            }
        }

        static float NormalTime(int i)
        {
            switch (i)
            {
                case 0: return TPre;
                case 1: return TPitch;
                case 2: return TThrow;
                case 3: return TBarrel;
                case 4: return TBarrel2;
                case 5: return TImpact;
                case 6: return TMound;
                case 7: return TBore;
                default: return TFlat;
            }
        }

        static int CrumbleKey(int i)
        {
            switch (i)
            {
                case 0: return KPre;
                case 1: return KMound;
                case 2: return KBore;
                default: return KFlat;
            }
        }

        static float CrumbleTime(int i)
        {
            switch (i)
            {
                case 0: return TPre;
                case 1: return TCrumbleMound;
                case 2: return TCrumbleBore;
                default: return TCrumbleFlat;
            }
        }

        /// <summary>Catmull-Rom across the keys of one path (uniform in key index, time remapped piecewise-linearly).</summary>
        static float2 PathPoint(in NativeArray<float2> keys, bool crumble, int j, float tau, float shoal)
        {
            int n = crumble ? 4 : 9;
            float t0 = crumble ? CrumbleTime(0) : NormalTime(0);
            float tn = crumble ? CrumbleTime(n - 1) : NormalTime(n - 1);
            if (tau <= t0) return KeyPoint(keys, crumble ? CrumbleKey(0) : NormalKey(0), j, shoal);
            if (tau >= tn) return KeyPoint(keys, crumble ? CrumbleKey(n - 1) : NormalKey(n - 1), j, shoal);
            int i = 0;
            while (i < n - 2 && (crumble ? CrumbleTime(i + 1) : NormalTime(i + 1)) < tau) i++;
            float ta = crumble ? CrumbleTime(i) : NormalTime(i);
            float tb = crumble ? CrumbleTime(i + 1) : NormalTime(i + 1);
            float u = (tau - ta) / (tb - ta);
            int i0 = math.max(0, i - 1), i3 = math.min(n - 1, i + 2);
            float2 p0 = KeyPoint(keys, crumble ? CrumbleKey(i0) : NormalKey(i0), j, shoal);
            float2 p1 = KeyPoint(keys, crumble ? CrumbleKey(i) : NormalKey(i), j, shoal);
            float2 p2 = KeyPoint(keys, crumble ? CrumbleKey(i + 1) : NormalKey(i + 1), j, shoal);
            float2 p3 = KeyPoint(keys, crumble ? CrumbleKey(i3) : NormalKey(i3), j, shoal);
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        /// <summary>Everything that decides the shape of one cross-section.</summary>
        public struct RowInput
        {
            public float tau;        // time since this point of the crest started to break (s)
            public float shoal;      // 0 round swell .. 1 steep face, before the break
            public float heavy;      // 0 mellow curl .. 1 Teahupoo slab
            public float lipless;    // 0 throws a lip .. 1 crumbles (behind the peak, closing section)
            public float xScale;     // metres per key unit along D
            public float yScale;     // metres per key unit up (height * envelope)
            public float xiMin, xiMax;
        }

        /// <summary>The 15 control points in metres (x along D from the crest line, y up), plus two ghost points.</summary>
        public static void Controls(in NativeArray<float2> keys, in RowInput r, ref FixedList512Bytes<float2> c)
        {
            c.Clear();
            c.Add(float2.zero);   // ghost, set below
            for (int j = 0; j < M; j++)
            {
                float2 p = PathPoint(keys, false, j, r.tau, r.shoal);
                if (r.lipless > 0f) p = math.lerp(p, PathPoint(keys, true, j, r.tau, r.shoal), r.lipless);
                if (j >= Wall && j <= Crest && p.x > 0f) p.x *= 0.55f + 0.45f * r.heavy;
                float x = j == F2 ? r.xiMax : (j == B2 ? r.xiMin : p.x * r.xScale);
                c.Add(new float2(x, p.y * r.yScale));
            }
            c.Add(float2.zero);
            c[0] = 2f * c[1] - c[2];
            c[M + 1] = 2f * c[M] - c[M - 1];
        }

        /// <summary>Centripetal Catmull-Rom point of segment i (between controls i and i+1) at t in [0, 1].</summary>
        public static float2 CurvePoint(ref FixedList512Bytes<float2> c, int i, float t)
        {
            float2 p0 = c[i], p1 = c[i + 1], p2 = c[i + 2], p3 = c[i + 3];
            float t0 = 0f;
            float t1 = t0 + math.sqrt(math.max(math.distance(p0, p1), 1e-4f));
            float t2 = t1 + math.sqrt(math.max(math.distance(p1, p2), 1e-4f));
            float t3 = t2 + math.sqrt(math.max(math.distance(p2, p3), 1e-4f));
            float tt = math.lerp(t1, t2, t);
            float2 a1 = math.lerp(p0, p1, (tt - t0) / (t1 - t0));
            float2 a2 = math.lerp(p1, p2, (tt - t1) / (t2 - t1));
            float2 a3 = math.lerp(p2, p3, (tt - t2) / (t3 - t2));
            float2 b1 = math.lerp(a1, a2, (tt - t0) / (t2 - t0));
            float2 b2 = math.lerp(a2, a3, (tt - t1) / (t3 - t1));
            return math.lerp(b1, b2, (tt - t1) / (t2 - t1));
        }

        /// <summary>Evaluates one whole cross-section into pts (VertexCount points).</summary>
        public static void Section(in NativeArray<float2> keys, in NativeArray<float2> vmap, in RowInput r, NativeSlice<float2> pts)
        {
            var c = new FixedList512Bytes<float2>();
            Controls(keys, r, ref c);
            for (int j = 0; j < vmap.Length; j++)
            {
                float2 m = vmap[j];
                pts[j] = CurvePoint(ref c, (int)m.x, m.y);
            }
        }

        // ------------------------------------------------------------------ reading a section

        /// <summary>The landmarks of a section that gameplay and shading need.</summary>
        public struct Landmarks
        {
            public int faceEnd;     // last vertex of the face branch (x decreasing from the front)
            public int tipIndex;    // lowest point of the lip after the face branch (== faceEnd when there is no curl)
            public int backStart;   // first vertex of the back branch (x decreasing to the back), from the lip's front
            public bool curl;       // the curve overhangs: a lip and a tube exist
            public float xRef;      // "crest" reference: back wall of the tube, or the top of the wave
            public float xTip, yTip, yTop, xTop;
        }

        public static Landmarks Find(NativeSlice<float2> pts)
        {
            var L = new Landmarks();
            int n = pts.Length;
            int j = 0;
            while (j < n - 1 && pts[j + 1].x <= pts[j].x + 1e-4f) j++;
            L.faceEnd = j;
            L.curl = j < n - 1;
            float yTop = float.MinValue, xTop = 0f;
            for (int k = 0; k < n; k++) if (pts[k].y > yTop) { yTop = pts[k].y; xTop = pts[k].x; }
            L.yTop = yTop; L.xTop = xTop;
            if (L.curl)
            {
                // ceiling: forward from the wall, down to the lowest point of the lip (its tip)
                int t = j, k2 = j;
                float yMin = float.MaxValue;
                while (k2 < n - 1 && pts[k2 + 1].x >= pts[k2].x - 1e-4f)
                {
                    k2++;
                    if (pts[k2].y < yMin) { yMin = pts[k2].y; t = k2; }
                }
                L.tipIndex = t;
                int b = n - 1;
                while (b > 0 && pts[b - 1].x >= pts[b].x - 1e-4f) b--;
                L.backStart = b;
                L.xRef = math.min(pts[j].x, xTop);
                L.xTip = pts[t].x; L.yTip = pts[t].y;
            }
            else
            {
                L.tipIndex = j;
                L.backStart = j;
                L.xRef = xTop;
                L.xTip = xTop; L.yTip = yTop;
            }
            return L;
        }

        /// <summary>Height of the face branch (the floor a rider stands on) at x, or of the back of the wave behind the
        /// face; also the lip underside above x when inside the tube. Returns the curve index of the hit segment.</summary>
        public static float HeightAt(NativeSlice<float2> pts, in Landmarks L, float x, out bool hasRoof, out float roofY, out float hitIndex)
        {
            hasRoof = false; roofY = 0f; hitIndex = 0f;
            int n = pts.Length;
            float y;
            if (x >= pts[L.faceEnd].x)
            {
                y = Cross(pts, 0, L.faceEnd, x, false, out hitIndex);
                if (L.curl && x < L.xTip)
                {
                    roofY = Cross(pts, L.faceEnd, L.tipIndex, x, true, out _);
                    hasRoof = roofY > y + 0.05f;
                }
            }
            else
            {
                y = Cross(pts, L.backStart, n - 1, x, false, out hitIndex);
            }
            return y;
        }

        /// <summary>y where the polyline pts[a..b] crosses x (decreasing x when !increasing).</summary>
        static float Cross(NativeSlice<float2> pts, int a, int b, float x, bool increasing, out float index)
        {
            index = a;
            if (b <= a) return pts[a].y;
            for (int k = a; k < b; k++)
            {
                float2 p = pts[k], q = pts[k + 1];
                bool inside = increasing ? (x >= p.x && x <= q.x) : (x <= p.x && x >= q.x);
                if (!inside) continue;
                float d = q.x - p.x;
                float f = math.abs(d) > 1e-5f ? (x - p.x) / d : 0f;
                index = k + f;
                return math.lerp(p.y, q.y, f);
            }
            // outside the branch: clamp to the nearest end
            bool beforeA = increasing ? x < pts[a].x : x > pts[a].x;
            index = beforeA ? a : b;
            return beforeA ? pts[a].y : pts[b].y;
        }
    }
}
