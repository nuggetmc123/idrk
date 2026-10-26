using System.Collections.Generic;
using UnityEngine;
using S = Veylix.MeshKit.Station;

namespace Veylix
{
    /// <summary>
    /// The parts a racing driver is made of, shaped like a person rather than a mannequin: a torso with chest,
    /// shoulders, waist and seat; arms with deltoid, biceps and forearm; legs with thigh and calf; race boots; gloved
    /// hands (wrapped round a rim, or relaxed); and a HANS device. All in one suit atlas painted per driver
    /// (side panels, shoulder straps, belt, zip, collar, chest sponsor and number, arm and leg stripes).
    /// Limbs are built along +Z from their joint; DriverBody poses them.
    /// </summary>
    public static class BodyKit
    {
        // Suit atlas: torso on the left half, sleeves and legs on the right.
        public static readonly Rect TorsoUV = new Rect(0f, 0f, 0.5f, 1f), ArmUV = new Rect(0.5f, 0.5f, 0.25f, 0.5f), LegUV = new Rect(0.75f, 0.5f, 0.25f, 0.5f);
        /// A spot in the atlas that is plain suit colour (back of a leg).
        public static readonly Vector2 PlainUV = new Vector2(0.875f, 0.51f);
        public const float UpperArm = 0.29f, Forearm = 0.27f, Thigh = 0.44f, Shin = 0.43f, SpineLen = 0.56f;

        static Mesh torso, upper, fore, thigh, shin, boot, fist, open, hans, joint;

        // Torso sections: height up the spine (fraction of SpineLen), chest offset, half width, half depth, squareness.
        static readonly float[] TZ = { -0.13f, -0.06f, 0f, 0.12f, 0.3f, 0.48f, 0.68f, 0.83f, 0.92f, 0.98f, 1.06f };
        static readonly float[] TCY = { -0.025f, -0.02f, -0.012f, -0.018f, -0.002f, 0.01f, 0.022f, 0.014f, 0.004f, 0f, 0.004f };
        static readonly float[] THW = { 0.04f, 0.115f, 0.148f, 0.156f, 0.148f, 0.158f, 0.182f, 0.2f, 0.17f, 0.085f, 0.064f };
        static readonly float[] THH = { 0.035f, 0.082f, 0.104f, 0.11f, 0.1f, 0.108f, 0.122f, 0.115f, 0.095f, 0.075f, 0.062f };
        static readonly float[] TP = { 2f, 2.3f, 2.6f, 2.7f, 2.5f, 2.5f, 2.6f, 2.8f, 2.6f, 2.2f, 2.1f };

        /// Torso along +Z from the pelvis to the neck; local +Y is the chest. UVs in TorsoUV (v 0.5 = front centre).
        public static Mesh Torso()
        {
            if (torso != null) return torso;
            var k = new MeshKit();
            var st = new S[TZ.Length];
            for (int i = 0; i < st.Length; i++) st[i] = new S(TZ[i] * SpineLen, TCY[i], THW[i], THH[i], TP[i]);
            k.Loft(0, st, 28, 30, TorsoUV, 0, 0);
            return torso = k.Build("torso");
        }

        /// Height up the spine (fraction) at a torso texture u (the loft spaces u evenly per section), and back.
        static float TorsoF(float u)
        {
            float x = Mathf.Clamp01(u) * (TZ.Length - 1);
            int i = Mathf.Min(TZ.Length - 2, (int)x);
            return Mathf.Lerp(TZ[i], TZ[i + 1], x - i);
        }

        static float TorsoU(float f)
        {
            int i = 0;
            while (i < TZ.Length - 2 && f > TZ[i + 1]) i++;
            return (i + Mathf.Clamp01((f - TZ[i]) / (TZ[i + 1] - TZ[i]))) / (TZ.Length - 1);
        }

        /// Height of the torso's front (or back, front = false) surface at a point up the spine (fraction) and across (x).
        public static float TorsoSurface(float f, float x, bool front = true)
        {
            int i = 0;
            while (i < TZ.Length - 2 && f > TZ[i + 1]) i++;
            float t = Mathf.Clamp01((f - TZ[i]) / (TZ[i + 1] - TZ[i]));
            float cy = Mathf.Lerp(TCY[i], TCY[i + 1], t), hw = Mathf.Lerp(THW[i], THW[i + 1], t), hh = Mathf.Lerp(THH[i], THH[i + 1], t), p = Mathf.Lerp(TP[i], TP[i + 1], t);
            float ax = Mathf.Clamp01(Mathf.Abs(x) / hw);
            float y = hh * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(ax, p)), 1f / p);
            return front ? cy + y : cy - y;
        }

        /// A flat strap through centre points, 'width' across along 'across', double-sided.
        public static void Strap(MeshKit k, int slot, Vector3[] pts, Vector3 across, float width)
        {
            Vector3 w = across.normalized * width * 0.5f;
            for (int i = 0; i < pts.Length - 1; i++)
                k.QuadFace(slot, pts[i] - w, pts[i] + w, pts[i + 1] + w, pts[i + 1] - w, true);
        }

        static Mesh Limb(Rect uv, float len, float[] at, float[] rx, float[] ry, float[] fwd, string name)
        {
            var k = new MeshKit();
            var st = new S[at.Length];
            for (int i = 0; i < at.Length; i++) st[i] = new S(at[i] * len, fwd[i], rx[i], ry[i], 2.2f);
            k.Loft(0, st, 18, 16, uv, 0, 0);
            return k.Build(name);
        }

        public static Mesh UpperArmMesh() => upper != null ? upper : upper = Limb(ArmUV, UpperArm,
            new[] { 0f, 0.1f, 0.3f, 0.55f, 0.85f, 1f }, new[] { 0.056f, 0.062f, 0.055f, 0.05f, 0.044f, 0.042f }, new[] { 0.056f, 0.06f, 0.05f, 0.052f, 0.043f, 0.041f }, new[] { 0f, 0.004f, 0f, 0.006f, 0f, 0f }, "upper arm");

        public static Mesh ForearmMesh() => fore != null ? fore : fore = Limb(ArmUV, Forearm,
            new[] { 0f, 0.2f, 0.5f, 0.85f, 1f }, new[] { 0.042f, 0.047f, 0.04f, 0.032f, 0.031f }, new[] { 0.041f, 0.044f, 0.036f, 0.027f, 0.026f }, new[] { 0f, 0.003f, 0.002f, 0f, 0f }, "forearm");

        public static Mesh ThighMesh() => thigh != null ? thigh : thigh = Limb(LegUV, Thigh,
            new[] { 0f, 0.15f, 0.5f, 0.85f, 1f }, new[] { 0.082f, 0.08f, 0.068f, 0.056f, 0.053f }, new[] { 0.085f, 0.082f, 0.07f, 0.058f, 0.054f }, new[] { 0f, 0.005f, 0.006f, 0.004f, 0f }, "thigh");

        public static Mesh ShinMesh() => shin != null ? shin : shin = Limb(LegUV, Shin,
            new[] { 0f, 0.12f, 0.32f, 0.7f, 0.92f, 1f }, new[] { 0.052f, 0.054f, 0.055f, 0.042f, 0.036f, 0.036f }, new[] { 0.052f, 0.056f, 0.06f, 0.044f, 0.037f, 0.036f }, new[] { 0f, -0.006f, -0.012f, -0.004f, 0f, 0f }, "shin");

        /// A rounded joint (shoulder, elbow, knee) of radius 0.5, scaled per use.
        public static Mesh Joint()
        {
            if (joint != null) return joint;
            var k = new MeshKit();
            var prof = new Vector2[11];
            for (int i = 0; i < 11; i++) { float a = -Mathf.PI / 2f + i / 10f * Mathf.PI; prof[i] = new Vector2(Mathf.Sin(a) * 0.5f, Mathf.Max(0.001f, Mathf.Cos(a) * 0.5f)); }
            for (int i = 0; i < 11; i++) prof[i] = new Vector2(prof[i].y, prof[i].x); // (radius, axial)
            k.Lathe(0, prof, 16);
            joint = k.Build("joint");
            var uv = new List<Vector2>(joint.vertexCount);
            for (int i = 0; i < joint.vertexCount; i++) uv.Add(PlainUV); // plain suit colour
            joint.SetUVs(0, uv);
            return joint;
        }

        /// Race boot: heel at the origin, toe along +Z, sole down (-Y), ankle rising up +Y (the shin meets it 0.08 up).
        /// Slot 0 upper, slot 1 sole and heel tab.
        public static Mesh Boot()
        {
            if (boot != null) return boot;
            var k = new MeshKit();
            k.Loft(0, new[]
            {
                new S(-0.05f, 0.012f, 0.038f, 0.045f, 2.4f, 0f, 0.7f),
                new S(0.0f, 0.016f, 0.044f, 0.05f, 2.6f, 0f, 0.8f),
                new S(0.08f, 0.0f, 0.046f, 0.036f, 2.8f, 0f, 0.85f),
                new S(0.16f, -0.012f, 0.041f, 0.026f, 2.6f, 0f, 0.85f),
                new S(0.205f, -0.018f, 0.026f, 0.018f, 2.2f, 0f, 0.8f),
                new S(0.215f, -0.022f, 0.012f, 0.01f, 2f, 0f, 0.6f),
            }, 18, 14, null, 0, 0);
            // Ankle: a padded cuff rising out of the shoe.
            k.Tube(0, new[] { new Vector3(0f, 0.0f, -0.008f), new Vector3(0f, 0.06f, -0.01f), new Vector3(0f, 0.12f, -0.012f) }, 0.044f, 14, true, 0.04f);
            // Thin rubber sole, and a pull tab at the heel.
            k.Box(1, new Vector3(0f, -0.038f, 0.08f), new Vector3(0.082f, 0.01f, 0.25f), 1f, 0.004f);
            k.Box(1, new Vector3(0f, 0.09f, -0.055f), new Vector3(0.022f, 0.05f, 0.006f), 1f, 0.002f);
            return boot = k.Build("boot", 2);
        }

        /// Gloved left hand: wrist at the origin, fingers along +Z, back of the hand +Y, thumb on +X (mirror it in X for
        /// a right hand). curl 0 = relaxed, 1 = wrapped round a rim. Slot 0 glove, slot 1 cuff strap and knuckle pad.
        public static Mesh Hand(float curl)
        {
            if (curl > 0.5f && fist != null) return fist;
            if (curl <= 0.5f && open != null) return open;
            var k = new MeshKit();
            AddPalm(k);
            // Fingers: three segments each, curling towards the palm (-Y).
            float bend = Mathf.Lerp(10f, 80f, curl);
            for (int f = 0; f < 4; f++)
            {
                var pts = new Vector3[4];
                Vector3 p = new Vector3(FingerX[f], -0.001f, FingerZ);
                Vector3 dir = Quaternion.AngleAxis((f - 1.5f) * -3f, Vector3.up) * Vector3.forward; // a slight fan
                pts[0] = p;
                for (int s = 1; s < 4; s++)
                {
                    dir = Quaternion.AngleAxis(bend, Vector3.right) * dir;
                    p += dir * FingerLen[f] * FingerSeg[s - 1];
                    pts[s] = p;
                }
                k.Tube(0, pts, f == 3 ? 0.0094f : 0.0105f, 10, true, f == 3 ? 0.0078f : 0.0085f);
            }
            // Thumb: a fleshy base off the side of the palm, then two segments reaching forward (relaxed) or over the
            // fingers (gripping).
            {
                Vector3 t0 = new Vector3(0.03f, -0.01f, 0.018f);
                Vector3 t1 = t0 + new Vector3(0.45f, -0.4f, 0.8f).normalized * 0.042f;
                Vector3 d2 = Vector3.Slerp(new Vector3(0.15f, -0.35f, 0.92f), new Vector3(-0.72f, -0.55f, 0.42f), curl).normalized;
                Vector3 t2 = t1 + d2 * 0.032f;
                Vector3 t3 = t2 + Vector3.Slerp(d2, new Vector3(-0.9f, -0.2f, 0.2f).normalized, curl * 0.6f) * 0.026f;
                k.Tube(0, new[] { t0, Vector3.Lerp(t0, t1, 0.5f), t1 }, 0.017f, 10, true, 0.0125f);
                k.Tube(0, new[] { t1, t2, t3 }, 0.0118f, 10, true, 0.0092f);
            }
            var m = k.Build(curl > 0.5f ? "fist" : "hand", 2);
            if (curl > 0.5f) fist = m; else open = m;
            return m;
        }

        static Mesh palm;

        /// Cuff, palm and knuckle pad without fingers (the VR gloves add animated fingers). Same layout as Hand().
        public static Mesh GlovePalm()
        {
            if (palm != null) return palm;
            var k = new MeshKit();
            AddPalm(k);
            return palm = k.Build("glove palm", 2);
        }

        static void AddPalm(MeshKit k)
        {
            // Gauntlet cuff, flaring over the sleeve, with a strap across it.
            k.Loft(0, new[] { new S(-0.065f, 0f, 0.043f, 0.037f, 2.3f), new S(-0.03f, 0f, 0.038f, 0.031f, 2.3f), new S(0.005f, 0f, 0.034f, 0.024f, 2.4f) }, 18, 4, null, -1, -1);
            k.Loft(1, new[] { new S(-0.042f, 0f, 0.0405f, 0.0345f, 2.3f), new S(-0.02f, 0f, 0.0385f, 0.0315f, 2.3f) }, 18, 2, null, -1, -1);
            k.Loft(0, new[]
            {
                new S(0.0f, 0f, 0.033f, 0.022f, 2.4f),
                new S(0.03f, 0.002f, 0.042f, 0.024f, 2.7f),
                new S(0.07f, 0.002f, 0.046f, 0.021f, 2.9f),
                new S(0.095f, 0f, 0.045f, 0.018f, 2.8f),
                new S(0.106f, -0.002f, 0.04f, 0.013f, 2.4f),
            }, 18, 10, null, 0, 0);
            k.Box(1, new Vector3(0f, 0.017f, 0.084f), new Vector3(0.074f, 0.006f, 0.02f), 1f, 0.0028f); // knuckle pad
        }

        /// Finger roots and lengths for the hand above (index first; left hand, thumb side +X).
        public static readonly float[] FingerX = { 0.03f, 0.01f, -0.01f, -0.029f }, FingerLen = { 0.075f, 0.082f, 0.078f, 0.062f }, FingerSeg = { 0.45f, 0.3f, 0.25f };
        public const float FingerZ = 0.09f;

        /// HANS device: a carbon yoke lying on the shoulders behind the neck with the two front legs on the chest.
        /// Origin at the base of the neck, +Y chest, +Z up the spine.
        public static Mesh Hans()
        {
            if (hans != null) return hans;
            var k = new MeshKit();
            // Collar round the back of the neck.
            var back = new Vector3[11];
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(-110f, 110f, i / 10f) * Mathf.Deg2Rad;
                back[i] = new Vector3(Mathf.Sin(a) * 0.1f, -Mathf.Cos(a) * 0.085f, 0.015f + Mathf.Cos(a) * 0.035f);
            }
            k.Tube(0, back, 0.022f, 10, true);
            // Front legs down the chest.
            for (int s = -1; s <= 1; s += 2)
                k.Tube(0, new[] { new Vector3(s * 0.095f, 0.03f, 0.0f), new Vector3(s * 0.085f, 0.11f, -0.07f), new Vector3(s * 0.07f, 0.125f, -0.17f) }, 0.016f, 8, true, 0.013f);
            // Tether posts behind the helmet.
            for (int s = -1; s <= 1; s += 2)
                k.Box(0, new Vector3(s * 0.05f, -0.08f, 0.06f), new Vector3(0.02f, 0.025f, 0.03f), 1f, 0.004f);
            return hans = k.Build("hans");
        }

        // ------------------------------------------------------------------ suit painting

        /// <summary>
        /// The suit, painted: base colour, accent side panels running up into the armpits, shoulder straps
        /// (epaulettes), a belt, a front zip, a collar, a chest sponsor and the race number, stripes down the sleeves
        /// and legs, cuffs, stitched seams and a little fabric grain.
        /// </summary>
        public static Texture2D SuitTexture(Color suit, Color accent, int number, string sponsor)
        {
            const int N = 512;
            var px = new Color32[N * N];
            Color seam = suit * 0.72f; seam.a = 1f;
            Color belt = Color.Lerp(suit, Color.black, 0.45f);
            Color zip = new Color(0.12f, 0.12f, 0.13f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fu = x / (float)N, fv = y / (float)N;
                    Color c = suit;
                    if (fu < 0.5f)
                    {
                        // Torso: u = 0 hips .. 1 neck; v = 0 back, 0.25 one side, 0.5 front, 0.75 other side.
                        float u = TorsoF(fu / 0.5f), v = fv; // u here: height up the spine
                        float side = Mathf.Min(Mathf.Abs(v - 0.25f), Mathf.Abs(v - 0.75f));
                        float panel = Mathf.Lerp(0.05f, 0.075f, Mathf.InverseLerp(0.58f, 0.9f, u)); // widens towards the armpit
                        if (side < panel && u < 0.91f && u > 0.02f) c = accent;
                        else if (Mathf.Abs(side - panel) < 0.004f && u < 0.91f && u > 0.02f) c = seam;
                        if (u > 0.26f && u < 0.33f) c = belt;
                        if (u > 0.9f && u < 0.985f && side < 0.06f) c = accent; // shoulder straps
                        if (u > 0.985f) c = accent;                         // collar
                        if (Mathf.Abs(v - 0.5f) < 0.0025f && u > 0.33f && u < 0.985f) c = zip;
                        if (Mathf.Abs(v) < 0.003f || Mathf.Abs(v - 1f) < 0.003f) c = seam; // back seam
                    }
                    else if (fv >= 0.5f)
                    {
                        // Sleeves (x 0.5..0.75) and legs (x 0.75..1): u along the limb, v around.
                        bool leg = fu >= 0.75f;
                        float u = (fu - (leg ? 0.75f : 0.5f)) / 0.25f, v = (fv - 0.5f) / 0.5f;
                        float side = Mathf.Min(Mathf.Abs(v - 0.25f), Mathf.Abs(v - 0.75f));
                        if (side < (leg ? 0.035f : 0.045f)) c = accent;
                        if (!leg && u > 0.9f) c = accent;     // cuffs (under the gloves)
                        if (leg && u > 0.95f) c = belt;       // ankle cuffs
                    }
                    float n = Mathf.PerlinNoise(fu * 160f, fv * 160f) * 0.05f - 0.025f;
                    px[y * N + x] = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), 1f);
                }
            Color ink = Lum(suit) > 0.5f ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.96f, 0.96f, 0.95f);
            // Text runs around the body (along v), so draw it upright in a scratch buffer and copy it in sideways.
            Stamp(px, N, TorsoU(0.64f) * 0.5f, 0.5f, 0.028f, string.IsNullOrEmpty(sponsor) ? "VEYLIX" : sponsor, ink);
            Stamp(px, N, TorsoU(0.8f) * 0.5f, 0.42f, 0.022f, number.ToString(), Lum(accent) > 0.5f ? new Color(0.08f, 0.08f, 0.09f) : accent);
            Stamp(px, N, TorsoU(0.62f) * 0.5f, 0f, 0.05f, number.ToString(), ink); // big number across the back
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Suit", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        /// Text centred at atlas (x = along, y = around), letters standing along the body's axis.
        static void Stamp(Color32[] px, int N, float cx, float cy, float height, string text, Color ink)
        {
            int w = Mathf.Max(8, Mathf.RoundToInt(N * height * (text.Length * 0.75f + 0.6f))), h = Mathf.Max(8, Mathf.RoundToInt(N * height * 1.4f));
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(0, 0, 0, 0);
            LiveryPainter.DrawText(buf, w, h, w * 0.5f, h * 0.5f, N * height, text, ink);
            // Rotate: the text's horizontal goes around the body (atlas y), its vertical up the body (atlas x).
            int x0 = Mathf.RoundToInt(cx * N) - h / 2, y0 = Mathf.RoundToInt(cy * N) - w / 2;
            for (int ty = 0; ty < h; ty++)
                for (int tx = 0; tx < w; tx++)
                {
                    var s = buf[ty * w + tx];
                    if (s.a < 8) continue;
                    int ax = x0 + ty, ay = y0 + (w - 1 - tx); // mirrored so it reads from outside
                    if (ay < 0) ay += N; if (ay >= N) ay -= N;
                    if (ax < 0 || ax >= N) continue;
                    float a = s.a / 255f;
                    Color d = px[ay * N + ax];
                    px[ay * N + ax] = Color.Lerp(d, new Color(s.r / 255f, s.g / 255f, s.b / 255f, 1f), a);
                }
        }

        // ------------------------------------------------------------------ combining

        public struct Piece { public Mesh mesh; public Matrix4x4 m; public int slot; public Piece(Mesh mesh, Matrix4x4 m, int slot) { this.mesh = mesh; this.m = m; this.slot = slot; } }

        /// Bakes posed pieces into one mesh (one sub-mesh per material slot; a piece's own sub-meshes go to slot+i).
        public static Mesh Combine(string name, List<Piece> pieces, int slots)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var tris = new List<int>[slots];
            for (int i = 0; i < slots; i++) tris[i] = new List<int>();
            foreach (var p in pieces)
            {
                int b = v.Count;
                var pv = p.mesh.vertices; var pn = p.mesh.normals; var pu = new List<Vector2>(); p.mesh.GetUVs(0, pu);
                for (int i = 0; i < pv.Length; i++)
                {
                    v.Add(p.m.MultiplyPoint3x4(pv[i]));
                    n.Add(i < pn.Length ? p.m.MultiplyVector(pn[i]).normalized : Vector3.up);
                    uv.Add(i < pu.Count ? pu[i] : Vector2.zero);
                }
                bool mirrored = p.m.determinant < 0f; // a mirrored piece (a right hand) needs its winding reversed
                for (int s = 0; s < p.mesh.subMeshCount; s++)
                {
                    int slot = Mathf.Min(slots - 1, p.slot + s);
                    var src = p.mesh.GetTriangles(s);
                    for (int i = 0; i < src.Length; i += 3)
                    {
                        tris[slot].Add(b + src[i]);
                        tris[slot].Add(b + (mirrored ? src[i + 2] : src[i + 1]));
                        tris[slot].Add(b + (mirrored ? src[i + 1] : src[i + 2]));
                    }
                }
            }
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = slots;
            for (int i = 0; i < slots; i++) m.SetTriangles(tris[i], i);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// Matrix placing a limb (built along +Z, length len0) between joints a and b, bending towards 'up'.
        public static Matrix4x4 Between(Vector3 a, Vector3 b, float len0, Vector3 up)
        {
            Vector3 d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            var rot = Quaternion.LookRotation(d / len, up);
            return Matrix4x4.TRS(a, rot, new Vector3(1f, 1f, len / len0));
        }
    }
}
