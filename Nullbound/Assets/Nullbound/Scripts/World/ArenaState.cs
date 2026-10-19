using Fusion;
using UnityEngine;

namespace Nullbound
{
    /// <summary>One per arena (a scene object): shares things that aren't owned by any fighter, like walls breaking.</summary>
    public class ArenaState : NetworkBehaviour
    {
        public static ArenaState Instance { get; private set; }
        public bool IsReady => Object != null && Object.IsValid;

        public override void Spawned() { Instance = this; }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_Break(int id, Vector3 push)
        {
            foreach (var b in Breakable.All)
                if (b.id == id) { b.BreakLocal(push); return; }
        }
    }
}
