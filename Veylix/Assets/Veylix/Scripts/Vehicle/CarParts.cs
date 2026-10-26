using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// References into a built car model: the pieces that can break off, the paint to char, the cockpit anchors
    /// (eye point, steering wheel, handbrake), mirrors and lights.
    /// </summary>
    public class CarParts : MonoBehaviour
    {
        public Material paint, rim, helmetPaint;
        public Texture2D livery;
        public Renderer[] bodyRenderers;
        public GameObject frontWingL, frontWingR, rearWing, helmet;
        public Transform steeringWheel, steeringWheelGrip, handbrakeLever, cockpitAnchor, eyePoint, chaseAnchor;
        public Transform paddleLeft, paddleRight;
        public Transform[] mirrorGlass;
        public Renderer rainLight, headLights;
        public Light headSpot;
        public Vector3 enginePoint = new Vector3(0f, 0.6f, -0.8f);
        public float wheelRadius = 0.3f; // steering wheel rim radius for grabbing
        public Transform displayAnchor;
        public Renderer[] shiftLeds;
        public Renderer brakeDiscs;
        public Material discMaterial;
        public List<Transform> exhausts = new List<Transform>();

        struct Saved { public Transform parent; public Vector3 pos; public Quaternion rot; }
        readonly Dictionary<GameObject, Saved> saved = new Dictionary<GameObject, Saved>();
        readonly List<GameObject> spawnedDebris = new List<GameObject>();
        bool charred;
        Color paintColor;

        void Awake() { }

        public void Remember()
        {
            foreach (var go in new[] { frontWingL, frontWingR, rearWing })
                if (go != null && !saved.ContainsKey(go)) saved[go] = new Saved { parent = go.transform.parent, pos = go.transform.localPosition, rot = go.transform.localRotation };
            if (paint != null) paintColor = paint.color;
        }

        /// <summary>A part comes off: it becomes a loose physics object that tumbles away.</summary>
        public void Break(CarDamage.Part p, Car car)
        {
            GameObject go = null;
            if (p == CarDamage.Part.FrontWingL) go = frontWingL;
            else if (p == CarDamage.Part.FrontWingR) go = frontWingR;
            else if (p == CarDamage.Part.RearWing) go = rearWing;
            else if (p >= CarDamage.Part.SuspFL && p <= CarDamage.Part.SuspRR)
            {
                var w = car.wheels[p - CarDamage.Part.SuspFL];
                if (w.steerPivot != null) go = w.steerPivot.gameObject;
                else if (w.visual != null) go = w.visual.gameObject;
            }
            if (go == null || !go.activeInHierarchy || go.transform.parent == null || go.transform.parent.GetComponentInParent<Car>() == null) return;
            if (!saved.ContainsKey(go)) saved[go] = new Saved { parent = go.transform.parent, pos = go.transform.localPosition, rot = go.transform.localRotation };

            Vector3 v = car.Body.GetPointVelocity(go.transform.position);
            go.transform.SetParent(null, true);
            Layers.SetRecursive(go, Layers.Debris);
            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = p >= CarDamage.Part.SuspFL ? 22f : 9f;
            rb.linearVelocity = v + Random.insideUnitSphere * 3f + Vector3.up * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (go.GetComponent<Collider>() == null)
            {
                var r = go.GetComponentInChildren<Renderer>();
                var bc = go.AddComponent<BoxCollider>();
                if (r != null)
                {
                    var b = r.bounds;
                    bc.center = go.transform.InverseTransformPoint(b.center);
                    bc.size = Vector3.Max(go.transform.InverseTransformVector(b.size).Abs(), Vector3.one * 0.05f);
                }
            }
            spawnedDebris.Add(go);
            Fx.DebrisBurst(go.transform.position, Vector3.up, 0.6f, paint);
        }

        /// <summary>Visible scuffing as a part takes damage (darker, rougher paint around it).</summary>
        public void Dent(CarDamage.Part p, float amount) { }

        public void Char()
        {
            if (charred) return;
            charred = true;
            if (paint != null)
            {
                paint.color = new Color(0.12f, 0.11f, 0.1f);
                paint.SetFloat("_Glossiness", 0.08f);
            }
            if (helmetPaint != null) helmetPaint.color = new Color(0.2f, 0.19f, 0.18f);
        }

        public void RestoreAll()
        {
            foreach (var kv in saved)
            {
                var go = kv.Key;
                if (go == null) continue;
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                var col = go.GetComponent<BoxCollider>();
                if (col != null && go != frontWingL && go != frontWingR && go != rearWing) Destroy(col);
                go.transform.SetParent(kv.Value.parent, false);
                go.transform.localPosition = kv.Value.pos;
                go.transform.localRotation = kv.Value.rot;
                Layers.SetRecursive(go, Layers.Car);
                go.SetActive(true);
            }
            spawnedDebris.Clear();
            if (charred && paint != null)
            {
                paint.color = paintColor == default ? Color.white : paintColor;
                paint.SetFloat("_Glossiness", 0.82f);
                if (helmetPaint != null) helmetPaint.color = Color.white;
            }
            charred = false;
        }

        void OnDestroy()
        {
            foreach (var d in spawnedDebris) if (d != null) Destroy(d);
            if (paint != null) Destroy(paint);
            if (rim != null) Destroy(rim);
            if (helmetPaint != null) Destroy(helmetPaint);
            if (livery != null) Destroy(livery);
            if (discMaterial != null) Destroy(discMaterial);
        }
    }

    /// <summary>Keeps a stretched cylinder between two points (suspension arms that follow the moving wheels).</summary>
    public class Strut : MonoBehaviour
    {
        public Transform a, b;
        public Vector3 aOffset, bOffset;
        public float thickness = 0.02f;

        void LateUpdate() => Apply();

        /// Stretch the arm between its two mount points (also used when the editor saves the car models).
        public void Apply()
        {
            if (a == null || b == null) return;
            Vector3 pa = a.TransformPoint(aOffset), pb = b.TransformPoint(bOffset);
            Vector3 d = pb - pa;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            transform.position = (pa + pb) * 0.5f;
            transform.rotation = Quaternion.LookRotation(d / len, a.up);
            transform.localScale = new Vector3(thickness, thickness, len);
        }
    }

    static class VecExt
    {
        public static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
