using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Keeps everyone's build-mode pieces in sync. Lives on the GameState object.
    ///
    /// Anyone asks the session's owner (state authority) to place or delete a
    /// piece; the owner gives it an id and tells everybody, and every player
    /// builds the same piece. Everyone keeps the full list, so if the owner
    /// leaves, the next one carries on. Players who join later ask for the list.
    /// </summary>
    public class BuildManager : NetworkBehaviour
    {
        public static BuildManager Instance { get; private set; }

        /// <summary>Most pieces a session can have; placing more removes the oldest.</summary>
        public const int MaxPieces = 250;

        class Piece
        {
            public int id, placer;
            public byte type;
            public Vector3 position;
            public float yaw;
            public GameObject go;
        }

        readonly List<Piece> pieces = new List<Piece>();
        int nextId = 1;
        int resyncIndex = -1;
        Transform root;

        public bool IsReady => Object != null && Object.IsValid;
        float placeWindowStart;
        int placesInWindow;
        public int Count => pieces.Count;

        public override void Spawned()
        {
            Instance = this;
            // Joining a session that's already running: ask the owner for everything built so far.
            if (!HasStateAuthority) RPC_RequestSync();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            foreach (var p in pieces) if (p.go) Destroy(p.go);
            pieces.Clear();
            if (Instance == this) Instance = null;
        }

        // ---------------- what build mode calls

        public void Place(int type, Vector3 position, float yaw)
        {
            if (!IsReady) return;
            RPC_RequestPlace((byte)type, position, yaw, Runner.LocalPlayer.PlayerId);
        }

        public void Remove(int id)
        {
            if (!IsReady) return;
            RPC_RequestRemove(id);
        }

        /// <summary>Delete every piece you placed (Spawn Menu > Remove all mine).</summary>
        public void RemoveAllMine()
        {
            if (!IsReady) return;
            int me = Runner.LocalPlayer.PlayerId;
            foreach (var p in pieces.ToArray())
                if (p.placer == me) RPC_RequestRemove(p.id);
        }

        /// <summary>Delete the last piece you placed.</summary>
        public bool UndoMine()
        {
            if (!IsReady) return false;
            int me = Runner.LocalPlayer.PlayerId;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                if (pieces[i].placer != me) continue;
                RPC_RequestRemove(pieces[i].id);
                return true;
            }
            return false;
        }

        // ---------------- owner side

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestPlace(byte type, Vector3 position, float yaw, int placer, RpcInfo info = default)
        {
            // Outside Free Roam / Tutorial Park only a proven moderator may build ("Build anywhere").
            if (!KartModeInfo.CanBuild(GameState.CurrentMode) && !FromVerifiedMod(info)) return;
            // Anti-spam: a hacked game can't flood the map (a person places a few pieces a second at most).
            if (Time.time - placeWindowStart > 1f) { placeWindowStart = Time.time; placesInWindow = 0; }
            if (++placesInWindow > 8) return;
            position = KartMaps.ClampInside(GameState.CurrentMode, position + Vector3.up * 0.5f, 2f) + Vector3.down * 0.5f; // never past the barrier
            while (pieces.Count >= MaxPieces)
            {
                int oldest = pieces[0].id;
                RemoveLocal(oldest); // right away here, so this loop always ends
                RPC_Removed(oldest);
            }
            if (pieces.Count > 0) nextId = Mathf.Max(nextId, pieces.Max(p => p.id) + 1);
            RPC_Placed(nextId++, type, position, yaw, placer);
        }

        static bool FromVerifiedMod(RpcInfo info)
        {
            foreach (var k in KartController.All)
                if (k != null && k.IsReady && k.Object.StateAuthority == info.Source) return k.IsVerifiedMod;
            return false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRemove(int id)
        {
            if (pieces.Any(p => p.id == id)) RPC_Removed(id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestSync()
        {
            if (pieces.Count > 0) resyncIndex = 0;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || resyncIndex < 0) return;
            // Re-send the list a few pieces per tick (everyone ignores pieces they already have).
            for (int n = 0; n < 6 && resyncIndex < pieces.Count; n++, resyncIndex++)
            {
                var p = pieces[resyncIndex];
                RPC_Placed(p.id, p.type, p.position, p.yaw, p.placer);
            }
            if (resyncIndex >= pieces.Count) resyncIndex = -1;
        }

        // ---------------- everyone

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Placed(int id, byte type, Vector3 position, float yaw, int placer)
        {
            if (pieces.Any(p => p.id == id)) return;
            if (root == null) root = new GameObject("BuildPieces").transform;
            var go = BuildCatalog.Make(type, false);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var tag = go.AddComponent<BuildPiece>();
            tag.id = id;
            tag.type = type;
            pieces.Add(new Piece { id = id, placer = placer, type = type, position = position, yaw = yaw, go = go });
            if (resyncIndex < 0) AudioManager.PlayAt(AudioManager.Sfx.Place, position);
            nextId = Mathf.Max(nextId, id + 1);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Removed(int id) => RemoveLocal(id);

        void RemoveLocal(int id)
        {
            int i = pieces.FindIndex(p => p.id == id);
            if (i < 0) return;
            AudioManager.PlayAt(AudioManager.Sfx.Delete, pieces[i].position);
            if (pieces[i].go) Destroy(pieces[i].go);
            pieces.RemoveAt(i);
        }
    }
}
