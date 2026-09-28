using WavyBoard.InputSys;
using WavyBoard.Ocean;
using WavyBoard.Tricks;
using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.Rider
{
    /// <summary>KickOut: the ride is over without a fall — gone over the back of the wave, or ridden to its end.</summary>
    public enum RiderState { Paddle, DuckDive, TakeOff, Ride, Air, Wipeout, KickOut }

    /// <summary>
    /// Surface-locked bodyboard controller. The physics runs in FixedUpdate (kinematic integration on the analytic
    /// water); the visuals are extrapolated to render time every frame so the rider and the camera stay perfectly
    /// smooth on the moving wave. The class is split by concern:
    ///   RiderController.cs ......... state machine, paddling, take-off, wipeout, end of the ride
    ///   RiderController.Ride.cs .... riding the face: speed (the wave's power), carving, pumping, the lip, the tube, jumping
    ///   RiderController.Air.cs ..... flight: launch, return to the face, attitude, landing, air scoring
    ///   RiderController.Tricks.cs .. zones and the flick / button manoeuvres
    ///   RiderController.Render.cs .. render-time pose (extrapolation), camera pivot
    ///
    /// The controls and the water are injectable (<see cref="Input"/>, <see cref="WaterSource"/>) and a physics step is
    /// one call (<see cref="Step"/>): the tests ride whole waves in the Editor, without Play mode, with a bot at the
    /// same controls a player has.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public partial class RiderController : MonoBehaviour
    {
        public RiderTuning tuning;
        public BoardSpec board;
        public SurfSpotConfig spot;
        public Transform visualRoot;

        public RiderState State { get; private set; }
        /// <summary>Physics position (fixed step).</summary>
        public Vector3 Position => pos;
        /// <summary>Position extrapolated to the current frame: what is drawn, what the camera follows.</summary>
        public Vector3 RenderPosition => renderPos;
        public Vector3 Velocity => vel;
        /// <summary>Horizontal direction the board's nose points.</summary>
        public Vector3 HeadingDir => Heading();
        public float Speed => vel.magnitude;
        public WaterSample Sample => lastSample;
        public bool InTube { get; private set; }
        public float TubeTime { get; private set; }
        public float TotalTubeTime { get; private set; }
        public float AirTime { get; private set; }
        public float Lean { get; private set; }
        public float StateTime => stateTime;
        public string LastEvent { get; private set; } = "";
        public float LastEventTime { get; private set; }
        public int WavesRidden { get; private set; }
        public int Wipeouts { get; private set; }
        public float RideTime { get; private set; }
        public bool DropKnee { get; private set; }
        public float PumpFlash { get; private set; }
        public int AirsLanded { get; private set; }
        public string LastWipeoutReason { get; private set; } = "";
        public float AirPeak { get; private set; }     // max height above the surface during the air
        public bool GrabHeldInAir { get; private set; }
        public float RailSlip { get; private set; }    // lateral slip speed on the face (spray)
        public Vector3 BoardForward { get; private set; } = Vector3.forward;
        public Vector3 BoardRight { get; private set; } = Vector3.right;
        /// <summary>0..1 — the rider is crouched on the board, loading a jump.</summary>
        public float Crouch { get; private set; }
        /// <summary>Id of the wave the current air was launched from (-1 when none): the camera and the landing follow it.</summary>
        public int AirWaveId => airWaveId;
        /// <summary>World point the air is being steered back to (valid while <see cref="AirWaveId"/> &gt;= 0).</summary>
        public Vector3 AirLandingPoint { get; private set; }
        public event System.Action<string> OnEvent;
        /// <summary>Touched down after an air: (impact speed m/s, clean).</summary>
        public event System.Action<float, bool> OnLanded;

        /// <summary>Where on the wave the rider is: the same gesture is a different manoeuvre in each zone.</summary>
        public WaveZone Zone { get; private set; }
        public TrickRunner Tricks => tricks;
        public bool Grabbing { get; private set; }
        public int TricksLanded { get; private set; }
        /// <summary>The last gesture read off the right stick, for the HUD.</summary>
        public Flick LastGesture { get; private set; }
        public float LastGestureTime { get; private set; } = -10f;
        public int LastGestureQuarters { get; private set; }
        /// <summary>0..1: how much of the rider the wave is carrying (0 paddling, 1 fully in the pocket).</summary>
        public float Engaged { get; private set; }
        /// <summary>Speed on the face, relative to the moving water (m/s): what tricks ask for.</summary>
        public float FaceSpeed => State == RiderState.Paddle ? vel.magnitude : relVel.magnitude;
        /// <summary>Speed of the board through the water along its nose while riding (m/s).</summary>
        public float LineSpeed { get; private set; }
        /// <summary>The pace the wave holds the rider at along the line right now (m/s; 0 out of its power): the
        /// curl's own speed, more when he leans on, less when he sits back or stalls.</summary>
        public float PaceTarget { get; private set; }
        /// <summary>0..1: how much of the breaking wave's power reaches the rider (the pocket = 1).</summary>
        public float Power { get; private set; }
        /// <summary>A wave face is arriving behind a paddling rider: paddle now and it takes you. (Paddling much
        /// earlier runs away from it: it then reaches you already broken.)</summary>
        public bool CanCatchNow
        {
            get
            {
                var s = lastSample;
                return State == RiderState.Paddle && s.WaveHeight > 0.4f && s.BreakPhase < tuning.takeoffMaxPhase
                       && s.CrestDistance > 0f && s.CrestDistance < s.FaceWidth + 5f;
            }
        }
        /// <summary>A wave is on its way to a paddling rider but still too far: wait, facing the beach.</summary>
        public bool WaveIncoming
        {
            get
            {
                var s = lastSample;
                return State == RiderState.Paddle && s.WaveHeight > 0.4f && s.BreakPhase < tuning.takeoffMaxPhase
                       && s.CrestDistance >= s.FaceWidth + 5f && s.CrestDistance < s.FaceWidth + 25f;
            }
        }
        /// <summary>A manoeuvre was completed: name and points. Airs are named at the landing from the rotation
        /// actually turned; manoeuvres drawn on the water score when they finish, still riding.</summary>
        public event System.Action<string, float> OnTrick;

        Vector3 pos, vel, relVel;
        float yaw;
        float stateTime;
        Vector3 lineup;
        float initialYaw;
        WaterSample lastSample;
        float lastPumpTime = -10f;
        float stallTimer;
        float lineShare = -1f, lineYLast;   // the height on the face the board holds, stick centred (share of H)
        bool onBackLast;                    // past the top of the face at the end of the last step
        float rideCelerity = 7f;
        readonly TrickRunner tricks = new TrickRunner();
        IRiderInput driver;
        IWaterSurface waterSource;

        /// <summary>Where the controls come from: the player (the <see cref="InputRouter"/>) unless a bot drives.</summary>
        public IRiderInput Input
        {
            get => driver ?? InputRouter.Instance;
            set => driver = value;
        }

        /// <summary>The water ridden: the scene's <see cref="WaterSurfaceComposite"/> unless a simulated sea is given.</summary>
        public IWaterSurface WaterSource
        {
            get => waterSource ?? WaterSurfaceComposite.Instance;
            set => waterSource = value;
        }

        IWaterSurface Water => WaterSource;
        IRiderInput In => Input;
        double now;   // time of the physics step being run

        void Start()
        {
            if (spot != null)
            {
                lineup = spot.LineupPosition();
                initialYaw = spot.travelYawDeg;
            }
            else
            {
                lineup = transform.position;
                initialYaw = transform.eulerAngles.y;
            }
            now = Time.timeAsDouble;
            Respawn();
            // the rider is the centre of the world: every wave is generated around him
            var sched = FindAnyObjectByType<WaveSetScheduler>();
            if (sched != null && sched.aimAt == null) sched.aimAt = transform;
        }

        public void Respawn()
        {
            pos = lineup;
            vel = Vector3.zero;
            relVel = Vector3.zero;
            yaw = initialYaw;
            InTube = false; TubeTime = 0f; AirTime = 0f; RideTime = 0f; stallTimer = 0f; Crouch = 0f;
            tricks.Reset(); surfaceRoll = 0f; surfaceTrick = ""; surfacePoints = 0f; surfaceTimer = 0f; airSettling = false;
            airWaveId = -1;
            if (Water != null) lastSample = Water.Sample(pos, now);
            Enter(RiderState.Paddle);
            SnapVisuals();
        }

        /// <summary>Puts the rider on the water at <paramref name="position"/>, lying on the board facing
        /// <paramref name="yawDeg"/>, waiting for a wave — the line-up is now there.</summary>
        public void PlaceAt(Vector3 position, float yawDeg, double time)
        {
            now = time;
            lineup = position;
            initialYaw = yawDeg;
            Respawn();
        }

        /// <summary>Back in the line-up without being teleported: the waves come to the rider wherever he is, so the
        /// line-up is simply here, lying on the board facing the beach.</summary>
        void RecoverHere()
        {
            lineup = new Vector3(pos.x, lastSample.Height - tuning.paddleDraft, pos.z);
            Respawn();
        }

        Vector3 Heading() => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        void Enter(RiderState s)
        {
            var from = State;
            State = s;
            stateTime = 0f;
            if (s == RiderState.Air) BeginAir(from);
            if (s != RiderState.Ride) { LineSpeed = 0f; PaceTarget = 0f; Power = 0f; }
            if (s == RiderState.Ride)
            {
                if (from != RiderState.Air) RideTime = 0f;
                lineShare = -1f;
                lineYLast = pos.y - lastSample.SeaLevel;
                float carryK = CarryFactor(in lastSample);
                relVel = vel - (Vector3)lastSample.TravelDir * (rideCelerity * carryK);
            }
            if (s == RiderState.Wipeout || s == RiderState.KickOut)
            {
                tricks.StopTracks(); surfaceTrick = ""; surfaceTimer = 0f; airWaveId = -1; Crouch = 0f;
            }
            if (s == RiderState.Wipeout)
                wipeoutSpin = new Vector3(Random.Range(-240f, 240f), Random.Range(-120f, 120f), Random.Range(-300f, 300f));
        }

        void Event(string name)
        {
            LastEvent = name;
            LastEventTime = Time.time;
            OnEvent?.Invoke(name);
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime, Time.fixedTimeAsDouble);

        /// <summary>One physics step of <paramref name="dt"/> seconds at <paramref name="time"/> (the game calls it from
        /// FixedUpdate; the ride simulation calls it directly).</summary>
        public void Step(float dt, double time)
        {
            if (Water == null || In == null || tuning == null) return;
            now = time;
            float t = (float)time;
            stateTime += dt;
            PumpFlash = Mathf.Max(0f, PumpFlash - dt * 3f);

            if (In.ConsumeReset()) { RecoverHere(); return; }
            if (In.ConsumeStance() && (State == RiderState.Ride || State == RiderState.Paddle)) { DropKnee = !DropKnee; Event(DropKnee ? "Drop-knee" : "Prone"); }

            // which side of the crest the last step left him on: the resample below is at the new time, and the wave
            // has moved on under him since
            onBackLast = lastSample.OnBack;
            lastSample = Water.Sample(pos, time);
            // a grab is the stick parked out on the rim in mid-air, the way a skater's hand holds the board
            Grabbing = State == RiderState.Air && In.StickHeld;
            Zone = ComputeZone();
            bool carried = State == RiderState.TakeOff || State == RiderState.Ride || State == RiderState.Air;
            Engaged = carried ? Mathf.Max(0.35f, CarryFactor(in lastSample)) : 0f;
            bool crouchable = State == RiderState.Ride || State == RiderState.Paddle;
            Crouch = Mathf.MoveTowards(Crouch, crouchable && In.Crouched ? Mathf.Max(0.35f, In.CrouchCharge) : 0f, dt * 8f);

            switch (State)
            {
                case RiderState.Paddle: UpdatePaddle(dt, t); break;
                case RiderState.DuckDive: UpdateDuckDive(dt, t); break;
                case RiderState.TakeOff: UpdateTakeOff(dt, t); break;
                case RiderState.Ride: UpdateRide(dt, t); break;
                case RiderState.Air: UpdateAir(dt, t); break;
                case RiderState.Wipeout: UpdateWipeout(dt, t); break;
                case RiderState.KickOut: UpdateKickOut(dt, t); break;
            }
            UpdateBodyTarget();
        }

        // ------------------------------------------------------------------ Paddle
        void UpdatePaddle(float dt, float t)
        {
            Vector2 mv = In.Move;
            yaw += mv.x * tuning.paddleTurnRate * dt;
            Vector3 F = Heading();
            float target = mv.y > 0f ? (In.SprintHeld ? tuning.paddleSprintSpeed : tuning.paddleSpeed) * mv.y : mv.y * 0.8f;
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
            horiz = Vector3.MoveTowards(horiz, F * target, 3.5f * dt);
            Vector3 wv = lastSample.Velocity; wv.y = 0f;
            Vector3 push = wv * (0.5f + lastSample.WhitewaterAmount * tuning.whitewaterPush);
            // take-off assist: a face rising behind a paddling rider carries him along and lines him up with the wave
            var ls = lastSample;
            bool faceBehind = ls.BreakPhase > 0.15f && ls.BreakPhase < tuning.takeoffMaxPhase && ls.CrestDistance > -1f && ls.CrestDistance < ls.FaceWidth + 10f && ls.WaveHeight > 0.4f;
            if (faceBehind && mv.y > 0.2f && tuning.takeoffAssist > 0f)
            {
                Vector3 D = ls.TravelDir;
                float slopeK = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, 1f - ls.Normal.y * ls.Normal.y)) / 0.12f);
                push += D * (FindCelerity(ls.WaveId) * 0.45f * tuning.takeoffAssist * slopeK);
                float yawD = Mathf.Atan2(D.x, D.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, yawD, 1f - Mathf.Exp(-dt * 2.5f * tuning.takeoffAssist));
            }
            pos += (horiz + push) * dt;
            vel = horiz;
            float targetY = lastSample.Height - tuning.paddleDraft;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-dt * 12f));

            if (In.ConsumeDuck()) { Enter(RiderState.DuckDive); Event("Duck dive"); return; }
            In.ConsumePump();
            if (In.ConsumeJump(out float charge)) { Hop(charge); return; }
            // the right stick is the board here too: hops, flat rolls, and the duck dive
            if (HandleGesture(dt, Heading(), (Vector3)lastSample.Normal)) return;
            if (CanTakeOff())
            {
                rideCelerity = FindCelerity(lastSample.WaveId);
                Enter(RiderState.TakeOff);
                Event("Take-off");
            }
        }

        /// <summary>
        /// How much the wave transports the rider along D (0..1 of the celerity): the formed pocket, the whitewater,
        /// or simply lying on a sloped face of a shoaling wave (arcade: on the face = carried, the wave never outruns
        /// you). Only its front carries: behind the crest the wave runs on from under the rider (the way out of it).
        /// </summary>
        static float CarryFactor(in WaterSample s)
        {
            float ny = Mathf.Clamp(s.Normal.y, 0.05f, 1f);
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;
            float face = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.14f, slope))
                       * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.55f, s.BreakPhase))
                       * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.4f, 3f, s.BreakPhase)));
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.5f * Mathf.Max(1f, s.WaveHeight), 0f, s.CrestDistance));
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(s.Energy * 1.6f, face) * front, s.WhitewaterAmount * 0.9f));
        }

        bool CanTakeOff()
        {
            var s = lastSample;
            if (s.BreakPhase < 0.3f || s.BreakPhase > tuning.takeoffMaxPhase) return false;
            // must be heading roughly toward the beach: paddling out over the wave is not a take-off
            if (Vector3.Dot(Heading(), (Vector3)s.TravelDir) < 0.2f) return false;
            if (s.WaveHeight < 0.4f) return false;
            // on the face: between the crest and the bottom of the face (generous: the assist pulls the rider into the pocket)
            if (s.CrestDistance < 0.5f || s.CrestDistance > s.FaceWidth + 6f) return false;
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - s.Normal.y * s.Normal.y)) / Mathf.Max(0.05f, s.Normal.y);
            if (slope < 0.035f) return false;
            bool paddling = In.Move.y > 0.2f || Vector3.Dot(vel, (Vector3)s.TravelDir) >= tuning.takeoffMinForwardSpeed;
            return paddling;
        }

        float FindCelerity(int waveId)
        {
            var w = FindWave(waveId);
            return w != null ? w.Params.celerity : 7f;
        }

        SurfWave FindWave(int waveId) => Water?.FindWave(waveId);

        // ------------------------------------------------------------------ Duck dive
        void UpdateDuckDive(float dt, float t)
        {
            float k = stateTime / tuning.duckDiveDuration;
            float depth = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * tuning.duckDiveDepth;
            Vector3 F = Heading();
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
            horiz = Vector3.MoveTowards(horiz, F * tuning.paddleSpeed * 0.8f, 2f * dt);
            Vector3 wv = lastSample.Velocity; wv.y = 0f;
            pos += (horiz + wv * 0.15f) * dt;
            vel = horiz;
            pos.y = lastSample.Height - tuning.paddleDraft - depth;
            if (stateTime >= tuning.duckDiveDuration) Enter(RiderState.Paddle);
        }

        // ------------------------------------------------------------------ Take-off
        void UpdateTakeOff(float dt, float t)
        {
            float k = Mathf.Clamp01(stateTime / tuning.takeoffDuration);
            Vector3 D = lastSample.TravelDir;
            // the face pulls the rider: accelerate down-slope quickly, then blend to a fraction of the celerity
            Vector3 n0 = lastSample.Normal;
            Vector3 aG = -tuning.gravity * (Vector3.up - n0 * Vector3.Dot(Vector3.up, n0));
            Vector3 target = D * rideCelerity * 0.95f + Heading() * 1.0f;
            vel = Vector3.Lerp(vel, target, 1f - Mathf.Exp(-dt * 10f)) + aG * dt;
            pos += vel * dt;
            var s2 = Water.Sample(pos, now); lastSample = s2;
            float draft = Mathf.Lerp(tuning.paddleDraft, tuning.rideDraft, k);
            pos.y = s2.Height - draft;
            // face the travel direction progressively — angled the way the player steers: straight into the line
            float targetYaw = Mathf.Atan2(D.x, D.z) * Mathf.Rad2Deg + Mathf.Clamp(In.Move.x, -1f, 1f) * tuning.takeoffAngleMax;
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-dt * 5f));
            if (k >= 1f) { Enter(RiderState.Ride); Event("Riding"); }
            if (s2.BreakPhase < 0f || s2.BreakPhase > 2.4f) { Enter(RiderState.Paddle); Event("Missed"); }
        }

        // ------------------------------------------------------------------ Wipeout / end of the ride
        void Wipeout(string reason)
        {
            LastWipeoutReason = reason;
            Wipeouts++;
            InTube = false; TubeTime = 0f;
            Enter(RiderState.Wipeout);
            Event("Wipeout: " + reason);
            In.Rumble(0.9f, 0.6f, 0.35f);
        }

        void UpdateWipeout(float dt, float t)
        {
            var s = lastSample;
            Vector3 wv = s.Velocity;
            pos += wv * tuning.wipeoutDrift * dt;
            vel = Vector3.Lerp(vel, Vector3.zero, 1f - Mathf.Exp(-dt * 2f));
            pos += vel * dt;
            float targetY = s.Height - 0.5f;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-dt * 4f));
            if (stateTime >= tuning.wipeoutDuration)
            {
                if (tuning.autoReturnToLineup) { RecoverHere(); Event("Back to the lineup"); return; }
                pos.y = s.Height - tuning.paddleDraft;
                vel = Vector3.zero;
                yaw = initialYaw;
                Enter(RiderState.Paddle);
                Event("Recovered");
            }
        }

        void UpdateKickOut(float dt, float t)
        {
            if (stateTime < dt * 1.5f)
            {
                WavesRidden++;
                InTube = false; TubeTime = 0f;
            }
            vel = Vector3.Lerp(vel, Vector3.zero, 1f - Mathf.Exp(-dt * 1.5f));
            pos += vel * dt;
            pos.y = Mathf.Lerp(pos.y, lastSample.Height - tuning.paddleDraft, 1f - Mathf.Exp(-dt * 8f));
            if (stateTime >= 1.6f)
            {
                if (tuning.autoReturnToLineup) { RecoverHere(); Event("Back to the lineup"); }
                else Enter(RiderState.Paddle);
            }
        }
    }
}
