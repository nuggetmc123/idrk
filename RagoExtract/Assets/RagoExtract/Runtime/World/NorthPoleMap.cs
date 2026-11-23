using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// THE NORTH POLE (Christmas map): Santa's village taken over by evil elves. Snow everywhere, candy-cane posts along
    /// the main avenue, a giant Christmas tree in the square, the elves' workshops, the Toy Factory, igloos, piles of
    /// presents (and lots of real ones to open) - and in the middle of it all the GIANT PRESENT: a vault wrapped in red
    /// paper and gold ribbon that only the North Pole Keycard opens (found inside presents). Evil Santa holds his house
    /// at the north end of the avenue.
    /// </summary>
    public class NorthPoleMap : MapBase
    {
        static Material Wrap => Mats.Lit("np_wrap", Color.white, 0.1f, 0.55f, Mats.Noise, null, 0.08f, 0.05f, true, 0.5f);
        static Material Ribbon => Mats.Lit("np_ribbon", new Color(0.95f, 0.75f, 0.22f), 0.7f, 0.7f, Mats.Noise, null, 0.05f, 0.05f);
        static Material Candy => Mats.Lit("np_candy", Color.white, 0f, 0.6f, Mats.Noise, null, 0.05f, 0.05f);
        static Material Ice => Mats.Lit("np_ice", new Color(0.75f, 0.88f, 0.98f), 0.1f, 0.85f, Mats.Noise, null, 0.02f, 0.05f, true, 0.5f);
        static Material Glow => Mats.Lit("xmas_glow", new Color(1f, 0.9f, 0.6f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(1.6f, 1.3f, 0.7f));

        public override void Build()
        {
            Ambience = Synth.Amb.Forest;
            NavBounds = new Bounds(new Vector3(0, 6, 0), new Vector3(200, 40, 200));
            Lighting(new Color(0.95f, 0.95f, 1f), 0.9f, new Vector3(28, 140, 0), new Color(0.6f, 0.66f, 0.78f), new Color(0.55f, 0.58f, 0.64f), new Color(0.45f, 0.47f, 0.5f),
                new Color(0.78f, 0.82f, 0.88f), 0.01f, new Color(0.5f, 0.6f, 0.75f), new Color(0.85f, 0.88f, 0.92f), 0.7f, 0.6f);
            var b = B;
            b.Ground(Vector3.zero, new Vector2(180, 180), Festive.Snow, Synth.Surface.Grass);
            var pts = new List<Vector3>();

            // ---- the main avenue (north-south) and the ring road round the square, packed icy snow
            b.Box(new Vector3(0, 0.01f, 0), new Vector3(8, 0.02f, 180), Ice, Color.white, null, false);
            b.Box(new Vector3(0, 0.012f, 0), new Vector3(180, 0.02f, 7), Ice, Color.white, null, false);
            // candy-cane posts along both roads, a light on every other one
            for (int i = -8; i <= 8; i++)
            {
                if (i == 0) continue;
                foreach (float sx in new[] { -5.2f, 5.2f })
                {
                    CandyPost(new Vector3(sx, 0, i * 10f));
                    CandyPost(new Vector3(i * 10f, 0, sx * 0.9f));
                }
                if (i % 2 == 0) b.LightPole(new Vector3(6.2f, 0, i * 10f + 5f), 5.5f, new Color(1f, 0.82f, 0.6f), i % 4 == 0, 12f);
            }

            // ---- the square: a giant Christmas tree and presents piled round it
            Festive.Tree(transform, new Vector3(-14, 0, -14), 4.2f, 501);
            for (int i = 0; i < 14; i++) GiantGift(new Vector3(-14, 0, -14) + Quaternion.Euler(0, i * 26f, 0) * new Vector3(0, 0, b.Rng.Range(4.6f, 6.5f)), b.Rng.Range(0.5f, 1.3f), i);

            // ---- the GIANT PRESENT: the vault, wrapped (door faces the square, south)
            var vp = new Vector3(14, 0.02f, 14);
            var v = AddVault(vp, 0, false, Wrap, new Color(0.72f, 0.06f, 0.08f), 4.2f, true, 7f, 0.5f);
            if (v != null) v.CardId = "key_xmas";
            for (int k = Mathf.Max(0, Crates.Count - 3); k < Crates.Count; k++) if (Crates[k] != null) Crates[k].presentGrade = true;   // its three crates: packed with vault loot
            // gold ribbon round the sides and back, and over the roof; a huge bow on top
            b.Box(vp + new Vector3(-3.78f, 2.1f, 2.6f), new Vector3(0.06f, 0.7f, 5.4f), Ribbon, Color.white, null, false);
            b.Box(vp + new Vector3(3.78f, 2.1f, 2.6f), new Vector3(0.06f, 0.7f, 5.4f), Ribbon, Color.white, null, false);
            b.Box(vp + new Vector3(0, 2.1f, 5.45f), new Vector3(7.6f, 0.7f, 0.06f), Ribbon, Color.white, null, false);
            b.Box(vp + new Vector3(0, 4.48f, 2.6f), new Vector3(0.7f, 0.06f, 5.6f), Ribbon, Color.white, null, false);
            b.Box(vp + new Vector3(0, 4.48f, 2.6f), new Vector3(7.6f, 0.06f, 0.7f), Ribbon, Color.white, null, false);
            b.Sphere(vp + new Vector3(-1.2f, 5.1f, 2.6f), new Vector3(1.3f, 0.7f, 0.5f), 12, Ribbon, Color.white, false);
            b.Sphere(vp + new Vector3(1.2f, 5.1f, 2.6f), new Vector3(1.3f, 0.7f, 0.5f), 12, Ribbon, Color.white, false);
            b.Sphere(vp + new Vector3(0, 4.85f, 2.6f), new Vector3(0.5f, 0.45f, 0.5f), 10, Ribbon, Color.white, false);
            b.Sign(vp + new Vector3(0, 4.0f, -0.3f), Quaternion.identity, "THE GIANT PRESENT", new Color(1f, 0.85f, 0.4f), 0.32f);
            b.Sign(vp + new Vector3(0, 3.55f, -0.3f), Quaternion.identity, "north pole keycard only", new Color(0.95f, 0.95f, 0.92f), 0.13f);
            b.AddLight(vp + new Vector3(0, 3.2f, -3f), new Color(1f, 0.8f, 0.5f), 10f, 1.4f);

            // ---- elf workshops round the square (wooden, red and green), the Toy Factory to the south
            Color[] cabin = { new Color(0.62f, 0.12f, 0.1f), new Color(0.14f, 0.4f, 0.2f), new Color(0.7f, 0.6f, 0.45f) };
            var shops = new[] { new Vector3(-40, 0, -32), new Vector3(40, 0, -32), new Vector3(-40, 0, 32), new Vector3(40, 0, 32), new Vector3(-66, 0, 0), new Vector3(66, 0, 0), new Vector3(-66, 0, -60), new Vector3(66, 0, 60), new Vector3(-25, 0, 66), new Vector3(66, 0, -60) };
            for (int i = 0; i < shops.Length; i++)
            {
                float yaw = shops[i].x < 0 ? 90 : -90;
                pts.AddRange(b.Building(shops[i], yaw, new Vector2(11, b.Rng.Range(13f, 16f)), b.Rng.Range(1, 3), Maps.WoodPlank, cabin[i % cabin.Length], Maps.WoodPlank, Maps.WoodPlank, 0, true, false));
                Festive.Wreath(transform, shops[i] + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 2.7f, -7.2f), 0.4f);
                Festive.Tree(transform, shops[i] + Quaternion.Euler(0, yaw, 0) * new Vector3(4.5f, 0, -9.5f), 1.2f, 600 + i);
            }
            var factory = new Vector3(0, 0, -52);
            pts.AddRange(b.Building(factory, 0, new Vector2(24, 16), 2, Maps.Brick, new Color(0.75f, 0.55f, 0.5f), Maps.WoodPlank, Maps.Concrete, 0, true, true));
            b.Sign(factory + new Vector3(0, 7.4f, -8.4f), Quaternion.identity, "TOY FACTORY", new Color(0.95f, 0.85f, 0.4f), 0.5f);
            foreach (float x in new[] { -8f, 8f }) b.Cyl(factory + new Vector3(x, 6.4f, 4), factory + new Vector3(x, 11f, 4), 0.7f, 0.6f, 12, Maps.Brick, new Color(0.6f, 0.4f, 0.35f));   // chimneys
            Squad(factory + new Vector3(0, 0.2f, 0), 3, true); Squad(factory + new Vector3(-6, 3.4f, 3), 2);

            // ---- Santa's house at the north end of the avenue: Evil Santa's lair
            var house = new Vector3(0, 0, 62);
            pts.AddRange(b.Building(house, 180, new Vector2(18, 14), 2, Maps.WoodPlank, new Color(0.55f, 0.1f, 0.08f), Maps.WoodPlank, Maps.WoodPlank, 0, true, false));
            b.Sign(house + new Vector3(0, 7.2f, 7.4f), Quaternion.Euler(0, 180, 0), "SANTA'S HOUSE", new Color(1f, 0.9f, 0.6f), 0.45f);
            b.Cyl(house + new Vector3(5, 6.4f, 2), house + new Vector3(5, 9.5f, 2), 0.6f, 0.6f, 10, Maps.Brick, new Color(0.6f, 0.35f, 0.3f));
            Boss(house + new Vector3(0, 0.2f, 0), "Evil Santa", 0.05f);   // rare: 1 raid in 20
            Sleigh(house + new Vector3(-12, 0, -4), 20f);

            // ---- igloos, snowmen, ice blocks and more presents scattered about
            for (int i = 0; i < 6; i++)
            {
                if (!b.Spot(() => new Vector3(b.Rng.Range(-80f, 80f), 0, b.Rng.Range(-80f, 80f)), new Vector3(3.2f, 2f, 3.2f), 0, out var ip)) continue;
                Igloo(ip, b.Rng.Range(0f, 360f));
            }
            for (int i = 0; i < 10; i++)
                if (b.Spot(() => new Vector3(b.Rng.Range(-82f, 82f), 0, b.Rng.Range(-82f, 82f)), new Vector3(0.9f, 1.8f, 0.9f), 0, out var sp)) Snowman(sp, b.Rng.Range(0f, 360f));
            for (int i = 0; i < 26; i++)
                if (b.Spot(() => new Vector3(b.Rng.Range(-84f, 84f), 0, b.Rng.Range(-84f, 84f)), new Vector3(1.2f, 1.2f, 1.2f), 0, out var gp)) GiantGift(gp, b.Rng.Range(0.6f, 1.4f), 100 + i);
            for (int i = 0; i < 24; i++)
                if (b.Spot(() => new Vector3(b.Rng.Range(-86f, 86f), 0, b.Rng.Range(-86f, 86f)), new Vector3(1.4f, 2.4f, 1.4f), 0, out var tp)) Festive.Tree(transform, tp, b.Rng.Range(1.1f, 2f), 700 + i);
            for (int i = 0; i < 14; i++)
                if (b.Spot(() => new Vector3(b.Rng.Range(-80f, 80f), 0, b.Rng.Range(-80f, 80f)), new Vector3(1.6f, 1.2f, 1.6f), 0, out var cp)) b.Crate(cp, b.Rng.Range(0f, 90f), new Vector3(1f, 0.8f, 1f));

            ScatterLoot(pts, 0.75f);
            AddExtract(new Vector3(0, 0, -86), "Reindeer Stables");
            AddExtract(new Vector3(-86, 0, 40), "Sleigh Runway");
            AddExtract(new Vector3(84, 0, -20), "Ice Road");
            // player spawns round the edge, facing in
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2 + 0.3f;
                var p = new Vector3(Mathf.Cos(a) * 80f, 0.1f, Mathf.Sin(a) * 80f);
                Spawn(p, Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg);
            }
            // evil elves everywhere
            Squad(new Vector3(-14, 0, -6), 3); Squad(new Vector3(14, 0, 5), 2, true); Squad(new Vector3(-40, 0, -20), 2); Squad(new Vector3(40, 0, -20), 3);
            Squad(new Vector3(-40, 0, 20), 2); Squad(new Vector3(40, 0, 20), 2); Squad(new Vector3(-66, 0, 10), 2); Squad(new Vector3(66, 0, -10), 2);
            Squad(new Vector3(0, 0, 40), 3, true); Squad(new Vector3(-30, 0, 66), 2); Squad(new Vector3(60, 0, 60), 2); Squad(new Vector3(-60, 0, -60), 2);
            // snowfields and forest round the village: 800 m
            Outskirts(new Rect(-90, -90, 180, 180), 800, new[] { "Frozen Lake", "Weather Station", "Ice Cave", "Polar Camp" }, true, false, Festive.Snow, 14);
        }

        void CandyPost(Vector3 p)
        {
            var b = B;
            for (int k = 0; k < 6; k++) b.Cyl(p + Vector3.up * (k * 0.4f), p + Vector3.up * (k * 0.4f + 0.4f), 0.08f, 0.08f, 10, Candy, k % 2 == 0 ? new Color(0.85f, 0.08f, 0.08f) : new Color(0.96f, 0.96f, 0.95f), k == 0);
            for (int k = 0; k < 5; k++)
            {
                float a = k / 4f * Mathf.PI, a2 = (k + 1) / 4f * Mathf.PI;
                if (k == 4) break;
                var c = p + Vector3.up * 2.4f;
                b.Cyl(c + new Vector3(0.3f - Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, 0), c + new Vector3(0.3f - Mathf.Cos(a2) * 0.3f, Mathf.Sin(a2) * 0.3f, 0), 0.08f, 0.08f, 10, Candy, k % 2 == 0 ? new Color(0.96f, 0.96f, 0.95f) : new Color(0.85f, 0.08f, 0.08f), false);
            }
        }

        /// <summary>A decorative wrapped box (solid - you can take cover behind the big ones).</summary>
        void GiantGift(Vector3 p, float s, int i)
        {
            var b = B;
            Color[] wraps = { new Color(0.72f, 0.06f, 0.08f), new Color(0.1f, 0.42f, 0.18f), new Color(0.15f, 0.25f, 0.65f), new Color(0.85f, 0.7f, 0.2f), new Color(0.85f, 0.85f, 0.88f), new Color(0.5f, 0.12f, 0.55f) };
            Color w = wraps[i % wraps.Length], r = wraps[(i + 3) % wraps.Length];
            var q = Quaternion.Euler(0, i * 37f, 0);
            b.Box(p + Vector3.up * s * 0.5f, Vector3.one * s, Wrap, w, q, true, Synth.Surface.Wood);
            b.Box(p + Vector3.up * s * 0.5f, new Vector3(s * 0.14f, s + 0.01f, s + 0.01f), Wrap, r, q, false);
            b.Box(p + Vector3.up * s * 0.5f, new Vector3(s + 0.01f, s + 0.01f, s * 0.14f), Wrap, r, q, false);
            b.Sphere(p + Vector3.up * (s + 0.08f * s), new Vector3(0.22f, 0.12f, 0.22f) * s, 8, Wrap, r, false);
        }

        void Igloo(Vector3 p, float yaw)
        {
            var b = B;
            b.Sphere(p, new Vector3(2.6f, 2.2f, 2.6f), 16, Festive.Snow, Color.white, true, Synth.Surface.Grass);
            var q = Quaternion.Euler(0, yaw, 0);
            b.Box(p + q * new Vector3(0, 0.7f, 2.6f), new Vector3(1.3f, 1.4f, 1.4f), Festive.Snow, new Color(0.92f, 0.94f, 0.97f), q, true, Synth.Surface.Grass);
        }

        void Snowman(Vector3 p, float yaw)
        {
            var b = B;
            var q = Quaternion.Euler(0, yaw, 0);
            b.Sphere(p + Vector3.up * 0.4f, Vector3.one * 0.45f, 12, Festive.Snow, Color.white, true, Synth.Surface.Grass);
            b.Sphere(p + Vector3.up * 1.0f, Vector3.one * 0.32f, 12, Festive.Snow, Color.white, false);
            b.Sphere(p + Vector3.up * 1.45f, Vector3.one * 0.22f, 12, Festive.Snow, Color.white, false);
            b.Cyl(p + Vector3.up * 1.45f + q * new Vector3(0, 0, 0.2f), p + Vector3.up * 1.44f + q * new Vector3(0, 0, 0.42f), 0.04f, 0.005f, 8, Candy, new Color(0.95f, 0.45f, 0.08f), false);
            b.Cyl(p + Vector3.up * 1.62f, p + Vector3.up * 1.92f, 0.16f, 0.16f, 12, Maps.Metal, new Color(0.05f, 0.05f, 0.05f), false);
            b.Cyl(p + Vector3.up * 1.6f, p + Vector3.up * 1.63f, 0.26f, 0.26f, 12, Maps.Metal, new Color(0.05f, 0.05f, 0.05f), false);
        }

        void Sleigh(Vector3 p, float yaw)
        {
            var b = B;
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => p + q * new Vector3(x, y, z);
            b.Box(P(0, 0.9f, 0), new Vector3(1.8f, 0.8f, 3.2f), Wrap, new Color(0.7f, 0.06f, 0.06f), q, true, Synth.Surface.Wood);
            b.Box(P(0, 1.5f, -1.2f), new Vector3(1.8f, 0.9f, 0.3f), Wrap, new Color(0.7f, 0.06f, 0.06f), q, true, Synth.Surface.Wood);
            foreach (float sx in new[] { -0.8f, 0.8f }) b.Box(P(sx, 0.2f, 0.2f), new Vector3(0.08f, 0.08f, 3.8f), Ribbon, Color.white, q, false);
            for (int i = 0; i < 5; i++) GiantGift(P(-0.4f + (i % 2) * 0.8f, 1.3f, -0.4f + i * 0.35f), 0.45f, 200 + i);
        }
    }
}
