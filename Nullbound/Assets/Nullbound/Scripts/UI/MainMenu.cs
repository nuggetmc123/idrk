using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// The main menu: a room you stand in when the game starts (and after leaving a match).
    /// Middle: PLAY (Quick Match, private lobby, join a code, Training Grounds), your name, Player ID and coins.
    /// Left: every fighter (pick one you own; locked ones show as a dark shape) with a 3D preview.
    /// Right: crates you open with coins to unlock more fighters.
    /// The first time, you have to pick your name before anything else.
    /// Built at runtime; the player stands at this object's position facing +Z.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }

        TextMeshPro status, who, coinsText, fighterText, crateText, codeText;
        Transform playPage, codePage, previewSpot, crateSpot, crateBox, revealSpot;
        PokeButton modButton;
        PokeButton[] fighterButtons;
        FighterModel preview, reveal;
        int previewId = -1, previewShownAs = -1;
        bool previewLocked;
        float nextRefresh, crateAnimUntil = -1f, revealUntil = -1f;
        int pendingCrate = -1;
        string typed = "";

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            Collection.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }

        /// <summary>Where the player stands in the menu.</summary>
        public Vector3 StandPoint => transform.position;
        public Quaternion StandRotation => transform.rotation;

        void Start()
        {
            BuildCenter();
            BuildFighters();
            BuildCrates();
            Collection.Changed += Refresh;
            Refresh();
        }

        // ================================================================== layout

        Transform Panel(string name, Vector3 pos, float yaw, Vector2 size, Color trim)
        {
            var p = new GameObject(name).transform;
            p.SetParent(transform, false);
            p.localPosition = pos;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // A lab console: gunmetal frame, a dark glassy screen, a tinted header band behind the title, glowing top
            // and bottom trims and corner brackets in the panel's colour.
            var screen = new Color(0.035f, 0.04f, 0.05f);
            NBUI.Part(p, PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(size.x + 0.1f, size.y + 0.1f, 0.06f), NBUI.Mat(new Color(0.17f, 0.18f, 0.2f), 0.45f, 0.6f), default, "Frame");
            NBUI.Part(p, PrimitiveType.Cube, new Vector3(0f, 0f, 0.06f), new Vector3(size.x, size.y, 0.06f), NBUI.Mat(screen, 0.85f), default, "Backing");
            NBUI.Part(p, PrimitiveType.Cube, new Vector3(0f, size.y * 0.5f - 0.1f, 0.025f), new Vector3(size.x - 0.06f, 0.16f, 0.01f), NBUI.Mat(Color.Lerp(screen, trim, 0.16f), 0.6f), default, "Header");
            NBUI.Part(p, PrimitiveType.Cube, new Vector3(0f, size.y * 0.5f, 0.02f), new Vector3(size.x + 0.04f, 0.025f, 0.03f), NBUI.Glow(trim));
            NBUI.Part(p, PrimitiveType.Cube, new Vector3(0f, -size.y * 0.5f, 0.02f), new Vector3(size.x + 0.04f, 0.025f, 0.03f), NBUI.Glow(trim));
            var bracket = NBUI.Glow(Color.Lerp(trim, Color.white, 0.25f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector3 c = new Vector3(sx * (size.x * 0.5f + 0.035f), sy * (size.y * 0.5f + 0.035f), 0.05f);
                    NBUI.Part(p, PrimitiveType.Cube, c - new Vector3(sx * 0.06f, 0f, 0f), new Vector3(0.14f, 0.02f, 0.02f), bracket);
                    NBUI.Part(p, PrimitiveType.Cube, c - new Vector3(0f, sy * 0.06f, 0f), new Vector3(0.02f, 0.14f, 0.02f), bracket);
                }
            return p;
        }

        bool Ready()
        {
            if (PlayerProfile.NameLocked) return true;
            AudioManager.Play(AudioManager.Sfx.Error);
            Status("Pick your name first (the keyboard in front of you)");
            return false;
        }

        void Status(string text) { if (status != null) status.text = text; nextRefresh = Time.time + 4f; }

        void BuildCenter()
        {
            var p = Panel("PlayPanel", new Vector3(0f, 1.55f, 2.6f), 0f, new Vector2(1.7f, 1.45f), new Color(1f, 0.35f, 0.2f));
            // The title: one line (it used to be too wide for its box and wrapped into "NULLBOUN / D").
            var title = NBUI.Fit(NBUI.Label(p, "<b>NULLBOUND</b>", new Vector3(0f, 0.6f, -0.01f), 2.4f, new Color(1f, 0.45f, 0.3f), new Vector2(1.6f, 0.24f)), 1f);
            title.characterSpacing = 6f;
            title.outlineWidth = 0.15f;
            title.outlineColor = new Color32(40, 8, 0, 255);
            who = NBUI.Label(p, "", new Vector3(0f, 0.43f, 0f), 0.75f, Color.white, new Vector2(1.6f, 0.12f));
            coinsText = NBUI.Label(p, "", new Vector3(0f, 0.33f, 0f), 0.75f, new Color(1f, 0.85f, 0.3f), new Vector2(1.6f, 0.1f));

            playPage = new GameObject("Play").transform;
            playPage.SetParent(p, false);
            var big = new Vector3(0.78f, 0.17f, 0.04f);
            Btn(playPage, "QuickMatch", "<b>QUICK MATCH</b>\n<size=60%>the Battleground, online</size>", new Vector3(-0.41f, 0.15f, 0f), big, new Color(1f, 0.5f, 0.3f), () => { if (Ready()) SessionManager.Instance?.JoinPublic(); });
            Btn(playPage, "Training", "<b>TRAINING GROUNDS</b>\n<size=60%>you vs bots, dummies</size>", new Vector3(0.41f, 0.15f, 0f), big, new Color(0.45f, 1f, 0.55f), () => { if (Ready()) SessionManager.Instance?.StartTraining(); });
            Btn(playPage, "Private", "<b>PRIVATE LOBBY</b>\n<size=60%>Battleground with friends</size>", new Vector3(-0.41f, -0.05f, 0f), big, new Color(1f, 0.55f, 0.9f), () => { if (Ready()) SessionManager.Instance?.CreatePrivateLobby(); });
            Btn(playPage, "JoinCode", "<b>JOIN A CODE</b>\n<size=60%>a friend's room or lobby</size>", new Vector3(0.41f, -0.05f, 0f), big, new Color(0.55f, 0.75f, 1f), () => { if (Ready()) { typed = ""; ShowCode(true); } });
            modButton = Btn(playPage, "ModMenu", "<b>MOD MENU</b>", new Vector3(0f, -0.23f, 0f), new Vector3(0.5f, 0.1f, 0.04f), new Color(1f, 0.4f, 0.35f), ModMenu.Toggle);
            status = NBUI.Label(p, "", new Vector3(0f, -0.38f, 0f), 0.55f, new Color(0.85f, 0.92f, 1f), new Vector2(1.6f, 0.1f));
            NBUI.Label(p, "Settings: press the MENU button (three lines) on your left controller", new Vector3(0f, -0.6f, 0f), 0.42f, new Color(1f, 1f, 1f, 0.6f), new Vector2(1.6f, 0.08f));

            codePage = new GameObject("Code").transform;
            codePage.SetParent(p, false);
            codeText = NBUI.Label(codePage, "", new Vector3(0f, 0.2f, 0f), 1.1f, new Color(0.4f, 1f, 0.5f), new Vector2(1.4f, 0.14f));
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "DEL", "0", "JOIN" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                var pos = new Vector3(((i % 3) - 1) * 0.3f, 0.06f - (i / 3) * 0.1f, 0f);
                Btn(codePage, "Key" + k, k, pos, new Vector3(0.27f, 0.085f, 0.04f), k == "JOIN" ? new Color(0.4f, 1f, 0.5f) : k == "DEL" ? new Color(1f, 0.6f, 0.5f) : new Color(0.85f, 0.85f, 0.9f), () => Key(k), 0.6f);
            }
            Btn(codePage, "Back", "Back", new Vector3(0.6f, 0.2f, 0f), new Vector3(0.22f, 0.08f, 0.04f), new Color(0.5f, 0.55f, 0.65f), () => ShowCode(false), 0.5f);
            ShowCode(false);
        }

        void ShowCode(bool show)
        {
            playPage.gameObject.SetActive(!show);
            codePage.gameObject.SetActive(show);
            nextRefresh = 0f;
        }

        void Key(string k)
        {
            if (k == "DEL") { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); }
            else if (k == "JOIN")
            {
                if (typed.Length == 6) { SessionManager.Instance?.JoinCode(typed); ShowCode(false); }
                else AudioManager.Play(AudioManager.Sfx.Error);
            }
            else if (typed.Length < 6) typed += k;
            nextRefresh = 0f;
        }

        static PokeButton Btn(Transform parent, string name, string text, Vector3 pos, Vector3 size, Color color, System.Action onPress, float font = 0.7f)
        {
            var b = PokeButton.Create(parent, name, text, pos, size, color, onPress, font);
            b.cooldown = 0.8f;
            return b;
        }

        void BuildFighters()
        {
            var p = Panel("FightersPanel", new Vector3(-2.3f, 1.55f, 1.55f), -55f, new Vector2(1.7f, 1.45f), new Color(0.4f, 0.8f, 1f));
            NBUI.Label(p, "<b>FIGHTERS</b>", new Vector3(0f, 0.63f, 0f), 1.4f, Color.white, new Vector2(1.6f, 0.14f));
            int n = FighterInfo.Count, cols = 6;
            fighterButtons = new PokeButton[n];
            var size = new Vector3(0.255f, 0.07f, 0.03f);
            for (int i = 0; i < n; i++)
            {
                int id = i;
                int row = i / cols, col = i % cols;
                var pos = new Vector3((col - (cols - 1) * 0.5f) * 0.268f, 0.52f - row * 0.078f, 0f);
                fighterButtons[i] = PokeButton.Create(p, "F" + i, "", pos, size, new Color(0.3f, 0.32f, 0.38f), () => PickFighter(id), 0.42f);
                fighterButtons[i].cooldown = 0.3f;
            }
            fighterText = NBUI.Label(p, "", new Vector3(0f, -0.58f, 0f), 0.5f, Color.white, new Vector2(1.6f, 0.24f));
            // The preview stands between this panel and the play panel.
            previewSpot = new GameObject("PreviewSpot").transform;
            previewSpot.SetParent(transform, false);
            previewSpot.localPosition = new Vector3(-1.25f, 0.1f, 2.3f);
            NBUI.Part(previewSpot, PrimitiveType.Cylinder, Vector3.down * 0.05f, new Vector3(0.9f, 0.05f, 0.9f), NBUI.Glow(new Color(0.3f, 0.7f, 1f)));
        }

        void PickFighter(int id)
        {
            previewId = id;
            if (!Collection.IsUnlocked(id))
            {
                AudioManager.Play(AudioManager.Sfx.Error);
                Status(FighterInfo.Get(id).name + " is locked: open crates (on your right) to unlock fighters");
                return;
            }
            GameSettings.Fighter = (FighterKind)id;
            AudioManager.Play(AudioManager.Sfx.Ready);
            Refresh();
        }

        void BuildCrates()
        {
            var p = Panel("CratesPanel", new Vector3(2.3f, 1.55f, 1.55f), 55f, new Vector2(1.7f, 1.45f), new Color(1f, 0.75f, 0.2f));
            NBUI.Label(p, "<b>CRATES</b>", new Vector3(0f, 0.63f, 0f), 1.4f, Color.white, new Vector2(1.6f, 0.14f));
            for (int i = 0; i < Collection.Crates.Length; i++)
            {
                int index = i;
                var c = Collection.Crates[i];
                Btn(p, "Crate" + i, "<b>OPEN " + c.name + "</b>\n<size=70%>" + c.cost + " coins</size>", new Vector3(0f, 0.38f - i * 0.22f, 0f), new Vector3(1.2f, 0.18f, 0.04f), c.color, () => OpenCrate(index), 0.75f);
            }
            var odds = new System.Text.StringBuilder("<b>Odds</b>\n");
            foreach (var c in Collection.Crates)
            {
                odds.Append(c.name).Append(": ");
                for (int r = 0; r < c.odds.Length; r++)
                    if (c.odds[r] > 0f) odds.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(FighterInfo.RarityColor((Rarity)r))).Append(">")
                        .Append(FighterInfo.RarityName((Rarity)r)).Append(" ").Append(c.odds[r]).Append("%</color>  ");
                odds.Append('\n');
            }
            odds.Append("<size=85%>Each crate gives a fighter you don't have yet. Earn " + Collection.CoinsPerKO + " coins for every player you knock out.\nAll fighters are balanced: rarer ones look cooler, they aren't stronger.</size>");
            NBUI.Label(p, odds.ToString(), new Vector3(0f, -0.2f, 0f), 0.4f, Color.white, new Vector2(1.55f, 0.4f));
            crateText = NBUI.Label(p, "", new Vector3(0f, -0.56f, 0f), 0.6f, new Color(1f, 0.85f, 0.3f), new Vector2(1.6f, 0.2f));

            crateSpot = new GameObject("CrateSpot").transform;
            crateSpot.SetParent(transform, false);
            crateSpot.localPosition = new Vector3(1.25f, 0.1f, 2.3f);
            NBUI.Part(crateSpot, PrimitiveType.Cylinder, Vector3.down * 0.05f, new Vector3(0.9f, 0.05f, 0.9f), NBUI.Glow(new Color(1f, 0.7f, 0.2f)));
            crateBox = new GameObject("Crate").transform;
            crateBox.SetParent(crateSpot, false);
            crateBox.localPosition = new Vector3(0f, 0.35f, 0f);
            NBUI.Part(crateBox, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.6f, NBUI.Mat(new Color(0.15f, 0.15f, 0.2f), 0.5f, 0.3f));
            var edge = NBUI.Glow(new Color(1f, 0.7f, 0.2f));
            foreach (var e in new[] { new Vector3(0.3f, 0f, 0.3f), new Vector3(-0.3f, 0f, 0.3f), new Vector3(0.3f, 0f, -0.3f), new Vector3(-0.3f, 0f, -0.3f) })
                NBUI.Part(crateBox, PrimitiveType.Cube, e, new Vector3(0.04f, 0.62f, 0.04f), edge);
            NBUI.Part(crateBox, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.04f, 0.62f), edge);
            NBUI.Label(crateBox, "?", new Vector3(0f, 0f, -0.31f), 4f, new Color(1f, 0.8f, 0.3f), new Vector2(0.5f, 0.5f));
            revealSpot = new GameObject("Reveal").transform;
            revealSpot.SetParent(crateSpot, false);
        }

        void OpenCrate(int index)
        {
            if (!Ready() || crateAnimUntil > Time.time || revealUntil > Time.time) return;
            var c = Collection.Crates[index];
            if (Collection.Coins < c.cost)
            {
                AudioManager.Play(AudioManager.Sfx.Error);
                crateText.text = "Not enough coins (" + c.cost + "). Knock out players to earn " + Collection.CoinsPerKO + " each!";
                return;
            }
            pendingCrate = index;
            crateAnimUntil = Time.time + 1.3f;
            crateText.text = "Opening...";
            AudioManager.Play(AudioManager.Sfx.Whoosh, 1f, 0.6f);
        }

        void FinishCrate()
        {
            var result = Collection.Open(pendingCrate);
            pendingCrate = -1;
            crateBox.gameObject.SetActive(false);
            Vector3 at = crateSpot.position + Vector3.up * 0.4f;
            if (!result.opened) { crateText.text = result.problem; crateBox.gameObject.SetActive(true); return; }
            var color = FighterInfo.RarityColor(result.rarity);
            Fx.Shockwave(crateSpot.position, 1.5f, color);
            Fx.Impact(at, Vector3.up, color, true, false);
            AudioManager.Play(result.rarity >= Rarity.Legendary ? AudioManager.Sfx.KO : AudioManager.Sfx.Respawn);
            revealUntil = Time.time + 7f;
            if (reveal != null && reveal.root != null) { Destroy(reveal.root.gameObject); Destroy(reveal.leftGlove.gameObject); Destroy(reveal.rightGlove.gameObject); }
            reveal = null;
            if (result.fighter < 0)
            {
                crateText.text = "You already have every " + FighterInfo.RarityName(result.rarity) + " fighter! +" + result.refund + " coins";
                return;
            }
            var st = FighterInfo.Get(result.fighter);
            reveal = FighterModel.Build(revealSpot, st);
            // A beam of the rarity's colour.
            var beam = NBUI.Part(crateSpot, PrimitiveType.Cylinder, Vector3.up * 2f, new Vector3(0.7f, 2f, 0.7f), NBUI.Glow(color * 0.5f), default, "RarityBeam");
            var life = beam.gameObject.AddComponent<FxLife>();
            life.life = 2.5f;
            life.startScale = new Vector3(0.7f, 2f, 0.7f);
            life.endScale = new Vector3(0.1f, 2f, 0.1f);
            crateText.text = "<b>NEW FIGHTER!</b>\n<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + st.name + "  (" + FighterInfo.RarityName(st.rarity) + ")</color>";
            previewId = result.fighter;
            GameSettings.Fighter = (FighterKind)result.fighter;
            Refresh();
        }

        // ================================================================== updates

        void Refresh()
        {
            if (fighterButtons == null) return;
            int chosen = (int)GameSettings.Fighter;
            for (int i = 0; i < fighterButtons.Length; i++)
            {
                var st = FighterInfo.Get(i);
                bool owned = Collection.IsUnlocked(i);
                var rc = FighterInfo.RarityColor(st.rarity);
                fighterButtons[i].SetLabel((i == chosen ? "> " : "") + (owned ? st.name : "<color=#777777>LOCKED</color>"));
                fighterButtons[i].SetColor(owned ? Color.Lerp(rc, Color.white, 0.25f) : new Color(0.22f, 0.22f, 0.26f));
            }
            if (previewId < 0) previewId = chosen;
            var p = FighterInfo.Get(previewId);
            bool unlocked = Collection.IsUnlocked(previewId);
            string rarity = "<color=#" + ColorUtility.ToHtmlStringRGB(FighterInfo.RarityColor(p.rarity)) + ">" + FighterInfo.RarityName(p.rarity) + "</color>";
            fighterText.text = unlocked
                ? "<b>" + p.name + "</b>  " + rarity + (previewId == chosen ? "  <color=#7CFF8A>(playing as)</color>" : "") + "\n" + p.style +
                  "\n<color=#ffcf4a>" + p.special + "</color>: " + FighterInfo.SpecialHow(p.specialType) + "\n<size=85%>HP " + p.maxHealth + "   " + FighterInfo.PowerText(p) + "   + " + p.strengths + "   - " + p.weakness + "</size>"
                : "<b>???</b>  " + rarity + "\nLocked. Open crates to unlock it.";
            coinsText.text = "COINS: " + Collection.Coins + "    FIGHTERS: " + Collection.UnlockedCount + "/" + FighterInfo.Count;
        }

        void Update()
        {
            // Preview model (rebuilt when the shown fighter changes).
            int show = previewId < 0 ? (int)GameSettings.Fighter : previewId;
            bool locked = !Collection.IsUnlocked(show);
            if (show != previewShownAs || locked != previewLocked)
            {
                previewShownAs = show;
                previewLocked = locked;
                if (preview != null && preview.root != null) { Destroy(preview.root.gameObject); Destroy(preview.leftGlove.gameObject); Destroy(preview.rightGlove.gameObject); }
                preview = FighterModel.Build(previewSpot, FighterInfo.Get(show), locked);
            }
            previewSpot.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(Time.time * 0.6f) * 35f, 0f);
            preview?.PoseIdle(previewSpot, Time.time);

            // Crate shake, then open.
            if (pendingCrate >= 0)
            {
                float t = 1f - (crateAnimUntil - Time.time) / 1.3f;
                crateBox.localRotation = Quaternion.Euler(Random.Range(-1f, 1f) * 12f * t, Random.Range(-1f, 1f) * 12f * t, Random.Range(-1f, 1f) * 12f * t);
                crateBox.localScale = Vector3.one * (1f + t * 0.15f);
                if (Time.time >= crateAnimUntil) FinishCrate();
            }
            if (reveal != null)
            {
                revealSpot.localPosition = Vector3.up * Mathf.Min(0.1f, (7f - (revealUntil - Time.time)) * 0.1f);
                revealSpot.localRotation = Quaternion.Euler(0f, 180f + Time.time * 60f, 0f);
                reveal.PoseIdle(revealSpot, Time.time);
            }
            if (revealUntil > 0f && Time.time > revealUntil)
            {
                revealUntil = -1f;
                if (reveal != null && reveal.root != null) { Destroy(reveal.root.gameObject); Destroy(reveal.leftGlove.gameObject); Destroy(reveal.rightGlove.gameObject); }
                reveal = null;
                crateBox.gameObject.SetActive(true);
                crateBox.localRotation = Quaternion.identity;
                crateBox.localScale = Vector3.one;
                crateText.text = "";
            }

            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            var s = SessionManager.Instance;
            who.text = (PlayerProfile.NameLocked ? "<b>" + PlayerProfile.Nickname + "</b>" : "<color=#ff8080>Pick your name to play</color>") +
                       "     ID: <color=#ffd54a>" + PlayerIdentity.PlayerId + "</color>" + (Moderation.IAmMod ? "   <color=#ff5a4a>MODERATOR</color>" : "");
            modButton.gameObject.SetActive(Moderation.IAmMod);
            string transient = s != null ? s.TransientStatus : null;
            status.text = !string.IsNullOrEmpty(transient) ? transient : s != null && s.Busy ? s.Status
                : Moderation.IsBanned(out string why) ? "<color=#ff8080>" + why + " (Training still works)</color>" : "Pick a fighter on the left, then PLAY";
            codeText.text = "Code: " + typed.PadRight(6, '_');
            Refresh();
        }
    }
}
