using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Who you are, in a way nobody else can fake. On first launch this device makes a secret key pair and
    /// keeps it. Your <see cref="PlayerId"/> (like 3F9A2-C01B7, shown in Menu > Menu) comes from the public half.
    /// Moderators sign their commands with their secret half, so a cheater can't pretend to be a mod even if they
    /// copy a mod's name.
    /// </summary>
    public static class PlayerIdentity
    {
        const string KeyPref = "nb.identityKey";
        static RSACryptoServiceProvider rsa;
        static string playerId, deviceHash;
        static byte[] modulus;

        static RSACryptoServiceProvider Rsa
        {
            get
            {
                if (rsa != null) return rsa;
                rsa = new RSACryptoServiceProvider(1024);
                string saved = PlayerPrefs.GetString(KeyPref, "");
                bool loaded = false;
                if (saved.Length > 0)
                {
                    try { rsa.FromXmlString(saved); loaded = true; }
                    catch (Exception) { }
                }
                if (!loaded)
                {
                    PlayerPrefs.SetString(KeyPref, rsa.ToXmlString(true)); // a fresh key is made by the constructor
                    PlayerPrefs.Save();
                }
                return rsa;
            }
        }

        /// <summary>The public half of your key (128 bytes).</summary>
        public static byte[] PublicModulus => modulus ?? (modulus = Rsa.ExportParameters(false).Modulus);

        /// <summary>Your Player ID, e.g. "3F9A2-C01B7". Give it to the game's owner to become a moderator.</summary>
        public static string PlayerId => playerId ?? (playerId = Fingerprint(PublicModulus));

        /// <summary>A short code for this headset (so a ban can survive a new name).</summary>
        public static string DeviceHash => deviceHash ?? (deviceHash = Hex(Sha("nullbound-device:" + SystemInfo.deviceUniqueIdentifier), 6));

        public static string Fingerprint(byte[] publicModulus)
        {
            if (publicModulus == null || publicModulus.Length == 0) return "";
            string hex = Hex(Sha(publicModulus), 5);
            return hex.Substring(0, 5) + "-" + hex.Substring(5, 5);
        }

        public static byte[] Sign(byte[] data) => Rsa.SignData(data, new SHA256CryptoServiceProvider());

        public static bool Verify(byte[] publicModulus, byte[] data, byte[] signature)
        {
            if (publicModulus == null || publicModulus.Length < 64 || signature == null || data == null) return false;
            try
            {
                using (var check = new RSACryptoServiceProvider())
                {
                    check.ImportParameters(new RSAParameters { Modulus = publicModulus, Exponent = new byte[] { 1, 0, 1 } });
                    return check.VerifyData(data, new SHA256CryptoServiceProvider(), signature);
                }
            }
            catch (Exception) { return false; }
        }

        static byte[] Sha(string s) => Sha(Encoding.UTF8.GetBytes(s));
        static byte[] Sha(byte[] b) { using (var h = SHA256.Create()) return h.ComputeHash(b); }

        static string Hex(byte[] bytes, int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count && i < bytes.Length; i++) sb.Append(bytes[i].ToString("X2"));
            return sb.ToString();
        }
    }
}
