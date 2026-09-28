using WavyBoard.Ocean;
using WavyBoard.Wave;
using UnityEditor;
using UnityEngine;

namespace WavyBoard.EditorTools
{
    /// <summary>
    /// Edit-mode wave photography: builds one surf wave at a chosen moment of its peel in the open scene and parks the
    /// main camera on a named shot, so `unity command capture_game_view` shows it without entering Play mode.
    ///
    ///   unity command eval --code 'return WavyBoard.EditorTools.WaveLab.Shot("channel", 40f, 1.2f);'
    ///   unity command capture_game_view --save_path Assets/Screenshots~/wave_channel.png
    ///   unity command eval --code 'return WavyBoard.EditorTools.WaveLab.Cleanup();'
    ///
    /// Shots: channel (side view from the channel, like the classic Teahupoo photo), tube (inside the barrel, looking
    /// out), mouth (in front of the tube, looking in), back (from the sea side), top (aerial of the peel), rider (the
    /// ride camera), whitewater (the broken part behind), lineup (the set coming in, from the take-off spot).
    /// </summary>
    public static class WaveLab
    {
        const string kName = "WaveLab";
        const string kPrefab = "Assets/_Project/Prefabs/SurfWave.prefab";

        [MenuItem("WavyBoard/Wave Lab/Channel shot")] static void MenuChannel() => Debug.Log(Shot("channel", 40f, 1.2f));
        [MenuItem("WavyBoard/Wave Lab/Tube shot")] static void MenuTube() => Debug.Log(Shot("tube", 40f, 1.2f));
        [MenuItem("WavyBoard/Wave Lab/Clean up")] static void MenuCleanup() => Debug.Log(Cleanup());

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
            Frame(cam, ocean, camPos, Quaternion.LookRotation(look - camPos, up), fov);
            SceneView.RepaintAll();
            return $"{view}: tw={tw:0.0} peelS={wave.PeelS(tw):0.0} H={H:0.0} c={P.celerity:0.0}\n{wave.VertexStats()}";
        }

        /// <summary>
        /// The game's tube camera, in edit mode: a stand-in rider in the barrel <paramref name="behindCurl"/> metres behind
        /// the curl, at <paramref name="acrossShare"/> of the way from the back wall to the lip tip, and the camera framed
        /// by the same code as in play (<see cref="WavyBoard.CameraRig.SurfCameraMath.TryTubeShot"/>).
        /// </summary>
        public static string TubeCam(float peelS, float heightScale, float behindCurl = 6f, float acrossShare = 0.4f, float side = 1f)
        {
            var spot = Object.FindAnyObjectByType<SurfSpotConfig>();
            if (spot == null) return "no SurfSpotConfig in the open scene";
            var wave = EnsureWave();
            var ocean = Object.FindAnyObjectByType<OceanSurface>();
            if (ocean != null) { ocean.Rebake(); ocean.Publish(); }
            var aim = spot.LineupPosition();
            var p = spot.BuildParams(999, heightScale, aim);
            float tw = p.firstBreakTime + spot.BreakDelay(peelS);
            wave.BuildNow(spot, tw, heightScale, aim);
            double time = wave.SpawnTime + tw;
            float s = wave.PeelS(tw) - side * behindCurl;
            if (!wave.TryTubeSlice(s, 0f, time, 0.5f, out var sl)) return $"no open barrel at s={s:0.0}";
            float x = Mathf.Lerp(sl.xWall, sl.xTip, acrossShare);
            Vector3 floor = wave.FacePointWorld(s, x);
            Vector3 anchor = floor + Vector3.up * 0.45f;

            var marker = GameObject.Find(kName + "_Rider");
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = kName + "_Rider";
                marker.hideFlags = HideFlags.DontSave;
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.localScale = new Vector3(0.5f, 0.45f, 0.5f);
            }
            marker.transform.position = floor + Vector3.up * 0.25f;

            var k = WavyBoard.CameraRig.SurfCameraMath.TubeShotSettings.Default;
            var director = Object.FindAnyObjectByType<WavyBoard.CameraRig.CameraDirector>();
            if (director != null) k = director.tubeShot;
            if (!WavyBoard.CameraRig.SurfCameraMath.TryTubeShot(wave, anchor, side, wave.Params.height, time, in k, out Vector3 camPos, out Vector3 lookPos))
                return $"tube shot refused at s={s:0.0} (headroom {sl.Headroom:0.0} m)";
            var cam = Camera.main;
            Frame(cam, ocean, camPos, Quaternion.LookRotation(lookPos - camPos, Vector3.up), director != null ? director.tubeFov : 80f);
            SceneView.RepaintAll();
            return $"tube cam: H={wave.Params.height:0.0} s={s:0.0} headroom={sl.Headroom:0.0} wall..tip={sl.xWall:0.0}..{sl.xTip:0.0} rider x={x:0.0} cam-rider {Vector3.Distance(camPos, anchor):0.0} m";
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

        // The shots move the scene's camera and ocean: their pose is kept for the session and put back by Cleanup,
        // so taking pictures never leaves the scene modified.
        const string kSaved = "WavyBoard.WaveLab.Saved";

        static void Frame(Camera cam, OceanSurface ocean, Vector3 pos, Quaternion rot, float fov)
        {
            if (!SessionState.GetBool(kSaved, false))
            {
                SessionState.SetBool(kSaved, true);
                SessionState.SetVector3(kSaved + ".camPos", cam.transform.position);
                SessionState.SetVector3(kSaved + ".camRot", cam.transform.eulerAngles);
                SessionState.SetFloat(kSaved + ".fov", cam.fieldOfView);
                SessionState.SetVector3(kSaved + ".ocean", ocean != null ? ocean.transform.position : Vector3.zero);
            }
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            if (ocean != null) ocean.transform.position = new Vector3(pos.x, 0f, pos.z);
        }

        static void RestoreFraming()
        {
            if (!SessionState.GetBool(kSaved, false)) return;
            SessionState.SetBool(kSaved, false);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(SessionState.GetVector3(kSaved + ".camPos", cam.transform.position),
                                                     Quaternion.Euler(SessionState.GetVector3(kSaved + ".camRot", cam.transform.eulerAngles)));
                cam.fieldOfView = SessionState.GetFloat(kSaved + ".fov", cam.fieldOfView);
            }
            var ocean = Object.FindAnyObjectByType<OceanSurface>();
            if (ocean != null) ocean.transform.position = SessionState.GetVector3(kSaved + ".ocean", ocean.transform.position);
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
            var marker = GameObject.Find(kName + "_Rider");
            if (marker != null) Object.DestroyImmediate(marker);
            RestoreFraming();
            return "wave lab cleaned up";
        }
    }
}
