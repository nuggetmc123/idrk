#if FLOPZONE_FUSION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Online play over Photon Fusion 2 (Shared Mode). Every hub and match is a
    /// Photon room; whichever player Photon makes the room's master client runs
    /// the rules (<see cref="RoomHost"/>). Players talk with small JSON messages
    /// sent through Photon's reliable data channel, so no networked prefabs are
    /// needed.
    ///
    /// Parties also live here: invite anyone in your room; when the leader picks
    /// a mode, the whole party moves to the same new room together.
    ///
    /// Accounts (player ID, name, friends, reports, server bans) go to the
    /// optional Flopzone account server if one is set, otherwise they're kept on
    /// the headset (<see cref="LocalServer"/>).
    ///
    /// This file only compiles once Photon Fusion 2 is imported (the
    /// FLOPZONE_FUSION define is switched on by Setup/Editor/FusionSwitch.cs).
    /// </summary>
    public class PhotonLink : ILink, INetworkRunnerCallbacks
    {
        public bool Online => true;
        public bool Connected => true;

        readonly Queue<Dictionary<string, object>> inbox = new Queue<Dictionary<string, object>>();
        readonly LocalServer local = new LocalServer();
        WsLink account;
        bool accountFailed;
        readonly List<Dictionary<string, object>> accountQueue = new List<Dictionary<string, object>>();
        readonly List<Dictionary<string, object>> buf = new List<Dictionary<string, object>>();
        float accountStarted;

        NetworkRunner runner;
        bool busy, shuttingDownOnPurpose, inRoom;
        string myId = "", myName = "";
        bool admin;
        readonly Dictionary<PlayerRef, string> refToId = new Dictionary<PlayerRef, string>();
        readonly Dictionary<string, PlayerRef> idToRef = new Dictionary<string, PlayerRef>();
        PlayerRef hostRef;
        bool hostKnown;
        RoomHost host;
        readonly List<Dictionary<string, object>> hostQueue = new List<Dictionary<string, object>>();
        string roomKind, roomMode;
        float roomWaitUntil, autoHubAt = -1, retryAt = -1;
        string retryDest;

        // party (the leader keeps the real list and shares it)
        string partyId, partyLeader;
        readonly List<KeyValuePair<string, string>> party = new List<KeyValuePair<string, string>>();
        string invitePartyId, inviteLeader;

        public static Func<Session> CurrentSession;

        public PhotonLink(string accountServerUrl)
        {
            if (!string.IsNullOrEmpty(accountServerUrl))
            {
                account = new WsLink(accountServerUrl);
                accountStarted = Time.realtimeSinceStartup;
            }
        }

        /* ======================= ILink ======================= */

        static readonly HashSet<string> AccountTypes = new HashSet<string>
        { "hello", "set_name", "friend_req", "friend_accept", "friend_decline", "friend_remove", "friends", "report" };

        public void Send(Dictionary<string, object> m)
        {
            string t = m.Str("t");
            if (t == "report" && (account == null || accountFailed)) { ToHost(m); return; }   // tell the moderators in this room
            if (AccountTypes.Contains(t)) { ToAccount(m); return; }
            switch (t)
            {
                case "go": Go(m.Str("dest")); break;
                case "st": Pose(m); break;
                case "ev": if (m.Obj("e") != null) Broadcast(Msg.Of("_", "e", "e", m.Obj("e"))); break;
                case "hit":
                case "die":
                case "flag":
                case "chat":
                    if (t == "chat" && LocalChatBlocked(m.Str("msg"))) break;
                    ToHost(m);
                    break;
                case "mod": Mod(m); break;
                case "party_invite": PartyInvite(m.Str("id")); break;
                case "party_accept": PartyAccept(m.Str("partyId")); break;
                case "party_decline": invitePartyId = null; break;
                case "party_leave": PartyLeave(); break;
                case "party_kick": PartyKick(m.Str("id")); break;
            }
        }

        public void Poll(List<Dictionary<string, object>> into)
        {
            PumpAccount();
            if (host != null) host.Tick();
            // the master left: Photon made us the new one
            if (runner != null && inRoom && host == null && !hostKnown && Time.time > roomWaitUntil && runner.IsRunning && runner.IsSharedModeMasterClient)
                BecomeHost(CurrentSession?.Invoke());
            if (autoHubAt > 0 && Time.time >= autoHubAt) { autoHubAt = -1; AfterMatch(); }
            if (retryAt > 0 && Time.time >= retryAt) { retryAt = -1; Go(retryDest); }
            while (inbox.Count > 0) into.Add(inbox.Dequeue());
        }

        public void Close()
        {
            account?.Close();
            _ = Shutdown();
        }

        void Emit(Dictionary<string, object> m) => inbox.Enqueue(m);

        /* ======================= accounts ======================= */

        void ToAccount(Dictionary<string, object> m)
        {
            if (m.Str("t") == "hello")
            {
                if (string.IsNullOrEmpty(Save.AccountId) || Save.AccountId == "FZOFFLINE") Save.AccountId = NewId();
                m["id"] = Save.AccountId;
                m["token"] = Save.AccountToken;
            }
            if (account != null && !accountFailed)
            {
                if (account.Connected) account.Send(m);
                else accountQueue.Add(m);
                return;
            }
            LocalAccount(m);
        }

        void LocalAccount(Dictionary<string, object> m)
        {
            local.Send(m);
            buf.Clear();
            local.Poll(buf);
            foreach (var r in buf) FromAccount(r);
        }

        void PumpAccount()
        {
            if (account == null) return;
            if (!accountFailed && (account.Failed || (!account.Connected && Time.realtimeSinceStartup - accountStarted > 10f)))
            {
                accountFailed = true;
                Debug.LogWarning("[Flopzone] Account server unreachable, keeping accounts on this headset.");
                foreach (var q in accountQueue) LocalAccount(q);
                accountQueue.Clear();
            }
            if (account.Connected && accountQueue.Count > 0)
            {
                foreach (var q in accountQueue) account.Send(q);
                accountQueue.Clear();
            }
            buf.Clear();
            account.Poll(buf);
            foreach (var r in buf.ToArray()) FromAccount(r);
        }

        void FromAccount(Dictionary<string, object> m)
        {
            switch (m.Str("t"))
            {
                case "welcome":
                    myId = m.Str("id");
                    myName = m.Str("name");
                    if (m.Has("token")) { Save.AccountId = myId; Save.AccountToken = m.Str("token"); }
                    admin = m.Bool("admin") || Moderation.IsAdmin(myId);
                    m["admin"] = admin;
                    Emit(m);
                    if (Moderation.IsBanned(myId, out var why, out var until))
                        Emit(Msg.Of("t", "banned", "until", (double)until, "reason", why));
                    SendParty();
                    break;
                case "name_result":
                    if (m.Bool("ok")) myName = m.Str("name");
                    Emit(m);
                    break;
                case "kicked":
                case "banned":
                    Emit(m);
                    _ = Shutdown();
                    break;
                case "friends":
                case "toast":
                case "muted":
                case "mod_result":
                    Emit(m);
                    break;
                // rooms and parties come from Photon, not the account server
            }
        }

        static string NewId()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new StringBuilder("FZ");
            for (int i = 0; i < 6; i++) sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
            return sb.ToString();
        }

        bool LocalChatBlocked(string msg)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Save.OfflineMuteUntil > now) { Emit(Msg.Of("t", "muted", "until", (double)Save.OfflineMuteUntil, "reason", "You are muted.")); return true; }
            string bad = WordFilter.CheckChat(msg);
            if (bad == null) return false;
            Save.OfflineMuteUntil = now + WordFilter.AutoMuteMinutes * 60000L;
            Emit(Msg.Of("t", "muted", "until", (double)Save.OfflineMuteUntil, "reason", "Auto-mod: " + bad + ". You are muted for " + WordFilter.AutoMuteMinutes + " minutes."));
            return true;
        }

        void Mod(Dictionary<string, object> m)
        {
            string action = m.Str("action");
            bool server = account != null && account.Connected && !accountFailed;
            // room actions are done by the room's rules; global ones by the account server
            if (action == "kick" || action == "mute" || action == "unmute" || action == "ban" || action == "online" || action == "lookup")
            {
                if (inRoom && (action != "lookup" || !server)) ToHost(m);
            }
            if (server && action != "online") account.Send(m);
            else if (!server && (action == "unban" || action == "reports"))
                Emit(Msg.Of("t", "mod_result", "ok", false, "msg", "That needs the optional account server. For bans everywhere, add the ID to moderation.json."));
        }

        /* ======================= rooms ======================= */

        async void Go(string dest)
        {
            if (busy) return;
            bool hub = dest == "hub";
            if (!hub && !LocalServer.Modes.ContainsKey(dest)) return;
            if (partyId != null && partyLeader != myId) { Emit(Msg.Of("t", "toast", "msg", "Only the party leader picks where the party goes.")); return; }
            string name = null;
            var md = hub ? null : LocalServer.Modes[dest];
            if (partyId != null && party.Count > 1)
            {
                if (md != null && party.Count > md.max) { Emit(Msg.Of("t", "toast", "msg", $"Your party has {party.Count} people, {md.name} fits {md.max}.")); return; }
                // a fresh room for the whole party; the others follow straight away
                name = "FZ_" + Guid.NewGuid().ToString("N").Substring(0, 10);
                foreach (var kv in party)
                    if (kv.Key != myId && idToRef.TryGetValue(kv.Key, out var r))
                        SendTo(r, Msg.Of("_", "follow", "dest", dest, "session", name));
            }
            await JoinRoom(dest, name);
        }

        async Task JoinRoom(string dest, string sessionName)
        {
            if (busy) return;
            busy = true;
            try
            {
                bool hub = dest == "hub";
                var md = hub ? null : LocalServer.Modes[dest];
                await Shutdown();
                ResetRoom();
                roomKind = hub ? "hub" : "match";
                roomMode = hub ? null : dest;
                var go = new GameObject("Photon");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<NetworkRunner>();
                var scenes = go.AddComponent<NetworkSceneManagerDefault>();
                runner.AddCallbacks(this);
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Shared,
                    SessionName = sessionName,
                    SessionNameGenerator = () => "FZ_" + Guid.NewGuid().ToString("N").Substring(0, 10),
                    SessionProperties = new Dictionary<string, SessionProperty> { ["k"] = hub ? 0 : 1, ["m"] = hub ? 0 : ModeIndex(dest) },
                    IsVisible = true,
                    PlayerCount = hub ? RoomHost.HubMax : md.max,
                    SceneManager = scenes,
                };
                var r = runner;
                var start = r.StartGame(args);
                if (await Task.WhenAny(start, Task.Delay(30000)) != start)
                {
                    await Shutdown();
                    Failed(dest, "the game servers didn't answer (check the Wi-Fi)");
                    return;
                }
                var result = await start;
                if (!result.Ok)
                {
                    runner = null;
                    Failed(dest, Explain(result));
                    return;
                }
                inRoom = true;
                roomWaitUntil = Time.time + 4f;
                // say hi to everyone already here
                foreach (var p in runner.ActivePlayers) if (p != runner.LocalPlayer) SendTo(p, Hi());
                if (runner.IsSharedModeMasterClient && runner.ActivePlayers.Count() <= 1) BecomeHost(null);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Failed(dest, e.Message);
            }
            finally { busy = false; }
        }

        static int ModeIndex(string mode)
        {
            int i = 0;
            foreach (var k in LocalServer.Modes.Keys) { if (k == mode) return i + 1; i++; }
            return 0;
        }

        void Failed(string dest, string why)
        {
            inRoom = false;
            bool hopeless = why.Contains("App ID");
            Emit(Msg.Of("t", "toast", "msg", "Couldn't go online: " + why + (hopeless ? ". Playing offline for now." : ". Trying again soon...")));
            if (!hopeless) { retryDest = dest ?? "hub"; retryAt = Time.time + 8f; }
            // somewhere to stand in the meantime
            LocalRoom("hub");
        }

        /// <summary>Somewhere to stand while Photon is unreachable.</summary>
        void LocalRoom(string dest)
        {
            local.Send(Msg.Of("t", "go", "dest", dest));
            buf.Clear();
            local.Poll(buf);
            foreach (var m in buf) Emit(m);
        }

        static string Explain(StartGameResult r)
        {
            switch (r.ShutdownReason)
            {
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                    return "the Photon App ID is missing or wrong (Flopzone > Set Up Multiplayer, then build again)";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.OperationTimeout:
                    return "no internet connection";
                case ShutdownReason.GameIsFull:
                    return "that room is full";
                default:
                    return r.ShutdownReason + (string.IsNullOrEmpty(r.ErrorMessage) ? "" : " - " + r.ErrorMessage);
            }
        }

        void ResetRoom()
        {
            refToId.Clear();
            idToRef.Clear();
            hostKnown = false;
            host = null;
            hostQueue.Clear();
            inRoom = false;
            autoHubAt = -1;
        }

        async Task Shutdown()
        {
            if (runner == null) return;
            var r = runner;
            runner = null;
            inRoom = false;
            shuttingDownOnPurpose = true;
            try { if (r.IsRunning) await Task.WhenAny(r.Shutdown(), Task.Delay(8000)); }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                shuttingDownOnPurpose = false;
                if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            }
        }

        void BecomeHost(Session s)
        {
            if (host != null || runner == null) return;
            Action<string, Dictionary<string, object>> emit = HostEmit;
            if (s != null && s.RoomId == "PHOTON" && s.RoomKind == roomKind) host = RoomHost.Adopt(s, emit);
            else host = new RoomHost(roomKind, roomMode, UnityEngine.Random.Range(0, MapBuilder.Names.Length), emit);
            hostKnown = true;
            hostRef = runner.LocalPlayer;
            Broadcast(Msg.Of("_", "host"));
            if (!host.Has(myId)) host.Join(myId, myName, partyId, admin);
            foreach (var kv in idToRef) if (!host.Has(kv.Key)) host.Join(kv.Key, NameOf(kv.Key), PartyOf(kv.Key), AdminOf(kv.Key));
            foreach (var q in hostQueue) host.Handle(myId, q);
            hostQueue.Clear();
        }

        // what other players told us about themselves
        readonly Dictionary<string, (string name, string party, bool admin)> peers = new Dictionary<string, (string, string, bool)>();
        string NameOf(string id) => peers.TryGetValue(id, out var p) ? p.name : "Player";
        string PartyOf(string id) => peers.TryGetValue(id, out var p) ? p.party : null;
        bool AdminOf(string id) => peers.TryGetValue(id, out var p) && p.admin;

        void HostEmit(string to, Dictionary<string, object> m)
        {
            if (to == null)
            {
                Emit(m);
                Broadcast(Msg.Of("_", "h", "m", m));
                if (m.Str("t") == "me") autoHubAt = Time.time + 10f;
                return;
            }
            if (to == myId) { Local(m); return; }
            if (idToRef.TryGetValue(to, out var r)) SendTo(r, Msg.Of("_", "h", "m", m));
        }

        /// <summary>A message from the room's rules meant for us.</summary>
        void Local(Dictionary<string, object> m)
        {
            string t = m.Str("t");
            if (t == "_kick")
            {
                Emit(Msg.Of("t", "toast", "msg", m.Str("reason")));
                partyId = null;
                party.Clear();
                SendParty();
                Go("hub");
                return;
            }
            if (t == "me") autoHubAt = Time.time + 10f;
            Emit(m);
        }

        void AfterMatch()
        {
            // the party leader (or someone on their own) takes everyone back; members wait for the leader
            if (partyId != null && partyLeader != myId && idToRef.ContainsKey(partyLeader)) return;
            Go("hub");
        }

        void ToHost(Dictionary<string, object> m)
        {
            if (!inRoom) return;
            if (host != null) { host.Handle(myId, m); return; }
            if (hostKnown) SendTo(hostRef, Msg.Of("_", "c", "m", m));
            else hostQueue.Add(m);
        }

        void Pose(Dictionary<string, object> m)
        {
            if (!inRoom) return;
            var p = m.FloatArr("p");
            if (p == null) return;
            if (host != null && p.Length >= 3) host.SetPos(myId, new Vector3(p[0], p[1], p[2]));
            Broadcast(Msg.Of("_", "p", "p", p));
        }

        Dictionary<string, object> Hi() => Msg.Of("_", "hi", "id", myId, "name", myName, "party", partyId, "admin", admin);

        /* ======================= sending ======================= */

        void SendTo(PlayerRef p, Dictionary<string, object> m)
        {
            if (runner == null || !runner.IsRunning || p == runner.LocalPlayer) return;
            try { runner.SendReliableDataToPlayer(p, default(ReliableKey), Encoding.UTF8.GetBytes(Json.Write(m))); }
            catch (Exception e) { Debug.LogWarning("[Flopzone] send failed: " + e.Message); }
        }

        void Broadcast(Dictionary<string, object> m)
        {
            if (runner == null || !runner.IsRunning) return;
            byte[] data = null;
            foreach (var p in runner.ActivePlayers)
            {
                if (p == runner.LocalPlayer) continue;
                if (data == null) data = Encoding.UTF8.GetBytes(Json.Write(m));
                try { runner.SendReliableDataToPlayer(p, default(ReliableKey), data); }
                catch (Exception e) { Debug.LogWarning("[Flopzone] send failed: " + e.Message); }
            }
        }

        void Receive(PlayerRef from, byte[] bytes)
        {
            var m = Json.ParseObject(Encoding.UTF8.GetString(bytes));
            if (m == null) return;
            refToId.TryGetValue(from, out var fromId);
            switch (m.Str("_"))
            {
                case "hi":
                {
                    string id = m.Str("id");
                    if (string.IsNullOrEmpty(id)) return;
                    refToId[from] = id;
                    idToRef[id] = from;
                    peers[id] = (m.Str("name"), m.Has("party") ? m.Str("party") : null, m.Bool("admin"));
                    if (host != null && !host.Has(id)) host.Join(id, m.Str("name"), PartyOf(id), m.Bool("admin"));
                    break;
                }
                case "host":
                    hostRef = from;
                    hostKnown = true;
                    host = null;
                    foreach (var q in hostQueue) SendTo(hostRef, Msg.Of("_", "c", "m", q));
                    hostQueue.Clear();
                    break;
                case "c":
                    if (host != null && fromId != null && m.Obj("m") != null) host.Handle(fromId, m.Obj("m"));
                    break;
                case "h":
                    if (m.Obj("m") == null) return;
                    if (!hostKnown) { hostRef = from; hostKnown = true; }
                    if (from != hostRef) return;
                    Local(m.Obj("m"));
                    break;
                case "p":
                {
                    if (fromId == null) return;
                    var p = m.FloatArr("p");
                    Emit(Msg.Of("t", "st", "id", fromId, "p", p));
                    if (host != null && p != null && p.Length >= 3) host.SetPos(fromId, new Vector3(p[0], p[1], p[2]));
                    break;
                }
                case "e":
                    if (fromId != null) Emit(Msg.Of("t", "ev", "id", fromId, "e", m.Obj("e")));
                    break;
                case "pinv":
                    invitePartyId = m.Str("party");
                    inviteLeader = fromId;
                    Emit(Msg.Of("t", "party_invite", "partyId", invitePartyId, "from", m.Str("from"), "fromId", fromId));
                    break;
                case "pacc":
                    if (fromId != null && partyLeader == myId && m.Str("party") == partyId)
                    {
                        if (party.Count >= 6) { SendTo(from, Msg.Of("_", "toast", "msg", "That party is full.")); return; }
                        if (!party.Any(kv => kv.Key == fromId)) party.Add(new KeyValuePair<string, string>(fromId, NameOf(fromId)));
                        SendParty();
                    }
                    break;
                case "party":
                    if (fromId == null || m.Str("leader") != fromId) return;
                    partyId = m.Has("id") ? m.Str("id") : null;
                    partyLeader = m.Str("leader");
                    party.Clear();
                    var list = m.Arr("members");
                    bool stillIn = false;
                    if (list != null) foreach (var o in list)
                            if (o is Dictionary<string, object> d) { party.Add(new KeyValuePair<string, string>(d.Str("id"), d.Str("name"))); if (d.Str("id") == myId) stillIn = true; }
                    if (!stillIn) { partyId = partyLeader = null; party.Clear(); }
                    EmitParty();
                    break;
                case "pleave":
                    if (fromId != null && partyLeader == myId) { party.RemoveAll(kv => kv.Key == fromId); SendParty(); }
                    break;
                case "follow":
                    if (fromId != null && fromId == partyLeader) _ = JoinRoom(m.Str("dest"), m.Str("session"));
                    break;
                case "toast":
                    Emit(Msg.Of("t", "toast", "msg", m.Str("msg")));
                    break;
            }
        }

        /* ======================= party ======================= */

        void PartyInvite(string id)
        {
            if (!idToRef.TryGetValue(id ?? "", out var r)) { Emit(Msg.Of("t", "toast", "msg", "They need to be in the same room as you to invite them.")); return; }
            if (partyId != null && partyLeader != myId) { Emit(Msg.Of("t", "toast", "msg", "Only the party leader can invite.")); return; }
            if (partyId == null)
            {
                partyId = "P" + myId;
                partyLeader = myId;
                party.Clear();
                party.Add(new KeyValuePair<string, string>(myId, myName));
                SendParty();
            }
            if (party.Count >= 6) { Emit(Msg.Of("t", "toast", "msg", "Parties can have up to 6 people.")); return; }
            SendTo(r, Msg.Of("_", "pinv", "party", partyId, "from", myName));
            Emit(Msg.Of("t", "toast", "msg", "Party invite sent to " + NameOf(id) + "."));
        }

        void PartyAccept(string id)
        {
            if (id == null || id != invitePartyId || inviteLeader == null || !idToRef.TryGetValue(inviteLeader, out var r))
            {
                Emit(Msg.Of("t", "toast", "msg", "That party invite has expired."));
                return;
            }
            if (partyId != null && partyId != id) PartyLeave();
            SendTo(r, Msg.Of("_", "pacc", "party", id));
            invitePartyId = null;
        }

        void PartyLeave()
        {
            if (partyId == null) return;
            if (partyLeader == myId)
            {
                // hand the party to the next person
                party.RemoveAll(kv => kv.Key == myId);
                if (party.Count >= 2)
                {
                    var next = party[0].Key;
                    var msg = PartyPayload(partyId, next);
                    foreach (var kv in party) if (idToRef.TryGetValue(kv.Key, out var r)) SendTo(r, Msg.Of("_", "party", "id", partyId, "leader", next, "members", msg["members"]));
                }
                else foreach (var kv in party) if (idToRef.TryGetValue(kv.Key, out var r)) SendTo(r, Msg.Of("_", "party", "id", null, "leader", myId, "members", new List<object>()));
            }
            else if (partyLeader != null && idToRef.TryGetValue(partyLeader, out var lr)) SendTo(lr, Msg.Of("_", "pleave"));
            partyId = partyLeader = null;
            party.Clear();
            EmitParty();
        }

        void PartyKick(string id)
        {
            if (partyLeader != myId || id == myId) return;
            party.RemoveAll(kv => kv.Key == id);
            if (idToRef.TryGetValue(id, out var r)) SendTo(r, Msg.Of("_", "party", "id", null, "leader", myId, "members", new List<object>()));
            SendParty();
        }

        Dictionary<string, object> PartyPayload(string id, string leader)
        {
            var members = new List<object>();
            foreach (var kv in party) members.Add(Msg.Of("id", kv.Key, "name", kv.Value));
            return Msg.Of("t", "party", "id", id, "leader", leader, "members", members);
        }

        /// <summary>The leader shares the party list with everyone in it (who's in this room).</summary>
        void SendParty()
        {
            if (partyId != null && partyLeader == myId)
            {
                var p = PartyPayload(partyId, myId);
                foreach (var kv in party)
                    if (kv.Key != myId && idToRef.TryGetValue(kv.Key, out var r))
                        SendTo(r, Msg.Of("_", "party", "id", partyId, "leader", myId, "members", p["members"]));
            }
            EmitParty();
        }

        void EmitParty()
        {
            if (partyId == null) Emit(Msg.Of("t", "party", "id", null, "leader", null, "members", new List<object>()));
            else Emit(PartyPayload(partyId, partyLeader));
        }

        /* ======================= Fusion callbacks ======================= */

        public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
        {
            if (r != runner || player == r.LocalPlayer) return;
            SendTo(player, Hi());
            if (host != null) SendTo(player, Msg.Of("_", "host"));
        }

        public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
        {
            if (r != runner) return;
            if (refToId.TryGetValue(player, out var id))
            {
                refToId.Remove(player);
                idToRef.Remove(id);
                host?.Leave(id);
            }
            if (hostKnown && player == hostRef) { hostKnown = false; roomWaitUntil = Time.time; }
        }

        public void OnShutdown(NetworkRunner r, ShutdownReason reason)
        {
            if (shuttingDownOnPurpose || (runner != null && r != runner)) return;
            runner = null;
            inRoom = false;
            Emit(Msg.Of("t", "toast", "msg", "Disconnected (" + reason + "). Reconnecting..."));
            retryDest = "hub";
            retryAt = Time.time + 3f;
        }

        // Fusion 2.0 passes ArraySegment, 2.1+ passes ReadOnlySpan; having both means either compiles.
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            if (r != runner) return;
            var bytes = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, bytes, 0, data.Count);
            Receive(player, bytes);
        }

        public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (r != runner) return;
            Receive(player, data.ToArray());
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
#endif
