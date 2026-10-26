using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>Simple vertex/triangle accumulator for long generated strips (roads, kerbs, barriers).</summary>
    public class MeshAcc
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Vector2> uv2 = new List<Vector2>();
        public readonly List<Color32> col = new List<Color32>();
        public readonly List<int> t = new List<int>();
        public int Count => v.Count;
        public bool Empty => t.Count == 0;

        public int Add(Vector3 p, Vector3 nrm, Vector2 u, Vector2 u2, Color32 c)
        {
            v.Add(p); n.Add(nrm); uv.Add(u); uv2.Add(u2); col.Add(c);
            return v.Count - 1;
        }

        public int Add(Vector3 p, Vector3 nrm, Vector2 u) => Add(p, nrm, u, u, new Color32(255, 255, 255, 255));

        /// <summary>Adds a quad (a,b,c,d in order around) facing 'facing' (flips winding if needed).</summary>
        public void Quad(int a, int b, int c, int d, Vector3 facing)
        {
            Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(fn, facing) >= 0f) { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d); }
            else { t.Add(a); t.Add(c); t.Add(b); t.Add(a); t.Add(d); t.Add(c); }
        }

        public void Tri(int a, int b, int c, Vector3 facing)
        {
            Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(fn, facing) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
            else { t.Add(a); t.Add(c); t.Add(b); }
        }

        public void Box(Vector3 c, Vector3 size, Quaternion rot, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d0 in dirs)
            {
                Vector3 d = rot * d0;
                Vector3 u0 = Mathf.Abs(d0.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, d0);
                Vector3 w0 = Vector3.Cross(d0, u0);
                float su = Mathf.Abs(Vector3.Dot(u0, size)), sw = Mathf.Abs(Vector3.Dot(w0, size));
                Vector3 fc = c + rot * Vector3.Scale(d0, h);
                Vector3 u = rot * u0 * su * 0.5f, w = rot * w0 * sw * 0.5f;
                int a = Add(fc - u - w, d, new Vector2(0, 0));
                int b = Add(fc + u - w, d, new Vector2(su * uvScale, 0));
                int cc = Add(fc + u + w, d, new Vector2(su * uvScale, sw * uvScale));
                int e = Add(fc - u + w, d, new Vector2(0, sw * uvScale));
                Quad(a, b, cc, e, d);
            }
        }

        public Mesh ToMesh(string name, bool recalcNormals = false)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetUVs(1, uv2);
            m.SetColors(col);
            m.SetTriangles(t, 0);
            if (recalcNormals) m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        public void Clear() { v.Clear(); n.Clear(); uv.Clear(); uv2.Clear(); col.Clear(); t.Clear(); }

        /// <summary>Makes a GameObject (optionally with a mesh collider and a surface tag) and clears the accumulator.</summary>
        public GameObject Emit(Transform parent, string name, Material mat, int layer, bool collider, SurfaceType? surface = null, bool shadows = true, bool recalcNormals = false)
        {
            if (Empty) { Clear(); return null; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var mesh = ToMesh(name, recalcNormals);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) Obj.SafeMeshCollider(go, mesh);
            if (surface.HasValue) go.AddComponent<Surface>().type = surface.Value;
            go.isStatic = true;
            Clear();
            return go;
        }
    }
}
