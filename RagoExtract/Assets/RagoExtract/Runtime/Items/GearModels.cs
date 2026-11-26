using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Detailed models for worn gear and hand weapons other than guns: helmets and vests (submeshes 0 shell / plates,
    /// 1 fittings / metal, 2 cloth / webbing, 3 visor glass), grenades (0 body, 1 metal) and melee weapons (knives: 0 grip,
    /// 1 steel; long ones: 0 wood / paint, 1 steel, 2 tape). Sizes and pivots match the old simple models exactly, so
    /// colliders, grips, hit points and where gear sits on the body are unchanged. No game logic here, so the same code can
    /// be rendered outside the game to check how they look.
    /// </summary>
    public static class GearModels
    {
        static readonly Color Dark = new Color(0.1f, 0.1f, 0.11f), Black = new Color(0.05f, 0.05f, 0.055f), Steel = new Color(0.62f, 0.63f, 0.66f),
            Rubber = new Color(0.13f, 0.13f, 0.13f), Webbing = new Color(0.2f, 0.2f, 0.19f);
        public static Color Lin(Color c) => new Color(Mathf.Pow(Mathf.Clamp01(c.r), 2.2f), Mathf.Pow(Mathf.Clamp01(c.g), 2.2f), Mathf.Pow(Mathf.Clamp01(c.b), 2.2f), 1f);
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        // ================================================================================================ HELMETS
        /// <summary>Head-bone space: the shell is centred at (0, 0.105, 0), the face towards +z.</summary>
        public static MB Helmet(ArmorDef d) => d.tier > 0 ? HelmetTier(d) : HelmetClassic(d);

        /// <summary>The original helmet models - the boss helmets keep these.</summary>
        static MB HelmetClassic(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = Lin(d.color), t = Lin(d.trim), shade = Mul(c, 0.8f);
            float s = d.style == 4 ? 1.1f : d.style == 0 ? 0.96f : 1f;
            Vector3 o = new Vector3(0, 0.105f, -0.002f);
            Vector3 r = new Vector3(0.122f, 0.118f, 0.132f) * s;
            if (d.style == 1)
            {
                // a classic combat helmet: deeper, the brim flaring out a little all the way round
                mb.Sub(0).Sphere(o, r, 22, c, 0f, 0.56f);
                mb.Sub(0).Push(o + Vector3.down * 0.004f, Quaternion.identity, new Vector3(1f, 1f, r.z / r.x));
                mb.Sub(0).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(r.x * 0.985f, 0.012f), new Vector2(r.x * 1.03f, -0.006f), new Vector2(r.x * 1.07f, -0.016f), new Vector2(r.x * 1.065f, -0.021f), new Vector2(r.x * 0.97f, -0.012f) }, 26, shade);
                mb.Pop();
            }
            else
            {
                float cut = d.style == 2 ? 0.48f : 0.55f;
                mb.Sub(0).Sphere(o, r, 22, c, 0f, cut);
                // rolled edge trim round the rim
                mb.Sub(2).Push(o + Vector3.down * (d.style == 2 ? 0.0f : 0.004f), Quaternion.identity, new Vector3(1f, 1f, r.z / r.x));
                mb.Sub(2).Tube(new Vector3(0, -0.006f, 0), new Vector3(0, 0.004f, 0), r.x + 0.003f, r.x - 0.006f, 26, Rubber);
                mb.Pop();
            }
            // the liner and pads inside (seen from below / when it's lying on the ground)
            mb.Sub(2).Sphere(o + Vector3.down * 0.004f, r * 0.9f, 16, Webbing, 0f, 0.5f);
            foreach (var p in new[] { new Vector3(0, 0.07f, 0), new Vector3(0.06f, 0.04f, 0.05f), new Vector3(-0.06f, 0.04f, 0.05f), new Vector3(0.07f, 0.03f, -0.05f), new Vector3(-0.07f, 0.03f, -0.05f) })
                mb.Sub(2).Sphere(o + p * s, new Vector3(0.028f, 0.012f, 0.028f), 8, Mul(Webbing, 0.8f));
            // chin strap hanging from the sides with a buckle
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Sub(2).Box(new Vector3(x * 0.112f * s, 0.06f, 0.035f), new Vector3(0.004f, 0.06f, 0.016f), Webbing, 0.001f, 1, Quaternion.Euler(14f, 0, 0));
                mb.Sub(2).Box(new Vector3(x * 0.112f * s, 0.06f, -0.03f), new Vector3(0.004f, 0.06f, 0.016f), Webbing, 0.001f, 1, Quaternion.Euler(-14f, 0, 0));
            }
            mb.Sub(1).Box(new Vector3(0.112f * s, 0.022f, 0.002f), new Vector3(0.008f, 0.018f, 0.024f), Black, 0.003f);
            switch (d.style)
            {
                case 0: // bump helmet: rows of vents, thin side rails, a rear dial, a front shroud plate
                    for (int row = 0; row < 2; row++)
                        for (int i = -2; i <= 2; i++)
                        {
                            float a = i * 0.32f, el = 0.95f - row * 0.3f;
                            Vector3 n = new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el) * (row == 0 ? 0.2f : -0.6f)).normalized;
                            mb.Sub(1).Sphere(o + Vector3.Scale(n, r) * 0.99f, new Vector3(0.011f, 0.004f, 0.02f), 8, Black);
                        }
                    Rails(mb, o, s, 0.05f, Dark);
                    mb.Sub(1).Cyl(new Vector3(0, 0.07f, -0.128f * s), new Vector3(0, 0.07f, -0.142f * s), 0.016f, 0.015f, 14, Black);   // fit dial
                    for (int i = 0; i < 10; i++) { float a = i * 36f * Mathf.Deg2Rad; mb.Sub(1).Box(new Vector3(Mathf.Cos(a) * 0.016f, 0.07f + Mathf.Sin(a) * 0.016f, -0.136f * s), new Vector3(0.003f, 0.003f, 0.012f), Black, 0f); }
                    Shroud(mb, s, Dark);
                    break;
                case 1: // ballistic: ear defenders, a cat-eye band round the cover with its reflective tabs
                    Ears(mb, s, false);
                    mb.Sub(2).Push(o + Vector3.up * 0.03f, Quaternion.identity, new Vector3(1f, 1f, r.z / r.x));
                    mb.Sub(2).Tube(new Vector3(0, -0.012f, 0), new Vector3(0, 0.012f, 0), r.x * 0.985f + 0.004f, r.x * 0.97f, 26, Black);
                    mb.Pop();
                    mb.Sub(3).Box(new Vector3(0, 0.133f, -0.128f), new Vector3(0.04f, 0.008f, 0.004f), new Color(0.9f, 0.8f, 0.3f), 0.001f);   // cat eyes
                    break;
                case 2: // FAST: ARC rails with slots, a NVG shroud, velcro panels, a counterweight pouch, bungee cords
                    Ears(mb, s, true);
                    Rails(mb, o, s, 0.03f, Dark);
                    Shroud(mb, s, Dark);
                    mb.Sub(2).Box(new Vector3(0, 0.218f, -0.03f), new Vector3(0.075f, 0.006f, 0.06f), t, 0.002f, 1, Quaternion.Euler(-12f, 0, 0));   // velcro top
                    mb.Sub(2).Box(new Vector3(0, 0.15f, -0.125f), new Vector3(0.07f, 0.045f, 0.006f), t, 0.002f, 1, Quaternion.Euler(28f, 0, 0));   // velcro back
                    mb.Sub(2).Box(new Vector3(0, 0.09f, -0.13f), new Vector3(0.1f, 0.05f, 0.03f), Mul(t, 0.8f), 0.01f, 2);   // counterweight pouch
                    mb.Sub(1).Box(new Vector3(0, 0.09f, -0.1465f), new Vector3(0.05f, 0.012f, 0.002f), Black, 0f);
                    foreach (float x in new[] { -1f, 1f }) mb.Sub(2).Capsule(new Vector3(x * 0.1f, 0.13f, 0.07f), new Vector3(x * 0.1f, 0.15f, -0.08f), 0.0025f, 5, Black);   // bungees
                    break;
                case 3: // assault visor: a big tinted visor on hinge bolts, a brow guard, rails, a strobe on top
                    Ears(mb, s, true);
                    Rails(mb, o, s, 0.03f, Dark);
                    mb.Sub(3).Sphere(new Vector3(0, 0.07f, 0.03f), new Vector3(0.118f, 0.1f, 0.115f), 22, Color.white, 0.25f, 0.62f);
                    mb.Sub(1).Box(new Vector3(0, 0.165f, 0.12f), new Vector3(0.17f, 0.022f, 0.03f), t, 0.006f, 2);   // brow guard
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(1).Cyl(new Vector3(x * 0.118f, 0.12f, 0.06f), new Vector3(x * 0.132f, 0.12f, 0.06f), 0.014f, 0.013f, 14, t);   // hinges
                        mb.Sub(1).Cyl(new Vector3(x * 0.131f, 0.12f, 0.06f), new Vector3(x * 0.135f, 0.12f, 0.06f), 0.006f, 0.006f, 6, Steel);
                    }
                    mb.Sub(1).Box(new Vector3(0, 0.226f, -0.06f), new Vector3(0.022f, 0.012f, 0.03f), Black, 0.003f);   // strobe
                    mb.Sub(3).Box(new Vector3(0, 0.234f, -0.06f), new Vector3(0.014f, 0.005f, 0.02f), new Color(0.3f, 0.9f, 1f), 0.002f);
                    break;
                default: // juggernaut: a full face plate with an eye slit, rivets, a ridge crest and gold trim
                    Ears(mb, s, false);
                    mb.Sub(0).Box(new Vector3(0, 0.05f, 0.1f), new Vector3(0.2f, 0.16f, 0.06f), c, 0.03f, 2);
                    mb.Sub(0).Box(new Vector3(0, 0.02f, 0.128f), new Vector3(0.12f, 0.06f, 0.02f), shade, 0.015f, 2);   // jaw guard
                    for (int i = -2; i <= 2; i++) mb.Sub(1).Box(new Vector3(i * 0.018f, 0.01f, 0.139f), new Vector3(0.008f, 0.03f, 0.004f), Black, 0.001f);   // breathing slots
                    mb.Sub(3).Box(new Vector3(0, 0.08f, 0.132f), new Vector3(0.15f, 0.022f, 0.01f), Color.white, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.215f * s, 0.0f), new Vector3(0.03f, 0.016f, 0.24f), t, 0.005f, 2);   // crest
                    mb.Sub(1).Box(new Vector3(0, 0.045f, 0.134f), new Vector3(0.19f, 0.01f, 0.006f), t, 0.002f);
                    mb.Sub(1).Box(new Vector3(0, 0.097f, 0.134f), new Vector3(0.19f, 0.008f, 0.006f), t, 0.002f);
                    for (int i = 0; i < 6; i++) { float x = -0.085f + i * 0.034f; mb.Sub(1).Sphere(new Vector3(x, 0.127f, 0.133f - Mathf.Abs(x) * 0.15f), Vector3.one * 0.005f, 6, t); }
                    break;
            }
            return mb;
        }

        static void Ears(MB mb, float s, bool comms)
        {
            foreach (float x in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(x * 0.118f * s, 0.07f, 0.0f);
                mb.Sub(1).Push(c, Quaternion.Euler(0, 0, x * 90f), new Vector3(1f, 1f, 1.15f));
                mb.Sub(1).Cyl(new Vector3(0, -0.012f, 0), new Vector3(0, 0.022f, 0), 0.034f, 0.03f, 18, Rubber);   // cup
                mb.Sub(2).Tube(new Vector3(0, -0.02f, 0), new Vector3(0, -0.012f, 0), 0.036f, 0.022f, 18, Black);   // cushion
                mb.Sub(1).Cyl(new Vector3(0, 0.022f, 0), new Vector3(0, 0.028f, 0), 0.02f, 0.018f, 14, Dark);   // cap
                mb.Pop();
                mb.Sub(1).Box(c + new Vector3(x * 0.012f, 0.045f, 0), new Vector3(0.008f, 0.03f, 0.014f), Dark, 0.003f);   // arm to the rail
            }
            if (comms)
            {
                mb.Sub(1).Capsule(new Vector3(-0.13f * s, 0.06f, 0.02f), new Vector3(-0.09f, 0.025f, 0.11f), 0.0025f, 6, Black);   // boom mic
                mb.Sub(2).Sphere(new Vector3(-0.087f, 0.024f, 0.115f), new Vector3(0.008f, 0.006f, 0.01f), 8, Black);
                mb.Sub(1).Box(new Vector3(0.135f * s, 0.07f, 0.0f), new Vector3(0.008f, 0.02f, 0.02f), Black, 0.003f);   // push-to-talk
            }
        }

        static void Rails(MB mb, Vector3 o, float s, float yUp, Color col)
        {
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Sub(1).Box(new Vector3(x * 0.118f * s, o.y + yUp - 0.08f, 0.03f), new Vector3(0.012f, 0.025f, 0.11f), col, 0.003f);
                for (int i = 0; i < 4; i++) mb.Sub(1).Box(new Vector3(x * 0.1245f * s, o.y + yUp - 0.08f, -0.012f + i * 0.028f), new Vector3(0.002f, 0.012f, 0.014f), Black, 0f);   // slots
            }
        }

        static void Shroud(MB mb, float s, Color col)
        {
            mb.Sub(1).Box(new Vector3(0, 0.16f, 0.12f * s), new Vector3(0.055f, 0.04f, 0.02f), col, 0.006f, 1, Quaternion.Euler(-18f, 0, 0));
            mb.Sub(1).Box(new Vector3(0, 0.152f, 0.132f * s), new Vector3(0.024f, 0.016f, 0.008f), Black, 0.002f, 1, Quaternion.Euler(-18f, 0, 0));   // mount slot
            foreach (float x in new[] { -0.018f, 0f, 0.018f }) mb.Sub(1).Cyl(new Vector3(x, 0.175f, 0.126f * s), new Vector3(x, 0.178f, 0.132f * s), 0.0028f, 0.0028f, 6, Steel);   // screws
        }

        // ================================================================================================ VESTS
        /// <summary>Chest-bone space, the front towards +z. Panels front and back at z = ±0.12.</summary>
        public static MB Vest(ArmorDef d) => d.tier > 0 ? VestTier(d) : VestClassic(d);

        /// <summary>The original vest models - the boss vests keep these.</summary>
        static MB VestClassic(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = Lin(d.color), c2 = Mul(c, 0.8f), t = Lin(d.trim), seam = Mul(c, 0.6f);
            float thick = d.style == 0 ? 0.035f : d.style >= 2 ? 0.07f : 0.06f;
            float fz = 0.12f + thick * 0.5f;   // front face
            // front and back panels with the plates showing as a raised, rounded shape
            mb.Sub(2).Box(new Vector3(0, 0.04f, 0.12f), new Vector3(0.3f, 0.32f, thick), c, 0.02f, 2);
            mb.Sub(2).Box(new Vector3(0, 0.04f, -0.12f), new Vector3(0.3f, 0.32f, thick * 0.9f), c, 0.02f, 2);
            if (d.style >= 1)
            {
                mb.Sub(2).Box(new Vector3(0, 0.05f, fz + 0.002f), new Vector3(0.25f, 0.28f, 0.006f), c2, 0.01f, 2);
                mb.Sub(2).Box(new Vector3(0, 0.05f, -fz + 0.004f), new Vector3(0.25f, 0.28f, 0.006f), c2, 0.01f, 2);
            }
            else
            {
                // soft vest: quilted stitch lines
                for (int i = 0; i < 5; i++) mb.Sub(2).Box(new Vector3(0, -0.08f + i * 0.055f, fz + 0.0005f), new Vector3(0.27f, 0.002f, 0.002f), seam, 0f);
                for (int i = 0; i < 5; i++) mb.Sub(2).Box(new Vector3(0, -0.08f + i * 0.055f, -0.12f - thick * 0.45f - 0.0005f), new Vector3(0.27f, 0.002f, 0.002f), seam, 0f);
            }
            // cummerbund wrapping the sides, with its velcro tab
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Sub(2).Box(new Vector3(x * 0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), c2, 0.012f, 2);
                mb.Sub(2).Box(new Vector3(x * 0.155f, 0.0f, 0.1f), new Vector3(0.03f, 0.15f, 0.04f), c2, 0.01f, 1, Quaternion.Euler(0, x * 40f, 0));
                mb.Sub(2).Box(new Vector3(x * 0.155f, 0.0f, -0.1f), new Vector3(0.03f, 0.15f, 0.04f), c2, 0.01f, 1, Quaternion.Euler(0, x * -40f, 0));
                for (int i = 0; i < 3; i++) mb.Sub(2).Box(new Vector3(x * 0.1865f, -0.045f + i * 0.045f, 0), new Vector3(0.002f, 0.006f, 0.18f), seam, 0f);   // webbing rows
                // padded shoulder straps with a quick-release buckle
                mb.Sub(2).Box(new Vector3(x * 0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), c2, 0.012f, 2);
                mb.Sub(2).Box(new Vector3(x * 0.1f, 0.206f, 0), new Vector3(0.05f, 0.006f, 0.2f), seam, 0.002f);
                mb.Sub(1).Box(new Vector3(x * 0.1f, 0.16f, fz - 0.006f), new Vector3(0.036f, 0.022f, 0.01f), Black, 0.003f);
            }
            // MOLLE webbing rows across the front
            if (d.style >= 1)
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.012f + i * 0.026f;
                    mb.Sub(2).Box(new Vector3(0, y, fz + 0.007f), new Vector3(0.24f, 0.012f, 0.004f), Mul(c2, 0.85f), 0.001f);
                    for (int k = 0; k < 7; k++) mb.Sub(2).Box(new Vector3(-0.105f + k * 0.035f, y, fz + 0.0095f), new Vector3(0.002f, 0.013f, 0.002f), seam, 0f);   // tacking
                }
            if (d.style >= 1)
            {
                // mag pouches: each with a flap, a pull tab and the top of a magazine peeking out
                int pouches = d.style >= 3 ? 4 : 3;
                float pz = fz + 0.018f, step = 0.09f * (3f / pouches);
                for (int i = 0; i < pouches; i++)
                {
                    float x = -step * (pouches - 1) / 2f + i * step;
                    mb.Sub(2).Box(new Vector3(x, -0.06f, pz), new Vector3(step * 0.8f, 0.12f, 0.036f), c2, 0.012f, 2);
                    mb.Sub(2).Box(new Vector3(x, -0.005f, pz + 0.001f), new Vector3(step * 0.84f, 0.02f, 0.04f), Mul(c2, 0.92f), 0.008f, 1, Quaternion.Euler(-8f, 0, 0));   // flap
                    mb.Sub(1).Box(new Vector3(x, -0.022f, pz + 0.021f), new Vector3(0.014f, 0.022f, 0.004f), Black, 0.002f);   // pull tab
                    mb.Sub(2).Box(new Vector3(x, -0.06f, pz + 0.0185f), new Vector3(step * 0.6f, 0.002f, 0.002f), seam, 0f);   // stitching
                }
                // admin pouch with a light-reflective name tape
                mb.Sub(2).Box(new Vector3(0, 0.12f, fz + 0.012f), new Vector3(0.16f, 0.06f, 0.025f), c2, 0.01f, 2);
                mb.Sub(1).Box(new Vector3(0, 0.14f, fz + 0.025f), new Vector3(0.15f, 0.004f, 0.003f), Black, 0f);   // zip
                mb.Sub(2).Box(new Vector3(0, 0.115f, fz + 0.0255f), new Vector3(0.1f, 0.022f, 0.002f), t, 0.001f);
                // drag handle at the top of the back
                mb.Sub(2).Capsule(new Vector3(-0.04f, 0.2f, -fz - 0.004f), new Vector3(0.04f, 0.2f, -fz - 0.004f), 0.01f, 8, Webbing);
            }
            if (d.style >= 2)
            {
                // shoulder pads, a collar, a radio pouch with its antenna on the left
                mb.Sub(0).Box(new Vector3(-0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, -25));
                mb.Sub(0).Box(new Vector3(0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, 25));
                foreach (float x in new[] { -1f, 1f }) for (int i = 0; i < 3; i++) mb.Sub(2).Box(new Vector3(x * (0.18f + i * 0.015f), 0.192f - i * 0.007f, 0), new Vector3(0.003f, 0.003f, 0.15f), seam, 0f, 1, Quaternion.Euler(0, 0, x * 25f));
                mb.Sub(0).Box(new Vector3(0, 0.22f, -0.04f), new Vector3(0.22f, 0.05f, 0.14f), c2, 0.02f, 2);
                mb.Sub(2).Box(new Vector3(-0.2f, -0.02f, -0.02f), new Vector3(0.035f, 0.11f, 0.06f), c2, 0.01f, 2);
                mb.Sub(1).Box(new Vector3(-0.2f, 0.05f, -0.03f), new Vector3(0.02f, 0.03f, 0.03f), Black, 0.004f);
                mb.Sub(1).Capsule(new Vector3(-0.2f, 0.06f, -0.03f), new Vector3(-0.21f, 0.2f, -0.06f), 0.003f, 6, Black);
            }
            if (d.style >= 3)
            {
                mb.Sub(2).Box(new Vector3(0, -0.2f, 0.125f), new Vector3(0.16f, 0.14f, 0.03f), c, 0.012f, 2);   // groin flap
                mb.Sub(2).Box(new Vector3(0, -0.2f, 0.1405f), new Vector3(0.14f, 0.002f, 0.002f), seam, 0f);
                // a tourniquet pouch and a pistol mag pouch on the side
                mb.Sub(2).Box(new Vector3(0.2f, -0.02f, 0.02f), new Vector3(0.03f, 0.1f, 0.04f), c2, 0.01f, 2);
                mb.Sub(1).Box(new Vector3(0.2f, 0.035f, 0.02f), new Vector3(0.02f, 0.02f, 0.03f), new Color(0.75f, 0.1f, 0.08f), 0.004f);
            }
            if (d.style >= 4)
            {
                // juggernaut: an extra armoured front shell, gold trim and rivets
                mb.Sub(0).Box(new Vector3(0, 0.04f, 0.16f), new Vector3(0.32f, 0.34f, 0.03f), c, 0.02f, 2);
                mb.Sub(0).Box(new Vector3(0, 0.12f, 0.178f), new Vector3(0.26f, 0.12f, 0.012f), Mul(c, 1.15f), 0.012f, 2);
                mb.Sub(1).Box(new Vector3(0, 0.21f, 0.18f), new Vector3(0.3f, 0.012f, 0.012f), t, 0.003f);
                mb.Sub(1).Box(new Vector3(0, -0.13f, 0.18f), new Vector3(0.3f, 0.012f, 0.012f), t, 0.003f);
                for (int i = 0; i < 8; i++) { float x = -0.14f + i * 0.04f; mb.Sub(1).Sphere(new Vector3(x, 0.2f, 0.186f), Vector3.one * 0.005f, 6, t); mb.Sub(1).Sphere(new Vector3(x, -0.12f, 0.186f), Vector3.one * 0.005f, 6, t); }
            }
            mb.Sub(1).Box(new Vector3(0.1f, 0.15f, fz + 0.002f), new Vector3(0.04f, 0.035f, 0.004f), t, 0.002f);   // patch
            return mb;
        }

        // ================================================================================================ TIER ARMOUR (T1-T6)
        /// <summary>A curved band round the head (yaw th0..th1 degrees from the front, heights y0..y1, radius R(y)), both faces - mandibles, face plates, visors.</summary>
        static void Band(MB mb, int sub, float th0, float th1, float y0, float y1, System.Func<float, float> R, float zOff, int nu, int nv, Color col)
        {
            for (int side = 0; side < 2; side++)
            {
                float inset = side == 0 ? 0f : -0.004f;
                mb.Sub(sub).Patch(nu, nv, (u, v) =>
                {
                    float th = Mathf.Lerp(th0, th1, u / (float)nu) * Mathf.Deg2Rad, y = Mathf.Lerp(y0, y1, v / (float)nv), r = R(y) + inset;
                    return new Vector3(Mathf.Sin(th) * r, y, Mathf.Cos(th) * r * 1.06f + zOff);
                }, col, false, side == 0);
            }
        }

        static void TierShell(MB mb, Vector3 o, Vector3 r, float cut, Color c, bool edge)
        {
            mb.Sub(0).Sphere(o, r, 26, c, 0f, cut);
            if (edge)
            {
                // rubber edge trim following the rim
                float rimY = Mathf.Cos(cut * Mathf.PI) * r.y, rimR = Mathf.Sin(cut * Mathf.PI);
                mb.Sub(2).Push(o + new Vector3(0, rimY, 0), Quaternion.identity, new Vector3(1f, 1f, r.z / r.x));
                mb.Sub(2).Tube(new Vector3(0, -0.005f, 0), new Vector3(0, 0.004f, 0), r.x * rimR + 0.003f, r.x * rimR - 0.006f, 28, Rubber);
                mb.Pop();
            }
            mb.Sub(2).Sphere(o + Vector3.down * 0.004f, r * 0.9f, 16, Webbing, 0f, 0.5f);   // liner
            foreach (var p in new[] { new Vector3(0, 0.07f, 0), new Vector3(0.06f, 0.04f, 0.05f), new Vector3(-0.06f, 0.04f, 0.05f), new Vector3(0.07f, 0.03f, -0.05f), new Vector3(-0.07f, 0.03f, -0.05f) })
                mb.Sub(2).Sphere(o + p, new Vector3(0.028f, 0.012f, 0.028f), 8, Mul(Webbing, 0.8f));   // pads
        }

        static void ChinStrap(MB mb, float s)
        {
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Sub(2).Box(new Vector3(x * 0.112f * s, 0.06f, 0.035f), new Vector3(0.004f, 0.06f, 0.014f), Webbing, 0.001f, 1, Quaternion.Euler(14f, 0, 0));
                mb.Sub(2).Box(new Vector3(x * 0.112f * s, 0.06f, -0.03f), new Vector3(0.004f, 0.06f, 0.014f), Webbing, 0.001f, 1, Quaternion.Euler(-14f, 0, 0));
            }
            mb.Sub(1).Box(new Vector3(0.112f * s, 0.022f, 0.002f), new Vector3(0.008f, 0.018f, 0.024f), Black, 0.003f);
        }

        /// <summary>Tier helmets: 0 T1 scout bump, 1 T2 patrol (covered, goggles), 2 T3 raptor high-cut, 3 T4 ronin with mandible, 4 T5 vanguard visor, 5 T6 titan full-face.</summary>
        static MB HelmetTier(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = Lin(d.color), t = Lin(d.trim), shade = Mul(c, 0.78f);
            int st = Mathf.Clamp(d.style, 0, 5);
            float s = st == 5 ? 1.1f : st == 4 ? 1.03f : st == 0 ? 0.96f : 1f;
            Vector3 o = new Vector3(0, 0.105f, -0.002f), r = new Vector3(0.122f, 0.118f, 0.132f) * s;
            switch (st)
            {
                case 0: // T1 SCOUT BUMP: a light vented shell, a two-tone ridge, a rear dial, a small shroud
                    TierShell(mb, o, r, 0.5f, c, true);
                    ChinStrap(mb, s);
                    for (int row = 0; row < 2; row++)
                        for (int i = -2; i <= 2; i++)
                        {
                            float a = i * 0.3f, el = 0.95f - row * 0.32f;
                            Vector3 n = new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el) * (row == 0 ? 0.25f : -0.6f)).normalized;
                            mb.Sub(1).Sphere(o + Vector3.Scale(n, r) * 0.99f, new Vector3(0.011f, 0.004f, 0.02f), 8, Black);
                        }
                    for (int i = 0; i < 10; i++) { float a = Mathf.Lerp(0.25f, 2.6f, i / 9f); mb.Sub(1).Sphere(o + new Vector3(0, Mathf.Sin(a) * r.y, Mathf.Cos(a) * r.z) * 1.005f, new Vector3(0.012f, 0.006f, 0.014f), 8, t); }   // ridge
                    Rails(mb, o, s, 0.05f, Dark);
                    mb.Sub(1).Cyl(new Vector3(0, 0.07f, -0.126f * s), new Vector3(0, 0.07f, -0.14f * s), 0.016f, 0.015f, 14, Black);
                    Shroud(mb, s, Dark);
                    mb.Sub(2).Box(new Vector3(0.05f, 0.2f, -0.02f), new Vector3(0.04f, 0.004f, 0.03f), t, 0.001f, 1, Quaternion.Euler(-10f, 0, -20f));   // patch
                    break;
                case 1: // T2 PATROL: full cut with a flared brim, a fitted camo cover (flat printed patches), a band with a pair of goggles on the brow
                {
                    mb.Sub(2).Sphere(o, r, 28, c, 0f, 0.56f);   // the cloth cover over the shell
                    mb.Sub(0).Push(o + Vector3.down * 0.004f, Quaternion.identity, new Vector3(1f, 1f, r.z / r.x));
                    mb.Sub(2).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(r.x * 0.985f, 0.012f), new Vector2(r.x * 1.03f, -0.006f), new Vector2(r.x * 1.065f, -0.015f), new Vector2(r.x * 1.06f, -0.02f), new Vector2(r.x * 0.97f, -0.012f) }, 28, shade);   // brim (cover tucked under)
                    mb.Pop();
                    mb.Sub(2).Sphere(o + Vector3.down * 0.004f, r * 0.9f, 16, Webbing, 0f, 0.5f);
                    ChinStrap(mb, s);
                    // camo: flat patches lying on the cover (printed, not lumps), three colours
                    var rnd = new System.Random(7);
                    Color[] camo = { Mul(c, 0.6f), Lin(new Color(0.42f, 0.36f, 0.25f)), Lin(new Color(0.16f, 0.17f, 0.12f)) };
                    for (int i = 0; i < 34; i++)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f, el = 0.12f + (float)rnd.NextDouble() * 1.35f;
                        Vector3 n = new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el));
                        if (n.z > 0.55f && n.y < 0.62f && n.y > 0.2f) continue;   // keep the brow under the goggles clear
                        Vector3 p0 = o + Vector3.Scale(n, r) * 1.002f;
                        float w = 0.016f + (float)rnd.NextDouble() * 0.016f;
                        mb.Sub(2).Push(p0, Quaternion.LookRotation(Vector3.Scale(n, new Vector3(1f / r.x, 1f / r.y, 1f / r.z)).normalized) * Quaternion.Euler(0, 0, (float)rnd.NextDouble() * 180f));
                        mb.Sub(2).Sphere(Vector3.zero, new Vector3(w, w * (0.45f + (float)rnd.NextDouble() * 0.4f), 0.0012f), 10, camo[i % 3]);
                        mb.Pop();
                    }
                    // seams of the cover and an elastic band round it with two reflective cat-eye tabs at the back
                    mb.Sub(2).Push(o + Vector3.up * 0.035f, Quaternion.identity, new Vector3(1f, 1f, r.z / r.x));
                    mb.Sub(2).Tube(new Vector3(0, -0.009f, 0), new Vector3(0, 0.009f, 0), r.x * 0.98f + 0.004f, r.x * 0.965f, 32, Black);
                    mb.Pop();
                    foreach (float x in new[] { -0.035f, 0.035f }) mb.Sub(3).Box(new Vector3(x, 0.14f, -0.132f), new Vector3(0.022f, 0.008f, 0.003f), new Color(0.95f, 0.85f, 0.4f), 0.001f);
                    // goggles on the brow: a strap round the band, one wide rubber frame following the helmet's curve, two-part amber lens, a vent strip
                    System.Func<float, float> shellR = y => r.x * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((y - o.y) / r.y, 2f)));   // the cover's radius at height y
                    Band(mb, 2, -160f, 160f, 0.146f, 0.158f, y => shellR(y) + 0.0035f, 0.0f, 36, 1, Rubber);   // strap hugging the cover
                    Band(mb, 1, -40f, 40f, 0.133f, 0.173f, y => shellR(y) + 0.009f, 0.0f, 18, 4, Rubber);       // goggle frame, curved round the brow
                    foreach (float sx in new[] { -1f, 1f })
                        Band(mb, 1, sx < 0 ? -36f : 4f, sx < 0 ? -4f : 36f, 0.138f, 0.168f, y => shellR(y) + 0.0125f, 0.0f, 10, 3, Lin(new Color(0.8f, 0.5f, 0.12f)));   // amber lenses
                    mb.Sub(1).Box(new Vector3(0, 0.15f, 0.133f), new Vector3(0.01f, 0.028f, 0.01f), Rubber, 0.003f);   // nose bridge
                    mb.Sub(1).Box(new Vector3(0, 0.172f, 0.128f), new Vector3(0.06f, 0.003f, 0.008f), Black, 0.001f, 1, Quaternion.Euler(-30f, 0, 0));   // vent strip
                    Ears(mb, s, false);
                    break;
                }
                case 2: // T3 RAPTOR high-cut: ARC rails, NVG shroud, comms headset, velcro, counterweight battery, a helmet light and an IR strobe
                    TierShell(mb, o, r, 0.47f, c, true);
                    ChinStrap(mb, s);
                    Ears(mb, s, true);
                    Rails(mb, o, s, 0.03f, Dark);
                    Shroud(mb, s, Dark);
                    mb.Sub(2).Box(new Vector3(0, 0.218f, -0.03f), new Vector3(0.075f, 0.006f, 0.06f), t, 0.002f, 1, Quaternion.Euler(-12f, 0, 0));
                    mb.Sub(2).Box(new Vector3(0, 0.15f, -0.125f), new Vector3(0.07f, 0.045f, 0.006f), t, 0.002f, 1, Quaternion.Euler(28f, 0, 0));
                    mb.Sub(2).Box(new Vector3(0, 0.09f, -0.13f), new Vector3(0.1f, 0.05f, 0.03f), Mul(t, 0.8f), 0.01f, 2);   // counterweight
                    mb.Sub(1).Box(new Vector3(0, 0.09f, -0.147f), new Vector3(0.05f, 0.012f, 0.002f), Black, 0f);
                    foreach (float x in new[] { -1f, 1f }) mb.Sub(2).Capsule(new Vector3(x * 0.1f, 0.13f, 0.07f), new Vector3(x * 0.1f, 0.15f, -0.08f), 0.0025f, 5, Black);
                    mb.Sub(1).Cyl(new Vector3(0.132f, 0.09f, 0.03f), new Vector3(0.132f, 0.09f, 0.085f), 0.011f, 0.012f, 14, Black);   // light
                    mb.Sub(3).Cyl(new Vector3(0.132f, 0.09f, 0.085f), new Vector3(0.132f, 0.09f, 0.087f), 0.009f, 0.009f, 14, Color.white);
                    mb.Sub(1).Box(new Vector3(0, 0.226f, -0.07f), new Vector3(0.02f, 0.012f, 0.028f), Black, 0.003f);   // strobe
                    mb.Sub(3).Box(new Vector3(0, 0.234f, -0.07f), new Vector3(0.012f, 0.005f, 0.018f), new Color(0.3f, 1f, 0.5f), 0.002f);
                    break;
                case 3: // T4 RONIN: high-cut shell, armoured ear plates, a bolted mandible guarding the jaw, rails and comms
                    TierShell(mb, o, r, 0.47f, c, true);
                    ChinStrap(mb, s);
                    Ears(mb, s, true);
                    Rails(mb, o, s, 0.03f, Dark);
                    Shroud(mb, s, Dark);
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Box(new Vector3(x * 0.132f, 0.07f, 0.0f), new Vector3(0.02f, 0.085f, 0.1f), shade, 0.012f, 2);   // ear plates
                        mb.Sub(1).Cyl(new Vector3(x * 0.142f, 0.05f, 0.04f), new Vector3(x * 0.146f, 0.05f, 0.04f), 0.008f, 0.008f, 10, t);   // mandible hinge bolts
                    }
                    Band(mb, 0, -72f, 72f, -0.02f, 0.045f, y => 0.12f - (0.045f - y) * 0.25f, 0.0f, 20, 4, c);   // the mandible
                    for (int i = -2; i <= 2; i++) mb.Sub(1).Box(new Vector3(i * 0.016f, 0.012f, 0.128f - Mathf.Abs(i) * 0.004f), new Vector3(0.006f, 0.026f, 0.004f), Black, 0.001f);   // breathing slots
                    mb.Sub(1).Box(new Vector3(0, 0.044f, 0.127f), new Vector3(0.18f, 0.006f, 0.006f), t, 0.002f);   // trim along its top edge
                    break;
                case 4: // T5 VANGUARD: a full-cut shell, a wide framed ballistic visor, brow guard, nape protector, strobe
                    TierShell(mb, o, r, 0.55f, c, true);
                    ChinStrap(mb, s);
                    Ears(mb, s, true);
                    Rails(mb, o, s, 0.03f, Dark);
                    Band(mb, 3, -68f, 68f, 0.012f, 0.15f, y => Mathf.Sqrt(Mathf.Max(0f, 0.152f * 0.152f - (y - 0.085f) * (y - 0.085f))) - 0.002f, -0.012f, 22, 8, Lin(new Color(0.12f, 0.16f, 0.2f)));   // the visor, domed like the shell
                    Band(mb, 1, -72f, 72f, 0.146f, 0.162f, y => 0.136f, -0.012f, 22, 1, t);   // its frame / brow guard
                    Band(mb, 1, -68f, 68f, 0.006f, 0.013f, y => 0.133f, -0.012f, 22, 1, Rubber);   // bottom edge
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(1).Cyl(new Vector3(x * 0.125f, 0.13f, 0.06f), new Vector3(x * 0.14f, 0.13f, 0.06f), 0.015f, 0.014f, 14, t);
                        mb.Sub(1).Cyl(new Vector3(x * 0.139f, 0.13f, 0.06f), new Vector3(x * 0.143f, 0.13f, 0.06f), 0.006f, 0.006f, 8, Steel);
                    }
                    Band(mb, 0, 120f, 240f, 0.005f, 0.05f, y => 0.125f + (0.05f - y) * 0.3f, 0.0f, 16, 3, shade);   // nape protector
                    mb.Sub(1).Box(new Vector3(0, 0.236f, -0.06f), new Vector3(0.022f, 0.012f, 0.03f), Black, 0.003f);
                    mb.Sub(3).Box(new Vector3(0, 0.244f, -0.06f), new Vector3(0.014f, 0.005f, 0.02f), new Color(0.3f, 0.9f, 1f), 0.002f);
                    break;
                default: // T6 TITAN: a huge full-face helm - face plate, glowing eye slit, breathing grille, crest, gold trim, rivets, a segmented neck guard
                {
                    TierShell(mb, o, r, 0.56f, c, false);
                    Ears(mb, s, false);
                    Band(mb, 0, -82f, 82f, -0.04f, 0.16f, y => Mathf.Sqrt(Mathf.Max(0f, 0.17f * 0.17f - (y - 0.065f) * (y - 0.065f))) - 0.012f, -0.008f, 24, 10, c);   // face plate, rounded
                    Band(mb, 0, -55f, 55f, 0.075f, 0.094f, y => 0.162f, -0.008f, 18, 2, Lin(new Color(0.45f, 0.95f, 1f)));   // eye slit glow
                    Band(mb, 1, -60f, 60f, 0.07f, 0.075f, y => 0.163f, -0.008f, 18, 1, t);
                    Band(mb, 1, -60f, 60f, 0.094f, 0.099f, y => 0.163f, -0.008f, 18, 1, t);
                    for (int row = 0; row < 3; row++) for (int i = -3; i <= 3; i++) mb.Sub(1).Box(new Vector3(i * 0.014f, -0.005f + row * 0.016f, 0.15f - Mathf.Abs(i) * 0.0035f), new Vector3(0.008f, 0.008f, 0.006f), Black, 0.002f);   // grille
                    for (int i = 0; i < 12; i++)   // crest
                    {
                        float a0 = Mathf.Lerp(0.3f, 2.75f, i / 12f), a1 = Mathf.Lerp(0.3f, 2.75f, (i + 1) / 12f);
                        mb.Sub(1).Capsule(o + new Vector3(0, Mathf.Sin(a0) * r.y, Mathf.Cos(a0) * r.z) * 1.02f, o + new Vector3(0, Mathf.Sin(a1) * r.y, Mathf.Cos(a1) * r.z) * 1.02f, 0.011f, 8, t);
                    }
                    for (int i = 0; i < 9; i++) { float th = Mathf.Lerp(-70f, 70f, i / 8f) * Mathf.Deg2Rad; mb.Sub(1).Sphere(new Vector3(Mathf.Sin(th) * 0.155f, 0.132f, Mathf.Cos(th) * 0.157f), Vector3.one * 0.0055f, 6, t); }   // rivets
                    for (int k = 0; k < 3; k++) Band(mb, 0, 100f, 260f, -0.01f - k * 0.022f, 0.012f - k * 0.022f, y => 0.13f + k * 0.008f, 0.0f, 16, 2, k % 2 == 0 ? shade : c);   // neck guard
                    break;
                }
            }
            return mb;
        }

        /// <summary>Tier vests: 0 T1 soft shell, 1 T2 light carrier, 2 T3 ranger (chest rig), 3 T4 heavy, 4 T5 assault, 5 T6 titan.</summary>
        static MB VestTier(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = Lin(d.color), c2 = Mul(c, 0.8f), t = Lin(d.trim), seam = Mul(c, 0.6f);
            int st = Mathf.Clamp(d.style, 0, 5);
            float thick = st == 0 ? 0.035f : st == 1 ? 0.05f : st <= 3 ? 0.062f : 0.072f;
            float fz = 0.12f + thick * 0.5f;
            // carrier panels, front and back; plates show as a raised shape (T2+)
            mb.Sub(2).Box(new Vector3(0, 0.04f, 0.12f), new Vector3(0.3f, 0.32f, thick), c, 0.02f, 2);
            mb.Sub(2).Box(new Vector3(0, 0.04f, -0.12f), new Vector3(0.3f, 0.32f, thick * 0.9f), c, 0.02f, 2);
            foreach (float x in new[] { -1f, 1f })
            {
                // cummerbund + shoulder straps
                mb.Sub(2).Box(new Vector3(x * 0.165f, 0.0f, 0), new Vector3(0.04f, st == 0 ? 0.1f : 0.15f, 0.2f), c2, 0.012f, 2);
                mb.Sub(2).Box(new Vector3(x * 0.155f, 0.0f, 0.1f), new Vector3(0.03f, st == 0 ? 0.1f : 0.15f, 0.04f), c2, 0.01f, 1, Quaternion.Euler(0, x * 40f, 0));
                mb.Sub(2).Box(new Vector3(x * 0.155f, 0.0f, -0.1f), new Vector3(0.03f, st == 0 ? 0.1f : 0.15f, 0.04f), c2, 0.01f, 1, Quaternion.Euler(0, x * -40f, 0));
                mb.Sub(2).Box(new Vector3(x * 0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), c2, 0.012f, 2);
                mb.Sub(1).Box(new Vector3(x * 0.1f, 0.16f, fz - 0.006f), new Vector3(0.036f, 0.022f, 0.01f), Black, 0.003f);
            }
            if (st == 0)
            {
                // T1 soft shell: quilting, elastic side straps, a zipped chest pocket, a name tape
                for (int i = 0; i < 6; i++) mb.Sub(2).Box(new Vector3(0, -0.09f + i * 0.048f, fz + 0.0005f), new Vector3(0.27f, 0.002f, 0.002f), seam, 0f);
                for (int i = 0; i < 4; i++) mb.Sub(2).Box(new Vector3(-0.1f + i * 0.067f, 0.04f, fz + 0.0005f), new Vector3(0.002f, 0.28f, 0.002f), seam, 0f);
                foreach (float x in new[] { -1f, 1f }) for (int k = 0; k < 2; k++) mb.Sub(2).Box(new Vector3(x * 0.186f, -0.03f + k * 0.05f, 0), new Vector3(0.004f, 0.014f, 0.18f), Black, 0.001f);
                mb.Sub(2).Box(new Vector3(-0.07f, 0.1f, fz + 0.008f), new Vector3(0.08f, 0.07f, 0.014f), c2, 0.008f, 2);
                mb.Sub(1).Box(new Vector3(-0.07f, 0.133f, fz + 0.016f), new Vector3(0.07f, 0.003f, 0.002f), Black, 0f);
                mb.Sub(2).Box(new Vector3(0.07f, 0.12f, fz + 0.002f), new Vector3(0.08f, 0.02f, 0.002f), t, 0.001f);
                return mb;
            }
            // raised plate shapes front and back (T2+), with a trim-coloured edge on T5/T6
            mb.Sub(0).Box(new Vector3(0, 0.05f, fz + 0.003f), new Vector3(0.25f, 0.28f, 0.008f), c2, 0.012f, 2);
            mb.Sub(0).Box(new Vector3(0, 0.05f, -fz + 0.005f), new Vector3(0.25f, 0.28f, 0.008f), c2, 0.012f, 2);
            // laser-cut MOLLE slots across the front
            for (int row = 0; row < 3; row++) for (int k = 0; k < 6; k++) mb.Sub(1).Box(new Vector3(-0.088f + k * 0.035f, 0.0f + row * 0.03f, fz + 0.0075f), new Vector3(0.022f, 0.005f, 0.002f), Black, 0f);
            mb.Sub(2).Capsule(new Vector3(-0.04f, 0.2f, -fz - 0.004f), new Vector3(0.04f, 0.2f, -fz - 0.004f), 0.01f, 8, Webbing);   // drag handle
            if (st == 1)
            {
                // T2 light carrier: three low-profile elastic mag pouches, mag tops peeking out, a small placard patch
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.07f + i * 0.07f;
                    mb.Sub(2).Box(new Vector3(x, -0.07f, fz + 0.014f), new Vector3(0.055f, 0.1f, 0.026f), Mul(c2, 0.9f), 0.008f, 2);
                    for (int k = 0; k < 2; k++) mb.Sub(2).Box(new Vector3(x, -0.08f + k * 0.04f, fz + 0.0275f), new Vector3(0.056f, 0.008f, 0.002f), Black, 0f);
                    mb.Sub(1).Box(new Vector3(x, -0.012f, fz + 0.012f), new Vector3(0.03f, 0.02f, 0.018f), Black, 0.002f);
                }
                mb.Sub(1).Box(new Vector3(0.08f, 0.14f, fz + 0.009f), new Vector3(0.05f, 0.032f, 0.004f), t, 0.002f);
                return mb;
            }
            // T3+: a chest-rig placard: 4 rifle mag pouches with flaps, 2 pistol pouches, an admin pouch with a map light
            for (int i = 0; i < 4; i++)
            {
                float x = -0.0975f + i * 0.065f;
                mb.Sub(2).Box(new Vector3(x, -0.065f, fz + 0.02f), new Vector3(0.056f, 0.12f, 0.038f), c2, 0.012f, 2);
                mb.Sub(2).Box(new Vector3(x, -0.008f, fz + 0.022f), new Vector3(0.06f, 0.02f, 0.042f), Mul(c2, 0.92f), 0.008f, 1, Quaternion.Euler(-8f, 0, 0));
                mb.Sub(1).Box(new Vector3(x, -0.024f, fz + 0.044f), new Vector3(0.014f, 0.022f, 0.004f), Black, 0.002f);
            }
            foreach (float x in new[] { -0.035f, 0.035f }) { mb.Sub(2).Box(new Vector3(x, 0.05f, fz + 0.016f), new Vector3(0.03f, 0.06f, 0.026f), c2, 0.008f, 2); mb.Sub(1).Box(new Vector3(x, 0.083f, fz + 0.014f), new Vector3(0.022f, 0.012f, 0.016f), Black, 0.002f); }
            mb.Sub(2).Box(new Vector3(0, 0.13f, fz + 0.012f), new Vector3(0.16f, 0.05f, 0.024f), c2, 0.01f, 2);
            mb.Sub(1).Box(new Vector3(0, 0.148f, fz + 0.025f), new Vector3(0.15f, 0.004f, 0.003f), Black, 0f);
            mb.Sub(2).Box(new Vector3(0, 0.125f, fz + 0.0245f), new Vector3(0.1f, 0.02f, 0.002f), t, 0.001f);
            mb.Sub(1).Cyl(new Vector3(0.1f, 0.15f, fz + 0.006f), new Vector3(0.1f, 0.15f, fz + 0.026f), 0.009f, 0.01f, 10, Black);   // map light
            mb.Sub(3).Cyl(new Vector3(0.1f, 0.15f, fz + 0.026f), new Vector3(0.1f, 0.15f, fz + 0.028f), 0.007f, 0.007f, 10, new Color(1f, 0.3f, 0.2f));
            // radio pouch + antenna on the left, a tourniquet on the right, a hydration hose over the shoulder
            mb.Sub(2).Box(new Vector3(-0.2f, -0.02f, -0.02f), new Vector3(0.035f, 0.11f, 0.06f), c2, 0.01f, 2);
            mb.Sub(1).Box(new Vector3(-0.2f, 0.05f, -0.03f), new Vector3(0.02f, 0.03f, 0.03f), Black, 0.004f);
            mb.Sub(1).Capsule(new Vector3(-0.2f, 0.06f, -0.03f), new Vector3(-0.21f, 0.2f, -0.06f), 0.003f, 6, Black);
            mb.Sub(2).Box(new Vector3(0.2f, -0.02f, 0.02f), new Vector3(0.03f, 0.1f, 0.04f), c2, 0.01f, 2);
            mb.Sub(1).Box(new Vector3(0.2f, 0.035f, 0.02f), new Vector3(0.02f, 0.02f, 0.03f), new Color(0.75f, 0.1f, 0.08f), 0.004f);
            mb.Sub(1).Capsule(new Vector3(0.1f, 0.21f, -0.05f), new Vector3(0.11f, 0.2f, 0.08f), 0.005f, 6, Black);
            mb.Sub(1).Capsule(new Vector3(0.11f, 0.2f, 0.08f), new Vector3(0.1f, 0.15f, fz + 0.004f), 0.005f, 6, Black);
            if (st == 2) return mb;
            // T4+: side plates, shoulder armour, a collar, grenade pouches, a dump pouch
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Sub(0).Box(new Vector3(x * 0.19f, 0.0f, 0), new Vector3(0.016f, 0.13f, 0.14f), c2, 0.008f, 2);
                mb.Sub(0).Box(new Vector3(x * 0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, x * 25f));
                for (int i = 0; i < 3; i++) mb.Sub(2).Box(new Vector3(x * (0.18f + i * 0.015f), 0.192f - i * 0.007f, 0), new Vector3(0.003f, 0.003f, 0.15f), seam, 0f, 1, Quaternion.Euler(0, 0, x * 25f));
                mb.Sub(2).Sphere(new Vector3(x * 0.135f, -0.06f, fz + 0.012f), new Vector3(0.022f, 0.03f, 0.022f), 10, c2);   // grenade pouches
            }
            mb.Sub(0).Box(new Vector3(0, 0.22f, -0.04f), new Vector3(0.22f, 0.05f, 0.14f), c2, 0.02f, 2);   // collar
            mb.Sub(2).Box(new Vector3(-0.12f, -0.14f, 0.1f), new Vector3(0.08f, 0.05f, 0.05f), c2, 0.015f, 2);   // dump pouch
            if (st == 3) return mb;
            // T5+: groin flap, layered deltoid armour, a throat guard, trim-edged plates, a hydration bladder on the back
            mb.Sub(2).Box(new Vector3(0, -0.2f, 0.125f), new Vector3(0.16f, 0.14f, 0.03f), c, 0.012f, 2);
            mb.Sub(1).Box(new Vector3(0, -0.13f, 0.142f), new Vector3(0.16f, 0.006f, 0.004f), t, 0.001f);
            foreach (float x in new[] { -1f, 1f })
                for (int k = 0; k < 2; k++) mb.Sub(0).Box(new Vector3(x * (0.235f + k * 0.012f), 0.13f - k * 0.04f, 0), new Vector3(0.08f, 0.04f, 0.15f - k * 0.02f), k == 0 ? c : c2, 0.014f, 2, Quaternion.Euler(0, 0, x * (40f + k * 15f)));
            mb.Sub(0).Box(new Vector3(0, 0.215f, 0.11f), new Vector3(0.12f, 0.04f, 0.03f), c, 0.012f, 2, Quaternion.Euler(20f, 0, 0));   // throat guard
            mb.Sub(1).Box(new Vector3(0, 0.195f, fz + 0.008f), new Vector3(0.25f, 0.006f, 0.004f), t, 0.001f);
            mb.Sub(2).Box(new Vector3(0, 0.03f, -fz - 0.02f), new Vector3(0.18f, 0.26f, 0.035f), c2, 0.02f, 2);   // hydration bladder
            if (st == 4) return mb;
            // T6 TITAN: a massive layered shell over everything - chest plate, segmented belly, big pauldrons, a gorget, gold trim and rivets
            mb.Sub(0).Box(new Vector3(0, 0.1f, 0.175f), new Vector3(0.33f, 0.22f, 0.035f), c, 0.03f, 3);
            for (int k = 0; k < 3; k++) mb.Sub(0).Box(new Vector3(0, -0.04f - k * 0.045f, 0.17f - k * 0.006f), new Vector3(0.28f - k * 0.02f, 0.042f, 0.03f), k % 2 == 0 ? c2 : c, 0.014f, 2);
            foreach (float x in new[] { -1f, 1f })
            {
                mb.Sub(0).Sphere(new Vector3(x * 0.205f, 0.16f, 0), new Vector3(0.1f, 0.075f, 0.12f), 18, c, 0f, 0.56f);
                mb.Sub(0).Sphere(new Vector3(x * 0.222f, 0.118f, 0), new Vector3(0.092f, 0.055f, 0.11f), 16, c2, 0f, 0.56f);
                mb.Sub(1).Push(new Vector3(x * 0.222f, 0.115f, 0), Quaternion.Euler(0, 0, x * -10f));
                mb.Sub(1).Tube(new Vector3(0, -0.003f, 0), new Vector3(0, 0.003f, 0), 0.094f, 0.086f, 22, t);
                mb.Pop();
            }
            mb.Sub(0).Tube(new Vector3(0, 0.2f, -0.01f), new Vector3(0, 0.245f, -0.01f), 0.13f, 0.085f, 24, c2);   // gorget
            mb.Sub(1).Tube(new Vector3(0, 0.243f, -0.01f), new Vector3(0, 0.25f, -0.01f), 0.132f, 0.11f, 24, t);
            foreach (float y in new[] { 0.205f, -0.005f }) mb.Sub(1).Box(new Vector3(0, y, 0.194f), new Vector3(0.31f, 0.012f, 0.008f), t, 0.003f);
            for (int i = 0; i < 8; i++) { float x = -0.14f + i * 0.04f; mb.Sub(1).Sphere(new Vector3(x, 0.19f, 0.195f), Vector3.one * 0.005f, 6, t); mb.Sub(1).Sphere(new Vector3(x, 0.008f, 0.195f), Vector3.one * 0.005f, 6, t); }
            mb.Sub(0).Box(new Vector3(0, 0.05f, -fz - 0.045f), new Vector3(0.3f, 0.3f, 0.025f), c, 0.025f, 3);   // back shell
            mb.Sub(1).Box(new Vector3(0, 0.05f, -fz - 0.06f), new Vector3(0.02f, 0.26f, 0.012f), t, 0.004f);   // spine ridge
            return mb;
        }

        // ================================================================================================ GRENADES
        /// <summary>0 frag, 1 flashbang, 2 impact. Fuse up (+y), the spoon down the +x side, the pin ring at (-0.016, 0.045, 0).</summary>
        public static MB Grenade(int shape)
        {
            var mb = new MB(2);
            Color w = Color.white;
            if (shape == 1)
            {
                // M84: a perforated steel tube with end caps and a printed band
                Color body = new Color(0.55f, 0.57f, 0.58f);
                mb.Sub(0).Cyl(new Vector3(0, -0.055f, 0), new Vector3(0, 0.045f, 0), 0.024f, 0.024f, 22, body);
                mb.Sub(0).Cyl(new Vector3(0, -0.058f, 0), new Vector3(0, -0.048f, 0), 0.0255f, 0.0255f, 22, Mul(body, 0.8f));   // bottom cap
                mb.Sub(0).Cyl(new Vector3(0, 0.038f, 0), new Vector3(0, 0.046f, 0), 0.0255f, 0.0245f, 22, Mul(body, 0.8f));     // top cap
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i + row * 0.5f) * 45f * Mathf.Deg2Rad, y = -0.035f + row * 0.018f;
                        mb.Sub(0).Sphere(new Vector3(Mathf.Cos(a) * 0.0236f, y, Mathf.Sin(a) * 0.0236f), new Vector3(0.0045f, 0.0045f, 0.0045f), 8, new Color(0.08f, 0.08f, 0.08f));
                    }
                mb.Sub(0).Cyl(new Vector3(0, 0.018f, 0), new Vector3(0, 0.03f, 0), 0.0243f, 0.0243f, 22, new Color(0.15f, 0.15f, 0.15f));   // label band
                mb.Sub(0).Cyl(new Vector3(0, 0.023f, 0), new Vector3(0, 0.025f, 0), 0.0246f, 0.0246f, 22, new Color(0.85f, 0.75f, 0.2f));
            }
            else if (shape == 2)
            {
                // RGN: a ribbed sphere with a black band, a yellow stripe, a domed impact fuse
                Color body = new Color(0.22f, 0.27f, 0.2f);
                mb.Sub(0).Sphere(new Vector3(0, -0.012f, 0), new Vector3(0.034f, 0.034f, 0.034f), 20, body);
                for (int i = 0; i < 4; i++) { float y = -0.036f + i * 0.012f; float rr = Mathf.Sqrt(Mathf.Max(0f, 0.034f * 0.034f - (y + 0.012f) * (y + 0.012f))); mb.Sub(0).Tube(new Vector3(0, y - 0.001f, 0), new Vector3(0, y + 0.001f, 0), rr + 0.0012f, rr - 0.002f, 20, Mul(body, 0.8f)); }
                mb.Sub(0).Cyl(new Vector3(0, -0.018f, 0), new Vector3(0, -0.006f, 0), 0.0352f, 0.0352f, 20, new Color(0.06f, 0.06f, 0.06f));
                mb.Sub(0).Cyl(new Vector3(0, 0.004f, 0), new Vector3(0, 0.009f, 0), 0.0338f, 0.033f, 20, new Color(0.85f, 0.7f, 0.1f));
                mb.Sub(0).Cyl(new Vector3(0, -0.047f, 0), new Vector3(0, -0.044f, 0), 0.012f, 0.014f, 14, Mul(body, 0.7f));   // base plug
            }
            else
            {
                // M67: an egg-shaped steel body with a seam, a yellow band, a base plug
                Color body = new Color(0.3f, 0.34f, 0.22f);
                mb.Sub(0).Lathe(new Vector3(0, -0.015f, 0), Quaternion.identity, new List<Vector2> { new Vector2(0.0f, -0.039f), new Vector2(0.014f, -0.037f), new Vector2(0.025f, -0.029f), new Vector2(0.031f, -0.016f), new Vector2(0.0325f, 0f), new Vector2(0.031f, 0.017f), new Vector2(0.025f, 0.029f), new Vector2(0.016f, 0.036f), new Vector2(0.011f, 0.039f) }, 22, body);
                mb.Sub(0).Tube(new Vector3(0, -0.0165f, 0), new Vector3(0, -0.0135f, 0), 0.0332f, 0.03f, 22, Mul(body, 0.75f));   // seam
                mb.Sub(0).Tube(new Vector3(0, 0.006f, 0), new Vector3(0, 0.011f, 0), 0.0316f, 0.028f, 22, new Color(0.85f, 0.72f, 0.15f));   // yellow band
                mb.Sub(0).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, -0.055f, 0), 0.014f, 0.012f, 14, new Color(0.25f, 0.28f, 0.18f));
            }
            // fuse: a stepped body, the striker housing, the spoon curving down the side, the safety clip
            mb.Sub(1).Cyl(new Vector3(0, 0.018f, 0), new Vector3(0, 0.032f, 0), 0.0125f, 0.0125f, 16, w * 0.8f);
            mb.Sub(1).Cyl(new Vector3(0, 0.032f, 0), new Vector3(0, 0.05f, 0), 0.0105f, 0.0095f, 16, w);
            mb.Sub(1).Cyl(new Vector3(0, 0.05f, 0), new Vector3(0, 0.053f, 0), 0.0085f, 0.006f, 12, w * 0.9f);
            for (int i = 0; i < 6; i++) mb.Sub(1).Box(new Vector3(0, 0.025f, 0) + Quaternion.Euler(0, i * 60f, 0) * new Vector3(0.0126f, 0, 0), new Vector3(0.002f, 0.012f, 0.004f), w * 0.7f, 0f, 1, Quaternion.Euler(0, i * 60f, 0));   // knurls
            float[] sy = { 0.048f, 0.036f, 0.02f, 0.004f, -0.012f }, sx = { 0.012f, 0.017f, 0.021f, 0.024f, 0.025f };
            for (int i = 0; i < sy.Length - 1; i++)
            {
                Vector3 a = new Vector3(sx[i], sy[i], 0), b = new Vector3(sx[i + 1], sy[i + 1], 0);
                if (shape == 1) { a.x = Mathf.Max(a.x, 0.027f); b.x = Mathf.Max(b.x, 0.027f); }
                if (shape == 2) { a.x += 0.012f * i / 3f; b.x += 0.012f * (i + 1) / 3f; }
                Vector3 m = (a + b) * 0.5f, dir = b - a;
                mb.Sub(1).Box(m, new Vector3(0.003f, dir.magnitude + 0.002f, 0.014f), w * 0.85f, 0.001f, 1, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            }
            mb.Sub(1).Box(new Vector3(0.006f, 0.051f, 0), new Vector3(0.014f, 0.003f, 0.012f), w * 0.85f, 0.001f);   // spoon lip over the top
            mb.Sub(1).Box(new Vector3(-0.012f, 0.042f, 0), new Vector3(0.004f, 0.006f, 0.008f), w * 0.6f, 0.001f);   // pin housing
            return mb;
        }

        /// <summary>The pull ring: a proper round wire ring with the split showing.</summary>
        public static MB PinRing()
        {
            var mb = new MB();
            for (int i = 0; i < 22; i++)
            {
                float a0 = i * 16f * Mathf.Deg2Rad, a1 = (i + 1) * 16f * Mathf.Deg2Rad;
                Vector3 p0 = new Vector3(-0.01f + Mathf.Cos(a0) * 0.011f, Mathf.Sin(a0) * 0.011f, 0), p1 = new Vector3(-0.01f + Mathf.Cos(a1) * 0.011f, Mathf.Sin(a1) * 0.011f, 0);
                mb.Capsule(p0, p1, 0.0011f, 5, Color.white);
            }
            mb.Capsule(new Vector3(0.001f, 0, 0), new Vector3(0.006f, 0, 0), 0.0009f, 5, Color.white);   // the pin itself
            return mb;
        }

        // ================================================================================================ KNIVES
        /// <summary>0 combat knife, 1 hunting knife. Handle along -z (to -0.11), guard at 0, blade along +z.</summary>
        public static MB Knife(int shape, out float blade)
        {
            bool hunt = shape == 1;
            blade = hunt ? 0.17f : 0.14f;
            var mb = new MB(2);
            Color w = Color.white, grip = hunt ? new Color(0.42f, 0.28f, 0.16f) : new Color(0.1f, 0.1f, 0.1f);
            // handle: a lathed, slightly flattened grip with finger swells (or a wooden handle with brass pins)
            mb.Sub(0).Push(new Vector3(0, 0, 0.003f), Quaternion.Euler(90f, 0, 0), new Vector3(0.8f, 1f, 1f));   // lathe up = -z: the handle behind the guard (it was -90: built over the blade)
            var prof = hunt
                ? new List<Vector2> { new Vector2(0.0f, -0.104f), new Vector2(0.015f, -0.104f), new Vector2(0.017f, -0.09f), new Vector2(0.0155f, -0.06f), new Vector2(0.0145f, -0.03f), new Vector2(0.0165f, -0.008f), new Vector2(0.0f, -0.006f) }
                : new List<Vector2> { new Vector2(0.0f, -0.104f), new Vector2(0.016f, -0.104f), new Vector2(0.0165f, -0.09f), new Vector2(0.0145f, -0.075f), new Vector2(0.0165f, -0.06f), new Vector2(0.0145f, -0.045f), new Vector2(0.0165f, -0.03f), new Vector2(0.0145f, -0.015f), new Vector2(0.016f, -0.006f), new Vector2(0.0f, -0.004f) };
            mb.Sub(0).Lathe(Vector3.zero, Quaternion.identity, prof, 16, grip);
            mb.Pop();
            if (hunt) foreach (float z in new[] { -0.08f, -0.035f }) mb.Sub(1).Cyl(new Vector3(-0.0135f, 0, z), new Vector3(0.0135f, 0, z), 0.0035f, 0.0035f, 8, new Color(0.85f, 0.65f, 0.3f));
            else for (int i = 0; i < 7; i++) mb.Sub(0).Tube(new Vector3(0, 0, -0.098f + i * 0.013f), new Vector3(0, 0, -0.096f + i * 0.013f), 0.0172f, 0.014f, 14, Mul(grip, 0.6f));   // grip rings
            // guard, pommel with a lanyard hole
            mb.Sub(1).Box(new Vector3(0, -0.004f, 0.002f), new Vector3(0.028f, 0.056f, 0.008f), w * 0.7f, 0.003f, 2);
            mb.Sub(1).Cyl(new Vector3(0, 0, -0.114f), new Vector3(0, 0, -0.1f), 0.015f, 0.017f, 14, w * 0.6f);   // pommel, right against the handle
            mb.Sub(0).Cyl(new Vector3(-0.016f, 0, -0.109f), new Vector3(0.016f, 0, -0.109f), 0.004f, 0.004f, 8, Black);
            // blade: a real side profile - a spine with a clip point, a belly, the fuller groove and a sharpened edge
            float L = blade, z0 = 0.006f, h = hunt ? 0.032f : 0.028f;
            var bl = new List<Vector2>
            {
                new Vector2(z0, -h * 0.55f), new Vector2(z0 + L * 0.65f, -h * 0.55f), new Vector2(z0 + L * 0.9f, -h * 0.3f), new Vector2(z0 + L + 0.008f, h * 0.18f),
                new Vector2(z0 + L * 0.78f, h * 0.42f), new Vector2(z0 + L * 0.7f, h * 0.45f), new Vector2(z0, h * 0.45f)
            };
            mb.Sub(1).Extrude(bl, 0.004f, w, 0.0012f, new Vector3(0, 0.002f, 0));
            mb.Sub(1).Box(new Vector3(0, 0.002f - h * 0.46f, z0 + L * 0.4f), new Vector3(0.0045f, 0.004f, L * 0.78f), w * 1.25f, 0.0005f);   // bright edge bevel
            mb.Sub(1).Box(new Vector3(0, 0.002f + h * 0.12f, z0 + L * 0.35f), new Vector3(0.0046f, 0.0045f, L * 0.5f), w * 0.55f, 0.001f);   // fuller
            if (!hunt) for (int i = 0; i < 6; i++) mb.Sub(1).Box(new Vector3(0, 0.002f + h * 0.47f, z0 + 0.01f + i * 0.008f), new Vector3(0.0042f, 0.005f, 0.004f), w * 0.5f, 0f, 1, Quaternion.Euler(45f, 0, 0));   // serrations on the spine
            return mb;
        }

        // ================================================================================================ LONG MELEE
        /// <summary>2 bat, 3 crowbar, 4 machete, 5 sledgehammer, 6 fire axe, 7 frying pan, 8 candy cane. Held at z = 0.</summary>
        public static MB Long(int shape)
        {
            var mb = new MB(3);   // 0 wood / grip / paint, 1 steel, 2 tape
            Color w = Color.white, tape = new Color(0.08f, 0.08f, 0.08f), wood = new Color(0.62f, 0.45f, 0.28f), ash = new Color(0.55f, 0.4f, 0.25f);
            var Z = Quaternion.Euler(90f, 0, 0);   // lathe profiles run along +z
            switch (shape)
            {
                case 2:   // baseball bat: one smooth turned profile, a knob, taped handle, a maker's stamp, scuffs on the barrel
                    mb.Sub(0).Lathe(Vector3.zero, Z, new List<Vector2> { new Vector2(0f, -0.103f), new Vector2(0.024f, -0.103f), new Vector2(0.027f, -0.095f), new Vector2(0.022f, -0.087f), new Vector2(0.0145f, -0.08f), new Vector2(0.015f, 0.1f), new Vector2(0.018f, 0.25f), new Vector2(0.026f, 0.42f), new Vector2(0.032f, 0.58f), new Vector2(0.0335f, 0.68f), new Vector2(0.031f, 0.705f), new Vector2(0.02f, 0.715f), new Vector2(0f, 0.718f) }, 20, wood);
                    mb.Sub(2).Lathe(Vector3.zero, Z, new List<Vector2> { new Vector2(0.0158f, -0.082f), new Vector2(0.0162f, 0.12f) }, 18, tape);
                    for (int i = 0; i < 12; i++) mb.Sub(2).Tube(new Vector3(0, 0, -0.078f + i * 0.017f), new Vector3(0, 0, -0.077f + i * 0.017f), 0.0166f, 0.015f, 14, Mul(tape, 1.8f));   // tape wraps
                    mb.Sub(0).Box(new Vector3(0, 0.0275f, 0.5f), new Vector3(0.016f, 0.002f, 0.06f), new Color(0.12f, 0.08f, 0.05f), 0f, 1, Quaternion.Euler(-1.8f, 0, 0));   // stamp
                    for (int i = 0; i < 5; i++) mb.Sub(0).Box(Quaternion.Euler(0, 0, i * 71f) * new Vector3(0, 0.032f, 0) + new Vector3(0, 0, 0.55f + i * 0.025f), new Vector3(0.006f, 0.001f, 0.018f), Mul(wood, 0.7f), 0f, 1, Quaternion.Euler(0, 0, i * 71f));   // scuffs
                    break;
                case 3:   // crowbar: a hex bar painted red, a polished hook with a split claw, a flat chisel end
                {
                    Color red = new Color(0.6f, 0.08f, 0.06f);
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.11f), new Vector3(0, 0, 0.42f), 0.011f, 0.011f, 6, red);
                    for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 0.92f, a2 = (i + 1) / 8f * Mathf.PI * 0.92f; mb.Sub(0).Cyl(new Vector3(0, -0.05f + Mathf.Cos(a) * 0.05f, 0.42f + Mathf.Sin(a) * 0.05f), new Vector3(0, -0.05f + Mathf.Cos(a2) * 0.05f, 0.42f + Mathf.Sin(a2) * 0.05f), 0.011f, 0.011f, 6, red); }
                    var tipP = new Vector3(0, -0.05f + Mathf.Cos(Mathf.PI * 0.92f) * 0.05f, 0.42f + Mathf.Sin(Mathf.PI * 0.92f) * 0.05f);
                    foreach (float x in new[] { -1f, 1f }) mb.Sub(1).Box(tipP + new Vector3(x * 0.0045f, -0.008f, -0.006f), new Vector3(0.007f, 0.02f, 0.012f), w * 0.75f, 0.001f, 1, Quaternion.Euler(-30f, 0, 0));   // claw
                    mb.Sub(1).Box(new Vector3(0, 0.01f, -0.122f), new Vector3(0.024f, 0.006f, 0.036f), w * 0.6f, 0.002f, 1, Quaternion.Euler(-24, 0, 0));   // chisel
                    mb.Sub(1).Cyl(new Vector3(0, 0, -0.105f), new Vector3(0, 0, -0.095f), 0.0115f, 0.0115f, 6, w * 0.6f);
                    break;
                }
                case 4:   // machete: riveted slab handle with a lanyard hole, a long blade widening to the tip, a bright edge
                {
                    Color grip = new Color(0.12f, 0.12f, 0.12f);
                    mb.Sub(0).Box(new Vector3(0, 0, -0.06f), new Vector3(0.026f, 0.032f, 0.13f), grip, 0.009f, 2);
                    for (int i = 0; i < 3; i++) mb.Sub(1).Cyl(new Vector3(-0.0135f, 0, -0.1f + i * 0.04f), new Vector3(0.0135f, 0, -0.1f + i * 0.04f), 0.0035f, 0.0035f, 8, Steel);
                    mb.Sub(0).Box(new Vector3(0, -0.017f, -0.06f), new Vector3(0.027f, 0.004f, 0.11f), Mul(grip, 1.6f), 0.001f);   // finger groove band
                    mb.Sub(1).Box(new Vector3(0, 0.002f, 0.008f), new Vector3(0.022f, 0.042f, 0.006f), w * 0.6f, 0.002f);   // bolster
                    var bl = new List<Vector2> { new Vector2(0.01f, -0.02f), new Vector2(0.3f, -0.027f), new Vector2(0.43f, -0.022f), new Vector2(0.475f, 0.0f), new Vector2(0.455f, 0.024f), new Vector2(0.3f, 0.022f), new Vector2(0.01f, 0.02f) };
                    mb.Sub(1).Extrude(bl, 0.0035f, w * 0.85f, 0.001f, new Vector3(0, 0.004f, 0));
                    mb.Sub(1).Box(new Vector3(0, 0.004f - 0.022f, 0.22f), new Vector3(0.004f, 0.005f, 0.4f), w * 1.2f, 0.0005f, 1, Quaternion.Euler(1.2f, 0, 0));   // edge
                    break;
                }
                case 5:   // sledgehammer: a hickory handle with a rubber grip, a steel head with chamfered striking faces and a wedge
                {
                    mb.Sub(0).Lathe(Vector3.zero, Z, new List<Vector2> { new Vector2(0f, -0.185f), new Vector2(0.02f, -0.185f), new Vector2(0.021f, -0.17f), new Vector2(0.017f, 0.1f), new Vector2(0.016f, 0.45f), new Vector2(0.019f, 0.6f), new Vector2(0.02f, 0.62f), new Vector2(0f, 0.63f) }, 16, ash);
                    mb.Sub(2).Lathe(Vector3.zero, Z, new List<Vector2> { new Vector2(0.0205f, -0.175f), new Vector2(0.0215f, -0.165f), new Vector2(0.0195f, 0.0f), new Vector2(0.0185f, 0.01f) }, 16, new Color(0.7f, 0.12f, 0.08f));
                    for (int i = 0; i < 10; i++) mb.Sub(2).Tube(new Vector3(0, 0, -0.16f + i * 0.016f), new Vector3(0, 0, -0.155f + i * 0.016f), 0.0212f, 0.019f, 14, new Color(0.5f, 0.08f, 0.05f));
                    Color head = new Color(0.28f, 0.28f, 0.3f);
                    mb.Sub(1).Box(new Vector3(0, 0, 0.64f), new Vector3(0.07f, 0.16f, 0.075f), head, 0.012f, 2);
                    foreach (float y in new[] { -1f, 1f }) mb.Sub(1).Box(new Vector3(0, y * 0.088f, 0.64f), new Vector3(0.062f, 0.018f, 0.067f), Mul(head, 1.3f), 0.008f, 2);   // striking faces
                    mb.Sub(1).Box(new Vector3(0, 0, 0.678f), new Vector3(0.008f, 0.03f, 0.002f), Steel, 0f);   // wedge
                    break;
                }
                case 6:   // fire axe: a red head with a polished bit and a pick, a yellow-banded handle with a flared knob.
                {         // The cutting edge is on -y, the side you swing with (like the machete and knives): it used to point back at you.
                    Color red = new Color(0.75f, 0.1f, 0.08f);
                    mb.Sub(0).Lathe(Vector3.zero, Z, new List<Vector2> { new Vector2(0f, -0.152f), new Vector2(0.022f, -0.152f), new Vector2(0.02f, -0.135f), new Vector2(0.016f, -0.1f), new Vector2(0.017f, 0.55f), new Vector2(0.018f, 0.63f), new Vector2(0f, 0.635f) }, 16, ash);
                    mb.Sub(2).Tube(new Vector3(0, 0, 0.4f), new Vector3(0, 0, 0.44f), 0.0178f, 0.016f, 14, new Color(0.9f, 0.75f, 0.1f));
                    mb.Sub(0).Box(new Vector3(0, 0.0f, 0.6f), new Vector3(0.042f, 0.065f, 0.07f), red, 0.008f, 2);   // eye
                    var bit = new List<Vector2> { new Vector2(0.565f, -0.03f), new Vector2(0.635f, -0.03f), new Vector2(0.67f, -0.2f), new Vector2(0.6f, -0.17f), new Vector2(0.53f, -0.2f) };
                    mb.Sub(0).Extrude(bit, 0.016f, red, 0.003f);
                    mb.Sub(1).Extrude(new List<Vector2> { new Vector2(0.535f, -0.18f), new Vector2(0.6f, -0.157f), new Vector2(0.665f, -0.18f), new Vector2(0.67f, -0.205f), new Vector2(0.6f, -0.175f), new Vector2(0.53f, -0.205f) }, 0.006f, w * 0.95f, 0.001f);   // ground edge
                    mb.Sub(0).Extrude(new List<Vector2> { new Vector2(0.575f, 0.03f), new Vector2(0.625f, 0.03f), new Vector2(0.6f, 0.13f) }, 0.016f, red, 0.003f);   // pick
                    mb.Sub(1).Box(new Vector3(0, 0.12f, 0.6f), new Vector3(0.006f, 0.014f, 0.008f), w * 0.8f, 0.001f);
                    break;
                }
                case 7:   // frying pan: a cast-iron pan with a pour lip, a riveted handle with a hanging hole
                {
                    Color iron = new Color(0.12f, 0.12f, 0.12f);
                    mb.Sub(0).Box(new Vector3(0, 0, 0.0f), new Vector3(0.026f, 0.018f, 0.2f), new Color(0.1f, 0.1f, 0.1f), 0.006f, 2);
                    mb.Sub(0).Box(new Vector3(0, 0.0095f, 0.0f), new Vector3(0.012f, 0.002f, 0.16f), Mul(iron, 0.7f), 0.001f);
                    mb.Sub(0).Cyl(new Vector3(0, -0.01f, -0.08f), new Vector3(0, 0.01f, -0.08f), 0.006f, 0.006f, 10, Black);   // hanging hole
                    mb.Push(new Vector3(0, -0.012f, 0.22f), Quaternion.identity);
                    mb.Sub(0).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.115f, 0f), new Vector2(0.122f, 0.004f), new Vector2(0.13f, 0.032f), new Vector2(0.133f, 0.034f), new Vector2(0.126f, 0.034f), new Vector2(0.116f, 0.01f), new Vector2(0.11f, 0.0075f), new Vector2(0f, 0.0075f) }, 32, iron);
                    mb.Sub(1).Cyl(new Vector3(0, 0.0076f, 0), new Vector3(0, 0.0078f, 0), 0.108f, 0.108f, 32, new Color(0.22f, 0.22f, 0.23f), false);   // seasoned cooking surface
                    mb.Pop();
                    foreach (float x in new[] { -0.008f, 0.008f }) mb.Sub(1).Sphere(new Vector3(x, 0.006f, 0.098f), Vector3.one * 0.004f, 6, Steel);   // rivets
                    break;
                }
                default:  // candy cane: a twisted barber-pole stripe and a hook
                {
                    Color red = new Color(0.9f, 0.12f, 0.12f), white = new Color(0.95f, 0.95f, 0.95f);
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.51f), 0.02f, 0.02f, 16, white);
                    for (int i = 0; i < 40; i++)
                    {
                        float z = -0.12f + i * 0.0155f, a = i * 40f;
                        mb.Sub(0).Box(new Vector3(0, 0, z) + Quaternion.Euler(0, 0, a) * new Vector3(0.0195f, 0, 0), new Vector3(0.004f, 0.014f, 0.02f), red, 0.001f, 1, Quaternion.Euler(0, 0, a) * Quaternion.Euler(0, 0, 0));
                    }
                    for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI, a2 = (i + 1) / 12f * Mathf.PI; mb.Sub(0).Cyl(new Vector3(0, -0.07f + Mathf.Cos(a) * 0.07f, 0.51f + Mathf.Sin(a) * 0.07f), new Vector3(0, -0.07f + Mathf.Cos(a2) * 0.07f, 0.51f + Mathf.Sin(a2) * 0.07f), 0.02f, 0.02f, 14, i % 2 == 0 ? white : red); }
                    mb.Sub(0).Sphere(new Vector3(0, -0.14f, 0.51f), Vector3.one * 0.02f, 12, red);
                    mb.Sub(0).Sphere(new Vector3(0, 0, -0.12f), Vector3.one * 0.02f, 12, white);
                    break;
                }
            }
            return mb;
        }
    }
}
