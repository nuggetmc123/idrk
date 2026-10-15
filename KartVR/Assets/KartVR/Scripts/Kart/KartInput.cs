using UnityEngine;
using UnityEngine.InputSystem;

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
    ///           B = respawn, left stick click = recenter view, Y = show/hide wrist menu.
    /// Walking (VR, hub): Left stick = walk, Right stick = snap turn.
    /// Keyboard: W/S or arrows = gas/brake (walk), A/D = steer (strafe),
    ///           Q/E = turn, Shift = booster, Space = drift,
    ///           R = respawn, C = recenter, Tab = wrist menu,
    ///           hold right mouse = look around, left click = press buttons.
    /// </summary>
    public static class KartInput
    {
        static InputAction gas, brake, steer, drift, respawn, recenter, menu, boost, move, turn, gripLeft, gripRight;
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
            menu.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
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

        public static float Gas { get { Init(); return Mathf.Clamp01(gas.ReadValue<float>()); } }
        public static float Brake { get { Init(); return Mathf.Clamp01(brake.ReadValue<float>()); } }
        public static float Steer { get { Init(); return Mathf.Clamp(steer.ReadValue<float>(), -1f, 1f); } }
        public static bool DriftHeld { get { Init(); return drift.IsPressed(); } }
        public static bool LeftGripHeld { get { Init(); return gripLeft.IsPressed(); } }
        public static bool RightGripHeld { get { Init(); return gripRight.IsPressed(); } }
        public static bool BoostHeld { get { Init(); return boost.IsPressed(); } }
        public static Vector2 Move { get { Init(); return Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f); } }
        public static float Turn { get { Init(); return Mathf.Clamp(turn.ReadValue<float>(), -1f, 1f); } }

        // "Pressed this frame" checks only work reliably from Update, not FixedUpdate.
        public static bool RespawnPressed { get { Init(); return respawn.WasPressedThisFrame(); } }
        public static bool RecenterPressed { get { Init(); return recenter.WasPressedThisFrame(); } }
        public static bool MenuPressed { get { Init(); return menu.WasPressedThisFrame(); } }
    }
}
