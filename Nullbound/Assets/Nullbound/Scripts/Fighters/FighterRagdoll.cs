using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// A real physics ragdoll for a fighter's model: rigid bodies for the hips, chest, head, upper and lower arms and
    /// legs, joined with limited joints, colliding with the world (floors, walls, props) but not with anyone's
    /// character controller. Made when a fighter is knocked flying by a special, thrown, or knocked out, and removed
    /// when they get up. The bodies live apart from the model; every frame their poses are copied onto its bones.
    ///
    /// It's a local effect (each game simulates its own), kept in step with the networked fighter by a spring that
    /// pulls the hips and chest towards where the fighter really is and how fast it's moving, so the body flies and
    /// rolls where the fighter goes while the arms, legs and head flop freely.
    /// </summary>
    public class FighterRagdoll
    {
        struct Part { public Bone bone; public Rigidbody rb; }
        struct Follower { public Bone bone, parent; public Vector3 pos; public Quaternion rot; }

        readonly List<Part> parts = new List<Part>();
        readonly List<Follower> followers = new List<Follower>();
        GameObject root;
        Driver driver;

        public bool Active => root != null;

        /// <summary>Build the ragdoll from the model's current pose and throw it with `velocity` (+ `push` on the chest).</summary>
        public void Begin(Transform[] bones, BodySpec b, float w, Vector3 velocity, Vector3 push, IEnumerable<Collider> ignore)
        {
            End();
            root = new GameObject("Ragdoll");
            driver = root.AddComponent<Driver>();
            parts.Clear();
            followers.Clear();

            Rigidbody Make(Bone bone, float mass)
            {
                var t = bones[(int)bone];
                var go = new GameObject(bone.ToString());
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(t.position, t.rotation);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = mass * w;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 1.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = 12;
                rb.linearVelocity = velocity;
                parts.Add(new Part { bone = bone, rb = rb });
                return rb;
            }

            void Capsule(Rigidbody rb, float length, float thick)
            {
                var c = rb.gameObject.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = Mathf.Clamp(thick * 0.5f, 0.03f, 0.2f);
                c.height = Mathf.Max(length, c.radius * 2f);
                c.center = new Vector3(0f, length * 0.5f, 0f);
            }

            void Joint(Rigidbody child, Rigidbody parent, Vector3 worldAnchor, Vector3 axis, Vector3 swingAxis, float lowTwist, float highTwist, float swing1, float swing2)
            {
                var j = child.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                j.anchor = child.transform.InverseTransformPoint(worldAnchor);
                j.autoConfigureConnectedAnchor = true;
                j.axis = axis;
                j.swingAxis = swingAxis;
                j.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
                j.highTwistLimit = new SoftJointLimit { limit = highTwist };
                j.swing1Limit = new SoftJointLimit { limit = swing1 };
                j.swing2Limit = new SoftJointLimit { limit = swing2 };
                j.enableProjection = true;
                j.projectionDistance = 0.04f;
                j.projectionAngle = 15f;
            }

            Vector3 P(Bone bone) => bones[(int)bone].position;

            // Core.
            var hips = Make(Bone.Hips, 12f);
            hips.gameObject.AddComponent<BoxCollider>().size = new Vector3(b.hipW * 1.1f, 0.2f, b.chestD) * w;
            var chest = Make(Bone.Chest, 16f);
            var chestBox = chest.gameObject.AddComponent<BoxCollider>();
            chestBox.size = new Vector3(b.chestW, b.chestH * 1.15f, b.chestD) * w;
            Joint(chest, hips, Vector3.Lerp(P(Bone.Hips), P(Bone.Chest), 0.45f), Vector3.up, Vector3.right, -20f, 20f, 30f, 20f);
            var head = Make(Bone.Head, 5f);
            head.gameObject.AddComponent<SphereCollider>().radius = 0.12f * b.headScale * w;
            Joint(head, chest, P(Bone.Neck), Vector3.up, Vector3.right, -45f, 45f, 40f, 30f);

            // Arms: shoulders swing freely, elbows bend about the bone's side axis.
            foreach (var (upper, fore) in new[] { (Bone.UpperArmL, Bone.ForearmL), (Bone.UpperArmR, Bone.ForearmR) })
            {
                var u = Make(upper, 2.5f);
                Capsule(u, b.upperArm * w, b.armThick * 1.1f * w);
                Joint(u, chest, P(upper), Vector3.up, Vector3.right, -35f, 35f, 85f, 70f);
                var f = Make(fore, 2f);
                Capsule(f, (b.forearm + 0.08f) * w, b.armThick * w);
                Joint(f, u, P(fore), Vector3.right, Vector3.forward, -75f, 75f, 8f, 8f);
            }
            // Legs (spirits have none).
            if (b.legs)
            {
                foreach (var (thigh, shin) in new[] { (Bone.ThighL, Bone.ShinL), (Bone.ThighR, Bone.ShinR) })
                {
                    var t = Make(thigh, 7f);
                    Capsule(t, b.thigh * w, b.legThick * 1.15f * w);
                    Joint(t, hips, P(thigh), Vector3.up, Vector3.right, -25f, 25f, 75f, 40f);
                    var s = Make(shin, 4f);
                    Capsule(s, (b.shin + 0.06f) * w, b.legThick * w);
                    Joint(s, t, P(shin), Vector3.right, Vector3.forward, -70f, 70f, 6f, 6f);
                }
            }

            // Bones without a body ride along with their neighbour.
            void Follow(Bone bone, Bone parent)
            {
                var t = bones[(int)bone];
                var p = bones[(int)parent];
                var inv = Quaternion.Inverse(p.rotation);
                followers.Add(new Follower { bone = bone, parent = parent, pos = inv * (t.position - p.position), rot = inv * t.rotation });
            }
            Follow(Bone.Neck, Bone.Chest);
            Follow(Bone.Back, Bone.Chest);
            Follow(Bone.HandL, Bone.ForearmL);
            Follow(Bone.HandR, Bone.ForearmR);
            if (b.legs) { Follow(Bone.FootL, Bone.ShinL); Follow(Bone.FootR, Bone.ShinR); }
            else foreach (var leg in new[] { Bone.ThighL, Bone.ThighR, Bone.ShinL, Bone.ShinR, Bone.FootL, Bone.FootR }) Follow(leg, Bone.Hips);

            // Don't fight anyone's character controller, and don't tangle with itself.
            var mine = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < mine.Length; i++)
            {
                for (int k = i + 1; k < mine.Length; k++) Physics.IgnoreCollision(mine[i], mine[k]);
                foreach (var c in ignore) if (c != null && !c.isTrigger) Physics.IgnoreCollision(mine[i], c);
            }

            // The hit: an extra shove on the chest and a spin, so it goes over like it was really struck.
            chest.AddForce(push, ForceMode.VelocityChange);
            Vector3 spinAxis = Vector3.Cross(Vector3.up, push.sqrMagnitude > 0.01f ? push.normalized : Vector3.forward);
            chest.AddTorque(spinAxis * Mathf.Clamp(push.magnitude * 1.5f, 2f, 12f), ForceMode.VelocityChange);

            driver.hips = hips;
            driver.chest = chest;
            driver.all = parts.ConvertAll(p => p.rb);
            driver.hipLift = 0.25f * w;
        }

        /// <summary>Where the networked fighter is (feet) and how it's moving: the core is pulled towards that.</summary>
        public void Follow(Vector3 feet, Vector3 velocity, bool lying)
        {
            if (driver == null) return;
            driver.target = feet;
            driver.targetVelocity = velocity;
            driver.lying = lying;
        }

        /// <summary>A hit landing on the ragdoll: knock the nearest body part.</summary>
        public void Push(Vector3 point, Vector3 impulse)
        {
            Rigidbody best = null;
            float bestD = float.MaxValue;
            foreach (var p in parts)
            {
                if (p.rb == null) continue;
                float d = (p.rb.worldCenterOfMass - point).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p.rb; }
            }
            if (best != null) best.AddForceAtPosition(impulse, point, ForceMode.VelocityChange);
        }

        /// <summary>Copy the simulated pose onto the model's bones.</summary>
        public void Apply(Transform[] bones)
        {
            foreach (var p in parts)
                if (p.rb != null) bones[(int)p.bone].SetPositionAndRotation(p.rb.transform.position, p.rb.transform.rotation);
            foreach (var f in followers)
            {
                var parent = bones[(int)f.parent];
                bones[(int)f.bone].SetPositionAndRotation(parent.position + parent.rotation * f.pos, parent.rotation * f.rot);
            }
        }

        public void End()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            driver = null;
            parts.Clear();
            followers.Clear();
        }

        /// <summary>Applies the follow spring in the physics step.</summary>
        class Driver : MonoBehaviour
        {
            public Rigidbody hips, chest;
            public List<Rigidbody> all;
            public Vector3 target, targetVelocity;
            public float hipLift;
            public bool lying;
            bool hasTarget;

            void FixedUpdate()
            {
                if (hips == null) return;
                if (!hasTarget) { hasTarget = target != Vector3.zero; if (!hasTarget) return; }
                Vector3 goal = target + Vector3.up * hipLift;
                Vector3 err = goal - hips.position;
                // Way off (a teleport, or stuck in a wall): move the whole body over.
                if (err.sqrMagnitude > 16f)
                {
                    foreach (var rb in all) if (rb != null) { rb.position += err; rb.linearVelocity = targetVelocity; }
                    return;
                }
                // Firm sideways, soft up and down (gravity does the falling), and match the fighter's speed.
                float side = lying ? 12f : 22f;
                Vector3 acc = new Vector3(err.x * side, err.y * (err.y > 0f ? 10f : 2f), err.z * side) + (targetVelocity - hips.linearVelocity) * (lying ? 2f : 4f);
                acc = Vector3.ClampMagnitude(acc, 60f);
                hips.AddForce(acc, ForceMode.Acceleration);
                if (chest != null) chest.AddForce(acc * 0.6f, ForceMode.Acceleration);
            }
        }
    }
}
