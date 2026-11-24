using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// DLC packs: what each one is, what it costs, what comes in it, who owns it, and buying it.
    ///
    /// REAL MONEY: packs are never free. To sell them:
    ///   Meta Quest - create an Add-on (durable) for each pack in the Meta developer dashboard with the pack's <c>sku</c>,
    ///                put the app's id in Resources/Rago/meta_app_id.txt, add the Meta XR Platform SDK and the scripting
    ///                define RAGO_META_IAP, then set PaymentsLive = true.
    ///   Steam      - make a DLC for each pack on Steamworks, put its DLC app id in the pack's <c>steamDlc</c>, add
    ///                Steamworks.NET and the define RAGO_STEAM, then set PaymentsLive = true.
    /// Paid packs are re-checked with the store when the game starts, so a player who reinstalls gets them back.
    /// </summary>
    public static class Dlc
    {
        /// <summary>The packs are always paid for with real money - never free. Where there's no store to pay through
        /// (a build without the Meta Platform SDK, the editor, PC without Steam) BUY says so instead of giving it away.
        /// Only the developer's test account can unlock them there, to test.</summary>
        public static readonly bool PaymentsLive = true;

        public class Pack
        {
            public string id, name, tagline, usd, sku; public uint steamDlc; public Color col; public string[] items = new string[0]; public bool seasonal;
            public int cash;   // > 0: bought with in-game cash in the DLC tab / store, not real money
        }

        public static readonly Pack[] Packs =
        {
            new Pack { id = "holiday", name = "HOLIDAY PACK", usd = "$1.99", cash = 25000, sku = "rago_pack_holiday", col = new Color(0.75f, 0.1f, 0.1f), seasonal = true,
                tagline = "festive skins + a candy cane club", items = new[] { "candycane", "impact", "impact", "impact" } },
            new Pack { id = "street", name = "STREET PACK", usd = "$2.99", sku = "rago_pack_street", col = new Color(0.15f, 0.6f, 0.35f), tagline = "loud city paint jobs" },
            new Pack { id = "survivor", name = "SURVIVOR PACK", usd = "$2.99", sku = "rago_pack_survivor", col = new Color(0.2f, 0.4f, 0.65f), tagline = "weathered and wild" },
            new Pack { id = "collector", name = "COLLECTOR'S PACK", usd = "$4.99", sku = "rago_pack_collector", col = new Color(0.55f, 0.3f, 0.75f), tagline = "the rarest finishes" },
            new Pack { id = "gold", name = "GOLD PACK", usd = "$4.99", sku = "rago_pack_gold", col = new Color(0.85f, 0.65f, 0.15f),
                tagline = "solid gold, black gold and mirror chrome", items = new[] { "val_chain" } },
            new Pack { id = "synthwave", name = "SYNTHWAVE PACK", usd = "$2.99", sku = "rago_pack_synthwave", col = new Color(0.9f, 0.2f, 0.7f), tagline = "neon sunsets and laser grids" },
            new Pack { id = "predator", name = "PREDATOR PACK", usd = "$2.99", sku = "rago_pack_predator", col = new Color(0.65f, 0.45f, 0.15f), tagline = "zebra, leopard and python skin" },
            new Pack { id = "elements", name = "ELEMENTS PACK", usd = "$3.99", sku = "rago_pack_elements", col = new Color(0.95f, 0.35f, 0.1f), tagline = "lava, ice, lightning and toxic slime" },
            new Pack { id = "operator", name = "OPERATOR PACK", usd = "$3.99", sku = "rago_pack_operator", col = new Color(0.3f, 0.4f, 0.25f),
                tagline = "pro camo + a field med kit", items = new[] { "ifak", "ifak", "stim" } },
        };

        public static IEnumerable<Pack> Shown() { foreach (var p in Packs) if (!p.seasonal || Festive.On || Owns(p.id)) yield return p; }
        public static Pack Get(string id) { foreach (var p in Packs) if (p.id == id) return p; return null; }
        public static bool Owns(string id) => Profile.Current != null && Profile.Current.dlc != null && Profile.Current.dlc.Contains(id);
        public static string PriceText(Pack p) => Owns(p.id) ? "OWNED" : Price(p);
        /// <summary>What it costs: real money, or for an in-game-cash pack "$25,000 CASH".</summary>
        public static string Price(Pack p) => p.cash > 0 ? "$" + p.cash.ToString("N0") + " CASH" : p.usd;
        public static List<GunSkins.Skin> Skins(Pack p) => GunSkins.All.FindAll(s => s.pack == p.id);

        public static event Action Changed;

        static IStorePayments _store;
        static IStorePayments Store
        {
            get
            {
                if (_store != null) return _store;
#if RAGO_META_IAP
                if (Application.platform == RuntimePlatform.Android) _store = new MetaPayments();
#endif
#if RAGO_STEAM
                if (_store == null && Application.platform != RuntimePlatform.Android) _store = new SteamPayments();
#endif
                return _store;
            }
        }

        static bool _busy;
        /// <summary>BUY: free while payments are off; otherwise the store's own checkout (real money), then it's yours.</summary>
        public static void Buy(Pack p, Action<bool, string> done)
        {
            if (p == null || Owns(p.id)) { done?.Invoke(false, "You already own it"); return; }
            if (_busy) { done?.Invoke(false, "A purchase is already open"); return; }
            if (p.cash > 0)
            {
                // bought with in-game money: straight from your cash, no store checkout
                var prof = Profile.Current;
                if (prof == null || prof.money < p.cash) { done?.Invoke(false, "Not enough cash - " + p.name + " costs $" + p.cash.ToString("N0")); return; }
                prof.money -= p.cash;
                Grant(p);
                done?.Invoke(true, p.name + " unlocked for $" + p.cash.ToString("N0") + "!");
                return;
            }
            var s = Store;
            if (s == null)
            {
                if (Spectator.IsTestAccount) { Grant(p); done?.Invoke(true, p.name + " unlocked (test account)"); return; }
                done?.Invoke(false, "DLC packs are bought through the Meta Quest store - get the game from the Meta Horizon Store to buy them");
                return;
            }
            _busy = true;
            try
            {
                s.Purchase(p, (ok, why) =>
                {
                    _busy = false;
                    if (ok) { Grant(p); done?.Invoke(true, "Thanks! " + p.name + " unlocked"); }
                    else done?.Invoke(false, why ?? "Purchase cancelled");
                });
            }
            catch (Exception e) { _busy = false; Debug.LogWarning("[Rago] purchase: " + e); done?.Invoke(false, "The store couldn't start the purchase"); }
        }

        /// <summary>Gives the pack: its skins at the paint booth, its items on the Deliveries screen.</summary>
        public static void Grant(Pack p)
        {
            var prof = Profile.Current; if (prof == null || p == null) return;
            prof.dlc ??= new List<string>();
            if (prof.dlc.Contains(p.id)) return;
            prof.dlc.Add(p.id);
            foreach (var id in p.items) Profile.Deliver(new ItemData(id), p.name);
            prof.Save();
            Changed?.Invoke();
        }

        /// <summary>At start-up (payments on): whatever the store says this player has bought, they have.</summary>
        public static void Restore()
        {
            if (Store == null) return;
            try
            {
                Store.Owned(ids =>
                {
                    foreach (var p in Packs) if (ids.Contains(p.sku) || ids.Contains(p.steamDlc.ToString())) Grant(p);
                });
            }
            catch (Exception e) { Debug.LogWarning("[Rago] restore purchases: " + e.Message); }
        }
    }

    /// <summary>A store's in-app purchases (Meta Quest add-ons, Steam DLC).</summary>
    public interface IStorePayments
    {
        void Purchase(Dlc.Pack p, Action<bool, string> done);
        void Owned(Action<HashSet<string>> done);   // SKUs (Meta) or DLC app ids (Steam) this player owns
    }

#if RAGO_META_IAP
    /// <summary>Meta Quest: the Platform SDK's in-app purchases (durable add-ons, one per pack SKU).</summary>
    public class MetaPayments : IStorePayments
    {
        void Init() { MetaPlatform.Init(); }   // started at boot (with the ownership check)

        public void Purchase(Dlc.Pack p, Action<bool, string> done)
        {
            Init();
            Oculus.Platform.IAP.LaunchCheckoutFlow(p.sku).OnComplete(msg =>
            {
                if (msg.IsError) { done(false, msg.GetError() != null ? msg.GetError().Message : "Purchase failed"); return; }
                var pur = msg.GetPurchase();
                done(pur != null && pur.Sku == p.sku, null);
            });
        }

        public void Owned(Action<HashSet<string>> done)
        {
            Init();
            Oculus.Platform.IAP.GetViewerPurchases().OnComplete(msg =>
            {
                var set = new HashSet<string>();
                if (!msg.IsError) foreach (var pur in msg.GetPurchaseList()) set.Add(pur.Sku);
                done(set);
            });
        }
    }
#endif

#if RAGO_STEAM
    /// <summary>Steam: each pack is a DLC app; BUY opens its store page in the Steam overlay.</summary>
    public class SteamPayments : IStorePayments
    {
        public void Purchase(Dlc.Pack p, Action<bool, string> done)
        {
            if (p.steamDlc == 0) { done(false, "Not on Steam yet"); return; }
            var app = new Steamworks.AppId_t(p.steamDlc);
            if (Steamworks.SteamApps.BIsDlcInstalled(app)) { done(true, null); return; }
            Steamworks.SteamFriends.ActivateGameOverlayToStore(app, Steamworks.EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
            // Steam tells us when it's bought (the DLC "installs"); until then it's not ours
            Watch(p, done);
        }

        static void Watch(Dlc.Pack p, Action<bool, string> done)
        {
            var go = new GameObject("SteamDlcWatch"); UnityEngine.Object.DontDestroyOnLoad(go);
            var w = go.AddComponent<Watcher>(); w.app = new Steamworks.AppId_t(p.steamDlc); w.done = done;
        }

        class Watcher : MonoBehaviour
        {
            public Steamworks.AppId_t app; public Action<bool, string> done; float _t;
            void Update()
            {
                _t += Time.unscaledDeltaTime;
                if (Steamworks.SteamApps.BIsDlcInstalled(app)) { done(true, null); Destroy(gameObject); }
                else if (_t > 300f) { done(false, "Purchase not finished"); Destroy(gameObject); }
            }
        }

        public void Owned(Action<HashSet<string>> done)
        {
            var set = new HashSet<string>();
            foreach (var p in Dlc.Packs) if (p.steamDlc != 0 && Steamworks.SteamApps.BIsDlcInstalled(new Steamworks.AppId_t(p.steamDlc))) set.Add(p.steamDlc.ToString());
            done(set);
        }
    }
#endif
}
