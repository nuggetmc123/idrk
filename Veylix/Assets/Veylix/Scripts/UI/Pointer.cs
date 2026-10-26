using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Laser pointer for the world-space menus. Each controller has one; it only shows while a menu is up and the ray
    /// actually hits UI. Trigger presses, holding the trigger drags sliders. On a monitor the mouse does the same
    /// (one shared desktop pointer handled by the right-hand instance).
    /// </summary>
    public class Pointer : MonoBehaviour
    {
        public bool left;
        public static bool MenuActive;
        static Pointer lastUsed;

        LineRenderer line;
        Transform dot;
        VElement hover, pressed;
        const float Range = 6f;

        void Start()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.004f;
            line.endWidth = 0.0015f;
            line.numCapVertices = 2;
            var shader = Shader.Find("Veylix/UI") ?? Shader.Find("Sprites/Default");
            line.sharedMaterial = new Material(shader);
            line.startColor = new Color(1f, 0.45f, 0.25f, 0.9f);
            line.endColor = new Color(1f, 0.45f, 0.25f, 0.0f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(d.GetComponent<Collider>());
            d.transform.localScale = Vector3.one * 0.012f;
            d.GetComponent<Renderer>().sharedMaterial = line.sharedMaterial;
            dot = d.transform;
            dot.gameObject.SetActive(false);
            if (lastUsed == null && !left) lastUsed = this;
        }

        void Update()
        {
            InputHub.Poll();
            var rig = Rig.I;
            bool xr = rig != null && rig.IsXR;
            Ray ray;
            bool down, held, up;
            if (xr)
            {
                var h = left ? InputHub.Left : InputHub.Right;
                if (!h.valid) { Hide(); return; }
                // Aim along the controller, tilted like the Touch controller's pointing direction.
                ray = new Ray(transform.position, transform.rotation * Quaternion.Euler(35f, 0f, 0f) * Vector3.forward);
                down = h.triggerDown; held = h.triggerHeld; up = !h.triggerHeld && pressed != null;
                if (down) lastUsed = this;
            }
            else
            {
                if (left || rig == null || rig.cam == null || InputHub.Ms == null) { Hide(); return; }
                ray = rig.cam.ScreenPointToRay(InputHub.Ms.position.ReadValue());
                down = InputHub.Ms.leftButton.wasPressedThisFrame; held = InputHub.Ms.leftButton.isPressed; up = InputHub.Ms.leftButton.wasReleasedThisFrame;
            }
            bool inHub = Game.I != null && Game.I.state == Game.State.Menu;
            if (!MenuActive && !inHub) { Hide(); return; }

            VElement hit = null;
            Vector3 point = ray.origin + ray.direction * Range;
            if (Physics.Raycast(ray, out var rh, Range, 1 << Layers.UI, QueryTriggerInteraction.Collide))
            {
                hit = rh.collider.GetComponentInParent<VElement>();
                point = rh.point;
            }
            if (hit != hover)
            {
                if (hover != null) hover.Exit();
                hover = hit;
                if (hover != null) { hover.Enter(); if (xr) rig.Haptic(left, 0.12f, 0.015f); }
            }
            if (xr)
            {
                bool show = hit != null || lastUsed == this;
                line.enabled = show;
                if (show) { line.SetPosition(0, ray.origin); line.SetPosition(1, point); }
                dot.gameObject.SetActive(hit != null);
                if (hit != null) dot.position = point;
            }
            if (down && hover != null && hover.interactable)
            {
                pressed = hover;
                pressed.Press(pressed.transform.InverseTransformPoint(point));
                if (xr) rig.Haptic(left, 0.35f, 0.03f);
            }
            else if (held && pressed != null && pressed == hover)
                pressed.Drag(pressed.transform.InverseTransformPoint(point));
            if (up || !held) { if (pressed != null) pressed.Release(); pressed = null; }
        }

        void Hide()
        {
            if (line != null) line.enabled = false;
            if (dot != null) dot.gameObject.SetActive(false);
            if (hover != null) { hover.Exit(); hover = null; }
            pressed = null;
        }
    }
}
