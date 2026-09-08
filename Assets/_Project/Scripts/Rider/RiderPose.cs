using UnityEngine;

namespace Biscotte.Rider
{
    /// <summary>Inputs of the procedural pose solver (plain data so editor tools can preview poses without a running rider).</summary>
    public struct PoseInput
    {
        public RiderState state;
        public float stateTime;
        public float lean;          // -1..1, + = leaning to the right (turning right)
        public bool dropKnee;
        public float paddle;        // 0..1 stroke intensity (0 = resting on the board)
        public float paddlePhase;   // radians, accumulates while paddling
        public bool sprint;
        public bool grab;
        public float pump;          // 0..1 pulse right after a pump
        public float airTuck;       // 0..1 how much the legs are tucked in the air
        public float time;          // seconds, for idle breathing / wipeout flailing noise
        public float lookYaw;       // degrees, head turn toward the peel (+ = right)
        public float lookPitch;     // degrees, + = look up
        public float speed;         // m/s, tightens the body at speed
    }

    /// <summary>
    /// Procedural humanoid pose for a prone bodyboarder, expressed in Mecanim muscle space (HumanPose) so it retargets
    /// to any Humanoid avatar (the Quaternius mannequin now, a custom character later). Body orientation is prone: the
    /// chest faces the water and the head points toward the board nose (+Z of the visual root).
    /// Muscle sign convention (Unity): "A-B" muscles go from A at -1 to B at +1; "Stretch" muscles are bent at -1, straight at +1.
    /// </summary>
    public static class RiderPose
    {
        // muscle indices (HumanTrait.MuscleName order)
        public const int SpineFB = 0, SpineLR = 1, SpineTw = 2, ChestFB = 3, ChestLR = 4, ChestTw = 5, UChestFB = 6, UChestLR = 7, UChestTw = 8;
        public const int NeckNod = 9, NeckTilt = 10, NeckTurn = 11, HeadNod = 12, HeadTilt = 13, HeadTurn = 14;
        public const int LUpLegFB = 21, LUpLegIO = 22, LUpLegTw = 23, LLowLeg = 24, LLowLegTw = 25, LFootUD = 26, LFootTw = 27, LToes = 28;
        public const int RUpLegFB = 29, RUpLegIO = 30, RUpLegTw = 31, RLowLeg = 32, RLowLegTw = 33, RFootUD = 34, RFootTw = 35, RToes = 36;
        public const int LShoulderDU = 37, LShoulderFB = 38, LArmDU = 39, LArmFB = 40, LArmTw = 41, LForearm = 42, LForearmTw = 43, LHandDU = 44, LHandIO = 45;
        public const int RShoulderDU = 46, RShoulderFB = 47, RArmDU = 48, RArmFB = 49, RArmTw = 50, RForearm = 51, RForearmTw = 52, RHandDU = 53, RHandIO = 54;
        public const int LFingers = 55, RFingers = 75; // 20 muscles each: thumb(4) index(4) middle(4) ring(4) little(4)
        public const int MuscleCount = 95;

        /// <summary>Prone body orientation in the root space: chest toward -Y, head toward +Z.</summary>
        public static readonly Quaternion ProneRotation = Quaternion.Euler(90f, 0f, 0f);

        public static void Solve(in PoseInput i, float[] m, out Vector3 bodyPos, out Quaternion bodyRot)
        {
            for (int k = 0; k < MuscleCount; k++) m[k] = 0f;
            bodyPos = new Vector3(0f, 0.19f, -0.22f);
            bodyRot = ProneRotation;
            float t = i.time;
            float breath = Mathf.Sin(t * 2.1f) * 0.03f;

            // ---- base prone position: chest lifted on the elbows, head up, legs trailing with fins, hands on the nose
            m[SpineFB] = 0.45f + breath; m[ChestFB] = 0.3f; m[UChestFB] = 0.15f;
            m[NeckNod] = 0.65f; m[HeadNod] = 0.55f;
            SetArm(m, true, du: 0.72f, fb: -0.5f, forearm: -0.55f, twist: -0.2f, handDU: 0.1f);
            SetArm(m, false, du: 0.72f, fb: -0.5f, forearm: -0.55f, twist: -0.2f, handDU: 0.1f);
            SetLeg(m, true, fb: 0.12f, io: 0.1f, low: -0.15f, foot: -0.6f);
            SetLeg(m, false, fb: 0.12f, io: 0.1f, low: -0.15f, foot: -0.6f);
            Grip(m, 0.55f);

            switch (i.state)
            {
                case RiderState.Paddle:
                {
                    float amp = i.paddle;
                    float p = i.paddlePhase;
                    // freestyle stroke, arms in opposition: pull under the body (front) then recover over the water (back)
                    Stroke(m, true, p, amp, i.sprint);
                    Stroke(m, false, p + Mathf.PI, amp, i.sprint);
                    // flutter kick with fins
                    float kick = amp * (i.sprint ? 0.28f : 0.18f);
                    float kp = p * 1.5f;
                    SetLeg(m, true, fb: 0.05f + kick * Mathf.Sin(kp), io: 0.08f, low: -0.12f - 0.15f * Mathf.Max(0f, Mathf.Sin(kp + 1.2f)) * amp, foot: -0.7f);
                    SetLeg(m, false, fb: 0.05f - kick * Mathf.Sin(kp), io: 0.08f, low: -0.12f - 0.15f * Mathf.Max(0f, -Mathf.Sin(kp + 1.2f)) * amp, foot: -0.7f);
                    // body roll with the stroke, lower chest when resting
                    m[SpineTw] = 0.18f * amp * Mathf.Sin(p);
                    m[SpineFB] = Mathf.Lerp(0.2f, 0.4f, amp) + breath; m[ChestFB] = Mathf.Lerp(0.15f, 0.25f, amp);
                    m[NeckNod] = 0.7f; m[HeadNod] = 0.55f;
                    bodyPos.y = 0.17f; bodyPos.z = -0.2f;
                    break;
                }
                case RiderState.DuckDive:
                {
                    // arms straight pushing the nose down, one knee on the tail, head down
                    float k = Mathf.Clamp01(i.stateTime * 3f);
                    SetArm(m, true, du: 0.75f, fb: -0.75f, forearm: 0.2f, twist: 0f, handDU: -0.2f);
                    SetArm(m, false, du: 0.75f, fb: -0.75f, forearm: 0.2f, twist: 0f, handDU: -0.2f);
                    SetLeg(m, false, fb: -0.55f * k, io: 0.15f, low: -0.85f, foot: -0.4f);
                    SetLeg(m, true, fb: 0.05f, io: 0.1f, low: -0.2f, foot: -0.8f);
                    m[SpineFB] = 0.05f; m[ChestFB] = -0.05f; m[NeckNod] = -0.2f; m[HeadNod] = -0.3f;
                    bodyPos = new Vector3(0f, 0.24f, -0.3f);
                    bodyRot = Quaternion.Euler(70f, 0f, 0f);
                    break;
                }
                case RiderState.TakeOff:
                {
                    // last hard kicks, arms pressing on the deck to lift the chest
                    float p = i.paddlePhase;
                    Stroke(m, true, p, 1f, true); Stroke(m, false, p + Mathf.PI, 1f, true);
                    float k = Mathf.Clamp01(i.stateTime * 2.5f);
                    SetArm(m, true, du: Mathf.Lerp(m[LArmDU], 0.62f, k), fb: Mathf.Lerp(m[LArmFB], -0.55f, k), forearm: Mathf.Lerp(m[LForearm], -0.25f, k), twist: -0.2f, handDU: 0f);
                    SetArm(m, false, du: Mathf.Lerp(m[RArmDU], 0.62f, k), fb: Mathf.Lerp(m[RArmFB], -0.55f, k), forearm: Mathf.Lerp(m[RForearm], -0.25f, k), twist: -0.2f, handDU: 0f);
                    m[SpineFB] = 0.5f; m[ChestFB] = 0.3f; m[NeckNod] = 0.5f; m[HeadNod] = 0.4f;
                    bodyPos.y = Mathf.Lerp(0.17f, 0.2f, k);
                    break;
                }
                case RiderState.Ride:
                case RiderState.KickOut:
                {
                    if (i.dropKnee) { DropKnee(m, i, ref bodyPos, ref bodyRot); break; }
                    float lean = Mathf.Clamp(i.lean, -1f, 1f);
                    float al = Mathf.Abs(lean);
                    float speedK = Mathf.Clamp01((i.speed - 4f) / 8f);
                    // inside elbow drops onto the rail, outside arm extends and pushes the nose; hips shift to the inside
                    bool right = lean > 0f;
                    SetArm(m, right, du: 0.55f - 0.1f * al, fb: -0.5f - 0.2f * al, forearm: -0.75f - 0.15f * al, twist: -0.3f, handDU: 0.1f);
                    SetArm(m, !right, du: 0.68f + 0.1f * al, fb: -0.38f, forearm: -0.35f + 0.5f * al, twist: -0.1f, handDU: 0.15f);
                    m[SpineLR] = 0.55f * lean; m[ChestLR] = 0.3f * lean; m[SpineTw] = 0.25f * lean;
                    m[HeadTilt] = -0.5f * lean; m[NeckTilt] = -0.25f * lean;
                    m[HeadTurn] = Mathf.Clamp(i.lookYaw / 60f, -1f, 1f) * 0.6f; m[NeckTurn] = Mathf.Clamp(i.lookYaw / 60f, -1f, 1f) * 0.4f;
                    m[HeadNod] = 0.35f + Mathf.Clamp(i.lookPitch / 45f, -1f, 1f) * 0.4f;
                    // legs: fins lifted clear of the water at speed, the inside leg drops in to trim on a hard turn
                    float inFb = 0.1f - 0.25f * al, outFb = 0.15f;
                    SetLeg(m, right, fb: inFb, io: 0.25f * al + 0.1f, low: -0.25f - 0.2f * al, foot: -0.7f);
                    SetLeg(m, !right, fb: outFb, io: 0.1f, low: -0.2f - 0.15f * speedK, foot: -0.7f);
                    // pump: chest drives down into the board, arms straighten for a beat
                    float pump = Mathf.Clamp01(i.pump);
                    m[SpineFB] += -0.35f * pump; m[ChestFB] += -0.15f * pump;
                    m[LForearm] += 0.35f * pump; m[RForearm] += 0.35f * pump;
                    bodyPos.y += -0.03f * pump + 0.02f * speedK;
                    bodyPos.x = 0.06f * lean;
                    bodyRot = ProneRotation * Quaternion.Euler(0f, 0f, -6f * lean);
                    break;
                }
                case RiderState.Air:
                {
                    float tuck = Mathf.Clamp01(i.airTuck);
                    // knees to the chest (board comes up with the body), hands lock on the nose
                    SetLeg(m, true, fb: -0.45f * tuck + 0.1f, io: 0.15f, low: -0.55f, foot: -0.7f);
                    SetLeg(m, false, fb: -0.45f * tuck + 0.1f, io: 0.15f, low: -0.55f, foot: -0.7f);
                    SetArm(m, true, du: 0.6f, fb: -0.5f, forearm: -0.7f, twist: -0.3f, handDU: 0.1f);
                    if (i.grab) SetArm(m, false, du: -0.15f, fb: -0.45f, forearm: -0.35f, twist: 0.2f, handDU: 0.3f); // right hand reaches the tail (grab)
                    else SetArm(m, false, du: 0.6f, fb: -0.5f, forearm: -0.7f, twist: -0.3f, handDU: 0.1f);
                    m[SpineFB] = 0.2f - 0.3f * tuck; m[ChestFB] = 0.1f - 0.15f * tuck; m[NeckNod] = 0.4f; m[HeadNod] = 0.3f;
                    m[HeadTurn] = Mathf.Clamp(i.lookYaw / 60f, -1f, 1f) * 0.7f;
                    bodyPos.y = 0.2f; bodyPos.z = -0.15f;
                    break;
                }
                case RiderState.Wipeout:
                {
                    // flailing: limbs driven by low-frequency noise, spine curled
                    float w = Mathf.Clamp01(i.stateTime * 2f);
                    float n1 = Mathf.PerlinNoise(t * 1.7f, 0.3f) * 2f - 1f, n2 = Mathf.PerlinNoise(t * 1.9f, 7.1f) * 2f - 1f;
                    float n3 = Mathf.PerlinNoise(t * 1.5f, 3.7f) * 2f - 1f, n4 = Mathf.PerlinNoise(t * 2.2f, 5.9f) * 2f - 1f;
                    SetArm(m, true, du: 0.3f + 0.6f * n1, fb: -0.2f + 0.5f * n2, forearm: -0.3f + 0.5f * n3, twist: 0f, handDU: 0f);
                    SetArm(m, false, du: 0.3f + 0.6f * n2, fb: -0.2f + 0.5f * n3, forearm: -0.3f + 0.5f * n4, twist: 0f, handDU: 0f);
                    SetLeg(m, true, fb: -0.3f * w + 0.4f * n3, io: 0.3f + 0.3f * n1, low: -0.5f + 0.4f * n2, foot: -0.5f);
                    SetLeg(m, false, fb: -0.3f * w + 0.4f * n4, io: 0.3f + 0.3f * n2, low: -0.5f + 0.4f * n1, foot: -0.5f);
                    m[SpineFB] = -0.3f * w + 0.3f * n1; m[SpineLR] = 0.4f * n2; m[SpineTw] = 0.4f * n3;
                    m[NeckNod] = -0.2f; m[HeadNod] = -0.2f + 0.3f * n4; m[HeadTilt] = 0.4f * n1;
                    Grip(m, 0.2f);
                    bodyPos = new Vector3(0f, 0.15f, -0.1f);
                    bodyRot = ProneRotation * Quaternion.Euler(20f * n1, 0f, 25f * n2);
                    break;
                }
            }

            // stiffen slightly at speed (less breathing)
            if (i.speed > 6f) m[SpineFB] -= breath * 0.5f;
        }

        static void Stroke(float[] m, bool left, float p, float amp, bool sprint)
        {
            // p in [0, 2pi): 0 = hand entry at full reach, pull under the body, exit near the hip, recover over the water
            float reach = 0.65f + 0.25f * amp;
            float du = Mathf.Lerp(0.62f, reach * Mathf.Cos(p) + 0.05f, amp);            // + = toward the head (forward when prone)
            float pull = Mathf.Sin(p);                                                  // + = pulling phase (hand under the water)
            float fb = Mathf.Lerp(-0.42f, pull > 0f ? -0.45f - 0.4f * pull : -0.05f + 0.35f * pull, amp);   // front = down into the water, back = recovery above the body
            float forearm = Mathf.Lerp(-0.55f, pull > 0f ? -0.2f - 0.2f * pull : -0.75f, amp);
            float twist = Mathf.Lerp(-0.2f, pull > 0f ? -0.35f : 0.35f, amp);
            float hand = Mathf.Lerp(0.1f, pull > 0f ? -0.3f : 0.2f, amp);
            SetArm(m, left, du, fb, forearm, twist, hand);
            int sDU = left ? LShoulderDU : RShoulderDU, sFB = left ? LShoulderFB : RShoulderFB;
            m[sDU] = 0.3f * amp * Mathf.Max(0f, -pull); m[sFB] = -0.2f * amp * Mathf.Max(0f, pull);
            if (sprint) m[left ? LForearm : RForearm] -= 0.05f;
        }

        static void DropKnee(float[] m, in PoseInput i, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            // classic drop-knee: left knee on the deck near the tail, right foot planted forward, torso upright, arms out for balance
            float lean = Mathf.Clamp(i.lean, -1f, 1f);
            SetLeg(m, true, fb: -0.35f, io: 0.05f, low: -0.95f, foot: -0.7f);
            SetLeg(m, false, fb: -0.95f, io: 0.35f, low: -0.7f, foot: 0.1f);
            SetArm(m, true, du: -0.15f, fb: -0.25f, forearm: -0.25f, twist: 0f, handDU: 0f);
            SetArm(m, false, du: 0.05f + 0.3f * lean, fb: -0.3f, forearm: -0.15f, twist: 0f, handDU: 0f);
            m[SpineFB] = -0.15f; m[ChestFB] = 0.05f; m[SpineLR] = 0.35f * lean; m[SpineTw] = 0.35f + 0.2f * lean;
            m[NeckNod] = 0.15f; m[HeadNod] = 0.1f; m[HeadTurn] = 0.35f; m[HeadTilt] = -0.3f * lean;
            Grip(m, 0.1f);
            bodyPos = new Vector3(0.05f, 0.62f, -0.12f);
            bodyRot = Quaternion.Euler(18f, 25f, -8f * lean);
        }

        static void SetArm(float[] m, bool left, float du, float fb, float forearm, float twist, float handDU)
        {
            int b = left ? LArmDU : RArmDU;
            m[b] = du; m[b + 1] = fb; m[b + 2] = twist; m[b + 3] = forearm; m[b + 4] = 0f; m[b + 5] = handDU; m[b + 6] = 0f;
        }

        static void SetLeg(float[] m, bool left, float fb, float io, float low, float foot)
        {
            int b = left ? LUpLegFB : RUpLegFB;
            m[b] = fb; m[b + 1] = io; m[b + 2] = 0f; m[b + 3] = low; m[b + 4] = 0f; m[b + 5] = foot; m[b + 6] = 0f; m[b + 7] = -0.3f;
        }

        static void Grip(float[] m, float curl)
        {
            for (int h = 0; h < 2; h++)
            {
                int b = h == 0 ? LFingers : RFingers;
                for (int f = 0; f < 5; f++)
                {
                    int fb = b + f * 4;
                    m[fb] = -curl * 0.6f; m[fb + 1] = 0f; m[fb + 2] = -curl; m[fb + 3] = -curl;
                }
            }
        }
    }
}
