using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRDevice = UnityEngine.XR.InputDevice;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace Nullbound
{
    /// <summary>
    /// All fighting controls, created in code so there is no Input Actions asset to wire up. Every action has a VR
    /// binding plus keyboard and gamepad fallbacks, so you can test in the editor without a headset.
    ///
    /// VR: PUNCH = really punch (swing your fist fast), BLOCK = put both fists up in front of your face,
    ///     Left stick = move, Right stick = turn, A = jump, B = dodge dash (the way the left stick points, or back),
    ///     X = special ability, menu button (three lines) = wrist menu, click the right stick = recenter.
    /// Keyboard: W/A/S/D move, Q/E turn, Space jump, Shift dash, F special, left click = punch (right),
    ///     G = punch (left), hold H = block, C = recenter, Tab/Esc = menu, hold right mouse = look around.
    /// </summary>
    public static class FightInput
    {
        static InputAction jump, dash, special, recenter, menu, move, turn, gripLeft, gripRight, punchLeftKey, punchRightKey, blockKey;
        static bool initialized;

        static void Init()
        {
            if (initialized) return;
            initialized = true;

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            jump.AddBinding("<Gamepad>/buttonSouth");
            jump.AddBinding("<Keyboard>/space");

            dash = new InputAction("Dash", InputActionType.Button);
            dash.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            dash.AddBinding("<Gamepad>/buttonEast");
            dash.AddBinding("<Keyboard>/leftShift");

            special = new InputAction("Special", InputActionType.Button);
            special.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            special.AddBinding("<Gamepad>/buttonWest");
            special.AddBinding("<Keyboard>/f");

            recenter = new InputAction("Recenter", InputActionType.Button);
            recenter.AddBinding("<Gamepad>/select");
            recenter.AddBinding("<Keyboard>/c");

            menu = new InputAction("Menu", InputActionType.Button);
            menu.AddBinding("<XRController>{LeftHand}/{MenuButton}"); // the three-lines button
            menu.AddBinding("<Keyboard>/escape");
            menu.AddBinding("<Gamepad>/start");
            menu.AddBinding("<Keyboard>/tab");

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

            gripLeft = new InputAction("GripLeft", InputActionType.Button);
            gripLeft.AddBinding("<XRController>{LeftHand}/{GripButton}");
            gripRight = new InputAction("GripRight", InputActionType.Button);
            gripRight.AddBinding("<XRController>{RightHand}/{GripButton}");

            // Desktop testing: punches and block on the keyboard / mouse.
            punchRightKey = new InputAction("PunchR", InputActionType.Button);
            punchRightKey.AddBinding("<Mouse>/leftButton");
            punchRightKey.AddBinding("<Gamepad>/rightShoulder");
            punchLeftKey = new InputAction("PunchL", InputActionType.Button);
            punchLeftKey.AddBinding("<Keyboard>/g");
            punchLeftKey.AddBinding("<Gamepad>/leftShoulder");
            blockKey = new InputAction("Block", InputActionType.Button);
            blockKey.AddBinding("<Keyboard>/h");
            blockKey.AddBinding("<Gamepad>/leftTrigger");

            foreach (var a in new[] { jump, dash, special, recenter, menu, move, turn, gripLeft, gripRight, punchLeftKey, punchRightKey, blockKey })
                a.Enable();
        }

        // Every control reads BOTH the Input System and Unity's built-in XR controller API and uses whichever is
        // active, so VR controllers keep working even if the Input System isn't receiving them.
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
        /// <summary>Trigger pulls (used to click menu buttons with the laser pointer).</summary>
        public static float LeftTrigger => XRVal(XRNode.LeftHand, XRUsages.trigger);
        public static float RightTrigger => XRVal(XRNode.RightHand, XRUsages.trigger);
        public static bool LeftGripHeld { get { Init(); return gripLeft.IsPressed() || XRGrip(XRNode.LeftHand); } }
        public static bool RightGripHeld { get { Init(); return gripRight.IsPressed() || XRGrip(XRNode.RightHand); } }

        // "Pressed this frame" checks only work reliably from Update, not FixedUpdate.
        public static bool JumpPressed { get { Init(); return Pressed(0, jump, XRNode.RightHand, XRUsages.primaryButton); } }
        public static bool DashPressed { get { Init(); return Pressed(1, dash, XRNode.RightHand, XRUsages.secondaryButton); } }
        public static bool SpecialPressed { get { Init(); return Pressed(3, special, XRNode.LeftHand, XRUsages.primaryButton); } }
        public static bool MenuPressed { get { Init(); return Pressed(2, menu, XRNode.LeftHand, XRUsages.menuButton); } }
        public static bool RecenterPressed { get { Init(); return Pressed(4, recenter, XRNode.RightHand, XRUsages.primary2DAxisClick); } }

        /// <summary>Desktop only: a keyboard / mouse punch (in VR you just punch).</summary>
        public static bool KeyPunchRight { get { Init(); return punchRightKey.WasPressedThisFrame(); } }
        public static bool KeyPunchLeft { get { Init(); return punchLeftKey.WasPressedThisFrame(); } }
        public static bool KeyBlockHeld { get { Init(); return blockKey.IsPressed(); } }

        /// <summary>Push-to-talk: hold the left stick in, or V on the keyboard.</summary>
        public static bool TalkHeld { get { Init(); return XRBtn(XRNode.LeftHand, XRUsages.primary2DAxisClick) || Keyboard.current != null && Keyboard.current.vKey.isPressed; } }

        /// <summary>A short buzz on one controller (hits, blocks, abilities).</summary>
        public static void Buzz(bool left, float strength, float seconds)
        {
            var d = Dev(left ? XRNode.LeftHand : XRNode.RightHand);
            if (d.isValid) d.SendHapticImpulse(0u, Mathf.Clamp01(strength), seconds);
        }

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

        static readonly bool[] xrWas = new bool[6], xrNow = new bool[6];
        static readonly int[] xrFrame = { -1, -1, -1, -1, -1, -1 }, firedFrame = { -1, -1, -1, -1, -1, -1 };
        static readonly float[] quietUntil = new float[6];

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
