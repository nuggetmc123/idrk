using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// THE SWAMP (3.65): a flooded bayou. Knee-deep murky water over soft mud, grassy islands joined by rickety boardwalks,
    /// moss-hung cypress trees, reeds and lily pads, stilt shacks to loot - and on the big north island the old BAIT &amp;
    /// TACKLE, lair of THE SWAMP KING, a giant crocodile who is there EVERY raid. Every raider here is a crocodile.
    /// Kill the King and take his crown to the TURN-IN board in your bunker.
    /// </summary>
    public class SwampMap : MapBase
    {
        static Material Mud => Mats.Lit("swamp_mud", new Color(0.2f, 0.17f, 0.11f), 0f, 0.35f, Mats.Noise, null, 0.3f, 0.05f);
        static Material Murk => Mats.Lit("swamp_water", new Color(0.09f, 0.13f, 0.07f), 0.2f, 0.94f, null, null, 0, 0, true, 0.2f);
        static Material Bank => Mats.Lit("swamp_bank", new Color(0.33f, 0.36f, 0.2f), 0f, 0.2f, Mats.Noise, null, 0.35f, 0.05f);
        static Material Moss => Mats.Lit("swamp_moss", new Color(0.36f, 0.42f, 0.3f), 0f, 0.15f, Mats.Fabric, null, 0.2f, 0f);
        static Material Lily => Mats.Lit("swamp_lily", new Color(0.2f, 0.42f, 0.16f), 0f, 0.5f, null, null, 0.05f, 0f);

        const float MudY = -0.35f, WaterY = -0.05f, IslandY = 0.12f;
        const float K = 1.4f;        // (3.71) the swamp itself is 40% bigger (and the land round it much smaller)
        const float Core = 120f;     // half-size of the swamp
        readonly List<(Vector3 p, float r)> _islands = new List<(Vector3, float)>();

        public override void Build()
        {
            Ambience = Synth.Amb.Forest;
            NavBounds = new Bounds(new Vector3(0, 5, 0), new Vector3(Core * 2 + 20, 30, Core * 2 + 20));
            // a hazy green afternoon
            Lighting(new Color(1f, 0.92f, 0.75f), 0.85f, new Vector3(32, 210, 0), new Color(0.45f, 0.55f, 0.42f), new Color(0.4f, 0.45f, 0.33f), new Color(0.25f, 0.27f, 0.18f),
                new Color(0.5f, 0.58f, 0.46f), 0.012f, new Color(0.45f, 0.58f, 0.6f), new Color(0.7f, 0.76f, 0.62f), 0.6f, 0.7f);
            var b = B;
            var pts = new List<Vector3>();

            // ---- the swamp bed and the water over it (you wade through it; it doesn't stop bullets)
            b.Ground(Vector3.zero, new Vector2(Core * 2, Core * 2), Mud, Synth.Surface.Dirt, MudY);
            b.Box(new Vector3(0, WaterY, 0), new Vector3(Core * 2, 0.02f, Core * 2), Murk, Color.white, null, false);

            // ---- islands
            var isl = new[] {
                (new Vector3(0, 0, 50), 17f),     // the Swamp King's island (north)
                (new Vector3(-46, 0, -38), 12f), (new Vector3(42, 0, -44), 11f), (new Vector3(-56, 0, 22), 10f), (new Vector3(56, 0, 14), 12f),
                (new Vector3(-18, 0, -4), 9f), (new Vector3(24, 0, 6), 8f), (new Vector3(-6, 0, -58), 10f), (new Vector3(60, 0, -72), 7f), (new Vector3(-62, 0, -70), 8f),
                (new Vector3(-70, 0, 64), 8f), (new Vector3(66, 0, 62), 8f) };
            foreach (var (p, r) in isl) Island(p * K, r * 1.2f);

            // ---- boardwalks between them
            int[,] links = { { 0, 5 }, { 0, 6 }, { 5, 1 }, { 5, 3 }, { 6, 4 }, { 6, 2 }, { 1, 7 }, { 2, 7 }, { 3, 10 }, { 4, 11 }, { 2, 8 }, { 1, 9 } };
            for (int i = 0; i < links.GetLength(0); i++) Boardwalk(_islands[links[i, 0]], _islands[links[i, 1]]);

            // ---- stilt shacks on the islands (loot inside)
            Color[] shack = { new Color(0.5f, 0.42f, 0.32f), new Color(0.42f, 0.38f, 0.3f), new Color(0.38f, 0.44f, 0.36f), new Color(0.55f, 0.45f, 0.35f) };
            for (int i = 1; i < _islands.Count; i++)
            {
                var (p, r) = _islands[i];
                if (r < 8.5f) continue;
                float yaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;   // doors face the middle of the swamp
                yaw = Mathf.Round(yaw / 90f) * 90f;
                pts.AddRange(b.Building(p + new Vector3(0, IslandY, 0), yaw, new Vector2(8, 7), 1, Maps.WoodPlank, shack[i % shack.Length], Maps.WoodPlank, Maps.WoodPlank, 0, true, false));
                b.AddLight(p + new Vector3(0, IslandY + 2.6f, 0) + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -4.2f), new Color(1f, 0.75f, 0.4f), 7f, 1.1f);   // a lantern by the door
            }

            // ---- the BAIT & TACKLE: the Swamp King's lair, a big two-storey shack on the north island
            var lair = _islands[0].p + new Vector3(0, IslandY, 2);
            pts.AddRange(b.Building(lair, 180, new Vector2(16, 12), 2, Maps.WoodPlank, new Color(0.36f, 0.3f, 0.22f), Maps.WoodPlank, Maps.WoodPlank, 0, true, true));
            b.Sign(lair + new Vector3(0, 7.2f, -6.4f), Quaternion.identity, "BAIT & TACKLE", new Color(0.95f, 0.85f, 0.45f), 0.5f);
            b.Sign(lair + new Vector3(0, 6.7f, -6.4f), Quaternion.identity, "keep out - king's orders", new Color(0.9f, 0.3f, 0.25f), 0.16f);
            b.AddLight(lair + new Vector3(0, 3f, -8f), new Color(1f, 0.6f, 0.3f), 12f, 1.5f);
            // bones and skulls piled by the door
            for (int i = 0; i < 6; i++) b.Sphere(lair + new Vector3(-4.5f + i * 1.7f, 0.12f, -7.6f + (i % 2) * 0.4f), new Vector3(0.14f, 0.1f, 0.12f), 8, Mats.Lit("bone", new Color(0.88f, 0.84f, 0.72f), 0f, 0.4f), Color.white, false);
            Boss(lair + new Vector3(0, 0.1f, 0), "The Swamp King", 1f);   // he's home EVERY raid
            Squad(lair + new Vector3(-5, 0.1f, -9), 3, true);

            // ---- cypress trees with hanging moss, out in the water and on the islands
            for (int i = 0; i < 120; i++)
            {
                var p = new Vector3(b.Rng.Range(-Core + 4f, Core - 4f), 0, b.Rng.Range(-Core + 4f, Core - 4f));
                if (OnIsland(p, 1.5f, out float top) && b.Rng.Chance(0.6f)) continue;   // mostly in the water
                if (NearPath(p, 3f)) continue;
                Cypress(p, top, b.Rng.Range(9f, 15f));
            }
            for (int i = 0; i < 40; i++)
            {
                int k = b.Rng.Range(0, _islands.Count); var (ip, ir) = _islands[k];
                var p = ip + new Vector3(b.Rng.Range(-ir, ir) * 0.85f, 0, b.Rng.Range(-ir, ir) * 0.85f);
                if (!OnIsland(p, 1f, out float top) || NearPath(p, 2.5f) || !b.Clear(p + Vector3.up * (top + 2f), new Vector3(2f, 3f, 2f), 0)) continue;
                Cypress(p, top, b.Rng.Range(8f, 12f));
            }
            // reeds, lily pads, logs and old boats
            for (int i = 0; i < 480; i++)
            {
                var p = new Vector3(b.Rng.Range(-Core + 2f, Core - 2f), 0, b.Rng.Range(-Core + 2f, Core - 2f));
                if (OnIsland(p, -0.5f, out _)) { if (b.Rng.Chance(0.5f)) b.GrassTuft(new Vector3(p.x, IslandY, p.z), b.Rng.Range(0.6f, 1.1f)); continue; }
                if (b.Rng.Chance(0.55f)) Reeds(new Vector3(p.x, MudY, p.z));
                else b.Raw(Lily, p).Cyl(new Vector3(p.x, WaterY + 0.012f, p.z), new Vector3(p.x, WaterY + 0.018f, p.z), b.Rng.Range(0.25f, 0.5f), b.Rng.Range(0.25f, 0.5f), 10, Color.white * b.Rng.Range(0.85f, 1.1f), true);
            }
            for (int i = 0; i < 22; i++)
            {
                var p = new Vector3(b.Rng.Range(-Core + 6f, Core - 6f), 0, b.Rng.Range(-Core + 6f, Core - 6f));
                if (OnIsland(p, 2f, out _) || NearPath(p, 3f)) continue;
                float yaw = b.Rng.Range(0f, 180f) * Mathf.Deg2Rad; var d = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)) * b.Rng.Range(2.5f, 5f);
                b.Cyl(new Vector3(p.x, MudY + 0.2f, p.z) - d, new Vector3(p.x, MudY + 0.3f, p.z) + d, 0.35f, 0.28f, 9, Maps.WoodPlank, new Color(0.35f, 0.28f, 0.2f), true, Synth.Surface.Wood);   // a rotting log (cover)
            }
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(b.Rng.Range(-Core + 8f, Core - 8f), 0, b.Rng.Range(-Core + 8f, Core - 8f));
                if (OnIsland(p, 3f, out _) || NearPath(p, 4f)) continue;
                Boat(new Vector3(p.x, MudY, p.z), b.Rng.Range(0f, 360f));
            }

            ScatterLoot(pts, 0.75f);
            AddExtract(new Vector3(0, MudY, -Core + 4), "Airboat Dock");
            AddExtract(new Vector3(-Core + 4, MudY, 12), "Old Rail Bridge");
            AddExtract(new Vector3(Core - 6, MudY, 50), "Ranger Tower");
            // player spawns round the edge of the swamp, facing in
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2 + 0.2f;
                var p = new Vector3(Mathf.Cos(a) * (Core - 14f), MudY + 0.1f, Mathf.Sin(a) * (Core - 14f));
                Spawn(p, Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg);
            }
            // crocodiles everywhere
            Squad(_islands[5].p + Vector3.up * 0.2f, 3); Squad(_islands[6].p + Vector3.up * 0.2f, 2); Squad(_islands[1].p + Vector3.up * 0.2f, 3, true); Squad(_islands[2].p + Vector3.up * 0.2f, 2);
            Squad(_islands[3].p + Vector3.up * 0.2f, 2); Squad(_islands[4].p + Vector3.up * 0.2f, 3); Squad(_islands[7].p + Vector3.up * 0.2f, 2, true);
            Squad(new Vector3(-42, MudY + 0.1f, 42), 2); Squad(new Vector3(42, MudY + 0.1f, -28), 2); Squad(new Vector3(-42, MudY + 0.1f, -84), 2); Squad(new Vector3(56, MudY + 0.1f, 56), 2);
            Squad(new Vector3(-90, MudY + 0.1f, -10), 2); Squad(new Vector3(90, MudY + 0.1f, -10), 2);
            // forest and fields round the swamp: 800 m
            Outskirts(new Rect(-Core, -Core, Core * 2, Core * 2), 420, new[] { "Fishing Camp", "Sawmill", "Gator Farm", "Levee Road" }, true, false, Maps.GroundGrass, 5);   // (3.71) much less land round it
        }

        /// <summary>(3.71) An island: a flat grassy top with gentle muddy banks you can walk up out of the water, and a
        /// collider the same shape (it used to be a capsule as wide as the island - an invisible dome nobody could get onto).</summary>
        void Island(Vector3 p, float r)
        {
            var b = B;
            const float bank = 4f; const int nu = 32, nv = 8;
            float[] rad = { 0f, r * 0.5f, r * 0.85f, r, r + bank * 0.3f, r + bank * 0.6f, r + bank * 0.85f, r + bank, r + bank + 0.6f };
            Vector3 P(int i, int j)
            {
                float a = i / (float)nu * Mathf.PI * 2f, rr = rad[j];
                float wob = 1f + 0.06f * Mathf.Sin(a * 3f + p.x) + 0.04f * Mathf.Sin(a * 5f + p.z);   // not a perfect circle
                float t = Mathf.Clamp01((rr - r) / bank);
                float y = rr <= r ? IslandY : Mathf.Lerp(IslandY, MudY - 0.04f, t * t * (3f - 2f * t));
                return p + new Vector3(Mathf.Sin(a) * rr * wob, y, Mathf.Cos(a) * rr * wob);
            }
            b.Raw(Bank, p).Patch(nu, nv, P, new Color(0.85f, 0.9f, 0.75f), true);
            var go = new GameObject("IslandCollider");
            go.transform.SetParent(b.Root, false);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = new MB().Patch(nu, nv, P, Color.white, true).ToMesh("island_col");
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Grass;
            _islands.Add((p, r));
        }

        bool OnIsland(Vector3 p, float margin, out float top)
        {
            foreach (var (ip, r) in _islands)
                if (new Vector2(p.x - ip.x, p.z - ip.z).magnitude < r - margin) { top = IslandY; return true; }
            top = MudY; return false;
        }

        readonly List<(Vector3 a, Vector3 b)> _paths = new List<(Vector3, Vector3)>();
        bool NearPath(Vector3 p, float d)
        {
            foreach (var (a, c) in _paths)
            {
                Vector3 ab = c - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                Vector3 q = a + ab * t; if (new Vector2(p.x - q.x, p.z - q.z).sqrMagnitude < d * d) return true;
            }
            return false;
        }

        /// <summary>A plank walkway on posts from one island's shore to another's.</summary>
        void Boardwalk((Vector3 p, float r) A, (Vector3 p, float r) Bi)
        {
            var b = B;
            Vector3 d = Bi.p - A.p; d.y = 0; float len = d.magnitude; d /= len;
            Vector3 s = A.p + d * (A.r * 0.8f), e = Bi.p - d * (Bi.r * 0.8f);
            float L = (e - s).magnitude; if (L < 1f) return;
            Vector3 mid = (s + e) * 0.5f; float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var q = Quaternion.Euler(0, yaw, 0);
            const float deckY = 0.26f;
            b.Box(new Vector3(mid.x, deckY, mid.z), new Vector3(1.7f, 0.08f, L), Maps.WoodPlank, new Color(0.55f, 0.45f, 0.33f), q, true, Synth.Surface.Wood);
            int posts = Mathf.Max(2, Mathf.RoundToInt(L / 3f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 pp = Vector3.Lerp(s, e, i / (float)posts);
                foreach (float sx in new[] { -0.8f, 0.8f })
                {
                    Vector3 at = pp + q * new Vector3(sx, 0, 0);
                    b.Cyl(new Vector3(at.x, MudY - 0.2f, at.z), new Vector3(at.x, deckY + (i % 2 == 0 ? 0.9f : 0f), at.z), 0.08f, 0.07f, 6, Maps.WoodPlank, new Color(0.38f, 0.3f, 0.22f), false);
                }
            }
            // (3.71) steps up out of the water onto the walkway, on the side without the rail
            {
                Vector3 side = q * new Vector3(-1f, 0, 0);
                b.Stairs(new Vector3(mid.x, MudY, mid.z) + side * 2.4f, new Vector3(mid.x, deckY, mid.z) + side * 0.86f, 1.1f, Maps.WoodPlank);
            }
            // a rope rail on one side
            Vector3 rs = s + q * new Vector3(0.8f, 0, 0), re = e + q * new Vector3(0.8f, 0, 0);
            b.Cyl(new Vector3(rs.x, deckY + 0.85f, rs.z), new Vector3(re.x, deckY + 0.85f, re.z), 0.02f, 0.02f, 5, Moss, new Color(0.6f, 0.5f, 0.35f), false);
            _paths.Add((s, e));
        }

        /// <summary>A bald cypress: a flared trunk with knees poking out of the water round it, a flat crown and grey moss hanging down.</summary>
        void Cypress(Vector3 p, float groundY, float h)
        {
            var b = B;
            Vector3 g = new Vector3(p.x, groundY, p.z);
            b.Cyl(g + Vector3.down * 0.2f, g + Vector3.up * 1.2f, h * 0.075f, h * 0.035f, 10, Maps.WoodPlank, new Color(0.42f, 0.36f, 0.3f), true, Synth.Surface.Wood);   // the flared base
            b.LeafyTree(g + Vector3.up * 0.6f, h);
            for (int k = 0; k < 4; k++)   // knees
            {
                float a = b.Rng.Range(0f, Mathf.PI * 2f), r = b.Rng.Range(0.9f, 1.8f);
                var kp = g + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                b.Raw(Moss, kp).Cyl(new Vector3(kp.x, MudY, kp.z), new Vector3(kp.x, WaterY + b.Rng.Range(0.15f, 0.45f), kp.z), 0.12f, 0.03f, 6, new Color(0.5f, 0.42f, 0.36f), true);
            }
            for (int k = 0; k < 7; k++)   // Spanish moss
            {
                float a = b.Rng.Range(0f, Mathf.PI * 2f), r = b.Rng.Range(1f, h * 0.28f);
                var top = g + new Vector3(Mathf.Cos(a) * r, h * b.Rng.Range(0.5f, 0.68f), Mathf.Sin(a) * r);
                float len = b.Rng.Range(0.8f, 2.2f);
                b.Raw(Moss, top).Box(top + Vector3.down * len * 0.5f, new Vector3(0.18f, len, 0.04f), new Color(0.72f, 0.76f, 0.66f), 0f, 1, Quaternion.Euler(0, b.Rng.Range(0f, 180f), 0));
            }
        }

        void Reeds(Vector3 p)
        {
            var b = B;
            var mb = b.Raw(Moss, p);
            int n = b.Rng.Range(5, 11);
            for (int i = 0; i < n; i++)
            {
                var o = new Vector3(b.Rng.Range(-0.4f, 0.4f), 0, b.Rng.Range(-0.4f, 0.4f));
                float h = b.Rng.Range(1.1f, 1.9f);
                var lean = new Vector3(b.Rng.Range(-0.12f, 0.12f), 0, b.Rng.Range(-0.12f, 0.12f));
                mb.Cyl(p + o, p + o + Vector3.up * h + lean, 0.012f, 0.006f, 4, new Color(0.55f, 0.62f, 0.35f), false);
                if (b.Rng.Chance(0.35f)) mb.Capsule(p + o + Vector3.up * (h - 0.25f) + lean * 0.85f, p + o + Vector3.up * (h - 0.05f) + lean, 0.022f, 5, new Color(0.35f, 0.22f, 0.12f));   // cattail
            }
        }

        /// <summary>A half-sunk wooden boat, nose up out of the water (cover).</summary>
        void Boat(Vector3 p, float yaw)
        {
            var b = B;
            var q = Quaternion.Euler(-12f, yaw, 6f);
            Color hull = new Color(0.42f, 0.36f, 0.3f);
            b.Box(p + q * new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.12f, 4.2f), Maps.WoodPlank, hull, q, true, Synth.Surface.Wood);
            foreach (float sx in new[] { -0.8f, 0.8f }) b.Box(p + q * new Vector3(sx, 0.7f, 0), new Vector3(0.1f, 0.7f, 4.2f), Maps.WoodPlank, hull * 0.9f, q, true, Synth.Surface.Wood);
            b.Box(p + q * new Vector3(0, 0.7f, -2.05f), new Vector3(1.6f, 0.7f, 0.1f), Maps.WoodPlank, hull * 0.85f, q, true, Synth.Surface.Wood);
            b.Box(p + q * new Vector3(0, 0.75f, 0.4f), new Vector3(1.5f, 0.08f, 0.35f), Maps.WoodPlank, hull * 1.1f, q, false);   // a seat
        }
    }
}
