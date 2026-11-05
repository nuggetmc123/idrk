using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A big wall map next to the raid terminal: the three islands in the sea and the hatch down to the Underground
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
            Kiosk.OnMapSelected += Show;
            Show(Kiosk.Current != null ? Kiosk.Current.SelectedMap : "dockyard");
        }

        void OnDestroy() { Kiosk.OnMapSelected -= Show; }

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
            var btn = UI.Button(p, Maps.Name(id).ToUpper(), c + new Vector3(0, labelY, -0.012f), new Vector2(0.4f, 0.085f), () => Kiosk.Current?.SelectMap(id), new Color(0.12f, 0.12f, 0.12f), BtnStyle.Tag);
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
                _rules.text = Maps.Name(id).ToUpper() + "   /   " + (QueueRules.MinFor(id)) + "-" + QueueRules.MaxFor(id) + " OPERATORS" + (QueueRules.PartyMaxFor(id) < QueueRules.Max ? "   /   SQUADS OF " + QueueRules.PartyMaxFor(id) : "") + "   /   AUTO-LAUNCH AT 01:00";
        }
    }
}
