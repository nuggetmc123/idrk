using UnityEngine;

namespace Nullbound
{
    /// <summary>Where one bone of a fighter goes (world space).</summary>
    public struct BonePose
    {
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    /// <summary>What drives a fighter's pose this frame (from the network, a bot, or a menu preview).</summary>
    public struct PoseInput
    {
        public Vector3 feet, headPos, leftPos, rightPos, fwd, velocity;
        public Quaternion headRot, leftRot, rightRot;
        public float speed, walkPhase, time;
        public bool down, airborne, blocking;
        /// <summary>Preview pose: stand at natural height with the guard up (ignores head and hand inputs).</summary>
        public bool idle;
        /// <summary>0..1 how much of the ragdoll tumble to show (blends in and out).</summary>
        public float tumble;
        /// <summary>The roll of the tumbling body.</summary>
        public Quaternion tumbleRot;
    }

    /// <summary>
    /// Solves a fighter's skeleton: the head and hands go exactly where the player's are, the spine hangs from the
    /// head, elbows and knees bend with two-bone IK, feet step as you move, and K.O.s and super hits throw the whole
    /// body into a lying or tumbling (ragdoll) pose. Pure maths, so it also runs outside the game for previews.
    /// </summary>
    public static class FighterPose
    {
        public const int BoneCount = 17;

        /// <summary>How high a fighter's head sits when standing relaxed (feet to head centre).</summary>
        public static float NaturalHeadHeight(BodySpec b, float w)
        {
            float legs = b.legs ? 0.09f + (b.thigh + b.shin) * 0.97f + 0.05f : 0.75f;
            return (legs + b.chestH * 0.75f + 0.06f + b.chestH * 0.55f + 0.02f + 0.13f * b.headScale) * w;
        }

        public static void Solve(BodySpec b, float w, PoseInput p, BonePose[] bones)
        {
            Vector3 fwd = p.fwd;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(fwd, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 up = Vector3.up;
            float k = b.headScale;
            float natural = NaturalHeadHeight(b, w);

            // Ragdoll poses (K.O. and tumbling) are built standing at natural height, then rotated as one body.
            bool ragdoll = p.down || p.tumble > 0.001f || p.idle;
            Vector3 headPos = p.headPos;
            Quaternion headRot = p.headRot;
            if (ragdoll)
            {
                headPos = p.feet + up * natural + up * (p.idle ? Mathf.Sin(p.time * 2f) * 0.012f : 0f);
                headRot = yaw;
            }

            // ---- spine: hangs from the head, hips pulled partly back over the feet
            Vector3 neckBase = headPos - up * (0.13f * k * w) - fwd * 0.02f * w;
            float lean = b.hunch * 35f + Mathf.Clamp(p.speed * 2.2f, 0f, 12f);
            Vector3 chestUpGuess = Quaternion.AngleAxis(lean, right) * up;
            Vector3 chest = neckBase - chestUpGuess * ((b.chestH * 0.55f + 0.02f) * w);
            Vector3 hips = chest - up * ((b.chestH * 0.75f + 0.06f) * w);
            if (!ragdoll)
            {
                Vector3 overFeet = new Vector3(p.feet.x, hips.y, p.feet.z) - fwd * 0.03f * w;
                hips = Vector3.Lerp(hips, overFeet, 0.4f);
                // Never let the hips sink into the floor (crouching low / giants).
                float minHip = p.feet.y + (b.legs ? (b.thigh + b.shin) * 0.35f : 0.3f) * w;
                if (hips.y < minHip) hips.y = minHip;
            }
            Vector3 spineUp = (neckBase - hips).normalized;
            Quaternion chestRot = Frame(Vector3.Lerp(spineUp, chestUpGuess, 0.5f).normalized, fwd);
            Quaternion hipRot = Frame(Vector3.Lerp(up, spineUp, 0.3f).normalized, fwd);

            Set(bones, Bone.Head, headPos, headRot, w);
            Set(bones, Bone.Neck, neckBase - up * 0.03f * w, chestRot, w);
            Set(bones, Bone.Chest, chest, chestRot, w);
            Set(bones, Bone.Hips, hips, hipRot, w);
            Set(bones, Bone.Back, chest + chestRot * new Vector3(0f, b.chestH * 0.3f, -b.chestD * 0.5f) * w, chestRot, w);

            // ---- arms
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                Vector3 shoulder = chest + chestRot * new Vector3(side * b.shoulderW * 0.42f, b.chestH * 0.3f, 0f) * w;
                Vector3 handPos = left ? p.leftPos : p.rightPos;
                Quaternion handRot = left ? p.leftRot : p.rightRot;
                float reach = (b.upperArm + b.forearm) * w;
                if (ragdoll)
                {
                    if (p.idle)
                    {
                        // Fighting guard.
                        handPos = shoulder + fwd * reach * 0.6f - up * reach * 0.2f + right * side * 0.05f * w;
                        handRot = yaw * Quaternion.Euler(-10f, side * 8f, 0f);
                    }
                    else
                    {
                        // Limp, flung out to the sides; flailing while tumbling.
                        float flail = p.tumble * Mathf.Sin(p.time * 9f + side * 1.3f);
                        Vector3 outDir = (right * side * 0.85f + up * (0.25f + flail * 0.5f) - fwd * (0.15f + flail * 0.3f)).normalized;
                        handPos = shoulder + outDir * reach * 0.9f;
                        handRot = Quaternion.LookRotation(outDir, up);
                    }
                }
                Vector3 wrist = handPos - handRot * Vector3.forward * (0.085f * w * b.handScale);
                Vector3 elbowHint = -up + right * side * 0.6f - fwd * 0.4f;
                Limb(bones, left ? Bone.UpperArmL : Bone.UpperArmR, left ? Bone.ForearmL : Bone.ForearmR,
                    shoulder, wrist, b.upperArm * w, b.forearm * w, elbowHint, fwd, w, 1.45f);
                Set(bones, left ? Bone.HandL : Bone.HandR, handPos, handRot, w);
            }

            // ---- legs
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                Vector3 hipJoint = hips + hipRot * new Vector3(side * b.hipW * 0.32f, -0.05f, 0f) * w;
                Vector3 ankle;
                Quaternion footRot = yaw;
                float legLen = (b.thigh + b.shin) * w;
                if (!b.legs)
                {
                    ankle = hipJoint - up * legLen; // unused (spirits have no legs) but keeps the bones sane
                }
                else if (ragdoll)
                {
                    if (p.idle)
                    {
                        ankle = new Vector3(hipJoint.x, p.feet.y + 0.09f * w, hipJoint.z) + right * side * 0.05f * w + fwd * (left ? 0.06f : -0.08f) * w;
                        footRot = yaw * Quaternion.Euler(0f, side * 12f, 0f);
                    }
                    else
                    {
                        float kick = p.tumble * Mathf.Sin(p.time * 7f + side * 2f);
                        ankle = hipJoint + (-up * 0.9f + right * side * 0.3f + fwd * (0.15f + kick * 0.4f)).normalized * legLen * 0.95f;
                        footRot = yaw * Quaternion.Euler(30f, 0f, 0f);
                    }
                }
                else if (p.airborne)
                {
                    ankle = hipJoint - up * legLen * 0.72f + fwd * (left ? 0.12f : -0.05f) * w;
                    footRot = yaw * Quaternion.Euler(25f, 0f, 0f);
                }
                else
                {
                    // Walk cycle: feet swing along the movement, lifting on the way forward.
                    Vector3 vel = p.velocity;
                    vel.y = 0f;
                    Vector3 moveDir = vel.sqrMagnitude > 0.01f ? vel.normalized : fwd;
                    float phase = p.walkPhase + (left ? 0f : Mathf.PI);
                    float stride = Mathf.Clamp01(p.speed / 4f) * 0.3f * w;
                    Vector3 basePos = new Vector3(hipJoint.x, p.feet.y, hipJoint.z) + right * side * 0.03f * w;
                    ankle = basePos + moveDir * Mathf.Sin(phase) * stride + up * (0.09f * w + Mathf.Max(0f, Mathf.Cos(phase)) * stride * 0.5f);
                }
                // Out of reach (a tall player on a short fighter): stretch a little, then let the feet dangle.
                Vector3 toAnkle = ankle - hipJoint;
                if (toAnkle.magnitude > legLen * 1.25f) ankle = hipJoint + toAnkle.normalized * legLen * 1.25f;
                Vector3 kneeHint = fwd + right * side * 0.1f;
                Limb(bones, left ? Bone.ThighL : Bone.ThighR, left ? Bone.ShinL : Bone.ShinR, hipJoint, ankle, b.thigh * w, b.shin * w, kneeHint, fwd, w, 1.25f);
                Set(bones, left ? Bone.FootL : Bone.FootR, ankle, footRot, w);
            }

            if (!ragdoll || p.idle) return;

            // ---- rotate the whole body: lying on its back (K.O.) or rolling (tumble)
            Vector3 centre = hips;
            Quaternion lie = Quaternion.AngleAxis(-90f, right);
            Quaternion r;
            Vector3 place;
            if (p.down)
            {
                r = lie;
                place = p.feet + up * (b.chestD * 0.5f * w) - fwd * 0.1f;
            }
            else
            {
                r = Quaternion.Slerp(Quaternion.identity, p.tumbleRot, p.tumble);
                place = p.feet + up * Mathf.Lerp(hips.y - p.feet.y, b.chestD * 0.6f * w + 0.2f * w, p.tumble);
            }
            for (int i = 0; i < BoneCount; i++)
            {
                bones[i].pos = place + r * (bones[i].pos - centre);
                bones[i].rot = r * bones[i].rot;
            }
        }

        /// <summary>A rotation whose +Y is exactly up and whose +Z faces as close to forward as possible.</summary>
        public static Quaternion Frame(Vector3 up, Vector3 forward)
        {
            Vector3 f = forward - up * Vector3.Dot(forward, up);
            if (f.sqrMagnitude < 0.0001f)
            {
                f = Vector3.Cross(up, Vector3.right);
                if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            }
            return Quaternion.LookRotation(f.normalized, up);
        }

        static void Set(BonePose[] bones, Bone bone, Vector3 pos, Quaternion rot, float w)
        {
            bones[(int)bone] = new BonePose { pos = pos, rot = rot, scale = new Vector3(w, w, w) };
        }

        /// <summary>Two-bone IK: places a limb from root toward target, bending toward hint. Stretches up to maxStretch.</summary>
        static void Limb(BonePose[] bones, Bone upper, Bone lower, Vector3 root, Vector3 target, float l1, float l2, Vector3 hint, Vector3 fwd, float w, float maxStretch)
        {
            Vector3 d = target - root;
            float dist = d.magnitude;
            Vector3 dir = dist > 0.0001f ? d / dist : Vector3.down;
            float stretch = Mathf.Clamp(dist / (l1 + l2), 1f, maxStretch);
            l1 *= stretch;
            l2 *= stretch;
            dist = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.001f, (l1 + l2) * 0.999f);
            float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 bendDir = hint - dir * Vector3.Dot(hint, dir);
            if (bendDir.sqrMagnitude < 0.0001f) bendDir = fwd - dir * Vector3.Dot(fwd, dir);
            if (bendDir.sqrMagnitude < 0.0001f) bendDir = Vector3.Cross(dir, Vector3.right);
            bendDir.Normalize();
            Vector3 joint = root + dir * (l1 * cosA) + bendDir * (l1 * sinA);
            Vector3 end = root + dir * dist;

            Vector3 upperDir = (joint - root).normalized, lowerDir = (end - joint).normalized;
            bones[(int)upper] = new BonePose { pos = root, rot = Frame(upperDir, fwd), scale = new Vector3(w, w * stretch, w) };
            bones[(int)lower] = new BonePose { pos = joint, rot = Frame(lowerDir, fwd), scale = new Vector3(w, w * stretch, w) };
        }
    }
}
