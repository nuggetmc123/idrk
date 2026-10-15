// =============================================================================
// OFFLINE STAND-IN FOR PHOTON FUSION
//
// Lets the project compile and play (solo, on your own computer) before Photon
// Fusion 2 is imported. It has the same names as the parts of Fusion the game
// uses, so the rest of the code doesn't need to change.
//
// As soon as you import Photon Fusion 2, KartVR/Setup (FusionSwitch) detects it
// and deletes this folder, which switches the real multiplayer on. If Fusion is
// removed, it puts this folder back. You don't need to touch anything here.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fusion
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NetworkedAttribute : Attribute { }

    public enum RpcSources { All, StateAuthority, InputAuthority, Proxies }
    public enum RpcTargets { All, StateAuthority, InputAuthority, Proxies }

    /// <summary>Offline: RPCs are just normal method calls on this computer.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public RpcAttribute(RpcSources sources, RpcTargets targets) { }
    }

    public struct PlayerRef : IEquatable<PlayerRef>
    {
        public int PlayerId;
        public static PlayerRef None => default(PlayerRef);
        public static bool operator ==(PlayerRef a, PlayerRef b) => a.PlayerId == b.PlayerId;
        public static bool operator !=(PlayerRef a, PlayerRef b) => a.PlayerId != b.PlayerId;
        public bool Equals(PlayerRef other) => PlayerId == other.PlayerId;
        public override bool Equals(object obj) => obj is PlayerRef p && p.PlayerId == PlayerId;
        public override int GetHashCode() => PlayerId;
        public override string ToString() => "Player " + PlayerId;
    }

    public struct NetworkBool
    {
        bool value;
        public static implicit operator bool(NetworkBool b) => b.value;
        public static implicit operator NetworkBool(bool b) => new NetworkBool { value = b };
        public override string ToString() => value.ToString();
    }

    /// <summary>Size marker for NetworkString (16 characters).</summary>
    public struct _16 { }

    public struct NetworkString<TSize> where TSize : struct
    {
        string value;
        public static implicit operator NetworkString<TSize>(string s) => new NetworkString<TSize> { value = s };
        public string Value => value ?? "";
        public override string ToString() => value ?? "";
    }

    /// <summary>A countdown timer (offline version uses Unity's clock).</summary>
    public struct TickTimer
    {
        float endTime;
        bool set;

        public static TickTimer None => default(TickTimer);

        public static TickTimer CreateFromSeconds(NetworkRunner runner, float seconds) =>
            new TickTimer { endTime = Time.time + seconds, set = true };

        public bool Expired(NetworkRunner runner) => set && Time.time >= endTime;
        public bool IsRunning => set && Time.time < endTime;
        public float? RemainingTime(NetworkRunner runner) => set ? Mathf.Max(0f, endTime - Time.time) : (float?)null;
    }

    public class SimulationBehaviour : MonoBehaviour
    {
        public NetworkRunner Runner { get; internal set; }
    }

    /// <summary>Offline: you own everything, so HasStateAuthority is always true.</summary>
    public class NetworkBehaviour : SimulationBehaviour
    {
        public NetworkObject Object { get; internal set; }
        public bool HasStateAuthority => true;
        public bool HasInputAuthority => true;

        public virtual void Spawned() { }
        public virtual void Despawned(NetworkRunner runner, bool hasState) { }
        public virtual void FixedUpdateNetwork() { }
        public virtual void Render() { }
    }

    public enum GameMode { Single, Shared, Server, Host, Client, AutoHostOrClient }
    public enum ShutdownReason { Ok, Error, IncompatibleConfiguration, ServerInRoom, DisconnectedByPluginLogic, GameClosed, GameNotFound, MaxCcuReached, InvalidRegion, GameIdAlreadyExists, GameIsFull, InvalidAuthentication, CustomAuthenticationFailed, AuthenticationTicketExpired, PhotonCloudTimeout }

    public class SessionProperty
    {
        public object PropertyValue;
        public static implicit operator SessionProperty(int v) => new SessionProperty { PropertyValue = v };
        public static implicit operator SessionProperty(string v) => new SessionProperty { PropertyValue = v };
        public static implicit operator SessionProperty(bool v) => new SessionProperty { PropertyValue = v };
    }

    public struct SceneRef
    {
        public int Index;
        public static SceneRef FromIndex(int index) => new SceneRef { Index = index };
    }

    public struct NetworkSceneInfo
    {
        public void AddSceneRef(SceneRef scene, LoadSceneMode mode) { }
    }

    public interface INetworkSceneManager { }

    public class StartGameArgs
    {
        public GameMode GameMode;
        public string SessionName;
        public Dictionary<string, SessionProperty> SessionProperties;
        public bool? IsVisible;
        public bool? IsOpen;
        public int? PlayerCount;
        public NetworkSceneInfo? Scene;
        public INetworkSceneManager SceneManager;
    }

    public class StartGameResult
    {
        public bool Ok;
        public ShutdownReason ShutdownReason;
    }

    public struct NetworkInput { }
    public struct SimulationMessagePtr { }
    public class SessionInfo { }
    public class HostMigrationToken { }

    public class NetworkRunnerCallbackArgs
    {
        public class ConnectRequest { }
    }

    public interface INetworkRunnerCallbacks
    {
        void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player);
        void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player);
        void OnPlayerJoined(NetworkRunner runner, PlayerRef player);
        void OnPlayerLeft(NetworkRunner runner, PlayerRef player);
        void OnInput(NetworkRunner runner, NetworkInput input);
        void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input);
        void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason);
        void OnConnectedToServer(NetworkRunner runner);
        void OnDisconnectedFromServer(NetworkRunner runner, Sockets.NetDisconnectReason reason);
        void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token);
        void OnConnectFailed(NetworkRunner runner, Sockets.NetAddress remoteAddress, Sockets.NetConnectFailedReason reason);
        void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message);
        void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList);
        void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data);
        void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken);
        void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, Sockets.ReliableKey key, ReadOnlySpan<byte> data);
        void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, Sockets.ReliableKey key, float progress);
        void OnSceneLoadDone(NetworkRunner runner);
        void OnSceneLoadStart(NetworkRunner runner);
    }
}

namespace Fusion.Sockets
{
    public struct NetAddress { }
    public enum NetDisconnectReason { Unknown, Requested, Timeout }
    public enum NetConnectFailedReason { Timeout, ServerFull, ServerRefused }
    public struct ReliableKey { }
}
