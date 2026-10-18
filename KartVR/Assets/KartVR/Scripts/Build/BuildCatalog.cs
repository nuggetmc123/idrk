using UnityEngine;

namespace KartVR
{
    /// <summary>Marks a placed build piece so it can be pointed at and deleted.</summary>
    public class BuildPiece : MonoBehaviour
    {
        public int id;
        public int type;
    }

    /// <summary>
    /// Everything you can place in build mode, made from simple shapes at runtime.
    /// Each piece's pivot is the middle of its footprint on the ground, and it faces +Z.
    /// </summary>
    public static class BuildCatalog
    {
        public static readonly string[] Names =
        {
            "Ramp", "Big Ramp", "Kicker", "Road", "Road Corner", "Boost Pad", "Bush", "Tree", "Pine Tree",
            "Wall", "Cone", "Rock", "Quarter Pipe", "Bouncer", "Boost Hoop", "Block",
        };

        public static int Count => Names.Length;

        static Material M(Color c, float smooth = 0.35f) => KartUI.Mat(c, smooth);
        static readonly Color Yellow = new Color(1f, 0.8f, 0.2f), Blue = new Color(0.25f, 0.55f, 1f), Red = new Color(1f, 0.3f, 0.3f),
            Asphalt = new Color(0.2f, 0.2f, 0.22f), White = new Color(0.95f, 0.95f, 0.95f), Leaf = new Color(0.25f, 0.6f, 0.22f),
            LeafDark = new Color(0.16f, 0.45f, 0.18f), Bark = new Color(0.4f, 0.27f, 0.16f), Stone = new Color(0.55f, 0.55f, 0.57f),
            Orange = new Color(1f, 0.5f, 0.05f);

        /// <summary>Build a piece. A ghost has no colliders or gameplay parts and glows, for previewing where it'll go.</summary>
        public static GameObject Make(int type, bool ghost)
        {
            type = Mathf.Clamp(type, 0, Count - 1);
            var root = new GameObject(ghost ? "BuildGhost" : "Built_" + Names[type]).transform;
            switch (type)
            {
                case 0: Ramp(root, 8f, 6f, 10f, M(Yellow)); break;
                case 1: Ramp(root, 14f, 8f, 14f, M(Blue)); break;
                case 2: Ramp(root, 5f, 6f, 15f, M(Red)); break;
                case 3: Road(root, 10f); break;
                case 4: RoadCorner(root); break;
                case 5: MakeBoostPad(root, ghost); break;
                case 6: Bush(root); break;
                case 7: Tree(root); break;
                case 8: Pine(root); break;
                case 9: Wall(root); break;
                case 10: Cone(root); break;
                case 11: Solid(root, PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0f), new Vector3(3.4f, 2.4f, 2.8f), M(Stone, 0.15f)); break;
                case 12: QuarterPipe(root); break;
                case 13: MakeBouncer(root, ghost); break;
                case 14: BoostHoop(root, ghost); break;
                default: Solid(root, PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(6f, 2f, 6f), M(Stone, 0.2f)); break;
            }
            if (ghost) MakeGhost(root.gameObject);
            return root.gameObject;
        }

        /// <summary>A solid primitive with a collider shaped like it (cylinders get a real mesh collider, not a capsule).</summary>
        static GameObject Solid(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default(Vector3))
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (type == PrimitiveType.Cylinder)
            {
                Object.Destroy(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }

        static void Visual(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default(Vector3)) =>
            KartUI.Part(parent, type, pos, scale, mat, euler);

        /// <summary>
        /// A ramp that rises towards +Z: a gentle lip (30% of the length, under half the angle) then the main
        /// slope. The low end sits flush with the ground, so karts roll onto it instead of hitting a step.
        /// </summary>
        static void Ramp(Transform root, float length, float width, float angle, Material mat)
        {
            Vector3 start = new Vector3(0f, 0f, -length * 0.5f);
            foreach (var (len, ang) in new[] { (length * 0.3f, angle * 0.45f), (length * 0.7f, angle) })
            {
                float a = ang * Mathf.Deg2Rad;
                var dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                var up = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
                Solid(root, PrimitiveType.Cube, start + dir * (len * 0.5f) - up * 0.5f, new Vector3(width, 1f, len + 0.02f), mat, new Vector3(-ang, 0f, 0f));
                start += dir * len;
            }
        }

        static void Road(Transform root, float length)
        {
            Solid(root, PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(9f, 0.05f, length), M(Asphalt, 0.3f)); // thin, so it's no bump
            foreach (float x in new[] { -4f, 4f })
                Visual(root, PrimitiveType.Cube, new Vector3(x, 0.055f, 0f), new Vector3(0.25f, 0.01f, length), M(White));
            Visual(root, PrimitiveType.Cube, new Vector3(0f, 0.055f, 0f), new Vector3(0.25f, 0.01f, length * 0.5f), M(Yellow));
        }

        /// <summary>A 90 degree turn to the right, starting at the pivot heading +Z.</summary>
        static void RoadCorner(Transform root)
        {
            const float radius = 14f;
            const int segments = 6;
            var center = new Vector3(radius, 0f, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * 90f / segments, a1 = (i + 1) * 90f / segments, am = (a0 + a1) * 0.5f;
                Vector3 p0 = center + Quaternion.Euler(0f, a0, 0f) * new Vector3(-radius, 0f, 0f);
                Vector3 p1 = center + Quaternion.Euler(0f, a1, 0f) * new Vector3(-radius, 0f, 0f);
                float len = (p1 - p0).magnitude + 0.4f;
                var seg = new GameObject("Segment").transform;
                seg.SetParent(root, false);
                seg.localPosition = (p0 + p1) * 0.5f;
                seg.localRotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
                Road(seg, len);
            }
        }

        static void MakeBoostPad(Transform root, bool ghost)
        {
            var pad = Solid(root, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(4f, 0.06f, 3f), KartUI.Glow(new Color(1f, 0.5f, 0f)));
            pad.GetComponent<Collider>().isTrigger = true;
            if (!ghost) pad.AddComponent<BoostPad>();
            for (int i = 0; i < 2; i++)
                foreach (float s in new[] { -1f, 1f })
                    Visual(root, PrimitiveType.Cube, new Vector3(s * 0.6f, 0.12f, -0.3f + i * 0.9f), new Vector3(1.4f, 0.04f, 0.18f), KartUI.Glow(Color.white),
                        new Vector3(0f, s * 40f, 0f));
        }

        static void Bush(Transform root)
        {
            Solid(root, PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.6f, 2f), M(Leaf, 0.1f));
            Visual(root, PrimitiveType.Sphere, new Vector3(0.8f, 0.55f, 0.3f), new Vector3(1.5f, 1.2f, 1.4f), M(LeafDark, 0.1f));
            Visual(root, PrimitiveType.Sphere, new Vector3(-0.7f, 0.5f, -0.4f), new Vector3(1.4f, 1.1f, 1.3f), M(LeafDark, 0.1f));
        }

        static void Tree(Transform root)
        {
            Solid(root, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.45f, 1.6f, 0.45f), M(Bark, 0.1f));
            Visual(root, PrimitiveType.Sphere, new Vector3(0f, 4f, 0f), Vector3.one * 3.6f, M(Leaf, 0.1f));
            Visual(root, PrimitiveType.Sphere, new Vector3(0.9f, 3.4f, 0.4f), Vector3.one * 2.4f, M(LeafDark, 0.1f));
            Visual(root, PrimitiveType.Sphere, new Vector3(-0.7f, 3.6f, -0.6f), Vector3.one * 2.6f, M(Leaf, 0.1f));
        }

        static void Pine(Transform root)
        {
            Solid(root, PrimitiveType.Cylinder, new Vector3(0f, 1.4f, 0f), new Vector3(0.4f, 1.4f, 0.4f), M(Bark, 0.1f));
            for (int i = 0; i < 3; i++)
                Visual(root, PrimitiveType.Sphere, new Vector3(0f, 3f + i * 1.3f, 0f), new Vector3(3.2f - i * 0.9f, 1.6f, 3.2f - i * 0.9f), M(LeafDark, 0.1f));
        }

        static void Wall(Transform root)
        {
            Solid(root, PrimitiveType.Cube, new Vector3(-2f, 0.6f, 0f), new Vector3(4f, 1.2f, 0.6f), M(Red));
            Solid(root, PrimitiveType.Cube, new Vector3(2f, 0.6f, 0f), new Vector3(4f, 1.2f, 0.6f), M(White));
        }

        static void Cone(Transform root)
        {
            Solid(root, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.5f, 0.4f, 0.5f), M(Orange));
            Visual(root, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.52f, 0.08f, 0.52f), M(White));
            Visual(root, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.04f, 0.8f), M(Orange));
        }

        /// <summary>A curved wall made of tilted panels; you drive up it heading +Z.</summary>
        static void QuarterPipe(Transform root)
        {
            const float radius = 5f, width = 10f;
            const int panels = 7;
            for (int i = 0; i < panels; i++)
            {
                float a0 = i * 80f / panels, a1 = (i + 1) * 80f / panels, am = (a0 + a1) * 0.5f;
                var p = new Vector3(0f, radius - Mathf.Cos(am * Mathf.Deg2Rad) * radius, Mathf.Sin(am * Mathf.Deg2Rad) * radius - radius * 0.5f);
                p -= new Vector3(0f, Mathf.Cos(am * Mathf.Deg2Rad), -Mathf.Sin(am * Mathf.Deg2Rad)) * 0.2f; // top surface on the curve, no step
                float len = 2f * Mathf.PI * radius * (a1 - a0) / 360f + 0.15f;
                Solid(root, PrimitiveType.Cube, p, new Vector3(width, 0.4f, len), M(i % 2 == 0 ? Blue : Red), new Vector3(-am, 0f, 0f));
            }
            Solid(root, PrimitiveType.Cube, new Vector3(0f, radius * 0.95f, radius * 0.5f + 1.5f), new Vector3(width, 0.4f, 3f), M(Stone, 0.2f));
        }

        static void MakeBouncer(Transform root, bool ghost)
        {
            var b = Solid(root, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(2.2f, 0.6f, 2.2f), M(Red));
            if (!ghost) b.AddComponent<Bouncer>();
            Visual(root, PrimitiveType.Cylinder, new Vector3(0f, 1.22f, 0f), new Vector3(0.9f, 0.04f, 0.9f), KartUI.Glow(Color.white));
            Visual(root, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(2.3f, 0.15f, 2.3f), M(White));
        }

        static void BoostHoop(Transform root, bool ghost)
        {
            const float radius = 3.2f, centerY = 4f;
            const int segments = 20;
            float seg = 2f * Mathf.PI * radius / segments * 1.08f;
            var glow = KartUI.Glow(new Color(1f, 0.5f, 0f));
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                var p = Quaternion.Euler(0f, 0f, a) * new Vector3(0f, radius, 0f) + new Vector3(0f, centerY, 0f);
                Visual(root, PrimitiveType.Cube, p, new Vector3(seg, 0.35f, 0.35f), glow, new Vector3(0f, 0f, a));
            }
            // Two thin legs so it doesn't float.
            foreach (float x in new[] { -radius, radius })
                Solid(root, PrimitiveType.Cube, new Vector3(x, (centerY) * 0.5f, 0f), new Vector3(0.25f, centerY, 0.25f), M(Stone, 0.2f));
            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(root, false);
            trigger.transform.localPosition = new Vector3(0f, centerY, 0f);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(radius * 1.7f, radius * 1.7f, 1.2f);
            if (!ghost) trigger.AddComponent<BoostPad>().kick = 6f;
        }

        /// <summary>Strip colliders and make every surface glow see-through-blue, so it reads as a preview.</summary>
        static void MakeGhost(GameObject root)
        {
            var glow = KartUI.Glow(new Color(0.3f, 0.85f, 1f));
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = glow;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2; // Ignore Raycast
        }
    }
}
