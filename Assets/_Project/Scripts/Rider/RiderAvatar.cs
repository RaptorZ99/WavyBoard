using Biscotte.InputSys;
using UnityEngine;

namespace Biscotte.Rider
{
    /// <summary>
    /// Drives a Humanoid character (any avatar: the Quaternius mannequin now, a custom one later) with the procedural
    /// prone poses of <see cref="RiderPose"/>, written every frame through a HumanPoseHandler. No animator controller
    /// or clips are needed: the pose follows the rider state, lean, paddling input and tricks, smoothed over time.
    /// </summary>
    public class RiderAvatar : MonoBehaviour
    {
        public RiderController rider;
        [Tooltip("Humanoid animator of the character instance (child of the rider visual root)")]
        public Animator animator;
        [Range(1f, 40f)] public float blendRate = 14f;
        public float strokeRate = 1.5f;        // paddle strokes per second at full paddle
        public float sprintStrokeRate = 2.3f;
        public float lookAlongCrestDeg = 55f;  // how much the head turns toward the peel while riding
        [Header("Prone <-> drop-knee transition (through a push-up pose)")]
        public float toDropKneeDuration = 0.7f;
        public float toProneDuration = 0.6f;
        [Range(1f, 40f)] public float transitionBlendRate = 9f;
        [Header("Secondary motion")]
        public float landingPulseDuration = 0.5f;
        public float railSlipFull = 4f;        // m/s of lateral slide that fully braces the outer arm
        [Header("Swim fins (procedural, attached to the foot bones)")]
        public bool fins = true;
        public Material finMaterial;
        public float finLength = 0.3f;
        public float finWidth = 0.19f;

        HumanPoseHandler handler;
        HumanPose pose;
        readonly float[] target = new float[RiderPose.MuscleCount];
        readonly float[] current = new float[RiderPose.MuscleCount];
        Vector3 curPos;
        Quaternion curRot = Quaternion.identity;
        float paddlePhase;
        float dkBlend;            // 0 prone .. 1 drop-knee, advanced over time (the solver routes it through the push-up)
        bool transitioning;
        RiderState prevState;
        float landingTimer;
        float prevVelY, vertAccel, steer;
        bool ready;

        void Start()
        {
            if (rider == null) rider = GetComponentInParent<RiderController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning("RiderAvatar: no humanoid animator found, avatar disabled", this);
                enabled = false;
                return;
            }
            animator.enabled = false;   // the pose is written directly, no controller
            foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            handler.GetHumanPose(ref pose);
            if (pose.muscles == null || pose.muscles.Length != RiderPose.MuscleCount) pose.muscles = new float[RiderPose.MuscleCount];
            if (fins) AttachFins();
            // start directly in the prone pose (no blend from the T-pose)
            prevState = rider.State;
            prevVelY = rider.Velocity.y;
            var input = BuildInput(0f);
            RiderPose.Solve(in input, current, out curPos, out curRot);
            ready = true;
            Apply();
        }

        void OnDestroy()
        {
            handler?.Dispose();
        }

        /// <summary>Builds a flat tapered fin blade in each foot bone frame, from the toes outward (bind-pose geometry, so it follows the feet).</summary>
        void AttachFins()
        {
            for (int side = 0; side < 2; side++)
            {
                var foot = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                var toes = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                var shin = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                if (foot == null || shin == null) continue;
                Vector3 toesLocal = toes != null ? foot.InverseTransformPoint(toes.position) : foot.InverseTransformDirection(-foot.up) * 0.12f;
                Vector3 dir = toesLocal.sqrMagnitude > 1e-6f ? toesLocal.normalized : Vector3.forward;
                Vector3 legUp = foot.InverseTransformDirection((shin.position - foot.position).normalized);
                Vector3 lateral = Vector3.Cross(dir, legUp).normalized;
                if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.right;
                Vector3 up = Vector3.Cross(lateral, dir).normalized;
                float scale = 1f / Mathf.Max(1e-4f, foot.lossyScale.x);
                var go = new GameObject(side == 0 ? "FinL" : "FinR");
                go.transform.SetParent(foot, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = finMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mf.sharedMesh = BuildFin(toesLocal, dir, lateral, up, finLength * scale, finWidth * scale, 0.02f * scale);
            }
        }

        static Mesh BuildFin(Vector3 origin, Vector3 dir, Vector3 lateral, Vector3 up, float length, float width, float thickness)
        {
            // profile along the blade: foot pocket (narrow) -> wide blade -> rounded tip
            int segs = 6;
            var v = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float w = width * (0.35f + 0.65f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 0.55f + 0.45f)));   // widest mid-blade, rounded tip
                float th = thickness * (1f - 0.7f * t);
                Vector3 c = origin + dir * (length * t) - up * (0.01f + 0.03f * t);   // blade curves slightly down toward the sole side
                v.Add(c + lateral * w * 0.5f + up * th * 0.5f); v.Add(c - lateral * w * 0.5f + up * th * 0.5f);
                v.Add(c + lateral * w * 0.5f - up * th * 0.5f); v.Add(c - lateral * w * 0.5f - up * th * 0.5f);
            }
            for (int i = 0; i < segs; i++)
            {
                int a = i * 4, b = a + 4;
                // top (0,1), bottom (2,3), sides
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                tris.AddRange(new[] { a + 2, a + 3, b + 2, a + 3, b + 3, b + 2 });
                tris.AddRange(new[] { a, a + 2, b, b, a + 2, b + 2 });
                tris.AddRange(new[] { a + 1, b + 1, a + 3, a + 3, b + 1, b + 3 });
            }
            int last = segs * 4;
            tris.AddRange(new[] { last, last + 1, last + 2, last + 1, last + 3, last + 2 });
            var mesh = new Mesh { name = "Fin" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void LateUpdate()
        {
            if (!ready || rider == null) return;
            float dt = Time.deltaTime;
            var input = BuildInput(dt);
            RiderPose.Solve(in input, target, out Vector3 bp, out Quaternion br);
            // slower smoothing while getting up / lying back down: the timed transition path already carries the motion
            float k = 1f - Mathf.Exp(-dt * (transitioning ? transitionBlendRate : blendRate));
            for (int i = 0; i < RiderPose.MuscleCount; i++) current[i] = Mathf.Lerp(current[i], target[i], k);
            curPos = Vector3.Lerp(curPos, bp, k);
            curRot = Quaternion.Slerp(curRot, br, k);
            Apply();
        }

        void Apply()
        {
            System.Array.Copy(current, pose.muscles, RiderPose.MuscleCount);
            pose.bodyPosition = curPos;
            pose.bodyRotation = curRot;
            handler.SetHumanPose(ref pose);
        }

        PoseInput BuildInput(float dt)
        {
            var In = InputRouter.Instance;
            var s = rider.Sample;
            float paddle = 0f;
            bool sprint = false;
            if (rider.State == RiderState.Paddle || rider.State == RiderState.TakeOff)
            {
                float mv = In != null ? Mathf.Clamp01(In.Move.y) : 0f;
                float horiz = new Vector2(rider.Velocity.x, rider.Velocity.z).magnitude;
                paddle = Mathf.Max(mv, Mathf.Clamp01(horiz / 1.5f));
                sprint = In != null && In.SprintHeld;
                if (rider.State == RiderState.TakeOff) { paddle = 1f; sprint = true; }
            }
            float rate = sprint ? sprintStrokeRate : strokeRate;
            paddlePhase += dt * rate * Mathf.Lerp(0.15f, 1f, paddle) * 2f * Mathf.PI;
            if (paddlePhase > 1000f) paddlePhase -= 1000f;

            // drop-knee blend: only advances while riding (the prone states ignore it), goes back to prone elsewhere
            bool riding = rider.State == RiderState.Ride || rider.State == RiderState.KickOut;
            float dkTarget = rider.DropKnee && riding ? 1f : 0f;
            float duration = dkTarget > dkBlend ? toDropKneeDuration : toProneDuration;
            dkBlend = dt > 0f ? Mathf.MoveTowards(dkBlend, dkTarget, dt / Mathf.Max(0.05f, duration)) : dkTarget;
            transitioning = dkBlend > 0.001f && dkBlend < 0.999f;

            // landing / drop-in pulse
            if (rider.State != prevState)
            {
                if (rider.State == RiderState.Ride && prevState == RiderState.Air) landingTimer = landingPulseDuration;
                else if (rider.State == RiderState.Ride && prevState == RiderState.TakeOff) landingTimer = landingPulseDuration * 0.6f;
                prevState = rider.State;
            }
            landingTimer = Mathf.Max(0f, landingTimer - dt);
            float landing = landingPulseDuration > 0f ? Mathf.Clamp01(landingTimer / landingPulseDuration) : 0f;
            landing *= landing;   // ease out

            // smoothed vertical acceleration (drops / bumps) and raw steering (anticipation)
            float vy = rider.Velocity.y;
            float accRaw = dt > 1e-4f ? Mathf.Clamp((vy - prevVelY) / dt, -30f, 30f) : 0f;
            prevVelY = vy;
            float smooth = dt > 0f ? 1f - Mathf.Exp(-dt * 12f) : 1f;
            vertAccel = Mathf.Lerp(vertAccel, accRaw, smooth);
            float steerRaw = In != null && riding ? Mathf.Clamp(In.Move.x, -1f, 1f) : 0f;
            steer = Mathf.Lerp(steer, steerRaw, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);

            // head: look along the crest toward the unbroken section (the peel) while riding
            float lookYaw = 0f;
            if (rider.State == RiderState.Ride || rider.State == RiderState.Air)
            {
                Vector3 T = s.CrestDir;
                if (T.sqrMagnitude > 0.5f)
                {
                    float a = Vector3.SignedAngle(rider.BoardForward, T, Vector3.up);
                    lookYaw = Mathf.Clamp(a, -lookAlongCrestDeg, lookAlongCrestDeg);
                }
            }

            return new PoseInput
            {
                state = rider.State,
                stateTime = rider.StateTime,
                lean = Mathf.Clamp(rider.Lean, -1f, 1f),
                dropKnee = dkBlend > 0.5f,
                dropKneeBlend = dkBlend,
                paddle = paddle,
                paddlePhase = paddlePhase,
                sprint = sprint,
                grab = rider.GrabHeldInAir,
                pump = rider.PumpFlash,
                airTuck = Mathf.Clamp01(rider.AirTime * 3f),
                time = Time.time,
                lookYaw = lookYaw,
                lookPitch = rider.State == RiderState.Ride ? 8f : 0f,
                speed = rider.Speed,
                steer = steer,
                railSlip = Mathf.Clamp01(Mathf.Abs(rider.RailSlip) / Mathf.Max(0.1f, railSlipFull)),
                vertAccel = vertAccel,
                landing = landing,
            };
        }
    }
}
