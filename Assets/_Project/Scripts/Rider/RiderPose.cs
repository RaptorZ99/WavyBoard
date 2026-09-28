using UnityEngine;

namespace WavyBoard.Rider
{
    /// <summary>Inputs of the procedural pose solver (plain data so editor tools can preview poses without a running rider).</summary>
    public struct PoseInput
    {
        public RiderState state;
        public float stateTime;
        public float lean;          // -1..1, + = leaning to the right (turning right)
        public bool dropKnee;       // informational (blend > 0.5); the solver only uses dropKneeBlend
        public float dropKneeBlend; // 0 = prone .. 1 = drop-knee, driven through the push-up transition by RiderAvatar
        public float paddle;        // 0..1 stroke intensity (0 = resting on the board)
        public float paddlePhase;   // radians, accumulates while paddling
        public bool sprint;
        public bool grab;
        public float pump;          // 0..1 pulse right after a pump
        public float airTuck;       // 0..1 how much the legs are tucked in the air
        public float time;          // seconds, for idle breathing / wipeout flailing noise
        public float lookYaw;       // degrees, head turn toward the peel (+ = right)
        public float lookPitch;     // degrees, + = look up
        public float speed;         // m/s, tightens / flattens the body at speed
        public float steer;         // -1..1 raw steering input (+ = right): quick torso and head anticipation
        public float railSlip;      // 0..1 lateral slide on the face: the outer arm braces
        public float vertAccel;     // m/s^2, + = pushed up; a drop or an impact (negative) compresses the body
        public float landing;       // 0..1 pulse right after landing an air / dropping in
    }

    /// <summary>
    /// Procedural humanoid pose for a prone bodyboarder, expressed in Mecanim muscle space (HumanPose) so it retargets
    /// to any Humanoid avatar (the Quaternius mannequin now, a custom character later). Body orientation is prone: the
    /// chest faces the water and the head points toward the board nose (+Z of the visual root).
    /// Muscle sign convention (Unity): "A-B" muscles go from A at -1 to B at +1; "Stretch" muscles are bent at -1, straight at +1.
    /// Prone specifics: "Arm Down-Up" + = toward the head = forward; "Arm Front-Back" - = toward the board (down);
    /// "Upper Leg Front-Back" - = hip flexion = the leg drops below the board (fins in the water), + = leg lifted.
    /// Body rotation: ProneRotation * Euler(pitch about the shoulder line, roll about the head-feet axis, yaw about the chest axis).
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

        // scratch poses for the prone <-> drop-knee transition (main thread only)
        static readonly float[] sA = new float[MuscleCount], sB = new float[MuscleCount], sC = new float[MuscleCount];

        public static void Solve(in PoseInput i, float[] m, out Vector3 bodyPos, out Quaternion bodyRot)
        {
            Base(in i, m, out bodyPos, out bodyRot);
            switch (i.state)
            {
                case RiderState.Paddle: Paddle(in i, m, ref bodyPos, ref bodyRot); break;
                case RiderState.DuckDive: DuckDive(in i, m, ref bodyPos, ref bodyRot); break;
                case RiderState.TakeOff: TakeOff(in i, m, ref bodyPos, ref bodyRot); break;
                case RiderState.Ride:
                case RiderState.KickOut: RideBlend(in i, m, out bodyPos, out bodyRot); break;
                case RiderState.Air: Air(in i, m, ref bodyPos, ref bodyRot); break;
                case RiderState.Wipeout: Wipeout(in i, m, ref bodyPos, ref bodyRot); break;
            }
            Common(in i, m);
        }

        // ------------------------------------------------------------------ base prone position
        static void Base(in PoseInput i, float[] m, out Vector3 bodyPos, out Quaternion bodyRot)
        {
            for (int k = 0; k < MuscleCount; k++) m[k] = 0f;
            bodyPos = new Vector3(0f, 0.19f, -0.22f);
            bodyRot = ProneRotation;
            // chest lifted on the elbows, head up, hands on the nose, legs trailing in the water (fins under the deck level)
            m[SpineFB] = 0.45f; m[ChestFB] = 0.3f; m[UChestFB] = 0.15f;
            m[NeckNod] = 0.65f; m[HeadNod] = 0.55f;
            SetArm(m, true, du: 0.72f, fb: -0.5f, forearm: -0.55f, twist: -0.2f, handDU: 0.1f);
            SetArm(m, false, du: 0.72f, fb: -0.5f, forearm: -0.55f, twist: -0.2f, handDU: 0.1f);
            SetLeg(m, true, fb: -0.12f, io: 0.08f, low: -0.03f, foot: -0.65f);
            SetLeg(m, false, fb: -0.12f, io: 0.08f, low: -0.03f, foot: -0.65f);
            Grip(m, 0.55f);
        }

        /// <summary>Breathing, tiny head/chest sway, stiffening at speed. Applied last, on every state.</summary>
        static void Common(in PoseInput i, float[] m)
        {
            float t = i.time;
            float stiff = Mathf.Clamp01((i.speed - 6f) / 6f);
            m[SpineFB] += Mathf.Sin(t * 2.1f) * 0.03f * (1f - 0.5f * stiff);
            m[ChestLR] += 0.015f * Mathf.Sin(t * 1.3f);
            m[HeadNod] += 0.03f * Mathf.Sin(t * 1.7f + 0.5f) * (1f - 0.5f * stiff);
        }

        // ------------------------------------------------------------------ paddling
        static void Paddle(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            float amp = Mathf.Clamp01(i.paddle);
            float p = i.paddlePhase;
            // freestyle stroke, arms in opposition: pull under the body (front) then recover over the water (back)
            Stroke(m, true, p, amp, i.sprint);
            Stroke(m, false, p + Mathf.PI, amp, i.sprint);
            // flutter kick with the fins in the water: legs hang below the tail, knees only slightly bent
            float kick = amp * (i.sprint ? 0.16f : 0.11f);
            float kp = p * 1.5f;
            SetLeg(m, true, fb: -0.13f + kick * Mathf.Sin(kp), io: 0.08f, low: -0.03f - 0.07f * Mathf.Max(0f, Mathf.Sin(kp + 1.2f)) * amp, foot: -0.7f);
            SetLeg(m, false, fb: -0.13f - kick * Mathf.Sin(kp), io: 0.08f, low: -0.03f - 0.07f * Mathf.Max(0f, -Mathf.Sin(kp + 1.2f)) * amp, foot: -0.7f);
            // shoulders roll with the stroke, chest lower when resting, small head bob and body bob
            m[SpineTw] = 0.2f * amp * Mathf.Sin(p); m[ChestTw] = 0.12f * amp * Mathf.Sin(p); m[SpineLR] = 0.08f * amp * Mathf.Sin(p);
            m[SpineFB] = Mathf.Lerp(0.2f, 0.4f, amp); m[ChestFB] = Mathf.Lerp(0.15f, 0.25f, amp);
            m[NeckNod] = 0.7f; m[HeadNod] = 0.55f + 0.06f * amp * Mathf.Sin(2f * p);
            m[HeadTurn] = 0.08f * amp * Mathf.Sin(p);
            bodyPos.y = 0.17f + 0.012f * amp * Mathf.Sin(2f * p);
            bodyPos.z = -0.2f;
            bodyRot = ProneRotation * Quaternion.Euler(0f, -4f * amp * Mathf.Sin(p), 0f);   // rail-to-rail roll with the stroke
        }

        // ------------------------------------------------------------------ duck dive
        static void DuckDive(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            // arms straight pushing the nose down, one knee on the tail, head down then up again as the board surfaces
            float k = Mathf.Clamp01(i.stateTime * 3f);
            float up = Mathf.Clamp01((i.stateTime - 0.8f) * 2f);
            SetArm(m, true, du: 0.75f, fb: -0.75f + 0.2f * up, forearm: 0.2f, twist: 0f, handDU: -0.2f);
            SetArm(m, false, du: 0.75f, fb: -0.75f + 0.2f * up, forearm: 0.2f, twist: 0f, handDU: -0.2f);
            SetLeg(m, false, fb: -0.55f * k, io: 0.15f, low: -0.85f, foot: -0.4f);
            SetLeg(m, true, fb: -0.1f, io: 0.1f, low: -0.2f, foot: -0.8f);
            m[SpineFB] = 0.05f + 0.25f * up; m[ChestFB] = -0.05f + 0.1f * up; m[NeckNod] = -0.2f + 0.6f * up; m[HeadNod] = -0.3f + 0.6f * up;
            bodyPos = new Vector3(0f, 0.24f, -0.3f);
            bodyRot = Quaternion.Euler(70f + 12f * up, 0f, 0f);
        }

        // ------------------------------------------------------------------ take-off
        static void TakeOff(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            // last hard kicks, arms pressing on the deck to lift the chest
            float p = i.paddlePhase;
            Stroke(m, true, p, 1f, true); Stroke(m, false, p + Mathf.PI, 1f, true);
            float k = Mathf.Clamp01(i.stateTime * 2.5f);
            SetArm(m, true, du: Mathf.Lerp(m[LArmDU], 0.62f, k), fb: Mathf.Lerp(m[LArmFB], -0.55f, k), forearm: Mathf.Lerp(m[LForearm], -0.25f, k), twist: -0.2f, handDU: 0f);
            SetArm(m, false, du: Mathf.Lerp(m[RArmDU], 0.62f, k), fb: Mathf.Lerp(m[RArmFB], -0.55f, k), forearm: Mathf.Lerp(m[RForearm], -0.25f, k), twist: -0.2f, handDU: 0f);
            float kp = p * 1.5f;
            SetLeg(m, true, fb: -0.13f + 0.12f * Mathf.Sin(kp), io: 0.08f, low: -0.05f, foot: -0.7f);
            SetLeg(m, false, fb: -0.13f - 0.12f * Mathf.Sin(kp), io: 0.08f, low: -0.05f, foot: -0.7f);
            m[SpineFB] = 0.5f; m[ChestFB] = 0.3f; m[NeckNod] = 0.5f; m[HeadNod] = 0.4f;
            m[SpineTw] = 0.15f * Mathf.Sin(p);
            bodyPos.y = Mathf.Lerp(0.17f, 0.2f, k);
        }

        // ------------------------------------------------------------------ riding (prone / push-up / drop-knee, blended)
        static void RideBlend(in PoseInput i, float[] m, out Vector3 pos, out Quaternion rot)
        {
            float b = Mathf.Clamp01(i.dropKneeBlend);
            if (b <= 0.001f) { Base(in i, m, out pos, out rot); RideProne(in i, m, ref pos, ref rot); return; }
            if (b >= 0.999f) { Base(in i, m, out pos, out rot); DropKnee(in i, m, ref pos, ref rot); return; }

            // transition path prone -> push-up -> drop-knee (and back), eased on each half so the hips never cut through the deck
            Base(in i, sA, out Vector3 pA, out Quaternion rA); RideProne(in i, sA, ref pA, ref rA);
            Base(in i, sB, out Vector3 pB, out Quaternion rB); PushUp(in i, sB, ref pB, ref rB);
            Base(in i, sC, out Vector3 pC, out Quaternion rC); DropKnee(in i, sC, ref pC, ref rC);
            float[] from, to; Vector3 pf, pt; Quaternion rf, rt; float u;
            if (b < 0.5f) { from = sA; to = sB; pf = pA; pt = pB; rf = rA; rt = rB; u = Smooth(b / 0.5f); }
            else { from = sB; to = sC; pf = pB; pt = pC; rf = rB; rt = rC; u = Smooth((b - 0.5f) / 0.5f); }
            for (int k = 0; k < MuscleCount; k++) m[k] = Mathf.Lerp(from[k], to[k], u);
            pos = Vector3.Lerp(pf, pt, u);
            pos.y += 0.05f * Mathf.Sin(b * Mathf.PI);   // arc: the hips rise over the deck along the way
            rot = Quaternion.Slerp(rf, rt, u);
        }

        static void RideProne(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            float lean = Mathf.Clamp(i.lean, -1f, 1f);
            float al = Mathf.Abs(lean);
            float steer = Mathf.Clamp(i.steer, -1f, 1f);
            float speedK = Mathf.Clamp01((i.speed - 4f) / 8f);
            float slip = Mathf.Clamp01(i.railSlip);
            float press = Mathf.Clamp01(Mathf.Clamp01(-i.vertAccel / 12f) + Mathf.Clamp01(i.landing));   // impact / drop: body compresses
            float lift = Mathf.Clamp01(i.vertAccel / 12f);                                                 // pushed up over a bump: body extends
            bool right = lean > 0f;

            // inside elbow drops onto the rail, outside arm extends and pushes the nose (more when sliding); shoulders dip into the turn
            SetArm(m, right, du: 0.55f - 0.1f * al, fb: -0.55f - 0.2f * al, forearm: -0.75f - 0.15f * al + 0.35f * press, twist: -0.3f, handDU: 0.1f);
            SetArm(m, !right, du: 0.7f + 0.1f * al, fb: -0.4f, forearm: -0.35f + 0.5f * al + 0.3f * slip + 0.35f * press, twist: -0.1f, handDU: 0.15f);
            m[right ? RShoulderDU : LShoulderDU] = -0.3f * al;
            m[right ? LShoulderDU : RShoulderDU] = 0.15f * al;

            // torso: side-bend and twist into the turn, quick anticipation from the raw stick, flattens at speed and on impacts
            m[SpineLR] = 0.65f * lean + 0.15f * steer; m[ChestLR] = 0.35f * lean; m[UChestLR] = 0.1f * lean;
            m[SpineTw] = 0.3f * lean + 0.2f * steer; m[ChestTw] = 0.12f * steer;
            m[SpineFB] = 0.45f - 0.2f * speedK - 0.35f * press + 0.1f * lift; m[ChestFB] = 0.3f - 0.1f * speedK - 0.15f * press; m[UChestFB] = 0.15f;
            m[HeadTilt] = -0.5f * lean; m[NeckTilt] = -0.25f * lean;
            float look = Mathf.Clamp(Mathf.Clamp(i.lookYaw / 60f, -1f, 1f) + 0.25f * steer, -1f, 1f);
            m[HeadTurn] = look * 0.6f; m[NeckTurn] = look * 0.4f;
            m[NeckNod] = 0.65f - 0.15f * speedK;
            m[HeadNod] = 0.45f - 0.15f * speedK - 0.2f * press + Mathf.Clamp(i.lookPitch / 45f, -1f, 1f) * 0.4f;

            // legs: fins in the water; the inside leg drops deeper to trim on a hard turn, slight lift at speed
            float inFb = -0.2f - 0.2f * al + 0.08f * speedK, outFb = -0.12f + 0.1f * speedK;
            SetLeg(m, right, fb: inFb, io: 0.2f * al + 0.08f, low: -0.04f - 0.12f * al, foot: -0.65f);
            SetLeg(m, !right, fb: outFb, io: 0.08f, low: -0.03f - 0.06f * speedK, foot: -0.65f);

            // pump: chest drives down into the board while the hips lift, arms straighten for a beat
            float pump = Mathf.Clamp01(i.pump);
            m[SpineFB] += -0.35f * pump; m[ChestFB] += -0.15f * pump;
            m[LForearm] += 0.35f * pump; m[RForearm] += 0.35f * pump;
            m[LUpLegFB] += 0.1f * pump; m[RUpLegFB] += 0.1f * pump;

            bodyPos.x = 0.08f * lean;
            bodyPos.y += 0.035f * pump - 0.03f * press + 0.02f * lift + 0.015f * speedK;
            bodyPos.z += -0.02f * speedK;
            // pitch: nose down on a pump / impact; roll: inside shoulder down; yaw: a touch toward the steering
            bodyRot = ProneRotation * Quaternion.Euler(6f * pump - 4f * press + 3f * lift, -9f * lean - 3f * slip * Mathf.Sign(lean), -3f * steer);
        }

        static void PushUp(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            // getting up: arms straight pushing on the deck, chest high, right knee sliding forward under the hips
            SetArm(m, true, du: 0.55f, fb: -0.8f, forearm: 0.35f, twist: -0.1f, handDU: -0.2f);
            SetArm(m, false, du: 0.55f, fb: -0.8f, forearm: 0.35f, twist: -0.1f, handDU: -0.2f);
            SetLeg(m, true, fb: -0.2f, io: 0.1f, low: -0.35f, foot: -0.5f);
            SetLeg(m, false, fb: -0.7f, io: 0.25f, low: -0.75f, foot: -0.2f);
            m[SpineFB] = 0.25f; m[ChestFB] = 0.15f; m[SpineTw] = 0.15f; m[SpineLR] = 0.1f * Mathf.Clamp(i.lean, -1f, 1f);
            m[NeckNod] = 0.4f; m[HeadNod] = 0.3f; m[HeadTurn] = 0.2f;
            Grip(m, 0.1f);
            bodyPos = new Vector3(0.03f, 0.38f, -0.18f);
            bodyRot = Quaternion.Euler(52f, 12f, 0f);
        }

        static void DropKnee(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            // classic drop-knee: left knee on the deck near the tail, right foot planted forward, torso upright, arms out for balance
            float lean = Mathf.Clamp(i.lean, -1f, 1f);
            float steer = Mathf.Clamp(i.steer, -1f, 1f);
            float speedK = Mathf.Clamp01((i.speed - 4f) / 8f);
            float press = Mathf.Clamp01(Mathf.Clamp01(-i.vertAccel / 12f) + Mathf.Clamp01(i.landing));
            float pump = Mathf.Clamp01(i.pump);
            float crouch = Mathf.Clamp01(0.15f * speedK + 0.3f * press + 0.4f * pump);   // sinks into the knees at speed, on impacts and pumps
            SetLeg(m, true, fb: -0.35f - 0.1f * crouch, io: 0.05f, low: -0.95f, foot: -0.7f);
            SetLeg(m, false, fb: -0.95f - 0.05f * crouch, io: 0.35f, low: -0.7f - 0.2f * crouch, foot: 0.1f + 0.2f * crouch);
            // rear (left) arm rises for balance when leaning right, front arm reaches toward the face
            SetArm(m, true, du: -0.15f + 0.3f * lean, fb: -0.25f - 0.1f * steer, forearm: -0.25f, twist: 0f, handDU: 0f);
            SetArm(m, false, du: 0.05f - 0.15f * lean, fb: -0.3f + 0.1f * steer, forearm: -0.15f, twist: 0f, handDU: 0f);
            m[SpineFB] = -0.15f - 0.2f * crouch; m[ChestFB] = 0.05f;
            m[SpineLR] = 0.35f * lean + 0.1f * steer; m[SpineTw] = 0.35f + 0.2f * lean + 0.15f * steer; m[ChestTw] = 0.1f * steer;
            m[NeckNod] = 0.15f; m[HeadNod] = 0.1f - 0.1f * crouch; m[HeadTurn] = 0.35f + 0.2f * steer; m[HeadTilt] = -0.3f * lean;
            Grip(m, 0.1f);
            bodyPos = new Vector3(0.05f + 0.06f * lean, 0.62f - 0.06f * crouch, -0.12f);
            bodyRot = Quaternion.Euler(18f + 6f * crouch, 25f + 8f * steer, -8f * lean);
        }

        // ------------------------------------------------------------------ air
        static void Air(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            float tuck = Mathf.Clamp01(i.airTuck);
            float pop = 1f - Mathf.Clamp01(i.stateTime / 0.25f);     // full extension at take-off, then the tuck
            float steer = Mathf.Clamp(i.steer, -1f, 1f);
            // knees to the chest (board comes up with the body), hands lock on the nose
            SetLeg(m, true, fb: 0.15f * pop - 0.45f * tuck + 0.05f, io: 0.15f, low: -0.1f - 0.5f * tuck, foot: -0.7f);
            SetLeg(m, false, fb: 0.15f * pop - 0.45f * tuck + 0.05f, io: 0.15f, low: -0.1f - 0.5f * tuck, foot: -0.7f);
            SetArm(m, true, du: 0.6f, fb: -0.5f, forearm: -0.7f + 0.5f * pop, twist: -0.3f, handDU: 0.1f);
            if (i.grab) SetArm(m, false, du: -0.15f, fb: -0.45f, forearm: -0.35f, twist: 0.2f, handDU: 0.3f);   // right hand reaches the tail (grab)
            else SetArm(m, false, du: 0.6f, fb: -0.5f, forearm: -0.7f + 0.5f * pop, twist: -0.3f, handDU: 0.1f);
            m[SpineFB] = 0.2f + 0.2f * pop - 0.3f * tuck; m[ChestFB] = 0.1f - 0.15f * tuck;
            m[SpineTw] = 0.25f * steer; m[SpineLR] = 0.15f * steer;                                   // the body leads the rotation
            m[NeckNod] = 0.4f; m[HeadNod] = 0.3f;
            m[HeadTurn] = Mathf.Clamp(Mathf.Clamp(i.lookYaw / 60f, -1f, 1f) * 0.7f + 0.3f * steer, -1f, 1f);
            bodyPos.y = 0.2f; bodyPos.z = -0.15f;
            bodyRot = ProneRotation * Quaternion.Euler(-4f * pop, 0f, 0f);
        }

        // ------------------------------------------------------------------ wipeout
        static void Wipeout(in PoseInput i, float[] m, ref Vector3 bodyPos, ref Quaternion bodyRot)
        {
            // flailing: limbs driven by low-frequency noise, spine curled
            float t = i.time;
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
            bodyRot = ProneRotation * Quaternion.Euler(20f * n1, 25f * n2, 0f);
        }

        // ------------------------------------------------------------------ helpers
        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

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
