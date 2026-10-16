using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Moderator menu (Menu > Mod Menu, only for players in the moderation list). Shows everyone on your server,
    /// with a warning next to anyone the anti-cheat caught. Pick a player, then Kick (5 minutes), Ban (1 day,
    /// 7 days or forever), Force rename or Mute (10 minutes). Bans ask you to press twice.
    /// The player's ID and headset code are shown so the game's owner can make a ban permanent.
    /// </summary>
    public class ModMenu : MonoBehaviour
    {
        static ModMenu instance;

        public static void Toggle()
        {
            if (instance != null) { Close(); return; }
            if (!Moderation.IAmMod) { SessionManager.Instance?.SetStatus("Only moderators can open the mod menu", 4f); return; }
            var go = new GameObject("ModMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ModMenu>();
        }

        public static void Close()
        {
            if (instance == null) return;
            var menu = instance;
            instance = null; // straight away, so a quick second press can't open a copy
            Destroy(menu.gameObject);
        }

        void OnDestroy()
        {
            // The panel rides on your rig (not under this object), so it has to be removed by hand.
            // (Leaving it behind is what made Close look like it did nothing and the menu double up.)
            if (panel != null) Destroy(panel.gameObject);
            if (instance == this) instance = null;
            SpawnMenu.Close();
        }

        Transform panel, listRoot;
        TextMeshPro details;
        KartController selected;
        string armed;          // a ban button waiting for its second press
        float armedUntil, nextRefresh;
        readonly List<KartController> shown = new List<KartController>();

        void Start()
        {
            panel = new GameObject("ModPanel").transform;
            panel.SetParent(transform, false);
            panel.localScale = Vector3.one * 0.85f;
            var rig = KartRig.Instance;
            if (rig != null && rig.head != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
                panel.SetParent(rig.transform, true); // rides along with you
                panel.position = rig.head.position + fwd * 1.2f - Vector3.up * 0.1f;
                panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            KartUI.Box(panel, "Backing", new Vector3(0f, 0f, 0.02f), new Vector3(1.2f, 0.8f, 0.02f), new Color(0.09f, 0.05f, 0.06f));
            KartUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.015f), new Vector3(1.22f, 0.015f, 0.025f), KartUI.Glow(new Color(1f, 0.35f, 0.3f)));
            KartUI.Label(panel, "<b>MOD MENU</b>", new Vector3(-0.2f, 0.34f, 0f), 0.6f, new Color(1f, 0.55f, 0.45f), new Vector2(0.7f, 0.08f));
            PokeButton.Create(panel, "Close", "Close", new Vector3(0.48f, 0.34f, 0f), new Vector3(0.18f, 0.07f, 0.03f), new Color(0.6f, 0.6f, 0.7f), Close, 0.3f).cooldown = 0.8f;
            listRoot = new GameObject("Players").transform;
            listRoot.SetParent(panel, false);
            details = KartUI.Label(panel, "", new Vector3(0.28f, 0.16f, 0f), 0.24f, Color.white, new Vector2(0.58f, 0.26f), TextAlignmentOptions.TopLeft);
            details.enableWordWrapping = true;

            var size = new Vector3(0.27f, 0.07f, 0.03f);
            Action("Kick", "Kick (5 min)", new Vector3(0.14f, -0.02f, 0f), size, new Color(1f, 0.8f, 0.4f), Moderation.Action.Kick, 0, false);
            Action("Mute", "Mute 10 min", new Vector3(0.43f, -0.02f, 0f), size, new Color(0.7f, 0.75f, 1f), Moderation.Action.Mute, 10, false);
            Action("Rename", "Force rename", new Vector3(0.14f, -0.1f, 0f), size, new Color(0.6f, 0.9f, 1f), Moderation.Action.Rename, 0, false);
            Action("Ban1", "Ban 1 day", new Vector3(0.43f, -0.1f, 0f), size, new Color(1f, 0.55f, 0.45f), Moderation.Action.Ban, 24, true);
            Action("Ban7", "Ban 7 days", new Vector3(0.14f, -0.18f, 0f), size, new Color(1f, 0.45f, 0.4f), Moderation.Action.Ban, 24 * 7, true);
            Action("BanForever", "Ban forever", new Vector3(0.43f, -0.18f, 0f), size, new Color(1f, 0.3f, 0.3f), Moderation.Action.Ban, 0, true);
            // Your own mod tools.
            KartUI.Label(panel, "<b>YOUR TOOLS</b>", new Vector3(-0.33f, -0.27f, 0f), 0.22f, new Color(1f, 0.85f, 0.4f), new Vector2(0.46f, 0.04f));
            var tool = new Vector3(0.37f, 0.07f, 0.03f);
            invisibleButton = PokeButton.Create(panel, "Invisible", "", new Vector3(-0.39f, -0.34f, 0f), tool, new Color(0.75f, 0.6f, 1f), () => Flip(ref ModPowers.Invisible, "Invisible"), 0.24f);
            buildButton = PokeButton.Create(panel, "SpawnMenu", "Spawn Menu", new Vector3(0f, -0.34f, 0f), tool, new Color(0.5f, 1f, 0.65f), SpawnMenu.Toggle, 0.24f);
            blasterButton = PokeButton.Create(panel, "Blaster", "", new Vector3(0.39f, -0.34f, 0f), tool, new Color(1f, 0.5f, 0.4f), () => Flip(ref ModPowers.Blaster, "Blaster"), 0.24f);
            foreach (var b in new[] { invisibleButton, buildButton, blasterButton }) b.cooldown = 0.6f;
            RefreshTools();
        }

        PokeButton invisibleButton, buildButton, blasterButton;

        void Flip(ref bool power, string what)
        {
            power = !power;
            AudioManager.Play(AudioManager.Sfx.Click, 1f, power ? 1.3f : 0.9f);
            string tip = what == "Invisible" ? (power ? "Nobody else can see you now" : "Everyone can see you again")
                : power ? "Get out of your kart (Y) and pull the right trigger to shoot" : "Blaster put away";
            SessionManager.Instance?.SetStatus(what + (power ? " ON: " : " OFF: ") + tip, 5f);
            RefreshTools();
        }

        void RefreshTools()
        {
            invisibleButton.SetLabel("Invisible: " + (ModPowers.Invisible ? "<b>ON</b>" : "off"));
            buildButton.SetLabel("Spawn Menu");
            blasterButton.SetLabel("Blaster: " + (ModPowers.Blaster ? "<b>ON</b>" : "off"));
        }

        void Action(string name, string label, Vector3 pos, Vector3 size, Color color, Moderation.Action action, int arg, bool confirm)
        {
            PokeButton.Create(panel, name, label, pos, size, color, () => Do(name, label, action, arg, confirm), 0.26f).cooldown = 0.6f;
        }

        void Do(string name, string label, Moderation.Action action, int arg, bool confirm)
        {
            if (selected == null || !selected.IsReady) { SessionManager.Instance?.SetStatus("Pick a player first", 3f); return; }
            if (confirm && (armed != name || Time.time > armedUntil))
            {
                armed = name;
                armedUntil = Time.time + 4f;
                SessionManager.Instance?.SetStatus("Press " + label + " again to confirm (" + selected.DisplayName + ")", 4f);
                return;
            }
            armed = null;
            Moderation.Send(selected, action, arg);
            AudioManager.Play(AudioManager.Sfx.Click);
            SessionManager.Instance?.SetStatus(label + ": " + selected.DisplayName, 5f);
        }

        void Update()
        {
            if (!Moderation.IAmMod) { ModPowers.Reset(); Close(); return; }
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + 1f;

            var others = KartController.All.Where(k => k != null && k.IsReady && !k.IsLocal).Take(5).ToList();
            if (!others.SequenceEqual(shown)) RebuildList(others);
            if (selected != null && !selected.IsReady) selected = null;

            if (selected == null) details.text = others.Count == 0 ? "Nobody else is on this server." : "Pick a player on the left.";
            else
            {
                string id = selected.PlayerId.ToString();
                int strikes = AntiCheat.Strikes(id);
                details.text = "<b>" + selected.DisplayName + "</b>" + (selected.DisplayName != selected.Nickname.ToString() ? " (sent: " + selected.Nickname + ")" : "") +
                               "\nPlayer ID: " + id + "\nHeadset: " + selected.DeviceHash +
                               (strikes > 0 ? "\n<color=#ff8a65>Anti-cheat: caught moving too fast " + strikes + "x</color>" : "\nAnti-cheat: nothing seen") +
                               (Moderation.IsMod(id) ? "\n<color=#9fe39f>(also a moderator)</color>" : "");
            }
        }

        void RebuildList(List<KartController> others)
        {
            shown.Clear();
            shown.AddRange(others);
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            for (int i = 0; i < others.Count; i++)
            {
                var k = others[i];
                bool flagged = AntiCheat.Flagged(k.PlayerId.ToString());
                string label = (flagged ? "<color=#ff5040>! </color>" : "") + k.DisplayName;
                PokeButton.Create(listRoot, "Player" + i, label, new Vector3(-0.33f, 0.24f - i * 0.075f, 0f), new Vector3(0.46f, 0.065f, 0.03f),
                    flagged ? new Color(1f, 0.6f, 0.5f) : new Color(0.5f, 0.6f, 0.75f), () => { selected = k; nextRefresh = 0f; }, 0.28f);
            }
        }
    }
}
