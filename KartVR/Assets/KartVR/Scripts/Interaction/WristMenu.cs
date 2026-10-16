using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The menu: a panel floating above your left controller. Press the MENU
    /// button (three lines, left controller) to open or close it; it starts
    /// closed. Poke it with your right hand, or point at it and pull the trigger. Four pages:
    /// Menu (recenter, respawn, hub, lobby, build mode), Sound (effects and music
    /// volume), Controls (turning style, steering wheel, list of controls) and Voice
    /// (open mic / push-to-talk / muted, voice volume, mic test).
    /// Keyboard: Tab or Esc opens it, left click presses buttons.
    /// </summary>
    public class WristMenu : MonoBehaviour
    {
        public Vector3 panelOffset = new Vector3(0f, 0.1f, 0.03f);
        public Vector3 panelTilt = new Vector3(35f, 0f, 0f);

        enum Page { Main, Sound, Controls, Voice }

        Transform panel;
        readonly Transform[] pages = new Transform[4];
        readonly PokeButton[] tabs = new PokeButton[4];
        Page page;
        TextMeshPro info, sfxBar, musicBar, engineBar;
        PokeButton lobbyButton, buildButton, turnButton, wheelButton, micButton, micTestButton;
        TextMeshPro voiceBar, micMeter;
        float nextRefresh;

        static readonly Color TabOn = new Color(0.4f, 0.85f, 1f), TabOff = new Color(0.35f, 0.38f, 0.45f);

        void Start()
        {
            panel = new GameObject("WristPanel").transform;
            panel.SetParent(transform, false);
            panel.localPosition = panelOffset;
            panel.localRotation = Quaternion.Euler(panelTilt);

            KartUI.Box(panel, "Backing", new Vector3(0f, -0.01f, 0.012f), new Vector3(0.25f, 0.21f, 0.01f), new Color(0.1f, 0.1f, 0.15f));
            var tabSize = new Vector3(0.058f, 0.028f, 0.02f);
            string[] names = { "Menu", "Sound", "Controls", "Voice" };
            for (int i = 0; i < 4; i++)
            {
                var p = (Page)i;
                tabs[i] = PokeButton.Create(panel, "Tab" + names[i], names[i], new Vector3((i - 1.5f) * 0.061f, 0.075f, 0f), tabSize, TabOff, () => Show(p), 0.085f);
                pages[i] = new GameObject(names[i] + "Page").transform;
                pages[i].SetParent(panel, false);
            }

            BuildMain(pages[0]);
            BuildSound(pages[1]);
            BuildControls(pages[2]);
            BuildVoice(pages[3]);
            Show(Page.Main);
            panel.gameObject.SetActive(false); // closed until you press the menu button
        }

        void Show(Page p)
        {
            page = p;
            for (int i = 0; i < 4; i++)
            {
                pages[i].gameObject.SetActive(i == (int)p);
                tabs[i].SetColor(i == (int)p ? TabOn : TabOff);
            }
            nextRefresh = 0f;
        }

        // ---------------- pages

        void BuildMain(Transform root)
        {
            info = KartUI.Label(root, "", new Vector3(0f, 0.04f, 0f), 0.11f, Color.white, new Vector2(0.24f, 0.04f));
            var size = new Vector3(0.075f, 0.032f, 0.02f);
            var wide = new Vector3(0.24f, 0.032f, 0.02f);
            PokeButton.Create(root, "Recenter", "Recenter", new Vector3(-0.082f, 0.002f, 0f), size, new Color(0.6f, 0.8f, 1f), () => KartRig.Instance?.Recenter(), 0.1f);
            PokeButton.Create(root, "Respawn", "Respawn", new Vector3(0f, 0.002f, 0f), size, new Color(1f, 0.85f, 0.4f), Respawn, 0.1f);
            PokeButton.Create(root, "Hub", "Hub", new Vector3(0.082f, 0.002f, 0f), size, new Color(0.5f, 1f, 0.6f), () => SessionManager.Instance?.GoToHub(), 0.1f);
            lobbyButton = PokeButton.Create(root, "Lobby", "Create Lobby", new Vector3(0f, -0.038f, 0f), wide, new Color(1f, 0.6f, 0.9f), ToggleLobby, 0.1f);
            buildButton = PokeButton.Create(root, "Build", "Build Mode", new Vector3(0f, -0.078f, 0f), wide, new Color(0.5f, 0.9f, 1f), BuildMode.Toggle, 0.1f);
        }

        void BuildSound(Transform root)
        {
            sfxBar = VolumeRow(root, "Sound effects", 0.045f, d => ChangeSfx(d), new Color(0.6f, 1f, 0.7f));
            engineBar = VolumeRow(root, "Kart engine", -0.008f, d => ChangeEngine(d), new Color(1f, 0.85f, 0.5f));
            musicBar = VolumeRow(root, "Music", -0.061f, d => ChangeMusic(d), new Color(0.6f, 0.85f, 1f));
            KartUI.Label(root, "All sounds and music are made by the game", new Vector3(0f, -0.108f, 0f), 0.065f, new Color(1f, 1f, 1f, 0.6f), new Vector2(0.24f, 0.015f));
            RefreshSound();
        }

        /// <summary>A title, then [-] bar [+] underneath it. Returns the bar's text.</summary>
        static TextMeshPro VolumeRow(Transform root, string title, float y, System.Action<float> change, Color barColor)
        {
            var small = new Vector3(0.04f, 0.028f, 0.02f);
            KartUI.Label(root, title, new Vector3(0f, y, 0f), 0.1f, Color.white, new Vector2(0.24f, 0.018f));
            PokeButton.Create(root, title + "Down", "-", new Vector3(-0.1f, y - 0.025f, 0f), small, new Color(1f, 0.6f, 0.5f), () => change(-0.1f), 0.2f);
            var bar = KartUI.Label(root, "", new Vector3(0f, y - 0.025f, 0f), 0.1f, barColor, new Vector2(0.15f, 0.028f));
            PokeButton.Create(root, title + "Up", "+", new Vector3(0.1f, y - 0.025f, 0f), small, new Color(0.5f, 1f, 0.6f), () => change(0.1f), 0.2f);
            return bar;
        }

        void BuildControls(Transform root)
        {
            var wide = new Vector3(0.24f, 0.03f, 0.02f);
            turnButton = PokeButton.Create(root, "Turning", "", new Vector3(0f, 0.04f, 0f), wide, new Color(0.7f, 0.75f, 1f), CycleTurn, 0.1f);
            wheelButton = PokeButton.Create(root, "Wheel", "", new Vector3(0f, 0.004f, 0f), wide, new Color(0.7f, 0.75f, 1f), ToggleWheel, 0.1f);
            KartUI.Label(root,
                "<b>Drive:</b> R trigger gas, L trigger brake, grab wheel (grip)\n" +
                "X drift  A boost  B respawn  L stick click recenter\n" +
                "<b>Walk:</b> L stick (click R stick = run), R stick turn\n" +
                "<b>Free Roam:</b> R stick click = build mode",
                new Vector3(0f, -0.06f, 0f), 0.065f, new Color(0.85f, 0.9f, 1f), new Vector2(0.24f, 0.07f));
            PokeButton.Create(root, "Tutorial", "Play the tutorial again", new Vector3(0f, -0.112f, 0f), new Vector3(0.24f, 0.026f, 0.02f),
                new Color(1f, 0.85f, 0.4f), () => { if (SessionManager.Instance != null && !SessionManager.Instance.Busy && !Tutorial.Running) SessionManager.Instance.StartTutorial(); }, 0.09f);
            RefreshControls();
        }

        void BuildVoice(Transform root)
        {
            var wide = new Vector3(0.24f, 0.03f, 0.02f);
            micButton = PokeButton.Create(root, "MicMode", "", new Vector3(0f, 0.04f, 0f), wide, new Color(0.5f, 1f, 0.6f), CycleMic, 0.1f);
            micMeter = KartUI.Label(root, "", new Vector3(0f, 0.012f, 0f), 0.09f, new Color(0.6f, 1f, 0.7f), new Vector2(0.24f, 0.02f));
            voiceBar = VolumeRow(root, "Voice volume", -0.005f, d => ChangeVoice(d), new Color(0.6f, 0.85f, 1f));
            micTestButton = PokeButton.Create(root, "MicTest", "", new Vector3(0f, -0.065f, 0f), wide, new Color(1f, 0.85f, 0.4f),
                () => { VoiceChat.MicTest = !VoiceChat.MicTest; RefreshVoice(); }, 0.09f);
            KartUI.Label(root, "Mic Test plays your voice back so you can hear yourself", new Vector3(0f, -0.095f, 0f), 0.055f,
                new Color(1f, 1f, 1f, 0.6f), new Vector2(0.24f, 0.015f));
            RefreshVoice();
        }

        void CycleMic()
        {
            GameSettings.Mic = (MicMode)(((int)GameSettings.Mic + 1) % 3);
            RefreshVoice();
        }

        void ChangeVoice(float d)
        {
            GameSettings.VoiceVolume = Mathf.Round((GameSettings.VoiceVolume + d) * 10f) / 10f;
            RefreshVoice();
        }

        void RefreshVoice()
        {
            var mode = GameSettings.Mic;
            micButton?.SetLabel(mode == MicMode.Open ? "Mic: open (talk any time)" : mode == MicMode.PushToTalk ? "Mic: push-to-talk (hold Y)" : "Mic: muted");
            micButton?.SetColor(mode == MicMode.Muted ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 1f, 0.6f));
            micTestButton?.SetLabel(VoiceChat.MicTest ? "Mic Test: ON (tap to stop)" : "Mic Test: off");
            if (voiceBar) voiceBar.text = Bar(GameSettings.VoiceVolume);
            var v = VoiceChat.Local;
            if (micMeter)
            {
                if (v == null) micMeter.text = "Join a server to use voice chat";
                else
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt(v.MicLevel * 10f), 0, 10);
                    micMeter.text = "<color=#7CFF8A>" + new string('|', n) + "</color><color=#444444>" + new string('|', 10 - n) + "</color>  " + v.MicStatus;
                }
            }
        }

        // ---------------- actions

        static void Respawn()
        {
            if (GameState.CurrentMode == KartMode.Hub) PlayerAvatar.Local?.RespawnInHub();
            else KartController.Local?.Respawn();
        }

        void ToggleLobby()
        {
            var s = SessionManager.Instance;
            if (s == null || s.Busy) return;
            if (s.IsPrivate) s.LeaveToPublicHub();
            else s.CreatePrivateLobby();
        }

        void ChangeSfx(float d)
        {
            GameSettings.SfxVolume = Mathf.Round((GameSettings.SfxVolume + d) * 10f) / 10f;
            AudioManager.RefreshVolumes();
            RefreshSound();
            AudioManager.Play(AudioManager.Sfx.Checkpoint, 0.8f); // a sample at the new volume
        }

        void ChangeEngine(float d)
        {
            GameSettings.EngineVolume = Mathf.Round((GameSettings.EngineVolume + d) * 10f) / 10f;
            AudioManager.RefreshVolumes();
            RefreshSound();
        }

        void ChangeMusic(float d)
        {
            GameSettings.MusicVolume = Mathf.Round((GameSettings.MusicVolume + d) * 10f) / 10f;
            AudioManager.RefreshVolumes();
            RefreshSound();
        }

        void CycleTurn()
        {
            GameSettings.Turn = (TurnStyle)(((int)GameSettings.Turn + 1) % 3);
            RefreshControls();
        }

        void ToggleWheel()
        {
            GameSettings.GrabWheel = !GameSettings.GrabWheel;
            RefreshControls();
        }

        static string Bar(float v)
        {
            int n = Mathf.RoundToInt(v * 10f);
            return new string('|', n) + "<color=#444444>" + new string('|', 10 - n) + "</color>  " + n * 10 + "%";
        }

        void RefreshSound()
        {
            if (sfxBar) sfxBar.text = Bar(GameSettings.SfxVolume);
            if (musicBar) musicBar.text = Bar(GameSettings.MusicVolume);
            if (engineBar) engineBar.text = Bar(GameSettings.EngineVolume);
        }

        void RefreshControls()
        {
            turnButton?.SetLabel("Turning: " + GameSettings.TurnName(GameSettings.Turn));
            wheelButton?.SetLabel(GameSettings.GrabWheel ? "Steering: grab the wheel" : "Steering: left stick only");
        }

        void Update()
        {
            if (panel == null) return;
            if (KartInput.MenuPressed)
            {
                bool open = !panel.gameObject.activeSelf;
                panel.gameObject.SetActive(open);
                AudioManager.Play(AudioManager.Sfx.Click, 0.8f, open ? 1.2f : 0.9f);
                if (open) Show(page);
            }
            if (!panel.gameObject.activeSelf) return;
            if (page == Page.Voice) { RefreshVoice(); return; } // live mic meter
            if (Time.time < nextRefresh) return;
            if (page != Page.Main) return;
            nextRefresh = Time.time + 0.25f;

            var s = SessionManager.Instance;
            if (s == null) return;
            info.text = s.DescribeLobby();
            lobbyButton.SetLabel(s.IsPrivate ? "Leave Lobby" : "Create Private Lobby");
            // Build mode is a Free Roam thing.
            buildButton.gameObject.SetActive(GameState.CurrentMode == KartMode.FreeRoam);
            buildButton.SetLabel(BuildMode.Active ? "Back to Kart" : "Build Mode");
        }
    }
}
