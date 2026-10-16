using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRDevice = UnityEngine.XR.InputDevice;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace KartVR
{
    /// <summary>
    /// All kart controls, created in code so there is no Input Actions asset to
    /// wire up. Every action has a VR binding plus keyboard and gamepad
    /// fallbacks, so you can test in the editor without a headset.
    ///
    /// Driving (VR): Right trigger = gas, Left trigger = brake/reverse,
    ///           Grip = grab the steering wheel and turn it with your hands
    ///           (or steer with the left stick), X = drift, A = booster,
    ///           B = respawn, left stick click = recenter view, menu button (three lines) = menu + settings.
    /// Walking (VR, hub): Left stick = walk (click the right stick to run), Right stick = snap turn.
    /// Build mode (Free Roam): right stick click / B key; see the Build* properties below.
    /// Keyboard: W/S or arrows = gas/brake (walk), A/D = steer (strafe),
    ///           Q/E = turn, Shift = booster, Space = drift,
    ///           R = respawn, C = recenter, Tab = wrist menu,
    ///           hold right mouse = look around, left click = press buttons.
    /// </summary>
    public static class KartInput
    {
        static InputAction gas, brake, steer, drift, respawn, recenter, menu, boost, move, turn, gripLeft, gripRight,
            flyUp, runClick, buildToggle, buildPlace, buildDelete, buildRotate, buildNext, buildPrev;
        static bool initialized;

        static void Init()
        {
            if (initialized) return;
            initialized = true;

            gas = new InputAction("Gas", InputActionType.Value, expectedControlType: "Axis");
            gas.AddBinding("<XRController>{RightHand}/{Trigger}");
            gas.AddBinding("<Gamepad>/rightTrigger");
            gas.AddBinding("<Keyboard>/w");
            gas.AddBinding("<Keyboard>/upArrow");

            brake = new InputAction("Brake", InputActionType.Value, expectedControlType: "Axis");
            brake.AddBinding("<XRController>{LeftHand}/{Trigger}");
            brake.AddBinding("<Gamepad>/leftTrigger");
            brake.AddBinding("<Keyboard>/s");
            brake.AddBinding("<Keyboard>/downArrow");

            steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
            steer.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}/x");
            steer.AddBinding("<Gamepad>/leftStick/x");
            steer.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");

            drift = new InputAction("Drift", InputActionType.Button);
            drift.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            drift.AddBinding("<Gamepad>/rightShoulder");
            drift.AddBinding("<Keyboard>/space");

            respawn = new InputAction("Respawn", InputActionType.Button);
            respawn.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            respawn.AddBinding("<Gamepad>/buttonNorth");
            respawn.AddBinding("<Keyboard>/r");

            recenter = new InputAction("Recenter", InputActionType.Button);
            recenter.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");
            recenter.AddBinding("<Gamepad>/select");
            recenter.AddBinding("<Keyboard>/c");

            menu = new InputAction("Menu", InputActionType.Button);
            menu.AddBinding("<XRController>{LeftHand}/{MenuButton}"); // the three-lines button
            menu.AddBinding("<Keyboard>/escape");
            menu.AddBinding("<Gamepad>/start");
            menu.AddBinding("<Keyboard>/tab");

            boost = new InputAction("Boost", InputActionType.Button);
            boost.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            boost.AddBinding("<Gamepad>/buttonSouth");
            boost.AddBinding("<Keyboard>/leftShift");

            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            turn = new InputAction("Turn", InputActionType.Value, expectedControlType: "Axis");
            turn.AddBinding("<XRController>{RightHand}/{Primary2DAxis}/x");
            turn.AddBinding("<Gamepad>/rightStick/x");
            turn.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q")
                .With("Positive", "<Keyboard>/e");

            // Grips grab the steering wheel (one per hand).
            gripLeft = new InputAction("GripLeft", InputActionType.Button);
            gripLeft.AddBinding("<XRController>{LeftHand}/{GripButton}");
            gripRight = new InputAction("GripRight", InputActionType.Button);
            gripRight.AddBinding("<XRController>{RightHand}/{GripButton}");

            // Build mode (Free Roam). VR uses the controller buttons directly (see the Build* properties).
            flyUp = new InputAction("FlyUp", InputActionType.Value, expectedControlType: "Axis");
            flyUp.AddBinding("<Gamepad>/rightStick/y");
            flyUp.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftCtrl")
                .With("Positive", "<Keyboard>/space");
            runClick = new InputAction("Run", InputActionType.Button);
            runClick.AddBinding("<Gamepad>/leftStickPress");
            runClick.Enable();
            buildToggle = new InputAction("BuildToggle", InputActionType.Button);
            buildToggle.AddBinding("<Keyboard>/b");
            buildToggle.AddBinding("<Gamepad>/rightStickPress");
            buildPlace = new InputAction("BuildPlace", InputActionType.Button);
            buildPlace.AddBinding("<Keyboard>/f");
            buildPlace.AddBinding("<Gamepad>/rightTrigger");
            buildDelete = new InputAction("BuildDelete", InputActionType.Button);
            buildDelete.AddBinding("<Keyboard>/g");
            buildDelete.AddBinding("<Gamepad>/leftTrigger");
            buildRotate = new InputAction("BuildRotate", InputActionType.Button);
            buildRotate.AddBinding("<Keyboard>/t");
            buildRotate.AddBinding("<Gamepad>/rightShoulder");
            buildNext = new InputAction("BuildNext", InputActionType.Button);
            buildNext.AddBinding("<Keyboard>/2");
            buildNext.AddBinding("<Gamepad>/buttonSouth");
            buildPrev = new InputAction("BuildPrev", InputActionType.Button);
            buildPrev.AddBinding("<Keyboard>/1");
            buildPrev.AddBinding("<Gamepad>/buttonWest");
            foreach (var a in new[] { flyUp, buildToggle, buildPlace, buildDelete, buildRotate, buildNext, buildPrev }) a.Enable();

            gripLeft.Enable();
            gripRight.Enable();
            boost.Enable();
            move.Enable();
            turn.Enable();
            gas.Enable();
            brake.Enable();
            steer.Enable();
            drift.Enable();
            respawn.Enable();
            recenter.Enable();
            menu.Enable();
        }

        // Every control reads BOTH the Input System and Unity's built-in XR controller API and uses whichever is
        // active. So VR controllers keep working even if the Input System isn't receiving them (e.g. new input
        // backends switched off in Player Settings).
        public static float Gas { get { Init(); return Mathf.Clamp01(Mathf.Max(gas.ReadValue<float>(), XRVal(XRNode.RightHand, XRUsages.trigger))); } }
        public static float Brake { get { Init(); return Mathf.Clamp01(Mathf.Max(brake.ReadValue<float>(), XRVal(XRNode.LeftHand, XRUsages.trigger))); } }
        public static float Steer { get { Init(); return Mathf.Clamp(Strongest(steer.ReadValue<float>(), XRAxis(XRNode.LeftHand, XRUsages.primary2DAxis).x), -1f, 1f); } }
        public static bool DriftHeld { get { Init(); return drift.IsPressed() || XRBtn(XRNode.LeftHand, XRUsages.primaryButton); } }
        public static bool LeftGripHeld { get { Init(); return gripLeft.IsPressed() || XRGrip(XRNode.LeftHand); } }
        public static bool RightGripHeld { get { Init(); return gripRight.IsPressed() || XRGrip(XRNode.RightHand); } }
        public static bool BoostHeld { get { Init(); return boost.IsPressed() || XRBtn(XRNode.RightHand, XRUsages.primaryButton); } }
        public static Vector2 Move
        {
            get
            {
                Init();
                Vector2 a = move.ReadValue<Vector2>(), b = XRAxis(XRNode.LeftHand, XRUsages.primary2DAxis);
                return Vector2.ClampMagnitude(a.sqrMagnitude >= b.sqrMagnitude ? a : b, 1f);
            }
        }
        public static float Turn { get { Init(); return Mathf.Clamp(Strongest(turn.ReadValue<float>(), XRAxis(XRNode.RightHand, XRUsages.primary2DAxis).x), -1f, 1f); } }

        // "Pressed this frame" checks only work reliably from Update, not FixedUpdate.
        public static bool RespawnPressed { get { Init(); return Pressed(0, respawn, XRNode.RightHand, XRUsages.secondaryButton); } }
        public static bool RecenterPressed { get { Init(); return Pressed(1, recenter, XRNode.LeftHand, XRUsages.primary2DAxisClick); } }
        public static bool MenuPressed { get { Init(); return Pressed(2, menu, XRNode.LeftHand, XRUsages.menuButton); } }

        // ---------------- Build mode (Free Roam) ----------------
        // VR: right stick click = enter/leave, right trigger = place, left trigger = delete, right grip = rotate,
        // A / X = next / previous item, B = undo, left stick = fly, right stick up/down = fly up/down.

        /// <summary>Walking: click the right stick to start running (it stops when you stop walking).</summary>
        public static bool RunPressed { get { Init(); return Pressed(4, runClick, XRNode.RightHand, XRUsages.primary2DAxisClick); } }
        /// <summary>Keyboard: hold Shift to run.</summary>
        public static bool RunKeyHeld => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        public static bool BuildTogglePressed { get { Init(); return Pressed(3, buildToggle, XRNode.RightHand, XRUsages.primary2DAxisClick); } }
        public static bool BuildPlaceHeld { get { Init(); return buildPlace.IsPressed() || XRVal(XRNode.RightHand, XRUsages.trigger) > 0.6f; } }
        public static bool BuildDeleteHeld { get { Init(); return buildDelete.IsPressed() || XRVal(XRNode.LeftHand, XRUsages.trigger) > 0.6f; } }
        public static bool BuildRotateHeld { get { Init(); return buildRotate.IsPressed() || XRGrip(XRNode.RightHand); } }
        public static bool BuildNextHeld { get { Init(); return buildNext.IsPressed() || XRBtn(XRNode.RightHand, XRUsages.primaryButton); } }
        public static bool BuildPrevHeld { get { Init(); return buildPrev.IsPressed() || XRBtn(XRNode.LeftHand, XRUsages.primaryButton); } }
        /// <summary>Push-to-talk: hold Y (left controller), or V on the keyboard.</summary>
        public static bool TalkHeld { get { Init(); return XRBtn(XRNode.LeftHand, XRUsages.secondaryButton) || Keyboard.current != null && Keyboard.current.vKey.isPressed; } }
        public static bool BuildFastHeld { get { Init(); return XRGrip(XRNode.LeftHand) || Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed; } }
        public static float FlyVertical { get { Init(); return Mathf.Clamp(Strongest(flyUp.ReadValue<float>(), XRAxis(XRNode.RightHand, XRUsages.primary2DAxis).y), -1f, 1f); } }

        /// <summary>True when the headset is on but neither controller is visible to Unity.</summary>
        public static bool ControllersMissing =>
            UnityEngine.XR.XRSettings.isDeviceActive && !Dev(XRNode.LeftHand).isValid && !Dev(XRNode.RightHand).isValid;

        // ---------------- Unity's built-in XR controller API (backup path) ----------------

        static XRDevice Dev(XRNode node) => UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);

        static float XRVal(XRNode node, UnityEngine.XR.InputFeatureUsage<float> usage)
        {
            var d = Dev(node);
            return d.isValid && d.TryGetFeatureValue(usage, out float v) ? v : 0f;
        }

        static bool XRBtn(XRNode node, UnityEngine.XR.InputFeatureUsage<bool> usage)
        {
            var d = Dev(node);
            return d.isValid && d.TryGetFeatureValue(usage, out bool v) && v;
        }

        static Vector2 XRAxis(XRNode node, UnityEngine.XR.InputFeatureUsage<Vector2> usage)
        {
            var d = Dev(node);
            if (!d.isValid || !d.TryGetFeatureValue(usage, out Vector2 v)) return Vector2.zero;
            return v.sqrMagnitude < 0.15f * 0.15f ? Vector2.zero : v; // small dead zone
        }

        static bool XRGrip(XRNode node) => XRBtn(node, XRUsages.gripButton) || XRVal(node, XRUsages.grip) > 0.6f;

        static float Strongest(float a, float b) => Mathf.Abs(a) >= Mathf.Abs(b) ? a : b;

        // ---------------- "pressed this frame" for buttons read two ways

        static readonly bool[] xrWas = new bool[5], xrNow = new bool[5];
        static readonly int[] xrFrame = { -1, -1, -1, -1, -1 }, firedFrame = { -1, -1, -1, -1, -1 };
        static readonly float[] quietUntil = new float[5];

        /// <summary>
        /// One physical press = one "pressed". The button is read through the Input System AND Unity's XR API
        /// (whichever works); both are checked every time, and after a press the button ignores further presses
        /// for a moment, so the two can never count the same press twice (which used to open the menu and
        /// close it again a frame later). Every caller in the same frame gets the same answer.
        /// </summary>
        static bool Pressed(int slot, InputAction action, XRNode node, UnityEngine.XR.InputFeatureUsage<bool> usage)
        {
            bool xr = XRPressed(slot, node, usage);           // always evaluated, so its state never goes stale
            bool input = action.WasPressedThisFrame();
            if (firedFrame[slot] == Time.frameCount) return true;
            if (!(xr || input) || Time.unscaledTime < quietUntil[slot]) return false;
            firedFrame[slot] = Time.frameCount;
            quietUntil[slot] = Time.unscaledTime + 0.3f;
            return true;
        }

        static bool XRPressed(int slot, XRNode node, UnityEngine.XR.InputFeatureUsage<bool> usage)
        {
            if (xrFrame[slot] != Time.frameCount)
            {
                bool now = XRBtn(node, usage);
                // If nobody read this button last frame, don't compare with an old state (that made phantom presses).
                xrWas[slot] = xrFrame[slot] == Time.frameCount - 1 ? xrNow[slot] : now;
                xrNow[slot] = now;
                xrFrame[slot] = Time.frameCount;
            }
            return xrNow[slot] && !xrWas[slot];
        }
    }
}
