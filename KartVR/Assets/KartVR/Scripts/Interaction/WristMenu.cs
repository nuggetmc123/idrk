using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The menu: a panel floating above your left controller. Press the MENU
    /// button (three lines, left controller) to open or close it; it starts
    /// closed. Poke it with your right hand, or point at it and pull the trigger. Three pages:
    /// Menu (recenter, respawn, hub, lobby, build mode), Sound (effects and music
    /// volume) and Controls (turning style, steering wheel, list of controls).
    /// Keyboard: Tab or Esc opens it, left click presses buttons.
    /// </summary>
    public class WristMenu : MonoBehaviour
    {
        public Vector3 panelOffset = new Vector3(0f, 0.1f, 0.03f);
        public Vector3 panelTilt = new Vector3(35f, 0f, 0f);

        enum Page { Main, Sound, Controls }

        Transform panel;
        readonly Transform[] pages = new Transform[3];
        readonly PokeButton[] tabs = new PokeButton[3];
        Page page;
        TextMeshPro info, sfxBar, musicBar;
        PokeButton lobbyButton, buildButton, turnButton, wheelButton;
        float nextRefresh;

        static readonly Color TabOn = new Color(0.4f, 0.85f, 1f), TabOff = new Color(0.35f, 0.38f, 0.45f);

        void Start()
        {
            panel = new GameObject("WristPanel").transform;
            panel.SetParent(transform, false);
            panel.localPosition = panelOffset;
            panel.localRotation = Quaternion.Euler(panelTilt);

            KartUI.Box(panel, "Backing", new Vector3(0f, -0.01f, 0.012f), new Vector3(0.25f, 0.21f, 0.01f), new Color(0.1f, 0.1f, 0.15f));
            var tabSize = new Vector3(0.078f, 0.028f, 0.02f);
            string[] names = { "Menu", "Sound", "Controls" };
            for (int i = 0; i < 3; i++)
            {
                var p = (Page)i;
                tabs[i] = PokeButton.Create(panel, "Tab" + names[i], names[i], new Vector3((i - 1) * 0.082f, 0.075f, 0f), tabSize, TabOff, () => Show(p), 0.1f);
                pages[i] = new GameObject(names[i] + "Page").transform;
                pages[i].SetParent(panel, false);
            }

            BuildMain(pages[0]);
            BuildSound(pages[1]);
            BuildControls(pages[2]);
            Show(Page.Main);
            panel.gameObject.SetActive(false); // closed until you press the menu button
        }

        void Show(Page p)
        {
            page = p;
            for (int i = 0; i < 3; i++)
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
            var small = new Vector3(0.04f, 0.032f, 0.02f);
            KartUI.Label(root, "Sound effects", new Vector3(0f, 0.045f, 0f), 0.11f, Color.white, new Vector2(0.24f, 0.02f));
            PokeButton.Create(root, "SfxDown", "-", new Vector3(-0.1f, 0.018f, 0f), small, new Color(1f, 0.6f, 0.5f), () => ChangeSfx(-0.1f), 0.2f);
            sfxBar = KartUI.Label(root, "", new Vector3(0f, 0.018f, 0f), 0.11f, new Color(0.6f, 1f, 0.7f), new Vector2(0.15f, 0.03f));
            PokeButton.Create(root, "SfxUp", "+", new Vector3(0.1f, 0.018f, 0f), small, new Color(0.5f, 1f, 0.6f), () => ChangeSfx(0.1f), 0.2f);

            KartUI.Label(root, "Music", new Vector3(0f, -0.022f, 0f), 0.11f, Color.white, new Vector2(0.24f, 0.02f));
            PokeButton.Create(root, "MusicDown", "-", new Vector3(-0.1f, -0.05f, 0f), small, new Color(1f, 0.6f, 0.5f), () => ChangeMusic(-0.1f), 0.2f);
            musicBar = KartUI.Label(root, "", new Vector3(0f, -0.05f, 0f), 0.11f, new Color(0.6f, 0.85f, 1f), new Vector2(0.15f, 0.03f));
            PokeButton.Create(root, "MusicUp", "+", new Vector3(0.1f, -0.05f, 0f), small, new Color(0.5f, 1f, 0.6f), () => ChangeMusic(0.1f), 0.2f);
            KartUI.Label(root, "All sounds and music are made by the game", new Vector3(0f, -0.088f, 0f), 0.07f, new Color(1f, 1f, 1f, 0.6f), new Vector2(0.24f, 0.015f));
            RefreshSound();
        }

        void BuildControls(Transform root)
        {
            var wide = new Vector3(0.24f, 0.03f, 0.02f);
            turnButton = PokeButton.Create(root, "Turning", "", new Vector3(0f, 0.04f, 0f), wide, new Color(0.7f, 0.75f, 1f), CycleTurn, 0.1f);
            wheelButton = PokeButton.Create(root, "Wheel", "", new Vector3(0f, 0.004f, 0f), wide, new Color(0.7f, 0.75f, 1f), ToggleWheel, 0.1f);
            KartUI.Label(root,
                "<b>Drive:</b> R trigger gas, L trigger brake, grab wheel (grip)\n" +
                "X drift  A boost  B respawn  L stick click recenter\n" +
                "<b>Walk:</b> L stick (hold R trigger = run), R stick turn\n" +
                "<b>Free Roam:</b> R stick click = build mode",
                new Vector3(0f, -0.06f, 0f), 0.065f, new Color(0.85f, 0.9f, 1f), new Vector2(0.24f, 0.07f));
            RefreshControls();
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
            if (!panel.gameObject.activeSelf || Time.time < nextRefresh) return;
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
