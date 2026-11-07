using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The Exchange: a big sell counter that takes anything. Put loot in the bin, read the receipt, press SELL ALL.
    /// Each item fetches the best price any Market trader would pay for it. There's one in the Market plaza and one
    /// in the bunker's main hall. The customer stands at local -Z.
    /// </summary>
    public class SellStation : MonoBehaviour
    {
        public static readonly List<SellStation> All = new List<SellStation>();
        static List<TraderDef> _traders;

        ItemContainer _bin;
        TextMesh _receipt, _values, _total, _cash;

        public static SellStation Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("SellStation");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<SellStation>();
            s.Build();
            return s;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Build()
        {
            Color steel = UI.Lin(new Color(0.22f, 0.23f, 0.24f)), orange = UI.Lin(new Color(0.95f, 0.5f, 0.12f)), blk = UI.Lin(new Color(0.06f, 0.06f, 0.06f)),
                  top = UI.Lin(new Color(0.38f, 0.39f, 0.4f)), yel = UI.Lin(new Color(0.95f, 0.75f, 0.1f));
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.5f, 0), new Vector3(2.7f, 1.0f, 0.8f), steel, 0.02f, 2);                          // counter
            mb.Sub(0).Box(new Vector3(0, 1.02f, -0.02f), new Vector3(2.8f, 0.05f, 0.9f), top, 0.01f);                          // worktop
            mb.Sub(1).Box(new Vector3(0, 0.75f, -0.402f), new Vector3(2.7f, 0.12f, 0.006f), orange, 0f);                       // orange band
            for (int i = 0; i < 12; i++)                                                                                       // hazard kick plate
                mb.Sub(1).Box(new Vector3(-1.24f + i * 0.225f, 0.08f, -0.403f), new Vector3(0.1f, 0.15f, 0.006f), i % 2 == 0 ? yel : blk, 0f, 0, Quaternion.Euler(0, 0, 35));
            mb.Sub(1).Box(new Vector3(-0.3f, 1.05f, -0.02f), new Vector3(1.84f, 0.012f, 0.78f), orange * 0.55f, 0.003f);     // the bin's tray
            foreach (var sx in new[] { -1.3f, 1.3f }) mb.Sub(0).Box(new Vector3(sx, 1.55f, 0.46f), new Vector3(0.1f, 3.1f, 0.1f), steel, 0.01f); // posts
            mb.Sub(0).Box(new Vector3(0, 2.75f, 0.42f), new Vector3(2.9f, 0.62f, 0.1f), blk, 0.02f, 1);                       // header sign
            mb.Sub(1).Box(new Vector3(0, 2.43f, 0.36f), new Vector3(2.9f, 0.03f, 0.04f), orange, 0.004f);                      // trim
            mb.Build("Exchange", transform, Mats.Lit("exchange_steel", Color.white, 0.6f, 0.45f, Mats.Metal, Mats.MetalN, 0.5f, 0.5f),
                Mats.Lit("exchange_paint", Color.white, 0.1f, 0.4f, Mats.Noise, null, 0.4f, 0.3f));
            AddCol(new Vector3(0, 0.5f, 0), new Vector3(2.7f, 1.04f, 0.8f));
            AddCol(new Vector3(-1.3f, 1.55f, 0.46f), new Vector3(0.1f, 3.1f, 0.1f));
            AddCol(new Vector3(1.3f, 1.55f, 0.46f), new Vector3(0.1f, 3.1f, 0.1f));
            AddCol(new Vector3(0, 2.75f, 0.42f), new Vector3(2.9f, 0.62f, 0.1f));
            UI.Text(transform, "SELL", new Vector3(-0.75f, 2.78f, 0.365f), 0.3f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "THE EXCHANGE\nbest price from any trader", new Vector3(0.62f, 2.78f, 0.365f), 0.07f, new Color(0.92f, 0.9f, 0.85f), TextAnchor.MiddleCenter);
            var l = new GameObject("ExchangeLight").AddComponent<Light>();
            l.transform.SetParent(transform, false); l.transform.localPosition = new Vector3(0, 2.3f, -0.6f);
            l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.45f); l.range = 4.5f; l.intensity = 1.4f;

            // the bin: 4 x 2, takes anything
            _bin = ItemContainer.Grid(transform, new Vector3(-0.3f, 1.08f, -0.02f), Quaternion.identity, 4, 2, new Vector2(0.44f, 0.36f), 0.34f);
            _bin.onChanged = Refresh;
            UI.Text(transform, "PUT LOOT HERE", new Vector3(-0.3f, 1.06f, -0.43f), 0.035f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).transform.localRotation = Quaternion.Euler(60, 0, 0);
            // receipt clipboard
            var rp = UI.Panel(transform, "Receipt", new Vector3(1.0f, 1.4f, 0.12f), Quaternion.Euler(-25, 0, 0), new Vector2(0.62f, 0.62f), null, true, Skin.Paper);
            UI.Text(rp, "RECEIPT", new Vector3(0, 0.26f, 0), 0.034f, UI.PaperInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            _receipt = UI.Text(rp, "", new Vector3(-0.28f, 0.21f, 0), 0.022f, UI.PaperInk, TextAnchor.UpperLeft);
            _values = UI.Text(rp, "", new Vector3(0.28f, 0.21f, 0), 0.022f, UI.PaperInk, TextAnchor.UpperRight, FontStyle.Bold);
            _total = UI.Text(rp, "", new Vector3(0, -0.13f, 0), 0.032f, UI.PaperInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            _cash = UI.Text(rp, "", new Vector3(0, -0.18f, 0), 0.02f, UI.PaperDim, TextAnchor.MiddleCenter);
            UI.Button(rp, "SELL ALL", new Vector3(0, -0.25f, 0), new Vector2(0.34f, 0.08f), Sell, null, BtnStyle.Stamp);
            Refresh();
        }

        void AddCol(Vector3 c, Vector3 s) { var b = gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        /// <summary>Best offer for an item across the Market's traders, and who makes it.</summary>
        public static int Offer(Item it, out string who)
        {
            if (_traders == null) _traders = TraderStall.Defaults();
            int best = 0; who = "";
            if (it == null || it.def == null) return 0;
            foreach (var t in _traders)
            {
                if (t.buys != null && !t.buys(it.def)) continue;
                int v = Mathf.Max(1, Mathf.RoundToInt(it.Value * t.sellRate * (it.def.kind == ItemKind.Valuable ? t.valuableBonus : 1f)));
                if (v > best) { best = v; who = t.name; }
            }
            return best;
        }

        void Refresh()
        {
            if (_receipt == null) return;
            var sb = new System.Text.StringBuilder(); var vb = new System.Text.StringBuilder();
            int total = 0, n = 0;
            foreach (var s in _bin.slots)
            {
                if (s.item == null) continue;
                int v = Offer(s.item, out _);
                total += v; n++;
                if (n <= 8)
                {
                    string name = s.item.def != null ? s.item.def.name : "item";
                    if (name.Length > 22) name = name.Substring(0, 21) + ".";
                    sb.Append(name).Append('\n'); vb.Append('$').Append(v.ToString("N0")).Append('\n');
                }
            }
            _receipt.text = n == 0 ? "Put anything you want to sell\nin the bin on the counter.\n\nEach item gets the best price\nany trader in the Market pays." : sb.ToString() + (n > 8 ? "+ " + (n - 8) + " more" : "");
            _values.text = n == 0 ? "" : vb.ToString();
            _total.text = n == 0 ? "" : "TOTAL  $" + total.ToString("N0");
            _cash.text = Profile.Current != null ? "your cash  $" + Profile.Current.money.ToString("N0") : "";
        }

        void Sell()
        {
            int total = 0, n = 0;
            foreach (var s in _bin.slots)
                if (s.item != null) { total += Offer(s.item, out _); n++; var it = s.item; s.Release(it); Destroy(it.gameObject); }
            if (n == 0) { Sfx.UI(Synth.UiError(), 0.3f); RagoGame.I?.Player?.Toast("Put items in the bin first"); return; }
            Profile.Current.money += total;
            Profile.Current.Save();
            Sfx.UI(Synth.Cash(), 0.7f);
            RagoGame.I?.Player?.Toast("Sold " + n + " item(s) for $" + total.ToString("N0"));
            Refresh();
        }

        /// <summary>Leaving: anything still in the bin goes back to the stash, unsold.</summary>
        public void SweepToStash(List<ItemData> into)
        {
            if (_bin == null) return;
            foreach (var s in _bin.slots) if (s.item != null) { into.Add(s.item.Save()); var it = s.item; s.Release(it); Destroy(it.gameObject); }
        }
    }
}
