using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Flopzone
{
    /// <summary>Lasers from both hands (or the mouse) that hover and click UIButtons.</summary>
    public class UIPointer : MonoBehaviour
    {
        public static UIPointer I { get; private set; }

        public Transform[] hands = new Transform[2];
        LineRenderer[] lines = new LineRenderer[2];
        Transform[] dots = new Transform[2];
        public bool Active { get; set; }
        float keepUntil;
        /// <summary>A screen in the world (loadout station, the plane's mode screen) wants lasers while you're near it.</summary>
        public void KeepOn() => keepUntil = Time.time + 0.25f;
        bool Nearby => !Active && Time.time < keepUntil;
        /// <summary>True while a laser is on a button: guns don't fire then.</summary>
        public bool OverUI { get; private set; }

        void Awake()
        {
            I = this;
            for (int h = 0; h < 2; h++)
            {
                var go = new GameObject("laser" + h);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.004f;
                lr.endWidth = 0.002f;
                lr.useWorldSpace = true;
                lr.sharedMaterial = Prims.Sprite(new Color(1f, 0.6f, 0.2f, 0.8f));
                lr.sharedMaterial.renderQueue = 3150;
                lines[h] = lr;
                dots[h] = Prims.Sphere(transform, Vector3.zero, 0.012f, Prims.Unlit(UIKit.Accent)).transform;
            }
        }

        void Update()
        {
            OverUI = false;
            var input = VRInput.I;
            for (int h = 0; h < 2; h++)
            {
                bool show = (Active || Nearby) && input != null && input.XRActive && hands[h] != null;
                lines[h].enabled = show;
                dots[h].gameObject.SetActive(false);
                if (!show) continue;
                var origin = hands[h].position;
                var dir = hands[h].forward;
                float len = 3f;
                if (Physics.Raycast(origin, dir, out var hit, 6f, Layers.UIMask, QueryTriggerInteraction.Collide))
                {
                    len = hit.distance;
                    dots[h].gameObject.SetActive(true);
                    dots[h].position = hit.point;
                    var b = hit.collider.GetComponentInParent<UIButton>();
                    if (b != null)
                    {
                        OverUI = true;
                        b.Hover();
                        if (input.TriggerDown(h)) { b.Click(); input.Haptic(h, 0.3f, 0.03f); }
                    }
                    else if (hit.collider.GetComponentInParent<UIPanel>() != null) OverUI = true;
                }
                lines[h].SetPosition(0, origin);
                lines[h].SetPosition(1, origin + dir * len);
            }
            if ((Active || Nearby) && input != null && !input.XRActive) DesktopPointer();
        }

        void DesktopPointer()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            var cam = Camera.main;
            if (cam == null) return;
            try
            {
                // near a screen in the world, aim with the middle of the view so mouse-look keeps working
                if (Cursor.lockState == CursorLockMode.Locked && !Nearby) Cursor.lockState = CursorLockMode.None;
                var ray = Nearby && Cursor.lockState == CursorLockMode.Locked
                    ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0))
                    : cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 8f, Layers.UIMask, QueryTriggerInteraction.Collide))
                {
                    var b = hit.collider.GetComponentInParent<UIButton>();
                    OverUI = true;
                    if (b != null)
                    {
                        b.Hover();
                        if (Input.GetMouseButtonDown(0)) b.Click();
                    }
                }
            }
            catch (InvalidOperationException) { }
#endif
        }
    }
}
