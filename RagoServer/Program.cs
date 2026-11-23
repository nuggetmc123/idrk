using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Rago.Relay;

namespace RagoServer
{
    /// <summary>
    /// The Rago Extract game server: a relay with rooms. Players connect over TCP (reliable: events, joins, invites) and
    /// UDP (fast: movement, voice, raiders). A room is what the game calls a session: a party, the Market, or a raid
    /// queue. The server only passes messages between players in the same room - the game itself (raiders, loot,
    /// hits) still runs on the players' machines, like with Photon. No player limit except what the machine can carry.
    ///
    /// Run:  ./ragoserver [port]     (default 27015, TCP and UDP)
    /// </summary>
    static partial class Program
    {
        sealed class Client
        {
            public ulong Id; public uint Token; public string Name = "?", Ver = "", Key = "", Device = ""; public bool Refused;
            public TcpClient Tcp; public NetworkStream Stream;
            public Room Room; public IPEndPoint Udp;
            public readonly Channel<byte[]> Out = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
            public long Queued; public bool Closed; public DateTime Seen = DateTime.UtcNow;
            public long RateSec; public int RateFrames; public long RateBytes;   // flood guard: this second's frames / bytes
            public override string ToString() => Name + " (#" + Id + ")";
        }

        sealed class Room
        {
            public string Name; public int Max; public Client Host;
            public readonly List<Client> Members = new List<Client>();
            public readonly Dictionary<string, string> Props = new Dictionary<string, string>();
        }

        static readonly object Lock = new object();
        static readonly Dictionary<ulong, Client> Clients = new Dictionary<ulong, Client>();
        static readonly Dictionary<uint, Client> ByToken = new Dictionary<uint, Client>();
        static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        static ulong _nextId = 1;
        static UdpClient _udp;
        static readonly Random Rng = new Random();
        static long _rxTcp, _rxUdp, _txUdp;

        static void Log(string s) => Console.WriteLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s);

        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "check") { Environment.Exit(await Check(args.Length > 1 ? args[1] : "127.0.0.1", args.Length > 2 && int.TryParse(args[2], out var cp) ? cp : P.Port)); return; }
            int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : P.Port;
            // IPv4 + IPv6 together where the machine has IPv6, else IPv4 only
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.IPv6Any, port);
                listener.Server.DualMode = true;
                listener.Start();
                _udp = new UdpClient(AddressFamily.InterNetworkV6);
                _udp.Client.DualMode = true;
                _udp.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
            }
            catch (SocketException)
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            }
            try { _udp.Client.IOControl(-1744830452, new byte[] { 0 }, null); } catch { }   // Windows: ignore ICMP "port unreachable"
            Log("Rago game server listening on TCP+UDP " + port);
            LoadData();
            StartPanel();
            _ = Task.Run(UdpLoop);
            _ = Task.Run(Housekeeping);
            while (true)
            {
                TcpClient tcp;
                try { tcp = await listener.AcceptTcpClientAsync(); }
                catch (Exception e) { Log("accept: " + e.Message); continue; }
                _ = Task.Run(() => Serve(tcp));
            }
        }

        /// <summary>
        /// "./ragoserver check idrk.dev 27015": connects to a running server like the game does and checks both TCP (log in)
        /// and UDP (keep-alive answered). Run it from another machine to prove the firewall lets the game in.
        /// </summary>
        static async Task<int> Check(string host, int port)
        {
            Console.WriteLine("Checking the Rago game server at " + host + ":" + port + " ...");
            try
            {
                var ip = (await Dns.GetHostAddressesAsync(host)).First(a => a.AddressFamily == AddressFamily.InterNetwork);
                using var tcp = new TcpClient();
                var conn = tcp.ConnectAsync(ip, port);
                if (await Task.WhenAny(conn, Task.Delay(5000)) != conn || !tcp.Connected) { Console.WriteLine("TCP  FAIL - no answer on TCP " + port + " (server not running, or the firewall blocks TCP " + port + ")"); return 1; }
                var s = tcp.GetStream();
                var hello = new W(P.Hello).Str("CHECK#0000").Str("check").Frame();
                await s.WriteAsync(hello, 0, hello.Length);
                var head = new byte[4]; var cts = new CancellationTokenSource(5000);
                int got = 0; while (got < 4) { int k = await s.ReadAsync(head, got, 4 - got, cts.Token); if (k <= 0) throw new Exception("closed"); got += k; }
                int len = BitConverter.ToInt32(head, 0); var body = new byte[len]; got = 0;
                while (got < len) { int k = await s.ReadAsync(body, got, len - got, cts.Token); if (k <= 0) throw new Exception("closed"); got += k; }
                if (body[0] != P.Welcome) { Console.WriteLine("TCP  FAIL - something answered, but it isn't the Rago server"); return 1; }
                var r = new R(body, 1, len - 1); r.U64(); uint token = r.U32(); int online = r.I32();
                Console.WriteLine("TCP  OK   - logged in (" + online + " connected incl. this check)");
                using var udp = new UdpClient(); udp.Connect(ip, port);
                for (int i = 0; i < 5; i++)
                {
                    await udp.SendAsync(BitConverter.GetBytes(token), 4);
                    var rcv = udp.ReceiveAsync();
                    if (await Task.WhenAny(rcv, Task.Delay(800)) == rcv) { Console.WriteLine("UDP  OK   - keep-alive answered"); Console.WriteLine("All good: the game can connect."); return 0; }
                }
                Console.WriteLine("UDP  FAIL - no answer on UDP " + port + " (the firewall blocks UDP " + port + "). The game still works over TCP only, but movement and voice will be laggier.");
                return 2;
            }
            catch (Exception e) { Console.WriteLine("FAIL - " + e.Message); return 1; }
        }

        // ------------------------------------------------------------------------------------------- TCP
        static async Task Serve(TcpClient tcp)
        {
            tcp.NoDelay = true;
            var first = new byte[4];
            try { if (!await ReadExact(tcp.GetStream(), first, 4)) { tcp.Close(); return; } } catch { tcp.Close(); return; }
            if (LooksLikeHttp(first)) { await HttpConn(tcp.GetStream(), first, tcp); return; }   // a browser: the owner's admin panel
            var c = new Client { Tcp = tcp, Stream = tcp.GetStream() };
            lock (Lock)
            {
                if (Clients.Count >= 5000) { tcp.Close(); return; }
                c.Id = _nextId++;
                do c.Token = (uint)Rng.Next(1, int.MaxValue); while (ByToken.ContainsKey(c.Token));
                Clients[c.Id] = c; ByToken[c.Token] = c;
            }
            _ = Task.Run(() => Writer(c));
            var head = first;   // the first frame's length is already read
            bool got = true;
            try
            {
                while (true)
                {
                    if (!got && !await ReadExact(c.Stream, head, 4)) break;
                    got = false;
                    int len = BitConverter.ToInt32(head, 0);
                    if (len < 1 || len > P.MaxFrame) { Log(c + " sent a bad frame (" + len + ")"); break; }
                    var body = new byte[len];
                    if (!await ReadExact(c.Stream, body, len)) break;
                    Interlocked.Add(ref _rxTcp, len + 4);
                    c.Seen = DateTime.UtcNow;
                    if (Flooding(c, len + 4)) { Log(c + " flooding the server - dropped"); break; }
                    if (c.Refused || (body[0] != P.Hello && c.Name == "?")) continue;   // nothing before hello, nothing from the banned
                    try { lock (Lock) Handle(c, body[0], new R(body, 1, len - 1)); }
                    catch (Exception e) { Log(c + " message " + body[0] + " failed: " + e.Message); }
                }
            }
            catch (Exception) { }
            Drop(c);
        }

        /// <summary>Anti-abuse: a real game sends a few dozen messages and well under 1 MB a second. Far more is a
        /// modded or broken client trying to swamp everyone else in the room.</summary>
        static bool Flooding(Client c, int bytes)
        {
            long sec = Environment.TickCount64 / 1000;
            lock (c) { if (c.RateSec != sec) { c.RateSec = sec; c.RateFrames = 0; c.RateBytes = 0; } c.RateFrames++; c.RateBytes += bytes; return c.RateFrames > 2000 || c.RateBytes > 4 * 1024 * 1024; }
        }

        static async Task<bool> ReadExact(NetworkStream s, byte[] b, int n)
        {
            int got = 0;
            while (got < n)
            {
                int k = await s.ReadAsync(b, got, n - got);
                if (k <= 0) return false;
                got += k;
            }
            return true;
        }

        static async Task Writer(Client c)
        {
            try
            {
                await foreach (var f in c.Out.Reader.ReadAllAsync())
                {
                    Interlocked.Add(ref c.Queued, -f.Length);
                    await c.Stream.WriteAsync(f, 0, f.Length);
                }
            }
            catch (Exception) { }
            Drop(c);
        }

        static void Send(Client c, byte[] frame)
        {
            if (c.Closed) return;
            // a player who can't keep up (connection stalled) gets dropped instead of eating the server's memory
            if (Interlocked.Add(ref c.Queued, frame.Length) > 8 * 1024 * 1024) { Log(c + " too far behind - dropped"); Drop(c); return; }
            c.Out.Writer.TryWrite(frame);
        }

        static void Drop(Client c)
        {
            lock (Lock)
            {
                if (c.Closed) return;
                c.Closed = true;
                LeaveRoom(c);
                Clients.Remove(c.Id); ByToken.Remove(c.Token);
            }
            c.Out.Writer.TryComplete();
            try { c.Tcp.Close(); } catch { }
            if (c.Name != "?") Log(c + " disconnected (" + Clients.Count + " online)");
        }

        static void Handle(Client c, byte type, R r)
        {
            switch (type)
            {
                case P.Hello:
                {
                    bool first = c.Name == "?";
                    c.Name = r.Str(); c.Ver = r.Str();
                    if (r.More) { c.Key = Clean(r.Str()); c.Device = Clean(r.Str()); }
                    if (c.Key.Length > 64) c.Key = c.Key.Substring(0, 64);
                    if (c.Device.Length > 64) c.Device = c.Device.Substring(0, 64);
                    if (!OnHello(c)) break;   // banned: told why, connection closing
                    Send(c, new W(P.Welcome).U64(c.Id).U32(c.Token).I32(Clients.Count).Frame());
                    if (first) Log(c + " connected, version " + c.Ver + " (" + Clients.Count + " online)");
                    break;
                }
                case P.Search:
                {
                    string q = r.Str(); int id = r.I32();
                    var names = SearchNames(q, 8);
                    var w = new W(P.Found).I32(id).U16(names.Count);
                    foreach (var n in names) w.Str(n);
                    Send(c, w.Frame());
                    break;
                }
                case P.ModReq:
                {
                    byte act = r.U8(); string target = r.Str(), reason = r.Str(); int hours = r.I32();
                    OnModRequest(c, act, target, reason, hours);
                    break;
                }
                case P.Join:
                {
                    string name = r.Str(); int max = r.U8(); bool create = r.Bool(); var props = r.Dict();
                    LeaveRoom(c);
                    if (!Rooms.TryGetValue(name, out var room))
                    {
                        if (!create) { Send(c, new W(P.JoinFail).Str(name).Str("not found").Frame()); break; }
                        room = new Room { Name = name, Max = Math.Max(1, max) };
                        foreach (var kv in props) room.Props[kv.Key] = kv.Value;
                        Rooms[name] = room;
                    }
                    else if (room.Members.Count >= room.Max) { Send(c, new W(P.JoinFail).Str(name).Str("full").Frame()); break; }
                    var others = room.Members.ToList();
                    room.Members.Add(c); c.Room = room;
                    if (room.Host == null) room.Host = c;
                    var w = new W(P.Joined).Str(name).U64(room.Host.Id).Dict(room.Props).U16(others.Count);
                    foreach (var o in others) w.U64(o.Id).Str(o.Name);
                    Send(c, w.Frame());
                    var pj = new W(P.PeerJoin).U64(c.Id).Str(c.Name).Frame();
                    foreach (var o in others) Send(o, pj);
                    break;
                }
                case P.Leave: LeaveRoom(c); break;
                case P.Data:
                case P.Fast:
                {
                    ulong target = r.U64(); var data = r.Bytes();
                    var room = c.Room; if (room == null) break;
                    var f = new W(type == P.Data ? P.DataIn : P.FastIn).U64(c.Id).Bytes(data).Frame();
                    foreach (var o in room.Members) if (o != c && (target == 0 || o.Id == target)) Send(o, f);
                    break;
                }
                case P.SetProps:
                {
                    var d = r.Dict(); var room = c.Room; if (room == null) break;
                    foreach (var kv in d) { if (string.IsNullOrEmpty(kv.Value)) room.Props.Remove(kv.Key); else room.Props[kv.Key] = kv.Value; }
                    var f = new W(P.Props).Str(room.Name).Dict(room.Props).Frame();
                    foreach (var o in room.Members) Send(o, f);
                    break;
                }
                case P.List:
                {
                    string prefix = r.Str(); int id = r.I32();
                    var list = Rooms.Values.Where(x => x.Name.StartsWith(prefix, StringComparison.Ordinal)).Take(200).ToList();
                    var w = new W(P.Rooms).I32(id).U16(list.Count);
                    foreach (var x in list) w.Str(x.Name).U8((byte)Math.Min(255, x.Members.Count)).U8((byte)Math.Min(255, x.Max)).Dict(x.Props);
                    Send(c, w.Frame());
                    break;
                }
                case P.Whisper:
                {
                    string to = r.Str(); var data = r.Bytes();
                    var t = Clients.Values.FirstOrDefault(x => x != c && x.Ver == c.Ver && string.Equals(x.Name, to, StringComparison.OrdinalIgnoreCase));
                    if (t == null) Send(c, new W(P.NoSuch).Str(to).Frame());
                    else Send(t, new W(P.WhisperIn).Str(c.Name).U64(c.Id).Bytes(data).Frame());
                    break;
                }
                case P.SetHost:
                {
                    ulong id = r.U64(); var room = c.Room; if (room == null) break;
                    var h = room.Members.FirstOrDefault(x => x.Id == id);
                    if (h == null || h == room.Host) break;
                    room.Host = h;
                    var f = new W(P.Host).U64(h.Id).Frame();
                    foreach (var o in room.Members) Send(o, f);
                    break;
                }
                case P.Ping: Send(c, new W(P.Pong).F32(r.F32()).Frame()); break;
            }
        }

        static void LeaveRoom(Client c)
        {
            var room = c.Room; if (room == null) return;
            c.Room = null;
            room.Members.Remove(c);
            if (room.Members.Count == 0) { Rooms.Remove(room.Name); return; }
            var f = new W(P.PeerLeft).U64(c.Id).Frame();
            foreach (var o in room.Members) Send(o, f);
            if (room.Host == c)
            {
                room.Host = room.Members[0];
                var hf = new W(P.Host).U64(room.Host.Id).Frame();
                foreach (var o in room.Members) Send(o, hf);
            }
        }

        // ------------------------------------------------------------------------------------------- UDP
        static async Task UdpLoop()
        {
            var ack = new byte[8];
            while (true)
            {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync(); }
                catch (Exception) { continue; }
                var b = res.Buffer;
                if (b.Length < 4) continue;
                Interlocked.Add(ref _rxUdp, b.Length);
                uint token = BitConverter.ToUInt32(b, 0);
                List<(IPEndPoint ep, Client c)> to = null; byte[] outPkt = null; Client from;
                lock (Lock)
                {
                    if (!ByToken.TryGetValue(token, out from) || from.Closed) continue;
                    if (Flooding(from, b.Length)) continue;   // over the limit: its UDP packets are just dropped
                    from.Udp = res.RemoteEndPoint;
                    from.Seen = DateTime.UtcNow;
                    if (b.Length > 4 && from.Room != null)
                    {
                        outPkt = new byte[8 + b.Length - 4];
                        Buffer.BlockCopy(BitConverter.GetBytes(from.Id), 0, outPkt, 0, 8);
                        Buffer.BlockCopy(b, 4, outPkt, 8, b.Length - 4);
                        to = new List<(IPEndPoint, Client)>();
                        foreach (var o in from.Room.Members) if (o != from) to.Add((o.Udp, o));
                    }
                }
                if (b.Length == 4) { try { await _udp.SendAsync(ack, 8, res.RemoteEndPoint); } catch { } continue; }   // keep-alive: answer it
                if (to == null) continue;
                foreach (var (ep, c) in to)
                {
                    if (ep != null) { try { await _udp.SendAsync(outPkt, outPkt.Length, ep); Interlocked.Add(ref _txUdp, outPkt.Length); } catch { } }
                    else { var data = new byte[b.Length - 4]; Buffer.BlockCopy(b, 4, data, 0, data.Length); lock (Lock) Send(c, new W(P.FastIn).U64(from.Id).Bytes(data).Frame()); }   // they have no UDP: send it over TCP
                }
            }
        }

        // ------------------------------------------------------------------------------------------- housekeeping
        static async Task Housekeeping()
        {
            int tick = 0;
            while (true)
            {
                await Task.Delay(5000);
                List<Client> dead = new List<Client>();
                lock (Lock)
                {
                    var f = new W(P.Online).I32(Clients.Count).Frame();
                    foreach (var c in Clients.Values) { Send(c, f); if ((DateTime.UtcNow - c.Seen).TotalSeconds > 60) dead.Add(c); }
                }
                foreach (var c in dead) { Log(c + " silent for 60 s - dropped"); Drop(c); }
                lock (Lock) if (_playersDirty) { _playersDirty = false; SavePlayers(); }
                if (++tick % 60 == 0)   // every 5 minutes
                    lock (Lock) Log("status: " + Clients.Count + " online, " + Rooms.Count + " rooms, in " + (_rxTcp + _rxUdp) / 1024 / 1024 + " MB, udp out " + _txUdp / 1024 / 1024 + " MB");
            }
        }
    }
}
