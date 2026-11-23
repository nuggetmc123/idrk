using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Rago.Relay;

namespace RagoServer
{
    /// <summary>
    /// Moderation: bans, wipes and Market kicks.
    ///
    ///  * Every game sends a player key (random, made once per install) and a device id (hashed) when it connects. Bans
    ///    match either, so changing your callsign doesn't get around one.
    ///  * MODERATORS (picked by the owner on the web panel) get a MOD tab in the game. Anything they do is only a REQUEST:
    ///    it waits on the owner's web panel until the owner approves or denies it.
    ///  * The OWNER uses the web panel (password protected, http://127.0.0.1:27016 on the server - reach it through the
    ///    website's reverse proxy over HTTPS). There they approve requests, and can ban / unban / wipe / kick directly.
    ///  * Kept in small text files in the server's data folder (bans, mods, known players, pending wipes and requests).
    /// </summary>
    static partial class Program
    {
        // ------------------------------------------------------------------------------------------- records
        sealed class Ban { public string Key, Device, Name, Reason, By; public long Until; }   // Until: unix seconds, 0 = permanent
        sealed class Known { public string Key, Device, Name; public long Seen; }
        sealed class Request { public int Id; public byte Act; public string Key, Device, Name, Reason, By; public int Hours; public long At; }

        static readonly List<Ban> Bans = new List<Ban>();
        static readonly Dictionary<string, string> Mods = new Dictionary<string, string>();           // key -> name
        static readonly Dictionary<string, Known> Players = new Dictionary<string, Known>();          // key -> last seen
        static readonly HashSet<string> Wipes = new HashSet<string>();                               // keys to wipe on next connect
        static readonly List<Request> Pending = new List<Request>();
        static readonly List<string> History = new List<string>();
        static int _nextReq = 1;
        static string _dataDir;

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        static string ActName(byte a) => a == P.ActBan ? "BAN" : a == P.ActUnban ? "UNBAN" : a == P.ActWipe ? "WIPE" : a == P.ActKick ? "KICK (Market)" : "?";

        // ------------------------------------------------------------------------------------------- files
        static void LoadData()
        {
            _dataDir = Environment.GetEnvironmentVariable("STATE_DIRECTORY") ?? Environment.GetEnvironmentVariable("RAGO_DATA") ?? Path.Combine(AppContext.BaseDirectory, "data");
            if (_dataDir.Contains(':') && !Directory.Exists(_dataDir)) _dataDir = _dataDir.Split(':')[0];
            try { Directory.CreateDirectory(_dataDir); } catch (Exception e) { Log("can't create the data folder " + _dataDir + ": " + e.Message); }
            foreach (var f in Lines("bans.tsv")) if (f.Length >= 6) Bans.Add(new Ban { Key = f[0], Device = f[1], Name = f[2], Until = long.TryParse(f[3], out var u) ? u : 0, Reason = f[4], By = f[5] });
            foreach (var f in Lines("mods.tsv")) if (f.Length >= 2) Mods[f[0]] = f[1];
            foreach (var f in Lines("players.tsv")) if (f.Length >= 4) Players[f[0]] = new Known { Key = f[0], Device = f[1], Name = f[2], Seen = long.TryParse(f[3], out var s) ? s : 0 };
            foreach (var f in Lines("wipes.tsv")) if (f.Length >= 1 && f[0].Length > 0) Wipes.Add(f[0]);
            foreach (var f in Lines("requests.tsv"))
                if (f.Length >= 9 && int.TryParse(f[0], out var id))
                {
                    Pending.Add(new Request { Id = id, Act = byte.TryParse(f[1], out var a) ? a : (byte)0, Key = f[2], Device = f[3], Name = f[4], Hours = int.TryParse(f[5], out var h) ? h : 0, Reason = f[6], By = f[7], At = long.TryParse(f[8], out var at) ? at : 0 });
                    _nextReq = Math.Max(_nextReq, id + 1);
                }
            Log("data folder " + _dataDir + ": " + Bans.Count + " bans, " + Mods.Count + " mods, " + Players.Count + " known players, " + Pending.Count + " requests waiting");
        }

        static IEnumerable<string[]> Lines(string file)
        {
            string p = Path.Combine(_dataDir, file);
            if (!File.Exists(p)) yield break;
            string[] all; try { all = File.ReadAllLines(p); } catch { yield break; }
            foreach (var l in all) if (l.Length > 0) yield return l.Split('\t');
        }

        static void Save(string file, IEnumerable<string> lines)
        {
            try
            {
                string p = Path.Combine(_dataDir, file), tmp = p + ".tmp";
                File.WriteAllLines(tmp, lines);
                File.Move(tmp, p, true);
            }
            catch (Exception e) { Log("couldn't save " + file + ": " + e.Message); }
        }
        static void SaveBans() => Save("bans.tsv", Bans.Select(b => string.Join("\t", Clean(b.Key), Clean(b.Device), Clean(b.Name), b.Until, Clean(b.Reason), Clean(b.By))));
        static void SaveMods() => Save("mods.tsv", Mods.Select(kv => Clean(kv.Key) + "\t" + Clean(kv.Value)));
        static void SavePlayers() => Save("players.tsv", Players.Values.Select(k => string.Join("\t", Clean(k.Key), Clean(k.Device), Clean(k.Name), k.Seen)));
        static void SaveWipes() => Save("wipes.tsv", Wipes);
        static void SaveRequests() => Save("requests.tsv", Pending.Select(r => string.Join("\t", r.Id, r.Act, Clean(r.Key), Clean(r.Device), Clean(r.Name), r.Hours, Clean(r.Reason), Clean(r.By), r.At)));
        static void Note(string s) { History.Add(DateTime.UtcNow.ToString("MM-dd HH:mm") + "  " + s); while (History.Count > 60) History.RemoveAt(0); Log(s); }

        // ------------------------------------------------------------------------------------------- game side
        static Ban BanFor(string key, string device)
        {
            long now = Now;
            Bans.RemoveAll(b => b.Until != 0 && b.Until < now);
            return Bans.FirstOrDefault(b => (!string.IsNullOrEmpty(key) && b.Key == key) || (!string.IsNullOrEmpty(device) && b.Device == device));
        }

        static string BanText(Ban b) => (string.IsNullOrEmpty(b.Reason) ? "no reason given" : b.Reason) + (b.Until == 0 ? " (permanent)" : " (until " + DateTimeOffset.FromUnixTimeSeconds(b.Until).UtcDateTime.ToString("yyyy-MM-dd HH:mm") + " UTC)");

        /// <summary>A player just said hello (under the lock): banned? a moderator? owed a wipe? Returns false if dropped.</summary>
        static bool OnHello(Client c)
        {
            var ban = BanFor(c.Key, c.Device);
            if (ban != null)
            {
                Send(c, new W(P.Banned).Str(BanText(ban)).Frame());
                c.Refused = true;
                c.Out.Writer.TryComplete();   // the writer sends that, then the connection closes
                Log(c + " is banned - refused");
                return false;
            }
            if (!string.IsNullOrEmpty(c.Key))
            {
                Players[c.Key] = new Known { Key = c.Key, Device = c.Device, Name = c.Name, Seen = Now };
                _playersDirty = true;
                if (Mods.ContainsKey(c.Key)) { Mods[c.Key] = c.Name; Send(c, new W(P.Role).U8(1).Frame()); }
                if (Wipes.Remove(c.Key)) { Send(c, new W(P.Wipe).Frame()); SaveWipes(); Note("wiped " + c.Name + " (on connect)"); }
            }
            return true;
        }
        static bool _playersDirty;

        /// <summary>Callsigns matching what's typed so far: online players first, then everyone the server has seen.</summary>
        static List<string> SearchNames(string q, int max)
        {
            q = (q ?? "").Trim().ToLowerInvariant();
            var res = new List<string>();
            if (q.Length == 0) return res;
            int Score(string n) { var l = n.ToLowerInvariant(); return l.StartsWith(q) ? 0 : l.Contains(q) ? 1 : 9; }
            foreach (var n in Clients.Values.Select(x => x.Name).Where(n => n != "?" && Score(n) < 9).OrderBy(Score).ThenBy(n => n.Length))
                if (!res.Contains(n)) { res.Add(n); if (res.Count >= max) return res; }
            foreach (var n in Players.Values.OrderByDescending(k => k.Seen).Select(k => k.Name).Where(n => Score(n) < 9).OrderBy(Score))
                if (!res.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase))) { res.Add(n); if (res.Count >= max) return res; }
            return res;
        }

        static Client OnlineByName(string name) => Clients.Values.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        static Known KnownByName(string name) => Players.Values.Where(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(k => k.Seen).FirstOrDefault();
        static bool InMarket(Client c) => c.Room != null && c.Room.Name.StartsWith("RagoMarket", StringComparison.Ordinal);

        /// <summary>A moderator's request from the MOD tab (under the lock): queued for the owner.</summary>
        static void OnModRequest(Client c, byte act, string target, string reason, int hours)
        {
            if (string.IsNullOrEmpty(c.Key) || !Mods.ContainsKey(c.Key)) { Send(c, new W(P.ModReply).Str("You're not a moderator.").Frame()); return; }
            target = (target ?? "").Trim();
            var on = OnlineByName(target); var known = on == null ? KnownByName(target) : null;
            string key = on != null ? on.Key : known?.Key, device = on != null ? on.Device : known?.Device, name = on != null ? on.Name : known?.Name;
            if (act == P.ActUnban && key == null)
            {
                var b = Bans.FirstOrDefault(x => string.Equals(x.Name, target, StringComparison.OrdinalIgnoreCase));
                if (b != null) { key = b.Key; device = b.Device; name = b.Name; }
            }
            if (key == null) { Send(c, new W(P.ModReply).Str("Nobody called " + target + " has played on this server.").Frame()); return; }
            if (key == c.Key) { Send(c, new W(P.ModReply).Str("You can't do that to yourself.").Frame()); return; }
            if (act == P.ActKick && (on == null || !InMarket(on))) { Send(c, new W(P.ModReply).Str(name + " isn't in the Market right now (kicks are for the Market only).").Frame()); return; }
            if (act < P.ActBan || act > P.ActKick) return;
            var r = new Request { Id = _nextReq++, Act = act, Key = key, Device = device, Name = name, Reason = Clean(reason), Hours = Math.Max(0, hours), By = c.Name, At = Now };
            Pending.Add(r); SaveRequests();
            Note("request #" + r.Id + ": " + c.Name + " wants " + ActName(act) + " " + name + (act == P.ActBan ? " (" + (hours == 0 ? "permanent" : hours + " h") + ")" : "") + " - " + r.Reason);
            Send(c, new W(P.ModReply).Str("Sent to the owner for approval: " + ActName(act) + " " + name + " (request #" + r.Id + ").").Frame());
        }

        // ------------------------------------------------------------------------------------------- actions (owner)
        static string DoBan(string key, string device, string name, int hours, string reason, string by)
        {
            Bans.RemoveAll(b => b.Key == key);
            var ban = new Ban { Key = key, Device = device, Name = name, Until = hours <= 0 ? 0 : Now + hours * 3600L, Reason = Clean(reason), By = by };
            Bans.Add(ban); SaveBans();
            foreach (var c in Clients.Values.Where(x => x.Key == key || (!string.IsNullOrEmpty(device) && x.Device == device)).ToList())
            { Send(c, new W(P.Banned).Str(BanText(ban)).Frame()); LeaveRoom(c); c.Refused = true; c.Out.Writer.TryComplete(); }
            Note("BANNED " + name + " " + (hours <= 0 ? "permanently" : "for " + hours + " h") + " - " + ban.Reason + " (by " + by + ")");
            return "Banned " + name;
        }
        static string DoUnban(string key, string name, string by)
        {
            int n = Bans.RemoveAll(b => b.Key == key);
            SaveBans(); Note("unbanned " + name + " (by " + by + ")");
            return n > 0 ? "Unbanned " + name : name + " wasn't banned";
        }
        static string DoWipe(string key, string name, string by)
        {
            var on = Clients.Values.FirstOrDefault(x => x.Key == key);
            if (on != null) Send(on, new W(P.Wipe).Frame()); else { Wipes.Add(key); SaveWipes(); }
            Note("WIPED " + name + (on != null ? "" : " (when they next connect)") + " (by " + by + ")");
            return "Wiped " + name + (on != null ? "" : " - happens when they next connect");
        }
        static string DoKick(string key, string name, string reason, string by)
        {
            var on = Clients.Values.FirstOrDefault(x => x.Key == key);
            if (on == null || !InMarket(on)) return name + " isn't in the Market";
            LeaveRoom(on);
            Send(on, new W(P.Kicked).Str(string.IsNullOrEmpty(reason) ? "kicked from the Market" : reason).Frame());
            Note("kicked " + name + " from the Market (by " + by + ")");
            return "Kicked " + name + " from the Market";
        }
        static string Apply(Request r, string by) =>
            r.Act == P.ActBan ? DoBan(r.Key, r.Device, r.Name, r.Hours, r.Reason, r.By + ", approved by " + by)
            : r.Act == P.ActUnban ? DoUnban(r.Key, r.Name, r.By + ", approved by " + by)
            : r.Act == P.ActWipe ? DoWipe(r.Key, r.Name, r.By + ", approved by " + by)
            : DoKick(r.Key, r.Name, r.Reason, r.By + ", approved by " + by);

        static void TellMod(string modName, string text)
        {
            var m = OnlineByName(modName);
            if (m != null) Send(m, new W(P.ModReply).Str(text).Frame());
        }

        // ------------------------------------------------------------------------------------------- web panel
        static string _password;
        static readonly Dictionary<string, DateTime> Sessions = new Dictionary<string, DateTime>();
        static int _badLogins; static DateTime _badT;

        static void StartPanel()
        {
            string bind = Environment.GetEnvironmentVariable("RAGO_PANEL") ?? "http://127.0.0.1:27016/";
            _password = Environment.GetEnvironmentVariable("RAGO_ADMIN_PASSWORD");
            string pwFile = Path.Combine(_dataDir, "admin_password.txt");
            if (string.IsNullOrEmpty(_password))
            {
                try { if (File.Exists(pwFile)) _password = File.ReadAllText(pwFile).Trim(); } catch { }
                if (string.IsNullOrEmpty(_password))
                {
                    _password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('/', 'x').Replace('+', 'y').TrimEnd('=');
                    try { File.WriteAllText(pwFile, _password); } catch { }
                    Log("ADMIN PANEL PASSWORD (first run): " + _password + "   (saved in " + pwFile + ")");
                }
            }
            var http = new HttpListener();
            try { http.Prefixes.Add(bind); http.Start(); }
            catch (Exception e) { Log("admin panel couldn't start on " + bind + ": " + e.Message); return; }
            Log("admin panel on " + bind);
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    HttpListenerContext ctx;
                    try { ctx = await http.GetContextAsync(); } catch { continue; }
                    _ = Task.Run(() => { try { Serve(ctx); } catch (Exception e) { Log("panel: " + e.Message); try { ctx.Response.Abort(); } catch { } } });
                }
            });
        }

        static string H(string s) => WebUtility.HtmlEncode(s ?? "");

        static Dictionary<string, string> Form(HttpListenerRequest rq)
        {
            var d = new Dictionary<string, string>();
            string body = "";
            if (rq.HasEntityBody) using (var sr = new StreamReader(rq.InputStream, Encoding.UTF8)) body = sr.ReadToEnd();
            foreach (var part in body.Split('&'))
            {
                int e = part.IndexOf('='); if (e <= 0) continue;
                d[WebUtility.UrlDecode(part.Substring(0, e))] = WebUtility.UrlDecode(part.Substring(e + 1));
            }
            return d;
        }

        static string SessionOf(HttpListenerRequest rq)
        {
            var c = rq.Cookies["rs"];
            if (c == null) return null;
            lock (Lock) { if (Sessions.TryGetValue(c.Value, out var t) && DateTime.UtcNow - t < TimeSpan.FromHours(12)) return c.Value; }
            return null;
        }

        static void Reply(HttpListenerContext ctx, string html, int code = 200)
        {
            var b = Encoding.UTF8.GetBytes(html);
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'";
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.Close();
        }
        static void Redirect(HttpListenerContext ctx, string msg = null)
        {
            ctx.Response.StatusCode = 303;
            ctx.Response.Headers["Location"] = msg == null ? "./" : "./?m=" + WebUtility.UrlEncode(msg);
            ctx.Response.Close();
        }

        static void Serve(HttpListenerContext ctx)
        {
            var rq = ctx.Request;
            string path = rq.Url.AbsolutePath.TrimEnd('/');
            string sess = SessionOf(rq);
            if (rq.HttpMethod == "POST" && path.EndsWith("/login"))
            {
                var f = Form(rq);
                bool ok;
                lock (Lock)
                {
                    if (_badLogins >= 5 && DateTime.UtcNow - _badT < TimeSpan.FromMinutes(5)) ok = false;
                    else ok = f.TryGetValue("pw", out var pw) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pw), Encoding.UTF8.GetBytes(_password));
                    if (!ok) { _badLogins++; _badT = DateTime.UtcNow; }
                    else
                    {
                        _badLogins = 0;
                        sess = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                        Sessions[sess] = DateTime.UtcNow;
                    }
                }
                if (!ok) { Task.Delay(1000).Wait(); Reply(ctx, Page("Login", LoginForm("Wrong password (5 wrong tries locks it for 5 minutes)."))); return; }
                ctx.Response.Headers.Add("Set-Cookie", "rs=" + sess + "; Path=/; HttpOnly; SameSite=Strict; Max-Age=43200" + (rq.Headers["X-Forwarded-Proto"] == "https" ? "; Secure" : ""));
                Redirect(ctx);
                return;
            }
            if (sess == null) { Reply(ctx, Page("Login", LoginForm(null))); return; }
            if (rq.HttpMethod == "POST" && path.EndsWith("/act"))
            {
                var f = Form(rq);
                if (!f.TryGetValue("t", out var tok) || tok != sess) { Reply(ctx, "bad request", 400); return; }
                string msg;
                lock (Lock) msg = Act(f);
                Redirect(ctx, msg);
                return;
            }
            if (path.EndsWith("/logout")) { lock (Lock) Sessions.Remove(sess); Redirect(ctx); return; }
            string m = rq.QueryString["m"], q = rq.QueryString["q"];
            string html;
            lock (Lock) html = Dashboard(sess, m, q);
            Reply(ctx, html);
        }

        static string Act(Dictionary<string, string> f)
        {
            string G(string k) => f.TryGetValue(k, out var v) ? v : "";
            string op = G("op"), key = G("key"), reason = G("reason");
            int.TryParse(G("hours"), out int hours);
            Known K() => Players.TryGetValue(key, out var k) ? k : null;
            switch (op)
            {
                case "approve":
                case "deny":
                {
                    if (!int.TryParse(G("id"), out int id)) return "?";
                    var r = Pending.FirstOrDefault(x => x.Id == id);
                    if (r == null) return "That request is gone.";
                    Pending.Remove(r); SaveRequests();
                    if (op == "deny") { Note("denied request #" + id + " (" + ActName(r.Act) + " " + r.Name + " by " + r.By + ")"); TellMod(r.By, "The owner DENIED: " + ActName(r.Act) + " " + r.Name); return "Denied #" + id; }
                    string res = Apply(r, "owner");
                    TellMod(r.By, "The owner APPROVED: " + ActName(r.Act) + " " + r.Name);
                    return res;
                }
                case "ban": { var k = K(); if (k == null) return "Unknown player"; return DoBan(k.Key, k.Device, k.Name, hours, reason, "owner"); }
                case "unban": { var b = Bans.FirstOrDefault(x => x.Key == key); return b == null ? "Not banned" : DoUnban(b.Key, b.Name, "owner"); }
                case "wipe": { var k = K(); if (k == null) return "Unknown player"; return DoWipe(k.Key, k.Name, "owner"); }
                case "kick": { var k = K(); if (k == null) return "Unknown player"; return DoKick(k.Key, k.Name, reason, "owner"); }
                case "mod":
                {
                    var k = K(); if (k == null) return "Unknown player";
                    Mods[k.Key] = k.Name; SaveMods();
                    var on = Clients.Values.FirstOrDefault(x => x.Key == k.Key); if (on != null) Send(on, new W(P.Role).U8(1).Frame());
                    Note(k.Name + " is now a moderator");
                    return k.Name + " is now a moderator";
                }
                case "unmod":
                {
                    if (!Mods.TryGetValue(key, out var nm)) return "Not a moderator";
                    Mods.Remove(key); SaveMods();
                    var on = Clients.Values.FirstOrDefault(x => x.Key == key); if (on != null) Send(on, new W(P.Role).U8(0).Frame());
                    Note(nm + " is no longer a moderator");
                    return nm + " is no longer a moderator";
                }
            }
            return "?";
        }

        static string LoginForm(string err) =>
            "<h1>Rago Extract - owner panel</h1>" + (err != null ? "<p class=err>" + H(err) + "</p>" : "") +
            "<form method=post action='login'><input type=password name=pw placeholder='Admin password' autofocus> <button>Log in</button></form>";

        static string Btn(string sess, string op, string key, string label, string extra = "", string cls = "") =>
            "<form method=post action='act' class=inl><input type=hidden name=t value='" + sess + "'><input type=hidden name=op value='" + op + "'><input type=hidden name=key value='" + H(key) + "'>" + extra +
            "<button class='" + cls + "'>" + H(label) + "</button></form>";

        static string BanForm(string sess, string key) =>
            "<form method=post action='act' class=inl><input type=hidden name=t value='" + sess + "'><input type=hidden name=op value='ban'><input type=hidden name=key value='" + H(key) + "'>" +
            "<select name=hours><option value=24>1 day</option><option value=168>7 days</option><option value=720>30 days</option><option value=0>permanent</option></select> " +
            "<input name=reason placeholder='reason' size=14> <button class=red>Ban</button></form>";

        static string Dashboard(string sess, string msg, string q)
        {
            var sb = new StringBuilder();
            sb.Append("<h1>Rago Extract - owner panel</h1><p class=dim>").Append(Clients.Count).Append(" online &middot; ").Append(Rooms.Count).Append(" rooms &middot; <a href='logout'>log out</a></p>");
            if (!string.IsNullOrEmpty(msg)) sb.Append("<p class=ok>").Append(H(msg)).Append("</p>");
            // requests
            sb.Append("<h2>Waiting for your approval (").Append(Pending.Count).Append(")</h2>");
            if (Pending.Count == 0) sb.Append("<p class=dim>Nothing waiting.</p>");
            else
            {
                sb.Append("<table><tr><th>#</th><th>From mod</th><th>Wants to</th><th>Player</th><th>Details</th><th></th></tr>");
                foreach (var r in Pending)
                    sb.Append("<tr><td>").Append(r.Id).Append("</td><td>").Append(H(r.By)).Append("</td><td><b>").Append(ActName(r.Act)).Append("</b></td><td>").Append(H(r.Name))
                      .Append("</td><td>").Append(r.Act == P.ActBan ? (r.Hours == 0 ? "permanent" : r.Hours >= 24 ? r.Hours / 24 + " day(s)" : r.Hours + " h") + " - " : "").Append(H(r.Reason))
                      .Append("<br><span class=dim>").Append(DateTimeOffset.FromUnixTimeSeconds(r.At).UtcDateTime.ToString("MM-dd HH:mm")).Append(" UTC</span></td><td>")
                      .Append(Btn(sess, "approve", "", "Approve", "<input type=hidden name=id value=" + r.Id + ">", "green")).Append(Btn(sess, "deny", "", "Deny", "<input type=hidden name=id value=" + r.Id + ">")).Append("</td></tr>");
                sb.Append("</table>");
            }
            // players: online, or search everyone seen
            sb.Append("<h2>Players</h2><form method=get class=inl><input name=q value='").Append(H(q)).Append("' placeholder='search a callsign (e.g. nug)' size=26> <button>Search</button></form>");
            IEnumerable<Known> list;
            if (!string.IsNullOrWhiteSpace(q)) { var names = SearchNames(q, 30); list = names.Select(n => OnlineByName(n) is Client oc && !string.IsNullOrEmpty(oc.Key) && Players.TryGetValue(oc.Key, out var ok) ? ok : KnownByName(n)).Where(k => k != null).Distinct(); }
            else list = Clients.Values.Where(c => !string.IsNullOrEmpty(c.Key) && Players.ContainsKey(c.Key)).Select(c => Players[c.Key]);
            sb.Append("<table><tr><th>Callsign</th><th>Where</th><th>Last seen</th><th>Actions</th></tr>");
            int rows = 0;
            foreach (var k in list)
            {
                rows++;
                var on = Clients.Values.FirstOrDefault(c => c.Key == k.Key);
                bool banned = Bans.Any(b => b.Key == k.Key), mod = Mods.ContainsKey(k.Key);
                sb.Append("<tr><td><b>").Append(H(k.Name)).Append("</b>").Append(mod ? " <span class=tag>MOD</span>" : "").Append(banned ? " <span class='tag red'>BANNED</span>" : "")
                  .Append("</td><td>").Append(on == null ? "offline" : on.Room == null ? "bunker" : InMarket(on) ? "Market" : on.Room.Name.Contains("_q_") ? "raid" : on.Room.Name.Contains("party") ? "party" : "online")
                  .Append("</td><td>").Append(on != null ? "now" : DateTimeOffset.FromUnixTimeSeconds(k.Seen).UtcDateTime.ToString("MM-dd HH:mm")).Append("</td><td>");
                if (banned) sb.Append(Btn(sess, "unban", k.Key, "Unban")); else sb.Append(BanForm(sess, k.Key));
                sb.Append(Btn(sess, "wipe", k.Key, "Wipe", "", "orange"));
                if (on != null && InMarket(on)) sb.Append(Btn(sess, "kick", k.Key, "Kick from Market"));
                sb.Append(mod ? Btn(sess, "unmod", k.Key, "Remove mod") : Btn(sess, "mod", k.Key, "Make mod"));
                sb.Append("</td></tr>");
            }
            if (rows == 0) sb.Append("<tr><td colspan=4 class=dim>").Append(string.IsNullOrWhiteSpace(q) ? "Nobody online right now - search to find anyone who's played." : "No callsign matches.").Append("</td></tr>");
            sb.Append("</table>");
            // bans
            sb.Append("<h2>Bans (").Append(Bans.Count).Append(")</h2>");
            if (Bans.Count == 0) sb.Append("<p class=dim>No bans.</p>");
            else
            {
                sb.Append("<table><tr><th>Callsign</th><th>Until</th><th>Reason</th><th>By</th><th></th></tr>");
                foreach (var b in Bans)
                    sb.Append("<tr><td>").Append(H(b.Name)).Append("</td><td>").Append(b.Until == 0 ? "permanent" : DateTimeOffset.FromUnixTimeSeconds(b.Until).UtcDateTime.ToString("yyyy-MM-dd HH:mm")).Append("</td><td>")
                      .Append(H(b.Reason)).Append("</td><td>").Append(H(b.By)).Append("</td><td>").Append(Btn(sess, "unban", b.Key, "Unban")).Append("</td></tr>");
                sb.Append("</table>");
            }
            // mods + history
            sb.Append("<h2>Moderators (").Append(Mods.Count).Append(")</h2><p>").Append(Mods.Count == 0 ? "<span class=dim>None yet - press Make mod next to a player.</span>" : string.Join(", ", Mods.Values.Select(H))).Append("</p>");
            sb.Append("<h2>Recent actions</h2><pre>").Append(H(string.Join("\n", Enumerable.Reverse(History)))).Append("</pre>");
            return Page("Owner panel", sb.ToString());
        }

        static string Page(string title, string body) =>
            "<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>" + H(title) + "</title><style>" +
            "body{font:15px system-ui,sans-serif;background:#16181b;color:#e8e6e1;margin:0 auto;padding:16px;max-width:1000px}h1{font-size:22px;margin:4px 0}h2{font-size:17px;margin:22px 0 8px;color:#f0b44c}" +
            "table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:6px 8px;border-bottom:1px solid #2c2f34;vertical-align:top}th{color:#9a9ea5;font-weight:600}" +
            "input,select,button{font:inherit;padding:5px 8px;border-radius:6px;border:1px solid #3a3e44;background:#22252a;color:#e8e6e1}button{cursor:pointer;background:#30343a}" +
            "button.green{background:#2d6a3a;border-color:#3d8a4c}button.red{background:#7a2a24;border-color:#9a3a32}button.orange{background:#7a5320;border-color:#9a6a2a}" +
            ".inl{display:inline-block;margin:2px 4px 2px 0}.dim{color:#8a8e95}.ok{background:#1f3a26;padding:8px 10px;border-radius:6px}.err{color:#ff8a7a}" +
            ".tag{font-size:11px;background:#3a5a8a;padding:1px 6px;border-radius:4px}.tag.red{background:#8a2a24}pre{background:#1e2024;padding:10px;border-radius:6px;white-space:pre-wrap;font-size:13px}a{color:#7ab4ff}" +
            "@media(max-width:640px){td,th{padding:5px 4px;font-size:13px}}" +
            "</style></head><body>" + body + "</body></html>";
    }
}
