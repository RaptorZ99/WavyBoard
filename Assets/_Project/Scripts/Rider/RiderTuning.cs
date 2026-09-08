using UnityEngine;

namespace Biscotte.Rider
{
    [CreateAssetMenu(menuName = "Biscotte/Rider Tuning", fileName = "RiderTuning")]
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
        public float takeoffMinPhase = 0.5f;
        public float takeoffMaxPhase = 1.9f;
        public float takeoffMinSlope = 0.22f;
        public float takeoffMinEnergy = 0.3f;
        public float takeoffMinForwardSpeed = 1.2f;
        public float takeoffInitialCelerityFactor = 0.55f;
        public float takeoffDuration = 0.45f;
        [Tooltip("0..1: how much a rising face pulls a paddling rider along and lines him up (arcade feel)")]
        public float takeoffAssist = 0.8f;

        [Header("Ride")]
        public float gravity = 9.81f;
        public float rideDraft = 0.05f;
        public float pushGain = 0.8f;
        public float pushTau = 0.9f;
        [Tooltip("Legacy (unused since the 2026-09-08 speed tuning): see quadraticDrag / planingDamping")]
        public float dragCoefficient = 0.012f;
        [Tooltip("Quadratic drag on the relative speed (1/m): sets the terminal speed of a drop (~11 m/s on a 45 deg face)")]
        public float quadraticDrag = 0.025f;
        [Tooltip("Linear planing damping (1/s): low = the board keeps its speed across the face")]
        public float planingDamping = 0.25f;
        [Tooltip("Linear damping while the stall button is held (1/s)")]
        public float stallDamping = 2.2f;
        public float stallDragMultiplier = 3.2f;
        [Tooltip("Gravity multiplier on steep faces (x1 at slope 0.3 -> x gain at slope 1): dropping down the face builds real speed")]
        public float slopeGravityGain = 1.7f;
        [Tooltip("Gravity scale while climbing back up the face (< 1 keeps enough speed to reach the lip after a bottom turn)")]
        public float climbGravityScale = 0.6f;
        [Tooltip("Forward drive while carving hard at speed (m/s^2, scaled by |stick x| and speed)")]
        public float carveDrive = 1.2f;
        [Tooltip("Forward drive while trimming forward (stick up) on the way down the face (m/s^2)")]
        public float trimDrive = 1.5f;
        public float yawRateMax = 140f;          // deg/s
        public float leanMax = 55f;              // deg (visual)
        public float gripTau = 0.22f;
        public float alignTau = 0.35f;
        public float maxSpeed = 16f;
        public float minRideSpeedBeforeStall = 1.5f;
        public float stallWipeoutTime = 1.6f;

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

        [Header("Air")]
        public float popVerticalSpeed = 2.5f;
        [Tooltip("Extra vertical speed per m/s of ride speed when popping off the lip (hit the lip fast = big air)")]
        public float popSpeedGain = 0.25f;
        [Tooltip("Minimum ride speed to pop / El Rollo off the lip")]
        public float popMinSpeed = 3.5f;
        public float popWindowCrestDistance = 8f;
        public float ejectVerticalSpeed = 1.8f;
        public float airDrag = 0.02f;
        public float spinRate = 360f;             // deg/s while stick held
        public float flipRate = 300f;
        public float grabRateMultiplier = 0.8f;
        public float rolloDuration = 0.7f;
        public float landAlignMin = 0.82f;
        public float landSketchyMin = 0.7f;
        public float maxAirTime = 3.5f;

        [Header("Wipeout")]
        public float wipeoutDuration = 2.6f;
        public float wipeoutDrift = 0.7f;
        public float tubeCloseoutWipeoutPhase = 2.25f;
        [Tooltip("'Too steep' wipeout only below this speed on a near-vertical face (speed alone never wipes out)")]
        public float tooSteepMaxSpeed = 2f;
        [Tooltip("Whitewater wipeout only below this speed")]
        public float whitewaterWipeoutSpeed = 3f;

        [Header("Prototype")]
        [Tooltip("After a wipeout or kick-out, respawn at the lineup instead of paddling back (no channel yet).")]
        public bool autoReturnToLineup = true;
    }
}
