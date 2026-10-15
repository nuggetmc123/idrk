using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Small helpers for building world-space text and simple shapes from code.
    /// TextMeshPro is used (not the legacy TextMesh) because legacy text only
    /// renders in one eye on Quest's single-pass stereo rendering.
    /// </summary>
    public static class KartUI
    {
        /// <summary>
        /// Material that runtime-made shapes copy (set by SessionManager). Using a
        /// material asset from the scene guarantees its shader is included in builds.
        /// </summary>
        public static Material BaseMaterial;

        public static void Paint(Renderer r, Color color)
        {
            var m = BaseMaterial != null ? new Material(BaseMaterial) : r.material;
            SetColor(m, color);
            r.material = m;
        }

        public static void SetColor(Material m, Color color)
        {
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        }

        /// <param name="fontSize">Roughly: 10 = one metre tall text.</param>
        public static TextMeshPro Label(Transform parent, string text, Vector3 localPos, float fontSize,
            Color color, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            Paint(go.GetComponent<Renderer>(), color);
            return go;
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return m + ":" + s.ToString("00.0");
        }

        public static string FormatCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 6) return code;
            return code.Substring(0, 3) + " " + code.Substring(3);
        }
    }
}
