using System;
using UnityEngine;
using Rand = System.Random;

namespace Flopzone
{
    /// <summary>Building blocks for maps: boxes, ramps, crates, trees, buildings, lamps.</summary>
    public class MapKit
    {
        public readonly Transform S;   // static geometry (batched)
        public readonly Transform D;   // moving / interactive things
        public readonly Rand r;
        public readonly World world;

        public MapKit(World w, int seed)
        {
            world = w;
            S = Prims.Empty("static", w.transform).transform;
            D = Prims.Empty("dynamic", w.transform).transform;
            w.dynamicRoot = D;
            r = new Rand(seed);
        }

        public static Material M(string hex, float smooth = 0.2f, float metal = 0f) => Prims.Lit(Prims.Hex(hex), smooth, metal);
        public static Material Glow(string hex, float k = 1.6f) => Prims.Emissive(Prims.Hex(hex), k);

        public float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
        public int Ri(int a, int b) => r.Next(a, b);
        public T Pick<T>(params T[] items) => items[r.Next(items.Length)];

        public GameObject Box(Vector3 center, Vector3 size, Material m, float yaw = 0f, bool collider = true)
            => Prims.Box(S, center, size, m, collider, Quaternion.Euler(0, yaw, 0));

        public GameObject BoxRot(Vector3 center, Vector3 size, Material m, Quaternion rot, bool collider = true)
            => Prims.Box(S, center, size, m, collider, rot);

        /// <summary>Box sitting on the ground at pos (pos.y = floor height).</summary>
        public GameObject Block(Vector3 pos, Vector3 size, Material m, float yaw = 0f)
            => Box(pos + Vector3.up * size.y * 0.5f, size, m, yaw);

        public void Ground(float halfX, float halfZ, Material m, float y = 0f)
        {
            Box(new Vector3(0, y - 0.5f, 0), new Vector3(halfX * 2, 1f, halfZ * 2), m);
        }

        /// <summary>Walls around the play area.</summary>
        public void Perimeter(float halfX, float halfZ, float h, Material m, float thick = 1f)
        {
            Box(new Vector3(0, h / 2, halfZ + thick / 2), new Vector3(halfX * 2 + thick * 2, h, thick), m);
            Box(new Vector3(0, h / 2, -halfZ - thick / 2), new Vector3(halfX * 2 + thick * 2, h, thick), m);
            Box(new Vector3(halfX + thick / 2, h / 2, 0), new Vector3(thick, h, halfZ * 2), m);
            Box(new Vector3(-halfX - thick / 2, h / 2, 0), new Vector3(thick, h, halfZ * 2), m);
        }

        /// <summary>Invisible walls (for open maps), plus a tall one so nobody jumps out.</summary>
        public void InvisibleFence(float halfX, float halfZ, float h = 12f)
        {
            foreach (var (c, s) in new[] {
                (new Vector3(0, h / 2, halfZ + 0.5f), new Vector3(halfX * 2 + 2, h, 1)),
                (new Vector3(0, h / 2, -halfZ - 0.5f), new Vector3(halfX * 2 + 2, h, 1)),
                (new Vector3(halfX + 0.5f, h / 2, 0), new Vector3(1, h, halfZ * 2)),
                (new Vector3(-halfX - 0.5f, h / 2, 0), new Vector3(1, h, halfZ * 2)) })
            {
                var go = new GameObject("fence");
                go.transform.SetParent(S, false);
                go.transform.localPosition = c;
                go.AddComponent<BoxCollider>().size = s;
            }
        }

        /// <summary>A sloped walkway from bottom (at the low end) going `length` along yaw, rising `height`.</summary>
        public void Ramp(Vector3 bottom, float yaw, float length, float width, float height, Material m)
        {
            float slopeLen = Mathf.Sqrt(length * length + height * height);
            float angle = Mathf.Atan2(height, length) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(-angle, 0, 0);
            var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var center = bottom + dir * length * 0.5f + Vector3.up * (height * 0.5f - 0.1f);
            BoxRot(center, new Vector3(width, 0.25f, slopeLen), m, rot);
        }

        public void Stairs(Vector3 bottom, float yaw, int steps, float stepH, float stepD, float width, Material m)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < steps; i++)
            {
                var c = bottom + rot * new Vector3(0, stepH * (i + 0.5f), stepD * (i + 0.5f));
                BoxRot(c, new Vector3(width, stepH, stepD), m, rot);
            }
            // a smooth invisible slope on top so walking up never snags
            float len = steps * stepD, h = steps * stepH;
            float slope = Mathf.Sqrt(len * len + h * h);
            var srot = rot * Quaternion.Euler(-Mathf.Atan2(h, len) * Mathf.Rad2Deg, 0, 0);
            var go = new GameObject("stairSlope");
            go.transform.SetParent(S, false);
            go.transform.SetPositionAndRotation(bottom + rot * new Vector3(0, h * 0.5f + 0.02f, len * 0.5f), srot);
            go.AddComponent<BoxCollider>().size = new Vector3(width, 0.05f, slope);
        }

        public void Crate(Vector3 pos, float size, float yaw)
        {
            var wood = M(Pick("b07a45", "a06c3c", "9a7044"), 0.15f);
            var edge = M("6e4a28", 0.15f);
            var c = Block(pos, Vector3.one * size, wood, yaw);
            var t = c.transform;
            float e = 0.06f / size;
            // darker edge planks make it read as a crate
            foreach (var v in new[] { new Vector3(0, 0.5f, 0.5f), new Vector3(0, -0.5f, 0.5f), new Vector3(0, 0.5f, -0.5f), new Vector3(0, -0.5f, -0.5f) })
                Prims.Box(t, v, new Vector3(1.02f, e, e), edge, false);
            foreach (var v in new[] { new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, -0.5f) })
                Prims.Box(t, v, new Vector3(e, 1.02f, e), edge, false);
            Prims.Box(t, new Vector3(0, 0, 0.5f), new Vector3(1.0f, e, e), edge, false, Quaternion.Euler(0, 0, 45));
            Prims.Box(t, new Vector3(0, 0, -0.5f), new Vector3(1.0f, e, e), edge, false, Quaternion.Euler(0, 0, -45));
        }

        public void CrateStack(Vector3 pos, float yaw)
        {
            float s = R(1.0f, 1.3f);
            Crate(pos, s, yaw);
            if (r.NextDouble() < 0.6) Crate(pos + Quaternion.Euler(0, yaw, 0) * new Vector3(s * 1.02f, 0, R(-0.2f, 0.2f)), s * 0.9f, yaw + R(-12, 12));
            if (r.NextDouble() < 0.5) Crate(pos + Vector3.up * s, s * 0.85f, yaw + R(-20, 20));
        }

        public void Barrel(Vector3 pos, string hex)
        {
            var m = M(hex, 0.45f, 0.3f);
            var b = Prims.Cyl(S, pos + Vector3.up * 0.55f, 0.75f, 1.1f, m, Vector3.up, true);
            Prims.Cyl(b.transform, new Vector3(0, 0.6f, 0), 1.03f, 0.05f, M("2a2a2a", 0.4f, 0.5f), Vector3.up);
            Prims.Cyl(b.transform, new Vector3(0, -0.6f, 0), 1.03f, 0.05f, M("2a2a2a", 0.4f, 0.5f), Vector3.up);
        }

        public void Container(Vector3 pos, float yaw, string hex, bool doorsOpen = false)
        {
            var m = M(hex, 0.3f, 0.35f);
            var rib = M(hex, 0.25f, 0.2f);
            var size = new Vector3(2.5f, 2.6f, 6.1f);
            var c = Block(pos, size, m, yaw);
            for (int i = 0; i < 9; i++)
            {
                float z = -0.45f + i * 0.1125f;
                Prims.Box(c.transform, new Vector3(0.505f, 0, z), new Vector3(0.02f, 0.94f, 0.03f), rib, false);
                Prims.Box(c.transform, new Vector3(-0.505f, 0, z), new Vector3(0.02f, 0.94f, 0.03f), rib, false);
            }
            Prims.Box(c.transform, new Vector3(0, 0.505f, 0), new Vector3(1.01f, 0.02f, 1.01f), M("2d2d2d", 0.3f, 0.3f), false);
        }

        public void Tree(Vector3 pos, float h, string leafHex)
        {
            var trunk = M("6b4a2e", 0.1f);
            var leaf = M(leafHex, 0.1f);
            Prims.Cyl(S, pos + Vector3.up * h * 0.35f, 0.35f * h / 5f + 0.15f, h * 0.7f, trunk, Vector3.up, true);
            int blobs = Ri(3, 6);
            for (int i = 0; i < blobs; i++)
            {
                var off = new Vector3(R(-1, 1), R(-0.3f, 0.6f), R(-1, 1)) * h * 0.18f;
                float s = h * R(0.32f, 0.5f);
                Prims.Ball(S, pos + Vector3.up * h * 0.75f + off, new Vector3(s, s * 0.85f, s), leaf);
            }
        }

        public void Pine(Vector3 pos, float h)
        {
            var trunk = M("5a3c22", 0.1f);
            var needle = M(Pick("2f5d3a", "2a5233", "36694a"), 0.1f);
            var snow = M("f2f6ff", 0.3f);
            Prims.Cyl(S, pos + Vector3.up * h * 0.2f, 0.3f, h * 0.4f, trunk, Vector3.up, true);
            for (int i = 0; i < 4; i++)
            {
                float k = 1f - i * 0.22f;
                var y = pos.y + h * (0.3f + i * 0.17f);
                var tier = Prims.Box(S, new Vector3(pos.x, y, pos.z), new Vector3(h * 0.42f * k, h * 0.2f, h * 0.42f * k), needle, false, Quaternion.Euler(0, 45 + i * 20, 0));
                if (i == 3 || R(0, 1) < 0.5f) Prims.Box(tier.transform, new Vector3(0, 0.5f, 0), new Vector3(0.8f, 0.12f, 0.8f), snow, false);
            }
        }

        public void Palm(Vector3 pos, float h)
        {
            var trunk = M("8a6a45", 0.15f);
            var leaf = M(Pick("3f8f3a", "4e9a3b"), 0.2f);
            int segs = 6;
            Vector3 p = pos;
            float lean = R(-12, 12);
            for (int i = 0; i < segs; i++)
            {
                var next = p + Quaternion.Euler(0, 0, lean) * Vector3.up * (h / segs);
                Prims.Cyl(S, (p + next) * 0.5f, 0.32f - i * 0.03f, h / segs + 0.05f, trunk, next - p, i == 0);
                p = next;
            }
            for (int i = 0; i < 7; i++)
            {
                var rot = Quaternion.Euler(R(15, 35), i * 360f / 7f, 0);
                var frond = Prims.Box(S, p + rot * new Vector3(0, 0, 1.2f), new Vector3(0.6f, 0.06f, 2.6f), leaf, false, rot);
                frond.name = "frond";
            }
        }

        public void Bush(Vector3 pos, float s, string hex)
        {
            var m = M(hex, 0.1f);
            for (int i = 0; i < 3; i++)
                Prims.Ball(S, pos + new Vector3(R(-0.4f, 0.4f) * s, s * 0.35f, R(-0.4f, 0.4f) * s), new Vector3(s, s * 0.8f, s) * R(0.7f, 1f), m);
        }

        public void Rock(Vector3 pos, float s, string hex)
        {
            var m = M(hex, 0.15f);
            var rot = Quaternion.Euler(R(0, 40), R(0, 360), R(0, 40));
            Prims.Box(S, pos + Vector3.up * s * 0.3f, new Vector3(s, s * 0.7f, s * 0.9f), m, true, rot);
        }

        public void Lamp(Vector3 pos, string glowHex, float h = 3.6f)
        {
            var pole = M("2b2d33", 0.5f, 0.6f);
            Prims.Cyl(S, pos + Vector3.up * h * 0.5f, 0.12f, h, pole, Vector3.up, true);
            Prims.Box(S, pos + new Vector3(0, h, 0), new Vector3(0.5f, 0.12f, 0.5f), pole, false);
            Prims.Box(S, pos + new Vector3(0, h - 0.12f, 0), new Vector3(0.36f, 0.1f, 0.36f), Glow(glowHex, 2.5f), false);
        }

        /// <summary>A building block with window strips.</summary>
        public GameObject Building(Vector3 pos, Vector3 size, string wallHex, string windowHex, bool glow, bool collider = true)
        {
            var wall = M(wallHex, 0.2f);
            var win = glow ? Glow(windowHex, 1.3f) : M(windowHex, 0.85f, 0.4f);
            var b = Box(pos + Vector3.up * size.y * 0.5f, size, wall, 0f, collider);
            int floors = Mathf.Max(1, Mathf.FloorToInt(size.y / 3.2f));
            for (int f = 0; f < floors; f++)
            {
                float y = -0.5f + (f + 0.6f) / floors;
                float hh = 1.2f / size.y;
                Prims.Box(b.transform, new Vector3(0, y, 0.502f), new Vector3(0.85f, hh, 0.01f), win, false);
                Prims.Box(b.transform, new Vector3(0, y, -0.502f), new Vector3(0.85f, hh, 0.01f), win, false);
                Prims.Box(b.transform, new Vector3(0.502f, y, 0), new Vector3(0.01f, hh, 0.85f), win, false);
                Prims.Box(b.transform, new Vector3(-0.502f, y, 0), new Vector3(0.01f, hh, 0.85f), win, false);
            }
            return b;
        }

        public void Wall(Vector3 a, Vector3 b, float h, float thick, Material m)
        {
            var d = b - a;
            d.y = 0;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Box((a + b) * 0.5f + Vector3.up * h * 0.5f, new Vector3(thick, h, d.magnitude), m, yaw);
        }

        /// <summary>A wall with merlons on top (castle / desert style).</summary>
        public void Battlement(Vector3 a, Vector3 b, float h, float thick, Material m)
        {
            Wall(a, b, h, thick, m);
            var d = b - a;
            int n = Mathf.FloorToInt(d.magnitude / 1.6f);
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 0.5f) / n);
                Box(p + Vector3.up * (h + 0.35f), new Vector3(thick, 0.7f, 0.8f), m, yaw);
            }
        }

        public void Railing(Vector3 a, Vector3 b, Material m)
        {
            var d = b - a;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Box((a + b) * 0.5f + Vector3.up * 1.0f, new Vector3(0.06f, 0.06f, d.magnitude), m, yaw);
            Box((a + b) * 0.5f + Vector3.up * 0.5f, new Vector3(0.04f, 1.0f, d.magnitude), Prims.Fade(new Color(0.5f, 0.6f, 0.7f, 0.25f)), yaw);
            int posts = Mathf.Max(2, Mathf.CeilToInt(d.magnitude / 2f) + 1);
            for (int i = 0; i < posts; i++)
                Box(Vector3.Lerp(a, b, i / (float)(posts - 1)) + Vector3.up * 0.5f, new Vector3(0.07f, 1.0f, 0.07f), m, yaw, false);
        }

        public TextMesh Sign(Vector3 pos, float yaw, string text, float h, Color c)
        {
            var go = Prims.Empty("sign", S, pos);
            go.transform.rotation = Quaternion.Euler(0, yaw + 180f, 0);
            return UIKit.Text(go.transform, text, Vector3.zero, h, c);
        }

        /// <summary>Place something and its point-mirror (so both teams get the same map).</summary>
        public void Sym(Action<Vector3, float> place, Vector3 p, float yaw)
        {
            place(p, yaw);
            place(new Vector3(-p.x, p.y, -p.z), yaw + 180f);
        }

        /// <summary>Standard spawn points: a cluster at each team's end, a ring for free-for-all.</summary>
        public void DefaultSpawns(float baseZ, float ringRadius, float y = 0f, float spreadX = 6f)
        {
            for (int t = 0; t < 2; t++)
            {
                float z = t == 0 ? -baseZ : baseZ;
                for (int i = 0; i < 8; i++)
                {
                    float x = (i % 4 - 1.5f) * spreadX / 2f;
                    float dz = (i / 4) * 2.5f * (t == 0 ? -1 : 1);
                    world.teamSpawns[t].Add(new Vector3(x, y, z + dz));
                }
                world.flagBase[t] = new Vector3(0, y, t == 0 ? -baseZ - 4f : baseZ + 4f);
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2 / 12;
                world.ffaSpawns.Add(new Vector3(Mathf.Cos(a) * ringRadius, y, Mathf.Sin(a) * ringRadius));
            }
        }

        /// <summary>Colored pads that show each team's base.</summary>
        public void BasePads()
        {
            for (int t = 0; t < 2; t++)
            {
                var c = Avatar.TeamColor(t);
                var p = world.flagBase[t];
                var pad = Prims.Cyl(S, p + Vector3.up * 0.03f, 3.2f, 0.06f, Prims.Emissive(c * 0.6f, 0.8f), Vector3.up, false);
                pad.name = "basePad";
            }
        }

        /// <summary>Distant scenery with no collision.</summary>
        public void Backdrop(int count, float minR, float maxR, Func<Vector3, GameObject> make)
        {
            for (int i = 0; i < count; i++)
            {
                float a = R(0, Mathf.PI * 2);
                float d = R(minR, maxR);
                var go = make(new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d));
                if (go != null) foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            }
        }
    }
}
