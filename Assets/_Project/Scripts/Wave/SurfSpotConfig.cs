using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Biscotte.Wave
{
    /// <summary>Authoring data for one surf break: where the crest starts breaking along the wave (the "break line"), heights, tube intensity.</summary>
    public class SurfSpotConfig : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            public float s;            // crest coordinate (m)
            public float breakOffset;  // distance along D from the origin where breaking starts (m)
            public float height;       // H (m)
            public float intensity;    // vortex ratio 1.6 (mellow) .. 3 (slab)
        }

        [Header("Geometry")]
        [Tooltip("Yaw (deg) of the travel direction D around world up; 0 = +Z")] public float travelYawDeg = 0f;
        public float length = 150f;
        public float wavelength = 30f;
        public float celerity = 7f;
        public float crestStartOffset = -40f;
        public float maxTravel = 340f;
        public float sPad = 25f;
        public float xiMin = -32f;
        public float xiMax = 36f;

        [Header("Timing (s)")]
        public float shoalTime = 3f;
        public float pitchTime = 2.0f;
        public float collapseTime = 1.2f;
        public float decayTime = 9f;
        public float spawnFadeTime = 4f;
        public float endTaper = 14f;

        [Header("Break line (sorted by s)")]
        public List<Key> keys = new List<Key>
        {
            // peel angle ~61 deg (f' ~ 1.8) -> peel speed ~3.9 m/s at c = 7 m/s; overhead faces (2.6-3.2 m) with a hollow middle section
            new Key { s = 0f,   breakOffset = 40f,  height = 2.6f, intensity = 2.0f },
            new Key { s = 30f,  breakOffset = 95f,  height = 2.9f, intensity = 2.5f },
            new Key { s = 65f,  breakOffset = 160f, height = 3.2f, intensity = 2.9f },
            new Key { s = 100f, breakOffset = 225f, height = 3.0f, intensity = 2.4f },
            new Key { s = 130f, breakOffset = 280f, height = 2.6f, intensity = 2.0f },
            new Key { s = 150f, breakOffset = 300f, height = 2.0f, intensity = 1.7f }, // closing section
        };

        [Header("Lineup")]
        public float lineupS = 12f;
        public float lineupBack = 14f;

        public float3 TravelDir
        {
            get { float a = math.radians(travelYawDeg); return new float3(math.sin(a), 0f, math.cos(a)); }
        }
        public float3 CrestDir => math.normalize(math.cross(new float3(0f, 1f, 0f), TravelDir));
        public float3 Origin => transform.position;

        public Vector3 LineupPosition()
        {
            float f = SampleKey(lineupS).breakOffset;
            return (Vector3)(Origin + CrestDir * lineupS + TravelDir * (f - lineupBack));
        }

        public Key SampleKey(float s)
        {
            if (keys.Count == 0) return new Key { s = s, breakOffset = 40f, height = 1.8f, intensity = 2f };
            if (s <= keys[0].s) return keys[0];
            for (int i = 1; i < keys.Count; i++)
            {
                if (s <= keys[i].s)
                {
                    var a = keys[i - 1]; var b = keys[i];
                    float t = math.saturate((s - a.s) / math.max(1e-3f, b.s - a.s));
                    return new Key { s = s, breakOffset = math.lerp(a.breakOffset, b.breakOffset, t), height = math.lerp(a.height, b.height, t), intensity = math.lerp(a.intensity, b.intensity, t) };
                }
            }
            return keys[keys.Count - 1];
        }

        public void FillProfile(NativeArray<float4> arr, float heightScale)
        {
            int n = arr.Length;
            for (int i = 0; i < n; i++)
            {
                float s = length * i / (n - 1);
                var k = SampleKey(s);
                arr[i] = new float4(k.breakOffset, k.height * heightScale, k.intensity, 0f);
            }
        }

        public SurfWaveParams BuildParams(int id)
        {
            return new SurfWaveParams
            {
                origin = Origin, travelDir = TravelDir, crestDir = CrestDir,
                celerity = celerity, wavelength = wavelength, length = length,
                crestStartOffset = crestStartOffset,
                shoalTime = shoalTime, pitchTime = pitchTime, collapseTime = collapseTime, decayTime = decayTime,
                spawnFadeTime = spawnFadeTime, endTaper = endTaper, sPad = sPad, xiMin = xiMin, xiMax = xiMax, id = id
            };
        }

        void OnDrawGizmos()
        {
            float3 o = Origin; float3 T = CrestDir; float3 D = TravelDir;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(o, o + T * length);
            Gizmos.color = Color.yellow;
            for (int i = 1; i < keys.Count; i++)
            {
                var a = keys[i - 1]; var b = keys[i];
                Gizmos.DrawLine(o + T * a.s + D * a.breakOffset + new float3(0, a.height, 0), o + T * b.s + D * b.breakOffset + new float3(0, b.height, 0));
            }
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(LineupPosition(), 0.6f);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(o + D * crestStartOffset, o + T * length + D * crestStartOffset);
        }
    }
}
