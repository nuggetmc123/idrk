using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The menu on the LEFT MENU button (the three lines; N on desktop). Works in the bunker and in raids.
    ///  - PARTY: who's in your party, invites you've received (ACCEPT / DECLINE), LEAVE PARTY.
    ///  - FRIENDS: add a friend by their callsign (NAME#1234), invite them, remove them.
    ///  - RECENT: everyone in your current raid, then people you've raided with before: invite them or add them.
    ///  - SETTINGS: grip angle, turning, volume, microphone.
    /// It opens in front of you and stays where it is; walk away (or press the button again) to close it.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static GameMenu I;
        enum Tab { Party, Friends, Recent, Settings, Tester, Notes, Mod, Dlc }
        Tab _tab = Tab.Party;
        Transform _panel, _content;
        TextMesh _header;
        readonly List<WorldButton> _tabBtns = new List<WorldButton>();
        bool _open, _typing, _dirty = true;
        string _typed = "";
        int _page;
        float _refresh;
        static bool _hooked;

        public static void Init()
        {
            if (I != null) return;
            var go = new GameObject("GameMenu");
            DontDestroyOnLoad(go);
            I = go.AddComponent<GameMenu>();
            I.Build();
            I.Show(false);
        }

        public static void Close() { if (I != null) I.Show(false); SpawnBrowser.Close(); }

        /// <summary>Back from the spawn board: the menu again, where it was.</summary>
        public static void Reopen() { if (I == null) return; I._dirty = true; I.Show(true); }

        void Build()
        {
            _panel = UI.Panel(transform, "Menu", Vector3.zero, Quaternion.identity, new Vector2(1.12f, 0.9f), null, true, Skin.Steel);
            _header = UI.Text(_panel, "", new Vector3(-0.52f, 0.39f, 0), 0.034f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Button(_panel, "X", new Vector3(0.5f, 0.39f, 0), new Vector2(0.07f, 0.065f), () => Show(false), new Color(0.5f, 0.15f, 0.1f), BtnStyle.Key);
            string[] names = { "PARTY", "FRIENDS", "RECENT", "SETTINGS", "TESTER", "NOTES", "MOD", "DLC" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = (Tab)i;
                _tabBtns.Add(UI.Button(_panel, names[i], new Vector3(-0.48f + (i == 6 ? 7 : i == 7 ? 6 : i) * 0.137f, 0.3f, 0), new Vector2(0.13f, 0.075f), () => { _tab = t; _typing = false; _page = 0; _dlcSel = null; _modTools = false; _dirty = true; Sfx.UI(Synth.Beep(), 0.2f); }));
            }
            _content = new GameObject("Content").transform;
            _content.SetParent(_panel, false);
            Dress(_panel, 1.12f, 0.9f);
        }

        Renderer _blink;
        /// <summary>The menu's own look - a field terminal: a dark header band with an amber edge and the RAGO // EXTRACT
        /// stamp, amber corner brackets, a divider under the tabs, side rails with bolts, and a hazard-striped footer with a
        /// status line and a blinking light. Only round the edges: nothing covers the buttons or the text.</summary>
        void Dress(Transform p, float w, float h)
        {
            var amber = new Color(1f, 0.78f, 0.16f);
            var deco = new MB(4);
            float hx = w / 2, hy = h / 2, z = -0.0012f;
            // header band behind the title row, amber strip down its left end
            deco.Sub(0).Box(new Vector3(0, hy - 0.05f, -0.0006f), new Vector3(w - 0.02f, 0.085f, 0.001f), new Color(0.05f, 0.055f, 0.06f), 0f);
            deco.Sub(1).Box(new Vector3(-hx + 0.016f, hy - 0.05f, z), new Vector3(0.008f, 0.085f, 0.001f), amber, 0f);
            // divider under the tabs
            deco.Sub(1).Box(new Vector3(0, 0.255f, z), new Vector3(w - 0.06f, 0.0025f, 0.001f), amber, 0f);
            // corner brackets, just outside the panel
            foreach (float sx in new[] { -1f, 1f }) foreach (float sy in new[] { -1f, 1f })
            {
                deco.Sub(1).Box(new Vector3(sx * (hx + 0.006f - 0.035f), sy * (hy + 0.006f), z), new Vector3(0.07f, 0.008f, 0.004f), amber, 0f);
                deco.Sub(1).Box(new Vector3(sx * (hx + 0.006f), sy * (hy + 0.006f - 0.035f), z), new Vector3(0.008f, 0.07f, 0.004f), amber, 0f);
                deco.Sub(2).Cyl(new Vector3(sx * (hx - 0.025f), sy * (hy - 0.025f) + (sy > 0 ? -0.075f : 0f), z), new Vector3(sx * (hx - 0.025f), sy * (hy - 0.025f) + (sy > 0 ? -0.075f : 0f), z - 0.003f), 0.007f, 0.006f, 8, new Color(0.55f, 0.56f, 0.58f));   // bolts
            }
            // side rails
            foreach (float sx in new[] { -1f, 1f }) deco.Sub(2).Box(new Vector3(sx * (hx - 0.004f), -0.05f, z), new Vector3(0.006f, h * 0.62f, 0.002f), new Color(0.3f, 0.31f, 0.33f), 0f);
            // hazard-striped footer
            float fy = -hy + 0.018f;
            deco.Sub(0).Box(new Vector3(0, fy, -0.0006f), new Vector3(w - 0.02f, 0.026f, 0.001f), new Color(0.05f, 0.05f, 0.05f), 0f);
            for (float x = -hx + 0.03f; x < -hx + 0.32f; x += 0.03f)
                deco.Sub(1).Box(new Vector3(x, fy, z), new Vector3(0.012f, 0.03f, 0.001f), amber, 0f, 0, Quaternion.Euler(0, 0, 35f));
            deco.Build("MenuDress", p, Mats.Unlit("menu_dark", new Color(0.05f, 0.055f, 0.06f, 1f)), Mats.Unlit("menu_amber", new Color(amber.r, amber.g, amber.b, 1f)), Mats.Lit("menu_bolt", Color.white, 0.8f, 0.6f), Mats.Unlit("menu_unused", Color.white));
            UI.Text(p, "RAGO // EXTRACT", new Vector3(0.4f, hy - 0.05f, -0.002f), 0.026f, amber, TextAnchor.MiddleRight, FontStyle.Bold);
            UI.Text(p, "FIELD TERMINAL  //  " + PatchNotes.All[0].version + "  //  SECURE LINK", new Vector3(hx - 0.06f, fy, -0.002f), 0.014f, new Color(0.6f, 0.62f, 0.6f), TextAnchor.MiddleRight);
            var led = new MB().Sphere(new Vector3(hx - 0.035f, fy, -0.002f), Vector3.one * 0.006f, 8, Color.white).Build("Led", p, Mats.Unlit("menu_led", new Color(0.3f, 1f, 0.4f, 1f)));
            _blink = led.GetComponent<Renderer>();
        }

        void Show(bool on)
        {
            _open = on;
            if (_panel != null) _panel.gameObject.SetActive(on);
            if (!on) { _typing = false; Desktop.Typing = false; }
        }

        void OpenInFront()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            Vector3 fwd = p.Head.forward; fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = p.transform.forward; fwd.Normalize();
            Vector3 pos = p.Head.position + fwd * 0.62f + Vector3.down * 0.18f;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - p.Head.position, Vector3.up));
            _dirty = true;
            Show(true);
            Sfx.UI(Synth.Beep(), 0.3f);
        }

        void Update()
        {
            if (_blink != null && _open) _blink.enabled = Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f;
            HookNet();
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            bool toggle = XRInputs.VR ? XRInputs.Left.MenuDown : Desktop.MenuKey;
            if (toggle && SpawnBrowser.IsOpen) { SpawnBrowser.Close(); toggle = false; }   // the menu button closes the spawn board
            if (toggle) { if (_open) Show(false); else OpenInFront(); }
            if (!_open) return;
            if (p.Dead || Vector3.Distance(p.Head.position, transform.position) > 2.5f) { Show(false); return; }
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f) { _refresh = 1.5f; if (!_typing && _tab != Tab.Dlc) _dirty = true; }   // (nothing on the DLC tab changes by itself: no redraw - it was a stutter every 1.5 s)
            if (Time.unscaledTime > _pendT) { _pendT = Time.unscaledTime + 3f; CheckPending(); }
            SearchTick();
            CalTick();
            if (_dirty) { _dirty = false; Rebuild(); }
        }

        void HookNet()
        {
            if (_hooked || NetHub.Backend == null) return;
            _hooked = true;
            NetHub.Backend.OnPartyChanged += () => { _dirty = true; };
            NetHub.Backend.OnMetPlayer += AddRecent;
        }

        // ------------------------------------------------------------------ lists kept in your profile
        static string Key(string s) => (s ?? "").Trim().ToLowerInvariant();

        static void AddRecent(string callsign)
        {
            var prof = Profile.Current;
            if (prof == null || string.IsNullOrEmpty(callsign) || Key(callsign) == Key(prof.DisplayName)) return;
            prof.recent.RemoveAll(x => Key(x) == Key(callsign));
            prof.recent.Insert(0, callsign);
            while (prof.recent.Count > 24) prof.recent.RemoveAt(prof.recent.Count - 1);
            prof.Save();
        }

        static bool IsFriend(string callsign) { var prof = Profile.Current; return prof != null && prof.friends.Exists(x => Key(x) == Key(callsign)); }

        static void AddFriend(string callsign)
        {
            var prof = Profile.Current;
            var pl = RagoGame.I.Player;
            callsign = (callsign ?? "").Trim().ToUpperInvariant();
            int hash = callsign.IndexOf('#');
            if (hash <= 0 || hash != callsign.Length - 5 || !int.TryParse(callsign.Substring(hash + 1), out _))
            { pl.Toast("Type their full callsign with the tag, like GHOST#0193"); Sfx.UI(Synth.UiError(), 0.3f); return; }
            if (Key(callsign) == Key(prof.DisplayName)) { pl.Toast("That's you"); return; }
            if (IsFriend(callsign)) { pl.Toast(callsign + " is already a friend"); return; }
            // only real players: someone you've raided with, or who's in this session (Market / party / raid) now
            string real = KnownPlayer(callsign);
            if (real == null)
            {
                // not seen online yet (they may be sitting in their bunker): a request that confirms itself the moment the
                // game sees them in a party, raid or the Market - a made-up name never confirms and expires
                if (prof.pendingFriends.Exists(x => Key(x.Split('|')[0]) == Key(callsign))) { pl.Toast(callsign + " is already waiting to be found"); return; }
                prof.pendingFriends.Add(callsign + "|" + System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
                prof.Save();
                pl.Toast("Friend request for " + callsign + ": added as soon as they're online in a party, raid or the Market");
                Sfx.UI(Synth.Beep(), 0.3f);
                return;
            }
            callsign = real;
            prof.friends.Add(callsign);
            prof.Save();
            pl.Toast("Added " + callsign + " as a friend");
            Sfx.UI(Synth.QueueFound(), 0.35f);
        }

        /// <summary>The real callsign of a player the game has actually met (RECENT, or with you now), or null.</summary>
        float _pendT;
        /// <summary>Confirm pending friend requests whose player has been seen; drop ones never seen in 14 days.</summary>
        static void CheckPending()
        {
            var prof = Profile.Current;
            if (prof == null || prof.pendingFriends.Count == 0) return;
            bool changed = false;
            for (int i = prof.pendingFriends.Count - 1; i >= 0; i--)
            {
                var parts = prof.pendingFriends[i].Split('|');
                string real = KnownPlayer(parts[0]);
                if (real != null)
                {
                    prof.pendingFriends.RemoveAt(i); changed = true;
                    if (!IsFriend(real)) { prof.friends.Add(real); RagoGame.I?.Player?.Toast(real + " found - added as a friend"); }
                    continue;
                }
                if (parts.Length > 1 && System.DateTime.TryParse(parts[1], out var when) && (System.DateTime.UtcNow - when).TotalDays > 14) { prof.pendingFriends.RemoveAt(i); changed = true; }
            }
            if (changed) prof.Save();
        }

        static string KnownPlayer(string callsign)
        {
            var here = NetHub.Online ? NetHub.Backend.SessionPlayers : null;
            if (here != null) foreach (var n in here) if (Key(n) == Key(callsign)) return n;
            var prof = Profile.Current;
            if (prof != null) foreach (var n in prof.recent) if (Key(n) == Key(callsign)) return n;
            return KnownPlayers.Find(callsign);   // seen online this session (e.g. leading a party)
        }

        static void Invite(string callsign)
        {
            var pl = RagoGame.I.Player;
            if (!NetHub.Online) { pl.Toast("Multiplayer isn't set up (Rago Extract > Multiplayer in Unity)"); return; }
            if (Kiosk.AnyQueued) { pl.Toast("You're in the raid queue - invite them after"); return; }
            if (NetHub.Backend.PartyCode != null && NetHub.Backend.PartyMembers.Count >= QueueRules.PartyMax) { pl.Toast("Your party is full (" + QueueRules.PartyMax + " players)"); return; }
            NetHub.Backend.InviteToParty(callsign);
            pl.Toast("Invited " + callsign + (RagoGame.I.Area == Area.Raid ? " - you'll team up back in your bunkers" : ""));
            Sfx.UI(Synth.Beep(), 0.3f);
        }

        // ------------------------------------------------------------------ drawing
        void Rebuild()
        {
            if (_content != null) { SpawnBrowser.ReturnPreviews(_content); Destroy(_content.gameObject); }
            _content = new GameObject("Content").transform;
            _content.SetParent(_panel, false);
            var prof = Profile.Current;
            _header.text = "MENU   " + (prof != null ? prof.DisplayName : "");
            for (int i = 0; i < _tabBtns.Count; i++) _tabBtns[i].SetColor(i == (int)_tab ? new Color(0.2f, 0.42f, 0.2f) : new Color(0.28f, 0.3f, 0.32f));
            // the MOD tab: only for moderators (the server says who they are)
            _tabBtns[(int)Tab.Mod].gameObject.SetActive(RelayBackend.IsMod);
            // the TESTER tab (god mode, spawn items...) is the developer's test account only
            _tabBtns[(int)Tab.Tester].gameObject.SetActive(Spectator.IsTestAccount);
            if (_tab == Tab.Tester && !Spectator.IsTestAccount) _tab = Tab.Party;
            if (_tab == Tab.Mod && !RelayBackend.IsMod) _tab = Tab.Party;
            if (_typing) { DrawKeyboard(); return; }
            switch (_tab)
            {
                case Tab.Party: DrawParty(); break;
                case Tab.Friends: DrawFriends(); break;
                case Tab.Recent: DrawRecent(); break;
                case Tab.Tester: DrawTester(); break;
                case Tab.Notes: DrawNotes(); break;
                case Tab.Mod: DrawMod(); break;
                case Tab.Dlc: DrawDlc(); break;
                default: DrawSettings(); break;
            }
        }

        TextMesh Line(string text, float y, float size = 0.03f, Color? col = null, float x = -0.52f) =>
            UI.Text(_content, text, new Vector3(x, y, 0), size, col ?? Color.white, TextAnchor.MiddleLeft);

        void Btn(string label, float x, float y, float w, System.Action click, Color? col = null) =>
            UI.Button(_content, label, new Vector3(x, y, 0), new Vector2(w, 0.065f), () => { click(); _dirty = true; }, col);

        static readonly Color Go = new Color(0.2f, 0.42f, 0.2f), Stop = new Color(0.5f, 0.15f, 0.1f);

        // ------------------------------------------------------------------------------- tester tools
        static int _npcKind, _ammoKind;
        static readonly string[] AmmoKinds = { "STANDARD", "AP", "INCENDIARY" };
        static readonly string[] NpcKinds = { "Raider", "Heavy raider", "Doctor Kessler", "The Harbormaster", "The Courier" };


        void DrawTester()
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            float y = 0.2f;
            Line("God mode (no damage):", y);
            Btn(pl.Vitals.God ? "ON" : "OFF", 0.28f, y, 0.16f, () => { pl.Vitals.God = !pl.Vitals.God; if (pl.Vitals.God) pl.Vitals.ResetFull(); }, pl.Vitals.God ? Go : (Color?)null);
            Btn("HEAL", 0.45f, y, 0.14f, () => pl.Vitals.ResetFull()); y -= 0.08f;
            Line("Invisible to AI (not players):", y);
            Btn(Tester.Invisible ? "ON" : "OFF", 0.28f, y, 0.16f, () => Tester.Invisible = !Tester.Invisible, Tester.Invisible ? Go : (Color?)null);
            y -= 0.08f;
            // ammo: pick standard / AP / incendiary, then add 300 of it for the gun in your hand
            Line("Ammo type:", y);
            Btn("<", -0.12f, y, 0.08f, () => _ammoKind = (_ammoKind + 2) % 3);
            Line(AmmoKinds[_ammoKind], y, 0.026f, _ammoKind == 1 ? new Color(0.5f, 0.8f, 1f) : _ammoKind == 2 ? new Color(1f, 0.55f, 0.2f) : Color.white, -0.03f);
            Btn(">", 0.24f, y, 0.08f, () => _ammoKind = (_ammoKind + 1) % 3);
            Btn("+AMMO", 0.45f, y, 0.14f, () =>
            {
                var g = (pl.HandR.Held as Gun) ?? (pl.HandL.Held as Gun);
                if (g == null) { pl.Toast("Hold a gun to get ammo for it"); return; }
                var t = _ammoKind == 1 ? AmmoType.AP : _ammoKind == 2 ? AmmoType.Incendiary : AmmoType.Standard;
                pl.Gear.AddAmmo(g.g.cal, 300, t);
            }); y -= 0.1f;
            // spawn any item: a board of little models of everything, pick one and point where it goes
            Btn("SPAWN ITEM...", -0.25f, y, 0.42f, () => { Show(false); SpawnBrowser.Open(transform); }, Go); y -= 0.1f;
            // spawn raiders / bosses
            Btn("<", -0.47f, y, 0.08f, () => _npcKind = (_npcKind + NpcKinds.Length - 1) % NpcKinds.Length);
            Line(NpcKinds[_npcKind], y, 0.026f, Color.white, -0.41f);
            Btn(">", 0.1f, y, 0.08f, () => _npcKind = (_npcKind + 1) % NpcKinds.Length);
            Btn("SPAWN NPC", 0.36f, y, 0.2f, () => { Show(false); Tester.BeginPlace(null, _npcKind); }, Stop); y -= 0.08f;
            Line("NPCs only spawn in a raid (in multiplayer, everyone sees them).", y, 0.02f, UI.Dim);
        }

        void DrawParty()
        {
            var be = NetHub.Backend;
            if (!NetHub.Online) { Line("Multiplayer isn't set up, so parties are off.", 0.2f); Line("In Unity: Rago Extract > Multiplayer.", 0.13f, 0.026f, UI.Dim); return; }
            float y = 0.2f;
            if (be.PartyCode == null)
            {
                Line("You're not in a party.", y); y -= 0.065f;
                Line("Invite people from FRIENDS or RECENT - the first invite starts one.", y, 0.024f, UI.Dim); y -= 0.08f;
            }
            else
            {
                var m = be.PartyMembers;
                Line("PARTY  (" + m.Count + ")" + (be.PartyLeader ? "  - you lead" : ""), y, 0.032f, UI.Accent); y -= 0.06f;
                for (int i = 0; i < m.Count && i < 5; i++) { Line("  " + m[i], y, 0.028f); y -= 0.05f; }
                Btn("LEAVE PARTY", 0.36f, 0.2f, 0.3f, () => { be.LeaveParty(); RagoGame.I.Player.Toast("Left the party"); }, Stop);
                y -= 0.02f;
            }
            var inv = be.Invites;
            if (inv.Count > 0)
            {
                Line("INVITES", y, 0.03f, UI.Accent); y -= 0.065f;
                for (int i = 0; i < inv.Count && i < 3; i++)
                {
                    var iv = inv[i];
                    Line(iv.from + " invited you", y, 0.027f);
                    Btn("ACCEPT", 0.2f, y, 0.2f, () => { be.AcceptInvite(iv); RagoGame.I.Player.Toast("Joining " + iv.from + "'s party"); }, Go);
                    Btn("DECLINE", 0.42f, y, 0.2f, () => be.DeclineInvite(iv), Stop);
                    y -= 0.08f;
                }
            }
            Line("The leader presses QUEUE on the raid terminal: the whole party goes in together.", -0.39f, 0.021f, UI.Dim);
        }

        void DrawFriends()
        {
            var prof = Profile.Current;
            Btn("+ ADD FRIEND", -0.36f, 0.2f, 0.32f, () => { _kbMod = false; _typing = true; _typed = ""; _suggest.Clear(); _searched = ""; Desktop.Typing = true; }, Go);
            var here = NetHub.Online ? NetHub.Backend.SessionPlayers : null;
            var pending = prof.pendingFriends.ConvertAll(x => x.Split('|')[0]);
            if (prof.friends.Count == 0 && pending.Count == 0) { Line("No friends yet. Press + ADD FRIEND and type their callsign (like GHOST#0193),", 0.08f, 0.026f); Line("or add people you've played with from RECENT.", 0.03f, 0.026f); return; }
            var all = new List<string>(prof.friends); all.AddRange(pending);
            DrawList(all, 0.1f, name =>
            {
                if (pending.Exists(x => Key(x) == Key(name)) && !IsFriend(name)) return (name, "request: waiting to see them online");
                bool inSession = here != null && FindIn(here, name);
                return (name, inSession ? "here with you" : "");
            }, (name, y) =>
            {
                bool wait = !IsFriend(name);
                if (!wait) Btn("INVITE", 0.2f, y, 0.2f, () => Invite(name), Go);
                Btn("REMOVE", 0.42f, y, 0.2f, () => { prof.friends.RemoveAll(x => Key(x) == Key(name)); prof.pendingFriends.RemoveAll(x => Key(x.Split('|')[0]) == Key(name)); prof.Save(); }, Stop);
            });
        }

        /// <summary>Patch notes: one update per page, newest first.</summary>
        void DrawNotes()
        {
            var all = PatchNotes.All;
            int pages = all.Length;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            var (ver, title, lines) = all[_page];
            Line("UPDATE " + ver + "  -  " + title.ToUpper() + (_page == 0 ? "   (latest update)" : ""), 0.22f, 0.03f, UI.Accent);
            float y = 0.15f;
            foreach (var l in lines)
            {
                string wrapped = UI.Wrap("- " + l, 62);
                int rows = wrapped.Split('\n').Length;
                var tm = UI.Text(_content, wrapped, new Vector3(-0.52f, y, 0), 0.021f, Color.white, TextAnchor.UpperLeft);
                y -= rows * 0.032f + 0.018f;
            }
            if (pages > 1)
            {
                Btn("< NEWER", -0.3f, -0.39f, 0.18f, () => _page = Mathf.Max(0, _page - 1));
                Line((_page + 1) + "/" + pages, -0.39f, 0.024f, UI.Dim, -0.035f);
                Btn("OLDER >", 0.12f, -0.39f, 0.18f, () => _page = Mathf.Min(pages - 1, _page + 1));
            }
        }

        void DrawRecent()
        {
            var prof = Profile.Current;
            var list = new List<string>();
            var here = NetHub.Online ? NetHub.Backend.SessionPlayers : null;
            if (here != null) foreach (var n in here) list.Add(n);
            foreach (var n in prof.recent) if (!FindIn(list, n)) list.Add(n);
            // what this machine can see of the others - screenshot this when multiplayer misbehaves
            if (NetHub.Online)
            {
                string info = Perf.Report + NetHub.Backend.DebugInfo + (NetGame.Live ? NetGame.I.DebugInfo() : "");
                var np = UI.Panel(_content, "NetInfo", new Vector3(0.88f, 0, 0), Quaternion.identity, new Vector2(0.6f, 0.9f), null, true, Skin.Steel);
                UI.Text(np, info, new Vector3(-0.28f, 0.42f, -0.002f), 0.016f, new Color(0.6f, 0.85f, 1f), TextAnchor.UpperLeft);
            }
            if (list.Count == 0) { Line("Nobody yet: people you raid, party or meet in the Market with show up here.", 0.2f, 0.028f); return; }
            DrawList(list, 0.2f, name => (name, here != null && FindIn(here, name) ? "in this raid" : ""), (name, y) =>
            {
                Btn("INVITE", 0.2f, y, 0.2f, () => Invite(name), Go);
                if (!IsFriend(name)) Btn("+ FRIEND", 0.42f, y, 0.2f, () => AddFriend(name));
                else Line("friend", y, 0.022f, UI.Dim, 0.36f);
            });
        }

        static bool FindIn(IReadOnlyList<string> l, string name) { foreach (var x in l) if (Key(x) == Key(name)) return true; return false; }

        void DrawList(IReadOnlyList<string> items, float top, System.Func<string, (string, string)> label, System.Action<string, float> buttons)
        {
            const int Per = 6;
            int pages = Mathf.Max(1, (items.Count + Per - 1) / Per);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            float y = top;
            for (int i = _page * Per; i < items.Count && i < (_page + 1) * Per; i++)
            {
                var (name, note) = label(items[i]);
                Line(name, y + (note.Length > 0 ? 0.012f : 0), 0.028f);
                if (note.Length > 0) Line(note, y - 0.022f, 0.02f, new Color(0.5f, 1f, 0.55f));
                buttons(items[i], y);
                y -= 0.085f;
            }
            if (pages > 1)
            {
                Btn("<", -0.1f, -0.39f, 0.08f, () => _page = Mathf.Max(0, _page - 1));
                Line((_page + 1) + "/" + pages, -0.39f, 0.024f, UI.Dim, -0.035f);
                Btn(">", 0.06f, -0.39f, 0.08f, () => _page = Mathf.Min(pages - 1, _page + 1));
            }
        }

        // ---- callsign search: as you type, the server suggests matching players (online first): "nug" -> NUGGIE#1252
        bool _kbMod; string _searched = ""; float _searchT;
        readonly List<string> _suggest = new List<string>();

        void SearchTick()
        {
            if (!_typing || _typed == _searched || RelayBackend.Instance == null) return;
            _searchT -= Time.unscaledDeltaTime;
            if (_searchT > 0f) return;
            _searchT = 0.3f;
            _searched = _typed;
            if (_typed.Trim().Length < 2) { _suggest.Clear(); _dirty = true; return; }
            RelayBackend.Instance.Search(_typed, names => { _suggest.Clear(); _suggest.AddRange(names); _dirty = true; });
        }

        void DrawKeyboard()
        {
            Line(_kbMod ? "MOD - find a player (type part of their callsign):" : "ADD FRIEND - callsign of someone you've played with:", 0.2f, 0.028f, UI.Accent);
            Line((_typed.Length > 0 ? _typed : "") + "_", 0.155f, 0.04f);
            // suggestions: tap one to fill it in
            for (int k = 0; k < _suggest.Count && k < 4; k++)
            {
                string nm = _suggest[k];
                Btn(nm.Length > 18 ? nm.Substring(0, 17) + "." : nm, -0.39f + k * 0.26f, 0.095f, 0.25f, () => { _typed = nm; _searched = nm; _suggest.Clear(); }, new Color(0.22f, 0.32f, 0.45f));
            }
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL_", "ZXCVBNM#" };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                {
                    char ch = rows[r][i];
                    UI.Button(_content, ch.ToString(), new Vector3(-0.4725f + i * 0.105f, 0.03f - r * 0.085f, 0), new Vector2(0.095f, 0.075f), () => { if (_typed.Length < 22) _typed += ch; _dirty = true; }, null, BtnStyle.Key);
                }
            Btn("DEL", 0.37f, 0.03f - 3 * 0.085f, 0.2f, () => { if (_typed.Length > 0) _typed = _typed.Substring(0, _typed.Length - 1); });
            Btn("CANCEL", -0.3f, -0.39f, 0.3f, () => { _typing = false; Desktop.Typing = false; }, Stop);
            if (_kbMod) Btn("PICK", 0.3f, -0.39f, 0.3f, () => { _modTarget = _typed.Trim(); _typing = false; Desktop.Typing = false; }, Go);
            else Btn("ADD", 0.3f, -0.39f, 0.3f, () => { AddFriend(_typed); if (IsFriend(_typed)) { _typing = false; Desktop.Typing = false; } }, Go);
        }

        // ---- MOD tab: pick a player, then ask for a ban / unban / wipe / Market kick. The owner approves it on the web panel.
        string _modTarget = "";
        static int _evKind = 1, _evLen = 1;
        int _modReason, _modLen = 1;
        static readonly string[] ModReasons = { "Cheating", "Exploiting a bug", "Harassment", "Hate speech", "Spamming", "Griefing", "Other" };
        static readonly string[] ModLens = { "1 DAY", "7 DAYS", "30 DAYS", "PERMANENT" };
        static readonly int[] ModHours = { 24, 168, 720, 0 };

        bool _modTools;

        void DrawMod()
        {
            // page 2: the spawning tools (items, raiders and bosses, ammo, god mode) - the same as the developer's TESTER tab
            Btn(_modTools ? "< MOD TOOLS" : "SPAWN TOOLS >", 0.4f, -0.39f, 0.26f, () => { _modTools = !_modTools; _typing = false; });
            if (_modTools) { DrawTester(); return; }
            float y = 0.2f;
            if (!string.IsNullOrEmpty(RelayBackend.LastModReply)) Line(UI.Wrap(RelayBackend.LastModReply, 70), y, 0.022f, new Color(0.6f, 1f, 0.65f));
            else Line("MODERATOR TOOLS - bans, wipes and kicks go to the owner for approval first.", y, 0.024f, UI.Dim);
            y -= 0.07f;
            Line("Player:", y);
            Line(string.IsNullOrEmpty(_modTarget) ? "(none picked)" : _modTarget, y, 0.032f, string.IsNullOrEmpty(_modTarget) ? UI.Dim : UI.Accent, -0.3f);
            Btn("FIND PLAYER", 0.36f, y, 0.26f, () => { _kbMod = true; _typing = true; _typed = ""; _suggest.Clear(); _searched = ""; Desktop.Typing = true; }, Go); y -= 0.08f;
            Line("Reason:", y);
            Btn("<", -0.22f, y, 0.07f, () => _modReason = (_modReason + ModReasons.Length - 1) % ModReasons.Length);
            Line(ModReasons[_modReason], y, 0.028f, Color.white, -0.17f);
            Btn(">", 0.2f, y, 0.07f, () => _modReason = (_modReason + 1) % ModReasons.Length); y -= 0.075f;
            Line("Ban length:", y);
            Btn("<", -0.22f, y, 0.07f, () => _modLen = (_modLen + ModLens.Length - 1) % ModLens.Length);
            Line(ModLens[_modLen], y, 0.028f, Color.white, -0.17f);
            Btn(">", 0.2f, y, 0.07f, () => _modLen = (_modLen + 1) % ModLens.Length); y -= 0.085f;
            bool has = !string.IsNullOrEmpty(_modTarget);
            void Ask(byte act) { if (!has) { RagoGame.I?.Player?.Toast("Find a player first"); return; } RelayBackend.Instance?.ModRequest(act, _modTarget, ModReasons[_modReason], ModHours[_modLen]); }
            Btn("BAN", -0.39f, y, 0.22f, () => Ask(Rago.Relay.P.ActBan), Stop);
            Btn("UNBAN", -0.14f, y, 0.22f, () => Ask(Rago.Relay.P.ActUnban));
            Btn("WIPE", 0.11f, y, 0.22f, () => Ask(Rago.Relay.P.ActWipe), new Color(0.55f, 0.35f, 0.1f));
            Btn("KICK (MARKET)", 0.39f, y, 0.26f, () => Ask(Rago.Relay.P.ActKick)); y -= 0.065f;
            Line("Kick only works on someone in the Market right now. Wipe resets their money, stash, gear and level.", y, 0.02f, UI.Dim); y -= 0.075f;
            // LIVE EVENT: starts straight away for everyone on the server (no approval needed)
            Line("LIVE EVENT:", y, 0.026f, new Color(1f, 0.8f, 0.25f));
            Btn("<", -0.27f, y, 0.07f, () => _evKind = _evKind <= 1 ? LiveEvent.Names.Length - 1 : _evKind - 1);
            Line(LiveEvent.Names[_evKind], y, 0.024f, Color.white, -0.22f);
            Btn(">", 0.04f, y, 0.07f, () => _evKind = _evKind >= LiveEvent.Names.Length - 1 ? 1 : _evKind + 1);
            bool nuke = _evKind == LiveEvent.Nuke;   // the nuke's length is fixed: a 1-minute countdown (+1 for the blast)
            int len = nuke ? LiveEvent.NukeMinutes : LiveEvent.Lengths[_evLen];
            Btn(len >= 60 ? (len / 60) + " H" : len + " MIN", 0.15f, y, 0.12f, () => { if (!nuke) _evLen = (_evLen + 1) % LiveEvent.Lengths.Length; });
            Btn(nuke ? "LAUNCH" : "START", 0.29f, y, 0.14f, () => RelayBackend.Instance?.StartEvent((byte)_evKind, len), nuke ? Stop : Go);
            Btn("STOP", 0.44f, y, 0.13f, () => RelayBackend.Instance?.StartEvent(0, 0), Stop); y -= 0.055f;
            Line(LiveEvent.Active ? "Running: " + LiveEvent.Names[LiveEvent.Kind] + " (" + LiveEvent.Left() + " left, by " + LiveEvent.By + ")" : "For every player on every server - " + LiveEvent.Blurb[_evKind] + ".", y, 0.02f, LiveEvent.Active ? new Color(1f, 0.8f, 0.25f) : UI.Dim);
        }

        // ------------------------------------------------------------------------------- DLC
        string _dlcSel, _dlcSkin;
        static Material _swatchFrame;

        /// <summary>DLC tab: every pack as a tile (price, owned); tap one for everything that comes with it, and BUY.</summary>
        void DrawDlc()
        {
            Db.Init();
            var pack = _dlcSel != null ? Dlc.Get(_dlcSel) : null;
            if (pack == null) { DrawDlcList(); return; }
            DrawDlcPack(pack);
        }

        void DrawDlcList()
        {
            var all = new List<Dlc.Pack>(Dlc.Shown());
            const int per = 6;
            int pages = Mathf.Max(1, (all.Count + per - 1) / per);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            Line("DLC PACKS - tap one to see what's in it", 0.22f, 0.024f, UI.Dim);
            var rifle = Db.Get("sentinel") ?? Db.Get("p9");
            for (int k = 0; k < per; k++)
            {
                int idx = _page * per + k; if (idx >= all.Count) break;
                var p = all[idx];
                float x = -0.36f + (k % 3) * 0.36f, y = 0.06f - (k / 3) * 0.27f;
                var pp = p;
                bool owned = Dlc.Owns(p.id);
                UI.Button(_content, "", new Vector3(x, y, 0), new Vector2(0.34f, 0.25f), () => { _dlcSel = pp.id; _dlcSkin = null; _dirty = true; Sfx.UI(Synth.Beep(), 0.2f); }, Color.Lerp(p.col, new Color(0.12f, 0.12f, 0.14f), 0.72f));
                UI.Text(_content, p.name, new Vector3(x, y + 0.095f, -0.02f), 0.022f, Color.Lerp(p.col, Color.white, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
                var skins = Dlc.Skins(p);
                if (rifle != null && skins.Count > 0)
                {
                    var holder = new GameObject("Preview").transform; holder.SetParent(_content, false); holder.localPosition = new Vector3(x, y + 0.02f, -0.05f);
                    try { SpawnBrowser.Preview(rifle, holder, 0.26f, 0.08f, skins[0].id, 15f); } catch { }
                }
                UI.Text(_content, skins.Count + " skins" + (p.items.Length > 0 ? " + " + p.items.Length + " items" : ""), new Vector3(x, y - 0.05f, -0.02f), 0.017f, UI.Dim, TextAnchor.MiddleCenter);
                UI.Text(_content, owned ? "OWNED" : Dlc.Price(p), new Vector3(x, y - 0.09f, -0.02f), 0.02f, owned ? new Color(0.5f, 1f, 0.55f) : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            if (pages > 1)
            {
                Btn("<", -0.2f, -0.39f, 0.09f, () => _page = (_page + pages - 1) % pages);
                Line("PAGE " + (_page + 1) + " / " + pages, -0.39f, 0.024f, Color.white, -0.07f);
                Btn(">", 0.2f, -0.39f, 0.09f, () => _page = (_page + 1) % pages);
            }
        }

        void DrawDlcPack(Dlc.Pack p)
        {
            bool owned = Dlc.Owns(p.id);
            var skins = Dlc.Skins(p);
            if (_dlcSkin == null && skins.Count > 0) _dlcSkin = skins[0].id;
            Btn("< ALL PACKS", -0.42f, 0.22f, 0.22f, () => { _dlcSel = null; });
            Line(p.name, 0.22f, 0.034f, Color.Lerp(p.col, Color.white, 0.45f), -0.28f);
            Line(p.tagline, 0.165f, 0.022f, UI.Dim, -0.28f);
            // the rifle wearing the skin you picked
            var rifle = Db.Get("sentinel") ?? Db.Get("p9");
            if (rifle != null)
            {
                var holder = new GameObject("Showcase").transform; holder.SetParent(_content, false); holder.localPosition = new Vector3(-0.2f, 0.05f, -0.08f);
                try { SpawnBrowser.Preview(rifle, holder, 0.6f, 0.2f, _dlcSkin, 20f); } catch { }
            }
            var cur = skins.Find(k => k.id == _dlcSkin);
            if (cur != null) Line(cur.name.ToUpper(), -0.07f, 0.024f, Color.white, -0.5f);
            // the skins: tap one to put it on the rifle
            Line("SKINS (" + skins.Count + ")  - tap one to try it on", -0.11f, 0.02f, UI.Accent, -0.5f);
            if (_swatchFrame == null) _swatchFrame = Mats.Lit("dlc_frame", Color.white, 0.8f, 0.6f);
            for (int k = 0; k < skins.Count && k < 5; k++)
            {
                var sk = skins[k];
                float x = -0.45f + k * 0.12f, y = -0.19f;
                UI.Button(_content, "", new Vector3(x, y, 0), new Vector2(0.11f, 0.09f), () => { _dlcSkin = sk.id; }, sk.id == _dlcSkin ? new Color(0.9f, 0.85f, 0.4f) : new Color(0.25f, 0.26f, 0.28f));
                var print = new MB(); print.Box(Vector3.zero, new Vector3(0.095f, 0.07f, 0.004f), Color.white, 0f);
                print.Build("Print", _content, sk.mat != null ? sk.mat() : _swatchFrame).transform.localPosition = new Vector3(x, y + 0.005f, -0.02f);
                string nm = sk.name; if (nm.Length > 12) nm = nm.Substring(0, 11) + ".";
                UI.Text(_content, nm, new Vector3(x, y - 0.06f, -0.02f), 0.014f, Color.white, TextAnchor.MiddleCenter);
            }
            // what comes with it
            float ry = 0.1f;
            Line("COMES WITH", ry, 0.024f, UI.Accent, 0.16f); ry -= 0.05f;
            Line("- " + skins.Count + " gun skins for every gun", ry, 0.019f, Color.white, 0.16f); ry -= 0.035f;
            Line("  (paint them on at the booth", ry, 0.017f, UI.Dim, 0.16f); ry -= 0.03f;
            Line("   in your bunker Armory)", ry, 0.017f, UI.Dim, 0.16f); ry -= 0.045f;
            if (p.items.Length > 0)
            {
                var counts = new Dictionary<string, int>(); var order = new List<string>();
                foreach (var id in p.items) { if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); } counts[id]++; }
                for (int k = 0; k < order.Count && k < 3; k++)
                {
                    var def = Db.Get(order[k]); if (def == null) continue;
                    var holder = new GameObject("Item").transform; holder.SetParent(_content, false); holder.localPosition = new Vector3(0.2f, ry - 0.005f, -0.05f);
                    try { SpawnBrowser.Preview(def, holder, 0.07f, 0.045f, null, 30f); } catch { }
                    Line((counts[order[k]] > 1 ? counts[order[k]] + "x " : "") + def.name, ry, 0.019f, Color.white, 0.26f); ry -= 0.06f;
                }
                Line("  (items go to your Deliveries screen)", ry, 0.017f, UI.Dim, 0.16f);
            }
            // price and BUY
            if (owned)
            {
                Line("OWNED", -0.3f, 0.05f, new Color(0.5f, 1f, 0.55f), 0.2f);
            }
            else
            {
                Line(Dlc.Price(p), -0.27f, 0.05f, Color.white, 0.16f);
                var pp = p;
                Btn("BUY", 0.4f, -0.29f, 0.2f, () =>
                {
                    Dlc.Buy(pp, (ok, msg) =>
                    {
                        if (ok) Sfx.UI(Synth.Cash(), 0.7f); else Sfx.UI(Synth.UiError(), 0.4f);
                        RagoGame.I?.Player?.Toast(msg);
                        _dirty = true;
                    });
                }, Go);
            }
        }

        float _calT = -1f;
        void DrawSettings()
        {
            var p = Profile.Current;
            float y = 0.2f;
            Btn(_page == 0 ? "MORE >" : "< BACK", 0.42f, -0.39f, 0.2f, () => { _page = _page == 0 ? 1 : 0; });
            if (_page == 1) { DrawComfort(p); return; }
            Line("Grip angle:  " + Hand.GripPitch + " deg", y); Btn("-", 0.25f, y, 0.1f, () => { Hand.GripPitch -= 5; SaveSettings(); }); Btn("+", 0.4f, y, 0.1f, () => { Hand.GripPitch += 5; SaveSettings(); }); y -= 0.085f;
            Line("Turning:  " + (PlayerRig.SmoothTurn ? "Smooth" : "Snap " + PlayerRig.SnapAngle), y);
            Btn("MODE", 0.25f, y, 0.14f, () => { PlayerRig.SmoothTurn = !PlayerRig.SmoothTurn; SaveSettings(); });
            Btn("ANGLE", 0.42f, y, 0.14f, () => { PlayerRig.SnapAngle = PlayerRig.SnapAngle < 40 ? 45 : PlayerRig.SnapAngle < 60 ? 90 : 30; SaveSettings(); }); y -= 0.085f;
            // volumes: two to a row - master, effects / weather & ambience, voice chat (there's no music in the game)
            void Vol(string label, float x, float yy, System.Func<float> get, System.Action<float> set)
            {
                Line(label + " " + Mathf.RoundToInt(get() * 100) + "%", yy, 0.026f, Color.white, x);
                Btn("-", x + 0.34f, yy, 0.07f, () => { set(Mathf.Max(0f, Mathf.Round((get() - 0.1f) * 10f) / 10f)); Sfx.ApplyVolumes(); SaveSettings(); });
                Btn("+", x + 0.43f, yy, 0.07f, () => { set(Mathf.Min(1f, Mathf.Round((get() + 0.1f) * 10f) / 10f)); Sfx.ApplyVolumes(); SaveSettings(); });
            }
            Vol("Master", -0.52f, y, () => Sfx.MasterVolume, v => Sfx.MasterVolume = v);
            Vol("Effects", 0.02f, y, () => Sfx.Effects, v => Sfx.Effects = v); y -= 0.075f;
            Vol("Weather", -0.52f, y, () => Sfx.Ambience, v => Sfx.Ambience = v);
            Vol("Voices", 0.02f, y, () => Sfx.Voices, v => Sfx.Voices = v); y -= 0.085f;
            int q = p != null ? Mathf.Clamp(p.quality, 0, 2) : 1;
            Line("Graphics:  " + (q == 0 ? "Low (fastest)" : q == 1 ? "Medium" : "High") + "  - next map you load", y);
            Btn("CHANGE", 0.32f, y, 0.24f, () => { if (Profile.Current != null) { Profile.Current.quality = (Mathf.Clamp(Profile.Current.quality, 0, 2) + 1) % 3; SaveSettings(); } }); y -= 0.085f;
            Line("Microphone:  " + (NetHub.Voice == null ? "not installed" : NetHub.Voice.Muted ? "muted" : "on"), y);
            if (NetHub.Voice != null) Btn(NetHub.Voice.Muted ? "UNMUTE" : "MUTE", 0.32f, y, 0.24f, () => { NetHub.Voice.SetMuted(!NetHub.Voice.Muted); SaveSettings(); });
            y -= 0.1f;
            Line("Network:  " + (NetHub.Backend == null ? "offline" : NetHub.Backend.Status), y, 0.024f, UI.Dim); y -= 0.05f;
            Line("Callsign:  " + (p != null ? p.DisplayName : ""), y, 0.024f, UI.Dim); y -= 0.06f;
            if (ErrorLog.Recent.Count > 0)
            {
                Line("Recent errors (send these if something breaks):", y, 0.022f, new Color(1f, 0.5f, 0.4f)); y -= 0.04f;
                for (int i = Mathf.Max(0, ErrorLog.Recent.Count - 3); i < ErrorLog.Recent.Count; i++) { Line(UI.Wrap(ErrorLog.Recent[i], 70), y, 0.017f, new Color(1f, 0.75f, 0.7f)); y -= 0.05f; }
            }
        }

        /// <summary>Settings page 2: auto sprint, seated / standing, height calibration.</summary>
        void DrawComfort(Profile p)
        {
            if (p == null) return;
            float y = 0.25f;
            Line("Auto sprint:  " + (p.autoSprint ? "ON - push the stick right forward to run" : "OFF - click the left stick to run"), y, 0.026f);
            Btn(p.autoSprint ? "ON" : "OFF", 0.42f, y, 0.16f, () => { p.autoSprint = !p.autoSprint; p.Save(); }, p.autoSprint ? Go : (Color?)null); y -= 0.09f;
            // (3.51) easy reloads (Quest 2 controllers make lining a mag up with the magwell fiddly)
            Line("Auto mag:  " + (p.autoMag ? "ON - hold a mag near the magwell and it goes in" : "OFF - line the mag up and push it in"), y, 0.026f);
            Btn(p.autoMag ? "ON" : "OFF", 0.42f, y, 0.16f, () => { p.autoMag = !p.autoMag; p.Save(); }, p.autoMag ? Go : (Color?)null); y -= 0.09f;
            Line("Play mode:  " + (p.seated ? "SEATED - you sit, your operator stands" : "STANDING"), y, 0.026f);
            Btn(p.seated ? "SEATED" : "STANDING", 0.4f, y, 0.2f, () => { p.seated = !p.seated; p.Save(); }); y -= 0.1f;
            Line("Height:  " + (p.seated ? (p.seatCal >= 0f ? "calibrated (+" + p.seatCal.ToString("0.00") + " m)" : "default lift (+0.50 m)") : (Mathf.Abs(p.heightCal) > 0.005f ? "calibrated (" + (p.heightCal >= 0 ? "+" : "") + p.heightCal.ToString("0.00") + " m)" : "not calibrated")), y, 0.026f);
            Btn(_calT >= 0f ? "..." + Mathf.CeilToInt(_calT) : "CALIBRATE", 0.38f, y, 0.24f, () => { if (_calT < 0f) _calT = 3f; }, Go); y -= 0.08f;
            Line(p.seated ? "Sit up straight, look ahead, press CALIBRATE and keep still for 3 seconds." : "Stand up straight, look ahead, press CALIBRATE and keep still for 3 seconds.", y, 0.022f, UI.Dim); y -= 0.06f;
            Btn("RESET HEIGHT", -0.3f, y, 0.3f, () => { if (p.seated) p.seatCal = -1f; else p.heightCal = 0f; p.Save(); }); y -= 0.1f;
            Line("Calibrating lines your eyes up with your operator's, so your hands, body and gun all sit right.", y, 0.02f, UI.Dim);
        }

        void CalTick()
        {
            if (_calT < 0f) return;
            float before = _calT;
            _calT -= Time.unscaledDeltaTime;
            if (Mathf.CeilToInt(before) != Mathf.CeilToInt(_calT)) { _dirty = true; Sfx.UI(Synth.Beep(), 0.2f); }
            if (_calT <= 0f) { _calT = -1f; string res = PlayerRig.Calibrate(); RagoGame.I?.Player?.Toast(res); Sfx.UI(Synth.QueueFound(), 0.4f); _dirty = true; }
        }

        static void SaveSettings()
        {
            var p = Profile.Current; if (p == null) return;
            p.gripPitch = Hand.GripPitch; p.smoothTurn = PlayerRig.SmoothTurn; p.snapAngle = PlayerRig.SnapAngle; p.volume = Sfx.MasterVolume; p.volEffects = Sfx.Effects; p.volAmbience = Sfx.Ambience; p.volVoice = Sfx.Voices;
            p.Save();
        }
    }
}
