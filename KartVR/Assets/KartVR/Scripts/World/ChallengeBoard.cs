using System;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The DAILY CHALLENGES board on the garage wall: today's three challenges with a progress bar each, the XP
    /// they're worth, your level and streak, and how long until new ones. Builds itself at runtime; it faces this
    /// object's -Z (like the other signs).
    /// </summary>
    public class ChallengeBoard : MonoBehaviour
    {
        const float Width = 6.4f, Height = 3.4f, BarWidth = 3.6f;

        readonly TextMeshPro[] rows = new TextMeshPro[3], amounts = new TextMeshPro[3];
        readonly Transform[] fills = new Transform[3];
        readonly Renderer[] fillRenderers = new Renderer[3];
        TextMeshPro footer;
        Material barOn, barDone;
        float nextRefresh;

        static readonly Color Gold = new Color(1f, 0.84f, 0.29f), Green = new Color(0.49f, 1f, 0.54f);

        void Start()
        {
            KartUI.Box(transform, "Backing", new Vector3(0f, 0f, 0.08f), new Vector3(Width, Height, 0.12f), new Color(0.08f, 0.09f, 0.13f));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width + 0.1f, 0.08f, 0.08f), KartUI.Glow(Gold));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, -Height * 0.5f, 0f), new Vector3(Width + 0.1f, 0.08f, 0.08f), KartUI.Glow(Gold));
            KartUI.Label(transform, "<b>DAILY CHALLENGES</b>", new Vector3(0f, 1.3f, -0.03f), 4f, Gold, new Vector2(Width - 0.4f, 0.5f));

            barOn = KartUI.Glow(new Color(0.3f, 0.8f, 1f));
            barDone = KartUI.Glow(Green);
            var track = KartUI.Mat(new Color(0.2f, 0.21f, 0.27f), 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.62f - i * 0.82f;
                rows[i] = KartUI.Label(transform, "", new Vector3(0f, y, -0.03f), 2f, Color.white, new Vector2(Width - 0.5f, 0.35f), TextAlignmentOptions.Left);
                // Progress bar: a dark track and a glowing fill that grows from the left.
                float barX = -Width * 0.5f + 0.25f + BarWidth * 0.5f;
                KartUI.Part(transform, PrimitiveType.Cube, new Vector3(barX, y - 0.3f, -0.01f), new Vector3(BarWidth, 0.16f, 0.03f), track);
                var pivot = new GameObject("Fill" + i).transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = new Vector3(barX - BarWidth * 0.5f, y - 0.3f, -0.03f);
                var fill = KartUI.Part(pivot, PrimitiveType.Cube, new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 1f), barOn);
                fillRenderers[i] = fill.GetComponent<Renderer>();
                pivot.localScale = new Vector3(0.001f, 0.16f, 0.03f);
                fills[i] = pivot;
                amounts[i] = KartUI.Label(transform, "", new Vector3(barX + BarWidth * 0.5f + 1.15f, y - 0.3f, -0.03f), 1.8f, Gold, new Vector2(2.1f, 0.3f));
            }
            footer = KartUI.Label(transform, "", new Vector3(0f, -1.45f, -0.03f), 1.5f, new Color(0.8f, 0.95f, 1f), new Vector2(Width - 0.4f, 0.25f));
        }

        void Update()
        {
            if (footer == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            for (int i = 0; i < 3; i++)
            {
                string text = Progress.ChallengeText(i, out float p, out float target, out bool done, out int xp);
                float t = target > 0f ? Mathf.Clamp01(p / target) : 0f;
                if (done) t = 1f;
                rows[i].text = done ? "<color=#7CFF8A>DONE!  " + text + "</color>" : text;
                fills[i].localScale = new Vector3(Mathf.Max(0.001f, BarWidth * t), 0.16f, 0.03f);
                fillRenderers[i].sharedMaterial = done ? barDone : barOn;
                amounts[i].text = (done ? "" : Mathf.FloorToInt(Mathf.Min(p, target)) + "/" + target + "   ") + "+" + xp + " XP";
            }
            var left = DateTime.Today.AddDays(1) - DateTime.Now;
            footer.text = "Level " + Progress.Level + "   Daily streak: " + Mathf.Max(1, Progress.Streak) + " day(s)   New challenges in " +
                          (int)left.TotalHours + "h " + left.Minutes.ToString("00") + "m";
        }
    }
}
