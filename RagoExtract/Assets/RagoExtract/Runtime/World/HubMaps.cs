using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>Your personal bunker: stash wall, raid kiosk, shooting range, settings, door to the Market.</summary>
    public class BunkerMap : MapBase
    {
        public StashWall Stash;

        public override void Build()
        {
            IsRaid = false;
            Ambience = Synth.Amb.Bunker;
            NavBounds = new Bounds(new Vector3(0, 2, 6), new Vector3(30, 8, 50));
            Lighting(new Color(1f, 0.9f, 0.75f), 0.05f, new Vector3(60, 30, 0), new Color(0.22f, 0.23f, 0.25f), new Color(0.2f, 0.19f, 0.17f), new Color(0.12f, 0.11f, 0.1f),
                new Color(0.1f, 0.1f, 0.1f), 0f, new Color(0.1f, 0.1f, 0.12f), new Color(0.2f, 0.2f, 0.2f), 0f, 0.5f);
            var b = B;
            var conc = Maps.Concrete;
            var paint = Mats.Lit("bunker_paint", new Color(0.24f, 0.3f, 0.26f), 0f, 0.35f, Mats.Plaster, null, 0.5f, 0.3f, true, 0.4f);
            var floor = Mats.Lit("bunker_floor", new Color(0.5f, 0.5f, 0.5f), 0.1f, 0.45f, Mats.Concrete, null, 0.4f, 0.15f, true, 0.4f);
            float W = 16, D = 12, Ht = 3.6f;
            // room
            b.Box(new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), floor, Color.white);
            b.Box(new Vector3(0, Ht + 0.1f, 0), new Vector3(W, 0.2f, D), conc, new Color(0.5f, 0.5f, 0.5f));
            b.Wall(new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f));
            b.Wall(new Vector3(W / 2, 0, -D / 2), new Vector3(-W / 2, 0, -D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f));
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f));
            // west wall opens into the range tunnel
            b.Wall(new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f), new List<Vector4> { new Vector4(D / 2, 3f, 0, 2.6f) });
            // two-tone paint band
            foreach (var w in new[] { (new Vector3(0, 0.6f, D / 2 - 0.16f), new Vector3(W, 1.2f, 0.02f)), (new Vector3(0, 0.6f, -D / 2 + 0.16f), new Vector3(W, 1.2f, 0.02f)), (new Vector3(W / 2 - 0.16f, 0.6f, 0), new Vector3(0.02f, 1.2f, D)) })
                b.Box(w.Item1, w.Item2, paint, Color.white, null, false);
            // pipes & cable trays
            for (int i = 0; i < 3; i++) b.Cyl(new Vector3(-W / 2, Ht - 0.25f - i * 0.12f, D / 2 - 0.4f), new Vector3(W / 2, Ht - 0.25f - i * 0.12f, D / 2 - 0.4f), 0.05f, 0.05f, 10, Maps.Metal, new Color(0.35f + i * 0.1f, 0.3f, 0.25f), false);
            b.Box(new Vector3(0, Ht - 0.3f, -D / 2 + 0.6f), new Vector3(W, 0.06f, 0.4f), Maps.Metal, new Color(0.4f, 0.4f, 0.4f), null, false);
            for (int i = -1; i <= 1; i++)
            {
                b.Box(new Vector3(i * 5, Ht - 0.08f, 0), new Vector3(1.2f, 0.08f, 0.25f), Mats.Lit("lamp_on", new Color(1f, 0.95f, 0.8f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.2f, 2f, 1.6f)), Color.white, null, false);
                b.AddLight(new Vector3(i * 5, Ht - 0.4f, 0), new Color(1f, 0.88f, 0.7f), 9f, 1.4f, i == 0);
            }
            // stash on the north wall
            Stash = StashWall.Create(transform, new Vector3(-2, 0, D / 2 - 0.25f), 0);
            // kiosk on the east side
            Kiosk.Create(transform, new Vector3(W / 2 - 0.9f, 0, -1.5f), Quaternion.Euler(0, 90, 0));
            // workbench + props
            b.Table(new Vector3(4.5f, 0, D / 2 - 0.8f), 0, new Vector2(2.4f, 0.9f), 0.9f);
            b.Box(new Vector3(4.5f, 1.5f, D / 2 - 0.25f), new Vector3(2.4f, 1.0f, 0.05f), Mats.Lit("pegboard", Color.white, 0.1f, 0.3f, Mats.Wood, null, 0.3f, 0.1f), new Color(0.5f, 0.45f, 0.35f), null, false);
            b.Crate(new Vector3(6.6f, 0, 3.5f), 10, new Vector3(1.0f, 0.6f, 0.6f));
            b.Crate(new Vector3(6.4f, 0.6f, 3.6f), -5, new Vector3(0.8f, 0.5f, 0.5f));
            b.Barrel(new Vector3(-6.8f, 0, -5f), new Color(0.2f, 0.3f, 0.2f));
            // bed + locker
            b.Box(new Vector3(-5.8f, 0.3f, -4.6f), new Vector3(2.0f, 0.15f, 0.9f), Maps.Metal, new Color(0.3f, 0.32f, 0.3f), null, true, Synth.Surface.Metal);
            b.Box(new Vector3(-5.8f, 0.45f, -4.6f), new Vector3(1.9f, 0.15f, 0.85f), Mats.Lit("mattress", new Color(0.35f, 0.38f, 0.3f), 0, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0), Color.white, null, false, Synth.Surface.Dirt, 0.05f);
            SettingsBoard.Create(transform, new Vector3(-3.5f, 1.5f, -D / 2 + 0.2f), 180);
            // supply crate with a free stim + bandages each visit so you can always heal up
            var supply = AddCrate(new Vector3(2f, 0, -4.8f), 0, LootCrate.Kind.Medical);
            // travel
            HubProps.TravelDoor(b, transform, new Vector3(2.5f, 0, -D / 2 + 0.2f), 180, "MARKET  >", () => RagoGame.I.GoTo(Area.Market));
            b.Sign(new Vector3(2.5f, 3.1f, -D / 2 + 0.25f), Quaternion.Euler(0, 180, 0), "LIFT - MARKET", new Color(0.9f, 0.8f, 0.6f), 0.12f);
            b.Sign(new Vector3(-2, 3.2f, D / 2 - 0.3f), Quaternion.identity, "RAGO EXTRACT", UI.Accent, 0.22f);
            // shooting range tunnel (west)
            float L = 26;
            b.Box(new Vector3(-W / 2 - L / 2, -0.1f, 0), new Vector3(L, 0.2f, 3.4f), floor, Color.white);
            b.Box(new Vector3(-W / 2 - L / 2, 2.7f, 0), new Vector3(L, 0.2f, 3.4f), conc, new Color(0.45f, 0.45f, 0.45f));
            b.Wall(new Vector3(-W / 2, 0, 1.7f), new Vector3(-W / 2 - L, 0, 1.7f), 2.7f, 0.3f, conc, new Color(0.7f, 0.68f, 0.64f));
            b.Wall(new Vector3(-W / 2 - L, 0, -1.7f), new Vector3(-W / 2, 0, -1.7f), 2.7f, 0.3f, conc, new Color(0.7f, 0.68f, 0.64f));
            b.Wall(new Vector3(-W / 2 - L, 0, 1.7f), new Vector3(-W / 2 - L, 0, -1.7f), 2.7f, 0.3f, conc, new Color(0.4f, 0.35f, 0.3f));
            b.Box(new Vector3(-W / 2 - 0.8f, 1.0f, 0), new Vector3(0.6f, 0.08f, 3f), Maps.WoodPlank, new Color(0.6f, 0.5f, 0.38f), null, true, Synth.Surface.Wood); // shooting bench
            for (int i = 0; i < 3; i++)
            {
                float x = -W / 2 - 7 - i * 8;
                HubProps.SteelTarget(transform, new Vector3(x, 2.4f, -0.8f + i * 0.8f), 0.35f + i * 0.08f);
                b.AddLight(new Vector3(x, 2.4f, 0), new Color(1f, 0.9f, 0.75f), 6f, 0.9f);
                b.Sign(new Vector3(x + 0.5f, 2.3f, 1.55f), Quaternion.identity, (7 + i * 8) + "m", Color.white, 0.12f);
            }
            // a couple of cheap guns on the bench to try
            ItemFactory.Spawn("p9", new Vector3(-W / 2 - 0.8f, 1.12f, -0.6f), Quaternion.Euler(0, -90, 90));
            Spawn(new Vector3(0, 0.05f, -2f), 0);
        }
    }

    /// <summary>The Market: shared hub where players meet, trade with NPC traders and queue for raids.</summary>
    public class MarketMap : MapBase
    {
        public readonly List<TraderStall> Stalls = new List<TraderStall>();

        public override void Build()
        {
            IsRaid = false;
            Safe = true;
            Ambience = Synth.Amb.Market;
            NavBounds = new Bounds(new Vector3(0, 5, 0), new Vector3(70, 20, 50));
            Lighting(new Color(0.55f, 0.6f, 0.85f), 0.25f, new Vector3(40, 200, 0), new Color(0.2f, 0.22f, 0.3f), new Color(0.22f, 0.2f, 0.2f), new Color(0.1f, 0.1f, 0.1f),
                new Color(0.12f, 0.12f, 0.16f), 0.012f, new Color(0.03f, 0.04f, 0.08f), new Color(0.15f, 0.12f, 0.15f), 0.3f, 0.6f);
            var b = B;
            float W = 56, D = 38, H = 11;
            b.Ground(Vector3.zero, new Vector2(W + 20, D + 20), Maps.Asphalt, Synth.Surface.Concrete);
            b.Box(new Vector3(0, 0.01f, 0), new Vector3(W, 0.02f, D), Maps.ConcreteDark, new Color(0.7f, 0.7f, 0.7f), null, false);
            // hangar
            var corr = Maps.Corrugated;
            Color wc = new Color(0.45f, 0.5f, 0.52f);
            b.Wall(new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), H, 0.3f, corr, wc, null, Synth.Surface.Metal);
            b.Wall(new Vector3(W / 2, 0, -D / 2), new Vector3(-W / 2, 0, -D / 2), H, 0.3f, corr, wc, new List<Vector4> { new Vector4(W / 2, 10f, 0, 7f) }, Synth.Surface.Metal);
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), H, 0.3f, corr, wc, null, Synth.Surface.Metal);
            b.Wall(new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), H, 0.3f, corr, wc, null, Synth.Surface.Metal);
            // big door half open + outside darkness
            b.Box(new Vector3(-3, 3.5f, -D / 2 - 0.3f), new Vector3(9f, 7f, 0.15f), corr, wc * 0.8f, null, true, Synth.Surface.Metal);
            // roof trusses
            for (int i = 0; i < 8; i++)
            {
                float x = -W / 2 + (i + 0.5f) * W / 8;
                b.Box(new Vector3(x, H - 0.2f, 0), new Vector3(0.3f, 0.5f, D), Maps.Metal, new Color(0.3f, 0.3f, 0.3f), null, false);
                for (int k = -3; k <= 3; k++) b.Box(new Vector3(x, H - 1f, k * 5), new Vector3(0.12f, 1.5f, 0.12f), Maps.Metal, new Color(0.3f, 0.3f, 0.3f), Quaternion.Euler(30 * Mathf.Sign(k + 0.5f), 0, 0), false);
            }
            b.Box(new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, D), corr, new Color(0.3f, 0.32f, 0.33f), null, true, Synth.Surface.Metal);
            // string lights
            var bulb = Mats.Lit("bulb", new Color(1f, 0.8f, 0.5f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(3f, 2f, 1f));
            for (int s = 0; s < 4; s++)
            {
                float z = -14 + s * 9;
                for (int i = 0; i < 22; i++)
                {
                    float x = -W / 2 + 2 + i * (W - 4) / 21f;
                    float sag = Mathf.Sin(i / 21f * Mathf.PI) * 1.2f;
                    b.Sphere(new Vector3(x, 7.5f - sag, z), Vector3.one * 0.07f, 6, bulb, Color.white, false);
                }
                b.AddLight(new Vector3(-12, 6.5f, z), new Color(1f, 0.75f, 0.45f), 16f, 1.2f);
                b.AddLight(new Vector3(12, 6.5f, z), new Color(1f, 0.75f, 0.45f), 16f, 1.2f);
            }
            // traders around the plaza
            var defs = TraderStall.Defaults();
            var spots = new[] { (new Vector3(-18, 0, 12), 0f), (new Vector3(-6, 0, 14), 0f), (new Vector3(8, 0, 14), 0f), (new Vector3(20, 0, 12), 0f) };
            for (int i = 0; i < defs.Count && i < spots.Length; i++)
                Stalls.Add(TraderStall.Create(transform, spots[i].Item1, spots[i].Item2, defs[i], 100 + i * 17));
            // central kiosk
            Kiosk.Create(transform, new Vector3(0, 0, -2), Quaternion.identity);
            Kiosk.Create(transform, new Vector3(-16, 0, -6), Quaternion.Euler(0, -80, 0));
            b.Sign(new Vector3(0, 4.2f, -2.2f), Quaternion.identity, "RAID KIOSK", UI.Accent, 0.3f);
            // fire barrels + benches + trucks
            foreach (var p in new[] { new Vector3(-10, 0, 0), new Vector3(10, 0, -4), new Vector3(-22, 0, -10), new Vector3(22, 0, -12) })
            {
                b.Barrel(p, new Color(0.3f, 0.2f, 0.15f));
                b.Sphere(p + Vector3.up * 0.95f, new Vector3(0.25f, 0.15f, 0.25f), 8, Mats.Lit("embers", new Color(1f, 0.4f, 0.1f), 0, 0.2f, null, null, 0, 0, false, 0.5f, new Color(3f, 0.9f, 0.2f)), Color.white, false);
                var l = b.AddLight(p + Vector3.up * 1.4f, new Color(1f, 0.55f, 0.25f), 7f, 1.6f);
                l.gameObject.AddComponent<Flicker>();
                b.Box(p + new Vector3(2.2f, 0.4f, 0), new Vector3(0.5f, 0.06f, 2f), Maps.WoodPlank, new Color(0.55f, 0.42f, 0.3f), null, true, Synth.Surface.Wood);
            }
            b.CarWreck(new Vector3(-22, 0, 2), 80, new Color(0.25f, 0.28f, 0.22f));
            b.ShippingContainer(new Vector3(24, 0, 0), 0, new Color(0.5f, 0.15f, 0.12f), false);
            b.ShippingContainer(new Vector3(24, 2.6f, 0), 3, new Color(0.15f, 0.3f, 0.5f), false);
            for (int i = 0; i < 10; i++) b.Crate(new Vector3(b.Rng.Range(-25f, 25f), 0, b.Rng.Range(-16f, -8f)), b.Rng.Range(0, 90f), new Vector3(1f, 0.8f, 1f));
            b.Sign(new Vector3(0, 9f, D / 2 - 0.3f), Quaternion.identity, "THE MARKET", UI.Accent, 0.8f);
            b.Sign(new Vector3(0, 8f, D / 2 - 0.3f), Quaternion.identity, "safe zone - weapons cold", UI.Dim, 0.25f);
            HubProps.TravelDoor(b, transform, new Vector3(W / 2 - 0.3f, 0, -10), 90, "<  BUNKER", () => RagoGame.I.GoTo(Area.Bunker));
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; Spawn(new Vector3(Mathf.Cos(a) * 4, 0.05f, -8 + Mathf.Sin(a) * 3), 0); }
        }
    }

    public class Flicker : MonoBehaviour
    {
        Light _l; float _base;
        void Start() { _l = GetComponent<Light>(); _base = _l.intensity; }
        void Update() { _l.intensity = _base * (0.75f + Mathf.PerlinNoise(Time.time * 6f, GetInstanceID()) * 0.5f); }
    }
}
