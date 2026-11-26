using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public enum Outfit { Operator, Raider, RaiderHeavy, Gunsmith, Medic, Fence, Quartermaster, Kiosk, Foreman, BossKessler, BossHarbor, BossWarden, BossBanker, BossCourier, BossSanta }

    /// <summary>
    /// A procedurally built humanoid made of rigid segments on a bone hierarchy. Used for the player's own
    /// ragdoll body, remote players, raid NPCs and traders. Bones carry kinematic rigidbodies + joints so the
    /// whole thing can go full ragdoll with one call.
    /// </summary>
    public class HumanRig
    {
        public GameObject root;
        public Transform hips, spine, chest, neck, head, uaL, uaR, faL, faR, handL, handR, thL, thR, shL, shR, footL, footR;
        public readonly List<Transform> bones = new List<Transform>();
        public readonly List<Rigidbody> bodies = new List<Rigidbody>();
        public readonly List<Collider> colliders = new List<Collider>();
        public readonly List<Renderer> headRenderers = new List<Renderer>();
        public readonly List<GameObject> gearVisuals = new List<GameObject>(); // worn helmet / vest / backpack (ArmorBuilder.Dress)
        public float upperArm = 0.29f, foreArm = 0.27f, thigh = 0.45f, shin = 0.45f, spineLen = 0.36f, neckLen = 0.16f;
        public bool ragdoll;
        public Transform weaponR; // where an NPC rifle sits
        float _phase;

        static Material ClothMat => Mats.Lit("cloth", Color.white, 0f, 0.15f, Mats.Fabric, Mats.FabricN, 0.4f, 0.05f);
        static Material HardMat => Mats.Lit("gear_hard", Color.white, 0.2f, 0.4f, Mats.Noise, null, 0.3f, 0.35f);
        static Material SkinMat => Mats.Lit("skin_v", Color.white, 0f, 0.35f, Mats.Noise, null, 0.1f, 0f);

        // --------------------------------------------------------------------------------------------- build
        public static HumanRig Build(Outfit outfit, int seed, Transform parent, bool localPlayer = false, string look = null)
        {
            var r = new HumanRig();
            var rng = new Rng(seed);
            _seed = seed;
            r.root = new GameObject("Human_" + outfit);
            r.root.transform.SetParent(parent, false);

            var pal = Palette(outfit, rng);
            if (outfit == Outfit.Operator)
            {
                // (3.48) operators always wear clothes: the wardrobe's picks, or the combat uniform if they never chose
                pal.dressed = true; pal.look = Cosmetics.Decode(look);
                pal.mask = false; pal.cap = false; pal.beanie = false; pal.gasMask = pal.look.face == 3;
            }
            r.hips = Bone(r, "Hips", r.root.transform, new Vector3(0, 0.98f, 0));
            r.spine = Bone(r, "Spine", r.hips, new Vector3(0, 0.10f, 0));
            r.chest = Bone(r, "Chest", r.spine, new Vector3(0, 0.20f, 0));
            r.neck = Bone(r, "Neck", r.chest, new Vector3(0, 0.12f, 0));
            r.head = Bone(r, "Head", r.neck, new Vector3(0, 0.1f, 0));
            r.uaL = Bone(r, "UpperArmL", r.chest, new Vector3(-0.19f, 0.1f, 0));
            r.uaR = Bone(r, "UpperArmR", r.chest, new Vector3(0.19f, 0.1f, 0));
            r.faL = Bone(r, "ForeArmL", r.uaL, new Vector3(0, 0, r.upperArm));
            r.faR = Bone(r, "ForeArmR", r.uaR, new Vector3(0, 0, r.upperArm));
            r.handL = Bone(r, "HandL", r.faL, new Vector3(0, 0, r.foreArm));
            r.handR = Bone(r, "HandR", r.faR, new Vector3(0, 0, r.foreArm));
            r.thL = Bone(r, "ThighL", r.hips, new Vector3(-0.1f, -0.04f, 0));
            r.thR = Bone(r, "ThighR", r.hips, new Vector3(0.1f, -0.04f, 0));
            r.shL = Bone(r, "ShinL", r.thL, new Vector3(0, 0, r.thigh));
            r.shR = Bone(r, "ShinR", r.thR, new Vector3(0, 0, r.thigh));
            r.footL = Bone(r, "FootL", r.shL, new Vector3(0, 0, r.shin));
            r.footR = Bone(r, "FootR", r.shR, new Vector3(0, 0, r.shin));
            // limbs point along +Z in bone space; rotate the default pose so they hang down
            foreach (var b in new[] { r.uaL, r.uaR, r.thL, r.thR }) b.localRotation = Quaternion.Euler(90, 0, 0);

            int shape = 0;   // 1 snowman, 2 Evil Santa: their hitboxes are resized to the body you see
            if (outfit == Outfit.BossSanta) { BuildSanta(r); shape = 2; }                    // Evil Santa (North Pole boss)
            else if (PoleMap && (outfit == Outfit.Raider || outfit == Outfit.RaiderHeavy) && !localPlayer) BuildElf(r, outfit == Outfit.RaiderHeavy, rng);   // North Pole: evil elves
            else if (Snowman(outfit) && !localPlayer) { BuildSnowman(r, outfit, rng); shape = 1; }   // Christmas: the raiders are snowmen
            else BuildMeshes(r, outfit, pal, rng, localPlayer);
            BuildPhysics(r);
            if (shape == 1) { float k = outfit == Outfit.RaiderHeavy ? 1.12f : 1f; Fit(r.hips, new Vector3(0, -0.13f, 0), 0.29f * k, 0.6f * k, 0); Fit(r.spine, new Vector3(0, 0.12f, 0), 0.2f * k, 0.4f * k, 0); Fit(r.chest, new Vector3(0, 0.12f, 0), 0.14f, 0.3f, 0); Fit(r.head, new Vector3(0, 0.07f, 0), 0.14f, 0.28f, 1); foreach (var t in new[] { r.uaL, r.uaR, r.faL, r.faR }) Fit(t, null, 0.035f, -1f, -1); foreach (var t in new[] { r.thL, r.thR }) Fit(t, null, 0.1f, -1f, -1); foreach (var t in new[] { r.shL, r.shR }) Fit(t, null, 0.085f, -1f, -1); }
            if (shape == 2) { Fit(r.hips, new Vector3(0, -0.02f, 0.02f), 0.23f, 0.46f, 0); Fit(r.spine, new Vector3(0, 0.1f, 0.04f), 0.25f, 0.5f, 0); Fit(r.chest, new Vector3(0, 0.08f, 0.01f), 0.2f, 0.44f, 0); Fit(r.head, new Vector3(0, 0.06f, 0.03f), 0.12f, 0.3f, 1); foreach (var t in new[] { r.uaL, r.uaR }) Fit(t, null, 0.07f, -1f, -1); foreach (var t in new[] { r.thL, r.thR }) Fit(t, null, 0.095f, -1f, -1); }
            r.weaponR = new GameObject("WeaponR").transform;
            r.weaponR.SetParent(r.handR, false);
            return r;
        }

        static Transform Bone(HumanRig r, string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            r.bones.Add(t);
            return t;
        }

        struct Pal { public Color top, top2, pants, boots, gear, gear2, skin, helmet, accent, hair; public bool helmetOn, mask, beanie, gasMask, plate, cap, apron, coat, suit;
            public bool dressed; public Cosmetics.Look look; }   // dressed: an operator in clothes from the wardrobe

        static Pal Palette(Outfit o, Rng rng)
        {
            var p = new Pal();
            Color[] skins = { new Color(0.86f, 0.68f, 0.56f), new Color(0.72f, 0.53f, 0.4f), new Color(0.52f, 0.36f, 0.26f), new Color(0.36f, 0.24f, 0.17f), new Color(0.92f, 0.76f, 0.66f) };
            p.skin = rng.Pick(skins);
            p.hair = rng.Pick(new[] { new Color(0.1f, 0.08f, 0.06f), new Color(0.25f, 0.16f, 0.08f), new Color(0.05f, 0.05f, 0.05f), new Color(0.55f, 0.42f, 0.25f) });
            p.boots = rng.Pick(new[] { new Color(0.12f, 0.1f, 0.08f), new Color(0.42f, 0.33f, 0.22f), new Color(0.08f, 0.08f, 0.08f) });
            switch (o)
            {
                case Outfit.Operator:
                    var schemes = new[]
                    {
                        (new Color(0.36f, 0.38f, 0.28f), new Color(0.55f, 0.5f, 0.38f)),
                        (new Color(0.2f, 0.22f, 0.24f), new Color(0.12f, 0.12f, 0.13f)),
                        (new Color(0.52f, 0.46f, 0.36f), new Color(0.6f, 0.53f, 0.4f)),
                        (new Color(0.24f, 0.28f, 0.33f), new Color(0.3f, 0.32f, 0.3f)),
                    };
                    var s = rng.Pick(schemes);
                    p.top = s.Item1 * 1.1f; p.top2 = s.Item1; p.pants = s.Item1 * 0.95f; p.gear = s.Item2; p.gear2 = s.Item2 * 0.8f;
                    p.helmet = s.Item2 * 0.9f; p.accent = new Color(0.15f, 0.15f, 0.15f);
                    p.helmetOn = false; p.mask = rng.Chance(0.6f); p.plate = false; // armour is found in raids and worn separately
                    break;
                case Outfit.Raider:
                case Outfit.RaiderHeavy:
                    p.top = rng.Pick(new[] { new Color(0.3f, 0.26f, 0.2f), new Color(0.18f, 0.2f, 0.24f), new Color(0.35f, 0.12f, 0.1f), new Color(0.22f, 0.25f, 0.18f), new Color(0.4f, 0.38f, 0.34f) });
                    p.top2 = p.top * 0.8f;
                    p.pants = rng.Pick(new[] { new Color(0.16f, 0.2f, 0.3f), new Color(0.28f, 0.25f, 0.2f), new Color(0.12f, 0.12f, 0.12f), new Color(0.3f, 0.32f, 0.24f) });
                    p.gear = rng.Pick(new[] { new Color(0.12f, 0.12f, 0.1f), new Color(0.32f, 0.3f, 0.22f) });
                    p.gear2 = p.gear * 0.8f; p.accent = new Color(0.7f, 0.08f, 0.06f);
                    p.helmet = new Color(0.25f, 0.27f, 0.2f);
                    p.beanie = rng.Chance(0.4f); p.mask = rng.Chance(0.5f); p.gasMask = !p.beanie && rng.Chance(0.35f);
                    p.cap = !p.beanie && !p.gasMask && rng.Chance(0.5f);
                    p.helmetOn = false; p.plate = false; // raider armour is real, droppable gear (Npc.Spawn)
                    break;
                // map bosses: each dressed so you know them on sight
                case Outfit.BossKessler:   // lab coat over scrubs, surgical mask
                    p.top = new Color(0.9f, 0.91f, 0.9f); p.top2 = new Color(0.3f, 0.5f, 0.55f); p.pants = new Color(0.22f, 0.4f, 0.45f); p.gear = new Color(0.75f, 0.1f, 0.08f); p.gear2 = p.gear * 0.8f;
                    p.coat = true; p.mask = true; p.accent = new Color(0.75f, 0.1f, 0.08f); p.helmetOn = false; p.plate = false;
                    break;
                case Outfit.BossHarbor:    // rust-orange foul-weather coat, navy trousers, watch cap
                    p.top = new Color(0.72f, 0.3f, 0.1f); p.top2 = new Color(0.14f, 0.18f, 0.3f); p.pants = new Color(0.14f, 0.18f, 0.3f); p.gear = new Color(0.78f, 0.6f, 0.28f); p.gear2 = p.gear * 0.8f;
                    p.coat = true; p.beanie = false; p.accent = new Color(0.78f, 0.6f, 0.28f); p.helmetOn = false; p.plate = false;
                    break;
                case Outfit.BossWarden:    // forest green camo, face covered
                    p.top = new Color(0.2f, 0.3f, 0.15f); p.top2 = new Color(0.32f, 0.36f, 0.2f); p.pants = new Color(0.25f, 0.3f, 0.17f); p.gear = new Color(0.36f, 0.26f, 0.14f); p.gear2 = p.gear * 0.8f;
                    p.mask = true; p.accent = new Color(0.36f, 0.26f, 0.14f); p.helmetOn = false; p.plate = false;
                    break;
                case Outfit.BossBanker:    // black suit, gold tie and trim
                    p.top = new Color(0.08f, 0.08f, 0.09f); p.top2 = new Color(0.85f, 0.65f, 0.2f); p.pants = new Color(0.08f, 0.08f, 0.09f); p.gear = new Color(0.85f, 0.65f, 0.2f); p.gear2 = p.gear * 0.8f;
                    p.suit = true; p.accent = new Color(0.85f, 0.65f, 0.2f); p.helmetOn = false; p.plate = false;
                    break;
                case Outfit.BossCourier:   // 1930s look: pinstripe charcoal suit, white shirt, fedora-dark cap
                    p.top = new Color(0.16f, 0.16f, 0.18f); p.top2 = new Color(0.9f, 0.9f, 0.86f); p.pants = new Color(0.16f, 0.16f, 0.18f); p.gear = new Color(0.45f, 0.08f, 0.08f); p.gear2 = p.gear * 0.8f;
                    p.suit = true; p.cap = true; p.accent = new Color(0.45f, 0.08f, 0.08f); p.helmetOn = false; p.plate = false;
                    break;
                case Outfit.Gunsmith:
                    p.top = new Color(0.32f, 0.3f, 0.28f); p.top2 = p.top; p.pants = new Color(0.2f, 0.22f, 0.28f); p.gear = new Color(0.35f, 0.24f, 0.14f); p.apron = true; p.cap = true; p.accent = new Color(0.5f, 0.3f, 0.1f);
                    break;
                case Outfit.Medic:
                    p.top = new Color(0.85f, 0.86f, 0.85f); p.top2 = new Color(0.25f, 0.45f, 0.5f); p.pants = new Color(0.25f, 0.45f, 0.5f); p.gear = new Color(0.8f, 0.1f, 0.1f); p.coat = true; p.mask = true; p.accent = new Color(0.8f, 0.1f, 0.1f);
                    break;
                case Outfit.Fence:
                    p.top = new Color(0.1f, 0.1f, 0.12f); p.top2 = new Color(0.55f, 0.1f, 0.12f); p.pants = new Color(0.1f, 0.1f, 0.12f); p.gear = new Color(0.8f, 0.65f, 0.25f); p.suit = true; p.accent = new Color(0.8f, 0.65f, 0.25f);
                    break;
                case Outfit.Foreman:
                    // work shirt + hi-vis vest (apron panels) + orange cap
                    p.top = new Color(0.22f, 0.28f, 0.4f); p.top2 = p.top * 0.85f; p.pants = new Color(0.3f, 0.27f, 0.22f); p.gear = new Color(1f, 0.5f, 0.06f); p.apron = true; p.cap = true; p.accent = new Color(0.9f, 0.9f, 0.3f);
                    break;
                case Outfit.Quartermaster:
                    p.top = new Color(0.3f, 0.33f, 0.24f); p.top2 = p.top * 0.85f; p.pants = new Color(0.28f, 0.3f, 0.22f); p.gear = new Color(0.25f, 0.2f, 0.15f); p.beanie = true; p.plate = false; p.accent = new Color(0.9f, 0.5f, 0.1f);
                    break;
                default:
                    p.top = new Color(0.15f, 0.2f, 0.3f); p.top2 = p.top; p.pants = new Color(0.1f, 0.1f, 0.12f); p.gear = new Color(0.9f, 0.5f, 0.1f); p.cap = true; p.accent = new Color(0.9f, 0.5f, 0.1f);
                    break;
            }
            return p;
        }

        // sub-mesh slots for characters
        const int CLOTH = 0, HARD = 1, PLASTIC = 2, JOINT = 3;
        public static Material PlasticMat => Mats.Lit("dummy_plastic", Color.white, 0f, 0.62f, Mats.Noise, null, 0.22f, 0.18f);
        /// <summary>The ragdoll figure's smooth, slightly soft body (3.47).</summary>
        public static Material RagdollMat => Mats.Lit("ragdoll_body", Color.white, 0f, 0.42f, Mats.Noise, null, 0.12f, 0.04f);
        public static Material JointMat => Mats.Lit("dummy_joint", Color.white, 0.75f, 0.55f, Mats.Metal, Mats.MetalN, 0.2f, 0.4f);
        public static readonly Color JointCol = new Color(0.16f, 0.16f, 0.17f);
        static readonly Color MarkYellow = new Color(1f, 0.58f, 0.01f), MarkBlack = new Color(0.04f, 0.04f, 0.04f);

        /// <summary>Plastic shell colour of a ragdoll dummy. The player's colour is fixed by their profile seed so their VR hands match their body.</summary>
        public static Color BodyColor(Outfit o, int seed)
        {
            // picked in sRGB; vertex colours are used as linear values, so convert to keep the plastic saturated
            var rng = new Rng(seed * 7 + 3);
            Color c;
            switch (o)
            {
                case Outfit.Operator:
                    {
                        // your own colour: spread over the whole colour wheel (golden-ratio steps) plus shade and
                        // brightness, so practically no two operators look the same (6 colours used to repeat a lot)
                        float u = seed * 0.6180339887f; u -= Mathf.Floor(u);
                        float v = seed * 0.3819660113f; v -= Mathf.Floor(v);
                        float w = seed * 0.1213203436f; w -= Mathf.Floor(w);
                        c = Color.HSVToRGB(u, Mathf.Lerp(0.45f, 0.9f, v), Mathf.Lerp(0.7f, 0.97f, w));
                    }
                    break;
                case Outfit.Raider:
                case Outfit.RaiderHeavy:
                    c = rng.Pick(new[] { new Color(0.22f, 0.22f, 0.23f), new Color(0.5f, 0.14f, 0.1f), new Color(0.34f, 0.34f, 0.28f), new Color(0.3f, 0.25f, 0.22f) });
                    break;
                case Outfit.BossKessler: c = new Color(0.86f, 0.84f, 0.8f); break;
                case Outfit.BossHarbor: c = new Color(0.16f, 0.2f, 0.32f); break;
                case Outfit.BossWarden: c = new Color(0.3f, 0.38f, 0.22f); break;
                case Outfit.BossBanker: c = new Color(0.88f, 0.68f, 0.22f); break;
                case Outfit.BossCourier: c = new Color(0.55f, 0.1f, 0.1f); break;
                default:
                    c = new Color(0.94f, 0.93f, 0.9f);
                    break;
            }
            return new Color(Mathf.Pow(c.r, 2.2f), Mathf.Pow(c.g, 2.2f), Mathf.Pow(c.b, 2.2f), 1f);
        }

        /// <summary>Yellow/black crash-test target sticker.</summary>
        static void Marker(MB mb, Vector3 c, Vector3 normal, float r)
        {
            if (_human) return;   // people don't wear crash-test stickers
            var rot = Quaternion.LookRotation(normal);
            for (int q = 0; q < 4; q++) mb.Sub(PLASTIC).Sector(c + normal * 0.002f, rot, r, q * 90f, q * 90f + 90f, 6, q % 2 == 0 ? MarkYellow : MarkBlack);
        }

        /// <summary>A moulded body part (3.47 ragdoll): a rounded-box shape lofted from its bottom (t=0) to its top (t=1) -
        /// half-width w(t), half-depth in front df(t) and behind db(t). side/end exponents below 1 make it boxier; a furrow
        /// runs a shallow groove down the middle of the front (a sternum).</summary>
        static void Loft(MB mb, Vector3 c, float hy, System.Func<float, float> w, System.Func<float, float> df, System.Func<float, float> db,
                         float side, float end, int nu, int nv, Color col, float furrow = 0f, float ph0 = 0f, float ph1 = 1f)
        {
            mb.Patch(nu, nv, (iu, iv) =>
            {
                float th = iu / (float)nu * Mathf.PI * 2f, ph = Mathf.Lerp(ph0, ph1, iv / (float)nv) * Mathf.PI;   // ph0..ph1: only a band of it (0 = top)
                float cy = Mathf.Cos(ph), sy = Mathf.Sin(ph);
                float Y = Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), end), H = Mathf.Pow(sy, end);
                float t = 0.5f + 0.5f * Y;
                float cx = Mathf.Cos(th), sz = Mathf.Sin(th);
                float X = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), side), Z = Mathf.Sign(sz) * Mathf.Pow(Mathf.Abs(sz), side);
                float x = X * H * w(t), z = Z * H * (Z > 0f ? df(t) : db(t));
                if (furrow > 0f && Z > 0f) z -= furrow * Mathf.Exp(-(x * x) / 0.0004f) * H;
                return c + new Vector3(x, Y * hy, z);
            }, col, true, true);
        }

        // the body's moulded shapes - the clothes are cut from the same ones, a little bigger
        static float ChestW(float t) => Mathf.Lerp(0.132f, 0.171f, MathX.Step(0f, 0.78f, t));
        static float ChestDf(float t) => 0.09f + 0.017f * Mathf.Sin(Mathf.Clamp01((t - 0.15f) / 0.8f) * Mathf.PI);
        static float ChestDb(float t) => 0.094f + 0.006f * t;
        // (3.50) narrower and shallower right at the bottom, so trousers run into the legs instead of bulging like a nappy
        static float PelvisW(float t) => Mathf.Lerp(0.1f, 0.144f, MathX.Step(0f, 0.8f, t));
        static float PelvisDf(float t) => 0.074f + 0.012f * MathX.Step(0f, 0.55f, t) + 0.004f * t;
        static float PelvisDb(float t) => (0.088f + 0.008f * Mathf.Sin(Mathf.Clamp01(t * 1.3f) * Mathf.PI)) * Mathf.Lerp(0.86f, 1f, MathX.Step(0f, 0.4f, t));

        // ------------------------------------------------------------------------------------------------ clothes (3.48)
        /// <summary>What someone wears. top: 0 combat shirt, 1 t-shirt, 2 long sleeve, 3 hoodie, 4 jacket, 5 tank top,
        /// 6 none. bottom: 0 combat pants, 1 cargo pants, 2 jeans, 3 shorts, 4 none. Colours are linear.</summary>
        struct Garb { public int top, bottom; public Color top1, top2, bot, accent; public bool patch; }
        const int TopNone = 6, BottomNone = 4;
        static bool Sleeved(int top) => top != TopNone && top != 5;
        static bool LongSleeved(int top) => top == 0 || top == 2 || top == 3 || top == 4;
        static Color Sh(Color c, float k) { var r = c * k; r.a = 1f; return r; }
        /// <summary>A combat shirt's body: a lighter, plainer fabric than its sleeves and shoulders.</summary>
        static Color CombatBody(Color c) => Color.Lerp(c, Lin3(new Color(0.6f, 0.56f, 0.46f)), 0.28f);
        static Color TorsoCol(Garb g) => g.top == 0 ? CombatBody(g.top1) : g.top1;

        /// <summary>Trousers over the pelvis (fly, back pockets), and the hem of a top worn loose over them.</summary>
        static void GarbPelvis(MB mb, Garb g)
        {
            if (g.bottom != BottomNone)
            {
                Color c = g.bot;
                mb.Sub(CLOTH);
                Loft(mb, new Vector3(0, -0.03f, 0), 0.088f, t => PelvisW(t) + 0.012f, t => PelvisDf(t) + 0.012f, t => PelvisDb(t) + 0.012f, 0.7f, 0.62f, 26, 12, c);
                mb.Sub(CLOTH).Box(new Vector3(0, 0.0f, 0.1015f), new Vector3(0.008f, 0.07f, 0.003f), Sh(c, 0.7f), 0f);   // fly
                foreach (float sx in new[] { -1f, 1f })   // back pockets, turned to follow the seat
                    mb.Sub(CLOTH).Box(new Vector3(sx * 0.054f, -0.03f, -0.1063f), new Vector3(0.052f, 0.066f, 0.003f), Sh(c, 0.84f), 0.001f, 1, Quaternion.Euler(0, -sx * 6f, 0));
            }
            if (g.top != TopNone && g.top != 0)   // anything but the combat shirt is worn untucked: its hem hangs over the waistband
            {
                mb.Sub(CLOTH);
                Loft(mb, new Vector3(0, -0.03f, 0), 0.088f, t => PelvisW(t) + 0.024f, t => PelvisDf(t) + 0.022f, t => PelvisDb(t) + 0.022f, 0.7f, 0.62f, 26, 6, TorsoCol(g), 0f, 0f, 0.42f);
            }
        }

        /// <summary>The top round the waist: tucked in (combat shirt) or hanging loose; hoodie pocket, jacket zip and pockets.</summary>
        static void GarbWaist(MB mb, Garb g)
        {
            if (g.top == TopNone) return;
            Color c = TorsoCol(g);
            mb.Sub(CLOTH);
            Loft(mb, new Vector3(0, 0.06f, 0), 0.16f, t => 0.126f + 0.011f * t, t => 0.101f, t => 0.103f, 0.78f, 0.3f, 26, 10, c);
            if (g.top == 3) mb.Sub(CLOTH).Box(new Vector3(0, 0.01f, 0.1035f), new Vector3(0.17f, 0.085f, 0.008f), Sh(c, 0.82f), 0.006f);   // kangaroo pocket
            if (g.top == 4)
            {
                mb.Sub(CLOTH).Box(new Vector3(0, 0.06f, 0.1018f), new Vector3(0.008f, 0.27f, 0.0035f), new Color(0.06f, 0.06f, 0.06f), 0f);   // zip
                foreach (float sx in new[] { -1f, 1f })   // hand-warmer pocket welts
                    mb.Sub(CLOTH).Box(new Vector3(sx * 0.075f, 0.0f, 0.1f), new Vector3(0.012f, 0.085f, 0.006f), Sh(g.top2, 0.9f), 0.002f, 1, Quaternion.Euler(0, sx * 18f, sx * -14f));
            }
        }

        /// <summary>The top over the chest: a body cut from the chest's own shape, a yoke, shoulders, collar or hood, pockets.</summary>
        static void GarbChest(MB mb, Garb g)
        {
            if (g.top == TopNone) return;
            Color main = g.top1, torso = TorsoCol(g);
            float grow = g.top == 5 ? 0.009f : 0.013f;
            const float cy = 0.078f, hy = 0.135f, end = 0.66f;
            mb.Sub(CLOTH);
            Loft(mb, new Vector3(0, cy, 0.002f), hy, t => ChestW(t) + grow, t => ChestDf(t) + grow, t => ChestDb(t) + grow - 0.001f, 0.72f, end, 32, 16, torso);
            if (g.top == 0 || g.top == 4)   // a yoke over the shoulders and upper chest - a seam line and the second colour
                Loft(mb, new Vector3(0, cy, 0.002f), hy, t => ChestW(t) + grow + 0.003f, t => ChestDf(t) + grow + 0.003f, t => ChestDb(t) + grow + 0.002f, 0.72f, end, 32, 6, g.top == 0 ? main : g.top2, 0f, 0f, 0.36f);
            if (Sleeved(g.top))
                foreach (float sx in new[] { -1f, 1f }) mb.Sub(CLOTH).Sphere(new Vector3(sx * 0.171f, 0.121f, 0.004f), new Vector3(0.065f, 0.061f, 0.086f), 14, main);   // shoulders, level with the shoulder line
            Color dark = Sh(main, 0.72f);
            switch (g.top)
            {
                case 0:   // mock-neck collar with a short zip
                    mb.Sub(CLOTH).Cyl(new Vector3(0, 0.186f, -0.004f), new Vector3(0, 0.236f, -0.002f), 0.057f, 0.052f, 18, main, false);
                    mb.Sub(CLOTH).Cyl(new Vector3(0, 0.233f, -0.002f), new Vector3(0, 0.238f, -0.002f), 0.0525f, 0.051f, 18, dark, false);
                    mb.Sub(CLOTH).Box(new Vector3(0, 0.212f, 0.0545f), new Vector3(0.006f, 0.05f, 0.003f), new Color(0.05f, 0.05f, 0.05f), 0f, 1, Quaternion.Euler(-6f, 0, 0));
                    mb.Sub(HARD).Box(new Vector3(0, 0.226f, 0.058f), new Vector3(0.007f, 0.014f, 0.004f), new Color(0.3f, 0.3f, 0.3f), 0.001f);   // zip pull
                    break;
                case 1: case 2:   // crew neck
                    mb.Sub(CLOTH).Cyl(new Vector3(0, 0.194f, 0f), new Vector3(0, 0.212f, 0f), 0.06f, 0.056f, 18, dark, false);
                    break;
                case 3:   // hood down on the back, drawstrings at the front
                    mb.Sub(CLOTH).Sphere(new Vector3(0, 0.205f, -0.072f), new Vector3(0.105f, 0.052f, 0.068f), 14, Sh(main, 0.92f));
                    mb.Sub(CLOTH).Cyl(new Vector3(0, 0.19f, 0f), new Vector3(0, 0.22f, -0.004f), 0.064f, 0.06f, 18, main, false);
                    foreach (float sx in new[] { -1f, 1f }) mb.Sub(CLOTH).Capsule(new Vector3(sx * 0.03f, 0.2f, 0.07f), new Vector3(sx * 0.034f, 0.1f, 0.122f), 0.0035f, 0.0035f, 6, new Color(0.75f, 0.75f, 0.72f));
                    break;
                case 4:   // stand collar, full zip, chest pockets with flaps
                    mb.Sub(CLOTH).Cyl(new Vector3(0, 0.18f, -0.006f), new Vector3(0, 0.25f, -0.01f), 0.075f, 0.068f, 18, g.top2, false);
                    for (int k = 0; k < 6; k++)
                    {
                        float y0 = -0.045f + k * 0.04f, y1 = y0 + 0.04f, ym = (y0 + y1) * 0.5f;
                        float z0 = LoftFront(y0, cy, hy, end, t => ChestDf(t) + grow), z1 = LoftFront(y1, cy, hy, end, t => ChestDf(t) + grow);
                        mb.Sub(CLOTH).Box(new Vector3(0, ym, (z0 + z1) * 0.5f + 0.002f), new Vector3(0.008f, 0.041f, 0.0035f), new Color(0.06f, 0.06f, 0.06f), 0f, 1, Quaternion.Euler(Mathf.Atan2(z1 - z0, 0.04f) * -Mathf.Rad2Deg, 0, 0));
                    }
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Sub(CLOTH).Box(new Vector3(sx * 0.08f, 0.1f, 0.1185f), new Vector3(0.066f, 0.07f, 0.01f), Sh(main, 0.9f), 0.004f, 1, Quaternion.Euler(0, -sx * 9.5f, 0));
                        mb.Sub(CLOTH).Box(new Vector3(sx * 0.08f, 0.132f, 0.1205f), new Vector3(0.07f, 0.022f, 0.011f), g.top2, 0.004f, 1, Quaternion.Euler(0, -sx * 9.5f, 0));
                    }
                    break;
                case 5:   // tank top: a binding round the neckline
                    mb.Sub(CLOTH).Cyl(new Vector3(0, 0.188f, 0.004f), new Vector3(0, 0.2f, 0.004f), 0.074f, 0.07f, 18, dark, false);
                    break;
            }
        }

        /// <summary>Where the front (x = 0) of a loft is at height y, for things sewn onto it.</summary>
        static float LoftFront(float y, float cy, float hy, float end, System.Func<float, float> df)
        {
            float Y = Mathf.Clamp((y - cy) / hy, -0.999f, 0.999f);
            float c = Mathf.Sign(Y) * Mathf.Pow(Mathf.Abs(Y), 1f / end);
            return Mathf.Pow(Mathf.Sqrt(Mathf.Max(0f, 1f - c * c)), end) * df(0.5f + 0.5f * Y);
        }

        static void GarbUpperArm(MB mb, Garb g, float side, float len)
        {
            if (!Sleeved(g.top)) return;
            Color c = g.top1;
            bool tee = g.top == 1;
            float end = tee ? len * 0.52f : len - 0.02f;
            mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.02f), new Vector3(0, 0, end), 0.059f, tee ? 0.057f : 0.053f, 14, c);
            if (tee) mb.Sub(CLOTH).Cyl(new Vector3(0, 0, end - 0.012f), new Vector3(0, 0, end + 0.004f), 0.0585f, 0.058f, 14, Sh(c, 0.85f), false);   // the sleeve's hem
            if (g.top == 0 || g.top == 4)   // a sleeve pocket with its flap, and a patch on it
            {
                mb.Sub(CLOTH).Box(new Vector3(side * 0.059f, 0, 0.112f), new Vector3(0.018f, 0.048f, 0.07f), Sh(c, 0.9f), 0.005f);
                mb.Sub(CLOTH).Box(new Vector3(side * 0.061f, 0, 0.072f), new Vector3(0.02f, 0.052f, 0.024f), Sh(c, 0.76f), 0.005f);
                if (g.patch) mb.Sub(HARD).Box(new Vector3(side * 0.0695f, 0, 0.115f), new Vector3(0.004f, 0.036f, 0.04f), g.accent, 0.0015f);
            }
        }

        static void GarbForeArm(MB mb, Garb g, float len)
        {
            if (!LongSleeved(g.top)) return;
            Color c = g.top1;
            mb.Sub(CLOTH).Capsule(new Vector3(0, 0, -0.01f), new Vector3(0, 0, len - 0.055f), 0.05f, 0.044f, 14, c);
            mb.Sub(CLOTH).Sphere(new Vector3(0, 0, 0.085f), new Vector3(0.051f, 0.049f, 0.08f), 12, c);   // over the forearm's fuller top
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0, len - 0.072f), new Vector3(0, 0, len - 0.042f), 0.0472f, 0.0462f, 14, g.top == 4 ? g.top2 : Sh(c, 0.8f), false);   // cuff
        }

        static void GarbThigh(MB mb, Garb g, float side, float len)
        {
            if (g.bottom == BottomNone) return;
            Color c = g.bot;
            bool shorts = g.bottom == 3, jeans = g.bottom == 2;
            float end = shorts ? len * 0.52f : len - 0.02f;
            mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0f), new Vector3(0, 0, end), 0.09f, shorts ? 0.081f : (jeans ? 0.068f : 0.072f), 14, c);
            if (shorts) mb.Sub(CLOTH).Cyl(new Vector3(0, 0, end - 0.016f), new Vector3(0, 0, end + 0.004f), 0.0838f, 0.083f, 14, Sh(c, 0.82f), false);
            if (g.bottom == 0 || g.bottom == 1)   // a cargo pocket on the outside of the thigh, with its flap
            {
                float px = g.bottom == 1 ? 0.088f : 0.085f;
                mb.Sub(CLOTH).Box(new Vector3(side * px, 0.004f, 0.215f), new Vector3(0.032f, 0.085f, 0.12f), Sh(c, 0.88f), 0.008f);
                mb.Sub(CLOTH).Box(new Vector3(side * (px + 0.003f), 0.004f, 0.16f), new Vector3(0.034f, 0.09f, 0.03f), Sh(c, 0.74f), 0.006f);
            }
        }

        static void GarbShin(MB mb, Garb g, float len)
        {
            if (g.bottom == BottomNone || g.bottom == 3) return;
            Color c = g.bot;
            float end = len - 0.1f;
            mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0f), new Vector3(0, 0, end), 0.064f, 0.058f, 14, c);
            mb.Sub(CLOTH).Sphere(new Vector3(0, -0.012f, 0.15f), new Vector3(0.061f, 0.065f, 0.118f), 12, c);   // over the calf
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0, end - 0.03f), new Vector3(0, 0, end + 0.012f), 0.0638f, 0.0628f, 14, Sh(c, 0.9f), false);   // bunched over the boot
        }

        /// <summary>The ragdoll's moulded head (3.47): an egg with a narrower jaw, a flatter face and a rounded back of the
        /// skull - still faceless.</summary>
        static void RagdollHead(MB mb, Vector3 c, Color col)
        {
            // (3.50) a mannequin's head: still no eyes or mouth, but the planes of a face - a brow, the hollows under it,
            // the bridge and tip of a nose, cheekbones, the curve over the teeth, a chin - and ears. The columns and rows
            // bunch up over the face so the small shapes come out smooth.
            const int NU = 48, NV = 32;
            float G(float x, float y, float cx, float cy, float sx, float sy) { float a = (x - cx) / sx, b = (y - cy) / sy; return Mathf.Exp(-(a * a + b * b)); }
            mb.Patch(NU, NV, (iu, iv) =>
            {
                float su = iu / (float)NU * 2f - 1f, tv = iv / (float)NV * 2f - 1f;
                float th = Mathf.PI * 0.5f + Mathf.PI * (0.45f * su + 0.55f * su * su * su);          // the face (+z) in the middle of the strip
                float ph = Mathf.PI * 0.5f + Mathf.PI * 0.5f * (0.55f * tv + 0.45f * tv * tv * tv);   // 0 top .. PI bottom
                float ny = Mathf.Cos(ph), sr = Mathf.Sin(ph);
                float nx = Mathf.Cos(th) * sr, nz = Mathf.Sin(th) * sr;
                float low = MathX.Step(0.1f, -0.95f, ny);                                   // 0 above the middle, 1 at the chin
                float rx = 0.084f * (1f - 0.15f * low), rz = 0.1f * (1f - 0.1f * low);
                if (nz > 0f) rz *= 1f - 0.1f * nz * nz * nz * (1f - 0.5f * low);             // a flatter face
                else rz *= 1f - 0.06f * nz * MathX.Step(-0.3f, 0.45f, ny);                  // the rounded back of the skull
                var pt = new Vector3(nx * rx, ny * 0.112f, nz * rz + low * 0.008f);
                float f = MathX.Step(0.25f, 0.75f, nz);                                     // features fade out round the sides
                float d = f * (0.0055f * G(nx, ny, 0f, 0.17f, 0.42f, 0.07f)                                                        // brow
                             - 0.0065f * (G(nx, ny, -0.33f, 0.03f, 0.15f, 0.09f) + G(nx, ny, 0.33f, 0.03f, 0.15f, 0.09f))          // hollows under it
                             + 0.014f * G(nx, ny, 0f, -0.16f, 0.085f, 0.16f) + 0.006f * G(nx, ny, 0f, -0.27f, 0.075f, 0.06f)        // nose: bridge and tip
                             + 0.004f * (G(nx, ny, -0.46f, -0.1f, 0.17f, 0.11f) + G(nx, ny, 0.46f, -0.1f, 0.17f, 0.11f))            // cheekbones
                             + 0.0035f * G(nx, ny, 0f, -0.44f, 0.28f, 0.1f)                                                        // over the teeth
                             + 0.0065f * G(nx, ny, 0f, -0.69f, 0.2f, 0.09f));                                                      // chin
                d -= 0.003f * (G(nx, ny, -0.78f, 0.25f, 0.16f, 0.16f) + G(nx, ny, 0.78f, 0.25f, 0.16f, 0.16f)) * MathX.Step(-0.2f, 0.3f, nz);   // temples
                return c + pt + new Vector3(nx / rx, ny / 0.112f, nz / rz).normalized * d;
            }, col, true, true);
            foreach (float sx in new[] { -1f, 1f })   // ears
                mb.Sphere(c + new Vector3(sx * 0.0845f, -0.004f, -0.008f), new Vector3(0.011f, 0.03f, 0.019f), 10, col);
        }

        /// <summary>(3.50) A hand with fingers: palm, four two-part fingers in a relaxed curl and a thumb. glove: 0 none,
        /// 1 fingerless (the palm and the first joints), 2 a full glove. +y is the back of the hand, +z along the fingers.</summary>
        static void RagdollHand(MB mb, float side, Color skin, Color gloveCol, int glove)
        {
            int Sub(bool g) => g ? CLOTH : SKIN;
            Color Col(bool g) => g ? gloveCol : skin;
            bool g1 = glove >= 1, g2 = glove >= 2;
            mb.Sub(Sub(g1)).Box(new Vector3(0, -0.003f, 0.038f), new Vector3(0.078f, 0.03f, 0.076f), Col(g1), 0.012f, 3);   // palm
            float[] l1 = { 0.03f, 0.033f, 0.031f, 0.025f }, l2 = { 0.026f, 0.029f, 0.027f, 0.022f }, rad = { 0.0086f, 0.009f, 0.0086f, 0.0076f };
            float[] c1 = { 14f, 18f, 22f, 26f }, c2 = { 24f, 28f, 31f, 34f }, spread = { -6f, -2f, 2f, 6f };
            for (int k = 0; k < 4; k++)   // index (next to the thumb) to little finger
            {
                var b0 = new Vector3(side * (-0.026f + k * 0.0175f), -0.003f, 0.072f);
                var d1 = Quaternion.Euler(c1[k], side * spread[k], 0) * Vector3.forward;
                var d2 = Quaternion.Euler(c1[k] + c2[k], side * spread[k], 0) * Vector3.forward;
                var e1 = b0 + d1 * l1[k]; var e2 = e1 + d2 * l2[k];
                mb.Sub(Sub(g1)).Capsule(b0, e1, rad[k], rad[k] * 0.95f, 8, Col(g1));
                mb.Sub(Sub(g2)).Capsule(e1, e2, rad[k] * 0.93f, rad[k] * 0.82f, 8, Col(g2));
            }
            var t0 = new Vector3(side * -0.033f, -0.011f, 0.022f);
            var t1 = t0 + new Vector3(side * -0.55f, -0.45f, 0.7f).normalized * 0.03f;
            var t2 = t1 + new Vector3(side * -0.2f, -0.5f, 0.84f).normalized * 0.026f;
            mb.Sub(Sub(g1)).Capsule(t0, t1, 0.0115f, 0.0105f, 8, Col(g1));
            mb.Sub(Sub(g2)).Capsule(t1, t2, 0.0098f, 0.0088f, 8, Col(g2));
        }

        static void Ball(MB mb, Vector3 c, float r) { if (_human) mb.Sub(PLASTIC).Sphere(c, Vector3.one * r * 0.95f, 14, _ballCol); else mb.Sub(JOINT).Sphere(c, Vector3.one * r, 12, JointCol); }
        [System.ThreadStatic] static bool _human; [System.ThreadStatic] static Color _ballCol;
        const int SKIN = PLASTIC;
        static Color Lin3(Color c) => new Color(Mathf.Pow(c.r, 2.2f), Mathf.Pow(c.g, 2.2f), Mathf.Pow(c.b, 2.2f), 1f);

        static void BuildMeshes(HumanRig r, Outfit o, Pal p, Rng rng, bool local)
        {
            var mats = new[] { ClothMat, HardMat, RagdollMat, JointMat };   // (3.47) a faceless ragdoll figure: slot 2 is its smooth body
            _human = true;
            Color body = BodyColor(o, _seed);
            Color bodyDark = body * 0.72f; bodyDark.a = 1;
            Color skinC = body;   // the head, neck and hands are the figure's own colour
            _ballCol = bodyDark;
            bool soldier = o == Outfit.Operator || o == Outfit.Raider || o == Outfit.RaiderHeavy;
            bool kit = o == Outfit.Raider || o == Outfit.RaiderHeavy;   // raiders wear real clothes now (3.45): jacket, cargo trousers, gloves
            Color glove = new Color(0.09f, 0.085f, 0.08f);
            if (kit)   // palette colours are picked by eye (sRGB); vertex colours are linear - convert so the clothes aren't washed out
            {
                Color L(Color c) => new Color(Mathf.Pow(c.r, 2.2f), Mathf.Pow(c.g, 2.2f), Mathf.Pow(c.b, 2.2f), 1f);
                p.top = L(p.top); p.top2 = L(p.top2); p.pants = L(p.pants); glove = L(new Color(0.16f, 0.15f, 0.14f));
            }

            // what they wear (3.48): operators the wardrobe's picks (a combat uniform unless they chose otherwise), raiders a
            // jacket and cargo trousers in their palette; bosses and traders keep their own outfits
            var g = new Garb { top = TopNone, bottom = BottomNone };
            if (p.dressed) { g.top = p.look.shirt; g.bottom = p.look.pants; g.top1 = Lin3(Cosmetics.Col(p.look.shirtCol)); g.top2 = Sh(g.top1, 0.72f); g.bot = Lin3(Cosmetics.Col(p.look.pantsCol)); g.patch = true; g.accent = p.accent; }
            else if (kit) { g.top = 4; g.bottom = 1; g.top1 = p.top; g.top2 = p.top2; g.bot = p.pants; }
            bool cargo = g.bottom == 0 || g.bottom == 1;

            // ---- pelvis
            var mb = new MB(4);
            // a moulded pelvis: hips wider than the crotch, a little fuller behind
            mb.Sub(SKIN);
            Loft(mb, new Vector3(0, -0.03f, 0), 0.08f, PelvisW, PelvisDf, PelvisDb, 0.7f, 0.6f, 26, 12, body);
            Ball(mb, new Vector3(-0.1f, -0.07f, 0), 0.062f); Ball(mb, new Vector3(0.1f, -0.07f, 0), 0.062f);
            Marker(mb, new Vector3(0.147f, -0.03f, 0), Vector3.right, 0.028f); Marker(mb, new Vector3(-0.147f, -0.03f, 0), Vector3.left, 0.028f);
            if (soldier || p.apron)
            {
                // (3.50) a webbing belt that wraps round the waist (it was a flat-sided box) and its buckle
                mb.Sub(HARD);
                Loft(mb, new Vector3(0, 0.06f, 0), 0.023f, t => 0.153f, t => 0.104f, t => 0.104f, 0.72f, 0.08f, 28, 3, p.gear2);
                mb.Sub(HARD).Box(new Vector3(0, 0.06f, 0.1065f), new Vector3(0.056f, 0.036f, 0.012f), new Color(0.5f, 0.5f, 0.5f), 0.004f);
                mb.Sub(HARD).Box(new Vector3(0, 0.06f, 0.1125f), new Vector3(0.036f, 0.018f, 0.003f), new Color(0.2f, 0.2f, 0.2f), 0.001f);
            }
            if (soldier)
            {
                mb.Sub(HARD).Box(new Vector3(0.15f, 0.02f, -0.06f), new Vector3(0.06f, 0.09f, 0.08f), p.gear, 0.012f);
                mb.Sub(HARD).Box(new Vector3(-0.16f, 0.03f, 0.03f), new Vector3(0.05f, 0.07f, 0.06f), p.gear, 0.01f);
            }
            GarbPelvis(mb, g);
            mb.Build("Mesh", r.hips, mats);

            // ---- abdomen: ribbed rubber insert, like a real crash dummy
            mb = new MB(4);
            mb.Sub(SKIN); Loft(mb, new Vector3(0, 0.06f, 0), 0.165f, t => 0.112f + 0.008f * t, t => 0.087f, t => 0.089f, 0.8f, 0.5f, 24, 12, body);   // the waist (an oval now, so clothes fit over it)
            if (p.apron) mb.Sub(CLOTH).Box(new Vector3(0, 0.05f, 0.12f), new Vector3(0.3f, 0.4f, 0.02f), p.gear, 0.01f);
            if (p.coat) mb.Sub(CLOTH).Box(new Vector3(0, -0.04f, 0.0f), new Vector3(0.35f, 0.3f, 0.25f), p.top, 0.05f, 2);
            GarbWaist(mb, g);
            mb.Build("Mesh", r.spine, mats);

            // ---- chest shell + gear
            mb = new MB(4);
            // a moulded chest (3.47): a V from broad shoulders down to the ribs, the chest a little forward, a sternum groove
            mb.Sub(SKIN);
            Loft(mb, new Vector3(0, 0.08f, 0), 0.125f, ChestW, ChestDf, ChestDb, 0.72f, 0.62f, 32, 16, body, 0.006f);
            if (!Sleeved(g.top))   // bare shoulders and their joints (under sleeves the shirt's own shoulders take their place)
            {
                foreach (float sx in new[] { -1f, 1f }) mb.Sub(SKIN).Sphere(new Vector3(sx * 0.155f, 0.15f, 0.004f), new Vector3(0.068f, 0.06f, 0.088f), 14, body);   // rounded shoulders
                Ball(mb, new Vector3(-0.19f, 0.1f, 0), 0.058f); Ball(mb, new Vector3(0.19f, 0.1f, 0), 0.058f);
            }
            if (!local) Ball(mb, new Vector3(0, 0.205f, 0), 0.045f);   // the neck joint (your own would sit in the bottom of your view, inside your collar)
            if (!p.plate && !p.coat && !p.suit && !p.apron) { Marker(mb, new Vector3(0.09f, 0.13f, 0.106f), Vector3.forward, 0.026f); }
            if (p.suit)
            {
                mb.Sub(CLOTH).Box(new Vector3(0, 0.06f, 0.0f), new Vector3(0.37f, 0.27f, 0.23f), p.top, 0.07f, 2);
                mb.Sub(CLOTH).Box(new Vector3(0, 0.07f, 0.116f), new Vector3(0.08f, 0.2f, 0.01f), Color.white * 0.9f, 0.01f);
                mb.Sub(CLOTH).Box(new Vector3(0, 0.06f, 0.123f), new Vector3(0.025f, 0.17f, 0.01f), p.top2, 0.005f);
            }
            if (p.coat) mb.Sub(CLOTH).Box(new Vector3(0, 0.06f, 0.0f), new Vector3(0.37f, 0.27f, 0.23f), p.top, 0.07f, 2);
            // each boss's signature details, so you can tell them apart at a glance
            switch (o)
            {
                case Outfit.BossKessler:   // lab coat: red cross on the breast pocket, a pen, a name badge
                    mb.Sub(CLOTH).Box(new Vector3(-0.08f, 0.11f, 0.118f), new Vector3(0.06f, 0.018f, 0.004f), new Color(0.8f, 0.08f, 0.08f), 0f);
                    mb.Sub(CLOTH).Box(new Vector3(-0.08f, 0.11f, 0.118f), new Vector3(0.018f, 0.06f, 0.004f), new Color(0.8f, 0.08f, 0.08f), 0f);
                    mb.Sub(HARD).Box(new Vector3(0.08f, 0.13f, 0.12f), new Vector3(0.008f, 0.06f, 0.008f), new Color(0.1f, 0.2f, 0.6f), 0.002f);
                    mb.Sub(HARD).Box(new Vector3(0.08f, 0.05f, 0.119f), new Vector3(0.07f, 0.03f, 0.004f), new Color(0.95f, 0.95f, 0.95f), 0.002f);
                    break;
                case Outfit.BossHarbor:    // foul-weather coat: yellow reflective bands round the chest
                    foreach (float y in new[] { 0.0f, 0.12f }) mb.Sub(CLOTH).Box(new Vector3(0, y, 0), new Vector3(0.375f, 0.03f, 0.235f), new Color(0.95f, 0.85f, 0.1f), 0.012f);
                    break;
                case Outfit.BossWarden:    // woodland camo blotches and a ghillie collar of leafy strips
                    for (int i = 0; i < 9; i++) { float a1 = i * 0.7f; mb.Sub(CLOTH).Sphere(new Vector3(Mathf.Sin(a1 * 3f) * 0.14f, 0.0f + (i % 3) * 0.07f, 0.11f), new Vector3(0.04f, 0.025f, 0.008f), 6, i % 2 == 0 ? new Color(0.12f, 0.18f, 0.08f) : new Color(0.38f, 0.3f, 0.16f)); }
                    for (int i = 0; i < 12; i++) { float a2 = i / 12f * Mathf.PI * 2f; mb.Sub(CLOTH).Box(new Vector3(Mathf.Cos(a2) * 0.15f, 0.2f, Mathf.Sin(a2) * 0.11f), new Vector3(0.03f, 0.1f, 0.01f), i % 2 == 0 ? new Color(0.2f, 0.28f, 0.12f) : new Color(0.32f, 0.3f, 0.16f), 0f, 1, Quaternion.Euler(Mathf.Sin(a2) * 25f, -a2 * Mathf.Rad2Deg, Mathf.Cos(a2) * 25f)); }
                    break;
                case Outfit.BossBanker:    // gold watch chain across the waistcoat and a red pocket square
                    for (int i = 0; i < 6; i++) mb.Sub(HARD).Sphere(new Vector3(-0.1f + i * 0.04f, -0.02f - Mathf.Sin(i / 5f * Mathf.PI) * 0.03f, 0.122f), Vector3.one * 0.007f, 6, new Color(0.95f, 0.75f, 0.25f));
                    mb.Sub(CLOTH).Box(new Vector3(-0.1f, 0.14f, 0.121f), new Vector3(0.04f, 0.025f, 0.006f), new Color(0.75f, 0.08f, 0.08f), 0.002f, 1, Quaternion.Euler(0, 0, 15));
                    break;
                case Outfit.BossCourier:   // pinstripes and a red carnation in the lapel
                    for (int i = 0; i < 7; i++) mb.Sub(CLOTH).Box(new Vector3(-0.15f + i * 0.05f, 0.06f, 0.124f), new Vector3(0.003f, 0.26f, 0.002f), new Color(0.6f, 0.6f, 0.62f), 0f);
                    mb.Sub(CLOTH).Sphere(new Vector3(0.11f, 0.15f, 0.125f), Vector3.one * 0.018f, 8, new Color(0.8f, 0.05f, 0.08f));
                    break;
            }
            GarbChest(mb, g);
            if (p.apron) { mb.Sub(CLOTH).Box(new Vector3(0, 0.02f, 0.11f), new Vector3(0.26f, 0.2f, 0.02f), p.gear, 0.01f); mb.Sub(CLOTH).Box(new Vector3(-0.09f, 0.17f, 0.04f), new Vector3(0.025f, 0.02f, 0.16f), p.gear * 0.8f, 0.005f); mb.Sub(CLOTH).Box(new Vector3(0.09f, 0.17f, 0.04f), new Vector3(0.025f, 0.02f, 0.16f), p.gear * 0.8f, 0.005f); }
            if (p.plate)
            {
                mb.Sub(HARD).Box(new Vector3(0, 0.04f, 0.12f), new Vector3(0.3f, 0.32f, 0.06f), p.gear, 0.02f, 2);
                mb.Sub(HARD).Box(new Vector3(0, 0.04f, -0.12f), new Vector3(0.3f, 0.32f, 0.05f), p.gear, 0.02f, 2);
                mb.Sub(HARD).Box(new Vector3(-0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), p.gear2, 0.01f);
                mb.Sub(HARD).Box(new Vector3(0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), p.gear2, 0.01f);
                mb.Sub(HARD).Box(new Vector3(-0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), p.gear2, 0.01f);
                mb.Sub(HARD).Box(new Vector3(0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), p.gear2, 0.01f);
                for (int i = 0; i < 3; i++) mb.Sub(HARD).Box(new Vector3(-0.09f + i * 0.09f, -0.06f, 0.165f), new Vector3(0.075f, 0.12f, 0.04f), p.gear2, 0.012f);
                mb.Sub(HARD).Box(new Vector3(0, 0.12f, 0.16f), new Vector3(0.16f, 0.06f, 0.03f), p.gear2, 0.01f);
                if (rng.Chance(0.6f)) mb.Sub(HARD).Box(new Vector3(0.11f, 0.15f, 0.17f), new Vector3(0.04f, 0.04f, 0.02f), p.accent, 0.005f);
            }
            else if (o == Outfit.Raider)
            {
                mb.Sub(HARD).Box(new Vector3(0, -0.04f, 0.12f), new Vector3(0.28f, 0.14f, 0.05f), p.gear, 0.015f);
                for (int i = 0; i < 4; i++) mb.Sub(HARD).Box(new Vector3(-0.1f + i * 0.067f, -0.02f, 0.15f), new Vector3(0.055f, 0.12f, 0.03f), p.gear2, 0.008f);
                mb.Sub(HARD).Box(new Vector3(-0.1f, 0.15f, 0.02f), new Vector3(0.04f, 0.02f, 0.25f), p.gear2, 0.005f);
                mb.Sub(HARD).Box(new Vector3(0.1f, 0.15f, 0.02f), new Vector3(0.04f, 0.02f, 0.25f), p.gear2, 0.005f);
            }
            mb.Build("Mesh", r.chest, mats);

            // ---- neck: metal column
            mb = new MB(4);
            mb.Sub(SKIN).Cyl(new Vector3(0, -0.01f, 0), new Vector3(0, 0.1f, 0), 0.042f, 0.038f, 14, bodyDark, false);
            if (p.suit) mb.Sub(CLOTH).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.03f, 0), 0.06f, 0.055f, 12, Color.white * 0.9f, false);
            var neckGo = mb.Build("Mesh", r.neck, mats);

            // ---- head: smooth egg with crash-test markers, gear on top
            mb = new MB(4);
            mb.Sub(SKIN); RagdollHead(mb, new Vector3(0, 0.085f, 0.008f), skinC);   // moulded and faceless
            if (!p.helmetOn && !p.gasMask) { Marker(mb, new Vector3(0.088f, 0.09f, 0.005f), Vector3.right, 0.026f); Marker(mb, new Vector3(-0.088f, 0.09f, 0.005f), Vector3.left, 0.026f); }
            if (p.helmetOn)
            {
                mb.Sub(HARD).Sphere(new Vector3(0, 0.105f, -0.002f), new Vector3(0.122f, 0.118f, 0.132f), 18, p.helmet, 0f, 0.55f);
                mb.Sub(HARD).Box(new Vector3(0, 0.16f, 0.115f), new Vector3(0.05f, 0.035f, 0.02f), new Color(0.1f, 0.1f, 0.1f), 0.005f);
                mb.Sub(HARD).Box(new Vector3(-0.112f, 0.07f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
                mb.Sub(HARD).Box(new Vector3(0.112f, 0.07f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
                mb.Sub(HARD).Box(new Vector3(-0.118f, 0.115f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), new Color(0.1f, 0.1f, 0.1f), 0.003f);
                mb.Sub(HARD).Box(new Vector3(0.118f, 0.115f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), new Color(0.1f, 0.1f, 0.1f), 0.003f);
                Marker(mb, new Vector3(0, 0.215f, 0.0f), Vector3.up, 0.024f);
            }
            if (p.beanie) mb.Sub(CLOTH).Sphere(new Vector3(0, 0.11f, 0.0f), new Vector3(0.096f, 0.1f, 0.107f), 14, rng.Pick(new[] { new Color(0.15f, 0.15f, 0.15f), new Color(0.35f, 0.1f, 0.08f), new Color(0.25f, 0.27f, 0.2f) }), 0f, 0.55f);
            if (p.cap)
            {
                mb.Sub(CLOTH).Sphere(new Vector3(0, 0.12f, 0.0f), new Vector3(0.095f, 0.085f, 0.105f), 14, p.gear, 0f, 0.5f);
                mb.Sub(CLOTH).Box(new Vector3(0, 0.125f, 0.12f), new Vector3(0.14f, 0.01f, 0.08f), p.gear, 0.004f, 1, Quaternion.Euler(-8, 0, 0));
            }
            if (p.dressed)
            {
                Color hc = Lin3(Cosmetics.Col(p.look.hatCol));
                if (p.look.hat == 1)
                {
                    mb.Sub(CLOTH).Sphere(new Vector3(0, 0.12f, 0.0f), new Vector3(0.095f, 0.085f, 0.105f), 14, hc, 0f, 0.5f);
                    mb.Sub(CLOTH).Box(new Vector3(0, 0.125f, 0.12f), new Vector3(0.14f, 0.01f, 0.08f), hc * 0.9f, 0.004f, 1, Quaternion.Euler(-8, 0, 0));
                }
                else if (p.look.hat == 2) mb.Sub(CLOTH).Sphere(new Vector3(0, 0.11f, 0.0f), new Vector3(0.096f, 0.1f, 0.107f), 14, hc, 0f, 0.55f);
                else if (p.look.hat == 3)
                {
                    mb.Sub(CLOTH).Sphere(new Vector3(0, 0.125f, 0.0f), new Vector3(0.092f, 0.075f, 0.104f), 14, hc, 0f, 0.5f);
                    mb.Sub(CLOTH).Box(new Vector3(0, 0.13f, -0.1f), new Vector3(0.04f, 0.03f, 0.03f), hc * 0.85f, 0.008f);   // the knot
                }
                if (p.look.face == 1)
                {
                    var dark = new Color(0.03f, 0.03f, 0.04f);
                    mb.Sub(HARD).Box(new Vector3(-0.033f, 0.098f, 0.1f), new Vector3(0.042f, 0.022f, 0.008f), dark, 0.004f);
                    mb.Sub(HARD).Box(new Vector3(0.033f, 0.098f, 0.1f), new Vector3(0.042f, 0.022f, 0.008f), dark, 0.004f);
                    mb.Sub(HARD).Box(new Vector3(0, 0.102f, 0.102f), new Vector3(0.026f, 0.006f, 0.006f), dark);
                    mb.Sub(HARD).Box(new Vector3(-0.085f, 0.1f, 0.05f), new Vector3(0.005f, 0.006f, 0.1f), dark);
                    mb.Sub(HARD).Box(new Vector3(0.085f, 0.1f, 0.05f), new Vector3(0.005f, 0.006f, 0.1f), dark);
                }
                else if (p.look.face == 2) mb.Sub(CLOTH).Sphere(new Vector3(0, 0.06f, 0.008f), new Vector3(0.094f, 0.08f, 0.106f), 18, Sh(Lin3(Cosmetics.Col(p.look.shirtCol)), 0.6f), 0.4f, 1f);   // neck gaiter up over the chin
            }
            if (p.mask && o != Outfit.Operator) mb.Sub(CLOTH).Sphere(new Vector3(0, 0.085f, 0.008f), new Vector3(0.094f, 0.118f, 0.106f), 18, p.top2 * 0.55f, 0.56f, 0.86f); // neck gaiter hugging the lower head
            if (p.gasMask)
            {
                mb.Sub(HARD).Sphere(new Vector3(0, 0.06f, 0.06f), new Vector3(0.095f, 0.1f, 0.08f), 14, new Color(0.15f, 0.15f, 0.14f));
                mb.Sub(HARD).Cyl(new Vector3(0, 0.01f, 0.12f), new Vector3(0, -0.02f, 0.17f), 0.035f, 0.035f, 12, new Color(0.25f, 0.27f, 0.2f));
                mb.Sub(HARD).Sphere(new Vector3(-0.04f, 0.09f, 0.115f), new Vector3(0.028f, 0.028f, 0.01f), 10, new Color(0.2f, 0.25f, 0.3f));
                mb.Sub(HARD).Sphere(new Vector3(0.04f, 0.09f, 0.115f), new Vector3(0.028f, 0.028f, 0.01f), 10, new Color(0.2f, 0.25f, 0.3f));
            }
            if (Festive.On && o >= Outfit.Gunsmith && o <= Outfit.Foreman)
            {
                // Christmas: the traders wear Santa hats
                mb.Sub(CLOTH).Cyl(new Vector3(0, 0.15f, 0.0f), new Vector3(0, 0.185f, 0.0f), 0.104f, 0.102f, 18, new Color(0.95f, 0.95f, 0.93f));
                mb.Sub(CLOTH).Cyl(new Vector3(0, 0.185f, 0.0f), new Vector3(0, 0.33f, -0.07f), 0.095f, 0.012f, 16, new Color(0.75f, 0.06f, 0.06f));
                mb.Sub(CLOTH).Sphere(new Vector3(0, 0.33f, -0.075f), Vector3.one * 0.025f, 10, new Color(0.95f, 0.95f, 0.93f));
            }
            var headGo = mb.Build("Mesh", r.head, mats);
            if (local)
            {
                // your own head would block the camera: render shadows only
                foreach (var rr in new[] { headGo.GetComponent<Renderer>(), neckGo.GetComponent<Renderer>() })
                {
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    r.headRenderers.Add(rr);
                }
            }

            // ---- arms: plastic segments between metal ball joints
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                var ua = s == 0 ? r.uaL : r.uaR; var fa = s == 0 ? r.faL : r.faR; var hand = s == 0 ? r.handL : r.handR;
                mb = new MB(4);
                mb.Sub(SKIN).Capsule(new Vector3(0, 0, 0.055f), new Vector3(0, 0, r.upperArm - 0.045f), 0.05f, 0.043f, 16, body);
                if (o == Outfit.Operator && !Sleeved(g.top)) mb.Sub(HARD).Box(new Vector3(side * 0.047f, 0, 0.11f), new Vector3(0.008f, 0.055f, 0.065f), p.accent, 0.003f); // shoulder patch (on the sleeve pocket when there is a sleeve)
                if (o == Outfit.Raider || o == Outfit.RaiderHeavy) { if (s == 0) mb.Sub(CLOTH).Cyl(new Vector3(0, 0, 0.11f), new Vector3(0, 0, 0.16f), 0.0625f, 0.0615f, 14, p.accent, false); } // red armband, over the sleeve (it used to be hidden inside it)
                if (p.coat || p.suit) mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.02f), new Vector3(0, 0, r.upperArm - 0.03f), 0.058f, 0.05f, 12, p.top);
                GarbUpperArm(mb, g, side, r.upperArm);
                mb.Build("Mesh", ua, mats);
                mb = new MB(4);
                Ball(mb, Vector3.zero, 0.044f); // elbow
                mb.Sub(SKIN).Capsule(new Vector3(0, 0, 0.05f), new Vector3(0, 0, r.foreArm - 0.04f), 0.041f, 0.034f, 16, body);
                mb.Sub(SKIN).Sphere(new Vector3(0, 0, 0.085f), new Vector3(0.045f, 0.043f, 0.075f), 14, body);   // the forearm's fuller top
                if (!soldier) Ball(mb, new Vector3(0, 0, r.foreArm), 0.03f); // wrist (gloved hands cover it with the glove's cuff)
                if (p.coat || p.suit) mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.02f), new Vector3(0, 0, r.foreArm - 0.05f), 0.048f, 0.042f, 12, p.coat ? p.top : p.top);
                GarbForeArm(mb, g, r.foreArm);
                mb.Build("Mesh", fa, mats);
                if (!local)
                {
                    // (3.50) a hand with fingers - fingerless gloves on soldiers, full gloves on raiders
                    mb = new MB(4);
                    RagdollHand(mb, side, body, glove, kit ? 2 : (soldier ? 1 : 0));
                    if (soldier && !kit)
                    {
                        mb.Sub(HARD).Box(new Vector3(0, 0.0145f, 0.062f), new Vector3(0.06f, 0.007f, 0.02f), Sh(glove, 1.6f), 0.0025f);   // knuckle guard
                        mb.Sub(CLOTH).Cyl(new Vector3(0, -0.003f, -0.03f), new Vector3(0, -0.003f, 0.012f), 0.036f, 0.035f, 12, Sh(glove, 1.25f), false);   // the glove's cuff, over the wrist joint
                    }
                    if (kit) mb.Sub(CLOTH).Cyl(new Vector3(0, -0.004f, -0.03f), new Vector3(0, -0.004f, 0.02f), 0.04f, 0.038f, 12, glove * 1.3f, false);   // glove cuff, over the wrist joint
                    mb.Build("Mesh", hand, mats);
                }
            }
            // ---- legs
            for (int s = 0; s < 2; s++)
            {
                var th = s == 0 ? r.thL : r.thR; var sh = s == 0 ? r.shL : r.shR; var ft = s == 0 ? r.footL : r.footR;
                float side = s == 0 ? -1 : 1;
                mb = new MB(4);
                mb.Sub(SKIN).Capsule(new Vector3(0, 0, 0.06f), new Vector3(0, 0, r.thigh - 0.05f), 0.078f, 0.06f, 16, body);
                if (soldier && !cargo) mb.Sub(HARD).Box(new Vector3(side * (g.bottom == BottomNone ? 0.075f : 0.088f), 0, 0.22f), new Vector3(0.03f, 0.1f, 0.12f), p.gear2, 0.01f); // thigh pouch (cargo trousers have their pocket there instead)
                if (p.coat) mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.thigh * 0.6f), 0.09f, 0.085f, 12, p.top);
                GarbThigh(mb, g, side, r.thigh);
                mb.Build("Mesh", th, mats);
                mb = new MB(4);
                Ball(mb, Vector3.zero, 0.058f); // knee
                mb.Sub(SKIN).Capsule(new Vector3(0, 0, 0.06f), new Vector3(0, 0, r.shin - 0.05f), 0.055f, 0.044f, 16, body);
                mb.Sub(SKIN).Sphere(new Vector3(0, -0.012f, 0.15f), new Vector3(0.054f, 0.058f, 0.11f), 14, body);   // the calf (the shin's +y is the front)
                Ball(mb, new Vector3(0, 0, r.shin), 0.04f); // ankle
                if (o == Outfit.Operator || o == Outfit.RaiderHeavy) mb.Sub(HARD).Box(new Vector3(0, 0.06f, 0.03f), new Vector3(0.09f, 0.04f, 0.09f), p.gear2, 0.015f, 2); // knee pad - on the front of the knee (it used to sit behind it)
                GarbShin(mb, g, r.shin);
                if (soldier) mb.Sub(HARD).Cyl(new Vector3(0, 0, r.shin - 0.12f), new Vector3(0, 0, r.shin - 0.02f), 0.056f, 0.052f, 12, p.boots, false); // boot shaft
                mb.Build("Mesh", sh, mats);
                mb = new MB(4);
                Color footCol = soldier ? p.boots : body;
                mb.Sub(soldier ? HARD : SKIN).Box(new Vector3(0, -0.055f, 0.035f), new Vector3(0.095f, 0.2f, 0.07f), footCol, 0.032f, 3);
                mb.Sub(JOINT).Box(new Vector3(0, -0.055f, 0.071f), new Vector3(0.1f, 0.205f, 0.01f), JointCol, 0.004f);
                if (soldier)
                {
                    mb.Sub(HARD).Sphere(new Vector3(0, -0.14f, 0.045f), new Vector3(0.05f, 0.03f, 0.03f), 10, footCol * 0.8f);                           // toe cap
                    for (int k = 0; k < 4; k++) mb.Sub(CLOTH).Box(new Vector3(0, -0.02f - k * 0.025f, 0.0f), new Vector3(0.05f, 0.004f, 0.006f), new Color(0.1f, 0.1f, 0.1f), 0.001f);   // laces
                }
                mb.Build("Mesh", ft, mats);
            }
            _human = false;   // the snowman / elf / Santa builders keep their own look
        }

        /// <summary>Christmas: raiders and bosses are snowmen (traders and players stay as they are).</summary>
        public static bool Snowman(Outfit o) => Festive.On && (o == Outfit.Raider || o == Outfit.RaiderHeavy);   // bosses keep their own outfits

        /// <summary>A raid on the North Pole is running (its raiders are evil elves).</summary>
        public static bool PoleMap => RagoGame.I != null && RagoGame.I.Map != null && RagoGame.I.Map.Id == "northpole";

        static Material SnowmanMat => Mats.Lit("snowman", Color.white, 0f, 0.3f, Mats.Noise, null, 0.04f, 0f);

        /// <summary>An evil elf on the raider skeleton: green tunic with a belt and zig-zag collar, red-and-white striped
        /// stockings, curly shoes, pointy ears, angry red eyes and a floppy hat with a bell. Heavies wear red.</summary>
        static void BuildElf(HumanRig r, bool heavy, Rng rng)
        {
            var mats = new[] { ClothMat, HardMat, PlasticMat, JointMat };
            Color tunic = heavy ? new Color(0.6f, 0.06f, 0.06f) : rng.Chance(0.5f) ? new Color(0.1f, 0.38f, 0.16f) : new Color(0.08f, 0.3f, 0.12f);
            Color trim = heavy ? new Color(0.1f, 0.38f, 0.16f) : new Color(0.7f, 0.08f, 0.08f);
            Color skin = new Color(0.9f, 0.75f, 0.65f), white = new Color(0.95f, 0.95f, 0.93f), red = new Color(0.75f, 0.06f, 0.06f), black = new Color(0.06f, 0.05f, 0.05f), gold = new Color(0.9f, 0.7f, 0.2f);
            // hips: tunic skirt and belt
            var mb = new MB(4);
            mb.Sub(CLOTH).Cyl(new Vector3(0, -0.13f, 0), new Vector3(0, 0.04f, 0), 0.2f, 0.16f, 16, tunic);
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; mb.Sub(CLOTH).Cyl(new Vector3(Mathf.Cos(a) * 0.19f, -0.13f, Mathf.Sin(a) * 0.19f), new Vector3(Mathf.Cos(a) * 0.21f, -0.19f, Mathf.Sin(a) * 0.21f), 0.05f, 0.005f, 6, trim); }   // zig-zag hem
            mb.Sub(HARD).Cyl(new Vector3(0, 0.03f, 0), new Vector3(0, 0.08f, 0), 0.165f, 0.165f, 16, black);
            mb.Sub(HARD).Box(new Vector3(0, 0.055f, 0.166f), new Vector3(0.07f, 0.055f, 0.01f), gold, 0.005f);
            mb.Build("Mesh", r.hips, mats);
            mb = new MB(4);
            mb.Sub(CLOTH).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.22f, 0), 0.16f, 0.15f, 16, tunic);
            for (int i = 0; i < 3; i++) mb.Sub(HARD).Sphere(new Vector3(0, 0.05f + i * 0.06f, 0.152f), Vector3.one * 0.014f, 8, gold);
            mb.Build("Mesh", r.spine, mats);
            mb = new MB(4);
            mb.Sub(CLOTH).Box(new Vector3(0, 0.08f, 0), new Vector3(0.34f, 0.24f, 0.22f), tunic, 0.08f, 2);
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2f; mb.Sub(CLOTH).Cyl(new Vector3(Mathf.Cos(a) * 0.1f, 0.19f, Mathf.Sin(a) * 0.09f), new Vector3(Mathf.Cos(a) * 0.15f, 0.15f, Mathf.Sin(a) * 0.13f), 0.035f, 0.004f, 5, trim); }   // pointed collar
            mb.Build("Mesh", r.chest, mats);
            mb = new MB(4);
            mb.Sub(PLASTIC).Cyl(new Vector3(0, -0.01f, 0), new Vector3(0, 0.1f, 0), 0.045f, 0.042f, 10, skin);
            mb.Build("Mesh", r.neck, mats);
            // head: pointy ears, angry brows, red eyes, a long floppy hat with a bell
            mb = new MB(4);
            mb.Sub(PLASTIC).Sphere(new Vector3(0, 0.085f, 0.008f), new Vector3(0.088f, 0.105f, 0.098f), 16, skin);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sub(PLASTIC).Cyl(new Vector3(sx * 0.08f, 0.09f, 0), new Vector3(sx * 0.17f, 0.15f, -0.03f), 0.03f, 0.004f, 8, skin);
                mb.Sub(HARD).Sphere(new Vector3(sx * 0.035f, 0.1f, 0.095f), new Vector3(0.016f, 0.01f, 0.006f), 8, new Color(0.95f, 0.1f, 0.05f));
                mb.Sub(HARD).Box(new Vector3(sx * 0.037f, 0.125f, 0.094f), new Vector3(0.04f, 0.008f, 0.006f), black, 0.002f, 1, Quaternion.Euler(0, 0, sx * -25f));   // angry brows
            }
            mb.Sub(PLASTIC).Sphere(new Vector3(0, 0.075f, 0.105f), Vector3.one * 0.016f, 8, new Color(0.95f, 0.6f, 0.55f));   // nose
            mb.Sub(HARD).Box(new Vector3(0, 0.035f, 0.097f), new Vector3(0.04f, 0.006f, 0.004f), black, 0.002f, 1, Quaternion.Euler(0, 0, 0));   // a scowl
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0.15f, 0), new Vector3(0, 0.18f, 0), 0.098f, 0.096f, 16, white);
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0.18f, 0), new Vector3(0, 0.33f, -0.06f), 0.092f, 0.04f, 14, tunic);
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0.33f, -0.06f), new Vector3(0, 0.3f, -0.2f), 0.04f, 0.008f, 10, tunic);
            mb.Sub(HARD).Sphere(new Vector3(0, 0.29f, -0.21f), Vector3.one * 0.022f, 10, gold);   // the bell
            mb.Build("Mesh", r.head, mats);
            // arms: sleeves, white cuffs, skin hands
            for (int s = 0; s < 2; s++)
            {
                var ua = s == 0 ? r.uaL : r.uaR; var fa = s == 0 ? r.faL : r.faR; var hand = s == 0 ? r.handL : r.handR;
                mb = new MB(4); mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.03f), new Vector3(0, 0, r.upperArm - 0.02f), 0.055f, 0.048f, 12, tunic); mb.Build("Mesh", ua, mats);
                mb = new MB(4); mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.foreArm - 0.04f), 0.046f, 0.04f, 12, tunic);
                mb.Sub(CLOTH).Cyl(new Vector3(0, 0, r.foreArm - 0.06f), new Vector3(0, 0, r.foreArm - 0.01f), 0.05f, 0.05f, 12, white); mb.Build("Mesh", fa, mats);
                mb = new MB(4); mb.Sub(PLASTIC).Box(new Vector3(0, -0.004f, 0.05f), new Vector3(0.07f, 0.03f, 0.09f), skin, 0.014f, 2); mb.Build("Mesh", hand, mats);
            }
            // legs: striped stockings, curly shoes
            for (int s = 0; s < 2; s++)
            {
                var th = s == 0 ? r.thL : r.thR; var sh = s == 0 ? r.shL : r.shR; var ft = s == 0 ? r.footL : r.footR;
                mb = new MB(4);
                for (int k = 0; k < 6; k++) mb.Sub(CLOTH).Cyl(new Vector3(0, 0, 0.02f + k * r.thigh / 6f), new Vector3(0, 0, 0.02f + (k + 1) * r.thigh / 6f), 0.07f, 0.065f, 12, k % 2 == 0 ? red : white, k == 0);
                mb.Build("Mesh", th, mats);
                mb = new MB(4);
                for (int k = 0; k < 6; k++) mb.Sub(CLOTH).Cyl(new Vector3(0, 0, k * r.shin / 6f), new Vector3(0, 0, (k + 1) * r.shin / 6f), 0.055f, 0.05f, 12, k % 2 == 0 ? white : red, false);
                mb.Build("Mesh", sh, mats);
                mb = new MB(4);
                mb.Sub(CLOTH).Box(new Vector3(0, -0.06f, 0.035f), new Vector3(0.09f, 0.2f, 0.07f), tunic, 0.03f, 2);
                mb.Sub(CLOTH).Cyl(new Vector3(0, -0.16f, 0.04f), new Vector3(0, -0.2f, 0.1f), 0.03f, 0.004f, 8, tunic);   // curled toe
                mb.Sub(HARD).Sphere(new Vector3(0, -0.2f, 0.1f), Vector3.one * 0.012f, 6, gold);
                mb.Build("Mesh", ft, mats);
            }
        }

        /// <summary>Evil Santa: a big red suit with white fur trim, black belt and boots, a full white beard, glowing red
        /// eyes and his hat.</summary>
        static void BuildSanta(HumanRig r)
        {
            var mats = new[] { ClothMat, HardMat, PlasticMat, JointMat };
            Color red = new Color(0.68f, 0.04f, 0.04f), fur = new Color(0.95f, 0.95f, 0.93f), black = new Color(0.05f, 0.05f, 0.05f), gold = new Color(0.9f, 0.72f, 0.2f), skin = new Color(0.88f, 0.68f, 0.58f);
            var mb = new MB(4);
            mb.Sub(CLOTH).Sphere(new Vector3(0, 0.0f, 0.02f), new Vector3(0.24f, 0.2f, 0.22f), 16, red);
            mb.Sub(CLOTH).Cyl(new Vector3(0, -0.2f, 0.01f), new Vector3(0, -0.14f, 0.01f), 0.22f, 0.23f, 16, fur);
            mb.Build("Mesh", r.hips, mats);
            mb = new MB(4);
            mb.Sub(CLOTH).Sphere(new Vector3(0, 0.1f, 0.04f), new Vector3(0.25f, 0.2f, 0.24f), 16, red);   // the belly
            mb.Sub(HARD).Cyl(new Vector3(0, 0.0f, 0.03f), new Vector3(0, 0.06f, 0.03f), 0.245f, 0.245f, 18, black);
            mb.Sub(HARD).Box(new Vector3(0, 0.03f, 0.27f), new Vector3(0.1f, 0.075f, 0.015f), gold, 0.006f);
            mb.Sub(CLOTH).Box(new Vector3(0, 0.17f, 0.25f), new Vector3(0.06f, 0.18f, 0.03f), fur, 0.01f);
            mb.Build("Mesh", r.spine, mats);
            mb = new MB(4);
            mb.Sub(CLOTH).Box(new Vector3(0, 0.08f, 0.01f), new Vector3(0.42f, 0.27f, 0.28f), red, 0.1f, 2);
            mb.Sub(CLOTH).Box(new Vector3(0, 0.0f, 0.15f), new Vector3(0.06f, 0.2f, 0.02f), fur, 0.01f);
            mb.Build("Mesh", r.chest, mats);
            mb = new MB(4);
            mb.Sub(CLOTH).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.09f, 0), 0.08f, 0.07f, 12, fur);
            mb.Build("Mesh", r.neck, mats);
            mb = new MB(4);
            mb.Sub(PLASTIC).Sphere(new Vector3(0, 0.085f, 0.008f), new Vector3(0.092f, 0.11f, 0.1f), 16, skin);
            mb.Sub(CLOTH).Sphere(new Vector3(0, 0.0f, 0.06f), new Vector3(0.1f, 0.12f, 0.08f), 14, fur);   // beard
            mb.Sub(CLOTH).Sphere(new Vector3(0, 0.055f, 0.1f), new Vector3(0.06f, 0.018f, 0.02f), 10, fur);   // moustache
            mb.Sub(PLASTIC).Sphere(new Vector3(0, 0.08f, 0.108f), Vector3.one * 0.018f, 8, new Color(0.9f, 0.45f, 0.45f));
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sub(HARD).Sphere(new Vector3(sx * 0.035f, 0.108f, 0.095f), new Vector3(0.015f, 0.009f, 0.006f), 8, new Color(1f, 0.05f, 0.02f));
                mb.Sub(CLOTH).Box(new Vector3(sx * 0.038f, 0.132f, 0.096f), new Vector3(0.045f, 0.012f, 0.01f), fur, 0.004f, 1, Quaternion.Euler(0, 0, sx * -28f));
            }
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0.15f, 0), new Vector3(0, 0.19f, 0), 0.106f, 0.104f, 18, fur);
            mb.Sub(CLOTH).Cyl(new Vector3(0, 0.19f, 0), new Vector3(0.0f, 0.36f, -0.1f), 0.098f, 0.014f, 16, red);
            mb.Sub(CLOTH).Sphere(new Vector3(0, 0.36f, -0.105f), Vector3.one * 0.03f, 10, fur);
            mb.Build("Mesh", r.head, mats);
            for (int s = 0; s < 2; s++)
            {
                var ua = s == 0 ? r.uaL : r.uaR; var fa = s == 0 ? r.faL : r.faR; var hand = s == 0 ? r.handL : r.handR;
                mb = new MB(4); mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.03f), new Vector3(0, 0, r.upperArm - 0.02f), 0.07f, 0.06f, 12, red); mb.Build("Mesh", ua, mats);
                mb = new MB(4); mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.foreArm - 0.05f), 0.06f, 0.052f, 12, red);
                mb.Sub(CLOTH).Cyl(new Vector3(0, 0, r.foreArm - 0.07f), new Vector3(0, 0, r.foreArm - 0.01f), 0.065f, 0.065f, 12, fur); mb.Build("Mesh", fa, mats);
                mb = new MB(4); mb.Sub(CLOTH).Box(new Vector3(0, -0.004f, 0.05f), new Vector3(0.08f, 0.035f, 0.1f), black, 0.015f, 2); mb.Build("Mesh", hand, mats);
            }
            for (int s = 0; s < 2; s++)
            {
                var th = s == 0 ? r.thL : r.thR; var sh = s == 0 ? r.shL : r.shR; var ft = s == 0 ? r.footL : r.footR;
                mb = new MB(4); mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.04f), new Vector3(0, 0, r.thigh - 0.03f), 0.095f, 0.08f, 12, red); mb.Build("Mesh", th, mats);
                mb = new MB(4); mb.Sub(CLOTH).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.shin * 0.45f), 0.075f, 0.07f, 12, red);
                mb.Sub(HARD).Cyl(new Vector3(0, 0, r.shin * 0.4f), new Vector3(0, 0, r.shin), 0.07f, 0.065f, 12, black);
                mb.Sub(CLOTH).Cyl(new Vector3(0, 0, r.shin * 0.38f), new Vector3(0, 0, r.shin * 0.46f), 0.078f, 0.078f, 12, fur); mb.Build("Mesh", sh, mats);
                mb = new MB(4); mb.Sub(HARD).Box(new Vector3(0, -0.06f, 0.035f), new Vector3(0.1f, 0.22f, 0.08f), black, 0.03f, 2); mb.Build("Mesh", ft, mats);
            }
        }

        /// <summary>A snowman on the same skeleton (so it walks, aims, gets hit and falls over just like a raider): a big
        /// snowball for the hips, a middle one with coal buttons, a red scarf, a head with coal eyes and smile, a carrot
        /// nose and a top hat, and twig arms.</summary>
        static void BuildSnowman(HumanRig r, Outfit o, Rng rng)
        {
            var mats = new[] { SnowmanMat, HardMat, PlasticMat, JointMat };
            const int SNOW = 0;
            Color snow = new Color(0.93f, 0.95f, 0.98f), snowSh = new Color(0.82f, 0.86f, 0.92f), coal = new Color(0.04f, 0.04f, 0.045f);
            Color twig = new Color(0.32f, 0.2f, 0.1f), carrot = new Color(0.95f, 0.45f, 0.08f);
            bool boss = o >= Outfit.BossKessler, heavy = o == Outfit.RaiderHeavy;
            Color scarf = boss ? new Color(0.1f, 0.45f, 0.2f) : rng.Chance(0.5f) ? new Color(0.7f, 0.08f, 0.08f) : new Color(0.15f, 0.25f, 0.6f);
            float big = heavy || boss ? 1.12f : 1f;

            // ---- hips: the big bottom snowball
            var mb = new MB(4);
            mb.Sub(SNOW).Sphere(new Vector3(0, -0.13f, 0), new Vector3(0.3f, 0.28f, 0.28f) * big, 18, snow);
            mb.Build("Mesh", r.hips, mats);
            // ---- spine: the middle snowball, coal buttons down the front
            mb = new MB(4);
            mb.Sub(SNOW).Sphere(new Vector3(0, 0.12f, 0), new Vector3(0.2f, 0.19f, 0.19f) * big, 16, snow);
            for (int i = 0; i < 3; i++) mb.Sub(HARD).Sphere(new Vector3(0, 0.2f - i * 0.075f, 0.185f * big - i * 0.004f), Vector3.one * 0.022f, 8, coal);
            mb.Build("Mesh", r.spine, mats);
            // ---- chest: the scarf, its tail hanging down the front
            mb = new MB(4);
            mb.Sub(HARD).Cyl(new Vector3(0, 0.1f, 0), new Vector3(0, 0.17f, 0), 0.135f, 0.12f, 16, scarf);
            mb.Sub(HARD).Box(new Vector3(0.06f, 0.0f, 0.185f), new Vector3(0.06f, 0.15f, 0.02f), scarf * 0.9f, 0.01f, 1, Quaternion.Euler(-18, 0, 8));
            mb.Build("Mesh", r.chest, mats);
            // ---- neck: a bit of snow so the head sits on the body
            mb = new MB(4);
            mb.Sub(SNOW).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.08f, 0), 0.1f, 0.09f, 12, snowSh);
            mb.Build("Mesh", r.neck, mats);
            // ---- head: coal eyes and smile, carrot nose, top hat
            mb = new MB(4);
            mb.Sub(SNOW).Sphere(new Vector3(0, 0.07f, 0), new Vector3(0.14f, 0.135f, 0.135f), 16, snow);
            mb.Sub(HARD).Sphere(new Vector3(-0.048f, 0.11f, 0.12f), Vector3.one * 0.018f, 8, coal);
            mb.Sub(HARD).Sphere(new Vector3(0.048f, 0.11f, 0.12f), Vector3.one * 0.018f, 8, coal);
            for (int i = 0; i < 5; i++) { float a = (i - 2) * 0.32f; mb.Sub(HARD).Sphere(new Vector3(Mathf.Sin(a) * 0.07f, 0.02f + (1f - Mathf.Cos(a)) * 0.06f, 0.125f - Mathf.Abs(a) * 0.02f), Vector3.one * 0.012f, 6, coal); }
            mb.Sub(HARD).Cyl(new Vector3(0, 0.07f, 0.125f), new Vector3(0, 0.06f, 0.27f), 0.024f, 0.003f, 10, carrot);
            Color hat = boss ? new Color(0.12f, 0.1f, 0.08f) : coal;
            mb.Sub(HARD).Cyl(new Vector3(0, 0.175f, 0), new Vector3(0, 0.19f, 0), 0.16f, 0.16f, 18, hat);
            mb.Sub(HARD).Cyl(new Vector3(0, 0.19f, 0), new Vector3(0, boss ? 0.4f : 0.34f, 0), 0.095f, 0.1f, 18, hat);
            mb.Sub(HARD).Cyl(new Vector3(0, 0.2f, 0), new Vector3(0, 0.235f, 0), 0.102f, 0.102f, 18, boss ? new Color(0.85f, 0.65f, 0.15f) : scarf, false);
            mb.Build("Mesh", r.head, mats);
            // ---- arms: twigs, with little twig fingers
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                var ua = s == 0 ? r.uaL : r.uaR; var fa = s == 0 ? r.faL : r.faR; var hand = s == 0 ? r.handL : r.handR;
                mb = new MB(4);
                mb.Sub(HARD).Cyl(new Vector3(0, 0, 0.02f), new Vector3(0, 0, r.upperArm + 0.01f), 0.02f, 0.017f, 7, twig);
                mb.Build("Mesh", ua, mats);
                mb = new MB(4);
                mb.Sub(HARD).Cyl(Vector3.zero, new Vector3(0, 0, r.foreArm + 0.01f), 0.017f, 0.013f, 7, twig);
                mb.Sub(HARD).Cyl(new Vector3(0, 0, r.foreArm * 0.5f), new Vector3(side * 0.05f, 0.03f, r.foreArm * 0.7f), 0.009f, 0.004f, 5, twig);
                mb.Build("Mesh", fa, mats);
                mb = new MB(4);
                for (int k = 0; k < 3; k++) mb.Sub(HARD).Cyl(Vector3.zero, new Vector3((k - 1) * 0.035f, 0, 0.08f), 0.01f, 0.004f, 5, twig);
                mb.Build("Mesh", hand, mats);
            }
            // ---- legs: packed-snow stumps under the big snowball
            for (int s = 0; s < 2; s++)
            {
                var th = s == 0 ? r.thL : r.thR; var sh = s == 0 ? r.shL : r.shR; var ft = s == 0 ? r.footL : r.footR;
                mb = new MB(4);
                mb.Sub(SNOW).Capsule(new Vector3(0, 0, 0.1f), new Vector3(0, 0, r.thigh - 0.04f), 0.105f, 0.095f, 12, snowSh);
                mb.Build("Mesh", th, mats);
                mb = new MB(4);
                mb.Sub(SNOW).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.shin - 0.05f), 0.09f, 0.08f, 12, snowSh);
                mb.Build("Mesh", sh, mats);
                mb = new MB(4);
                mb.Sub(SNOW).Sphere(new Vector3(0, -0.04f, 0.03f), new Vector3(0.09f, 0.12f, 0.075f), 10, snow);
                mb.Build("Mesh", ft, mats);
            }
        }

        // the build seed, so BodyColor stays deterministic per character
        [System.ThreadStatic] static int _seed;

        static void BuildPhysics(HumanRig r)
        {
            Cap(r, r.hips, new Vector3(0, -0.02f, 0), 0.16f, 0.32f, 0, 11f);
            Cap(r, r.spine, new Vector3(0, 0.1f, 0), 0.15f, 0.26f, 1, 9f);
            Cap(r, r.chest, new Vector3(0, 0.07f, 0), 0.17f, 0.4f, 0, 14f);
            Cap(r, r.head, new Vector3(0, 0.06f, 0.01f), 0.11f, 0.26f, 1, 5f);
            foreach (var t in new[] { r.uaL, r.uaR }) Cap(r, t, new Vector3(0, 0, r.upperArm * 0.5f), 0.055f, r.upperArm, 2, 2.5f);
            foreach (var t in new[] { r.faL, r.faR }) Cap(r, t, new Vector3(0, 0, r.foreArm * 0.5f + 0.03f), 0.045f, r.foreArm + 0.06f, 2, 1.8f);
            foreach (var t in new[] { r.thL, r.thR }) Cap(r, t, new Vector3(0, 0, r.thigh * 0.5f), 0.08f, r.thigh, 2, 7f);
            foreach (var t in new[] { r.shL, r.shR }) Cap(r, t, new Vector3(0, 0, r.shin * 0.5f), 0.06f, r.shin, 2, 4f);
            foreach (var t in new[] { r.footL, r.footR }) Cap(r, t, new Vector3(0, -0.05f, 0.03f), 0.05f, 0.2f, 1, 1f);

            Joint(r.spine, r.hips, -20, 30, 25, 15);
            Joint(r.chest, r.spine, -20, 25, 20, 15);
            Joint(r.head, r.chest, -40, 40, 40, 40);
            Joint(r.uaL, r.chest, -70, 90, 80, 60); Joint(r.uaR, r.chest, -70, 90, 80, 60);
            Joint(r.faL, r.uaL, -120, 5, 10, 60); Joint(r.faR, r.uaR, -120, 5, 10, 60);
            Joint(r.thL, r.hips, -60, 90, 35, 20); Joint(r.thR, r.hips, -60, 90, 35, 20);
            Joint(r.shL, r.thL, -5, 130, 5, 5); Joint(r.shR, r.thR, -5, 130, 5, 5);
            Joint(r.footL, r.shL, -30, 30, 10, 10); Joint(r.footR, r.shR, -30, 30, 10, 10);
            r.SetRagdoll(false);
        }

        /// <summary>Resize a bone's hitbox to the body drawn on it (center null / height &lt; 0 / dir &lt; 0 = keep).</summary>
        static void Fit(Transform t, Vector3? center, float radius, float height, int dir)
        {
            var c = t != null ? t.GetComponent<CapsuleCollider>() : null;
            if (c == null) return;
            if (center.HasValue) c.center = center.Value;
            c.radius = radius;
            if (height > 0f) c.height = Mathf.Max(height, radius * 2f);
            if (dir >= 0) c.direction = dir;
        }

        static void Cap(HumanRig r, Transform t, Vector3 center, float radius, float height, int dir, float mass)
        {
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            c.center = center; c.radius = radius; c.height = height; c.direction = dir;
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.SetDamping(0.05f, 0.4f);
            rb.interpolation = RigidbodyInterpolation.None;
            r.bodies.Add(rb);
            r.colliders.Add(c);
        }

        static void Joint(Transform t, Transform parent, float lowTwist, float highTwist, float swing1, float swing2)
        {
            var j = t.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = parent.GetComponent<Rigidbody>();
            j.axis = Vector3.right;
            j.swingAxis = Vector3.up;
            j.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            j.highTwistLimit = new SoftJointLimit { limit = highTwist };
            j.swing1Limit = new SoftJointLimit { limit = swing1 };
            j.swing2Limit = new SoftJointLimit { limit = swing2 };
            j.enablePreprocessing = false;
            j.enableProjection = true;
        }

        public void SetRagdoll(bool on, Vector3 impulse = default, Vector3 point = default, Vector3 inheritVelocity = default)
        {
            ragdoll = on;
            foreach (var rb in bodies)
            {
                rb.isKinematic = !on;
                rb.collisionDetectionMode = on ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
                if (on) { rb.SetVel(inheritVelocity); rb.angularVelocity = Vector3.zero; }
            }
            if (on)
            {
                foreach (var h in headRenderers) h.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (impulse != default)
                {
                    Rigidbody best = null; float bd = 1e9f;
                    foreach (var rb in bodies) { float d = (rb.worldCenterOfMass - point).sqrMagnitude; if (d < bd) { bd = d; best = rb; } }
                    if (best != null) best.AddForceAtPosition(impulse, point, ForceMode.Impulse);
                    foreach (var rb in bodies) rb.AddForce(impulse * 0.08f, ForceMode.Impulse);
                }
            }
        }

        public void SetCollidersEnabled(bool on) { foreach (var c in colliders) c.enabled = on; }

        // --------------------------------------------------------------------------------------------- pose
        /// <summary>
        /// Procedural full-body pose from head + hands (IK) and a locomotion cycle for the legs.
        /// rootPos = point on the ground under the body, yaw = body facing.
        /// </summary>
        public void Pose(Vector3 rootPos, float yaw, Vector3 headPos, Quaternion headRot, Vector3 handLPos, Quaternion handLRot,
            Vector3 handRPos, Quaternion handRRot, Vector3 velocity, float dt, bool handsValid = true, bool seated = false, Vector3? seatFoot = null)
        {
            if (ragdoll) return;
            Quaternion yawQ = Quaternion.Euler(0, yaw, 0);
            Vector3 fwd = yawQ * Vector3.forward, right = yawQ * Vector3.right;
            float headH = headPos.y - rootPos.y;
            float crouch = Mathf.InverseLerp(1.62f, 1.0f, headH);
            // headPos is the eye point. Head bone sits at the base of the skull, neck bone at the top of the chest.
            // mostly your facing, only a little of your head's tilt: looking down used to swing the neck (and with it your
            // chest and arms) ~11 cm back, so the body looked like it was behind your head
            // (eyes 6 cm above and 4 cm in front of the head bone: it was 8 / 7 cm, which put your body a bit too far back
            // and low under your view)
            Vector3 headBone = headPos - Quaternion.Slerp(yawQ, headRot, 0.3f) * new Vector3(0, 0.06f, 0.04f);
            Vector3 neckBase = headBone - Vector3.Slerp(Vector3.up, headRot * Vector3.up, 0.3f) * 0.1f;
            Vector3 hipsGuess = rootPos + Vector3.up * Mathf.Lerp(0.98f, 0.6f, crouch) - fwd * crouch * 0.1f;
            Vector3 toNeck = neckBase - hipsGuess;
            Vector3 spineUp = toNeck.sqrMagnitude > 0.0001f ? toNeck.normalized : Vector3.up;
            spineUp = Vector3.Slerp(Vector3.up, spineUp, 0.85f).normalized;
            Vector3 hipsPos = neckBase - spineUp * 0.42f;
            hipsPos.y = Mathf.Clamp(hipsPos.y, rootPos.y + 0.35f, rootPos.y + 1.02f);
            if (seated)
            {
                // sitting (cart seat): rootPos is the top of the cushion - hips on it, back upright, no lean from crouching
                hipsPos = rootPos + Vector3.up * 0.1f - fwd * 0.04f; crouch = 0f;
                toNeck = neckBase - hipsPos;
                spineUp = toNeck.sqrMagnitude > 0.0001f ? Vector3.Slerp(Vector3.up, toNeck.normalized, 0.85f).normalized : Vector3.up;
            }
            Vector3 bodyFwd = Vector3.ProjectOnPlane(fwd, spineUp).normalized;
            if (bodyFwd.sqrMagnitude < 0.01f) bodyFwd = fwd;

            hips.position = hipsPos;
            hips.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(bodyFwd, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(crouch * 25f, 0, 0);
            Vector3 mid = Vector3.Slerp(Vector3.up, spineUp, 0.6f);
            spine.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(bodyFwd, mid).normalized, mid);
            chest.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(bodyFwd, spineUp).normalized, spineUp);
            Vector3 headFwd = headRot * Vector3.forward;
            neck.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.Slerp(bodyFwd, headFwd, 0.5f), spineUp), spineUp);
            head.rotation = headRot;

            // arms
            if (handsValid)
            {
                ArmIK(uaL, faL, handL, handLPos, handLRot, -1);
                ArmIK(uaR, faR, handR, handRPos, handRRot, 1);
            }
            else
            {
                RelaxArm(uaL, faL, -1); RelaxArm(uaR, faR, 1);
            }

            if (seated)
            {
                // knees forward over the seat edge, feet on the floor in front (no walking while you ride)
                _phase = 0f;
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1 : 1;
                    Vector3 sf = seatFoot ?? new Vector3(0.15f, -0.4f, 0.42f);   // out to the side, down, forward from the seat
                    Vector3 foot = rootPos + fwd * sf.z + right * side * sf.x + Vector3.up * sf.y;
                    LegIK(s == 0 ? thL : thR, s == 0 ? shL : shR, s == 0 ? footL : footR, foot, fwd + Vector3.up * 0.5f, right * side);
                }
                return;
            }
            // legs: walk cycle driven by planar speed
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = planar.magnitude;
            float stride = Mathf.Clamp(speed * 0.32f, 0, 0.55f);
            _phase += dt * (speed > 0.15f ? 3.2f + speed * 1.8f : 0f);
            if (speed <= 0.15f) _phase = Mathf.MoveTowards(_phase, Mathf.Round(_phase / Mathf.PI) * Mathf.PI, dt * 4f);
            Vector3 moveDir = speed > 0.1f ? planar / speed : fwd;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                float ph = _phase + (s == 0 ? 0 : Mathf.PI);
                Vector3 hipJ = (s == 0 ? thL : thR).position;
                // crouching kneels: the feet go back under / behind you (they used to come forward, so your knees and thighs
                // rose up in front of you and blocked the view down)
                Vector3 foot = rootPos + right * side * (0.11f + crouch * 0.08f) + moveDir * Mathf.Sin(ph) * stride * 0.5f - fwd * crouch * 0.3f;
                foot.y = rootPos.y + 0.085f + Mathf.Max(0, Mathf.Cos(ph)) * stride * 0.25f;
                LegIK(s == 0 ? thL : thR, s == 0 ? shL : shR, s == 0 ? footL : footR, foot, fwd, right * side);
            }
        }

        void ArmIK(Transform ua, Transform fa, Transform hand, Vector3 target, Quaternion rot, float side)
        {
            Vector3 sh = ua.position;
            Vector3 hint = chest.rotation * new Vector3(side * 0.6f, -1f, -0.5f);
            float reach = upperArm + foreArm;
            Vector3 d = target - sh;
            if (d.magnitude > reach * 0.999f) target = sh + d.normalized * reach * 0.999f;
            Vector3 elbow = MathX.SolveKnee(sh, target, upperArm, foreArm, hint);
            ua.rotation = Quaternion.LookRotation(elbow - sh, chest.rotation * Vector3.up);
            fa.rotation = Quaternion.LookRotation(target - elbow, rot * Vector3.up);
            hand.rotation = rot;
        }

        void RelaxArm(Transform ua, Transform fa, float side)
        {
            ua.rotation = chest.rotation * Quaternion.Euler(80, side * 8, 0);
            fa.rotation = ua.rotation * Quaternion.Euler(-20, 0, 0);
        }

        void LegIK(Transform th, Transform sh, Transform ft, Vector3 foot, Vector3 fwd, Vector3 outward)
        {
            Vector3 hip = th.position;
            Vector3 knee = MathX.SolveKnee(hip, foot, thigh, shin, fwd + outward * 0.1f);
            th.rotation = Quaternion.LookRotation(knee - hip, fwd);
            sh.rotation = Quaternion.LookRotation(foot - knee, fwd);
            // foot: +Z of foot bone follows the shin direction (down), boot mesh extends along -Y (forward)
            ft.rotation = Quaternion.LookRotation(Vector3.down, -fwd);
        }

        // ------------------------------------------------------------------------------------------- name tape
        static readonly string[] Surnames = { "VOLKOV", "BRANDT", "KOVAC", "MERCER", "ORLOV", "DRAKE", "SOKOL", "HALE", "BECK", "NOVAK", "RUSSO", "PETROV", "GRANT", "MALIK", "KRAUSE", "VANCE", "IVANOV", "SHAW", "DUBOIS", "REYES", "LANG", "MORENO", "FISCHER", "KANE", "BOYD", "STROM", "ZAJAC", "HOLT", "MARSH", "PIKE", "CROSS", "WOLFE", "KESSEL", "YATES", "BRODY", "LUKIN" };
        /// <summary>A surname for a scav, fixed by its seed.</summary>
        public static string ScavName(int seed) { uint h = (uint)seed * 2654435761u; return Surnames[(int)(h % (uint)Surnames.Length)]; }

        /// <summary>A sewn name tape high on the chest: the person's name (a player's name without the #tag).
        /// Returns the text so it can be changed later; the tape goes away with the rig.</summary>
        public static TextMesh NameTape(HumanRig r, string name, bool creator = false)
        {
            if (r == null || r.chest == null) return null;
            var root = new GameObject("NameTape").transform;
            root.SetParent(r.chest, false);
            root.localPosition = new Vector3(0, 0.15f, 0.104f);
            root.localRotation = Quaternion.Euler(-18f, 0, 0);
            // a content creator's tape is red with white letters, with a CC badge (camera icon) beside it
            var tmb = new MB().Box(Vector3.zero, new Vector3(0.15f, 0.034f, 0.006f), creator ? new Color(0.78f, 0.08f, 0.07f) : new Color(0.2f, 0.22f, 0.16f), 0.002f);
            if (creator)
            {
                var bc = new Vector3(0.112f, 0f, 0.0005f);
                tmb.Cyl(bc + new Vector3(0, 0, -0.003f), bc + new Vector3(0, 0, 0.003f), 0.024f, 0.024f, 20, new Color(0.95f, 0.8f, 0.25f));   // gold rim
                tmb.Cyl(bc + new Vector3(0, 0, -0.0035f), bc + new Vector3(0, 0, 0.0036f), 0.02f, 0.02f, 20, new Color(0.78f, 0.08f, 0.07f));   // red patch
                tmb.Box(bc + new Vector3(-0.002f, 0.007f, 0.0042f), new Vector3(0.018f, 0.011f, 0.002f), Color.white, 0.001f);                 // camera body
                tmb.Cyl(bc + new Vector3(0.007f, 0.007f, 0.0042f), bc + new Vector3(0.007f, 0.007f, 0.0055f), 0.0035f, 0.0035f, 10, new Color(0.78f, 0.08f, 0.07f));   // lens
            }
            var tape = tmb.Build("Tape", root, Mats.Lit("name_tape", Color.white, 0f, 0.15f));
            tape.layer = r.chest.gameObject.layer;
            if (creator)
            {
                var cc = UI.Text(root, "CC", new Vector3(0.112f, -0.0075f, 0.0068f), 0.0085f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                cc.transform.localRotation = Quaternion.Euler(0, 180f, 0); cc.gameObject.layer = r.chest.gameObject.layer;
            }
            var tm = UI.Text(root, "", new Vector3(0, 0, 0.0065f), 0.02f, creator ? Color.white : new Color(0.05f, 0.05f, 0.04f), TextAnchor.MiddleCenter, FontStyle.Bold);
            tm.transform.localRotation = Quaternion.Euler(0, 180f, 0);   // readable from the front (text faces away from its +Z)
            tm.gameObject.layer = r.chest.gameObject.layer;
            SetTape(tm, name);
            PlaceTape(r, tm);
            return tm;
        }

        /// <summary>
        /// Lay the name tape flat on the front of the chest - or on the front of the vest when one is worn (it used to
        /// float out in front of the collar, and hovered above a vest). The vest is built in chest space, so its front
        /// is read straight from its mesh. Call again after the gear changes (a frame later, once the old vest is gone).
        /// </summary>
        public static void PlaceTape(HumanRig r, TextMesh tm)
        {
            if (r == null || r.chest == null || tm == null) return;
            var root = tm.transform.parent;
            float z = 0.104f, tilt = -18f;   // on the chest shell, where its rounded top starts
            Transform vest = null;
            for (int i = 0; i < r.chest.childCount; i++) { var c = r.chest.GetChild(i); if (c.gameObject.name == "Vest") vest = c; }   // the newest one
            var mf = vest != null ? vest.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null)
            {
                float best = -1f;
                // the front of the vest's upper half (its plate / admin panel; the pouches lower down stick out further)
                foreach (var v in mf.sharedMesh.vertices)
                    if (v.y > 0.05f && v.y < 0.215f && Mathf.Abs(v.x) < 0.2f && v.z > best) best = v.z;
                if (best > 0f) { z = best + 0.003f; tilt = -6f; }
            }
            root.localPosition = new Vector3(0, 0.15f, z);
            root.localRotation = Quaternion.Euler(tilt, 0, 0);
        }
        public static void SetTape(TextMesh tm, string name)
        {
            if (tm == null) return;
            name = (name ?? "").Split('#')[0].Trim().ToUpperInvariant();
            if (name.Length > 12) name = name.Substring(0, 12);
            tm.text = name;
            // shrink long names to fit the tape
            tm.characterSize = 0.002f * Mathf.Min(1f, 9f / Mathf.Max(1, name.Length));
        }
    }
}
