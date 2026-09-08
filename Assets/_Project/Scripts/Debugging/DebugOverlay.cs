using Biscotte.InputSys;
using Biscotte.Rider;
using Biscotte.Wave;
using UnityEngine;

namespace Biscotte.Debugging
{
    /// <summary>Dev HUD (IMGUI): state, speed, wave phase, tube, score counters. Toggle with F1 / Select. N / D-pad up spawns a wave.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        public RiderController rider;
        public WaveSetScheduler scheduler;
        public bool visible = true;

        float fps;
        GUIStyle style, big, hint;

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), 0.08f);
            var input = InputRouter.Instance;
            if (input == null) return;
            if (input.ConsumeDebug()) visible = !visible;
            if (input.ConsumeSpawn() && scheduler != null) scheduler.SpawnNow();
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
                style.normal.textColor = Color.white;
                big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = Color.white;
                hint = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            }
            if (rider == null) return;
            var s = rider.Sample;
            float speedKmh = rider.Speed * 3.6f;

            // main HUD (always on)
            GUI.Label(new Rect(20, Screen.height - 70, 300, 40), $"{speedKmh:0} km/h", big);
            string tube = rider.InTube ? $"  TUBE {rider.TubeTime:0.0}s" : "";
            GUI.Label(new Rect(20, Screen.height - 105, 600, 30), $"<b>{rider.State}</b>{tube}   vagues {rider.WavesRidden}   airs {rider.AirsLanded}   wipeouts {rider.Wipeouts}", style);
            if (Time.time - rider.LastEventTime < 1.6f)
                GUI.Label(new Rect(Screen.width * 0.5f - 250, Screen.height * 0.22f, 500, 40), rider.LastEvent, big);

            bool pad = InputRouter.Instance != null && InputRouter.Instance.UsingGamepad;
            string h = pad
                ? "Stick G : diriger/ramer   R2 : pump   L2 : stall   Croix : pop / kick-out (sprint en rame)   R1 : El Rollo   Stick D : rotation en l'air   Rond : duck dive   Triangle : reset   D-pad haut : vague"
                : "ZQSD/WASD : diriger/ramer   Shift : pump   Ctrl : stall   Espace : pop / kick-out (sprint en rame)   E : El Rollo   Souris : rotation en l'air   C : duck dive   R : reset   N : vague   F1 : debug";
            GUI.Label(new Rect(20, Screen.height - 28, Screen.width - 40, 24), h, hint);

            if (!visible) return;
            string txt =
                $"FPS {fps:0}  |  t {Time.time:0.0}\n" +
                $"pos ({rider.Position.x:0.0}, {rider.Position.y:0.00}, {rider.Position.z:0.0})  v {rider.Speed:0.00} m/s\n" +
                $"wave {s.WaveId}  phase {s.BreakPhase:0.00}  energy {s.Energy:0.00}  crestDist {s.CrestDistance:0.0}  peel {s.PeelDistance:0.0}\n" +
                $"H {s.WaveHeight:0.00}  lipW {s.LipWidth:0.00}  lipH {s.LipHeight:0.00}  faceW {s.FaceWidth:0.0}  ww {s.WhitewaterAmount:0.00}\n" +
                $"height {s.Height:0.00}  n.y {s.Normal.y:0.00}  inTube {s.InTube} depth {s.TubeDepth:0.00}\n" +
                $"ride {rider.RideTime:0.0}s  best {rider.BestRideTime:0.0}s  tube total {rider.TotalTubeTime:0.0}s  pumps {rider.PumpsThisRide}  DK {rider.DropKnee}\n" +
                $"last: {rider.LastEvent}  wipeout: {rider.LastWipeoutReason}";
            GUI.Box(new Rect(10, 10, 560, 150), "");
            GUI.Label(new Rect(18, 14, 550, 150), txt, style);
        }
    }
}
