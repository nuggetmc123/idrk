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
        public bool smoothTurn;
        public float snapAngle = 45f;
        public float volume = 1f;
        public int version = 1;

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
            if (loadout.pack == null) loadout.pack = new List<ItemData>();
            stash.RemoveAll(d => d == null || Db.Get(d.id) == null);
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
                pack = new List<ItemData> { new ItemData("bandage") }
            };
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
