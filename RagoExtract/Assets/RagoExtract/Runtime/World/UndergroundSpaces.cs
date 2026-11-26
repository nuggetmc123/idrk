using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The Underground's big spaces (3.49). It used to be nothing but box rooms on a grid of corridors. Now a
    /// three-storey ATRIUM opens up the middle of every level - balconies, bridges and a glowing coolant column that
    /// rises from B3 to the roof - each level has two HALLS (two cells and the corridor between them made into one big
    /// room, the corridor running straight through it), and the corridors have cover in them: sandbag walls,
    /// barricades and slabs of fallen ceiling. Everything here rolls its own dice, so the rest of the layout is unchanged.
    /// </summary>
    public partial class UndergroundMap
    {
        static readonly (int i0, int j0, int i1, int j1, string type)[][] Halls =
        {
            new[] { (2, 1, 2, 2, "atrium"), (0, 1, 0, 2, "warehouse"), (3, 1, 4, 1, "mess hall") },
            new[] { (2, 1, 2, 2, "atrium"), (3, 1, 3, 2, "server farm"), (0, 2, 1, 2, "hydroponics") },
            new[] { (2, 1, 2, 2, "atrium"), (4, 1, 4, 2, "turbine hall"), (0, 1, 0, 2, "pump room") },
        };

        static bool InHall(int i, int j, int l)
        {
            if (i < 0 || j < 0 || i >= NX || j >= NZ) return false;
            foreach (var h in Halls[l]) if (i >= h.i0 && i <= h.i1 && j >= h.j0 && j <= h.j1) return true;
            return false;
        }

        /// <summary>The atrium's open middle on B1 and B2: the floor round it is a 2.2 m balcony.</summary>
        static readonly Rect AtriumHole = Rect.MinMaxRect(-3.8f, -11.8f, 3.8f, 11.8f);
        const float CoreZ = 5f, CoreR = 1.5f;
        /// <summary>Every doorway and opening built so far (rooms and halls) - the corridor cover keeps clear of them.</summary>
        readonly List<Vector3> _gaps = new List<Vector3>();

        void PaintBand(Vector3 a, Vector3 dir, Vector3 inward, float t0, float t1, Material paint)
            => B.Box(a + dir * ((t0 + t1) * 0.5f) + inward * 0.16f + Vector3.up * 0.6f, new Vector3(0.02f, 1.2f, t1 - t0), paint, Color.white, Quaternion.LookRotation(dir, Vector3.up), false);

        void FloorSpot(List<Vector3> pts, Vector3 p, float yaw) { var q = p + Vector3.up * 0.02f; pts.Add(q); B.SpotYaw[q] = yaw; }
        void DeskSpot(List<Vector3> pts, Vector3 p, float yaw) { pts.Add(p); B.DeskSpots[p] = true; B.SpotYaw[p] = yaw; }

        // ------------------------------------------------------------------------------------------------------ halls
        void BuildHall(int i0, int j0, int i1, int j1, string type, int l, Material conc, Color wallCol, Material paint, Material metal, Material lamp, Material yellow, List<Vector3> pts)
        {
            var b = B;
            var rng = new Rng(7001 + l * 131 + i0 * 17 + j0 * 5);
            Vector3 c0 = Cell(i0, j0, l), c1 = Cell(i1, j1, l);
            float h = Room / 2f, x0 = c0.x - h, x1 = c1.x + h, z0 = c0.z - h, z1 = c1.z + h, y = c0.y;
            var c = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
            bool alongZ = j1 > j0, atrium = type == "atrium";
            var corners = new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1) };
            string label = "B" + (l + 1) + "  " + type.ToUpper();
            for (int s = 0; s < 4; s++)
            {
                Vector3 a = corners[s], e = corners[(s + 1) % 4];
                float len = Vector3.Distance(a, e);
                Vector3 dir = (e - a) / len, inward = Vector3.Cross(dir, Vector3.up);
                var ops = new List<Vector4>(); var gaps = new List<(float off, float w)>(); var doors = new List<float>();
                // the corridor it swallowed runs straight through: a wide opening where it meets the long walls
                bool cross = alongZ ? (s == 1 || s == 3) : (s == 0 || s == 2);
                if (cross) { ops.Add(new Vector4(len * 0.5f, 3.6f, 0f, 3.0f)); gaps.Add((len * 0.5f, 3.6f)); }
                // doorways: one in the middle of each end wall (most of the time); the atrium also gets one per cell on its long walls
                if (!cross || atrium)
                {
                    int cells = Mathf.RoundToInt((len + 4f) / Pitch);
                    for (int k = 0; k < cells; k++)
                    {
                        float off = h + k * Pitch;
                        if (!atrium && !rng.Chance(0.8f)) continue;
                        ops.Add(new Vector4(off, 1.6f, 0f, 2.3f)); gaps.Add((off, 1.6f)); doors.Add(off);
                    }
                }
                b.Wall(a, e, Ht, 0.3f, conc, wallCol, ops);
                foreach (var off in doors) CustomDoors.Add(new WorldBuilder.DoorGap { c = a + dir * off, along = dir, across = inward, halfW = 0.8f, top = y + 2.3f, swing = true });
                gaps.Sort((p, q2) => p.off.CompareTo(q2.off));
                float t0 = 0.2f;
                foreach (var gp in gaps)
                {
                    float t1 = gp.off - gp.w * 0.5f - 0.1f;
                    if (t1 - t0 > 0.3f) PaintBand(a, dir, inward, t0, t1, paint);
                    t0 = gp.off + gp.w * 0.5f + 0.1f;
                    var mid = a + dir * gp.off;
                    _gaps.Add(mid);
                    b.Sign(mid - inward * 0.18f + Vector3.up * (gp.w > 2f ? 3.2f : 2.55f), Quaternion.LookRotation(inward), label, new Color(0.85f, 0.85f, 0.8f), 0.12f);
                }
                if (len - 0.2f - t0 > 0.3f) PaintBand(a, dir, inward, t0, len - 0.2f, paint);
                // loot along the inside of the wall, clear of the openings
                for (float t = 2.2f; t < len - 1.5f; t += 4.3f)
                {
                    bool clear = true;
                    foreach (var gp in gaps) if (Mathf.Abs(t - gp.off) < gp.w * 0.5f + 1.2f) { clear = false; break; }
                    if (clear) FloorSpot(pts, a + dir * t + inward * 0.65f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90f);
                }
            }
            if (atrium) { DressAtriumLevel(l, rng, metal, lamp, yellow, pts); return; }
            // u runs the long way (the swallowed corridor crosses at u = 0, so |u| < 2.4 is kept clear), v across
            System.Func<float, float, float, Vector3> P = (u, v, yy) => alongZ ? new Vector3(c.x + v, y + yy, c.z + u) : new Vector3(c.x + u, y + yy, c.z + v);
            var q = Quaternion.Euler(0, alongZ ? 0f : 90f, 0);   // local z = the long way
            float tableYaw = alongZ ? 90f : 0f;                  // b.Table's size.x lies along the hall
            for (float u = -11f; u <= 11.01f; u += 5.5f)
                foreach (float v in new[] { -3f, 3f })
                    b.Box(P(u, v, Ht - 0.04f), new Vector3(0.3f, 0.06f, 1.4f), lamp, Color.white, q, false);
            Squad(P(rng.Range(-1.2f, 1.2f), rng.Range(-3f, 3f), 0.05f), rng.Range(3, 6), type == "turbine hall" || type == "server farm" || type == "warehouse");
            DressHall(type, P, q, tableYaw, alongZ, rng, metal, pts);
        }

        void DressHall(string type, System.Func<float, float, float, Vector3> P, Quaternion q, float tableYaw, bool alongZ, Rng rng, Material metal, List<Vector3> pts)
        {
            var b = B;
            float sideYaw = q.eulerAngles.y;
            var dark = new Color(0.16f, 0.16f, 0.17f);
            var screen = Mats.Lit("ug_screen", new Color(0.3f, 0.6f, 0.9f), 0f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(0.4f, 0.9f, 1.6f));
            switch (type)
            {
                case "warehouse":
                {
                    // tall pallet racking (orange beams, blue uprights) loaded with boxes, shrink-wrap and drums, a forklift
                    var beam = Mats.Lit("ug_rackbeam", new Color(0.85f, 0.42f, 0.08f), 0.45f, 0.4f, Mats.Grunge, null, 0.5f, 0.45f, true, 1.5f);
                    var post = Mats.Lit("ug_rackpost", new Color(0.14f, 0.28f, 0.58f), 0.45f, 0.4f, Mats.Grunge, null, 0.5f, 0.45f, true, 1.5f);
                    var card = Mats.Lit("cardboard", Color.white, 0, 0.2f, Mats.Noise, null, 0.4f, 0f);
                    var wrap = Mats.Lit("ug_wrap", new Color(0.5f, 0.53f, 0.56f), 0f, 0.65f);
                    var wood = Mats.Lit("ug_pallet", new Color(0.62f, 0.5f, 0.34f), 0f, 0.25f, Mats.Wood, Mats.WoodN, 0.5f, 0.2f, true, 1f);
                    foreach (float side in new[] { -1f, 1f })
                        foreach (float v in new[] { -2.1f, 2.1f })
                        {
                            float ua = side * 3.2f, ub = side * 12.6f, lo = Mathf.Min(ua, ub), um = (ua + ub) * 0.5f, ul = Mathf.Abs(ub - ua);
                            b.Collider(P(um, v, 1.5f), new Vector3(1.1f, 3f, ul), q, Synth.Surface.Metal);
                            const int bays = 4; float bay = ul / bays;
                            for (int k = 0; k <= bays; k++)
                                foreach (float fv in new[] { -0.5f, 0.5f })
                                    b.Box(P(lo + k * bay, v + fv, 1.5f), new Vector3(0.08f, 3f, 0.08f), post, Color.white, q, false);
                            foreach (float lv in new[] { 0.12f, 1.22f, 2.32f })
                                foreach (float fv in new[] { -0.5f, 0.5f })
                                    b.Box(P(um, v + fv, lv), new Vector3(0.06f, 0.1f, ul), beam, Color.white, q, false);
                            for (int k = 0; k < bays; k++)
                                for (int lv = 0; lv < 3; lv++)
                                {
                                    if (!rng.Chance(lv == 0 ? 0.85f : 0.65f)) continue;
                                    float bu = lo + (k + 0.5f) * bay, by = 0.17f + lv * 1.1f, lh = rng.Range(0.45f, 0.8f);
                                    b.Box(P(bu, v, by + 0.06f), new Vector3(1.0f, 0.12f, 1.15f), wood, Color.white, q, false);   // pallet
                                    int kind = rng.Range(0, 3);
                                    if (kind == 0) b.Box(P(bu, v, by + 0.12f + lh * 0.5f), new Vector3(0.95f, lh, 1.05f), card, new Color(0.66f, 0.5f, 0.32f), q, false);
                                    else if (kind == 1) b.Box(P(bu, v, by + 0.12f + lh * 0.5f), new Vector3(0.96f, lh, 1.08f), wrap, Color.white, q, false, Synth.Surface.Concrete, 0.04f);
                                    else foreach (float du in new[] { -0.28f, 0.28f }) b.Cyl(P(bu + du, v, by + 0.12f), P(bu + du, v, by + 0.95f), 0.26f, 0.26f, 12, metal, rng.Chance(0.5f) ? new Color(0.2f, 0.3f, 0.6f) : new Color(0.55f, 0.15f, 0.1f), false);
                                }
                            FloorSpot(pts, P(um + rng.Range(-2f, 2f), v + Mathf.Sign(v) * 0.95f, 0f), sideYaw + (v > 0 ? -90f : 90f));
                        }
                    // a forklift parked at one end, forks toward the middle
                    var fl = Mats.Lit("ug_forklift", new Color(0.9f, 0.68f, 0.08f), 0.3f, 0.45f, Mats.Grunge, null, 0.4f, 0.3f, true, 1.5f);
                    System.Func<float, float, float, Vector3> F = (du, dv, dy) => P(12.2f + du, -4.4f + dv, dy);
                    b.Box(F(0f, 0f, 0.7f), new Vector3(1.1f, 0.95f, 1.9f), fl, Color.white, q, true, Synth.Surface.Metal, 0.04f);
                    b.Box(F(0.45f, 0f, 1.95f), new Vector3(1.05f, 0.06f, 1.1f), metal, dark, q, false);   // overhead guard
                    foreach (float dv in new[] { -0.5f, 0.5f }) foreach (float du in new[] { -0.05f, 0.95f }) b.Box(F(du, dv, 1.42f), new Vector3(0.05f, 1.05f, 0.05f), metal, dark, q, false);
                    foreach (float dv in new[] { -0.32f, 0.32f })
                    {
                        b.Box(F(-1.05f, dv, 1.25f), new Vector3(0.1f, 2.5f, 0.1f), metal, dark, q, false);    // mast
                        b.Box(F(-1.65f, dv * 0.8f, 0.08f), new Vector3(0.12f, 0.05f, 1.1f), metal, dark, q, false);   // forks
                    }
                    foreach (float du in new[] { -0.6f, 0.6f }) foreach (float dv in new[] { -0.6f, 0.6f })
                            b.Cyl(F(du, dv - 0.08f, 0.3f), F(du, dv + 0.08f, 0.3f), 0.3f, 0.3f, 12, metal, new Color(0.06f, 0.06f, 0.06f), false);
                    break;
                }
                case "mess hall":
                {
                    // long tables with benches, a serving counter under an extraction hood, vending machines
                    var bench = Mats.Lit("ug_bench", new Color(0.35f, 0.38f, 0.4f), 0.5f, 0.35f, Mats.Metal, null, 0.4f, 0.3f);
                    var steel = Mats.Lit("ug_steel", new Color(0.75f, 0.76f, 0.78f), 0.8f, 0.6f, Mats.Metal, Mats.MetalN, 0.3f, 0.4f);
                    var glass = Mats.Lit("lab_glass", new Color(0.6f, 0.8f, 0.85f), 0.1f, 0.9f);
                    var vend = Mats.Lit("ug_vend", new Color(0.9f, 0.95f, 1f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(1.3f, 1.4f, 1.6f));
                    foreach (float side in new[] { -1f, 1f })
                        foreach (float u in new[] { 5.2f, 9.8f })
                            foreach (float v in new[] { -3.4f, -0.6f })
                            {
                                var tp = P(side * u, v, 0f);
                                if (!b.Table(tp, tableYaw, new Vector2(3f, 0.9f), 0.76f)) continue;
                                foreach (float dv in new[] { -0.72f, 0.72f }) b.Box(P(side * u, v + dv, 0.23f), new Vector3(0.32f, 0.46f, 3f), bench, Color.white, q, true, Synth.Surface.Metal);
                                if (rng.Chance(0.6f)) DeskSpot(pts, P(side * u + rng.Range(-0.9f, 0.9f), v, 0.82f), sideYaw);
                            }
                    b.Box(P(7.9f, 4.95f, 0.46f), new Vector3(0.8f, 0.92f, 9f), steel, Color.white, q, true, Synth.Surface.Metal);   // serving counter
                    b.Box(P(7.9f, 4.62f, 1.3f), new Vector3(0.02f, 0.42f, 9f), glass, Color.white, q, false);                       // sneeze guard
                    for (int k = 0; k < 8; k++) b.Box(P(4.2f + k * 1.06f, 5.0f, 0.95f), new Vector3(0.32f, 0.06f, 0.5f), steel, new Color(0.85f, 0.85f, 0.85f), q, false);   // food pans
                    b.Box(P(7.9f, 5.45f, 2.75f), new Vector3(0.75f, 0.45f, 9f), steel, new Color(0.7f, 0.7f, 0.72f), q, false);    // extraction hood
                    DeskSpot(pts, P(5.3f, 4.95f, 0.95f), sideYaw + 180f); DeskSpot(pts, P(10.6f, 4.95f, 0.95f), sideYaw + 180f);
                    foreach (float u in new[] { -12.2f, -11.0f })
                    {
                        b.Box(P(u, 5.35f, 0.95f), new Vector3(0.8f, 1.9f, 1.0f), metal, new Color(0.12f, 0.14f, 0.2f), q, true, Synth.Surface.Metal);
                        b.Box(P(u, 4.94f, 1.15f), new Vector3(0.02f, 1.2f, 0.8f), vend, Color.white, q, false);
                    }
                    b.Cyl(P(-9.7f, 5.4f, 0f), P(-9.7f, 5.4f, 1.05f), 0.18f, 0.18f, 12, steel, Color.white, true, Synth.Surface.Metal);   // water cooler
                    b.Cyl(P(-9.7f, 5.4f, 1.05f), P(-9.7f, 5.4f, 1.45f), 0.15f, 0.15f, 12, glass, new Color(0.5f, 0.7f, 0.95f), false);
                    break;
                }
                case "server farm":
                {
                    // aisles of humming server racks (LEDs blinking), cable trays over them, a monitoring desk
                    var rack = Mats.Lit("ug_server", new Color(0.1f, 0.1f, 0.11f), 0.55f, 0.45f, Mats.Metal, Mats.MetalN, 0.3f, 0.3f);
                    var ledG = Mats.Lit("server_led", new Color(0.1f, 0.8f, 0.3f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.2f, 1.6f, 0.5f));
                    var ledB = Mats.Lit("server_led_b", new Color(0.1f, 0.4f, 0.9f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.2f, 0.7f, 2.0f));
                    var tray = Mats.Lit("ug_tray", new Color(0.55f, 0.56f, 0.58f), 0.6f, 0.4f, Mats.Metal, null, 0.4f, 0.3f);
                    foreach (float side in new[] { -1f, 1f })
                        foreach (float v in new[] { -3.4f, -1.0f, 1.4f })
                        {
                            float ua = side * 3.0f, ub = side * 12.4f, lo = Mathf.Min(ua, ub), um = (ua + ub) * 0.5f, ul = Mathf.Abs(ub - ua);
                            b.Box(P(um, v, 1.05f), new Vector3(0.9f, 2.1f, ul), rack, Color.white, q, true, Synth.Surface.Metal);
                            int units = Mathf.FloorToInt(ul / 0.62f);
                            for (int k = 0; k < units; k++)
                            {
                                float uu = lo + (k + 0.5f) * (ul / units);
                                foreach (float face in new[] { -1f, 1f })
                                {
                                    b.Box(P(lo + k * (ul / units), v + face * 0.452f, 1.05f), new Vector3(0.004f, 2.0f, 0.014f), metal, new Color(0.04f, 0.04f, 0.04f), q, false);   // between cabinets
                                    b.Box(P(uu, v + face * 0.453f, rng.Range(0.5f, 1.8f)), new Vector3(0.004f, 0.025f, 0.34f), rng.Chance(0.7f) ? ledG : ledB, Color.white, q, false);
                                }
                            }
                            b.Box(P(um, v, 2.6f), new Vector3(0.45f, 0.07f, ul), tray, Color.white, q, false);   // cable tray
                        }
                    if (b.Table(P(7.6f, 4.9f, 0f), tableYaw, new Vector2(5f, 0.9f), 0.76f))
                    {
                        for (int k = 0; k < 4; k++) b.Box(P(5.7f + k * 1.25f, 5.18f, 1.12f), new Vector3(0.04f, 0.5f, 0.85f), screen, Color.white, q, false);
                        DeskSpot(pts, P(6.3f, 4.8f, 0.82f), sideYaw + 180f); DeskSpot(pts, P(9f, 4.8f, 0.82f), sideYaw + 180f);
                    }
                    for (int k = 0; k < 4; k++) b.Box(P(-4.6f - k * 1.9f, 5.25f, 0.9f), new Vector3(0.9f, 1.8f, 1.2f), metal, new Color(0.22f, 0.23f, 0.24f), q, true, Synth.Surface.Metal);   // UPS cabinets
                    break;
                }
                case "hydroponics":
                {
                    // grow beds of greens under magenta grow lights, water tanks, a lab bench
                    var trough = Mats.Lit("ug_trough", new Color(0.82f, 0.84f, 0.86f), 0.3f, 0.5f, Mats.Grunge, null, 0.4f, 0.2f, true, 1.5f);
                    var soil = Mats.Lit("ug_soil", new Color(0.2f, 0.14f, 0.09f), 0f, 0.1f, Mats.Noise, null, 0.3f, 0f);
                    var leaf = Mats.Lit("ug_leaf", new Color(0.3f, 0.6f, 0.2f), 0f, 0.35f, Mats.Noise, null, 0.2f, 0f);
                    var grow = Mats.Lit("ug_growlamp", new Color(1f, 0.4f, 0.9f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(2.4f, 0.6f, 2.2f));
                    foreach (float side in new[] { -1f, 1f })
                        foreach (float v in new[] { -3.6f, -1.2f, 1.2f })
                        {
                            float ua = side * 3.0f, ub = side * 12.2f, lo = Mathf.Min(ua, ub), hi = Mathf.Max(ua, ub), um = (ua + ub) * 0.5f, ul = hi - lo;
                            b.Box(P(um, v, 0.38f), new Vector3(1.0f, 0.76f, ul), trough, Color.white, q, true, Synth.Surface.Metal);
                            b.Box(P(um, v, 0.765f), new Vector3(0.9f, 0.02f, ul - 0.1f), soil, Color.white, q, false);
                            for (float uu = lo + 0.35f; uu < hi - 0.3f; uu += 0.55f)
                                foreach (float dv in new[] { -0.22f, 0.22f })
                                {
                                    float r = rng.Range(0.11f, 0.19f);
                                    b.Sphere(P(uu + rng.Range(-0.05f, 0.05f), v + dv, 0.78f + r * 0.45f), new Vector3(r, r * 0.75f, r), 6, leaf, new Color(rng.Range(0.75f, 1f), rng.Range(0.85f, 1.1f), rng.Range(0.7f, 0.9f)), false);
                                }
                            b.Box(P(um, v, 2.4f), new Vector3(0.3f, 0.05f, ul - 0.4f), grow, Color.white, q, false);   // grow light
                            foreach (float end in new[] { lo + 0.4f, hi - 0.4f }) b.Box(P(end, v, 2.92f), new Vector3(0.01f, 0.95f, 0.01f), metal, dark, q, false);   // its hangers
                        }
                    var tank = Mats.Lit("ug_tank", new Color(0.7f, 0.74f, 0.76f), 0.5f, 0.55f, Mats.Grunge, null, 0.4f, 0.3f, true, 1.5f);
                    foreach (float u in new[] { -11f, -8.8f })
                    {
                        b.Cyl(P(u, 4.9f, 0f), P(u, 4.9f, 2.3f), 0.75f, 0.75f, 18, tank, Color.white, true, Synth.Surface.Metal);
                        foreach (float by in new[] { 0.5f, 1.7f }) b.Cyl(P(u, 4.9f, by), P(u, 4.9f, by + 0.06f), 0.78f, 0.78f, 18, metal, dark, false);
                    }
                    if (b.Table(P(7.8f, 5.15f, 0f), tableYaw, new Vector2(3.2f, 0.8f), 0.9f))
                    {
                        DeskSpot(pts, P(6.9f, 5.15f, 0.96f), sideYaw + 180f); DeskSpot(pts, P(8.7f, 5.15f, 0.96f), sideYaw + 180f);
                        for (int k = 0; k < 3; k++) b.Cyl(P(6.6f + k * 1.2f, 5.35f, 0.93f), P(6.6f + k * 1.2f, 5.35f, 1.13f), 0.05f, 0.04f, 10, Mats.Lit("lab_glass", new Color(0.6f, 0.8f, 0.85f), 0.1f, 0.9f), Color.white, false);
                    }
                    break;
                }
                case "turbine hall":
                {
                    // two big turbine sets on plinths, their generators at the ends, exhaust stacks into the ceiling,
                    // control panels and pipe runs along the walls
                    var tm = Mats.Lit("ug_turbine", new Color(0.32f, 0.42f, 0.36f), 0.55f, 0.45f, Mats.Grunge, null, 0.5f, 0.4f, true, 1.5f);
                    var plinth = Mats.Lit("ug_plinth", new Color(0.55f, 0.54f, 0.5f), 0f, 0.3f, Mats.Concrete, null, 0.6f, 0.3f, true, 1f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float uc = side * 7.6f, v = -2.3f;
                        b.Box(P(uc, v, 0.25f), new Vector3(2.6f, 0.5f, 7.4f), plinth, Color.white, q, true);
                        b.Cyl(P(uc - 3f, v, 1.55f), P(uc + 3f, v, 1.55f), 1.0f, 1.05f, 20, tm, Color.white, true, Synth.Surface.Metal);
                        for (int k = 0; k < 5; k++) { float ru = uc - 2.4f + k * 1.2f; b.Cyl(P(ru - 0.06f, v, 1.55f), P(ru + 0.06f, v, 1.55f), 1.09f, 1.09f, 20, metal, dark, false); }
                        b.Box(P(side * 11.7f, v, 1.3f), new Vector3(2.2f, 2.1f, 1.7f), tm, Color.white, q, true, Synth.Surface.Metal, 0.05f);   // generator
                        b.Cyl(P(uc - side * 1.5f, v, 2.4f), P(uc - side * 1.5f, v, Ht), 0.35f, 0.35f, 14, metal, dark, false);                  // exhaust stack
                        b.Box(P(uc, 5.4f, 0.8f), new Vector3(0.6f, 1.6f, 1.6f), tm, Color.white, q, true, Synth.Surface.Metal);                 // control panel
                        b.Box(P(uc, 5.09f, 1.25f), new Vector3(0.02f, 0.4f, 1.2f), screen, Color.white, q, false);
                        FloorSpot(pts, P(uc + 1.4f, 4.6f, 0f), sideYaw + 90f);
                        foreach (float py in new[] { 1.0f, 2.5f }) b.Cyl(P(side * 2.4f, -5.5f, py), P(side * 13.6f, -5.5f, py), 0.16f, 0.16f, 10, metal, new Color(0.3f, 0.26f, 0.2f), false);
                    }
                    break;
                }
                case "pump room":
                {
                    // flooded: dark standing water over the whole floor, four big pumps feeding a main along the wall
                    var water = Mats.Lit("ug_floodwater", new Color(0.14f, 0.16f, 0.15f), 0.2f, 0.95f);
                    b.Box(P(0f, 0f, 0.05f), new Vector3(11.4f, 0.02f, 27.4f), water, Color.white, q, false);
                    var pm = Mats.Lit("ug_pump", new Color(0.2f, 0.32f, 0.5f), 0.5f, 0.45f, Mats.Grunge, null, 0.6f, 0.45f, true, 1.5f);
                    var plinth = Mats.Lit("ug_plinth", new Color(0.55f, 0.54f, 0.5f), 0f, 0.3f, Mats.Concrete, null, 0.6f, 0.3f, true, 1f);
                    foreach (float u in new[] { -10.5f, -6f, 6f, 10.5f })
                    {
                        const float v = -2.6f;
                        b.Box(P(u, v, 0.2f), new Vector3(1.4f, 0.4f, 1.6f), plinth, Color.white, q, true);
                        b.Cyl(P(u, v, 0.4f), P(u, v, 1.4f), 0.5f, 0.5f, 16, pm, Color.white, true, Synth.Surface.Metal);
                        b.Box(P(u, v, 1.75f), new Vector3(0.7f, 0.7f, 0.9f), pm, new Color(0.8f, 0.8f, 0.8f), q, false, Synth.Surface.Metal, 0.03f);   // motor
                        b.Cyl(P(u, v - 0.5f, 0.9f), P(u, -5.2f, 0.9f), 0.2f, 0.2f, 12, metal, dark, false);
                        b.Cyl(P(u, -4.0f, 1.12f), P(u, -4.0f, 1.32f), 0.03f, 0.03f, 6, metal, dark, false);   // valve stem
                        b.Cyl(P(u, -4.0f, 1.32f), P(u, -4.0f, 1.36f), 0.22f, 0.22f, 14, metal, new Color(0.7f, 0.12f, 0.08f), false);   // and its wheel
                    }
                    foreach (float side in new[] { -1f, 1f }) b.Cyl(P(side * 2.4f, -5.45f, 0.9f), P(side * 13.6f, -5.45f, 0.9f), 0.34f, 0.34f, 14, metal, new Color(0.3f, 0.32f, 0.3f), true, Synth.Surface.Metal);
                    FloorSpot(pts, P(-8.2f, -0.5f, 0f), sideYaw); FloorSpot(pts, P(8.2f, -0.5f, 0f), sideYaw);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------- atrium
        /// <summary>One level of the atrium: lights round the balcony, rails and a bridge on B1/B2, cover on the B3 floor.</summary>
        void DressAtriumLevel(int l, Rng rng, Material metal, Material lamp, Material yellow, List<Vector3> pts)
        {
            var b = B; float y = LevelY(l);
            bool bottom = l == Levels - 1;
            // strip lights round the edge - under the balcony above (or the roof), never over the open middle
            for (float z = -12f; z <= 12.01f; z += 6f) foreach (float x in new[] { -4.9f, 4.9f }) b.Box(new Vector3(x, y + Ht - 0.04f, z), new Vector3(0.3f, 0.06f, 1.4f), lamp, Color.white, null, false);
            foreach (float z in new[] { -12.9f, 12.9f }) b.Box(new Vector3(0, y + Ht - 0.04f, z), new Vector3(1.4f, 0.06f, 0.3f), lamp, Color.white, null, false);
            b.Sign(new Vector3(3.4f, y + 2.6f, 13.83f), Quaternion.identity, "B" + (l + 1), new Color(0.9f, 0.75f, 0.2f), 0.9f);
            if (!bottom)
            {
                float hx = AtriumHole.xMax, hz = AtriumHole.yMax, bz = l == 0 ? -2f : 9.5f;   // the bridge across the middle
                Rail(new Vector3(-hx, y, -hz), new Vector3(hx, y, -hz), metal);
                Rail(new Vector3(hx, y, hz), new Vector3(-hx, y, hz), metal);
                foreach (float sx in new[] { -1f, 1f })
                {
                    Rail(new Vector3(sx * hx, y, -hz), new Vector3(sx * hx, y, bz - 0.95f), metal);
                    Rail(new Vector3(sx * hx, y, bz + 0.95f), new Vector3(sx * hx, y, hz), metal);
                }
                b.Box(new Vector3(0, y - 0.07f, bz), new Vector3(hx * 2f, 0.14f, 1.9f), metal, new Color(0.36f, 0.37f, 0.35f), null, true, Synth.Surface.Metal);
                foreach (float dz in new[] { -0.92f, 0.92f }) Rail(new Vector3(-hx, y, bz + dz), new Vector3(hx, y, bz + dz), metal);
                // yellow edge line along the balcony, a step back from the drop
                foreach (float sx in new[] { -1f, 1f }) b.Box(new Vector3(sx * (hx + 0.25f), y + 0.004f, 0), new Vector3(0.1f, 0.008f, hz * 2f), yellow, Color.white, null, false);
                Squad(new Vector3(rng.Chance(0.5f) ? -4.9f : 4.9f, y + 0.05f, rng.Range(-9f, 9f)), 2);
            }
            else
            {
                // the bottom of the atrium: concrete barriers and crates to fight round
                var barrier = Mats.Lit("ug_barrier", new Color(0.62f, 0.6f, 0.56f), 0f, 0.3f, Mats.Concrete, null, 0.6f, 0.3f, true, 1f);
                foreach (var (bx, bz, yaw) in new[] { (-2.6f, -6.5f, 15f), (2.7f, -9.6f, -20f), (-3.2f, 10.6f, 80f), (3.0f, -2.6f, 95f), (-2.9f, 0.4f, -70f) })
                {
                    var bq = Quaternion.Euler(0, yaw, 0); var bp = new Vector3(bx, y, bz);
                    b.Box(bp + Vector3.up * 0.3f, new Vector3(2.0f, 0.6f, 0.6f), barrier, Color.white, bq, true);
                    b.Box(bp + Vector3.up * 0.72f, new Vector3(2.0f, 0.24f, 0.26f), barrier, Color.white, bq, true);
                }
                b.Crate(new Vector3(-4.4f, y, -11.5f), 12f, new Vector3(1.1f, 0.9f, 1.0f)); b.Crate(new Vector3(4.6f, y, 9.4f), -8f, new Vector3(1.2f, 0.9f, 1.0f));
                Squad(new Vector3(rng.Range(-2f, 2f), y + 0.05f, -7.5f), rng.Range(3, 5), true);
            }
        }

        /// <summary>The coolant column rising through the atrium from the B3 floor to the roof, its pipes, lights and
        /// the cables hanging in the void.</summary>
        void BuildAtriumCore(Material metal, Material conc, Color wallCol, Material lamp)
        {
            var b = B;
            float yB = LevelY(Levels - 1), yT = LevelY(0) + Ht;
            var core = Mats.Lit("ug_core", new Color(0.3f, 0.33f, 0.36f), 0.6f, 0.5f, Mats.Grunge, null, 0.5f, 0.45f, true, 1.5f);
            var glow = Mats.Lit("ug_coreglow", new Color(0.3f, 1f, 0.95f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(0.35f, 2.4f, 2.2f));
            var cp = new Vector3(0, 0, CoreZ);
            var dark = new Color(0.2f, 0.2f, 0.2f);
            b.Box(cp + new Vector3(0, yB + 0.25f, 0), new Vector3(4.2f, 0.5f, 4.2f), conc, wallCol * 0.8f, null, true);   // plinth
            b.Cyl(cp + Vector3.up * yB, cp + Vector3.up * yT, CoreR, CoreR, 28, core, Color.white, true, Synth.Surface.Metal);
            for (float yy = yB + 1.2f; yy < yT - 0.4f; yy += 1.4f) b.Cyl(cp + Vector3.up * yy, cp + Vector3.up * (yy + 0.14f), CoreR + 0.07f, CoreR + 0.07f, 28, metal, dark, false);
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + k * 90f) * Mathf.Deg2Rad; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Box(cp + d * (CoreR + 0.03f) + Vector3.up * ((yB + 0.6f + yT) * 0.5f), new Vector3(0.14f, yT - yB - 0.8f, 0.06f), glow, Color.white, Quaternion.LookRotation(d), false);
            }
            // pipes from the column out to the walls on every level (above head height on the balconies)
            for (int l = 0; l < Levels; l++)
                foreach (float sx in new[] { -1f, 1f })
                    b.Cyl(cp + new Vector3(sx * CoreR, LevelY(l) + 2.75f, 0), new Vector3(sx * 5.85f, LevelY(l) + 2.75f, CoreZ), 0.26f, 0.26f, 14, metal, new Color(0.35f, 0.3f, 0.22f), false);
            // a control desk facing the column on the B3 floor
            b.Box(new Vector3(0, yB + 0.5f, CoreZ - CoreR - 1.5f), new Vector3(2.2f, 1.0f, 0.7f), core, Color.white, null, true, Synth.Surface.Metal);
            b.Box(new Vector3(0, yB + 1.1f, CoreZ - CoreR - 1.35f), new Vector3(1.8f, 0.4f, 0.03f), Mats.Lit("ug_screen", new Color(0.3f, 0.6f, 0.9f), 0f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(0.4f, 0.9f, 1.6f)), Color.white, Quaternion.Euler(-25f, 0, 0), false);
            b.Sign(new Vector3(0, yB + 3.3f, CoreZ - CoreR - 0.1f), Quaternion.identity, "COOLANT CORE", new Color(0.3f, 1f, 0.9f), 0.32f);
            // cables hanging into the void from the roof
            var crng = new Rng(4848);
            for (int k = 0; k < 7; k++)
            {
                float cx = crng.Range(-3.2f, 3.2f), cz = crng.Range(-11f, 11f);
                if (Mathf.Abs(cz - CoreZ) < CoreR + 0.6f || Mathf.Abs(cz - -2f) < 1.3f || Mathf.Abs(cz - 9.5f) < 1.3f) continue;   // not through the column or a bridge
                b.Cyl(new Vector3(cx, yT, cz), new Vector3(cx + crng.Range(-0.2f, 0.2f), crng.Range(LevelY(1) + 1.5f, LevelY(0) + 0.5f), cz), 0.014f, 0.014f, 4, metal, new Color(0.05f, 0.05f, 0.05f), false);
            }
            // a big work lamp hung over the void, and light on the column's foot
            b.Cyl(new Vector3(0, yT, -6f), new Vector3(0, yT - 0.8f, -6f), 0.02f, 0.02f, 4, metal, Color.black, false);
            b.Cyl(new Vector3(0, yT - 0.8f, -6f), new Vector3(0, yT - 1.05f, -6f), 0.12f, 0.5f, 16, metal, new Color(0.25f, 0.25f, 0.25f), false);
            b.Cyl(new Vector3(0, yT - 1.04f, -6f), new Vector3(0, yT - 1.06f, -6f), 0.46f, 0.46f, 16, lamp, Color.white, false);
            b.AddLight(new Vector3(0, yT - 1.4f, -6f), new Color(1f, 0.92f, 0.78f), 15f, 1.0f);
            b.AddLight(new Vector3(0, yB + 3f, CoreZ - 3f), new Color(0.5f, 1f, 0.95f), 11f, 1.3f);
            b.AddLight(new Vector3(0, LevelY(1) + 1.6f, -8f), new Color(0.9f, 0.95f, 1f), 10f, 0.7f);
        }

        // ------------------------------------------------------------------------------------------- corridor cover
        /// <summary>Cover on one side of about two in five corridor stretches - never by a doorway, a stairwell, a
        /// spawn or an extract.</summary>
        void CorridorCovers(Material metal)
        {
            var r = new Rng(9119);
            var sand = Mats.Lit("ug_sandbag", new Color(0.56f, 0.5f, 0.38f), 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.6f, 0.1f);
            var rubble = Mats.Lit("ug_rubble", new Color(0.55f, 0.53f, 0.5f), 0f, 0.2f, Mats.Concrete, null, 0.6f, 0.2f, true, 2f);
            var keep = new List<Vector3>(_gaps);
            float b3 = LevelY(2);
            keep.AddRange(new[] { new Vector3(-40, b3, 32), new Vector3(40, b3, -32), new Vector3(8, b3, 32), new Vector3(40, b3, 32), new Vector3(-8, b3, -32), new Vector3(24, b3, -32), new Vector3(8, 0, 0), new Vector3(-40, b3, -32) });
            bool Clear(Vector3 p)
            {
                foreach (var k in keep) if (Mathf.Abs(k.y - p.y) < 1.5f && new Vector2(k.x - p.x, k.z - p.z).sqrMagnitude < 3.3f * 3.3f) return false;
                return true;
            }
            for (int l = 0; l < Levels; l++)
            {
                float y = LevelY(l);
                for (int i = 0; i <= NX; i++)   // the corridors running north-south
                    for (int j = 0; j < NZ; j++)
                    {
                        if (InHall(i - 1, j, l) || InHall(i, j, l) || !r.Chance(0.42f)) continue;
                        var p = new Vector3((i - NX / 2f) * Pitch, y, (j - (NZ - 1) / 2f) * Pitch + (r.Chance(0.5f) ? -1f : 1f) * r.Range(3.6f, 4.8f));   // between the rooms' doors (mid-way) and the junctions
                        float side = r.Chance(0.5f) ? -1f : 1f; int kind = r.Range(0, 3);
                        if (Clear(p)) Cover(p, Vector3.forward, side, kind, r, sand, rubble, metal);
                    }
                for (int j = 0; j <= NZ; j++)   // and east-west
                    for (int i = 0; i < NX; i++)
                    {
                        if (InHall(i, j - 1, l) || InHall(i, j, l) || !r.Chance(0.42f)) continue;
                        var p = new Vector3((i - (NX - 1) / 2f) * Pitch + (r.Chance(0.5f) ? -1f : 1f) * r.Range(3.6f, 4.8f), y, (j - NZ / 2f) * Pitch);
                        float side = r.Chance(0.5f) ? -1f : 1f; int kind = r.Range(0, 3);
                        if (Clear(p)) Cover(p, Vector3.right, side, kind, r, sand, rubble, metal);
                    }
            }
        }

        void Cover(Vector3 p, Vector3 along, float side, int kind, Rng r, Material sand, Material rubble, Material metal)
        {
            var b = B;
            var across = Vector3.Cross(Vector3.up, along);
            var q = Quaternion.LookRotation(along, Vector3.up);
            var at = p + across * side * 1.0f;   // the corridor is 4 m wide: this leaves 2.25 m clear on the other side
            if (kind == 0)   // a sandbag wall across half the corridor
            {
                for (int row = 0; row < 3; row++)
                {
                    int n = row == 2 ? 2 : 3;
                    for (int k = 0; k < n; k++)
                    {
                        float off = (k - (n - 1) * 0.5f) * 0.5f;
                        var tint = new Color(r.Range(0.85f, 1f), r.Range(0.85f, 1f), r.Range(0.8f, 0.95f));
                        b.Box(at + q * new Vector3(off, 0.12f + row * 0.23f, 0f), new Vector3(0.5f, 0.24f, 0.42f), sand, tint, q * Quaternion.Euler(0, r.Range(-7f, 7f), 0), false, Synth.Surface.Dirt, 0.07f);
                    }
                }
                b.Collider(at + Vector3.up * 0.36f, new Vector3(1.5f, 0.72f, 0.45f), q, Synth.Surface.Dirt);
            }
            else if (kind == 1)   // a barricade: a table knocked on its side, a crate behind it
            {
                var wood = Mats.Lit("table", Color.white, 0f, 0.35f, Mats.Wood, Mats.WoodN, 0.4f, 0.2f, true, 1f);
                b.Box(at + Vector3.up * 0.45f, new Vector3(1.6f, 0.9f, 0.05f), wood, new Color(0.55f, 0.42f, 0.3f), q * Quaternion.Euler(0, r.Range(-12f, 12f), 0), true, Synth.Surface.Wood);
                foreach (float lx in new[] { -0.7f, 0.7f }) foreach (float ly in new[] { 0.1f, 0.8f })
                        b.Box(at + q * new Vector3(lx, ly, -0.38f), new Vector3(0.05f, 0.05f, 0.74f), wood, new Color(0.4f, 0.3f, 0.22f), q, false);
                b.Crate(at + q * new Vector3(r.Range(-0.3f, 0.3f), 0, -1.15f), r.Range(0f, 30f), new Vector3(0.9f, 0.7f, 0.7f));
            }
            else   // a slab of ceiling come down: one end on the floor, rubble round it, the hole it left above
            {
                var sq = q * Quaternion.Euler(r.Range(28f, 38f), r.Range(-8f, 8f), 0);
                b.Box(at + Vector3.up * 0.75f, new Vector3(1.5f, 0.2f, 2.2f), rubble, new Color(0.72f, 0.7f, 0.67f), sq, true, Synth.Surface.Concrete);
                b.Box(new Vector3(at.x, p.y + Ht - 0.012f, at.z), new Vector3(1.4f, 0.02f, 2.0f), Mats.Lit("ug_hole", new Color(0.02f, 0.02f, 0.02f), 0f, 0.1f), Color.white, q, false);
                for (int k = 0; k < 7; k++)
                {
                    float s = r.Range(0.08f, 0.22f);
                    b.Box(at + q * new Vector3(r.Range(-0.8f, 0.8f), s * 0.4f, r.Range(-1.4f, 1.4f)), new Vector3(s, s * 0.7f, s), rubble, Color.white, Quaternion.Euler(r.Range(0f, 40f), r.Range(0f, 180f), r.Range(0f, 40f)), false);
                }
                foreach (float rx in new[] { -0.45f, 0.05f, 0.5f })   // rebar sticking out of the broken edge
                    b.Cyl(at + q * new Vector3(rx, 1.34f, -0.92f), at + q * new Vector3(rx + r.Range(-0.1f, 0.1f), 1.62f, -1.22f), 0.012f, 0.012f, 4, metal, new Color(0.35f, 0.2f, 0.12f), false);
            }
        }
    }
}
