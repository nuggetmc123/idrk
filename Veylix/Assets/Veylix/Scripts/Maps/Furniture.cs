using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Circuit furniture: pit lane and garages, pit wall, start/finish gantry with working start lights, grid boxes,
    /// the chequered line, grandstands full of people, advertising boards, marshal posts, braking-distance boards,
    /// an advertising bridge, and floodlight towers for night races.
    /// </summary>
    public static class Furniture
    {
        static Material M(System.Func<VXAssets, Material> f, string n, Color c, float s = 0.3f) { var A = VXAssets.I; return VXAssets.Or(A != null ? f(A) : null, n, c, s); }

        // ------------------------------------------------------------------ start / finish

        public static StartLights StartGantry(Transform parent, World w, float startS)
        {
            var s = w.track;
            int i = s.IndexAt(startS);
            Vector3 c = s.pos[i];
            Vector3 r = s.right[i], f = s.fwd[i];
            float span = Mathf.Max(w.sides.barrierL[i], w.sides.barrierR[i]) + 1f;
            var acc = new MeshAcc();
            var rot = Quaternion.LookRotation(f, Vector3.up);
            // Columns and truss beam.
            acc.Box(c - r * w.sides.barrierL[i] - r * 0.6f + Vector3.up * 3.5f, new Vector3(0.5f, 7.5f, 0.5f), rot);
            acc.Box(c + r * w.sides.barrierR[i] + r * 0.6f + Vector3.up * 3.5f, new Vector3(0.5f, 7.5f, 0.5f), rot);
            acc.Box(c + Vector3.up * 7f, new Vector3(span * 2f, 0.9f, 0.6f), rot);
            acc.Box(c + Vector3.up * 6.2f, new Vector3(span * 2f, 0.12f, 0.12f), rot);
            var gantry = acc.Emit(parent, "Gantry", M(a => a.gantry, "Gantry", new Color(0.15f, 0.15f, 0.17f), 0.4f), Layers.Scenery, false);
            // The light panel: five columns of two red lights, facing the grid.
            var panel = new GameObject("StartLights");
            panel.transform.SetParent(parent, false);
            panel.transform.position = c + Vector3.up * 6.1f - f * 0.35f;
            panel.transform.rotation = Quaternion.LookRotation(-f, Vector3.up);
            var lights = panel.AddComponent<StartLights>();
            lights.Build();
            // Chequered line on the road.
            var line = new MeshAcc();
            float hw = s.halfWidth[i];
            Vector3 lc = c + s.up[i] * 0.012f + s.up[i] * 0.05f * 0.5f;
            int a0 = line.Add(lc - r * hw - f * 0.6f, s.up[i], new Vector2(0, 0)), a1 = line.Add(lc + r * hw - f * 0.6f, s.up[i], new Vector2(hw * 2f / 0.6f, 0));
            int a2 = line.Add(lc + r * hw + f * 0.6f, s.up[i], new Vector2(hw * 2f / 0.6f, 2)), a3 = line.Add(lc - r * hw + f * 0.6f, s.up[i], new Vector2(0, 2));
            line.Quad(a0, a1, a2, a3, Vector3.up);
            line.Emit(parent, "FinishLine", M(a => a.flag, "Chequer", Color.white, 0.3f), Layers.Scenery, false, null, false);
            return lights;
        }

        /// <summary>Grid slot positions behind the start line, staggered left/right like a real grid.</summary>
        public static List<Pose> GridSlots(World w, float startS, int count)
        {
            var list = new List<Pose>();
            var s = w.track;
            for (int k = 0; k < count; k++)
            {
                float d = startS - 6f - k * 8f;
                int i = s.IndexAt(d);
                float lat = (k % 2 == 0 ? -1f : 1f) * s.halfWidth[i] * 0.42f;
                Vector3 p = s.PointAt(d, lat) + s.up[i] * 0.15f;
                list.Add(new Pose(p, Quaternion.LookRotation(s.ForwardAt(d), s.up[i])));
            }
            return list;
        }

        public static void GridMarks(Transform parent, World w, List<Pose> slots)
        {
            var acc = new MeshAcc();
            foreach (var p in slots)
            {
                Vector3 f = p.rotation * Vector3.forward, r = p.rotation * Vector3.right, u = p.rotation * Vector3.up;
                Vector3 c = p.position - u * 0.13f + f * 2.8f;
                // White box "bracket": front line and two short side lines.
                Mark(acc, c, r, f, u, 2.2f, 0.18f);
                Mark(acc, c - r * 1.05f - f * 0.6f, f, -r, u, 1.2f, 0.15f);
                Mark(acc, c + r * 1.05f - f * 0.6f, f, -r, u, 1.2f, 0.15f);
            }
            acc.Emit(parent, "GridMarks", M(a => a.linePaint, "Paint", Color.white, 0.4f), Layers.Scenery, false, null, false);
        }

        static void Mark(MeshAcc acc, Vector3 c, Vector3 along, Vector3 across, Vector3 up, float len, float wid)
        {
            int a = acc.Add(c - along * len * 0.5f - across * wid * 0.5f, up, Vector2.zero);
            int b = acc.Add(c + along * len * 0.5f - across * wid * 0.5f, up, Vector2.right);
            int d = acc.Add(c + along * len * 0.5f + across * wid * 0.5f, up, Vector2.one);
            int e = acc.Add(c - along * len * 0.5f + across * wid * 0.5f, up, Vector2.up);
            acc.Quad(a, b, d, e, up);
        }

        // ------------------------------------------------------------------ pits

        public static void PitComplex(Transform parent, World w, float startS, int side)
        {
            var s = w.track;
            var lane = new MeshAcc();
            var wall = new MeshAcc();
            var garage = new MeshAcc();
            var dark = new MeshAcc();
            float from = startS - 260f, to = startS + 200f;
            int steps = Mathf.RoundToInt((to - from) / 4f);
            int prev = -1;
            for (int k = 0; k <= steps; k++)
            {
                float d = from + k * 4f;
                int i = s.IndexAt(d);
                float barrier = side < 0 ? w.sides.barrierL[i] : w.sides.barrierR[i];
                float laneIn = barrier + 1f, laneOut = laneIn + 13f;
                Vector3 p0 = s.PointAt(d, side * laneIn), p1 = s.PointAt(d, side * laneOut);
                p0.y = p1.y = s.pos[i].y - 0.02f;
                int a = lane.Add(p0, Vector3.up, new Vector2(laneIn / 4f, d / 4f)), b = lane.Add(p1, Vector3.up, new Vector2(laneOut / 4f, d / 4f));
                if (prev >= 0) lane.Quad(prev, prev + 1, b, a, Vector3.up);
                prev = a;
            }
            lane.Emit(parent, "PitLane", M(a => a.pitLane, "Pit lane", new Color(0.3f, 0.3f, 0.31f), 0.25f), Layers.Road, true, SurfaceType.Asphalt);

            // Garages: one long building split into team bays with dark open doors.
            int bays = 10;
            float bayLen = 15f;
            float g0 = startS - bays * bayLen * 0.5f - 20f;
            for (int b = 0; b < bays; b++)
            {
                float d = g0 + (b + 0.5f) * bayLen;
                int i = s.IndexAt(d);
                float barrier = side < 0 ? w.sides.barrierL[i] : w.sides.barrierR[i];
                Vector3 front = s.PointAt(d, side * (barrier + 14.5f));
                front.y = s.pos[i].y;
                Vector3 outward = s.right[i] * side;
                var rot = Quaternion.LookRotation(-outward, Vector3.up);
                garage.Box(front + outward * 9f + Vector3.up * 4.5f, new Vector3(bayLen, 9f, 18f), rot, 0.25f);
                // Door opening (dark inset) and a team-coloured band above it.
                Vector3 doorC = front - outward * 0.02f + Vector3.up * 2.4f;
                Vector3 along = s.fwd[i];
                int q0 = dark.Add(doorC - along * (bayLen * 0.42f) - Vector3.up * 2.4f, -outward, Vector2.zero);
                int q1 = dark.Add(doorC + along * (bayLen * 0.42f) - Vector3.up * 2.4f, -outward, Vector2.right);
                int q2 = dark.Add(doorC + along * (bayLen * 0.42f) + Vector3.up * 2.4f, -outward, Vector2.one);
                int q3 = dark.Add(doorC - along * (bayLen * 0.42f) + Vector3.up * 2.4f, -outward, Vector2.up);
                dark.Quad(q0, q1, q2, q3, -outward);
            }
            // Pit wall with its fence and the timing stands.
            prev = -1;
            for (int k = 0; k <= steps; k++)
            {
                float d = from + k * 4f;
                int i = s.IndexAt(d);
                float barrier = side < 0 ? w.sides.barrierL[i] : w.sides.barrierR[i];
                Vector3 b0 = s.PointAt(d, side * barrier);
                b0.y = s.pos[i].y - 0.3f;
                Vector3 r = s.right[i] * side;
                int c0 = wall.Add(b0, -r, new Vector2(d / 4f, 0)), c1 = wall.Add(b0 + Vector3.up * 1.4f, -r, new Vector2(d / 4f, 1)), c2 = wall.Add(b0 + Vector3.up * 1.4f + r * 0.8f, r, new Vector2(d / 4f, 1.2f)), c3 = wall.Add(b0 + r * 0.8f, r, new Vector2(d / 4f, 2f));
                if (prev >= 0) { wall.Quad(prev, c0, c1, prev + 1, -r); wall.Quad(prev + 1, c1, c2, prev + 2, Vector3.up); wall.Quad(prev + 2, c2, c3, prev + 3, r); }
                prev = c0;
            }
            wall.Emit(parent, "PitWall", M(a => a.wallConcrete, "Wall", new Color(0.8f, 0.8f, 0.78f), 0.2f), Layers.Barrier, true, SurfaceType.Concrete);
            garage.Emit(parent, "Garages", M(a => a.buildingOffice, "Garage", new Color(0.8f, 0.8f, 0.82f), 0.4f), Layers.Barrier, true);
            dark.Emit(parent, "GarageDoors", M(a => a.interior, "Dark", new Color(0.04f, 0.04f, 0.05f), 0.1f), Layers.Scenery, false, null, false);
            // Control tower at the end of the pit building.
            int ti = s.IndexAt(g0 + bays * bayLen + 15f);
            float tb = side < 0 ? w.sides.barrierL[ti] : w.sides.barrierR[ti];
            Vector3 tp = s.PointAt(g0 + bays * bayLen + 15f, side * (tb + 24f));
            tp.y = s.pos[ti].y;
            var tower = new MeshAcc();
            tower.Box(tp + Vector3.up * 11f, new Vector3(12f, 22f, 12f), Quaternion.LookRotation(s.fwd[ti]), 0.25f);
            tower.Box(tp + Vector3.up * 23.5f, new Vector3(16f, 3f, 16f), Quaternion.LookRotation(s.fwd[ti]), 0.25f);
            tower.Emit(parent, "ControlTower", M(a => a.buildingOffice, "Tower", new Color(0.8f, 0.82f, 0.86f), 0.5f), Layers.Barrier, true);
        }

        // ------------------------------------------------------------------ grandstands

        public static void Grandstand(Transform parent, Vector3 frontCentre, Vector3 towardTrack, float length, int rows)
        {
            towardTrack.y = 0f; towardTrack.Normalize();
            var rot = Quaternion.LookRotation(towardTrack, Vector3.up);
            Vector3 right = rot * Vector3.right, back = -towardTrack;
            var structure = new MeshAcc();
            var seats = new MeshAcc();
            var crowd = new MeshAcc();
            float rowDepth = 0.9f, rowRise = 0.45f;
            for (int r = 0; r < rows; r++)
            {
                Vector3 c = frontCentre + back * (r * rowDepth + rowDepth * 0.5f) + Vector3.up * (1.5f + r * rowRise);
                seats.Box(c - Vector3.up * 0.25f, new Vector3(length, 0.5f, rowDepth), rot, 0.5f);
                // People: a strip of crowd cards on every row.
                Vector3 pc = c + Vector3.up * 0.55f + towardTrack * 0.05f;
                int a = crowd.Add(pc - right * length * 0.5f - Vector3.up * 0.55f, towardTrack, new Vector2(0, 0));
                int b = crowd.Add(pc + right * length * 0.5f - Vector3.up * 0.55f, towardTrack, new Vector2(length / 6f, 0));
                int d = crowd.Add(pc + right * length * 0.5f + Vector3.up * 0.55f, towardTrack, new Vector2(length / 6f, 1));
                int e = crowd.Add(pc - right * length * 0.5f + Vector3.up * 0.55f, towardTrack, new Vector2(0, 1));
                crowd.Quad(a, b, d, e, towardTrack);
            }
            float depth = rows * rowDepth, topH = 1.5f + rows * rowRise;
            // Back wall, end walls, roof on pillars.
            structure.Box(frontCentre + back * (depth + 0.2f) + Vector3.up * (topH * 0.5f), new Vector3(length, topH + 1f, 0.4f), rot, 0.3f);
            structure.Box(frontCentre + back * depth * 0.5f + right * (length * 0.5f + 0.2f) + Vector3.up * topH * 0.5f, new Vector3(0.4f, topH, depth), rot, 0.3f);
            structure.Box(frontCentre + back * depth * 0.5f - right * (length * 0.5f + 0.2f) + Vector3.up * topH * 0.5f, new Vector3(0.4f, topH, depth), rot, 0.3f);
            structure.Box(frontCentre + Vector3.up * 0.7f, new Vector3(length, 1.4f, 0.3f), rot, 0.3f);
            float roofH = topH + 3.2f;
            structure.Box(frontCentre + back * depth * 0.5f + Vector3.up * roofH, new Vector3(length + 2f, 0.3f, depth + 3f), rot, 0.2f);
            for (float x = -length * 0.5f; x <= length * 0.5f + 0.1f; x += 12f)
                structure.Box(frontCentre + back * (depth + 0.1f) + right * x + Vector3.up * roofH * 0.5f, new Vector3(0.35f, roofH, 0.35f), rot, 0.3f);
            structure.Emit(parent, "Stand", M(a => a.grandstand, "Stand", new Color(0.7f, 0.72f, 0.75f), 0.3f), Layers.Barrier, true);
            seats.Emit(parent, "Seats", M(a => a.grandstand, "Seats", new Color(0.2f, 0.3f, 0.5f), 0.3f), Layers.Scenery, false);
            crowd.Emit(parent, "Crowd", M(a => a.crowd, "Crowd", new Color(0.5f, 0.45f, 0.4f), 0.1f), Layers.Scenery, false, null, false);
        }

        // ------------------------------------------------------------------ boards, posts, lights

        public static void AdBoards(Transform parent, World w, Rng rng)
        {
            var s = w.track;
            var acc = new MeshAcc();
            for (int i = 0; i < s.Count; i += 40)
            {
                if (rng.Chance(0.35f)) continue;
                int side = rng.Chance(0.5f) ? -1 : 1;
                var kind = side < 0 ? w.sides.barrierKindL[i] : w.sides.barrierKindR[i];
                if (kind == BarrierKind.None) continue;
                float off = (side < 0 ? w.sides.barrierL[i] : w.sides.barrierR[i]) + (kind == BarrierKind.Tyres ? 1.15f : 0.6f);
                int panel = rng.Range(0, 6); // the atlas's top row is the wide bridge banner
                Vector3 r = s.right[i] * side;
                for (int k = 0; k < 6; k++)
                {
                    int j = s.Wrap(i + k * 2), j2 = s.Wrap(i + k * 2 + 2);
                    Vector3 p0 = s.pos[j] + s.right[j] * side * off, p1 = s.pos[j2] + s.right[j2] * side * off;
                    float u0 = k / 6f, u1 = (k + 1) / 6f;
                    Vector2 uvA = new Vector2((panel % 2) * 0.5f + u0 * 0.5f, (panel / 2) * 0.25f), uvB = new Vector2((panel % 2) * 0.5f + u1 * 0.5f, (panel / 2) * 0.25f + 0.25f);
                    int a = acc.Add(p0 + Vector3.up * 0.05f, -r, new Vector2(uvA.x, uvA.y));
                    int b = acc.Add(p1 + Vector3.up * 0.05f, -r, new Vector2(uvB.x, uvA.y));
                    int c = acc.Add(p1 + Vector3.up * 1.0f, -r, new Vector2(uvB.x, uvB.y));
                    int d = acc.Add(p0 + Vector3.up * 1.0f, -r, new Vector2(uvA.x, uvB.y));
                    acc.Quad(a, b, c, d, -r);
                }
            }
            acc.Emit(parent, "AdBoards", M(a => a.ads, "Ads", Color.white, 0.4f), Layers.Scenery, false, null, false);
        }

        public static void MarshalPosts(Transform parent, World w)
        {
            var s = w.track;
            var acc = new MeshAcc();
            var roof = new MeshAcc();
            for (int i = 60; i < s.Count; i += 160)
            {
                int side = (i / 160) % 2 == 0 ? 1 : -1;
                float off = (side < 0 ? w.sides.barrierL[i] : w.sides.barrierR[i]) + 3f;
                Vector3 p = s.pos[i] + s.right[i] * side * off;
                var rot = Quaternion.LookRotation(s.fwd[i]);
                acc.Box(p + Vector3.up * 1.1f, new Vector3(2f, 2.2f, 1.6f), rot, 0.5f);
                roof.Box(p + Vector3.up * 2.35f, new Vector3(2.4f, 0.15f, 2f), rot, 0.5f);
            }
            acc.Emit(parent, "MarshalPosts", M(a => a.concrete, "Post", new Color(0.85f, 0.85f, 0.83f), 0.2f), Layers.Barrier, true);
            roof.Emit(parent, "PostRoofs", M(a => a.kerbYellow, "Orange", new Color(0.95f, 0.5f, 0.1f), 0.3f), Layers.Scenery, false);
        }

        /// <summary>100/50 m countdown boards before the heavy braking zones (from the racing line's speed drop).</summary>
        public static void BrakeBoards(Transform parent, World w, float[] speed)
        {
            var s = w.track;
            var acc = new MeshAcc();
            int n = s.Count;
            int last = -999;
            for (int i = 0; i < n; i++)
            {
                float v0 = speed[i], v1 = speed[s.Wrap(i + 60)];
                if (v0 - v1 < 22f || i - last < 150) continue;
                // i is roughly where braking starts: find the apex ahead.
                int apex = i;
                for (int k = 0; k < 120; k++) if (speed[s.Wrap(i + k)] < speed[apex]) apex = s.Wrap(i + k);
                last = i;
                float curvSign = Mathf.Sign(s.curvature[apex]);
                int side = curvSign > 0 ? -1 : 1; // boards on the outside, where the driver looks
                // Corner entry: where the speed has nearly dropped to the apex speed.
                int entry = apex;
                for (int k = 0; k < 120; k++) { int q = s.Wrap(i + k); if (speed[q] <= speed[apex] * 1.08f) { entry = q; break; } }
                for (int b = 0; b < 3; b++)
                {
                    float dist = new[] { 150f, 100f, 50f }[b];
                    int j = s.Wrap(entry - Mathf.RoundToInt(dist / TrackSpline.Step));
                    float off = (side < 0 ? w.sides.barrierL[j] : w.sides.barrierR[j]) - 0.6f;
                    Vector3 p = s.pos[j] + s.right[j] * side * Mathf.Max(s.halfWidth[j] + 2f, off);
                    Vector3 f = s.fwd[j];
                    float u0 = b / 4f, u1 = (b + 1) / 4f;
                    // Board faces oncoming cars.
                    Vector3 r = Vector3.Cross(Vector3.up, -f).normalized;
                    int a0 = acc.Add(p - r * 0.6f + Vector3.up * 0.6f, -f, new Vector2(u0, 0.75f));
                    int a1 = acc.Add(p + r * 0.6f + Vector3.up * 0.6f, -f, new Vector2(u1, 0.75f));
                    int a2 = acc.Add(p + r * 0.6f + Vector3.up * 1.8f, -f, new Vector2(u1, 1f));
                    int a3 = acc.Add(p - r * 0.6f + Vector3.up * 1.8f, -f, new Vector2(u0, 1f));
                    acc.Quad(a0, a1, a2, a3, -f);
                }
            }
            acc.Emit(parent, "BrakeBoards", M(a => a.signs, "Signs", Color.white, 0.3f), Layers.Scenery, false, null, false);
        }

        public static void Bridge(Transform parent, World w, float atS)
        {
            var s = w.track;
            int i = s.IndexAt(atS);
            var rot = Quaternion.LookRotation(s.fwd[i]);
            float span = Mathf.Max(w.sides.barrierL[i], w.sides.barrierR[i]) * 2f + 6f;
            var acc = new MeshAcc();
            Vector3 c = s.pos[i];
            acc.Box(c + Vector3.up * 7.5f, new Vector3(span, 1.6f, 3.2f), rot, 0.4f);
            acc.Box(c - s.right[i] * (span * 0.5f) + Vector3.up * 4f, new Vector3(1.2f, 8.5f, 3.2f), rot, 0.4f);
            acc.Box(c + s.right[i] * (span * 0.5f) + Vector3.up * 4f, new Vector3(1.2f, 8.5f, 3.2f), rot, 0.4f);
            acc.Emit(parent, "Bridge", M(a => a.gantry, "Bridge", new Color(0.25f, 0.25f, 0.28f), 0.4f), Layers.Barrier, true);
            // Banner face on both sides of the span.
            var ads = new MeshAcc();
            foreach (var dir in new[] { 1f, -1f })
            {
                Vector3 f = s.fwd[i] * dir;
                Vector3 bc = c + Vector3.up * 7.5f - f * 1.62f;
                Vector3 r = s.right[i] * dir;
                int a = ads.Add(bc - r * span * 0.45f - Vector3.up * 0.7f, -f, new Vector2(0f, 0.75f));
                int b = ads.Add(bc + r * span * 0.45f - Vector3.up * 0.7f, -f, new Vector2(1f, 0.75f));
                int d = ads.Add(bc + r * span * 0.45f + Vector3.up * 0.7f, -f, new Vector2(1f, 1f));
                int e = ads.Add(bc - r * span * 0.45f + Vector3.up * 0.7f, -f, new Vector2(0f, 1f));
                ads.Quad(a, b, d, e, -f);
            }
            ads.Emit(parent, "BridgeBanner", M(a => a.ads, "Ads", Color.white, 0.4f), Layers.Scenery, false, null, false);
        }

        /// <summary>Floodlight poles every ~45 m (alternating sides), with lamp heads and glow pools on the road.</summary>
        public static List<Vector3> Floodlights(Transform parent, TrackSpline s, RoadSides sides, float spacing)
        {
            var lamps = new List<Vector3>();
            var poles = new MeshAcc();
            var heads = new MeshAcc();
            var pools = new MeshAcc();
            int step = Mathf.Max(4, Mathf.RoundToInt(spacing / TrackSpline.Step));
            for (int i = 0, k = 0; i < s.Count; i += step, k++)
            {
                int side = k % 2 == 0 ? 1 : -1;
                float off = (side < 0 ? sides.barrierL[i] : sides.barrierR[i]) + 1.8f;
                Vector3 r = s.right[i] * side;
                Vector3 b = s.pos[i] + r * off - Vector3.up * 0.3f;
                float h = 12f;
                poles.Box(b + Vector3.up * h * 0.5f, new Vector3(0.3f, h, 0.3f), Quaternion.LookRotation(s.fwd[i]), 0.5f);
                Vector3 arm = b + Vector3.up * h - r * 2.6f;
                poles.Box((b + Vector3.up * h + arm) * 0.5f, new Vector3(0.15f, 0.15f, 2.8f), Quaternion.LookRotation(r), 0.5f);
                heads.Box(arm - Vector3.up * 0.15f, new Vector3(1.4f, 0.25f, 0.6f), Quaternion.LookRotation(s.fwd[i]), 1f);
                lamps.Add(arm - Vector3.up * 0.4f);
                // Light pool: a soft additive disc on the tarmac under the lamp.
                Vector3 pc = s.pos[i] + r * (off - 2.6f - s.halfWidth[i] * 0.4f) * 0.6f + s.up[i] * 0.03f + Vector3.up * 0.04f;
                float rad = s.halfWidth[i] * 1.6f;
                int c0 = pools.Add(pc - s.right[i] * rad - s.fwd[i] * rad, Vector3.up, new Vector2(0, 0));
                int c1 = pools.Add(pc + s.right[i] * rad - s.fwd[i] * rad, Vector3.up, new Vector2(1, 0));
                int c2 = pools.Add(pc + s.right[i] * rad + s.fwd[i] * rad, Vector3.up, new Vector2(1, 1));
                int c3 = pools.Add(pc - s.right[i] * rad + s.fwd[i] * rad, Vector3.up, new Vector2(0, 1));
                pools.Quad(c0, c1, c2, c3, Vector3.up);
            }
            poles.Emit(parent, "LightPoles", M(a => a.gantry, "Pole", new Color(0.4f, 0.4f, 0.42f), 0.5f), Layers.Scenery, false);
            var headGo = heads.Emit(parent, "LampHeads", M(a => a.lampHead, "Lamp", Color.white, 0.5f), Layers.Scenery, false, null, false);
            var poolGo = pools.Emit(parent, "LightPools", M(a => a.lightCone, "Pool", new Color(1f, 0.95f, 0.85f, 0.3f), 0f), Layers.Scenery, false, null, false);
            if (poolGo != null) poolGo.AddComponent<NightOnly>();
            return lamps;
        }
    }

    /// <summary>Shows this object only when it's dark (light pools, lit signs).</summary>
    public class NightOnly : MonoBehaviour
    {
        Renderer r;
        void Awake() { r = GetComponent<Renderer>(); }
        void Update() { if (r != null) r.enabled = Env.Daylight < 0.45f; }
    }

    /// <summary>The F1-style start lights: five columns light one by one, then all go out.</summary>
    public class StartLights : MonoBehaviour
    {
        Renderer[] lamps = new Renderer[10];
        MaterialPropertyBlock mpb;
        int lit;

        public void Build()
        {
            mpb = new MaterialPropertyBlock();
            var mat = VXAssets.Or(VXAssets.I != null ? VXAssets.I.lightRed : null, "Start light", new Color(0.3f, 0f, 0f), 0.6f);
            var housing = VXAssets.Or(VXAssets.I != null ? VXAssets.I.satinBlack : null, "Housing", new Color(0.05f, 0.05f, 0.05f), 0.3f);
            for (int c = 0; c < 5; c++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(transform, false);
                box.transform.localPosition = new Vector3((c - 2) * 0.55f, 0f, 0.05f);
                box.transform.localScale = new Vector3(0.45f, 1.0f, 0.12f);
                box.GetComponent<Renderer>().sharedMaterial = housing;
                for (int r = 0; r < 2; r++)
                {
                    var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(lamp.GetComponent<Collider>());
                    lamp.transform.SetParent(transform, false);
                    lamp.transform.localPosition = new Vector3((c - 2) * 0.55f, 0.22f - r * 0.44f, -0.02f);
                    lamp.transform.localScale = Vector3.one * 0.3f;
                    var rr = lamp.GetComponent<Renderer>();
                    rr.sharedMaterial = mat;
                    lamps[c * 2 + r] = rr;
                }
            }
            Set(0);
        }

        /// <summary>Number of columns lit (0..5).</summary>
        public void Set(int columns)
        {
            lit = columns;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                bool on = i / 2 < columns;
                lamps[i].GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", on ? new Color(1f, 0.02f, 0.01f) * 6f : Color.black);
                mpb.SetColor("_Color", on ? new Color(1f, 0.1f, 0.08f) : new Color(0.12f, 0.02f, 0.02f));
                lamps[i].SetPropertyBlock(mpb);
            }
        }
    }
}
