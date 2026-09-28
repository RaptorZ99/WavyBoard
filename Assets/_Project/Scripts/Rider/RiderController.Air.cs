using WavyBoard.Ocean;
using WavyBoard.Tricks;
using UnityEngine;

namespace WavyBoard.Rider
{
    public partial class RiderController
    {
        // The board's attitude in the air is two rotations: a BASE that levels itself for the landing (upright to the
        // water it is going to meet, nose along the way it travels on the wave), and the TRICK rotation played on top
        // of it in the board's own frame (spins, rolls, flips). Full rotations bring the trick part back to identity,
        // so a completed trick lands exactly on the base.
        Quaternion airBase = Quaternion.identity, airTrick = Quaternion.identity;
        int airWaveId = -1;              // the wave the air left from (-1: flat water, no return assist)
        Vector3 airD = Vector3.forward;  // its travel direction
        float airCelerity;               // its speed along D: the frame the flight is steered in
        bool airWasAbove;                // was above the water on the previous step (a landing comes down from above)
        float airPrevSurface;            // water height under the rider on the previous step
        float airLandEta = 1f;           // predicted seconds to touchdown
        Vector3 airLandNormal = Vector3.up;
        bool jumpUsedThisAir;
        bool airSettling;                // the player asked to spot the landing: stop turning and square up
        bool airFromTube;                // popped inside a barrel: the lip above is a ceiling

        Quaternion AirRot => airBase * airTrick;

        void BeginAir(RiderState from)
        {
            airFromTube = InTube;
            AirTime = 0f; AirSpin = 0f; AirFlip = 0f; AirRoll = 0f; AirPeak = 0f; GrabHeldInAir = false;
            PopEnergy = lastSample.Energy;
            tricks.ResetAccumulation();
            airSettling = false; jumpUsedThisAir = false; Crouch = 0f;
            airBase = bodyTarget; airTrick = Quaternion.identity;
            airLandNormal = lastSample.Normal;
            bool fromWave = (from == RiderState.Ride || from == RiderState.TakeOff) && lastSample.WaveId >= 0;
            airWaveId = fromWave ? lastSample.WaveId : -1;
            airD = lastSample.TravelDir;
            airCelerity = fromWave ? rideCelerity : 0f;
            if (from == RiderState.Paddle) RideTime = 0f;
            InTube = false; TubeTime = 0f;
            // clear of the water before the first ballistic step (paddling, the board sits in it)
            pos.y = Mathf.Max(pos.y, lastSample.Height + (fromWave ? 0.06f : 0.12f));
            airPrevSurface = lastSample.Height;
            airWasAbove = true;
        }

        // ------------------------------------------------------------------ Air
        void UpdateAir(float dt, float t)
        {
            AirTime += dt;
            float g = tuning.gravity * tuning.airGravityScale;

            // air drag relative to the wave's frame (the wave carries the air around its lip with it), then gravity
            Vector3 frame = airWaveId >= 0 ? airD * airCelerity : Vector3.zero;
            Vector3 rel = vel - frame;
            rel -= rel * (rel.magnitude * tuning.airDrag * dt);
            vel = frame + rel + Vector3.down * (g * dt);

            // a mouse pop released just after the lip threw you still pops (coyote time; the stick: see AirGesture)
            if (In.ConsumeJump(out float charge) && !jumpUsedThisAir && AirTime < tuning.jumpCoyoteTime && airWaveId >= 0)
            {
                vel.y += JumpPop(charge, tuning.jumpLipScale, Mathf.Max(0f, vel.y));
                jumpUsedThisAir = true;
                Event("Air");
            }

            airLandEta = TimeToFall(pos.y - lastSample.Height, vel.y, g);
            ReturnToFace(dt, g, t);
            pos += vel * dt;

            // right stick: flicked rotations (see TrickCatalog); left stick: your own spin, for as long as you hold it
            AirGesture();
            Vector3 d = tricks.Tick(dt);
            float mul = Grabbing ? tuning.grabRateMultiplier : 1f;
            float spin = In.Move.x * tuning.airSpinRate * mul * dt;
            d.x += spin;
            tricks.AddYaw(spin);
            airTrick = airTrick * Quaternion.Euler(d.y, d.x, d.z);
            AirSpin += d.x; AirFlip += d.y; AirRoll += d.z;
            GrabHeldInAir |= Grabbing;
            tricks.Grabbed |= Grabbing;
            AirAttitude(dt, Mathf.Abs(In.Move.x) > 0.2f);
            Vector3 nose = AirRot * Vector3.forward;
            if (nose.x * nose.x + nose.z * nose.z > 1e-4f) yaw = Mathf.Atan2(nose.x, nose.z) * Mathf.Rad2Deg;

            var s = Water.Sample(pos, t);
            lastSample = s;

            // popped inside the barrel: the roof stops the flight (off the lip, the rider punts through it)
            if (airFromTube && s.HasLipRoof && pos.y < s.LipRoofY && pos.y > s.LipRoofY - 0.5f && vel.y > 0f)
            {
                pos.y = s.LipRoofY - 0.5f;
                vel.y = -0.3f * vel.y;
            }
            AirPeak = Mathf.Max(AirPeak, pos.y - s.Height);

            // Touchdown: coming down onto the water from above. A surface that jumps up under the rider is the lip he is
            // flying through, not a landing.
            bool above = pos.y > s.Height + 0.05f;
            bool surfaceJumped = s.Height - airPrevSurface > 0.35f + Mathf.Abs(vel.y) * dt * 2f;
            if (!above && vel.y < 0.5f && AirTime > 0.08f)
            {
                if (airWasAbove && !surfaceJumped) { Land(s); return; }
                if (vel.y < -1f && pos.y < s.Height - 0.6f && !s.HasLipRoof) { pos.y = s.Height; Land(s); return; }
            }
            if (above) airWasAbove = true;
            else if (surfaceJumped) airWasAbove = false;
            airPrevSurface = s.Height;
            if (AirTime > tuning.maxAirTime) Wipeout("fell");
        }

        static float TimeToFall(float height, float vy, float g)
            => (vy + Mathf.Sqrt(Mathf.Max(0f, vy * vy + 2f * g * Mathf.Max(0f, height)))) / Mathf.Max(0.1f, g);

        /// <summary>
        /// Off a wave, the flight is bent back toward a landing spot on the face: the wave moves on under a rider who
        /// launched up and back off the lip, and without this he lands behind it. The spot is a share of the face width
        /// in front of the crest (so it scales with every wave size), the correction is spread over the time left in the
        /// air, and it only ever pulls hard toward the face — pushed out in front, it just holds back a little.
        /// </summary>
        void ReturnToFace(float dt, float g, float t)
        {
            if (airWaveId < 0) return;
            if (FindWave(airWaveId) == null) { airWaveId = -1; return; }
            var s = lastSample;
            if (s.WaveId != airWaveId) return;

            float cdTarget = Mathf.Max(0.6f, tuning.airLandingFaceShare * s.FaceWidth);
            Vector3 probe = pos + airD * (cdTarget - s.CrestDistance);
            var sl = Water.Sample(probe, t);
            if (sl.WaveId != airWaveId) return;
            airLandNormal = sl.Normal;
            AirLandingPoint = new Vector3(probe.x, sl.Height, probe.z);
            airLandEta = TimeToFall(pos.y - sl.Height, vel.y, g);
            if (tuning.airReturnAssist <= 0f) return;

            float eta = Mathf.Clamp(airLandEta, 0.2f, 2.5f);
            float vRelD = Vector3.Dot(vel, airD) - airCelerity;
            float need = (cdTarget - s.CrestDistance) / eta;
            float k = (1f - Mathf.Exp(-dt * tuning.airReturnRate)) * tuning.airReturnAssist;
            vel += airD * ((need - vRelD) * (need > vRelD ? k : 0.35f * k));
        }

        /// <summary>
        /// Levels the base attitude for the landing: upright between the horizon and the water ahead, nose along the way
        /// the board travels on the wave (or tail first when that is closer: it reverts on the water). In the last
        /// moments before touchdown, or when the player spots the landing, an unfinished spin is squared up to the
        /// nearest half turn.
        /// </summary>
        void AirAttitude(float dt, bool spinning)
        {
            Vector3 up = Vector3.Slerp(Vector3.up, airLandNormal, tuning.airLevelToSurface).normalized;
            Vector3 frame = airWaveId >= 0 ? airD * airCelerity : Vector3.zero;
            Vector3 cur = Vector3.ProjectOnPlane(airBase * Vector3.forward, up);
            Vector3 fwd = Vector3.ProjectOnPlane(vel - frame, up);
            if (fwd.sqrMagnitude < 0.25f) fwd = cur;
            if (fwd.sqrMagnitude < 1e-4f) return;
            if (cur.sqrMagnitude > 1e-4f && Vector3.Angle(cur, fwd) > 110f) fwd = -fwd;
            bool finalApproach = airLandEta < tuning.airLandingAssistTime;
            float rate = airSettling ? tuning.airSettleRate : tuning.airAutoLevelRate * (finalApproach ? 2f : 1f);
            airBase = Quaternion.Slerp(airBase, Quaternion.LookRotation(fwd.normalized, up), 1f - Mathf.Exp(-dt * rate));

            if ((finalApproach || airSettling) && !tricks.Busy && !spinning)
            {
                Vector3 f = airTrick * Vector3.forward;
                float yawNow = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float snapped = Mathf.Round(yawNow / 180f) * 180f;
                airTrick = Quaternion.Slerp(airTrick, Quaternion.Euler(0f, snapped, 0f), 1f - Mathf.Exp(-dt * (tuning.airSettleRate + 4f)));
            }
        }

        /// <summary>
        /// Three things decide a landing, the three a judge looks at: is the board flat to the water (align), is it
        /// pointing where it is travelling on the wave, nose or tail first (yaw error — coming down sideways is what
        /// really hurts), and had the rotation finished. Together they give an execution multiplier rather than a
        /// pass/fail, so a scrappy landing costs points instead of the wave. Forgiving off the wave, strict on a face.
        /// </summary>
        void Land(WaterSample s)
        {
            Vector3 n = s.Normal;
            Quaternion rot = AirRot;
            float align = Vector3.Dot(rot * Vector3.up, n);
            bool onWave = s.BreakPhase >= 0f && s.WaveId >= 0;
            float c = onWave ? (s.WaveId == airWaveId ? airCelerity : FindCelerity(s.WaveId)) : 0f;
            Vector3 frame = onWave ? (Vector3)s.TravelDir * (c * CarryFactor(in s)) : Vector3.zero;
            Vector3 travel = Vector3.ProjectOnPlane(vel - frame, n);
            Vector3 nose = Vector3.ProjectOnPlane(rot * Vector3.forward, n);
            float yawErr = travel.sqrMagnitude > 1f && nose.sqrMagnitude > 1e-4f ? Vector3.Angle(nose, travel) : 0f;
            bool tailFirst = yawErr > 90f;
            if (tailFirst) yawErr = 180f - yawErr;
            float impact = Mathf.Max(0f, -Vector3.Dot(vel - frame, n));
            bool stillSpinning = tricks.Busy;
            tricks.StopTracks();
            airSettling = false;

            float yawMax = Mathf.Max(5f, tuning.landYawMax);
            float need = onWave ? (stillSpinning ? tuning.landAlignMin + 0.08f : tuning.landAlignMin) : tuning.landSketchyMin;
            if (align < tuning.landSketchyMin * 0.75f && vel.y < -9f) { Wipeout("réception ratée"); return; }
            if (onWave && AirTime > 0.35f && yawErr > 75f && align < tuning.landAlignMin) { Wipeout("posé en travers"); return; }
            if (onWave && align < tuning.landSketchyMin) { Wipeout("réception ratée"); return; }

            bool clean = align >= need && yawErr <= yawMax && !stillSpinning;
            float quality = Mathf.SmoothStep(need, 0.995f, align) * (1f - Mathf.Clamp01(yawErr / yawMax));
            float execution = clean ? Mathf.Clamp(0.8f + 0.7f * quality, 0.8f, 1.5f) : 0.6f;
            if (nose.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(nose.x, nose.z) * Mathf.Rad2Deg;
            Vector3 slide = travel * (clean ? Mathf.Lerp(0.88f, 0.96f, quality) : 0.72f);
            ScoreAir(execution, !clean, onWave);   // before the revert below starts a manoeuvre of its own
            airWaveId = -1;
            lastSample = s;
            if (onWave)
            {
                rideCelerity = c;
                vel = frame + slide;
                pos.y = s.Height + tuning.rideDraft;
                AirsLanded++;
                lastLandedTime = Time.time;
                Enter(RiderState.Ride);
                if (tailFirst) BeginRevert(nose, travel, n);
            }
            else
            {
                pos.y = s.Height - tuning.paddleDraft;
                vel = new Vector3(vel.x, 0f, vel.z) * 0.6f;
                Enter(RiderState.Paddle);
            }
            OnLanded?.Invoke(impact, clean);
            In.Rumble(clean ? 0.5f : 0.7f, 0.3f, 0.15f);
        }

        /// <summary>Came down tail first: the board swings back round on the water, the shortest way.</summary>
        void BeginRevert(Vector3 nose, Vector3 travel, Vector3 n)
        {
            float sign = Vector3.Dot(Vector3.Cross(nose, travel), n) >= 0f ? 1f : -1f;
            var def = new TrickDef { name = "Revert", kind = TrickKind.Surface, yawDeg = 180f * sign, duration = 0.4f, points = 40f };
            tricks.Begin(def);
            BeginSurfaceTrick(def);
        }

        /// <summary>
        /// Names the air after what was actually turned and scores it: the programmed manoeuvres, or the rotation the
        /// player really turned by hand if that is worth more, plus the height. A plain air off the lip scores too.
        /// </summary>
        void ScoreAir(float execution, bool sketchy, bool onWave)
        {
            float rotPts = Mathf.Floor(Mathf.Abs(tricks.Yaw) / 180f + 0.2f) * 70f
                         + Mathf.Floor(Mathf.Abs(tricks.Roll) / 360f + 0.2f) * 160f
                         + Mathf.Floor(Mathf.Abs(tricks.Pitch) / 360f + 0.2f) * 220f;
            float heightPts = onWave ? 45f * Mathf.Clamp(AirPeak - 0.3f, 0f, 4f) : 0f;
            float pts = Mathf.Max(tricks.PendingPoints, rotPts) + heightPts;
            if (tricks.Grabbed) pts *= 1.15f;
            if (AirTime > 0.3f && pts >= 25f)
            {
                string name = tricks.NameRun();
                pts *= execution;
                TricksLanded++;
                OnTrick?.Invoke(name, pts);
                Event(sketchy ? name + " (sale)" : name);
            }
            else Event("Posé");
            tricks.ResetAccumulation();
        }
    }
}
