using Biscotte.Wave;
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

        void Start()
        {
            // the waves are generated around the player, so the ocean has to be too: slide its tile grid along
            if (Application.isPlaying && controller != null && controller.GetComponent<OceanFollow>() == null)
                controller.gameObject.AddComponent<OceanFollow>();
        }
        void OnDisable()
        {
            if (Instance == this) Instance = null;
            // the hole rectangles live on the shared material asset: clear them so no hole is left in the plane after Play
            var mat = StormBreakers.Ocean.sharedMaterial;
            if (mat != null) for (int i = 0; i < kMaxHoles; i++) mat.SetVector(kRectB[i], kFar);
        }

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

        void LateUpdate() { SyncMaterials(); SyncHoles(); SyncReflections(); }

        // ---------------------------------------------------------------- reflections
        // The Storm Breakers graph samples its own cubemap textures (auto-named _SampleReflectedCubemap_*), whose original
        // assets are not in this project: left null they reflect a flat grey (the beige patches). Feed the scene reflection
        // probe (sky + shore + water) instead, every frame, into the ambient material and the surf wave material(s).
        [Tooltip("Reflection probe whose cubemap replaces the Storm Breakers reflection cubemaps (found automatically when empty)")]
        public ReflectionProbe reflectionProbe;
        public Texture fallbackReflection;
        readonly System.Collections.Generic.List<int> cubemapIds = new System.Collections.Generic.List<int>();
        Material cubemapIdsSource;

        public void SyncReflections()
        {
            var src = StormBreakers.Ocean.sharedMaterial;
            if (src == null) return;
            if (cubemapIdsSource != src)
            {
                cubemapIds.Clear();
                foreach (var name in src.GetTexturePropertyNames()) if (name.Contains("SampleReflectedCubemap")) cubemapIds.Add(Shader.PropertyToID(name));
                cubemapIdsSource = src;
            }
            if (cubemapIds.Count == 0) return;
            if (reflectionProbe == null) reflectionProbe = FindFirstObjectByType<ReflectionProbe>();
            Texture cube = null;
            if (reflectionProbe != null) cube = reflectionProbe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime ? reflectionProbe.realtimeTexture : reflectionProbe.texture;
            if (cube == null) cube = fallbackReflection;
            if (cube == null) return;
            foreach (int id in cubemapIds)
            {
                src.SetTexture(id, cube);
                if (syncMaterials != null) foreach (var m in syncMaterials) if (m != null) m.SetTexture(id, cube);
            }
        }

        // ---------------------------------------------------------------- holes in the ambient plane under the surf waves
        [Tooltip("Inset (m) of the alpha-clipped hole relative to the surf mesh border; the mesh ring overlapping the plane dips under it")]
        public float holeInset = 1f;
        const int kMaxHoles = 4;
        static readonly int[] kRectA = { Shader.PropertyToID("_SurfRectA0"), Shader.PropertyToID("_SurfRectA1"), Shader.PropertyToID("_SurfRectA2"), Shader.PropertyToID("_SurfRectA3") };
        static readonly int[] kRectB = { Shader.PropertyToID("_SurfRectB0"), Shader.PropertyToID("_SurfRectB1"), Shader.PropertyToID("_SurfRectB2"), Shader.PropertyToID("_SurfRectB3") };
        static readonly Vector4 kFar = new Vector4(1e9f, 1e9f, 1e9f, 1e9f);

        /// <summary>Feeds the OceanAmbientClip graph with the footprint rectangles of the active surf waves (wave-local s / d coordinates).</summary>
        public void SyncHoles()
        {
            var mat = StormBreakers.Ocean.sharedMaterial;
            if (mat == null) return;
            int n = 0;
            var water = WaterSurfaceComposite.Instance;
            var waves = water != null ? water.ActiveSurfWaves : null;
            if (waves != null)
            {
                for (int i = 0; i < waves.Count && n < kMaxHoles; i++)
                {
                    var w = waves[i];
                    if (w == null || !w.IsAlive) continue;
                    var P = w.Params;
                    float crest = w.CrestPositionAlongD;
                    mat.SetVector(kRectA[n], new Vector4(P.origin.x, P.origin.z, P.crestDir.x, P.crestDir.z));
                    mat.SetVector(kRectB[n], new Vector4(-P.sPad + holeInset, P.length + P.sPad - holeInset, crest + P.xiMin + holeInset, crest + P.xiMax - holeInset));
                    n++;
                }
            }
            for (; n < kMaxHoles; n++) mat.SetVector(kRectB[n], kFar);
        }

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
