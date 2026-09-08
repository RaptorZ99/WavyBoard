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

        // ---------------------------------------------------------------- material mirroring
        [Tooltip("Materials sharing the Storm Breakers ocean look (surf wave mesh): the dynamic ocean properties are mirrored every frame")]
        public Material[] syncMaterials;

        static readonly int[] kMatrixIds = { Shader.PropertyToID("_LIDR"), Shader.PropertyToID("_NKVW") };
        static readonly int[] kColorIds = { Shader.PropertyToID("_waterColor"), Shader.PropertyToID("_totalLigthColor"), Shader.PropertyToID("_groundColor") };
        static readonly int[] kFloatIds = { Shader.PropertyToID("_oceanIntensity"), Shader.PropertyToID("_underwater"), Shader.PropertyToID("_smoothness"), Shader.PropertyToID("_ripplesIntensity"), Shader.PropertyToID("_ripplesDirection") };
        static readonly int[] kVectorIds = { Shader.PropertyToID("_terrainPosition"), Shader.PropertyToID("_terrainScale") };
        static readonly int kTerrainTex = Shader.PropertyToID("_terrainHeightmap");

        void LateUpdate() { SyncMaterials(); }

        /// <summary>
        /// Copies the per-frame Storm Breakers material state (wave matrices, light, terrain) to the surf wave material(s).
        /// The wave matrices _LIDR/_NKVW are NON-exposed graph properties: Material.HasMatrix reports false for them
        /// and they are never serialized, so they are copied unconditionally (the CPU statics are the ground truth).
        /// </summary>
        public void SyncMaterials()
        {
            if (syncMaterials == null) return;
            var src = StormBreakers.Ocean.sharedMaterial;
            bool haveWaves = StormBreakers.Ocean.wavelength != null && StormBreakers.Ocean.wavelength.Length >= 4;
            for (int i = 0; i < syncMaterials.Length; i++)
            {
                var m = syncMaterials[i];
                if (m == null) continue;
                if (haveWaves)
                {
                    m.SetMatrix(kMatrixIds[0], StormBreakers.Ocean.LIDR);
                    m.SetMatrix(kMatrixIds[1], StormBreakers.Ocean.NKVW);
                }
                if (src == null) continue;
                foreach (int id in kColorIds) if (src.HasColor(id)) m.SetColor(id, src.GetColor(id));
                foreach (int id in kFloatIds) if (src.HasFloat(id)) m.SetFloat(id, src.GetFloat(id));
                foreach (int id in kVectorIds) if (src.HasVector(id)) m.SetVector(id, src.GetVector(id));
                if (src.HasTexture(kTerrainTex)) { var t = src.GetTexture(kTerrainTex); if (t != null) m.SetTexture(kTerrainTex, t); }
            }
        }
    }
}
