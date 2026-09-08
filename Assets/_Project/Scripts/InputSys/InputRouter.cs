using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace Biscotte.InputSys
{
    /// <summary>Reads the Surf action map, latches button presses for FixedUpdate consumers, detects the active device, drives rumble.</summary>
    [DefaultExecutionOrder(-280)]
    public class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        [SerializeField] InputActionAsset actions;

        InputActionMap surf;
        InputAction move, airRotate, camNudge, pump, stall, pop, rollo, grab, stance, duck, reset, pause, sprint, dbg, spawn;

        // latched presses (set in Update, consumed by gameplay)
        bool pumpP, popP, rolloP, stanceP, duckP, resetP, spawnP, dbgP, pauseP;

        // ---- scripted override (autopilot / automated tests)
        public bool OverrideEnabled;
        public Vector2 OverrideMove;
        public Vector2 OverrideAirRotate;
        public bool OverrideSprint, OverrideStall, OverrideGrab;
        public void InjectPump() { pumpP = true; }
        public void InjectPop() { popP = true; }
        public void InjectRollo() { rolloP = true; }
        public void InjectDuck() { duckP = true; }
        public void InjectStance() { stanceP = true; }

        public Vector2 Move => OverrideEnabled ? OverrideMove : (move != null ? move.ReadValue<Vector2>() : Vector2.zero);
        public Vector2 AirRotate => OverrideEnabled ? OverrideAirRotate : (airRotate != null ? airRotate.ReadValue<Vector2>() : Vector2.zero);
        public Vector2 CameraNudge => camNudge != null ? camNudge.ReadValue<Vector2>() : Vector2.zero;
        public bool StallHeld => OverrideEnabled ? OverrideStall : (stall != null && stall.IsPressed());
        public bool GrabHeld => OverrideEnabled ? OverrideGrab : (grab != null && grab.IsPressed());
        public bool SprintHeld => OverrideEnabled ? OverrideSprint : (sprint != null && sprint.IsPressed());
        public bool PumpHeld => pump != null && pump.IsPressed();

        public bool ConsumePump() { bool v = pumpP; pumpP = false; return v; }
        public bool ConsumePop() { bool v = popP; popP = false; return v; }
        public bool ConsumeRollo() { bool v = rolloP; rolloP = false; return v; }
        public bool ConsumeStance() { bool v = stanceP; stanceP = false; return v; }
        public bool ConsumeDuck() { bool v = duckP; duckP = false; return v; }
        public bool ConsumeReset() { bool v = resetP; resetP = false; return v; }
        public bool ConsumeSpawn() { bool v = spawnP; spawnP = false; return v; }
        public bool ConsumeDebug() { bool v = dbgP; dbgP = false; return v; }
        public bool ConsumePause() { bool v = pauseP; pauseP = false; return v; }

        public bool UsingGamepad { get; private set; }
        public bool IsDualSense { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;
            if (actions == null) actions = Resources.Load<InputActionAsset>("Input/BiscotteActions");
            if (actions == null) { Debug.LogError("InputRouter: no InputActionAsset assigned (and Resources/Input/BiscotteActions not found)"); return; }
            surf = actions.FindActionMap("Surf", true);
            move = surf.FindAction("Move", true);
            airRotate = surf.FindAction("AirRotate", true);
            camNudge = surf.FindAction("CameraNudge", true);
            pump = surf.FindAction("Pump", true);
            stall = surf.FindAction("Stall", true);
            pop = surf.FindAction("Pop", true);
            rollo = surf.FindAction("Rollo", true);
            grab = surf.FindAction("Grab", true);
            stance = surf.FindAction("Stance", true);
            duck = surf.FindAction("DuckDiveBail", true);
            reset = surf.FindAction("Reset", true);
            pause = surf.FindAction("Pause", true);
            sprint = surf.FindAction("Sprint", true);
            dbg = surf.FindAction("DebugOverlay", true);
            spawn = surf.FindAction("SpawnWave", true);
        }

        void OnEnable() { surf?.Enable(); }
        void OnDisable() { surf?.Disable(); if (Instance == this) Instance = null; }

        void Update()
        {
            if (surf == null) return;
            pumpP |= pump.WasPressedThisFrame();
            popP |= pop.WasPressedThisFrame();
            rolloP |= rollo.WasPressedThisFrame();
            stanceP |= stance.WasPressedThisFrame();
            duckP |= duck.WasPressedThisFrame();
            resetP |= reset.WasPressedThisFrame();
            spawnP |= spawn.WasPressedThisFrame();
            dbgP |= dbg.WasPressedThisFrame();
            pauseP |= pause.WasPressedThisFrame();

            var gp = Gamepad.current;
            var kb = Keyboard.current;
            var ms = Mouse.current;
            double gpT = gp != null ? gp.lastUpdateTime : -1;
            double kbT = kb != null ? kb.lastUpdateTime : -1;
            double msT = ms != null ? ms.lastUpdateTime : -1;
            UsingGamepad = gp != null && gpT > kbT && gpT > msT;
            IsDualSense = gp is DualSenseGamepadHID || gp is DualShockGamepad;
        }

        public void Rumble(float low, float high, float duration)
        {
            var gp = Gamepad.current;
            if (gp == null) return;
            StopAllCoroutines();
            StartCoroutine(RumbleRoutine(gp, low, high, duration));
        }

        IEnumerator RumbleRoutine(Gamepad gp, float low, float high, float duration)
        {
            gp.SetMotorSpeeds(low, high);
            yield return new WaitForSecondsRealtime(duration);
            gp.SetMotorSpeeds(0f, 0f);
        }

        public void SetLightBar(Color c)
        {
            if (Gamepad.current is DualShockGamepad ds) ds.SetLightBarColor(c);
        }
    }
}
