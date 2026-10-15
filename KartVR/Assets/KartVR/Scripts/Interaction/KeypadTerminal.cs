using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The lobby code terminal in the hub. Walk up to it and poke the keys:
    /// type a friend's 6-digit code and press JOIN, or press CREATE to make your
    /// own private lobby (the code then shows on the screen to share).
    /// Builds itself at runtime.
    /// </summary>
    public class KeypadTerminal : MonoBehaviour
    {
        TextMeshPro screen;
        TextMeshPro entry;
        PokeButton createButton;
        string typed = "";
        float nextRefresh;

        void Start()
        {
            var face = new GameObject("Face").transform;
            face.SetParent(transform, false);

            KartUI.Box(face, "Panel", new Vector3(0f, 0f, 0.03f), new Vector3(0.9f, 0.85f, 0.04f), new Color(0.12f, 0.12f, 0.18f));
            screen = KartUI.Label(face, "", new Vector3(0f, 0.33f, 0f), 0.32f, Color.white, new Vector2(0.85f, 0.16f));
            entry = KartUI.Label(face, "", new Vector3(-0.17f, 0.2f, 0f), 0.6f, new Color(0.4f, 1f, 0.5f), new Vector2(0.5f, 0.1f));

            // 3x4 keypad on the left.
            var key = new Vector3(0.12f, 0.09f, 0.04f);
            Color keyColor = new Color(0.85f, 0.85f, 0.9f);
            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "DEL", "0", "CLR" };
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 3, col = i % 3;
                string l = labels[i];
                var pos = new Vector3(-0.32f + col * 0.14f, 0.08f - row * 0.105f, 0f);
                PokeButton.Create(face, "Key_" + l, l, pos, key, keyColor, () => KeyPressed(l), l.Length > 1 ? 0.3f : 0.5f);
            }

            // Actions on the right.
            var wide = new Vector3(0.36f, 0.09f, 0.04f);
            PokeButton.Create(face, "Join", "JOIN", new Vector3(0.2f, 0.08f, 0f), wide, new Color(0.4f, 1f, 0.5f), Join, 0.45f);
            createButton = PokeButton.Create(face, "Create", "CREATE LOBBY", new Vector3(0.2f, -0.025f, 0f), wide, new Color(1f, 0.6f, 0.9f), CreateOrLeave, 0.3f);

            KartUI.Label(face, "Customize your kart\nin YOUR GARAGE", new Vector3(0.2f, -0.25f, 0f), 0.3f, new Color(1f, 0.8f, 0.4f), new Vector2(0.4f, 0.2f));
        }

        void KeyPressed(string key)
        {
            if (key == "DEL") { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); }
            else if (key == "CLR") typed = "";
            else if (typed.Length < 6) typed += key;
            nextRefresh = 0f;
        }

        void Join()
        {
            var s = SessionManager.Instance;
            if (s == null || s.Busy) return;
            if (typed.Length != 6) { s.SetStatus("Type a 6-digit code first", 3f); return; }
            s.JoinPrivateLobby(typed);
            typed = "";
        }

        void CreateOrLeave()
        {
            var s = SessionManager.Instance;
            if (s == null || s.Busy) return;
            if (s.IsPrivate) s.LeaveToPublicHub();
            else s.CreatePrivateLobby();
        }

        void Update()
        {
            if (screen == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.25f;
            var s = SessionManager.Instance;
            if (s == null) return;

            string transient = s.TransientStatus;
            screen.text = !string.IsNullOrEmpty(transient) ? transient : s.DescribeLobby();
            entry.text = "Code: " + typed.PadRight(6, '_');
            createButton.SetLabel(s.IsPrivate ? "LEAVE LOBBY" : "CREATE LOBBY");
        }
    }
}
