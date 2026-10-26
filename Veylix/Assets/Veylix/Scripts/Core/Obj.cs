using UnityEngine;

namespace Veylix
{
    /// <summary>Destroy that also works when the editor builds things outside Play mode (track preview, car models).</summary>
    public static class Obj
    {
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
