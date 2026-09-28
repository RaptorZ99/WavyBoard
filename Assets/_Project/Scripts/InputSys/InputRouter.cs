using System.Collections;
using WavyBoard.Tricks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WavyBoard.InputSys
{
    /// <summary>
    /// Reads the Surf action map, latches button presses for FixedUpdate consumers, detects the active device, drives rumble.
    ///
    /// Every trick is on the right stick (or the mouse): it is the board, everywhere, and traces Skate-style flick-it
    /// gestures (see <see cref="FlickIt"/>): pull it down to crouch, flick it up to pop, and the way it travels names the
    /// trick. The buttons only ever ride:
    ///   left stick / ZQSD ........ steer, paddle; in the air: spin
    ///   Cross / Space ............ sprint while paddling (held), kick out on the shoulder
    ///   R2 / Shift ............... pump
    ///   L2 / Ctrl ................ stall (let the curl catch you)
    ///   Circle / C ............... duck dive
    ///   L1 / Q ................... tap: prone / drop-knee; hold: the right stick (mouse) looks around
    ///   left mouse button ........ keyboard and mouse only: hold to crouch, release to pop (same as down-then-up)
    ///
    /// Presses are buffered for a short time so a press a frame early or late still counts (FixedUpdate may not
    /// run on the frame the button went down).
    /// </summary>
    [DefaultExecutionOrder(-280)]
    public class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        [SerializeField] InputActionAsset actions;

        [SerializeField, Tooltip("Seconds a recognised gesture stays valid before it is dropped.")]
        float flickBuffer = 0.2f;

        [SerializeField, Tooltip("Seconds a button press stays valid before it is dropped.")]
        float pressBuffer = 0.15f;

        [SerializeField, Tooltip("Seconds the mouse jump button has to be held for a full charge.")]
        float jumpChargeTime = 0.35f;

        [SerializeField, Tooltip("Mouse sensitivity when the mouse is used as the trick stick (screen pixels for a full deflection).")]
        float mouseStickPixels = 220f;

        [SerializeField, Tooltip("How fast the virtual mouse stick springs back to the centre (1/s).")]
        float mouseStickReturn = 7f;

        InputActionMap surf;
        InputAction move, boardStick, camNudge, pump, stall, kickOut, stance, duck, reset, sprint, dbg, spawn, mouseJump;

        // latched presses (set in Update, consumed by gameplay)
        bool stanceP, resetP, spawnP, dbgP;
        float pumpT = -99f, kickOutT = -99f, duckT = -99f, jumpReleaseT = -99f;
        float jumpDownT = -99f, jumpReleaseCharge;
        bool jumpDown;

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
        public bool OverrideSprint, OverrideStall, OverrideCrouch;
        public void InjectPump() { pumpT = Now; }
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
        /// <summary>The rider is crouched, loading a pop: the board stick pulled down, or the mouse jump button held.</summary>
        public bool Crouched => OverrideEnabled ? OverrideCrouch : (jumpDown || flick.Loaded > 0f);
        /// <summary>0..1 — how loaded the crouch is.</summary>
        public float CrouchCharge => OverrideEnabled ? (OverrideCrouch ? 1f : 0f)
            : Mathf.Max(flick.Loaded, jumpDown ? Mathf.Clamp01((Now - jumpDownT) / Mathf.Max(0.05f, jumpChargeTime)) : 0f);

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

        public bool ConsumePump() => Take(ref pumpT);
        /// <summary>The sprint button pressed (not held): on a fading shoulder it kicks you out of the wave.</summary>
        public bool ConsumeKickOut() => Take(ref kickOutT);
        public bool ConsumeDuck() => Take(ref duckT);
        public bool ConsumeStance() { bool v = stanceP; stanceP = false; return v; }
        public bool ConsumeReset() { bool v = resetP; resetP = false; return v; }
        public bool ConsumeSpawn() { bool v = spawnP; spawnP = false; return v; }
        public bool ConsumeDebug() { bool v = dbgP; dbgP = false; return v; }

        /// <summary>The mouse jump button was released (recently): returns true once, with the charge it was loaded with.</summary>
        public bool ConsumeJump(out float charge)
        {
            charge = jumpReleaseCharge;
            return Take(ref jumpReleaseT);
        }

        /// <summary>Takes the buffered gesture and clears it, so one flick fires exactly one trick.</summary>
        public FlickResult ConsumeFlick()
        {
            if (Now - flickT > flickBuffer) return default;
            flickT = -99f;
            return flick_;
        }

        bool Take(ref float t)
        {
            bool v = Now - t <= pressBuffer;
            t = -99f;
            return v;
        }

        public bool UsingGamepad { get; private set; }

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
            sprint = surf.FindAction("Sprint", true);
            dbg = surf.FindAction("DebugOverlay", true);
            spawn = surf.FindAction("SpawnWave", true);
            mouseJump = surf.FindAction("MouseJump", false);
        }

        void OnEnable() { surf?.Enable(); }
        void OnDisable() { surf?.Disable(); if (Instance == this) Instance = null; }

        void Update()
        {
            if (surf == null) return;
            float now = Now;
            if (pump.WasPressedThisFrame()) pumpT = now;
            if (kickOut.WasPressedThisFrame()) kickOutT = now;
            if (duck.WasPressedThisFrame()) duckT = now;
            resetP |= reset.WasPressedThisFrame();
            spawnP |= spawn.WasPressedThisFrame();
            dbgP |= dbg.WasPressedThisFrame();

            // mouse jump: pressing crouches, releasing pops with whatever charge was built
            if (mouseJump != null && mouseJump.WasPressedThisFrame()) { jumpDown = true; jumpDownT = now; }
            if (jumpDown && (mouseJump == null || !mouseJump.IsPressed()))
            {
                jumpReleaseCharge = Mathf.Clamp01((now - jumpDownT) / Mathf.Max(0.05f, jumpChargeTime));
                jumpReleaseT = now;
                jumpDown = false;
            }

            // stance is a TAP; holding the same button is free look, so one button does both without a rebind
            if (stance.WasPressedThisFrame()) stanceDownT = now;
            if (stance.WasReleasedThisFrame() && now - stanceDownT < k_LookHoldTime) stanceP = true;
            LookHeld = stance.IsPressed() && now - stanceDownT >= k_LookHoldTime;

            var gp = Gamepad.current;
            DetectDevice(gp);

            Stick = ReadStick(gp);
            if (RideContext && !LookHeld && !OverrideEnabled)
            {
                var r = flick.Feed(Stick, now);
                if (r.flick != Flick.None) { flick_ = r; flickT = now; }
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
    }
}
