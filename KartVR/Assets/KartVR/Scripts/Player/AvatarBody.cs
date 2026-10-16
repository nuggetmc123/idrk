using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// A player's full body. Give it where the head, hands, hips and feet are
    /// each frame and it bends the arms and legs to fit (two-bone IK), so it
    /// works both walking in the hub and sitting in a kart.
    ///
    /// It uses the Racer model (Models/Racer.fbx) when one is set on the
    /// SessionManager. The model is made of separate rigid parts (Helmet,
    /// Torso, UpperArm_L, Forearm_L, Glove_L, Thigh_L, Shin_L, Boot_L, ...),
    /// so each part is attached to a joint and moved, no skeleton needed.
    /// Joint positions are measured from the parts themselves, so a re-exported
    /// model with different proportions still lines up. Orange parts take the
    /// player's paint color and white parts their accent color.
    /// Without the model it falls back to a body built from simple shapes.
    /// </summary>
    public class AvatarBody : MonoBehaviour
    {
        int builtPaint = -1, builtAccent = -1;
        bool headVisible = true;
        bool wholeBodyHiddenFromSelf;
        ModelRig model;
        PrimitiveRig prims;

        public void Build(int paint, int accent)
        {
            if (paint == builtPaint && accent == builtAccent) return;
            builtPaint = paint;
            builtAccent = accent;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            model = null;
            prims = null;

            if (KartUI.RacerModel != null)
            {
                model = ModelRig.Create(transform, KartUI.RacerModel, PlayerProfile.GetColor(paint), PlayerProfile.GetColor(accent));
                if (model == null) Debug.LogWarning("[KartVR] Racer model is missing expected parts; using the simple body instead.");
            }
            if (model == null) prims = PrimitiveRig.Create(transform, paint, accent);
            SetHeadVisible(headVisible);
            if (wholeBodyHiddenFromSelf) HideWholeBodyFromSelf();
        }

        /// <summary>
        /// Walking around: you don't see your own body at all (it looked odd from inside), only your hands.
        /// Everyone else still sees it.
        /// </summary>
        public void HideWholeBodyFromSelf()
        {
            wholeBodyHiddenFromSelf = true;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) SetHidden(r, true);
        }

        /// <summary>Hide your own helmet in first person so it doesn't block the camera.</summary>
        /// <summary>
        /// Your own helmet goes on this layer instead of being switched off: your VR camera skips it (so it
        /// doesn't block your view) but the Quest Cam still sees it, so recordings don't show you headless.
        /// </summary>
        public const int HiddenFromSelfLayer = 30;

        static void SetHidden(Renderer r, bool hiddenFromSelf)
        {
            if (r) r.gameObject.layer = hiddenFromSelf ? HiddenFromSelfLayer : 0;
        }

        public void SetHeadVisible(bool visible)
        {
            headVisible = visible;
            if (model != null) model.SetHeadVisible(visible);
            if (prims != null) prims.SetHeadVisible(visible);
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            if (visible) SetHeadVisible(headVisible);
        }

        /// <param name="hips">World position of the hips (pelvis).</param>
        /// <param name="bodyRot">Which way the body faces (yaw, or the kart's rotation when seated).</param>
        /// <param name="leftFoot">Where the bottom of the left boot should be.</param>
        public void Pose(Vector3 hips, Quaternion bodyRot, Vector3 headPos, Quaternion headRot,
            Vector3 leftHand, Quaternion leftRot, Vector3 rightHand, Quaternion rightRot, Vector3 leftFoot, Vector3 rightFoot)
        {
            if (model != null) model.Pose(hips, bodyRot, headPos, headRot, leftHand, leftRot, rightHand, rightRot, leftFoot, rightFoot);
            else if (prims != null) prims.Pose(hips, bodyRot, headPos, headRot, leftHand, leftRot, rightHand, rightRot, leftFoot, rightFoot);
        }

        /// <summary>Two-bone IK: where the middle joint (elbow/knee) goes, bending towards hint.</summary>
        static Vector3 SolveJoint(Vector3 root, Vector3 target, float a, float b, Vector3 hint, out Vector3 end)
        {
            Vector3 toTarget = target - root;
            float d = Mathf.Clamp(toTarget.magnitude, 0.01f, a + b - 0.001f);
            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.down;
            // Distance along dir to the joint, and how far it sticks out sideways (law of cosines).
            float x = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 bend = Vector3.ProjectOnPlane(hint, dir);
            if (bend.sqrMagnitude < 0.0001f) bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            end = root + dir * d;
            return root + dir * x + bend.normalized * h;
        }

        // =====================================================================
        // The Racer.fbx model, cut into joints.
        // =====================================================================
        class ModelRig
        {
            // A joint: an empty object the model's parts hang off. In the rest
            // pose it sits at `rest` with no rotation; posing moves and turns it.
            class Bone
            {
                public Transform t;
                public Vector3 rest;
            }

            Bone head, neck, torso, pelvis, upperL, lowerL, handL, upperR, lowerR, handR, thighL, shinL, footL, thighR, shinR, footR;
            readonly List<Renderer> headParts = new List<Renderer>();

            // Rest-pose joint positions (model space: +Z forward, +Y up, feet at y=0).
            Vector3 rHead, rNeckBase, rPelvis, rShoulderL, rShoulderR, rElbowL, rElbowR, rWristL, rWristR;
            Vector3 rHipL, rHipR, rKneeL, rKneeR, rAnkleL, rAnkleR, rSoleL, rSoleR;
            float upperArm, lowerArm, thigh, shin, spine;

            public static ModelRig Create(Transform parent, GameObject prefab, Color paint, Color accent)
            {
                // Measure the model standing at the world origin, then attach its parts to joints.
                var holder = new GameObject("RacerModel").transform;
                var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, holder);
                instance.transform.localScale = Vector3.one;

                // Imported cameras/lights would take over the view; drop them.
                foreach (var c in instance.GetComponentsInChildren<Camera>(true)) { c.enabled = false; Object.Destroy(c.gameObject); }
                foreach (var l in instance.GetComponentsInChildren<Light>(true)) { l.enabled = false; Object.Destroy(l.gameObject); }

                var parts = new Dictionary<string, Renderer>();
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    if (r.GetComponent<Camera>() == null) parts[Clean(r.gameObject.name)] = r;

                if (!parts.ContainsKey("Helmet") || !parts.ContainsKey("Boot_L") || !parts.ContainsKey("Torso"))
                {
                    Object.Destroy(holder.gameObject);
                    return null;
                }

                // Make sure the model faces +Z with its left side at -X, whatever the importer did.
                if (parts.ContainsKey("Visor") && parts["Visor"].bounds.center.z < parts["Helmet"].bounds.center.z)
                    holder.rotation = Quaternion.Euler(0f, 180f, 0f);
                bool mirrored = parts["Boot_L"].bounds.center.x > parts["Boot_R"].bounds.center.x;

                var rig = new ModelRig();
                if (!rig.Measure(parts, mirrored))
                {
                    Object.Destroy(holder.gameObject);
                    return null;
                }
                rig.Assemble(parent, parts, mirrored, paint, accent);
                Object.Destroy(holder.gameObject);
                return rig;
            }

            static string Clean(string name)
            {
                int dot = name.IndexOf('.');
                return dot > 0 ? name.Substring(0, dot) : name;
            }

            static Vector3 Center(Dictionary<string, Renderer> p, string n) => p[n].bounds.center;

            static Vector3 Bottom(Dictionary<string, Renderer> p, string n)
            {
                var b = p[n].bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }

            /// <summary>Center of the region where two parts overlap (that's where the joint between them is).</summary>
            static Vector3 Between(Dictionary<string, Renderer> p, string a, string b)
            {
                Bounds A = p[a].bounds, B = p[b].bounds;
                var min = Vector3.Max(A.min, B.min);
                var max = Vector3.Min(A.max, B.max);
                if (min.x > max.x || min.y > max.y || min.z > max.z) return (A.center + B.center) * 0.5f;
                return (min + max) * 0.5f;
            }

            bool Measure(Dictionary<string, Renderer> p, bool mirrored)
            {
                string L = mirrored ? "_R" : "_L", R = mirrored ? "_L" : "_R";
                foreach (var need in new[] { "Hips", "Neck", "Shoulder" + L, "UpperArm" + L, "Forearm" + L, "Glove" + L, "Thigh" + L, "Shin" + L, "Boot" + L,
                             "Shoulder" + R, "UpperArm" + R, "Forearm" + R, "Glove" + R, "Thigh" + R, "Shin" + R, "Boot" + R })
                    if (!p.ContainsKey(need)) return false;

                rHead = Center(p, "Helmet");
                rNeckBase = Bottom(p, "Neck");
                rPelvis = Center(p, "Hips");
                rShoulderL = Center(p, "Shoulder" + L); rShoulderR = Center(p, "Shoulder" + R);
                rElbowL = Between(p, "UpperArm" + L, "Forearm" + L); rElbowR = Between(p, "UpperArm" + R, "Forearm" + R);
                rWristL = Center(p, "Glove" + L); rWristR = Center(p, "Glove" + R);
                rHipL = Between(p, "Thigh" + L, "Hips"); rHipR = Between(p, "Thigh" + R, "Hips");
                rKneeL = Between(p, "Thigh" + L, "Shin" + L); rKneeR = Between(p, "Thigh" + R, "Shin" + R);
                rAnkleL = Between(p, "Shin" + L, "Boot" + L); rAnkleR = Between(p, "Shin" + R, "Boot" + R);
                rSoleL = Bottom(p, "Boot" + L); rSoleR = Bottom(p, "Boot" + R);

                upperArm = Vector3.Distance(rShoulderL, rElbowL);
                lowerArm = Vector3.Distance(rElbowL, rWristL);
                thigh = Vector3.Distance(rHipL, rKneeL);
                shin = Vector3.Distance(rKneeL, rAnkleL);
                spine = Vector3.Distance(rPelvis, rNeckBase);
                return upperArm > 0.05f && lowerArm > 0.05f && thigh > 0.05f && shin > 0.05f;
            }

            void Assemble(Transform parent, Dictionary<string, Renderer> p, bool mirrored, Color paint, Color accent)
            {
                string L = mirrored ? "_R" : "_L", R = mirrored ? "_L" : "_R";
                head = MakeBone(parent, "Head", rHead);
                neck = MakeBone(parent, "Neck", rNeckBase);
                torso = MakeBone(parent, "Torso", rPelvis);
                pelvis = MakeBone(parent, "Pelvis", rPelvis);
                upperL = MakeBone(parent, "UpperArmL", rShoulderL); lowerL = MakeBone(parent, "ForearmL", rElbowL); handL = MakeBone(parent, "HandL", rWristL);
                upperR = MakeBone(parent, "UpperArmR", rShoulderR); lowerR = MakeBone(parent, "ForearmR", rElbowR); handR = MakeBone(parent, "HandR", rWristR);
                thighL = MakeBone(parent, "ThighL", rHipL); shinL = MakeBone(parent, "ShinL", rKneeL); footL = MakeBone(parent, "FootL", rAnkleL);
                thighR = MakeBone(parent, "ThighR", rHipR); shinR = MakeBone(parent, "ShinR", rKneeR); footR = MakeBone(parent, "FootR", rAnkleR);

                var paintMat = KartUI.Mat(paint, 0.55f, 0.05f);
                var accentMat = KartUI.Mat(accent, 0.5f);
                var darkMat = KartUI.Mat(new Color(0.08f, 0.08f, 0.1f), 0.35f);
                var visorMat = KartUI.Mat(new Color(0.03f, 0.04f, 0.06f), 0.95f, 0.6f);

                foreach (var kv in p)
                {
                    var bone = BoneFor(kv.Key, L, R);
                    var r = kv.Value;
                    if (bone == null) { r.enabled = false; continue; }

                    // Recolor: the model's orange = paint, white = accent, dark stays dark.
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (kv.Key == "Visor") { mats[i] = visorMat; continue; }
                        Color c = mats[i] != null ? mats[i].color : Color.gray;
                        Color.RGBToHSV(c, out float hue, out float s, out float v);
                        mats[i] = s > 0.45f && v > 0.3f ? paintMat : v > 0.6f ? accentMat : darkMat;
                    }
                    r.sharedMaterials = mats;
                    r.transform.SetParent(bone.t, true);
                    if (bone == head || kv.Key == "Neck") headParts.Add(r);
                }

                // Bones were placed in the model's rest frame; now move them under the avatar.
                foreach (var b in new[] { head, neck, torso, pelvis, upperL, lowerL, handL, upperR, lowerR, handR, thighL, shinL, footL, thighR, shinR, footR })
                    b.t.SetParent(parent, false);
            }

            Bone MakeBone(Transform parent, string name, Vector3 rest)
            {
                var t = new GameObject(name).transform;
                t.SetPositionAndRotation(rest, Quaternion.identity); // world space, while the model sits at the origin
                return new Bone { t = t, rest = rest };
            }

            Bone BoneFor(string part, string L, string R)
            {
                switch (part)
                {
                    case "Helmet": case "HelmetStripe": case "ChinBar": case "Visor": return head;
                    case "Neck": return neck;
                    case "Torso": case "SuitStripe": return torso;
                    case "Hips": case "Belt": return pelvis;
                }
                if (part == "Shoulder" + L) return torso;
                if (part == "Shoulder" + R) return torso;
                if (part == "UpperArm" + L) return upperL;
                if (part == "UpperArm" + R) return upperR;
                if (part == "Forearm" + L || part == "GloveCuff" + L) return lowerL;
                if (part == "Forearm" + R || part == "GloveCuff" + R) return lowerR;
                if (part == "Glove" + L) return handL;
                if (part == "Glove" + R) return handR;
                if (part == "Thigh" + L) return thighL;
                if (part == "Thigh" + R) return thighR;
                if (part == "Shin" + L) return shinL;
                if (part == "Shin" + R) return shinR;
                if (part == "Boot" + L) return footL;
                if (part == "Boot" + R) return footR;
                // Unknown extra parts follow the torso so they don't float away.
                return torso;
            }

            public void SetHeadVisible(bool visible)
            {
                foreach (var r in headParts) SetHidden(r, !visible);
            }

            /// <summary>Point a segment bone from `from` towards `to`, turning it the least from its rest direction.</summary>
            static void Aim(Bone bone, Vector3 from, Vector3 restDir, Vector3 to, Quaternion bodyRot)
            {
                Vector3 local = Quaternion.Inverse(bodyRot) * (to - from);
                Quaternion rot = local.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(restDir, local) : Quaternion.identity;
                bone.t.SetPositionAndRotation(from, bodyRot * rot);
            }

            /// <summary>
            /// Turns a controller's rotation into the glove's: the glove's fingers (down in the rest pose) point
            /// where the controller points, and its thumb side (forward in the rest pose) is the controller's top.
            /// </summary>
            static readonly Quaternion GloveFromController = Quaternion.Euler(-90f, 0f, 0f);

            /// <summary>
            /// One arm. The glove always lands exactly on your controller: if it's further than the model's arm
            /// reaches, the arm stretches a little (up to 30%) and then the shoulder gives the rest.
            /// </summary>
            void Arm(Bone upper, Bone lower, Bone hand, Vector3 shoulder, Vector3 target, Quaternion handRot, Vector3 hint,
                Vector3 rShoulder, Vector3 rElbow, Vector3 rWrist, Quaternion bodyRot)
            {
                float reach = Vector3.Distance(shoulder, target), length = upperArm + lowerArm;
                float stretch = Mathf.Clamp(reach / (length * 0.98f), 1f, 1.3f);
                upper.t.localScale = lower.t.localScale = Vector3.one * stretch;
                float a = upperArm * stretch, b = lowerArm * stretch;
                if (reach > (a + b) * 0.99f) shoulder += (target - shoulder).normalized * (reach - (a + b) * 0.99f);
                Vector3 elbow = SolveJoint(shoulder, target, a, b, hint, out Vector3 wrist);
                Aim(upper, shoulder, rElbow - rShoulder, elbow, bodyRot);
                Aim(lower, elbow, rWrist - rElbow, wrist, bodyRot);
                hand.t.SetPositionAndRotation(target, handRot * GloveFromController); // turns with your real wrist
            }

            public void Pose(Vector3 hips, Quaternion bodyRot, Vector3 headPos, Quaternion headRot,
                Vector3 leftHand, Quaternion leftRot, Vector3 rightHand, Quaternion rightRot, Vector3 leftSole, Vector3 rightSole)
            {
                Vector3 up = bodyRot * Vector3.up, right = bodyRot * Vector3.right, fwd = bodyRot * Vector3.forward;

                // Head is rigid; the spine leans from the hips towards where the neck would be.
                head.t.SetPositionAndRotation(headPos, headRot);
                Vector3 neckTarget = headPos + headRot * (rNeckBase - rHead);
                Vector3 spineDir = (neckTarget - hips).sqrMagnitude > 0.0001f ? (neckTarget - hips).normalized : up;
                Vector3 neckBase = hips + spineDir * spine;
                Aim(torso, hips, rNeckBase - rPelvis, neckBase, bodyRot);
                Aim(neck, neckBase, rHead - rNeckBase, headPos, bodyRot);
                pelvis.t.SetPositionAndRotation(hips, bodyRot);

                // Arms: shoulders ride the torso, elbows bend down and out.
                Quaternion torsoRot = torso.t.rotation;
                Vector3 shL = hips + torsoRot * (rShoulderL - rPelvis), shR = hips + torsoRot * (rShoulderR - rPelvis);
                Arm(upperL, lowerL, handL, shL, leftHand, leftRot, -up - right * 0.6f - fwd * 0.3f, rShoulderL, rElbowL, rWristL, bodyRot);
                Arm(upperR, lowerR, handR, shR, rightHand, rightRot, -up + right * 0.6f - fwd * 0.3f, rShoulderR, rElbowR, rWristR, bodyRot);

                // Legs: hips ride the pelvis, knees bend forward, boots stay flat.
                Vector3 hipL = hips + bodyRot * (rHipL - rPelvis), hipR = hips + bodyRot * (rHipR - rPelvis);
                Vector3 ankleL = leftSole + bodyRot * (rAnkleL - rSoleL), ankleR = rightSole + bodyRot * (rAnkleR - rSoleR);
                Vector3 knL = SolveJoint(hipL, ankleL, thigh, shin, fwd + up * 0.2f, out Vector3 anL);
                Vector3 knR = SolveJoint(hipR, ankleR, thigh, shin, fwd + up * 0.2f, out Vector3 anR);
                Aim(thighL, hipL, rKneeL - rHipL, knL, bodyRot);
                Aim(shinL, knL, rAnkleL - rKneeL, anL, bodyRot);
                Aim(thighR, hipR, rKneeR - rHipR, knR, bodyRot);
                Aim(shinR, knR, rAnkleR - rKneeR, anR, bodyRot);
                footL.t.SetPositionAndRotation(anL, bodyRot);
                footR.t.SetPositionAndRotation(anR, bodyRot);
            }
        }

        // =====================================================================
        // Fallback: a body made of simple shapes (used if no model is set).
        // =====================================================================
        class PrimitiveRig
        {
            const float UpperArm = 0.3f, LowerArm = 0.29f, UpperLeg = 0.44f, LowerLeg = 0.44f;

            Transform helmet, torso, pelvis, neck;
            Transform lUpperArm, lLowerArm, rUpperArm, rLowerArm, lGlove, rGlove;
            Transform lThigh, lShin, rThigh, rShin, lBoot, rBoot;

            public static PrimitiveRig Create(Transform t, int paint, int accent)
            {
                var suit = KartUI.Mat(PlayerProfile.GetColor(paint), 0.35f);
                var trim = KartUI.Mat(PlayerProfile.GetColor(accent), 0.4f);
                var dark = KartUI.Mat(new Color(0.12f, 0.12f, 0.14f), 0.3f);
                var helmetMat = KartUI.Mat(PlayerProfile.GetColor(paint), 0.85f, 0.1f);
                var visorMat = KartUI.Mat(new Color(0.05f, 0.08f, 0.12f), 0.95f, 0.6f);

                var r = new PrimitiveRig();
                r.helmet = KartUI.Part(t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.27f, 0.29f, 0.3f), helmetMat, Vector3.zero, "Helmet");
                KartUI.Part(r.helmet, PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(0.18f, 0.12f, 1.02f), trim, Vector3.zero, "HelmetStripe");
                KartUI.Part(r.helmet, PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.2f), new Vector3(0.82f, 0.42f, 0.68f), visorMat, Vector3.zero, "Visor");
                r.neck = KartUI.Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.05f, 0.1f), dark, Vector3.zero, "Neck");
                r.torso = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.36f, 0.3f, 0.24f), suit, Vector3.zero, "Torso");
                KartUI.Part(r.torso, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.42f), new Vector3(0.3f, 0.25f, 0.2f), trim, Vector3.zero, "ChestStripe");
                r.pelvis = KartUI.Part(t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.32f, 0.2f, 0.22f), dark, Vector3.zero, "Pelvis");

                r.lUpperArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "LUpperArm");
                r.lLowerArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, trim, Vector3.zero, "LLowerArm");
                r.rUpperArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "RUpperArm");
                r.rLowerArm = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, trim, Vector3.zero, "RLowerArm");
                r.lGlove = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 0.05f, 0.11f), dark, Vector3.zero, "LGlove");
                r.rGlove = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 0.05f, 0.11f), dark, Vector3.zero, "RGlove");

                r.lThigh = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "LThigh");
                r.lShin = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "LShin");
                r.rThigh = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "RThigh");
                r.rShin = KartUI.Part(t, PrimitiveType.Capsule, Vector3.zero, Vector3.one, suit, Vector3.zero, "RShin");
                r.lBoot = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.11f, 0.08f, 0.24f), trim, Vector3.zero, "LBoot");
                r.rBoot = KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.11f, 0.08f, 0.24f), trim, Vector3.zero, "RBoot");
                return r;
            }

            public void SetHeadVisible(bool visible)
            {
                if (helmet) foreach (var r in helmet.GetComponentsInChildren<Renderer>()) SetHidden(r, !visible);
                if (neck) SetHidden(neck.GetComponent<Renderer>(), !visible);
            }

            public void Pose(Vector3 hips, Quaternion bodyRot, Vector3 headPos, Quaternion headRot,
                Vector3 leftHand, Quaternion leftRot, Vector3 rightHand, Quaternion rightRot, Vector3 leftFoot, Vector3 rightFoot)
            {
                if (helmet == null) return;
                Vector3 up = bodyRot * Vector3.up, right = bodyRot * Vector3.right, fwd = bodyRot * Vector3.forward;
                leftFoot += up * 0.04f;  // these boxes are centred, so lift them onto the ground
                rightFoot += up * 0.04f;

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

            static void Limb(Transform upper, Transform lower, Vector3 root, Vector3 target, float a, float b, Vector3 hint,
                float thickUpper, float thickLower)
            {
                Vector3 elbow = SolveJoint(root, target, a, b, hint, out Vector3 end);
                KartUI.Span(upper, root, elbow, thickUpper);
                KartUI.Span(lower, elbow, target, thickLower); // reaches all the way to your hand, even past arm's length
            }
        }
    }
}
