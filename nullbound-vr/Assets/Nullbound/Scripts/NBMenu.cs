using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nullbound
{
    /// The floating menu. Lobby tabs: NULLBOUND (play), FIGHTERS, CONTROLS, VR SETUP, SETTINGS.
    /// In a match the menu button opens it in front of you with a MATCH tab instead.
    public class NBMenu : MonoBehaviour
    {
        public enum Tab { Nullbound, Fighters, Controls, Setup, Settings, Match }

        public static NBMenu Instance { get; private set; }
        public bool Visible => m_Panel != null && m_Panel.gameObject.activeSelf;
        public Tab CurrentTab => m_Tab;

        const float W = 1.5f, H = 0.98f;
        Transform m_Panel, m_Tabs, m_Content;
        Tab m_Tab = Tab.Nullbound;
        int m_SetupStep, m_ViewFighter;
        readonly List<Action> m_Live = new List<Action>();

        // on-panel keyboard
        bool m_Kb;
        string m_KbTitle, m_KbText, m_KbChars;
        int m_KbMax;
        Action<string> m_KbDone;

        public static NBMenu Create()
        {
            var g = new GameObject("NB_Menu");
            var m = g.AddComponent<NBMenu>();
            Instance = m;
            m.m_Panel = new GameObject("Panel").transform;
            m.m_Panel.SetParent(g.transform, false);
            NBMat.Prim(PrimitiveType.Quad, m.m_Panel, new Vector3(0, 0, 0.01f), new Vector3(W + 0.04f, H + 0.04f, 1), NBMat.Glow(new Color(0.55f, 0.3f, 1f, 0.9f)));
            NBMat.Prim(PrimitiveType.Quad, m.m_Panel, new Vector3(0, 0, 0.005f), new Vector3(W, H, 1), NBMat.Glow(new Color(0.06f, 0.05f, 0.1f, 0.96f)));
            m.m_Tabs = new GameObject("Tabs").transform; m.m_Tabs.SetParent(m.m_Panel, false);
            m.m_Content = new GameObject("Content").transform; m.m_Content.SetParent(m.m_Panel, false);
            m.m_ViewFighter = NBSettings.Fighter;
            return m;
        }

        // ------------------------------------------------------------ placement
        public void ShowLobby(Tab tab, string status = null)
        {
            m_Panel.position = NBArena.MenuPos;
            m_Panel.rotation = Quaternion.identity;
            m_Panel.gameObject.SetActive(true);
            m_Kb = false;
            if (status != null) NBNet.Status = status;
            Open(tab);
        }

        public void ShowInMatch()
        {
            var rig = NBRig.Instance;
            var f = rig.Forward;
            m_Panel.position = rig.Head.position + f * 1.1f + Vector3.down * 0.15f;
            m_Panel.rotation = Quaternion.LookRotation(f);
            m_Panel.gameObject.SetActive(true);
            m_Kb = false;
            Open(Tab.Match);
        }

        public void Hide() => m_Panel.gameObject.SetActive(false);

        public void Open(Tab tab)
        {
            m_Tab = tab;
            if (tab == Tab.Fighters) m_ViewFighter = NBSettings.Fighter;
            Rebuild();
        }

        void Update()
        {
            if (!Visible) return;
            foreach (var a in m_Live) a();
        }

        // ------------------------------------------------------------ building helpers
        void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        NBButton Btn(string text, float x, float y, float w, float h, Action a, float ts = 0.035f, Color? c = null)
            => NBButton.Create(m_Content, text, new Vector3(x, y, -0.006f), new Vector2(w, h), a, ts, c);

        TextMesh Txt(string s, float x, float y, float size, Color? c = null, TextAnchor anchor = TextAnchor.MiddleCenter)
            => NBMat.Text(m_Content, s, new Vector3(x, y, -0.006f), size, c ?? Color.white, anchor);

        void Live(TextMesh t, Func<string> f) => m_Live.Add(() => { if (t != null) t.text = f(); });

        static string Wrap(string s, int width)
        {
            var words = s.Split(' ');
            var sb = new System.Text.StringBuilder();
            int line = 0;
            foreach (var w in words)
            {
                if (line + w.Length > width && line > 0) { sb.Append('\n'); line = 0; }
                else if (line > 0) { sb.Append(' '); line++; }
                sb.Append(w); line += w.Length;
            }
            return sb.ToString();
        }

        static readonly Color Purple = new Color(0.78f, 0.6f, 1f);
        static readonly Color Dim = new Color(0.75f, 0.75f, 0.85f);

        void Rebuild()
        {
            m_Live.Clear();
            Clear(m_Tabs); Clear(m_Content);
            bool inMatch = NBGame.Instance != null && NBGame.Instance.InMatch;

            var tabs = inMatch
                ? new[] { (Tab.Match, "MATCH"), (Tab.Fighters, "FIGHTERS"), (Tab.Controls, "CONTROLS"), (Tab.Settings, "SETTINGS") }
                : new[] { (Tab.Nullbound, "NULLBOUND"), (Tab.Fighters, "FIGHTERS"), (Tab.Controls, "CONTROLS"), (Tab.Setup, "VR SETUP"), (Tab.Settings, "SETTINGS") };
            float tw = (W - 0.04f) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                var (tab, label) = tabs[i];
                var b = NBButton.Create(m_Tabs, label, new Vector3(-W / 2 + 0.02f + tw * (i + 0.5f), H / 2 - 0.06f, -0.006f), new Vector2(tw - 0.012f, 0.08f), () => { m_Kb = false; Open(tab); }, 0.034f);
                b.SetSelected(tab == m_Tab && !m_Kb);
            }

            if (m_Kb) { BuildKeyboard(); return; }
            switch (m_Tab)
            {
                case Tab.Nullbound: BuildPlay(); break;
                case Tab.Fighters: BuildFighters(); break;
                case Tab.Controls: BuildControls(); break;
                case Tab.Setup: BuildSetup(); break;
                case Tab.Settings: BuildSettings(); break;
                case Tab.Match: BuildMatch(); break;
            }
        }

        // ------------------------------------------------------------ NULLBOUND (play) tab
        void BuildPlay()
        {
            Txt("NULLBOUND", 0, 0.3f, 0.13f, Purple);
            Txt("VR BATTLEGROUNDS  -  THROW REAL PUNCHES", 0, 0.215f, 0.032f, Dim);

            Txt("PLAYER", -0.42f, 0.12f, 0.03f, Dim);
            Live(Txt("", -0.42f, 0.065f, 0.055f), () => NBSettings.PlayerName);
            Btn("CHANGE NAME", -0.42f, -0.005f, 0.5f, 0.075f, () => OpenKeyboard("YOUR NAME", NBSettings.PlayerName, Letters + Digits + "_", 12, s => NBSettings.PlayerName = s));
            Txt("FIGHTER", -0.42f, -0.085f, 0.03f, Dim);
            var fname = Txt("", -0.42f, -0.14f, 0.055f);
            Live(fname, () => { var f = NBFighters.Get(NBSettings.Fighter); fname.color = f.main; return f.name; });
            Btn("PICK FIGHTER", -0.42f, -0.21f, 0.5f, 0.075f, () => Open(Tab.Fighters));

            var solo = Btn("PLAY SOLO VS BOTS", 0.3f, 0.12f, 0.62f, 0.085f, () => NBNet.Instance.StartSolo(), 0.038f, NBButton.AccentColor);
            var host = Btn("HOST ONLINE MATCH", 0.3f, 0.025f, 0.62f, 0.085f, () => NBNet.Instance.HostOnline());
            var join = Btn("JOIN WITH CODE", 0.3f, -0.07f, 0.62f, 0.085f,
                () => OpenKeyboard("ENTER ROOM CODE", NBSettings.JoinCode, Letters + Digits, 6, s => { NBSettings.JoinCode = s; NBNet.Instance.JoinOnline(s); }));
            var lanHost = Btn("LAN HOST", 0.145f, -0.165f, 0.305f, 0.08f, () => NBNet.Instance.HostLan());
            var lanJoin = Btn("LAN JOIN", 0.455f, -0.165f, 0.305f, 0.08f,
                () => OpenKeyboard("HOST'S LAN IP", NBSettings.LanIp, Digits + ".", 15, s => { NBSettings.LanIp = s; NBNet.Instance.JoinLan(s); }));
            Live(Txt("", 0.12f, -0.255f, 0.034f), () => "BOTS: " + NBSettings.Bots);
            Btn("-", 0.33f, -0.255f, 0.08f, 0.07f, () => NBSettings.Bots--, 0.05f);
            Btn("+", 0.43f, -0.255f, 0.08f, 0.07f, () => NBSettings.Bots++, 0.05f);

            var status = Txt("", 0, -0.37f, 0.034f, new Color(1f, 0.9f, 0.5f));
            Live(status, () => Wrap(NBNet.Status, 70));
            m_Live.Add(() =>
            {
                bool busy = NBNet.Instance != null && NBNet.Instance.Busy;
                foreach (var b in new[] { solo, host, join, lanHost, lanJoin }) b.Interactable = !busy;
            });
        }

        // ------------------------------------------------------------ FIGHTERS tab
        void BuildFighters()
        {
            for (int i = 0; i < NBFighters.All.Length; i++)
            {
                int id = i;
                var f = NBFighters.All[i];
                var b = Btn(f.name, -0.56f, 0.28f - i * 0.11f, 0.32f, 0.095f, () => { m_ViewFighter = id; Rebuild(); }, 0.04f);
                b.Label.color = f.main;
                b.SetSelected(i == m_ViewFighter);
                if (i == NBSettings.Fighter) Txt("*", -0.73f, 0.28f - i * 0.11f, 0.05f, Color.yellow);
            }
            var d = NBFighters.Get(m_ViewFighter);
            float x = -0.36f;
            Txt(d.name, x, 0.3f, 0.08f, d.main, TextAnchor.MiddleLeft);
            Txt(d.title.ToUpperInvariant(), x, 0.235f, 0.032f, Dim, TextAnchor.MiddleLeft);
            Txt(Wrap(d.blurb, 58), x, 0.17f, 0.028f, Color.white, TextAnchor.UpperLeft);
            string[] keys = { "X", "Y", "L-TRIG", "R-TRIG" };
            if (NBSettings.LeftHanded) keys = new[] { "A", "B", "R-TRIG", "L-TRIG" };
            for (int i = 0; i < 4; i++)
            {
                var m = d.moves[i];
                Txt("[" + keys[i] + "] " + m.name, x, 0.06f - i * 0.075f, 0.034f, Color.white, TextAnchor.MiddleLeft);
                Txt(m.hint + "  (" + m.cooldown + "s)", x + 0.03f, 0.03f - i * 0.075f, 0.026f, Dim, TextAnchor.MiddleLeft);
            }
            Txt("[BOTH STICKS] AWAKEN: " + d.ult.name, x, -0.25f, 0.034f, Purple, TextAnchor.MiddleLeft);
            Txt(d.ult.hint, x + 0.03f, -0.28f, 0.026f, Dim, TextAnchor.MiddleLeft);
            bool chosen = NBSettings.Fighter == m_ViewFighter;
            var sel = Btn(chosen ? "SELECTED" : "SELECT " + d.name, 0.45f, -0.38f, 0.5f, 0.09f, () =>
            {
                NBSettings.Fighter = m_ViewFighter;
                var local = NBPlayer.Local;
                if (local != null && local.IsSpawned)
                {
                    local.FighterId.Value = m_ViewFighter;
                    NBLocalFighter.Instance?.ResetCooldowns();
                }
                Rebuild();
            }, 0.038f, chosen ? (Color?)NBButton.DefaultColor : NBButton.AccentColor);
            sel.Interactable = !chosen;
        }

        // ------------------------------------------------------------ CONTROLS tab
        void BuildControls()
        {
            bool l = NBSettings.LeftHanded;
            string main = l ? "LEFT" : "RIGHT", off = l ? "RIGHT" : "LEFT";
            string ab = l ? "X / Y" : "A / B", xy = l ? "A / B" : "X / Y";
            var lines = new[]
            {
                ("PUNCH", "Swing your real fists into enemies. Faster = harder."),
                ("COMBO", "4 punches in a row = FINISHER (big knockback, breaks block)."),
                ("UPPERCUT", "Punch upward to launch. DOWNSLAM: punch down in the air."),
                ("MOVES", xy + " = moves 1 & 2,  " + off + " TRIGGER = 3,  " + main + " TRIGGER = 4."),
                ("BLOCK", "Hold BOTH GRIPS. Finishers and some moves break guard."),
                ("JUMP / DASH", ab + ":  first = jump, second = dash (aim with your move stick)."),
                ("EVASIVE", "Press DASH while you're being comboed to escape (14s cooldown)."),
                ("AWAKEN", "Fill the purple bar by fighting, then CLICK BOTH STICKS."),
                ("MOVE / TURN", off + " stick moves, " + main + " stick turns. MENU button = menu."),
                ("KEYBOARD", "WASD, mouse look, LMB/RMB punch, 1-4 moves, SHIFT dash, F block, G awaken, ESC menu."),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                float y = 0.31f - i * 0.072f;
                Txt(lines[i].Item1, -0.7f, y, 0.034f, Purple, TextAnchor.MiddleLeft);
                Txt(lines[i].Item2, -0.38f, y, 0.029f, Color.white, TextAnchor.MiddleLeft);
            }
        }

        // ------------------------------------------------------------ VR SETUP tab
        void BuildSetup()
        {
            const int steps = 6;
            Txt("VR SETUP  -  STEP " + (m_SetupStep + 1) + " / " + steps, 0, 0.32f, 0.045f, Purple);
            switch (m_SetupStep)
            {
                case 0:
                    Txt("WELCOME TO NULLBOUND", 0, 0.22f, 0.07f);
                    Txt(Wrap("This is a full-body VR fighting game: you punch for real. Before you start, make sure your Guardian / boundary is set up and you have room to swing your arms in every direction. Roomscale (2x2 m) is best, Stationary works too. Move pets, people and furniture out of the way!", 70), 0, 0.12f, 0.032f, Color.white, TextAnchor.UpperCenter);
                    Txt(Wrap("Tip: wear the wrist straps on your controllers.", 70), 0, -0.14f, 0.032f, new Color(1f, 0.9f, 0.5f));
                    break;
                case 1:
                    Txt("HEIGHT", 0, 0.22f, 0.07f);
                    Txt(Wrap("Stand (or sit, if you play seated) the way you will play, look straight ahead and press CALIBRATE.", 70), 0, 0.15f, 0.032f, Color.white, TextAnchor.UpperCenter);
                    Live(Txt("", 0, 0.04f, 0.04f, new Color(1f, 0.9f, 0.5f)), () => "EYE HEIGHT: " + (NBInput.HeadPos.y + NBSettings.HeightOffset).ToString("0.00") + " m   (OFFSET " + NBSettings.HeightOffset.ToString("+0.00;-0.00") + ")");
                    var seat = Btn("", -0.4f, -0.07f, 0.4f, 0.08f, null);
                    seat.onClick = () => { NBSettings.Seated = !NBSettings.Seated; seat.SetText(NBSettings.Seated ? "MODE: SEATED" : "MODE: STANDING"); };
                    seat.SetText(NBSettings.Seated ? "MODE: SEATED" : "MODE: STANDING");
                    Btn("CALIBRATE", 0.1f, -0.07f, 0.4f, 0.08f, Calibrate, 0.038f, NBButton.AccentColor);
                    Btn("FLOOR -5cm", -0.25f, -0.18f, 0.3f, 0.07f, () => Nudge(-0.05f));
                    Btn("FLOOR +5cm", 0.1f, -0.18f, 0.3f, 0.07f, () => Nudge(0.05f));
                    break;
                case 2:
                    Txt("MAIN HAND", 0, 0.22f, 0.07f);
                    Txt(Wrap("Your main hand gets JUMP / DASH and the turning stick. Your other hand moves you around.", 70), 0, 0.15f, 0.032f, Color.white, TextAnchor.UpperCenter);
                    var r = Btn("RIGHT HANDED", -0.25f, 0f, 0.42f, 0.1f, () => { NBSettings.LeftHanded = false; Rebuild(); }, 0.04f);
                    var lft = Btn("LEFT HANDED", 0.25f, 0f, 0.42f, 0.1f, () => { NBSettings.LeftHanded = true; Rebuild(); }, 0.04f);
                    r.SetSelected(!NBSettings.LeftHanded); lft.SetSelected(NBSettings.LeftHanded);
                    break;
                case 3:
                    Txt("COMFORT", 0, 0.22f, 0.07f);
                    Txt(Wrap("New to VR? Keep SNAP turning and the vignette on. You can change these later in SETTINGS.", 70), 0, 0.15f, 0.032f, Color.white, TextAnchor.UpperCenter);
                    ComfortRows(0.04f);
                    break;
                case 4:
                    Txt("YOUR NAME", 0, 0.22f, 0.07f);
                    Txt("This shows above your head in matches.", 0, 0.15f, 0.032f);
                    Live(Txt("", 0, 0.04f, 0.08f, Purple), () => NBSettings.PlayerName);
                    Btn("CHANGE NAME", 0, -0.08f, 0.5f, 0.09f, () => OpenKeyboard("YOUR NAME", NBSettings.PlayerName, Letters + Digits + "_", 12, s => NBSettings.PlayerName = s), 0.04f);
                    break;
                case 5:
                    Txt("TEST PUNCH", 0, 0.22f, 0.07f);
                    Txt(Wrap("Turn right: there's a punching bag next to you. Punch it! Fast, snappy punches hit hardest. 3+ m/s is a good punch.", 70), 0, 0.15f, 0.032f, Color.white, TextAnchor.UpperCenter);
                    Live(Txt("", 0, 0.0f, 0.06f, new Color(1f, 0.9f, 0.5f)), () => NBPunchBag.Hits == 0 ? "WAITING FOR A PUNCH..." : "LAST PUNCH: " + NBPunchBag.LastSpeed.ToString("0.0") + " m/s");
                    break;
            }
            if (m_SetupStep > 0) Btn("BACK", -0.5f, -0.38f, 0.3f, 0.085f, () => { m_SetupStep--; Rebuild(); });
            if (m_SetupStep < steps - 1) Btn("NEXT", 0.5f, -0.38f, 0.3f, 0.085f, () => { m_SetupStep++; Rebuild(); }, 0.04f, NBButton.AccentColor);
            else Btn("FINISH", 0.5f, -0.38f, 0.3f, 0.085f, () => { NBSettings.SetupDone = true; m_SetupStep = 0; Open(Tab.Nullbound); }, 0.04f, NBButton.AccentColor);
            if (!NBSettings.SetupDone) Btn("SKIP", 0f, -0.38f, 0.22f, 0.07f, () => { NBSettings.SetupDone = true; Open(Tab.Nullbound); }, 0.03f);
        }

        void Calibrate()
        {
            float eye = NBInput.HeadPos.y;
            NBSettings.HeightOffset = NBSettings.Seated ? 1.6f - eye : (eye < 0.9f ? 1.6f - eye : 0f);
            NBRig.Instance?.ApplyHeight();
            NBFX.Burst(NBRig.Instance.Head.position + NBRig.Instance.Forward * 0.8f, Purple, 0.6f, 8);
        }

        void Nudge(float d)
        {
            NBSettings.HeightOffset += d;
            NBRig.Instance?.ApplyHeight();
        }

        void ComfortRows(float top)
        {
            var turn = Btn("", -0.2f, top, 0.5f, 0.075f, null);
            turn.onClick = () => { NBSettings.SnapTurn = !NBSettings.SnapTurn; Rebuild(); };
            turn.SetText(NBSettings.SnapTurn ? "TURNING: SNAP" : "TURNING: SMOOTH");
            if (NBSettings.SnapTurn)
            {
                Live(Txt("", 0.25f, top, 0.032f), () => NBSettings.SnapAngle + " DEG");
                Btn("-", 0.4f, top, 0.07f, 0.07f, () => NBSettings.SnapAngle -= 15, 0.05f);
                Btn("+", 0.49f, top, 0.07f, 0.07f, () => NBSettings.SnapAngle += 15, 0.05f);
            }
            else
            {
                Live(Txt("", 0.25f, top, 0.032f), () => Mathf.RoundToInt(NBSettings.TurnSpeed) + " DEG/S");
                Btn("-", 0.4f, top, 0.07f, 0.07f, () => NBSettings.TurnSpeed -= 20, 0.05f);
                Btn("+", 0.49f, top, 0.07f, 0.07f, () => NBSettings.TurnSpeed += 20, 0.05f);
            }
            var vig = Btn("", -0.2f, top - 0.1f, 0.5f, 0.075f, null);
            vig.onClick = () => { NBSettings.Vignette = !NBSettings.Vignette; vig.SetText("COMFORT VIGNETTE: " + (NBSettings.Vignette ? "ON" : "OFF")); };
            vig.SetText("COMFORT VIGNETTE: " + (NBSettings.Vignette ? "ON" : "OFF"));
            var kb = Btn("", -0.2f, top - 0.2f, 0.5f, 0.075f, null);
            kb.onClick = () => { NBSettings.ReducedKnockback = !NBSettings.ReducedKnockback; kb.SetText("REDUCED KNOCKBACK: " + (NBSettings.ReducedKnockback ? "ON" : "OFF")); };
            kb.SetText("REDUCED KNOCKBACK: " + (NBSettings.ReducedKnockback ? "ON" : "OFF"));
        }

        // ------------------------------------------------------------ SETTINGS tab
        void BuildSettings()
        {
            Txt("SETTINGS", 0, 0.32f, 0.05f, Purple);
            ComfortRows(0.22f);
            var hand = Btn("", -0.2f, -0.08f, 0.5f, 0.075f, null);
            hand.onClick = () => { NBSettings.LeftHanded = !NBSettings.LeftHanded; hand.SetText("MAIN HAND: " + (NBSettings.LeftHanded ? "LEFT" : "RIGHT")); };
            hand.SetText("MAIN HAND: " + (NBSettings.LeftHanded ? "LEFT" : "RIGHT"));

            Live(Txt("", -0.2f, -0.18f, 0.032f), () => "HEIGHT OFFSET " + NBSettings.HeightOffset.ToString("+0.00;-0.00") + " m");
            Btn("-", 0.13f, -0.18f, 0.07f, 0.07f, () => Nudge(-0.05f), 0.05f);
            Btn("+", 0.22f, -0.18f, 0.07f, 0.07f, () => Nudge(0.05f), 0.05f);
            Live(Txt("", -0.2f, -0.27f, 0.032f), () => "VOLUME " + Mathf.RoundToInt(NBSettings.Volume * 100) + "%");
            Btn("-", 0.13f, -0.27f, 0.07f, 0.07f, () => NBSettings.Volume -= 0.1f, 0.05f);
            Btn("+", 0.22f, -0.27f, 0.07f, 0.07f, () => NBSettings.Volume += 0.1f, 0.05f);

            bool inMatch = NBGame.Instance != null && NBGame.Instance.InMatch;
            if (!inMatch) Btn("RE-RUN VR SETUP", 0, -0.38f, 0.5f, 0.08f, () => { m_SetupStep = 0; Open(Tab.Setup); });
        }

        // ------------------------------------------------------------ MATCH tab (in-game menu)
        void BuildMatch()
        {
            var net = NBNet.Instance;
            Live(Txt("", 0, 0.32f, 0.05f, Purple), () => net != null ? net.RoomLabel : "");
            Txt("SCOREBOARD", -0.35f, 0.23f, 0.035f, Dim);
            var board = Txt("", -0.7f, 0.19f, 0.032f, Color.white, TextAnchor.UpperLeft);
            Live(board, () =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (var p in NBPlayer.All.Where(p => p != null && p.IsSpawned).OrderByDescending(p => p.Kills.Value).Take(10))
                    sb.Append((p == NBPlayer.Local ? "> " : "  ") + p.Name.PadRight(15) + p.Def.name.PadRight(9) + "KO " + p.Kills.Value + "  DOWN " + p.Deaths.Value + "\n");
                return sb.ToString();
            });
            Btn("RESUME", 0.42f, 0.18f, 0.45f, 0.09f, Hide, 0.04f, NBButton.AccentColor);
            Btn("PICK FIGHTER", 0.42f, 0.07f, 0.45f, 0.085f, () => Open(Tab.Fighters));
            if (net != null && net.IsHost)
            {
                Btn("+ BOT", 0.31f, -0.04f, 0.22f, 0.08f, () => net.AddBot());
                Btn("- BOT", 0.54f, -0.04f, 0.22f, 0.08f, () => net.RemoveBot());
            }
            Btn("LEAVE MATCH", 0.42f, -0.36f, 0.45f, 0.085f, () => NBNet.Instance.Leave(), 0.04f, new Color(0.6f, 0.15f, 0.2f, 0.95f));
        }

        // ------------------------------------------------------------ keyboard
        const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string Digits = "0123456789";

        public void OpenKeyboard(string title, string initial, string chars, int max, Action<string> done)
        {
            m_Kb = true; m_KbTitle = title; m_KbText = initial ?? ""; m_KbChars = chars; m_KbMax = max; m_KbDone = done;
            if (m_KbText.Length > max) m_KbText = m_KbText.Substring(0, max);
            Rebuild();
        }

        void BuildKeyboard()
        {
            Txt(m_KbTitle, 0, 0.31f, 0.045f, Purple);
            NBMat.Prim(PrimitiveType.Quad, m_Content, new Vector3(0, 0.215f, -0.004f), new Vector3(0.9f, 0.1f, 1), NBMat.Glow(new Color(0, 0, 0, 0.8f)));
            Live(Txt("", 0, 0.215f, 0.06f), () => m_KbText + ((Time.time % 1f) < 0.5f ? "_" : " "));
            const int perRow = 10;
            float kw = 0.12f, kh = 0.085f;
            for (int i = 0; i < m_KbChars.Length; i++)
            {
                char ch = m_KbChars[i];
                int row = i / perRow, col = i % perRow;
                Btn(ch.ToString(), -kw * (perRow - 1) / 2f + col * kw, 0.09f - row * (kh + 0.012f), kw - 0.012f, kh, () =>
                {
                    if (m_KbText.Length < m_KbMax) m_KbText += ch;
                }, 0.045f);
            }
            Btn("DEL", -0.45f, -0.38f, 0.2f, 0.08f, () => { if (m_KbText.Length > 0) m_KbText = m_KbText.Substring(0, m_KbText.Length - 1); });
            Btn("CLEAR", -0.2f, -0.38f, 0.2f, 0.08f, () => m_KbText = "");
            Btn("CANCEL", 0.12f, -0.38f, 0.24f, 0.08f, () => { m_Kb = false; Rebuild(); });
            Btn("OK", 0.42f, -0.38f, 0.24f, 0.08f, () =>
            {
                m_Kb = false;
                var t = m_KbText;
                Rebuild();
                m_KbDone?.Invoke(t);
            }, 0.045f, NBButton.AccentColor);
        }
    }
}
