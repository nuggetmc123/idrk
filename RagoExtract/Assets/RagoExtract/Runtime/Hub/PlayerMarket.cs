using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Player-to-player trading in the Market. Nothing changes hands until it's safe:
    ///  - TRADE TABLE: two players each stand at a side, put items (and cash) on their half and press ACCEPT. The swap
    ///    only happens when both have accepted exactly the offers on the table - any change un-accepts both sides. Your
    ///    items never leave your side until then; what you get lands on your RECEIVED tray.
    ///  - PLAYER STALLS: claim a stall, hang items on it and on the mannequin (sold as one kit), set prices. Buyers press
    ///    BUY: their money is held, the seller's game checks the item is still there and sends it over (or refunds).
    /// Everyone only ever touches their own real items; other players see display copies.
    /// </summary>
    public static class MarketNet
    {
        public const byte Offer = 1, Release = 2, Accept = 3, Listing = 4, StallRelease = 5, BuyReq = 6, Sold = 7, NoSale = 8;
        static bool _hooked;
        public static void Hook() { if (_hooked) return; _hooked = true; NetGame.OnMarket += Receive; }
        public static ulong Me => NetHub.Backend != null ? NetHub.Backend.LocalId : 0;
        public static string NameOf(ulong id)
        {
            if (id == Me) return Profile.Current != null ? Profile.Current.DisplayName : "You";
            return NetGame.I != null && NetGame.I.Remotes.TryGetValue(id, out var r) && r != null ? r.DisplayName : "Operator";
        }
        public static bool Present(ulong id) => id == Me || (NetGame.I != null && NetGame.I.Remotes.ContainsKey(id));
        public static void Send(System.Action<BinaryWriter> w, ulong target = ulong.MaxValue) { if (NetGame.Live) NetGame.I.SendMarket(w, target); }

        static void Receive(ulong from, BinaryReader r)
        {
            byte op = r.ReadByte();
            if (op <= Accept) { int t = r.ReadByte(); if (TradeTable.I != null && t == 0) TradeTable.I.OnNet(op, from, r); }
            else { int st = r.ReadByte(); if (st < PlayerStall.All.Count) PlayerStall.All[st].OnNet(op, from, r); }
        }

        public static string Json(Item it) => JsonUtility.ToJson(it.Save());
        public static ItemData Data(string json) { try { return JsonUtility.FromJson<ItemData>(json); } catch { return null; } }

        /// <summary>A look-but-don't-touch copy of an item, sized to fit in 'fit' metres.</summary>
        public static GameObject Ghost(ItemData d, Transform parent, Vector3 localPos, float fit)
        {
            if (d == null) return null;
            var it = ItemFactory.Spawn(d, parent.TransformPoint(localPos), parent.rotation);
            if (it == null) return null;
            foreach (var x in it.GetComponentsInChildren<Item>(true)) { Item.All.Remove(x); if (x.rb != null) { x.rb.isKinematic = true; x.rb.detectCollisions = false; } }
            foreach (var c in it.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            var go = it.gameObject;
            foreach (var x in go.GetComponentsInChildren<Item>(true)) Object.Destroy(x);
            go.transform.SetParent(parent, true);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
                float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (big > fit) go.transform.localScale *= fit / big;
            }
            go.transform.localPosition = localPos;
            return go;
        }

        public static TextMesh Label(Transform parent, string s, Vector3 p, float size, Color c) => UI.Text(parent, s, p, size, c, TextAnchor.MiddleCenter, FontStyle.Bold);
        public static Material Wood => Mats.Lit("pm_wood", new Color(0.5f, 0.36f, 0.22f), 0f, 0.3f, Mats.Noise, null, 0.3f, 0.2f);
        public static Material Steel => Mats.Lit("pm_steel", new Color(0.3f, 0.31f, 0.33f), 0.7f, 0.45f, Mats.Metal, null, 0.3f, 0.3f);
    }

    // =========================================================================================== TRADE TABLE
    public class TradeTable : MonoBehaviour
    {
        public static TradeTable I;
        class Side
        {
            public ulong owner; public string name = "";
            public ItemContainer grid, tray;   // the real items (only on the owner's machine) / what you received
            public int version, money;
            public bool accepted; public int accVa, accVb;
            public readonly List<string> items = new List<string>();
            public readonly List<GameObject> ghosts = new List<GameObject>();
            public TextMesh sign, info; public WorldButton claim, accept, plus, minus;
            public Transform root;
        }
        readonly Side[] _s = { new Side(), new Side() };
        float _bcastT;
        bool _quiet;

        public static TradeTable Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("TradeTable");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var t = go.AddComponent<TradeTable>();
            I = t;
            MarketNet.Hook();
            t.Build();
            return t;
        }

        void Build()
        {
            var mb = new MB();
            mb.Box(new Vector3(0, 0.88f, 0), new Vector3(2.2f, 0.08f, 1.5f), Color.white, 0.02f);
            foreach (var x in new[] { -1f, 1f }) foreach (var z in new[] { -0.65f, 0.65f }) mb.Box(new Vector3(x, 0.42f, z), new Vector3(0.08f, 0.84f, 0.08f), Color.white * 0.8f);
            mb.Box(new Vector3(0, 0.925f, 0), new Vector3(2.2f, 0.01f, 0.03f), new Color(0.9f, 0.75f, 0.2f));   // the line down the middle
            mb.Build("Table", transform, MarketNet.Wood);
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.46f, 0); col.size = new Vector3(2.2f, 0.92f, 1.5f);
            MarketNet.Label(transform, "TRADE TABLE", new Vector3(0, 1.9f, 0), 0.12f, UI.Accent);
            MarketNet.Label(transform, "TRADE TABLE", new Vector3(0, 1.9f, 0.01f), 0.12f, UI.Accent).transform.localRotation = Quaternion.Euler(0, 180, 0);
            for (int k = 0; k < 2; k++)
            {
                var sd = _s[k];
                float zz = k == 0 ? -1f : 1f;   // side 0 stands at -Z, side 1 at +Z
                sd.root = new GameObject("Side" + k).transform; sd.root.SetParent(transform, false);
                sd.root.localPosition = new Vector3(0, 0.93f, zz * 0.38f);
                sd.root.localRotation = Quaternion.Euler(0, k == 0 ? 0 : 180, 0);
                int kk = k;
                sd.grid = ItemContainer.Grid(sd.root, new Vector3(-0.25f, 0.02f, 0), Quaternion.identity, 3, 2, new Vector2(0.3f, 0.3f), 0.26f);
                sd.grid.onChanged = () => OnGridChanged(kk);
                sd.tray = ItemContainer.Grid(sd.root, new Vector3(0.78f, 0.02f, -0.05f), Quaternion.identity, 1, 2, new Vector2(0.3f, 0.3f), 0.26f);
                var ui = UI.Panel(sd.root, "TradeUI", new Vector3(0, 0.28f, -0.36f), Quaternion.Euler(-25, 0, 0), new Vector2(1.9f, 0.32f), null, true, Skin.Steel);
                sd.sign = UI.Text(ui, "", new Vector3(-0.9f, 0.1f, -0.003f), 0.03f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                sd.info = UI.Text(ui, "", new Vector3(-0.9f, 0.04f, -0.003f), 0.022f, UI.Dim, TextAnchor.MiddleLeft);
                sd.claim = UI.Button(ui, "TRADE HERE", new Vector3(-0.55f, -0.08f, 0), new Vector2(0.34f, 0.1f), () => Claim(kk), new Color(0.2f, 0.4f, 0.6f), BtnStyle.Key);
                sd.minus = UI.Button(ui, "- $1000", new Vector3(-0.15f, -0.08f, 0), new Vector2(0.22f, 0.1f), () => Money(kk, -1000), new Color(0.35f, 0.35f, 0.35f), BtnStyle.Key);
                sd.plus = UI.Button(ui, "+ $1000", new Vector3(0.1f, -0.08f, 0), new Vector2(0.22f, 0.1f), () => Money(kk, 1000), new Color(0.35f, 0.35f, 0.35f), BtnStyle.Key);
                sd.accept = UI.Button(ui, "ACCEPT", new Vector3(0.55f, -0.08f, 0), new Vector2(0.34f, 0.1f), () => AcceptPress(kk), new Color(0.2f, 0.5f, 0.2f), BtnStyle.Key);
                MarketNet.Label(sd.root, "RECEIVED", new Vector3(0.78f, 0.01f, 0.3f), 0.025f, UI.Dim).transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Refresh();
        }

        int Mine() { for (int k = 0; k < 2; k++) if (_s[k].owner != 0 && _s[k].owner == MarketNet.Me) return k; return -1; }

        void Claim(int k)
        {
            if (!NetGame.Live) { RagoGame.I.Player.Toast("Trading needs the Market online"); return; }
            if (_s[k].owner != 0 && _s[k].owner != MarketNet.Me) return;
            if (_s[k].owner == MarketNet.Me) { Leave(k); return; }
            int had = Mine(); if (had >= 0) Leave(had);
            _s[k].owner = MarketNet.Me; _s[k].name = MarketNet.NameOf(MarketNet.Me);
            _s[k].version++; _s[k].money = 0;
            ResetAccepts();
            Broadcast(k);
            Refresh();
        }

        void Leave(int k)
        {
            var sd = _s[k];
            if (sd.owner != MarketNet.Me) return;
            sd.owner = 0; sd.name = ""; sd.money = 0; sd.items.Clear(); sd.version++;
            ResetAccepts();
            MarketNet.Send(w => { w.Write(MarketNet.Release); w.Write((byte)0); w.Write((byte)k); });
            Refresh();
        }

        void Money(int k, int d)
        {
            var sd = _s[k];
            if (sd.owner != MarketNet.Me || Profile.Current == null) return;
            sd.money = Mathf.Clamp(sd.money + d, 0, Profile.Current.money);
            sd.version++; ResetAccepts(); Broadcast(k); Refresh();
        }

        void OnGridChanged(int k)
        {
            if (_quiet) return;
            var sd = _s[k];
            sd.items.Clear();
            foreach (var sl in sd.grid.slots) if (sl.item != null) sd.items.Add(MarketNet.Json(sl.item));
            if (sd.owner != MarketNet.Me) return;
            sd.version++; ResetAccepts(); Broadcast(k); Refresh();
        }

        void ResetAccepts() { _s[0].accepted = _s[1].accepted = false; }

        void AcceptPress(int k)
        {
            var sd = _s[k]; var other = _s[1 - k];
            if (sd.owner != MarketNet.Me) return;
            if (other.owner == 0) { RagoGame.I.Player.Toast("Nobody on the other side yet"); return; }
            sd.accepted = !sd.accepted; sd.accVa = _s[0].version; sd.accVb = _s[1].version;
            bool acc = sd.accepted; int va = sd.accVa, vb = sd.accVb;
            MarketNet.Send(w => { w.Write(MarketNet.Accept); w.Write((byte)0); w.Write((byte)k); w.Write(acc); w.Write(va); w.Write(vb); });
            Sfx.UI(Synth.UiClick(), 0.4f);
            Refresh();
        }

        void Broadcast(int k)
        {
            var sd = _s[k];
            if (sd.owner != MarketNet.Me) return;
            string name = sd.name; int ver = sd.version, money = sd.money; var items = new List<string>(sd.items);
            MarketNet.Send(w =>
            {
                w.Write(MarketNet.Offer); w.Write((byte)0); w.Write((byte)k);
                w.Write(name); w.Write(ver); w.Write(money); w.Write((byte)items.Count);
                foreach (var j in items) w.Write(j);
            });
        }

        public void OnNet(byte op, ulong from, BinaryReader r)
        {
            int k = r.ReadByte(); if (k > 1) return;
            var sd = _s[k];
            switch (op)
            {
                case MarketNet.Offer:
                {
                    string name = r.ReadString(); int ver = r.ReadInt32(), money = r.ReadInt32(); int n = r.ReadByte();
                    var items = new List<string>(); for (int i = 0; i < n; i++) items.Add(r.ReadString());
                    if (sd.owner == MarketNet.Me) { if (from < MarketNet.Me) { Leave(k); } else { Broadcast(k); break; } }   // both claimed at once: the lower id keeps it
                    bool changed = sd.owner != from || sd.version != ver;
                    sd.owner = from; sd.name = name; sd.money = money;
                    if (changed) { sd.version = ver; ResetAccepts(); }
                    sd.items.Clear(); sd.items.AddRange(items);
                    ShowGhosts(k);
                    break;
                }
                case MarketNet.Release:
                    if (sd.owner == from) { sd.owner = 0; sd.name = ""; sd.money = 0; sd.items.Clear(); sd.version++; ResetAccepts(); ShowGhosts(k); }
                    break;
                case MarketNet.Accept:
                {
                    bool acc = r.ReadBoolean(); int va = r.ReadInt32(), vb = r.ReadInt32();
                    if (sd.owner != from) break;
                    sd.accepted = acc; sd.accVa = va; sd.accVb = vb;
                    break;
                }
            }
            Refresh();
        }

        void ShowGhosts(int k)
        {
            var sd = _s[k];
            foreach (var g in sd.ghosts) if (g != null) Destroy(g);
            sd.ghosts.Clear();
            if (sd.owner == MarketNet.Me) return;
            for (int i = 0; i < sd.items.Count && i < 6; i++)
            {
                var d = MarketNet.Data(sd.items[i]);
                var pos = new Vector3(-0.25f + (i % 3 - 1) * 0.3f, 0.12f, (i / 3 - 0.5f) * 0.3f);
                var g = MarketNet.Ghost(d, sd.root, pos, 0.26f);
                if (g != null) sd.ghosts.Add(g);
            }
        }

        void Update()
        {
            int mine = Mine();
            // the other trader left the Market: free their side
            for (int k = 0; k < 2; k++) if (_s[k].owner != 0 && !MarketNet.Present(_s[k].owner)) { _s[k].owner = 0; _s[k].items.Clear(); _s[k].money = 0; _s[k].version++; ResetAccepts(); ShowGhosts(k); Refresh(); }
            if (mine < 0) return;
            // walked away from the table: give up your side (your items stay where they are)
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && Vector3.Distance(pl.transform.position, _s[mine].root.position) > 4f) { Leave(mine); return; }
            _bcastT -= Time.deltaTime;
            if (_bcastT <= 0f) { _bcastT = 3f; Broadcast(mine); }   // late arrivals catch up
            // both accepted exactly what's on the table now: swap
            var a = _s[0]; var b = _s[1];
            if (a.owner != 0 && b.owner != 0 && a.accepted && b.accepted && a.accVa == a.version && a.accVb == b.version && b.accVa == a.version && b.accVb == b.version)
                Commit(mine);
        }

        void Commit(int mine)
        {
            var me = _s[mine]; var them = _s[1 - mine];
            var prof = Profile.Current;
            if (prof == null || prof.money < me.money) { me.accepted = false; RagoGame.I.Player.Toast("Not enough money for your offer"); Refresh(); return; }
            _quiet = true;
            // what you gave goes, what they gave lands on your RECEIVED tray (overflow on the table top)
            foreach (var sl in me.grid.slots) if (sl.item != null) { var it = sl.item; sl.Release(it); it.Consumed = true; Destroy(it.gameObject); }
            prof.money += them.money - me.money;
            int ti = 0;
            foreach (var j in them.items)
            {
                var d = MarketNet.Data(j); if (d == null) continue;
                var it = ItemFactory.Spawn(d, me.root.TransformPoint(new Vector3(0.78f, 0.3f, 0)), me.root.rotation);
                if (it == null) continue;
                while (ti < me.tray.slots.Count && me.tray.slots[ti].item != null) ti++;
                if (ti < me.tray.slots.Count) me.tray.slots[ti].Store(it, false);
            }
            _quiet = false;
            me.items.Clear(); me.money = 0; me.version++; them.items.Clear(); them.money = 0; them.version++;
            ResetAccepts(); ShowGhosts(1 - mine);
            prof.Save();
            Sfx.UI(Synth.Cash(), 0.7f);
            RagoGame.I.Player.Toast("TRADE DONE with " + them.name + " - your new stuff is on the RECEIVED tray");
            Broadcast(mine);
            Refresh();
        }

        void Refresh()
        {
            int mine = Mine();
            for (int k = 0; k < 2; k++)
            {
                var sd = _s[k]; bool own = sd.owner == MarketNet.Me && sd.owner != 0;
                sd.sign.text = sd.owner == 0 ? "FREE - press TRADE HERE" : (own ? "YOU" : sd.name) + (sd.accepted ? "   <color=#66ff66>ACCEPTED</color>" : "");
                sd.sign.richText = true;
                int val = 0; foreach (var j in sd.items) { var d = MarketNet.Data(j); if (d != null && Db.Get(d.id) != null) val += Db.Get(d.id).price; }
                sd.info.text = sd.owner == 0 ? "" : sd.items.Count + " item(s)  ~$" + val.ToString("N0") + (sd.money > 0 ? "   + $" + sd.money.ToString("N0") + " cash" : "");
                sd.claim.Label.text = own ? "LEAVE" : "TRADE HERE";
                sd.claim.Interactable = sd.owner == 0 || own;
                sd.accept.Interactable = own; sd.plus.Interactable = own; sd.minus.Interactable = own;
                sd.accept.Label.text = own && sd.accepted ? "UN-ACCEPT" : "ACCEPT";
            }
        }

        public void SweepToStash(List<ItemData> into)
        {
            foreach (var sd in _s)
                foreach (var c in new[] { sd.grid, sd.tray })
                    foreach (var sl in c.slots) if (sl.item != null) { var it = sl.item; into.Add(it.Save()); sl.Release(it); Destroy(it.gameObject); }
        }

        void OnDestroy() { if (I == this) I = null; }
    }

    // =========================================================================================== PLAYER STALLS
    public class PlayerStall : MonoBehaviour
    {
        public static readonly List<PlayerStall> All = new List<PlayerStall>();
        const int Hooks = 6, Kit = 6, Slots = 10;   // 0-5 hooks / table, 6-9 the mannequin (helmet, vest, gun, bag) sold as one
        int _index;
        ulong _owner; string _ownerName = "";
        readonly Slot[] _slot = new Slot[Slots];
        readonly int[] _price = new int[Slots];
        readonly string[] _json = new string[Slots];
        readonly GameObject[] _ghost = new GameObject[Slots];
        readonly TextMesh[] _tag = new TextMesh[Hooks + 1];
        readonly WorldButton[] _buy = new WorldButton[Hooks + 1], _up = new WorldButton[Hooks + 1], _down = new WorldButton[Hooks + 1];
        TextMesh _sign; WorldButton _claim;
        Transform _pickup;
        float _bcastT, _pendingT; int _pendingSlot = -1, _pendingPaid;
        bool _quiet;

        public static PlayerStall Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("PlayerStall" + All.Count);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<PlayerStall>();
            s._index = All.Count; All.Add(s);
            MarketNet.Hook();
            s.Build();
            return s;
        }

        // the customer stands at local -Z
        void Build()
        {
            var mb = new MB();
            mb.Box(new Vector3(0, 0.45f, 0.1f), new Vector3(2.4f, 0.9f, 0.7f), Color.white, 0.02f);                          // counter
            mb.Box(new Vector3(0, 1.6f, 0.55f), new Vector3(2.4f, 1.4f, 0.06f), Color.white * 0.75f, 0.01f);                  // pegboard
            foreach (var x in new[] { -1.2f, 1.2f }) mb.Box(new Vector3(x, 1.25f, 0.5f), new Vector3(0.08f, 2.5f, 0.08f), Color.white * 0.6f);
            mb.Box(new Vector3(0, 2.55f, 0.5f), new Vector3(2.6f, 0.3f, 0.08f), new Color(0.2f, 0.25f, 0.35f), 0.01f);       // header
            mb.Build("Stall", transform, MarketNet.Wood);
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.45f, 0.1f); col.size = new Vector3(2.4f, 0.9f, 0.7f);
            _sign = MarketNet.Label(transform, "", new Vector3(0, 2.55f, 0.45f), 0.075f, Color.white);
            // six spots: three hooks on the board, three on the counter
            for (int i = 0; i < Hooks; i++)
            {
                bool hook = i < 3;
                Vector3 p = hook ? new Vector3(-0.75f + i * 0.75f, 1.75f, 0.42f) : new Vector3(-0.75f + (i - 3) * 0.75f, 0.95f, 0.15f);
                _slot[i] = MakeSlot(p, 0.4f, i);
                if (hook) new MB().Cyl(p + new Vector3(0, 0.22f, 0.1f), p + new Vector3(0, 0.22f, -0.05f), 0.01f, 0.01f, 6, Color.gray).Build("Hook", transform, MarketNet.Steel);
                Controls(i, p + (hook ? new Vector3(0, -0.32f, -0.05f) : new Vector3(0, -0.06f, -0.38f)), hook ? Quaternion.identity : Quaternion.Euler(50, 0, 0));
            }
            // the mannequin: a whole kit for one price
            var mq = new Vector3(1.75f, 0, -0.1f);
            var m = new MB();
            m.Cyl(mq, mq + new Vector3(0, 0.05f, 0), 0.25f, 0.25f, 16, Color.gray);
            m.Cyl(mq + new Vector3(0, 0.05f, 0), mq + new Vector3(0, 1.0f, 0), 0.025f, 0.025f, 8, Color.gray);
            m.Box(mq + new Vector3(0, 1.3f, 0), new Vector3(0.36f, 0.55f, 0.22f), new Color(0.85f, 0.82f, 0.75f), 0.08f);
            m.Sphere(mq + new Vector3(0, 1.72f, 0), new Vector3(0.11f, 0.13f, 0.12f), 12, new Color(0.85f, 0.82f, 0.75f));
            m.Build("Mannequin", transform, MarketNet.Wood);
            _slot[6] = MakeSlot(mq + new Vector3(0, 1.82f, 0), 0.32f, 6, it => it is ArmorItem a && a.ad.slot == ArmorSlot.Helmet);
            _slot[7] = MakeSlot(mq + new Vector3(0, 1.3f, -0.08f), 0.5f, 7, it => it is ArmorItem a && a.ad.slot == ArmorSlot.Vest);
            _slot[8] = MakeSlot(mq + new Vector3(0, 1.15f, -0.25f), 0.9f, 8, it => it is Gun);
            _slot[9] = MakeSlot(mq + new Vector3(0, 1.3f, 0.2f), 0.5f, 9, it => it is Backpack);
            Controls(Hooks, mq + new Vector3(0, 0.55f, -0.3f), Quaternion.identity);
            _claim = UI.Button(transform, "CLAIM STALL", new Vector3(-0.95f, 1.05f, -0.26f), new Vector2(0.4f, 0.1f), Claim, new Color(0.2f, 0.4f, 0.6f), BtnStyle.Key);
            _claim.transform.localRotation = Quaternion.Euler(50, 0, 0);
            _pickup = new GameObject("Pickup").transform; _pickup.SetParent(transform, false); _pickup.localPosition = new Vector3(0.9f, 1.0f, -0.15f);
            Refresh();
        }

        Slot MakeSlot(Vector3 p, float fit, int i, System.Func<Item, bool> filter = null)
        {
            var c = ItemContainer.Grid(transform, p, Quaternion.identity, 1, 1, new Vector2(fit, fit), fit, filter);
            var sl = c.slots[0];
            c.onChanged = () => OnSlotChanged(i);
            return sl;
        }

        void Controls(int i, Vector3 p, Quaternion rot)
        {
            int k = i;
            var root = new GameObject("Price" + i).transform; root.SetParent(transform, false); root.localPosition = p; root.localRotation = rot;
            _tag[i] = UI.Text(root, "", new Vector3(0, 0.05f, -0.01f), 0.035f, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _down[i] = UI.Button(root, "-", new Vector3(-0.2f, -0.04f, 0), new Vector2(0.08f, 0.07f), () => Price(k, -1), new Color(0.35f, 0.35f, 0.35f), BtnStyle.Key);
            _buy[i] = UI.Button(root, "BUY", new Vector3(0, -0.04f, 0), new Vector2(0.2f, 0.07f), () => Buy(k), new Color(0.2f, 0.5f, 0.2f), BtnStyle.Key);
            _up[i] = UI.Button(root, "+", new Vector3(0.2f, -0.04f, 0), new Vector2(0.08f, 0.07f), () => Price(k, 1), new Color(0.35f, 0.35f, 0.35f), BtnStyle.Key);
        }

        int PriceIdx(int i) => i >= Kit ? Kit : i;
        bool Mine => _owner != 0 && _owner == MarketNet.Me;

        void Claim()
        {
            if (!NetGame.Live) { RagoGame.I.Player.Toast("Stalls need the Market online"); return; }
            if (Mine) { Release(); return; }
            if (_owner != 0) return;
            foreach (var s in All) if (s != this && s.Mine) s.Release();
            _owner = MarketNet.Me; _ownerName = MarketNet.NameOf(_owner);
            Sfx.UI(Synth.UiClick(), 0.4f);
            Broadcast(); Refresh();
        }

        void Release()
        {
            if (!Mine) return;
            _owner = 0; _ownerName = "";
            for (int i = 0; i < Slots; i++) { _json[i] = null; _price[i] = 0; }
            int st = _index;
            MarketNet.Send(w => { w.Write(MarketNet.StallRelease); w.Write((byte)st); });
            Refresh();
        }

        void Price(int i, int dir)
        {
            if (!Mine) return;
            int p = _price[i];
            int step = p < 1000 ? 100 : p < 10000 ? 500 : p < 50000 ? 2500 : 10000;
            _price[i] = Mathf.Clamp(p + dir * step, 0, 9999999);
            Broadcast(); Refresh();
        }

        void OnSlotChanged(int i)
        {
            if (_quiet || !Mine) return;
            var it = _slot[i].item;
            _json[i] = it != null ? MarketNet.Json(it) : null;
            int pi = PriceIdx(i);
            if (it != null && _price[pi] <= 0) _price[pi] = Mathf.Max(100, Mathf.RoundToInt(SellStation.Offer(it, out _) * 1.3f / 100f) * 100);   // a starting price you can change
            if (pi == Kit) { int sum = 0; for (int k = Kit; k < Slots; k++) if (_slot[k].item != null) sum += SellStation.Offer(_slot[k].item, out _); if (sum > 0 && it != null) _price[Kit] = Mathf.Max(_price[Kit], Mathf.RoundToInt(sum * 1.2f / 100f) * 100); }
            Broadcast(); Refresh();
        }

        void Broadcast()
        {
            if (!Mine) return;
            int st = _index; string name = _ownerName;
            var js = (string[])_json.Clone(); var pr = (int[])_price.Clone();
            MarketNet.Send(w =>
            {
                w.Write(MarketNet.Listing); w.Write((byte)st); w.Write(name);
                for (int i = 0; i < Slots; i++) { w.Write(js[i] ?? ""); w.Write(pr[i]); }
            });
        }

        void Buy(int i)
        {
            if (Mine || _owner == 0) return;
            var prof = Profile.Current;
            bool any = i == Hooks ? HasKit() : !string.IsNullOrEmpty(_json[i]);
            if (!any || prof == null) return;
            int slot = i == Hooks ? Kit : i;
            int price = _price[slot];
            if (_pendingSlot >= 0) { RagoGame.I.Player.Toast("Waiting for the last purchase..."); return; }
            if (prof.money < price) { Sfx.UI(Synth.UiError(), 0.4f); RagoGame.I.Player.Toast("Not enough money"); return; }
            // hold the money until the seller's game confirms
            prof.money -= price; _pendingSlot = slot; _pendingPaid = price; _pendingT = 20f;
            int st = _index;
            MarketNet.Send(w => { w.Write(MarketNet.BuyReq); w.Write((byte)st); w.Write((byte)slot); w.Write(price); }, _owner);
            Sfx.UI(Synth.UiClick(), 0.4f);
            Refresh();
        }

        bool HasKit() { for (int k = Kit; k < Slots; k++) if (!string.IsNullOrEmpty(_json[k])) return true; return false; }

        public void OnNet(byte op, ulong from, BinaryReader r)
        {
            switch (op)
            {
                case MarketNet.Listing:
                {
                    string name = r.ReadString();
                    var js = new string[Slots]; var pr = new int[Slots];
                    for (int i = 0; i < Slots; i++) { js[i] = r.ReadString(); pr[i] = r.ReadInt32(); }
                    if (Mine) { if (from < MarketNet.Me) Release(); else { Broadcast(); break; } }
                    _owner = from; _ownerName = name;
                    for (int i = 0; i < Slots; i++)
                    {
                        string j = string.IsNullOrEmpty(js[i]) ? null : js[i];
                        if (j != _json[i]) { _json[i] = j; Ghost(i); }
                        _price[i] = pr[i];
                    }
                    break;
                }
                case MarketNet.StallRelease:
                    if (_owner == from) Clear();
                    break;
                case MarketNet.BuyReq:
                {
                    int slot = r.ReadByte(), price = r.ReadInt32();
                    if (!Mine) break;
                    // still for sale at that price? hand it over
                    var give = new List<string>();
                    bool ok = price == _price[slot] && price >= 0;
                    if (ok)
                    {
                        _quiet = true;
                        int a = slot == Kit ? Kit : slot, b = slot == Kit ? Slots : slot + 1;
                        for (int k = a; k < b; k++)
                        {
                            var it = _slot[k].item; if (it == null) continue;
                            give.Add(MarketNet.Json(it));
                            _slot[k].Release(it); it.Consumed = true; Destroy(it.gameObject);
                            _json[k] = null;
                        }
                        _quiet = false;
                        ok = give.Count > 0;
                    }
                    int st = _index;
                    if (ok)
                    {
                        Profile.Current.money += price; Profile.Current.Save();
                        Sfx.UI(Synth.Cash(), 0.6f);
                        RagoGame.I.Player.Toast("SOLD to " + MarketNet.NameOf(from) + " for $" + price.ToString("N0"));
                        MarketNet.Send(w => { w.Write(MarketNet.Sold); w.Write((byte)st); w.Write((byte)slot); w.Write(price); w.Write((byte)give.Count); foreach (var j in give) w.Write(j); }, from);
                        Broadcast();
                    }
                    else MarketNet.Send(w => { w.Write(MarketNet.NoSale); w.Write((byte)st); w.Write((byte)slot); }, from);
                    break;
                }
                case MarketNet.Sold:
                {
                    int slot = r.ReadByte(); int paid = r.ReadInt32(); int n = r.ReadByte();
                    // it arrived after we'd given up and refunded: still yours - pay for it now
                    if (_pendingSlot != slot && Profile.Current != null) Profile.Current.money -= Mathf.Min(paid, Profile.Current.money);
                    for (int i = 0; i < n; i++)
                    {
                        var d = MarketNet.Data(r.ReadString()); if (d == null) continue;
                        ItemFactory.Spawn(d, _pickup.position + Vector3.up * 0.1f * i, _pickup.rotation);
                    }
                    _pendingSlot = -1;
                    Profile.Current?.Save();
                    Sfx.UI(Synth.Cash(), 0.6f);
                    RagoGame.I.Player.Toast("Bought from " + _ownerName + " - it's on the counter");
                    break;
                }
                case MarketNet.NoSale:
                {
                    int slot = r.ReadByte();
                    if (_pendingSlot != slot) break;
                    Refund("No longer for sale at that price - refunded");
                    break;
                }
            }
            Refresh();
        }

        void Refund(string why)
        {
            if (_pendingSlot < 0) return;
            if (Profile.Current != null) Profile.Current.money += _pendingPaid;
            _pendingSlot = -1; _pendingPaid = 0;
            RagoGame.I?.Player?.Toast(why);
        }

        void Clear()
        {
            _owner = 0; _ownerName = "";
            for (int i = 0; i < Slots; i++) { _json[i] = null; _price[i] = 0; Ghost(i); }
            if (_pendingSlot >= 0) Refund("The seller left - refunded");
        }

        void Ghost(int i)
        {
            if (_ghost[i] != null) Destroy(_ghost[i]);
            _ghost[i] = null;
            if (Mine || string.IsNullOrEmpty(_json[i])) return;
            float fit = i == 8 ? 0.9f : i >= Kit ? 0.45f : 0.4f;
            _ghost[i] = MarketNet.Ghost(MarketNet.Data(_json[i]), transform, _slot[i].transform.parent.localPosition, fit);
        }

        void Update()
        {
            if (_owner != 0 && !Mine && !MarketNet.Present(_owner)) { Clear(); Refresh(); }
            if (_pendingSlot >= 0 && (_pendingT -= Time.deltaTime) <= 0f) { Refund("The seller didn't answer - refunded"); Refresh(); }
            if (Mine && (_bcastT -= Time.deltaTime) <= 0f) { _bcastT = 4f; Broadcast(); }
        }

        void Refresh()
        {
            bool mine = Mine;
            _sign.text = _owner == 0 ? "FREE STALL - claim it to sell" : mine ? "YOUR STALL" : _ownerName.Split('#')[0].ToUpper() + "'S STALL";
            _claim.Label.text = mine ? "CLOSE STALL" : "CLAIM STALL";
            _claim.Interactable = _owner == 0 || mine;
            for (int i = 0; i <= Hooks; i++)
            {
                int pi = i == Hooks ? Kit : i;
                bool has = i == Hooks ? (mine ? KitReal() : HasKit()) : (mine ? _slot[i].item != null : !string.IsNullOrEmpty(_json[i]));
                _tag[i].text = has ? (i == Hooks ? "KIT  " : "") + "$" + _price[pi].ToString("N0") : "";
                _up[i].gameObject.SetActive(mine && has); _down[i].gameObject.SetActive(mine && has);
                _buy[i].gameObject.SetActive(!mine && has && _owner != 0);
                if (_buy[i].Label != null) _buy[i].Label.text = _pendingSlot == pi ? "..." : "BUY";
            }
        }

        bool KitReal() { for (int k = Kit; k < Slots; k++) if (_slot[k].item != null) return true; return false; }

        /// <summary>Leaving the Market: unsold items go back to your stash.</summary>
        public void SweepToStash(List<ItemData> into)
        {
            for (int i = 0; i < Slots; i++)
                if (_slot[i] != null && _slot[i].item != null) { var it = _slot[i].item; into.Add(it.Save()); _slot[i].Release(it); Destroy(it.gameObject); }
            if (Mine) Release();
            if (_pendingSlot >= 0) Refund("Left the Market - refunded");
        }

        void OnDestroy() { All.Remove(this); }
    }
}
