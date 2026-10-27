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
        public DriverBody driver;
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
            Fling(go, car, p >= CarDamage.Part.SuspFL ? 22f : 9f);
        }

        /// Loose bits that can be knocked off in a crash (mirrors, bargeboards, small bodywork). Added by the model.
        public readonly List<GameObject> looseBits = new List<GameObject>();

        /// <summary>Knock some loose bits off; more, the harder the hit (0..1).</summary>
        public void FlingBits(Car car, float strength)
        {
            int n = Mathf.RoundToInt(strength * 3f + Random.value);
            for (int i = 0; i < n; i++)
            {
                GameObject pick = null;
                foreach (var b in looseBits) if (b != null && b.activeInHierarchy && b.transform.parent != null && b.transform.parent.GetComponentInParent<Car>() != null && Random.value < 0.5f) { pick = b; break; }
                if (pick == null) break;
                Fling(pick, car, 2f);
            }
            if (strength > 0.3f) Fx.DebrisBurst(car.transform.position + Vector3.up * 0.4f, Vector3.up, strength, paint);
        }

        /// <summary>Detach a piece into a tumbling physics object (put back by RestoreAll).</summary>
        void Fling(GameObject go, Car car, float mass)
        {
            if (!saved.ContainsKey(go)) saved[go] = new Saved { parent = go.transform.parent, pos = go.transform.localPosition, rot = go.transform.localRotation };
            Vector3 v = car.Body.GetPointVelocity(go.transform.position);
            go.transform.SetParent(null, true);
            Layers.SetRecursive(go, Layers.Debris);
            var rb = Obj.Need<Rigidbody>(go);
            rb.isKinematic = false;
            rb.mass = mass;
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
            if (helmetPaint != null) { if (helmetPaint.mainTexture != null && helmetPaint.mainTexture != livery) Destroy(helmetPaint.mainTexture); Destroy(helmetPaint); }
            if (livery != null) Destroy(livery);
            if (discMaterial != null) Destroy(discMaterial);
        }
    }

    /// <summary>
    /// All of a car's suspension arms in one mesh (one draw instead of two dozen). Each arm is a streamlined tube
    /// stretched between its chassis mount and the upright, rebuilt every frame as the wheels move.
    /// </summary>
    public class StrutSet : MonoBehaviour
    {
        struct Arm { public Transform a, b; public Vector3 aOffset, bOffset; public float thickness; }
        readonly List<Arm> arms = new List<Arm>();
        Mesh mesh, unit;
        Vector3[] unitV, unitN;
        Vector2[] unitUV;
        int[] unitT;
        readonly List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>();

        public static StrutSet For(Transform car, Material mat, Mesh unitTube)
        {
            var existing = car.Find("Arm");
            var set = existing != null ? existing.GetComponent<StrutSet>() : null;
            if (set != null) return set;
            var go = new GameObject("Arm"); // the name CarLod hides at a distance
            go.transform.SetParent(car, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            set = go.AddComponent<StrutSet>();
            set.unit = unitTube;
            return set;
        }

        public void Add(Transform a, Vector3 aOffset, Transform b, Vector3 bOffset, float thickness)
        {
            arms.Add(new Arm { a = a, b = b, aOffset = aOffset, bOffset = bOffset, thickness = thickness });
            mesh = null; // rebuild with the new arm
        }

        void LateUpdate() => Apply();

        /// Rebuild the arms where the wheels are now (also used when the editor saves the car models).
        public void Apply()
        {
            if (unit == null || arms.Count == 0) return;
            if (unitV == null) { unitV = unit.vertices; unitN = unit.normals; unitT = unit.triangles; var uvl = new List<Vector2>(); unit.GetUVs(0, uvl); unitUV = uvl.ToArray(); }
            var mf = GetComponent<MeshFilter>();
            if (mesh == null)
            {
                mesh = new Mesh { name = "suspension" };
                mesh.MarkDynamic();
                verts.Clear(); norms.Clear();
                for (int i = 0; i < arms.Count; i++) { verts.AddRange(unitV); norms.AddRange(unitN); }
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                if (unitUV.Length == unitV.Length)
                {
                    var uvs = new List<Vector2>(unitUV.Length * arms.Count);
                    for (int i = 0; i < arms.Count; i++) uvs.AddRange(unitUV);
                    mesh.SetUVs(0, uvs); // the carbon weave
                }
                var tris = new int[unitT.Length * arms.Count];
                for (int i = 0; i < arms.Count; i++)
                    for (int t = 0; t < unitT.Length; t++) tris[i * unitT.Length + t] = unitT[t] + i * unitV.Length;
                mesh.SetTriangles(tris, 0);
                mf.sharedMesh = mesh;
            }
            var toLocal = transform.worldToLocalMatrix;
            int k = 0;
            for (int i = 0; i < arms.Count; i++)
            {
                var arm = arms[i];
                Matrix4x4 m = Matrix4x4.Scale(Vector3.zero);
                if (arm.a != null && arm.b != null)
                {
                    Vector3 pa = arm.a.TransformPoint(arm.aOffset), pb = arm.b.TransformPoint(arm.bOffset);
                    Vector3 d = pb - pa;
                    float len = d.magnitude;
                    if (len > 1e-4f) m = toLocal * Matrix4x4.TRS((pa + pb) * 0.5f, Quaternion.LookRotation(d / len, arm.a.up), new Vector3(arm.thickness, arm.thickness, len));
                }
                for (int v = 0; v < unitV.Length; v++, k++)
                {
                    verts[k] = m.MultiplyPoint3x4(unitV[v]);
                    norms[k] = m.MultiplyVector(unitN[v]).normalized;
                }
            }
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.RecalculateBounds();
        }
    }

    static class VecExt
    {
        public static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
