using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Rago.Relay;

namespace RagoServer
{
    /// <summary>
    /// (3.73) CLOUD SAVES: each player's profile lives here on the server, not in an editable file on their headset. The
    /// game uploads a sealed (encrypted + signed) blob keyed by the player's key; we store it as an opaque file and hand it
    /// back on request. We never read inside it - it's the game's own sealed save - we just keep the newest copy per player.
    /// A player who edits their local file, or crafts their own upload, is held to the newest revision we've seen: an older
    /// revision can't overwrite a newer one.
    /// </summary>
    static partial class Program
    {
        const int MaxCloud = 512 * 1024;   // a profile is a few KB; cap well above that
        static string CloudDir => Path.Combine(_dataDir, "profiles");

        static string CloudPath(string key)
        {
            // the key hashed to a safe filename (a player's key never becomes a path)
            using var sha = SHA256.Create();
            string h = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(key ?? ""))).ToLowerInvariant();
            return Path.Combine(CloudDir, h + ".sav");
        }

        /// <summary>Send the player their saved profile (or "none").</summary>
        static void CloudSend(Client c)
        {
            int rev = 0; byte[] blob = null;
            if (!string.IsNullOrEmpty(c.Key))
            {
                try
                {
                    var path = CloudPath(c.Key);
                    if (File.Exists(path))
                    {
                        var all = File.ReadAllBytes(path);
                        if (all.Length >= 4) { rev = BitConverter.ToInt32(all, 0); blob = all.Skip(4).ToArray(); }
                    }
                }
                catch (Exception e) { Log("cloud read " + c + ": " + e.Message); }
            }
            var w = new W(P.CloudData).Bool(blob != null).I32(rev).Bytes(blob ?? Array.Empty<byte>());
            Send(c, w.Frame());
        }

        /// <summary>Store the player's uploaded profile - but never let an older revision overwrite a newer one.</summary>
        static void CloudStore(Client c, int rev, byte[] blob)
        {
            if (string.IsNullOrEmpty(c.Key) || blob == null || blob.Length == 0 || blob.Length > MaxCloud) return;
            try
            {
                Directory.CreateDirectory(CloudDir);
                var path = CloudPath(c.Key);
                if (File.Exists(path))
                {
                    try { var cur = File.ReadAllBytes(path); if (cur.Length >= 4 && BitConverter.ToInt32(cur, 0) > rev) { Log(c + " cloud save ignored (older rev " + rev + ")"); return; } }
                    catch { }
                }
                var outp = new byte[4 + blob.Length];
                Buffer.BlockCopy(BitConverter.GetBytes(rev), 0, outp, 0, 4);
                Buffer.BlockCopy(blob, 0, outp, 4, blob.Length);
                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, outp);
                File.Move(tmp, path, true);
            }
            catch (Exception e) { Log("cloud write " + c + ": " + e.Message); }
        }

        /// <summary>Delete a player's cloud save (the owner wiped them: the cloud must not restore the old profile).</summary>
        static void CloudDelete(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            try { var path = CloudPath(key); if (File.Exists(path)) File.Delete(path); } catch (Exception e) { Log("cloud delete: " + e.Message); }
        }

        /// <summary>How many cloud saves are stored (owner panel).</summary>
        static int CloudCount()
        {
            try { return Directory.Exists(CloudDir) ? Directory.GetFiles(CloudDir, "*.sav").Length : 0; }
            catch { return 0; }
        }
    }
}
