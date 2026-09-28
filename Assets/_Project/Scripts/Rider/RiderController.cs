using WavyBoard.InputSys;
using WavyBoard.Ocean;
using WavyBoard.Tricks;
using WavyBoard.Wave;
using Unity.Mathematics;
using UnityEngine;

namespace WavyBoard.Rider
{
    public enum RiderState { Paddle, DuckDive, TakeOff, Ride, Air, Wipeout, KickOut }

    /// <summary>Surface-locked bodyboard controller (spec §6-7). Kinematic integration in FixedUpdate; visuals in ApplyTransform.</summary>
    [DefaultExecutionOrder(-50)]
    public class RiderController : MonoBehaviour
    {
        public RiderTuning tuning;
        public BoardSpec board;
        public SurfSpotConfig spot;
        public Transform visualRoot;
        public Transform boardRoot;
        public Transform cameraTarget;

        public RiderState State { get; private set; }
        public Vector3 Position => pos;
        public Vector3 Velocity => vel;
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
        public float BestRideTime { get; private set; }
        public bool DropKnee { get; private set; }
        public float PumpFlash { get; private set; }
        public int PumpsThisRide { get; private set; }
        public int AirsLanded { get; private set; }
        public string LastWipeoutReason { get; private set; } = "";
        public float AirSpin { get; private set; }     // accumulated yaw degrees during the current/last air
        public float AirFlip { get; private set; }     // accumulated pitch degrees
        public float AirRoll { get; private set; }     // accumulated roll degrees (El Rollo)
        public float AirPeak { get; private set; }     // max height above the surface during the air
        public float PopEnergy { get; private set; }   // pocket energy at take-off of the air
        public bool GrabHeldInAir { get; private set; }
        public float RailSlip { get; private set; }    // lateral slip speed on the face (spray)
        public Vector3 RelVelocity => relVel;
        public Vector3 BoardForward { get; private set; } = Vector3.forward;
        public Vector3 BoardRight { get; private set; } = Vector3.right;
        public event System.Action<string> OnEvent;

        // ---- flick tricks (right stick)
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
        /// <summary>A wave face is arriving behind a paddling rider: paddle now and it takes you.</summary>
        public bool CanCatchNow
        {
            get
            {
                var s = lastSample;
                return State == RiderState.Paddle && s.WaveHeight > 0.4f && s.BreakPhase < tuning.takeoffMaxPhase
                       && s.CrestDistance > 0f && s.CrestDistance < s.FaceWidth + 12f;
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
        Quaternion visualRot = Quaternion.identity;
        Quaternion airRot = Quaternion.identity;
        float lastPumpTime = -10f;
        int pumpsThisDescent;
        bool wasDescending;
        float stallTimer;
        Vector3 wipeoutSpin;
        float rideCelerity = 7f;
        float lastLandedTime = -10f;
        readonly TrickRunner tricks = new TrickRunner();
        float surfaceRoll;             // visual roll of a flat roll / snap played on the water
        string surfaceTrick = "";      // manoeuvre being drawn on the water; it scores when it finishes
        float surfacePoints, surfaceTimer;
        bool airSettling;              // the player asked to spot the landing: stop turning and square up

        IWaterSurface Water => WaterSurfaceComposite.Instance;
        InputRouter In => InputRouter.Instance;

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
            Respawn();
            // the rider is the centre of the world: every wave is generated around him
            var sched = FindAnyObjectByType<WaveSetScheduler>();
            if (sched != null && sched.aimAt == null) sched.aimAt = transform;
        }

        public void SetLineup(Vector3 p, float yawDeg) { lineup = p; initialYaw = yawDeg; }

        public void Respawn()
        {
            pos = lineup;
            vel = Vector3.zero;
            yaw = initialYaw;
            airRot = Quaternion.Euler(0f, yaw, 0f);
            visualRot = airRot;
            InTube = false; TubeTime = 0f; AirTime = 0f; RideTime = 0f; stallTimer = 0f;
            tricks.Reset(); surfaceRoll = 0f; surfaceTrick = ""; surfacePoints = 0f; surfaceTimer = 0f; airSettling = false;
            Enter(RiderState.Paddle);
            ApplyTransform(0f);
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
            State = s;
            stateTime = 0f;
            if (s == RiderState.Air)
            {
                AirTime = 0f; airRot = visualRot; AirSpin = 0f; AirFlip = 0f; AirRoll = 0f; AirPeak = 0f; GrabHeldInAir = false; PopEnergy = lastSample.Energy;
                tricks.ResetAccumulation(); airSettling = false;
                // clear of the water before the first ballistic step (paddling, the board sits in it), or the landing
                // test fires straight away and the hop never happens
                pos.y = Mathf.Max(pos.y, lastSample.Height + 0.12f);
            }
            if (s == RiderState.Ride)
            {
                RideTime = 0f; PumpsThisRide = 0;
                float carryK = CarryFactor(in lastSample);
                relVel = vel - (Vector3)lastSample.TravelDir * (rideCelerity * carryK);
            }
            if (s == RiderState.Wipeout || s == RiderState.KickOut) { tricks.StopTracks(); surfaceTrick = ""; surfaceTimer = 0f; }
            if (s == RiderState.Wipeout) { wipeoutSpin = new Vector3(UnityEngine.Random.Range(-240f, 240f), UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(-300f, 300f)); }
        }

        void Event(string name)
        {
            LastEvent = name;
            LastEventTime = Time.time;
            OnEvent?.Invoke(name);
        }

        void FixedUpdate()
        {
            if (Water == null || In == null || tuning == null) return;
            float dt = Time.fixedDeltaTime;
            float t = (float)Time.fixedTimeAsDouble;
            stateTime += dt;
            PumpFlash = Mathf.Max(0f, PumpFlash - dt * 3f);

            if (In.ConsumeReset()) { RecoverHere(); return; }
            if (In.ConsumeStance() && (State == RiderState.Ride || State == RiderState.Paddle)) { DropKnee = !DropKnee; Event(DropKnee ? "Drop-knee" : "Prone"); }

            lastSample = Water.Sample(pos, t);
            In.RideContext = true;   // the right stick is the board, everywhere, always (hold L1 / Q to look around)
            // a grab is the stick parked out on the rim in mid-air, the way a skater's hand holds the board
            Grabbing = State == RiderState.Air && In.StickHeld;
            Zone = ComputeZone();
            bool carried = State == RiderState.TakeOff || State == RiderState.Ride || State == RiderState.Air;
            Engaged = carried ? Mathf.Max(0.35f, CarryFactor(in lastSample)) : 0f;

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
            ApplyTransform(dt);
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
            In.ConsumeKickOut();   // same button as the sprint: a press while paddling means nothing
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
        /// or simply lying on a sloped face of a shoaling wave (arcade: on the face = carried, the wave never outruns you).
        /// </summary>
        static float CarryFactor(in WaterSample s)
        {
            float ny = Mathf.Clamp(s.Normal.y, 0.05f, 1f);
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;
            float face = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.14f, slope))
                       * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.55f, s.BreakPhase))
                       * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.4f, 3f, s.BreakPhase)));
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(s.Energy * 1.6f, s.WhitewaterAmount * 0.9f), face));
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
            var list = Water.ActiveSurfWaves;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].Params.id == waveId) return list[i].Params.celerity;
            return 7f;
        }

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
            var s2 = Water.Sample(pos, t); lastSample = s2;
            float draft = Mathf.Lerp(tuning.paddleDraft, tuning.rideDraft, k);
            pos.y = s2.Height - draft;
            // face the travel direction progressively
            float targetYaw = Mathf.Atan2(D.x, D.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-dt * 5f));
            if (k >= 1f) { Enter(RiderState.Ride); Event("Riding"); }
            if (s2.BreakPhase < 0f || s2.BreakPhase > 2.4f) { Enter(RiderState.Paddle); Event("Missed"); }
        }

        // ------------------------------------------------------------------ Ride
        // Model: world velocity = carry (the wave transports the rider at its celerity while he is on the face)
        //        + relative velocity on the face (gravity down the slope, drag, rail grip, steering, pump, drive).
        //
        // Speed tuning (2026-09-08, "il faut prendre beaucoup plus de vitesse pour taper la lèvre"):
        //   gravity along the slope x slopeGravityGain (1 -> 1.7 between slope 0.3 and 1), x climbGravityScale (0.6) uphill;
        //   drag = quadraticDrag (0.025) * v^2 + planingDamping (0.25) * v   (stall: stallDamping 2.2 + x3.2 quadratic);
        //   carveDrive 1.2 m/s^2 at full lean and speed, trimDrive 1.5 m/s^2 stick-forward on the way down;
        //   pump = (0.08 * (vRel + c) + 0.4) * pumpBoost 1.4 * [0.7..1 by pocket energy]  -> +1.5..2.1 m/s;
        //   caps: relative speed <= maxSpeed - 2, world speed <= maxSpeed (16).
        //   Expected: a drop from the top of a 3 m face reaches 9-11 m/s relative (15-16 m/s world in the direction of
        //   travel), a bottom turn from 8-10 m/s still hits the lip with > 5 m/s; pop vertical speed = 2.5 + 0.25 * speed.
        void UpdateRide(float dt, float t)
        {
            RideTime += dt;
            Vector3 n = lastSample.Normal;
            Vector3 D = lastSample.TravelDir;
            Vector2 mv = In.Move;
            float x = mv.x, trim = mv.y;
            float c = rideCelerity;

            float carryK = CarryFactor(in lastSample);
            Vector3 carry = D * (c * carryK);

            float relSpeed = relVel.magnitude;
            float fv = Mathf.Clamp((relSpeed + c * 0.3f) / 8f, 0.35f, 1.2f);
            float yawRate = tuning.yawRateMax * board.yawMultiplier * (DropKnee ? 1.25f : 1f);
            yaw += x * yawRate * fv * dt;
            Lean = Mathf.Lerp(Lean, x, 1f - Mathf.Exp(-dt * 8f));

            Vector3 F = Vector3.ProjectOnPlane(Heading(), n).normalized;
            if (F.sqrMagnitude < 1e-4f) F = Heading();
            Vector3 R = Vector3.Cross(n, F).normalized;

            // gravity along the face: stronger on the steep part (the drop is where speed is made), softer when climbing
            Vector3 aG = -tuning.gravity * (Vector3.up - n * Vector3.Dot(Vector3.up, n));
            bool descending = Vector3.Dot(relVel, aG) > 0f;
            float ny = Mathf.Clamp(n.y, 0.05f, 1f);
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;
            float gGain = Mathf.Lerp(1f, tuning.slopeGravityGain, Mathf.Clamp01((slope - 0.3f) / 0.7f));
            if (!descending && relSpeed > 0.5f) gGain *= tuning.climbGravityScale;
            aG *= gGain;

            // drag: quadratic (terminal speed of a drop) + a low linear planing damping; the stall button drags hard
            float cd = tuning.quadraticDrag * board.dragMultiplier;
            if (In.StallHeld) cd *= tuning.stallDragMultiplier;
            cd *= trim > 0f ? Mathf.Lerp(1f, 0.85f, trim) : Mathf.Lerp(1f, 1.3f, -trim);
            float planing = In.StallHeld ? tuning.stallDamping : tuning.planingDamping;
            Vector3 aDrag = -cd * relSpeed * relVel - relVel * planing;

            // rail drive (arcade): carving hard at speed and trimming forward down the face generate speed
            float drive = tuning.carveDrive * Mathf.Abs(x) * Mathf.Clamp01(relSpeed / 6f);
            if (descending && trim > 0f) drive += tuning.trimDrive * trim;
            Vector3 aDrive = F * drive;

            // pump: rhythmic press while dropping down the face
            if (descending && !wasDescending) pumpsThisDescent = 0;
            wasDescending = descending;
            if (In.ConsumePump())
            {
                float since = t - lastPumpTime;
                lastPumpTime = t;
                if (descending && since >= tuning.pumpWindowMin && since <= tuning.pumpWindowMax && pumpsThisDescent < tuning.pumpMaxPerDescent)
                {
                    float energyK = Mathf.Lerp(tuning.pumpEnergyFloor, 1f, Mathf.Clamp01(lastSample.Energy));
                    relVel += F * (tuning.pumpGainFactor * (relSpeed + c) + tuning.pumpGainFlat) * tuning.pumpBoost * energyK * (DropKnee ? 0.7f : 1f);
                    pumpsThisDescent++; PumpsThisRide++; PumpFlash = 1f;
                    Event("Pump");
                }
                else relVel *= tuning.pumpPenalty;
            }

            relVel += (aG + aDrag + aDrive) * dt;

            // rail grip / slip and alignment of the relative velocity with the board heading
            float vF = Vector3.Dot(relVel, F), vR = Vector3.Dot(relVel, R), vN = Vector3.Dot(relVel, n);
            float gripTau = tuning.gripTau * board.gripMultiplier * (1f + 0.8f * (1f - Mathf.Abs(x))) * (lastSample.WhitewaterAmount > 0.5f ? 2.5f : 1f) * (DropKnee ? 0.8f : 1f);
            vR *= Mathf.Exp(-dt / gripTau);
            RailSlip = Mathf.Abs(vR);
            BoardForward = F; BoardRight = R;
            relVel = F * vF + R * vR + n * vN;
            Vector3 vh = relVel - n * vN;
            float vhMag = vh.magnitude;
            if (vhMag > 0.3f)
            {
                Vector3 dir = vh / vhMag;
                float k = 1f - Mathf.Exp(-dt / tuning.alignTau);
                Vector3 target = Vector3.Dot(dir, F) >= 0f ? F : -F;
                dir = Vector3.Slerp(dir, target, k).normalized;
                relVel = dir * vhMag + n * vN;
            }
            relSpeed = relVel.magnitude;
            float maxRel = Mathf.Max(4f, tuning.maxSpeed - 2f);
            if (relSpeed > maxRel) relVel = relVel / relSpeed * maxRel;

            vel = carry + relVel;
            float vmag = vel.magnitude;
            if (vmag > tuning.maxSpeed)
            {
                // cap the WORLD speed by trimming the relative velocity along the motion
                relVel -= vel / vmag * (vmag - tuning.maxSpeed);
                vel = carry + relVel;
            }
            pos += vel * dt;
            var s2 = Water.Sample(pos, t);
            lastSample = s2;
            Vector3 n2 = s2.Normal;
            float targetY = s2.Height + tuning.rideDraft;
            float dy = targetY - pos.y;

            // natural ejection over the lip
            if (dy < -0.7f && vel.y > tuning.ejectVerticalSpeed && s2.CrestDistance > -3f && s2.BreakPhase > 0.6f)
            {
                Enter(RiderState.Air);
                Event("Launched");
                return;
            }
            pos.y = targetY;
            relVel -= n2 * Vector3.Dot(relVel, n2);
            vel = carry + relVel;

            // kick-out on the shoulder; every trick is on the right stick
            if (In.ConsumeKickOut() && s2.Energy < 0.35f && s2.BreakPhase < 1.2f) { Enter(RiderState.KickOut); Event("Kick-out"); return; }

            // flick tricks: the right stick traces the gesture, the zone decides what it means
            Zone = ComputeZone();
            if (HandleGesture(dt, F, n2)) return;

            // tube
            InTube = s2.InTube;
            if (InTube)
            {
                TubeTime += dt; TotalTubeTime += dt;
                if (TubeTime > 0.3f && stateTime > 0.5f && LastEvent != "Tube") Event("Tube");
                if (s2.BreakPhase >= tuning.tubeCloseoutWipeoutPhase && s2.TubeDepth > 0.8f) { Wipeout("closeout"); return; }
            }
            else TubeTime = 0f;

            // left the wave footprint or went over the back
            if (s2.BreakPhase < 0f) { Enter(RiderState.KickOut); Event("Wave over"); return; }
            if (s2.CrestDistance < -2f && s2.BreakPhase > 0.2f) { Enter(RiderState.KickOut); Event("Over the back"); return; }

            // failure modes
            float speed = vel.magnitude;
            if (speed < tuning.minRideSpeedBeforeStall && s2.BreakPhase >= 1f) { stallTimer += dt; if (stallTimer > tuning.stallWipeoutTime) { Wipeout("stalled"); return; } }
            else stallTimer = 0f;
            if (n2.y < 0.3f && speed < tuning.tooSteepMaxSpeed && !s2.InTube) { Wipeout("too steep"); return; }
            if (s2.WhitewaterAmount > 0.75f && speed < tuning.whitewaterWipeoutSpeed && s2.CrestDistance < 4f) { Wipeout("whitewater"); return; }
        }

        // ------------------------------------------------------------------ Air
        void UpdateAir(float dt, float t)
        {
            AirTime += dt;
            vel += Vector3.down * tuning.gravity * dt;
            vel -= vel * (vel.magnitude * tuning.airDrag * dt);
            pos += vel * dt;

            // right stick: flicked rotations (see TrickCatalog); left stick: your own spin, for as long as you hold it
            AirGesture();
            Vector3 d = tricks.Tick(dt);
            float mul = Grabbing ? tuning.grabRateMultiplier : 1f;
            float spin = In.Move.x * tuning.airSpinRate * mul * dt;
            d.x += spin;
            tricks.AddYaw(spin);
            airRot = airRot * Quaternion.Euler(d.y, d.x, d.z);
            yaw += d.x;
            AirSpin += d.x; AirFlip += d.y; AirRoll += d.z;
            GrabHeldInAir |= Grabbing;
            tricks.Grabbed |= Grabbing;
            if (airSettling && !tricks.Busy) SquareUp(dt);

            var s = Water.Sample(pos, t);
            lastSample = s;
            AirPeak = Mathf.Max(AirPeak, pos.y - s.Height);
            if (AirTime > 0.08f && pos.y <= s.Height + 0.05f) { Land(s); return; }
            if (AirTime > tuning.maxAirTime) { Wipeout("fell"); }
        }

        /// <summary>
        /// Three things decide a landing, the three a judge looks at: is the board flat to the water (align), is it
        /// pointing where it is travelling (yaw error — coming down sideways is what really hurts), and had the
        /// rotation finished. Together they give an execution multiplier rather than a pass/fail, so a scrappy
        /// landing costs points instead of the wave. Forgiving off the wave, strict on a face: a hop in the line-up
        /// should never punish you for messing about.
        /// </summary>
        void Land(WaterSample s)
        {
            Vector3 n = s.Normal;
            float align = Vector3.Dot(airRot * Vector3.up, n);
            Vector3 travel = Vector3.ProjectOnPlane(vel, n);
            Vector3 nose = Vector3.ProjectOnPlane(airRot * Vector3.forward, n);
            float yawErr = travel.sqrMagnitude > 1f && nose.sqrMagnitude > 1e-4f ? Vector3.Angle(nose, travel) : 0f;
            bool stillSpinning = tricks.Busy;
            tricks.StopTracks();
            airSettling = false;

            bool onWave = s.BreakPhase >= 0f;
            float yawMax = Mathf.Max(5f, tuning.landYawMax);
            float need = onWave ? (stillSpinning ? tuning.landAlignMin + 0.08f : tuning.landAlignMin) : tuning.landSketchyMin;
            if (align < tuning.landSketchyMin * 0.75f && vel.y < -9f) { Wipeout("réception ratée"); return; }
            if (onWave && AirTime > 0.35f && yawErr > yawMax * 2f && align < tuning.landAlignMin) { Wipeout("posé en travers"); return; }
            if (onWave && align < tuning.landSketchyMin) { Wipeout("réception ratée"); return; }

            bool clean = align >= need && yawErr <= yawMax && !stillSpinning;
            float quality = Mathf.SmoothStep(need, 0.995f, align) * (1f - Mathf.Clamp01(yawErr / yawMax));
            float execution = clean ? Mathf.Clamp(0.8f + 0.7f * quality, 0.8f, 1.5f) : 0.6f;
            vel = Vector3.ProjectOnPlane(vel, n) * (clean ? Mathf.Lerp(0.88f, 0.96f, quality) : 0.72f);
            if (onWave)
            {
                pos.y = s.Height + tuning.rideDraft;
                AirsLanded++;
                lastLandedTime = Time.time;
                Enter(RiderState.Ride);
            }
            else
            {
                pos.y = s.Height - tuning.paddleDraft;
                vel.y = 0f;
                Enter(RiderState.Paddle);
            }
            ScoreAir(execution, !clean);
            In.Rumble(clean ? 0.5f : 0.7f, 0.3f, 0.15f);
        }

        /// <summary>Names the air after what was actually turned and scores it.</summary>
        void ScoreAir(float execution, bool sketchy)
        {
            if (AirTime > 0.25f && tricks.PendingPoints > 0f)
            {
                string name = tricks.NameRun();
                float pts = tricks.PendingPoints * execution * (0.85f + 0.35f * Mathf.Clamp01(AirPeak / 3f));
                TricksLanded++;
                OnTrick?.Invoke(name, pts);
                Event(sketchy ? name + " (sale)" : name);
            }
            else Event("Posé");
            tricks.ResetAccumulation();
        }

        // ------------------------------------------------------------------ Wipeout / Kick-out
        void Wipeout(string reason)
        {
            LastWipeoutReason = reason;
            Wipeouts++;
            if (RideTime > BestRideTime) BestRideTime = RideTime;
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
            visualRot = visualRot * Quaternion.Euler(wipeoutSpin * dt);
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
                if (RideTime > BestRideTime) BestRideTime = RideTime;
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

        // ------------------------------------------------------------------ Flick tricks
        WaveZone ComputeZone()
        {
            var s = lastSample;
            switch (State)
            {
                case RiderState.Air: return WaveZone.Air;
                case RiderState.TakeOff:
                case RiderState.Ride:
                    if (InTube) return WaveZone.Tube;
                    bool nearLip = Mathf.Abs(s.CrestDistance) < tuning.popWindowCrestDistance && s.CrestDistance > -2.5f
                                   && s.BreakPhase >= 0.6f && s.BreakPhase < 2.3f;
                    return nearLip ? WaveZone.Lip : WaveZone.Face;
                default: return WaveZone.Flat;
            }
        }

        /// <summary>
        /// Plays the surface part of running manoeuvres and reads a new gesture on the face. Returns true when the
        /// gesture launched an air (the ride step must stop there).
        /// </summary>
        bool HandleGesture(float dt, Vector3 F, Vector3 n)
        {
            if (tricks.Busy)
            {
                Vector3 d = tricks.Tick(dt);
                yaw += d.x;
                surfaceRoll += d.z;
            }
            else surfaceRoll = Mathf.MoveTowards(surfaceRoll, 0f, 540f * dt);
            TickSurfaceTrick(dt);

            var g = In.ConsumeFlick();
            if (g.flick == Flick.None) return false;
            NoteGesture(g);
            float faceSpeed = FaceSpeed;
            if (!TrickCatalog.Resolve(g, Zone, faceSpeed, Grabbing, out TrickDef def, out float needed))
            {
                Event(needed > 0f && faceSpeed < needed
                    ? "trop lent — " + faceSpeed.ToString("0.0") + " / " + needed.ToString("0.0") + " m/s"
                    : "pas ici");
                return false;
            }
            switch (def.kind)
            {
                case TrickKind.Air:
                {
                    float speed = vel.magnitude;
                    float vUp = (tuning.popVerticalSpeed + tuning.popSpeedGain * speed) * board.popMultiplier * def.popScale;
                    vel += Vector3.up * vUp + n * 1.0f;
                    Enter(RiderState.Air);
                    tricks.Begin(def);
                    Event(def.name);
                    In.Rumble(0.25f, 0.45f, 0.09f);
                    return true;
                }
                case TrickKind.Stall:
                    if (Zone == WaveZone.Flat) { Enter(RiderState.DuckDive); Event("Canard"); return true; }
                    relVel *= 0.82f;
                    Event(def.name);
                    return false;
                case TrickKind.Drive:
                    relVel += F * def.driveBoost;
                    BeginSurfaceTrick(def);
                    return false;
                case TrickKind.Settle:
                case TrickKind.Tuck:
                    return false;   // these only mean anything in the air
                default:
                    tricks.Begin(def);
                    if (def.driveBoost > 0f) relVel += F * def.driveBoost;
                    BeginSurfaceTrick(def);
                    return false;
            }
        }

        void NoteGesture(in FlickResult g)
        {
            LastGesture = g.flick;
            LastGestureQuarters = g.quarters;
            LastGestureTime = Time.time;
        }

        /// <summary>Arms a manoeuvre on the water; it scores when it finishes, and only if the rider is still up.</summary>
        void BeginSurfaceTrick(in TrickDef def)
        {
            surfaceTrick = def.name;
            surfacePoints = def.points;
            surfaceTimer = Mathf.Max(0.05f, def.duration);
            Event(def.name);
        }

        void TickSurfaceTrick(float dt)
        {
            if (surfaceTimer <= 0f) return;
            surfaceTimer -= dt;
            if (surfaceTimer > 0f) return;
            if (State == RiderState.Ride && surfacePoints > 0f)
            {
                TricksLanded++;
                OnTrick?.Invoke(surfaceTrick, surfacePoints);
            }
            surfaceTrick = ""; surfacePoints = 0f; surfaceTimer = 0f;
        }

        /// <summary>A gesture in the air: add a rotation, tuck it in, or spot the landing.</summary>
        void AirGesture()
        {
            var g = In.ConsumeFlick();
            if (g.flick == Flick.None) return;
            NoteGesture(g);
            if (!TrickCatalog.Resolve(g, WaveZone.Air, 0f, Grabbing, out TrickDef def)) return;
            switch (def.kind)
            {
                case TrickKind.Settle:
                    tricks.StopTracks();
                    airSettling = true;
                    Event("Réception");
                    break;
                case TrickKind.Tuck:
                    tricks.Speed = tuning.airTuckSpeedup;
                    vel.y -= tuning.airTuckDrop;
                    airSettling = false;
                    Event(def.name);
                    break;
                default:
                    tricks.Begin(def);
                    airSettling = false;
                    Event(def.name);
                    break;
            }
        }

        /// <summary>Spotting the landing: the board squares up to the water it is about to meet.</summary>
        void SquareUp(float dt)
        {
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
            Vector3 fwd = horiz.sqrMagnitude > 0.25f ? horiz.normalized : Heading();
            Vector3 upT = Vector3.Slerp(Vector3.up, (Vector3)lastSample.Normal, tuning.airLevelToSurface).normalized;
            Vector3 f2 = Vector3.ProjectOnPlane(fwd, upT);
            if (f2.sqrMagnitude < 1e-4f) return;
            float k = 1f - Mathf.Exp(-dt * tuning.airSettleRate);
            airRot = Quaternion.Slerp(airRot, Quaternion.LookRotation(f2.normalized, upT), k);
            yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, k);
        }

        // ------------------------------------------------------------------ Visuals
        void ApplyTransform(float dt)
        {
            transform.position = pos;
            Quaternion target;
            Vector3 n = (Vector3)lastSample.Normal;
            if (n.sqrMagnitude < 0.5f) n = Vector3.up;
            Vector3 F = Vector3.ProjectOnPlane(Heading(), n).normalized;
            if (F.sqrMagnitude < 1e-4f) F = Heading();
            switch (State)
            {
                case RiderState.Air: target = airRot; break;
                case RiderState.Wipeout: target = visualRot; break;
                case RiderState.Ride:
                case RiderState.TakeOff:
                    target = Quaternion.LookRotation(F, n) * Quaternion.AngleAxis(-Lean * tuning.leanMax * Mathf.Clamp01(Speed / 6f) + surfaceRoll, Vector3.forward);
                    break;
                default:
                    target = Quaternion.LookRotation(F, n);
                    break;
            }
            float k = dt > 0f ? 1f - Mathf.Exp(-dt * (State == RiderState.Air ? 30f : 14f)) : 1f;
            visualRot = State == RiderState.Wipeout ? visualRot : Quaternion.Slerp(visualRot, target, k);
            if (visualRoot != null) visualRoot.rotation = visualRot;
            if (cameraTarget != null)
            {
                // The camera pivot is oriented in the WAVE frame (forward = along the crest toward the unbroken side),
                // so the follow camera sits on the beach side, trailing along the crest, and never ends up inside the face.
                Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
                Vector3 look;
                bool onWave = lastSample.BreakPhase >= 0f && (State == RiderState.Ride || State == RiderState.Air || State == RiderState.TakeOff);
                if (onWave) look = (Vector3)lastSample.CrestDir;
                else look = horiz.sqrMagnitude > 0.25f ? horiz.normalized : Heading();
                if (look.sqrMagnitude < 1e-4f) look = Vector3.forward;
                cameraTarget.position = pos + Vector3.up * 0.35f + horiz * 0.1f;
                Quaternion camRot = Quaternion.LookRotation(look, Vector3.up);
                cameraTarget.rotation = dt > 0f ? Quaternion.Slerp(cameraTarget.rotation, camRot, 1f - Mathf.Exp(-dt * 4f)) : camRot;
            }
        }
    }
}
