using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Particle effects built in code: sparks, debris, tyre smoke, dust, spray, smoke and fire attached to cars, and the
    /// explosion. One-shot effects share a few world-space systems (cheap on Quest); attached ones live on the car.
    /// </summary>
    public static class Fx
    {
        static ParticleSystem sparks, debris, explosionFire, explosionSmoke, dustPuff;
        static GameObject root;
        static readonly Dictionary<Transform, ParticleSystem> smokes = new Dictionary<Transform, ParticleSystem>();
        static readonly Dictionary<Transform, ParticleSystem> fires = new Dictionary<Transform, ParticleSystem>();
        static readonly Dictionary<Transform, Light> fireLights = new Dictionary<Transform, Light>();

        static Material M(System.Func<VXAssets, Material> pick, string name, Color c)
        {
            var A = VXAssets.I;
            var m = A != null ? pick(A) : null;
            if (m != null) return m;
            var sh = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit");
            return new Material(sh) { name = name, color = c };
        }

        static void Ensure()
        {
            if (root != null) return;
            root = new GameObject("Fx");
            Object.DontDestroyOnLoad(root);
            sparks = MakeSystem("Sparks", M(a => a.spark, "spark", new Color(1f, 0.7f, 0.3f)), 600, stretch: true);
            var main = sparks.main;
            main.gravityModifier = 1.2f;
            var col = sparks.collision;
            col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.35f; col.dampen = 0.4f; col.quality = ParticleSystemCollisionQuality.Low; col.collidesWith = Car.GroundMask;
            debris = MakeSystem("Debris", M(a => a.debris, "debris", new Color(0.1f, 0.1f, 0.1f)), 300, stretch: false, alphaBlend: true);
            var dm = debris.main; dm.gravityModifier = 1f;
            var dc = debris.collision; dc.enabled = true; dc.type = ParticleSystemCollisionType.World; dc.bounce = 0.3f; dc.dampen = 0.5f; dc.collidesWith = Car.GroundMask; dc.quality = ParticleSystemCollisionQuality.Low;
            var drot = debris.rotationOverLifetime; drot.enabled = true; drot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            explosionFire = MakeSystem("ExplosionFire", M(a => a.fire, "fire", new Color(1f, 0.55f, 0.2f)), 200, false);
            var ef = explosionFire.main; ef.gravityModifier = -0.15f;
            Fade(explosionFire, new Color(1f, 0.85f, 0.6f), new Color(1f, 0.35f, 0.08f), new Color(0.3f, 0.1f, 0.05f));
            Grow(explosionFire, 0.6f, 1.6f);
            explosionSmoke = MakeSystem("ExplosionSmoke", M(a => a.smoke, "smoke", new Color(0.2f, 0.2f, 0.2f)), 260, false, alphaBlend: true);
            var es = explosionSmoke.main; es.gravityModifier = -0.06f;
            Fade(explosionSmoke, new Color(0.18f, 0.16f, 0.15f, 0.9f), new Color(0.25f, 0.24f, 0.23f, 0.6f), new Color(0.4f, 0.4f, 0.4f, 0f));
            Grow(explosionSmoke, 0.5f, 3f);
            dustPuff = MakeSystem("Dust", M(a => a.dust, "dust", new Color(0.6f, 0.55f, 0.45f)), 400, false, alphaBlend: true);
            Grow(dustPuff, 0.4f, 2.2f);
            Fade(dustPuff, new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0f));
        }

        public static ParticleSystem MakeSystem(string name, Material mat, int max, bool stretch, bool alphaBlend = false, Transform parent = null, bool worldSpace = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root.transform, false);
            go.layer = Layers.Scenery;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.04f; r.lengthScale = 1f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        public static void Fade(ParticleSystem ps, Color a, Color b, Color c)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.4f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(a.a, 0f), new GradientAlphaKey(b.a, 0.4f), new GradientAlphaKey(c.a, 1f) });
            col.color = g;
        }

        public static void Grow(ParticleSystem ps, float from, float to)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        // ------------------------------------------------------------------ one-shots

        public static void Sparks(Vector3 p, Vector3 normal, float amount, Vector3 carVelocity)
        {
            Ensure();
            int n = Mathf.Clamp(Mathf.RoundToInt(6 + amount * 40f), 1, 60);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = p + Random.insideUnitSphere * 0.05f;
                Vector3 dir = (normal + Random.insideUnitSphere * 0.9f).normalized;
                ep.velocity = carVelocity * Random.Range(0.4f, 0.9f) + dir * Random.Range(1.5f, 7f);
                ep.startLifetime = Random.Range(0.25f, 0.9f);
                ep.startSize = Random.Range(0.012f, 0.03f);
                ep.startColor = Color.Lerp(new Color(1f, 0.85f, 0.55f), new Color(1f, 0.45f, 0.15f), Random.value);
                sparks.Emit(ep, 1);
            }
        }

        public static void DebrisBurst(Vector3 p, Vector3 normal, float amount, Material paint)
        {
            Ensure();
            int n = Mathf.Clamp(Mathf.RoundToInt(amount * 30f), 2, 40);
            var ep = new ParticleSystem.EmitParams();
            Color c = paint != null ? Color.Lerp(paint.color, Color.black, 0.2f) : new Color(0.1f, 0.1f, 0.1f);
            for (int i = 0; i < n; i++)
            {
                ep.position = p + Random.insideUnitSphere * 0.15f;
                ep.velocity = (normal * 2f + Random.insideUnitSphere * 4f + Vector3.up * 2.5f) * Random.Range(0.6f, 1.4f);
                ep.startLifetime = Random.Range(2.5f, 5f);
                ep.startSize = Random.Range(0.02f, 0.09f);
                ep.startColor = Random.value < 0.6f ? new Color(0.06f, 0.06f, 0.065f) : c;
                ep.rotation = Random.Range(0f, 360f);
                debris.Emit(ep, 1);
            }
        }

        public static void Dust(Vector3 p, Vector3 v, Color c, float amount)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams
            {
                position = p + Random.insideUnitSphere * 0.2f,
                velocity = v * 0.3f + Random.insideUnitSphere * 1.5f + Vector3.up * 0.8f,
                startLifetime = Random.Range(1.2f, 2.6f),
                startSize = Random.Range(0.6f, 1.2f) * (0.6f + amount),
                startColor = c,
            };
            dustPuff.Emit(ep, 1);
        }

        /// <summary>A believable fuel-cell explosion: fireball, rolling black smoke, a flash and a shower of parts.</summary>
        public static void Explosion(Vector3 p, float scale)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 70; i++)
            {
                ep.position = p + Random.insideUnitSphere * 0.8f * scale;
                ep.velocity = Random.insideUnitSphere * 7f * scale + Vector3.up * Random.Range(2f, 6f);
                ep.startLifetime = Random.Range(0.6f, 1.5f);
                ep.startSize = Random.Range(1.2f, 2.8f) * scale;
                ep.rotation = Random.Range(0f, 360f);
                ep.startColor = Color.white;
                explosionFire.Emit(ep, 1);
            }
            for (int i = 0; i < 60; i++)
            {
                ep.position = p + Random.insideUnitSphere * 1f * scale + Vector3.up * 0.5f;
                ep.velocity = Random.insideUnitSphere * 3f + Vector3.up * Random.Range(2f, 5f);
                ep.startLifetime = Random.Range(3f, 7f);
                ep.startSize = Random.Range(2f, 4f) * scale;
                ep.rotation = Random.Range(0f, 360f);
                ep.startColor = Color.white;
                explosionSmoke.Emit(ep, 1);
            }
            Sparks(p, Vector3.up, 1f, Vector3.zero);
            Sparks(p, Vector3.up, 1f, Vector3.zero);
            DebrisBurst(p, Vector3.up, 1f, null);
            // Flash.
            var lgo = new GameObject("ExplosionLight");
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.25f);
            l.range = 40f * scale;
            l.intensity = 8f;
            l.shadows = LightShadows.None;
            lgo.transform.position = p + Vector3.up;
            lgo.AddComponent<FadeLight>().duration = 1.4f;
            // A few real chunks that bounce and roll.
            for (int i = 0; i < 6; i++)
            {
                var chunk = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                chunk.layer = Layers.Debris;
                chunk.transform.position = p + Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f;
                chunk.transform.localScale = new Vector3(Random.Range(0.08f, 0.3f), Random.Range(0.03f, 0.1f), Random.Range(0.1f, 0.45f));
                chunk.transform.rotation = Random.rotation;
                var mr = chunk.GetComponent<MeshRenderer>();
                mr.sharedMaterial = VXAssets.Or(VXAssets.I != null ? VXAssets.I.carbon : null, "Carbon", new Color(0.06f, 0.06f, 0.06f), 0.4f);
                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 3f;
                rb.linearVelocity = Random.insideUnitSphere * 9f + Vector3.up * 7f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                Object.Destroy(chunk, 20f);
            }
        }

        // ------------------------------------------------------------------ attached smoke and fire

        public static void AttachSmoke(Transform car, Vector3 local, float amount)
        {
            Ensure();
            if (!smokes.TryGetValue(car, out var ps) || ps == null)
            {
                ps = MakeSystem("EngineSmoke", M(a => a.smoke, "smoke", new Color(0.3f, 0.3f, 0.3f)), 200, false, true, car);
                ps.transform.localPosition = local;
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.gravityModifier = -0.05f;
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f;
                Fade(ps, new Color(0.25f, 0.25f, 0.26f, 0.7f), new Color(0.3f, 0.3f, 0.3f, 0.45f), new Color(0.5f, 0.5f, 0.5f, 0f));
                Grow(ps, 0.6f, 3f);
                smokes[car] = ps;
            }
            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(2f, 40f, amount);
        }

        public static void AttachFire(Transform car, Vector3 local, float amount)
        {
            Ensure();
            if (!fires.TryGetValue(car, out var ps) || ps == null)
            {
                ps = MakeSystem("Fire", M(a => a.fire, "fire", new Color(1f, 0.5f, 0.2f)), 260, false, false, car);
                ps.transform.localPosition = local;
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.gravityModifier = -0.3f;
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.6f, 0.2f, 1.2f);
                Fade(ps, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.45f, 0.1f), new Color(0.4f, 0.1f, 0.03f));
                Grow(ps, 0.7f, 1.4f);
                fires[car] = ps;
                var lgo = new GameObject("FireLight");
                lgo.transform.SetParent(car, false);
                lgo.transform.localPosition = local + Vector3.up * 0.6f;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 9f; l.shadows = LightShadows.None;
                fireLights[car] = l;
                AttachSmoke(car, local + Vector3.up * 0.3f, 1f);
            }
            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(10f, 120f, amount);
            if (fireLights.TryGetValue(car, out var light) && light != null)
                light.intensity = (1.5f + amount * 3f) * (0.85f + Mathf.PerlinNoise(Time.time * 12f, 0f) * 0.3f);
        }

        public static void StopFire(Transform car)
        {
            if (fires.TryGetValue(car, out var ps) && ps != null) Object.Destroy(ps.gameObject);
            fires.Remove(car);
            if (fireLights.TryGetValue(car, out var l) && l != null) Object.Destroy(l.gameObject);
            fireLights.Remove(car);
        }

        public static void StopAttached(Transform car)
        {
            StopFire(car);
            if (smokes.TryGetValue(car, out var ps) && ps != null) Object.Destroy(ps.gameObject);
            smokes.Remove(car);
        }
    }

    public class FadeLight : MonoBehaviour
    {
        public float duration = 1f;
        float t, start;
        Light l;
        void Start() { l = GetComponent<Light>(); start = l.intensity; }
        void Update()
        {
            t += Time.deltaTime;
            l.intensity = start * Mathf.Pow(1f - Mathf.Clamp01(t / duration), 2f);
            if (t >= duration) Destroy(gameObject);
        }
    }
}
