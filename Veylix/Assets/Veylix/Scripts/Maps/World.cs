using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The currently loaded map and everything generated for it. Build() makes the whole place from the map's
    /// definition (track, terrain, roads, trackside, buildings, vegetation, landmarks), spread over frames so the
    /// loading screen stays smooth in VR. The heavy terrain maths runs on a worker thread.
    /// </summary>
    public class World : MonoBehaviour
    {
        public static World Current { get; private set; }

        public MapDef map;
        public TrackSpline track;
        public RoadSides sides;
        public RacingLine line;
        public readonly List<TrackSpline> roads = new List<TrackSpline>();
        public readonly List<RoadSides> roadSides = new List<RoadSides>();
        public TerrainBuilder terrain;
        public Vegetation vegetation;
        public StartLights startLights;
        public float startS;
        public List<Pose> grid = new List<Pose>();
        public List<Vector3> lamps = new List<Vector3>();
        public float[] referenceSpeed; // racing-line speed for a top car (brake boards, practice line)
        public int pitSide = -1;
        public Bounds trackBounds;
        Transform root;
        Rng rng;

        public bool Ready { get; private set; }
        public float Progress { get; private set; }
        public string Stage { get; private set; } = "";

        void Awake() { Current = this; }

        /// The editor's track preview builds a map outside Play mode, where Awake doesn't run.
        public void UseAsCurrent() => Current = this;
        void OnDestroy() { if (Current == this) Current = null; }

        public void Clear()
        {
            Ready = false;
            if (root != null) Obj.Kill(root.gameObject);
            roads.Clear(); roadSides.Clear(); grid.Clear(); lamps.Clear();
            Surface.ClearCache();
            Buildings.ResetCache();
        }

        void Step(float p, string stage) { Progress = p; Stage = stage; }

        public IEnumerator Build(MapDef m)
        {
            Clear();
            map = m;
            rng = new Rng(m.seed);
            root = new GameObject("World " + m.name).transform;
            Layers.Setup();
            Env.AmbientC = m.ambientC;

            // ---------------- splines
            Step(0.03f, "Laying out the track");
            var cps = new List<Vector3>();
            for (int i = 0; i + 2 < m.points.Length; i += 3)
                cps.Add(new Vector3(m.points[i] * m.scale, m.points[i + 2] * m.elevScale, m.points[i + 1] * m.scale));
            Vector3 min = cps[0], max = cps[0];
            foreach (var p in cps) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            Vector3 centre = (min + max) * 0.5f;
            centre.y = 0f;
            float baseY = (m.Has(Feature.Sea) ? m.seaLevel + 2.5f : 0f) - min.y;
            for (int i = 0; i < cps.Count; i++) cps[i] = cps[i] - centre + Vector3.up * baseY;
            track = TrackSpline.FromControlPoints(cps, true, m.width * 0.5f);
            var kind = KindFor(m);
            sides = RoadSides.Design(track, kind, m);
            roads.Add(track); roadSides.Add(sides);
            trackBounds = new Bounds(track.pos[0], Vector3.zero);
            foreach (var p in track.pos) trackBounds.Encapsulate(p);
            yield return null;

            terrain = new TerrainBuilder(m);
            if (m.freeRoam) { ExtraRoads(m); yield return null; }

            Step(0.1f, "Finding the racing line");
            line = new RacingLine(track);
            referenceSpeed = line.SpeedProfile(CarCatalog.Get(0), 1f, 0f);
            startS = Mathf.Min(260f, track.Length * 0.07f);
            if (m.freeRoam) startS = 40f;
            yield return null;

            // ---------------- terrain (worker thread)
            Step(0.15f, "Shaping the land");
            var stamps = new List<TerrainBuilder.RoadStamp>();
            for (int i = 0; i < roads.Count; i++) stamps.Add(new TerrainBuilder.RoadStamp { spline = roads[i], sides = roadSides[i] });
            var task = Task.Run(() => terrain.Compute(stamps));
            float t0 = Time.realtimeSinceStartup;
            while (!task.IsCompleted)
            {
                Progress = Mathf.Min(0.45f, 0.15f + (Time.realtimeSinceStartup - t0) * 0.02f);
                yield return null;
            }
            if (task.Exception != null) Debug.LogException(task.Exception);
            Step(0.47f, "Painting the ground");
            terrain.Apply(root);
            yield return null;

            // ---------------- roads
            Step(0.52f, "Building the track");
            for (int i = 0; i < roads.Count; i++)
            {
                if (m.freeRoam) AddRoadsideArmco(roads[i], roadSides[i]);
                RoadBuilder.Build(root, roads[i], roadSides[i], m, i == 0 ? line : null, i == 0);
                if (i % 2 == 1) yield return null;
            }
            if (m.Has(Feature.Sea) || m.terrain == TerrainStyle.Island)
                Landmarks.Sea(root, m.seaLevel, m.worldSize * 4f);
            yield return null;

            // ---------------- trackside
            Step(0.6f, "Trackside");
            var furn = new GameObject("Trackside").transform;
            furn.SetParent(root, false);
            if (!m.freeRoam)
            {
                int si = track.IndexAt(startS);
                pitSide = PickPitSide(si);
                startLights = Furniture.StartGantry(furn, this, startS);
                Furniture.PitComplex(furn, this, startS, pitSide);
                Furniture.AdBoards(furn, this, rng);
                Furniture.MarshalPosts(furn, this);
                Furniture.BrakeBoards(furn, this, referenceSpeed);
                PlaceGrandstands(furn);
                Furniture.Bridge(furn, this, BestStraight(startS + track.Length * 0.35f));
            }
            else
            {
                startLights = Furniture.StartGantry(furn, this, startS + 30f);
            }
            grid = Furniture.GridSlots(this, startS, 20);
            if (!m.freeRoam) Furniture.GridMarks(furn, this, grid);
            if (m.Has(Feature.Floodlights) || m.Has(Feature.Street))
                lamps = Furniture.Floodlights(furn, track, sides, m.Has(Feature.Floodlights) ? 46f : 70f);
            yield return null;

            // ---------------- buildings and landmarks
            Step(0.68f, "Building the town");
            var buildings = new Buildings(root, m.seed);
            PlaceBuildings(buildings);
            yield return null;
            PlaceLandmarks(buildings);
            buildings.Finish();
            yield return null;

            // ---------------- vegetation
            Step(0.8f, "Planting trees");
            var vegGo = new GameObject("Vegetation");
            vegGo.transform.SetParent(root, false);
            vegetation = vegGo.AddComponent<Vegetation>();
            vegetation.Init();
            yield return ScatterVegetation();

            // ---------------- horizon
            Step(0.95f, "Finishing");
            float horizonH = Mathf.Max(60f, m.relief * 1.4f);
            if (m.biome == Biome.CityNight || m.biome == Biome.Neon) horizonH = 90f;
            Landmarks.Horizon(root, m, m.worldSize * 0.62f, terrain.minY, horizonH, m.seed);
            Step(1f, "Ready");
            Ready = true;
        }

        static RoadKind KindFor(MapDef m)
        {
            if (m.biome == Biome.CityNight && m.freeRoam) return RoadKind.City;
            if (m.Has(Feature.Street)) return RoadKind.Street;
            if (!m.freeRoam) return RoadKind.Circuit;
            return m.width >= 10f ? RoadKind.Highway : RoadKind.Lane;
        }

        // ------------------------------------------------------------------ free-roam road network

        void ExtraRoads(MapDef m)
        {
            if (m.biome == Biome.CityNight)
            {
                CityGrid(m);
                return;
            }
            int made = 0;
            for (int attempt = 0; attempt < 80 && made < m.extraRoads; attempt++)
            {
                int a = rng.Range(0, track.Count);
                int b = rng.Range(0, track.Count);
                float along = Mathf.Abs(track.dist[a] - track.dist[b]);
                along = Mathf.Min(along, track.Length - along);
                float straight = Vector3.Distance(track.pos[a], track.pos[b]);
                bool spur = made % 3 == 2; // every third road is a dead-end lane into the hills
                if (!spur && (along < track.Length * 0.2f || straight < 500f || straight > 2600f)) continue;
                Vector3 pa = track.pos[a], pb;
                if (spur)
                {
                    Vector3 inward = -track.right[a] * Mathf.Sign(Vector3.Dot(track.right[a], track.pos[a]));
                    pb = pa + inward * rng.Range(500f, 1100f) + track.fwd[a] * rng.Range(-300f, 300f);
                }
                else pb = track.pos[b];
                // Start a little way off the loop (perpendicular), so the junction is clean.
                Vector3 offA = SideAway(a, pb);
                var pts = new List<Vector3> { pa + offA * (m.width * 0.5f - 1f), pa + offA * 40f };
                int mids = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(pa, pb) / 260f));
                Vector3 dir = (pb - pa).normalized;
                Vector3 perp = Vector3.Cross(Vector3.up, dir);
                for (int k = 1; k <= mids; k++)
                {
                    Vector3 p = Vector3.Lerp(pa + offA * 40f, pb, k / (float)(mids + 1)) + perp * rng.Range(-160f, 160f);
                    pts.Add(p);
                }
                if (!spur)
                {
                    Vector3 offB = SideAway(b, pa);
                    pts.Add(pb + offB * 40f);
                    pts.Add(pb + offB * (m.width * 0.5f - 1f));
                }
                else pts.Add(pb);
                // Heights follow the land (smoothed), ends match the loop.
                for (int k = 0; k < pts.Count; k++)
                {
                    var p = pts[k];
                    float h = terrain.Natural(p.x, p.z);
                    if (k <= 1) h = pa.y;
                    else if (!spur && k >= pts.Count - 2) h = pb.y;
                    if (m.Has(Feature.Sea)) h = Mathf.Max(h, m.seaLevel + 3f);
                    p.y = h;
                    pts[k] = p;
                }
                var s = TrackSpline.FromControlPoints(pts, false, Mathf.Max(3.6f, m.width * 0.4f), 2f);
                if (!Clear(s, 30f)) continue;
                // Grade limit: no road steeper than ~14%.
                bool steep = false;
                for (int i = 2; i < s.Count; i++) if (Mathf.Abs(s.pos[i].y - s.pos[i - 2].y) / (2f * TrackSpline.Step) > 0.16f) { steep = true; break; }
                if (steep) continue;
                roads.Add(s);
                roadSides.Add(RoadSides.Design(s, RoadKind.Lane, m));
                made++;
            }
        }

        Vector3 SideAway(int i, Vector3 target)
        {
            Vector3 r = track.right[i];
            r.y = 0f;
            return Vector3.Dot(r, target - track.pos[i]) > 0f ? r.normalized : -r.normalized;
        }

        /// <summary>True if the new road keeps its distance from every existing road (except at its own ends).</summary>
        bool Clear(TrackSpline s, float minDist)
        {
            for (int i = 25; i < s.Count - 25; i += 3)
                foreach (var r in roads)
                {
                    int n = r.Nearest(s.pos[i], -1, minDist + 40f);
                    if (n >= 0 && Vector3.Distance(Flat(r.pos[n]), Flat(s.pos[i])) < minDist + r.halfWidth[n] + s.halfWidth[i]) return false;
                }
            return true;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void CityGrid(MapDef m)
        {
            // Streets every 200 m inside the loop's bounding box, plus a block beyond it.
            Bounds b = trackBounds;
            b.Expand(new Vector3(400f, 0f, 400f));
            float y = track.pos[0].y;
            float x0 = Mathf.Ceil(b.min.x / 200f) * 200f, z0 = Mathf.Ceil(b.min.z / 200f) * 200f;
            for (float x = x0; x <= b.max.x; x += 200f)
            {
                var pts = new List<Vector3> { new Vector3(x, y, b.min.z), new Vector3(x, y, b.max.z) };
                AddCityStreet(pts, m);
            }
            for (float z = z0; z <= b.max.z; z += 200f)
            {
                var pts = new List<Vector3> { new Vector3(b.min.x, y, z), new Vector3(b.max.x, y, z) };
                AddCityStreet(pts, m);
            }
        }

        void AddCityStreet(List<Vector3> pts, MapDef m)
        {
            // Skip streets that run along the main loop (it already is a street there).
            var s = TrackSpline.FromPolyline(TrackSpline.CatmullRom(pts, false), false, 6f, 0f);
            int overlap = 0;
            for (int i = 0; i < s.Count; i += 5)
            {
                int n = track.Nearest(s.pos[i], -1, 30f);
                if (n >= 0 && Vector3.Distance(Flat(track.pos[n]), Flat(s.pos[i])) < 12f && Mathf.Abs(Vector3.Dot(track.fwd[n], s.fwd[i])) > 0.9f) overlap++;
            }
            if (overlap > s.Count / 5 * 0.3f) return;
            roads.Add(s);
            roadSides.Add(RoadSides.Design(s, RoadKind.City, m));
        }

        void AddRoadsideArmco(TrackSpline s, RoadSides r)
        {
            if (r.kind == RoadKind.City) return;
            for (int i = 0; i < s.Count; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float off = side < 0 ? r.barrierL[i] : r.barrierR[i];
                    Vector3 p = s.pos[i] + s.right[i] * side * (off + 4f);
                    float drop = s.pos[i].y - terrain.Height(p.x, p.z);
                    bool needs = drop > 2.5f || (r.severity[i] > 0.6f && drop > 1f);
                    if (needs) { if (side < 0) r.barrierKindL[i] = BarrierKind.Armco; else r.barrierKindR[i] = BarrierKind.Armco; }
                }
            }
        }

        // ------------------------------------------------------------------ placement helpers

        int PickPitSide(int si)
        {
            // The side with more open ground beside the start straight.
            float l = 0f, r = 0f;
            for (int k = -60; k <= 60; k += 10)
            {
                int i = track.Wrap(si + k);
                l += terrain.RoadDistance(track.pos[i].x - track.right[i].x * 40f, track.pos[i].z - track.right[i].z * 40f);
                r += terrain.RoadDistance(track.pos[i].x + track.right[i].x * 40f, track.pos[i].z + track.right[i].z * 40f);
                if (terrain.InSea(track.pos[i].x - track.right[i].x * 40f, track.pos[i].z - track.right[i].z * 40f)) l -= 1000f;
                if (terrain.InSea(track.pos[i].x + track.right[i].x * 40f, track.pos[i].z + track.right[i].z * 40f)) r -= 1000f;
            }
            return l > r ? -1 : 1;
        }

        float BestStraight(float near)
        {
            float best = near, bestScore = float.MaxValue;
            for (float d = near - 400f; d < near + 400f; d += 20f)
            {
                int i = track.IndexAt(d);
                float c = Mathf.Abs(track.curvature[i]) + Mathf.Abs(track.curvature[track.Wrap(i + 15)]);
                if (c < bestScore) { bestScore = c; best = d; }
            }
            return best;
        }

        public bool SpotFree(Vector3 p, float clearance)
        {
            if (terrain.InSea(p.x, p.z)) return false;
            if (terrain.RoadDistance(p.x, p.z) < clearance) return false;
            return Mathf.Abs(p.x) < map.worldSize * 0.48f && Mathf.Abs(p.z) < map.worldSize * 0.48f;
        }

        public Vector3 Ground(float x, float z) => new Vector3(x, terrain.Height(x, z), z);

        void PlaceGrandstands(Transform parent)
        {
            // Main stand opposite the pits, then stands at the slowest corners.
            var spots = new List<float> { startS - 40f };
            var order = new List<int>();
            for (int i = 0; i < track.Count; i += 10) order.Add(i);
            order.Sort((a, b) => referenceSpeed[a].CompareTo(referenceSpeed[b]));
            foreach (int i in order)
            {
                float d = track.dist[i];
                bool far = true;
                foreach (var s in spots) if (Mathf.Abs(Mathf.DeltaAngle(d / track.Length * 360f, s / track.Length * 360f)) < 300f / track.Length * 360f) far = false;
                if (far) spots.Add(d);
                if (spots.Count >= 6) break;
            }
            for (int k = 0; k < spots.Count; k++)
            {
                int i = track.IndexAt(spots[k]);
                int side = k == 0 ? -pitSide : (track.curvature[i] > 0f ? -1 : 1);
                float off = (side < 0 ? sides.barrierL[i] : sides.barrierR[i]) + 7f;
                Vector3 front = track.pos[i] + track.right[i] * side * off;
                front.y = terrain.Height(front.x, front.z);
                float len = k == 0 ? 120f : 70f;
                bool ok = true;
                foreach (var t in new[] { -0.5f, 0.5f })
                {
                    Vector3 test = front + track.fwd[i] * len * t + track.right[i] * side * 12f;
                    if (!SpotFree(test, 2f)) ok = false;
                }
                if (!ok) continue;
                Furniture.Grandstand(parent, front, -track.right[i] * side, len, k == 0 ? 22 : 14);
            }
        }

        BuildingStyle StreetStyle()
        {
            switch (map.biome)
            {
                case Biome.Riviera: case Biome.Coast: return BuildingStyle.Stucco;
                case Biome.CityNight: case Biome.Gulf: return BuildingStyle.Office;
                case Biome.Neon: return BuildingStyle.Tower;
                case Biome.Port: return BuildingStyle.Warehouse;
                case Biome.Farmland: return BuildingStyle.Village;
                default: return BuildingStyle.Brick;
            }
        }

        void PlaceBuildings(Buildings b)
        {
            var style = StreetStyle();
            if (map.Has(Feature.Street))
            {
                // Rows of buildings along both sides of the street circuit.
                for (int side = -1; side <= 1; side += 2)
                {
                    float d = 0f;
                    while (d < track.Length)
                    {
                        int i = track.IndexAt(d);
                        float w = rng.Range(14f, 30f);
                        float depth = rng.Range(14f, 26f);
                        float off = (side < 0 ? sides.barrierL[i] : sides.barrierR[i]) + 5.5f + depth * 0.5f;
                        Vector3 p = track.pos[i] + track.right[i] * side * off;
                        float h = Height(style);
                        bool ok = SpotFree(p, depth * 0.5f - 2f);
                        if (!map.freeRoam && Mathf.Abs(Mathf.DeltaAngle(d / track.Length * 360f, startS / track.Length * 360f)) < 300f / track.Length * 360f && side == pitSide) ok = false;
                        if (ok)
                        {
                            p.y = terrain.Height(p.x, p.z);
                            b.Add(p, -track.right[i] * side, w, depth, h, style);
                            if (map.Has(Feature.Neon) && rng.Chance(0.5f))
                                Landmarks.NeonSign(root, p - track.right[i] * side * (depth * 0.5f + 0.3f) + Vector3.up * rng.Range(6f, 14f), -track.right[i] * side, rng.Range(8f, 16f), rng.Range(3f, 6f), rng.Range(0, 16));
                        }
                        d += w + rng.Range(1f, 6f);
                    }
                }
            }
            if (map.biome == Biome.CityNight && map.freeRoam)
            {
                // Fill the city blocks between the 200 m street grid.
                Bounds bb = trackBounds; bb.Expand(new Vector3(400f, 0f, 400f));
                for (float x = Mathf.Ceil(bb.min.x / 200f) * 200f; x < bb.max.x - 1f; x += 200f)
                for (float z = Mathf.Ceil(bb.min.z / 200f) * 200f; z < bb.max.z - 1f; z += 200f)
                {
                    Vector3 c = new Vector3(x + 100f, 0f, z + 100f);
                    float distC = new Vector2(c.x - trackBounds.center.x, c.z - trackBounds.center.z).magnitude;
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 p = c + new Vector3(rng.Range(-55f, 55f), 0f, rng.Range(-55f, 55f));
                        float w = rng.Range(24f, 46f), dd = rng.Range(24f, 46f);
                        if (!SpotFree(p, Mathf.Max(w, dd) * 0.5f - 6f)) continue;
                        p.y = terrain.Height(p.x, p.z);
                        float h = Mathf.Lerp(160f, 30f, Mathf.Clamp01(distC / 1400f)) * rng.Range(0.4f, 1.1f);
                        b.Add(p, rng.Chance(0.5f) ? Vector3.forward : Vector3.right, w, dd, h, h > 70f ? BuildingStyle.Tower : BuildingStyle.Office);
                    }
                }
            }
            if (map.Has(Feature.Skyline))
            {
                // A skyline of towers beyond the track.
                for (int k = 0; k < 70; k++)
                {
                    float a = rng.Range(0f, Mathf.PI * 2f);
                    float r = trackBounds.extents.magnitude + rng.Range(150f, 700f);
                    Vector3 p = trackBounds.center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (!SpotFree(p, 20f)) continue;
                    p.y = terrain.Height(p.x, p.z);
                    b.Add(p, -p, rng.Range(30f, 55f), rng.Range(30f, 55f), rng.Range(70f, 240f), BuildingStyle.Tower, false);
                }
            }
            if (!map.Has(Feature.Street) && !map.freeRoam)
            {
                // Paddock and hospitality buildings behind the pits.
                int si = track.IndexAt(startS);
                for (int k = 0; k < 6; k++)
                {
                    int i = track.Wrap(si - 90 + k * 30);
                    float off = (pitSide < 0 ? sides.barrierL[i] : sides.barrierR[i]) + 60f + rng.Range(0f, 20f);
                    Vector3 p = track.pos[i] + track.right[i] * pitSide * off;
                    if (!SpotFree(p, 10f)) continue;
                    p.y = terrain.Height(p.x, p.z);
                    b.Add(p, -track.right[i] * pitSide, rng.Range(20f, 40f), rng.Range(15f, 25f), rng.Range(7f, 12f), BuildingStyle.Office);
                }
            }
            if (map.Has(Feature.Village)) Village(b, map.biome == Biome.Tropical ? BuildingStyle.Stucco : BuildingStyle.Village);
            if (map.biome == Biome.Port) Port();
        }

        float Height(BuildingStyle s)
        {
            switch (s)
            {
                case BuildingStyle.Stucco: return rng.Range(10f, 28f);
                case BuildingStyle.Office: return rng.Range(25f, 90f);
                case BuildingStyle.Tower: return rng.Range(30f, 110f);
                case BuildingStyle.Warehouse: return rng.Range(10f, 16f);
                case BuildingStyle.Village: return rng.Range(6f, 10f);
                default: return rng.Range(9f, 18f);
            }
        }

        void Village(Buildings b, BuildingStyle style)
        {
            // A cluster of houses beside a road somewhere away from the start.
            var r = roads[Mathf.Min(roads.Count - 1, roads.Count > 1 ? 1 : 0)];
            int ci = r.Count / 2;
            for (int k = 0; k < 40; k++)
            {
                int i = r.Wrap(ci + rng.Range(-90, 90));
                int side = rng.Chance(0.5f) ? -1 : 1;
                float off = r.halfWidth[i] + rng.Range(9f, 40f);
                Vector3 p = r.pos[i] + r.right[i] * side * off;
                if (!SpotFree(p, 4f)) continue;
                p.y = terrain.Height(p.x, p.z);
                b.Add(p, -r.right[i] * side, rng.Range(8f, 13f), rng.Range(8f, 11f), rng.Range(5f, 9f), style);
            }
        }

        void Port()
        {
            for (int k = 0; k < 24; k++)
            {
                Vector3 p = FindSpot(25f, 140f);
                if (p.x == float.MaxValue) continue;
                Landmarks.Containers(root, p, rng.Range(0, 4) * 90f, rng.Range(2, 5), rng.Range(3, 8), 4, rng);
            }
        }

        /// <summary>A random spot whose distance to the roads is in [minRd, maxRd]. x = MaxValue if none found.</summary>
        public Vector3 FindSpot(float minRd, float maxRd, int tries = 60)
        {
            Bounds b = trackBounds;
            b.Expand(new Vector3(maxRd * 2f, 0f, maxRd * 2f));
            for (int t = 0; t < tries; t++)
            {
                float x = rng.Range(b.min.x, b.max.x), z = rng.Range(b.min.z, b.max.z);
                float rd = terrain.RoadDistance(x, z);
                if (rd < minRd || rd > maxRd || terrain.InSea(x, z) || terrain.Slope(x, z) > 18f) continue;
                return Ground(x, z);
            }
            return new Vector3(float.MaxValue, 0f, 0f);
        }

        Vector3 FindSeaSpot(float minFromShore, float maxFromShore)
        {
            Bounds b = trackBounds;
            b.Expand(new Vector3(800f, 0f, 800f));
            for (int t = 0; t < 200; t++)
            {
                float x = rng.Range(b.min.x, b.max.x), z = rng.Range(b.min.z, b.max.z);
                if (!terrain.InSea(x, z)) continue;
                // Close to shore: some land within maxFromShore.
                bool near = false, tooNear = false;
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2f;
                    if (!terrain.InSea(x + Mathf.Cos(a) * maxFromShore, z + Mathf.Sin(a) * maxFromShore)) near = true;
                    if (!terrain.InSea(x + Mathf.Cos(a) * minFromShore, z + Mathf.Sin(a) * minFromShore)) tooNear = true;
                }
                if (near && !tooNear) return new Vector3(x, map.seaLevel, z);
            }
            return new Vector3(float.MaxValue, 0f, 0f);
        }

        void PlaceLandmarks(Buildings b)
        {
            var m = map;
            if (m.Has(Feature.Harbour))
                for (int k = 0; k < 26; k++)
                {
                    Vector3 p = FindSeaSpot(25f, 160f);
                    if (p.x != float.MaxValue) Landmarks.Yacht(root, p, rng.Range(0f, 360f), rng.Range(14f, 42f), rng);
                }
            if (m.Has(Feature.Lighthouse))
            {
                Vector3 p = FindSpot(40f, 250f);
                for (int k = 0; k < 30 && p.x != float.MaxValue; k++)
                {
                    // Prefer a headland: near the sea.
                    bool coast = false;
                    for (int a = 0; a < 8; a++) if (terrain.InSea(p.x + Mathf.Cos(a) * 60f, p.z + Mathf.Sin(a) * 60f)) coast = true;
                    if (coast) break;
                    p = FindSpot(40f, 400f);
                }
                if (p.x != float.MaxValue) Landmarks.Lighthouse(root, p);
            }
            if (m.Has(Feature.Pagoda)) { var p = FindSpot(60f, 220f); if (p.x != float.MaxValue) Landmarks.Pagoda(root, p, rng.Range(0f, 90f)); }
            if (m.Has(Feature.FerrisWheel)) { var p = FindSpot(60f, 200f); if (p.x != float.MaxValue) Landmarks.FerrisWheel(root, p, rng.Range(0f, 180f)); }
            if (m.Has(Feature.Castle))
            {
                // On the highest nearby hilltop.
                Vector3 best = new Vector3(float.MaxValue, 0, 0);
                for (int k = 0; k < 40; k++) { var p = FindSpot(120f, 700f, 20); if (p.x != float.MaxValue && (best.x == float.MaxValue || p.y > best.y)) best = p; }
                if (best.x != float.MaxValue) Landmarks.Castle(root, best, rng.Range(0f, 90f));
            }
            if (m.Has(Feature.Windmills))
                for (int k = 0; k < (m.biome == Biome.Outback ? 4 : 7); k++) { var p = FindSpot(80f, 900f, 30); if (p.x != float.MaxValue) Landmarks.Windmill(root, p, rng.Range(0f, 360f), m.biome != Biome.Outback); }
            if (m.Has(Feature.Cranes))
                for (int k = 0; k < 6; k++) { var p = FindSpot(30f, 160f); if (p.x != float.MaxValue) Landmarks.Crane(root, p, rng.Range(0f, 360f), rng); }
            if (m.Has(Feature.Runway))
            {
                // A runway parallel to the long test straight, inside the loop.
                int i = track.IndexAt(track.Length * 0.15f);
                Vector3 c = track.pos[i] + track.right[i] * 260f;
                c.y = terrain.Height(c.x, c.z) + 0.05f;
                Landmarks.Runway(root, c, Quaternion.LookRotation(track.fwd[i]).eulerAngles.y, 2400f, 45f);
                // Hangars along the apron.
                for (int k = 0; k < 4; k++)
                {
                    Vector3 hp = c + track.right[i] * 140f + track.fwd[i] * (k - 1.5f) * 90f;
                    if (SpotFree(hp, 10f)) { hp.y = terrain.Height(hp.x, hp.z); b.Add(hp, -track.right[i], 60f, 45f, 18f, BuildingStyle.Warehouse); }
                }
            }
            if (m.Has(Feature.StadiumBowl))
            {
                int i = track.IndexAt(track.Length * 0.62f);
                Vector3 c = track.pos[i];
                Landmarks.StadiumBowl(root, c, 170f, 120f);
            }
        }

        // ------------------------------------------------------------------ vegetation

        struct SpeciesWeight { public Species s; public float w; public SpeciesWeight(Species s, float w) { this.s = s; this.w = w; } }

        SpeciesWeight[] Mix()
        {
            switch (map.biome)
            {
                case Biome.Riviera: return new[] { new SpeciesWeight(Species.Cypress, 0.3f), new SpeciesWeight(Species.Pine, 0.3f), new SpeciesWeight(Species.Palm, 0.15f), new SpeciesWeight(Species.Bush, 0.25f) };
                case Biome.Parkland: return new[] { new SpeciesWeight(Species.Broadleaf, 0.6f), new SpeciesWeight(Species.Bush, 0.25f), new SpeciesWeight(Species.Fir, 0.15f) };
                case Biome.Desert: return new[] { new SpeciesWeight(Species.Scrub, 0.6f), new SpeciesWeight(Species.Palm, 0.15f), new SpeciesWeight(Species.Rock, 0.25f) };
                case Biome.JapanHills: return new[] { new SpeciesWeight(Species.Fir, 0.45f), new SpeciesWeight(Species.Broadleaf, 0.4f), new SpeciesWeight(Species.Bush, 0.15f) };
                case Biome.Coast: return new[] { new SpeciesWeight(Species.Pine, 0.45f), new SpeciesWeight(Species.Cypress, 0.15f), new SpeciesWeight(Species.Bush, 0.3f), new SpeciesWeight(Species.Rock, 0.1f) };
                case Biome.Alpine: case Biome.Highland: return new[] { new SpeciesWeight(Species.Fir, 0.75f), new SpeciesWeight(Species.Birch, 0.1f), new SpeciesWeight(Species.Rock, 0.15f) };
                case Biome.Tropical: return new[] { new SpeciesWeight(Species.Palm, 0.5f), new SpeciesWeight(Species.Broadleaf, 0.3f), new SpeciesWeight(Species.Bush, 0.2f) };
                case Biome.CityNight: case Biome.Port: return new[] { new SpeciesWeight(Species.Broadleaf, 0.7f), new SpeciesWeight(Species.Bush, 0.3f) };
                case Biome.Neon: case Biome.Gulf: return new[] { new SpeciesWeight(Species.Palm, 0.7f), new SpeciesWeight(Species.Scrub, 0.3f) };
                case Biome.Ardennes: return new[] { new SpeciesWeight(Species.Fir, 0.6f), new SpeciesWeight(Species.Pine, 0.2f), new SpeciesWeight(Species.Broadleaf, 0.2f) };
                case Biome.Farmland: return new[] { new SpeciesWeight(Species.Broadleaf, 0.5f), new SpeciesWeight(Species.Cypress, 0.2f), new SpeciesWeight(Species.Bush, 0.3f) };
                case Biome.Snow: return new[] { new SpeciesWeight(Species.SnowFir, 0.8f), new SpeciesWeight(Species.Birch, 0.1f), new SpeciesWeight(Species.Rock, 0.1f) };
                case Biome.Outback: return new[] { new SpeciesWeight(Species.Scrub, 0.5f), new SpeciesWeight(Species.Broadleaf, 0.3f), new SpeciesWeight(Species.Rock, 0.2f) };
                default: return new[] { new SpeciesWeight(Species.Broadleaf, 0.6f), new SpeciesWeight(Species.Bush, 0.4f) };
            }
        }

        float Density()
        {
            switch (map.biome)
            {
                case Biome.Ardennes: case Biome.Tropical: case Biome.JapanHills: return 1f;
                case Biome.Alpine: case Biome.Highland: case Biome.Snow: return 0.75f;
                case Biome.Parkland: case Biome.Farmland: case Biome.Riviera: case Biome.Coast: return 0.5f;
                case Biome.Outback: return 0.25f;
                case Biome.Desert: case Biome.Gulf: case Biome.Neon: return 0.12f;
                case Biome.CityNight: case Biome.Port: return 0.1f;
                default: return 0.4f;
            }
        }

        IEnumerator ScatterVegetation()
        {
            var mix = Mix();
            float total = 0f; foreach (var e in mix) total += e.w;
            int cap = Settings.Quality == 0 ? 7000 : Settings.Quality == 1 ? 14000 : 22000;
            float density = Density();
            float half = map.worldSize * 0.48f;
            // Concentrate near the roads (what you actually see), thinner far away.
            float area = map.worldSize * map.worldSize;
            int candidates = Mathf.Min(cap * 4, Mathf.RoundToInt(area / 900f * density));
            float treeLine = map.biome == Biome.Alpine || map.biome == Biome.Highland || map.biome == Biome.Snow ? terrain.minY + (terrain.maxY - terrain.minY) * 0.62f : float.MaxValue;
            float s = map.seed * 0.013f;
            int placed = 0;
            for (int c = 0; c < candidates && placed < cap; c++)
            {
                float x = rng.Range(-half, half), z = rng.Range(-half, half);
                float rd = terrain.RoadDistance(x, z);
                if (rd < 3f) continue;
                float keep = rd < 300f ? 1f : rd < 900f ? 0.45f : 0.18f;
                // Forest patches.
                float patch = Noise.Fbm(x * 0.003f + s, z * 0.003f, 3) * 0.5f + 0.5f;
                if (rng.Value > keep * Mathf.Clamp01((patch - 0.35f) * 2.2f + density * 0.3f)) continue;
                float h = terrain.Height(x, z);
                if (map.Has(Feature.Sea) && h < map.seaLevel + 1.5f) continue;
                if (map.terrain == TerrainStyle.Island && h < map.seaLevel + 1.5f) continue;
                if (h > treeLine + rng.Range(-30f, 30f)) continue;
                float slope = terrain.Slope(x, z);
                // Pick a species.
                float pick = rng.Range(0f, total);
                Species sp = mix[0].s;
                foreach (var e in mix) { if (pick < e.w) { sp = e.s; break; } pick -= e.w; }
                if (sp != Species.Rock && slope > 34f) continue;
                if (sp == Species.Rock && slope < 10f && rng.Chance(0.6f)) continue;
                float scale = sp == Species.Rock ? rng.Range(0.6f, 2.4f) : rng.Range(0.75f, 1.3f);
                bool collider = map.freeRoam ? rd < 25f : rd < 6f;
                vegetation.Add(sp, new Vector3(x, h - 0.15f, z), scale, rng.Range(0f, 360f), collider);
                placed++;
                if (placed % 1500 == 0) { Progress = 0.8f + 0.14f * placed / cap; yield return null; }
            }
        }
    }
}
