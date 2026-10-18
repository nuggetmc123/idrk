using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The fun bits of progress you see: "+25 XP" pop-ups, a big LEVEL UP with confetti and what you unlocked,
    /// challenge-complete and new-record banners, and a welcome screen with your streak and today's challenges.
    /// </summary>
    public class ProgressFx : MonoBehaviour
    {
        class Popup { public TextMeshPro text; public float born; }
        readonly List<Popup> popups = new List<Popup>();
        Transform popupRoot;

        Transform banner;
        TextMeshPro bannerText;
        float bannerUntil;
        readonly Queue<string> bannerQueue = new Queue<string>();
        bool welcomed;

        void Start()
        {
            Progress.XpGained += OnXp;
            Progress.LeveledUp += OnLevel;
            Progress.ChallengeDone += c => Show("<size=55%>DAILY CHALLENGE COMPLETE!</size>\n<b>" + c + "</b>", AudioManager.Sfx.Lap);
            Progress.Record += r => Show("<b>" + r + "</b>", AudioManager.Sfx.Lap);
        }

        void OnXp(int xp, string why)
        {
            if (why == null) return;
            var rig = KartRig.Instance;
            if (rig == null || rig.head == null) return;
            if (popupRoot == null) { popupRoot = new GameObject("XpPopups").transform; DontDestroyOnLoad(popupRoot.gameObject); }
            var t = KartUI.Label(popupRoot, "<b>+" + xp + " XP</b>  <size=70%>" + why + "</size>", Vector3.zero, 0.55f, new Color(0.55f, 1f, 0.6f), new Vector2(2f, 0.2f));
            popups.Add(new Popup { text = t, born = Time.time });
            AudioManager.Play(AudioManager.Sfx.Checkpoint, 0.35f, 1.6f);
        }

        void OnLevel(int level)
        {
            var sb = new StringBuilder("<size=55%>LEVEL UP!</size>\n<b>LEVEL " + level + "</b>");
            for (int i = 0; i < KartParts.UnderglowLevel.Length; i++)
                if (KartParts.UnderglowLevel[i] == level && i > 0) sb.Append("\n<size=45%>Unlocked: ").Append(KartParts.Name((KartUnderglow)i)).Append("!</size>");
            for (int i = 0; i < KartParts.TrailLevel.Length; i++)
                if (KartParts.TrailLevel[i] == level && i > 0) sb.Append("\n<size=45%>Unlocked: ").Append(KartParts.Name((KartTrail)i)).Append("!</size>");
            Show(sb.ToString(), AudioManager.Sfx.Finish);
            var rig = KartRig.Instance;
            if (rig != null && rig.head != null) KartFx.Confetti(rig.head.position + rig.head.forward * 1.5f, 1.2f);
        }

        void Show(string text, AudioManager.Sfx sound)
        {
            bannerQueue.Enqueue(text);
            AudioManager.Play(sound);
        }

        void Update()
        {
            var rig = KartRig.Instance;
            if (rig == null || rig.head == null) return;

            // Welcome screen, once per launch, when you first arrive in the hub.
            if (!welcomed && GameState.CurrentMode == KartMode.Hub && PlayerAvatar.Local != null && PlayerProfile.NameLocked && !NameEntry.Showing)
            {
                welcomed = true;
                Progress.CheckDaily();
                var sb = new StringBuilder("<size=50%>WELCOME BACK</size>\n<b>" + PlayerProfile.Nickname.ToUpper() + "</b>\n<size=45%>Level " + Progress.Level);
                if (Progress.Streak > 1) sb.Append("   <color=#ffb347>" + Progress.Streak + " day streak!</color>");
                sb.Append("\nToday's challenges are on the board and in Menu > Profile</size>");
                bannerQueue.Enqueue(sb.ToString());
                KartFx.Confetti(rig.head.position + rig.head.forward * 2f + Vector3.up * 0.5f, 1f);
            }

            // Pop-ups rise and fade in front of you.
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            var face = Quaternion.LookRotation(fwd, Vector3.up);
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.time - p.born;
                if (p.text == null || age > 2.2f)
                {
                    if (p.text != null) Destroy(p.text.gameObject);
                    popups.RemoveAt(i);
                    continue;
                }
                int slot = popups.Count - 1 - i;
                p.text.transform.SetPositionAndRotation(rig.head.position + fwd * 1.6f + Vector3.up * (-0.35f + age * 0.08f + slot * 0.12f), face);
                var c = p.text.color;
                c.a = Mathf.Clamp01(2.2f - age);
                p.text.color = c;
            }

            // Banners, one at a time.
            if (Time.time >= bannerUntil && bannerQueue.Count > 0)
            {
                if (banner == null)
                {
                    banner = new GameObject("ProgressBanner").transform;
                    DontDestroyOnLoad(banner.gameObject);
                    KartUI.Box(banner, "Back", new Vector3(0f, 0f, 0.03f), new Vector3(2.4f, 1f, 0.02f), new Color(0.05f, 0.06f, 0.1f));
                    KartUI.Part(banner, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.02f), new Vector3(2.42f, 0.03f, 0.03f), KartUI.Glow(new Color(1f, 0.8f, 0.25f)));
                    KartUI.Part(banner, PrimitiveType.Cube, new Vector3(0f, -0.5f, 0.02f), new Vector3(2.42f, 0.03f, 0.03f), KartUI.Glow(new Color(0.3f, 0.9f, 1f)));
                    bannerText = KartUI.Label(banner, "", Vector3.zero, 1.6f, new Color(1f, 0.92f, 0.5f), new Vector2(2.3f, 0.95f));
                }
                bannerText.text = bannerQueue.Dequeue();
                bannerUntil = Time.time + 4.5f;
            }
            if (banner != null)
            {
                bool show = Time.time < bannerUntil;
                banner.gameObject.SetActive(show);
                if (show)
                {
                    float pop = Mathf.Clamp01((Time.time - (bannerUntil - 4.5f)) * 5f); // pops in
                    banner.localScale = Vector3.one * Mathf.Lerp(0.6f, 0.8f, pop);
                    banner.SetPositionAndRotation(rig.head.position + fwd * 2.4f + Vector3.up * 0.35f, face);
                }
            }
        }
    }
}
