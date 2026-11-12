using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Body-mounted gear, Ghost-of-Tabor style:
    ///  - front of the chest: primary long gun on a sling, muzzle down to the left (no backpack needed)
    ///  - behind lower back: secondary long gun
    ///  - right hip: pistol holster
    ///  - left hip / right chest: utility pouches (meds, keys, sights, valuables)
    ///  - front of both hips: magazine pouches (pull a fresh mag for the gun in your other hand from your ammo reserve)
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
        float _yaw, _magHintT;

        public void Init(PlayerRig p)
        {
            _p = p;
            Root = new GameObject("GearRoot").transform;
            DontDestroyOnLoad(Root.gameObject);
            // front sling: the rifle hangs across your stomach, muzzle down to the left, top of the gun facing out
            // low on the stomach and nearly flat, so even a long stock stays below your chest - never up by your face
            Primary = MakeSlot("FrontSling", new Vector3(0f, -0.56f, 0.27f), Quaternion.LookRotation(new Vector3(-0.86f, -0.5f, 0f), Vector3.forward), it => it is Gun g && g.g.TwoHanded, 0.3f, 0f);
            Secondary = MakeSlot("Secondary", new Vector3(0f, -0.6f, -0.2f), Quaternion.Euler(0, 90, 0) * Quaternion.Euler(0, 0, 180), it => it is Gun g && g.g.TwoHanded, 0.28f, 0f);
            Pistol = MakeSlot("Holster", new Vector3(0.22f, -0.66f, 0.04f), Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 0), it => it is Gun g && !g.g.TwoHanded, 0.16f, 0f);
            Util1 = MakeSlot("PouchL", new Vector3(-0.22f, -0.64f, 0.04f), Quaternion.identity, it => !(it is Gun) && !(it is Backpack) && !(it is ArmorItem), 0.13f, 0.11f);
            Util2 = MakeSlot("PouchChest", new Vector3(0.15f, -0.36f, 0.17f), Quaternion.identity, it => !(it is Gun) && !(it is Backpack) && !(it is ArmorItem), 0.11f, 0.09f);
            PackSlot = MakeSlot("PackSlot", new Vector3(-0.16f, -0.32f, -0.2f), Quaternion.identity, it => it is Backpack, 0.3f, 0.02f);
            int bodyLayer = Layers.LocalBody > 0 ? Layers.LocalBody : 0;
            // pistol holster on the right hip: polymer shell, retention hood and a thigh strap
            var hb = new MB(2);
            hb.Sub(0).Box(new Vector3(0, -0.05f, 0.03f), new Vector3(0.05f, 0.19f, 0.13f), new Color(0.12f, 0.12f, 0.11f), 0.012f, 2);
            hb.Sub(0).Box(new Vector3(0, 0.055f, 0.035f), new Vector3(0.056f, 0.04f, 0.11f), new Color(0.09f, 0.09f, 0.085f), 0.008f);   // hood
            hb.Sub(1).Box(new Vector3(-0.01f, -0.1f, 0.0f), new Vector3(0.012f, 0.035f, 0.2f), new Color(0.2f, 0.22f, 0.16f), 0.004f);      // thigh strap
            hb.Sub(1).Box(new Vector3(-0.01f, 0.1f, 0.03f), new Vector3(0.012f, 0.05f, 0.06f), new Color(0.2f, 0.22f, 0.16f), 0.004f);       // belt loop
            var hv = hb.Build("HolsterMesh", Pistol.transform, Mats.Polymer, Mats.Lit("pouch", Color.white, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.4f, 0f));
            hv.transform.localRotation = Quaternion.Inverse(Pistol.transform.localRotation);
            hv.layer = bodyLayer;
            // front sling: a strap from the right shoulder down to the left hip, and a padded front holster plate with a clip
            var sl = new MB(2);
            Vector3 sa = new Vector3(0.19f, -0.3f, 0.05f), sbp = new Vector3(-0.22f, -0.76f, 0.16f);   // from the shoulder (behind and below your chin) to the left hip
            var sdir = sbp - sa;
            sl.Sub(0).Box((sa + sbp) * 0.5f, new Vector3(0.04f, sdir.magnitude, 0.008f), new Color(0.18f, 0.19f, 0.14f), 0.002f, 0, Quaternion.FromToRotation(Vector3.up, sdir.normalized));
            sl.Sub(0).Box(new Vector3(0f, -0.56f, 0.215f), new Vector3(0.15f, 0.2f, 0.025f), new Color(0.22f, 0.24f, 0.17f), 0.008f, 1, Quaternion.Euler(0, 0, 60f));   // holster plate
            sl.Sub(1).Box(new Vector3(0f, -0.56f, 0.232f), new Vector3(0.05f, 0.07f, 0.014f), new Color(0.1f, 0.1f, 0.1f), 0.004f, 0, Quaternion.Euler(0, 0, 60f));    // clip
            var sv = sl.Build("FrontSlingMesh", Root, Mats.Lit("pouch", Color.white, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.4f, 0f), Mats.Polymer);
            sv.layer = bodyLayer;
            // AMMO POUCHES on the front of each hip: pull a fresh mag for the gun in your other hand, or put ammo
            // you find (boxes, mags) back in. Yellow flap + AMMO label so you can't miss them; the flap turns
            // green when what you're holding will go in, and your reserve shows when your hand is near.
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1f : 1f;
                var pb = new MB(2);
                pb.Sub(0).Box(new Vector3(0, -0.01f, 0), new Vector3(0.11f, 0.12f, 0.05f), new Color(0.2f, 0.22f, 0.16f), 0.008f, 1);           // pouch body
                foreach (float mx in new[] { -0.026f, 0.026f }) pb.Sub(0).Box(new Vector3(mx, 0.062f, 0), new Vector3(0.036f, 0.03f, 0.024f), new Color(0.08f, 0.08f, 0.08f), 0.003f); // mag tops
                var pv = pb.Build(sx < 0 ? "AmmoPouchL" : "AmmoPouchR", Root, Mats.Lit("pouch", Color.white, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.4f, 0f), Mats.Polymer);
                pv.transform.localPosition = new Vector3(sx * 0.15f, -0.62f, 0.13f);
                pv.transform.localRotation = Quaternion.Euler(0, sx * 25f, 0);
                pv.layer = Layers.LocalBody > 0 ? Layers.LocalBody : 0;
                // the flap: its own material so it can light up
                var fb = new MB();
                fb.Box(new Vector3(0, 0.035f, 0.027f), new Vector3(0.112f, 0.03f, 0.006f), Color.white, 0.002f);
                var flap = fb.Build("Flap", pv.transform, new Material(Mats.Lit("ammo_flap", Color.white, 0f, 0.4f)));
                flap.layer = pv.layer;
                _flap[k] = flap.GetComponent<MeshRenderer>().material;
                _flap[k].SetColor("_Color", FlapIdle);
                // label and readout lie on top, tilted up towards your eyes (you look down at your hip)
                var lbl = UI.Text(pv.transform, "AMMO", new Vector3(0, 0.052f, 0.021f), 0.016f, new Color(0.95f, 0.8f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Bold);
                lbl.transform.localRotation = Quaternion.Euler(70, 0, 0);
                lbl.gameObject.layer = pv.layer;
                _pouchInfo[k] = UI.Text(pv.transform, "", new Vector3(0, 0.1f, 0.06f), 0.016f, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
                _pouchInfo[k].transform.localRotation = Quaternion.Euler(70, 0, 0);
                _pouchInfo[k].gameObject.layer = pv.layer;
            }
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

        static readonly Color FlapIdle = new Color(0.95f, 0.75f, 0.12f), FlapReady = new Color(0.25f, 0.95f, 0.35f);
        readonly Material[] _flap = new Material[2];
        readonly TextMesh[] _pouchInfo = new TextMesh[2];

        /// <summary>Is this something the ammo pouch takes (a magazine or a box of ammo)?</summary>
        static bool AmmoThing(Item it) => it is Magazine || (it != null && it.def is SimpleDef sd && sd.kind == ItemKind.Ammo);

        void TickPouches()
        {
            var pos = new[] { HipMagL, HipMagR };
            for (int k = 0; k < 2; k++)
            {
                if (_flap[k] == null) continue;
                bool ready = false, near = false;
                foreach (var h in new[] { _p.HandL, _p.HandR })
                {
                    if (h == null) continue;
                    float d = Vector3.Distance(h.transform.position, pos[k]);
                    if (d < 0.2f) near = true;
                    if (d < 0.16f && h.Held != null && !h.HeldAsSecondary && AmmoThing(h.Held)) ready = true;
                }
                _flap[k].SetColor("_Color", ready ? FlapReady : FlapIdle);
                if (_pouchInfo[k] != null) _pouchInfo[k].text = ready ? "LET GO TO STORE" : near ? ReserveText() : "";
            }
        }

        /// <summary>Your reserve for the guns you have on you, e.g. "5.45x39  120".</summary>
        string ReserveText()
        {
            var cals = new List<Caliber>();
            foreach (var s in new[] { Primary, Secondary, Pistol }) if (s != null && s.item is Gun g && !cals.Contains(g.g.cal)) cals.Add(g.g.cal);
            foreach (var h in new[] { _p.HandL, _p.HandR }) if (h != null && h.Held is Gun hg && !cals.Contains(hg.g.cal)) cals.Add(hg.g.cal);
            if (cals.Count == 0) return "AMMO POUCH";
            var sb = new System.Text.StringBuilder();
            foreach (var c in cals) sb.Append(Db.CalName(c)).Append("  ").Append(Ammo[(int)c]).Append('\n');
            return sb.ToString().TrimEnd();
        }

        public void Tick()
        {
            if (_p.Dead) return;
            TickPouches();
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
        Vector3 LowerBack => Root.TransformPoint(new Vector3(0f, -0.58f, -0.18f));

        Vector3 HeadTop => _p.Head.position + Vector3.up * 0.12f;
        Vector3 Collar => Root.TransformPoint(new Vector3(0f, -0.24f, 0.14f));
        Vector3 VestGrab { get { float yaw = _p.Head.eulerAngles.y; return _p.Head.position + Quaternion.Euler(0, yaw, 0) * new Vector3(0f, -0.32f, 0.13f); } }
        Vector3 ChestCenter => Root.TransformPoint(new Vector3(0f, -0.4f, 0.08f));

        public bool NearChestPouch(Vector3 p) => Vector3.Distance(p, ChestPouch) < 0.17f;
        // mag pouches on the front of both hips (either hand, either side) plus the chest one
        Vector3 HipMagL => Root.TransformPoint(new Vector3(-0.15f, -0.62f, 0.13f));
        Vector3 HipMagR => Root.TransformPoint(new Vector3(0.15f, -0.62f, 0.13f));
        public bool NearMagPouch(Vector3 p) => Vector3.Distance(p, HipMagL) < 0.16f || Vector3.Distance(p, HipMagR) < 0.16f;

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
            if (NearMagPouch(p))
            {
                var gun = h.Other.Held as Gun;
                if (gun == null || h.Other.HeldAsSecondary)
                {
                    if (Time.time > _magHintT) { _magHintT = Time.time + 3f; _p.Toast("Hold a gun in your other hand to pull a mag for it"); }
                }
                else
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
            // armour: grab the helmet anywhere on top of your head; grab the vest at your collar / upper chest.
            // Both follow your head (not the slower body turn), so they're where you reach even with your head turned.
            Vector3 hp = _p.Head.position;
            if (Helmet != null && (Vector3.Distance(p, HeadTop) < 0.19f || (Vector3.Distance(p, hp) < 0.18f && p.y > hp.y + 0.02f)))
                return TakeOff(ArmorSlot.Helmet, p, h.Anchor.rotation);
            if (Vest != null && Vector3.Distance(p, VestGrab) < 0.17f) return TakeOff(ArmorSlot.Vest, p, h.Anchor.rotation);
            if (Vector3.Distance(p, LeftShoulder) < 0.2f && PackSlot.item != null) return PackSlot.item;
            if (Vector3.Distance(p, Primary.transform.position) < 0.22f && Primary.item != null) return Primary.item;   // front sling
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
            l.pack = packOnMe ? Pack.SaveContents() : new List<ItemData>();
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

    /// <summary>
    /// A backpack. Pull it off your left shoulder (or pick it up) and the flap opens onto one big open compartment:
    /// drop things in and they stack up where you let go - meds, loot, helmets, vests, pistols and SMGs.
    /// Things that are a bit too big get squeezed in; things that don't fit at all fall out.
    /// Long guns strap onto the sides. The bag keeps its contents wherever it goes (back, stash, floor, body, trader).
    /// </summary>
    public class Backpack : Item
    {
        public BackpackDef bd;
        public readonly List<Slot> Sides = new List<Slot>();
        public static readonly List<Backpack> AllBags = new List<Backpack>();
        Transform _space, _lid;
        float _lidOpen;
        bool _filling;

        class Stored { public Item it; public Slot slot; public Vector3 size; public float x, z, bottom; }
        readonly List<Stored> _stored = new List<Stored>();

        const float Wall = ArmorBuilder.BagWall;
        Vector3 In => bd.inner;

        public static Backpack Create(Vector3 pos, BackpackDef d)
        {
            if (d == null) d = Db.Packs.Count > 0 ? Db.Packs[0] : new BackpackDef { id = "pack_day", name = "Daypack", kind = ItemKind.Backpack, inner = new Vector3(0.32f, 0.2f, 0.24f), sides = 1, color = new Color(0.3f, 0.32f, 0.26f) };
            var go = new GameObject(d.name);
            go.transform.position = pos;
            var bp = go.AddComponent<Backpack>();
            bp.bd = d; bp.def = d;
            Vector3 n = d.inner, o = ArmorBuilder.BagOuter(d);
            float W = n.x + Wall * 2, H = n.y, D = n.z + Wall * 2;
            ArmorBuilder.OpenBag(d).Build("Model", go.transform, ArmorBuilder.BagMats);
            // flap: hinged on the back rim, closed on the ground, flips open when you hold the bag
            bp._lid = new GameObject("Flap").transform;
            bp._lid.SetParent(go.transform, false);
            bp._lid.localPosition = ArmorBuilder.BagHinge(d);
            ArmorBuilder.BagFlap(d).Build("FlapMesh", bp._lid, ArmorBuilder.BagMats);
            // colliders on the walls only, so your hand can reach in and grab what's inside
            float cw = o.x * 0.94f, cd = o.z * 0.9f, t = 0.02f;
            AddCol(go, new Vector3(0, -t * 0.5f - 0.02f, 0), new Vector3(cw, t + 0.04f, cd));
            AddCol(go, new Vector3(-cw / 2 + t / 2, H / 2, 0), new Vector3(t, H, cd));
            AddCol(go, new Vector3(cw / 2 - t / 2, H / 2, 0), new Vector3(t, H, cd));
            AddCol(go, new Vector3(0, H / 2, -cd / 2 + t / 2), new Vector3(cw, H, t + 0.03f));
            AddCol(go, new Vector3(0, H * 0.4f, cd / 2 + 0.02f), new Vector3(cw, H * 0.8f, t + 0.06f));
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Dirt;
            bp.localBounds = new Bounds(new Vector3(0, H / 2, 0), new Vector3(o.x, H + 0.08f, o.z));
            bp._space = new GameObject("Compartment").transform;
            bp._space.SetParent(go.transform, false);
            // rifle straps on the sides: the gun rides vertically, muzzle up
            for (int i = 0; i < Mathf.Clamp(d.sides, 0, 2); i++)
            {
                float sx = i == 0 ? 1 : -1;
                var sgo = new GameObject("SideStrap" + i);
                sgo.transform.SetParent(go.transform, false);
                sgo.transform.localPosition = new Vector3(sx * (o.x / 2 + 0.04f), H * 0.62f, 0);
                var sl = sgo.AddComponent<Slot>();
                sl.radius = 0.22f; sl.fit = 0f;
                sl.storeRot = Quaternion.Euler(-90, 0, 0);
                sl.filter = it => it is Gun g && g.g.TwoHanded && (bp.Open || bp._filling);
                bp.Sides.Add(sl);
            }
            bp.SetupPhysics(1f + n.x * n.y * n.z * 20f);
            return bp;
        }

        static void AddCol(GameObject go, Vector3 c, Vector3 s) { var b = go.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        protected override void Awake() { base.Awake(); AllBags.Add(this); }
        protected override void OnDestroy() { base.OnDestroy(); AllBags.Remove(this); }

        /// <summary>True when the bag is full-size (not shrunk onto your shoulder or a stash shelf).</summary>
        public bool Open => transform.lossyScale.x > 0.9f;

        public override bool SnapGrip => false;
        // your other hand reaches into the bag you're holding instead of snatching the bag itself
        public override bool CanGrab(Hand h) => !(Held && primaryHand != h);
        public override int Value => (def != null ? def.price : 0) + ContentsValue();
        public override string Label => def.name + "  (" + bd.Liters + " L" + (bd.sides > 0 ? ", " + bd.sides + " rifle strap" + (bd.sides > 1 ? "s" : "") : "") + ")";

        public int ContentsValue()
        {
            int v = 0;
            foreach (var s in _stored) if (s.it != null) v += s.it.Value;
            foreach (var s in Sides) if (s.item != null) v += s.item.Value;
            return v;
        }

        // ------------------------------------------------------------------------------- packing
        static readonly Quaternion[] Orient = BuildOrient();
        static Quaternion[] BuildOrient()
        {
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var l = new List<Quaternion>();
            for (int f = 0; f < 3; f++) for (int u = 0; u < 3; u++) if (f != u) l.Add(Quaternion.LookRotation(axes[f], axes[u]));
            return l.ToArray();
        }

        static float MinScale(Item it) => it is Gun ? 0.8f : 0.55f;

        static bool CanGoInside(Item it) => !(it is Backpack) && it.rb != null;

        /// <summary>Find room for an item: (orientation, scale, x, z, bottom). Prefers the spot nearest `want` and an orientation close to `cur`.</summary>
        bool FindSpot(Item it, Vector2 want, Quaternion cur, Stored ignore, out Quaternion rot, out float scale, out Vector3 size, out float x, out float z, out float bottom)
        {
            rot = Quaternion.identity; scale = 1; size = Vector3.zero; x = z = bottom = 0;
            var b = it.localBounds.size;
            if (b.sqrMagnitude < 1e-6f) b = Vector3.one * 0.05f;
            float best = -1;
            foreach (var q in Orient)
            {
                Vector3 e = Abs(q * b);
                float sc = Mathf.Min(1f, In.x / e.x, (In.y + 0.04f) / e.y, In.z / e.z);
                float score = sc * 10f - e.y * sc * 2f - Quaternion.Angle(q, cur) / 900f;
                if (sc >= MinScale(it) && score > best) { best = score; rot = q; scale = sc; size = e * sc; }
            }
            if (best < 0) return false;
            // try the spot you let go first, then every other spot nearest to it
            float step = 0.025f;
            float hx = (In.x - size.x) / 2f, hz = (In.z - size.z) / 2f;
            var cands = new List<Vector2>();
            cands.Add(new Vector2(Mathf.Clamp(want.x, -hx, hx), Mathf.Clamp(want.y, -hz, hz)));
            for (float cx = -hx; cx <= hx + 1e-4f; cx += step) for (float cz = -hz; cz <= hz + 1e-4f; cz += step) cands.Add(new Vector2(cx, cz));
            var w0 = cands[0];
            cands.Sort(1, cands.Count - 1, Comparer<Vector2>.Create((a, c) => (a - w0).sqrMagnitude.CompareTo((c - w0).sqrMagnitude)));
            foreach (var p in cands)
            {
                float bot = BaseAt(p.x, p.y, size, ignore);
                if (bot + size.y <= In.y + 0.04f) { x = p.x; z = p.y; bottom = bot; return true; }
            }
            return false;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        float BaseAt(float x, float z, Vector3 size, Stored ignore)
        {
            float top = 0;
            foreach (var s in _stored)
            {
                if (s == ignore || s.it == null) continue;
                if (Mathf.Abs(s.x - x) * 2f < s.size.x + size.x - 0.004f && Mathf.Abs(s.z - z) * 2f < s.size.z + size.z - 0.004f)
                    top = Mathf.Max(top, s.bottom + s.size.y);
            }
            return top;
        }

        /// <summary>Put an item into the main compartment, as close as possible to where it is now. False if it doesn't fit.</summary>
        public bool Add(Item it, bool sound = true) => AddAt(it, null, sound);

        bool AddAt(Item it, Vector3? worldPos, bool sound)
        {
            if (it == null || it == this || !CanGoInside(it)) return false;
            // long guns go on the side straps first
            if (it is Gun g && g.g.TwoHanded) foreach (var s in Sides) if (s.Accepts(it)) { s.Store(it, sound); return true; }
            Vector3 lp = worldPos.HasValue ? _space.InverseTransformPoint(worldPos.Value) : Vector3.zero;
            Quaternion cur = Quaternion.Inverse(_space.rotation) * it.transform.rotation;
            if (!FindSpot(it, new Vector2(lp.x, lp.z), cur, null, out var rot, out var sc, out var size, out var x, out var z, out var bottom)) return false;
            var sgo = new GameObject("In_" + it.name);
            sgo.transform.SetParent(_space, false);
            var sl = sgo.AddComponent<Slot>();
            sl.showGhost = false; sl.radius = 0f;
            sl.storeRot = rot;
            float m = Mathf.Max(it.localBounds.size.x, Mathf.Max(it.localBounds.size.y, it.localBounds.size.z));
            sl.fit = sc < 0.999f ? m * sc : 0f;
            var st = new Stored { it = it, slot = sl, size = size, x = x, z = z, bottom = bottom };
            _stored.Add(st);
            sgo.transform.localPosition = new Vector3(x, bottom + size.y / 2f, z);
            sl.Store(it, false);
            sl.onChanged = s => { if (s.item == null) OnTakenOut(st); };
            if (sound) Sfx.Play(Synth.Grab(2), transform.position, 0.4f, 0.8f, 0.5f, 6f);
            return true;
        }

        void OnTakenOut(Stored st)
        {
            _stored.Remove(st);
            if (st.slot != null) Compat.Kill(st.slot.gameObject);
            // everything that was resting on it settles down
            _stored.Sort((a, b) => a.bottom.CompareTo(b.bottom));
            var settled = new List<Stored>(_stored);
            _stored.Clear();
            foreach (var s in settled)
            {
                if (s.it == null) continue;
                s.bottom = BaseAt(s.x, s.z, s.size, null);
                if (s.slot != null) s.slot.transform.localPosition = new Vector3(s.x, s.bottom + s.size.y / 2f, s.z);
                _stored.Add(s);
            }
        }

        /// <summary>Called for any item let go of in the world: if it was released inside an open bag, it goes in.</summary>
        public static bool TryDropInto(Item it)
        {
            if (it == null || it is Backpack) return false;
            var c = it.transform.TransformPoint(it.localBounds.center);
            Backpack best = null; float bd = 1e9f;
            foreach (var b in AllBags)
            {
                if (b == null || !b.Open || !b.Held) continue; // the flap has to be open: hold the bag
                var lp = b._space.InverseTransformPoint(c);
                var n = b.In;
                if (Mathf.Abs(lp.x) > n.x / 2 + 0.06f || Mathf.Abs(lp.z) > n.z / 2 + 0.06f || lp.y < -0.06f || lp.y > n.y + 0.22f) continue;
                float d = new Vector2(lp.x, lp.z).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            if (best == null) return false;
            if (best.AddAt(it, c, true)) return true;
            RagoGame.I?.Player?.Toast(it is Gun gg && gg.g.TwoHanded ? "Too long - strap it to the side of the bag" : "Doesn't fit in the " + best.def.name);
            return false;
        }

        // ------------------------------------------------------------------------------- save / load
        public static string Pack(List<ItemData> items) => items == null || items.Count == 0 ? "" : JsonUtility.ToJson(new ItemList { items = items });
        public static List<ItemData> Unpack(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<ItemData>();
            try { var l = JsonUtility.FromJson<ItemList>(json); if (l != null && l.items != null) return l.items; } catch { }
            return new List<ItemData>();
        }

        /// <summary>Sell value of an item, including everything inside it if it's a backpack.</summary>
        public static int DataValue(ItemData d)
        {
            var def = d != null ? Db.Get(d.id) : null;
            if (def == null) return 0;
            int v = def.price;
            if (!string.IsNullOrEmpty(d.contents)) foreach (var c in Unpack(d.contents)) v += DataValue(c);
            return v;
        }

        public List<ItemData> SaveContents()
        {
            var l = new List<ItemData>();
            foreach (var s in Sides) if (s.item != null) l.Add(s.item.Save());
            foreach (var s in _stored) if (s.it != null) l.Add(s.it.Save());
            return l;
        }

        public override ItemData Save()
        {
            var d = base.Save();
            d.contents = Pack(SaveContents());
            return d;
        }

        public override void Load(ItemData d) { if (d != null) Fill(Unpack(d.contents)); }

        public void Fill(List<ItemData> items)
        {
            if (items == null) return;
            // biggest things first so they end up at the bottom
            var spawned = new List<Item>();
            foreach (var d in items) { var it = ItemFactory.Spawn(d, transform.position + Vector3.up * 0.3f, transform.rotation); if (it != null) spawned.Add(it); }
            spawned.Sort((a, b) => (b.localBounds.size.x * b.localBounds.size.y * b.localBounds.size.z).CompareTo(a.localBounds.size.x * a.localBounds.size.y * a.localBounds.size.z));
            _filling = true;
            foreach (var it in spawned)
                if (!AddAt(it, null, false)) it.Drop(Vector3.zero, Vector3.zero); // doesn't fit: falls out
            _filling = false;
        }

        // ------------------------------------------------------------------------------- holding
        public override void OnGrab(Hand h)
        {
            base.OnGrab(h);
            Sfx.Play(Synth.Zipper(), transform.position, 0.5f, 1f, 0.5f, 6f);
        }

        public override void AnyUpdate()
        {
            float target = Held && Open ? 1f : 0f;
            if (Mathf.Abs(_lidOpen - target) > 0.001f)
            {
                _lidOpen = Mathf.MoveTowards(_lidOpen, target, Time.deltaTime * 4f);
                _lid.localRotation = Quaternion.Euler(-200f * Mathf.SmoothStep(0, 1, _lidOpen), 0, 0);
            }
        }

        public override void GetHeldPose(out Vector3 pos, out Quaternion rot)
        {
            // hold the bag open in front of you by its near rim, level, mouth up
            var a = primaryHand.Anchor;
            Vector3 fwd = a.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            rot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            var n = In;
            Vector3 grip = new Vector3(0, n.y + 0.02f, -ArmorBuilder.BagOuter(bd).z / 2 - 0.01f); // by the haul loop
            pos = a.position - rot * grip;
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
            // stash, crate and trader slots accept a bag too
            return base.TrySlot(h);
        }
    }
}
