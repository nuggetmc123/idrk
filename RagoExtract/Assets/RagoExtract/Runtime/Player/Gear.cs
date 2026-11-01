using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Body-mounted gear, Ghost-of-Tabor style:
    ///  - over right shoulder: primary long gun
    ///  - behind lower back: secondary long gun
    ///  - right hip: pistol holster
    ///  - left hip / left chest: utility pouches (meds, keys, sights, valuables)
    ///  - centre chest: magazine pouch (pulls a fresh mag for the gun in your other hand from your ammo reserve)
    ///  - over left shoulder: backpack (pull it off to open it)
    ///  - head / chest: helmet and vest. No armour at the start: find it in raids, bring it to your
    ///    head or chest and let go to wear it; grab the top of your head / your collar to take it off.
    /// </summary>
    public class Gear : MonoBehaviour
    {
        PlayerRig _p;
        public Transform Root;
        public Slot Primary, Secondary, Pistol, Util1, Util2, PackSlot;
        public Backpack Pack;
        public ArmorDef Helmet, Vest;
        public System.Action OnGearChanged;
        public int[] Ammo = new int[System.Enum.GetValues(typeof(Caliber)).Length];
        float _yaw;

        public void Init(PlayerRig p)
        {
            _p = p;
            Root = new GameObject("GearRoot").transform;
            DontDestroyOnLoad(Root.gameObject);
            Primary = MakeSlot("Primary", new Vector3(0.1f, -0.38f, -0.2f), Quaternion.Euler(0, 0, 55) * Quaternion.Euler(-90, 0, 0), it => it is Gun g && g.g.TwoHanded, 0.33f, 0f);
            Secondary = MakeSlot("Secondary", new Vector3(0f, -0.72f, -0.2f), Quaternion.Euler(0, 90, 0) * Quaternion.Euler(0, 0, 180), it => it is Gun g && g.g.TwoHanded, 0.28f, 0f);
            Pistol = MakeSlot("Holster", new Vector3(0.22f, -0.78f, 0.02f), Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 0), it => it is Gun g && !g.g.TwoHanded, 0.16f, 0f);
            Util1 = MakeSlot("PouchL", new Vector3(-0.22f, -0.76f, 0.04f), Quaternion.identity, it => !(it is Gun) && !(it is Backpack) && !(it is ArmorItem), 0.13f, 0.11f);
            Util2 = MakeSlot("PouchChest", new Vector3(-0.13f, -0.38f, 0.17f), Quaternion.identity, it => !(it is Gun) && !(it is Backpack) && !(it is ArmorItem), 0.11f, 0.09f);
            PackSlot = MakeSlot("PackSlot", new Vector3(-0.16f, -0.32f, -0.2f), Quaternion.identity, it => it is Backpack, 0.3f, 0.02f);
            // holster visual
            var hb = new MB();
            hb.Box(new Vector3(0, -0.05f, 0.03f), new Vector3(0.05f, 0.18f, 0.12f), new Color(0.12f, 0.12f, 0.11f), 0.012f, 2);
            var hv = hb.Build("HolsterMesh", Pistol.transform, Mats.Polymer);
            hv.transform.localRotation = Quaternion.Inverse(Pistol.transform.localRotation);
        }

        Slot MakeSlot(string name, Vector3 local, Quaternion storeRot, System.Func<Item, bool> filter, float radius, float fit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = local;
            var s = go.AddComponent<Slot>();
            s.filter = filter; s.radius = radius; s.fit = fit; s.storeRot = storeRot;
            return s;
        }

        public void Tick()
        {
            if (_p.Dead) return;
            // gear follows the torso (head position, smoothed yaw)
            float headYaw = _p.Head.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, headYaw)) > 30f || _p.MoveVelocity.sqrMagnitude > 0.3f) _yaw = Mathf.MoveTowardsAngle(_yaw, headYaw, Time.deltaTime * 240f);
            Root.position = _p.Head.position;
            Root.rotation = Quaternion.Euler(0, _yaw, 0);
            // shoulder slots are grabbed from over the shoulder, so their store point follows head height closely
        }

        Vector3 ChestPouch => Root.TransformPoint(new Vector3(0.0f, -0.42f, 0.2f));
        Vector3 LeftShoulder => Root.TransformPoint(new Vector3(-0.18f, -0.15f, -0.08f));
        Vector3 RightShoulder => Root.TransformPoint(new Vector3(0.18f, -0.15f, -0.08f));
        Vector3 LowerBack => Root.TransformPoint(new Vector3(0f, -0.7f, -0.18f));

        Vector3 HeadTop => _p.Head.position + Vector3.up * 0.12f;
        Vector3 Collar => Root.TransformPoint(new Vector3(0f, -0.24f, 0.14f));
        Vector3 ChestCenter => Root.TransformPoint(new Vector3(0f, -0.4f, 0.08f));

        public bool NearChestPouch(Vector3 p) => Vector3.Distance(p, ChestPouch) < 0.17f;

        // ---------------------------------------------------------------------- armour
        /// <summary>Wear an armour item released near your head (helmet) or chest (vest). Swaps out what you had on.</summary>
        public bool TryWear(ArmorItem a, Hand h)
        {
            bool helm = a.ad.slot == ArmorSlot.Helmet;
            Vector3 target = helm ? _p.Head.position + Vector3.up * 0.04f : ChestCenter;
            if (Vector3.Distance(a.transform.position, target) > (helm ? 0.3f : 0.32f)) return false;
            var old = helm ? Helmet : Vest;
            if (helm) Helmet = a.ad; else Vest = a.ad;
            if (old != null)
            {
                var o = ItemFactory.Spawn(old.id, a.transform.position, a.transform.rotation);
                if (o != null) o.Drop(Vector3.zero, Vector3.zero);
            }
            Destroy(a.gameObject);
            Sfx.Play(Synth.Zipper(), target, 0.5f, helm ? 1.3f : 0.9f, 0.5f, 6f);
            h.Haptic(0.4f, 0.1f);
            _p.Toast(a.ad.name + " on  (-" + Mathf.RoundToInt(a.ad.protection * 100) + "% " + (helm ? "head" : "chest") + " damage)");
            Changed();
            return true;
        }

        /// <summary>Takes worn armour off into the hand.</summary>
        public Item TakeOff(ArmorSlot slot, Vector3 p, Quaternion rot)
        {
            var d = slot == ArmorSlot.Helmet ? Helmet : Vest;
            if (d == null) return null;
            if (slot == ArmorSlot.Helmet) Helmet = null; else Vest = null;
            var it = ItemFactory.Spawn(d.id, p, rot);
            Sfx.Play(Synth.Zipper(), p, 0.45f, 1.1f, 0.5f, 6f);
            Changed();
            return it;
        }

        public void Changed() { OnGearChanged?.Invoke(); }

        /// <summary>Called when a hand grips empty air near your body.</summary>
        public Item TakeFromBody(Hand h)
        {
            Vector3 p = h.Anchor.position;
            // magazine pouch
            if (NearChestPouch(p))
            {
                var gun = h.Other.Held as Gun;
                if (gun != null && !h.Other.HeldAsSecondary)
                {
                    int have = Ammo[(int)gun.g.cal];
                    if (have <= 0) { _p.Toast("No " + Db.CalName(gun.g.cal) + " left"); Sfx.UI(Synth.UiError(), 0.3f); return null; }
                    int n = Mathf.Min(have, gun.g.magSize);
                    Ammo[(int)gun.g.cal] -= n;
                    var m = Magazine.Create(gun, n, p, h.Anchor.rotation);
                    Sfx.Play(Synth.Zipper(), p, 0.35f, 1.4f, 0.5f, 5f);
                    return m;
                }
            }
            if (Helmet != null && Vector3.Distance(p, HeadTop) < 0.13f) return TakeOff(ArmorSlot.Helmet, p, h.Anchor.rotation);
            if (Vest != null && Vector3.Distance(p, Collar) < 0.1f && Util2.item == null) return TakeOff(ArmorSlot.Vest, p, h.Anchor.rotation);
            if (Vector3.Distance(p, LeftShoulder) < 0.2f && PackSlot.item != null) return PackSlot.item;
            if (Vector3.Distance(p, RightShoulder) < 0.22f && Primary.item != null) return Primary.item;
            if (Vector3.Distance(p, LowerBack) < 0.2f && Secondary.item != null) return Secondary.item;
            return null;
        }

        public void AddAmmo(Caliber c, int n) { Ammo[(int)c] += n; _p.Toast("+" + n + " " + Db.CalName(c)); }

        // ---------------------------------------------------------------------- persistence
        public Loadout SaveLoadout()
        {
            var l = new Loadout();
            l.primary = Primary.item != null ? Primary.item.Save() : null;
            l.secondary = Secondary.item != null ? Secondary.item.Save() : null;
            l.pistol = Pistol.item != null ? Pistol.item.Save() : null;
            l.util1 = Util1.item != null ? Util1.item.Save() : null;
            l.util2 = Util2.item != null ? Util2.item.Save() : null;
            bool packOnMe = Pack != null && (Pack.slot == PackSlot || Pack.primaryHand == _p.HandL || Pack.primaryHand == _p.HandR);
            l.hasPack = packOnMe;
            l.packId = packOnMe ? Pack.def.id : "";
            l.pack = packOnMe ? Pack.Container.Save() : new List<ItemData>();
            l.helmet = Helmet != null ? Helmet.id : "";
            l.vest = Vest != null ? Vest.id : "";
            // items held in hands go to the best matching slot
            foreach (var hand in new[] { _p.HandL, _p.HandR })
            {
                if (hand.Held == null || hand.HeldAsSecondary || hand.Held is Backpack) continue;
                if (hand.Held is ArmorItem ai)
                {
                    // armour in your hand: put it on if that slot is free, otherwise into the pack
                    if (ai.ad.slot == ArmorSlot.Helmet && string.IsNullOrEmpty(l.helmet)) l.helmet = ai.ad.id;
                    else if (ai.ad.slot == ArmorSlot.Vest && string.IsNullOrEmpty(l.vest)) l.vest = ai.ad.id;
                    else l.pack.Add(hand.Held.Save());
                    continue;
                }
                var d = hand.Held.Save();
                if (hand.Held is Gun g)
                {
                    if (g.g.TwoHanded) { if (l.primary == null) l.primary = d; else if (l.secondary == null) l.secondary = d; else l.pack.Add(d); }
                    else if (l.pistol == null) l.pistol = d; else l.pack.Add(d);
                }
                else if (hand.Held is Magazine mag) Ammo[(int)mag.cal] += mag.rounds;
                else if (l.util1 == null) l.util1 = d; else if (l.util2 == null) l.util2 = d; else l.pack.Add(d);
            }
            l.ammo = (int[])Ammo.Clone();
            return l;
        }

        public void ClearAll()
        {
            bool packHeld = Pack != null && (Pack.primaryHand == _p.HandL || Pack.primaryHand == _p.HandR);
            _p.HandL.ForceRelease(); _p.HandR.ForceRelease();
            foreach (var s in new[] { Primary, Secondary, Pistol, Util1, Util2, PackSlot })
                if (s.item != null) { var it = s.item; s.Release(it); Destroy(it.gameObject); }
            // a bag you put down (in the stash, on the floor) stays where it is
            if (Pack != null && packHeld) Destroy(Pack.gameObject);
            Pack = null;
            Helmet = null; Vest = null;
            for (int i = 0; i < Ammo.Length; i++) Ammo[i] = 0;
            Changed();
        }

        public void LoadLoadout(Loadout l)
        {
            ClearAll();
            if (l == null) return;
            Put(Primary, l.primary); Put(Secondary, l.secondary); Put(Pistol, l.pistol); Put(Util1, l.util1); Put(Util2, l.util2);
            if (l.hasPack)
            {
                Pack = Backpack.Create(Root.position, Db.Get<BackpackDef>(string.IsNullOrEmpty(l.packId) ? "pack_assault" : l.packId));
                PackSlot.Store(Pack, false);
                Pack.Fill(l.pack);
            }
            Helmet = string.IsNullOrEmpty(l.helmet) ? null : Db.Get<ArmorDef>(l.helmet);
            Vest = string.IsNullOrEmpty(l.vest) ? null : Db.Get<ArmorDef>(l.vest);
            Changed();
            if (l.ammo != null) for (int i = 0; i < Mathf.Min(Ammo.Length, l.ammo.Length); i++) Ammo[i] = l.ammo[i];
        }

        void Put(Slot s, ItemData d)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) return;
            var it = ItemFactory.Spawn(d, s.transform.position, s.transform.rotation);
            if (it == null) return;
            it.transform.SetParent(null, true);
            if (s.Accepts(it)) s.Store(it, false); else Destroy(it.gameObject);
        }

        /// <summary>Everything you carry (for death drops / extraction value).</summary>
        public List<ItemData> AllCarried()
        {
            var l = SaveLoadout();
            var all = new List<ItemData>();
            foreach (var d in new[] { l.primary, l.secondary, l.pistol, l.util1, l.util2 }) if (d != null) all.Add(d);
            if (l.hasPack && !string.IsNullOrEmpty(l.packId)) all.Add(new ItemData(l.packId) { contents = Backpack.Pack(l.pack) });
            else all.AddRange(l.pack);
            if (!string.IsNullOrEmpty(l.helmet)) all.Add(new ItemData(l.helmet));
            if (!string.IsNullOrEmpty(l.vest)) all.Add(new ItemData(l.vest));
            return all;
        }

        // ---------------------------------------------------------------------- desktop helpers
        public void DesktopReload(Gun gun)
        {
            if (gun.Internal)
            {
                int have = Ammo[(int)gun.g.cal];
                int need = gun.g.magSize - gun.internalRounds;
                int n = Mathf.Min(have, need);
                if (n <= 0) { _p.Toast(have <= 0 ? "No ammo" : "Full"); return; }
                Ammo[(int)gun.g.cal] -= n;
                var m = Magazine.Create(gun, n, gun.transform.position, gun.transform.rotation);
                gun.InsertMag(m);
                if (!gun.chambered) gun.Rack();
                return;
            }
            if (gun.mag != null) gun.EjectMag();
            int have2 = Ammo[(int)gun.g.cal];
            if (have2 <= 0) { _p.Toast("No " + Db.CalName(gun.g.cal) + " left"); return; }
            int k = Mathf.Min(have2, gun.g.magSize);
            Ammo[(int)gun.g.cal] -= k;
            var mag = Magazine.Create(gun, k, gun.transform.position, gun.transform.rotation);
            gun.InsertMag(mag);
            if (!gun.chambered) gun.Rack();
        }

        public void DesktopEquip(int idx)
        {
            var s = idx == 0 ? Primary : idx == 1 ? Secondary : Pistol;
            var h = _p.HandR;
            if (h.Held != null)
            {
                var cur = h.Held;
                h.ForceRelease(true);
                var dest = Slot.FindFor(cur, cur.transform.position);
                if (cur is Gun g) dest = g.g.TwoHanded ? (Primary.item == null ? Primary : Secondary.item == null ? Secondary : null) : (Pistol.item == null ? Pistol : null);
                if (dest != null) dest.Store(cur); else cur.Drop(Vector3.zero, Vector3.zero);
                if (_p.HandL.HeldAsSecondary) _p.HandL.ForceRelease();
                if (cur.slot == s) { Desktop.RightGripToggle = false; return; }
            }
            if (s.item != null) { h.Grab(s.item); Desktop.RightGripToggle = true; }
        }

        public void DesktopBackpack()
        {
            var h = _p.HandL;
            if (h.Held is Backpack bp) { h.ForceRelease(true); PackSlot.Store(bp); Desktop.LeftGripToggle = false; return; }
            if (PackSlot.item != null && h.Held == null) { h.Grab(PackSlot.item); Desktop.LeftGripToggle = true; }
        }

        /// <summary>Desktop H: wear the armour in either hand, or take off the helmet, then the vest.</summary>
        public void DesktopArmor()
        {
            foreach (var hand in new[] { _p.HandR, _p.HandL })
                if (hand.Held is ArmorItem a && !hand.HeldAsSecondary)
                {
                    hand.ForceRelease(true);
                    a.transform.position = a.ad.slot == ArmorSlot.Helmet ? _p.Head.position : ChestCenter;
                    if (!TryWear(a, hand)) a.Drop(Vector3.zero, Vector3.zero);
                    if (hand == _p.HandR) Desktop.RightGripToggle = false; else Desktop.LeftGripToggle = false;
                    return;
                }
            var free = _p.HandR.Held == null ? _p.HandR : _p.HandL.Held == null ? _p.HandL : null;
            if (free == null) { _p.Toast("Free a hand first"); return; }
            var it = Helmet != null ? TakeOff(ArmorSlot.Helmet, free.Anchor.position, free.Anchor.rotation)
                   : Vest != null ? TakeOff(ArmorSlot.Vest, free.Anchor.position, free.Anchor.rotation) : null;
            if (it == null) { _p.Toast("No armour on"); return; }
            free.Grab(it);
            if (free == _p.HandR) Desktop.RightGripToggle = true; else Desktop.LeftGripToggle = true;
        }
    }

    [System.Serializable]
    public class Loadout
    {
        public ItemData primary, secondary, pistol, util1, util2;
        public bool hasPack = true;
        public string packId = "";                 // which backpack (BackpackDef id)
        public List<ItemData> pack = new List<ItemData>();
        public string helmet = "", vest = "";      // worn armour (ArmorDef ids, "" = none)
        public int[] ammo;
    }

    [System.Serializable]
    public class ItemList { public List<ItemData> items = new List<ItemData>(); }

    /// <summary>A backpack: pull it off your left shoulder and it opens into a grid of pockets (size depends on the bag).
    /// It keeps its contents wherever it goes - on your back, in the stash, on the ground or on a body.</summary>
    public class Backpack : Item
    {
        public ItemContainer Container;
        public BackpackDef bd;
        const float Cell = 0.11f;

        public static Backpack Create(Vector3 pos, BackpackDef d)
        {
            if (d == null) d = Db.Packs.Count > 0 ? Db.Packs[0] : new BackpackDef { id = "pack_day", name = "Day Pack", kind = ItemKind.Backpack, cols = 2, rows = 2, color = new Color(0.3f, 0.32f, 0.26f) };
            var go = new GameObject(d.name);
            go.transform.position = pos;
            var bp = go.AddComponent<Backpack>();
            bp.bd = d; bp.def = d;
            float w = d.cols * Cell + 0.04f, dep = d.rows * Cell + 0.04f;
            var mb = new MB(2);
            Color c = d.color, c2 = d.color * 0.85f, dk = new Color(0.18f, 0.18f, 0.16f);
            c2.a = 1;
            mb.Sub(0).Box(new Vector3(0, -0.02f, 0), new Vector3(w, 0.12f, dep), c, 0.04f, 2);
            mb.Sub(0).Box(new Vector3(0, -0.02f, dep * 0.5f + 0.015f), new Vector3(w * 0.82f, 0.09f, 0.04f), c2, 0.02f, 1);
            mb.Sub(1).Box(new Vector3(w * 0.28f, -0.085f, 0), new Vector3(0.04f, 0.01f, dep - 0.02f), dk, 0.004f);
            mb.Sub(1).Box(new Vector3(-w * 0.28f, -0.085f, 0), new Vector3(0.04f, 0.01f, dep - 0.02f), dk, 0.004f);
            if (d.style >= 2)
            {
                mb.Sub(0).Box(new Vector3(w * 0.5f + 0.025f, -0.025f, 0), new Vector3(0.05f, 0.09f, dep * 0.6f), c2, 0.015f);
                mb.Sub(0).Box(new Vector3(-w * 0.5f - 0.025f, -0.025f, 0), new Vector3(0.05f, 0.09f, dep * 0.6f), c2, 0.015f);
            }
            if (d.style >= 3) for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(0, -0.045f + i * 0.025f, dep * 0.5f + 0.037f), new Vector3(w * 0.7f, 0.008f, 0.004f), dk, 0.001f);
            // interior floor (dark) to read as an open bag
            mb.Sub(1).Box(new Vector3(0, 0.041f, 0), new Vector3(w - 0.04f, 0.002f, dep - 0.04f), dk * 0.6f, 0);
            mb.Build("Model", go.transform, Mats.Lit("pack_cloth", Color.white, 0f, 0.15f, Mats.Fabric, Mats.FabricN, 0.4f, 0.05f), Mats.Lit("pack_strap", Color.white, 0f, 0.2f, Mats.Fabric, null, 0.3f, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, -0.02f, 0); col.size = new Vector3(w, 0.12f, dep);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Dirt;
            bp.localBounds = new Bounds(col.center, col.size);
            bp.Container = ItemContainer.Grid(go.transform, new Vector3(0, 0.07f, 0), Quaternion.identity, d.cols, d.rows, new Vector2(Cell, Cell), 0.1f,
                it => !(it is Backpack) && !(it is Gun g && g.g.TwoHanded));
            bp.SetupPhysics(1f + d.cols * d.rows * 0.08f);
            return bp;
        }

        public override bool SnapGrip => false;
        public override int Value => (def != null ? def.price : 0) + (Container != null ? Container.TotalValue() : 0);
        public override string Label => def.name + "  (" + bd.cols + "x" + bd.rows + ")";

        /// <summary>Sell value of an item, including everything inside it if it's a backpack.</summary>
        public static int DataValue(ItemData d)
        {
            var def = d != null ? Db.Get(d.id) : null;
            if (def == null) return 0;
            int v = def.price;
            if (!string.IsNullOrEmpty(d.contents)) foreach (var c in Unpack(d.contents)) v += DataValue(c);
            return v;
        }

        public static string Pack(List<ItemData> items) => items == null || items.Count == 0 ? "" : JsonUtility.ToJson(new ItemList { items = items });
        public static List<ItemData> Unpack(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<ItemData>();
            try { var l = JsonUtility.FromJson<ItemList>(json); if (l != null && l.items != null) return l.items; } catch { }
            return new List<ItemData>();
        }

        public override ItemData Save()
        {
            var d = base.Save();
            d.contents = Pack(Container.Save());
            return d;
        }

        public override void Load(ItemData d) { if (d != null) Fill(Unpack(d.contents)); }

        public void Fill(List<ItemData> items)
        {
            if (items == null) return;
            foreach (var d in items)
            {
                var it = ItemFactory.Spawn(d, transform.position, Quaternion.identity);
                if (it != null && !Container.Add(it)) it.Drop(Vector3.zero, Vector3.zero); // doesn't fit: falls out
            }
        }

        public override void OnGrab(Hand h)
        {
            base.OnGrab(h);
            Sfx.Play(Synth.Zipper(), transform.position, 0.5f, 1f, 0.5f, 6f);
        }

        public override void GetHeldPose(out Vector3 pos, out Quaternion rot)
        {
            // hold the bag open in front of you, level, with pockets facing up
            var a = primaryHand.Anchor;
            Vector3 fwd = a.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            rot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            float side = bd.cols * Cell * 0.5f - 0.04f, fw = bd.rows * Cell * 0.5f;
            pos = a.position + rot * new Vector3(primaryHand.left ? side : -side, -0.04f, fw);
        }

        protected override bool TrySlot(Hand h)
        {
            var g = RagoGame.I.Player.Gear;
            if (g.PackSlot.item == null && Vector3.Distance(h.Anchor.position, g.PackSlot.transform.position) < 0.35f)
            {
                g.PackSlot.Store(this);
                if (g.Pack != this) { g.Pack = this; g.Changed(); }
                return true;
            }
            // stash, crate and body slots accept a bag too
            return base.TrySlot(h);
        }
    }
}
