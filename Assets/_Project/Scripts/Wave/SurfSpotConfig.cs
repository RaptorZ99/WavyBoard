using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Biscotte.Wave
{
    /// <summary>
    /// A reef break in the Teahupoo mould: one peak, a left-to-right (along +T) peel of heavy barrel sections, a slower
    /// section where a rider can come out of the tube, and a shoulder that softens and backs off into the channel.
    /// Nothing breaks the other way from the peak: behind it the wave just crumbles and fades.
    /// </summary>
    public class SurfSpotConfig : MonoBehaviour
    {
        [Serializable]
        public struct Section
        {
            [Tooltip("Crest coordinate from the peak (m)")] public float s;
            [Tooltip("How fast the curl runs along the crest here (m/s): faster than the rider = he has to race it")] public float peelSpeed;
            [Tooltip("Height relative to the wave's height")] public float height;
            [Tooltip("0 mellow curl .. 1 thick Teahupoo lip and a square tube")] [Range(0f, 1f)] public float heavy;
            [Tooltip("0 throws a lip .. 1 just crumbles")] [Range(0f, 1f)] public float crumble;
        }

        [Header("Wave")]
        [Tooltip("Yaw (deg) of the travel direction D around world up; 0 = +Z")] public float travelYawDeg = 0f;
        [Tooltip("Crest height of a medium set wave above the sea (m)")] public float height = 3.4f;
        [Tooltip("Speed of the crest toward the beach for a medium wave (m/s)")] public float celerity = 7.5f;
        [Tooltip("Length of crest that breaks, from the peak (m)")] public float breakLength = 175f;
        [Tooltip("Behind the peak the wave crumbles and fades over this length (m)")] public float tailLength = 35f;
        [Tooltip("Past the last section the shoulder backs off into the channel over this length (m)")] public float endTaper = 45f;
        public float sPad = 10f;
        [Tooltip("Footprint of the wave mesh along D, relative to the crest (m): the trailing foam lives behind")] public float xiMin = -44f;
        public float xiMax = 30f;

        [Header("Timing (s)")]
        [Tooltip("From the spawn far out to the peak pitching")] public float breakLead = 16f;
        [Tooltip("The swell rises out of the sea")] public float spawnFade = 5f;
        [Tooltip("Round swell -> steep face, right before the first break")] public float shoalDuration = 7f;
        [Tooltip("Behind the peak, how fast the crumbling spreads (m/s)")] public float tailPeelSpeed = 9f;

        [Header("Sections (sorted by s)")]
        public List<Section> sections = new List<Section>
        {
            new Section { s = 0f, peelSpeed = 6.0f, height = 0.95f, heavy = 0.85f, crumble = 0f },     // the peak
            new Section { s = 25f, peelSpeed = 6.5f, height = 1.0f, heavy = 1f, crumble = 0f },        // the bowl
            new Section { s = 55f, peelSpeed = 8.0f, height = 1.05f, heavy = 1f, crumble = 0f },       // racy tube: pump or get caught
            new Section { s = 85f, peelSpeed = 5.0f, height = 1.0f, heavy = 0.9f, crumble = 0f },      // slower: come out, hit the lip
            new Section { s = 115f, peelSpeed = 6.3f, height = 0.93f, heavy = 0.85f, crumble = 0f },   // second tube
            new Section { s = 148f, peelSpeed = 5.5f, height = 0.78f, heavy = 0.5f, crumble = 0.25f }, // the shoulder softens
            new Section { s = 175f, peelSpeed = 4.5f, height = 0.55f, heavy = 0.2f, crumble = 0.85f }, // and crumbles into the channel
        };

        [Header("Line-up")]
        [Tooltip("Where the rider takes off, along the crest from the peak (m)")] public float lineupS = 6f;
        [Tooltip("How far seaward of the break line he waits (m)")] public float lineupBack = 4f;

        public float3 TravelDir { get { float a = math.radians(travelYawDeg); return new float3(math.sin(a), 0f, math.cos(a)); } }
        public float3 CrestDir => math.normalize(math.cross(new float3(0f, 1f, 0f), TravelDir));
        public float3 Origin => transform.position;

        /// <summary>Where the rider waits for the first set: just seaward of the peak.</summary>
        public Vector3 LineupPosition() => (Vector3)(Origin + CrestDir * lineupS - TravelDir * lineupBack);

        public Section SampleSection(float s)
        {
            if (sections.Count == 0) return new Section { s = s, peelSpeed = 6f, height = 1f, heavy = 1f };
            if (s <= sections[0].s) return sections[0];
            for (int i = 1; i < sections.Count; i++)
            {
                if (s > sections[i].s) continue;
                var a = sections[i - 1]; var b = sections[i];
                float t = math.smoothstep(0f, 1f, math.saturate((s - a.s) / math.max(1e-3f, b.s - a.s)));
                return new Section
                {
                    s = s, peelSpeed = math.lerp(a.peelSpeed, b.peelSpeed, t), height = math.lerp(a.height, b.height, t),
                    heavy = math.lerp(a.heavy, b.heavy, t), crumble = math.lerp(a.crumble, b.crumble, t),
                };
            }
            return sections[sections.Count - 1];
        }

        /// <summary>A wave of the given size aimed at a rider at <paramref name="aim"/>: its peak pitches just in front
        /// of him <see cref="breakLead"/> seconds after the spawn.</summary>
        public SurfWaveParams BuildParams(int id, float heightScale, Vector3 aim)
        {
            float3 D = TravelDir, T = CrestDir;
            float H = height * heightScale;
            float ts = math.clamp(math.sqrt(heightScale), 0.8f, 1.3f);
            float c = celerity * math.sqrt(math.clamp(heightScale, 0.5f, 2f));
            float3 origin = (float3)aim - T * lineupS + D * lineupBack;
            origin.y = 0f;
            var p = new SurfWaveParams
            {
                origin = origin, travelDir = D, crestDir = T,
                celerity = c,
                firstBreakTime = breakLead,
                crestStartOffset = -c * breakLead,
                timeScale = ts,
                height = H,
                breakLength = breakLength,
                endTaper = endTaper,
                sMin = -tailLength - sPad,
                sMax = breakLength + endTaper + sPad,
                xiMin = xiMin,
                xiMax = xiMax,
                spawnFade = spawnFade,
                shoalDuration = shoalDuration,
                id = id,
            };
            p.endTime = breakLead + BreakDelay(breakLength) + WaveProfile.TFlat * ts + 1f;
            return p;
        }

        /// <summary>Seconds after the peak that the crest at s starts to pitch.</summary>
        public float BreakDelay(float s)
        {
            if (s <= 0f) return -s / tailPeelSpeed;
            float d = 0f, ds = 1f;
            for (float x = 0f; x < s; x += ds) d += math.min(ds, s - x) / math.max(0.5f, SampleSection(x + 0.5f * ds).peelSpeed);
            return d;
        }

        /// <summary>Fills the per-crest-coordinate table (break delay, height, heavy, lipless) over [sMin, sMax].</summary>
        public void FillSections(NativeArray<float4> arr, in SurfWaveParams p)
        {
            int n = arr.Length;
            float delay = 0f, prevS = 0f;
            float endDelay = BreakDelay(breakLength);
            var last = SampleSection(breakLength);
            for (int i = 0; i < n; i++)
            {
                float s = math.lerp(p.sMin, p.sMax, (float)i / (n - 1));
                float4 v;
                if (s < 0f)
                {
                    var peak = SampleSection(0f);
                    float h = p.height * peak.height * math.smoothstep(-tailLength, -3f, s);
                    float lipless = 1f - math.smoothstep(-10f, -1f, s);
                    v = new float4(-s / tailPeelSpeed, h, peak.heavy * (1f - lipless), math.max(lipless, peak.crumble));
                }
                else if (s <= breakLength)
                {
                    if (s > prevS) { delay += (s - prevS) / math.max(0.5f, SampleSection(0.5f * (s + prevS)).peelSpeed); prevS = s; }
                    var k = SampleSection(s);
                    v = new float4(delay, p.height * k.height, k.heavy, k.crumble);
                }
                else
                {
                    // the channel: the break slows to a stop (continuous), the shoulder shrinks away
                    float e = s - breakLength;
                    float h = p.height * last.height * (1f - math.smoothstep(0f, endTaper, e));
                    v = new float4(endDelay + e / math.max(0.5f, last.peelSpeed) + e * e * 0.08f, h, last.heavy, math.max(last.crumble, math.smoothstep(0f, 12f, e)));
                }
                arr[i] = v;
            }
        }

        void OnDrawGizmos()
        {
            float3 o = Origin; float3 T = CrestDir; float3 D = TravelDir;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(o - T * tailLength, o + T * (breakLength + endTaper));
            Gizmos.color = Color.yellow;
            float prev = 0f;
            for (float s = 10f; s <= breakLength; s += 10f)
            {
                // where each point of the crest pitches, for a medium wave
                Gizmos.DrawLine(o + T * (s - 10f) + D * (celerity * prev), o + T * s + D * (celerity * (prev = BreakDelay(s))));
            }
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(LineupPosition(), 0.6f);
        }
    }
}
