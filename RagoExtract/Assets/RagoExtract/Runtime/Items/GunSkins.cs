using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Gun paint jobs from the bunker's paint booth. A skin repaints the gun's body (not its optic, attachments or mag)
    /// with a procedural pattern mapped tri-planar, so it wraps any gun shape. Saved on the gun (ItemData.skin) and sent
    /// with it, so other players see it too.
    /// </summary>
    public static class GunSkins
    {
        public class Skin { public string id, name; public Color spray; public System.Func<Material> mat; }
        // patterns modelled on real camouflage and real gun coatings, sized for a gun (a few centimetres per blob),
        // painted in the gun's own space so they stay put when it moves, with worn edges showing bare metal
        public static readonly List<Skin> All = new List<Skin>
        {
            new Skin { id = "", name = "Factory finish", spray = new Color(0.25f, 0.25f, 0.26f) },
            new Skin { id = "desert", name = "Flat Dark Earth", spray = new Color(0.6f, 0.5f, 0.36f), mat = () => Coat("desert", new Color(0.6f, 0.5f, 0.36f), 0.22f) },
            new Skin { id = "woodland", name = "M81 Woodland", spray = new Color(0.4f, 0.42f, 0.3f), mat = () => Paint("woodland", Woodland, 3f) },
            new Skin { id = "multi", name = "Multi-Terrain", spray = new Color(0.62f, 0.56f, 0.42f), mat = () => Paint("multi", MultiTerrain, 3.2f) },
            new Skin { id = "arctic", name = "Arctic Splinter", spray = new Color(0.9f, 0.92f, 0.94f), mat = () => Paint("arctic", ArcticSplinter, 2.6f) },
            new Skin { id = "urban", name = "Urban Digital", spray = new Color(0.5f, 0.52f, 0.55f), mat = () => Paint("urban", UrbanDigital, 3.5f, true) },
            new Skin { id = "tiger", name = "Tiger Stripe", spray = new Color(0.42f, 0.42f, 0.26f), mat = () => Paint("tiger", TigerStripe, 2.8f) },
            new Skin { id = "crimson", name = "Crimson Cerakote", spray = new Color(0.5f, 0.07f, 0.07f), mat = () => Coat("crimson", new Color(0.5f, 0.06f, 0.06f), 0.32f) },
            new Skin { id = "gold", name = "Gold Rush", spray = new Color(1f, 0.8f, 0.25f), mat = () => Obj(Mats.Lit("skin_gold", new Color(1f, 0.76f, 0.34f), 1f, 0.82f, Mats.Metal, null, 0.06f, 0.18f, true, 3f)) },
            new Skin { id = "carbon", name = "Carbon Fibre", spray = new Color(0.1f, 0.1f, 0.11f), mat = () => Paint("carbon", CarbonTwill, 14f, false, 0.25f, 0.9f, 0.08f, 0.05f) },
        };

        public static Skin Get(string id) { foreach (var s in All) if (s.id == (id ?? "")) return s; return All[0]; }

        class Original : MonoBehaviour { public readonly Dictionary<Renderer, Material[]> mats = new Dictionary<Renderer, Material[]>(); }

        /// <summary>Paint a gun (or put its factory finish back with "").</summary>
        public static void Apply(Gun g, string id)
        {
            if (g == null || g.rig == null || g.rig.root == null) return;
            var skin = Get(id);
            g.skin = skin.id;
            var orig = g.rig.root.GetComponent<Original>() ?? g.rig.root.AddComponent<Original>();
            foreach (var r in g.rig.root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponentInParent<Item>() != g) continue;        // the optic, attachments and mag keep their own look
                if (!orig.mats.ContainsKey(r)) orig.mats[r] = r.sharedMaterials;
                if (skin.mat == null) { r.sharedMaterials = orig.mats[r]; continue; }
                var m = skin.mat();
                var src = orig.mats[r]; var arr = new Material[src.Length];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = src[i] != null && (src[i].name.Contains("glass") || src[i].name.Contains("lens") || src[i].name.Contains("glow") || src[i].name.Contains("emit")) ? src[i] : m;   // sights / glowing bits stay
                r.sharedMaterials = arr;
            }
        }

        // ---------------------------------------------------------------- patterns
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static Texture2D Make(string key, int size, System.Func<int, int, Color> px)
        {
            if (_tex.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4, name = "skin_" + key };
            var c = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) c[y * size + x] = px(x, y);
            t.SetPixels(c); t.Apply(true, true);
            return _tex[key] = t;
        }

        static Material Obj(Material m) { m.SetFloat("_Triplanar", 2f); return m; }   // mapped in the gun's own space

        /// <summary>A solid factory coating (Cerakote): satin, a faint mottle, edges worn to bare metal.</summary>
        static Material Coat(string key, Color c, float smooth)
        {
            var tex = Make(key, 128, (x, y) => { float n = Fbm(x / 128f, y / 128f, 6f, 3.1f); return c * (0.94f + n * 0.12f); });
            return Obj(Mats.Lit("skin_" + key, Color.white, 0.05f, smooth, tex, null, 0.25f, 0.5f, true, 4f));
        }

        static Material Paint(string key, System.Func<float, float, Color> px, float scale, bool point = false, float metal = 0f, float smooth = 0.24f, float grime = 0.3f, float scratch = 0.45f)
        {
            const int n = 256;
            var tex = Make(key, n, (x, y) => px(x / (float)n, y / (float)n));
            if (point) tex.filterMode = FilterMode.Point;
            return Obj(Mats.Lit("skin_" + key, Color.white, metal, smooth, tex, null, grime, scratch, true, scale));
        }

        /// <summary>M81 woodland: light green ground, field-drab and brown shapes, thin black branches.</summary>
        static Color Woodland(float u, float v)
        {
            Color green = new Color(0.4f, 0.42f, 0.3f), drab = new Color(0.56f, 0.49f, 0.35f), brown = new Color(0.34f, 0.25f, 0.16f), black = new Color(0.07f, 0.07f, 0.06f);
            float wu = u + (Fbm(u, v, 3f, 5.5f) - 0.5f) * 0.12f, wv = v + (Fbm(u, v, 3f, 8.2f) - 0.5f) * 0.12f;   // organic warp
            float a = Fbm(wu, wv, 3f, 1.3f), b = Fbm(wu, wv, 3f, 4.7f), k = Fbm(wu, wv, 4f, 2.9f);
            Color c = green;
            if (b > 0.55f) c = drab;
            if (a > 0.56f) c = brown;
            if (Mathf.Abs(k - 0.5f) < 0.035f && Fbm(u, v, 3f, 7.1f) > 0.5f) c = black;   // branch strokes
            return c * (0.97f + Fbm(u, v, 24f, 0.4f) * 0.06f);   // a little print texture
        }

        /// <summary>Multi-terrain: a soft tan-to-olive ground, blended brown and green patches, small dark and cream twigs.</summary>
        static Color MultiTerrain(float u, float v)
        {
            Color tan = new Color(0.66f, 0.59f, 0.45f), olive = new Color(0.47f, 0.47f, 0.33f), brown = new Color(0.45f, 0.35f, 0.24f),
                  dark = new Color(0.27f, 0.21f, 0.15f), cream = new Color(0.82f, 0.78f, 0.66f), green = new Color(0.36f, 0.4f, 0.27f);
            float g = Fbm(u, v, 2f, 3.3f);
            Color c = Color.Lerp(tan, olive, Mathf.SmoothStep(0.4f, 0.6f, g));
            float p = Fbm(u, v, 4f, 6.6f), q = Fbm(u, v, 4f, 9.1f);
            c = Color.Lerp(c, brown, Mathf.SmoothStep(0.55f, 0.6f, p));
            c = Color.Lerp(c, green, Mathf.SmoothStep(0.57f, 0.62f, q) * 0.9f);
            // small dark and cream spots (the "twigs")
            float t1 = Fbm(u, v, 16f, 1.8f), t2 = Fbm(u, v, 16f, 4.4f);
            if (t1 > 0.72f && p > 0.45f) c = dark;
            else if (t2 > 0.74f) c = cream;
            return c;
        }

        /// <summary>Arctic splinter: white ground broken by sharp grey shards in two directions.</summary>
        static Color ArcticSplinter(float u, float v)
        {
            Color white = new Color(0.9f, 0.92f, 0.93f), light = new Color(0.72f, 0.75f, 0.78f), mid = new Color(0.5f, 0.53f, 0.57f), dark = new Color(0.3f, 0.32f, 0.35f);
            // splinter: flat-shaded angular patches - noise sampled on a coarse rotated grid, then hard thresholds
            float s1 = Shard(u, v, 0.35f, 4f, 1.1f), s2 = Shard(u, v, -0.95f, 5f, 3.7f);
            Color c = white;
            if (s1 > 0.55f) c = light;
            if (s2 > 0.6f) c = mid;
            if (s1 > 0.68f && s2 > 0.55f) c = dark;
            return c;
        }
        static float Shard(float u, float v, float ang, float freq, float seed)
        {
            // value noise stretched hard along one direction: long thin angular slivers
            // quantise along one direction: the patches get straight edges like hand-cut splinter camo
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            float stripe = Mathf.Floor((u * ca + v * sa) * 9f) / 9f;
            return Fbm(u + stripe * 0.37f, v - stripe * 0.21f, freq, seed);
        }

        /// <summary>Urban digital: square pixels of four greys in clusters.</summary>
        static Color UrbanDigital(float u, float v)
        {
            Color l = new Color(0.66f, 0.68f, 0.7f), m = new Color(0.47f, 0.49f, 0.52f), d = new Color(0.3f, 0.31f, 0.34f), k = new Color(0.12f, 0.12f, 0.13f);
            float cu = Mathf.Floor(u * 64f) / 64f, cv = Mathf.Floor(v * 64f) / 64f;   // 4-texel pixels
            float a = Fbm(cu, cv, 4f, 2.2f), b = Fbm(cu, cv, 4f, 5.9f), c2 = Fbm(cu, cv, 8f, 8.8f);
            if (c2 > 0.66f) return k;
            if (a > 0.57f) return d;
            if (b > 0.52f) return m;
            return l;
        }

        /// <summary>Tiger stripe: olive ground with horizontal brush strokes of brown and black.</summary>
        static Color TigerStripe(float u, float v)
        {
            Color olive = new Color(0.44f, 0.44f, 0.28f), green = new Color(0.33f, 0.36f, 0.22f), brown = new Color(0.32f, 0.23f, 0.13f), black = new Color(0.08f, 0.08f, 0.07f);
            // horizontal brush strokes: noise squashed vertically (v scaled up), wrapped
            float sv = v * 7f - Mathf.Floor(v * 7f);
            float a = Fbm(u, sv, 3f, 1.4f) * 0.7f + Fbm(u, v, 6f, 9.4f) * 0.3f, b = Fbm(u, sv, 3f, 6.2f) * 0.7f + Fbm(u, v, 6f, 2.6f) * 0.3f, g = Fbm(u, v, 2f, 3.3f);
            Color c = Color.Lerp(olive, green, Mathf.SmoothStep(0.4f, 0.6f, g));
            if (b > 0.6f) c = brown;
            if (a > 0.64f) c = black;
            return c;
        }

        /// <summary>Carbon fibre: a 2x2 twill weave with each tow catching the light differently.</summary>
        static Color CarbonTwill(float u, float v)
        {
            int cells = 16;
            float x = u * cells, y = v * cells;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            bool warp = ((ix + iy / 2 * 2 + (iy % 2)) / 2 + iy) % 2 == 0;   // 2x2 twill: steps diagonally
            float fx = x - ix, fy = y - iy;
            float across = warp ? fx : fy;   // which way this tow runs
            float shade = 0.07f + 0.08f * Mathf.Sin(across * Mathf.PI) + (warp ? 0.03f : 0f);
            return new Color(shade, shade, shade * 1.1f);
        }

        /// <summary>Tileable fractal noise in 0..1: periodic gradient noise (wraps exactly at u/v = 0..1, no stretching).</summary>
        static float Fbm(float u, float v, float freq, float seed)
        {
            float s = 0, amp = 1, tot = 0;
            int f = Mathf.Max(1, Mathf.RoundToInt(freq));
            int sd = (int)(seed * 1000f);
            for (int o = 0; o < 4; o++)
            {
                s += Grad(u * f, v * f, f, sd + o * 101) * amp; tot += amp;
                amp *= 0.5f; f *= 2;
            }
            return Mathf.Clamp01(0.5f + 0.5f * (s / tot) * 1.6f);
        }
        static float Grad(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float Dot(int ix, int iy, float dx, float dy)
            {
                int hx = ((ix % period) + period) % period, hy = ((iy % period) + period) % period;
                uint h = (uint)(hx * 374761393 + hy * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                float a = (h & 0xffff) / 65535f * Mathf.PI * 2f;
                return Mathf.Cos(a) * dx + Mathf.Sin(a) * dy;
            }
            float sx = fx * fx * fx * (fx * (fx * 6 - 15) + 10), sy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            float n00 = Dot(x0, y0, fx, fy), n10 = Dot(x0 + 1, y0, fx - 1, fy), n01 = Dot(x0, y0 + 1, fx, fy - 1), n11 = Dot(x0 + 1, y0 + 1, fx - 1, fy - 1);
            float a0 = n00 + (n10 - n00) * sx, a1 = n01 + (n11 - n01) * sx;
            return (a0 + (a1 - a0) * sy) * 1.4f;
        }
    }

    /// <summary>
    /// The paint booth on the Armory workbench: clamp a gun on the red mat, pick a skin on the board (&lt; &gt;) and
    /// press PAINT - a spray can shakes, then sweeps up and down the gun in a cloud of paint, and the gun comes out in
    /// its new skin. Customer stands at local -Z.
    /// </summary>
    public class PaintBooth : MonoBehaviour
    {
        ItemContainer _clamp; TextMesh _name, _hint; int _pick = 1;
        Transform _can; ParticleSystem _mist; AudioSource _hiss; float _sprayT = -1f; string _paintTo; WorldButton _paintBtn;
        const float SprayTime = 3.2f;

        public static PaintBooth Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("PaintBooth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var b = go.AddComponent<PaintBooth>();
            b.Build();
            return b;
        }

        void Build()
        {
            // a red rubber mat with two clamps, on the workbench top
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.915f, 0), new Vector3(1.3f, 0.01f, 0.45f), new Color(0.55f, 0.08f, 0.07f), 0.004f);
            foreach (var x in new[] { -0.35f, 0.35f })
            {
                mb.Sub(1).Box(new Vector3(x, 0.95f, 0), new Vector3(0.05f, 0.06f, 0.12f), new Color(0.2f, 0.2f, 0.22f), 0.01f);
                mb.Sub(1).Box(new Vector3(x, 0.99f, 0.05f), new Vector3(0.05f, 0.02f, 0.04f), new Color(0.75f, 0.6f, 0.1f), 0.006f);
            }
            mb.Build("Mat", transform, Mats.Lit("paint_mat", Color.white, 0f, 0.2f, Mats.Noise, null, 0.3f, 0.1f), Mats.Lit("paint_clamp", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.3f, 0.3f));
            // guns lie along the bench (barrel to your right)
            _clamp = ItemContainer.Grid(transform, new Vector3(0, 0.93f, 0), Quaternion.identity, 1, 1, new Vector2(1.2f, 0.4f), 1.15f, it => it is Gun, Quaternion.Euler(0, 90, 0));
            _clamp.onChanged = Refresh;
            // a few cans of paint on the bench
            var cans = new MB(2);
            int k = 0;
            foreach (var s in GunSkins.All)
            {
                if (s.mat == null) continue;
                float x = -0.6f + (k % 4) * 0.07f, z = 0.18f + (k / 4) * 0.07f; k++;
                cans.Sub(0).Cyl(new Vector3(x, 0.91f, z), new Vector3(x, 1.05f, z), 0.028f, 0.028f, 10, s.spray);
                cans.Sub(1).Cyl(new Vector3(x, 1.05f, z), new Vector3(x, 1.075f, z), 0.012f, 0.01f, 8, new Color(0.85f, 0.85f, 0.85f));
            }
            cans.Build("Cans", transform, Mats.Lit("paint_cans", Color.white, 0.5f, 0.6f, null, null, 0.1f, 0.1f), Mats.Lit("paint_nozzle", Color.white, 0.2f, 0.4f));
            // the board on the pegboard behind: skin name, < >, PAINT
            var board = UI.Panel(transform, "PaintBoard", new Vector3(0, 1.55f, 0.36f), Quaternion.identity, new Vector2(1.1f, 0.5f), null, true, Skin.Steel);
            UI.Text(board, "PAINT BOOTH", new Vector3(0, 0.19f, -0.003f), 0.05f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _name = UI.Text(board, "", new Vector3(0, 0.07f, -0.003f), 0.045f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _hint = UI.Text(board, "", new Vector3(0, -0.02f, -0.003f), 0.024f, UI.Dim, TextAnchor.MiddleCenter);
            UI.Button(board, "<", new Vector3(-0.42f, 0.07f, 0), new Vector2(0.1f, 0.09f), () => { _pick = (_pick + GunSkins.All.Count - 1) % GunSkins.All.Count; Refresh(); }, new Color(0.3f, 0.3f, 0.32f), BtnStyle.Key);
            UI.Button(board, ">", new Vector3(0.42f, 0.07f, 0), new Vector2(0.1f, 0.09f), () => { _pick = (_pick + 1) % GunSkins.All.Count; Refresh(); }, new Color(0.3f, 0.3f, 0.32f), BtnStyle.Key);
            _paintBtn = UI.Button(board, "PAINT", new Vector3(0, -0.15f, 0), new Vector2(0.36f, 0.1f), Paint, new Color(0.55f, 0.15f, 0.12f), BtnStyle.Key);
            // the spray can that does the work (parked at the side)
            var can = new MB(2);
            can.Sub(0).Cyl(new Vector3(0, -0.07f, 0), new Vector3(0, 0.07f, 0), 0.03f, 0.03f, 12, Color.white);
            can.Sub(1).Cyl(new Vector3(0, 0.07f, 0), new Vector3(0, 0.1f, 0), 0.013f, 0.01f, 8, new Color(0.8f, 0.8f, 0.8f));
            _can = can.Build("SprayCan", transform, Mats.Lit("spray_can", Color.white, 0.5f, 0.6f, null, null, 0.1f, 0.1f), Mats.Lit("paint_nozzle", Color.white, 0.2f, 0.4f)).transform;
            Park();
            // paint mist
            var mgo = new GameObject("Mist"); mgo.transform.SetParent(_can, false); mgo.transform.localPosition = new Vector3(0, 0.1f, 0);
            mgo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            _mist = mgo.AddComponent<ParticleSystem>();
            _mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _mist.main; main.startLifetime = 0.7f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.maxParticles = 400; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.05f; main.playOnAwake = false;
            var em = _mist.emission; em.rateOverTime = 150f;
            var sh = _mist.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.005f;
            var col = _mist.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0, 1) }); col.color = gr;
            var sol = _mist.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 2.5f)));
            mgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Unlit("fx_paint_mist", Color.white, Mats.Smoke);
            _hiss = gameObject.AddComponent<AudioSource>(); _hiss.clip = Synth.SprayHiss(); _hiss.loop = true; _hiss.spatialBlend = 1f; _hiss.minDistance = 0.5f; _hiss.maxDistance = 8f; _hiss.volume = 0f;
            Refresh();
        }

        Gun Clamped => _clamp.slots[0].item as Gun;
        void Park() { _can.localPosition = new Vector3(0.55f, 0.99f, 0.15f); _can.localRotation = Quaternion.identity; }

        void Refresh()
        {
            var s = GunSkins.All[_pick];
            var g = Clamped;
            _name.text = s.name;
            _hint.text = _sprayT >= 0f ? "painting..." : g == null ? "Put a gun on the red mat" : g.skin == s.id ? "The " + g.g.name + " already has this" : "Paint the " + g.g.name;
            _paintBtn.Interactable = g != null && _sprayT < 0f && g.skin != s.id;
            if (_paintBtn.Label != null) _paintBtn.Label.text = s.mat == null ? "STRIP" : "PAINT";
        }

        void Paint()
        {
            var g = Clamped;
            if (g == null || _sprayT >= 0f) return;
            var s = GunSkins.All[_pick];
            _paintTo = s.id;
            _sprayT = 0f;
            var main = _mist.main; main.startColor = new ParticleSystem.MinMaxGradient(s.spray, Color.Lerp(s.spray, Color.white, 0.25f));
            Sfx.Play(Synth.CanRattle(), _can.position, 0.7f, 1f, 0.4f, 6f);
            Refresh();
        }

        void Update()
        {
            if (_sprayT < 0f) return;
            var g = Clamped;
            if (g == null) { StopSpray(); return; }   // they took it off mid-spray
            _sprayT += Time.deltaTime;
            float shake = 0.6f;   // shake the can first
            if (_sprayT < shake)
            {
                _can.localPosition = new Vector3(0.45f, 1.15f + Mathf.Sin(_sprayT * 60f) * 0.03f, -0.05f);
                _can.localRotation = Quaternion.Euler(0, 0, 180f + Mathf.Sin(_sprayT * 60f) * 15f);
                return;
            }
            float t = (_sprayT - shake) / SprayTime;
            if (!_mist.isPlaying) { _mist.Play(); _hiss.Play(); }
            _hiss.volume = 0.55f;
            // sweep along the gun and back, top then side, aiming down at it
            float along = Mathf.Sin(t * Mathf.PI * 3f) * 0.5f;
            float height = 1.2f + Mathf.Sin(t * Mathf.PI * 7f) * 0.03f;
            _can.localPosition = new Vector3(along, height, -0.12f + Mathf.Sin(t * Mathf.PI) * 0.06f);
            _can.localRotation = Quaternion.LookRotation(Vector3.down + Vector3.forward * 0.5f) * Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 180);
            var mt = _mist.transform; mt.rotation = Quaternion.LookRotation((g.transform.position + Vector3.up * 0.02f - mt.position).normalized);
            // the new paint goes on half way through, under the cloud
            if (t >= 0.5f && _paintTo != null) { GunSkins.Apply(g, _paintTo); _paintTo = null; }
            if (t >= 1f)
            {
                StopSpray();
                Sfx.UI(Synth.Cash(), 0.4f);
                RagoGame.I?.Player?.Toast(g.g.name + " painted: " + GunSkins.Get(g.skin).name);
            }
        }

        void StopSpray()
        {
            if (_paintTo != null && Clamped != null) GunSkins.Apply(Clamped, _paintTo);
            _paintTo = null; _sprayT = -1f;
            _mist.Stop(true, ParticleSystemStopBehavior.StopEmitting); _hiss.Stop(); _hiss.volume = 0f;
            Park(); Refresh();
        }

        /// <summary>Leaving the bunker: a gun left in the clamp goes back to the stash.</summary>
        /// <summary>The gun in the clamp, saved without taking it out (the bunker saves it with the stash).</summary>
        public void SaveTo(List<ItemData> into) { var sl = _clamp.slots[0]; if (sl.item != null) into.Add(sl.item.Save()); }

        public void SweepToStash(List<ItemData> into)
        {
            var sl = _clamp.slots[0];
            if (sl.item != null) { var it = sl.item; into.Add(it.Save()); sl.Release(it); Item.Discard(it); }
        }
    }
}
