using UnityEngine;

namespace Veylix
{
    /// <summary>Destroy that also works when the editor builds things outside Play mode (track preview, car models).</summary>
    public static class Obj
    {
        /// GetComponent, or add it if missing. (Don't use "GetComponent() ?? AddComponent()": in the editor a missing
        /// component comes back as a special fake-null object that ?? doesn't see as null.)
        public static T Need<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        /// <summary>
        /// Adds a MeshCollider for the mesh only if PhysX can use it: at least one triangle with real area and no
        /// broken (NaN or huge) vertices. Otherwise PhysX logs "cleaning the mesh failed" and the collider does nothing.
        /// </summary>
        public static MeshCollider SafeMeshCollider(GameObject go, Mesh mesh)
        {
            if (mesh == null || !Collidable(mesh)) return null;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            return mc;
        }

        static bool Collidable(Mesh mesh)
        {
            var v = mesh.vertices;
            if (v.Length < 3) return false;
            foreach (var p in v)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || Mathf.Abs(p.x) > 1e6f || Mathf.Abs(p.y) > 1e6f || Mathf.Abs(p.z) > 1e6f) return false;
            var t = mesh.triangles;
            for (int i = 0; i + 2 < t.Length; i += 3)
                if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).sqrMagnitude > 1e-10f) return true;
            return false;
        }

        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
