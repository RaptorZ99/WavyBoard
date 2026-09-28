using UnityEngine;

namespace WavyBoard.Rider
{
    [CreateAssetMenu(menuName = "WavyBoard/Rider Tuning", fileName = "RiderTuning")]
    public class RiderTuning : ScriptableObject
    {
        [Header("Paddle")]
        public float paddleSpeed = 2.0f;
        public float paddleSprintSpeed = 2.9f;
        public float paddleTurnRate = 100f;      // deg/s
        public float paddleDraft = 0.12f;
        public float whitewaterPush = 1.1f;
        public float duckDiveDepth = 1.3f;
        public float duckDiveDuration = 1.3f;

        [Header("Take-off")]
        public float takeoffMaxPhase = 1.9f;
        public float takeoffMinForwardSpeed = 1.2f;
        public float takeoffDuration = 0.45f;
        [Tooltip("0..1: how much a rising face pulls a paddling rider along and lines him up (arcade feel)")]
        public float takeoffAssist = 0.8f;

        [Header("Ride")]
        public float gravity = 9.81f;
        public float rideDraft = 0.05f;
        [Tooltip("Quadratic drag on the relative speed (1/m): sets the terminal speed of a drop (~11 m/s on a 45 deg face)")]
        public float quadraticDrag = 0.025f;
        [Tooltip("Linear planing damping (1/s): low = the board keeps its speed across the face")]
        public float planingDamping = 0.25f;
        [Tooltip("Linear damping while the stall button is held (1/s)")]
        public float stallDamping = 2.2f;
        public float stallDragMultiplier = 3.2f;
        [Tooltip("Gravity multiplier on steep faces (x1 at slope 0.3 -> x gain at slope 1): dropping down the face builds real speed")]
        public float slopeGravityGain = 1.7f;
        [Tooltip("Gravity scale while climbing back up the face (< 1: the rising face carries you up to the lip after a bottom turn)")]
        public float climbGravityScale = 0.38f;
        [Tooltip("Drag scale while climbing the face (the board planes on water that is itself rising)")]
        public float climbDragScale = 0.55f;
        [Tooltip("Speed of the water up a live face, as a share of the wave's celerity (the power of the pocket: trimming\nslightly down the face keeps the speed along the line)")]
        public float faceFlow = 0.3f;
        [Tooltip("Forward drive while carving hard at speed (m/s^2, scaled by |stick x| and speed)")]
        public float carveDrive = 1.2f;
        [Tooltip("Forward drive while trimming forward (stick up) on the way down the face (m/s^2)")]
        public float trimDrive = 1.5f;
        public float yawRateMax = 150f;          // deg/s
        public float leanMax = 55f;              // deg (visual)
        public float gripTau = 0.22f;
        public float alignTau = 0.3f;
        public float maxSpeed = 16f;
        public float minRideSpeedBeforeStall = 1.5f;
        public float stallWipeoutTime = 1.6f;

        [Header("Lip (top of the face)")]
        [Tooltip("Vertical speed relative to the wave (m/s) that sends the rider off the top of the face into the air")]
        public float lipLaunchMinVy = 2f;
        [Tooltip("Same, while crouched (board stick pulled down to pop): the player asked for air, the lip gives it sooner")]
        public float lipLaunchMinVyCrouched = 0.6f;
        [Tooltip("Extra upward speed (m/s) the lip adds when it throws a rider off (arcade pop)")]
        public float lipLaunchLift = 0.8f;
        [Tooltip("Near the top of the face, too slow to launch: the lip draws the rider back onto the face (m/s^2)")]
        public float crestPull = 6f;
        [Tooltip("Headroom (m) a rider keeps under the lip in the tube: the wall above it cannot be climbed")]
        public float tubeWallHeadroom = 1.15f;

        [Header("Pump")]
        public float pumpWindowMin = 0.3f;
        public float pumpWindowMax = 1.3f;
        public float pumpGainFactor = 0.08f;
        public float pumpGainFlat = 0.4f;
        public float pumpPenalty = 0.97f;
        public int pumpMaxPerDescent = 3;
        [Tooltip("Multiplier on the pump impulse (a well-timed pump at speed gives +1.5..2 m/s)")]
        public float pumpBoost = 1.4f;
        [Tooltip("Pump impulse factor when the pocket energy is 0 (lerps to 1 at full energy)")]
        public float pumpEnergyFloor = 0.7f;

        [Header("Pop (right stick down then up; left mouse button)")]
        public float popVerticalSpeed = 2.5f;
        [Tooltip("Extra vertical speed per m/s of ride speed when popping off the lip (hit the lip fast = big air)")]
        public float popSpeedGain = 0.25f;
        [Tooltip("Share of the full pop a quick tap gives (a held, fully charged jump gives all of it)")]
        [Range(0.2f, 1f)] public float jumpTapShare = 0.6f;
        [Tooltip("Pop scale by zone: at the lip, on the open face, in the tube (under the roof), on flat water")]
        public float jumpLipScale = 1f;
        public float jumpFaceScale = 0.7f;
        public float jumpTubeScale = 0.5f;
        public float jumpFlatScale = 0.55f;
        [Tooltip("A flick (or mouse pop) this long after the lip threw you still reads as a flick at the lip (s)")]
        public float jumpCoyoteTime = 0.22f;
        [Tooltip("Distance (m) from the crest within which the rider is at the lip: flicks there launch airs")]
        public float popWindowCrestDistance = 8f;

        [Header("Air")]
        [Tooltip("Gravity scale in the air (< 1 = more hang time for tricks)")]
        public float airGravityScale = 0.8f;
        public float airDrag = 0.02f;
        [Tooltip("0..1: in the air off a wave, how strongly the flight is bent back toward the face (0 = pure ballistics:\nyou fall behind the wave)")]
        [Range(0f, 1f)] public float airReturnAssist = 0.9f;
        [Tooltip("How fast (1/s) the flight is bent back toward the landing spot")]
        public float airReturnRate = 3.5f;
        [Tooltip("Landing spot on the face, as a share of the face width in front of the crest (scales with the wave)")]
        [Range(0.1f, 0.8f)] public float airLandingFaceShare = 0.35f;
        [Tooltip("Rate (1/s) the board levels itself for the landing when no trick is turning")]
        public float airAutoLevelRate = 2.4f;
        [Tooltip("In the last seconds before touching down, an unfinished spin is squared up to the nearest half turn")]
        public float airLandingAssistTime = 0.35f;
        public float grabRateMultiplier = 0.8f;
        public float landAlignMin = 0.82f;
        public float landSketchyMin = 0.7f;
        [Tooltip("Degrees between the nose (or the tail) and the direction of travel beyond which a landing is not clean")]
        public float landYawMax = 45f;
        public float maxAirTime = 3.5f;

        [Header("Flick tricks (right stick)")]
        [Tooltip("Spin (deg/s) the left stick adds in the air, on top of the flicked rotations")]
        public float airSpinRate = 380f;
        [Tooltip("Tuck gesture: time scale on the rotations still playing")]
        public float airTuckSpeedup = 1.8f;
        [Tooltip("Tuck gesture: vertical speed (m/s) lost, you drop sooner")]
        public float airTuckDrop = 1.2f;
        [Tooltip("Settle gesture: rate (1/s) the board squares up to the water for the landing")]
        public float airSettleRate = 6f;
        [Tooltip("Settle gesture: 0 = level with the horizon, 1 = flat to the face under you")]
        [Range(0f, 1f)] public float airLevelToSurface = 0.65f;

        [Header("Wipeout")]
        public float wipeoutDuration = 2.6f;
        public float wipeoutDrift = 0.7f;
        public float tubeCloseoutWipeoutPhase = 2.25f;
        [Tooltip("Whitewater wipeout only below this speed through the foam (relative to the wave)")]
        public float whitewaterWipeoutSpeed = 3f;

        [Header("Prototype")]
        [Tooltip("After a wipeout or kick-out, reset straight to the line-up pose where you are (the waves come to you)\ninstead of recovering from wherever the wave left you.")]
        public bool autoReturnToLineup = true;
    }
}
