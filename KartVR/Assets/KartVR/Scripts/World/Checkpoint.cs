using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// A trigger box across the race track. Index 0 is the start/finish line.
    /// Karts must pass them in order (0, 1, 2 ... back to 0) to count a lap.
    /// The transform's forward should point in the racing direction.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        public int index;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = index == 0 ? Color.white : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
