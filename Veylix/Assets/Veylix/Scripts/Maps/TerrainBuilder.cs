using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Builds the map's Unity Terrain: natural landform from noise (shaped by the map's style: dunes, hills, alpine
    /// ridges, an island), a coastline for sea maps, then every road is carved in with cut-and-fill slopes so the land
    /// meets the tarmac naturally. Texture layers are painted by slope, height and distance from the road.
    /// Keeps a "distance to nearest road" raster for placing trees and buildings.
    /// </summary>
    public class TerrainBuilder
    {
        public Terrain terrain;
        public float size, minY, maxY;
        public int res;
        public float[] roadDist;   // metres to the nearest road corridor edge (res x res), negative inside
        public float[] heightsWorld;
        public Vector3 origin;
        readonly MapDef map;
        readonly int seed;
        float coast = float.MaxValue;

        readonly int quality;
        float[,] heightGrid;
        float[,,] splat;

        /// <summary>Create on the main thread (reads settings); Compute() may then run on a worker thread.</summary>
        public TerrainBuilder(MapDef map) { this.map = map; seed = map.seed; quality = Settings.Quality; size = map.worldSize; }

        // ------------------------------------------------------------------ natural landform

        public float Natural(float x, float z)
        {
            float s = seed * 0.001f;
            float n;
            float r = map.relief;
            float edge = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - size * 0.28f) / (size * 0.22f)); // rises towards the map edge
            switch (map.terrain)
            {
                case TerrainStyle.Flat:
                    n = Noise.Fbm(x * 0.002f + s, z * 0.002f, 3) * 1.5f + edge * edge * r;
                    break;
                case TerrainStyle.Rolling:
                    n = (Noise.Fbm(x * 0.0012f + s, z * 0.0012f, 4) * 0.5f + 0.5f) * r * 0.6f + edge * edge * r * 0.8f;
                    break;
                case TerrainStyle.Hills:
                    n = (Noise.Fbm(x * 0.0011f + s, z * 0.0011f, 5) * 0.5f + 0.4f) * r * 0.7f + Noise.Ridged(x * 0.0007f, z * 0.0007f + s, 4) * r * 0.5f * edge + edge * edge * r * 0.6f;
                    break;
                case TerrainStyle.Mountains:
                {
                    float ridge = Noise.Ridged(x * 0.0006f + s, z * 0.0006f, 6);
                    n = ridge * r * (0.25f + edge * 0.9f) + (Noise.Fbm(x * 0.002f, z * 0.002f + s, 4) * 0.5f + 0.5f) * r * 0.12f;
                    break;
                }
                case TerrainStyle.Dunes:
                {
                    // Long dune crests across the wind, warped, plus distant mesas.
                    float wx = x + Noise.Fbm(x * 0.001f, z * 0.001f + s, 3) * 300f;
                    float dune = Mathf.Pow(Mathf.Abs(Mathf.Sin(wx * 0.006f + z * 0.002f)), 1.5f);
                    n = dune * r * 0.12f + (Noise.Fbm(x * 0.0008f + s, z * 0.0008f, 3) * 0.5f + 0.5f) * r * 0.15f;
                    float mesa = Mathf.Clamp01((Noise.Fbm(x * 0.0005f, z * 0.0005f + s * 3f, 3) - 0.15f) * 6f) * edge;
                    n += Mathf.Round(mesa * 3f) / 3f * r;
                    break;
                }
                case TerrainStyle.Valley:
                    n = (Noise.Fbm(x * 0.0013f + s, z * 0.0013f, 5) * 0.5f + 0.5f) * r + edge * r * 0.5f;
                    break;
                case TerrainStyle.Island:
                {
                    float d = Mathf.Sqrt(x * x + z * z) / (size * 0.42f);
                    float shape = d + Noise.Fbm(x * 0.0009f + s, z * 0.0009f, 4) * 0.25f;
                    float land = Mathf.Clamp01(1f - shape);
                    float peak = Mathf.Pow(land, 2.2f) * r + Noise.Ridged(x * 0.0012f, z * 0.0012f + s, 4) * r * 0.25f * land;
                    n = Mathf.Lerp(-25f, 3f, Mathf.Clamp01(land * 8f)) + peak;
                    break;
                }
                default: n = 0f; break;
            }
            // Sea maps (other than islands): land drops into the water past the coastline.
            if (map.Has(Feature.Sea) && map.terrain != TerrainStyle.Island)
            {
                Vector2 dir = map.seaDir.normalized;
                float along = x * dir.x + z * dir.y;
                float wobble = Noise.Fbm(x * 0.002f, z * 0.002f + s, 3) * 60f;
                float t = Mathf.Clamp01((along - coast - wobble) / 70f);
                n = Mathf.Lerp(n, map.seaLevel - 18f, t);
            }
            return n;
        }

        // ------------------------------------------------------------------ build

        public struct RoadStamp
        {
            public TrackSpline spline;
            public RoadSides sides;
        }

        /// <summary>All the number crunching (heights, carving, texture weights). Thread-safe: no Unity objects.</summary>
        public void Compute(List<RoadStamp> roads)
        {
            size = map.worldSize;
            res = map.freeRoam && quality > 0 ? 2049 : 1025;
            origin = new Vector3(-size * 0.5f, 0f, -size * 0.5f);
            if (map.Has(Feature.Sea) && map.terrain != TerrainStyle.Island)
            {
                // Coastline just beyond the furthest bit of track on the sea side.
                Vector2 dir = map.seaDir.normalized;
                float maxAlong = float.MinValue;
                foreach (var r in roads)
                    for (int i = 0; i < r.spline.Count; i += 4)
                        maxAlong = Mathf.Max(maxAlong, r.spline.pos[i].x * dir.x + r.spline.pos[i].z * dir.y);
                coast = maxAlong + (map.Has(Feature.Street) ? 45f : 90f);
            }

            int N = res;
            float cell = size / (N - 1);
            var natural = new float[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                natural[y * N + x] = Natural(origin.x + x * cell, origin.z + y * cell);

            // Carve the roads: nearest corridor wins; slopes scale with how much cut or fill is needed.
            roadDist = new float[N * N];
            var targetH = new float[N * N];
            var blend = new float[N * N];
            for (int i = 0; i < roadDist.Length; i++) { roadDist[i] = 1e6f; blend[i] = 30f; }
            foreach (var r in roads)
            {
                var s = r.spline;
                for (int i = 0; i < s.Count; i++)
                {
                    Vector3 p = s.pos[i];
                    float corridor = Mathf.Max(r.sides.CorridorL(i), r.sides.CorridorR(i));
                    float nat = Natural(p.x, p.z);
                    float br = Mathf.Clamp(Mathf.Abs(nat - p.y) * 1.7f, 18f, 140f);
                    float reach = corridor + br;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - reach - origin.x) / cell)), x1 = Mathf.Min(N - 1, Mathf.CeilToInt((p.x + reach - origin.x) / cell));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt((p.z - reach - origin.z) / cell)), y1 = Mathf.Min(N - 1, Mathf.CeilToInt((p.z + reach - origin.z) / cell));
                    for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        float wx = origin.x + xx * cell, wz = origin.z + yy * cell;
                        float dx = wx - p.x, dz = wz - p.z;
                        // Distance across the road (along-road distance doesn't count: the samples are dense).
                        float lat = dx * s.right[i].x + dz * s.right[i].z;
                        float alongD = dx * s.fwd[i].x + dz * s.fwd[i].z;
                        if (Mathf.Abs(alongD) > TrackSpline.Step * 1.5f && (s.closed || (i > 0 && i < s.Count - 1))) continue;
                        float side = lat < 0f ? r.sides.CorridorL(i) : r.sides.CorridorR(i);
                        float d = Mathf.Abs(lat) - side;
                        int idx = yy * N + xx;
                        if (d < roadDist[idx])
                        {
                            roadDist[idx] = d;
                            // Banked road: follow its cross-slope out to the edge.
                            targetH[idx] = p.y + s.up[i].y * 0f + (s.right[i].y * lat) - 0.35f;
                            blend[idx] = br;
                        }
                    }
                }
            }

            minY = float.MaxValue; maxY = float.MinValue;
            heightsWorld = new float[N * N];
            for (int i = 0; i < N * N; i++)
            {
                float h = natural[i];
                float d = roadDist[i];
                if (d < 1e5f)
                {
                    float t = d <= 0f ? 0f : Smooth(d / blend[i]);
                    h = Mathf.Lerp(targetH[i], h, t);
                }
                heightsWorld[i] = h;
                minY = Mathf.Min(minY, h); maxY = Mathf.Max(maxY, h);
            }
            minY -= 2f; maxY += 2f;
            float range = Mathf.Max(10f, maxY - minY);
            heightGrid = new float[N, N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                heightGrid[y, x] = (heightsWorld[y * N + x] - minY) / range;
            ComputeSplat();
        }

        /// <summary>Creates the Terrain from the computed data (main thread).</summary>
        public void Apply(Transform parent)
        {
            int N = res;
            float range = Mathf.Max(10f, maxY - minY);
            var data = new TerrainData { heightmapResolution = N };
            data.size = new Vector3(size, range, size);
            data.SetHeights(0, 0, heightGrid);
            ApplyLayers(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(origin.x, minY, origin.z);
            go.layer = Layers.Terrain;
            terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = quality == 0 ? 8f : 4f;
            terrain.basemapDistance = quality == 0 ? 180f : 400f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            terrain.treeDistance = 0f;
            terrain.detailObjectDistance = 0f;
            var tc = go.GetComponent<TerrainCollider>();
            if (tc != null)
            {
                var pm = new PhysicsMaterial("ground") { dynamicFriction = 0.7f, staticFriction = 0.8f };
                tc.sharedMaterial = pm;
            }
            Surface.TerrainType = map.ground;
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>World height of the terrain (bilinear from our own copy, no physics needed).</summary>
        public float Height(float x, float z)
        {
            int N = res;
            float cell = size / (N - 1);
            float fx = Mathf.Clamp((x - origin.x) / cell, 0f, N - 1.001f), fz = Mathf.Clamp((z - origin.z) / cell, 0f, N - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            float a = heightsWorld[iz * N + ix], b = heightsWorld[iz * N + ix + 1], c = heightsWorld[(iz + 1) * N + ix], d = heightsWorld[(iz + 1) * N + ix + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        public float RoadDistance(float x, float z)
        {
            int N = res;
            float cell = size / (N - 1);
            int ix = Mathf.Clamp(Mathf.RoundToInt((x - origin.x) / cell), 0, N - 1), iz = Mathf.Clamp(Mathf.RoundToInt((z - origin.z) / cell), 0, N - 1);
            return roadDist[iz * N + ix];
        }

        public float Slope(float x, float z)
        {
            float e = size / (res - 1);
            float dx = Height(x + e, z) - Height(x - e, z), dz = Height(x, z + e) - Height(x, z - e);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / (2f * e)) * Mathf.Rad2Deg;
        }

        public bool InSea(float x, float z) => map.Has(Feature.Sea) && Height(x, z) < map.seaLevel + 0.2f;

        // ------------------------------------------------------------------ texture layers

        int[] LayerSet()
        {
            switch (map.biome)
            {
                case Biome.Desert: case Biome.Gulf: return new[] { TL.Sand, TL.Dirt, TL.Rock, TL.Gravel };
                case Biome.Outback: return new[] { TL.Dirt, TL.DryGrass, TL.Rock, TL.Sand };
                case Biome.Snow: return new[] { TL.Snow, TL.Rock, TL.Gravel, TL.Dirt };
                case Biome.Tropical: return new[] { TL.Tropical, TL.Mud, TL.Rock, TL.Sand };
                case Biome.Riviera: case Biome.Coast: return new[] { TL.DryGrass, TL.Dirt, TL.Rock, TL.Sand };
                case Biome.Ardennes: return new[] { TL.Grass, TL.Forest, TL.Rock, TL.Mud };
                case Biome.Alpine: case Biome.Highland: return new[] { TL.Grass, TL.Rock, TL.Snow, TL.Dirt };
                case Biome.CityNight: case Biome.Neon: case Biome.Port: return new[] { TL.Grass, TL.Gravel, TL.Dirt, TL.Rock };
                case Biome.Farmland: return new[] { TL.Grass, TL.DryGrass, TL.Dirt, TL.Rock };
                default: return new[] { TL.Grass, TL.DryGrass, TL.Rock, TL.Dirt };
            }
        }

        void ApplyLayers(TerrainData data)
        {
            var set = LayerSet();
            var layers = new TerrainLayer[4];
            bool any = false;
            for (int i = 0; i < 4; i++) { layers[i] = VXAssets.Layer(set[i]); any |= layers[i] != null; }
            if (!any) return;
            for (int i = 0; i < 4; i++) if (layers[i] == null) layers[i] = layers[0];
            data.terrainLayers = layers;
            data.alphamapResolution = splat.GetLength(0);
            data.SetAlphamaps(0, 0, splat);
        }

        void ComputeSplat()
        {
            int A = quality == 0 ? 512 : 1024;
            var maps = new float[A, A, 4];
            float s = seed * 0.01f;
            for (int y = 0; y < A; y++)
            for (int x = 0; x < A; x++)
            {
                float wx = origin.x + x / (float)(A - 1) * size, wz = origin.z + y / (float)(A - 1) * size;
                float h = Height(wx, wz);
                float slope = Slope(wx, wz);
                float rd = RoadDistance(wx, wz);
                float n1 = Noise.Fbm(wx * 0.01f + s, wz * 0.01f, 3) * 0.5f + 0.5f;
                float n2 = Noise.Fbm(wx * 0.003f, wz * 0.003f + s, 3) * 0.5f + 0.5f;
                float w0 = 1f, w1 = 0f, w2 = 0f, w3 = 0f;
                // Patches of the secondary ground.
                w1 = Mathf.Clamp01((n2 - 0.45f) * 3f + (n1 - 0.5f) * 0.6f);
                // Rock on steep slopes.
                w2 = Mathf.Clamp01((slope - 28f) / 12f);
                // Fourth layer: worn ground next to roads, beaches near the sea, snow on high peaks.
                if (map.biome == Biome.Alpine || map.biome == Biome.Highland) w3 = 0f;
                if (rd < 6f && rd > -50f) w3 = Mathf.Clamp01(1f - rd / 6f) * 0.7f * n1;
                if (map.Has(Feature.Sea) && h < map.seaLevel + 3f) { w3 = 1f; w0 = 0f; w1 = 0f; }
                if ((map.biome == Biome.Alpine || map.biome == Biome.Highland) && h > maxY - (maxY - minY) * 0.25f) { w2 = Mathf.Max(w2, 0.2f); w3 = 0f; w1 = Mathf.Clamp01((h - (maxY - (maxY - minY) * 0.25f)) / 40f); }
                float total = w0 + w1 + w2 + w3;
                // Normalise with rock and the edge layer taking priority.
                float rock = w2, edgeL = w3 * (1f - rock);
                float rest = Mathf.Max(0f, 1f - rock - edgeL);
                maps[y, x, 0] = rest * (1f - w1);
                maps[y, x, 1] = rest * w1;
                maps[y, x, 2] = rock;
                maps[y, x, 3] = edgeL;
            }
            splat = maps;
        }
    }
}
