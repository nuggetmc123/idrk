using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KartVR
{
    [Serializable]
    public class ModEntry
    {
        public string name = "";
        public string id = "";   // Player ID, e.g. 3F9A2-C01B7
    }

    [Serializable]
    public class BanEntry
    {
        public string name = "";    // locked name (optional)
        public string id = "";      // Player ID (optional)
        public string device = "";  // device code from the mod menu (optional)
        public string reason = "";
        public string until = "";   // "" = forever, or a date like 2026-12-31
    }

    /// <summary>Lifts in-game kicks/bans (and mutes) given to this Player ID before the time.</summary>
    [Serializable]
    public class PardonEntry
    {
        public string id = "";      // Player ID
        public string before = "";  // UTC, e.g. 2026-10-19 02:30: anything given before this is lifted
    }

    [Serializable]
    public class ModerationList
    {
        public int version;
        public ModEntry[] mods = new ModEntry[0];
        public BanEntry[] bans = new BanEntry[0];
        public string[] blockedNames = new string[0];
        public PardonEntry[] pardons = new PardonEntry[0];
    }

    /// <summary>
    /// Moderators, bans and mutes.
    ///
    /// The list of mods and permanent bans is Assets/KartVR/Resources/moderation.json. The game also downloads the
    /// newest copy from GitHub when it starts, so a mod or ban added there works on every headset WITHOUT a new APK
    /// (the copy with the highest "version" wins, and it's remembered for offline use).
    ///
    /// In-game mod actions (kick, ban, force rename, mute) are signed with the mod's secret key
    /// (<see cref="PlayerIdentity"/>); the player's game checks the signature against the mod list before obeying.
    /// </summary>
    public static class Moderation
    {
        public const string Repo = "nuggetmc123/idrk";
        static readonly string[] Urls =
        {
            "https://raw.githubusercontent.com/" + Repo + "/main/KartVR/Assets/KartVR/Resources/moderation.json",
            "https://raw.githubusercontent.com/" + Repo + "/claude/optimistic-thompson-p39nuz/KartVR/Assets/KartVR/Resources/moderation.json",
        };
        const string CacheKey = "kartvr.modList", LocalBanKey = "kartvr.localBan", MuteKey = "kartvr.mutedUntil", MuteGivenKey = "kartvr.mutedAt";

        public enum Action : byte { Kick = 1, Ban = 2, Rename = 3, Mute = 4 }

        static ModerationList list;
        public static ModerationList List
        {
            get
            {
                if (list != null) return list;
                list = Parse((Resources.Load<TextAsset>("moderation") as TextAsset)?.text) ?? new ModerationList();
                var cached = Parse(PlayerPrefs.GetString(CacheKey, ""));
                if (cached != null && cached.version > list.version) list = cached;
                return list;
            }
        }

        /// <summary>Fired when a newer list arrives from GitHub.</summary>
        public static event System.Action Updated;

        static ModerationList Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<ModerationList>(json); }
            catch (Exception) { return null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("KartVR Safety");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SafetyRunner>();
        }

        /// <summary>Downloads the newest list from GitHub (runs once at startup).</summary>
        public static IEnumerator Fetch()
        {
            foreach (var url in Urls)
            {
                using (var req = UnityWebRequest.Get(url + "?t=" + DateTime.UtcNow.Ticks))
                {
                    req.timeout = 10;
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success) continue;
                    var fetched = Parse(req.downloadHandler.text);
                    if (fetched == null || fetched.version <= List.version) continue;
                    list = fetched;
                    PlayerPrefs.SetString(CacheKey, req.downloadHandler.text);
                    PlayerPrefs.Save();
                    Debug.Log("[KartVR] Moderation list updated to version " + fetched.version);
                    Updated?.Invoke();
                }
            }
        }

        // ---------------- who's a mod

        public static bool IsMod(string playerId) =>
            !string.IsNullOrEmpty(playerId) && List.mods.Any(m => m != null && Same(m.id, playerId));

        public static bool IAmMod => IsMod(PlayerIdentity.PlayerId);

        /// <summary>True if this player (e.g. who sent an RPC) proved they're a moderator.</summary>
        public static bool IsVerifiedModPlayer(Fusion.PlayerRef player)
        {
            foreach (var k in KartController.All)
                if (k != null && k.IsReady && k.Object.StateAuthority == player) return k.IsVerifiedMod;
            return false;
        }

        /// <summary>A mod's name belongs to them: nobody else can pick it.</summary>
        public static bool NameReservedForSomeoneElse(string name) =>
            List.mods.Any(m => m != null && Same(m.name, name) && !Same(m.id, PlayerIdentity.PlayerId));

        public static bool NameBlocked(string name) =>
            List.blockedNames.Any(b => Same(b, name)) || List.bans.Any(b => b != null && b.name.Length > 0 && Same(b.name, name));

        static bool Same(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        // ---------------- bans and mutes on this headset

        /// <summary>True if this headset may not play online right now (why says why, and until when).</summary>
        public static bool IsBanned(out string why)
        {
            why = null;
            ApplyPardons();
            // A ban a mod gave us in game.
            string local = PlayerPrefs.GetString(LocalBanKey, "");
            if (local.Length > 0)
            {
                var parts = local.Split('|');
                long until = parts.Length > 0 && long.TryParse(parts[0], out long u) ? u : 0;
                string reason = parts.Length > 1 ? parts[1] : "";
                if (until == 0 || until > Now)
                {
                    why = (until == 0 ? "Banned" : "Banned until " + DateTimeOffset.FromUnixTimeSeconds(until).ToLocalTime().ToString("MMM d, h:mm tt")) +
                          (reason.Length > 0 ? ": " + reason : "");
                    return true;
                }
                PlayerPrefs.DeleteKey(LocalBanKey);
            }
            // The permanent list.
            foreach (var b in List.bans)
            {
                if (b == null) continue;
                bool me = b.id.Length > 0 && Same(b.id, PlayerIdentity.PlayerId) ||
                          b.device.Length > 0 && Same(b.device, PlayerIdentity.DeviceHash) ||
                          b.name.Length > 0 && PlayerProfile.NameLocked && Same(b.name, PlayerProfile.Nickname);
                if (!me) continue;
                if (b.until.Length > 0 && DateTime.TryParse(b.until, out var end) && end < DateTime.Now) continue;
                why = "Banned" + (b.until.Length > 0 ? " until " + b.until : "") + (b.reason.Length > 0 ? ": " + b.reason : "");
                return true;
            }
            return false;
        }

        public static bool Muted => PlayerPrefs.GetString(MuteKey, "0") is string s && long.TryParse(s, out long t) && t > Now;

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ---------------- signed commands

        static byte[] CommandBytes(Action action, int arg, long time, string targetId) =>
            Encoding.UTF8.GetBytes("KVR1|" + (int)action + "|" + arg + "|" + time + "|" + targetId);

        /// <summary>Mod side: send a signed command to a player's kart.</summary>
        public static void Send(KartController target, Action action, int arg)
        {
            if (target == null || !target.IsReady || !IAmMod) return;
            long time = Now;
            var sig = PlayerIdentity.Sign(CommandBytes(action, arg, time, target.PlayerId.ToString()));
            target.RPC_Moderate((byte)action, arg, time, PlayerIdentity.PublicModulus, sig);
        }

        /// <summary>Player side: obey a command only if a real mod signed it, recently, for us.</summary>
        public static void Receive(byte action, int arg, long time, byte[] modKey, byte[] signature)
        {
            string modId = PlayerIdentity.Fingerprint(modKey);
            if (!IsMod(modId)) { Debug.LogWarning("[KartVR] Ignored a mod command from a non-mod."); return; }
            if (Math.Abs(Now - time) > 600) { Debug.LogWarning("[KartVR] Ignored an old mod command."); return; }
            if (!PlayerIdentity.Verify(modKey, CommandBytes((Action)action, arg, time, PlayerIdentity.PlayerId), signature))
            {
                Debug.LogWarning("[KartVR] Ignored a mod command with a bad signature.");
                return;
            }
            string modName = List.mods.FirstOrDefault(m => Same(m.id, modId))?.name;
            if (string.IsNullOrWhiteSpace(modName)) modName = "a moderator";
            Apply((Action)action, arg, modName);
        }

        static void Apply(Action action, int arg, string modName)
        {
            var sessions = SessionManager.Instance;
            switch (action)
            {
                case Action.Kick:
                    SaveLocalBan(Now + 5 * 60, "kicked by " + modName);
                    sessions?.Removed("You were kicked by " + modName + ". You can play online again in 5 minutes.");
                    break;
                case Action.Ban:
                    SaveLocalBan(arg <= 0 ? 0 : Now + arg * 3600L, "by " + modName);
                    sessions?.Removed(arg <= 0 ? "You were banned by " + modName + "." : "You were banned by " + modName + " for " + arg + " hours.");
                    break;
                case Action.Rename:
                    PlayerProfile.UnlockName();
                    NameEntry.Show("A moderator (" + modName + ") asked you to pick a new name.");
                    break;
                case Action.Mute:
                    PlayerPrefs.SetString(MuteKey, (Now + Mathf.Max(1, arg) * 60L).ToString());
                    PlayerPrefs.SetString(MuteGivenKey, Now.ToString());
                    PlayerPrefs.Save();
                    sessions?.SetStatus("You were muted for " + Mathf.Max(1, arg) + " minutes by " + modName, 8f);
                    break;
            }
        }

        /// <summary>
        /// The owner can undo an in-game kick or ban (or mute) from moderation.json: a pardon for your ID lifts
        /// anything a mod gave you before its time, so you can still be kicked again afterwards.
        /// </summary>
        static void ApplyPardons()
        {
            if (List.pardons == null) return;
            foreach (var p in List.pardons)
            {
                if (p == null || !Same(p.id, PlayerIdentity.PlayerId)) continue;
                if (!DateTime.TryParse(p.before, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var when)) continue;
                long cutoff = new DateTimeOffset(when, TimeSpan.Zero).ToUnixTimeSeconds();
                string local = PlayerPrefs.GetString(LocalBanKey, "");
                if (local.Length > 0)
                {
                    // Bans saved by older versions have no time: treat them as old.
                    var parts = local.Split('|');
                    long given = parts.Length > 2 && long.TryParse(parts[2], out long g) ? g : 0;
                    if (given <= cutoff) { PlayerPrefs.DeleteKey(LocalBanKey); Debug.Log("[KartVR] A moderator ban/kick was lifted."); }
                }
                string mute = PlayerPrefs.GetString(MuteGivenKey, "0");
                if (long.TryParse(mute, out long mutedAt) && mutedAt <= cutoff) { PlayerPrefs.DeleteKey(MuteKey); PlayerPrefs.DeleteKey(MuteGivenKey); }
                PlayerPrefs.Save();
            }
        }

        static void SaveLocalBan(long until, string reason)
        {
            PlayerPrefs.SetString(LocalBanKey, until + "|" + reason.Replace("|", "/") + "|" + Now);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Runs the startup download and the anti-cheat checks.</summary>
    public class SafetyRunner : MonoBehaviour
    {
        void Start()
        {
            StartCoroutine(Moderation.Fetch());
            gameObject.AddComponent<AntiCheat>();
            gameObject.AddComponent<NameEntry>();
            gameObject.AddComponent<ModBlaster>();
            gameObject.AddComponent<LiveEventFx>();
            gameObject.AddComponent<ProgressFx>();
            gameObject.AddComponent<MapAtmosphere>();
            gameObject.AddComponent<HalloweenDecor>();
            gameObject.AddComponent<HalloweenWatcher>();
            Moderation.Updated += () =>
            {
                var sessions = SessionManager.Instance;
                if (Moderation.IsBanned(out string why)) sessions?.Removed(why);
                else if (sessions != null && sessions.IsSolo && !sessions.Busy &&
                         (sessions.OnlineError.Contains("anned") || sessions.OnlineError.Contains("kicked")))
                    sessions.JoinPublic(KartMode.Hub); // a pardon just lifted the kick/ban: back online
            };
        }
    }
}
