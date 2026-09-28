using System.Text;
using WavyBoard.InputSys;
using WavyBoard.Rider;
using WavyBoard.Scoring;
using WavyBoard.Tricks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WavyBoard.UI
{
    /// <summary>
    /// The whole player-facing HUD, in one place: speed and grip bottom left, the board stick's own read-out beside
    /// it, session and wave score top right, the manoeuvres you just landed stacked in the middle, and a gesture
    /// guide that follows the device you are holding.
    ///
    /// Two things here are doing real work rather than decorating:
    ///
    /// <b>The stick ring.</b> The right stick is the entire trick vocabulary, so the player gets to SEE it being
    /// read — where the stick is, how loaded the crouch is, and the arc being wound round the rim. Learning a
    /// flick-it scheme without that feedback is learning in the dark.
    ///
    /// <b>The speed colour.</b> On a breaking wave the one thing that matters is whether you are ahead of the
    /// break or it is catching you. Rather than print a second number, the speed you already read turns warm
    /// when the curl is getting on top of you.
    ///
    /// Everything is cached; a steady frame allocates nothing.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public RiderController rider;
        public RideScorer scorer;

        [Tooltip("Show the gesture guide. Toggled live with H.")]
        public bool showControls = true;

        static readonly Color Bone = new Color(0.96f, 0.95f, 0.92f);
        static readonly Color Teal = new Color(0.50f, 0.82f, 0.78f);
        static readonly Color Sand = new Color(0.95f, 0.76f, 0.47f);
        static readonly Color Coral = new Color(0.91f, 0.47f, 0.35f);

        GUIStyle big, mid, small, trick, trickSmall, chip, legendKey, legendLabel;
        Texture2D panelTex, ringTex;
        bool stylesReady;

        // legend, rebuilt only when the active device changes
        bool builtPad, builtOnce;
        string legendLeft = "", legendRight = "";

        // the last few manoeuvres, so a linked sequence reads as a sequence
        struct Popup { public string text; public float time; }
        readonly Popup[] popups = new Popup[3];
        int popupCount;
        float lastSeenTrickTime = -10f;

        readonly StringBuilder sb = new StringBuilder(128);

        void Awake()
        {
            if (rider == null) rider = FindAnyObjectByType<RiderController>();
            if (scorer == null && rider != null) scorer = rider.GetComponent<RideScorer>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) showControls = !showControls;

            if (scorer != null && scorer.LastTrickTime > lastSeenTrickTime)
            {
                lastSeenTrickTime = scorer.LastTrickTime;
                for (int i = popups.Length - 1; i > 0; i--) popups[i] = popups[i - 1];
                popups[0] = new Popup
                {
                    text = scorer.LastTrick + "  <color=#7FD1C4>+" + Mathf.RoundToInt(scorer.LastTrickPoints) + "</color>",
                    time = Time.time,
                };
                popupCount = Mathf.Min(popups.Length, popupCount + 1);
            }
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            panelTex = Solid(new Color(0.04f, 0.07f, 0.09f, 0.55f));
            ringTex = Solid(new Color(1f, 1f, 1f, 0.16f));

            big = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, richText = true };
            big.normal.textColor = Bone;
            mid = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            mid.normal.textColor = Bone;
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            small.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            trick = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            trick.normal.textColor = Sand;
            trickSmall = new GUIStyle(trick) { fontSize = 24 };
            trickSmall.normal.textColor = new Color(0.95f, 0.76f, 0.47f, 0.7f);
            chip = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, richText = true };
            chip.normal.textColor = Bone;
            legendKey = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, richText = true };
            legendKey.normal.textColor = Teal;
            legendLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            legendLabel.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (rider == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            DrawSpeedPanel(h);
            DrawStickRing(h);
            DrawScorePanel(w);
            DrawPopups(w, h);

            string cue = Cue();
            if (cue != null)
            {
                var s = new GUIStyle(trick) { fontSize = 26 };
                s.normal.textColor = Teal;
                GUI.Label(new Rect(w * 0.5f - 400, h * 0.72f, 800, 40), cue, s);
            }

            if (showControls) DrawLegend(w, h);
        }

        // ---------------------------------------------------------------- bottom left: how you are going

        void DrawSpeedPanel(float h)
        {
            GUI.DrawTexture(new Rect(0, h - 132, 540, 132), panelTex);

            // World speed means something in every state. On a breaking wave it is coloured by where you are against
            // the break, so a glance says "you are ahead of it" or "it is catching you" without a second readout.
            var s = rider.Sample;
            bool racing = rider.State == RiderState.Ride && s.BreakPhase >= 0.6f;
            Color c = Bone;
            if (racing) c = s.PeelDistance > -3f ? Teal : Coral;
            var prev = big.normal.textColor;
            big.normal.textColor = c;
            GUI.Label(new Rect(24, h - 118, 300, 60), Mathf.RoundToInt(rider.Speed * 3.6f) + "<size=20> km/h</size>", big);
            big.normal.textColor = prev;

            GUI.Label(new Rect(24, h - 58, 510, 26), StateLine(), mid);
            GUI.Label(new Rect(24, h - 32, 510, 22), ZoneLine(), small);
        }

        string StateLine()
        {
            sb.Clear();
            sb.Append("<b>").Append(StateLabel(rider.State)).Append("</b>  ");
            // a bar, not a label: how much of you the wave has hold of, which is the whole ride in one number
            int filled = Mathf.RoundToInt(Mathf.Clamp01(rider.Engaged) * 8f);
            sb.Append("<color=#7FD1C4>").Append('|', filled).Append("</color>");
            sb.Append("<color=#3A4A52>").Append('|', 8 - filled).Append("</color>");
            if (rider.InTube) sb.Append("   <color=#7FD1C4>TUBE ").Append(rider.TubeTime.ToString("0.0")).Append(" s</color>");
            else if (rider.Sample.WhitewaterAmount > 0.45f) sb.Append("   <color=#E8785A>MOUSSE</color>");
            if (rider.Grabbing) sb.Append("   <color=#F2C078>GRAB</color>");
            return sb.ToString();
        }

        string ZoneLine()
        {
            switch (rider.Zone)
            {
                case WaveZone.Lip: return "<color=#F2C078>lèvre</color> — ↓↑ air · ↓↗ el rollo · ↓ puis tour = spin";
                case WaveZone.Face: return "<color=#F2C078>face</color> — monte vers la lèvre pour t'envoler · ↓↗ snap · quart de tour = cutback";
                case WaveZone.Tube: return "<color=#F2C078>tube</color> — reste dans la poche (freine pour t'enfoncer) · figures +40 %";
                case WaveZone.Air: return "<color=#F2C078>en l'air</color> — enroule = spin · ↓↗ rollo · stick G = tourner · ↑ vise la réception";
                default: return "stick gauche : tourne et rame · stick droit : ↓ puis ↑ = saut";
            }
        }

        // ---------------------------------------------------------------- the board stick, drawn as it is read

        void DrawStickRing(float h)
        {
            var input = InputRouter.Instance;
            if (input == null) return;
            var c = new Vector2(600f, h - 66f);
            GUI.DrawTexture(new Rect(c.x - 40f, c.y - 40f, 80f, 80f), panelTex);

            // the rim
            const int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                GUI.DrawTexture(new Rect(c.x + Mathf.Cos(a) * 30f - 1f, c.y - Mathf.Sin(a) * 30f - 1f, 2f, 2f), ringTex);
            }

            // the wind traced so far: the arc the player is drawing, which is what the spin is made of
            float arc = input.Recognizer.TotalArc;
            int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(arc) / 12f), 0, 60);
            GUI.color = new Color(0.95f, 0.76f, 0.47f, 0.65f);
            for (int i = 0; i < steps; i++)
            {
                float a = (-90f - Mathf.Sign(arc) * i * 12f) * Mathf.Deg2Rad;
                GUI.DrawTexture(new Rect(c.x + Mathf.Cos(a) * 24f - 1.5f, c.y - Mathf.Sin(a) * 24f - 1.5f, 3f, 3f), Texture2D.whiteTexture);
            }

            // the stick itself, warm while it is loaded
            Vector2 st = input.Stick;
            GUI.color = input.Loaded > 0.3f ? Sand : Teal;
            GUI.DrawTexture(new Rect(c.x + st.x * 30f - 4f, c.y - st.y * 30f - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (Time.time - rider.LastGestureTime < 1.2f)
            {
                sb.Clear();
                sb.Append(GestureLabel(rider.LastGesture));
                if (rider.LastGestureQuarters > 1) sb.Append(" ×").Append(rider.LastGestureQuarters);
                GUI.Label(new Rect(648f, h - 78f, 280f, 22f), sb.ToString(), small);
            }
        }

        static string GestureLabel(Flick f)
        {
            switch (f)
            {
                case Flick.Up: return "flick ↑";
                case Flick.UpRight: return "flick ↗";
                case Flick.UpLeft: return "flick ↖";
                case Flick.ScoopRight: return "enroulé ↻";
                case Flick.ScoopLeft: return "enroulé ↺";
                case Flick.CircleRight: return "tour complet ↻";
                case Flick.CircleLeft: return "tour complet ↺";
                case Flick.Nollie: return "↑ puis ↓";
                default: return "";
            }
        }

        // ---------------------------------------------------------------- top right: the session

        void DrawScorePanel(float w)
        {
            if (scorer == null) return;
            sb.Clear();
            sb.Append("vague <b>").Append(Mathf.RoundToInt(scorer.WaveRawPoints + scorer.PendingPoints)).Append("</b> pts");
            if (scorer.ChainCount > 0) sb.Append("   chaîne <color=#F2C078>x").Append(scorer.ChainMultiplier.ToString("0.00")).Append("</color>");
            GUI.Label(new Rect(w - 520, 18, 500, 26), sb.ToString(), chip);

            sb.Clear();
            sb.Append("meilleures <b>").Append(scorer.Best1.ToString("0.0")).Append("</b> + <b>")
              .Append(scorer.Best2.ToString("0.0")).Append("</b> = <b>")
              .Append((scorer.Best1 + scorer.Best2).ToString("0.0")).Append("</b>");
            GUI.Label(new Rect(w - 520, 44, 500, 26), sb.ToString(), chip);

            sb.Clear();
            sb.Append("vagues ").Append(rider.WavesRidden).Append("   figures ").Append(rider.TricksLanded)
              .Append("   chutes ").Append(rider.Wipeouts);
            GUI.Label(new Rect(w - 520, 72, 500, 24), sb.ToString(), chip);
        }

        void DrawPopups(float w, float h)
        {
            float y = h * 0.16f;
            bool any = false;
            for (int i = 0; i < popupCount; i++)
            {
                float age = Time.time - popups[i].time;
                if (age > 2.2f) continue;
                any = true;
                GUI.Label(new Rect(w * 0.5f - 400, y, 800, 60), popups[i].text, i == 0 ? trick : trickSmall);
                y += i == 0 ? 44f : 28f;
            }
            if (!any && Time.time - rider.LastEventTime < 1.2f)
                GUI.Label(new Rect(w * 0.5f - 400, h * 0.16f, 800, 60), rider.LastEvent, trick);

            if (scorer != null && Time.time - scorer.LastWaveScoreTime < 3f)
                GUI.Label(new Rect(w * 0.5f - 400, h * 0.34f, 800, 60), "NOTE " + scorer.WaveScore.ToString("0.0"), trick);
        }

        /// <summary>The single most useful thing to tell the player at this instant.</summary>
        string Cue()
        {
            if (rider.CanCatchNow) return PaddleCue();
            if (rider.WaveIncoming) return WaitCue();
            if (rider.Sample.WhitewaterAmount > 0.5f && rider.Engaged < 0.3f) return "mousse — canard pour passer dessous";
            if (rider.State != RiderState.Ride) return null;
            var input = InputRouter.Instance;
            bool pad = input != null && input.UsingGamepad;
            if (rider.Zone == WaveZone.Lip && rider.FaceSpeed > 2.2f)
                return input != null && input.Crouched ? "↑  —  envoie !" : (pad ? "stick D ↓ … puis ↑ au sommet" : "souris ↓ … puis ↑ au sommet (ou clic)");
            if (rider.InTube) return "reste dedans";
            var s = rider.Sample;
            if (s.BreakPhase >= 0.6f && s.PeelDistance < -3f) return "le rouleau te rattrape — file dans la ligne";
            if (rider.FaceSpeed < 1.8f) return "descends la face pour prendre de la vitesse";
            return null;
        }

        /// <summary>A wave on its way: face the beach and wait for it (paddling now would run away from it).</summary>
        string WaitCue()
        {
            Vector3 f = rider.HeadingDir;
            return Vector3.Dot(f, (Vector3)rider.Sample.TravelDir) < 0.5f
                ? "◀ ▶  TOURNE-TOI VERS LA PLAGE — la vague arrive"
                : "la vague arrive… attends-la";
        }

        /// <summary>To catch it you paddle WITH the wave, toward the beach: first turn the board that way.</summary>
        string PaddleCue()
        {
            Vector3 f = rider.visualRoot != null ? rider.visualRoot.forward : rider.transform.forward;
            f.y = 0f;
            return Vector3.Dot(f.normalized, (Vector3)rider.Sample.TravelDir) < 0.5f
                ? "◀ ▶  TOURNE-TOI VERS LA PLAGE, PUIS RAME  ▲"
                : "▲  RAME  ▲";
        }

        static string StateLabel(RiderState st)
        {
            switch (st)
            {
                case RiderState.Paddle: return "RAME";
                case RiderState.DuckDive: return "CANARD";
                case RiderState.TakeOff: return "TAKE-OFF";
                case RiderState.Ride: return "EN VAGUE";
                case RiderState.Air: return "AIR";
                case RiderState.Wipeout: return "CHUTE";
                case RiderState.KickOut: return "SORTIE";
            }
            return st.ToString();
        }

        // ---------------------------------------------------------------- the gesture guide

        void DrawLegend(float w, float h)
        {
            bool pad = InputRouter.Instance != null && InputRouter.Instance.UsingGamepad;
            if (!builtOnce || pad != builtPad) BuildLegend(pad);

            const float lw = 440f, lh = 356f;
            GUI.DrawTexture(new Rect(w - lw - 16, h - lh - 16, lw, lh), panelTex);
            GUI.Label(new Rect(w - lw - 4, h - lh - 8, 200, 22), "  FIGURES  <size=11>(H)</size>", mid);
            GUI.Label(new Rect(w - lw - 4, h - lh + 18, 200, lh), legendLeft, legendLabel);
            GUI.Label(new Rect(w - 256, h - lh + 18, 236, lh), legendRight, legendKey);
        }

        void BuildLegend(bool pad)
        {
            builtPad = pad; builtOnce = true;
            // The stick shape is the same on both devices — the mouse is integrated into a virtual stick — so the
            // gestures read identically and only the buttons change.
            string stick = pad ? "stick D" : "souris";
            string[,] rows =
            {
                { "S'envoler de la lèvre", "monte la face vite, ↓ puis ↑ en haut" },
                { "Sauter (pop)", pad ? "↓ puis ↑" : "↓ puis ↑, ou clic gauche" },
                { "El Rollo", "↓ puis ↗ / ↖" },
                { "Spin 180", "↓, quart de tour, ↑" },
                { "360, 540, 900…", "continue d'enrouler" },
                { "ARS", "rollo, puis enroule" },
                { "Backflip", "charge + tour complet" },
                { "Invert", "↑ puis ↓ sec" },
                { "Grab", pad ? "garde le stick tendu" : "garde la souris tendue" },
                { "Viser la réception", "↑ en l'air" },
                { "Tourner / ramer / carver", pad ? "stick G" : "ZQSD" },
                { "Sprint en rame", pad ? "Croix maintenu" : "Espace maintenu" },
                { "Pump", pad ? "R2" : "Maj" },
                { "Caler", pad ? "L2" : "Ctrl" },
                { "Sortir de la vague", pad ? "Croix (sur l'épaule)" : "Espace (sur l'épaule)" },
                { "Drop-knee", pad ? "L1 (appui court)" : "A (appui court)" },
                { "Regarder autour", pad ? "L1 maintenu + stick D" : "A maintenu + souris" },
                { "Canard", pad ? "Rond, ou ↑ puis ↓ à plat" : "C, ou ↑ puis ↓ à plat" },
                { "Reset / vague", pad ? "Triangle / D-pad ↑" : "R / N" },
            };

            var l = new StringBuilder(320);
            var r = new StringBuilder(320);
            l.Append("<i>").Append(stick).AppendLine(" = la planche</i>");
            r.AppendLine("");
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                l.AppendLine(rows[i, 0]);
                r.AppendLine(rows[i, 1]);
            }
            legendLeft = l.ToString();
            legendRight = r.ToString();
        }
    }
}
