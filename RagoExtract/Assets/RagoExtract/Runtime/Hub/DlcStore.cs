using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The DLC STORE (Market, west wall): content packs bought with your in-game cash. A pack unlocks its gun skins at
    /// the paint booth and sends its items to your bunker (the Armory floor). Each pack is bought once.
    /// The Holiday Pack is only on sale over Christmas (keep it if you bought it). All packs are FREE for now (price 0);
    /// paying real money later goes through the store's in-app purchases (see README "DLC for real money").
    /// </summary>
    public class DlcStore : MonoBehaviour
    {
        public class Pack
        {
            public string id, name, tagline; public int price; public Color col; public string[] items; public bool seasonal;
        }

        public static readonly Pack[] Packs =
        {
            new Pack { id = "holiday", name = "HOLIDAY PACK", price = 0, col = new Color(0.75f, 0.1f, 0.1f), seasonal = true,
                tagline = "festive skins + a candy cane club", items = new[] { "candycane", "impact", "impact", "impact" } },
            new Pack { id = "street", name = "STREET PACK", price = 0, col = new Color(0.15f, 0.6f, 0.35f),
                tagline = "loud city paint jobs", items = new string[0] },
            new Pack { id = "survivor", name = "SURVIVOR PACK", price = 0, col = new Color(0.2f, 0.4f, 0.65f),
                tagline = "weathered and wild", items = new string[0] },
            new Pack { id = "collector", name = "COLLECTOR'S PACK", price = 0, col = new Color(0.55f, 0.3f, 0.75f),
                tagline = "the rarest finishes", items = new string[0] },
        };

        /// <summary>A card's showcase rifle: one copy per skin in the pack, shown in turn.</summary>
        class Showcase { public List<GameObject> guns = new List<GameObject>(); public List<string> names = new List<string>(); public TextMesh label; public int at; }
        readonly List<Showcase> _shows = new List<Showcase>();
        float _cycleT;

        Transform _cards;

        public static DlcStore Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("DlcStore");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var s = go.AddComponent<DlcStore>();
            s.Build();
            return s;
        }

        void Build()
        {
            // a dark back panel with a neon header (the customer is at local -Z)
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 1.75f, 0.03f), new Vector3(4.8f, 2.6f, 0.06f), new Color(0.06f, 0.06f, 0.08f), 0.02f);
            mb.Sub(1).Box(new Vector3(0, 3.06f, -0.01f), new Vector3(4.6f, 0.04f, 0.03f), Color.white, 0f);
            mb.Sub(1).Box(new Vector3(0, 0.48f, -0.01f), new Vector3(4.6f, 0.04f, 0.03f), Color.white, 0f);
            mb.Build("Board", transform, Mats.Lit("dlc_board", Color.white, 0.3f, 0.6f, Mats.Noise, null, 0.1f, 0.1f),
                Mats.Lit("dlc_neon", new Color(0.4f, 0.8f, 1f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(0.6f, 1.4f, 2.2f)));
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.75f, 0.03f); col.size = new Vector3(4.6f, 2.5f, 0.06f);
            UI.Text(transform, "DLC STORE", new Vector3(0, 2.8f, -0.01f), 0.2f, new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "content packs - skins for the paint booth, items to your bunker", new Vector3(0, 2.62f, -0.01f), 0.045f, UI.Dim, TextAnchor.MiddleCenter);
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(transform, false); l.transform.localPosition = new Vector3(0, 2.2f, -1.2f);
            l.type = LightType.Point; l.color = new Color(0.5f, 0.75f, 1f); l.range = 5f; l.intensity = 1f;
            if (Festive.On) Festive.Wreath(transform, new Vector3(0, 3.35f, -0.05f), 0.35f);
            Redraw();
        }

        void Redraw()
        {
            if (_cards != null) Destroy(_cards.gameObject);
            _shows.Clear();
            _cards = new GameObject("Cards").transform; _cards.SetParent(transform, false);
            var shown = new List<Pack>();
            foreach (var p in Packs) if (!p.seasonal || Festive.On || Owns(p.id)) shown.Add(p);
            float w = 1.06f, gap = 0.06f, x0 = -(shown.Count - 1) * (w + gap) / 2f;
            Db.Init();
            for (int i = 0; i < shown.Count; i++)
            {
                var p = shown[i];
                bool owned = Owns(p.id);
                var c = new Vector3(x0 + i * (w + gap), 1.56f, -0.02f);
                var card = UI.Panel(_cards, "Card", c, Quaternion.identity, new Vector2(w, 1.9f), new Color(0.1f, 0.1f, 0.12f), true, Skin.Steel);
                Color light = Color.Lerp(p.col, Color.white, 0.45f);
                // header band in the pack's colour
                var hb = new MB(); hb.Box(Vector3.zero, new Vector3(w - 0.04f, 0.17f, 0.01f), p.col, 0.01f);
                hb.Build("Header", card, Mats.Lit("dlc_head_" + p.id, Color.white, 0.2f, 0.6f, null, null, 0f, 0f, false, 0.5f, p.col * 0.35f)).transform.localPosition = new Vector3(0, 0.84f, -0.008f);
                UI.Text(card, p.name, new Vector3(0, 0.855f, -0.016f), 0.058f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Text(card, p.tagline, new Vector3(0, 0.735f, -0.006f), 0.028f, UI.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
                // the showcase: a rifle wearing each of the pack's skins in turn
                var skins = GunSkins.All.FindAll(sk => sk.pack == p.id);
                var show = new Showcase();
                var stand = new GameObject("Showcase").transform; stand.SetParent(card, false); stand.localPosition = new Vector3(0, 0.47f, -0.12f);
                var rifle = Db.Get("sentinel") ?? Db.Get("p9");
                foreach (var sk in skins)
                {
                    var holder = new GameObject("Skin_" + sk.id).transform; holder.SetParent(stand, false);
                    var g = rifle != null ? SpawnBrowser.Model(rifle, holder, w * 0.86f, 0.36f, sk.id, 20f) : null;
                    holder.gameObject.SetActive(show.guns.Count == 0);
                    show.guns.Add(holder.gameObject); show.names.Add(sk.name);
                }
                show.label = UI.Text(card, skins.Count > 0 ? skins[0].name.ToUpper() : "", new Vector3(0, 0.255f, -0.006f), 0.034f, light, TextAnchor.MiddleCenter, FontStyle.Bold);
                _shows.Add(show);
                // skin prints: a swatch of each pattern with its name
                UI.Text(card, "SKINS (" + skins.Count + ")", new Vector3(-w / 2 + 0.05f, 0.17f, -0.006f), 0.026f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                float sw = Mathf.Min(0.22f, (w - 0.1f) / Mathf.Max(1, skins.Count) - 0.02f);
                for (int k = 0; k < skins.Count; k++)
                {
                    float sx = -(skins.Count - 1) * (sw + 0.02f) / 2f + k * (sw + 0.02f);
                    var frame = new MB(); frame.Box(Vector3.zero, new Vector3(sw + 0.012f, sw * 0.7f + 0.012f, 0.006f), new Color(0.75f, 0.75f, 0.78f), 0.003f);
                    frame.Build("Frame", card, Mats.Lit("dlc_frame", Color.white, 0.8f, 0.6f)).transform.localPosition = new Vector3(sx, 0.055f, -0.008f);
                    var print = new MB(); print.Box(Vector3.zero, new Vector3(sw, sw * 0.7f, 0.004f), Color.white, 0f);
                    var mat = skins[k].mat != null ? skins[k].mat() : Mats.Lit("dlc_plain", Color.gray);
                    print.Build("Print_" + skins[k].id, card, mat).transform.localPosition = new Vector3(sx, 0.055f, -0.013f);
                    string nm = skins[k].name; if (nm.Length > 13) nm = nm.Substring(0, 12) + ".";
                    UI.Text(card, nm, new Vector3(sx, 0.055f - sw * 0.35f - 0.025f, -0.006f), 0.019f, Color.white, TextAnchor.MiddleCenter);
                }
                // items in the pack: little models with a count
                float y = -0.2f;
                if (p.items.Length > 0)
                {
                    UI.Text(card, "ITEMS", new Vector3(-w / 2 + 0.05f, y + 0.08f, -0.006f), 0.026f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                    var counts = new Dictionary<string, int>(); var order = new List<string>();
                    foreach (var id in p.items) { if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); } counts[id]++; }
                    for (int k = 0; k < order.Count; k++)
                    {
                        var def = Db.Get(order[k]); if (def == null) continue;
                        float ix = -(order.Count - 1) * 0.3f / 2f + k * 0.3f;
                        var holder = new GameObject("Item_" + def.id).transform; holder.SetParent(card, false); holder.localPosition = new Vector3(ix, y - 0.04f, -0.06f);
                        SpawnBrowser.Model(def, holder, 0.24f, 0.14f, null, 30f);
                        UI.Text(card, (counts[order[k]] > 1 ? counts[order[k]] + "x " : "") + def.name, new Vector3(ix, y - 0.15f, -0.006f), 0.019f, Color.white, TextAnchor.MiddleCenter);
                    }
                }
                else UI.Text(card, "unlocks at your paint booth\nin the bunker Armory", new Vector3(0, y - 0.05f, -0.006f), 0.024f, UI.Dim, TextAnchor.MiddleCenter);
                // price and button
                UI.Text(card, owned ? "OWNED" : p.price <= 0 ? "FREE" : "$" + p.price.ToString("N0"), new Vector3(0, -0.53f, -0.006f), 0.085f, owned ? new Color(0.5f, 1f, 0.55f) : light, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (!owned) { var pp = p; UI.Button(card, p.price <= 0 ? "GET" : "BUY", new Vector3(0, -0.75f, 0), new Vector2(0.55f, 0.13f), () => Buy(pp), new Color(0.2f, 0.45f, 0.22f), BtnStyle.Key); }
                else UI.Text(card, "paint it on at the booth", new Vector3(0, -0.74f, -0.006f), 0.024f, UI.Dim, TextAnchor.MiddleCenter);
            }
        }

        void Update()
        {
            // the showcase rifles change skin every 2.5 s
            _cycleT -= Time.deltaTime;
            if (_cycleT > 0f) return;
            _cycleT = 2.5f;
            foreach (var sh in _shows)
            {
                if (sh.guns.Count < 2) continue;
                sh.guns[sh.at].SetActive(false);
                sh.at = (sh.at + 1) % sh.guns.Count;
                sh.guns[sh.at].SetActive(true);
                if (sh.label != null) sh.label.text = sh.names[sh.at].ToUpper();
            }
            // slowly turning, so you see the whole gun
        }

        void LateUpdate()
        {
            foreach (var sh in _shows) foreach (var g in sh.guns) if (g.activeSelf) g.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 0.6f) * 25f, 0);
        }

        static bool Owns(string id) => Profile.Current != null && Profile.Current.dlc != null && Profile.Current.dlc.Contains(id);

        float _buyT;
        void Buy(Pack p)
        {
            if (Time.unscaledTime < _buyT) return;
            _buyT = Time.unscaledTime + 0.6f;
            var prof = Profile.Current; if (prof == null || Owns(p.id)) return;
            if (prof.money < p.price) { Sfx.UI(Synth.UiError(), 0.4f); RagoGame.I?.Player?.Toast("Not enough money - the " + p.name + " is $" + p.price.ToString("N0")); return; }
            prof.money -= p.price;
            prof.dlc ??= new List<string>();
            prof.dlc.Add(p.id);
            foreach (var id in p.items) Profile.Deliver(new ItemData(id), p.name);
            prof.Save();
            Sfx.UI(Synth.Cash(), 0.7f);
            RagoGame.I?.Player?.Toast(p.name + " unlocked - new skins at your paint booth" + (p.items.Length > 0 ? ", items on the Armory floor" : ""));
            Redraw();
        }
    }
}
