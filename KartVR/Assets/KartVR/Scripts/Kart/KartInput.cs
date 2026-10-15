using UnityEngine;
using UnityEngine.InputSystem;

namespace KartVR
{
    /// <summary>
    /// All kart controls, created in code so there is no Input Actions asset to
    /// wire up. Every action has a VR binding plus keyboard and gamepad
    /// fallbacks, so you can test in the editor without a headset.
    ///
    /// VR:       Right trigger = gas, Left trigger = brake/reverse,
    ///           Left stick = steer, Grip (either) = drift,
    ///           B = respawn, X = recenter view, Y = show/hide wrist menu.
    /// Keyboard: W/S or arrows = gas/brake, A/D = steer, Space = drift,
    ///           R = respawn, C = recenter, Tab = wrist menu,
    ///           hold right mouse = look around, left click = press buttons.
    /// </summary>
    public static class KartInput
    {
        static InputAction gas, brake, steer, drift, respawn, recenter, menu;
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
            drift.AddBinding("<XRController>{RightHand}/{GripButton}");
            drift.AddBinding("<XRController>{LeftHand}/{GripButton}");
            drift.AddBinding("<Gamepad>/rightShoulder");
            drift.AddBinding("<Keyboard>/space");

            respawn = new InputAction("Respawn", InputActionType.Button);
            respawn.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            respawn.AddBinding("<Gamepad>/buttonNorth");
            respawn.AddBinding("<Keyboard>/r");

            recenter = new InputAction("Recenter", InputActionType.Button);
            recenter.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            recenter.AddBinding("<Gamepad>/select");
            recenter.AddBinding("<Keyboard>/c");

            menu = new InputAction("Menu", InputActionType.Button);
            menu.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
            menu.AddBinding("<Gamepad>/start");
            menu.AddBinding("<Keyboard>/tab");

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

        // "Pressed this frame" checks only work reliably from Update, not FixedUpdate.
        public static bool RespawnPressed { get { Init(); return respawn.WasPressedThisFrame(); } }
        public static bool RecenterPressed { get { Init(); return recenter.WasPressedThisFrame(); } }
        public static bool MenuPressed { get { Init(); return menu.WasPressedThisFrame(); } }
    }
}
