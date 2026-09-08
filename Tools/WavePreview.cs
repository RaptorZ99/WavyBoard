using Biscotte.Ocean;
using Biscotte.Wave;
using UnityEditor;
using UnityEngine;

namespace Biscotte.Tools
{
    /// <summary>
    /// Edit-mode wave preview (no Play mode): builds one surf wave mesh at a fixed wave time in the open Playground scene,
    /// parks the main camera on the beach side of the breaking front and disables the Cinemachine brain so
    /// `unity command capture_game_view` shows the wave. Cleanup removes the preview and re-enables the brain.
    ///   unity command run_script --file Tools/WavePreview.cs --entry Biscotte.Tools.WavePreview.Setup --timeout_ms 120000 --timeout 200
    ///   unity command capture_game_view --save_path Screenshots~/preview.png
    ///   unity command run_script --file Tools/WavePreview.cs --entry Biscotte.Tools.WavePreview.Cleanup --timeout_ms 60000 --timeout 100
    /// Variants: SetupTube (camera inside the barrel), SetupFar (30 m back on the shoulder), SetupTop (high angle).
    /// </summary>
    public static class WavePreview
    {
        const string kName = "WavePreview";
        public static float WaveTime = 24f;

        public static string Setup() { return Build(WaveTime, 0); }
        public static string SetupTube() { return Build(WaveTime, 1); }
        public static string SetupFar() { return Build(WaveTime, 2); }
        public static string SetupTop() { return Build(WaveTime, 3); }
        public static string SetupWhitewater() { return Build(WaveTime + 14f, 4); }

        static string Build(float tw, int view)
        {
            var spot = Object.FindFirstObjectByType<SurfSpotConfig>();
            if (spot == null) return "no SurfSpotConfig in the open scene (open Playground first)";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/SurfWave.prefab");
            if (prefab == null) return "SurfWave prefab missing";

            var existing = GameObject.Find(kName);
            if (existing != null) Object.DestroyImmediate(existing);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = kName;
            var wave = go.GetComponent<SurfWave>();
            wave.BuildNow(spot, tw);

            // make sure the ocean statics + surf material are in sync in edit mode
            var oc = Object.FindFirstObjectByType<StormBreakers.OceanController>();
            if (oc != null) oc.UpdateWaves();
            var amb = Object.FindFirstObjectByType<OceanAmbient>();
            if (amb != null) amb.SyncMaterials();

            // camera on the beach side of the breaking front
            float sPeel = wave.PeelS();
            float crest = wave.Params.CrestOffset(tw);
            Vector3 T = spot.CrestDir, D = spot.TravelDir, O = spot.Origin;
            Vector3 front = O + T * sPeel + D * crest;
            Vector3 camPos, lookAt;
            switch (view)
            {
                case 1: camPos = front + T * -6f + D * 3f + Vector3.up * 1.4f; lookAt = front + T * 12f + D * 1f + Vector3.up * 1.2f; break;
                case 2: camPos = front + T * 8f + D * 30f + Vector3.up * 3.5f; lookAt = front + T * 20f + D * 2f + Vector3.up * 1.0f; break;
                case 3: camPos = front + T * 10f + D * 22f + Vector3.up * 16f; lookAt = front + T * 25f + D * -4f; break;
                case 4: camPos = front + T * -8f + D * 26f + Vector3.up * 5f; lookAt = front + T * -30f + D * -6f + Vector3.up * 0.5f; break;   // looking back at the broken section
                default: camPos = front + T * 4f + D * 14f + Vector3.up * 4f; lookAt = front + T * 14f + D * 0f + Vector3.up * 1.2f; break;
            }
            var cam = Camera.main;
            if (cam == null) return "no main camera";
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookAt - camPos, Vector3.up));
            cam.fieldOfView = 60f;
            SceneView.RepaintAll();
            var mr = go.GetComponent<MeshRenderer>();
            return $"preview tw={tw:0.0} peelS={sPeel:0.0} crest={crest:0.0} cam={camPos} mat={(mr.sharedMaterial != null ? mr.sharedMaterial.shader.name : "null")}\n{wave.VertexStats()}";
        }

        public static string Cleanup()
        {
            var existing = GameObject.Find(kName);
            if (existing != null) Object.DestroyImmediate(existing);
            var cam = Camera.main;
            if (cam != null)
            {
                var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null) brain.enabled = true;
            }
            return "preview removed";
        }
    }
}
