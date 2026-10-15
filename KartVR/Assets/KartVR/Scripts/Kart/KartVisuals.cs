using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Builds the kart's 3D model from its <see cref="KartLook"/> (paint, pattern,
    /// booster, wheels, spoiler). Everything is made from simple shapes at
    /// runtime so a look change rebuilds instantly and the same code shows your
    /// kart in your garage, on track, and on other players' screens.
    /// Visual only: no colliders, physics is the kart root's box collider.
    /// Kart space: +Z forward, the collider's bottom (the ground) is at y = 0.15.
    /// </summary>
    public class KartVisuals : MonoBehaviour
    {
        public Transform SteeringWheel { get; private set; }
        public readonly List<Transform> FrontWheels = new List<Transform>();
        public readonly List<Transform> AllWheels = new List<Transform>();

        Transform root;
        readonly List<ParticleSystem> boostFlames = new List<ParticleSystem>();
        readonly List<Renderer> nozzleGlows = new List<Renderer>();
        int builtLook = int.MinValue;
        bool boosting;

        const float Ground = 0.15f;

        static readonly Dictionary<int, Texture2D> PatternTextures = new Dictionary<int, Texture2D>();

        public void Apply(KartLook look)
        {
            int packed = look.Pack();
            if (packed == builtLook) return;
            builtLook = packed;
            Build(look);
        }

        public void SetBoosting(bool on)
        {
            if (on == boosting) return;
            boosting = on;
            foreach (var f in boostFlames)
            {
                if (f == null) continue;
                if (on) f.Play(); else f.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            foreach (var r in nozzleGlows)
                if (r) r.sharedMaterial = on ? KartUI.Glow(new Color(1f, 0.55f, 0.1f)) : KartUI.Mat(new Color(0.25f, 0.1f, 0.05f), 0.5f);
        }

        void Build(KartLook look)
        {
            if (root != null) Destroy(root.gameObject);
            FrontWheels.Clear();
            AllWheels.Clear();
            boostFlames.Clear();
            nozzleGlows.Clear();
            boosting = false;

            root = new GameObject("KartModel").transform;
            root.SetParent(transform, false);

            Color paint = look.PaintColor, accent = look.AccentColor;
            Material body = BodyMaterial(look);
            Material accentMat = KartUI.Paint(accent);
            Material sideMat = look.pattern == KartPattern.TwoTone ? accentMat : body;
            Material carbon = KartUI.Carbon(), chrome = KartUI.Chrome(), rubber = KartUI.Rubber();
            Material seat = KartUI.Mat(new Color(0.1f, 0.1f, 0.12f), 0.2f);

            // --- Chassis ---
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.27f, -0.05f), new Vector3(1.0f, 0.06f, 1.85f), carbon, Vector3.zero, "Floor");
            // Side pods: rounded fuel-tank style capsules along each side.
            foreach (float x in new[] { -0.46f, 0.46f })
            {
                KartUI.Part(root, PrimitiveType.Capsule, new Vector3(x, 0.4f, 0.0f), new Vector3(0.3f, 0.62f, 0.32f), sideMat, new Vector3(90f, 0f, 0f), "SidePod");
                KartUI.Part(root, PrimitiveType.Cube, new Vector3(x * 1.08f, 0.47f, 0.0f), new Vector3(0.05f, 0.05f, 0.9f), accentMat, Vector3.zero, "PodTrim");
            }
            // Nose cone and body.
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.38f, 0.62f), new Vector3(0.62f, 0.2f, 0.7f), body, new Vector3(-6f, 0f, 0f), "Nose");
            KartUI.Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.37f, 1.0f), new Vector3(0.62f, 0.24f, 0.42f), body, Vector3.zero, "NoseTip");
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.27f, 1.14f), new Vector3(1.28f, 0.05f, 0.24f), accentMat, Vector3.zero, "FrontWing");
            foreach (float x in new[] { -0.64f, 0.64f })
                KartUI.Part(root, PrimitiveType.Cube, new Vector3(x, 0.31f, 1.14f), new Vector3(0.03f, 0.13f, 0.26f), accentMat, Vector3.zero, "WingEnd");
            // Cockpit surround and dash.
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.32f), new Vector3(0.64f, 0.16f, 0.3f), body, new Vector3(-20f, 0f, 0f), "Cowl");
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.42f), new Vector3(0.36f, 0.1f, 0.05f), carbon, new Vector3(-35f, 0f, 0f), "Dash");
            // Number plate on the nose.
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.47f, 0.85f), new Vector3(0.3f, 0.01f, 0.22f), KartUI.Mat(Color.white, 0.4f), new Vector3(-6f, 0f, 0f), "Plate");

            // Stripes / decoration patterns that are geometry rather than texture.
            if (look.pattern == KartPattern.RacingStripes)
            {
                foreach (float x in new[] { -0.09f, 0.09f })
                {
                    KartUI.Part(root, PrimitiveType.Cube, new Vector3(x, 0.485f, 0.62f), new Vector3(0.07f, 0.01f, 0.72f), accentMat, new Vector3(-6f, 0f, 0f), "Stripe");
                    KartUI.Part(root, PrimitiveType.Cube, new Vector3(x, 0.59f, -0.78f), new Vector3(0.07f, 0.01f, 0.4f), accentMat, Vector3.zero, "Stripe");
                }
            }

            // --- Seat ---
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.36f, -0.3f), new Vector3(0.46f, 0.1f, 0.46f), seat, Vector3.zero, "SeatBase");
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.66f, -0.56f), new Vector3(0.5f, 0.6f, 0.09f), seat, new Vector3(-12f, 0f, 0f), "SeatBack");
            foreach (float x in new[] { -0.25f, 0.25f })
                KartUI.Part(root, PrimitiveType.Cube, new Vector3(x, 0.52f, -0.42f), new Vector3(0.06f, 0.3f, 0.36f), seat, Vector3.zero, "SeatSide");

            // --- Engine, exhausts, rear bumper ---
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.48f, -0.86f), new Vector3(0.5f, 0.24f, 0.3f), body, Vector3.zero, "EngineCover");
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0.22f, 0.43f, -0.86f), new Vector3(0.12f, 0.2f, 0.24f), chrome, Vector3.zero, "Engine");
            foreach (float x in new[] { -0.14f, 0.0f })
                KartUI.Part(root, PrimitiveType.Cylinder, new Vector3(x, 0.42f, -1.04f), new Vector3(0.07f, 0.09f, 0.07f), chrome, new Vector3(90f, 0f, 0f), "Exhaust");
            KartUI.Part(root, PrimitiveType.Capsule, new Vector3(0f, 0.3f, -1.08f), new Vector3(0.1f, 0.62f, 0.1f), carbon, new Vector3(0f, 0f, 90f), "RearBumper");
            KartUI.Part(root, PrimitiveType.Capsule, new Vector3(0f, 0.3f, 1.27f), new Vector3(0.08f, 0.58f, 0.08f), carbon, new Vector3(0f, 0f, 90f), "FrontBumper");

            // --- Steering wheel (pivot so we can turn it) ---
            KartUI.Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0.34f), new Vector3(0.04f, 0.2f, 0.04f), carbon, new Vector3(-55f, 0f, 0f), "Column");
            var wheelPivot = new GameObject("SteeringWheel").transform;
            wheelPivot.SetParent(root, false);
            wheelPivot.localPosition = new Vector3(0f, 0.72f, 0.2f);
            var wheelTilt = new GameObject("Tilt").transform;
            wheelTilt.SetParent(wheelPivot, false);
            wheelTilt.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            KartUI.Part(wheelTilt, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.3f, 0.012f, 0.3f), rubber, Vector3.zero, "Rim");
            KartUI.Part(wheelTilt, PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f), new Vector3(0.22f, 0.012f, 0.22f), carbon, Vector3.zero, "Inner");
            KartUI.Part(wheelTilt, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0f), new Vector3(0.27f, 0.01f, 0.04f), accentMat, Vector3.zero, "Spoke");
            KartUI.Part(wheelTilt, PrimitiveType.Cylinder, new Vector3(0f, 0.016f, 0f), new Vector3(0.07f, 0.01f, 0.07f), accentMat, Vector3.zero, "Hub");
            SteeringWheel = wheelPivot;

            // --- Wheels ---
            float radius = look.wheels == KartWheels.Chunky ? 0.27f : look.wheels == KartWheels.Racing ? 0.21f : 0.23f;
            float width = look.wheels == KartWheels.Chunky ? 0.3f : look.wheels == KartWheels.Racing ? 0.26f : 0.2f;
            Material rim = look.wheels == KartWheels.Racing ? KartUI.Mat(accent, 0.8f, 0.6f) : chrome;
            Wheel(new Vector3(-0.66f, Ground + radius, 0.68f), radius, width, rim, rubber, true, look.wheels);
            Wheel(new Vector3(0.66f, Ground + radius, 0.68f), radius, width, rim, rubber, true, look.wheels);
            Wheel(new Vector3(-0.68f, Ground + radius, -0.68f), radius * 1.08f, width * 1.2f, rim, rubber, false, look.wheels);
            Wheel(new Vector3(0.68f, Ground + radius, -0.68f), radius * 1.08f, width * 1.2f, rim, rubber, false, look.wheels);

            // --- Spoiler ---
            if (look.spoiler == KartSpoiler.Lip)
            {
                KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.63f, -1.0f), new Vector3(0.6f, 0.04f, 0.14f), accentMat, new Vector3(15f, 0f, 0f), "Lip");
            }
            else if (look.spoiler != KartSpoiler.None)
            {
                bool big = look.spoiler == KartSpoiler.BigWing;
                float h = big ? 1.05f : 0.82f, w = big ? 1.2f : 0.9f;
                foreach (float x in new[] { -0.2f, 0.2f })
                    KartUI.Part(root, PrimitiveType.Cube, new Vector3(x, (0.55f + h) * 0.5f, -1.0f), new Vector3(0.04f, h - 0.55f, 0.08f), carbon, Vector3.zero, "Strut");
                KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, h, -1.02f), new Vector3(w, 0.04f, 0.3f), accentMat, new Vector3(-8f, 0f, 0f), "Wing");
                foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                    KartUI.Part(root, PrimitiveType.Cube, new Vector3(x, h, -1.02f), new Vector3(0.03f, 0.2f, 0.34f), body, Vector3.zero, "Endplate");
            }

            // --- Boosters ---
            switch (look.booster)
            {
                case KartBooster.TwinRockets:
                    Rocket(new Vector3(-0.42f, 0.62f, -0.82f), 0.09f, 0.55f, accentMat, chrome);
                    Rocket(new Vector3(0.42f, 0.62f, -0.82f), 0.09f, 0.55f, accentMat, chrome);
                    break;
                case KartBooster.Jet:
                    Rocket(new Vector3(0f, 0.85f, -0.8f), 0.17f, 0.7f, body, chrome);
                    KartUI.Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, -0.43f), new Vector3(0.3f, 0.02f, 0.3f), carbon, new Vector3(90f, 0f, 0f), "Intake");
                    KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.66f, -0.8f), new Vector3(0.08f, 0.2f, 0.3f), carbon, Vector3.zero, "Mount");
                    break;
                case KartBooster.MegaRockets:
                    foreach (var p in new[] { new Vector3(-0.42f, 0.6f, -0.85f), new Vector3(0.42f, 0.6f, -0.85f), new Vector3(-0.25f, 0.88f, -0.9f), new Vector3(0.25f, 0.88f, -0.9f) })
                        Rocket(p, 0.08f, 0.6f, accentMat, chrome);
                    break;
            }
        }

        void Wheel(Vector3 pos, float radius, float width, Material rim, Material rubber, bool front, KartWheels style)
        {
            // The pivot is what steers (front) and spins; the cylinder inside is turned on its side.
            var pivot = new GameObject(front ? "FrontWheel" : "RearWheel").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = pos;
            var spin = new GameObject("Spin").transform;
            spin.SetParent(pivot, false);
            KartUI.Part(spin, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, width * 0.5f, radius * 2f), rubber, new Vector3(0f, 0f, 90f), "Tire");
            float side = pos.x > 0 ? 1f : -1f;
            KartUI.Part(spin, PrimitiveType.Cylinder, new Vector3(side * 0.012f, 0f, 0f), new Vector3(radius * 1.25f, width * 0.5f, radius * 1.25f), rim, new Vector3(0f, 0f, 90f), "Rim");
            // A spoke bar so you can see the wheel turning.
            KartUI.Part(spin, PrimitiveType.Cube, new Vector3(side * (width * 0.5f + 0.005f), 0f, 0f), new Vector3(0.01f, radius * 1.15f, 0.05f), rim, Vector3.zero, "Spoke");
            if (style == KartWheels.Chunky)
            {
                // Tread blocks.
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 36f;
                    KartUI.Part(spin, PrimitiveType.Cube, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, radius, 0f),
                        new Vector3(width * 1.02f, 0.03f, 0.06f), rubber, new Vector3(a, 0f, 0f), "Tread");
                }
            }
            if (front) FrontWheels.Add(pivot);
            AllWheels.Add(pivot);
        }

        void Rocket(Vector3 pos, float radius, float length, Material bodyMat, Material nozzleMat)
        {
            var r = new GameObject("Rocket").transform;
            r.SetParent(root, false);
            r.localPosition = pos;
            KartUI.Part(r, PrimitiveType.Capsule, Vector3.zero, new Vector3(radius * 2f, length * 0.5f, radius * 2f), bodyMat, new Vector3(90f, 0f, 0f), "Body");
            KartUI.Part(r, PrimitiveType.Cylinder, new Vector3(0f, 0f, -length * 0.5f), new Vector3(radius * 1.7f, 0.06f, radius * 1.7f), nozzleMat, new Vector3(90f, 0f, 0f), "Nozzle");
            var glow = KartUI.Part(r, PrimitiveType.Cylinder, new Vector3(0f, 0f, -length * 0.5f - 0.05f), new Vector3(radius * 1.3f, 0.01f, radius * 1.3f),
                KartUI.Mat(new Color(0.25f, 0.1f, 0.05f), 0.5f), new Vector3(90f, 0f, 0f), "Glow");
            nozzleGlows.Add(glow.GetComponent<Renderer>());
            // Little fins.
            foreach (float a in new[] { 0f, 120f, 240f })
                KartUI.Part(r, PrimitiveType.Cube, Quaternion.Euler(0f, 0f, a) * new Vector3(0f, radius * 1.1f, -length * 0.3f),
                    new Vector3(0.015f, radius * 0.9f, length * 0.3f), nozzleMat, new Vector3(0f, 0f, a), "Fin");
            boostFlames.Add(KartUI.Flame(r, new Vector3(0f, 0f, -length * 0.5f - 0.06f), radius * 2.2f,
                new Color(1f, 0.9f, 0.4f), new Color(1f, 0.3f, 0.05f)));
        }

        /// <summary>Paint material for the main body, including texture patterns.</summary>
        static Material BodyMaterial(KartLook look)
        {
            if (look.pattern != KartPattern.Checker && look.pattern != KartPattern.Flames && look.pattern != KartPattern.Camo)
                return KartUI.Paint(look.PaintColor);
            int key = (int)look.pattern * 256 + look.paint * 16 + look.accent;
            if (!PatternTextures.TryGetValue(key, out var tex) || tex == null)
            {
                tex = MakePattern(look.pattern, look.PaintColor, look.AccentColor);
                PatternTextures[key] = tex;
            }
            return KartUI.Mat(Color.white, 0.75f, 0.1f, default(Color), tex);
        }

        static Texture2D MakePattern(KartPattern pattern, Color a, Color b)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                bool useB;
                switch (pattern)
                {
                    case KartPattern.Checker:
                        useB = ((x / 8) + (y / 8)) % 2 == 0;
                        break;
                    case KartPattern.Flames:
                        // Tongues of flame licking up from the bottom of each face.
                        float flame = 0.45f + 0.18f * Mathf.Sin(u * 6.283f * 3f) + 0.1f * Mathf.Sin(u * 6.283f * 7f + 1f);
                        useB = v < flame;
                        break;
                    default: // Camo
                        float n = Mathf.PerlinNoise(u * 4f + 3.1f, v * 4f + 7.7f) + 0.5f * Mathf.PerlinNoise(u * 9f, v * 9f);
                        useB = n > 0.9f;
                        break;
                }
                px[y * size + x] = useB ? b : a;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
