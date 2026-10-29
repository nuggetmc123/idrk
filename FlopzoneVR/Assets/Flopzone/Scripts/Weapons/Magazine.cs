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
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, -0.05f, 0);
            col.size = new Vector3(0.035f, 0.11f, 0.06f);
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
