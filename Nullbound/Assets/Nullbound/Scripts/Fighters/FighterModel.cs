using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Builds a fighter's body out of simple shapes from its <see cref="FighterStats"/> (body build, head, hair /
    /// horns / crown, cape / wings / tail / jetpack, glove style, colours), so every fighter has its own look while
    /// staying cheap on Quest. Used for fighters in the arena, the menu preview and crate reveals.
    /// </summary>
    public class FighterModel
    {
        public Transform root, head, torso, hips, leftArm, rightArm, leftLeg, rightLeg, leftGlove, rightGlove, shield;
        public readonly List<Renderer> gloveRenderers = new List<Renderer>();
        public Material main, accent, glow, gloveGlow;
        public FighterStats stats;

        enum Motion { None, Sway, Flap, Swing, Orbit, Flicker, Thrust }
        struct Extra { public Transform t; public Motion motion; public Vector3 basePos, baseScale; public Quaternion baseRot; public float phase; }
        readonly List<Extra> extras = new List<Extra>();
        Transform back; // cape / wings / tail / jetpack ride here (behind the chest)
        float walkPhase;

        /// <summary>Build the model under parent. Silhouette = all dark (a locked fighter in the menu).</summary>
        public static FighterModel Build(Transform parent, FighterStats s, bool silhouette = false)
        {
            var m = new FighterModel { stats = s };
            var shadow = NBUI.Mat(new Color(0.05f, 0.05f, 0.07f), 0.2f);
            m.main = silhouette ? shadow : NBUI.Mat(s.main, 0.55f, 0.1f);
            m.accent = silhouette ? shadow : NBUI.Glow(s.accent);
            m.glow = silhouette ? shadow : NBUI.Glow(s.eyes);
            m.gloveGlow = silhouette ? shadow : NBUI.Glow(Color.Lerp(s.main, Color.white, 0.4f));
            var dark = silhouette ? shadow : NBUI.Mat(new Color(0.11f, 0.11f, 0.14f), 0.4f);
            var skin = silhouette ? shadow : NBUI.Mat(new Color(0.95f, 0.8f, 0.68f), 0.3f);
            var bone = silhouette ? shadow : NBUI.Mat(new Color(0.92f, 0.9f, 0.82f), 0.3f);
            var metal = silhouette ? shadow : NBUI.Mat(Color.Lerp(s.main, new Color(0.7f, 0.72f, 0.76f), 0.5f), 0.8f, 0.8f);
            float w = s.scale;

            m.root = new GameObject("Model").transform;
            m.root.SetParent(parent, false);
            var r = m.root;

            // Body.
            bool boxy = s.body == BodyType.Robot;
            float torsoW = s.body == BodyType.Giant ? 0.6f : s.body == BodyType.Bulky ? 0.52f : s.body == BodyType.Slim || s.body == BodyType.Spirit ? 0.38f : 0.44f;
            m.hips = NBUI.Part(r, boxy ? PrimitiveType.Cube : PrimitiveType.Capsule, Vector3.zero, new Vector3(0.36f * w, 0.2f, 0.26f * w), dark, default, "Hips");
            m.torso = NBUI.Part(r, boxy ? PrimitiveType.Cube : PrimitiveType.Capsule, Vector3.zero, new Vector3(torsoW * w, 0.32f, 0.3f * w), m.main, default, "Torso");
            NBUI.Part(m.torso, PrimitiveType.Cube, new Vector3(0f, 0.1f, boxy ? 0.5f : 0.42f), new Vector3(0.5f, 0.12f, 0.12f), m.accent, default, "ChestStripe");
            if (s.body == BodyType.Robot || s.glove == GloveType.Metal || s.body == BodyType.Giant)
                foreach (float x in new[] { -0.55f, 0.55f })
                    NBUI.Part(m.torso, PrimitiveType.Sphere, new Vector3(x, 0.4f, 0f), new Vector3(0.45f, 0.35f, 0.9f), boxy ? metal : m.main, default, "Shoulder");

            // Head.
            m.head = new GameObject("Head").transform;
            m.head.SetParent(r, false);
            BuildHead(m, s, skin, bone, dark, metal);
            BuildTop(m, s, dark);

            // Limbs.
            m.leftArm = NBUI.Part(r, PrimitiveType.Capsule, Vector3.zero, Vector3.one, m.main, default, "LeftArm");
            m.rightArm = NBUI.Part(r, PrimitiveType.Capsule, Vector3.zero, Vector3.one, m.main, default, "RightArm");
            bool legs = s.body != BodyType.Spirit;
            m.leftLeg = NBUI.Part(r, PrimitiveType.Capsule, Vector3.zero, Vector3.one, dark, default, "LeftLeg");
            m.rightLeg = NBUI.Part(r, PrimitiveType.Capsule, Vector3.zero, Vector3.one, dark, default, "RightLeg");
            if (!legs)
            {
                // A wisp instead of legs.
                m.leftLeg.GetComponent<Renderer>().sharedMaterial = m.main;
                m.rightLeg.gameObject.SetActive(false);
            }
            m.leftGlove = Glove(m, r, s, dark, metal);
            m.rightGlove = Glove(m, r, s, dark, metal);

            // Back pieces.
            m.back = new GameObject("Back").transform;
            m.back.SetParent(r, false);
            BuildBack(m, s, dark, metal);

            m.shield = NBUI.Part(r, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.55f, 0.01f, 0.55f), NBUI.Glow(new Color(0.4f, 0.75f, 1f)), default, "Shield");
            m.shield.gameObject.SetActive(false);
            return m;
        }

        static void Eyes(FighterModel m, float z, float y, float size)
        {
            foreach (float x in new[] { -0.045f, 0.045f })
                NBUI.Part(m.head, PrimitiveType.Sphere, new Vector3(x, y, z), Vector3.one * size, m.glow, default, "Eye");
        }

        static void BuildHead(FighterModel m, FighterStats s, Material skin, Material bone, Material dark, Material metal)
        {
            var h = m.head;
            switch (s.head)
            {
                case HeadType.Box:
                    NBUI.Part(h, PrimitiveType.Cube, Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), s.body == BodyType.Robot ? metal : m.main, default, "Face");
                    NBUI.Part(h, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.122f), new Vector3(0.18f, 0.04f, 0.01f), m.glow, default, "EyeBar");
                    break;
                case HeadType.Helmet:
                    NBUI.Part(h, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.26f, 0.28f, 0.27f), m.main, default, "Helmet");
                    NBUI.Part(h, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.11f), new Vector3(0.2f, 0.05f, 0.06f), m.glow, default, "Visor");
                    NBUI.Part(h, PrimitiveType.Cube, new Vector3(0f, 0.13f, 0f), new Vector3(0.04f, 0.04f, 0.24f), m.accent, default, "Crest");
                    break;
                case HeadType.Hood:
                    NBUI.Part(h, PrimitiveType.Sphere, new Vector3(0f, 0.01f, -0.015f), new Vector3(0.29f, 0.31f, 0.3f), m.main, default, "Hood");
                    NBUI.Part(h, PrimitiveType.Sphere, new Vector3(0f, -0.01f, 0.04f), new Vector3(0.2f, 0.22f, 0.22f), dark, default, "Shade");
                    Eyes(m, 0.14f, 0f, 0.035f);
                    break;
                case HeadType.Skull:
                    NBUI.Part(h, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.24f, 0.26f, 0.25f), s.main.grayscale < 0.2f ? bone : m.main, default, "Skull");
                    foreach (float x in new[] { -0.05f, 0.05f })
                        NBUI.Part(h, PrimitiveType.Sphere, new Vector3(x, 0.01f, 0.1f), new Vector3(0.065f, 0.06f, 0.05f), dark, default, "Socket");
                    Eyes(m, 0.125f, 0.01f, 0.025f);
                    NBUI.Part(h, PrimitiveType.Cube, new Vector3(0f, -0.08f, 0.1f), new Vector3(0.1f, 0.025f, 0.02f), dark, default, "Teeth");
                    break;
                case HeadType.Mask:
                    NBUI.Part(h, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), skin, default, "Face");
                    NBUI.Part(h, PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0.03f), new Vector3(0.25f, 0.15f, 0.24f), m.main, default, "Mask");
                    NBUI.Part(h, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.04f, 0.26f), m.accent, default, "Headband");
                    Eyes(m, 0.115f, 0.015f, 0.03f);
                    break;
                default:
                    NBUI.Part(h, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), skin, default, "Face");
                    NBUI.Part(h, PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.075f), new Vector3(0.21f, 0.07f, 0.12f), m.glow, default, "Visor");
                    break;
            }
        }

        static void BuildTop(FighterModel m, FighterStats s, Material dark)
        {
            var h = m.head;
            var hair = s.head == HeadType.Round || s.head == HeadType.Mask ? m.main : m.accent;
            switch (s.top)
            {
                case TopType.Spikes:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = (i - 3) * 22f;
                        NBUI.Part(h, PrimitiveType.Cube, Quaternion.Euler(-30f + Mathf.Abs(i - 3) * 6f, a, 0f) * new Vector3(0f, 0.11f, -0.04f),
                            new Vector3(0.06f, 0.2f, 0.06f), hair, new Vector3(-35f, a, (i - 3) * -8f), "Spike");
                    }
                    break;
                case TopType.Mohawk:
                    for (int i = 0; i < 6; i++)
                        NBUI.Part(h, PrimitiveType.Cube, new Vector3(0f, 0.14f + Mathf.Sin(i * 0.6f) * 0.02f, 0.09f - i * 0.045f), new Vector3(0.03f, 0.12f, 0.05f), m.accent, new Vector3(-15f, 0f, 0f), "Mohawk");
                    break;
                case TopType.Horns:
                    foreach (float x in new[] { -1f, 1f })
                    {
                        NBUI.Part(h, PrimitiveType.Capsule, new Vector3(x * 0.11f, 0.12f, 0f), new Vector3(0.05f, 0.09f, 0.05f), dark, new Vector3(0f, 0f, -x * 35f), "Horn");
                        NBUI.Part(h, PrimitiveType.Capsule, new Vector3(x * 0.17f, 0.22f, 0f), new Vector3(0.035f, 0.06f, 0.035f), m.accent, new Vector3(0f, 0f, -x * 10f), "HornTip");
                    }
                    break;
                case TopType.Crown:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        NBUI.Part(h, PrimitiveType.Cube, new Vector3(Mathf.Sin(a) * 0.1f, 0.15f, Mathf.Cos(a) * 0.1f), new Vector3(0.035f, 0.07f, 0.035f), m.accent, new Vector3(0f, a * Mathf.Rad2Deg, 0f), "Crown");
                    }
                    break;
                case TopType.Halo:
                    m.AddExtra(NBUI.Part(h, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.24f, 0.006f, 0.24f), m.glow, default, "Halo"), Motion.Flicker);
                    break;
                case TopType.Antennae:
                    foreach (float x in new[] { -0.06f, 0.06f })
                    {
                        NBUI.Part(h, PrimitiveType.Cylinder, new Vector3(x, 0.19f, 0f), new Vector3(0.012f, 0.08f, 0.012f), dark, new Vector3(0f, 0f, -x * 200f), "Antenna");
                        NBUI.Part(h, PrimitiveType.Sphere, new Vector3(x * 1.8f, 0.27f, 0f), Vector3.one * 0.04f, m.glow, default, "AntennaTip");
                    }
                    break;
                case TopType.Ears:
                    foreach (float x in new[] { -1f, 1f })
                        NBUI.Part(h, PrimitiveType.Cube, new Vector3(x * 0.08f, 0.15f, -0.01f), new Vector3(0.06f, 0.11f, 0.03f), hair, new Vector3(0f, 0f, -x * 20f), "Ear");
                    break;
                case TopType.Ponytail:
                    NBUI.Part(h, PrimitiveType.Sphere, new Vector3(0f, 0.04f, -0.06f), new Vector3(0.25f, 0.24f, 0.2f), hair, default, "Hair");
                    m.AddExtra(NBUI.Part(h, PrimitiveType.Capsule, new Vector3(0f, -0.06f, -0.16f), new Vector3(0.06f, 0.14f, 0.06f), hair, new Vector3(25f, 0f, 0f), "Ponytail"), Motion.Sway);
                    break;
                case TopType.Flames:
                    for (int i = 0; i < 5; i++)
                        m.AddExtra(NBUI.Part(h, PrimitiveType.Cube, new Vector3((i - 2) * 0.045f, 0.15f, -0.02f), new Vector3(0.05f, 0.14f, 0.05f), m.accent, new Vector3(0f, 45f, (i - 2) * -10f), "Flame"), Motion.Flicker, i);
                    break;
            }
        }

        static void BuildBack(FighterModel m, FighterStats s, Material dark, Material metal)
        {
            var b = m.back;
            switch (s.back)
            {
                case BackType.Cape:
                    m.AddExtra(NBUI.Part(b, PrimitiveType.Cube, new Vector3(0f, -0.35f, -0.04f), new Vector3(0.42f * s.scale, 0.75f, 0.02f), m.main, new Vector3(8f, 0f, 0f), "Cape"), Motion.Sway);
                    NBUI.Part(b, PrimitiveType.Cube, new Vector3(0f, 0.02f, -0.03f), new Vector3(0.44f * s.scale, 0.05f, 0.04f), m.accent, default, "CapeClasp");
                    break;
                case BackType.Wings:
                    foreach (float x in new[] { -1f, 1f })
                    {
                        var pivot = new GameObject("WingPivot").transform;
                        pivot.SetParent(b, false);
                        pivot.localPosition = new Vector3(x * 0.08f, -0.05f, -0.05f);
                        NBUI.Part(pivot, PrimitiveType.Cube, new Vector3(x * 0.3f, 0.12f, 0f), new Vector3(0.6f, 0.3f, 0.02f), m.accent, new Vector3(0f, 0f, x * 25f), "Wing");
                        m.AddExtra(pivot, x < 0 ? Motion.Flap : Motion.Flap, x < 0 ? 0f : Mathf.PI);
                    }
                    break;
                case BackType.Tail:
                    {
                        var pivot = new GameObject("TailPivot").transform;
                        pivot.SetParent(b, false);
                        pivot.localPosition = new Vector3(0f, -0.45f, -0.08f);
                        for (int i = 0; i < 3; i++)
                            NBUI.Part(pivot, PrimitiveType.Capsule, new Vector3(0f, -0.05f - i * 0.05f, -0.1f - i * 0.17f), new Vector3(0.08f - i * 0.015f, 0.12f, 0.08f - i * 0.015f), i == 2 ? m.accent : m.main, new Vector3(70f - i * 15f, 0f, 0f), "Tail");
                        m.AddExtra(pivot, Motion.Swing);
                    }
                    break;
                case BackType.Jetpack:
                    foreach (float x in new[] { -0.08f, 0.08f })
                    {
                        NBUI.Part(b, PrimitiveType.Cylinder, new Vector3(x, -0.1f, -0.09f), new Vector3(0.1f, 0.15f, 0.1f), metal, default, "Thruster");
                        m.AddExtra(NBUI.Part(b, PrimitiveType.Sphere, new Vector3(x, -0.3f, -0.09f), new Vector3(0.07f, 0.12f, 0.07f), m.glow, default, "JetFlame"), Motion.Thrust);
                    }
                    break;
                case BackType.Blades:
                    foreach (float a in new[] { -35f, 35f })
                        NBUI.Part(b, PrimitiveType.Cube, new Vector3(0f, 0f, -0.1f), new Vector3(0.05f, 0.85f, 0.01f), m.accent, new Vector3(0f, 0f, a), "Blade");
                    break;
                case BackType.Orbs:
                    for (int i = 0; i < 3; i++)
                        m.AddExtra(NBUI.Part(b, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.08f, m.glow, default, "Orb"), Motion.Orbit, i * Mathf.PI * 2f / 3f);
                    break;
                case BackType.Spines:
                    for (int i = 0; i < 4; i++)
                        NBUI.Part(b, PrimitiveType.Cube, new Vector3(0f, 0.05f - i * 0.13f, -0.15f), new Vector3(0.05f, 0.12f, 0.12f), m.accent, new Vector3(45f, 0f, 45f), "Spine");
                    break;
            }
        }

        static Transform Glove(FighterModel m, Transform parent, FighterStats s, Material dark, Material metal)
        {
            var g = new GameObject("Glove").transform;
            g.SetParent(parent, false);
            var fistMat = s.glove == GloveType.Glow ? m.gloveGlow : s.glove == GloveType.Metal ? metal : m.main;
            float big = s.glove == GloveType.Big ? 1.25f : 1f;
            var fist = s.glove == GloveType.Metal
                ? NBUI.Part(g, PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), new Vector3(0.12f, 0.11f, 0.14f), fistMat, default, "Fist")
                : NBUI.Part(g, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.02f), new Vector3(0.13f, 0.12f, 0.15f) * big, fistMat, default, "Fist");
            m.gloveRenderers.Add(fist.GetComponent<Renderer>());
            NBUI.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.085f * big), new Vector3(0.12f * big, 0.035f, 0.03f), m.accent, default, "Knuckles");
            NBUI.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.06f), new Vector3(0.1f, 0.03f, 0.1f), dark, new Vector3(90f, 0f, 0f), "Cuff");
            if (s.glove == GloveType.Claws)
                for (int i = -1; i <= 1; i++)
                    NBUI.Part(g, PrimitiveType.Cube, new Vector3(i * 0.035f, 0.03f, 0.14f), new Vector3(0.012f, 0.012f, 0.12f), m.accent, default, "Claw");
            return g;
        }

        void AddExtra(Transform t, Motion motion, float phase = 0f)
        {
            extras.Add(new Extra { t = t, motion = motion, basePos = t.localPosition, baseScale = t.localScale, baseRot = t.localRotation, phase = phase });
        }

        /// <summary>Show or hide the whole model (invisible moderators, K.O.).</summary>
        public void SetVisible(bool visible) { if (root != null) root.gameObject.SetActive(visible); }

        /// <summary>Lay the body out under the head, arms reaching to the gloves, legs stepping as it moves.</summary>
        public void Pose(Vector3 feet, Vector3 headPos, Quaternion headRot, Vector3 leftPos, Quaternion leftRot, Vector3 rightPos, Quaternion rightRot,
            Vector3 fwd, float speed, bool down, bool blocking, bool airborne, float dt)
        {
            Quaternion yaw = Quaternion.LookRotation(fwd);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float w = stats.scale;

            if (down)
            {
                head.position = feet + fwd * 0.75f + Vector3.up * 0.15f;
                head.rotation = yaw * Quaternion.Euler(-80f, 0f, 0f);
                torso.position = feet + fwd * 0.35f + Vector3.up * 0.15f;
                torso.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);
                hips.position = feet + Vector3.up * 0.15f;
                foreach (var t in new[] { leftArm, rightArm, leftGlove, rightGlove, leftLeg, rightLeg, back }) t.gameObject.SetActive(false);
                shield.gameObject.SetActive(false);
                return;
            }
            foreach (var t in new[] { leftArm, rightArm, leftGlove, rightGlove, leftLeg, back }) t.gameObject.SetActive(true);
            rightLeg.gameObject.SetActive(stats.body != BodyType.Spirit);

            float height = Mathf.Clamp(headPos.y - feet.y, 0.9f, 2.1f);
            Vector3 neck = headPos + Vector3.down * 0.2f - fwd * 0.05f;
            Vector3 chest = neck + Vector3.down * 0.28f;
            Vector3 pelvis = new Vector3(headPos.x, feet.y + height * 0.52f, headPos.z) - fwd * (stats.body == BodyType.Beast ? 0.15f : 0.05f);

            head.position = headPos;
            head.rotation = headRot;
            torso.position = (neck + pelvis) * 0.5f + Vector3.up * 0.05f;
            torso.rotation = Quaternion.FromToRotation(Vector3.up, (neck - pelvis).normalized) * yaw;
            var ts = torso.localScale;
            ts.y = Mathf.Max(0.2f, (neck - pelvis).magnitude * 0.55f);
            torso.localScale = ts;
            hips.position = pelvis;
            hips.rotation = yaw;
            back.position = chest - fwd * 0.12f * w + Vector3.up * 0.05f;
            back.rotation = yaw;

            Vector3 ls = chest - right * 0.22f * w + Vector3.up * 0.06f, rs = chest + right * 0.22f * w + Vector3.up * 0.06f;
            leftGlove.position = leftPos;
            leftGlove.rotation = leftRot;
            rightGlove.position = rightPos;
            rightGlove.rotation = rightRot;
            float armThick = (stats.body == BodyType.Giant || stats.body == BodyType.Bulky ? 0.14f : 0.11f) * w;
            NBUI.Span(leftArm, ls, Vector3.MoveTowards(leftPos, ls, 0.06f), armThick);
            NBUI.Span(rightArm, rs, Vector3.MoveTowards(rightPos, rs, 0.06f), armThick);

            walkPhase += speed * dt * 3.2f;
            float swing = Mathf.Clamp01(speed / 4f) * 0.32f;
            Vector3 lh = pelvis - right * 0.11f * w, rh = pelvis + right * 0.11f * w;
            if (stats.body == BodyType.Spirit)
            {
                // Floats: a wisp trailing down instead of legs.
                Vector3 tip = new Vector3(pelvis.x, feet.y + 0.35f + Mathf.Sin(Time.time * 2f) * 0.05f, pelvis.z) - fwd * 0.2f;
                NBUI.Span(leftLeg, pelvis, tip, 0.2f * w);
            }
            else
            {
                Vector3 lf = new Vector3(lh.x, feet.y + (airborne ? 0.35f : 0.05f), lh.z) + fwd * Mathf.Sin(walkPhase) * swing;
                Vector3 rf = new Vector3(rh.x, feet.y + (airborne ? 0.25f : 0.05f), rh.z) - fwd * Mathf.Sin(walkPhase) * swing;
                float legThick = (stats.body == BodyType.Giant ? 0.19f : 0.15f) * w;
                NBUI.Span(leftLeg, lh, lf, legThick);
                NBUI.Span(rightLeg, rh, rf, legThick);
            }

            shield.gameObject.SetActive(blocking);
            if (blocking)
            {
                shield.position = (leftPos + rightPos) * 0.5f + fwd * 0.08f;
                shield.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);
            }
            AnimateExtras(speed, airborne);
        }

        void AnimateExtras(float speed, bool airborne)
        {
            float t = Time.time;
            foreach (var e in extras)
            {
                if (e.t == null) continue;
                switch (e.motion)
                {
                    case Motion.Sway:
                        e.t.localRotation = e.baseRot * Quaternion.Euler(Mathf.Sin(t * 2.2f + e.phase) * 6f + Mathf.Clamp(speed * 6f, 0f, 35f), 0f, 0f);
                        break;
                    case Motion.Flap:
                        float f = Mathf.Sin(t * (airborne ? 12f : 3f) + e.phase) * (airborne ? 30f : 8f);
                        e.t.localRotation = e.baseRot * Quaternion.Euler(0f, f * (e.phase > 0f ? -1f : 1f), 0f);
                        break;
                    case Motion.Swing:
                        e.t.localRotation = e.baseRot * Quaternion.Euler(0f, Mathf.Sin(t * 3f) * 25f, 0f);
                        break;
                    case Motion.Orbit:
                        float a = t * 2f + e.phase;
                        e.t.localPosition = new Vector3(Mathf.Cos(a) * 0.45f, 0.15f + Mathf.Sin(a * 2f) * 0.1f, Mathf.Sin(a) * 0.45f + 0.12f);
                        break;
                    case Motion.Flicker:
                        float k = 0.85f + 0.25f * Mathf.Sin(t * 13f + e.phase * 2f);
                        e.t.localScale = e.motion == Motion.Flicker && e.baseScale.y < 0.05f
                            ? new Vector3(e.baseScale.x * (0.95f + 0.05f * k), e.baseScale.y, e.baseScale.z * (0.95f + 0.05f * k))
                            : new Vector3(e.baseScale.x, e.baseScale.y * k, e.baseScale.z);
                        break;
                    case Motion.Thrust:
                        float th = (airborne ? 1.8f : 1f) * (0.8f + 0.3f * Mathf.Sin(t * 30f + e.phase));
                        e.t.localScale = new Vector3(e.baseScale.x, e.baseScale.y * th, e.baseScale.z);
                        break;
                }
            }
        }

        /// <summary>A standing pose for previews (menu, crate reveal): guard up, facing +Z of the parent.</summary>
        public void PoseIdle(Transform at, float time)
        {
            Vector3 feet = at.position;
            Vector3 fwd = at.forward, right = at.right;
            float bob = Mathf.Sin(time * 2f) * 0.02f;
            Vector3 headPos = feet + Vector3.up * (1.6f + bob);
            Vector3 l = feet + Vector3.up * (1.3f + bob) + fwd * 0.3f - right * 0.18f;
            Vector3 r = feet + Vector3.up * (1.3f + bob) + fwd * 0.3f + right * 0.18f;
            Quaternion rot = Quaternion.LookRotation(fwd);
            Pose(feet, headPos, rot, l, rot, r, rot, fwd, 0f, false, false, false, Time.deltaTime);
        }
    }
}
