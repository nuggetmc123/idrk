using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Builds armor and backpack meshes. Helmets are built in head-bone space, vests and packs in chest-bone space,
    /// so the same mesh is used for the worn version (parented to the bone) and the loot item (centred).
    /// </summary>
    public static class ArmorBuilder
    {
        static Material Shell => Mats.Lit("armor_shell", Color.white, 0.12f, 0.42f, Mats.Noise, null, 0.35f, 0.35f);
        static Material Fit => Mats.Lit("armor_fit", Color.white, 0.6f, 0.45f, Mats.Metal, null, 0.25f, 0.3f);
        static Material Cloth => Mats.Lit("armor_cloth", Color.white, 0f, 0.15f, Mats.Fabric, Mats.FabricN, 0.4f, 0.05f);
        static Material Visor => Mats.Lit("armor_visor", new Color(0.05f, 0.07f, 0.09f), 0.6f, 0.92f);
        static readonly Color Dark = new Color(0.1f, 0.1f, 0.11f);

        public static MB Helmet(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = Lin(d.color), t = Lin(d.trim);
            float s = d.style == 4 ? 1.1f : d.style == 0 ? 0.96f : 1f;
            float cut = d.style == 2 ? 0.48f : 0.55f;
            mb.Sub(0).Sphere(new Vector3(0, 0.105f, -0.002f), new Vector3(0.122f, 0.118f, 0.132f) * s, 18, c, 0f, cut);
            mb.Sub(0).Cyl(new Vector3(0, 0.1f - 0.005f * s, -0.002f), new Vector3(0, 0.108f, -0.002f), 0.123f * s, 0.123f * s, 18, c * 0.85f, false); // rim
            switch (d.style)
            {
                case 0: // bump helmet: vents + strap
                    for (int i = -1; i <= 1; i++) mb.Sub(1).Box(new Vector3(i * 0.035f, 0.215f, -0.01f), new Vector3(0.018f, 0.006f, 0.05f), Dark, 0.002f);
                    break;
                case 1: // ballistic: ear pro + chin strap
                    Ears(mb);
                    break;
                case 2: // FAST: rails, NVG shroud, velcro
                    Ears(mb);
                    mb.Sub(1).Box(new Vector3(-0.118f, 0.115f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), Dark, 0.003f);
                    mb.Sub(1).Box(new Vector3(0.118f, 0.115f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), Dark, 0.003f);
                    mb.Sub(1).Box(new Vector3(0, 0.16f, 0.118f), new Vector3(0.05f, 0.035f, 0.02f), Dark, 0.005f);
                    mb.Sub(2).Box(new Vector3(0, 0.205f, -0.04f), new Vector3(0.07f, 0.004f, 0.05f), t, 0.002f);
                    break;
                case 3: // assault visor
                    Ears(mb);
                    mb.Sub(3).Sphere(new Vector3(0, 0.07f, 0.03f), new Vector3(0.118f, 0.1f, 0.115f), 16, Color.white, 0.25f, 0.62f);
                    mb.Sub(1).Box(new Vector3(0, 0.165f, 0.12f), new Vector3(0.16f, 0.02f, 0.03f), t, 0.005f);
                    break;
                default: // juggernaut: full face plate with eye slit and gold trim
                    mb.Sub(0).Box(new Vector3(0, 0.05f, 0.1f), new Vector3(0.2f, 0.16f, 0.06f), c, 0.03f, 2);
                    mb.Sub(3).Box(new Vector3(0, 0.08f, 0.132f), new Vector3(0.15f, 0.022f, 0.01f), Color.white, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.215f, 0.0f), new Vector3(0.03f, 0.012f, 0.24f), t, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.045f, 0.134f), new Vector3(0.19f, 0.01f, 0.006f), t, 0.002f);
                    Ears(mb);
                    break;
            }
            return mb;
        }

        static void Ears(MB mb)
        {
            mb.Sub(1).Box(new Vector3(-0.115f, 0.07f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
            mb.Sub(1).Box(new Vector3(0.115f, 0.07f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
        }

        public static MB Vest(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = Lin(d.color), c2 = Lin(d.color * 0.8f), t = Lin(d.trim);
            c2.a = 1;
            float thick = d.style == 0 ? 0.035f : d.style >= 2 ? 0.07f : 0.06f;
            mb.Sub(2).Box(new Vector3(0, 0.04f, 0.12f), new Vector3(0.3f, 0.32f, thick), c, 0.02f, 2);
            mb.Sub(2).Box(new Vector3(0, 0.04f, -0.12f), new Vector3(0.3f, 0.32f, thick * 0.9f), c, 0.02f, 2);
            mb.Sub(2).Box(new Vector3(-0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), c2, 0.01f);
            mb.Sub(2).Box(new Vector3(0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), c2, 0.01f);
            mb.Sub(2).Box(new Vector3(-0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), c2, 0.01f);
            mb.Sub(2).Box(new Vector3(0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), c2, 0.01f);
            if (d.style >= 1)
            {
                int pouches = d.style >= 3 ? 4 : 3;
                for (int i = 0; i < pouches; i++) mb.Sub(2).Box(new Vector3(-0.09f * (pouches - 1) / 2f + i * 0.09f * (3f / pouches), -0.06f, 0.12f + thick * 0.5f + 0.018f), new Vector3(0.07f, 0.12f, 0.036f), c2, 0.012f);
                mb.Sub(2).Box(new Vector3(0, 0.12f, 0.12f + thick * 0.5f + 0.012f), new Vector3(0.16f, 0.06f, 0.025f), c2, 0.01f);
            }
            if (d.style >= 2)
            {
                // shoulder pads + collar
                mb.Sub(0).Box(new Vector3(-0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, -25));
                mb.Sub(0).Box(new Vector3(0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, 25));
                mb.Sub(0).Box(new Vector3(0, 0.22f, -0.04f), new Vector3(0.22f, 0.05f, 0.14f), c2, 0.02f, 2);
            }
            if (d.style >= 3) mb.Sub(2).Box(new Vector3(0, -0.2f, 0.125f), new Vector3(0.16f, 0.14f, 0.03f), c, 0.012f, 2); // groin flap
            if (d.style >= 4)
            {
                mb.Sub(0).Box(new Vector3(0, 0.04f, 0.16f), new Vector3(0.32f, 0.34f, 0.03f), c, 0.02f, 2);
                mb.Sub(1).Box(new Vector3(0, 0.21f, 0.18f), new Vector3(0.3f, 0.012f, 0.012f), t, 0.003f);
                mb.Sub(1).Box(new Vector3(0, -0.13f, 0.18f), new Vector3(0.3f, 0.012f, 0.012f), t, 0.003f);
            }
            mb.Sub(1).Box(new Vector3(0.1f, 0.15f, 0.12f + thick * 0.5f + 0.002f), new Vector3(0.04f, 0.035f, 0.004f), t, 0.002f); // patch
            return mb;
        }

        // =========================================================================================== BACKPACKS
        // Soft military rucksacks. One shared body builder is used for the bag you hold open in front of you
        // (mouth up, back panel towards -z) and for the bag worn on a character's back (flap closed).
        public const float BagWall = 0.014f;
        public const float BagCollar = 0.09f;   // fabric throat above the rim; the flap is hinged at its back
        public static Material[] BagMats => new[] { Cloth, Mats.Lit("pack_strap", Color.white, 0f, 0.2f, Mats.Fabric, null, 0.3f, 0f), Fit };

        /// <summary>Vertex colours are linear: convert the palette colours so gear doesn't come out pastel.</summary>
        static Color Lin(Color c) => new Color(Mathf.Pow(Mathf.Clamp01(c.r), 2.2f), Mathf.Pow(Mathf.Clamp01(c.g), 2.2f), Mathf.Pow(Mathf.Clamp01(c.b), 2.2f), 1f);

        /// <summary>Outer size of the bag body (the inside compartment is d.inner).</summary>
        public static Vector3 BagOuter(BackpackDef d) => new Vector3(d.inner.x * 1.16f + BagWall * 2, d.inner.y, d.inner.z * 1.16f + BagWall * 2);
        /// <summary>Where the flap is hinged (back of the collar), in bag space.</summary>
        public static Vector3 BagHinge(BackpackDef d) => new Vector3(0, d.inner.y + BagCollar * 0.75f, -BagOuter(d).z * 0.5f + 0.004f);

        // rounded-rectangle ("squircle") cross-section, angle t from +x towards +z
        static Vector3 Sq(float t, float a, float b)
        {
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            return new Vector3(a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.4f), 0, b * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.4f));
        }

        static void BagBody(MB mb, BackpackDef d, bool open, int cloth, int strap)
        {
            Vector3 n = d.inner;
            float H = n.y, a = n.x * 0.58f + BagWall, b = n.z * 0.58f + BagWall;
            Color c = Lin(d.color), c2 = Lin(d.color * 0.82f), c3 = Lin(d.color * 0.68f), dk = Lin(new Color(0.15f, 0.15f, 0.14f)), liner = Lin(new Color(0.11f, 0.11f, 0.1f));
            const int NU = 44;
            // body profile: rounded bottom, belly bulge, slightly narrower mouth
            float[] ys = { -0.04f, -0.036f, -0.022f, 0f, H * 0.3f, H * 0.6f, H * 0.84f, H };
            float[] sc = { 0.08f, 0.6f, 0.88f, 0.97f, 1.03f, 1.04f, 1.0f, 0.96f };
            System.Func<int, int, Vector3> outer = (u, v) =>
            {
                float t = u / (float)NU * Mathf.PI * 2f;
                var q = Sq(t, a * sc[v], b * sc[v]);
                if (q.z < 0) q.z *= 0.9f;                       // back panel stays flatter
                else if (v >= 3 && v < 7) q.z *= 1.05f;          // front bellies out
                float wr = v >= 3 ? 0.0022f * Mathf.Sin(t * 9f + v * 1.7f) : 0f; // soft fabric wrinkles
                q += new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)) * wr;
                q.y = ys[v];
                return q;
            };
            mb.Sub(cloth).Patch(NU, ys.Length - 1, outer, c, true, false, 5f, 2f);
            // rim piping
            mb.Sub(strap).Patch(NU, 8, (u, v) =>
            {
                var r = outer(u, 7); var rad = new Vector3(r.x, 0, r.z).normalized; float ph = v / 8f * Mathf.PI * 2f;
                return r + rad * (0.009f * Mathf.Cos(ph)) + Vector3.up * (0.009f * Mathf.Sin(ph));
            }, dk, true);
            if (open)
            {
                // inside: dark liner, same shape inset by the wall, plus the floor
                float ka = (a - BagWall) / a, kb = (b - BagWall) / b;
                System.Func<int, int, Vector3> inner = (u, v) => { var q = outer(u, v + 3); q.x *= ka; q.z *= kb; return q; };
                mb.Sub(cloth).Patch(NU, 4, inner, liner, true, true, 5f, 2f);
                mb.Sub(cloth).Patch(NU, 1, (u, v) => v == 0 ? new Vector3(0, 0.002f, 0) : inner(u, 0) + Vector3.up * 0.002f, liner, true, true);
            }
            // cinch collar: open = loose and flared, closed = drawn tight under the flap
            float ch = BagCollar;
            float[] cy = { H, H + ch * 0.45f, H + ch };
            float[] cs = open ? new[] { 0.96f, 0.99f, 1.03f } : new[] { 0.96f, 0.86f, 0.62f };
            System.Func<int, int, Vector3> collar = (u, v) =>
            {
                float t = u / (float)NU * Mathf.PI * 2f;
                var q = Sq(t, a * cs[v], b * cs[v]);
                if (q.z < 0) q.z *= 0.9f;
                q += new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)) * ((0.003f + v * 0.004f) * Mathf.Sin(t * 14f + v));
                q.y = cy[v];
                return q;
            };
            mb.Sub(cloth).Patch(NU, 2, collar, c3, true, false, 5f, 1f);
            mb.Sub(cloth).Patch(NU, 2, collar, liner, true, true, 5f, 1f);
            mb.Sub(strap).Patch(NU, 6, (u, v) =>
            {
                var r = collar(u, 2); var rad = new Vector3(r.x, 0, r.z).normalized; float ph = v / 6f * Mathf.PI * 2f;
                return r + rad * (0.0045f * Mathf.Cos(ph)) + Vector3.up * (0.0045f * Mathf.Sin(ph));
            }, dk, true);
            mb.Sub(strap).Box(collar(NU / 4, 2) + new Vector3(0.03f, -0.015f, 0.008f), new Vector3(0.018f, 0.028f, 0.014f), dk, 0.005f); // cord lock

            // front pocket (pillow) with a zip, MOLLE rows on tactical bags
            float fz = b * 1.02f;
            Vector3 pc = new Vector3(0, H * 0.36f, fz + 0.026f);
            Vector3 ps = new Vector3(a * 1.42f, H * 0.5f, 0.06f);
            mb.Sub(cloth).Box(pc, ps, c2, 0.028f, 2);
            mb.Sub(strap).Cyl(pc + new Vector3(-ps.x * 0.4f, ps.y * 0.42f, 0.03f), pc + new Vector3(ps.x * 0.4f, ps.y * 0.42f, 0.03f), 0.0035f, 0.0035f, 6, dk, false);
            mb.Sub(strap).Box(pc + new Vector3(ps.x * 0.38f, ps.y * 0.36f, 0.033f), new Vector3(0.012f, 0.03f, 0.005f), dk, 0.002f);
            if (d.style >= 1) for (int k = 0; k < 3; k++)
                    mb.Sub(strap).Box(pc + new Vector3(0, -ps.y * 0.3f + k * ps.y * 0.2f, 0.031f), new Vector3(ps.x * 0.86f, 0.012f, 0.004f), dk, 0.001f);
            if (d.style >= 3) // admin pouch above the main pocket
            {
                mb.Sub(cloth).Box(new Vector3(0, H * 0.74f, fz + 0.016f), new Vector3(a * 0.95f, H * 0.2f, 0.04f), c2, 0.018f, 2);
                mb.Sub(strap).Box(new Vector3(0, H * 0.79f, fz + 0.038f), new Vector3(0.06f, 0.035f, 0.004f), Lin(new Color(0.55f, 0.5f, 0.4f)), 0.002f); // name tape
            }
            // side bottle pouches
            if (d.style >= 2) foreach (var sx in new[] { -1, 1 })
                {
                    float r = Mathf.Min(0.05f, b * 0.45f);
                    mb.Sub(cloth).Cyl(new Vector3(sx * (a + r * 0.55f), H * 0.05f, 0), new Vector3(sx * (a + r * 0.55f), H * 0.4f, 0), r, r, 14, c2, true);
                    mb.Sub(strap).Cyl(new Vector3(sx * (a + r * 0.55f), H * 0.33f, 0), new Vector3(sx * (a + r * 0.55f), H * 0.345f, 0), r + 0.003f, r + 0.003f, 14, dk, false);
                }
            // compression straps round the sides: this is where the rifles get strapped
            foreach (var sx in new[] { -1, 1 })
                foreach (var hy in new[] { 0.5f, 0.82f })
                {
                    mb.Sub(strap).Box(new Vector3(sx * (a * 1.02f + 0.003f), H * hy, 0), new Vector3(0.008f, 0.022f, b * 1.75f), dk, 0.003f);
                    mb.Sub(2).Box(new Vector3(sx * (a * 1.02f + 0.008f), H * hy, b * 0.45f), new Vector3(0.01f, 0.03f, 0.03f), dk, 0.004f); // buckle
                }
            // back: padded shoulder straps (S-curve), haul loop, hip belt on the big ones
            float bz = -b * 0.9f;
            foreach (var sx in new[] { -1, 1 })
            {
                float x = sx * a * 0.42f;
                mb.Sub(cloth).Box(new Vector3(x, H * 0.82f, bz - 0.03f), new Vector3(0.062f, H * 0.3f, 0.024f), c3, 0.011f, 2, Quaternion.Euler(18, 0, 0));
                mb.Sub(cloth).Box(new Vector3(x, H * 0.5f, bz - 0.045f), new Vector3(0.062f, H * 0.34f, 0.024f), c3, 0.011f, 2);
                mb.Sub(strap).Box(new Vector3(x, H * 0.24f, bz - 0.03f), new Vector3(0.028f, H * 0.22f, 0.008f), dk, 0.003f, 1, Quaternion.Euler(-20, 0, 0));
            }
            mb.Sub(strap).Patch(10, 6, (u, v) =>
            {
                float t = u / 10f * Mathf.PI; float ph = v / 6f * Mathf.PI * 2f;
                var cen = new Vector3(Mathf.Cos(t) * 0.035f, H + 0.005f + Mathf.Sin(t) * 0.03f, bz - 0.012f);
                var rad = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0);
                return cen + rad * (0.006f * Mathf.Cos(ph)) + Vector3.forward * (0.006f * Mathf.Sin(ph));
            }, dk);
            if (d.style >= 4)
            {
                mb.Sub(cloth).Box(new Vector3(0, H * 0.06f, bz - 0.05f), new Vector3(a * 2.3f, 0.09f, 0.03f), c3, 0.014f, 2); // hip belt
                // rolled sleeping mat strapped under the bag
                float rr = Mathf.Min(0.065f, H * 0.12f);
                mb.Sub(cloth).Cyl(new Vector3(-a * 0.95f, -0.04f - rr, 0), new Vector3(a * 0.95f, -0.04f - rr, 0), rr, rr, 16, Lin(new Color(0.22f, 0.24f, 0.2f)), true);
                foreach (var sx in new[] { -0.55f, 0.55f }) mb.Sub(strap).Cyl(new Vector3(sx * a, -0.04f - rr, 0), new Vector3(sx * a + 0.025f, -0.04f - rr, 0), rr + 0.004f, rr + 0.004f, 16, dk, false);
            }
            if (d.style >= 5)
            {
                // hauling frame
                var fr = Lin(new Color(0.32f, 0.32f, 0.34f));
                foreach (var sx in new[] { -1, 1 }) mb.Sub(2).Cyl(new Vector3(sx * a * 0.85f, -0.12f, bz - 0.07f), new Vector3(sx * a * 0.85f, H + 0.16f, bz - 0.07f), 0.011f, 0.011f, 8, fr, true);
                foreach (var hy in new[] { -0.1f, H + 0.15f }) mb.Sub(2).Cyl(new Vector3(-a * 0.85f, hy, bz - 0.07f), new Vector3(a * 0.85f, hy, bz - 0.07f), 0.011f, 0.011f, 8, fr, true);
            }
        }

        static void BagFlapInto(MB mb, BackpackDef d, int cloth, int strap)
        {
            var o = BagOuter(d);
            float W = o.x * 1.04f, D = o.z * 1.06f;
            Color c2 = Lin(d.color * 0.82f), dk = Lin(new Color(0.15f, 0.15f, 0.14f));
            // padded lid with a zip pocket, two buckle straps hanging off the front edge
            mb.Sub(cloth).Box(new Vector3(0, 0.022f, D * 0.5f), new Vector3(W, 0.06f, D), c2, 0.028f, 2);
            mb.Sub(strap).Cyl(new Vector3(-W * 0.32f, 0.053f, D * 0.62f), new Vector3(W * 0.32f, 0.053f, D * 0.62f), 0.0035f, 0.0035f, 6, dk, false);
            mb.Sub(strap).Box(new Vector3(W * 0.3f, 0.054f, D * 0.66f), new Vector3(0.012f, 0.005f, 0.028f), dk, 0.002f);
            foreach (var sx in new[] { -1, 1 })
            {
                mb.Sub(strap).Box(new Vector3(sx * W * 0.24f, 0.05f, D * 0.6f), new Vector3(0.026f, 0.006f, D * 0.8f), dk, 0.002f);
                mb.Sub(strap).Box(new Vector3(sx * W * 0.24f, -0.02f, D + 0.006f), new Vector3(0.026f, 0.1f, 0.006f), dk, 0.002f);
                mb.Sub(2).Box(new Vector3(sx * W * 0.24f, -0.06f, D + 0.012f), new Vector3(0.034f, 0.026f, 0.012f), dk, 0.004f);
            }
        }

        /// <summary>An open rucksack (origin = centre of the inside floor, mouth up, back panel towards -z).</summary>
        public static MB OpenBag(BackpackDef d) { var mb = new MB(3); BagBody(mb, d, true, 0, 1); return mb; }

        /// <summary>The bag's padded flap, hinged at the back of the collar (origin on the hinge, lid extends towards +z).</summary>
        public static MB BagFlap(BackpackDef d) { var mb = new MB(3); BagFlapInto(mb, d, 0, 1); return mb; }

        /// <summary>Backpack worn on a character's back (chest-bone space), flap closed.</summary>
        public static MB Pack(BackpackDef d)
        {
            var mb = new MB(4);
            var o = BagOuter(d);
            float top = 0.2f, y0 = top - (d.inner.y + BagCollar * 0.75f);
            mb.Push(new Vector3(0, y0, -0.13f - o.z * 0.5f), Quaternion.Euler(0, 180, 0));
            BagBody(mb, d, false, 2, 2);
            mb.Push(BagHinge(d), Quaternion.Euler(8, 0, 0));
            BagFlapInto(mb, d, 2, 2);
            mb.Pop(); mb.Pop();
            return mb;
        }

        public static Material[] Materials => new[] { Shell, Fit, Cloth, Visor };

        /// <summary>Attach a worn piece of gear to a bone. Returns the created object (so it can be removed later).</summary>
        public static GameObject Attach(MB mb, Transform bone, string name, bool shadowOnly)
        {
            var go = mb.Build(name, bone, Materials);
            if (shadowOnly) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            return go;
        }

        /// <summary>Applies helmet / vest / backpack visuals to a character.</summary>
        public static void Dress(HumanRig rig, ArmorDef helmet, ArmorDef vest, BackpackDef pack, bool local)
        {
            foreach (var g in rig.gearVisuals) if (g != null) { rig.headRenderers.Remove(g.GetComponent<Renderer>()); Compat.Kill(g); }
            rig.headRenderers.RemoveAll(r => r == null);
            rig.gearVisuals.Clear();
            if (helmet != null)
            {
                var g = Attach(Helmet(helmet), rig.head, "Helmet", local);
                rig.gearVisuals.Add(g);
                if (local) rig.headRenderers.Add(g.GetComponent<Renderer>());
            }
            if (vest != null)
            {
                // your own vest sits right under your chin in VR and blocks the view: like your helmet, you only see its
                // shadow (everyone else sees it normally, and it shows again on your body if you die)
                var g = Attach(Vest(vest), rig.chest, "Vest", local);
                rig.gearVisuals.Add(g);
                if (local) rig.headRenderers.Add(g.GetComponent<Renderer>());
            }
            if (pack != null) rig.gearVisuals.Add(Attach(Pack(pack), rig.chest, "Pack", false));
        }
    }

    /// <summary>A helmet or vest lying in the world. Bring it to your head / chest and let go to put it on.</summary>
    public class ArmorItem : Item
    {
        public ArmorDef ad;
        public override bool SnapGrip => false;

        public static ArmorItem Create(ArmorDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var a = go.AddComponent<ArmorItem>();
            a.def = d; a.ad = d;
            var mb = d.slot == ArmorSlot.Helmet ? ArmorBuilder.Helmet(d) : ArmorBuilder.Vest(d);
            var model = mb.Build("Model", go.transform, ArmorBuilder.Materials);
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            model.transform.localPosition = -mesh.bounds.center;
            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.04f);
            a.localBounds = new Bounds(Vector3.zero, mesh.bounds.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            a.SetupPhysics(d.slot == ArmorSlot.Helmet ? 1.4f : 4f);
            return a;
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead && pl.Gear.TryWear(this, h)) return true;
            return base.TrySlot(h);
        }
    }
}
