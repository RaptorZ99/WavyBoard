using WavyBoard.Ocean;
using UnityEngine;

namespace WavyBoard.Audio
{
    /// <summary>
    /// The wind in the player's ears: a looped wind clip whose volume, pitch and pan follow the wind felt at the camera
    /// (the breeze plus the camera's own speed through the air, weaker down at the water), with gusts. Silent under
    /// water and muffled inside a barrel. Replaces the Storm Breakers wind controller, which needs their ocean.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class WindAmbience : MonoBehaviour
    {
        [Tooltip("Direction the wind blows TO, degrees around world up (0 = +Z)")]
        [Range(-180f, 180f)] public float windDirection = 0f;
        [Tooltip("Wind speed at full height (km/h)")] public float windSpeedKmh = 20f;
        [Tooltip("Height above the water where the wind is at full strength; at the water it is half")]
        public float windHeight = 10f;
        [Range(0f, 1f)] public float cameraSpeedShare = 1f;
        public float pitchFactor = 0.5f;
        public float volumeFactor = 0.1f;
        public float panFactor = 0.5f;
        [Tooltip("Gusts: share of the wind added or removed")] [Range(0f, 1f)] public float gustAmplitude = 0.5f;
        [Tooltip("Gusts per second")] public float gustFrequency = 0.5f;

        AudioSource source;
        Transform cam;
        Vector3 prevCamPos;
        Vector3 camVel;
        float muffle = 1f;

        void Start()
        {
            source = GetComponent<AudioSource>();
            if (Camera.main != null) { cam = Camera.main.transform; prevCamPos = cam.position; }
        }

        void Update()
        {
            if (source == null) return;
            if (cam == null) { if (Camera.main == null) return; cam = Camera.main.transform; prevCamPos = cam.position; }
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            camVel = Vector3.Lerp(camVel, (cam.position - prevCamPos) / dt, 1f - Mathf.Exp(-dt * 4f));
            prevCamPos = cam.position;

            var water = WaterSurfaceComposite.Instance;
            bool under = false, inTube = false;
            float height = 5f;
            if (water != null)
            {
                var p = water.ProbeOne(cam.position, Time.timeAsDouble);
                under = p.inside;
                inTube = p.hasRoof && cam.position.y < p.roofY;
                height = cam.position.y - p.height;
            }

            float a = windDirection * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            float gust = 1f + gustAmplitude * (2f * Mathf.PerlinNoise(Time.time * gustFrequency, 0.37f) - 1f);
            float strength = windSpeedKmh / 3.6f * (0.5f + 0.5f * Mathf.Clamp01(height / Mathf.Max(0.1f, windHeight))) * gust;
            Vector3 wind = dir * strength - camVel * cameraSpeedShare;
            float w = wind.magnitude;
            Vector3 wv = w > 0.01f ? wind / w : Vector3.zero;
            float forward = Vector3.Dot(-cam.forward, wv);
            float right = Vector3.Dot(-cam.right, wv);

            muffle = Mathf.MoveTowards(muffle, under ? 0f : inTube ? 0.35f : 1f, dt * 3f);
            source.volume = Mathf.Min(1f, w * volumeFactor) * (0.7f + 0.2f * forward + 0.3f * Mathf.Abs(right)) * muffle;
            source.pitch = Mathf.Sqrt(w) * pitchFactor;
            source.panStereo = right * panFactor;
        }
    }
}
