using Nullbound.Art;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Body-part meshes for fighters that read as anatomy instead of tubes: a muscular, tapering limb (thick at the
    /// shoulder or hip, a bulge for the bicep or thigh, slimmer at the wrist or ankle) and a V-shaped torso (broad
    /// rounded shoulders and chest, narrower waist, squarer cross-section). Both follow Unity's capsule convention
    /// (1 wide, 2 tall, centred) so a recipe can swap Shape.Capsule for them without changing any sizes. The limb's
    /// thick end is at -Y, where limb bones start. Pure maths (also used by the preview tool).
    /// </summary>
    public static class FighterShapes
    {
        const int Rings = 26, Segments = 18;

        /// <summary>Muscular limb (thick end at -Y).</summary>
        public static MeshData Limb()
        {
            return Lathe((t, angle) =>
            {
                // t: 0 at the root end (-Y), 1 at the far end (+Y).
                float bulge = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.04f) / 0.72f));
                float r = 0.5f * (0.96f - 0.24f * t + 0.1f * bulge);
                // Slightly flattened front to back near the far end (forearms, shins).
                return new Vector2(r, r * (1f - 0.1f * t));
            }, 0.48f, 0.37f);
        }

        /// <summary>V-shaped torso: broad top, narrower waist, squarish section, fuller at the chest.</summary>
        public static MeshData Torso()
        {
            return Lathe((t, angle) =>
            {
                // t: 0 at the bottom (waist), 1 at the top (shoulders).
                float width = 0.5f * (0.8f + 0.2f * Smooth(0.15f, 0.75f, t));
                float chest = Mathf.Exp(-Mathf.Pow((t - 0.66f) / 0.22f, 2f));
                float depth = 0.5f * (0.88f + 0.12f * chest);
                return new Vector2(width, depth);
            }, 0.42f, 0.36f, 3.2f, 0.05f);
        }

        /// <summary>
        /// A sculpted human head in metres (for headScale 1, centred where the head bone is, facing +Z): a skull that's
        /// fuller at the back, a jaw that narrows to a chin, cheekbones, a brow ridge, eye sockets for the eyes to sit
        /// in, a nose and lips. The ears, eyes, brows and mouth are separate parts on top.
        /// </summary>
        public static MeshData HumanHead()
        {
            const int Lat = 44, Lon = 56;
            var pos = new Vector3[Lat + 1, Lon + 1];
            for (int i = 0; i <= Lat; i++)
            {
                float phi = -Mathf.PI * 0.5f + Mathf.PI * i / Lat; // bottom to top
                for (int j = 0; j <= Lon; j++)
                {
                    // Longitude 0 at the back, so the seam is behind the head.
                    float th = Mathf.PI * 2f * (j % Lon) / Lon;
                    var d = new Vector3(Mathf.Sin(th + Mathf.PI) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Cos(th + Mathf.PI) * Mathf.Cos(phi));
                    pos[i, j] = d * HeadRadius(d);
                }
            }
            var m = new MeshData();
            for (int i = 0; i <= Lat; i++)
                for (int j = 0; j <= Lon; j++)
                {
                    int jn = (j + 1) % Lon, jp = (j - 1 + Lon) % Lon;
                    Vector3 du = pos[i, jn] - pos[i, jp];
                    Vector3 dv = pos[Mathf.Min(Lat, i + 1), j % Lon] - pos[Mathf.Max(0, i - 1), j % Lon];
                    Vector3 n = Vector3.Cross(dv, du);
                    if (i == 0) n = Vector3.down;
                    else if (i == Lat) n = Vector3.up;
                    if (Vector3.Dot(n, pos[i, j % Lon]) < 0f) n = -n;
                    n.Normalize();
                    Vector3 t = du.sqrMagnitude > 1e-12f ? du.normalized : Vector3.right;
                    m.Add(pos[i, j % Lon], n, new Vector4(t.x, t.y, t.z, 1f), new Vector2((float)j / Lon * 0.7f, (float)i / Lat * 0.4f));
                }
            int W = Lon + 1;
            for (int i = 0; i < Lat; i++)
                for (int j = 0; j < Lon; j++)
                {
                    int a = i * W + j, b = a + 1, c = a + W + 1, dd = a + W;
                    // Make sure each triangle faces out (works whichever way the grid runs).
                    Vector3 fn = Vector3.Cross(m.vertices[dd] - m.vertices[a], m.vertices[c] - m.vertices[a]);
                    if (Vector3.Dot(fn, m.vertices[a] + m.vertices[c]) >= 0f) { m.Tri(a, dd, c); m.Tri(a, c, b); }
                    else { m.Tri(a, c, dd); m.Tri(a, b, c); }
                }
            return m;
        }

        /// <summary>
        /// A boot / foot in metres for the foot bone (ankle at the origin, sole at y = -0.088, toes towards +Z), sized
        /// for legThick 0.14 (scale x for other widths): a rounded heel, an ankle collar, the instep sloping down to a
        /// rounded toe box.
        /// </summary>
        public static MeshData Boot()
        {
            const float sole = -0.088f;
            // z, half-width, top
            var sections = new (float z, float hw, float top)[]
            {
                (-0.092f, 0.012f, -0.02f), (-0.088f, 0.04f, 0.035f), (-0.074f, 0.055f, 0.06f), (-0.045f, 0.062f, 0.066f),
                (-0.01f, 0.064f, 0.064f), (0.03f, 0.066f, 0.045f), (0.07f, 0.068f, 0.012f), (0.11f, 0.068f, -0.016f),
                (0.15f, 0.064f, -0.035f), (0.18f, 0.054f, -0.047f), (0.2f, 0.036f, -0.056f), (0.21f, 0.01f, -0.066f),
            };
            const int Around = 24;
            var pos = new Vector3[sections.Length, Around];
            var centres = new Vector3[sections.Length];
            for (int i = 0; i < sections.Length; i++)
            {
                var sct = sections[i];
                float cy = (sct.top + sole) * 0.5f, hy = (sct.top - sole) * 0.5f;
                centres[i] = new Vector3(0f, cy, sct.z);
                for (int j = 0; j < Around; j++)
                {
                    float a = j * Mathf.PI * 2f / Around;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    // Flat-ish sole, rounder top.
                    float e = sn < 0f ? 0.45f : 0.8f;
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * sct.hw;
                    float y = cy + Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e) * hy;
                    pos[i, j] = new Vector3(x, y, sct.z);
                }
            }
            return Loft(pos, centres, true, true);
        }

        /// <summary>
        /// An armour plate for shoulders and knees, filling the space of a unit sphere's top: a dome made of three
        /// overlapping bands (lames) with a flared rim. Scale it like the sphere it replaces.
        /// </summary>
        public static MeshData Plate()
        {
            const int Rows = 30, Around = 28;
            var pos = new Vector3[Rows + 1, Around];
            var centres = new Vector3[Rows + 1];
            for (int i = 0; i <= Rows; i++)
            {
                float t = (float)i / Rows;                 // 0 top .. 1 bottom of the skirt
                float dome = Mathf.Min(1f, t / 0.85f);
                float y = t <= 0.85f ? 0.5f * Mathf.Cos(dome * Mathf.PI * 0.5f) - 0.02f : -0.02f - (t - 0.85f) / 0.15f * 0.16f;
                float r = 0.5f * Mathf.Sin(dome * Mathf.PI * 0.5f);
                float band = dome * 3f;
                float u = band - Mathf.Floor(band);
                if (t > 0.05f && t <= 0.85f) r *= 1f + 0.07f * u;             // each band overlaps the next
                if (t > 0.78f && t <= 0.85f) r *= 1.05f;                      // flared rim
                if (t > 0.85f) r = Mathf.Lerp(0.5f * 1.12f, 0.02f, (t - 0.85f) / 0.15f); // underside back in
                centres[i] = new Vector3(0f, y, 0f);
                for (int j = 0; j < Around; j++)
                {
                    float a = j * Mathf.PI * 2f / Around;
                    pos[i, j] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                }
            }
            return Loft(pos, centres, false, false);
        }

        /// <summary>
        /// Rings of points (ring i, point j going round) joined into a closed surface, with optional end caps. Normals and
        /// triangle winding are made to face away from each ring's centre, whichever way the rings run.
        /// </summary>
        static MeshData Loft(Vector3[,] pos, Vector3[] centres, bool capStart, bool capEnd)
        {
            int n = pos.GetLength(0), around = pos.GetLength(1);
            var d = new MeshData();
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= around; j++)
                {
                    int jj = j % around;
                    Vector3 du = pos[i, (jj + 1) % around] - pos[i, (jj + around - 1) % around];
                    Vector3 dv = pos[Mathf.Min(n - 1, i + 1), jj] - pos[Mathf.Max(0, i - 1), jj];
                    Vector3 nn = Vector3.Cross(du, dv);
                    Vector3 outward = pos[i, jj] - centres[i];
                    if (outward.sqrMagnitude < 1e-10f) outward = centres[Mathf.Min(n - 1, i + 1)] - centres[Mathf.Max(0, i - 1)];
                    if (Vector3.Dot(nn, outward) < 0f) nn = -nn;
                    if (nn.sqrMagnitude < 1e-12f) nn = outward;
                    nn.Normalize();
                    Vector3 t = du.sqrMagnitude > 1e-12f ? du.normalized : Vector3.right;
                    d.Add(pos[i, jj], nn, new Vector4(t.x, t.y, t.z, 1f), new Vector2((float)j / around * 0.3f, i * 0.02f));
                }
            int W = around + 1;
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < around; j++)
                {
                    int a = i * W + j, b = a + 1, c = a + W + 1, e = a + W;
                    Tri(d, a, e, c, (centres[i] + centres[i + 1]) * 0.5f);
                    Tri(d, a, c, b, (centres[i] + centres[i + 1]) * 0.5f);
                }
            void Cap(int i, Vector3 axis)
            {
                int centre = d.vertices.Count;
                d.Add(centres[i], axis, new Vector4(1f, 0f, 0f, 1f), Vector2.zero);
                int first = d.vertices.Count;
                for (int j = 0; j < around; j++) d.Add(pos[i, j], axis, new Vector4(1f, 0f, 0f, 1f), Vector2.zero);
                for (int j = 0; j < around; j++) Tri(d, centre, first + j, first + (j + 1) % around, centres[i] - axis);
            }
            if (capStart && n > 1) Cap(0, (centres[0] - centres[1]).normalized);
            if (capEnd && n > 1) Cap(n - 1, (centres[n - 1] - centres[n - 2]).normalized);
            return d;
        }

        /// <summary>Adds a triangle facing away from `inside` (Unity: clockwise seen from outside).</summary>
        static void Tri(MeshData d, int a, int b, int c, Vector3 inside)
        {
            Vector3 pa = d.vertices[a], pb = d.vertices[b], pc = d.vertices[c];
            Vector3 n = Vector3.Cross(pb - pa, pc - pa);
            if (Vector3.Dot(n, (pa + pb + pc) / 3f - inside) >= 0f) d.Tri(a, b, c);
            else d.Tri(a, c, b);
        }

        static float Bump(Vector3 d, Vector3 centre, float sx, float sy, float sz)
        {
            Vector3 q = d - centre;
            return Mathf.Exp(-(q.x * q.x / (sx * sx) + q.y * q.y / (sy * sy) + q.z * q.z / (sz * sz)));
        }

        /// <summary>The head's distance from its centre in direction d (unit).</summary>
        static float HeadRadius(Vector3 d)
        {
            // Skull: rounder and a little wider at the back and crown; the face is flatter and the jaw narrows smoothly
            // down to the chin.
            float a = 0.104f, b = 0.124f, c = d.z < 0f ? 0.118f : 0.108f;
            float lower = Smooth(-0.05f, -0.95f, d.y);
            a *= 1f - 0.22f * lower;
            if (d.z < 0f) c *= 1f - 0.3f * lower; // the back of the head tucks in above the neck
            float r = 1f / Mathf.Sqrt(d.x * d.x / (a * a) + d.y * d.y / (b * b) + d.z * d.z / (c * c));
            if (d.z <= 0f) return r;
            float front = Smooth(0f, 0.7f, d.z);
            // Flatten the face a little across the front (cheeks), so features stand out.
            r -= 0.006f * Bump(d, new Vector3(0f, -0.05f, 1f), 0.45f, 0.45f, 0.6f);
            // Features (heights in metres).
            r += 0.016f * Bump(d, new Vector3(0f, 0.32f, 0.95f), 0.42f, 0.09f, 0.5f) * front;          // brow ridge
            r -= 0.012f * Bump(d, new Vector3(0.36f, 0.12f, 0.92f), 0.14f, 0.11f, 0.4f);               // eye sockets
            r -= 0.012f * Bump(d, new Vector3(-0.36f, 0.12f, 0.92f), 0.14f, 0.11f, 0.4f);
            r += 0.026f * Bump(d, new Vector3(0f, -0.07f, 1f), 0.1f, 0.19f, 0.6f) * front;             // nose bridge and body
            r += 0.011f * Bump(d, new Vector3(0f, -0.2f, 1f), 0.1f, 0.07f, 0.6f) * front;             // nose tip
            r += 0.004f * Bump(d, new Vector3(0.1f, -0.22f, 0.98f), 0.06f, 0.045f, 0.6f);               // nostrils
            r += 0.004f * Bump(d, new Vector3(-0.1f, -0.22f, 0.98f), 0.06f, 0.045f, 0.6f);
            r += 0.009f * Bump(d, new Vector3(0f, -0.43f, 0.9f), 0.2f, 0.08f, 0.4f);                   // lips
            r -= 0.005f * Bump(d, new Vector3(0f, -0.56f, 0.83f), 0.16f, 0.05f, 0.4f);                 // under the lip
            r += 0.016f * Bump(d, new Vector3(0f, -0.72f, 0.68f), 0.2f, 0.12f, 0.3f);                  // chin
            r += 0.007f * Bump(d, new Vector3(0.6f, -0.1f, 0.78f), 0.2f, 0.16f, 0.4f);                 // cheekbones
            r += 0.007f * Bump(d, new Vector3(-0.6f, -0.1f, 0.78f), 0.2f, 0.16f, 0.4f);
            r += 0.008f * Bump(d, new Vector3(0.74f, -0.56f, 0.35f), 0.2f, 0.15f, 0.3f);               // jaw angles
            r += 0.008f * Bump(d, new Vector3(-0.74f, -0.56f, 0.35f), 0.2f, 0.15f, 0.3f);
            return r;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// A closed shape round the Y axis from y = -1 to 1. radii(t, angle) gives the half-width (x) and half-depth (z)
        /// of the cross-section at height t (0..1); capLow / capHigh are how tall the rounded ends are; squareness > 2
        /// makes the section a rounded rectangle (superellipse); frontPush moves the front (+Z) of the upper part out.
        /// </summary>
        static MeshData Lathe(System.Func<float, float, Vector2> radii, float capLow, float capHigh, float squareness = 2f, float frontPush = 0f)
        {
            var pos = new Vector3[Rings + 1, Segments + 1];
            for (int i = 0; i <= Rings; i++)
            {
                // Rings bunch up towards the ends so the caps stay round.
                float s = (float)i / Rings;
                float t = 0.5f - 0.5f * Mathf.Cos(s * Mathf.PI);
                float y = -1f + 2f * t;
                float capScale = 1f;
                if (y < -1f + capLow) { float u = (-1f + capLow - y) / capLow; capScale = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); }
                else if (y > 1f - capHigh) { float u = (y - (1f - capHigh)) / capHigh; capScale = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); }
                for (int j = 0; j <= Segments; j++)
                {
                    float a = (j % Segments) * Mathf.PI * 2f / Segments;
                    Vector2 r = radii(t, a) * capScale;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float e = 2f / squareness;
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * r.x;
                    float z = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e) * r.y;
                    if (z > 0f) z += frontPush * Mathf.Exp(-Mathf.Pow((t - 0.66f) / 0.2f, 2f)) * sn * capScale;
                    pos[i, j] = new Vector3(x, y, z);
                }
            }

            var d = new MeshData();
            for (int i = 0; i <= Rings; i++)
                for (int j = 0; j <= Segments; j++)
                {
                    int jn = (j + 1) % Segments, jp = (j - 1 + Segments) % Segments;
                    Vector3 du = pos[i, jn] - pos[i, jp];
                    Vector3 dv = pos[Mathf.Min(Rings, i + 1), j % Segments] - pos[Mathf.Max(0, i - 1), j % Segments];
                    Vector3 n = Vector3.Cross(du, dv);
                    // Unity is left-handed: (around, up) crossed gives inward, so flip it.
                    n = -n;
                    if (i == 0) n = Vector3.down;
                    else if (i == Rings) n = Vector3.up;
                    else if (n.sqrMagnitude < 1e-10f) n = new Vector3(pos[i, j].x, 0f, pos[i, j].z);
                    n.Normalize();
                    Vector3 tan = du.sqrMagnitude > 1e-10f ? du.normalized : new Vector3(-Mathf.Sin(j * Mathf.PI * 2f / Segments), 0f, Mathf.Cos(j * Mathf.PI * 2f / Segments));
                    d.Add(pos[i, j], n, new Vector4(tan.x, tan.y, tan.z, 1f), new Vector2((float)j / Segments, (float)i / Rings));
                }
            int W = Segments + 1;
            for (int i = 0; i < Rings; i++)
                for (int j = 0; j < Segments; j++)
                {
                    int a = i * W + j, b = a + 1, c = a + W + 1, dd = a + W;
                    d.Tri(a, dd, c);
                    d.Tri(a, c, b);
                }
            return d;
        }
    }
}
