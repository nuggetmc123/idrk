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
        enum Tab { Party, Friends, Recent, Settings, Tester }
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

        public static void Close() { if (I != null) I.Show(false); }

        void Build()
        {
            _panel = UI.Panel(transform, "Menu", Vector3.zero, Quaternion.identity, new Vector2(1.12f, 0.9f), null, true, Skin.Steel);
            _header = UI.Text(_panel, "", new Vector3(-0.52f, 0.39f, 0), 0.034f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Button(_panel, "X", new Vector3(0.5f, 0.39f, 0), new Vector2(0.07f, 0.065f), () => Show(false), new Color(0.5f, 0.15f, 0.1f), BtnStyle.Key);
            string[] names = { "PARTY", "FRIENDS", "RECENT", "SETTINGS", "TESTER" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = (Tab)i;
                _tabBtns.Add(UI.Button(_panel, names[i], new Vector3(-0.432f + i * 0.216f, 0.3f, 0), new Vector2(0.205f, 0.075f), () => { _tab = t; _typing = false; _page = 0; _dirty = true; Sfx.UI(Synth.Beep(), 0.2f); }));
            }
            _content = new GameObject("Content").transform;
            _content.SetParent(_panel, false);
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
            HookNet();
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            bool toggle = XRInputs.VR ? XRInputs.Left.MenuDown : Desktop.MenuKey;
            if (toggle) { if (_open) Show(false); else OpenInFront(); }
            if (!_open) return;
            if (p.Dead || Vector3.Distance(p.Head.position, transform.position) > 2.5f) { Show(false); return; }
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f) { _refresh = 1.5f; if (!_typing) _dirty = true; }
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
            if (real == null) { pl.Toast("No player called " + callsign + " found. Check the spelling and tag, or play with them once (raid, Market or party)"); Sfx.UI(Synth.UiError(), 0.3f); return; }
            callsign = real;
            prof.friends.Add(callsign);
            prof.Save();
            pl.Toast("Added " + callsign + " as a friend");
            Sfx.UI(Synth.QueueFound(), 0.35f);
        }

        /// <summary>The real callsign of a player the game has actually met (RECENT, or with you now), or null.</summary>
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
            NetHub.Backend.InviteToParty(callsign);
            pl.Toast("Invited " + callsign + (RagoGame.I.Area == Area.Raid ? " - you'll team up back in your bunkers" : ""));
            Sfx.UI(Synth.Beep(), 0.3f);
        }

        // ------------------------------------------------------------------ drawing
        void Rebuild()
        {
            if (_content != null) Destroy(_content.gameObject);
            _content = new GameObject("Content").transform;
            _content.SetParent(_panel, false);
            var prof = Profile.Current;
            _header.text = "MENU   " + (prof != null ? prof.DisplayName : "");
            for (int i = 0; i < _tabBtns.Count; i++) _tabBtns[i].SetColor(i == (int)_tab ? new Color(0.2f, 0.42f, 0.2f) : new Color(0.28f, 0.3f, 0.32f));
            if (_typing) { DrawKeyboard(); return; }
            switch (_tab)
            {
                case Tab.Party: DrawParty(); break;
                case Tab.Friends: DrawFriends(); break;
                case Tab.Recent: DrawRecent(); break;
                case Tab.Tester: DrawTester(); break;
                default: DrawSettings(); break;
            }
        }

        TextMesh Line(string text, float y, float size = 0.03f, Color? col = null, float x = -0.52f) =>
            UI.Text(_content, text, new Vector3(x, y, 0), size, col ?? Color.white, TextAnchor.MiddleLeft);

        void Btn(string label, float x, float y, float w, System.Action click, Color? col = null) =>
            UI.Button(_content, label, new Vector3(x, y, 0), new Vector2(w, 0.065f), () => { click(); _dirty = true; }, col);

        static readonly Color Go = new Color(0.2f, 0.42f, 0.2f), Stop = new Color(0.5f, 0.15f, 0.1f);

        // ------------------------------------------------------------------------------- tester tools
        static readonly string[] Cats = { "GUNS", "ARMOR", "ATTACHMENTS", "SIGHTS", "OTHER" };
        static int _cat, _pick, _npcKind;
        static readonly string[] NpcKinds = { "Raider", "Heavy raider", "Doctor Kessler", "The Harbormaster", "The Warden", "The Banker" };

        static List<ItemDef> CatItems(int cat)
        {
            Db.Init();
            var l = new List<ItemDef>();
            switch (cat)
            {
                case 0: l.AddRange(Db.Guns); break;
                case 1: l.AddRange(Db.Armor); l.AddRange(Db.Packs); break;
                case 2: l.AddRange(Db.Attachments); break;
                case 3: l.AddRange(Db.Sights); break;
                default: l.AddRange(Db.Misc); break;
            }
            return l;
        }

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
            Btn("+AMMO", 0.45f, y, 0.14f, () =>
            {
                var g = (pl.HandR.Held as Gun) ?? (pl.HandL.Held as Gun);
                if (g == null) { pl.Toast("Hold a gun to get ammo for it"); return; }
                pl.Gear.Ammo[(int)g.g.cal] += 300; pl.Toast("+300 " + Db.CalName(g.g.cal));
            }); y -= 0.1f;
            // spawn any item
            var items = CatItems(_cat);
            _pick = items.Count == 0 ? 0 : (_pick % items.Count + items.Count) % items.Count;
            Btn("<", -0.47f, y, 0.08f, () => { _cat = (_cat + Cats.Length - 1) % Cats.Length; _pick = 0; });
            Line(Cats[_cat], y, 0.028f, UI.Accent, -0.41f);
            Btn(">", -0.1f, y, 0.08f, () => { _cat = (_cat + 1) % Cats.Length; _pick = 0; }); y -= 0.08f;
            string nm = items.Count > 0 ? items[_pick].name : "-";
            Btn("<", -0.47f, y, 0.08f, () => _pick--);
            Line(nm.Length > 24 ? nm.Substring(0, 23) + "." : nm, y, 0.026f, Color.white, -0.41f);
            Btn(">", 0.1f, y, 0.08f, () => _pick++);
            Btn("SPAWN", 0.36f, y, 0.2f, () => { if (items.Count > 0) { Show(false); Tester.BeginPlace(items[_pick]); } }, Go); y -= 0.1f;
            // spawn raiders / bosses
            Btn("<", -0.47f, y, 0.08f, () => _npcKind = (_npcKind + NpcKinds.Length - 1) % NpcKinds.Length);
            Line(NpcKinds[_npcKind], y, 0.026f, Color.white, -0.41f);
            Btn(">", 0.1f, y, 0.08f, () => _npcKind = (_npcKind + 1) % NpcKinds.Length);
            Btn("SPAWN NPC", 0.36f, y, 0.2f, () => { Show(false); Tester.BeginPlace(null, _npcKind); }, Stop); y -= 0.08f;
            Line("NPCs only spawn in a raid (and only the host's, in multiplayer).", y, 0.02f, UI.Dim);
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
            Btn("+ ADD FRIEND", -0.36f, 0.2f, 0.32f, () => { _typing = true; _typed = ""; Desktop.Typing = true; }, Go);
            var here = NetHub.Online ? NetHub.Backend.SessionPlayers : null;
            if (prof.friends.Count == 0) { Line("No friends yet. Play with someone (a raid, the Market or a party),", 0.08f, 0.026f); Line("then add them from RECENT - or type the callsign of someone you've met.", 0.03f, 0.026f); return; }
            DrawList(prof.friends, 0.1f, name =>
            {
                bool inSession = here != null && FindIn(here, name);
                return (name, inSession ? "here with you" : "");
            }, (name, y) =>
            {
                Btn("INVITE", 0.2f, y, 0.2f, () => Invite(name), Go);
                Btn("REMOVE", 0.42f, y, 0.2f, () => { prof.friends.RemoveAll(x => Key(x) == Key(name)); prof.Save(); }, Stop);
            });
        }

        void DrawRecent()
        {
            var prof = Profile.Current;
            var list = new List<string>();
            var here = NetHub.Online ? NetHub.Backend.SessionPlayers : null;
            if (here != null) foreach (var n in here) list.Add(n);
            foreach (var n in prof.recent) if (!FindIn(list, n)) list.Add(n);
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

        void DrawKeyboard()
        {
            Line("ADD FRIEND - callsign of someone you've played with:", 0.2f, 0.028f, UI.Accent);
            Line((_typed.Length > 0 ? _typed : "") + "_", 0.13f, 0.042f);
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL_", "ZXCVBNM#" };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                {
                    char ch = rows[r][i];
                    UI.Button(_content, ch.ToString(), new Vector3(-0.4725f + i * 0.105f, 0.03f - r * 0.085f, 0), new Vector2(0.095f, 0.075f), () => { if (_typed.Length < 22) _typed += ch; _dirty = true; }, null, BtnStyle.Key);
                }
            Btn("DEL", 0.37f, 0.03f - 3 * 0.085f, 0.2f, () => { if (_typed.Length > 0) _typed = _typed.Substring(0, _typed.Length - 1); });
            Btn("CANCEL", -0.3f, -0.39f, 0.3f, () => { _typing = false; Desktop.Typing = false; }, Stop);
            Btn("ADD", 0.3f, -0.39f, 0.3f, () => { AddFriend(_typed); if (IsFriend(_typed)) { _typing = false; Desktop.Typing = false; } }, Go);
        }

        void DrawSettings()
        {
            var p = Profile.Current;
            float y = 0.2f;
            Line("Grip angle:  " + Hand.GripPitch + " deg", y); Btn("-", 0.25f, y, 0.1f, () => { Hand.GripPitch -= 5; SaveSettings(); }); Btn("+", 0.4f, y, 0.1f, () => { Hand.GripPitch += 5; SaveSettings(); }); y -= 0.085f;
            Line("Turning:  " + (PlayerRig.SmoothTurn ? "Smooth" : "Snap " + PlayerRig.SnapAngle), y);
            Btn("MODE", 0.25f, y, 0.14f, () => { PlayerRig.SmoothTurn = !PlayerRig.SmoothTurn; SaveSettings(); });
            Btn("ANGLE", 0.42f, y, 0.14f, () => { PlayerRig.SnapAngle = PlayerRig.SnapAngle < 40 ? 45 : PlayerRig.SnapAngle < 60 ? 90 : 30; SaveSettings(); }); y -= 0.085f;
            Line("Volume:  " + Mathf.RoundToInt(Sfx.MasterVolume * 100) + "%", y);
            Btn("-", 0.25f, y, 0.1f, () => { Sfx.MasterVolume = Mathf.Max(0f, Sfx.MasterVolume - 0.1f); AudioListener.volume = Sfx.MasterVolume; SaveSettings(); });
            Btn("+", 0.4f, y, 0.1f, () => { Sfx.MasterVolume = Mathf.Min(1f, Sfx.MasterVolume + 0.1f); AudioListener.volume = Sfx.MasterVolume; SaveSettings(); }); y -= 0.085f;
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

        static void SaveSettings()
        {
            var p = Profile.Current; if (p == null) return;
            p.gripPitch = Hand.GripPitch; p.smoothTurn = PlayerRig.SmoothTurn; p.snapAngle = PlayerRig.SnapAngle; p.volume = Sfx.MasterVolume;
            p.Save();
        }
    }
}
