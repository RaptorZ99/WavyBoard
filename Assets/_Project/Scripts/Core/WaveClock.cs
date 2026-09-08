using UnityEngine;

namespace Biscotte.Core
{
    /// <summary>Single time source for all water/wave math. RenderTime for meshes/VFX, FixedTime for physics.</summary>
    public static class WaveClock
    {
        public static double RenderTime { get; internal set; }
        public static double FixedTime { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RenderTime = 0;
            FixedTime = 0;
        }
    }

    [DefaultExecutionOrder(-300)]
    public class WaveClockDriver : MonoBehaviour
    {
        void Update() { WaveClock.RenderTime = Time.timeAsDouble; }
        void FixedUpdate() { WaveClock.FixedTime = Time.fixedTimeAsDouble; }
    }
}
