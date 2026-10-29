using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Builds each gun's look, modelled on a real design (Glock, Desert Eagle, Uzi, a
    /// revolver, a target .22, a Beretta 93R, a pump shotgun, a sawed-off, a Benelli,
    /// an AA-12, bolt-action and marksman rifles, an AR-15, an AUG bullpup, an AK, a
    /// SCAR, an M249 and a Vector). Local frame: +Z is the barrel, +Y is up, and the
    /// origin is where the hand holds the pistol grip.
    /// </summary>
    public class GunModel : MonoBehaviour
    {
        public GunDef def;
        public Transform muzzle;
        public Transform magwell;
        public Transform scopeEye;      // back lens of the scope (where your eye goes)
        public Transform scopeFront;    // front lens (the scope's camera looks out from here)
        public Transform ejectPort;
        public Transform pumpPart;
        public Transform foregrip;      // where the second hand holds on
        public Transform ammoAnchor;    // where the ammo counter sits (back of the gun, facing you)
        public Transform visual;        // everything but the mag; recoil kicks this

        public static Material Metal => Prims.Lit(new Color(0.09f, 0.095f, 0.105f), 0.55f, 0.75f, Surface.Metal);
        public static Material DarkMetal => Prims.Lit(new Color(0.05f, 0.05f, 0.055f), 0.45f, 0.7f, Surface.Metal);
        public static Material Chrome => Prims.Lit(new Color(0.72f, 0.73f, 0.75f), 0.85f, 0.95f, Surface.Metal);
        public static Material Polymer => Prims.Lit(new Color(0.13f, 0.135f, 0.145f), 0.28f, 0f, Surface.Plastic);
        public static Material Wood => Prims.Lit(new Color(0.45f, 0.26f, 0.12f), 0.45f, 0f, Surface.Wood);
        public static Material DarkWood => Prims.Lit(new Color(0.3f, 0.17f, 0.08f), 0.45f, 0f, Surface.Wood);
        public static Material Grip => Prims.Lit(new Color(0.1f, 0.1f, 0.11f), 0.15f, 0f, Surface.Rubber);
        public static Material Tan => Prims.Lit(new Color(0.62f, 0.53f, 0.38f), 0.3f, 0.1f, Surface.Plastic);
        public static Material Olive => Prims.Lit(new Color(0.27f, 0.31f, 0.2f), 0.3f, 0.05f, Surface.Plastic);
        public static Material Port => Prims.Lit(new Color(0.02f, 0.02f, 0.025f), 0.2f);
        public static Material Brass => Prims.Lit(new Color(0.85f, 0.65f, 0.25f), 0.75f, 0.9f, Surface.Metal);
        public static Material Glass => Prims.Lit(new Color(0.1f, 0.18f, 0.25f), 0.95f, 0.4f, Surface.Plastic);

        /* ======================= little building helpers ======================= */

        Transform v;
        Material accent;

        GameObject B(float x, float y, float z, float sx, float sy, float sz, Material m, float rx = 0, float ry = 0, float rz = 0)
            => Prims.Box(v, new Vector3(x, y, z), new Vector3(sx, sy, sz), m, false, Quaternion.Euler(rx, ry, rz));

        /// <summary>A cylinder along Z from z0 to z1.</summary>
        GameObject Z(float x, float y, float z0, float z1, float d, Material m)
            => Prims.Cyl(v, new Vector3(x, y, (z0 + z1) * 0.5f), d, Mathf.Abs(z1 - z0), m, Vector3.forward);

        GameObject Ax(Vector3 p, float d, float len, Material m, Vector3 axis) => Prims.Cyl(v, p, d, len, m, axis);

        GameObject Ball(float x, float y, float z, float sx, float sy, float sz, Material m) => Prims.Ball(v, new Vector3(x, y, z), new Vector3(sx, sy, sz), m);

        /// <summary>A Picatinny rail: a base strip with cross slots.</summary>
        void Rail(float y, float z0, float z1, float w = 0.022f)
        {
            B(0, y, (z0 + z1) * 0.5f, w * 0.7f, 0.006f, z1 - z0, DarkMetal);
            for (float z = z0 + 0.005f; z < z1 - 0.004f; z += 0.01f) B(0, y + 0.004f, z, w, 0.005f, 0.006f, DarkMetal);
        }

        /// <summary>Pistol grip with finger grooves and texture, slanted back at the bottom.</summary>
        void PistolGrip(float z, float len, Material m, float angle = 18f, float w = 0.03f, float depth = 0.036f)
        {
            float a = angle * Mathf.Deg2Rad;
            var center = new Vector3(0, -len * 0.5f + 0.02f, z - Mathf.Sin(a) * len * 0.5f);
            Prims.Box(v, center, new Vector3(w, len, depth), m, false, Quaternion.Euler(angle, 0, 0));
            // a rounded back strap and finger grooves on the front
            Prims.Box(v, center + new Vector3(0, 0, -depth * 0.45f), new Vector3(w * 0.9f, len * 0.95f, depth * 0.25f), m, false, Quaternion.Euler(angle, 0, 0));
            for (int i = 0; i < 3; i++)
            {
                float t = 0.25f + i * 0.22f;
                var p = new Vector3(0, 0.02f - len * t, z - Mathf.Sin(a) * len * t + depth * 0.5f * Mathf.Cos(a));
                Prims.Box(v, p, new Vector3(w * 1.02f, 0.006f, 0.008f), m, false, Quaternion.Euler(angle, 0, 0));
            }
            for (int i = 0; i < 4; i++)
            {
                float t = 0.2f + i * 0.18f;
                var p = new Vector3(0, 0.02f - len * t, z - Mathf.Sin(a) * len * t);
                Prims.Box(v, p, new Vector3(w * 1.04f, 0.003f, depth * 0.8f), Grip, false, Quaternion.Euler(angle, 0, 0));
            }
        }

        void TriggerAndGuard(float z, float y, bool square = false, float len = 0.055f)
        {
            B(0, y - 0.006f, z + 0.012f, 0.006f, 0.022f, 0.006f, DarkMetal, -18);
            B(0, y - 0.03f, z + len * 0.5f - 0.008f, 0.012f, 0.005f, len, Metal);
            B(0, y - 0.016f, z + len - 0.008f, 0.012f, square ? 0.03f : 0.026f, 0.005f, Metal, square ? 0 : -25);
        }

        void IronSights(float y, float zFront, float zRear)
        {
            B(0, y + 0.007f, zFront, 0.004f, 0.014f, 0.006f, DarkMetal);
            B(0, y + 0.003f, zFront, 0.012f, 0.006f, 0.012f, DarkMetal);
            B(-0.006f, y + 0.008f, zRear, 0.004f, 0.014f, 0.006f, DarkMetal);
            B(0.006f, y + 0.008f, zRear, 0.004f, 0.014f, 0.006f, DarkMetal);
            B(0, y + 0.002f, zRear, 0.018f, 0.005f, 0.008f, DarkMetal);
        }

        void RedDot(float y, float z)
        {
            B(0, y + 0.006f, z, 0.026f, 0.01f, 0.04f, DarkMetal);
            Z(0, y + 0.027f, z - 0.02f, z + 0.02f, 0.036f, DarkMetal);
            Prims.Cyl(v, new Vector3(0, y + 0.027f, z + 0.021f), 0.03f, 0.002f, Glass, Vector3.forward);
            Prims.Box(v, new Vector3(0, y + 0.027f, z - 0.019f), new Vector3(0.004f, 0.004f, 0.001f), Prims.Emissive(new Color(1f, 0.15f, 0.1f), 3f), false);
            B(0.022f, y + 0.027f, z, 0.008f, 0.012f, 0.012f, DarkMetal);
        }

        /// <summary>A rifle scope sitting on rings over the rail. Returns the eye end's height.</summary>
        void Scope(float y, float zBack, float zFront, float tube = 0.03f, float bell = 0.05f)
        {
            float cy = y + bell * 0.5f + 0.012f;
            foreach (float z in new[] { zBack + (zFront - zBack) * 0.32f, zBack + (zFront - zBack) * 0.66f })
            {
                B(0, y + 0.006f, z, 0.026f, 0.012f, 0.018f, DarkMetal);
                Ax(new Vector3(0, cy, z), tube + 0.008f, 0.016f, DarkMetal, Vector3.forward);
                B(0, (y + cy) * 0.5f, z, 0.01f, cy - y, 0.016f, DarkMetal);
            }
            float len = zFront - zBack;
            Z(0, cy, zBack + len * 0.18f, zFront - len * 0.22f, tube, Metal);
            // eyepiece and objective bells, tapering into the tube
            Z(0, cy, zBack, zBack + len * 0.12f, bell * 0.85f, Metal);
            Z(0, cy, zBack + len * 0.12f, zBack + len * 0.2f, (bell * 0.85f + tube) * 0.5f, Metal);
            Z(0, cy, zFront - len * 0.18f, zFront, bell, Metal);
            Z(0, cy, zFront - len * 0.24f, zFront - len * 0.18f, (bell + tube) * 0.5f, Metal);
            Z(0, cy, zFront - 0.006f, zFront, bell * 1.04f, accent);
            // windage and elevation turrets
            Ax(new Vector3(0, cy + tube * 0.5f + 0.012f, zBack + len * 0.5f), 0.022f, 0.024f, DarkMetal, Vector3.up);
            Ax(new Vector3(tube * 0.5f + 0.012f, cy, zBack + len * 0.5f), 0.022f, 0.024f, DarkMetal, Vector3.right);
            Ax(new Vector3(-tube * 0.5f - 0.008f, cy, zBack + len * 0.5f), 0.016f, 0.016f, DarkMetal, Vector3.right);
            Prims.Cyl(v, new Vector3(0, cy, zFront + 0.0005f), bell * 0.86f, 0.001f, Glass, Vector3.forward);
            scopeEye = Prims.Empty("scopeEye", transform, new Vector3(0, cy, zBack - 0.004f)).transform;
            scopeFront = Prims.Empty("scopeFront", transform, new Vector3(0, cy, zFront + 0.01f)).transform;
        }

        void Suppressor(float y, float z0, float len, float d)
        {
            Z(0, y, z0, z0 + len, d, DarkMetal);
            Z(0, y, z0 + len - 0.006f, z0 + len, d * 0.6f, Port);
            for (int i = 1; i < 4; i++) Z(0, y, z0 + len * i / 4f - 0.002f, z0 + len * i / 4f + 0.002f, d * 1.03f, Metal);
        }

        void MuzzleBrake(float y, float z, float d)
        {
            Z(0, y, z, z + 0.05f, d * 1.5f, DarkMetal);
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++) B(s * d * 0.74f, y, z + 0.01f + i * 0.014f, 0.004f, d * 0.9f, 0.007f, Port);
        }

        void FlashHider(float y, float z, float d)
        {
            Z(0, y, z, z + 0.045f, d * 1.35f, DarkMetal);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var o = Quaternion.Euler(0, 0, a) * new Vector3(d * 0.66f, 0, 0);
                B(o.x, y + o.y, z + 0.03f, 0.003f, 0.003f, 0.03f, Port, 0, 0, a);
            }
        }

        /// <summary>An M4-style collapsible stock on a buffer tube.</summary>
        void M4Stock(float y, float z, Material m)
        {
            Z(0, y, z - 0.16f, z, 0.03f, Metal);
            B(0, y - 0.012f, z - 0.17f, 0.036f, 0.07f, 0.11f, m, 4);
            B(0, y + 0.018f, z - 0.17f, 0.03f, 0.022f, 0.1f, m);
            B(0, y - 0.012f, z - 0.226f, 0.04f, 0.1f, 0.016f, Grip);
            B(0, y - 0.05f, z - 0.15f, 0.03f, 0.02f, 0.05f, m);
        }

        /// <summary>A classic rifle stock: wrist into the pistol grip, comb, and a rubber butt pad.</summary>
        void RifleStock(float y, float z, float len, Material m, bool thumbhole = false)
        {
            B(0, y - 0.012f, z - 0.04f, 0.034f, 0.04f, 0.09f, m, 12);
            B(0, -0.025f, -0.02f, 0.032f, 0.09f, 0.042f, m, 22);                 // the grip of the wrist, where the hand goes
            B(0, -0.068f, -0.04f, 0.034f, 0.012f, 0.05f, Grip, 22);
            B(0, y - 0.02f, z - len * 0.55f, 0.036f, 0.07f, len * 0.75f, m, -4);
            B(0, y + 0.012f, z - len * 0.6f, 0.032f, 0.03f, len * 0.6f, m);
            B(0, y - 0.03f, z - len * 0.62f, 0.034f, 0.06f, len * 0.6f, m, 6);
            B(0, y - 0.02f, z - len - 0.005f, 0.038f, 0.11f, 0.018f, Grip);
            if (thumbhole) B(0, y - 0.04f, z - 0.1f, 0.04f, 0.03f, 0.04f, Port);
        }

        void SkeletonStock(float y, float z, Material m)
        {
            B(0, y + 0.012f, z - 0.12f, 0.02f, 0.014f, 0.22f, m);
            B(0, y - 0.045f, z - 0.12f, 0.02f, 0.012f, 0.22f, m, -8);
            B(0, y - 0.016f, z - 0.235f, 0.034f, 0.1f, 0.016f, Grip);
            B(0, y - 0.016f, z - 0.01f, 0.02f, 0.07f, 0.014f, m);
        }

        void Bipod(float y, float z)
        {
            B(0, y - 0.01f, z, 0.03f, 0.016f, 0.03f, DarkMetal);
            foreach (float s in new[] { -1f, 1f })
                Ax(new Vector3(s * 0.012f, y - 0.018f, z - 0.08f), 0.01f, 0.16f, DarkMetal, new Vector3(s * 0.06f, -0.05f, -1f));
        }

        void Port_(float x, float y, float z, float sy, float sz) => B(x, y, z, 0.003f, sy, sz, Port);

        /* ======================= the guns ======================= */

        public static GunModel Build(GunDef def, Transform parent)
        {
            var root = new GameObject("gun:" + def.name);
            root.transform.SetParent(parent, false);
            var m = root.AddComponent<GunModel>();
            m.def = def;
            m.v = Prims.Empty("visual", root.transform).transform;
            m.visual = m.v;
            m.accent = Prims.Lit(def.accent, 0.5f, 0.3f, Surface.PaintedMetal);
            switch (def.id)
            {
                case "flop9": m.Glock(); break;
                case "deagle": m.Deagle(); break;
                case "buzz": m.Uzi(); break;
                case "revolt": m.Revolver(); break;
                case "squeak": m.TargetPistol(); break;
                case "hammer": m.Beretta93(); break;
                case "boom": m.PumpShotgun(false); break;
                case "slug": m.PumpShotgun(true); break;
                case "sawed": m.SawedOff(); break;
                case "thunder": m.Benelli(); break;
                case "brute": m.AA12(); break;
                case "longshot": m.BoltRifle(Olive, false, 0.6f); break;
                case "elephant": m.BoltRifle(Wood, true, 0.62f); break;
                case "needle": m.Awp(); break;
                case "whisper": m.Vss(); break;
                case "marksman": m.Dmr(); break;
                case "bulldog": m.Aug(); break;
                case "viper": m.Ak(); break;
                case "bravo": m.Scar(); break;
                case "hmr": m.M249(); break;
                case "spitfire": m.Vector(); break;
                default: m.Ar15(); break;
            }
            if (m.foregrip == null) m.foregrip = Prims.Empty("foregrip", root.transform, new Vector3(0, -0.03f, 0.05f)).transform;
            if (m.ammoAnchor == null)
            {
                m.ammoAnchor = Prims.Empty("ammo", m.v, new Vector3(0.03f, 0.06f, -0.04f)).transform;
                m.ammoAnchor.localRotation = Quaternion.Euler(0, -60, 0);
            }
            CombineParts(m.v);
            return m;
        }

        void Muzzle(float y, float z) => muzzle = Prims.Empty("muzzle", transform, new Vector3(0, y, z)).transform;
        void Well(float y, float z, float tilt = 0f)
        {
            magwell = Prims.Empty("magwell", v, new Vector3(0, y, z)).transform;
            magwell.localRotation = Quaternion.Euler(tilt, 0, 0);
        }
        void Eject(float y, float z) => ejectPort = Prims.Empty("eject", transform, new Vector3(0.025f, y, z)).transform;
        void Fore(float y, float z) => foregrip = Prims.Empty("foregrip", transform, new Vector3(0, y, z)).transform;
        void Ammo(float x, float y, float z)
        {
            ammoAnchor = Prims.Empty("ammo", v, new Vector3(x, y, z)).transform;
            ammoAnchor.localRotation = Quaternion.Euler(0, -60, 0);
        }

        // ---------------- pistols ----------------

        void PistolFrame(float slideLen, float slideH, float slideW, Material slide, Material frame, bool square = false)
        {
            float back = -0.045f, front = back + slideLen;
            PistolGrip(0, 0.1f, frame, 20f, 0.028f, 0.04f);
            B(0, 0.016f, (back + front) * 0.5f + 0.004f, slideW * 0.92f, 0.022f, slideLen * 0.92f, frame);   // frame / dust cover
            B(0, 0.009f, front - 0.03f, slideW * 0.7f, 0.006f, 0.04f, frame);                                 // accessory rail
            for (int i = 0; i < 3; i++) B(0, 0.005f, front - 0.045f + i * 0.012f, slideW * 0.8f, 0.004f, 0.006f, frame);
            TriggerAndGuard(0.0f, 0.016f, square, 0.06f);
            B(0, 0.027f + slideH * 0.5f, (back + front) * 0.5f, slideW, slideH, slideLen, slide);             // slide
            B(0, 0.027f + slideH + 0.001f, (back + front) * 0.5f, slideW * 0.5f, 0.003f, slideLen * 0.9f, slide);
            for (int i = 0; i < 6; i++) B(0, 0.027f + slideH * 0.5f, back + 0.006f + i * 0.0065f, slideW * 1.03f, slideH * 0.8f, 0.0022f, Port);
            for (int i = 0; i < 4; i++) B(0, 0.027f + slideH * 0.5f, front - 0.012f - i * 0.0065f, slideW * 1.03f, slideH * 0.6f, 0.002f, Port);
            Port_(slideW * 0.5f, 0.027f + slideH * 0.6f, (back + front) * 0.5f + 0.008f, slideH * 0.5f, 0.024f);
            IronSights(0.027f + slideH, front - 0.008f, back + 0.008f);
            Z(0, 0.027f + slideH * 0.45f, front - 0.004f, front + 0.001f, 0.011f, Port);
            B(0, 0.022f, back - 0.002f, 0.012f, 0.012f, 0.01f, frame);                                        // beavertail
            Muzzle(0.027f + slideH * 0.45f, front + 0.01f);
            Eject(0.027f + slideH * 0.6f, (back + front) * 0.5f);
            Ammo(slideW * 0.5f + 0.01f, 0.027f + slideH * 0.5f, back + 0.015f);
            Well(0.02f, -0.004f, 20f);
            Fore(-0.04f, 0.03f);
        }

        void Glock() => PistolFrame(0.18f, 0.03f, 0.025f, DarkMetal, Polymer, true);

        void Deagle()
        {
            PistolFrame(0.24f, 0.036f, 0.032f, Chrome, Chrome);
            // the triangular barrel on top, with its grooves
            B(0, 0.072f, 0.075f, 0.018f, 0.012f, 0.23f, Chrome);
            for (int i = 0; i < 6; i++) B(0, 0.079f, 0.0f + i * 0.03f, 0.02f, 0.002f, 0.012f, Metal);
            B(0, 0.072f, 0.19f, 0.018f, 0.014f, 0.01f, Metal);
            B(0, -0.03f, -0.012f, 0.03f, 0.075f, 0.036f, Grip, 20);
        }

        void Uzi()
        {
            // boxy receiver, mag through the grip, ribbed top cover and a stubby barrel
            PistolGrip(0.0f, 0.1f, Polymer, 4f, 0.032f, 0.04f);
            B(0, 0.042f, 0.06f, 0.044f, 0.05f, 0.25f, DarkMetal);
            for (int i = 0; i < 8; i++) B(0, 0.068f, -0.04f + i * 0.026f, 0.046f, 0.004f, 0.012f, Metal);
            Ball(0, 0.069f, -0.05f, 0.012f, 0.012f, 0.012f, Metal);   // cocking knob
            Z(0, 0.045f, 0.185f, 0.24f, 0.016f, Metal);
            Z(0, 0.045f, 0.225f, 0.24f, 0.024f, DarkMetal);
            TriggerAndGuard(0.0f, 0.018f, true, 0.06f);
            B(0, 0.03f, -0.08f, 0.04f, 0.035f, 0.03f, DarkMetal);
            IronSights(0.067f, 0.17f, -0.05f);
            Port_(0.022f, 0.05f, 0.06f, 0.02f, 0.04f);
            Muzzle(0.045f, 0.25f);
            Eject(0.05f, 0.06f);
            Ammo(0.03f, 0.05f, -0.06f);
            Well(0.02f, 0.0f, 4f);
            Fore(0.01f, 0.12f);
        }

        void Revolver()
        {
            PistolGrip(-0.01f, 0.095f, Wood, 26f, 0.03f, 0.042f);
            B(0, 0.03f, 0.02f, 0.026f, 0.04f, 0.09f, Metal);                  // frame
            B(0, 0.055f, -0.025f, 0.012f, 0.025f, 0.012f, Metal, -30);        // hammer
            Z(0, 0.048f, 0.0f, 0.05f, 0.046f, Metal);                          // cylinder
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Z(Mathf.Cos(a) * 0.021f, 0.048f + Mathf.Sin(a) * 0.021f, -0.001f, 0.051f, 0.008f, Port);
            }
            Z(0, 0.048f, 0.05f, 0.21f, 0.017f, Metal);                         // barrel
            B(0, 0.036f, 0.13f, 0.016f, 0.018f, 0.16f, Metal);                // full underlug
            B(0, 0.06f, 0.13f, 0.008f, 0.006f, 0.16f, Metal);                 // vent rib
            B(0, 0.066f, 0.2f, 0.004f, 0.012f, 0.01f, accent);                // front sight
            TriggerAndGuard(0.0f, 0.015f, false, 0.05f);
            Muzzle(0.048f, 0.22f);
            Eject(0.048f, 0.025f);
            Ammo(0.03f, 0.05f, -0.03f);
            Well(0.018f, 0.025f);
            Fore(-0.04f, 0.03f);
        }

        void TargetPistol()
        {
            PistolGrip(-0.01f, 0.1f, Wood, 28f, 0.03f, 0.042f);
            B(0, 0.035f, 0.0f, 0.024f, 0.035f, 0.11f, Metal);                 // receiver tube
            Z(0, 0.04f, -0.055f, 0.055f, 0.028f, Metal);
            Z(0, 0.042f, 0.055f, 0.2f, 0.016f, Metal);                         // long, slim barrel
            B(0, 0.054f, 0.19f, 0.004f, 0.012f, 0.008f, accent);
            B(0, 0.06f, -0.04f, 0.016f, 0.01f, 0.01f, DarkMetal);
            TriggerAndGuard(0.0f, 0.016f, false, 0.055f);
            Muzzle(0.042f, 0.21f);
            Eject(0.045f, 0.0f);
            Ammo(0.025f, 0.045f, -0.05f);
            Well(0.02f, -0.006f, 28f);
            Fore(-0.04f, 0.03f);
        }

        void Beretta93()
        {
            PistolFrame(0.2f, 0.03f, 0.026f, Metal, Metal);
            B(0, 0.052f, 0.06f, 0.012f, 0.01f, 0.07f, Port);                   // open-top slide
            Z(0, 0.042f, 0.15f, 0.2f, 0.03f, DarkMetal);                       // compensator
            for (int i = 0; i < 3; i++) B(0, 0.058f, 0.16f + i * 0.013f, 0.012f, 0.004f, 0.006f, Port);
            B(0, -0.005f, 0.11f, 0.012f, 0.045f, 0.012f, DarkMetal, 10);       // folding foregrip
            B(0, -0.03f, 0.115f, 0.03f, 0.008f, 0.012f, DarkMetal);
            Muzzle(0.042f, 0.21f);
            Fore(-0.02f, 0.11f);
        }

        // ---------------- shotguns ----------------

        void PumpShotgun(bool slug)
        {
            // Remington 870: rounded receiver, tube magazine under the barrel, ribbed wooden pump
            var wood = slug ? Polymer : Wood;
            RifleStock(0.045f, -0.04f, 0.34f, wood);
            B(0, 0.05f, 0.05f, 0.036f, 0.06f, 0.2f, slug ? DarkMetal : Metal);
            B(0, 0.08f, 0.05f, 0.028f, 0.01f, 0.2f, slug ? DarkMetal : Metal);
            Port_(0.019f, 0.055f, 0.06f, 0.025f, 0.06f);
            TriggerAndGuard(-0.005f, 0.022f, false, 0.06f);
            float barrelEnd = slug ? 0.75f : 0.7f;
            Z(0, 0.068f, 0.15f, barrelEnd, 0.024f, slug ? DarkMetal : Metal);
            B(0, 0.082f, (0.15f + barrelEnd) * 0.5f, 0.008f, 0.004f, barrelEnd - 0.15f, Metal);   // vent rib
            Z(0, 0.04f, 0.15f, barrelEnd - 0.08f, 0.024f, Metal);              // magazine tube
            Z(0, 0.04f, barrelEnd - 0.09f, barrelEnd - 0.07f, 0.028f, DarkMetal);
            var pump = Prims.Empty("pump", v, new Vector3(0, 0.044f, 0.32f)).transform;
            Prims.Box(pump, Vector3.zero, new Vector3(0.042f, 0.042f, 0.16f), wood, false);
            for (int i = 0; i < 9; i++) Prims.Box(pump, new Vector3(0, -0.019f, -0.068f + i * 0.017f), new Vector3(0.044f, 0.006f, 0.006f), slug ? Grip : DarkWood, false);
            pumpPart = pump;
            if (slug)
            {
                Rail(0.087f, -0.02f, 0.12f);
                RedDot(0.09f, 0.05f);
                // heat shield over the rifled slug barrel
                B(0, 0.085f, 0.4f, 0.03f, 0.006f, 0.2f, DarkMetal);
                for (int i = 0; i < 5; i++) B(0, 0.085f, 0.32f + i * 0.04f, 0.026f, 0.007f, 0.012f, Port);
            }
            else Ball(0, 0.085f, barrelEnd - 0.01f, 0.006f, 0.006f, 0.006f, Brass);   // brass bead
            B(-0.02f, 0.05f, 0.0f, 0.004f, 0.008f, 0.012f, accent);            // safety
            Muzzle(0.068f, barrelEnd + 0.01f);
            Eject(0.055f, 0.06f);
            Ammo(0.03f, 0.06f, -0.05f);
            Well(0.02f, 0.08f);
            Fore(0.02f, 0.32f);
        }

        void SawedOff()
        {
            // side-by-side, cut down: pistol-grip wood stock, short barrels, a big hinge
            PistolGrip(0, 0.1f, Wood, 24f, 0.032f, 0.044f);
            B(0, 0.04f, 0.03f, 0.044f, 0.045f, 0.1f, Metal);
            Ax(new Vector3(0, 0.045f, 0.085f), 0.02f, 0.05f, DarkMetal, Vector3.right);
            Z(-0.012f, 0.055f, 0.08f, 0.33f, 0.024f, Metal);
            Z(0.012f, 0.055f, 0.08f, 0.33f, 0.024f, Metal);
            Z(-0.012f, 0.055f, 0.325f, 0.331f, 0.016f, Port);
            Z(0.012f, 0.055f, 0.325f, 0.331f, 0.016f, Port);
            B(0, 0.069f, 0.2f, 0.01f, 0.004f, 0.24f, Metal);
            B(0, 0.035f, 0.17f, 0.04f, 0.03f, 0.14f, Wood);                    // forend
            B(0, 0.065f, -0.02f, 0.008f, 0.012f, 0.012f, Metal);               // top lever
            for (int s = -1; s <= 1; s += 2) B(s * 0.006f, 0.065f, -0.03f, 0.005f, 0.014f, 0.006f, Metal, -30);   // two hammers
            TriggerAndGuard(0.0f, 0.02f, false, 0.06f);
            Muzzle(0.055f, 0.34f);
            Eject(0.055f, 0.08f);
            Ammo(0.03f, 0.05f, -0.03f);
            Well(0.02f, 0.06f);
            Fore(0.015f, 0.17f);
        }

        void Benelli()
        {
            // Benelli M4: black, pistol-grip stock, rail with ghost-ring sights, tube magazine
            PistolGrip(0, 0.1f, Polymer, 18f, 0.03f, 0.038f);
            M4Stock(0.06f, -0.04f, Polymer);
            B(0, 0.055f, 0.06f, 0.038f, 0.06f, 0.2f, DarkMetal);
            Rail(0.087f, -0.03f, 0.15f);
            B(0, 0.1f, -0.02f, 0.02f, 0.02f, 0.015f, DarkMetal);
            Ax(new Vector3(0, 0.106f, -0.02f), 0.014f, 0.004f, Port, Vector3.forward);
            Z(0, 0.07f, 0.16f, 0.65f, 0.024f, Metal);
            Z(0, 0.043f, 0.16f, 0.56f, 0.024f, Metal);
            B(0, 0.048f, 0.28f, 0.044f, 0.04f, 0.17f, Polymer);
            for (int i = 0; i < 6; i++) B(0, 0.048f, 0.21f + i * 0.026f, 0.046f, 0.03f, 0.004f, Grip);
            B(0, 0.088f, 0.62f, 0.008f, 0.016f, 0.012f, DarkMetal);
            Port_(0.02f, 0.06f, 0.07f, 0.025f, 0.06f);
            B(0.022f, 0.06f, 0.03f, 0.01f, 0.012f, 0.02f, accent);
            TriggerAndGuard(0.0f, 0.025f, false, 0.06f);
            Muzzle(0.07f, 0.66f);
            Eject(0.06f, 0.07f);
            Ammo(0.03f, 0.065f, -0.04f);
            Well(0.025f, 0.08f);
            Fore(0.02f, 0.28f);
        }

        void AA12()
        {
            // AA-12: big boxy body, carry handle, drum underneath
            PistolGrip(0, 0.1f, Polymer, 16f, 0.032f, 0.04f);
            B(0, 0.06f, 0.05f, 0.05f, 0.08f, 0.36f, DarkMetal);
            B(0, 0.04f, -0.2f, 0.046f, 0.09f, 0.14f, Polymer);
            B(0, 0.04f, -0.275f, 0.05f, 0.12f, 0.016f, Grip);
            B(0, 0.135f, 0.04f, 0.016f, 0.012f, 0.18f, Metal);                 // carry handle
            foreach (float z in new[] { -0.04f, 0.12f }) B(0, 0.115f, z, 0.016f, 0.03f, 0.014f, Metal);
            Z(0, 0.07f, 0.23f, 0.46f, 0.03f, Metal);
            Z(0, 0.07f, 0.44f, 0.48f, 0.042f, DarkMetal);
            B(0, 0.04f, 0.31f, 0.05f, 0.04f, 0.12f, Polymer);
            for (int i = 0; i < 6; i++) B(0.026f, 0.06f, -0.08f + i * 0.04f, 0.003f, 0.05f, 0.012f, Port);
            B(0.026f, 0.08f, 0.1f, 0.006f, 0.012f, 0.04f, accent);
            TriggerAndGuard(0.0f, 0.02f, true, 0.06f);
            Muzzle(0.07f, 0.49f);
            Eject(0.07f, 0.06f);
            Ammo(0.032f, 0.08f, -0.08f);
            Well(0.02f, 0.11f);
            Fore(0.02f, 0.31f);
        }

        // ---------------- rifles ----------------

        /// <summary>An AR-15 type receiver: lower, upper, forward assist, charging handle and the top rail.</summary>
        void ArReceiver(Material upper, Material lower, float y = 0.06f)
        {
            PistolGrip(0, 0.1f, Polymer, 18f, 0.03f, 0.036f);
            B(0, y - 0.03f, 0.03f, 0.03f, 0.036f, 0.2f, lower);
            B(0, y - 0.045f, 0.075f, 0.026f, 0.025f, 0.075f, lower);             // magwell flare
            B(0, y + 0.01f, 0.04f, 0.034f, 0.044f, 0.24f, upper);
            Rail(y + 0.035f, -0.075f, 0.155f);
            B(0, y + 0.02f, -0.085f, 0.02f, 0.012f, 0.02f, DarkMetal);           // charging handle
            Ax(new Vector3(0.022f, y + 0.012f, -0.035f), 0.012f, 0.018f, DarkMetal, Vector3.forward);   // forward assist
            Port_(0.0175f, y + 0.008f, 0.05f, 0.02f, 0.06f);
            B(0.0185f, y - 0.002f, 0.05f, 0.002f, 0.004f, 0.065f, Metal);       // dust cover hinge
            B(-0.017f, y - 0.03f, 0.01f, 0.004f, 0.008f, 0.012f, accent);       // fire selector
            B(-0.016f, y - 0.025f, 0.09f, 0.004f, 0.01f, 0.01f, DarkMetal);     // mag release
            TriggerAndGuard(0.0f, y - 0.045f, true, 0.06f);
        }

        void Handguard(float y, float z0, float z1, float d, Material m, bool slots = true)
        {
            Z(0, y, z0, z1, d, m);
            Rail(y + d * 0.5f, z0 + 0.005f, z1 - 0.005f);
            if (slots)
                foreach (float s in new[] { -1f, 1f })
                    for (float z = z0 + 0.03f; z < z1 - 0.02f; z += 0.04f) B(s * d * 0.49f, y, z, 0.003f, d * 0.3f, 0.022f, Port);
            Z(0, y, z1 - 0.008f, z1, d * 0.55f, DarkMetal);
        }

        void Ar15()
        {
            ArReceiver(DarkMetal, DarkMetal);
            M4Stock(0.06f, -0.07f, Polymer);
            Handguard(0.065f, 0.16f, 0.38f, 0.05f, Polymer);
            Z(0, 0.065f, 0.38f, 0.49f, 0.016f, Metal);
            B(0, 0.095f, 0.43f, 0.012f, 0.05f, 0.016f, DarkMetal);             // front sight post
            B(0, 0.12f, 0.43f, 0.003f, 0.012f, 0.004f, DarkMetal);
            FlashHider(0.065f, 0.49f, 0.016f);
            RedDot(0.098f, 0.03f);
            B(0, 0.022f, 0.24f, 0.022f, 0.06f, 0.024f, Polymer, 8);            // vertical grip
            Muzzle(0.065f, 0.54f);
            Eject(0.07f, 0.05f);
            Ammo(0.03f, 0.075f, -0.05f);
            Well(0.012f, 0.075f);
            Fore(0.0f, 0.24f);
        }

        void Dmr()
        {
            ArReceiver(Tan, Tan);
            SkeletonStock(0.06f, -0.07f, Tan);
            Handguard(0.065f, 0.16f, 0.46f, 0.052f, Tan);
            Z(0, 0.065f, 0.46f, 0.66f, 0.02f, Metal);
            MuzzleBrake(0.065f, 0.64f, 0.02f);
            Scope(0.095f, -0.05f, 0.21f, 0.03f, 0.05f);
            Bipod(0.04f, 0.44f);
            Muzzle(0.065f, 0.7f);
            Eject(0.07f, 0.05f);
            Ammo(0.03f, 0.075f, -0.05f);
            Well(0.012f, 0.075f);
            Fore(0.02f, 0.3f);
        }

        void BoltRifle(Material stock, bool bigBore, float barrel)
        {
            // M24 / Remington 700 style: one-piece stock, round receiver, bolt handle, heavy barrel
            RifleStock(0.05f, -0.05f, 0.34f, stock);
            B(0, 0.04f, 0.18f, 0.044f, 0.045f, 0.42f, stock);                    // fore-end
            B(0, 0.022f, 0.3f, 0.04f, 0.02f, 0.2f, stock);
            Z(0, 0.065f, -0.07f, 0.13f, 0.034f, Metal);                          // receiver
            Ax(new Vector3(0.022f, 0.07f, -0.03f), 0.012f, 0.05f, Metal, new Vector3(1, -0.4f, 0));
            Ball(0.048f, 0.058f, -0.03f, 0.022f, 0.022f, 0.022f, Metal);         // bolt knob
            Z(0, 0.065f, -0.1f, -0.07f, 0.026f, Metal);                          // bolt shroud
            Port_(0.017f, 0.07f, 0.03f, 0.016f, 0.06f);
            float d = bigBore ? 0.026f : 0.022f;
            Z(0, 0.065f, 0.13f, 0.13f + barrel, d, Metal);
            if (bigBore) MuzzleBrake(0.065f, 0.13f + barrel - 0.04f, d);
            Scope(0.083f, -0.06f, 0.22f, 0.03f, bigBore ? 0.05f : 0.056f);
            if (!bigBore) Bipod(0.03f, 0.34f);
            else for (int i = 0; i < 2; i++) Ax(new Vector3(0, 0.02f, 0.3f + i * 0.02f), 0.05f, 0.006f, Brass, Vector3.forward);
            TriggerAndGuard(0.0f, 0.02f, false, 0.06f);
            Muzzle(0.065f, 0.14f + barrel + (bigBore ? 0.02f : 0f));
            Eject(0.07f, 0.03f);
            Ammo(0.03f, 0.075f, -0.07f);
            Well(0.02f, 0.06f);
            Fore(0.02f, 0.3f);
        }

        void Awp()
        {
            // AWM style: green thumbhole stock, long fluted barrel, big brake, big scope
            var green = Prims.Lit(new Color(0.24f, 0.32f, 0.2f), 0.3f, 0f, Surface.Plastic);
            RifleStock(0.05f, -0.05f, 0.34f, green, true);
            B(0, 0.04f, 0.15f, 0.05f, 0.05f, 0.36f, green);
            Z(0, 0.065f, -0.07f, 0.13f, 0.036f, Metal);
            Ax(new Vector3(0.024f, 0.07f, -0.03f), 0.012f, 0.05f, Metal, new Vector3(1, -0.4f, 0));
            Ball(0.05f, 0.058f, -0.03f, 0.024f, 0.024f, 0.024f, Metal);
            Z(0, 0.065f, 0.13f, 0.72f, 0.022f, Metal);
            foreach (float s in new[] { -1f, 1f }) B(s * 0.011f, 0.065f, 0.4f, 0.003f, 0.006f, 0.5f, Port);
            MuzzleBrake(0.065f, 0.72f, 0.024f);
            Scope(0.085f, -0.07f, 0.24f, 0.032f, 0.06f);
            Bipod(0.03f, 0.3f);
            TriggerAndGuard(0.0f, 0.02f, false, 0.06f);
            Muzzle(0.065f, 0.78f);
            Eject(0.07f, 0.03f);
            Ammo(0.03f, 0.075f, -0.07f);
            Well(0.02f, 0.06f);
            Fore(0.02f, 0.28f);
        }

        void Vss()
        {
            // VSS Vintorez: skeleton wooden stock, fat integral suppressor, scope
            PistolGrip(0, 0.09f, Wood, 22f, 0.03f, 0.04f);
            SkeletonStock(0.055f, -0.06f, Wood);
            B(0, 0.055f, 0.05f, 0.034f, 0.05f, 0.22f, DarkMetal);
            B(0, 0.03f, 0.18f, 0.04f, 0.04f, 0.14f, Wood);
            Suppressor(0.065f, 0.15f, 0.34f, 0.04f);
            Scope(0.082f, -0.04f, 0.17f, 0.028f, 0.044f);
            Port_(0.0175f, 0.06f, 0.06f, 0.02f, 0.05f);
            B(0.019f, 0.07f, -0.02f, 0.008f, 0.01f, 0.03f, Metal);
            TriggerAndGuard(0.0f, 0.03f, false, 0.06f);
            Muzzle(0.065f, 0.5f);
            Eject(0.065f, 0.06f);
            Ammo(0.03f, 0.065f, -0.05f);
            Well(0.03f, 0.08f);
            Fore(0.0f, 0.18f);
        }

        void Aug()
        {
            // Steyr AUG bullpup: the magazine goes in BEHIND the grip; integrated optic, folding foregrip
            var green = Prims.Lit(new Color(0.24f, 0.29f, 0.2f), 0.3f, 0f, Surface.Plastic);
            PistolGrip(0, 0.1f, green, 10f, 0.032f, 0.04f);
            B(0, 0.05f, -0.15f, 0.05f, 0.09f, 0.3f, green);                    // the stock is the receiver
            B(0, 0.085f, -0.17f, 0.044f, 0.03f, 0.24f, green);                 // cheek rest
            B(0, 0.03f, -0.305f, 0.052f, 0.13f, 0.018f, Grip);
            B(0, 0.0f, 0.05f, 0.012f, 0.014f, 0.1f, green);                     // the big trigger guard
            B(0, -0.035f, 0.1f, 0.03f, 0.008f, 0.012f, green);
            B(0, 0.06f, 0.08f, 0.036f, 0.04f, 0.14f, green);
            Z(0, 0.065f, 0.15f, 0.42f, 0.02f, Metal);
            FlashHider(0.065f, 0.42f, 0.02f);
            B(0, 0.01f, 0.13f, 0.024f, 0.07f, 0.024f, green, 12);              // foregrip
            // integrated 1.5x optic on a tall housing
            B(0, 0.105f, -0.02f, 0.03f, 0.03f, 0.14f, green);
            Z(0, 0.135f, -0.07f, 0.08f, 0.034f, green);
            Prims.Cyl(v, new Vector3(0, 0.135f, 0.081f), 0.028f, 0.002f, Glass, Vector3.forward);
            Prims.Cyl(v, new Vector3(0, 0.135f, -0.071f), 0.026f, 0.002f, Glass, Vector3.forward);
            Port_(0.026f, 0.06f, -0.08f, 0.025f, 0.05f);
            B(-0.026f, 0.04f, -0.02f, 0.004f, 0.012f, 0.02f, accent);
            Muzzle(0.065f, 0.47f);
            Eject(0.06f, -0.08f);
            Ammo(0.03f, 0.08f, -0.2f);
            Well(0.005f, -0.1f);
            Fore(-0.02f, 0.13f);
        }

        void Ak()
        {
            // AK: stamped receiver, wood furniture, gas tube, slant brake, banana mag
            PistolGrip(0, 0.095f, DarkWood, 20f, 0.03f, 0.038f);
            B(0, 0.04f, 0.07f, 0.034f, 0.05f, 0.25f, Metal);
            Z(0, 0.064f, -0.05f, 0.19f, 0.034f, Metal);                          // rounded dust cover
            for (int i = 0; i < 6; i++) B(0, 0.08f, -0.03f + i * 0.04f, 0.022f, 0.004f, 0.008f, DarkMetal);
            B(0.018f, 0.055f, 0.06f, 0.004f, 0.012f, 0.07f, DarkMetal);         // charging handle slot
            B(0.025f, 0.055f, 0.1f, 0.012f, 0.01f, 0.015f, Metal);
            B(0.018f, 0.035f, 0.02f, 0.003f, 0.02f, 0.09f, Metal);              // safety lever
            B(0, 0.035f, -0.12f, 0.03f, 0.05f, 0.09f, Wood, 10);                // wood stock, dropping down
            B(0, 0.015f, -0.25f, 0.032f, 0.08f, 0.2f, Wood, 8);
            B(0, 0.005f, -0.355f, 0.036f, 0.1f, 0.012f, Metal);
            B(0, 0.035f, 0.26f, 0.04f, 0.04f, 0.14f, Wood);                     // lower handguard
            B(0, 0.075f, 0.25f, 0.026f, 0.022f, 0.12f, Wood);                   // upper handguard over the gas tube
            Z(0, 0.075f, 0.31f, 0.38f, 0.016f, Metal);                           // gas tube
            Z(0, 0.05f, 0.19f, 0.5f, 0.017f, Metal);
            B(0, 0.065f, 0.39f, 0.02f, 0.04f, 0.022f, Metal);                   // gas block
            B(0, 0.085f, 0.48f, 0.014f, 0.035f, 0.02f, Metal);                  // front sight
            B(0, 0.085f, 0.18f, 0.02f, 0.01f, 0.03f, Metal);                    // rear sight
            Z(0, 0.05f, 0.5f, 0.54f, 0.024f, DarkMetal);                         // slant brake
            B(0, 0.06f, 0.53f, 0.024f, 0.006f, 0.03f, DarkMetal, -20);
            TriggerAndGuard(0.0f, 0.02f, false, 0.06f);
            Muzzle(0.05f, 0.55f);
            Eject(0.06f, 0.06f);
            Ammo(0.03f, 0.065f, -0.05f);
            Well(0.015f, 0.07f, 0f);
            Fore(0.0f, 0.26f);
        }

        void Scar()
        {
            // FN SCAR: tan monolithic upper with a full-length rail, folding stock with cheek riser
            PistolGrip(0, 0.1f, Polymer, 18f, 0.03f, 0.036f);
            B(0, 0.03f, 0.03f, 0.03f, 0.036f, 0.2f, Polymer);
            B(0, 0.075f, 0.12f, 0.038f, 0.055f, 0.42f, Tan);
            Rail(0.106f, -0.08f, 0.32f);
            foreach (float s in new[] { -1f, 1f }) { B(s * 0.021f, 0.065f, 0.25f, 0.006f, 0.012f, 0.14f, DarkMetal); }
            B(-0.024f, 0.09f, 0.05f, 0.012f, 0.012f, 0.02f, DarkMetal);        // side charging handle
            Port_(0.0195f, 0.075f, 0.05f, 0.02f, 0.06f);
            B(0, 0.065f, -0.12f, 0.03f, 0.05f, 0.1f, Tan);
            B(0, 0.095f, -0.14f, 0.024f, 0.02f, 0.1f, Tan);
            B(0, 0.045f, -0.19f, 0.036f, 0.1f, 0.016f, Grip);
            B(0, 0.04f, -0.13f, 0.024f, 0.012f, 0.1f, Tan, -12);
            Z(0, 0.075f, 0.33f, 0.46f, 0.018f, Metal);
            FlashHider(0.075f, 0.46f, 0.018f);
            RedDot(0.11f, 0.05f);
            B(0.01f, 0.015f, 0.08f, 0.004f, 0.01f, 0.01f, accent);
            TriggerAndGuard(0.0f, 0.015f, true, 0.06f);
            Muzzle(0.075f, 0.51f);
            Eject(0.075f, 0.05f);
            Ammo(0.03f, 0.08f, -0.06f);
            Well(0.012f, 0.08f);
            Fore(0.03f, 0.25f);
        }

        void M249()
        {
            // M249: long receiver, feed tray cover, carry handle, heavy barrel, bipod
            PistolGrip(0, 0.1f, Polymer, 18f, 0.032f, 0.038f);
            B(0, 0.06f, 0.07f, 0.05f, 0.07f, 0.3f, DarkMetal);
            B(0, 0.105f, 0.04f, 0.052f, 0.02f, 0.2f, Metal);                    // feed tray cover
            Rail(0.116f, -0.05f, 0.13f);
            SkeletonStock(0.06f, -0.08f, Polymer);
            B(0, 0.04f, 0.32f, 0.05f, 0.03f, 0.2f, Polymer);
            Z(0, 0.07f, 0.22f, 0.6f, 0.026f, Metal);
            B(0, 0.098f, 0.34f, 0.03f, 0.01f, 0.18f, DarkMetal);                // heat shield
            for (int i = 0; i < 5; i++) B(0, 0.098f, 0.27f + i * 0.035f, 0.026f, 0.011f, 0.012f, Port);
            B(0, 0.13f, 0.25f, 0.012f, 0.012f, 0.1f, Metal);                    // carry handle
            foreach (float z in new[] { 0.21f, 0.29f }) B(0, 0.112f, z, 0.012f, 0.03f, 0.012f, Metal);
            FlashHider(0.07f, 0.6f, 0.026f);
            Bipod(0.05f, 0.5f);
            B(0.027f, 0.06f, 0.06f, 0.004f, 0.03f, 0.08f, Port);
            B(-0.028f, 0.07f, 0.0f, 0.008f, 0.012f, 0.02f, accent);
            TriggerAndGuard(0.0f, 0.025f, true, 0.06f);
            Muzzle(0.07f, 0.65f);
            Eject(0.065f, 0.06f);
            Ammo(0.03f, 0.08f, -0.06f);
            Well(0.025f, 0.1f);
            Fore(0.02f, 0.32f);
        }

        void Vector()
        {
            // KRISS Vector: angular body, the mag goes in at an angle in front of the grip
            PistolGrip(0, 0.1f, Polymer, 16f, 0.03f, 0.036f);
            B(0, 0.06f, 0.06f, 0.04f, 0.08f, 0.26f, Polymer);
            B(0, 0.02f, 0.1f, 0.036f, 0.06f, 0.14f, Polymer, -20);
            B(0, 0.025f, 0.16f, 0.034f, 0.05f, 0.05f, Polymer, 25);
            Rail(0.103f, -0.06f, 0.18f);
            B(0, 0.075f, 0.22f, 0.036f, 0.03f, 0.08f, accent);
            Z(0, 0.075f, 0.26f, 0.33f, 0.02f, Metal);
            Suppressor(0.075f, 0.3f, 0.12f, 0.032f);
            M4Stock(0.07f, -0.07f, Polymer);
            foreach (float s in new[] { -1f, 1f }) B(s * 0.021f, 0.07f, 0.05f, 0.003f, 0.03f, 0.1f, accent);
            RedDot(0.107f, 0.04f);
            Port_(0.0205f, 0.08f, 0.08f, 0.02f, 0.04f);
            TriggerAndGuard(0.0f, 0.02f, true, 0.06f);
            Muzzle(0.075f, 0.43f);
            Eject(0.08f, 0.08f);
            Ammo(0.03f, 0.08f, -0.05f);
            Well(0.02f, 0.06f, 0f);
            Fore(0.0f, 0.15f);
        }

        /* ======================= merging ======================= */

        /// <summary>
        /// Merge the gun's many little parts into one mesh per material, so a
        /// detailed gun costs a handful of draw calls instead of a hundred.
        /// (Parts under a child with its own children, like the pump, stay separate.)
        /// </summary>
        static void CombineParts(Transform vis)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var done = new List<GameObject>();
            var toLocal = vis.worldToLocalMatrix;
            foreach (Transform t in vis)
            {
                var go = t.gameObject;
                var mf = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null || t.childCount > 0 || go.GetComponents<Component>().Length != 3) continue;
                if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * go.transform.localToWorldMatrix });
                done.Add(go);
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "fz_gunpart" };
                if (kv.Value.Count * 24 > 60000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var part = new GameObject("parts");
                part.transform.SetParent(vis, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            foreach (var go in done) DestroyImmediate(go);
        }

        /* ======================= magazines ======================= */

        /// <summary>The detachable magazine for this gun. Its origin sits on the gun's magwell; it hangs down (-Y).</summary>
        public static GameObject BuildMag(GunDef def, Transform parent)
        {
            var go = new GameObject("mag:" + def.id);
            go.transform.SetParent(parent, false);
            var t = go.transform;
            var steel = Prims.Lit(new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.6f, Surface.Metal);
            var poly = Prims.Lit(new Color(0.12f, 0.12f, 0.13f), 0.3f, 0f, Surface.Plastic);
            var brass = Brass;
            switch (def.id)
            {
                case "revolt":
                {
                    // a speed loader: six rounds round a knob
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Prims.Cyl(t, new Vector3(Mathf.Cos(a) * 0.016f, -0.015f, Mathf.Sin(a) * 0.016f), 0.009f, 0.03f, brass, Vector3.up);
                    }
                    Prims.Cyl(t, new Vector3(0, -0.035f, 0), 0.045f, 0.012f, steel, Vector3.up);
                    Prims.Cyl(t, new Vector3(0, -0.05f, 0), 0.02f, 0.02f, poly, Vector3.up);
                    break;
                }
                case "brute":
                case "hmr":
                {
                    // a drum
                    Prims.Box(t, new Vector3(0, -0.02f, 0), new Vector3(0.03f, 0.04f, 0.05f), steel, false);
                    Prims.Cyl(t, new Vector3(0, -0.08f, 0), 0.11f, 0.06f, def.id == "hmr" ? Olive : poly, Vector3.right);
                    Prims.Cyl(t, new Vector3(0.031f, -0.08f, 0), 0.05f, 0.004f, steel, Vector3.right);
                    Prims.Cyl(t, new Vector3(-0.031f, -0.08f, 0), 0.08f, 0.004f, steel, Vector3.right);
                    break;
                }
                case "rattle":
                case "bravo":
                case "bulldog":
                case "viper":
                {
                    // curved rifle mags: STANAG (a gentle curve) or AK (a real banana)
                    bool ak = def.id == "viper";
                    var m = ak ? Prims.Lit(new Color(0.42f, 0.22f, 0.1f), 0.35f, 0f, Surface.Plastic) : def.id == "bravo" ? Tan : steel;
                    float curve = ak ? 9f : 4f, seg = ak ? 0.038f : 0.04f;
                    var p = Vector3.zero;
                    var dir = Quaternion.identity;
                    for (int i = 0; i < 4; i++)
                    {
                        var c = p + dir * new Vector3(0, -seg * 0.5f, 0);
                        Prims.Box(t, c, new Vector3(0.024f, seg * 1.02f, ak ? 0.06f : 0.062f), m, false, dir);
                        Prims.Box(t, c + dir * new Vector3(0.0125f, 0, 0), new Vector3(0.002f, seg * 0.8f, 0.03f), steel, false, dir);
                        p += dir * new Vector3(0, -seg, 0);
                        dir *= Quaternion.Euler(-curve, 0, 0);
                    }
                    Prims.Box(t, p + dir * new Vector3(0, -0.005f, 0), new Vector3(0.028f, 0.01f, 0.068f), m, false, dir);
                    Prims.Box(t, new Vector3(0, -0.003f, 0.005f), new Vector3(0.016f, 0.006f, 0.04f), brass, false);
                    break;
                }
                case "spitfire":
                case "buzz":
                case "hammer":
                {
                    // long, straight stick mags (with a base pad)
                    float len = def.id == "spitfire" ? 0.17f : def.id == "buzz" ? 0.19f : 0.15f;
                    Prims.Box(t, new Vector3(0, -len * 0.5f, 0), new Vector3(0.022f, len, 0.032f), steel, false);
                    Prims.Box(t, new Vector3(0, -len - 0.005f, 0.002f), new Vector3(0.026f, 0.012f, 0.038f), poly, false);
                    Prims.Box(t, new Vector3(0, -0.004f, 0), new Vector3(0.012f, 0.006f, 0.022f), brass, false);
                    break;
                }
                case "boom":
                case "slug":
                case "thunder":
                case "sawed":
                {
                    // a shell holder: red shells with brass bases side by side
                    int n = def.id == "sawed" ? 2 : 4;
                    var shell = Prims.Lit(new Color(0.75f, 0.1f, 0.08f), 0.35f, 0f, Surface.Plastic);
                    Prims.Box(t, new Vector3(0, -0.035f, 0), new Vector3(0.03f, 0.07f, 0.024f * n + 0.006f), poly, false);
                    for (int i = 0; i < n; i++)
                    {
                        float z = (i - (n - 1) * 0.5f) * 0.024f;
                        Prims.Cyl(t, new Vector3(0, -0.035f, z), 0.021f, 0.07f, shell, Vector3.up);
                        Prims.Cyl(t, new Vector3(0, -0.005f, z), 0.022f, 0.012f, brass, Vector3.up);
                    }
                    break;
                }
                default:
                {
                    // straight pistol and rifle box mags
                    bool pistol = def.cls == GunClass.Pistol;
                    float len = pistol ? 0.09f : 0.075f, depth = pistol ? 0.03f : 0.06f;
                    var m = def.id == "marksman" ? Tan : steel;
                    Prims.Box(t, new Vector3(0, -len * 0.5f, 0), new Vector3(pistol ? 0.022f : 0.026f, len, depth), m, false);
                    Prims.Box(t, new Vector3(0, -len - 0.005f, 0.002f), new Vector3(pistol ? 0.026f : 0.03f, 0.01f, depth + 0.006f), poly, false);
                    Prims.Box(t, new Vector3(0, -0.004f, 0), new Vector3(0.012f, 0.006f, depth * 0.7f), brass, false);
                    for (int i = 0; i < 3; i++)
                        Prims.Cyl(t, new Vector3(0.0115f, -len * (0.25f + i * 0.2f), 0), 0.005f, 0.002f, Port, Vector3.right);
                    break;
                }
            }
            return go;
        }
    }
}
