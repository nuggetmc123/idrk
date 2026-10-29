using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>Builds the game's models out of primitives and shared materials.</summary>
    public static class Prims
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Texture2D softDot;

        /// <summary>A lit, textured material. Surface.Auto picks a texture from how metallic it is.</summary>
        public static Material Lit(Color c, float smooth = 0.25f, float metal = 0f, Surface surface = Surface.Auto)
        {
            if (surface == Surface.Auto) surface = metal >= 0.5f ? Surface.Metal : metal >= 0.2f ? Surface.PaintedMetal : Surface.Generic;
            string key = "L" + c + smooth + metal + surface;
            if (!cache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(GameAssets.I.litNormal) { name = "fz_" + surface };
                var tex = Surfaces.Get(surface);
                m.mainTexture = tex.albedo;
                m.mainTextureScale = Vector2.one / Surfaces.TileMeters(surface);
                if (m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", tex.normal);
                    m.SetTextureScale("_BumpMap", m.mainTextureScale);
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
                // the detail texture averages ~0.85, so lift the colour to keep it true
                m.color = new Color(Mathf.Min(1, c.r * 1.15f), Mathf.Min(1, c.g * 1.15f), Mathf.Min(1, c.b * 1.15f), c.a);
                m.SetFloat("_Glossiness", smooth);
                m.SetFloat("_Metallic", metal);
                m.enableInstancing = true;
                cache[key] = m;
            }
            return m;
        }

        public static Material Emissive(Color c, float intensity = 1.5f)
        {
            string key = "E" + c + intensity;
            if (!cache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(GameAssets.I.litEmissive) { name = "fz_glow" };
                m.color = c;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.enableInstancing = true;
                cache[key] = m;
            }
            return m;
        }

        /// <summary>See-through lit material; not shared, so its alpha can be animated.</summary>
        public static Material Fade(Color c)
        {
            var m = new Material(GameAssets.I.fade) { name = "fz_fade" };
            m.color = c;
            return m;
        }

        public static Material Unlit(Color c)
        {
            string key = "U" + c;
            if (!cache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(GameAssets.I.unlit) { name = "fz_unlit" };
                m.color = c;
                cache[key] = m;
            }
            return m;
        }

        /// <summary>Unlit, alpha-blended, double-sided (UI panels, overlays). Not shared.</summary>
        public static Material Sprite(Color c)
        {
            var m = new Material(GameAssets.I.sprite) { name = "fz_sprite" };
            m.color = c;
            return m;
        }

        public static Material Particle(bool additive, Color tint)
        {
            string key = (additive ? "PA" : "PB") + tint;
            if (!cache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(additive ? GameAssets.I.particleAdd : GameAssets.I.particleAlpha) { name = "fz_particle" };
                m.mainTexture = SoftDot;
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
                m.color = tint;
                cache[key] = m;
            }
            return m;
        }

        public static Texture2D SoftDot
        {
            get
            {
                if (softDot == null)
                {
                    const int n = 64;
                    softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "fz_dot" };
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                            float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                            a = a * a * (3 - 2 * a);
                            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                        }
                    softDot.SetPixels32(px);
                    softDot.Apply();
                }
                return softDot;
            }
        }

        public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = true, Quaternion? localRot = null, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            // boxes get UVs in meters, so textures are the same size on every box
            if (type == PrimitiveType.Cube) go.GetComponent<MeshFilter>().sharedMesh = Surfaces.Box(scale);
            if (!collider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = true, Quaternion? rot = null)
            => Make(PrimitiveType.Cube, parent, pos, size, mat, collider, rot);

        public static GameObject Sphere(Transform parent, Vector3 pos, float diameter, Material mat, bool collider = false)
            => Make(PrimitiveType.Sphere, parent, pos, Vector3.one * diameter, mat, collider);

        public static GameObject Ball(Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
            => Make(PrimitiveType.Sphere, parent, pos, scale, mat, collider);

        static Mesh lowBall;

        /// <summary>A cheap ball (60 vertices instead of 500) for things there are lots of, like sandbags.</summary>
        public static GameObject LowBall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            if (lowBall == null)
            {
                const int seg = 10, ring = 5;
                var v = new System.Collections.Generic.List<Vector3>();
                var uv = new System.Collections.Generic.List<Vector2>();
                var t = new System.Collections.Generic.List<int>();
                for (int r = 0; r <= ring; r++)
                    for (int s = 0; s <= seg; s++)
                    {
                        float lat = Mathf.PI * r / ring - Mathf.PI / 2, lon = 2 * Mathf.PI * s / seg;
                        v.Add(new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * 0.5f);
                        uv.Add(new Vector2((float)s / seg, (float)r / ring));
                    }
                for (int r = 0; r < ring; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a = r * (seg + 1) + s, b = a + seg + 1;
                        t.Add(a); t.Add(b); t.Add(a + 1);
                        t.Add(a + 1); t.Add(b); t.Add(b + 1);
                    }
                lowBall = new Mesh { name = "fz_lowball" };
                lowBall.SetVertices(v);
                lowBall.SetUVs(0, uv);
                lowBall.SetTriangles(t, 0);
                lowBall.RecalculateNormals();
                lowBall.RecalculateTangents();
                lowBall.RecalculateBounds();
            }
            var go = Make(PrimitiveType.Sphere, parent, pos, scale, mat, false);
            go.GetComponent<MeshFilter>().sharedMesh = lowBall;
            return go;
        }

        /// <summary>Cylinder of a given diameter and full length, lying along local axis.</summary>
        public static GameObject Cyl(Transform parent, Vector3 pos, float diameter, float length, Material mat, Vector3 axis, bool collider = false)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            var go = Make(PrimitiveType.Cylinder, parent, pos, new Vector3(diameter, length * 0.5f, diameter), mat, false, rot);
            if (collider)
            {
                // the cylinder primitive's own collider is a capsule, which turns
                // flat discs into giant spheres; use the real shape instead
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true;
            }
            return go;
        }

        public static GameObject Capsule(Transform parent, Vector3 pos, float diameter, float length, Material mat, Vector3 axis, bool collider = false)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            return Make(PrimitiveType.Capsule, parent, pos, new Vector3(diameter, length * 0.5f, diameter), mat, collider, rot);
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }
    }

    /// <summary>Rigidbody calls that were renamed in Unity 6.</summary>
    public static class Rb
    {
        public static void SetVelocity(Rigidbody r, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            r.linearVelocity = v;
#else
            r.velocity = v;
#endif
        }

        public static Vector3 GetVelocity(Rigidbody r)
        {
#if UNITY_6000_0_OR_NEWER
            return r.linearVelocity;
#else
            return r.velocity;
#endif
        }

        public static void SetDamping(Rigidbody r, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            r.linearDamping = linear;
            r.angularDamping = angular;
#else
            r.drag = linear;
            r.angularDrag = angular;
#endif
        }
    }

    /// <summary>Layers used by the game (all built in, so no Tag Manager edits).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int UI = 5;
        /// <summary>What bullets can hit: everything but UI and trigger volumes.</summary>
        public const int ShootMask = ~((1 << UI) | (1 << IgnoreRaycast));
        public const int UIMask = 1 << UI;
    }
}
