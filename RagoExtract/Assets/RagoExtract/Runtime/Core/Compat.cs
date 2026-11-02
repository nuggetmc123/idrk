using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Small helpers that hide API renames between Unity versions so the project
    /// compiles cleanly (and without API-updater prompts) on Unity 2021.3 → Unity 6.
    /// </summary>
    public static class Compat
    {
        /// <summary>Unit-length rotation (identity if it's zero / NaN), safe for Rigidbody.MoveRotation.</summary>
        public static Quaternion SafeRot(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m < 1e-5f || float.IsNaN(m) || float.IsInfinity(m)) return Quaternion.identity;
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        public static Vector3 Vel(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(this Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(this Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        static Font _font;
        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                string[] names = { "LegacyRuntime.ttf", "Arial.ttf" };
                foreach (var n in names)
                {
                    try { _font = Resources.GetBuiltinResource<Font>(n); } catch { _font = null; }
                    if (_font != null) return _font;
                }
                try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Roboto", "Arial", "Helvetica", "DejaVu Sans" }, 64); } catch { }
                return _font;
            }
        }

        static Material _textMat;
        /// <summary>
        /// Material for 3D world text that is depth-tested (hidden behind walls). Unity's own font material draws
        /// TextMesh on top of everything, so signs and labels showed through walls.
        /// </summary>
        public static Material TextMaterial
        {
            get
            {
                var f = DefaultFont;
                if (f == null) return null;
                if (_textMat == null)
                {
                    var baseMat = Resources.Load<Material>("Rago/M_Text");
                    if (baseMat != null) _textMat = new Material(baseMat);
                    else { var sh = Shader.Find("Rago/Text3D"); if (sh != null) _textMat = new Material(sh); }
                    if (_textMat == null) return f.material; // shader missing: fall back to Unity's
                    _textMat.name = "RagoText";
                    _textMat.hideFlags = HideFlags.DontSave;
                    Font.textureRebuilt += font => { if (font == _font && _textMat != null) _textMat.mainTexture = font.material.mainTexture; };
                }
                _textMat.mainTexture = f.material.mainTexture;
                return _textMat;
            }
        }

        /// <summary>Destroy that also works in edit mode (used when the world is imported into the scene).</summary>
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static void SetLayerRecursive(this GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static Transform FindDeep(this Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static readonly List<Collider> _cols = new List<Collider>();
        public static void IgnoreAll(GameObject a, GameObject b, bool ignore = true)
        {
            if (a == null || b == null) return;
            var ca = a.GetComponentsInChildren<Collider>(true);
            var cb = b.GetComponentsInChildren<Collider>(true);
            foreach (var x in ca)
                foreach (var y in cb)
                    if (x != null && y != null && x != y && x.enabled && y.enabled) Physics.IgnoreCollision(x, y, ignore);
        }
    }

    /// <summary>Deterministic RNG so every client builds identical maps and loot from a seed.</summary>
    public class Rng
    {
        uint _s;
        public Rng(int seed) { _s = (uint)seed * 747796405u + 2891336453u; if (_s == 0) _s = 1; }
        public uint NextU() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public float Value => (NextU() & 0xFFFFFF) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Value;
        public int Range(int a, int bExclusive) => bExclusive <= a ? a : a + (int)(NextU() % (uint)(bExclusive - a));
        public bool Chance(float p) => Value < p;
        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];
        public Vector2 InCircle() { float a = Value * Mathf.PI * 2f, r = Mathf.Sqrt(Value); return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
        public Vector3 InSphere() { for (int i = 0; i < 12; i++) { var p = new Vector3(Value * 2 - 1, Value * 2 - 1, Value * 2 - 1); if (p.sqrMagnitude <= 1f) return p; } return Vector3.zero; }
        public Quaternion Yaw() => Quaternion.Euler(0, Value * 360f, 0);
    }

    public static class Layers
    {
        // Layers are created by the editor setup. If they're missing we fall back to Default (0)
        // and the code uses Physics.IgnoreCollision instead, so nothing breaks.
        public static int Player = -1, Hands = -1, Items = -1, Npc = -1, Ragdoll = -1, LocalBody = -1;
        public static void Init()
        {
            Player = Get("RagoPlayer");
            Hands = Get("RagoHands");
            Items = Get("RagoItems");
            Npc = Get("RagoNpc");
            Ragdoll = Get("RagoRagdoll");
            LocalBody = Get("RagoLocalBody");
            // our own hands / body never collide with our own character capsule
            Ignore(Hands, Player); Ignore(LocalBody, Player); Ignore(LocalBody, Hands);
            if (Hands > 0) Ballistics.BulletMask = ~(1 << Hands); // bullets pass through your own hands
        }
        static void Ignore(int a, int b) { if (a > 0 && b > 0) Physics.IgnoreLayerCollision(a, b, true); }
        static int Get(string n) { int l = LayerMask.NameToLayer(n); return l < 0 ? 0 : l; }
    }

    public static class MathX
    {
        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Quaternion Damp(Quaternion a, Quaternion b, float lambda, float dt) => Quaternion.Slerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);
        public static float Hash(float x) { return Frac(Mathf.Sin(x * 127.1f) * 43758.5453f); }
        public static float Hash(int x, int y, int s) { return Frac(Mathf.Sin(x * 127.1f + y * 311.7f + s * 74.7f) * 43758.5453f); }
        public static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>Two-bone IK. Returns the mid joint position given root, target and bone lengths.</summary>
        public static Vector3 SolveKnee(Vector3 root, Vector3 target, float l1, float l2, Vector3 hint)
        {
            Vector3 d = target - root;
            float dist = Mathf.Clamp(d.magnitude, 0.001f, l1 + l2 - 0.001f);
            Vector3 dir = d.normalized;
            float a = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 bend = Vector3.ProjectOnPlane(hint, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return root + dir * a + bend.normalized * h;
        }

        // Value noise (deterministic)
        public static float Noise(float x, float y, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
        // Tileable noise for textures: u,v in [0,1), integer frequencies -> wraps seamlessly.
        public static float TNoise(float u, float v, int fx, int fy, int seed = 0)
        {
            float x = u * fx, y = v * fy;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float a1 = xf * xf * (3 - 2 * xf), b1 = yf * yf * (3 - 2 * yf);
            int x0 = ((xi % fx) + fx) % fx, x1 = (x0 + 1) % fx, y0 = ((yi % fy) + fy) % fy, y1 = (y0 + 1) % fy;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, a1), Mathf.Lerp(c, d, a1), b1);
        }
        public static float TFbm(float u, float v, int fx, int fy, int oct, int seed = 0)
        {
            float s = 0, amp = 0.5f; int mx = 1;
            for (int i = 0; i < oct; i++) { s += TNoise(u, v, fx * mx, fy * mx, seed + i * 17) * amp; mx *= 2; amp *= 0.5f; }
            return s;
        }
        public static float Fbm(float x, float y, int oct, int seed = 0)
        {
            float s = 0, amp = 0.5f, f = 1;
            for (int i = 0; i < oct; i++) { s += Noise(x * f, y * f, seed + i * 17) * amp; f *= 2.03f; amp *= 0.5f; }
            return s;
        }
    }
}
