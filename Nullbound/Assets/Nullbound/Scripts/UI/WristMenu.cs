using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// The menu: a panel floating above your left controller. Press the MENU button (three lines, left controller)
    /// to open or close it. Poke it with your right hand, or point at it and pull the trigger.
    /// Pages: PLAY (quick match, training vs bots, private lobbies, join a code, bots), FIGHTER (pick who you
    /// fight as) and SETTINGS (turning, comfort, room scale, volume).
    /// Keyboard: Tab or Esc opens it, left click presses buttons.
    /// </summary>
    public class WristMenu : MonoBehaviour
    {
        static readonly Vector3 PanelOffset = new Vector3(0f, 0.2f, 0.05f);
        static readonly Vector3 PanelTilt = new Vector3(35f, 0f, 0f);
        static WristMenu instance;

        public static bool Open => instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;

        enum Page { Play, Fighter, Settings, Keypad }

        Transform panel;
        readonly Transform[] pages = new Transform[4];
        readonly PokeButton[] tabs = new PokeButton[3];
        Page page;
        float nextRefresh;

        TextMeshPro status, fighterInfo, keypadText, nameLabel;
        PokeButton lobbyButton, addBotButton, removeBotsButton, difficultyButton, turnButton, vignetteButton, roomScaleButton;
        PokeButton[] fighterButtons;
        TextMeshPro sfxBar, musicBar;
        string typed = "";

        static readonly Color TabOn = new Color(0.4f, 0.85f, 1f), TabOff = new Color(0.35f, 0.38f, 0.45f);

        void Start()
        {
            instance = this;
            panel = new GameObject("WristPanel").transform;
            panel.SetParent(transform, false);
            panel.localPosition = PanelOffset;
            panel.localRotation = Quaternion.Euler(PanelTilt);

            NBUI.Box(panel, "Backing", new Vector3(0f, -0.03f, 0.012f), new Vector3(0.26f, 0.25f, 0.01f), new Color(0.07f, 0.07f, 0.1f));
            NBUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, 0.095f, 0.01f), new Vector3(0.26f, 0.003f, 0.012f), NBUI.Glow(new Color(1f, 0.35f, 0.2f)));
            string[] names = { "Play", "Fighter", "Settings" };
            var tabSize = new Vector3(0.08f, 0.026f, 0.02f);
            for (int i = 0; i < 3; i++)
            {
                var p = (Page)i;
                tabs[i] = PokeButton.Create(panel, "Tab" + names[i], names[i], new Vector3((i - 1f) * 0.084f, 0.075f, 0f), tabSize, TabOff, () => Show(p), 0.075f);
            }
            for (int i = 0; i < 4; i++)
            {
                pages[i] = new GameObject(((Page)i) + "Page").transform;
                pages[i].SetParent(panel, false);
            }
            BuildPlay(pages[0]);
            BuildFighter(pages[1]);
            BuildSettings(pages[2]);
            BuildKeypad(pages[3]);
            Show(Page.Play);
            panel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Show(Page p)
        {
            page = p;
            for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == (int)p);
            for (int i = 0; i < tabs.Length; i++) tabs[i].SetColor(i == (int)p || (p == Page.Keypad && i == 0) ? TabOn : TabOff);
            nextRefresh = 0f;
        }

        static SessionManager S => SessionManager.Instance;

        // ---------------- pages

        void BuildPlay(Transform root)
        {
            status = NBUI.Label(root, "", new Vector3(0f, 0.045f, 0f), 0.075f, Color.white, new Vector2(0.24f, 0.035f));
            var half = new Vector3(0.118f, 0.03f, 0.02f);
            PokeButton.Create(root, "Quick", "<b>Quick Match</b>", new Vector3(-0.061f, 0.008f, 0f), half, new Color(1f, 0.55f, 0.3f),
                () => S?.JoinPublic(), 0.085f).cooldown = 2f;
            PokeButton.Create(root, "Training", "<b>Training</b> vs bots", new Vector3(0.061f, 0.008f, 0f), half, new Color(0.5f, 1f, 0.6f),
                () => S?.StartTraining(), 0.08f).cooldown = 2f;
            lobbyButton = PokeButton.Create(root, "Lobby", "Create Private Lobby", new Vector3(-0.061f, -0.027f, 0f), half, new Color(1f, 0.6f, 0.9f), ToggleLobby, 0.065f);
            lobbyButton.cooldown = 2f;
            PokeButton.Create(root, "JoinCode", "Join a Code", new Vector3(0.061f, -0.027f, 0f), half, new Color(0.6f, 0.75f, 1f), () => { typed = ""; Show(Page.Keypad); }, 0.075f);
            var third = new Vector3(0.077f, 0.026f, 0.02f);
            addBotButton = PokeButton.Create(root, "AddBot", "+ Add Bot", new Vector3(-0.081f, -0.06f, 0f), third, new Color(1f, 0.85f, 0.4f), () => S?.AddBot(), 0.07f);
            removeBotsButton = PokeButton.Create(root, "RemoveBots", "Remove Bots", new Vector3(0f, -0.06f, 0f), third, new Color(0.85f, 0.7f, 0.5f), () => S?.RemoveBots(), 0.06f);
            difficultyButton = PokeButton.Create(root, "Difficulty", "", new Vector3(0.081f, -0.06f, 0f), third, new Color(0.9f, 0.6f, 0.4f), CycleDifficulty, 0.06f);
            PokeButton.Create(root, "Recenter", "Recenter view", new Vector3(0f, -0.093f, 0f), new Vector3(0.24f, 0.024f, 0.02f), new Color(0.6f, 0.8f, 1f),
                () => VRRig.Instance?.Recenter(), 0.07f);
            NBUI.Label(root, "Punch = swing your fist   Block = fists up   A jump   B dash   X special", new Vector3(0f, -0.12f, 0f), 0.05f,
                new Color(1f, 1f, 1f, 0.7f), new Vector2(0.25f, 0.015f));
        }

        void BuildFighter(Transform root)
        {
            fighterButtons = new PokeButton[FighterInfo.Count];
            for (int i = 0; i < FighterInfo.Count; i++)
            {
                var kind = (FighterKind)i;
                var st = FighterInfo.Get(kind);
                fighterButtons[i] = PokeButton.Create(root, st.name, "<b>" + st.name + "</b>", new Vector3((i - (FighterInfo.Count - 1) * 0.5f) * 0.122f, 0.04f, 0f),
                    new Vector3(0.118f, 0.036f, 0.02f), st.main, () => Pick(kind), 0.09f);
            }
            fighterInfo = NBUI.Label(root, "", new Vector3(0f, -0.03f, 0f), 0.06f, Color.white, new Vector2(0.24f, 0.09f), TextAlignmentOptions.Top);
            nameLabel = NBUI.Label(root, "", new Vector3(-0.04f, -0.11f, 0f), 0.065f, new Color(1f, 0.85f, 0.3f), new Vector2(0.16f, 0.02f));
            PokeButton.Create(root, "Rename", "New name", new Vector3(0.085f, -0.11f, 0f), new Vector3(0.07f, 0.022f, 0.02f), new Color(0.7f, 0.7f, 0.8f),
                () => { PlayerName.Reroll(); if (Fighter.Local != null) Fighter.Local.FighterName = PlayerName.Get(); nextRefresh = 0f; }, 0.06f);
        }

        void BuildSettings(Transform root)
        {
            var wide = new Vector3(0.24f, 0.026f, 0.02f);
            turnButton = PokeButton.Create(root, "Turn", "", new Vector3(0f, 0.045f, 0f), wide, new Color(0.7f, 0.75f, 1f), () => { GameSettings.Turn = (TurnStyle)(((int)GameSettings.Turn + 1) % 3); nextRefresh = 0f; }, 0.075f);
            vignetteButton = PokeButton.Create(root, "Vignette", "", new Vector3(0f, 0.015f, 0f), wide, new Color(0.7f, 0.9f, 0.8f), () => { GameSettings.Vignette = !GameSettings.Vignette; nextRefresh = 0f; }, 0.065f);
            roomScaleButton = PokeButton.Create(root, "RoomScale", "", new Vector3(0f, -0.015f, 0f), wide, new Color(0.8f, 0.8f, 1f), () => { GameSettings.RoomScale = !GameSettings.RoomScale; nextRefresh = 0f; }, 0.065f);
            sfxBar = VolumeRow(root, "Sound", -0.05f, d => { GameSettings.SfxVolume = Mathf.Round((GameSettings.SfxVolume + d) * 10f) / 10f; AudioManager.RefreshVolumes(); nextRefresh = 0f; });
            musicBar = VolumeRow(root, "Music", -0.085f, d => { GameSettings.MusicVolume = Mathf.Round((GameSettings.MusicVolume + d) * 10f) / 10f; AudioManager.RefreshVolumes(); nextRefresh = 0f; });
        }

        static TextMeshPro VolumeRow(Transform root, string title, float y, System.Action<float> change)
        {
            var small = new Vector3(0.035f, 0.026f, 0.02f);
            NBUI.Label(root, title, new Vector3(-0.09f, y, 0f), 0.075f, Color.white, new Vector2(0.06f, 0.02f));
            PokeButton.Create(root, title + "Down", "-", new Vector3(-0.04f, y, 0f), small, new Color(1f, 0.6f, 0.5f), () => change(-0.1f), 0.15f);
            var bar = NBUI.Label(root, "", new Vector3(0.035f, y, 0f), 0.07f, new Color(0.6f, 1f, 0.7f), new Vector2(0.1f, 0.02f));
            PokeButton.Create(root, title + "Up", "+", new Vector3(0.105f, y, 0f), small, new Color(0.5f, 1f, 0.6f), () => change(0.1f), 0.15f);
            return bar;
        }

        void BuildKeypad(Transform root)
        {
            keypadText = NBUI.Label(root, "", new Vector3(0f, 0.045f, 0f), 0.1f, new Color(0.4f, 1f, 0.5f), new Vector2(0.24f, 0.025f));
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "DEL", "0", "JOIN" };
            var size = new Vector3(0.07f, 0.024f, 0.02f);
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                var pos = new Vector3(((i % 3) - 1) * 0.078f, 0.015f - (i / 3) * 0.028f, 0f);
                var color = k == "JOIN" ? new Color(0.4f, 1f, 0.5f) : k == "DEL" ? new Color(1f, 0.6f, 0.5f) : new Color(0.85f, 0.85f, 0.9f);
                PokeButton.Create(root, "Key" + k, k, pos, size, color, () => Key(k), k.Length > 1 ? 0.07f : 0.1f);
            }
            PokeButton.Create(root, "Back", "Back", new Vector3(0f, -0.105f, 0f), new Vector3(0.24f, 0.022f, 0.02f), TabOff, () => Show(Page.Play), 0.07f);
        }

        // ---------------- actions

        void Key(string k)
        {
            if (k == "DEL") { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); }
            else if (k == "JOIN")
            {
                if (typed.Length == 6) { S?.JoinCode(typed); typed = ""; Show(Page.Play); }
                else AudioManager.Play(AudioManager.Sfx.Error);
            }
            else if (typed.Length < 6) typed += k;
            nextRefresh = 0f;
        }

        void ToggleLobby()
        {
            if (S == null || S.Busy) return;
            if (S.IsPrivate) S.JoinPublic();
            else S.CreatePrivateLobby();
        }

        void CycleDifficulty()
        {
            BotBrain.Difficulty = (BotBrain.Difficulty + 1) % 3;
            nextRefresh = 0f;
        }

        void Pick(FighterKind kind)
        {
            GameSettings.Fighter = kind;
            if (Fighter.Local != null) Fighter.Local.SwitchKind(kind);
            AudioManager.Play(AudioManager.Sfx.Ready);
            nextRefresh = 0f;
        }

        static string Bar(float v)
        {
            int n = Mathf.RoundToInt(v * 10f);
            return new string('|', n) + "<color=#444444>" + new string('|', 10 - n) + "</color>";
        }

        void Update()
        {
            if (panel == null) return;
            if (FightInput.MenuPressed)
            {
                bool open = !panel.gameObject.activeSelf;
                panel.gameObject.SetActive(open);
                AudioManager.Play(AudioManager.Sfx.Click, 0.8f, open ? 1.2f : 0.9f);
                if (open) Show(page == Page.Keypad ? Page.Play : page);
            }
            if (!panel.gameObject.activeSelf || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.25f;
            var s = S;

            switch (page)
            {
                case Page.Play:
                    if (s == null) break;
                    string transient = s.TransientStatus;
                    status.text = !string.IsNullOrEmpty(transient) ? transient : s.DescribeRoom();
                    lobbyButton.SetLabel(s.IsPrivate ? "Leave Lobby" : "Create Private Lobby");
                    bool bots = s.CanUseBots;
                    addBotButton.gameObject.SetActive(bots);
                    removeBotsButton.gameObject.SetActive(bots);
                    difficultyButton.gameObject.SetActive(bots);
                    difficultyButton.SetLabel("Bots: " + new[] { "Easy", "Normal", "Hard" }[BotBrain.Difficulty]);
                    break;
                case Page.Fighter:
                    var current = Fighter.Local != null ? Fighter.Local.Kind : GameSettings.Fighter;
                    for (int i = 0; i < fighterButtons.Length; i++)
                        fighterButtons[i].SetLabel(((int)current == i ? "> " : "") + "<b>" + FighterInfo.Get((FighterKind)i).name + "</b>");
                    var st = FighterInfo.Get(current);
                    fighterInfo.text = "<b>" + st.name + "</b>: " + st.style + "\n<color=#ffcf4a>" + st.special + "</color>: " + st.specialHow +
                                       "\n<color=#7CFF8A>+ " + st.strengths + "</color>\n<color=#ff8a7a>- " + st.weakness + "</color>" +
                                       "\n<size=80%>Health " + st.maxHealth + "   more fighters coming soon</size>";
                    nameLabel.text = "You: " + PlayerName.Get();
                    break;
                case Page.Settings:
                    turnButton.SetLabel("Turning: " + GameSettings.TurnName(GameSettings.Turn));
                    vignetteButton.SetLabel("Comfort vignette: " + (GameSettings.Vignette ? "ON" : "off"));
                    roomScaleButton.SetLabel("Room scale walking: " + (GameSettings.RoomScale ? "ON" : "off"));
                    sfxBar.text = Bar(GameSettings.SfxVolume);
                    musicBar.text = Bar(GameSettings.MusicVolume);
                    break;
                case Page.Keypad:
                    keypadText.text = "Code: " + typed.PadRight(6, '_');
                    break;
            }
        }
    }
}
