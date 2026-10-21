using System.IO;
using System.Linq;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Nullbound.EditorTools
{
    /// <summary>The Training Grounds arena (centred on the world origin).</summary>
    public static partial class ArenaBuilder
    {
        // ================================================================== the Training Grounds arena

        const float Half = 45f; // the fighting floor is 90 m across

        static void BuildTrainingGrounds()
        {
            var a = Group("TrainingGrounds", null, Vector3.zero);
            var scenery = Group("Scenery", a, Vector3.zero);
            int nextId = 0;

            // Floor, with a big ground slab under it in case anything falls.
            Prim(PrimitiveType.Cube, "Floor", scenery, new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f + 2f, 1f, Half * 2f + 2f), floor);
            Prim(PrimitiveType.Cube, "Outside", scenery, new Vector3(0f, -1.2f, 0f), new Vector3(240f, 1f, 240f), Mat("Outside", new Color(0.28f, 0.24f, 0.26f), 0.1f));

            // Knee-high walls round the fighting floor (with a tall invisible barrier), inside the test hall's walls.
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 1.25f, Half + 0.5f), new Vector3(Half * 2f + 2f, 2.5f, 1f)), (new Vector3(0f, 1.25f, -Half - 0.5f), new Vector3(Half * 2f + 2f, 2.5f, 1f)),
                (new Vector3(Half + 0.5f, 1.25f, 0f), new Vector3(1f, 2.5f, Half * 2f + 2f)), (new Vector3(-Half - 0.5f, 1.25f, 0f), new Vector3(1f, 2.5f, Half * 2f + 2f)),
            })
            {
                Prim(PrimitiveType.Cube, "Wall", scenery, pos, size, labPanelDark);
                Prim(PrimitiveType.Cube, "WallCap", scenery, pos + Vector3.up * 1.3f, new Vector3(size.x + 0.04f, 0.1f, size.z + 0.04f), hazard, false);
                var barrier = Prim(PrimitiveType.Cube, "Barrier", scenery, pos + Vector3.up * 14f, new Vector3(size.x, 26f, size.z), wall);
                Object.DestroyImmediate(barrier.GetComponent<Renderer>());
                Object.DestroyImmediate(barrier.GetComponent<MeshFilter>());
            }

            // The test hall round it all: walls, roof, lights and floor paint (ArenaBuilder.Hangar.cs).
            BuildHall(scenery);

            // The sparring ring in the middle: a low stage (you can step up on it), corner posts and glowing ropes.
            var ring = Group("SparringRing", scenery, Vector3.zero);
            const float r = 7f;
            Prim(PrimitiveType.Cube, "Stage", ring, new Vector3(0f, 0.15f, 0f), new Vector3(r * 2f, 0.3f, r * 2f), ringMat);
            Prim(PrimitiveType.Cube, "StageEdge", ring, new Vector3(0f, 0.16f, 0f), new Vector3(r * 2f + 0.15f, 0.26f, r * 2f + 0.15f), dark);
            Prim(PrimitiveType.Cylinder, "Logo", ring, new Vector3(0f, 0.305f, 0f), new Vector3(4.5f, 0.005f, 4.5f), neonRed, false);
            Prim(PrimitiveType.Cylinder, "LogoInner", ring, new Vector3(0f, 0.31f, 0f), new Vector3(4.1f, 0.005f, 4.1f), ringMat, false);
            Text(ring, "LogoText", "<b>NULLBOUND</b>", new Vector3(0f, 0.32f, 0f), Quaternion.Euler(90f, 0f, 0f), 9f, new Color(1f, 0.4f, 0.3f), new Vector2(4f, 1f));
            var posts = new[] { new Vector3(-r, 0f, -r), new Vector3(r, 0f, -r), new Vector3(r, 0f, r), new Vector3(-r, 0f, r) };
            for (int i = 0; i < 4; i++)
            {
                Prim(PrimitiveType.Cylinder, "Post", ring, posts[i] + Vector3.up * 1.0f, new Vector3(0.22f, 0.85f, 0.22f), i % 2 == 0 ? padRed : padBlue);
                Vector3 next = posts[(i + 1) % 4];
                for (int k = 0; k < 3; k++)
                {
                    float h = 0.75f + k * 0.42f;
                    var rp = Prim(PrimitiveType.Cylinder, "Rope", ring, Vector3.zero, Vector3.one, k == 1 ? Mat("RopeWhite", Color.white, 0.3f) : rope, false);
                    Vector3 p0 = posts[i] + Vector3.up * h, p1 = next + Vector3.up * h;
                    rp.transform.localPosition = (p0 + p1) * 0.5f;
                    rp.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (p1 - p0).normalized);
                    rp.transform.localScale = new Vector3(0.05f, (p1 - p0).magnitude * 0.5f, 0.05f);
                }
            }

            // Training dummies along the north side.
            var dummies = Group("Dummies", a, new Vector3(0f, 0f, 30f));
            for (int i = 0; i < 7; i++) Dummy(dummies, new Vector3((i - 3) * 3.4f, 0f, 0f));

            // Breakable stone pillars round the ring.
            var breakables = Group("Breakables", a, Vector3.zero);
            for (int pi = 0; pi < 8; pi++)
            {
                float pa = (pi + 0.5f) * Mathf.PI * 2f / 8f;
                var p = new Vector3(Mathf.Cos(pa) * 15f, 0f, Mathf.Sin(pa) * 15f);
                var pillar = Prim(PrimitiveType.Cylinder, "Pillar", breakables, p + Vector3.up * 2f, new Vector3(1.4f, 2f, 1.4f), stone);
                MakeBreakable(pillar, nextId++, 70f, stone);
                Prim(PrimitiveType.Cylinder, "PillarBase", scenery, p + Vector3.up * 0.1f, new Vector3(1.9f, 0.1f, 1.9f), dark);
            }
            // Crate stacks in the corners.
            foreach (var c in new[] { new Vector3(-24f, 0f, -6f), new Vector3(24f, 0f, -6f), new Vector3(-8f, 0f, -22f), new Vector3(8f, 0f, 22f), new Vector3(-36f, 0f, 2f), new Vector3(36f, 0f, 2f) })
            {
                for (int k = 0; k < 4; k++)
                {
                    Vector3 off = k < 3 ? new Vector3((k - 1) * 1.25f, 0.6f, 0f) : new Vector3(0f, 1.8f, 0f);
                    var box = Prim(PrimitiveType.Cube, "Crate", breakables, c + off, Vector3.one * 1.2f, crate, true, Quaternion.Euler(0f, k * 7f, 0f));
                    MakeBreakable(box, nextId++, 25f, crate);
                }
            }
            // A wall to smash through on the south side, built from blocks.
            for (int x = 0; x < 6; x++)
            for (int y = 0; y < 2; y++)
            {
                var block = Prim(PrimitiveType.Cube, "WallBlock", breakables, new Vector3(-7.5f + x * 3f, 0.75f + y * 1.5f, -26f), new Vector3(2.95f, 1.45f, 0.8f), concrete);
                MakeBreakable(block, nextId++, 45f, concrete);
            }

            BuildDowntown(a, breakables, ref nextId);
            BuildPlateau(scenery);
            BuildDojo(a, breakables, ref nextId);

            // KO board (east), how-to board (west), title sign (north).
            var score = Board(a, "KOBoard", new Vector3(Half - 0.2f, 4.2f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(12f, 5f), 3.2f, "KO BOARD", neonGold);
            score.alignment = TextAlignmentOptions.TopLeft;
            score.transform.parent.gameObject.AddComponent<ScoreBoard>().text = score;
            Posts(scenery, new Vector3(Half - 0.2f, 0f, 0f), true);
            Board(a, "HowTo", new Vector3(-Half + 0.2f, 4.2f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(12f, 5f), 2.6f,
                "<b><color=#ff6b4a>HOW TO FIGHT</color></b>\n" +
                "<b>PUNCH:</b> really punch! Faster swings hit harder. 4 hits in a row = LAUNCH\n" +
                "<b>BLOCK:</b> both fists up in front of your face\n" +
                "<b>A</b> jump   <b>B</b> dodge dash (you can't be hit mid-dash)\n" +
                "<b>X</b> fly into your target   <b>Click right stick</b> super   <b>Menu button</b>: Quick Match, Training, fighters, settings\n" +
                "Left stick move, right stick turn. Smash walls, crates and pillars!", neonCyan);
            Posts(scenery, new Vector3(-Half + 0.2f, 0f, 0f), true);
            var title = Group("Title", scenery, new Vector3(0f, 8.6f, Half - 0.3f));
            Prim(PrimitiveType.Cube, "TitleBack", title, new Vector3(0f, 0f, 0.3f), new Vector3(22f, 4f, 0.4f), sign);
            Prim(PrimitiveType.Cube, "TitleGlow", title, new Vector3(0f, -2.05f, 0.1f), new Vector3(22.2f, 0.12f, 0.12f), neonRed, false);
            var titleText = Text(title, "TitleText", "<b>NULLBOUND</b>", new Vector3(0f, 0.35f, 0f), Quaternion.identity, 22f, new Color(1f, 0.45f, 0.3f), new Vector2(21f, 2.6f));
            titleText.enableWordWrapping = false;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMax = 22f;
            titleText.fontSizeMin = 8f;
            Text(title, "Subtitle", "TRAINING GROUNDS", new Vector3(0f, -1.3f, 0f), Quaternion.identity, 8f, Color.white, new Vector2(21f, 1f));
            for (int i = -1; i <= 1; i += 2) Prim(PrimitiveType.Cube, "TitleBracket", title, new Vector3(i * 8f, 0f, 0.65f), new Vector3(0.4f, 3f, 0.4f), metal, false);

            // Spawn points: round the ring and in the open.
            var spawns = Group("SpawnPoints", a, Vector3.zero);
            for (int i = 0; i < 16; i++)
            {
                float ang = i * Mathf.PI * 2f / 16f;
                float d = i % 2 == 0 ? 4.5f : 22f;
                var sp = Group("Spawn" + i, spawns, new Vector3(Mathf.Cos(ang) * d, i % 2 == 0 ? 0.35f : 0.05f, Mathf.Sin(ang) * d));
                sp.gameObject.AddComponent<SpawnPoint>();
            }

            MarkStatic(scenery);
            Probe("HallProbe", a, new Vector3(0f, 6f, 0f), new Vector3(Half * 2f + 2f, 14f, Half * 2f + 2f));
            ArtPass(a);
        }

        // ---------------- extra zones of the bigger arena

        /// <summary>North-east: a little city block with breakable buildings, a car and street lights.</summary>
        static void BuildDowntown(Transform a, Transform breakables, ref int nextId)
        {
            var town = Group("Downtown", a, new Vector3(28f, 0f, 22f));
            var road = asphaltMat;
            var brick = brickMat;
            var glassMat = Mat("Window", new Color(0.4f, 0.6f, 0.8f), 0.9f, 0f, null, null, new Color(0.15f, 0.25f, 0.35f));
            Prim(PrimitiveType.Cube, "Road", town, new Vector3(0f, 0.01f, 0f), new Vector3(30f, 0.02f, 6f), road, false);
            for (int i = -2; i <= 2; i++) Prim(PrimitiveType.Cube, "RoadLine", town, new Vector3(i * 6f, 0.025f, 0f), new Vector3(2.5f, 0.01f, 0.2f), Mat("Line", new Color(0.85f, 0.84f, 0.8f), 0.3f), false);
            foreach (float z in new[] { -3.4f, 3.4f }) Prim(PrimitiveType.Cube, "Kerb", town, new Vector3(0f, 0.08f, z), new Vector3(30f, 0.16f, 0.8f), concretePolished, false);
            // Three buildings made of breakable blocks (smash a hole and walk through).
            for (int b = 0; b < 3; b++)
            {
                Vector3 c = new Vector3(-9f + b * 9f, 0f, 7f);
                var mat = b == 1 ? concrete : brick;
                for (int floorN = 0; floorN < 3; floorN++)
                for (int side = 0; side < 4; side++)
                {
                    Vector3 off = side == 0 ? new Vector3(0f, 0f, -2.5f) : side == 1 ? new Vector3(0f, 0f, 2.5f) : side == 2 ? new Vector3(-2.5f, 0f, 0f) : new Vector3(2.5f, 0f, 0f);
                    Vector3 size = side < 2 ? new Vector3(5.4f, 2f, 0.4f) : new Vector3(0.4f, 2f, 4.6f);
                    var w = Prim(PrimitiveType.Cube, "BuildingWall", breakables, town.position + c + off + Vector3.up * (1f + floorN * 2f), size, mat);
                    MakeBreakable(w, nextId++, 35f, mat);
                    if (side == 0) Prim(PrimitiveType.Cube, "Window", w.transform, new Vector3(0f, 0.1f, -0.55f), new Vector3(0.5f, 0.35f, 0.1f), glassMat, false);
                }
                Prim(PrimitiveType.Cube, "Roof", town, c + Vector3.up * 6.1f, new Vector3(5.6f, 0.2f, 5.6f), dark);
            }
            // A car to throw people into.
            var car = Prim(PrimitiveType.Cube, "Car", breakables, town.position + new Vector3(4f, 0.7f, -0.5f), new Vector3(4f, 1f, 1.9f), Mat("CarPaint", new Color(0.9f, 0.2f, 0.2f), 0.7f, 0.3f));
            MakeBreakable(car, nextId++, 30f, metal);
            Prim(PrimitiveType.Cube, "CarTop", car.transform, new Vector3(-0.05f, 0.85f, 0f), new Vector3(0.55f, 0.7f, 0.9f), glassMat, false);
            foreach (var wpos in new[] { new Vector3(-0.32f, -0.45f, 0.5f), new Vector3(0.32f, -0.45f, 0.5f), new Vector3(-0.32f, -0.45f, -0.5f), new Vector3(0.32f, -0.45f, -0.5f) })
                Prim(PrimitiveType.Cylinder, "Wheel", car.transform, wpos, new Vector3(0.18f, 0.06f, 0.35f), dark, false, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 lp = new Vector3(-12f + i * 8f, 0f, -3.6f);
                Prim(PrimitiveType.Cylinder, "LampPost", town, lp + Vector3.up * 2f, new Vector3(0.12f, 2f, 0.12f), metal);
                Prim(PrimitiveType.Cube, "LampArm", town, lp + new Vector3(0f, 4.05f, 0.35f), new Vector3(0.1f, 0.1f, 0.8f), metal, false);
                Prim(PrimitiveType.Cube, "Lamp", town, lp + new Vector3(0f, 3.98f, 0.7f), new Vector3(0.32f, 0.08f, 0.5f), lightPanel, false);
            }
        }

        /// <summary>South-west: raised rock platforms with ramps up (fight on high ground, knock people off).</summary>
        static void BuildPlateau(Transform scenery)
        {
            var platMat = concreteRaw;
            var plat = Group("Plateau", scenery, new Vector3(-27f, 0f, -24f));
            Prim(PrimitiveType.Cube, "Platform", plat, new Vector3(0f, 1f, 0f), new Vector3(12f, 2f, 12f), platMat);
            Prim(PrimitiveType.Cube, "PlatformTop", plat, new Vector3(0f, 2.02f, 0f), new Vector3(11.6f, 0.04f, 11.6f), ringMat, false);
            Ramp(plat, new Vector3(0f, 0f, 14f), new Vector3(0f, 2f, 6f), 4f, platMat);
            Ramp(plat, new Vector3(14f, 0f, 0f), new Vector3(6f, 2f, 0f), 4f, platMat);
            Prim(PrimitiveType.Cube, "Peak", plat, new Vector3(-3f, 3f, -3f), new Vector3(5f, 2f, 5f), platMat);
            Ramp(plat, new Vector3(-3f, 2f, 4.5f), new Vector3(-3f, 4f, -0.5f), 2.5f, platMat);
            int seed = 31;
            foreach (var r in new[] { new Vector3(8f, 0f, 9f), new Vector3(-8f, 0f, 9f), new Vector3(9f, 0f, -8f) })
            {
                // A boulder to hide behind: chunky rock mesh, the same round collider as before.
                var b = Prim(PrimitiveType.Sphere, "Boulder", plat, r + Vector3.up * 0.8f, new Vector3(2.2f, 1.6f, 2f), rockMat);
                b.GetComponent<MeshFilter>().sharedMesh = RockMesh(seed++, new Vector3(2.2f, 1.6f, 2f));
            }
            // Hazard edging round the top, so the drop reads from a distance.
            foreach (var (p, s) in new[] { (new Vector3(0f, 2.06f, 5.95f), new Vector3(12f, 0.12f, 0.1f)), (new Vector3(0f, 2.06f, -5.95f), new Vector3(12f, 0.12f, 0.1f)),
                (new Vector3(5.95f, 2.06f, 0f), new Vector3(0.1f, 0.12f, 12f)), (new Vector3(-5.95f, 2.06f, 0f), new Vector3(0.1f, 0.12f, 12f)) })
                Prim(PrimitiveType.Cube, "Edge", plat, p, s, hazard, false);
        }

        /// <summary>North-west: a dojo with breakable paper-screen walls.</summary>
        static void BuildDojo(Transform a, Transform breakables, ref int nextId)
        {
            var dojo = Group("Dojo", a, new Vector3(-28f, 0f, 22f));
            var woodMat = woodPlanksMat;
            var paper = Mat("Paper", new Color(0.95f, 0.92f, 0.82f), 0.1f, 0f, null, null, new Color(0.25f, 0.22f, 0.15f));
            Prim(PrimitiveType.Cube, "DojoFloor", dojo, new Vector3(0f, 0.15f, 0f), new Vector3(14f, 0.3f, 12f), woodMat);
            foreach (var c in new[] { new Vector3(-7f, 0f, -6f), new Vector3(7f, 0f, -6f), new Vector3(-7f, 0f, 6f), new Vector3(7f, 0f, 6f) })
                Prim(PrimitiveType.Cylinder, "Pillar", dojo, c + Vector3.up * 2.2f, new Vector3(0.5f, 2f, 0.5f), padRed);
            var roof = Prim(PrimitiveType.Cube, "Roof", dojo, new Vector3(0f, 4.4f, 0f), new Vector3(16f, 0.4f, 14f), dark);
            Prim(PrimitiveType.Cube, "RoofTop", roof.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 2f, 0.7f), dark, false, Quaternion.Euler(0f, 45f, 0f));
            // Paper screens on three sides (front left open), all breakable.
            for (int i = 0; i < 4; i++)
            {
                var back = Prim(PrimitiveType.Cube, "Screen", breakables, dojo.position + new Vector3(-5.25f + i * 3.5f, 1.9f, 6f), new Vector3(3.4f, 3.2f, 0.12f), paper);
                MakeBreakable(back, nextId++, 15f, paper);
            }
            for (int i = 0; i < 3; i++)
                foreach (float x in new[] { -7f, 7f })
                {
                    var side = Prim(PrimitiveType.Cube, "Screen", breakables, dojo.position + new Vector3(x, 1.9f, -4f + i * 4f), new Vector3(0.12f, 3.2f, 3.9f), paper);
                    MakeBreakable(side, nextId++, 15f, paper);
                }
            Text(dojo, "DojoSign", "<b>DOJO</b>", new Vector3(0f, 3.8f, -6.1f), Quaternion.identity, 8f, new Color(1f, 0.4f, 0.3f), new Vector2(6f, 1.2f));
        }


        static void Dummy(Transform parent, Vector3 pos)
        {
            var d = Group("Dummy", parent, pos);
            Prim(PrimitiveType.Cylinder, "Base", d, new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.1f, 0.9f), dark);
            Prim(PrimitiveType.Cylinder, "Pole", d, new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 0.4f, 0.12f), metal, false);
            var pivot = Group("Pivot", d, new Vector3(0f, 0.85f, 0f));
            var bag = Prim(PrimitiveType.Capsule, "Bag", pivot, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.55f, 0.55f), bagMat, false);
            Prim(PrimitiveType.Sphere, "Head", pivot, new Vector3(0f, 1.25f, 0f), Vector3.one * 0.34f, bagMat, false);
            Prim(PrimitiveType.Cube, "Band", pivot, new Vector3(0f, 0.75f, 0f), new Vector3(0.57f, 0.08f, 0.57f), neonGold, false);
            var hit = new GameObject("Hurtbox");
            hit.transform.SetParent(pivot, false);
            hit.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var cap = hit.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = 0.32f;
            cap.height = 1.5f;
            var dummy = d.gameObject.AddComponent<TrainingDummy>();
            dummy.bag = pivot;
            hit.AddComponent<Hurtbox>().owner = dummy;
            dummy.readout = Text(d, "Readout", "PUNCH ME", new Vector3(0f, 2.75f, 0f), Quaternion.identity, 2f, Color.white, new Vector2(2.6f, 0.8f));
            _ = bag;
        }
    }
}
