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
