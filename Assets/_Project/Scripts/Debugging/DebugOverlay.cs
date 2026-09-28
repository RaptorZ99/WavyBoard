using WavyBoard.InputSys;
using WavyBoard.Rider;
using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.Debugging
{
    /// <summary>
    /// Developer panel, hidden by default: F1 (or Select) toggles it, N (or D-pad up) brings a wave in.
    /// Numbers only — the player-facing HUD is <see cref="WavyBoard.UI.GameHud"/>, and the two never overlap.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        public RiderController rider;
        public WaveSetScheduler scheduler;
        public bool visible;

        float fps;
        GUIStyle style;
        WavyBoard.CameraRig.CameraDirector director;

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
            if (!visible || rider == null) return;
            if (director == null) director = FindAnyObjectByType<WavyBoard.CameraRig.CameraDirector>();
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                style.normal.textColor = Color.white;
            }
            var s = rider.Sample;
            var input = InputRouter.Instance;
            string stick = input != null ? input.Stick.x.ToString("0.00") + ", " + input.Stick.y.ToString("0.00") : "-";
            string txt =
                $"FPS {fps:0}   t {Time.time:0.0}\n" +
                $"pos ({rider.Position.x:0.0}, {rider.Position.y:0.00}, {rider.Position.z:0.0})   world {rider.Speed:0.00} m/s   face {rider.FaceSpeed:0.00} m/s\n" +
                $"wave {s.WaveId}   phase {s.BreakPhase:0.00}   energy {s.Energy:0.00}   crestDist {s.CrestDistance:0.0}   peel {s.PeelDistance:0.0} @ {s.PeelSpeed:0.0} m/s\n" +
                $"line {rider.LineSpeed:0.0} m/s   pace {rider.PaceTarget:0.0} m/s   power {rider.Power:0.00}   along the crest {Vector3.Dot(rider.Velocity, (Vector3)s.CrestDir):0.0} m/s\n" +
                $"H {s.WaveHeight:0.00}   lipW {s.LipWidth:0.00}   lipH {s.LipHeight:0.00}   faceW {s.FaceWidth:0.0}   ww {s.WhitewaterAmount:0.00}\n" +
                $"height {s.Height:0.00}   n.y {s.Normal.y:0.00}   tube {s.InTube} ({s.TubeDepth:0.00})   roof {s.HasLipRoof}\n" +
                $"state {rider.State}   zone {rider.Zone}   grip {rider.Engaged:0.00}   ride {rider.RideTime:0.0} s   tube total {rider.TotalTubeTime:0.0} s   DK {rider.DropKnee}\n" +
                $"stick ({stick})   arc {(input != null ? input.Recognizer.Arc.ToString("0") : "-")}   load {(input != null ? input.Loaded.ToString("0.00") : "-")}\n" +
                $"trick {rider.Tricks.Current}   yaw {rider.Tricks.Yaw:0}  pitch {rider.Tricks.Pitch:0}  roll {rider.Tricks.Roll:0}   air peak {rider.AirPeak:0.0}\n" +
                $"last: {rider.LastEvent}   chute: {rider.LastWipeoutReason}   camera: {(director != null ? director.ShotName + " (" + director.ArmState + ")" : "-")}";
            GUI.Box(new Rect(10, 10, 640, 198), "");
            GUI.Label(new Rect(18, 14, 630, 198), txt, style);
        }
    }
}
