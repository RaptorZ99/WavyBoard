using System.Collections;
using WavyBoard.Tricks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace WavyBoard.InputSys
{
    /// <summary>
    /// Reads the Surf action map, latches button presses for FixedUpdate consumers, detects the active device, drives rumble.
    ///
    /// Every trick is on the right stick (or the mouse): it is the board, everywhere, and traces Skate-style flick-it
    /// gestures (see <see cref="FlickIt"/>). Holding the stance button (L1 / Q) hands it to the camera. The buttons
    /// only ever ride: sprint and kick-out (Cross / Space), pump, stall, duck dive, stance.
    /// </summary>
    [DefaultExecutionOrder(-280)]
    public class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        [SerializeField] InputActionAsset actions;

        [SerializeField, Tooltip("Seconds a recognised gesture stays valid before it is dropped.")]
        float flickBuffer = 0.2f;

        [SerializeField, Tooltip("Mouse sensitivity when the mouse is used as the trick stick (screen pixels for a full deflection).")]
        float mouseStickPixels = 220f;

        [SerializeField, Tooltip("How fast the virtual mouse stick springs back to the centre (1/s).")]
        float mouseStickReturn = 7f;

        InputActionMap surf;
        InputAction move, boardStick, camNudge, pump, stall, kickOut, stance, duck, reset, pause, sprint, dbg, spawn;

        // latched presses (set in Update, consumed by gameplay)
        bool pumpP, kickOutP, stanceP, duckP, resetP, spawnP, dbgP, pauseP;

        readonly FlickIt flick = new FlickIt();
        FlickResult flick_;
        float flickT = -99f;
        Vector2 mouseStick;
        float stanceDownT = -99f;
        double gpActiveT = -1, kbActiveT = -1, msActiveT = -1;
        const float k_LookHoldTime = 0.22f;   // longer than this and the stance button is a camera hold, not a tap

        static float Now => Time.unscaledTime;

        // ---- scripted override (autopilot / automated tests)
        public bool OverrideEnabled;
        public Vector2 OverrideMove;
        public bool OverrideSprint, OverrideStall;
        public void InjectPump() { pumpP = true; }
        public void InjectKickOut() { kickOutP = true; }
        public void InjectDuck() { duckP = true; }
        public void InjectStance() { stanceP = true; }
        /// <summary>Applies a gesture as if the stick had traced it (autopilot, tests).</summary>
        public void InjectFlick(FlickResult r) { flick_ = r; flickT = Now; }

        /// <summary>Set by the rider each tick: true while the right stick belongs to the board, not the camera.</summary>
        public bool RideContext;

        public Vector2 Move => OverrideEnabled ? OverrideMove : (move != null ? move.ReadValue<Vector2>() : Vector2.zero);

        /// <summary>Camera orbit: the right stick while paddling, or anywhere while the stance button is held.</summary>
        public Vector2 CameraNudge
            => camNudge == null || (RideContext && !LookHeld) ? Vector2.zero : camNudge.ReadValue<Vector2>();

        public bool StallHeld => OverrideEnabled ? OverrideStall : (stall != null && stall.IsPressed());
        public bool SprintHeld => OverrideEnabled ? OverrideSprint : (sprint != null && sprint.IsPressed());
        public bool PumpHeld => pump != null && pump.IsPressed();

        /// <summary>Hold the stance button to take the camera: the right stick orbits instead of driving the board.</summary>
        public bool LookHeld { get; private set; }

        /// <summary>Right stick as the board: raw on a gamepad, integrated mouse motion on keyboard and mouse.</summary>
        public Vector2 Stick { get; private set; }

        /// <summary>0..1 — the board stick is pulled down and held: the crouch that loads a pop.</summary>
        public float Loaded => OverrideEnabled ? 0f : flick.Loaded;

        /// <summary>The board stick is parked out on the rim and still: in the air, that is a grab.</summary>
        public bool StickHeld => !OverrideEnabled && flick.Held;

        /// <summary>The gesture recogniser, for the HUD and for tests.</summary>
        public FlickIt Recognizer => flick;

        public bool ConsumePump() { bool v = pumpP; pumpP = false; return v; }
        /// <summary>The sprint button pressed (not held): on a fading shoulder it kicks you out of the wave.</summary>
        public bool ConsumeKickOut() { bool v = kickOutP; kickOutP = false; return v; }
        public bool ConsumeStance() { bool v = stanceP; stanceP = false; return v; }
        public bool ConsumeDuck() { bool v = duckP; duckP = false; return v; }
        public bool ConsumeReset() { bool v = resetP; resetP = false; return v; }
        public bool ConsumeSpawn() { bool v = spawnP; spawnP = false; return v; }
        public bool ConsumeDebug() { bool v = dbgP; dbgP = false; return v; }
        public bool ConsumePause() { bool v = pauseP; pauseP = false; return v; }

        /// <summary>Takes the buffered gesture and clears it, so one flick fires exactly one trick.</summary>
        public FlickResult ConsumeFlick()
        {
            if (Now - flickT > flickBuffer) return default;
            flickT = -99f;
            return flick_;
        }

        public bool UsingGamepad { get; private set; }
        public bool IsDualSense { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;
            if (actions == null) actions = Resources.Load<InputActionAsset>("Input/WavyBoardActions");
            if (actions == null) { Debug.LogError("InputRouter: no InputActionAsset assigned (and Resources/Input/WavyBoardActions not found)"); return; }
            surf = actions.FindActionMap("Surf", true);
            move = surf.FindAction("Move", true);
            boardStick = surf.FindAction("AirRotate", true);   // right stick / mouse delta: the board
            camNudge = surf.FindAction("CameraNudge", true);
            pump = surf.FindAction("Pump", true);
            stall = surf.FindAction("Stall", true);
            kickOut = surf.FindAction("Pop", true);            // same button as the sprint
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
            kickOutP |= kickOut.WasPressedThisFrame();
            duckP |= duck.WasPressedThisFrame();
            resetP |= reset.WasPressedThisFrame();
            spawnP |= spawn.WasPressedThisFrame();
            dbgP |= dbg.WasPressedThisFrame();
            pauseP |= pause.WasPressedThisFrame();
            // stance is a TAP; holding the same button is free look, so one button does both without a rebind
            if (stance.WasPressedThisFrame()) stanceDownT = Now;
            if (stance.WasReleasedThisFrame() && Now - stanceDownT < k_LookHoldTime) stanceP = true;
            LookHeld = stance.IsPressed() && Now - stanceDownT >= k_LookHoldTime;

            var gp = Gamepad.current;
            DetectDevice(gp);
            IsDualSense = gp is DualSenseGamepadHID || gp is DualShockGamepad;

            Stick = ReadStick(gp);
            if (RideContext && !LookHeld && !OverrideEnabled)
            {
                var r = flick.Feed(Stick, Now);
                if (r.flick != Flick.None) { flick_ = r; flickT = Now; }
            }
            else flick.Reset();
        }

        /// <summary>
        /// The device the player is actually using. A DualSense streams reports continuously (motion sensors), so its
        /// update time says nothing: only real input counts — a stick off centre or a button down.
        /// </summary>
        void DetectDevice(Gamepad gp)
        {
            double now = Time.unscaledTimeAsDouble;
            if (gp != null && (gp.leftStick.ReadValue().sqrMagnitude > 0.04f || gp.rightStick.ReadValue().sqrMagnitude > 0.04f
                               || gp.leftTrigger.ReadValue() > 0.2f || gp.rightTrigger.ReadValue() > 0.2f
                               || gp.buttonSouth.isPressed || gp.buttonEast.isPressed || gp.buttonWest.isPressed || gp.buttonNorth.isPressed
                               || gp.leftShoulder.isPressed || gp.rightShoulder.isPressed))
                gpActiveT = now;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) kbActiveT = now;
            var ms = Mouse.current;
            if (ms != null && (ms.delta.ReadValue().sqrMagnitude > 1f || ms.leftButton.isPressed || ms.rightButton.isPressed)) msActiveT = now;
            UsingGamepad = gp != null && gpActiveT > kbActiveT && gpActiveT > msActiveT;
        }

        /// <summary>
        /// The board stick. A gamepad gives it directly; a mouse gives velocity, so mouse motion is integrated into
        /// a virtual stick that springs back to the centre. Dragging down then flicking up does the same thing with
        /// a mouse as it does with a thumbstick.
        /// </summary>
        Vector2 ReadStick(Gamepad gp)
        {
            if (OverrideEnabled) return Vector2.zero;
            if (UsingGamepad && gp != null) { mouseStick = Vector2.zero; return gp.rightStick.ReadValue(); }
            // the board action carries the mouse delta already scaled by its binding's processor
            Vector2 d = boardStick != null ? boardStick.ReadValue<Vector2>() : Vector2.zero;
            mouseStick += d / Mathf.Max(1f, mouseStickPixels);
            mouseStick = Vector2.Lerp(mouseStick, Vector2.zero, 1f - Mathf.Exp(-Time.unscaledDeltaTime * mouseStickReturn));
            if (mouseStick.sqrMagnitude > 1f) mouseStick.Normalize();
            return mouseStick;
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
