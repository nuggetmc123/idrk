using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// A fighter's body in the game: builds the design from <see cref="FighterRecipe"/> onto a skeleton of bones and
    /// poses it every frame with <see cref="FighterPose"/> (bending elbows and knees, walking, K.O. and the ragdoll
    /// tumble). Used for fighters in the arena, the menu preview and crate reveals.
    /// </summary>
    public class FighterModel
    {
        public Transform root, head, torso, hips, leftGlove, rightGlove, shield;
        public readonly List<Renderer> gloveRenderers = new List<Renderer>();
        public Material main, accent, glow, gloveGlow, glove;
        public FighterStats stats;
        public BodySpec body;
        /// <summary>Extra size on the hands (the glove pulse when empowered).</summary>
        public float handPulse = 1f;

        readonly Transform[] bones = new Transform[FighterPose.BoneCount];
        readonly BonePose[] solved = new BonePose[FighterPose.BoneCount];
        struct Extra { public Transform t; public Anim anim; public Vector3 basePos, baseScale; public Quaternion baseRot; public float phase; }
        readonly List<Extra> extras = new List<Extra>();
        float walkPhase;

        public static Material SlotMaterial(Slot slot, FighterStats s, bool silhouette, Dictionary<Slot, Material> cache)
        {
            if (cache.TryGetValue(slot, out var m)) return m;
            if (silhouette) m = NBUI.Mat(new Color(0.05f, 0.05f, 0.07f), 0.2f);
            else
            {
                Color c = FighterRecipe.SlotColor(slot, s, out float smooth, out float metal, out float glowAmount);
                m = glowAmount > 0f ? NBUI.Mat(c, smooth, metal, c * glowAmount) : NBUI.Mat(c, smooth, metal);
                m = FighterSurface.Apply(m, slot, s); // cloth, leather, metal and skin detail
            }
            cache[slot] = m;
            return m;
        }

        static PrimitiveType Prim(Shape s) => s switch
        {
            Shape.Cube => PrimitiveType.Cube, Shape.Sphere => PrimitiveType.Sphere, Shape.Cylinder => PrimitiveType.Cylinder, _ => PrimitiveType.Capsule,
        };

        /// <summary>Build the model under parent. Silhouette = all dark (a locked fighter in the menu).</summary>
        public static FighterModel Build(Transform parent, FighterStats s, bool silhouette = false)
        {
            var m = new FighterModel { stats = s, body = FighterRecipe.Body(s) };
            var mats = new Dictionary<Slot, Material>();
            m.main = SlotMaterial(Slot.Main, s, silhouette, mats);
            m.accent = SlotMaterial(Slot.Accent, s, silhouette, mats);
            m.glow = SlotMaterial(Slot.Eye, s, silhouette, mats);
            m.glove = SlotMaterial(Slot.Glove, s, silhouette, mats);
            m.gloveGlow = silhouette ? m.main : NBUI.Glow(Color.Lerp(s.main, Color.white, 0.4f));

            m.root = new GameObject("Model").transform;
            m.root.SetParent(parent, false);
            for (int i = 0; i < FighterPose.BoneCount; i++)
            {
                var b = new GameObject(((Bone)i).ToString()).transform;
                b.SetParent(m.root, false);
                m.bones[i] = b;
            }
            m.head = m.bones[(int)Bone.Head];
            m.torso = m.bones[(int)Bone.Chest];
            m.hips = m.bones[(int)Bone.Hips];
            m.leftGlove = m.bones[(int)Bone.HandL];
            m.rightGlove = m.bones[(int)Bone.HandR];

            var pivots = new Dictionary<string, Transform>();
            foreach (var p in FighterRecipe.Parts(s))
            {
                if (!m.body.legs && p.bone >= Bone.ThighL) continue;
                Transform bone = m.bones[(int)p.bone];
                Transform parentT = bone;
                Vector3 pos = p.pos;
                if (p.anim == Anim.Sway || p.anim == Anim.Flap || p.anim == Anim.Swing)
                {
                    // Swinging parts hang from a pivot (capes from the shoulders, wings from the spine...).
                    Vector3 pivot = FighterRecipe.PivotOf(p);
                    string key = p.bone + "/" + p.anim + "/" + pivot.ToString("F4");
                    if (!pivots.TryGetValue(key, out parentT))
                    {
                        parentT = new GameObject(p.name + "Pivot").transform;
                        parentT.SetParent(bone, false);
                        parentT.localPosition = pivot;
                        pivots[key] = parentT;
                        m.AddExtra(parentT, p.anim, p.phase);
                    }
                    pos -= pivot;
                }
                var t = NBUI.Part(parentT, Prim(p.shape), pos, p.scale, SlotMaterial(p.slot, s, silhouette, mats), p.euler, p.name);
                if (p.shape == Shape.Limb || p.shape == Shape.Torso) t.GetComponent<MeshFilter>().sharedMesh = ShapeMesh(p.shape);
                if (p.useRotation) t.localRotation = p.rotation;
                if (p.slot == Slot.Glove) m.gloveRenderers.Add(t.GetComponent<Renderer>());
                if (p.name == "Finger") (p.bone == Bone.HandL ? m.fingersL : m.fingersR).Add(t);
                if (p.anim == Anim.Orbit || p.anim == Anim.Flicker || p.anim == Anim.Thrust) m.AddExtra(t, p.anim, p.phase);
            }

            m.shield = NBUI.Part(m.root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.55f, 0.01f, 0.55f), NBUI.Glow(new Color(0.4f, 0.75f, 1f)), default, "Shield");
            m.shield.gameObject.SetActive(false);
            return m;
        }

        static Mesh limbMesh, torsoMesh;

        /// <summary>The shared tapered-limb / V-torso meshes (FighterShapes), made once.</summary>
        static Mesh ShapeMesh(Shape shape)
        {
            ref Mesh m = ref shape == Shape.Torso ? ref torsoMesh : ref limbMesh;
            if (m != null) return m;
            var d = shape == Shape.Torso ? FighterShapes.Torso() : FighterShapes.Limb();
            m = new Mesh { name = shape.ToString() };
            m.SetVertices(d.vertices);
            m.SetNormals(d.normals);
            m.SetTangents(d.tangents);
            m.SetUVs(0, d.uvs);
            m.SetTriangles(d.triangles, 0);
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        void AddExtra(Transform t, Anim anim, float phase)
        {
            extras.Add(new Extra { t = t, anim = anim, basePos = t.localPosition, baseScale = t.localScale, baseRot = t.localRotation, phase = phase });
        }

        public Transform BoneOf(Bone b) => bones[(int)b];

        readonly List<Transform> fingersL = new List<Transform>(), fingersR = new List<Transform>();
        float curlL = 0.9f, curlR = 0.9f;
        static readonly List<PartSpec> fingerScratch = new List<PartSpec>();

        /// <summary>Open or close the hands (0 = open, 1 = fist), e.g. from the controller grips.</summary>
        public void SetCurl(float left, float right)
        {
            if (Mathf.Abs(left - curlL) > 0.02f) { curlL = left; ApplyCurl(fingersL, Bone.HandL, -1f, left); }
            if (Mathf.Abs(right - curlR) > 0.02f) { curlR = right; ApplyCurl(fingersR, Bone.HandR, 1f, right); }
        }

        void ApplyCurl(List<Transform> fingers, Bone hand, float side, float curl)
        {
            fingerScratch.Clear();
            FighterRecipe.Fingers(fingerScratch, hand, side, stats, body, curl);
            for (int i = 0; i < fingers.Count && i < fingerScratch.Count; i++)
            {
                if (fingers[i] == null) continue;
                fingers[i].localPosition = fingerScratch[i].pos;
                fingers[i].localRotation = fingerScratch[i].rotation;
            }
        }
        public float Scale => stats.scale;

        /// <summary>Show or hide the whole model (invisible moderators, K.O.).</summary>
        public void SetVisible(bool visible)
        {
            if (!visible) ragdoll.End();
            if (root != null) root.gameObject.SetActive(visible);
        }

        /// <summary>Remove the ragdoll's bodies (call when the model goes away).</summary>
        public void Dispose() => ragdoll.End();

        // ---------------- physics: ragdoll and hit flinches

        readonly FighterRagdoll ragdoll = new FighterRagdoll();
        readonly Vector3[] blendPos = new Vector3[FighterPose.BoneCount];
        readonly Quaternion[] blendRot = new Quaternion[FighterPose.BoneCount];
        float blendLeft;
        const float GetUpTime = 0.4f;
        Vector3 flinch, flinchVel;

        public bool Ragdolled => ragdoll.Active;

        /// <summary>Go limp: the body becomes a physics ragdoll thrown with `velocity`, the chest shoved by `push`.</summary>
        public void StartRagdoll(Vector3 velocity, Vector3 push, IEnumerable<Collider> ignore)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return;
            ragdoll.Begin(bones, body, stats.scale, velocity, push, ignore);
            blendLeft = 0f;
            flinch = flinchVel = Vector3.zero;
        }

        /// <summary>Get up: the posed body takes over again, blending from where the ragdoll lay (unless instant).</summary>
        public void StopRagdoll(bool instant)
        {
            if (!ragdoll.Active) return;
            ragdoll.Apply(bones);
            for (int i = 0; i < bones.Length; i++) { blendPos[i] = bones[i].position; blendRot[i] = bones[i].rotation; }
            blendLeft = instant ? 0f : GetUpTime;
            ragdoll.End();
        }

        /// <summary>
        /// A hit landing: the head and guard snap back along `dir` and spring back (the spine follows the head), or,
        /// on a ragdoll, the nearest body part gets knocked. strength ~ metres per second of snap.
        /// </summary>
        public void Flinch(Vector3 point, Vector3 dir, float strength)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            if (ragdoll.Active) { ragdoll.Push(point, dir * strength * 2f); return; }
            flinchVel += dir * Mathf.Min(strength, 3f);
        }

        /// <summary>Pose the whole body for this frame (see <see cref="PoseInput"/>).</summary>
        public void Pose(PoseInput p, float dt)
        {
            walkPhase += p.speed * dt * 3.2f / Mathf.Max(0.6f, stats.scale);
            p.walkPhase = walkPhase;
            p.time = Time.time;

            if (ragdoll.Active)
            {
                ragdoll.Follow(p.feet, p.velocity, p.down);
                ragdoll.Apply(bones);
                shield.gameObject.SetActive(false);
                AnimateExtras(p.speed, true);
                return;
            }

            // Hit flinch: a bouncy spring on the head (and the guard with it).
            if (flinch.sqrMagnitude > 1e-6f || flinchVel.sqrMagnitude > 1e-6f)
            {
                flinchVel += (-flinch * 140f - flinchVel * 11f) * dt;
                flinch = Vector3.ClampMagnitude(flinch + flinchVel * dt, 0.3f * stats.scale);
                if (!p.down && !p.idle)
                {
                    p.headPos += flinch;
                    Vector3 tiltAxis = Vector3.Cross(Vector3.up, flinch);
                    if (tiltAxis.sqrMagnitude > 1e-8f) p.headRot = Quaternion.AngleAxis(flinch.magnitude / stats.scale * 160f, tiltAxis.normalized) * p.headRot;
                    p.leftPos += flinch * 0.6f;
                    p.rightPos += flinch * 0.6f;
                }
            }

            FighterPose.Solve(body, stats.scale, p, solved);
            float blend = blendLeft > 0f ? Mathf.SmoothStep(0f, 1f, 1f - blendLeft / GetUpTime) : 1f;
            blendLeft = Mathf.Max(0f, blendLeft - dt);
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;
                // First-person gloves ride your real hands, so they're not posed here.
                if ((i == (int)Bone.HandL || i == (int)Bone.HandR) && b.parent != root) continue;
                if (blend < 1f) b.SetPositionAndRotation(Vector3.Lerp(blendPos[i], solved[i].pos, blend), Quaternion.Slerp(blendRot[i], solved[i].rot, blend));
                else b.SetPositionAndRotation(solved[i].pos, solved[i].rot);
                b.localScale = solved[i].scale * (i == (int)Bone.HandL || i == (int)Bone.HandR ? handPulse : 1f);
            }

            bool showShield = p.blocking && !p.down && p.tumble < 0.5f;
            shield.gameObject.SetActive(showShield);
            if (showShield)
            {
                shield.position = (p.leftPos + p.rightPos) * 0.5f + p.fwd * 0.08f;
                shield.rotation = Quaternion.LookRotation(p.fwd) * Quaternion.Euler(90f, 0f, 0f);
            }
            AnimateExtras(p.speed, p.airborne || p.tumble > 0.5f);
        }

        void AnimateExtras(float speed, bool airborne)
        {
            float t = Time.time;
            foreach (var e in extras)
            {
                if (e.t == null) continue;
                switch (e.anim)
                {
                    case Anim.Sway:
                        e.t.localRotation = e.baseRot * Quaternion.Euler(Mathf.Sin(t * 2.2f + e.phase) * 5f + Mathf.Clamp(speed * 7f, 0f, 45f) + (airborne ? 25f : 0f), 0f, Mathf.Sin(t * 1.7f + e.phase) * 3f);
                        break;
                    case Anim.Flap:
                        float f = Mathf.Sin(t * (airborne ? 12f : 2.5f)) * (airborne ? 28f : 7f);
                        e.t.localRotation = e.baseRot * Quaternion.Euler(0f, -Mathf.Sign(e.phase) * f * 0.6f, Mathf.Sign(e.phase) * f);
                        break;
                    case Anim.Swing:
                        e.t.localRotation = e.baseRot * Quaternion.Euler(Mathf.Sin(t * 2.3f) * 8f, Mathf.Sin(t * 3f) * 22f, 0f);
                        break;
                    case Anim.Orbit:
                        float a = t * 2f + e.phase;
                        e.t.localPosition = new Vector3(Mathf.Cos(a) * 0.45f, 0.15f + Mathf.Sin(a * 2f) * 0.1f, Mathf.Sin(a) * 0.45f + 0.12f);
                        break;
                    case Anim.Flicker:
                        float k = 0.85f + 0.25f * Mathf.Sin(t * 13f + e.phase * 2f);
                        e.t.localScale = e.baseScale.y < 0.02f
                            ? new Vector3(e.baseScale.x * (0.95f + 0.05f * k), e.baseScale.y, e.baseScale.z * (0.95f + 0.05f * k))
                            : new Vector3(e.baseScale.x, e.baseScale.y * k, e.baseScale.z);
                        break;
                    case Anim.Thrust:
                        float th = (airborne ? 1.8f : 1f) * (0.8f + 0.3f * Mathf.Sin(t * 30f + e.phase));
                        e.t.localScale = new Vector3(e.baseScale.x, e.baseScale.y * th, e.baseScale.z);
                        break;
                }
            }
        }

        /// <summary>A standing pose for previews (menu, crate reveal): guard up, facing +Z of the spot.</summary>
        public void PoseIdle(Transform at, float time)
        {
            Pose(new PoseInput { feet = at.position, fwd = at.forward, headRot = at.rotation, leftRot = at.rotation, rightRot = at.rotation, idle = true }, Time.deltaTime);
        }
    }
}
