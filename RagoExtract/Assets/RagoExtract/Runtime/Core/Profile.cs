using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rago
{
    /// <summary>Persistent player progress: money, stash, loadout, stats, settings. Saved as JSON.</summary>
    [System.Serializable]
    public class Profile
    {
        public static Profile Current;
        public string name = "Operator";
        public int money = 6000;
        public int outfitSeed;
        public int raids, extracts, deaths, kills, xp;
        public List<ItemData> stash = new List<ItemData>();
        public int[] stashAmmo = new int[System.Enum.GetValues(typeof(Caliber)).Length];
        public Loadout loadout = new Loadout();
        public float gripPitch = 0f;
        public int aimVersion;   // 1 = guns follow the controller's aim pose (old grip-angle tweaks reset)
        public bool smoothTurn;
        public float snapAngle = 45f;
        public float volume = 1f;
        public int version = 1;
        public bool freeArmor;
        // callsign: name + a 4-digit tag, so no two operators ever show the same name (Discord-style)
        public int tag;
        public bool nameSet, tutorialDone;
        public int tutorialStep;
        public List<string> friends = new List<string>();   // callsigns you added (menu > FRIENDS)
        public List<string> recent = new List<string>();    // people you've raided with, newest first
        public bool friendsChecked;   // once: drop typed-in friends nobody has ever met (made-up names)
        public string DisplayName => tag > 0 ? name + "#" + tag.ToString("0000") : name;
        public static int NewTagFor(int avoid = -1) { int t; do t = Random.Range(1000, 10000); while (t == avoid); return t; }
        // bunker expansions (Foreman jobs in the Market)
        public int bunkerLevel;                              // how many expansions are built
        public List<int> jobProgress = new List<int>();      // items turned in for the current job, per requirement
        public int genCash;                                  // generator earnings waiting to be collected
        public int medRestockRaid;                           // raid count when the Med Bay was last restocked   // set when you die: a basic helmet + vest wait in the Armor Room if you own none

        static string PathFile => Path.Combine(Application.persistentDataPath, "rago_profile.json");

        public static Profile LoadOrCreate()
        {
            try
            {
                if (File.Exists(PathFile))
                {
                    var p = JsonUtility.FromJson<Profile>(File.ReadAllText(PathFile));
                    if (p != null) { p.Fix(); Current = p; return p; }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] profile load failed, starting fresh: " + e.Message); }
            Current = NewProfile();
            Current.Save();
            return Current;
        }

        void Fix()
        {
            int n = System.Enum.GetValues(typeof(Caliber)).Length;
            if (stashAmmo == null || stashAmmo.Length != n) { var a = new int[n]; if (stashAmmo != null) System.Array.Copy(stashAmmo, a, Mathf.Min(n, stashAmmo.Length)); stashAmmo = a; }
            if (stash == null) stash = new List<ItemData>();
            if (loadout == null) loadout = new Loadout();
            if (jobProgress == null) jobProgress = new List<int>();
            if (tag <= 0) tag = NewTagFor();
            if (loadout.pack == null) loadout.pack = new List<ItemData>();
            if (friends == null) friends = new List<string>();
            if (recent == null) recent = new List<string>();
            if (!friendsChecked)
            {
                friendsChecked = true;
                friends.RemoveAll(f => !recent.Exists(r => r.Trim().ToLowerInvariant() == f.Trim().ToLowerInvariant()));
            }
            stash.RemoveAll(d => d == null || Db.Get(d.id) == null);
            loadout.pack.RemoveAll(d => d == null || Db.Get(d.id) == null);
            if (version < 2)
            {
                // older saves had a single bag type and armour built into the body
                if (loadout.hasPack && string.IsNullOrEmpty(loadout.packId)) loadout.packId = "pack_assault";
                version = 2;
            }
            if (loadout.hasPack && Db.Get<BackpackDef>(loadout.packId) == null) loadout.packId = "pack_assault";
            if (!string.IsNullOrEmpty(loadout.helmet) && Db.Get<ArmorDef>(loadout.helmet) == null) loadout.helmet = "";
            if (!string.IsNullOrEmpty(loadout.vest) && Db.Get<ArmorDef>(loadout.vest) == null) loadout.vest = "";
            if (version < 3)
            {
                // players from before the training existed don't have to sit through it
                if (raids > 0 || extracts > 0) tutorialDone = true;
                version = 3;
            }
        }

        public static Profile NewProfile()
        {
            var p = new Profile();
            p.outfitSeed = Random.Range(1, 99999);
            p.name = "Operator-" + Random.Range(100, 999);
            p.loadout = new Loadout
            {
                pistol = new ItemData("p9"),
                primary = new ItemData("vostok74"),
                util1 = new ItemData("bandage"),
                util2 = new ItemData("medkit"),
                hasPack = true,
                packId = "pack_day",
                pack = new List<ItemData> { new ItemData("bandage") },
                helmet = "", vest = ""   // no armour to start: find it in raids
            };
            p.version = 3;
            p.tag = NewTagFor();
            // a beat-up helmet and a soft vest wait on the stands in the Armor Room
            p.stash.Add(new ItemData("helm_bump"));
            p.stash.Add(new ItemData("vest_soft"));
            p.stash.Add(new ItemData("painkillers"));
            p.stash.Add(new ItemData("breacher"));
            p.stash.Add(new ItemData("vanguard"));
            p.stash.Add(new ItemData("micro_dot"));
            p.stash.Add(new ItemData("reflex_pan"));
            p.stash.Add(new ItemData("stim"));
            p.stashAmmo[(int)Caliber.C9mm] = 240;
            p.stashAmmo[(int)Caliber.C545] = 300;
            p.stashAmmo[(int)Caliber.C556] = 240;
            p.stashAmmo[(int)Caliber.C12g] = 64;
            return p;
        }

        public void Save()
        {
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(this, true)); }
            catch (System.Exception e) { Debug.LogWarning("[Rago] profile save failed: " + e.Message); }
        }

        /// <summary>Pack ammo from the stash for the guns you're bringing (up to 5 mags each).</summary>
        public void PackAmmoForLoadout()
        {
            if (loadout.ammo == null || loadout.ammo.Length != stashAmmo.Length) loadout.ammo = new int[stashAmmo.Length];
            foreach (var d in new[] { loadout.primary, loadout.secondary, loadout.pistol })
            {
                var g = d != null ? Db.Get<GunDef>(d.id) : null;
                if (g == null) continue;
                int want = g.magSize * (g.cls == GunClass.LMG ? 2 : 5);
                int c = (int)g.cal;
                int take = Mathf.Clamp(want - loadout.ammo[c], 0, stashAmmo[c]);
                stashAmmo[c] -= take;
                loadout.ammo[c] += take;
            }
        }

        public void ReturnAmmoToStash()
        {
            if (loadout.ammo == null) return;
            for (int i = 0; i < Mathf.Min(stashAmmo.Length, loadout.ammo.Length); i++) { stashAmmo[i] += loadout.ammo[i]; loadout.ammo[i] = 0; }
        }
    }
}
