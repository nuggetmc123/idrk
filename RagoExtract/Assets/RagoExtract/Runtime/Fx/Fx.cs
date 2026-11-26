using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Shooting and hit effects: muzzle flashes with light, muzzle smoke, ejected brass, tracers,
    /// sparks, dust, debris, bullet-hole decals, and - since 3.47 the people are ragdoll figures that leak OIL, not
    /// blood - oil spray/mist, oil splatter on walls & floors, wound decals that stick to bodies and spreading oil pools.
    /// (The fields and methods kept their old "blood" names.)
    /// </summary>
    public class Fx : MonoBehaviour
    {
        static Fx _i;
        public static Fx I { get { if (_i == null) { var go = new GameObject("Fx"); DontDestroyOnLoad(go); _i = go.AddComponent<Fx>(); _i.Setup(); } return _i; } }

        ParticleSystem _smoke, _sparks, _dust, _blood, _bloodMist, _debris, _flashSprite, _chips;
        readonly List<Flash> _flashes = new List<Flash>();
        readonly List<Tracer> _tracers = new List<Tracer>();
        readonly List<Decal> _holes = new List<Decal>();
        readonly List<Decal> _bloodDecals = new List<Decal>();
        readonly List<Casing> _casings = new List<Casing>();
        readonly List<Pool> _pools = new List<Pool>();
        int _holeIdx, _bloodIdx, _casingIdx;
        Mesh _quad, _casingMesh, _shellMesh;

        class Flash { public GameObject go; public Light light; public float t; public Transform follow; }
        class Tracer { public LineRenderer lr; public Vector3 a, b; public float t, life; public bool active; }
        class Decal { public GameObject go; public MeshRenderer mr; }
        class Pool { public Transform t; public float grow, size, target; }

        void Setup()
        {
            _quad = new MB().Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0), Color.white, Vector2.one).ToMesh("fxquad");
            _casingMesh = new MB().Cyl(new Vector3(0, -0.012f, 0), new Vector3(0, 0.012f, 0), 0.0045f, 0.004f, 8, Color.white).ToMesh("casing");
            _shellMesh = new MB().Cyl(new Vector3(0, -0.03f, 0), new Vector3(0, 0.03f, 0), 0.0095f, 0.0095f, 10, new Color(0.75f, 0.15f, 0.12f)).ToMesh("shell");

            _smoke = MakePS("Smoke", Mats.Unlit("fx_smoke", new Color(0.8f, 0.8f, 0.8f, 0.35f), Mats.Smoke), 400, 0.0f, false, 1f, 2.5f);
            _dust = MakePS("Dust", Mats.Unlit("fx_dust", new Color(0.7f, 0.65f, 0.58f, 0.6f), Mats.Smoke), 400, 0.25f, false, 0.6f, 3f);
            _sparks = MakePS("Sparks", Mats.Unlit("fx_spark", new Color(1f, 0.75f, 0.35f, 1f), Mats.Spark, Mats.BlendKind.Additive), 300, 1f, true, 0f, 0.1f);
            _blood = MakePS("Oil", Mats.Unlit("fx_oil", Color.white, Mats.OilDrop), 600, 1.2f, true, 0f, 0.4f);
            _bloodMist = MakePS("OilMist", Mats.Unlit("fx_oilmist", new Color(1f, 1f, 1f, 0.55f), Mats.Smoke), 200, 0.05f, false, 0.3f, 3.5f);
            _flashSprite = MakePS("FlashSprite", Mats.Unlit("fx_flashsprite", new Color(1f, 0.95f, 0.85f, 1f), Mats.Flash, Mats.BlendKind.Additive), 60, 0f, false, 0.4f, 1.6f);   // grenade / flashbang pop (was never made: flashbangs threw an error)
            _debris = MakePS("Debris", Mats.Unlit("fx_debris", new Color(0.3f, 0.28f, 0.25f, 1f), Mats.Drop), 300, 1.5f, false, 0f, 0.8f);
            _chips = MakePS("ShellChips", Mats.Unlit("fx_chips", new Color(0.86f, 0.85f, 0.82f, 1f), Mats.Drop), 120, 1.6f, false, 0f, 0.8f);   // bits of a ragdoll's pale shell
        }

        ParticleSystem MakePS(string name, Material mat, int max, float gravity, bool stretch, float growth, float growEnd)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.duration = 1f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (growth > 0)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f - growth * 0.5f), new Keyframe(1, growEnd)));
            }
            var lim = ps.limitVelocityOverLifetime;
            if (!stretch) { lim.enabled = true; lim.dampen = 0.08f; lim.limit = 0.5f; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.04f; r.lengthScale = 1.5f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color col, float rot = 0)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startSize = size, startLifetime = life, startColor = col,
                rotation = rot, applyShapeToPosition = false
            };
            if (ps != null) ps.Emit(ep, 1);
        }

        // ---------------------------------------------------------------- grenades
        /// <summary>Frag: fireball, sparks, a dust ring and a column of smoke. Flashbang: a blinding white pop and a puff.</summary>
        /// <summary>Glass shards flying out of a broken window.</summary>
        public static void GlassShatter(Vector3 c, Vector3 size, Quaternion rot, Vector3 push)
        {
            if (I == null) return;
            var ps = I._debris != null ? I._debris : I._sparks;
            for (int i = 0; i < 40; i++)
            {
                var p = c + rot * new Vector3(0, Random.Range(-0.5f, 0.5f) * size.y, Random.Range(-0.5f, 0.5f) * size.z);
                Emit(ps, p, push * Random.Range(1f, 4f) + Random.insideUnitSphere * 1.2f + Vector3.down * 0.5f, Random.Range(0.01f, 0.04f), Random.Range(0.8f, 1.6f), new Color(0.75f, 0.85f, 0.9f, 0.8f), Random.Range(0f, 360f));
            }
        }

        /// <summary>A puff of coloured marker smoke (air drop flare / landing zone).</summary>
        /// <summary>One puff of fire from something burning: rising flame tongues, a few embers and dark smoke.</summary>
        public static void Flames(Vector3 p, float strength = 1f)
        {
            var I2 = I;
            if (I2 == null || I2._flashSprite == null) return;
            for (int i = 0; i < 2; i++)
                Emit(I2._flashSprite, p + Random.insideUnitSphere * 0.06f * strength, Vector3.up * Random.Range(0.5f, 1.1f), Random.Range(0.08f, 0.16f) * strength, Random.Range(0.18f, 0.32f),
                     Color.Lerp(new Color(1f, 0.45f, 0.08f, 1f), new Color(1f, 0.8f, 0.3f, 1f), Random.value), Random.Range(0f, 360f));
            if (Random.value < 0.35f) Emit(I2._sparks, p, Vector3.up * Random.Range(0.6f, 1.6f) + Random.insideUnitSphere * 0.4f, 0.006f, Random.Range(0.3f, 0.7f), new Color(1f, 0.6f, 0.2f));
            if (Random.value < 0.25f) Emit(I2._smoke, p + Vector3.up * 0.15f, Vector3.up * Random.Range(0.4f, 0.8f), Random.Range(0.15f, 0.3f), Random.Range(1f, 1.8f), new Color(0.15f, 0.13f, 0.12f, 0.45f));
        }

        public static void SignalSmoke(Vector3 p, Color c)
        {
            if (I == null || I._smoke == null) return;
            for (int i = 0; i < 3; i++)
                Emit(I._smoke, p + Random.insideUnitSphere * 0.15f, Vector3.up * Random.Range(1.2f, 2.2f) + Random.insideUnitSphere * 0.4f, Random.Range(0.6f, 1.3f), Random.Range(3f, 5f), c);
        }

        public static void Explosion(Vector3 p, bool flash)
        {
            var I2 = I;
            var lgo = new GameObject("BlastLight");
            lgo.transform.position = p + Vector3.up * 0.4f;
            var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = flash ? 14f : 16f; l.intensity = flash ? 3f : 6f;   // no eye-searing white pop
            l.color = flash ? new Color(1f, 1f, 1f) : new Color(1f, 0.6f, 0.25f);
            Destroy(lgo, flash ? 0.12f : 0.18f);
            if (flash)
            {
                for (int i = 0; i < 2; i++) Emit(I2._flashSprite, p + Vector3.up * 0.2f, Vector3.zero, 1.2f + i * 0.5f, 0.08f, new Color(0.6f, 0.6f, 0.55f, 0.6f));
                for (int i = 0; i < 4; i++) Emit(I2._smoke, p + Vector3.up * 0.1f, (Vector3.up + Random.insideUnitSphere * 0.6f) * 0.6f, Random.Range(0.4f, 0.8f), Random.Range(1.5f, 2.5f), new Color(0.85f, 0.85f, 0.85f, 0.45f));
                for (int i = 0; i < 12; i++) Emit(I2._sparks, p, Random.insideUnitSphere * 5f, 0.012f, 0.15f, new Color(1, 1, 0.9f));
                return;
            }
            for (int i = 0; i < 4; i++) Emit(I2._flashSprite, p + Vector3.up * 0.4f + Random.insideUnitSphere * 0.4f, Vector3.zero, Random.Range(2.2f, 3.6f), 0.16f, new Color(1f, 0.65f, 0.3f, 1f));
            for (int i = 0; i < 40; i++) Emit(I2._sparks, p + Vector3.up * 0.2f, (Random.insideUnitSphere + Vector3.up * 0.4f) * Random.Range(6f, 16f), Random.Range(0.012f, 0.025f), Random.Range(0.2f, 0.6f), new Color(1f, 0.75f, 0.35f));
            for (int i = 0; i < 26; i++) Emit(I2._debris, p + Vector3.up * 0.1f, (Random.insideUnitSphere + Vector3.up * 0.8f) * Random.Range(3f, 9f), Random.Range(0.015f, 0.04f), Random.Range(0.6f, 1.4f), new Color(0.28f, 0.24f, 0.2f));
            for (int i = 0; i < 14; i++)
            {
                var r = Random.insideUnitCircle.normalized;
                Emit(I2._dust, p + Vector3.up * 0.15f, new Vector3(r.x, 0.15f, r.y) * Random.Range(3f, 6f), Random.Range(0.6f, 1.2f), Random.Range(1.2f, 2.4f), new Color(0.5f, 0.45f, 0.38f, 0.6f));
            }
            for (int i = 0; i < 10; i++) Emit(I2._smoke, p + Vector3.up * (0.3f + i * 0.15f), (Vector3.up * Random.Range(0.8f, 2f) + Random.insideUnitSphere * 0.6f), Random.Range(0.8f, 1.6f), Random.Range(2.5f, 4.5f), new Color(0.22f, 0.21f, 0.2f, 0.65f));
            // scorch mark
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out var g, 2f, ~0, QueryTriggerInteraction.Ignore) && g.collider.attachedRigidbody == null)
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, Mats.Unlit("fx_scorch", new Color(0.05f, 0.05f, 0.05f, 0.85f), Mats.OilSplat, Mats.BlendKind.Alpha, false, -40), g.point, g.normal, Random.Range(1.4f, 2f), null);
        }

        // ---------------------------------------------------------------- muzzle
        public static void MuzzleFlash(Transform muzzle, float scale, bool suppressed)
        {
            var f = I.GetFlash();
            f.follow = muzzle;
            f.t = suppressed ? 0.025f : 0.05f;
            f.go.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation * Quaternion.Euler(0, 0, Random.Range(0, 360f)));
            float s = scale * (suppressed ? 0.25f : 1f) * Random.Range(0.8f, 1.2f);
            f.go.transform.localScale = new Vector3(s, s, s * Random.Range(1.0f, 1.6f));
            f.go.SetActive(true);
            f.light.intensity = suppressed ? 0.6f : 3.5f * Mathf.Clamp(scale, 0.6f, 1.6f);
            f.light.range = suppressed ? 2f : 6f;
            // muzzle smoke
            int n = suppressed ? 2 : 4;
            for (int i = 0; i < n; i++)
                Emit(I._smoke, muzzle.position + muzzle.forward * 0.03f, muzzle.forward * Random.Range(0.3f, 1.4f) + Random.insideUnitSphere * 0.2f + Vector3.up * 0.15f,
                    Random.Range(0.06f, 0.14f) * scale, Random.Range(0.6f, 1.4f), new Color(0.85f, 0.85f, 0.85f, 0.25f), Random.Range(0, 6.28f));
            if (!suppressed)
                for (int i = 0; i < 3; i++)
                    Emit(I._sparks, muzzle.position, muzzle.forward * Random.Range(6, 14f) + Random.insideUnitSphere * 2f, 0.01f, Random.Range(0.04f, 0.1f), new Color(1, 0.8f, 0.4f));
        }

        Flash GetFlash()
        {
            foreach (var x in _flashes) if (!x.go.activeSelf) return x;
            var f = new Flash();
            f.go = new GameObject("MuzzleFlash");
            f.go.transform.SetParent(transform, false);
            var mat = Mats.Unlit("fx_flash", new Color(1.6f, 1.2f, 0.7f, 1f), Mats.Flash, Mats.BlendKind.Additive);
            // crossed quads + front facing burst
            for (int i = 0; i < 3; i++)
            {
                var q = new GameObject("q");
                q.transform.SetParent(f.go.transform, false);
                q.AddComponent<MeshFilter>().sharedMesh = _quad;
                var mr = q.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (i < 2) { q.transform.localRotation = Quaternion.Euler(0, 90, i * 90); q.transform.localScale = new Vector3(0.22f, 0.09f, 1); q.transform.localPosition = new Vector3(0, 0, 0.1f); }
                else q.transform.localScale = new Vector3(0.12f, 0.12f, 1);
            }
            var lgo = new GameObject("light");
            lgo.transform.SetParent(f.go.transform, false);
            lgo.transform.localPosition = new Vector3(0, 0, 0.05f);
            f.light = lgo.AddComponent<Light>();
            f.light.type = LightType.Point;
            f.light.color = new Color(1f, 0.75f, 0.45f);
            f.light.shadows = LightShadows.None;
            _flashes.Add(f);
            return f;
        }

        // ---------------------------------------------------------------- brass
        public static void EjectCasing(Transform port, Vector3 gunVel, bool shotgunShell, bool big)
        {
            if (port == null) return;
            var c = I.GetCasing();
            c.gameObject.SetActive(true);
            c.Setup(shotgunShell ? I._shellMesh : I._casingMesh, shotgunShell ? Mats.Lit("shellred", new Color(0.7f, 0.12f, 0.1f), 0.1f, 0.5f) : Mats.Brass, big ? 1.6f : 1f);
            c.transform.position = port.position;
            c.transform.rotation = port.rotation * Quaternion.Euler(0, 0, 90);
            var rb = c.Rb;
            rb.SetVel(gunVel + port.rotation * new Vector3(Random.Range(1.6f, 2.6f), Random.Range(0.8f, 1.6f), Random.Range(-0.6f, 0.2f)));
            rb.angularVelocity = Random.insideUnitSphere * 30f;
        }

        Casing GetCasing()
        {
            if (_casings.Count < 40)
            {
                var go = new GameObject("Casing");
                go.transform.SetParent(transform, false);
                var c = go.AddComponent<Casing>();
                c.Init();
                _casings.Add(c);
                return c;
            }
            _casingIdx = (_casingIdx + 1) % _casings.Count;
            return _casings[_casingIdx];
        }

        // ---------------------------------------------------------------- tracers
        public static void TracerLine(Vector3 from, Vector3 to, float speed = 900f, Color? col = null)
        {
            var t = I.GetTracer();
            t.a = from; t.b = to; t.t = 0; t.life = Mathf.Max(0.03f, Vector3.Distance(from, to) / speed);
            t.active = true;
            t.lr.enabled = true;
            var c = col ?? new Color(1f, 0.75f, 0.4f, 1f);
            t.lr.startColor = new Color(c.r, c.g, c.b, 0); t.lr.endColor = c;
        }

        Tracer GetTracer()
        {
            foreach (var x in _tracers) if (!x.active) return x;
            var t = new Tracer();
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            t.lr = go.AddComponent<LineRenderer>();
            t.lr.positionCount = 2;
            t.lr.useWorldSpace = true;
            t.lr.startWidth = 0.008f; t.lr.endWidth = 0.022f;
            t.lr.sharedMaterial = Mats.Unlit("fx_tracer", new Color(2.5f, 1.6f, 0.8f, 1f), Mats.Glow, Mats.BlendKind.Additive);
            t.lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.lr.numCapVertices = 2;
            _tracers.Add(t);
            return t;
        }

        // ---------------------------------------------------------------- impacts
        public static void Impact(Vector3 point, Vector3 normal, Vector3 dir, Synth.Surface surf, Transform hitT, float caliberScale = 1f)
        {
            var I2 = I;
            switch (surf)
            {
                case Synth.Surface.Metal:
                    for (int i = 0; i < 10; i++) Emit(I2._sparks, point, (Vector3.Reflect(dir, normal) + Random.insideUnitSphere * 0.7f) * Random.Range(3f, 9f), 0.012f, Random.Range(0.1f, 0.35f), new Color(1, 0.8f, 0.4f));
                    Emit(I2._smoke, point, normal * 0.3f, 0.12f, 0.6f, new Color(0.6f, 0.6f, 0.6f, 0.3f));
                    break;
                case Synth.Surface.Wood:
                    for (int i = 0; i < 6; i++) Emit(I2._debris, point, (normal + Random.insideUnitSphere * 0.8f) * Random.Range(1.5f, 4f), Random.Range(0.01f, 0.03f), Random.Range(0.5f, 1f), new Color(0.45f, 0.3f, 0.18f));
                    Emit(I2._dust, point, normal * 0.5f, 0.15f, 0.8f, new Color(0.55f, 0.45f, 0.35f, 0.5f));
                    break;
                case Synth.Surface.Dirt:
                case Synth.Surface.Grass:
                    for (int i = 0; i < 3; i++) Emit(I2._dust, point + normal * 0.05f, (normal * 1.5f + Random.insideUnitSphere) * Random.Range(0.6f, 1.6f), Random.Range(0.18f, 0.35f), Random.Range(0.8f, 1.6f), new Color(0.45f, 0.38f, 0.28f, 0.7f));
                    for (int i = 0; i < 8; i++) Emit(I2._debris, point, (normal * 2 + Random.insideUnitSphere) * Random.Range(1f, 3.5f), Random.Range(0.01f, 0.025f), Random.Range(0.4f, 0.9f), new Color(0.3f, 0.24f, 0.17f));
                    break;
                case Synth.Surface.Water:
                    for (int i = 0; i < 12; i++) Emit(I2._sparks, point, (Vector3.up * 3 + Random.insideUnitSphere * 1.5f), 0.015f, 0.4f, new Color(0.8f, 0.9f, 1f, 0.6f));
                    break;
                case Synth.Surface.Flesh:
                    return; // handled by Blood()
                default:
                    for (int i = 0; i < 2; i++) Emit(I2._dust, point + normal * 0.03f, (normal + Random.insideUnitSphere * 0.4f) * Random.Range(0.5f, 1.4f), Random.Range(0.12f, 0.25f), Random.Range(0.8f, 1.6f), new Color(0.72f, 0.7f, 0.66f, 0.6f));
                    for (int i = 0; i < 7; i++) Emit(I2._debris, point, (normal * 1.5f + Random.insideUnitSphere) * Random.Range(1.5f, 4.5f), Random.Range(0.008f, 0.02f), Random.Range(0.4f, 0.9f), new Color(0.55f, 0.53f, 0.5f));
                    for (int i = 0; i < 3; i++) Emit(I2._sparks, point, (normal + Random.insideUnitSphere) * 4f, 0.008f, 0.08f, new Color(1, 0.85f, 0.6f));
                    break;
            }
            if (surf != Synth.Surface.Water && surf != Synth.Surface.Grass) I2.PlaceDecal(I2._holes, ref I2._holeIdx, 140, Mats.Unlit("fx_hole", new Color(1, 1, 1, 0.95f), Mats.BulletHole, Mats.BlendKind.Alpha, false, -50),
                point, normal, Random.Range(0.035f, 0.05f) * caliberScale, hitT);
            Sfx.Play(Synth.Impact(surf, Random.Range(0, 3)), point, 0.7f, 1f + Random.Range(-0.08f, 0.08f), 1.5f, 40f);
        }

        // ---------------------------------------------------------------- oil (was blood)
        // The ragdoll people leak used motor oil: near-black drops with a glint, a dark brown haze, black splats with
        // amber edges and a rainbow film, and pools that spread under a body.
        static Material SplatMat => Mats.Unlit("fx_oilsplat", new Color(1f, 1f, 1f, 0.94f), Mats.OilSplat, Mats.BlendKind.Alpha, false, -40);
        static Material WoundMat => Mats.Unlit("fx_oilwound", new Color(0.9f, 0.9f, 0.9f, 1f), Mats.OilSplat, Mats.BlendKind.Alpha, false, -40);
        static Material WoundHoleMat => Mats.Unlit("fx_oilhole", new Color(0.03f, 0.028f, 0.025f, 1f), Mats.BulletHole, Mats.BlendKind.Alpha, false, -39);
        static Color Oil() => new Color(Random.Range(0.06f, 0.12f), Random.Range(0.05f, 0.095f), Random.Range(0.035f, 0.065f), 1f);
        static Color OilHaze(float a) => new Color(0.075f, 0.065f, 0.05f, a);

        /// <summary>Oil on a body hit: spray out of entry and exit, mist, wound decal on the body, splatter on whatever is behind.</summary>
        public static void Blood(Vector3 point, Vector3 dir, Vector3 normal, Transform bone, float damage)
        {
            var I2 = I;
            float amount = Mathf.Clamp(damage / 35f, 0.4f, 2.5f);
            // your own wounds: nothing flies at the camera and nothing sticks in front of your eyes (a head or neck hit
            // used to cover your view in red you couldn't see through)
            var cam = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head : null;
            bool nearEyes = cam != null && (point - cam.position).sqrMagnitude < 0.55f * 0.55f;
            if (nearEyes) { dir = (point - cam.position).sqrMagnitude > 0.0001f ? (point - cam.position).normalized : dir; normal = dir; }
            // small fast droplets of oil that fall, and a faint dark haze - not a fountain of blobs
            int drops = nearEyes ? 0 : Mathf.RoundToInt(16 * amount);
            for (int i = 0; i < drops; i++)
            {
                Vector3 v = dir * Random.Range(1.2f, 4f) + Random.insideUnitSphere * 1.1f;
                Emit(I2._blood, point + dir * 0.06f, v, Random.Range(0.006f, 0.018f), Random.Range(0.5f, 1.0f), Oil());
            }
            if (!nearEyes)
                for (int i = 0; i < Mathf.RoundToInt(3 * amount); i++)
                    Emit(I2._blood, point, normal * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere * 0.5f, Random.Range(0.005f, 0.014f), Random.Range(0.4f, 0.8f), Oil());
            if (!nearEyes)
                Emit(I2._bloodMist, point + dir * 0.05f, dir * Random.Range(0.3f, 0.8f) + Random.insideUnitSphere * 0.2f, Random.Range(0.08f, 0.16f) * amount, Random.Range(0.15f, 0.3f), OilHaze(0.28f));
            // wound decal stuck to the body part (never on your own face in front of the camera): a smear plus a dark hole
            if (bone != null && !nearEyes)
            {
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, WoundMat, point, normal, Random.Range(0.045f, 0.08f) * Mathf.Sqrt(amount), bone);
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, WoundHoleMat, point + normal * 0.001f, normal, Random.Range(0.016f, 0.024f), bone);
                // exit wound on the far side of the body part, with its own spray going out the back
                var col = bone.GetComponent<Collider>();
                if (col != null && damage > 18f && col.Raycast(new Ray(point + dir * 0.6f, -dir), out var ex, 0.6f))
                {
                    I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, WoundMat, ex.point, ex.normal, Random.Range(0.07f, 0.12f) * Mathf.Sqrt(amount), bone);
                    for (int i = 0; i < Mathf.RoundToInt(10 * amount); i++)
                        Emit(I2._blood, ex.point, dir * Random.Range(2f, 5f) + Random.insideUnitSphere * 1.4f, Random.Range(0.008f, 0.022f), Random.Range(0.5f, 1.1f), Oil());
                    Emit(I2._bloodMist, ex.point, dir * Random.Range(0.6f, 1.2f), Random.Range(0.12f, 0.25f) * amount, Random.Range(0.2f, 0.4f), OilHaze(0.32f));
                }
                // headshots: a burst of oil and bits of shell, and big splatter all around behind them
                var hz = bone.GetComponent<HitZone>();
                if (hz != null && hz.part == BodyPart.Head && damage >= 35f) Gibs(point, dir, amount);
            }
            // splatter behind the target: a few splats in a cone, and a run of oil dripping down a wall
            for (int k = 0; k < (amount > 1.2f ? 3 : 2); k++)
                if (Physics.Raycast(point + dir * 0.3f, (dir + Random.insideUnitSphere * (0.12f + k * 0.12f)).normalized, out var hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.attachedRigidbody == null || hit.collider.attachedRigidbody.isKinematic && hit.collider.GetComponentInParent<HitZone>() == null)
                    {
                        float sz = Random.Range(0.25f, 0.7f) * Mathf.Lerp(1.3f, 0.6f, hit.distance / 3.5f) * amount * (k == 0 ? 1f : 0.55f);
                        I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, SplatMat,
                            hit.point, hit.normal, sz, null);
                        if (k == 0 && Mathf.Abs(hit.normal.y) < 0.4f) I2.Streak(hit.point, hit.normal, sz);   // runs down the wall
                    }
                }
            // drip on the ground below
            if (Physics.Raycast(point, Vector3.down, out var g, 2.5f, ~0, QueryTriggerInteraction.Ignore) && g.collider.GetComponentInParent<HitZone>() == null)
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, SplatMat,
                    g.point + Random.insideUnitSphere.Flat() * 0.3f, g.normal, Random.Range(0.15f, 0.35f) * amount, null);
            Sfx.Play(Synth.Impact(Synth.Surface.Flesh, Random.Range(0, 3)), point, 0.9f, 1f + Random.Range(-0.1f, 0.1f), 1.5f, 30f);
        }

        const int MaxBlood = 180;

        /// <summary>A long smear of oil running down a wall from a splat.</summary>
        static Material StreakMat => Mats.Unlit("fx_oilstreak", new Color(0.09f, 0.075f, 0.05f, 0.88f), Mats.Drop, Mats.BlendKind.Alpha, false, -41);
        void Streak(Vector3 point, Vector3 normal, float size)
        {
            Decal d;
            if (_bloodDecals.Count < MaxBlood) { PlaceDecal(_bloodDecals, ref _bloodIdx, MaxBlood, StreakMat, point, normal, size, null); d = _bloodDecals[_bloodDecals.Count - 1]; }
            else { PlaceDecal(_bloodDecals, ref _bloodIdx, MaxBlood, StreakMat, point, normal, size, null); d = _bloodDecals[_bloodIdx]; }
            float len = size * Random.Range(1.5f, 3f);
            // long axis straight down the wall
            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
            d.go.transform.rotation = Quaternion.LookRotation(-normal, -down);
            d.go.transform.position = point + normal * 0.0045f + down * len * 0.45f;
            d.go.transform.localScale = new Vector3(size * 0.22f, len, 1f);
        }

        /// <summary>A headshot: a burst of oil and chips of the shell blown out the back, splats all over behind.</summary>
        public static void Gibs(Vector3 point, Vector3 dir, float amount)
        {
            var I2 = I;
            for (int i = 0; i < 24; i++)
                Emit(I2._blood, point + dir * 0.08f, (dir * Random.Range(1.5f, 5.5f) + Random.insideUnitSphere * 2f), Random.Range(0.012f, 0.035f), Random.Range(0.6f, 1.3f), Oil());
            for (int i = 0; i < 9; i++)   // chips of the pale shell
                Emit(I2._chips, point + dir * 0.06f, (dir * Random.Range(1.2f, 4f) + Random.insideUnitSphere * 2.2f), Random.Range(0.01f, 0.026f), Random.Range(0.7f, 1.3f),
                     Random.value < 0.3f ? new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(1f, 0.98f, 0.94f, 1f));
            for (int i = 0; i < 3; i++)
                Emit(I2._bloodMist, point + dir * 0.1f, dir * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.4f, Random.Range(0.25f, 0.45f), Random.Range(0.3f, 0.6f), OilHaze(0.4f));
            for (int k = 0; k < 4; k++)
                if (Physics.Raycast(point + dir * 0.3f, (dir + Random.insideUnitSphere * 0.5f).normalized, out var h, 4f, ~0, QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<HitZone>() == null)
                {
                    float sz = Random.Range(0.4f, 0.9f);
                    I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, MaxBlood, SplatMat, h.point, h.normal, sz, null);
                    if (Mathf.Abs(h.normal.y) < 0.4f) I2.Streak(h.point, h.normal, sz * 0.8f);
                }
            Sfx.Play(Synth.Impact(Synth.Surface.Flesh, Random.Range(0, 3)), point, 1f, 0.7f, 2f, 35f);
        }

        /// <summary>Single drip of oil for a leaking ("bleeding") character.</summary>
        public static void Drip(Vector3 pos)
        {
            Emit(I._blood, pos, Vector3.down * 0.3f, 0.012f, 0.7f, Oil());
            if (Random.value < 0.12f) for (int i = 0; i < 6; i++) Emit(I._blood, pos, Random.insideUnitSphere.Flat() * 1.2f + Vector3.up * Random.Range(0.3f, 1f), Random.Range(0.006f, 0.012f), 0.6f, Oil());   // a spurt
            if (Random.value < 0.35f && Physics.Raycast(pos, Vector3.down, out var g, 2.2f, ~0, QueryTriggerInteraction.Ignore) && g.collider.GetComponentInParent<HitZone>() == null)
                I.PlaceDecal(I._bloodDecals, ref I._bloodIdx, MaxBlood, SplatMat, g.point, g.normal, Random.Range(0.05f, 0.12f), null);
        }

        /// <summary>A pool of oil that slowly spreads under a body.</summary>
        public static void BloodPool(Vector3 pos)
        {
            if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var g, 2f, ~0, QueryTriggerInteraction.Ignore)) return;
            var go = new GameObject("OilPool");
            go.transform.SetParent(RagoGame.I != null && RagoGame.I.MapRoot != null ? RagoGame.I.MapRoot : I.transform, true);
            go.transform.position = g.point + g.normal * 0.006f;
            go.transform.rotation = Quaternion.LookRotation(-g.normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
            go.AddComponent<MeshFilter>().sharedMesh = I._quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Unlit("fx_oilpool", new Color(1f, 1f, 1f, 0.97f), Mats.OilSplat, Mats.BlendKind.Alpha, false, -45);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * 0.05f;
            I._pools.Add(new Pool { t = go.transform, size = 0.05f, target = Random.Range(1.3f, 2.1f), grow = 0.12f });   // bigger pools
        }

        void PlaceDecal(List<Decal> list, ref int idx, int max, Material mat, Vector3 point, Vector3 normal, float size, Transform parent)
        {
            Decal d;
            if (list.Count < max)
            {
                d = new Decal { go = new GameObject("Decal") };
                d.go.AddComponent<MeshFilter>().sharedMesh = _quad;
                d.mr = d.go.AddComponent<MeshRenderer>();
                d.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.mr.receiveShadows = false;
                list.Add(d);
            }
            else { idx = (idx + 1) % max; d = list[idx]; }
            d.mr.sharedMaterial = mat;
            d.go.SetActive(true);
            d.go.transform.SetParent(parent != null ? parent : (RagoGame.I != null && RagoGame.I.MapRoot != null ? RagoGame.I.MapRoot : transform), true);
            d.go.transform.position = point + normal * 0.004f;
            d.go.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
            Vector3 ls = Vector3.one * size;
            if (parent != null) { var ps = parent.lossyScale; ls = new Vector3(size / Mathf.Max(0.001f, ps.x), size / Mathf.Max(0.001f, ps.y), size / Mathf.Max(0.001f, ps.z)); }
            d.go.transform.localScale = ls;
        }

        /// <summary>Called when a map is torn down: decals parented to the map die with it, just forget them.</summary>
        public static void ClearMapEffects()
        {
            if (_i == null) return;
            _i._holes.RemoveAll(d => d.go == null);
            _i._bloodDecals.RemoveAll(d => d.go == null);
            foreach (var d in _i._holes) d.go.SetActive(false);
            foreach (var d in _i._bloodDecals) { d.go.transform.SetParent(_i.transform, false); d.go.SetActive(false); }
            _i._pools.Clear();
            _i._holeIdx = 0; _i._bloodIdx = 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var f in _flashes)
            {
                if (!f.go.activeSelf) continue;
                f.t -= dt;
                if (f.follow != null) f.go.transform.position = f.follow.position;
                f.light.intensity *= 0.6f;
                if (f.t <= 0) f.go.SetActive(false);
            }
            foreach (var t in _tracers)
            {
                if (!t.active) continue;
                t.t += dt;
                float k = t.t / t.life;
                if (k >= 1) { t.active = false; t.lr.enabled = false; continue; }
                Vector3 head = Vector3.Lerp(t.a, t.b, k);
                Vector3 tail = Vector3.Lerp(t.a, t.b, Mathf.Max(0, k - 4f / Mathf.Max(1f, Vector3.Distance(t.a, t.b))));
                t.lr.SetPosition(0, tail); t.lr.SetPosition(1, head);
            }
            for (int i = _pools.Count - 1; i >= 0; i--)
            {
                var p = _pools[i];
                if (p.t == null) { _pools.RemoveAt(i); continue; }
                p.size = Mathf.MoveTowards(p.size, p.target, p.grow * dt * (1.2f - p.size / p.target));
                p.t.localScale = Vector3.one * p.size;
                if (Mathf.Abs(p.size - p.target) < 0.01f) _pools.RemoveAt(i);
            }
            // tear down decals whose parent was destroyed (e.g. a body despawned)
        }
    }

    /// <summary>Ejected brass casing with bounce sounds.</summary>
    public class Casing : MonoBehaviour
    {
        public Rigidbody Rb;
        MeshFilter _mf; MeshRenderer _mr; CapsuleCollider _col;
        float _life; int _bounces;

        public void Init()
        {
            _mf = gameObject.AddComponent<MeshFilter>();
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _col = gameObject.AddComponent<CapsuleCollider>();
            _col.radius = 0.005f; _col.height = 0.026f; _col.direction = 1;
            Rb = gameObject.AddComponent<Rigidbody>();
            Rb.mass = 0.02f;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            gameObject.SetLayerRecursive(Layers.Items);
        }

        public void Setup(Mesh m, Material mat, float scale)
        {
            _mf.sharedMesh = m; _mr.sharedMaterial = mat;
            transform.localScale = Vector3.one * scale;
            _life = 6f; _bounces = 0;
            Rb.isKinematic = false;
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0) gameObject.SetActive(false);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_bounces++ > 3) return;
            if (c.relativeVelocity.magnitude < 0.6f) return;
            Sfx.Play(Synth.Casing(Random.Range(0, 4)), transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 4f) * 0.35f, 1f + Random.Range(-0.1f, 0.1f), 0.5f, 12f);
        }
    }
}
