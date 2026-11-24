using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Rago.Relay;

namespace RagoServer
{
    /// <summary>
    /// LIVE EVENTS: one at a time, for everyone on the server (every room, the hub, raids, the Market). A moderator starts
    /// one from the MOD tab in the game, or the owner from the panel; it runs for a set time, survives a restart, and
    /// players who connect while it's on are told about it straight away.
    /// </summary>
    static partial class Program
    {
        public static readonly string[] EventNames = { "", "Double XP", "Double Cash", "Boss Hunt", "Air Drop Rain", "Present Rain", "Concert", "Nuke" };
        static byte EvKind; static long EvUntil; static string EvBy = "";
        static bool EventOn => EvKind != 0 && Now < EvUntil;

        static void LoadEvent()
        {
            foreach (var f in Lines("event.tsv"))
                if (f.Length >= 3 && byte.TryParse(f[0], out var k) && long.TryParse(f[1], out var u) && k < EventNames.Length) { EvKind = k; EvUntil = u; EvBy = f[2]; }
            if (!EventOn) EvKind = 0;
        }

        static W EventFrame() => new W(P.EventIn).U8(EventOn ? EvKind : (byte)0).I32(EventOn ? (int)(EvUntil - Now) : 0).Str(EventOn ? EvBy : "");

        static void SendEvent(Client c) { if (EventOn) Send(c, EventFrame().Frame()); }

        /// <summary>Start (kind 1..) or stop (kind 0) the live event (under the lock). Tells everyone.</summary>
        static string StartEvent(byte kind, int minutes, string by)
        {
            if (kind >= EventNames.Length) return "Unknown event";
            if (kind == 0)
            {
                if (!EventOn) return "No event running";
                Note("live event " + EventNames[EvKind] + " stopped (by " + by + ")");
                EvKind = 0; EvUntil = 0; EvBy = by;
            }
            else
            {
                if (kind == 7) minutes = 5;   // the nuke: a 5-minute countdown, then every raid is hit
                minutes = Math.Clamp(minutes, 3, 1440);
                EvKind = kind; EvUntil = Now + minutes * 60L; EvBy = by;
                Note("live event " + EventNames[kind] + " started for " + minutes + " min (by " + by + ")");
            }
            Save("event.tsv", new[] { EvKind + "\t" + EvUntil + "\t" + Clean(EvBy) });
            var frame = EventFrame().Frame();
            foreach (var c in Clients.Values.Where(x => !x.Refused && x.Name != "?").ToList()) Send(c, frame);
            return kind == 0 ? "Event stopped" : EventNames[kind] + " is on for everyone for " + minutes + " min";
        }

        static void OnEventRequest(Client c, byte kind, int minutes)
        {
            if (string.IsNullOrEmpty(c.Key) || !Mods.ContainsKey(c.Key)) { Send(c, new W(P.ModReply).Str("You're not a moderator.").Frame()); return; }
            Send(c, new W(P.ModReply).Str(StartEvent(kind, minutes, c.Name)).Frame());
        }
    
        // ------------------------------------------------------------------ gifts from the owner (e.g. the CC trophy)
        static void SendGift(Client c, string item, string note) => Send(c, new W(P.Gift).Str(item).Str(note).Frame());

        static string GiveItem(string key, string name, string item, string label, string note)
        {
            var on = Clients.Values.FirstOrDefault(x => x.Key == key && !x.Refused);
            if (on != null) SendGift(on, item, note);
            else
            {
                var all = Lines("gifts.tsv").Select(f => string.Join("\t", f)).ToList();
                all.Add(Clean(key) + "\t" + Clean(item) + "\t" + Clean(note));
                Save("gifts.tsv", all);
            }
            Note("gave " + name + " a " + label + (on != null ? "" : " (waiting for them to log in)"));
            return "Gave " + name + " a " + label + (on != null ? " - it's on their Deliveries screen" : " - they get it next time they play");
        }

        static void DeliverGifts(Client c)
        {
            var all = Lines("gifts.tsv").Where(f => f.Length >= 3).ToList();
            var mine = all.Where(f => f[0] == c.Key).ToList();
            if (mine.Count == 0) return;
            foreach (var f in mine) SendGift(c, f[1], f[2]);
            Save("gifts.tsv", all.Where(f => f[0] != c.Key).Select(f => string.Join("\t", f)));
        }
    
        // ------------------------------------------------------------------ renames (owner panel)
        /// <summary>A callsign the game would accept: 3-14 of A-Z, 0-9 and _ (upper-cased here).</summary>
        static string CleanCallsign(string n)
        {
            n = (n ?? "").Trim().ToUpperInvariant();
            if (n.Length < 3 || n.Length > 14) return null;
            foreach (var ch in n) if (!(ch >= 'A' && ch <= 'Z') && !(ch >= '0' && ch <= '9') && ch != '_') return null;
            if (n.Trim('_').Length == 0) return null;
            return n;
        }

        static string WithTag(string oldDisplay, string newName)
        {
            int h = (oldDisplay ?? "").IndexOf('#');
            return h >= 0 ? newName + oldDisplay.Substring(h) : newName;
        }

        /// <summary>The owner renames a player: their callsign changes on the server now, and in their game straight away (or
        /// the next time they connect). Their #tag stays the same.</summary>
        static string RenamePlayer(string key, string newName)
        {
            if (!Players.TryGetValue(key, out var k)) return "Unknown player";
            var n = CleanCallsign(newName);
            if (n == null) return "Callsign must be 3-14 letters, numbers or _";
            string old = k.Name, shown = WithTag(old, n);
            if (Players.Values.Any(o => o.Key != key && string.Equals(o.Name, shown, StringComparison.OrdinalIgnoreCase))) return shown + " is already taken";
            k.Name = shown; _playersDirty = true;
            if (Mods.ContainsKey(key)) { Mods[key] = shown; SaveMods(); }
            var on = Clients.Values.FirstOrDefault(x => x.Key == key && !x.Refused);
            if (on != null) { on.Name = shown; Send(on, new W(P.Rename).Str(n).Frame()); }
            else
            {
                var all = Lines("renames.tsv").Where(f => f.Length >= 2 && f[0] != key).Select(f => string.Join("\t", f)).ToList();
                all.Add(Clean(key) + "\t" + n);
                Save("renames.tsv", all);
            }
            Note("renamed " + old + " to " + shown + (on != null ? "" : " (when they next log in)"));
            return "Renamed " + old + " to " + shown + (on != null ? "" : " - it changes in their game next time they play");
        }

        static void DeliverRename(Client c)
        {
            var all = Lines("renames.tsv").Where(f => f.Length >= 2).ToList();
            var mine = all.LastOrDefault(f => f[0] == c.Key);
            if (mine == null) return;
            Send(c, new W(P.Rename).Str(mine[1]).Frame());
            Save("renames.tsv", all.Where(f => f[0] != c.Key).Select(f => string.Join("\t", f)));
        }
    
        // ------------------------------------------------------------------ watching a player (owner panel)
        // While the watch page is open it asks again every second; the player's game is told to keep sending small views
        // (a JPEG of what they see, about 2 a second) and stops on its own a few seconds after the page closes.
        static readonly Dictionary<string, long> WatchAsked = new Dictionary<string, long>();   // key -> last page refresh
        static readonly Dictionary<string, long> WatchSent = new Dictionary<string, long>();    // key -> last WatchOn sent
        static readonly Dictionary<string, byte[]> WatchFrames = new Dictionary<string, byte[]>();
        static readonly byte[] Blank = { 0x47,0x49,0x46,0x38,0x39,0x61,1,0,1,0,0x80,0,0,0,0,0,0,0,0,0x21,0xF9,4,1,0,0,0,0,0x2C,0,0,0,0,1,0,1,0,0,2,2,0x44,1,0 };

        static void KeepWatching(string key)
        {
            WatchAsked[key] = Now;
            var on = Clients.Values.FirstOrDefault(x => x.Key == key && !x.Refused);
            if (on != null && (!WatchSent.TryGetValue(key, out var t) || Now - t >= 4)) { Send(on, new W(P.WatchOn).Frame()); WatchSent[key] = Now; }
            // anyone no longer being watched: tell their game to stop
            foreach (var k in WatchAsked.Where(kv => Now - kv.Value > 12).Select(kv => kv.Key).ToList())
            {
                WatchAsked.Remove(k); WatchSent.Remove(k); WatchFrames.Remove(k);
                var c = Clients.Values.FirstOrDefault(x => x.Key == k); if (c != null) Send(c, new W(P.WatchOff).Frame());
            }
        }

        static void OnWatchFrame(Client c, byte[] jpg)
        {
            if (string.IsNullOrEmpty(c.Key) || !WatchAsked.ContainsKey(c.Key) || jpg == null || jpg.Length < 4 || jpg.Length > 300_000) return;
            if (jpg[0] != 0xFF || jpg[1] != 0xD8) return;   // JPEG only
            WatchFrames[c.Key] = jpg;
        }

        static byte[] WatchImage(string key)
        {
            if (string.IsNullOrEmpty(key)) return Blank;
            if (WatchAsked.ContainsKey(key)) KeepWatching(key);   // the page fetching frames keeps the watch going (and renews the player's game)
            return WatchFrames.TryGetValue(key, out var j) ? j : Blank;
        }

        /// <summary>The watch page's script: fetches the next frame in the background and swaps it in when it has loaded,
        /// about 3 times a second - the page itself never reloads (it used to, every second: it flashed and jumped).</summary>
        const string WatchJs = @"(function(){var img=document.getElementById('v');if(!img)return;var key=img.getAttribute('data-key');var st=document.getElementById('st');var fails=0;
function next(){var n=new Image();n.onload=function(){img.src=n.src;fails=0;if(st)st.textContent=n.naturalWidth>1?'live':'waiting for their view...';setTimeout(next,300);};
n.onerror=function(){fails++;if(st)st.textContent='connection lost - retrying';setTimeout(next,fails>3?3000:1000);};
n.src='watchimg?key='+encodeURIComponent(key)+'&t='+Date.now();}next();})();";

        static string WatchPage(string key)
        {
            if (string.IsNullOrEmpty(key) || !Players.TryGetValue(key, out var k)) return Page("Watch", "<p>Unknown player. <a href='./'>Back</a></p>");
            var on = Clients.Values.FirstOrDefault(x => x.Key == key && !x.Refused);
            if (on == null) return Page("Watch", "<h1>" + H(k.Name) + "</h1><p class=dim>Not online right now. <a href='./'>Back to the panel</a></p>");
            KeepWatching(key);
            string where = on.Room == null ? "bunker" : InMarket(on) ? "Market" : on.Room.Name.Contains("_q_") ? "raid" : "online";
            return "<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>Watching " + H(k.Name) + "</title>"
                 + "<style>body{font:15px system-ui,sans-serif;background:#0d0e10;color:#e8e6e1;margin:0;padding:12px;text-align:center}img{width:100%;max-width:960px;aspect-ratio:16/9;background:#000;border-radius:8px;object-fit:contain}a{color:#7ab4ff}.dim{color:#8a8e95}</style></head><body>"
                 + "<h2 style='margin:4px 0'>" + H(k.Name) + " <span class=dim>(" + where + ")</span> <span id=st class=dim>connecting...</span></h2>"
                 + "<img id=v data-key='" + H(key) + "' alt=''>"
                 + "<p class=dim>Their first-person view, live. Their watch shows SPECTATED while you're here. <a href='./'>Stop watching</a></p>"
                 + "<script src='watch.js'></script></body></html>";
        }
    }
}
