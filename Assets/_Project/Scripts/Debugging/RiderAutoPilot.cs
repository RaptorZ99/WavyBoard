// Test tooling (Tools/playtest.py): editor and development builds only, never in the released game.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using WavyBoard.InputSys;
using WavyBoard.Rider;
using WavyBoard.Tricks;
using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.Debugging
{
    /// <summary>
    /// Scripted rider for automated tests and demo captures: paddles for the next wave, takes off, steers to stay in the
    /// pocket and pumps. Injects inputs through the InputRouter overrides. Modes:
    ///   Pocket ... holds the pocket (under the lip when there is one): tube rides;
    ///   Airs ..... from the pocket, turns up the face crouched, leaves the lip and flicks a trick in the air;
    ///   Weave .... changes direction along the line every few seconds (cutbacks): exercises the camera swing.
    /// </summary>
    public class RiderAutoPilot : MonoBehaviour
    {
        public enum Mode { Pocket, Airs, Weave }

        public RiderController rider;
        public SurfSpotConfig spot;
        public bool active = true;
        public Mode mode = Mode.Pocket;
        [Tooltip("Legacy switch: true = Airs mode")] public bool doAirs = false;
        public float pumpInterval = 0.55f;
        public float targetPeelDistance = -2.5f;   // Pocket: where to sit along the crest from the curl (negative = inside it; too deep = closeout)
        [Header("Airs")]
        public float climbAfter = 2.5f;            // seconds of pocket riding before going for the lip
        [Range(10f, 80f)] public float climbAngle = 35f;   // heading off straight up the face, toward the line
        [Header("Weave")]
        public float weavePeriod = 4f;

        float nextPump;
        float pocketSince;
        int trickIndex;
        bool flicked;
        float weaveSign = 1f, nextWeave;
        bool cuttingBack;

        void Start()
        {
            if (rider == null) rider = GetComponent<RiderController>();
            if (spot == null && rider != null) spot = rider.spot;
        }

        void OnDisable()
        {
            var input = InputRouter.Instance;
            if (input != null) { input.OverrideEnabled = false; input.OverrideCrouch = false; }
        }

        void Update()
        {
            var input = InputRouter.Instance;
            if (!active || rider == null || input == null || spot == null) return;
            if (doAirs) mode = Mode.Airs;
            input.OverrideEnabled = true;
            input.OverrideCrouch = false;
            var s = rider.Sample;
            Vector3 D = spot.TravelDir;
            Vector3 T = spot.CrestDir;
            Vector3 heading = rider.HeadingDir;

            switch (rider.State)
            {
                case RiderState.Paddle:
                case RiderState.DuckDive:
                {
                    // wait where we are facing the beach (the waves come to the rider); paddle hard once the face is
                    // close (paddling earlier runs away from it and it arrives broken): the HUD's "RAME" cue
                    bool waveComing = rider.CanCatchNow;
                    float turn = SignedAngle(heading, D);
                    input.OverrideMove = new Vector2(Mathf.Clamp(turn / 30f, -1f, 1f), waveComing ? 1f : 0f);
                    input.OverrideSprint = waveComing;
                    input.OverrideStall = false;
                    pocketSince = Time.time; weaveSign = 1f; nextWeave = Time.time + weavePeriod + 3f; cuttingBack = false;
                    break;
                }
                case RiderState.TakeOff:
                    input.OverrideMove = new Vector2(0f, 1f);
                    pocketSince = Time.time;
                    break;
                case RiderState.Ride:
                {
                    if (mode == Mode.Weave && Time.time > nextWeave) { weaveSign = -weaveSign; nextWeave = Time.time + weavePeriod; }
                    RideLikeASurfer(input, s, D, T, heading);
                    flicked = false;
                    break;
                }
                case RiderState.Air:
                    input.OverrideMove = Vector2.zero;
                    // flick a trick just after the lip threw us (it still reads as a flick at the lip)
                    if (mode == Mode.Airs && !flicked && rider.AirTime > 0.05f && rider.AirWaveId >= 0)
                    {
                        flicked = true;
                        input.InjectFlick(NextTrick());
                    }
                    pocketSince = Time.time;
                    break;
                default:
                    input.OverrideMove = Vector2.zero;
                    break;
            }
        }

        /// <summary>
        /// Drop, bottom turn, then trim the line in the pocket. The heading is an angle from straight down the wave (D)
        /// round to the line (90 deg along the crest, toward the side we ride): above 90 climbs, below 90 drops.
        ///   height: hold a spot on the face (the pocket; under the lip in a barrel) — too low: climb, too high: drop;
        ///   pace: hold a spot just behind the curl — behind it: drop a little for speed and pump; far ahead: stall.
        /// Airs mode goes for the lip from there; Weave turns round every few seconds.
        /// </summary>
        void RideLikeASurfer(InputRouter input, in WavyBoard.Ocean.WaterSample s, Vector3 D, Vector3 T, Vector3 heading)
        {
            float rideT = rider.RideTime;
            float fw = Mathf.Max(2f, s.FaceWidth);
            // too far out on the shoulder: cut back to the curl, then turn round and race it again
            if (mode != Mode.Weave && rideT > 1.5f)
            {
                if (!cuttingBack && s.PeelDistance > 9f) cuttingBack = true;
                else if (cuttingBack && s.PeelDistance < 2f) cuttingBack = false;
            }
            float lineSign = cuttingBack ? -weaveSign : weaveSign;
            bool barrel = s.LipWidth > 0.8f && s.BreakPhase > 1.3f;
            float angle;
            bool stall = false, climb = false;

            if (rideT < 0.7f && s.CrestDistance < 0.65f * fw)
            {
                angle = 40f;                                           // the drop: down the face, already angled to the line
            }
            else
            {
                // in a barrel ride high on the wall (the steep part has the power; the floor under the curtain is dead)
                float cdTarget = barrel ? Mathf.Clamp(0.3f * s.LipWidth, 0.18f * fw, 0.35f * fw) : 0.33f * fw;
                float hErr = s.CrestDistance - cdTarget;               // + = too low on the face
                float peelTarget = mode == Mode.Pocket ? targetPeelDistance : 4f;   // Airs: ahead of the curl, where the lip is still to throw
                float pErr = mode == Mode.Weave || cuttingBack ? 0f : s.PeelDistance - peelTarget;   // - = behind the curl
                angle = 88f + Mathf.Clamp(hErr * 14f, -40f, 40f) + Mathf.Clamp(pErr * 3f, -22f, 12f);
                stall = mode != Mode.Weave && pErr > 7f && rider.FaceSpeed > 4f;
                // go for the lip only on a section with some power (not the fading shoulder)
                climb = mode == Mode.Airs && Time.time - pocketSince > climbAfter && rider.FaceSpeed > 4.5f
                        && s.BreakPhase > 0.75f && s.BreakPhase < 1.9f && !rider.InTube && s.WaveHeight > 1.6f;
                if (climb) angle = 180f - climbAngle;                  // straight at the lip, angled a little along the line
            }
            angle = Mathf.Clamp(angle, 25f, 165f);
            Vector3 desired = Quaternion.AngleAxis(lineSign * angle, Vector3.up) * D;
            float turn = SignedAngle(heading, desired);
            input.OverrideMove = new Vector2(Mathf.Clamp(turn / 22f, -1f, 1f), climb ? 0f : 0.35f);
            input.OverrideStall = stall;
            input.OverrideCrouch = climb;
            input.OverrideSprint = false;
            if (!climb && Time.time >= nextPump) { input.InjectPump(); nextPump = Time.time + pumpInterval; }
        }

        FlickResult NextTrick()
        {
            switch (trickIndex++ % 4)
            {
                case 0: return new FlickResult { flick = Flick.Up, charge = 0.8f, snap = 0.8f, sign = 1f };
                case 1: return new FlickResult { flick = Flick.ScoopRight, charge = 0.6f, snap = 0.7f, quarters = 2, sign = 1f };
                case 2: return new FlickResult { flick = Flick.UpRight, charge = 0.7f, snap = 0.7f, sign = 1f };
                default: return new FlickResult { flick = Flick.ScoopLeft, charge = 0.6f, snap = 0.7f, quarters = 1, sign = -1f };
            }
        }

        static float SignedAngle(Vector3 from, Vector3 to) => Vector3.SignedAngle(from, to, Vector3.up);
    }
}
#endif
