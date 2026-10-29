using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The edge of the map: tall walls nobody can pass, drawn as a glowing
    /// hex-grid energy field that fades in as you get close.
    /// </summary>
    public class BarrierField : MonoBehaviour
    {
        Material mat;
        Vector2 half;
        static Texture2D hexTex;

        public static BarrierField Create(Transform parent, Vector2 half, float height = 60f)
        {
            var go = new GameObject("Barrier");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BarrierField>();
            b.half = half;
            b.mat = new Material(GameAssets.I.particleAdd) { name = "fz_barrier", mainTexture = HexTexture() };
            b.SetAlpha(0f);
            float visH = 8f;
            void Wall(Vector3 center, Vector3 size, float yaw, float length)
            {
                // solid, invisible collider (tall, so nobody jumps over)
                var col = new GameObject("barrierWall") { layer = Layers.IgnoreRaycast };   // bullets pass, bodies don't
                col.transform.SetParent(go.transform, false);
                col.transform.localPosition = center + Vector3.up * (height * 0.5f - visH * 0.5f);
                col.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                col.AddComponent<BoxCollider>().size = new Vector3(size.x, height, size.z);
                // the glowing field
                var q = Prims.Make(PrimitiveType.Quad, go.transform, center, new Vector3(length, visH, 1), b.mat, false, Quaternion.Euler(0, yaw, 0), "barrierField");
                q.GetComponent<MeshFilter>().sharedMesh = TiledQuad(length / 4f, visH / 4f);
            }
            float y = visH * 0.5f - 0.5f;
            Wall(new Vector3(0, y, half.y), new Vector3(half.x * 2 + 2, 0, 1), 0, half.x * 2);
            Wall(new Vector3(0, y, -half.y), new Vector3(half.x * 2 + 2, 0, 1), 180, half.x * 2);
            Wall(new Vector3(half.x, y, 0), new Vector3(half.y * 2 + 2, 0, 1), 90, half.y * 2);
            Wall(new Vector3(-half.x, y, 0), new Vector3(half.y * 2 + 2, 0, 1), -90, half.y * 2);
            return b;
        }

        void SetAlpha(float a)
        {
            var c = new Color(0.25f, 0.75f, 1f, a);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
            mat.color = c;
        }

        void Update()
        {
            var rig = PlayerRig.I;
            if (rig == null) return;
            var p = rig.transform.position;
            float edge = Mathf.Min(half.x - Mathf.Abs(p.x), half.y - Mathf.Abs(p.z));
            float a = Mathf.Clamp01(1f - edge / 7f) * 0.55f + 0.04f + Mathf.Sin(Time.time * 2f) * 0.01f;
            SetAlpha(a);
            mat.mainTextureOffset = new Vector2(0, Time.time * 0.05f);
        }

        static Mesh TiledQuad(float u, float v)
        {
            var m = new Mesh { name = "fz_barrierQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(u, 0), new Vector2(0, v), new Vector2(u, v) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };   // both sides
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Texture2D HexTexture()
        {
            if (hexTex != null) return hexTex;
            const int n = 128;
            hexTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "fz_hex", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // distance to the nearest hexagon edge
                    float u = x / (float)n * 4f, v = y / (float)n * 4f * 0.866f * 2f / 1.732f;
                    var a = new Vector2(u, v * 1.1547f);
                    Vector2 r = new Vector2(1f, 1.732f), h = r * 0.5f;
                    Vector2 m1 = new Vector2(Mod(a.x, r.x) - h.x, Mod(a.y, r.y) - h.y);
                    Vector2 m2 = new Vector2(Mod(a.x - h.x, r.x) - h.x, Mod(a.y - h.y, r.y) - h.y);
                    var g = m1.sqrMagnitude < m2.sqrMagnitude ? m1 : m2;
                    g = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
                    float d = 0.5f - Mathf.Max(Vector2.Dot(g, new Vector2(0.5f, 0.866f)), g.x);
                    float line = Mathf.Clamp01(1f - d / 0.05f);
                    float glow = Mathf.Clamp01(1f - d / 0.25f) * 0.25f;
                    float c = Mathf.Max(line, glow) + 0.05f;
                    px[y * n + x] = new Color(c, c, c, c);
                }
            hexTex.SetPixels(px);
            hexTex.Apply(true, true);
            return hexTex;
        }

        static float Mod(float a, float b) => a - b * Mathf.Floor(a / b);
    }
}
