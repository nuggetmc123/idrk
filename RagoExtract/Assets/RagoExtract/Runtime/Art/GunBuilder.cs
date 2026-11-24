using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>Anchors produced by the gun model builder. Gun origin = centre of the pistol grip (where your hand is).</summary>
    public class GunRig
    {
        public GameObject root;
        public Transform muzzle, magwell, ejectPort, rail, foregrip, slide, pump, sightLine, butt;
        public Transform under;   // pistols: the accessory rail under the frame (lights / lasers)
        public GameObject muzzleDevice;   // the gun's own flash hider / brake: hidden while a suppressor or brake is screwed on
        public Vector3 slideTravel = new Vector3(0, 0, -0.03f);
        public float magEntry = -0.07f;   // how far below the magwell a mag's top is caught to slide in (pistols: the bottom of the grip)
        public Vector3 pumpTravel = new Vector3(0, 0, -0.07f);
        public Bounds bounds;
        public MagStyle magStyle;
        public float boreY;
    }

    public enum MagStyle { PistolBox, Stanag, Banana, Straight, Drum, SmgCurved, LongBox, BeltBox, ShellCaddy, Speedloader, Clip, BigBox, SmgStick }

    public static class GunBuilder
    {
        static Material FinishMat(Finish f)
        {
            switch (f)
            {
                case Finish.Gray: return Mats.Lit("fin_gray", new Color(0.24f, 0.25f, 0.25f), 0.7f, 0.35f, Mats.Metal, Mats.MetalN, 0.35f, 0.5f);
                case Finish.Chrome: return Mats.Chrome;
                case Finish.Gold: return Mats.Gold;
                case Finish.Fde: return Mats.Lit("fin_fde", new Color(0.6f, 0.5f, 0.36f), 0.25f, 0.3f, Mats.Metal, Mats.MetalN, 0.4f, 0.55f);
                case Finish.Od: return Mats.Lit("fin_od", new Color(0.3f, 0.33f, 0.24f), 0.2f, 0.3f, Mats.Metal, Mats.MetalN, 0.4f, 0.5f);
                case Finish.Tan: return Mats.Lit("fin_tan", new Color(0.7f, 0.62f, 0.48f), 0.2f, 0.3f, Mats.Metal, Mats.MetalN, 0.35f, 0.5f);
                case Finish.Plum: return Mats.Lit("fin_plum", new Color(0.32f, 0.14f, 0.12f), 0f, 0.3f, Mats.Noise, null, 0.3f, 0.2f);
                case Finish.Wood: return Mats.GunWood;
                default: return Mats.GunMetal;
            }
        }
        static Material FurnMat(Finish f)
        {
            switch (f)
            {
                case Finish.Wood: return Mats.GunWood;
                case Finish.Fde: return Mats.PolymerFde;
                case Finish.Od: return Mats.PolymerOd;
                case Finish.Tan: return Mats.Lit("poly_tan", new Color(0.66f, 0.58f, 0.44f), 0f, 0.25f, Mats.Noise, null, 0.45f, 0.2f);
                case Finish.Plum: return FinishMat(Finish.Plum);
                case Finish.Gold: return Mats.Gold;
                case Finish.Gray: return Mats.Lit("poly_gray", new Color(0.3f, 0.31f, 0.32f), 0f, 0.3f, Mats.Noise, null, 0.35f, 0.2f);
                case Finish.Chrome: return Mats.Chrome;
                default: return Mats.Polymer;
            }
        }

        // sub-mesh slots
        const int FIN = 0, FUR = 1, BLK = 2, ACC = 3, DARK = 4, DOT = 5;
        /// <summary>Glowing sight dots (like tritium) so the post and notch read in the dark too.</summary>
        public static Material SightDot => Mats.Lit("sight_dot", new Color(0.55f, 1f, 0.3f), 0f, 0.6f, null, null, 0f, 0f, false, 0.5f, new Color(0.45f, 1.1f, 0.25f));
        static readonly Color W = Color.white;

        public static GunRig Build(GunDef d, Transform parent)
        {
            var rig = new GunRig();
            var root = new GameObject(d.name);
            root.transform.SetParent(parent, false);
            rig.root = root;
            var mb = new MB(6);
            var mats = new[] { FinishMat(d.finish), FurnMat(d.furniture), Mats.Polymer, d.finish == Finish.Gold || d.furniture == Finish.Gold ? Mats.Gold : Mats.GunBlued, Mats.Lit("gun_dark", new Color(0.03f, 0.03f, 0.03f), 0.5f, 0.3f), SightDot };
            _device = new MB(6);
            switch (d.style)
            {
                case GunStyle.Pistol: Pistol(d, rig, mb, mats); break;
                case GunStyle.Revolver: Revolver(d, rig, mb, mats); break;
                case GunStyle.AR: AR(d, rig, mb, mats); break;
                case GunStyle.AK: AK(d, rig, mb, mats); break;
                case GunStyle.Battle: Battle(d, rig, mb, mats); break;
                case GunStyle.SmgBox: SmgBox(d, rig, mb, mats); break;
                case GunStyle.SmgMac: SmgMac(d, rig, mb, mats); break;
                case GunStyle.SmgVector: SmgVector(d, rig, mb, mats); break;
                case GunStyle.Bullpup: Bullpup(d, rig, mb, mats); break;
                case GunStyle.Bolt: Bolt(d, rig, mb, mats); break;
                case GunStyle.Pump: Pump(d, rig, mb, mats); break;
                case GunStyle.Double: DoubleBarrel(d, rig, mb, mats); break;
                case GunStyle.BoxShotgun: BoxShotgun(d, rig, mb, mats); break;
                case GunStyle.Lmg: Lmg(d, rig, mb, mats); break;
                case GunStyle.AntiMat: AntiMat(d, rig, mb, mats); break;
                case GunStyle.Tommy: Tommy(d, rig, mb, mats); break;
            }
            var body = mb.Build("Body", root.transform, mats);
            var dev = _device; _device = null;
            if (dev != null && dev.VertexCount > 0) rig.muzzleDevice = dev.Build("MuzzleDevice", root.transform, mats);
            rig.bounds = body.GetComponent<MeshFilter>().sharedMesh.bounds;
            return rig;
        }

        static Transform Anchor(GameObject root, string name, Vector3 pos, Quaternion? rot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.localPosition = pos;
            t.localRotation = rot ?? Quaternion.identity;
            return t;
        }

        // ------------------------------------------------------------------ shared parts
        static void PistolGrip(MB mb, int sub, float angle = 18f, float len = 0.1f, float width = 0.032f, float topY = 0.03f, float topZ = 0f)
        {
            var poly = new List<Vector2>
            {
                new Vector2(topZ + 0.03f, topY), new Vector2(topZ - 0.03f, topY),
            };
            float a = angle * Mathf.Deg2Rad;
            Vector2 down = new Vector2(-Mathf.Sin(a), -Mathf.Cos(a));
            Vector2 b0 = new Vector2(topZ - 0.03f, topY) + down * len;
            Vector2 b1 = new Vector2(topZ + 0.022f, topY) + down * len;
            poly.Add(b0 + new Vector2(-0.004f, 0.01f));
            poly.Add(b0 + new Vector2(0.004f, -0.002f));
            poly.Add(b1 + new Vector2(0.0f, -0.002f));
            // finger grooves on front strap
            for (int i = 1; i <= 3; i++)
            {
                Vector2 p = Vector2.Lerp(b1, new Vector2(topZ + 0.03f, topY), i / 4f);
                poly.Add(p + new Vector2(i % 2 == 0 ? 0.004f : -0.001f, 0));
            }
            mb.Sub(sub).Extrude(poly, width, W, 0.005f);
            // texture panels on both sides, a base cap and a beavertail where the web of your hand sits
            var q = Quaternion.Euler(angle, 0, 0);
            Vector3 mid = new Vector3(0, topY, topZ - 0.004f) + q * new Vector3(0, -len * 0.5f, 0);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sub(DARK).Box(mid + new Vector3(sx * (width * 0.5f + 0.0006f), 0, 0), new Vector3(0.0012f, len * 0.62f, 0.036f), W, 0.0005f, 1, q);
                for (int r = 0; r < 4; r++) for (int c = 0; c < 3; c++)
                    mb.Sub(sub).Box(mid + q * new Vector3(sx * (width * 0.5f + 0.0012f), -len * 0.24f + r * len * 0.16f, -0.012f + c * 0.012f), new Vector3(0.0012f, 0.0035f, 0.0035f), W, 0.0005f, 1, q);
            }
            Vector3 bot = new Vector3(0, topY, topZ - 0.004f) + q * new Vector3(0, -len - 0.002f, 0);
            mb.Sub(sub).Box(bot, new Vector3(width + 0.003f, 0.006f, 0.054f), W, 0.002f, 1, q);
            mb.Sub(sub).Box(new Vector3(0, topY + 0.001f, topZ - 0.038f), new Vector3(width * 0.85f, 0.008f, 0.022f), W, 0.003f, 1, Quaternion.Euler(-12, 0, 0));
        }

        /// <summary>A cross pin (takedown / trigger pin) showing on both sides.</summary>
        static void PinX(MB mb, float y, float z, float halfW, float r = 0.003f)
        {
            mb.Sub(DARK).Cyl(new Vector3(-halfW - 0.0008f, y, z), new Vector3(halfW + 0.0008f, y, z), r, r, 8, W);
        }

        /// <summary>A sling swivel / loop.</summary>
        static void Swivel(MB mb, Vector3 at)
        {
            mb.Sub(DARK).Tube(at + new Vector3(-0.002f, 0, 0), at + new Vector3(0.002f, 0, 0), 0.008f, 0.005f, 10, W);
        }

        static void Rail(MB mb, Vector3 start, float length, float width = 0.022f)
        {
            mb.Sub(DARK).Box(start + new Vector3(0, 0.004f, length * 0.5f), new Vector3(width, 0.008f, length), W, 0.0015f);
            int slots = Mathf.Max(2, Mathf.RoundToInt(length / 0.01f));
            for (int i = 0; i < slots; i++)
            {
                float z = start.z + (i + 0.5f) * length / slots;
                mb.Sub(DARK).Box(new Vector3(start.x, start.y + 0.0095f, z), new Vector3(width * 1.05f, 0.003f, length / slots * 0.55f), W, 0.0007f);
            }
        }

        static void Barrel(MB mb, Vector3 from, float len, float r, int sub = FIN)
        {
            mb.Sub(sub).Tube(from, from + Vector3.forward * len, r, r * 0.45f, 14, W);
            mb.Sub(DARK).Tube(from + Vector3.forward * (len - 0.004f), from + Vector3.forward * len, r * 1.03f, r * 0.5f, 14, W);   // crowned muzzle
        }

        static MB _device;   // muzzle devices go in their own mesh while a gun is being built
        static void Muzzle(MB mb, Vector3 pos, float r, int kind)
        {
            if (kind != 3 && _device != null) mb = _device;   // (an integral suppressor stays part of the gun)
            switch (kind)
            {
                case 0: // birdcage flash hider
                    mb.Sub(DARK).Tube(pos, pos + Vector3.forward * 0.05f, r * 1.5f, r * 0.9f, 10, W);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * 90f + 45f;
                        var q = Quaternion.Euler(0, 0, ang);
                        mb.Sub(BLK).Box(pos + new Vector3(0, 0, 0.032f) + q * new Vector3(0, r * 1.5f, 0), new Vector3(0.004f, 0.002f, 0.025f), W, 0, 0, q);
                    }
                    break;
                case 1: // AK slant brake
                    mb.Sub(FIN).Cyl(pos, pos + Vector3.forward * 0.045f, r * 1.6f, r * 1.4f, 12, W);
                    break;
                case 2: // big two-port brake
                    mb.Sub(DARK).Box(pos + new Vector3(0, 0, 0.05f), new Vector3(r * 6.5f, r * 3.2f, 0.1f), W, 0.006f, 2);
                    mb.Sub(BLK).Box(pos + new Vector3(0, 0, 0.035f), new Vector3(r * 6.8f, r * 2.2f, 0.022f), W, 0.002f);
                    mb.Sub(BLK).Box(pos + new Vector3(0, 0, 0.07f), new Vector3(r * 6.8f, r * 2.2f, 0.022f), W, 0.002f);
                    break;
                case 3: // suppressor
                    mb.Sub(DARK).Cyl(pos, pos + Vector3.forward * 0.17f, r * 2.6f, r * 2.6f, 18, W);
                    mb.Sub(BLK).Cyl(pos + Vector3.forward * 0.17f, pos + Vector3.forward * 0.175f, r * 2.4f, r * 1.0f, 18, W);
                    break;
                default: // simple crown
                    mb.Sub(FIN).Cyl(pos, pos + Vector3.forward * 0.02f, r * 1.25f, r * 1.25f, 12, W);
                    break;
            }
        }

        static void TriggerGuard(MB mb, float z0, float z1, float yTop, float yBot, int sub = FIN)
        {
            // the guard: bottom bar, a 45-degree corner and the front post - one continuous loop
            float h = yTop - yBot, c = Mathf.Min(0.012f, h * 0.45f);
            mb.Sub(sub).Box(new Vector3(0, yBot, (z0 + z1 - c) * 0.5f), new Vector3(0.012f, 0.005f, z1 - z0 - c), W, 0.002f);
            mb.Sub(sub).Box(new Vector3(0, yBot + c * 0.5f, z1 - c * 0.5f), new Vector3(0.012f, 0.005f, c * 1.45f), W, 0.002f, 1, Quaternion.Euler(-45, 0, 0));
            mb.Sub(sub).Box(new Vector3(0, yBot + c + (h - c) * 0.5f, z1), new Vector3(0.012f, h - c + 0.002f, 0.005f), W, 0.002f);
            mb.Sub(sub).Box(new Vector3(0, yBot + 0.004f, z0 - 0.002f), new Vector3(0.012f, 0.008f, 0.005f), W, 0.002f);   // rear tang up into the frame
            // curved trigger blade
            float tz = z0 + (z1 - z0) * 0.42f;
            mb.Sub(DARK).Box(new Vector3(0, yBot + h * 0.72f, tz), new Vector3(0.006f, h * 0.42f, 0.005f), W, 0.0018f, 1, Quaternion.Euler(4, 0, 0));
            mb.Sub(DARK).Box(new Vector3(0, yBot + h * 0.32f, tz + 0.003f), new Vector3(0.006f, h * 0.42f, 0.005f), W, 0.0018f, 1, Quaternion.Euler(24, 0, 0));
        }

        /// <summary>
        /// Real iron sights: a rear sight with a notch you can see through and a thin front post between two
        /// protective ears, each with a glowing dot. Line them up "equal height": the top of the front post level
        /// with the tops of the rear ears, sitting in the middle of the gap. Returns the height of that sight line.
        /// </summary>
        static float Irons(MB mb, float zRear, float zFront, float line, float rearBase, float frontBase)
        {
            const float notch = 0.007f, gap = 0.0065f, ear = 0.0065f;
            float nb = line - notch;
            if (nb > rearBase + 0.0005f) mb.Sub(DARK).Box(new Vector3(0, (rearBase + nb) * 0.5f, zRear), new Vector3(gap + ear * 2f, nb - rearBase, 0.01f), W, 0.001f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (gap + ear) * 0.5f;
                mb.Sub(DARK).Box(new Vector3(x, nb + notch * 0.5f, zRear), new Vector3(ear, notch, 0.01f), W, 0.0008f);
                mb.Sub(DOT).Box(new Vector3(x, line - 0.0022f, zRear - 0.0052f), new Vector3(0.0016f, 0.0016f, 0.001f), W);
            }
            float pb = Mathf.Max(frontBase, line - 0.014f);
            if (pb > frontBase + 0.0005f) mb.Sub(DARK).Box(new Vector3(0, (frontBase + pb) * 0.5f, zFront), new Vector3(0.022f, pb - frontBase, 0.012f), W, 0.002f);
            mb.Sub(DARK).Box(new Vector3(0, pb + 0.003f, zFront), new Vector3(0.022f, 0.006f, 0.012f), W, 0.0015f);              // post base
            mb.Sub(DARK).Box(new Vector3(0, (pb + line) * 0.5f, zFront), new Vector3(0.0026f, line - pb, 0.004f), W);             // the post
            mb.Sub(DOT).Box(new Vector3(0, line - 0.0016f, zFront - 0.0022f), new Vector3(0.0026f, 0.0028f, 0.001f), W);
            foreach (float sx in new[] { -1f, 1f })                                                                                // ears guarding the post
                mb.Sub(DARK).Box(new Vector3(sx * 0.0095f, (pb + line + 0.003f) * 0.5f, zFront), new Vector3(0.003f, line + 0.003f - pb, 0.01f), W, 0.0006f);
            return line;
        }

        static float IronSights(MB mb, float zRear, float zFront, float y, bool folding)
        {
            float h = folding ? 0.022f : 0.012f;
            return Irons(mb, zRear, zFront, y + h + 0.004f, y, y);
        }

        static void Stock(MB mb, int sub, float zFront, float zBack, float yTop, float yBot, float dropBack = 0f, float thickness = 0.04f, bool skeleton = false)
        {
            var p = new List<Vector2>
            {
                new Vector2(zFront, yTop), new Vector2(zFront, yBot + 0.01f),
                new Vector2(zBack + 0.02f, yBot - dropBack), new Vector2(zBack, yBot - dropBack - 0.005f),
                new Vector2(zBack, yTop - dropBack * 0.4f + 0.005f), new Vector2(zBack + 0.03f, yTop - dropBack * 0.3f),
            };
            mb.Sub(sub).Extrude(p, thickness, W, 0.007f);
            float padY = (yTop + yBot) * 0.5f - dropBack * 0.6f, padH = (yTop - yBot) + dropBack * 0.4f;
            mb.Sub(BLK).Box(new Vector3(0, padY, zBack - 0.006f), new Vector3(thickness * 1.05f, padH, 0.014f), W, 0.004f);
            for (int i = 0; i < 4; i++) mb.Sub(DARK).Box(new Vector3(0, padY - padH * 0.36f + i * padH * 0.24f, zBack - 0.0135f), new Vector3(thickness * 0.9f, 0.003f, 0.002f), W, 0.0008f);   // rubber pad ridges
            Swivel(mb, new Vector3(0, yBot - dropBack + 0.002f, zBack + 0.07f));
            if (skeleton) mb.Sub(DARK).Box(new Vector3(0, (yTop + yBot) * 0.5f, (zFront + zBack) * 0.5f), new Vector3(thickness * 1.02f, (yTop - yBot) * 0.35f, (zFront - zBack) * 0.45f), W, 0.004f);
        }

        // ------------------------------------------------------------------ styles
        static void Pistol(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            float slideLen = 0.16f + d.barrel * 0.4f + (d.cal == Caliber.C50AE ? 0.04f : 0);
            float zF = 0.03f + slideLen * 0.75f, zB = zF - slideLen;
            float slideH = d.cal == Caliber.C50AE ? 0.04f : 0.032f;
            rig.boreY = 0.062f;
            // frame
            // frame: dust cover up front, then a grip raked back ~16 degrees with a curved front strap, a rounded
            // backstrap swelling into the beavertail, and a flared base
            var frame = new List<Vector2>
            {
                new Vector2(zF - 0.01f, 0.047f), new Vector2(zF - 0.01f, 0.03f), new Vector2(0.065f, 0.028f),
                new Vector2(0.034f, 0.028f), new Vector2(0.027f, 0.018f), new Vector2(0.025f, -0.01f), new Vector2(0.02f, -0.04f),
                new Vector2(0.014f, -0.068f), new Vector2(0.016f, -0.079f), new Vector2(-0.034f, -0.083f),
                new Vector2(-0.038f, -0.06f), new Vector2(-0.044f, -0.025f), new Vector2(-0.048f, 0.005f), new Vector2(-0.05f, 0.024f),
                new Vector2(-0.062f, 0.034f), new Vector2(-0.063f, 0.041f), new Vector2(-0.045f, 0.047f)
            };
            mb.Sub(d.style == GunStyle.Pistol && d.furniture == Finish.Wood ? FIN : BLK).Extrude(frame, 0.028f, W, 0.004f);
            if (d.furniture == Finish.Wood || d.furniture == Finish.Fde || d.furniture == Finish.Od)
            {
                // grip panels
                var panel = new List<Vector2> { new Vector2(0.02f, 0.012f), new Vector2(0.01f, -0.066f), new Vector2(-0.03f, -0.07f), new Vector2(-0.042f, 0.014f) };
                mb.Sub(FUR).Extrude(panel, 0.032f, W, 0.004f);
            }
            TriggerGuard(mb, 0.025f, 0.07f, 0.03f, -0.003f, BLK);
            // accessory rail under frame, with its cross slots
            mb.Sub(BLK).Box(new Vector3(0, 0.022f, zF - 0.035f), new Vector3(0.02f, 0.01f, 0.05f), W, 0.002f);
            for (int i = 0; i < 3; i++) mb.Sub(DARK).Box(new Vector3(0, 0.0168f, zF - 0.05f + i * 0.014f), new Vector3(0.021f, 0.002f, 0.004f), W);
            // controls: slide stop and takedown lever on the left, mag release button, recoil spring guide rod
            mb.Sub(DARK).Box(new Vector3(-0.0145f, 0.04f, 0.035f), new Vector3(0.003f, 0.006f, 0.03f), W, 0.001f);
            mb.Sub(DARK).Box(new Vector3(-0.0145f, 0.038f, 0.065f), new Vector3(0.003f, 0.007f, 0.007f), W, 0.001f);
            mb.Sub(DARK).Cyl(new Vector3(-0.0145f, 0.018f, 0.026f), new Vector3(-0.0158f, 0.018f, 0.026f), 0.0045f, 0.0045f, 10, W);
            mb.Sub(DARK).Cyl(new Vector3(0, 0.038f, zF - 0.012f), new Vector3(0, 0.038f, zF - 0.002f), 0.0035f, 0.0035f, 8, W);
            PinX(mb, 0.036f, 0.002f, 0.014f, 0.0022f);
            // grip texture and a flared magwell
            foreach (float sx in new[] { -1f, 1f })
                for (int r = 0; r < 5; r++) for (int c = 0; c < 3; c++)
                    mb.Sub(DARK).Box(new Vector3(sx * 0.0148f, -0.06f + r * 0.016f, -0.02f + c * 0.012f - r * 0.002f), new Vector3(0.001f, 0.004f, 0.004f), W);
            mb.Sub(BLK).Box(new Vector3(0, -0.077f, -0.003f), new Vector3(0.032f, 0.006f, 0.06f), W, 0.002f, 1, Quaternion.Euler(-14, 0, 0));
            if (d.cal == Caliber.C45)
            {
                // the classic .45: hammer and grip safety at the back
                mb.Sub(DARK).Box(new Vector3(0, 0.058f, -0.05f), new Vector3(0.007f, 0.016f, 0.01f), W, 0.002f, 1, Quaternion.Euler(-25, 0, 0));
                mb.Sub(FIN).Box(new Vector3(0, 0.025f, -0.045f), new Vector3(0.02f, 0.03f, 0.008f), W, 0.002f, 1, Quaternion.Euler(-14, 0, 0));
            }
            // barrel visible at front
            Barrel(mb, new Vector3(0, rig.boreY, zF - 0.01f), 0.012f, 0.0075f, ACC);
            // slide (separate animated object)
            var sb = new MB(3);
            float sy = 0.047f + slideH * 0.5f;
            sb.Sub(0).Box(new Vector3(0, sy, (zF + zB) * 0.5f), new Vector3(0.027f, slideH, slideLen), W, 0.004f, 2);
            for (int i = 0; i < 7; i++) sb.Sub(1).Box(new Vector3(0, sy, zB + 0.01f + i * 0.0055f), new Vector3(0.028f, slideH * 0.8f, 0.0022f), W);
            sb.Sub(1).Box(new Vector3(0.008f, sy + slideH * 0.32f, zB + slideLen * 0.55f), new Vector3(0.014f, 0.006f, 0.035f), W, 0.001f); // ejection port
            sb.Sub(1).Box(new Vector3(0, sy + slideH * 0.5f + 0.004f, zF - 0.008f), new Vector3(0.003f, 0.008f, 0.005f), W, 0.0005f); // front sight
            sb.Sub(1).Box(new Vector3(-0.007f, sy + slideH * 0.5f + 0.004f, zB + 0.012f), new Vector3(0.005f, 0.008f, 0.008f), W, 0.001f);
            sb.Sub(1).Box(new Vector3(0.007f, sy + slideH * 0.5f + 0.004f, zB + 0.012f), new Vector3(0.005f, 0.008f, 0.008f), W, 0.001f);
            float st = sy + slideH * 0.5f;   // three-dot sights: the front dot sits between the two rear ones
            sb.Sub(2).Box(new Vector3(0, st + 0.0055f, zF - 0.0106f), new Vector3(0.0022f, 0.0022f, 0.001f), W);
            sb.Sub(2).Box(new Vector3(-0.007f, st + 0.0055f, zB + 0.0078f), new Vector3(0.0022f, 0.0022f, 0.001f), W);
            sb.Sub(2).Box(new Vector3(0.007f, st + 0.0055f, zB + 0.0078f), new Vector3(0.0022f, 0.0022f, 0.001f), W);
            // slide detail: chamfered top edges, front cocking serrations, a real ejection port with the barrel hood showing
            foreach (float sx in new[] { -1f, 1f })
            {
                sb.Sub(0).Box(new Vector3(sx * 0.0115f, sy + slideH * 0.44f, (zF + zB) * 0.5f), new Vector3(0.006f, 0.006f, slideLen * 0.98f), W, 0.002f, 1, Quaternion.Euler(0, 0, sx * 35f));
                for (int i = 0; i < 5; i++) sb.Sub(1).Box(new Vector3(sx * 0.0137f, sy - slideH * 0.05f, zF - 0.018f - i * 0.005f), new Vector3(0.001f, slideH * 0.6f, 0.0022f), W);
                for (int i = 0; i < 7; i++) sb.Sub(1).Box(new Vector3(sx * 0.0137f, sy - slideH * 0.05f, zB + 0.01f + i * 0.0055f), new Vector3(0.001f, slideH * 0.7f, 0.0022f), W);
            }
            sb.Sub(1).Box(new Vector3(0.004f, sy + slideH * 0.42f, zB + slideLen * 0.57f), new Vector3(0.02f, 0.004f, slideLen * 0.25f), W, 0.0008f);   // ejection port
            sb.Sub(0).Box(new Vector3(0.002f, sy + slideH * 0.38f, zB + slideLen * 0.57f), new Vector3(0.012f, 0.003f, slideLen * 0.22f), W, 0.0008f);   // barrel hood in the port
            sb.Sub(1).Cyl(new Vector3(0, rig.boreY, zF - 0.0005f), new Vector3(0, rig.boreY, zF + 0.0015f), 0.0045f, 0.0045f, 10, W);                         // bore
            var slide = sb.Build("Slide", rig.root.transform, mats[FIN], mats[DARK], SightDot);
            rig.slide = slide.transform;
            rig.slideTravel = new Vector3(0, 0, -slideLen * 0.28f);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, zF + 0.004f));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.012f, sy + 0.01f, zB + slideLen * 0.55f));
            // the mag goes right up inside the grip (only its base plate shows under it): the magwell is at the top of the
            // grip, square to it. It used to sit under the grip, tipped forward - the mag hung off the bottom of the gun
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.018f, -0.009f), Quaternion.Euler(3, 0, 0));
            rig.magEntry = -0.13f;
            rig.magStyle = MagStyle.PistolBox;
            rig.under = Anchor(rig.root, "UnderRail", new Vector3(0, 0.017f, zF - 0.035f), Quaternion.Euler(0, 0, -90));
            if (d.optics)
            {
                var r = Anchor(slide, "Rail", new Vector3(0, sy + slideH * 0.5f, zB + slideLen * 0.36f));
                rig.rail = r;
            }
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, sy + slideH * 0.5f + 0.008f, zB));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0, -0.04f));
            if (d.suppressed) Muzzle(mb, new Vector3(0, rig.boreY, zF), 0.008f, 3);
        }

        static void Revolver(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.058f;
            float bl = 0.08f + d.barrel * 0.8f;
            var frame = new List<Vector2> { new Vector2(0.075f, 0.075f), new Vector2(0.075f, 0.03f), new Vector2(0.03f, 0.028f), new Vector2(-0.005f, 0.028f), new Vector2(-0.03f, 0.045f), new Vector2(-0.035f, 0.075f), new Vector2(0.0f, 0.082f) };
            mb.Sub(FIN).Extrude(frame, 0.026f, W, 0.003f);
            // grip (wood)
            var grip = new List<Vector2> { new Vector2(0.0f, 0.032f), new Vector2(0.01f, -0.06f), new Vector2(-0.03f, -0.072f), new Vector2(-0.042f, 0.035f), new Vector2(-0.03f, 0.05f) };
            mb.Sub(FUR).Extrude(grip, 0.032f, W, 0.006f);
            TriggerGuard(mb, 0.0f, 0.04f, 0.028f, -0.004f, FIN);
            // cylinder with flutes
            var cylC = new Vector3(0, 0.052f, 0.03f);
            mb.Sub(FIN).Cyl(cylC + Vector3.back * 0.022f, cylC + Vector3.forward * 0.022f, 0.02f, 0.02f, 18, W);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                mb.Sub(DARK).Box(cylC + new Vector3(Mathf.Cos(a) * 0.019f, Mathf.Sin(a) * 0.019f, 0), new Vector3(0.006f, 0.006f, 0.032f), W, 0.002f, 1, Quaternion.Euler(0, 0, i * 60f));
            }
            // barrel + rib
            Barrel(mb, new Vector3(0, rig.boreY + 0.008f, 0.075f), bl, 0.009f, FIN);
            mb.Sub(FIN).Box(new Vector3(0, rig.boreY + 0.02f, 0.075f + bl * 0.5f), new Vector3(0.008f, 0.008f, bl), W, 0.002f);
            mb.Sub(FIN).Box(new Vector3(0, rig.boreY - 0.004f, 0.075f + bl * 0.5f), new Vector3(0.012f, 0.01f, bl * 0.9f), W, 0.002f); // ejector shroud
            Irons(mb, -0.005f, 0.065f + bl, 0.091f, 0.082f, rig.boreY + 0.024f);
            // chambers in the cylinder face, ejector rod, cylinder latch, top strap, grip medallion
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                mb.Sub(DARK).Cyl(cylC + new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, 0.0215f), cylC + new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, 0.0235f), 0.0045f, 0.0045f, 8, W);
            }
            mb.Sub(FIN).Cyl(new Vector3(0, cylC.y - 0.014f, 0.055f), new Vector3(0, cylC.y - 0.014f, 0.075f + bl * 0.55f), 0.0035f, 0.0035f, 8, W);
            mb.Sub(DARK).Box(new Vector3(-0.0135f, 0.052f, -0.012f), new Vector3(0.003f, 0.008f, 0.012f), W, 0.0015f);
            mb.Sub(FIN).Box(new Vector3(0, 0.077f, 0.03f), new Vector3(0.014f, 0.006f, 0.05f), W, 0.002f);
            foreach (float sx in new[] { -1f, 1f }) mb.Sub(ACC).Cyl(new Vector3(sx * 0.016f, -0.01f, -0.016f), new Vector3(sx * 0.0172f, -0.01f, -0.016f), 0.006f, 0.006f, 12, W);
            // hammer
            mb.Sub(FIN).Box(new Vector3(0, 0.071f, -0.036f), new Vector3(0.008f, 0.016f, 0.012f), W, 0.002f, 1, Quaternion.Euler(-30, 0, 0));   // low and back: below the rear sight's notch, so it never blocks the sight picture
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY + 0.008f, 0.077f + bl));
            rig.ejectPort = Anchor(rig.root, "Eject", cylC + new Vector3(0.02f, 0, 0));
            rig.magwell = Anchor(rig.root, "Magwell", cylC + new Vector3(0.035f, 0, -0.03f), Quaternion.Euler(0, 0, 90));
            rig.magStyle = MagStyle.Speedloader;
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, rig.boreY + 0.024f, 0.09f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.091f, -0.03f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0, -0.04f));
        }

        static void LowerAR(MB mb, float zMagwell, float magDepth = 0.04f)
        {
            mb.Sub(FIN).Box(new Vector3(0, 0.042f, 0.035f), new Vector3(0.03f, 0.035f, 0.2f), W, 0.004f, 2);
            mb.Sub(FIN).Box(new Vector3(0, 0.03f - magDepth * 0.4f, zMagwell), new Vector3(0.034f, magDepth + 0.02f, 0.072f), W, 0.004f, 2);
            TriggerGuard(mb, 0.02f, 0.06f, 0.025f, 0.0f, FIN);
        }

        static void AR(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.085f;
            float hg = Mathf.Clamp(d.barrel * 0.62f, 0.18f, 0.33f);
            bool dmr = d.cls == GunClass.DMR;
            PistolGrip(mb, BLK, 20f, 0.095f, 0.032f, 0.03f, 0.0f);
            LowerAR(mb, 0.095f);
            // upper receiver
            mb.Sub(FIN).Box(new Vector3(0, 0.08f, 0.05f), new Vector3(0.032f, 0.04f, 0.27f), W, 0.004f, 2);
            mb.Sub(FIN).Box(new Vector3(0.017f, 0.078f, 0.06f), new Vector3(0.006f, 0.012f, 0.05f), W, 0.001f); // dust cover
            mb.Sub(DARK).Box(new Vector3(0.016f, 0.074f, 0.12f), new Vector3(0.006f, 0.008f, 0.012f), W, 0.002f); // forward assist area
            // receiver detail: ejection port, hinged dust cover, brass deflector, forward assist plunger, bolt catch,
            // mag release, selector, takedown pins, flared magwell and a fence round the mag release
            mb.Sub(DARK).Box(new Vector3(0.0163f, 0.083f, 0.06f), new Vector3(0.002f, 0.014f, 0.05f), W, 0.0006f);
            mb.Sub(FIN).Box(new Vector3(0.0185f, 0.076f, 0.06f), new Vector3(0.002f, 0.012f, 0.052f), W, 0.0006f);
            mb.Sub(DARK).Cyl(new Vector3(0.0195f, 0.07f, 0.034f), new Vector3(0.0195f, 0.07f, 0.088f), 0.0012f, 0.0012f, 6, W);
            mb.Sub(FIN).Box(new Vector3(0.019f, 0.09f, 0.025f), new Vector3(0.008f, 0.012f, 0.014f), W, 0.003f);
            mb.Sub(FIN).Cyl(new Vector3(0.018f, 0.088f, -0.005f), new Vector3(0.026f, 0.093f, -0.03f), 0.006f, 0.006f, 10, W);
            mb.Sub(DARK).Box(new Vector3(-0.0175f, 0.048f, 0.075f), new Vector3(0.003f, 0.018f, 0.009f), W, 0.001f);
            mb.Sub(DARK).Cyl(new Vector3(0.017f, 0.04f, 0.06f), new Vector3(0.0195f, 0.04f, 0.06f), 0.0045f, 0.0045f, 10, W);
            mb.Sub(FIN).Box(new Vector3(0.0175f, 0.036f, 0.06f), new Vector3(0.002f, 0.014f, 0.016f), W, 0.001f);
            mb.Sub(DARK).Box(new Vector3(-0.0168f, 0.05f, -0.01f), new Vector3(0.003f, 0.004f, 0.018f), W, 0.001f, 1, Quaternion.Euler(30, 0, 0));
            PinX(mb, 0.06f, -0.04f, 0.016f); PinX(mb, 0.058f, 0.115f, 0.016f); PinX(mb, 0.035f, 0.025f, 0.015f, 0.002f); PinX(mb, 0.035f, 0.038f, 0.015f, 0.002f);
            mb.Sub(FIN).Box(new Vector3(0, 0.003f, 0.095f), new Vector3(0.04f, 0.008f, 0.08f), W, 0.003f, 1, Quaternion.Euler(6, 0, 0));
            Rail(mb, new Vector3(0, 0.1f, -0.085f), 0.265f);
            // charging handle (animated)
            var ch = new MB(1);
            ch.Box(new Vector3(0, 0.098f, -0.085f), new Vector3(0.03f, 0.008f, 0.015f), W, 0.002f);
            ch.Box(new Vector3(0, 0.094f, -0.06f), new Vector3(0.008f, 0.006f, 0.05f), W, 0.001f);
            rig.slide = ch.Build("ChargingHandle", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.05f);
            // buffer + stock
            mb.Sub(DARK).Cyl(new Vector3(0, 0.08f, -0.085f), new Vector3(0, 0.08f, -0.3f), 0.0145f, 0.0145f, 14, W);
            mb.Sub(DARK).Cyl(new Vector3(0, 0.08f, -0.09f), new Vector3(0, 0.08f, -0.1f), 0.019f, 0.019f, 8, W);    // castle nut
            mb.Sub(FIN).Box(new Vector3(0, 0.068f, -0.096f), new Vector3(0.03f, 0.03f, 0.006f), W, 0.002f);         // end plate / sling point
            for (int i = 0; i < 5; i++) mb.Sub(BLK).Box(new Vector3(0, 0.066f, -0.2f - i * 0.016f), new Vector3(0.006f, 0.004f, 0.006f), W, 0.001f);   // adjustment holes
            if (d.furniture == Finish.Wood) Stock(mb, FUR, -0.16f, -0.36f, 0.1f, 0.02f, 0.01f, 0.042f);
            else
            {
                var sp = new List<Vector2> { new Vector2(-0.17f, 0.1f), new Vector2(-0.17f, 0.065f), new Vector2(-0.26f, 0.025f), new Vector2(-0.315f, 0.015f), new Vector2(-0.33f, 0.025f), new Vector2(-0.33f, 0.115f), new Vector2(-0.29f, 0.118f) };
                mb.Sub(FUR).Extrude(sp, 0.044f, W, 0.006f);
                mb.Sub(BLK).Box(new Vector3(0, 0.068f, -0.336f), new Vector3(0.046f, 0.11f, 0.014f), W, 0.004f);
                if (dmr) mb.Sub(FUR).Box(new Vector3(0, 0.12f, -0.25f), new Vector3(0.03f, 0.016f, 0.08f), W, 0.004f); // cheek riser
            }
            // handguard
            float hz = 0.185f;
            mb.Sub(d.finish == Finish.Black ? DARK : FIN).Box(new Vector3(0, 0.08f, hz + hg * 0.5f), new Vector3(0.046f, 0.05f, hg), W, 0.012f, 2);
            for (int i = 0; i < Mathf.FloorToInt(hg / 0.05f); i++)
            {
                float z = hz + 0.03f + i * 0.05f;
                mb.Sub(BLK).Box(new Vector3(0.023f, 0.075f, z), new Vector3(0.003f, 0.012f, 0.03f), W, 0.002f);
                mb.Sub(BLK).Box(new Vector3(-0.023f, 0.075f, z), new Vector3(0.003f, 0.012f, 0.03f), W, 0.002f);
                mb.Sub(BLK).Box(new Vector3(0, 0.054f, z), new Vector3(0.012f, 0.003f, 0.03f), W, 0.002f);
            }
            Rail(mb, new Vector3(0, 0.105f, hz), hg - 0.01f, 0.02f);
            mb.Sub(DARK).Box(new Vector3(0, 0.08f, hz + 0.004f), new Vector3(0.05f, 0.054f, 0.008f), W, 0.004f);   // barrel nut / handguard end
            mb.Sub(DARK).Cyl(new Vector3(0.024f, 0.068f, hz + 0.03f), new Vector3(0.027f, 0.068f, hz + 0.03f), 0.005f, 0.005f, 10, W);   // QD sling cup
            float bz = hz + hg;
            float bl = Mathf.Max(0.04f, d.barrel - hg + 0.05f);
            Barrel(mb, new Vector3(0, rig.boreY, bz), bl, 0.0085f, DARK);
            mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY, bz - 0.01f), new Vector3(0, rig.boreY, bz + 0.03f), 0.014f, 0.012f, 12, W); // gas block
            Muzzle(mb, new Vector3(0, rig.boreY, bz + bl), 0.0085f, d.suppressed ? 3 : 0);
            float arLine = IronSights(mb, -0.07f, hz + hg - 0.02f, 0.11f, true);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, bz + bl + (d.suppressed ? 0.17f : 0.05f)));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.02f, 0.082f, 0.06f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.0f, 0.095f), Quaternion.Euler(6, 0, 0));
            rig.magStyle = d.cal == Caliber.C308 ? MagStyle.Straight : MagStyle.Stanag;
            if (d.magSize > 30) rig.magStyle = MagStyle.LongBox;
            rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.11f, -0.005f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.052f, hz + hg * 0.55f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, arLine, -0.07f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.07f, -0.34f));
        }

        static void AK(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.07f;
            bool lmg = d.cls == GunClass.LMG;
            PistolGrip(mb, d.furniture == Finish.Wood ? BLK : FUR, 16f, 0.095f, 0.03f, 0.03f, -0.005f);
            // receiver with dust cover
            mb.Sub(FIN).Box(new Vector3(0, 0.055f, 0.05f), new Vector3(0.036f, 0.05f, 0.29f), W, 0.004f, 2);
            mb.Sub(FIN).Box(new Vector3(0, 0.085f, 0.04f), new Vector3(0.034f, 0.02f, 0.25f), W, 0.012f, 2);
            mb.Sub(FIN).Box(new Vector3(0.019f, 0.06f, 0.1f), new Vector3(0.003f, 0.012f, 0.08f), W, 0.001f); // selector lever
            // stamped receiver: rivets, the dust cover's rib and retaining button, rear trunnion, mag catch
            foreach (float sx in new[] { -1f, 1f })
                foreach (var rz in new[] { -0.075f, -0.06f, 0.155f, 0.17f, 0.005f, 0.02f })
                    mb.Sub(DARK).Sphere(new Vector3(sx * 0.0182f, rz > 0.1f || rz < -0.05f ? 0.045f : 0.038f, rz), Vector3.one * 0.0022f, 6, W);
            mb.Sub(FIN).Box(new Vector3(0, 0.0965f, 0.04f), new Vector3(0.012f, 0.004f, 0.24f), W, 0.0015f);
            mb.Sub(DARK).Cyl(new Vector3(0, 0.088f, -0.081f), new Vector3(0, 0.088f, -0.088f), 0.005f, 0.005f, 8, W);
            mb.Sub(FIN).Box(new Vector3(0, 0.03f, 0.075f), new Vector3(0.012f, 0.012f, 0.008f), W, 0.002f);
            PinX(mb, 0.042f, 0.03f, 0.018f, 0.0022f); PinX(mb, 0.042f, 0.045f, 0.018f, 0.0022f);
            TriggerGuard(mb, 0.015f, 0.055f, 0.03f, 0.0f, FIN);
            // bolt carrier handle (animated)
            var ch = new MB(1);
            ch.Box(new Vector3(0.024f, 0.072f, 0.135f), new Vector3(0.016f, 0.01f, 0.012f), W, 0.003f);
            rig.slide = ch.Build("Bolt", rig.root.transform, mats[FIN]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.11f);
            // stock
            if (d.furniture == Finish.Wood) Stock(mb, FUR, -0.095f, -0.4f, 0.075f, 0.03f, 0.05f, 0.04f);
            else if (d.furniture == Finish.Gold) { Stock(mb, BLK, -0.095f, -0.38f, 0.08f, 0.035f, 0.04f, 0.04f, true); }
            else Stock(mb, FUR, -0.095f, -0.38f, 0.08f, 0.035f, 0.04f, 0.04f, true);
            // handguard lower + gas tube upper
            float hz = 0.195f;
            mb.Sub(FUR).Box(new Vector3(0, 0.06f, hz + 0.08f), new Vector3(0.046f, 0.04f, 0.17f), W, 0.012f, 2);
            mb.Sub(FUR).Box(new Vector3(0, 0.094f, hz + 0.07f), new Vector3(0.032f, 0.022f, 0.15f), W, 0.01f, 2);
            mb.Sub(FIN).Cyl(new Vector3(0, 0.094f, hz + 0.15f), new Vector3(0, 0.094f, hz + 0.2f), 0.009f, 0.009f, 10, W);
            float bl = Mathf.Max(0.12f, d.barrel - 0.05f);
            Barrel(mb, new Vector3(0, rig.boreY, hz + 0.16f), bl, 0.0095f, FIN);
            mb.Sub(FIN).Box(new Vector3(0, 0.083f, hz + 0.2f), new Vector3(0.018f, 0.04f, 0.022f), W, 0.004f); // gas block
            float fz = hz + 0.16f + bl - 0.04f;
            mb.Sub(FIN).Box(new Vector3(0, 0.085f, fz), new Vector3(0.016f, 0.04f, 0.018f), W, 0.004f); // front sight block
            mb.Sub(FIN).Tube(new Vector3(0, 0.107f, fz - 0.006f), new Vector3(0, 0.107f, fz + 0.006f), 0.013f, 0.011f, 14, W);   // hooded front sight
            mb.Sub(FIN).Cyl(new Vector3(0, rig.boreY - 0.014f, hz + 0.17f), new Vector3(0, rig.boreY - 0.014f, fz + 0.01f), 0.0028f, 0.0028f, 6, W);   // cleaning rod
            for (int i = 0; i < 3; i++) mb.Sub(DARK).Box(new Vector3(0.016f, 0.094f, hz + 0.03f + i * 0.04f), new Vector3(0.003f, 0.006f, 0.014f), W, 0.001f);   // vents in the upper handguard
            mb.Sub(FIN).Box(new Vector3(0, 0.06f, hz + 0.165f), new Vector3(0.05f, 0.044f, 0.008f), W, 0.002f);                 // handguard retainer
            Swivel(mb, new Vector3(0, 0.044f, hz + 0.165f));
            mb.Sub(FIN).Box(new Vector3(0, 0.094f, 0.17f), new Vector3(0.024f, 0.008f, 0.04f), W, 0.003f); // rear sight leaf base
            Irons(mb, 0.165f, fz, 0.116f, 0.098f, 0.105f);
            Muzzle(mb, new Vector3(0, rig.boreY, hz + 0.16f + bl), 0.0095f, d.suppressed ? 3 : 1);
            if (d.optics) Rail(mb, new Vector3(0, 0.095f, -0.07f), 0.19f);
            if (lmg)
            {
                // bipod folded under barrel
                mb.Sub(DARK).Box(new Vector3(0.012f, 0.055f, fz - 0.12f), new Vector3(0.006f, 0.006f, 0.22f), W, 0.002f);
                mb.Sub(DARK).Box(new Vector3(-0.012f, 0.055f, fz - 0.12f), new Vector3(0.006f, 0.006f, 0.22f), W, 0.002f);
            }
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, hz + 0.16f + bl + (d.suppressed ? 0.17f : 0.045f)));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.02f, 0.075f, 0.07f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.05f, 0.1f), Quaternion.Euler(10, 0, 0));   // the mag's lip goes 2 cm up inside the receiver (it stopped at its underside)
            rig.magStyle = d.drum ? MagStyle.Drum : MagStyle.Banana;
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.105f, 0.0f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.04f, hz + 0.08f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.116f, 0.17f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.035f, -0.4f));
        }

        static void Battle(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            AR(d, rig, mb, mats);
            // extra bulk: taller receiver and a beefier handguard
            mb.Sub(FIN).Box(new Vector3(0, 0.07f, 0.08f), new Vector3(0.036f, 0.03f, 0.2f), W, 0.004f, 2);
            if (d.furniture == Finish.Wood)
            {
                mb.Sub(FUR).Box(new Vector3(0, 0.07f, 0.3f), new Vector3(0.05f, 0.045f, 0.22f), W, 0.014f, 2);
                Stock(mb, FUR, -0.08f, -0.42f, 0.09f, 0.0f, 0.05f, 0.045f);
            }
            rig.magStyle = MagStyle.Straight;
        }

        static void SmgBox(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.07f;
            PistolGrip(mb, BLK, 15f, 0.09f, 0.03f, 0.03f, 0.0f);
            mb.Sub(FIN).Box(new Vector3(0, 0.068f, 0.06f), new Vector3(0.034f, 0.05f, 0.3f), W, 0.01f, 2);
            mb.Sub(FIN).Cyl(new Vector3(0, 0.082f, 0.2f), new Vector3(0, 0.082f, 0.24f), 0.012f, 0.012f, 10, W); // cocking tube
            TriggerGuard(mb, 0.015f, 0.055f, 0.03f, 0.0f, BLK);
            mb.Sub(FIN).Box(new Vector3(0, 0.045f, 0.105f), new Vector3(0.026f, 0.04f, 0.04f), W, 0.004f); // magwell
            mb.Sub(FUR).Box(new Vector3(0, 0.062f, 0.24f), new Vector3(0.044f, 0.044f, 0.1f), W, 0.012f, 2); // handguard
            // round-topped receiver, stamped ribs, paddle mag release, hooded front sight ring
            mb.Sub(FIN).Cyl(new Vector3(0, 0.085f, -0.09f), new Vector3(0, 0.085f, 0.21f), 0.0175f, 0.0175f, 16, W);
            foreach (float sx in new[] { -1f, 1f }) mb.Sub(DARK).Box(new Vector3(sx * 0.0172f, 0.06f, 0.06f), new Vector3(0.0015f, 0.004f, 0.22f), W, 0.0006f);
            for (int i = 0; i < 6; i++) mb.Sub(DARK).Box(new Vector3(0, 0.036f, 0.205f + i * 0.014f), new Vector3(0.045f, 0.003f, 0.004f), W, 0.001f);
            mb.Sub(DARK).Box(new Vector3(0, 0.035f, 0.083f), new Vector3(0.014f, 0.004f, 0.012f), W, 0.001f, 1, Quaternion.Euler(-20, 0, 0));
            mb.Sub(FIN).Tube(new Vector3(0, 0.108f, 0.262f), new Vector3(0, 0.108f, 0.276f), 0.012f, 0.0095f, 14, W);
            PinX(mb, 0.05f, -0.07f, 0.017f); PinX(mb, 0.045f, 0.03f, 0.016f);
            var ch = new MB(1);
            ch.Box(new Vector3(-0.022f, 0.082f, 0.23f), new Vector3(0.012f, 0.012f, 0.01f), W, 0.003f);
            rig.slide = ch.Build("CockingHandle", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.06f);
            // collapsible stock rods
            mb.Sub(DARK).Cyl(new Vector3(0.012f, 0.07f, -0.09f), new Vector3(0.012f, 0.07f, -0.27f), 0.004f, 0.004f, 8, W);
            mb.Sub(DARK).Cyl(new Vector3(-0.012f, 0.07f, -0.09f), new Vector3(-0.012f, 0.07f, -0.27f), 0.004f, 0.004f, 8, W);
            mb.Sub(BLK).Box(new Vector3(0, 0.055f, -0.275f), new Vector3(0.04f, 0.08f, 0.016f), W, 0.004f);
            float bz = 0.29f;
            Barrel(mb, new Vector3(0, rig.boreY, bz), 0.04f, 0.008f, DARK);
            Muzzle(mb, new Vector3(0, rig.boreY, bz + 0.04f), 0.008f, d.suppressed ? 3 : 4);
            if (d.suppressed) mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY, 0.18f), new Vector3(0, rig.boreY, bz + 0.2f), 0.02f, 0.02f, 16, W);
            Irons(mb, -0.06f, 0.27f, 0.116f, 0.093f, 0.095f);
            if (d.optics) Rail(mb, new Vector3(0, 0.093f, -0.04f), 0.15f);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, bz + (d.suppressed ? 0.21f : 0.06f)));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.02f, 0.075f, 0.06f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.045f, 0.105f), Quaternion.Euler(12, 0, 0));   // up inside the magwell housing
            rig.magStyle = d.cal == Caliber.C45 ? MagStyle.SmgStick : MagStyle.SmgCurved;   // .45: a slim straight stick, not the big 7.62 rifle mag
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.102f, 0.02f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.038f, 0.24f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.116f, -0.06f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.055f, -0.28f));
        }

        /// <summary>Thompson: wooden butt stock and pistol grip, long milled receiver, finned barrel, Cutts compensator,
        /// vertical wooden foregrip, top cocking knob and a drum.</summary>
        static void Tommy(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.072f;
            PistolGrip(mb, FUR, 12f, 0.1f, 0.034f, 0.03f, 0.0f);
            // receiver: long, squared, with the rounded top of the frame
            mb.Sub(FIN).Box(new Vector3(0, 0.07f, 0.07f), new Vector3(0.042f, 0.052f, 0.3f), W, 0.008f, 2);
            mb.Sub(FIN).Box(new Vector3(0, 0.035f, 0.03f), new Vector3(0.036f, 0.03f, 0.2f), W, 0.006f);   // lower frame
            TriggerGuard(mb, 0.0f, 0.06f, 0.025f, -0.01f, FIN);
            mb.Sub(FIN).Box(new Vector3(0, 0.03f, 0.13f), new Vector3(0.03f, 0.03f, 0.05f), W, 0.004f);   // magwell
            // top cocking knob in its slot
            mb.Sub(DARK).Box(new Vector3(0, 0.098f, 0.07f), new Vector3(0.008f, 0.004f, 0.12f), W, 0.001f);
            var ch = new MB(1);
            ch.Sphere(new Vector3(0, 0.106f, 0.12f), Vector3.one * 0.009f, 10, W);
            rig.slide = ch.Build("CockingKnob", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.08f);
            // finned barrel and the Cutts compensator
            float bz = 0.22f, blen = 0.26f;
            Barrel(mb, new Vector3(0, rig.boreY, bz), blen, 0.009f, DARK);
            for (int i = 0; i < 11; i++)
            {
                float z = bz + 0.01f + i * 0.012f;
                mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY, z), new Vector3(0, rig.boreY, z + 0.005f), 0.019f, 0.019f, 16, W);
            }
            mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY, bz + blen), new Vector3(0, rig.boreY, bz + blen + 0.055f), 0.016f, 0.014f, 14, W);
            for (int i = 0; i < 3; i++) mb.Sub(BLK).Box(new Vector3(0, rig.boreY + 0.013f, bz + blen + 0.012f + i * 0.014f), new Vector3(0.012f, 0.004f, 0.004f), W, 0.001f);
            // vertical wooden foregrip
            mb.Sub(FUR).Cyl(new Vector3(0, 0.05f, 0.29f), new Vector3(0, -0.045f, 0.3f), 0.017f, 0.02f, 14, W);
            for (int i = 0; i < 4; i++) mb.Sub(FUR).Cyl(new Vector3(0, 0.035f - i * 0.022f, 0.296f), new Vector3(0, 0.025f - i * 0.022f, 0.296f), 0.022f, 0.022f, 14, W);
            mb.Sub(FIN).Box(new Vector3(0, 0.055f, 0.28f), new Vector3(0.03f, 0.02f, 0.05f), W, 0.004f);
            // wooden butt stock
            Stock(mb, FUR, -0.06f, -0.38f, 0.085f, 0.0f, 0.06f, 0.042f);
            mb.Sub(FIN).Box(new Vector3(0, 0.055f, -0.07f), new Vector3(0.04f, 0.04f, 0.04f), W, 0.004f);   // stock mount
            mb.Sub(DARK).Box(new Vector3(0.0215f, 0.08f, 0.09f), new Vector3(0.002f, 0.018f, 0.06f), W, 0.001f);       // ejection port
            mb.Sub(FIN).Box(new Vector3(-0.022f, 0.06f, 0.0f), new Vector3(0.003f, 0.01f, 0.025f), W, 0.002f);         // fire selector
            mb.Sub(FIN).Box(new Vector3(-0.022f, 0.06f, 0.05f), new Vector3(0.003f, 0.01f, 0.025f), W, 0.002f);        // safety
            PinX(mb, 0.035f, -0.04f, 0.018f); PinX(mb, 0.035f, 0.1f, 0.018f);
            Irons(mb, -0.02f, bz + blen - 0.01f, 0.112f, 0.092f, rig.boreY + 0.012f);
            if (d.optics) Rail(mb, new Vector3(0, 0.097f, -0.02f), 0.12f);
            float mz = bz + blen + 0.06f;
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, mz));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.022f, 0.08f, 0.09f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.018f, 0.13f));
            rig.magStyle = MagStyle.Drum;
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.106f, 0.02f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.0f, 0.298f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.112f, -0.02f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.03f, -0.38f));
        }

        static void SmgMac(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.075f;
            // receiver block
            mb.Sub(FIN).Box(new Vector3(0, 0.07f, 0.04f), new Vector3(0.036f, 0.06f, 0.2f), W, 0.005f, 2);
            // grip doubles as magwell
            mb.Sub(BLK).Box(new Vector3(0, -0.012f, -0.003f), new Vector3(0.032f, 0.1f, 0.038f), W, 0.006f, 2, Quaternion.Euler(-8, 0, 0));
            TriggerGuard(mb, 0.02f, 0.07f, 0.04f, 0.0f, FIN);
            var ch = new MB(1);
            ch.Box(new Vector3(0, 0.104f, 0.07f), new Vector3(0.01f, 0.01f, 0.012f), W, 0.003f);
            rig.slide = ch.Build("Bolt", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.07f);
            // wire stock folded on top
            mb.Sub(DARK).Box(new Vector3(0.016f, 0.104f, -0.02f), new Vector3(0.004f, 0.004f, 0.2f), W);
            mb.Sub(DARK).Box(new Vector3(-0.016f, 0.104f, -0.02f), new Vector3(0.004f, 0.004f, 0.2f), W);
            mb.Sub(DARK).Box(new Vector3(0, 0.104f, -0.115f), new Vector3(0.036f, 0.004f, 0.006f), W);
            Barrel(mb, new Vector3(0, rig.boreY, 0.14f), 0.035f, 0.009f, DARK);
            Muzzle(mb, new Vector3(0, rig.boreY, 0.175f), 0.009f, d.suppressed ? 3 : 4);
            // front strap for the support hand
            mb.Sub(BLK).Box(new Vector3(0, 0.03f, 0.11f), new Vector3(0.02f, 0.03f, 0.05f), W, 0.006f);
            mb.Sub(DARK).Box(new Vector3(0.0185f, 0.08f, 0.04f), new Vector3(0.002f, 0.016f, 0.06f), W, 0.001f);   // ejection port
            foreach (float sx in new[] { -1f, 1f }) for (int i = 0; i < 4; i++) mb.Sub(DARK).Box(new Vector3(sx * 0.0185f, 0.055f, -0.03f + i * 0.04f), new Vector3(0.001f, 0.004f, 0.02f), W);   // stamped ribs
            PinX(mb, 0.05f, -0.045f, 0.018f); PinX(mb, 0.05f, 0.12f, 0.018f);
            if (d.optics) Rail(mb, new Vector3(0, 0.1f, 0.0f), 0.12f, 0.02f);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, 0.2f));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.02f, 0.085f, 0.04f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.025f, -0.004f), Quaternion.Euler(14, 0, 0));   // up inside the raked grip (it hung under it)
            rig.magEntry = -0.13f;
            rig.magStyle = MagStyle.PistolBox;
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.11f, 0.05f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.02f, 0.115f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.112f, -0.05f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.07f, -0.08f));
        }

        static void SmgVector(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.085f;
            PistolGrip(mb, BLK, 14f, 0.09f, 0.03f, 0.03f, 0.0f);
            var body = new List<Vector2> { new Vector2(-0.1f, 0.11f), new Vector2(0.22f, 0.11f), new Vector2(0.24f, 0.07f), new Vector2(0.12f, 0.0f), new Vector2(0.08f, -0.01f), new Vector2(0.04f, 0.03f), new Vector2(-0.06f, 0.03f), new Vector2(-0.1f, 0.06f) };
            mb.Sub(FIN).Extrude(body, 0.042f, W, 0.006f);
            mb.Sub(DARK).Box(new Vector3(0.022f, 0.075f, 0.12f), new Vector3(0.004f, 0.03f, 0.12f), W, 0.002f);
            foreach (float sx in new[] { -1f, 1f }) { mb.Sub(BLK).Box(new Vector3(sx * 0.0215f, 0.05f, 0.14f), new Vector3(0.002f, 0.03f, 0.05f), W, 0.002f, 1, Quaternion.Euler(-40, 0, 0)); }
            PinX(mb, 0.09f, -0.05f, 0.021f); PinX(mb, 0.05f, 0.08f, 0.021f);
            TriggerGuard(mb, 0.01f, 0.05f, 0.03f, 0.0f, BLK);
            var ch = new MB(1);
            ch.Box(new Vector3(-0.024f, 0.098f, 0.18f), new Vector3(0.01f, 0.01f, 0.012f), W, 0.003f);
            rig.slide = ch.Build("Bolt", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.06f);
            // folding stock
            mb.Sub(BLK).Box(new Vector3(0, 0.09f, -0.18f), new Vector3(0.03f, 0.03f, 0.16f), W, 0.006f, 2);
            mb.Sub(BLK).Box(new Vector3(0, 0.07f, -0.265f), new Vector3(0.04f, 0.08f, 0.014f), W, 0.004f);
            Barrel(mb, new Vector3(0, rig.boreY, 0.24f), 0.07f, 0.009f, DARK);
            Muzzle(mb, new Vector3(0, rig.boreY, 0.31f), 0.009f, d.suppressed ? 3 : 0);
            Rail(mb, new Vector3(0, 0.11f, -0.08f), 0.3f);
            float pdLine = IronSights(mb, -0.06f, 0.2f, 0.12f, true);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, 0.36f));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.022f, 0.09f, 0.05f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.0f, 0.1f), Quaternion.Euler(-4, 0, 0));
            rig.magStyle = d.magSize > 30 ? MagStyle.LongBox : MagStyle.PistolBox;
            rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.12f, 0.02f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.03f, 0.19f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, pdLine, -0.06f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.07f, -0.27f));
        }

        static void Bullpup(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.075f;
            bool pdw = d.cls == GunClass.SMG;
            PistolGrip(mb, FUR, 10f, 0.09f, 0.03f, 0.03f, 0.0f);
            var body = new List<Vector2> { new Vector2(-0.4f, 0.11f), new Vector2(0.2f, 0.11f), new Vector2(0.24f, 0.08f), new Vector2(0.24f, 0.035f), new Vector2(0.06f, 0.03f), new Vector2(0.03f, 0.04f), new Vector2(-0.04f, 0.03f), new Vector2(-0.37f, -0.02f), new Vector2(-0.41f, -0.01f) };
            mb.Sub(FUR).Extrude(body, pdw ? 0.05f : 0.046f, W, 0.012f);
            mb.Sub(BLK).Box(new Vector3(0, 0.04f, -0.41f), new Vector3(0.05f, 0.11f, 0.014f), W, 0.004f);
            TriggerGuard(mb, 0.02f, 0.07f, 0.035f, -0.005f, FUR);
            // foregrip lip
            mb.Sub(FUR).Box(new Vector3(0, 0.02f, 0.2f), new Vector3(0.04f, 0.03f, 0.03f), W, 0.008f);
            mb.Sub(DARK).Box(new Vector3(0.024f, 0.075f, -0.15f), new Vector3(0.002f, 0.02f, 0.06f), W, 0.001f);   // ejection port by your cheek
            for (int i = 0; i < 4; i++) mb.Sub(DARK).Box(new Vector3(0, 0f, -0.3f + i * 0.04f), new Vector3(0.05f, 0.003f, 0.012f), W, 0.001f);
            Swivel(mb, new Vector3(0, -0.012f, -0.38f));
            var ch = new MB(1);
            ch.Box(new Vector3(-0.026f, 0.095f, 0.12f), new Vector3(0.012f, 0.012f, 0.014f), W, 0.003f);
            rig.slide = ch.Build("ChargingHandle", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.08f);
            float bl = Mathf.Max(0.08f, d.barrel - 0.15f);
            Barrel(mb, new Vector3(0, rig.boreY, 0.24f), bl, 0.0085f, DARK);
            Muzzle(mb, new Vector3(0, rig.boreY, 0.24f + bl), 0.0085f, d.suppressed ? 3 : 0);
            Rail(mb, new Vector3(0, 0.11f, -0.12f), 0.28f);
            float bpLine = IronSights(mb, -0.1f, 0.15f, 0.12f, true);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, 0.29f + bl));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.025f, 0.07f, -0.15f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.032f, -0.15f), Quaternion.Euler(6, 0, 0));   // up inside the stock body
            rig.magStyle = pdw ? MagStyle.LongBox : MagStyle.Stanag;
            rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.12f, -0.03f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.02f, 0.17f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, bpLine, -0.1f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.04f, -0.42f));
        }

        static void Bolt(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.065f;
            bool semi = d.action == FireAction.Semi;
            // one-piece stock with grip
            var stock = new List<Vector2>
            {
                new Vector2(0.38f, 0.06f), new Vector2(0.38f, 0.035f), new Vector2(0.06f, 0.025f), new Vector2(0.035f, 0.025f),
                new Vector2(0.025f, -0.07f), new Vector2(-0.025f, -0.065f), new Vector2(-0.05f, 0.0f), new Vector2(-0.15f, 0.0f),
                new Vector2(-0.4f, -0.04f), new Vector2(-0.42f, -0.03f), new Vector2(-0.42f, 0.06f), new Vector2(-0.3f, 0.07f), new Vector2(-0.08f, 0.06f), new Vector2(-0.06f, 0.055f)
            };
            mb.Sub(FUR).Extrude(stock, 0.042f, W, 0.008f);
            mb.Sub(BLK).Box(new Vector3(0, 0.012f, -0.428f), new Vector3(0.044f, 0.1f, 0.014f), W, 0.004f);
            if (d.id == "lynx") mb.Sub(DARK).Box(new Vector3(0, 0.02f, -0.27f), new Vector3(0.046f, 0.03f, 0.12f), W, 0.006f);
            // receiver: round action with a front ring and rear bridge, the bolt shroud at the back, recoil lug
            mb.Sub(FIN).Cyl(new Vector3(0, 0.068f, -0.07f), new Vector3(0, 0.068f, 0.17f), 0.017f, 0.017f, 16, W);
            mb.Sub(FIN).Cyl(new Vector3(0, 0.068f, 0.11f), new Vector3(0, 0.068f, 0.17f), 0.0185f, 0.0185f, 16, W);
            mb.Sub(FIN).Cyl(new Vector3(0, 0.068f, -0.07f), new Vector3(0, 0.068f, -0.03f), 0.0185f, 0.0185f, 16, W);
            mb.Sub(DARK).Box(new Vector3(0.0155f, 0.072f, 0.04f), new Vector3(0.004f, 0.018f, 0.075f), W, 0.001f);   // ejection port
            mb.Sub(FIN).Cyl(new Vector3(0, 0.066f, -0.105f), new Vector3(0, 0.066f, -0.075f), 0.013f, 0.011f, 12, W);   // bolt shroud
            mb.Sub(FIN).Box(new Vector3(0, 0.046f, 0.155f), new Vector3(0.024f, 0.012f, 0.012f), W, 0.002f);           // recoil lug
            // checkering on the grip and forend, sling studs, floorplate
            foreach (float sx in new[] { -1f, 1f })
            {
                for (int r = 0; r < 3; r++) for (int c = 0; c < 6; c++) mb.Sub(DARK).Box(new Vector3(sx * 0.0215f, 0.04f + r * 0.007f, 0.24f + c * 0.012f), new Vector3(0.001f, 0.004f, 0.007f), W, 0, 0, Quaternion.Euler(45, 0, 0));
                for (int r = 0; r < 4; r++) for (int c = 0; c < 3; c++) mb.Sub(DARK).Box(new Vector3(sx * 0.0215f, -0.045f + r * 0.012f, -0.02f + c * 0.01f + r * 0.004f), new Vector3(0.001f, 0.005f, 0.005f), W, 0, 0, Quaternion.Euler(45, 0, 0));
            }
            Swivel(mb, new Vector3(0, 0.03f, 0.33f)); Swivel(mb, new Vector3(0, -0.033f, -0.33f));
            mb.Sub(FIN).Box(new Vector3(0, 0.022f, 0.09f), new Vector3(0.024f, 0.004f, 0.09f), W, 0.002f);
            TriggerGuard(mb, 0.0f, 0.045f, 0.025f, -0.002f, FIN);
            // bolt (animated) with handle
            var bb = new MB(1);
            bb.Cyl(new Vector3(0, 0.068f, -0.1f), new Vector3(0, 0.068f, -0.06f), 0.013f, 0.013f, 12, W);
            if (!semi)
            {
                bb.Cyl(new Vector3(0, 0.068f, -0.07f), new Vector3(0.05f, 0.045f, -0.075f), 0.004f, 0.004f, 8, W);
                bb.Sphere(new Vector3(0.052f, 0.044f, -0.075f), Vector3.one * 0.009f, 10, W);
            }
            else bb.Box(new Vector3(0.022f, 0.07f, 0.06f), new Vector3(0.014f, 0.01f, 0.012f), W, 0.003f);
            rig.slide = bb.Build("Bolt", rig.root.transform, mats[FIN]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.08f);
            float bl = d.barrel;
            Barrel(mb, new Vector3(0, rig.boreY + 0.003f, 0.17f), bl, 0.011f, FIN);
            mb.Sub(FIN).Cyl(new Vector3(0, rig.boreY + 0.003f, 0.17f), new Vector3(0, rig.boreY + 0.003f, 0.17f + bl * 0.5f), 0.0125f, 0.011f, 14, W);
            Muzzle(mb, new Vector3(0, rig.boreY + 0.003f, 0.17f + bl), 0.011f, d.cal == Caliber.C338 || d.cal == Caliber.C308 && d.rarity >= Rarity.Rare ? 2 : 4);
            if (d.optics) Rail(mb, new Vector3(0, 0.085f, -0.06f), 0.2f, 0.02f);
            float boltLine = d.optics ? 0.1f : IronSights(mb, 0.12f, 0.15f + bl, 0.08f, false);
            // magazine well
            mb.Sub(DARK).Box(new Vector3(0, 0.03f, 0.08f), new Vector3(0.03f, 0.02f, 0.07f), W, 0.004f);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY + 0.003f, 0.19f + bl + 0.03f));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.02f, 0.075f, 0.04f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.02f, 0.08f));
            rig.magStyle = d.magSize >= 10 && !semi ? MagStyle.Straight : d.id == "comrade" ? MagStyle.Clip : MagStyle.Clip;
            if (d.id == "polar" || d.id == "lynx" || d.id == "kodiak" || d.id == "hunter" || d.id == "ranger" || d.id == "prairie" || d.id == "harrier" || d.id == "glacier" || d.id == "boss_longbow") rig.magStyle = MagStyle.Straight;
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.095f, 0.02f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.035f, 0.26f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, boltLine, d.optics ? 0f : 0.1f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.01f, -0.43f));
        }

        static void Pump(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.07f;
            bool tactical = d.id == "riot";
            mb.Sub(FIN).Box(new Vector3(0, 0.06f, 0.05f), new Vector3(0.038f, 0.06f, 0.22f), W, 0.006f, 2);
            TriggerGuard(mb, 0.0f, 0.05f, 0.03f, 0.0f, BLK);
            if (tactical)
            {
                PistolGrip(mb, FUR, 18f, 0.095f, 0.032f, 0.03f, -0.005f);
                mb.Sub(DARK).Cyl(new Vector3(0, 0.07f, -0.06f), new Vector3(0, 0.07f, -0.25f), 0.014f, 0.014f, 12, W);
                var sp = new List<Vector2> { new Vector2(-0.12f, 0.1f), new Vector2(-0.12f, 0.05f), new Vector2(-0.3f, 0.01f), new Vector2(-0.33f, 0.02f), new Vector2(-0.33f, 0.11f) };
                mb.Sub(FUR).Extrude(sp, 0.044f, W, 0.006f);
            }
            else
            {
                Stock(mb, FUR, -0.06f, -0.4f, 0.08f, 0.0f, 0.05f, 0.042f);
                var gr = new List<Vector2> { new Vector2(-0.06f, 0.03f), new Vector2(-0.025f, -0.06f), new Vector2(-0.07f, -0.05f), new Vector2(-0.12f, 0.0f) };
                mb.Sub(FUR).Extrude(gr, 0.036f, W, 0.006f);
            }
            float bl = d.barrel;
            Barrel(mb, new Vector3(0, rig.boreY + 0.008f, 0.16f), bl, 0.013f, FIN);
            mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY - 0.02f, 0.16f), new Vector3(0, rig.boreY - 0.02f, 0.16f + bl * 0.85f), 0.012f, 0.012f, 12, W); // mag tube
            mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY - 0.02f, 0.16f + bl * 0.85f), new Vector3(0, rig.boreY - 0.02f, 0.17f + bl * 0.85f), 0.013f, 0.013f, 12, W);
            mb.Sub(ACC).Sphere(new Vector3(0, rig.boreY + 0.024f, 0.155f + bl), Vector3.one * 0.003f, 8, W); // bead
            // receiver: ejection port, loading port underneath, the slide bars, a barrel clamp and the mag cap
            mb.Sub(DARK).Box(new Vector3(0.0195f, 0.07f, 0.06f), new Vector3(0.002f, 0.022f, 0.07f), W, 0.001f);
            mb.Sub(DARK).Box(new Vector3(0, 0.03f, 0.08f), new Vector3(0.026f, 0.003f, 0.08f), W, 0.001f);
            foreach (float sx in new[] { -1f, 1f }) mb.Sub(DARK).Box(new Vector3(sx * 0.012f, rig.boreY - 0.03f, 0.18f), new Vector3(0.003f, 0.004f, 0.12f), W, 0.001f);
            mb.Sub(FIN).Box(new Vector3(0, rig.boreY - 0.006f, 0.16f + bl * 0.83f), new Vector3(0.022f, 0.04f, 0.012f), W, 0.003f);
            mb.Sub(FIN).Cyl(new Vector3(0, rig.boreY - 0.02f, 0.17f + bl * 0.85f), new Vector3(0, rig.boreY - 0.02f, 0.19f + bl * 0.85f), 0.011f, 0.009f, 12, W);
            PinX(mb, 0.05f, 0.0f, 0.0195f); PinX(mb, 0.05f, 0.04f, 0.0195f);
            Swivel(mb, new Vector3(0, rig.boreY - 0.035f, 0.17f + bl * 0.85f));
            var pump = new MB(1);
            pump.Box(new Vector3(0, rig.boreY - 0.02f, 0.27f), new Vector3(0.042f, 0.036f, 0.15f), W, 0.01f, 2);
            for (int i = 0; i < 6; i++) pump.Box(new Vector3(0, rig.boreY - 0.02f, 0.21f + i * 0.024f), new Vector3(0.044f, 0.038f, 0.006f), W, 0.004f);
            rig.pump = pump.Build("Pump", rig.root.transform, mats[FUR]).transform;
            rig.pumpTravel = new Vector3(0, 0, -0.08f);
            if (d.optics) Rail(mb, new Vector3(0, 0.09f, -0.04f), 0.15f);
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY + 0.008f, 0.17f + bl));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.022f, 0.065f, 0.06f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.02f, 0.08f), Quaternion.Euler(-30, 0, 0));
            rig.magStyle = MagStyle.ShellCaddy;
            if (d.optics) rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.1f, 0.03f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, rig.boreY - 0.04f, 0.27f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.095f, -0.04f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.02f, -0.4f));
        }

        static void DoubleBarrel(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.06f;
            mb.Sub(FIN).Box(new Vector3(0, 0.05f, 0.03f), new Vector3(0.05f, 0.05f, 0.1f), W, 0.008f, 2);
            Stock(mb, FUR, -0.02f, -0.38f, 0.07f, -0.005f, 0.06f, 0.044f);
            var gr = new List<Vector2> { new Vector2(-0.02f, 0.035f), new Vector2(0.0f, -0.05f), new Vector2(-0.045f, -0.045f), new Vector2(-0.09f, 0.0f) };
            mb.Sub(FUR).Extrude(gr, 0.038f, W, 0.006f);
            TriggerGuard(mb, 0.0f, 0.05f, 0.025f, -0.003f, FIN);
            float bl = d.barrel;
            Barrel(mb, new Vector3(0.012f, rig.boreY, 0.08f), bl, 0.012f, FIN);
            Barrel(mb, new Vector3(-0.012f, rig.boreY, 0.08f), bl, 0.012f, FIN);
            mb.Sub(FIN).Box(new Vector3(0, rig.boreY + 0.012f, 0.08f + bl * 0.5f), new Vector3(0.006f, 0.004f, bl), W);
            mb.Sub(FUR).Box(new Vector3(0, rig.boreY - 0.016f, 0.2f), new Vector3(0.045f, 0.022f, 0.16f), W, 0.008f, 2);
            mb.Sub(ACC).Sphere(new Vector3(0, rig.boreY + 0.016f, 0.075f + bl), Vector3.one * 0.003f, 8, W);
            // break lever on top, hinge pin, engraved side plates, double triggers
            mb.Sub(FIN).Box(new Vector3(0, 0.078f, -0.005f), new Vector3(0.008f, 0.006f, 0.03f), W, 0.002f, 1, Quaternion.Euler(0, 20, 0));
            mb.Sub(DARK).Cyl(new Vector3(-0.026f, 0.035f, 0.075f), new Vector3(0.026f, 0.035f, 0.075f), 0.005f, 0.005f, 10, W);
            foreach (float sx in new[] { -1f, 1f }) { mb.Sub(ACC).Box(new Vector3(sx * 0.0255f, 0.05f, 0.02f), new Vector3(0.0012f, 0.035f, 0.07f), W, 0.002f); }
            mb.Sub(DARK).Box(new Vector3(0, 0.012f, 0.03f), new Vector3(0.005f, 0.016f, 0.004f), W, 0.001f, 1, Quaternion.Euler(15, 0, 0));
            Swivel(mb, new Vector3(0, rig.boreY - 0.03f, 0.25f));
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, 0.085f + bl));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0, rig.boreY, 0.08f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, rig.boreY + 0.03f, 0.1f), Quaternion.Euler(0, 0, 0));
            rig.magStyle = MagStyle.ShellCaddy;
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, rig.boreY - 0.03f, 0.2f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, rig.boreY + 0.02f, 0.06f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.0f, -0.38f));
        }

        static void BoxShotgun(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            AK(d, rig, mb, mats);
            // fatter barrel + handguard
            mb.Sub(DARK).Cyl(new Vector3(0, rig.boreY, 0.35f), new Vector3(0, rig.boreY, 0.2f + d.barrel), 0.014f, 0.014f, 12, W);
            rig.magStyle = d.drum ? MagStyle.Drum : MagStyle.Straight;
        }

        static void Lmg(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.085f;
            PistolGrip(mb, BLK, 18f, 0.1f, 0.033f, 0.03f, 0.0f);
            mb.Sub(FIN).Box(new Vector3(0, 0.07f, 0.06f), new Vector3(0.05f, 0.08f, 0.32f), W, 0.006f, 2);
            mb.Sub(FIN).Box(new Vector3(0, 0.118f, 0.07f), new Vector3(0.048f, 0.016f, 0.22f), W, 0.006f, 2); // top cover
            mb.Sub(DARK).Box(new Vector3(-0.026f, 0.09f, 0.1f), new Vector3(0.004f, 0.02f, 0.05f), W); // feed tray
            TriggerGuard(mb, 0.015f, 0.06f, 0.03f, -0.002f, FIN);
            var ch = new MB(1);
            ch.Box(new Vector3(0.03f, 0.075f, 0.16f), new Vector3(0.018f, 0.012f, 0.014f), W, 0.003f);
            rig.slide = ch.Build("ChargingHandle", rig.root.transform, mats[DARK]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.1f);
            if (d.furniture == Finish.Wood) Stock(mb, FUR, -0.1f, -0.42f, 0.1f, 0.01f, 0.03f, 0.045f, true);
            else
            {
                Stock(mb, FUR, -0.1f, -0.4f, 0.11f, 0.03f, 0.02f, 0.046f);
            }
            float bl = d.barrel;
            mb.Sub(DARK).Box(new Vector3(0, 0.085f, 0.3f), new Vector3(0.05f, 0.06f, 0.16f), W, 0.012f, 2); // heat shield / handguard
            for (int i = 0; i < 5; i++) mb.Sub(BLK).Box(new Vector3(0.025f, 0.085f, 0.24f + i * 0.03f), new Vector3(0.003f, 0.02f, 0.012f), W, 0.002f);
            Barrel(mb, new Vector3(0, rig.boreY, 0.38f), bl - 0.1f, 0.011f, FIN);
            Muzzle(mb, new Vector3(0, rig.boreY, 0.28f + bl), 0.011f, 0);
            // carry handle
            mb.Sub(BLK).Box(new Vector3(0, 0.15f, 0.3f), new Vector3(0.012f, 0.012f, 0.12f), W, 0.004f);
            mb.Sub(BLK).Box(new Vector3(0, 0.13f, 0.25f), new Vector3(0.01f, 0.03f, 0.01f), W);
            mb.Sub(BLK).Box(new Vector3(0, 0.13f, 0.35f), new Vector3(0.01f, 0.03f, 0.01f), W);
            // bipod folded
            mb.Sub(DARK).Box(new Vector3(0.016f, 0.06f, 0.3f + bl * 0.4f), new Vector3(0.007f, 0.007f, 0.24f), W, 0.002f);
            mb.Sub(DARK).Box(new Vector3(-0.016f, 0.06f, 0.3f + bl * 0.4f), new Vector3(0.007f, 0.007f, 0.24f), W, 0.002f);
            Rail(mb, new Vector3(0, 0.126f, -0.06f), 0.18f);
            // feed cover latch, ejection port, belt box mount, gas tube, flash hider barrel collar
            mb.Sub(DARK).Box(new Vector3(0, 0.12f, -0.035f), new Vector3(0.03f, 0.008f, 0.012f), W, 0.002f);
            mb.Sub(DARK).Box(new Vector3(0.0255f, 0.065f, 0.07f), new Vector3(0.002f, 0.02f, 0.06f), W, 0.001f);
            mb.Sub(FIN).Cyl(new Vector3(0, rig.boreY - 0.025f, 0.22f), new Vector3(0, rig.boreY - 0.025f, 0.26f + bl * 0.5f), 0.007f, 0.007f, 10, W);
            mb.Sub(FIN).Cyl(new Vector3(0, rig.boreY, 0.38f), new Vector3(0, rig.boreY, 0.40f), 0.016f, 0.016f, 12, W);
            PinX(mb, 0.05f, -0.06f, 0.025f); PinX(mb, 0.05f, 0.2f, 0.025f);
            Swivel(mb, new Vector3(0, 0.03f, -0.36f));
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, 0.33f + bl));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.026f, 0.06f, 0.1f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(-0.03f, 0.04f, 0.1f), Quaternion.Euler(0, 0, -15));
            rig.magStyle = MagStyle.BeltBox;
            rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.136f, 0.0f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.05f, 0.3f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.15f, -0.05f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.06f, -0.42f));
        }

        static void AntiMat(GunDef d, GunRig rig, MB mb, Material[] mats)
        {
            rig.boreY = 0.085f;
            bool bolt = d.action == FireAction.Bolt;
            PistolGrip(mb, BLK, 16f, 0.1f, 0.034f, 0.03f, 0.0f);
            mb.Sub(FIN).Box(new Vector3(0, 0.07f, 0.08f), new Vector3(0.056f, 0.085f, 0.42f), W, 0.008f, 2);
            mb.Sub(FUR).Box(new Vector3(0, 0.07f, 0.4f), new Vector3(0.06f, 0.07f, 0.24f), W, 0.01f, 2);
            for (int i = 0; i < 6; i++) mb.Sub(DARK).Box(new Vector3(0.03f, 0.07f, 0.32f + i * 0.03f), new Vector3(0.004f, 0.03f, 0.014f), W, 0.002f);
            TriggerGuard(mb, 0.015f, 0.065f, 0.03f, -0.004f, FIN);
            // stock w/ monopod
            var sp = new List<Vector2> { new Vector2(-0.13f, 0.11f), new Vector2(-0.13f, 0.03f), new Vector2(-0.42f, 0.0f), new Vector2(-0.46f, 0.01f), new Vector2(-0.46f, 0.13f), new Vector2(-0.3f, 0.13f) };
            mb.Sub(FIN).Extrude(sp, 0.05f, W, 0.008f);
            mb.Sub(BLK).Box(new Vector3(0, 0.07f, -0.468f), new Vector3(0.052f, 0.13f, 0.02f), W, 0.006f);
            mb.Sub(DARK).Box(new Vector3(0, -0.01f, -0.37f), new Vector3(0.012f, 0.06f, 0.012f), W, 0.003f);
            var bb = new MB(1);
            if (bolt)
            {
                bb.Cyl(new Vector3(0.03f, 0.09f, -0.05f), new Vector3(0.075f, 0.065f, -0.05f), 0.006f, 0.006f, 8, W);
                bb.Sphere(new Vector3(0.078f, 0.064f, -0.05f), Vector3.one * 0.012f, 10, W);
            }
            else bb.Box(new Vector3(0.032f, 0.1f, 0.12f), new Vector3(0.016f, 0.016f, 0.02f), W, 0.004f);
            rig.slide = bb.Build("Bolt", rig.root.transform, mats[FIN]).transform;
            rig.slideTravel = new Vector3(0, 0, -0.1f);
            float bl = d.barrel;
            float br = d.cal == Caliber.C20mm ? 0.02f : 0.014f;   // the 20mm: a much fatter barrel and a huge brake
            Barrel(mb, new Vector3(0, rig.boreY, 0.52f), bl - 0.25f, br, FIN);
            for (int i = 0; i < 6; i++) mb.Sub(FIN).Cyl(new Vector3(0, rig.boreY, 0.55f + i * 0.04f), new Vector3(0, rig.boreY, 0.565f + i * 0.04f), br * 1.2f, br * 1.2f, 12, W); // flutes
            Muzzle(mb, new Vector3(0, rig.boreY, 0.27f + bl), br, 2);
            Rail(mb, new Vector3(0, 0.113f, -0.1f), 0.42f, 0.024f);
            // big receiver details: ejection port, bolt release, buffer housing ribs, carry handle
            mb.Sub(DARK).Box(new Vector3(0.0285f, 0.085f, 0.03f), new Vector3(0.002f, 0.028f, 0.09f), W, 0.001f);
            for (int i = 0; i < 4; i++) mb.Sub(DARK).Box(new Vector3(0, 0.0275f, -0.06f + i * 0.04f), new Vector3(0.058f, 0.003f, 0.012f), W, 0.001f);
            mb.Sub(BLK).Box(new Vector3(0, 0.145f, 0.2f), new Vector3(0.012f, 0.012f, 0.14f), W, 0.004f);
            mb.Sub(BLK).Box(new Vector3(0, 0.128f, 0.14f), new Vector3(0.01f, 0.03f, 0.012f), W, 0.002f);
            mb.Sub(BLK).Box(new Vector3(0, 0.128f, 0.26f), new Vector3(0.01f, 0.03f, 0.012f), W, 0.002f);
            PinX(mb, 0.05f, -0.1f, 0.028f, 0.004f); PinX(mb, 0.05f, 0.27f, 0.028f, 0.004f);
            // bipod
            mb.Sub(DARK).Box(new Vector3(0.02f, 0.035f, 0.55f), new Vector3(0.008f, 0.008f, 0.26f), W, 0.002f, 1, Quaternion.Euler(-8, 0, 0));
            mb.Sub(DARK).Box(new Vector3(-0.02f, 0.035f, 0.55f), new Vector3(0.008f, 0.008f, 0.26f), W, 0.002f, 1, Quaternion.Euler(-8, 0, 0));
            rig.muzzle = Anchor(rig.root, "Muzzle", new Vector3(0, rig.boreY, 0.37f + bl));
            rig.ejectPort = Anchor(rig.root, "Eject", new Vector3(0.03f, 0.08f, 0.05f));
            rig.magwell = Anchor(rig.root, "Magwell", new Vector3(0, 0.048f, 0.11f));   // up inside the receiver
            rig.magStyle = MagStyle.BigBox;
            rig.rail = Anchor(rig.root, "Rail", new Vector3(0, 0.123f, 0.0f));
            rig.foregrip = Anchor(rig.root, "Foregrip", new Vector3(0, 0.03f, 0.42f));
            rig.sightLine = Anchor(rig.root, "SightLine", new Vector3(0, 0.14f, -0.06f));
            rig.butt = Anchor(rig.root, "Butt", new Vector3(0, 0.07f, -0.47f));
        }

        // ------------------------------------------------------------------ magazines
        /// <summary>Builds a magazine model. Origin = top of the magazine (where it seats into the magwell). Body extends along -Y.</summary>
        public static GameObject BuildMag(MagStyle style, Transform parent, Finish finish, out Vector3 size)
        {
            var mb = new MB(3);
            var mats = new[] { style == MagStyle.Banana ? Mats.Lit("mag_ak", new Color(0.35f, 0.17f, 0.1f), 0.1f, 0.4f, Mats.Noise, null, 0.3f, 0.2f) : finish == Finish.Fde ? Mats.PolymerFde : Mats.Polymer, Mats.GunMetal, Mats.Brass };
            Color w = Color.white;
            size = new Vector3(0.03f, 0.12f, 0.06f);
            switch (style)
            {
                case MagStyle.PistolBox:
                    mb.Sub(1).Box(new Vector3(0, -0.055f, 0), new Vector3(0.022f, 0.11f, 0.032f), w, 0.003f);
                    mb.Sub(0).Box(new Vector3(0, -0.113f, 0.002f), new Vector3(0.028f, 0.012f, 0.04f), w, 0.004f);
                    mb.Sub(2).Cyl(new Vector3(0, -0.003f, -0.01f), new Vector3(0, -0.003f, 0.01f), 0.0045f, 0.0045f, 8, w);
                    size = new Vector3(0.028f, 0.12f, 0.04f);
                    break;
                case MagStyle.Stanag:
                case MagStyle.LongBox:
                    float l = style == MagStyle.LongBox ? 0.24f : 0.18f;
                    {
                        var end = CurvedMag(mb, 1, 4, l, 0.024f, 0.062f, 3.5f, w);
                        mb.Sub(0).Box(end.Item1, new Vector3(0.03f, 0.012f, 0.07f), w, 0.004f, 1, end.Item2);
                    }
                    mb.Sub(2).Cyl(new Vector3(0, -0.004f, -0.02f), new Vector3(0, -0.004f, 0.02f), 0.005f, 0.005f, 8, w);
                    size = new Vector3(0.03f, l, 0.07f);
                    break;
                case MagStyle.Banana:
                    {
                        var end = CurvedMag(mb, 0, 6, 0.21f, 0.026f, 0.068f, 7f, w, true);
                        mb.Sub(1).Box(end.Item1, new Vector3(0.028f, 0.01f, 0.072f), w, 0.003f, 1, end.Item2);
                    }
                    mb.Sub(2).Cyl(new Vector3(0, -0.004f, -0.02f), new Vector3(0, -0.004f, 0.02f), 0.0055f, 0.0055f, 8, w);
                    size = new Vector3(0.03f, 0.2f, 0.12f);
                    break;
                case MagStyle.Straight:
                case MagStyle.BigBox:
                    float sl = style == MagStyle.BigBox ? 0.12f : 0.14f;
                    float sw = style == MagStyle.BigBox ? 0.045f : 0.028f;
                    mb.Sub(1).Box(new Vector3(0, -sl * 0.5f, 0), new Vector3(sw, sl, 0.075f), w, 0.003f);
                    for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(0, -0.03f - i * 0.035f, 0), new Vector3(sw + 0.003f, 0.004f, 0.07f), w);
                    mb.Sub(0).Box(new Vector3(0, -sl - 0.005f, 0), new Vector3(sw + 0.005f, 0.012f, 0.08f), w, 0.004f);
                    size = new Vector3(sw, sl, 0.08f);
                    break;
                case MagStyle.SmgStick:
                    mb.Sub(1).Box(new Vector3(0, -0.085f, 0), new Vector3(0.024f, 0.17f, 0.036f), w, 0.003f);
                    for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(0, -0.035f - i * 0.045f, 0.0185f), new Vector3(0.016f, 0.012f, 0.002f), w);   // witness holes
                    mb.Sub(0).Box(new Vector3(0, -0.175f, 0.002f), new Vector3(0.028f, 0.012f, 0.042f), w, 0.004f);
                    size = new Vector3(0.028f, 0.18f, 0.042f);
                    break;
                case MagStyle.SmgCurved:
                    {
                        var end = CurvedMag(mb, 1, 4, 0.14f, 0.022f, 0.032f, 6f, w);
                        mb.Sub(0).Box(end.Item1, new Vector3(0.026f, 0.01f, 0.04f), w, 0.003f, 1, end.Item2);
                    }
                    size = new Vector3(0.026f, 0.14f, 0.06f);
                    break;
                case MagStyle.Drum:
                    mb.Sub(1).Box(new Vector3(0, -0.025f, 0), new Vector3(0.026f, 0.05f, 0.05f), w, 0.004f);
                    // the drum faces forward: its round face looks down the barrel, the winding key on the muzzle side
                    mb.Sub(0).Cyl(new Vector3(0, -0.09f, -0.03f), new Vector3(0, -0.09f, 0.03f), 0.065f, 0.065f, 24, w);
                    mb.Sub(1).Cyl(new Vector3(0, -0.09f, 0.03f), new Vector3(0, -0.09f, 0.036f), 0.03f, 0.022f, 18, w);
                    size = new Vector3(0.13f, 0.16f, 0.07f);
                    break;
                case MagStyle.BeltBox:
                    mb.Sub(0).Box(new Vector3(0, -0.06f, 0), new Vector3(0.07f, 0.12f, 0.11f), w, 0.008f, 2);
                    mb.Sub(1).Box(new Vector3(0, 0.0f, 0), new Vector3(0.03f, 0.012f, 0.06f), w, 0.003f);
                    for (int i = 0; i < 4; i++) mb.Sub(2).Cyl(new Vector3(0.036f - i * 0.006f, 0.003f + i * 0.005f, -0.02f), new Vector3(0.036f - i * 0.006f, 0.003f + i * 0.005f, 0.02f), 0.005f, 0.005f, 8, w);
                    size = new Vector3(0.07f, 0.12f, 0.11f);
                    break;
                case MagStyle.ShellCaddy:
                    mb.Sub(0).Box(new Vector3(0, -0.02f, 0), new Vector3(0.03f, 0.04f, 0.1f), w, 0.004f);
                    for (int i = 0; i < 4; i++)
                    {
                        mb.Sub(1).Cyl(new Vector3(0, 0.0f, -0.035f + i * 0.023f), new Vector3(0, 0.05f, -0.035f + i * 0.023f), 0.0095f, 0.0095f, 10, new Color(0.7f, 0.12f, 0.1f));
                        mb.Sub(2).Cyl(new Vector3(0, -0.002f, -0.035f + i * 0.023f), new Vector3(0, 0.008f, -0.035f + i * 0.023f), 0.0105f, 0.0105f, 10, w);
                    }
                    size = new Vector3(0.03f, 0.09f, 0.1f);
                    break;
                case MagStyle.Speedloader:
                    mb.Sub(0).Cyl(new Vector3(0, -0.03f, 0), new Vector3(0, -0.01f, 0), 0.021f, 0.021f, 16, w);
                    mb.Sub(0).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, -0.03f, 0), 0.008f, 0.01f, 10, w);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        var p = new Vector3(Mathf.Cos(a) * 0.014f, 0, Mathf.Sin(a) * 0.014f);
                        mb.Sub(2).Cyl(p + new Vector3(0, -0.01f, 0), p + new Vector3(0, 0.02f, 0), 0.0045f, 0.0035f, 8, w);
                    }
                    size = new Vector3(0.045f, 0.07f, 0.045f);
                    break;
                default: // stripper clip
                    mb.Sub(1).Box(new Vector3(0, -0.004f, 0), new Vector3(0.012f, 0.008f, 0.06f), w, 0.001f);
                    for (int i = 0; i < 5; i++) mb.Sub(2).Cyl(new Vector3(0, -0.004f, -0.024f + i * 0.012f), new Vector3(0, 0.05f, -0.024f + i * 0.012f), 0.0055f, 0.004f, 8, w);
                    size = new Vector3(0.015f, 0.06f, 0.065f);
                    break;
            }
            return mb.Build("Mag", parent, mats);
        }

        /// <summary>
        /// A smoothly curved magazine body: overlapping segments laid along an arc that bends towards the muzzle.
        /// Returns the position/rotation of the base plate.
        /// </summary>
        static (Vector3, Quaternion) CurvedMag(MB mb, int sub, int segs, float length, float width, float depth, float bendPerSeg, Color w, bool ribs = false)
        {
            float segLen = length / segs;
            Vector3 p = Vector3.zero;
            for (int i = 0; i < segs; i++)
            {
                var q = Quaternion.Euler(-bendPerSeg * (i + 0.5f), 0, 0);
                Vector3 c = p + q * new Vector3(0, -segLen * 0.5f, 0);
                mb.Sub(sub).Box(c, new Vector3(width, segLen * 1.08f, depth), w, 0.003f, 1, q);
                if (ribs) mb.Sub(sub).Box(c + q * new Vector3(width * 0.5f + 0.0008f, 0, 0), new Vector3(0.0018f, segLen * 0.55f, depth * 0.6f), w * 0.72f, 0.0006f, 1, q);
                p += q * new Vector3(0, -segLen, 0);
            }
            var qe = Quaternion.Euler(-bendPerSeg * segs, 0, 0);
            return (p + qe * new Vector3(0, -0.004f, 0), qe);
        }

        /// <summary>
        /// A magnified scope: tube, rings, turrets, bells. Returns the eyepiece (a round lens facing the shooter at
        /// -Z); an "Objective" point at the front marks where the scope's camera looks out from.
        /// </summary>
        static Transform BuildScope(SightDef s, Transform parent, Material body)
        {
            var mb = new MB(2);
            Color w = Color.white;
            float ax, tubeR, zEye, zObj, eyeR, objR, lensR;
            if (s.model == 7)
            {   // prism: a short box body
                ax = 0.036f; lensR = 0.0135f; zEye = -0.078f; zObj = 0.072f;
                mb.Sub(0).Box(new Vector3(0, 0.006f, 0), new Vector3(0.026f, 0.012f, 0.07f), w, 0.002f);
                mb.Sub(0).Box(new Vector3(0, ax, -0.002f), new Vector3(0.036f, 0.042f, 0.1f), w, 0.005f, 2);
                mb.Sub(0).Cyl(new Vector3(0, ax, -0.052f), new Vector3(0, ax, zEye), 0.0165f, 0.0165f, 20, w);
                mb.Sub(0).Cyl(new Vector3(0, ax, 0.048f), new Vector3(0, ax, zObj), 0.0175f, 0.0175f, 20, w);
                mb.Sub(1).Cyl(new Vector3(0.018f, ax + 0.006f, 0f), new Vector3(0.026f, ax + 0.006f, 0f), 0.008f, 0.008f, 12, w);
            }
            else
            {
                bool big = s.model == 6;
                ax = big ? 0.05f : 0.043f; tubeR = big ? 0.015f : 0.0127f; eyeR = big ? 0.021f : 0.019f; objR = big ? 0.027f : 0.021f;
                lensR = eyeR - 0.003f;
                float z0 = big ? -0.085f : -0.075f, z1 = big ? 0.13f : 0.105f;
                zEye = z0 - (big ? 0.065f : 0.06f); zObj = z1 + (big ? 0.11f : 0.08f);
                mb.Sub(0).Cyl(new Vector3(0, ax, z0), new Vector3(0, ax, z1), tubeR, tubeR, 20, w);                                   // main tube
                mb.Sub(0).Cyl(new Vector3(0, ax, z0), new Vector3(0, ax, z0 - 0.025f), tubeR, eyeR, 20, w);                           // eyepiece bell
                mb.Sub(0).Cyl(new Vector3(0, ax, z0 - 0.025f), new Vector3(0, ax, zEye), eyeR, eyeR, 20, w);
                mb.Sub(1).Cyl(new Vector3(0, ax, zEye + 0.012f), new Vector3(0, ax, zEye + 0.02f), eyeR + 0.0012f, eyeR + 0.0012f, 20, w); // focus ring
                mb.Sub(0).Cyl(new Vector3(0, ax, z1), new Vector3(0, ax, z1 + 0.04f), tubeR, objR, 20, w);                             // objective bell
                mb.Sub(0).Cyl(new Vector3(0, ax, z1 + 0.04f), new Vector3(0, ax, zObj), objR, objR, 20, w);
                // turrets
                mb.Sub(0).Cyl(new Vector3(0, ax, 0.015f), new Vector3(0, ax + tubeR + 0.012f, 0.015f), 0.009f, 0.009f, 14, w);
                mb.Sub(0).Cyl(new Vector3(0, ax, 0.015f), new Vector3(tubeR + 0.012f, ax, 0.015f), 0.009f, 0.009f, 14, w);
                for (int i = 0; i < 6; i++) mb.Sub(1).Box(new Vector3(0, ax + tubeR + 0.008f, 0.015f) + Quaternion.Euler(0, i * 30f, 0) * new Vector3(0.0092f, 0, 0), new Vector3(0.0015f, 0.008f, 0.0015f), w);
                // rings
                foreach (float rz in new[] { -0.045f, big ? 0.085f : 0.065f })
                {
                    mb.Sub(1).Box(new Vector3(0, (ax - tubeR) * 0.5f, rz), new Vector3(0.024f, ax - tubeR, 0.016f), w, 0.002f);
                    mb.Sub(1).Cyl(new Vector3(0, ax, rz - 0.008f), new Vector3(0, ax, rz + 0.008f), tubeR + 0.003f, tubeR + 0.003f, 20, w);
                }
            }
            var go = mb.Build("SightBody", parent, body, Mats.Lit("scope_ring", new Color(0.08f, 0.08f, 0.085f), 0.6f, 0.45f, Mats.Metal, null, 0.2f, 0.3f));
            // front glass (blue coated)
            var fg = new MB(1);
            float fr = s.model == 7 ? 0.0145f : s.model == 6 ? 0.024f : 0.018f;
            fg.Cyl(new Vector3(0, ax, zObj), new Vector3(0, ax, zObj + 0.0008f), fr, fr, 20, w);
            fg.Build("ObjectiveGlass", go.transform, Mats.Lit("scope_glass", new Color(0.05f, 0.1f, 0.16f), 0.2f, 0.95f));
            var obj = new GameObject("Objective").transform;
            obj.SetParent(go.transform, false); obj.localPosition = new Vector3(0, ax, zObj + 0.004f);
            // the eyepiece
            var lens = new MB(1);
            float L = lensR;
            lens.Quad(new Vector3(-L, -L, 0), new Vector3(L, -L, 0), new Vector3(L, L, 0), new Vector3(-L, L, 0), Color.white, Vector2.one);
            var lgo = lens.Build("Lens", go.transform, Mats.Scope(s.model == 7 ? 2 : s.model == 6 ? 1 : 0, s.reticle, L));
            lgo.transform.localPosition = new Vector3(0, ax, zEye - 0.0008f);
            return lgo.transform;
        }

        // ------------------------------------------------------------------ attachments
        /// <summary>
        /// Builds a muzzle device, light/laser or foregrip. Muzzle: origin at the gun's muzzle point, +Z forward.
        /// Side: origin on the rail, body sticks out along +X and runs along Z. Grip: origin at its top, hangs down -Y.
        /// Returns the emitter (where light / laser come out), or null.
        /// </summary>
        public static Transform BuildAttachment(AttachDef a, Transform parent)
        {
            var mb = new MB(3);
            Transform emit = null;
            switch (a.slot)
            {
                case AttachSlot.Muzzle:
                    switch (a.model)
                    {
                        case 0:
                            mb.Sub(0).Cyl(new Vector3(0, 0, -0.045f), new Vector3(0, 0, 0.085f), 0.0165f, 0.0165f, 16, W);
                            mb.Sub(1).Cyl(new Vector3(0, 0, 0.085f), new Vector3(0, 0, 0.092f), 0.0165f, 0.012f, 16, W);
                            mb.Sub(1).Cyl(new Vector3(0, 0, -0.03f), new Vector3(0, 0, -0.022f), 0.0172f, 0.0172f, 16, W);
                            break;
                        case 1:
                            mb.Sub(0).Cyl(new Vector3(0, 0, -0.045f), new Vector3(0, 0, 0.15f), 0.019f, 0.019f, 18, W);
                            mb.Sub(1).Cyl(new Vector3(0, 0, 0.15f), new Vector3(0, 0, 0.158f), 0.019f, 0.013f, 18, W);
                            for (int i = 0; i < 3; i++) mb.Sub(1).Cyl(new Vector3(0, 0, -0.025f + i * 0.006f), new Vector3(0, 0, -0.022f + i * 0.006f), 0.0196f, 0.0196f, 18, W);
                            break;
                        case 2:
                            mb.Sub(0).Box(new Vector3(0, 0, -0.012f), new Vector3(0.032f, 0.026f, 0.06f), W, 0.004f);
                            for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(0, 0, -0.025f + i * 0.016f), new Vector3(0.034f, 0.014f, 0.007f), W, 0.001f);
                            break;
                        default:
                            mb.Sub(0).Cyl(new Vector3(0, 0, -0.04f), new Vector3(0, 0, 0.02f), 0.013f, 0.013f, 14, W);
                            for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(0, 0.011f, -0.015f + i * 0.012f), new Vector3(0.006f, 0.006f, 0.006f), W);
                            break;
                    }
                    break;
                case AttachSlot.Side:
                    mb.Sub(0).Box(new Vector3(0.006f, 0, 0), new Vector3(0.012f, 0.02f, 0.03f), W, 0.002f);   // rail clamp
                    if (a.model == 0)
                    {
                        mb.Sub(0).Cyl(new Vector3(0.02f, 0, -0.035f), new Vector3(0.02f, 0, 0.05f), 0.012f, 0.012f, 14, W);
                        mb.Sub(0).Cyl(new Vector3(0.02f, 0, 0.05f), new Vector3(0.02f, 0, 0.062f), 0.015f, 0.015f, 14, W);
                        mb.Sub(2).Cyl(new Vector3(0.02f, 0, 0.0615f), new Vector3(0.02f, 0, 0.0625f), 0.013f, 0.013f, 14, W);
                        emit = Point(parent, new Vector3(0.02f, 0, 0.065f));
                    }
                    else if (a.model == 1)
                    {
                        mb.Sub(0).Box(new Vector3(0.019f, 0, 0.005f), new Vector3(0.022f, 0.022f, 0.045f), W, 0.004f);
                        mb.Sub(2).Cyl(new Vector3(0.019f, 0, 0.0275f), new Vector3(0.019f, 0, 0.0285f), 0.0035f, 0.0035f, 10, W);
                        emit = Point(parent, new Vector3(0.019f, 0, 0.03f));
                    }
                    else
                    {
                        mb.Sub(0).Box(new Vector3(0.022f, 0, 0.0f), new Vector3(0.028f, 0.03f, 0.075f), W, 0.004f);
                        mb.Sub(1).Box(new Vector3(0.022f, 0.016f, -0.01f), new Vector3(0.02f, 0.004f, 0.03f), W, 0.001f);
                        mb.Sub(2).Cyl(new Vector3(0.026f, -0.004f, 0.0375f), new Vector3(0.026f, -0.004f, 0.0385f), 0.009f, 0.009f, 12, W);
                        mb.Sub(2).Cyl(new Vector3(0.014f, 0.007f, 0.0375f), new Vector3(0.014f, 0.007f, 0.0385f), 0.003f, 0.003f, 8, W);
                        emit = Point(parent, new Vector3(0.022f, 0, 0.04f));
                    }
                    break;
                default:   // grips
                    mb.Sub(0).Box(new Vector3(0, -0.005f, 0), new Vector3(0.022f, 0.01f, 0.035f), W, 0.002f);   // clamp
                    if (a.model == 0)
                    {
                        mb.Sub(0).Cyl(new Vector3(0, -0.008f, 0), new Vector3(0, -0.085f, -0.006f), 0.0135f, 0.0145f, 14, W);
                        for (int i = 0; i < 5; i++) mb.Sub(1).Cyl(new Vector3(0, -0.025f - i * 0.012f, -0.001f * i), new Vector3(0, -0.029f - i * 0.012f, -0.001f * i), 0.0148f, 0.0148f, 14, W);
                    }
                    else if (a.model == 1)
                    {
                        // an AFG: a long rail clamp, a ramp sloping down and forward for the palm, a rounded front lip that
                        // stops the hand sliding off, grip ridges along the ramp
                        var poly = new List<Vector2> {
                            new Vector2(0.05f, -0.006f), new Vector2(-0.05f, -0.006f), new Vector2(-0.052f, -0.013f), new Vector2(-0.03f, -0.022f),
                            new Vector2(0.005f, -0.04f), new Vector2(0.025f, -0.05f), new Vector2(0.04f, -0.05f), new Vector2(0.052f, -0.04f), new Vector2(0.055f, -0.02f) };
                        mb.Sub(0).Extrude(poly, 0.026f, W, 0.004f);
                        // ridges across the ramp
                        for (int i = 0; i < 5; i++)
                        {
                            float t = (i + 0.5f) / 5f;
                            var p = Vector3.Lerp(new Vector3(0, -0.02f, -0.036f), new Vector3(0, -0.044f, 0.014f), t);
                            mb.Sub(1).Box(p, new Vector3(0.027f, 0.003f, 0.006f), W, 0f, 1, Quaternion.Euler(27f, 0, 0));
                        }
                        // the clamp's screw knobs
                        foreach (float zz in new[] { -0.03f, 0.03f }) mb.Sub(1).Cyl(new Vector3(0.013f, -0.008f, zz), new Vector3(0.017f, -0.008f, zz), 0.004f, 0.004f, 8, W);
                    }
                    else
                    {
                        mb.Sub(0).Cyl(new Vector3(0, -0.008f, 0), new Vector3(0, -0.048f, 0), 0.014f, 0.016f, 14, W);
                        mb.Sub(1).Cyl(new Vector3(0, -0.046f, 0), new Vector3(0, -0.052f, 0), 0.017f, 0.017f, 14, W);
                    }
                    break;
            }
            var lens = Mats.Lit("att_lens", new Color(0.9f, 0.95f, 1f), 0f, 0.95f, null, null, 0f, 0f, false, 0.5f, a.laser && !a.light ? (Color?)(a.beam * 2f) : new Color(1.2f, 1.2f, 1.1f));
            mb.Build("Attachment", parent, FinishMat(a.finish == Finish.Black ? Finish.Black : a.finish), Mats.GunBlued, lens);
            return emit;
        }

        static Transform Point(Transform parent, Vector3 p)
        {
            var t = new GameObject("Emitter").transform;
            t.SetParent(parent, false); t.localPosition = p;
            return t;
        }

        // ------------------------------------------------------------------ sights
        /// <summary>Builds a holo/red-dot sight. Origin = bottom centre of the mount (sits on the rail). Returns the lens transform.</summary>
        public static Transform BuildSight(SightDef s, Transform parent)
        {
            var mb = new MB(2);
            Color w = Color.white;
            Material body = s.finish == Finish.Fde ? Mats.Lit("sight_fde", new Color(0.6f, 0.5f, 0.36f), 0.3f, 0.35f, Mats.Metal, null, 0.3f, 0.6f)
                : s.finish == Finish.Od ? Mats.Lit("sight_od", new Color(0.3f, 0.33f, 0.24f), 0.3f, 0.35f, Mats.Metal, null, 0.3f, 0.6f)
                : s.finish == Finish.Gray ? Mats.Lit("sight_gray", new Color(0.35f, 0.36f, 0.38f), 0.6f, 0.45f, Mats.Metal, null, 0.2f, 0.4f)
                : Mats.Lit("sight_blk", new Color(0.06f, 0.06f, 0.065f), 0.4f, 0.4f, Mats.Metal, null, 0.25f, 0.5f);
            if (s.mag > 0f) return BuildScope(s, parent, body);
            Vector3 lensPos; Vector2 lensSize;
            switch (s.model)
            {
                case 0: // EX3 holo: box body + hood
                    mb.Sub(0).Box(new Vector3(0, 0.006f, -0.005f), new Vector3(0.03f, 0.012f, 0.085f), w, 0.003f);
                    mb.Sub(0).Box(new Vector3(0, 0.02f, -0.03f), new Vector3(0.034f, 0.022f, 0.035f), w, 0.004f, 2);
                    mb.Sub(0).Box(new Vector3(0.0175f, 0.042f, 0.012f), new Vector3(0.004f, 0.05f, 0.05f), w, 0.002f);
                    mb.Sub(0).Box(new Vector3(-0.0175f, 0.042f, 0.012f), new Vector3(0.004f, 0.05f, 0.05f), w, 0.002f);
                    mb.Sub(0).Box(new Vector3(0, 0.069f, 0.012f), new Vector3(0.039f, 0.004f, 0.05f), w, 0.002f);
                    mb.Sub(1).Box(new Vector3(0.012f, 0.02f, -0.045f), new Vector3(0.004f, 0.006f, 0.006f), w); // buttons
                    mb.Sub(1).Box(new Vector3(-0.012f, 0.02f, -0.045f), new Vector3(0.004f, 0.006f, 0.006f), w);
                    lensPos = new Vector3(0, 0.044f, 0.012f); lensSize = new Vector2(0.03f, 0.046f);
                    break;
                case 1: // micro dot tube
                    mb.Sub(0).Box(new Vector3(0, 0.005f, 0), new Vector3(0.022f, 0.01f, 0.03f), w, 0.002f);
                    mb.Sub(0).Tube(new Vector3(0, 0.028f, -0.018f), new Vector3(0, 0.028f, 0.018f), 0.0125f, 0.0105f, 20, w);
                    mb.Sub(0).Box(new Vector3(0.013f, 0.028f, 0), new Vector3(0.006f, 0.008f, 0.01f), w, 0.002f);
                    mb.Sub(0).Cyl(new Vector3(0, 0.041f, 0), new Vector3(0, 0.046f, 0), 0.005f, 0.005f, 10, w);
                    lensPos = new Vector3(0, 0.028f, 0.0f); lensSize = new Vector2(0.021f, 0.021f);
                    break;
                case 2: // open reflex with angled window
                    mb.Sub(0).Box(new Vector3(0, 0.005f, 0), new Vector3(0.028f, 0.01f, 0.06f), w, 0.002f);
                    mb.Sub(0).Box(new Vector3(0.016f, 0.026f, 0.015f), new Vector3(0.004f, 0.036f, 0.016f), w, 0.002f);
                    mb.Sub(0).Box(new Vector3(-0.016f, 0.026f, 0.015f), new Vector3(0.004f, 0.036f, 0.016f), w, 0.002f);
                    mb.Sub(0).Box(new Vector3(0, 0.046f, 0.015f), new Vector3(0.036f, 0.004f, 0.016f), w, 0.002f);
                    lensPos = new Vector3(0, 0.027f, 0.015f); lensSize = new Vector2(0.028f, 0.034f);
                    break;
                case 3: // hex holo
                    mb.Sub(0).Box(new Vector3(0, 0.007f, -0.01f), new Vector3(0.032f, 0.014f, 0.09f), w, 0.004f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f + 30f;
                        var q = Quaternion.Euler(0, 0, a);
                        mb.Sub(0).Box(new Vector3(0, 0.042f, 0.012f) + q * new Vector3(0, 0.024f, 0), new Vector3(0.028f, 0.004f, 0.04f), w, 0.0015f, 1, q);
                    }
                    mb.Sub(1).Box(new Vector3(0, 0.02f, -0.04f), new Vector3(0.026f, 0.014f, 0.02f), w, 0.003f);
                    lensPos = new Vector3(0, 0.042f, 0.012f); lensSize = new Vector2(0.036f, 0.04f);
                    break;
                default: // tactical reflex w/ hood
                    mb.Sub(0).Box(new Vector3(0, 0.008f, -0.005f), new Vector3(0.03f, 0.016f, 0.07f), w, 0.004f, 2);
                    mb.Sub(0).Tube(new Vector3(0, 0.038f, -0.01f), new Vector3(0, 0.038f, 0.025f), 0.021f, 0.018f, 6, w);
                    lensPos = new Vector3(0, 0.038f, 0.008f); lensSize = new Vector2(0.03f, 0.03f);
                    break;
            }
            var go = mb.Build("SightBody", parent, body, Mats.Lit("sight_btn", new Color(0.12f, 0.12f, 0.12f), 0f, 0.2f));
            // lens quad
            var lens = new MB(1);
            lens.Quad(new Vector3(-lensSize.x / 2, -lensSize.y / 2, 0), new Vector3(lensSize.x / 2, -lensSize.y / 2, 0), new Vector3(lensSize.x / 2, lensSize.y / 2, 0), new Vector3(-lensSize.x / 2, lensSize.y / 2, 0), Color.white, Vector2.one);
            var lgo = lens.Build("Lens", go.transform, Mats.Holo(s.reticle, s.style, s.size));
            lgo.transform.localPosition = lensPos;
            // make the quad face the shooter (normal towards -Z); quad is double sided via Cull Off
            return lgo.transform;
        }
    }
}
