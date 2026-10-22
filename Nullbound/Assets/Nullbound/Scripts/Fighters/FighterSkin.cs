using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>A generated skinned body: vertices with up to two bone weights, one triangle list per material slot.</summary>
    public sealed class SkinData
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector4> tangents = new List<Vector4>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        /// <summary>Per vertex: the main bone, a second bone and how much of the second bone (0..1).</summary>
        public readonly List<int> bone0 = new List<int>(), bone1 = new List<int>();
        public readonly List<float> weight1 = new List<float>();
        public readonly List<Slot> slots = new List<Slot>();
        public readonly List<List<int>> triangles = new List<List<int>>();
        /// <summary>The skeleton the mesh was made on (model space, feet at the origin, facing +Z).</summary>
        public BonePose[] bind;

        public List<int> TrianglesFor(Slot slot)
        {
            int i = slots.IndexOf(slot);
            if (i >= 0) return triangles[i];
            slots.Add(slot);
            triangles.Add(new List<int>());
            return triangles[triangles.Count - 1];
        }
    }

    /// <summary>
    /// Builds a fighter's body as ONE smooth, skinned mesh instead of separate balls and tubes: a sculpted torso
    /// (pelvis, waist, ribcage, chest with pecs, shoulders and trapezius flowing into the neck), and arms and legs with
    /// real muscle shapes (deltoid, bicep, forearm, thigh, calf) that bend smoothly at the shoulders, elbows, hips and
    /// knees because each ring of vertices is weighted between the bones on either side. Clothing is painted on by
    /// region (shirt, sleeves, trousers, bare skin) with a small hem where one ends, and the outfit pieces (belts, armour,
    /// boots, gloves, heads, capes...) still sit on top as separate parts. Pure maths: the preview tool uses it too.
    /// </summary>
    public static class FighterSkin
    {
        const int Around = 28;

        /// <summary>Robots (boxy) keep their mechanical part-built bodies; everyone else gets a skin.</summary>
        public static bool Uses(BodySpec b) => !b.boxy;

        struct Ring
        {
            public Vector3 centre;
            public Quaternion frame;     // +Y along the body, +Z forward, +X to the side
            public float rx, rz;         // half-width and half-depth
            public float square;         // 2 = ellipse, higher = rounded box
            public float pec;            // chest muscle bulge at the front
            public float back;           // shift of the fullest part towards the back (calves)
            public int b0, b1;
            public float w1;
            public float v;              // texture coordinate along the body (metres)
        }

        /// <summary>The bind pose: standing naturally, arms hanging out to the sides (an A-pose), facing +Z.</summary>
        public static BonePose[] BindPose(BodySpec b, float w)
        {
            var bones = new BonePose[FighterPose.BoneCount];
            float h = FighterPose.NaturalHeadHeight(b, w);
            var p = new PoseInput { feet = Vector3.zero, headPos = new Vector3(0f, h, 0f), headRot = Quaternion.identity, fwd = Vector3.forward, leftRot = Quaternion.identity, rightRot = Quaternion.identity };
            p.leftPos = p.rightPos = new Vector3(0f, h * 0.5f, 0.3f);
            FighterPose.Solve(b, w, p, bones);
            // Now hang the hands at full reach, 38 degrees out from the sides.
            var chest = bones[(int)Bone.Chest];
            float reach = (b.upperArm + b.forearm) * w;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = chest.pos + chest.rot * new Vector3(side * b.shoulderW * 0.42f, b.chestH * 0.3f, 0f) * w;
                Vector3 dir = new Vector3(side * Mathf.Sin(38f * Mathf.Deg2Rad), -Mathf.Cos(38f * Mathf.Deg2Rad), 0.06f).normalized;
                Vector3 wristTarget = shoulder + dir * reach * 0.985f;
                Quaternion handRot = Quaternion.LookRotation(dir, Vector3.forward);
                Vector3 hand = wristTarget + handRot * Vector3.forward * (0.085f * w * b.handScale);
                if (side < 0) { p.leftPos = hand; p.leftRot = handRot; } else { p.rightPos = hand; p.rightRot = handRot; }
            }
            FighterPose.Solve(b, w, p, bones);
            return bones;
        }

        public static SkinData Build(FighterStats s, BodySpec b)
        {
            float w = s.scale;
            var d = new SkinData { bind = BindPose(b, w) };
            FighterRecipe.SkinSlots(s, out Slot torso, out Slot pants, out Slot sleeve, out Slot fore, out Slot neck);
            const Slot belt = Slot.Dark;
            Torso(d, b, w, torso, pants, neck, belt);
            for (int side = -1; side <= 1; side += 2)
            {
                Arm(d, b, w, side, sleeve, fore);
                if (b.legs) Leg(d, b, w, side, pants);
            }
            return d;
        }

        // ------------------------------------------------------------------ body regions

        static BonePose B(SkinData d, Bone bone) => d.bind[(int)bone];

        static void Torso(SkinData d, BodySpec b, float w, Slot shirt, Slot pants, Slot neckSlot, Slot belt)
        {
            var hips = B(d, Bone.Hips); var chest = B(d, Bone.Chest); var neck = B(d, Bone.Neck); var head = B(d, Bone.Head);
            float k = b.headScale;
            float neckR = (0.05f * k + 0.008f) * w;
            float yH = hips.pos.y, yC = chest.pos.y, yN = neck.pos.y, yHead = head.pos.y;
            float ch = b.chestH * w;
            // Centre line through the hips, chest, neck and head of the bind pose (follows any lean / hunch).
            Vector3 Spine(float y)
            {
                if (y <= yC) return Vector3.Lerp(hips.pos, chest.pos, Mathf.InverseLerp(yH, yC, y)) + Vector3.up * (y - Mathf.Lerp(yH, yC, Mathf.InverseLerp(yH, yC, y)));
                if (y <= yN) return Vector3.Lerp(chest.pos, neck.pos, Mathf.InverseLerp(yC, yN, y)) + Vector3.up * (y - Mathf.Lerp(yC, yN, Mathf.InverseLerp(yC, yN, y)));
                return Vector3.Lerp(neck.pos, head.pos, Mathf.InverseLerp(yN, yHead, y)) + Vector3.up * (y - Mathf.Lerp(yN, yHead, Mathf.InverseLerp(yN, yHead, y)));
            }
            int H = (int)Bone.Hips, C = (int)Bone.Chest, N = (int)Bone.Neck, Hd = (int)Bone.Head;
            var rings = new List<Ring>();
            var segSlots = new List<Slot>();
            void R(float y, float rx, float rz, float square, int b0, int b1, float w1, Slot slotBelow, float pec = 0f, Quaternion? frame = null)
            {
                var c = Spine(y);
                var f = frame ?? (y < yC ? Quaternion.Slerp(hips.rot, chest.rot, Mathf.InverseLerp(yH, yC, y)) : chest.rot);
                if (rings.Count > 0) segSlots.Add(slotBelow);
                rings.Add(new Ring { centre = c, frame = f, rx = rx, rz = rz, square = square, pec = pec, b0 = b0, b1 = b1, w1 = w1 });
            }
            float hw = b.hipW * w, ww = b.waistW * w, cw = b.chestW * w, cd = b.chestD * w, sw = b.shoulderW * w;
            float waistY = Mathf.Lerp(yH, yC, 0.55f);
            float armR = b.armThick * 0.55f * w;
            // Crotch (closed, between the legs) up through the pelvis.
            R(yH - 0.12f * w, hw * 0.12f, cd * 0.12f, 2f, H, H, 0f, pants);
            R(yH - 0.105f * w, hw * 0.36f, cd * 0.34f, 2.2f, H, H, 0f, pants);
            R(yH - 0.07f * w, hw * 0.5f, cd * 0.44f, 2.4f, H, H, 0f, pants);
            R(yH - 0.01f * w, hw * 0.56f, cd * 0.48f, 2.5f, H, H, 0f, pants);
            R(yH + 0.04f * w, Mathf.Lerp(hw, ww, 0.5f) * 0.54f, cd * 0.47f, 2.5f, H, H, 0f, pants);
            // The belt: a band standing proud of the waist, then the shirt above it.
            float beltX = Mathf.Lerp(hw, ww, 0.55f) * 0.545f * 1.07f, beltZ = cd * 0.475f * 1.07f;
            R(yH + 0.04f * w, beltX, beltZ, 2.5f, H, H, 0f, belt);
            R(yH + 0.1f * w, beltX * 0.995f, beltZ * 0.995f, 2.5f, H, C, 0.15f, belt);
            R(yH + 0.1f * w, Mathf.Lerp(hw, ww, 0.6f) * 0.53f, cd * 0.46f, 2.5f, H, C, 0.15f, belt);
            R(waistY, ww * 0.5f, cd * 0.42f, 2.6f, H, C, 0.5f, shirt);
            R(yC - ch * 0.3f, cw * 0.47f, cd * 0.45f, 2.7f, C, H, 0.15f, shirt);
            R(yC - ch * 0.08f, cw * 0.51f, cd * 0.48f, 2.8f, C, C, 0f, shirt, 0.25f);
            R(yC + ch * 0.12f, Mathf.Lerp(cw, sw, 0.4f) * 0.53f, cd * 0.48f, 2.9f, C, C, 0f, shirt, 0.5f);
            // Shoulder line: as wide as the shoulder joints plus the top of the arm, then rounding over the top.
            R(yC + ch * 0.27f, sw * 0.42f + armR * 0.55f, cd * 0.43f, 3.2f, C, C, 0f, shirt, 0.15f);
            R(yC + ch * 0.38f, sw * 0.36f, cd * 0.36f, 3f, C, C, 0f, shirt);
            R(yC + ch * 0.47f, sw * 0.24f, cd * 0.27f, 2.6f, C, N, 0.15f, shirt);
            // Trapezius sloping into the neck (neckline hem here).
            R(yN - 0.005f * w, neckR * 1.5f, neckR * 1.25f, 2.2f, C, N, 0.45f, shirt);
            R(yN - 0.005f * w, neckR * 1.32f, neckR * 1.15f, 2.1f, C, N, 0.45f, neckSlot);
            R(yN + 0.035f * w, neckR * 1.02f, neckR * 0.98f, 2f, N, C, 0.15f, neckSlot);
            R(yN + 0.075f * w, neckR, neckR * 0.95f, 2f, N, Hd, 0.3f, neckSlot, 0f, Quaternion.Slerp(chest.rot, head.rot, 0.5f));
            R(yHead - 0.05f * k * w, neckR * 0.98f, neckR * 0.92f, 2f, Hd, N, 0.2f, neckSlot, 0f, head.rot);
            R(yHead - 0.01f * k * w, neckR * 0.5f, neckR * 0.5f, 2f, Hd, Hd, 0f, neckSlot, 0f, head.rot);
            Emit(d, rings, segSlots, true, true);
        }

        static void Arm(SkinData d, BodySpec b, float w, int side, Slot sleeve, Slot fore)
        {
            bool left = side < 0;
            var up = B(d, left ? Bone.UpperArmL : Bone.UpperArmR);
            var lo = B(d, left ? Bone.ForearmL : Bone.ForearmR);
            var hand = B(d, left ? Bone.HandL : Bone.HandR);
            int U = (int)(left ? Bone.UpperArmL : Bone.UpperArmR), F = (int)(left ? Bone.ForearmL : Bone.ForearmR), C = (int)Bone.Chest;
            Vector3 shoulder = up.pos, elbow = lo.pos;
            Vector3 wrist = hand.pos - hand.rot * Vector3.forward * (0.085f * w * b.handScale);
            float R = b.armThick * 0.55f * w;
            Vector3 upDir = (elbow - shoulder).normalized, loDir = (wrist - elbow).normalized;
            float upLen = Vector3.Distance(shoulder, elbow), loLen = Vector3.Distance(elbow, wrist);
            var rings = new List<Ring>();
            var segSlots = new List<Slot>();
            void Up(float s, float r, int b0, int b1, float w1, Slot slot, float flat = 0.92f)
            {
                if (rings.Count > 0) segSlots.Add(slot);
                rings.Add(new Ring { centre = shoulder + upDir * (s * upLen), frame = up.rot, rx = r, rz = r * flat, square = 2f, b0 = b0, b1 = b1, w1 = w1 });
            }
            void Lo(float s, float r, int b0, int b1, float w1, Slot slot, float flat = 0.88f)
            {
                if (rings.Count > 0) segSlots.Add(slot);
                rings.Add(new Ring { centre = elbow + loDir * (s * loLen), frame = lo.rot, rx = r, rz = r * flat, square = 2.1f, b0 = b0, b1 = b1, w1 = w1 });
            }
            // A rounded cap hidden in the shoulder, the deltoid, the bicep, the elbow.
            Up(-0.2f, R * 0.25f, U, C, 0.5f, sleeve);
            Up(-0.13f, R * 0.8f, U, C, 0.45f, sleeve);
            Up(-0.03f, R * 1.08f, U, C, 0.35f, sleeve);
            Up(0.12f, R * 1.12f, U, C, 0.12f, sleeve);
            Up(0.32f, R * 1.02f, U, U, 0f, sleeve);
            Up(0.55f, R * 1.04f, U, U, 0f, sleeve, 1f);
            if (sleeve != fore)
            {
                // A sleeve that ends above the elbow (short sleeves): hem, then the arm underneath.
                Up(0.68f, R * 1.03f, U, U, 0f, sleeve);
                Up(0.68f, R * 0.95f, U, U, 0f, fore);
                Up(0.8f, R * 0.9f, U, F, 0.12f, fore);
            }
            else Up(0.8f, R * 0.92f, U, F, 0.12f, sleeve);
            Up(0.95f, R * 0.84f, U, F, 0.38f, fore);
            Lo(0.06f, R * 0.86f, F, U, 0.38f, fore);
            Lo(0.22f, R * 0.98f, F, U, 0.1f, fore, 0.85f);
            Lo(0.42f, R * 0.92f, F, F, 0f, fore);
            Lo(0.68f, R * 0.76f, F, F, 0f, fore);
            Lo(0.92f, R * 0.64f, F, F, 0f, fore, 0.8f);
            Lo(1.04f, R * 0.5f, F, F, 0f, fore, 0.8f);
            Lo(1.09f, R * 0.15f, F, F, 0f, fore);
            Emit(d, rings, segSlots, true, true);
        }

        static void Leg(SkinData d, BodySpec b, float w, int side, Slot pants)
        {
            bool left = side < 0;
            var th = B(d, left ? Bone.ThighL : Bone.ThighR);
            var sh = B(d, left ? Bone.ShinL : Bone.ShinR);
            var ft = B(d, left ? Bone.FootL : Bone.FootR);
            int T = (int)(left ? Bone.ThighL : Bone.ThighR), S = (int)(left ? Bone.ShinL : Bone.ShinR), H = (int)Bone.Hips;
            Vector3 hip = th.pos, knee = sh.pos, ankle = ft.pos;
            float R = b.legThick * 0.575f * w;
            Vector3 tDir = (knee - hip).normalized, sDir = (ankle - knee).normalized;
            float tLen = Vector3.Distance(hip, knee), sLen = Vector3.Distance(knee, ankle);
            var rings = new List<Ring>();
            var segSlots = new List<Slot>();
            void Th(float s, float r, int b0, int b1, float w1, float flat = 0.95f, float back = 0f)
            {
                if (rings.Count > 0) segSlots.Add(pants);
                // The top of the thigh leans in under the pelvis a little.
                Vector3 c = hip + tDir * (s * tLen) + th.rot * Vector3.right * (-side * Mathf.Max(0f, 0.15f - s) * R * 0.6f);
                rings.Add(new Ring { centre = c, frame = th.rot, rx = r, rz = r * flat, square = 2f, back = back, b0 = b0, b1 = b1, w1 = w1 });
            }
            void Sh(float s, float r, int b0, int b1, float w1, float flat = 0.9f, float back = 0f)
            {
                segSlots.Add(pants);
                rings.Add(new Ring { centre = knee + sDir * (s * sLen), frame = sh.rot, rx = r, rz = r * flat, square = 2f, back = back, b0 = b0, b1 = b1, w1 = w1 });
            }
            // A cap hidden in the pelvis, the full upper thigh, tapering to the knee.
            Th(-0.22f, R * 0.3f, T, H, 0.5f);
            Th(-0.13f, R * 0.88f, T, H, 0.45f);
            Th(-0.03f, R * 1.06f, T, H, 0.3f);
            Th(0.12f, R * 1.08f, T, H, 0.1f);
            Th(0.38f, R * 1f, T, T, 0f);
            Th(0.65f, R * 0.88f, T, T, 0f);
            Th(0.86f, R * 0.76f, T, S, 0.15f);
            Th(0.97f, R * 0.72f, T, S, 0.4f, 0.98f);
            // The calf bulges at the back, then the ankle (inside the boot on most fighters).
            Sh(0.06f, R * 0.72f, S, T, 0.4f, 0.98f);
            Sh(0.2f, R * 0.78f, S, T, 0.1f, 0.95f, 0.12f);
            Sh(0.36f, R * 0.8f, S, S, 0f, 0.95f, 0.16f);
            Sh(0.58f, R * 0.66f, S, S, 0f, 0.9f, 0.08f);
            Sh(0.82f, R * 0.52f, S, S, 0f);
            Sh(1f, R * 0.48f, S, S, 0f);
            Sh(1.06f, R * 0.3f, S, S, 0f);
            Sh(1.1f, R * 0.05f, S, S, 0f);
            Emit(d, rings, segSlots, true, true);
        }

        // ------------------------------------------------------------------ mesh building

        /// <summary>
        /// Turns rings into vertices and triangles. Each run of segments with the same slot becomes its own strip (the
        /// ring where a slot changes is doubled), so clothing edges are crisp. Ends are closed with a point.
        /// </summary>
        static void Emit(SkinData d, List<Ring> rings, List<Slot> segSlots, bool capStart, bool capEnd)
        {
            int n = rings.Count;
            // Texture coordinate along the body: metres from the first ring.
            float vAcc = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) vAcc += Vector3.Distance(rings[i].centre, rings[i - 1].centre);
                var r = rings[i];
                r.v = vAcc;
                rings[i] = r;
            }
            var pos = new Vector3[n, Around];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < Around; j++) pos[i, j] = RingPoint(rings[i], j);
            var nrm = new Vector3[n, Around];
            var tan = new Vector3[n, Around];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < Around; j++)
                {
                    Vector3 du = pos[i, (j + 1) % Around] - pos[i, (j + Around - 1) % Around];
                    // Along the body: skip doubled rings (same place) so hems don't break the shading.
                    int ia = i, ib = i;
                    do ia = Mathf.Max(0, ia - 1); while (ia > 0 && (rings[ia].centre - rings[i].centre).sqrMagnitude < 1e-8f);
                    do ib = Mathf.Min(n - 1, ib + 1); while (ib < n - 1 && (rings[ib].centre - rings[i].centre).sqrMagnitude < 1e-8f);
                    Vector3 dv = pos[ib, j] - pos[ia, j];
                    Vector3 nn = -Vector3.Cross(du, dv);
                    if (nn.sqrMagnitude < 1e-12f) nn = rings[i].frame * new Vector3(Mathf.Cos(Angle(j)), 0f, Mathf.Sin(Angle(j)));
                    nrm[i, j] = nn.normalized;
                    tan[i, j] = du.sqrMagnitude > 1e-12f ? du.normalized : rings[i].frame * Vector3.right;
                }

            // Strips: runs of segments with the same slot.
            int a = 0;
            while (a < n - 1)
            {
                int e = a;
                while (e + 1 < n - 1 && segSlots[e + 1] == segSlots[a]) e++;
                var tris = d.TrianglesFor(segSlots[a]);
                int first = d.vertices.Count;
                for (int i = a; i <= e + 1; i++)
                    for (int j = 0; j <= Around; j++)
                    {
                        int jj = j % Around;
                        var r = rings[i];
                        d.vertices.Add(pos[i, jj]);
                        d.normals.Add(nrm[i, jj]);
                        d.tangents.Add(new Vector4(tan[i, jj].x, tan[i, jj].y, tan[i, jj].z, 1f));
                        // Around: metres of a typical limb so the cloth texture is the same scale everywhere.
                        d.uvs.Add(new Vector2((float)j / Around * 0.6f, r.v));
                        d.bone0.Add(r.b0);
                        d.bone1.Add(r.b1);
                        d.weight1.Add(r.b0 == r.b1 ? 0f : r.w1);
                    }
                int W = Around + 1;
                for (int i = 0; i <= e - a; i++)
                    for (int j = 0; j < Around; j++)
                    {
                        int v0 = first + i * W + j, v1 = v0 + 1, v2 = v0 + W + 1, v3 = v0 + W;
                        tris.Add(v0); tris.Add(v3); tris.Add(v2);
                        tris.Add(v0); tris.Add(v2); tris.Add(v1);
                    }
                a = e + 1;
            }
            if (capStart) Cap(d, rings[0], pos, nrm, 0, segSlots[0], false);
            if (capEnd) Cap(d, rings[n - 1], pos, nrm, n - 1, segSlots[n - 2], true);
        }

        static void Cap(SkinData d, Ring r, Vector3[,] pos, Vector3[,] nrm, int i, Slot slot, bool end)
        {
            var tris = d.TrianglesFor(slot);
            Vector3 axis = r.frame * Vector3.up * (end ? 1f : -1f);
            int centre = d.vertices.Count;
            d.vertices.Add(r.centre + axis * Mathf.Min(r.rx, r.rz) * 0.4f);
            d.normals.Add(axis);
            d.tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            d.uvs.Add(new Vector2(0f, r.v));
            d.bone0.Add(r.b0); d.bone1.Add(r.b1); d.weight1.Add(r.b0 == r.b1 ? 0f : r.w1);
            int first = d.vertices.Count;
            for (int j = 0; j < Around; j++)
            {
                d.vertices.Add(pos[i, j]);
                d.normals.Add(nrm[i, j]);
                d.tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                d.uvs.Add(new Vector2((float)j / Around * 0.6f, r.v));
                d.bone0.Add(r.b0); d.bone1.Add(r.b1); d.weight1.Add(r.b0 == r.b1 ? 0f : r.w1);
            }
            for (int j = 0; j < Around; j++)
            {
                int v0 = first + j, v1 = first + (j + 1) % Around;
                // Clockwise seen from outside: from below for the start, from above for the end.
                if (end) { tris.Add(centre); tris.Add(v1); tris.Add(v0); }
                else { tris.Add(centre); tris.Add(v0); tris.Add(v1); }
            }
        }

        static float Angle(int j) => j * Mathf.PI * 2f / Around;

        /// <summary>A point of a ring: a superellipse in the ring's frame, with the chest and calf shaping.</summary>
        static Vector3 RingPoint(Ring r, int j)
        {
            float a = Angle(j);
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float e = 2f / r.square;
            float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * r.rx;
            float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * r.rz;
            if (r.pec > 0f && z > 0f)
            {
                // Two pecs either side of the breastbone: fuller at about 35% out from the middle, flat in the centre.
                float side = Mathf.Abs(x) / Mathf.Max(0.001f, r.rx);
                float pec = Mathf.Exp(-Mathf.Pow((side - 0.38f) / 0.24f, 2f));
                z += r.pec * pec * r.rz * 0.16f * s;
            }
            if (r.back > 0f && z < 0f) z *= 1f + r.back;
            return r.centre + r.frame * new Vector3(x, 0f, z);
        }

        // ------------------------------------------------------------------ skinning (for previews / CPU checks)

        /// <summary>Where a vertex of the skin ends up for a posed skeleton (linear blend skinning, like the GPU).</summary>
        public static Vector3 Deform(SkinData d, int i, BonePose[] posed)
        {
            Vector3 v = d.vertices[i];
            Vector3 p0 = Move(v, d.bind[d.bone0[i]], posed[d.bone0[i]]);
            float w1 = d.weight1[i];
            if (w1 <= 0f) return p0;
            Vector3 p1 = Move(v, d.bind[d.bone1[i]], posed[d.bone1[i]]);
            return Vector3.Lerp(p0, p1, w1);
        }

        static Vector3 Move(Vector3 v, BonePose bind, BonePose posed)
        {
            Vector3 local = Quaternion.Inverse(bind.rot) * (v - bind.pos);
            local = new Vector3(local.x / bind.scale.x, local.y / bind.scale.y, local.z / bind.scale.z);
            return posed.pos + posed.rot * new Vector3(local.x * posed.scale.x, local.y * posed.scale.y, local.z * posed.scale.z);
        }
    }
}
