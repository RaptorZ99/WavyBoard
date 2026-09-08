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

        [Header("Ride")]
        public float gravity = 9.81f;
        public float rideDraft = 0.05f;
        public float pushGain = 0.8f;
        public float pushTau = 0.9f;
        public float dragCoefficient = 0.012f;
        public float stallDragMultiplier = 3.2f;
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

        [Header("Air")]
        public float popVerticalSpeed = 2.5f;
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
    }
}
