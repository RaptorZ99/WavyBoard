using UnityEngine;

namespace WavyBoard.Tricks
{
    /// <summary>
    /// Plays the manoeuvres the rider starts. Every rotation runs as its own track with a smoothstep profile
    /// (it eases in and out, and lands on exactly the degrees it promised), so several gestures can overlap in one
    /// air — a scoop thrown during an El Rollo really does add a spin on top of the roll, which is how an ARS is
    /// built. Tracks are integrated by difference, so no rounding drifts over a long flight.
    /// </summary>
    public class TrickRunner
    {
        const int MaxTracks = 4;

        struct Track
        {
            public float yaw, pitch, roll;   // total degrees
            public float duration, elapsed;
            public bool used;
        }

        readonly Track[] tracks = new Track[MaxTracks];

        /// <summary>Name of the manoeuvre currently being played (the last one started).</summary>
        public string Current { get; private set; } = "";
        /// <summary>Base points of the manoeuvre currently being played.</summary>
        public float CurrentPoints { get; private set; }
        /// <summary>True while at least one rotation track is still playing.</summary>
        public bool Busy { get; private set; }

        /// <summary>Degrees accumulated since the last <see cref="ResetAccumulation"/>, for naming and scoring.</summary>
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Roll { get; private set; }
        /// <summary>Points promised by every manoeuvre started since the last reset.</summary>
        public float PendingPoints { get; private set; }
        /// <summary>A grab was held at some point during the run.</summary>
        public bool Grabbed { get; set; }
        /// <summary>Time scale of every running rotation: tucking in the air makes what is already turning turn
        /// faster, exactly as pulling your knees in does. 1 = as authored.</summary>
        public float Speed { get; set; } = 1f;

        /// <summary>Clears everything (new air, wipeout, respawn).</summary>
        public void Reset()
        {
            for (int i = 0; i < MaxTracks; i++) tracks[i] = default;
            Current = ""; CurrentPoints = 0f; Busy = false; Speed = 1f;
            ResetAccumulation();
        }

        /// <summary>Clears the accumulated rotation and pending points, keeping any track still playing.</summary>
        public void ResetAccumulation()
        {
            Yaw = 0f; Pitch = 0f; Roll = 0f; PendingPoints = 0f; Grabbed = false;
        }

        /// <summary>Starts one manoeuvre. Returns false when no track is free (four at once is already absurd).</summary>
        public bool Begin(in TrickDef d)
        {
            Current = d.name;
            CurrentPoints = d.points;
            PendingPoints += d.points;
            if (!d.HasRotation) return true;
            for (int i = 0; i < MaxTracks; i++)
            {
                if (tracks[i].used) continue;
                tracks[i] = new Track
                {
                    yaw = d.yawDeg, pitch = d.pitchDeg, roll = d.rollDeg,
                    duration = Mathf.Max(0.05f, d.duration), elapsed = 0f, used = true,
                };
                Busy = true;
                return true;
            }
            return false;
        }

        /// <summary>Advances every track and returns the rotation to apply this step, in degrees (yaw, pitch, roll).</summary>
        public Vector3 Tick(float dt)
        {
            Vector3 delta = Vector3.zero;
            bool any = false;
            for (int i = 0; i < MaxTracks; i++)
            {
                ref Track t = ref tracks[i];
                if (!t.used) continue;
                float u0 = Mathf.Clamp01(t.elapsed / t.duration);
                t.elapsed += dt * Mathf.Max(0.1f, Speed);
                float u1 = Mathf.Clamp01(t.elapsed / t.duration);
                float k = Ease(u1) - Ease(u0);
                delta += new Vector3(t.yaw, t.pitch, t.roll) * k;
                if (u1 >= 1f) t.used = false; else any = true;
            }
            Busy = any;
            Yaw += delta.x; Pitch += delta.y; Roll += delta.z;
            return delta;
        }

        /// <summary>Adds rotation the player steered by hand (the left stick in the air) to the running total,
        /// so the manoeuvre is named after what was actually turned, not only what was programmed.</summary>
        public void AddYaw(float deg) { Yaw += deg; }

        /// <summary>Smoothstep: the rotation eases in and out instead of snapping on and off.</summary>
        static float Ease(float u) => u * u * (3f - 2f * u);

        /// <summary>Stops every rotation immediately (landing, wipeout) without clearing what was accumulated.</summary>
        public void StopTracks()
        {
            for (int i = 0; i < MaxTracks; i++) tracks[i].used = false;
            Busy = false;
            Speed = 1f;
        }

        /// <summary>How far the flat spin is from the nearest half turn (deg, signed). Near zero means the board is
        /// already square to where it started, which is what a clean landing wants.</summary>
        public float YawOffHalfTurn()
        {
            float half = Mathf.Round(Yaw / 180f) * 180f;
            return Yaw - half;
        }

        /// <summary>The name the completed rotation earns, from what was actually turned.</summary>
        public string NameRun() => TrickCatalog.NameAir(Yaw, Pitch, Roll, Grabbed);
    }
}
