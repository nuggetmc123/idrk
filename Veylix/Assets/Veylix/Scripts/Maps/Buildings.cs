using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    public enum BuildingStyle { Stucco, Office, Brick, Industrial, Village, Tower, Warehouse }

    /// <summary>
    /// Generates buildings as combined meshes per city block-sized cell: walls with facades whose windows line up with
    /// the building's corners and floors, roofs (flat with plant, or pitched tiles), balconies on stucco blocks and
    /// lit windows at night (the facade materials' emission follows the time of day). Each building gets a box collider.
    /// </summary>
    public class Buildings
    {
        class Bucket { public MeshAcc acc = new MeshAcc(); public Material mat; }

        readonly Dictionary<string, Bucket> buckets = new Dictionary<string, Bucket>();
        readonly Transform root;
        readonly Transform colliders;
        readonly Rng rng;
        public int Count;
        public static readonly List<Material> NightMaterials = new List<Material>();

        public Buildings(Transform parent, int seed)
        {
            root = new GameObject("Buildings").transform;
            root.SetParent(parent, false);
            colliders = new GameObject("BuildingColliders").transform;
            colliders.SetParent(root, false);
            rng = new Rng(seed);
        }

        static readonly Dictionary<string, Material> tinted = new Dictionary<string, Material>();

        Material StyleMat(BuildingStyle s, int tint)
        {
            var A = VXAssets.I;
            Material baseMat;
            Color c = Color.white;
            switch (s)
            {
                case BuildingStyle.Stucco:
                    baseMat = A?.buildingStucco;
                    c = new[] { new Color(1f, 0.93f, 0.8f), new Color(1f, 0.82f, 0.72f), new Color(0.96f, 0.86f, 0.62f), new Color(0.98f, 0.96f, 0.9f), new Color(0.86f, 0.78f, 0.7f) }[tint % 5];
                    break;
                case BuildingStyle.Office: case BuildingStyle.Tower: baseMat = A?.buildingOffice; c = new[] { Color.white, new Color(0.85f, 0.92f, 1f), new Color(0.9f, 0.9f, 0.85f) }[tint % 3]; break;
                case BuildingStyle.Brick: baseMat = A?.buildingBrick; c = new[] { Color.white, new Color(0.9f, 0.8f, 0.75f), new Color(0.8f, 0.75f, 0.72f) }[tint % 3]; break;
                case BuildingStyle.Industrial: case BuildingStyle.Warehouse: baseMat = A?.buildingIndustrial; c = new[] { Color.white, new Color(0.75f, 0.82f, 0.85f), new Color(0.85f, 0.8f, 0.7f) }[tint % 3]; break;
                default: baseMat = A?.buildingWarm; c = new[] { new Color(1f, 0.97f, 0.9f), new Color(0.95f, 0.9f, 0.8f), new Color(0.9f, 0.88f, 0.84f) }[tint % 3]; break;
            }
            string key = s + "_" + tint;
            if (tinted.TryGetValue(key, out var m) && m != null) return m;
            m = baseMat != null ? new Material(baseMat) : VXAssets.Fallback(key, c * 0.8f, 0.2f);
            m.color = c;
            m.name = key;
            tinted[key] = m;
            if (m.IsKeywordEnabled("_EMISSION")) NightMaterials.Add(m);
            return m;
        }

        MeshAcc Acc(string key, Material mat)
        {
            if (!buckets.TryGetValue(key, out var b)) { b = new Bucket { mat = mat }; buckets[key] = b; }
            return b.acc;
        }

        /// <summary>A building standing at 'pos' (ground centre), its front facing 'facing'.</summary>
        public void Add(Vector3 pos, Vector3 facing, float width, float depth, float height, BuildingStyle style, bool collider = true)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
            facing.Normalize();
            var rot = Quaternion.LookRotation(facing, Vector3.up);
            int tint = rng.Range(0, 5);
            var wallMat = StyleMat(style, tint);
            string cellKey = Mathf.FloorToInt(pos.x / 300f) + "_" + Mathf.FloorToInt(pos.z / 300f);
            var walls = Acc(cellKey + "w" + wallMat.name, wallMat);
            var A = VXAssets.I;
            var roofMat = VXAssets.Or(A?.roof, "Roof", new Color(0.4f, 0.38f, 0.36f), 0.2f);
            var roofs = Acc(cellKey + "r", roofMat);

            float floor = style == BuildingStyle.Industrial || style == BuildingStyle.Warehouse ? 6f : 3.4f;
            float bay = style == BuildingStyle.Office || style == BuildingStyle.Tower ? 3f : 3.6f;
            height = Mathf.Max(floor, Mathf.Round(height / floor) * floor);
            // Foundation goes a couple of metres into the ground so slopes never show a gap.
            Vector3 b = pos - Vector3.up * 2f;
            float h = height + 2f;
            Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
            Vector3[] c =
            {
                b + right * width * 0.5f + fwd * depth * 0.5f, b - right * width * 0.5f + fwd * depth * 0.5f,
                b - right * width * 0.5f - fwd * depth * 0.5f, b + right * width * 0.5f - fwd * depth * 0.5f,
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 p0 = c[i], p1 = c[(i + 1) % 4];
                float len = Vector3.Distance(p0, p1);
                float bays = Mathf.Max(1f, Mathf.Round(len / bay));
                Vector3 n = Vector3.Cross(p1 - p0, Vector3.up).normalized;
                if (Vector3.Dot(n, (p0 + p1) * 0.5f - b) < 0f) n = -n;
                float v0 = -2f / floor, v1 = height / floor;
                int a0 = walls.Add(p0, n, new Vector2(0, v0));
                int a1 = walls.Add(p1, n, new Vector2(bays, v0));
                int a2 = walls.Add(p1 + Vector3.up * h, n, new Vector2(bays, v1));
                int a3 = walls.Add(p0 + Vector3.up * h, n, new Vector2(0, v1));
                walls.Quad(a0, a1, a2, a3, n);
            }
            Vector3 top = b + Vector3.up * h;
            bool pitched = style == BuildingStyle.Village || (style == BuildingStyle.Stucco && height < 14f && rng.Chance(0.6f));
            if (pitched)
            {
                // Gable roof with overhang.
                float rise = Mathf.Min(width, depth) * 0.32f;
                Vector3 o = right * 0.4f, of = fwd * 0.4f;
                Vector3 r0 = top + right * (width * 0.5f) + o, l0 = top - right * (width * 0.5f) - o;
                Vector3 ridgeF = top + Vector3.up * rise + fwd * (depth * 0.5f) + of, ridgeB = top + Vector3.up * rise - fwd * (depth * 0.5f) - of;
                Vector3 rf = r0 + fwd * (depth * 0.5f) + of, rb = r0 - fwd * (depth * 0.5f) - of;
                Vector3 lf = l0 + fwd * (depth * 0.5f) + of, lb = l0 - fwd * (depth * 0.5f) - of;
                var tiles = Acc(cellKey + "t", VXAssets.Or(A?.roofTiles, "Tiles", new Color(0.6f, 0.3f, 0.2f), 0.2f));
                Vector3 nR = Vector3.Cross(ridgeF - rf, rb - rf).normalized; if (nR.y < 0) nR = -nR;
                Vector3 nL = Vector3.Cross(ridgeF - lf, lb - lf).normalized; if (nL.y < 0) nL = -nL;
                float sl = Vector3.Distance(rf, ridgeF);
                int q0 = tiles.Add(rf, nR, new Vector2(0, 0)), q1 = tiles.Add(rb, nR, new Vector2(depth / 2f, 0)), q2 = tiles.Add(ridgeB, nR, new Vector2(depth / 2f, sl / 2f)), q3 = tiles.Add(ridgeF, nR, new Vector2(0, sl / 2f));
                tiles.Quad(q0, q1, q2, q3, nR);
                q0 = tiles.Add(lf, nL, new Vector2(0, 0)); q1 = tiles.Add(lb, nL, new Vector2(depth / 2f, 0)); q2 = tiles.Add(ridgeB, nL, new Vector2(depth / 2f, sl / 2f)); q3 = tiles.Add(ridgeF, nL, new Vector2(0, sl / 2f));
                tiles.Quad(q0, q1, q2, q3, nL);
                // Gable ends (wall material).
                foreach (var end in new[] { 1f, -1f })
                {
                    Vector3 e1 = top + right * width * 0.5f + fwd * depth * 0.5f * end, e2 = top - right * width * 0.5f + fwd * depth * 0.5f * end, e3 = top + Vector3.up * rise + fwd * depth * 0.5f * end;
                    Vector3 n = fwd * end;
                    int g0 = walls.Add(e1, n, new Vector2(0, v1Of(height, floor))), g1 = walls.Add(e2, n, new Vector2(1, v1Of(height, floor))), g2 = walls.Add(e3, n, new Vector2(0.5f, v1Of(height, floor)));
                    walls.Tri(g0, g1, g2, n);
                }
            }
            else
            {
                // Flat roof with a parapet and some plant on top.
                int r0 = roofs.Add(top + right * width * 0.5f + fwd * depth * 0.5f, Vector3.up, new Vector2(width / 4f, depth / 4f));
                int r1 = roofs.Add(top - right * width * 0.5f + fwd * depth * 0.5f, Vector3.up, new Vector2(0, depth / 4f));
                int r2 = roofs.Add(top - right * width * 0.5f - fwd * depth * 0.5f, Vector3.up, new Vector2(0, 0));
                int r3 = roofs.Add(top + right * width * 0.5f - fwd * depth * 0.5f, Vector3.up, new Vector2(width / 4f, 0));
                roofs.Quad(r0, r1, r2, r3, Vector3.up);
                roofs.Box(top + Vector3.up * 0.4f + fwd * (depth * 0.5f - 0.15f), new Vector3(width, 0.8f, 0.3f), rot, 0.5f);
                roofs.Box(top + Vector3.up * 0.4f - fwd * (depth * 0.5f - 0.15f), new Vector3(width, 0.8f, 0.3f), rot, 0.5f);
                roofs.Box(top + Vector3.up * 0.4f + right * (width * 0.5f - 0.15f), new Vector3(0.3f, 0.8f, depth), rot, 0.5f);
                roofs.Box(top + Vector3.up * 0.4f - right * (width * 0.5f - 0.15f), new Vector3(0.3f, 0.8f, depth), rot, 0.5f);
                int units = Mathf.Clamp(Mathf.RoundToInt(width * depth / 120f), 1, 6);
                for (int u = 0; u < units; u++)
                {
                    Vector3 up = top + right * rng.Range(-width * 0.35f, width * 0.35f) + fwd * rng.Range(-depth * 0.35f, depth * 0.35f);
                    Vector3 size = new Vector3(rng.Range(1.5f, 3.5f), rng.Range(1f, 2.4f), rng.Range(1.5f, 3f));
                    roofs.Box(up + Vector3.up * size.y * 0.5f, size, rot, 0.5f);
                }
            }
            Details(pos, rot, right, fwd, width, depth, height, floor, bay, style, pitched, cellKey, top);

            // Balconies on stucco apartments (thin slabs per floor on the front).
            if (style == BuildingStyle.Stucco && height > 8f)
            {
                var slab = Acc(cellKey + "b", VXAssets.Or(A?.concrete, "Concrete", new Color(0.8f, 0.78f, 0.74f), 0.2f));
                for (float y = floor; y < height - 1f; y += floor)
                {
                    slab.Box(pos + Vector3.up * y + fwd * (depth * 0.5f + 0.6f), new Vector3(width * 0.8f, 0.15f, 1.2f), rot, 0.5f);
                    // Railing: top rail and a thin front panel.
                    slab.Box(pos + Vector3.up * (y + 1.0f) + fwd * (depth * 0.5f + 1.17f), new Vector3(width * 0.8f, 0.06f, 0.06f), rot, 0.5f);
                    slab.Box(pos + Vector3.up * (y + 0.55f) + fwd * (depth * 0.5f + 1.18f), new Vector3(width * 0.8f, 0.7f, 0.03f), rot, 0.5f);
                }
            }
            if (collider)
            {
                var go = new GameObject("b");
                go.layer = Layers.Barrier;
                go.transform.SetParent(colliders, false);
                go.transform.SetPositionAndRotation(pos + Vector3.up * (height * 0.5f - 1f), rot);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(width, height + 2f, depth);
            }
            Count++;
        }

        static float v1Of(float h, float floor) => h / floor;

        static readonly Color[] AwningColours = { new Color(0.55f, 0.1f, 0.08f), new Color(0.1f, 0.25f, 0.18f), new Color(0.12f, 0.18f, 0.35f), new Color(0.75f, 0.6f, 0.35f), new Color(0.2f, 0.2f, 0.22f) };

        Material Tinted(string key, Material baseMat, Color c, float smooth)
        {
            if (tinted.TryGetValue(key, out var m) && m != null) return m;
            m = baseMat != null ? new Material(baseMat) : VXAssets.Fallback(key, c, smooth);
            m.color = c;
            m.name = key;
            tinted[key] = m;
            return m;
        }

        /// <summary>
        /// What makes a block read as a real building rather than a box: a ground floor with shopfronts and awnings,
        /// a string course and corner pilasters, a cornice at the roofline, setbacks on towers, roller doors on
        /// industrial units and clutter on the roof (water tanks, aerials, stair houses).
        /// </summary>
        void Details(Vector3 pos, Quaternion rot, Vector3 right, Vector3 fwd, float width, float depth, float height, float floor, float bay, BuildingStyle style, bool pitched, string cellKey, Vector3 top)
        {
            var A = VXAssets.I;
            var stone = Acc(cellKey + "s", VXAssets.Or(A?.concrete, "Stone", new Color(0.78f, 0.76f, 0.72f), 0.2f));
            var metal = Acc(cellKey + "m", VXAssets.Or(A?.metal, "Metal", new Color(0.55f, 0.56f, 0.58f), 0.6f));
            bool town = style == BuildingStyle.Stucco || style == BuildingStyle.Brick || style == BuildingStyle.Office || style == BuildingStyle.Village;
            bool industrial = style == BuildingStyle.Industrial || style == BuildingStyle.Warehouse;

            if (town && height >= floor * 2f)
            {
                // Ground floor plinth, slightly proud of the wall, with shop windows and the odd awning on the front.
                float gf = Mathf.Min(floor, 4f);
                stone.Box(pos + Vector3.up * (gf * 0.5f - 0.5f), new Vector3(width + 0.3f, gf + 1f, depth + 0.3f), rot, 0.5f);
                var glass = Acc(cellKey + "g", VXAssets.Or(A?.glass, "ShopGlass", new Color(0.12f, 0.14f, 0.16f), 0.9f));
                int bays = Mathf.Max(1, Mathf.RoundToInt(width / bay));
                float bw = width / bays;
                int awningTint = rng.Range(0, AwningColours.Length);
                var awnings = Acc(cellKey + "a" + awningTint, Tinted("Awning" + awningTint, A?.tent, AwningColours[awningTint], 0.15f));
                for (int i = 0; i < bays; i++)
                {
                    Vector3 c = pos + right * (-width * 0.5f + bw * (i + 0.5f)) + fwd * (depth * 0.5f + 0.17f);
                    glass.Box(c + Vector3.up * (gf * 0.45f), new Vector3(bw * 0.78f, gf * 0.62f, 0.04f), rot, 0.5f);
                    if (rng.Chance(0.35f))
                    {
                        // Sloped canvas awning on two thin arms.
                        var tilt = rot * Quaternion.Euler(-22f, 0f, 0f);
                        awnings.Box(c + Vector3.up * (gf * 0.85f) + fwd * 0.65f, new Vector3(bw * 0.86f, 0.05f, 1.4f), tilt, 0.5f);
                        awnings.Box(c + Vector3.up * (gf * 0.85f - 0.25f) + fwd * 1.32f, new Vector3(bw * 0.86f, 0.32f, 0.03f), rot, 0.5f);
                    }
                }
                // String course between the shops and the floors above.
                stone.Box(pos + Vector3.up * (gf + 0.1f), new Vector3(width + 0.4f, 0.2f, depth + 0.4f), rot, 0.5f);
            }

            if ((style == BuildingStyle.Stucco || style == BuildingStyle.Brick) && height > floor)
            {
                // Corner pilasters.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        stone.Box(pos + right * (sx * (width * 0.5f - 0.15f)) + fwd * (sz * (depth * 0.5f - 0.15f)) + Vector3.up * (height * 0.5f), new Vector3(0.5f, height, 0.5f), rot, 0.5f);
            }

            if (!pitched && !industrial)
            {
                // Cornice: a projecting band at the roofline.
                stone.Box(top + Vector3.down * 0.25f, new Vector3(width + 0.7f, 0.45f, depth + 0.7f), rot, 0.5f);
            }

            if ((style == BuildingStyle.Office || style == BuildingStyle.Tower) && height > 30f && !pitched)
            {
                // Setback: the top floors step in, with their own roof.
                float upH = Mathf.Round(height * 0.25f / floor) * floor;
                var wallMat = StyleMat(style, rng.Range(0, 5));
                var walls = Acc(cellKey + "w" + wallMat.name, wallMat);
                walls.Box(top + Vector3.up * (upH * 0.5f), new Vector3(width * 0.72f, upH, depth * 0.72f), rot, 1f / 3.4f);
                stone.Box(top + Vector3.up * (upH + 0.2f), new Vector3(width * 0.74f, 0.4f, depth * 0.74f), rot, 0.5f);
                metal.Box(top + Vector3.up * (upH + 4f), new Vector3(0.12f, 8f, 0.12f), rot, 0.5f); // mast
            }

            if (industrial)
            {
                // Roller doors along the front and a row of roof vents.
                int doors = Mathf.Max(1, Mathf.RoundToInt(width / 9f));
                for (int i = 0; i < doors; i++)
                {
                    Vector3 c = pos + right * (-width * 0.5f + width / doors * (i + 0.5f)) + fwd * (depth * 0.5f + 0.08f);
                    metal.Box(c + Vector3.up * 2.4f, new Vector3(Mathf.Min(5f, width / doors * 0.7f), 4.8f, 0.1f), rot, 0.5f);
                }
                for (float x = -width * 0.35f; x <= width * 0.35f; x += 6f)
                    metal.Box(top + right * x + Vector3.up * 0.6f, new Vector3(1.2f, 1.2f, 1.2f), rot, 0.5f);
            }
            else if (!pitched && height > 10f)
            {
                // Roof clutter: a water tank on legs, an aerial and a stair house.
                Vector3 t = top + right * rng.Range(-width * 0.3f, width * 0.3f) + fwd * rng.Range(-depth * 0.3f, depth * 0.3f);
                if (style == BuildingStyle.Brick || style == BuildingStyle.Stucco)
                {
                    metal.Box(t + Vector3.up * 1.2f, new Vector3(0.15f, 2.4f, 0.15f), rot, 0.5f);
                    stone.Box(t + Vector3.up * 3.2f, new Vector3(2.4f, 2.2f, 2.4f), rot * Quaternion.Euler(0f, 45f, 0f), 0.5f);
                }
                metal.Box(top + right * (width * 0.3f) + Vector3.up * 2.5f, new Vector3(0.06f, 5f, 0.06f), rot, 0.5f);
                stone.Box(top - right * (width * 0.25f) + Vector3.up * 1.3f, new Vector3(3f, 2.6f, 3.4f), rot, 0.5f);
            }
        }

        public void Finish()
        {
            foreach (var kv in buckets)
            {
                var b = kv.Value;
                var go = b.acc.Emit(root, "Blk", b.mat, Layers.Scenery, false, null, true);
                if (go != null) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            buckets.Clear();
        }

        public static void ResetCache() { tinted.Clear(); NightMaterials.Clear(); }
    }
}
