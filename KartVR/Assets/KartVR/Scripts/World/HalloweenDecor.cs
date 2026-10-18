using System;
using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Spooky season: through October (and Halloween night into Nov 1) every map gets glowing jack-o'-lanterns,
    /// floating ghosts and circling bats, and the hub garage gets orange/purple string lights and a banner.
    /// Pure decoration built at runtime on this device only (no colliders, nothing networked), rebuilt when you
    /// change map. Turn it on outside October with the static Force flag (handy for testing).
    /// </summary>
    public class HalloweenDecor : MonoBehaviour
    {
        public static bool Force;

        public static bool IsSeason
        {
            get
            {
                var now = DateTime.Now;
                return Force || now.Month == 10 || (now.Month == 11 && now.Day == 1);
            }
        }

        static readonly Color Orange = new Color(1f, 0.45f, 0.05f), Purple = new Color(0.55f, 0.2f, 0.9f);

        KartMode built = (KartMode)255;
        Transform root;
        readonly List<Transform> ghosts = new List<Transform>(), bats = new List<Transform>();
        readonly List<Vector3> ghostHome = new List<Vector3>();
        Vector3 batCenter;
        Material pumpkin, stem, face, ghost, ghostEye, bat;
        float nextCheck;

        void Update()
        {
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 1f;
                var mode = IsSeason ? GameState.CurrentMode : (KartMode)255;
                if (mode != built) Rebuild(mode);
            }
            Animate();
        }

        void Rebuild(KartMode mode)
        {
            built = mode;
            if (root != null) Destroy(root.gameObject);
            ghosts.Clear(); bats.Clear(); ghostHome.Clear();
            if (mode == (KartMode)255) return;
            if (pumpkin == null)
            {
                pumpkin = KartUI.Mat(Orange, 0.5f);
                stem = KartUI.Mat(new Color(0.25f, 0.18f, 0.08f), 0.1f);
                face = KartUI.Glow(new Color(1f, 0.75f, 0.2f));
                ghost = KartUI.Mat(new Color(0.92f, 0.95f, 1f), 0.4f, 0f, new Color(0.35f, 0.4f, 0.5f));
                ghostEye = KartUI.Mat(new Color(0.05f, 0.05f, 0.08f), 0.2f);
                bat = KartUI.Mat(new Color(0.06f, 0.05f, 0.08f), 0.2f);
            }
            root = new GameObject("Halloween").transform;
            var map = KartMaps.Get(mode);
            var rng = new System.Random((int)mode * 97 + 5);

            if (mode == KartMode.Hub) { BuildHub(map.center, rng); return; }
            if (mode == KartMode.Backrooms) return; // nothing festive in the Backrooms

            // Jack-o'-lanterns dotted around, sat on whatever ground is there.
            int pumpkins = Mathf.Clamp((int)(map.radius / 4f), 25, 70);
            for (int i = 0; i < pumpkins; i++)
            {
                if (!GroundPoint(map, rng, 0.85f, out var p)) continue;
                JackOLantern(p, 0.8f + (float)rng.NextDouble() * 0.9f, (float)rng.NextDouble() * 360f);
            }
            // Ghosts bobbing about above head height.
            for (int i = 0; i < 10; i++)
            {
                if (!GroundPoint(map, rng, 0.7f, out var p)) continue;
                var home = p + Vector3.up * (3f + (float)rng.NextDouble() * 4f);
                ghosts.Add(Ghost(home, 1f + (float)rng.NextDouble() * 0.8f));
                ghostHome.Add(home);
            }
            // A flock of bats circling over the middle.
            batCenter = map.center + Vector3.up * 25f;
            for (int i = 0; i < 14; i++) bats.Add(Bat(0.8f + (float)rng.NextDouble() * 0.6f));
        }

        void BuildHub(Vector3 center, System.Random rng)
        {
            // The garage is 46 m x 46 m, 9 m tall (see KartWorldBuilder.Maps BuildHub): drape string lights round the top.
            const float halfX = 22.6f, north = 21.6f, south = -23.6f, y = 8.3f;
            var corners = new[] { new Vector3(-halfX, y, south), new Vector3(halfX, y, south), new Vector3(halfX, y, north), new Vector3(-halfX, y, north) };
            var bulbs = new[] { KartUI.Glow(Orange), KartUI.Glow(Purple), KartUI.Glow(new Color(0.4f, 1f, 0.3f)) };
            var wire = KartUI.Mat(new Color(0.05f, 0.05f, 0.05f), 0.2f);
            int n = 0;
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side] + center, b = corners[(side + 1) % 4] + center;
                int count = Mathf.RoundToInt(Vector3.Distance(a, b) / 1.2f);
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count;
                    // Sagging between hooks every 6 bulbs.
                    float sag = Mathf.Sin((i % 6 + 0.5f) / 6f * Mathf.PI) * 0.6f;
                    var p = Vector3.Lerp(a, b, t) + Vector3.down * sag;
                    KartUI.Part(root, PrimitiveType.Sphere, p, Vector3.one * 0.22f, bulbs[n++ % bulbs.Length]);
                    KartUI.Part(root, PrimitiveType.Cube, p + Vector3.up * 0.14f, new Vector3(0.03f, 0.1f, 0.03f), wire);
                }
            }
            // Banner above the map board.
            var banner = KartUI.Part(root, PrimitiveType.Cube, center + new Vector3(0f, 8.35f, north - 0.05f), new Vector3(16f, 0.9f, 0.06f), KartUI.Mat(new Color(0.12f, 0.04f, 0.18f), 0.3f));
            var label = KartUI.Label(root, "<b>HAPPY HALLOWEEN!</b>", center + new Vector3(0f, 8.35f, north - 0.1f), 7f, Orange, new Vector2(16f, 0.9f));
            label.transform.rotation = Quaternion.identity;
            banner.name = "HalloweenBanner";
            // Pumpkins in the corners and along the walls, ghosts floating near the roof.
            for (int i = 0; i < 14; i++)
            {
                float x = (i % 7 - 3f) * 6.5f;
                float z = i < 7 ? north - 1.2f : south + 1.2f;
                JackOLantern(center + new Vector3(x, 0f, z), 0.9f + (float)rng.NextDouble() * 0.5f, i < 7 ? 0f : 180f);
            }
            for (int i = 0; i < 5; i++)
            {
                var home = center + new Vector3((i - 2f) * 8f, 6.5f, -1f + (i % 2) * 6f);
                ghosts.Add(Ghost(home, 0.9f));
                ghostHome.Add(home);
            }
        }

        /// <summary>A random spot on the map's floor, found by casting down from above.</summary>
        static bool GroundPoint(KartMaps.Map map, System.Random rng, float spread, out Vector3 p)
        {
            for (int tries = 0; tries < 4; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rng.NextDouble()) * map.radius * spread;
                var top = map.center + new Vector3(Mathf.Cos(a) * d, 60f, Mathf.Sin(a) * d);
                if (Physics.Raycast(top, Vector3.down, out var hit, 90f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.8f
                    && hit.point.y > map.center.y - 3f)
                {
                    p = hit.point;
                    return true;
                }
            }
            p = default(Vector3);
            return false;
        }

        void JackOLantern(Vector3 pos, float s, float yaw)
        {
            var g = new GameObject("JackOLantern").transform;
            g.SetParent(root, false);
            g.position = pos;
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.localScale = Vector3.one * s;
            KartUI.Part(g, PrimitiveType.Sphere, new Vector3(0f, 0.32f, 0f), new Vector3(0.75f, 0.6f, 0.75f), pumpkin);
            KartUI.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f), new Vector3(0.08f, 0.08f, 0.08f), stem);
            foreach (float x in new[] { -0.14f, 0.14f })
                KartUI.Part(g, PrimitiveType.Cube, new Vector3(x, 0.4f, -0.36f), new Vector3(0.11f, 0.1f, 0.04f), face, new Vector3(0f, 0f, 45f));
            KartUI.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.22f, -0.355f), new Vector3(0.34f, 0.07f, 0.04f), face);
        }

        Transform Ghost(Vector3 pos, float s)
        {
            var g = new GameObject("Ghost").transform;
            g.SetParent(root, false);
            g.position = pos;
            g.localScale = Vector3.one * s;
            KartUI.Part(g, PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.7f, 0.7f), ghost);
            KartUI.Part(g, PrimitiveType.Capsule, new Vector3(0f, -0.15f, 0f), new Vector3(0.7f, 0.5f, 0.7f), ghost);
            foreach (float x in new[] { -0.2f, 0f, 0.2f })
                KartUI.Part(g, PrimitiveType.Sphere, new Vector3(x, -0.62f, 0f), new Vector3(0.26f, 0.3f, 0.26f), ghost);
            foreach (float x in new[] { -0.12f, 0.12f })
                KartUI.Part(g, PrimitiveType.Sphere, new Vector3(x, 0.38f, -0.31f), new Vector3(0.1f, 0.16f, 0.06f), ghostEye);
            KartUI.Part(g, PrimitiveType.Sphere, new Vector3(0f, 0.18f, -0.32f), new Vector3(0.12f, 0.12f, 0.05f), ghostEye);
            return g;
        }

        Transform Bat(float s)
        {
            var g = new GameObject("Bat").transform;
            g.SetParent(root, false);
            g.localScale = Vector3.one * s;
            KartUI.Part(g, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.25f, 0.22f, 0.4f), bat);
            foreach (float side in new[] { -1f, 1f })
            {
                var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                pivot.SetParent(g, false);
                KartUI.Part(pivot, PrimitiveType.Cube, new Vector3(side * 0.4f, 0f, 0f), new Vector3(0.7f, 0.03f, 0.35f), bat);
            }
            return g;
        }

        void Animate()
        {
            float t = Time.time;
            var head = KartRig.Instance != null ? KartRig.Instance.head : null;
            for (int i = 0; i < ghosts.Count; i++)
            {
                var g = ghosts[i];
                if (g == null) continue;
                float ph = i * 1.7f;
                g.position = ghostHome[i] + new Vector3(Mathf.Sin(t * 0.4f + ph) * 2f, Mathf.Sin(t * 1.3f + ph) * 0.4f, Mathf.Cos(t * 0.3f + ph) * 2f);
                // Ghosts turn to stare at you.
                if (head != null)
                {
                    Vector3 away = g.position - head.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.01f) g.rotation = Quaternion.LookRotation(away);
                }
            }
            for (int i = 0; i < bats.Count; i++)
            {
                var b = bats[i];
                if (b == null) continue;
                float speed = 0.35f + (i % 4) * 0.06f, r = 10f + (i % 5) * 4f, a = t * speed + i * 0.9f;
                var pos = batCenter + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(t * 0.8f + i) * 3f + (i % 3) * 2f, Mathf.Sin(a) * r);
                b.position = pos;
                b.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
                float flap = Mathf.Sin(t * 14f + i) * 40f;
                if (b.childCount >= 3)
                {
                    b.GetChild(1).localRotation = Quaternion.Euler(0f, 0f, flap);
                    b.GetChild(2).localRotation = Quaternion.Euler(0f, 0f, -flap);
                }
            }
        }
    }
}
