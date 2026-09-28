using WavyBoard.Ocean;
using UnityEngine;

namespace WavyBoard.Rider
{
    public partial class RiderController
    {
        // The physics steps at 50 Hz; the screen runs at 60-144 Hz on a wave moving at 7+ m/s. Drawing the physics pose
        // as is makes the rider (and the camera following him) judder. Instead, every frame the pose is carried forward
        // from the last physics step to the current time and put back on the water as it is drawn right now.
        Quaternion bodyTarget = Quaternion.identity;   // orientation the physics wants (fixed step)
        Quaternion visualRot = Quaternion.identity;    // orientation drawn (smoothed per frame)
        Vector3 renderPos;
        float surfaceOffset;                           // height of the rider above the water at the last physics step
        Vector3 wipeoutSpin;

        /// <summary>After each physics step: the orientation the board should have, and its height over the water.</summary>
        void UpdateBodyTarget()
        {
            Vector3 n = lastSample.Normal;
            if (n.sqrMagnitude < 0.5f) n = Vector3.up;
            Vector3 F = Vector3.ProjectOnPlane(Heading(), n).normalized;
            if (F.sqrMagnitude < 1e-4f) F = Heading();
            switch (State)
            {
                case RiderState.Air: bodyTarget = AirRot; break;
                case RiderState.Wipeout: break;
                case RiderState.Ride:
                case RiderState.TakeOff:
                    bodyTarget = Quaternion.LookRotation(F, n) * Quaternion.AngleAxis(-Lean * tuning.leanMax * Mathf.Clamp01(Speed / 6f) + surfaceRoll, Vector3.forward);
                    break;
                default:
                    bodyTarget = Quaternion.LookRotation(F, n);
                    break;
            }
            surfaceOffset = pos.y - lastSample.Height;
        }

        void Update()
        {
            if (tuning == null) return;
            float dt = Time.deltaTime;
            renderPos = ExtrapolatedPosition();
            if (State == RiderState.Wipeout) visualRot = visualRot * Quaternion.Euler(wipeoutSpin * dt);
            else visualRot = Quaternion.Slerp(visualRot, bodyTarget, 1f - Mathf.Exp(-dt * (State == RiderState.Air ? 30f : 16f)));
            ApplyVisuals();
        }

        Vector3 ExtrapolatedPosition()
        {
            float a = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            switch (State)
            {
                case RiderState.Wipeout:
                    return pos;
                case RiderState.Air:
                    return pos + vel * a + Vector3.down * (0.5f * tuning.gravity * tuning.airGravityScale * a * a);
                default:
                {
                    Vector3 p = pos + vel * a;
                    var water = WaterSurfaceComposite.Instance;
                    if (water != null) p.y = water.ProbeOne(p, Time.timeAsDouble).height + surfaceOffset;
                    return p;
                }
            }
        }

        void ApplyVisuals()
        {
            transform.position = renderPos;
            if (visualRoot != null) visualRoot.rotation = visualRot;
            if (cameraTarget != null) cameraTarget.SetPositionAndRotation(renderPos + Vector3.up * 0.35f, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>Puts the drawn pose exactly on the physics pose (spawn, reset).</summary>
        void SnapVisuals()
        {
            if (tuning == null) return;
            UpdateBodyTarget();
            visualRot = bodyTarget;
            renderPos = pos;
            ApplyVisuals();
        }
    }
}
