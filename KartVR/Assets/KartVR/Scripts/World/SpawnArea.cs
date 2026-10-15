using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Where karts appear for a mode. Each child transform is one starting slot
    /// (its forward = the direction the kart faces). Falling below killY respawns you.
    /// </summary>
    public class SpawnArea : MonoBehaviour
    {
        public KartMode mode;
        public float killY = -10f;

        static readonly Dictionary<KartMode, SpawnArea> Areas = new Dictionary<KartMode, SpawnArea>();

        public static SpawnArea Get(KartMode mode)
        {
            Areas.TryGetValue(mode, out var area);
            return area;
        }

        void OnEnable() { Areas[mode] = this; }

        void OnDisable()
        {
            if (Areas.TryGetValue(mode, out var a) && a == this) Areas.Remove(mode);
        }

        public Pose GetSlot(int index)
        {
            if (transform.childCount == 0) return new Pose(transform.position, transform.rotation);
            var t = transform.GetChild(Mathf.Abs(index) % transform.childCount);
            return new Pose(t.position, t.rotation);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            foreach (Transform t in transform)
            {
                Gizmos.DrawWireCube(t.position + Vector3.up * 0.4f, new Vector3(1.2f, 0.6f, 1.9f));
                Gizmos.DrawRay(t.position + Vector3.up * 0.4f, t.forward * 2f);
            }
        }
    }
}
