using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    // ======================================================================================= KIOSK
    /// <summary>
    /// Raid kiosk: pick a map, queue up and wait for other operators. Raids hold 4-10 players; the queue tries to
    /// reach 8 and launches with whoever is queued once the timer hits 1 minute.
    /// </summary>
    public class Kiosk : MonoBehaviour
    {
        string _map = "dockyard";
        TextMesh _status, _mapInfo, _title;
        WorldButton _queueBtn;
        readonly List<WorldButton> _mapBtns = new List<WorldButton>();
        bool _queued;
        Coroutine _offline;
        public static bool AnyQueued;

        public static Kiosk Create(Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Kiosk");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var k = go.AddComponent<Kiosk>();
            k.Build();
            return k;
        }

        void Build()
        {
            // terminal body
            var mb = new MB(3);
            mb.Sub(0).Box(new Vector3(0, 0.6f, 0.15f), new Vector3(1.6f, 1.2f, 0.5f), new Color(0.2f, 0.21f, 0.22f), 0.03f, 2);
            mb.Sub(0).Box(new Vector3(0, 1.75f, 0.2f), new Vector3(1.7f, 1.2f, 0.25f), new Color(0.16f, 0.17f, 0.18f), 0.03f, 2);
            mb.Sub(1).Box(new Vector3(0, 2.42f, 0.18f), new Vector3(1.7f, 0.12f, 0.3f), Color.white, 0.02f);
            mb.Sub(2).Box(new Vector3(0, 1.21f, -0.05f), new Vector3(1.5f, 0.03f, 0.25f), new Color(0.1f, 0.1f, 0.1f), 0.01f);
            mb.Build("Terminal", transform, Mats.Lit("kiosk_body", Color.white, 0.6f, 0.45f, Mats.Metal, null, 0.4f, 0.5f), Mats.Lit("kiosk_light", UI.Accent, 0, 0.5f, null, null, 0, 0, false, 0.5f, UI.Accent * 2f), Mats.Polymer);
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.2f, 0.2f); col.size = new Vector3(1.6f, 2.4f, 0.5f);
            var screen = UI.Panel(transform, "Screen", new Vector3(0, 1.75f, 0.06f), Quaternion.identity, new Vector2(1.55f, 1.05f), new Color(0.03f, 0.04f, 0.05f, 1f));
            _title = UI.Text(screen, "RAID DEPLOYMENT", new Vector3(0, 0.43f, 0), 0.06f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            for (int i = 0; i < Maps.RaidIds.Length; i++)
            {
                string id = Maps.RaidIds[i];
                var b = UI.Button(screen, Maps.Name(id), new Vector3(-0.5f + i * 0.5f, 0.27f, 0), new Vector2(0.46f, 0.11f), () => Select(id));
                _mapBtns.Add(b);
            }
            _mapInfo = UI.Text(screen, "", new Vector3(0, 0.12f, 0), 0.028f, UI.Dim);
            _status = UI.Text(screen, "", new Vector3(0, -0.07f, 0), 0.034f, Color.white);
            _queueBtn = UI.Button(screen, "QUEUE FOR RAID", new Vector3(0, -0.3f, 0), new Vector2(0.7f, 0.15f), ToggleQueue, new Color(0.2f, 0.45f, 0.25f));
            UI.Text(screen, "Raids: 4-10 operators  |  aims for 8  |  launches at 1:00 with whoever is in queue", new Vector3(0, -0.46f, 0), 0.018f, UI.Dim);
            Select(_map);
        }

        void Select(string id)
        {
            if (_queued) return;
            _map = id;
            for (int i = 0; i < _mapBtns.Count; i++) _mapBtns[i].SetColor(Maps.RaidIds[i] == id ? new Color(0.55f, 0.35f, 0.1f) : new Color(0.16f, 0.17f, 0.18f));
            _mapInfo.text = Maps.Blurb(id);
            _status.text = NetHub.Online ? "Online  -  " + NetHub.Backend.Status : "Offline  -  solo raids with AI (see Rago Extract > Setup to enable multiplayer)";
        }

        void ToggleQueue()
        {
            if (_queued) { Cancel(); return; }
            if (AnyQueued) return;
            _queued = true; AnyQueued = true;
            _queueBtn.SetText("CANCEL"); _queueBtn.SetColor(new Color(0.55f, 0.15f, 0.12f));
            Sfx.UI(Synth.Beep(), 0.5f);
            // save the loadout before matchmaking so a crash can't eat gear
            RagoGame.I.SaveHubState();
            if (NetHub.Online) NetHub.Backend.QueueRaid(_map, OnStatus, OnStart, OnFailed);
            else _offline = StartCoroutine(OfflineQueue());
        }

        void Cancel()
        {
            if (NetHub.Online) NetHub.Backend.CancelQueue();
            if (_offline != null) StopCoroutine(_offline);
            _queued = false; AnyQueued = false;
            _queueBtn.SetText("QUEUE FOR RAID"); _queueBtn.SetColor(new Color(0.2f, 0.45f, 0.25f));
            _status.text = "Queue cancelled";
        }

        void OnStatus(QueueStatus s)
        {
            if (!_queued) return;
            _status.text = s.text ?? QueueRules.Describe(s.players, s.elapsed);
        }

        void OnFailed(string why)
        {
            _status.text = "Matchmaking failed: " + why;
            Sfx.UI(Synth.UiError(), 0.4f);
            _queued = false; AnyQueued = false;
            _queueBtn.SetText("QUEUE FOR RAID"); _queueBtn.SetColor(new Color(0.2f, 0.45f, 0.25f));
        }

        void OnStart(RaidStart rs)
        {
            _queued = false; AnyQueued = false;
            Sfx.UI(Synth.QueueFound(), 0.7f);
            RagoGame.I.StartRaid(rs);
        }

        IEnumerator OfflineQueue()
        {
            // No matchmaking service: run the same queue rules locally and deploy solo with AI hostiles.
            float t = 0;
            const float soloWait = 5f;
            while (t < soloWait)
            {
                _status.text = "Searching for operators  1/" + QueueRules.Max + "\n<size=24>Offline - no other players. Deploying solo in " + Mathf.CeilToInt(soloWait - t) + "s</size>";
                t += Time.deltaTime;
                yield return null;
            }
            OnStart(new RaidStart { map = _map, seed = Random.Range(1, 1000000), spawnIndex = Random.Range(0, 10), players = 1, online = false });
        }

        void OnDestroy() { if (_queued) { AnyQueued = false; if (NetHub.Online) NetHub.Backend.CancelQueue(); } }
    }

    // ======================================================================================= TRADERS
    public class TraderDef
    {
        public string name, title;
        public Outfit outfit;
        public List<string> stock = new List<string>();
        public float sellRate = 0.55f;
        public System.Func<ItemDef, bool> buys;
        public Color color;
    }

    public class TraderStall : MonoBehaviour
    {
        TraderDef _t;
        int _page;
        Transform _panel;
        TextMesh _money, _sellLabel;
        readonly List<GameObject> _rows = new List<GameObject>();
        ItemContainer _delivery, _sell;
        HumanRig _npc;
        float _yaw;
        Vector3 _npcPos;

        public static TraderStall Create(Transform parent, Vector3 pos, float yaw, TraderDef def, int seed)
        {
            var go = new GameObject("Trader_" + def.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<TraderStall>();
            s._t = def;
            s.Build(seed);
            return s;
        }

        void Build(int seed)
        {
            var wood = Mats.Lit("stall_wood", Color.white, 0f, 0.3f, Mats.Wood, Mats.WoodN, 0.45f, 0.2f, true, 1f);
            var mb = new MB(3);
            // counter facing -Z (the customer stands at -Z)
            mb.Sub(0).Box(new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1.0f, 0.7f), new Color(0.6f, 0.45f, 0.32f), 0.02f, 2);
            mb.Sub(1).Box(new Vector3(0, 1.02f, -0.02f), new Vector3(3.3f, 0.05f, 0.8f), new Color(0.3f, 0.3f, 0.32f), 0.01f);
            // awning + posts
            mb.Sub(2).Box(new Vector3(0, 3.1f, 0.7f), new Vector3(4.2f, 0.06f, 2.8f), _t.color, 0.02f, 1, Quaternion.Euler(-12, 0, 0));
            foreach (var x in new[] { -2f, 2f }) foreach (var z in new[] { -0.5f, 2f }) mb.Sub(1).Cyl(new Vector3(x, 0, z), new Vector3(x, 3.1f, z), 0.05f, 0.05f, 8, new Color(0.25f, 0.25f, 0.25f));
            // back shelf
            mb.Sub(0).Box(new Vector3(0, 1.2f, 1.9f), new Vector3(3.6f, 2.4f, 0.4f), new Color(0.45f, 0.34f, 0.24f), 0.02f);
            for (int i = 0; i < 3; i++) mb.Sub(1).Box(new Vector3(0, 0.6f + i * 0.6f, 1.75f), new Vector3(3.5f, 0.04f, 0.4f), new Color(0.35f, 0.35f, 0.35f));
            mb.Build("Stall", transform, wood, Mats.Lit("stall_metal", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.4f, 0.4f), Mats.Lit("awning", Color.white, 0f, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0f));
            AddCol(new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1.04f, 0.7f));
            AddCol(new Vector3(0, 1.2f, 1.9f), new Vector3(3.6f, 2.4f, 0.4f));
            var sign = new GameObject("Sign").transform; sign.SetParent(transform, false); sign.localPosition = new Vector3(0, 2.85f, -0.75f);
            UI.Text(sign, _t.name.ToUpper(), new Vector3(0, 0.08f, 0), 0.12f, _t.color * 1.5f, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(sign, _t.title, new Vector3(0, -0.08f, 0), 0.05f, UI.Dim);
            // decorative wares on the back shelf
            var rng = new Rng(seed);
            for (int i = 0; i < Mathf.Min(6, _t.stock.Count); i++)
            {
                var it = ItemFactory.Spawn(rng.Pick(_t.stock), transform.TransformPoint(new Vector3(-1.4f + i * 0.56f, 1.25f + (i % 2) * 0.6f, 1.75f)), transform.rotation * Quaternion.Euler(0, 90, 0));
                if (it != null) { it.transform.SetParent(transform, true); Destroy(it.rb); foreach (var c in it.GetComponentsInChildren<Collider>()) Destroy(c); Destroy(it); }
            }
            // menu panel standing on the counter, angled towards the customer
            _panel = UI.Panel(transform, "Menu", new Vector3(0.35f, 1.55f, 0.05f), Quaternion.Euler(-12, 0, 0), new Vector2(1.5f, 0.95f));
            _money = UI.Text(_panel, "", new Vector3(0.68f, 0.41f, 0), 0.03f, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleRight);
            UI.Text(_panel, _t.name + " - BUY", new Vector3(-0.7f, 0.41f, 0), 0.035f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Button(_panel, "<", new Vector3(-0.55f, -0.4f, 0), new Vector2(0.12f, 0.09f), () => { _page = Mathf.Max(0, _page - 1); Refresh(); });
            UI.Button(_panel, ">", new Vector3(-0.4f, -0.4f, 0), new Vector2(0.12f, 0.09f), () => { _page++; Refresh(); });
            // trays on the counter
            _delivery = ItemContainer.Grid(transform, new Vector3(-1.1f, 1.08f, -0.05f), Quaternion.identity, 2, 2, new Vector2(0.36f, 0.3f), 0.3f);
            TrayVisual(new Vector3(-1.1f, 1.05f, -0.05f), "PICKUP", new Color(0.3f, 0.6f, 1f));
            _sell = ItemContainer.Grid(transform, new Vector3(1.15f, 1.08f, -0.05f), Quaternion.identity, 2, 2, new Vector2(0.36f, 0.3f), 0.3f, it => _t.buys == null || _t.buys(it.def));
            TrayVisual(new Vector3(1.15f, 1.05f, -0.05f), "SELL", new Color(1f, 0.6f, 0.2f));
            _sell.onChanged = Refresh;
            var sp = UI.Panel(transform, "SellPanel", new Vector3(1.15f, 1.3f, 0.32f), Quaternion.Euler(-20, 0, 0), new Vector2(0.7f, 0.22f));
            _sellLabel = UI.Text(sp, "", new Vector3(0, 0.05f, 0), 0.022f, Color.white);
            UI.Button(sp, "SELL ITEMS", new Vector3(0, -0.055f, 0), new Vector2(0.4f, 0.08f), Sell, new Color(0.55f, 0.35f, 0.1f));
            // the trader
            _npcPos = transform.TransformPoint(new Vector3(-0.2f, 0, 1.0f));
            _npc = HumanRig.Build(_t.outfit, seed, transform);
            _yaw = transform.eulerAngles.y + 180f;
            Refresh();
        }

        void AddCol(Vector3 c, Vector3 s) { var b = gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        void TrayVisual(Vector3 p, string label, Color c)
        {
            var mb = new MB();
            mb.Box(Vector3.zero, new Vector3(0.78f, 0.02f, 0.66f), c * 0.5f, 0.005f);
            var go = mb.Build("Tray", transform, Mats.Lit("tray_" + label, Color.white, 0.3f, 0.4f));
            go.transform.localPosition = p;
            UI.Text(transform, label, p + new Vector3(0, 0.02f, -0.36f), 0.025f, c).transform.localRotation = Quaternion.Euler(60, 0, 0);
        }

        const int PerPage = 6;

        void Refresh()
        {
            foreach (var r in _rows) Destroy(r);
            _rows.Clear();
            int pages = Mathf.Max(1, Mathf.CeilToInt(_t.stock.Count / (float)PerPage));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            for (int i = 0; i < PerPage; i++)
            {
                int idx = _page * PerPage + i;
                if (idx >= _t.stock.Count) break;
                var def = Db.Get(_t.stock[idx]);
                if (def == null) continue;
                var row = new GameObject("Row").transform;
                row.SetParent(_panel, false);
                row.localPosition = new Vector3(0, 0.3f - i * 0.11f, 0);
                _rows.Add(row.gameObject);
                UI.Text(row, def.name, new Vector3(-0.7f, 0.015f, 0), 0.026f, ItemDef.RarityColor(def.rarity), TextAnchor.MiddleLeft, FontStyle.Bold);
                string sub = def is GunDef g ? Db.CalName(g.cal) + "  " + g.magSize + "rd  " + g.action + (g.optics ? "  rail" : "") : def.rarity.ToString();
                UI.Text(row, sub, new Vector3(-0.7f, -0.025f, 0), 0.016f, UI.Dim, TextAnchor.MiddleLeft);
                int price = Price(def);
                UI.Text(row, "$" + price.ToString("N0"), new Vector3(0.38f, 0, 0), 0.026f, new Color(0.6f, 1f, 0.6f), TextAnchor.MiddleRight);
                var d2 = def;
                UI.Button(row, "BUY", new Vector3(0.58f, 0, 0), new Vector2(0.18f, 0.08f), () => Buy(d2));
            }
            _money.text = "$" + Profile.Current.money.ToString("N0");
            int v = 0, n = 0;
            foreach (var s in _sell.slots) if (s.item != null) { v += SellValue(s.item); n++; }
            _sellLabel.text = n == 0 ? "Put items on the SELL tray" : n + " item(s) for <color=#80ff80>$" + v.ToString("N0") + "</color>";
        }

        int Price(ItemDef d) => Mathf.RoundToInt(d.price * (d.rarity >= Rarity.Rare ? 1.6f : 1.25f) / 10f) * 10;
        int SellValue(Item it) => Mathf.RoundToInt(it.Value * _t.sellRate * (it.def != null && it.def.kind == ItemKind.Valuable ? 1.3f : 1f));

        void Buy(ItemDef d)
        {
            int price = Price(d);
            var prof = Profile.Current;
            if (prof.money < price) { Sfx.UI(Synth.UiError(), 0.4f); RagoGame.I.Player.Toast("Not enough money"); return; }
            if (d.kind == ItemKind.Ammo)
            {
                var sd = (SimpleDef)d;
                prof.money -= price;
                prof.stashAmmo[(int)sd.ammoCal] += sd.ammoCount;
                Sfx.UI(Synth.Cash(), 0.6f);
                RagoGame.I.Player.Toast("+" + sd.ammoCount + " " + Db.CalName(sd.ammoCal) + " sent to stash");
                Refresh();
                return;
            }
            var slot = _delivery.FirstFree(null);
            Slot free = null;
            foreach (var s in _delivery.slots) if (s.item == null) { free = s; break; }
            prof.money -= price;
            var it = ItemFactory.Spawn(new ItemData(d.id), free != null ? free.transform.position + Vector3.up * 0.1f : transform.TransformPoint(new Vector3(0, 1.2f, -0.2f)), transform.rotation);
            if (it != null && free != null && free.Accepts(it)) free.Store(it, false);
            Sfx.UI(Synth.Cash(), 0.6f);
            RagoGame.I.Player.Toast("Bought " + d.name);
            prof.Save();
            Refresh();
        }

        void Sell()
        {
            int total = 0;
            foreach (var s in _sell.slots)
                if (s.item != null) { total += SellValue(s.item); var it = s.item; s.Release(it); Destroy(it.gameObject); }
            if (total == 0) { Sfx.UI(Synth.UiError(), 0.3f); return; }
            Profile.Current.money += total;
            Profile.Current.Save();
            Sfx.UI(Synth.Cash(), 0.7f);
            RagoGame.I.Player.Toast("Sold for $" + total.ToString("N0"));
            Refresh();
        }

        void LateUpdate()
        {
            if (_npc == null) return;
            var p = RagoGame.I.Player;
            float t = Time.time + GetInstanceID() * 0.37f;
            Vector3 head = _npcPos + Vector3.up * (1.62f + Mathf.Sin(t * 1.6f) * 0.006f);
            Vector3 look = p.Head.position;
            bool near = Vector3.Distance(look, _npcPos) < 7f;
            Vector3 dir = near ? (look - head).normalized : transform.rotation * Quaternion.Euler(0, Mathf.Sin(t * 0.3f) * 30f, 0) * Vector3.back;
            Quaternion hr = Quaternion.Slerp(transform.rotation * Quaternion.Euler(0, 180, 0), Quaternion.LookRotation(dir), 0.8f);
            Quaternion yaw = Quaternion.Euler(0, _yaw, 0);
            // hands resting on the counter
            Vector3 hl = transform.TransformPoint(new Vector3(-0.45f, 1.1f, 0.25f));
            Vector3 hrp = transform.TransformPoint(new Vector3(0.1f + Mathf.Sin(t * 0.7f) * 0.05f, 1.1f, 0.22f));
            Quaternion handRot = yaw * Quaternion.Euler(70, 0, 0);
            _npc.Pose(_npcPos, _yaw, head, hr, hl, handRot, hrp, handRot, Vector3.zero, Time.deltaTime);
        }

        public void SweepToStash(List<ItemData> into)
        {
            foreach (var s in _delivery.slots) if (s.item != null) { into.Add(s.item.Save()); var it = s.item; s.Release(it); Destroy(it.gameObject); }
            foreach (var s in _sell.slots) if (s.item != null) { into.Add(s.item.Save()); var it = s.item; s.Release(it); Destroy(it.gameObject); }
        }

        public static List<TraderDef> Defaults()
        {
            Db.Init();
            var gunsmith = new TraderDef { name = "Viktor", title = "Gunsmith - rifles & shotguns", outfit = Outfit.Gunsmith, color = new Color(0.75f, 0.45f, 0.2f), sellRate = 0.5f };
            foreach (var g in Db.Guns) if (g.TwoHanded && g.rarity <= Rarity.Rare && g.cls != GunClass.SMG) gunsmith.stock.Add(g.id);
            gunsmith.buys = d => d.kind == ItemKind.Gun;
            var arms = new TraderDef { name = "Mara", title = "Quartermaster - sidearms, SMGs, optics, ammo", outfit = Outfit.Quartermaster, color = new Color(0.35f, 0.55f, 0.3f), sellRate = 0.5f };
            foreach (var g in Db.Guns) if ((!g.TwoHanded || g.cls == GunClass.SMG) && g.rarity <= Rarity.Rare) arms.stock.Add(g.id);
            foreach (var s in Db.Sights) if (s.rarity <= Rarity.Rare) arms.stock.Add(s.id);
            foreach (var m in Db.Misc) if (m.kind == ItemKind.Ammo) arms.stock.Add(m.id);
            arms.buys = d => d.kind == ItemKind.Gun || d.kind == ItemKind.Sight;
            var medic = new TraderDef { name = "Doc Hale", title = "Medic - meds & stims", outfit = Outfit.Medic, color = new Color(0.8f, 0.2f, 0.2f), sellRate = 0.6f };
            foreach (var m in Db.Misc) if (m.kind == ItemKind.Med) medic.stock.Add(m.id);
            medic.buys = d => d.kind == ItemKind.Med;
            var fence = new TraderDef { name = "Silas", title = "Fence - buys anything, sells keycards", outfit = Outfit.Fence, color = new Color(0.8f, 0.65f, 0.25f), sellRate = 0.65f };
            fence.stock.Add("key_vault"); fence.stock.Add("val_intel"); fence.stock.Add("val_drive");
            fence.buys = d => true;
            return new List<TraderDef> { gunsmith, arms, medic, fence };
        }
    }

    // ======================================================================================= STASH
    /// <summary>Your bunker stash: gun rack + shelves. Everything left here when you leave is saved.</summary>
    public class StashWall : MonoBehaviour
    {
        ItemContainer _rack, _pistols, _shelf;
        readonly List<ItemData> _overflow = new List<ItemData>();
        TextMesh _info, _ammo;

        public static StashWall Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Stash");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<StashWall>();
            s.Build();
            return s;
        }

        void Build()
        {
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 1.3f, 0.1f), new Vector3(5.2f, 2.6f, 0.08f), new Color(0.35f, 0.32f, 0.26f), 0.01f);
            for (int i = 0; i < 40; i++) for (int j = 0; j < 20; j++) if ((i + j) % 2 == 0) mb.Sub(1).Box(new Vector3(-2.5f + i * 0.128f, 0.1f + j * 0.128f, 0.055f), new Vector3(0.012f, 0.012f, 0.004f), Color.black);
            mb.Sub(0).Box(new Vector3(1.75f, 0.45f, -0.15f), new Vector3(1.7f, 0.04f, 0.5f), new Color(0.3f, 0.3f, 0.3f), 0.01f);
            mb.Sub(0).Box(new Vector3(1.75f, 1.05f, -0.15f), new Vector3(1.7f, 0.04f, 0.5f), new Color(0.3f, 0.3f, 0.3f), 0.01f);
            mb.Sub(0).Box(new Vector3(1.75f, 1.65f, -0.15f), new Vector3(1.7f, 0.04f, 0.5f), new Color(0.3f, 0.3f, 0.3f), 0.01f);
            mb.Build("Pegboard", transform, Mats.Lit("pegboard", Color.white, 0.1f, 0.3f, Mats.Wood, null, 0.3f, 0.1f), Mats.Polymer);
            var c = gameObject.AddComponent<BoxCollider>(); c.center = new Vector3(0, 1.3f, 0.1f); c.size = new Vector3(5.2f, 2.6f, 0.1f);
            UI.Text(transform, "STASH", new Vector3(0, 2.75f, 0.05f), 0.16f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            // long guns hang vertically on the left, pistols middle, small items on shelves right
            _rack = ItemContainer.Grid(transform, new Vector3(-1.3f, 1.35f, -0.05f), Quaternion.Euler(-90, 0, 0), 6, 1, new Vector2(0.42f, 1f), 1.2f, it => it is Gun, Quaternion.Euler(0, 0, 90));
            _pistols = ItemContainer.Grid(transform, new Vector3(0.45f, 1.5f, -0.05f), Quaternion.Euler(-90, 0, 0), 2, 3, new Vector2(0.28f, 0.3f), 0.25f, it => !(it is Backpack));
            _shelf = ItemContainer.Grid(transform, new Vector3(1.75f, 0.5f, -0.15f), Quaternion.identity, 4, 1, new Vector2(0.42f, 0.4f), 0.32f, it => !(it is Backpack));
            var s2 = ItemContainer.Grid(transform, new Vector3(1.75f, 1.1f, -0.15f), Quaternion.identity, 4, 1, new Vector2(0.42f, 0.4f), 0.32f, it => !(it is Backpack));
            var s3 = ItemContainer.Grid(transform, new Vector3(1.75f, 1.7f, -0.15f), Quaternion.identity, 4, 1, new Vector2(0.42f, 0.4f), 0.32f, it => !(it is Backpack));
            _shelf.slots.AddRange(s2.slots); _shelf.slots.AddRange(s3.slots);
            var panel = UI.Panel(transform, "StashPanel", new Vector3(-1.3f, 0.35f, -0.2f), Quaternion.Euler(-30, 0, 0), new Vector2(1.6f, 0.5f));
            _info = UI.Text(panel, "", new Vector3(-0.75f, 0.17f, 0), 0.024f, Color.white, TextAnchor.UpperLeft);
            _ammo = UI.Text(panel, "", new Vector3(0.05f, 0.17f, 0), 0.018f, UI.Dim, TextAnchor.UpperLeft);
            UI.Button(panel, "NEXT PAGE", new Vector3(-0.45f, -0.17f, 0), new Vector2(0.4f, 0.09f), NextPage);
            Load();
        }

        IEnumerable<Slot> AllSlots() { foreach (var s in _rack.slots) yield return s; foreach (var s in _pistols.slots) yield return s; foreach (var s in _shelf.slots) yield return s; }

        void Load()
        {
            var list = new List<ItemData>(Profile.Current.stash);
            _overflow.Clear();
            foreach (var d in list) Place(d);
            Refresh();
        }

        void Place(ItemData d)
        {
            var it = ItemFactory.Spawn(d, transform.position + Vector3.up, transform.rotation);
            if (it == null) return;
            it.transform.SetParent(null, true);
            Slot target = null;
            if (it is Gun g && g.g.TwoHanded) target = _rack.FirstFree(it);
            if (target == null) target = _pistols.FirstFree(it);
            if (target == null) target = _shelf.FirstFree(it);
            if (target == null) { _overflow.Add(d); Destroy(it.gameObject); return; }
            target.Store(it, false);
        }

        void NextPage()
        {
            if (_overflow.Count == 0) { RagoGame.I.Player.Toast("No more items"); return; }
            // swap the visible items with the hidden ones
            var visible = new List<ItemData>();
            foreach (var s in AllSlots()) if (s.item != null) { visible.Add(s.item.Save()); var it = s.item; s.Release(it); Destroy(it.gameObject); }
            var hidden = new List<ItemData>(_overflow);
            _overflow.Clear();
            foreach (var d in hidden) Place(d);
            _overflow.AddRange(visible);
            Refresh();
        }

        public List<ItemData> Save()
        {
            var l = new List<ItemData>();
            foreach (var s in AllSlots()) if (s.item != null) l.Add(s.item.Save());
            l.AddRange(_overflow);
            return l;
        }

        void Update() { if (Time.frameCount % 30 == 0) Refresh(); }

        void Refresh()
        {
            var prof = Profile.Current;
            int count = _overflow.Count; foreach (var s in AllSlots()) if (s.item != null) count++;
            _info.text = "<b>" + prof.name + "</b>\nCash  <color=#80ff80>$" + prof.money.ToString("N0") + "</color>\nItems  " + count + (_overflow.Count > 0 ? "  (+" + _overflow.Count + " hidden)" : "") +
                "\nRaids " + prof.raids + "  Extracts " + prof.extracts + "  Kills " + prof.kills;
            var sb = new System.Text.StringBuilder("AMMO RESERVE\n");
            var cals = System.Enum.GetValues(typeof(Caliber));
            int col = 0;
            foreach (Caliber cc in cals) { if (prof.stashAmmo[(int)cc] <= 0) continue; sb.Append(Db.CalName(cc)).Append(": ").Append(prof.stashAmmo[(int)cc]).Append(++col % 2 == 0 ? "\n" : "    "); }
            _ammo.text = sb.ToString();
        }
    }

    // ======================================================================================= MISC HUB PROPS
    public class SettingsBoard : MonoBehaviour
    {
        TextMesh _txt;
        public static SettingsBoard Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Settings");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<SettingsBoard>();
            var p = UI.Panel(go.transform, "Panel", Vector3.zero, Quaternion.identity, new Vector2(1.1f, 0.9f));
            UI.Text(p, "SETTINGS", new Vector3(0, 0.38f, 0), 0.045f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            s._txt = UI.Text(p, "", new Vector3(-0.5f, 0.28f, 0), 0.026f, Color.white, TextAnchor.UpperLeft);
            UI.Button(p, "Grip -", new Vector3(-0.3f, -0.05f, 0), new Vector2(0.22f, 0.08f), () => { Hand.GripPitch -= 5; s.Apply(); });
            UI.Button(p, "Grip +", new Vector3(-0.05f, -0.05f, 0), new Vector2(0.22f, 0.08f), () => { Hand.GripPitch += 5; s.Apply(); });
            UI.Button(p, "Turn mode", new Vector3(0.25f, -0.05f, 0), new Vector2(0.3f, 0.08f), () => { PlayerRig.SmoothTurn = !PlayerRig.SmoothTurn; s.Apply(); });
            UI.Button(p, "Snap 30/45/90", new Vector3(-0.2f, -0.17f, 0), new Vector2(0.4f, 0.08f), () => { PlayerRig.SnapAngle = PlayerRig.SnapAngle < 40 ? 45 : PlayerRig.SnapAngle < 60 ? 90 : 30; s.Apply(); });
            UI.Button(p, "Volume", new Vector3(0.25f, -0.17f, 0), new Vector2(0.3f, 0.08f), () => { Sfx.MasterVolume = Sfx.MasterVolume >= 1f ? 0.25f : Sfx.MasterVolume + 0.25f; AudioListener.volume = Sfx.MasterVolume; s.Apply(); });
            UI.Button(p, "Mute mic", new Vector3(-0.2f, -0.29f, 0), new Vector2(0.4f, 0.08f), () => { if (NetHub.Voice != null) NetHub.Voice.SetMuted(!NetHub.Voice.Muted); s.Apply(); });
            UI.Button(p, "New name", new Vector3(0.25f, -0.29f, 0), new Vector2(0.3f, 0.08f), () => { Profile.Current.name = "Operator-" + Random.Range(100, 999); s.Apply(); });
            s.Apply();
            return s;
        }

        void Apply()
        {
            var p = Profile.Current;
            p.gripPitch = Hand.GripPitch; p.smoothTurn = PlayerRig.SmoothTurn; p.snapAngle = PlayerRig.SnapAngle; p.volume = Sfx.MasterVolume;
            p.Save();
            _txt.text = "Name:  " + p.name + "\nGrip angle:  " + Hand.GripPitch + " deg\nTurning:  " + (PlayerRig.SmoothTurn ? "Smooth" : "Snap " + PlayerRig.SnapAngle) +
                "\nVolume:  " + Mathf.RoundToInt(Sfx.MasterVolume * 100) + "%\nVoice:  " + (NetHub.Voice == null ? "not installed" : NetHub.Voice.Status + (NetHub.Voice.Muted ? " (muted)" : "")) +
                "\nNetwork:  " + (NetHub.Backend == null ? "offline (install multiplayer in the Rago Extract tab)" : NetHub.Backend.Status);
        }
    }

    public static class HubProps
    {
        public static void TravelDoor(WorldBuilder b, Transform parent, Vector3 pos, float yaw, string label, System.Action go)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            var steel = Mats.Lit("door_steel", new Color(0.35f, 0.37f, 0.36f), 0.8f, 0.45f, Mats.Metal, Mats.MetalN, 0.4f, 0.5f);
            b.Box(pos + q * new Vector3(0, 1.3f, 0), new Vector3(2.2f, 2.6f, 0.15f), steel, Color.white, q, true, Synth.Surface.Metal, 0.02f);
            b.Box(pos + q * new Vector3(0, 1.3f, -0.08f), new Vector3(0.04f, 2.5f, 0.02f), Mats.Polymer, Color.white, q, false);
            b.Box(pos + q * new Vector3(0, 2.75f, -0.05f), new Vector3(2.5f, 0.3f, 0.2f), Mats.Lit("door_hazard", new Color(0.9f, 0.7f, 0.1f), 0, 0.4f), Color.white, q, false);
            var root = new GameObject("Door_" + label).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(pos + q * new Vector3(1.6f, 1.3f, -0.1f), q);
            UI.Button(root, label, Vector3.zero, new Vector2(0.6f, 0.2f), go, new Color(0.15f, 0.3f, 0.5f));
        }

        public static void SteelTarget(Transform parent, Vector3 pos, float size)
        {
            var anchor = new GameObject("TargetAnchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = pos;
            var arb = anchor.AddComponent<Rigidbody>(); arb.isKinematic = true;
            var plate = new MB().Box(Vector3.zero, new Vector3(size, size * 1.3f, 0.02f), new Color(0.85f, 0.85f, 0.82f), 0.004f).Build("Plate", parent, Mats.Lit("steel_target", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.3f, 0.6f));
            plate.transform.position = pos + Vector3.down * size * 0.9f;
            var bc = plate.AddComponent<BoxCollider>(); bc.size = new Vector3(size, size * 1.3f, 0.02f);
            plate.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            var rb = plate.AddComponent<Rigidbody>(); rb.mass = 6f; rb.SetDamping(0.3f, 1.5f);
            var hj = plate.AddComponent<HingeJoint>(); hj.connectedBody = arb; hj.anchor = new Vector3(0, size * 0.9f, 0); hj.axis = Vector3.right;
            var chain = new MB().Cyl(Vector3.zero, Vector3.down * size * 0.25f, 0.006f, 0.006f, 6, Color.gray).Build("Chain", anchor.transform, Mats.GunMetal);
        }
    }
}
