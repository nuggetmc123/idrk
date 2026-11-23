using System;
using System.Linq;
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
    }
}
