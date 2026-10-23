using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>What the decorations look like and where they go (pure maths: HalloweenDecor builds it in the game).</summary>
    public enum HMat { Pumpkin, Stem, Face, Stone, StoneDark, Soil, Ghost, Black, Web, Wire, BulbOrange, BulbPurple, BulbGreen, Wood, Shirt, Denim, Straw, Hat, Bark, Candle, Flame, Iron, Brew, BannerBack, FlagOrange, FlagPurple }

    public struct HPiece
    {
        public int area;
        public HShape shape;
        public HMat mat;
        public Vector3 pos, scale;
        public Quaternion rot;
        public bool shadow;
    }

    public struct HGhost { public int area; public Vector3 centre; public float radius, speed, phase, size; }
    public struct HFlock { public int area; public Vector3 centre; public float radius; public int count; }
    public struct HBanner { public int area; public Vector3 pos; public Quaternion rot; public string text; public float width, fontSize; }

    /// <summary>The game world as the plan sees it (physics in the game, nothing in previews).</summary>
    public interface IHalloweenPlace
    {
        /// <summary>The ground height under (x, z) in that area, or false where something (a house, a tree) is over it.</summary>
        bool Ground(int area, float x, float z, out float y);
        /// <summary>True when nothing solid fills an upright capsule standing on `bottom`.</summary>
        bool Clear(Vector3 bottom, float radius, float height);
        /// <summary>True near a place where fighters appear (keep those spots free).</summary>
        bool NearSpawn(Vector3 p, float distance);
    }

    /// <summary>
    /// Spooky season decorations for every place in the world: the briefing room (main menu), the Training hall and
    /// the Battleground. Jack-o'-lanterns, a graveyard or two, ghosts, bats, scarecrows, cauldrons, cobwebs, string
    /// lights and "HAPPY HALLOWEEN" banners. Only looks; nothing here has a collider.
    /// </summary>
    public class HalloweenPlan
    {
        public const int Menu = 0, Training = 1, Battle = 2;
        public static readonly Vector3 MenuOrigin = new Vector3(0f, 0f, -1500f), BattleOrigin = new Vector3(0f, 0f, 700f);

        public readonly List<HPiece> pieces = new List<HPiece>();
        public readonly List<HGhost> ghosts = new List<HGhost>();
        public readonly List<HFlock> flocks = new List<HFlock>();
        public readonly List<HBanner> banners = new List<HBanner>();

        readonly IHalloweenPlace place;
        System.Random rng = new System.Random(1031);
        int area;

        HalloweenPlan(IHalloweenPlace place) { this.place = place; }

        public static HalloweenPlan Make(IHalloweenPlace place)
        {
            var p = new HalloweenPlan(place);
            p.BuildMenu();
            p.BuildTraining();
            p.BuildBattleground();
            return p;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);
        static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        /// <summary>A rotation whose local X and Y point along x and y.</summary>
        static Quaternion Basis(Vector3 x, Vector3 y) => Quaternion.LookRotation(Vector3.Cross(x, y), y);

        void Add(HShape shape, HMat mat, Vector3 pos, Quaternion rot, Vector3 scale, bool shadow = true)
            => pieces.Add(new HPiece { area = area, shape = shape, mat = mat, pos = pos, rot = rot, scale = scale, shadow = shadow });

        /// <summary>A unit cylinder (lathe) stretched from a to b.</summary>
        void Rod(Vector3 a, Vector3 b, float thickness, HMat mat, bool shadow = true)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Add(HShape.Cylinder, mat, a, Quaternion.FromToRotation(Vector3.up, d), new Vector3(thickness, d.magnitude, thickness), shadow);
        }

        void Ball(Vector3 c, Vector3 size, HMat mat, Quaternion? rot = null, bool shadow = true)
            => Add(HShape.Sphere, mat, c, rot ?? Quaternion.identity, size, shadow);

        void Block(Vector3 centre, Vector3 size, HMat mat, Quaternion? rot = null, bool shadow = true)
            => Add(HShape.Box, mat, centre, rot ?? Quaternion.identity, size, shadow);

        // ------------------------------------------------------------------ pieces

        /// <summary>A pumpkin sitting on `pos` (radius r metres), carved and glowing unless lit is false.</summary>
        void Pumpkin(Vector3 pos, float yaw, float r, bool lit = true)
        {
            var rot = Yaw(yaw);
            Add(HShape.Pumpkin, HMat.Pumpkin, pos, rot, Vector3.one * r);
            if (lit) Add(HShape.PumpkinFace, HMat.Face, pos, rot, Vector3.one * r, false);
            Add(HShape.Stem, HMat.Stem, pos + Vector3.up * HalloweenShapes.PumpkinTop * r * 0.97f, rot * Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * r * 1.1f);
        }

        /// <summary>A pumpkin on the ground near (x, z), if there's room and it isn't on a spawn spot.</summary>
        bool GroundPumpkin(float x, float z, float yaw, float r, bool lit = true)
        {
            if (!place.Ground(area, x, z, out float y)) return false;
            var p = new Vector3(x, y, z);
            if (place.NearSpawn(p, 3f) || !place.Clear(p, r * 1.1f, r * 1.6f)) return false;
            Pumpkin(p, yaw, r, lit);
            return true;
        }

        /// <summary>One to three pumpkins together, the big one in front.</summary>
        void PumpkinPile(Vector3 c, float faceYaw, float big, int count)
        {
            Vector3 fwd = Yaw(faceYaw) * Vector3.forward, right = Yaw(faceYaw) * Vector3.right;
            GroundPumpkin(c.x, c.z, faceYaw + R(-15f, 15f), big);
            if (count > 1) { Vector3 p = c - fwd * big * 1.2f + right * big * 1.25f; GroundPumpkin(p.x, p.z, faceYaw + R(-25f, 25f), big * 0.7f, rng.Next(3) > 0); }
            if (count > 2) { Vector3 p = c - fwd * big * 0.9f - right * big * 1.4f; GroundPumpkin(p.x, p.z, faceYaw + R(-25f, 25f), big * 0.55f, rng.Next(2) > 0); }
        }

        void Gravestone(Vector3 pos, float yaw)
        {
            var rot = Yaw(yaw) * Quaternion.Euler(R(-7f, 7f), 0f, R(-6f, 6f));
            int kind = rng.Next(5);
            float s = R(0.85f, 1.15f);
            HMat stone = rng.Next(3) == 0 ? HMat.StoneDark : HMat.Stone;
            if (kind <= 2) Add(HShape.Gravestone, stone, pos + Vector3.down * 0.05f, rot, Vector3.one * s);
            else if (kind == 3)
            {
                Add(HShape.CrossUpright, stone, pos + rot * new Vector3(0f, 0.42f * s, 0f), rot, Vector3.one * s);
                Add(HShape.CrossBar, stone, pos + rot * new Vector3(0f, 0.62f * s, 0f), rot, Vector3.one * s);
            }
            else Block(pos + rot * new Vector3(0f, 0.18f * s, 0f), new Vector3(0.55f, 0.4f, 0.28f) * s, stone, rot);
            // the grave in front of it
            Ball(pos + Yaw(yaw) * new Vector3(0f, 0f, 0.85f), new Vector3(0.75f, 0.22f, 1.5f), HMat.Soil, Yaw(yaw));
        }

        void Scarecrow(Vector3 pos, float yaw)
        {
            var rot = Yaw(yaw);
            Vector3 At(float x, float y, float z) => pos + rot * new Vector3(x, y, z);
            Rod(pos, At(0f, 1.75f, 0f), 0.09f, HMat.Wood);
            Rod(At(-0.72f, 1.32f, 0f), At(0.72f, 1.32f, 0f), 0.07f, HMat.Wood);
            Block(At(0f, 0.85f, 0f), new Vector3(0.42f, 0.5f, 0.2f), HMat.Denim, rot);
            Block(At(0f, 1.22f, 0f), new Vector3(0.5f, 0.6f, 0.24f), HMat.Shirt, rot);
            Block(At(0f, 1.32f, 0f), new Vector3(1.2f, 0.17f, 0.17f), HMat.Shirt, rot);
            foreach (float sx in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++) Rod(At(sx * 0.6f, 1.32f, 0f), At(sx * R(0.72f, 0.8f), R(1.18f, 1.36f), R(-0.08f, 0.08f)), 0.025f, HMat.Straw, false);
                Rod(At(sx * 0.12f, 0.6f, 0f), At(sx * R(0.12f, 0.2f), R(0.45f, 0.5f), R(-0.05f, 0.05f)), 0.03f, HMat.Straw, false);
            }
            Pumpkin(At(0f, 1.5f, 0f), yaw, 0.2f);
            Add(HShape.Cylinder, HMat.Hat, At(0f, 1.78f, 0f), rot * Quaternion.Euler(-8f, 0f, 6f), new Vector3(0.62f, 0.025f, 0.62f));
            Add(HShape.Cone, HMat.Hat, At(0f, 1.79f, 0f), rot * Quaternion.Euler(-8f, 0f, 6f), new Vector3(0.3f, 0.42f, 0.3f));
        }

        void DeadTree(Vector3 pos, int seed)
        {
            var saved = rng;
            rng = new System.Random(seed);
            void Branch(Vector3 a, Vector3 dir, float length, float thick, int depth)
            {
                Vector3 b = a + dir * length;
                Rod(a, b, thick, HMat.Bark);
                if (depth == 0) return;
                int n = depth > 2 ? 2 : rng.Next(2, 4);
                for (int i = 0; i < n; i++)
                {
                    Vector3 nd = Quaternion.Euler(R(-38f, 38f), R(0f, 360f), R(-38f, 38f)) * dir;
                    nd = (nd + Vector3.up * 0.25f).normalized;
                    Branch(b, nd, length * R(0.55f, 0.75f), thick * 0.62f, depth - 1);
                }
            }
            Branch(pos + Vector3.down * 0.1f, Quaternion.Euler(R(-6f, 6f), 0f, R(-6f, 6f)) * Vector3.up, 2.4f, 0.32f, 4);
            rng = saved;
        }

        /// <summary>A sagging string of bulbs (alternating orange and purple) from a to b.</summary>
        void Lights(Vector3 a, Vector3 b, float sag, float spacing = 0.4f, bool green = false)
        {
            float len = Vector3.Distance(a, b);
            int bulbs = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
            Vector3 Point(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            const int wireSegs = 8;
            for (int i = 0; i < wireSegs; i++) Rod(Point(i / (float)wireSegs), Point((i + 1) / (float)wireSegs), 0.012f, HMat.Wire, false);
            for (int i = 1; i < bulbs; i++)
            {
                Vector3 p = Point(i / (float)bulbs) + Vector3.down * 0.04f;
                HMat m = green && i % 3 == 2 ? HMat.BulbGreen : i % 2 == 0 ? HMat.BulbOrange : HMat.BulbPurple;
                Ball(p, new Vector3(0.05f, 0.075f, 0.05f), m, null, false);
            }
        }

        /// <summary>Little triangle flags on a string from a to b.</summary>
        void Bunting(Vector3 a, Vector3 b, float sag)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.32f));
            Vector3 Point(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            for (int i = 0; i < 8; i++) Rod(Point(i / 8f), Point((i + 1) / 8f), 0.01f, HMat.Wire, false);
            var rot = Quaternion.LookRotation(Vector3.Cross(b - a, Vector3.up).normalized, Vector3.up);
            for (int i = 0; i < n; i++) Add(HShape.Flag, i % 2 == 0 ? HMat.FlagOrange : HMat.FlagPurple, Point((i + 0.5f) / n), rot, Vector3.one, false);
        }

        void Web(Vector3 corner, Vector3 alongX, Vector3 alongY, float size, bool round = false)
            => Add(round ? HShape.WebRound : HShape.Web, HMat.Web, corner, Basis(alongX.normalized, alongY.normalized), Vector3.one * size, false);

        void Cauldron(Vector3 pos)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Rod(pos + new Vector3(Mathf.Cos(a) * 0.3f, 0f, Mathf.Sin(a) * 0.3f), pos + new Vector3(Mathf.Cos(a) * 0.2f, 0.22f, Mathf.Sin(a) * 0.2f), 0.05f, HMat.Iron);
            }
            Add(HShape.Bowl, HMat.Iron, pos + Vector3.up * 0.12f, Quaternion.identity, Vector3.one);
            Add(HShape.Cylinder, HMat.Brew, pos + Vector3.up * 0.62f, Quaternion.identity, new Vector3(0.66f, 0.01f, 0.66f), false);
            for (int i = 0; i < 5; i++)
                Ball(pos + new Vector3(R(-0.22f, 0.22f), 0.63f, R(-0.22f, 0.22f)), Vector3.one * R(0.05f, 0.11f), HMat.Brew, null, false);
            // the fire under it
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 20f;
                Rod(pos + Yaw(a) * new Vector3(-0.28f, 0.04f, 0f), pos + Yaw(a) * new Vector3(0.28f, 0.06f, 0f), 0.07f, HMat.Bark);
            }
            Ball(pos + Vector3.up * 0.1f, new Vector3(0.22f, 0.16f, 0.22f), HMat.Flame, null, false);
        }

        void Candle(Vector3 pos, float h)
        {
            Add(HShape.Cylinder, HMat.Candle, pos, Quaternion.identity, new Vector3(0.05f, h, 0.05f), false);
            Ball(pos + Vector3.up * (h + 0.02f), new Vector3(0.022f, 0.045f, 0.022f), HMat.Flame, null, false);
        }

        void Banner(Vector3 pos, Quaternion rot, float width, float height)
        {
            Block(pos + rot * new Vector3(0f, 0f, 0.03f), new Vector3(width, height, 0.03f), HMat.BannerBack, rot, false);
            banners.Add(new HBanner { area = area, pos = pos + rot * new Vector3(0f, 0f, -0.01f), rot = rot, text = "HAPPY HALLOWEEN", width = width * 0.95f, fontSize = height * 9f });
            Bunting(pos + rot * new Vector3(-width * 0.5f, height * 0.5f + 0.05f, -0.03f), pos + rot * new Vector3(width * 0.5f, height * 0.5f + 0.05f, -0.03f), 0.06f);
        }

        void Ghost(Vector3 centre, float radius, float size, float speed)
            => ghosts.Add(new HGhost { area = area, centre = centre, radius = radius, size = size, speed = speed, phase = R(0f, Mathf.PI * 2f) });

        void Bats(Vector3 centre, float radius, int count)
            => flocks.Add(new HFlock { area = area, centre = centre, radius = radius, count = count });

        // ------------------------------------------------------------------ the briefing room (main menu)

        void BuildMenu()
        {
            area = Menu;
            Vector3 o = MenuOrigin;
            const float half = 8.6f, inner = 8.0f, h = 5.6f;
            // pumpkin piles in three corners (the fourth has crates) and by the door
            PumpkinPile(o + new Vector3(-7.4f, 0f, 7.4f), 135f + 45f, 0.32f, 3);
            PumpkinPile(o + new Vector3(7.4f, 0f, 7.4f), 180f + 45f, 0.32f, 3);
            PumpkinPile(o + new Vector3(-7.4f, 0f, -7.3f), 45f, 0.3f, 3);
            GroundPumpkin(o.x - 2.6f, o.z - 7.9f, 0f, 0.26f);
            GroundPumpkin(o.x + 2.6f, o.z - 7.9f, 0f, 0.26f);
            // on top of the lockers, on the workbench and the crates
            Pumpkin(o + new Vector3(-8.25f, 2.0f, -4.5f), 90f, 0.14f);
            Pumpkin(o + new Vector3(-8.25f, 2.0f, -2.8f), 90f, 0.12f, false);
            Pumpkin(o + new Vector3(7.95f, 0.94f, -5.05f), -90f, 0.13f);
            Pumpkin(o + new Vector3(half - 1.2f, 1.9f, -7f), -45f, 0.16f);
            foreach (var (dz, ch) in new[] { (-3.55f, 0.16f), (-3.4f, 0.11f), (-3.68f, 0.08f) }) Candle(o + new Vector3(8.05f, 0.94f, dz), ch);
            // string lights round the top of the walls
            foreach (var (from, to) in new[] { (new Vector3(-inner, 0f, inner), new Vector3(inner, 0f, inner)), (new Vector3(inner, 0f, inner), new Vector3(inner, 0f, -inner)),
                                               (new Vector3(inner, 0f, -inner), new Vector3(-inner, 0f, -inner)), (new Vector3(-inner, 0f, -inner), new Vector3(-inner, 0f, inner)) })
                for (int s = 0; s < 3; s++)
                {
                    Vector3 a = Vector3.Lerp(from, to, s / 3f), b = Vector3.Lerp(from, to, (s + 1) / 3f);
                    Lights(o + a + Vector3.up * 5.45f, o + b + Vector3.up * 5.45f, 0.2f, 0.42f);
                }
            // cobwebs in the top corners
            const float c = half - 0.42f;
            foreach (var (x, z) in new[] { (-1f, 1f), (1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                Vector3 corner = o + new Vector3(x * c, h - 0.1f, z * c);
                Web(corner, new Vector3(-x, 0f, 0f), Vector3.down, 1.1f);
                Web(corner + new Vector3(0f, -0.05f, 0f), new Vector3(0f, 0f, -z), Vector3.down, 0.8f);
            }
            Banner(o + new Vector3(0f, 4.85f, half - 0.42f), Quaternion.identity, 5.6f, 0.6f);
            Bats(o + new Vector3(0f, 4.5f, 0f), 4.6f, 5);
            Ghost(o + new Vector3(0f, 2.2f, 15f), 3.5f, 1.15f, 0.25f); // drifting past the window
        }

        // ------------------------------------------------------------------ the Training hall

        void BuildTraining()
        {
            area = Training;
            const float ring = 7f, postTop = 1.85f;
            // jack-o'-lanterns on the corner posts, lights strung between them
            var posts = new[] { new Vector3(-ring, 0f, -ring), new Vector3(ring, 0f, -ring), new Vector3(ring, 0f, ring), new Vector3(-ring, 0f, ring) };
            for (int i = 0; i < 4; i++)
            {
                Pumpkin(posts[i] + Vector3.up * postTop, YawTo(posts[i], Vector3.zero) + 180f, 0.17f);
                Lights(posts[i] + Vector3.up * (postTop + 0.05f), posts[(i + 1) % 4] + Vector3.up * (postTop + 0.05f), 0.18f, 0.45f);
            }
            // pumpkins round the ring on the hall floor
            for (int side = 0; side < 4; side++)
            {
                Quaternion turn = Yaw(side * 90f);
                foreach (float along in new[] { -4.5f, 0f, 4.5f })
                {
                    Vector3 p = turn * new Vector3(along, 0f, -8.35f);
                    GroundPumpkin(p.x, p.z, YawTo(Vector3.zero, p), R(0.24f, 0.34f), rng.Next(4) > 0);
                }
                Vector3 corner = turn * new Vector3(-8.6f, 0f, -8.6f);
                PumpkinPile(corner, YawTo(Vector3.zero, corner), 0.36f, 3);
            }
            // a pair at the foot of every stone pillar
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p = dir * 16.5f;
                PumpkinPile(p, YawTo(Vector3.zero, p), 0.3f, 2);
            }
            // a little graveyard by the punching bags
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                {
                    Vector3 p = new Vector3(-17f + col * 2.3f + R(-0.2f, 0.2f), 0f, 36.5f + row * 2.4f);
                    if (place.Clear(p, 0.45f, 1f) && !place.NearSpawn(p, 3f)) Gravestone(p, 180f + R(-8f, 8f));
                }
            DeadTree(new Vector3(-21.5f, 0f, 39.5f), 7);
            // the banner under the hall's NULLBOUND sign, bats under the roof and ghosts drifting round the floor
            Banner(new Vector3(0f, 5.6f, 44.55f), Quaternion.identity, 12f, 1.25f);
            Bats(new Vector3(0f, 9f, 0f), 7f, 8);
            Bats(new Vector3(0f, 10f, 37f), 8f, 6);
            Ghost(new Vector3(0f, 1.7f, 0f), 22f, 1.3f, 0.12f);
            Ghost(new Vector3(0f, 2.3f, 0f), 26f, 1.4f, -0.1f);
            Ghost(new Vector3(-14f, 1.6f, 37f), 4f, 1.1f, 0.3f);
            Ghost(new Vector3(26f, 2.2f, 18f), 6f, 1.2f, -0.22f);
        }

        // ------------------------------------------------------------------ the Battleground

        void BuildBattleground()
        {
            area = Battle;
            Vector3 o = BattleOrigin;
            // the fountain: lanterns all round its rim, ghosts round the spire, bats round the orb
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Pumpkin(o + dir * 2.82f + Vector3.up * 0.8f, YawTo(Vector3.zero, dir), 0.16f);
            }
            Ghost(o + Vector3.up * 4.6f, 6.5f, 1.4f, 0.2f);
            Ghost(o + Vector3.up * 5.4f, 8.5f, 1.25f, -0.16f);
            Bats(o + Vector3.up * 8f, 5f, 7);
            // the plaza's edge
            for (int i = 0; i < 14; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 14f;
                Vector3 p = o + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 16.8f;
                if (i % 3 == 0) PumpkinPile(p, YawTo(p, o), 0.4f, 2);
                else GroundPumpkin(p.x, p.z, YawTo(p, o), R(0.3f, 0.42f));
            }
            // villages: lanterns round each square, a cauldron, two scarecrows, a ghost and bats
            for (int v = 0; v < 3; v++)
            {
                float a = (v * 120f + 30f) * Mathf.Deg2Rad;
                Vector3 c = o + new Vector3(Mathf.Cos(a) * 85f, 0f, Mathf.Sin(a) * 85f);
                for (int i = 0; i < 10; i++)
                {
                    float pa = (i + 0.5f) * Mathf.PI * 2f / 10f;
                    Vector3 p = c + new Vector3(Mathf.Cos(pa), 0f, Mathf.Sin(pa)) * 11.6f;
                    GroundPumpkin(p.x, p.z, YawTo(p, c), R(0.3f, 0.45f), rng.Next(5) > 0);
                }
                Vector3 pot = c + new Vector3(4.2f, 0f, 3.1f);
                if (place.Ground(area, pot.x, pot.z, out float py) && place.Clear(new Vector3(pot.x, py, pot.z), 0.5f, 0.8f)) Cauldron(new Vector3(pot.x, py, pot.z));
                foreach (float sa in new[] { 45f, 225f })
                {
                    Vector3 p = c + Yaw(sa + v * 30f) * Vector3.forward * 8.6f;
                    if (place.Ground(area, p.x, p.z, out float y) && place.Clear(new Vector3(p.x, y, p.z), 0.5f, 1.9f) && !place.NearSpawn(new Vector3(p.x, y, p.z), 3f))
                        Scarecrow(new Vector3(p.x, y, p.z), YawTo(p, c));
                }
                Ghost(c + Vector3.up * 2.2f, 9.5f, 1.4f, v % 2 == 0 ? 0.14f : -0.14f);
                Bats(c + Vector3.up * 12f, 9f, 6);
                // lanterns down the road to the plaza
                for (float d = 24f; d < 70f; d += 11f)
                {
                    Vector3 along = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = new Vector3(-along.z, 0f, along.x);
                    Vector3 p = o + along * d + side * (((int)(d / 11f) % 2 == 0) ? 3.3f : -3.3f);
                    GroundPumpkin(p.x, p.z, YawTo(p, o + along * d), R(0.28f, 0.36f));
                }
            }
            // graveyards between the villages
            foreach (float ga in new[] { 90f, 210f, 330f })
            {
                float a = ga * Mathf.Deg2Rad;
                Vector3 c = o + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 84f;
                Graveyard(c, YawTo(c, o));
            }
            // bats round the tower tops, and a few pumpkins out in the fields
            for (int t = 0; t < 6; t += 2)
            {
                float a = t * 60f * Mathf.Deg2Rad;
                Bats(o + new Vector3(Mathf.Cos(a) * 62f, 19f, Mathf.Sin(a) * 62f), 7f, 5);
            }
            for (int i = 0, placed = 0; i < 80 && placed < 22; i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(28f, 135f);
                Vector3 p = o + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (GroundPumpkin(p.x, p.z, R(0f, 360f), R(0.3f, 0.5f), rng.Next(3) > 0)) placed++;
            }
        }

        /// <summary>Rows of headstones facing `yaw`, a fence with a gate on that side, a dead tree, lanterns at the gate.</summary>
        void Graveyard(Vector3 c, float yaw)
        {
            var rot = Yaw(yaw);
            Vector3 At(float x, float z) => c + rot * new Vector3(x, 0f, z);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 5; col++)
                {
                    Vector3 p = At(-4.8f + col * 2.4f + R(-0.25f, 0.25f), -3.6f + row * 2.4f + R(-0.2f, 0.2f));
                    if (!place.Ground(area, p.x, p.z, out float y)) continue;
                    p.y = y;
                    if (place.NearSpawn(p, 3f) || !place.Clear(p, 0.45f, 1f)) continue;
                    Gravestone(p, yaw + 180f + R(-10f, 10f));
                }
            // fence: four sides, a gap for the gate in the front (+Z) side
            const float hx = 6.8f, hz = 5.6f;
            void FenceRun(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.6f));
                for (int i = 0; i <= posts; i++)
                {
                    Vector2 q = Vector2.Lerp(a, b, i / (float)posts);
                    Vector3 p = At(q.x, q.y);
                    if (!place.Ground(area, p.x, p.z, out float y)) continue;
                    p.y = y;
                    Block(p + Vector3.up * 0.5f, new Vector3(0.1f, 1f, 0.1f), HMat.Wood, rot);
                    Block(p + Vector3.up * 1.03f, new Vector3(0.14f, 0.06f, 0.14f), HMat.Wood, rot * Quaternion.Euler(0f, 45f, 0f));
                    if (i == posts) continue;
                    Vector2 q2 = Vector2.Lerp(a, b, (i + 1) / (float)posts);
                    Vector3 p2 = At(q2.x, q2.y);
                    if (!place.Ground(area, p2.x, p2.z, out float y2)) continue;
                    p2.y = y2;
                    foreach (float rail in new[] { 0.35f, 0.78f }) Rod(p + Vector3.up * rail, p2 + Vector3.up * rail, 0.05f, HMat.Wood);
                }
            }
            FenceRun(new Vector2(-hx, -hz), new Vector2(hx, -hz));
            FenceRun(new Vector2(hx, -hz), new Vector2(hx, hz));
            FenceRun(new Vector2(-hx, hz), new Vector2(-hx, -hz));
            FenceRun(new Vector2(-hx, hz), new Vector2(-1.4f, hz));
            FenceRun(new Vector2(1.4f, hz), new Vector2(hx, hz));
            Vector3 tree = At(-5.2f, -4.6f);
            if (place.Ground(area, tree.x, tree.z, out float ty)) DeadTree(new Vector3(tree.x, ty, tree.z), (int)(c.x * 7f + c.z));
            foreach (float gx in new[] { -1.9f, 1.9f })
            {
                Vector3 g = At(gx, hz + 0.7f);
                GroundPumpkin(g.x, g.z, yaw, 0.34f);
            }
            Ghost(At(0f, 0f) + Vector3.up * 1.4f, 4.5f, 1.3f, 0.25f);
            Bats(At(-5.2f, -4.6f) + Vector3.up * 7f, 4f, 5);
        }
    }
}
