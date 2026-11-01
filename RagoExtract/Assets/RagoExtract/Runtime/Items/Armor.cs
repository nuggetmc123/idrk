using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Builds armor and backpack meshes. Helmets are built in head-bone space, vests and packs in chest-bone space,
    /// so the same mesh is used for the worn version (parented to the bone) and the loot item (centred).
    /// </summary>
    public static class ArmorBuilder
    {
        static Material Shell => Mats.Lit("armor_shell", Color.white, 0.12f, 0.42f, Mats.Noise, null, 0.35f, 0.35f);
        static Material Fit => Mats.Lit("armor_fit", Color.white, 0.6f, 0.45f, Mats.Metal, null, 0.25f, 0.3f);
        static Material Cloth => Mats.Lit("armor_cloth", Color.white, 0f, 0.15f, Mats.Fabric, Mats.FabricN, 0.4f, 0.05f);
        static Material Visor => Mats.Lit("armor_visor", new Color(0.05f, 0.07f, 0.09f), 0.6f, 0.92f);
        static readonly Color Dark = new Color(0.1f, 0.1f, 0.11f);

        public static MB Helmet(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = d.color, t = d.trim;
            float s = d.style == 4 ? 1.1f : d.style == 0 ? 0.96f : 1f;
            float cut = d.style == 2 ? 0.48f : 0.55f;
            mb.Sub(0).Sphere(new Vector3(0, 0.105f, -0.002f), new Vector3(0.122f, 0.118f, 0.132f) * s, 18, c, 0f, cut);
            mb.Sub(0).Cyl(new Vector3(0, 0.1f - 0.005f * s, -0.002f), new Vector3(0, 0.108f, -0.002f), 0.123f * s, 0.123f * s, 18, c * 0.85f, false); // rim
            switch (d.style)
            {
                case 0: // bump helmet: vents + strap
                    for (int i = -1; i <= 1; i++) mb.Sub(1).Box(new Vector3(i * 0.035f, 0.215f, -0.01f), new Vector3(0.018f, 0.006f, 0.05f), Dark, 0.002f);
                    break;
                case 1: // ballistic: ear pro + chin strap
                    Ears(mb);
                    break;
                case 2: // FAST: rails, NVG shroud, velcro
                    Ears(mb);
                    mb.Sub(1).Box(new Vector3(-0.118f, 0.115f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), Dark, 0.003f);
                    mb.Sub(1).Box(new Vector3(0.118f, 0.115f, 0.03f), new Vector3(0.012f, 0.025f, 0.09f), Dark, 0.003f);
                    mb.Sub(1).Box(new Vector3(0, 0.16f, 0.118f), new Vector3(0.05f, 0.035f, 0.02f), Dark, 0.005f);
                    mb.Sub(2).Box(new Vector3(0, 0.205f, -0.04f), new Vector3(0.07f, 0.004f, 0.05f), t, 0.002f);
                    break;
                case 3: // assault visor
                    Ears(mb);
                    mb.Sub(3).Sphere(new Vector3(0, 0.07f, 0.03f), new Vector3(0.118f, 0.1f, 0.115f), 16, Color.white, 0.25f, 0.62f);
                    mb.Sub(1).Box(new Vector3(0, 0.165f, 0.12f), new Vector3(0.16f, 0.02f, 0.03f), t, 0.005f);
                    break;
                default: // juggernaut: full face plate with eye slit and gold trim
                    mb.Sub(0).Box(new Vector3(0, 0.05f, 0.1f), new Vector3(0.2f, 0.16f, 0.06f), c, 0.03f, 2);
                    mb.Sub(3).Box(new Vector3(0, 0.08f, 0.132f), new Vector3(0.15f, 0.022f, 0.01f), Color.white, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.215f, 0.0f), new Vector3(0.03f, 0.012f, 0.24f), t, 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.045f, 0.134f), new Vector3(0.19f, 0.01f, 0.006f), t, 0.002f);
                    Ears(mb);
                    break;
            }
            return mb;
        }

        static void Ears(MB mb)
        {
            mb.Sub(1).Box(new Vector3(-0.115f, 0.07f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
            mb.Sub(1).Box(new Vector3(0.115f, 0.07f, 0.0f), new Vector3(0.04f, 0.07f, 0.07f), new Color(0.12f, 0.12f, 0.12f), 0.015f, 2);
        }

        public static MB Vest(ArmorDef d)
        {
            var mb = new MB(4);
            Color c = d.color, c2 = d.color * 0.8f, t = d.trim;
            c2.a = 1;
            float thick = d.style == 0 ? 0.035f : d.style >= 2 ? 0.07f : 0.06f;
            mb.Sub(2).Box(new Vector3(0, 0.04f, 0.12f), new Vector3(0.3f, 0.32f, thick), c, 0.02f, 2);
            mb.Sub(2).Box(new Vector3(0, 0.04f, -0.12f), new Vector3(0.3f, 0.32f, thick * 0.9f), c, 0.02f, 2);
            mb.Sub(2).Box(new Vector3(-0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), c2, 0.01f);
            mb.Sub(2).Box(new Vector3(0.165f, 0.0f, 0), new Vector3(0.04f, 0.15f, 0.2f), c2, 0.01f);
            mb.Sub(2).Box(new Vector3(-0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), c2, 0.01f);
            mb.Sub(2).Box(new Vector3(0.1f, 0.19f, 0), new Vector3(0.06f, 0.03f, 0.25f), c2, 0.01f);
            if (d.style >= 1)
            {
                int pouches = d.style >= 3 ? 4 : 3;
                for (int i = 0; i < pouches; i++) mb.Sub(2).Box(new Vector3(-0.09f * (pouches - 1) / 2f + i * 0.09f * (3f / pouches), -0.06f, 0.12f + thick * 0.5f + 0.018f), new Vector3(0.07f, 0.12f, 0.036f), c2, 0.012f);
                mb.Sub(2).Box(new Vector3(0, 0.12f, 0.12f + thick * 0.5f + 0.012f), new Vector3(0.16f, 0.06f, 0.025f), c2, 0.01f);
            }
            if (d.style >= 2)
            {
                // shoulder pads + collar
                mb.Sub(0).Box(new Vector3(-0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, -25));
                mb.Sub(0).Box(new Vector3(0.2f, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.16f), c, 0.015f, 2, Quaternion.Euler(0, 0, 25));
                mb.Sub(0).Box(new Vector3(0, 0.22f, -0.04f), new Vector3(0.22f, 0.05f, 0.14f), c2, 0.02f, 2);
            }
            if (d.style >= 3) mb.Sub(2).Box(new Vector3(0, -0.2f, 0.125f), new Vector3(0.16f, 0.14f, 0.03f), c, 0.012f, 2); // groin flap
            if (d.style >= 4)
            {
                mb.Sub(0).Box(new Vector3(0, 0.04f, 0.16f), new Vector3(0.32f, 0.34f, 0.03f), c, 0.02f, 2);
                mb.Sub(1).Box(new Vector3(0, 0.21f, 0.18f), new Vector3(0.3f, 0.012f, 0.012f), t, 0.003f);
                mb.Sub(1).Box(new Vector3(0, -0.13f, 0.18f), new Vector3(0.3f, 0.012f, 0.012f), t, 0.003f);
            }
            mb.Sub(1).Box(new Vector3(0.1f, 0.15f, 0.12f + thick * 0.5f + 0.002f), new Vector3(0.04f, 0.035f, 0.004f), t, 0.002f); // patch
            return mb;
        }

        /// <summary>Backpack worn on the back (chest-bone space). Size follows the pocket grid.</summary>
        public static MB Pack(BackpackDef d)
        {
            var mb = new MB(4);
            float w = 0.16f + d.cols * 0.045f, h = 0.2f + d.rows * 0.06f, depth = 0.1f + d.rows * 0.025f;
            Color c = d.color, c2 = d.color * 0.8f; c2.a = 1;
            float y = 0.04f - (h - 0.34f) * 0.3f;
            mb.Sub(2).Box(new Vector3(0, y, -0.12f - depth * 0.5f), new Vector3(w, h, depth), c, 0.04f, 2);
            mb.Sub(2).Box(new Vector3(0, y - h * 0.2f, -0.12f - depth - 0.02f), new Vector3(w * 0.8f, h * 0.45f, 0.04f), c2, 0.02f, 1);
            mb.Sub(1).Box(new Vector3(-w * 0.3f, y, -0.11f), new Vector3(0.04f, h * 0.9f, 0.015f), Dark, 0.004f);
            mb.Sub(1).Box(new Vector3(w * 0.3f, y, -0.11f), new Vector3(0.04f, h * 0.9f, 0.015f), Dark, 0.004f);
            if (d.style >= 2) mb.Sub(1).Box(new Vector3(0, y + h * 0.5f + 0.02f, -0.12f - depth * 0.5f), new Vector3(w * 0.9f, 0.03f, depth * 0.6f), Dark, 0.01f); // frame top / bedroll strap
            if (d.style >= 3) { mb.Sub(2).Box(new Vector3(-w * 0.5f - 0.025f, y, -0.12f - depth * 0.5f), new Vector3(0.05f, h * 0.6f, depth * 0.7f), c2, 0.015f); mb.Sub(2).Box(new Vector3(w * 0.5f + 0.025f, y, -0.12f - depth * 0.5f), new Vector3(0.05f, h * 0.6f, depth * 0.7f), c2, 0.015f); }
            return mb;
        }

        public static Material[] Materials => new[] { Shell, Fit, Cloth, Visor };

        /// <summary>Attach a worn piece of gear to a bone. Returns the created object (so it can be removed later).</summary>
        public static GameObject Attach(MB mb, Transform bone, string name, bool shadowOnly)
        {
            var go = mb.Build(name, bone, Materials);
            if (shadowOnly) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            return go;
        }

        /// <summary>Applies helmet / vest / backpack visuals to a character.</summary>
        public static void Dress(HumanRig rig, ArmorDef helmet, ArmorDef vest, BackpackDef pack, bool local)
        {
            foreach (var g in rig.gearVisuals) if (g != null) { rig.headRenderers.Remove(g.GetComponent<Renderer>()); Compat.Kill(g); }
            rig.headRenderers.RemoveAll(r => r == null);
            rig.gearVisuals.Clear();
            if (helmet != null)
            {
                var g = Attach(Helmet(helmet), rig.head, "Helmet", local);
                rig.gearVisuals.Add(g);
                if (local) rig.headRenderers.Add(g.GetComponent<Renderer>());
            }
            if (vest != null) rig.gearVisuals.Add(Attach(Vest(vest), rig.chest, "Vest", false));
            if (pack != null) rig.gearVisuals.Add(Attach(Pack(pack), rig.chest, "Pack", false));
        }
    }

    /// <summary>A helmet or vest lying in the world. Bring it to your head / chest and let go to put it on.</summary>
    public class ArmorItem : Item
    {
        public ArmorDef ad;
        public override bool SnapGrip => false;

        public static ArmorItem Create(ArmorDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var a = go.AddComponent<ArmorItem>();
            a.def = d; a.ad = d;
            var mb = d.slot == ArmorSlot.Helmet ? ArmorBuilder.Helmet(d) : ArmorBuilder.Vest(d);
            var model = mb.Build("Model", go.transform, ArmorBuilder.Materials);
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            model.transform.localPosition = -mesh.bounds.center;
            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.04f);
            a.localBounds = new Bounds(Vector3.zero, mesh.bounds.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            a.SetupPhysics(d.slot == ArmorSlot.Helmet ? 1.4f : 4f);
            return a;
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead && pl.Gear.TryWear(this, h)) return true;
            return base.TrySlot(h);
        }
    }
}
