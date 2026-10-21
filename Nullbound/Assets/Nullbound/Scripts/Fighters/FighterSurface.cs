using System.Collections.Generic;
using Nullbound.Art;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Surface detail for fighter materials: a small tileable normal map and a light albedo breakup per kind of
    /// material (woven fabric, leather, brushed metal, skin, hair), generated once at runtime with ProcTex and shared
    /// by every fighter. Turns flat plastic-looking colours into cloth, leather and metal you can see up close in VR.
    /// </summary>
    public static class FighterSurface
    {
        enum Kind { None, Fabric, Leather, Brushed, Skin, Hair }

        const int Size = 128;
        static readonly Dictionary<Kind, (Texture2D albedo, Texture2D normal, Vector2 tiling)> sets = new Dictionary<Kind, (Texture2D, Texture2D, Vector2)>();
        static readonly Dictionary<(Material, Kind), Material> detailed = new Dictionary<(Material, Kind), Material>();

        static Kind KindOf(Slot slot, FighterStats s)
        {
            switch (slot)
            {
                case Slot.Main: case Slot.Secondary: case Slot.Cloth: case Slot.Dark: return Kind.Fabric;
                case Slot.Skin: return Kind.Skin;
                case Slot.Hair: return Kind.Hair;
                case Slot.Metal: case Slot.Gold: return Kind.Brushed;
                case Slot.Glove:
                    if (s.glove == GloveType.Glow) return Kind.None;
                    return s.glove == GloveType.Metal ? Kind.Brushed : Kind.Leather;
                default: return Kind.None; // eyes, glowing accents, ivory: smooth on purpose
            }
        }

        /// <summary>A copy of `baseMat` with the slot's surface detail (or `baseMat` itself if the slot has none).</summary>
        public static Material Apply(Material baseMat, Slot slot, FighterStats s)
        {
            var kind = KindOf(slot, s);
            if (kind == Kind.None || baseMat == null || !baseMat.HasProperty("_BumpMap")) return baseMat;
            if (detailed.TryGetValue((baseMat, kind), out var m) && m != null) return m;
            var set = Get(kind);
            m = new Material(baseMat) { name = baseMat.name + " " + kind };
            m.SetTexture("_MainTex", set.albedo);
            m.SetTextureScale("_MainTex", set.tiling);
            m.SetTexture("_BumpMap", set.normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            detailed[(baseMat, kind)] = m;
            return m;
        }

        static (Texture2D albedo, Texture2D normal, Vector2 tiling) Get(Kind kind)
        {
            if (sets.TryGetValue(kind, out var set) && set.albedo != null) return set;
            TexSet t;
            Vector2 tiling;
            switch (kind)
            {
                case Kind.Fabric: t = ProcTex.Build("Fabric", Size, 1f, 3f, Fabric); tiling = new Vector2(4f, 4f); break;
                case Kind.Leather: t = ProcTex.Build("Leather", Size, 1f, 4f, Leather); tiling = new Vector2(2f, 2f); break;
                case Kind.Brushed: t = ProcTex.Build("Brushed", Size, 1f, 1.5f, Brushed); tiling = new Vector2(2f, 2f); break;
                case Kind.Skin: t = ProcTex.Build("Skin", Size, 1f, 1.2f, Skin); tiling = new Vector2(2f, 2f); break;
                default: t = ProcTex.Build("Hair", Size, 1f, 4f, Hair); tiling = new Vector2(3f, 1f); break;
            }
            set = (ToTexture(t.name + "Albedo", t.albedo, false), ToTexture(t.name + "Normal", t.normal, true), tiling);
            sets[kind] = set;
            return set;
        }

        static Texture2D ToTexture(string name, Color[] pixels, bool linear)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, linear)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2,
            };
            tex.SetPixels(pixels);
            tex.Apply(true, true); // no CPU copy needed after upload
            return tex;
        }

        // ------------------------------------------------------------------ recipes (albedo is a grey multiplier)

        static float Wave(float t) => Mathf.Sin(t * Mathf.PI * 2f);

        /// <summary>A fine twill weave with soft wrinkles.</summary>
        static void Fabric(float u, float v, out Color albedo, out float height, out float metallic, out float smooth, out float ao)
        {
            float weave = Wave((u + v) * 32f) * 0.5f + Wave((u - v) * 32f) * 0.15f;
            float wrinkle = ProcTex.Fbm(u, v, 4, 3, 11);
            height = weave * 0.12f + wrinkle * 0.9f;
            float g = 0.86f + wrinkle * 0.1f + weave * 0.03f;
            albedo = new Color(g, g, g);
            metallic = 0f; smooth = 0.5f; ao = 1f;
        }

        /// <summary>Pebbled grain with a few creases.</summary>
        static void Leather(float u, float v, out Color albedo, out float height, out float metallic, out float smooth, out float ao)
        {
            float cell = ProcTex.Worley(u, v, 22, 5, out float edge, out _);
            float crease = ProcTex.Fbm(u, v, 3, 3, 9);
            height = Mathf.Clamp01(edge * 4f) * 0.25f + crease * 0.6f;
            float g = 0.84f + cell * 0.08f + crease * 0.1f;
            albedo = new Color(g, g, g);
            metallic = 0f; smooth = 0.5f; ao = 1f;
        }

        /// <summary>Fine brushing along one direction with a few scratches.</summary>
        static void Brushed(float u, float v, out Color albedo, out float height, out float metallic, out float smooth, out float ao)
        {
            float wobble = ProcTex.Value(u, v, 4, 3) * 0.6f;
            float brush = Wave(v * 96f + wobble) * 0.3f + Wave(v * 41f + wobble * 2f) * 0.2f;
            float scratch = ProcTex.Value(u, v, 64, 21) > 0.97f ? 0.4f : 0f;
            height = brush * 0.2f + scratch * 0.3f;
            float g = 0.9f + brush * 0.05f - scratch * 0.1f;
            albedo = new Color(g, g, g);
            metallic = 0f; smooth = 0.5f; ao = 1f;
        }

        /// <summary>Very soft pores and blotches (keeps skin from looking like plastic).</summary>
        static void Skin(float u, float v, out Color albedo, out float height, out float metallic, out float smooth, out float ao)
        {
            float pores = ProcTex.Value(u, v, 48, 7);
            float blotch = ProcTex.Fbm(u, v, 4, 3, 13);
            height = pores * 0.15f + blotch * 0.3f;
            float g = 0.93f + blotch * 0.07f;
            albedo = new Color(g, g * 0.985f, g * 0.975f);
            metallic = 0f; smooth = 0.5f; ao = 1f;
        }

        /// <summary>Strands running along v.</summary>
        static void Hair(float u, float v, out Color albedo, out float height, out float metallic, out float smooth, out float ao)
        {
            float flow = ProcTex.Perlin(u, v, 3, 17) * 1.5f;
            float strands = Wave(u * 64f + flow) * 0.5f + Wave(u * 23f + flow * 0.5f) * 0.3f;
            height = strands * 0.3f;
            float g = 0.8f + strands * 0.12f + ProcTex.Value(u, v, 16, 4) * 0.08f;
            albedo = new Color(g, g, g);
            metallic = 0f; smooth = 0.5f; ao = 1f;
        }
    }
}
