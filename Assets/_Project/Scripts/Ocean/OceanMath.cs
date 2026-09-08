using Unity.Burst;
using Unity.Mathematics;

namespace Biscotte.Ocean
{
    /// <summary>One Storm Breakers wave system, baked (see StormBreakers.Ocean.BakeAndPackWaveData).</summary>
    public struct OceanWaveSystem
    {
        public float wavelength, intensity, randomization, setNumber, wavenumber, groupSpeed, pulsation;
        public float3 directionVector, iVector, jVector;
    }

    /// <summary>Blittable copy of the Storm Breakers static ocean state, usable inside Burst jobs.</summary>
    public struct OceanParams
    {
        public OceanWaveSystem s0, s1, s2, s3;
        public float breakSpeedFactor;
        public int useTerrain;
        public int valid;

        public OceanWaveSystem Get(int i)
        {
            switch (i)
            {
                case 0: return s0;
                case 1: return s1;
                case 2: return s2;
                default: return s3;
            }
        }
    }

    /// <summary>
    /// Faithful Burst port of StormBreakers.Ocean.OceanDeformation / GetHeight / GetNormal / GetVelocity
    /// (CC0, https://github.com/Stormrider31/Storm-Breakers). Same constants, same order of operations,
    /// so CPU gameplay sampling matches the ocean vertex shader. Parity test: Biscotte.Tests.EditMode.OceanMathParityTests.
    /// </summary>
    [BurstCompile]
    public static class OceanMath
    {
        public static float3 Deformation(in OceanParams p, float time, in float3 undeformedPosition, float groundDepth, out float3 breakingVelocity)
        {
            float3 oceanDeformation = float3.zero;
            float3 localAmplitude = float3.zero;
            breakingVelocity = float3.zero;
            if (p.valid == 0) return oceanDeformation;

            for (int w = 0; w < 4; w++)
            {
                OceanWaveSystem s = p.Get(w);
                if (s.wavelength <= 0f) continue;

                float systemPosition = -time * s.groupSpeed;
                float3 worldAxisPosition = undeformedPosition - s.directionVector * systemPosition;
                float Ai = math.dot(worldAxisPosition, s.iVector);
                float Aj = math.dot(worldAxisPosition, s.jVector);
                float im = math.floor(Ai) + 0.5f;
                float jm = math.floor(Aj) + 0.5f;
                float randomimjm = math.cos(33f * im + 53f * jm) * math.cos(20f * im + 48f * jm);
                float deltai = s.randomization * randomimjm;
                float randomjmim = math.cos(33f * jm + 53f * im) * math.cos(20f * jm + 48f * im);
                float deltaj = s.randomization * randomjmim;
                float distance = math.min(0.5f - math.abs(deltai), 0.5f - math.abs(deltaj));
                float ic = im + deltai;
                float jc = jm + deltaj;
                float ir = (ic - Ai) / distance;
                float jr = (jc - Aj) / distance;
                float relI = math.min(1f, math.abs(ir));
                float ai = 1f - relI * relI;
                float relJ = math.min(1f, math.abs(jr));
                float aj = 1f - relJ * relJ;
                bool isFrontOfTheGroup = (ir - jr) > 0f;
                float variation = 0.8f + 0.2f * math.cos(10f * randomjmim + 0.1f * s.pulsation * time);
                float phaseAdvance = ir + jr;
                phaseAdvance = variation * (1f - phaseAdvance * phaseAdvance);
                float unphasing = 10f * randomimjm;
                float unclampedAmplitude = ai * aj * ai * aj * 2f * distance * variation;
                unclampedAmplitude *= s.intensity;

                float3 orientedAmplitude = s.directionVector * unclampedAmplitude;
                orientedAmplitude.x = math.clamp(orientedAmplitude.x, -1f - localAmplitude.x, 1f - localAmplitude.x);
                orientedAmplitude.z = math.clamp(orientedAmplitude.z, -1f - localAmplitude.z, 1f - localAmplitude.z);

                float phase = math.dot(undeformedPosition, s.directionVector) * s.wavenumber + time * s.pulsation + unphasing + s.intensity * phaseAdvance;
                float clampedAmplitude = unclampedAmplitude > 1f ? 1f : unclampedAmplitude;
                float relativePhase = phase / math.PI - 1f;
                relativePhase = relativePhase - 2f * math.floor(relativePhase * 0.5f) - 1f;
                relativePhase = math.abs(relativePhase);

                float verticalAmplitude = 0.085f * s.wavelength;
                if (p.useTerrain != 0)
                {
                    float shallowWaterAmplitude = 0.1f * verticalAmplitude + 0.5f * groundDepth;
                    shallowWaterAmplitude = shallowWaterAmplitude < 0f ? 0f : shallowWaterAmplitude;
                    float clampingFactor = 1f - 0.5f * groundDepth / verticalAmplitude;
                    clampingFactor = math.clamp(clampingFactor, 0f, 1f);
                    if (shallowWaterAmplitude < verticalAmplitude) verticalAmplitude = shallowWaterAmplitude;
                    unclampedAmplitude += clampingFactor;
                }

                float deltav = verticalAmplitude * (0.2f + math.cos(math.PI * math.pow(relativePhase, 1f + clampedAmplitude * 0.5f)));
                float deltah = -0.12f * s.wavelength * math.sin(phase);

                if (unclampedAmplitude > 0.85f && deltah > 0f && isFrontOfTheGroup)
                {
                    float scaleij = s.setNumber * s.wavelength;
                    float3 centerGroupPosition = scaleij * scaleij * (ic * s.iVector + jc * s.jVector) + systemPosition * s.directionVector;
                    float centerGroupPhase = math.dot(centerGroupPosition, s.directionVector) * s.wavenumber + time * s.pulsation + unphasing + s.intensity * variation + 0.7f;
                    if (math.sin(centerGroupPhase) > 0f)
                    {
                        breakingVelocity -= s.groupSpeed * (unclampedAmplitude - 0.85f) * p.breakSpeedFactor * s.directionVector;
                        breakingVelocity.y += (unclampedAmplitude - 0.85f) * s.wavelength * 0.01f;
                    }
                }

                float3 waveDeformation = math.length(orientedAmplitude) * new float3(0f, 1f, 0f) * deltav + orientedAmplitude * deltah;
                localAmplitude += orientedAmplitude;
                oceanDeformation += waveDeformation;
            }
            return oceanDeformation;
        }

        /// <summary>Height of the water below/above 'position'. 'undeformed' is a persistent per-caller cache (init with position).</summary>
        public static float GetHeight(in OceanParams p, float time, in float3 position, ref float3 undeformed, out float3 deformation, float groundDepth)
        {
            if (p.valid == 0) { deformation = float3.zero; return 0f; }
            deformation = Deformation(p, time, undeformed, groundDepth, out _);
            float deltaX = position.x - deformation.x - undeformed.x;
            float deltaZ = position.z - deformation.z - undeformed.z;
            undeformed.x += deltaX;
            undeformed.z += deltaZ;
            if (deltaX > 1f || deltaX < -1f || deltaZ > 1f || deltaZ < -1f)
            {
                deformation = Deformation(p, time, undeformed, groundDepth, out _);
                deltaX = position.x - deformation.x - undeformed.x;
                deltaZ = position.z - deformation.z - undeformed.z;
                undeformed.x += deltaX;
                undeformed.z += deltaZ;
            }
            deformation = Deformation(p, time, undeformed, groundDepth, out _);
            return deformation.y;
        }

        /// <summary>Quick height (two iterations from the given position, no cache).</summary>
        public static float QuickHeight(in OceanParams p, float time, in float3 position, float groundDepth)
        {
            float3 und = new float3(position.x, 0f, position.z);
            return GetHeight(p, time, position, ref und, out _, groundDepth);
        }

        public static float3 GetNormal(in OceanParams p, float time, in float3 undeformedPosition, in float3 alreadyComputedDeformation, float groundDepth, float precision)
        {
            if (p.valid == 0) return new float3(0f, 1f, 0f);
            float3 undeformedForwardPoint = undeformedPosition + new float3(0f, 0f, precision);
            float3 undeformedLeftPoint = undeformedPosition + new float3(precision, 0f, 0f);
            float3 v1 = undeformedPosition + alreadyComputedDeformation - undeformedForwardPoint - Deformation(p, time, undeformedForwardPoint, groundDepth, out _);
            float3 v2 = undeformedPosition + alreadyComputedDeformation - undeformedLeftPoint - Deformation(p, time, undeformedLeftPoint, groundDepth, out _);
            float3 n = math.cross(v1, v2);
            float len = math.length(n);
            if (len < 1e-6f) return new float3(0f, 1f, 0f);
            n /= len;
            return n.y < 0f ? -n : n;
        }

        public static float3 GetVelocity(in OceanParams p, float time, in float3 undeformedPosition, in float3 alreadyComputedDeformation, float groundDepth, float dt)
        {
            if (p.valid == 0) return float3.zero;
            float3 current = undeformedPosition + alreadyComputedDeformation;
            float3 future = undeformedPosition + Deformation(p, time + dt, undeformedPosition, groundDepth, out float3 breakVelocity);
            breakVelocity.y = 0f;
            return (future - current) / dt + breakVelocity;
        }
    }
}
