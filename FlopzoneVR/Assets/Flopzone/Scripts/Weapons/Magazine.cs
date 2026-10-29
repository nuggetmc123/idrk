using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>A magazine: carried, dropped, or sitting in a gun.</summary>
    public class Magazine : Grabbable
    {
        public GunDef def;
        public int ammo;
        float looseSince = -1f;

        public static Magazine Spawn(GunDef def, int ammo, Vector3 pos, Quaternion rot)
        {
            var go = GunModel.BuildMag(def, null);
            go.transform.SetPositionAndRotation(pos, rot);
            // a box that fits this mag's shape (drums, curved and long mags differ a lot)
            var bounds = new Bounds(new Vector3(0, -0.05f, 0), Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var lc = go.transform.InverseTransformPoint(r.bounds.center);
                var ls = go.transform.InverseTransformVector(r.bounds.size);
                var lb = new Bounds(lc, new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
                if (first) { bounds = lb; first = false; } else bounds.Encapsulate(lb);
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = bounds.center;
            col.size = Vector3.Max(bounds.size, new Vector3(0.025f, 0.03f, 0.025f));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            var m = go.AddComponent<Magazine>();
            m.def = def;
            m.ammo = ammo;
            return m;
        }

        public override void OnRelease(Hand h, Vector3 velocity, Vector3 angular)
        {
            base.OnRelease(h, velocity, angular);
            looseSince = Time.time;
        }

        protected override void Update()
        {
            base.Update();
            if (Held) { looseSince = -1f; return; }
            if (looseSince > 0 && Time.time - looseSince > 8f) Destroy(gameObject);
        }
    }
}
