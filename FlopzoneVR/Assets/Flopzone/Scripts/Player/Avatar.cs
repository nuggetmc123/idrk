using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// A white, floppy mannequin built from primitives, posed every frame from a head
    /// and two hands (arms and legs are solved with simple two-bone IK), and
    /// turned into a floppy physics ragdoll when the player dies.
    /// </summary>
    public class Avatar : MonoBehaviour
    {
        public Transform head, torso, pelvis, neck;
        public Transform[] upperArm = new Transform[2], lowerArm = new Transform[2], hand = new Transform[2];
        public Transform[] thigh = new Transform[2], shin = new Transform[2], foot = new Transform[2];
        public Color shirt, skin, pants;
        float walkPhase;
        Cosmetics.Body body;
        Material bare;
        readonly System.Collections.Generic.List<GameObject> outfit = new System.Collections.Generic.List<GameObject>();
        /// <summary>The outfit being worn (a packed <see cref="Look"/>), or -1 before the first one.</summary>
        public int LookCode { get; private set; } = -1;

        const float UpperArm = 0.28f, LowerArm = 0.27f, Thigh = 0.42f, Shin = 0.42f;

        public static Color TeamColor(int team)
        {
            if (team == 0) return new Color(0.9f, 0.22f, 0.2f);
            if (team == 1) return new Color(0.2f, 0.45f, 0.95f);
            return new Color(0.95f, 0.75f, 0.2f);
        }

        /// <summary>Build a player body. look is a packed <see cref="Look"/>; -1 picks a random outfit from the seed (bots).</summary>
        public static Avatar Build(Transform parent, string seed, Color shirtColor, int look = 0)
        {
            var go = new GameObject("avatar");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<Avatar>();
            // a smooth, white, floppy mannequin: tall rounded head with a dot-eyed smile,
            // long rounded body, thin limbs, little fingers, flat rounded feet.
            // All white like the reference; the team shows only as a coloured band on each upper arm.
            var white = new Color(0.93f, 0.93f, 0.92f);
            a.skin = white;
            a.shirt = white;
            a.pants = white;
            var skinM = Prims.Lit(a.skin, 0.45f, 0f, Surface.Plastic);
            var bodyM = Prims.Lit(a.shirt, 0.45f, 0f, Surface.Plastic);
            var bandM = Prims.Lit(shirtColor, 0.4f, 0f, Surface.Plastic);
            var dark = Prims.Lit(new Color(0.08f, 0.08f, 0.09f), 0.7f);

            a.head = Prims.Empty("head", go.transform).transform;
            Prims.Make(PrimitiveType.Capsule, a.head, new Vector3(0, 0.01f, 0), new Vector3(0.21f, 0.135f, 0.19f), skinM, false);
            Prims.Ball(a.head, new Vector3(-0.045f, 0.03f, 0.094f), new Vector3(0.028f, 0.034f, 0.012f), dark, false);
            Prims.Ball(a.head, new Vector3(0.045f, 0.03f, 0.094f), new Vector3(0.028f, 0.034f, 0.012f), dark, false);
            for (int i = 0; i < 5; i++)
            {
                float t = (i - 2) / 2f;
                Prims.Ball(a.head, new Vector3(t * 0.04f, -0.03f + t * t * 0.012f, 0.094f - t * t * 0.004f), new Vector3(0.016f, 0.01f, 0.008f), dark, false);
            }
            var hb = a.head.gameObject.AddComponent<SphereCollider>();
            hb.radius = 0.12f;

            a.torso = Prims.Empty("torso", go.transform).transform;
            Prims.Make(PrimitiveType.Capsule, a.torso, new Vector3(0, -0.04f, 0), new Vector3(0.24f, 0.36f, 0.2f), bodyM, false);
            var tb = a.torso.gameObject.AddComponent<CapsuleCollider>();
            tb.radius = 0.13f; tb.height = 0.72f; tb.center = new Vector3(0, -0.04f, 0);
            a.pelvis = Prims.Empty("pelvis", go.transform).transform;
            Prims.Make(PrimitiveType.Capsule, a.pelvis, Vector3.zero, new Vector3(0.2f, 0.11f, 0.17f), bodyM, false, Quaternion.Euler(0, 0, 90));
            var pb = a.pelvis.gameObject.AddComponent<SphereCollider>();
            pb.radius = 0.12f;
            a.neck = Prims.Empty("neck", go.transform).transform;
            Prims.Cyl(a.neck, Vector3.zero, 0.07f, 0.08f, skinM, Vector3.up);

            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                a.upperArm[s] = Segment("upperArm" + s, go.transform, 0.07f, bodyM);
                Prims.Cyl(a.upperArm[s].GetChild(0), new Vector3(0, -0.5f, 0), 1.15f, 0.6f, bandM, Vector3.up);
                a.lowerArm[s] = Segment("lowerArm" + s, go.transform, 0.06f, skinM);
                a.hand[s] = Prims.Empty("hand" + s, go.transform).transform;
                Prims.Ball(a.hand[s], new Vector3(0, 0, -0.01f), new Vector3(0.06f, 0.025f, 0.075f), skinM, false);
                for (int f = 0; f < 4; f++)
                    Prims.Capsule(a.hand[s], new Vector3((f - 1.5f) * 0.014f, -0.004f, 0.045f), 0.012f, 0.05f, skinM, new Vector3((f - 1.5f) * 0.25f, -0.15f, 1));
                Prims.Capsule(a.hand[s], new Vector3(-side * 0.032f, 0, 0.0f), 0.013f, 0.045f, skinM, new Vector3(-side, 0, 0.8f));
                a.thigh[s] = Segment("thigh" + s, go.transform, 0.075f, skinM);
                a.shin[s] = Segment("shin" + s, go.transform, 0.07f, skinM);
                a.foot[s] = Prims.Empty("foot" + s, go.transform).transform;
                Prims.Capsule(a.foot[s], new Vector3(0, 0.025f, 0.05f), 0.075f, 0.2f, skinM, Vector3.forward);
            }
            a.bare = skinM;
            a.body = new Cosmetics.Body
            {
                head = a.head, torso = a.torso, pelvis = a.pelvis, upperArm = a.upperArm, lowerArm = a.lowerArm, hand = a.hand,
                thigh = a.thigh, shin = a.shin, foot = a.foot,
                torsoBase = a.torso.GetChild(0).GetComponent<Renderer>(),
                pelvisBase = a.pelvis.GetChild(0).GetComponent<Renderer>(),
                upperArmVis = new Renderer[2], lowerArmVis = new Renderer[2], thighVis = new Renderer[2], shinVis = new Renderer[2], footBase = new Renderer[2],
                handParts = new System.Collections.Generic.List<Renderer>[2],
            };
            for (int s = 0; s < 2; s++)
            {
                a.body.upperArmVis[s] = a.upperArm[s].GetChild(0).GetComponent<Renderer>();
                a.body.lowerArmVis[s] = a.lowerArm[s].GetChild(0).GetComponent<Renderer>();
                a.body.thighVis[s] = a.thigh[s].GetChild(0).GetComponent<Renderer>();
                a.body.shinVis[s] = a.shin[s].GetChild(0).GetComponent<Renderer>();
                a.body.footBase[s] = a.foot[s].GetChild(0).GetComponent<Renderer>();
                a.body.handParts[s] = new System.Collections.Generic.List<Renderer>();
                foreach (Transform c in a.hand[s]) a.body.handParts[s].Add(c.GetComponent<Renderer>());
            }
            a.ApplyLook(look < 0 ? Look.FromSeed(seed) : look);
            return a;
        }

        /// <summary>Change clothes: take the old outfit off and put the new one on.</summary>
        public void ApplyLook(int code)
        {
            if (code == LookCode) return;
            LookCode = code;
            foreach (var go in outfit) if (go != null) DestroyImmediate(go);
            outfit.Clear();
            body.torsoBase.sharedMaterial = shirtMat;
            body.pelvisBase.sharedMaterial = shirtMat;
            for (int s = 0; s < 2; s++)
            {
                body.upperArmVis[s].sharedMaterial = shirtMat;
                body.lowerArmVis[s].sharedMaterial = bare;
                body.thighVis[s].sharedMaterial = bare;
                body.shinVis[s].sharedMaterial = bare;
                body.footBase[s].sharedMaterial = bare;
                foreach (var r in body.handParts[s]) r.sharedMaterial = bare;
            }
            Cosmetics.Apply(body, Look.Unpack(code), outfit);
            // a hidden body (your own, or a player waiting for their first pose) stays hidden
            bool shown = body.torsoBase.enabled;
            foreach (var go in outfit)
                if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = shown;
        }

        Material shirtMat => Prims.Lit(shirt, 0.45f, 0f, Surface.Plastic);

        static Transform Segment(string name, Transform parent, float thickness, Material m)
        {
            var t = Prims.Empty(name, parent).transform;
            var vis = Prims.Make(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(thickness, 0.5f, thickness), m, false);
            vis.name = "vis";
            return t;
        }

        /// <summary>Stretch a segment between two points (its visual is a unit capsule along Y).</summary>
        static void Place(Transform seg, Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            seg.position = (a + b) * 0.5f;
            seg.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            var vis = seg.GetChild(0);
            var s = vis.localScale;
            vis.localScale = new Vector3(s.x, (len + s.x) * 0.5f, s.z);
        }

        static Vector3 SolveElbow(Vector3 root, Vector3 target, float l1, float l2, Vector3 pole)
        {
            var d = target - root;
            float dist = Mathf.Clamp(d.magnitude, 0.01f, l1 + l2 - 0.001f);
            var dir = d.normalized;
            float a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist);
            float h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - a * a));
            var bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-4f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            return root + dir * a + bend.normalized * h;
        }

        /// <summary>Pose the body. feet = floor point under the player, yaw = body facing.</summary>
        public void Pose(Vector3 feet, float yaw, Vector3 headPos, Quaternion headRot, Vector3 lPos, Quaternion lRot, Vector3 rPos, Quaternion rRot, float speed, bool crouch)
        {
            var bodyRot = Quaternion.Euler(0, yaw, 0);
            var right = bodyRot * Vector3.right;
            var fwd = bodyRot * Vector3.forward;
            head.SetPositionAndRotation(headPos, headRot);
            var neckPos = headPos - Vector3.up * 0.16f - fwd * 0.04f;
            neck.position = headPos - Vector3.up * 0.12f;
            neck.rotation = bodyRot;
            float hipH = Mathf.Clamp(headPos.y - feet.y - 0.78f, 0.35f, 0.92f);
            var pelvisPos = new Vector3(neckPos.x, feet.y + hipH, neckPos.z) - fwd * 0.03f;
            var spineUp = (neckPos - pelvisPos).normalized;
            torso.position = Vector3.Lerp(pelvisPos, neckPos, 0.55f);
            torso.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, spineUp), spineUp);
            pelvis.SetPositionAndRotation(pelvisPos, bodyRot);

            walkPhase += Time.deltaTime * Mathf.Clamp(speed, 0, 7f) * 2.6f;
            float stride = Mathf.Clamp01(speed / 4f) * 0.32f;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                var shoulder = neckPos + right * side * 0.19f - spineUp * 0.06f;
                var handPos = s == 0 ? lPos : rPos;
                var handRot = s == 0 ? lRot : rRot;
                var reach = handPos - shoulder;
                if (reach.magnitude > UpperArm + LowerArm) handPos = shoulder + reach.normalized * (UpperArm + LowerArm);
                var elbow = SolveElbow(shoulder, handPos, UpperArm, LowerArm, Vector3.down + right * side * 0.6f - fwd * 0.3f);
                Place(upperArm[s], shoulder, elbow);
                Place(lowerArm[s], elbow, handPos);
                hand[s].SetPositionAndRotation(handPos, handRot);

                var hip = pelvisPos + right * side * 0.1f;
                float ph = walkPhase + (s == 0 ? 0 : Mathf.PI);
                var footPos = new Vector3(hip.x, feet.y + 0.02f, hip.z) + fwd * Mathf.Sin(ph) * stride + right * side * 0.04f;
                footPos.y += Mathf.Max(0, Mathf.Cos(ph)) * stride * 0.35f;
                var knee = SolveElbow(hip, footPos + Vector3.up * 0.06f, Thigh, Shin, fwd + Vector3.up * 0.1f);
                Place(thigh[s], hip, knee);
                Place(shin[s], knee, footPos + Vector3.up * 0.06f);
                foot[s].SetPositionAndRotation(footPos, bodyRot);
            }
        }

        public void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = v;
        }

        /* ---------------- ragdoll ---------------- */

        /// <summary>
        /// Copy the current pose into a physics ragdoll and fling it.
        /// The ragdoll cleans itself up after a while.
        /// </summary>
        public GameObject SpawnRagdoll(Vector3 velocity, Vector3 impulse, bool headshot)
        {
            var root = new GameObject("ragdoll");
            var skinM = Prims.Lit(skin, 0.45f, 0f, Surface.Plastic);
            var shirtM = Prims.Lit(shirt, 0.45f, 0f, Surface.Plastic);

            var rPelvis = Part(root.transform, "pelvis", pelvis.position, torso.rotation, 0.12f, 0.24f, shirtM, 9f, false, false);
            CopyDetails(pelvis, rPelvis.transform, 0);
            var rTorso = Part(root.transform, "torso", torso.position, torso.rotation, 0.13f, 0.72f, shirtM, 11f, false, false);
            var rHead = Part(root.transform, "head", head.position, head.rotation, 0.12f, 0.27f, skinM, 4f, true, false);
            // the real head and body shapes (copied so the ragdoll keeps its look)
            CopyDetails(head, rHead.transform, 0);
            CopyDetails(torso, rTorso.transform, 0);
            Joint(rTorso, rPelvis, torso.position - torso.up * 0.27f, 25, 30, 20);
            Joint(rHead, rTorso, head.position - Vector3.up * 0.13f, 30, 40, 40);
            var parts = new System.Collections.Generic.List<Rigidbody> { rPelvis, rTorso, rHead };
            for (int s = 0; s < 2; s++)
            {
                var ua = SegPart(root.transform, upperArm[s], 0.05f, Worn(upperArm[s]), 2.5f);
                var la = SegPart(root.transform, lowerArm[s], 0.042f, Worn(lowerArm[s]), 1.8f);
                var th = SegPart(root.transform, thigh[s], 0.07f, Worn(thigh[s]), 6f);
                var sh = SegPart(root.transform, shin[s], 0.06f, Worn(shin[s]), 4.5f);
                // hands (gloves and all) ride on the forearms, feet (and shoes) on the shins
                CopyDetails(hand[s], la.transform, 0);
                CopyDetails(foot[s], sh.transform, 0);
                Joint(ua, rTorso, upperArm[s].position - upperArm[s].up * Half(upperArm[s]), 80, 80, 60);
                Joint(la, ua, lowerArm[s].position - lowerArm[s].up * Half(lowerArm[s]), 5, 100, 10);
                Joint(th, rPelvis, thigh[s].position - thigh[s].up * Half(thigh[s]), 40, 60, 30);
                Joint(sh, th, shin[s].position - shin[s].up * Half(shin[s]), 5, 100, 10);
                parts.Add(ua); parts.Add(la); parts.Add(th); parts.Add(sh);
            }
            foreach (var rb in parts) Rb.SetVelocity(rb, velocity);
            var hitPart = headshot ? rHead : rTorso;
            hitPart.AddForce(impulse, ForceMode.Impulse);
            rPelvis.AddForce(impulse * 0.4f + Vector3.up * 2f, ForceMode.Impulse);
            root.AddComponent<RagdollLife>();
            AudioManager.Sfx("flop", torso.position, 1f, Random.Range(0.85f, 1.15f));
            return root;
        }

        static Material Worn(Transform seg) => seg.GetChild(0).GetComponent<Renderer>().sharedMaterial;

        /// <summary>Copy a body part's shapes (from child <paramref name="first"/> on) onto a ragdoll part.</summary>
        static void CopyDetails(Transform from, Transform to, int first = 1)
        {
            for (int i = first; i < from.childCount; i++)
            {
                var c = from.GetChild(i);
                var copy = Instantiate(c.gameObject, to);
                copy.transform.SetPositionAndRotation(c.position, c.rotation);
                copy.transform.localScale = c.lossyScale;
                foreach (var col in copy.GetComponentsInChildren<Collider>()) Destroy(col);
                foreach (var rend in copy.GetComponentsInChildren<Renderer>(true)) rend.enabled = true;
            }
        }

        static float Half(Transform seg) => seg.GetChild(0).localScale.y - seg.GetChild(0).localScale.x * 0.5f;

        static Rigidbody Part(Transform parent, string name, Vector3 pos, Quaternion rot, float radius, float height, Material m, float mass, bool sphere = false, bool visual = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            if (sphere)
            {
                var c = go.AddComponent<SphereCollider>(); c.radius = radius;
                if (visual) Prims.Ball(go.transform, Vector3.zero, new Vector3(0.24f, 0.26f, 0.25f), m, false);
            }
            else
            {
                var c = go.AddComponent<CapsuleCollider>(); c.radius = radius; c.height = height; c.direction = 1;
                if (visual) Prims.Make(PrimitiveType.Capsule, go.transform, Vector3.zero, new Vector3(radius * 2, height * 0.5f, radius * 2), m, false);
            }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            Rb.SetDamping(rb, 0.05f, 0.6f);
            rb.solverIterations = 12;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return rb;
        }

        static Rigidbody SegPart(Transform parent, Transform seg, float radius, Material m, float mass)
        {
            float len = (seg.GetChild(0).localScale.y * 2f) - radius;
            return Part(parent, seg.name, seg.position, seg.rotation, radius, Mathf.Max(radius * 2.1f, len), m, mass);
        }

        static void Joint(Rigidbody child, Rigidbody parentBody, Vector3 worldAnchor, float twist, float swing1, float swing2)
        {
            var j = child.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = parentBody;
            j.anchor = child.transform.InverseTransformPoint(worldAnchor);
            j.axis = Vector3.right;
            j.swingAxis = Vector3.forward;
            j.lowTwistLimit = new SoftJointLimit { limit = -twist };
            j.highTwistLimit = new SoftJointLimit { limit = twist };
            j.swing1Limit = new SoftJointLimit { limit = swing1 };
            j.swing2Limit = new SoftJointLimit { limit = swing2 };
            j.enableProjection = true;
        }
    }
}
