using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Flopzone
{
    /// <summary>
    /// The moderator and ban lists in Resources/moderation.json. Every headset
    /// also downloads the newest copy from <c>updateUrl</c> when the game starts,
    /// so a ban added to the file on GitHub works without a new APK.
    ///
    /// <code>
    /// "admins": ["FZ7K3Q9X"],
    /// "bans": [{ "id": "FZAB12CD", "reason": "slurs", "until": "2026-12-01" }]   // until "" = forever
    /// </code>
    /// </summary>
    public static class Moderation
    {
        static int version = -1;
        static readonly HashSet<string> admins = new HashSet<string>();
        static readonly Dictionary<string, (string reason, DateTime until)> bans = new Dictionary<string, (string, DateTime)>();
        static string updateUrl = "";

        public static void LoadBuiltIn()
        {
            var t = Resources.Load<TextAsset>("moderation");
            if (t != null) Apply(t.text);
        }

        /// <summary>Fetch the latest list (gives up quietly after a few seconds).</summary>
        public static IEnumerator Refresh()
        {
            if (version < 0) LoadBuiltIn();
            if (string.IsNullOrEmpty(updateUrl)) yield break;
            using (var r = UnityWebRequest.Get(updateUrl + (updateUrl.Contains("?") ? "&" : "?") + "t=" + DateTime.UtcNow.Ticks))
            {
                r.timeout = 6;
                yield return r.SendWebRequest();
                if (r.result == UnityWebRequest.Result.Success) Apply(r.downloadHandler.text);
            }
        }

        static void Apply(string json)
        {
            var d = Json.ParseObject(json);
            if (d == null) return;
            int v = d.Int("version");
            if (v < version) return;
            version = v;
            if (d.Has("updateUrl")) updateUrl = d.Str("updateUrl");
            admins.Clear();
            var a = d.Arr("admins");
            if (a != null) foreach (var o in a) if (o is string s) admins.Add(s.Trim().ToUpperInvariant());
            bans.Clear();
            var b = d.Arr("bans");
            if (b != null)
                foreach (var o in b)
                {
                    if (!(o is Dictionary<string, object> ban)) continue;
                    var until = DateTime.MaxValue;
                    if (DateTime.TryParse(ban.Str("until"), out var u)) until = u;
                    bans[ban.Str("id").Trim().ToUpperInvariant()] = (ban.Str("reason"), until);
                }
        }

        public static bool IsAdmin(string id) => !string.IsNullOrEmpty(id) && admins.Contains(id.ToUpperInvariant());

        public static bool IsBanned(string id, out string reason, out long untilMs)
        {
            reason = "";
            untilMs = 0;
            if (string.IsNullOrEmpty(id) || !bans.TryGetValue(id.ToUpperInvariant(), out var ban)) return false;
            if (ban.until <= DateTime.UtcNow) return false;
            reason = ban.reason;
            untilMs = ban.until == DateTime.MaxValue ? 0 : new DateTimeOffset(ban.until).ToUnixTimeMilliseconds();
            return true;
        }
    }
}
