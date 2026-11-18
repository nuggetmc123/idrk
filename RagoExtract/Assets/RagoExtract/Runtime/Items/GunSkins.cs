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
        public static readonly List<Skin> All = new List<Skin>
        {
            new Skin { id = "", name = "Factory finish", spray = new Color(0.25f, 0.25f, 0.26f) },
            new Skin { id = "desert", name = "Desert Tan", spray = new Color(0.78f, 0.66f, 0.46f), mat = () => Camo("desert", new Color(0.78f, 0.66f, 0.46f), new Color(0.62f, 0.5f, 0.33f), new Color(0.48f, 0.38f, 0.25f), 0.35f) },
            new Skin { id = "woodland", name = "Woodland Camo", spray = new Color(0.3f, 0.38f, 0.22f), mat = () => Camo("woodland", new Color(0.34f, 0.4f, 0.24f), new Color(0.24f, 0.2f, 0.13f), new Color(0.1f, 0.11f, 0.09f), 0.45f) },
            new Skin { id = "arctic", name = "Arctic", spray = new Color(0.92f, 0.94f, 0.96f), mat = () => Camo("arctic", new Color(0.93f, 0.95f, 0.97f), new Color(0.72f, 0.76f, 0.8f), new Color(0.45f, 0.48f, 0.52f), 0.4f) },
            new Skin { id = "urban", name = "Urban Digital", spray = new Color(0.5f, 0.52f, 0.55f), mat = () => Digital("urban", new Color(0.55f, 0.57f, 0.6f), new Color(0.35f, 0.37f, 0.4f), new Color(0.18f, 0.19f, 0.21f)) },
            new Skin { id = "crimson", name = "Crimson", spray = new Color(0.65f, 0.08f, 0.08f), mat = () => Mats.Lit("skin_crimson", new Color(0.6f, 0.07f, 0.07f), 0.55f, 0.6f, Mats.Metal, Mats.MetalN, 0.15f, 0.25f, true, 1.5f) },
            new Skin { id = "tiger", name = "Neon Tiger", spray = new Color(1f, 0.45f, 0.05f), mat = () => Tiger() },
            new Skin { id = "gold", name = "Gold Rush", spray = new Color(1f, 0.8f, 0.25f), mat = () => Mats.Gold },
            new Skin { id = "carbon", name = "Carbon Fibre", spray = new Color(0.1f, 0.1f, 0.11f), mat = () => Carbon() },
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
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "skin_" + key };
            var c = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) c[y * size + x] = px(x, y);
            t.SetPixels(c); t.Apply(true, true);
            return _tex[key] = t;
        }

        /// <summary>Soft blotchy camouflage in three colours (tileable noise).</summary>
        static Material Camo(string key, Color a, Color b, Color c, float scale)
        {
            int n = 128;
            var tex = Make(key, n, (x, y) =>
            {
                float u = x / (float)n, v = y / (float)n;
                float f1 = Tile(u, v, 4f, 1.7f), f2 = Tile(u, v, 6f, 9.3f);
                return f1 > 0.58f ? b : f2 > 0.62f ? c : a;
            });
            return Mats.Lit("skin_" + key, Color.white, 0.1f, 0.3f, tex, null, 0.25f, 0.15f, true, scale);
        }

        /// <summary>Square "pixel" camouflage.</summary>
        static Material Digital(string key, Color a, Color b, Color c)
        {
            int n = 64;
            var tex = Make(key, n, (x, y) =>
            {
                int bx = x / 4, by = y / 4;
                float f = Tile(bx / 16f, by / 16f, 3f, 4.1f), f2 = Tile(bx / 16f, by / 16f, 5f, 7.7f);
                return f > 0.6f ? b : f2 > 0.64f ? c : a;
            });
            tex.filterMode = FilterMode.Point;
            return Mats.Lit("skin_" + key, Color.white, 0.1f, 0.3f, tex, null, 0.25f, 0.15f, true, 0.4f);
        }

        static Material Tiger()
        {
            int n = 128;
            var tex = Make("tiger", n, (x, y) =>
            {
                float u = x / (float)n, v = y / (float)n;
                float stripe = Mathf.Sin((u * 6f + Tile(u, v, 3f, 2.2f) * 1.6f) * Mathf.PI * 2f);
                return stripe > 0.55f ? new Color(0.06f, 0.05f, 0.05f) : new Color(1f, 0.5f, 0.06f);
            });
            return Mats.Lit("skin_tiger", Color.white, 0.2f, 0.55f, tex, null, 0.1f, 0.1f, true, 0.35f, new Color(0.25f, 0.1f, 0f));
        }

        static Material Carbon()
        {
            int n = 32;
            var tex = Make("carbon", n, (x, y) =>
            {
                bool weave = ((x / 4) + (y / 4)) % 2 == 0;
                float g = weave ? 0.12f + 0.05f * Mathf.Sin(x * 0.8f) : 0.08f + 0.05f * Mathf.Sin(y * 0.8f);
                return new Color(g, g, g * 1.08f);
            });
            return Mats.Lit("skin_carbon", Color.white, 0.4f, 0.75f, tex, null, 0.05f, 0.05f, true, 3f);
        }

        /// <summary>Tileable value noise in 0..1.</summary>
        static float Tile(float u, float v, float freq, float seed)
        {
            float s = 0, amp = 1, tot = 0;
            for (int o = 0; o < 3; o++)
            {
                float f = freq * (1 << o);
                float a = Mathf.PerlinNoise(Mathf.Cos(u * Mathf.PI * 2) * f * 0.3f + seed, Mathf.Sin(u * Mathf.PI * 2) * f * 0.3f + Mathf.Cos(v * Mathf.PI * 2) * f * 0.3f + seed * 2.1f);
                float b2 = Mathf.PerlinNoise(Mathf.Sin(v * Mathf.PI * 2) * f * 0.3f + seed * 0.7f, a * 2f);
                s += (a * 0.6f + b2 * 0.4f) * amp; tot += amp; amp *= 0.5f;
            }
            return s / tot;
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
        public void SweepToStash(List<ItemData> into)
        {
            var sl = _clamp.slots[0];
            if (sl.item != null) { var it = sl.item; into.Add(it.Save()); sl.Release(it); Destroy(it.gameObject); }
        }
    }
}
