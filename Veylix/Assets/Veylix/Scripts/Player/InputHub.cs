using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Veylix
{
    /// <summary>
    /// Every input in one place: Quest controllers through Unity's XR input (grip, trigger, buttons, sticks, haptics),
    /// plus keyboard, mouse and gamepad for playing without a headset. Read once per frame; edge flags
    /// ("pressed this frame") are worked out here so every system agrees.
    /// </summary>
    public static class InputHub
    {
        public struct Hand
        {
            public bool valid;
            public float grip, trigger;
            public bool gripHeld, triggerHeld, primary, secondary, stickClick, menu;
            public bool gripDown, gripUp, triggerDown, primaryDown, secondaryDown, stickClickDown, menuDown;
            public Vector2 stick;
            public Vector3 position;
            public Quaternion rotation;
        }

        public static Hand Left, Right;
        static Hand prevL, prevR;
        static int frame = -1;
        public static bool XR => XRSettings.isDeviceActive;

        static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");

        public static void Poll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            prevL = Left; prevR = Right;
            Left = Read(XRNode.LeftHand, prevL);
            Right = Read(XRNode.RightHand, prevR);
        }

        static Hand Read(XRNode node, Hand prev)
        {
            var h = new Hand();
            var d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid) return h;
            h.valid = true;
            d.TryGetFeatureValue(CommonUsages.grip, out h.grip);
            d.TryGetFeatureValue(CommonUsages.trigger, out h.trigger);
            d.TryGetFeatureValue(CommonUsages.primaryButton, out h.primary);
            d.TryGetFeatureValue(CommonUsages.secondaryButton, out h.secondary);
            d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out h.stickClick);
            d.TryGetFeatureValue(CommonUsages.menuButton, out h.menu);
            d.TryGetFeatureValue(CommonUsages.primary2DAxis, out h.stick);
            d.TryGetFeatureValue(CommonUsages.devicePosition, out h.position);
            if (!d.TryGetFeatureValue(CommonUsages.deviceRotation, out h.rotation)) h.rotation = Quaternion.identity;
            // Hysteresis so a half-squeezed grip doesn't flicker.
            h.gripHeld = prev.gripHeld ? h.grip > 0.35f : h.grip > 0.6f;
            h.triggerHeld = prev.triggerHeld ? h.trigger > 0.4f : h.trigger > 0.7f;
            h.gripDown = h.gripHeld && !prev.gripHeld;
            h.gripUp = !h.gripHeld && prev.gripHeld;
            h.triggerDown = h.triggerHeld && !prev.triggerHeld;
            h.primaryDown = h.primary && !prev.primary;
            h.secondaryDown = h.secondary && !prev.secondary;
            h.stickClickDown = h.stickClick && !prev.stickClick;
            h.menuDown = h.menu && !prev.menu;
            return h;
        }

        public static void Haptic(XRNode node, float amplitude, float seconds)
        {
            if (amplitude <= 0.001f) return;
            var d = InputDevices.GetDeviceAtXRNode(node);
            if (d.isValid && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
        }

        // ------------------------------------------------------------------ desktop / gamepad

        public static Keyboard Kb => Keyboard.current;
        public static Mouse Ms => Mouse.current;
        public static Gamepad Pad => Gamepad.current;

        public static bool Key(Key k) => Kb != null && Kb[k].isPressed;
        public static bool KeyDown(Key k) => Kb != null && Kb[k].wasPressedThisFrame;

        public static float PadSteer => Pad != null ? Pad.leftStick.ReadValue().x : 0f;
        public static float PadThrottle => Pad != null ? Pad.rightTrigger.ReadValue() : 0f;
        public static float PadBrake => Pad != null ? Pad.leftTrigger.ReadValue() : 0f;

        /// <summary>Pause / menu from any device.</summary>
        public static bool MenuPressed
        {
            get
            {
                Poll();
                return Left.menuDown || KeyDown(UnityEngine.InputSystem.Key.Escape) || (Pad != null && Pad.startButton.wasPressedThisFrame);
            }
        }
    }
}
