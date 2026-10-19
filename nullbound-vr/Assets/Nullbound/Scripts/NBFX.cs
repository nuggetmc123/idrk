using System.Collections;
using UnityEngine;

namespace Nullbound
{
    /// Cheap procedural effects made from primitives: work on Quest without any art assets.
    public class NBFX : MonoBehaviour
    {
        static NBFX s_Host;
        static NBFX Host
        {
            get
            {
                if (s_Host == null) s_Host = new GameObject("NB_FX").AddComponent<NBFX>();
                return s_Host;
            }
        }

        public static void Burst(Vector3 pos, Color c, float size = 1f, int bits = 8)
        {
            var flash = NBMat.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * 0.3f * size, NBMat.Glow(new Color(c.r, c.g, c.b, 0.8f)));
            Host.StartCoroutine(Grow(flash.transform, 0.3f * size, 1.4f * size, 0.18f));
            for (int i = 0; i < bits; i++)
            {
                var b = NBMat.Prim(PrimitiveType.Cube, null, pos, Vector3.one * 0.09f * size, NBMat.Glow(c));
                Host.StartCoroutine(Fly(b.transform, Random.onUnitSphere * Random.Range(3f, 7f) * size, 0.35f));
            }
        }

        public static void Ring(Vector3 pos, float radius, Color c, float time = 0.35f)
        {
            var g = NBMat.Prim(PrimitiveType.Cylinder, null, pos + Vector3.up * 0.05f, new Vector3(0.1f, 0.02f, 0.1f), NBMat.Glow(new Color(c.r, c.g, c.b, 0.55f)));
            Host.StartCoroutine(Spread(g.transform, radius * 2f, time));
        }

        public static void Pillar(Vector3 pos, float radius, float height, Color c, float time = 0.5f)
        {
            var g = NBMat.Prim(PrimitiveType.Cylinder, null, pos + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height * 0.5f, radius * 2f), NBMat.Glow(new Color(c.r, c.g, c.b, 0.5f)));
            Host.StartCoroutine(FadeOut(g, time));
        }

        public static GameObject Marker(Vector3 pos, float radius, Color c, float time)
        {
            var g = NBMat.Prim(PrimitiveType.Cylinder, null, pos + Vector3.up * 0.04f, new Vector3(radius * 2f, 0.01f, radius * 2f), NBMat.Glow(new Color(c.r, c.g, c.b, 0.25f)));
            var inner = NBMat.Prim(PrimitiveType.Cylinder, g.transform, Vector3.up * 0.5f, new Vector3(0.01f, 1f, 0.01f), NBMat.Glow(new Color(c.r, c.g, c.b, 0.5f)));
            Host.StartCoroutine(MarkerFill(g, inner.transform, time));
            return g;
        }

        public static void Beam(Vector3 from, Vector3 dir, float length, float width, Color c, float time)
        {
            var g = new GameObject("Beam");
            g.transform.position = from + dir * length * 0.5f;
            g.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
            var core = NBMat.Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(width * 0.5f, length * 0.5f, width * 0.5f), NBMat.Glow(new Color(1, 1, 1, 0.9f)));
            var outer = NBMat.Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(width * 1.3f, length * 0.5f, width * 1.3f), NBMat.Glow(new Color(c.r, c.g, c.b, 0.45f)));
            Host.StartCoroutine(BeamLife(g, core.transform, outer.transform, time));
        }

        public static void Trail(Vector3 from, Vector3 to, Color c, float width = 0.6f)
        {
            var d = to - from;
            if (d.sqrMagnitude < 0.01f) return;
            var g = NBMat.Prim(PrimitiveType.Cube, null, (from + to) * 0.5f, new Vector3(width, width, d.magnitude), NBMat.Glow(new Color(c.r, c.g, c.b, 0.35f)));
            g.transform.rotation = Quaternion.LookRotation(d);
            Host.StartCoroutine(FadeOut(g, 0.3f));
        }

        public static void DamageNumber(Vector3 pos, float dmg, Color c, bool blocked)
        {
            var g = new GameObject("Dmg");
            g.transform.position = pos + Random.insideUnitSphere * 0.2f;
            var t = NBMat.Text(g.transform, blocked ? "BLOCK" : Mathf.RoundToInt(dmg).ToString(), Vector3.zero, blocked ? 0.16f : 0.2f + dmg * 0.006f, blocked ? Color.gray : c);
            Host.StartCoroutine(FloatText(g.transform, t));
        }

        public static void Text3D(Vector3 pos, string s, Color c, float size, float time)
        {
            var g = new GameObject("FxText");
            g.transform.position = pos;
            var t = NBMat.Text(g.transform, s, Vector3.zero, size, c);
            Host.StartCoroutine(FloatText(g.transform, t, time));
        }

        // ---- coroutines ----
        static IEnumerator Grow(Transform t, float a, float b, float time)
        {
            var r = t.GetComponent<Renderer>(); var c0 = r.material.color;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                t.localScale = Vector3.one * Mathf.Lerp(a, b, k);
                r.material.color = new Color(c0.r, c0.g, c0.b, c0.a * (1 - k));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        static IEnumerator Fly(Transform t, Vector3 v, float time)
        {
            var s0 = t.localScale;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                v += Physics.gravity * Time.deltaTime * 0.6f;
                t.position += v * Time.deltaTime;
                t.localScale = s0 * (1 - k);
                t.Rotate(400 * Time.deltaTime, 300 * Time.deltaTime, 0);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        static IEnumerator Spread(Transform t, float size, float time)
        {
            var r = t.GetComponent<Renderer>(); var c0 = r.material.color;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                float s = Mathf.Lerp(0.2f, size, 1 - (1 - k) * (1 - k));
                t.localScale = new Vector3(s, 0.02f, s);
                r.material.color = new Color(c0.r, c0.g, c0.b, c0.a * (1 - k));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        static IEnumerator FadeOut(GameObject g, float time)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            var c0 = new Color[rs.Length];
            for (int i = 0; i < rs.Length; i++) c0[i] = rs[i].material.color;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (g == null) yield break;
                for (int i = 0; i < rs.Length; i++) rs[i].material.color = new Color(c0[i].r, c0[i].g, c0[i].b, c0[i].a * (1 - k));
                yield return null;
            }
            if (g != null) Destroy(g);
        }

        static IEnumerator MarkerFill(GameObject g, Transform inner, float time)
        {
            float full = g.transform.localScale.x;
            for (float k = 0; k < 1f; k += Time.deltaTime / Mathf.Max(0.05f, time))
            {
                if (g == null) yield break;
                inner.localScale = new Vector3(k, 1f, k);
                yield return null;
            }
            if (g != null) Destroy(g);
        }

        static IEnumerator BeamLife(GameObject g, Transform core, Transform outer, float time)
        {
            float t = 0;
            var s1 = core.localScale; var s2 = outer.localScale;
            while (t < time)
            {
                if (g == null) yield break;
                t += Time.deltaTime;
                float wob = 1f + Mathf.Sin(t * 50f) * 0.12f;
                float fade = t > time - 0.2f ? (time - t) / 0.2f : 1f;
                core.localScale = new Vector3(s1.x * wob * fade, s1.y, s1.z * wob * fade);
                outer.localScale = new Vector3(s2.x * wob * fade, s2.y, s2.z * wob * fade);
                yield return null;
            }
            if (g != null) Destroy(g);
        }

        static IEnumerator FloatText(Transform t, TextMesh tm, float time = 0.8f)
        {
            var c0 = tm.color;
            for (float k = 0; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                t.position += Vector3.up * Time.deltaTime * 0.9f;
                var cam = NBRig.Instance != null ? NBRig.Instance.Head : null;
                if (cam != null) t.rotation = Quaternion.LookRotation(t.position - cam.position);
                tm.color = new Color(c0.r, c0.g, c0.b, 1 - k * k);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
