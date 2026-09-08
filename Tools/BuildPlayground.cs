using System.IO;
using Biscotte.CameraRig;
using Biscotte.Core;
using Biscotte.Debugging;
using Biscotte.InputSys;
using Biscotte.Ocean;
using Biscotte.Rider;
using Biscotte.Wave;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Biscotte.Tools
{
    // Builds Assets/_Project/Scenes/Playground.unity from scratch (spec P1/P3).
    // unity command run_script --file Tools/BuildPlayground.cs --entry Biscotte.Tools.BuildPlayground.Main --timeout_ms 600000
    public static class BuildPlayground
    {
        const string kScene = "Assets/_Project/Scenes/Playground.unity";
        const string kHdri = "Assets/ThirdParty/PolyHaven/hdris/secluded_beach/secluded_beach_4k.hdr";
        const string kOceanPrefab = "Assets/ThirdParty/StormBreakers/2-Prefabs & templates/Ocean.prefab";
        const string kWindPrefab = "Assets/ThirdParty/StormBreakers/2-Prefabs & templates/wind.prefab";
        const string kNormals = "Assets/ThirdParty/StormBreakers/5-Textures/WaveNormals.png";
        const string kFoam = "Assets/ThirdParty/StormBreakers/5-Textures/seaFoamFloatsOpaque.png";
        const string kActions = "Assets/_Project/Resources/Input/BiscotteActions.inputactions";
        const string kMat = "Assets/_Project/Art/Materials";
        const string kData = "Assets/_Project/Data";
        const string kPrefabs = "Assets/_Project/Prefabs";

        public static string Main()
        {
            var log = new System.Text.StringBuilder();
            Directory.CreateDirectory(kMat); Directory.CreateDirectory(kData); Directory.CreateDirectory(kPrefabs); Directory.CreateDirectory("Assets/_Project/Scenes");

            // ---------- materials
            var waterShader = Shader.Find("Biscotte/SurfWaveWater");
            if (waterShader == null) return "ERROR: shader Biscotte/SurfWaveWater not found";
            var nImp = AssetImporter.GetAtPath(kNormals) as TextureImporter;
            if (nImp != null && nImp.textureType != TextureImporterType.NormalMap) { nImp.textureType = TextureImporterType.NormalMap; nImp.SaveAndReimport(); log.Append("WaveNormals set to NormalMap. "); }
            var waterMat = LoadOrCreate<Material>(kMat + "/SurfWaveWater.mat", () => new Material(waterShader));
            waterMat.shader = waterShader;
            waterMat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(kNormals));
            waterMat.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(kFoam));
            waterMat.SetColor("_ShallowColor", new Color(0.03f, 0.36f, 0.42f));
            waterMat.SetColor("_DeepColor", new Color(0.008f, 0.06f, 0.12f));
            waterMat.SetFloat("_Smoothness", 0.86f);
            waterMat.SetFloat("_NormalStrength", 0.4f);
            waterMat.SetFloat("_SSSStrength", 1.8f);
            waterMat.SetFloat("_ShallowByHeight", 0.8f);
            EditorUtility.SetDirty(waterMat);

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            var sprayMat = LoadOrCreate<Material>(kMat + "/Particles_Spray.mat", () => new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            ConfigureParticleMaterial(sprayMat, "Assets/ThirdParty/Kenney/ParticlePack/Textures/circle_05.png", new Color(1f, 1f, 1f, 0.85f));
            var foamMat = LoadOrCreate<Material>(kMat + "/Particles_Foam.mat", () => new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            ConfigureParticleMaterial(foamMat, "Assets/ThirdParty/Kenney/ParticlePack/Textures/circle_04.png", new Color(1f, 1f, 1f, 0.7f));
            var boardMat = LoadOrCreate<Material>(kMat + "/Board_Deck.mat", () => new Material(litShader));
            boardMat.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.18f)); boardMat.SetFloat("_Smoothness", 0.65f); EditorUtility.SetDirty(boardMat);
            var riderMat = LoadOrCreate<Material>(kMat + "/Rider_Placeholder.mat", () => new Material(litShader));
            riderMat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.18f)); riderMat.SetFloat("_Smoothness", 0.4f); EditorUtility.SetDirty(riderMat);

            // ---------- data
            var tuning = LoadOrCreate<RiderTuning>(kData + "/RiderTuning.asset", () => ScriptableObject.CreateInstance<RiderTuning>());
            var boardSpec = LoadOrCreate<BoardSpec>(kData + "/Board_Standard.asset", () => ScriptableObject.CreateInstance<BoardSpec>());
            var boardMesh = LoadOrCreate<Mesh>(kData + "/Bodyboard_Procedural.asset", () => BodyboardMeshBuilder.Build(boardSpec.length, boardSpec.width, boardSpec.thickness));
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(kActions);
            if (actions == null) return "ERROR: input actions asset not found";

            // ---------- skybox / volume
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Dev/Sky_SecludedBeach.mat");
            if (skyMat == null)
            {
                var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(kHdri);
                skyMat = new Material(Shader.Find("Skybox/Cubemap")); skyMat.SetTexture("_Tex", cube);
                AssetDatabase.CreateAsset(skyMat, kMat + "/Sky_SecludedBeach.mat");
            }
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Dev/Mood_Volume.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, kData + "/Playground_Volume.asset");
                var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.12f); bloom.threshold.Override(1.1f);
                EditorUtility.SetDirty(profile);
            }

            // ---------- scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cam.fieldOfView = 58f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 4000f;
            camGo.transform.position = new Vector3(0f, 6f, -40f);
            camGo.AddComponent<CinemachineBrain>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.color = new Color(1f, 0.96f, 0.9f); light.intensity = 2.2f; light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.skybox = skyMat; RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.38f, 0.47f, 0.62f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox; RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f); RenderSettings.fogDensity = 0.0009f;

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile;

            // realtime reflection probe above the water: reflections then show sea + sky instead of the HDRI's beach ground
            var probeGo = new GameObject("Ocean Reflection Probe");
            probeGo.transform.position = new Vector3(0f, 6f, 120f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(4000f, 400f, 4000f);
            probe.boxProjection = false;
            probe.importance = 2;
            probe.hdr = true;
            probe.nearClipPlane = 1f; probe.farClipPlane = 3000f;
            probeGo.AddComponent<Biscotte.CameraRig.OceanReflectionProbeDriver>();

            // ---------- ambient ocean (Storm Breakers)
            var oceanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kOceanPrefab);
            if (oceanPrefab == null) return "ERROR: Ocean prefab missing";
            var ocean = (GameObject)PrefabUtility.InstantiatePrefab(oceanPrefab); ocean.name = "Ocean";
            var rend = ocean.GetComponentInChildren<MeshRenderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var oceanMat = LoadOrCreate<Material>(kMat + "/Ocean_Ambient.mat", () => new Material(rend.sharedMaterial));
                rend.sharedMaterial = oceanMat;
                rend.shadowCastingMode = ShadowCastingMode.Off;   // water must not cast hard polygonal shadows on itself
                rend.receiveShadows = true;
            }
            var oc = ocean.GetComponentInChildren<StormBreakers.OceanController>();
            if (oc != null)
            {
                oc.waterColor = new Color(0.03f, 0.12f, 0.16f);
                oc.waveIntensity = 1f;
                oc.wavelength0 = 26f; oc.intensity0 = 0.6f; oc.direction0 = -90f; oc.setNumber0 = 3.0f; oc.waveDensity0 = 0.9f;
                oc.wavelength1 = 11f; oc.intensity1 = 0.55f; oc.direction1 = -78f; oc.setNumber1 = 1.8f; oc.waveDensity1 = 0.85f;
                oc.wavelength2 = 5f; oc.intensity2 = 0.5f; oc.direction2 = -105f; oc.setNumber2 = 1.5f; oc.waveDensity2 = 0.8f;
                oc.wavelength3 = 1.6f; oc.intensity3 = 0.75f; oc.direction3 = -85f; oc.setNumber3 = 1.5f; oc.waveDensity3 = 0.75f;
            }
            // Storm Breakers breaker VFX disabled for now (dark quads under headless capture, and the surf wave has its own effects)
            foreach (var vfx in ocean.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true)) vfx.enabled = false;
            var windPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kWindPrefab);
            if (windPrefab != null) { var wind = (GameObject)PrefabUtility.InstantiatePrefab(windPrefab); wind.name = "Wind"; }

            // ---------- surf spot
            var spotGo = new GameObject("SurfSpot_Baie");
            spotGo.transform.position = new Vector3(-60f, 0f, 0f);
            var spot = spotGo.AddComponent<SurfSpotConfig>();
            spot.travelYawDeg = 0f;

            // ---------- surf wave prefab
            var swGo = new GameObject("SurfWave");
            var swMf = swGo.AddComponent<MeshFilter>();
            var swMr = swGo.AddComponent<MeshRenderer>();
            swMr.sharedMaterial = waterMat;
            swGo.AddComponent<SurfWave>();
            var wvfx = swGo.AddComponent<Biscotte.VFX.WaveVfx>(); wvfx.sprayMaterial = sprayMat; wvfx.foamMaterial = foamMat;
            var swPrefab = PrefabUtility.SaveAsPrefabAsset(swGo, kPrefabs + "/SurfWave.prefab");
            Object.DestroyImmediate(swGo);

            // ---------- rider prefab
            var riderGo = new GameObject("Rider");
            var visual = new GameObject("Visual"); visual.transform.SetParent(riderGo.transform, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body"; body.transform.SetParent(visual.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.8f, 0.3f);
            body.transform.localPosition = new Vector3(0f, 0.17f, -0.1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = riderMat;
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.name = "Head"; head.transform.SetParent(visual.transform, false);
            head.transform.localScale = Vector3.one * 0.24f; head.transform.localPosition = new Vector3(0f, 0.3f, 0.78f);
            head.GetComponent<MeshRenderer>().sharedMaterial = riderMat;
            var boardGo = new GameObject("Board"); boardGo.transform.SetParent(visual.transform, false);
            boardGo.transform.localPosition = new Vector3(0f, 0.03f, 0.12f);
            var bmf = boardGo.AddComponent<MeshFilter>(); bmf.sharedMesh = boardMesh;
            var bmr = boardGo.AddComponent<MeshRenderer>(); bmr.sharedMaterial = boardMat;
            var camTarget = new GameObject("CameraTarget"); camTarget.transform.SetParent(riderGo.transform, false);
            var rc = riderGo.AddComponent<RiderController>();
            rc.tuning = tuning; rc.board = boardSpec; rc.visualRoot = visual.transform; rc.boardRoot = boardGo.transform; rc.cameraTarget = camTarget.transform;
            var rvfx = riderGo.AddComponent<Biscotte.VFX.RiderVfx>(); rvfx.rider = rc; rvfx.sprayMaterial = sprayMat; rvfx.foamMaterial = foamMat;
            var scorer = riderGo.AddComponent<Biscotte.Scoring.RideScorer>(); scorer.rider = rc;
            var riderPrefab = PrefabUtility.SaveAsPrefabAsset(riderGo, kPrefabs + "/Rider.prefab");
            Object.DestroyImmediate(riderGo);
            var riderInst = (GameObject)PrefabUtility.InstantiatePrefab(riderPrefab);
            var riderCtrl = riderInst.GetComponent<RiderController>();
            riderCtrl.spot = spot;
            riderInst.transform.position = spot.LineupPosition();

            // ---------- managers
            var managers = new GameObject("Managers");
            managers.AddComponent<WaveClockDriver>();
            var amb = managers.AddComponent<OceanAmbient>();
            SetPrivate(amb, "controller", oc);
            managers.AddComponent<WaterSurfaceComposite>();
            var input = managers.AddComponent<InputRouter>();
            SetPrivate(input, "actions", actions);
            var sched = managers.AddComponent<WaveSetScheduler>();
            sched.spot = spot; sched.wavePrefab = swPrefab.GetComponent<SurfWave>(); sched.poolSize = 4; sched.setInterval = 42f; sched.wavesPerSet = 2; sched.wavePeriod = 12f; sched.firstSetDelay = 5f;
            var director = managers.AddComponent<CameraDirector>();
            director.rider = riderCtrl; director.cameraTarget = riderInst.transform.Find("CameraTarget");
            var overlay = managers.AddComponent<Biscotte.Debugging.DebugOverlay>();
            overlay.rider = riderCtrl; overlay.scheduler = sched; overlay.scorer = riderInst.GetComponent<Biscotte.Scoring.RideScorer>();
            var audio = managers.AddComponent<Biscotte.Audio.AudioDirector>();
            audio.rider = riderCtrl;
            audio.ambienceLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/OpenGameArt/Audio/ocean_ambience_loop_57s_generated.wav");
            audio.splashClips = new[] {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/OpenGameArt/Audio/beach_wave_01_cc0_jasinski.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/OpenGameArt/Audio/beach_wave_02_cc0_jasinski.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/OpenGameArt/Audio/beach_wave_03_cc0_jasinski.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/OpenGameArt/Audio/beach_wave_04_cc0_jasinski.wav") };

            // (shore rocks come back in the environment phase once they have Mesh LODs; the raw Poly Haven scans are 0.5-2M tris)

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, kScene);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool has = false; foreach (var s in scenes) if (s.path == kScene) has = true;
            if (!has) { scenes.Insert(0, new EditorBuildSettingsScene(kScene, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            return "Playground built: " + kScene + " | " + log;
        }

        static void ConfigureParticleMaterial(Material m, string texPath, Color tint)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Kenney/ParticlePack/Textures/circle_05.png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.SetFloat("_SoftParticlesEnabled", 1f);
            m.EnableKeyword("_SOFTPARTICLES_ON");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
        }

        static void PlaceRock(string path, Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static T LoadOrCreate<T>(string path, System.Func<T> factory) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = factory();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void SetPrivate(Object target, string field, Object value)
        {
            var fi = target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (fi != null) fi.SetValue(target, value);
            else Debug.LogWarning($"field {field} not found on {target}");
            EditorUtility.SetDirty(target);
            var so = new SerializedObject(target);
            so.Update();
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            so.Dispose();
        }
    }
}
