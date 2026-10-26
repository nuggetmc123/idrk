using UnityEngine;
using S = Veylix.MeshKit.Station;

namespace Veylix
{
    /// <summary>
    /// The driver: a full body in a race suit, sitting in the car. Torso with the six-point harness, legs reaching to the
    /// pedals in racing boots, and arms that follow the hands. For the player in the cockpit the arms reach to the VR
    /// gloves (so looking down you see your own arms on the wheel); for AI drivers, and the player's car seen from
    /// outside, the gloved hands hold the steering wheel at quarter to three and turn with it. The suit is in the car's
    /// colours, with the secondary colour on the side panels and sleeves.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after the gloves and the rig have moved this frame
    public class DriverBody : MonoBehaviour
    {
        Car car;
        CarParts parts;
        readonly Transform[] upper = new Transform[2], fore = new Transform[2], elbow = new Transform[2], hand = new Transform[2];
        readonly Vector3[] shoulder = new Vector3[2]; // car-local
        const float UpperLen = 0.29f, ForeLen = 0.27f;

        /// <param name="recline">0 = upright (1970s), 1 = lying back like a modern car.</param>
        public static DriverBody Build(Car car, CarParts parts, Transform root, float eyeY, float headZ, float recline, Color suit, Color accent, Color gloves)
        {
            var A = VXAssets.I;
            var fabric = new Material(VXAssets.Or(A != null ? A.padding : null, "Suit", suit, 0.2f)) { name = "Suit", color = suit };
            if (fabric.HasProperty("_Glossiness")) fabric.SetFloat("_Glossiness", 0.22f);
            var panel = new Material(fabric) { name = "Suit accent", color = accent };
            var black = VXAssets.Or(A != null ? A.interior : null, "Harness", new Color(0.04f, 0.04f, 0.045f), 0.25f);
            var metal = VXAssets.Or(A != null ? A.metal : null, "Buckle", new Color(0.7f, 0.7f, 0.72f), 0.8f);
            var gloveMat = new Material(black) { name = "Gloves", color = gloves };

            var go = new GameObject("Driver");
            go.transform.SetParent(root, false);
            var d = go.AddComponent<DriverBody>();
            d.car = car;
            d.parts = parts;

            // ---- skeleton (car-local): eye -> neck -> pelvis, legs out to the pedals
            Vector3 eye = new Vector3(0f, eyeY, headZ);
            Vector3 neck = eye + new Vector3(0f, -0.2f, -0.03f);
            Vector3 pelvis = eye + new Vector3(0f, Mathf.Lerp(-0.64f, -0.6f, recline), Mathf.Lerp(0.1f, 0.32f, recline));
            Vector3 spine = neck - pelvis;
            float spineLen = spine.magnitude;
            var spineRot = Quaternion.LookRotation(spine / spineLen, Vector3.forward); // local Y = chest facing forward

            var k = new MeshKit();
            // Torso: hips, waist, chest, shoulders, neck.
            k.matrix = Matrix4x4.TRS(pelvis, spineRot, Vector3.one);
            k.Loft(0, new[]
            {
                new S(0f, 0f, 0.17f, 0.12f, 2.6f),
                new S(spineLen * 0.3f, 0.005f, 0.152f, 0.105f, 2.5f),
                new S(spineLen * 0.62f, 0.015f, 0.18f, 0.118f, 2.6f),
                new S(spineLen * 0.86f, 0.005f, 0.2f, 0.1f, 3f),
                new S(spineLen * 1.0f, 0f, 0.07f, 0.065f, 2.2f),
            }, 22, 16, null, 0, 0);
            // Side panels in the accent colour.
            for (int s = -1; s <= 1; s += 2)
                k.Box(1, new Vector3(s * 0.165f, 0f, spineLen * 0.45f), new Vector3(0.02f, 0.13f, spineLen * 0.62f));
            // Six-point harness: shoulder straps, lap belt, buckle.
            for (int s = -1; s <= 1; s += 2)
            {
                k.matrix = Matrix4x4.TRS(pelvis, spineRot, Vector3.one);
                k.Box(2, new Vector3(s * 0.07f, 0.124f, spineLen * 0.55f), new Vector3(0.05f, 0.008f, spineLen * 0.75f));
            }
            k.Box(2, new Vector3(0f, 0.122f, spineLen * 0.12f), new Vector3(0.3f, 0.008f, 0.06f));
            k.matrix = Matrix4x4.TRS(pelvis + spineRot * new Vector3(0f, 0.13f, spineLen * 0.14f), spineRot * Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
            k.Lathe(3, new[] { new Vector2(0.001f, 0f), new Vector2(0.045f, 0f), new Vector2(0.045f, 0.012f), new Vector2(0.001f, 0.014f) }, 16);
            k.matrix = Matrix4x4.identity;

            // Legs: thigh and shin with a bent knee, boots on the pedals.
            float reach = Mathf.Lerp(0.74f, 0.82f, recline);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 hip = pelvis + new Vector3(s * 0.09f, 0.02f, 0.03f);
                Vector3 heel = pelvis + new Vector3(s * 0.11f, Mathf.Lerp(0.02f, 0.1f, recline), reach);
                Vector3 knee = SolveJoint(hip, heel, 0.44f, 0.43f, new Vector3(s * 0.15f, 1f, 0f));
                k.Tube(0, new[] { hip, knee }, 0.075f, 12, true, 0.06f);
                k.Tube(0, new[] { knee, heel }, 0.058f, 12, true, 0.045f);
                k.Tube(1, new[] { Vector3.Lerp(hip, knee, 0.15f), Vector3.Lerp(hip, knee, 0.85f) }, 0.077f, 12, false, 0.062f); // accent stripe band
                // Boot: heel to toe, pointing up the pedal.
                Vector3 toe = heel + new Vector3(0f, 0.09f, 0.16f);
                k.Tube(2, new[] { heel + new Vector3(0f, -0.02f, -0.03f), toe }, 0.045f, 10, true, 0.035f);
            }

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = k.Build(car.driverName + " driver", 4);
            body.AddComponent<MeshRenderer>().sharedMaterials = new[] { fabric, panel, black, metal };

            // ---- arms (posed every frame)
            Vector3 chest = pelvis + spine * 0.86f;
            Mesh limb = LimbMesh(), joint = JointMesh(), glove = GloveMesh();
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                d.shoulder[i] = chest + new Vector3(s * 0.2f, 0.01f, 0f);
                d.upper[i] = Part(go.transform, "UpperArm", limb, new[] { fabric });
                d.fore[i] = Part(go.transform, "Forearm", limb, new[] { panel });
                d.elbow[i] = Part(go.transform, "Elbow", joint, new[] { fabric });
                d.hand[i] = Part(go.transform, "Hand", glove, new[] { gloveMat });
                var sh = Part(go.transform, "Shoulder", joint, new[] { fabric });
                sh.localPosition = d.shoulder[i];
                sh.localScale = Vector3.one * 0.14f;
            }
            d.Pose();
            return d;
        }

        /// <summary>
        /// Your driver standing up, helmet on, for the garage (it stands by your car and shows your design while you
        /// customise it). Static pose: weight on both feet, arms relaxed.
        /// </summary>
        public static GameObject BuildStanding(Transform parent, DriverLook look)
        {
            var A = VXAssets.I;
            var fabric = new Material(VXAssets.Or(A != null ? A.padding : null, "Suit", look.Suit, 0.2f)) { name = "Suit", color = look.Suit };
            if (fabric.HasProperty("_Glossiness")) fabric.SetFloat("_Glossiness", 0.22f);
            var panel = new Material(fabric) { name = "Suit accent", color = look.SuitAccent };
            var dark = VXAssets.Or(A != null ? A.interior : null, "Boots", new Color(0.04f, 0.04f, 0.045f), 0.3f);
            var gloveMat = new Material(dark) { name = "Gloves", color = look.Gloves };

            var go = new GameObject("Driver (standing)");
            go.transform.SetParent(parent, false);
            var k = new MeshKit();
            Vector3 pelvis = new Vector3(0f, 0.95f, 0f), neck = new Vector3(0f, 1.5f, -0.01f);
            Vector3 spine = neck - pelvis;
            float len = spine.magnitude;
            var rot = Quaternion.LookRotation(spine / len, Vector3.forward);
            k.matrix = Matrix4x4.TRS(pelvis, rot, Vector3.one);
            k.Loft(0, new[]
            {
                new S(-0.04f, 0f, 0.165f, 0.115f, 2.6f),
                new S(len * 0.3f, 0.005f, 0.148f, 0.1f, 2.5f),
                new S(len * 0.62f, 0.012f, 0.18f, 0.115f, 2.6f),
                new S(len * 0.86f, 0.004f, 0.2f, 0.1f, 3f),
                new S(len * 1.0f, 0f, 0.07f, 0.065f, 2.2f),
            }, 22, 16, null, 0, 0);
            for (int sd = -1; sd <= 1; sd += 2) k.Box(1, new Vector3(sd * 0.158f, 0f, len * 0.45f), new Vector3(0.02f, 0.12f, len * 0.62f));
            // Waistband: a slightly fuller ring round the hips in the accent colour.
            k.Loft(1, new[] { new S(-0.02f, 0f, 0.169f, 0.119f, 2.6f), new S(0.06f, 0.002f, 0.163f, 0.113f, 2.6f) }, 22, 2, null, -1, -1);
            k.matrix = Matrix4x4.identity;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 hip = pelvis + new Vector3(sd * 0.09f, -0.02f, 0f), knee = new Vector3(sd * 0.1f, 0.5f, 0.02f), ankle = new Vector3(sd * 0.11f, 0.1f, -0.01f);
                k.Tube(0, new[] { hip, knee }, 0.078f, 12, true, 0.062f);
                k.Tube(0, new[] { knee, ankle }, 0.058f, 12, true, 0.046f);
                // Stripe down the outside of each leg.
                k.Box(1, Vector3.Lerp(hip, knee, 0.5f) + new Vector3(sd * 0.072f, 0f, 0f), new Vector3(0.014f, Vector3.Distance(hip, knee) * 0.9f, 0.04f));
                k.Box(1, Vector3.Lerp(knee, ankle, 0.5f) + new Vector3(sd * 0.053f, 0f, 0f), new Vector3(0.012f, Vector3.Distance(knee, ankle) * 0.9f, 0.034f));
                k.Tube(2, new[] { ankle + new Vector3(0f, -0.04f, -0.05f), ankle + new Vector3(0f, -0.06f, 0.17f) }, 0.05f, 10, true, 0.042f); // boot
                Vector3 sh = new Vector3(sd * 0.2f, 1.39f, -0.01f), el = new Vector3(sd * 0.265f, 1.13f, -0.03f), wr = new Vector3(sd * 0.285f, 0.88f, 0.03f);
                k.Tube(0, new[] { sh, el }, 0.06f, 12, true, 0.052f);
                k.Tube(1, new[] { el, wr }, 0.05f, 12, true, 0.042f);
            }
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = k.Build("standing driver", 3);
            body.AddComponent<MeshRenderer>().sharedMaterials = new[] { fabric, panel, dark };
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var hand = Part(go.transform, "Hand", GloveMesh(), new[] { gloveMat });
                hand.localPosition = new Vector3(sd * 0.29f, 0.84f, 0.03f);
                hand.localRotation = Quaternion.Euler(95f, 0f, sd * -10f);
                var shoulder = Part(go.transform, "Shoulder", JointMesh(), new[] { fabric });
                shoulder.localPosition = new Vector3(sd * 0.2f, 1.405f, -0.01f);
                shoulder.localScale = Vector3.one * 0.165f;
            }
            var hm = CarModel.HelmetMaterials(look, null);
            hm[2] = fabric; // out of the car the collar is the suit's
            var helmet = Part(go.transform, "Helmet", CarModel.HelmetMesh(), hm);
            helmet.localPosition = new Vector3(0f, 1.67f, 0.01f);
            return go;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return g.transform;
        }

        static Mesh limbMesh, jointMesh, gloveMesh;

        /// Unit-length tapered sleeve along +Z (scaled per arm).
        static Mesh LimbMesh()
        {
            if (limbMesh != null) return limbMesh;
            var k = new MeshKit();
            k.Tube(0, new[] { Vector3.zero, new Vector3(0f, 0f, 1f) }, 1f, 12, true, 0.8f);
            return limbMesh = k.Build("limb");
        }

        static Mesh JointMesh()
        {
            if (jointMesh != null) return jointMesh;
            var k = new MeshKit();
            var prof = new Vector2[9];
            for (int i = 0; i < 9; i++) { float a = -Mathf.PI / 2f + i / 8f * Mathf.PI; prof[i] = new Vector2(Mathf.Max(0.001f, Mathf.Cos(a) * 0.5f), Mathf.Sin(a) * 0.5f); }
            k.Lathe(0, prof, 12);
            return jointMesh = k.Build("joint");
        }

        /// A gloved hand wrapped round a rim (for drivers seen from outside).
        static Mesh GloveMesh()
        {
            if (gloveMesh != null) return gloveMesh;
            var k = new MeshKit();
            k.Loft(0, new[]
            {
                new S(-0.06f, 0f, 0.03f, 0.028f, 2.2f),
                new S(0.0f, 0f, 0.045f, 0.03f, 2.6f),
                new S(0.05f, -0.01f, 0.045f, 0.035f, 2.6f),
                new S(0.08f, -0.03f, 0.04f, 0.03f, 2.4f),
            }, 14, 8, null, 0, 0);
            return gloveMesh = k.Build("glove");
        }

        /// Two-bone joint (elbow / knee) between a root and a target, bending towards 'pole'.
        static Vector3 SolveJoint(Vector3 root, Vector3 target, float a, float b, Vector3 pole)
        {
            Vector3 d = target - root;
            float dist = Mathf.Clamp(d.magnitude, 0.05f, a + b - 0.001f);
            Vector3 dir = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
            float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 p = pole - dir * Vector3.Dot(pole, dir);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.up;
            return root + dir * (a * cosA) + p.normalized * (a * sinA);
        }

        void LateUpdate() => Pose();

        void Pose()
        {
            if (car == null) return;
            var rig = Rig.I;
            bool ownHands = rig != null && rig.Car == car && rig.view == Rig.View.Cockpit && rig.leftGlove != null && rig.rightGlove != null;
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 target;
                if (ownHands)
                {
                    var g = i == 0 ? rig.leftGlove : rig.rightGlove;
                    target = transform.InverseTransformPoint(g.WristPosition);
                }
                else
                {
                    // Hands at quarter to three on the rim, turning with the wheel.
                    var grip = parts != null ? parts.steeringWheelGrip : null;
                    if (grip != null)
                    {
                        float r = parts.wheelRadius > 0f ? parts.wheelRadius : 0.14f;
                        Vector3 w = grip.TransformPoint(new Vector3(-s * (r + 0.005f), 0.01f, 0f)); // the wheel faces the driver (rotated 180)
                        target = transform.InverseTransformPoint(w) + new Vector3(0f, 0f, -0.06f);
                    }
                    else target = shoulder[i] + new Vector3(s * 0.1f, -0.15f, 0.4f);
                }
                Vector3 sh = shoulder[i];
                Vector3 el = SolveJoint(sh, target, UpperLen, ForeLen, new Vector3(s * 0.55f, -1f, 0f)); // elbows down and a little out, inside the cockpit
                Vector3 toHand = target - el;
                Vector3 wrist = toHand.magnitude > ForeLen ? el + toHand.normalized * ForeLen : target;
                Place(upper[i], sh, el, 0.058f);
                Place(fore[i], el, wrist, 0.048f);
                elbow[i].localPosition = el;
                elbow[i].localScale = Vector3.one * 0.105f;
                hand[i].gameObject.SetActive(!ownHands); // the VR gloves are the hands in your own cockpit
                if (!ownHands)
                {
                    hand[i].localPosition = wrist;
                    hand[i].localRotation = Quaternion.LookRotation((target - el).sqrMagnitude > 1e-6f ? (target - el).normalized : Vector3.forward, Vector3.up) * Quaternion.Euler(0f, 0f, s * 70f);
                }
            }
        }

        static void Place(Transform t, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            t.localPosition = a;
            t.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
            t.localScale = new Vector3(radius, radius, len);
        }
    }
}
