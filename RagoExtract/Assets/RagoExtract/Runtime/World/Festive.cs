using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// CHRISTMAS (15 November - 10 January, by the date on your headset): snow on the ground, snow falling in raids,
    /// decorated Christmas trees with presents on every map, and the raiders are snowmen. Everyone in a raid sees the
    /// same (it only depends on the date and the map).
    /// </summary>
    public static class Festive
    {
        /// <summary>Master switch for the Christmas update: false = the normal game (no snow, trees, snowmen or presents)
        /// whatever the date.</summary>
        public static readonly bool Enabled = true;

        public static bool On
        {
            get
            {
                if (!Enabled) return false;
                var d = System.DateTime.Now;
                return d.Month == 12 || (d.Month == 11 && d.Day >= 15) || (d.Month == 1 && d.Day <= 10);
            }
        }

        public static Material Snow => Mats.Lit("w_snow", new Color(0.93f, 0.95f, 1f), 0f, 0.45f, Mats.Noise, null, 0.06f, 0f, true, 0.25f);

        // ---------------------------------------------------------------------------------- decorations
        /// <summary>Christmas trees (and presents) for this map: in the bunker's main hall, the Market, and beside every
        /// extract on the raid maps (on the right floor in the Underground).</summary>
        public static void Decorate(MapBase map)
        {
            if (!On || map == null) return;
            try
            {
                var t = map.transform;
                switch (map.Id)
                {
                    case "bunker": Tree(t, new Vector3(2.0f, 0, 1.8f), 1.0f, 11); break;
                    case "market": Tree(t, new Vector3(3.5f, 0, 1.5f), 1.6f, 12); break;
                    case "welcome": break;
                    default:
                        int i = 0;
                        foreach (var ex in map.Extracts)
                        {
                            if (ex == null) continue;
                            Vector3 e = ex.transform.position;
                            // a clear patch of ground a few metres from the extract
                            for (int k = 0; k < 8; k++)
                            {
                                Vector3 at = e + Quaternion.Euler(0, k * 45f + i * 17f, 0) * Vector3.forward * (k % 2 == 0 ? 5f : 7f);
                                float up = map.Id == "underground" ? 1.2f : 3f;   // under a ceiling: start low so we don't land on the floor above
                                if (!Physics.Raycast(at + Vector3.up * up, Vector3.down, out var h, up + 5f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;
                                if (h.normal.y < 0.85f) continue;
                                if (Physics.CheckBox(h.point + Vector3.up * 1.3f, new Vector3(0.8f, 1.1f, 0.8f), Quaternion.identity, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;
                                Tree(t, t.InverseTransformPoint(h.point), map.Id == "underground" ? 0.8f : 1.3f, 20 + i);
                                break;
                            }
                            i++;
                        }
                        break;
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[Rago] christmas decorations: " + ex.Message); }
        }

        /// <summary>A Christmas tree: stacked green tiers, baubles, warm fairy lights spiralling round it, a star on
        /// top and a few presents underneath.</summary>
        public static void Tree(Transform parent, Vector3 localPos, float scale, int seed)
        {
            var rng = new Rng(seed * 97 + 5);
            var go = new GameObject("ChristmasTree");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            Color green = new Color(0.08f, 0.32f, 0.14f), green2 = new Color(0.1f, 0.4f, 0.17f);
            var mb = new MB(2);
            mb.Sub(0).Cyl(Vector3.zero, new Vector3(0, 0.35f, 0), 0.08f, 0.07f, 10, new Color(0.3f, 0.18f, 0.1f));          // trunk
            mb.Sub(0).Cyl(Vector3.zero, new Vector3(0, 0.12f, 0), 0.22f, 0.2f, 12, new Color(0.55f, 0.1f, 0.08f));          // pot
            float[] y0 = { 0.3f, 0.75f, 1.15f, 1.5f }, r0 = { 0.8f, 0.64f, 0.48f, 0.32f };
            for (int i = 0; i < 4; i++) mb.Sub(0).Cyl(new Vector3(0, y0[i], 0), new Vector3(0, y0[i] + 0.65f, 0), r0[i], 0.02f, 16, i % 2 == 0 ? green : green2);
            // baubles on the tiers
            Color[] baub = { new Color(0.75f, 0.08f, 0.08f), new Color(0.85f, 0.65f, 0.15f), new Color(0.15f, 0.3f, 0.75f), new Color(0.75f, 0.75f, 0.78f) };
            for (int k = 0; k < 22; k++)
            {
                int tier = k % 4; float hgt = rng.Range(0.05f, 0.5f);
                float rr = Mathf.Lerp(r0[tier], 0.02f, hgt / 0.65f) + 0.02f;
                float a = rng.Range(0f, Mathf.PI * 2f);
                mb.Sub(0).Sphere(new Vector3(Mathf.Cos(a) * rr, y0[tier] + hgt, Mathf.Sin(a) * rr), Vector3.one * 0.045f, 8, baub[k % baub.Length]);
            }
            // fairy lights spiralling up, and the star (both glow)
            for (int k = 0; k < 40; k++)
            {
                float u = k / 40f, hgt = Mathf.Lerp(0.35f, 2.05f, u);
                float rr = Mathf.Lerp(0.78f, 0.08f, u) + 0.03f, a = u * Mathf.PI * 2f * 4.5f;
                mb.Sub(1).Sphere(new Vector3(Mathf.Cos(a) * rr, hgt, Mathf.Sin(a) * rr), Vector3.one * 0.025f, 6, Color.white);
            }
            mb.Sub(1).Sphere(new Vector3(0, 2.2f, 0), new Vector3(0.12f, 0.12f, 0.04f), 8, Color.white);
            mb.Build("Tree", go.transform, Mats.Lit("xmas_tree", Color.white, 0.1f, 0.4f, Mats.Noise, null, 0.15f, 0.05f),
                     Mats.Lit("xmas_glow", new Color(1f, 0.9f, 0.6f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(1.6f, 1.3f, 0.7f)));
            // presents round the base
            var pm = new MB();
            Color[] wrap = { new Color(0.75f, 0.1f, 0.1f), new Color(0.1f, 0.45f, 0.2f), new Color(0.2f, 0.3f, 0.75f), new Color(0.85f, 0.7f, 0.2f), new Color(0.8f, 0.8f, 0.82f) };
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f + rng.Range(-15f, 15f), d = rng.Range(0.75f, 0.95f);
                var sz = new Vector3(rng.Range(0.18f, 0.32f), rng.Range(0.14f, 0.26f), rng.Range(0.18f, 0.3f));
                var c = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, d) + Vector3.up * sz.y / 2f;
                var q = Quaternion.Euler(0, a + rng.Range(-20f, 20f), 0);
                Color w = wrap[k % wrap.Length], ribbon = wrap[(k + 3) % wrap.Length];
                pm.Box(c, sz, w, 0.01f, 1, q);
                pm.Box(c, new Vector3(sz.x + 0.004f, sz.y + 0.004f, 0.04f), ribbon, 0f, 1, q);
                pm.Box(c, new Vector3(0.04f, sz.y + 0.004f, sz.z + 0.004f), ribbon, 0f, 1, q);
            }
            pm.Build("Presents", go.transform, Mats.Lit("xmas_presents", Color.white, 0.1f, 0.5f, Mats.Noise, null, 0.1f, 0f));
            // solid: you walk round it, bullets hit it
            var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 1.1f, 0); col.radius = 0.55f; col.height = 2.2f;
            if (parent.GetComponentInParent<MapBase>() is MapBase mb2 && mb2.IsRaid)
            {
                // a little light so it shows at night
                var lgo = new GameObject("TreeLight"); lgo.transform.SetParent(go.transform, false); lgo.transform.localPosition = new Vector3(0, 1.4f, 0);
                var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.5f); l.range = 6f * scale; l.intensity = 1.2f; l.renderMode = LightRenderMode.ForceVertex;
            }
        }

        static Material Glow => Mats.Lit("xmas_glow", new Color(1f, 0.9f, 0.6f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(1.6f, 1.3f, 0.7f));
        static Material Green => Mats.Lit("xmas_tree", Color.white, 0.1f, 0.4f, Mats.Noise, null, 0.15f, 0.05f);

        /// <summary>A wreath hung on a wall (facing local -Z): a ring of green, red berries and a bow.</summary>
        public static void Wreath(Transform parent, Vector3 localPos, float radius)
        {
            var mb = new MB(2);
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2f;
                var c = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
                mb.Sub(0).Sphere(c, new Vector3(0.09f, 0.09f, 0.05f) * (radius / 0.35f), 8, k % 2 == 0 ? new Color(0.08f, 0.32f, 0.14f) : new Color(0.1f, 0.4f, 0.17f));
                if (k % 3 == 0) mb.Sub(0).Sphere(c + new Vector3(0, 0, -0.05f), Vector3.one * 0.022f, 6, new Color(0.75f, 0.06f, 0.06f));
            }
            mb.Sub(0).Sphere(new Vector3(-0.07f, -radius, -0.06f), new Vector3(0.08f, 0.05f, 0.03f), 8, new Color(0.8f, 0.08f, 0.08f));
            mb.Sub(0).Sphere(new Vector3(0.07f, -radius, -0.06f), new Vector3(0.08f, 0.05f, 0.03f), 8, new Color(0.8f, 0.08f, 0.08f));
            var go = mb.Build("Wreath", parent, Green, Glow);
            go.transform.localPosition = localPos;
        }

        /// <summary>A garland of fir with warm lights, sagging between two points.</summary>
        public static void Garland(Transform parent, Vector3 a, Vector3 b, float sag)
        {
            var mb = new MB(2);
            int n = Mathf.Max(6, Mathf.RoundToInt((b - a).magnitude / 0.12f));
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag;
                mb.Sub(0).Sphere(p, new Vector3(0.09f, 0.07f, 0.09f), 6, k % 2 == 0 ? new Color(0.08f, 0.32f, 0.14f) : new Color(0.1f, 0.4f, 0.17f));
                if (k % 3 == 1) mb.Sub(1).Sphere(p + Vector3.down * 0.06f, Vector3.one * 0.025f, 6, Color.white);
            }
            mb.Build("Garland", parent, Green, Glow);
        }
    }
}
