using Biscotte.InputSys;
using Biscotte.Ocean;
using Biscotte.Rider;
using Biscotte.Wave;
using UnityEngine;

namespace Biscotte.Debugging
{
    /// <summary>
    /// Scripted rider for automated tests and demo captures: paddles for the next wave, takes off,
    /// steers to stay in the pocket, pumps, pops on the lip when asked. Injects inputs through InputRouter overrides.
    /// </summary>
    public class RiderAutoPilot : MonoBehaviour
    {
        public RiderController rider;
        public SurfSpotConfig spot;
        public bool active = true;
        public bool doAirs = false;
        [Range(0f, 90f)] public float rideAngleDeg = 32f;     // heading angle from the travel direction toward the peel side
        public float pumpInterval = 0.55f;
        public float targetPeelDistance = -2f;     // negative = behind the breaking front, inside the curl (too deep = closeout)

        float nextPump;
        float lastAirTime = -10f;
        float peelIntegral;

        void Start()
        {
            if (rider == null) rider = GetComponent<RiderController>();
            if (spot == null && rider != null) spot = rider.spot;
        }

        void OnDisable()
        {
            var input = InputRouter.Instance;
            if (input != null) input.OverrideEnabled = false;
        }

        void Update()
        {
            var input = InputRouter.Instance;
            if (!active || rider == null || input == null || spot == null) return;
            input.OverrideEnabled = true;
            var s = rider.Sample;
            Vector3 D = spot.TravelDir;
            Vector3 T = spot.CrestDir;
            Vector3 heading = rider.visualRoot != null ? Vector3.ProjectOnPlane(rider.visualRoot.forward, Vector3.up).normalized : D;
            if (heading.sqrMagnitude < 0.01f) heading = D;

            switch (rider.State)
            {
                case RiderState.Paddle:
                case RiderState.DuckDive:
                {
                    // wait at the lineup facing the beach; paddle hard when a wave face arrives behind us
                    Vector3 toLineup = spot.LineupPosition() - rider.Position; toLineup.y = 0f;
                    bool waveComing = s.BreakPhase >= 0.15f && s.CrestDistance > 0f && s.CrestDistance < 15f && s.BreakPhase < 1.9f;
                    Vector3 desired = waveComing ? D : (toLineup.magnitude > 5f ? toLineup.normalized : D);
                    float turn = SignedAngle(heading, desired);
                    float fwd = waveComing ? 1f : (toLineup.magnitude > 5f ? 0.7f : 0f);
                    input.OverrideMove = new Vector2(Mathf.Clamp(turn / 30f, -1f, 1f), fwd);
                    input.OverrideSprint = waveComing;
                    input.OverrideStall = false;
                    break;
                }
                case RiderState.TakeOff:
                    input.OverrideMove = new Vector2(0f, 1f);
                    break;
                case RiderState.Ride:
                {
                    // hold a position on the face: under the lip when there is one, otherwise in the pocket
                    float targetX = s.LipWidth > 0.5f ? 0.45f * s.LipWidth : 0.35f * Mathf.Max(2f, s.FaceWidth);
                    float xErr = s.CrestDistance - targetX;                 // + = too low on the face -> climb (heading toward the crest)
                    float peelErr = s.PeelDistance - targetPeelDistance;      // + = too far ahead on the shoulder -> slow down along the crest
                    peelIntegral = Mathf.Clamp(peelIntegral + peelErr * Time.deltaTime, -6f, 6f);
                    float angle = 70f + Mathf.Clamp(xErr * 30f, -50f, 45f) - Mathf.Clamp(peelErr * 8f + peelIntegral * 3f, -50f, 50f);
                    angle = Mathf.Clamp(angle, 5f, 118f);
                    Vector3 desired = Quaternion.AngleAxis(angle, Vector3.up) * D;
                    float turn = SignedAngle(heading, desired);
                    input.OverrideMove = new Vector2(Mathf.Clamp(turn / 25f, -1f, 1f), 0.2f);
                    input.OverrideStall = peelErr > 1.5f;
                    input.OverrideSprint = false;
                    if (Time.time >= nextPump) { input.InjectPump(); nextPump = Time.time + pumpInterval; }
                    if (doAirs && s.CrestDistance < 3f && s.BreakPhase > 0.9f && s.BreakPhase < 1.8f && rider.Speed > 6f && Time.time - lastAirTime > 4f)
                    {
                        input.InjectPop();
                        lastAirTime = Time.time;
                    }
                    break;
                }
                case RiderState.Air:
                    input.OverrideMove = Vector2.zero;
                    input.OverrideAirRotate = Vector2.zero;
                    break;
                default:
                    input.OverrideMove = Vector2.zero;
                    break;
            }
        }

        static float SignedAngle(Vector3 from, Vector3 to)
        {
            return Vector3.SignedAngle(from, to, Vector3.up);
        }
    }
}
