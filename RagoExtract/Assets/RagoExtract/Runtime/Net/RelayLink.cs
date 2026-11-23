using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Rago.Relay
{
    /// <summary>
    /// The game's connection to the Rago game server (plain C#, no Unity): one TCP connection for everything reliable and
    /// a UDP socket for the fast lane. Background threads read and write; everything that arrives waits in Inbox for the
    /// game to handle on its own thread. Fast packets go over UDP while the server answers our keep-alives, else TCP.
    /// </summary>
    public sealed class RelayLink
    {
        public struct Msg { public byte Type; public byte[] Body; public ulong From; }   // From: only for UDP fast packets (Type = FastIn, Body = data)

        public readonly ConcurrentQueue<Msg> Inbox = new ConcurrentQueue<Msg>();
        public volatile bool Connected, Failed;
        public volatile string Error = "";
        public ulong Id; public uint Token; public int Online;
        public bool UdpOk => _udpAck > 0 && Environment.TickCount - _udpAck < 4000;
        public long SentBytes, ReceivedBytes;

        TcpClient _tcp; NetworkStream _stream; UdpClient _udp; IPEndPoint _server;
        readonly BlockingCollection<byte[]> _out = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        volatile int _udpAck; volatile bool _closing;
        Thread _reader, _writer, _udpReader;

        /// <summary>Connect in the background; Connected (or Failed + Error) says how it went.</summary>
        public void Connect(string host, int port, string name, string ver)
        {
            var t = new Thread(() =>
            {
                try
                {
                    var addrs = Dns.GetHostAddresses(host);
                    if (addrs.Length == 0) throw new Exception("server address not found");
                    IPAddress ip = Array.Find(addrs, a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs[0];
                    _server = new IPEndPoint(ip, port);
                    _tcp = new TcpClient(ip.AddressFamily) { NoDelay = true };
                    var ar = _tcp.BeginConnect(ip, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(6000)) { try { _tcp.Close(); } catch { } throw new Exception("the server didn't answer (is it running? port " + port + " open?)"); }
                    _tcp.EndConnect(ar);
                    _stream = _tcp.GetStream();
                    _writer = new Thread(WriteLoop) { IsBackground = true, Name = "RagoRelayOut" }; _writer.Start();
                    Send(new W(P.Hello).Str(name).Str(ver).Frame());
                    try
                    {
                        _udp = new UdpClient(ip.AddressFamily);
                        _udp.Connect(_server);
                        _udpReader = new Thread(UdpLoop) { IsBackground = true, Name = "RagoRelayUdp" }; _udpReader.Start();
                    }
                    catch (Exception) { _udp = null; }   // no UDP: everything goes over TCP
                    _reader = new Thread(ReadLoop) { IsBackground = true, Name = "RagoRelayIn" }; _reader.Start();
                }
                catch (Exception e) { Error = e.Message; Failed = true; }
            }) { IsBackground = true, Name = "RagoRelayConnect" };
            t.Start();
        }

        public void Send(byte[] frame) { if (!_closing) _out.Add(frame); }

        /// <summary>Fast lane: UDP when it works, else over TCP. Broadcast to the room.</summary>
        public void SendFast(byte[] data, int len)
        {
            if (_closing || len <= 0) return;
            if (_udp != null && UdpOk && len <= P.MaxUdp)
            {
                var pkt = new byte[len + 4];
                Buffer.BlockCopy(BitConverter.GetBytes(Token), 0, pkt, 0, 4);
                Buffer.BlockCopy(data, 0, pkt, 4, len);
                try { _udp.Send(pkt, pkt.Length); Interlocked.Add(ref SentBytes, pkt.Length); return; } catch (Exception) { }
            }
            Send(new W(P.Fast).U64(0).Bytes(data, 0, len).Frame());
        }

        /// <summary>Call about once a second: UDP keep-alive (and the server's answer proves UDP gets through).</summary>
        public void KeepAlive()
        {
            if (_udp == null || Token == 0 || _closing) return;
            try { _udp.Send(BitConverter.GetBytes(Token), 4); } catch (Exception) { }
        }

        void WriteLoop()
        {
            try
            {
                foreach (var f in _out.GetConsumingEnumerable())
                {
                    _stream.Write(f, 0, f.Length);
                    Interlocked.Add(ref SentBytes, f.Length);
                }
            }
            catch (Exception e) { Lost(e.Message); }
        }

        void ReadLoop()
        {
            var head = new byte[4];
            try
            {
                while (!_closing)
                {
                    if (!ReadExact(head, 4)) break;
                    int len = BitConverter.ToInt32(head, 0);
                    if (len < 1 || len > P.MaxFrame) throw new Exception("bad data from the server");
                    var body = new byte[len];
                    if (!ReadExact(body, len)) break;
                    Interlocked.Add(ref ReceivedBytes, len + 4);
                    byte type = body[0];
                    if (type == P.Welcome)
                    {
                        var r = new R(body, 1, len - 1);
                        Id = r.U64(); Token = r.U32(); Online = r.I32();
                        Connected = true;
                        KeepAlive();
                    }
                    var payload = new byte[len - 1];
                    Buffer.BlockCopy(body, 1, payload, 0, len - 1);
                    Inbox.Enqueue(new Msg { Type = type, Body = payload });
                }
                Lost("the server closed the connection");
            }
            catch (Exception e) { Lost(e.Message); }
        }

        bool ReadExact(byte[] b, int n)
        {
            int got = 0;
            while (got < n) { int k = _stream.Read(b, got, n - got); if (k <= 0) return false; got += k; }
            return true;
        }

        void UdpLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (!_closing)
            {
                try
                {
                    var b = _udp.Receive(ref any);
                    if (b == null || b.Length < 8) continue;
                    Interlocked.Add(ref ReceivedBytes, b.Length);
                    ulong from = BitConverter.ToUInt64(b, 0);
                    if (from == 0) { _udpAck = Environment.TickCount; if (_udpAck == 0) _udpAck = 1; continue; }   // keep-alive answered
                    var data = new byte[b.Length - 8];
                    Buffer.BlockCopy(b, 8, data, 0, data.Length);
                    Inbox.Enqueue(new Msg { Type = P.FastIn, Body = data, From = from });
                }
                catch (SocketException) { if (_closing) return; Thread.Sleep(50); }   // e.g. "port unreachable" bounced back: keep listening
                catch (ObjectDisposedException) { return; }
                catch (Exception) { if (_closing) return; }
            }
        }

        void Lost(string why)
        {
            if (_closing) return;
            Error = why;
            bool was = Connected;
            Connected = false; Failed = true;
            if (was) Inbox.Enqueue(new Msg { Type = P.Bye, Body = new byte[0] });
            Close();
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            Connected = false;
            try { _out.CompleteAdding(); } catch { }
            try { _tcp?.Close(); } catch { }
            try { _udp?.Close(); } catch { }
        }
    }
}
