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
        bool _queued, _joining;
        float _queueStart;
        Coroutine _offline;
        public static bool AnyQueued;
        public static Kiosk Current;
        public string SelectedMap => _map;
        public static event System.Action<string> OnMapSelected;
        /// <summary>Pick a raid from elsewhere (the world map board).</summary>
        public void SelectMap(string id) { if (_queued) { RagoGame.I.Player.Toast("Cancel the queue to change the map"); return; } Select(id); Sfx.UI(Synth.Beep(), 0.3f); }

        public static Kiosk Create(Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Kiosk");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var k = go.AddComponent<Kiosk>();
            Current = k;
            k.Build();
            return k;
        }

        Material _beacon;
        Light _beaconLight;

        void Build()
        {
            // Deployment station: a steel frame built around the big map screen, a header sign on top and a sloped
            // desk below with the QUEUE key.
            Color gun = UI.Lin(new Color(0.2f, 0.21f, 0.22f)), gunLt = UI.Lin(new Color(0.32f, 0.33f, 0.34f)), olive = UI.Lin(new Color(0.28f, 0.31f, 0.23f)),
                  blk = UI.Lin(new Color(0.05f, 0.05f, 0.055f)), steel = UI.Lin(new Color(0.45f, 0.46f, 0.47f)), yel = UI.Lin(new Color(0.95f, 0.75f, 0.1f));
            var mb = new MB(3);
            mb.Sub(0).Box(new Vector3(0, 0.025f, 0.15f), new Vector3(2.56f, 0.05f, 0.8f), gun, 0.01f, 1);                    // base plate
            for (int i = 0; i < 18; i++)                                                                                     // hazard edge on the base
                mb.Sub(1).Box(new Vector3(-1.19f + i * 0.14f, 0.026f, -0.235f), new Vector3(0.06f, 0.052f, 0.03f), i % 2 == 0 ? yel : blk, 0f, 0, Quaternion.Euler(0, 35, 0));
            foreach (var sx in new[] { -1, 1 })
            {
                mb.Sub(0).Box(new Vector3(sx * 1.2f, 1.47f, 0.12f), new Vector3(0.12f, 2.84f, 0.2f), gunLt, 0.015f, 2);      // posts
                mb.Sub(0).Box(new Vector3(sx * 1.2f, 0.09f, 0.12f), new Vector3(0.24f, 0.08f, 0.36f), gun, 0.01f);            // post feet
                foreach (var y in new[] { 0.4f, 1.47f, 2.55f })
                    mb.Sub(2).Cyl(new Vector3(sx * 1.2f, y, 0.019f), new Vector3(sx * 1.2f, y, 0.012f), 0.014f, 0.012f, 8, steel); // bolts
            }
            mb.Sub(0).Box(new Vector3(0, 2.0f, 0.1f), new Vector3(2.28f, 1.44f, 0.14f), blk, 0.02f, 1);                      // screen bezel
            mb.Sub(0).Box(new Vector3(0, 2.83f, 0.12f), new Vector3(2.52f, 0.16f, 0.3f), olive, 0.02f, 2);                   // header sign
            mb.Sub(1).Box(new Vector3(0, 2.745f, 0.05f), new Vector3(2.28f, 0.015f, 0.12f), yel, 0.002f);                    // trim under the header
            mb.Sub(0).Box(new Vector3(0, 0.55f, 0.2f), new Vector3(1.5f, 1.0f, 0.3f), olive, 0.02f, 2);                      // lower cabinet
            for (int i = 0; i < 6; i++) mb.Sub(1).Box(new Vector3(0, 0.3f + i * 0.05f, 0.048f), new Vector3(0.6f, 0.02f, 0.006f), blk, 0.002f); // vent slats
            mb.Sub(0).Box(new Vector3(0, 1.08f, -0.1f), new Vector3(1.7f, 0.06f, 0.5f), gun, 0.015f, 1, Quaternion.Euler(-35, 0, 0)); // sloped desk
            mb.Sub(1).Box(new Vector3(0, 1.235f, 0.12f), new Vector3(1.7f, 0.03f, 0.06f), blk, 0.005f);                      // desk back lip
            mb.Sub(1).Cyl(new Vector3(0.5f, 0.06f, 0.4f), new Vector3(0.5f, -0.05f, 0.4f), 0.04f, 0.04f, 10, blk);          // conduit into the floor
            mb.Sub(2).Cyl(new Vector3(0.95f, 2.91f, 0.12f), new Vector3(0.95f, 2.97f, 0.12f), 0.05f, 0.05f, 12, steel);      // beacon base
            mb.Build("Terminal", transform, Mats.Lit("kiosk_frame", Color.white, 0.55f, 0.4f, Mats.Metal, Mats.MetalN, 0.55f, 0.6f), Mats.Lit("kiosk_paint", Color.white, 0.1f, 0.35f, Mats.Noise, null, 0.4f, 0.3f), Mats.Lit("kiosk_steel", Color.white, 0.85f, 0.5f, Mats.Metal, null, 0.3f, 0.3f));
            // amber beacon (flashes while you're queued)
            var dome = new MB().Sphere(new Vector3(0.95f, 2.98f, 0.12f), new Vector3(0.05f, 0.07f, 0.05f), 12, Color.white, 0f, 0.5f).Build("Beacon", transform, Mats.Lit("kiosk_beacon", new Color(1f, 0.55f, 0.1f), 0, 0.8f));
            _beacon = new Material(dome.GetComponent<MeshRenderer>().sharedMaterial); dome.GetComponent<MeshRenderer>().sharedMaterial = _beacon;
            _beaconLight = new GameObject("BeaconLight").AddComponent<Light>(); _beaconLight.transform.SetParent(transform, false); _beaconLight.transform.localPosition = new Vector3(0.95f, 3.03f, 0.02f);
            _beaconLight.type = LightType.Point; _beaconLight.color = new Color(1f, 0.55f, 0.15f); _beaconLight.range = 3f; _beaconLight.intensity = 0f;
            // colliders: the screen one sits behind the map so the laser reaches the map's location buttons
            void Col(Vector3 c, Vector3 sz) { var bc = gameObject.AddComponent<BoxCollider>(); bc.center = c; bc.size = sz; }
            Col(new Vector3(0, 2.0f, 0.14f), new Vector3(2.28f, 1.44f, 0.06f));
            Col(new Vector3(0, 2.83f, 0.12f), new Vector3(2.52f, 0.16f, 0.3f));
            Col(new Vector3(-1.2f, 1.47f, 0.12f), new Vector3(0.12f, 2.84f, 0.2f));
            Col(new Vector3(1.2f, 1.47f, 0.12f), new Vector3(0.12f, 2.84f, 0.2f));
            Col(new Vector3(0, 0.55f, 0.2f), new Vector3(1.5f, 1.0f, 0.3f));
            Col(new Vector3(0, 1.0f, -0.05f), new Vector3(1.7f, 0.14f, 0.45f));
            Col(new Vector3(0, 0.025f, 0.15f), new Vector3(2.56f, 0.05f, 0.8f));
            // stencilled markings
            UI.Text(transform, "RAID DEPLOYMENT", new Vector3(0, 2.83f, -0.035f), 0.075f, UI.Stencil, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "RDT-8", new Vector3(-1.1f, 2.83f, -0.035f), 0.045f, UI.Stencil, TextAnchor.MiddleLeft, FontStyle.Bold);
            _online = UI.Text(transform, "", new Vector3(1.12f, 2.83f, -0.035f), 0.042f, new Color(0.45f, 1f, 0.55f), TextAnchor.MiddleRight, FontStyle.Bold);
            UpdateOnline();
            UI.Text(transform, "NO WEAPONS ON THE DESK", new Vector3(0.7f, 0.2f, 0.044f), 0.018f, UI.Lin(new Color(0.85f, 0.85f, 0.8f)), TextAnchor.MiddleRight);

            // the screen: a big map of the raid locations - point at one and pull the trigger to pick it
            WorldMapBoard.Create(transform, new Vector3(0, 2.0f, 0.0f), Quaternion.identity, 0.82f);
            _mapInfo = UI.Text(transform, "", Vector3.zero, 0.04f, UI.CrtDim, TextAnchor.UpperLeft);
            _status = UI.Text(transform, "", Vector3.zero, 0.036f, UI.CrtGreen, TextAnchor.UpperLeft);
            _mapInfo.gameObject.SetActive(false); _status.gameObject.SetActive(false);
            // the console: one big push key
            var console = new GameObject("Console").transform; console.SetParent(transform, false);
            console.localPosition = new Vector3(0, 1.124f, -0.131f); console.localRotation = Quaternion.Euler(55, 0, 0);
            _queueBtn = UI.Button(console, "QUEUE", new Vector3(0.1f, 0, 0), new Vector2(0.34f, 0.15f), ToggleQueue, new Color(0.2f, 0.42f, 0.2f), BtnStyle.Key);
            UI.Text(console, "PRESS TO\nDEPLOY", new Vector3(-0.3f, 0, -0.002f), 0.03f, UI.Stencil, TextAnchor.MiddleCenter, FontStyle.Bold);
            // your party, on a little screen beside the QUEUE key: who's in it and who leads
            var pp = UI.Panel(console, "PartyScreen", new Vector3(0.56f, 0, -0.003f), Quaternion.identity, new Vector2(0.5f, 0.34f), null, true, Skin.Crt);
            UI.Text(pp, "PARTY", new Vector3(-0.22f, 0.13f, 0), 0.03f, UI.CrtGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            _party = UI.Text(pp, "", new Vector3(-0.22f, 0.09f, 0), 0.022f, UI.CrtGreen, TextAnchor.UpperLeft);
            UpdateParty();
            Select(_map);
        }

        TextMesh _online, _party;
        float _onlineT, _partyT;

        /// <summary>The party screen on the console: members (leader marked), or how to start one.</summary>
        void UpdateParty()
        {
            if (_party == null) return;
            var b = NetHub.Backend;
            if (!NetHub.Online || b == null) { _party.text = "offline - solo raids\nwith AI"; return; }
            if (b.PartyCode == null) { _party.text = "no party\n\ninvite friends: menu\nbutton > FRIENDS"; return; }
            var sb = new System.Text.StringBuilder();
            foreach (var m in b.PartyMembers) sb.Append("> ").Append(m.ToUpper()).Append('\n');
            sb.Append(b.PartyLeader ? "\nyou lead: QUEUE takes\nthe whole party" : "\nyour leader queues -\nyou follow them in");
            _party.text = sb.ToString();
        }

        /// <summary>The live count of people playing right now (from Photon), on the header sign.</summary>
        void UpdateOnline()
        {
            if (_online == null) return;
            int n = NetHub.Online && NetHub.Backend != null ? NetHub.Backend.OnlineCount : -1;
            if (!NetHub.Online) { _online.text = "OFFLINE"; _online.color = new Color(0.6f, 0.6f, 0.6f); }
            else if (n <= 0) { _online.text = "PLAYERS ONLINE  ..."; _online.color = new Color(0.45f, 1f, 0.55f); }
            else { _online.text = n + (n == 1 ? " PLAYER ONLINE" : " PLAYERS ONLINE"); _online.color = new Color(0.45f, 1f, 0.55f); }
        }

        void Update()
        {
            _onlineT -= Time.deltaTime;
            if (_onlineT <= 0f) { _onlineT = 2f; UpdateOnline(); }
            _partyT -= Time.deltaTime;
            if (_partyT <= 0f) { _partyT = 1f; UpdateParty(); }
            float on = _queued && Mathf.Repeat(Time.time, 0.8f) < 0.4f ? 1f : 0f;
            if (_beacon != null) _beacon.SetColor("_EmissionColor", new Color(3f, 1.4f, 0.2f) * on);
            if (_beaconLight != null) _beaconLight.intensity = on * 1.5f;
        }

        void Select(string id)
        {
            if (_queued) return;
            _map = id;
            for (int i = 0; i < _mapBtns.Count; i++)
            {
                bool sel = Maps.RaidIds[i] == id;
                _mapBtns[i].SetColor(sel ? new Color(0.3f, 0.85f, 0.4f) : new Color(0.03f, 0.1f, 0.05f));
                _mapBtns[i].SetTextColor(sel ? new Color(0.02f, 0.08f, 0.03f) : UI.CrtGreen);
                _mapBtns[i].SetText((sel ? "> " : "  ") + Maps.Name(Maps.RaidIds[i]).ToUpper());
            }
            _mapInfo.text = "";
            OnMapSelected?.Invoke(id);
            _status.text = NetHub.Online ? "" : "> " + UI.Wrap(NetHub.StatusText.ToUpper(), 44).Replace("\n", "\n  ");   // only shown when something is wrong
        }

        void ToggleQueue()
        {
            if (_joining) return;   // raid already found: on the way in
            if (_queued) { Cancel(); return; }
            if (AnyQueued) return;
            if (NetHub.Online && NetHub.Backend.PartyCode != null && NetHub.Backend.PartyMembers.Count > QueueRules.PartyMaxFor(_map))
            {
                RagoGame.I.Player.Toast(Maps.Name(_map) + " is for squads of up to " + QueueRules.PartyMaxFor(_map) + " - your party has " + NetHub.Backend.PartyMembers.Count);
                Sfx.UI(Synth.UiError(), 0.3f);
                return;
            }
            if (NetHub.Online && NetHub.Backend.PartyCode != null && !NetHub.Backend.PartyLeader)
            {
                RagoGame.I.Player.Toast("Only your party leader can start the queue - you'll follow them in");
                Sfx.UI(Synth.UiError(), 0.3f);
                return;
            }
            StartQueue();
        }

        void StartQueue()
        {
            _queued = true; AnyQueued = true; _joining = false; _queueStart = Time.time;
            HudQueue("FINDING RAID", true);
            _queueBtn.SetText("CANCEL"); _queueBtn.SetColor(new Color(0.6f, 0.14f, 0.1f));
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
            _queueBtn.SetText("QUEUE"); _queueBtn.SetColor(new Color(0.2f, 0.42f, 0.2f));
            _status.text = "> QUEUE CANCELLED";
            HudQueue("");
        }

        void OnStatus(QueueStatus s)
        {
            if (!_queued) return;
            _status.text = "> " + UI.Wrap((s.text ?? QueueRules.Describe(s.players, s.elapsed)).ToUpper(), 44);
        }

        void OnFailed(string why)
        {
            _status.text = "> MATCHMAKING FAILED: " + (why ?? "").ToUpper();
            HudQueue("");
            RagoGame.I?.Player?.Toast("Couldn't find a raid: " + (why ?? "matchmaking failed"));
            Sfx.UI(Synth.UiError(), 0.4f);
            _queued = false; AnyQueued = false;
            _queueBtn.SetText("QUEUE"); _queueBtn.SetColor(new Color(0.2f, 0.42f, 0.2f));
        }

        void OnStart(RaidStart rs)
        {
            if (_joining) return;
            _joining = true;
            Sfx.UI(Synth.QueueFound(), 0.7f);
            StartCoroutine(Join(rs));
        }

        // top-left: RAID FOUND, then JOINING RAID, then off to the raid (the line is cleared once you arrive)
        IEnumerator Join(RaidStart rs)
        {
            HudQueue("RAID FOUND");
            yield return new WaitForSeconds(1.5f);
            HudQueue("JOINING RAID", true);   // keeps counting through the loading screen until you're in
            yield return new WaitForSeconds(0.5f);
            _queued = false; AnyQueued = false; _joining = false;
            RagoGame.I.StartRaid(rs);
        }

        static void HudQueue(string s, bool timer = false) { var pl = RagoGame.I != null ? RagoGame.I.Player : null; if (pl != null && pl.Hud != null) pl.Hud.Queue(s, timer); }

        IEnumerator OfflineQueue()
        {
            // No matchmaking service: run the same queue rules locally and deploy solo with AI hostiles.
            float t = 0;
            const float soloWait = 5f;
            while (t < soloWait)
            {
                _status.text = "> SEARCHING FOR OPERATORS  1/" + QueueRules.MaxFor(_map) + "\n> OFFLINE - DEPLOYING SOLO IN " + Mathf.CeilToInt(soloWait - t) + "S";
                t += Time.deltaTime;
                yield return null;
            }
            OnStart(new RaidStart { map = _map, seed = Random.Range(1, 1000000), spawnIndex = Random.Range(0, 10), players = 1, online = false });
        }

        void OnDestroy()
        {
            if (NetHub.Backend != null) { NetHub.Backend.OnPartyQueue -= OnPartyQueue; NetHub.Backend.OnPartyQueueCancelled -= OnPartyCancelled; }
            if (_queued) { AnyQueued = false; if (!_joining) { HudQueue(""); if (NetHub.Online) NetHub.Backend.CancelQueue(); } }
        }

        void Start()
        {
            if (NetHub.Backend != null) { NetHub.Backend.OnPartyQueue += OnPartyQueue; NetHub.Backend.OnPartyQueueCancelled += OnPartyCancelled; }
        }

        // your party leader pressed QUEUE: follow them into the same raid
        void OnPartyQueue(string map)
        {
            if (_queued || this == null) return;
            Select(map);
            RagoGame.I.Player.Toast("Your party leader started the queue: " + Maps.Name(map));
            StartQueue();
        }

        void OnPartyCancelled()
        {
            if (!_queued || _joining || this == null) return;
            if (_offline != null) StopCoroutine(_offline);
            _queued = false; AnyQueued = false;
            _queueBtn.SetText("QUEUE"); _queueBtn.SetColor(new Color(0.2f, 0.42f, 0.2f));
            HudQueue("");
            RagoGame.I.Player.Toast("Your party leader cancelled the queue");
        }
    }

    // ======================================================================================= TRADERS
    public class TraderDef
    {
        public string name, title;
        public Outfit outfit;
        public List<string> stock = new List<string>();
        public float sellRate = 0.55f;
        public float valuableBonus = 1f;   // extra multiplier when buying valuables / junk
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
            // hand-painted plank sign hanging from the awning on two chains
            var sb = new MB(2);
            sb.Sub(0).Box(new Vector3(0, 2.72f, -0.72f), new Vector3(2.1f, 0.42f, 0.035f), UI.Lin(new Color(0.42f, 0.3f, 0.2f)), 0.01f, 1);
            sb.Sub(0).Box(new Vector3(0, 2.72f, -0.737f), new Vector3(2.0f, 0.004f, 0.002f), UI.Lin(new Color(0.3f, 0.21f, 0.14f)), 0f); // plank seam
            foreach (var x in new[] { -0.85f, 0.85f }) sb.Sub(1).Cyl(new Vector3(x, 2.93f, -0.72f), new Vector3(x, 3.05f, -0.68f), 0.006f, 0.006f, 6, UI.Lin(new Color(0.3f, 0.3f, 0.3f)));
            sb.Build("SignBoard", transform, Mats.Lit("sign_wood", Color.white, 0f, 0.25f, Mats.Wood, Mats.WoodN, 0.5f, 0.3f), Mats.Lit("sign_chain", Color.white, 0.9f, 0.4f, Mats.Metal, null, 0.3f, 0.2f));
            var sign = new GameObject("Sign").transform; sign.SetParent(transform, false); sign.localPosition = new Vector3(0, 2.72f, -0.74f);
            UI.Text(sign, _t.name.ToUpper(), new Vector3(0, 0.06f, 0), 0.17f, Color.Lerp(_t.color, new Color(0.95f, 0.9f, 0.8f), 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(sign, _t.title, new Vector3(0, -0.12f, 0), 0.055f, new Color(0.9f, 0.86f, 0.76f));
            // decorative wares on the back shelf
            var rng = new Rng(seed);
            for (int i = 0; i < Mathf.Min(6, _t.stock.Count); i++)
            {
                var it = ItemFactory.Spawn(rng.Pick(_t.stock), transform.TransformPoint(new Vector3(-1.4f + i * 0.56f, 1.25f + (i % 2) * 0.6f, 1.75f)), transform.rotation * Quaternion.Euler(0, 90, 0));
                if (it != null) { it.transform.SetParent(transform, true); var rb = it.rb; foreach (var c in it.GetComponentsInChildren<Collider>()) Compat.Kill(c); Compat.Kill(it); Compat.Kill(rb); }
            }
            // menu panel standing on the counter, angled towards the customer
            // chalkboard price list propped on the counter
            _panel = UI.Panel(transform, "Menu", new Vector3(0.35f, 1.58f, 0.08f), Quaternion.Euler(-10, 0, 0), new Vector2(1.5f, 0.95f), null, true, Skin.Chalk);
            _money = UI.Text(_panel, "", new Vector3(0.7f, 0.41f, 0), 0.032f, UI.ChalkDim, TextAnchor.MiddleRight);
            UI.Text(_panel, _t.name + "'s", new Vector3(-0.7f, 0.405f, 0), 0.06f, UI.ChalkYellow, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UI.Text(_panel, "~ for sale ~", new Vector3(-0.22f, 0.4f, 0), 0.032f, UI.ChalkDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            UI.Button(_panel, "prev", new Vector3(-0.55f, -0.41f, 0), new Vector2(0.16f, 0.07f), () => { _page = Mathf.Max(0, _page - 1); Refresh(); }, null, BtnStyle.Tag);
            UI.Button(_panel, "next", new Vector3(-0.35f, -0.41f, 0), new Vector2(0.16f, 0.07f), () => { _page++; Refresh(); }, null, BtnStyle.Tag);
            // trays on the counter
            _delivery = ItemContainer.Grid(transform, new Vector3(-1.1f, 1.08f, -0.05f), Quaternion.identity, 2, 2, new Vector2(0.36f, 0.3f), 0.3f);
            TrayVisual(new Vector3(-1.1f, 1.05f, -0.05f), "PICKUP", new Color(0.3f, 0.6f, 1f));
            _sell = ItemContainer.Grid(transform, new Vector3(1.15f, 1.08f, -0.05f), Quaternion.identity, 2, 2, new Vector2(0.36f, 0.3f), 0.3f, it => _t.buys == null || _t.buys(it.def));
            TrayVisual(new Vector3(1.15f, 1.05f, -0.05f), "SELL", new Color(1f, 0.6f, 0.2f));
            _sell.onChanged = Refresh;
            // a receipt pad on a clipboard for selling
            var sp = UI.Panel(transform, "SellPanel", new Vector3(1.15f, 1.32f, 0.34f), Quaternion.Euler(-25, 0, 0), new Vector2(0.66f, 0.24f), null, true, Skin.Paper);
            _sellLabel = UI.Text(sp, "", new Vector3(0, 0.035f, 0), 0.028f, UI.PaperInk);
            UI.Button(sp, "SELL", new Vector3(0, -0.06f, 0), new Vector2(0.3f, 0.08f), Sell, null, BtnStyle.Stamp);
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
            foreach (var r in _rows) Compat.Kill(r);
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
                // chalk lettering: rarity shows as a coloured chalk dot, prices in yellow chalk
                UI.Text(row, "*", new Vector3(-0.71f, 0.014f, 0), 0.05f, Color.Lerp(ItemDef.RarityColor(def.rarity), UI.Chalk, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Text(row, def.name, new Vector3(-0.67f, 0.02f, 0), 0.046f, UI.Chalk, TextAnchor.MiddleLeft, FontStyle.Bold);
                string sub = def is GunDef g ? Db.CalName(g.cal) + ", " + g.magSize + " rd, " + g.action.ToString().ToLower() + (g.optics ? ", rail" : "") : def.rarity.ToString().ToLower();
                UI.Text(row, sub, new Vector3(-0.67f, -0.032f, 0), 0.03f, UI.ChalkDim, TextAnchor.MiddleLeft, FontStyle.Italic);
                int price = Price(def);
                UI.Text(row, "$" + price.ToString("N0"), new Vector3(0.45f, 0, 0), 0.048f, UI.ChalkYellow, TextAnchor.MiddleRight, FontStyle.Bold);
                var d2 = def;
                UI.Button(row, "buy", new Vector3(0.61f, 0, 0), new Vector2(0.17f, 0.085f), () => Buy(d2), null, BtnStyle.Tag);
            }
            _money.text = "your cash  $" + Profile.Current.money.ToString("N0");
            int v = 0, n = 0;
            foreach (var s in _sell.slots) if (s.item != null) { v += SellValue(s.item); n++; }
            _sellLabel.text = n == 0 ? "Put items on the SELL tray" : n + " item(s) for <color=#1d6b2a>$" + v.ToString("N0") + "</color>";
        }

        int Price(ItemDef d) => Mathf.RoundToInt(d.price * (d.rarity >= Rarity.Rare ? 1.6f : 1.25f) / 10f) * 10;
        int SellValue(Item it) => Mathf.Max(1, Mathf.RoundToInt(it.Value * _t.sellRate * (it.def != null && it.def.kind == ItemKind.Valuable ? _t.valuableBonus : 1f)));

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
            var arms = new TraderDef { name = "Mara", title = "Quartermaster - sidearms, SMGs, optics, ammo, bags", outfit = Outfit.Quartermaster, color = new Color(0.35f, 0.55f, 0.3f), sellRate = 0.5f };
            foreach (var g in Db.Guns) if ((!g.TwoHanded || g.cls == GunClass.SMG) && g.rarity <= Rarity.Rare) arms.stock.Add(g.id);
            foreach (var s in Db.Sights) if (s.rarity <= Rarity.Rare) arms.stock.Add(s.id);
            foreach (var m in Db.Misc) if (m.kind == ItemKind.Ammo || m.kind == ItemKind.Throwable || m.kind == ItemKind.Melee) arms.stock.Add(m.id);
            // basic bags only; better bags and all armour have to be found in raids
            arms.stock.Add("pack_day"); arms.stock.Add("pack_assault");
            arms.buys = d => d.kind == ItemKind.Gun || d.kind == ItemKind.Sight || d.kind == ItemKind.Armor || d.kind == ItemKind.Backpack || d.kind == ItemKind.Magazine;
            var medic = new TraderDef { name = "Doc Hale", title = "Medic - meds & stims", outfit = Outfit.Medic, color = new Color(0.8f, 0.2f, 0.2f), sellRate = 0.6f };
            foreach (var m in Db.Misc) if (m.kind == ItemKind.Med) medic.stock.Add(m.id);
            medic.buys = d => d.kind == ItemKind.Med || d.kind == ItemKind.Valuable;
            var fence = new TraderDef { name = "Silas", title = "Fence - buys anything, best price for valuables", outfit = Outfit.Fence, color = new Color(0.8f, 0.65f, 0.25f), sellRate = 0.65f, valuableBonus = 1.3f };
            fence.stock.Add("key_vault"); fence.stock.Add("val_intel"); fence.stock.Add("val_drive");
            fence.buys = d => true;
            return new List<TraderDef> { gunsmith, arms, medic, fence };
        }
    }

    // ======================================================================================= STASH
    public enum StashKind { Weapons, Armor, Gear }

    /// <summary>
    /// One part of your bunker stash. The Armory holds guns, sights and mags; the Armor Room has armour stands
    /// (helmet + vest) and backpack shelves; the Gear wall holds meds, valuables, keys and junk.
    /// Everything left here when you leave is saved. When a section is full the rest goes on the next page.
    /// </summary>
    public class StashSection : MonoBehaviour
    {
        public StashKind kind;
        readonly List<ItemContainer> _groups = new List<ItemContainer>();
        readonly List<ItemData> _overflow = new List<ItemData>();
        TextMesh _info, _ammo;
        static Material Board => Mats.Lit("pegboard", Color.white, 0.1f, 0.3f, Mats.Wood, null, 0.3f, 0.1f);
        static Material Steel => Mats.Lit("shelf", new Color(0.3f, 0.32f, 0.35f), 0.7f, 0.4f, Mats.Metal, null, 0.4f, 0.5f);

        /// <summary>Which section an item belongs in: armour and bags go to the Armor Room, everything else to the Armory.</summary>
        public static StashKind For(ItemData d)
        {
            var def = d != null ? Db.Get(d.id) : null;
            if (def != null && (def.kind == ItemKind.Armor || def.kind == ItemKind.Backpack)) return StashKind.Armor;
            return StashKind.Weapons;
        }

        public static StashSection Create(Transform parent, Vector3 pos, float yaw, StashKind kind)
        {
            var go = new GameObject("Stash_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var s = go.AddComponent<StashSection>();
            s.kind = kind;
            if (kind == StashKind.Weapons) s.BuildWeapons(); else if (kind == StashKind.Armor) s.BuildArmor(); else s.BuildGear();
            return s;
        }

        void Backboard(string title)
        {
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 1.3f, 0.1f), new Vector3(5.2f, 2.6f, 0.08f), new Color(0.35f, 0.32f, 0.26f), 0.01f);
            for (int i = 0; i < 40; i++) for (int j = 0; j < 20; j++) if ((i + j) % 2 == 0) mb.Sub(1).Box(new Vector3(-2.5f + i * 0.128f, 0.1f + j * 0.128f, 0.055f), new Vector3(0.012f, 0.012f, 0.004f), Color.black);
            mb.Build("Pegboard", transform, Board, Mats.Polymer);
            var c = gameObject.AddComponent<BoxCollider>(); c.center = new Vector3(0, 1.3f, 0.1f); c.size = new Vector3(5.2f, 2.6f, 0.1f);
            UI.Text(transform, title, new Vector3(0, 2.75f, 0.05f), 0.16f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void Shelf(MB mb, float x, float y, float w, float depth = 0.5f)
        {
            mb.Sub(0).Box(new Vector3(x, y, -0.15f), new Vector3(w, 0.04f, depth), new Color(0.3f, 0.3f, 0.3f), 0.01f);
            mb.Sub(0).Box(new Vector3(x - w / 2 + 0.03f, y - 0.08f, 0.0f), new Vector3(0.03f, 0.16f, 0.2f), new Color(0.25f, 0.25f, 0.25f), 0.005f);
            mb.Sub(0).Box(new Vector3(x + w / 2 - 0.03f, y - 0.08f, 0.0f), new Vector3(0.03f, 0.16f, 0.2f), new Color(0.25f, 0.25f, 0.25f), 0.005f);
        }

        ItemContainer Group(ItemContainer c) { _groups.Add(c); return c; }

        WorldButton _next;

        /// <summary>Info lettered straight onto the pegboard (no separate panel), plus a NEXT PAGE button that only shows up when there's more.</summary>
        void Panel(Vector3 pos, Quaternion rot, Vector2 size)
        {
            var paint = new Color(0.93f, 0.89f, 0.76f);
            _info = UI.Text(transform, "", new Vector3(-2.48f, 2.53f, 0.05f), 0.05f, paint, TextAnchor.UpperLeft);
            if (kind != StashKind.Armor) _ammo = UI.Text(transform, "", new Vector3(-0.3f, 2.53f, 0.05f), 0.042f, new Color(0.8f, 0.77f, 0.66f), TextAnchor.UpperLeft);
            _next = UI.Button(transform, "NEXT PAGE >", new Vector3(2.2f, 2.45f, 0.062f), new Vector2(0.5f, 0.11f), NextPage);
            _next.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------------- armory
        void BuildWeapons()
        {
            Backboard("ARMORY");
            var mb = new MB(1);
            // rifle rack: rubber-coated pegs top and bottom
            for (int i = 0; i < 7; i++)
            {
                float x = -2.26f + i * 0.42f;
                mb.Box(new Vector3(x, 2.2f, -0.02f), new Vector3(0.12f, 0.03f, 0.18f), new Color(0.12f, 0.12f, 0.12f), 0.008f);
                mb.Box(new Vector3(x, 0.4f, -0.08f), new Vector3(0.16f, 0.06f, 0.3f), new Color(0.12f, 0.12f, 0.12f), 0.01f);
            }
            Shelf(mb, 1.55f, 0.45f, 1.6f);
            mb.Build("Racks", transform, Steel);
            Group(ItemContainer.Grid(transform, new Vector3(-1.0f, 1.35f, -0.05f), Quaternion.Euler(-90, 0, 0), 7, 1, new Vector2(0.42f, 1f), 1.2f, it => it is Gun g && g.g.TwoHanded, Quaternion.Euler(0, 0, 90)));
            Group(ItemContainer.Grid(transform, new Vector3(1.55f, 1.55f, -0.05f), Quaternion.Euler(-90, 0, 0), 4, 3, new Vector2(0.36f, 0.3f), 0.26f, it => !(it is Backpack) && !(it is ArmorItem) && !(it is Gun g && g.g.TwoHanded)));   // pistols, sights, mags, meds, loot
            Group(ItemContainer.Grid(transform, new Vector3(1.55f, 0.5f, -0.15f), Quaternion.identity, 4, 1, new Vector2(0.38f, 0.4f), 0.34f, it => !(it is Backpack) && !(it is ArmorItem)));
            Panel(new Vector3(-1.0f, 0.26f, -0.45f), Quaternion.Euler(-30, 0, 0), new Vector2(1.9f, 0.5f));
        }

        // ------------------------------------------------------------------------------- armour room
        void BuildArmor()
        {
            Backboard("ARMOR");
            var mb = new MB(2);
            Color steel = new Color(0.3f, 0.3f, 0.32f), form = new Color(0.16f, 0.16f, 0.17f);
            for (int i = 0; i < 3; i++)
            {
                float x = -2.0f + i * 1.0f, z = -0.45f;
                // armour stand: weighted base, pole, torso form and head form
                mb.Sub(0).Cyl(new Vector3(x, 0f, z), new Vector3(x, 0.04f, z), 0.22f, 0.2f, 16, steel);
                mb.Sub(0).Cyl(new Vector3(x, 0.04f, z), new Vector3(x, 1.02f, z), 0.022f, 0.022f, 8, steel, false);
                mb.Sub(1).Box(new Vector3(x, 1.25f, z), new Vector3(0.3f, 0.42f, 0.18f), form, 0.06f, 2);
                mb.Sub(1).Cyl(new Vector3(x, 1.46f, z), new Vector3(x, 1.58f, z), 0.04f, 0.04f, 10, form, false);
                mb.Sub(1).Sphere(new Vector3(x, 1.68f, z), new Vector3(0.085f, 0.11f, 0.095f), 14, form);
                var stand = new GameObject("Stand" + i).transform;
                stand.SetParent(transform, false);
                Group(Single(stand, new Vector3(x, 1.27f, z), it => it is ArmorItem a && a.ad.slot == ArmorSlot.Vest, 0.3f));
                Group(Single(stand, new Vector3(x, 1.75f, z), it => it is ArmorItem a && a.ad.slot == ArmorSlot.Helmet, 0.2f));
            }
            Shelf(mb, 1.6f, 0.45f, 1.6f, 0.55f);
            Shelf(mb, 1.6f, 1.25f, 1.6f, 0.55f);
            mb.Build("Stands", transform, Steel, Mats.Rubber);
            // backpacks and spare armour on the shelves
            Group(ItemContainer.Grid(transform, new Vector3(1.6f, 0.52f, -0.15f), Quaternion.identity, 3, 1, new Vector2(0.52f, 0.5f), 0.46f, it => it is Backpack || it is ArmorItem));
            Group(ItemContainer.Grid(transform, new Vector3(1.6f, 1.32f, -0.15f), Quaternion.identity, 3, 1, new Vector2(0.52f, 0.5f), 0.46f, it => it is Backpack || it is ArmorItem));
            Panel(new Vector3(1.6f, 2.05f, 0.04f), Quaternion.identity, new Vector2(1.6f, 0.4f));
        }

        ItemContainer Single(Transform parent, Vector3 pos, System.Func<Item, bool> filter, float radius)
        {
            var c = ItemContainer.Grid(parent, pos, Quaternion.identity, 1, 1, Vector2.one * radius, 0f, filter, Quaternion.Euler(0, 180, 0));
            foreach (var sl in c.slots) sl.radius = radius;
            return c;
        }

        // ------------------------------------------------------------------------------- gear wall
        void BuildGear()
        {
            Backboard("GEAR");
            var mb = new MB(1);
            Shelf(mb, 1.55f, 0.45f, 1.7f); Shelf(mb, 1.55f, 1.05f, 1.7f); Shelf(mb, 1.55f, 1.65f, 1.7f);
            mb.Build("Shelves", transform, Steel);
            System.Func<Item, bool> small = it => !(it is Backpack) && !(it is ArmorItem) && !(it is Gun g && g.g.TwoHanded);
            Group(ItemContainer.Grid(transform, new Vector3(-1.2f, 1.55f, -0.05f), Quaternion.Euler(-90, 0, 0), 6, 3, new Vector2(0.3f, 0.3f), 0.24f, small));
            for (int r = 0; r < 3; r++) Group(ItemContainer.Grid(transform, new Vector3(1.55f, 0.5f + r * 0.6f, -0.15f), Quaternion.identity, 4, 1, new Vector2(0.42f, 0.4f), 0.32f, small));
            Panel(new Vector3(-1.2f, 0.35f, -0.2f), Quaternion.Euler(-30, 0, 0), new Vector2(1.7f, 0.5f));
        }

        // ------------------------------------------------------------------------------- contents
        IEnumerable<Slot> AllSlots() { foreach (var g in _groups) foreach (var s in g.slots) yield return s; }

        public void Place(ItemData d) => Place(d, true);

        /// <summary>Puts an item on this section. If it's full: hidden on the next page (allowOverflow) or false.</summary>
        public bool Place(ItemData d, bool allowOverflow)
        {
            var it = ItemFactory.Spawn(d, transform.position + Vector3.up, transform.rotation);
            if (it == null) return true;
            it.transform.SetParent(null, true);
            Slot target = null;
            foreach (var g in _groups) { target = g.FirstFree(it); if (target != null) break; }
            if (target == null)
            {
                Compat.Kill(it.gameObject);
                if (!allowOverflow) return false;
                _overflow.Add(d);
                return true;
            }
            target.Store(it, false);
            return true;
        }

        void NextPage()
        {
            if (_overflow.Count == 0) { RagoGame.I.Player.Toast("Nothing on the next page"); return; }
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

        public void Refresh()
        {
            var prof = Profile.Current;
            if (prof == null || _info == null) return;
            if (_next != null && _next.gameObject.activeSelf != (_overflow.Count > 0)) _next.gameObject.SetActive(_overflow.Count > 0);
            int count = 0, value = 0;
            foreach (var s in AllSlots()) if (s.item != null) { count++; value += s.item.Value; }
            foreach (var d in _overflow) { var def = Db.Get(d.id); if (def != null) value += def.price; }
            string items = "Items  " + (count + _overflow.Count) + (_overflow.Count > 0 ? "  (+" + _overflow.Count + " on next page)" : "");
            if (kind != StashKind.Armor)
            {
                _info.text = "<b>" + prof.DisplayName + "</b>\nCash  <color=#b8f0a0>$" + prof.money.ToString("N0") + "</color>\n" + items +
                    "\nRaids " + prof.raids + "  Extracts " + prof.extracts + "  Kills " + prof.kills;
                var sb = new System.Text.StringBuilder("AMMO RESERVE\n");
                int col = 0;
                foreach (Caliber cc in System.Enum.GetValues(typeof(Caliber))) { if (prof.stashAmmo[(int)cc] <= 0) continue; sb.Append(Db.CalName(cc)).Append(": ").Append(prof.stashAmmo[(int)cc]).Append(++col % 2 == 0 ? "\n" : "    "); }
                _ammo.text = sb.ToString();
            }
            else _info.text = items + "\nWorth  <color=#b8f0a0>$" + value.ToString("N0") + "</color>" + (kind == StashKind.Armor ? "\nWear armour: hold it to your head / chest and let go." : "");
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
            s.Apply();
            return s;
        }

        void Apply()
        {
            var p = Profile.Current;
            p.gripPitch = Hand.GripPitch; p.smoothTurn = PlayerRig.SmoothTurn; p.snapAngle = PlayerRig.SnapAngle; p.volume = Sfx.MasterVolume;
            p.Save();
            _txt.text = "Name:  " + p.DisplayName + "\nGrip angle:  " + Hand.GripPitch + " deg\nTurning:  " + (PlayerRig.SmoothTurn ? "Smooth" : "Snap " + PlayerRig.SnapAngle) +
                "\nVolume:  " + Mathf.RoundToInt(Sfx.MasterVolume * 100) + "%\nVoice:  " + (NetHub.Voice == null ? "not installed" : NetHub.Voice.Status + (NetHub.Voice.Muted ? " (muted)" : "")) +
                "\nNetwork:  " + (NetHub.Backend == null ? "offline (Rago Extract > Multiplayer)" : NetHub.Backend.Status);
        }
    }

    public static class HubProps
    {
        /// <summary>Hanging steel plate; yaw turns its face (default faces along z) towards the shooter.</summary>
        public static void SteelTarget(Transform parent, Vector3 pos, float size, float yaw = 0f)
        {
            var anchor = new GameObject("TargetAnchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = pos;
            var arb = anchor.AddComponent<Rigidbody>(); arb.isKinematic = true;
            var plate = new MB().Box(Vector3.zero, new Vector3(size, size * 1.3f, 0.02f), new Color(0.85f, 0.85f, 0.82f), 0.004f).Build("Plate", parent, Mats.Lit("steel_target", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.3f, 0.6f));
            plate.transform.position = pos + Vector3.down * size * 0.9f;
            plate.transform.rotation = Quaternion.Euler(0, yaw, 0);   // the hinge axis turns with it, so it swings back away from the shooter
            var bc = plate.AddComponent<BoxCollider>(); bc.size = new Vector3(size, size * 1.3f, 0.02f);
            plate.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            var rb = plate.AddComponent<Rigidbody>(); rb.mass = 6f; rb.SetDamping(0.3f, 1.5f);
            var hj = plate.AddComponent<HingeJoint>(); hj.connectedBody = arb; hj.anchor = new Vector3(0, size * 0.9f, 0); hj.axis = Vector3.right;
            var chain = new MB().Cyl(Vector3.zero, Vector3.down * size * 0.25f, 0.006f, 0.006f, 6, Color.gray).Build("Chain", anchor.transform, Mats.GunMetal);
        }
    }
}
