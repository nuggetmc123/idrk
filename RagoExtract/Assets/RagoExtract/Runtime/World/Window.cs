using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A pane of window glass in a house. You can see through it (raiders too); a bullet, a grenade blast or a hard
    /// knock shatters it and the bullet carries on. Breaking is shared online (every window has the same number on
    /// every machine, because the map is built the same way).
    /// </summary>
    public class Window : MonoBehaviour, IBreakable
    {
        public const int Layer = 2;   // "Ignore Raycast": left out of every sight / light / wall check, still hit by bullets
        public static readonly List<Window> All = new List<Window>();
        public int Id;
        bool _broken;
        Vector3 _size;

        public static Window Create(Transform parent, Vector3 center, Vector3 size, Quaternion rot)
        {
            var go = new GameObject("Window");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(center, rot);
            go.layer = Layer;
            var w = go.AddComponent<Window>();
            w._size = size; w.Id = All.Count; All.Add(w);
            var mb = new MB();
            mb.Box(Vector3.zero, size, new Color(0.55f, 0.7f, 0.75f, 0.22f), 0f);
            var vis = mb.Build("Glass", go.transform, Mats.Unlit("window_glass", new Color(0.6f, 0.75f, 0.8f, 0.22f), null, Mats.BlendKind.Alpha));
            vis.layer = Layer;
            foreach (var r in vis.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = go.AddComponent<BoxCollider>(); col.size = size;
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            return w;
        }

        void OnDestroy() { All.Remove(this); }
        public static void ClearAll() { All.Clear(); }

        public void Damage(float d, Vector3 p) { Break(Vector3.zero, true); }

        public void Break(Vector3 dir = default, bool tell = false)
        {
            if (_broken) return;
            _broken = true;
            Fx.GlassShatter(transform.position, _size, transform.rotation, dir);
            Sfx.Play(Synth.GlassBreak(), transform.position, 0.9f, 1f + Random.Range(-0.08f, 0.08f), 2f, 60f);
            Noise.Emit(transform.position, 25f, 0);
            if (tell && NetGame.Live) NetGame.I.WindowBroken(Id);
            Destroy(gameObject);
        }
    }
}
