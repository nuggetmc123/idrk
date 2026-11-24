using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rago.Relay
{
    /// <summary>
    /// The wire protocol between the game and the Rago game server (Server/RagoServer). Plain C#, shared by both sides.
    ///
    /// TCP frames: [u32 length][u8 type][body] (length counts type + body). Strings: [u16 length][utf8].
    /// UDP (the fast lane: movement, voice, raiders): game -> server [u32 token][payload]; server -> game
    /// [u64 from][payload]. An empty payload is a keep-alive; the server answers it with [u64 0] so the game knows UDP works.
    /// </summary>
    public static class P
    {
        public const int Port = 27015;
        public const int MaxFrame = 1 << 20;
        public const int MaxUdp = 1150;

        // game -> server
        public const byte Hello = 1, Join = 2, Leave = 3, Data = 4, SetProps = 5, List = 6, Whisper = 7, SetHost = 8, Ping = 9, Fast = 10,
                          ModReq = 11, Search = 12,
                          ShopPut = 13, ShopGet = 14, ShopBuy = 15, ShopUnlist = 16, EventReq = 17, WatchFrame = 18;
        // WatchFrame: a small JPEG of what this player sees, while the owner is watching them (owner panel > Watch)
        // Search: callsigns starting with / containing some text (as you type). Shop*: the black market, kept on the
        // server so shops stay open while their owner is offline.
        // EventReq: a moderator starts (kind, minutes) or stops (kind 0) a LIVE EVENT for everyone on the server
        // ModReq: a moderator asks for a ban / unban / wipe / Market kick (the owner approves it on the web panel)
        // server -> game
        public const byte Welcome = 101, Joined = 102, JoinFail = 103, DataIn = 104, Props = 105, Rooms = 106, WhisperIn = 107, NoSuch = 108,
                          Host = 109, Pong = 110, PeerJoin = 111, PeerLeft = 112, Online = 113, FastIn = 114, Bye = 115,
                          Role = 116, Banned = 117, Wipe = 118, Kicked = 119, ModReply = 120, Found = 121,
                          ShopsIn = 122, BuyResult = 123, ShopSold = 124, UnlistResult = 125, EventIn = 126, Gift = 127, Rename = 128, WatchOn = 129, WatchOff = 130;
        // WatchOn: the owner is watching - send a view now and then (renewed every few seconds; stops by itself without it). WatchOff: stop
        // Rename: the owner changed this player's callsign (the new name, without the #tag)
        // Gift: the owner gives a player an item (e.g. the CC trophy) - it lands on their Deliveries screen
        // EventIn: the live event right now (kind, seconds left, started by) - sent to everyone when it changes and on connect
        // moderation actions (ModReq)
        public const byte ActBan = 1, ActUnban = 2, ActWipe = 3, ActKick = 4;
    }

    public sealed class W
    {
        readonly MemoryStream _m = new MemoryStream(64);
        public W(byte type) { _m.Write(new byte[4], 0, 4); _m.WriteByte(type); }
        public W U8(byte v) { _m.WriteByte(v); return this; }
        public W Bool(bool v) { _m.WriteByte((byte)(v ? 1 : 0)); return this; }
        public W U16(int v) { _m.WriteByte((byte)v); _m.WriteByte((byte)(v >> 8)); return this; }
        public W U32(uint v) { var b = BitConverter.GetBytes(v); _m.Write(b, 0, 4); return this; }
        public W I32(int v) { var b = BitConverter.GetBytes(v); _m.Write(b, 0, 4); return this; }
        public W U64(ulong v) { var b = BitConverter.GetBytes(v); _m.Write(b, 0, 8); return this; }
        public W F32(float v) { var b = BitConverter.GetBytes(v); _m.Write(b, 0, 4); return this; }
        public W Str(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(b.Length, 65535);
            U16(n); _m.Write(b, 0, n); return this;
        }
        public W Bytes(byte[] b, int off = 0, int len = -1)
        {
            if (len < 0) len = b == null ? 0 : b.Length - off;
            U32((uint)len); if (len > 0) _m.Write(b, off, len); return this;
        }
        public W Dict(Dictionary<string, string> d)
        {
            U16(d == null ? 0 : d.Count);
            if (d != null) foreach (var kv in d) { Str(kv.Key); Str(kv.Value); }
            return this;
        }
        /// <summary>The finished frame, length prefix filled in.</summary>
        public byte[] Frame()
        {
            var a = _m.ToArray();
            var len = BitConverter.GetBytes((uint)(a.Length - 4));
            Buffer.BlockCopy(len, 0, a, 0, 4);
            return a;
        }
    }

    public sealed class R
    {
        readonly byte[] _b; int _i; readonly int _end;
        public R(byte[] b, int off = 0, int len = -1) { _b = b; _i = off; _end = len < 0 ? b.Length : off + len; }
        public bool More => _i < _end;
        void Need(int n) { if (_i + n > _end) throw new EndOfStreamException(); }
        public byte U8() { Need(1); return _b[_i++]; }
        public bool Bool() => U8() != 0;
        public int U16() { Need(2); int v = _b[_i] | (_b[_i + 1] << 8); _i += 2; return v; }
        public uint U32() { Need(4); var v = BitConverter.ToUInt32(_b, _i); _i += 4; return v; }
        public int I32() { Need(4); var v = BitConverter.ToInt32(_b, _i); _i += 4; return v; }
        public ulong U64() { Need(8); var v = BitConverter.ToUInt64(_b, _i); _i += 8; return v; }
        public float F32() { Need(4); var v = BitConverter.ToSingle(_b, _i); _i += 4; return v; }
        public string Str() { int n = U16(); Need(n); var s = Encoding.UTF8.GetString(_b, _i, n); _i += n; return s; }
        public byte[] Bytes() { int n = (int)U32(); if (n < 0 || n > P.MaxFrame) throw new InvalidDataException(); Need(n); var a = new byte[n]; Buffer.BlockCopy(_b, _i, a, 0, n); _i += n; return a; }
        public Dictionary<string, string> Dict()
        {
            int n = U16(); var d = new Dictionary<string, string>();
            for (int k = 0; k < n; k++) { var key = Str(); d[key] = Str(); }
            return d;
        }
    }
}
