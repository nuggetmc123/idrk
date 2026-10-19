using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Moderator menu (Menu > Settings > Mod Menu, only shown to players in the moderation list). Lists everyone in
    /// your room: press a name to see their Player ID and headset code, then Kick, Mute 15 min, Ban 1 day,
    /// Ban 7 days, Ban forever or Force rename (bans ask you to press twice). Your own powers: Invincible and
    /// Invisible, and you always have every fighter.
    /// </summary>
    public class ModMenu : MonoBehaviour
    {
        static ModMenu instance;
        public static bool Showing => instance != null;

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
            instance = null;
            Destroy(menu.gameObject);
        }

        void OnDestroy()
        {
            // The panel rides on your rig (not under this object), so it has to be removed by hand.
            if (panel != null) Destroy(panel.gameObject);
            if (instance == this) instance = null;
        }

        Transform panel, listRoot;
        TextMeshPro details;
        Fighter selected;
        string armed;
        float armedUntil, nextRefresh;
        readonly List<Fighter> shown = new List<Fighter>();
        PokeButton invincibleButton, invisibleButton;

        void Start()
        {
            panel = new GameObject("ModPanel").transform;
            panel.localScale = Vector3.one * 0.85f;
            var rig = VRRig.Instance;
            if (rig != null && rig.head != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
                panel.SetParent(rig.transform, true);
                panel.position = rig.head.position + fwd * 1.2f - Vector3.up * 0.1f;
                panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            NBUI.Box(panel, "Backing", new Vector3(0f, -0.06f, 0.02f), new Vector3(1.2f, 0.9f, 0.02f), new Color(0.09f, 0.05f, 0.06f));
            NBUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, 0.39f, 0.015f), new Vector3(1.22f, 0.015f, 0.025f), NBUI.Glow(new Color(1f, 0.35f, 0.3f)));
            NBUI.Label(panel, "<b>MOD MENU</b>", new Vector3(-0.2f, 0.33f, 0f), 0.6f, new Color(1f, 0.55f, 0.45f), new Vector2(0.7f, 0.08f));
            PokeButton.Create(panel, "Close", "Close", new Vector3(0.48f, 0.33f, 0f), new Vector3(0.18f, 0.07f, 0.03f), new Color(0.6f, 0.6f, 0.7f), Close, 0.3f).cooldown = 0.8f;
            NBUI.Label(panel, "Players (press a name)", new Vector3(-0.33f, 0.27f, 0f), 0.2f, new Color(0.8f, 0.8f, 0.9f), new Vector2(0.46f, 0.04f));
            listRoot = new GameObject("Players").transform;
            listRoot.SetParent(panel, false);
            details = NBUI.Label(panel, "", new Vector3(0.28f, 0.16f, 0f), 0.24f, Color.white, new Vector2(0.58f, 0.24f), TextAlignmentOptions.TopLeft);
            details.enableWordWrapping = true;

            var size = new Vector3(0.27f, 0.07f, 0.03f);
            Action("Kick", "Kick", new Vector3(0.14f, -0.02f, 0f), size, new Color(1f, 0.8f, 0.4f), Moderation.Action.Kick, 0, false);
            Action("Mute", "Mute 15 min", new Vector3(0.43f, -0.02f, 0f), size, new Color(0.7f, 0.75f, 1f), Moderation.Action.Mute, 15, false);
            Action("Ban1", "Ban 1 day", new Vector3(0.14f, -0.1f, 0f), size, new Color(1f, 0.55f, 0.45f), Moderation.Action.Ban, 24, true);
            Action("Ban7", "Ban 7 days", new Vector3(0.43f, -0.1f, 0f), size, new Color(1f, 0.45f, 0.4f), Moderation.Action.Ban, 24 * 7, true);
            Action("BanForever", "Ban forever", new Vector3(0.14f, -0.18f, 0f), size, new Color(1f, 0.3f, 0.3f), Moderation.Action.Ban, 0, true);
            Action("Rename", "Force rename", new Vector3(0.43f, -0.18f, 0f), size, new Color(0.6f, 0.9f, 1f), Moderation.Action.Rename, 0, false);

            NBUI.Label(panel, "<b>YOUR POWERS</b>", new Vector3(-0.33f, -0.28f, 0f), 0.22f, new Color(1f, 0.85f, 0.4f), new Vector2(0.46f, 0.04f));
            var tool = new Vector3(0.37f, 0.08f, 0.03f);
            invincibleButton = PokeButton.Create(panel, "Invincible", "", new Vector3(-0.39f, -0.37f, 0f), tool, new Color(1f, 0.8f, 0.3f), () => Flip(ref ModPowers.Invincible, "Invincible", "Nothing can hurt you", "You can be hurt again"), 0.24f);
            invisibleButton = PokeButton.Create(panel, "Invisible", "", new Vector3(0f, -0.37f, 0f), tool, new Color(0.75f, 0.6f, 1f), () => Flip(ref ModPowers.Invisible, "Invisible", "Nobody else can see or hit you", "Everyone can see you again"), 0.24f);
            NBUI.Label(panel, "<b>Every fighter</b>\nunlocked (Menu > Fighter)", new Vector3(0.39f, -0.37f, 0f), 0.2f, new Color(0.6f, 1f, 0.7f), new Vector2(0.37f, 0.08f));
            invincibleButton.cooldown = invisibleButton.cooldown = 0.6f;
            RefreshTools();
        }

        void Flip(ref bool power, string what, string on, string off)
        {
            power = !power;
            AudioManager.Play(AudioManager.Sfx.Click, 1f, power ? 1.3f : 0.9f);
            SessionManager.Instance?.SetStatus(what + (power ? " ON: " + on : " OFF: " + off), 5f);
            RefreshTools();
        }

        void RefreshTools()
        {
            invincibleButton.SetLabel("Invincible: " + (ModPowers.Invincible ? "<b>ON</b>" : "off"));
            invisibleButton.SetLabel("Invisible: " + (ModPowers.Invisible ? "<b>ON</b>" : "off"));
        }

        void Action(string name, string label, Vector3 pos, Vector3 size, Color color, Moderation.Action action, int arg, bool confirm)
        {
            PokeButton.Create(panel, name, label, pos, size, color, () => Do(name, label, action, arg, confirm), 0.26f).cooldown = 0.6f;
        }

        void Do(string name, string label, Moderation.Action action, int arg, bool confirm)
        {
            if (selected == null || !selected.IsReady) { SessionManager.Instance?.SetStatus("Press a player's name first", 3f); return; }
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

            var others = Fighter.All.Where(f => f != null && f.IsReady && !f.IsBot && !f.IsMine).Take(8).ToList();
            if (!others.SequenceEqual(shown)) RebuildList(others);
            if (selected != null && !selected.IsReady) selected = null;

            if (selected == null) details.text = others.Count == 0 ? "Nobody else is in this room." : "Press a player's name on the left to see their ID.";
            else
            {
                string id = selected.PlayerId.ToString();
                details.text = "<b>" + selected.DisplayName + "</b>\nPlayer ID: <color=#ffd54a>" + id + "</color>\nHeadset: " + selected.DeviceHash +
                               "\nFighter: " + selected.Stats.name +
                               (!selected.Verified ? "\n<color=#ff8a65>(hasn't proved this ID yet)</color>" : "") +
                               (Moderation.IsMod(id) ? "\n<color=#9fe39f>(also a moderator)</color>" : "");
            }
        }

        void RebuildList(List<Fighter> others)
        {
            shown.Clear();
            shown.AddRange(others);
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            for (int i = 0; i < others.Count; i++)
            {
                var f = others[i];
                PokeButton.Create(listRoot, "Player" + i, f.DisplayName, new Vector3(-0.33f, 0.21f - i * 0.065f, 0f), new Vector3(0.46f, 0.058f, 0.03f),
                    new Color(0.5f, 0.6f, 0.75f), () => { selected = f; nextRefresh = 0f; }, 0.26f);
            }
        }
    }
}
