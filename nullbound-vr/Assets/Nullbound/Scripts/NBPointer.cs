using UnityEngine;

namespace Nullbound
{
    /// Controller laser for the menus (mouse cursor in the editor).
    public class NBPointer : MonoBehaviour
    {
        LineRenderer m_Line;
        Transform m_Dot;
        NBButton m_Hover;
        bool m_UseLeft;

        public static NBPointer Create(NBRig rig)
        {
            var g = new GameObject("NB_Pointer");
            g.transform.SetParent(rig.transform, false);
            var p = g.AddComponent<NBPointer>();
            p.m_Line = g.AddComponent<LineRenderer>();
            p.m_Line.positionCount = 2;
            p.m_Line.startWidth = 0.006f; p.m_Line.endWidth = 0.002f;
            p.m_Line.sharedMaterial = NBMat.Glow(new Color(0.8f, 0.6f, 1f, 0.8f));
            p.m_Line.useWorldSpace = true;
            p.m_Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.m_Dot = NBMat.Prim(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.015f, NBMat.Glow(Color.white)).transform;
            p.m_Dot.SetParent(g.transform, true);
            return p;
        }

        void LateUpdate()
        {
            var rig = NBRig.Instance;
            bool active = rig != null && NBMenu.Instance != null && NBMenu.Instance.Visible;
            if (!active)
            {
                m_Line.enabled = false; m_Dot.gameObject.SetActive(false);
                SetHover(null);
                return;
            }

            Ray ray;
            if (NBInput.PointerFromMouse)
            {
                if (!NBInput.TryMouseRay(rig.Cam, out ray)) return;
            }
            else
            {
                // whichever hand pulled its trigger last owns the laser
                if (NBInput.Left.triggerDown) m_UseLeft = true;
                if (NBInput.Right.triggerDown) m_UseLeft = false;
                var h = m_UseLeft ? NBInput.Left : NBInput.Right;
                var origin = rig.Space.TransformPoint(h.aimPos);
                var dir = rig.Space.rotation * h.aimRot * Vector3.forward;
                ray = new Ray(origin, dir);
            }

            NBButton hit = null;
            float len = 4f;
            var hits = Physics.RaycastAll(ray, 6f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            foreach (var hh in hits)
            {
                var b = hh.collider.GetComponent<NBButton>();
                if (b != null && b.gameObject.activeInHierarchy && hh.distance < best) { best = hh.distance; hit = b; len = hh.distance; }
            }
            SetHover(hit);

            bool showLaser = !NBInput.PointerFromMouse;
            m_Line.enabled = showLaser;
            m_Dot.gameObject.SetActive(showLaser && hit != null);
            if (showLaser)
            {
                m_Line.SetPosition(0, ray.origin);
                m_Line.SetPosition(1, ray.origin + ray.direction * len);
                m_Dot.position = ray.origin + ray.direction * len;
            }

            if (NBInput.UISelect && hit != null)
            {
                hit.Click();
                NBInput.Haptic(!m_UseLeft, 0.3f, 0.03f);
            }
        }

        void SetHover(NBButton b)
        {
            if (m_Hover == b) return;
            if (m_Hover != null) m_Hover.SetHover(false);
            m_Hover = b;
            if (m_Hover != null) { m_Hover.SetHover(true); NBInput.Haptic(!m_UseLeft, 0.1f, 0.01f); }
        }
    }
}
