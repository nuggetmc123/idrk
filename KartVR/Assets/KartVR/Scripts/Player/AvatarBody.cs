using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// A simple full-body racer (helmet, racing suit, gloves, boots) built from
    /// shapes at runtime. Give it where the head, hands, hips and feet are each
    /// frame and it bends the arms and legs to fit (two-bone IK), so it works
    /// both walking in the hub and sitting in a kart.
    /// </summary>
    public class AvatarBody : MonoBehaviour
    {
        const float UpperArm = 0.3f, LowerArm = 0.29f, UpperLeg = 0.44f, LowerLeg = 0.44f;

        Transform helmet, visor, torso, chestStripe, pelvis, neck;
        Transform lUpperArm, lLowerArm, rUpperArm, rLowerArm, lGlove, rGlove;
        Transform lThigh, lShin, rThigh, rShin, lBoot, rBoot;
        int builtPaint = -1, builtAccent = -1;
        bool headVisible = true;

        public void Build(int paint, int accent)
        {
            if (paint == builtPaint && accent == builtAccent) return;
            builtPaint = paint;
            builtAccent = accent;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            var suit = KartUI.Mat(PlayerProfile.GetColor(paint), 0.35f);
            var trim = KartUI.Mat(PlayerProfile.GetColor(accent), 0.4f);
            var dark = KartUI.Mat(new Color(0.12f, 0.12f, 0.14f), 0.3f);
            var helmetMat = KartUI.Mat(PlayerProfile.GetColor(paint), 0.85f, 0.1f);
            var visorMat = KartUI.Mat(new Color(0.05f, 0.08f, 0.12f), 0.95f, 0.6f);

            var t = transform;
            helmet = KartUI.Part(t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.27f, 0.29f, 0.3f), helmetMat, Vector3.zero, "Helmet");
            KartUI.Part(helmet, PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(0.18f, 0.12f, 1.02f), trim, Vector3.zero, "HelmetStripe");
            visor = KartUI.Part(helmet, PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.2f), new Vector3(0.82f, 0.42f, 0.68f), visorMat, Vector3.zero, "Visor");
            neck = KartUI.Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.05f, 0.1f), dark, Vector3.zero, "Neck");
            torso = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.36f, 0.3f, 0.24f), suit, Vector3.zero, "Torso");
            chestStripe = KartUI.Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.42f), new Vector3(0.3f, 0.25f, 0.2f), trim, Vector3.zero, "ChestStripe");
            pelvis = KartUI.Part(t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.32f, 0.2f, 0.22f), dark, Vector3.zero, "Pelvis");

            lUpperArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "LUpperArm");
            lLowerArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, trim, Vector3.zero, "LLowerArm");
            rUpperArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "RUpperArm");
            rLowerArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, trim, Vector3.zero, "RLowerArm");
            lGlove = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 0.05f, 0.11f), dark, Vector3.zero, "LGlove");
            rGlove = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 0.05f, 0.11f), dark, Vector3.zero, "RGlove");

            lThigh = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "LThigh");
            lShin = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "LShin");
            rThigh = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "RThigh");
            rShin = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "RShin");
            lBoot = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.11f, 0.08f, 0.24f), trim, Vector3.zero, "LBoot");
            rBoot = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.11f, 0.08f, 0.24f), trim, Vector3.zero, "RBoot");

            SetHeadVisible(headVisible);
        }

        /// <summary>Hide your own helmet in first person so it doesn't block the camera.</summary>
        public void SetHeadVisible(bool visible)
        {
            headVisible = visible;
            if (helmet) foreach (var r in helmet.GetComponentsInChildren<Renderer>()) r.enabled = visible;
            if (neck) neck.GetComponent<Renderer>().enabled = visible;
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            if (visible) SetHeadVisible(headVisible);
        }

        /// <param name="hips">World position of the hips.</param>
        /// <param name="bodyRot">Which way the body faces (yaw, or the kart's rotation when seated).</param>
        public void Pose(Vector3 hips, Quaternion bodyRot, Vector3 headPos, Quaternion headRot,
            Vector3 leftHand, Quaternion leftRot, Vector3 rightHand, Quaternion rightRot, Vector3 leftFoot, Vector3 rightFoot)
        {
            if (helmet == null) return;
            Vector3 up = bodyRot * Vector3.up, right = bodyRot * Vector3.right, fwd = bodyRot * Vector3.forward;

            helmet.SetPositionAndRotation(headPos, headRot);

            // Spine from hips to the base of the neck (a bit below and behind the head).
            Vector3 neckBase = headPos - (headRot * Vector3.up) * 0.17f - (headRot * Vector3.forward) * 0.05f;
            Vector3 spine = neckBase - hips;
            if (spine.magnitude > 0.65f) neckBase = hips + spine.normalized * 0.65f; // don't stretch too far
            if (spine.magnitude < 0.3f) neckBase = hips + up * 0.3f;
            Vector3 chestDir = (neckBase - hips).normalized;
            Quaternion torsoRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, chestDir).normalized, chestDir);
            torso.SetPositionAndRotation((hips + neckBase) * 0.5f + chestDir * 0.03f, torsoRot);
            torso.localScale = new Vector3(0.36f, (neckBase - hips).magnitude * 0.62f, 0.24f);
            KartUI.Span(neck, neckBase, headPos - (headRot * Vector3.up) * 0.1f, 0.1f, false);
            pelvis.SetPositionAndRotation(hips, bodyRot);

            Vector3 torsoRight = torsoRot * Vector3.right;
            Vector3 shoulderL = neckBase - torsoRight * 0.19f - chestDir * 0.06f;
            Vector3 shoulderR = neckBase + torsoRight * 0.19f - chestDir * 0.06f;

            // Elbows bend down and out; knees bend forward.
            Limb(lUpperArm, lLowerArm, shoulderL, leftHand, UpperArm, LowerArm, -up - torsoRight * 0.6f - fwd * 0.3f, 0.09f, 0.08f);
            Limb(rUpperArm, rLowerArm, shoulderR, rightHand, UpperArm, LowerArm, -up + torsoRight * 0.6f - fwd * 0.3f, 0.09f, 0.08f);
            lGlove.SetPositionAndRotation(leftHand, leftRot);
            rGlove.SetPositionAndRotation(rightHand, rightRot);

            Vector3 hipL = hips - right * 0.1f, hipR = hips + right * 0.1f;
            Limb(lThigh, lShin, hipL, leftFoot, UpperLeg, LowerLeg, fwd + up * 0.2f, 0.13f, 0.11f);
            Limb(rThigh, rShin, hipR, rightFoot, UpperLeg, LowerLeg, fwd + up * 0.2f, 0.13f, 0.11f);
            Quaternion footRot = Quaternion.LookRotation(fwd, up);
            lBoot.SetPositionAndRotation(leftFoot + fwd * 0.05f, footRot);
            rBoot.SetPositionAndRotation(rightFoot + fwd * 0.05f, footRot);
        }

        /// <summary>Two-bone IK: place upper and lower segments from root towards target, bending towards hint.</summary>
        static void Limb(Transform upper, Transform lower, Vector3 root, Vector3 target, float a, float b, Vector3 hint,
            float thickUpper, float thickLower)
        {
            Vector3 toTarget = target - root;
            float d = Mathf.Clamp(toTarget.magnitude, 0.01f, a + b - 0.001f);
            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.down;
            // Distance along dir to the elbow, and how far it sticks out sideways (law of cosines).
            float x = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 bend = Vector3.ProjectOnPlane(hint, dir);
            if (bend.sqrMagnitude < 0.0001f) bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            Vector3 elbow = root + dir * x + bend.normalized * h;
            Vector3 end = root + dir * d;
            KartUI.Span(upper, root, elbow, thickUpper);
            KartUI.Span(lower, elbow, end, thickLower);
        }
    }
}
