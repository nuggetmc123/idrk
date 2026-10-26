using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The world-space UI kit every menu is built from: panels with rounded corners, labels, buttons, toggles, sliders,
    /// option selectors, images. All in metres, so the same menus work in VR (laser pointer + trigger) and on a monitor
    /// (mouse). Visual language: dark graphite panels, a thin signal-orange accent, condensed type.
    /// </summary>
    public static class VUI
    {
        public static readonly Color Panel = new Color(0.07f, 0.075f, 0.085f, 0.94f);
        public static readonly Color PanelLight = new Color(0.13f, 0.14f, 0.155f, 0.96f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Accent = new Color(1f, 0.32f, 0.16f, 1f);
        public static readonly Color AccentDim = new Color(1f, 0.32f, 0.16f, 0.22f);
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.94f);
        public static readonly Color TextDim = new Color(0.6f, 0.64f, 0.68f);
        public static readonly Color Good = new Color(0.3f, 0.85f, 0.45f);
        public static readonly Color Purple = new Color(0.72f, 0.4f, 1f);
        public static readonly Color Warn = new Color(1f, 0.8f, 0.2f);

        static TMP_FontAsset display, body;
        static Material panelMat;
        static readonly Dictionary<string, Mesh> rects = new Dictionary<string, Mesh>();

        public static TMP_FontAsset Display
        {
            get
            {
                if (display == null && VXAssets.I != null && VXAssets.I.displayText != null) display = VXAssets.I.displayText;
                if (display == null)
                {
                    var f = VXAssets.I != null ? VXAssets.I.displayFont : null;
                    if (f == null) f = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold");
                    display = f != null ? TMP_FontAsset.CreateFontAsset(f) : TMP_Settings.defaultFontAsset;
                }
                return display;
            }
        }

        public static TMP_FontAsset Body
        {
            get
            {
                if (body == null && VXAssets.I != null && VXAssets.I.bodyText != null) body = VXAssets.I.bodyText;
                if (body == null)
                {
                    var f = VXAssets.I != null ? VXAssets.I.bodyFont : null;
                    if (f == null) f = Resources.Load<Font>("Fonts/BarlowCondensed-Medium");
                    body = f != null ? TMP_FontAsset.CreateFontAsset(f) : Display;
                }
                return body;
            }
        }

        /// Forget cached fonts, panel material and shapes (the editor calls this before building things to save).
        public static void ResetCache()
        {
            display = null; body = null; panelMat = null;
            rects.Clear();
        }

        public static Material PanelMaterial
        {
            get
            {
                if (panelMat == null)
                {
                    var src = VXAssets.I != null ? VXAssets.I.uiPanel : null;
                    panelMat = src != null ? new Material(src) : new Material(Shader.Find("Veylix/UI") ?? Shader.Find("Sprites/Default"));
                    panelMat.renderQueue = 2995;
                    // Panels write depth, so text and signs behind a menu (the garage walls) can't show through it.
                    if (panelMat.HasProperty("_ZWrite")) panelMat.SetFloat("_ZWrite", 1f);
                }
                return panelMat;
            }
        }

        /// <summary>Rounded rectangle, centred, in metres. Vertex colours carry the fill colour.</summary>
        public static Mesh RoundRect(float w, float h, float r)
        {
            string key = w.ToString("F3") + h.ToString("F3") + r.ToString("F3");
            if (rects.TryGetValue(key, out var m) && m != null) return m;
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            int seg = 5;
            Vector2[] centres = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r), new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                    Vector2 p = centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    verts.Add(new Vector3(p.x, p.y, 0f));
                    uvs.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f));
                }
            var tris = new List<int>();
            int n = verts.Count - 1;
            for (int i = 1; i <= n; i++) { tris.Add(0); tris.Add(i % n + 1); tris.Add(i); }
            m = new Mesh { name = "rrect" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            var nrm = new Vector3[verts.Count];
            for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.back;
            m.normals = nrm;
            var cols = new Color32[verts.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            m.colors32 = cols;
            m.RecalculateBounds();
            rects[key] = m;
            return m;
        }

        public static MeshRenderer Quad(Transform parent, string name, Vector2 pos, Vector2 size, Color c, float z = 0f, float radius = 0.008f, Texture tex = null)
        {
            var go = new GameObject(name);
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, z);
            go.AddComponent<MeshFilter>().sharedMesh = RoundRect(size.x, size.y, radius);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = PanelMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, c, tex);
            return r;
        }

        static MaterialPropertyBlock mpb;
        public static void SetColor(Renderer r, Color c, Texture tex = null)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            if (tex != null) mpb.SetTexture("_MainTex", tex);
            r.SetPropertyBlock(mpb);
        }

        public static TextMeshPro Label(Transform parent, string text, Vector2 pos, float size, Color c, TextAlignmentOptions align = TextAlignmentOptions.Left, bool display = false, float width = 1f, float z = -0.002f)
        {
            var go = new GameObject("T");
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, z);
            var t = go.AddComponent<TextMeshPro>();
            t.font = display ? Display : Body;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.enableWordWrapping = width > 0f;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(width > 0f ? width : 4f, size * 0.02f);
                rt.pivot = new Vector2(align == TextAlignmentOptions.Right || align == TextAlignmentOptions.MidlineRight ? 1f : align == TextAlignmentOptions.Center || align == TextAlignmentOptions.Midline ? 0.5f : 0f, 0.5f);
            }
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
            return t;
        }

        /// <summary>
        /// Keep a label inside a box: one line (or 'lines'), shrinking the font (down to 45%) instead of wrapping onto
        /// the next row or running past the edge.
        /// </summary>
        public static TextMeshPro Fit(TextMeshPro t, float width, float height, int lines = 1)
        {
            if (t == null) return t;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = t.fontSize * 0.45f;
            t.enableWordWrapping = lines > 1;
            t.overflowMode = lines > 1 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            var rt = t.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(width, height);
            return t;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    /// <summary>Anything the pointer can interact with.</summary>
    public abstract class VElement : MonoBehaviour
    {
        public bool interactable = true;
        public bool Hovered { get; private set; }
        public virtual void Enter() { Hovered = true; Refresh(); }
        public virtual void Exit() { Hovered = false; Refresh(); }
        public virtual void Press(Vector3 localHit) { }
        public virtual void Drag(Vector3 localHit) { }
        public virtual void Release() { }
        public virtual void Refresh() { }
        protected BoxCollider AddCollider(Vector2 size)
        {
            var bc = Obj.Need<BoxCollider>(gameObject);
            bc.size = new Vector3(size.x, size.y, 0.01f);
            bc.isTrigger = true;
            return bc;
        }
    }

    public class VButton : VElement
    {
        public Action onClick;
        public bool selected;
        public bool primary;
        MeshRenderer bg, edge;
        TextMeshPro label, sub;
        Vector2 size;

        public static VButton Make(Transform parent, string text, Vector2 pos, Vector2 size, Action onClick, bool primary = false, string subtitle = null, float fontSize = 0f)
        {
            var go = new GameObject("Btn " + text);
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.003f);
            var b = go.AddComponent<VButton>();
            b.size = size;
            b.onClick = onClick;
            b.primary = primary;
            b.bg = VUI.Quad(go.transform, "bg", Vector2.zero, size, VUI.PanelLight, 0f, 0.006f);
            b.edge = VUI.Quad(go.transform, "edge", new Vector2(-size.x * 0.5f + 0.002f, 0f), new Vector2(0.004f, size.y * 0.7f), VUI.Accent, -0.001f, 0.001f);
            float textW = size.x - 0.034f, x0 = -size.x * 0.5f + 0.016f;
            if (subtitle == null)
            {
                float fs = fontSize > 0f ? fontSize : Mathf.Clamp(size.y * 6.2f, 0.17f, 0.46f);
                b.label = VUI.Fit(VUI.Label(go.transform, text, new Vector2(x0, 0f), fs, VUI.Text, TextAlignmentOptions.MidlineLeft, true, textW), textW, size.y * 0.8f);
            }
            else
            {
                // Title on the top half, description below it, each kept inside its own band.
                float fs = fontSize > 0f ? fontSize : Mathf.Clamp(size.y * 3.9f, 0.17f, 0.4f);
                b.label = VUI.Fit(VUI.Label(go.transform, text, new Vector2(x0, size.y * 0.2f), fs, VUI.Text, TextAlignmentOptions.MidlineLeft, true, textW), textW, size.y * 0.42f);
                b.sub = VUI.Fit(VUI.Label(go.transform, subtitle, new Vector2(x0, -size.y * 0.2f), Mathf.Min(0.2f, fs * 0.55f), VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, textW), textW, size.y * 0.42f, 2);
            }
            b.AddCollider(size);
            b.Refresh();
            return b;
        }

        public void SetText(string t) { if (label != null) label.text = t; }

        public override void Refresh()
        {
            if (bg == null) return;
            Color c = !interactable ? new Color(0.1f, 0.1f, 0.11f, 0.8f) : selected ? new Color(0.24f, 0.13f, 0.1f, 0.97f) : Hovered ? new Color(0.2f, 0.21f, 0.23f, 0.98f) : primary ? new Color(0.62f, 0.18f, 0.08f, 0.97f) : VUI.PanelLight;
            if (primary && Hovered && interactable) c = new Color(0.82f, 0.25f, 0.1f, 1f);
            VUI.SetColor(bg, c);
            edge.enabled = selected || (Hovered && interactable) || primary;
            VUI.SetColor(edge, primary ? new Color(1f, 0.6f, 0.4f) : VUI.Accent);
            if (label != null) label.color = interactable ? VUI.Text : VUI.TextDim;
            transform.localScale = Hovered && interactable ? new Vector3(1.01f, 1.01f, 1f) : Vector3.one;
        }

        public override void Press(Vector3 localHit)
        {
            if (!interactable) return;
            CrashAudio.Click();
            onClick?.Invoke();
        }
    }

    public class VSlider : VElement
    {
        public float value, min, max = 1f, step;
        public Action<float> onChange;
        public Func<float, string> format;
        MeshRenderer fill, knob;
        TextMeshPro valueText;
        float width;

        public static VSlider Make(Transform parent, Vector2 pos, float width, float min, float max, float value, Action<float> onChange, Func<float, string> format = null, float step = 0f)
        {
            var go = new GameObject("Slider");
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.003f);
            var s = go.AddComponent<VSlider>();
            s.min = min; s.max = max; s.value = value; s.onChange = onChange; s.format = format; s.width = width; s.step = step;
            VUI.Quad(go.transform, "track", Vector2.zero, new Vector2(width, 0.006f), new Color(1f, 1f, 1f, 0.12f), 0f, 0.003f);
            s.fill = VUI.Quad(go.transform, "fill", Vector2.zero, new Vector2(width, 0.006f), VUI.Accent, -0.001f, 0.003f);
            s.knob = VUI.Quad(go.transform, "knob", Vector2.zero, new Vector2(0.018f, 0.018f), VUI.Text, -0.002f, 0.009f);
            s.valueText = VUI.Fit(VUI.Label(go.transform, "", new Vector2(width * 0.5f + 0.06f, 0f), 0.22f, VUI.Text, TextAlignmentOptions.MidlineRight, true, 0.1f), 0.1f, 0.04f);
            s.AddCollider(new Vector2(width + 0.03f, 0.04f));
            s.Refresh();
            return s;
        }

        public override void Refresh()
        {
            if (fill == null) return;
            float t = Mathf.InverseLerp(min, max, value);
            fill.transform.localScale = new Vector3(Mathf.Max(0.001f, t), 1f, 1f);
            fill.transform.localPosition = new Vector3(-width * 0.5f * (1f - t), 0f, -0.001f);
            knob.transform.localPosition = new Vector3(Mathf.Lerp(-width * 0.5f, width * 0.5f, t), 0f, -0.002f);
            knob.transform.localScale = Hovered ? Vector3.one * 1.3f : Vector3.one;
            valueText.text = format != null ? format(value) : value.ToString("0.00");
        }

        public override void Press(Vector3 localHit) => Drag(localHit);

        public override void Drag(Vector3 localHit)
        {
            float t = Mathf.Clamp01((localHit.x + width * 0.5f) / width);
            float v = Mathf.Lerp(min, max, t);
            if (step > 0f) v = Mathf.Round(v / step) * step;
            if (Mathf.Abs(v - value) < 1e-5f) return;
            value = v;
            onChange?.Invoke(value);
            Refresh();
            CrashAudio.Tick();
        }
    }

    /// <summary>"&lt;  value  &gt;" picker for a list of options.</summary>
    public class VSelector : VElement
    {
        public string[] options;
        public int index;
        public Action<int> onChange;
        TextMeshPro text;
        MeshRenderer bg;
        float width;

        public static VSelector Make(Transform parent, Vector2 pos, float width, string[] options, int index, Action<int> onChange)
        {
            var go = new GameObject("Selector");
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.003f);
            var s = go.AddComponent<VSelector>();
            s.options = options; s.index = Mathf.Clamp(index, 0, options.Length - 1); s.onChange = onChange; s.width = width;
            s.bg = VUI.Quad(go.transform, "bg", Vector2.zero, new Vector2(width, 0.04f), new Color(1f, 1f, 1f, 0.05f), 0f, 0.005f);
            VUI.Label(go.transform, "<", new Vector2(-width * 0.5f + 0.015f, 0f), 0.3f, VUI.Accent, TextAlignmentOptions.MidlineLeft, true, 0.05f);
            VUI.Label(go.transform, ">", new Vector2(width * 0.5f - 0.015f, 0f), 0.3f, VUI.Accent, TextAlignmentOptions.MidlineRight, true, 0.05f);
            s.text = VUI.Fit(VUI.Label(go.transform, "", Vector2.zero, 0.24f, VUI.Text, TextAlignmentOptions.Midline, true, width - 0.08f), width - 0.08f, 0.04f);
            s.AddCollider(new Vector2(width, 0.045f));
            s.Refresh();
            return s;
        }

        public override void Refresh()
        {
            if (text == null) return;
            text.text = options.Length > 0 ? options[index] : "";
            VUI.SetColor(bg, Hovered ? new Color(1f, 1f, 1f, 0.11f) : new Color(1f, 1f, 1f, 0.05f));
        }

        public override void Press(Vector3 localHit)
        {
            int dir = localHit.x < 0f ? -1 : 1;
            index = (index + dir + options.Length) % options.Length;
            CrashAudio.Click();
            onChange?.Invoke(index);
            Refresh();
        }
    }

    public class VToggle : VElement
    {
        public bool value;
        public Action<bool> onChange;
        MeshRenderer box, dot;

        public static VToggle Make(Transform parent, Vector2 pos, bool value, Action<bool> onChange)
        {
            var go = new GameObject("Toggle");
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.003f);
            var t = go.AddComponent<VToggle>();
            t.value = value; t.onChange = onChange;
            t.box = VUI.Quad(go.transform, "box", Vector2.zero, new Vector2(0.06f, 0.028f), new Color(1f, 1f, 1f, 0.12f), 0f, 0.014f);
            t.dot = VUI.Quad(go.transform, "dot", Vector2.zero, new Vector2(0.022f, 0.022f), VUI.Text, -0.001f, 0.011f);
            t.AddCollider(new Vector2(0.07f, 0.04f));
            t.Refresh();
            return t;
        }

        public override void Refresh()
        {
            if (box == null) return;
            VUI.SetColor(box, value ? VUI.Accent : new Color(1f, 1f, 1f, Hovered ? 0.2f : 0.12f));
            dot.transform.localPosition = new Vector3(value ? 0.016f : -0.016f, 0f, -0.001f);
        }

        public override void Press(Vector3 localHit)
        {
            value = !value;
            CrashAudio.Click();
            onChange?.Invoke(value);
            Refresh();
        }
    }

    /// <summary>A clickable swatch or card (colour chips, map cards, car cards).</summary>
    public class VCard : VElement
    {
        public Action onClick;
        public bool selected;
        MeshRenderer frame;
        Vector2 size;

        public static VCard Make(Transform parent, Vector2 pos, Vector2 size, Color fill, Texture tex, Action onClick)
        {
            var go = new GameObject("Card");
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.003f);
            var c = go.AddComponent<VCard>();
            c.size = size;
            c.onClick = onClick;
            c.frame = VUI.Quad(go.transform, "frame", Vector2.zero, size + new Vector2(0.008f, 0.008f), VUI.Accent, 0.0005f, 0.008f);
            VUI.Quad(go.transform, "fill", Vector2.zero, size, fill, 0f, 0.006f, tex);
            c.AddCollider(size);
            c.Refresh();
            return c;
        }

        public override void Refresh()
        {
            if (frame == null) return;
            frame.enabled = selected || Hovered;
            VUI.SetColor(frame, selected ? VUI.Accent : new Color(1f, 1f, 1f, 0.5f));
        }

        public override void Press(Vector3 localHit) { CrashAudio.Click(); onClick?.Invoke(); }
    }
}
