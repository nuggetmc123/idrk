using System;
using System.Collections.Generic;
using System.Linq;
using Rago.Relay;

namespace RagoServer
{
    /// <summary>
    /// The black market, kept on the server: every player's shop listings are stored here, so a shop stays open while its
    /// owner is offline. A buyer pays (or hands over the items the seller wants) through the server; the server takes
    /// the listing off the shop at once (so it can't sell twice) and keeps the sale for the seller - money and items are
    /// handed over the next time the seller connects (straight away if they're online).
    /// </summary>
    static partial class Program
    {
        sealed class Listing { public int Id; public string Item, Want; public int Price; }
        sealed class Shop { public string Key, Name; public List<Listing> Items = new List<Listing>(); public HashSet<int> Gone = new HashSet<int>(); }
        sealed class Sale { public string SellerKey, Buyer; public int EntryId, Money; public List<string> Items = new List<string>(); }

        static readonly Dictionary<string, Shop> ShopsByKey = new Dictionary<string, Shop>();
        static readonly List<Sale> Sales = new List<Sale>();

        // ------------------------------------------------------------------------------------------- files
        static string B64(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? ""));
        static string Un64(string s) { try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s ?? "")); } catch { return ""; } }

        /// <summary>Raise with the game's Profile.Season: the first start after that clears the black market (every shop,
        /// listing, sold-item record and unpaid sale), so a season wipe leaves nothing behind on the server either.</summary>
        const int MarketSeason = 2;

        static void LoadMarket()
        {
            int season = 1;
            foreach (var f in Lines("season.tsv")) if (f.Length >= 1 && int.TryParse(f[0], out var sv)) season = sv;
            if (season < MarketSeason)
            {
                Save("shops.tsv", new string[0]); Save("sales.tsv", new string[0]);
                Save("season.tsv", new[] { MarketSeason.ToString() });
                Log("NEW SEASON " + MarketSeason + ": black market cleared (every shop, listing and unpaid sale)");
            }
            foreach (var f in Lines("shops.tsv"))
            {
                if (f.Length < 4) continue;
                var sh = new Shop { Key = f[0], Name = Un64(f[1]) };
                foreach (var g in f[2].Split(',')) if (int.TryParse(g, out var gi)) sh.Gone.Add(gi);
                foreach (var e in f[3].Split(',')) { var p = Un64(e).Split('\u0001'); if (p.Length >= 4 && int.TryParse(p[0], out var id) && int.TryParse(p[2], out var pr)) sh.Items.Add(new Listing { Id = id, Item = p[1], Price = pr, Want = p[3] }); }
                ShopsByKey[sh.Key] = sh;
            }
            foreach (var f in Lines("sales.tsv"))
                if (f.Length >= 5 && int.TryParse(f[1], out var eid) && int.TryParse(f[2], out var m))
                    Sales.Add(new Sale { SellerKey = f[0], EntryId = eid, Money = m, Buyer = Un64(f[3]), Items = f[4].Length == 0 ? new List<string>() : f[4].Split(',').Select(Un64).ToList() });
            Log("black market: " + ShopsByKey.Values.Sum(s => s.Items.Count) + " listings in " + ShopsByKey.Count + " shops, " + Sales.Count + " sales waiting for their sellers");
        }
        static void SaveShops() => Save("shops.tsv", ShopsByKey.Values.Select(s => string.Join("\t", Clean(s.Key), B64(s.Name), string.Join(",", s.Gone.TakeLast(200)),
            string.Join(",", s.Items.Select(i => B64(i.Id + "\u0001" + i.Item + "\u0001" + i.Price + "\u0001" + (i.Want ?? "")))))));
        static void SaveSales() => Save("sales.tsv", Sales.Select(x => string.Join("\t", Clean(x.SellerKey), x.EntryId, x.Money, B64(x.Buyer), string.Join(",", x.Items.Select(B64)))));

        static byte[] SoldFrame(Sale s)
        {
            var w = new W(P.ShopSold).I32(s.EntryId).I32(s.Money).Str(s.Buyer).U16(s.Items.Count);
            foreach (var i in s.Items) w.Str(i);
            return w.Frame();
        }

        /// <summary>A seller connected: hand over everything sold while they were away.</summary>
        static void DeliverSales(Client c)
        {
            if (string.IsNullOrEmpty(c.Key)) return;
            var mine = Sales.Where(s => s.SellerKey == c.Key).ToList();
            if (mine.Count == 0) return;
            foreach (var s in mine) Send(c, SoldFrame(s));
            Sales.RemoveAll(s => s.SellerKey == c.Key);
            SaveSales();
            Log(c + ": " + mine.Count + " black market sale(s) handed over");
        }

        // ------------------------------------------------------------------------------------------- requests
        /// <summary>Is this item (JSON of the game's ItemData) the kind the seller asked for?</summary>
        static bool IsItem(string json, string id) => !string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(id) && json.Contains("\"id\":\"" + id + "\"");

        static void HandleMarket(Client c, byte type, R r)
        {
            if (string.IsNullOrEmpty(c.Key)) return;   // old game versions: no shop on the server
            switch (type)
            {
                case P.ShopPut:
                {
                    // the seller's whole shop as their game has it (listings they've sold since are dropped)
                    int n = r.U16();
                    if (!ShopsByKey.TryGetValue(c.Key, out var sh)) ShopsByKey[c.Key] = sh = new Shop { Key = c.Key };
                    sh.Name = c.Name;
                    var items = new List<Listing>();
                    for (int i = 0; i < n && i < 40; i++)
                    {
                        var l = new Listing { Id = r.I32(), Item = r.Str(), Price = Math.Max(0, r.I32()), Want = r.Str() };
                        if (!sh.Gone.Contains(l.Id) && l.Item.Length < 8000) items.Add(l);
                    }
                    sh.Items = items;
                    if (sh.Items.Count == 0 && sh.Gone.Count == 0) ShopsByKey.Remove(c.Key);
                    SaveShops();
                    break;
                }
                case P.ShopGet:
                {
                    int id = r.I32();
                    var list = ShopsByKey.Values.Where(s => s.Key != c.Key && s.Items.Count > 0).Take(60).ToList();
                    var w = new W(P.ShopsIn).I32(id).U16(list.Count);
                    foreach (var s in list)
                    {
                        bool online = Clients.Values.Any(x => x.Key == s.Key);
                        w.Str(s.Key).Str(s.Name).Bool(online).U16(s.Items.Count);
                        foreach (var l in s.Items) w.I32(l.Id).Str(l.Item).I32(l.Price).Str(l.Want ?? "");
                    }
                    Send(c, w.Frame());
                    break;
                }
                case P.ShopBuy:
                {
                    string sellerKey = r.Str(); int eid = r.I32(), paid = r.I32(); int n = r.U16();
                    var offers = new List<string>(); for (int i = 0; i < n && i < 8; i++) offers.Add(r.Str());
                    string Fail(string why) { Send(c, new W(P.BuyResult).I32(eid).Str(sellerKey).Bool(false).Str(why).Frame()); return null; }
                    if (sellerKey == c.Key) { Fail("That's your own shop"); break; }
                    if (!ShopsByKey.TryGetValue(sellerKey, out var sh)) { Fail("That shop has closed"); break; }
                    var l = sh.Items.FirstOrDefault(x => x.Id == eid);
                    if (l == null) { Fail("Already sold"); break; }
                    bool forMoney = string.IsNullOrEmpty(l.Want);
                    if (forMoney && paid != l.Price) { Fail("The price changed"); break; }
                    if (!forMoney && (offers.Count < Math.Max(1, l.Price) || !offers.All(o => IsItem(o, l.Want)))) { Fail("That's not what they want"); break; }
                    // sold: off the shop for good, the sale kept for the seller
                    sh.Items.Remove(l); sh.Gone.Add(l.Id);
                    var sale = new Sale { SellerKey = sellerKey, EntryId = l.Id, Money = forMoney ? l.Price : 0, Buyer = c.Name, Items = forMoney ? new List<string>() : offers.Take(Math.Max(1, l.Price)).ToList() };
                    var seller = Clients.Values.FirstOrDefault(x => x.Key == sellerKey);
                    if (seller != null) Send(seller, SoldFrame(sale)); else { Sales.Add(sale); SaveSales(); }
                    SaveShops();
                    Send(c, new W(P.BuyResult).I32(eid).Str(sellerKey).Bool(true).Str(l.Item).Frame());
                    Log("black market: " + c.Name + " bought listing " + eid + " from " + sh.Name + (seller == null ? " (seller offline - kept for them)" : ""));
                    break;
                }
                case P.ShopUnlist:
                {
                    int eid = r.I32();
                    bool ok = ShopsByKey.TryGetValue(c.Key, out var sh) && sh.Items.RemoveAll(x => x.Id == eid) > 0;
                    if (ok) { sh.Gone.Add(eid); SaveShops(); }
                    Send(c, new W(P.UnlistResult).I32(eid).Bool(ok).Frame());
                    break;
                }
            }
        }
    }
}
