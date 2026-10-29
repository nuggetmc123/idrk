using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The hub: a forward operating base where players meet and party up. A
    /// deployment hangar with a gate for each game mode, an armory with all 22
    /// guns, a live-fire range, a helipad, barracks, towers and HESCO walls.
    /// </summary>
    public static class Hub
    {
        public static readonly (string mode, string label, string info)[] Portals =
        {
            ("tdm", "TEAM DEATHMATCH", "5 v 5 · first to 40"),
            ("ffa", "FREE FOR ALL", "8 players · first to 20"),
            ("1v1", "1 v 1", "first to 10"),
            ("2v2", "2 v 2", "first to 15"),
            ("3v3", "3 v 3", "first to 20"),
            ("ctf", "CAPTURE THE FLAG", "5 v 5 · 3 captures"),
        };

        public static void Build(MapKit k)
        {
            k.world.ApplyTheme(new MapTheme
            {
                sky = SkyStyle.Day,
                skyTint = Prims.Hex("7fb2ff"), groundTint = Prims.Hex("5a5a48"), exposure = 1.3f, sunIntensity = 1.25f,
                sun = Prims.Hex("ffe9c8"), sunEuler = new Vector3(38, -42, 0),
                ambSky = Prims.Hex("9fb8d8"), ambEquator = Prims.Hex("8a8a78"), ambGround = Prims.Hex("4a4535"),
                fog = Prims.Hex("c9d4de"), fogDensity = 0.006f
            });
            var dirt = MapKit.M("8a7a5e", 0.08f, 0f, Surface.Dirt);
            var concrete = MapKit.M("a3a199", 0.25f, 0f, Surface.Concrete);
            var asphalt = MapKit.M("3a3a3c", 0.4f, 0f, Surface.Asphalt);
            var paint = MapKit.M("e8c33a", 0.35f);
            var white = MapKit.M("e6e6e0", 0.35f);
            k.Ground(70, 70, dirt);

            // ---- the parade ground (concrete apron) and roads ----
            k.Box(new Vector3(0, 0.02f, 4), new Vector3(46, 0.05f, 40), concrete, 0, false);
            for (int x = -20; x <= 20; x += 5) k.Box(new Vector3(x, 0.046f, 4), new Vector3(0.12f, 0.01f, 40), MapKit.M("8f8d86", 0.2f, 0f, Surface.Concrete), 0, false);
            k.Box(new Vector3(0, 0.03f, -40), new Vector3(7, 0.05f, 12), asphalt, 0, false);
            k.Box(new Vector3(31, 0.03f, -4), new Vector3(16, 0.05f, 7), asphalt, 0, false);
            k.Box(new Vector3(-31, 0.03f, -4), new Vector3(16, 0.05f, 7), asphalt, 0, false);
            for (int z = -14; z <= 22; z += 4) k.Box(new Vector3(0, 0.05f, z), new Vector3(0.2f, 0.01f, 2f), paint, 0, false);
            k.Box(new Vector3(0, 0.05f, 23.4f), new Vector3(44, 0.01f, 0.3f), paint, 0, false);
            for (int i = 0; i < 22; i++) k.Box(new Vector3(-21f + i * 2f, 0.052f, 23.4f), new Vector3(0.9f, 0.012f, 0.3f), MapKit.M("1a1a1a", 0.3f), 45, false);

            // ---- flagpole + base sign in the middle ----
            MilitaryProps.Flagpole(k, new Vector3(0, 0, 9));
            var plaque = k.Block(new Vector3(0, 0, 6.5f), new Vector3(3.4f, 1.1f, 0.5f), concrete);
            var t1 = k.Sign(new Vector3(0, 0.8f, 6.24f), 180, "FLOPZONE", 0.4f, new Color(0.95f, 0.85f, 0.45f));
            t1.fontStyle = FontStyle.Bold;
            k.Sign(new Vector3(0, 0.42f, 6.24f), 180, "FORWARD OPERATING BASE", 0.14f, Color.white);

            // ---- the deployment hangar with a gate for each mode ----
            Hangar(k);

            // ---- armory: the gun table under a camo net, ammo stacked around ----
            Armory(k);

            // ---- shooting range to the south ----
            Range(k);

            // ---- helipad + motor pool to the east ----
            var pad = Prims.Cyl(k.S, new Vector3(31, 0.06f, 12), 13f, 0.12f, concrete, Vector3.up);
            pad.name = "helipad";
            Prims.Cyl(k.S, new Vector3(31, 0.125f, 12), 11.6f, 0.01f, white, Vector3.up);
            Prims.Cyl(k.S, new Vector3(31, 0.13f, 12), 11.0f, 0.01f, concrete, Vector3.up);
            k.Box(new Vector3(29.5f, 0.14f, 12), new Vector3(0.6f, 0.01f, 4f), white, 0, false);
            k.Box(new Vector3(32.5f, 0.14f, 12), new Vector3(0.6f, 0.01f, 4f), white, 0, false);
            k.Box(new Vector3(31, 0.14f, 12), new Vector3(3f, 0.01f, 0.6f), white, 0, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                k.Box(new Vector3(31 + Mathf.Cos(a) * 6.3f, 0.15f, 12 + Mathf.Sin(a) * 6.3f), new Vector3(0.25f, 0.12f, 0.25f), Prims.Emissive(Prims.Hex("ffb02e"), 2.5f), 0, false);
            }
            MilitaryProps.Helicopter(k, new Vector3(31, 0.12f, 12.5f), -100);
            MilitaryProps.Truck(k, new Vector3(32, 0, -12), 90);
            MilitaryProps.Jeep(k, new Vector3(26, 0, -20), 30);
            MilitaryProps.Jeep(k, new Vector3(36, 0, -22), -20);
            MilitaryProps.CamoNet(k, new Vector3(32, 0, -18), new Vector2(14, 10), 3.6f);
            for (int i = 0; i < 6; i++) MilitaryProps.Drum(k, new Vector3(38 + (i % 2) * 0.7f, 0, -28 + (i / 2) * 0.7f), i % 3 == 0 ? "8a2a1a" : "3f4a2a");
            MilitaryProps.Radar(k, new Vector3(38, 0, 24));

            // ---- barracks + tents to the west ----
            MilitaryProps.Quonset(k, new Vector3(-33, 0, 14), 90, 13);
            MilitaryProps.Quonset(k, new Vector3(-33, 0, -14), 90, 13);
            MilitaryProps.Tent(k, new Vector3(-24, 0, 2), 90);
            MilitaryProps.Tent(k, new Vector3(-24, 0, -4), 90);
            MilitaryProps.Tent(k, new Vector3(-36, 0, 0), -90);
            for (int i = 0; i < 4; i++) MilitaryProps.Pallet(k, new Vector3(-26 + i * 1.6f, 0, -26), 0);
            MilitaryProps.Sandbags(k, new Vector3(-22, 0, -22), new Vector3(-16, 0, -22), 3);
            MilitaryProps.Sandbags(k, new Vector3(16, 0, -22), new Vector3(22, 0, -22), 3);

            // ---- perimeter: HESCO walls, a fence by the gate, towers in the corners, floodlights ----
            float hx = 42.5f, hz = 44.5f;
            MilitaryProps.Hesco(k, new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), 1.8f);
            MilitaryProps.Hesco(k, new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), 1.8f);
            MilitaryProps.Hesco(k, new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), 1.8f);
            MilitaryProps.Hesco(k, new Vector3(-hx, 0, -hz), new Vector3(-6, 0, -hz), 1.8f);
            MilitaryProps.Hesco(k, new Vector3(6, 0, -hz), new Vector3(hx, 0, -hz), 1.8f);
            MilitaryProps.Fence(k, new Vector3(-6, 0, -hz + 0.6f), new Vector3(6, 0, -hz + 0.6f), 3f);
            foreach (var c in new[] { new Vector3(-38.5f, 0, -40.5f), new Vector3(38.5f, 0, -40.5f), new Vector3(-38.5f, 0, 40.5f), new Vector3(38.5f, 0, 40.5f) })
                MilitaryProps.Watchtower(k, c, Mathf.Atan2(-c.x, -c.z) * Mathf.Rad2Deg);
            MilitaryProps.FloodLight(k, new Vector3(-19, 0, 21), 160);
            MilitaryProps.FloodLight(k, new Vector3(19, 0, 21), 200);
            MilitaryProps.FloodLight(k, new Vector3(-19, 0, -14), 20);
            MilitaryProps.FloodLight(k, new Vector3(19, 0, -14), -20);
            for (int i = 0; i < 6; i++) MilitaryProps.Jersey(k, new Vector3(-21 + i * 8.4f, 0, -8.5f), 90);

            // ---- hills and trees outside the wire ----
            k.Backdrop(40, 55, 110, p => { k.Pine(p, k.R(8, 14)); return null; });
            k.Backdrop(14, 120, 220, p => { Prims.Ball(k.S, p, new Vector3(k.R(60, 120), k.R(25, 55), k.R(60, 120)), MapKit.M("5f6b48", 0.05f, 0f, Surface.Grass)); return null; });

            for (int i = 0; i < 10; i++) k.world.ffaSpawns.Add(new Vector3(k.R(-5, 5), 0, k.R(-5, -2)));
            k.world.killY = -20;
        }

        static void Hangar(MapKit k)
        {
            var wall = MapKit.M("6f7563", 0.35f, 0.3f, Surface.PaintedMetal);
            var trim = MilitaryProps.OliveDark;
            var concrete = MilitaryProps.ConcreteMat;
            float front = 25f, back = 38f, half = 24.5f, h = 9f, bayTop = 6f;
            k.Box(new Vector3(0, 0.03f, (front + back) / 2), new Vector3(half * 2, 0.06f, back - front), MapKit.M("5a5b58", 0.4f, 0f, Surface.Concrete), 0, false);
            k.Box(new Vector3(0, h / 2, back), new Vector3(half * 2, h, 0.6f), wall);
            k.Box(new Vector3(-half, h / 2, (front + back) / 2), new Vector3(0.6f, h, back - front), wall);
            k.Box(new Vector3(half, h / 2, (front + back) / 2), new Vector3(0.6f, h, back - front), wall);
            k.Box(new Vector3(0, h + 0.2f, (front + back) / 2), new Vector3(half * 2 + 1.2f, 0.4f, back - front + 1.2f), trim);
            k.Box(new Vector3(0, (bayTop + h) / 2, front), new Vector3(half * 2, h - bayTop, 0.6f), wall);
            k.Box(new Vector3(0, bayTop + 0.15f, front - 0.35f), new Vector3(half * 2, 0.3f, 0.2f), MilitaryProps.Hazard, 0, false);
            for (int i = 0; i < 8; i++)
            {
                float x = -24.5f + i * 7f;
                k.Box(new Vector3(x, bayTop / 2, front), new Vector3(1f, bayTop, 0.9f), concrete);
                for (int s = 0; s < 6; s++) k.BoxRot(new Vector3(x, 0.4f + s * 0.95f, front - 0.46f), new Vector3(1.0f, 0.3f, 0.02f), s % 2 == 0 ? MilitaryProps.Hazard : MapKit.M("1a1a1a", 0.3f), Quaternion.Euler(0, 0, 30), false);
            }
            // roof edge lights and the big sign
            for (int i = 0; i < 12; i++) k.Box(new Vector3(-22 + i * 4f, h - 0.4f, front - 0.32f), new Vector3(0.5f, 0.15f, 0.06f), Prims.Emissive(Prims.Hex("fff1c9"), 2.2f), 0, false);
            var sign = k.Sign(new Vector3(0, 7.9f, front - 0.33f), 180, "DEPLOYMENT", 0.9f, new Color(0.95f, 0.9f, 0.7f));
            sign.fontStyle = FontStyle.Bold;
            k.Sign(new Vector3(0, 6.85f, front - 0.33f), 180, "walk through a gate to deploy  ·  the party leader takes the whole squad", 0.24f, new Color(0.85f, 0.85f, 0.8f));
            // inside: dim lights and crates so the gates have depth
            for (int i = 0; i < 6; i++) k.Box(new Vector3(-17.5f + i * 7f, h - 0.3f, 31.5f), new Vector3(3f, 0.1f, 0.4f), Prims.Emissive(Prims.Hex("e8f0ff"), 1.4f), 0, false);
            for (int i = 0; i < 5; i++) k.CrateStack(new Vector3(-20 + i * 10f, 0, 36), 0);
            MilitaryProps.Radar(k, new Vector3(16, h + 0.4f, 32));
            Prims.Cyl(k.S, new Vector3(-16, h + 3.4f, 33), 0.12f, 6f, MilitaryProps.Steel, Vector3.up);
            k.Box(new Vector3(-16, h + 6.5f, 33), new Vector3(0.2f, 0.2f, 0.2f), Prims.Emissive(Prims.Hex("ff3020"), 3f), 0, false);
            // the six gates
            for (int i = 0; i < Portals.Length; i++)
                HubPortal.Create(k, new Vector3(-17.5f + i * 7f, 0, front), 180f, Portals[i].mode, Portals[i].label, Portals[i].info, i);
        }

        static void Armory(MapKit k)
        {
            MilitaryProps.CamoNet(k, new Vector3(0, 0, -12f), new Vector2(26, 6), 3.4f);
            k.Sign(new Vector3(0, 2.7f, -8.9f), 0, "ARMORY", 0.5f, new Color(0.95f, 0.85f, 0.45f));
            var tableM = MapKit.M("5a4632", 0.3f, 0f, Surface.Plank);
            var leg = MilitaryProps.DarkSteel;
            float tx = -10.5f;
            foreach (GunClass cls in System.Enum.GetValues(typeof(GunClass)))
            {
                int n = 0;
                foreach (var g in GunCatalog.All) if (g.cls == cls) n++;
                float len = n * 0.85f;
                var center = new Vector3(tx + len / 2, 0, -12f);
                k.Box(center + Vector3.up * 0.92f, new Vector3(len, 0.06f, 1.1f), tableM);
                k.Box(center + Vector3.up * 0.45f, new Vector3(len - 0.2f, 0.9f, 0.9f), MilitaryProps.OliveDark);
                foreach (var dx in new[] { -len / 2 + 0.08f, len / 2 - 0.08f }) k.Box(center + new Vector3(dx, 0.46f, 0.48f), new Vector3(0.06f, 0.92f, 0.06f), leg, 0, false);
                k.Sign(center + new Vector3(0, 1.75f, 0.3f), 0, GunCatalog.ClassName(cls).ToUpperInvariant(), 0.22f, UIKit.Accent);
                int j = 0;
                foreach (var g in GunCatalog.All)
                {
                    if (g.cls != cls) continue;
                    var slotPos = new Vector3(tx + 0.42f + j * 0.85f, 1.12f, -12f);
                    var slot = Prims.Empty("slot " + g.name, k.D, slotPos).transform;
                    slot.rotation = Quaternion.Euler(0, 180, 90);
                    if (Application.isPlaying)
                    {
                        var gun = Gun.Spawn(g, slotPos, slot.rotation);
                        gun.home = slot;
                        gun.returnDelay = 6f;
                        gun.displayOnly = true;
                        gun.GoHome();
                    }
                    else GunModel.Build(g, slot);   // editor preview: just the look
                    var label = UIKit.Text(k.D, g.name, slotPos + new Vector3(0, -0.06f, 0.56f), 0.055f, Color.white);
                    label.transform.rotation = Quaternion.Euler(60, 180, 0);
                    j++;
                }
                tx += len + 0.6f;
            }
            for (int i = 0; i < 4; i++) MilitaryProps.AmmoCrate(k, new Vector3(-12.5f, 0, -13.5f + i * 0.6f), 90);
            for (int i = 0; i < 4; i++) MilitaryProps.AmmoCrate(k, new Vector3(12.5f, 0, -13.5f + i * 0.6f), -90);
        }

        static void Range(MapKit k)
        {
            k.Box(new Vector3(0, 0.03f, -31), new Vector3(24, 0.06f, 30), MapKit.M("7a6a52", 0.1f, 0f, Surface.Dirt), 0, false);
            MilitaryProps.Hesco(k, new Vector3(-12.6f, 0, -16), new Vector3(-12.6f, 0, -46), 2.2f);
            MilitaryProps.Hesco(k, new Vector3(12.6f, 0, -16), new Vector3(12.6f, 0, -46), 2.2f);
            Prims.Ball(k.S, new Vector3(0, 0, -46.5f), new Vector3(30, 9, 6), MapKit.M("7a6a52", 0.05f, 0f, Surface.Dirt), true);
            MilitaryProps.Sandbags(k, new Vector3(-11.5f, 0, -17.2f), new Vector3(11.5f, 0, -17.2f), 2);
            for (int x = -9; x <= 9; x += 6) k.Box(new Vector3(x - 3, 0.5f, -24), new Vector3(0.12f, 1.0f, 14), MapKit.M("6b5536", 0.15f, 0f, Surface.Wood));
            k.Sign(new Vector3(0, 3.6f, -16.4f), 0, "LIVE FIRE RANGE", 0.6f, Color.white);
            int ti = 0;
            foreach (var z in new[] { -24f, -32f, -42f })
                for (int x = -6; x <= 6; x += 6)
                    ShootingTarget.Create(k.D, new Vector3(x + (ti++ % 2 == 0 ? 1 : -1) * 0.8f, 0, z), 180f);
        }
    }
}
