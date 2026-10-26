using UnityEngine;
using UnityEngine.XR;

namespace Veylix
{
    /// <summary>
    /// The player's eyes and hands. In VR the head and both controllers are tracked (poses read straight from Unity's XR
    /// system just before each frame is drawn); without a headset it becomes a desktop camera with mouse look and a
    /// chase view. In a car the rig sits at the driver's eye point; Recenter lines your real head up with it, so it
    /// works seated or standing. Also owns the comfort vignette and screen fades.
    /// </summary>
    public class Rig : MonoBehaviour
    {
        public static Rig I { get; private set; }

        public Transform head, leftHand, rightHand;
        public Camera cam;
        public Glove leftGlove, rightGlove;
        public WristWatch watch;
        public bool IsXR => XRSettings.isDeviceActive;

        public enum View { Cockpit, Chase, Free }
        public View view = View.Cockpit;

        Car car;
        Vector3 recenterOffset, standPos;
        float standYaw;
        float recenterYaw;
        float mouseYaw, mousePitch;
        Vector3 chaseVel;
        Vector3 chasePos;
        Renderer vignette, fade;
        MaterialPropertyBlock mpb;
        float fadeAlpha, fadeTarget;
        float vignetteAlpha;
        Vector3 lastHeadPos;
        float recenterAt = -1f;

        public Car Car => car;

        void Awake()
        {
            I = this;
            if (head == null) Build();
            Cameras.Main = cam;
            AudioListenerRef.Listener = head;
            mpb = new MaterialPropertyBlock();
        }

        void OnEnable() { Application.onBeforeRender += BeforeRender; }
        void OnDisable() { Application.onBeforeRender -= BeforeRender; }

        void Build()
        {
            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);
            cam = head.gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 9000f;
            cam.fieldOfView = 75f;
            cam.tag = "MainCamera";
            cam.allowMSAA = true;
            cam.allowHDR = false;
            head.gameObject.AddComponent<AudioListener>();
            leftHand = new GameObject("LeftHand").transform;
            leftHand.SetParent(transform, false);
            rightHand = new GameObject("RightHand").transform;
            rightHand.SetParent(transform, false);
            leftGlove = Glove.Create(leftHand, true);
            watch = WristWatch.Create(leftGlove);
            rightGlove = Glove.Create(rightHand, false);
            leftHand.gameObject.AddComponent<Pointer>().left = true;
            rightHand.gameObject.AddComponent<Pointer>().left = false;
            vignette = Overlay("Vignette", 0.08f, VXAssets.I != null ? VXAssets.I.uiIcon : null, true);
            fade = Overlay("Fade", 0.06f, null, false);
        }

        Renderer Overlay(string name, float dist, Material mat, bool ring)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(head, false);
            q.transform.localPosition = new Vector3(0f, 0f, dist);
            q.transform.localScale = Vector3.one * dist * 3.2f;
            var r = q.GetComponent<Renderer>();
            var shader = Shader.Find("Veylix/Overlay") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { renderQueue = 4000 };
            if (ring) m.mainTexture = Vignette();
            m.color = new Color(0f, 0f, 0f, 0f);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            q.layer = Layers.UI;
            return r;
        }

        static Texture2D vigTex;
        static Texture2D Vignette()
        {
            if (vigTex != null) return vigTex;
            int n = 128;
            vigTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x / (n - 1f) - 0.5f, y / (n - 1f) - 0.5f).magnitude * 2f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, d));
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            vigTex.SetPixels32(px);
            vigTex.Apply();
            return vigTex;
        }

        // ------------------------------------------------------------------ placement

        public void SitIn(Car c)
        {
            car = c;
            view = View.Cockpit;
            var anchor = c.parts != null && c.parts.eyePoint != null ? c.parts.eyePoint : c.transform;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (c.parts != null && c.parts.helmet != null) c.parts.helmet.SetActive(false);
            recenterAt = Time.time + 0.15f;
            mouseYaw = mousePitch = 0f;
            chasePos = c.transform.position - c.transform.forward * 6f + Vector3.up * 2f;
        }

        public void StandAt(Vector3 pos, Quaternion rot)
        {
            if (car != null && car.parts != null && car.parts.helmet != null) car.parts.helmet.SetActive(true);
            car = null;
            view = View.Free;
            transform.SetParent(null, false);
            standPos = pos;
            standYaw = rot.eulerAngles.y;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, standYaw, 0f);
            recenterOffset = Vector3.zero;
            recenterYaw = 0f;
            if (!IsXR) { head.localPosition = new Vector3(0f, 1.6f, 0f); head.localRotation = Quaternion.identity; mouseYaw = mousePitch = 0f; }
            recenterAt = Time.time + 0.15f;
        }

        /// <summary>Lines your real head up with the seat (or the standing spot). Call from the menu or by holding both sticks.</summary>
        public void Recenter()
        {
            if (!IsXR)
            {
                head.localPosition = car != null ? Vector3.zero : new Vector3(0f, 1.6f, 0f);
                return;
            }
            var d = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!d.isValid) d = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!d.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 p)) d.TryGetFeatureValue(CommonUsages.devicePosition, out p);
            d.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion r);
            recenterYaw = r.eulerAngles.y;
            var yawInv = Quaternion.Euler(0f, -recenterYaw, 0f);
            Vector3 rotated = yawInv * p;
            if (car != null)
                recenterOffset = -rotated + new Vector3(0f, Settings.SeatHeight, Settings.SeatForward);
            else
                recenterOffset = new Vector3(-rotated.x, Settings.Seated ? 1.6f - rotated.y : 0f, -rotated.z);
            ApplyRigOffset();
        }

        void ApplyRigOffset()
        {
            if (car != null && view == View.Cockpit)
            {
                transform.localRotation = Quaternion.Euler(0f, -recenterYaw, 0f);
                transform.localPosition = recenterOffset;
            }
            else if (car == null)
            {
                // Standing spot: put the tracked head over it, facing the way the spot faces.
                transform.rotation = Quaternion.Euler(0f, standYaw - recenterYaw, 0f);
                transform.position = standPos + Quaternion.Euler(0f, standYaw, 0f) * recenterOffset;
            }
        }

        // ------------------------------------------------------------------ per frame

        void BeforeRender()
        {
            if (!IsXR) return;
            ApplyPose(head, XRNode.CenterEye, CommonUsages.centerEyePosition, CommonUsages.centerEyeRotation);
            ApplyHand(leftHand, XRNode.LeftHand);
            ApplyHand(rightHand, XRNode.RightHand);
        }

        static void ApplyPose(Transform t, XRNode node, InputFeatureUsage<Vector3> pu, InputFeatureUsage<Quaternion> ru)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid) return;
            if (d.TryGetFeatureValue(pu, out Vector3 p)) t.localPosition = p;
            if (d.TryGetFeatureValue(ru, out Quaternion r) && r.w * r.w + r.x * r.x + r.y * r.y + r.z * r.z > 0.5f) t.localRotation = r;
        }

        static void ApplyHand(Transform t, XRNode node)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid) return;
            if (d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) t.localPosition = p;
            if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r) && r.w * r.w + r.x * r.x + r.y * r.y + r.z * r.z > 0.5f) t.localRotation = r;
        }

        void Update()
        {
            InputHub.Poll();
            if (recenterAt > 0f && Time.time >= recenterAt) { recenterAt = -1f; Recenter(); }
            // Hold both thumbsticks in to recenter (seated players do this a lot).
            if (InputHub.Left.stickClick && InputHub.Right.stickClick && (InputHub.Left.stickClickDown || InputHub.Right.stickClickDown)) Recenter();
            if (!IsXR) DesktopUpdate();
            Walk();
            if (InputHub.KeyDown(UnityEngine.InputSystem.Key.C) || (InputHub.Pad != null && InputHub.Pad.buttonNorth.wasPressedThisFrame)) ToggleChase();
            UpdateFade();
        }

        void LateUpdate()
        {
            if (car == null) return;
            if (view == View.Cockpit)
            {
                ApplyRigOffset();
                if (Settings.HorizonLock && car.parts != null && car.parts.eyePoint != null)
                {
                    // Keep the horizon level: only follow the car's heading, not its pitch and roll.
                    Vector3 f = car.transform.forward; f.y = 0f;
                    if (f.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(0f, -recenterYaw, 0f);
                }
                Comfort();
            }
            else if (view == View.Chase)
            {
                // Smooth, level chase camera behind the car (always level: no rolling with crashes).
                Vector3 f = car.Body != null && car.Body.linearVelocity.sqrMagnitude > 4f ? car.Body.linearVelocity.normalized : car.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
                f.Normalize();
                Vector3 target = car.transform.position - f * 6.5f + Vector3.up * 2.2f;
                chasePos = Vector3.SmoothDamp(chasePos, target, ref chaseVel, 0.18f);
                transform.SetParent(null, true);
                Vector3 look = car.transform.position + Vector3.up * 0.8f - chasePos;
                transform.rotation = Quaternion.LookRotation(new Vector3(look.x, 0f, look.z).normalized);
                transform.position = chasePos - (IsXR ? transform.rotation * new Vector3(head.localPosition.x, head.localPosition.y - 0.1f, head.localPosition.z) : Vector3.zero);
                if (!IsXR) head.localRotation = Quaternion.Euler(Mathf.Clamp(Vector3.SignedAngle(new Vector3(look.x, 0, look.z), look, transform.right), -30f, 30f), 0f, 0f);
                if (vignette != null) vignette.enabled = false;
            }
            Vector3 hv = (head.position - lastHeadPos) / Mathf.Max(1e-4f, Time.deltaTime);
            AudioListenerRef.Velocity = hv;
            lastHeadPos = head.position;
        }

        public void ToggleChase()
        {
            if (car == null) return;
            SetView(view == View.Cockpit ? View.Chase : View.Cockpit);
        }

        public void SetView(View v)
        {
            if (car == null) return;
            view = v;
            if (v == View.Cockpit)
            {
                var anchor = car.parts != null && car.parts.eyePoint != null ? car.parts.eyePoint : car.transform;
                transform.SetParent(anchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                if (car.parts != null && car.parts.helmet != null) car.parts.helmet.SetActive(false);
                ApplyRigOffset();
            }
            else
            {
                chasePos = car.transform.position - car.transform.forward * 6.5f + Vector3.up * 2.2f;
                if (car.parts != null && car.parts.helmet != null) car.parts.helmet.SetActive(true);
            }
        }

        void DesktopUpdate()
        {
            var ms = InputHub.Ms;
            if (ms != null && (ms.rightButton.isPressed || view == View.Free && car == null && ms.middleButton.isPressed))
            {
                Vector2 d = ms.delta.ReadValue();
                mouseYaw += d.x * 0.12f;
                mousePitch = Mathf.Clamp(mousePitch - d.y * 0.12f, -70f, 70f);
            }
            else if (car != null)
            {
                mouseYaw = Mathf.Lerp(mouseYaw, 0f, Time.deltaTime * 2f);
                mousePitch = Mathf.Lerp(mousePitch, 0f, Time.deltaTime * 2f);
            }
            if (view == View.Cockpit || car == null)
            {
                head.localRotation = Quaternion.Euler(mousePitch, mouseYaw, 0f);
                if (car != null) head.localPosition = Vector3.zero;
            }
            // Desktop gloves rest on the steering wheel (the cockpit code places them).
        }

        // ------------------------------------------------------------------ walking around the garage

        /// Set while you're in the garage hub: the floor area you can walk around (null in a car).
        public Bounds? walkArea;
        bool snapReady = true;

        /// Left stick walks (where you look), right stick snap-turns 30 degrees. Desktop: WASD / arrows walk, Q/E turn,
        /// hold the right mouse button to look around. A gamepad's left stick walks too.
        void Walk()
        {
            if (car != null || walkArea == null) return;
            float dt = Time.deltaTime;
            Vector2 move = Vector2.zero;
            float turn = 0f;
            if (IsXR)
            {
                move = InputHub.Left.stick;
                float tx = InputHub.Right.stick.x;
                if (Mathf.Abs(tx) < 0.35f) snapReady = true;
                else if (snapReady && Mathf.Abs(tx) > 0.75f) { turn = 30f * Mathf.Sign(tx); snapReady = false; }
            }
            else
            {
                var kb = InputHub.Kb;
                if (kb != null)
                {
                    move.y += (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                    move.x += (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                    mouseYaw += ((kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f)) * 90f * dt;
                }
                if (InputHub.Pad != null) { move += InputHub.Pad.leftStick.ReadValue(); mouseYaw += InputHub.Pad.rightStick.ReadValue().x * 120f * dt; }
            }
            if (move.sqrMagnitude < 0.02f && turn == 0f) return;
            move = Vector2.ClampMagnitude(move, 1f);
            Vector3 f = head.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = transform.forward;
            f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 p = standPos + (f * move.y + r * move.x) * 1.7f * dt;
            var b = walkArea.Value;
            p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
            p.z = Mathf.Clamp(p.z, b.min.z, b.max.z);
            standPos = p;
            standYaw += turn;
            if (IsXR) ApplyRigOffset();
            else { transform.position = standPos; transform.rotation = Quaternion.Euler(0f, standYaw, 0f); }
        }

        // ------------------------------------------------------------------ comfort

        void Comfort()
        {
            if (vignette == null) return;
            float strength = Settings.Vignette;
            float target = 0f;
            if (strength > 0.01f && car != null && car.Body != null)
            {
                float yawRate = Mathf.Abs(car.Body.angularVelocity.y);
                float roll = Mathf.Abs(Vector3.Dot(car.Body.angularVelocity, car.transform.forward)) + Mathf.Abs(Vector3.Dot(car.Body.angularVelocity, car.transform.right));
                target = Mathf.Clamp01((yawRate - 0.9f) * 0.5f + (roll - 0.8f) * 0.6f + (car.Airborne ? 0.4f : 0f)) * strength;
            }
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, target, Time.deltaTime * 3f);
            vignette.enabled = vignetteAlpha > 0.01f;
            if (vignette.enabled)
            {
                vignette.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", new Color(0f, 0f, 0f, vignetteAlpha));
                vignette.SetPropertyBlock(mpb);
            }
        }

        /// Loading screens float in a dark void instead of the sky.
        public void SetVoid(bool on)
        {
            if (cam == null) return;
            cam.clearFlags = on ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.015f, 0.016f, 0.02f, 1f);
        }

        public void FadeTo(float alpha) { fadeTarget = alpha; }
        public bool Faded => fadeAlpha > 0.98f;
        public void FadeInstant(float a) { fadeAlpha = fadeTarget = a; UpdateFade(); }

        void UpdateFade()
        {
            if (fade == null) return;
            fadeAlpha = Mathf.MoveTowards(fadeAlpha, fadeTarget, Time.unscaledDeltaTime * 2.5f);
            fade.enabled = fadeAlpha > 0.005f;
            if (fade.enabled)
            {
                fade.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", new Color(0f, 0f, 0f, fadeAlpha));
                fade.SetPropertyBlock(mpb);
            }
        }

        public void Haptic(bool left, float amp, float dur)
        {
            InputHub.Haptic(left ? XRNode.LeftHand : XRNode.RightHand, amp * Settings.Haptics, dur);
        }
    }
}
