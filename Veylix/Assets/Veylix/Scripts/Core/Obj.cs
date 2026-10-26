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

        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
