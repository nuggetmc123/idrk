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

        static readonly RaycastHit[] _smash = new RaycastHit[8];
        /// <summary>(3.62) Something swung hard from a to b (a melee weapon, the stock of your gun): any window in its path
        /// shatters (shared online). True if one broke.</summary>
        public static bool TrySmash(Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 0.001f || len > 1.5f) return false;
            int n = Physics.SphereCastNonAlloc(a, radius, d / len, _smash, len, 1 << Layer, QueryTriggerInteraction.Collide);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var w = _smash[i].collider != null ? _smash[i].collider.GetComponentInParent<Window>() : null;
                if (w != null && !w._broken) { w.Break(d / len, true); any = true; }
            }
            return any;
        }

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
