using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// The menu: a panel floating above your left controller. Press the MENU button (three lines, left controller)
    /// to open or close it. Poke it with your right hand, or point at it and pull the trigger.
    /// Your name and Player ID are always shown at the top.
    /// Pages: PLAY (quick match, training, private lobbies, join a code, bots, main menu), FIGHTER (switch between
    /// the fighters you own) and SETTINGS (turning, comfort, room scale, voice, volume, and the Mod Menu for mods).
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

        TextMeshPro status, fighterInfo, keypadText, idLabel;
        PokeButton lobbyButton, addBotButton, removeBotsButton, difficultyButton, turnButton, vignetteButton, roomScaleButton, micButton, modButton, bodyButton, playModButton, goreButton, ragdollCamButton;
        int browse = -1;
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

            NBUI.Box(panel, "Backing", new Vector3(0f, -0.045f, 0.012f), new Vector3(0.26f, 0.28f, 0.01f), new Color(0.07f, 0.07f, 0.1f));
            NBUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, 0.095f, 0.01f), new Vector3(0.26f, 0.003f, 0.012f), NBUI.Glow(new Color(1f, 0.35f, 0.2f)));
            // Who you are: give your Player ID to the game's owner to become a moderator.
            NBUI.Box(panel, "IdStrip", new Vector3(0f, 0.112f, 0.008f), new Vector3(0.26f, 0.028f, 0.004f), new Color(0.25f, 0.2f, 0.04f));
            idLabel = NBUI.Fit(NBUI.Label(panel, "", new Vector3(0f, 0.112f, -0.002f), 0.11f, new Color(1f, 0.86f, 0.25f), new Vector2(0.25f, 0.026f)), 0.05f);
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
            PokeButton.Create(root, "MainMenu", "<b>Main Menu</b>", new Vector3(-0.061f, -0.093f, 0f), new Vector3(0.118f, 0.024f, 0.02f), new Color(1f, 0.45f, 0.35f),
                () => S?.GoToMainMenu(), 0.075f).cooldown = 2f;
            PokeButton.Create(root, "Recenter", "Recenter view", new Vector3(0.061f, -0.093f, 0f), new Vector3(0.118f, 0.024f, 0.02f), new Color(0.6f, 0.8f, 1f),
                () => VRRig.Instance?.Recenter(), 0.07f);
            // Moderators: the mod menu right here on the first page (it's also under Settings).
            playModButton = PokeButton.Create(root, "ModMenuPlay", "<b>MOD MENU</b>", new Vector3(0f, -0.124f, 0f), new Vector3(0.24f, 0.024f, 0.02f), new Color(1f, 0.4f, 0.35f), ModMenu.Toggle, 0.075f);
            playModButton.cooldown = 0.8f;
            NBUI.Label(root, "Punch = swing   Block = fists up   A jump   B dash   X fly   R-stick super", new Vector3(0f, -0.15f, 0f), 0.05f,
                new Color(1f, 1f, 1f, 0.7f), new Vector2(0.25f, 0.015f));
        }

        void BuildFighter(Transform root)
        {
            var half = new Vector3(0.075f, 0.028f, 0.02f);
            PokeButton.Create(root, "Prev", "< Prev", new Vector3(-0.082f, 0.045f, 0f), half, new Color(0.6f, 0.7f, 0.85f), () => Browse(-1), 0.075f);
            PokeButton.Create(root, "Choose", "<b>Choose</b>", new Vector3(0f, 0.045f, 0f), half, new Color(0.5f, 1f, 0.6f), () => Pick((FighterKind)Mathf.Max(0, browse)), 0.08f);
            PokeButton.Create(root, "Next", "Next >", new Vector3(0.082f, 0.045f, 0f), half, new Color(0.6f, 0.7f, 0.85f), () => Browse(1), 0.075f);
            fighterInfo = NBUI.Label(root, "", new Vector3(0f, -0.035f, 0f), 0.058f, Color.white, new Vector2(0.24f, 0.12f), TextAlignmentOptions.Top);
        }

        /// <summary>Step through the fighters you own.</summary>
        void Browse(int dir)
        {
            if (browse < 0) browse = (int)(Fighter.Local != null ? Fighter.Local.Kind : GameSettings.Fighter);
            for (int n = 0; n < FighterInfo.Count; n++)
            {
                browse = (browse + dir + FighterInfo.Count) % FighterInfo.Count;
                if (Collection.IsUnlocked(browse)) break;
            }
            nextRefresh = 0f;
        }

        void BuildSettings(Transform root)
        {
            var wide = new Vector3(0.24f, 0.022f, 0.02f);
            var half = new Vector3(0.117f, 0.022f, 0.02f);
            bodyButton = PokeButton.Create(root, "Body", "", new Vector3(0.0615f, 0.052f, 0f), half, new Color(1f, 0.8f, 0.6f), () => { GameSettings.FullBody = !GameSettings.FullBody; nextRefresh = 0f; }, 0.06f);
            turnButton = PokeButton.Create(root, "Turn", "", new Vector3(-0.0615f, 0.052f, 0f), half, new Color(0.7f, 0.75f, 1f), () => { GameSettings.Turn = (TurnStyle)(((int)GameSettings.Turn + 1) % 3); nextRefresh = 0f; }, 0.07f);
            goreButton = PokeButton.Create(root, "Gore", "", new Vector3(0.0615f, 0.027f, 0f), half, new Color(1f, 0.45f, 0.5f), () => { GameSettings.Gore = !GameSettings.Gore; nextRefresh = 0f; }, 0.06f);
            vignetteButton = PokeButton.Create(root, "Vignette", "", new Vector3(-0.0615f, 0.027f, 0f), half, new Color(0.7f, 0.9f, 0.8f), () => { GameSettings.Vignette = !GameSettings.Vignette; nextRefresh = 0f; }, 0.06f);
            roomScaleButton = PokeButton.Create(root, "RoomScale", "", new Vector3(-0.0615f, 0.002f, 0f), half, new Color(0.8f, 0.8f, 1f), () => { GameSettings.RoomScale = !GameSettings.RoomScale; nextRefresh = 0f; }, 0.06f);
            ragdollCamButton = PokeButton.Create(root, "RagdollCam", "", new Vector3(0.0615f, 0.002f, 0f), half, new Color(1f, 0.7f, 0.4f), () => { GameSettings.RagdollCam = (RagdollCamera)(((int)GameSettings.RagdollCam + 1) % 3); nextRefresh = 0f; }, 0.06f);
            micButton = PokeButton.Create(root, "Mic", "", new Vector3(0f, -0.023f, 0f), wide, new Color(0.5f, 1f, 0.6f), () => { GameSettings.Mic = (MicMode)(((int)GameSettings.Mic + 1) % 3); nextRefresh = 0f; }, 0.06f);
            sfxBar = VolumeRow(root, "Sound", -0.052f, d => { GameSettings.SfxVolume = Mathf.Round((GameSettings.SfxVolume + d) * 10f) / 10f; AudioManager.RefreshVolumes(); nextRefresh = 0f; });
            musicBar = VolumeRow(root, "Music", -0.08f, d => { GameSettings.MusicVolume = Mathf.Round((GameSettings.MusicVolume + d) * 10f) / 10f; AudioManager.RefreshVolumes(); nextRefresh = 0f; });
            modButton = PokeButton.Create(root, "ModMenu", "<b>MOD MENU</b>", new Vector3(0f, -0.11f, 0f), wide, new Color(1f, 0.4f, 0.35f), ModMenu.Toggle, 0.075f);
            modButton.cooldown = 0.8f;
        }

        static TextMeshPro VolumeRow(Transform root, string title, float y, System.Action<float> change)
        {
            var small = new Vector3(0.035f, 0.022f, 0.02f);
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
            idLabel.text = "<b>" + PlayerProfile.Nickname + "</b>    PLAYER ID: <b>" + PlayerIdentity.PlayerId + "</b>" + (Moderation.IAmMod ? "   <color=#ff6a5a>MOD</color>" : "");
            playModButton.gameObject.SetActive(Moderation.IAmMod);

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
                    if (browse < 0) browse = (int)(Fighter.Local != null ? Fighter.Local.Kind : GameSettings.Fighter);
                    var current = Fighter.Local != null ? Fighter.Local.Kind : GameSettings.Fighter;
                    var st = FighterInfo.Get(browse);
                    string rc = ColorUtility.ToHtmlStringRGB(FighterInfo.RarityColor(st.rarity));
                    fighterInfo.text = "<b>" + st.name + "</b>  <color=#" + rc + ">" + FighterInfo.RarityName(st.rarity) + "</color>" +
                                       ((int)current == browse ? "  <color=#7CFF8A>(you)</color>" : "") + "\n" + st.style +
                                       "\n<color=#ffcf4a>" + st.special + "</color>: " + FighterInfo.SpecialHow(st.specialType) +
                                       "\n<color=#7CFF8A>+ " + st.strengths + "</color>  <color=#ff8a7a>- " + st.weakness + "</color>" +
                                       "\n<size=80%>Health " + st.maxHealth + "   " + FighterInfo.PowerText(st) + "   You own " + Collection.UnlockedCount + "/" + FighterInfo.Count + " (crates in the main menu)</size>";
                    break;
                case Page.Settings:
                    turnButton.SetLabel("Turn: " + GameSettings.TurnName(GameSettings.Turn));
                    vignetteButton.SetLabel("Vignette: " + (GameSettings.Vignette ? "ON" : "off"));
                    goreButton.SetLabel(GameSettings.Gore ? "Gore: <b>ON</b>" : "Gore: off (confetti)");
                    bodyButton.SetLabel("Body: " + (GameSettings.FullBody ? "ON" : "off"));
                    roomScaleButton.SetLabel("Room walk: " + (GameSettings.RoomScale ? "ON" : "off"));
                    ragdollCamButton.SetLabel("Ragdoll cam: " + GameSettings.RagdollCamName(GameSettings.RagdollCam));
                    var mic = GameSettings.Mic;
                    micButton.SetLabel(Moderation.Muted ? "Voice: muted by a moderator" : mic == MicMode.Open ? "Voice: open mic" : mic == MicMode.PushToTalk ? "Voice: push-to-talk (hold L stick)" : "Voice: muted");
                    modButton.gameObject.SetActive(Moderation.IAmMod);
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
