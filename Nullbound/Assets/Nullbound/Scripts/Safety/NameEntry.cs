using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// "Pick your name" keyboard. Pops up in front of you in the main menu the first time you play (you can't play
    /// until you've picked one), and again if a moderator makes you change a bad name. Point the laser at the keys
    /// and pull the trigger (or poke them). The name has to be one nobody else has (<see cref="NameRegistry"/>).
    /// Once picked, your name is locked: you can't change it yourself.
    /// </summary>
    public class NameEntry : MonoBehaviour
    {
        static NameEntry instance;
        static string pendingMessage;

        /// <summary>True while the keyboard is up (the tutorial waits for it).</summary>
        public static bool Showing => instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;

        /// <summary>Open the keyboard (used when a moderator asks for a new name).</summary>
        public static void Show(string message)
        {
            pendingMessage = message;
            if (instance != null) instance.Open();
        }

        Transform panel;
        TextMeshPro typed, message;
        string text = "";
        bool upper = true;
        Vector3 panelLocal;
        bool placed;
        PokeButton shiftButton;

        void Awake() { instance = this; }

        void Update()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.head == null) return;
            if (!PlayerProfile.NameLocked && !Showing) Open();
            if (!Showing) return;
            Follow(rig);
        }

        void Open()
        {
            if (panel == null) Build();
            panel.gameObject.SetActive(true);
            placed = false;
            text = "";
            Refresh();
            message.text = pendingMessage ?? "This is the name everyone sees. <b>You can't change it later</b>, so choose well!\nNo rude names: they get you banned.";
            pendingMessage = null;
        }

        void Build()
        {
            panel = new GameObject("NamePanel").transform;
            panel.SetParent(transform, false);
            panel.localScale = Vector3.one * 0.85f;
            NBUI.Box(panel, "Backing", new Vector3(0f, 0f, 0.02f), new Vector3(1.12f, 0.78f, 0.02f), new Color(0.06f, 0.07f, 0.1f));
            NBUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, 0.39f, 0.015f), new Vector3(1.14f, 0.015f, 0.025f), NBUI.Glow(new Color(0.3f, 0.9f, 1f)));
            NBUI.Label(panel, "<b>PICK YOUR NAME</b>", new Vector3(0f, 0.33f, 0f), 0.6f, new Color(1f, 0.85f, 0.25f), new Vector2(1f, 0.08f));
            message = NBUI.Label(panel, "", new Vector3(0f, 0.255f, 0f), 0.25f, new Color(0.85f, 0.92f, 1f), new Vector2(1.05f, 0.08f));
            message.enableWordWrapping = true;
            NBUI.Box(panel, "TypedBox", new Vector3(0f, 0.17f, 0.005f), new Vector3(0.7f, 0.08f, 0.01f), new Color(0.15f, 0.17f, 0.22f));
            typed = NBUI.Label(panel, "", new Vector3(0f, 0.17f, -0.01f), 0.55f, Color.white, new Vector2(0.68f, 0.08f));

            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL_", "ZXCVBNM" };
            var key = new Vector3(0.085f, 0.075f, 0.03f);
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                float y = 0.07f - r * 0.088f;
                for (int i = 0; i < row.Length; i++)
                {
                    char c = row[i];
                    float x = (i - (row.Length - 1) * 0.5f) * 0.095f - (r == 3 ? 0.14f : 0f);
                    PokeButton.Create(panel, "Key" + c, c.ToString(), new Vector3(x, y, 0f), key, new Color(0.55f, 0.65f, 0.8f), () => Type(c), 0.4f).cooldown = 0.15f;
                }
            }
            float lastY = 0.07f - 3 * 0.088f;
            PokeButton.Create(panel, "Back", "<-", new Vector3(0.38f, lastY, 0f), new Vector3(0.13f, 0.075f, 0.03f), new Color(1f, 0.6f, 0.5f), Backspace, 0.4f).cooldown = 0.15f;
            shiftButton = PokeButton.Create(panel, "Shift", "", new Vector3(-0.44f, -0.34f, 0f), new Vector3(0.18f, 0.07f, 0.03f), new Color(0.7f, 0.75f, 1f), () => { upper = !upper; Refresh(); }, 0.3f);
            PokeButton.Create(panel, "Done", "<b>DONE</b>", new Vector3(0.3f, -0.34f, 0f), new Vector3(0.36f, 0.07f, 0.03f), new Color(0.5f, 1f, 0.6f), Done, 0.4f).cooldown = 0.6f;
        }

        void Type(char c)
        {
            if (text.Length >= NameRules.MaxLength) return;
            text += char.IsLetter(c) ? (upper ? c : char.ToLowerInvariant(c)) : c;
            if (text.Length == 1 && upper) upper = false; // capital first letter, then lower case
            Refresh();
        }

        void Backspace()
        {
            if (text.Length > 0) text = text.Substring(0, text.Length - 1);
            if (text.Length == 0) upper = true;
            Refresh();
        }

        void Refresh()
        {
            typed.text = text.Length > 0 ? text + "<color=#7fdfff>|</color>" : "<color=#777777>type your name</color>";
            shiftButton.SetLabel(upper ? "ABC" : "abc");
        }

        bool checking;

        void Done()
        {
            if (checking) return;
            if (!NameRules.Valid(text, out string problem))
            {
                message.text = "<color=#ff8080>" + problem + "</color>";
                AudioManager.Play(AudioManager.Sfx.Error);
                return;
            }
            string wanted = text;
            checking = true;
            message.text = "Checking nobody else has \"" + wanted + "\"...";
            StartCoroutine(NameRegistry.Claim(wanted, (ok, why) =>
            {
                checking = false;
                if (!ok)
                {
                    message.text = "<color=#ff8080>" + why + "</color>";
                    AudioManager.Play(AudioManager.Sfx.Error);
                    return;
                }
                PlayerProfile.SetLockedName(wanted);
                AudioManager.Play(AudioManager.Sfx.Ready);
                SessionManager.Instance?.SetStatus("Welcome, " + PlayerProfile.Nickname + "!", 5f);
                panel.gameObject.SetActive(false);
            }));
        }

        /// <summary>In front of you at chest height, lazily following your view.</summary>
        void Follow(VRRig rig)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = rig.transform.forward;
            fwd.Normalize();
            Vector3 target = rig.transform.InverseTransformPoint(rig.head.position + fwd * 1.3f - Vector3.up * 0.1f);
            panelLocal = placed ? Vector3.Lerp(panelLocal, target, 1f - Mathf.Exp(-1.5f * Time.deltaTime)) : target;
            placed = true;
            panel.position = rig.transform.TransformPoint(panelLocal);
            Vector3 away = panel.position - rig.head.position;
            if (away.sqrMagnitude > 0.01f) panel.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
