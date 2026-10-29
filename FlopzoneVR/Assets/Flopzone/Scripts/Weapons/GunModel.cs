using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Builds a gun's look from its GunDef. Local frame: +Z is the barrel,
    /// +Y is up, the origin is where the hand holds the grip.
    /// </summary>
    public class GunModel : MonoBehaviour
    {
        public GunDef def;
        public Transform muzzle;
        public Transform magwell;
        public Transform scopeEye;
        public Transform ejectPort;
        public Transform pumpPart;
        public Transform ammoAnchor;  // where the ammo counter sits (back of the gun, facing you)
        public Transform visual;    // everything but the mag; recoil kicks this

        public static Material Metal => Prims.Lit(new Color(0.11f, 0.115f, 0.13f), 0.65f, 0.75f);
        public static Material Polymer => Prims.Lit(new Color(0.16f, 0.165f, 0.18f), 0.3f, 0f, Surface.Plastic);
        public static Material Wood => Prims.Lit(new Color(0.45f, 0.27f, 0.13f), 0.35f, 0f, Surface.Wood);
        public static Material Grip => Prims.Lit(new Color(0.12f, 0.12f, 0.13f), 0.15f, 0f, Surface.Rubber);

        public static GunModel Build(GunDef def, Transform parent)
        {
            var root = new GameObject("gun:" + def.name);
            root.transform.SetParent(parent, false);
            var m = root.AddComponent<GunModel>();
            m.def = def;
            var vis = Prims.Empty("visual", root.transform).transform;
            m.visual = vis;
            var accent = Prims.Lit(def.accent, 0.55f, 0.2f);
            bool wooden = def.id == "boom" || def.id == "sawed" || def.id == "elephant" || def.id == "slug" || def.id == "longshot";
            var furniture = wooden ? Wood : Polymer;
            float L = def.bodyLen, H = def.height, W = def.cls == GunClass.Pistol ? 0.028f : 0.036f;
            float bodyY = 0.035f + H * 0.5f;
            float back = -L * 0.3f, front = L * 0.7f;

            // grip
            Prims.Box(vis, new Vector3(0, -0.02f, -0.012f), new Vector3(W * 0.95f, 0.11f, 0.042f), wooden ? furniture : Grip, false, Quaternion.Euler(14, 0, 0));
            for (int i = 0; i < 4; i++)
                Prims.Box(vis, new Vector3(0, -0.055f + i * 0.022f, -0.035f + i * 0.0055f), new Vector3(W * 0.98f, 0.004f, 0.01f), Polymer, false, Quaternion.Euler(14, 0, 0));
            // receiver / slide
            Prims.Box(vis, new Vector3(0, bodyY, (back + front) * 0.5f), new Vector3(W, H, L), Metal, false);
            Prims.Box(vis, new Vector3(0, bodyY + H * 0.5f + 0.004f, (back + front) * 0.5f), new Vector3(W * 0.6f, 0.008f, L * 0.92f), accent, false);
            Prims.Box(vis, new Vector3(W * 0.51f, bodyY, (back + front) * 0.5f + 0.01f), new Vector3(0.003f, H * 0.35f, L * 0.6f), accent, false);
            // trigger + guard
            Prims.Box(vis, new Vector3(0, 0.012f, 0.03f), new Vector3(0.006f, 0.025f, 0.008f), Metal, false, Quaternion.Euler(-15, 0, 0));
            Prims.Box(vis, new Vector3(0, -0.006f, 0.035f), new Vector3(W * 0.5f, 0.006f, 0.06f), Metal, false);

            // barrel(s)
            float barrelY = bodyY + H * 0.12f;
            float bd = def.cls == GunClass.Shotgun ? 0.026f : def.cls == GunClass.Sniper ? 0.02f : 0.018f;
            if (def.twinBarrel)
            {
                Prims.Cyl(vis, new Vector3(-0.012f, barrelY, front + def.barrelLen * 0.5f), bd, def.barrelLen, Metal, Vector3.forward);
                Prims.Cyl(vis, new Vector3(0.012f, barrelY, front + def.barrelLen * 0.5f), bd, def.barrelLen, Metal, Vector3.forward);
            }
            else if (def.barrelLen > 0.001f)
            {
                Prims.Cyl(vis, new Vector3(0, barrelY, front + def.barrelLen * 0.5f), bd, def.barrelLen, Metal, Vector3.forward);
                // muzzle brake / tip
                Prims.Cyl(vis, new Vector3(0, barrelY, front + def.barrelLen - 0.01f), bd * 1.45f, 0.028f, def.cls == GunClass.Sniper ? Metal : accent, Vector3.forward);
            }
            m.muzzle = Prims.Empty("muzzle", root.transform, new Vector3(0, barrelY, front + def.barrelLen + 0.01f)).transform;

            // pump / handguard
            if (def.cls == GunClass.Shotgun || def.cls == GunClass.AssaultRifle || def.cls == GunClass.Sniper)
            {
                float guardLen = Mathf.Max(0.12f, def.barrelLen * 0.55f);
                var guard = Prims.Box(vis, new Vector3(0, barrelY - 0.022f, front + guardLen * 0.5f - 0.01f), new Vector3(W * 1.1f, 0.035f, guardLen), def.pump ? furniture : Polymer, false);
                if (def.pump) m.pumpPart = guard.transform;
                for (int i = 0; i < 3 && def.cls == GunClass.AssaultRifle; i++)
                    Prims.Box(vis, new Vector3(0, barrelY - 0.042f, front + 0.02f + i * 0.04f), new Vector3(W * 0.9f, 0.006f, 0.012f), accent, false);
            }

            // stock
            if (def.stockLen > 0.01f)
            {
                Prims.Box(vis, new Vector3(0, bodyY - 0.01f, back - def.stockLen * 0.5f), new Vector3(W * 0.85f, H * 0.8f, def.stockLen), furniture, false, Quaternion.Euler(-4, 0, 0));
                Prims.Box(vis, new Vector3(0, bodyY - 0.02f, back - def.stockLen + 0.01f), new Vector3(W * 1.05f, H * 1.25f, 0.025f), Polymer, false);
            }

            // revolver cylinder
            if (def.revolver)
                Prims.Cyl(vis, new Vector3(0, bodyY, 0.035f), 0.05f, 0.05f, accent, Vector3.forward);

            // sights / scope
            if (def.scope)
            {
                float sy = bodyY + H * 0.5f + 0.035f;
                Prims.Box(vis, new Vector3(0, sy - 0.024f, (back + front) * 0.5f - 0.04f), new Vector3(0.012f, 0.02f, 0.012f), Metal, false);
                Prims.Box(vis, new Vector3(0, sy - 0.024f, (back + front) * 0.5f + 0.08f), new Vector3(0.012f, 0.02f, 0.012f), Metal, false);
                Prims.Cyl(vis, new Vector3(0, sy, (back + front) * 0.5f + 0.02f), 0.038f, 0.22f, Metal, Vector3.forward);
                Prims.Cyl(vis, new Vector3(0, sy, (back + front) * 0.5f + 0.13f), 0.05f, 0.03f, accent, Vector3.forward);
                Prims.Cyl(vis, new Vector3(0, sy, (back + front) * 0.5f - 0.09f), 0.046f, 0.03f, accent, Vector3.forward);
                m.scopeEye = Prims.Empty("scopeEye", root.transform, new Vector3(0, sy, (back + front) * 0.5f - 0.106f)).transform;
            }
            else
            {
                float sy = bodyY + H * 0.5f + 0.008f;
                Prims.Box(vis, new Vector3(0, sy, front - 0.01f), new Vector3(0.004f, 0.012f, 0.006f), Metal, false);
                Prims.Box(vis, new Vector3(0, sy, back + 0.02f), new Vector3(0.014f, 0.012f, 0.006f), Metal, false);
                if (def.cls == GunClass.AssaultRifle)
                {
                    // carry rail + red-dot box
                    Prims.Box(vis, new Vector3(0, sy + 0.012f, (back + front) * 0.5f), new Vector3(0.026f, 0.022f, 0.06f), Metal, false);
                    Prims.Box(vis, new Vector3(0, sy + 0.016f, (back + front) * 0.5f - 0.031f), new Vector3(0.02f, 0.014f, 0.002f), Prims.Emissive(new Color(1f, 0.2f, 0.2f), 2f), false);
                }
            }

            // mag well: the mag slots up into the grip (pistols) or into the bottom of the receiver,
            // and hangs off the moving part of the gun so it kicks with the recoil
            if (def.cls == GunClass.Pistol)
            {
                m.magwell = Prims.Empty("magwell", vis, new Vector3(0, 0.025f, -0.002f)).transform;
                m.magwell.localRotation = Quaternion.Euler(14, 0, 0);
            }
            else
            {
                float wellY = bodyY - H * 0.5f;
                Prims.Box(vis, new Vector3(0, wellY - 0.012f, L * 0.22f), new Vector3(W * 0.92f, 0.03f, 0.07f), Metal, false);
                m.magwell = Prims.Empty("magwell", vis, new Vector3(0, wellY - 0.004f, L * 0.22f)).transform;
            }

            // detail: ejection port, charging / bolt handle, rail, front sight, sling swivels
            Prims.Box(vis, new Vector3(W * 0.505f, bodyY + H * 0.12f, (back + front) * 0.5f + 0.02f), new Vector3(0.004f, H * 0.35f, L * 0.18f), Prims.Lit(new Color(0.03f, 0.03f, 0.035f), 0.2f), false);
            if (def.mode == FireMode.Bolt)
            {
                Prims.Cyl(vis, new Vector3(W * 0.5f + 0.03f, bodyY + H * 0.2f, back + L * 0.25f), 0.01f, 0.06f, Metal, Vector3.right);
                Prims.Sphere(vis, new Vector3(W * 0.5f + 0.062f, bodyY + H * 0.2f, back + L * 0.25f), 0.022f, Metal);
            }
            else if (def.cls != GunClass.Pistol)
                Prims.Box(vis, new Vector3(-W * 0.5f - 0.008f, bodyY + H * 0.25f, back + L * 0.3f), new Vector3(0.016f, 0.012f, 0.03f), Metal, false);
            else
            {
                // slide serrations
                for (int i = 0; i < 5; i++)
                    Prims.Box(vis, new Vector3(0, bodyY + H * 0.1f, back + 0.012f + i * 0.007f), new Vector3(W * 1.02f, H * 0.6f, 0.002f), Prims.Lit(new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.6f), false);
            }
            if (def.cls == GunClass.AssaultRifle || def.cls == GunClass.Sniper)
            {
                int teeth = Mathf.RoundToInt(L * 0.8f / 0.016f);
                for (int i = 0; i < teeth; i++)
                    Prims.Box(vis, new Vector3(0, bodyY + H * 0.5f + 0.011f, back + L * 0.1f + i * 0.016f), new Vector3(W * 0.62f, 0.004f, 0.007f), Metal, false);
            }
            if (!def.scope && def.barrelLen > 0.05f)
                Prims.Box(vis, new Vector3(0, barrelY + bd * 0.5f + 0.012f, front + def.barrelLen - 0.03f), new Vector3(0.004f, 0.024f, 0.01f), Metal, false);
            if (def.stockLen > 0.01f)
                Prims.Cyl(vis, new Vector3(-W * 0.5f - 0.004f, bodyY - 0.01f, back - def.stockLen * 0.7f), 0.012f, 0.006f, Metal, Vector3.right);

            m.ammoAnchor = Prims.Empty("ammo", vis, new Vector3(W * 0.5f + 0.012f, bodyY + H * 0.25f, back + 0.02f)).transform;
            m.ammoAnchor.localRotation = Quaternion.Euler(0, -60, 0);
            m.ejectPort = Prims.Empty("eject", root.transform, new Vector3(W * 0.6f, bodyY + 0.01f, (back + front) * 0.5f)).transform;
            CombineParts(vis);
            return m;
        }

        /// <summary>
        /// Merge the gun's many little parts into one mesh per material, so a
        /// detailed gun costs a handful of draw calls instead of fifty.
        /// </summary>
        static void CombineParts(Transform vis)
        {
            var groups = new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
            var done = new System.Collections.Generic.List<GameObject>();
            var toLocal = vis.worldToLocalMatrix;
            foreach (var mf in vis.GetComponentsInChildren<MeshFilter>())
            {
                var go = mf.gameObject;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || go.transform.childCount > 0 || go.GetComponents<Component>().Length != 3) continue;
                if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new System.Collections.Generic.List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * go.transform.localToWorldMatrix });
                done.Add(go);
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "fz_gunpart" };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var part = new GameObject("parts");
                part.transform.SetParent(vis, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            foreach (var go in done) DestroyImmediate(go);
        }

        /// <summary>The detachable magazine for this gun. Its origin sits on the gun's magwell.</summary>
        public static GameObject BuildMag(GunDef def, Transform parent)
        {
            var go = new GameObject("mag:" + def.id);
            go.transform.SetParent(parent, false);
            var accent = Prims.Lit(def.accent * 0.8f, 0.4f, 0.1f);
            var body = def.cls == GunClass.Shotgun ? Prims.Lit(new Color(0.7f, 0.12f, 0.1f), 0.3f) : Prims.Lit(new Color(0.09f, 0.09f, 0.1f), 0.4f, 0.5f);
            if (def.drum)
            {
                Prims.Cyl(go.transform, new Vector3(0, -0.06f, 0), 0.1f, 0.04f, body, Vector3.right);
                Prims.Box(go.transform, new Vector3(0, -0.012f, 0), new Vector3(0.024f, 0.03f, 0.03f), body, false);
                Prims.Cyl(go.transform, new Vector3(0.022f, -0.06f, 0), 0.04f, 0.005f, accent, Vector3.right);
            }
            else
            {
                float len = def.cls == GunClass.Pistol ? 0.085f : def.cls == GunClass.Shotgun ? 0.07f : def.cls == GunClass.Sniper ? 0.07f : 0.12f;
                if (def.longMag) len *= 1.5f;
                float depth = def.cls == GunClass.Pistol ? 0.03f : def.cls == GunClass.Shotgun ? 0.07f : 0.05f;
                float tilt = def.cls == GunClass.AssaultRifle ? 12f : 0f;
                Prims.Box(go.transform, new Vector3(0, -len * 0.5f, len * 0.08f * (tilt > 0 ? 1 : 0)), new Vector3(0.024f, len, depth), body, false, Quaternion.Euler(tilt, 0, 0));
                Prims.Box(go.transform, new Vector3(0, -len - 0.004f, len * 0.16f * (tilt > 0 ? 1 : 0)), new Vector3(0.028f, 0.01f, depth + 0.006f), accent, false, Quaternion.Euler(tilt, 0, 0));
                if (def.cls != GunClass.Shotgun)
                    Prims.Box(go.transform, new Vector3(0, -0.004f, 0), new Vector3(0.016f, 0.008f, depth * 0.7f), Prims.Lit(new Color(0.85f, 0.65f, 0.2f), 0.7f, 0.9f), false);
            }
            return go;
        }
    }
}
