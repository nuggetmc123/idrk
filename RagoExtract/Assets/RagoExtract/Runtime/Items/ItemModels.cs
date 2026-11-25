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
            bool gold = d.kind == ItemKind.Valuable && (d.shape == 6 || d.shape == 0 || d.shape == 18 || d.shape == 19 || d.shape == 33);   // 33: the operator figurine's gold armour trim
            if (gold) m1 = Mats.Gold;
            if (d.kind == ItemKind.Valuable && d.shape == 7) m1 = Mats.Unlit("diamond", new Color(0.5f, 0.85f, 1f, 0.85f));
            return new[] { m0, m1, m2, m3 };
        }

        /// <summary>The name on a streamer figurine's base plate (null = no plate), where the plate is, and its tilt.</summary>
        public static string Nameplate(SimpleDef d, out Vector3 at)
        {
            at = Vector3.zero;
            if (d == null || d.kind != ItemKind.Valuable) return null;
            if (d.shape == 32) { at = new Vector3(0, -0.064f, 0.0447f); return "PEANUT8"; }
            if (d.shape == 33) { at = new Vector3(0, -0.092f, 0.0497f); return "SHUSHED"; }
            return null;
        }

        /// <summary>Printed words on an item (text, local position, size, colour), drawn facing out of the front (+Z).</summary>
        public static (string text, Vector3 at, float size, Color col)[] Labels(SimpleDef d)
        {
            if (d == null || d.kind != ItemKind.Valuable) return null;
            if (d.shape == 35) return new[] { ("OWAYS", new Vector3(0, 0.072f, 0.0272f), 0.011f, Color.white), ("CEREAL", new Vector3(0, 0.05f, 0.0272f), 0.0055f, new Color(1f, 0.85f, 0.3f)), ("crunchy  rings", new Vector3(0, 0.022f, 0.0262f), 0.0035f, Color.white) };
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
                case 5: // energy drink: a proper can shape - tapered top, rolled rims, ring pull, label stripes
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0f, -0.061f), new Vector2(0.026f, -0.061f), new Vector2(0.031f, -0.056f), new Vector2(0.033f, -0.05f) }, 20, new Color(0.8f, 0.8f, 0.82f));
                    mb.Sub(0).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, 0.046f, 0), 0.033f, 0.033f, 20, c);
                    mb.Sub(3).Cyl(new Vector3(0, -0.004f, 0), new Vector3(0, 0.012f, 0), 0.0334f, 0.0334f, 20, Color.white, false);   // logo band
                    mb.Sub(0).Cyl(new Vector3(0, 0.0005f, 0), new Vector3(0, 0.0075f, 0), 0.0336f, 0.0336f, 20, c * 0.5f, false);
                    mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, new List<Vector2> { new Vector2(0.033f, 0.046f), new Vector2(0.029f, 0.056f), new Vector2(0.0285f, 0.06f), new Vector2(0.026f, 0.059f), new Vector2(0f, 0.058f) }, 20, new Color(0.8f, 0.8f, 0.82f));
                    mb.Sub(1).Tube(new Vector3(0, 0.0585f, 0.006f), new Vector3(0, 0.0595f, 0.006f), 0.0075f, 0.004f, 12, new Color(0.7f, 0.7f, 0.72f));   // ring pull
                    mb.Sub(1).Box(new Vector3(0, 0.0592f, -0.004f), new Vector3(0.008f, 0.0008f, 0.01f), new Color(0.7f, 0.7f, 0.72f), 0.0004f);
                    break;
                default:
                    mb.Sub(0).Cyl(new Vector3(0, -0.06f, 0), new Vector3(0, 0.05f, 0), 0.014f, 0.014f, 12, c);
                    break;
            }
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
                case 32: // peanut streamer figurine: a cartoon peanut in a gaming headset with a boom mic, waving, on a LIVE base
                {
                    Color shell = c, dark = c * 0.72f; dark.a = 1f;
                    // round display base with a red LIVE plate on the front
                    mb.Sub(0).Cyl(new Vector3(0, -0.07f, 0), new Vector3(0, -0.058f, 0), 0.045f, 0.042f, 28, DarkGrey);
                    mb.Sub(0).Cyl(new Vector3(0, -0.058f, 0), new Vector3(0, -0.055f, 0), 0.038f, 0.038f, 28, new Color(0.45f, 0.2f, 0.75f));   // purple top
                    mb.Sub(0).Box(new Vector3(0, -0.064f, 0.043f), new Vector3(0.046f, 0.009f, 0.003f), Red, 0.001f, 1, Quaternion.Euler(-6f, 0, 0));   // nameplate (PEANUT8 is written on it)
                    // the peanut: two lobes with a pinched waist, tilted a touch
                    mb.Push(new Vector3(0, -0.006f, 0), Quaternion.Euler(0, 0, -4f));
                    mb.Sub(0).Sphere(new Vector3(0, -0.022f, 0), new Vector3(0.03f, 0.033f, 0.028f), 18, shell);
                    mb.Sub(0).Sphere(new Vector3(0, 0.03f, 0), new Vector3(0.028f, 0.031f, 0.026f), 18, shell);
                    mb.Sub(0).Cyl(new Vector3(0, -0.006f, 0), new Vector3(0, 0.014f, 0), 0.024f, 0.023f, 18, shell);   // waist
                    // shell texture: rows of little pits and the faint ridges
                    for (int row = 0; row < 7; row++)
                        for (int k = 0; k < 9; k++)
                        {
                            float y = -0.044f + row * 0.013f; float a = (k + (row % 2) * 0.5f) * Mathf.PI * 2f / 9f;
                            if (a > Mathf.PI * 0.3f && a < Mathf.PI * 0.7f && row >= 4) continue;   // keep the face clear
                            float lobe = y < 0.004f ? 0.03f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((y + 0.022f) / 0.033f, 2f))) : 0.028f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((y - 0.03f) / 0.031f, 2f)));
                            float r = Mathf.Max(0.018f, lobe);
                            { var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); mb.Sub(0).Sphere(new Vector3(n.x * r * 0.955f, y, n.z * r * 0.93f * 0.955f), new Vector3(0.0022f, 0.0022f, 0.0022f), 6, dark * 0.85f); }
                        }
                    // face (on the top lobe, +Z): big eyes, eyebrows, a happy open mouth
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(3).Sphere(new Vector3(x * 0.0095f, 0.036f, 0.022f), new Vector3(0.0075f, 0.009f, 0.005f), 12, Color.white);
                        mb.Sub(3).Sphere(new Vector3(x * 0.0085f, 0.0355f, 0.0262f), new Vector3(0.0038f, 0.0046f, 0.0022f), 10, Black);
                        mb.Sub(3).Sphere(new Vector3(x * 0.0078f, 0.0375f, 0.0282f), Vector3.one * 0.0012f, 6, Color.white);   // catch-light
                        mb.Sub(0).Box(new Vector3(x * 0.0095f, 0.0475f, 0.022f), new Vector3(0.008f, 0.0018f, 0.002f), dark * 0.6f, 0.0006f, 1, Quaternion.Euler(-20f, 0, x * -12f));
                    }
                    mb.Sub(0).Sphere(new Vector3(0, 0.0245f, 0.0232f), new Vector3(0.0095f, 0.0068f, 0.003f), 14, new Color(0.22f, 0.04f, 0.05f), 0.5f, 1f);   // big open smile (lower half)
                    mb.Sub(0).Sphere(new Vector3(0, 0.0195f, 0.0242f), new Vector3(0.0048f, 0.0018f, 0.0014f), 8, new Color(0.9f, 0.4f, 0.45f));   // tongue
                    foreach (float x in new[] { -1f, 1f }) mb.Sub(0).Sphere(new Vector3(x * 0.017f, 0.027f, 0.018f), new Vector3(0.004f, 0.0025f, 0.002f), 8, new Color(0.95f, 0.5f, 0.45f));   // cheeks
                    // gaming headset: padded band over the top, big ear cups with purple rings, a boom mic to the mouth
                    for (int i = 0; i <= 10; i++)
                    {
                        float a0 = Mathf.PI * i / 10f, a1 = Mathf.PI * (i + 1) / 10f;
                        if (i == 10) break;
                        var p0 = new Vector3(Mathf.Cos(a0) * 0.031f, 0.03f + Mathf.Sin(a0) * 0.034f, -0.002f);
                        var p1 = new Vector3(Mathf.Cos(a1) * 0.031f, 0.03f + Mathf.Sin(a1) * 0.034f, -0.002f);
                        mb.Sub(0).Capsule(p0, p1, 0.0032f, 6, DarkGrey);
                    }
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Cyl(new Vector3(x * 0.026f, 0.03f, 0), new Vector3(x * 0.036f, 0.03f, 0), 0.012f, 0.011f, 18, DarkGrey);
                        mb.Sub(0).Tube(new Vector3(x * 0.0362f, 0.03f, 0), new Vector3(x * 0.0372f, 0.03f, 0), 0.0105f, 0.007f, 18, new Color(0.6f, 0.3f, 1f));   // glowing ring
                        mb.Sub(3).Cyl(new Vector3(x * 0.0365f, 0.03f, 0), new Vector3(x * 0.0372f, 0.03f, 0), 0.0068f, 0.0068f, 14, Black);
                    }
                    mb.Sub(1).Capsule(new Vector3(-0.034f, 0.026f, 0.006f), new Vector3(-0.02f, 0.02f, 0.024f), 0.0016f, 6, Steel);   // boom
                    mb.Sub(0).Capsule(new Vector3(-0.019f, 0.0198f, 0.0245f), new Vector3(-0.012f, 0.0195f, 0.027f), 0.0034f, 8, DarkGrey);   // mic tip
                    // stick arms in little white gloves: one waving, one on the hip
                    mb.Sub(0).Capsule(new Vector3(0.027f, 0.0f, 0.004f), new Vector3(0.045f, 0.024f, 0.008f), 0.0022f, 6, Black);
                    mb.Sub(3).Sphere(new Vector3(0.047f, 0.028f, 0.009f), new Vector3(0.0065f, 0.0075f, 0.004f), 10, Color.white);
                    mb.Sub(0).Capsule(new Vector3(-0.028f, -0.002f, 0.004f), new Vector3(-0.04f, -0.018f, 0.006f), 0.0022f, 6, Black);
                    mb.Sub(0).Capsule(new Vector3(-0.04f, -0.018f, 0.006f), new Vector3(-0.031f, -0.03f, 0.01f), 0.0022f, 6, Black);
                    mb.Sub(3).Sphere(new Vector3(-0.03f, -0.031f, 0.011f), Vector3.one * 0.0055f, 10, Color.white);
                    // little legs and sneakers standing on the base
                    foreach (float x in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Capsule(new Vector3(x * 0.01f, -0.052f, 0), new Vector3(x * 0.012f, -0.062f, 0.002f), 0.0022f, 6, Black);
                        mb.Sub(0).Sphere(new Vector3(x * 0.012f, -0.0625f, 0.007f), new Vector3(0.006f, 0.0035f, 0.0095f), 10, x < 0 ? Red : new Color(0.45f, 0.2f, 0.75f));
                    }
                    mb.Pop();
                    break;
                }
                case 33: // shush operator figurine (a streamer item): the operator in Juggernaut armour, a finger to his lips, on the LIVE base
                {
                    // the same purple display base with its red LIVE plate as the peanut streamer
                    mb.Sub(0).Cyl(new Vector3(0, -0.098f, 0), new Vector3(0, -0.086f, 0), 0.05f, 0.047f, 28, DarkGrey);
                    mb.Sub(0).Cyl(new Vector3(0, -0.086f, 0), new Vector3(0, -0.083f, 0), 0.043f, 0.043f, 28, new Color(0.45f, 0.2f, 0.75f));
                    mb.Sub(0).Box(new Vector3(0, -0.092f, 0.048f), new Vector3(0.05f, 0.009f, 0.003f), Red, 0.001f, 1, Quaternion.Euler(-6f, 0, 0));   // nameplate (SHUSHED is written on it)
                    // the figure, built at a tenth of real size (1.8 m tall -> 18 cm), feet on the base
                    Color gold = new Color(1f, 0.78f, 0.3f), suit = new Color(0.1f, 0.1f, 0.11f), suit2 = new Color(0.14f, 0.14f, 0.15f), plate = new Color(0.06f, 0.06f, 0.065f), helmet = new Color(0.85f, 0.2f, 0.15f), glove = new Color(0.9f, 0.25f, 0.18f), belt = new Color(0.55f, 0.56f, 0.55f);
                    mb.Push(new Vector3(0, -0.083f, 0), Quaternion.identity, Vector3.one * 0.1f);
                    // boots and legs
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Box(new Vector3(s * 0.11f, 0.06f, 0.04f), new Vector3(0.14f, 0.12f, 0.3f), Black, 0.03f, 2);
                        mb.Sub(2).Box(new Vector3(s * 0.11f, 0.5f, 0f), new Vector3(0.17f, 0.78f, 0.2f), suit, 0.05f, 2);
                        mb.Sub(0).Box(new Vector3(s * 0.11f, 0.5f, 0.1f), new Vector3(0.13f, 0.14f, 0.04f), plate, 0.02f, 2);   // knee pads
                    }
                    mb.Sub(2).Box(new Vector3(0, 0.95f, 0), new Vector3(0.42f, 0.16f, 0.24f), suit, 0.05f, 2);   // hips
                    // belt with pouches
                    mb.Sub(0).Box(new Vector3(0, 0.98f, 0), new Vector3(0.46f, 0.07f, 0.27f), belt, 0.02f, 2);
                    foreach (float s in new[] { -1f, 1f }) mb.Sub(0).Box(new Vector3(s * 0.24f, 0.93f, 0.02f), new Vector3(0.06f, 0.14f, 0.12f), belt * 0.9f, 0.02f, 2);
                    // torso under the armour, neck
                    mb.Sub(2).Box(new Vector3(0, 1.26f, 0), new Vector3(0.44f, 0.52f, 0.24f), suit2, 0.06f, 2);
                    mb.Sub(2).Cyl(new Vector3(0, 1.5f, 0), new Vector3(0, 1.58f, 0), 0.06f, 0.06f, 12, suit);
                    // the Juggernaut vest: thick front and back plates, an armoured shell, gold trim and rivets, a groin flap, shoulder plates
                    mb.Sub(0).Box(new Vector3(0, 1.26f, 0.13f), new Vector3(0.48f, 0.46f, 0.07f), plate, 0.04f, 2);
                    mb.Sub(0).Box(new Vector3(0, 1.34f, 0.17f), new Vector3(0.38f, 0.2f, 0.03f), plate * 1.3f, 0.025f, 2);
                    mb.Sub(0).Box(new Vector3(0, 1.26f, -0.13f), new Vector3(0.48f, 0.46f, 0.06f), plate, 0.04f, 2);
                    mb.Sub(0).Box(new Vector3(0, 0.88f, 0.13f), new Vector3(0.22f, 0.18f, 0.04f), plate, 0.02f, 2);
                    foreach (float y in new[] { 1.47f, 1.06f }) mb.Sub(1).Box(new Vector3(0, y, 0.17f), new Vector3(0.44f, 0.018f, 0.02f), gold, 0.004f);
                    for (int i = 0; i < 6; i++) mb.Sub(1).Sphere(new Vector3(-0.18f + i * 0.072f, 1.44f, 0.185f), Vector3.one * 0.012f, 6, gold);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.Sub(0).Box(new Vector3(s * 0.29f, 1.46f, 0), new Vector3(0.18f, 0.07f, 0.26f), plate, 0.03f, 2, Quaternion.Euler(0, 0, s * -20f));
                        mb.Sub(1).Box(new Vector3(s * 0.3f, 1.5f, 0), new Vector3(0.16f, 0.012f, 0.24f), gold, 0.003f, 1, Quaternion.Euler(0, 0, s * -20f));
                    }
                    // left arm hanging, red glove
                    mb.Sub(2).Capsule(new Vector3(-0.3f, 1.42f, 0), new Vector3(-0.32f, 1.14f, 0.0f), 0.075f, 10, suit);
                    mb.Sub(2).Capsule(new Vector3(-0.32f, 1.14f, 0), new Vector3(-0.31f, 0.9f, 0.04f), 0.065f, 10, suit);
                    mb.Sub(0).Box(new Vector3(-0.31f, 0.83f, 0.04f), new Vector3(0.1f, 0.12f, 0.09f), glove, 0.03f, 2);
                    // right arm bent up, gloved hand at the mouth, the index finger up across the lips: shhh
                    mb.Sub(2).Capsule(new Vector3(0.3f, 1.42f, 0), new Vector3(0.3f, 1.17f, 0.12f), 0.075f, 10, suit);
                    mb.Sub(2).Capsule(new Vector3(0.3f, 1.17f, 0.12f), new Vector3(0.08f, 1.52f, 0.2f), 0.065f, 10, suit);
                    mb.Sub(0).Box(new Vector3(0.04f, 1.5f, 0.18f), new Vector3(0.1f, 0.1f, 0.08f), glove, 0.03f, 2, Quaternion.Euler(0, 0, 25f));   // fist, under the chin
                    mb.Sub(0).Capsule(new Vector3(0.015f, 1.54f, 0.17f), new Vector3(0.0f, 1.65f, 0.15f), 0.022f, 8, glove);   // the finger, up across the lips
                    // head: black face mask, the red helmet with a gold crest and rim, vent dots, two eye glints
                    mb.Sub(2).Sphere(new Vector3(0, 1.69f, 0), new Vector3(0.12f, 0.135f, 0.125f), 16, Black);
                    mb.Sub(3).Sphere(new Vector3(0, 1.71f, -0.005f), new Vector3(0.135f, 0.13f, 0.14f), 20, helmet, 0f, 0.52f);
                    mb.Sub(1).Push(new Vector3(0, 1.705f, -0.005f), Quaternion.identity, new Vector3(1f, 1f, 1.04f));
                    mb.Sub(1).Tube(new Vector3(0, -0.006f, 0), new Vector3(0, 0.006f, 0), 0.138f, 0.128f, 20, gold);
                    mb.Pop();
                    mb.Sub(1).Box(new Vector3(0, 1.83f, -0.01f), new Vector3(0.035f, 0.02f, 0.22f), gold, 0.006f, 1, Quaternion.Euler(0, 0, 0));
                    foreach (float x in new[] { -0.04f, 0.04f }) mb.Sub(0).Sphere(new Vector3(x, 1.79f, 0.11f), Vector3.one * 0.012f, 6, Black);
                    foreach (float x in new[] { -0.045f, 0.045f }) mb.Sub(3).Box(new Vector3(x, 1.7f, 0.122f), new Vector3(0.05f, 0.014f, 0.006f), new Color(0.9f, 0.9f, 0.95f), 0.002f);
                    mb.Pop();
                    break;
                }
                case 35: // cereal box: a tall printed carton - orange front, a blue band, a bowl of cereal, the name across the top
                {
                    var blue = new Color(0.15f, 0.35f, 0.8f); var milk = new Color(0.97f, 0.96f, 0.92f);
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.14f, 0.2f, 0.05f), c, 0.003f);
                    mb.Sub(0).Box(new Vector3(0, 0.068f, 0.0255f), new Vector3(0.13f, 0.05f, 0.002f), blue, 0f);          // name band (front)
                    mb.Sub(0).Box(new Vector3(0, 0.068f, -0.0255f), new Vector3(0.13f, 0.05f, 0.002f), blue, 0f);         // and on the back
                    mb.Sub(0).Sphere(new Vector3(0, -0.045f, 0.0262f), new Vector3(0.045f, 0.03f, 0.002f), 16, milk);       // the bowl of milk
                    mb.Sub(0).Box(new Vector3(0, -0.07f, 0.0262f), new Vector3(0.07f, 0.012f, 0.002f), new Color(0.85f, 0.15f, 0.15f), 0.004f);   // the bowl
                    for (int i = 0; i < 9; i++)   // cereal rings floating in it
                        mb.Sub(0).Sphere(new Vector3(-0.03f + (i % 5) * 0.015f, -0.04f + (i / 5) * 0.012f - (i % 2) * 0.004f, 0.0272f), new Vector3(0.0055f, 0.0055f, 0.0015f), 8, i % 3 == 0 ? new Color(0.95f, 0.8f, 0.2f) : i % 3 == 1 ? new Color(0.9f, 0.3f, 0.5f) : new Color(0.4f, 0.75f, 0.3f));
                    mb.Sub(0).Box(new Vector3(0, 0.1f, 0), new Vector3(0.142f, 0.004f, 0.052f), c * 0.85f, 0f);              // the top flap
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
