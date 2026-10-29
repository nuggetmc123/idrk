using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// World-space VR UI made of quads, TextMesh labels and colliders on the
    /// built-in "UI" layer, clicked with a laser from either controller.
    /// No UGUI/TextMeshPro package needed.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.12f, 0.94f);
        public static readonly Color ButtonColor = new Color(0.18f, 0.2f, 0.28f, 1f);
        public static readonly Color ButtonHover = new Color(0.98f, 0.55f, 0.15f, 1f);
        public static readonly Color Accent = new Color(1f, 0.58f, 0.12f, 1f);
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.4f, 1f);
        public static readonly Color Bad = new Color(0.95f, 0.3f, 0.3f, 1f);
        public static readonly Color Muted = new Color(0.65f, 0.68f, 0.75f, 1f);

        const int FontPx = 48;
        static Material textMat;

        static Material TextMaterial
        {
            get
            {
                if (textMat == null)
                {
                    var f = GameAssets.I.font;
                    textMat = new Material(f.material) { name = "fz_text", renderQueue = 3100 };
                }
                return textMat;
            }
        }

        /// <summary>A text label; lineHeight in meters.</summary>
        public static TextMesh Text(Transform parent, string text, Vector3 localPos, float lineHeight, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = GameAssets.I.font;
            tm.fontSize = FontPx;
            tm.characterSize = lineHeight * 10f / FontPx;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left
                : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight ? TextAlignment.Right : TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial;
            return tm;
        }

        /// <summary>Flat rectangle facing -Z (towards the viewer of a panel).</summary>
        public static GameObject Quad(Transform parent, Vector3 localPos, Vector2 size, Color color, int queue = 2995)
        {
            var go = Prims.Make(PrimitiveType.Quad, parent, localPos, new Vector3(size.x, size.y, 1), null, false);
            var m = Prims.Sprite(color);
            m.renderQueue = queue;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Very simple word wrap for TextMesh (which has none).</summary>
        public static string Wrap(string text, float width, float lineHeight)
        {
            int maxChars = Mathf.Max(8, Mathf.FloorToInt(width / (lineHeight * 0.52f)));
            var sb = new StringBuilder();
            foreach (var para in (text ?? "").Split('\n'))
            {
                int col = 0;
                foreach (var word in para.Split(' '))
                {
                    if (col > 0 && col + word.Length + 1 > maxChars) { sb.Append('\n'); col = 0; }
                    else if (col > 0) { sb.Append(' '); col++; }
                    sb.Append(word);
                    col += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        public static string Clip(string s, int max) => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");
    }
}
