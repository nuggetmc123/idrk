using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Pooled visual effects: muzzle flashes, tracers, blood, sparks, bullet
    /// holes, blood splats and ejected shells.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx I { get; private set; }

        ParticleSystem sparks, smoke, blood, bloodMist;
        Light flashLight;
        float flashLightUntil;

        class Timed { public GameObject go; public float born, life; public Material mat; public Color baseColor; public LineRenderer line; }
        readonly List<Timed> live = new List<Timed>();
        readonly Queue<GameObject> flashes = new Queue<GameObject>();
        readonly Queue<LineRenderer> tracers = new Queue<LineRenderer>();
        readonly Queue<GameObject> holes = new Queue<GameObject>();
        readonly Queue<GameObject> splats = new Queue<GameObject>();
        readonly Queue<Rigidbody> shells = new Queue<Rigidbody>();

        void Awake()
        {
            I = this;
            sparks = MakeSystem("sparks", true, 0.35f, 0.4f);
            smoke = MakeSystem("smoke", false, 0.9f, -0.05f);
            blood = MakeSystem("blood", false, 0.9f, 1.2f);
            bloodMist = MakeSystem("bloodMist", false, 0.35f, 0.2f);

            var lg = new GameObject("flashLight");
            lg.transform.SetParent(transform, false);
            flashLight = lg.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.75f, 0.4f);
            flashLight.range = 6f;
            flashLight.intensity = 0f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;

            for (int i = 0; i < 12; i++) flashes.Enqueue(MakeFlash());
            for (int i = 0; i < 24; i++) tracers.Enqueue(MakeTracer());
            for (int i = 0; i < 60; i++) holes.Enqueue(MakeDecal("hole", new Color(0.05f, 0.05f, 0.05f, 0.85f), 0.035f));
            for (int i = 0; i < 40; i++) splats.Enqueue(MakeDecal("splat", new Color(0.45f, 0.02f, 0.03f, 0.9f), 0.35f));
            for (int i = 0; i < 24; i++) shells.Enqueue(MakeShell());
        }

        ParticleSystem MakeSystem(string n, bool additive, float life, float gravity)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Prims.Particle(additive, Color.white);
            ps.Play();
            return ps;
        }

        GameObject MakeFlash()
        {
            var go = new GameObject("flash");
            go.transform.SetParent(transform, false);
            var mat = Prims.Particle(true, new Color(1f, 0.7f, 0.3f, 1f));
            for (int i = 0; i < 3; i++)
            {
                var q = Prims.Make(PrimitiveType.Quad, go.transform, new Vector3(0, 0, 0.06f), new Vector3(0.09f, 0.22f, 1f), mat, false,
                                   Quaternion.Euler(90, i * 60f, 0));
                q.name = "petal";
            }
            var core = Prims.Make(PrimitiveType.Quad, go.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 1f), mat, false);
            core.name = "core";
            go.SetActive(false);
            return go;
        }

        LineRenderer MakeTracer()
        {
            var go = new GameObject("tracer");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.012f;
            lr.endWidth = 0.004f;
            lr.sharedMaterial = Prims.Particle(true, new Color(1f, 0.85f, 0.5f, 1f));
            lr.enabled = false;
            return lr;
        }

        GameObject MakeDecal(string n, Color c, float size)
        {
            var go = Prims.Make(PrimitiveType.Quad, transform, Vector3.zero, Vector3.one * size, null, false);
            go.name = n;
            var m = Prims.Sprite(c);
            m.mainTexture = Prims.SoftDot;
            m.renderQueue = 2501;   // first in the see-through queue, under the other effects
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.SetActive(false);
            return go;
        }

        Rigidbody MakeShell()
        {
            var go = Prims.Cyl(transform, Vector3.zero, 0.011f, 0.03f, Prims.Lit(new Color(0.85f, 0.65f, 0.2f), 0.8f, 0.9f), Vector3.up, true);
            go.name = "shell";
            go.layer = Layers.IgnoreRaycast;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.interpolation = RigidbodyInterpolation.None;
            go.SetActive(false);
            return rb;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var e = live[i];
                float k = (t - e.born) / e.life;
                if (e.go == null) { live.RemoveAt(i); continue; }
                if (k >= 1f)
                {
                    e.go.SetActive(false);
                    if (e.line != null) e.line.enabled = false;
                    live.RemoveAt(i);
                    continue;
                }
                if (e.line != null)
                {
                    var c = new Color(1f, 0.85f, 0.5f, 1f - k);
                    e.line.startColor = c;
                    e.line.endColor = new Color(c.r, c.g, c.b, c.a * 0.3f);
                }
                else if (e.mat != null && k > 0.7f)
                {
                    var c = e.baseColor;
                    c.a *= 1f - (k - 0.7f) / 0.3f;
                    e.mat.color = c;
                }
            }
            if (flashLight.enabled && t > flashLightUntil) flashLight.enabled = false;
        }

        void Track(GameObject go, float life, Material mat = null, LineRenderer line = null)
        {
            // pooled objects get reused while still showing: forget their old timer
            for (int i = live.Count - 1; i >= 0; i--) if (live[i].go == go) live.RemoveAt(i);
            live.Add(new Timed { go = go, born = Time.time, life = life, mat = mat, baseColor = mat ? mat.color : Color.white, line = line });
        }

        T Next<T>(Queue<T> q) where T : Object
        {
            var item = q.Dequeue();
            q.Enqueue(item);
            return item;
        }

        /* ---------------- public effects ---------------- */

        public void MuzzleFlash(Transform muzzle, float size)
        {
            var go = Next(flashes);
            go.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation * Quaternion.Euler(0, 0, Random.Range(0, 360f)));
            go.transform.localScale = Vector3.one * size * Random.Range(0.8f, 1.2f);
            go.SetActive(true);
            Track(go, 0.045f);
            flashLight.transform.position = muzzle.position + muzzle.forward * 0.1f;
            flashLight.intensity = 2.2f * size;
            flashLight.enabled = true;
            flashLightUntil = Time.time + 0.04f;
            Emit(smoke, muzzle.position + muzzle.forward * 0.05f, muzzle.forward * 0.6f, 3, 0.4f, new Color(0.8f, 0.8f, 0.8f, 0.25f), 0.06f * size, 0.12f * size, 0.5f, 0.9f);
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            var lr = Next(tracers);
            // start a little in front of the gun so it doesn't fill the view
            var dir = to - from;
            float len = dir.magnitude;
            if (len < 0.5f) return;
            lr.SetPosition(0, from + dir / len * Mathf.Min(1.2f, len * 0.2f));
            lr.SetPosition(1, to);
            lr.enabled = true;
            lr.gameObject.SetActive(true);
            Track(lr.gameObject, 0.07f, null, lr);
        }

        public void Impact(Vector3 point, Vector3 normal)
        {
            Emit(sparks, point, normal * 2.5f, 8, 2.5f, new Color(1f, 0.8f, 0.4f, 1f), 0.015f, 0.03f, 0.15f, 0.35f);
            Emit(smoke, point, normal * 0.5f, 3, 0.3f, new Color(0.6f, 0.55f, 0.5f, 0.35f), 0.08f, 0.16f, 0.4f, 0.8f);
            var hole = Next(holes);
            hole.transform.SetPositionAndRotation(point + normal * 0.004f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f)));
            hole.SetActive(true);
            var m = hole.GetComponent<Renderer>().sharedMaterial;
            m.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            Track(hole, 25f, m);
        }

        /// <summary>Blood burst where a bullet hits a body, plus a splat on whatever is behind it.</summary>
        public void Blood(Vector3 point, Vector3 dir, float amount = 1f)
        {
            int n = Mathf.RoundToInt(18 * amount);
            Emit(blood, point, dir * 2.5f + Vector3.up * 0.8f, n, 2.2f, new Color(0.62f, 0.02f, 0.04f, 1f), 0.02f, 0.05f, 0.5f, 0.9f);
            Emit(bloodMist, point, dir * 0.8f, Mathf.Max(3, n / 3), 0.6f, new Color(0.7f, 0.05f, 0.06f, 0.5f), 0.1f, 0.22f, 0.2f, 0.35f);
            if (Physics.Raycast(point + dir * 0.3f, (dir + Vector3.down * 0.6f).normalized, out var hit, 4f, Layers.ShootMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Hitbox>() == null)
                Splat(hit.point, hit.normal, Random.Range(0.25f, 0.5f) * amount);
        }

        public void Splat(Vector3 point, Vector3 normal, float size)
        {
            var s = Next(splats);
            s.transform.SetPositionAndRotation(point + normal * 0.006f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f)));
            s.transform.localScale = new Vector3(size, size * Random.Range(0.6f, 1f), 1f);
            s.SetActive(true);
            var m = s.GetComponent<Renderer>().sharedMaterial;
            m.color = new Color(0.45f, 0.02f, 0.03f, 0.9f);
            Track(s, 30f, m);
        }

        public void Shell(Transform port, Vector3 velocity)
        {
            var rb = Next(shells);
            rb.gameObject.SetActive(true);
            rb.transform.SetPositionAndRotation(port.position, port.rotation * Quaternion.Euler(0, 0, 90));
            Rb.SetVelocity(rb, velocity + port.right * Random.Range(1.2f, 2f) + port.up * Random.Range(0.8f, 1.4f));
            rb.angularVelocity = Random.insideUnitSphere * 20f;
            Track(rb.gameObject, 3f);
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, int count, float spread, Color color, float minSize, float maxSize, float minLife, float maxLife)
        {
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos;
                p.velocity = vel + Random.insideUnitSphere * spread;
                p.startSize = Random.Range(minSize, maxSize);
                p.startLifetime = Random.Range(minLife, maxLife);
                p.startColor = color;
                ps.Emit(p, 1);
            }
        }
    }
}
