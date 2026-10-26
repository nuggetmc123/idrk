using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// A racing glove that follows a controller: palm, four two-jointed fingers and a thumb that curl with the grip and
    /// trigger, or wrap around whatever the hand is holding (the steering wheel rim, the handbrake). When holding
    /// something the glove is pinned to that object so it never floats off the rim.
    /// </summary>
    public class Glove : MonoBehaviour
    {
        public bool left;
        Transform model;
        readonly Transform[,] fingers = new Transform[5, 2];
        float curl, curlTarget;
        Transform pinnedTo;
        Vector3 pinPos;
        Quaternion pinRot;

        /// Where the forearm meets the glove (the driver body's arms reach to here).
        public Vector3 WristPosition => model != null ? model.TransformPoint(new Vector3(0f, 0f, -0.09f)) : transform.position;

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
            var leather = VXAssets.Or(A != null ? A.interior : null, "Glove", new Color(0.06f, 0.06f, 0.07f), 0.35f);
            var accent = VXAssets.Or(A != null ? A.padding : null, "Glove accent", new Color(0.1f, 0.25f, 0.6f), 0.3f);
            model = new GameObject("Glove").transform;
            model.SetParent(transform, false);
            // Controller grip pose -> hand: fingers forward, palm facing inwards, slight downward tilt.
            model.localRotation = Quaternion.Euler(-30f, 0f, left ? 90f : -90f);
            model.localPosition = new Vector3(left ? -0.01f : 0.01f, -0.01f, -0.04f);
            float side = left ? -1f : 1f;
            var palm = new MeshKit();
            palm.Loft(0, new[]
            {
                new MeshKit.Station(0.045f, 0f, 0.042f, 0.014f, 3f),
                new MeshKit.Station(0.0f, 0f, 0.045f, 0.018f, 3f),
                new MeshKit.Station(-0.05f, 0f, 0.035f, 0.02f, 2.6f),
                new MeshKit.Station(-0.085f, 0f, 0.03f, 0.022f, 2.3f),
            }, 16, 8, null, 0, 1);
            palm.Box(1, new Vector3(0f, 0.019f, -0.02f), new Vector3(0.05f, 0.004f, 0.05f));
            MakePart(model, "Palm", palm.Build("palm"), new[] { leather, accent });
            float[] xs = { 0.03f, 0.011f, -0.009f, -0.028f };
            float[] lens = { 0.042f, 0.047f, 0.045f, 0.036f };
            var seg = SegmentMesh();
            for (int f = 0; f < 4; f++)
            {
                var root = new GameObject("F" + f).transform;
                root.SetParent(model, false);
                root.localPosition = new Vector3(xs[f] * side * -1f * -1f, 0f, 0.045f);
                fingers[f, 0] = root;
                var a = MakePart(root, "a", seg, new[] { leather });
                a.transform.localScale = new Vector3(0.017f, 0.016f, lens[f] * 0.55f);
                var mid = new GameObject("b").transform;
                mid.SetParent(root, false);
                mid.localPosition = new Vector3(0f, 0f, lens[f] * 0.55f);
                fingers[f, 1] = mid;
                var b = MakePart(mid, "b", seg, new[] { leather });
                b.transform.localScale = new Vector3(0.016f, 0.015f, lens[f] * 0.5f);
            }
            var thumb = new GameObject("Thumb").transform;
            thumb.SetParent(model, false);
            thumb.localPosition = new Vector3(0.04f * (left ? -1f : 1f) * -1f * -1f, -0.008f, -0.01f);
            thumb.localRotation = Quaternion.Euler(0f, (left ? -1f : 1f) * 40f, 0f);
            fingers[4, 0] = thumb;
            var t1 = MakePart(thumb, "a", seg, new[] { leather });
            t1.transform.localScale = new Vector3(0.019f, 0.018f, 0.03f);
            var t2 = new GameObject("b").transform;
            t2.SetParent(thumb, false);
            t2.localPosition = new Vector3(0f, 0f, 0.03f);
            fingers[4, 1] = t2;
            var t2m = MakePart(t2, "b", seg, new[] { leather });
            t2m.transform.localScale = new Vector3(0.017f, 0.016f, 0.026f);
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
                if (fingers[f, 0] != null) fingers[f, 0].localRotation = Quaternion.Euler(c * 70f, 0f, 0f);
                if (fingers[f, 1] != null) fingers[f, 1].localRotation = Quaternion.Euler(c * 85f, 0f, 0f);
            }
            if (fingers[4, 0] != null) fingers[4, 0].localRotation = Quaternion.Euler(curl * 25f, (left ? -1f : 1f) * Mathf.Lerp(40f, 15f, curl), 0f);
            if (fingers[4, 1] != null) fingers[4, 1].localRotation = Quaternion.Euler(curl * 40f, 0f, 0f);
            if (pinnedTo != null)
            {
                model.position = pinnedTo.TransformPoint(pinPos);
                model.rotation = pinnedTo.rotation * pinRot;
            }
            else
            {
                model.localPosition = new Vector3(left ? -0.01f : 0.01f, -0.01f, -0.04f);
                model.localRotation = Quaternion.Euler(-30f, 0f, left ? 90f : -90f);
            }
        }

        public void SetVisible(bool v) { if (model != null) model.gameObject.SetActive(v); }
    }
}
