using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The raid terminal's screen: a big map of the three islands in the sea and the hatch down to the Underground
    /// Sector. Point at a location and pull the trigger (click on desktop) to pick it; the terminal follows, and a
    /// ring marks the one that's selected. Then press QUEUE on the terminal.
    /// </summary>
    public class WorldMapBoard : MonoBehaviour
    {
        const float BW = 2.6f, BH = 1.6f;
        readonly Dictionary<string, WorldButton> _buttons = new Dictionary<string, WorldButton>();
        readonly Dictionary<string, Vector3> _where = new Dictionary<string, Vector3>();
        Transform _ring;

        TextMesh _rules;

        public static WorldMapBoard Create(Transform parent, Vector3 pos, Quaternion rot, float scale = 1f)
        {
            var go = new GameObject("WorldMapBoard");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = Vector3.one * scale;
            var b = go.AddComponent<WorldMapBoard>();
            b.Build();
            return b;
        }

        void Build()
        {
            var p = UI.Panel(transform, "Board", Vector3.zero, Quaternion.identity, new Vector2(BW, BH), null, true, Skin.Steel);
            // the sea
            var sea = new MB();
            sea.Quad(new Vector3(-BW / 2 + 0.06f, -BH / 2 + 0.12f, -0.004f), new Vector3(BW / 2 - 0.06f, -BH / 2 + 0.12f, -0.004f), new Vector3(BW / 2 - 0.06f, BH / 2 - 0.16f, -0.004f), new Vector3(-BW / 2 + 0.06f, BH / 2 - 0.16f, -0.004f), Color.white, Vector2.one);
            sea.Build("Sea", p, Mats.Unlit("board_sea", new Color(0.09f, 0.2f, 0.3f, 1f)));
            // faint grid lines like a chart
            var grid = new MB();
            for (float x = -1.2f; x <= 1.21f; x += 0.3f) grid.Box(new Vector3(x, -0.02f, -0.005f), new Vector3(0.003f, BH - 0.3f, 0.001f), Color.white, 0f);
            for (float y = -0.6f; y <= 0.61f; y += 0.3f) grid.Box(new Vector3(0, y, -0.005f), new Vector3(BW - 0.14f, 0.003f, 0.001f), Color.white, 0f);
            grid.Build("Grid", p, Mats.Unlit("board_grid", new Color(0.25f, 0.4f, 0.52f, 1f)));
            UI.Text(p, "SELECT YOUR RAID", new Vector3(-BW / 2 + 0.08f, BH / 2 - 0.08f, -0.006f), 0.055f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(p, "point at a location and pull the trigger - then press QUEUE below", new Vector3(BW / 2 - 0.08f, BH / 2 - 0.08f, -0.006f), 0.03f, UI.Dim, TextAnchor.MiddleRight);
            _rules = UI.Text(p, "", new Vector3(-BW / 2 + 0.08f, -BH / 2 + 0.06f, -0.006f), 0.034f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            // the islands
            Island(p, "dockyard", new Vector3(-0.78f, 0.22f, 0), 0.34f, 11);
            Island(p, "pinewood", new Vector3(0.05f, -0.28f, 0), 0.32f, 23);
            Island(p, "oldtown", new Vector3(0.82f, 0.25f, 0), 0.33f, 37);
            // the North Pole: an ice floe in the bottom-left corner (Christmas update only)
            if (Festive.Enabled)
            {
                var np = new Vector3(-0.85f, -0.36f, 0);
                Blob(p, np, 0.22f, 61, new Color(0.75f, 0.85f, 0.95f), -0.007f);
                Blob(p, np, 0.18f, 61, new Color(0.96f, 0.97f, 1f), -0.008f);
                new MB().Box(np + new Vector3(0.02f, 0.05f, -0.009f), new Vector3(0.05f, 0.05f, 0.002f), Color.white, 0f).Build("Gift", p, Mats.Unlit("board_gift", new Color(0.8f, 0.1f, 0.1f, 1f)));
                Location(p, "northpole", np, -0.03f);
            }
            // (3.65) the Swamp: a murky green marsh in the bottom-right corner
            {
                var sw = new Vector3(0.84f, -0.36f, 0);
                Blob(p, sw, 0.2f, 71, new Color(0.28f, 0.36f, 0.2f), -0.007f);
                Blob(p, sw + new Vector3(0.02f, 0.01f, 0), 0.13f, 72, new Color(0.18f, 0.28f, 0.22f), -0.008f);
                Location(p, "swamp", sw, -0.03f);
            }
            // the hatch down to the Underground Sector, on a rock between them
            var u = new Vector3(0.12f, 0.36f, 0);
            Blob(p, u, 0.11f, 51, new Color(0.35f, 0.34f, 0.32f), -0.007f);
            new MB().Box(u + new Vector3(0, 0.03f, -0.009f), new Vector3(0.1f, 0.1f, 0.002f), Color.white, 0f).Build("HatchRim", p, Mats.Unlit("board_hatch_rim", new Color(0.85f, 0.68f, 0.08f, 1f)));
            new MB().Box(u + new Vector3(0, 0.03f, -0.011f), new Vector3(0.065f, 0.065f, 0.002f), Color.white, 0f).Build("Hatch", p, Mats.Unlit("board_hatch", new Color(0.08f, 0.08f, 0.08f, 1f)));
            Location(p, "underground", u, -0.11f);
            // selection ring
            _ring = new GameObject("Ring").transform; _ring.SetParent(p, false);
            var ring = new MB();
            for (int i = 0; i < 24; i++) { float a = i * 15f; ring.Box(Quaternion.Euler(0, 0, a) * new Vector3(0.2f, 0, 0), new Vector3(0.012f, 0.05f, 0.002f), Color.white, 0f, 0, Quaternion.Euler(0, 0, a)); }
            ring.Build("Ring", _ring, Mats.Unlit("board_ring", new Color(1f, 0.62f, 0.18f, 1f)));
            // everything on the chart goes in one object: opening a map hides it completely (it used to show through)
            _chart = new GameObject("Chart").transform; _chart.SetParent(p, false);
            var kids = new List<Transform>(); foreach (Transform t in p) if (t != _chart && t.name != "bg") kids.Add(t);
            foreach (var t in kids) t.SetParent(_chart, true);
            Kiosk.OnMapSelected += Show;
            Show(Kiosk.Current != null ? Kiosk.Current.SelectedMap : "dockyard");
        }

        // ---------------------------------------------------------------------------------------------- map details
        // Pick a map and it ZOOMS IN out of the island: a big map on the left, everything about it on the right, BACK to
        // the islands. (Pressing QUEUE below queues for the map you picked.)
        Transform _detail, _chart; Vector3 _detailFrom; float _detailT = -1f; bool _detailOpen;
        WorldButton _deploy; bool _deployQueued;

        /// <summary>The map's boss (each turns up in about 1 raid in 4 with heavy guards - every raid during BOSS HUNT).</summary>
        static string BossOf(string id) => id == "dockyard" ? "THE HARBORMASTER" : id == "underground" ? "DOCTOR KESSLER" : id == "northpole" ? "-" : id == "swamp" ? "THE SWAMP KING (every raid)" : "THE COURIER (air drops)";   // only three bosses now

        static string Facts(string id)
        {
            switch (id)
            {
                case "dockyard": return "Container yard by the sea at dusk\nWarehouses, cranes and container stacks\nVAULT: the Harbor Office\nBeaches and cliffs round the island";
                case "pinewood": return "Thick pine forest round a military outpost\nCabins and camps in the woods\nVAULT: the outpost bunker\nFog rolls in some raids";
                case "oldtown": return "An abandoned town at dawn\nStreets, apartments and shops to search\nVAULT: the old bank\nVillages and farms on the outskirts";
                case "underground": return "A sealed research sector, 3 floors down\nTight corridors and dark rooms\nTWO VAULTS - and the power cuts out\nSounds barely carry between floors";
                case "swamp": return "A flooded bayou - wade through the murk\nEvery raider here is a crocodile\nTHE SWAMP KING holds the Bait & Tackle\nTake his crown to the TURN-IN board";
                case "northpole": return "Santa's village, overrun by evil elves\nChristmas keycards hide in the presents\nVAULT: the Giant Present\nPresents everywhere";
                default: return Maps.Blurb(id);
            }
        }

        void OpenDetail(string id, Vector3 from)
        {
            if (_detail != null) Destroy(_detail.gameObject);
            var board = transform.Find("Board");
            if (board == null) return;
            _detail = new GameObject("Detail").transform;
            _detail.SetParent(board, false);
            if (_chart != null) _chart.gameObject.SetActive(false);
            _deployQueued = false;   // the new DEPLOY button picks up the queue state next frame
            float w = BW - 0.06f, h = BH - 0.06f;
            // dark sheet over the islands
            var sheet = new MB().Box(new Vector3(0, 0, -0.02f), new Vector3(w, h, 0.004f), Color.white, 0f).Build("Sheet", _detail, Mats.Lit("board_detail_bg", new Color(0.05f, 0.07f, 0.09f), 0f, 0.2f));
            var sc = sheet.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 0, -0.02f); sc.size = new Vector3(w, h, 0.004f);   // the island buttons behind it can't be pressed through it
            new MB().Box(new Vector3(0, h / 2 - 0.004f, -0.023f), new Vector3(w, 0.008f, 0.001f), Color.white, 0f).Build("TopLine", _detail, Mats.Lit("board_detail_amber", new Color(1f, 0.62f, 0.18f), 0f, 0.3f, null, null, 0, 0, false, 0.5f, new Color(0.6f, 0.35f, 0.08f)));
            // the big map
            float ms = 1.18f; var mc = new Vector3(-w / 2 + 0.05f + ms / 2, -0.04f, -0.024f);
            new MB().Box(mc + new Vector3(0, 0, 0.001f), new Vector3(ms + 0.03f, ms + 0.03f, 0.001f), Color.white, 0f).Build("MapFrame", _detail, Mats.Lit("board_photo_frame2", new Color(0.92f, 0.88f, 0.76f), 0f, 0.2f));
            var tex = Resources.Load<Texture2D>("Rago/MapPhotos/" + id);
            bool own = false; if (tex == null) { tex = FieldMap.LoadShot(id); own = tex != null; }
            if (tex != null)
            {
                float hs = ms / 2;
                var img = new MB().Quad(new Vector3(mc.x - hs, mc.y - hs, mc.z - 0.001f), new Vector3(mc.x + hs, mc.y - hs, mc.z - 0.001f), new Vector3(mc.x + hs, mc.y + hs, mc.z - 0.001f), new Vector3(mc.x - hs, mc.y + hs, mc.z - 0.001f), Color.white, Vector2.one);
                var m = Mats.Unlit(null, Color.white, tex, Mats.BlendKind.Alpha, true);
                if (own) _made.Add(tex); _made.Add(m);
                img.Build("BigMap", _detail, m);
            }
            else UI.Text(_detail, "NO MAP YET", mc + new Vector3(0, 0, -0.002f), 0.05f, UI.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
            // the details
            float x = mc.x + ms / 2 + 0.07f, y = h / 2 - 0.1f;
            bool ex = Maps.Extreme(id);
            UI.Text(_detail, Maps.Name(id).ToUpper(), new Vector3(x, y, -0.026f), 0.1f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold); y -= 0.1f;
            UI.Text(_detail, Maps.Difficulty(id), new Vector3(x, y, -0.026f), 0.07f, ex ? new Color(1f, 0.3f, 0.22f) : new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleLeft, FontStyle.Bold); y -= 0.09f;
            UI.Text(_detail, UI.Wrap(Maps.Blurb(id), 36), new Vector3(x, y, -0.026f), 0.04f, new Color(0.88f, 0.9f, 0.92f), TextAnchor.UpperLeft); y -= 0.17f;
            void Stat(string k, string v) { UI.Text(_detail, k, new Vector3(x, y, -0.026f), 0.04f, UI.Dim, TextAnchor.MiddleLeft, FontStyle.Bold); UI.Text(_detail, v, new Vector3(x + 0.5f, y, -0.026f), 0.04f, new Color(1f, 0.78f, 0.3f), TextAnchor.MiddleLeft, FontStyle.Bold); y -= 0.062f; }
            Stat("PLAYERS", QueueRules.MinFor(id) + " - " + QueueRules.MaxFor(id));
            UI.Text(_detail, "BOSS", new Vector3(x, y, -0.026f), 0.04f, UI.Dim, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(_detail, BossOf(id), new Vector3(x + 0.5f, y, -0.026f), 0.04f, new Color(1f, 0.35f, 0.25f), TextAnchor.MiddleLeft, FontStyle.Bold); y -= 0.062f;
            Stat("SQUADS", "up to " + QueueRules.PartyMaxFor(id));
            Stat("RAID TIME", "40:00");
            Stat("TIME OF DAY", RaidChoice.Night ? "NIGHT (your pick)" : "DAY (your pick)");
            Stat("AUTO-LAUNCH", "01:00");
            y -= 0.02f;
            UI.Text(_detail, Facts(id), new Vector3(x, y, -0.026f), 0.034f, new Color(0.82f, 0.85f, 0.87f), TextAnchor.UpperLeft);
            // DEPLOY: queues for this map right here (CANCEL while you're in the queue); BACK to the islands
            _deploy = UI.Button(_detail, "DEPLOY", new Vector3(x + 0.2f, -h / 2 + 0.09f, -0.024f), new Vector2(0.4f, 0.12f), () => { Kiosk.Current?.SelectMap(id); Kiosk.Current?.Deploy(); }, new Color(0.15f, 0.5f, 0.2f), BtnStyle.Key);
            UI.Button(_detail, "< BACK", new Vector3(w / 2 - 0.16f, -h / 2 + 0.09f, -0.024f), new Vector2(0.26f, 0.12f), CloseDetail, new Color(0.2f, 0.22f, 0.25f), BtnStyle.Key);
            // zoom in from the island
            _detailFrom = from; _detailT = 0f; _detailOpen = true;
            _detail.localPosition = new Vector3(from.x, from.y, 0); _detail.localScale = Vector3.one * 0.12f;
            Sfx.UI(Synth.Beep(), 0.25f);
        }

        void CloseDetail()
        {
            if (_detail == null) return;
            _detailOpen = false; _detailT = 0f;
            Sfx.UI(Synth.Beep(), 0.2f);
        }

        void Update()
        {
            // the DEPLOY button turns into CANCEL while you're queued
            if (_deploy != null && Kiosk.Current != null && Kiosk.Current.Queued != _deployQueued)
            {
                _deployQueued = Kiosk.Current.Queued;
                _deploy.SetText(_deployQueued ? "CANCEL" : "DEPLOY");
                _deploy.SetColor(_deployQueued ? new Color(0.6f, 0.14f, 0.1f) : new Color(0.15f, 0.5f, 0.2f));
            }
            if (_detail == null || _detailT < 0f) return;
            _detailT += Time.deltaTime / 0.28f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_detailT));
            if (!_detailOpen) k = 1f - k;
            _detail.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, k);
            _detail.localPosition = Vector3.Lerp(new Vector3(_detailFrom.x, _detailFrom.y, 0), Vector3.zero, k);
            if (_detailT >= 1f) { _detailT = -1f; if (!_detailOpen) { Destroy(_detail.gameObject); _detail = null; if (_chart != null) _chart.gameObject.SetActive(true); } }
        }

        void OnDestroy() { Kiosk.OnMapSelected -= Show; foreach (var o in _made) if (o != null) Destroy(o); _made.Clear(); }

        readonly List<Object> _made = new List<Object>();

        /// <summary>The real top-down photo of the map (from the last raid there), framed like a chart pinned over the
        /// island. False if you haven't played that map yet (then the drawn island stays).</summary>
        bool Photo(Transform p, string id, Vector3 c, float size)
        {
            // the map's picture that ships with the game (Resources/Rago/MapPhotos), else the photo from your last raid there
            var tex = Resources.Load<Texture2D>("Rago/MapPhotos/" + id);
            bool own = false;
            if (tex == null) { tex = FieldMap.LoadShot(id); own = tex != null; }
            if (tex == null) return false;
            float h = size / 2f;
            var frame = new MB();
            frame.Box(new Vector3(c.x, c.y, -0.0095f), new Vector3(size + 0.03f, size + 0.03f, 0.002f), Color.white, 0f);
            frame.Build("PhotoFrame_" + id, p, Mats.Unlit("board_photo_frame", new Color(0.92f, 0.88f, 0.76f, 1f)));
            var img = new MB().Quad(new Vector3(c.x - h, c.y - h, -0.0105f), new Vector3(c.x + h, c.y - h, -0.0105f), new Vector3(c.x + h, c.y + h, -0.0105f), new Vector3(c.x - h, c.y + h, -0.0105f), Color.white, Vector2.one);
            var m = Mats.Unlit(null, Color.white, tex, Mats.BlendKind.Alpha, true);
            if (own) _made.Add(tex);   // (a built-in picture is shared: never destroyed)
            _made.Add(m);
            img.Build("Photo_" + id, p, m);
            return true;
        }

        void Island(Transform p, string id, Vector3 c, float r, int seed)
        {
            Blob(p, c, r, seed, new Color(0.86f, 0.78f, 0.58f), -0.007f);          // beach
            Blob(p, c, r * 0.84f, seed, new Color(0.28f, 0.42f, 0.22f), -0.008f);   // land
            // a few dots for buildings / woods
            var rng = new Rng(seed);
            var dots = new MB();
            for (int i = 0; i < 9; i++) { var o = rng.InCircle() * r * 0.6f; if (Mathf.Abs(o.y + 0.02f) < 0.06f) o.y += 0.1f; dots.Box(c + new Vector3(o.x * 1.2f, o.y, -0.009f), Vector3.one * 0.025f, Color.white, 0f); }
            Color dc = id == "pinewood" ? new Color(0.12f, 0.24f, 0.12f) : new Color(0.55f, 0.53f, 0.5f);
            dots.Build("Dots", p, Mats.Unlit("board_dots_" + id, new Color(dc.r, dc.g, dc.b, 1f)));
            Location(p, id, c, -0.02f);
        }

        void Location(Transform p, string id, Vector3 c, float labelY)
        {
            _where[id] = c;
            var btn = UI.Button(p, Maps.Name(id).ToUpper(), c + new Vector3(0, labelY, -0.012f), new Vector2(0.4f, 0.085f), () => { Kiosk.Current?.SelectMap(id); OpenDetail(id, c); }, new Color(0.12f, 0.12f, 0.12f), BtnStyle.Tag);
            // the difficulty, under the name: NORMAL in green, EXTREME in red
            bool ex = Maps.Extreme(id);
            UI.Text(p, Maps.Difficulty(id), c + new Vector3(0.21f, labelY + 0.03f, -0.016f), 0.024f, ex ? new Color(1f, 0.3f, 0.22f) : new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _buttons[id] = btn;
            UI.Text(p, UI.Wrap(Maps.Blurb(id), 34), c + new Vector3(0, labelY - 0.07f, -0.012f), 0.021f, new Color(0.95f, 0.95f, 0.9f), TextAnchor.UpperCenter);
        }

        /// <summary>An irregular filled shape (a fan of triangles round a noisy outline).</summary>
        static void Blob(Transform p, Vector3 c, float r, int seed, Color col, float z)
        {
            var mb = new MB();
            int n = 28;
            Vector3 Pt(int i)
            {
                float a = i * Mathf.PI * 2f / n;
                float k = 0.66f + 0.5f * Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + seed, Mathf.Sin(a) * 1.6f + seed * 0.7f);   // an irregular coastline
                return c + new Vector3(Mathf.Cos(a) * r * k * 1.25f, Mathf.Sin(a) * r * k, z);
            }
            var cc = new Vector3(c.x, c.y, z);
            for (int i = 0; i < n; i++) mb.Card(cc, cc, Pt(i), Pt(i + 1), col, 0f, 0f);
            mb.Build("Blob", p, Mats.Unlit("board_" + ((int)(col.r * 100)) + "_" + ((int)(col.g * 100)), new Color(col.r, col.g, col.b, 1f)));
        }

        void Show(string id)
        {
            if (this == null || id == null) return;
            foreach (var kv in _buttons) kv.Value.SetColor(kv.Key == id ? new Color(0.85f, 0.45f, 0.1f) : new Color(0.12f, 0.12f, 0.12f));
            if (_where.TryGetValue(id, out var c) && _ring != null) _ring.localPosition = new Vector3(c.x, c.y, -0.010f);
            if (_rules != null)
                _rules.text = Maps.NameWithDifficulty(id) + "   /   " + (QueueRules.MinFor(id)) + "-" + QueueRules.MaxFor(id) + " OPERATORS" + (QueueRules.PartyMaxFor(id) < QueueRules.Max ? "   /   SQUADS OF " + QueueRules.PartyMaxFor(id) : "") + "   /   AUTO-LAUNCH AT 01:00";
        }
    }
}
