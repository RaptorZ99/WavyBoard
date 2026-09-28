using WavyBoard.InputSys;
using WavyBoard.Ocean;
using WavyBoard.Rider;
using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.CameraRig
{
    /// <summary>
    /// The surf camera: one procedural rig that reads the rider and the wave and frames the shot a surf film would.
    ///
    /// <b>Ride.</b> The camera sits in front of the face, on the beach side, trailing the rider along the line: it looks
    /// the way he is going, with the wall on one side of the frame and the open water on the other. Which way is
    /// "along the line" is read from his motion (with hysteresis), and when he turns back the camera swings round IN
    /// FRONT of the face to the other side — it never crosses the wave. It is anchored halfway to the wave, so the rider
    /// visibly moves up and down the face (and flies above it) instead of the wave moving round a locked rider.
    ///
    /// <b>Tube.</b> Inside the barrel, a few metres deeper than the rider, placed in the actual cavity of the wave
    /// (between the face and the lip, from the analytic profile) and looking out at the mouth. The mouth is always the
    /// same way (the wave peels toward +T), so whichever way the rider rides in there — racing out, or coming in on a
    /// cutback and turning round — the camera stays on the deep side: it never has to swing through the barrel. When
    /// the barrel behind him has already closed it comes in closer; blends in and out over a fraction of a second.
    ///
    /// <b>Changing direction.</b> The ride shot follows the way the board is committed to (see
    /// <see cref="SurfCameraMath.LineSide"/>): nose and motion pointing the other way along the line — a cutback, a
    /// pivot — and it swings round at once; the slide of a top turn or a spin in the air does not move it.
    ///
    /// <b>Air, paddle, wipeout.</b> Pulls back and up in the air, showing where he will land; chases the board while
    /// paddling and turns round to show a wave standing up behind a rider paddling for it; holds still in a wipeout.
    ///
    /// Every distance scales with the size of the wave being ridden. Whatever the shot asks for, a spring arm keeps the
    /// camera out of the water: the line from the rider to the camera is tested against the wave's water volume (the
    /// inside of a tube is air), the camera comes in at once when the water gets in the way and eases back out.
    ///
    /// The main camera is driven directly.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class CameraDirector : MonoBehaviour
    {
        public RiderController rider;

        [Header("Ride: in front of the face, trailing the rider along the line")]
        [Tooltip("Angle (deg) from straight in front of the face round to behind the rider")]
        public float rideAngle = 50f;
        public float rideDistance = 3.8f;
        public float rideDistancePerH = 0.75f;
        public float rideDistancePerSpeed = 0.12f;
        public float rideHeight = 1.2f;
        public float rideHeightPerH = 0.32f;
        [Tooltip("Share of the rider's moves on the face the camera does NOT follow (0 = locked to the rider)")]
        [Range(0f, 0.9f)] public float waveAnchoring = 0.45f;
        public float rideLookAhead = 1.4f;
        public float rideLookAheadPerSpeed = 0.2f;
        public float rideLookUpPerH = 0.16f;
        [Tooltip("Speed along the line (m/s), the way the board points, that commits a change of direction")]
        public float sideSwitchSpeed = 1f;
        [Tooltip("Seconds the rider must stay committed to the other way before the camera swings round")]
        public float sideSwitchDelay = 0.15f;
        [Tooltip("Seconds the camera takes to swing round to the other side")]
        public float swingTime = 0.45f;

        [Header("Tube: inside the barrel, deeper than the rider, looking out at the mouth")]
        public SurfCameraMath.TubeShotSettings tubeShot = SurfCameraMath.TubeShotSettings.Default;
        public float tubeFov = 80f;
        public float tubeBlendIn = 0.3f;
        public float tubeBlendOut = 0.55f;
        [Tooltip("Seconds out of the tube before the camera leaves it (no flicker when the lip opens for a moment)")]
        public float tubeExitHold = 0.35f;

        [Header("Air")]
        public float airExtraDistance = 1.4f;
        public float airExtraHeight = -0.3f;
        [Tooltip("In the air the camera stays down with the wave (this share of the rider's height is not followed): he flies up the frame")]
        [Range(0f, 1f)] public float airVerticalAnchoring = 0.85f;
        [Range(0f, 1f)] public float airLookToLanding = 0.3f;
        public float airFov = 4f;

        [Header("Paddle")]
        public float paddleDistance = 5.4f;
        public float paddleHeight = 2.1f;
        public float paddleLookAhead = 3f;
        [Tooltip("A wave standing up this close behind a rider paddling for the beach turns the camera round to show it")]
        public float watchRange = 34f;

        [Header("Lens and feel")]
        public float baseFov = 58f;
        public float speedFov = 12f;
        public float dutchPerLean = 5f;
        public float offsetSmoothTime = 0.22f;
        public float lookSmoothTime = 0.12f;
        [Tooltip("Seconds the camera takes to ease back out after the water pushed it in")]
        public float armRecoverTime = 0.35f;
        [Tooltip("Never closer to the rider than this (m): when the water blocks the shot, the camera looks for a clear\nangle (higher, then out in front of the face) instead of diving into him")]
        public float minArmLength = 1.8f;
        public float waterClearance = 0.45f;
        [Tooltip("Degrees of shake at full trauma")]
        public float shakeAmplitude = 1.6f;

        Camera cam;
        Vector3 D = Vector3.forward, T = Vector3.right;
        float H = 3f, hVel;
        SurfCameraMath.LineSide lineSide = new SurfCameraMath.LineSide { Side = 1f };
        float theta, thetaVel;
        const float ExitSide = 1f;   // the wave peels toward +T (see SurfSpotConfig): the mouth of every tube is that way
        float wWave, wWaveVel, wTube, wTubeVel, wAir, wAirVel, wWatch, wWatchVel, wWipe, wWipeVel;
        bool tubeActive; float tubeExitTimer; bool tubeShotValid;
        Vector3 tubeOffset, tubeLook;
        Vector3 offset, look, lookVel, armDir = Vector3.back;
        float armLen, armLenVel;
        float arm = 10f, armVel;
        Vector3 avoidOffset, avoidVel;
        float avoidW, avoidWVel;
        float fov, fovVel, dutch;
        Vector2 orbit; float orbitIdle;
        float trauma;
        bool wipeFrozen; Vector3 wipeCamPos;
        Vector3 camPos;
        bool initialised;

        void Start()
        {
            cam = Camera.main;
            if (cam == null) { Debug.LogError("CameraDirector: no Main Camera"); enabled = false; return; }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            fov = baseFov;
            if (rider != null)
            {
                rider.OnLanded += OnLanded;
                rider.OnEvent += OnRiderEvent;
                if (rider.spot != null) { D = rider.spot.TravelDir; T = rider.spot.CrestDir; }
            }
        }

        void OnDestroy()
        {
            if (rider == null) return;
            rider.OnLanded -= OnLanded;
            rider.OnEvent -= OnRiderEvent;
        }

        void OnLanded(float impact, bool clean) => AddTrauma((clean ? 0.18f : 0.4f) + Mathf.Clamp01(impact / 12f) * 0.35f);

        void OnRiderEvent(string e)
        {
            if (e.StartsWith("Wipeout")) AddTrauma(0.75f);
            else if (e == "Envol") AddTrauma(0.12f);
        }

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>The shot being played, for the debug overlay.</summary>
        public string ShotName => wWipe > 0.5f ? "wipeout" : wTube > 0.5f ? "tube" : wAir > 0.5f ? "air" : wWave > 0.5f ? "ride" : wWatch > 0.5f ? "wave coming" : "paddle";

        /// <summary>How the shot is being kept out of the water, for the debug overlay and the playtests: the arm asked
        /// for, what the water leaves of it, and how much of an avoiding angle is blended in.</summary>
        public string ArmState { get; private set; } = "";

        void LateUpdate()
        {
            if (rider == null || cam == null || rider.tuning == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var s = rider.Sample;
            var state = rider.State;
            Vector3 anchor = rider.RenderPosition + Vector3.up * 0.45f;
            double time = Time.timeAsDouble;
            var water = WaterSurfaceComposite.Instance;

            // ---------------------------------------------------------------- the wave being ridden
            bool riding = state == RiderState.TakeOff || state == RiderState.Ride
                          || (state == RiderState.Air && rider.AirWaveId >= 0);
            SurfWave wave = water != null ? water.FindWave(riding ? (s.WaveId >= 0 ? s.WaveId : rider.AirWaveId) : -1) : null;
            float incomingXi = float.MaxValue;
            SurfWave incoming = riding ? null : FindIncoming(water, anchor, time, out incomingXi);
            SurfWave framed = wave != null ? wave : incoming;
            if (framed != null)
            {
                D = framed.Params.travelDir; T = framed.Params.crestDir;
                H = Mathf.SmoothDamp(H, framed.Params.height, ref hVel, 0.8f);
            }
            else H = Mathf.SmoothDamp(H, 2.5f, ref hVel, 2f);

            // ---------------------------------------------------------------- which way along the line
            if (!riding) lineSide.Reset();    // a new ride starts toward the unbroken shoulder (+T)
            else if (state == RiderState.Ride)   // on the water only: a spin in the air is not a change of direction
                lineSide.Update(Vector3.Dot(rider.HeadingDir, T), Vector3.Dot(rider.Velocity, T), sideSwitchSpeed, sideSwitchDelay, dt);
            float side = lineSide.Side;
            theta = Mathf.SmoothDamp(theta, -side * rideAngle, ref thetaVel, swingTime);

            // ---------------------------------------------------------------- mode weights
            // in the tube, or about to be (the lip pitching over him): from out in front of the face the curtain would
            // come between him and the camera, from inside the barrel behind him it frames him
            bool inTubeNow = riding && (rider.InTube && rider.TubeTime > 0.1f || UnderTheCurl(in s));
            if (inTubeNow) { tubeActive = true; tubeExitTimer = 0f; }
            else if (tubeActive)
            {
                tubeExitTimer += dt;
                if (tubeExitTimer > tubeExitHold || !riding) tubeActive = false;
            }
            if (tubeActive && wave != null)
            {
                tubeShotValid = SurfCameraMath.TryTubeShot(wave, anchor, ExitSide, H, time, in tubeShot, out Vector3 tubeCam, out Vector3 tubeAim);
                if (tubeShotValid) { tubeOffset = tubeCam - anchor; tubeLook = tubeAim - anchor; }
            }
            else if (!tubeActive) tubeShotValid = false;
            bool airborne = state == RiderState.Air;
            bool wiping = state == RiderState.Wipeout;
            bool watching = !riding && incoming != null && Vector3.Dot(rider.HeadingDir, D) > 0.2f
                            && (state == RiderState.Paddle || state == RiderState.DuckDive);
            float tubeTarget = tubeActive && tubeShotValid ? 1f : 0f;
            wTube = Mathf.SmoothDamp(wTube, tubeTarget, ref wTubeVel, tubeTarget > wTube ? tubeBlendIn : tubeBlendOut);
            wWave = Mathf.SmoothDamp(wWave, riding ? 1f : 0f, ref wWaveVel, riding ? 0.35f : 0.8f);
            wAir = Mathf.SmoothDamp(wAir, airborne && riding ? 1f : 0f, ref wAirVel, airborne ? 0.3f : 0.45f);
            float watchK = watching ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(watchRange, watchRange * 0.45f, incomingXi)) : 0f;
            wWatch = Mathf.SmoothDamp(wWatch, watchK, ref wWatchVel, 0.6f);
            wWipe = Mathf.SmoothDamp(wWipe, wiping ? 1f : 0f, ref wWipeVel, wiping ? 0.15f : 0.7f);

            // ---------------------------------------------------------------- the shots, as offsets from the rider
            float speed = rider.FaceSpeed;
            Vector3 h = D * Mathf.Cos(theta * Mathf.Deg2Rad) + T * Mathf.Sin(theta * Mathf.Deg2Rad);

            // ride
            float dist = rideDistance + rideDistancePerH * H + rideDistancePerSpeed * speed + airExtraDistance * wAir;
            float height = rideHeight + rideHeightPerH * H + airExtraHeight * wAir;
            Vector3 toWave = Vector3.zero;
            if (riding && s.WaveId >= 0)
            {
                // halfway back toward a fixed point of the face: the rider moves in the frame, not the wave
                float cdRef = 0.35f * s.FaceWidth;
                float yRef = s.SeaLevel + 0.45f * H;
                float vk = Mathf.Lerp(waveAnchoring, airVerticalAnchoring, wAir);
                toWave = D * ((cdRef - s.CrestDistance) * waveAnchoring) + Vector3.up * ((yRef - anchor.y) * vk);
                toWave = Vector3.ClampMagnitude(toWave, 1.6f * H);
            }
            Vector3 offRide = h * dist + Vector3.up * height + toWave;
            Vector3 lookRide = T * (side * (rideLookAhead + rideLookAheadPerSpeed * speed)) + Vector3.up * (rideLookUpPerH * H);
            if (airborne && rider.AirWaveId >= 0)
                lookRide = Vector3.Lerp(lookRide, rider.AirLandingPoint - anchor + Vector3.up * 0.5f, airLookToLanding * wAir);

            // paddle: chase the board; a wave standing up behind: turn round to show it coming (as the ride will frame it)
            Vector3 heading = rider.HeadingDir;
            Vector3 offPad = -heading * paddleDistance + Vector3.up * paddleHeight;
            Vector3 lookPad = heading * paddleLookAhead + Vector3.up * 0.4f;
            if (wWatch > 1e-3f)
            {
                Vector3 hw = D * Mathf.Cos(-rideAngle * Mathf.Deg2Rad) + T * Mathf.Sin(-rideAngle * Mathf.Deg2Rad);
                Vector3 offWatch = hw * (rideDistance + rideDistancePerH * H) + Vector3.up * (rideHeight + 0.2f * H);
                float xi = incomingXi < float.MaxValue ? incomingXi : 10f;
                Vector3 lookWatch = -D * (Mathf.Clamp(xi, 0f, watchRange) * 0.22f) + Vector3.up * (0.3f * H);
                offPad = Orbit(offPad, offWatch, wWatch);
                lookPad = Vector3.Lerp(lookPad, lookWatch, wWatch);
            }

            Vector3 off = Orbit(offPad, offRide, wWave);
            Vector3 lk = Vector3.Lerp(lookPad, lookRide, wWave);
            off = Orbit(off, tubeOffset, wTube);
            lk = Vector3.Lerp(lk, tubeLook, wTube);

            // wipeout: the camera stays where it was and watches
            if (wiping && !wipeFrozen) { wipeFrozen = true; wipeCamPos = camPos; }
            if (!wiping) wipeFrozen = false;
            if (wWipe > 1e-3f && wipeFrozen)
            {
                Vector3 hold = Vector3.ClampMagnitude(wipeCamPos - anchor, 14f);
                if (hold.sqrMagnitude < 4f) hold = off;
                off = Orbit(off, hold, wWipe);
                lk = Vector3.Lerp(lk, Vector3.zero, wWipe);
            }

            // ---------------------------------------------------------------- player look-around (L1 / Q held)
            var input = InputRouter.Instance;
            Vector2 raw = input != null ? input.CameraNudge : Vector2.zero;
            if (raw.sqrMagnitude > 0.0004f)
            {
                orbit += input.UsingGamepad ? raw * (150f * dt) : raw * 2.5f;
                orbit.x = Mathf.Clamp(orbit.x, -100f, 100f);
                orbit.y = Mathf.Clamp(orbit.y, -25f, 35f);
                orbitIdle = 0f;
            }
            else
            {
                orbitIdle += dt;
                if (orbitIdle > 1.2f) orbit = Vector2.MoveTowards(orbit, Vector2.zero, 90f * dt);
            }
            if (orbit.sqrMagnitude > 1e-4f)
            {
                Vector3 flat = new Vector3(off.x, 0f, off.z);
                Vector3 axis = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 1e-4f ? flat.normalized : -heading);
                off = Quaternion.AngleAxis(orbit.x * (1f - 0.5f * wTube), Vector3.up) * Quaternion.AngleAxis(-orbit.y, axis) * off;
            }

            // ---------------------------------------------------------------- smoothing (relative to the rider: he never drifts in frame)
            // direction and distance separately: a shot swinging round to the other side (into the tube, a cutback)
            // travels round the rider, never through him
            if (!initialised) { offset = off; look = lk; armDir = off.normalized; armLen = off.magnitude; }
            float smooth = Mathf.Lerp(Mathf.Lerp(offsetSmoothTime, 0.12f, wTube), 0.5f, wWipe);
            if (off.sqrMagnitude > 1e-6f) armDir = Vector3.Slerp(armDir, off.normalized, 1f - Mathf.Exp(-dt * 2.2f / Mathf.Max(0.02f, smooth)));
            armLen = Mathf.SmoothDamp(armLen, off.magnitude, ref armLenVel, smooth);
            offset = armDir * armLen;
            look = Vector3.SmoothDamp(look, lk, ref lookVel, Mathf.Lerp(lookSmoothTime, 0.25f, wWipe));

            // ---------------------------------------------------------------- stay out of the water
            Vector3 pivot = anchor;
            if (water != null)
            {
                var ps = water.ProbeOne(pivot, time);
                if (!ps.hasRoof && pivot.y < ps.height + 0.3f) pivot.y = ps.height + 0.3f;
            }
            // a wall of water between the rider and the shot (the foam of a closed section, the lip): find a clear angle
            // nearby rather than pulling in onto the rider
            float want = offset.magnitude;
            float direct = water != null ? SurfCameraMath.FreeLength(water, pivot, pivot + offset, time) : want;
            // in the barrel the tube shot was placed where it sees the rider: if the smoothed shot lags into the wall
            // (he pivots, the tube curves) cut straight to it rather than fold the arm into him
            if (water != null && wTube > 0.5f && tubeShotValid && direct < Mathf.Min(want, minArmLength))
            {
                offset = tubeOffset;
                armDir = tubeOffset.normalized;
                armLen = tubeOffset.magnitude;
                armLenVel = 0f;
                want = armLen;
                direct = SurfCameraMath.FreeLength(water, pivot, pivot + offset, time);
                avoidW = 0f; avoidWVel = 0f;
            }
            bool blocked = direct < Mathf.Min(want, minArmLength) || direct < 0.6f * want;
            if (blocked && water != null)
            {
                Vector3 clear = ClearOffset(water, pivot, offset, time);
                avoidOffset = avoidW < 0.01f ? clear : Vector3.SmoothDamp(avoidOffset, clear, ref avoidVel, 0.2f);
            }
            // in the tube the shot is clear by construction: an avoiding angle is dropped at once once it is not needed
            avoidW = Mathf.SmoothDamp(avoidW, blocked ? 1f : 0f, ref avoidWVel, blocked ? 0.08f : wTube > 0.5f ? 0.08f : 0.45f);
            Vector3 desired = pivot + Orbit(offset, avoidOffset, avoidW);
            float len = Vector3.Distance(pivot, desired);
            float free = water != null ? SurfCameraMath.FreeLength(water, pivot, desired, time) : len;
            // out of the tube the arm never folds into the rider: for the instant a thrown lip passes between them the
            // camera stays at arm's length (lifted out of the water below) while it swings round to a clear angle
            if (wTube < 0.5f) free = Mathf.Max(free, Mathf.Min(len, minArmLength));
            if (!initialised) arm = free;
            if (free < arm) { arm = free; armVel = 0f; }
            else arm = Mathf.SmoothDamp(arm, free, ref armVel, armRecoverTime);
            Vector3 dir = len > 1e-3f ? (desired - pivot) / len : Vector3.back;
            Vector3 p = pivot + dir * Mathf.Min(len, arm);
            Vector3 armEnd = p;
            if (water != null) p = KeepOutOfWater(water, p, time);
            camPos = p;
            ArmState = $"want {want:0.0} direct {direct:0.0} free {free:0.0} arm {arm:0.0} avoid {avoidW:0.00} lifted {Vector3.Distance(armEnd, p):0.0}";

            // ---------------------------------------------------------------- lens, roll, shake
            float targetFov = baseFov + speedFov * Mathf.Clamp01(speed / 12f) + airFov * wAir;
            targetFov = Mathf.Lerp(targetFov, tubeFov, wTube);
            fov = Mathf.SmoothDamp(fov, targetFov, ref fovVel, 0.35f);
            float dutchTarget = riding ? -rider.Lean * dutchPerLean * (1f - wAir) : 0f;
            dutchTarget += wTube * ExitSide * 4f;   // the barrel curls over one side of the frame
            dutch = Mathf.Lerp(dutch, dutchTarget, 1f - Mathf.Exp(-dt * 4f));

            float ambient = rider.InTube ? 0.1f + 0.15f * rider.Sample.TubeDepth : 0f;
            trauma = Mathf.Max(ambient, trauma - dt * 1.3f);
            float shake = shakeAmplitude * trauma * trauma;
            float tt = Time.time * 17f;
            Vector3 shakeEuler = new Vector3(Mathf.PerlinNoise(tt, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, tt) - 0.5f, Mathf.PerlinNoise(tt, tt * 0.5f + 3f) - 0.5f) * (2f * shake);

            Vector3 lookPos = anchor + look;
            Vector3 fwd = lookPos - camPos;
            if (fwd.sqrMagnitude < 1e-4f) fwd = heading;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.AngleAxis(dutch, Vector3.forward) * Quaternion.Euler(shakeEuler);
            cam.transform.SetPositionAndRotation(camPos, rot);
            cam.fieldOfView = fov;
            initialised = true;
        }

        /// <summary>
        /// A clear camera offset near a blocked one, same distance: tilted up over the obstacle, along the tube (under a
        /// lip that is the only way out), out in front of the face, then high above the rider. The first fully clear one
        /// wins, else the clearest.
        /// </summary>
        Vector3 ClearOffset(WaterSurfaceComposite water, Vector3 pivot, Vector3 blockedOffset, double time)
        {
            float len = Mathf.Max(minArmLength, blockedOffset.magnitude);
            Vector3 flat = new Vector3(blockedOffset.x, 0f, blockedOffset.z);
            Vector3 flatDir = flat.sqrMagnitude > 1e-4f ? flat.normalized : D;
            float el = Mathf.Asin(Mathf.Clamp(blockedOffset.normalized.y, -1f, 1f));
            Vector3 best = blockedOffset;
            float bestShare = -1f;
            for (int i = 0; i < 7; i++)
            {
                Vector3 cand;
                switch (i)
                {
                    case 0: cand = Tilt(flatDir, el + 25f * Mathf.Deg2Rad) * len; break;
                    case 1: cand = Tilt(flatDir, el + 50f * Mathf.Deg2Rad) * len; break;
                    case 2: cand = Tilt(-ExitSide * T, 12f * Mathf.Deg2Rad) * len; break;    // along the tube, deeper
                    case 3: cand = Tilt(ExitSide * T, 12f * Mathf.Deg2Rad) * len; break;     // along the tube, toward the mouth
                    case 4: cand = Tilt(D, 30f * Mathf.Deg2Rad) * len; break;            // out in front of the face
                    case 5: cand = Tilt(D, 55f * Mathf.Deg2Rad) * len; break;
                    default: cand = Tilt(flatDir, 75f * Mathf.Deg2Rad) * len; break;     // high above him
                }
                float share = SurfCameraMath.FreeLength(water, pivot, pivot + cand, time) / len;
                if (share >= 0.95f) return cand;
                if (share > bestShare) { bestShare = share; best = cand; }
            }
            return best;
        }

        /// <summary>The lip is being thrown over this part of the face: the mouth of the tube.</summary>
        static bool UnderTheCurl(in WaterSample s)
            => s.WaveId >= 0 && s.BreakPhase > 1.15f && s.BreakPhase < 2.3f && s.LipWidth > 0.6f && s.CrestDistance < s.LipWidth + 0.5f;

        /// <summary>Blends two camera offsets from the rider by direction and distance: round him, never through him.</summary>
        static Vector3 Orbit(Vector3 a, Vector3 b, float t)
        {
            float la = a.magnitude, lb = b.magnitude;
            if (la < 1e-3f || lb < 1e-3f) return Vector3.Lerp(a, b, t);
            return Vector3.Slerp(a / la, b / lb, t) * Mathf.Lerp(la, lb, t);
        }

        static Vector3 Tilt(Vector3 flatDir, float elevation)
        {
            elevation = Mathf.Min(elevation, 80f * Mathf.Deg2Rad);
            return flatDir * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
        }

        /// <summary>Last guard: above the water, and under the lip when the camera is inside a barrel.</summary>
        Vector3 KeepOutOfWater(WaterSurfaceComposite water, Vector3 p, double time)
        {
            var s = water.ProbeOne(p, time);
            float minY = s.height + waterClearance;
            if (s.hasRoof && p.y < s.roofY)
            {
                float maxY = s.roofY - 0.3f;
                if (maxY > s.height + 0.25f) minY = Mathf.Min(minY, maxY);
                if (p.y > maxY) p.y = maxY;
            }
            if (p.y < minY) p.y = minY;
            // the profile can hold water the height probe does not see (a collapsing curtain, the foam of a closed
            // tube): climb out of it
            var pts = water.ProbePoints;
            const int n = 16;
            for (int i = 0; i < n; i++) pts[i] = p + Vector3.up * (0.3f * i);
            water.Probe(n, time);
            var res = water.ProbeResults;
            for (int i = 0; i < n; i++)
                if (!res[i].inside) return pts[i];
            return pts[n - 1];
        }

        /// <summary>The nearest wave standing up behind the rider (he is in front of its crest, within the watch range).</summary>
        SurfWave FindIncoming(WaterSurfaceComposite water, Vector3 at, double time, out float xiBest)
        {
            xiBest = float.MaxValue;
            if (water == null) return null;
            SurfWave best = null;
            var list = water.ActiveSurfWaves;
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (w == null || !w.IsAlive || w.SwellLead > 3f) continue;
                w.WaveCoords(at, time, out float s, out float xi, out _);
                if (s < w.Params.sMin + 5f || s > w.Params.sMax - 5f) continue;
                if (w.LocalTau(s, out _) > 0.6f) continue;   // already broken here: it will just roll through
                if (xi < -2f || xi > watchRange || xi >= xiBest) continue;
                xiBest = xi; best = w;
            }
            return best;
        }
    }
}
