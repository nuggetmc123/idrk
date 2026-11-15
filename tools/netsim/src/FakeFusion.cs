// A working stand-in for Photon Fusion's shared mode, for testing the real FusionBackend with several players in one process.
using System; using System.Collections.Generic; using System.Linq; using System.Threading.Tasks; using UnityEngine;
namespace Fusion.Sockets { public enum NetDisconnectReason { Unknown } public struct NetAddress {} public enum NetConnectFailedReason { Timeout } }
namespace Fusion.Photon.Realtime { public class FusionAppSettings { public string AppIdFusion; public FusionAppSettings GetCopy() => this; } public class PhotonAppSettings : ScriptableObject { public static PhotonAppSettings Global; public FusionAppSettings AppSettings; } }
namespace Fusion {
 using Fusion.Sockets; using Fusion.Photon.Realtime;
 public struct PlayerRef : IEquatable<PlayerRef> { public int Raw; public int PlayerId => Raw; public bool Equals(PlayerRef o) => Raw == o.Raw; public static bool operator ==(PlayerRef a, PlayerRef b) => a.Raw == b.Raw; public static bool operator !=(PlayerRef a, PlayerRef b) => a.Raw != b.Raw; public override bool Equals(object o) => o is PlayerRef p && p.Raw == Raw; public override int GetHashCode() => Raw; public override string ToString() => "P" + Raw; }
 public struct ReliableKey { public static ReliableKey FromInts(int a = 0, int b = 0, int c = 0, int d = 0) => default; }
 public enum GameMode { Single, Shared, Server, Host, Client, AutoHostOrClient }
 public enum ShutdownReason { Ok, Error, GameNotFound }
 public class SessionProperty { public object V; public static implicit operator SessionProperty(int v) => new SessionProperty { V = v }; public static implicit operator SessionProperty(string v) => new SessionProperty { V = v }; public static explicit operator int(SessionProperty p) => p.V is int i ? i : 0; public override string ToString() => V?.ToString(); public bool IsString => V is string; public bool IsInt => V is int; public object PropertyValue => V; }
 public class SessionInfo { public string Name; public bool IsOpen { get; set; } = true; public bool IsVisible { get; set; } = true; public Dictionary<string, SessionProperty> Props = new Dictionary<string, SessionProperty>(); public IReadOnlyDictionary<string, SessionProperty> Properties => Props; public int PlayerCount, MaxPlayers;
  public bool UpdateCustomProperties(Dictionary<string, SessionProperty> p) { foreach (var kv in p) Props[kv.Key] = kv.Value; return true; } }
 public interface INetworkSceneManager {}
 public class AuthenticationValues { public string UserId; public AuthenticationValues() {} public AuthenticationValues(string u) { UserId = u; } }
 public class StartGameArgs { public GameMode GameMode; public string SessionName; public int? PlayerCount; public Dictionary<string, SessionProperty> SessionProperties; public FusionAppSettings CustomPhotonAppSettings; public INetworkSceneManager SceneManager; public AuthenticationValues AuthValues; public bool EnableClientSessionCreation = true; }
 public class StartGameResult { public bool Ok; public ShutdownReason ShutdownReason; public string ErrorMessage; }
 public class NetworkObject : MonoBehaviour {} public struct NetworkInput {} public struct SimulationMessagePtr {} public class HostMigrationToken {}
 public class NetworkRunnerCallbackArgs { public class ConnectRequest {} }
 public interface INetworkRunnerCallbacks {
  void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player); void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player);
  void OnPlayerJoined(NetworkRunner runner, PlayerRef player); void OnPlayerLeft(NetworkRunner runner, PlayerRef player);
  void OnInput(NetworkRunner runner, NetworkInput input); void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input);
  void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason); void OnConnectedToServer(NetworkRunner runner);
  void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason); void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token);
  void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason); void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message);
  void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList); void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data);
  void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken); void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data);
  void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress); void OnSceneLoadDone(NetworkRunner runner); void OnSceneLoadStart(NetworkRunner runner); }

 /// <summary>How the fake cloud misbehaves.</summary>
 public static class Cloud
 {
  public static bool MislabelAsMaster;   // every packet looks like it came from the master client
  public static bool DropBetweenClients; // nothing gets through between two non-master players
  public static int Sent, Delivered;
  public class Room { public string Name; public List<NetworkRunner> Members = new List<NetworkRunner>(); public int Next = 1; public SessionInfo Info; public NetworkRunner Master => Members.Count > 0 ? Members[0] : null; }
  public static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
  public static readonly List<(NetworkRunner to, PlayerRef label, byte[] data)> Queue = new List<(NetworkRunner, PlayerRef, byte[])>();
  public static Action<NetworkRunner> BeforeDeliver;   // the driver switches "who's running" (statics like the profile)
  public static void Pump()
  {
   var q = Queue.ToArray(); Queue.Clear();
   foreach (var (to, label, data) in q) { if (!to.IsRunning) continue; BeforeDeliver?.Invoke(to); Delivered++; foreach (var cb in to.Cbs) cb.OnReliableDataReceived(to, label, default, data); }
  }
 }

 public class NetworkRunner : MonoBehaviour
 {
  public bool ProvideInput; public bool IsRunning; public PlayerRef LocalPlayer; public SessionInfo SessionInfo;
  public List<INetworkRunnerCallbacks> Cbs = new List<INetworkRunnerCallbacks>();
  internal Cloud.Room Room; internal string Uid;
  public bool IsSharedModeMasterClient => Room != null && Room.Master == this;
  public IEnumerable<PlayerRef> ActivePlayers => Room == null ? Enumerable.Empty<PlayerRef>() : Room.Members.Select(m => m.LocalPlayer).ToList();
  public void AddCallbacks(params INetworkRunnerCallbacks[] c) => Cbs.AddRange(c);
  public string GetPlayerUserId(PlayerRef p) => Room?.Members.FirstOrDefault(m => m.LocalPlayer == p)?.Uid;
  public Task<StartGameResult> StartGame(StartGameArgs a)
  {
   if (!Cloud.Rooms.TryGetValue(a.SessionName, out var room))
   {
    if (!a.EnableClientSessionCreation) return Task.FromResult(new StartGameResult { Ok = false, ShutdownReason = ShutdownReason.GameNotFound });
    room = Cloud.Rooms[a.SessionName] = new Cloud.Room { Name = a.SessionName, Info = new SessionInfo { Name = a.SessionName } };
    if (a.SessionProperties != null) room.Info.UpdateCustomProperties(a.SessionProperties);
   }
   Room = room; Uid = a.AuthValues?.UserId; SessionInfo = room.Info;
   LocalPlayer = new PlayerRef { Raw = room.Next++ };
   IsRunning = true;
   room.Members.Add(this);
   foreach (var m in room.Members.ToArray()) if (m != this) { Cloud.BeforeDeliver?.Invoke(m); foreach (var cb in m.Cbs) cb.OnPlayerJoined(m, LocalPlayer); }
   Cloud.BeforeDeliver?.Invoke(this);
   foreach (var m in room.Members.ToArray()) foreach (var cb in Cbs) cb.OnPlayerJoined(this, m.LocalPlayer);
   return Task.FromResult(new StartGameResult { Ok = true });
  }
  public Task Shutdown(bool destroyGameObject = true, ShutdownReason r = ShutdownReason.Ok, bool force = false)
  {
   if (Room != null) { Room.Members.Remove(this); foreach (var m in Room.Members.ToArray()) { Cloud.BeforeDeliver?.Invoke(m); foreach (var cb in m.Cbs) cb.OnPlayerLeft(m, LocalPlayer); } Cloud.BeforeDeliver?.Invoke(this); if (Room.Members.Count == 0) Cloud.Rooms.Remove(Room.Name); }
   Room = null; IsRunning = false;
   return Task.CompletedTask;
  }
  public void SendReliableDataToPlayer(PlayerRef p, ReliableKey k, byte[] d)
  {
   if (Room == null) throw new Exception("not running");
   var to = Room.Members.FirstOrDefault(m => m.LocalPlayer == p);
   if (to == null) throw new Exception("no such player " + p);
   Cloud.Sent++;
   if (Cloud.DropBetweenClients && Room.Master != this && Room.Master != to) return;
   var label = Cloud.MislabelAsMaster && Room.Master != null && Room.Master != to ? Room.Master.LocalPlayer : LocalPlayer;
   Cloud.Queue.Add((to, label, (byte[])d.Clone()));
  }
 }
}
namespace UnityEngine { public class ScriptableObject : Object { } public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad } public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute { public RuntimeInitializeOnLoadMethodAttribute() { } public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } } }
namespace UnityEngine { public class TextAsset : Object { public string text = ""; } }
namespace Rago { public static class Spectator { public static bool Active; } }
