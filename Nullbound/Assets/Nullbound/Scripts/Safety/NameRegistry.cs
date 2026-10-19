using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Nullbound
{
    /// <summary>
    /// Makes sure no two players have the same name. Names are claimed on a tiny server (Nullbound/Server, a free
    /// Cloudflare Worker) that remembers which Player ID owns each name; the claim is signed with your secret key,
    /// so nobody can steal your name by copying your ID. Its address goes in moderation.json ("nameServer"), so it
    /// reaches every headset without a new APK. Until it's set up, names are only checked against the rules
    /// (and moderators' names) on this headset.
    /// </summary>
    public static class NameRegistry
    {
        public static string Url => (Moderation.List.nameServer ?? "").Trim().TrimEnd('/');
        public static bool Configured => Url.Length > 0;

        [Serializable]
        class Reply
        {
            public bool ok;
            public string error = "";
            public string owner = "";
        }

        /// <summary>Claim a name for this headset's Player ID. done(ok, problem).</summary>
        public static IEnumerator Claim(string name, Action<bool, string> done)
        {
            if (!Configured) { done(true, null); yield break; }
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string message = "NB-NAME|" + name.Trim() + "|" + time;
            string body = "{\"name\":" + Quote(name.Trim()) + ",\"id\":" + Quote(PlayerIdentity.PlayerId) +
                          ",\"key\":" + Quote(Convert.ToBase64String(PlayerIdentity.PublicModulus)) + ",\"time\":" + time +
                          ",\"sig\":" + Quote(Convert.ToBase64String(PlayerIdentity.Sign(Encoding.UTF8.GetBytes(message)))) + "}";
            using (var req = new UnityWebRequest(Url + "/claim", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success && req.responseCode != 409)
                {
                    done(false, "Couldn't check the name (no internet?). Try again.");
                    yield break;
                }
                Reply reply = null;
                try { reply = JsonUtility.FromJson<Reply>(req.downloadHandler.text); } catch (Exception) { }
                if (reply != null && reply.ok) done(true, null);
                else done(false, reply != null && reply.error == "taken" ? "That name is taken. Pick another!" : "That name can't be used");
            }
        }

        /// <summary>At startup: make sure your locked name is still yours (and claim it if the server is new).</summary>
        public static IEnumerator Refresh()
        {
            // Wait for the mod list download (it holds the server address).
            float until = Time.realtimeSinceStartup + 15f;
            while (!Configured && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(1f);
            if (!Configured || !PlayerProfile.NameLocked) yield break;
            string name = PlayerProfile.Nickname;
            yield return Claim(name, (ok, problem) =>
            {
                if (ok || problem == null || !problem.Contains("taken")) return;
                PlayerProfile.UnlockName();
                NameEntry.Show("Someone else already had the name \"" + name + "\". Please pick a new one.");
            });
        }

        static string Quote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
