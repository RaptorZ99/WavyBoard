using Biscotte.InputSys;
using Biscotte.Ocean;
using Biscotte.Rider;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace Biscotte.CameraRig
{
    /// <summary>Builds and drives the Cinemachine 3 rig: ride / tube / lineup cameras, speed FOV, dutch on carve (spec §9).</summary>
    public class CameraDirector : MonoBehaviour
    {
        public RiderController rider;
        public Transform cameraTarget;

        CinemachineBrain brain;
        CinemachineCamera rideCam, tubeCam, lineupCam;
        CinemachineFollow rideFollow, tubeFollow;
        Vector3 rideBaseOffset, tubeBaseOffset;
        Vector2 orbit;            // player camera nudge (yaw, pitch) in degrees, recenters when idle
        float orbitIdle;
        bool tubeActive;
        float tubeExitTimer;
        float fovVel;

        void Start()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("CameraDirector: no Main Camera"); return; }
            brain = cam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = cam.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.6f);
            var blends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            blends.CustomBlends = new[]
            {
                new CinemachineBlenderSettings.CustomBlend { From = "CM_Ride", To = "CM_Tube", Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f) },
                new CinemachineBlenderSettings.CustomBlend { From = "CM_Tube", To = "CM_Ride", Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.0f) },
            };
            brain.CustomBlends = blends;

            // Offsets are in the pivot's local frame: forward = along the crest (peel side), right = toward the open sea,
            // so a negative x puts the camera on the beach side of the rider, looking back at the face.
            rideCam = MakeCam("CM_Ride", 10, 58f);
            rideFollow = AddFollow(rideCam, new Vector3(-4.2f, 2.4f, -6.0f), new Vector3(0.35f, 0.35f, 0.35f));
            rideBaseOffset = rideFollow.FollowOffset;
            var rc = rideCam.gameObject.AddComponent<CinemachineRotationComposer>();
            rc.Lookahead.Enabled = true; rc.Lookahead.Time = 0.3f; rc.Lookahead.Smoothing = 8f; rc.Lookahead.IgnoreY = true;
            rc.Damping = new Vector2(0.35f, 0.3f);
            rc.Composition.ScreenPosition = new Vector2(0f, -0.06f);
            rideCam.gameObject.AddComponent<CameraAboveWater>();

            tubeCam = MakeCam("CM_Tube", 5, 70f);
            tubeFollow = AddFollow(tubeCam, new Vector3(-2.0f, 1.2f, -4.6f), new Vector3(0.15f, 0.15f, 0.15f));
            tubeBaseOffset = tubeFollow.FollowOffset;
            var tc = tubeCam.gameObject.AddComponent<CinemachineRotationComposer>();
            tc.Damping = new Vector2(0.15f, 0.15f);
            tc.Lookahead.Enabled = true; tc.Lookahead.Time = 0.15f; tc.Lookahead.Smoothing = 6f; tc.Lookahead.IgnoreY = true;
            tc.Composition.ScreenPosition = new Vector2(0f, 0.08f);   // rider low in the frame so the exit stays visible
            tubeCam.gameObject.AddComponent<CameraAboveWater>();

            lineupCam = MakeCam("CM_Lineup", 5, 60f);
            AddFollow(lineupCam, new Vector3(-2.5f, 3.2f, -7.5f), new Vector3(0.5f, 0.5f, 0.5f));
            var lc = lineupCam.gameObject.AddComponent<CinemachineRotationComposer>();
            lc.Damping = new Vector2(0.5f, 0.5f);
            lineupCam.gameObject.AddComponent<CameraAboveWater>();
        }

        CinemachineFollow AddFollow(CinemachineCamera cam, Vector3 offset, Vector3 damping)
        {
            var f = cam.gameObject.AddComponent<CinemachineFollow>();
            f.FollowOffset = offset;
            f.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            f.TrackerSettings.PositionDamping = damping;
            f.TrackerSettings.RotationDamping = new Vector3(0.5f, 0.5f, 0.5f);
            f.TrackerSettings.AngularDampingMode = AngularDampingMode.Euler;
            return f;
        }

        CinemachineCamera MakeCam(string name, int priority, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<CinemachineCamera>();
            c.Priority = priority;
            c.Follow = cameraTarget;
            c.LookAt = cameraTarget;
            var lens = LensSettings.Default;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 4000f;
            c.Lens = lens;
            return c;
        }

        void LateUpdate()
        {
            if (rider == null || rideCam == null) return;
            float speed = rider.Speed;
            var lens = rideCam.Lens;
            float targetFov = 55f + 15f * Mathf.Clamp01(speed / 14f);
            lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, targetFov, ref fovVel, 0.35f);
            lens.Dutch = Mathf.Lerp(lens.Dutch, -rider.Lean * 6f, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            rideCam.Lens = lens;

            float dt = Time.deltaTime;

            // player camera nudge: right stick (rate) or mouse delta (pre-scaled), recenters after a short idle; the stick
            // rotates the board in the air instead (AirRotate), so no nudge there
            var input = InputRouter.Instance;
            Vector2 raw = (input != null && rider.State != RiderState.Air) ? input.CameraNudge : Vector2.zero;
            if (raw.sqrMagnitude > 0.0004f)
            {
                orbit += input.UsingGamepad ? raw * (150f * dt) : raw * 2.5f;
                orbit.x = Mathf.Clamp(orbit.x, -80f, 80f);
                orbit.y = Mathf.Clamp(orbit.y, -25f, 30f);
                orbitIdle = 0f;
            }
            else
            {
                orbitIdle += dt;
                if (orbitIdle > 1.2f) orbit = Vector2.MoveTowards(orbit, Vector2.zero, 90f * dt);
            }
            rideFollow.FollowOffset = Quaternion.Euler(-orbit.y, orbit.x, 0f) * rideBaseOffset;
            tubeFollow.FollowOffset = Quaternion.Euler(-orbit.y * 0.5f, orbit.x * 0.5f, 0f) * tubeBaseOffset;

            // tube camera with hysteresis: no flip-flop when the lip briefly opens
            bool inTube = rider.InTube && rider.TubeTime > 0.2f;
            if (inTube) { tubeActive = true; tubeExitTimer = 0f; }
            else if (tubeActive)
            {
                tubeExitTimer += dt;
                if (tubeExitTimer > 0.7f || rider.State != RiderState.Ride) tubeActive = false;
            }
            tubeCam.Priority = tubeActive ? 20 : 5;
            bool paddle = rider.State == RiderState.Paddle || rider.State == RiderState.DuckDive || rider.State == RiderState.Wipeout;
            lineupCam.Priority = paddle ? 12 : 5;
        }
    }

    /// <summary>Keeps any Cinemachine camera above the water surface.</summary>
    public class CameraAboveWater : CinemachineExtension
    {
        public float minHeightAboveWater = 0.45f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            var water = WaterSurfaceComposite.Instance;
            if (water == null) return;
            Vector3 p = state.RawPosition + state.PositionCorrection;
            float h = water.Sample(p, (float)Time.timeAsDouble).Height;
            float minY = h + minHeightAboveWater;
            if (p.y < minY) state.PositionCorrection += Vector3.up * (minY - p.y);
        }
    }
}
