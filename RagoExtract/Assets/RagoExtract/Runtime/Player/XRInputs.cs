using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Rago
{
    public struct HandInput
    {
        public float Grip, Trigger;
        public bool GripDown, GripUp, TriggerDown, TriggerUp;
        public bool Primary, PrimaryDown, Secondary, SecondaryDown;
        public Vector2 Stick;
        public bool StickClick, StickClickDown, Menu, MenuDown;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Tracked;
        public bool GripHeld => Grip > 0.55f;
    }

    /// <summary>
    /// Reads VR controllers through Unity's built-in XR input API (works with OpenXR on Quest and PC VR),
    /// and falls back to keyboard + mouse when no headset is active so the game is playable in the editor.
    /// </summary>
    public static class XRInputs
    {
        public static HandInput Left, Right;
        public static Vector3 HeadPos;
        public static Quaternion HeadRot = Quaternion.identity;
        public static bool HeadTracked;
        static bool _lg, _rg, _lt, _rt, _lp, _rp, _ls, _rs, _lc, _rc, _lm, _rm;
        static readonly List<XRInputSubsystem> _subs = new List<XRInputSubsystem>();
        static float _originSetTime;

        public static bool VR
        {
            get
            {
                try { return XRSettings.isDeviceActive; } catch { return false; }
            }
        }

        public static void Update()
        {
            if (VR)
            {
                if (Time.unscaledTime > _originSetTime)
                {
                    _originSetTime = Time.unscaledTime + 5f;
                    try
                    {
                        SubsystemManager.GetSubsystems(_subs);
                        foreach (var s in _subs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                    }
                    catch { }
                }
                ReadHand(XRNode.LeftHand, ref Left, ref _lg, ref _lt, ref _lp, ref _ls, ref _lc, ref _lm);
                ReadHand(XRNode.RightHand, ref Right, ref _rg, ref _rt, ref _rp, ref _rs, ref _rc, ref _rm);
                var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                HeadTracked = head.isValid;
                if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 hp) || head.TryGetFeatureValue(CommonUsages.devicePosition, out hp)) HeadPos = hp;
                if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion hr) || head.TryGetFeatureValue(CommonUsages.deviceRotation, out hr)) HeadRot = hr;
            }
            else
            {
                Desktop.Update(ref Left, ref Right);
            }
        }

        /// <summary>Re-read just the head and hand poses (no buttons) right before rendering, for the lowest latency.</summary>
        public static void RefreshPoses()
        {
            if (!VR) return;
            var l = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand); var r = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (l.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 lp)) Left.Position = lp;
            if (l.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion lr)) Left.Rotation = lr;
            if (r.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rp)) Right.Position = rp;
            if (r.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rr)) Right.Rotation = rr;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 hp) || head.TryGetFeatureValue(CommonUsages.devicePosition, out hp)) HeadPos = hp;
            if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion hr) || head.TryGetFeatureValue(CommonUsages.deviceRotation, out hr)) HeadRot = hr;
        }

        static void ReadHand(XRNode node, ref HandInput h, ref bool pg, ref bool pt, ref bool pp, ref bool ps, ref bool pc, ref bool pm)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            var n = new HandInput();
            n.Tracked = d.isValid;
            d.TryGetFeatureValue(CommonUsages.grip, out n.Grip);
            d.TryGetFeatureValue(CommonUsages.trigger, out n.Trigger);
            d.TryGetFeatureValue(CommonUsages.primaryButton, out n.Primary);
            d.TryGetFeatureValue(CommonUsages.secondaryButton, out n.Secondary);
            d.TryGetFeatureValue(CommonUsages.primary2DAxis, out n.Stick);
            d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out n.StickClick);
            d.TryGetFeatureValue(CommonUsages.menuButton, out n.Menu);
            if (d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) n.Position = p; else n.Position = h.Position;
            if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r)) n.Rotation = r; else n.Rotation = h.Rotation;
            bool g = n.Grip > 0.55f, t = n.Trigger > 0.6f;
            n.GripDown = g && !pg; n.GripUp = !g && pg;
            n.TriggerDown = t && !pt; n.TriggerUp = !t && pt;
            n.PrimaryDown = n.Primary && !pp; n.SecondaryDown = n.Secondary && !ps;
            n.StickClickDown = n.StickClick && !pc; n.MenuDown = n.Menu && !pm;
            pg = g; pt = t; pp = n.Primary; ps = n.Secondary; pc = n.StickClick; pm = n.Menu;
            h = n;
        }

        public static void Haptic(bool left, float amp, float dur)
        {
            if (!VR) return;
            try
            {
                var d = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
                if (d.isValid) d.SendHapticImpulse(0, Mathf.Clamp01(amp), dur);
            }
            catch { }
        }
    }

    /// <summary>Keyboard & mouse emulation of two VR hands (for testing in the editor / on PC without a headset).</summary>
    public static class Desktop
    {
        public static float Yaw, Pitch;
        public static bool Aiming, Crouch;
        public static Vector2 Move;
        public static bool Sprint;
        public static bool RightGripToggle, LeftGripToggle;
        public static bool Click, Reload, Interact, Drop, Backpack, Armor;
        public static bool Typing;
        public static bool MapKey, Jump, MenuKey;   // looking at a keyboard terminal: letters type instead of moving
        public static int Equip = -1;
        static bool _lt, _rp, _rs, _ok = true;

        static bool Key(KeyCode k) { try { return Input.GetKey(k); } catch { _ok = false; return false; } }
        static bool KeyDown(KeyCode k) { try { return Input.GetKeyDown(k); } catch { _ok = false; return false; } }
        static bool Mouse(int b) { try { return Input.GetMouseButton(b); } catch { return false; } }
        static bool MouseDown(int b) { try { return Input.GetMouseButtonDown(b); } catch { return false; } }
        static float Axis(string a) { try { return Input.GetAxisRaw(a); } catch { return 0; } }
        public static bool InputAvailable => _ok;

        public static void Update(ref HandInput left, ref HandInput right)
        {
            if (MouseDown(0) && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (KeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Yaw += Axis("Mouse X") * 2.2f;
                Pitch = Mathf.Clamp(Pitch - Axis("Mouse Y") * 2.2f, -85, 85);
            }
            if (Typing)
            {
                Move = Vector2.zero; Sprint = false; Reload = Interact = Drop = Backpack = Armor = MapKey = Jump = MenuKey = false; Equip = -1;
                Click = MouseDown(0);
                var rt = new HandInput { Tracked = true };
                rt.Grip = RightGripToggle ? 1 : 0; rt.GripDown = false; rt.GripUp = false;
                rt.Trigger = Mouse(0) && Cursor.lockState == CursorLockMode.Locked ? 1 : 0; rt.TriggerDown = MouseDown(0) && Cursor.lockState == CursorLockMode.Locked;
                right = rt;
                var lt2 = new HandInput { Tracked = true }; lt2.Grip = LeftGripToggle ? 1 : 0; left = lt2;
                return;
            }
            Move = new Vector2((Key(KeyCode.D) ? 1 : 0) - (Key(KeyCode.A) ? 1 : 0), (Key(KeyCode.W) ? 1 : 0) - (Key(KeyCode.S) ? 1 : 0));
            Sprint = Key(KeyCode.LeftShift);
            if (KeyDown(KeyCode.C) || KeyDown(KeyCode.LeftControl)) Crouch = !Crouch;
            Aiming = Mouse(1);
            if (KeyDown(KeyCode.E)) RightGripToggle = !RightGripToggle;
            if (KeyDown(KeyCode.Q)) LeftGripToggle = !LeftGripToggle;
            Reload = KeyDown(KeyCode.R);
            Interact = KeyDown(KeyCode.F);
            Drop = KeyDown(KeyCode.G);
            Backpack = KeyDown(KeyCode.Tab);
            Armor = KeyDown(KeyCode.H);
            MapKey = KeyDown(KeyCode.M);
            MenuKey = KeyDown(KeyCode.N);
            Jump = KeyDown(KeyCode.Space);
            Equip = KeyDown(KeyCode.Alpha1) ? 0 : KeyDown(KeyCode.Alpha2) ? 1 : KeyDown(KeyCode.Alpha3) ? 2 : -1;
            Click = MouseDown(0);

            var r = new HandInput { Tracked = true };
            r.Grip = RightGripToggle ? 1 : 0;
            r.Trigger = Mouse(0) && Cursor.lockState == CursorLockMode.Locked ? 1 : 0;
            r.TriggerDown = MouseDown(0) && Cursor.lockState == CursorLockMode.Locked;
            r.Primary = Key(KeyCode.X); r.PrimaryDown = r.Primary && !_rp; _rp = r.Primary;
            r.Secondary = Key(KeyCode.F); r.SecondaryDown = r.Secondary && !_rs; _rs = r.Secondary;
            r.GripDown = r.Grip > 0.5f && right.Grip < 0.5f; r.GripUp = r.Grip < 0.5f && right.Grip > 0.5f;
            right = r;
            var l = new HandInput { Tracked = true };
            l.Grip = LeftGripToggle ? 1 : 0;
            l.GripDown = l.Grip > 0.5f && left.Grip < 0.5f; l.GripUp = l.Grip < 0.5f && left.Grip > 0.5f;
            bool lt = Key(KeyCode.Z);
            l.Trigger = lt ? 1 : 0; l.TriggerDown = lt && !_lt; _lt = lt;
            left = l;
        }
    }
}
