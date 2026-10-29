using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    public class PlayerInfo
    {
        public string id, name;
        public int team = -1, hp = 100, k, d;
        public bool alive = true, bot, admin;
    }

    public class FriendInfo
    {
        public string id, name, where;
        public bool online;
    }

    public class FlagInfo
    {
        public int team;
        public string state = "home";
        public string carrier;
        public Vector3? pos;
    }

    /// <summary>
    /// Everything the server has told us (who we are, friends, party, the
    /// room we're in, who's in it) plus helpers to send requests. Works the
    /// same over the internet or against the offline LocalServer.
    /// </summary>
    public class Session
    {
        public ILink link;
        public string MyId = "", MyName = "";
        public bool IsAdmin, NeedsName, Welcomed;
        public long MuteUntil;
        public long ServerOffsetMs;
        public readonly List<FriendInfo> Friends = new List<FriendInfo>();
        public readonly List<FriendInfo> Incoming = new List<FriendInfo>();
        public readonly List<FriendInfo> Outgoing = new List<FriendInfo>();
        public string PartyId, PartyLeader;
        public readonly List<KeyValuePair<string, string>> PartyMembers = new List<KeyValuePair<string, string>>();
        public string PendingInvitePartyId, PendingInviteFrom;

        public string RoomId, RoomKind, Mode, State = "";
        /// <summary>A private match: just the party (plus bots), no strangers, no waiting.</summary>
        public bool PrivateMatch;
        public int Map = -1, MyTeam = -1, SpawnIndex;
        public float StateEndsAt;
        public int[] Scores = { 0, 0 };
        public readonly Dictionary<string, PlayerInfo> Players = new Dictionary<string, PlayerInfo>();
        public FlagInfo[] Flags;
        public readonly List<string> ChatLog = new List<string>();
        public Dictionary<string, object> LastMatchEnd;

        public bool Online => link != null && link.Online;
        public bool InParty => !string.IsNullOrEmpty(PartyId);
        public bool IsLeaderOrSolo => !InParty || PartyLeader == MyId;
        public bool InMatch => RoomKind == "match";
        public bool InHub => RoomKind == "hub";
        public bool IsMuted => MuteUntil > NowMs;
        public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ServerOffsetMs;
        public float TimeLeft => Mathf.Max(0, StateEndsAt - Time.time);
        public PlayerInfo Me => Players.TryGetValue(MyId, out var p) ? p : null;

        public event Action Welcome, NeedName, FriendsChanged, PartyChanged, Closed;
        public event Action<bool, string> NameResult;
        public event Action<string, string> PartyInvite;           // partyId, from
        public event Action RoomEntered;
        public event Action<PlayerInfo> PlayerJoined;
        public event Action<string> PlayerLeft;
        public event Action<string, float[]> Pose;
        public event Action<string, Dictionary<string, object>> Event;
        public event Action<string, int, string> Hp;               // id, hp, by
        public event Action<string, string, string, bool, Vector3> Kill; // victim, killer, gun, head, dir
        public event Action<string, int> Respawn;                  // id, spawn index
        public event Action<string, string> MatchState;            // state, note
        public event Action<Dictionary<string, object>> MatchEnd;
        public event Action<string> FlagsChanged;                  // note
        public event Action<string, string, string> Chat;          // id, name, msg
        public event Action<string> Toast;
        public event Action<long, string> Muted;
        public event Action<string> Kicked;
        public event Action<long, string> Banned;
        public event Action<Dictionary<string, object>> ModResult;
        public event Action<string, string> NameChanged;
        /// <summary>The party leader boarded the lobby helicopter and pulled us in.</summary>
        public event Action Board;

        readonly List<Dictionary<string, object>> buf = new List<Dictionary<string, object>>();

        public Session(ILink link)
        {
            this.link = link;
        }

        public void Tick()
        {
            buf.Clear();
            link.Poll(buf);
            foreach (var m in buf)
            {
                try { Handle(m); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void Handle(Dictionary<string, object> m)
        {
            switch (m.Str("t"))
            {
                case "welcome":
                    MyId = m.Str("id");
                    MyName = m.Str("name");
                    IsAdmin = m.Bool("admin");
                    NeedsName = m.Bool("needsName");
                    MuteUntil = (long)m.Num("muteUntil");
                    if (m.Has("serverTime")) ServerOffsetMs = (long)m.Num("serverTime") - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (m.Has("token")) { Save.AccountId = MyId; Save.AccountToken = m.Str("token"); }
                    if (Online && !string.IsNullOrEmpty(MyName)) Save.OfflineName = MyName;   // offline play reuses it
                    bool first = !Welcomed;
                    Welcomed = true;
                    if (NeedsName) NeedName?.Invoke();
                    if (first) Welcome?.Invoke();
                    break;
                case "name_result":
                    if (m.Bool("ok"))
                    {
                        MyName = m.Str("name");
                        NeedsName = false;
                        Save.OfflineName = MyName;
                        if (Players.TryGetValue(MyId, out var me)) me.name = MyName;
                    }
                    NameResult?.Invoke(m.Bool("ok"), m.Str("error"));
                    break;
                case "friends":
                    ReadFriends(m.Arr("friends"), Friends);
                    ReadFriends(m.Arr("incoming"), Incoming);
                    ReadFriends(m.Arr("outgoing"), Outgoing);
                    FriendsChanged?.Invoke();
                    break;
                case "party":
                    PartyId = m.Has("id") ? m.Str("id") : null;
                    PartyLeader = m.Has("leader") ? m.Str("leader") : null;
                    PartyMembers.Clear();
                    var members = m.Arr("members");
                    if (members != null)
                        foreach (var o in members)
                            if (o is Dictionary<string, object> pm) PartyMembers.Add(new KeyValuePair<string, string>(pm.Str("id"), pm.Str("name")));
                    PartyChanged?.Invoke();
                    break;
                case "party_invite":
                    PendingInvitePartyId = m.Str("partyId");
                    PendingInviteFrom = m.Str("from");
                    PartyInvite?.Invoke(PendingInvitePartyId, PendingInviteFrom);
                    break;
                case "room":
                    RoomId = m.Str("id");
                    RoomKind = m.Str("kind");
                    Mode = m.Str("mode", null);
                    PrivateMatch = m.Bool("priv");
                    Map = m.Int("map", -1);
                    State = m.Str("state");
                    StateEndsAt = Time.time + (float)m.Num("time");
                    MyTeam = m.Int("team", -1);
                    SpawnIndex = m.Int("spawn");
                    Scores = ReadScores(m.Arr("scores"));
                    Players.Clear();
                    var list = m.Arr("players");
                    if (list != null) foreach (var o in list) if (o is Dictionary<string, object> pd) { var p = ReadPlayer(pd); Players[p.id] = p; }
                    ReadFlags(m.Arr("flags"));
                    ChatLog.Clear();
                    RoomEntered?.Invoke();
                    break;
                case "pj":
                {
                    var p = ReadPlayer(m.Obj("p"));
                    Players[p.id] = p;
                    PlayerJoined?.Invoke(p);
                    break;
                }
                case "pl":
                    Players.Remove(m.Str("id"));
                    PlayerLeft?.Invoke(m.Str("id"));
                    break;
                case "pn":
                    if (Players.TryGetValue(m.Str("id"), out var renamed)) renamed.name = m.Str("name");
                    NameChanged?.Invoke(m.Str("id"), m.Str("name"));
                    break;
                case "st":
                    Pose?.Invoke(m.Str("id"), m.FloatArr("p"));
                    break;
                case "ev":
                    Event?.Invoke(m.Str("id"), m.Obj("e"));
                    break;
                case "hp":
                    if (Players.TryGetValue(m.Str("id"), out var hpP)) hpP.hp = m.Int("hp");
                    Hp?.Invoke(m.Str("id"), m.Int("hp"), m.Str("by"));
                    break;
                case "kill":
                {
                    string victim = m.Str("victim"), killer = m.Has("killer") ? m.Str("killer") : null;
                    if (Players.TryGetValue(victim, out var v)) { v.alive = false; v.hp = 0; v.d = m.Int("vd", v.d); }
                    if (killer != null && Players.TryGetValue(killer, out var kp)) kp.k = m.Int("kk", kp.k);
                    Scores = ReadScores(m.Arr("scores"));
                    var dir = Vector3.zero;
                    var d = m.FloatArr("dir");
                    if (d != null && d.Length >= 3) dir = new Vector3(d[0], d[1], d[2]);
                    Kill?.Invoke(victim, killer, m.Str("gun"), m.Bool("head"), dir);
                    break;
                }
                case "rs":
                    if (Players.TryGetValue(m.Str("id"), out var rp)) { rp.alive = true; rp.hp = 100; }
                    Respawn?.Invoke(m.Str("id"), m.Int("spawn"));
                    break;
                case "ms":
                    State = m.Str("state");
                    StateEndsAt = Time.time + (float)m.Num("time");
                    Scores = ReadScores(m.Arr("scores"));
                    if (State == "live") foreach (var p in Players.Values) { p.k = 0; p.d = 0; }
                    MatchState?.Invoke(State, m.Str("note"));
                    break;
                case "me":
                    State = "ended";
                    StateEndsAt = Time.time + 10f;
                    Scores = ReadScores(m.Arr("scores"));
                    LastMatchEnd = m;
                    MatchEnd?.Invoke(m);
                    break;
                case "fl":
                    ReadFlags(m.Arr("flags"));
                    Scores = ReadScores(m.Arr("scores"));
                    FlagsChanged?.Invoke(m.Str("note"));
                    break;
                case "chat":
                {
                    string line = m.Str("name") + ": " + m.Str("msg");
                    ChatLog.Add(line);
                    while (ChatLog.Count > 30) ChatLog.RemoveAt(0);
                    Chat?.Invoke(m.Str("id"), m.Str("name"), m.Str("msg"));
                    break;
                }
                case "toast":
                    Toast?.Invoke(m.Str("msg"));
                    break;
                case "muted":
                    MuteUntil = (long)m.Num("until");
                    Muted?.Invoke(MuteUntil, m.Str("reason"));
                    break;
                case "kicked":
                    Kicked?.Invoke(m.Str("reason"));
                    break;
                case "banned":
                    Banned?.Invoke((long)m.Num("until"), m.Str("reason"));
                    break;
                case "mod_result":
                    ModResult?.Invoke(m);
                    break;
                case "board":
                    Board?.Invoke();
                    break;
                case "_closed":
                    Closed?.Invoke();
                    break;
            }
        }

        static int[] ReadScores(List<object> l)
        {
            var f = l.Floats();
            return f != null && f.Length >= 2 ? new[] { (int)f[0], (int)f[1] } : new[] { 0, 0 };
        }

        static PlayerInfo ReadPlayer(Dictionary<string, object> d)
        {
            return new PlayerInfo
            {
                id = d.Str("id"), name = d.Str("name"), team = d.Int("team", -1), hp = d.Int("hp", 100),
                alive = d.Bool("alive", true), k = d.Int("k"), d = d.Int("d"), bot = d.Bool("bot"), admin = d.Bool("admin")
            };
        }

        static void ReadFriends(List<object> src, List<FriendInfo> dst)
        {
            dst.Clear();
            if (src == null) return;
            foreach (var o in src)
                if (o is Dictionary<string, object> f)
                    dst.Add(new FriendInfo { id = f.Str("id"), name = f.Str("name"), online = f.Bool("online"), where = f.Str("where") });
        }

        void ReadFlags(List<object> l)
        {
            if (l == null) { Flags = null; return; }
            Flags = new FlagInfo[l.Count];
            for (int i = 0; i < l.Count; i++)
            {
                var f = l[i] as Dictionary<string, object>;
                var fi = new FlagInfo { team = f.Int("team", i), state = f.Str("state", "home"), carrier = f.Has("carrier") ? f.Str("carrier") : null };
                var pos = f.FloatArr("pos");
                if (pos != null && pos.Length >= 3) fi.pos = new Vector3(pos[0], pos[1], pos[2]);
                Flags[i] = fi;
            }
        }

        /* ---------------- requests ---------------- */

        void Send(params object[] kv) => link.Send(Msg.Of(kv));

        public void Hello() => Send("t", "hello", "id", Save.AccountId, "token", Save.AccountToken, "ver", 1);
        public void SetName(string n) => Send("t", "set_name", "name", n);
        public void FriendRequest(string target) => Send("t", "friend_req", "target", target);
        public void FriendAccept(string id) => Send("t", "friend_accept", "id", id);
        public void FriendDecline(string id) => Send("t", "friend_decline", "id", id);
        public void FriendRemove(string id) => Send("t", "friend_remove", "id", id);
        public void PartyInviteFriend(string id) => Send("t", "party_invite", "id", id);
        public void PartyAccept(string partyId) => Send("t", "party_accept", "partyId", partyId);
        public void PartyDecline(string partyId) => Send("t", "party_decline", "partyId", partyId);
        public void PartyLeave() => Send("t", "party_leave");
        public void PartyKick(string id) => Send("t", "party_kick", "id", id);
        public void Go(string dest, bool priv = false) => Send("t", "go", "dest", dest, "priv", priv);
        public void SendPose(float[] p) => Send("t", "st", "p", p);
        public void SendEvent(Dictionary<string, object> e) => Send("t", "ev", "e", e);
        public void SendHit(string target, float dmg, bool head, string gun, Vector3 dir)
            => Send("t", "hit", "target", target, "dmg", Mathf.Round(dmg), "head", head, "gun", gun, "dir", new[] { dir.x, dir.y, dir.z });
        public void SendDie() => Send("t", "die");
        public void SendFlag(string a, int team) => Send("t", "flag", "a", a, "team", team);
        public void SendChat(string msg) => Send("t", "chat", "msg", msg);
        public void SendReport(string target, string reason) => Send("t", "report", "target", target, "reason", reason);
        public void SendMod(string action, string target, int minutes, string reason)
            => Send("t", "mod", "action", action, "target", target, "minutes", minutes, "reason", reason);
        public void Ping() => Send("t", "ping");
        public void BoardParty() => Send("t", "party_board");
        /// <summary>Offline: stop searching and fill the match with bots now.</summary>
        public void StartNow() => Send("t", "start_now");
    }
}
