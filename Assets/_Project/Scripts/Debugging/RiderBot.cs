// Test tooling (the autopilot, the ride simulation of the tests): editor and development builds only.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using WavyBoard.InputSys;
using WavyBoard.Ocean;
using WavyBoard.Rider;
using WavyBoard.Tricks;
using UnityEngine;

namespace WavyBoard.Debugging
{
    /// <summary>
    /// A bot that surfs with the player's controls, nothing more (it is an <see cref="IRiderInput"/>): it waits for the
    /// wave facing the beach, paddles it in, drops, and rides the line like a surfer — reading the curl, never charging
    /// into the foam. It drives the in-game autopilot (<see cref="RiderAutoPilot"/>, Tools/playtest.py) and the ride
    /// simulation of the tests. Call <see cref="Think"/> once before each physics step of the rider.
    ///
    /// Plans (what it does once up and riding):
    ///   Pocket ... tube rides: sits in the barrel just behind the curl (sits back, stalls), then races out (leans on,
    ///              pumps) before it closes, and goes back in;
    ///   Exit ..... races the curl out of wherever it is (leans on, holds the middle of the face; pumps if allowed);
    ///   Cruise ... gets on the line, then lets go of the sticks: the wave's own pace, the board holding its line;
    ///   Stall .... gets on the line and holds the stall;
    ///   Airs ..... rides in front of the curl and every few seconds bottom-turns, climbs crouched and flies off the lip,
    ///              flicking a trick;
    ///   Carve .... cutbacks: out along the shoulder, round back toward the curl, round again before the foam;
    ///   InAndOut . in and out of the tube, again and again: races out, pivots round (stall + steer) back to the curl,
    ///              pivots round again in the mouth of the barrel, sits in it a moment, races out.
    /// Whatever the plan, where the wave dies out in the channel it goes over the shoulder: the way out of a wave.
    /// </summary>
    public sealed class RiderBot : IRiderInput
    {
        public enum Plan { Pocket, Exit, Cruise, Stall, Airs, Carve, InAndOut }

        public Plan plan = Plan.Pocket;
        /// <summary>Pump in rhythm when it wants speed.</summary>
        public bool pumps = true;
        public float pumpInterval = 0.5f;
        /// <summary>Pocket: where to sit along the crest, in wave heights from the curl (negative = in the tube, which
        /// opens some 1.2 to 1.8 wave heights behind it).</summary>
        public float pocketDepth = -2f;
        /// <summary>Pocket: seconds in the tube before racing out.</summary>
        public float tubeStay = 3f;
        /// <summary>Airs: seconds of riding between two hits of the lip.</summary>
        public float airEvery = 2.5f;

        // ---- the controls, as the rider reads them
        public Vector2 Move { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool StallHeld { get; private set; }
        public bool Crouched { get; private set; }
        public float CrouchCharge => Crouched ? 1f : 0f;
        public bool StickHeld => false;
        bool pumpP;
        FlickResult flickP;
        public bool ConsumePump() { bool v = pumpP; pumpP = false; return v; }
        public bool ConsumeDuck() => false;
        public bool ConsumeStance() => false;
        public bool ConsumeReset() => false;
        public bool ConsumeJump(out float charge) { charge = 0f; return false; }
        public FlickResult ConsumeFlick() { var f = flickP; flickP = default; return f; }
        public void Rumble(float low, float high, float duration) { }

        /// <summary>What the bot is doing, for logs.</summary>
        public string Intent { get; private set; } = "";

        float nextPump;
        bool racing, onLine, flicked;
        int carveStage, loopStage;
        float loopSince;
        float lastHit = -99f, hitStart;
        int hitStage, trickIndex;

        /// <summary>Decides the controls for the rider's next physics step at <paramref name="time"/>.</summary>
        public void Think(RiderController rider, float time)
        {
            pumpP = false;
            flickP = default;
            Crouched = false;
            StallHeld = false;
            SprintHeld = false;
            var s = rider.Sample;
            Vector3 D = s.TravelDir;
            Vector3 heading = rider.HeadingDir;
            switch (rider.State)
            {
                case RiderState.Paddle:
                case RiderState.DuckDive:
                {
                    // face the beach and wait (the waves come to the rider); paddle hard once the face is close enough
                    bool go = rider.CanCatchNow;
                    Move = new Vector2(Mathf.Clamp(SignedAngle(heading, D) / 30f, -1f, 1f), go ? 1f : 0f);
                    SprintHeld = go;
                    racing = onLine = false; carveStage = 0; loopStage = 0; hitStage = 0; lastHit = time;
                    Intent = go ? "paddle" : "wait";
                    break;
                }
                case RiderState.TakeOff:
                    // angled into the line (+T, the way the wave peels); sprint released: that must not stop the ride
                    Move = new Vector2(0.8f, 1f);
                    Intent = "take-off";
                    break;
                case RiderState.Ride:
                    Ride(rider, in s, D, heading, time);
                    flicked = false;
                    break;
                case RiderState.Air:
                    Move = Vector2.zero;
                    // flick a trick just after the lip threw us (it still reads as a flick at the lip)
                    if (plan == Plan.Airs && !flicked && rider.AirTime > 0.04f && rider.AirWaveId >= 0)
                    {
                        flicked = true;
                        flickP = NextTrick();
                    }
                    hitStage = 0; lastHit = time;
                    Intent = "air";
                    break;
                default:
                    Move = Vector2.zero;
                    Intent = rider.State.ToString();
                    break;
            }
        }

        /// <summary>
        /// The heading is an angle from straight down the wave (D) round to the line (90 = along the crest, toward the
        /// side being ridden): above 90 climbs, below 90 drops. The height on the face is held by the angle, the pace
        /// against the curl by the trim, the stall and the pump — exactly the player's tools.
        /// </summary>
        void Ride(RiderController rider, in WaterSample s, Vector3 D, Vector3 heading, float time)
        {
            float H = Mathf.Max(1f, s.WaveHeight);
            float fw = Mathf.Max(2f, s.FaceWidth);
            float d = s.PeelDistance;
            float cdTarget = 0.35f * fw;
            float trim = 0f;
            bool stall = false, pump = false, crouch = false, handsOff = false;
            float lineSign = 1f;
            float? forcedAngle = null;

            if (rider.RideTime < 0.6f && s.CrestDistance < 0.6f * fw)
            {
                forcedAngle = 40f; trim = 1f;                          // the drop, already angled to the line
                Intent = "drop";
            }
            else if (s.PeelSpeed < 2.5f && d > 0f && s.WaveHeight < 1.6f)
            {
                forcedAngle = 165f; trim = 0f; stall = true;           // the wave dies out in the channel: sit back, over its shoulder
                Intent = "kick out";
            }
            else switch (plan)
            {
                case Plan.Pocket:
                {
                    bool barrel = s.LipWidth > 0.6f && s.BreakPhase > 1.2f;
                    if (barrel) cdTarget = Mathf.Clamp(0.3f * s.LipWidth, 0.2f * fw, 0.4f * fw);
                    if (!racing)
                    {
                        float pErr = d - pocketDepth * H;              // + = too far out in front, - = too deep
                        trim = Mathf.Clamp(-pErr * 0.5f, -1f, 1f);
                        stall = pErr > 1.5f;
                        pump = pErr < -1f;
                        bool closing = s.BreakPhase > 2.05f || s.TubeDepth > 0.8f;
                        if (rider.InTube && (rider.TubeTime > tubeStay || closing)) racing = true;
                        Intent = rider.InTube ? "tube" : pErr > 0f ? "wait for the curl" : "pocket";
                    }
                    else
                    {
                        trim = 1f; pump = true;
                        if (!rider.InTube && d > 0.5f * H) racing = false;
                        Intent = "race out";
                    }
                    break;
                }
                case Plan.Exit:
                    cdTarget = 0.4f * fw; trim = 1f; pump = true;
                    Intent = "race out";
                    break;
                case Plan.Cruise:
                    // get on the line once, then hands off: the wave's pace and the board's own line holding
                    if (!onLine && Mathf.Abs(SignedAngle(heading, Quaternion.AngleAxis(90f, Vector3.up) * D)) < 12f
                        && s.CrestDistance > 0.2f * fw && s.CrestDistance < 0.6f * fw) onLine = true;
                    handsOff = onLine;
                    Intent = onLine ? "hands off" : "to the line";
                    break;
                case Plan.Stall:
                    stall = true;
                    Intent = "stall";
                    break;
                case Plan.Airs:
                    Airs(rider, in s, H, fw, d, time, ref cdTarget, ref trim, ref pump, ref crouch, ref forcedAngle);
                    break;
                case Plan.InAndOut:
                    InAndOut(rider, in s, H, fw, d, time, heading, ref cdTarget, ref trim, ref pump, ref stall, ref lineSign);
                    break;
                case Plan.Carve:
                    // out on the shoulder, climb to the top third, then round (through the bottom of the face) back
                    // toward the curl; it closes at its speed plus ours, so round again well before it
                    if (carveStage == 0 && d > 2.4f * H) carveStage = 1;
                    else if (carveStage == 1 && s.CrestDistance < 0.3f * fw) carveStage = 2;
                    else if (carveStage == 2 && (d < 1.8f * H || s.WhitewaterAmount > 0.1f)) carveStage = 0;
                    lineSign = carveStage == 2 ? -1f : 1f;
                    cdTarget = carveStage == 1 ? 0.2f * fw : 0.4f * fw;
                    trim = carveStage == 0 ? 0.6f : 0f;
                    pump = carveStage == 0;
                    Intent = carveStage == 0 ? "down the line" : carveStage == 1 ? "set up the cutback" : "cutback";
                    break;
            }

            if (handsOff) { Move = Vector2.zero; }
            else
            {
                float hErr = (s.CrestDistance - cdTarget) / fw;          // + = too low on the face: climb
                float angle = forcedAngle ?? 90f + Mathf.Clamp(hErr * 90f, -35f, 35f);
                angle = Mathf.Clamp(angle, 25f, 165f);
                Vector3 desired = Quaternion.AngleAxis(lineSign * angle, Vector3.up) * D;
                float turn = SignedAngle(heading, desired);
                // a turn round (a cutback, the rebound) goes through the bottom of the face: over the top at speed is a launch
                float toBottom = SignedAngle(heading, D);
                if (Mathf.Abs(turn) > 110f && Mathf.Abs(toBottom) > 45f) turn = Mathf.Sign(toBottom) * 90f;
                Move = new Vector2(Mathf.Clamp(turn / 22f, -1f, 1f), trim);
            }
            StallHeld = stall;
            Crouched = crouch;
            if (pump && pumps && !crouch && time >= nextPump) { pumpP = true; nextPump = time + pumpInterval; }
        }

        /// <summary>
        /// In and out of the tube with the player's tools: race out (lean on, pump) onto the open face, pivot round
        /// (stall + steer) and ride back to the curl, pivot round again in the mouth of the barrel, sit in it a moment,
        /// race out again.
        /// </summary>
        void InAndOut(RiderController rider, in WaterSample s, float H, float fw, float d, float time, Vector3 heading,
                      ref float cdTarget, ref float trim, ref bool pump, ref bool stall, ref float lineSign)
        {
            float alongT = Vector3.Dot(heading, (Vector3)s.CrestDir);
            cdTarget = 0.4f * fw;
            switch (loopStage)
            {
                case 0:   // race out onto the open face
                    trim = 1f; pump = true;
                    Intent = "race out";
                    if (!rider.InTube && d > 1.4f * H) { loopStage = 1; loopSince = time; }
                    break;
                case 1:   // pivot round and ride back to the curl
                    lineSign = -1f;
                    stall = alongT > -0.5f;
                    trim = 0.5f;
                    Intent = stall ? "pivot back" : "back to the curl";
                    if (!stall && (d < -0.2f * H || rider.InTube || s.WhitewaterAmount > 0.1f)) { loopStage = 2; loopSince = time; }
                    break;
                case 2:   // pivot round again in the mouth of the barrel
                    stall = alongT < 0.5f;
                    Intent = "pivot in the barrel";
                    if (!stall) { loopStage = 3; loopSince = time; }
                    break;
                default:  // sit in the tube a moment, then race out
                    trim = -0.5f;
                    Intent = rider.InTube ? "tube" : "wait for the curl";
                    if (time - loopSince > 1.5f || s.BreakPhase > 2.05f) loopStage = 0;
                    break;
            }
        }

        /// <summary>Airs: ride in front of the curl, then bottom turn (drop low), then straight up the face crouched.</summary>
        void Airs(RiderController rider, in WaterSample s, float H, float fw, float d, float time,
                  ref float cdTarget, ref float trim, ref bool pump, ref bool crouch, ref float? forcedAngle)
        {
            float pErr = d - 0.8f * H;
            trim = Mathf.Clamp(-pErr * 0.4f, -1f, 1f);
            pump = pErr < -0.5f;
            bool section = s.BreakPhase > 0.75f && s.BreakPhase < 1.9f && !rider.InTube && H > 1.2f;
            switch (hitStage)
            {
                case 0:
                    Intent = "set up";
                    if (section && time - lastHit > airEvery && rider.LineSpeed > 4f) { hitStage = 1; hitStart = time; }
                    break;
                case 1:
                    forcedAngle = 45f; trim = 1f;                       // down to the bottom of the face
                    Intent = "bottom turn";
                    if (s.CrestDistance > 0.72f * fw || time - hitStart > 1.2f) { hitStage = 2; hitStart = time; }
                    break;
                default:
                    forcedAngle = 150f; crouch = true; trim = 0f;       // straight at the lip, a little along the line
                    Intent = "hit the lip";
                    if (time - hitStart > 1.6f) { hitStage = 0; lastHit = time; }
                    break;
            }
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
