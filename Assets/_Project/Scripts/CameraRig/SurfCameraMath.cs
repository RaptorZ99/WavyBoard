using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.CameraRig
{
    /// <summary>
    /// The geometry behind the surf camera, kept free of any MonoBehaviour so the same code frames the game and the
    /// edit-mode previews (WaveLab.TubeCam): the shot inside a barrel, and the spring arm that keeps a camera out of
    /// the water.
    /// </summary>
    public static class SurfCameraMath
    {
        [System.Serializable]
        public struct TubeShotSettings
        {
            [Tooltip("Distance behind the rider along the tube, per metre of wave height")] public float backPerH;
            public float backMin, backMax;
            [Tooltip("Camera height across the cavity (0 floor .. 1 ceiling)")] [Range(0.2f, 0.8f)] public float heightShare;
            [Tooltip("Never more than this above the rider: the camera looks along the tube, not down into it")] public float maxAboveRider;
            public float lookAhead;
            [Tooltip("Headroom (m) a slice needs to hold the camera")] public float minHeadroom;

            public static TubeShotSettings Default => new TubeShotSettings
            {
                backPerH = 0.85f, backMin = 2.2f, backMax = 4.4f, heightShare = 0.42f, maxAboveRider = 1.3f, lookAhead = 2.6f, minHeadroom = 1.1f,
            };
        }

        /// <summary>
        /// Which way along the crest the rider is riding, as the ride shot frames it (+1 = +T). It follows the board
        /// the moment the rider commits: nose pointed the other way along the line AND already moving that way, for a
        /// short while — the slide of a top turn, a wobble or the nose swinging through an air do not swing the camera,
        /// a cutback or a U-turn does, at once.
        /// </summary>
        public struct LineSide
        {
            public float Side;
            float timer;

            public void Reset(float side = 1f) { Side = side; timer = 0f; }

            /// <param name="headingT">the board's nose along the crest (-1..1)</param>
            /// <param name="vT">the rider's speed along the crest (m/s)</param>
            /// <returns>true when it switched side</returns>
            public bool Update(float headingT, float vT, float minSpeed, float delay, float dt)
            {
                float want = headingT >= 0f ? 1f : -1f;
                if (want == Side || Mathf.Abs(headingT) < 0.5f || vT * want < minSpeed) { timer = 0f; return false; }
                timer += dt;
                if (timer < delay) return false;
                Side = want;
                timer = 0f;
                return true;
            }
        }

        /// <summary>
        /// The camera inside the barrel, deeper than the rider along the tube (away from <paramref name="exitSide"/>,
        /// the way the mouth is) and looking out at the mouth. Tries the full distance first, then closer (the barrel
        /// may already have closed behind him); a candidate must be inside an open part of the cavity AND see the rider
        /// (the tube curves, the lip hangs lower in places).
        /// </summary>
        public static bool TryTubeShot(SurfWave wave, Vector3 anchor, float exitSide, float waveHeight, double time,
                                       in TubeShotSettings k, out Vector3 camPos, out Vector3 lookPos)
        {
            camPos = lookPos = anchor;
            if (wave == null) return false;
            Vector3 T = wave.Params.crestDir;
            wave.WaveCoords(anchor, time, out float s, out float xi, out float hA);
            float riderY = anchor.y - hA;
            float back = Mathf.Clamp(k.backPerH * waveHeight, k.backMin, k.backMax);
            for (int i = 0; i < 5; i++, back *= 0.75f)
            {
                if (back < 2f) break;   // closer than this, the rider's own body fills the frame
                if (!wave.TryTubeSlice(s - exitSide * back, xi, time, k.minHeadroom, out var sl)) continue;
                float y = sl.yFloor + k.heightShare * sl.Headroom;
                y = Mathf.Min(y, riderY + k.maxAboveRider);
                y = Mathf.Clamp(y, sl.yFloor + 0.5f, sl.yRoof - 0.4f);
                Vector3 p = wave.WaveToWorld(sl.s, sl.x, y, time);
                if (FreeLength(wave, anchor, p, time) < 0.85f * Vector3.Distance(anchor, p)) continue;
                camPos = p;
                // aim just ahead of the rider, drawn toward the middle of the tube ahead: the exit stays in the frame
                lookPos = anchor + T * (exitSide * k.lookAhead) + Vector3.up * 0.2f;
                if (wave.TryTubeSlice(s + exitSide * k.lookAhead * 1.5f, xi, time, 0.8f, out var ahead))
                {
                    Vector3 mid = wave.WaveToWorld(ahead.s, ahead.x, ahead.yFloor + 0.45f * ahead.Headroom, time);
                    lookPos = Vector3.Lerp(lookPos, mid, 0.35f);
                }
                return true;
            }
            return false;
        }

        /// <summary>How far from the pivot toward the target the line stays out of the water (minus a margin): one
        /// batch of probes every quarter metre along the line (a thrown lip can be thinner than half a metre).</summary>
        public static float FreeLength(IWaterProbe water, Vector3 pivot, Vector3 target, double time)
        {
            float len = Vector3.Distance(pivot, target);
            if (len < 1e-3f || water == null) return len;
            Vector3 dir = (target - pivot) / len;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 0.25f), 8, water.Capacity);
            float step = len / n;
            var pts = water.ProbePoints;
            for (int i = 0; i < n; i++) pts[i] = pivot + dir * (step * (i + 1));
            water.Probe(n, time);
            var res = water.ProbeResults;
            for (int i = 0; i < n; i++)
                if (res[i].inside) return Mathf.Max(0.5f, step * i - 0.3f);
            return len;
        }
    }
}
