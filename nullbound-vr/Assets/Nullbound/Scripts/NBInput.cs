using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Nullbound
{
    public struct NBHandState
    {
        public Vector3 pos;          // tracking space
        public Quaternion rot;
        public Vector3 aimPos;       // laser origin, tracking space
        public Quaternion aimRot;
        public bool tracked;
        public float trigger, grip;
        public Vector2 stick;
        public bool primary, secondary, stickClick, menu;
        public bool triggerDown, gripDown, primaryDown, secondaryDown, stickClickDown, menuDown;
        public bool TriggerHeld => trigger > 0.55f;
        public bool GripHeld => grip > 0.55f;
    }

    /// One place that reads the Quest controllers (or keyboard + mouse when no headset is
    /// connected, so the game can be tested in the Unity editor) and turns raw buttons into
    /// game actions.
    public static class NBInput
    {
        public static bool XRActive { get; private set; }
        public static Vector3 HeadPos;          // tracking space
        public static Quaternion HeadRot = Quaternion.identity;
        public static NBHandState Left, Right;

        // ---- game actions (computed every frame) ----
        public static Vector2 MoveAxis, TurnAxis;
        public static float DesktopYawDelta;    // desktop only: mouse turning
        public static bool Jump, Dash, Block, Ult, MenuToggle, UISelect;
        public static readonly bool[] Move = new bool[4];
        public static bool PointerFromMouse => !XRActive;

        static int s_LastFrame = -1;
        static readonly List<InputDevice> s_Tmp = new List<InputDevice>();
        static readonly InputFeatureUsage<Vector3> PointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");

        // desktop emulation state
        static float s_Pitch;
        static float s_PunchR = -1f, s_PunchL = -1f;
        const float PunchTime = 0.16f;

        public static NBHandState Main => NBSettings.LeftHanded ? Left : Right;
        public static NBHandState Off => NBSettings.LeftHanded ? Right : Left;

        public static void Poll()
        {
            if (Time.frameCount == s_LastFrame) return;
            s_LastFrame = Time.frameCount;

            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            XRActive = head.isValid && XRSettings.isDeviceActive;

            if (XRActive) PollXR(head);
            else PollDesktop();

            ComputeActions();
        }

        /// Fresh poses right before rendering (lower latency for head and hands).
        public static void PollPosesOnly()
        {
            if (!XRActive) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out var p)) HeadPos = p;
            else if (head.TryGetFeatureValue(CommonUsages.devicePosition, out p)) HeadPos = p;
            if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var r)) HeadRot = r;
            else if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out r)) HeadRot = r;
            ReadPose(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), ref Left);
            ReadPose(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), ref Right);
        }

        static void PollXR(InputDevice head)
        {
            PollPosesOnly();
            ReadHand(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), ref Left);
            ReadHand(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), ref Right);
        }

        static void ReadPose(InputDevice d, ref NBHandState h)
        {
            h.tracked = d.isValid;
            if (!d.isValid) return;
            if (d.TryGetFeatureValue(CommonUsages.devicePosition, out var p)) h.pos = p;
            if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out var r)) h.rot = r;
            if (d.TryGetFeatureValue(PointerPosition, out var ap) && d.TryGetFeatureValue(PointerRotation, out var ar))
            {
                h.aimPos = ap; h.aimRot = ar;
            }
            else
            {
                h.aimPos = h.pos; h.aimRot = h.rot;
            }
        }

        static void ReadHand(InputDevice d, ref NBHandState h)
        {
            bool pTrig = h.TriggerHeld, pGrip = h.GripHeld, pPri = h.primary, pSec = h.secondary, pStick = h.stickClick, pMenu = h.menu;
            if (d.isValid)
            {
                d.TryGetFeatureValue(CommonUsages.trigger, out h.trigger);
                d.TryGetFeatureValue(CommonUsages.grip, out h.grip);
                d.TryGetFeatureValue(CommonUsages.primary2DAxis, out h.stick);
                d.TryGetFeatureValue(CommonUsages.primaryButton, out h.primary);
                d.TryGetFeatureValue(CommonUsages.secondaryButton, out h.secondary);
                d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out h.stickClick);
                d.TryGetFeatureValue(CommonUsages.menuButton, out h.menu);
            }
            Edges(ref h, pTrig, pGrip, pPri, pSec, pStick, pMenu);
        }

        static void Edges(ref NBHandState h, bool pTrig, bool pGrip, bool pPri, bool pSec, bool pStick, bool pMenu)
        {
            h.triggerDown = h.TriggerHeld && !pTrig;
            h.gripDown = h.GripHeld && !pGrip;
            h.primaryDown = h.primary && !pPri;
            h.secondaryDown = h.secondary && !pSec;
            h.stickClickDown = h.stickClick && !pStick;
            h.menuDown = h.menu && !pMenu;
        }

        static void ComputeActions()
        {
            for (int i = 0; i < 4; i++) Move[i] = false;
            if (XRActive)
            {
                var main = Main; var off = Off;
                MoveAxis = off.stick;
                TurnAxis = main.stick;
                DesktopYawDelta = 0f;
                Jump = main.primaryDown;                 // A
                Dash = main.secondaryDown;               // B
                Move[0] = off.primaryDown;               // X
                Move[1] = off.secondaryDown;             // Y
                Move[2] = off.triggerDown;               // off-hand trigger
                Move[3] = main.triggerDown;              // main-hand trigger
                Block = Left.GripHeld && Right.GripHeld; // hold both grips
                Ult = (Left.stickClick && Right.stickClick) && (Left.stickClickDown || Right.stickClickDown);
                MenuToggle = Left.menuDown || Right.menuDown;
                UISelect = Right.triggerDown || Left.triggerDown;
            }
            else
            {
                DesktopActions();
            }
        }

        // ------------------------------------------------------------------
        // Desktop / editor fallback: WASD + mouse.  LMB/RMB punch, 1-4 moves,
        // Shift dash, Space jump, F block, G awaken, Esc menu.
        // ------------------------------------------------------------------
        static void PollDesktop()
        {
            float mx = 0, my = 0;
            bool lmb = false, rmb = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue();
                mx = d.x * 0.08f; my = d.y * 0.08f;
                lmb = mouse.leftButton.wasPressedThisFrame;
                rmb = mouse.rightButton.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            mx = Input.GetAxisRaw("Mouse X") * 2f; my = Input.GetAxisRaw("Mouse Y") * 2f;
            lmb = Input.GetMouseButtonDown(0); rmb = Input.GetMouseButtonDown(1);
#endif
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked) { mx = 0; my = 0; }
            DesktopYawDelta = mx;
            s_Pitch = Mathf.Clamp(s_Pitch - my, -80f, 80f);

            HeadPos = new Vector3(0, 1.62f, 0);
            HeadRot = Quaternion.Euler(s_Pitch, 0, 0);

            if (locked && lmb) s_PunchR = 0f;
            if (locked && rmb) s_PunchL = 0f;
            DesktopHand(ref Right, new Vector3(0.22f, -0.28f, 0.32f), ref s_PunchR);
            DesktopHand(ref Left, new Vector3(-0.22f, -0.28f, 0.32f), ref s_PunchL);

            bool pTrig = Right.TriggerHeld;
            Right.trigger = (!locked && (mouse0Down())) ? 1f : 0f;
            Right.triggerDown = Right.TriggerHeld && !pTrig;
        }

        static bool mouse0Down()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        static void DesktopHand(ref NBHandState h, Vector3 rest, ref float punchT)
        {
            var off = rest;
            if (punchT >= 0f)
            {
                punchT += Time.deltaTime;
                float k = punchT / PunchTime;
                float ext = k < 0.5f ? k * 2f : (1f - k) * 2f;
                off += new Vector3(-rest.x * 0.6f, 0.18f, 0.42f) * Mathf.Clamp01(ext);
                if (punchT > PunchTime) punchT = -1f;
            }
            h.tracked = true;
            h.pos = HeadPos + HeadRot * off;
            h.rot = HeadRot;
            h.aimPos = h.pos;
            h.aimRot = HeadRot;
        }

        static void DesktopActions()
        {
            MoveAxis = Vector2.zero; TurnAxis = Vector2.zero;
            Jump = Dash = Block = Ult = MenuToggle = UISelect = false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                MoveAxis = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                                       (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                Jump = k.spaceKey.wasPressedThisFrame;
                Dash = k.leftShiftKey.wasPressedThisFrame;
                Block = k.fKey.isPressed;
                Ult = k.gKey.wasPressedThisFrame;
                MenuToggle = k.escapeKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame;
                Move[0] = k.digit1Key.wasPressedThisFrame;
                Move[1] = k.digit2Key.wasPressedThisFrame;
                Move[2] = k.digit3Key.wasPressedThisFrame;
                Move[3] = k.digit4Key.wasPressedThisFrame;
                if (k.qKey.isPressed) DesktopYawDelta -= 120f * Time.deltaTime;
                if (k.eKey.isPressed) DesktopYawDelta += 120f * Time.deltaTime;
            }
            UISelect = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked;
#elif ENABLE_LEGACY_INPUT_MANAGER
            MoveAxis = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                   (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            Jump = Input.GetKeyDown(KeyCode.Space);
            Dash = Input.GetKeyDown(KeyCode.LeftShift);
            Block = Input.GetKey(KeyCode.F);
            Ult = Input.GetKeyDown(KeyCode.G);
            MenuToggle = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab);
            Move[0] = Input.GetKeyDown(KeyCode.Alpha1);
            Move[1] = Input.GetKeyDown(KeyCode.Alpha2);
            Move[2] = Input.GetKeyDown(KeyCode.Alpha3);
            Move[3] = Input.GetKeyDown(KeyCode.Alpha4);
            UISelect = Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked;
#endif
        }

        /// Mouse position ray (desktop UI pointing).
        public static bool TryMouseRay(Camera cam, out Ray ray)
        {
            ray = default;
            if (cam == null) return false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            ray = cam.ScreenPointToRay(Input.mousePosition);
            return true;
#else
            return false;
#endif
        }

        public static void Haptic(bool rightHand, float amplitude, float seconds)
        {
            if (!XRActive) return;
            var d = InputDevices.GetDeviceAtXRNode(rightHand ? XRNode.RightHand : XRNode.LeftHand);
            if (d.isValid && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
        }

        public static void SetFloorOrigin()
        {
            var list = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(list);
            foreach (var s in list)
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }
}
