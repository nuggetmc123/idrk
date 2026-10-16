using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Wall panel in the hub for picking a public server. Each server holds
    /// <see cref="SessionManager.MaxPlayers"/> players (one per garage); if the one
    /// you pick is full you're put on the next free one. Builds itself at runtime;
    /// the buttons face the same way as this object's -Z (like the other signs).
    /// </summary>
    public class ServerBoard : MonoBehaviour
    {
        public int buttons = 6;

        TextMeshPro info;
        float nextRefresh;

        void Start()
        {
            KartUI.Box(transform, "Backing", new Vector3(0f, 0f, 0.06f), new Vector3(2.6f, 1.5f, 0.1f), new Color(0.09f, 0.1f, 0.14f));
            KartUI.Box(transform, "Trim", new Vector3(0f, 0.74f, 0f), new Vector3(2.62f, 0.04f, 0.04f), new Color(0.3f, 0.9f, 1f));
            KartUI.Label(transform, "<b>SERVERS</b>", new Vector3(0f, 0.6f, -0.02f), 1.6f, Color.white, new Vector2(2.4f, 0.25f));
            info = KartUI.Label(transform, "", new Vector3(0f, 0.36f, -0.02f), 0.9f, new Color(0.8f, 0.95f, 1f), new Vector2(2.4f, 0.3f));
            var size = new Vector3(0.7f, 0.32f, 0.08f);
            for (int i = 0; i < buttons; i++)
            {
                int number = i + 1;
                var pos = new Vector3((i % 3 - 1) * 0.8f, i < 3 ? -0.05f : -0.45f, -0.02f);
                PokeButton.Create(transform, "Server" + number, "Server " + number, pos, size, new Color(0.45f, 0.75f, 1f),
                    () => SessionManager.Instance?.JoinServer(number), 0.9f);
            }
            KartUI.Label(transform, "6 players per server. Full? You'll get the next one.", new Vector3(0f, -0.68f, -0.02f), 0.55f,
                new Color(1f, 1f, 1f, 0.7f), new Vector2(2.4f, 0.12f));
        }

        void Update()
        {
            if (info == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            var s = SessionManager.Instance;
            if (s == null) return;
            if (!SessionManager.MultiplayerAvailable) info.text = "Offline (solo) - import Photon Fusion to play online";
            else if (s.IsPrivate) info.text = "In private lobby " + KartUI.FormatCode(s.LobbyCode) + " (servers are for public play)";
            else info.text = "You're on server " + s.ServerNumber + "  (" + s.PlayerCount + "/" + SessionManager.MaxPlayers + " players)";
        }
    }
}
