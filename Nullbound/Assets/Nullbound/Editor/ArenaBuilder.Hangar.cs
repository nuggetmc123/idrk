using System.Collections.Generic;
using Nullbound.Art;
using UnityEngine;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// The test hall round the Training Grounds: tall panelled walls with steel pilasters, light strips, pipes, vents,
    /// stencilled sector names, a bay door and observation windows, and a roof on steel trusses with hanging light
    /// panels. Pure decoration: nothing here has a collider (the gameplay walls and barriers stay as they were).
    /// </summary>
    public static partial class ArenaBuilder
    {
        const float HallHeight = 28f;   // just above the invisible barrier, so nothing sees out
        const float HallInner = Half + 1f; // the walls' inside faces (behind the knee-high gameplay walls)

        static readonly Color StencilColour = new Color(0.17f, 0.18f, 0.2f, 0.88f);

        static void BuildHall(Transform parent)
        {
            var hall = Group("Hall", parent, Vector3.zero);
            var sides = new[]
            {
                ("North", new Vector3(0f, 0f, HallInner), Quaternion.identity, "SECTOR 01"),
                ("East", new Vector3(HallInner, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), "SECTOR 02"),
                ("South", new Vector3(0f, 0f, -HallInner), Quaternion.Euler(0f, 180f, 0f), "SECTOR 03"),
                ("West", new Vector3(-HallInner, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), "SECTOR 04"),
            };
            foreach (var (name, pos, rot, label) in sides) HallWall(Group("Wall" + name, hall, pos, rot), name, label);
            HallRoof(Group("Roof", hall, Vector3.zero));
            HallFloorMarkings(Group("FloorMarkings", hall, Vector3.zero));
            NoShadow(hall.gameObject); // the hall is lit from its own light panels (the sun is only the key light)
        }

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// One wall, built in its own frame: local +X runs along the wall (left to right seen from inside), +Z points
        /// out of the hall and the inside face is at z = 0.
        /// </summary>
        static void HallWall(Transform w, string side, string label)
        {
            const float len = Half * 2f + 3.2f; // overlaps the neighbours at the corners
            const float H = HallHeight;
            bool door = side == "South", windows = side == "East" || side == "West";

            Block("Panels", w, new Vector3(0f, H * 0.5f, 0.3f), new Vector3(len, H, 0.6f), labPanel, null, 0.01f);
            // A dark plinth band at the bottom, an orange band up high (the hall's signature stripe).
            Block("Plinth", w, new Vector3(0f, 1.75f, -0.04f), new Vector3(len, 3.5f, 0.08f), labPanelDark, null, 0.01f);
            Block("PlinthCap", w, new Vector3(0f, 3.53f, -0.07f), new Vector3(len, 0.1f, 0.14f), gunmetal);
            Block("OrangeBand", w, new Vector3(0f, 21.1f, -0.03f), new Vector3(len, 0.9f, 0.06f), orangePaint, null, 0.005f);
            Block("WhiteLine", w, new Vector3(0f, 20.45f, -0.03f), new Vector3(len, 0.12f, 0.06f), labPanelDark, null, 0.005f);

            // Steel beams: a ledge at 12 m (with a light strip under it) and the eaves at the top.
            Block("Beam", w, new Vector3(0f, 12.3f, -0.35f), new Vector3(len, 0.7f, 0.7f), gunmetal, null, 0.03f);
            Block("BeamLight", w, new Vector3(0f, 11.86f, -0.45f), new Vector3(len - 1f, 0.12f, 0.36f), lightPanel, null, 0.01f);
            Block("Eaves", w, new Vector3(0f, H - 0.5f, -0.4f), new Vector3(len, 1f, 0.8f), gunmetal, null, 0.03f);

            // Pilasters every 10 m, each with a light strip up its face below the beam.
            for (int i = -4; i <= 4; i++)
            {
                float x = i * 10f;
                if (door && Mathf.Abs(x) < 13f) continue;
                // The middle one stops under the observation window.
                float top = windows && i == 0 ? 14.1f : H;
                Block("Pilaster", w, new Vector3(x, (3.6f + top) * 0.5f, -0.35f), new Vector3(1.1f, top - 3.6f, 0.7f), gunmetal, null, 0.03f);
                Block("PilasterBase", w, new Vector3(x, 1.75f, -0.3f), new Vector3(1.4f, 3.5f, 0.6f), labPanelDark, null, 0.02f);
                Block("PilasterLight", w, new Vector3(x, 7.6f, -0.72f), new Vector3(0.18f, 7.2f, 0.06f), lightPanel, null, 0.01f);
                // Pipe clamps where the pipes cross.
                if (!door || Mathf.Abs(x) > 13f) Block("Clamp", w, new Vector3(x, 13.1f, -0.85f), new Vector3(0.3f, 0.75f, 0.4f), gunmetal);
            }

            // Two pipes resting on the beam.
            foreach (var (y, d) in new[] { (12.92f, 0.26f), (13.32f, 0.18f) })
                Cyl("Pipe", w, new Vector3(0f, y, -0.95f), d, len - 1f, brushedSteel, Quaternion.Euler(0f, 0f, 90f));

            // Vents low on the wall, between pilasters.
            foreach (float x in new[] { -35f, -15f, 15f, 35f })
            {
                if (door && Mathf.Abs(x) < 16f) continue;
                Block("VentFrame", w, new Vector3(x, 5.4f, -0.05f), new Vector3(3.4f, 1.9f, 0.1f), gunmetal, null, 0.02f);
                Block("Vent", w, new Vector3(x, 5.4f, -0.08f), new Vector3(3f, 1.5f, 0.06f), grate, null, 0.005f);
            }

            // Stencilled names in the bays either side of the middle: the sector number big, the hall code smaller.
            string[] words = label.Split(' ');
            Stencil(w, "SectorWord", "<b>" + words[0] + "</b>", new Vector3(-25f, 19.4f, -0.08f), 18f, new Vector2(8.4f, 1.6f));
            Stencil(w, "SectorNumber", "<b>" + words[1] + "</b>", new Vector3(-25f, 16.2f, -0.08f), 60f, new Vector2(8.4f, 4.6f));
            Stencil(w, "HallCode", "<b>NB-CTF</b>", new Vector3(25f, 18.6f, -0.08f), 30f, new Vector2(8.4f, 2.6f));
            Stencil(w, "HallName", "TEST HALL A", new Vector3(25f, 16.6f, -0.08f), 12f, new Vector2(8.4f, 1.1f));
            Stencil(w, "Warning", "KEEP CLEAR DURING ACTIVE TRIALS", new Vector3(25f, 15.4f, -0.08f), 5f, new Vector2(8.4f, 0.6f));

            if (door) BayDoor(Group("BayDoor", w, Vector3.zero));
            if (windows) ObservationWindow(Group("Observation", w, Vector3.zero));
        }

        /// <summary>A big closed hangar door (24 x 12 m) with hazard-striped jambs, under the beam.</summary>
        static void BayDoor(Transform d)
        {
            const float W = 24f, Ht = 11.95f;
            Block("Leaf", d, new Vector3(0f, Ht * 0.5f, -0.12f), new Vector3(W, Ht, 0.2f), labPanelDark, null, 0.02f);
            for (int k = 1; k < 12; k++)
                Block("Rib", d, new Vector3(0f, k * 1f, -0.26f), new Vector3(W - 0.2f, 0.12f, 0.1f), gunmetal, null, 0.01f);
            Block("Split", d, new Vector3(0f, Ht * 0.5f, -0.3f), new Vector3(0.12f, Ht, 0.12f), gunmetal);
            foreach (float s in new[] { -1f, 1f })
                Block("Jamb", d, new Vector3(s * (W * 0.5f + 0.5f), Ht * 0.5f, -0.35f), new Vector3(1f, Ht, 0.7f), hazard, null, 0.03f);
            Block("DoorPanelSign", d, new Vector3(0f, 8f, -0.34f), new Vector3(8f, 1.6f, 0.04f), orangePaint, null, 0.005f);
            Stencil(d, "DoorText", "<b>BAY 03</b>", new Vector3(0f, 8f, -0.37f), 14f, new Vector2(7.6f, 1.5f));
            Block("DoorLamp", d, new Vector3(W * 0.5f + 2f, 10.4f, -0.2f), new Vector3(0.6f, 0.6f, 0.3f), neonRed, null, 0.05f);
            Block("DoorLamp", d, new Vector3(-W * 0.5f - 2f, 10.4f, -0.2f), new Vector3(0.6f, 0.6f, 0.3f), neonRed, null, 0.05f);
        }

        /// <summary>A control room looking down on the hall: framed glass, a dark room behind with screens and a light.</summary>
        static void ObservationWindow(Transform o)
        {
            const float W = 18f, y0 = 14.4f, Ht = 4.6f;
            float yc = y0 + Ht * 0.5f;
            Block("Room", o, new Vector3(0f, yc, -0.06f), new Vector3(W, Ht, 0.1f), labPanelDark, null, 0.005f);
            Block("RoomLight", o, new Vector3(0f, y0 + Ht - 0.35f, -0.13f), new Vector3(W - 1f, 0.12f, 0.04f), lightPanel, null, 0.005f);
            for (int i = 0; i < 4; i++)
                Block("Monitor", o, new Vector3(-6.75f + i * 4.5f, y0 + 1.1f, -0.15f), new Vector3(1.6f, 0.9f, 0.04f), screen, null, 0.005f);
            Block("Desk", o, new Vector3(0f, y0 + 0.45f, -0.25f), new Vector3(W - 0.6f, 0.9f, 0.3f), gunmetal, null, 0.02f);
            Block("Glass", o, new Vector3(0f, yc, -0.45f), new Vector3(W, Ht, 0.03f), glass, null, 0f);
            Block("FrameTop", o, new Vector3(0f, y0 + Ht + 0.15f, -0.45f), new Vector3(W + 0.6f, 0.3f, 0.5f), gunmetal, null, 0.03f);
            Block("FrameSill", o, new Vector3(0f, y0 - 0.15f, -0.55f), new Vector3(W + 0.6f, 0.3f, 0.7f), gunmetal, null, 0.03f);
            for (int i = 0; i <= 4; i++)
                Block("Mullion", o, new Vector3(-W * 0.5f + i * W / 4f, yc, -0.5f), new Vector3(0.2f, Ht, 0.4f), gunmetal, null, 0.02f);
        }

        /// <summary>Painted lettering on a wall, shrunk to fit `rect` (metres) so it never runs past a pilaster.</summary>
        static void Stencil(Transform parent, string name, string text, Vector3 pos, float size, Vector2 rect)
        {
            var t = Text(parent, name, text, pos, Quaternion.identity, size, StencilColour, rect);
            t.enableWordWrapping = false;
            t.enableAutoSizing = true;
            t.fontSizeMin = 1f;
            t.fontSizeMax = size;
        }

        // ------------------------------------------------------------------ roof

        static void HallRoof(Transform r)
        {
            const float H = HallHeight, span = Half * 2f + 2f;
            Block("Ceiling", r, new Vector3(0f, H + 0.3f, 0f), new Vector3(span + 1.2f, 0.6f, span + 1.2f), labPanelDark, null, 0.01f);

            // Steel trusses across the hall every 10 m: one merged mesh shared by all of them.
            var truss = CachedMesh("Truss_" + Cm(span), () => TrussMesh(span, 3f, 4f));
            for (int i = -4; i <= 4; i++)
            {
                var t = new GameObject("Truss");
                t.transform.SetParent(r, false);
                t.transform.localPosition = new Vector3(0f, H - 1.6f, i * 10f);
                t.AddComponent<MeshFilter>().sharedMesh = truss;
                t.AddComponent<MeshRenderer>().sharedMaterial = gunmetal;
            }
            // Purlins along the hall tying the trusses together.
            foreach (float x in new[] { -30f, -15f, 0f, 15f, 30f })
                Block("Purlin", r, new Vector3(x, H - 0.15f, 0f), new Vector3(0.3f, 0.3f, span), gunmetal, null, 0.02f);

            // Long light fixtures hanging between the trusses: a steel housing with a glowing diffuser underneath.
            for (int row = 0; row < 8; row++)
            {
                float z = -35f + row * 10f;
                foreach (float x in new[] { -28f, 0f, 28f })
                {
                    float y = H - 3.4f;
                    Block("LightHousing", r, new Vector3(x, y + 0.14f, z), new Vector3(14.4f, 0.28f, 2.4f), gunmetal, null, 0.04f);
                    Block("LightPanel", r, new Vector3(x, y - 0.03f, z), new Vector3(14f, 0.08f, 2f), lightPanel, null, 0.01f);
                    foreach (float hx in new[] { -6f, 6f })
                        Block("Hanger", r, new Vector3(x + hx, (y + H - 0.3f) * 0.5f, z), new Vector3(0.06f, H - 0.3f - y, 0.06f), brushedSteel, null, 0f);
                }
            }
        }

        /// <summary>A flat steel truss `length` long and `height` tall along X: chords, posts every `bay` m and diagonals.</summary>
        static MeshData TrussMesh(float length, float height, float bay)
        {
            var d = new MeshData();
            float hy = height * 0.5f - 0.15f;
            AppendBox(d, new Vector3(length, 0.3f, 0.3f), new Vector3(0f, hy, 0f), Quaternion.identity, 0.03f);
            AppendBox(d, new Vector3(length, 0.3f, 0.3f), new Vector3(0f, -hy, 0f), Quaternion.identity, 0.03f);
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / bay));
            float step = length / bays;
            for (int i = 0; i <= bays; i++)
            {
                float x = -length * 0.5f + i * step;
                AppendBox(d, new Vector3(0.18f, hy * 2f, 0.18f), new Vector3(x, 0f, 0f), Quaternion.identity, 0f);
                if (i == bays) continue;
                // Diagonals zig-zag, like a Warren truss.
                float dx = step, dy = hy * 2f;
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg * (i % 2 == 0 ? 1f : -1f);
                AppendBox(d, new Vector3(Mathf.Sqrt(dx * dx + dy * dy), 0.14f, 0.14f), new Vector3(x + step * 0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, angle), 0f);
            }
            return d;
        }

        /// <summary>
        /// Adds a mesh kit box of `size` metres, moved and turned, to a merged mesh (scale 1). The kit stores normals and
        /// tangents for the object scale, so they're converted back here.
        /// </summary>
        static void AppendBox(MeshData into, Vector3 size, Vector3 pos, Quaternion rot, float bevel)
        {
            var part = MeshKit.Box(size, MeshKit.ClampBevel(size, bevel));
            int start = into.vertices.Count;
            for (int i = 0; i < part.vertices.Count; i++)
            {
                Vector3 v = part.vertices[i], n = part.normals[i];
                Vector4 t = part.tangents[i];
                Vector3 nw = rot * new Vector3(n.x / size.x, n.y / size.y, n.z / size.z).normalized;
                Vector3 tw = rot * new Vector3(t.x * size.x, t.y * size.y, t.z * size.z).normalized;
                into.Add(rot * Vector3.Scale(v, size) + pos, nw.normalized, new Vector4(tw.x, tw.y, tw.z, t.w), part.uvs[i]);
            }
            foreach (int idx in part.triangles) into.triangles.Add(start + idx);
        }

        // ------------------------------------------------------------------ floor paint

        static void HallFloorMarkings(Transform f)
        {
            const float y = 0.005f, t = 0.01f;
            // A hazard band along the foot of each wall.
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, y, Half - 0.45f), new Vector3(Half * 2f, t, 0.9f)), (new Vector3(0f, y, -Half + 0.45f), new Vector3(Half * 2f, t, 0.9f)),
                (new Vector3(Half - 0.45f, y, 0f), new Vector3(0.9f, t, Half * 2f - 1.8f)), (new Vector3(-Half + 0.45f, y, 0f), new Vector3(0.9f, t, Half * 2f - 1.8f)),
            })
                Block("HazardBand", f, pos, size, hazard, null, 0f);

            // Painted orange lines marking out the ring zone and the four quarters.
            const float zone = 11.5f, lw = 0.16f;
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, y, zone), new Vector3(zone * 2f + lw, t, lw)), (new Vector3(0f, y, -zone), new Vector3(zone * 2f + lw, t, lw)),
                (new Vector3(zone, y, 0f), new Vector3(lw, t, zone * 2f)), (new Vector3(-zone, y, 0f), new Vector3(lw, t, zone * 2f)),
                (new Vector3(0f, y, (zone + Half) * 0.5f), new Vector3(lw, t, Half - zone - 1f)), (new Vector3(0f, y, -(zone + Half) * 0.5f), new Vector3(lw, t, Half - zone - 1f)),
                (new Vector3((zone + Half) * 0.5f, y, 0f), new Vector3(Half - zone - 1f, t, lw)), (new Vector3(-(zone + Half) * 0.5f, y, 0f), new Vector3(Half - zone - 1f, t, lw)),
            })
                Block("ZoneLine", f, pos, size, orangePaint, null, 0f);

            // Big stencilled zone numbers on the floor, one per quarter.
            var paint = new Color(0.17f, 0.18f, 0.2f, 0.55f);
            string[] names = { "A1", "A2", "A3", "A4" };
            var spots = new[] { new Vector3(-20f, 0.02f, 16f), new Vector3(20f, 0.02f, 16f), new Vector3(20f, 0.02f, -16f), new Vector3(-20f, 0.02f, -16f) };
            for (int i = 0; i < 4; i++)
                Text(f, "ZoneNumber", "<b>" + names[i] + "</b>", spots[i], Quaternion.Euler(90f, 0f, 0f), 48f, paint, new Vector2(8f, 5f)).enableWordWrapping = false;
        }
    }
}
