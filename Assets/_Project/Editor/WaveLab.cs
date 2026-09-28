using Biscotte.Ocean;
using Biscotte.Wave;
using UnityEditor;
using UnityEngine;

namespace Biscotte.EditorTools
{
    /// <summary>
    /// Edit-mode wave photography: builds one surf wave at a chosen moment of its peel in the open scene and parks the
    /// main camera on a named shot, so `unity command capture_game_view` shows it without entering Play mode.
    ///
    ///   unity command eval --code 'return Biscotte.EditorTools.WaveLab.Shot("channel", 40f, 1.2f);'
    ///   unity command capture_game_view --save_path Assets/Screenshots~/wave_channel.png
    ///   unity command eval --code 'return Biscotte.EditorTools.WaveLab.Cleanup();'
    ///
    /// Shots: channel (side view from the channel, like the classic Teahupoo photo), tube (inside the barrel, looking
    /// out), mouth (in front of the tube, looking in), back (from the sea side), top (aerial of the peel), rider (the
    /// ride camera), whitewater (the broken part behind), lineup (the set coming in, from the take-off spot).
    /// </summary>
    public static class WaveLab
    {
        const string kName = "WaveLab";
        const string kPrefab = "Assets/_Project/Prefabs/SurfWave.prefab";

        [MenuItem("Biscotte/Wave Lab/Channel shot")] static void MenuChannel() => Debug.Log(Shot("channel", 40f, 1.2f));
        [MenuItem("Biscotte/Wave Lab/Tube shot")] static void MenuTube() => Debug.Log(Shot("tube", 40f, 1.2f));
        [MenuItem("Biscotte/Wave Lab/Clean up")] static void MenuCleanup() => Debug.Log(Cleanup());

        /// <summary>Builds the wave with its curl at crest coordinate <paramref name="peelS"/> and frames the shot.</summary>
        public static string Shot(string view, float peelS, float heightScale, float swellTime = 12f)
        {
            var spot = Object.FindAnyObjectByType<SurfSpotConfig>();
            if (spot == null) return "no SurfSpotConfig in the open scene";
            var wave = EnsureWave();
            if (wave == null) return "SurfWave prefab missing";
            var ocean = Object.FindAnyObjectByType<OceanSurface>();
            if (ocean != null) { ocean.previewTime = swellTime; ocean.Rebake(); ocean.Publish(); }

            var aim = spot.LineupPosition();
            var p = spot.BuildParams(999, heightScale, aim);
            float tw = view == "lineup" ? p.firstBreakTime - 3f : p.firstBreakTime + spot.BreakDelay(peelS);
            wave.BuildNow(spot, tw, heightScale, aim);
            if (ocean != null) ocean.Publish();

            var P = wave.Params;
            Vector3 D = P.travelDir, T = P.crestDir, up = Vector3.up;
            float H = P.height;
            float crest = P.CrestOffset(tw);
            Vector3 curl = (Vector3)P.origin + T * peelS + D * crest;
            Vector3 camPos, look;
            float fov = 60f;
            switch (view)
            {
                case "channel":
                    camPos = curl + D * (7.5f * H) + T * (1.5f * H) + up * (0.25f * H);
                    look = curl - T * (2.5f * H) + D * (0.3f * H) + up * (0.55f * H);
                    fov = 50f;
                    break;
                case "tube":
                    camPos = curl - T * (4.5f * H) + D * (0.55f * H) + up * (0.32f * H);
                    look = curl - T * (1.2f * H) + D * (0.85f * H) + up * (0.3f * H);
                    fov = 78f;
                    break;
                case "mouth":
                    camPos = curl - T * (1.4f * H) + D * (2.6f * H) + up * (0.35f * H);
                    look = curl - T * (4f * H) + D * (0.3f * H) + up * (0.35f * H);
                    fov = 62f;
                    break;
                case "back":
                    camPos = curl - D * (6f * H) + T * (3f * H) + up * (1.3f * H);
                    look = curl - T * (2f * H) + up * (0.6f * H);
                    break;
                case "top":
                    camPos = curl + D * (7f * H) + T * (4f * H) + up * (7f * H);
                    look = curl - T * (4f * H);
                    fov = 55f;
                    break;
                case "rider":
                    camPos = curl - T * (3.5f * H) + D * (2.2f * H) + up * (0.9f * H);
                    look = curl - T * (0.5f * H) + D * (0.5f * H) + up * (0.4f * H);
                    fov = 62f;
                    break;
                case "whitewater":
                    camPos = curl + D * (5f * H) - T * (6f * H) + up * (1.4f * H);
                    look = curl - T * (14f * H) + up * (0.2f * H);
                    break;
                case "lineup":
                    camPos = aim - D * 2f + up * 1.6f;
                    look = aim - D * 60f + T * 12f + up * 1.5f;
                    fov = 65f;
                    break;
                default:
                    return "unknown view " + view;
            }

            var cam = Camera.main;
            if (cam == null) return "no main camera";
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos, up));
            cam.fieldOfView = fov;
            if (ocean != null) ocean.transform.position = new Vector3(camPos.x, 0f, camPos.z);
            SceneView.RepaintAll();
            return $"{view}: tw={tw:0.0} peelS={wave.PeelS(tw):0.0} H={H:0.0} c={P.celerity:0.0}\n{wave.VertexStats()}";
        }

        /// <summary>Samples the gameplay surface along a line across the wave at crest coordinate s (for graphs).</summary>
        public static string Transect(float s, float peelS, float heightScale, float xiFrom = -12f, float xiTo = 16f, float step = 0.5f)
        {
            var spot = Object.FindAnyObjectByType<SurfSpotConfig>();
            var wave = EnsureWave();
            if (spot == null || wave == null) return "missing spot or wave";
            var aim = spot.LineupPosition();
            var p = spot.BuildParams(999, heightScale, aim);
            float tw = p.firstBreakTime + spot.BreakDelay(peelS);
            wave.BuildNow(spot, tw, heightScale, aim);
            var P = wave.Params;
            double time = wave.SpawnTime + tw;
            var sb = new System.Text.StringBuilder("xi;height;phase;crestDist;inTube;roof;foam;energy\n");
            for (float xi = xiFrom; xi <= xiTo; xi += step)
            {
                Vector3 w = (Vector3)P.origin + (Vector3)P.crestDir * s + (Vector3)P.travelDir * (P.CrestOffset(tw) + xi);
                var r = wave.Sample(w, time);
                sb.Append($"{xi:0.0};{r.Height:0.00};{r.BreakPhase:0.00};{r.CrestDistance:0.0};{(r.InTube ? 1 : 0)};{(r.HasLipRoof ? r.LipRoofY : 0f):0.00};{r.WhitewaterAmount:0.00};{r.Energy:0.00}\n");
            }
            return sb.ToString();
        }

        static SurfWave EnsureWave()
        {
            var go = GameObject.Find(kName);
            if (go == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kPrefab);
                if (prefab == null) return null;
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = kName;
                go.hideFlags = HideFlags.DontSave;
            }
            return go.GetComponent<SurfWave>();
        }

        public static string Cleanup()
        {
            var go = GameObject.Find(kName);
            if (go != null) Object.DestroyImmediate(go);
            var cam = Camera.main;
            if (cam != null)
            {
                var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null) brain.enabled = true;
            }
            return "wave lab cleaned up";
        }
    }
}
