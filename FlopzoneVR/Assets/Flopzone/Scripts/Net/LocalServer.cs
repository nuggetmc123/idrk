using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Offline play: speaks the same protocol as the online server, runs the
    /// same match rules, and fills matches with bots. Used when no server URL
    /// is set (or the server can't be reached).
    /// </summary>
    public class LocalServer : ILink
    {
        public bool Online => false;
        public bool Connected => true;

        public class Mode
        {
            public string name; public bool teams; public int teamSize, max, killLimit, capLimit; public float time;
        }

        public static readonly Dictionary<string, Mode> Modes = new Dictionary<string, Mode>
        {
            { "tdm", new Mode { name = "Team Deathmatch", teams = true, teamSize = 5, max = 10, killLimit = 40, time = 480 } },
            { "ffa", new Mode { name = "Free For All", teams = false, teamSize = 8, max = 8, killLimit = 20, time = 480 } },
            { "1v1", new Mode { name = "1v1", teams = true, teamSize = 1, max = 2, killLimit = 10, time = 300 } },
            { "2v2", new Mode { name = "2v2", teams = true, teamSize = 2, max = 4, killLimit = 15, time = 360 } },
            { "3v3", new Mode { name = "3v3", teams = true, teamSize = 3, max = 6, killLimit = 20, time = 420 } },
            { "ctf", new Mode { name = "Capture The Flag", teams = true, teamSize = 5, max = 10, capLimit = 3, time = 600 } },
        };

        static readonly string[] BotNames =
        {
            "Floppy_Joe", "Noodle", "Wobbles", "Sir_Flops", "Jellybean", "Spaghetti", "Ragtime", "Bonk",
            "Limp_Lenny", "Wiggles", "Captain_Flop", "Gloop", "Mr_Bendy", "Squishy", "Dizzy", "Tumbles"
        };

        public class P
        {
            public string id, name;
            public int team = -1, hp = 100, k, d;
            public bool alive = true, bot;
            public float respawnAt, protectUntil;
            public Vector3 pos;
        }

        class Flag { public int team; public string state = "home"; public string carrier; public Vector3? pos; public float dropAt; }

        readonly Queue<Dictionary<string, object>> outbox = new Queue<Dictionary<string, object>>();
        string myId = "FZOFFLINE";
        string myName = "";
        string kind, mode, state = "";
        int map = -1, roomSeq;
        float stateEnd, endsAt;
        int[] scores = { 0, 0 };
        Flag[] flags;
        public readonly Dictionary<string, P> players = new Dictionary<string, P>();

        public bool IsBot(string id) => players.TryGetValue(id, out var p) && p.bot;
        public string MatchStateName => state;

        void Out(params object[] kv) => outbox.Enqueue(Msg.Of(kv));

        public void Poll(List<Dictionary<string, object>> into)
        {
            Tick();
            while (outbox.Count > 0) into.Add(outbox.Dequeue());
        }

        public void Close() { }

        public void Send(Dictionary<string, object> m)
        {
            switch (m.Str("t"))
            {
                case "hello":
                    if (!string.IsNullOrEmpty(m.Str("id"))) myId = m.Str("id");
                    myName = Save.OfflineName;
                    Out("t", "welcome", "id", myId, "name", myName, "needsName", string.IsNullOrEmpty(myName), "admin", false,
                        "muteUntil", (double)Save.OfflineMuteUntil, "serverTime", (double)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    Out("t", "friends", "friends", new List<object>(), "incoming", new List<object>(), "outgoing", new List<object>());
                    Out("t", "party", "id", null, "leader", null, "members", new List<object>());
                    break;
                case "set_name":
                {
                    string n = m.Str("name").Trim();
                    string bad = WordFilter.CheckName(n);
                    if (bad == null && System.Array.Exists(BotNames, b => b.ToLowerInvariant() == n.ToLowerInvariant())) bad = "Someone already has that name. Try another one.";
                    if (bad != null) { Out("t", "name_result", "ok", false, "error", bad); break; }
                    myName = n;
                    Save.OfflineName = n;
                    Out("t", "name_result", "ok", true, "name", n);
                    if (players.TryGetValue(myId, out var me)) { me.name = n; Out("t", "pn", "id", myId, "name", n); }
                    break;
                }
                case "go":
                {
                    string dest = m.Str("dest");
                    if (dest == "hub") EnterHub();
                    else if (Modes.ContainsKey(dest)) EnterMatch(dest);
                    break;
                }
                case "st":
                {
                    var p = m.FloatArr("p");
                    if (p != null && p.Length >= 3 && players.TryGetValue(myId, out var me)) me.pos = new Vector3(p[0], p[1], p[2]);
                    break;
                }
                case "hit":
                    ApplyHit(myId, m.Str("target"), (float)m.Num("dmg"), m.Str("gun"), m.Bool("head"), m.FloatArr("dir"));
                    break;
                case "die":
                    if (players.TryGetValue(myId, out var meDie) && meDie.alive && kind == "match") Kill(meDie, null, "fall", false, null);
                    break;
                case "flag":
                    FlagAction(myId, m.Str("a"), m.Int("team"));
                    break;
                case "chat":
                {
                    string msg = m.Str("msg").Trim();
                    if (msg.Length == 0) break;
                    long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (Save.OfflineMuteUntil > now) { Out("t", "muted", "until", (double)Save.OfflineMuteUntil, "reason", "You are muted."); break; }
                    string bad = WordFilter.CheckChat(msg);
                    if (bad != null)
                    {
                        Save.OfflineMuteUntil = now + WordFilter.AutoMuteMinutes * 60000L;
                        Out("t", "muted", "until", (double)Save.OfflineMuteUntil, "reason", "Auto-mod: " + bad + ". You are muted for " + WordFilter.AutoMuteMinutes + " minutes.");
                        break;
                    }
                    Out("t", "chat", "id", myId, "name", myName, "msg", msg);
                    break;
                }
                case "report":
                    Out("t", "toast", "msg", "Reports go to the moderators when you play online.");
                    break;
                case "mod":
                    Out("t", "mod_result", "ok", false, "msg", "Mod tools work on the online server (in Unity: Flopzone > Set Up Multiplayer).");
                    break;
                case "friend_req":
                case "party_invite":
                    Out("t", "toast", "msg", "Friends and parties need the online server. You're playing offline.");
                    break;
                case "ping":
                    Out("t", "pong");
                    break;
            }
        }

        /* ---------------- rooms ---------------- */

        P Me => players.TryGetValue(myId, out var p) ? p : null;

        void Reset(string newKind, string newMode)
        {
            if (players.Count > 0) foreach (var id in new List<string>(players.Keys)) if (id != myId) Out("t", "pl", "id", id);
            players.Clear();
            kind = newKind;
            mode = newMode;
            roomSeq++;
            scores = new[] { 0, 0 };
            flags = newMode == "ctf" ? new[] { new Flag { team = 0 }, new Flag { team = 1 } } : null;
        }

        void EnterHub()
        {
            Reset("hub", null);
            map = -1;
            state = "hub";
            players[myId] = new P { id = myId, name = myName, team = -1 };
            SendRoom();
        }

        void EnterMatch(string m)
        {
            var md = Modes[m];
            Reset("match", m);
            map = Random.Range(0, MapBuilder.Names.Length);
            state = "countdown";
            stateEnd = Time.time + 5f;
            players[myId] = new P { id = myId, name = myName, team = md.teams ? 0 : -1, protectUntil = Time.time + 1.5f };
            var names = new List<string>(BotNames);
            for (int i = 1; i < md.max; i++)
            {
                int pick = Random.Range(0, names.Count);
                var b = new P { id = "BOT" + i, name = names[pick], bot = true, team = md.teams ? (i % 2 == 0 ? 0 : 1) : -1, protectUntil = Time.time + 1.5f };
                names.RemoveAt(pick);
                players[b.id] = b;
            }
            SendRoom();
            Out("t", "ms", "state", state, "time", 5.0, "scores", Scores(), "note", "");
        }

        List<object> Scores() => new List<object> { (double)scores[0], (double)scores[1] };

        Dictionary<string, object> PlayerPayload(P p) => Msg.Of("id", p.id, "name", p.name, "team", p.team, "hp", p.hp, "alive", p.alive, "k", p.k, "d", p.d, "bot", p.bot);

        void SendRoom()
        {
            var list = new List<object>();
            foreach (var p in players.Values) list.Add(PlayerPayload(p));
            Out("t", "room", "id", "LOCAL" + roomSeq, "kind", kind, "mode", mode, "map", map, "state", state,
                "time", (double)TimeLeft(), "scores", Scores(), "flags", FlagsPayload(), "team", Me.team, "spawn", Random.Range(0, 1000), "players", list);
        }

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

        /* ---------------- match rules (same as the server's) ---------------- */

        void Tick()
        {
            if (kind != "match") return;
            float t = Time.time;
            if (state == "countdown" && t >= stateEnd) StartMatch();
            else if (state == "live" && t >= endsAt) TimeUp();
            else if (state == "ended" && t >= stateEnd) { EnterHub(); return; }
            foreach (var p in players.Values)
                if (!p.alive && p.respawnAt > 0 && t >= p.respawnAt) Respawn(p);
            if (flags != null)
                foreach (var f in flags)
                    if (f.state == "dropped" && t - f.dropAt >= 20f) { f.state = "home"; f.pos = null; SendFlags($"The {(f.team == 1 ? "Blue" : "Red")} flag went home."); }
        }

        void StartMatch()
        {
            var md = Modes[mode];
            state = "live";
            endsAt = Time.time + md.time;
            scores = new[] { 0, 0 };
            foreach (var p in players.Values) { p.k = 0; p.d = 0; }
            if (flags != null) foreach (var f in flags) { f.state = "home"; f.carrier = null; f.pos = null; }
            Out("t", "ms", "state", "live", "time", (double)md.time, "scores", Scores(), "note", "FIGHT!");
            foreach (var p in players.Values) Respawn(p);
            if (flags != null) SendFlags("");
        }

        void Respawn(P p)
        {
            p.alive = true; p.hp = 100; p.respawnAt = 0; p.protectUntil = Time.time + 1.5f;
            Out("t", "rs", "id", p.id, "spawn", Random.Range(0, 1000));
        }

        void TimeUp()
        {
            var md = Modes[mode];
            if (md.teams) End(scores[0] == scores[1] ? -1 : (scores[0] > scores[1] ? 0 : 1), null, "Time's up!");
            else
            {
                P best = null;
                foreach (var p in players.Values) if (best == null || p.k > best.k) best = p;
                End(-1, best != null ? best.id : null, "Time's up!");
            }
        }

        void End(int winnerTeam, string winnerId, string note)
        {
            if (state == "ended") return;
            state = "ended";
            stateEnd = Time.time + 10f;
            var list = new List<object>();
            foreach (var p in players.Values) list.Add(Msg.Of("id", p.id, "name", p.name, "team", p.team, "k", p.k, "d", p.d));
            Out("t", "me", "winnerTeam", winnerTeam, "winnerId", winnerId, "note", note, "scores", Scores(), "players", list);
        }

        public void ApplyHit(string shooterId, string targetId, float dmg, string gun, bool head, float[] dir)
        {
            if (kind != "match" || state == "countdown" || state == "ended") return;
            if (!players.TryGetValue(shooterId, out var s) || !players.TryGetValue(targetId, out var t)) return;
            if (!s.alive || !t.alive || s == t) return;
            if (Modes[mode].teams && s.team == t.team) return;
            if (Time.time < t.protectUntil) return;
            dmg = Mathf.Clamp(dmg, 0, 260);
            if (dmg <= 0) return;
            s.protectUntil = 0;
            t.hp = Mathf.Max(0, Mathf.RoundToInt(t.hp - dmg));
            Out("t", "hp", "id", t.id, "hp", t.hp, "by", s.id);
            if (t.hp <= 0) Kill(t, s, gun, head, dir);
        }

        public void BotFell(string id)
        {
            if (players.TryGetValue(id, out var p) && p.alive && kind == "match") Kill(p, null, "fall", false, null);
        }

        void Kill(P victim, P killer, string gun, bool head, float[] dir)
        {
            victim.alive = false;
            victim.hp = 0;
            victim.respawnAt = Time.time + 3f;
            var md = Modes[mode];
            bool live = state == "live";
            if (live)
            {
                victim.d++;
                if (killer != null)
                {
                    killer.k++;
                    if (md.teams && mode != "ctf") scores[killer.team]++;
                }
            }
            Out("t", "kill", "victim", victim.id, "killer", killer != null ? killer.id : null, "gun", gun, "head", head,
                "dir", dir != null && dir.Length >= 3 ? new List<object> { (double)dir[0], (double)dir[1], (double)dir[2] } : null,
                "scores", Scores(), "kk", killer != null ? killer.k : 0, "vd", victim.d);
            DropFlag(victim.id);
            if (!live || killer == null || md.killLimit == 0) return;
            if (md.teams && scores[killer.team] >= md.killLimit) End(killer.team, null, "");
            else if (!md.teams && killer.k >= md.killLimit) End(-1, killer.id, "");
        }

        public void SetPos(string id, Vector3 pos)
        {
            if (players.TryGetValue(id, out var p)) p.pos = pos;
        }

        void SendFlags(string note)
        {
            Out("t", "fl", "flags", FlagsPayload(), "scores", Scores(), "note", note);
        }

        void DropFlag(string carrierId)
        {
            if (flags == null) return;
            foreach (var f in flags)
            {
                if (f.state == "carried" && f.carrier == carrierId)
                {
                    f.state = "dropped";
                    f.carrier = null;
                    f.dropAt = Time.time;
                    f.pos = players.TryGetValue(carrierId, out var p) ? p.pos : (Vector3?)null;
                    if (f.pos == null) f.state = "home";
                    SendFlags($"The {(f.team == 1 ? "Blue" : "Red")} flag was dropped!");
                }
            }
        }

        public void FlagAction(string id, string a, int team)
        {
            if (flags == null || state != "live" || !players.TryGetValue(id, out var p) || !p.alive) return;
            if (a == "touch")
            {
                if (team < 0 || team > 1) return;
                var f = flags[team];
                if (f.team != p.team && (f.state == "home" || f.state == "dropped"))
                {
                    f.state = "carried"; f.carrier = p.id; f.pos = null;
                    SendFlags($"{p.name} took the {(f.team == 1 ? "Blue" : "Red")} flag!");
                }
                else if (f.team == p.team && f.state == "dropped")
                {
                    f.state = "home"; f.pos = null;
                    SendFlags($"{p.name} returned the {(f.team == 1 ? "Blue" : "Red")} flag.");
                }
            }
            else if (a == "cap")
            {
                if (p.team < 0) return;
                var mine = flags[p.team];
                var theirs = flags[1 - p.team];
                if (theirs.carrier != p.id || mine.state != "home") return;
                theirs.state = "home"; theirs.carrier = null; theirs.pos = null;
                scores[p.team]++;
                SendFlags($"{p.name} captured the flag!");
                if (scores[p.team] >= Modes["ctf"].capLimit) End(p.team, null, "");
            }
        }

        public string FlagCarrier(int team) => flags != null && flags[team].state == "carried" ? flags[team].carrier : null;
        public string FlagState(int team) => flags != null ? flags[team].state : null;
        public Vector3? FlagPos(int team) => flags != null ? flags[team].pos : null;
    }
}
