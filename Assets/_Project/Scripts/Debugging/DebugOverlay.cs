using Biscotte.InputSys;
using Biscotte.Rider;
using Biscotte.Scoring;
using Biscotte.Wave;
using UnityEngine;

namespace Biscotte.Debugging
{
    /// <summary>Prototype HUD (IMGUI): speed, state, tricks/score popups, controls hint; F1 / Select toggles the debug panel. N / D-pad up spawns a wave.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        public RiderController rider;
        public WaveSetScheduler scheduler;
        public RideScorer scorer;
        public bool visible = false;

        float fps;
        GUIStyle style, big, huge, hint, score;

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), 0.08f);
            var input = InputRouter.Instance;
            if (input == null) return;
            if (input.ConsumeDebug()) visible = !visible;
            if (input.ConsumeSpawn() && scheduler != null) scheduler.SpawnNow();
            if (scorer == null && rider != null) scorer = rider.GetComponent<RideScorer>();
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true }; style.normal.textColor = Color.white;
                big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; big.normal.textColor = Color.white;
                huge = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; huge.normal.textColor = new Color(1f, 0.92f, 0.4f);
                hint = new GUIStyle(GUI.skin.label) { fontSize = 13 }; hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                score = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight }; score.normal.textColor = Color.white;
            }
            if (rider == null) return;
            var s = rider.Sample;
            float speedKmh = rider.Speed * 3.6f;

            // ---- main HUD
            GUI.Label(new Rect(20, Screen.height - 70, 300, 40), $"{speedKmh:0} km/h", big);
            string tube = rider.InTube ? $"  <color=#7ff>TUBE {rider.TubeTime:0.0}s</color>" : "";
            GUI.Label(new Rect(20, Screen.height - 105, 700, 30), $"<b>{StateLabel(rider.State)}</b>{tube}   vagues {rider.WavesRidden}   airs {rider.AirsLanded}   wipeouts {rider.Wipeouts}", style);
            if (Time.time - rider.LastEventTime < 1.4f && (scorer == null || Time.time - scorer.LastTrickTime > 0.1f))
                GUI.Label(new Rect(Screen.width * 0.5f - 300, Screen.height * 0.22f, 600, 40), rider.LastEvent, big);

            if (scorer != null)
            {
                if (Time.time - scorer.LastTrickTime < 1.8f)
                    GUI.Label(new Rect(Screen.width * 0.5f - 350, Screen.height * 0.14f, 700, 60), $"{scorer.LastTrick}  +{scorer.LastTrickPoints:0}", huge);
                if (Time.time - scorer.LastWaveScoreTime < 3f)
                    GUI.Label(new Rect(Screen.width * 0.5f - 350, Screen.height * 0.3f, 700, 60), $"NOTE {scorer.WaveScore:0.0}", huge);
                string chain = scorer.ChainCount > 0 ? $"  chaîne x{Mathf.Min(2f, Mathf.Pow(1.1f, scorer.ChainCount)):0.00}" : "";
                GUI.Label(new Rect(Screen.width - 420, 16, 400, 30), $"vague {scorer.WaveRawPoints + scorer.PendingPoints:0} pts{chain}", score);
                GUI.Label(new Rect(Screen.width - 420, 44, 400, 30), $"meilleures {scorer.Best1:0.0} + {scorer.Best2:0.0} = {scorer.Best1 + scorer.Best2:0.0}", score);
            }

            bool pad = InputRouter.Instance != null && InputRouter.Instance.UsingGamepad;
            string h = pad
                ? "Stick G : diriger/ramer   R2 : pump   L2 : stall   Croix : pop (lèvre) / kick-out (épaule) / sprint en rame   R1 : El Rollo   Stick D : rotation en l'air   Carré : grab   Rond : duck dive   Triangle : reset   D-pad haut : vague   Select : debug"
                : "ZQSD/WASD : diriger/ramer   Shift : pump   Ctrl : stall   Espace : pop (lèvre) / kick-out (épaule) / sprint en rame   E : El Rollo   Souris : rotation en l'air   F : grab   C : duck dive   R : reset   N : vague   F1 : debug";
            GUI.Label(new Rect(20, Screen.height - 28, Screen.width - 40, 24), h, hint);

            if (!visible) return;
            string txt =
                $"FPS {fps:0}  |  t {Time.time:0.0}\n" +
                $"pos ({rider.Position.x:0.0}, {rider.Position.y:0.00}, {rider.Position.z:0.0})  v {rider.Speed:0.00} m/s  rel {rider.RelVelocity.magnitude:0.00}\n" +
                $"wave {s.WaveId}  phase {s.BreakPhase:0.00}  energy {s.Energy:0.00}  crestDist {s.CrestDistance:0.0}  peel {s.PeelDistance:0.0}\n" +
                $"H {s.WaveHeight:0.00}  lipW {s.LipWidth:0.00}  lipH {s.LipHeight:0.00}  faceW {s.FaceWidth:0.0}  ww {s.WhitewaterAmount:0.00}\n" +
                $"height {s.Height:0.00}  n.y {s.Normal.y:0.00}  inTube {s.InTube} depth {s.TubeDepth:0.00}\n" +
                $"ride {rider.RideTime:0.0}s  best {rider.BestRideTime:0.0}s  tube total {rider.TotalTubeTime:0.0}s  pumps {rider.PumpsThisRide}  DK {rider.DropKnee}\n" +
                $"air spin {rider.AirSpin:0} flip {rider.AirFlip:0} roll {rider.AirRoll:0} peak {rider.AirPeak:0.0}\n" +
                $"last: {rider.LastEvent}  wipeout: {rider.LastWipeoutReason}";
            GUI.Box(new Rect(10, 10, 600, 170), "");
            GUI.Label(new Rect(18, 14, 590, 170), txt, style);
        }

        static string StateLabel(RiderState st)
        {
            switch (st)
            {
                case RiderState.Paddle: return "RAME";
                case RiderState.DuckDive: return "DUCK DIVE";
                case RiderState.TakeOff: return "TAKE-OFF";
                case RiderState.Ride: return "RIDE";
                case RiderState.Air: return "AIR";
                case RiderState.Wipeout: return "WIPEOUT";
                case RiderState.KickOut: return "KICK-OUT";
            }
            return st.ToString();
        }
    }
}
