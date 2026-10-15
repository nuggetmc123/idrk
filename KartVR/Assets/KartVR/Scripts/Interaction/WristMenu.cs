using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// A little panel floating above your left controller. Poke it with your
    /// right hand. Y (left controller) shows/hides it. Builds itself at runtime.
    /// </summary>
    public class WristMenu : MonoBehaviour
    {
        public Vector3 panelOffset = new Vector3(0f, 0.09f, 0.03f);
        public Vector3 panelTilt = new Vector3(35f, 0f, 0f);

        Transform panel;
        TextMeshPro info;
        PokeButton lobbyButton;
        float nextRefresh;

        void Start()
        {
            panel = new GameObject("WristPanel").transform;
            panel.SetParent(transform, false);
            panel.localPosition = panelOffset;
            panel.localRotation = Quaternion.Euler(panelTilt);

            KartUI.Box(panel, "Backing", new Vector3(0f, 0f, 0.012f), new Vector3(0.22f, 0.15f, 0.01f), new Color(0.1f, 0.1f, 0.15f));
            info = KartUI.Label(panel, "", new Vector3(0f, 0.045f, 0f), 0.12f, Color.white, new Vector2(0.21f, 0.06f));

            var size = new Vector3(0.065f, 0.035f, 0.02f);
            PokeButton.Create(panel, "Recenter", "Recenter", new Vector3(-0.072f, -0.005f, 0f), size, new Color(0.6f, 0.8f, 1f), () => KartRig.Instance?.Recenter(), 0.1f);
            PokeButton.Create(panel, "Respawn", "Respawn", new Vector3(0f, -0.005f, 0f), size, new Color(1f, 0.85f, 0.4f), Respawn, 0.1f);
            PokeButton.Create(panel, "Hub", "Hub", new Vector3(0.072f, -0.005f, 0f), size, new Color(0.5f, 1f, 0.6f), () => SessionManager.Instance?.GoToHub(), 0.1f);
            lobbyButton = PokeButton.Create(panel, "Lobby", "Create Lobby", new Vector3(0f, -0.05f, 0f), new Vector3(0.21f, 0.035f, 0.02f), new Color(1f, 0.6f, 0.9f), ToggleLobby, 0.1f);
        }

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

        void Update()
        {
            if (panel == null) return;
            if (KartInput.MenuPressed) panel.gameObject.SetActive(!panel.gameObject.activeSelf);
            if (!panel.gameObject.activeSelf || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.25f;

            var s = SessionManager.Instance;
            if (s == null) return;
            info.text = s.DescribeLobby();
            lobbyButton.SetLabel(s.IsPrivate ? "Leave Lobby" : "Create Private Lobby");
        }
    }
}
