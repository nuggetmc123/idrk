using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Shooting and blood effects: muzzle flashes with light, muzzle smoke, ejected brass, tracers,
    /// sparks, dust, debris, bullet-hole decals, blood spray/mist, blood splatter on walls & floors,
    /// wound decals that stick to bodies and spreading blood pools.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        static Fx _i;
        public static Fx I { get { if (_i == null) { var go = new GameObject("Fx"); DontDestroyOnLoad(go); _i = go.AddComponent<Fx>(); _i.Setup(); } return _i; } }

        ParticleSystem _smoke, _sparks, _dust, _blood, _bloodMist, _debris, _flashSprite;
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
            _blood = MakePS("Blood", Mats.Unlit("fx_blood", new Color(0.45f, 0.02f, 0.02f, 1f), Mats.BloodDrop), 600, 1.2f, true, 0f, 0.4f);
            _bloodMist = MakePS("BloodMist", Mats.Unlit("fx_bloodmist", new Color(0.5f, 0.03f, 0.03f, 0.55f), Mats.Smoke), 200, 0.05f, false, 0.3f, 3.5f);
            _flashSprite = MakePS("FlashSprite", Mats.Unlit("fx_flashsprite", new Color(1f, 0.95f, 0.85f, 1f), Mats.Flash, Mats.BlendKind.Additive), 60, 0f, false, 0.4f, 1.6f);   // grenade / flashbang pop (was never made: flashbangs threw an error)
            _debris = MakePS("Debris", Mats.Unlit("fx_debris", new Color(0.3f, 0.28f, 0.25f, 1f), Mats.BloodDrop), 300, 1.5f, false, 0f, 0.8f);
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
        public static void Explosion(Vector3 p, bool flash)
        {
            var I2 = I;
            var lgo = new GameObject("BlastLight");
            lgo.transform.position = p + Vector3.up * 0.4f;
            var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = flash ? 22f : 16f; l.intensity = flash ? 9f : 6f;
            l.color = flash ? new Color(1f, 1f, 1f) : new Color(1f, 0.6f, 0.25f);
            Destroy(lgo, flash ? 0.12f : 0.18f);
            if (flash)
            {
                for (int i = 0; i < 3; i++) Emit(I2._flashSprite, p + Vector3.up * 0.2f, Vector3.zero, 2.5f + i, 0.12f, new Color(1, 1, 1, 1));
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
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, 90, Mats.Unlit("fx_scorch", new Color(0.05f, 0.05f, 0.05f, 0.85f), Mats.BloodSplat, Mats.BlendKind.Alpha, false, -40), g.point, g.normal, Random.Range(1.4f, 2f), null);
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

        // ---------------------------------------------------------------- blood
        /// <summary>Blood on a flesh hit: spray out of entry and exit, mist, wound decal on the body, splatter on whatever is behind.</summary>
        public static void Blood(Vector3 point, Vector3 dir, Vector3 normal, Transform bone, float damage)
        {
            var I2 = I;
            float amount = Mathf.Clamp(damage / 35f, 0.4f, 2.5f);
            int drops = Mathf.RoundToInt(14 * amount);
            for (int i = 0; i < drops; i++)
            {
                Vector3 v = (dir * Random.Range(1.5f, 5f) + Random.insideUnitSphere * 1.8f + Vector3.up * 0.5f);
                Emit(I2._blood, point + dir * 0.08f, v, Random.Range(0.015f, 0.045f), Random.Range(0.4f, 0.9f), new Color(Random.Range(0.35f, 0.55f), 0.02f, 0.02f, 1f));
            }
            for (int i = 0; i < Mathf.RoundToInt(5 * amount); i++)
                Emit(I2._blood, point, (normal * Random.Range(1f, 2.5f) + Random.insideUnitSphere), Random.Range(0.012f, 0.03f), Random.Range(0.3f, 0.6f), new Color(0.5f, 0.02f, 0.02f, 1f));
            for (int i = 0; i < 2; i++)
                Emit(I2._bloodMist, point + dir * 0.05f, dir * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.3f, Random.Range(0.15f, 0.3f) * amount, Random.Range(0.3f, 0.6f), new Color(0.5f, 0.03f, 0.03f, 0.5f));
            // wound decal stuck to the body part
            if (bone != null)
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, 90, Mats.Unlit("fx_wound", new Color(0.7f, 0.05f, 0.05f, 1f), Mats.BloodSplat, Mats.BlendKind.Alpha, false, -40), point, normal, Random.Range(0.06f, 0.1f), bone);
            // splatter behind the target
            if (Physics.Raycast(point + dir * 0.3f, (dir + Random.insideUnitSphere * 0.15f).normalized, out var hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody == null || hit.collider.attachedRigidbody.isKinematic && hit.collider.GetComponentInParent<HitZone>() == null)
                    I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, 90, Mats.Unlit("fx_bloodsplat", new Color(0.65f, 0.04f, 0.04f, 0.95f), Mats.BloodSplat, Mats.BlendKind.Alpha, false, -40),
                        hit.point, hit.normal, Random.Range(0.25f, 0.7f) * Mathf.Lerp(1.3f, 0.6f, hit.distance / 3.5f) * amount, null);
            }
            // drip on the ground below
            if (Physics.Raycast(point, Vector3.down, out var g, 2.5f, ~0, QueryTriggerInteraction.Ignore) && g.collider.GetComponentInParent<HitZone>() == null)
                I2.PlaceDecal(I2._bloodDecals, ref I2._bloodIdx, 90, Mats.Unlit("fx_bloodsplat", new Color(0.65f, 0.04f, 0.04f, 0.95f), Mats.BloodSplat, Mats.BlendKind.Alpha, false, -40),
                    g.point + Random.insideUnitSphere.Flat() * 0.3f, g.normal, Random.Range(0.15f, 0.35f) * amount, null);
            Sfx.Play(Synth.Impact(Synth.Surface.Flesh, Random.Range(0, 3)), point, 0.9f, 1f + Random.Range(-0.1f, 0.1f), 1.5f, 30f);
        }

        /// <summary>Single drip for bleeding characters.</summary>
        public static void Drip(Vector3 pos)
        {
            Emit(I._blood, pos, Vector3.down * 0.3f, 0.02f, 0.7f, new Color(0.45f, 0.02f, 0.02f, 1f));
            if (Random.value < 0.35f && Physics.Raycast(pos, Vector3.down, out var g, 2.2f, ~0, QueryTriggerInteraction.Ignore) && g.collider.GetComponentInParent<HitZone>() == null)
                I.PlaceDecal(I._bloodDecals, ref I._bloodIdx, 90, Mats.Unlit("fx_bloodsplat", new Color(0.65f, 0.04f, 0.04f, 0.95f), Mats.BloodSplat, Mats.BlendKind.Alpha, false, -40), g.point, g.normal, Random.Range(0.05f, 0.12f), null);
        }

        /// <summary>A pool of blood that slowly spreads under a corpse.</summary>
        public static void BloodPool(Vector3 pos)
        {
            if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var g, 2f, ~0, QueryTriggerInteraction.Ignore)) return;
            var go = new GameObject("BloodPool");
            go.transform.SetParent(RagoGame.I != null && RagoGame.I.MapRoot != null ? RagoGame.I.MapRoot : I.transform, true);
            go.transform.position = g.point + g.normal * 0.006f;
            go.transform.rotation = Quaternion.LookRotation(-g.normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
            go.AddComponent<MeshFilter>().sharedMesh = I._quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Unlit("fx_pool", new Color(0.42f, 0.02f, 0.02f, 0.97f), Mats.BloodSplat, Mats.BlendKind.Alpha, false, -45);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * 0.05f;
            I._pools.Add(new Pool { t = go.transform, size = 0.05f, target = Random.Range(0.9f, 1.5f), grow = 0.12f });
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
