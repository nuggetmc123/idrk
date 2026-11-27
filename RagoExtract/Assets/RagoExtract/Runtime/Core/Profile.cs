using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rago
{
    /// <summary>An item left on the bunker floor (or a table, a shelf top...) and exactly where it was.</summary>
    [System.Serializable]
    public class FloorItem { public ItemData item; public Vector3 pos; public Quaternion rot = Quaternion.identity; }
    [System.Serializable] public class MailItem { public ItemData item; public string note = ""; public long time; }   // the bunker's DELIVERIES screen (item null = just a message, e.g. "SOLD ... for $")

    /// <summary>Persistent player progress: money, stash, loadout, stats, settings. Saved as JSON.</summary>
    [System.Serializable]
    public class Profile
    {
        public static Profile Current;
        /// <summary>The current season (1 = the first real one; everything before it was the test period). Raise it to wipe
        /// every account (money, level, stash, gear, bunker) the next time each player opens the game - paid DLC, callsign,
        /// friends and settings are kept. Saves from the test period have season 0.</summary>
        public const int Season = 3;   // 2: full wipe in 3.06; 3: full wipe + skins (except the beta skin) in 3.22
        public int season;
        public int dlcPaidCheck;   // 1 = packs claimed for free in the test period were taken back (bought ones come back from the store)
        public static bool SeasonWiped;   // this launch wiped the account: say so once you're in
        public string name = "Operator";
        public int money = 6000;
        public int outfitSeed;
        public bool colorLocked;
        public int quality = 1;    // graphics: 0 low, 1 medium, 2 high (how far grass, bushes and trees are drawn, shadows)   // your colour was fixed when you picked your callsign: it never changes after that
        public int raids, extracts, deaths, kills, xp;
        public string look = "";   // clothes picked at the bunker wardrobe (Cosmetics.Encode)
        public List<ItemData> stash = new List<ItemData>();
        public List<FloorItem> bunkerFloor = new List<FloorItem>();
        public int deliverN;   // how many things have been delivered: each lands on the next spot of the Armory floor   // things left lying around the bunker: they stay exactly where you put them
        public int[] stashAmmo = new int[System.Enum.GetValues(typeof(Caliber)).Length];
        public int[] stashAp = new int[System.Enum.GetValues(typeof(Caliber)).Length], stashInc = new int[System.Enum.GetValues(typeof(Caliber)).Length];
        public Loadout loadout = new Loadout();
        public float gripPitch = 0f;
        public int aimVersion;   // 1 = guns follow the controller's aim pose (old grip-angle tweaks reset)
        public bool smoothTurn;
        public bool autoSprint, seated, autoMag, bodyLookDown, virtualStock, turnOff;   // virtualStock / turnOff: 3.55   // bodyLookDown (3.52): keep your own body shown when you look down   // autoMag (3.51): a mag held near the magwell loads itself          // comfort: sprint on its own when you push fully forward; play sitting down
        public float heightCal, seatCal = -1f;   // height calibration (metres added to your view): standing, and seated (-1 = not done: a default lift)
        public float snapAngle = 45f;
        public float volume = 1f, volEffects = 1f, volAmbience = 1f, volVoice = 1f;   // master, effects, weather & ambience, voice chat
        public int version = 1;
        public bool freeArmor;
        public float pugBowl = 0.5f;
        public List<ShopEntry> shop = new List<ShopEntry>(); public int shopNextId; public bool shopOpen;   // your black market shop (items listed, with a price or what you want)   // how full the bunker pug's bowl is (0..1)
        // callsign: name + a 4-digit tag, so no two operators ever show the same name (Discord-style)
        public int tag;
        public bool nameSet, tutorialDone;
        public bool inRaid;   // set while a raid is on: still set at the next launch = you quit mid-raid, which counts as dying
        public int tutorialStep;
        public List<MailItem> mailbox = new List<MailItem>();   // DELIVERIES screen in the bunker: bought / traded / sold
        public bool creator;   // a content creator: the owner gave them the CC trophy (red name tape and a CC badge)
        public List<string> skins = new List<string>();      // gun skins won on the Market's Skin Wheel
        public List<string> dlc = new List<string>();       // DLC STORE packs bought (and "northpole": the South Pole vault)
        public List<string> friends = new List<string>();   // callsigns you added (menu > FRIENDS)
        public List<string> pendingFriends = new List<string>();   // typed in but not seen online yet: "CALLSIGN#1234|day added"
        public List<string> recent = new List<string>();    // people you've raided with, newest first
        public bool friendsChecked, friendsChecked2;   // drop typed-in friends nobody has ever met (made-up names)
        public string DisplayName => tag > 0 ? name + "#" + tag.ToString("0000") : name;
        public static int NewTagFor(int avoid = -1) { int t; do t = Random.Range(1000, 10000); while (t == avoid); return t; }
        // bunker expansions (Foreman jobs in the Market)
        public int bunkerLevel;                              // how many expansions are built
        public List<int> jobProgress = new List<int>();      // items turned in for the current job, per requirement
        public int genCash;                                  // generator earnings waiting to be collected
        public List<ItemData> medCabinet = new List<ItemData>(); public bool medStocked;   // the Med Bay cabinet's own contents
        public int medRestockRaid;                           // raid count when the Med Bay was last restocked   // set when you die: a basic helmet + vest wait in the Armor Room if you own none

        static string PathFile => Path.Combine(Application.persistentDataPath, "rago_profile.json");

        static string BackupFile => PathFile + ".bak";

        static Profile TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) return null;
                var p = JsonUtility.FromJson<Profile>(text);
                return p != null && p.stash != null ? p : null;
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] couldn't read " + path + ": " + e.Message); return null; }
        }

        /// <summary>Moderation / season WIPE: progress back to a fresh start (money, stash, gear, level, bunker). Your callsign,
        /// clothes, settings, friends, DLC packs and ALL your gun skins (packs, Skin Wheel, South Pole vault) stay - keep it
        /// that way: never clear <c>skins</c> or <c>dlc</c> here.</summary>
        public void Wipe()
        {
            money = 6000; xp = 0; raids = extracts = deaths = kills = 0;
            stash = new List<ItemData>(); bunkerFloor = new List<FloorItem>(); deliverN = 0;
            int n = System.Enum.GetValues(typeof(Caliber)).Length;
            stashAmmo = new int[n]; stashAp = new int[n]; stashInc = new int[n];
            loadout = new Loadout(); freeArmor = false;
            shop = new List<ShopEntry>(); shopNextId = 0; shopOpen = false;
            bunkerLevel = 0; jobProgress = new List<int>(); genCash = 0;
            medCabinet = new List<ItemData>(); medStocked = false; medRestockRaid = 0;
            mailbox = new List<MailItem>();
            inRaid = false;
            // the same starter kit a brand-new account gets (guns, meds, armour on the stands, ammo) - not an empty bunker
            var fresh = NewProfile();
            loadout = fresh.loadout;
            stash = fresh.stash;
            stashAmmo = fresh.stashAmmo; stashAp = fresh.stashAp; stashInc = fresh.stashInc;
            version = fresh.version;
            Fix();
            Save();
        }

        public static Profile LoadOrCreate()
        {
            // the profile, or if that's damaged (the game was closed mid-save), the backup - never a silent reset
            var p = TryRead(PathFile) ?? TryRead(BackupFile);
            if (p != null)
            {
                p.Fix(); Current = p;
                // each one-time fix is marked done even if it goes wrong, so it can never block start-up again
                if (p.season < Season)
                {
                    // a new season: everyone starts over with the starter kit. Season 3 also takes the gun skins (Skin Wheel
                    // wins, DLC / South Pole unlocks) - except the BETA TESTER skin. Packs bought with real money come straight
                    // back from the store at start-up (Dlc.Restore). Each step on its own: one going wrong never stops the game
                    try { p.Wipe(); SeasonWiped = true; }
                    catch (System.Exception e) { Debug.LogError("[Rago] season wipe: " + e); }
                    try
                    {
                        if (p.skins != null) p.skins.RemoveAll(sk => sk != "beta");
                        if (p.dlc != null) p.dlc.RemoveAll(d => d != "beta");
                    }
                    catch (System.Exception e) { Debug.LogError("[Rago] season wipe: " + e); }
                    p.season = Season;
                    try { p.Save(); } catch { }
                }
                if (p.dlcPaidCheck < 1)
                {
                    // the packs were free during the test period: those claims go. Packs really bought come straight back from
                    // the store at start-up (Dlc.Restore), so nobody loses one they paid for
                    try { p.dlc?.RemoveAll(id => Dlc.Get(id) != null); } catch (System.Exception e) { Debug.LogError("[Rago] dlc check: " + e); }
                    p.dlcPaidCheck = 1;
                    try { p.Save(); } catch { }
                }
                return p;
            }
            if (File.Exists(PathFile))
                try { File.Copy(PathFile, PathFile + ".damaged", true); } catch { }   // keep the broken one, just in case
            Current = NewProfile();
            Current.Save();
            return Current;
        }

        void Fix()
        {
            int n = System.Enum.GetValues(typeof(Caliber)).Length;
            if (stashAmmo == null || stashAmmo.Length != n) { var a = new int[n]; if (stashAmmo != null) System.Array.Copy(stashAmmo, a, Mathf.Min(n, stashAmmo.Length)); stashAmmo = a; }
            if (stashAp == null || stashAp.Length != n) { var a = new int[n]; if (stashAp != null) System.Array.Copy(stashAp, a, Mathf.Min(n, stashAp.Length)); stashAp = a; }
            if (stashInc == null || stashInc.Length != n) { var a = new int[n]; if (stashInc != null) System.Array.Copy(stashInc, a, Mathf.Min(n, stashInc.Length)); stashInc = a; }
            if (stash == null) stash = new List<ItemData>();
            if (skins == null) skins = new List<string>();
            // given the CC trophy before the badge existed: still a creator
            if (!creator && (stash.Exists(d => d != null && d.id == "cc_trophy") || (mailbox != null && mailbox.Exists(m => m != null && m.item != null && m.item.id == "cc_trophy")) || (bunkerFloor != null && bunkerFloor.Exists(f => f != null && f.item != null && f.item.id == "cc_trophy")))) creator = true;
            if (loadout == null) loadout = new Loadout();
            if (jobProgress == null) jobProgress = new List<int>();
            if (tag <= 0) tag = NewTagFor();
            if (loadout.pack == null) loadout.pack = new List<ItemData>();
            if (friends == null) friends = new List<string>();
            if (pendingFriends == null) pendingFriends = new List<string>();
            if (recent == null) recent = new List<string>();
            if (!friendsChecked || !friendsChecked2)
            {
                friendsChecked = friendsChecked2 = true;
                friends.RemoveAll(f => !recent.Exists(r => r.Trim().ToLowerInvariant() == f.Trim().ToLowerInvariant()));
            }
            // the game was closed in the middle of a raid: the ammo packed for it goes back to the reserve
            if (loadout.ammo != null) ReturnAmmoToStash();
            loadout.ammo = null;
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
            if (!colorLocked && nameSet) LockColor();   // players from before: their colour is fixed now, from their callsign
            if (version < 3)
            {
                // players from before the training existed don't have to sit through it
                if (raids > 0 || extracts > 0) tutorialDone = true;
                version = 3;
            }
        }

        /// <summary>
        /// Give this operator their own colour, for good: worked out from their callsign (name + tag), which nobody
        /// else has, so every operator gets a different shade - and everyone sees the same colour for them.
        /// </summary>
        public void LockColor()
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in DisplayName.ToLowerInvariant()) h = (h ^ c) * 16777619;
                outfitSeed = (h & 0x7fffffff) % 9999991 + 1;
            }
            colorLocked = true;
        }

        public static Profile NewProfile()
        {
            var p = new Profile();
            p.season = Season; p.dlcPaidCheck = 1;
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

        public static event System.Action MailChanged;

        /// <summary>
        /// Something you bought, traded for or got back: it waits on the DELIVERIES screen in your bunker (west wall of the
        /// main hall) until you press TAKE there.
        /// </summary>
        public static void Deliver(ItemData d, string note = null)
        {
            var p = Current;
            if (p == null || d == null) return;
            p.mailbox ??= new List<MailItem>();
            p.mailbox.Add(new MailItem { item = d, note = note ?? "Delivered", time = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            p.Save();
            MailChanged?.Invoke();
        }

        /// <summary>Just a message on the DELIVERIES screen (a sale: the money is already yours).</summary>
        public static void Notify(string note)
        {
            var p = Current; if (p == null) return;
            p.mailbox ??= new List<MailItem>();
            p.mailbox.Add(new MailItem { item = null, note = note, time = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            while (p.mailbox.FindAll(m => m.item == null).Count > 20) p.mailbox.Remove(p.mailbox.Find(m => m.item == null));
            p.Save();
            MailChanged?.Invoke();
        }

        /// <summary>The old way (before 2.62): on the Armory floor.</summary>
        public static void DeliverToFloor(ItemData d)
        {
            var p = Current;
            if (p == null || d == null) return;
            int i = p.deliverN++ % 20;
            var pos = new Vector3(-1.1f + (i % 5) * 0.55f, 0.35f, 8.6f + (i / 5) * 0.55f);   // Armory: x -6..6, z 6..13
            var g = RagoGame.I;
            if (g != null && g.Area == Area.Bunker && g.Map is BunkerMap bm && Application.isPlaying)
            {
                try { ItemFactory.Spawn(d, bm.transform.TransformPoint(pos), Quaternion.Euler(0, 90, 0)); return; }
                catch (System.Exception e) { Debug.LogWarning("[Rago] delivery: " + e.Message); }
            }
            if (p.bunkerFloor == null) p.bunkerFloor = new List<FloorItem>();
            p.bunkerFloor.Add(new FloorItem { item = d, pos = pos, rot = Quaternion.Euler(0, 90, 0) });
        }

        public void Save()
        {
            // write a temp file and swap it in, keeping the last good one as a backup: a save cut off half way (the app
            // closed, the headset slept) can never leave a broken profile that loads as a brand-new one
            try
            {
                string tmp = PathFile + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(this, true));
                if (File.Exists(PathFile)) { File.Copy(PathFile, BackupFile, true); File.Delete(PathFile); }
                File.Move(tmp, PathFile);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] profile save failed: " + e.Message); }
        }

        /// <summary>Pack ammo from the stash for the guns you're bringing: ALL of it for each gun's calibre (it used to be
        /// only 5 mags' worth). What you don't use comes home with you when you extract.</summary>
        public void PackAmmoForLoadout()
        {
            if (loadout.ammo == null || loadout.ammo.Length != stashAmmo.Length) loadout.ammo = new int[stashAmmo.Length];
            foreach (var d in new[] { loadout.primary, loadout.secondary, loadout.pistol })
            {
                var g = d != null ? Db.Get<GunDef>(d.id) : null;
                if (g == null) continue;
                int c = (int)g.cal;
                int take = Mathf.Max(0, stashAmmo[c]);
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
