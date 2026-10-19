using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Anime-style hit effects made from simple glowing shapes (cheap on Quest): impact flashes, shockwave rings,
    /// damage numbers, speed lines, afterimages, cracks in the ground and flying debris. Everything is local and
    /// cleans itself up.
    /// </summary>
    public static class Fx
    {
        static readonly Color White = new Color(1f, 1f, 0.95f);

        /// <summary>A punch landing: a bright burst, a ring and sparks flying along the punch.</summary>
        public static void Impact(Vector3 pos, Vector3 dir, Color color, bool heavy, bool blocked)
        {
            float s = heavy ? 1.6f : 1f;
            var core = NBUI.Glow(blocked ? new Color(0.6f, 0.85f, 1f) : White);
            var glow = NBUI.Glow(blocked ? new Color(0.3f, 0.6f, 1f) : color);
            Spawn(PrimitiveType.Sphere, pos, Quaternion.identity, Vector3.one * 0.12f * s, Vector3.one * 0.5f * s, core, 0.09f);
            Spawn(PrimitiveType.Sphere, pos, Quaternion.identity, Vector3.one * 0.2f * s, Vector3.one * 0.75f * s, glow, 0.14f);
            var ringRot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            Spawn(PrimitiveType.Cylinder, pos, ringRot, new Vector3(0.2f, 0.005f, 0.2f) * s, new Vector3(1.2f, 0.005f, 1.2f) * s, glow, 0.18f);
            int sparks = heavy ? 9 : 5;
            for (int i = 0; i < sparks; i++)
            {
                Vector3 d = (dir.normalized + Random.insideUnitSphere * 0.9f).normalized;
                var spark = Spawn(PrimitiveType.Cube, pos, Quaternion.LookRotation(d), new Vector3(0.02f, 0.02f, 0.25f) * s, new Vector3(0.005f, 0.005f, 0.05f), glow, 0.22f);
                spark.velocity = d * Random.Range(4f, 9f) * s;
            }
        }

        /// <summary>A ring of force rolling out across the ground, plus a dome flash.</summary>
        public static void Shockwave(Vector3 pos, float radius, Color color)
        {
            var glow = NBUI.Glow(color);
            Spawn(PrimitiveType.Cylinder, pos + Vector3.up * 0.1f, Quaternion.identity, new Vector3(0.5f, 0.03f, 0.5f), new Vector3(radius * 2f, 0.03f, radius * 2f), glow, 0.35f);
            Spawn(PrimitiveType.Cylinder, pos + Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.3f, 0.4f, 0.3f), new Vector3(radius * 1.6f, 0.02f, radius * 1.6f), NBUI.Glow(White), 0.25f);
            Spawn(PrimitiveType.Sphere, pos, Quaternion.identity, Vector3.one * 0.5f, Vector3.one * radius * 1.2f, NBUI.Glow(color * 0.6f), 0.2f);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0.6f, Mathf.Sin(a));
                var rock = Spawn(PrimitiveType.Cube, pos + d * 0.4f, Random.rotation, Vector3.one * 0.25f, Vector3.one * 0.08f, NBUI.Mat(new Color(0.35f, 0.32f, 0.3f), 0.1f), 0.9f);
                rock.velocity = d * Random.Range(4f, 8f);
                rock.gravity = true;
                rock.spin = Random.insideUnitSphere * 600f;
            }
            Crater(pos, radius * 0.55f);
        }

        /// <summary>Dark cracks in the floor that fade away after a while.</summary>
        public static void Crater(Vector3 pos, float radius)
        {
            var dark = NBUI.Mat(new Color(0.08f, 0.07f, 0.07f), 0.1f);
            var root = new GameObject("Crater").transform;
            root.position = pos + Vector3.up * 0.02f;
            NBUI.Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius, 0.005f, radius), dark);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 360f / 7f + Random.Range(-20f, 20f);
                float len = radius * Random.Range(1f, 1.8f);
                NBUI.Part(root, PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.004f, len * 0.5f),
                    new Vector3(0.08f, 0.005f, len), dark, new Vector3(0f, a, 0f));
            }
            var life = root.gameObject.AddComponent<FxLife>();
            life.life = 8f;
            life.shrinkAtEnd = true;
            life.startScale = life.endScale = Vector3.one;
        }

        /// <summary>A number (or word) that pops up and floats away, facing you.</summary>
        public static void Popup(Vector3 pos, string text, Color color, float size = 1f)
        {
            var go = new GameObject("Popup");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 2.2f * size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
            go.AddComponent<FaceCamera>();
            var life = go.AddComponent<FxLife>();
            life.life = 0.8f;
            life.velocity = Vector3.up * 1.2f + Random.insideUnitSphere * 0.3f;
            life.startScale = Vector3.one * 0.6f;
            life.endScale = Vector3.one * 1.1f;
            life.punch = true;
        }

        /// <summary>A glowing ghost of a fighter left behind (Flash Step, dashes).</summary>
        public static void Afterimage(Vector3 feet, Vector3 head, Color color)
        {
            var glow = NBUI.Glow(color * 0.8f);
            var root = new GameObject("Afterimage").transform;
            root.position = feet;
            float h = Mathf.Max(0.8f, head.y - feet.y);
            NBUI.Part(root, PrimitiveType.Capsule, new Vector3(0f, h * 0.55f, 0f), new Vector3(0.45f, h * 0.45f, 0.3f), glow);
            NBUI.Part(root, PrimitiveType.Sphere, new Vector3(0f, h, 0f), Vector3.one * 0.3f, glow);
            var life = root.gameObject.AddComponent<FxLife>();
            life.life = 0.35f;
            life.startScale = Vector3.one;
            life.endScale = new Vector3(0.2f, 1.1f, 0.2f);
        }

        /// <summary>A streak of lightning between two points (Speedster).</summary>
        public static void Streak(Vector3 from, Vector3 to, Color color)
        {
            var glow = NBUI.Glow(color);
            Vector3 d = to - from;
            if (d.magnitude < 0.1f) return;
            int n = 5;
            Vector3 prev = from;
            for (int i = 1; i <= n; i++)
            {
                Vector3 next = Vector3.Lerp(from, to, i / (float)n) + (i < n ? Random.insideUnitSphere * 0.25f : Vector3.zero);
                var seg = new GameObject("Streak").transform;
                NBUI.Part(seg, PrimitiveType.Cube, Vector3.zero, Vector3.one, glow);
                NBUI.Span(seg.GetChild(0), prev, next, 0.06f, false);
                var life = seg.gameObject.AddComponent<FxLife>();
                life.life = 0.25f;
                life.startScale = life.endScale = Vector3.one;
                life.shrinkAtEnd = true;
                prev = next;
            }
        }

        /// <summary>Chunks flying out of something that broke.</summary>
        public static void Debris(Vector3 pos, Vector3 size, Material mat, Vector3 push, int count = 10)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.Scale(new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f), size);
                float chunk = Mathf.Clamp(size.magnitude * 0.18f, 0.15f, 0.7f) * Random.Range(0.6f, 1.2f);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Debris";
                go.transform.position = pos + offset;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * chunk;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 2f;
                rb.linearVelocity = push * Random.Range(0.4f, 1f) + Random.insideUnitSphere * 3f + Vector3.up * 2f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                var life = go.AddComponent<FxLife>();
                life.life = Random.Range(2.5f, 4f);
                life.startScale = life.endScale = go.transform.localScale;
                life.shrinkAtEnd = true;
                life.useTransformMotion = false;
            }
        }

        /// <summary>A glowing ring on the ground showing where something is about to land.</summary>
        public static void Telegraph(Vector3 pos, float radius, Color color, float seconds)
        {
            var glow = NBUI.Glow(color);
            var ring = Spawn(PrimitiveType.Cylinder, pos + Vector3.up * 0.05f, Quaternion.identity, new Vector3(radius * 2f, 0.01f, radius * 2f), new Vector3(radius * 2f, 0.01f, radius * 2f), glow, seconds);
            ring.shrinkAtEnd = false;
            Spawn(PrimitiveType.Cylinder, pos + Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.1f, 0.012f, 0.1f), new Vector3(radius * 2f, 0.012f, radius * 2f), NBUI.Glow(color * 0.5f), seconds);
        }

        /// <summary>Run something after a delay (for effects that land a moment later).</summary>
        public static void Delayed(float seconds, System.Action action) => FxHost.Get().Run(seconds, action);

        /// <summary>A glowing rock falling from the sky onto a point, then the action.</summary>
        public static void Falling(Vector3 from, Vector3 to, float seconds, Color color, System.Action landed)
        {
            var rock = Spawn(PrimitiveType.Sphere, from, Quaternion.identity, Vector3.one * 1.4f, Vector3.one * 1.4f, NBUI.Glow(color), seconds);
            rock.velocity = (to - from) / seconds;
            NBUI.Part(rock.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.5f, NBUI.Glow(new Color(1f, 0.3f, 0.05f) * 0.5f));
            Delayed(seconds, landed);
        }

        /// <summary>A fan of force rolling forward (Shockwave special).</summary>
        public static void Cone(Vector3 pos, Vector3 dir, float range, Color color)
        {
            var glow = NBUI.Glow(color);
            for (int i = -3; i <= 3; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 18f, 0f) * dir;
                var wave = Spawn(PrimitiveType.Cube, pos, Quaternion.LookRotation(d), new Vector3(0.6f, 0.6f, 0.15f), new Vector3(1.6f, 1.2f, 0.05f), glow, 0.35f);
                wave.velocity = d * range / 0.35f;
            }
        }

        /// <summary>A spinning ring of wind round a fighter (Whirlwind).</summary>
        public static void Spin(Transform around, float seconds, Color color)
        {
            var go = new GameObject("Spin").transform;
            go.SetParent(around, false);
            go.localPosition = Vector3.up * 1f;
            var glow = NBUI.Glow(color);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                NBUI.Part(go, PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 1.6f), new Vector3(0.9f, 1.4f, 0.04f), glow, new Vector3(0f, a + 30f, 0f));
            }
            var life = go.gameObject.AddComponent<FxLife>();
            life.life = seconds;
            life.startScale = life.endScale = Vector3.one;
            life.spin = new Vector3(0f, 900f, 0f);
            life.shrinkAtEnd = true;
            life.velocity = Vector3.zero;
            life.followParent = true;
        }

        /// <summary>An energy beam between two points.</summary>
        public static void Beam(Vector3 from, Vector3 to, Color color)
        {
            var root = new GameObject("Beam").transform;
            var core = NBUI.Part(root, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, NBUI.Glow(Color.white));
            var outer = NBUI.Part(root, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, NBUI.Glow(color));
            NBUI.Span(core, from, to, 0.18f, false);
            NBUI.Span(outer, from, to, 0.45f, false);
            var life = root.gameObject.AddComponent<FxLife>();
            life.life = 0.35f;
            life.startScale = life.endScale = Vector3.one;
            life.shrinkAtEnd = true;
            Impact(to, from - to, color, true, false);
        }

        /// <summary>Rings rising up round a fighter (healing).</summary>
        public static void Rings(Transform around, Color color, float seconds)
        {
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("HealRing").transform;
                go.SetParent(around, false);
                go.localPosition = Vector3.up * (0.2f + i * 0.4f);
                NBUI.Part(go, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.1f, 0.01f, 1.1f), NBUI.Glow(color));
                var life = go.gameObject.AddComponent<FxLife>();
                life.life = seconds;
                life.startScale = Vector3.one * 0.6f;
                life.endScale = Vector3.one * 1.3f;
                life.velocity = Vector3.up * 0.8f;
                life.followParent = true;
            }
        }

        /// <summary>A see-through-looking glowing bubble round a fighter (Barrier).</summary>
        public static void Bubble(Transform around, Color color, float seconds)
        {
            var go = new GameObject("Bubble").transform;
            go.SetParent(around, false);
            go.localPosition = Vector3.up * 1f;
            // A cage of glowing rings reads as a shield without needing a transparent shader.
            for (int i = 0; i < 3; i++)
                NBUI.Part(go, PrimitiveType.Cylinder, Vector3.zero, new Vector3(2.1f, 0.01f, 2.1f), NBUI.Glow(color), new Vector3(i * 60f, 0f, 90f));
            NBUI.Part(go, PrimitiveType.Cylinder, Vector3.zero, new Vector3(2.1f, 0.01f, 2.1f), NBUI.Glow(color));
            var life = go.gameObject.AddComponent<FxLife>();
            life.life = seconds;
            life.startScale = life.endScale = Vector3.one;
            life.spin = new Vector3(0f, 90f, 0f);
            life.shrinkAtEnd = true;
            life.followParent = true;
        }

        static FxLife Spawn(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 from, Vector3 to, Material mat, float life)
        {
            var root = new GameObject("Fx").transform;
            root.position = pos;
            root.rotation = rot;
            NBUI.Part(root, type, Vector3.zero, Vector3.one, mat);
            var l = root.gameObject.AddComponent<FxLife>();
            l.life = life;
            l.startScale = from;
            l.endScale = to;
            root.localScale = from;
            return l;
        }
    }

    /// <summary>Grows / moves an effect over its life, then removes it.</summary>
    public class FxLife : MonoBehaviour
    {
        public float life = 0.3f;
        public Vector3 startScale = Vector3.one, endScale = Vector3.one;
        public Vector3 velocity, spin;
        public bool gravity, shrinkAtEnd, punch, useTransformMotion = true, followParent;
        /// <summary>Air drag (confetti flutters down slowly).</summary>
        public float drag;
        float age;

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            if (useTransformMotion)
            {
                if (gravity) velocity += Physics.gravity * Time.deltaTime;
                if (drag > 0f) velocity *= Mathf.Exp(-drag * Time.deltaTime);
                if (followParent) transform.localPosition += velocity * Time.deltaTime; else transform.position += velocity * Time.deltaTime;
                if (spin != Vector3.zero) transform.Rotate(spin * Time.deltaTime);
                float k = punch ? 1f - Mathf.Pow(1f - Mathf.Min(1f, t * 4f), 3f) : 1f - (1f - t) * (1f - t);
                Vector3 s = Vector3.Lerp(startScale, endScale, k);
                if (shrinkAtEnd || punch) s *= t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f;
                transform.localScale = s;
            }
            else if (shrinkAtEnd && t > 0.75f) transform.localScale = startScale * (1f - (t - 0.75f) / 0.25f);
            if (age >= life) Destroy(gameObject);
        }
    }

    /// <summary>Runs delayed effects (one object for the whole game).</summary>
    public class FxHost : MonoBehaviour
    {
        static FxHost instance;

        public static FxHost Get()
        {
            if (instance != null) return instance;
            instance = new GameObject("FxHost").AddComponent<FxHost>();
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        public void Run(float seconds, System.Action action) => StartCoroutine(After(seconds, action));

        static System.Collections.IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action?.Invoke();
        }
    }

    /// <summary>Turns text toward your eyes.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var rig = VRRig.Instance;
            var cam = rig != null && rig.head != null ? rig.head : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;
            Vector3 away = transform.position - cam.position;
            if (away.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
