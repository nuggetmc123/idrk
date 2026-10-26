using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The racing line: a minimum-curvature path inside the track edges (found by relaxation), plus a speed profile
    /// for a given car: the corner speed its grip and downforce allow, then braking and acceleration passes so the
    /// target speed is always reachable. Used by the AI, the practice-mode line overlay and the brake markers.
    /// </summary>
    public class RacingLine
    {
        public readonly TrackSpline track;
        public float[] offset;    // lateral offset of the line from the centre (m, right +)
        public float[] lineCurv;  // curvature of the line itself

        public RacingLine(TrackSpline t, float margin = 1.4f)
        {
            track = t;
            int n = t.Count;
            offset = new float[n];
            lineCurv = new float[n];
            var tmp = new float[n];
            // Coarse-to-fine relaxation: straighten the path at several scales, clamped to the track.
            foreach (int k in new[] { 24, 16, 10, 6, 3 })
            {
                for (int iter = 0; iter < 60; iter++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int a = t.Wrap(i - k), b = t.Wrap(i + k);
                        Vector3 pa = t.pos[a] + t.right[a] * offset[a];
                        Vector3 pb = t.pos[b] + t.right[b] * offset[b];
                        Vector3 mid = (pa + pb) * 0.5f;
                        float o = Vector3.Dot(mid - t.pos[i], t.right[i]);
                        float lim = t.halfWidth[i] - margin;
                        tmp[i] = Mathf.Clamp(Mathf.Lerp(offset[i], o, 0.55f), -lim, lim);
                    }
                    if (!t.closed) { tmp[0] = 0f; tmp[n - 1] = 0f; }
                    System.Array.Copy(tmp, offset, n);
                }
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = Point(t.Wrap(i - 4)), p1 = Point(i), p2 = Point(t.Wrap(i + 4));
                lineCurv[i] = Curv(p0, p1, p2);
            }
            // Light smoothing of curvature.
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int k = -3; k <= 3; k++) s += lineCurv[t.Wrap(i + k)];
                tmp[i] = s / 7f;
            }
            System.Array.Copy(tmp, lineCurv, n);
        }

        public Vector3 Point(int i) => track.pos[i] + track.right[i] * offset[i];

        static float Curv(Vector3 a, Vector3 b, Vector3 c)
        {
            a.y = b.y = c.y = 0f;
            float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c), ac = Vector3.Distance(a, c);
            float area2 = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
            float den = ab * bc * ac;
            return den > 1e-5f ? -2f * area2 / den : 0f; // signed: positive = right turn
        }

        /// <summary>Target speed (m/s) along the line for a car, with a skill factor (0.8..1.0 of the limit).</summary>
        public float[] SpeedProfile(CarSpec spec, float skill, float wetness)
        {
            int n = track.Count;
            var v = new float[n];
            float mass = spec.mass + 120f;
            float kAero = 0.5f * Car.AirDensity * spec.downforceClA / mass;
            float mu = spec.gripMu * Mathf.Lerp(1f, 0.62f, wetness) * skill * 0.92f;
            float vmax = 95f;
            for (int i = 0; i < n; i++)
            {
                float c = Mathf.Abs(lineCurv[i]);
                float den = c - mu * kAero;
                v[i] = den <= 1e-5f ? vmax : Mathf.Min(vmax, Mathf.Sqrt(mu * Car.Gravity / den));
            }
            float ds = track.Length / n;
            float dragK = 0.5f * Car.AirDensity * spec.dragCdA / mass;
            float powerW = spec.peakTorque * spec.peakTorqueRpm * 2f * Mathf.PI / 60f * 1.1f + spec.hybridKw * 1000f;
            // Acceleration pass (forward) and braking pass (backward), twice around for closed loops.
            int passes = track.closed ? 2 : 1;
            for (int p = 0; p < passes; p++)
                for (int k = 1; k <= n; k++)
                {
                    int i = track.Wrap(k), prev = track.Wrap(k - 1);
                    if (!track.closed && k >= n) break;
                    float vp = v[prev];
                    float traction = mu * (Car.Gravity + kAero * vp * vp) * 0.75f;
                    float powerAcc = powerW / mass / Mathf.Max(5f, vp);
                    float a = Mathf.Min(traction, powerAcc) - dragK * vp * vp;
                    v[i] = Mathf.Min(v[i], Mathf.Sqrt(Mathf.Max(0f, vp * vp + 2f * Mathf.Max(0.2f, a) * ds)));
                }
            for (int p = 0; p < passes; p++)
                for (int k = n - 2; k >= -1; k--)
                {
                    if (!track.closed && k < 0) break;
                    int i = track.Wrap(k), nxt = track.Wrap(k + 1);
                    float vn = v[nxt];
                    float decel = mu * (Car.Gravity + kAero * vn * vn) * 0.88f + dragK * vn * vn;
                    v[i] = Mathf.Min(v[i], Mathf.Sqrt(vn * vn + 2f * decel * ds));
                }
            return v;
        }
    }
}
