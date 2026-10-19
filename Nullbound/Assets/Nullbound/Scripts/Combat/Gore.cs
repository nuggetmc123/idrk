using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// What flies off a fighter when they get hit (Settings > Gore). GORE ON: blood sprays from the hit in droplets,
    /// splats on the ground and pools under a knocked-out fighter: realistic-ish (dark, wet, drips with gravity) but
    /// no wounds or body parts. GORE OFF (the default): the same moments burst into colourful confetti instead.
    /// Everything is local, cheap on Quest, and cleans itself up.
    /// </summary>
    public static class Gore
    {
        static Material blood, bloodDark, bloodMist;
        static Material[] confetti;
        static readonly Queue<GameObject> splats = new Queue<GameObject>();
        const int MaxSplats = 40;

        static void Mats()
        {
            if (blood != null) return;
            blood = NBUI.Mat(new Color(0.45f, 0.02f, 0.03f), 0.85f);
            bloodDark = NBUI.Mat(new Color(0.24f, 0.01f, 0.02f), 0.9f);
            bloodMist = NBUI.Mat(new Color(0.5f, 0.05f, 0.05f), 0.3f);
            var colors = new[]
            {
                new Color(1f, 0.25f, 0.35f), new Color(1f, 0.8f, 0.15f), new Color(0.3f, 0.85f, 1f), new Color(0.45f, 1f, 0.4f),
                new Color(0.75f, 0.4f, 1f), new Color(1f, 0.55f, 0.15f), Color.white,
            };
            confetti = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) confetti[i] = NBUI.Glow(colors[i]);
        }

        /// <summary>A hit landing at pos, pushing outward along dir. Bigger hits throw more.</summary>
        public static void Hit(Vector3 pos, Vector3 dir, float damage)
        {
            Mats();
            float k = Mathf.Clamp(damage / 12f, 0.4f, 2.5f);
            if (GameSettings.Gore) BloodSpray(pos, dir, k, false);
            else ConfettiBurst(pos, dir, k, false);
        }

        /// <summary>A knock-out: a big burst, and (with gore) a pool spreading under them.</summary>
        public static void KnockOut(Vector3 pos, Vector3 feet)
        {
            Mats();
            if (GameSettings.Gore)
            {
                BloodSpray(pos, Vector3.up, 2.5f, true);
                Pool(feet, 1.1f, 2.5f, 25f);
            }
            else ConfettiBurst(pos, Vector3.up, 3f, true);
        }

        static void BloodSpray(Vector3 pos, Vector3 dir, float k, bool big)
        {
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
            // A quick mist at the hit.
            var mist = Piece(PrimitiveType.Sphere, pos, Quaternion.identity, Vector3.one * 0.06f * k, Vector3.one * 0.35f * k, bloodMist, 0.18f);
            mist.shrinkAtEnd = true;
            // Droplets arcing out and falling.
            int drops = Mathf.RoundToInt((big ? 26 : 8) * Mathf.Min(k, 2f));
            for (int i = 0; i < drops; i++)
            {
                Vector3 d = (dir + Random.insideUnitSphere * 0.8f + Vector3.up * 0.3f).normalized;
                float size = Random.Range(0.012f, 0.03f) * (big ? 1.4f : 1f);
                var drop = Piece(PrimitiveType.Sphere, pos, Quaternion.LookRotation(d), new Vector3(size, size, size * 2.5f), new Vector3(size, size, size * 1.2f),
                    Random.value < 0.3f ? bloodDark : blood, Random.Range(0.5f, 0.9f));
                drop.velocity = d * Random.Range(2f, 5.5f) * (big ? 1.2f : 1f);
                drop.gravity = true;
            }
            // Splats on the ground where it lands.
            int splatCount = big ? 6 : Mathf.Clamp(Mathf.RoundToInt(k * 2f), 1, 4);
            for (int i = 0; i < splatCount; i++)
            {
                Vector3 flat = new Vector3(dir.x, 0f, dir.z) + Random.insideUnitSphere * 0.7f;
                flat.y = 0f;
                Vector3 at = pos + flat.normalized * Random.Range(0.2f, big ? 1.6f : 1.1f);
                float r = Random.Range(0.08f, 0.22f) * (big ? 1.5f : Mathf.Sqrt(k));
                if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    Splat(hit.point, hit.normal, r, 18f);
            }
        }

        /// <summary>A flat, irregular wet stain on the ground (a few overlapping blobs) that fades after a while.</summary>
        static void Splat(Vector3 point, Vector3 normal, float radius, float seconds)
        {
            var root = new GameObject("BloodSplat").transform;
            root.position = point + normal * 0.006f;
            root.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            NBUI.Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, 0.002f, radius * 1.6f), blood);
            for (int i = 0; i < 4; i++)
            {
                Vector3 off = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * radius * 0.9f;
                float s = radius * Random.Range(0.25f, 0.6f);
                NBUI.Part(root, PrimitiveType.Cylinder, off + Vector3.up * 0.0005f * (i + 1), new Vector3(s, 0.002f, s), i % 2 == 0 ? bloodDark : blood);
            }
            Keep(root.gameObject, seconds);
        }

        /// <summary>A pool that spreads out under a body.</summary>
        static void Pool(Vector3 feet, float radius, float spreadSeconds, float seconds)
        {
            if (!Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return;
            var root = new GameObject("BloodPool").transform;
            root.position = hit.point + hit.normal * 0.008f;
            root.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            NBUI.Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, 0.002f, radius * 1.7f), bloodDark);
            NBUI.Part(root, PrimitiveType.Cylinder, new Vector3(0.15f, 0.001f, 0.1f), new Vector3(radius * 1.4f, 0.002f, radius * 1.2f), blood);
            var life = root.gameObject.AddComponent<FxLife>();
            life.life = spreadSeconds;
            life.startScale = Vector3.one * 0.15f;
            life.endScale = Vector3.one;
            Fx.Delayed(spreadSeconds, () => { if (root != null) { Object.Destroy(life); Keep(root.gameObject, seconds); } });
        }

        static void ConfettiBurst(Vector3 pos, Vector3 dir, float k, bool big)
        {
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
            int count = Mathf.RoundToInt((big ? 60 : 14) * Mathf.Min(k, 2f));
            for (int i = 0; i < count; i++)
            {
                Vector3 d = (dir + Random.insideUnitSphere * (big ? 1.2f : 0.9f) + Vector3.up * 0.6f).normalized;
                float size = Random.Range(0.025f, 0.045f);
                var bit = Piece(PrimitiveType.Cube, pos, Random.rotation, new Vector3(size, 0.004f, size * 1.6f), new Vector3(size, 0.004f, size * 1.6f),
                    confetti[Random.Range(0, confetti.Length)], Random.Range(1.2f, big ? 2.6f : 1.8f));
                bit.velocity = d * Random.Range(2.5f, big ? 7f : 5f);
                bit.gravity = true;
                bit.spin = Random.insideUnitSphere * 720f;
                bit.drag = 2.2f; // flutters down slowly
                bit.shrinkAtEnd = true;
            }
            if (big)
                for (int i = 0; i < 6; i++)
                {
                    // Streamers.
                    Vector3 d = (Vector3.up + Random.insideUnitSphere * 0.8f).normalized;
                    var s = Piece(PrimitiveType.Cube, pos, Random.rotation, new Vector3(0.02f, 0.004f, 0.35f), new Vector3(0.02f, 0.004f, 0.35f), confetti[i % confetti.Length], 2.2f);
                    s.velocity = d * Random.Range(4f, 7f);
                    s.gravity = true;
                    s.spin = Random.insideUnitSphere * 400f;
                    s.drag = 1.5f;
                    s.shrinkAtEnd = true;
                }
        }

        static FxLife Piece(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 from, Vector3 to, Material mat, float life)
        {
            var root = new GameObject("GoreFx").transform;
            root.SetPositionAndRotation(pos, rot);
            NBUI.Part(root, type, Vector3.zero, Vector3.one, mat);
            var l = root.gameObject.AddComponent<FxLife>();
            l.life = life;
            l.startScale = from;
            l.endScale = to;
            root.localScale = from;
            return l;
        }

        /// <summary>Stains last a while, then shrink away; only the newest few dozen are kept.</summary>
        static void Keep(GameObject go, float seconds)
        {
            var life = go.AddComponent<FxLife>();
            life.life = seconds;
            life.useTransformMotion = false;
            life.shrinkAtEnd = true;
            life.startScale = go.transform.localScale;
            splats.Enqueue(go);
            while (splats.Count > MaxSplats)
            {
                var old = splats.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }
    }
}
