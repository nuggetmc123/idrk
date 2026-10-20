using UnityEngine;

namespace Nullbound.Art
{
    /// <summary>
    /// The game's library of generated PBR surfaces (the "NULLBOUND Combat Test Facility" look plus the outdoor field
    /// test site). Every method returns a TILEABLE texture set (albedo, normal, mask = metal/AO/smoothness, sometimes
    /// emission) that covers TexSet.metres metres, so a material's tiling is 1 / metres on meshes with metre UVs.
    /// Heights inside the recipes are in metres, so bumps are physically sized (relief exaggerates them a little).
    /// Pure maths like ProcTex: the editor writes these as texture assets, and previews run outside Unity.
    /// </summary>
    public static class Surfaces
    {
        /// <summary>Bump when a recipe changes (lets tools tell an old texture from a new one).</summary>
        public const int Version = 2;

        /// <summary>Every id Get() understands.</summary>
        public static readonly string[] Ids =
        {
            "LabPanel", "LabPanelDark", "LabFloor", "ConcretePolished", "ConcreteRaw", "Gunmetal", "BrushedSteel",
            "SafetyOrangePaint", "HazardStripe", "RubberMat", "SteelGrate", "LightPanel", "Screen", "Glass",
            "Grass", "Dirt", "Gravel", "Rock", "Cliff", "Plaster", "Brick", "StoneBlocks", "WoodPlanks", "Timber",
            "RoofTiles", "Bark", "PineNeedles", "Asphalt", "Crate",
        };

        // The art-direction palette (sRGB).
        public static readonly Color LabWhite = ProcTex.Hex("D9D6CF"), PanelGrey = ProcTex.Hex("B8B5AE"), ConcreteGrey = ProcTex.Hex("8E8A84"),
            GunmetalGrey = ProcTex.Hex("2B2D31"), Charcoal = ProcTex.Hex("1A1B1E"), SafetyOrange = ProcTex.Hex("FF6A1A"),
            HazardYellow = ProcTex.Hex("F2C230"), Teal = ProcTex.Hex("3FD0C9"), Danger = ProcTex.Hex("E5402A");

        // Plaster and roof colours for the field test site (the tint parameters of Plaster() and RoofTiles()).
        public static readonly Color PlasterCream = new Color(0.86f, 0.8f, 0.68f), PlasterSand = new Color(0.78f, 0.69f, 0.56f),
            PlasterBlue = new Color(0.66f, 0.73f, 0.76f), RoofTerracotta = new Color(0.66f, 0.3f, 0.2f),
            RoofSlate = new Color(0.33f, 0.36f, 0.4f), RoofBrown = new Color(0.45f, 0.3f, 0.2f);

        /// <summary>Looks a surface up by name (case and spaces/underscores don't matter). Unknown names give LabPanel.</summary>
        public static TexSet Get(string id, int size = 512)
        {
            string k = (id ?? "").Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
            switch (k)
            {
                case "labpanel": return LabPanel(size);
                case "labpaneldark": return LabPanelDark(size);
                case "labfloor": return LabFloor(size);
                case "concretepolished": return ConcretePolished(size);
                case "concreteraw": case "concrete": return ConcreteRaw(size);
                case "gunmetal": return Gunmetal(size);
                case "brushedsteel": case "steel": return BrushedSteel(size);
                case "safetyorangepaint": case "orangepaint": case "safetyorange": return SafetyOrangePaint(size);
                case "hazardstripe": case "hazard": return HazardStripe(size);
                case "rubbermat": case "rubber": return RubberMat(size);
                case "steelgrate": case "grate": case "treadplate": return SteelGrate(size);
                case "lightpanel": case "light": return LightPanel(size);
                case "screen": return Screen(size);
                case "glass": return Glass(size);
                case "grass": return Grass(size);
                case "dirt": return Dirt(size);
                case "gravel": return Gravel(size);
                case "rock": return Rock(size);
                case "cliff": return Cliff(size);
                case "plaster": return Plaster(PlasterCream, size);
                case "brick": return Brick(size);
                case "stoneblocks": case "stone": return StoneBlocks(size);
                case "woodplanks": case "planks": case "wood": return WoodPlanks(size);
                case "timber": return Timber(size);
                case "rooftiles": case "roof": return RoofTiles(RoofTerracotta, size);
                case "bark": return Bark(size);
                case "pineneedles": case "pine": case "needles": return PineNeedles(size);
                case "asphalt": case "road": return Asphalt(size);
                case "crate": return Crate(size);
                default: return LabPanel(size);
            }
        }

        // ================================================================== shared bits

        static TexSet Make(string name, int size, float metres, float relief, ProcTex.Sample sample, System.Func<float, float, Color> emission = null)
            => ProcTex.Build(name, size, metres, 256f / metres * relief, sample, emission);

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static string Tag(Color c) =>
            ((int)(c.r * 255f + 0.5f)).ToString("X2") + ((int)(c.g * 255f + 0.5f)).ToString("X2") + ((int)(c.b * 255f + 0.5f)).ToString("X2");

        /// <summary>
        /// Chipped paint, worked out once for two depths: outer = where the top coat is gone (primer shows), inner =
        /// where it's worn through to bare metal. Warped noise, so chips come in small irregular clusters.
        /// amount 0..1 = how worn (0.2 = a few chips, 0.6 = well used).
        /// </summary>
        static void Chips(float u, float v, int cells, float amount, int seed, out float outer, out float inner)
        {
            // Chips only happen in a few worn areas (where knocks land), not evenly all over.
            float cluster = ProcTex.Smooth(0.5f, 0.7f, ProcTex.Fbm(u, v, 3, 3, seed + 1));
            outer = inner = 0f;
            if (cluster <= 0f) return;
            float wu = u, wv = v;
            ProcTex.Warp(ref wu, ref wv, cells, 0.6f / cells, seed);
            float n = ProcTex.Fbm(wu, wv, cells, 3, seed + 2);
            float t = 0.8f - amount * 0.2f + (1f - cluster) * 0.25f;
            outer = ProcTex.Smooth(t, t + 0.02f, n);
            inner = ProcTex.Smooth(t + 0.035f, t + 0.05f, n);
        }

        /// <summary>
        /// Scratch coverage 0..1: short straight scratches scattered over the texture (cells across, perCell in each).
        /// Lengths and widths are in cells; px = soft edge in cells. Each pattern's scratches are worked out once.
        /// </summary>
        static float Scratches(float u, float v, int cells, int perCell, float lengthCells, float widthCells, float px, int seed)
        {
            var key = (cells, perCell, lengthCells, seed);
            if (!scratchCache.TryGetValue(key, out var seg))
            {
                seg = new float[cells * cells * perCell * 5];
                for (int cy = 0; cy < cells; cy++)
                    for (int cx = 0; cx < cells; cx++)
                        for (int k = 0; k < perCell; k++)
                        {
                            int s = seed + k * 31, i = ((cy * cells + cx) * perCell + k) * 5;
                            float a = ProcTex.Hash01(cx, cy, s + 2) * Mathf.PI * 2f, len = lengthCells * (0.4f + ProcTex.Hash01(cx, cy, s + 3));
                            seg[i] = ProcTex.Hash01(cx, cy, s);
                            seg[i + 1] = ProcTex.Hash01(cx, cy, s + 1);
                            seg[i + 2] = Mathf.Cos(a) * len;
                            seg[i + 3] = Mathf.Sin(a) * len;
                            seg[i + 4] = ProcTex.Hash01(cx, cy, s + 4);
                        }
                scratchCache[key] = seg;
            }
            float x = u * cells, y = v * cells;
            int gx0 = Mathf.FloorToInt(x), gy0 = Mathf.FloorToInt(y);
            float best = 0f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = gx0 + ox, gy = gy0 + oy, cell = ProcTex.Wrap(gy, cells) * cells + ProcTex.Wrap(gx, cells);
                    for (int k = 0; k < perCell; k++)
                    {
                        int i = (cell * perCell + k) * 5;
                        float ax = gx + seg[i], ay = gy + seg[i + 1];
                        float bx = ax + seg[i + 2], by = ay + seg[i + 3];
                        // Quick reject: the point is nowhere near this scratch.
                        if (x < Mathf.Min(ax, bx) - 0.1f || x > Mathf.Max(ax, bx) + 0.1f || y < Mathf.Min(ay, by) - 0.1f || y > Mathf.Max(ay, by) + 0.1f) continue;
                        float d = ProcTex.Segment(x, y, ax, ay, bx, by, out float t);
                        float w = widthCells * (1f - Mathf.Abs(t - 0.5f) * 1.2f);
                        float cov = ProcTex.Fill(d - w, px) * seg[i + 4];
                        if (cov > best) best = cov;
                    }
                }
            return best;
        }

        static readonly System.Collections.Generic.Dictionary<(int, int, float, int), float[]> scratchCache =
            new System.Collections.Generic.Dictionary<(int, int, float, int), float[]>();

        // ================================================================== lab

        /// <summary>Off-white composite wall panels, 1 x 2 m: recessed seams, countersunk bolts, subtle per-panel variation.</summary>
        public static TexSet LabPanel(int size = 512) => Panels("LabPanel", size, LabWhite, false);

        /// <summary>Gunmetal steel wall panels, 1 x 2 m, painted, with worn edges.</summary>
        public static TexSet LabPanelDark(int size = 512) => Panels("LabPanelDark", size, C(0.2f, 0.21f, 0.23f), true);

        static TexSet Panels(string name, int size, Color paint, bool dark)
        {
            const float M = 4f; // 4 columns x 2 rows of 1 x 2 m panels
            float px = M / size;
            int seed = ProcTex.Seed(name);
            bool labels = px < 0.0065f;
            string[] codes = { "A-04", "C-11", "B-07", "D-02", "A-15", "E-09", "C-03", "B-12" };
            Color bolt = C(0.56f, 0.57f, 0.58f);
            return Make(name, size, M, 1.4f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                int cx = Mathf.Min(3, (int)x), cy = Mathf.Min(1, (int)(y * 0.5f));
                float lx = x - cx, ly = y - cy * 2f;
                float ex = Mathf.Min(lx, 1f - lx), ey = Mathf.Min(ly, 2f - ly);
                float d = Mathf.Min(ex, ey); // metres to the nearest seam
                float r1 = ProcTex.Hash01(cx, cy, seed), r2 = ProcTex.Hash01(cx, cy, seed + 7), r3 = ProcTex.Hash01(cx, cy, seed + 13);

                // Relief: a recessed seam with rounded shoulders, the face raised a touch inside a flange, countersunk bolts.
                float seam = 1f - ProcTex.Smooth(0.003f, 0.0065f, d);
                float shoulder = 1f - ProcTex.Smooth(0.003f, 0.016f, d);
                float face = ProcTex.Smooth(0.052f, 0.06f, d);
                h = -seam * 0.004f - shoulder * shoulder * 0.0012f + face * 0.0009f;

                float bx = ex - 0.03f;
                float by = Mathf.Min(Mathf.Abs(ly - 0.03f), Mathf.Min(Mathf.Abs(ly - 1f), Mathf.Abs(ly - 1.97f)));
                float db = Mathf.Sqrt(bx * bx + by * by);
                float head = ProcTex.Fill(db - 0.0072f, px * 0.6f);
                float sink = ProcTex.Fill(db - 0.0115f, px * 0.6f) * (1f - head);
                h += -sink * 0.0012f + head * 0.0005f * (1f - ProcTex.Pow2(db / 0.0072f));

                float mott = ProcTex.Fbm(u, v, 3, 5, seed + 1);
                float smudge = ProcTex.Fbm(u, v, 6, 4, seed + 2);
                float grimeN = ProcTex.Fbm(u, v, 20, 3, seed + 3);
                Color col = ProcTex.Mul(paint, 1f + (r1 - 0.5f) * (dark ? 0.14f : 0.05f) + (mott - 0.5f) * (dark ? 0.12f : 0.06f));
                col = ProcTex.Warm(col, (r2 - 0.5f) * 0.025f);

                // Grime: settles along the seams and at the bottom of each panel, broken up by noise.
                float grime = (1f - ProcTex.Smooth(0f, 0.03f + grimeN * 0.05f, d)) * 0.55f
                              + (1f - ProcTex.Smooth(0f, 0.3f, ly)) * ProcTex.Smooth(0.35f, 0.75f, grimeN) * 0.35f;
                col = Color.Lerp(col, col * C(0.78f, 0.75f, 0.7f), ProcTex.Saturate(grime));
                col = Color.Lerp(col, col * 0.35f, seam);
                s = (dark ? 0.4f : 0.47f) + (r2 - 0.5f) * 0.1f + (smudge - 0.5f) * 0.16f - grime * 0.12f - seam * 0.2f;
                m = 0f;

                if (dark)
                {
                    // Paint worn through to steel along the panel edges and round the bolts.
                    float edgeWear = (1f - ProcTex.Smooth(0.004f, 0.035f, d)) + sink * 0.8f;
                    float wear = 0f;
                    if (edgeWear > 0.01f)
                    {
                        Chips(u, v, 40, ProcTex.Saturate(edgeWear), seed + 9, out float worn, out _);
                        wear = worn * ProcTex.Saturate(edgeWear * 1.6f);
                    }
                    col = Color.Lerp(col, C(0.46f, 0.47f, 0.49f), wear);
                    m = Mathf.Max(m, wear);
                    s = Mathf.Lerp(s, 0.55f, wear);
                    h -= wear * 0.0002f;
                }

                // A small painted panel code on some panels.
                if (labels && r3 < 0.4f)
                {
                    float t = ProcTex.Text(lx - 0.08f, ly - 1.84f, codes[(cx + cy * 4) % codes.Length], 0.055f, px * 0.6f);
                    col = Color.Lerp(col, dark ? C(0.62f, 0.6f, 0.56f) : C(0.33f, 0.33f, 0.34f), t * 0.85f);
                }

                col = Color.Lerp(col, bolt, head);
                m = Mathf.Max(m, head * 0.85f);
                s = Mathf.Lerp(s, 0.5f, head);
                col = Color.Lerp(col, col * 0.55f, sink);
                c = col;
                ao = 1f - seam * 0.55f - sink * 0.4f - shoulder * 0.08f;
            });
        }

        /// <summary>Light polished concrete floor: painted 1 m grid, bolder lines every 4 m, saw-cut slab joints, scuffs and cracks.</summary>
        public static TexSet LabFloor(int size = 512)
        {
            const float M = 4f;
            float px = M / size;
            const int seed = 4101;
            Color concreteBase = C(0.7f, 0.695f, 0.68f), lineGrey = C(0.36f, 0.365f, 0.37f), lineDark = C(0.14f, 0.145f, 0.15f);
            return Make("LabFloor", size, M, 1.5f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                int sx = Mathf.Min(1, (int)(x * 0.5f)), sy = Mathf.Min(1, (int)(y * 0.5f));
                float r1 = ProcTex.Hash01(sx, sy, seed), r2 = ProcTex.Hash01(sx, sy, seed + 3);

                float cloud = ProcTex.Fbm(u, v, 3, 5, seed + 1);
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 4, 0.1f, seed + 2);
                float trowel = ProcTex.Fbm(wu, wv, 10, 3, seed + 4);
                float agg = ProcTex.Worley(u, v, 140, seed + 5, out _, out float aid);
                float stone = ProcTex.Fill(agg - 0.3f, 0.12f);
                float grain = ProcTex.Fbm(u, v, 96, 2, seed + 11);
                Color col = ProcTex.Mul(concreteBase, 1f + (r1 - 0.5f) * 0.07f + (cloud - 0.5f) * 0.14f + (trowel - 0.5f) * 0.05f + (grain - 0.5f) * 0.05f);
                col = ProcTex.Warm(col, (r2 - 0.5f) * 0.03f);
                col = Color.Lerp(col, ProcTex.Mul(col, aid > 0.55f ? 1.07f : 0.9f), stone * 0.55f);

                // Painted lines (worn in places).
                float dThin = Mathf.Min(ProcTex.LineDistance(x, 1f), ProcTex.LineDistance(y, 1f));
                float dBold = Mathf.Min(ProcTex.LineDistance(x, 4f), ProcTex.LineDistance(y, 4f));
                float paintWear = dThin < 0.03f || dBold < 0.04f ? ProcTex.Smooth(0.56f, 0.72f, ProcTex.Fbm(u, v, 40, 3, seed + 6)) : 0f;
                float thin = ProcTex.Fill(dThin - 0.0065f, px * 0.6f) * (1f - paintWear * 0.75f);
                float bold = ProcTex.Fill(dBold - 0.02f, px * 0.6f) * (1f - paintWear * 0.6f);
                col = Color.Lerp(col, lineGrey, thin * 0.9f);
                col = Color.Lerp(col, lineDark, bold * 0.95f);

                // Saw-cut joints between the 2 m slabs, down the middle of the painted lines.
                float dJoint = Mathf.Min(ProcTex.LineDistance(x, 2f), ProcTex.LineDistance(y, 2f));
                float joint = ProcTex.Fill(dJoint - 0.0016f, px * 0.5f);

                // Hairline cracks, only here and there.
                float crackMask = ProcTex.Smooth(0.64f, 0.76f, ProcTex.Fbm(u, v, 3, 2, seed + 9));
                float crack = 0f;
                if (crackMask > 0f)
                {
                    float cu = u, cv = v;
                    ProcTex.Warp(ref cu, ref cv, 6, 0.06f, seed + 7);
                    ProcTex.Worley(cu, cv, 5, seed + 8, out float cedge, out _);
                    crack = ProcTex.Fill(cedge * (M / 5f) * 0.5f - 0.0007f, px * 0.5f) * crackMask;
                }

                // Black rubber scuffs from boots.
                float scuff = Scuffs(x, y, M, px, seed + 10);

                col = Color.Lerp(col, col * 0.62f, crack * 0.6f);
                col = Color.Lerp(col, C(0.18f, 0.17f, 0.17f), scuff * 0.35f);
                col = Color.Lerp(col, C(0.08f, 0.08f, 0.08f), joint);
                c = col;
                h = (cloud - 0.5f) * 0.0008f + (grain - 0.5f) * 0.0002f + stone * 0.00015f + (thin + bold) * 0.00012f - joint * 0.003f - crack * 0.0008f;
                m = 0f;
                s = 0.62f + (r2 - 0.5f) * 0.08f + (cloud - 0.5f) * 0.14f - (thin + bold) * 0.14f - scuff * 0.25f - joint * 0.4f - crack * 0.2f;
                ao = 1f - joint * 0.6f - crack * 0.3f;
            });
        }

        /// <summary>Boot scuffs: short dark streaks (positions in metres; M = texture size in metres).</summary>
        static float Scuffs(float x, float y, float M, float px, int seed)
        {
            const float cell = 0.5f;
            int n = Mathf.RoundToInt(M / cell);
            int cx = Mathf.FloorToInt(x / cell), cy = Mathf.FloorToInt(y / cell);
            float best = 0f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy, wx = ProcTex.Wrap(gx, n), wy = ProcTex.Wrap(gy, n);
                    if (ProcTex.Hash01(wx, wy, seed) > 0.45f) continue;
                    float ax = (gx + ProcTex.Hash01(wx, wy, seed + 1)) * cell, ay = (gy + ProcTex.Hash01(wx, wy, seed + 2)) * cell;
                    float a = ProcTex.Hash01(wx, wy, seed + 3) * Mathf.PI * 2f, len = 0.06f + ProcTex.Hash01(wx, wy, seed + 4) * 0.22f;
                    float wdt = 0.004f + ProcTex.Hash01(wx, wy, seed + 5) * 0.01f;
                    float d = ProcTex.Segment(x, y, ax, ay, ax + Mathf.Cos(a) * len, ay + Mathf.Sin(a) * len, out float t);
                    float streak = ProcTex.Perlin(t * 0.999f, ProcTex.Hash01(wx, wy, seed + 6), 6, 1, seed + 7);
                    float cov = ProcTex.Fill(d - wdt * (1f - Mathf.Abs(t - 0.5f)), px) * (0.35f + 0.65f * ProcTex.Hash01(wx, wy, seed + 8)) * ProcTex.Smooth(0.3f, 0.6f, streak);
                    if (cov > best) best = cov;
                }
            return best;
        }

        /// <summary>Polished mid-grey concrete: cloudy trowelled tone, faint exposed aggregate, the odd pore (no markings).</summary>
        public static TexSet ConcretePolished(int size = 512)
        {
            const float M = 2f;
            float px = M / size;
            const int seed = 5207;
            return Make("ConcretePolished", size, M, 1.3f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float cloud = ProcTex.Fbm(u, v, 3, 5, seed);
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 3, 0.12f, seed + 1);
                float swirl = ProcTex.Fbm(wu, wv, 8, 3, seed + 2);
                // Aggregate ground flat by polishing: soft-edged stones about 1-2 cm, only slightly different in tone.
                float f1 = ProcTex.Worley(u, v, 64, seed + 3, out _, out float id);
                float stone = ProcTex.Fill(f1 - (0.18f + id * 0.12f), 0.16f) * (id < 0.7f ? 1f : 0f);
                float fine = ProcTex.Fbm(u, v, 96, 2, seed + 4);
                float pf = ProcTex.Worley(u, v, 90, seed + 5, out _, out float pid);
                float pore = pid < 0.025f ? ProcTex.Fill(pf * (M / 90f) - 0.0022f, px * 0.7f) : 0f;
                Color baseC = ProcTex.Mul(ConcreteGrey, 1.04f + (cloud - 0.5f) * 0.16f + (swirl - 0.5f) * 0.06f + (fine - 0.5f) * 0.035f);
                Color stoneC = id < 0.3f ? C(0.7f, 0.69f, 0.66f) : id < 0.55f ? C(0.47f, 0.45f, 0.43f) : C(0.62f, 0.57f, 0.5f);
                Color col = Color.Lerp(baseC, Color.Lerp(baseC, stoneC, 0.22f), stone);
                col = Color.Lerp(col, col * 0.55f, pore);
                c = col;
                h = (cloud - 0.5f) * 0.0008f - pore * 0.0015f + (fine - 0.5f) * 0.00015f;
                m = 0f;
                s = 0.58f + (cloud - 0.5f) * 0.16f + (swirl - 0.5f) * 0.1f - pore * 0.3f;
                ao = 1f - pore * 0.4f;
            });
        }

        /// <summary>Raw cast concrete: formwork joints, form-tie holes with rust-free water stains, bug holes, rough grain.</summary>
        public static TexSet ConcreteRaw(int size = 512)
        {
            const float M = 2.4f; // two 1.2 x 2.4 m formwork panels
            float px = M / size;
            const int seed = 6301;
            float[] tieX = { 0.3f, 0.9f, 1.5f, 2.1f }, tieY = { 0.3f, 1.2f, 2.1f };
            return Make("ConcreteRaw", size, M, 1.6f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                float cloud = ProcTex.Fbm(u, v, 3, 5, seed);
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 4, 0.1f, seed + 1);
                float release = ProcTex.Fbm(wu, wv, 6, 3, seed + 2);
                float grain = ProcTex.Fbm(u, v, 96, 3, seed + 3);
                Color col = ProcTex.Mul(C(0.6f, 0.585f, 0.56f), 1f + (cloud - 0.5f) * 0.2f + (release - 0.5f) * 0.1f + (grain - 0.5f) * 0.08f);
                h = (grain - 0.5f) * 0.0012f + (cloud - 0.5f) * 0.002f;

                // Formwork joints: a thin raised fin with a dark line.
                float dj = Mathf.Min(ProcTex.LineDistance(x, 1.2f), ProcTex.LineDistance(y, 2.4f));
                float fin = ProcTex.Fill(dj - 0.0025f, px * 0.6f);
                col = Color.Lerp(col, col * 0.8f, fin);
                h += fin * 0.0012f;

                // Tie holes, each with a faint stain running down from it.
                float hole = 0f, ring = 0f, stain = 0f;
                for (int i = 0; i < tieX.Length; i++)
                    for (int j = 0; j < tieY.Length; j++)
                    {
                        float dx = x - tieX[i], dy = y - tieY[j];
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        hole = Mathf.Max(hole, ProcTex.Fill(d - 0.013f, px * 0.6f));
                        ring = Mathf.Max(ring, ProcTex.Fill(d - 0.022f, px * 0.6f));
                        float below = tieY[j] - y;
                        if (below > 0f && below < 1.2f)
                        {
                            float n = ProcTex.Perlin(u, v, 24, 3, seed + 4);
                            float w = 0.012f + below * 0.02f;
                            stain = Mathf.Max(stain, Mathf.Exp(-dx * dx / (w * w)) * (1f - below / 1.2f) * (0.4f + n * 0.6f));
                        }
                    }
                col = Color.Lerp(col, col * 0.86f, stain * 0.7f);
                col = Color.Lerp(col, col * 0.88f, ring - hole);
                col = Color.Lerp(col, col * 0.3f, hole);
                h += -hole * 0.01f - (ring - hole) * 0.0008f;

                // Bug holes (air bubbles against the formwork).
                float bf = ProcTex.Worley(u, v, 60, seed + 5, out _, out float bid);
                float bug = bid < 0.3f ? ProcTex.Fill(bf * (M / 60f) - (0.0018f + bid * 0.012f), px * 0.6f) : 0f;
                col = Color.Lerp(col, col * 0.45f, bug);
                h -= bug * 0.003f;

                c = col;
                m = 0f;
                s = 0.2f + (release - 0.5f) * 0.1f + stain * 0.05f - bug * 0.1f;
                ao = 1f - hole * 0.7f - bug * 0.5f - (ring - hole) * 0.15f;
            });
        }

        /// <summary>Dark painted steel (gunmetal) with chipped edges showing bare metal, and fine scratches.</summary>
        public static TexSet Gunmetal(int size = 512) => PaintedMetal("Gunmetal", GunmetalGrey, 0.42f, 0.5f, size);

        /// <summary>Safety-orange paint over steel, worn: chips to dark primer and bare metal, scratches, a little grime.</summary>
        public static TexSet SafetyOrangePaint(int size = 512) => PaintedMetal("SafetyOrangePaint", SafetyOrange, 0.45f, 0.65f, size);

        /// <summary>Any paint colour over steel, worn like SafetyOrangePaint (wear 0..1).</summary>
        public static TexSet PaintedMetal(Color paint, float wear = 0.5f, int size = 512) => PaintedMetal("Paint_" + Tag(paint), paint, 0.45f, wear, size);

        static TexSet PaintedMetal(string name, Color paint, float gloss, float wear, int size)
        {
            const float M = 1f;
            float px = M / size;
            int seed = ProcTex.Seed(name);
            Color primer = C(0.25f, 0.25f, 0.26f), steel = C(0.6f, 0.61f, 0.63f);
            return Make(name, size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float var = ProcTex.Fbm(u, v, 3, 4, seed);
                float peel = ProcTex.Fbm(u, v, 48, 2, seed + 1); // orange-peel texture of sprayed paint
                float dirt = ProcTex.Fbm(u, v, 5, 3, seed + 2);
                float handling = ProcTex.Fbm(u, v, 9, 3, seed + 5); // duller and shinier patches from use
                float grime = ProcTex.Smooth(0.55f, 0.85f, dirt);
                Color col = ProcTex.Mul(paint, 1f + (var - 0.5f) * 0.08f + (peel - 0.5f) * 0.025f);
                col = Color.Lerp(col, col * C(0.8f, 0.77f, 0.72f), grime * 0.3f);

                Chips(u, v, 26, wear * 0.55f, seed + 3, out float chipOuter, out float chipInner);
                // Fine scratches (about a millimetre wide): faint lines that mostly show in the shine.
                float scratch = Scratches(u, v, 10, 2, 0.9f, 0.0006f * 10f, px * 10f * 0.7f, seed + 4) * Mathf.Min(1f, wear * 1.4f);
                col = Color.Lerp(col, primer, chipOuter);
                col = Color.Lerp(col, ProcTex.Mul(steel, 0.92f + (peel - 0.5f) * 0.2f), chipInner);
                col = Color.Lerp(col, Color.Lerp(col, steel, 0.45f), scratch * 0.35f);
                c = col;
                m = Mathf.Max(chipInner, scratch * 0.25f);
                s = gloss + (var - 0.5f) * 0.06f + (handling - 0.5f) * 0.2f - grime * 0.1f;
                s = Mathf.Lerp(s, 0.3f, chipOuter - chipInner);
                s = Mathf.Lerp(s, 0.62f, chipInner);
                s += scratch * 0.12f;
                h = (peel - 0.5f) * 0.00008f - chipOuter * 0.00016f - chipInner * 0.00008f - scratch * 0.00002f;
                ao = 1f - (chipOuter - chipInner) * 0.12f;
            });
        }

        /// <summary>Brushed stainless steel: fine directional streaks along u, faint smudges, a few scratches. Metallic.</summary>
        public static TexSet BrushedSteel(int size = 512)
        {
            const float M = 0.5f;
            float px = M / size;
            const int seed = 7103;
            return Make("BrushedSteel", size, M, 1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float streak = ProcTex.Fbm(u, v, 1, 64, 3, seed);
                float fine = ProcTex.Perlin(u, v, 2, 320, seed + 1);
                float smudge = ProcTex.Fbm(u, v, 4, 3, seed + 2);
                float scratch = Scratches(u, v, 5, 2, 1.1f, 0.0004f * 5f / M, px * 5f / M * 0.7f, seed + 3);
                Color col = ProcTex.Mul(C(0.73f, 0.74f, 0.75f), 1f + (streak - 0.5f) * 0.12f + (fine - 0.5f) * 0.08f);
                col = Color.Lerp(col, C(0.84f, 0.85f, 0.86f), scratch * 0.35f);
                c = col;
                m = 1f;
                s = 0.62f + (streak - 0.5f) * 0.2f + (fine - 0.5f) * 0.12f - ProcTex.Smooth(0.55f, 0.85f, smudge) * 0.07f - scratch * 0.1f;
                h = (streak - 0.5f) * 0.00004f + (fine - 0.5f) * 0.00003f - scratch * 0.00002f;
                ao = 1f;
            });
        }

        /// <summary>Hazard stripes: yellow and black at 45 degrees (about 9 cm bands), a few chips and scuffs, a little grime.</summary>
        public static TexSet HazardStripe(int size = 512)
        {
            const float M = 1f;
            float px = M / size;
            const int seed = 8209;
            const int stripes = 4; // stripe pairs along u + v (keeps it tileable)
            float period = M / (stripes * Mathf.Sqrt(2f)); // distance between stripe pairs, measured across them
            Color black = C(0.07f, 0.07f, 0.075f), primer = C(0.3f, 0.3f, 0.31f), steel = C(0.58f, 0.59f, 0.61f);
            return Make("HazardStripe", size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float t = ProcTex.Frac((u + v) * stripes);
                float fromMid = ProcTex.Frac(t + 0.25f) - 0.5f; // -0.5..0.5 from the middle of the yellow band (t = 0.25)
                float dEdge = (Mathf.Abs(fromMid) - 0.25f) * period; // < 0 inside the yellow band
                float yellow = ProcTex.Fill(dEdge, px * 0.6f);
                float var = ProcTex.Fbm(u, v, 4, 3, seed);
                float dirt = ProcTex.Fbm(u, v, 6, 3, seed + 1);
                float grime = ProcTex.Smooth(0.5f, 0.85f, dirt);
                Color col = Color.Lerp(black, ProcTex.Mul(HazardYellow, 1f + (var - 0.5f) * 0.1f), yellow);
                col = Color.Lerp(col, col * C(0.75f, 0.71f, 0.64f), grime * 0.45f);
                Chips(u, v, 24, 0.4f, seed + 2, out float chipOuter, out float chipInner);
                float scuff = Scratches(u, v, 8, 2, 0.8f, 0.0012f * 8f, px * 8f * 0.7f, seed + 3);
                col = Color.Lerp(col, primer, chipOuter);
                col = Color.Lerp(col, steel, chipInner);
                col = Color.Lerp(col, Color.Lerp(col, C(0.22f, 0.21f, 0.2f), yellow), scuff * 0.4f);
                c = col;
                m = chipInner;
                s = 0.45f + (var - 0.5f) * 0.08f - grime * 0.14f - scuff * 0.08f;
                s = Mathf.Lerp(s, 0.55f, chipInner);
                h = -chipOuter * 0.00018f - chipInner * 0.0001f + (yellow - 0.5f) * 0.00004f;
                ao = 1f - (chipOuter - chipInner) * 0.1f;
            });
        }

        /// <summary>Dark rubber floor tiles (50 cm) with a pebbled grip surface, dusty scuffs and slightly shiny wear.</summary>
        public static TexSet RubberMat(int size = 512)
        {
            const float M = 1f;
            float px = M / size;
            const int seed = 9001;
            return Make("RubberMat", size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                int tx = Mathf.Min(1, (int)(x * 2f)), ty = Mathf.Min(1, (int)(y * 2f));
                float r1 = ProcTex.Hash01(tx, ty, seed), r2 = ProcTex.Hash01(tx, ty, seed + 1);
                float d = Mathf.Min(ProcTex.LineDistance(x, 0.5f), ProcTex.LineDistance(y, 0.5f));
                float seam = ProcTex.Fill(d - 0.0018f, px * 0.6f);
                float bevel = 1f - ProcTex.Smooth(0.0018f, 0.006f, d);

                ProcTex.Worley(u, v, 72, seed + 2, out float edge, out float id);
                float pebble = ProcTex.Smooth(0f, 0.35f, edge);
                float wear = ProcTex.Fbm(u, v, 4, 4, seed + 3);
                float dust = ProcTex.Smooth(0.5f, 0.8f, ProcTex.Fbm(u, v, 6, 4, seed + 4));
                float scuff = Scuffs(x * 4f, y * 4f, 4f, px * 4f, seed + 5);
                Color col = ProcTex.Mul(C(0.115f, 0.115f, 0.12f), 1f + (r1 - 0.5f) * 0.12f + (pebble - 0.5f) * 0.12f + (id - 0.5f) * 0.05f);
                col = Color.Lerp(col, C(0.28f, 0.27f, 0.26f), dust * 0.35f * (1f - pebble * 0.5f));
                col = Color.Lerp(col, C(0.3f, 0.29f, 0.28f), scuff * 0.3f);
                col = Color.Lerp(col, C(0.03f, 0.03f, 0.03f), seam);
                c = col;
                h = pebble * 0.0008f - seam * 0.003f - bevel * 0.0008f;
                m = 0f;
                s = 0.24f + (r2 - 0.5f) * 0.06f + ProcTex.Smooth(0.55f, 0.85f, wear) * pebble * 0.14f - dust * 0.1f;
                ao = 1f - seam * 0.6f - (1f - pebble) * 0.12f;
            });
        }

        /// <summary>Diamond tread plate steel (relief only, not cut out): worn shiny tops, dirt between the lentils.</summary>
        public static TexSet SteelGrate(int size = 512)
        {
            const float M = 0.5f;
            const int cells = 12;
            const int seed = 9907;
            return Make("SteelGrate", size, M, 1.3f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * cells, y = v * cells;
                int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
                float lx = x - cx - 0.5f, ly = y - cy - 0.5f;
                bool flip = ((cx + cy) & 1) == 0;
                const float k = 0.70710678f;
                float a = flip ? (lx + ly) * k : (lx - ly) * k, b = flip ? (lx - ly) * k : (lx + ly) * k;
                float cellM = M / cells;
                float A = 0.4f, B = 0.105f; // lentil half length and half width (in cells)
                float e = ProcTex.Pow2(a / A) + ProcTex.Pow2(b / B);
                float lentil = e < 1f ? Mathf.Sqrt(1f - e) : 0f;
                float rim = ProcTex.Saturate(1f - (e - 1f) * 2.5f) * (e >= 1f ? 1f : 0f);
                float var = ProcTex.Fbm(u, v, 3, 4, seed);
                float dirtN = ProcTex.Fbm(u, v, 12, 3, seed + 1);
                float dirt = ProcTex.Saturate((1f - lentil) * ProcTex.Smooth(0.35f, 0.75f, dirtN) + rim * 0.4f);
                Color col = ProcTex.Mul(C(0.6f, 0.61f, 0.62f), 1f + (var - 0.5f) * 0.12f + lentil * 0.08f);
                col = Color.Lerp(col, C(0.27f, 0.26f, 0.25f), dirt * 0.7f);
                c = col;
                h = lentil * 0.0016f * (cellM / 0.0417f);
                m = 1f - dirt * 0.45f;
                s = 0.42f + lentil * 0.22f + (var - 0.5f) * 0.1f - dirt * 0.22f;
                ao = 1f - rim * 0.25f - (1f - lentil) * 0.08f;
            });
        }

        /// <summary>Ceiling light panel: white diffuser cells (25 cm) in an aluminium grid. Emission map = the glowing cells.</summary>
        public static TexSet LightPanel(int size = 512)
        {
            const float M = 1f;
            float px = M / size;
            const float cell = 0.25f;
            return Make("LightPanel", size, M, 1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                float d = Mathf.Min(ProcTex.LineDistance(x, cell), ProcTex.LineDistance(y, cell));
                float frame = ProcTex.Fill(d - 0.008f, px * 0.6f);
                c = Color.Lerp(C(0.95f, 0.95f, 0.94f), C(0.62f, 0.63f, 0.65f), frame);
                h = frame * 0.004f;
                m = frame * 0.8f;
                s = Mathf.Lerp(0.7f, 0.45f, frame);
                ao = 1f - (1f - ProcTex.Smooth(0.008f, 0.02f, d)) * (1f - frame) * 0.2f;
            }, (u, v) =>
            {
                float x = u * M, y = v * M;
                float d = Mathf.Min(ProcTex.LineDistance(x, cell), ProcTex.LineDistance(y, cell));
                float frame = ProcTex.Fill(d - 0.008f, px * 0.6f);
                float glow = (0.82f + 0.18f * ProcTex.Smooth(0.008f, 0.07f, d)) * (1f - frame);
                return new Color(glow, glow * 0.985f, glow * 0.95f, 1f);
            });
        }

        /// <summary>Dark teal display glass with fine scanlines; the emission map is a faint teal glow (text goes on top).</summary>
        public static TexSet Screen(int size = 512)
        {
            const float M = 0.5f;
            float px = M / size;
            const int seed = 10111;
            int lines = Mathf.Max(16, size / 4);
            return Make("Screen", size, M, 1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float smudge = ProcTex.Fbm(u, v, 4, 4, seed);
                c = C(0.02f, 0.035f, 0.04f);
                h = 0f;
                m = 0f;
                s = 0.9f - ProcTex.Smooth(0.6f, 0.85f, smudge) * 0.2f;
                ao = 1f;
            }, (u, v) =>
            {
                float scan = ProcTex.Lines(v, lines, 0.5f / lines);
                float col = ProcTex.Lines(u, lines * 2, 0.35f / (lines * 2));
                float n = ProcTex.Fbm(u, v, 6, 3, seed + 1);
                float g = 0.11f + scan * 0.05f - col * 0.02f + (n - 0.5f) * 0.05f;
                return new Color(Teal.r * g, Teal.g * g, Teal.b * g, 1f);
            });
        }

        /// <summary>Lightly tinted glass for a Fade material (albedo alpha = opacity), with faint smudges.</summary>
        public static TexSet Glass(int size = 512)
        {
            const float M = 1f;
            const int seed = 10501;
            var set = Make("Glass", size, M, 0.5f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float smudge = ProcTex.Smooth(0.55f, 0.85f, ProcTex.Fbm(u, v, 4, 5, seed));
                float wave = ProcTex.Fbm(u, v, 2, 3, seed + 1);
                c = C(0.7f, 0.8f, 0.82f);
                h = (wave - 0.5f) * 0.0004f;
                m = 0f;
                s = 0.95f - smudge * 0.3f;
                ao = 1f;
            });
            // Opacity: mostly clear, smudges a little more visible.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sm = ProcTex.Smooth(0.55f, 0.85f, ProcTex.Fbm((x + 0.5f) / size, (y + 0.5f) / size, 4, 5, seed));
                    set.albedo[y * size + x].a = 0.22f + sm * 0.08f;
                }
            return set;
        }

        // ================================================================== outdoors

        /// <summary>
        /// Lawn grass: a dense mat of overlapping blades in several greens (darker deeper down), drier patches, clover
        /// patches and the odd bare spot. Low contrast between blades, so it reads as a lawn rather than noise.
        /// </summary>
        public static TexSet Grass(int size = 512)
        {
            const float M = 2f;
            const int N = 64, K = 4; // blade cells across the texture (about 3 cm), blades per cell
            const int seed = 11003;
            float aa = N / (float)size * 0.7f;
            Color deep = C(0.12f, 0.19f, 0.065f), soil = C(0.24f, 0.19f, 0.11f);
            Color g1 = C(0.19f, 0.3f, 0.09f), g2 = C(0.25f, 0.38f, 0.11f), g3 = C(0.33f, 0.44f, 0.15f), dry = C(0.48f, 0.46f, 0.26f);
            // Blade parameters per cell, worked out once (start, direction x length, half width, depth, colour pick).
            int count = N * N * K;
            var bx = new float[count]; var by = new float[count]; var dx = new float[count]; var dy = new float[count];
            var bw = new float[count]; var bz = new float[count]; var hue = new float[count];
            for (int cy = 0; cy < N; cy++)
                for (int cx = 0; cx < N; cx++)
                    for (int k = 0; k < K; k++)
                    {
                        int i = (cy * N + cx) * K + k, sd = seed + 10 + k * 17;
                        bx[i] = ProcTex.Hash01(cx, cy, sd);
                        by[i] = ProcTex.Hash01(cx, cy, sd + 1);
                        float ang = ProcTex.Hash01(cx, cy, sd + 2) * Mathf.PI * 2f, len = 0.5f + ProcTex.Hash01(cx, cy, sd + 3) * 0.6f;
                        dx[i] = Mathf.Cos(ang) * len;
                        dy[i] = Mathf.Sin(ang) * len;
                        bw[i] = 0.1f + ProcTex.Hash01(cx, cy, sd + 4) * 0.06f;
                        bz[i] = ProcTex.Hash01(cx, cy, sd + 5);
                        hue[i] = ProcTex.Hash01(cx, cy, sd + 6);
                    }
            return Make("Grass", size, M, 1.1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float big = ProcTex.Fbm(u, v, 2, 3, seed);
                float patch = ProcTex.Fbm(u, v, 5, 3, seed + 1);
                float x = u * N, y = v * N;
                int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
                float bestZ = -1f, bestCov = 0f, bestT = 0f, bestHue = 0f;
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int gx = cx + ox, gy = cy + oy, cell = (ProcTex.Wrap(gy, N) * N + ProcTex.Wrap(gx, N)) * K;
                        for (int k = 0; k < K; k++)
                        {
                            int i = cell + k;
                            float sx = gx + bx[i], sy = gy + by[i];
                            float d = ProcTex.Segment(x, y, sx, sy, sx + dx[i], sy + dy[i], out float t);
                            float cov = ProcTex.Fill(d - bw[i] * (1f - t * 0.8f), aa);
                            if (cov <= 0.02f) continue;
                            float z = bz[i] + t * 0.35f;
                            if (z <= bestZ) continue;
                            bestZ = z;
                            bestCov = cov;
                            bestT = t;
                            bestHue = hue[i];
                        }
                    }
                Color bc = bestHue < 0.4f ? Color.Lerp(g1, g2, bestHue / 0.4f) : Color.Lerp(g2, g3, (bestHue - 0.4f) / 0.6f);
                bc = Color.Lerp(bc, dry, ProcTex.Smooth(0.6f, 0.85f, big) * 0.4f + (bestHue > 0.96f ? 0.45f : 0f));
                bc = ProcTex.Mul(bc, 0.86f + bestT * 0.2f + Mathf.Min(1f, bestZ) * 0.08f);
                // Tufts: each clump a little lighter in the middle.
                ProcTex.Worley(u, v, 22, seed + 4, out float tuftEdge, out float tuftId);
                bc = ProcTex.Mul(bc, 0.9f + ProcTex.Smooth(0f, 0.5f, tuftEdge) * 0.12f + (tuftId - 0.5f) * 0.08f);

                // Clover in patches, sitting low among the grass.
                float clover = 0f;
                float cloverMask = ProcTex.Smooth(0.6f, 0.68f, patch);
                if (cloverMask > 0f)
                {
                    const int CN = 30;
                    float qx = u * CN, qy = v * CN;
                    int qcx = Mathf.FloorToInt(qx), qcy = Mathf.FloorToInt(qy);
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = qcx + ox, gy = qcy + oy, wx = ProcTex.Wrap(gx, CN), wy = ProcTex.Wrap(gy, CN);
                            float px0 = gx + ProcTex.Hash01(wx, wy, seed + 90), py0 = gy + ProcTex.Hash01(wx, wy, seed + 91);
                            float rot = ProcTex.Hash01(wx, wy, seed + 92) * 6.283f;
                            for (int leaf = 0; leaf < 3; leaf++)
                            {
                                float a = rot + leaf * 2.094f;
                                float lx = px0 + Mathf.Cos(a) * 0.16f, ly = py0 + Mathf.Sin(a) * 0.16f;
                                float dd = Mathf.Sqrt((qx - lx) * (qx - lx) + (qy - ly) * (qy - ly));
                                clover = Mathf.Max(clover, ProcTex.Fill(dd - 0.15f, CN / (float)size * 0.7f));
                            }
                        }
                    clover *= cloverMask;
                }

                // Between the blades: deep green shade, with earth showing in a few bare spots.
                float bare = ProcTex.Smooth(0.7f, 0.8f, ProcTex.Fbm(u, v, 7, 3, seed + 3));
                Color under = Color.Lerp(deep, soil, bare * 0.6f);
                float cover = bestCov * (1f - bare * 0.5f);
                Color col = Color.Lerp(under, bc, cover);
                if (clover > 0f && (bestZ < 0.9f || bestCov < 0.5f))
                    col = Color.Lerp(col, C(0.21f, 0.37f, 0.13f), clover * 0.9f);
                // Broad light and dark drifts across the lawn so repeats don't show.
                col = ProcTex.Mul(col, 0.9f + big * 0.16f + (patch - 0.5f) * 0.08f);
                c = col;
                h = Mathf.Max(0f, bestZ) * cover * 0.0035f + clover * 0.003f;
                m = 0f;
                s = 0.1f + cover * 0.14f + clover * 0.08f;
                ao = 0.62f + 0.38f * Mathf.Max(cover * (0.6f + 0.4f * Mathf.Min(1f, bestZ)), clover);
            });
        }

        /// <summary>Packed earth: dry and damp patches, small pebbles, clods and fine cracks in the dry parts.</summary>
        public static TexSet Dirt(int size = 512)
        {
            const float M = 2f;
            float px = M / size;
            const int seed = 12007;
            Color dryC = C(0.5f, 0.42f, 0.32f), midC = C(0.41f, 0.32f, 0.23f), dampC = C(0.28f, 0.21f, 0.15f);
            return Make("Dirt", size, M, 1.4f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float wet = ProcTex.Fbm(u, v, 3, 4, seed);
                float clod = ProcTex.Fbm(u, v, 24, 4, seed + 1);
                float fine = ProcTex.Fbm(u, v, 128, 2, seed + 2);
                Color col = ProcTex.Lerp3(dryC, midC, dampC, ProcTex.Smooth(0.3f, 0.75f, wet));
                col = ProcTex.Mul(col, 1f + (clod - 0.5f) * 0.25f + (fine - 0.5f) * 0.1f);
                h = (clod - 0.5f) * 0.006f + (fine - 0.5f) * 0.001f;

                float pu = u, pv = v;
                ProcTex.Warp(ref pu, ref pv, 28, 0.25f / 28f, seed + 6); // irregular, not round
                float f1 = ProcTex.Worley(pu, pv, 28, seed + 3, out float pe, out float id);
                float pr = 0.14f + id * 1.1f;
                float pebble = id < 0.2f ? ProcTex.Fill(f1 - pr, 0.1f) : 0f;
                // Pebbles are only a little paler than the earth, and half buried in it.
                Color stone = ProcTex.Mul(id < 0.12f ? C(0.47f, 0.44f, 0.4f) : id < 0.22f ? C(0.42f, 0.36f, 0.29f) : C(0.52f, 0.47f, 0.41f), 0.85f + clod * 0.25f);
                col = Color.Lerp(col, Color.Lerp(col, stone, 0.75f), pebble);
                h += pebble * Mathf.Sqrt(Mathf.Max(0f, 1f - ProcTex.Pow2(f1 / pr))) * 0.0022f;

                // Dry mud cracks.
                ProcTex.Worley(u, v, 7, seed + 4, out float ce, out _);
                float dryness = ProcTex.Smooth(0.45f, 0.25f, wet);
                float crack = ProcTex.Fill(ce * (M / 7f) * 0.5f - 0.001f, px * 0.6f) * dryness * ProcTex.Smooth(0.5f, 0.65f, ProcTex.Fbm(u, v, 8, 2, seed + 5));
                col = Color.Lerp(col, col * 0.6f, crack);
                h -= crack * 0.003f;
                c = col;
                m = 0f;
                s = 0.1f + ProcTex.Smooth(0.5f, 0.85f, wet) * 0.14f + pebble * 0.08f;
                ao = 1f - crack * 0.5f - (1f - pebble) * ProcTex.Smooth(0.5f, 0.3f, clod) * 0.15f;
            });
        }

        /// <summary>Loose gravel: two sizes of rounded stones in greys and beiges with dark gaps.</summary>
        public static TexSet Gravel(int size = 512)
        {
            const float M = 1f;
            const int seed = 12503;
            return Make("Gravel", size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                ProcTex.Worley(u, v, 18, seed, out float e1, out float id1);
                ProcTex.Worley(u, v, 37, seed + 1, out float e2, out float id2);
                float d1 = ProcTex.Smooth(0.02f, 0.4f, e1), d2 = ProcTex.Smooth(0.02f, 0.4f, e2);
                float h1 = Mathf.Sqrt(d1) * 0.014f, h2 = Mathf.Sqrt(d2) * 0.008f;
                bool top = h1 >= h2;
                float id = top ? id1 : id2;
                float dome = top ? d1 : d2;
                float n = ProcTex.Fbm(u, v, 64, 2, seed + 2);
                Color stone = id < 0.25f ? C(0.56f, 0.55f, 0.53f) : id < 0.5f ? C(0.64f, 0.59f, 0.51f) : id < 0.75f ? C(0.4f, 0.39f, 0.38f) : C(0.52f, 0.45f, 0.38f);
                stone = ProcTex.Mul(stone, 0.8f + dome * 0.3f + (n - 0.5f) * 0.15f);
                float gap = 1f - ProcTex.Smooth(0.01f, 0.16f, Mathf.Max(top ? e1 : e2, 0f));
                Color col = Color.Lerp(stone, C(0.17f, 0.15f, 0.13f), gap * 0.65f);
                c = col;
                h = Mathf.Max(h1, h2) + (n - 0.5f) * 0.0006f;
                m = 0f;
                s = 0.22f + dome * 0.12f + (n - 0.5f) * 0.06f;
                ao = 1f - gap * 0.6f;
            });
        }

        /// <summary>Weathered boulder rock: creases, cracks, colour bands and sparse lichen.</summary>
        public static TexSet Rock(int size = 512)
        {
            const float M = 3f;
            float px = M / size;
            const int seed = 13001;
            Color a = C(0.5f, 0.47f, 0.43f), b = C(0.41f, 0.39f, 0.37f), w = C(0.58f, 0.53f, 0.46f);
            return Make("Rock", size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 3, 0.15f, seed);
                float form = ProcTex.Fbm(wu, wv, 3, 5, seed + 1);
                float ridge = ProcTex.Ridged(wu, wv, 4, 5, seed + 2);
                float detail = ProcTex.Fbm(u, v, 32, 3, seed + 3);
                ProcTex.Worley(wu, wv, 6, seed + 4, out float ce, out _);
                float crack = ProcTex.Fill(ce * (M / 6f) * 0.5f - 0.003f, px * 0.7f) * ProcTex.Smooth(0.4f, 0.6f, ProcTex.Fbm(u, v, 4, 2, seed + 5));
                Color col = ProcTex.Lerp3(b, a, w, ProcTex.Saturate(form * 0.9f + (detail - 0.5f) * 0.4f));
                col = ProcTex.Mul(col, 0.8f + ridge * 0.35f);
                // Lichen: pale grey-green and dark spots on the upper parts.
                ProcTex.Worley(u, v, 18, seed + 6, out float le, out float lid);
                float lichenMask = ProcTex.Smooth(0.6f, 0.72f, ProcTex.Fbm(u, v, 6, 3, seed + 7));
                float lichen = ProcTex.Smooth(0.05f, 0.2f, le) * lichenMask;
                col = Color.Lerp(col, lid < 0.6f ? C(0.62f, 0.62f, 0.48f) : C(0.25f, 0.26f, 0.21f), lichen * 0.7f);
                col = Color.Lerp(col, col * 0.4f, crack);
                c = col;
                h = form * 0.06f + ridge * 0.025f + (detail - 0.5f) * 0.006f - crack * 0.012f + lichen * 0.0006f;
                m = 0f;
                s = 0.2f + (detail - 0.5f) * 0.08f - lichen * 0.08f;
                ao = 0.75f + 0.25f * ridge - crack * 0.4f;
            });
        }

        /// <summary>Cliff face: layered sedimentary strata (soft transitions), weathered rock texture, ledges and vertical fissures.</summary>
        public static TexSet Cliff(int size = 512)
        {
            const float M = 6f;
            float px = M / size;
            const int seed = 13501;
            const int layers = 9;
            Color[] pal = { C(0.58f, 0.51f, 0.42f), C(0.53f, 0.5f, 0.46f), C(0.56f, 0.47f, 0.38f), C(0.49f, 0.46f, 0.41f), C(0.61f, 0.56f, 0.48f) };
            return Make("Cliff", size, M, 1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float warp = ProcTex.Fbm(u, v, 3, 4, seed) - 0.5f;
                float b = v * layers + warp * 1.4f + (ProcTex.Fbm(u, v, 8, 2, seed + 1) - 0.5f) * 0.35f;
                int band = Mathf.FloorToInt(b);
                float fb = b - band;
                Color lo = pal[(int)(ProcTex.Hash01(ProcTex.Wrap(band, layers), 0, seed + 2) * pal.Length) % pal.Length];
                Color hi = pal[(int)(ProcTex.Hash01(ProcTex.Wrap(band + 1, layers), 0, seed + 2) * pal.Length) % pal.Length];
                Color col = Color.Lerp(lo, hi, ProcTex.Smooth(0.82f, 1f, fb)); // strata blend into each other
                col = ProcTex.Mul(col, 0.92f + ProcTex.Hash01(ProcTex.Wrap(band, layers), 1, seed + 2) * 0.14f);

                float detail = ProcTex.Fbm(u, v, 24, 4, seed + 3);
                float ridged = ProcTex.Ridged(u, v, 10, 4, seed + 6);
                float erosion = ProcTex.Fbm(u, v, 6, 3, seed + 7);
                float fis = ProcTex.Perlin(u, v, 14, 2, seed + 4);
                float fissure = ProcTex.Fill(Mathf.Abs(fis - 0.5f) * (M / 14f) - 0.003f, px) * ProcTex.Smooth(0.45f, 0.62f, ProcTex.Perlin(u, v, 3, 6, seed + 5));
                float underLedge = 1f - ProcTex.Smooth(0f, 0.12f, fb); // shade just under each layer's lip
                col = ProcTex.Mul(col, 0.8f + detail * 0.22f + ridged * 0.1f + (erosion - 0.5f) * 0.16f - underLedge * 0.12f);
                col = Color.Lerp(col, col * 0.65f, fissure);
                c = col;
                float slope = fb * (1f - ProcTex.Smooth(0.86f, 1f, fb)); // each layer leans back, rounded off at its lip
                h = slope * 0.035f + (detail - 0.5f) * 0.02f + ridged * 0.025f + (erosion - 0.5f) * 0.03f - fissure * 0.025f;
                m = 0f;
                s = 0.16f + (detail - 0.5f) * 0.08f;
                ao = 0.72f + ridged * 0.15f + (1f - underLedge) * 0.13f - fissure * 0.35f;
            });
        }

        /// <summary>Painted render (plaster) in the given colour: trowel marks, sand grain, hairline cracks, faint rain streaks.</summary>
        public static TexSet Plaster(Color tint, int size = 512, string name = null)
        {
            const float M = 2f;
            float px = M / size;
            string n = name ?? "Plaster_" + Tag(tint);
            int seed = ProcTex.Seed(n);
            return Make(n, size, M, 1.3f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float mott = ProcTex.Fbm(u, v, 3, 5, seed);
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 5, 0.08f, seed + 1);
                float trowel = ProcTex.Fbm(wu, wv, 10, 3, seed + 2);
                float sand = ProcTex.Fbm(u, v, 128, 2, seed + 3);
                float streak = ProcTex.Fbm(u, v, 24, 2, 3, seed + 4);
                Color col = ProcTex.Mul(tint, 1f + (mott - 0.5f) * 0.12f + (trowel - 0.5f) * 0.06f + (sand - 0.5f) * 0.05f);
                col = Color.Lerp(col, col * C(0.82f, 0.8f, 0.76f), ProcTex.Smooth(0.55f, 0.8f, streak) * 0.3f);
                ProcTex.Worley(wu, wv, 4, seed + 5, out float ce, out _);
                float crack = ProcTex.Fill(ce * (M / 4f) * 0.5f - 0.0008f, px * 0.6f) * ProcTex.Smooth(0.6f, 0.72f, ProcTex.Fbm(u, v, 4, 2, seed + 6));
                // A few spots where the render has come off.
                float spall = ProcTex.Smooth(0.765f, 0.79f, ProcTex.Fbm(wu, wv, 6, 4, seed + 7));
                col = Color.Lerp(col, col * 0.72f, crack);
                col = Color.Lerp(col, ProcTex.Mul(C(0.55f, 0.5f, 0.45f), 0.9f + sand * 0.2f), spall);
                c = col;
                h = (trowel - 0.5f) * 0.0025f + (sand - 0.5f) * 0.0007f - crack * 0.001f - spall * 0.004f;
                m = 0f;
                s = 0.12f + (trowel - 0.5f) * 0.06f;
                ao = 1f - crack * 0.4f - spall * 0.15f;
            });
        }

        /// <summary>Red brick in running bond (21.5 x 6.5 cm bricks, 1 cm recessed mortar), with burnt and chipped bricks.</summary>
        public static TexSet Brick(int size = 512)
        {
            const float M = 0.9f; // 4 bricks across, 12 courses up
            float px = M / size;
            const int seed = 14009;
            return Make("Brick", size, M, 1.4f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float y = v * 12f;
                int row = Mathf.Min(11, (int)y);
                float x = u * 4f + ((row & 1) == 1 ? 0.5f : 0f);
                int bi = ProcTex.Wrap(Mathf.FloorToInt(x), 4);
                float lx = (x - Mathf.Floor(x)) * 0.225f, ly = (y - row) * 0.075f;
                float edgeN = (ProcTex.Fbm(u, v, 48, 3, seed) - 0.5f) * 0.004f;
                float d = ProcTex.Box(lx - 0.1125f, ly - 0.0375f, 0.1075f, 0.0325f) + edgeN;
                float brick = ProcTex.Fill(d, px * 0.6f);
                float r1 = ProcTex.Hash01(bi, row, seed + 1), r2 = ProcTex.Hash01(bi, row, seed + 2);
                Color bc = r1 < 0.12f ? C(0.33f, 0.18f, 0.13f) : r1 > 0.85f ? C(0.66f, 0.38f, 0.25f) : Color.Lerp(C(0.5f, 0.24f, 0.17f), C(0.6f, 0.31f, 0.21f), r2);
                float face = ProcTex.Fbm(u, v, 64, 3, seed + 3);
                float pit = ProcTex.Worley(u, v, 90, seed + 4, out _, out float pid);
                float pitC = pid < 0.25f ? ProcTex.Fill(pit * (M / 90f) - 0.0012f, px * 0.6f) : 0f;
                bc = ProcTex.Mul(bc, 0.88f + face * 0.24f);
                bc = Color.Lerp(bc, bc * 0.6f, pitC);
                float mortarN = ProcTex.Fbm(u, v, 96, 2, seed + 5);
                Color mortar = ProcTex.Mul(C(0.6f, 0.58f, 0.53f), 0.85f + mortarN * 0.3f);
                c = Color.Lerp(mortar, bc, brick);
                float round = ProcTex.Smooth(0.006f, -0.001f, d);
                h = brick * (0.006f + round * -0.0f) - (1f - brick) * 0.0f + ProcTex.Smooth(0f, -0.004f, d) * 0.002f + (face - 0.5f) * 0.0008f * brick - pitC * 0.001f + (mortarN - 0.5f) * 0.0008f * (1f - brick);
                m = 0f;
                s = Mathf.Lerp(0.08f, 0.2f + (r2 - 0.5f) * 0.06f, brick);
                ao = Mathf.Lerp(0.6f, 1f, ProcTex.Smooth(0.003f, -0.002f, d));
            });
        }

        /// <summary>Dressed stone blocks in courses of varying height and length, with chipped edges and mortar joints.</summary>
        public static TexSet StoneBlocks(int size = 512)
        {
            const float M = 2f;
            float px = M / size;
            const int seed = 14503;
            return Make("StoneBlocks", size, M, 1.3f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                int row = ProcTex.Pieces(v, 6, 0.25f, seed, out float fy, out float rh);
                float shift = ProcTex.Hash01(row, 3, seed);
                int blk = ProcTex.Pieces(u + shift, 4, 0.45f, seed + 100 + row * 7, out float fx, out float bw);
                float wM = bw * M, hM = rh * M;
                float lx = (fx - 0.5f) * wM, ly = (fy - 0.5f) * hM;
                float edgeN = (ProcTex.Fbm(u, v, 24, 3, seed + 1) - 0.5f) * 0.012f;
                float d = ProcTex.Box(lx, ly, wM * 0.5f - 0.008f, hM * 0.5f - 0.008f) + edgeN;
                float stone = ProcTex.Fill(d, px * 0.6f);
                float r1 = ProcTex.Hash01(blk, row, seed + 2), r2 = ProcTex.Hash01(blk, row, seed + 3);
                float chisel = ProcTex.Fbm(u, v, 40, 3, seed + 4);
                float cloud = ProcTex.Fbm(u, v, 6, 3, seed + 5);
                Color sc = Color.Lerp(C(0.6f, 0.58f, 0.53f), C(0.68f, 0.63f, 0.55f), r1);
                sc = ProcTex.Mul(sc, 0.9f + (r2 - 0.5f) * 0.16f + (chisel - 0.5f) * 0.14f + (cloud - 0.5f) * 0.12f);
                Color mortar = ProcTex.Mul(C(0.47f, 0.45f, 0.41f), 0.9f + chisel * 0.2f);
                float bevel = ProcTex.Smooth(0f, -0.02f, d);
                c = Color.Lerp(mortar, sc, stone);
                h = stone * (0.004f + bevel * 0.006f) + (chisel - 0.5f) * 0.002f * stone + (cloud - 0.5f) * 0.002f;
                m = 0f;
                s = Mathf.Lerp(0.1f, 0.22f + (r2 - 0.5f) * 0.06f, stone);
                ao = Mathf.Lerp(0.55f, 1f, ProcTex.Smooth(0.004f, -0.01f, d));
            });
        }

        /// <summary>Wooden deck planks (20 cm) running along u, staggered butt joints, grain, knots and nails.</summary>
        public static TexSet WoodPlanks(int size = 512)
        {
            const float M = 2f;
            float px = M / size;
            const int seed = 15013;
            const int planks = 10;
            return Make("WoodPlanks", size, M, 1.3f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float y = v * planks;
                int row = Mathf.Min(planks - 1, (int)y);
                float fy = y - row;
                float shift = ProcTex.Hash01(row, 0, seed);
                int seg = ProcTex.Pieces(u + shift, 2, 0.4f, seed + 50 + row * 13, out float fx, out float bw);
                int id = row * 4 + seg;
                float lenM = bw * M;
                float ex = Mathf.Min(fx, 1f - fx) * lenM, ey = Mathf.Min(fy, 1f - fy) * (M / planks);
                float gap = 1f - ProcTex.Smooth(0.0015f, 0.003f, Mathf.Min(ex, ey));
                float round = 1f - ProcTex.Smooth(0.002f, 0.008f, Mathf.Min(ex, ey));
                float r1 = ProcTex.Hash01(id, 1, seed), r2 = ProcTex.Hash01(id, 2, seed);
                float grain = ProcTex.Fbm(u, v, 2, 40, 3, seed + id * 7);
                float rings = ProcTex.Frac(grain * 9f + ProcTex.Perlin(u, v, 3, 20, seed + 3) * 0.6f);
                float late = ProcTex.Smooth(0.75f, 0.92f, rings);
                Color wc = Color.Lerp(C(0.55f, 0.39f, 0.25f), C(0.66f, 0.5f, 0.33f), r1);
                wc = Color.Lerp(wc, C(0.52f, 0.5f, 0.46f), ProcTex.Smooth(0.7f, 0.95f, r2) * 0.6f); // a few weathered grey planks
                wc = ProcTex.Mul(wc, 0.92f + (grain - 0.5f) * 0.25f - late * 0.18f);
                // Two nails at each end of every plank.
                float nx = Mathf.Min(Mathf.Abs(fx * lenM - 0.05f), Mathf.Abs((1f - fx) * lenM - 0.05f));
                float ny = Mathf.Abs(fy - 0.5f) * (M / planks) - 0.05f;
                float nail = ProcTex.Fill(Mathf.Sqrt(nx * nx + ny * ny) - 0.004f, px * 0.6f);
                Color col = Color.Lerp(wc, C(0.18f, 0.16f, 0.15f), nail);
                col = Color.Lerp(col, C(0.06f, 0.05f, 0.04f), gap);
                c = col;
                h = -gap * 0.006f - round * round * 0.0015f + (grain - 0.5f) * 0.0006f - late * 0.0003f + (r1 - 0.5f) * 0.0006f;
                m = nail * 0.6f;
                s = 0.24f + (r2 - 0.5f) * 0.06f - late * 0.04f;
                ao = 1f - gap * 0.7f - round * 0.15f;
            });
        }

        /// <summary>Weathered timber for beams and posts: grain along u, drying checks (cracks) and a dark stain.</summary>
        public static TexSet Timber(int size = 512)
        {
            const float M = 1.5f;
            float px = M / size;
            const int seed = 15511;
            return Make("Timber", size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float grain = ProcTex.Fbm(u, v, 2, 48, 3, seed);
                float rings = ProcTex.Frac(grain * 10f + ProcTex.Perlin(u, v, 2, 12, seed + 1) * 0.8f);
                float late = ProcTex.Smooth(0.72f, 0.92f, rings);
                float tone = ProcTex.Fbm(u, v, 3, 3, seed + 2);
                float chk = ProcTex.Perlin(u, v, 2, 20, seed + 3);
                float check = ProcTex.Fill(Mathf.Abs(chk - 0.5f) * (M / 20f) - 0.0012f, px * 0.6f) * ProcTex.Smooth(0.45f, 0.65f, ProcTex.Perlin(u, v, 6, 3, seed + 4));
                Color col = ProcTex.Mul(C(0.36f, 0.24f, 0.15f), 0.85f + tone * 0.3f + (grain - 0.5f) * 0.25f - late * 0.2f);
                col = Color.Lerp(col, C(0.06f, 0.04f, 0.03f), check);
                c = col;
                h = (grain - 0.5f) * 0.001f - late * 0.0005f - check * 0.004f;
                m = 0f;
                s = 0.2f + (tone - 0.5f) * 0.06f;
                ao = 1f - check * 0.6f;
            });
        }

        /// <summary>
        /// Clay roof tiles in the given colour: rounded tiles 20 cm wide, 30 cm courses, moss in the grooves.
        /// v runs UP the roof (each course's lip faces -v).
        /// </summary>
        public static TexSet RoofTiles(Color tint, int size = 512, string name = null)
        {
            const float M = 1.2f;
            string n = name ?? "RoofTiles_" + Tag(tint);
            int seed = ProcTex.Seed(n);
            return Make(n, size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float y = v * 4f;
                int row = Mathf.Min(3, (int)y);
                float fy = y - row;
                float t = u * 6f + ((row & 1) == 1 ? 0.5f : 0f);
                int ti = ProcTex.Wrap(Mathf.FloorToInt(t), 6);
                float ft = t - Mathf.Floor(t);
                float barrel = Mathf.Sin(ft * Mathf.PI);
                float groove = 1f - ProcTex.Smooth(0f, 0.25f, barrel);
                float r1 = ProcTex.Hash01(ti, row, seed), r2 = ProcTex.Hash01(ti, row, seed + 1);
                float n1 = ProcTex.Fbm(u, v, 24, 3, seed + 2);
                float dirt = ProcTex.Fbm(u, v, 6, 3, seed + 3);
                Color col = ProcTex.Mul(tint, 0.85f + r1 * 0.3f + (n1 - 0.5f) * 0.12f);
                col = Color.Lerp(col, col * 0.7f, groove * 0.6f + ProcTex.Smooth(0.6f, 0.85f, dirt) * 0.3f);
                // Moss and grime collect in the grooves near the bottom lip of each course.
                float moss = groove * ProcTex.Smooth(0.5f, 0.1f, fy) * ProcTex.Smooth(0.45f, 0.7f, n1 + dirt * 0.4f);
                col = Color.Lerp(col, C(0.3f, 0.33f, 0.16f), moss * 0.7f);
                float under = ProcTex.Smooth(0.75f, 1f, fy); // shadowed just under the next course's lip
                c = Color.Lerp(col, col * 0.45f, under * 0.7f);
                h = (1f - fy) * 0.014f + barrel * 0.016f + (n1 - 0.5f) * 0.0008f + (r2 - 0.5f) * 0.002f;
                m = 0f;
                s = 0.28f + (r2 - 0.5f) * 0.08f - moss * 0.15f;
                ao = 1f - under * 0.55f - groove * 0.2f;
            });
        }

        /// <summary>Pine bark: vertical (along v) plates split by deep dark furrows, flaky, reddish-brown with grey edges.</summary>
        public static TexSet Bark(int size = 512)
        {
            const float M = 1f;
            const int seed = 16001;
            return Make("Bark", size, M, 1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 6, 0.08f, seed);
                ProcTex.Worley(wu, wv, 16, 5, seed + 1, out float edge, out float id);
                float gapW = 0.12f + ProcTex.Fbm(u, v, 12, 2, seed + 5) * 0.2f; // crevices vary in width
                float plate = ProcTex.Smooth(0.01f, gapW, edge);
                float flake = ProcTex.Fbm(u, v, 24, 8, 3, seed + 2);
                float fine = ProcTex.Fbm(u, v, 64, 2, seed + 3);
                float streak = ProcTex.Fbm(u, v, 32, 2, 2, seed + 4); // vertical fibres
                float layers = ProcTex.Frac(flake * 4f + id * 3f); // flaky layers on each plate
                Color pc = Color.Lerp(C(0.34f, 0.23f, 0.15f), C(0.46f, 0.32f, 0.21f), id);
                pc = ProcTex.Mul(pc, 0.8f + flake * 0.3f + (streak - 0.5f) * 0.14f - ProcTex.Smooth(0.7f, 0.95f, layers) * 0.12f);
                pc = Color.Lerp(pc, C(0.44f, 0.41f, 0.38f), ProcTex.Smooth(0.2f, 0.05f, edge) * 0.35f); // grey weathered plate edges
                Color col = Color.Lerp(C(0.17f, 0.12f, 0.085f), pc, plate);
                c = ProcTex.Mul(col, 0.92f + fine * 0.16f);
                h = Mathf.Sqrt(plate) * 0.01f + (flake - 0.5f) * 0.003f * plate - ProcTex.Smooth(0.7f, 0.95f, layers) * 0.001f + (fine - 0.5f) * 0.001f + (streak - 0.5f) * 0.002f;
                m = 0f;
                s = 0.14f + plate * 0.04f;
                ao = 0.5f + plate * 0.5f;
            });
        }

        /// <summary>Pine foliage: dense needle sprigs in dark greens with lighter tips and a few twigs, over deep shadow.</summary>
        public static TexSet PineNeedles(int size = 512)
        {
            const float M = 1f;
            const int N = 30, K = 8; // sprig cells across, needles per sprig
            const int seed = 16501;
            float aa = N / (float)size * 0.7f;
            Color n1 = C(0.07f, 0.16f, 0.07f), n2 = C(0.12f, 0.25f, 0.1f), n3 = C(0.2f, 0.32f, 0.13f);
            // Each cell is a little sprig: a twig with needles fanning out along it. Worked out once per cell.
            int cells = N * N;
            var sx = new float[cells]; var sy = new float[cells]; var tdx = new float[cells]; var tdy = new float[cells]; var sz = new float[cells];
            var nx0 = new float[cells * K]; var ny0 = new float[cells * K]; var ndx = new float[cells * K]; var ndy = new float[cells * K]; var nh = new float[cells * K];
            for (int cy = 0; cy < N; cy++)
                for (int cx = 0; cx < N; cx++)
                {
                    int c0 = cy * N + cx;
                    sx[c0] = ProcTex.Hash01(cx, cy, seed + 1);
                    sy[c0] = ProcTex.Hash01(cx, cy, seed + 2);
                    float dir = ProcTex.Hash01(cx, cy, seed + 3) * Mathf.PI * 2f;
                    tdx[c0] = Mathf.Cos(dir) * 1.2f;
                    tdy[c0] = Mathf.Sin(dir) * 1.2f;
                    sz[c0] = ProcTex.Hash01(cx, cy, seed + 4);
                    for (int k = 0; k < K; k++)
                    {
                        int i = c0 * K + k, sd = seed + 10 + k * 13;
                        float along = (k / (K - 1f)) * 1.1f;
                        nx0[i] = Mathf.Cos(dir) * along;
                        ny0[i] = Mathf.Sin(dir) * along;
                        float side = (k & 1) == 0 ? 1f : -1f;
                        float a = dir + side * (0.6f + ProcTex.Hash01(cx, cy, sd) * 0.4f);
                        float len = 0.55f + ProcTex.Hash01(cx, cy, sd + 1) * 0.35f;
                        ndx[i] = Mathf.Cos(a) * len;
                        ndy[i] = Mathf.Sin(a) * len;
                        nh[i] = ProcTex.Hash01(cx, cy, sd + 2);
                    }
                }
            return Make("PineNeedles", size, M, 1f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * N, y = v * N;
                int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
                float bestZ = -1f, bestCov = 0f, bestT = 0f, bestHue = 0f;
                bool twig = false;
                float tone = ProcTex.Fbm(u, v, 3, 3, seed);
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int gx = cx + ox, gy = cy + oy, c0 = ProcTex.Wrap(gy, N) * N + ProcTex.Wrap(gx, N);
                        float ox0 = gx + sx[c0], oy0 = gy + sy[c0];
                        // Quick reject: nothing of this sprig reaches further than ~2 cells from its start.
                        if (Mathf.Abs(x - ox0) > 2.1f || Mathf.Abs(y - oy0) > 2.1f) continue;
                        for (int k = 0; k < K; k++)
                        {
                            int i = c0 * K + k;
                            float bx = ox0 + nx0[i], by = oy0 + ny0[i];
                            float d = ProcTex.Segment(x, y, bx, by, bx + ndx[i], by + ndy[i], out float t);
                            float cov = ProcTex.Fill(d - 0.06f * (1f - t * 0.6f), aa);
                            if (cov <= 0.02f) continue;
                            float z = sz[c0] + k * 0.04f + t * 0.1f;
                            if (z <= bestZ) continue;
                            bestZ = z;
                            bestCov = cov;
                            bestT = t;
                            bestHue = nh[i];
                            twig = false;
                        }
                        float td = ProcTex.Segment(x, y, ox0, oy0, ox0 + tdx[c0], oy0 + tdy[c0], out _);
                        float tc = ProcTex.Fill(td - 0.035f, aa);
                        float tz = sz[c0] - 0.05f;
                        if (tc > 0.3f && tz > bestZ) { bestZ = tz; bestCov = tc; twig = true; }
                    }
                Color nc = twig ? C(0.24f, 0.17f, 0.1f) : ProcTex.Mul(ProcTex.Lerp3(n1, n2, n3, bestHue * 0.7f + tone * 0.3f), 0.72f + bestT * 0.42f);
                c = Color.Lerp(C(0.045f, 0.08f, 0.04f), nc, bestCov);
                h = Mathf.Max(0f, bestZ) * bestCov * 0.01f;
                m = 0f;
                s = 0.18f + bestCov * 0.14f;
                ao = 0.45f + 0.55f * bestCov * Mathf.Min(1f, 0.5f + bestZ * 0.5f);
            });
        }

        /// <summary>Asphalt with exposed aggregate, worn tracks, cracks and a repair patch; optionally a dashed centre line.</summary>
        public static TexSet Asphalt(int size = 512, bool laneLine = false, float metres = 4f)
        {
            float M = metres;
            float px = M / size;
            const int seed = 17003;
            string name = laneLine ? "AsphaltLane" : "Asphalt";
            return Make(name, size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                float tone = ProcTex.Fbm(u, v, 3, 4, seed);
                float wear = ProcTex.Fbm(u, v, 2, 8, 3, seed + 1);
                int cells = Mathf.RoundToInt(M / 0.018f);
                float f1 = ProcTex.Worley(u, v, cells, seed + 2, out _, out float id);
                float stone = id < 0.5f ? ProcTex.Fill(f1 - 0.28f, 0.16f) : 0f;
                float fine = ProcTex.Fbm(u, v, Mathf.RoundToInt(M * 40f), 2, seed + 3);
                Color col = ProcTex.Mul(C(0.17f, 0.17f, 0.18f), 1f + (tone - 0.5f) * 0.3f + (fine - 0.5f) * 0.1f);
                col = Color.Lerp(col, C(0.28f, 0.28f, 0.28f), ProcTex.Smooth(0.5f, 0.8f, wear) * 0.45f);
                // Worn-through aggregate: only a little lighter than the binder, so it reads as texture, not speckle.
                col = Color.Lerp(col, ProcTex.Mul(C(0.3f, 0.3f, 0.29f), 0.85f + id * 0.3f), stone * 0.5f);
                // Cracks and a darker repair patch.
                float wu = u, wv = v;
                ProcTex.Warp(ref wu, ref wv, 5, 0.05f, seed + 4);
                ProcTex.Worley(wu, wv, 4, seed + 5, out float ce, out _);
                float crack = ProcTex.Fill(ce * (M / 4f) * 0.5f - 0.0025f, px * 0.6f) * ProcTex.Smooth(0.5f, 0.65f, ProcTex.Fbm(u, v, 4, 2, seed + 6));
                float pd = ProcTex.Box(x - M * 0.27f, y - M * 0.68f, M * 0.09f, M * 0.13f) + (ProcTex.Fbm(u, v, 16, 2, seed + 7) - 0.5f) * 0.05f;
                float patch = ProcTex.Fill(pd, px);
                col = Color.Lerp(col, C(0.11f, 0.11f, 0.12f), patch * 0.8f);
                col = Color.Lerp(col, C(0.05f, 0.05f, 0.05f), crack);
                float paint = 0f;
                if (laneLine)
                {
                    float dl = Mathf.Abs(x - M * 0.5f) - 0.06f;
                    float dash = ProcTex.Frac(v) < 0.5f ? 1f : 0f;
                    paint = ProcTex.Fill(dl, px * 0.6f) * dash * (1f - ProcTex.Smooth(0.55f, 0.75f, ProcTex.Fbm(u, v, 24, 3, seed + 8)) * 0.7f);
                    col = Color.Lerp(col, C(0.86f, 0.85f, 0.8f), paint);
                }
                c = col;
                h = stone * 0.0015f + (fine - 0.5f) * 0.0008f - crack * 0.003f + paint * 0.0006f - patch * 0.0005f;
                m = 0f;
                s = 0.16f + patch * 0.08f + paint * 0.12f - stone * 0.04f;
                ao = 1f - crack * 0.5f;
            });
        }

        /// <summary>
        /// One 1.2 m face of a stencilled wooden crate: framed boards, nails, steel corner plates and a sprayed
        /// stencil ("NB-" and the number). number = 0 picks 07.
        /// </summary>
        public static TexSet Crate(int size = 512, int number = 0)
        {
            const float M = 1.2f;
            float px = M / size;
            string label = "NB-" + (number <= 0 ? 7 : number).ToString("00");
            string name = number <= 0 ? "Crate" : "Crate" + number;
            const int seed = 18001;
            const float frame = 0.09f;
            return Make(name, size, M, 1.2f, (float u, float v, out Color c, out float h, out float m, out float s, out float ao) =>
            {
                float x = u * M, y = v * M;
                float ex = Mathf.Min(x, M - x), ey = Mathf.Min(y, M - y);
                bool vert = ex < frame && ex <= ey; // the left/right battens run up, top/bottom ones across
                bool batten = ex < frame || ey < frame;
                float de = batten ? (vert ? Mathf.Min(ex, frame - ex) : Mathf.Min(ey, frame - ey)) : 0f;
                // Boards inside the frame (4 across the height, 5 mm gaps).
                float by = (y - frame) / ((M - frame * 2f) / 4f);
                int board = Mathf.Clamp(Mathf.FloorToInt(by), 0, 3);
                float dGap = ProcTex.LineDistance(y - frame, (M - frame * 2f) / 4f);
                float gap = batten ? 0f : ProcTex.Fill(dGap - 0.0028f, px * 0.6f);
                int part = batten ? (vert ? (x < M * 0.5f ? 10 : 11) : (y < M * 0.5f ? 12 : 13)) : board;
                float r1 = ProcTex.Hash01(part, 0, seed);
                float grain = vert ? ProcTex.Fbm(u, v, 30, 2, 3, seed + part * 5) : ProcTex.Fbm(u, v, 2, 30, 3, seed + part * 5);
                float rings = ProcTex.Frac(grain * 8f);
                Color wood = batten ? Color.Lerp(C(0.58f, 0.43f, 0.27f), C(0.64f, 0.49f, 0.31f), r1) : Color.Lerp(C(0.68f, 0.53f, 0.34f), C(0.76f, 0.6f, 0.4f), r1);
                wood = ProcTex.Mul(wood, 0.9f + (grain - 0.5f) * 0.25f - ProcTex.Smooth(0.78f, 0.93f, rings) * 0.14f);
                float dirt = ProcTex.Fbm(u, v, 5, 3, seed + 1);
                wood = Color.Lerp(wood, wood * C(0.7f, 0.66f, 0.6f), (1f - ProcTex.Smooth(0f, 0.12f, Mathf.Min(ex, ey))) * 0.5f + ProcTex.Smooth(0.6f, 0.85f, dirt) * 0.2f);

                // Stencil, sprayed and a bit worn.
                float worn = ProcTex.Smooth(0.6f, 0.75f, ProcTex.Fbm(u, v, 12, 3, seed + 2));
                float big = ProcTex.TextCentred(x - M * 0.5f, y - M * 0.53f, label, 0.2f, px * 1.2f, 0.002f);
                float small = ProcTex.TextCentred(x - M * 0.5f, y - M * 0.29f, "NULLBOUND TEST STOCK", 0.035f, px * 0.8f, 0.0006f);
                float paint = Mathf.Max(big, small) * (1f - worn * 0.6f) * (batten ? 0f : 1f);
                Color col = Color.Lerp(wood, C(0.05f, 0.05f, 0.05f), paint * 0.9f);
                col = Color.Lerp(col, C(0.08f, 0.06f, 0.04f), gap);

                // Steel corner plates (L shapes) with bolts, and nails where the battens cross the boards.
                float cx0 = Mathf.Min(x, M - x), cy0 = Mathf.Min(y, M - y);
                float plate = ProcTex.Fill(Mathf.Max(Mathf.Min(cx0, cy0) - 0.035f, Mathf.Max(cx0, cy0) - 0.13f), px * 0.6f);
                float boltD = Mathf.Min(Mathf.Sqrt(ProcTex.Pow2(cx0 - 0.0175f) + ProcTex.Pow2(cy0 - 0.09f)), Mathf.Sqrt(ProcTex.Pow2(cx0 - 0.09f) + ProcTex.Pow2(cy0 - 0.0175f)));
                float bolt = ProcTex.Fill(boltD - 0.007f, px * 0.6f) * plate;
                float boardPitch = (M - frame * 2f) / 4f;
                float nailY = ProcTex.LineDistance(y - frame - boardPitch * 0.5f, boardPitch);
                float nailX = Mathf.Abs(Mathf.Min(x, M - x) - frame * 0.5f);
                float nail = vert ? ProcTex.Fill(Mathf.Sqrt(nailX * nailX + nailY * nailY) - 0.005f, px * 0.6f) : 0f;
                col = Color.Lerp(col, C(0.2f, 0.19f, 0.18f), nail);
                Color steel = ProcTex.Mul(C(0.36f, 0.37f, 0.39f), 0.9f + dirt * 0.2f);
                col = Color.Lerp(col, steel, plate);
                col = Color.Lerp(col, C(0.55f, 0.56f, 0.57f), bolt);
                c = col;
                float battenH = batten ? 0.012f * ProcTex.Smooth(0f, 0.006f, de) : 0f;
                h = battenH - gap * 0.004f + (grain - 0.5f) * 0.0006f + plate * 0.0025f + bolt * 0.0015f + paint * 0.00005f;
                m = Mathf.Max(plate * 0.85f, nail * 0.5f);
                s = Mathf.Lerp(0.22f + (r1 - 0.5f) * 0.06f, 0.4f, plate) + paint * 0.05f;
                ao = 1f - gap * 0.6f - (batten ? 0f : (1f - ProcTex.Smooth(0f, 0.02f, Mathf.Min(ex, ey) - frame)) * 0.35f);
            });
        }
    }
}
