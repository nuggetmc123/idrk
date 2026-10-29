using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Flopzone
{
    /// <summary>
    /// Reads the headset and both controllers through Unity's built-in XR
    /// input (works with OpenXR on Quest), with a keyboard + mouse stand-in
    /// so the game can be tried in the editor without a headset:
    ///   WASD move · mouse look (click to capture) · LMB fire · E / Q grab right / left
    ///   R reload · Space jump · F unload · Shift sprint · C crouch · Tab menu
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VRInput : MonoBehaviour
    {
        public static VRInput I { get; private set; }

        public const int Left = 0, Right = 1;

        struct Hand
        {
            public bool trigger, grip, primary, secondary, stick, menu, tracked;
            public float triggerValue, gripValue;
            public Vector2 axis;
            public Vector3 pos;
            public Quaternion rot;
        }

        Hand[] now = new Hand[2], prev = new Hand[2];
        InputDevice[] devices = new InputDevice[2];
        InputDevice headDevice;
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        float originCheck;
        bool floorOrigin = true;

        public bool XRActive { get; private set; }
        public Vector3 HeadPos { get; private set; } = new Vector3(0, 1.65f, 0);
        public Quaternion HeadRot { get; private set; } = Quaternion.identity;

        // desktop stand-in state
        float deskYaw, deskPitch;
        bool deskGripL, deskGripR;
        public bool DesktopReloadPressed { get; private set; }
        bool legacyInputWorks = true;

        void Awake()
        {
            I = this;
        }

        void Update()
        {
            Poll();
        }

        /// <summary>Also called from Application.onBeforeRender for low-latency poses.</summary>
        public void Poll()
        {
            if (Time.frameCount != lastPollFrame)
            {
                lastPollFrame = Time.frameCount;
                prev[0] = now[0];
                prev[1] = now[1];
                ReadButtons = true;
            }
            else ReadButtons = false;

            XRActive = XRSettings.isDeviceActive;
            if (XRActive) PollXR();
            else PollDesktop();
        }

        int lastPollFrame = -1;
        bool ReadButtons;

        void PollXR()
        {
            if (!headDevice.isValid) headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!devices[0].isValid) devices[0] = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!devices[1].isValid) devices[1] = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            if (Time.unscaledTime > originCheck)
            {
                originCheck = Time.unscaledTime + 3f;
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var s in subsystems)
                {
                    if (s.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
                        s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                    floorOrigin = s.GetTrackingOriginMode() == TrackingOriginModeFlags.Floor;
                }
            }

            float lift = floorOrigin ? 0f : 1.6f;
            if (headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 hp)) HeadPos = hp + Vector3.up * lift;
            if (headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion hr)) HeadRot = hr;

            for (int h = 0; h < 2; h++)
            {
                var d = devices[h];
                var s = now[h];
                if (d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) s.pos = p + Vector3.up * lift;
                if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r)) s.rot = r;
                s.tracked = d.isValid;
                if (ReadButtons)
                {
                    d.TryGetFeatureValue(CommonUsages.trigger, out s.triggerValue);
                    d.TryGetFeatureValue(CommonUsages.grip, out s.gripValue);
                    d.TryGetFeatureValue(CommonUsages.primaryButton, out s.primary);
                    d.TryGetFeatureValue(CommonUsages.secondaryButton, out s.secondary);
                    d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out s.stick);
                    d.TryGetFeatureValue(CommonUsages.primary2DAxis, out s.axis);
                    bool menu = false;
                    d.TryGetFeatureValue(CommonUsages.menuButton, out menu);
                    s.menu = menu;
                    // hysteresis so a half-held trigger doesn't chatter
                    s.trigger = s.triggerValue > (prev[h].trigger ? 0.35f : 0.6f);
                    s.grip = s.gripValue > (prev[h].grip ? 0.35f : 0.6f);
                }
                now[h] = s;
            }
            DesktopReloadPressed = false;
        }

        void PollDesktop()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!legacyInputWorks) return;
            try
            {
                if (ReadButtons)
                {
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        deskYaw += Input.GetAxis("Mouse X") * 2.2f;
                        deskPitch = Mathf.Clamp(deskPitch - Input.GetAxis("Mouse Y") * 2.2f, -85f, 85f);
                    }
                    if (Input.GetKeyDown(KeyCode.E)) deskGripR = !deskGripR;
                    if (Input.GetKeyDown(KeyCode.Q)) deskGripL = !deskGripL;
                    DesktopReloadPressed = Input.GetKeyDown(KeyCode.R);
                    var l = now[0];
                    var r = now[1];
                    l.axis = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                         (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
                    r.axis = new Vector2((Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0), 0);
                    r.trigger = Input.GetMouseButton(0);
                    r.triggerValue = r.trigger ? 1 : 0;
                    l.trigger = Input.GetMouseButton(1);
                    l.triggerValue = l.trigger ? 1 : 0;
                    r.grip = deskGripR; r.gripValue = r.grip ? 1 : 0;
                    l.grip = deskGripL; l.gripValue = l.grip ? 1 : 0;
                    r.primary = Input.GetKey(KeyCode.Space);
                    r.secondary = Input.GetKey(KeyCode.F);
                    l.primary = Input.GetKey(KeyCode.Z);
                    l.secondary = Input.GetKey(KeyCode.V);
                    l.stick = Input.GetKey(KeyCode.LeftShift);
                    r.stick = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
                    l.menu = Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.M);
                    now[0] = l;
                    now[1] = r;
                    if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && !MenuOpenForDesktop)
                        Cursor.lockState = CursorLockMode.Locked;
                    if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
                }
            }
            catch (InvalidOperationException)
            {
                // Project is set to the new Input System only; keyboard play is off.
                legacyInputWorks = false;
                Debug.LogWarning("[Flopzone] Keyboard/mouse test controls need Active Input Handling = Both (Flopzone > Run Setup sets it).");
            }
#endif
            HeadRot = Quaternion.Euler(deskPitch, deskYaw, 0);
            HeadPos = new Vector3(0, 1.65f, 0);
            var yawOnly = Quaternion.Euler(0, deskYaw, 0);
            var rr = now[1]; var ll = now[0];
            rr.pos = HeadPos + HeadRot * new Vector3(0.18f, -0.22f, 0.42f);
            rr.rot = HeadRot;
            ll.pos = HeadPos + yawOnly * new Vector3(-0.22f, -0.45f, 0.3f);
            ll.rot = yawOnly * Quaternion.Euler(20, 0, 0);
            rr.tracked = ll.tracked = true;
            now[1] = rr; now[0] = ll;
        }

        /// <summary>Set by the menu so desktop clicks go to the UI rather than capturing the mouse.</summary>
        public bool MenuOpenForDesktop { get; set; }

        public Vector3 HandPos(int h) => now[h].pos;
        public Quaternion HandRot(int h) => now[h].rot;
        public bool HandTracked(int h) => now[h].tracked;
        public float TriggerValue(int h) => now[h].triggerValue;
        public bool Trigger(int h) => now[h].trigger;
        public bool TriggerDown(int h) => now[h].trigger && !prev[h].trigger;
        public bool TriggerUp(int h) => !now[h].trigger && prev[h].trigger;
        public bool Grip(int h) => now[h].grip;
        public bool GripDown(int h) => now[h].grip && !prev[h].grip;
        public bool GripUp(int h) => !now[h].grip && prev[h].grip;
        public Vector2 MoveStick => now[0].axis;
        public Vector2 TurnStick => now[1].axis;
        public bool MenuDown => now[0].menu && !prev[0].menu;

        bool Get(Hand[] arr, XRButton b)
        {
            switch (b)
            {
                case XRButton.A: return arr[1].primary;
                case XRButton.B: return arr[1].secondary;
                case XRButton.X: return arr[0].primary;
                case XRButton.Y: return arr[0].secondary;
                case XRButton.LeftStickClick: return arr[0].stick;
                case XRButton.RightStickClick: return arr[1].stick;
                default: return false;
            }
        }

        public bool Held(XRButton b) => Get(now, b);
        public bool Down(XRButton b) => Get(now, b) && !Get(prev, b);
        public bool Up(XRButton b) => !Get(now, b) && Get(prev, b);
        public bool Held(ControlAction a) => Held(Save.Settings.Get(a));
        public bool Down(ControlAction a) => Down(Save.Settings.Get(a));
        public bool Up(ControlAction a) => Up(Save.Settings.Get(a));

        /// <summary>The first rebindable button pressed this frame (for Controls rebinding).</summary>
        public XRButton AnyButtonDown()
        {
            foreach (XRButton b in Enum.GetValues(typeof(XRButton)))
                if (b != XRButton.None && Down(b)) return b;
            return XRButton.None;
        }

        public void Haptic(int h, float amplitude, float seconds)
        {
            if (!XRActive) return;
            var d = devices[h];
            if (d.isValid && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
        }
    }
}
