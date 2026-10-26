using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Builds meshes out of parametric pieces: lofts (smooth bodies from cross-sections), lathes (wheels, helmets),
    /// tubes along curves (halo, suspension), airfoil wings, boxes and plates. Each piece goes into a material slot
    /// (sub-mesh) and gets smooth normals of its own, so hard edges stay hard between pieces and curved surfaces stay
    /// seamless inside a piece.
    /// </summary>
    public class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Vector2> uv2s = new List<Vector2>();
        readonly List<Color32> cols = new List<Color32>();
        readonly Dictionary<int, List<int>> tris = new Dictionary<int, List<int>>();
        public Matrix4x4 matrix = Matrix4x4.identity;
        public Color32 color = new Color32(255, 255, 255, 255);
        public int VertexCount => verts.Count;

        List<int> T(int slot)
        {
            if (!tris.TryGetValue(slot, out var l)) { l = new List<int>(); tris[slot] = l; }
            return l;
        }

        int pieceStart;
        void BeginPiece() { pieceStart = verts.Count; }

        int V(Vector3 p, Vector2 uv)
        {
            verts.Add(matrix.MultiplyPoint3x4(p));
            norms.Add(Vector3.zero);
            uvs.Add(uv);
            uv2s.Add(uv);
            cols.Add(color);
            return verts.Count - 1;
        }

        int V(Vector3 p, Vector3 n, Vector2 uv)
        {
            verts.Add(matrix.MultiplyPoint3x4(p));
            norms.Add(matrix.MultiplyVector(n).normalized);
            uvs.Add(uv);
            uv2s.Add(uv);
            cols.Add(color);
            return verts.Count - 1;
        }

        void Tri(int slot, int a, int b, int c) { var l = T(slot); l.Add(a); l.Add(b); l.Add(c); }
        void Quad(int slot, int a, int b, int c, int d) { Tri(slot, a, b, c); Tri(slot, a, c, d); }

        /// <summary>Smooth normals for the current piece, welding vertices that share a position (closes UV seams).</summary>
        void EndPieceSmooth(int slot, int triStart, float weldAngle = 70f)
        {
            int n = verts.Count - pieceStart;
            if (n <= 0) return;
            var acc = new Vector3[n];
            var list = T(slot);
            for (int i = triStart; i < list.Count; i += 3)
            {
                int a = list[i], b = list[i + 1], c = list[i + 2];
                if (a < pieceStart || b < pieceStart || c < pieceStart) continue;
                Vector3 fn = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                acc[a - pieceStart] += fn; acc[b - pieceStart] += fn; acc[c - pieceStart] += fn;
            }
            // Weld by position.
            var map = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                Vector3 p = verts[pieceStart + i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
                if (!map.TryGetValue(key, out var group)) { group = new List<int>(); map[key] = group; }
                group.Add(i);
            }
            float cosLimit = Mathf.Cos(weldAngle * Mathf.Deg2Rad);
            var result = new Vector3[n];
            foreach (var g in map.Values)
            {
                foreach (int i in g)
                {
                    Vector3 own = acc[i].normalized;
                    Vector3 sum = Vector3.zero;
                    foreach (int j in g)
                        if (Vector3.Dot(own, acc[j].normalized) > cosLimit) sum += acc[j];
                    result[i] = sum.sqrMagnitude > 1e-12f ? sum.normalized : Vector3.up;
                }
            }
            for (int i = 0; i < n; i++) norms[pieceStart + i] = result[i];
        }

        // ------------------------------------------------------------------ loft

        public struct Station
        {
            public float z, cx, cy, hw, hh, power, flatBottom, top;
            /// <summary>z along the car, centre (cx,cy), half width/height, superellipse power (2 = ellipse, 4+ = boxy),
            /// flatBottom 0..1 squashes the lower half flat.</summary>
            public Station(float z, float cy, float hw, float hh, float power = 2.5f, float cx = 0f, float flatBottom = 0f)
            { this.z = z; this.cx = cx; this.cy = cy; this.hw = hw; this.hh = hh; this.power = power; this.flatBottom = flatBottom; top = 1f; }
        }

        static Vector2 SuperEllipse(float t, float hw, float hh, float p, float flatBottom)
        {
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            float e = 2f / p;
            float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * hw;
            float y = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * hh;
            if (y < 0f) y *= 1f - flatBottom * 0.85f;
            return new Vector2(x, y);
        }

        static Station Lerp(Station a, Station b, float t)
        {
            return new Station
            {
                z = Mathf.Lerp(a.z, b.z, t), cx = Mathf.Lerp(a.cx, b.cx, t), cy = Mathf.Lerp(a.cy, b.cy, t),
                hw = Mathf.Lerp(a.hw, b.hw, t), hh = Mathf.Lerp(a.hh, b.hh, t), power = Mathf.Lerp(a.power, b.power, t),
                flatBottom = Mathf.Lerp(a.flatBottom, b.flatBottom, t), top = 1f,
            };
        }

        /// <summary>Catmull-Rom between stations so the body flows instead of kinking at each station.</summary>
        static Station Spline(Station[] s, float u)
        {
            float f = u * (s.Length - 1);
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, s.Length - 2);
            float t = f - i;
            Station p0 = s[Mathf.Max(0, i - 1)], p1 = s[i], p2 = s[i + 1], p3 = s[Mathf.Min(s.Length - 1, i + 2)];
            float CR(float a, float b, float c, float d) => 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (-a + 3f * b - 3f * c + d) * t * t * t);
            return new Station
            {
                z = CR(p0.z, p1.z, p2.z, p3.z), cx = CR(p0.cx, p1.cx, p2.cx, p3.cx), cy = CR(p0.cy, p1.cy, p2.cy, p3.cy),
                hw = Mathf.Max(0.002f, CR(p0.hw, p1.hw, p2.hw, p3.hw)), hh = Mathf.Max(0.002f, CR(p0.hh, p1.hh, p2.hh, p3.hh)),
                power = Mathf.Lerp(p1.power, p2.power, t), flatBottom = Mathf.Lerp(p1.flatBottom, p2.flatBottom, t), top = 1f,
            };
        }

        /// <summary>
        /// A closed smooth body through the stations (ordered front to back or back to front). UV: u along the body
        /// mapped into uvRect.x..x+w, v around (0 = bottom centre, 0.5 = top centre) into uvRect.y..y+h.
        /// openTop > 0 leaves a slot on top (radians each side of the top centre) with an inner wall of 'wall' thickness:
        /// used for the cockpit tub.
        /// </summary>
        public void Loft(int slot, Station[] stations, int around = 28, int along = 40, Rect? uvRect = null, int capSlotFront = -1, int capSlotBack = -1, float openTop = 0f, float wall = 0.02f, int innerSlot = -1)
        {
            Rect r = uvRect ?? new Rect(0, 0, 1, 1);
            BeginPiece();
            int triStart = T(slot).Count;
            bool open = openTop > 0f;
            int ring = around + 1;
            float t0 = -Mathf.PI / 2f; // bottom centre
            float span = Mathf.PI * 2f;
            if (open) { t0 = Mathf.PI / 2f + openTop; span = Mathf.PI * 2f - openTop * 2f; }
            var sts = new Station[along + 1];
            for (int j = 0; j <= along; j++) sts[j] = Spline(stations, j / (float)along);
            int baseIdx = verts.Count;
            for (int j = 0; j <= along; j++)
            {
                var st = sts[j];
                for (int i = 0; i <= around; i++)
                {
                    float t = t0 + span * i / around;
                    Vector2 p = SuperEllipse(t, st.hw, st.hh, st.power, st.flatBottom);
                    float vAround = open ? i / (float)around : i / (float)around;
                    V(new Vector3(st.cx + p.x, st.cy + p.y, st.z), new Vector2(r.x + r.width * (j / (float)along), r.y + r.height * vAround));
                }
            }
            for (int j = 0; j < along; j++)
            for (int i = 0; i < around; i++)
            {
                int a = baseIdx + j * ring + i, b = a + 1, c = a + ring + 1, d = a + ring;
                // Winding: outward normals for stations ordered with decreasing z (front to back); check and flip below.
                Quad(slot, a, d, c, b);
            }
            FixWinding(slot, triStart, baseIdx, sts);
            EndPieceSmooth(slot, triStart);

            if (open)
            {
                // Inner wall and the rim along the top edge.
                int innerSlotUse = innerSlot >= 0 ? innerSlot : slot;
                BeginPiece();
                int innerTri = T(innerSlotUse).Count;
                int innerBase = verts.Count;
                for (int j = 0; j <= along; j++)
                {
                    var st = sts[j];
                    for (int i = 0; i <= around; i++)
                    {
                        float t = t0 + span * i / around;
                        Vector2 p = SuperEllipse(t, Mathf.Max(0.005f, st.hw - wall), Mathf.Max(0.005f, st.hh - wall), st.power, st.flatBottom);
                        V(new Vector3(st.cx + p.x, st.cy + p.y, st.z), new Vector2(j / (float)along, i / (float)around));
                    }
                }
                for (int j = 0; j < along; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = innerBase + j * ring + i, b = a + 1, c = a + ring + 1, d = a + ring;
                    Quad(innerSlotUse, a, b, c, d);
                }
                FixWindingInner(innerSlotUse, innerTri, innerBase, sts);
                EndPieceSmooth(innerSlotUse, innerTri);
                // Rims (two edges along the slot) as flat strips.
                for (int side = 0; side < 2; side++)
                {
                    int i = side == 0 ? 0 : around;
                    BeginPiece();
                    int rimTri = T(slot).Count;
                    int rb = verts.Count;
                    for (int j = 0; j <= along; j++)
                    {
                        V(verts[baseIdx + j * ring + i], Vector2.zero);
                        V(verts[innerBase + j * ring + i], Vector2.one);
                    }
                    for (int j = 0; j < along; j++)
                    {
                        int a = rb + j * 2;
                        if (side == 0) Quad(slot, a, a + 1, a + 3, a + 2); else Quad(slot, a, a + 2, a + 3, a + 1);
                    }
                    FixFacing(slot, rimTri, Vector3.up);
                    EndPieceSmooth(slot, rimTri, 30f);
                }
            }
            else
            {
                if (capSlotFront >= 0) Cap(capSlotFront, sts[0], around, true, baseIdx, ring, stations);
                if (capSlotBack >= 0) Cap(capSlotBack, sts[along], around, false, baseIdx + along * ring, ring, stations);
            }
        }

        void FixWinding(int slot, int triStart, int baseIdx, Station[] sts)
        {
            // Compare a face normal near the top with the outward direction (up) and flip all if needed.
            var list = T(slot);
            if (list.Count - triStart < 3) return;
            int mid = sts.Length / 2;
            Vector3 centre = matrix.MultiplyPoint3x4(new Vector3(sts[mid].cx, sts[mid].cy, sts[mid].z));
            float score = 0f;
            for (int i = triStart; i < list.Count; i += 3)
            {
                Vector3 a = verts[list[i]], b = verts[list[i + 1]], c = verts[list[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 outward = (a + b + c) / 3f - centre;
                score += Vector3.Dot(n, outward);
                if (i - triStart > 600) break;
            }
            if (score < 0f) Flip(list, triStart);
        }

        void FixWindingInner(int slot, int triStart, int baseIdx, Station[] sts)
        {
            var list = T(slot);
            if (list.Count - triStart < 3) return;
            int mid = sts.Length / 2;
            Vector3 centre = matrix.MultiplyPoint3x4(new Vector3(sts[mid].cx, sts[mid].cy, sts[mid].z));
            float score = 0f;
            for (int i = triStart; i < list.Count; i += 3)
            {
                Vector3 a = verts[list[i]], b = verts[list[i + 1]], c = verts[list[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                score += Vector3.Dot(n, (a + b + c) / 3f - centre);
                if (i - triStart > 600) break;
            }
            if (score > 0f) Flip(list, triStart);
        }

        void FixFacing(int slot, int triStart, Vector3 desired)
        {
            var list = T(slot);
            float score = 0f;
            for (int i = triStart; i < list.Count; i += 3)
            {
                Vector3 a = verts[list[i]], b = verts[list[i + 1]], c = verts[list[i + 2]];
                score += Vector3.Dot(Vector3.Cross(b - a, c - a), matrix.MultiplyVector(desired));
            }
            if (score < 0f) Flip(list, triStart);
        }

        static void Flip(List<int> list, int start)
        {
            for (int i = start; i < list.Count; i += 3) { int t = list[i + 1]; list[i + 1] = list[i + 2]; list[i + 2] = t; }
        }

        void Cap(int slot, Station st, int around, bool front, int ringStart, int ring, Station[] all)
        {
            BeginPiece();
            int triStart = T(slot).Count;
            Vector3 centre = new Vector3(st.cx, st.cy, st.z);
            int c = V(centre, new Vector2(0.5f, 0.5f));
            int first = verts.Count;
            for (int i = 0; i <= around; i++)
            {
                Vector3 p = verts[ringStart + i];
                Vector3 local = matrix.inverse.MultiplyPoint3x4(p);
                V(local, new Vector2(0.5f + (local.x - st.cx) / (st.hw * 2f + 1e-4f), 0.5f + (local.y - st.cy) / (st.hh * 2f + 1e-4f)));
            }
            for (int i = 0; i < around; i++) Tri(slot, c, first + i, first + i + 1);
            // Face away from the body.
            float dirZ = all[0].z > all[all.Length - 1].z ? (front ? 1f : -1f) : (front ? -1f : 1f);
            FixFacing(slot, triStart, new Vector3(0f, 0f, dirZ));
            for (int i = pieceStart; i < verts.Count; i++) norms[i] = matrix.MultiplyVector(new Vector3(0f, 0f, dirZ)).normalized;
        }

        // ------------------------------------------------------------------ lathe (revolve a profile around local X axis)

        /// <summary>Revolves (radius, x) profile points around the X axis. Used for tyres, rims, helmets.</summary>
        public void Lathe(int slot, Vector2[] profile, int segments, float vScale = 1f, bool smooth = true, float uRepeat = 1f, float startAngle = 0f, float sweep = 360f)
        {
            BeginPiece();
            int triStart = T(slot).Count;
            int baseIdx = verts.Count;
            int ring = segments + 1;
            float accLen = 0f;
            var vs = new float[profile.Length];
            for (int k = 1; k < profile.Length; k++) { accLen += Vector2.Distance(profile[k], profile[k - 1]); vs[k] = accLen; }
            for (int k = 0; k < profile.Length; k++)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float a = (startAngle + sweep * i / segments) * Mathf.Deg2Rad;
                    float rr = profile[k].x;
                    V(new Vector3(profile[k].y, Mathf.Cos(a) * rr, Mathf.Sin(a) * rr), new Vector2(i / (float)segments * uRepeat, vs[k] / Mathf.Max(1e-4f, accLen) * vScale));
                }
            }
            for (int k = 0; k < profile.Length - 1; k++)
            for (int i = 0; i < segments; i++)
            {
                int a = baseIdx + k * ring + i, b = a + 1, c = a + ring + 1, d = a + ring;
                Quad(slot, a, b, c, d);
            }
            // Outward = away from the axis on average.
            var list = T(slot);
            float score = 0f;
            for (int i = triStart; i < list.Count; i += 3)
            {
                Vector3 pa = matrix.inverse.MultiplyPoint3x4(verts[list[i]]), pb = matrix.inverse.MultiplyPoint3x4(verts[list[i + 1]]), pc = matrix.inverse.MultiplyPoint3x4(verts[list[i + 2]]);
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                Vector3 m = (pa + pb + pc) / 3f;
                score += Vector3.Dot(n, new Vector3(0f, m.y, m.z));
            }
            if (score < 0f) Flip(list, triStart);
            EndPieceSmooth(slot, triStart, smooth ? 60f : 1f);
        }

        // ------------------------------------------------------------------ tube along a curve

        public void Tube(int slot, Vector3[] path, float radius, int sides = 10, bool capEnds = true, float radiusEnd = -1f)
        {
            if (path.Length < 2) return;
            BeginPiece();
            int triStart = T(slot).Count;
            int baseIdx = verts.Count;
            int ring = sides + 1;
            Vector3 prevNormal = Vector3.zero;
            for (int k = 0; k < path.Length; k++)
            {
                Vector3 tan = k == 0 ? path[1] - path[0] : k == path.Length - 1 ? path[k] - path[k - 1] : path[k + 1] - path[k - 1];
                tan.Normalize();
                Vector3 nrm = k == 0 ? Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized : Vector3.ProjectOnPlane(prevNormal, tan).normalized;
                prevNormal = nrm;
                Vector3 bin = Vector3.Cross(tan, nrm);
                float rr = radiusEnd < 0f ? radius : Mathf.Lerp(radius, radiusEnd, k / (float)(path.Length - 1));
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    V(path[k] + dir * rr, dir, new Vector2(i / (float)sides, k / (float)(path.Length - 1)));
                }
            }
            for (int k = 0; k < path.Length - 1; k++)
            for (int i = 0; i < sides; i++)
            {
                int a = baseIdx + k * ring + i, b = a + 1, c = a + ring + 1, d = a + ring;
                Quad(slot, a, d, c, b);
            }
            // Check against the stored outward normals.
            var list = T(slot);
            if (list.Count > triStart)
            {
                Vector3 a0 = verts[list[triStart]], b0 = verts[list[triStart + 1]], c0 = verts[list[triStart + 2]];
                if (Vector3.Dot(Vector3.Cross(b0 - a0, c0 - a0), norms[list[triStart]]) < 0f) Flip(list, triStart);
            }
            if (capEnds)
            {
                Disc(slot, path[0], (path[0] - path[1]).normalized, radius, sides);
                Disc(slot, path[path.Length - 1], (path[path.Length - 1] - path[path.Length - 2]).normalized, radiusEnd < 0f ? radius : radiusEnd, sides);
            }
        }

        public void Disc(int slot, Vector3 centre, Vector3 normal, float radius, int sides)
        {
            BeginPiece();
            Vector3 t1 = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(normal, t1);
            int c = V(centre, normal, new Vector2(0.5f, 0.5f));
            int first = verts.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                V(centre + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * radius, normal, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            int triStart = T(slot).Count;
            for (int i = 0; i < sides; i++) Tri(slot, c, first + i, first + i + 1);
            var list = T(slot);
            Vector3 a0 = verts[list[triStart]], b0 = verts[list[triStart + 1]], c0 = verts[list[triStart + 2]];
            if (Vector3.Dot(Vector3.Cross(b0 - a0, c0 - a0), matrix.MultiplyVector(normal)) < 0f) Flip(list, triStart);
        }

        // ------------------------------------------------------------------ boxes and plates

        /// <summary>Axis-aligned box (in the current matrix) with per-face UVs scaled by size (so textures don't stretch).</summary>
        public void Box(int slot, Vector3 centre, Vector3 size, float uvScale = 1f, float bevel = 0f)
        {
            BeginPiece();
            Vector3 h = size * 0.5f;
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 nn = n[f];
                Vector3 u = Mathf.Abs(nn.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, nn);
                Vector3 v = Vector3.Cross(nn, u);
                float su = Mathf.Abs(Vector3.Dot(u, size)), sv = Mathf.Abs(Vector3.Dot(v, size));
                Vector3 c = centre + Vector3.Scale(nn, h);
                Vector3 hu = u * su * 0.5f, hv = v * sv * 0.5f;
                int a = V(c - hu - hv, nn, new Vector2(0, 0));
                int b = V(c + hu - hv, nn, new Vector2(su * uvScale, 0));
                int cc = V(c + hu + hv, nn, new Vector2(su * uvScale, sv * uvScale));
                int d = V(c - hu + hv, nn, new Vector2(0, sv * uvScale));
                Quad(slot, a, d, cc, b);
                var list = T(slot);
                int s = list.Count - 6;
                Vector3 p0 = verts[list[s]], p1 = verts[list[s + 1]], p2 = verts[list[s + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), matrix.MultiplyVector(nn)) < 0f) { Flip(list, s); }
            }
        }

        /// <summary>A flat quad from four corners (counter-clockwise seen from the front), double-sided optional.</summary>
        public void QuadFace(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool doubleSided = false, Rect? uv = null)
        {
            Rect r = uv ?? new Rect(0, 0, 1, 1);
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i0 = V(a, n, new Vector2(r.xMin, r.yMin)), i1 = V(b, n, new Vector2(r.xMax, r.yMin)), i2 = V(c, n, new Vector2(r.xMax, r.yMax)), i3 = V(d, n, new Vector2(r.xMin, r.yMax));
            Tri(slot, i0, i2, i1); Tri(slot, i0, i3, i2);
            var list = T(slot);
            int s = list.Count - 6;
            if (Vector3.Dot(Vector3.Cross(verts[list[s + 1]] - verts[list[s]], verts[list[s + 2]] - verts[list[s]]), matrix.MultiplyVector(n)) < 0f) Flip(list, s);
            if (doubleSided)
            {
                int j0 = V(a, -n, new Vector2(r.xMin, r.yMin)), j1 = V(b, -n, new Vector2(r.xMax, r.yMin)), j2 = V(c, -n, new Vector2(r.xMax, r.yMax)), j3 = V(d, -n, new Vector2(r.xMin, r.yMax));
                Tri(slot, j0, j1, j2); Tri(slot, j0, j2, j3);
                s = list.Count - 6;
                if (Vector3.Dot(Vector3.Cross(verts[list[s + 1]] - verts[list[s]], verts[list[s + 2]] - verts[list[s]]), matrix.MultiplyVector(-n)) < 0f) Flip(list, s);
            }
        }

        /// <summary>A thick plate from a 2D outline (in the XY plane of the current matrix), extruded along Z by depth.</summary>
        public void Plate(int slot, Vector2[] outline, float depth, int edgeSlot = -1, Rect? uv = null)
        {
            Rect r = uv ?? new Rect(0, 0, 1, 1);
            if (edgeSlot < 0) edgeSlot = slot;
            Vector2 min = outline[0], max = outline[0];
            foreach (var p in outline) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 size = max - min;
            var idx = Triangulate(outline);
            for (int side = 0; side < 2; side++)
            {
                BeginPiece();
                float z = side == 0 ? depth * 0.5f : -depth * 0.5f;
                Vector3 n = side == 0 ? Vector3.forward : Vector3.back;
                int b = verts.Count;
                // The back face's u runs the other way, so text reads correctly from both sides.
                foreach (var p in outline)
                {
                    float fu = (p.x - min.x) / Mathf.Max(1e-4f, size.x);
                    if (side == 0) fu = 1f - fu;
                    V(new Vector3(p.x, p.y, z), n, new Vector2(r.x + r.width * fu, r.y + r.height * (p.y - min.y) / Mathf.Max(1e-4f, size.y)));
                }
                int ts = T(slot).Count;
                for (int i = 0; i < idx.Count; i += 3) Tri(slot, b + idx[i], b + idx[i + 1], b + idx[i + 2]);
                FixFacing(slot, ts, n);
            }
            // Edge band.
            BeginPiece();
            int es = T(edgeSlot).Count;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Length];
                Vector3 e = new Vector3(q.x - p.x, q.y - p.y, 0f);
                Vector3 n = new Vector3(e.y, -e.x, 0f).normalized;
                int a = V(new Vector3(p.x, p.y, depth * 0.5f), n, new Vector2(0, 0));
                int b = V(new Vector3(q.x, q.y, depth * 0.5f), n, new Vector2(1, 0));
                int c = V(new Vector3(q.x, q.y, -depth * 0.5f), n, new Vector2(1, 1));
                int d = V(new Vector3(p.x, p.y, -depth * 0.5f), n, new Vector2(0, 1));
                int s0 = T(edgeSlot).Count;
                Quad(edgeSlot, a, b, c, d);
                var list = T(edgeSlot);
                if (Vector3.Dot(Vector3.Cross(verts[list[s0 + 1]] - verts[list[s0]], verts[list[s0 + 2]] - verts[list[s0]]), matrix.MultiplyVector(n)) < 0f) Flip(list, s0);
            }
        }

        /// <summary>Ear-clipping triangulation of a simple polygon.</summary>
        public static List<int> Triangulate(Vector2[] poly)
        {
            var result = new List<int>();
            int n = poly.Length;
            if (n < 3) return result;
            var idx = new List<int>();
            float area = 0f;
            for (int i = 0; i < n; i++) { var p = poly[i]; var q = poly[(i + 1) % n]; area += p.x * q.y - q.x * p.y; }
            for (int i = 0; i < n; i++) idx.Add(area > 0 ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                    Vector2 pa = poly[a], pb = poly[b], pc = poly[c];
                    if ((pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x) <= 0f) continue;
                    bool inside = false;
                    foreach (int k in idx)
                    {
                        if (k == a || k == b || k == c) continue;
                        if (PointInTri(poly[k], pa, pb, pc)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(a); result.Add(b); result.Add(c);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ------------------------------------------------------------------ airfoil wing

        /// <summary>A NACA-style cambered aerofoil profile (chord along -Z from the leading edge, thickness in Y).</summary>
        public static Vector2[] Airfoil(float chord, float thickness = 0.1f, float camber = 0.06f, int n = 10)
        {
            var upper = new List<Vector2>();
            var lower = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                float x = (1f - Mathf.Cos(i / (float)n * Mathf.PI)) * 0.5f;
                float yt = 5f * thickness * (0.2969f * Mathf.Sqrt(x) - 0.126f * x - 0.3516f * x * x + 0.2843f * x * x * x - 0.1036f * x * x * x * x);
                float yc = camber * 4f * x * (1f - x);
                upper.Add(new Vector2(x * chord, (yc + yt) * chord));
                lower.Add(new Vector2(x * chord, (yc - yt) * chord));
            }
            var poly = new List<Vector2>();
            for (int i = 0; i <= n; i++) poly.Add(upper[i]);
            for (int i = n - 1; i >= 1; i--) poly.Add(lower[i]);
            return poly.ToArray();
        }

        /// <summary>
        /// Sweeps an aerofoil along X from xFrom to xTo. angleDeg tilts the element (positive = trailing edge up, i.e. more
        /// downforce). yAt/zAt let the element curve along its span (dihedral / sweep).
        /// </summary>
        public void Wing(int slot, float xFrom, float xTo, float chord, float angleDeg, float y, float z, float thickness = 0.09f, float camber = 0.07f,
            System.Func<float, float> yAt = null, System.Func<float, float> zAt = null, int spanSteps = 8, Rect? uv = null)
        {
            Rect r = uv ?? new Rect(0, 0, 1, 1);
            var prof = Airfoil(chord, thickness, camber);
            BeginPiece();
            int triStart = T(slot).Count;
            int baseIdx = verts.Count;
            int m = prof.Length;
            var rot = Quaternion.Euler(-angleDeg, 0f, 0f);
            for (int s = 0; s <= spanSteps; s++)
            {
                float t = s / (float)spanSteps;
                float x = Mathf.Lerp(xFrom, xTo, t);
                float yy = y + (yAt != null ? yAt(t) : 0f);
                float zz = z + (zAt != null ? zAt(t) : 0f);
                for (int k = 0; k <= m; k++)
                {
                    Vector2 p = prof[k % m];
                    Vector3 local = rot * new Vector3(0f, p.y, -p.x);
                    V(new Vector3(x, yy + local.y, zz + local.z), new Vector2(r.x + r.width * t, r.y + r.height * (k / (float)m)));
                }
            }
            int ring = m + 1;
            for (int s = 0; s < spanSteps; s++)
            for (int k = 0; k < m; k++)
            {
                int a = baseIdx + s * ring + k, b = a + 1, c = a + ring + 1, d = a + ring;
                Quad(slot, a, b, c, d);
            }
            // Outward from the profile's centroid.
            var list = T(slot);
            float score = 0f;
            for (int i = triStart; i < list.Count; i += 3)
            {
                Vector3 pa = verts[list[i]], pb = verts[list[i + 1]], pc = verts[list[i + 2]];
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                int ringIndex = (list[i] - baseIdx) / ring;
                Vector3 centroid = Vector3.zero;
                for (int k = 0; k < m; k++) centroid += verts[baseIdx + ringIndex * ring + k];
                centroid /= m;
                score += Vector3.Dot(n, (pa + pb + pc) / 3f - centroid);
                if (i - triStart > 300) break;
            }
            if (score < 0f) Flip(list, triStart);
            EndPieceSmooth(slot, triStart, 50f);
            // End caps.
            for (int e = 0; e < 2; e++)
            {
                float t = e;
                float x = e == 0 ? xFrom : xTo;
                float yy = y + (yAt != null ? yAt(t) : 0f);
                float zz = z + (zAt != null ? zAt(t) : 0f);
                var outline = new Vector2[m];
                for (int k = 0; k < m; k++) { Vector3 l = rot * new Vector3(0f, prof[k].y, -prof[k].x); outline[k] = new Vector2(l.z, l.y); }
                var tri = Triangulate(outline);
                BeginPiece();
                int b0 = verts.Count;
                Vector3 n = e == 0 ? Vector3.left : Vector3.right;
                foreach (var o in outline) V(new Vector3(x, yy + o.y, zz + o.x), n, new Vector2(0.5f, 0.5f));
                int ts = T(slot).Count;
                for (int i = 0; i < tri.Count; i += 3) Tri(slot, b0 + tri[i], b0 + tri[i + 1], b0 + tri[i + 2]);
                FixFacing(slot, ts, n);
            }
        }

        // ------------------------------------------------------------------ build

        public Mesh Build(string name, int slotCount = -1)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            int max = slotCount;
            foreach (var k in tris.Keys) max = Mathf.Max(max, k + 1);
            mesh.subMeshCount = Mathf.Max(1, max);
            for (int i = 0; i < mesh.subMeshCount; i++)
                mesh.SetTriangles(tris.TryGetValue(i, out var l) ? l : new List<int>(), i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public bool HasSlot(int slot) => tris.ContainsKey(slot) && tris[slot].Count > 0;

        public void Clear()
        {
            verts.Clear(); norms.Clear(); uvs.Clear(); uv2s.Clear(); cols.Clear(); tris.Clear();
            matrix = Matrix4x4.identity;
        }

        public static Matrix4x4 TRS(Vector3 p, Vector3 euler, Vector3 scale) => Matrix4x4.TRS(p, Quaternion.Euler(euler), scale);
        public static Matrix4x4 TRS(Vector3 p, Vector3 euler) => Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one);
    }
}
