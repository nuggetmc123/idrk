using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// The Battleground: the big map for real fights (Quick Match and private lobbies). 300 m across, with a castle
    /// plaza in the middle, three villages of smashable houses, tall towers you can knock down floor by floor, big
    /// rocks (some break), rolling hills, pine trees, dirt roads and two lookout platforms to fight on.
    /// </summary>
    public static partial class ArenaBuilder
    {
        static readonly Vector3 BattleOrigin = new Vector3(0f, 0f, 700f);
        const float BattleHalf = 150f;

        struct Spot { public Vector3 p; public float r; }
        static readonly List<Spot> taken = new List<Spot>();
        static int nextBreakable;

        static bool Free(Vector3 p, float r)
        {
            if (Mathf.Abs(p.x) > BattleHalf - r - 4f || Mathf.Abs(p.z) > BattleHalf - r - 4f) return false;
            foreach (var t in taken) if ((new Vector2(t.p.x - p.x, t.p.z - p.z)).magnitude < t.r + r) return false;
            return true;
        }

        static void Take(Vector3 p, float r) => taken.Add(new Spot { p = p, r = r });

        static void BuildBattleground()
        {
            taken.Clear();
            nextBreakable = Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None).Length;
            var rng = new System.Random(99);
            var b = Group("Battleground", null, BattleOrigin);
            var scenery = Group("Scenery", b, Vector3.zero);
            var smash = Group("Smashables", b, Vector3.zero);

            var grassTex = Tex("Grass", 256, (u, v, r) =>
            {
                float n = Noise(u, v, 10f, 2f);
                float blade = r.NextDouble() < 0.08 ? 0.06f : 0f;
                return new Color(0.26f + n * 0.12f + blade, 0.45f + n * 0.16f + blade, 0.2f + n * 0.06f);
            });
            var grass = Mat("BattleGrass", Color.white, 0.1f, 0f, grassTex, new Vector2(60f, 60f));
            var hillMat = Mat("Hill", Color.white, 0.1f, 0f, grassTex, new Vector2(6f, 6f));
            var dirt = Mat("Dirt", new Color(0.45f, 0.35f, 0.24f), 0.1f);
            var rockMat = ArenaBuilder.rockMat;
            var plaster = new[] { Mat("Plaster1", new Color(0.92f, 0.86f, 0.74f), 0.1f), Mat("Plaster2", new Color(0.8f, 0.72f, 0.6f), 0.1f), Mat("Plaster3", new Color(0.7f, 0.78f, 0.82f), 0.1f) };
            var roofs = new[] { Mat("RoofRed", new Color(0.65f, 0.2f, 0.15f), 0.2f), Mat("RoofBlue", new Color(0.2f, 0.3f, 0.5f), 0.2f), Mat("RoofBrown", new Color(0.4f, 0.26f, 0.16f), 0.2f) };
            var timber = Mat("Timber", new Color(0.35f, 0.22f, 0.12f), 0.2f);
            var towerStone = stoneBlocksMat;
            var leafMat = Mat("Pine", new Color(0.12f, 0.35f, 0.18f), 0.1f);
            var bark = Mat("Bark", new Color(0.3f, 0.2f, 0.12f), 0.1f);

            // Ground and the edge wall.
            Prim(PrimitiveType.Cube, "Ground", scenery, new Vector3(0f, -0.5f, 0f), new Vector3(BattleHalf * 2f + 120f, 1f, BattleHalf * 2f + 120f), grass);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 1.5f, BattleHalf + 1f), new Vector3(BattleHalf * 2f + 4f, 3f, 2f)), (new Vector3(0f, 1.5f, -BattleHalf - 1f), new Vector3(BattleHalf * 2f + 4f, 3f, 2f)),
                (new Vector3(BattleHalf + 1f, 1.5f, 0f), new Vector3(2f, 3f, BattleHalf * 2f + 4f)), (new Vector3(-BattleHalf - 1f, 1.5f, 0f), new Vector3(2f, 3f, BattleHalf * 2f + 4f)),
            })
            {
                Prim(PrimitiveType.Cube, "EdgeWall", scenery, pos, size, towerStone);
                var barrier = Prim(PrimitiveType.Cube, "Barrier", scenery, new Vector3(pos.x, SkyHeight * 0.5f, pos.z), new Vector3(size.x, SkyHeight, size.z), wall);
                Object.DestroyImmediate(barrier.GetComponent<Renderer>());
                Object.DestroyImmediate(barrier.GetComponent<MeshFilter>());
            }
            RockBoundary(scenery, rockMat, rng);

            // The castle plaza in the middle.
            Take(Vector3.zero, 22f);
            Prim(PrimitiveType.Cylinder, "Plaza", scenery, new Vector3(0f, 0.05f, 0f), new Vector3(36f, 0.05f, 36f), Pbr("PlazaStone", "StoneBlocks", new Color(0.9f, 0.88f, 0.85f)));
            Prim(PrimitiveType.Cylinder, "FountainBase", scenery, new Vector3(0f, 0.4f, 0f), new Vector3(6f, 0.4f, 6f), towerStone);
            Prim(PrimitiveType.Cylinder, "FountainWater", scenery, new Vector3(0f, 0.75f, 0f), new Vector3(5.2f, 0.05f, 5.2f), Mat("Water", new Color(0.2f, 0.5f, 0.8f), 0.95f, 0f, null, null, new Color(0.05f, 0.15f, 0.3f)), false);
            Prim(PrimitiveType.Cylinder, "FountainSpire", scenery, new Vector3(0f, 2f, 0f), new Vector3(0.7f, 1.6f, 0.7f), towerStone);
            Prim(PrimitiveType.Sphere, "FountainOrb", scenery, new Vector3(0f, 3.8f, 0f), Vector3.one * 1f, neonCyan, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 17f, 0f, Mathf.Sin(a) * 17f);
                Prim(PrimitiveType.Cylinder, "Torch", scenery, p + Vector3.up * 1.5f, new Vector3(0.3f, 1.5f, 0.3f), timber);
                Prim(PrimitiveType.Sphere, "TorchFlame", scenery, p + Vector3.up * 3.2f, Vector3.one * 0.5f, neonGold, false);
            }
            // The KO board on the plaza's north side.
            var score = Board(b, "KOBoard", new Vector3(0f, 4.5f, 19f), Quaternion.identity, new Vector2(12f, 5f), 3.2f, "KO BOARD", neonGold);
            score.alignment = TMPro.TextAlignmentOptions.TopLeft;
            score.transform.parent.gameObject.AddComponent<ScoreBoard>().text = score;
            Posts(scenery, new Vector3(0f, 0f, 19f), false);

            // Three villages of smashable houses.
            for (int v = 0; v < 3; v++)
            {
                float a = (v * 120f + 30f) * Mathf.Deg2Rad;
                Vector3 c = new Vector3(Mathf.Cos(a) * 85f, 0f, Mathf.Sin(a) * 85f);
                Road(scenery, Vector3.zero, c, dirt);
                Prim(PrimitiveType.Cylinder, "VillageSquare", scenery, c + Vector3.up * 0.03f, new Vector3(26f, 0.03f, 26f), dirt, false);
                Take(c, 6f);
                Probe("VillageProbe", b, c + Vector3.up * 6f, new Vector3(64f, 16f, 64f));
                for (int h = 0; h < 8; h++)
                {
                    float ha = h * Mathf.PI * 2f / 8f + (float)rng.NextDouble() * 0.3f;
                    float hd = 17f + (float)rng.NextDouble() * 10f;
                    Vector3 hp = c + new Vector3(Mathf.Cos(ha) * hd, 0f, Mathf.Sin(ha) * hd);
                    if (!Free(hp, 6f)) continue;
                    Take(hp, 6f);
                    float yaw = Mathf.Atan2(c.x - hp.x, c.z - hp.z) * Mathf.Rad2Deg; // door faces the square
                    House(smash, scenery, hp, yaw, rng.Next(3) == 0, plaster[rng.Next(plaster.Length)], roofs[rng.Next(roofs.Length)], timber);
                }
            }

            // Towers you can knock down.
            for (int t = 0; t < 6; t++)
            {
                float a = (t * 60f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * 62f, 0f, Mathf.Sin(a) * 62f);
                if (!Free(p, 7f)) p += new Vector3(8f, 0f, 8f);
                Take(p, 7f);
                Tower(smash, p, 4 + t % 3, towerStone, roofs[t % roofs.Length]);
            }
            // Two lookout platforms with ramps (high ground).
            foreach (var p in new[] { new Vector3(0f, 0f, -55f), new Vector3(-100f, 0f, 70f) })
            {
                Take(p, 10f);
                var look = Group("Lookout", scenery, p);
                foreach (var c in new[] { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), new Vector3(-4f, 0f, 4f), new Vector3(4f, 0f, 4f) })
                    Prim(PrimitiveType.Cube, "Leg", look, c + Vector3.up * 2.5f, new Vector3(0.6f, 5f, 0.6f), timber);
                Prim(PrimitiveType.Cube, "Deck", look, new Vector3(0f, 5f, 0f), new Vector3(10f, 0.4f, 10f), timber);
                Ramp(look, new Vector3(0f, 0f, -17f), new Vector3(0f, 5f, -5f), 3f, timber);
                Ramp(look, new Vector3(17f, 0f, 0f), new Vector3(5f, 5f, 0f), 3f, timber);
            }

            // Rolling hills in the open spaces left over.
            for (int i = 0, tries = 0; i < 12 && tries < 200; tries++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 240f, 0f, ((float)rng.NextDouble() - 0.5f) * 240f);
                float w = 30f + (float)rng.NextDouble() * 25f;
                if (!Free(p, w * 0.42f)) continue;
                Take(p, w * 0.42f);
                i++;
                Prim(PrimitiveType.Sphere, "Hill", scenery, new Vector3(p.x, -6f, p.z), new Vector3(w, 16f, w * (0.8f + (float)rng.NextDouble() * 0.4f)), hillMat);
            }

            // Big rocks (the smaller ones break).
            for (int i = 0, tries = 0; i < 34 && tries < 400; tries++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 280f, 0f, ((float)rng.NextDouble() - 0.5f) * 280f);
                float size = 2.5f + (float)rng.NextDouble() * 7f;
                if (!Free(p, size * 0.6f + 2f)) continue;
                Take(p, size * 0.6f + 2f);
                i++;
                var rock = Prim(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, "Rock", size < 5.5f ? smash : scenery, p + Vector3.up * size * 0.3f,
                    new Vector3(size, size * (0.6f + (float)rng.NextDouble() * 0.4f), size * (0.7f + (float)rng.NextDouble() * 0.5f)), rockMat, true,
                    Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f));
                if (size < 5.5f) MakeBreakable(rock, nextBreakable++, 60f, rockMat);
            }

            // Pine trees.
            for (int i = 0, tries = 0; i < 80 && tries < 600; tries++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 280f, 0f, ((float)rng.NextDouble() - 0.5f) * 280f);
                if (!Free(p, 2.5f)) continue;
                Take(p, 2.5f);
                i++;
                float s = 0.8f + (float)rng.NextDouble() * 0.7f;
                var tree = Group("Pine", scenery, p);
                Prim(PrimitiveType.Cylinder, "Trunk", tree, Vector3.up * 1.5f * s, new Vector3(0.45f, 1.5f, 0.45f) * s, bark);
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Sphere, "Needles", tree, Vector3.up * (3.2f + k * 1.5f) * s, new Vector3(3.4f - k * 0.9f, 2f, 3.4f - k * 0.9f) * s, leafMat, false);
            }

            // Spawn points all over (in the open).
            var spawns = Group("SpawnPoints", b, Vector3.zero);
            int made = 0;
            for (int tries = 0; made < 28 && tries < 600; tries++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 260f, 0.1f, ((float)rng.NextDouble() - 0.5f) * 260f);
                if (made < 6) { float a = made * Mathf.PI / 3f; p = new Vector3(Mathf.Cos(a) * 12f, 0.15f, Mathf.Sin(a) * 12f); }
                else if (!Free(p, 2f)) continue;
                var sp = Group("Spawn" + made, spawns, p);
                sp.gameObject.AddComponent<SpawnPoint>().map = ArenaMap.Battleground;
                made++;
            }

            MarkStatic(scenery);
            Probe("PlazaProbe", b, new Vector3(0f, 6f, 0f), new Vector3(40f, 14f, 40f));
            ArtPass(b);
        }

        static void Road(Transform parent, Vector3 from, Vector3 to, Material mat)
        {
            Vector3 d = to - from;
            var r = Prim(PrimitiveType.Cube, "Road", parent, (from + to) * 0.5f + Vector3.up * 0.02f, new Vector3(5f, 0.02f, d.magnitude), mat, false);
            r.transform.localRotation = Quaternion.LookRotation(d.normalized);
        }

        /// <summary>A house of smashable walls (front wall has a door), with a roof that falls when its walls go.</summary>
        static void House(Transform smash, Transform scenery, Vector3 pos, float yaw, bool twoStorey, Material walls, Material roof, Material timber)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            const float w = 7f, d = 6f, h = 3f, t = 0.35f;
            Vector3 W(Vector3 local) => BattleOrigin + pos + rot * local; // world position (smashables sit under the map root)
            Prim(PrimitiveType.Cube, "HouseFloor", scenery, pos + Vector3.up * 0.06f, new Vector3(w, 0.12f, d), timber, true, rot);
            var lower = new List<Breakable>();
            int storeys = twoStorey ? 2 : 1;
            List<Breakable> below = null;
            for (int s = 0; s < storeys; s++)
            {
                float y = s * h + h * 0.5f;
                var level = new List<Breakable>();
                Breakable Piece(string name, Vector3 local, Vector3 size)
                {
                    var go = Prim(PrimitiveType.Cube, name, smash, Vector3.zero, size, walls, true);
                    go.transform.position = W(local);
                    go.transform.rotation = rot;
                    MakeBreakable(go, nextBreakable++, 30f, walls);
                    var br = go.GetComponent<Breakable>();
                    br.regrowSeconds = 50f;
                    if (below != null) { br.restsOn = below.ToArray(); br.collapseWhen = 2; }
                    level.Add(br);
                    return br;
                }
                // Front with a doorway (ground floor) or a window gap (upstairs).
                Piece("FrontLeft", new Vector3(-w * 0.32f, y, -d * 0.5f), new Vector3(w * 0.36f, h, t));
                Piece("FrontRight", new Vector3(w * 0.32f, y, -d * 0.5f), new Vector3(w * 0.36f, h, t));
                Piece("FrontTop", new Vector3(0f, y + h * 0.35f, -d * 0.5f), new Vector3(w * 0.28f, h * 0.3f, t));
                Piece("Back", new Vector3(0f, y, d * 0.5f), new Vector3(w, h, t));
                Piece("Left", new Vector3(-w * 0.5f, y, 0f), new Vector3(t, h, d));
                Piece("Right", new Vector3(w * 0.5f, y, 0f), new Vector3(t, h, d));
                if (s == 0) lower = level;
                below = level;
            }
            // Roof: two 45-degree halves meeting at the ridge, with gable ends closing the triangles at the sides.
            float top = storeys * h;
            float slope = d * 0.5f / Mathf.Cos(45f * Mathf.Deg2Rad);
            foreach (float side in new[] { -1f, 1f })
            {
                var r = Prim(PrimitiveType.Cube, "Roof", smash, Vector3.zero, new Vector3(w + 0.7f, 0.25f, slope + 0.6f), roof, true);
                // Centre of the slope, pushed out by half the thickness so it sits on the gables.
                r.transform.position = W(new Vector3(0f, top + d * 0.25f + 0.09f, side * (d * 0.25f + 0.09f)));
                r.transform.rotation = rot * Quaternion.Euler(side * 45f, 0f, 0f);
                RestOn(r, roof, 20f, below);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                // A square turned 45 degrees: its top half is the gable triangle, its bottom half hides inside the wall.
                float half = d * 0.5f / Mathf.Sqrt(2f) * 2f;
                var g = Prim(PrimitiveType.Cube, "Gable", smash, Vector3.zero, new Vector3(t, half, half), walls, true);
                g.transform.position = W(new Vector3(side * w * 0.5f, top, 0f));
                g.transform.rotation = rot * Quaternion.Euler(45f, 0f, 0f);
                RestOn(g, walls, 25f, below);
            }
            var chimney = Prim(PrimitiveType.Cube, "Chimney", smash, Vector3.zero, new Vector3(0.7f, 1.8f, 0.7f), walls, true);
            chimney.transform.position = W(new Vector3(w * 0.3f, top + 2.6f, d * 0.2f));
            chimney.transform.rotation = rot;
            RestOn(chimney, walls, 15f, below);
            // A door frame and windows (looks), and a wooden beam along the ridge.
            var ridge = Prim(PrimitiveType.Cube, "Ridge", smash, Vector3.zero, new Vector3(w + 0.8f, 0.3f, 0.3f), timber, true);
            ridge.transform.position = W(new Vector3(0f, top + d * 0.5f + 0.2f, 0f));
            ridge.transform.rotation = rot * Quaternion.Euler(45f, 0f, 0f);
            RestOn(ridge, timber, 15f, below);
            Prim(PrimitiveType.Cube, "DoorStep", scenery, pos + rot * new Vector3(0f, 0.1f, -d * 0.5f - 0.5f), new Vector3(w * 0.3f, 0.2f, 0.8f), timber, true, rot);
        }

        static void RestOn(GameObject go, Material mat, float hp, List<Breakable> below)
        {
            MakeBreakable(go, nextBreakable++, hp, mat);
            var br = go.GetComponent<Breakable>();
            br.restsOn = below.ToArray();
            br.collapseWhen = 2;
            br.regrowSeconds = 50f;
        }

        const float SkyHeight = 40f; // height of the invisible walls at the edge

        /// <summary>A solid ring of cliffs all the way round the map (rocks overlap so there are no gaps), with big
        /// mountains behind them.</summary>
        static void RockBoundary(Transform scenery, Material rockMat, System.Random rng)
        {
            void Ring(float dist, float spacing, float minW, float maxW, float minH, float maxH, string name)
            {
                // Walk round the square, one rock every few metres; each rock is wider than the gap to the next.
                float side = dist * 2f;
                int perSide = Mathf.CeilToInt(side / spacing);
                for (int edge = 0; edge < 4; edge++)
                    for (int i = 0; i < perSide; i++)
                    {
                        float along = -dist + (i + (float)rng.NextDouble() * 0.3f) * side / perSide;
                        Vector3 p = edge switch
                        {
                            0 => new Vector3(along, 0f, dist), 1 => new Vector3(dist, 0f, -along), 2 => new Vector3(-along, 0f, -dist), _ => new Vector3(-dist, 0f, along),
                        };
                        p += new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * spacing * 0.3f;
                        float wdt = minW + (float)rng.NextDouble() * (maxW - minW);
                        float hgt = minH + (float)rng.NextDouble() * (maxH - minH);
                        Prim(PrimitiveType.Sphere, name, scenery, new Vector3(p.x, hgt * 0.15f, p.z), new Vector3(wdt, hgt, wdt * (0.8f + (float)rng.NextDouble() * 0.4f)), rockMat, false,
                            Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    }
            }
            Ring(BattleHalf + 9f, 9f, 16f, 22f, 12f, 24f, "Cliff");       // right behind the wall, touching each other
            Ring(BattleHalf + 22f, 14f, 24f, 34f, 22f, 40f, "Cliff");      // a higher row behind, filling any gaps
            Ring(BattleHalf + 50f, 28f, 55f, 80f, 45f, 90f, "Mountain");   // mountains in the distance
        }
        /// <summary>A tall stone tower, floor by floor: smash a floor and everything above it comes down.</summary>
        static void Tower(Transform smash, Vector3 pos, int floors, Material stone, Material cap)
        {
            const float s = 6f, h = 4f, t = 0.8f;
            List<Breakable> below = null;
            for (int f = 0; f < floors; f++)
            {
                float y = f * h + h * 0.5f;
                var level = new List<Breakable>();
                foreach (var (off, size) in new[]
                {
                    (new Vector3(0f, 0f, -s * 0.5f), new Vector3(s, h, t)), (new Vector3(0f, 0f, s * 0.5f), new Vector3(s, h, t)),
                    (new Vector3(-s * 0.5f, 0f, 0f), new Vector3(t, h, s - t * 2f)), (new Vector3(s * 0.5f, 0f, 0f), new Vector3(t, h, s - t * 2f)),
                })
                {
                    var go = Prim(PrimitiveType.Cube, "TowerWall", smash, Vector3.zero, size, stone, true);
                    go.transform.position = BattleOrigin + pos + off + Vector3.up * y;
                    MakeBreakable(go, nextBreakable++, 55f, stone);
                    var br = go.GetComponent<Breakable>();
                    br.regrowSeconds = 70f;
                    if (below != null) { br.restsOn = below.ToArray(); br.collapseWhen = 2; }
                    level.Add(br);
                }
                below = level;
            }
            // Pointed cap on top.
            var roof = Prim(PrimitiveType.Cube, "TowerCap", smash, Vector3.zero, new Vector3(s + 0.8f, 1.2f, s + 0.8f), cap, true);
            roof.transform.position = BattleOrigin + pos + Vector3.up * (floors * h + 0.6f);
            MakeBreakable(roof, nextBreakable++, 30f, cap);
            roof.GetComponent<Breakable>().restsOn = below.ToArray();
            roof.GetComponent<Breakable>().collapseWhen = 2;
            // (Inside the cap, so it disappears with it; scaled back from the cap's size.)
            var spire = Prim(PrimitiveType.Cube, "Spire", roof.transform, new Vector3(0f, 1.6f / 1.2f, 0f), new Vector3(0.45f / (s + 0.8f), 2.5f / 1.2f, 0.45f / (s + 0.8f)), cap, false, Quaternion.Euler(0f, 45f, 0f));
            spire.GetComponent<Renderer>().sharedMaterial = neonRed;
        }
    }
}
