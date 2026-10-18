using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The big map board on the hub wall. First pick a kind of game (Race, Bumper Karts, Tag, Free Roam or the
    /// Tutorial), then the board shows every map of that kind: poke one to go there. In public play you're put in
    /// a room on that map; in a private lobby you go there and stay in your lobby. Builds itself at runtime; it
    /// faces this object's -Z.
    /// </summary>
    public class MapPicker : MonoBehaviour
    {
        struct Entry
        {
            public KartMode mode;
            public string title, blurb;
            public Color color;
        }

        struct Category
        {
            public string title, blurb;
            public Color color;
            public Entry[] maps;
        }

        static readonly Category[] Categories =
        {
            new Category { title = "RACE", blurb = "3 laps, first one home wins", color = new Color(1f, 0.5f, 0.25f), maps = new[]
            {
                new Entry { mode = KartMode.Race, title = "MEADOW RACE", blurb = "Grandstands and green hills", color = new Color(1f, 0.45f, 0.25f) },
                new Entry { mode = KartMode.CanyonRace, title = "CANYON RACE", blurb = "Desert canyon circuit", color = new Color(1f, 0.7f, 0.25f) },
                new Entry { mode = KartMode.CityRace, title = "CITY RACE", blurb = "Night streets, neon towers", color = new Color(1f, 0.55f, 0.75f) },
            } },
            new Category { title = "BUMPER KARTS", blurb = "Knock everyone off", color = new Color(0.35f, 0.6f, 1f), maps = new[]
            {
                new Entry { mode = KartMode.Bumper, title = "LAVA ARENA", blurb = "Knock them into the lava", color = new Color(0.35f, 0.6f, 1f) },
                new Entry { mode = KartMode.IceBumper, title = "ICE BUMPER", blurb = "Slippery rink, snowmen", color = new Color(0.65f, 0.92f, 1f) },
                new Entry { mode = KartMode.RooftopBumper, title = "ROOFTOP BUMPER", blurb = "Don't fall off the skyscraper", color = new Color(0.55f, 0.75f, 1f) },
            } },
            new Category { title = "TAG", blurb = "Bump someone to make them IT", color = new Color(1f, 0.4f, 0.45f), maps = new[]
            {
                new Entry { mode = KartMode.Tag, title = "NEON ARENA", blurb = "Pillars, walls and ramps", color = new Color(1f, 0.4f, 0.45f) },
                new Entry { mode = KartMode.GraveyardTag, title = "GRAVEYARD TAG", blurb = "Spooky tag among the tombstones", color = new Color(0.75f, 0.55f, 1f) },
                new Entry { mode = KartMode.Backrooms, title = "THE BACKROOMS", blurb = "Endless yellow rooms. Tag + build", color = new Color(0.95f, 0.88f, 0.5f) },
            } },
            new Category { title = "FREE ROAM", blurb = "Drive anywhere + BUILD", color = new Color(0.45f, 1f, 0.5f), maps = new[]
            {
                new Entry { mode = KartMode.FreeRoam, title = "BIG WORLD", blurb = "Stunt park, hills, lake + BUILD", color = new Color(0.45f, 1f, 0.5f) },
                new Entry { mode = KartMode.IslandFreeRoam, title = "ISLAND", blurb = "Tropical island + BUILD", color = new Color(0.4f, 0.95f, 0.85f) },
            } },
            new Category { title = "TUTORIAL", blurb = "Learn to drive + build (solo)", color = new Color(1f, 0.85f, 0.35f), maps = new[]
            {
                new Entry { mode = KartMode.Tutorial, title = "TUTORIAL", blurb = "", color = new Color(1f, 0.85f, 0.35f) },
            } },
        };

        const float Width = 10.8f, Height = 3f;

        TextMeshPro footer, heading;
        Transform page;
        float nextRefresh, busyUntil, pageShownAt;
        bool onMapsPage;

        void Start()
        {
            KartUI.Box(transform, "Backing", new Vector3(0f, 0f, 0.1f), new Vector3(Width, Height, 0.15f), new Color(0.08f, 0.09f, 0.13f));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width + 0.1f, 0.08f, 0.08f), KartUI.Glow(new Color(0.3f, 0.9f, 1f)));
            KartUI.Part(transform, PrimitiveType.Cube, new Vector3(0f, -Height * 0.5f, 0f), new Vector3(Width + 0.1f, 0.08f, 0.08f), KartUI.Glow(new Color(1f, 0.3f, 0.8f)));
            heading = KartUI.Label(transform, "", new Vector3(0f, 1.05f, -0.03f), 4.5f, Color.white, new Vector2(10f, 0.55f));
            footer = KartUI.Label(transform, "", new Vector3(0f, -1.3f, -0.03f), 1.4f, new Color(0.8f, 0.95f, 1f), new Vector2(10f, 0.25f));
            ShowCategories();
        }

        Transform NewPage()
        {
            if (page != null) Destroy(page.gameObject);
            page = new GameObject("Page").transform;
            page.SetParent(transform, false);
            pageShownAt = Time.time;
            return page;
        }

        /// <summary>The first page: one big button per kind of game.</summary>
        void ShowCategories()
        {
            var p = NewPage();
            heading.text = "<b>PICK A GAME</b>";
            onMapsPage = false;
            float spacing = 10.4f / Categories.Length;
            var size = new Vector3(spacing - 0.12f, 1.75f, 0.12f);
            for (int i = 0; i < Categories.Length; i++)
            {
                var c = Categories[i];
                int index = i;
                string text = "<b>" + c.title + "</b>\n<size=50%>" + c.blurb + "\n" +
                              (c.maps.Length > 1 ? c.maps.Length + " maps" : "") + "</size>";
                var button = PokeButton.Create(p, c.title, text, new Vector3((i - (Categories.Length - 1) * 0.5f) * spacing, -0.05f, -0.03f), size, c.color,
                    () => Pick(index), 2f);
                button.cooldown = 0.8f;
            }
        }

        void Pick(int index)
        {
            if (Time.time < pageShownAt + 0.6f) return; // the finger that changed page shouldn't press a button straight away
            var c = Categories[index];
            if (c.maps.Length == 1) { Go(c.maps[0].mode); return; }
            AudioManager.Play(AudioManager.Sfx.Click);
            ShowMaps(c);
        }

        /// <summary>The second page: every map of one kind, plus BACK.</summary>
        void ShowMaps(Category c)
        {
            var p = NewPage();
            heading.text = "<b>" + c.title + " MAPS</b>";
            onMapsPage = true;
            int n = c.maps.Length;
            const float backWidth = 1.6f;
            float spacing = (10.4f - backWidth - 0.2f) / n;
            var size = new Vector3(spacing - 0.15f, 1.75f, 0.12f);
            float left = -5.2f + backWidth + 0.2f;
            for (int i = 0; i < n; i++)
            {
                var e = c.maps[i];
                var mode = e.mode;
                var button = PokeButton.Create(p, e.title, "<b>" + e.title + "</b>\n<size=55%>" + e.blurb + "</size>",
                    new Vector3(left + (i + 0.5f) * spacing, -0.05f, -0.03f), size, e.color, () =>
                    {
                        if (Time.time < pageShownAt + 0.6f) return;
                        Go(mode);
                    }, 1.7f);
                button.cooldown = 1f;
            }
            PokeButton.Create(p, "Back", "<b>< BACK</b>", new Vector3(-5.2f + backWidth * 0.5f, -0.05f, -0.03f), new Vector3(backWidth, 1.75f, 0.12f),
                new Color(0.6f, 0.65f, 0.75f), () =>
                {
                    if (Time.time < pageShownAt + 0.6f) return;
                    AudioManager.Play(AudioManager.Sfx.Click);
                    ShowCategories();
                }, 1.6f);
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
            if (onMapsPage && Time.time - pageShownAt > 30f) ShowCategories(); // left on a map list: back to the start for the next person
            var s = SessionManager.Instance;
            if (s == null) return;
            footer.text = s.IsSolo ? "Playing solo (offline): touch a map to go there"
                : s.IsPrivate
                ? "Private lobby " + KartUI.FormatCode(s.LobbyCode) + ": go to any map and you stay in your lobby"
                : "Touch a map to go there (you'll be put in a room with other players)";
        }
    }
}
