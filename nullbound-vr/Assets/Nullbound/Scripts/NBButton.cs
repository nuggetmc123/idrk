using System;
using UnityEngine;

namespace Nullbound
{
    /// A floating world-space button you point at with your controller laser and pull the trigger.
    public class NBButton : MonoBehaviour
    {
        public Action onClick;
        public TextMesh Label { get; private set; }
        Renderer m_Bg;
        Color m_Base, m_Hover, m_Selected;
        bool m_IsSelected, m_IsHover;
        public bool Interactable = true;

        public static readonly Color DefaultColor = new Color(0.2f, 0.18f, 0.3f, 0.95f);
        public static readonly Color AccentColor = new Color(0.5f, 0.25f, 0.95f, 0.95f);

        public static NBButton Create(Transform parent, string text, Vector3 localPos, Vector2 size, Action onClick, float textSize = 0.045f, Color? color = null)
        {
            var g = new GameObject("Btn_" + text);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            var b = g.AddComponent<NBButton>();
            b.onClick = onClick;
            b.m_Base = color ?? DefaultColor;
            b.m_Hover = Color.Lerp(b.m_Base, Color.white, 0.3f);
            b.m_Selected = AccentColor;
            var bg = NBMat.Prim(PrimitiveType.Quad, g.transform, Vector3.zero, new Vector3(size.x, size.y, 1), NBMat.Glow(b.m_Base));
            b.m_Bg = bg.GetComponent<Renderer>();
            b.Label = NBMat.Text(g.transform, text, new Vector3(0, 0, -0.003f), textSize, Color.white);
            var col = g.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 0.02f);
            col.isTrigger = true;
            return b;
        }

        public void SetText(string s) { if (Label != null) Label.text = s; }
        public void SetSelected(bool s) { m_IsSelected = s; Refresh(); }
        public void SetHover(bool h) { if (m_IsHover == h) return; m_IsHover = h; Refresh(); }

        void Refresh()
        {
            if (m_Bg == null) return;
            var c = m_IsSelected ? m_Selected : m_Base;
            if (m_IsHover && Interactable) c = Color.Lerp(c, Color.white, 0.3f);
            if (!Interactable) c = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a);
            m_Bg.material.color = c;
        }

        public void Click()
        {
            if (!Interactable) return;
            NBAudio.Play(NBAudio.Click, transform.position, 0.6f);
            onClick?.Invoke();
        }
    }
}
