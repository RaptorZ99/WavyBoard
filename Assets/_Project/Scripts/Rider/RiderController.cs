using Biscotte.InputSys;
using Biscotte.Ocean;
using Biscotte.Wave;
using Unity.Mathematics;
using UnityEngine;

namespace Biscotte.Rider
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
        public event System.Action<string> OnEvent;

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
        float rolloTimer;
        Vector3 wipeoutSpin;
        float rideCelerity = 7f;
        float lastLandedTime = -10f;

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
        }

        public void SetLineup(Vector3 p, float yawDeg) { lineup = p; initialYaw = yawDeg; }

        public void Respawn()
        {
            pos = lineup;
            vel = Vector3.zero;
            yaw = initialYaw;
            airRot = Quaternion.Euler(0f, yaw, 0f);
            visualRot = airRot;
            InTube = false; TubeTime = 0f; AirTime = 0f; RideTime = 0f; stallTimer = 0f; rolloTimer = 0f;
            Enter(RiderState.Paddle);
            ApplyTransform(0f);
        }

        Vector3 Heading() => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        void Enter(RiderState s)
        {
            State = s;
            stateTime = 0f;
            if (s == RiderState.Air) { AirTime = 0f; airRot = visualRot; }
            if (s == RiderState.Ride)
            {
                RideTime = 0f; PumpsThisRide = 0;
                float carryK = Mathf.Clamp01(Mathf.Max(lastSample.Energy * 1.6f, lastSample.WhitewaterAmount * 0.9f));
                relVel = vel - (Vector3)lastSample.TravelDir * (rideCelerity * carryK);
            }
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

            if (In.ConsumeReset()) { Respawn(); return; }
            if (In.ConsumeStance() && (State == RiderState.Ride || State == RiderState.Paddle)) { DropKnee = !DropKnee; Event(DropKnee ? "Drop-knee" : "Prone"); }

            lastSample = Water.Sample(pos, t);

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
            Vector3 push = wv * (0.3f + lastSample.WhitewaterAmount * tuning.whitewaterPush);
            pos += (horiz + push) * dt;
            vel = horiz;
            float targetY = lastSample.Height - tuning.paddleDraft;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-dt * 12f));

            if (In.ConsumeDuck()) { Enter(RiderState.DuckDive); Event("Duck dive"); return; }
            if (In.ConsumePop()) { /* sprint paddle is held elsewhere */ }
            if (CanTakeOff())
            {
                rideCelerity = FindCelerity(lastSample.WaveId);
                Enter(RiderState.TakeOff);
                Event("Take-off");
            }
        }

        bool CanTakeOff()
        {
            var s = lastSample;
            if (s.BreakPhase < 0.3f || s.BreakPhase > tuning.takeoffMaxPhase) return false;
            if (s.WaveHeight < 0.5f) return false;
            // on the face: between the crest and the bottom of the face
            if (s.CrestDistance < 1.0f || s.CrestDistance > s.FaceWidth + 3f) return false;
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - s.Normal.y * s.Normal.y)) / Mathf.Max(0.05f, s.Normal.y);
            if (slope < 0.06f) return false;
            bool paddling = In.Move.y > 0.3f || Vector3.Dot(vel, (Vector3)s.TravelDir) >= tuning.takeoffMinForwardSpeed;
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
        // Model: world velocity = carry (the wave transports the rider at its celerity while he is in the pocket)
        //        + relative velocity on the face (gravity down the slope, planing damping, rail grip, steering, pump).
        void UpdateRide(float dt, float t)
        {
            RideTime += dt;
            Vector3 n = lastSample.Normal;
            Vector3 D = lastSample.TravelDir;
            Vector2 mv = In.Move;
            float x = mv.x, trim = mv.y;
            float c = rideCelerity;

            float carryK = Mathf.Clamp01(Mathf.Max(lastSample.Energy * 1.6f, lastSample.WhitewaterAmount * 0.9f));
            Vector3 carry = D * (c * carryK);

            float relSpeed = relVel.magnitude;
            float fv = Mathf.Clamp((relSpeed + c * 0.3f) / 8f, 0.35f, 1.2f);
            float yawRate = tuning.yawRateMax * board.yawMultiplier * (DropKnee ? 1.25f : 1f);
            yaw += x * yawRate * fv * dt;
            Lean = Mathf.Lerp(Lean, x, 1f - Mathf.Exp(-dt * 8f));

            Vector3 F = Vector3.ProjectOnPlane(Heading(), n).normalized;
            if (F.sqrMagnitude < 1e-4f) F = Heading();
            Vector3 R = Vector3.Cross(n, F).normalized;

            Vector3 aG = -tuning.gravity * (Vector3.up - n * Vector3.Dot(Vector3.up, n));
            float cd = tuning.dragCoefficient * board.dragMultiplier;
            if (In.StallHeld) cd *= tuning.stallDragMultiplier;
            cd *= trim > 0f ? Mathf.Lerp(1f, 0.85f, trim) : Mathf.Lerp(1f, 1.3f, -trim);
            float planing = In.StallHeld ? 2.2f : 0.9f;                       // linear damping (board planing / water friction)
            Vector3 aDrag = -cd * relSpeed * relVel - relVel * planing;

            // pump: rhythmic press while dropping down the face
            bool descending = Vector3.Dot(relVel, aG) > 0f;
            if (descending && !wasDescending) pumpsThisDescent = 0;
            wasDescending = descending;
            if (In.ConsumePump())
            {
                float since = t - lastPumpTime;
                lastPumpTime = t;
                if (descending && since >= tuning.pumpWindowMin && since <= tuning.pumpWindowMax && pumpsThisDescent < tuning.pumpMaxPerDescent)
                {
                    relVel += F * (tuning.pumpGainFactor * (relSpeed + c) + tuning.pumpGainFlat) * Mathf.Max(0.3f, lastSample.Energy) * (DropKnee ? 0.7f : 1f);
                    pumpsThisDescent++; PumpsThisRide++; PumpFlash = 1f;
                    Event("Pump");
                }
                else relVel *= tuning.pumpPenalty;
            }

            relVel += (aG + aDrag) * dt;

            // rail grip / slip and alignment of the relative velocity with the board heading
            float vF = Vector3.Dot(relVel, F), vR = Vector3.Dot(relVel, R), vN = Vector3.Dot(relVel, n);
            float gripTau = tuning.gripTau * board.gripMultiplier * (1f + 0.8f * (1f - Mathf.Abs(x))) * (lastSample.WhitewaterAmount > 0.5f ? 2.5f : 1f) * (DropKnee ? 0.8f : 1f);
            vR *= Mathf.Exp(-dt / gripTau);
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
            float maxRel = tuning.maxSpeed - c * carryK;
            if (relSpeed > maxRel) relVel = relVel / relSpeed * maxRel;

            vel = carry + relVel;
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

            // pop / kick-out
            if (In.ConsumePop())
            {
                bool nearLip = Mathf.Abs(s2.CrestDistance) < tuning.popWindowCrestDistance && s2.CrestDistance > -2.5f && s2.BreakPhase >= 0.7f && s2.BreakPhase < 2.2f;
                if (nearLip)
                {
                    vel += Vector3.up * (tuning.popVerticalSpeed * board.popMultiplier * (0.6f + 0.4f * Mathf.Clamp01(vel.magnitude / 10f))) + (Vector3)n2 * 0.8f;
                    Enter(RiderState.Air);
                    Event("Pop");
                    return;
                }
                if (s2.Energy < 0.35f && s2.BreakPhase < 1.2f) { Enter(RiderState.KickOut); Event("Kick-out"); return; }
            }

            // tube
            InTube = s2.InTube;
            if (InTube)
            {
                TubeTime += dt; TotalTubeTime += dt;
                if (TubeTime > 0.3f && stateTime > 0.5f && LastEvent != "Tube") Event("Tube");
                if (s2.BreakPhase >= tuning.tubeCloseoutWipeoutPhase && s2.TubeDepth > 0.45f) { Wipeout("closeout"); return; }
            }
            else TubeTime = 0f;

            // left the wave footprint or went over the back
            if (s2.BreakPhase < 0f) { Enter(RiderState.KickOut); Event("Wave over"); return; }
            if (s2.CrestDistance < -2f && s2.BreakPhase > 0.2f) { Enter(RiderState.KickOut); Event("Over the back"); return; }

            // failure modes
            float speed = vel.magnitude;
            if (speed < tuning.minRideSpeedBeforeStall && s2.BreakPhase >= 1f) { stallTimer += dt; if (stallTimer > tuning.stallWipeoutTime) { Wipeout("stalled"); return; } }
            else stallTimer = 0f;
            if (n2.y < 0.35f && relSpeed < 2f) { Wipeout("too steep"); return; }
            if (s2.WhitewaterAmount > 0.75f && speed < 3.5f && s2.CrestDistance < 4f) { Wipeout("whitewater"); return; }
        }

        // ------------------------------------------------------------------ Air
        void UpdateAir(float dt, float t)
        {
            AirTime += dt;
            vel += Vector3.down * tuning.gravity * dt;
            vel -= vel * (vel.magnitude * tuning.airDrag * dt);
            pos += vel * dt;

            Vector2 rot = In.AirRotate;
            float mul = In.GrabHeld ? tuning.grabRateMultiplier : 1f;
            float spin = rot.x * tuning.spinRate * mul;
            float flip = -rot.y * tuning.flipRate * mul;
            if (In.ConsumeRollo() && rolloTimer <= 0f && stateTime < 0.6f) { rolloTimer = tuning.rolloDuration; Event("El Rollo"); }
            float roll = 0f;
            if (rolloTimer > 0f) { roll = 360f / tuning.rolloDuration; rolloTimer -= dt; }
            airRot = airRot * Quaternion.Euler(flip * dt, spin * dt, roll * dt);
            yaw += spin * dt;

            var s = Water.Sample(pos, t);
            lastSample = s;
            if (pos.y <= s.Height + 0.05f) { Land(s); return; }
            if (AirTime > tuning.maxAirTime) { Wipeout("fell"); }
        }

        void Land(WaterSample s)
        {
            Vector3 boardUp = airRot * Vector3.up;
            float align = Vector3.Dot(boardUp, (Vector3)s.Normal);
            rolloTimer = 0f;
            if (s.BreakPhase < 0f)
            {
                if (vel.y > -7f) { pos.y = s.Height - tuning.paddleDraft; vel = Vector3.zero; Enter(RiderState.Paddle); Event("Splash"); In.Rumble(0.4f, 0.2f, 0.15f); }
                else Wipeout("flat landing");
                return;
            }
            if (align >= tuning.landAlignMin)
            {
                vel = Vector3.ProjectOnPlane(vel, s.Normal) * 0.9f;
                pos.y = s.Height + tuning.rideDraft;
                AirsLanded++;
                lastLandedTime = Time.time;
                Enter(RiderState.Ride);
                Event(AirTime > 0.6f ? "Air landed!" : "Landed");
                In.Rumble(0.5f, 0.3f, 0.15f);
            }
            else if (align >= tuning.landSketchyMin)
            {
                vel = Vector3.ProjectOnPlane(vel, s.Normal) * 0.7f;
                pos.y = s.Height + tuning.rideDraft;
                Enter(RiderState.Ride);
                Event("Sketchy landing");
                In.Rumble(0.7f, 0.4f, 0.2f);
            }
            else Wipeout("bad landing");
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
            if (stateTime >= 1.0f) Enter(RiderState.Paddle);
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
                    target = Quaternion.LookRotation(F, n) * Quaternion.AngleAxis(-Lean * tuning.leanMax * Mathf.Clamp01(Speed / 6f), Vector3.forward);
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
