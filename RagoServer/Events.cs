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
        public static readonly string[] EventNames = { "", "Double XP", "Double Cash", "Boss Hunt", "Air Drop Rain", "Present Rain" };
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
                minutes = Math.Clamp(minutes, 10, 1440);
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
    }
}
