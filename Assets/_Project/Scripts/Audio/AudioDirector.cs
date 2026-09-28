using WavyBoard.Rider;
using UnityEngine;

namespace WavyBoard.Audio
{
    /// <summary>Ambience loop, procedural board hiss and whitewater rumble, splash one-shots, tube low-pass (spec §14 v1).</summary>
    public class AudioDirector : MonoBehaviour
    {
        public RiderController rider;
        public AudioClip ambienceLoop;
        public AudioClip[] splashClips;
        [Range(0f, 1f)] public float masterVolume = 1f;

        AudioSource ambience, hiss, rumble, oneShot;
        AudioLowPassFilter hissFilter, rumbleFilter, listenerFilter;
        AudioClip noiseClip;
        float tubeCut = 22000f;

        void Start()
        {
            noiseClip = GenerateNoise(3f, 44100);
            ambience = AddSource("Ambience", ambienceLoop, true, 0.35f);
            hiss = AddSource("BoardHiss", noiseClip, true, 0f);
            hissFilter = hiss.gameObject.AddComponent<AudioLowPassFilter>(); hissFilter.cutoffFrequency = 900f;
            rumble = AddSource("Whitewater", noiseClip, true, 0f);
            rumbleFilter = rumble.gameObject.AddComponent<AudioLowPassFilter>(); rumbleFilter.cutoffFrequency = 260f;
            oneShot = AddSource("OneShot", null, false, 1f);
            var cam = Camera.main;
            if (cam != null)
            {
                listenerFilter = cam.GetComponent<AudioLowPassFilter>();
                if (listenerFilter == null) listenerFilter = cam.gameObject.AddComponent<AudioLowPassFilter>();
                listenerFilter.cutoffFrequency = 22000f;
            }
            if (rider != null) rider.OnEvent += OnEvent;
        }

        void OnDestroy() { if (rider != null) rider.OnEvent -= OnEvent; }

        AudioSource AddSource(string name, AudioClip clip, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.loop = loop; src.volume = volume * masterVolume; src.spatialBlend = 0f; src.playOnAwake = false;
            if (clip != null && loop) { src.time = Random.Range(0f, Mathf.Max(0.1f, clip.length - 0.1f)); src.Play(); }
            return src;
        }

        void Update()
        {
            if (rider == null) return;
            float speed = rider.Speed;
            var s = rider.Sample;
            bool riding = rider.State == RiderState.Ride || rider.State == RiderState.TakeOff;
            float hissTarget = riding ? Mathf.Pow(Mathf.Clamp01((speed - 2f) / 12f), 1.5f) * 0.55f : 0f;
            hiss.volume = Mathf.Lerp(hiss.volume, hissTarget * masterVolume, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            hissFilter.cutoffFrequency = 500f + speed * 260f;

            float ww = Mathf.Clamp01(s.WhitewaterAmount * 1.5f) + (s.BreakPhase >= 1f && s.BreakPhase < 2.5f ? 0.35f * Mathf.Clamp01(s.Energy) : 0f);
            float rumbleTarget = Mathf.Clamp01(ww) * 0.6f + (rider.InTube ? 0.3f : 0f);
            rumble.volume = Mathf.Lerp(rumble.volume, rumbleTarget * masterVolume, 1f - Mathf.Exp(-Time.deltaTime * 3f));

            float cutTarget = rider.InTube ? 1500f : (rider.State == RiderState.Wipeout ? 600f : 22000f);
            tubeCut = Mathf.Lerp(tubeCut, cutTarget, 1f - Mathf.Exp(-Time.deltaTime * 5f));
            if (listenerFilter != null) listenerFilter.cutoffFrequency = tubeCut;
        }

        void OnEvent(string e)
        {
            if (splashClips == null || splashClips.Length == 0) return;
            float vol = 0f;
            if (e.StartsWith("Wipeout")) vol = 1f;
            else if (e == "Air landed!" || e == "Sketchy landing") vol = 0.8f;
            else if (e == "Landed" || e == "Splash" || e == "Take-off" || e == "Duck dive") vol = 0.5f;
            if (vol <= 0f) return;
            var clip = splashClips[Random.Range(0, splashClips.Length)];
            oneShot.pitch = Random.Range(0.9f, 1.1f);
            oneShot.PlayOneShot(clip, vol * masterVolume);
        }

        /// <summary>Pink-ish noise loop (Paul Kellet filter) for hiss/rumble synthesis.</summary>
        static AudioClip GenerateNoise(float seconds, int rate)
        {
            int n = (int)(seconds * rate);
            var data = new float[n];
            float b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            var rng = new System.Random(1234);
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                b3 = 0.86650f * b3 + white * 0.3104856f;
                b4 = 0.55000f * b4 + white * 0.5329522f;
                b5 = -0.7616f * b5 - white * 0.0168980f;
                float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
                b6 = white * 0.115926f;
                data[i] = Mathf.Clamp(pink * 0.11f, -1f, 1f);
            }
            // seamless loop: crossfade the last 0.2 s into the first 0.2 s
            int x = (int)(0.2f * rate);
            for (int i = 0; i < x; i++)
            {
                float w = (float)i / x;
                data[i] = data[i] * w + data[n - x + i] * (1f - w);
            }
            var clip = AudioClip.Create("PinkNoise", n - x, 1, rate, false);
            var trimmed = new float[n - x];
            System.Array.Copy(data, trimmed, n - x);
            clip.SetData(trimmed, 0);
            return clip;
        }
    }
}
