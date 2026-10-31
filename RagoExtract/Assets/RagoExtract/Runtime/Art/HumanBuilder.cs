using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public enum Outfit { Operator, Raider, RaiderHeavy, Gunsmith, Medic, Fence, Quartermaster, Kiosk }

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
        public float upperArm = 0.29f, foreArm = 0.27f, thigh = 0.45f, shin = 0.45f, spineLen = 0.36f, neckLen = 0.16f;
        public bool ragdoll;
        public Transform weaponR; // where an NPC rifle sits
        float _phase;

        static Material ClothMat => Mats.Lit("cloth", Color.white, 0f, 0.15f, Mats.Fabric, Mats.FabricN, 0.4f, 0.05f);
        static Material HardMat => Mats.Lit("gear_hard", Color.white, 0.2f, 0.4f, Mats.Noise, null, 0.3f, 0.35f);
        static Material SkinMat => Mats.Lit("skin_v", Color.white, 0f, 0.35f, Mats.Noise, null, 0.1f, 0f);

        // --------------------------------------------------------------------------------------------- build
        public static HumanRig Build(Outfit outfit, int seed, Transform parent, bool localPlayer = false)
        {
            var r = new HumanRig();
            var rng = new Rng(seed);
            r.root = new GameObject("Human_" + outfit);
            r.root.transform.SetParent(parent, false);

            var pal = Palette(outfit, rng);
            r.hips = Bone(r, "Hips", r.root.transform, new Vector3(0, 0.98f, 0));
            r.spine = Bone(r, "Spine", r.hips, new Vector3(0, 0.12f, 0));
            r.chest = Bone(r, "Chest", r.spine, new Vector3(0, 0.24f, 0));
            r.neck = Bone(r, "Neck", r.chest, new Vector3(0, 0.15f, 0));
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

            BuildMeshes(r, outfit, pal, rng, localPlayer);
            BuildPhysics(r);
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

        struct Pal { public Color top, top2, pants, boots, gear, gear2, skin, helmet, accent, hair; public bool helmetOn, mask, beanie, gasMask, plate, cap, apron, coat, suit; }

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
                    p.helmetOn = true; p.mask = rng.Chance(0.6f); p.plate = true;
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
                    p.helmetOn = o == Outfit.RaiderHeavy; p.plate = o == Outfit.RaiderHeavy || rng.Chance(0.3f);
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
                case Outfit.Quartermaster:
                    p.top = new Color(0.3f, 0.33f, 0.24f); p.top2 = p.top * 0.85f; p.pants = new Color(0.28f, 0.3f, 0.22f); p.gear = new Color(0.25f, 0.2f, 0.15f); p.beanie = true; p.plate = false; p.accent = new Color(0.9f, 0.5f, 0.1f);
                    break;
                default:
                    p.top = new Color(0.15f, 0.2f, 0.3f); p.top2 = p.top; p.pants = new Color(0.1f, 0.1f, 0.12f); p.gear = new Color(0.9f, 0.5f, 0.1f); p.cap = true; p.accent = new Color(0.9f, 0.5f, 0.1f);
                    break;
            }
            return p;
        }

        static void BuildMeshes(HumanRig r, Outfit o, Pal p, Rng rng, bool local)
        {
            var cloth = ClothMat; var hard = HardMat; var skin = SkinMat;
            // ---- hips / pelvis + belt
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, -0.02f, 0), new Vector3(0.33f, 0.2f, 0.2f), p.pants, 0.06f, 2);
            mb.Sub(1).Box(new Vector3(0, 0.07f, 0), new Vector3(0.35f, 0.045f, 0.22f), p.gear2, 0.015f);
            mb.Sub(1).Box(new Vector3(0, 0.07f, 0.112f), new Vector3(0.05f, 0.035f, 0.01f), new Color(0.55f, 0.55f, 0.55f), 0.004f);
            if (o == Outfit.Operator || o == Outfit.RaiderHeavy || o == Outfit.Raider)
            {
                mb.Sub(1).Box(new Vector3(0.16f, 0.02f, -0.06f), new Vector3(0.06f, 0.09f, 0.08f), p.gear, 0.012f); // dump pouch
                mb.Sub(1).Box(new Vector3(-0.17f, 0.03f, 0.03f), new Vector3(0.05f, 0.07f, 0.06f), p.gear, 0.01f);
            }
            mb.Build("Mesh", r.hips, cloth, hard);

            // ---- spine (stomach)
            mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.12f, 0), new Vector3(0.31f, 0.26f, 0.19f), p.coat ? p.top : p.top2, 0.07f, 2);
            if (p.apron) mb.Sub(1).Box(new Vector3(0, 0.05f, 0.1f), new Vector3(0.3f, 0.4f, 0.02f), p.gear, 0.01f);
            if (p.coat) mb.Sub(0).Box(new Vector3(0, -0.06f, 0.0f), new Vector3(0.36f, 0.3f, 0.23f), p.top, 0.05f, 2);
            mb.Build("Mesh", r.spine, cloth, hard);

            // ---- chest: torso + plate carrier / chest rig
            mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.07f, 0), new Vector3(0.38f, 0.26f, 0.22f), p.top, 0.08f, 2);
            mb.Sub(0).Sphere(new Vector3(-0.17f, 0.12f, 0), new Vector3(0.075f, 0.07f, 0.08f), 10, p.top);
            mb.Sub(0).Sphere(new Vector3(0.17f, 0.12f, 0), new Vector3(0.075f, 0.07f, 0.08f), 10, p.top);
            if (p.suit)
            {
                mb.Sub(0).Box(new Vector3(0, 0.07f, 0.112f), new Vector3(0.1f, 0.2f, 0.01f), Color.white * 0.9f, 0.01f);
                mb.Sub(0).Box(new Vector3(0, 0.06f, 0.12f), new Vector3(0.03f, 0.17f, 0.01f), p.top2, 0.005f);
            }
            if (p.plate)
            {
                mb.Sub(1).Box(new Vector3(0, 0.04f, 0.12f), new Vector3(0.3f, 0.33f, 0.06f), p.gear, 0.02f, 2);
                mb.Sub(1).Box(new Vector3(0, 0.04f, -0.12f), new Vector3(0.3f, 0.33f, 0.05f), p.gear, 0.02f, 2);
                mb.Sub(1).Box(new Vector3(-0.17f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), p.gear2, 0.01f);
                mb.Sub(1).Box(new Vector3(0.17f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), p.gear2, 0.01f);
                // shoulder straps
                mb.Sub(1).Box(new Vector3(-0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), p.gear2, 0.01f);
                mb.Sub(1).Box(new Vector3(0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), p.gear2, 0.01f);
                // triple mag pouches + admin pouch
                for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(-0.09f + i * 0.09f, -0.06f, 0.165f), new Vector3(0.075f, 0.12f, 0.04f), p.gear2, 0.012f);
                mb.Sub(1).Box(new Vector3(0, 0.12f, 0.16f), new Vector3(0.16f, 0.06f, 0.03f), p.gear2, 0.01f);
                if (rng.Chance(0.6f)) mb.Sub(1).Box(new Vector3(0.11f, 0.15f, 0.17f), new Vector3(0.04f, 0.04f, 0.02f), p.accent, 0.005f); // patch
            }
            else if (o == Outfit.Raider)
            {
                // cheap chest rig with mags + red armband feel
                mb.Sub(1).Box(new Vector3(0, -0.04f, 0.12f), new Vector3(0.28f, 0.14f, 0.05f), p.gear, 0.015f);
                for (int i = 0; i < 4; i++) mb.Sub(1).Box(new Vector3(-0.1f + i * 0.067f, -0.02f, 0.15f), new Vector3(0.055f, 0.12f, 0.03f), p.gear2, 0.008f);
                mb.Sub(1).Box(new Vector3(-0.1f, 0.15f, 0.02f), new Vector3(0.04f, 0.02f, 0.25f), p.gear2, 0.005f);
                mb.Sub(1).Box(new Vector3(0.1f, 0.15f, 0.02f), new Vector3(0.04f, 0.02f, 0.25f), p.gear2, 0.005f);
            }
            if (o == Outfit.Operator) mb.Sub(1).Box(new Vector3(0, 0.02f, -0.2f), new Vector3(0.28f, 0.34f, 0.16f), p.gear * 0.9f, 0.05f, 2); // backpack
            mb.Build("Mesh", r.chest, cloth, hard);

            // ---- neck
            mb = new MB(2);
            mb.Sub(0).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.1f, 0), 0.055f, 0.05f, 10, p.mask ? p.top2 * 0.6f : p.skin, false);
            if (p.suit) mb.Sub(0).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.03f, 0), 0.065f, 0.06f, 10, Color.white * 0.9f, false);
            var neckGo = mb.Build("Mesh", r.neck, p.mask ? cloth : skin, hard);

            // ---- head
            mb = new MB(3);
            Color face = p.mask ? p.top2 * 0.55f : p.skin;
            mb.Sub(2).Sphere(new Vector3(0, 0.07f, 0.005f), new Vector3(0.095f, 0.115f, 0.105f), 16, face);
            mb.Sub(2).Box(new Vector3(0, 0.0f, 0.03f), new Vector3(0.14f, 0.08f, 0.13f), face, 0.04f, 2); // jaw
            mb.Sub(2).Box(new Vector3(0, 0.06f, 0.105f), new Vector3(0.026f, 0.04f, 0.03f), face * 1.03f, 0.01f, 1); // nose
            mb.Sub(2).Box(new Vector3(0, 0.1f, 0.09f), new Vector3(0.15f, 0.025f, 0.04f), face * 0.97f, 0.01f, 1); // brow
            mb.Sub(2).Sphere(new Vector3(-0.098f, 0.065f, 0.0f), new Vector3(0.015f, 0.03f, 0.022f), 8, p.skin); // ears
            mb.Sub(2).Sphere(new Vector3(0.098f, 0.065f, 0.0f), new Vector3(0.015f, 0.03f, 0.022f), 8, p.skin);
            if (p.mask) mb.Sub(2).Box(new Vector3(0, 0.08f, 0.088f), new Vector3(0.16f, 0.04f, 0.03f), p.skin, 0.012f); // eye slot skin
            mb.Sub(1).Sphere(new Vector3(-0.035f, 0.08f, 0.098f), new Vector3(0.013f, 0.009f, 0.006f), 8, new Color(0.05f, 0.05f, 0.05f));
            mb.Sub(1).Sphere(new Vector3(0.035f, 0.08f, 0.098f), new Vector3(0.013f, 0.009f, 0.006f), 8, new Color(0.05f, 0.05f, 0.05f));
            if (!p.helmetOn && !p.beanie && !p.cap && !p.gasMask) mb.Sub(0).Sphere(new Vector3(0, 0.1f, -0.005f), new Vector3(0.1f, 0.09f, 0.11f), 14, p.hair, 0f, 0.5f);
            if (p.helmetOn)
            {
                mb.Sub(1).Sphere(new Vector3(0, 0.1f, -0.005f), new Vector3(0.125f, 0.12f, 0.135f), 18, p.helmet, 0f, 0.55f);
                mb.Sub(1).Box(new Vector3(0, 0.155f, 0.115f), new Vector3(0.05f, 0.035f, 0.02f), new Color(0.1f, 0.1f, 0.1f), 0.005f); // NVG shroud
                mb.Sub(1).Box(new Vector3(-0.12f, 0.06f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2); // ear pro
                mb.Sub(1).Box(new Vector3(0.12f, 0.06f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
                mb.Sub(1).Box(new Vector3(-0.12f, 0.11f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), new Color(0.1f, 0.1f, 0.1f), 0.003f); // side rails
                mb.Sub(1).Box(new Vector3(0.12f, 0.11f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), new Color(0.1f, 0.1f, 0.1f), 0.003f);
            }
            if (p.beanie) mb.Sub(0).Sphere(new Vector3(0, 0.1f, -0.005f), new Vector3(0.105f, 0.11f, 0.115f), 14, rng.Pick(new[] { new Color(0.15f, 0.15f, 0.15f), new Color(0.35f, 0.1f, 0.08f), new Color(0.25f, 0.27f, 0.2f) }), 0f, 0.55f);
            if (p.cap)
            {
                mb.Sub(0).Sphere(new Vector3(0, 0.11f, -0.005f), new Vector3(0.103f, 0.09f, 0.112f), 14, p.gear, 0f, 0.5f);
                mb.Sub(0).Box(new Vector3(0, 0.115f, 0.12f), new Vector3(0.15f, 0.01f, 0.08f), p.gear, 0.004f, 1, Quaternion.Euler(-8, 0, 0));
            }
            if (p.gasMask)
            {
                mb.Sub(1).Sphere(new Vector3(0, 0.05f, 0.06f), new Vector3(0.1f, 0.1f, 0.08f), 14, new Color(0.15f, 0.15f, 0.14f));
                mb.Sub(1).Cyl(new Vector3(0, 0.0f, 0.12f), new Vector3(0, -0.03f, 0.17f), 0.035f, 0.035f, 12, new Color(0.25f, 0.27f, 0.2f));
                mb.Sub(1).Sphere(new Vector3(-0.04f, 0.08f, 0.115f), new Vector3(0.028f, 0.028f, 0.01f), 10, new Color(0.2f, 0.25f, 0.3f));
                mb.Sub(1).Sphere(new Vector3(0.04f, 0.08f, 0.115f), new Vector3(0.028f, 0.028f, 0.01f), 10, new Color(0.2f, 0.25f, 0.3f));
            }
            var headGo = mb.Build("Mesh", r.head, cloth, hard, p.mask ? cloth : skin);
            if (local)
            {
                // the player's own head would block the camera: render shadows only
                foreach (var rr in new[] { headGo.GetComponent<Renderer>(), neckGo.GetComponent<Renderer>() })
                {
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    r.headRenderers.Add(rr);
                }
            }

            // ---- arms
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                var ua = s == 0 ? r.uaL : r.uaR; var fa = s == 0 ? r.faL : r.faR; var hand = s == 0 ? r.handL : r.handR;
                mb = new MB(2);
                mb.Sub(0).Capsule(new Vector3(0, 0, 0.02f), new Vector3(0, 0, r.upperArm - 0.02f), 0.062f, 0.052f, 12, p.coat ? p.top : p.top);
                if (o == Outfit.Operator) mb.Sub(1).Box(new Vector3(side * 0.05f, 0, 0.1f), new Vector3(0.012f, 0.06f, 0.07f), p.accent, 0.004f); // shoulder patch
                if (o == Outfit.Raider && s == 0) mb.Sub(1).Cyl(new Vector3(0, 0, 0.12f), new Vector3(0, 0, 0.17f), 0.064f, 0.064f, 12, p.accent, false); // red armband
                mb.Build("Mesh", ua, cloth, hard);
                mb = new MB(2);
                bool sleeves = !(o == Outfit.Gunsmith);
                mb.Sub(sleeves ? 0 : 1).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.foreArm - 0.03f), 0.05f, 0.04f, 12, sleeves ? p.top2 : p.skin);
                mb.Build("Mesh", fa, sleeves ? cloth : skin, skin);
                if (!local)
                {
                    mb = new MB(1);
                    Color glove = o == Outfit.Operator || o == Outfit.Raider || o == Outfit.RaiderHeavy ? new Color(0.12f, 0.11f, 0.1f) : p.skin;
                    mb.Box(new Vector3(0, -0.005f, 0.05f), new Vector3(0.08f, 0.035f, 0.1f), glove, 0.015f, 2);
                    mb.Box(new Vector3(side * -0.04f, 0.0f, 0.04f), new Vector3(0.025f, 0.025f, 0.06f), glove, 0.01f, 1, Quaternion.Euler(0, side * -30, 0));
                    mb.Build("Mesh", hand, o == Outfit.Operator || o == Outfit.Raider ? hard : skin);
                }
            }
            // ---- legs
            for (int s = 0; s < 2; s++)
            {
                var th = s == 0 ? r.thL : r.thR; var sh = s == 0 ? r.shL : r.shR; var ft = s == 0 ? r.footL : r.footR;
                float side = s == 0 ? -1 : 1;
                mb = new MB(2);
                mb.Sub(0).Capsule(new Vector3(0, 0, 0.0f), new Vector3(0, 0, r.thigh - 0.03f), 0.085f, 0.065f, 12, p.coat ? p.top : p.pants);
                if (o == Outfit.Operator || o == Outfit.RaiderHeavy) mb.Sub(1).Box(new Vector3(side * 0.08f, 0, 0.22f), new Vector3(0.03f, 0.1f, 0.12f), p.gear2, 0.01f); // thigh pouch
                mb.Build("Mesh", th, cloth, hard);
                mb = new MB(2);
                mb.Sub(0).Capsule(new Vector3(0, 0, 0.02f), new Vector3(0, 0, r.shin - 0.06f), 0.062f, 0.05f, 12, p.pants);
                if (o == Outfit.Operator) mb.Sub(1).Box(new Vector3(0, -0.055f, 0.04f), new Vector3(0.09f, 0.04f, 0.08f), p.gear2, 0.015f, 2); // knee pad
                mb.Sub(1).Cyl(new Vector3(0, 0, r.shin - 0.12f), new Vector3(0, 0, r.shin), 0.058f, 0.058f, 12, p.boots, false); // boot shaft
                mb.Build("Mesh", sh, cloth, hard);
                mb = new MB(1);
                // foot: built in foot space (+Z = down the leg). Boot points forward = -Y in the shin frame -> use rotation
                mb.Box(new Vector3(0, -0.055f, 0.035f), new Vector3(0.1f, 0.2f, 0.07f), p.boots, 0.025f, 2);
                mb.Box(new Vector3(0, -0.055f, 0.072f), new Vector3(0.105f, 0.21f, 0.012f), p.boots * 0.6f, 0.004f);
                mb.Build("Mesh", ft, hard);
            }
        }

        static void BuildPhysics(HumanRig r)
        {
            Cap(r, r.hips, new Vector3(0, -0.02f, 0), 0.16f, 0.32f, 0, 11f);
            Cap(r, r.spine, new Vector3(0, 0.12f, 0), 0.15f, 0.3f, 1, 9f);
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
            Vector3 handRPos, Quaternion handRRot, Vector3 velocity, float dt, bool handsValid = true)
        {
            if (ragdoll) return;
            Quaternion yawQ = Quaternion.Euler(0, yaw, 0);
            Vector3 fwd = yawQ * Vector3.forward, right = yawQ * Vector3.right;
            float headH = headPos.y - rootPos.y;
            float crouch = Mathf.InverseLerp(1.62f, 1.0f, headH);
            // headPos is the eye point. Head bone sits at the base of the skull, neck bone at the top of the chest.
            Vector3 headBone = headPos - headRot * new Vector3(0, 0.08f, 0.09f);
            Vector3 neckBase = headBone - Vector3.Slerp(Vector3.up, headRot * Vector3.up, 0.3f) * 0.1f;
            Vector3 hipsGuess = rootPos + Vector3.up * Mathf.Lerp(0.98f, 0.6f, crouch) - fwd * crouch * 0.1f;
            Vector3 toNeck = neckBase - hipsGuess;
            Vector3 spineUp = toNeck.sqrMagnitude > 0.0001f ? toNeck.normalized : Vector3.up;
            spineUp = Vector3.Slerp(Vector3.up, spineUp, 0.85f).normalized;
            Vector3 hipsPos = neckBase - spineUp * 0.51f;
            hipsPos.y = Mathf.Clamp(hipsPos.y, rootPos.y + 0.35f, rootPos.y + 1.02f);
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
                Vector3 foot = rootPos + right * side * (0.11f + crouch * 0.08f) + moveDir * Mathf.Sin(ph) * stride * 0.5f + fwd * crouch * 0.15f;
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
    }
}
