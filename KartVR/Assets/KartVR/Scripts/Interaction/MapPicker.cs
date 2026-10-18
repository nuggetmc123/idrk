using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The big map board on the hub wall: poke a map to go there. In public play
    /// it takes you to that map on your server; in a private lobby it takes the
    /// whole lobby. Builds itself at runtime; it faces this object's -Z.
    /// </summary>
    public class MapPicker : MonoBehaviour
    {
        struct Entry
        {
            public KartMode mode;
            public string title, blurb;
            public Color color;
        }

        static readonly Entry[] Maps =
        {
            new Entry { mode = KartMode.Race, title = "RACE", blurb = "3 laps round the meadow track", color = new Color(1f, 0.45f, 0.25f) },
            new Entry { mode = KartMode.CanyonRace, title = "CANYON RACE", blurb = "Desert canyon circuit", color = new Color(1f, 0.7f, 0.25f) },
            new Entry { mode = KartMode.Bumper, title = "BUMPER KARTS", blurb = "Knock them into the lava", color = new Color(0.35f, 0.6f, 1f) },
            new Entry { mode = KartMode.IceBumper, title = "ICE BUMPER", blurb = "Slippery rink, snowmen", color = new Color(0.65f, 0.92f, 1f) },
            new Entry { mode = KartMode.Tag, title = "TAG", blurb = "Bump someone to make them IT", color = new Color(1f, 0.4f, 0.45f) },
            new Entry { mode = KartMode.FreeRoam, title = "FREE ROAM", blurb = "Big open world + BUILD MODE", color = new Color(0.45f, 1f, 0.5f) },
            new Entry { mode = KartMode.Tutorial, title = "TUTORIAL", blurb = "Learn to drive + build (solo)", color = new Color(1f, 0.85f, 0.35f) },
        };

        TextMeshPro footer;
        float nextRefresh, busyUntil;

        void Start()
        {
            // One row of big buttons at hand height (the board's centre should be about 2 m off the floor).
            const float w = 10.8f, h = 3f;
            KartUI.Box(transform, "Backing", new Vector3(0f, 0f, 0.1f), new Vector3(w, h, 0.15f), new Color(0.08f, 0.09f, 0.13f));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, h * 0.5f, 0f), new Vector3(w + 0.1f, 0.08f, 0.08f), KartUI.Glow(new Color(0.3f, 0.9f, 1f)));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, -h * 0.5f, 0f), new Vector3(w + 0.1f, 0.08f, 0.08f), KartUI.Glow(new Color(1f, 0.3f, 0.8f)));
            KartUI.Label(transform, "<b>PICK A MAP</b>", new Vector3(0f, 1.05f, -0.03f), 4.5f, Color.white, new Vector2(10f, 0.55f));

            float spacing = 10.4f / Maps.Length;
            var size = new Vector3(spacing - 0.1f, 1.15f, 0.12f);
            for (int i = 0; i < Maps.Length; i++)
            {
                var e = Maps[i];
                var mode = e.mode;
                var button = PokeButton.Create(transform, e.title, "<b>" + e.title + "</b>\n<size=55%>" + e.blurb + "</size>",
                    new Vector3((i - (Maps.Length - 1) * 0.5f) * spacing, -0.2f, -0.03f), size, e.color, () => Go(mode), 1.65f);
                button.cooldown = 1f;
            }
            footer = KartUI.Label(transform, "", new Vector3(0f, -1.2f, -0.03f), 1.5f, new Color(0.8f, 0.95f, 1f), new Vector2(10f, 0.3f));
        }

        void Go(KartMode mode)
        {
            if (Time.time < busyUntil) return;
            if (mode == KartMode.Tutorial)
            {
                // The tutorial is always solo, in its own session.
                var s = SessionManager.Instance;
                if (s == null || s.Busy) return;
                busyUntil = Time.time + 3f;
                AudioManager.Play(AudioManager.Sfx.Teleport);
                s.StartTutorial();
                return;
            }
            if (ModePortal.Travel(mode))
            {
                busyUntil = Time.time + 3f;
                SessionManager.Instance?.SetStatus("Going to " + KartModeInfo.DisplayName(mode) + "...", 4f);
            }
        }

        void Update()
        {
            if (footer == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            var s = SessionManager.Instance;
            if (s == null) return;
            footer.text = s.IsSolo ? "Playing solo (offline): touch a map to go there"
                : s.IsPrivate
                ? "Private lobby: picking a map takes your whole lobby there"
                : "Touch a map to go there (you'll play with people on server " + s.ServerNumber + ")";
        }
    }
}
