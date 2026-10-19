using UnityEngine;

namespace Nullbound
{
    public static class NBMat
    {
        static NBMaterialSet s_Set;
        static Font s_Font;

        static NBMaterialSet Set
        {
            get
            {
                if (s_Set == null) s_Set = Resources.Load<NBMaterialSet>("NBMaterials");
                if (s_Set == null)
                {
                    s_Set = ScriptableObject.CreateInstance<NBMaterialSet>();
                    s_Set.lit = new Material(Shader.Find("Standard"));
                    s_Set.glow = new Material(Shader.Find("Sprites/Default"));
                }
                return s_Set;
            }
        }

        static readonly System.Collections.Generic.Dictionary<(Color, float, float), Material> s_LitCache =
            new System.Collections.Generic.Dictionary<(Color, float, float), Material>();

        /// Shared per colour (so the GPU can batch). Use renderer.material if you need to tint one object.
        public static Material Lit(Color c, float smooth = 0.25f, float emission = 0f)
        {
            var key = (c, smooth, emission);
            if (s_LitCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = new Material(Set.lit) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            else if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            s_LitCache[key] = m;
            return m;
        }

        public static Material Glow(Color c) => new Material(Set.glow) { color = c };

        public static Font Font
        {
            get
            {
                if (s_Font != null) return s_Font;
                s_Font = Set.font;
                if (s_Font == null) s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return s_Font;
            }
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material m, bool keepCollider = false)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!keepCollider) Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        public static TextMesh Text(Transform parent, string s, Vector3 localPos, float size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var g = new GameObject("Text");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            var t = g.AddComponent<TextMesh>();
            t.font = Font;
            t.fontSize = 64;
            t.characterSize = size * 0.0156f;
            t.anchor = anchor;
            t.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            t.color = c;
            t.text = s;
            var mr = g.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            return t;
        }
    }
}
