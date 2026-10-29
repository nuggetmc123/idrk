using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The rules of one Photon room (hub or match), run by whichever player
    /// Photon picks as the room's master client. Same rules as the Flopzone
    /// server: teams (parties stay together; a full 6-party in 3v3 splits 3/3),
    /// health, kills, the clock, capture the flag, chat auto-mute, room kicks.
    /// If the master leaves, the next master adopts the room from its copy of
    /// the state (<see cref="Adopt"/>).
    /// </summary>
    public class RoomHost : IBotHost
    {
        public const int HubMax = 20;
        /// <summary>How long a match waits for players before filling up with bots.</summary>
        public const float WaitForPlayers = 60f;
        float created = Time.time;
        int botSeq;
        /// <summary>Set by PhotonLink: share bot poses / shots with everyone.</summary>
        public Action<string, float[]> botPoseOut;
        public Action<string, int, Vector3, Vector3, int> botShotOut;

        static readonly string[] BotNames =
        {
            "Floppy_Joe", "Noodle", "Wobbles", "Sir_Flops", "Jellybean", "Spaghetti", "Ragtime", "Bonk",
            "Limp_Lenny", "Wiggles", "Captain_Flop", "Gloop", "Mr_Bendy", "Squishy", "Dizzy", "Tumbles"
        };

        public class P
        {
            public string id, name, party;
            public int team = -1, hp = 100, k, d;
            public bool alive = true, admin, bot;
            public float respawnAt, protectUntil;
            public Vector3 pos;
        }

        class Flag { public int team; public string state = "home"; public string carrier; public Vector3? pos; public float dropAt; }

        public readonly string kind, mode;
        public readonly int map;
        string state;
        float stateEnd, endsAt;
        int[] scores = { 0, 0 };
        Flag[] flags;
        readonly Dictionary<string, P> players = new Dictionary<string, P>();
        readonly HashSet<string> roomBans = new HashSet<string>();
        readonly Dictionary<string, long> mutes = new Dictionary<string, long>();
        readonly Dictionary<string, List<string>> chatLog = new Dictionary<string, List<string>>();
        /// <summary>(to player id, or null for everyone; message)</summary>
        readonly Action<string, Dictionary<string, object>> emit;

        LocalServer.Mode Md => mode != null && LocalServer.Modes.TryGetValue(mode, out var m) ? m : null;
        bool Teams => Md != null && Md.teams;
        static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public RoomHost(string kind, string mode, int map, Action<string, Dictionary<string, object>> emit)
        {
            this.kind = kind;
            this.mode = kind == "hub" ? null : mode;
            this.map = kind == "hub" ? -1 : map;
            this.emit = emit;
            state = kind == "hub" ? "hub" : "waiting";
            if (this.mode == "ctf") flags = new[] { new Flag { team = 0 }, new Flag { team = 1 } };
        }

        /// <summary>The old master left: carry on from what this player already knew.</summary>
        public static RoomHost Adopt(Session s, Action<string, Dictionary<string, object>> emit)
        {
            var h = new RoomHost(s.RoomKind ?? "hub", s.Mode, s.Map, emit);
            h.state = string.IsNullOrEmpty(s.State) ? h.state : s.State;
            if (h.state == "live") h.endsAt = Time.time + s.TimeLeft;
            else h.stateEnd = Time.time + s.TimeLeft;
            h.scores = new[] { s.Scores[0], s.Scores[1] };
            foreach (var pi in s.Players.Values)
            {
                h.players[pi.id] = new P
                {
                    id = pi.id, name = pi.name, team = pi.team, hp = pi.hp, k = pi.k, d = pi.d, alive = pi.alive, admin = pi.admin, bot = pi.bot,
                    respawnAt = pi.alive ? 0 : Time.time + 3f
                };
            }
            if (h.flags != null && s.Flags != null && s.Flags.Length == 2)
                for (int t = 0; t < 2; t++)
                {
                    h.flags[t].state = s.Flags[t].state;
                    h.flags[t].carrier = s.Flags[t].carrier;
                    h.flags[t].pos = s.Flags[t].pos;
                    h.flags[t].dropAt = Time.time;
                }
            return h;
        }

        public bool Has(string id) => players.ContainsKey(id);

        /* ---------------- joining and leaving ---------------- */

        public void Join(string id, string name, string party, bool admin)
        {
            if (players.ContainsKey(id)) return;
            if (roomBans.Contains(id))
            {
                emit(id, Msg.Of("t", "_kick", "reason", "You were removed from that room."));
                return;
            }
            // a full room (bots included): a real player takes a bot's place
            if (Md != null && players.Count >= Md.max)
            {
                P bot = null;
                foreach (var q in players.Values) if (q.bot) { bot = q; if (!Teams || TeamCounts()[q.team] >= TeamCounts()[1 - q.team]) break; }
                if (bot != null) Leave(bot.id);
            }
            var p = new P { id = id, name = UniqueName(name), party = party, admin = admin, team = PickTeam(party), protectUntil = Time.time + 1.5f };
            players[id] = p;
            if (p.name != name) emit(id, Msg.Of("t", "toast", "msg", "Someone here is already called " + name + ", so you're " + p.name + " in this room."));
            emit(id, RoomPayload(p));
            emit(null, Msg.Of("t", "pj", "p", PlayerPayload(p)));
            CheckStart();
        }

        string UniqueName(string name)
        {
            string n = string.IsNullOrEmpty(name) ? "Player" : name;
            int i = 2;
            string tryName = n;
            while (NameTaken(tryName)) tryName = n + (i++);
            return tryName;
        }

        bool NameTaken(string n)
        {
            foreach (var p in players.Values) if (string.Equals(p.name, n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        int PickTeam(string party)
        {
            var md = Md;
            if (md == null || !md.teams) return -1;
            var n = TeamCounts();
            // keep a party together while there's room on its team; the rest go to the other side
            if (!string.IsNullOrEmpty(party))
                foreach (var p in players.Values)
                    if (p.party == party && (p.team == 0 || p.team == 1) && n[p.team] < md.teamSize) return p.team;
            if (n[0] == n[1]) return UnityEngine.Random.value < 0.5f ? 0 : 1;
            return n[0] < n[1] ? 0 : 1;
        }

        int[] TeamCounts()
        {
            var n = new[] { 0, 0 };
            foreach (var p in players.Values) if (p.team == 0 || p.team == 1) n[p.team]++;
            return n;
        }

        public void Leave(string id)
        {
            if (!players.ContainsKey(id)) return;
            DropFlag(id);
            players.Remove(id);
            emit(null, Msg.Of("t", "pl", "id", id));
            if (kind != "match" || state != "live") return;
            var n = TeamCounts();
            if (Teams && (n[0] == 0 || n[1] == 0)) End(n[0] > 0 ? 0 : 1, null, "The other team left.");
            else if (!Teams && players.Count < 2) { foreach (var p in players.Values) End(-1, p.id, "Everyone else left."); }
        }

        public void SetPos(string id, Vector3 pos)
        {
            if (players.TryGetValue(id, out var p)) p.pos = pos;
        }

        /* ---------------- payloads ---------------- */

        static Dictionary<string, object> PlayerPayload(P p)
            => Msg.Of("id", p.id, "name", p.name, "team", p.team, "hp", p.hp, "alive", p.alive, "k", p.k, "d", p.d, "admin", p.admin, "bot", p.bot);

        List<object> Scores() => new List<object> { (double)scores[0], (double)scores[1] };

        float TimeLeft()
        {
            if (state == "live") return Mathf.Max(0, endsAt - Time.time);
            if (state == "countdown" || state == "ended") return Mathf.Max(0, stateEnd - Time.time);
            return 0;
        }

        List<object> FlagsPayload()
        {
            if (flags == null) return null;
            var l = new List<object>();
            foreach (var f in flags)
                l.Add(Msg.Of("team", f.team, "state", f.state, "carrier", f.carrier,
                    "pos", f.pos.HasValue ? new List<object> { (double)f.pos.Value.x, (double)f.pos.Value.y, (double)f.pos.Value.z } : null));
            return l;
        }

        Dictionary<string, object> RoomPayload(P you)
        {
            var list = new List<object>();
            foreach (var p in players.Values) list.Add(PlayerPayload(p));
            return Msg.Of("t", "room", "id", "PHOTON", "kind", kind, "mode", mode, "map", map, "state", state,
                "time", (double)TimeLeft(), "scores", Scores(), "flags", FlagsPayload(), "team", you.team,
                "spawn", UnityEngine.Random.Range(0, 1000), "players", list);
        }

        /* ---------------- match flow ---------------- */

        /// <summary>Start as soon as the room is full; a running countdown keeps going while there are two sides.</summary>
        bool CanStart()
        {
            var md = Md;
            if (md == null) return false;
            if (state == "waiting") return players.Count >= md.max;
            if (players.Count < 2) return false;
            if (!Teams) return true;
            var n = TeamCounts();
            return n[0] > 0 && n[1] > 0;
        }

        int Humans()
        {
            int n = 0;
            foreach (var p in players.Values) if (!p.bot) n++;
            return n;
        }

        /// <summary>Waited long enough: fill every empty seat with a bot.</summary>
        void FillBots()
        {
            var md = Md;
            if (md == null) return;
            var names = new List<string>(BotNames);
            foreach (var p in players.Values) names.Remove(p.name);
            while (players.Count < md.max && names.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, names.Count);
                var b = new P { id = "BOT" + (++botSeq) + UnityEngine.Random.Range(100, 999), name = names[pick], bot = true, team = PickTeam(null), protectUntil = Time.time + 1.5f };
                names.RemoveAt(pick);
                players[b.id] = b;
                emit(null, Msg.Of("t", "pj", "p", PlayerPayload(b)));
            }
        }

        void MatchState(string note) => emit(null, Msg.Of("t", "ms", "state", state, "time", (double)TimeLeft(), "scores", Scores(), "note", note));

        void CheckStart()
        {
            if (kind == "match" && state == "waiting" && CanStart())
            {
                state = "countdown";
                stateEnd = Time.time + 5f;
                MatchState("");
            }
        }

        public void Tick()
        {
            if (kind != "match") return;
            float t = Time.time;
            if (state == "waiting")
            {
                if (Humans() > 0 && t - created >= WaitForPlayers)
                {
                    FillBots();
                    state = "countdown";
                    stateEnd = t + 5f;
                    MatchState("Not enough players yet, so bots are filling the empty spots.");
                }
                else CheckStart();
            }
            else if (state == "countdown")
            {
                if (!CanStart()) { state = "waiting"; MatchState("Waiting for players"); }
                else if (t >= stateEnd) StartMatch();
            }
            else if (state == "live" && t >= endsAt) TimeUp();
            if (state == "ended") return;
            foreach (var p in players.Values)
                if (!p.alive && p.respawnAt > 0 && t >= p.respawnAt) Respawn(p);
            if (flags != null)
                foreach (var f in flags)
                    if (f.state == "dropped" && t - f.dropAt >= 20f) { f.state = "home"; f.pos = null; SendFlags($"The {TeamName(f.team)} flag went home."); }
        }

        static string TeamName(int t) => t == 1 ? "Blue" : "Red";

        void StartMatch()
        {
            var md = Md;
            state = "live";
            endsAt = Time.time + md.time;
            scores = new[] { 0, 0 };
            foreach (var p in players.Values) { p.k = 0; p.d = 0; }
            if (flags != null) foreach (var f in flags) { f.state = "home"; f.carrier = null; f.pos = null; }
            MatchState("FIGHT!");
            foreach (var p in players.Values) Respawn(p);
            if (flags != null) SendFlags("");
        }

        void Respawn(P p)
        {
            p.alive = true; p.hp = 100; p.respawnAt = 0; p.protectUntil = Time.time + 1.5f;
            emit(null, Msg.Of("t", "rs", "id", p.id, "spawn", UnityEngine.Random.Range(0, 1000)));
        }

        void TimeUp()
        {
            if (Teams) End(scores[0] == scores[1] ? -1 : (scores[0] > scores[1] ? 0 : 1), null, "Time's up!");
            else
            {
                P best = null;
                foreach (var p in players.Values) if (best == null || p.k > best.k) best = p;
                End(-1, best?.id, "Time's up!");
            }
        }

        void End(int winnerTeam, string winnerId, string note)
        {
            if (state == "ended") return;
            state = "ended";
            stateEnd = Time.time + 10f;
            var list = new List<object>();
            foreach (var p in players.Values) list.Add(Msg.Of("id", p.id, "name", p.name, "team", p.team, "k", p.k, "d", p.d));
            emit(null, Msg.Of("t", "me", "winnerTeam", winnerTeam, "winnerId", winnerId, "note", note, "scores", Scores(), "players", list));
        }

        /* ---------------- messages from players ---------------- */

        public void Handle(string from, Dictionary<string, object> m)
        {
            if (!players.TryGetValue(from, out var p)) return;
            switch (m.Str("t"))
            {
                case "hit": Hit(p, m); break;
                case "die": if (p.alive && kind == "match") Kill(p, null, "fall", false, null); break;
                case "flag": FlagAction(p, m.Str("a"), m.Int("team", -1)); break;
                case "chat": Chat(p, m.Str("msg")); break;
                case "mod": Mod(p, m); break;
                case "report": Report(p, m); break;
            }
        }

        void Hit(P s, Dictionary<string, object> m) => HitCore(s, m.Str("target"), (float)m.Num("dmg"), m.Str("gun"), m.Bool("head"), m.FloatArr("dir"));

        void HitCore(P s, string targetId, float dmg, string gun, bool head, float[] dir)
        {
            if (kind != "match" || state == "countdown" || state == "ended") return;
            if (s == null || !players.TryGetValue(targetId ?? "", out var t)) return;
            if (!s.alive || !t.alive || s == t || (Teams && s.team == t.team) || Time.time < t.protectUntil) return;
            dmg = Mathf.Clamp(dmg, 0, 260);
            if (dmg <= 0) return;
            s.protectUntil = 0;
            t.hp = Mathf.Max(0, Mathf.RoundToInt(t.hp - dmg));
            emit(null, Msg.Of("t", "hp", "id", t.id, "hp", t.hp, "by", s.id));
            if (t.hp <= 0) Kill(t, s, gun, head, dir);
        }

        /* ---------------- IBotHost: the bots run on this headset ---------------- */

        public void ApplyHit(string shooterId, string targetId, float dmg, string gun, bool head, float[] dir)
        {
            players.TryGetValue(shooterId ?? "", out var s);
            HitCore(s, targetId, dmg, gun, head, dir);
        }

        public void BotFell(string id)
        {
            if (players.TryGetValue(id, out var p) && p.alive && kind == "match") Kill(p, null, "fall", false, null);
        }

        public void FlagAction(string id, string a, int team)
        {
            if (players.TryGetValue(id, out var p)) FlagAction(p, a, team);
        }

        public string FlagCarrier(int team) => flags != null && flags[team].state == "carried" ? flags[team].carrier : null;
        public string FlagState(int team) => flags?[team].state;
        public Vector3? FlagPos(int team) => flags?[team].pos;
        public void BotPose(string id, float[] pose) => botPoseOut?.Invoke(id, pose);
        public void BotShot(string id, int hand, Vector3 from, Vector3 to, int gun) => botShotOut?.Invoke(id, hand, from, to, gun);

        void Kill(P victim, P killer, string gun, bool head, float[] dir)
        {
            victim.alive = false;
            victim.hp = 0;
            victim.respawnAt = Time.time + 3f;
            bool live = state == "live";
            if (live)
            {
                victim.d++;
                if (killer != null)
                {
                    killer.k++;
                    if (Teams && mode != "ctf") scores[killer.team]++;
                }
            }
            emit(null, Msg.Of("t", "kill", "victim", victim.id, "killer", killer?.id, "gun", gun, "head", head,
                "dir", dir != null && dir.Length >= 3 ? new List<object> { (double)dir[0], (double)dir[1], (double)dir[2] } : null,
                "scores", Scores(), "kk", killer != null ? killer.k : 0, "vd", victim.d));
            DropFlag(victim.id);
            var md = Md;
            if (!live || killer == null || md == null || md.killLimit == 0) return;
            if (md.teams && scores[killer.team] >= md.killLimit) End(killer.team, null, "");
            else if (!md.teams && killer.k >= md.killLimit) End(-1, killer.id, "");
        }

        void SendFlags(string note) => emit(null, Msg.Of("t", "fl", "flags", FlagsPayload(), "scores", Scores(), "note", note));

        void DropFlag(string carrier)
        {
            if (flags == null) return;
            foreach (var f in flags)
            {
                if (f.state != "carried" || f.carrier != carrier) continue;
                f.carrier = null;
                f.dropAt = Time.time;
                if (players.TryGetValue(carrier, out var p)) { f.state = "dropped"; f.pos = p.pos; }
                else { f.state = "home"; f.pos = null; }
                SendFlags($"The {TeamName(f.team)} flag was dropped!");
            }
        }

        void FlagAction(P p, string a, int team)
        {
            if (flags == null || state != "live" || !p.alive || p.team < 0) return;
            if (a == "touch" && (team == 0 || team == 1))
            {
                var f = flags[team];
                if (f.team != p.team && (f.state == "home" || f.state == "dropped")) { f.state = "carried"; f.carrier = p.id; f.pos = null; SendFlags($"{p.name} took the {TeamName(f.team)} flag!"); }
                else if (f.team == p.team && f.state == "dropped") { f.state = "home"; f.pos = null; SendFlags($"{p.name} returned the {TeamName(f.team)} flag."); }
            }
            else if (a == "cap")
            {
                var mine = flags[p.team];
                var theirs = flags[1 - p.team];
                if (theirs.carrier != p.id || mine.state != "home") return;
                theirs.state = "home"; theirs.carrier = null; theirs.pos = null;
                scores[p.team]++;
                SendFlags($"{p.name} captured the flag!");
                if (scores[p.team] >= LocalServer.Modes["ctf"].capLimit) End(p.team, null, "");
            }
        }

        void Chat(P p, string msg)
        {
            msg = (msg ?? "").Trim();
            if (msg.Length == 0) return;
            if (msg.Length > 120) msg = msg.Substring(0, 120);
            if (mutes.TryGetValue(p.id, out var until) && until > NowMs)
            {
                emit(p.id, Msg.Of("t", "muted", "until", (double)until, "reason", "You are muted."));
                return;
            }
            string bad = WordFilter.CheckChat(msg);
            if (bad != null)
            {
                until = NowMs + WordFilter.AutoMuteMinutes * 60000L;
                mutes[p.id] = until;
                Log(p.id, "[blocked] " + msg);
                emit(p.id, Msg.Of("t", "muted", "until", (double)until, "reason", "Auto-mod: " + bad + ". You are muted for " + WordFilter.AutoMuteMinutes + " minutes."));
                return;
            }
            Log(p.id, msg);
            emit(null, Msg.Of("t", "chat", "id", p.id, "name", p.name, "msg", msg));
        }

        void Log(string id, string line)
        {
            if (!chatLog.TryGetValue(id, out var l)) chatLog[id] = l = new List<string>();
            l.Add(line);
            if (l.Count > 6) l.RemoveAt(0);
        }

        public List<string> RecentChat(string id) => chatLog.TryGetValue(id, out var l) ? l : new List<string>();

        void Report(P by, Dictionary<string, object> m)
        {
            var t = Find(m.Str("target"));
            if (t == null) return;
            int told = 0;
            foreach (var a in players.Values)
                if (a.admin && a != by) { emit(a.id, Msg.Of("t", "toast", "msg", $"Report: {by.name} reported {t.name} [{t.id}] ({m.Str("reason")}).")); told++; }
            emit(by.id, Msg.Of("t", "toast", "msg", told > 0 ? "Thanks, the moderators here were told." : "Thanks. No moderator is in this room right now; tell the game owner their player ID: " + t.id));
        }

        P Find(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (players.TryGetValue(target.ToUpperInvariant(), out var p)) return p;
            foreach (var q in players.Values) if (string.Equals(q.name, target, StringComparison.OrdinalIgnoreCase)) return q;
            return null;
        }

        /// <summary>Room-level moderation (a moderator in the same room).</summary>
        void Mod(P admin, Dictionary<string, object> m)
        {
            void Result(bool ok, string msg, object data = null) => emit(admin.id, Msg.Of("t", "mod_result", "ok", ok, "msg", msg, "data", data));
            if (!admin.admin) { Result(false, "You are not a moderator."); return; }
            string action = m.Str("action");
            if (action == "online")
            {
                var list = new List<object>();
                foreach (var q in players.Values) list.Add(Msg.Of("id", q.id, "name", q.name, "where", "this room"));
                Result(true, players.Count + " in this room", list);
                return;
            }
            var t = Find(m.Str("target"));
            if (t == null) { Result(false, "Nobody called \"" + m.Str("target") + "\" is in this room."); return; }
            string label = t.name + " [" + t.id + "]";
            int mins = Mathf.Max(0, m.Int("minutes"));
            switch (action)
            {
                case "lookup":
                    Result(true, label, Msg.Of("online", true, "where", "this room", "banned", roomBans.Contains(t.id) ? "from this room" : "no",
                        "muted", mutes.TryGetValue(t.id, out var mu) && mu > NowMs ? "yes" : "no", "reports", 0, "recentChat", new List<object>(RecentChat(t.id))));
                    break;
                case "kick":
                    emit(t.id, Msg.Of("t", "_kick", "reason", "A moderator removed you from that room."));
                    Result(true, "Kicked " + label + ".");
                    break;
                case "ban":
                    roomBans.Add(t.id);
                    emit(t.id, Msg.Of("t", "_kick", "reason", "A moderator banned you from that room."));
                    Result(true, "Banned " + label + " from this room. For a ban everywhere, add their ID to moderation.json (see the README).");
                    break;
                case "mute":
                {
                    long until = NowMs + (mins > 0 ? mins : 60) * 60000L;
                    mutes[t.id] = until;
                    emit(t.id, Msg.Of("t", "muted", "until", (double)until, "reason", "A moderator muted you."));
                    Result(true, "Muted " + label + ".");
                    break;
                }
                case "unmute":
                    mutes.Remove(t.id);
                    emit(t.id, Msg.Of("t", "muted", "until", 0.0, "reason", "You were unmuted."));
                    Result(true, "Unmuted " + label + ".");
                    break;
                case "unban":
                    roomBans.Remove(t.id);
                    Result(true, "Unbanned " + label + " from this room.");
                    break;
                default:
                    Result(false, "That needs the optional account server.");
                    break;
            }
        }
    }
}
