using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The driver: a racing driver in a painted race suit (side panels, shoulder straps, belt, zip, chest sponsor and
    /// number, striped sleeves and legs), race boots, gloves, a HANS device and the six-point harness, sitting in the
    /// car. The arms follow the hands. For the player in the cockpit the arms reach to the VR gloves, so looking down
    /// you see your own arms on the wheel. For AI drivers, and the player's car seen from outside, the gloved hands
    /// wrap round the rim at quarter to three and turn with the wheel.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after the gloves and the rig have moved this frame
    public class DriverBody : MonoBehaviour
    {
        Car car;
        CarParts parts;
        readonly Transform[] upper = new Transform[2], fore = new Transform[2], elbow = new Transform[2], hand = new Transform[2];
        readonly Vector3[] shoulder = new Vector3[2]; // car-local

        // Material slots shared by the seated and standing bodies (Boot and Glove meshes use two slots each).
        const int Suit = 0, Harness = 1, Buckle = 2, Boot = 3, Hans = 5, Glove = 6, Slots = 8;

        static readonly Dictionary<string, Texture2D> suitTextures = new Dictionary<string, Texture2D>();

        /// suit, harness, buckle metal, boot, sole, HANS carbon, glove, glove cuff
        static Material[] Materials(Color suit, Color accent, Color gloves, int number, string sponsor)
        {
            var A = VXAssets.I;
            string key = ColorUtility.ToHtmlStringRGB(suit) + ColorUtility.ToHtmlStringRGB(accent) + number + sponsor;
            if (!suitTextures.TryGetValue(key, out var tex) || tex == null)
                suitTextures[key] = tex = BodyKit.SuitTexture(suit, accent, number, sponsor);
            var fabric = new Material(VXAssets.Or(A != null ? A.padding : null, "Suit", Color.white, 0.2f)) { name = "Suit", color = Color.white, mainTexture = tex };
            if (fabric.HasProperty("_Glossiness")) fabric.SetFloat("_Glossiness", 0.2f);
            var black = VXAssets.Or(A != null ? A.interior : null, "Harness", new Color(0.04f, 0.04f, 0.045f), 0.25f);
            var metal = VXAssets.Or(A != null ? A.metal : null, "Buckle", new Color(0.7f, 0.7f, 0.72f), 0.8f);
            var boot = new Material(black) { name = "Boots", color = Color.Lerp(gloves, suit, 0.25f) };
            if (boot.HasProperty("_Glossiness")) boot.SetFloat("_Glossiness", 0.45f);
            var carbon = VXAssets.Or(A != null ? A.carbon : null, "HANS", new Color(0.05f, 0.05f, 0.06f), 0.7f);
            var glove = new Material(black) { name = "Gloves", color = gloves };
            if (glove.HasProperty("_Glossiness")) glove.SetFloat("_Glossiness", 0.35f);
            var cuff = new Material(black) { name = "Glove cuff", color = accent };
            return new[] { fabric, black, metal, boot, black, carbon, glove, cuff };
        }

        /// <param name="recline">0 = upright (1970s), 1 = lying back like a modern car.</param>
        public static DriverBody Build(Car car, CarParts parts, Transform root, float eyeY, float headZ, float recline, Color suit, Color accent, Color gloves, int number, string sponsor)
        {
            var mats = Materials(suit, accent, gloves, number, sponsor);
            var go = new GameObject("Driver");
            go.transform.SetParent(root, false);
            var d = go.AddComponent<DriverBody>();
            d.car = car;
            d.parts = parts;

            // ---- skeleton (car-local): eye -> neck -> pelvis, legs out to the pedals
            Vector3 eye = new Vector3(0f, eyeY, headZ);
            Vector3 neck = eye + new Vector3(0f, -0.2f, -0.03f);
            Vector3 pelvis = eye + new Vector3(0f, Mathf.Lerp(-0.64f, -0.6f, recline), Mathf.Lerp(0.1f, 0.32f, recline));
            var pieces = new List<BodyKit.Piece>();
            var harness = new MeshKit();
            Matrix4x4 torso = AddTorso(pieces, harness, pelvis, neck);

            // Legs: thigh and shin with a bent knee, boots on the pedals.
            float reach = Mathf.Lerp(0.74f, 0.82f, recline);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 hip = pelvis + new Vector3(s * 0.09f, 0.02f, 0.03f);
                Vector3 heel = pelvis + new Vector3(s * 0.11f, Mathf.Lerp(0.02f, 0.1f, recline), reach);
                Vector3 toe = new Vector3(s * 0.08f, 0.5f, 1f).normalized;         // up the pedal, a little splayed
                Vector3 bootUp = Vector3.Cross(toe, Vector3.left).normalized;       // perpendicular: up and back
                Vector3 ankle = heel + bootUp * 0.08f;
                Vector3 knee = SolveJoint(hip, ankle, BodyKit.Thigh, BodyKit.Shin, new Vector3(s * 0.15f, 1f, 0f));
                AddLeg(pieces, hip, knee, ankle, Matrix4x4.TRS(heel, Quaternion.LookRotation(toe, bootUp), Vector3.one), Vector3.up, new Vector3(0f, 1f, 1f));
            }

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            pieces.Add(new BodyKit.Piece(harness.Build("harness", 2), Matrix4x4.identity, Harness)); // straps, then the buckle
            body.AddComponent<MeshFilter>().sharedMesh = BodyKit.Combine(car.driverName + " driver", pieces, Slots);
            body.AddComponent<MeshRenderer>().sharedMaterials = mats;

            // ---- arms (posed every frame)
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                d.shoulder[i] = torso.MultiplyPoint3x4(ShoulderLocal(s));
                d.upper[i] = Part(go.transform, "UpperArm", BodyKit.UpperArmMesh(), new[] { mats[Suit] });
                d.fore[i] = Part(go.transform, "Forearm", BodyKit.ForearmMesh(), new[] { mats[Suit] });
                d.elbow[i] = Part(go.transform, "Elbow", BodyKit.Joint(), new[] { mats[Suit] });
                d.hand[i] = Part(go.transform, "Hand", BodyKit.Hand(1f), new[] { mats[Glove], mats[Glove + 1] });
            }
            // Other drivers sit deep in their cockpits: their shadows can't be seen, so don't pay for them.
            if (!car.isPlayer)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.Pose();
            return d;
        }

        /// Shoulder joint in torso space for side s (-1 left, +1 right). The torso's local X points to the driver's left
        /// (it's LookRotation(spine, forward)), hence the minus.
        static Vector3 ShoulderLocal(float s) => new Vector3(-s * 0.172f, -0.008f, BodyKit.SpineLen * 0.84f);

        /// Torso, shoulders, HANS and (when 'harness' is given) the belts. Returns the torso's matrix (z scaled to the spine).
        static Matrix4x4 AddTorso(List<BodyKit.Piece> pieces, MeshKit harness, Vector3 pelvis, Vector3 neck)
        {
            Vector3 spine = neck - pelvis;
            float len = spine.magnitude;
            var rot = Quaternion.LookRotation(spine / len, Vector3.forward); // local Y = chest, facing forward
            float sc = Mathf.Clamp(len / (BodyKit.SpineLen * 0.97f), 0.8f, 1.15f);
            var torso = Matrix4x4.TRS(pelvis, rot, new Vector3(1f, 1f, sc));
            pieces.Add(new BodyKit.Piece(BodyKit.Torso(), torso, Suit));
            if (harness != null) // in the car: the HANS device on the shoulders
                pieces.Add(new BodyKit.Piece(BodyKit.Hans(), Matrix4x4.TRS(torso.MultiplyPoint3x4(new Vector3(0f, -0.005f, BodyKit.SpineLen * 0.955f)), rot, Vector3.one), Hans));
            for (int s = -1; s <= 1; s += 2)
                pieces.Add(new BodyKit.Piece(BodyKit.Joint(), Matrix4x4.TRS(torso.MultiplyPoint3x4(ShoulderLocal(s)), rot, Vector3.one * 0.125f), Suit));
            if (harness == null) return torso;

            // Six-point harness lying on the suit: shoulder straps over the HANS, lap belt, crotch straps, buckle.
            float L = BodyKit.SpineLen;
            harness.matrix = torso;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.072f;
                var pts = new List<Vector3>();
                for (float f = 0.16f; f <= 0.9f; f += 0.06f) pts.Add(new Vector3(x, BodyKit.TorsoSurface(f, x) + 0.007f, f * L));
                pts.Add(new Vector3(x * 1.08f, BodyKit.TorsoSurface(0.93f, x) + 0.02f, 0.94f * L));
                pts.Add(new Vector3(x * 1.12f, 0.035f, 1.0f * L));
                pts.Add(new Vector3(x * 1.15f, -0.04f, 1.02f * L));
                pts.Add(new Vector3(x * 1.2f, -0.11f, 0.97f * L));
                BodyKit.Strap(harness, 0, pts.ToArray(), Vector3.right, 0.05f);
                var crotch = new List<Vector3>();
                for (float f = 0.15f; f >= -0.005f; f -= 0.05f) crotch.Add(new Vector3(s * 0.035f, BodyKit.TorsoSurface(Mathf.Max(0f, f), s * 0.035f) + 0.007f, f * L));
                BodyKit.Strap(harness, 0, crotch.ToArray(), Vector3.right, 0.04f);
            }
            var lap = new List<Vector3>();
            for (float x = -0.155f; x <= 0.1551f; x += 0.031f) lap.Add(new Vector3(x, BodyKit.TorsoSurface(0.15f, x) + 0.008f, 0.15f * L));
            BodyKit.Strap(harness, 0, lap.ToArray(), Vector3.forward, 0.06f);
            harness.matrix = torso * Matrix4x4.TRS(new Vector3(0f, BodyKit.TorsoSurface(0.15f, 0f) + 0.011f, 0.15f * L), Quaternion.Euler(0f, 0f, 90f), Vector3.one); // lathe axis X -> out of the chest
            harness.Lathe(1, new[] { new Vector2(0.001f, 0f), new Vector2(0.042f, 0f), new Vector2(0.042f, 0.01f), new Vector2(0.034f, 0.014f), new Vector2(0.001f, 0.015f) }, 20);
            harness.matrix = Matrix4x4.identity;
            return torso;
        }

        /// Thigh, knee, shin and boot. 'bootM' places the boot (heel at its origin).
        static void AddLeg(List<BodyKit.Piece> pieces, Vector3 hip, Vector3 knee, Vector3 ankle, Matrix4x4 bootM, Vector3 thighFront, Vector3 shinFront)
        {
            pieces.Add(new BodyKit.Piece(BodyKit.ThighMesh(), BodyKit.Between(hip, knee, BodyKit.Thigh, thighFront), Suit));
            pieces.Add(new BodyKit.Piece(BodyKit.Joint(), Matrix4x4.TRS(knee, Quaternion.LookRotation(knee - hip, thighFront), Vector3.one * 0.11f), Suit));
            pieces.Add(new BodyKit.Piece(BodyKit.ShinMesh(), BodyKit.Between(knee, ankle, BodyKit.Shin, shinFront), Suit));
            pieces.Add(new BodyKit.Piece(BodyKit.Boot(), bootM, Boot));
        }

        /// <summary>
        /// Your driver standing up, helmet on, for the garage (it stands by your car and shows your design while you
        /// customise it). Weight on both feet, arms relaxed.
        /// </summary>
        public static GameObject BuildStanding(Transform parent, DriverLook look)
        {
            var mats = Materials(look.Suit, look.SuitAccent, look.Gloves, look.number, "VEYLIX");
            var go = new GameObject("Driver (standing)");
            go.transform.SetParent(parent, false);
            var pieces = new List<BodyKit.Piece>();
            Vector3 pelvis = new Vector3(0f, 0.96f, 0f), neck = new Vector3(0f, 1.49f, -0.012f);
            var torso = AddTorso(pieces, null, pelvis, neck);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 hip = new Vector3(s * 0.09f, 0.94f, 0.005f), ankle = new Vector3(s * 0.115f, 0.125f, -0.015f);
                Vector3 knee = SolveJoint(hip, ankle, BodyKit.Thigh, BodyKit.Shin, new Vector3(0f, 0f, 1f));
                var bootM = Matrix4x4.TRS(new Vector3(s * 0.115f, 0.043f, -0.01f), Quaternion.Euler(0f, s * 7f, 0f), Vector3.one);
                AddLeg(pieces, hip, knee, ankle, bootM, Vector3.forward, Vector3.forward);

                // Arms hanging relaxed, elbows slightly bent, hands by the thighs.
                Vector3 sh = torso.MultiplyPoint3x4(ShoulderLocal(s));
                Vector3 el = sh + new Vector3(s * 0.05f, -1f, -0.08f).normalized * BodyKit.UpperArm;
                Vector3 fdir = new Vector3(s * 0.03f, -1f, 0.22f).normalized;
                Vector3 wr = el + fdir * BodyKit.Forearm;
                pieces.Add(new BodyKit.Piece(BodyKit.UpperArmMesh(), BodyKit.Between(sh, el, BodyKit.UpperArm, Vector3.forward), Suit));
                pieces.Add(new BodyKit.Piece(BodyKit.Joint(), Matrix4x4.TRS(el, Quaternion.identity, Vector3.one * 0.088f), Suit));
                pieces.Add(new BodyKit.Piece(BodyKit.ForearmMesh(), BodyKit.Between(el, wr, BodyKit.Forearm, Vector3.forward), Suit));
                // Palm towards the thigh, thumb forward. The hand mesh is a left hand; the right is its mirror image.
                var hrot = Quaternion.LookRotation(fdir, new Vector3(s, 0f, 0f));
                pieces.Add(new BodyKit.Piece(BodyKit.Hand(0f), Matrix4x4.TRS(wr, hrot, new Vector3(s < 0 ? 1f : -1f, 1f, 1f)), Glove));
            }
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = BodyKit.Combine("standing driver", pieces, Slots);
            body.AddComponent<MeshRenderer>().sharedMaterials = mats;

            var helmet = Part(go.transform, "Helmet", CarModel.HelmetMesh(), CarModel.HelmetMaterials(look, null));
            helmet.localPosition = new Vector3(0f, neck.y + 0.163f, 0.012f);
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

        // The headset and controllers get their final pose just before rendering: pose the arms again then, so your
        // sleeves don't trail a frame behind your hands.
        void OnEnable() { Application.onBeforeRender += BeforeRender; }
        void OnDisable() { Application.onBeforeRender -= BeforeRender; }
        void BeforeRender() { if (OwnHands) Pose(); }

        bool OwnHands
        {
            get
            {
                var rig = Rig.I;
                return car != null && rig != null && rig.Car == car && rig.view == Rig.View.Cockpit && rig.leftGlove != null && rig.rightGlove != null;
            }
        }

        // Where the rim sits in a gripping hand (hand space: wrist at the origin, palm side -Y, fingers +Z).
        static readonly Vector3 RimInFist = new Vector3(0f, -0.032f, 0.07f);

        void Pose()
        {
            if (car == null) return;
            var rig = Rig.I;
            bool ownHands = OwnHands;
            // In your own car the shoulders lean with your head a little, so reaching forward or leaning into a
            // corner moves your arms with you.
            Vector3 lean = Vector3.zero;
            if (ownHands && rig.head != null)
            {
                Vector3 headNow = transform.InverseTransformPoint(rig.head.position);
                Vector3 headRest = parts != null && parts.eyePoint != null ? transform.InverseTransformPoint(parts.eyePoint.position) : (shoulder[0] + shoulder[1]) * 0.5f + new Vector3(0f, 0.27f, 0.05f);
                lean = Vector3.ClampMagnitude((headNow - headRest) * 0.6f, 0.09f);
            }
            var grip = parts != null ? parts.steeringWheelGrip : null;
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 sh = shoulder[i] + lean;
                Vector3 target;
                Quaternion handRot = Quaternion.identity;
                if (ownHands)
                {
                    var g = i == 0 ? rig.leftGlove : rig.rightGlove;
                    target = transform.InverseTransformPoint(g.WristPosition);
                }
                else if (grip != null)
                {
                    // Hands at quarter to three, fingers wrapped round the rim, turning with the wheel.
                    float r = parts.wheelRadius > 0f ? parts.wheelRadius : 0.14f;
                    Vector3 rim = transform.InverseTransformPoint(grip.TransformPoint(new Vector3(-s * r, 0f, 0f))); // the wheel faces the driver (rotated 180)
                    Vector3 centre = transform.InverseTransformPoint(grip.position);
                    Vector3 axis = transform.InverseTransformDirection(grip.forward);
                    if (axis.z > 0f) axis = -axis; // towards the driver
                    Vector3 outward = (rim - centre).normalized;
                    Vector3 fingers = Vector3.ProjectOnPlane(-axis, outward).normalized; // reaching forward past the rim
                    handRot = Quaternion.LookRotation(fingers, outward + axis * 0.3f);    // back of the hand out, palm on the rim
                    target = rim - handRot * RimInFist;
                }
                else target = sh + new Vector3(s * 0.1f, -0.15f, 0.4f);

                // Arms longer than the model's (or a hand stretched out): stretch the sleeves a little rather than
                // leave the glove floating free of the arm.
                float reach = (target - sh).magnitude, full = BodyKit.UpperArm + BodyKit.Forearm;
                float stretch = ownHands ? Mathf.Clamp(reach / (full * 0.97f), 1f, 1.35f) : 1f;
                float ua = BodyKit.UpperArm * stretch, fa = BodyKit.Forearm * stretch;
                Vector3 el = SolveJoint(sh, target, ua, fa, new Vector3(s * 0.55f, -1f, 0f)); // elbows down and a little out
                Vector3 toHand = target - el;
                Vector3 wrist = toHand.magnitude > fa ? el + toHand.normalized * fa : target;
                Place(upper[i], sh, el, BodyKit.UpperArm);
                Place(fore[i], el, wrist, BodyKit.Forearm);
                elbow[i].localPosition = el;
                elbow[i].localScale = Vector3.one * 0.088f;
                hand[i].gameObject.SetActive(!ownHands); // the VR gloves are the hands in your own cockpit
                if (!ownHands)
                {
                    if (grip == null) handRot = Quaternion.LookRotation(toHand.sqrMagnitude > 1e-6f ? toHand.normalized : Vector3.forward, new Vector3(s, 1f, 0f));
                    hand[i].localPosition = wrist;
                    hand[i].localRotation = handRot;
                    hand[i].localScale = new Vector3(s < 0f ? 1f : -1f, 1f, 1f); // left-hand mesh, mirrored for the right
                }
            }
        }

        static void Place(Transform t, Vector3 a, Vector3 b, float len0)
        {
            Vector3 d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            t.localPosition = a;
            t.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
            t.localScale = new Vector3(1f, 1f, len / len0);
        }
    }
}
