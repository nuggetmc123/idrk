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
            LoadEvent();
            LoadMinVer();
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
            // (3.58) too old a game version: refused, with a message to update. 3.58+ lock themselves (Outdated); older
            // games only understand "banned", so they get that, worded as an update message
            int v = VerNum(c.Ver);
            if (v < MinVer && c.Ver != "check")
            {
                string need = VerText(MinVer);
                if (v >= 3058) Send(c, new W(P.Outdated).Str(need).Frame());
                else Send(c, new W(P.Banned).Str("this version (" + (v > 0 ? VerText(v) : "old") + ") no longer works - install the latest Rago Extract (" + need + " or newer)").Frame());
                c.Refused = true;
                c.Out.Writer.TryComplete();
                Log(c + " on old version " + c.Ver + " - refused (needs " + need + ")");
                return false;
            }
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
                DeliverSales(c);   // black market sales made while they were away
                DeliverGifts(c);   // things the owner gave them while they were away
                DeliverRename(c);  // a new callsign the owner gave them while they were away
                CheckName(c);      // (3.58) a callsign that isn't allowed: renamed OPERATOR (their #tag stays)
            }
            return true;
        }
        static bool _playersDirty;

        // ---- (3.58) the oldest game version the server takes (owner panel; kept in minversion.txt)
        public const int DefaultMinVer = 3058;
        static int MinVer = DefaultMinVer;
        /// <summary>"_v3_58" / "3.58" -> 3058 (0 if it can't be read: a very old game).</summary>
        static int VerNum(string v)
        {
            v = (v ?? "").Trim().Replace("_v", "").Replace('_', '.').TrimStart('v', 'V');
            var p = v.Split('.');
            if (p.Length < 2 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b)) return 0;
            return a * 1000 + b;
        }
        static string VerText(int n) => n / 1000 + "." + (n % 1000).ToString("00");
        static void LoadMinVer()
        {
            foreach (var f in Lines("minversion.txt")) { int n = VerNum(f[0]); if (n > 0) MinVer = n; }   // the owner's choice (may be lower, to let an older version back in)
            Log("oldest game version allowed: " + VerText(MinVer));
        }
        static string SetMinVer(string text)
        {
            int n = VerNum(text);
            if (n <= 0) return "Type a version like 3.58";
            MinVer = n; Save("minversion.txt", new[] { VerText(n) });
            int kicked = 0;
            foreach (var c in Clients.Values.Where(x => !x.Refused && x.Name != "?" && x.Ver != "check" && VerNum(x.Ver) < n).ToList())
            {
                string need = VerText(n);
                if (VerNum(c.Ver) >= 3058) Send(c, new W(P.Outdated).Str(need).Frame());
                else Send(c, new W(P.Banned).Str("this version no longer works - install the latest Rago Extract (" + need + " or newer)").Frame());
                LeaveRoom(c); c.Refused = true; c.Out.Writer.TryComplete(); kicked++;
            }
            Note("oldest game version allowed is now " + VerText(n) + (kicked > 0 ? " (" + kicked + " on older versions disconnected)" : ""));
            return "Only " + VerText(n) + " and newer can play now" + (kicked > 0 ? " - " + kicked + " player(s) on older versions were disconnected" : "");
        }

        /// <summary>(3.58) Slurs, swearing and sexual callsigns (Rago.Relay.NameFilter): the player is renamed OPERATOR - or
        /// OPERATOR2, 3... if their #tag is already taken with it - here and in their game.</summary>
        static void CheckName(Client c)
        {
            if (!NameFilter.Bad(c.Name) || !Players.ContainsKey(c.Key)) return;
            string was = c.Name;
            for (int i = 1; i < 100; i++)
            {
                RenamePlayer(c.Key, i == 1 ? "OPERATOR" : "OPERATOR" + i);
                if (!NameFilter.Bad(Players[c.Key].Name)) break;
            }
            Log("callsign not allowed - renamed: " + was + " -> " + Players[c.Key].Name);
        }

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
            CloudDelete(key);   // (3.73) their cloud save goes too, so the wipe can't be undone by the cloud restoring it
            if (ShopsByKey.Remove(key)) SaveShops();   // their black market shop goes too
            if (Sales.RemoveAll(x => x.SellerKey == key) > 0) SaveSales();
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
        // A tiny web server of our own (no HttpListener): it answers on the GAME port too - a browser connecting to
        // http://<server>:27015/ gets the panel (the game's own messages never start with "GET " / "POST"), so it works
        // with no extra firewall rule. It also listens on 127.0.0.1:27016 for an HTTPS reverse proxy.
        static string _password;
        static readonly Dictionary<string, DateTime> Sessions = new Dictionary<string, DateTime>();
        static int _badLogins; static DateTime _badT;

        sealed class Req
        {
            public string Method = "GET", Path = "/", Query = "", Body = "";
            public readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string Header(string k) => Headers.TryGetValue(k, out var v) ? v : null;
            public string Cookie(string name)
            {
                var c = Header("Cookie"); if (c == null) return null;
                foreach (var part in c.Split(';')) { var p = part.Trim(); int e = p.IndexOf('='); if (e > 0 && p.Substring(0, e) == name) return p.Substring(e + 1); }
                return null;
            }
            public string Q(string k) { var d = ParseForm(Query); return d.TryGetValue(k, out var v) ? v : null; }
        }
        sealed class Resp { public int Code = 200; public string Html = "", Location, SetCookie, Type; public byte[] Bin; }

        static bool LooksLikeHttp(byte[] h) => (h[0] == 'G' && h[1] == 'E' && h[2] == 'T' && h[3] == ' ') || (h[0] == 'P' && h[1] == 'O' && h[2] == 'S' && h[3] == 'T') || (h[0] == 'H' && h[1] == 'E' && h[2] == 'A' && h[3] == 'D');

        static void StartPanel()
        {
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
            // the local listener for a reverse proxy (optional)
            string bind = Environment.GetEnvironmentVariable("RAGO_PANEL") ?? "http://127.0.0.1:27016/";
            try
            {
                var u = new Uri(bind);
                var ip = u.Host == "*" || u.Host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(u.Host == "localhost" ? "127.0.0.1" : u.Host);
                var l = new System.Net.Sockets.TcpListener(ip, u.Port); l.Start();
                Log("admin panel on " + bind + " and on the game port (http://<server>:<game port>/)");
                _ = Task.Run(async () => { while (true) { try { var t = await l.AcceptTcpClientAsync(); _ = Task.Run(() => HttpConn(t.GetStream(), null, t)); } catch { } } });
            }
            catch (Exception e) { Log("admin panel: local listener " + bind + " not started (" + e.Message + ") - the game port still serves it"); }
        }

        /// <summary>One browser request (head = its first 4 bytes, already read off the game port, or null).</summary>
        static async Task HttpConn(Stream s, byte[] head, System.Net.Sockets.TcpClient tcp)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(10000);
                var buf = new MemoryStream();
                if (head != null) buf.Write(head, 0, head.Length);
                var one = new byte[4096];
                int headerEnd = -1;
                while (headerEnd < 0)
                {
                    if (buf.Length > 16384) return;
                    int k = await s.ReadAsync(one, 0, one.Length, cts.Token);
                    if (k <= 0) return;
                    buf.Write(one, 0, k);
                    headerEnd = IndexOf(buf.GetBuffer(), (int)buf.Length, "\r\n\r\n");
                }
                var all = buf.ToArray();
                string headText = Encoding.ASCII.GetString(all, 0, headerEnd);
                var lines = headText.Split("\r\n");
                var first = lines[0].Split(' ');
                if (first.Length < 2) return;
                var rq = new Req { Method = first[0] };
                string target = first[1]; int qm = target.IndexOf('?');
                rq.Path = qm >= 0 ? target.Substring(0, qm) : target; rq.Query = qm >= 0 ? target.Substring(qm + 1) : "";
                for (int i = 1; i < lines.Length; i++) { int c = lines[i].IndexOf(':'); if (c > 0) rq.Headers[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim(); }
                int len = int.TryParse(rq.Header("Content-Length"), out var cl) ? Math.Clamp(cl, 0, 65536) : 0;
                var body = new MemoryStream(); int have = all.Length - headerEnd - 4;
                if (have > 0) body.Write(all, headerEnd + 4, Math.Min(have, len));
                while (body.Length < len) { int k = await s.ReadAsync(one, 0, Math.Min(one.Length, len - (int)body.Length), cts.Token); if (k <= 0) break; body.Write(one, 0, k); }
                rq.Body = Encoding.UTF8.GetString(body.ToArray());
                Resp rs;
                try { rs = Handle(rq); } catch (Exception e) { Log("panel: " + e.Message); rs = new Resp { Code = 500, Html = "error" }; }
                var sb = new StringBuilder();
                sb.Append("HTTP/1.1 ").Append(rs.Code).Append(rs.Code == 200 ? " OK" : rs.Code == 303 ? " See Other" : rs.Code == 400 ? " Bad Request" : " Error").Append("\r\n");
                var bytes = rs.Bin ?? Encoding.UTF8.GetBytes(rs.Html ?? "");
                sb.Append("Content-Type: ").Append(rs.Type ?? "text/html; charset=utf-8").Append("\r\nCache-Control: no-store\r\nX-Frame-Options: DENY\r\nReferrer-Policy: no-referrer\r\n");
                sb.Append("Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; img-src 'self'; form-action 'self'").Append("\r\n");
                if (rs.Location != null) sb.Append("Location: ").Append(rs.Location).Append("\r\n");
                if (rs.SetCookie != null) sb.Append("Set-Cookie: ").Append(rs.SetCookie).Append("\r\n");
                sb.Append("Content-Length: ").Append(bytes.Length).Append("\r\nConnection: close\r\n\r\n");
                var hb = Encoding.ASCII.GetBytes(sb.ToString());
                await s.WriteAsync(hb, 0, hb.Length);
                if (rq.Method != "HEAD") await s.WriteAsync(bytes, 0, bytes.Length);
                await s.FlushAsync();
            }
            catch (Exception) { }
            finally { try { tcp?.Close(); } catch { } }
        }

        static int IndexOf(byte[] b, int n, string pat)
        {
            for (int i = 0; i + pat.Length <= n; i++)
            {
                int j = 0; while (j < pat.Length && b[i + j] == pat[j]) j++;
                if (j == pat.Length) return i;
            }
            return -1;
        }

        static string H(string s) => WebUtility.HtmlEncode(s ?? "");

        static Dictionary<string, string> ParseForm(string body)
        {
            var d = new Dictionary<string, string>();
            foreach (var part in (body ?? "").Split('&'))
            {
                int e = part.IndexOf('='); if (e <= 0) continue;
                d[WebUtility.UrlDecode(part.Substring(0, e))] = WebUtility.UrlDecode(part.Substring(e + 1));
            }
            return d;
        }

        static Resp Handle(Req rq)
        {
            string path = rq.Path.TrimEnd('/');
            // public pages (no login): the privacy policy (for the store listing)
            if (rq.Method != "POST" && (path.EndsWith("/privacy") || path.EndsWith("/privacy.html") || path.EndsWith("/privacy-policy"))) return new Resp { Html = PrivacyPage() };
            string sess = rq.Cookie("rs");
            lock (Lock) { if (sess == null || !Sessions.TryGetValue(sess, out var t) || DateTime.UtcNow - t > TimeSpan.FromHours(12)) sess = null; }
            Resp Go(string msg = null) => new Resp { Code = 303, Location = msg == null ? "./" : "./?m=" + WebUtility.UrlEncode(msg) };
            if (rq.Method == "POST" && path.EndsWith("/login"))
            {
                var f = ParseForm(rq.Body);
                bool ok; string newSess = null;
                lock (Lock)
                {
                    if (_badLogins >= 5 && DateTime.UtcNow - _badT < TimeSpan.FromMinutes(5)) ok = false;
                    else ok = f.TryGetValue("pw", out var pw) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pw), Encoding.UTF8.GetBytes(_password));
                    if (!ok) { _badLogins++; _badT = DateTime.UtcNow; }
                    else { _badLogins = 0; newSess = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)); Sessions[newSess] = DateTime.UtcNow; }
                }
                if (!ok) { Task.Delay(1000).Wait(); return new Resp { Html = Page("Login", LoginForm("Wrong password (5 wrong tries locks it for 5 minutes).")) }; }
                var r = Go(); r.Location = "./";
                r.SetCookie = "rs=" + newSess + "; Path=/; HttpOnly; SameSite=Strict; Max-Age=43200" + (rq.Header("X-Forwarded-Proto") == "https" ? "; Secure" : "");
                return r;
            }
            if (sess == null) return new Resp { Html = Page("Login", LoginForm(null)) };
            if (rq.Method == "POST" && path.EndsWith("/act"))
            {
                var f = ParseForm(rq.Body);
                if (!f.TryGetValue("t", out var tok) || tok != sess) return new Resp { Code = 400, Html = "bad request" };
                string msg; lock (Lock) msg = Act(f);
                return Go(msg);
            }
            if (path.EndsWith("/logout")) { lock (Lock) Sessions.Remove(sess); return Go(); }
            string html; lock (Lock) html = Dashboard(sess, rq.Q("m"), rq.Q("q"));
            return new Resp { Html = html };
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
                case "betaskin": { var k = K(); if (k == null) return "Unknown player"; return GiveItem(k.Key, k.Name, "skin_beta", "Beta Tester skin", "from the owner - thanks for testing!"); }
                case "rename": return RenamePlayer(key, G("name"));
                case "cc":
                {
                    var k = K(); if (k == null) return "Unknown player";
                    string what = G("what");
                    string a = what == "skin" ? "" : GiveItem(k.Key, k.Name, "cc_trophy", "CC Trophy", "from the owner - thanks for the videos!");
                    string b = what == "trophy" ? "" : GiveItem(k.Key, k.Name, "skin_cc", "CC skin", "from the owner - thanks for the videos!");
                    return a.Length > 0 && b.Length > 0 ? a + " and the CC skin" : a + b;
                }
                case "trophy": { var k = K(); if (k == null) return "Unknown player"; return GiveItem(k.Key, k.Name, "cc_trophy", "CC Trophy", "from the owner - thanks for the videos!"); }
                case "event":
                {
                    byte.TryParse(G("kind"), out byte kind); int.TryParse(G("minutes"), out int mins);
                    return StartEvent(kind, mins, "owner");
                }
                case "stopevent": return StartEvent(0, 0, "owner");
                case "minver": return SetMinVer(G("ver"));
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
            // (3.58) moderators up top
            sb.Append("<h2>Moderators (").Append(Mods.Count).Append(")</h2><p>").Append(Mods.Count == 0 ? "<span class=dim>None yet - press Make mod next to a player.</span>" : string.Join(", ", Mods.Values.Select(H))).Append("</p>");
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
            // (3.58) game versions: the oldest one allowed, and who's on what
            sb.Append("<h2>Game version</h2><p>Oldest version allowed: <b>").Append(VerText(MinVer)).Append("</b> - older games can't connect and are told to update. Online now: ");
            var vers = Clients.Values.Where(c => c.Name != "?" && c.Ver != "check" && !c.Refused).GroupBy(c => VerNum(c.Ver)).OrderByDescending(g => g.Key).ToList();
            sb.Append(vers.Count == 0 ? "<span class=dim>nobody</span>" : string.Join(", ", vers.Select(g => (g.Key > 0 ? VerText(g.Key) : "old") + " (" + g.Count() + ")"))).Append("</p>");
            sb.Append("<p class=dim>Cloud saves stored: ").Append(CloudCount()).Append(" (players' profiles are kept on the server - editing the headset's file does nothing)</p>");
            sb.Append("<form method=post action='act' class=inl><input type=hidden name=t value='").Append(sess).Append("'><input type=hidden name=op value='minver'><input name=ver size=6 placeholder='3.58' value='").Append(VerText(MinVer))
              .Append("'> <button class=orange>Set oldest version</button></form>");
            if (vers.Count > 0 && vers[0].Key > MinVer)
                sb.Append(Btn(sess, "minver", "", "Only allow " + VerText(vers[0].Key) + " (the newest online)", "<input type=hidden name=ver value='" + VerText(vers[0].Key) + "'>", "orange"));
            sb.Append("<p class=dim>After each update, once you've installed it yourself, press \"Only allow\" so everyone has to update.</p>");
            // players: online, or search everyone seen
            sb.Append("<h2>Players (").Append(Players.Count).Append(" with an account, ").Append(Clients.Values.Count(c => !string.IsNullOrEmpty(c.Key))).Append(" online)</h2><form method=get class=inl><input name=q value='").Append(H(q)).Append("' placeholder='search a callsign (e.g. nug)' size=26> <button>Search</button></form>");
            IEnumerable<Known> list;
            if (!string.IsNullOrWhiteSpace(q)) { var names = SearchNames(q, 30); list = names.Select(n => OnlineByName(n) is Client oc && !string.IsNullOrEmpty(oc.Key) && Players.TryGetValue(oc.Key, out var ok) ? ok : KnownByName(n)).Where(k => k != null).Distinct(); }
            else
            {
                // everyone with a callsign who has played on 2.59 or newer: online first, then by when they were last on
                var onKeys = new HashSet<string>(Clients.Values.Where(c => !string.IsNullOrEmpty(c.Key)).Select(c => c.Key));
                list = Players.Values.OrderByDescending(k => onKeys.Contains(k.Key)).ThenByDescending(k => k.Seen).Take(300);
            }
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
                sb.Append(Btn(sess, "cc", k.Key, "Give CC", "<select name=what><option value=trophy>CC trophy</option><option value=skin>CC skin</option><option value=both>Trophy + skin</option></select> ", "green"));
                sb.Append(Btn(sess, "betaskin", k.Key, "Give Beta skin", "", "green"));
                sb.Append(Btn(sess, "rename", k.Key, "Rename", "<input name=name placeholder='new callsign' size=10 maxlength=14> "));
                sb.Append("</td></tr>");
            }
            // players on an OLD game version (before 2.59) don't send an id, so they can't be banned or made mods - show them anyway
            string ql = (q ?? "").Trim().ToLowerInvariant();
            foreach (var oc in Clients.Values.Where(c => string.IsNullOrEmpty(c.Key) && c.Name != "?" && c.Ver != "check" && (ql.Length == 0 || c.Name.ToLowerInvariant().Contains(ql))))
            {
                rows++;
                sb.Append("<tr><td><b>").Append(H(oc.Name)).Append("</b> <span class='tag red'>OLD VERSION</span></td><td>online</td><td>now</td><td class=dim>on game ").Append(H(oc.Ver.Replace("_v", "v").Replace('_', '.')))
                  .Append(" - needs 2.59 or newer before they can be made a mod, banned or wiped</td></tr>");
            }
            if (rows == 0) sb.Append("<tr><td colspan=4 class=dim>").Append(string.IsNullOrWhiteSpace(q) ? "Nobody has played on game version 2.59 or newer yet." : "No callsign matches. Only players who have connected with game version 2.59 or newer can be found (older versions don't send an id).").Append("</td></tr>");
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
            // live event
            sb.Append("<h2>Live event</h2>");
            if (EventOn) sb.Append("<p class=ok><b>").Append(EventNames[EvKind]).Append("</b> is on for everyone - ").Append((EvUntil - Now) / 60 + 1).Append(" min left (started by ").Append(H(EvBy)).Append(") ").Append(Btn(sess, "stopevent", "", "Stop it", "", "red")).Append("</p>");
            else sb.Append("<p class=dim>No event running.</p>");
            sb.Append("<form method=post action='act' class=inl><input type=hidden name=t value='").Append(sess).Append("'><input type=hidden name=op value='event'><select name=kind>");
            for (int i = 1; i < EventNames.Length; i++) sb.Append("<option value=").Append(i).Append(">").Append(EventNames[i]).Append("</option>");
            sb.Append("</select> <select name=minutes><option value=30>30 min</option><option value=60 selected>1 hour</option><option value=120>2 hours</option><option value=360>6 hours</option><option value=1440>24 hours</option></select> <button class=green>Start event</button></form>");
            sb.Append("<p class=dim>Nuke: always a 1-minute countdown, then everyone still in a raid dies (the length you pick is ignored).</p>");
            sb.Append("<p class=dim>Moderators can start and stop events from the MOD tab in the game too.</p>");
            // history
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
