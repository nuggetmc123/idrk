using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The menu on the ≡ button of the left controller: play, loadout,
    /// friends, party, chat, report, settings, controls, profile, and the
    /// mod tools for moderators.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        enum Tab { Play, Loadout, Friends, Party, Chat, Report, Settings, Controls, Profile, Mod }

        UIPanel panel;
        UIKeyboard keyboard;
        Tab tab = Tab.Play;
        int page;
        public bool IsOpen => panel != null;
        ControlAction? listening;
        string reportTarget, reportTargetName;
        string modTarget = "";
        string modResult = "";
        List<Dictionary<string, object>> modReports;
        const float W = 1.1f, H = 0.74f;
        const float CX = 0.09f;   // content center x

        Session S => Game.I.session;

        void Start()
        {
            Game.I.SessionChanged += Hook;
            Hook();
        }

        void Hook()
        {
            if (S == null) return;
            S.FriendsChanged += RefreshIf(Tab.Friends, Tab.Party);
            S.PartyChanged += RefreshIf(Tab.Party, Tab.Play, Tab.Friends);
            S.PartyInvite += (a, b) => RefreshIf(Tab.Party)();
            S.Chat += (a, b, c) => RefreshIf(Tab.Chat)();
            S.RoomEntered += RefreshIf(Tab.Report, Tab.Play, Tab.Mod);
            S.PlayerJoined += p => RefreshIf(Tab.Report)();
            S.PlayerLeft += p => RefreshIf(Tab.Report)();
            S.Muted += (a, b) => RefreshIf(Tab.Chat)();
            S.ModResult += OnModResult;
            S.NameResult += (ok, err) => RefreshIf(Tab.Profile)();
        }

        Action RefreshIf(params Tab[] tabs) => () => { if (IsOpen && Array.IndexOf(tabs, tab) >= 0) Build(); };

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open(int tabIndex = -1)
        {
            if (tabIndex >= 0) tab = (Tab)tabIndex;
            if (tab == Tab.Mod && !S.IsAdmin) tab = Tab.Play;
            if (panel == null)
            {
                panel = UIPanel.Create("Menu", new Vector2(W, H), UIKit.PanelColor);
                panel.PlaceInFront(PlayerRig.I.head, 0.85f, 0.12f);
            }
            AudioManager.Ui("ui_click");
            PlayerRig.I.MenuOpen = true;
            UIPointer.I.Active = true;
            VRInput.I.MenuOpenForDesktop = true;
            Build();
        }

        public void Close()
        {
            if (panel != null) Destroy(panel.gameObject);
            panel = null;
            keyboard = null;
            listening = null;
            if (PlayerRig.I) PlayerRig.I.MenuOpen = false;
            if (UIPointer.I) UIPointer.I.Active = Hud.I != null && Hud.I.PopupOpen;
            if (VRInput.I) VRInput.I.MenuOpenForDesktop = false;
            AudioManager.Ui("ui_back");
        }

        void Update()
        {
            if (listening.HasValue && IsOpen)
            {
                var b = VRInput.I.AnyButtonDown();
                if (b != XRButton.None)
                {
                    Save.Settings.Set(listening.Value, b);
                    Save.Store();
                    listening = null;
                    AudioManager.Ui("ui_click");
                    Build();
                }
            }
        }

        /* ---------------- layout helpers ---------------- */

        TextMesh L(string text, float x, float y, float h = 0.03f, Color? c = null, TextAnchor a = TextAnchor.MiddleLeft)
            => panel.Text(text, new Vector2(x, y), h, c ?? Color.white, a);

        UIButton B(string text, float x, float y, float w, float h, Action click, bool selected = false, float textH = 0f)
        {
            var b = panel.Button(text, new Vector2(x, y), new Vector2(w, h), click, textH);
            if (selected) b.SetColors(new Color(0.18f, 0.5f, 0.25f), UIKit.Good);
            return b;
        }

        void Title(string t) => L(t, -0.33f, H / 2 - 0.05f, 0.045f, UIKit.Accent);

        UIKeyboard Keyboard(string placeholder, int max, bool spaces, string initial, Action<string> submit)
        {
            keyboard = UIKeyboard.Create(panel.transform, new Vector3(CX, -H / 2 - 0.2f, -0.02f), placeholder, max, spaces, initial);
            keyboard.transform.localRotation = Quaternion.Euler(25, 0, 0);
            keyboard.onSubmit = submit;
            return keyboard;
        }

        void Build()
        {
            if (panel == null) return;
            panel.Clear();
            if (keyboard != null) { Destroy(keyboard.gameObject); keyboard = null; }

            // tabs
            var tabs = new List<Tab> { Tab.Play, Tab.Loadout, Tab.Friends, Tab.Party, Tab.Chat, Tab.Report, Tab.Settings, Tab.Controls, Tab.Profile };
            if (S.IsAdmin) tabs.Add(Tab.Mod);
            for (int i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                string label = t == Tab.Mod ? "MOD" : t.ToString();
                B(label, -W / 2 + 0.105f, H / 2 - 0.055f - i * 0.062f, 0.17f, 0.052f, () => { tab = t; page = 0; listening = null; Build(); }, t == tab, 0.024f);
            }
            // bottom bar
            if (S.InMatch) B("Leave Match", 0.22f, -H / 2 + 0.045f, 0.2f, 0.055f, () => { Game.I.GoHub(); Close(); });
            B("Close", 0.44f, -H / 2 + 0.045f, 0.16f, 0.055f, Close);

            switch (tab)
            {
                case Tab.Play: BuildPlay(); break;
                case Tab.Loadout: BuildLoadout(); break;
                case Tab.Friends: BuildFriends(); break;
                case Tab.Party: BuildParty(); break;
                case Tab.Chat: BuildChat(); break;
                case Tab.Report: BuildReport(); break;
                case Tab.Settings: BuildSettings(); break;
                case Tab.Controls: BuildControls(); break;
                case Tab.Profile: BuildProfile(); break;
                case Tab.Mod: BuildMod(); break;
            }
        }

        /* ---------------- pages ---------------- */

        void BuildPlay()
        {
            Title("Play");
            bool canPick = S.IsLeaderOrSolo;
            L(canPick ? (S.InParty ? $"You lead a party of {S.PartyMembers.Count}. Everyone comes with you." : "Pick a mode (or walk into a portal in the hub).")
                      : "Your party leader picks where the party goes.", -0.33f, 0.25f, 0.026f, UIKit.Muted);
            for (int i = 0; i < Hub.Modes.Length; i++)
            {
                var p = Hub.Modes[i];
                float x = i % 2 == 0 ? -0.1f : 0.32f;
                float y = 0.15f - (i / 2) * 0.12f;
                var b = B(p.label, x, y, 0.4f, 0.09f, () => { Game.I.GoMode(p.mode); Close(); }, false, 0.028f);
                b.interactable = canPick;
                L(p.info, x, y - 0.03f, 0.018f, UIKit.Muted, TextAnchor.MiddleCenter).transform.localPosition += new Vector3(0, 0, -0.006f);
            }
            if (S.InMatch) L("You're in a match. 'Leave Match' takes you (and your party) back to the hub.", -0.33f, -0.24f, 0.022f, UIKit.Muted);
            else if (!S.InHub) B("Go to Hub", CX, -0.24f, 0.3f, 0.06f, () => { Game.I.GoHub(); Close(); });
        }

        void BuildLoadout()
        {
            Title("Loadout");
            var st = Save.Settings;
            L("Primary (left hip)", -0.33f, 0.27f, 0.026f, UIKit.Muted);
            int col = 0, row = 0;
            for (int i = 0; i < GunCatalog.All.Length; i++)
            {
                var g = GunCatalog.All[i];
                if (g.cls == GunClass.Pistol) continue;
                int idx = i;
                float x = -0.24f + col * 0.205f, y = 0.215f - row * 0.06f;
                B(g.name, x, y, 0.195f, 0.05f, () => { st.primaryGun = idx; Save.Store(); Game.I.LoadoutChanged(); Build(); }, st.primaryGun == idx, 0.02f);
                if (++col == 4) { col = 0; row++; }
            }
            float y2 = 0.215f - 4 * 0.06f - 0.03f;
            L("Sidearm (right hip)", -0.33f, y2, 0.026f, UIKit.Muted);
            col = 0;
            for (int i = 0; i < GunCatalog.All.Length; i++)
            {
                var g = GunCatalog.All[i];
                if (g.cls != GunClass.Pistol) continue;
                int idx = i;
                B(g.name, -0.24f + (col % 4) * 0.205f, y2 - 0.055f - (col / 4) * 0.06f, 0.195f, 0.05f, () => { st.sidearmGun = idx; Save.Store(); Game.I.LoadoutChanged(); Build(); }, st.sidearmGun == idx, 0.02f);
                col++;
            }
            var def = GunCatalog.Get(st.primaryGun);
            L($"{def.name}: {GunCatalog.ClassName(def.cls).TrimEnd('s')} · {def.damage * def.pellets:0} dmg · {def.magSize} rounds · {def.rpm:0} rpm",
              -0.33f, -0.29f, 0.02f, UIKit.Muted);
        }

        void BuildFriends()
        {
            Title("Friends");
            if (!S.Online) { L(UIKit.Wrap("Friends and parties need online play. Ask the game owner to set it up (in Unity: Flopzone > Set Up Multiplayer).", 0.8f, 0.028f), -0.33f, 0.1f, 0.028f); return; }
            B("+ Add Friend", 0.4f, H / 2 - 0.05f, 0.2f, 0.05f, () =>
            {
                Build();
                Keyboard("name or player ID", 16, false, "", v => { if (v.Trim().Length > 0) S.FriendRequest(v.Trim()); Build(); });
            }, false, 0.022f);
            float y = 0.25f;
            foreach (var r in S.Incoming)
            {
                var req = r;
                L($"{req.name} wants to be friends", -0.33f, y, 0.026f, UIKit.Accent);
                B("Accept", 0.3f, y, 0.12f, 0.045f, () => S.FriendAccept(req.id), false, 0.02f);
                B("No", 0.44f, y, 0.1f, 0.045f, () => S.FriendDecline(req.id), false, 0.02f);
                y -= 0.055f;
                if (y < 0.1f) break;
            }
            if (S.Friends.Count == 0) L("No friends yet. Add someone by their name or player ID.", -0.33f, y - 0.02f, 0.025f, UIKit.Muted);
            const int per = 6;
            int pages = Mathf.Max(1, Mathf.CeilToInt(S.Friends.Count / (float)per));
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = page * per; i < Mathf.Min(S.Friends.Count, (page + 1) * per); i++)
            {
                var f = S.Friends[i];
                L(f.name, -0.33f, y, 0.028f, f.online ? Color.white : UIKit.Muted);
                L(UIKit.Clip(f.where, 34), -0.33f, y - 0.026f, 0.018f, f.online ? UIKit.Good : UIKit.Muted);
                var inv = B("Invite", 0.27f, y - 0.01f, 0.13f, 0.045f, () => S.PartyInviteFriend(f.id), false, 0.02f);
                inv.interactable = f.online && S.IsLeaderOrSolo;
                B("Remove", 0.43f, y - 0.01f, 0.14f, 0.045f, () => S.FriendRemove(f.id), false, 0.02f);
                y -= 0.065f;
            }
            if (pages > 1)
            {
                B("<", 0.0f, -0.25f, 0.06f, 0.05f, () => { page--; Build(); });
                L($"{page + 1}/{pages}", 0.06f, -0.25f, 0.024f, UIKit.Muted);
                B(">", 0.16f, -0.25f, 0.06f, 0.05f, () => { page++; Build(); });
            }
            foreach (var o in S.Outgoing) { L("Request sent to " + o.name, -0.33f, -0.3f, 0.02f, UIKit.Muted); break; }
        }

        void BuildParty()
        {
            Title("Party");
            if (!S.Online) { L("Parties need online play (in Unity: Flopzone > Set Up Multiplayer).", -0.33f, 0.1f, 0.028f); return; }
            float y = 0.24f;
            if (!string.IsNullOrEmpty(S.PendingInvitePartyId) && S.PendingInvitePartyId != S.PartyId)
            {
                L($"{S.PendingInviteFrom} invited you to their party", -0.33f, y, 0.027f, UIKit.Accent);
                B("Join", 0.3f, y, 0.12f, 0.045f, () => { S.PartyAccept(S.PendingInvitePartyId); S.PendingInvitePartyId = null; }, false, 0.022f);
                B("No", 0.44f, y, 0.1f, 0.045f, () => { S.PartyDecline(S.PendingInvitePartyId); S.PendingInvitePartyId = null; Build(); }, false, 0.022f);
                y -= 0.08f;
            }
            if (!S.InParty)
            {
                L(UIKit.Wrap("Up to 6 people. Wherever the party leader goes, the whole party follows. In team modes the party stays on one team; a full party of 6 in 3v3 splits 3 and 3.", 0.82f, 0.024f), -0.33f, y - 0.02f, 0.024f, UIKit.Muted);
                InviteList(y - 0.12f);
                return;
            }
            L($"Party ({S.PartyMembers.Count}/6)", -0.33f, y, 0.03f);
            y -= 0.06f;
            bool lead = S.PartyLeader == S.MyId;
            foreach (var m in S.PartyMembers)
            {
                var mem = m;
                bool isLeader = mem.Key == S.PartyLeader;
                L((isLeader ? "★ " : "   ") + mem.Value + (mem.Key == S.MyId ? " (you)" : ""), -0.33f, y, 0.028f, isLeader ? UIKit.Accent : Color.white);
                if (lead && mem.Key != S.MyId) B("Kick", 0.42f, y, 0.12f, 0.045f, () => S.PartyKick(mem.Key), false, 0.02f);
                y -= 0.055f;
            }
            if (lead && S.PartyMembers.Count < 6) InviteList(y - 0.01f);
            B("Leave Party", CX, -0.27f, 0.26f, 0.055f, () => S.PartyLeave());
        }

        /// <summary>Buttons to invite the people in this room.</summary>
        void InviteList(float y)
        {
            var here = new List<PlayerInfo>();
            foreach (var p in S.Players.Values)
                if (p.id != S.MyId && !p.bot && !S.PartyMembers.Exists(m => m.Key == p.id)) here.Add(p);
            if (here.Count == 0) { L("Nobody else is in this room to invite yet.", -0.33f, y, 0.024f, UIKit.Muted); return; }
            L("Invite someone in this room:", -0.33f, y, 0.024f, UIKit.Muted);
            for (int i = 0; i < Mathf.Min(here.Count, 8); i++)
            {
                var p = here[i];
                B(p.name, -0.24f + (i % 4) * 0.205f, y - 0.055f - (i / 4) * 0.055f, 0.195f, 0.045f, () => S.PartyInviteFriend(p.id), false, 0.02f);
            }
        }

        static readonly string[] QuickChat = { "gg", "nice shot!", "follow me", "lol", "help!", "thanks!", "on my way", "get the flag!" };

        void BuildChat()
        {
            Title("Chat");
            if (S.IsMuted)
            {
                long mins = Math.Max(1, (S.MuteUntil - S.NowMs) / 60000 + 1);
                L($"You are muted for about {mins} more minute{(mins == 1 ? "" : "s")}.", -0.33f, 0.27f, 0.026f, UIKit.Bad);
            }
            float y = 0.22f;
            int start = Mathf.Max(0, S.ChatLog.Count - 8);
            for (int i = start; i < S.ChatLog.Count; i++) { L(UIKit.Clip(S.ChatLog[i], 60), -0.33f, y, 0.022f); y -= 0.035f; }
            if (S.ChatLog.Count == 0) L("No messages yet.", -0.33f, y, 0.022f, UIKit.Muted);
            for (int i = 0; i < QuickChat.Length; i++)
            {
                var q = QuickChat[i];
                B(q, -0.24f + (i % 4) * 0.205f, -0.13f - (i / 4) * 0.06f, 0.19f, 0.05f, () => S.SendChat(q), false, 0.02f);
            }
            L("Be nice: slurs and bad words get you muted for 15 minutes.", -0.33f, -0.27f, 0.018f, UIKit.Muted);
            Keyboard("type a message", 100, true, "", v => { if (v.Trim().Length > 0) S.SendChat(v.Trim()); Build(); });
        }

        void BuildReport()
        {
            Title("Report a player");
            var others = new List<PlayerInfo>();
            foreach (var p in S.Players.Values) if (p.id != S.MyId && !p.bot) others.Add(p);
            if (others.Count == 0) { L("Nobody else is here right now.", -0.33f, 0.2f, 0.028f, UIKit.Muted); return; }
            L("1. Who?", -0.33f, 0.27f, 0.026f, UIKit.Muted);
            for (int i = 0; i < Mathf.Min(others.Count, 12); i++)
            {
                var p = others[i];
                B(p.name, -0.24f + (i % 4) * 0.205f, 0.215f - (i / 4) * 0.06f, 0.195f, 0.05f, () => { reportTarget = p.id; reportTargetName = p.name; Build(); }, reportTarget == p.id, 0.02f);
            }
            if (reportTarget == null || !S.Players.ContainsKey(reportTarget)) return;
            L($"2. Why are you reporting {reportTargetName}?", -0.33f, 0.0f, 0.026f, UIKit.Muted);
            string[] reasons = { "Bad language", "Cheating", "Bad name", "Harassment", "Griefing", "Other" };
            for (int i = 0; i < reasons.Length; i++)
            {
                var r = reasons[i];
                B(r, -0.2f + (i % 3) * 0.275f, -0.06f - (i / 3) * 0.065f, 0.26f, 0.055f, () =>
                {
                    S.SendReport(reportTarget, r);
                    reportTarget = null;
                    Build();
                }, false, 0.022f);
            }
            L("Moderators see the report with the recent chat.", -0.33f, -0.23f, 0.02f, UIKit.Muted);
        }

        void Row(string label, float y, string value, Action minus, Action plus)
        {
            L(label, -0.33f, y, 0.026f);
            B("-", 0.06f, y, 0.06f, 0.045f, minus);
            L(value, 0.17f, y, 0.026f, UIKit.Accent, TextAnchor.MiddleCenter);
            B("+", 0.28f, y, 0.06f, 0.045f, plus);
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100) + "%";

        void BuildSettings()
        {
            Title("Settings");
            var st = Save.Settings;
            Action save = () => { Save.Store(); Build(); };
            Row("Master volume", 0.25f, Pct(st.masterVolume), () => { st.masterVolume = Mathf.Clamp01(st.masterVolume - 0.1f); save(); }, () => { st.masterVolume = Mathf.Clamp01(st.masterVolume + 0.1f); save(); });
            Row("Music", 0.19f, Pct(st.musicVolume), () => { st.musicVolume = Mathf.Clamp01(st.musicVolume - 0.1f); save(); }, () => { st.musicVolume = Mathf.Clamp01(st.musicVolume + 0.1f); save(); });
            Row("Sound effects", 0.13f, Pct(st.sfxVolume), () => { st.sfxVolume = Mathf.Clamp01(st.sfxVolume - 0.1f); save(); }, () => { st.sfxVolume = Mathf.Clamp01(st.sfxVolume + 0.1f); save(); });
            L("Turning", -0.33f, 0.05f, 0.026f);
            B("Snap", 0.1f, 0.05f, 0.13f, 0.045f, () => { st.turnStyle = TurnStyle.Snap; save(); }, st.turnStyle == TurnStyle.Snap, 0.022f);
            B("Smooth", 0.25f, 0.05f, 0.13f, 0.045f, () => { st.turnStyle = TurnStyle.Smooth; save(); }, st.turnStyle == TurnStyle.Smooth, 0.022f);
            if (st.turnStyle == TurnStyle.Snap)
            {
                L("Snap angle", -0.33f, -0.01f, 0.026f);
                float[] angles = { 30, 45, 60, 90 };
                for (int i = 0; i < angles.Length; i++) { float a = angles[i]; B(a + "°", 0.06f + i * 0.1f, -0.01f, 0.09f, 0.045f, () => { st.snapAngle = a; save(); }, Mathf.Approximately(st.snapAngle, a), 0.022f); }
            }
            else Row("Turn speed", -0.01f, Mathf.RoundToInt(st.smoothTurnSpeed) + "°/s", () => { st.smoothTurnSpeed = Mathf.Max(30, st.smoothTurnSpeed - 15); save(); }, () => { st.smoothTurnSpeed = Mathf.Min(360, st.smoothTurnSpeed + 15); save(); });
            L("Sprint", -0.33f, -0.07f, 0.026f);
            B("Toggle", 0.1f, -0.07f, 0.13f, 0.045f, () => { st.sprintToggle = true; save(); }, st.sprintToggle, 0.022f);
            B("Hold", 0.25f, -0.07f, 0.13f, 0.045f, () => { st.sprintToggle = false; save(); }, !st.sprintToggle, 0.022f);
            L("Crouch", -0.33f, -0.13f, 0.026f);
            B("Toggle", 0.1f, -0.13f, 0.13f, 0.045f, () => { st.crouchToggle = true; save(); }, st.crouchToggle, 0.022f);
            B("Hold", 0.25f, -0.13f, 0.13f, 0.045f, () => { st.crouchToggle = false; save(); }, !st.crouchToggle, 0.022f);
            B("Reset sound", -0.18f, -0.22f, 0.22f, 0.05f, () => { st.ResetAudio(); save(); }, false, 0.022f);
            B("Reset to default", 0.08f, -0.22f, 0.26f, 0.05f, () => { st.ResetAudio(); st.ResetControls(); save(); Hud.I.Toast("Settings reset to default."); }, false, 0.022f);
        }

        void BuildControls()
        {
            Title("Controls");
            var st = Save.Settings;
            float y = 0.24f;
            foreach (ControlAction a in Enum.GetValues(typeof(ControlAction)))
            {
                var act = a;
                L(GameSettings.ActionName(a), -0.33f, y, 0.028f);
                bool isListening = listening == a;
                L(isListening ? "press a button..." : GameSettings.ButtonName(st.Get(a)), 0.02f, y, 0.026f, isListening ? UIKit.Accent : Color.white);
                B(isListening ? "Cancel" : "Change", 0.42f, y, 0.15f, 0.045f, () => { listening = isListening ? (ControlAction?)null : act; Build(); }, false, 0.022f);
                y -= 0.062f;
            }
            L(UIKit.Wrap("Always: Grip = grab · Trigger = shoot · hold Trigger at your waist = grab a mag · ≡ = this menu · Left stick = move · Right stick = turn", 0.82f, 0.022f), -0.33f, -0.08f, 0.022f, UIKit.Muted);
            B("Reset to default", CX, -0.22f, 0.3f, 0.055f, () => { st.ResetControls(); Save.Store(); listening = null; Build(); Hud.I.Toast("Controls reset to default."); });
        }

        void BuildProfile()
        {
            Title("Profile");
            L("Name", -0.33f, 0.24f, 0.024f, UIKit.Muted);
            L(string.IsNullOrEmpty(S.MyName) ? "(none yet)" : S.MyName, -0.33f, 0.2f, 0.04f);
            B("Change name", 0.38f, 0.21f, 0.22f, 0.05f, () =>
            {
                Build();
                Keyboard("new name", WordFilter.NameMax, false, "", v =>
                {
                    var bad = WordFilter.CheckName(v);
                    if (bad != null) { Hud.I.Toast(bad); return; }
                    S.SetName(v);
                });
            }, false, 0.022f);
            L("Player ID", -0.33f, 0.11f, 0.024f, UIKit.Muted);
            L(string.IsNullOrEmpty(S.MyId) ? "-" : S.MyId, -0.33f, 0.06f, 0.06f, UIKit.Accent);
            L("Give this ID to a moderator if you need help, or to a friend so they can add you.", -0.33f, 0.0f, 0.02f, UIKit.Muted);
            string server = S.Online ? "Online" : "Offline (bots only)";
            L("Status: " + server, -0.33f, -0.07f, 0.026f, S.Online ? UIKit.Good : UIKit.Muted);
            if (S.IsAdmin) L("You are a moderator: see the MOD tab.", -0.33f, -0.12f, 0.024f, UIKit.Accent);
            if (Save.TutorialDone) B("Replay tutorial", CX, -0.22f, 0.26f, 0.05f, () => { Close(); Game.I.StartTutorial(); }, false, 0.022f);
        }

        void OnModResult(Dictionary<string, object> m)
        {
            modResult = m.Str("msg");
            var data = m.Arr("data");
            modReports = null;
            if (data != null)
            {
                var lines = new List<string>();
                modReports = new List<Dictionary<string, object>>();
                foreach (var o in data)
                    if (o is Dictionary<string, object> d)
                    {
                        if (d.Has("targetName")) modReports.Add(d);
                        else lines.Add(d.Str("name") + " [" + d.Str("id") + "] " + d.Str("where"));
                    }
                if (lines.Count > 0) modResult += "\n" + string.Join("\n", lines.GetRange(0, Mathf.Min(lines.Count, 6)));
            }
            else if (m.Obj("data") is Dictionary<string, object> info)
            {
                modResult += $"\nonline: {info.Bool("online")} · {info.Str("where")}\nbanned: {info.Str("banned")} · muted: {info.Str("muted")} · reports: {info.Int("reports")}";
                var chat = info.Arr("recentChat");
                if (chat != null) for (int i = Mathf.Max(0, chat.Count - 3); i < chat.Count; i++) modResult += "\n  " + UIKit.Clip(chat[i] as string, 54);
            }
            if (IsOpen && tab == Tab.Mod) Build();
        }

        void BuildMod()
        {
            Title("Moderator tools");
            if (!S.IsAdmin) return;
            L("Target:", -0.33f, 0.26f, 0.026f, UIKit.Muted);
            L(string.IsNullOrEmpty(modTarget) ? "(pick a player or type an ID)" : modTarget, -0.2f, 0.26f, 0.03f, UIKit.Accent);
            B("Type ID/name", 0.43f, 0.26f, 0.18f, 0.045f, () => { Build(); Keyboard("player ID or name", 16, false, modTarget, v => { modTarget = v.Trim().ToUpperInvariant().StartsWith("FZ") ? v.Trim().ToUpperInvariant() : v.Trim(); Build(); }); }, false, 0.02f);
            int i = 0;
            foreach (var p in S.Players.Values)
            {
                if (p.id == S.MyId || p.bot || i >= 8) continue;
                var pid = p.id;
                B(p.name, -0.24f + (i % 4) * 0.205f, 0.2f - (i / 4) * 0.055f, 0.195f, 0.045f, () => { modTarget = pid; Build(); }, modTarget == pid, 0.02f);
                i++;
            }
            string[] labels = { "Lookup", "Kick", "Mute 15m", "Mute 1h", "Unmute", "Ban 1 day", "Ban forever", "Unban", "Reports", "Who's online" };
            (string action, int mins)[] acts = { ("lookup", 0), ("kick", 0), ("mute", 15), ("mute", 60), ("unmute", 0), ("ban", 1440), ("ban", 0), ("unban", 0), ("reports", 0), ("online", 0) };
            for (int k = 0; k < labels.Length; k++)
            {
                var a = acts[k];
                bool needsTarget = a.action != "reports" && a.action != "online";
                var b = B(labels[k], -0.24f + (k % 5) * 0.165f, 0.07f - (k / 5) * 0.06f, 0.155f, 0.05f, () =>
                {
                    modResult = "...";
                    S.SendMod(a.action, modTarget, a.mins, "Moderator action");
                    Build();
                }, false, 0.02f);
                b.interactable = !needsTarget || modTarget.Length > 0;
            }
            L(UIKit.Wrap(modResult, 0.8f, 0.02f), -0.33f, -0.1f, 0.02f, Color.white, TextAnchor.UpperLeft);
            if (modReports != null)
            {
                for (int k = 0; k < Mathf.Min(3, modReports.Count); k++)
                {
                    var r = modReports[k];
                    float y = -0.08f - k * 0.05f;
                    L(UIKit.Clip($"{r.Str("byName")} → {r.Str("targetName")}: {r.Str("reason")}", 46), -0.33f, y, 0.02f);
                    var tid = r.Str("target");
                    B("Use", 0.47f, y, 0.09f, 0.04f, () => { modTarget = tid; Build(); }, false, 0.018f);
                }
            }
        }
    }
}
