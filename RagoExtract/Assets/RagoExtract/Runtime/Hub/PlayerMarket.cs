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
    ///  - SHOPS on the black market screens: everyone (level 5+) has a shop. Pick a player's shop on a screen to see what
    ///    they sell and what they want for it - money, or an item (a gun, armour, a keycard...). BUY holds your money until
    ///    the seller's game hands it over; TRADE takes the item off your red OFFER square. Everything bought or swapped
    ///    goes straight to the buyer's / seller's bunker.
    /// Everyone only ever touches their own real items; other players see display copies.
    /// </summary>
    public static class MarketNet
    {
        public const byte Offer = 1, Release = 2, Accept = 3, Listing = 4, StallRelease = 5, BuyReq = 6, Sold = 7, NoSale = 8, TradeAsk = 9, SwapReq = 10, SwapDone = 11, SwapNo = 12;
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
            if (op == TradeAsk) { string what = r.ReadString(); RagoGame.I?.Player?.Toast(NameOf(from) + " wants to TRADE for your " + what + " - meet them at the TRADE TABLE"); Sfx.UI(Synth.UiClick(), 0.5f); return; }
            if (op <= Accept) { int t = r.ReadByte(); if (TradeTable.I != null && t == 0) TradeTable.I.OnNet(op, from, r); }
            else Shops.OnNet(op, from, r);
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
            public TextMesh sign, info, cash; public WorldButton claim, accept, plus, minus;
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
            // a sign board over the middle showing the whole deal, lettered on both faces
            var sb = new MB().Box(new Vector3(0, 2.05f, 0), new Vector3(1.9f, 0.5f, 0.04f), new Color(0.08f, 0.08f, 0.09f), 0.01f)
                .Box(new Vector3(-0.9f, 1.4f, 0), new Vector3(0.04f, 1.1f, 0.04f), new Color(0.2f, 0.2f, 0.2f)).Box(new Vector3(0.9f, 1.4f, 0), new Vector3(0.04f, 1.1f, 0.04f), new Color(0.2f, 0.2f, 0.2f));
            sb.Build("DealBoard", transform, MarketNet.Steel);
            _deal[0] = UI.Text(transform, "", new Vector3(0, 2.05f, -0.022f), 0.05f, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _deal[1] = UI.Text(transform, "", new Vector3(0, 2.05f, 0.026f), 0.05f, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _deal[1].transform.localRotation = Quaternion.Euler(0, 180, 0);
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
                // the control board stands up on your edge of the table, leaning back towards you; three clear rows of
                // text over a row of buttons (the text used to run together)
                // a low console along your edge of the table, tilted up like a lectern - you can see the items over it
                var ui = UI.Panel(sd.root, "TradeUI", new Vector3(0, 0.1f, -0.52f), Quaternion.Euler(55, 0, 0), new Vector2(1.9f, 0.42f), null, true, Skin.Steel);
                sd.sign = UI.Text(ui, "", new Vector3(-0.88f, 0.16f, -0.003f), 0.04f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                sd.info = UI.Text(ui, "", new Vector3(-0.88f, 0.09f, -0.003f), 0.03f, new Color(0.85f, 0.87f, 0.9f), TextAnchor.MiddleLeft);
                sd.cash = UI.Text(ui, "", new Vector3(-0.88f, 0.02f, -0.003f), 0.036f, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft, FontStyle.Bold);
                sd.claim = UI.Button(ui, "TRADE HERE", new Vector3(-0.62f, -0.12f, 0), new Vector2(0.4f, 0.11f), () => Claim(kk), new Color(0.2f, 0.4f, 0.6f), BtnStyle.Key);
                sd.minus = UI.Button(ui, "- $1000", new Vector3(-0.17f, -0.12f, 0), new Vector2(0.24f, 0.11f), () => Money(kk, -1000), new Color(0.35f, 0.35f, 0.35f), BtnStyle.Key);
                sd.plus = UI.Button(ui, "+ $1000", new Vector3(0.12f, -0.12f, 0), new Vector2(0.24f, 0.11f), () => Money(kk, 1000), new Color(0.35f, 0.35f, 0.35f), BtnStyle.Key);
                sd.accept = UI.Button(ui, "ACCEPT", new Vector3(0.6f, -0.12f, 0), new Vector2(0.4f, 0.11f), () => AcceptPress(kk), new Color(0.2f, 0.5f, 0.2f), BtnStyle.Key);
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

        string Gives(Side sd)
        {
            var names = new List<string>();
            foreach (var j in sd.items) { var d = MarketNet.Data(j); var df = d != null ? Db.Get(d.id) : null; if (df != null) names.Add(df.name); }
            return names.Count == 0 ? "no items" : string.Join(", ", names);
        }

        void Refresh()
        {
            int mine = Mine();
            for (int k = 0; k < 2; k++)
            {
                var sd = _s[k]; var other = _s[1 - k]; bool own = sd.owner == MarketNet.Me && sd.owner != 0;
                string who = sd.owner == 0 ? "" : own ? "YOU" : sd.name.Split('#')[0].ToUpper();
                sd.sign.text = sd.owner == 0 ? "FREE - press TRADE HERE" : who + (sd.accepted ? "   <color=#66ff66>ACCEPTED</color>" : "   (not accepted yet)");
                sd.sign.richText = true;
                int val = 0; foreach (var j in sd.items) { var d = MarketNet.Data(j); if (d != null && Db.Get(d.id) != null) val += Db.Get(d.id).price; }
                sd.info.text = sd.owner == 0 ? "" : UI.Wrap((own ? "YOU GIVE: " : who + " GIVES: ") + Gives(sd) + (val > 0 ? "  (~$" + val.ToString("N0") + ")" : ""), 55);
                // the money, big and clear: what you're paying and what you're getting
                sd.cash.text = sd.owner == 0 ? "" : (own ? "YOU PAY $" + sd.money.ToString("N0") : who + " PAYS $" + sd.money.ToString("N0"))
                    + (other.owner != 0 ? "      " + (own ? "YOU GET $" + other.money.ToString("N0") : "") : "");
                sd.claim.Label.text = own ? "LEAVE" : "TRADE HERE";
                sd.claim.Interactable = sd.owner == 0 || own;
                sd.accept.Interactable = own; sd.plus.Interactable = own; sd.minus.Interactable = own;
                sd.accept.Label.text = own && sd.accepted ? "UN-ACCEPT" : "ACCEPT";
            }
            // the deal, over the middle of the table - for both traders and anyone watching
            string Line(Side x) => x.owner == 0 ? "(nobody)" : x.name.Split('#')[0].ToUpper() + ":  " + Gives(x) + "  +  $" + x.money.ToString("N0") + (x.accepted ? "  [OK]" : "");
            string deal = Line(_s[0]) + "\n" + Line(_s[1]);
            foreach (var t in _deal) if (t != null) t.text = _s[0].owner == 0 && _s[1].owner == 0 ? "TRADE TABLE\nstand at a side and press TRADE HERE" : deal;
        }
        readonly TextMesh[] _deal = new TextMesh[2];

        public void SweepToStash(List<ItemData> into)
        {
            foreach (var sd in _s)
                foreach (var c in new[] { sd.grid, sd.tray })
                    foreach (var sl in c.slots) if (sl.item != null) { var it = sl.item; into.Add(it.Save()); sl.Release(it); Item.Discard(it); }
        }

        void OnDestroy() { if (I == this) I = null; }
    }

    // =========================================================================================== SHOPS
    /// <summary>One thing in a player's shop: the item, and either a price or what they want for it.</summary>
    [System.Serializable]
    public class ShopEntry { public int id; public ItemData item; public int price; public string want = ""; }   // want: "" = money, else the item id they want (price = how many)

    public static class Shops
    {
        public const int SellLevel = 5;   // you need level 5 to sell to other players
        public class Remote { public string name; public float heard; public readonly List<ShopEntry> entries = new List<ShopEntry>(); }
        public static readonly Dictionary<ulong, Remote> Others = new Dictionary<ulong, Remote>();
        public static System.Action Changed;

        static List<ShopEntry> Mine => Profile.Current != null ? (Profile.Current.shop ??= new List<ShopEntry>()) : new List<ShopEntry>();
        public static List<ShopEntry> MyEntries => Mine;

        // ---- pending purchases / swaps of mine, by (seller, entry id)
        class Pending { public ulong seller; public int id; public bool swap; public int paid; public string offer; public float t; }
        static readonly List<Pending> _pending = new List<Pending>();
        public static bool IsPending(ulong seller, int id) => _pending.Exists(p => p.seller == seller && p.id == id);
        static float _bcastT;

        public static string ItemName(ItemData d) { var def = d != null ? Db.Get(d.id) : null; return def != null ? def.name : "?"; }

        /// <summary>Called every frame by the screens in the Market.</summary>
        static int _tickFrame = -1;
        public static void Tick()
        {
            if (_tickFrame == Time.frameCount) return;   // several screens call this: once a frame
            _tickFrame = Time.frameCount;
            MarketNet.Hook();
            // shops of players who left disappear
            List<ulong> gone = null;
            foreach (var kv in Others) if (!MarketNet.Present(kv.Key)) (gone ??= new List<ulong>()).Add(kv.Key);
            if (gone != null) { foreach (var g in gone) Others.Remove(g); Changed?.Invoke(); }
            // a seller who left before answering: money back / your item back to your stash
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (MarketNet.Present(p.seller) && Time.time - p.t < 30f) continue;
                Undo(p, "The seller didn't answer");
                _pending.RemoveAt(i);
            }
            if ((_bcastT -= Time.deltaTime) <= 0f) { _bcastT = 4f; Broadcast(); }
        }

        public static void Broadcast()
        {
            if (!NetGame.Live || Profile.Current == null) return;
            var mine = new List<ShopEntry>(Mine);
            string name = Profile.Current.DisplayName;
            MarketNet.Send(w =>
            {
                w.Write(MarketNet.Listing); w.Write(name); w.Write((byte)Mathf.Min(mine.Count, 40));
                for (int i = 0; i < mine.Count && i < 40; i++) { var e = mine[i]; w.Write(e.id); w.Write(JsonUtility.ToJson(e.item)); w.Write(e.price); w.Write(e.want ?? ""); }
            });
        }

        public static bool List(Item it, int price, string want)
        {
            if (Profile.Current == null || it == null) return false;
            if (Levels.Current < SellLevel) { RagoGame.I.Player.Toast("You need to be level " + SellLevel + " to sell (you're " + Levels.Current + ")"); return false; }
            var prof = Profile.Current;
            var e = new ShopEntry { id = ++prof.shopNextId, item = it.Save(), price = Mathf.Max(0, price), want = want ?? "" };
            Mine.Add(e);
            if (it.slot != null) it.slot.Release(it);
            it.Consumed = true; Object.Destroy(it.gameObject);
            prof.Save(); Broadcast(); Changed?.Invoke();
            return true;
        }

        /// <summary>Take something out of your shop: it goes back to your stash.</summary>
        public static void Unlist(int id)
        {
            var e = Mine.Find(x => x.id == id); if (e == null) return;
            Mine.Remove(e);
            Profile.Deliver(e.item); Profile.Current.Save();
            RagoGame.I.Player.Toast(ItemName(e.item) + " taken off your shop - it's in your bunker");
            Broadcast(); Changed?.Invoke();
        }

        public static void Buy(ulong seller, ShopEntry e)
        {
            var prof = Profile.Current;
            if (prof == null || IsPending(seller, e.id)) return;
            if (prof.money < e.price) { Sfx.UI(Synth.UiError(), 0.4f); RagoGame.I.Player.Toast("Not enough money"); return; }
            prof.money -= e.price;   // held until the seller's game answers
            _pending.Add(new Pending { seller = seller, id = e.id, paid = e.price, t = Time.time });
            int id = e.id, price = e.price;
            MarketNet.Send(w => { w.Write(MarketNet.BuyReq); w.Write(id); w.Write(price); }, seller);
            Sfx.UI(Synth.UiClick(), 0.4f);
            Changed?.Invoke();
        }

        public static bool ForMoney(ShopEntry e) => string.IsNullOrEmpty(e.want);
        public static int Count(ShopEntry e) => ForMoney(e) ? 1 : Mathf.Max(1, e.price);   // item wants: how many they want
        public static void Swap(ulong seller, ShopEntry e, List<Item> onPad)
        {
            if (onPad == null || IsPending(seller, e.id)) return;
            var give = onPad.FindAll(x => x != null && ShopScreen.Matches(e.want, x.def));
            int need = Count(e);
            if (give.Count < need) { RagoGame.I.Player.Toast("They want " + need + " x " + ShopScreen.WantName(e.want) + " - put " + (need - give.Count) + " more on the red square"); return; }
            give = give.GetRange(0, need);
            var js = new List<string>();
            foreach (var it in give)
            {
                js.Add(JsonUtility.ToJson(it.Save()));
                if (it.slot != null) it.slot.Release(it);
                it.Consumed = true; Object.Destroy(it.gameObject);   // held for the swap: they come back if it falls through
            }
            string j = string.Join("\u0001", js);
            _pending.Add(new Pending { seller = seller, id = e.id, swap = true, offer = j, t = Time.time });
            int id = e.id;
            MarketNet.Send(w => { w.Write(MarketNet.SwapReq); w.Write(id); w.Write((byte)js.Count); foreach (var x in js) w.Write(x); }, seller);
            RagoGame.I.Player.Toast("Trade offer sent to " + MarketNet.NameOf(seller) + "...");
            Changed?.Invoke();
        }

        static void Undo(Pending p, string why)
        {
            var prof = Profile.Current; if (prof == null) return;
            if (p.swap) { foreach (var part in p.offer.Split('\u0001')) { var d = MarketNet.Data(part); if (d != null) Profile.Deliver(d); } why += " - your items are back in your bunker"; }
            else { prof.money += p.paid; why += " - refunded"; }
            prof.Save();
            RagoGame.I?.Player?.Toast(why);
            Changed?.Invoke();
        }

        public static void OnNet(byte op, ulong from, BinaryReader r)
        {
            var prof = Profile.Current;
            switch (op)
            {
                case MarketNet.Listing:
                {
                    string name = r.ReadString(); int n = r.ReadByte();
                    if (!Others.TryGetValue(from, out var sh)) Others[from] = sh = new Remote();
                    sh.name = name; sh.heard = Time.time; sh.entries.Clear();
                    for (int i = 0; i < n; i++) { var e = new ShopEntry { id = r.ReadInt32(), item = MarketNet.Data(r.ReadString()), price = r.ReadInt32(), want = r.ReadString() }; if (e.item != null) sh.entries.Add(e); }
                    Changed?.Invoke();
                    break;
                }
                case MarketNet.BuyReq:
                {
                    int id = r.ReadInt32(), price = r.ReadInt32();
                    var e = Mine.Find(x => x.id == id);
                    if (e != null && Shops.ForMoney(e) && e.price == price && prof != null)
                    {
                        Mine.Remove(e); prof.money += price; prof.Save();
                        Sfx.UI(Synth.Cash(), 0.6f);
                        RagoGame.I.Player.Toast("SOLD " + ItemName(e.item) + " to " + MarketNet.NameOf(from) + " for $" + price.ToString("N0"));
                        string j = JsonUtility.ToJson(e.item);
                        MarketNet.Send(w => { w.Write(MarketNet.Sold); w.Write(id); w.Write(price); w.Write(j); }, from);
                        Broadcast(); Changed?.Invoke();
                    }
                    else MarketNet.Send(w => { w.Write(MarketNet.NoSale); w.Write(id); }, from);
                    break;
                }
                case MarketNet.SwapReq:
                {
                    int id = r.ReadInt32(); int n = r.ReadByte();
                    var offers = new List<ItemData>(); for (int i = 0; i < n; i++) { var od = MarketNet.Data(r.ReadString()); if (od != null) offers.Add(od); }
                    var e = Mine.Find(x => x.id == id);
                    bool ok = e != null && !Shops.ForMoney(e) && offers.Count >= Count(e) && offers.TrueForAll(o => ShopScreen.Matches(e.want, Db.Get(o.id)));
                    if (ok && prof != null)
                    {
                        Mine.Remove(e); foreach (var od2 in offers) Profile.Deliver(od2); prof.Save();
                        Sfx.UI(Synth.Cash(), 0.6f);
                        RagoGame.I.Player.Toast("TRADED " + ItemName(e.item) + " to " + MarketNet.NameOf(from) + " for " + offers.Count + " x " + ShopScreen.WantName(e.want) + " - in your bunker");
                        string j = JsonUtility.ToJson(e.item);
                        MarketNet.Send(w => { w.Write(MarketNet.SwapDone); w.Write(id); w.Write(j); }, from);
                        Broadcast(); Changed?.Invoke();
                    }
                    else MarketNet.Send(w => { w.Write(MarketNet.SwapNo); w.Write(id); }, from);
                    break;
                }
                case MarketNet.Sold:
                case MarketNet.SwapDone:
                {
                    int id = r.ReadInt32(); int paid = op == MarketNet.Sold ? r.ReadInt32() : 0; var d = MarketNet.Data(r.ReadString());
                    int pi = _pending.FindIndex(p => p.seller == from && p.id == id);
                    if (pi >= 0) _pending.RemoveAt(pi);
                    else if (op == MarketNet.Sold && prof != null) prof.money -= Mathf.Min(paid, prof.money);   // arrived after we'd refunded: pay now
                    if (d != null && prof != null) { Profile.Deliver(d); prof.Save(); }
                    Sfx.UI(Synth.Cash(), 0.6f);
                    RagoGame.I.Player.Toast((op == MarketNet.Sold ? "BOUGHT " : "TRADE DONE: ") + ItemName(d) + " - it's in your bunker");
                    Changed?.Invoke();
                    break;
                }
                case MarketNet.NoSale:
                case MarketNet.SwapNo:
                {
                    int id = r.ReadInt32();
                    int pi = _pending.FindIndex(p => p.seller == from && p.id == id);
                    if (pi >= 0) { var p = _pending[pi]; _pending.RemoveAt(pi); Undo(p, "Not available any more"); }
                    break;
                }
            }
        }

        /// <summary>Leaving the Market with a purchase still waiting: undo it (money / item back).</summary>
        public static void LeaveMarket()
        {
            foreach (var p in _pending) Undo(p, "Left the Market");
            _pending.Clear(); Others.Clear();
        }
    }

    // =========================================================================================== SHOP SCREENS
    /// <summary>
    /// A black market screen. SHOPS lists everyone selling right now; open one to see what they sell and what they want
    /// in return (money, or an item). BUY / TRADE from there - anything you get goes to your bunker. MY SHOP: put an item
    /// on the blue SELL pad, pick a price or what you want for it (WANT), press LIST. You need level 5 to sell.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        // what you can ask for: money, or any specific item in the game - picked by category, then item
        public static readonly string[] Cats = { "MONEY", "GUNS", "ARMOUR", "BACKPACKS", "OPTICS", "ATTACHMENTS", "MEDS", "AMMO", "VALUABLES & KEYS", "GRENADES & MELEE", "JUNK & OTHER" };
        static readonly List<ItemDef>[] _catItems = new List<ItemDef>[Cats.Length];
        public static List<ItemDef> CatItems(int c)
        {
            if (_catItems[c] != null) return _catItems[c];
            Db.Init();
            var l = new List<ItemDef>();
            switch (c)
            {
                case 1: l.AddRange(Db.Guns); break;
                case 2: l.AddRange(Db.Armor); break;
                case 3: l.AddRange(Db.Packs); break;
                case 4: l.AddRange(Db.Sights); break;
                case 5: l.AddRange(Db.Attachments); break;
                case 6: l.AddRange(Db.Misc.FindAll(m => m.kind == ItemKind.Med)); break;
                case 7: l.AddRange(Db.Misc.FindAll(m => m.kind == ItemKind.Ammo)); break;
                case 8: l.AddRange(Db.Misc.FindAll(m => m.kind == ItemKind.Valuable || m.kind == ItemKind.Key)); break;
                case 9: l.AddRange(Db.Misc.FindAll(m => m.kind == ItemKind.Throwable || m.kind == ItemKind.Melee)); break;
                case 10: l.AddRange(Db.Misc.FindAll(m => m.kind != ItemKind.Med && m.kind != ItemKind.Ammo && m.kind != ItemKind.Valuable && m.kind != ItemKind.Key && m.kind != ItemKind.Throwable && m.kind != ItemKind.Melee)); break;
            }
            l.RemoveAll(d => d == null || d.rarity == Rarity.Boss);
            l.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return _catItems[c] = l;
        }
        public static string WantName(string id) { var d = string.IsNullOrEmpty(id) ? null : Db.Get(id); return d != null ? d.name : "money"; }
        /// <summary>Does this item count as what they asked for? The same item (any condition / attachments).</summary>
        public static bool Matches(string want, ItemDef d) => d != null && !string.IsNullOrEmpty(want) && d.id == want;

        enum Page { Shops, Shop, Mine }
        Page _page = Page.Shops; ulong _open; int _scroll;
        Transform _screen, _content; ItemContainer _pad; TextMesh _title, _status;
        int _price = 1000, _cat, _item, _count = 1, _pick;
        string WantId { get { if (_cat == 0) return ""; var l = CatItems(_cat); if (l.Count == 0) return ""; _item = (_item % l.Count + l.Count) % l.Count; return l[_item].id; } } bool _dirty = true; float _redrawT;
        List<Item> PadItems() { var l = new List<Item>(); foreach (var sl in _pad.slots) if (sl.item != null) l.Add(sl.item); return l; }

        public static ShopScreen Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("ShopScreen");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<ShopScreen>();
            s.Build();
            return s;
        }

        // the customer stands at local -Z, facing +Z
        void Build()
        {
            var mb = new MB();
            mb.Box(new Vector3(0, 0.75f, 0.1f), new Vector3(1.9f, 0.06f, 0.8f), new Color(0.15f, 0.15f, 0.16f), 0.01f);      // desk
            foreach (var x in new[] { -0.9f, 0.9f }) mb.Box(new Vector3(x, 0.37f, 0.1f), new Vector3(0.06f, 0.74f, 0.75f), new Color(0.12f, 0.12f, 0.13f));
            mb.Box(new Vector3(0, 1.42f, 0.42f), new Vector3(1.5f, 1.0f, 0.08f), new Color(0.05f, 0.05f, 0.06f), 0.02f);       // monitor
            mb.Box(new Vector3(0, 0.9f, 0.42f), new Vector3(0.12f, 0.3f, 0.06f), new Color(0.1f, 0.1f, 0.1f));
            mb.Box(new Vector3(0.45f, 0.785f, 0.02f), new Vector3(0.78f, 0.01f, 0.62f), new Color(0.75f, 0.08f, 0.06f), 0.003f); // the red square: what you sell, or what you offer
            mb.Build("Desk", transform, MarketNet.Steel);
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.39f, 0.1f); col.size = new Vector3(1.9f, 0.78f, 0.8f);
            var glow = new GameObject("ScreenLight").AddComponent<Light>(); glow.transform.SetParent(transform, false); glow.transform.localPosition = new Vector3(0, 1.4f, 0.0f);
            glow.type = LightType.Point; glow.color = new Color(0.3f, 1f, 0.5f); glow.range = 2.5f; glow.intensity = 0.7f;
            // the screen faces the customer (readable from -Z)
            _screen = UI.Panel(transform, "Screen", new Vector3(0, 1.42f, 0.37f), Quaternion.identity, new Vector2(1.42f, 0.92f), new Color(0.02f, 0.06f, 0.03f), true, Skin.Steel);
            _title = UI.Text(_screen, "", new Vector3(-0.67f, 0.4f, -0.003f), 0.045f, new Color(0.3f, 1f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _status = UI.Text(_screen, "", new Vector3(0.67f, 0.4f, -0.003f), 0.026f, new Color(0.3f, 0.8f, 0.4f), TextAnchor.MiddleRight);
            UI.Button(_screen, "SHOPS", new Vector3(-0.55f, -0.4f, 0), new Vector2(0.24f, 0.07f), () => Go(Page.Shops), new Color(0.1f, 0.3f, 0.14f), BtnStyle.Key);
            UI.Button(_screen, "MY MARKET", new Vector3(-0.28f, -0.4f, 0), new Vector2(0.26f, 0.07f), () => Go(Page.Mine), new Color(0.15f, 0.25f, 0.5f), BtnStyle.Key);
            UI.Button(_screen, "^", new Vector3(0.56f, -0.4f, 0), new Vector2(0.08f, 0.07f), () => { _scroll = Mathf.Max(0, _scroll - 1); _dirty = true; }, new Color(0.15f, 0.15f, 0.15f), BtnStyle.Key);
            UI.Button(_screen, "v", new Vector3(0.66f, -0.4f, 0), new Vector2(0.08f, 0.07f), () => { _scroll++; _dirty = true; }, new Color(0.15f, 0.15f, 0.15f), BtnStyle.Key);
            _content = new GameObject("Content").transform; _content.SetParent(_screen, false);
            // the red square: four spots; guns lie across it, barrel to the right (not pointing at you or the screen)
            var padRoot = new GameObject("RedSquare").transform; padRoot.SetParent(transform, false); padRoot.localPosition = new Vector3(0.45f, 0.8f, 0.02f);
            _pad = ItemContainer.Grid(padRoot, Vector3.zero, Quaternion.identity, 2, 2, new Vector2(0.38f, 0.3f), 0.34f, null, Quaternion.Euler(0, 90, 0));
            _pad.onChanged = () => _dirty = true;
            MarketNet.Label(transform, "PUT ITEMS HERE", new Vector3(0.45f, 0.792f, -0.3f), 0.034f, new Color(1f, 0.75f, 0.7f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Shops.Changed += MarkDirty;
            MarketNet.Hook();
        }

        void MarkDirty() => _dirty = true;
        void OnDestroy() { Shops.Changed -= MarkDirty; }   // don't keep old screens alive from the static event

        void Go(Page p) { _page = p; _scroll = 0; _dirty = true; Sfx.UI(Synth.UiClick(), 0.3f); }

        void Update()
        {
            Shops.Tick();
            if (_page != Page.Mine && (_redrawT -= Time.deltaTime) <= 0f) { _redrawT = 3f; _dirty = true; }   // keep other players' markets fresh
            if (_dirty) { _dirty = false; Draw(); }
        }

        void Row(int i, string a, string b, Color bc, string sub = null)
        {
            float y = 0.3f - i * 0.085f;
            UI.Text(_content, a, new Vector3(-0.67f, y, -0.003f), 0.032f, Color.white, TextAnchor.MiddleLeft);
            UI.Text(_content, b, new Vector3(-0.05f, y, -0.003f), 0.03f, bc, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (sub != null) UI.Text(_content, sub, new Vector3(-0.05f, y - 0.035f, -0.003f), 0.02f, UI.Dim, TextAnchor.MiddleLeft);
        }
        void Btn(int i, string label, float x, Color c, System.Action a, float w = 0.16f) => UI.Button(_content, label, new Vector3(x, 0.3f - i * 0.085f, 0), new Vector2(w, 0.06f), () => { a(); _dirty = true; }, c, BtnStyle.Key);

        void Draw()
        {
            foreach (Transform c in _content) Compat.KillWithMeshes(c.gameObject);   // (Destroy alone leaked every button's mesh)
            const int per = 7;
            bool live = NetGame.Live;
            switch (_page)
            {
                case Page.Shops:
                {
                    _title.text = "BLACK MARKET - PLAYER MARKETS";
                    var list = new List<KeyValuePair<ulong, Shops.Remote>>();
                    foreach (var kv in Shops.Others) if (kv.Value.entries.Count > 0) list.Add(kv);
                    _status.text = live ? list.Count + " shop(s) open" : "OFFLINE";
                    // your own market is always on the list, first
                    var myProf = Profile.Current;
                    int top = 0;
                    if (myProf != null && myProf.shopOpen)
                    {
                        var me = Shops.MyEntries;
                        Row(0, "YOUR MARKET", me.Count + " item(s)", new Color(0.5f, 0.8f, 1f), me.Count == 0 ? "nothing listed yet" : live ? "open to other players" : "closed (offline)");
                        Btn(0, "MANAGE", 0.55f, new Color(0.15f, 0.25f, 0.5f), () => Go(Page.Mine));
                        top = 1;
                    }
                    if (list.Count == 0) { UI.Text(_content, live ? "Nobody else is selling right now." + (top == 0 ? "\nOpen your own: MY MARKET." : "") : "The Market isn't online.", new Vector3(0, top == 1 ? -0.05f : 0.05f, -0.003f), 0.028f, new Color(0.3f, 0.8f, 0.4f), TextAnchor.MiddleCenter); break; }
                    _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, list.Count - (per - top)));
                    for (int i = 0; i < per - top && _scroll + i < list.Count; i++)
                    {
                        var kv = list[_scroll + i]; ulong who = kv.Key;
                        int money = 0, swaps = 0; foreach (var e in kv.Value.entries) if (Shops.ForMoney(e)) money++; else swaps++;
                        Row(i + top, kv.Value.name.Split('#')[0].ToUpper() + "'S MARKET", kv.Value.entries.Count + " item(s)", new Color(1f, 0.9f, 0.4f), (money > 0 ? money + " for money" : "") + (money > 0 && swaps > 0 ? ", " : "") + (swaps > 0 ? swaps + " for trade" : ""));
                        Btn(i + top, "OPEN", 0.55f, new Color(0.15f, 0.45f, 0.18f), () => { _open = who; Go(Page.Shop); });
                    }
                    break;
                }
                case Page.Shop:
                {
                    if (!Shops.Others.TryGetValue(_open, out var sh)) { _page = Page.Shops; Draw(); return; }
                    _title.text = sh.name.Split('#')[0].ToUpper() + "'S MARKET";
                    _status.text = "your $" + (Profile.Current != null ? Profile.Current.money.ToString("N0") : "0");
                    _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, sh.entries.Count - per));
                    for (int i = 0; i < per && _scroll + i < sh.entries.Count; i++)
                    {
                        var e = sh.entries[_scroll + i]; ulong who = _open;
                        bool pend = Shops.IsPending(who, e.id);
                        Row(i, Shops.ItemName(e.item), Asks(e), Shops.ForMoney(e) ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.6f, 0.9f));
                        if (pend) { UI.Text(_content, "waiting...", new Vector3(0.55f, 0.3f - i * 0.085f, -0.003f), 0.02f, UI.Dim, TextAnchor.MiddleCenter); continue; }
                        if (Shops.ForMoney(e)) Btn(i, "BUY", 0.55f, new Color(0.15f, 0.45f, 0.18f), () => Shops.Buy(who, e));
                        else Btn(i, "TRADE", 0.55f, new Color(0.6f, 0.15f, 0.12f), () => Shops.Swap(who, e, PadItems()));
                    }
                    UI.Text(_content, "BUY: your money is held until they hand it over.  TRADE: put what they want on the red square.  It all goes to your bunker.", new Vector3(0, -0.31f, -0.003f), 0.02f, UI.Dim, TextAnchor.MiddleCenter);
                    break;
                }
                case Page.Mine:
                {
                    var mine = Shops.MyEntries;
                    bool can = Levels.Current >= Shops.SellLevel;
                    _title.text = "MY MARKET";
                    if (!can)
                    {
                        _status.text = "level " + Levels.Current;
                        UI.Text(_content, "You need to be LEVEL " + Shops.SellLevel + " to open your own market.\nYou're level " + Levels.Current + " - raid, extract and get kills to level up.", new Vector3(0, 0.08f, -0.003f), 0.026f, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
                        break;
                    }
                    var prof = Profile.Current;
                    if (prof != null && !prof.shopOpen)
                    {
                        _status.text = "";
                        UI.Text(_content, "You don't have a market yet.", new Vector3(0, 0.15f, -0.003f), 0.03f, new Color(0.3f, 1f, 0.45f), TextAnchor.MiddleCenter);
                        UI.Button(_content, "CREATE MY MARKET", new Vector3(0, 0.0f, 0), new Vector2(0.5f, 0.09f), () => { prof.shopOpen = true; prof.Save(); Sfx.UI(Synth.Cash(), 0.4f); _dirty = true; }, new Color(0.15f, 0.45f, 0.18f), BtnStyle.Key);
                        break;
                    }
                    _status.text = (live ? "open" : "closed (offline)") + " - " + mine.Count + " for sale";
                    // what's for sale now (top), the listing controls (bottom)
                    int rows = 3;
                    _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, mine.Count - rows));
                    for (int i = 0; i < rows && _scroll + i < mine.Count; i++)
                    {
                        var e = mine[_scroll + i]; int id = e.id;
                        Row(i, Shops.ItemName(e.item), Asks(e), Shops.ForMoney(e) ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.6f, 0.9f));
                        Btn(i, "UNLIST", 0.55f, new Color(0.4f, 0.2f, 0.2f), () => Shops.Unlist(id));
                    }
                    if (mine.Count == 0) UI.Text(_content, "Nothing for sale yet - put items on the red square.", new Vector3(0, 0.25f, -0.003f), 0.022f, new Color(0.3f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
                    // 1. which item on the red square
                    var pad = PadItems();
                    _pick = pad.Count == 0 ? 0 : (_pick % pad.Count + pad.Count) % pad.Count;
                    float y = 0.0f;
                    UI.Text(_content, "1. SELL:", new Vector3(-0.67f, y, -0.003f), 0.026f, UI.Dim, TextAnchor.MiddleLeft);
                    UI.Text(_content, pad.Count == 0 ? "(put items on the red square)" : pad[_pick].def.name, new Vector3(-0.45f, y, -0.003f), 0.03f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                    if (pad.Count > 1) UI.Button(_content, "NEXT ITEM", new Vector3(0.5f, y, 0), new Vector2(0.24f, 0.06f), () => { _pick++; _dirty = true; }, new Color(0.25f, 0.25f, 0.3f), BtnStyle.Key);
                    // 2. what you want for it
                    y -= 0.09f;
                    // 2a. what kind of thing you want, 2b. exactly which one (every gun, armour, bag... in the game)
                    UI.Text(_content, "2. FOR:", new Vector3(-0.67f, y, -0.003f), 0.026f, UI.Dim, TextAnchor.MiddleLeft);
                    UI.Button(_content, "< ", new Vector3(-0.44f, y, 0), new Vector2(0.07f, 0.06f), () => { _cat = (_cat + Cats.Length - 1) % Cats.Length; _item = 0; _dirty = true; }, new Color(0.45f, 0.15f, 0.4f), BtnStyle.Key);
                    UI.Text(_content, Cats[_cat], new Vector3(-0.22f, y, -0.003f), 0.026f, new Color(1f, 0.6f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    UI.Button(_content, " >", new Vector3(0.0f, y, 0), new Vector2(0.07f, 0.06f), () => { _cat = (_cat + 1) % Cats.Length; _item = 0; _dirty = true; }, new Color(0.45f, 0.15f, 0.4f), BtnStyle.Key);
                    if (_cat != 0)
                    {
                        var cl = CatItems(_cat);
                        string wid = WantId;
                        UI.Button(_content, "<", new Vector3(0.1f, y, 0), new Vector2(0.06f, 0.06f), () => { _item--; _dirty = true; }, new Color(0.3f, 0.15f, 0.35f), BtnStyle.Key);
                        UI.Text(_content, cl.Count == 0 ? "-" : WantName(wid) + "  (" + (_item + 1) + "/" + cl.Count + ")", new Vector3(0.42f, y, -0.003f), 0.024f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                        UI.Button(_content, ">", new Vector3(0.68f, y, 0), new Vector2(0.06f, 0.06f), () => { _item++; _dirty = true; }, new Color(0.3f, 0.15f, 0.35f), BtnStyle.Key);
                    }
                    // 3. how much / how many
                    y -= 0.09f;
                    UI.Text(_content, _cat == 0 ? "3. PRICE:" : "3. HOW MANY:", new Vector3(-0.67f, y, -0.003f), 0.026f, UI.Dim, TextAnchor.MiddleLeft);
                    UI.Button(_content, "-", new Vector3(-0.3f, y, 0), new Vector2(0.07f, 0.06f), () => { if (_cat == 0) Step(-1); else _count = Mathf.Max(1, _count - 1); _dirty = true; }, new Color(0.2f, 0.2f, 0.2f), BtnStyle.Key);
                    UI.Text(_content, _cat == 0 ? "$" + _price.ToString("N0") : _count + " x", new Vector3(-0.1f, y, -0.003f), 0.034f, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    UI.Button(_content, "+", new Vector3(0.1f, y, 0), new Vector2(0.07f, 0.06f), () => { if (_cat == 0) Step(1); else _count = Mathf.Min(4, _count + 1); _dirty = true; }, new Color(0.2f, 0.2f, 0.2f), BtnStyle.Key);
                    UI.Button(_content, "LIST IT", new Vector3(0.5f, y, 0), new Vector2(0.24f, 0.07f), () =>
                    {
                        var items = PadItems();
                        if (items.Count == 0) { RagoGame.I.Player.Toast("Put what you're selling on the red square first"); return; }
                        var it = items[(_pick % items.Count + items.Count) % items.Count];
                        if (_cat != 0 && string.IsNullOrEmpty(WantId)) { RagoGame.I.Player.Toast("Pick what you want for it"); return; }
                        if (Shops.List(it, _cat == 0 ? _price : _count, WantId)) { Sfx.UI(Synth.Cash(), 0.4f); RagoGame.I.Player.Toast(it.def.name + " is in your market"); }
                        _dirty = true;
                    }, new Color(0.6f, 0.15f, 0.12f), BtnStyle.Key);
                    UI.Text(_content, "Your market is open while you're in the Market. UNLIST sends an item back to your bunker.", new Vector3(0, -0.31f, -0.003f), 0.02f, UI.Dim, TextAnchor.MiddleCenter);
                    break;
                }
            }
        }

        static string Asks(ShopEntry e) => Shops.ForMoney(e) ? "$" + e.price.ToString("N0") : "WANTS " + Shops.Count(e) + " x " + WantName(e.want);

        void Step(int dir)
        {
            int step = _price < 1000 ? 100 : _price < 10000 ? 500 : _price < 50000 ? 2500 : 10000;
            _price = Mathf.Clamp(_price + dir * step, 100, 9999999);
        }

        public void SweepToStash(List<ItemData> into)
        {
            foreach (var sl in _pad.slots)
                if (sl.item != null) { var it = sl.item; into.Add(it.Save()); sl.Release(it); Item.Discard(it); }
        }
    }
}
