using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Basic anti-tamper. Nothing on a player's own headset can be made impossible to mod, but this stops the easy route:
    ///
    ///  * SIGNATURE CHECK (Android / Quest): every APK is signed with the developer's key. Someone who unpacks the APK,
    ///    changes it and repacks it has to sign it with THEIR key - so the signature's fingerprint changes. Put your
    ///    fingerprint in Resources/Rago/signature.txt (the game prints it: menu > RECENT, first line) and any copy signed
    ///    with a different key is marked modified.
    ///  * DEBUGGABLE CHECK: a release build that says it's debuggable has been repacked (modding tools turn that on).
    ///  * A modified copy still runs, but ONLINE PLAY IS OFF for it: it can't join other players.
    ///  * Hits from other players are checked too (NetGame): impossible damage, unknown weapons and machine-gun-fast hit
    ///    streams are thrown away, so a modded client can't one-shot people.
    /// </summary>
    public static class AntiCheat
    {
        static bool _checked, _tampered;
        static string _fingerprint = "", _why = "";

        /// <summary>This copy has been changed: no online play.</summary>
        public static bool Tampered { get { Check(); return _tampered; } }
        public static string Why { get { Check(); return _why; } }
        /// <summary>SHA-256 of the APK's signing certificate (Android), "" elsewhere.</summary>
        public static string Fingerprint { get { Check(); return _fingerprint; } }

        static string Expected()
        {
            try
            {
                var t = Resources.Load<TextAsset>("Rago/signature");
                if (t == null) return "";
                foreach (var line in t.text.Split('\n')) { var l = line.Trim(); if (l.Length > 0 && !l.StartsWith("#")) return l.Replace(":", "").ToUpperInvariant(); }
            }
            catch { }
            return "";
        }

        static void Check()
        {
            if (_checked) return;
            _checked = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string pkg = activity.Call<string>("getPackageName");
                    using (var info = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 0x40))   // GET_SIGNATURES
                    {
                        var sigs = info.Get<AndroidJavaObject[]>("signatures");
                        if (sigs != null && sigs.Length > 0)
                        {
                            var bytes = sigs[0].Call<byte[]>("toByteArray");
                            using (var sha = System.Security.Cryptography.SHA256.Create())
                                _fingerprint = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                        }
                    }
                    using (var ai = activity.Call<AndroidJavaObject>("getApplicationInfo"))
                    {
                        int flags = ai.Get<int>("flags");
                        if ((flags & 2) != 0 && !Debug.isDebugBuild) { _tampered = true; _why = "repacked as debuggable"; }   // FLAG_DEBUGGABLE
                    }
                }
                string want = Expected();
                if (want.Length > 0 && _fingerprint.Length > 0 && !string.Equals(want, _fingerprint, StringComparison.OrdinalIgnoreCase)) { _tampered = true; _why = "signed with a different key"; }
                Debug.Log("[Rago] build signature " + _fingerprint + (want.Length == 0 ? " (not pinned yet - put it in Resources/Rago/signature.txt)" : _tampered ? " - MODIFIED: " + _why : " - ok"));
            }
            catch (Exception e) { Debug.LogWarning("[Rago] signature check skipped: " + e.Message); }
#endif
        }

        // ------------------------------------------------------------------------------------------- incoming hits
        static readonly Dictionary<ulong, (float t, int n)> _rate = new Dictionary<ulong, (float, int)>();

        /// <summary>Is a hit another player says they landed on us believable? (Damage within what that weapon can do,
        /// a known weapon, and not more than ~25 hits a second from one player.)</summary>
        public static bool HitOk(ulong from, float dmg, string gunId, BodyPart part)
        {
            if (float.IsNaN(dmg) || float.IsInfinity(dmg) || dmg < 0f) return false;
            float now = Time.realtimeSinceStartup;
            if (!_rate.TryGetValue(from, out var r) || now - r.t > 1f) r = (now, 0);
            r.n++; _rate[from] = r;
            if (r.n > 25) return false;   // shotguns put out ~9 pellets a shot; nothing legit lands 25+ hits a second
            float cap;
            var def = string.IsNullOrEmpty(gunId) ? null : Db.Get(gunId);
            if (def is GunDef g) cap = g.damage * Ballistics.DamageScale * 3.5f + 5f;            // headshot multipliers and a margin
            else if (def is SimpleDef sd && sd.kind == ItemKind.Melee) cap = Mathf.Max(45f, sd.heal) * 1.8f * 1.5f * 1.2f;
            else if (gunId == "cart") cap = 100f;
            else if (gunId == "frag" || gunId == "impact") cap = 260f;
            else cap = 150f;   // anything else (fire, falls...): generous but not instant
            return dmg <= cap;
        }
    }
}
