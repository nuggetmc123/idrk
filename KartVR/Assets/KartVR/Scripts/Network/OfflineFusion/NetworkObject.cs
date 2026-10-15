using UnityEngine;

namespace Fusion
{
    /// <summary>
    /// Offline stand-in for Fusion's NetworkObject (see OfflineFusion.cs). Marks an
    /// object the NetworkRunner looks after. Replaced by the real one when Photon
    /// Fusion 2 is imported.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour
    {
        [Tooltip("Only used by real Photon Fusion; ignored offline.")]
        public bool IsMasterClientObject = true;

        public NetworkRunner Runner { get; internal set; }
        public bool IsValid { get; internal set; }
        public PlayerRef StateAuthority { get; internal set; }
        public PlayerRef InputAuthority { get; internal set; }
        public bool HasStateAuthority => true;

        public void RequestStateAuthority() { }
    }
}
