using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The 3D models of the simple items: meds, ammo boxes, keycards, valuables and junk. Each is built from a few dozen
    /// shaped parts in four materials (0 painted / plastic, 1 metal - gold on gold items, 2 cloth / paper, 3 gloss: glass,
    /// screens, glossy labels). Sizes match the real things. Kept apart from the item logic so the same code can be
    /// rendered outside the game to check how they look.
    /// </summary>
    public static class ItemModels
    {
        public static readonly Color Paper = new Color(0.93f, 0.92f, 0.88f), Black = new Color(0.06f, 0.06f, 0.07f), Steel = new Color(0.62f, 0.63f, 0.66f),
            Brass = new Color(0.85f, 0.66f, 0.3f), Red = new Color(0.78f, 0.1f, 0.08f), DarkGrey = new Color(0.18f, 0.18f, 0.2f);

        public static Material[] Materials(SimpleDef d)
        {
            Color w = Color.white;
            var m0 = Mats.Lit("item_plastic", w, 0f, 0.35f, Mats.Noise, null, 0.3f, 0.1f);
            var m1 = Mats.Lit("item_metal", w, 0.9f, 0.7f, Mats.Metal, null, 0.1f, 0.2f);
            var m2 = Mats.Lit("item_cloth", w, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.2f, 0f);
            var m3 = Mats.Lit("item_gloss", w, 0.1f, 0.92f, null, null, 0.02f, 0.02f);
            bool gold = d.kind == ItemKind.Valuable && (d.shape == 6 || d.shape == 0 || d.shape == 18 || d.shape == 19 || d.shape == 33 || d.shape == 36);   // 33: the operator figurine's gold armour trim
            if (gold) m1 = Mats.Gold;
            if (d.kind == ItemKind.Valuable && d.shape == 7) m1 = Mats.Unlit("diamond", new Color(0.5f, 0.85f, 1f, 0.85f));
            if (d.kind == ItemKind.Valuable && d.shape == 36)   // the OLIVER kicks: a glowing mint stripe
                return new[] { m0, m1, m2, m3, Mats.Lit("item_glow_mint", new Color(0.4f, 1f, 0.8f), 0f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(0.3f, 1.4f, 0.9f)) };
            if (d.kind == ItemKind.Valuable && (d.shape == 32 || d.shape == 33 || d.shape == 37))   // the streamer figurines: their display base's LED ring glows
                return new[] { m0, m1, m2, m3, Mats.Lit("item_glow_purple", new Color(0.75f, 0.45f, 1f), 0f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(0.9f, 0.4f, 1.6f)) };
            return new[] { m0, m1, m2, m3 };
        }

        /// <summary>The name on a streamer figurine's base plate (null = no plate), where the plate is, and its tilt.</summary>
        public static string Nameplate(SimpleDef d, out Vector3 at)
        {
            at = Vector3.zero;
            if (d == null || d.kind != ItemKind.Valuable) return null;
            if (d.shape == 32) { at = new Vector3(0, -0.0655f, 0.0464f); return "PEANUT8"; }
            if (d.shape == 33) { at = new Vector3(0, -0.0935f, 0.0524f); return "SHUSHED"; }
            if (d.shape == 37) { at = new Vector3(0, -0.0935f, 0.0524f); return "S1KDM0R3"; }
            return null;
        }

        /// <summary>Printed words on an item (text, local position, size, colour), drawn facing out of the front (+Z).</summary>
        public static (string text, Vector3 at, float size, Color col)[] Labels(SimpleDef d)
        {
            if (d != null && d.kind == ItemKind.Valuable && d.shape == 36)   // OLIVER in gold on the side of the kicks
                return new[] { ("OLIVER", new Vector3(-0.03f, 0.084f, 0.0535f), 0.0105f, new Color(1f, 0.82f, 0.3f)) };
            if (d != null && d.kind == ItemKind.Med && d.shape == 5)
                return new[] { ("NUKE", new Vector3(0, 0.0115f, 0.0338f), 0.016f, new Color(0.45f, 1f, 0.15f)), ("COLA", new Vector3(0, 0.0005f, 0.0338f), 0.008f, Color.white), ("mildly radioactive", new Vector3(0, -0.037f, 0.0338f), 0.0033f, new Color(0.75f, 0.75f, 0.75f)) };
            if (d != null && d.kind == ItemKind.Med && (d.shape == 6 || d.shape == 7))
            {
                const float z = 0.0292f;
                bool sb = d.shape == 6;
                Color ink = new Color(0.06f, 0.06f, 0.06f);
                var l = new List<(string, Vector3, float, Color)> {
                    ("ENERGY DRINK    YOUR TASTE", new Vector3(0, 0.058f, z + 0.0004f), 0.0028f, new Color(0.3f, 0.3f, 0.3f)),
                    (sb ? "STRAWBERRY BANANA" : "MANGO ORANGE CREAM", new Vector3(0, 0.0492f, z + 0.0004f), 0.0042f, sb ? new Color(1f, 0.9f, 0.2f) : new Color(0.92f, 0.3f, 0.04f)),
                    ("SPONSOR", new Vector3(0, 0.039f, z + 0.0012f), 0.0068f, new Color(0.85f, 0.08f, 0.08f)),
                    ("|BE BETTER", new Vector3(-0.0145f, 0.008f, z - 0.0012f), 0.0055f, ink),
                    ("FOCUS ENERGY DRINK", new Vector3(0, -0.037f, z + 0.0004f), 0.0042f, new Color(0.85f, 0.97f, 0.2f)),
                    ("DRINK ME AND\nGO CONQUER A TASK", new Vector3(0, -0.0495f, z + 0.0004f), 0.0034f, ink),
                    ("12 FL OZ (355mL)", new Vector3(0, -0.059f, z + 0.0004f), 0.0026f, ink) };
                // DUBBY: big white letters with a thick black outline (black copies behind, nudged all round)
                var c0 = new Vector3(0.002f, -0.0008f, z + 0.001f);
                for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; l.Add(("|DUBBY", c0 + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.0009f, 0.029f, ink)); }
                l.Add(("|DUBBY", c0 + new Vector3(0, 0, 0.0004f), 0.029f, Color.white));
                return l.ToArray();
            }
            if (d == null || d.kind != ItemKind.Valuable) return null;
            if (d.shape == 35) return new[] { ("OWAYS", new Vector3(0, 0.081f, 0.0272f), 0.024f, Color.white), ("CEREAL", new Vector3(0, 0.0605f, 0.0272f), 0.012f, new Color(1f, 0.85f, 0.3f)), ("crunchy rings", new Vector3(-0.024f, 0.022f, 0.0262f), 0.0075f, Color.white), ("LIVE", new Vector3(0.049f, 0.012f, 0.0272f), 0.008f, Color.white), ("NET WT 12 OZ (340 g)", new Vector3(-0.042f, -0.093f, 0.0258f), 0.0032f, Color.white) };   // big enough to read from arm's length
            return null;
        }

        public static MB Build(SimpleDef d)
        {
            var mb = new MB(4);
            Color c = d.color;
            switch (d.kind)
            {
                case ItemKind.Med: Med(mb, d, c); break;
                case ItemKind.Ammo: AmmoBox(mb, d); break;
                case ItemKind.Key: Keycard(mb, d, c); break;
                default: Valuable(mb, d, c); break;
            }
            return mb;
        }

        // ------------------------------------------------------------------------------------------------ meds
        static void Med(MB mb, SimpleDef d, Color c)
        {
            switch (d.shape)
            {
                case 0: // bandage: a rolled gauze bandage with a loose tail and its metal clip
                    mb.Sub(2).Cyl(new Vector3(-0.04f, 0, 0), new Vector3(0.04f, 0, 0), 0.03f, 0.03f, 18, c);
                    mb.Sub(2).Cyl(new Vector3(-0.0405f, 0, 0), new Vector3(0.0405f, 0, 0), 0.012f, 0.012f, 12, c * 0.82f);   // the tight core at the ends
                    for (int i = 0; i < 3; i++) { float r = 0.017f + i * 0.0045f; mb.Sub(2).Tube(new Vector3(-0.0408f, 0, 0), new Vector3(0.0408f, 0, 0), r + 0.0008f, r, 18, c * 0.9f); }   // wound layers
                    mb.Sub(2).Box(new Vector3(0, -0.032f, 0.03f), new Vector3(0.078f, 0.002f, 0.06f), c, 0f, 1, Quaternion.Euler(-28f, 0, 0));   // tail
                    mb.Sub(1).Box(new Vector3(0, 0.024f, 0.017f), new Vector3(0.016f, 0.008f, 0.012f), Steel, 0.001f, 1, Quaternion.Euler(-35f, 0, 0));   // clip
                    mb.Sub(0).Box(new Vector3(0, 0.0302f, 0), new Vector3(0.04f, 0.0012f, 0.012f), Red, 0f);   // printed stripe
                    break;
                case 1: // medkit / trauma kit: a hard case with a lid seam, carry handle, latches, corner bumpers and a raised cross
                {
                    Vector3 s = new Vector3(0.18f, 0.12f, 0.07f);
                    mb.Sub(0).Box(Vector3.zero, s, c, 0.012f, 2);
                    mb.Sub(0).Box(new Vector3(0, 0, 0), new Vector3(s.x + 0.002f, s.y + 0.002f, 0.004f), c * 0.6f, 0.002f);   // lid seam
                    foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f })
                        mb.Sub(0).Box(new Vector3(x * (s.x / 2 - 0.008f), y * (s.y / 2 - 0.008f), 0), new Vector3(0.018f, 0.018f, s.z + 0.006f), DarkGrey, 0.005f);   // bumpers
                    mb.Sub(0).Box(new Vector3(0, 0, s.z / 2 + 0.002f), new Vector3(0.06f, 0.018f, 0.004f), Color.white, 0.002f);
                    mb.Sub(0).Box(new Vector3(0, 0, s.z / 2 + 0.002f), new Vector3(0.018f, 0.06f, 0.004f), Color.white, 0.002f);
                    foreach (float x in new[] { -0.05f, 0.05f })
                    {
                        mb.Sub(1).Box(new Vector3(x, s.y / 2 + 0.004f, 0.01f), new Vector3(0.016f, 0.008f, 0.014f), Steel, 0.002f);   // latches
                        mb.Sub(1).Cyl(new Vector3(x * 0.6f, s.y / 2, 0), new Vector3(x * 0.6f, s.y / 2 + 0.018f, 0), 0.005f, 0.005f, 8, DarkGrey);   // handle posts
                    }
                    mb.Sub(0).Capsule(new Vector3(-0.03f, s.y / 2 + 0.02f, 0), new Vector3(0.03f, s.y / 2 + 0.02f, 0), 0.007f, 10, DarkGrey);   // handle grip
                    break;
                }
                case 2: // adrenal stim: an auto-injector - grip ribs, a window onto the dose, a safety cap and a label band
                    mb.Sub(0).Cyl(new Vector3(0, -0.055f, 0), new Vector3(0, 0.05f, 0), 0.014f, 0.014f, 16, c);
                    for (int i = 0; i < 4; i++) mb.Sub(0).Cyl(new Vector3(0, -0.035f + i * 0.008f, 0), new Vector3(0, -0.031f + i * 0.008f, 0), 0.0152f, 0.0152f, 16, c * 0.7f);
                    mb.Sub(3).Box(new Vector3(0, 0.018f, 0.0132f), new Vector3(0.008f, 0.03f, 0.002f), new Color(0.3f, 0.95f, 1f), 0.001f);   // dose window
                    mb.Sub(2).Cyl(new Vector3(0, 0f, 0), new Vector3(0, 0.01f, 0), 0.0146f, 0.0146f, 16, Color.white);   // label
                    mb.Sub(0).Cyl(new Vector3(0, 0.05f, 0), new Vector3(0, 0.072f, 0), 0.015f, 0.012f, 16, new Color(1f, 0.6f, 0.05f));   // safety cap
                    mb.Sub(1).Cyl(new Vector3(0, -0.055f, 0), new Vector3(0, -0.066f, 0), 0.012f, 0.008f, 12, Steel);   // needle end
                    break;
                case 3: // painkillers: an amber pill bottle with a ribbed white child-proof cap and a label
                    mb.Sub(3).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, -0.04f), new Vector2(0.02f, -0.04f), new Vector2(0.022f, -0.036f), new Vector2(0.022f, 0.026f), new Vector2(0.018f, 0.032f), new Vector2(0.017f, 0.034f) }, 18, c);
                    mb.Sub(2).Cyl(new Vector3(0, -0.026f, 0), new Vector3(0, 0.014f, 0), 0.0225f, 0.0225f, 18, Color.white, false);   // label
                    mb.Sub(0).Cyl(new Vector3(0, -0.012f, 0), new Vector3(0, -0.004f, 0), 0.0228f, 0.0228f, 18, Red, false);   // label stripe
                    mb.Sub(0).Cyl(new Vector3(0, 0.032f, 0), new Vector3(0, 0.05f, 0), 0.0235f, 0.0235f, 20, Color.white);   // cap
                    for (int i = 0; i < 20; i++) { float a = i * Mathf.PI * 2f / 20f; mb.Sub(0).Box(new Vector3(Mathf.Cos(a) * 0.0237f, 0.041f, Mathf.Sin(a) * 0.0237f), new Vector3(0.002f, 0.016f, 0.002f), new Color(0.88f, 0.88f, 0.88f), 0f); }   // ribs
                    break;
                case 4: // IFAK: a soft pouch - MOLLE webbing rows, a zip with a pull tab, a red cross patch and a velcro lid
                {
                    Vector3 s = new Vector3(0.16f, 0.1f, 0.07f);
                    mb.Sub(2).Box(Vector3.zero, s, c, 0.022f, 2);
                    for (int i = 0; i < 2; i++) mb.Sub(2).Box(new Vector3(0, -0.025f + i * 0.03f, -s.z / 2 - 0.002f), new Vector3(s.x * 0.92f, 0.016f, 0.004f), c * 0.75f, 0.001f);   // webbing (back)
                    mb.Sub(2).Box(new Vector3(0, s.y / 2 - 0.006f, 0.004f), new Vector3(s.x * 0.94f, 0.012f, s.z * 0.9f), c * 0.85f, 0.004f);   // lid
                    mb.Sub(0).Box(new Vector3(0, s.y / 2 - 0.016f, s.z / 2 - 0.004f), new Vector3(s.x * 0.86f, 0.004f, 0.006f), DarkGrey, 0.001f);   // zip
                    mb.Sub(1).Box(new Vector3(s.x * 0.36f, s.y / 2 - 0.026f, s.z / 2 - 0.002f), new Vector3(0.008f, 0.016f, 0.003f), Steel, 0.001f);   // pull tab
                    mb.Sub(2).Box(new Vector3(0, -0.008f, s.z / 2 - 0.002f), new Vector3(0.05f, 0.05f, 0.003f), new Color(0.15f, 0.15f, 0.15f), 0.004f);   // patch
                    mb.Sub(0).Box(new Vector3(0, -0.008f, s.z / 2 + 0.0002f), new Vector3(0.034f, 0.011f, 0.002f), Red, 0f);
                    mb.Sub(0).Box(new Vector3(0, -0.008f, s.z / 2 + 0.0002f), new Vector3(0.011f, 0.034f, 0.002f), Red, 0f);
                    break;
                }
                case 5: // Nuke Cola: a glossy black can, radioactive-green ooze bands dripping down, a yellow hazard badge with the trefoil on the front
                {
                    Color al = new Color(0.8f, 0.8f, 0.82f), body = new Color(0.05f, 0.05f, 0.06f), ooze = new Color(0.45f, 1f, 0.15f);
                    const float R = 0.033f;
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, -0.061f), new Vector2(0.026f, -0.061f), new Vector2(0.031f, -0.056f), new Vector2(R, -0.05f) }, 24, al);
                    mb.Sub(3).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, 0.046f, 0), R, R, 28, body);
                    mb.Sub(3).Cyl(new Vector3(0, 0.03f, 0), new Vector3(0, 0.046f, 0), R + 0.0002f, R + 0.0002f, 28, ooze, false);   // green top band
                    mb.Sub(3).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, -0.043f, 0), R + 0.0002f, R + 0.0002f, 28, ooze, false);   // and the foot
                    for (int k = 0; k < 16; k++)   // ooze running down from the top band
                    {
                        float a = k / 16f * Mathf.PI * 2f + 0.2f, len = 0.005f + (k * 29 % 9) * 0.0016f;
                        var p = new Vector3(Mathf.Sin(a) * (R + 0.0004f), 0.03f - len * 0.5f, Mathf.Cos(a) * (R + 0.0004f));
                        mb.Sub(3).Box(p, new Vector3(0.0055f, len, 0.0012f), ooze, 0.0006f, 1, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                        mb.Sub(3).Sphere(p + Vector3.down * len * 0.5f, new Vector3(0.0032f, 0.0036f, 0.0012f), 8, ooze);
                    }
                    // the hazard badge: yellow disc with a black rim and the three-bladed trefoil
                    var bc = new Vector3(0, -0.017f, R + 0.0006f);
                    Fan(mb, 3, bc, Quaternion.identity, 0.0135f, 0f, 360f, 28, Black);
                    Fan(mb, 3, bc + new Vector3(0, 0, 0.0002f), Quaternion.identity, 0.0122f, 0f, 360f, 28, new Color(1f, 0.85f, 0.1f));
                    for (int k = 0; k < 3; k++) Fan(mb, 3, bc + new Vector3(0, 0, 0.0004f), Quaternion.identity, 0.0105f, 60f + k * 120f, 120f + k * 120f, 8, Black);
                    Fan(mb, 3, bc + new Vector3(0, 0, 0.0006f), Quaternion.identity, 0.0034f, 0f, 360f, 16, new Color(1f, 0.85f, 0.1f));
                    Fan(mb, 3, bc + new Vector3(0, 0, 0.0008f), Quaternion.identity, 0.0022f, 0f, 360f, 16, Black);
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(R, 0.046f), new Vector2(0.029f, 0.056f), new Vector2(0.0285f, 0.06f), new Vector2(0.026f, 0.059f), new Vector2(0f, 0.058f) }, 24, al);
                    mb.Sub(1).Tube(new Vector3(0, 0.0585f, 0.006f), new Vector3(0, 0.0595f, 0.006f), 0.0075f, 0.004f, 12, ooze * 0.8f);   // green ring pull
                    mb.Sub(1).Box(new Vector3(0, 0.0592f, -0.004f), new Vector3(0.008f, 0.0008f, 0.01f), new Color(0.7f, 0.7f, 0.72f), 0.0004f);
                    break;
                }
                case 6:   // DUBBY Strawberry Banana: slim can, pink dripping into yellow
                case 7:   // DUBBY Mango Orange Cream: yellow dripping into orange
                {
                    bool sb = d.shape == 6;
                    Color top = sb ? new Color(0.94f, 0.2f, 0.6f) : new Color(0.99f, 0.82f, 0.16f), bot = sb ? new Color(0.99f, 0.88f, 0.2f) : new Color(0.98f, 0.45f, 0.1f);
                    Color band = sb ? new Color(0.84f, 0.12f, 0.55f) : new Color(0.93f, 0.36f, 0.06f), white = new Color(0.97f, 0.97f, 0.97f);
                    Color al = new Color(0.82f, 0.82f, 0.84f);
                    const float R = 0.0285f, y0 = -0.066f, y1 = 0.062f, seam = -0.012f;
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, y0 - 0.004f), new Vector2(R - 0.004f, y0 - 0.004f), new Vector2(R, y0) }, 20, al);   // base
                    // bands bottom to top: white foot, FOCUS band, the bottom colour, the top colour, white rim
                    mb.Sub(3).Cyl(new Vector3(0, y0, 0), new Vector3(0, -0.042f, 0), R, R, 22, white);
                    mb.Sub(3).Cyl(new Vector3(0, -0.042f, 0), new Vector3(0, -0.032f, 0), R, R, 22, band);
                    mb.Sub(3).Cyl(new Vector3(0, -0.032f, 0), new Vector3(0, seam, 0), R, R, 22, bot);
                    mb.Sub(3).Cyl(new Vector3(0, seam, 0), new Vector3(0, 0.054f, 0), R, R, 22, top);
                    mb.Sub(3).Cyl(new Vector3(0, 0.054f, 0), new Vector3(0, y1, 0), R, R, 22, white);
                    // drips of the top colour running down into the bottom one, all the way round
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f, len = 0.006f + (i * 37 % 11) * 0.0013f;
                        var p = new Vector3(Mathf.Sin(a) * (R + 0.0004f), seam - len * 0.5f, Mathf.Cos(a) * (R + 0.0004f));
                        mb.Sub(3).Box(p, new Vector3(0.006f, len, 0.0012f), top, 0.0006f, 1, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                        mb.Sub(3).Sphere(p + Vector3.down * len * 0.5f, new Vector3(0.0034f, 0.0034f, 0.0012f), 8, top);
                    }
                    // the yellow SPONSOR box under the flavour name (front), outlined black
                    mb.Sub(0).Box(new Vector3(0, 0.039f, R - 0.0035f), new Vector3(0.032f, 0.0105f, 0.008f), new Color(1f, 0.9f, 0.15f), 0.002f);
                    mb.Sub(0).Box(new Vector3(0, 0.039f, R - 0.0038f), new Vector3(0.0345f, 0.013f, 0.0075f), new Color(0.06f, 0.06f, 0.06f), 0.002f);
                    // top: neck, rim, tab
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(R, y1), new Vector2(0.025f, y1 + 0.006f), new Vector2(0.0245f, y1 + 0.009f), new Vector2(0.022f, y1 + 0.0085f), new Vector2(0f, y1 + 0.008f) }, 20, al);
                    mb.Sub(1).Tube(new Vector3(0, y1 + 0.0085f, 0.005f), new Vector3(0, y1 + 0.0095f, 0.005f), 0.0065f, 0.0035f, 12, al * 0.85f);
                    mb.Sub(1).Cyl(new Vector3(0, y1 + 0.0082f, 0.0005f), new Vector3(0, y1 + 0.0098f, 0.0005f), 0.0018f, 0.0016f, 8, al * 0.7f);   // rivet
                    mb.Sub(1).Box(new Vector3(0, y1 + 0.0081f, -0.008f), new Vector3(0.012f, 0.0006f, 0.009f), al * 0.6f, 0.003f);                 // the scored mouth
                    // ice-cold: little drops of condensation all over the can
                    var drops = new System.Random(d.shape * 17 + 3);
                    for (int i = 0; i < 34; i++)
                    {
                        float a = (float)drops.NextDouble() * Mathf.PI * 2f, y = y0 + 0.006f + (float)drops.NextDouble() * (y1 - y0 - 0.012f), sz = 0.0004f + (float)drops.NextDouble() * 0.0006f;
                        var n = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                        mb.Sub(3).Sphere(n * (R + 0.0002f) + Vector3.up * y, new Vector3(sz, sz * 1.3f, sz * 0.6f), 6, new Color(0.72f, 0.78f, 0.84f));
                    }
                    break;
                }
                default:
                    mb.Sub(0).Cyl(new Vector3(0, -0.06f, 0), new Vector3(0, 0.05f, 0), 0.014f, 0.014f, 12, c);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------ figurine helpers
        /// <summary>The streamer figurines' display base: a bevelled dark plinth, a glowing purple ring, a glossy purple top and the red name plate at the front. y = the top surface.</summary>
        static void DisplayBase(MB mb, float y, float r)
        {
            var purple = new Color(0.45f, 0.2f, 0.75f);
            mb.Sub(0).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, y - 0.016f), new Vector2(r + 0.003f, y - 0.016f), new Vector2(r + 0.005f, y - 0.0135f), new Vector2(r + 0.005f, y - 0.0075f), new Vector2(r + 0.003f, y - 0.005f), new Vector2(0f, y - 0.005f) }, 40, DarkGrey);
            mb.Sub(4).Tube(new Vector3(0, y - 0.0052f, 0), new Vector3(0, y - 0.0032f, 0), r + 0.0014f, r - 0.003f, 40, Color.white);   // the glowing LED ring
            mb.Sub(4).Tube(new Vector3(0, y - 0.0156f, 0), new Vector3(0, y - 0.0146f, 0), r + 0.0034f, r + 0.0005f, 40, Color.white);   // and a thin one round the foot
            mb.Sub(3).Cyl(new Vector3(0, y - 0.0035f, 0), new Vector3(0, y, 0), r - 0.002f, r - 0.003f, 40, purple);
            mb.Sub(3).Box(new Vector3(0, y - 0.0105f, r + 0.0045f), new Vector3(0.05f, 0.0085f, 0.003f), Red, 0.001f, 2, Quaternion.Euler(-6f, 0, 0));   // name plate
        }

        /// <summary>A torus (ring) lying in rot's XY plane. zScale flattens it towards the plane (printed rings).</summary>
        static void Torus(MB mb, Vector3 c, Quaternion rot, float R, float r, int nu, int nv, Color col, float zScale = 1f)
        {
            mb.Push(c, rot, new Vector3(1f, 1f, zScale));
            for (int i = 0; i < nu; i++)
            {
                float a0 = i * Mathf.PI * 2f / nu, a1 = (i + 1) * Mathf.PI * 2f / nu;
                mb.Capsule(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * R, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * R, r, Mathf.Max(6, nv + 2), col);
            }
            mb.Pop();
        }

        /// <summary>A one-sided flat pie slice (printed artwork): centre c, angles a0..a1 in degrees, facing rot*forward; sy squashes it vertically.</summary>
        static void Fan(MB mb, int sub, Vector3 c, Quaternion rot, float r, float a0, float a1, int seg, Color col, float sy = 1f)
        {
            mb.Sub(sub).Patch(seg, 1, (u, v) =>
            {
                float a = Mathf.Lerp(a0, a1, u / (float)seg) * Mathf.Deg2Rad;
                return c + rot * new Vector3(Mathf.Cos(a) * r * v, Mathf.Sin(a) * r * v * sy, 0f);
            }, col);
        }

        /// <summary>A patch of a sphere's surface (a visor, a mask): yaw th0..th1 round from the front (+Z), elevation el0..el1, in degrees.</summary>
        static void SpherePatch(MB mb, int sub, Vector3 c, float R, float th0, float th1, float el0, float el1, int nu, int nv, Color col)
        {
            mb.Sub(sub).Patch(nu, nv, (u, v) =>
            {
                float th = Mathf.Lerp(th0, th1, u / (float)nu) * Mathf.Deg2Rad, el = Mathf.Lerp(el0, el1, v / (float)nv) * Mathf.Deg2Rad;
                return c + new Vector3(Mathf.Sin(th) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(th) * Mathf.Cos(el)) * R;
            }, col, false, true);
        }

        /// <summary>A white cartoon glove: a puffy palm, four fingers (spread open or curled into a fist), a thumb and a rolled cuff. q points the fingers along its up.</summary>
        static void Glove(MB mb, Vector3 p, Quaternion q, float s, bool open)
        {
            var w = new Color(0.97f, 0.97f, 0.97f);
            mb.Sub(3).Sphere(p, new Vector3(1f, 1.05f, 0.62f) * s, 14, w);
            for (int i = 0; i < 4; i++)
            {
                if (open) { var b = p + q * new Vector3((i - 1.5f) * 0.42f * s, 0.7f * s, 0); var dir = q * (Quaternion.Euler(0, 0, 24f - i * 16f) * Vector3.up); mb.Sub(3).Capsule(b, b + dir * s * 1.05f, s * 0.3f, 8, w); }
                else mb.Sub(3).Sphere(p + q * new Vector3((i - 1.5f) * 0.42f * s, 0.55f * s, 0.42f * s), Vector3.one * s * 0.36f, 10, w);
            }
            var tb = p + q * new Vector3(-0.85f * s, -0.1f * s, 0.25f * s);
            mb.Sub(3).Capsule(tb, tb + q * new Vector3(-0.45f, 0.6f, 0.3f) * s, s * 0.3f, 8, w);
            Torus(mb, p + q * new Vector3(0, -0.95f * s, 0), q * Quaternion.Euler(90f, 0, 0), s * 0.72f, s * 0.3f, 16, 6, w);
        }

        // ------------------------------------------------------------------------------------------------ ammo
        static void AmmoBox(MB mb, SimpleDef d)
        {
            // a cardboard carton: a lid, a coloured type label, a calibre stripe - and a few rounds standing in the cut-out
            Vector3 s = new Vector3(0.12f, 0.07f, 0.08f);
            Color card = new Color(0.36f, 0.4f, 0.26f), label = d.ammoType == AmmoType.AP ? new Color(0.08f, 0.08f, 0.1f) : d.ammoType == AmmoType.Incendiary ? new Color(0.85f, 0.15f, 0.08f) : new Color(0.9f, 0.76f, 0.2f);
            mb.Sub(2).Box(Vector3.zero, s, card, 0.004f);
            mb.Sub(2).Box(new Vector3(0, s.y / 2 - 0.009f, 0), new Vector3(s.x + 0.003f, 0.018f, s.z + 0.003f), card * 0.85f, 0.003f);   // lid
            mb.Sub(0).Box(new Vector3(0, -0.004f, s.z / 2 + 0.0016f), new Vector3(0.09f, 0.032f, 0.002f), label, 0.001f);
            mb.Sub(0).Box(new Vector3(-0.03f, -0.004f, s.z / 2 + 0.0028f), new Vector3(0.018f, 0.026f, 0.001f), Color.white, 0f);   // calibre block
            mb.Sub(0).Box(new Vector3(0, -0.026f, s.z / 2 + 0.0016f), new Vector3(s.x * 0.9f, 0.006f, 0.002f), Color.white, 0f);
            // the open end of the lid with brass cartridges showing
            mb.Sub(0).Box(new Vector3(0.032f, s.y / 2 + 0.0002f, 0), new Vector3(0.04f, 0.002f, 0.05f), new Color(0.06f, 0.06f, 0.05f), 0f);
            for (int i = 0; i < 6; i++)
            {
                float x = 0.02f + (i % 3) * 0.012f, z = -0.01f + (i / 3) * 0.02f;
                mb.Sub(1).Cyl(new Vector3(x, s.y / 2 - 0.01f, z), new Vector3(x, s.y / 2 + 0.006f, z), 0.0048f, 0.0048f, 10, Brass);
                mb.Sub(1).Cyl(new Vector3(x, s.y / 2 + 0.006f, z), new Vector3(x, s.y / 2 + 0.014f, z), 0.0045f, 0.0012f, 10, d.ammoType == AmmoType.AP ? Black : d.ammoType == AmmoType.Incendiary ? Red : new Color(0.75f, 0.42f, 0.25f));
            }
        }

        // ------------------------------------------------------------------------------------------------ keycards
        static void Keycard(MB mb, SimpleDef d, Color c)
        {
            if (d.shape == 1)
            {
                // the North Pole card: big, red, a green band, a gold chip, a snowflake - in a silver frame
                mb.Sub(1).Box(Vector3.zero, new Vector3(0.156f, 0.101f, 0.005f), Steel, 0.005f);
                mb.Sub(0).Box(new Vector3(0, 0, 0.0005f), new Vector3(0.148f, 0.093f, 0.006f), c, 0.004f);
                mb.Sub(0).Box(new Vector3(0, -0.028f, 0.0036f), new Vector3(0.148f, 0.018f, 0.002f), new Color(0.1f, 0.45f, 0.18f), 0f);
                mb.Sub(1).Box(new Vector3(-0.045f, 0.015f, 0.0036f), new Vector3(0.03f, 0.024f, 0.002f), new Color(1f, 0.8f, 0.3f), 0.001f);
                for (int i = 0; i < 3; i++) mb.Sub(0).Box(new Vector3(0.035f, 0.012f, 0.004f), new Vector3(0.05f, 0.006f, 0.002f), Color.white, 0f, 1, Quaternion.Euler(0, 0, i * 60f));
                mb.Sub(1).Tube(new Vector3(0.066f, 0.04f, -0.001f), new Vector3(0.066f, 0.04f, 0.006f), 0.006f, 0.0035f, 12, Steel);
                return;
            }
            // vault keycard: rounded plastic card, gold contact chip, magnetic stripe on the back, printed bands, a lanyard hole
            mb.Sub(0).Box(Vector3.zero, new Vector3(0.085f, 0.054f, 0.0035f), c, 0.003f);
            mb.Sub(3).Box(new Vector3(0.012f, 0.012f, 0.0019f), new Vector3(0.05f, 0.012f, 0.0008f), Color.Lerp(c, Color.white, 0.6f), 0f);   // printed band
            mb.Sub(3).Box(new Vector3(0.012f, -0.004f, 0.0019f), new Vector3(0.05f, 0.004f, 0.0008f), Color.Lerp(c, Color.black, 0.5f), 0f);
            mb.Sub(1).Box(new Vector3(-0.026f, -0.006f, 0.002f), new Vector3(0.012f, 0.01f, 0.001f), new Color(1f, 0.8f, 0.3f), 0.0005f);   // chip
            mb.Sub(0).Box(new Vector3(0, 0.01f, -0.0019f), new Vector3(0.085f, 0.01f, 0.0008f), Black, 0f);   // mag stripe
            mb.Sub(1).Tube(new Vector3(0.034f, 0.019f, -0.0025f), new Vector3(0.034f, 0.019f, 0.0025f), 0.004f, 0.0024f, 10, Steel);   // hole grommet
            mb.Sub(0).Box(new Vector3(-0.02f, -0.02f, 0.0019f), new Vector3(0.03f, 0.004f, 0.0008f), Color.white, 0f);   // printed id
        }

        // ------------------------------------------------------------------------------------------------ valuables / junk
        static void Valuable(MB mb, SimpleDef d, Color c)
        {
            switch (d.shape)
            {
                case 0: // gold chain: interlocking oval links in a loose loop
                    for (int i = 0; i < 22; i++)
                    {
                        float a = i / 22f * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * 0.034f, Mathf.Sin(a * 2f) * 0.003f, Mathf.Sin(a) * 0.026f);
                        var t = new Vector3(-Mathf.Sin(a) * 0.034f, 0, Mathf.Cos(a) * 0.026f).normalized;
                        var axis = i % 2 == 0 ? Vector3.up : Vector3.Cross(t, Vector3.up);
                        mb.Push(p, Quaternion.LookRotation(t, axis), new Vector3(1f, 1f, 1.5f));
                        mb.Sub(1).Tube(new Vector3(0, -0.0009f, 0), new Vector3(0, 0.0009f, 0), 0.0045f, 0.0026f, 10, c);
                        mb.Pop();
                    }
                    break;
                case 1: // luxury watch: a polished case, bezel, glossy dial with hands and markers, crown, leather strap
                    mb.Sub(1).Cyl(new Vector3(0, -0.005f, 0), new Vector3(0, 0.005f, 0), 0.02f, 0.02f, 28, c);
                    mb.Sub(1).Tube(new Vector3(0, 0.005f, 0), new Vector3(0, 0.008f, 0), 0.0205f, 0.0165f, 28, c * 0.85f);   // bezel
                    mb.Sub(3).Cyl(new Vector3(0, 0.005f, 0), new Vector3(0, 0.0062f, 0), 0.0166f, 0.0166f, 28, new Color(0.05f, 0.08f, 0.16f));   // dial
                    for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; mb.Sub(1).Box(new Vector3(Mathf.Cos(a) * 0.0135f, 0.0066f, Mathf.Sin(a) * 0.0135f), new Vector3(0.0012f, 0.0006f, i % 3 == 0 ? 0.004f : 0.0022f), c, 0f, 1, Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0)); }
                    mb.Sub(1).Box(new Vector3(0.003f, 0.0072f, 0.002f), new Vector3(0.0012f, 0.0006f, 0.011f), Color.white, 0f, 1, Quaternion.Euler(0, 55f, 0));   // hands
                    mb.Sub(1).Box(new Vector3(-0.0035f, 0.0074f, 0.0015f), new Vector3(0.001f, 0.0006f, 0.008f), Color.white, 0f, 1, Quaternion.Euler(0, -70f, 0));
                    mb.Sub(1).Cyl(new Vector3(0.02f, 0, 0), new Vector3(0.025f, 0, 0), 0.0028f, 0.0028f, 10, c);   // crown
                    foreach (float z in new[] { -1f, 1f })
                        for (int k = 0; k < 3; k++) mb.Sub(2).Box(new Vector3(0, -0.002f - k * 0.004f, z * (0.026f + k * 0.012f)), new Vector3(0.022f, 0.004f, 0.014f), new Color(0.22f, 0.13f, 0.07f), 0.0015f, 1, Quaternion.Euler(z * (12f + k * 22f), 0, 0));   // strap
                    break;
                case 2: // encrypted drive: a rugged metal-cased stick - connector, status LED, key ring
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.022f, 0.01f, 0.06f), c, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0, -0.012f), new Vector3(0.0225f, 0.0105f, 0.018f), Steel, 0.002f);   // metal sleeve
                    mb.Sub(1).Box(new Vector3(0, 0, 0.036f), new Vector3(0.012f, 0.0045f, 0.014f), Steel, 0.0008f);   // connector
                    mb.Sub(3).Sphere(new Vector3(0.006f, 0.0052f, 0.015f), Vector3.one * 0.0016f, 8, new Color(0.2f, 1f, 0.4f));   // LED
                    mb.Sub(0).Box(new Vector3(-0.004f, 0.0052f, 0.006f), new Vector3(0.006f, 0.0012f, 0.006f), new Color(0.6f, 0.6f, 0.62f), 0.0005f);   // keypad
                    mb.Sub(1).Tube(new Vector3(0, -0.001f, -0.034f), new Vector3(0, 0.001f, -0.034f), 0.008f, 0.0065f, 14, Steel);   // ring
                    break;
                case 3: // intel folder: a manila folder, sheets peeking out, a TOP SECRET band and a paper clip
                    mb.Sub(2).Box(Vector3.zero, new Vector3(0.24f, 0.006f, 0.31f), c, 0.002f);
                    mb.Sub(2).Box(new Vector3(0.006f, 0.004f, 0.004f), new Vector3(0.226f, 0.004f, 0.296f), Paper, 0.001f);   // sheets
                    mb.Sub(2).Box(new Vector3(0, 0.008f, 0), new Vector3(0.24f, 0.003f, 0.31f), c * 0.95f, 0.002f, 1, Quaternion.Euler(0, 0, -3f));   // cover, slightly open
                    mb.Sub(2).Box(new Vector3(-0.08f, 0.009f, 0.158f), new Vector3(0.06f, 0.003f, 0.012f), c * 0.95f, 0.001f);   // tab
                    mb.Sub(0).Box(new Vector3(0, 0.0105f, 0.05f), new Vector3(0.14f, 0.0005f, 0.032f), Red, 0f);   // stamp band
                    mb.Sub(0).Box(new Vector3(0, 0.0106f, -0.04f), new Vector3(0.12f, 0.0005f, 0.004f), Black, 0f);
                    mb.Sub(0).Box(new Vector3(0, 0.0106f, -0.055f), new Vector3(0.09f, 0.0005f, 0.004f), Black, 0f);
                    mb.Sub(1).Box(new Vector3(0.1f, 0.011f, 0.13f), new Vector3(0.008f, 0.0015f, 0.04f), Steel, 0.0007f);   // clip
                    break;
                case 4: // graphics card: board, shroud, two fans, backplate, bracket, gold fingers
                    mb.Sub(0).Box(new Vector3(0, -0.002f, 0), new Vector3(0.27f, 0.004f, 0.11f), new Color(0.06f, 0.25f, 0.12f), 0.001f);   // PCB
                    mb.Sub(0).Box(new Vector3(0, 0.017f, 0), new Vector3(0.26f, 0.034f, 0.105f), c, 0.005f, 2);   // shroud
                    mb.Sub(1).Box(new Vector3(0, -0.006f, 0), new Vector3(0.262f, 0.004f, 0.104f), DarkGrey, 0.002f);   // backplate
                    foreach (float x in new[] { -0.065f, 0.065f })
                    {
                        mb.Sub(0).Cyl(new Vector3(x, 0.034f, 0), new Vector3(x, 0.0355f, 0), 0.043f, 0.043f, 24, Black);
                        mb.Sub(0).Cyl(new Vector3(x, 0.035f, 0), new Vector3(x, 0.039f, 0), 0.011f, 0.011f, 16, DarkGrey);   // hub
                        for (int b = 0; b < 9; b++) mb.Sub(0).Box(new Vector3(x, 0.0365f, 0), new Vector3(0.009f, 0.002f, 0.07f), new Color(0.12f, 0.12f, 0.14f), 0f, 1, Quaternion.Euler(0, b * 20f, 8f));
                    }
                    mb.Sub(1).Box(new Vector3(-0.137f, 0.014f, 0), new Vector3(0.004f, 0.05f, 0.12f), Steel, 0.001f);   // bracket
                    mb.Sub(1).Box(new Vector3(0.02f, -0.012f, -0.052f), new Vector3(0.16f, 0.008f, 0.004f), Brass, 0f);   // gold fingers
                    break;
                case 5: // cold wallet: a small hardware wallet - screen, two buttons, USB-C end
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.06f, 0.01f, 0.026f), Black, 0.004f);
                    mb.Sub(3).Box(new Vector3(-0.004f, 0.0052f, 0), new Vector3(0.03f, 0.0008f, 0.016f), new Color(0.15f, 0.3f, 0.5f), 0.001f);   // screen
                    foreach (float x in new[] { 0.017f, 0.024f }) mb.Sub(1).Cyl(new Vector3(x, 0.005f, 0), new Vector3(x, 0.0064f, 0), 0.0028f, 0.0028f, 10, c);   // buttons
                    mb.Sub(1).Box(new Vector3(0.031f, 0, 0), new Vector3(0.003f, 0.004f, 0.009f), Steel, 0.001f);   // port
                    mb.Sub(1).Box(new Vector3(-0.031f, 0, 0), new Vector3(0.004f, 0.011f, 0.027f), c, 0.002f);   // swivel cover end
                    break;
                case 6: // gold bar: a proper trapezoid ingot with a stamped panel on top
                    mb.Sub(1).Extrude(new List<Vector2> { new Vector2(-0.03f, -0.0175f), new Vector2(0.03f, -0.0175f), new Vector2(0.024f, 0.0175f), new Vector2(-0.024f, 0.0175f) }, 0.1f, c, 0.003f);
                    mb.Sub(1).Box(new Vector3(0, 0.0178f, 0), new Vector3(0.06f, 0.0008f, 0.032f), c * 0.9f, 0.001f);
                    mb.Sub(1).Box(new Vector3(0, 0.0182f, 0.006f), new Vector3(0.04f, 0.0006f, 0.004f), c * 0.75f, 0f);
                    mb.Sub(1).Box(new Vector3(0, 0.0182f, -0.004f), new Vector3(0.028f, 0.0006f, 0.003f), c * 0.75f, 0f);
                    break;
                case 7: // blue diamond: a brilliant cut - a flat table, faceted crown, deep pointed pavilion
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, -0.022f), new Vector2(0.018f, 0f), new Vector2(0.02f, 0.002f), new Vector2(0.012f, 0.009f), new Vector2(0f, 0.009f) }, 8, c);
                    break;
                case 8: // scrap metal: a bent sheet, a twisted strip, a rusty pipe offcut, a bolt
                    mb.Sub(1).Box(Vector3.zero, new Vector3(0.12f, 0.004f, 0.05f), c, 0.001f, 1, Quaternion.Euler(0, 0, 12));
                    mb.Sub(1).Box(new Vector3(0.045f, 0.012f, 0), new Vector3(0.05f, 0.004f, 0.05f), c * 0.9f, 0.001f, 1, Quaternion.Euler(0, 0, 50));
                    mb.Sub(1).Box(new Vector3(0.02f, 0.015f, 0.01f), new Vector3(0.07f, 0.004f, 0.02f), c * 0.8f, 0.001f, 1, Quaternion.Euler(20, 30, -15));
                    mb.Sub(1).Tube(new Vector3(-0.05f, 0.002f, -0.02f), new Vector3(0.02f, 0.012f, -0.035f), 0.009f, 0.007f, 10, new Color(0.45f, 0.28f, 0.15f));
                    mb.Sub(1).Cyl(new Vector3(-0.02f, 0.006f, 0.025f), new Vector3(-0.02f, 0.03f, 0.025f), 0.003f, 0.003f, 8, Steel);
                    mb.Sub(1).Cyl(new Vector3(-0.02f, 0.03f, 0.025f), new Vector3(-0.02f, 0.035f, 0.025f), 0.006f, 0.006f, 6, Steel);
                    break;
                case 9: // tin can: rolled rims, a paper label, the lid cut and bent up
                    mb.Sub(1).Cyl(new Vector3(0, -0.04f, 0), new Vector3(0, 0.038f, 0), 0.033f, 0.033f, 18, c);
                    for (int i = 0; i < 2; i++) mb.Sub(1).Tube(new Vector3(0, -0.04f + i * 0.076f, 0), new Vector3(0, -0.036f + i * 0.076f, 0), 0.0345f, 0.03f, 18, c * 1.05f);   // rims
                    for (int i = 0; i < 3; i++) mb.Sub(1).Tube(new Vector3(0, -0.022f + i * 0.02f, 0), new Vector3(0, -0.0205f + i * 0.02f, 0), 0.0333f, 0.032f, 18, c * 0.9f);   // ribs
                    mb.Sub(2).Cyl(new Vector3(0, -0.028f, 0), new Vector3(0, 0.026f, 0), 0.0336f, 0.0336f, 18, new Color(0.78f, 0.22f, 0.12f), false);   // label
                    mb.Sub(2).Cyl(new Vector3(0, -0.006f, 0), new Vector3(0, 0.006f, 0), 0.0338f, 0.0338f, 18, Paper, false);
                    mb.Sub(1).Cyl(new Vector3(0.012f, 0.048f, -0.012f), new Vector3(0.012f, 0.0485f, -0.012f), 0.031f, 0.031f, 18, c);   // lid, bent up
                    break;
                case 10: // cigarettes: a soft pack, lid flipped open, filters showing, foil
                    mb.Sub(2).Box(new Vector3(0, -0.006f, 0), new Vector3(0.055f, 0.073f, 0.022f), Color.white, 0.003f);
                    mb.Sub(2).Box(new Vector3(0, 0.014f, 0), new Vector3(0.0555f, 0.03f, 0.0225f), c, 0.003f);   // coloured band
                    mb.Sub(3).Box(new Vector3(0, -0.02f, 0.0113f), new Vector3(0.03f, 0.016f, 0.0008f), Color.Lerp(c, Color.white, 0.5f), 0f);   // logo
                    mb.Sub(1).Box(new Vector3(0, 0.031f, -0.004f), new Vector3(0.054f, 0.004f, 0.012f), new Color(0.8f, 0.8f, 0.82f), 0.001f);   // foil
                    for (int i = 0; i < 4; i++) mb.Sub(2).Cyl(new Vector3(-0.018f + i * 0.012f, 0.028f, 0.003f), new Vector3(-0.018f + i * 0.012f, 0.04f, 0.003f), 0.0038f, 0.0038f, 10, new Color(0.85f, 0.55f, 0.25f));   // filters
                    mb.Sub(2).Box(new Vector3(0, 0.045f, -0.019f), new Vector3(0.055f, 0.004f, 0.022f), c, 0.002f, 1, Quaternion.Euler(-80f, 0, 0));   // lid, flipped back
                    break;
                case 11: // zippo: brushed case, hinged lid flipped open, chimney with holes, flint wheel
                    mb.Sub(1).Box(new Vector3(0, -0.006f, 0), new Vector3(0.038f, 0.044f, 0.013f), c, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.016f, 0), new Vector3(0.039f, 0.002f, 0.0135f), c * 0.75f, 0.0005f);   // seam
                    mb.Sub(1).Box(new Vector3(0, 0.026f, 0), new Vector3(0.022f, 0.018f, 0.011f), Steel, 0.001f);   // chimney
                    for (int i = 0; i < 4; i++) mb.Sub(0).Box(new Vector3(-0.006f + i * 0.004f, 0.028f, 0.0056f), new Vector3(0.0018f, 0.0018f, 0.001f), Black, 0f);   // holes
                    mb.Sub(1).Cyl(new Vector3(0.012f, 0.032f, -0.004f), new Vector3(0.012f, 0.032f, 0.004f), 0.0045f, 0.0045f, 12, DarkGrey);   // wheel
                    mb.Sub(1).Box(new Vector3(-0.026f, 0.028f, 0), new Vector3(0.014f, 0.022f, 0.0135f), c, 0.003f, 1, Quaternion.Euler(0, 0, 70f));   // lid, open
                    break;
                case 12: // battery pack: a power bank - charge LEDs, two USB ports, a grip strip
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.1f, 0.024f, 0.06f), c, 0.008f, 2);
                    for (int i = 0; i < 4; i++) mb.Sub(3).Sphere(new Vector3(-0.02f + i * 0.007f, 0.0122f, 0.022f), Vector3.one * 0.0014f, 6, i < 3 ? new Color(0.3f, 0.7f, 1f) : DarkGrey);
                    foreach (float z in new[] { -0.012f, 0.008f }) mb.Sub(1).Box(new Vector3(0.0505f, 0, z), new Vector3(0.002f, 0.005f, 0.012f), Black, 0.0005f);
                    mb.Sub(0).Box(new Vector3(-0.015f, 0, -0.0302f), new Vector3(0.06f, 0.014f, 0.001f), Color.Lerp(c, Color.white, 0.3f), 0f);   // label
                    break;
                case 13: // whiskey bottle: square-shouldered glass, a paper label and neck band, a cork stopper
                    mb.Sub(3).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0.0f, -0.1f), new Vector2(0.034f, -0.1f), new Vector2(0.038f, -0.094f), new Vector2(0.038f, 0.022f), new Vector2(0.033f, 0.038f), new Vector2(0.016f, 0.052f), new Vector2(0.0125f, 0.062f), new Vector2(0.0125f, 0.1f), new Vector2(0.0f, 0.1f) }, 18, c);
                    mb.Sub(2).Box(new Vector3(0, -0.035f, 0.0378f), new Vector3(0.044f, 0.06f, 0.001f), Paper, 0f);   // label
                    mb.Sub(0).Box(new Vector3(0, -0.03f, 0.0384f), new Vector3(0.034f, 0.01f, 0.0006f), Black, 0f);
                    mb.Sub(0).Box(new Vector3(0, -0.05f, 0.0384f), new Vector3(0.026f, 0.004f, 0.0006f), new Color(0.75f, 0.55f, 0.2f), 0f);
                    mb.Sub(2).Cyl(new Vector3(0, 0.07f, 0), new Vector3(0, 0.082f, 0), 0.0132f, 0.0132f, 14, Black);   // neck band
                    mb.Sub(0).Cyl(new Vector3(0, 0.098f, 0), new Vector3(0, 0.106f, 0), 0.016f, 0.016f, 14, new Color(0.15f, 0.08f, 0.04f));   // cork top
                    mb.Sub(2).Cyl(new Vector3(0, 0.09f, 0), new Vector3(0, 0.099f, 0), 0.0115f, 0.0115f, 12, new Color(0.7f, 0.55f, 0.35f));   // cork
                    break;
                case 14: // smartphone: rounded slab, glossy screen with a notch, camera bump, side buttons
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.072f, 0.148f, 0.008f), c, 0.006f, 1);
                    mb.Sub(3).Box(new Vector3(0, 0, 0.0042f), new Vector3(0.066f, 0.14f, 0.0008f), new Color(0.04f, 0.07f, 0.14f), 0.004f);
                    mb.Sub(0).Box(new Vector3(0, 0.066f, 0.0048f), new Vector3(0.012f, 0.003f, 0.0005f), Black, 0.001f);   // notch
                    mb.Sub(0).Box(new Vector3(-0.02f, 0.055f, -0.005f), new Vector3(0.024f, 0.024f, 0.002f), c * 0.8f, 0.004f);   // camera block
                    foreach (var o in new[] { new Vector3(-0.026f, 0.061f, 0), new Vector3(-0.026f, 0.05f, 0), new Vector3(-0.015f, 0.056f, 0) })
                        mb.Sub(3).Cyl(o + new Vector3(0, 0, -0.0055f), o + new Vector3(0, 0, -0.0068f), 0.0038f, 0.0038f, 12, Black);
                    mb.Sub(1).Box(new Vector3(0.0365f, 0.03f, 0), new Vector3(0.001f, 0.02f, 0.003f), c * 0.7f, 0.0005f);   // buttons
                    mb.Sub(1).Box(new Vector3(-0.0365f, 0.035f, 0), new Vector3(0.001f, 0.012f, 0.003f), c * 0.7f, 0.0005f);
                    break;
                case 15: // military radio: a ribbed rugged body, speaker grille, keypad, channel knobs, a whip antenna, belt clip
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.065f, 0.16f, 0.04f), c, 0.008f, 2);
                    for (int i = 0; i < 6; i++) mb.Sub(0).Box(new Vector3(0, 0.04f + i * 0.007f, 0.0205f), new Vector3(0.045f, 0.003f, 0.002f), Black, 0.0005f);   // grille
                    for (int i = 0; i < 9; i++) mb.Sub(0).Box(new Vector3(-0.014f + (i % 3) * 0.014f, -0.01f - (i / 3) * 0.012f, 0.0205f), new Vector3(0.01f, 0.008f, 0.002f), DarkGrey, 0.001f);   // keypad
                    mb.Sub(3).Box(new Vector3(0, 0.017f, 0.0205f), new Vector3(0.04f, 0.012f, 0.001f), new Color(0.35f, 0.55f, 0.3f), 0.001f);   // screen
                    mb.Sub(1).Cyl(new Vector3(0.02f, 0.08f, 0), new Vector3(0.02f, 0.24f, 0), 0.0045f, 0.0025f, 10, Black);   // antenna
                    mb.Sub(1).Cyl(new Vector3(0.02f, 0.08f, 0), new Vector3(0.02f, 0.095f, 0), 0.007f, 0.007f, 10, DarkGrey);
                    mb.Sub(1).Cyl(new Vector3(-0.014f, 0.08f, 0), new Vector3(-0.014f, 0.094f, 0), 0.0085f, 0.0085f, 14, Black);   // knob
                    for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; mb.Sub(1).Box(new Vector3(-0.014f + Mathf.Cos(a) * 0.0088f, 0.087f, Mathf.Sin(a) * 0.0088f), new Vector3(0.0015f, 0.013f, 0.0015f), DarkGrey, 0f); }
                    mb.Sub(1).Box(new Vector3(0, 0.02f, -0.0225f), new Vector3(0.03f, 0.07f, 0.003f), Steel, 0.001f);   // belt clip
                    break;
                case 16: // silver ring: a polished band with a raised setting, four claws and a blue stone
                    mb.Sub(1).Tube(new Vector3(0, -0.0022f, 0), new Vector3(0, 0.0022f, 0), 0.0115f, 0.0095f, 28, c);
                    mb.Sub(1).Cyl(new Vector3(0, 0, 0.0105f), new Vector3(0, 0, 0.0155f), 0.0035f, 0.0045f, 10, c);   // setting
                    for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2f + Mathf.PI / 4f; mb.Sub(1).Cyl(new Vector3(Mathf.Cos(a) * 0.0035f, Mathf.Sin(a) * 0.0035f, 0.0145f), new Vector3(Mathf.Cos(a) * 0.003f, Mathf.Sin(a) * 0.003f, 0.019f), 0.0007f, 0.0005f, 6, c); }
                    mb.Sub(3).Lathe(new Vector3(0, 0, 0.0155f), Quaternion.Euler(90f, 0, 0), new List<Vector2> { new Vector2(0f, -0.001f), new Vector2(0.0042f, 0.0015f), new Vector2(0.0028f, 0.0035f), new Vector2(0f, 0.0035f) }, 8, new Color(0.35f, 0.65f, 1f));
                    break;
                case 17: // rugged laptop: a closed case with rubber corners, a carry handle, a latch and a badge
                {
                    Vector3 s = new Vector3(0.32f, 0.036f, 0.23f);
                    mb.Sub(0).Box(Vector3.zero, s, c, 0.006f, 2);
                    mb.Sub(0).Box(new Vector3(0, 0f, 0), new Vector3(s.x + 0.002f, 0.003f, s.z + 0.002f), c * 0.6f, 0.001f);   // lid seam
                    foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Sub(0).Box(new Vector3(x * (s.x / 2 - 0.01f), 0, z * (s.z / 2 - 0.01f)), new Vector3(0.028f, s.y + 0.006f, 0.028f), Black, 0.006f);   // bumpers
                    mb.Sub(0).Box(new Vector3(0, 0f, s.z / 2 + 0.012f), new Vector3(0.12f, 0.014f, 0.01f), Black, 0.004f);   // handle grip
                    foreach (float x in new[] { -0.06f, 0.06f }) mb.Sub(0).Box(new Vector3(x, 0, s.z / 2 + 0.006f), new Vector3(0.012f, 0.012f, 0.014f), Black, 0.002f);
                    mb.Sub(1).Box(new Vector3(0.1f, 0f, s.z / 2 + 0.003f), new Vector3(0.03f, 0.016f, 0.008f), Steel, 0.002f);   // latch
                    mb.Sub(1).Box(new Vector3(0, s.y / 2 + 0.0008f, 0), new Vector3(0.05f, 0.0012f, 0.03f), Steel, 0.002f);   // badge
                    break;
                }
                case 18: // gold idol: a seated figure on a stepped base - crown, arms, green gem eyes
                    mb.Sub(1).Box(new Vector3(0, -0.075f, 0), new Vector3(0.075f, 0.014f, 0.075f), c, 0.003f);
                    mb.Sub(1).Box(new Vector3(0, -0.062f, 0), new Vector3(0.06f, 0.012f, 0.06f), c, 0.003f);
                    mb.Sub(1).Lathe(new Vector3(0, -0.056f, 0), Quaternion.identity, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.034f, 0.02f), new Vector2(0.026f, 0.05f), new Vector2(0.018f, 0.075f), new Vector2(0f, 0.078f) }, 16, c);   // body
                    mb.Sub(1).Sphere(new Vector3(0, 0.04f, 0), new Vector3(0.022f, 0.024f, 0.021f), 14, c);   // head
                    mb.Sub(1).Cyl(new Vector3(0, 0.056f, 0), new Vector3(0, 0.072f, 0), 0.016f, 0.008f, 8, c);   // headdress
                    foreach (float x in new[] { -1f, 1f }) mb.Sub(1).Capsule(new Vector3(x * 0.024f, 0.005f, 0), new Vector3(x * 0.012f, -0.02f, 0.022f), 0.0065f, 8, c);   // arms
                    mb.Sub(0).Sphere(new Vector3(-0.0085f, 0.044f, 0.019f), Vector3.one * 0.0045f, 8, new Color(0.1f, 0.8f, 0.3f));
                    mb.Sub(0).Sphere(new Vector3(0.0085f, 0.044f, 0.019f), Vector3.one * 0.0045f, 8, new Color(0.1f, 0.8f, 0.3f));
                    break;
                case 19: // crown jewel: a gold band with spikes topped by pearls, gems all round, a red velvet cap
                    mb.Sub(1).Tube(new Vector3(0, -0.02f, 0), new Vector3(0, 0.016f, 0), 0.06f, 0.054f, 28, c);
                    mb.Sub(1).Tube(new Vector3(0, -0.022f, 0), new Vector3(0, -0.016f, 0), 0.0625f, 0.054f, 28, c * 0.85f);   // base rim
                    mb.Sub(2).Sphere(new Vector3(0, 0.012f, 0), new Vector3(0.054f, 0.04f, 0.054f), 16, new Color(0.55f, 0.05f, 0.08f), 0.5f, 1f);   // velvet cap
                    mb.Sub(1).Sphere(new Vector3(0, 0.056f, 0), Vector3.one * 0.008f, 10, c);   // orb on top
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var pp = new Vector3(Mathf.Cos(a) * 0.057f, 0.016f, Mathf.Sin(a) * 0.057f);
                        mb.Sub(1).Cyl(pp, pp + Vector3.up * 0.03f, 0.008f, 0.002f, 6, c);   // spikes
                        mb.Sub(3).Sphere(pp + Vector3.up * 0.032f, Vector3.one * 0.0045f, 8, Paper);   // pearls
                        mb.Sub(0).Sphere(new Vector3(Mathf.Cos(a + 0.39f) * 0.061f, 0.0f, Mathf.Sin(a + 0.39f) * 0.061f), Vector3.one * 0.0065f, 8, i % 2 == 0 ? new Color(0.9f, 0.1f, 0.2f) : new Color(0.2f, 0.5f, 1f));
                    }
                    break;
                case 20: // duct tape: the roll, its cardboard core, a loose end
                    mb.Sub(2).Tube(new Vector3(0, -0.024f, 0), new Vector3(0, 0.024f, 0), 0.05f, 0.033f, 24, c);
                    mb.Sub(2).Tube(new Vector3(0, -0.0242f, 0), new Vector3(0, 0.0242f, 0), 0.033f, 0.029f, 24, new Color(0.7f, 0.6f, 0.45f));
                    mb.Sub(2).Box(new Vector3(0.05f, -0.004f, 0.03f), new Vector3(0.003f, 0.048f, 0.05f), c, 0f, 1, Quaternion.Euler(0, -30f, 0));   // loose end
                    break;
                case 21: // box of screws: a small cardboard box, lid open, screw heads inside
                    mb.Sub(2).Box(new Vector3(0, -0.003f, 0), new Vector3(0.09f, 0.044f, 0.06f), c, 0.002f);
                    mb.Sub(2).Box(new Vector3(0, 0.02f, -0.044f), new Vector3(0.09f, 0.002f, 0.03f), c * 0.9f, 0.001f, 1, Quaternion.Euler(-70f, 0, 0));   // lid
                    mb.Sub(0).Box(new Vector3(0, 0.017f, 0), new Vector3(0.086f, 0.002f, 0.056f), new Color(0.2f, 0.18f, 0.15f), 0f);
                    for (int i = 0; i < 18; i++) mb.Sub(1).Cyl(new Vector3(-0.036f + (i % 6) * 0.0145f, 0.018f, -0.02f + (i / 6) * 0.02f), new Vector3(-0.036f + (i % 6) * 0.0145f, 0.021f, -0.02f + (i / 6) * 0.02f), 0.004f, 0.004f, 8, Steel);
                    mb.Sub(2).Box(new Vector3(0, -0.003f, 0.0302f), new Vector3(0.06f, 0.02f, 0.001f), Paper, 0f);   // label
                    break;
                case 22: // copper wire: a reel with plastic flanges, the copper wound round it, a loose end
                    foreach (float y in new[] { -0.026f, 0.022f }) mb.Sub(0).Cyl(new Vector3(0, y, 0), new Vector3(0, y + 0.004f, 0), 0.055f, 0.055f, 24, DarkGrey);
                    for (int i = 0; i < 11; i++) mb.Sub(1).Tube(new Vector3(0, -0.022f + i * 0.004f, 0), new Vector3(0, -0.0185f + i * 0.004f, 0), 0.046f, 0.02f, 24, c * (0.9f + (i % 2) * 0.1f));
                    mb.Sub(0).Tube(new Vector3(0, -0.026f, 0), new Vector3(0, 0.026f, 0), 0.02f, 0.012f, 16, DarkGrey);   // core
                    mb.Sub(1).Cyl(new Vector3(0.046f, 0.005f, 0), new Vector3(0.075f, 0.015f, 0.02f), 0.0015f, 0.0015f, 6, c);   // loose end
                    break;
                case 23: // light bulb: frosted glass, threaded base, the filament inside
                    mb.Sub(3).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, 0.06f), new Vector2(0.018f, 0.056f), new Vector2(0.028f, 0.042f), new Vector2(0.03f, 0.028f), new Vector2(0.024f, 0.006f), new Vector2(0.014f, -0.008f), new Vector2(0.013f, -0.014f) }, 20, c);
                    for (int i = 0; i < 4; i++) mb.Sub(1).Tube(new Vector3(0, -0.016f - i * 0.0055f, 0), new Vector3(0, -0.0125f - i * 0.0055f, 0), 0.0138f, 0.0118f, 16, Steel);   // threads
                    mb.Sub(1).Cyl(new Vector3(0, -0.038f, 0), new Vector3(0, -0.012f, 0), 0.0122f, 0.0122f, 16, Steel);
                    mb.Sub(0).Cyl(new Vector3(0, -0.042f, 0), new Vector3(0, -0.037f, 0), 0.005f, 0.006f, 10, Black);   // contact
                    mb.Sub(1).Box(new Vector3(0, 0.032f, 0), new Vector3(0.012f, 0.0015f, 0.0015f), new Color(0.6f, 0.4f, 0.2f), 0f);   // filament
                    break;
                case 24: // toilet paper: the roll, its cardboard core, a sheet hanging off
                    mb.Sub(2).Tube(new Vector3(0, -0.05f, 0), new Vector3(0, 0.05f, 0), 0.055f, 0.022f, 24, c);
                    mb.Sub(2).Tube(new Vector3(0, -0.0502f, 0), new Vector3(0, 0.0502f, 0), 0.022f, 0.019f, 18, new Color(0.65f, 0.52f, 0.36f));
                    mb.Sub(2).Box(new Vector3(0.055f, -0.02f, 0.012f), new Vector3(0.002f, 0.098f, 0.1f), c * 0.97f, 0f, 1, Quaternion.Euler(0, -15f, 0));   // sheet
                    break;
                case 25: // matches: a box with the drawer pulled out, striker strips, red-tipped matches
                    mb.Sub(2).Box(new Vector3(-0.008f, 0, 0), new Vector3(0.05f, 0.015f, 0.035f), c, 0.002f);   // sleeve
                    foreach (float z in new[] { -1f, 1f }) mb.Sub(2).Box(new Vector3(-0.008f, 0, z * 0.0178f), new Vector3(0.044f, 0.011f, 0.001f), new Color(0.35f, 0.2f, 0.12f), 0f);   // strikers
                    mb.Sub(2).Box(new Vector3(0.016f, -0.0012f, 0), new Vector3(0.048f, 0.012f, 0.032f), Paper, 0.001f);   // drawer
                    for (int i = 0; i < 7; i++)
                    {
                        float z = -0.012f + i * 0.004f;
                        mb.Sub(2).Box(new Vector3(0.02f, 0.006f, z), new Vector3(0.04f, 0.0025f, 0.0025f), new Color(0.9f, 0.78f, 0.55f), 0f);
                        mb.Sub(0).Sphere(new Vector3(0.041f, 0.006f, z), new Vector3(0.0035f, 0.0028f, 0.0028f), 6, Red);
                    }
                    break;
                case 26: // bolts: hex bolts with threads, nuts and washers
                    for (int i = 0; i < 3; i++)
                    {
                        var o = new Vector3(-0.025f + i * 0.025f, 0, (i % 2) * 0.012f);
                        mb.Sub(1).Cyl(o + new Vector3(0, -0.03f, 0), o + new Vector3(0, 0.02f, 0), 0.006f, 0.006f, 10, c);
                        for (int t = 0; t < 7; t++) mb.Sub(1).Tube(o + new Vector3(0, -0.028f + t * 0.005f, 0), o + new Vector3(0, -0.0265f + t * 0.005f, 0), 0.0065f, 0.0055f, 10, c * 0.85f);   // threads
                        mb.Sub(1).Cyl(o + new Vector3(0, 0.02f, 0), o + new Vector3(0, 0.029f, 0), 0.011f, 0.011f, 6, c);   // hex head
                        mb.Sub(1).Tube(o + new Vector3(0, 0.0185f, 0), o + new Vector3(0, 0.02f, 0), 0.012f, 0.0065f, 14, c * 0.9f);   // washer
                    }
                    mb.Sub(1).Tube(new Vector3(0.035f, -0.03f, -0.015f), new Vector3(0.035f, -0.022f, -0.015f), 0.01f, 0.006f, 6, c);   // loose nut
                    break;
                case 27: // car battery: ribbed case, lid with vent caps, terminals in red and black, a carry strap, a label
                {
                    Vector3 s = new Vector3(0.24f, 0.17f, 0.17f);
                    mb.Sub(0).Box(Vector3.zero, s, c, 0.006f, 2);
                    mb.Sub(0).Box(new Vector3(0, s.y / 2 - 0.008f, 0), new Vector3(s.x + 0.004f, 0.016f, s.z + 0.004f), c * 0.8f, 0.004f);   // lid
                    for (int i = 0; i < 6; i++) mb.Sub(0).Cyl(new Vector3(-0.075f + i * 0.03f, s.y / 2, -0.03f), new Vector3(-0.075f + i * 0.03f, s.y / 2 + 0.005f, -0.03f), 0.009f, 0.009f, 10, c * 0.7f);   // vent caps
                    mb.Sub(1).Cyl(new Vector3(-0.085f, s.y / 2, 0.05f), new Vector3(-0.085f, s.y / 2 + 0.022f, 0.05f), 0.011f, 0.009f, 12, new Color(0.7f, 0.68f, 0.62f));
                    mb.Sub(1).Cyl(new Vector3(0.085f, s.y / 2, 0.05f), new Vector3(0.085f, s.y / 2 + 0.022f, 0.05f), 0.011f, 0.009f, 12, new Color(0.7f, 0.68f, 0.62f));
                    mb.Sub(0).Cyl(new Vector3(-0.085f, s.y / 2 + 0.004f, 0.05f), new Vector3(-0.085f, s.y / 2 + 0.012f, 0.05f), 0.016f, 0.016f, 12, Red);   // + cap
                    mb.Sub(0).Cyl(new Vector3(0.085f, s.y / 2 + 0.004f, 0.05f), new Vector3(0.085f, s.y / 2 + 0.012f, 0.05f), 0.016f, 0.016f, 12, Black);
                    mb.Sub(2).Box(new Vector3(0, s.y / 2 + 0.035f, 0), new Vector3(0.14f, 0.004f, 0.02f), Black, 0.001f);   // strap
                    foreach (float x in new[] { -0.07f, 0.07f }) mb.Sub(2).Box(new Vector3(x, s.y / 2 + 0.017f, 0), new Vector3(0.004f, 0.036f, 0.02f), Black, 0.001f);
                    mb.Sub(3).Box(new Vector3(0, 0.02f, -s.z / 2 - 0.0012f), new Vector3(0.16f, 0.07f, 0.002f), new Color(0.9f, 0.7f, 0.1f), 0.001f);   // label
                    mb.Sub(0).Box(new Vector3(0, 0.03f, -s.z / 2 - 0.0026f), new Vector3(0.1f, 0.018f, 0.001f), Black, 0f);
                    break;
                }
                case 28: // circuit board: green board with gold traces, chips, capacitors, a header and mounting holes
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.14f, 0.003f, 0.1f), c, 0.001f);
                    for (int i = 0; i < 9; i++) mb.Sub(1).Box(new Vector3(-0.06f + i * 0.015f, 0.0017f, 0), new Vector3(0.0012f, 0.0004f, 0.085f), Brass, 0f);   // traces
                    for (int i = 0; i < 4; i++) mb.Sub(1).Box(new Vector3(0, 0.0017f, -0.035f + i * 0.023f), new Vector3(0.12f, 0.0004f, 0.0012f), Brass, 0f);
                    mb.Sub(0).Box(new Vector3(-0.02f, 0.004f, 0.01f), new Vector3(0.032f, 0.004f, 0.032f), Black, 0.001f);   // main chip
                    for (int i = 0; i < 3; i++) mb.Sub(0).Box(new Vector3(0.035f, 0.003f, -0.03f + i * 0.022f), new Vector3(0.018f, 0.003f, 0.01f), Black, 0.0008f);
                    for (int i = 0; i < 5; i++) mb.Sub(1).Cyl(new Vector3(-0.055f + i * 0.01f, 0.0015f, -0.035f), new Vector3(-0.055f + i * 0.01f, 0.014f, -0.035f), 0.0035f, 0.0035f, 10, i % 2 == 0 ? new Color(0.15f, 0.25f, 0.6f) : DarkGrey);   // capacitors
                    for (int i = 0; i < 10; i++) mb.Sub(1).Box(new Vector3(-0.04f + i * 0.005f, 0.005f, 0.042f), new Vector3(0.0015f, 0.008f, 0.0015f), Brass, 0f);   // header pins
                    foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) mb.Sub(1).Tube(new Vector3(x * 0.064f, -0.0016f, z * 0.044f), new Vector3(x * 0.064f, 0.0017f, z * 0.044f), 0.0035f, 0.0018f, 10, Brass);
                    break;
                case 29: // instant coffee: a glass jar, coffee inside, a coloured lid and a paper label
                    mb.Sub(3).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, -0.055f), new Vector2(0.038f, -0.055f), new Vector2(0.041f, -0.05f), new Vector2(0.041f, 0.028f), new Vector2(0.036f, 0.038f), new Vector2(0.035f, 0.041f) }, 20, new Color(0.75f, 0.78f, 0.8f));
                    mb.Sub(2).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, 0.02f, 0), 0.0385f, 0.0385f, 18, new Color(0.3f, 0.17f, 0.08f));   // coffee
                    mb.Sub(2).Cyl(new Vector3(0, -0.032f, 0), new Vector3(0, 0.012f, 0), 0.0415f, 0.0415f, 20, new Color(0.85f, 0.2f, 0.15f), false);   // label
                    mb.Sub(2).Cyl(new Vector3(0, -0.016f, 0), new Vector3(0, -0.004f, 0), 0.0418f, 0.0418f, 20, Paper, false);
                    mb.Sub(0).Cyl(new Vector3(0, 0.04f, 0), new Vector3(0, 0.058f, 0), 0.04f, 0.04f, 22, c);   // lid
                    for (int i = 0; i < 24; i++) { float a = i * Mathf.PI / 12f; mb.Sub(0).Box(new Vector3(Mathf.Cos(a) * 0.0402f, 0.049f, Mathf.Sin(a) * 0.0402f), new Vector3(0.0018f, 0.016f, 0.0018f), c * 0.85f, 0f); }
                    break;
                case 30: // dog tags: two notched tags with rolled rims and stamped lines on a ball chain
                    for (int t = 0; t < 2; t++)
                    {
                        var o = new Vector3(t * 0.006f, -t * 0.006f, t * 0.003f); var q = Quaternion.Euler(0, 0, t * 12f);
                        mb.Push(o, q);
                        mb.Sub(1).Box(Vector3.zero, new Vector3(0.028f, 0.05f, 0.0012f), c * (t == 0 ? 1f : 0.92f), 0.006f);
                        mb.Sub(1).Box(new Vector3(0, 0, 0.0009f), new Vector3(0.024f, 0.046f, 0.0006f), c * 0.85f, 0.005f);   // rolled rim
                        for (int k = 0; k < 4; k++) mb.Sub(1).Box(new Vector3(0, 0.01f - k * 0.008f, 0.0014f), new Vector3(0.016f - (k % 2) * 0.004f, 0.0016f, 0.0004f), c * 0.6f, 0f);   // stamped text
                        mb.Sub(0).Cyl(new Vector3(0, 0.02f, -0.001f), new Vector3(0, 0.02f, 0.0015f), 0.0016f, 0.0016f, 8, Black);   // hole
                        mb.Pop();
                    }
                    for (int i = 0; i < 18; i++) mb.Sub(1).Sphere(new Vector3(Mathf.Sin(i * 0.36f) * 0.024f, 0.022f + i * 0.0045f, Mathf.Cos(i * 0.36f) * 0.004f), Vector3.one * 0.0016f, 6, c);   // ball chain
                    break;
                case 31: // rusty wrench: a combination spanner - open jaw, ring end, a tapered handle
                    mb.Sub(1).Extrude(new List<Vector2> { new Vector2(-0.065f, -0.007f), new Vector2(0.065f, -0.009f), new Vector2(0.065f, 0.009f), new Vector2(-0.065f, 0.007f) }, 0.005f, c, 0.0015f, new Vector3(0, 0, 0));
                    mb.Sub(1).Tube(new Vector3(-0.0025f, 0, 0.078f), new Vector3(0.0025f, 0, 0.078f), 0.016f, 0.009f, 12, c);   // ring end
                    mb.Sub(1).Extrude(new List<Vector2> { new Vector2(-0.083f, -0.018f), new Vector2(-0.068f, -0.012f), new Vector2(-0.068f, 0.012f), new Vector2(-0.083f, 0.018f), new Vector2(-0.1f, 0.016f), new Vector2(-0.1f, 0.007f), new Vector2(-0.082f, 0.006f), new Vector2(-0.082f, -0.006f), new Vector2(-0.1f, -0.007f), new Vector2(-0.1f, -0.016f) }, 0.006f, c, 0.001f);   // open jaw
                    break;
                case 32: // PEANUT8 streamer figurine: a glossy vinyl cartoon peanut in a gaming headset with a boom mic, waving, on the LIVE display base
                {
                    Color shell = c, net = c * 0.8f; net.a = 1f;
                    DisplayBase(mb, -0.055f, 0.04f);
                    mb.Push(new Vector3(0, -0.055f, 0), Quaternion.Euler(0, 0, -4f));   // the peanut stands on its sneakers, leaning a touch
                    // one smooth two-lobed shell (lathed), the lower lobe a bit fatter, a soft pinched waist
                    const float yl = 0.041f, yu = 0.093f;
                    float Rad(float y)
                    {
                        float a = 0.031f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((y - yl) / 0.031f, 2f)));
                        float b = 0.028f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((y - yu) / 0.03f, 2f)));
                        float k = 0.005f; return (a + b + Mathf.Sqrt((a - b) * (a - b) + k * k)) * 0.5f - k * 0.5f + 0.0001f;
                    }
                    var prof = new List<Vector2>(); const int N = 40; float yBot = yl - 0.031f, yTop = yu + 0.03f;
                    prof.Add(new Vector2(0f, yBot));
                    for (int i = 1; i < N; i++) { float t = (1f - Mathf.Cos(Mathf.PI * i / N)) * 0.5f, y = Mathf.Lerp(yBot, yTop, t); prof.Add(new Vector2(Rad(y), y)); }
                    prof.Add(new Vector2(0f, yTop));
                    mb.Sub(3).Lathe(Vector3.zero, Quaternion.identity, prof, 32, shell);
                    // the shell's net: ridges down the sides and wavy rings round it (kept off the face)
                    Vector3 On(float a, float y, float lift) { float r = Rad(y) + lift; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
                    bool Face(float a, float y) => y > yu - 0.022f && y < yu + 0.024f && Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90f)) < 62f;
                    for (int m = 0; m < 12; m++)
                    {
                        float a = m * Mathf.PI * 2f / 12f + 0.13f;
                        for (int j = 0; j < 22; j++)
                        {
                            float y0 = Mathf.Lerp(yBot + 0.008f, yTop - 0.008f, j / 22f), y1 = Mathf.Lerp(yBot + 0.008f, yTop - 0.008f, (j + 1) / 22f);
                            float w0 = a + Mathf.Sin(y0 * 260f + m) * 0.06f, w1 = a + Mathf.Sin(y1 * 260f + m) * 0.06f;
                            if (Face(w0, y0) || Face(w1, y1)) continue;
                            mb.Sub(0).Capsule(On(w0, y0, 0f), On(w1, y1, 0f), 0.0008f, 4, net);
                        }
                    }
                    foreach (float y in new[] { yl - 0.019f, yl - 0.006f, yl + 0.008f, yu - 0.016f, yu + 0.02f })
                        for (int k = 0; k < 24; k++)
                        {
                            float a0 = k * Mathf.PI * 2f / 24f, a1 = (k + 1) * Mathf.PI * 2f / 24f;
                            float ya = y + Mathf.Sin(k * 1.7f) * 0.0025f, yb = y + Mathf.Sin((k + 1) * 1.7f) * 0.0025f;
                            if (Face(a0, ya) || Face(a1, yb)) continue;
                            mb.Sub(0).Capsule(On(a0, ya, 0f), On(a1, yb, 0f), 0.0007f, 4, net);
                        }
                    // the face (+Z on the top lobe): big glossy eyes with brown irises, eyebrows, an open grin with teeth, blush
                    float fy = yu + 0.004f;
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(3).Sphere(new Vector3(x * 0.0095f, fy + 0.004f, 0.0205f), new Vector3(0.0078f, 0.0094f, 0.0062f), 16, Color.white);
                        mb.Sub(3).Sphere(new Vector3(x * 0.0088f, fy + 0.0035f, 0.0262f), new Vector3(0.0047f, 0.0055f, 0.0016f), 14, new Color(0.38f, 0.22f, 0.08f));
                        mb.Sub(3).Sphere(new Vector3(x * 0.0086f, fy + 0.0035f, 0.0274f), new Vector3(0.0027f, 0.0032f, 0.0012f), 12, Black);
                        mb.Sub(3).Sphere(new Vector3(x * 0.0072f, fy + 0.0058f, 0.0284f), Vector3.one * 0.0012f, 8, Color.white);   // catch-lights
                        mb.Sub(3).Sphere(new Vector3(x * 0.0098f, fy + 0.0012f, 0.0281f), Vector3.one * 0.0006f, 6, Color.white);
                        mb.Sub(0).Capsule(new Vector3(x * 0.004f, fy + 0.0145f, 0.0238f), new Vector3(x * 0.0145f, fy + 0.0165f, 0.0205f), 0.0011f, 6, new Color(0.3f, 0.17f, 0.07f));   // eyebrows
                        mb.Sub(0).Sphere(new Vector3(x * 0.0175f, fy - 0.0075f, 0.0198f), new Vector3(0.0042f, 0.0026f, 0.0018f), 10, new Color(0.98f, 0.55f, 0.5f));   // cheeks
                    }
                    mb.Sub(3).Sphere(new Vector3(0, fy - 0.0085f, 0.0242f), new Vector3(0.0105f, 0.0078f, 0.0032f), 18, new Color(0.25f, 0.04f, 0.06f), 0.5f, 1f);   // the open grin
                    mb.Sub(3).Box(new Vector3(0, fy - 0.0094f, 0.0262f), new Vector3(0.0135f, 0.0022f, 0.001f), Color.white, 0.0006f);   // top teeth
                    mb.Sub(3).Sphere(new Vector3(0, fy - 0.0142f, 0.0258f), new Vector3(0.0052f, 0.002f, 0.0014f), 10, new Color(0.95f, 0.42f, 0.48f));   // tongue
                    // gaming headset: padded band over the top, chunky ear cups with glowing purple rings, a boom mic with a foam tip
                    Color hs = new Color(0.12f, 0.12f, 0.14f), glow = new Color(0.66f, 0.35f, 1f);
                    for (int i = 0; i < 12; i++)
                    {
                        float a0 = Mathf.PI * i / 12f, a1 = Mathf.PI * (i + 1) / 12f;
                        Vector3 P(float a, float rr) => new Vector3(Mathf.Cos(a) * rr, yu + Mathf.Sin(a) * (rr + 0.002f), -0.002f);
                        mb.Sub(3).Capsule(P(a0, 0.0325f), P(a1, 0.0325f), 0.0034f, 8, hs);
                        if (i >= 2 && i < 10) mb.Sub(2).Capsule(P(a0, 0.0295f), P(a1, 0.0295f), 0.0026f, 6, new Color(0.3f, 0.3f, 0.34f));   // cushion
                    }
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(2).Cyl(new Vector3(x * 0.024f, yu, 0), new Vector3(x * 0.029f, yu, 0), 0.0125f, 0.0125f, 20, new Color(0.25f, 0.25f, 0.28f));   // cushion
                        mb.Sub(3).Cyl(new Vector3(x * 0.029f, yu, 0), new Vector3(x * 0.038f, yu, 0), 0.013f, 0.0115f, 24, hs);
                        mb.Sub(3).Tube(new Vector3(x * 0.0381f, yu, 0), new Vector3(x * 0.0389f, yu, 0), 0.0108f, 0.0075f, 24, glow);
                        mb.Sub(3).Cyl(new Vector3(x * 0.0381f, yu, 0), new Vector3(x * 0.0385f, yu, 0), 0.0074f, 0.0074f, 20, new Color(0.2f, 0.2f, 0.23f));
                        mb.Sub(3).Sphere(new Vector3(x * 0.0386f, yu, 0), new Vector3(0.0006f, 0.0032f, 0.0032f), 10, glow);   // logo dot
                    }
                    mb.Sub(1).Capsule(new Vector3(-0.036f, yu - 0.006f, 0.006f), new Vector3(-0.031f, yu - 0.014f, 0.018f), 0.0014f, 6, Steel);   // boom
                    mb.Sub(1).Capsule(new Vector3(-0.031f, yu - 0.014f, 0.018f), new Vector3(-0.017f, fy - 0.009f, 0.027f), 0.0014f, 6, Steel);
                    mb.Sub(2).Sphere(new Vector3(-0.0145f, fy - 0.0092f, 0.0282f), new Vector3(0.0042f, 0.0034f, 0.0034f), 12, Black);   // foam tip
                    // rubber-hose arms in white cartoon gloves: one waving high, one on the hip
                    Color arm = new Color(0.08f, 0.08f, 0.09f);
                    mb.Sub(0).Capsule(new Vector3(0.025f, yl + 0.016f, 0.006f), new Vector3(0.041f, yl + 0.03f, 0.01f), 0.0024f, 8, arm);
                    mb.Sub(0).Capsule(new Vector3(0.041f, yl + 0.03f, 0.01f), new Vector3(0.048f, yl + 0.049f, 0.012f), 0.0024f, 8, arm);
                    Glove(mb, new Vector3(0.05f, yl + 0.058f, 0.013f), Quaternion.Euler(0, 0, -12f), 0.0058f, true);
                    mb.Sub(0).Capsule(new Vector3(-0.025f, yl + 0.016f, 0.006f), new Vector3(-0.042f, yl + 0.002f, 0.016f), 0.0024f, 8, arm);
                    mb.Sub(0).Capsule(new Vector3(-0.042f, yl + 0.002f, 0.016f), new Vector3(-0.024f, yl - 0.008f, 0.036f), 0.0024f, 8, arm);
                    Glove(mb, new Vector3(-0.021f, yl - 0.009f, 0.039f), Quaternion.Euler(-70f, 0, -60f), 0.0048f, false);
                    {   // a game controller held out in front, thumbs on the sticks
                        var cc = new Vector3(-0.006f, yl - 0.004f, 0.04f); var cq = Quaternion.Euler(-60f, 0, 0);
                        mb.Push(cc, cq, Vector3.one * 0.62f);   // pocket-sized next to him
                        Color pad = new Color(0.04f, 0.04f, 0.05f);
                        mb.Sub(3).Box(Vector3.zero, new Vector3(0.034f, 0.016f, 0.008f), pad, 0.004f, 2);
                        foreach (float x in new[] { -1f, 1f }) mb.Sub(3).Capsule(new Vector3(x * 0.012f, -0.004f, 0), new Vector3(x * 0.016f, -0.014f, 0.001f), 0.0055f, 8, pad);   // grips
                        foreach (float x in new[] { -1f, 1f }) { mb.Sub(0).Cyl(new Vector3(x * 0.007f, 0.001f, -0.004f), new Vector3(x * 0.007f, 0.001f, -0.007f), 0.0028f, 0.0026f, 10, new Color(0.2f, 0.2f, 0.22f)); }   // thumbsticks
                        Color[] abxy = { new Color(0.2f, 0.8f, 0.3f), new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.5f, 0.95f), new Color(0.95f, 0.8f, 0.15f) };
                        for (int k = 0; k < 4; k++) { float a = k * Mathf.PI / 2f; mb.Sub(3).Sphere(new Vector3(0.012f + Mathf.Cos(a) * 0.0028f, 0.005f + Mathf.Sin(a) * 0.0028f, -0.0045f), Vector3.one * 0.0012f, 6, abxy[k]); }
                        mb.Sub(4).Box(new Vector3(0, 0.0065f, -0.0042f), new Vector3(0.008f, 0.0015f, 0.0008f), Color.white, 0f);   // light bar
                        mb.Pop();
                    }
                    // little legs and chunky sneakers (white soles, purple and red uppers)
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Capsule(new Vector3(x * 0.011f, yBot + 0.004f, 0.002f), new Vector3(x * 0.012f, 0.004f, 0.004f), 0.0024f, 8, arm);
                        mb.Sub(0).Box(new Vector3(x * 0.012f, 0.0015f, 0.007f), new Vector3(0.0115f, 0.003f, 0.019f), Color.white, 0.0012f, 2);
                        mb.Sub(3).Sphere(new Vector3(x * 0.012f, 0.0045f, 0.006f), new Vector3(0.0058f, 0.0042f, 0.0088f), 14, x < 0 ? new Color(0.85f, 0.12f, 0.12f) : new Color(0.5f, 0.22f, 0.85f));
                        mb.Sub(0).Sphere(new Vector3(x * 0.012f, 0.0032f, 0.0135f), new Vector3(0.0052f, 0.0028f, 0.0035f), 10, Color.white);   // toe cap
                        for (int k = 0; k < 2; k++) mb.Sub(2).Box(new Vector3(x * 0.012f, 0.0078f - k * 0.0003f, 0.004f + k * 0.0035f), new Vector3(0.006f, 0.0008f, 0.0012f), Color.white, 0.0003f);   // laces
                    }
                    mb.Pop();
                    break;
                }
                case 33: // SHUSHED streamer figurine: a chunky vinyl-toy operator in Juggernaut armour, red helmet, one finger to the mask - shhh
                {
                    DisplayBase(mb, -0.083f, 0.046f);
                    Color suit = new Color(0.09f, 0.09f, 0.1f), plate = new Color(0.085f, 0.09f, 0.1f), plate2 = new Color(0.13f, 0.135f, 0.15f), gold = new Color(1f, 0.78f, 0.3f);
                    Color helmet = new Color(0.68f, 0.05f, 0.04f), glove = new Color(0.72f, 0.08f, 0.05f), webbing = new Color(0.3f, 0.3f, 0.28f);
                    // built in "toy units" (the figure is 1.7 units tall) and scaled to 17 cm on the base
                    mb.Push(new Vector3(0, -0.083f, 0), Quaternion.identity, Vector3.one * 0.1f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Box(new Vector3(s * 0.13f, 0.03f, 0.04f), new Vector3(0.22f, 0.06f, 0.36f), DarkGrey, 0.025f, 2);   // sole
                        mb.Sub(0).Box(new Vector3(s * 0.13f, 0.1f, 0.03f), new Vector3(0.2f, 0.14f, 0.32f), Black, 0.06f, 2);       // boot
                        mb.Sub(2).Capsule(new Vector3(s * 0.13f, 0.16f, 0), new Vector3(s * 0.135f, 0.44f, 0), 0.1f, 0.11f, 14, suit);   // leg
                        mb.Sub(0).Box(new Vector3(s * 0.13f, 0.25f, 0.085f), new Vector3(0.15f, 0.17f, 0.06f), plate, 0.03f, 2);          // shin guard
                        mb.Sub(3).Sphere(new Vector3(s * 0.132f, 0.36f, 0.09f), new Vector3(0.085f, 0.075f, 0.055f), 14, plate2);       // knee cap
                        mb.Sub(1).Box(new Vector3(s * 0.13f, 0.17f, 0.118f), new Vector3(0.12f, 0.016f, 0.012f), gold, 0.004f);         // gold trim
                    }
                    mb.Sub(2).Box(new Vector3(0, 0.49f, 0), new Vector3(0.44f, 0.16f, 0.3f), suit, 0.07f, 2);   // hips
                    mb.Sub(0).Box(new Vector3(0, 0.53f, 0), new Vector3(0.47f, 0.065f, 0.33f), webbing, 0.025f, 2);   // belt
                    mb.Sub(1).Box(new Vector3(0, 0.53f, 0.168f), new Vector3(0.09f, 0.055f, 0.014f), gold, 0.01f);    // buckle
                    foreach (float s in new[] { -1f, 1f }) mb.Sub(0).Box(new Vector3(s * 0.2f, 0.47f, 0.11f), new Vector3(0.1f, 0.13f, 0.08f), webbing * 0.85f, 0.025f, 2);   // pouches
                    mb.Sub(0).Box(new Vector3(0, 0.42f, 0.14f), new Vector3(0.2f, 0.15f, 0.05f), plate, 0.03f, 2);   // groin flap
                    // torso and the big Juggernaut vest: thick rounded plates, ab plates, gold trim and rivets
                    mb.Sub(2).Sphere(new Vector3(0, 0.74f, 0), new Vector3(0.25f, 0.25f, 0.19f), 18, suit);
                    mb.Sub(3).Box(new Vector3(0, 0.76f, 0.07f), new Vector3(0.48f, 0.36f, 0.24f), plate, 0.09f, 3);
                    mb.Sub(3).Box(new Vector3(0, 0.82f, 0.17f), new Vector3(0.34f, 0.15f, 0.06f), plate2, 0.04f, 3);
                    for (int i = 0; i < 2; i++) foreach (float s in new[] { -1f, 1f }) mb.Sub(3).Box(new Vector3(s * 0.07f, 0.64f - i * 0.06f, 0.16f), new Vector3(0.12f, 0.05f, 0.05f), plate2, 0.018f, 2);
                    mb.Sub(1).Box(new Vector3(0, 0.905f, 0.17f), new Vector3(0.36f, 0.018f, 0.03f), gold, 0.006f);
                    mb.Sub(1).Box(new Vector3(0, 0.745f, 0.2f), new Vector3(0.3f, 0.014f, 0.02f), gold, 0.005f);
                    for (int i = 0; i < 5; i++) mb.Sub(1).Sphere(new Vector3(-0.12f + i * 0.06f, 0.88f, 0.198f), Vector3.one * 0.013f, 8, gold);
                    mb.Sub(3).Box(new Vector3(0, 0.76f, -0.12f), new Vector3(0.46f, 0.36f, 0.08f), plate, 0.04f, 2);   // back plate
                    mb.Sub(3).Tube(new Vector3(0, 0.92f, 0), new Vector3(0, 0.99f, 0), 0.17f, 0.08f, 24, plate2);       // gorget
                    mb.Sub(1).Tube(new Vector3(0, 0.985f, 0), new Vector3(0, 1.0f, 0), 0.172f, 0.12f, 24, gold);
                    // shoulder pauldrons: two stacked domes with gold rims
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Sub(3).Sphere(new Vector3(s * 0.3f, 0.86f, 0), new Vector3(0.15f, 0.12f, 0.16f), 18, plate, 0f, 0.56f);
                        mb.Sub(3).Sphere(new Vector3(s * 0.33f, 0.8f, 0), new Vector3(0.14f, 0.09f, 0.15f), 16, plate2, 0f, 0.56f);
                        Torus(mb, new Vector3(s * 0.33f, 0.795f, 0), Quaternion.Euler(90f, 0, s * -12f), 0.138f, 0.011f, 24, 6, gold);
                    }
                    // left arm hanging, an armoured forearm, red glove in a fist
                    mb.Sub(2).Capsule(new Vector3(-0.33f, 0.8f, 0), new Vector3(-0.36f, 0.6f, 0.02f), 0.085f, 0.08f, 12, suit);
                    mb.Sub(2).Capsule(new Vector3(-0.36f, 0.6f, 0.02f), new Vector3(-0.35f, 0.44f, 0.05f), 0.078f, 0.07f, 12, suit);
                    mb.Sub(3).Cyl(new Vector3(-0.36f, 0.58f, 0.025f), new Vector3(-0.352f, 0.46f, 0.048f), 0.088f, 0.08f, 16, plate);   // bracer
                    mb.Sub(1).Tube(new Vector3(-0.36f, 0.57f, 0.025f), new Vector3(-0.359f, 0.555f, 0.03f), 0.092f, 0.08f, 16, gold);
                    mb.Sub(3).Box(new Vector3(-0.35f, 0.4f, 0.08f), new Vector3(0.14f, 0.14f, 0.13f), glove, 0.055f, 3);
                    {   // his mini machine gun, held at the hip: receiver, ribbed barrel shroud, box mag, carry handle, stock, gold trim
                        Color gun = new Color(0.08f, 0.085f, 0.09f), gun2 = new Color(0.16f, 0.165f, 0.17f);
                        mb.Sub(3).Box(new Vector3(-0.35f, 0.47f, 0.16f), new Vector3(0.11f, 0.15f, 0.36f), gun, 0.03f, 2);          // receiver
                        mb.Sub(3).Cyl(new Vector3(-0.35f, 0.49f, 0.33f), new Vector3(-0.35f, 0.49f, 0.6f), 0.05f, 0.05f, 14, gun2);  // barrel shroud
                        for (int k = 0; k < 4; k++) mb.Sub(1).Tube(new Vector3(-0.35f, 0.49f, 0.36f + k * 0.06f), new Vector3(-0.35f, 0.49f, 0.375f + k * 0.06f), 0.054f, 0.048f, 14, gold);
                        mb.Sub(3).Cyl(new Vector3(-0.35f, 0.49f, 0.6f), new Vector3(-0.35f, 0.49f, 0.68f), 0.028f, 0.032f, 12, gun);   // muzzle
                        mb.Sub(3).Box(new Vector3(-0.35f, 0.34f, 0.2f), new Vector3(0.1f, 0.14f, 0.16f), gun2, 0.025f, 2);           // box mag
                        mb.Sub(3).Box(new Vector3(-0.35f, 0.57f, 0.16f), new Vector3(0.04f, 0.03f, 0.2f), gun, 0.01f, 1);            // carry handle
                        mb.Sub(3).Box(new Vector3(-0.35f, 0.45f, -0.06f), new Vector3(0.08f, 0.12f, 0.12f), gun, 0.03f, 2);          // stock
                        mb.Sub(1).Box(new Vector3(-0.29f, 0.49f, 0.16f), new Vector3(0.004f, 0.02f, 0.26f), gold, 0.002f);           // gold stripe
                    }
                    // right arm bent up, the glove at the mask with the index finger raised across the mouth: shhh
                    Vector3 sh = new Vector3(0.33f, 0.8f, 0), el = new Vector3(0.34f, 0.66f, 0.22f), wr = new Vector3(0.13f, 1.0f, 0.33f);
                    mb.Sub(2).Capsule(sh, el, 0.085f, 0.078f, 12, suit);
                    mb.Sub(2).Capsule(el, wr, 0.075f, 0.068f, 12, suit);
                    mb.Sub(3).Cyl(Vector3.Lerp(el, wr, 0.15f), Vector3.Lerp(el, wr, 0.75f), 0.086f, 0.078f, 16, plate);
                    mb.Sub(1).Tube(Vector3.Lerp(el, wr, 0.2f), Vector3.Lerp(el, wr, 0.25f), 0.09f, 0.078f, 16, gold);
                    var fist = new Vector3(0.07f, 1.06f, 0.36f);
                    mb.Sub(3).Box(fist, new Vector3(0.13f, 0.12f, 0.11f), glove, 0.05f, 3, Quaternion.Euler(0, -25f, 20f));
                    mb.Sub(3).Capsule(fist + new Vector3(-0.03f, 0.05f, 0.02f), new Vector3(0.005f, 1.25f, 0.39f), 0.026f, 0.022f, 10, glove);   // the raised finger
                    // the big head: red gloss helmet, black visor with glowing eye slits, a matte black mask over the mouth, gold crest and rim
                    var hc = new Vector3(0, 1.3f, 0.0f);
                    mb.Sub(3).Sphere(hc, new Vector3(0.355f, 0.34f, 0.36f), 28, helmet);
                    SpherePatch(mb, 3, hc, 0.366f, -62f, 62f, -4f, 16f, 20, 6, new Color(0.03f, 0.03f, 0.04f));   // visor
                    SpherePatch(mb, 0, hc, 0.362f, -48f, 48f, -44f, -6f, 18, 8, new Color(0.08f, 0.08f, 0.09f));   // mask
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float th = s * 20f * Mathf.Deg2Rad, elv = 6f * Mathf.Deg2Rad; var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(elv), Mathf.Sin(elv), Mathf.Cos(th) * Mathf.Cos(elv));
                        mb.Sub(3).Box(hc + n * 0.369f, new Vector3(0.1f, 0.026f, 0.01f), new Color(0.85f, 0.95f, 1f), 0.008f, 1, Quaternion.LookRotation(n) * Quaternion.Euler(0, 0, s * -8f));   // eye glints
                        mb.Sub(1).Cyl(hc + new Vector3(s * 0.33f, -0.02f, 0), hc + new Vector3(s * 0.375f, -0.02f, 0), 0.09f, 0.08f, 18, gold);   // ear discs
                        for (int k = 0; k < 3; k++) mb.Sub(0).Sphere(new Vector3(s * (0.06f + k * 0.035f), 1.08f, 0.0f) + new Vector3(0, 0, 0.3f - k * 0.02f), new Vector3(0.012f, 0.008f, 0.008f), 6, new Color(0.25f, 0.25f, 0.27f));   // mask vents
                    }
                    for (int i = 0; i < 12; i++)   // gold crest along the top, front to back
                    {
                        float a0 = Mathf.Lerp(35f, 150f, i / 12f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(35f, 150f, (i + 1) / 12f) * Mathf.Deg2Rad;
                        mb.Sub(1).Capsule(hc + new Vector3(0, Mathf.Sin(a0) * 0.355f, Mathf.Cos(a0) * 0.37f), hc + new Vector3(0, Mathf.Sin(a1) * 0.355f, Mathf.Cos(a1) * 0.37f), 0.032f, 8, gold);
                    }
                    Torus(mb, hc + new Vector3(0, -0.2f, -0.03f), Quaternion.Euler(90f, 0, 0), 0.27f, 0.02f, 28, 6, gold);   // rim at the neck
                    mb.Pop();
                    break;
                }
                case 35: // OWAYS cereal (a streamer item): a printed carton - sunburst behind a bowl of rainbow rings and milk, the blue name banner, LIVE sticker, nutrition panel on the side
                {
                    var blue = new Color(0.13f, 0.33f, 0.82f); var milk = new Color(0.98f, 0.97f, 0.93f); const float z = 0.025f;
                    Color[] ring = { new Color(1f, 0.82f, 0.15f), new Color(0.95f, 0.3f, 0.55f), new Color(0.35f, 0.78f, 0.3f), new Color(1f, 0.5f, 0.12f), new Color(0.55f, 0.35f, 0.9f) };
                    mb.Sub(2).Box(Vector3.zero, new Vector3(0.14f, 0.2f, 0.05f), c, 0.0025f, 2);
                    // sunburst rays (lighter orange) behind the bowl
                    for (int i = 0; i < 16; i += 2) Fan(mb, 0, new Vector3(0, -0.035f, z + 0.0003f), Quaternion.identity, 0.064f, i * 22.5f, i * 22.5f + 11.25f, 3, Color.Lerp(c, new Color(1f, 0.95f, 0.6f), 0.45f));
                    Fan(mb, 0, new Vector3(0, -0.035f, z + 0.0004f), Quaternion.identity, 0.022f, 0f, 360f, 24, Color.Lerp(c, new Color(1f, 0.97f, 0.75f), 0.6f));
                    // the blue name banner with a scalloped lower edge (front and back)
                    foreach (float sz in new[] { 1f, -1f })
                    {
                        var q = sz > 0 ? Quaternion.identity : Quaternion.Euler(0, 180f, 0);
                        mb.Sub(0).Box(new Vector3(0, 0.072f, sz * (z + 0.0005f)), new Vector3(0.136f, 0.046f, 0.001f), blue, 0f);
                        for (int k = 0; k < 7; k++) Fan(mb, 0, new Vector3(sz * (-0.058f + k * 0.0193f), 0.049f, sz * (z + 0.0008f)), q, 0.0105f, 180f, 360f, 10, blue);
                        mb.Sub(0).Box(new Vector3(0, 0.0965f, sz * (z + 0.0006f)), new Vector3(0.136f, 0.003f, 0.001f), new Color(1f, 0.85f, 0.2f), 0f);   // gold pinstripe
                    }
                    // the bowl: red, a light rim, a pool of milk, a heap of rainbow rings, a silver spoon, splashes
                    Color bowl = new Color(0.86f, 0.14f, 0.14f);
                    Fan(mb, 3, new Vector3(0, -0.055f, z + 0.0008f), Quaternion.identity, 0.045f, 180f, 360f, 24, bowl, 0.62f);
                    Fan(mb, 3, new Vector3(-0.014f, -0.064f, z + 0.001f), Quaternion.identity, 0.016f, 200f, 250f, 6, Color.Lerp(bowl, Color.white, 0.4f), 0.62f);   // shine
                    Fan(mb, 3, new Vector3(0, -0.055f, z + 0.0012f), Quaternion.identity, 0.0465f, 0f, 360f, 32, Color.Lerp(bowl, Color.white, 0.35f), 0.2f);   // rim
                    Fan(mb, 3, new Vector3(0, -0.055f, z + 0.0014f), Quaternion.identity, 0.042f, 0f, 360f, 32, milk, 0.2f);
                    var rnd = new System.Random(35);
                    for (int i = 0; i < 18; i++)
                    {
                        float x = -0.034f + (float)rnd.NextDouble() * 0.068f, hump = 1f - Mathf.Pow(x / 0.04f, 2f);
                        float y = -0.056f + (float)rnd.NextDouble() * 0.012f * hump + (i % 3) * 0.004f * hump;
                        Torus(mb, new Vector3(x, y, z + 0.0018f + (i % 3) * 0.0004f), Quaternion.Euler(-10f + (float)rnd.NextDouble() * 30f, (float)rnd.NextDouble() * 30f - 15f, (float)rnd.NextDouble() * 180f), 0.0052f, 0.0021f, 10, 4, ring[i % ring.Length], 0.35f);
                    }
                    for (int i = 0; i < 3; i++)   // rings flying out
                        Torus(mb, new Vector3(-0.04f + i * 0.04f, -0.022f + (i % 2) * 0.007f, z + 0.0018f), Quaternion.Euler(0, 0, 30f * i), 0.0062f, 0.0024f, 12, 4, ring[(i + 2) % ring.Length], 0.35f);
                    foreach (var sp in new[] { new Vector3(-0.028f, -0.045f, 0), new Vector3(0.03f, -0.043f, 0), new Vector3(-0.016f, -0.039f, 0), new Vector3(0.02f, -0.036f, 0) })
                        mb.Sub(3).Sphere(sp + new Vector3(0, 0, z + 0.0014f), new Vector3(0.0028f, 0.0036f, 0.0006f), 10, milk);   // milk splashes
                    mb.Sub(1).Capsule(new Vector3(0.022f, -0.05f, z + 0.0024f), new Vector3(0.056f, -0.012f, z + 0.0028f), 0.0022f, 0.0026f, 8, Steel);   // spoon handle
                    mb.Sub(1).Sphere(new Vector3(0.018f, -0.054f, z + 0.0024f), new Vector3(0.0075f, 0.0055f, 0.0012f), 14, Steel);
                    // the LIVE sticker: a red pill with a white record dot
                    mb.Sub(3).Box(new Vector3(0.046f, 0.012f, z + 0.0009f), new Vector3(0.04f, 0.016f, 0.0012f), new Color(0.88f, 0.08f, 0.08f), 0.0007f);
                    mb.Sub(3).Sphere(new Vector3(0.0315f, 0.012f, z + 0.0016f), new Vector3(0.0026f, 0.0026f, 0.0005f), 10, Color.white);
                    // the top: glued flaps, a crease, side panels (blue) with a white nutrition panel and its lines
                    // the top is torn open: the front and back flaps folded back, the side flaps up, cereal heaped inside
                    mb.Sub(2).Box(new Vector3(0, 0.0985f, 0), new Vector3(0.136f, 0.002f, 0.046f), new Color(0.72f, 0.62f, 0.45f), 0f);   // the bag's inside, just under the rim
                    foreach (float sz in new[] { 1f, -1f })
                        mb.Sub(2).Box(new Vector3(0, 0.1f + 0.016f, sz * (0.025f + 0.012f)), new Vector3(0.138f, 0.036f, 0.0015f), c * 0.92f, 0f, 0, Quaternion.Euler(sz * 42f, 0, 0));
                    foreach (float sx in new[] { 1f, -1f })
                        mb.Sub(2).Box(new Vector3(sx * (0.07f - 0.0008f), 0.1f + 0.012f, 0), new Vector3(0.0015f, 0.026f, 0.048f), c * 0.85f, 0f, 0, Quaternion.Euler(0, 0, sx * -12f));
                    for (int i = 0; i < 26; i++)
                    {
                        float x = -0.06f + (float)rnd.NextDouble() * 0.12f, z2 = -0.019f + (float)rnd.NextDouble() * 0.038f;
                        float hump = Mathf.Max(0f, 1f - Mathf.Pow(x / 0.07f, 2f)) * 0.012f;
                        Torus(mb, new Vector3(x, 0.1015f + (float)rnd.NextDouble() * hump, z2), Quaternion.Euler(70f + (float)rnd.NextDouble() * 40f, (float)rnd.NextDouble() * 180f, 0), 0.0052f, 0.0021f, 10, 4, ring[i % ring.Length]);
                    }
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Box(new Vector3(sx * 0.0703f, 0.0f, 0), new Vector3(0.0008f, 0.19f, 0.044f), blue, 0f);
                        mb.Sub(0).Box(new Vector3(sx * 0.0708f, 0.07f, 0), new Vector3(0.0006f, 0.03f, 0.036f), c, 0f);
                    }
                    mb.Sub(0).Box(new Vector3(0.0712f, -0.035f, 0), new Vector3(0.0006f, 0.095f, 0.036f), Color.white, 0f);
                    for (int i = 0; i < 12; i++) mb.Sub(0).Box(new Vector3(0.0716f, -0.0f - i * 0.0068f, 0), new Vector3(0.0004f, i % 4 == 0 ? 0.0016f : 0.0007f, 0.032f), Black, 0f);
                    // the back: a ring maze to play over breakfast
                    mb.Sub(0).Box(new Vector3(0, -0.028f, -z - 0.0005f), new Vector3(0.11f, 0.1f, 0.001f), Color.white, 0f);
                    {
                        const int G = 7; const float cell = 0.0135f; var o = new Vector2(-G * cell * 0.5f, -0.028f - G * cell * 0.5f);
                        void Wall(float x0, float y0, float x1, float y1) => mb.Sub(0).Box(new Vector3(-(x0 + x1) * 0.5f, (y0 + y1) * 0.5f, -z - 0.0012f), new Vector3(Mathf.Abs(x1 - x0) + 0.0016f, Mathf.Abs(y1 - y0) + 0.0016f, 0.0006f), blue, 0f);
                        Wall(o.x, o.y, o.x + G * cell, o.y); Wall(o.x, o.y + G * cell, o.x + G * cell, o.y + G * cell);
                        Wall(o.x, o.y + cell, o.x, o.y + G * cell); Wall(o.x + G * cell, o.y, o.x + G * cell, o.y + (G - 1) * cell);   // gaps = start / finish
                        for (int gx = 0; gx < G - 1; gx++)
                            for (int gy = 0; gy < G - 1; gy++)
                            {
                                float x = o.x + (gx + 1) * cell, y = o.y + (gy + 1) * cell;
                                if (rnd.NextDouble() < 0.5) Wall(x, y, x + cell, y); else Wall(x, y, x, y + cell);
                            }
                        Torus(mb, new Vector3(-(o.x + G * cell + 0.006f), o.y + G * cell - cell * 0.5f, -z - 0.0016f), Quaternion.identity, 0.004f, 0.0016f, 12, 6, ring[0], 0.3f);   // the prize ring at the exit
                    }
                    break;
                }
                case 36: // (3.64) OLIVER KICKS (a streamer item): a chunky high-top - white sole with a glowing mint stripe, black upper,
                         // purple side panels, a gold lightning bolt on each side, white laces, a red tongue tab and heel pull. Toe at +x.
                {
                    Color sole = new Color(0.95f, 0.95f, 0.93f), mint = new Color(0.25f, 1f, 0.75f), upper = new Color(0.07f, 0.07f, 0.08f),
                          panel = new Color(0.45f, 0.18f, 0.85f), gold = new Color(1f, 0.78f, 0.25f), red = new Color(0.9f, 0.1f, 0.12f);
                    mb.Sub(0).Box(new Vector3(0, 0.014f, 0), new Vector3(0.29f, 0.028f, 0.105f), sole, 0.012f, 3);                  // sole
                    mb.Sub(4).Box(new Vector3(0, 0.031f, 0), new Vector3(0.284f, 0.007f, 0.107f), mint, 0.003f);                     // glowing midsole stripe
                    for (int i = 0; i < 7; i++) mb.Sub(0).Box(new Vector3(-0.12f + i * 0.04f, 0.002f, 0), new Vector3(0.012f, 0.004f, 0.1f), sole * 0.8f, 0.001f);   // tread
                    mb.Sub(3).Sphere(new Vector3(0.095f, 0.045f, 0), new Vector3(0.055f, 0.032f, 0.05f), 16, sole, 0f, 0.5f);         // rubber toe cap
                    mb.Sub(2).Box(new Vector3(-0.005f, 0.066f, 0), new Vector3(0.2f, 0.07f, 0.096f), upper, 0.028f, 3);               // the upper
                    mb.Sub(2).Cyl(new Vector3(-0.075f, 0.07f, 0), new Vector3(-0.072f, 0.14f, 0), 0.05f, 0.046f, 18, upper);          // high-top ankle
                    mb.Sub(3).Tube(new Vector3(-0.072f, 0.135f, 0), new Vector3(-0.071f, 0.148f, 0), 0.05f, 0.036f, 18, panel);        // padded collar
                    foreach (float sz in new[] { 1f, -1f })
                    {
                        mb.Sub(3).Box(new Vector3(-0.025f, 0.062f, sz * 0.049f), new Vector3(0.17f, 0.046f, 0.004f), panel, 0.002f);   // side panel
                        // the lightning bolt
                        mb.Sub(1).Box(new Vector3(0.005f, 0.072f, sz * 0.0515f), new Vector3(0.05f, 0.01f, 0.002f), gold, 0.001f, 1, Quaternion.Euler(0, 0, 28f));
                        mb.Sub(1).Box(new Vector3(-0.02f, 0.06f, sz * 0.0515f), new Vector3(0.03f, 0.009f, 0.002f), gold, 0.001f);
                        mb.Sub(1).Box(new Vector3(-0.045f, 0.05f, sz * 0.0515f), new Vector3(0.05f, 0.01f, 0.002f), gold, 0.001f, 1, Quaternion.Euler(0, 0, 28f));
                        for (int i = 0; i < 5; i++) mb.Sub(1).Sphere(new Vector3(0.045f - i * 0.022f, 0.101f - i * 0.004f, sz * 0.03f), Vector3.one * 0.0035f, 6, gold);   // eyelets
                    }
                    for (int i = 0; i < 5; i++) mb.Sub(2).Box(new Vector3(0.045f - i * 0.022f, 0.104f - i * 0.004f, 0), new Vector3(0.007f, 0.006f, 0.064f), sole, 0.002f);   // laces
                    mb.Sub(2).Box(new Vector3(-0.035f, 0.135f, 0), new Vector3(0.03f, 0.06f, 0.05f), red, 0.01f, 2, Quaternion.Euler(0, 0, -20f));   // tongue
                    mb.Sub(2).Box(new Vector3(-0.124f, 0.13f, 0), new Vector3(0.012f, 0.05f, 0.026f), red, 0.004f);                                  // heel pull tab
                    mb.Sub(4).Box(new Vector3(-0.142f, 0.03f, 0), new Vector3(0.006f, 0.02f, 0.05f), mint, 0.002f);                                  // glowing heel window
                    break;
                }
                case 37: // (3.64) S1KDM0R3 (a streamer item): a grim reaper statue - hooded black robe, a skull with glowing purple
                         // eyes in the hood, bony hands on a long scythe - on the LIVE display base
                {
                    DisplayBase(mb, -0.083f, 0.046f);
                    Color robe = new Color(0.05f, 0.05f, 0.06f), robe2 = new Color(0.1f, 0.09f, 0.12f), bone = new Color(0.9f, 0.87f, 0.78f), wood = new Color(0.25f, 0.16f, 0.1f), blade = new Color(0.75f, 0.77f, 0.8f);
                    mb.Sub(2).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, -0.083f), new Vector2(0.04f, -0.083f), new Vector2(0.041f, -0.078f), new Vector2(0.034f, -0.045f), new Vector2(0.027f, -0.01f), new Vector2(0.025f, 0.02f), new Vector2(0.028f, 0.035f), new Vector2(0.022f, 0.045f), new Vector2(0f, 0.047f) }, 28, robe);   // the robe
                    for (int i = 0; i < 6; i++) { float a = i * 60f * Mathf.Deg2Rad; mb.Sub(2).Capsule(new Vector3(Mathf.Cos(a) * 0.036f, -0.078f, Mathf.Sin(a) * 0.036f), new Vector3(Mathf.Cos(a) * 0.026f, -0.02f, Mathf.Sin(a) * 0.026f), 0.004f, 0.003f, 6, robe2); }   // folds
                    mb.Sub(2).Sphere(new Vector3(0, 0.06f, -0.002f), new Vector3(0.027f, 0.032f, 0.029f), 16, robe);       // the hood
                    mb.Sub(0).Sphere(new Vector3(0, 0.056f, 0.012f), new Vector3(0.019f, 0.023f, 0.018f), 14, Black);      // the dark inside of the hood
                    mb.Sub(3).Sphere(new Vector3(0, 0.054f, 0.016f), new Vector3(0.012f, 0.014f, 0.011f), 14, bone);       // skull
                    mb.Sub(3).Box(new Vector3(0, 0.043f, 0.02f), new Vector3(0.012f, 0.006f, 0.006f), bone, 0.002f);      // jaw
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Sphere(new Vector3(sx * 0.0045f, 0.056f, 0.0255f), Vector3.one * 0.0035f, 8, Black);    // eye sockets
                        mb.Sub(4).Sphere(new Vector3(sx * 0.0045f, 0.056f, 0.0272f), Vector3.one * 0.0018f, 8, Color.white);   // glowing eyes
                    }
                    // arms in wide sleeves, bony hands gripping the scythe on the right
                    mb.Sub(2).Capsule(new Vector3(-0.024f, 0.03f, 0.004f), new Vector3(-0.03f, 0.0f, 0.022f), 0.009f, 0.012f, 10, robe2);
                    mb.Sub(2).Capsule(new Vector3(0.024f, 0.03f, 0.004f), new Vector3(0.03f, 0.012f, 0.024f), 0.009f, 0.012f, 10, robe2);
                    mb.Sub(3).Sphere(new Vector3(-0.03f, -0.006f, 0.026f), new Vector3(0.006f, 0.007f, 0.006f), 8, bone);
                    mb.Sub(3).Sphere(new Vector3(0.032f, 0.006f, 0.028f), new Vector3(0.006f, 0.007f, 0.006f), 8, bone);
                    for (int f = 0; f < 3; f++) mb.Sub(3).Capsule(new Vector3(0.034f, 0.003f + f * 0.004f, 0.031f), new Vector3(0.038f, 0.002f + f * 0.004f, 0.026f), 0.0012f, 5, bone);   // finger bones
                    // the scythe: a long staff and a curved blade at the top
                    mb.Sub(1).Cyl(new Vector3(0.037f, -0.082f, 0.028f), new Vector3(0.037f, 0.115f, 0.028f), 0.0024f, 0.0022f, 8, wood);
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = i / 8f, a1 = (i + 1) / 8f;
                        Vector3 B(float t) => new Vector3(0.037f - t * 0.07f, 0.112f - t * t * 0.03f, 0.028f);
                        mb.Sub(1).Capsule(B(a0), B(a1), 0.0045f * (1f - a0 * 0.8f), 0.0045f * (1f - a1 * 0.8f), 6, blade);
                    }
                    break;
                }
                case 34: // tennis ball: fuzzy yellow-green with the curving white seam
                {
                    mb.Sub(2).Sphere(Vector3.zero, Vector3.one * 0.033f, 16, c);
                    for (int i = 0; i < 40; i++)
                    {
                        float t = i / 40f * Mathf.PI * 2f, t2 = (i + 1) / 40f * Mathf.PI * 2f;
                        Vector3 P(float a) => new Vector3(Mathf.Cos(a) + 0.35f * Mathf.Cos(3f * a), Mathf.Sin(a) - 0.35f * Mathf.Sin(3f * a), 0.75f * Mathf.Sin(2f * a)).normalized * 0.0334f;
                        mb.Sub(0).Capsule(P(t), P(t2), 0.0018f, 4, new Color(0.95f, 0.95f, 0.92f));
                    }
                    break;
                }
                default:
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0.0f, -0.02f), new Vector2(0.022f, 0.004f), new Vector2(0.016f, 0.012f), new Vector2(0.0f, 0.013f) }, 8, c);
                    break;
            }
        }
    }
}
