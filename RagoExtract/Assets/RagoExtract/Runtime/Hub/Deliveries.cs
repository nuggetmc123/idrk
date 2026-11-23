using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// DELIVERIES: a screen on the west wall of the bunker's main hall (where the Lost &amp; Found bin used to be). Anything
    /// you buy, trade for or get back - from the black market, the traders, the DLC store - waits here, with where it came
    /// from. Press TAKE and it pops out on the shelf under the screen. Sales of yours show here too ("SOLD ... for $"),
    /// the money already in your pocket: press OK to clear them.
    /// </summary>
    public class Deliveries : MonoBehaviour
    {
        Transform _panel, _rows;
        TextMesh _count, _page;
        int _pg;
        bool _dirty = true;
        const int Per = 6;

        public static Deliveries Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Deliveries");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var d = go.AddComponent<Deliveries>();
            d.Build();
            return d;
        }

        void Build()
        {
            // a screen in a dark frame, and a small shelf under it where things come out
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0, 0.03f), new Vector3(1.28f, 0.92f, 0.05f), new Color(0.07f, 0.07f, 0.08f), 0.01f);
            mb.Sub(0).Box(new Vector3(0, -0.66f, -0.12f), new Vector3(1.1f, 0.04f, 0.3f), new Color(0.3f, 0.31f, 0.33f), 0.008f);
            mb.Sub(0).Box(new Vector3(-0.5f, -0.6f, 0f), new Vector3(0.04f, 0.12f, 0.06f), new Color(0.2f, 0.2f, 0.22f), 0.005f);
            mb.Sub(0).Box(new Vector3(0.5f, -0.6f, 0f), new Vector3(0.04f, 0.12f, 0.06f), new Color(0.2f, 0.2f, 0.22f), 0.005f);
            mb.Sub(1).Box(new Vector3(0, 0.47f, 0.0f), new Vector3(1.28f, 0.02f, 0.06f), Color.white, 0f);
            mb.Build("Frame", transform, Mats.Lit("deliv_frame", Color.white, 0.4f, 0.5f, Mats.Metal, null, 0.2f, 0.2f),
                Mats.Lit("deliv_neon", new Color(1f, 0.75f, 0.3f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(1.6f, 1.1f, 0.4f)));
            var shelf = gameObject.AddComponent<BoxCollider>(); shelf.center = new Vector3(0, -0.66f, -0.12f); shelf.size = new Vector3(1.1f, 0.04f, 0.3f);
            _panel = UI.Panel(transform, "Screen", new Vector3(0, 0, -0.0f), Quaternion.identity, new Vector2(1.2f, 0.84f), new Color(0.03f, 0.04f, 0.05f), true, Skin.Steel);
            UI.Text(_panel, "DELIVERIES", new Vector3(-0.56f, 0.36f, -0.004f), 0.05f, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _count = UI.Text(_panel, "", new Vector3(0.56f, 0.36f, -0.004f), 0.026f, UI.Dim, TextAnchor.MiddleRight);
            _page = UI.Text(_panel, "", new Vector3(0, -0.37f, -0.004f), 0.022f, UI.Dim, TextAnchor.MiddleCenter);
            UI.Button(_panel, "<", new Vector3(-0.2f, -0.37f, 0), new Vector2(0.08f, 0.06f), () => { _pg = Mathf.Max(0, _pg - 1); _dirty = true; }, null, BtnStyle.Key);
            UI.Button(_panel, ">", new Vector3(0.2f, -0.37f, 0), new Vector2(0.08f, 0.06f), () => { _pg++; _dirty = true; }, null, BtnStyle.Key);
            UI.Text(transform, "things you buy, trade for or sell show up here - press TAKE", new Vector3(0, -0.53f, -0.02f), 0.018f, UI.Dim, TextAnchor.MiddleCenter);
            Profile.MailChanged += Mark;
            Migrate();
        }

        void Mark() => _dirty = true;
        void OnDestroy() { Profile.MailChanged -= Mark; }

        /// <summary>Before 2.62 deliveries landed on the Armory floor: nothing to move (those stay where they are).</summary>
        void Migrate() { }

        void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            Draw();
        }

        void Draw()
        {
            if (_rows != null) Compat.KillWithMeshes(_rows.gameObject);
            _rows = new GameObject("Rows").transform; _rows.SetParent(_panel, false);
            var prof = Profile.Current;
            var list = prof != null && prof.mailbox != null ? prof.mailbox : new List<MailItem>();
            int waiting = list.FindAll(m => m.item != null).Count;
            _count.text = waiting == 0 ? "nothing waiting" : waiting + " item(s) waiting";
            int pages = Mathf.Max(1, (list.Count + Per - 1) / Per);
            _pg = Mathf.Clamp(_pg, 0, pages - 1);
            _page.text = list.Count == 0 ? "" : "page " + (_pg + 1) + " / " + pages;
            if (list.Count == 0) { UI.Text(_rows, "Nothing here yet.\nBuy from the black market or a trader, or sell something - it shows up here.", new Vector3(0, 0.05f, -0.004f), 0.026f, UI.Dim, TextAnchor.MiddleCenter); return; }
            // newest first
            for (int k = 0; k < Per; k++)
            {
                int idx = list.Count - 1 - (_pg * Per + k);
                if (idx < 0) break;
                var m = list[idx];
                float y = 0.24f - k * 0.1f;
                var def = m.item != null ? Db.Get(m.item.id) : null;
                string title = m.item != null ? (def != null ? def.name : "item") : m.note;
                if (title.Length > 34) title = title.Substring(0, 33) + ".";
                UI.Text(_rows, title, new Vector3(-0.56f, y + 0.015f, -0.004f), 0.03f, m.item != null ? (def != null ? Color.Lerp(ItemDef.RarityColor(def.rarity), Color.white, 0.3f) : Color.white) : new Color(0.5f, 1f, 0.55f), TextAnchor.MiddleLeft, FontStyle.Bold);
                string sub = (m.item != null ? m.note + "  -  " : "") + Ago(m.time);
                UI.Text(_rows, sub, new Vector3(-0.56f, y - 0.025f, -0.004f), 0.019f, UI.Dim, TextAnchor.MiddleLeft);
                var mm = m;
                if (m.item != null) UI.Button(_rows, "TAKE", new Vector3(0.48f, y, 0), new Vector2(0.17f, 0.07f), () => Take(mm), new Color(0.2f, 0.45f, 0.22f), BtnStyle.Key);
                else UI.Button(_rows, "OK", new Vector3(0.48f, y, 0), new Vector2(0.17f, 0.07f), () => Clear(mm), new Color(0.25f, 0.27f, 0.3f), BtnStyle.Key);
            }
        }

        static string Ago(long t)
        {
            long s = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - t;
            return s < 60 ? "just now" : s < 3600 ? (s / 60) + " min ago" : s < 86400 ? (s / 3600) + " h ago" : (s / 86400) + " day(s) ago";
        }

        float _takeT; int _spot;
        /// <summary>TAKE: it comes out on the shelf under the screen, the entry is gone.</summary>
        void Take(MailItem m)
        {
            if (Time.unscaledTime < _takeT) return;   // one press, one item
            _takeT = Time.unscaledTime + 0.4f;
            var prof = Profile.Current;
            if (prof == null || prof.mailbox == null || !prof.mailbox.Remove(m)) return;
            prof.Save();
            int n = _spot++;   // a different spot each time, so things don't land on each other
            var at = transform.TransformPoint(new Vector3(-0.4f + (n % 5) * 0.2f, -0.6f, -0.14f));
            var it = ItemFactory.Spawn(m.item, at, transform.rotation * Quaternion.Euler(0, 90, 0));
            if (it != null && it.rb != null) { it.rb.isKinematic = false; it.SetCollidersTrigger(false); }
            Sfx.Play(Synth.Grab(2), at, 0.6f, 1.1f, 0.5f, 6f);
            _dirty = true;
        }

        void Clear(MailItem m)
        {
            var prof = Profile.Current;
            if (prof == null || prof.mailbox == null) return;
            prof.mailbox.Remove(m); prof.Save();
            _dirty = true;
        }
    }
}
