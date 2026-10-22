using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>The skeleton every fighter model hangs on. Limb bones point along +Y from their joint.</summary>
    public enum Bone : byte
    {
        Hips, Chest, Neck, Head, Back,
        UpperArmL, UpperArmR, ForearmL, ForearmR, HandL, HandR,
        ThighL, ThighR, ShinL, ShinR, FootL, FootR,
    }

    /// <summary>Which of the fighter's colours a part uses.</summary>
    public enum Slot : byte { Main, Secondary, Accent, Dark, Skin, Eye, EyeWhite, Metal, Ivory, Hair, Gold, Cloth, Glove }
    public enum Shape : byte { Cube, Sphere, Capsule, Cylinder, Limb, Torso } // Limb / Torso: FighterShapes (capsule-sized)
    public enum Anim : byte { None, Sway, Flap, Swing, Orbit, Flicker, Thrust }
    public enum Outfit : byte { Suit, Gi, Armor, Robe, Tech, Primal, Ninja, Tank }

    /// <summary>One shape of a model, in its bone's space (a pure description: no Unity objects).</summary>
    public struct PartSpec
    {
        public string name;
        public Bone bone;
        public Shape shape;
        public Vector3 pos, euler, scale;
        /// <summary>When set, rotation is used instead of euler (finger segments).</summary>
        public Quaternion rotation;
        public bool useRotation;
        public Slot slot;
        public Anim anim;
        public float phase;
        /// <summary>Animated parts swing around this point (bone space) when hasPivot.</summary>
        public Vector3 pivot;
        public bool hasPivot;
    }

    /// <summary>Body proportions in metres (before the fighter's overall scale).</summary>
    public struct BodySpec
    {
        public float shoulderW, chestW, chestD, chestH, waistW, hipW;
        public float upperArm, forearm, armThick, thigh, shin, legThick;
        public float headScale, handScale, hunch;
        public bool legs, boxy;
    }

    /// <summary>
    /// The design of every fighter's model as a list of shapes on a skeleton: body build, face, hair, outfit,
    /// armour, gloves, boots and signature gear (capes, wings, tails, jetpacks, blades, orbs...). Kept free of Unity
    /// objects on purpose, so the same design can be drawn outside the game for preview pictures.
    /// </summary>
    public static class FighterRecipe
    {
        /// <summary>The colour (and shine) of each slot for this fighter. Shared with the preview tool.</summary>
        public static Color SlotColor(Slot slot, FighterStats s, out float smooth, out float metal, out float glowAmount)
        {
            smooth = 0.35f; metal = 0f; glowAmount = 0f;
            switch (slot)
            {
                case Slot.Main: smooth = 0.5f; return s.main;
                case Slot.Secondary: return s.secondary;
                case Slot.Accent: glowAmount = 0.6f; smooth = 0.6f; return s.accent;
                case Slot.Dark: return new Color(0.1f, 0.1f, 0.12f);
                case Slot.Skin: smooth = 0.25f; return s.skin;
                case Slot.Eye: glowAmount = 1.6f; return s.eyes;
                case Slot.EyeWhite: smooth = 0.7f; return new Color(0.96f, 0.96f, 0.94f);
                case Slot.Metal: smooth = 0.75f; metal = 0.8f; return Color.Lerp(s.main, new Color(0.72f, 0.74f, 0.78f), 0.55f);
                case Slot.Ivory: return new Color(0.92f, 0.89f, 0.8f);
                case Slot.Hair: smooth = 0.4f; return s.hair;
                case Slot.Gold: smooth = 0.8f; metal = 1f; return new Color(1f, 0.78f, 0.3f);
                case Slot.Cloth: smooth = 0.15f; return Color.Lerp(s.main, Color.white, 0.12f);
                default: // Glove
                    if (s.glove == GloveType.Glow) { glowAmount = 1.2f; return Color.Lerp(s.main, Color.white, 0.4f); }
                    if (s.glove == GloveType.Metal) { smooth = 0.75f; metal = 0.8f; return Color.Lerp(s.main, new Color(0.72f, 0.74f, 0.78f), 0.55f); }
                    smooth = 0.55f; return s.main;
            }
        }

        public static BodySpec Body(FighterStats s)
        {
            var b = new BodySpec
            {
                shoulderW = 0.44f, chestW = 0.4f, chestD = 0.24f, chestH = 0.34f, waistW = 0.3f, hipW = 0.3f,
                upperArm = 0.29f, forearm = 0.27f, armThick = 0.1f, thigh = 0.4f, shin = 0.41f, legThick = 0.14f,
                headScale = 1.1f, handScale = 1f, legs = true,
            };
            switch (s.body)
            {
                case BodyType.Slim:
                    b.shoulderW = 0.4f; b.chestW = 0.34f; b.chestD = 0.2f; b.chestH = 0.32f; b.waistW = 0.25f; b.hipW = 0.27f; b.armThick = 0.08f; b.legThick = 0.11f; break;
                case BodyType.Bulky:
                    b.shoulderW = 0.56f; b.chestW = 0.52f; b.chestD = 0.32f; b.chestH = 0.38f; b.waistW = 0.4f; b.hipW = 0.36f; b.armThick = 0.13f; b.legThick = 0.16f; b.handScale = 1.15f; break;
                case BodyType.Giant:
                    b.shoulderW = 0.66f; b.chestW = 0.62f; b.chestD = 0.38f; b.chestH = 0.42f; b.waistW = 0.48f; b.hipW = 0.42f; b.armThick = 0.16f; b.legThick = 0.2f;
                    b.headScale = 1f; b.handScale = 1.35f; break;
                case BodyType.Tiny:
                    b.shoulderW = 0.34f; b.chestW = 0.3f; b.chestD = 0.2f; b.chestH = 0.26f; b.waistW = 0.24f; b.hipW = 0.24f; b.armThick = 0.075f; b.legThick = 0.1f;
                    b.upperArm = 0.24f; b.forearm = 0.22f; b.thigh = 0.34f; b.shin = 0.34f; b.headScale = 1.4f; break;
                case BodyType.Robot:
                    b.shoulderW = 0.56f; b.chestW = 0.5f; b.chestD = 0.3f; b.chestH = 0.4f; b.waistW = 0.34f; b.hipW = 0.34f; b.armThick = 0.12f; b.legThick = 0.15f; b.boxy = true; b.handScale = 1.1f; break;
                case BodyType.Beast:
                    b.shoulderW = 0.5f; b.chestW = 0.46f; b.chestD = 0.3f; b.waistW = 0.32f; b.armThick = 0.11f; b.legThick = 0.14f; b.hunch = 0.25f; b.handScale = 1.1f; break;
                case BodyType.Spirit:
                    b.shoulderW = 0.4f; b.chestW = 0.36f; b.chestD = 0.22f; b.waistW = 0.24f; b.armThick = 0.08f; b.legs = false; break;
            }
            return b;
        }

        /// <summary>The point an animated part swings around (bone space): capes from the top, wings from the spine...</summary>
        public static Vector3 PivotOf(PartSpec p)
        {
            if (p.hasPivot) return p.pivot;
            switch (p.anim)
            {
                case Anim.Flap: return new Vector3(Mathf.Sign(p.phase) * 0.08f, 0.1f, -0.06f);
                case Anim.Swing: return new Vector3(0f, -0.4f, -0.1f);
                case Anim.Sway: return p.pos + Quaternion.Euler(p.euler) * new Vector3(0f, p.scale.y * 0.5f, 0f);
                default: return p.pos;
            }
        }

        public static Outfit OutfitOf(FighterStats s)
        {
            if (s.outfitSet) return s.outfit;
            if (s.body == BodyType.Robot) return Outfit.Tech;
            if (s.body == BodyType.Beast) return Outfit.Primal;
            if (s.body == BodyType.Spirit || s.head == HeadType.Hood) return Outfit.Robe;
            if (s.head == HeadType.Mask) return Outfit.Ninja;
            if (s.head == HeadType.Helmet || s.glove == GloveType.Metal) return Outfit.Armor;
            if (s.body == BodyType.Bulky || s.body == BodyType.Giant) return Outfit.Tank;
            return s.specialType == SpecialType.Heal || s.specialType == SpecialType.Whirlwind || s.specialType == SpecialType.Uppercut ? Outfit.Gi : Outfit.Suit;
        }

        public static List<PartSpec> Parts(FighterStats s)
        {
            var list = new List<PartSpec>();
            var b = Body(s);
            var outfit = OutfitOf(s);
            void P(string name, Bone bone, Shape shape, Vector3 pos, Vector3 scale, Slot slot, Vector3 euler = default, Anim anim = Anim.None, float phase = 0f)
                => list.Add(new PartSpec { name = name, bone = bone, shape = shape, pos = pos, scale = scale, euler = euler, slot = slot, anim = anim, phase = phase });

            bool bareArms = outfit == Outfit.Gi || outfit == Outfit.Primal || outfit == Outfit.Tank;
            Slot torsoSlot = outfit == Outfit.Gi ? Slot.Cloth : Slot.Main;
            Slot pants = outfit == Outfit.Gi ? Slot.Cloth : outfit == Outfit.Armor ? Slot.Metal : outfit == Outfit.Tech ? Slot.Metal : Slot.Secondary;
            Shape round = b.boxy ? Shape.Cube : Shape.Capsule;
            // Arms and legs taper like muscle (thick at the shoulder / hip); the torso is V-shaped (FighterShapes).
            Shape limb = b.boxy ? Shape.Cube : Shape.Limb;
            // A capsule of scale y is 2y tall; a cube is y tall.
            float L(float len) => b.boxy ? len * 1.02f : len * 0.55f;

            // ---------------- hips and legs
            P("Pelvis", Bone.Hips, b.boxy ? Shape.Cube : Shape.Sphere, new Vector3(0f, 0f, 0f), new Vector3(b.hipW * 1.15f, 0.2f, b.chestD * 0.95f), pants);
            P("Belt", Bone.Hips, Shape.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(b.waistW * 1.12f, 0.025f, b.chestD * 1.02f), outfit == Outfit.Gi ? Slot.Dark : Slot.Dark);
            P("Buckle", Bone.Hips, Shape.Cube, new Vector3(0f, 0.07f, b.chestD * 0.5f), new Vector3(0.07f, 0.05f, 0.02f), outfit == Outfit.Gi ? Slot.Dark : Slot.Gold);
            if (outfit == Outfit.Gi)
                foreach (float x in new[] { -0.03f, 0.03f })
                    P("BeltTail", Bone.Hips, Shape.Cube, new Vector3(x, -0.03f, b.chestD * 0.52f), new Vector3(0.03f, 0.14f, 0.01f), Slot.Dark, new Vector3(0f, 0f, x * 300f));
            if (outfit == Outfit.Robe)
            {
                P("RobeSkirt", Bone.Hips, Shape.Cylinder, new Vector3(0f, -0.2f, 0f), new Vector3(b.hipW * 1.3f, 0.2f, b.chestD * 1.35f), Slot.Main);
                P("RobeHem", Bone.Hips, Shape.Cylinder, new Vector3(0f, -0.39f, 0f), new Vector3(b.hipW * 1.34f, 0.015f, b.chestD * 1.39f), Slot.Accent);
            }
            if (outfit == Outfit.Armor)
                foreach (float x in new[] { -1f, 1f })
                    P("Tasset", Bone.Hips, Shape.Cube, new Vector3(x * b.hipW * 0.32f, -0.1f, b.chestD * 0.42f), new Vector3(b.hipW * 0.42f, 0.18f, 0.03f), Slot.Metal, new Vector3(-8f, 0f, 0f));
            if (outfit == Outfit.Primal)
                P("Loincloth", Bone.Hips, Shape.Cube, new Vector3(0f, -0.12f, b.chestD * 0.45f), new Vector3(b.hipW * 0.5f, 0.24f, 0.02f), Slot.Hair, new Vector3(-6f, 0f, 0f), Anim.Sway);

            if (b.legs)
            {
                foreach (var (thigh, shin, foot) in new[] { (Bone.ThighL, Bone.ShinL, Bone.FootL), (Bone.ThighR, Bone.ShinR, Bone.FootR) })
                {
                    P("Thigh", thigh, limb, new Vector3(0f, b.thigh * 0.5f, 0f), new Vector3(b.legThick * 1.15f, L(b.thigh), b.legThick * 1.15f), pants);
                    P("Shin", shin, limb, new Vector3(0f, b.shin * 0.5f, 0f), new Vector3(b.legThick, L(b.shin), b.legThick), pants);
                    if (outfit == Outfit.Armor || outfit == Outfit.Tech || s.body == BodyType.Giant)
                        P("KneePad", shin, Shape.Sphere, new Vector3(0f, 0.02f, b.legThick * 0.35f), new Vector3(b.legThick * 1.1f, b.legThick * 1f, b.legThick * 0.8f), Slot.Metal);
                    // Boots: from mid-shin down, then the foot.
                    Slot boot = outfit == Outfit.Gi || outfit == Outfit.Primal ? Slot.Skin : outfit == Outfit.Armor ? Slot.Metal : Slot.Dark;
                    if (outfit != Outfit.Gi && outfit != Outfit.Primal)
                        P("BootTop", shin, Shape.Cylinder, new Vector3(0f, b.shin * 0.82f, 0f), new Vector3(b.legThick * 1.12f, b.shin * 0.17f, b.legThick * 1.12f), boot);
                    if (s.body == BodyType.Beast)
                    {
                        P("Paw", foot, Shape.Sphere, new Vector3(0f, -0.03f, 0.06f), new Vector3(b.legThick * 1.1f, 0.07f, 0.2f), Slot.Hair);
                        for (int c = -1; c <= 1; c++) P("Claw", foot, Shape.Cube, new Vector3(c * 0.03f, -0.05f, 0.17f), new Vector3(0.015f, 0.015f, 0.05f), Slot.Ivory);
                    }
                    else
                    {
                        if (b.boxy) P("Foot", foot, Shape.Cube, new Vector3(0f, -0.04f, 0.05f), new Vector3(b.legThick * 0.95f, 0.085f, 0.26f), boot);
                        else P("Foot", foot, Shape.Capsule, new Vector3(0f, -0.04f, 0.05f), new Vector3(b.legThick * 0.9f, 0.125f, 0.085f), boot, new Vector3(90f, 0f, 0f));
                        P("Sole", foot, Shape.Cube, new Vector3(0f, -0.078f, 0.05f), new Vector3(b.legThick * 0.95f, 0.02f, 0.25f), Slot.Dark);
                        P("ToeCap", foot, Shape.Sphere, new Vector3(0f, -0.045f, 0.15f), new Vector3(b.legThick * 0.85f, 0.06f, 0.07f), outfit == Outfit.Gi || outfit == Outfit.Primal ? Slot.Skin : Slot.Accent);
                    }
                }
            }
            else
            {
                // Spirits float: a tapering wisp instead of legs.
                for (int i = 0; i < 4; i++)
                    P("Wisp", Bone.Hips, Shape.Sphere, new Vector3(0f, -0.15f - i * 0.17f, -i * 0.06f), new Vector3(b.hipW * (1.2f - i * 0.25f), 0.22f, b.chestD * (1.4f - i * 0.28f)), i % 2 == 0 ? Slot.Main : Slot.Secondary, default, Anim.Sway, i);
            }

            // ---------------- chest
            float cw = b.chestW, cd = b.chestD, ch = b.chestH;
            P("Torso", Bone.Chest, b.boxy ? Shape.Cube : Shape.Torso, new Vector3(0f, 0f, 0f), new Vector3(cw, L(ch * 1.13f), cd), torsoSlot);
            P("Waist", Bone.Chest, round, new Vector3(0f, -ch * 0.42f, -0.01f), new Vector3(b.waistW, L(ch * 0.58f), cd * 0.85f), outfit == Outfit.Tank ? Slot.Main : torsoSlot);
            if (s.body == BodyType.Bulky || s.body == BodyType.Giant || s.body == BodyType.Beast)
                foreach (float x in new[] { -1f, 1f })
                    P("Pec", Bone.Chest, Shape.Sphere, new Vector3(x * cw * 0.21f, ch * 0.13f, cd * 0.3f), new Vector3(cw * 0.44f, ch * 0.36f, cd * 0.32f), outfit == Outfit.Tank || outfit == Outfit.Primal ? Slot.Skin : torsoSlot);
            switch (outfit)
            {
                case Outfit.Gi:
                    // Crossed lapels and a coloured trim.
                    P("LapelL", Bone.Chest, Shape.Cube, new Vector3(-cw * 0.08f, ch * 0.05f, cd * 0.5f), new Vector3(0.05f, ch * 0.75f, 0.015f), Slot.Main, new Vector3(0f, 0f, 28f));
                    P("LapelR", Bone.Chest, Shape.Cube, new Vector3(cw * 0.08f, ch * 0.05f, cd * 0.505f), new Vector3(0.05f, ch * 0.75f, 0.015f), Slot.Main, new Vector3(0f, 0f, -28f));
                    P("SkinV", Bone.Chest, Shape.Sphere, new Vector3(0f, ch * 0.25f, cd * 0.42f), new Vector3(cw * 0.22f, ch * 0.3f, cd * 0.2f), Slot.Skin);
                    break;
                case Outfit.Armor:
                    P("Breastplate", Bone.Chest, Shape.Sphere, new Vector3(0f, ch * 0.05f, cd * 0.2f), new Vector3(cw * 1.05f, ch * 0.75f, cd * 0.85f), Slot.Metal);
                    P("Crest", Bone.Chest, Shape.Cube, new Vector3(0f, ch * 0.08f, cd * 0.62f), new Vector3(0.09f, 0.09f, 0.02f), Slot.Accent, new Vector3(0f, 0f, 45f));
                    P("Gorget", Bone.Chest, Shape.Cylinder, new Vector3(0f, ch * 0.42f, 0f), new Vector3(cw * 0.55f, 0.04f, cd * 0.85f), Slot.Metal);
                    break;
                case Outfit.Tech:
                    P("CorePlate", Bone.Chest, Shape.Cube, new Vector3(0f, ch * 0.05f, cd * 0.5f), new Vector3(cw * 0.7f, ch * 0.55f, 0.03f), Slot.Metal);
                    P("Core", Bone.Chest, Shape.Cylinder, new Vector3(0f, ch * 0.08f, cd * 0.52f), new Vector3(0.1f, 0.012f, 0.1f), Slot.Accent, new Vector3(90f, 0f, 0f));
                    foreach (float x in new[] { -1f, 1f })
                        P("GlowLine", Bone.Chest, Shape.Cube, new Vector3(x * cw * 0.42f, -ch * 0.05f, cd * 0.4f), new Vector3(0.012f, ch * 0.6f, 0.012f), Slot.Accent);
                    break;
                case Outfit.Robe:
                    P("RobeFront", Bone.Chest, Shape.Cube, new Vector3(0f, -ch * 0.05f, cd * 0.51f), new Vector3(0.07f, ch * 0.9f, 0.01f), Slot.Accent);
                    P("Sash", Bone.Chest, Shape.Cube, new Vector3(0f, -ch * 0.05f, 0f), new Vector3(cw * 1.04f, 0.06f, cd * 1.04f), Slot.Secondary, new Vector3(0f, 0f, 25f));
                    break;
                case Outfit.Primal:
                    P("FurMantle", Bone.Chest, Shape.Sphere, new Vector3(0f, ch * 0.38f, -cd * 0.05f), new Vector3(cw * 1.15f, ch * 0.35f, cd * 1.2f), Slot.Hair);
                    P("Strap", Bone.Chest, Shape.Cube, new Vector3(0f, 0f, cd * 0.48f), new Vector3(0.04f, ch * 0.9f, 0.012f), Slot.Dark, new Vector3(0f, 0f, 35f));
                    break;
                case Outfit.Ninja:
                    P("Wrap", Bone.Chest, Shape.Cube, new Vector3(0f, 0f, cd * 0.5f), new Vector3(0.05f, ch * 0.8f, 0.012f), Slot.Accent, new Vector3(0f, 0f, -30f));
                    P("Wrap2", Bone.Chest, Shape.Cube, new Vector3(0f, -ch * 0.3f, 0f), new Vector3(b.waistW * 1.1f, 0.05f, cd * 1.05f), Slot.Accent);
                    break;
                case Outfit.Tank:
                    P("Tank", Bone.Chest, round, new Vector3(0f, -ch * 0.05f, 0.005f), new Vector3(cw * 0.96f, L(ch * 0.9f), cd * 1.02f), Slot.Main);
                    P("TankStripe", Bone.Chest, Shape.Cube, new Vector3(0f, -ch * 0.05f, cd * 0.52f), new Vector3(cw * 0.7f, 0.04f, 0.01f), Slot.Accent);
                    break;
                default: // Suit: a racing stripe and an emblem.
                    P("Stripe", Bone.Chest, Shape.Cube, new Vector3(0f, 0f, cd * 0.5f), new Vector3(cw * 0.18f, ch * 0.85f, 0.012f), Slot.Accent);
                    P("Emblem", Bone.Chest, Shape.Cube, new Vector3(0f, ch * 0.15f, cd * 0.51f), new Vector3(0.08f, 0.08f, 0.015f), Slot.Gold, new Vector3(0f, 0f, 45f));
                    P("Collar", Bone.Chest, Shape.Cylinder, new Vector3(0f, ch * 0.4f, 0f), new Vector3(cw * 0.5f, 0.035f, cd * 0.8f), Slot.Secondary);
                    break;
            }
            // Shoulders (bigger pads on armoured and heavy fighters).
            bool pads = s.shoulderPads || outfit == Outfit.Armor || s.body == BodyType.Giant || s.body == BodyType.Robot;
            foreach (float x in new[] { -1f, 1f })
            {
                Vector3 sp = new Vector3(x * b.shoulderW * 0.5f, ch * 0.32f, 0f);
                P("Deltoid", Bone.Chest, b.boxy ? Shape.Cube : Shape.Sphere, sp, Vector3.one * b.armThick * 1.45f, bareArms ? Slot.Skin : torsoSlot);
                if (pads)
                {
                    P("ShoulderPad", Bone.Chest, b.boxy ? Shape.Cube : Shape.Sphere, sp + new Vector3(x * 0.02f, 0.045f, 0f), new Vector3(b.armThick * 2.3f, b.armThick * 1.2f, b.armThick * 2.1f), Slot.Metal, new Vector3(0f, 0f, -x * 18f));
                    P("PadTrim", Bone.Chest, Shape.Cube, sp + new Vector3(x * b.armThick * 1.05f, 0.03f, 0f), new Vector3(0.02f, b.armThick * 0.8f, b.armThick * 2f), Slot.Accent, new Vector3(0f, 0f, -x * 18f));
                }
            }
            P("Neck", Bone.Neck, Shape.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f * b.headScale + 0.02f, 0.06f, 0.1f * b.headScale + 0.02f), b.boxy ? Slot.Metal : Slot.Skin);
            if (s.scarf)
            {
                P("Scarf", Bone.Neck, Shape.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.17f, 0.045f, 0.16f), Slot.Accent);
                P("ScarfTail", Bone.Back, Shape.Cube, new Vector3(0.05f, 0.08f, -0.06f), new Vector3(0.07f, 0.4f, 0.015f), Slot.Accent, new Vector3(35f, 0f, 10f), Anim.Sway);
            }

            // ---------------- arms and gloves
            foreach (var (upper, fore, hand, x) in new[] { (Bone.UpperArmL, Bone.ForearmL, Bone.HandL, -1f), (Bone.UpperArmR, Bone.ForearmR, Bone.HandR, 1f) })
            {
                Slot sleeve = bareArms ? Slot.Skin : outfit == Outfit.Armor ? Slot.Metal : torsoSlot;
                P("UpperArm", upper, limb, new Vector3(0f, b.upperArm * 0.5f, 0f), new Vector3(b.armThick * 1.1f, L(b.upperArm), b.armThick * 1.1f), sleeve);
                if (s.body == BodyType.Bulky || s.body == BodyType.Giant)
                    P("Bicep", upper, Shape.Sphere, new Vector3(0f, b.upperArm * 0.45f, b.armThick * 0.2f), new Vector3(b.armThick * 1.3f, b.upperArm * 0.5f, b.armThick * 1.3f), sleeve);
                Slot fore_ = bareArms || outfit == Outfit.Suit || outfit == Outfit.Ninja ? (outfit == Outfit.Suit || outfit == Outfit.Ninja ? torsoSlot : Slot.Skin) : outfit == Outfit.Armor ? Slot.Metal : torsoSlot;
                P("Forearm", fore, limb, new Vector3(0f, b.forearm * 0.5f, 0f), new Vector3(b.armThick, L(b.forearm), b.armThick), fore_);
                P("Elbow", fore, Shape.Sphere, Vector3.zero, Vector3.one * b.armThick * 1.05f, fore_);
                // Bracer / wrist wraps.
                P("Bracer", fore, Shape.Cylinder, new Vector3(0f, b.forearm * 0.78f, 0f), new Vector3(b.armThick * 1.2f, b.forearm * 0.2f, b.armThick * 1.2f),
                    outfit == Outfit.Gi || outfit == Outfit.Tank ? Slot.Cloth : outfit == Outfit.Tech || outfit == Outfit.Armor ? Slot.Metal : Slot.Secondary);
                P("BracerGlow", fore, Shape.Cylinder, new Vector3(0f, b.forearm * 0.7f, 0f), new Vector3(b.armThick * 1.25f, 0.008f, b.armThick * 1.25f), Slot.Accent);
                Glove(list, hand, x, s, b);
            }

            // ---------------- head
            Head(list, s, b);

            // ---------------- back gear
            BackGear(list, s, b);
            return list;
        }

        /// <summary>
        /// A gloved hand with five fingers. Hand space: the palm centre is where the controller's grip is, +Z is where
        /// your knuckles point, +Y is the thumb side (up when you hold the controller), and the palm faces the body
        /// (-X on the right hand, +X on the left).
        /// </summary>
        static void Glove(List<PartSpec> list, Bone hand, float side, FighterStats s, BodySpec b)
        {
            float k = 1.15f * b.handScale;
            void P(string name, Shape shape, Vector3 pos, Vector3 scale, Slot slot, Vector3 euler = default)
                => list.Add(new PartSpec { name = name, bone = hand, shape = shape, pos = pos * k, scale = scale * k, euler = euler, slot = slot });
            bool metal = s.glove == GloveType.Metal;
            float big = s.glove == GloveType.Big ? 1.2f : 1f;
            // Palm and the back of the hand.
            P("Palm", metal ? Shape.Cube : Shape.Sphere, new Vector3(side * 0.02f, 0f, -0.005f), new Vector3(0.04f, 0.085f, 0.095f) * big, Slot.Glove);
            P("HandBack", Shape.Cube, new Vector3(side * 0.033f, 0f, 0f), new Vector3(0.014f, 0.07f, 0.07f) * big, metal ? Slot.Metal : Slot.Glove);
            P("KnuckleGuard", metal ? Shape.Cube : Shape.Capsule, new Vector3(side * 0.03f, 0f, 0.035f * big), metal ? new Vector3(0.022f, 0.085f, 0.026f) * big : new Vector3(0.024f, 0.045f, 0.024f) * big, Slot.Accent);
            P("Cuff", Shape.Cylinder, new Vector3(side * 0.012f, 0f, -0.065f), new Vector3(0.075f, 0.03f, 0.09f) * big, Slot.Dark, new Vector3(90f, 0f, 0f));
            P("CuffTrim", Shape.Cylinder, new Vector3(side * 0.012f, 0f, -0.048f), new Vector3(0.08f, 0.005f, 0.095f) * big, Slot.Accent, new Vector3(90f, 0f, 0f));
            if (metal) P("Spikes", Shape.Cube, new Vector3(side * 0.045f, 0f, 0.035f), new Vector3(0.02f, 0.07f, 0.012f), Slot.Accent);
            Fingers(list, hand, side, s, b, 0.9f);
        }

        struct FingerDef { public Vector3 root, dir, axis; public float[] len, bend; public float thick; }

        static FingerDef[] FingerDefs(float side, float big)
        {
            var defs = new FingerDef[5];
            float[] ys = { 0.03f, 0.01f, -0.01f, -0.029f };
            float[][] lens = { new[] { 0.045f, 0.028f, 0.022f }, new[] { 0.05f, 0.03f, 0.024f }, new[] { 0.046f, 0.028f, 0.022f }, new[] { 0.036f, 0.022f, 0.018f } };
            for (int f = 0; f < 4; f++)
                defs[f] = new FingerDef
                {
                    root = new Vector3(side * 0.012f, ys[f] * big, 0.04f * big), dir = new Vector3(0f, ys[f] * 0.5f, 1f).normalized,
                    axis = new Vector3(0f, -side, 0f), len = lens[f], bend = new[] { 85f, 95f, 70f }, thick = (f == 3 ? 0.018f : 0.021f) * big,
                };
            // Thumb: from the base of the palm on the thumb side, forward and across; it folds over the fingers.
            Vector3 tdir = new Vector3(-side * 0.45f, 0.45f, 0.77f).normalized;
            defs[4] = new FingerDef
            {
                root = new Vector3(side * 0.0f, 0.032f * big, -0.02f * big), dir = tdir, axis = Vector3.Cross(tdir, new Vector3(-side, 0f, 0f)).normalized,
                len = new[] { 0.035f, 0.03f, 0.024f }, bend = new[] { 25f, 45f, 35f }, thick = 0.023f * big,
            };
            return defs;
        }

        /// <summary>
        /// The finger segments of one hand, closed by curl (0 = open hand, 1 = fist). Called again every frame with the
        /// player's grip to move the same parts, in the same order.
        /// </summary>
        public static void Fingers(List<PartSpec> list, Bone hand, float side, FighterStats s, BodySpec b, float curl)
        {
            float k = 1.15f * b.handScale;
            bool metal = s.glove == GloveType.Metal, claws = s.glove == GloveType.Claws;
            float big = s.glove == GloveType.Big ? 1.2f : 1f;
            var defs = FingerDefs(side, big);
            for (int f = 0; f < defs.Length; f++)
            {
                var d = defs[f];
                Vector3 p = d.root, dir = d.dir;
                float thick = d.thick;
                for (int i = 0; i < 3; i++)
                {
                    dir = Quaternion.AngleAxis(d.bend[i] * Mathf.Clamp01(curl) + (f == 4 ? 0f : 4f), d.axis) * dir;
                    float len = d.len[i] * big;
                    Vector3 mid = p + dir * (len * 0.5f);
                    Quaternion rot = FighterPose.Frame(dir, Vector3.up);
                    list.Add(new PartSpec
                    {
                        name = "Finger", bone = hand, shape = metal ? Shape.Cube : Shape.Capsule, pos = mid * k, rotation = rot, useRotation = true,
                        scale = (metal ? new Vector3(thick, len, thick) : new Vector3(thick, (len + thick) * 0.5f, thick)) * k, slot = Slot.Glove,
                    });
                    p += dir * len;
                    thick *= 0.92f;
                }
                if (claws && f < 4)
                    list.Add(new PartSpec
                    {
                        name = "Finger", bone = hand, shape = Shape.Cube, pos = (p + dir * 0.015f) * k, rotation = FighterPose.Frame(dir, Vector3.up), useRotation = true,
                        scale = new Vector3(0.008f, 0.035f, 0.008f) * k, slot = Slot.Ivory,
                    });
            }
        }

        static void Head(List<PartSpec> list, FighterStats s, BodySpec b)
        {
            float k = b.headScale;
            void P(string name, Shape shape, Vector3 pos, Vector3 scale, Slot slot, Vector3 euler = default, Anim anim = Anim.None, float phase = 0f)
                => list.Add(new PartSpec { name = name, bone = Bone.Head, shape = shape, pos = pos * k, scale = scale * k, euler = euler, slot = slot, anim = anim, phase = phase });

            bool human = s.head == HeadType.Round || s.head == HeadType.Mask;
            void Face(bool mouth)
            {
                foreach (float x in new[] { -0.045f, 0.045f })
                {
                    P("EyeWhite", Shape.Sphere, new Vector3(x, 0.015f, 0.098f), new Vector3(0.045f, 0.035f, 0.02f), Slot.EyeWhite);
                    P("Iris", Shape.Sphere, new Vector3(x, 0.014f, 0.106f), new Vector3(0.027f, 0.029f, 0.012f), Slot.Eye);
                    P("Pupil", Shape.Sphere, new Vector3(x, 0.014f, 0.111f), new Vector3(0.012f, 0.014f, 0.006f), Slot.Dark);
                    P("Brow", Shape.Cube, new Vector3(x, 0.05f, 0.1f), new Vector3(0.05f, 0.012f, 0.012f), Slot.Hair, new Vector3(0f, 0f, x > 0 ? 14f : -14f));
                }
                P("Nose", Shape.Sphere, new Vector3(0f, -0.012f, 0.112f), new Vector3(0.025f, 0.035f, 0.025f), Slot.Skin);
                if (mouth) P("Mouth", Shape.Cube, new Vector3(0f, -0.052f, 0.106f), new Vector3(0.05f, 0.008f, 0.01f), Slot.Dark);
                foreach (float x in new[] { -1f, 1f })
                    P("Ear", Shape.Sphere, new Vector3(x * 0.112f, 0f, 0f), new Vector3(0.025f, 0.05f, 0.035f), Slot.Skin);
            }
            void GlowEyes(float z, float size)
            {
                foreach (float x in new[] { -0.045f, 0.045f })
                    P("Eye", Shape.Sphere, new Vector3(x, 0.01f, z), new Vector3(size * 1.3f, size, size * 0.6f), Slot.Eye);
            }

            switch (s.head)
            {
                case HeadType.Box:
                    P("Head", Shape.Cube, Vector3.zero, new Vector3(0.24f, 0.22f, 0.23f), b.boxy ? Slot.Metal : Slot.Main);
                    P("Screen", Shape.Cube, new Vector3(0f, 0.01f, 0.112f), new Vector3(0.19f, 0.12f, 0.01f), Slot.Dark);
                    P("EyeBar", Shape.Cube, new Vector3(0f, 0.025f, 0.118f), new Vector3(0.15f, 0.03f, 0.005f), Slot.Eye);
                    P("Grill", Shape.Cube, new Vector3(0f, -0.04f, 0.118f), new Vector3(0.08f, 0.012f, 0.005f), Slot.Accent);
                    foreach (float x in new[] { -1f, 1f }) P("EarBolt", Shape.Cylinder, new Vector3(x * 0.125f, 0f, 0f), new Vector3(0.06f, 0.015f, 0.06f), Slot.Accent, new Vector3(0f, 0f, 90f));
                    break;
                case HeadType.Helmet:
                    P("Helmet", Shape.Sphere, new Vector3(0f, 0.01f, -0.005f), new Vector3(0.255f, 0.27f, 0.265f), Slot.Main);
                    P("Faceplate", Shape.Sphere, new Vector3(0f, -0.02f, 0.06f), new Vector3(0.2f, 0.2f, 0.16f), Slot.Metal);
                    P("Visor", Shape.Cube, new Vector3(0f, 0.015f, 0.12f), new Vector3(0.17f, 0.045f, 0.03f), Slot.Eye);
                    P("Crest", Shape.Cube, new Vector3(0f, 0.12f, -0.01f), new Vector3(0.035f, 0.06f, 0.24f), Slot.Accent);
                    foreach (float x in new[] { -1f, 1f }) P("CheekGuard", Shape.Cube, new Vector3(x * 0.1f, -0.05f, 0.05f), new Vector3(0.03f, 0.08f, 0.08f), Slot.Metal, new Vector3(0f, x * 20f, 0f));
                    break;
                case HeadType.Hood:
                    P("Hood", Shape.Sphere, new Vector3(0f, 0.015f, -0.02f), new Vector3(0.285f, 0.31f, 0.3f), Slot.Main);
                    P("HoodPeak", Shape.Cube, new Vector3(0f, 0.14f, 0.03f), new Vector3(0.07f, 0.07f, 0.07f), Slot.Main, new Vector3(45f, 0f, 0f));
                    P("Shadow", Shape.Sphere, new Vector3(0f, -0.01f, 0.05f), new Vector3(0.2f, 0.22f, 0.2f), Slot.Dark);
                    GlowEyes(0.135f, 0.03f);
                    break;
                case HeadType.Skull:
                    P("Skull", Shape.Sphere, new Vector3(0f, 0.02f, 0f), new Vector3(0.235f, 0.24f, 0.25f), Slot.Ivory);
                    P("Jaw", Shape.Cube, new Vector3(0f, -0.075f, 0.04f), new Vector3(0.15f, 0.06f, 0.14f), Slot.Ivory);
                    foreach (float x in new[] { -0.05f, 0.05f })
                        P("Socket", Shape.Sphere, new Vector3(x, 0.015f, 0.095f), new Vector3(0.065f, 0.06f, 0.05f), Slot.Dark);
                    GlowEyes(0.115f, 0.022f);
                    for (int i = -2; i <= 2; i++) P("Tooth", Shape.Cube, new Vector3(i * 0.022f, -0.06f, 0.11f), new Vector3(0.016f, 0.025f, 0.01f), Slot.EyeWhite);
                    P("NoseHole", Shape.Cube, new Vector3(0f, -0.022f, 0.118f), new Vector3(0.025f, 0.03f, 0.01f), Slot.Dark, new Vector3(0f, 0f, 45f));
                    break;
                case HeadType.Mask:
                    P("Face", Shape.Sphere, Vector3.zero, new Vector3(0.22f, 0.25f, 0.23f), Slot.Skin);
                    Face(false);
                    P("Mask", Shape.Sphere, new Vector3(0f, -0.055f, 0.025f), new Vector3(0.235f, 0.14f, 0.23f), Slot.Main);
                    P("Headband", Shape.Cylinder, new Vector3(0f, 0.065f, 0f), new Vector3(0.235f, 0.025f, 0.24f), Slot.Accent);
                    P("BandTail", Shape.Cube, new Vector3(0.03f, 0.04f, -0.14f), new Vector3(0.03f, 0.16f, 0.01f), Slot.Accent, new Vector3(30f, 0f, 15f), Anim.Sway);
                    break;
                default:
                    P("Face", Shape.Sphere, Vector3.zero, new Vector3(0.22f, 0.25f, 0.23f), Slot.Skin);
                    P("Chin", Shape.Sphere, new Vector3(0f, -0.07f, 0.03f), new Vector3(0.14f, 0.1f, 0.14f), Slot.Skin);
                    Face(true);
                    break;
            }

            // Hair / headwear on top.
            Slot hair = human ? Slot.Hair : Slot.Accent;
            if (human && s.top != TopType.None)
            {
                P("HairCap", Shape.Sphere, new Vector3(0f, 0.055f, -0.03f), new Vector3(0.238f, 0.2f, 0.24f), Slot.Hair);
                P("HairBack", Shape.Sphere, new Vector3(0f, 0.0f, -0.05f), new Vector3(0.23f, 0.2f, 0.17f), Slot.Hair);
            }
            switch (s.top)
            {
                case TopType.Spikes:
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (i - 4) * 18f;
                        float tilt = -35f - Mathf.Abs(i - 4) * 5f;
                        P("Spike", Shape.Cube, Quaternion.Euler(-25f, a, 0f) * new Vector3(0f, 0.12f, -0.045f), new Vector3(0.055f, 0.2f - Mathf.Abs(i - 4) * 0.012f, 0.055f), hair, new Vector3(tilt, a, (i - 4) * -9f));
                    }
                    break;
                case TopType.Mohawk:
                    for (int i = 0; i < 7; i++)
                        P("Mohawk", Shape.Cube, new Vector3(0f, 0.13f + Mathf.Sin(i * 0.5f) * 0.025f, 0.1f - i * 0.038f), new Vector3(0.03f, 0.11f, 0.045f), Slot.Accent, new Vector3(-12f, 0f, 0f));
                    break;
                case TopType.Horns:
                    foreach (float x in new[] { -1f, 1f })
                    {
                        P("HornBase", Shape.Capsule, new Vector3(x * 0.1f, 0.11f, 0f), new Vector3(0.05f, 0.08f, 0.05f), Slot.Ivory, new Vector3(0f, 0f, -x * 40f));
                        P("HornMid", Shape.Capsule, new Vector3(x * 0.155f, 0.19f, -0.01f), new Vector3(0.04f, 0.07f, 0.04f), Slot.Ivory, new Vector3(-10f, 0f, -x * 15f));
                        P("HornTip", Shape.Capsule, new Vector3(x * 0.165f, 0.26f, -0.03f), new Vector3(0.025f, 0.05f, 0.025f), Slot.Accent, new Vector3(-25f, 0f, x * 10f));
                    }
                    break;
                case TopType.Crown:
                    P("CrownBand", Shape.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.21f, 0.025f, 0.21f), Slot.Gold);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        P("CrownPoint", Shape.Cube, new Vector3(Mathf.Sin(a) * 0.1f, 0.165f, Mathf.Cos(a) * 0.1f), new Vector3(0.03f, 0.06f, 0.03f), Slot.Gold, new Vector3(0f, a * Mathf.Rad2Deg + 45f, 0f));
                    }
                    P("CrownGem", Shape.Sphere, new Vector3(0f, 0.135f, 0.106f), new Vector3(0.03f, 0.03f, 0.015f), Slot.Accent);
                    break;
                case TopType.Halo:
                    P("Halo", Shape.Cylinder, new Vector3(0f, 0.25f, -0.02f), new Vector3(0.26f, 0.006f, 0.26f), Slot.Eye, new Vector3(-12f, 0f, 0f), Anim.Flicker);
                    break;
                case TopType.Antennae:
                    foreach (float x in new[] { -1f, 1f })
                    {
                        P("Antenna", Shape.Cylinder, new Vector3(x * 0.07f, 0.19f, 0f), new Vector3(0.012f, 0.08f, 0.012f), Slot.Dark, new Vector3(0f, 0f, -x * 22f));
                        P("AntennaTip", Shape.Sphere, new Vector3(x * 0.1f, 0.27f, 0f), Vector3.one * 0.04f, Slot.Eye, default, Anim.Flicker, x);
                    }
                    break;
                case TopType.Ears:
                    foreach (float x in new[] { -1f, 1f })
                    {
                        P("BeastEar", Shape.Cube, new Vector3(x * 0.085f, 0.15f, -0.01f), new Vector3(0.065f, 0.12f, 0.03f), Slot.Hair, new Vector3(0f, 0f, -x * 18f));
                        P("EarInner", Shape.Cube, new Vector3(x * 0.085f, 0.145f, 0.004f), new Vector3(0.035f, 0.08f, 0.01f), Slot.Accent, new Vector3(0f, 0f, -x * 18f));
                    }
                    break;
                case TopType.Ponytail:
                    P("Ponytail", Shape.Capsule, new Vector3(0f, -0.04f, -0.16f), new Vector3(0.06f, 0.15f, 0.06f), Slot.Hair, new Vector3(28f, 0f, 0f), Anim.Sway);
                    P("Tie", Shape.Cylinder, new Vector3(0f, 0.05f, -0.13f), new Vector3(0.05f, 0.02f, 0.05f), Slot.Accent, new Vector3(60f, 0f, 0f));
                    break;
                case TopType.Flames:
                    for (int i = 0; i < 6; i++)
                        P("Flame", Shape.Cube, new Vector3((i - 2.5f) * 0.04f, 0.15f + (i % 2) * 0.02f, -0.02f), new Vector3(0.045f, 0.15f, 0.045f), Slot.Accent, new Vector3(0f, 45f, (i - 2.5f) * -10f), Anim.Flicker, i);
                    break;
            }
        }

        static void BackGear(List<PartSpec> list, FighterStats s, BodySpec b)
        {
            void P(string name, Shape shape, Vector3 pos, Vector3 scale, Slot slot, Vector3 euler = default, Anim anim = Anim.None, float phase = 0f)
                => list.Add(new PartSpec { name = name, bone = Bone.Back, shape = shape, pos = pos, scale = scale, euler = euler, slot = slot, anim = anim, phase = phase });
            float w = b.shoulderW;
            switch (s.back)
            {
                case BackType.Cape:
                    P("Cape", Shape.Cube, new Vector3(0f, -0.38f, -0.03f), new Vector3(w * 0.95f, 0.8f, 0.02f), Slot.Main, new Vector3(6f, 0f, 0f), Anim.Sway);
                    P("CapeLining", Shape.Cube, new Vector3(0f, -0.38f, -0.015f), new Vector3(w * 0.9f, 0.78f, 0.01f), Slot.Secondary, new Vector3(6f, 0f, 0f), Anim.Sway);
                    foreach (float x in new[] { -1f, 1f }) P("Clasp", Shape.Sphere, new Vector3(x * w * 0.35f, 0.02f, 0.02f), Vector3.one * 0.05f, Slot.Gold);
                    break;
                case BackType.Wings:
                    foreach (float x in new[] { -1f, 1f })
                        for (int f = 0; f < 4; f++)
                            P("Feather", Shape.Cube, new Vector3(x * (0.12f + f * 0.11f), 0.12f - f * 0.07f, -0.06f), new Vector3(0.12f, 0.42f - f * 0.06f, 0.015f),
                                f % 2 == 0 ? Slot.Accent : Slot.Main, new Vector3(0f, 0f, x * (55f + f * 12f)), Anim.Flap, x);
                    break;
                case BackType.Tail:
                    for (int i = 0; i < 5; i++)
                        P("Tail", Shape.Capsule, new Vector3(0f, -0.42f - i * 0.05f, -0.12f - i * 0.13f), new Vector3(0.09f - i * 0.013f, 0.1f, 0.09f - i * 0.013f), i == 4 ? Slot.Accent : Slot.Hair, new Vector3(75f - i * 10f, 0f, 0f), Anim.Swing, i * 0.4f);
                    break;
                case BackType.Jetpack:
                    P("Pack", Shape.Cube, new Vector3(0f, -0.08f, -0.06f), new Vector3(0.24f, 0.3f, 0.12f), Slot.Metal);
                    foreach (float x in new[] { -0.08f, 0.08f })
                    {
                        P("Thruster", Shape.Cylinder, new Vector3(x, -0.22f, -0.08f), new Vector3(0.09f, 0.08f, 0.09f), Slot.Dark);
                        P("JetFlame", Shape.Sphere, new Vector3(x, -0.34f, -0.08f), new Vector3(0.07f, 0.12f, 0.07f), Slot.Accent, default, Anim.Thrust, x * 10f);
                    }
                    P("PackLight", Shape.Cube, new Vector3(0f, 0.02f, -0.125f), new Vector3(0.12f, 0.025f, 0.01f), Slot.Accent);
                    break;
                case BackType.Blades:
                    foreach (float a in new[] { -32f, 32f })
                    {
                        P("Blade", Shape.Cube, Quaternion.Euler(0f, 0f, a) * new Vector3(0f, 0.08f, 0f) + new Vector3(0f, 0f, -0.1f), new Vector3(0.05f, 0.85f, 0.01f), Slot.Accent, new Vector3(0f, 0f, a));
                        P("Hilt", Shape.Cube, Quaternion.Euler(0f, 0f, a) * new Vector3(0f, -0.38f, 0f) + new Vector3(0f, 0f, -0.1f), new Vector3(0.12f, 0.03f, 0.03f), Slot.Gold, new Vector3(0f, 0f, a));
                    }
                    break;
                case BackType.Orbs:
                    for (int i = 0; i < 3; i++)
                        P("Orb", Shape.Sphere, Vector3.zero, Vector3.one * 0.085f, Slot.Eye, default, Anim.Orbit, i * Mathf.PI * 2f / 3f);
                    break;
                case BackType.Spines:
                    for (int i = 0; i < 5; i++)
                        P("Spine", Shape.Cube, new Vector3(0f, 0.08f - i * 0.11f, -0.13f), new Vector3(0.05f, 0.13f - i * 0.012f, 0.13f - i * 0.012f), Slot.Accent, new Vector3(45f, 0f, 45f));
                    break;
            }
        }
    }
}
