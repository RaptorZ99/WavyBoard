using System.Text;
using WavyBoard.CameraRig;
using WavyBoard.Ocean;
using WavyBoard.Rider;
using UnityEngine;

namespace WavyBoard.Debugging
{
    /// <summary>
    /// Records what a play session actually did, for automated checks (read with <see cref="Report"/> through the CLI):
    /// launches off the lip, landings (clean, where on the face), wipeouts, time in the tube and in the tube shot, and
    /// every frame the camera ended up inside the water.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class PlaytestRecorder : MonoBehaviour
    {
        public RiderController rider;
        [Tooltip("When set, screenshots are taken on the moments worth looking at (air apex, just after a landing, a second\ninto a tube, mid camera swing, riding) into this folder (relative to the project)")]
        public string captureDir = "";
        public int maxCaptures = 16;

        public int Frames { get; private set; }
        public int CameraInWater { get; private set; }
        public float MinCameraClearance { get; private set; } = float.MaxValue;
        public int Launches { get; private set; }
        public int Landings { get; private set; }
        public int CleanLandings { get; private set; }
        public int LandedOnFace { get; private set; }
        public int WipeoutCount { get; private set; }
        public int TubeFrames { get; private set; }
        public int TubeShotFrames { get; private set; }
        public int SideSwitches { get; private set; }
        public float MaxAirPeak { get; private set; }

        readonly StringBuilder log = new StringBuilder(4096);
        CameraDirector director;
        float lastSide;
        int captures;
        float prevVy, lastCaptureTime = -10f, tubeCaptureAt = -1f, swingCaptureAt = -1f, landCaptureAt = -1f;

        void Start()
        {
            if (rider == null) rider = FindAnyObjectByType<RiderController>();
            director = FindAnyObjectByType<CameraDirector>();
            if (rider == null) return;
            rider.OnEvent += OnEvent;
            rider.OnLanded += OnLanded;
            rider.OnTrick += OnTrick;
        }

        void OnDestroy()
        {
            if (rider == null) return;
            rider.OnEvent -= OnEvent;
            rider.OnLanded -= OnLanded;
            rider.OnTrick -= OnTrick;
        }

        void Line(string what)
        {
            if (log.Length > 12000) return;
            var s = rider.Sample;
            log.Append(Time.time.ToString("0.00")).Append(' ').Append(what)
               .Append("  [").Append(rider.State).Append(" H=").Append(s.WaveHeight.ToString("0.0"))
               .Append(" cd=").Append(s.CrestDistance.ToString("0.0")).Append(" faceW=").Append(s.FaceWidth.ToString("0.0"))
               .Append(" vy=").Append(rider.Velocity.y.ToString("0.0")).Append(" face=").Append(rider.FaceSpeed.ToString("0.0"))
               .Append(" phase=").Append(s.BreakPhase.ToString("0.00")).Append(" cam=").Append(director != null ? director.ShotName : "-")
               .Append("]\n");
        }

        void OnEvent(string e)
        {
            if (e == "Envol" || e == "Air" || e == "Chop hop" || e == "Hop") Launches++;
            if (e.StartsWith("Wipeout")) WipeoutCount++;
            if (e == "Pump") return;
            Line(e);
        }

        void OnLanded(float impact, bool clean)
        {
            Landings++;
            if (clean) CleanLandings++;
            if (rider.State == RiderState.Ride && rider.Sample.CrestDistance > 0f) LandedOnFace++;
            MaxAirPeak = Mathf.Max(MaxAirPeak, rider.AirPeak);
            landCaptureAt = Time.time + 0.35f;
            Line("LANDED clean=" + clean + " impact=" + impact.ToString("0.0") + " air=" + rider.AirTime.ToString("0.00") + "s peak=" + rider.AirPeak.ToString("0.0"));
        }

        void OnTrick(string name, float pts) => Line("TRICK " + name + " +" + Mathf.RoundToInt(pts));

        void LateUpdate()
        {
            if (rider == null) return;
            Frames++;
            if (rider.InTube) TubeFrames++;
            if (director != null && director.ShotName == "tube") TubeShotFrames++;
            var cam = Camera.main;
            var water = WaterSurfaceComposite.Instance;
            if (cam == null || water == null) return;
            Vector3 p = cam.transform.position;
            double t = Time.timeAsDouble;
            if (water.IsInsideWater(p, t))
            {
                CameraInWater++;
                if (CameraInWater <= 12) Line("CAMERA IN WATER at " + p.ToString("0.0"));
            }
            var s = water.Sample(p, (float)t);
            float clear = s.HasLipRoof && p.y < s.LipRoofY ? Mathf.Min(p.y - s.Height, s.LipRoofY - p.y) : p.y - s.Height;
            MinCameraClearance = Mathf.Min(MinCameraClearance, clear);
            Vector3 toCam = p - rider.RenderPosition;
            float side = Mathf.Sign(Vector3.Dot(toCam, (Vector3)rider.Sample.CrestDir));
            if (rider.State == RiderState.Ride && side != lastSide) { SideSwitches++; lastSide = side; swingCaptureAt = Time.time + 0.45f; }
            AutoCapture();
        }

        void AutoCapture()
        {
            if (string.IsNullOrEmpty(captureDir) || captures >= maxCaptures) return;
            float now = Time.time;
            string shot = null;
            float vy = rider.Velocity.y;
            if (rider.State == RiderState.Air && prevVy > 0f && vy <= 0f && rider.AirTime > 0.2f) shot = "air";
            else if (rider.InTube && rider.TubeTime > 1f && tubeCaptureAt < 0f) { shot = "tube"; tubeCaptureAt = now; }
            else if (landCaptureAt > 0f && now >= landCaptureAt) { shot = "landed"; landCaptureAt = -1f; }
            else if (swingCaptureAt > 0f && now >= swingCaptureAt) { shot = "swing"; swingCaptureAt = -1f; }
            else if (rider.State == RiderState.Ride && !rider.InTube && rider.RideTime > 2f && now - lastCaptureTime > 6f) shot = "ride";
            if (!rider.InTube) tubeCaptureAt = -1f;
            prevVy = vy;
            if (shot == null || now - lastCaptureTime < 0.8f) return;
            lastCaptureTime = now;
            captures++;
            string path = System.IO.Path.Combine(captureDir, captures.ToString("00") + "_" + shot + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Line("CAPTURE " + path);
        }

        public string Report()
        {
            return $"frames {Frames} | camera in water {CameraInWater} (min clearance {MinCameraClearance:0.00} m) | launches {Launches} | landings {Landings} (clean {CleanLandings}, on the face {LandedOnFace}) | max air peak {MaxAirPeak:0.0} m | wipeouts {WipeoutCount} | tube frames {TubeFrames} (tube shot {TubeShotFrames}) | camera side switches {SideSwitches}\n"
                   + $"waves {rider.WavesRidden} tricks {rider.TricksLanded} airs {rider.AirsLanded} tube total {rider.TotalTubeTime:0.0}s\n" + log;
        }
    }
}
