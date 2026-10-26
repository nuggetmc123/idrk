using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// A road's centre line, resampled every <see cref="Step"/> metres: positions, frames (forward/right/up with a
    /// little banking in corners), curvature and distance. Closed loops are race tracks; open ones are extra roads.
    /// Also answers "where am I on this road" quickly using a spatial grid.
    /// </summary>
    public class TrackSpline
    {
        public const float Step = 2f;
        public Vector3[] pos, fwd, right, up;
        public float[] dist, curvature, bank, halfWidth;
        public bool closed;
        public float Length;
        public int Count => pos.Length;

        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        const float Cell = 40f;

        public static TrackSpline FromControlPoints(IList<Vector3> cps, bool closed, float halfWidth, float maxBankDeg = 3.5f)
        {
            var dense = CatmullRom(cps, closed);
            return FromPolyline(dense, closed, halfWidth, maxBankDeg);
        }

        /// <summary>Centripetal Catmull-Rom (no loops or cusps at uneven spacing).</summary>
        public static List<Vector3> CatmullRom(IList<Vector3> P, bool closed)
        {
            var outPts = new List<Vector3>();
            int n = P.Count;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = P[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)];
                Vector3 p1 = P[i];
                Vector3 p2 = P[(i + 1) % n];
                Vector3 p3 = P[closed ? (i + 2) % n : Mathf.Min(n - 1, i + 2)];
                float t0 = 0f;
                float t1 = t0 + Mathf.Sqrt(Mathf.Max(1e-3f, Flat(p1 - p0)));
                float t2 = t1 + Mathf.Sqrt(Mathf.Max(1e-3f, Flat(p2 - p1)));
                float t3 = t2 + Mathf.Sqrt(Mathf.Max(1e-3f, Flat(p3 - p2)));
                if (p0 == p1) t0 = t1 - 1e-3f;
                if (p3 == p2) t3 = t2 + 1e-3f;
                int steps = Mathf.Max(2, Mathf.CeilToInt(Flat(p2 - p1) / 1.0f));
                for (int k = 0; k < steps; k++)
                {
                    float t = Mathf.Lerp(t1, t2, k / (float)steps);
                    Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
                    Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
                    Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
                    Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
                    Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
                    outPts.Add((t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2);
                }
            }
            if (!closed) outPts.Add(P[n - 1]);
            return outPts;
        }

        static float Flat(Vector3 d) => Mathf.Sqrt(d.x * d.x + d.z * d.z);

        public static TrackSpline FromPolyline(List<Vector3> dense, bool closed, float halfWidth, float maxBankDeg)
        {
            // Arc length of the dense line, then resample uniformly.
            int n = dense.Count;
            var acc = new float[n + 1];
            for (int i = 1; i <= n; i++)
            {
                if (i == n && !closed) { acc[i] = acc[i - 1]; break; }
                acc[i] = acc[i - 1] + Vector3.Distance(dense[i - 1], dense[i % n]);
            }
            float total = closed ? acc[n] : acc[n - 1];
            int count = Mathf.Max(4, Mathf.RoundToInt(total / Step));
            float step = total / count;
            var s = new TrackSpline { closed = closed, Length = total };
            int m = closed ? count : count + 1;
            s.pos = new Vector3[m];
            int j = 0;
            for (int i = 0; i < m; i++)
            {
                float d = i * step;
                while (j < n - 1 && acc[j + 1] < d) j++;
                float segLen = acc[j + 1] - acc[j];
                float t = segLen > 1e-5f ? (d - acc[j]) / segLen : 0f;
                s.pos[i] = Vector3.Lerp(dense[j], dense[(j + 1) % n], Mathf.Clamp01(t));
            }
            s.SmoothElevation(closed ? 6 : 4);
            s.BuildFrames(halfWidth, maxBankDeg);
            s.BuildGrid();
            return s;
        }

        void SmoothElevation(int passes)
        {
            int n = pos.Length;
            var y = new float[n];
            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f; int c = 0;
                    for (int k = -6; k <= 6; k++)
                    {
                        int idx = i + k;
                        if (closed) idx = (idx % n + n) % n; else if (idx < 0 || idx >= n) continue;
                        sum += pos[idx].y; c++;
                    }
                    y[i] = sum / c;
                }
                for (int i = 0; i < n; i++) pos[i].y = y[i];
            }
        }

        public int Wrap(int i) => closed ? ((i % Count) + Count) % Count : Mathf.Clamp(i, 0, Count - 1);

        void BuildFrames(float hw, float maxBankDeg)
        {
            int n = pos.Length;
            fwd = new Vector3[n]; right = new Vector3[n]; up = new Vector3[n];
            dist = new float[n]; curvature = new float[n]; bank = new float[n]; halfWidth = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pos[Wrap(i - 2)], b = pos[Wrap(i + 2)];
                Vector3 f = b - a;
                if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
                fwd[i] = f.normalized;
                dist[i] = i * (Length / (closed ? n : n - 1));
                halfWidth[i] = hw;
            }
            // Signed curvature (1/m, positive = turning right) from heading change.
            for (int i = 0; i < n; i++)
            {
                Vector3 f0 = Flat3(fwd[Wrap(i - 3)]), f1 = Flat3(fwd[Wrap(i + 3)]);
                float ang = Vector3.SignedAngle(f0, f1, Vector3.up) * Mathf.Deg2Rad;
                curvature[i] = ang / (6f * Step);
            }
            // Smooth curvature, derive gentle banking (more in fast long corners).
            var c2 = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f; int c = 0;
                for (int k = -8; k <= 8; k++) { int idx = Wrap(i + k); sum += curvature[idx]; c++; }
                c2[i] = sum / c;
            }
            for (int i = 0; i < n; i++)
            {
                curvature[i] = c2[i];
                float b = Mathf.Clamp(c2[i] * 300f, -1f, 1f) * maxBankDeg;
                bank[i] = b;
                Vector3 flatF = Flat3(fwd[i]);
                Vector3 r = Vector3.Cross(Vector3.up, flatF).normalized;
                Quaternion q = Quaternion.AngleAxis(-b, flatF); // bank into the corner (right turn: right side lower)
                right[i] = q * r;
                // Keep the road's lateral line horizontal except for banking (no twist from elevation changes).
                up[i] = Vector3.Cross(fwd[i], right[i]).normalized;
            }
        }

        static Vector3 Flat3(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward; }

        void BuildGrid()
        {
            grid.Clear();
            for (int i = 0; i < pos.Length; i++)
            {
                long key = Key(pos[i].x, pos[i].z);
                if (!grid.TryGetValue(key, out var list)) { list = new List<int>(); grid[key] = list; }
                list.Add(i);
            }
        }

        static long Key(float x, float z) => ((long)Mathf.FloorToInt(x / Cell) << 32) ^ (uint)Mathf.FloorToInt(z / Cell);

        /// <summary>Nearest sample index to a world point (2D). hint &gt;= 0 searches near the last answer first.</summary>
        public int Nearest(Vector3 p, int hint = -1, float searchRadius = 80f)
        {
            if (hint >= 0)
            {
                int best = hint; float bd = Flat2(pos[hint] - p);
                for (int k = -12; k <= 12; k++)
                {
                    int i = Wrap(hint + k);
                    float d = Flat2(pos[i] - p);
                    if (d < bd) { bd = d; best = i; }
                }
                if (bd < 15f * 15f) return best;
            }
            int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
            int r = Mathf.CeilToInt(searchRadius / Cell);
            int bestI = -1; float bestD = float.MaxValue;
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                long key = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
                if (!grid.TryGetValue(key, out var list)) continue;
                foreach (int i in list)
                {
                    float d = Flat2(pos[i] - p);
                    // Prefer samples at a similar height (bridges, switchbacks stacked on a slope).
                    d += Mathf.Abs(pos[i].y - p.y) > 6f ? 400f : 0f;
                    if (d < bestD) { bestD = d; bestI = i; }
                }
            }
            if (bestI < 0)
            {
                for (int i = 0; i < pos.Length; i += 4)
                {
                    float d = Flat2(pos[i] - p);
                    if (d < bestD) { bestD = d; bestI = i; }
                }
            }
            return bestI;
        }

        static float Flat2(Vector3 d) => d.x * d.x + d.z * d.z;

        /// <summary>Distance along the track (continuous, interpolated) and the signed lateral offset (right +).</summary>
        public void Project(Vector3 p, ref int hint, out float along, out float lateral)
        {
            int i = Nearest(p, hint);
            hint = i;
            Vector3 d = p - pos[i];
            float t = Vector3.Dot(d, fwd[i]);
            along = dist[i] + t;
            if (closed) along = Mathf.Repeat(along, Length);
            lateral = Vector3.Dot(d, right[i]);
        }

        public int IndexAt(float along)
        {
            float f = along / Length * (closed ? Count : Count - 1);
            return Wrap(Mathf.RoundToInt(f));
        }

        public Vector3 PointAt(float along, float lateral = 0f)
        {
            float f = (closed ? Mathf.Repeat(along, Length) : Mathf.Clamp(along, 0f, Length)) / Length * (closed ? Count : Count - 1);
            int i0 = Mathf.FloorToInt(f);
            float t = f - i0;
            int a = Wrap(i0), b = Wrap(i0 + 1);
            Vector3 c = Vector3.Lerp(pos[a], pos[b], t);
            Vector3 r = Vector3.Lerp(right[a], right[b], t);
            return c + r * lateral;
        }

        public Vector3 ForwardAt(float along)
        {
            float f = (closed ? Mathf.Repeat(along, Length) : Mathf.Clamp(along, 0f, Length)) / Length * (closed ? Count : Count - 1);
            int i0 = Mathf.FloorToInt(f);
            return Vector3.Lerp(fwd[Wrap(i0)], fwd[Wrap(i0 + 1)], f - i0).normalized;
        }

        /// <summary>Track-relative distance from a to b going forward (closed loops wrap).</summary>
        public float Ahead(float a, float b)
        {
            float d = b - a;
            if (closed) { d = Mathf.Repeat(d, Length); }
            return d;
        }

        public void Translate(Vector3 offset)
        {
            for (int i = 0; i < pos.Length; i++) pos[i] += offset;
            BuildGrid();
        }
    }
}
