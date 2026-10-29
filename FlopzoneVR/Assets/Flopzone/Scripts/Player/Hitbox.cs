using UnityEngine;

namespace Flopzone
{
    /// <summary>Marks a collider that bullets can hurt.</summary>
    public class Hitbox : MonoBehaviour
    {
        public RemotePlayer owner;
        public bool head;
    }
}
