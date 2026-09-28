using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WavyBoard.Tools
{
    // Builds a temporary "mood" preview scene: Poly Haven HDRI sky + Storm Breakers ocean + Poly Haven rocks + post volume.
    // Run with: unity command run_script --file Tools/BuildMoodScene.cs --entry WavyBoard.Tools.BuildMoodScene.Main --timeout_ms 600000
    public static class BuildMoodScene
    {
        const string kHdri = "Assets/ThirdParty/PolyHaven/hdris/secluded_beach/secluded_beach_4k.hdr";
        const string kOceanPrefab = "Assets/ThirdParty/StormBreakers/2-Prefabs & templates/Ocean.prefab";
        const string kWindPrefab = "Assets/ThirdParty/StormBreakers/2-Prefabs & templates/wind.prefab";
        const string kRock1 = "Assets/ThirdParty/PolyHaven/models/coast_rocks_03/coast_rocks_03_2k.gltf";
        const string kRock2 = "Assets/ThirdParty/PolyHaven/models/coast_land_rocks_04/coast_land_rocks_04_2k.gltf";
        const string kRock3 = "Assets/ThirdParty/PolyHaven/models/boulder_01/boulder_01_2k.gltf";
        const string kDevDir = "Assets/_Dev";
        const string kScene = kDevDir + "/Mood_Preview.unity";

        public static string Main()
        {
            Directory.CreateDirectory(kDevDir);
            var log = new System.Text.StringBuilder();

            // 1. HDRI -> cubemap
            var ti = AssetImporter.GetAtPath(kHdri) as TextureImporter;
            if (ti == null) return "HDRI importer not found: " + kHdri;
            if (ti.textureShape != TextureImporterShape.TextureCube || ti.maxTextureSize != 2048)
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                ti.sRGBTexture = false;
                ti.maxTextureSize = 2048;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
                log.Append("HDRI reimported as cubemap. ");
            }
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(kHdri);
            if (cube == null) return "Cubemap load failed";

            var skyMat = new Material(Shader.Find("Skybox/Cubemap"));
            skyMat.SetTexture("_Tex", cube);
            skyMat.SetFloat("_Exposure", 1.0f);
            skyMat.SetFloat("_Rotation", 0f);
            AssetDatabase.CreateAsset(skyMat, kDevDir + "/Sky_SecludedBeach.mat");

            // 2. Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            camGo.transform.position = new Vector3(-40f, 4.5f, -18f);
            camGo.transform.rotation = Quaternion.Euler(5f, 40f, 0f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 2.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.skybox = skyMat;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.fogDensity = 0.0010f;

            // 3. Ocean (Storm Breakers)
            var oceanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kOceanPrefab);
            if (oceanPrefab == null) return "Ocean prefab not found: " + kOceanPrefab;
            var ocean = (GameObject)PrefabUtility.InstantiatePrefab(oceanPrefab);
            ocean.name = "Ocean";
            var rend = ocean.GetComponentInChildren<MeshRenderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var oceanMat = new Material(rend.sharedMaterial);
                AssetDatabase.CreateAsset(oceanMat, kDevDir + "/Mood_Ocean.mat");
                rend.sharedMaterial = oceanMat;
                log.Append("Ocean material cloned. ");
            }
            var oc = ocean.GetComponent<StormBreakers.OceanController>();
            if (oc == null) oc = ocean.GetComponentInChildren<StormBreakers.OceanController>();
            if (oc != null)
            {
                oc.waterColor = new Color(0.03f, 0.12f, 0.16f);
                oc.waveIntensity = 1f;
                oc.wavelength0 = 42f; oc.intensity0 = 1.02f; oc.direction0 = 0f;  oc.setNumber0 = 3.2f; oc.waveDensity0 = 0.9f;
                oc.wavelength1 = 16f; oc.intensity1 = 0.85f; oc.direction1 = 12f; oc.setNumber1 = 1.8f; oc.waveDensity1 = 0.85f;
                oc.wavelength2 = 6f;  oc.intensity2 = 0.7f;  oc.direction2 = -18f; oc.setNumber2 = 1.5f; oc.waveDensity2 = 0.8f;
                oc.wavelength3 = 1.8f; oc.intensity3 = 0.9f; oc.direction3 = 6f;  oc.setNumber3 = 1.5f; oc.waveDensity3 = 0.75f;
                log.Append("OceanController configured. ");
            }
            else log.Append("WARNING: OceanController not found on prefab. ");

            var windPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kWindPrefab);
            if (windPrefab != null)
            {
                var wind = (GameObject)PrefabUtility.InstantiatePrefab(windPrefab);
                wind.name = "Wind";
            }

            // 4. Rocks (Poly Haven, glTFast prefabs)
            Place(kRock1, new Vector3(30f, -0.8f, 40f), 0f, 1f, "Headland Rocks", log);
            Place(kRock2, new Vector3(-6f, -1.2f, 58f), 140f, 1f, "Land Rocks", log);
            Place(kRock3, new Vector3(-28f, -0.6f, 24f), 30f, 1.4f, "Boulder", log);

            // 5. Post-processing volume
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, kDevDir + "/Mood_Volume.asset");
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.12f); bloom.threshold.Override(1.1f);
            var ca = profile.Add<ColorAdjustments>(true); ca.contrast.Override(8f); ca.saturation.Override(6f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.15f);
            vol.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            EditorSceneManager.SaveScene(scene, kScene);
            return "mood scene saved: " + kScene + " | " + log;
        }

        static void Place(string path, Vector3 pos, float yaw, float scale, string name, System.Text.StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { log.Append("missing " + path + " "); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
        }
    }
}
