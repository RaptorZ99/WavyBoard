using WavyBoard.Ocean;
using WavyBoard.Tricks;
using UnityEngine;

namespace WavyBoard.Rider
{
    public partial class RiderController
    {
        // ------------------------------------------------------------------ Ride
        // Model: world velocity = carry (the wave transports the rider at its celerity while he is on the face)
        //        + relative velocity on the face (gravity down the slope, drag, rail grip, steering, the wave's power).
        //
        //   Speed along the line is the wave's: a breaking face drives a rider who trims along it toward the unbroken
        //   side (see WavePower), and the pace it holds him at is the curl's own (the sample's PeelSpeed), a little more
        //   when he leans on (stick forward), a little less when he sits back, much less with the stall. So whatever
        //   the section, the rider can always race the curl out of a tube, and choose to let it catch him. The power
        //   is in the pocket: out on the shoulder, far from the curl, it fades and the curl comes back.
        //   Pumping (in rhythm, anywhere on a live face, the tube included) adds bursts on top that the drag bleeds off.
        //
        //   The water of a breaking face flows UP the face in the wave's frame: grip and drag act on the board's speed
        //   relative to it (see FaceFlow), which is what holds a trimming rider at his height. With the steering stick
        //   centred the board holds its line (the height on the face) by itself; steer to climb or drop.
        //
        //   gravity along the slope x slopeGravityGain (1 -> 1.7 between slope 0.3 and 1) on the way down (drops make
        //   speed); on the way up x climbGravityScale and x climbDragScale on the drag (a bottom turn carries you back
        //   to the lip). drag = quadraticDrag * v^2 + planingDamping * v (stall: stallDamping + x stallDragMultiplier).
        //
        // The top of the face (see HandleFaceTop): climbing fast, the rider leaves the water there, whatever the wave
        // (a round crest, a pitching lip); too slow, the lip draws him back onto the face instead of over the back; deep
        // in the barrel the wall above cannot be climbed. Every distance is relative to the wave's own size.
        void UpdateRide(float dt, float t)
        {
            RideTime += dt;
            var s0 = lastSample;
            Vector3 n = s0.Normal;
            Vector3 D = s0.TravelDir;
            Vector2 mv = In.Move;
            float x = mv.x, trim = mv.y;
            float c = rideCelerity;

            // steering: brisk at any speed (a slow board turns easily); sitting back on the stall while steering pivots
            // the board round on its tail — the tight turn of a cutback, of a U-turn in the barrel
            bool stall = In.StallHeld;
            bool pivot = stall && Mathf.Abs(x) > 0.2f;
            float relSpeed = relVel.magnitude;
            float fv = Mathf.Clamp((relSpeed + c * 0.3f) / 8f, tuning.turnLowSpeedShare, 1.2f);
            float yawRate = tuning.yawRateMax * board.yawMultiplier * (DropKnee ? 1.25f : 1f) * (pivot ? tuning.pivotBoost : 1f);
            yaw += x * yawRate * fv * dt;

            float carryK = CarryFactor(in s0);
            // turning off the top (carving hard up there, not stalling): the lip carries him round with the wave
            if (Mathf.Abs(x) > 0.5f && !stall && !onBackLast && s0.FaceTop - s0.Height < 0.3f * Mathf.Max(1f, s0.WaveHeight))
                carryK = 1f;
            Vector3 carry = D * (c * carryK + HoldLine(in s0, x, dt));
            Lean = Mathf.Lerp(Lean, x, 1f - Mathf.Exp(-dt * 9f));

            Vector3 F = Vector3.ProjectOnPlane(Heading(), n).normalized;
            if (F.sqrMagnitude < 1e-4f) F = Heading();
            Vector3 R = Vector3.Cross(n, F).normalized;

            // gravity along the face: stronger on the steep part (the drop is where speed is made), softer when climbing
            Vector3 aG = -tuning.gravity * (Vector3.up - n * Vector3.Dot(Vector3.up, n));
            bool descending = Vector3.Dot(relVel, aG) > 0f;
            bool climbing = !descending && relSpeed > 0.5f;
            float ny = Mathf.Clamp(n.y, 0.05f, 1f);
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;
            float gGain = Mathf.Lerp(1f, tuning.slopeGravityGain, Mathf.Clamp01((slope - 0.3f) / 0.7f));
            if (climbing) gGain *= tuning.climbGravityScale;
            aG *= gGain;

            // the board planes on water that climbs the face: drag and grip act on the speed relative to it
            Vector3 flow = FaceFlow(in s0, n, c);
            Vector3 w = relVel - flow;
            float wSpeed = w.magnitude;

            // drag: quadratic (terminal speed of a drop) + a low linear planing damping; the stall button drags hard
            float cd = tuning.quadraticDrag * board.dragMultiplier;
            float planing = stall ? tuning.stallDamping : tuning.planingDamping;
            if (stall) cd *= tuning.stallDragMultiplier;
            else if (climbing) { cd *= tuning.climbDragScale; planing *= tuning.climbDragScale; }
            Vector3 aDrag = -cd * wSpeed * w - w * planing;

            // the wave's power along the line, and the pump on top of it
            Vector3 aPower = WavePower(in s0, F, n, w, aDrag, trim, stall);
            Pump(in s0, F, Vector3.Dot(w, F), t);

            relVel += (aG + aDrag + aPower + CrestPull(in s0, D)) * dt;

            // rail grip / slip and alignment of the board's speed through the water with its heading
            w = relVel - flow;
            float vF = Vector3.Dot(w, F), vR = Vector3.Dot(w, R), vN = Vector3.Dot(w, n);
            float gripTau = tuning.gripTau * board.gripMultiplier * (1f + 0.8f * (1f - Mathf.Abs(x))) * (s0.WhitewaterAmount > 0.5f ? 2.5f : 1f)
                            * (DropKnee ? 0.8f : 1f) * (pivot ? 1.8f : 1f);   // a pivot skids round
            vR *= Mathf.Exp(-dt / gripTau);
            RailSlip = Mathf.Abs(vR);
            BoardForward = F; BoardRight = R;
            w = F * vF + R * vR + n * vN;
            Vector3 vh = w - n * vN;
            float vhMag = vh.magnitude;
            if (vhMag > 0.3f)
            {
                Vector3 dir = vh / vhMag;
                float k = 1f - Mathf.Exp(-dt / tuning.alignTau);
                Vector3 target = Vector3.Dot(dir, F) >= 0f ? F : -F;
                dir = Vector3.Slerp(dir, target, k).normalized;
                w = dir * vhMag + n * vN;
            }
            relVel = flow + w;
            LineSpeed = Vector3.Dot(w, F);
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
            Vector3 prevPos = pos;
            pos += vel * dt;
            var s2 = Water.Sample(pos, now);

            // the top of the face: fly off it, or stay on it
            if (HandleFaceTop(in s0, ref s2, prevPos, carry, D, dt)) return;
            lastSample = s2;
            Vector3 n2 = s2.Normal;
            pos.y = s2.Height + tuning.rideDraft;
            relVel -= n2 * Vector3.Dot(relVel, n2);
            TubeWall(in s2, n2, dt);
            vel = carry + relVel;

            // flick tricks: the right stick traces the gesture, the zone decides what it means (the mouse button pops too)
            Zone = ComputeZone();
            if (In.ConsumeJump(out float charge)) { Jump(charge, n2); return; }
            if (HandleGesture(dt, F, n2)) return;

            // tube
            InTube = s2.InTube;
            if (InTube)
            {
                TubeTime += dt; TotalTubeTime += dt;
                if (TubeTime > 0.3f && TubeTime - dt <= 0.3f) Event("Tube");   // once per tube
                if (s2.BreakPhase >= tuning.tubeCloseoutWipeoutPhase && s2.TubeDepth > 0.5f) { Wipeout("closeout"); return; }
            }
            else TubeTime = 0f;

            // left the wave footprint, or went over the back (the way to leave a wave: over its shoulder)
            float H = Mathf.Max(1f, s2.WaveHeight);
            if (s2.BreakPhase < 0f) { Enter(RiderState.KickOut); Event("Wave over"); return; }
            if (s2.CrestDistance < -(0.9f * H + 0.5f) && s2.BreakPhase > 0.2f) { Enter(RiderState.KickOut); Event("Over the back"); return; }

            // failure modes
            float speed = vel.magnitude;
            if (speed < tuning.minRideSpeedBeforeStall && s2.BreakPhase >= 1f) { stallTimer += dt; if (stallTimer > tuning.stallWipeoutTime) { Wipeout("stalled"); return; } }
            else stallTimer = 0f;
            if (s2.WhitewaterAmount > 0.75f && relVel.magnitude < tuning.whitewaterWipeoutSpeed && s2.BreakPhase > 2.2f && s2.CrestDistance < 0.6f * H) { Wipeout("whitewater"); return; }
        }

        /// <summary>
        /// 0..1: how much of the breaking wave's power reaches a rider here. All of it on a live face (from the moment
        /// it stands up until it is foam), in the tube as much as in front of the curl; it fades out on the shoulder
        /// the further he gets ahead of the curl (in units of the wave's height), and is gone off the face and in the
        /// whitewater.
        /// </summary>
        float PowerZone(in WaterSample s)
        {
            if (s.WaveId < 0) return 0f;
            float fw = Mathf.Max(1f, s.FaceWidth);
            float H = Mathf.Max(1f, s.WaveHeight);
            float onFace = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f * fw, 0.1f * fw, s.CrestDistance))
                           * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f * fw, 1.35f * fw, s.CrestDistance)));
            float live = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, s.BreakPhase))
                         * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.45f, 2.8f, s.BreakPhase)));
            float foam = 1f - Mathf.Clamp01((s.WhitewaterAmount - 0.4f) * 1.6f);
            float ahead = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tuning.powerFullAhead * H, tuning.powerFadedAhead * H, s.PeelDistance));
            return onFace * live * foam * Mathf.Lerp(1f, tuning.powerShoulderShare, ahead);
        }

        /// <summary>
        /// The breaking wave drives a rider trimming along its face toward the unbroken side: below the pace it holds
        /// (the curl's speed x the trim — see <see cref="RiderTuning.trimNeutral"/>) it cancels the drag and closes the
        /// gap; above it, it lets go and the drag brings him back. Ridden back toward the curl (a cutback) it still
        /// carries him, at a slower pace (<see cref="RiderTuning.cutbackPace"/>), all the way to the curl — so he comes
        /// round again with speed — but not deeper into the barrel. Straight down or up the face it does nothing (that
        /// is gravity's). A drop or pumps take him over the pace for a while.
        /// </summary>
        Vector3 WavePower(in WaterSample s, Vector3 F, Vector3 n, Vector3 w, Vector3 aDrag, float trim, bool stall)
        {
            float power = PowerZone(in s);
            // the curl's pace, but never a crawl: where the peel dies out in the channel the rider still trims on
            float vp = Mathf.Max(s.PeelSpeed > 0.5f ? s.PeelSpeed : 6f, tuning.paceFloor);
            float margin = stall ? tuning.trimStall
                         : trim >= 0f ? Mathf.Lerp(tuning.trimNeutral, tuning.trimForward, trim) : Mathf.Lerp(tuning.trimNeutral, tuning.trimBack, -trim);
            Vector3 line = Vector3.ProjectOnPlane(s.CrestDir, n);
            float along = line.sqrMagnitude > 1e-4f ? Vector3.Dot(F, line.normalized) : 0f;
            if (along < 0f)
            {
                // back toward the curl: a slower pace, fading out once inside the barrel
                float H = Mathf.Max(1f, s.WaveHeight);
                margin = tuning.cutbackPace * (1f + 0.3f * trim) - 1f;
                power *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.2f * H, -0.3f * H, s.PeelDistance));
            }
            // weaker water (out on the shoulder, off the face, in the foam) holds a slower pace
            float target = power > 0f ? vp * (1f + margin) * Mathf.Lerp(tuning.powerShoulderShare, 1f, power) : 0f;
            Power = power;
            PaceTarget = target;
            if (power <= 0f) return Vector3.zero;
            float lineK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, Mathf.Abs(along)));
            if (lineK <= 0f) return Vector3.zero;

            float deficit = target - Vector3.Dot(w, F);
            float hold = Mathf.Clamp01(1f + deficit / Mathf.Max(0.1f, tuning.powerHoldBand));
            float a = hold * Mathf.Max(0f, -Vector3.Dot(aDrag, F)) + Mathf.Max(0f, deficit) * tuning.powerRate;
            return F * (a * power * lineK);
        }

        /// <summary>
        /// A pump: a press of the pump button in rhythm (not mashed) on a live face — down the face, along it, in the
        /// tube — pushes the board on. What it gives shrinks the further the rider already is over the wave's pace, so
        /// pumping takes him a few m/s over it at most; mashed faster than <see cref="RiderTuning.pumpWindowMin"/> it
        /// only costs speed.
        /// </summary>
        void Pump(in WaterSample s, Vector3 F, float lineSpeed, float t)
        {
            if (!In.ConsumePump()) return;
            float since = t - lastPumpTime;
            lastPumpTime = t;
            bool onLiveFace = s.WaveId >= 0 && s.BreakPhase > 0.3f && s.BreakPhase < 2.6f && s.CrestDistance > -0.2f * s.FaceWidth;
            if (since < tuning.pumpWindowMin || !onLiveFace) { relVel *= tuning.pumpPenalty; return; }
            float over = Mathf.Max(0f, lineSpeed - PaceTarget);
            float gain = tuning.pumpGain * Mathf.Lerp(tuning.pumpEnergyFloor, 1f, Power) * Mathf.Clamp01(1f - over / Mathf.Max(0.5f, tuning.pumpOverPace));
            relVel += F * (gain * (DropKnee ? 0.75f : 1f));
            PumpFlash = 1f;
            Event("Pump");
        }

        /// <summary>
        /// Steering stick centred, the board holds its line: the nose eases onto the line (along the crest, the way it
        /// already points) and the rider stays at the height on the face where the player left him — a share of the
        /// wave's height, so it follows the face as it grows and pitches. The height is held by the wave carrying him
        /// a little up or down its face (m/s along D, returned), damped by how fast he is actually rising or sinking.
        /// Nothing while he steers, nor when the board points up or down the face (a manoeuvre).
        /// </summary>
        float HoldLine(in WaterSample s, float steer, float dt)
        {
            float H = Mathf.Max(1f, s.WaveHeight);
            float y = pos.y - s.SeaLevel;
            float rate = dt > 0f ? (y - lineYLast) / dt : 0f;      // m/s: rising or sinking on the face
            lineYLast = y;
            float free = 1f - Mathf.Clamp01(Mathf.Abs(steer) / 0.2f);
            if (free < 1f || lineShare < 0f) lineShare = y / H;    // steering: the line is wherever he takes the board
            if (free <= 0f || tuning.lineHoldGain <= 0f || s.WaveId < 0) return 0f;

            Vector3 T = s.CrestDir;
            float along = Vector3.Dot(Heading(), T);
            float alongK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.85f, Mathf.Abs(along)));
            if (alongK <= 0f) return 0f;
            float lineYaw = Mathf.Atan2(T.x, T.z) * Mathf.Rad2Deg + (along < 0f ? 180f : 0f);
            yaw = Mathf.LerpAngle(yaw, lineYaw, (1f - Mathf.Exp(-dt * tuning.lineAlignRate)) * free * alongK);

            float ny = Mathf.Clamp(s.Normal.y, 0.2f, 1f);
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;   // the face drops this much per metre toward the beach
            if (slope < 0.15f) return 0f;
            float want = (lineShare * H - y) * tuning.lineHoldGain;
            float vD = -(want - rate) / Mathf.Max(0.4f, slope);
            return Mathf.Clamp(vD, -tuning.lineHoldMaxSpeed, tuning.lineHoldMaxSpeed) * free * alongK;
        }

        /// <summary>
        /// The rider has just moved onto <paramref name="s2"/>. Decides what the top of the face does to him:
        ///   · stalling out on the shoulder with the board pointed over the wave's back (see <see cref="KickOutIntent"/>),
        ///     near the top: he pulls out over it, the ride is over — the way out of a wave;
        ///   · climbing fast (or crouched, the stick pulled down to pop) near the top of the face, the water falls away
        ///     or the face ends: he is thrown into the air, the lip adding its own kick. A flick up right after still
        ///     reads as a flick at the lip (see AirGesture): hold down up the face, flick at the top;
        ///   · carving hard (steering), he turns off the top instead of flying (a top turn: to fly, let the steering go
        ///     or crouch) and the crest does not let him over;
        ///   · the face ends in an overhanging wall (a curl) and he is too slow: he stays on the face, the climb is lost
        ///     (the surface there is the top of the lip: never teleport onto it);
        ///   · a slow roll over a round crest is left to the crest pull and the over-the-back rule.
        /// Returns true when he left the face (in the air, or out of the wave).
        /// </summary>
        bool HandleFaceTop(in WaterSample s0, ref WaterSample s2, Vector3 prevPos, Vector3 carry, Vector3 D, float dt)
        {
            if (s0.WaveId < 0 || s2.WaveId != s0.WaveId || s0.BreakPhase < 0.3f) return false;
            float surfY = s2.Height + tuning.rideDraft;
            float prevSurfY = s0.Height + tuning.rideDraft;
            float vUp = vel.y;   // the carry is horizontal: this is the climb relative to the wave
            float g = tuning.gravity;

            bool overTop = s2.OnBack && !onBackLast;
            bool faceEnded = surfY - prevSurfY > 0.4f + Mathf.Abs(vUp) * dt * 3f;           // the surface jumped onto the lip
            bool fallsAway = prevPos.y + vUp * dt - 0.5f * g * dt * dt > surfY + 0.004f;       // convex crest, faster than gravity
            float H = Mathf.Max(1f, s0.WaveHeight);
            bool upperFace = s0.FaceTop - s0.Height < 0.4f * H;
            bool atTop = overTop || faceEnded || (fallsAway && upperFace);

            // pulling out over the shoulder: at the top of it, however fast (it would fly him off), or slowly near it
            float leaving = KickOutIntent(in s0);
            if (leaving > 0.5f && (atTop || s0.FaceTop - s0.Height < 0.3f * H))
            {
                Enter(RiderState.KickOut);
                Event("Sortie");
                return true;
            }
            if (!atTop) return false;

            bool deepInTube = InTube && s0.BreakPhase > 1.45f && s0.HasLipRoof && s0.LipRoofY - s0.Height > tuning.tubeWallHeadroom * 0.8f;
            // crouched he asked for air; carving hard (steering) he is turning off the lip, not leaving it
            float minVy = In.Crouched ? tuning.lipLaunchMinVyCrouched
                        : Mathf.Lerp(tuning.lipLaunchMinVy, tuning.lipLaunchMinVyCarving, Mathf.InverseLerp(0.4f, 0.8f, Mathf.Abs(In.Move.x)));
            if (!deepInTube && vUp > minVy)
            {
                // leave from the face, never from the far side of the lip
                if (faceEnded) pos = new Vector3(prevPos.x, prevPos.y + vUp * dt, prevPos.z);
                vel += Vector3.up * tuning.lipLaunchLift;
                Zone = WaveZone.Lip;
                LaunchAir("Envol");
                return true;
            }

            // carving hard he turns off the top (a top turn): the crest is where he comes round, not where he leaves
            bool carving = Mathf.Abs(In.Move.x) > 0.5f && !In.StallHeld;
            if (faceEnded || (overTop && (s0.LipWidth > 0.3f || carving)))
            {
                // an overhanging wall above, or a top turn, and not flying off: stay on the face, lose the climb (the
                // wave still carries him on this step)
                pos = prevPos + carry * dt;
                float vd = Vector3.Dot(relVel, D);
                if (vd < 0f) relVel -= D * vd;
                if (relVel.y > 0f) relVel.y = 0f;
                s2 = Water.Sample(pos, now);
            }
            return false;
        }

        /// <summary>
        /// 0..1: the rider means to leave the wave — sitting back on the stall, out on the shoulder away from the curl,
        /// the board pointed over its back, near the top of the face. There he pulls out (see HandleFaceTop); a flight
        /// off it is left alone and lands behind the wave. Without the stall, or in the pocket, it is a manoeuvre (a top
        /// turn, a hit): the lip holds him, an air is steered back onto the face. (A stalled pivot is turned low on the
        /// face, not at its top.)
        /// </summary>
        float KickOutIntent(in WaterSample s)
        {
            if (!In.StallHeld) return 0f;
            float H = Mathf.Max(1f, s.WaveHeight);
            float shoulder = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f * H, 1.8f * H, s.PeelDistance));
            float overTheBack = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.45f, -Vector3.Dot(Heading(), (Vector3)s.TravelDir)));
            return shoulder * overTheBack;
        }

        /// <summary>
        /// Speed of the water up the face, in the wave's frame: strongest on a steep, live face (from the moment it stands
        /// up until the section collapses), nothing on the flat in front, on the back or in the whitewater. Scales with
        /// the wave's celerity, so bigger waves have more power.
        /// </summary>
        Vector3 FaceFlow(in WaterSample s, Vector3 n, float c)
        {
            if (tuning.faceFlow <= 0f || s.WaveId < 0) return Vector3.zero;
            float fw = Mathf.Max(1f, s.FaceWidth);
            float onFace = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f * fw, 0.15f * fw, s.CrestDistance))
                           * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f * fw, 1.25f * fw, s.CrestDistance)));
            float live = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, s.BreakPhase))
                         * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 2.7f, s.BreakPhase)));
            float steep = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, 1f - n.y * n.y)) / 0.5f);
            float k = onFace * live * steep * (1f - Mathf.Clamp01(s.WhitewaterAmount * 1.5f));
            if (k <= 0f) return Vector3.zero;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n);
            if (up.sqrMagnitude < 1e-4f) return Vector3.zero;
            return up.normalized * (tuning.faceFlow * c * k);
        }

        /// <summary>
        /// Just past the top of a real face and too slow to fly: the lip draws the rider back onto the face instead of
        /// letting him slide down the back (arcade). Nothing while he is climbing fast enough to take off. It holds in
        /// the pocket, and it holds a rider carving hard anywhere (a top turn); out on the shoulder, away from the
        /// curl, it lets go — at once for a stalled board pointed over the top (he means to leave) — and a dying wave
        /// holds nobody: over the shoulder is the way out of a wave.
        /// </summary>
        Vector3 CrestPull(in WaterSample s, Vector3 D)
        {
            if (tuning.crestPull <= 0f || s.WaveId < 0 || s.BreakPhase < 0.5f || s.BreakPhase > 2.4f) return Vector3.zero;
            if (vel.y > tuning.lipLaunchMinVy) return Vector3.zero;
            float H = Mathf.Max(1f, s.WaveHeight);
            float cd = s.CrestDistance;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f * H, -0.1f * H, cd))
                      * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.6f * H, -1f * H, cd)));
            float shoulder = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f * H, 2.5f * H, s.PeelDistance));
            float dying = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 1.5f, s.PeelSpeed));
            // a rider carving hard is turning off the top (a top turn): the lip holds him wherever he is on the wave
            float carving = Mathf.InverseLerp(0.4f, 0.8f, Mathf.Abs(In.Move.x)) * (In.StallHeld ? 0f : 1f);
            float release = Mathf.Lerp(Mathf.Lerp(1f, 0.2f, shoulder), 1.6f, carving);
            return D * (tuning.crestPull * k * release * (1f - KickOutIntent(in s)) * (1f - dying));
        }

        /// <summary>Deep in an open barrel the wall under the ceiling cannot be ridden: the climb is taken away as the
        /// headroom runs out, so the rider stays on the floor of the tube instead of riding into the lip.</summary>
        void TubeWall(in WaterSample s, Vector3 n, float dt)
        {
            if (!s.InTube || !s.HasLipRoof || s.BreakPhase < 1.45f) return;
            float headroom = s.LipRoofY - s.Height;
            float k = Mathf.Clamp01(1f - headroom / Mathf.Max(0.1f, tuning.tubeWallHeadroom));
            if (k <= 0f) return;
            Vector3 upFace = Vector3.ProjectOnPlane(Vector3.up, n);
            if (upFace.sqrMagnitude < 1e-4f) return;
            upFace.Normalize();
            float vu = Vector3.Dot(relVel, upFace);
            if (vu > 0f) relVel -= upFace * vu * Mathf.Clamp01(k * 1.5f);
            relVel -= upFace * (k * 4f * dt);
        }

        // ------------------------------------------------------------------ Jump
        /// <summary>
        /// Upward speed a jump adds. The rider's own pop (a tap gives <see cref="RiderTuning.jumpTapShare"/> of it, a
        /// full charge all of it) grows with the speed on the face; on top of a climb that already flies, it adds less
        /// (the lip did part of the work).
        /// </summary>
        float JumpPop(float charge, float zoneScale, float vUpAlready)
        {
            float pop = (tuning.popVerticalSpeed + tuning.popSpeedGain * (FaceSpeed + 2f)) * board.popMultiplier
                        * Mathf.Lerp(tuning.jumpTapShare, 1f, Mathf.Clamp01(charge)) * zoneScale;
            return pop * (1f - 0.5f * Mathf.Clamp01(vUpAlready / 5f));
        }

        /// <summary>The mouse pop on the water (the stick's straight flick up does the same through the trick catalog):
        /// a big air at the lip, a chop hop on the face, a hop in the tube.</summary>
        void Jump(float charge, Vector3 n)
        {
            float zoneScale;
            string name;
            switch (Zone)
            {
                case WaveZone.Lip: zoneScale = tuning.jumpLipScale; name = "Air"; break;
                case WaveZone.Tube: zoneScale = tuning.jumpTubeScale; name = "Hop"; break;
                case WaveZone.Face: zoneScale = tuning.jumpFaceScale; name = "Chop hop"; break;
                default: zoneScale = tuning.jumpFlatScale; name = "Hop"; break;
            }
            vel += Vector3.up * JumpPop(charge, zoneScale, Mathf.Max(0f, vel.y)) + n * (0.7f * zoneScale);
            LaunchAir(name);
            jumpUsedThisAir = true;
        }

        /// <summary>A hop off flat water while paddling.</summary>
        void Hop(float charge)
        {
            vel.y += tuning.popVerticalSpeed * board.popMultiplier * Mathf.Lerp(tuning.jumpTapShare, 1f, charge) * tuning.jumpFlatScale * 1.4f;
            LaunchAir("Hop");
            jumpUsedThisAir = true;
        }

        void LaunchAir(string evt)
        {
            Enter(RiderState.Air);
            Event(evt);
            In.Rumble(0.25f, 0.45f, 0.09f);
        }
    }
}
