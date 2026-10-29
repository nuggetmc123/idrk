using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The hub: a plaza where players meet and party up, with a portal for
    /// each game mode, a shooting range and a table with all 22 guns.
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
                skyTint = Prims.Hex("7fb2ff"), groundTint = Prims.Hex("5a6a4a"), exposure = 1.35f, sunIntensity = 1.2f,
                sun = Prims.Hex("fff3dd"), sunEuler = new Vector3(48, -35, 0),
                ambSky = Prims.Hex("a7c4ea"), ambEquator = Prims.Hex("8a9a86"), ambGround = Prims.Hex("4a5038"),
                fog = Prims.Hex("c4d8f0"), fogDensity = 0.005f
            });
            var grass = MapKit.M("5f9a3f", 0.1f);
            var tile = MapKit.M("d8d2c4", 0.3f);
            var tileDark = MapKit.M("a89f8c", 0.3f);
            var stone = MapKit.M("bfb8a8", 0.2f);
            k.Ground(70, 70, grass);
            k.InvisibleFence(44, 46);
            // plaza
            Prims.Cyl(k.S, new Vector3(0, 0.02f, 0), 52f, 0.04f, tile, Vector3.up);
            Prims.Cyl(k.S, new Vector3(0, 0.025f, 0), 53.5f, 0.03f, tileDark, Vector3.up);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                k.BoxRot(new Vector3(0, 0.045f, 0), new Vector3(0.25f, 0.01f, 50f), tileDark, Quaternion.Euler(0, a, 0), false);
            }
            // fountain
            Prims.Cyl(k.S, new Vector3(0, 0.4f, 0), 7f, 0.8f, stone, Vector3.up, true);
            Prims.Cyl(k.S, new Vector3(0, 0.82f, 0), 6.2f, 0.04f, Prims.Lit(Prims.Hex("3a8fd6"), 0.95f), Vector3.up);
            Prims.Cyl(k.S, new Vector3(0, 1.6f, 0), 0.9f, 2.4f, stone, Vector3.up, true);
            var orb = Prims.Sphere(k.D, new Vector3(0, 3.3f, 0), 1.1f, MapKit.Glow("ff9a2e", 2.4f));
            orb.AddComponent<Spinner>().bob = 0.15f;

            // portals in an arc to the north
            for (int i = 0; i < Portals.Length; i++)
            {
                float a = Mathf.Lerp(150f, 30f, i / (float)(Portals.Length - 1)) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a) * 21f, 0, Mathf.Sin(a) * 21f);
                float yaw = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg; // facing the fountain
                HubPortal.Create(k, pos, yaw, Portals[i].mode, Portals[i].label, Portals[i].info, i);
            }
            var title = k.Sign(new Vector3(0, 9.5f, 27f), 180, "FLOPZONE", 3.2f, UIKit.Accent);
            title.fontStyle = FontStyle.Bold;
            k.Sign(new Vector3(0, 7.6f, 27f), 180, "walk into a portal to play  ·  party leader takes the whole party", 0.45f, Color.white);

            // shooting range to the south
            var wood = MapKit.M("8a6a45", 0.2f);
            var wall = MapKit.M("6b6f76", 0.3f);
            k.Box(new Vector3(0, 0.03f, -31), new Vector3(24, 0.06f, 30), MapKit.M("7a7266", 0.2f), 0, false);
            k.Box(new Vector3(-12.5f, 1.5f, -31), new Vector3(1, 3, 30), wall);
            k.Box(new Vector3(12.5f, 1.5f, -31), new Vector3(1, 3, 30), wall);
            k.Box(new Vector3(0, 3, -46.5f), new Vector3(26, 6, 1), wall);
            for (int x = -9; x <= 9; x += 6) k.Box(new Vector3(x - 3, 0.6f, -24), new Vector3(0.15f, 1.2f, 16), wood);
            k.Sign(new Vector3(0, 4.2f, -16.2f), 0, "SHOOTING RANGE", 1.0f, Color.white);
            int ti = 0;
            foreach (var z in new[] { -24f, -32f, -42f })
                for (int x = -6; x <= 6; x += 6)
                    ShootingTarget.Create(k.D, new Vector3(x + (ti++ % 2 == 0 ? 1 : -1) * 0.8f, 0, z), 180f);

            // the gun table: all 22 guns, grouped by class
            var tableM = MapKit.M("5a4632", 0.3f);
            float tx = -10.5f;
            foreach (GunClass cls in System.Enum.GetValues(typeof(GunClass)))
            {
                int n = 0;
                foreach (var g in GunCatalog.All) if (g.cls == cls) n++;
                float len = n * 0.85f;
                var center = new Vector3(tx + len / 2, 0, -15f);
                k.Block(center, new Vector3(len, 0.95f, 1.1f), tableM);
                k.Sign(center + new Vector3(0, 1.75f, 0.3f), 0, GunCatalog.ClassName(cls), 0.25f, UIKit.Accent);
                int j = 0;
                foreach (var g in GunCatalog.All)
                {
                    if (g.cls != cls) continue;
                    var slotPos = new Vector3(tx + 0.42f + j * 0.85f, 1.12f, -15f);
                    var slot = Prims.Empty("slot " + g.name, k.D, slotPos).transform;
                    slot.rotation = Quaternion.Euler(0, 180, 90);
                    var gun = Gun.Spawn(g, slotPos, slot.rotation);
                    gun.home = slot;
                    gun.returnDelay = 6f;
                    gun.displayOnly = true;
                    gun.GoHome();
                    var label = UIKit.Text(k.D, g.name, slotPos + new Vector3(0, -0.06f, 0.56f), 0.055f, Color.white);
                    label.transform.rotation = Quaternion.Euler(60, 180, 0);
                    j++;
                }
                tx += len + 0.6f;
            }

            // benches, lamps, trees, flowers
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 9f, 0, Mathf.Sin(a) * 9f);
                float yaw = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                if (p.z < -5f) continue;
                k.Block(p, new Vector3(2.2f, 0.45f, 0.6f), wood, yaw + 90);
                k.Lamp(p * 1.35f, "ffe3b0");
            }
            for (int i = 0; i < 28; i++)
            {
                float a = k.R(0, Mathf.PI * 2);
                float d = k.R(30, 42);
                var p = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                if (p.z < -12 && Mathf.Abs(p.x) < 16) continue;
                k.Tree(p, k.R(6, 10), k.Pick("4c9a3a", "3f8a35", "5aa842", "d98a3a"));
            }
            for (int i = 0; i < 20; i++)
            {
                float a = k.R(0, Mathf.PI * 2);
                var p = new Vector3(Mathf.Cos(a) * k.R(27, 29), 0, Mathf.Sin(a) * k.R(27, 29));
                k.Bush(p, k.R(0.6f, 1.1f), k.Pick("3b7a2e", "e06aa8", "f2c84b", "8a5ad6"));
            }
            k.Backdrop(30, 60, 140, p => { k.Tree(p, k.R(10, 16), "3f8a35"); return null; });
            k.Backdrop(12, 140, 220, p => { Prims.Ball(k.S, p, new Vector3(k.R(60, 120), k.R(30, 60), k.R(60, 120)), MapKit.M("6c8f5a", 0.05f)); return null; });

            for (int i = 0; i < 10; i++) k.world.ffaSpawns.Add(new Vector3(k.R(-4, 4), 0, k.R(-10, -6)));
            k.world.killY = -20;
        }
    }
}
