using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Tester menu > SPAWN ITEM: a board of little 3D models of every item (12 a page), each with its name underneath,
    /// sorted into GUNS / ARMOR / ATTACHMENTS / SIGHTS / OTHER. Press one and point where it goes, like before.
    /// </summary>
    public class SpawnBrowser : MonoBehaviour
    {
        static SpawnBrowser _i;
        public static bool IsOpen => _i != null && _i._panel != null && _i._panel.gameObject.activeSelf;

        static readonly string[] Cats = { "GUNS", "ARMOR", "ATTACH", "SIGHTS", "OTHER" };
        const int Cols = 4, Rows = 3, PerPage = Cols * Rows;
        Transform _panel, _content;
        readonly List<WorldButton> _catBtns = new List<WorldButton>();
        int _cat, _page;

        public static void Open(Transform at)
        {
            if (_i == null) { var go = new GameObject("SpawnBrowser"); DontDestroyOnLoad(go); _i = go.AddComponent<SpawnBrowser>(); _i.Build(); }
            _i.transform.SetPositionAndRotation(at.position, at.rotation);
            _i._panel.gameObject.SetActive(true);
            _i.Redraw();
        }

        public static void Close() { if (_i != null && _i._panel != null) { _i._panel.gameObject.SetActive(false); _i.ClearContent(); } }

        void Build()
        {
            _panel = UI.Panel(transform, "Board", Vector3.zero, Quaternion.identity, new Vector2(1.12f, 0.9f), null, true, Skin.Steel);
            UI.Text(_panel, "SPAWN", new Vector3(-0.52f, 0.39f, 0), 0.034f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Button(_panel, "< MENU", new Vector3(0.33f, 0.39f, 0), new Vector2(0.2f, 0.065f), () => { Close(); GameMenu.Reopen(); }, null, BtnStyle.Key);
            UI.Button(_panel, "X", new Vector3(0.5f, 0.39f, 0), new Vector2(0.07f, 0.065f), Close, new Color(0.5f, 0.15f, 0.1f), BtnStyle.Key);
            for (int i = 0; i < Cats.Length; i++)
            {
                int c = i;
                _catBtns.Add(UI.Button(_panel, Cats[i], new Vector3(-0.44f + i * 0.22f, 0.3f, 0), new Vector2(0.2f, 0.07f), () => { _cat = c; _page = 0; Sfx.UI(Synth.Beep(), 0.2f); Redraw(); }));
            }
            _panel.gameObject.SetActive(false);
        }

        void ClearContent() { if (_content != null) Destroy(_content.gameObject); _content = null; }

        static List<ItemDef> Items(int cat)
        {
            Db.Init();
            var l = new List<ItemDef>();
            switch (cat)
            {
                case 0: l.AddRange(Db.Guns); break;
                case 1: l.AddRange(Db.Armor); l.AddRange(Db.Packs); break;
                case 2: l.AddRange(Db.Attachments); break;
                case 3: l.AddRange(Db.Sights); break;
                default: l.Add(Tester.CartDef); l.Add(Db.Get("gift")); l.Add(Db.Get("gift_santa")); foreach (var m in Db.Misc) if (m.id != "gift" && m.id != "gift_santa") l.Add(m); break;
            }
            return l;
        }

        void Redraw()
        {
            ClearContent();
            _content = new GameObject("Content").transform;
            _content.SetParent(_panel, false);
            for (int i = 0; i < _catBtns.Count; i++) _catBtns[i].SetColor(i == _cat ? new Color(0.2f, 0.42f, 0.2f) : new Color(0.28f, 0.3f, 0.32f));
            var items = Items(_cat);
            int pages = Mathf.Max(1, (items.Count + PerPage - 1) / PerPage);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            const float tw = 0.255f, th = 0.17f;
            for (int k = 0; k < PerPage; k++)
            {
                int idx = _page * PerPage + k;
                if (idx >= items.Count) break;
                var d = items[idx];
                int col = k % Cols, row = k / Cols;
                var c = new Vector3(-0.39f + col * (tw + 0.01f), 0.17f - row * (th + 0.015f), 0);
                UI.Button(_content, "", c, new Vector2(tw, th), () => { Close(); Tester.BeginPlace(d); }, new Color(0.17f, 0.18f, 0.19f));
                // the little model, turned side-on and a bit towards you, filling the top of the tile
                var holder = new GameObject("Model").transform;
                holder.SetParent(_content, false);
                holder.localPosition = c + new Vector3(0, 0.02f, -0.06f);
                holder.localRotation = Quaternion.identity;
                try { Model(d, holder, tw * 0.62f, th * 0.5f); } catch (System.Exception e) { Debug.LogWarning("[Rago] spawn browser model " + d.id + ": " + e.Message); }
                string nm = d.name ?? d.id; if (nm.Length > 20) nm = nm.Substring(0, 19) + ".";
                UI.Text(_content, nm, c + new Vector3(0, -th * 0.38f, -0.024f), 0.017f, ItemDef.RarityColor(d.rarity), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            UI.Button(_content, "<", new Vector3(-0.2f, -0.39f, 0), new Vector2(0.09f, 0.065f), () => { _page = (_page + pages - 1) % pages; Sfx.UI(Synth.Beep(), 0.2f); Redraw(); });
            UI.Text(_content, "PAGE " + (_page + 1) + " / " + pages, new Vector3(0, -0.39f, 0), 0.026f, Color.white, TextAnchor.MiddleCenter);
            UI.Button(_content, ">", new Vector3(0.2f, -0.39f, 0), new Vector2(0.09f, 0.065f), () => { _page = (_page + 1) % pages; Sfx.UI(Synth.Beep(), 0.2f); Redraw(); });
        }

        /// <summary>A look-only copy of the item (no physics, not a real item), scaled into w x h and centred on the holder.</summary>
        static void Model(ItemDef d, Transform holder, float w, float h) => Model(d, holder, w, h, null);

        /// <summary>A look-only model of an item (optionally a gun in a paint job), fitted into w x h on the holder.</summary>
        public static GameObject Model(ItemDef d, Transform holder, float w, float h, string skin, float yaw = 25f)
        {
            GameObject go;
            if (d == Tester.PresentDef) go = MiniPresent();
            else if (d == Tester.CartDef) go = MiniCart();
            else
            {
                var it = ItemFactory.Spawn(new ItemData(d.id), holder.position, holder.rotation);
                if (it == null) return null;
                if (skin != null && it is Gun gun) GunSkins.Apply(gun, skin);
                foreach (var x in it.GetComponentsInChildren<Item>(true)) { Item.All.Remove(x); if (x.rb != null) { x.rb.isKinematic = true; x.rb.detectCollisions = false; } }
                foreach (var cl in it.GetComponentsInChildren<Collider>(true)) Destroy(cl);
                foreach (var x in it.GetComponentsInChildren<Item>(true)) Destroy(x);
                foreach (var l in it.GetComponentsInChildren<Light>(true)) Destroy(l);
                go = it.gameObject;
            }
            go.transform.SetParent(holder, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(8f, 90f + yaw, 0f);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return go;
            foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // measure only the solid model (laser beams, light cones and particles have huge bounds), in the board's plane
            Bounds Solid() { bool any = false; Bounds bb = default; foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { if (!r.enabled) continue; if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); } return any ? bb : rs[0].bounds; }
            Bounds b = Solid();
            Vector3 sz = holder.InverseTransformVector(b.size); sz = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
            float s = Mathf.Min(w / Mathf.Max(0.01f, sz.x), h / Mathf.Max(0.01f, sz.y), (w * 0.9f) / Mathf.Max(0.01f, sz.z));   // the depth too: it mustn't poke out of the board at you
            s = Mathf.Min(s, 1.6f);   // small things (a keycard, a bandage) aren't blown up to the size of a rifle
            go.transform.localScale *= s;
            b = Solid();
            go.transform.position += holder.position - b.center;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (!(r is MeshRenderer)) r.enabled = false;   // no beams / glows sticking out of the board
            return go;
        }

        // ---- cached previews: building a skinned gun model is slow (a whole gun is made, then stripped), so each preview
        // is built once and reused - menus that redraw (the DLC tab, the Market's DLC wall) just borrow it again
        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        static readonly List<GameObject> _lent = new List<GameObject>();
        static Transform _pool;

        /// <summary>A look-only item model like <see cref="Model"/>, but built once and kept: call <see cref="ReturnPreviews"/>
        /// before the holder is destroyed so it goes back to the pool.</summary>
        public static GameObject Preview(ItemDef d, Transform holder, float w, float h, string skin, float yaw = 25f)
        {
            if (d == null || holder == null) return null;
            if (_pool == null) { var pg = new GameObject("PreviewPool"); DontDestroyOnLoad(pg); pg.SetActive(false); _pool = pg.transform; }
            string key = d.id + "|" + (skin ?? "") + "|" + w.ToString("0.###") + "|" + h.ToString("0.###") + "|" + yaw.ToString("0");
            if (!_cache.TryGetValue(key, out var go) || go == null)
            {
                // build it on a stand-in holder at the origin, so its local placement is the same wherever it's shown
                var stand = new GameObject("Stand").transform; stand.SetParent(_pool, false);
                go = Model(d, stand, w, h, skin, yaw);
                if (go == null) { Destroy(stand.gameObject); return null; }
                go.transform.SetParent(_pool, false);
                Destroy(stand.gameObject);
                if (_cache.Count > 120) { foreach (var kv in _cache) if (kv.Value != null && !_lent.Contains(kv.Value)) Compat.KillWithMeshes(kv.Value); _cache.Clear(); }
                _cache[key] = go;
            }
            if (_lent.Contains(go)) return null;   // already on show somewhere else this frame
            go.transform.SetParent(holder, false);
            _lent.Add(go);
            return go;
        }

        /// <summary>Everything lent out goes back to the pool (before the menu that showed it throws its contents away).</summary>
        public static void ReturnPreviews(Transform under = null)
        {
            if (_pool == null) return;
            for (int i = _lent.Count - 1; i >= 0; i--)
            {
                var go = _lent[i];
                if (go == null) { _lent.RemoveAt(i); continue; }
                if (under != null && !go.transform.IsChildOf(under)) continue;
                go.transform.SetParent(_pool, false);
                _lent.RemoveAt(i);
            }
        }

        static GameObject MiniPresent()
        {
            var mb = new MB();
            Color wrap = new Color(0.75f, 0.1f, 0.1f), rib = new Color(0.9f, 0.75f, 0.2f);
            mb.Box(new Vector3(0, 0.15f, 0), new Vector3(0.42f, 0.3f, 0.42f), wrap, 0.01f);
            mb.Box(new Vector3(0, 0.15f, 0), new Vector3(0.05f, 0.306f, 0.426f), rib, 0f);
            mb.Box(new Vector3(0, 0.15f, 0), new Vector3(0.426f, 0.306f, 0.05f), rib, 0f);
            mb.Sphere(new Vector3(-0.045f, 0.33f, 0), new Vector3(0.05f, 0.03f, 0.025f), 8, rib);
            mb.Sphere(new Vector3(0.045f, 0.33f, 0), new Vector3(0.05f, 0.03f, 0.025f), 8, rib);
            return mb.Build("MiniPresent", null, Mats.Lit("present_wrap", Color.white, 0.1f, 0.5f, Mats.Noise, null, 0.08f, 0f));
        }

        static GameObject MiniCart()
        {
            var mb = new MB();
            Color body = new Color(0.88f, 0.88f, 0.85f), dark = new Color(0.1f, 0.1f, 0.1f), seat = new Color(0.2f, 0.25f, 0.45f);
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.4f, 2.3f), body, 0.08f);
            mb.Box(new Vector3(0, 0.75f, -0.2f), new Vector3(1.0f, 0.2f, 0.5f), seat, 0.05f);
            mb.Box(new Vector3(0, 1.85f, -0.1f), new Vector3(1.25f, 0.06f, 1.7f), body, 0.02f);
            foreach (var x in new[] { -0.55f, 0.55f }) foreach (var z in new[] { 0.55f, -0.75f }) mb.Box(new Vector3(x, 1.25f, z), new Vector3(0.05f, 1.2f, 0.05f), dark, 0f);
            foreach (var x in new[] { -0.6f, 0.6f }) foreach (var z in new[] { 0.75f, -0.75f }) mb.Cyl(new Vector3(x - Mathf.Sign(x) * 0.08f, 0.22f, z), new Vector3(x + Mathf.Sign(x) * 0.08f, 0.22f, z), 0.22f, 0.22f, 14, dark);
            return mb.Build("MiniCart", null, Mats.Lit("mini_cart", Color.white, 0.2f, 0.45f, Mats.Noise, null, 0.15f, 0.1f));
        }

        void Update()
        {
            if (!IsOpen) return;
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null || p.Dead || Vector3.Distance(p.Head.position, transform.position) > 2.5f) Close();
        }
    }
}
