using WavyBoard.Ocean;
using WavyBoard.Tricks;
using UnityEngine;

namespace WavyBoard.Rider
{
    public partial class RiderController
    {
        // ------------------------------------------------------------------ Ride
        // Model: world velocity = carry (the wave transports the rider at its celerity while he is on the face)
        //        + relative velocity on the face (gravity down the slope, drag, rail grip, steering, pump, drive).
        //
        //   The water of a breaking face is not still in the wave's frame: it flows UP the face (the wave moves through
        //   the sea, the sea climbs it). Grip and drag act on the board's speed relative to that water, so a board
        //   angled a little down the face keeps its speed along the line while the rising water holds it at the same
        //   height: that is trimming, and it is where the speed to race a peeling wave comes from (see FaceFlow).
        //
        //   gravity along the slope x slopeGravityGain (1 -> 1.7 between slope 0.3 and 1) on the way down; on the way up
        //   x climbGravityScale and x climbDragScale on the drag: the face is rising water, a bottom turn carries you
        //   back to the lip. drag = quadraticDrag * v^2 + planingDamping * v (stall: stallDamping + x3.2 quadratic).
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

            float carryK = CarryFactor(in s0);
            Vector3 carry = D * (c * carryK);

            float relSpeed = relVel.magnitude;
            float fv = Mathf.Clamp((relSpeed + c * 0.3f) / 8f, 0.4f, 1.2f);
            float yawRate = tuning.yawRateMax * board.yawMultiplier * (DropKnee ? 1.25f : 1f);
            yaw += x * yawRate * fv * dt;
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
            float planing = In.StallHeld ? tuning.stallDamping : tuning.planingDamping;
            if (In.StallHeld) cd *= tuning.stallDragMultiplier;
            else if (climbing) { cd *= tuning.climbDragScale; planing *= tuning.climbDragScale; }
            cd *= trim > 0f ? Mathf.Lerp(1f, 0.85f, trim) : Mathf.Lerp(1f, 1.3f, -trim);
            Vector3 aDrag = -cd * wSpeed * w - w * planing;

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
                    float energyK = Mathf.Lerp(tuning.pumpEnergyFloor, 1f, Mathf.Clamp01(s0.Energy));
                    relVel += F * (tuning.pumpGainFactor * (relSpeed + c) + tuning.pumpGainFlat) * tuning.pumpBoost * energyK * (DropKnee ? 0.7f : 1f);
                    pumpsThisDescent++; PumpsThisRide++; PumpFlash = 1f;
                    Event("Pump");
                }
                else relVel *= tuning.pumpPenalty;
            }

            relVel += (aG + aDrag + aDrive + CrestPull(in s0, D)) * dt;

            // rail grip / slip and alignment of the board's speed through the water with its heading
            w = relVel - flow;
            float vF = Vector3.Dot(w, F), vR = Vector3.Dot(w, R), vN = Vector3.Dot(w, n);
            float gripTau = tuning.gripTau * board.gripMultiplier * (1f + 0.8f * (1f - Mathf.Abs(x))) * (s0.WhitewaterAmount > 0.5f ? 2.5f : 1f) * (DropKnee ? 0.8f : 1f);
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
            var s2 = Water.Sample(pos, t);

            // the top of the face: fly off it, or stay on it
            if (HandleFaceTop(in s0, ref s2, prevPos, D, dt)) return;
            lastSample = s2;
            Vector3 n2 = s2.Normal;
            pos.y = s2.Height + tuning.rideDraft;
            relVel -= n2 * Vector3.Dot(relVel, n2);
            TubeWall(in s2, n2);
            vel = carry + relVel;

            // kick-out on the shoulder
            if (In.ConsumeKickOut() && s2.Energy < 0.35f && s2.BreakPhase < 1.2f && !s2.InTube) { Enter(RiderState.KickOut); Event("Kick-out"); return; }

            // flick tricks: the right stick traces the gesture, the zone decides what it means (the mouse button pops too)
            Zone = ComputeZone();
            if (In.ConsumeJump(out float charge)) { Jump(charge, n2); return; }
            if (HandleGesture(dt, F, n2)) return;

            // tube
            InTube = s2.InTube;
            if (InTube)
            {
                TubeTime += dt; TotalTubeTime += dt;
                if (TubeTime > 0.3f && stateTime > 0.5f && LastEvent != "Tube") Event("Tube");
                if (s2.BreakPhase >= tuning.tubeCloseoutWipeoutPhase && s2.TubeDepth > 0.5f) { Wipeout("closeout"); return; }
            }
            else TubeTime = 0f;

            // left the wave footprint or went over the back
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
        /// The rider has just moved onto <paramref name="s2"/>. Decides what the top of the face does to him:
        ///   · climbing fast (or crouched, the stick pulled down to pop) near the top of the face, the water falls away
        ///     or the face ends: he is thrown into the air, the lip adding its own kick. A flick up right after still
        ///     reads as a flick at the lip (see AirGesture): hold down up the face, flick at the top;
        ///   · the face ends in an overhanging wall (a curl) and he is too slow: he stays on the face, the climb is lost
        ///     (the surface there is the top of the lip: never teleport onto it);
        ///   · a slow roll over a round crest is left to the crest pull and the over-the-back rule.
        /// Returns true when he took off.
        /// </summary>
        bool HandleFaceTop(in WaterSample s0, ref WaterSample s2, Vector3 prevPos, Vector3 D, float dt)
        {
            if (s0.WaveId < 0 || s2.WaveId != s0.WaveId || s0.BreakPhase < 0.3f) return false;
            float surfY = s2.Height + tuning.rideDraft;
            float prevSurfY = s0.Height + tuning.rideDraft;
            float vUp = vel.y;   // the carry is horizontal: this is the climb relative to the wave
            float g = tuning.gravity;

            bool overTop = s2.OnBack && !s0.OnBack;
            bool faceEnded = surfY - prevSurfY > 0.4f + Mathf.Abs(vUp) * dt * 3f;           // the surface jumped onto the lip
            bool fallsAway = prevPos.y + vUp * dt - 0.5f * g * dt * dt > surfY + 0.004f;       // convex crest, faster than gravity
            float H = Mathf.Max(1f, s0.WaveHeight);
            bool upperFace = s0.FaceTop - s0.Height < 0.4f * H;
            if (!(overTop || faceEnded || (fallsAway && upperFace))) return false;

            bool deepInTube = InTube && s0.BreakPhase > 1.45f && s0.HasLipRoof && s0.LipRoofY - s0.Height > tuning.tubeWallHeadroom * 0.8f;
            float minVy = In.Crouched ? tuning.lipLaunchMinVyCrouched : tuning.lipLaunchMinVy;
            if (!deepInTube && vUp > minVy)
            {
                // leave from the face, never from the far side of the lip
                if (faceEnded) pos = new Vector3(prevPos.x, prevPos.y + vUp * dt, prevPos.z);
                vel += Vector3.up * tuning.lipLaunchLift;
                Zone = WaveZone.Lip;
                LaunchAir("Envol");
                return true;
            }

            if (faceEnded || (overTop && s0.LipWidth > 0.3f))
            {
                // an overhanging wall above and not enough speed to leave it: stay on the face, lose the climb
                pos = prevPos;
                float vd = Vector3.Dot(relVel, D);
                if (vd < 0f) relVel -= D * vd;
                if (relVel.y > 0f) relVel.y = 0f;
                s2 = s0;
            }
            return false;
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
        /// letting him slide down the back (arcade). Nothing while he is climbing fast enough to take off.
        /// </summary>
        Vector3 CrestPull(in WaterSample s, Vector3 D)
        {
            if (tuning.crestPull <= 0f || s.WaveId < 0 || s.BreakPhase < 0.5f || s.BreakPhase > 2.4f) return Vector3.zero;
            if (vel.y > tuning.lipLaunchMinVy) return Vector3.zero;
            float H = Mathf.Max(1f, s.WaveHeight);
            float cd = s.CrestDistance;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f * H, -0.1f * H, cd))
                      * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.6f * H, -1f * H, cd)));
            return D * (tuning.crestPull * k);
        }

        /// <summary>Deep in an open barrel the wall under the ceiling cannot be ridden: the climb is taken away as the
        /// headroom runs out, so the rider stays on the floor of the tube instead of riding into the lip.</summary>
        void TubeWall(in WaterSample s, Vector3 n)
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
            relVel -= upFace * (k * 4f * Time.fixedDeltaTime);
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
