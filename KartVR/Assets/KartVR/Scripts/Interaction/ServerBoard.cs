using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The ROOMS board in the hub (it replaced the old numbered servers). Shows your room code big, so friends can
    /// type it on the keypad next to it and join you on whatever map you're on. You're put in rooms automatically
    /// (the fullest one with space, a new one with a random code when they're all full); FIND A ROOM puts you in a
    /// different one. Also makes or leaves a private lobby. Builds itself at runtime; it faces this object's -Z.
    /// </summary>
    public class ServerBoard : MonoBehaviour
    {
        TextMeshPro code, info;
        PokeButton lobbyButton;
        float nextRefresh;

        void Start()
        {
            KartUI.Box(transform, "Backing", new Vector3(0f, 0f, 0.06f), new Vector3(2.6f, 1.5f, 0.1f), new Color(0.09f, 0.1f, 0.14f));
            KartUI.Box(transform, "Trim", new Vector3(0f, 0.74f, 0f), new Vector3(2.62f, 0.04f, 0.04f), new Color(0.3f, 0.9f, 1f));
            KartUI.Label(transform, "<b>ROOMS</b>", new Vector3(0f, 0.62f, -0.02f), 1.4f, Color.white, new Vector2(2.4f, 0.2f));
            code = KartUI.Label(transform, "", new Vector3(0f, 0.38f, -0.02f), 2.4f, new Color(1f, 0.9f, 0.3f), new Vector2(2.4f, 0.3f));
            info = KartUI.Label(transform, "", new Vector3(0f, 0.12f, -0.02f), 0.8f, new Color(0.8f, 0.95f, 1f), new Vector2(2.4f, 0.25f));
            var size = new Vector3(1.15f, 0.3f, 0.08f);
            PokeButton.Create(transform, "FindRoom", "FIND A ROOM", new Vector3(-0.62f, -0.2f, -0.02f), size, new Color(0.45f, 0.75f, 1f), FindRoom, 0.85f)
                .cooldown = 3f;
            lobbyButton = PokeButton.Create(transform, "Lobby", "CREATE PRIVATE LOBBY", new Vector3(0.62f, -0.2f, -0.02f), size, new Color(1f, 0.6f, 0.9f), ToggleLobby, 0.6f);
            lobbyButton.cooldown = 3f;
            KartUI.Label(transform, "Friend playing? Type THEIR code on the keypad and press JOIN:\nyou'll go to them on any map. Rooms hold 6.",
                new Vector3(0f, -0.55f, -0.02f), 0.5f, new Color(1f, 1f, 1f, 0.75f), new Vector2(2.45f, 0.25f));
        }

        void FindRoom()
        {
            var s = SessionManager.Instance;
            if (s == null || s.Busy) return;
            if (!SessionManager.MultiplayerAvailable) { s.SetStatus("Offline (solo) - import Photon Fusion to play online", 5f); return; }
            AudioManager.Play(AudioManager.Sfx.Teleport);
            s.JoinPublic(KartMode.Hub);
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
            if (info == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            var s = SessionManager.Instance;
            if (s == null) return;
            lobbyButton.SetLabel(s.IsPrivate ? "LEAVE LOBBY" : "CREATE PRIVATE LOBBY");
            bool online = s.IsConnected && !s.IsSolo && !s.IsTutorial && SessionManager.MultiplayerAvailable;
            code.text = online && !string.IsNullOrEmpty(s.RoomCode) ? "<size=55%>YOUR CODE</size>  " + KartUI.FormatCode(s.RoomCode) : "";
            if (!SessionManager.MultiplayerAvailable) info.text = "Offline (solo) - import Photon Fusion to play online";
            else if (s.Busy) info.text = s.Status;
            else if (s.IsSolo) info.text = "<color=#ffb347>Solo: couldn't go online.</color> Touch FIND A ROOM to try again";
            else if (s.IsPrivate) info.text = "Private lobby: " + s.PlayerCount + " on this map. Everyone can go to any map\nand stays in the lobby.";
            else info.text = "Public room: " + s.PlayerCount + "/" + SessionManager.MaxPlayers + " players";
        }
    }
}
