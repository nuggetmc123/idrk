using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    public enum RoadKind { Circuit, Street, Highway, Lane, City }
    public enum BarrierKind { None, Wall, Armco, Tyres }

    /// <summary>
    /// The trackside layout of one road, side by side: kerbs where the corners are, run-off (painted asphalt, gravel
    /// traps, grass) sized to how fast the corner is, and the barrier line. Worked out from the road's curvature so
    /// fast corners get big run-off and slow ones get kerbs and gravel, like a real circuit designer would.
    /// </summary>
    public class RoadSides
    {
        public RoadKind kind;
        public bool[] kerbL, kerbR;
        public float[] asphaltL, asphaltR, gravelL, gravelR, barrierL, barrierR;
        public BarrierKind[] barrierKindL, barrierKindR;
        public float[] severity;

        public float CorridorL(int i) => barrierL[i] + 3f;
        public float CorridorR(int i) => barrierR[i] + 3f;

        public static RoadSides Design(TrackSpline s, RoadKind kind, MapDef map)
        {
            int n = s.Count;
            var r = new RoadSides
            {
                kind = kind,
                kerbL = new bool[n], kerbR = new bool[n],
                asphaltL = new float[n], asphaltR = new float[n], gravelL = new float[n], gravelR = new float[n],
                barrierL = new float[n], barrierR = new float[n],
                barrierKindL = new BarrierKind[n], barrierKindR = new BarrierKind[n], severity = new float[n],
            };
            // Curvature over a longer window tells corner speed; signed (positive = right turn).
            var kappa = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = -15; k <= 15; k++) sum += s.curvature[s.Wrap(i + k)];
                kappa[i] = sum / 31f;
            }
            for (int i = 0; i < n; i++)
            {
                float k = kappa[i];
                float sev = Mathf.Clamp01(Mathf.Abs(k) * 110f);
                r.severity[i] = sev;
                bool rightTurn = k > 0f;
                float hw = s.halfWidth[i];
                switch (kind)
                {
                    case RoadKind.Circuit:
                    {
                        float outside = 5f + sev * 16f, inside = 3.5f + sev * 2.5f;
                        r.asphaltL[i] = rightTurn ? outside : inside;
                        r.asphaltR[i] = rightTurn ? inside : outside;
                        if (map.Has(Feature.GravelTraps) && sev > 0.35f)
                        {
                            // Slow corners: a short tarmac band, then the gravel trap.
                            float g = 6f + sev * 10f;
                            if (rightTurn) { r.gravelL[i] = g; r.asphaltL[i] = 3f; }
                            else { r.gravelR[i] = g; r.asphaltR[i] = 3f; }
                        }
                        r.kerbL[i] = sev > 0.22f;
                        r.kerbR[i] = sev > 0.22f;
                        r.barrierL[i] = hw + 1.3f + r.asphaltL[i] + r.gravelL[i] + 2.5f;
                        r.barrierR[i] = hw + 1.3f + r.asphaltR[i] + r.gravelR[i] + 2.5f;
                        r.barrierKindL[i] = sev > 0.3f && rightTurn ? BarrierKind.Tyres : BarrierKind.Armco;
                        r.barrierKindR[i] = sev > 0.3f && !rightTurn ? BarrierKind.Tyres : BarrierKind.Armco;
                        break;
                    }
                    case RoadKind.Street:
                    {
                        // Walls right at the edge; a little escape room on the outside of the slow corners.
                        r.kerbL[i] = sev > 0.3f && !rightTurn;
                        r.kerbR[i] = sev > 0.3f && rightTurn;
                        r.asphaltL[i] = rightTurn ? sev * 3f : 0.3f;
                        r.asphaltR[i] = rightTurn ? 0.3f : sev * 3f;
                        r.barrierL[i] = hw + 0.7f + r.asphaltL[i] + (r.kerbL[i] ? 1.2f : 0f);
                        r.barrierR[i] = hw + 0.7f + r.asphaltR[i] + (r.kerbR[i] ? 1.2f : 0f);
                        r.barrierKindL[i] = r.barrierKindR[i] = BarrierKind.Wall;
                        break;
                    }
                    case RoadKind.City:
                        r.asphaltL[i] = r.asphaltR[i] = 0f;
                        r.barrierL[i] = r.barrierR[i] = hw + 3.2f; // sidewalk; buildings stand behind it
                        r.barrierKindL[i] = r.barrierKindR[i] = BarrierKind.None;
                        break;
                    default: // Highway / Lane
                    {
                        float shoulder = kind == RoadKind.Highway ? 2f : 0.6f;
                        r.asphaltL[i] = r.asphaltR[i] = shoulder;
                        r.barrierL[i] = r.barrierR[i] = hw + shoulder + 2.5f;
                        r.barrierKindL[i] = r.barrierKindR[i] = BarrierKind.None; // armco decided once the terrain is known
                        break;
                    }
                }
            }
            // Kerbs only as whole stretches (no little fragments), and run-off smoothed along the track.
            Clean(r.kerbL, s, 6); Clean(r.kerbR, s, 6);
            Smooth(r.asphaltL, s, 10); Smooth(r.asphaltR, s, 10); Smooth(r.gravelL, s, 10); Smooth(r.gravelR, s, 10);
            Smooth(r.barrierL, s, 12); Smooth(r.barrierR, s, 12);
            return r;
        }

        static void Clean(bool[] m, TrackSpline s, int minRun)
        {
            int n = m.Length;
            var copy = (bool[])m.Clone();
            // Fill short gaps, then drop short runs.
            for (int i = 0; i < n; i++)
            {
                if (copy[i]) continue;
                bool before = false, after = false;
                for (int k = 1; k <= minRun; k++) { if (copy[s.Wrap(i - k)]) before = true; if (copy[s.Wrap(i + k)]) after = true; }
                if (before && after) m[i] = true;
            }
            copy = (bool[])m.Clone();
            for (int i = 0; i < n; i++)
            {
                if (!copy[i]) continue;
                int run = 0;
                for (int k = -minRun; k <= minRun; k++) if (copy[s.Wrap(i + k)]) run++;
                if (run < minRun) m[i] = false;
            }
        }

        static void Smooth(float[] a, TrackSpline s, int w)
        {
            int n = a.Length;
            var c = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f; int cnt = 0;
                for (int k = -w; k <= w; k++) { sum += a[s.Wrap(i + k)]; cnt++; }
                c[i] = Mathf.Max(a[i] * 0.6f, sum / cnt);
            }
            System.Array.Copy(c, a, n);
        }
    }

    /// <summary>Turns a road spline and its side layout into meshes and colliders.</summary>
    public static class RoadBuilder
    {
        const int Chunk = 120; // samples per mesh chunk (≈240 m), so off-screen parts get culled

        static Material M(System.Func<VXAssets, Material> f, string name, Color c, float smooth = 0.3f)
        {
            var A = VXAssets.I;
            return VXAssets.Or(A != null ? f(A) : null, name, c, smooth);
        }

        public static void Build(Transform parent, TrackSpline s, RoadSides sides, MapDef map, RacingLine line, bool mainRoad)
        {
            var root = new GameObject(mainRoad ? "Track" : "Road").transform;
            root.SetParent(parent, false);
            bool circuit = sides.kind == RoadKind.Circuit || sides.kind == RoadKind.Street;
            var asphalt = circuit ? M(a => a.asphalt, "Asphalt", new Color(0.18f, 0.18f, 0.19f), 0.2f) : M(a => a.asphaltRoad, "Road", new Color(0.2f, 0.2f, 0.21f), 0.2f);
            int n = s.Count;
            int segs = s.closed ? n : n - 1;
            float lift = mainRoad ? 0f : -0.02f; // side roads tuck under the main road where they join
            var acc = new MeshAcc();

            for (int c0 = 0; c0 < segs; c0 += Chunk)
            {
                int c1 = Mathf.Min(segs, c0 + Chunk);
                // ---- road surface (6 strips across for gentle crown and good lighting)
                int lanes = 6;
                for (int i = c0; i <= c1; i++)
                {
                    int si = s.Wrap(i);
                    float hw = s.halfWidth[si];
                    float rl = line != null ? line.offset[si] : 0f;
                    for (int l = 0; l <= lanes; l++)
                    {
                        float lat = Mathf.Lerp(-hw, hw, l / (float)lanes);
                        float crown = (1f - Mathf.Pow(lat / hw, 2f)) * 0.05f;
                        Vector3 p = s.pos[si] + s.right[si] * lat + s.up[si] * (crown + lift);
                        float dist = i * TrackSpline.Step;
                        // uv0 tiles the asphalt; uv1 maps the rubbered-in racing line (x: 0.5 on the line).
                        acc.Add(p, s.up[si], new Vector2(lat / 4f, dist / 4f), new Vector2(Mathf.Clamp01(0.5f + (lat - rl) / (hw * 2f)), dist / 40f), new Color32(255, 255, 255, 255));
                    }
                }
                Strips(acc, c1 - c0 + 1, lanes + 1, Vector3.up);
                acc.Emit(root, "Surface", asphalt, Layers.Road, true, SurfaceType.Asphalt);

                // ---- painted lines
                BuildLines(acc, s, sides, c0, c1, lift + 0.006f);
                acc.Emit(root, "Lines", M(a => a.linePaint, "Paint", new Color(0.9f, 0.9f, 0.88f), 0.4f), Layers.Scenery, false, null, false);

                // ---- kerbs
                if (sides.kind == RoadKind.Circuit || sides.kind == RoadKind.Street)
                {
                    for (int side = -1; side <= 1; side += 2) BuildKerbs(acc, s, side < 0 ? sides.kerbL : sides.kerbR, side, c0, c1);
                    acc.Emit(root, "Kerbs", M(a => map.biome == Biome.Neon || map.biome == Biome.CityNight ? a.kerbBlue : a.kerbRed, "Kerb", new Color(0.8f, 0.1f, 0.1f), 0.35f), Layers.Road, true, SurfaceType.Kerb);
                }

                // ---- run-off and verges
                for (int side = -1; side <= 1; side += 2) BuildBand(acc, s, sides, side, c0, c1, 0, lift);
                acc.Emit(root, "Runoff", circuit ? M(a => a.runoff, "Runoff", new Color(0.25f, 0.3f, 0.28f), 0.25f) : asphalt, Layers.Road, true, circuit ? SurfaceType.PaintedRunoff : SurfaceType.Asphalt);
                for (int side = -1; side <= 1; side += 2) BuildBand(acc, s, sides, side, c0, c1, 1, lift);
                acc.Emit(root, "Gravel", M(a => a.gravelTrap, "Gravel", new Color(0.6f, 0.56f, 0.48f), 0.1f), Layers.Road, true, SurfaceType.Gravel);
                if (sides.kind != RoadKind.Street && sides.kind != RoadKind.City)
                {
                    for (int side = -1; side <= 1; side += 2) BuildBand(acc, s, sides, side, c0, c1, 2, lift);
                    acc.Emit(root, "Verge", M(a => a.grassVerge, "Verge", new Color(0.3f, 0.4f, 0.2f), 0.15f), Layers.Road, true, map.ground);
                }
                if (sides.kind == RoadKind.City || sides.kind == RoadKind.Street)
                {
                    for (int side = -1; side <= 1; side += 2) BuildSidewalk(acc, s, sides, side, c0, c1, lift);
                    acc.Emit(root, "Sidewalk", M(a => a.sidewalk, "Sidewalk", new Color(0.55f, 0.54f, 0.52f), 0.2f), Layers.Road, true, SurfaceType.Concrete);
                }

                // ---- barriers
                BuildBarriers(root, acc, s, sides, c0, c1, map);
            }
        }

        static void Strips(MeshAcc acc, int rows, int cols, Vector3 facing)
        {
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = r * cols + c, b = a + 1, d = a + cols, e = d + 1;
                acc.Quad(a, b, e, d, facing);
            }
        }

        static void BuildLines(MeshAcc acc, TrackSpline s, RoadSides sides, int c0, int c1, float h)
        {
            bool road = sides.kind == RoadKind.Highway || sides.kind == RoadKind.Lane || sides.kind == RoadKind.City;
            float w = road ? 0.15f : 0.25f;
            for (int side = -1; side <= 1; side += 2)
                Line(acc, s, c0, c1, i => side * (s.halfWidth[i] - (road ? 0.35f : 0.15f)), w, h, null);
            if (road)
            {
                // Dashed centre line (solid on tight bends).
                Line(acc, s, c0, c1, i => 0f, 0.15f, h, i => sides.severity[i] > 0.5f || Mathf.Repeat(i * TrackSpline.Step, 12f) < 4.5f);
            }
        }

        static void Line(MeshAcc acc, TrackSpline s, int c0, int c1, System.Func<int, float> lat, float w, float h, System.Func<int, bool> on)
        {
            int prev = -1;
            for (int i = c0; i <= c1; i++)
            {
                int si = s.Wrap(i);
                bool draw = on == null || on(si);
                if (!draw) { prev = -1; continue; }
                float l = lat(si);
                Vector3 c = s.pos[si] + s.up[si] * (h + 0.05f * (1f - Mathf.Pow(Mathf.Clamp(l / s.halfWidth[si], -1f, 1f), 2f)));
                int a = acc.Add(c + s.right[si] * (l - w * 0.5f), s.up[si], new Vector2(0, i * 0.5f));
                int b = acc.Add(c + s.right[si] * (l + w * 0.5f), s.up[si], new Vector2(1, i * 0.5f));
                if (prev >= 0) acc.Quad(prev, prev + 1, b, a, Vector3.up);
                prev = a;
            }
        }

        static readonly Vector2[] KerbProfile = { new Vector2(-0.12f, 0.0f), new Vector2(0.15f, 0.025f), new Vector2(0.75f, 0.04f), new Vector2(1.2f, 0.02f), new Vector2(1.3f, -0.03f) };

        static void BuildKerbs(MeshAcc acc, TrackSpline s, bool[] mask, int side, int c0, int c1)
        {
            int cols = KerbProfile.Length;
            int start = -1;
            for (int i = c0; i <= c1 + 1; i++)
            {
                bool on = i <= c1 && mask[s.Wrap(i)];
                if (on && start < 0) start = i;
                if ((!on || i == c1 + 1) && start >= 0)
                {
                    int end = on ? i : i - 1;
                    int baseIdx = acc.Count;
                    int rows = 0;
                    for (int k = start; k <= Mathf.Min(end + 1, c1); k++)
                    {
                        int si = s.Wrap(k);
                        float hw = s.halfWidth[si];
                        // Taper the height at each end of the kerb.
                        float ends = Mathf.Clamp01(Mathf.Min(k - start, end - k) / 3f);
                        foreach (var p in KerbProfile)
                        {
                            float lat = side * (hw + p.x);
                            Vector3 pos = s.pos[si] + s.right[si] * lat + s.up[si] * (p.y * ends + 0.004f);
                            acc.Add(pos, s.up[si], new Vector2((p.x + 0.12f) / 1.42f, k * TrackSpline.Step / 3f));
                        }
                        rows++;
                    }
                    for (int r = 0; r < rows - 1; r++)
                    for (int c = 0; c < cols - 1; c++)
                    {
                        int a = baseIdx + r * cols + c;
                        acc.Quad(a, a + 1, a + cols + 1, a + cols, Vector3.up);
                    }
                    start = -1;
                }
            }
        }

        /// <summary>band 0 = tarmac run-off, 1 = gravel, 2 = grass verge sloping into the terrain.</summary>
        static void BuildBand(MeshAcc acc, TrackSpline s, RoadSides sides, int side, int c0, int c1, int band, float lift)
        {
            int baseIdx = acc.Count;
            int rows = 0;
            int cols = band == 2 ? 3 : 2;
            for (int i = c0; i <= c1; i++)
            {
                int si = s.Wrap(i);
                float hw = s.halfWidth[si];
                bool kerb = side < 0 ? sides.kerbL[si] : sides.kerbR[si];
                float inner = hw + (kerb ? 1.25f : 0f);
                float asph = side < 0 ? sides.asphaltL[si] : sides.asphaltR[si];
                float grav = side < 0 ? sides.gravelL[si] : sides.gravelR[si];
                float barrier = side < 0 ? sides.barrierL[si] : sides.barrierR[si];
                float a0, a1;
                if (band == 0) { a0 = inner; a1 = inner + asph; }
                else if (band == 1) { a0 = inner + asph; a1 = inner + asph + grav; }
                else { a0 = inner + asph + grav; a1 = Mathf.Max(a0 + 3f, barrier + 4f); }
                float drop0 = (a0 - hw) * 0.02f; // run-off falls away slightly for drainage
                for (int c = 0; c < cols; c++)
                {
                    float t = c / (float)(cols - 1);
                    float lat = Mathf.Lerp(a0, a1, t);
                    float y = -(lat - hw) * 0.02f - 0.005f + lift;
                    if (band == 1) y -= 0.04f;
                    if (band == 2 && c == cols - 1) y -= 0.6f; // dive under the terrain edge
                    Vector3 p = s.pos[si] + s.right[si] * side * lat + Vector3.up * y;
                    acc.Add(p, Vector3.up, new Vector2(lat / 3f, i * TrackSpline.Step / 3f));
                }
                rows++;
            }
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = baseIdx + r * cols + c;
                acc.Quad(a, a + 1, a + cols + 1, a + cols, Vector3.up);
            }
            // Remove zero-width rows' triangles: harmless (degenerate), the GPU skips them.
        }

        static void BuildSidewalk(MeshAcc acc, TrackSpline s, RoadSides sides, int side, int c0, int c1, float lift)
        {
            int baseIdx = acc.Count;
            int rows = 0;
            for (int i = c0; i <= c1; i++)
            {
                int si = s.Wrap(i);
                float hw = s.halfWidth[si];
                float inner = sides.kind == RoadKind.Street ? (side < 0 ? sides.barrierL[si] : sides.barrierR[si]) + 0.4f : hw;
                float outer = inner + 4f;
                Vector3 b = s.pos[si] + Vector3.up * lift;
                Vector3 r = s.right[si] * side;
                acc.Add(b + r * inner + Vector3.up * 0.0f, -r, new Vector2(0, i * 0.5f));
                acc.Add(b + r * inner + Vector3.up * 0.15f, Vector3.up, new Vector2(0.05f, i * 0.5f));
                acc.Add(b + r * outer + Vector3.up * 0.15f, Vector3.up, new Vector2(1.3f, i * 0.5f));
                acc.Add(b + r * outer + Vector3.down * 0.6f, Vector3.up, new Vector2(1.5f, i * 0.5f));
                rows++;
            }
            for (int rr = 0; rr < rows - 1; rr++)
            for (int c = 0; c < 3; c++)
            {
                int a = baseIdx + rr * 4 + c;
                acc.Quad(a, a + 1, a + 5, a + 4, c == 0 ? -s.right[s.Wrap(c0 + rr)] * side : Vector3.up);
            }
        }

        // ------------------------------------------------------------------ barriers

        static void BuildBarriers(Transform root, MeshAcc acc, TrackSpline s, RoadSides sides, int c0, int c1, MapDef map)
        {
            var wallMat = M(a => a.wallConcrete, "Wall", new Color(0.75f, 0.75f, 0.72f), 0.2f);
            var armcoMat = M(a => a.armco, "Armco", new Color(0.7f, 0.72f, 0.74f), 0.6f);
            var tyreMat = M(a => a.tyreWall, "Tyres", new Color(0.08f, 0.08f, 0.08f), 0.2f);
            var fenceMat = M(a => a.fence, "Fence", new Color(0.4f, 0.4f, 0.4f), 0.4f);
            foreach (var kind in new[] { BarrierKind.Wall, BarrierKind.Armco, BarrierKind.Tyres })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var kinds = side < 0 ? sides.barrierKindL : sides.barrierKindR;
                    var offs = side < 0 ? sides.barrierL : sides.barrierR;
                    int prev = -1;
                    for (int i = c0; i <= c1; i++)
                    {
                        int si = s.Wrap(i);
                        if (kinds[si] != kind) { prev = -1; continue; }
                        Vector3 r = s.right[si] * side;
                        Vector3 b = s.pos[si] + r * offs[si];
                        float ground = Mathf.Min(0f, -(offs[si] - s.halfWidth[si]) * 0.02f) - 0.3f;
                        Vector3 g = b + Vector3.up * ground;
                        float h = kind == BarrierKind.Wall ? 1.05f : kind == BarrierKind.Tyres ? 0.9f : 0.75f;
                        float th = kind == BarrierKind.Wall ? 0.5f : kind == BarrierKind.Tyres ? 1.0f : 0.25f;
                        float v = i * TrackSpline.Step / (kind == BarrierKind.Tyres ? 0.6f : 4f);
                        int cur = acc.Count;
                        // Track face, top, back face (barrier cross-section).
                        acc.Add(g, -r, new Vector2(v, 0));
                        acc.Add(b + Vector3.up * (h + 0.0f), -r, new Vector2(v, 1));
                        acc.Add(b + Vector3.up * h + r * th, Vector3.up, new Vector2(v, 1.2f));
                        acc.Add(g + r * th, r, new Vector2(v, 2f));
                        if (prev >= 0)
                        {
                            acc.Quad(prev, cur, cur + 1, prev + 1, -r);
                            acc.Quad(prev + 1, cur + 1, cur + 2, prev + 2, Vector3.up);
                            acc.Quad(prev + 2, cur + 2, cur + 3, prev + 3, r);
                        }
                        prev = cur;
                    }
                }
                var mat = kind == BarrierKind.Wall ? wallMat : kind == BarrierKind.Armco ? armcoMat : tyreMat;
                acc.Emit(root, kind + "s", mat, Layers.Barrier, true, SurfaceType.Concrete);
            }
            // Catch / debris fence above walls and tyre walls (see-through mesh, with posts).
            for (int side = -1; side <= 1; side += 2)
            {
                var kinds = side < 0 ? sides.barrierKindL : sides.barrierKindR;
                var offs = side < 0 ? sides.barrierL : sides.barrierR;
                int prev = -1;
                for (int i = c0; i <= c1; i++)
                {
                    int si = s.Wrap(i);
                    bool on = kinds[si] == BarrierKind.Wall || kinds[si] == BarrierKind.Tyres || (kinds[si] == BarrierKind.Armco && sides.severity[si] > 0.25f);
                    if (!on) { prev = -1; continue; }
                    Vector3 r = s.right[si] * side;
                    float back = kinds[si] == BarrierKind.Tyres ? 1.2f : kinds[si] == BarrierKind.Wall ? 0.3f : 0.6f;
                    Vector3 b = s.pos[si] + r * (offs[si] + back);
                    float h0 = kinds[si] == BarrierKind.Armco ? 0.2f : 1.0f;
                    int cur = acc.Count;
                    float u = i * TrackSpline.Step / 3f;
                    acc.Add(b + Vector3.up * h0, -r, new Vector2(u, 0));
                    acc.Add(b + Vector3.up * 3.6f + r * 0.4f, -r, new Vector2(u, 1));
                    if (prev >= 0) acc.Quad(prev, cur, cur + 1, prev + 1, -r);
                    prev = cur;
                }
            }
            var fence = acc.Emit(root, "Fence", fenceMat, Layers.Barrier, false, null, false);
            if (fence != null)
            {
                // Double-sided look comes from the material (cull off); add a simple collider so a flying car is caught.
                Obj.SafeMeshCollider(fence, fence.GetComponent<MeshFilter>().sharedMesh);
            }
        }
    }
}
