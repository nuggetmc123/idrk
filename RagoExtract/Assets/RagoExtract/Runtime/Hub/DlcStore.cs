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
            public string id, name, blurb; public int price; public Color col; public string[] items; public bool seasonal;
        }

        public static readonly Pack[] Packs =
        {
            new Pack { id = "holiday", name = "HOLIDAY PACK", price = 0, col = new Color(0.75f, 0.1f, 0.1f), seasonal = true,
                blurb = "Skins: Candy Cane, Ugly Sweater, Frostbite\nCandy Cane club\n3 impact grenades", items = new[] { "candycane", "impact", "impact", "impact" } },
            new Pack { id = "street", name = "STREET PACK", price = 0, col = new Color(0.15f, 0.6f, 0.35f),
                blurb = "Skins: Neon Circuit, Hazard\nBaseball bat, crowbar\nfrying pan", items = new[] { "bat", "crowbar", "pan" } },
            new Pack { id = "survivor", name = "SURVIVOR PACK", price = 0, col = new Color(0.2f, 0.4f, 0.65f),
                blurb = "Skins: Deep Ocean, Cherry Blossom\nMachete, sledgehammer\n2 impact grenades", items = new[] { "machete", "sledge", "impact", "impact" } },
            new Pack { id = "collector", name = "COLLECTOR'S PACK", price = 0, col = new Color(0.55f, 0.3f, 0.75f),
                blurb = "Skins: Dragon Scale, Galaxy,\nDamascus Steel, Bone\nFire axe", items = new[] { "fireaxe" } },
        };

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
            mb.Sub(0).Box(new Vector3(0, 1.75f, 0.03f), new Vector3(4.6f, 2.5f, 0.06f), new Color(0.06f, 0.06f, 0.08f), 0.02f);
            mb.Sub(1).Box(new Vector3(0, 3.06f, -0.01f), new Vector3(4.6f, 0.04f, 0.03f), Color.white, 0f);
            mb.Sub(1).Box(new Vector3(0, 0.48f, -0.01f), new Vector3(4.6f, 0.04f, 0.03f), Color.white, 0f);
            mb.Build("Board", transform, Mats.Lit("dlc_board", Color.white, 0.3f, 0.6f, Mats.Noise, null, 0.1f, 0.1f),
                Mats.Lit("dlc_neon", new Color(0.4f, 0.8f, 1f), 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(0.6f, 1.4f, 2.2f)));
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.75f, 0.03f); col.size = new Vector3(4.6f, 2.5f, 0.06f);
            UI.Text(transform, "DLC STORE", new Vector3(0, 2.8f, -0.01f), 0.2f, new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "content packs - skins for the paint booth, items to your bunker", new Vector3(0, 2.6f, -0.01f), 0.045f, UI.Dim, TextAnchor.MiddleCenter);
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(transform, false); l.transform.localPosition = new Vector3(0, 2.2f, -1.2f);
            l.type = LightType.Point; l.color = new Color(0.5f, 0.75f, 1f); l.range = 5f; l.intensity = 1f;
            if (Festive.On) Festive.Wreath(transform, new Vector3(0, 3.35f, -0.05f), 0.35f);
            Redraw();
        }

        void Redraw()
        {
            if (_cards != null) Destroy(_cards.gameObject);
            _cards = new GameObject("Cards").transform; _cards.SetParent(transform, false);
            var shown = new List<Pack>();
            foreach (var p in Packs) if (!p.seasonal || Festive.On || Owns(p.id)) shown.Add(p);
            float w = 1.05f, gap = 0.08f, x0 = -(shown.Count - 1) * (w + gap) / 2f;
            for (int i = 0; i < shown.Count; i++)
            {
                var p = shown[i];
                var c = new Vector3(x0 + i * (w + gap), 1.55f, -0.02f);
                var card = UI.Panel(_cards, "Card", c, Quaternion.identity, new Vector2(w, 1.75f), null, true, Skin.Steel);
                // box art: a little 3D game box in the pack's colour
                var box = new MB();
                box.Box(Vector3.zero, new Vector3(0.42f, 0.52f, 0.07f), p.col, 0.01f);
                box.Box(new Vector3(0, 0.19f, -0.036f), new Vector3(0.42f, 0.1f, 0.002f), Color.Lerp(p.col, Color.white, 0.6f), 0f);
                var bgo = box.Build("BoxArt", card, Mats.Lit("dlc_box", Color.white, 0.2f, 0.6f, Mats.Noise, null, 0.05f, 0.05f));
                bgo.transform.localPosition = new Vector3(0, 0.5f, -0.06f); bgo.transform.localRotation = Quaternion.Euler(0, -15, 0);
                UI.Text(bgo.transform, "RAGO", new Vector3(0, 0.19f, -0.04f), 0.04f, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Text(bgo.transform, p.name.Replace(" PACK", "").Replace("'S", ""), new Vector3(0, -0.02f, -0.04f), 0.05f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Text(card, p.name, new Vector3(0, 0.16f, -0.005f), 0.05f, Color.Lerp(p.col, Color.white, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Text(card, p.blurb, new Vector3(-0.47f, 0.07f, -0.005f), 0.034f, Color.white, TextAnchor.UpperLeft);
                bool owned = Owns(p.id);
                UI.Text(card, owned ? "OWNED" : p.price <= 0 ? "FREE" : "$" + p.price.ToString("N0"), new Vector3(0, -0.5f, -0.005f), 0.07f, owned ? new Color(0.5f, 1f, 0.55f) : UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (!owned) { var pp = p; UI.Button(card, p.price <= 0 ? "GET" : "BUY", new Vector3(0, -0.72f, 0), new Vector2(0.5f, 0.12f), () => Buy(pp), new Color(0.2f, 0.42f, 0.2f), BtnStyle.Key); }
            }
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
            foreach (var id in p.items) Profile.Deliver(new ItemData(id));
            prof.Save();
            Sfx.UI(Synth.Cash(), 0.7f);
            RagoGame.I?.Player?.Toast(p.name + " unlocked - new skins at your paint booth, items on the Armory floor");
            Redraw();
        }
    }
}
