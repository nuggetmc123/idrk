using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// A racing glove that follows a controller: a gauntlet cuff with a strap, a padded palm and knuckle pad, four
    /// three-jointed fingers and a thumb that curl with the grip and trigger, or wrap around whatever the hand is
    /// holding (the steering wheel rim, the handbrake). When holding something the glove is pinned to that object so it
    /// never floats off the rim. The driver's arms (DriverBody) reach to the glove's wrist, so in the cockpit your
    /// sleeves run into your gloves.
    /// </summary>
    public class Glove : MonoBehaviour
    {
        public bool left;
        Material gloveMat;

        /// Re-tint after the driver profile changes.
        public void SetColour(Color c) { if (gloveMat != null) gloveMat.color = c; }
        Transform model;
        readonly Transform[,] fingers = new Transform[5, 3];
        float curl, curlTarget;
        Transform pinnedTo;
        Vector3 pinPos;
        Quaternion pinRot;
        float thumbSide;

        public Transform Model => model;

        /// Where the forearm meets the glove (the driver body's arms reach to here).
        public Vector3 WristPosition => model != null ? model.TransformPoint(new Vector3(0f, 0f, -0.09f)) : transform.position;

        // Where the glove sits on the controller. The hand transform follows OpenXR's grip pose, which Unity gives as:
        // origin in the middle of your closed fist (the controller handle), +Z up the handle from little finger to
        // thumb, +Y forward (the way your knuckles face), +X to your left. The glove is built wrist -> fingers along
        // its +Z with the back of the hand on +Y, so: fingers along grip +Y, back of the hand outwards (grip +X on the
        // left hand, -X on the right), and the handle passing through the middle of the curled fingers.
        // Settings > Hands adds your own fine adjustment on top.
        static readonly Vector3 HandleInGlove = new Vector3(0f, -0.028f, 0.015f);
        Quaternion BaseRotation => Quaternion.LookRotation(Vector3.up, left ? Vector3.right : Vector3.left);
        Quaternion RestRotation => Quaternion.AngleAxis(Settings.GloveTilt, Vector3.right) * BaseRotation;
        Vector3 RestPosition => Quaternion.AngleAxis(Settings.GloveTilt, Vector3.right) * -(BaseRotation * HandleInGlove)
                                + new Vector3(0f, Settings.GloveForward, Settings.GloveUp);

        public static Glove Create(Transform hand, bool left)
        {
            var g = hand.gameObject.AddComponent<Glove>();
            g.left = left;
            g.BuildModel();
            return g;
        }

        void BuildModel()
        {
            var A = VXAssets.I;
            var leather = new Material(VXAssets.Or(A != null ? A.interior : null, "Glove", new Color(0.06f, 0.06f, 0.07f), 0.35f)) { name = "Glove" };
            leather.color = SaveData.Profile.Gloves; // your glove colour from the driver profile
            if (leather.HasProperty("_Glossiness")) leather.SetFloat("_Glossiness", 0.35f);
            gloveMat = leather;
            var accent = new Material(leather) { name = "Glove accent", color = SaveData.Profile.SuitAccent };
            model = new GameObject("Glove").transform;
            model.SetParent(transform, false);
            // Controller grip pose -> hand: fingers forward, palm facing inwards, slight downward tilt.
            model.localRotation = RestRotation;
            model.localPosition = RestPosition;
            // Back of the hand is +Y, fingers +Z: the thumb is on +X for a left hand and -X for a right hand.
            thumbSide = left ? 1f : -1f;
            const float wristZ = -0.09f;
            var palm = MakePart(model, "Palm", BodyKit.GlovePalm(), new[] { leather, accent });
            palm.transform.localPosition = new Vector3(0f, 0f, wristZ);
            var seg = SegmentMesh();
            for (int f = 0; f < 4; f++)
            {
                Transform parent = model;
                Vector3 at = new Vector3(BodyKit.FingerX[f] * thumbSide, -0.001f, wristZ + BodyKit.FingerZ);
                float r = f == 3 ? 0.0094f : 0.0105f;
                for (int j = 0; j < 3; j++)
                {
                    var joint = new GameObject("F" + f + "_" + j).transform;
                    joint.SetParent(parent, false);
                    joint.localPosition = at;
                    if (j == 0) joint.localRotation = Quaternion.Euler(0f, (f - 1.5f) * -3f * thumbSide, 0f); // a slight fan
                    fingers[f, j] = joint;
                    float len = BodyKit.FingerLen[f] * BodyKit.FingerSeg[j];
                    float rr = r * (1f - j * 0.08f);
                    var m = MakePart(joint, "s", seg, new[] { leather });
                    m.transform.localScale = new Vector3(rr * 2f, rr * 1.9f, len + rr);
                    m.transform.localPosition = new Vector3(0f, 0f, -rr * 0.5f);
                    parent = joint;
                    at = new Vector3(0f, 0f, len);
                }
            }
            // Thumb: fleshy base off the side of the palm, then two segments.
            var thumb = new GameObject("Thumb").transform;
            thumb.SetParent(model, false);
            thumb.localPosition = new Vector3(0.03f * thumbSide, -0.01f, wristZ + 0.018f);
            thumb.localRotation = Quaternion.LookRotation(new Vector3(0.45f * thumbSide, -0.4f, 0.8f));
            fingers[4, 0] = thumb;
            var t1 = MakePart(thumb, "a", seg, new[] { leather });
            t1.transform.localScale = new Vector3(0.032f, 0.03f, 0.05f);
            t1.transform.localPosition = new Vector3(0f, 0f, -0.006f);
            var t2 = new GameObject("b").transform;
            t2.SetParent(thumb, false);
            t2.localPosition = new Vector3(0f, 0f, 0.042f);
            fingers[4, 1] = t2;
            var t2m = MakePart(t2, "b", seg, new[] { leather });
            t2m.transform.localScale = new Vector3(0.023f, 0.022f, 0.034f);
            var t3 = new GameObject("c").transform;
            t3.SetParent(t2, false);
            t3.localPosition = new Vector3(0f, 0f, 0.03f);
            fingers[4, 2] = t3;
            var t3m = MakePart(t3, "c", seg, new[] { leather });
            t3m.transform.localScale = new Vector3(0.02f, 0.019f, 0.028f);
            Layers.SetRecursive(model.gameObject, Layers.Scenery);
        }

        static Mesh segment;
        static Mesh SegmentMesh()
        {
            if (segment != null) return segment;
            var k = new MeshKit();
            // A rounded capsule from z=0 to z=1 (scaled per segment).
            k.Loft(0, new[]
            {
                new MeshKit.Station(1.0f, 0f, 0.3f, 0.3f, 2f), new MeshKit.Station(0.92f, 0f, 0.5f, 0.48f, 2.2f),
                new MeshKit.Station(0.1f, 0f, 0.5f, 0.5f, 2.2f), new MeshKit.Station(0.0f, 0f, 0.35f, 0.35f, 2f),
            }, 10, 6, null, 0, 0);
            segment = k.Build("finger");
            return segment;
        }

        static GameObject MakePart(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Pin the glove model to a held object (world pose of the hand on it).</summary>
        public void Pin(Transform obj, Vector3 worldPos, Quaternion worldRot)
        {
            pinnedTo = obj;
            pinPos = obj.InverseTransformPoint(worldPos);
            pinRot = Quaternion.Inverse(obj.rotation) * worldRot;
        }

        public void Unpin() { pinnedTo = null; }
        public bool Pinned => pinnedTo != null;

        void LateUpdate()
        {
            InputHub.Poll();
            var h = left ? InputHub.Left : InputHub.Right;
            bool hasXR = h.valid;
            curlTarget = pinnedTo != null ? 0.85f : hasXR ? Mathf.Max(h.grip * 0.9f, h.trigger * 0.4f) : 0.2f;
            curl = Mathf.MoveTowards(curl, curlTarget, Time.deltaTime * 8f);
            for (int f = 0; f < 4; f++)
            {
                float c = curl * (f == 0 && hasXR && pinnedTo == null ? Mathf.Lerp(0.3f, 1f, h.trigger) : 1f);
                for (int j = 0; j < 3; j++)
                {
                    if (fingers[f, j] == null) continue;
                    var bend = Quaternion.Euler(Mathf.Lerp(8f, j == 0 ? 72f : 82f, c), 0f, 0f);
                    fingers[f, j].localRotation = j == 0 ? Quaternion.Euler(0f, (f - 1.5f) * -3f * thumbSide, 0f) * bend : bend;
                }
            }
            // Thumb: swings across towards the fingers as the hand closes.
            if (fingers[4, 0] != null) fingers[4, 0].localRotation = Quaternion.LookRotation(Vector3.Slerp(new Vector3(0.45f * thumbSide, -0.4f, 0.8f), new Vector3(0.25f * thumbSide, -0.62f, 0.75f), curl));
            if (fingers[4, 1] != null) fingers[4, 1].localRotation = Quaternion.Euler(curl * 25f, -thumbSide * Mathf.Lerp(20f, 55f, curl), 0f);
            if (fingers[4, 2] != null) fingers[4, 2].localRotation = Quaternion.Euler(curl * 30f, -thumbSide * curl * 25f, 0f);
            if (pinnedTo != null)
            {
                model.position = pinnedTo.TransformPoint(pinPos);
                model.rotation = pinnedTo.rotation * pinRot;
            }
            else
            {
                model.localPosition = RestPosition;
                model.localRotation = RestRotation;
            }
        }

        public void SetVisible(bool v) { if (model != null) model.gameObject.SetActive(v); }
    }
}
