using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Fusion
{
    /// <summary>
    /// Offline stand-in for Fusion's NetworkRunner (see OfflineFusion.cs). Runs a
    /// one-player "session" on this computer: spawns your kart and avatar, and
    /// calls Spawned / FixedUpdateNetwork / Render on every network script just
    /// like Fusion does, so the whole game works solo.
    /// </summary>
    public class NetworkRunner : MonoBehaviour
    {
        public bool IsRunning { get; private set; }
        public bool IsSharedModeMasterClient => true;
        public PlayerRef LocalPlayer { get; } = new PlayerRef { PlayerId = 1 };
        public IEnumerable<PlayerRef> ActivePlayers => IsRunning ? new[] { LocalPlayer } : new PlayerRef[0];

        readonly List<INetworkRunnerCallbacks> callbacks = new List<INetworkRunnerCallbacks>();
        readonly List<NetworkObject> objects = new List<NetworkObject>();
        readonly List<NetworkBehaviour> behaviours = new List<NetworkBehaviour>();

        public void AddCallbacks(params INetworkRunnerCallbacks[] cbs)
        {
            foreach (var c in cbs) if (c != null && !callbacks.Contains(c)) callbacks.Add(c);
        }

        public Task<StartGameResult> StartGame(StartGameArgs args)
        {
            IsRunning = true;
            // Network objects already placed in the scene (e.g. GameState) come alive now.
            foreach (var obj in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                Register(obj, false);
            foreach (var c in callbacks.ToArray()) c.OnPlayerJoined(this, LocalPlayer);
            return Task.FromResult(new StartGameResult { Ok = true, ShutdownReason = ShutdownReason.Ok });
        }

        public NetworkObject Spawn(NetworkObject prefab, Vector3? position = null, Quaternion? rotation = null,
            PlayerRef? inputAuthority = null)
        {
            var obj = Instantiate(prefab, position ?? Vector3.zero, rotation ?? Quaternion.identity);
            DontDestroyOnLoad(obj.gameObject);
            Register(obj, true);
            return obj;
        }

        public void Despawn(NetworkObject obj)
        {
            if (obj == null) return;
            foreach (var b in obj.GetComponentsInChildren<NetworkBehaviour>(true)) { b.Despawned(this, true); behaviours.Remove(b); }
            objects.Remove(obj);
            obj.IsValid = false;
            Destroy(obj.gameObject);
        }

        void Register(NetworkObject obj, bool spawned)
        {
            if (obj == null || objects.Contains(obj)) return;
            objects.Add(obj);
            obj.Runner = this;
            obj.StateAuthority = LocalPlayer;
            obj.InputAuthority = LocalPlayer;
            obj.IsValid = true;
            var list = obj.GetComponentsInChildren<NetworkBehaviour>(true);
            foreach (var b in list)
            {
                b.Runner = this;
                b.Object = obj;
                behaviours.Add(b);
            }
            foreach (var b in list) b.Spawned();
        }

        public Task Shutdown(bool destroyGameObject = true, ShutdownReason shutdownReason = ShutdownReason.Ok, bool forceShutdownProcedure = false)
        {
            if (IsRunning)
            {
                IsRunning = false;
                foreach (var b in behaviours.ToArray()) if (b) b.Despawned(this, true);
                foreach (var obj in objects.ToArray())
                {
                    if (obj == null) continue;
                    obj.IsValid = false;
                    if (obj.gameObject.scene.name == "DontDestroyOnLoad") Destroy(obj.gameObject); // things we spawned
                }
                objects.Clear();
                behaviours.Clear();
                foreach (var c in callbacks.ToArray()) c.OnShutdown(this, shutdownReason);
            }
            if (destroyGameObject && this) Destroy(gameObject);
            return Task.CompletedTask;
        }

        void FixedUpdate()
        {
            if (!IsRunning) return;
            foreach (var b in behaviours.ToArray())
                if (b && b.isActiveAndEnabled) b.FixedUpdateNetwork();
        }

        void LateUpdate()
        {
            if (!IsRunning) return;
            behaviours.RemoveAll(b => b == null);
            objects.RemoveAll(o => o == null);
            foreach (var b in behaviours.ToArray())
                if (b && b.isActiveAndEnabled) b.Render();
        }

        void OnDestroy()
        {
            if (IsRunning) Shutdown(false);
        }
    }
}
