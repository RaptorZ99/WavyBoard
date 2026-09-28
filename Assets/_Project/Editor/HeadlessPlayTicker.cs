using UnityEditor;
using UnityEngine;

namespace WavyBoard.EditorTools
{
    /// <summary>
    /// When the Editor is unfocused (or the desktop is locked) Unity stops running the player loop in Play mode.
    /// This ticker keeps Play mode paused and steps frames explicitly with EditorApplication.Step() from
    /// EditorApplication.update, so CLI-driven tests keep advancing (deterministic, can run faster than real time).
    /// Toggle with HeadlessPlayTicker.Enable(true/false) (eval) or the menu WavyBoard/Headless Play Ticker.
    /// </summary>
    [InitializeOnLoad]
    public static class HeadlessPlayTicker
    {
        const string kPref = "WavyBoard.HeadlessPlayTicker";
        const string kStepsPref = "WavyBoard.HeadlessPlayTicker.Steps";
        public static int Ticks { get; private set; }
        public static int Frames { get; private set; }

        static HeadlessPlayTicker()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(kPref, false);
            set => EditorPrefs.SetBool(kPref, value);
        }

        /// <summary>Frames stepped per editor update (editor updates run ~10-30 Hz when unfocused).</summary>
        public static int StepsPerUpdate
        {
            get => EditorPrefs.GetInt(kStepsPref, 4);
            set => EditorPrefs.SetInt(kStepsPref, Mathf.Clamp(value, 1, 60));
        }

        public static string Enable(bool on)
        {
            Enabled = on;
            if (!on && EditorApplication.isPlaying && EditorApplication.isPaused) EditorApplication.isPaused = false;
            return "HeadlessPlayTicker " + (on ? "enabled" : "disabled") + " stepsPerUpdate=" + StepsPerUpdate;
        }

        [MenuItem("WavyBoard/Headless Play Ticker")]
        static void Toggle() { Enable(!Enabled); }

        static void OnUpdate()
        {
            if (!Enabled) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode == false) return;
            if (EditorApplication.isCompiling) return;
            Ticks++;
            if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
            int n = StepsPerUpdate;
            for (int i = 0; i < n; i++) { EditorApplication.Step(); Frames++; }
        }
    }
}
