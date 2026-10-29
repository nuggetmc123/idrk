using System;

namespace Flopzone
{
    /// <summary>
    /// The shape of every gun, drawn as real side profiles. Coordinates are metres in the
    /// gun's frame: +Z toward the muzzle, +Y up, origin where the hand holds the grip.
    /// Profiles are (z, y) pairs going round the outline, as seen from the right side.
    /// </summary>
    public static partial class GunShapes
    {
        /* ======================= shared parts ======================= */

        /// <summary>Picatinny rail along the top: a base with cross slots.</summary>
        static void Rail(GunMesh g, float y, float z0, float z1, float w = 0.021f, GM m = GM.Dark)
        {
            g.Box(m, 0, y + 0.003f, (z0 + z1) / 2, w * 0.75f, 0.006f, z1 - z0, 0.001f);
            for (float z = z0 + 0.004f; z < z1 - 0.003f; z += 0.0095f)
                g.Box(m, 0, y + 0.0075f, z + 0.0025f, w, 0.004f, 0.005f, 0.0008f);
        }

        /// <summary>
        /// A contoured pistol grip: a curved front strap, a palm swell at the back, a flared base.
        /// top = height of its top edge, len = how far down it goes, lean = how far the bottom sits behind the top.
        /// </summary>
        static void PistolGrip(GunMesh g, GM m, float top = 0.025f, float len = 0.1f, float lean = 0.03f, float w = 0.03f, float depth = 0.038f, bool fingerGrooves = false)
        {
            float d = depth, by = top - len, bz = -lean;
            // front strap: from under the trigger guard down to the base, bowing out slightly
            float fz0 = 0.014f, fz1 = bz + 0.004f;
            float rz0 = fz0 - d - 0.004f, rz1 = bz - d + 0.002f;
            var pts = new System.Collections.Generic.List<float>
            {
                fz0, top, rz0 - 0.004f, top,
                rz0 - 0.006f, top - len * 0.12f,               // the web of the hand
                rz0 + (rz1 - rz0) * 0.45f - 0.006f, top - len * 0.5f,   // palm swell
                rz1 - 0.003f, by + 0.012f, rz1, by,            // flared base
                fz1 + 0.004f, by, fz1 + 0.006f, by + 0.008f,
                fz0 + (fz1 - fz0) * 0.7f + 0.004f, top - len * 0.68f,
                fz0 + (fz1 - fz0) * 0.35f + 0.005f, top - len * 0.36f,
                fz0 + 0.002f, top - 0.012f,
            };
            g.Profile(m, 0, w, Math.Min(0.008f, w * 0.28f), pts.ToArray());
            // a stippled panel on each side
            g.With(new P3(0, top - len * 0.5f, (fz0 + rz0) * 0.5f + bz * 0.5f), (float)(Math.Atan2(lean, len) * 180 / Math.PI), 0, 0, new P3(0, 0, 0), () =>
            {
                foreach (float s in new[] { -1f, 1f })
                    g.Box(GM.Rubber, s * w * 0.5f, top - len * 0.5f, (fz0 + rz0) * 0.5f + bz * 0.5f, 0.002f, len * 0.55f, d * 0.6f, 0.0006f);
            });
        }

        /// <summary>Trigger guard (a rounded loop in front of the grip) and a curved trigger.</summary>
        static void TriggerGuard(GunMesh g, float y, float z0 = 0.012f, float z1 = 0.068f, GM m = GM.Dark, bool square = false)
        {
            float t = 0.0045f;
            if (square)
                g.Profile(m, 0, 0.012f, 0.0015f, z0, y - 0.032f, z1, y - 0.032f, z1, y, z1 - t, y, z1 - t, y - 0.032f + t, z0, y - 0.032f + t);
            else
                g.Profile(m, 0, 0.012f, 0.0015f,
                    z0, y - 0.03f, z1 - 0.012f, y - 0.03f, z1 - 0.002f, y - 0.022f, z1, y,
                    z1 - t, y, z1 - t - 0.002f, y - 0.019f, z1 - 0.014f, y - 0.03f + t, z0, y - 0.03f + t);
            // the trigger: a thin curved blade
            float tz = (z0 + z1) * 0.5f - 0.004f;
            g.Profile(GM.Dark, 0, 0.006f, 0.0012f, tz - 0.003f, y + 0.002f, tz + 0.003f, y + 0.002f, tz + 0.006f, y - 0.01f, tz + 0.002f, y - 0.022f, tz - 0.001f, y - 0.022f, tz + 0.001f, y - 0.01f);
        }

        /// <summary>Front sight post on a block, rear aperture.</summary>
        static void IronSights(GunMesh g, float y, float zFront, float zRear, GM m = GM.Dark)
        {
            g.Box(m, 0, y + 0.004f, zFront, 0.012f, 0.008f, 0.012f);
            g.Box(m, 0, y + 0.013f, zFront, 0.003f, 0.012f, 0.004f, 0.0005f);
            g.Box(m, 0, y + 0.004f, zRear, 0.02f, 0.008f, 0.01f);
            g.Box(m, -0.0065f, y + 0.012f, zRear, 0.004f, 0.01f, 0.008f, 0.0006f);
            g.Box(m, 0.0065f, y + 0.012f, zRear, 0.004f, 0.01f, 0.008f, 0.0006f);
        }

        /// <summary>A red dot sight on a rail (y = top of the rail).</summary>
        static void RedDot(GunMesh g, float y, float z)
        {
            g.Profile(GM.Dark, 0, 0.026f, 0.002f, z - 0.024f, y, z + 0.024f, y, z + 0.022f, y + 0.012f, z - 0.022f, y + 0.012f);
            g.TubeZ(GM.Dark, 0, y + 0.03f, z - 0.024f, z + 0.024f, 0.019f, -1, 16, false);
            g.TubeZ(GM.Dark, 0, y + 0.03f, z - 0.024f, z - 0.02f, 0.021f);
            g.TubeZ(GM.Dark, 0, y + 0.03f, z + 0.02f, z + 0.024f, 0.021f);
            g.Profile(GM.Dark, 0, 0.012f, 0.002f, z - 0.016f, y + 0.008f, z + 0.016f, y + 0.008f, z + 0.012f, y + 0.014f, z - 0.012f, y + 0.014f);
            g.TubeZ(GM.Glass, 0, y + 0.03f, z - 0.018f, z + 0.018f, 0.0175f, -1, 16, true);
            g.TubeZ(GM.Glow, 0, y + 0.03f, z - 0.0185f, z - 0.018f, 0.0018f, -1, 8);
            g.TubeZ(GM.Dark, 0.021f, y + 0.03f, z - 0.006f, z + 0.006f, 0.006f);
            g.Tube(GM.Dark, new P3(0.012f, y + 0.03f, z + 0.002f), new P3(0.026f, y + 0.03f, z + 0.002f), 0.0055f, 0.0055f, 10);
        }

        /// <summary>A rifle scope on rings (y = top of the rail). Sets the scopeEye/scopeFront anchors.</summary>
        static void Scope(GunMesh g, float y, float zBack, float zFront, float tube = 0.0152f, float bell = 0.026f)
        {
            float cy = y + bell + 0.006f;
            float len = zFront - zBack;
            foreach (float z in new[] { zBack + len * 0.33f, zBack + len * 0.64f })
            {
                g.Profile(GM.Dark, 0, 0.022f, 0.002f, z - 0.011f, y, z + 0.011f, y, z + 0.008f, cy, z - 0.008f, cy);
                g.TubeZ(GM.Dark, 0, cy, z - 0.009f, z + 0.009f, tube + 0.004f);
            }
            g.TubeZ(GM.Metal, 0, cy, zBack + len * 0.2f, zFront - len * 0.24f, tube, -1, 20);
            // eyepiece
            g.TubeZ(GM.Metal, 0, cy, zBack, zBack + len * 0.12f, bell * 0.85f, bell * 0.82f, 20);
            g.TubeZ(GM.Metal, 0, cy, zBack + len * 0.12f, zBack + len * 0.2f, bell * 0.8f, tube, 20, false);
            g.TubeZ(GM.Rubber, 0, cy, zBack - 0.008f, zBack, bell * 0.86f, bell * 0.86f, 20);
            // objective bell
            g.TubeZ(GM.Metal, 0, cy, zFront - len * 0.24f, zFront - len * 0.16f, tube, bell, 20, false);
            g.TubeZ(GM.Metal, 0, cy, zFront - len * 0.16f, zFront, bell, bell, 20);
            g.TubeZ(GM.Accent, 0, cy, zFront - 0.006f, zFront, bell * 1.03f, bell * 1.03f, 20);
            g.TubeZ(GM.Glass, 0, cy, zFront, zFront + 0.0008f, bell * 0.85f, -1, 20);
            // turrets: elevation on top, windage on the right, parallax on the left
            float tz = zBack + len * 0.5f;
            g.Tube(GM.Metal, new P3(0, cy + tube * 0.6f, tz), new P3(0, cy + tube + 0.018f, tz), 0.011f, 0.011f, 14);
            g.Tube(GM.Dark, new P3(0, cy + tube + 0.012f, tz), new P3(0, cy + tube + 0.02f, tz), 0.012f, 0.012f, 14);
            g.Tube(GM.Metal, new P3(tube * 0.6f, cy, tz), new P3(tube + 0.016f, cy, tz), 0.011f, 0.011f, 14);
            g.Tube(GM.Dark, new P3(-tube * 0.6f, cy, tz), new P3(-tube - 0.01f, cy, tz), 0.009f, 0.009f, 14);
            g.Box(GM.Metal, 0, cy, tz, tube * 2.3f, tube * 1.9f, 0.04f, 0.004f);
            g.Anchor("scopeEye", cy, zBack - 0.01f);
            g.Anchor("scopeFront", cy, zFront + 0.012f);
            g.Anchor("scopeRadius", bell * 0.8f, 0);
        }

        static void Suppressor(GunMesh g, float y, float z0, float len, float r)
        {
            g.TubeZ(GM.Dark, 0, y, z0, z0 + 0.012f, r * 0.7f, r, 20);
            g.TubeZ(GM.Dark, 0, y, z0 + 0.012f, z0 + len, r, r, 20);
            for (int i = 1; i < 5; i++) g.TubeZ(GM.Metal, 0, y, z0 + len * i / 5f - 0.002f, z0 + len * i / 5f + 0.002f, r * 1.02f, -1, 20, false);
            g.Bore(y, z0 + len, r * 0.35f);
        }

        static void FlashHider(GunMesh g, float y, float z, float r)
        {
            g.TubeZ(GM.Dark, 0, y, z, z + 0.012f, r * 0.9f, r * 1.25f, 12);
            g.TubeZ(GM.Dark, 0, y, z + 0.012f, z + 0.048f, r * 1.25f, r * 1.25f, 12);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * (float)Math.PI / 180f;
                g.Box(GM.Port, (float)Math.Cos(a) * r * 1.2f, y + (float)Math.Sin(a) * r * 1.2f, z + 0.034f, 0.003f, 0.003f, 0.026f, 0.0003f);
            }
            g.Bore(y, z + 0.048f, r * 0.75f);
        }

        static void MuzzleBrake(GunMesh g, float y, float z, float r, float len = 0.06f)
        {
            g.TubeZ(GM.Dark, 0, y, z, z + len, r * 1.45f, r * 1.45f, 16);
            g.Box(GM.Dark, 0, y, z + len * 0.5f, r * 3.2f, r * 2.3f, len * 0.9f, 0.003f);
            for (int i = 0; i < 3; i++)
                foreach (float s in new[] { -1f, 1f })
                    g.Box(GM.Port, s * r * 1.62f, y, z + len * (0.25f + i * 0.25f), 0.003f, r * 1.7f, len * 0.13f, 0.0004f);
            g.Bore(y, z + len, r * 0.6f);
        }

        /// <summary>M4-style collapsible stock on its buffer tube; z = back of the receiver, y = the tube's centre.</summary>
        static void M4Stock(GunMesh g, float y, float z, GM m)
        {
            g.TubeZ(GM.Metal, 0, y, z - 0.19f, z, 0.0145f, -1, 16);
            g.Box(GM.Dark, 0, y, z - 0.006f, 0.034f, 0.034f, 0.012f, 0.002f);   // castle nut
            float zb = z - 0.27f;
            g.Profile(m, 0, 0.04f, 0.005f,
                z - 0.13f, y + 0.02f, zb + 0.012f, y + 0.022f, zb, y + 0.012f, zb, y - 0.085f, zb + 0.012f, y - 0.092f,
                z - 0.185f, y - 0.035f, z - 0.15f, y - 0.022f, z - 0.13f, y - 0.022f);
            g.Profile(GM.Rubber, 0, 0.042f, 0.004f, zb - 0.014f, y + 0.02f, zb + 0.002f, y + 0.02f, zb + 0.002f, y - 0.09f, zb - 0.014f, y - 0.088f);
            g.Box(GM.Dark, 0, y - 0.03f, z - 0.17f, 0.03f, 0.01f, 0.03f, 0.002f);   // adjustment lever
        }

        /// <summary>
        /// A one-piece rifle stock from the grip back: the wrist curves down into a pistol grip
        /// (the hand holds it at the origin), then the belly sweeps back to the butt.
        /// y = line of the bore, len = from the grip to the butt.
        /// </summary>
        static void RifleStock(GunMesh g, float y, float len, GM m, bool thumbhole = false, bool cheek = true)
        {
            float zb = -len;
            float c = cheek ? 0.012f : 0f;
            g.Profile(m, 0, 0.038f, 0.009f,
                0.03f, y - 0.012f,                 // under the action
                -0.03f, y - 0.016f,                // top of the wrist
                -0.11f, y + c * 0.5f,              // comb
                zb + 0.04f, y + c,
                zb, y + c - 0.004f,                // heel
                zb, y - 0.118f,                    // toe
                zb + 0.05f, y - 0.118f,
                -0.16f, y - 0.07f,                 // belly
                -0.085f, y - 0.062f,
                -0.052f, -0.072f, -0.024f, -0.076f,   // bottom of the grip
                0.012f, -0.01f,                    // front of the grip
                0.03f, y - 0.05f);
            if (thumbhole)
                g.Profile(GM.Port, 0, 0.0395f, 0.002f, -0.04f, y - 0.03f, -0.085f, y - 0.03f, -0.07f, y - 0.06f, -0.035f, y - 0.06f);
            g.Profile(GM.Rubber, 0, 0.04f, 0.004f, zb - 0.016f, y + c - 0.004f, zb + 0.002f, y + c - 0.004f, zb + 0.002f, y - 0.118f, zb - 0.016f, y - 0.114f);
            g.Tube(GM.Metal, new P3(-0.02f, y - 0.09f, zb + 0.09f), new P3(0.02f, y - 0.09f, zb + 0.09f), 0.005f, 0.005f, 8);   // sling swivel stud
        }

        static void Bipod(GunMesh g, float y, float z)
        {
            g.Box(GM.Dark, 0, y - 0.008f, z, 0.03f, 0.014f, 0.03f, 0.002f);
            foreach (float s in new[] { -1f, 1f })
            {
                g.Tube(GM.Dark, new P3(s * 0.01f, y - 0.012f, z), new P3(s * 0.013f, y - 0.02f, z - 0.17f), 0.0055f, 0.0055f, 8);
                g.Tube(GM.Rubber, new P3(s * 0.013f, y - 0.02f, z - 0.17f), new P3(s * 0.013f, y - 0.021f, z - 0.19f), 0.0065f, 0.006f, 8);
            }
        }

        /* ======================= the guns ======================= */

        /// <summary>Build a gun by its catalogue id.</summary>
        public static GunMesh Build(string id)
        {
            var g = new GunMesh();
            Pick(g, id);
            return g;
        }

        /// <summary>The AR-15 lower and upper receivers (shared by the AR, the DMR and others).</summary>
        static void ArReceivers(GunMesh g, GM upper, GM lower, float upperLen = 0.23f)
        {
            PistolGrip(g, GM.Polymer);
            // lower receiver, with the flared magwell and the trigger guard
            g.Profile(lower, 0, 0.026f, 0.0025f,
                -0.072f, 0.062f, 0.138f, 0.062f, 0.138f, 0.05f, 0.128f, 0.042f, 0.126f, 0.004f, 0.121f, -0.002f,
                0.058f, -0.002f, 0.052f, 0.006f, 0.052f, 0.026f, -0.03f, 0.026f, -0.052f, 0.04f, -0.072f, 0.044f);
            g.Box(lower, 0, 0.0f, 0.09f, 0.03f, 0.008f, 0.07f, 0.002f);   // magwell lip
            TriggerGuard(g, 0.026f, 0.0f, 0.054f, lower);
            g.Box(GM.Dark, -0.0135f, 0.042f, -0.012f, 0.003f, 0.012f, 0.012f, 0.0008f);   // safety selector
            g.Tube(GM.Accent, new P3(-0.013f, 0.042f, -0.012f), new P3(-0.016f, 0.042f, -0.012f), 0.0035f, 0.0035f, 8);
            g.Tube(GM.Dark, new P3(0.012f, 0.038f, 0.11f), new P3(0.016f, 0.038f, 0.11f), 0.0045f, 0.0045f, 10);   // mag release
            g.Tube(GM.Metal, new P3(-0.0135f, 0.052f, 0.122f), new P3(0.0135f, 0.052f, 0.122f), 0.0025f, 0.0025f, 8);   // pins
            g.Tube(GM.Metal, new P3(-0.0135f, 0.052f, -0.055f), new P3(0.0135f, 0.052f, -0.055f), 0.0025f, 0.0025f, 8);
            // upper receiver: flat top with a rail, ejection port, forward assist, charging handle
            float zf = -0.075f + upperLen;
            g.Profile(upper, 0, 0.03f, 0.003f,
                -0.075f, 0.062f, zf, 0.062f, zf, 0.098f, -0.07f, 0.098f, -0.075f, 0.09f);
            g.Box(upper, 0.0f, 0.073f, -0.03f, 0.036f, 0.022f, 0.06f, 0.004f);
            g.Tube(upper, new P3(0.014f, 0.082f, -0.045f), new P3(0.02f, 0.076f, -0.02f), 0.007f, 0.007f, 12);   // forward assist
            g.Tube(GM.Dark, new P3(0.02f, 0.076f, -0.02f), new P3(0.022f, 0.075f, -0.014f), 0.0075f, 0.0075f, 12);
            g.Box(GM.Port, 0.0152f, 0.078f, 0.045f, 0.002f, 0.016f, 0.05f, 0.0004f);           // ejection port
            g.Box(GM.Dark, 0.016f, 0.072f, 0.045f, 0.002f, 0.003f, 0.056f, 0.0004f);           // dust cover hinge
            g.Box(GM.Dark, 0.0158f, 0.088f, 0.012f, 0.0035f, 0.006f, 0.012f, 0.0006f);          // brass deflector
            Rail(g, 0.098f, -0.074f, zf - 0.002f);
            g.Profile(GM.Dark, 0, 0.012f, 0.001f, -0.09f, 0.092f, -0.07f, 0.092f, -0.07f, 0.1f, -0.09f, 0.1f);   // charging handle
            g.Box(GM.Dark, 0, 0.096f, -0.093f, 0.032f, 0.008f, 0.008f, 0.0015f);
        }

        /// <summary>A free-floating handguard (octagonal, with M-LOK slots) and the rail along its top.</summary>
        static void Handguard(GunMesh g, float y, float z0, float z1, float r, GM m)
        {
            g.TubeZ(m, 0, y, z0, z1, r, r, 8);
            foreach (float s in new[] { -1f, 1f })
                for (float z = z0 + 0.022f; z < z1 - 0.02f; z += 0.04f)
                    g.Box(GM.Port, s * r * 0.93f, y, z, 0.002f, 0.008f, 0.026f, 0.0004f);
            for (float z = z0 + 0.022f; z < z1 - 0.02f; z += 0.04f)
                g.Box(GM.Port, 0, y - r * 0.93f, z, 0.008f, 0.002f, 0.026f, 0.0004f);
            Rail(g, y + r * 0.92f, z0 + 0.004f, z1 - 0.004f);
        }

        static void Ar15(GunMesh g)
        {
            ArReceivers(g, GM.Dark, GM.Dark);
            M4Stock(g, 0.078f, -0.075f, GM.Polymer);
            float by = 0.078f;
            Handguard(g, by, 0.155f, 0.37f, 0.022f, GM.Dark);
            g.TubeZ(GM.Metal, 0, by, 0.37f, 0.49f, 0.0085f, -1, 14);
            g.Profile(GM.Dark, 0, 0.016f, 0.002f, 0.4f, by - 0.012f, 0.42f, by - 0.012f, 0.415f, by + 0.04f, 0.405f, by + 0.04f);   // A2 front sight
            g.Box(GM.Dark, 0, by + 0.046f, 0.41f, 0.003f, 0.014f, 0.003f, 0.0005f);
            g.TubeZ(GM.Dark, 0, by, 0.395f, 0.425f, 0.013f, -1, 14);
            FlashHider(g, by, 0.49f, 0.0105f);
            RedDot(g, 0.1055f, 0.03f);
            g.Profile(GM.Polymer, 0, 0.026f, 0.005f, 0.235f, by - 0.02f, 0.265f, by - 0.02f, 0.262f, by - 0.088f, 0.236f, by - 0.088f);   // vertical grip
            g.Anchor("muzzle", by, 0.545f);
            g.Anchor("magwell", 0.0f, 0.09f);
            g.Anchor("eject", 0.078f, 0.045f, 0, 0.018f);
            g.Anchor("foregrip", by - 0.05f, 0.25f);
            g.Anchor("ammo", 0.075f, -0.06f, 0, 0.022f);
        }

        /* ======================= magazines ======================= */

        public static GunMesh BuildMag(string id)
        {
            var g = new GunMesh();
            PickMag(g, id);
            return g;
        }

        /// <summary>A 30-round STANAG: a gentle forward curve, ribbed sides, a base plate. Hangs down from y = 0.</summary>
        static void Stanag(GunMesh g, GM m = GM.Dark)
        {
            g.Profile(m, 0, 0.023f, 0.002f,
                -0.031f, 0.012f, 0.031f, 0.012f, 0.032f, -0.06f, 0.04f, -0.12f, 0.052f, -0.175f,
                -0.012f, -0.188f, -0.022f, -0.13f, -0.028f, -0.065f);
            g.Profile(GM.Polymer, 0, 0.026f, 0.003f, -0.016f, -0.184f, 0.054f, -0.172f, 0.056f, -0.184f, -0.015f, -0.197f);
            for (int i = 0; i < 4; i++)
                g.Box(GM.Port, 0.0118f, -0.04f - i * 0.03f, 0.002f + i * 0.004f, 0.001f, 0.006f, 0.03f, 0.0002f);
            g.TubeZ(GM.Brass, 0, 0.014f, -0.02f, 0.02f, 0.0045f, 0.003f, 10);
        }
    }
}
