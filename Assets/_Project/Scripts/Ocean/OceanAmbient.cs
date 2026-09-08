using Unity.Mathematics;
using UnityEngine;

namespace Biscotte.Ocean
{
    /// <summary>
    /// Wraps the Storm Breakers OceanController: mirrors its static wave state into a blittable OceanParams
    /// every frame so jobs and gameplay can sample the ambient ocean with OceanMath.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class OceanAmbient : MonoBehaviour
    {
        public static OceanAmbient Instance { get; private set; }

        [SerializeField] StormBreakers.OceanController controller;
        [SerializeField] Terrain seabed;

        public OceanParams Params;
        public bool Ready => Params.valid != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        void Update() { Refresh(); }
        void FixedUpdate() { if (!Ready) Refresh(); }

        public void Refresh()
        {
            var wl = StormBreakers.Ocean.wavelength;
            if (wl == null || StormBreakers.Ocean.directionVector == null || wl.Length < 4)
            {
                Params.valid = 0;
                return;
            }
            Params.s0 = Read(0);
            Params.s1 = Read(1);
            Params.s2 = Read(2);
            Params.s3 = Read(3);
            Params.breakSpeedFactor = StormBreakers.Ocean.breakSpeedFactor;
            Params.useTerrain = StormBreakers.Ocean.useTerrain ? 1 : 0;
            Params.valid = 1;
            if (seabed == null && StormBreakers.Ocean.terrain != null) seabed = StormBreakers.Ocean.terrain;
        }

        static OceanWaveSystem Read(int w)
        {
            OceanWaveSystem s;
            s.wavelength = StormBreakers.Ocean.wavelength[w];
            s.intensity = StormBreakers.Ocean.intensity[w];
            s.randomization = StormBreakers.Ocean.randomization[w];
            s.setNumber = StormBreakers.Ocean.setNumber[w];
            s.wavenumber = StormBreakers.Ocean.wavenumber[w];
            s.groupSpeed = StormBreakers.Ocean.groupSpeed[w];
            s.pulsation = StormBreakers.Ocean.pulsation[w];
            s.directionVector = StormBreakers.Ocean.directionVector[w];
            s.iVector = StormBreakers.Ocean.iVector[w];
            s.jVector = StormBreakers.Ocean.jVector[w];
            return s;
        }

        /// <summary>Positive water depth to the seabed (200 m when no terrain), Storm Breakers convention.</summary>
        public float GroundDepth(float3 pos)
        {
            if (seabed == null) return 200f;
            return -seabed.SampleHeight(pos) - seabed.transform.position.y;
        }

        public float AmbientTime => Time.time;
    }
}
