using System.Linq;
using Fusion;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// One per session, placed in the scene. Holds which mode the session is in.
    /// Owned (state authority) by the Shared Mode master client. In the
    /// inspector, tick "Is Master Client Object" on its NetworkObject so
    /// ownership moves to someone else if the master client leaves.
    /// </summary>
    public class GameState : NetworkBehaviour
    {
        public static GameState Instance { get; private set; }

        [Networked] public KartMode Mode { get; set; }
        [Networked] NetworkBool Initialized { get; set; }

        public bool IsReady => Object != null && Object.IsValid && Initialized;

        public static KartMode CurrentMode =>
            Instance != null && Instance.IsReady ? Instance.Mode : KartMode.Hub;

        public override void Spawned()
        {
            Instance = this;
            TryInitialize();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        void TryInitialize()
        {
            if (!HasStateAuthority || Initialized) return;
            // The first player in a session decides its starting mode.
            Mode = SessionManager.Instance != null ? SessionManager.Instance.RequestedMode : KartMode.Hub;
            Initialized = true;
        }

        float nextAuthorityCheck;

        public override void FixedUpdateNetwork()
        {
            TryInitialize();
        }

        public override void Render()
        {
            // Render runs on every client (FixedUpdateNetwork only on the owner).
            // Backup in case "Is Master Client Object" wasn't ticked: if the
            // owner left, the new master client takes over.
            if (Time.time < nextAuthorityCheck) return;
            nextAuthorityCheck = Time.time + 1f;
            if (!HasStateAuthority && Runner.IsSharedModeMasterClient &&
                !Runner.ActivePlayers.Contains(Object.StateAuthority))
            {
                Object.RequestStateAuthority();
            }
        }

        /// <summary>Move the whole session to another mode (used inside private lobbies).</summary>
        public void RequestMode(KartMode mode)
        {
            if (!IsReady) return;
            if (HasStateAuthority) Mode = mode;
            else RPC_RequestMode(mode);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestMode(KartMode mode)
        {
            Mode = mode;
        }
    }
}
