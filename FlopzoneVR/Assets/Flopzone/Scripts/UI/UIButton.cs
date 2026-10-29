using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Flopzone
{
    /// <summary>A laser-clickable button.</summary>
    public class UIButton : MonoBehaviour
    {
        public Action onClick;
        public bool interactable = true;
        public Color baseColor = UIKit.ButtonColor;
        public Color hoverColor = UIKit.ButtonHover;
        public TextMesh label;
        Material bg;
        float hoverUntil;
        float pressAnim;

        public static UIButton Create(Transform parent, string text, Vector3 localPos, Vector2 size, Action onClick, float textHeight = 0f)
        {
            var go = new GameObject("button:" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.layer = Layers.UI;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 0.01f);
            col.isTrigger = true;
            var b = go.AddComponent<UIButton>();
            b.onClick = onClick;
            var q = UIKit.Quad(go.transform, Vector3.zero, size, UIKit.ButtonColor, 2996);
            q.layer = Layers.UI;
            b.bg = q.GetComponent<Renderer>().sharedMaterial;
            float th = textHeight > 0 ? textHeight : Mathf.Min(size.y * 0.5f, 0.028f);
            b.label = UIKit.Text(go.transform, text, new Vector3(0, 0, -0.002f), th, Color.white);
            return b;
        }

        public void SetColors(Color normal, Color hover)
        {
            baseColor = normal;
            hoverColor = hover;
        }

        public void Hover() => hoverUntil = Time.unscaledTime + 0.05f;

        public void Click()
        {
            if (!interactable) return;
            pressAnim = 1f;
            AudioManager.Ui("ui_click");
            try { onClick?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void Update()
        {
            if (bg == null) return;
            bool hovered = Time.unscaledTime < hoverUntil && interactable;
            var c = !interactable ? baseColor * 0.6f : hovered ? hoverColor : baseColor;
            c.a = 1f;
            pressAnim = Mathf.MoveTowards(pressAnim, 0f, Time.unscaledDeltaTime * 6f);
            bg.color = Color.Lerp(c, Color.white, pressAnim * 0.5f);
            transform.localScale = Vector3.one * (1f - pressAnim * 0.06f);
        }
    }
}
