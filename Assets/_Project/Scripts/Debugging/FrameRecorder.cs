// Capture tooling (trailers, site captures): editor and development builds only.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;

namespace WavyBoard.Debugging
{
    /// <summary>
    /// Records the main camera to numbered JPEG frames at a fixed frame rate, for trailers and captures:
    /// <see cref="Time.captureFramerate"/> makes game time advance exactly 1/fps per frame however long a frame takes
    /// to save, so the frames assemble into a smooth video (ffmpeg -framerate fps -i f%05d.jpg). The camera renders
    /// into its own target at the requested size, so the IMGUI HUD is not in the frames.
    ///
    ///   var rec = go.AddComponent&lt;WavyBoard.Debugging.FrameRecorder&gt;();
    ///   rec.directory = "Assets/Screenshots~/video/take1"; rec.seconds = 12f; rec.Begin();
    /// </summary>
    public sealed class FrameRecorder : MonoBehaviour
    {
        public string directory = "Assets/Screenshots~/video/take";
        public int width = 1920;
        public int height = 1080;
        public int fps = 30;
        public int quality = 92;
        public float seconds = 10f;
        /// <summary>Optional per-frame note written to frames.csv (e.g. the rider's state), to cut the takes afterwards.</summary>
        public System.Func<string> annotate;

        public int Frames { get; private set; }
        public bool Recording { get; private set; }

        Camera cam;
        RenderTexture target;
        Texture2D readback;
        StreamWriter log;

        public string Begin()
        {
            if (Recording) return "already recording";
            cam = Camera.main;
            if (cam == null) return "no main camera";
            Directory.CreateDirectory(directory);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "FrameRecorder" };
            readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.targetTexture = target;
            Time.captureFramerate = fps;
            log = new StreamWriter(Path.Combine(directory, "frames.csv")) { AutoFlush = true };
            log.WriteLine("frame,time,note");
            Frames = 0;
            Recording = true;
            StartCoroutine(Capture());
            return $"recording {seconds} s at {fps} fps to {directory}";
        }

        IEnumerator Capture()
        {
            var endOfFrame = new WaitForEndOfFrame();
            int total = Mathf.CeilToInt(seconds * fps);
            while (Recording && Frames < total)
            {
                yield return endOfFrame;
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readback.Apply(false);
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(directory, $"f{Frames:00000}.jpg"), readback.EncodeToJPG(quality));
                log?.WriteLine($"{Frames},{Time.time:0.000},{annotate?.Invoke()}");
                Frames++;
            }
            Stop();
        }

        public string Stop()
        {
            if (!Recording) return "idle";
            Recording = false;
            Time.captureFramerate = 0;
            if (cam != null) cam.targetTexture = null;
            if (target != null) { target.Release(); Destroy(target); }
            if (readback != null) Destroy(readback);
            log?.Dispose();
            log = null;
            return $"stopped after {Frames} frames";
        }

        void OnDisable() => Stop();
    }
}
#endif
