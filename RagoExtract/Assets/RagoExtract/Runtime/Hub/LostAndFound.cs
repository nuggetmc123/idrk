using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The bunker's Lost &amp; Found bin. Anything left lying on the floor (or that falls out of the world) turns up
    /// here after a few seconds, and so does whatever doesn't fit in your stash when the game loads - nothing is
    /// ever lost or hidden. It's part of your stash: saved with it, and you take things back out by hand.
    /// </summary>
    public class LostAndFound : MonoBehaviour
    {
        public static LostAndFound I;
        ItemContainer _bin;
        readonly List<ItemData> _extra = new List<ItemData>();   // more than fits: shown a page at a time
        TextMesh _count;
        WorldButton _next;
        readonly Dictionary<Item, float> _still = new Dictionary<Item, float>();
        float _scanT;
        public float FloorY;   // the bunker floor height

        public static LostAndFound Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("LostAndFound");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var l = go.AddComponent<LostAndFound>();
            l.FloorY = go.transform.position.y;
            l.Build();
            I = l;
            return l;
        }

        void OnDestroy() { if (I == this) I = null; }

        void Build()
        {
            Color body = UI.Lin(new Color(0.2f, 0.32f, 0.5f)), rim = UI.Lin(new Color(0.85f, 0.7f, 0.15f)), dark = UI.Lin(new Color(0.08f, 0.09f, 0.1f));
            var mb = new MB(2);
            // an open-topped bin on legs: the customer stands at local -Z
            mb.Sub(0).Box(new Vector3(0, 0.36f, 0), new Vector3(1.7f, 0.06f, 0.75f), body, 0.01f);                    // floor of the bin
            mb.Sub(0).Box(new Vector3(0, 0.6f, 0.36f), new Vector3(1.7f, 0.5f, 0.04f), body, 0.01f);                  // back
            mb.Sub(0).Box(new Vector3(0, 0.48f, -0.36f), new Vector3(1.7f, 0.26f, 0.04f), body, 0.01f);               // low front lip
            foreach (float sx in new[] { -0.85f, 0.85f }) mb.Sub(0).Box(new Vector3(sx, 0.55f, 0), new Vector3(0.04f, 0.44f, 0.75f), body, 0.01f);
            foreach (float sx in new[] { -0.8f, 0.8f }) foreach (float sz in new[] { -0.32f, 0.32f }) mb.Sub(1).Box(new Vector3(sx, 0.17f, sz), new Vector3(0.05f, 0.34f, 0.05f), dark, 0.005f);
            mb.Sub(1).Box(new Vector3(0, 0.615f, -0.36f), new Vector3(1.72f, 0.02f, 0.05f), rim, 0.003f);
            mb.Sub(1).Box(new Vector3(0, 1.25f, 0.37f), new Vector3(1.4f, 0.32f, 0.03f), dark, 0.01f);               // sign board
            mb.Build("Bin", transform, Mats.Lit("lf_paint", Color.white, 0.3f, 0.4f, Mats.Metal, null, 0.4f, 0.3f), Mats.Lit("lf_dark", Color.white, 0.5f, 0.4f, Mats.Metal, null, 0.3f, 0.3f));
            foreach (var (c, s) in new[] { (new Vector3(0, 0.36f, 0), new Vector3(1.7f, 0.06f, 0.75f)), (new Vector3(0, 0.6f, 0.36f), new Vector3(1.7f, 0.5f, 0.04f)), (new Vector3(0, 0.48f, -0.36f), new Vector3(1.7f, 0.26f, 0.04f)),
                                         (new Vector3(-0.85f, 0.55f, 0), new Vector3(0.04f, 0.44f, 0.75f)), (new Vector3(0.85f, 0.55f, 0), new Vector3(0.04f, 0.44f, 0.75f)) })
            { var b = gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }
            UI.Text(transform, "LOST & FOUND", new Vector3(0, 1.3f, 0.35f), 0.075f, new Color(0.95f, 0.78f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "things left on the floor end up here", new Vector3(0, 1.18f, 0.35f), 0.03f, new Color(0.85f, 0.85f, 0.8f));
            _count = UI.Text(transform, "", new Vector3(-0.62f, 0.68f, -0.39f), 0.03f, new Color(0.95f, 0.9f, 0.8f), TextAnchor.MiddleLeft);
            _next = UI.Button(transform, "MORE >", new Vector3(0.62f, 0.68f, -0.39f), new Vector2(0.26f, 0.08f), NextPage);
            _next.gameObject.SetActive(false);
            _bin = ItemContainer.Grid(transform, new Vector3(0, 0.395f, 0), Quaternion.identity, 4, 2, new Vector2(0.4f, 0.33f), 0.38f);
            _bin.onChanged = Refresh;
            Refresh();
        }

        /// <summary>Put an item (as data) in the bin, or on the next page if the bin is full.</summary>
        public void Add(ItemData d)
        {
            if (d == null) return;
            var free = _bin.slots.Find(s => s.item == null);
            if (free == null) { _extra.Add(d); Refresh(); return; }
            var it = ItemFactory.Spawn(d, free.transform.position + Vector3.up * 0.2f, transform.rotation);
            if (it == null) return;
            it.transform.SetParent(null, true);
            if (free.Accepts(it)) free.Store(it, false); else { Compat.Kill(it.gameObject); _extra.Add(d); }
            Refresh();
        }

        public List<ItemData> Save()
        {
            var l = new List<ItemData>();
            foreach (var s in _bin.slots) if (s.item != null) l.Add(s.item.Save());
            l.AddRange(_extra);
            return l;
        }

        void NextPage()
        {
            if (_extra.Count == 0) return;
            var shown = new List<ItemData>();
            foreach (var s in _bin.slots) if (s.item != null) { shown.Add(s.item.Save()); var it = s.item; s.Release(it); Destroy(it.gameObject); }
            var take = new List<ItemData>(_extra); _extra.Clear();
            _extra.AddRange(shown);
            foreach (var d in take) Add(d);
            Sfx.UI(Synth.Beep(), 0.3f);
        }

        void Refresh()
        {
            if (_count == null) return;
            int n = 0; foreach (var s in _bin.slots) if (s.item != null) n++;
            n += _extra.Count;
            _count.text = n == 0 ? "empty" : n + " item" + (n == 1 ? "" : "s") + (_extra.Count > 0 ? "  (" + _extra.Count + " more underneath)" : "");
            if (_next != null) _next.gameObject.SetActive(_extra.Count > 0);
        }

        /// <summary>Every couple of seconds: items left lying on the floor for a while (or fallen out of the world) come here.</summary>
        void Update()
        {
            if (Time.time < _scanT) return;
            _scanT = Time.time + 1f;
            var root = RagoGame.I != null ? RagoGame.I.MapRoot : null;
            foreach (var it in new List<Item>(Item.All))
            {
                if (it == null || it.Held || it.slot != null || it.rb == null) { if (it != null) _still.Remove(it); continue; }
                if (it is Magazine m && m.inGun != null) continue;
                if (it is Magazine lm && lm.transform.position.y - FloorY < 0.35f && lm.rb.Vel().sqrMagnitude < 0.01f)
                {
                    // a mag on the floor: after a while its rounds go back into your ammo reserve
                    if (!_still.TryGetValue(it, out var ms)) { _still[it] = Time.time; continue; }
                    if (Time.time - ms < 8f) continue;
                    _still.Remove(it);
                    if (Profile.Current != null) Profile.Current.stashAmmo[(int)lm.cal] += lm.rounds;
                    Destroy(it.gameObject);
                    continue;
                }
                if (it is Magazine) continue;
                if (it is SightItem si && si.onGun != null) continue;
                if (it is AttachItem ai && ai.onGun != null) continue;
                if (it.transform.parent != null && (root == null || !it.transform.IsChildOf(root))) continue;   // carried / in a bag / on a gun
                float y = it.transform.position.y - FloorY;
                bool lost = y < -3f;                                                     // fell out of the world
                bool onFloor = y < 0.35f && it.rb.Vel().sqrMagnitude < 0.01f;          // lying on the floor, still
                if (!lost && !onFloor) { _still.Remove(it); continue; }
                if (!_still.TryGetValue(it, out var since)) { _still[it] = Time.time; if (!lost) continue; }
                if (!lost && Time.time - since < 8f) continue;
                _still.Remove(it);
                var d = it.Save();
                if (d == null) continue;
                Destroy(it.gameObject);
                Add(d);
                RagoGame.I?.Player?.Toast((it.def != null ? it.def.name : "Item") + " moved to the Lost & Found");
            }
        }
    }
}
