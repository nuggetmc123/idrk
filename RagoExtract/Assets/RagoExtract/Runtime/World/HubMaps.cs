using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>Your personal bunker: main hall (raid terminal, settings), Armory (weapons, meds, loot),
    /// Armor Room (armour stands + backpacks), shooting range and the lift to the Market.</summary>
    public class BunkerMap : MapBase
    {
        public readonly List<StashSection> Stash = new List<StashSection>();

        /// <summary>Everything in all stash rooms (saved to the profile when you leave).</summary>
        public List<ItemData> SaveStash()
        {
            var l = new List<ItemData>();
            foreach (var s in Stash) if (s != null) l.AddRange(s.Save());
            if (_medCabinet != null) l.AddRange(_medCabinet.Save());
            return l;
        }

        ItemContainer _medCabinet;

        void LoadStash()
        {
            if (Profile.Current == null) return;
            foreach (var d in new List<ItemData>(Profile.Current.stash))
            {
                // fill the main rooms first, then the expansion rooms; overflow goes on the first section's next page
                var k = StashSection.For(d);
                bool placed = false;
                foreach (var s in Stash) if (s.kind == k && s.Place(d, false)) { placed = true; break; }
                if (!placed) foreach (var s in Stash) if (s.kind == k) { s.Place(d, true); break; }
            }
            // died and lost all your armour? a beat-up bump helmet and soft vest turn up on the stands, so you never raid naked
            var prof = Profile.Current;
            bool anyArmor = !string.IsNullOrEmpty(prof.loadout.helmet) || !string.IsNullOrEmpty(prof.loadout.vest) || prof.stash.Exists(d => Db.Get<ArmorDef>(d.id) != null);
            if (!anyArmor && prof.freeArmor)
                foreach (var s in Stash) if (s.kind == StashKind.Armor) { s.Place(new ItemData("helm_bump")); s.Place(new ItemData("vest_soft")); }
            if (Application.isPlaying) prof.freeArmor = false;
            foreach (var s in Stash) s.Refresh();
        }

        // ------------------------------------------------------------------------------- expansions
        void Walls(Material conc, Color tint, float Ht, params (Vector3 a, Vector3 b)[] segs) { foreach (var w in segs) B.Wall(w.a, w.b, Ht, 0.3f, conc, tint); }

        void FloorCeiling(float x0, float x1, float z0, float z1, Material floor, Material conc, float Ht)
        {
            var c = new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2); var sz = new Vector3(x1 - x0, 0.2f, z1 - z0);
            B.Box(c + Vector3.down * 0.1f, sz, floor, Color.white);
            B.Box(c + Vector3.up * (Ht + 0.1f), sz, conc, new Color(0.5f, 0.5f, 0.5f));
        }

        void Lamp(Vector3 p, Material lampMat, Color light, float Ht)
        {
            B.Box(new Vector3(p.x, Ht - 0.08f, p.z), new Vector3(1.2f, 0.08f, 0.25f), lampMat, Color.white, null, false);
            B.AddLight(new Vector3(p.x, Ht - 0.4f, p.z), light, 9f, 1.4f, true);
        }

        void BuildExpansions(Material floor, Material conc, Material paint, Material lampMat, Color tint, float Ht)
        {
            var b = B;
            var prof = Profile.Current;
            // 1. STORAGE ROOM (west of the Armory): two more racks with shelves
            if (BunkerJobs.Built(prof, "storage"))
            {
                FloorCeiling(-13, -6, 6, 13, floor, conc, Ht);
                Walls(conc, tint, Ht, (new Vector3(-13, 0, 6), new Vector3(-8, 0, 6)), (new Vector3(-6, 0, 13), new Vector3(-13, 0, 13)), (new Vector3(-13, 0, 13), new Vector3(-13, 0, 6)));
                b.Box(new Vector3(-9.5f, 0.6f, 12.84f), new Vector3(7, 1.2f, 0.02f), paint, Color.white, null, false);
                Lamp(new Vector3(-9.5f, 0, 9.5f), lampMat, new Color(1f, 0.88f, 0.7f), Ht);
                Stash.Add(StashSection.Create(transform, new Vector3(-12.6f, 0, 9.4f), -90, StashKind.Weapons));
                Stash.Add(StashSection.Create(transform, new Vector3(-9.4f, 0, 12.75f), 0, StashKind.Weapons));
                b.Crate(new Vector3(-7f, 0, 6.8f), 12, new Vector3(0.9f, 0.6f, 0.6f));
                b.Sign(new Vector3(-5.8f, 2.7f, 7.5f), Quaternion.Euler(0, -90, 0), "STORAGE", new Color(0.9f, 0.8f, 0.6f), 0.12f);
            }
            else SealedDoor.Create(b, transform, new Vector3(-6, 0, 7.5f), -90, "STORAGE ROOM", 1);

            // 2. MED BAY (south of the Armor Room): cabinet that restocks meds after every raid
            if (BunkerJobs.Built(prof, "medbay"))
            {
                FloorCeiling(8, 15, -13, -7, floor, conc, Ht);
                Walls(conc, tint, Ht, (new Vector3(8, 0, -13), new Vector3(15, 0, -13)), (new Vector3(8, 0, -7), new Vector3(8, 0, -13)), (new Vector3(15, 0, -13), new Vector3(15, 0, -7)));
                var white = Mats.Lit("medbay_white", new Color(0.82f, 0.84f, 0.84f), 0f, 0.4f, Mats.Plaster, null, 0.3f, 0.1f, true, 0.4f);
                b.Box(new Vector3(11.5f, 1.0f, -12.84f), new Vector3(7, 2.0f, 0.02f), white, Color.white, null, false);
                Lamp(new Vector3(11.5f, 0, -10f), lampMat, new Color(0.85f, 0.95f, 1f), Ht);
                // stretcher
                b.Box(new Vector3(9.3f, 0.75f, -10.5f), new Vector3(0.75f, 0.08f, 2.0f), Maps.Metal, new Color(0.5f, 0.52f, 0.55f), null, true, Synth.Surface.Metal);
                b.Box(new Vector3(9.3f, 0.82f, -10.5f), new Vector3(0.7f, 0.06f, 1.9f), Mats.Lit("mattress", new Color(0.35f, 0.38f, 0.3f), 0, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0), new Color(0.6f, 0.75f, 0.8f), null, false);
                foreach (var dx in new[] { -0.3f, 0.3f }) foreach (var dz in new[] { -0.9f, 0.9f }) b.Cyl(new Vector3(9.3f + dx, 0, -10.5f + dz), new Vector3(9.3f + dx, 0.75f, -10.5f + dz), 0.02f, 0.02f, 6, Maps.Metal, new Color(0.5f, 0.5f, 0.5f), false);
                // med cabinet on the back wall
                var cab = new GameObject("MedCabinet").transform; cab.SetParent(transform, false);
                cab.localPosition = new Vector3(12.2f, 0, -12.8f); cab.localRotation = Quaternion.Euler(0, 180, 0);
                var mb = new MB(2);
                mb.Sub(0).Box(new Vector3(0, 1.35f, 0.06f), new Vector3(1.5f, 1.0f, 0.12f), new Color(0.9f, 0.9f, 0.88f), 0.01f);
                mb.Sub(1).Box(new Vector3(0, 1.98f, 0.0f), new Vector3(0.2f, 0.06f, 0.01f), new Color(0.85f, 0.1f, 0.1f), 0f);
                mb.Sub(1).Box(new Vector3(0, 1.98f, 0.0f), new Vector3(0.06f, 0.2f, 0.01f), new Color(0.85f, 0.1f, 0.1f), 0f);
                mb.Build("Cabinet", cab, Mats.Lit("cabinet", Color.white, 0.4f, 0.5f, Mats.Metal, null, 0.2f, 0.2f), Mats.Unlit("redcross", Color.white));
                _medCabinet = ItemContainer.Grid(cab, new Vector3(0, 1.35f, -0.02f), Quaternion.Euler(-90, 0, 0), 4, 3, new Vector2(0.34f, 0.3f), 0.26f, it => !(it is Gun) && !(it is Backpack) && !(it is ArmorItem));
                UI.Text(cab, "MED CABINET - restocks after every raid", new Vector3(0, 0.75f, -0.02f), 0.035f, UI.Dim);
                b.Sign(new Vector3(11.5f, 2.7f, -6.8f), Quaternion.Euler(0, 180, 0), "MED BAY", new Color(0.95f, 0.5f, 0.5f), 0.12f);
            }
            else SealedDoor.Create(b, transform, new Vector3(11.5f, 0, -7), 180, "MED BAY", 2);

            // 3. GENERATOR ROOM (east of the Armory): makes money every raid
            if (BunkerJobs.Built(prof, "generator"))
            {
                FloorCeiling(6, 13, 6, 13, floor, conc, Ht);
                Walls(conc, tint, Ht, (new Vector3(8, 0, 6), new Vector3(13, 0, 6)), (new Vector3(13, 0, 13), new Vector3(6, 0, 13)), (new Vector3(13, 0, 6), new Vector3(13, 0, 13)));
                Lamp(new Vector3(9.5f, 0, 9.5f), lampMat, new Color(1f, 0.85f, 0.6f), Ht);
                var body = Mats.Lit("generator", new Color(0.55f, 0.48f, 0.12f), 0.5f, 0.45f, Mats.Metal, Mats.MetalN, 0.5f, 0.5f);
                b.Box(new Vector3(10.2f, 0.15f, 9.0f), new Vector3(2.6f, 0.3f, 1.4f), Maps.Metal, new Color(0.25f, 0.25f, 0.25f), null, true, Synth.Surface.Metal); // skid
                b.Box(new Vector3(9.6f, 0.95f, 9.0f), new Vector3(1.4f, 1.3f, 1.2f), body, Color.white, null, true, Synth.Surface.Metal, 0.03f);
                b.Cyl(new Vector3(10.4f, 0.75f, 9.0f), new Vector3(11.3f, 0.75f, 9.0f), 0.45f, 0.45f, 18, body, new Color(0.8f, 0.8f, 0.8f), true, Synth.Surface.Metal);
                b.Cyl(new Vector3(9.3f, 1.6f, 8.7f), new Vector3(9.3f, Ht, 8.7f), 0.08f, 0.08f, 10, Maps.Metal, new Color(0.3f, 0.3f, 0.3f), false); // exhaust
                b.Box(new Vector3(9.6f, 1.25f, 8.39f), new Vector3(0.5f, 0.3f, 0.02f), Mats.Lit("gen_panel", new Color(0.1f, 0.1f, 0.1f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.2f, 1.2f, 0.3f)), Color.white, null, false);
                for (int i = 0; i < 4; i++) b.Cyl(new Vector3(8.0f, 0.2f + i * 0.12f, 9.0f), new Vector3(6.3f, 0.2f + i * 0.12f, 9.0f + i * 0.1f), 0.025f, 0.025f, 6, Maps.Metal, new Color(0.12f, 0.12f, 0.12f), false); // cables
                var l = b.AddLight(new Vector3(9.6f, 1.6f, 8.0f), new Color(0.4f, 1f, 0.5f), 3f, 0.6f);
                l.gameObject.AddComponent<Flicker>();
                GeneratorPanel.Create(transform, new Vector3(9.5f, 1.5f, 12.8f), 0);
                b.Sign(new Vector3(5.8f, 2.7f, 11f), Quaternion.Euler(0, 90, 0), "GENERATOR", new Color(0.9f, 0.8f, 0.6f), 0.12f);
            }
            else SealedDoor.Create(b, transform, new Vector3(6, 0, 11), 90, "GENERATOR ROOM", 3);

            // 4. TROPHY VAULT (north of the Armory): more armour stands and gun racks
            if (BunkerJobs.Built(prof, "vault"))
            {
                FloorCeiling(-1, 6, 13, 20, floor, conc, Ht);
                Walls(conc, tint, Ht, (new Vector3(6, 0, 20), new Vector3(-1, 0, 20)), (new Vector3(-1, 0, 20), new Vector3(-1, 0, 13)), (new Vector3(6, 0, 13), new Vector3(6, 0, 20)));
                Lamp(new Vector3(2.5f, 0, 16.5f), lampMat, new Color(1f, 0.85f, 0.55f), Ht);
                Stash.Add(StashSection.Create(transform, new Vector3(-0.6f, 0, 16.6f), -90, StashKind.Weapons));
                Stash.Add(StashSection.Create(transform, new Vector3(3.0f, 0, 19.6f), 0, StashKind.Armor));
                // trophy plinth with a gold light
                b.Box(new Vector3(3.6f, 0.5f, 15.6f), new Vector3(0.7f, 1.0f, 0.7f), Mats.Lit("plinth", new Color(0.15f, 0.15f, 0.16f), 0.2f, 0.6f), Color.white, null, true, Synth.Surface.Metal, 0.02f);
                b.Box(new Vector3(3.6f, 1.12f, 15.6f), new Vector3(0.22f, 0.22f, 0.22f), Mats.Gold, Color.white, Quaternion.Euler(0, 30, 0), false);
                b.AddLight(new Vector3(3.6f, 2.6f, 15.6f), new Color(1f, 0.8f, 0.4f), 4f, 1.6f);
                b.Sign(new Vector3(4.5f, 2.7f, 12.8f), Quaternion.identity, "TROPHY VAULT", new Color(1f, 0.8f, 0.35f), 0.12f);
            }
            else SealedDoor.Create(b, transform, new Vector3(4.5f, 0, 13), 0, "TROPHY VAULT", 4);
        }

        void RestockMedBay()
        {
            var prof = Profile.Current;
            if (_medCabinet == null || prof == null || !Application.isPlaying) return;
            if (prof.raids <= prof.medRestockRaid) return;
            prof.medRestockRaid = prof.raids;
            var rng = new Rng(prof.raids * 977 + 5);
            var ids = new List<string> { "ifak", "painkillers", "bandage" };
            if (rng.Chance(0.35f)) ids.Add("trauma_kit");
            if (rng.Chance(0.25f)) ids.Add("stim");
            int n = 0;
            foreach (var id in ids)
            {
                var it = ItemFactory.Spawn(id, _medCabinet.transform.position, _medCabinet.transform.rotation);
                if (it == null) continue;
                if (_medCabinet.Add(it)) n++; else Destroy(it.gameObject);
            }
            if (n > 0 && RagoGame.I != null && RagoGame.I.Player != null) RagoGame.I.Player.Toast("Med Bay restocked: +" + n + " meds");
        }

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
            // north wall opens into the Armory, east wall into the Armor Room
            b.Wall(new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f), new List<Vector4> { new Vector4(W / 2, 2.4f, 0, 2.6f) });
            b.Wall(new Vector3(W / 2, 0, -D / 2), new Vector3(-W / 2, 0, -D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f), new List<Vector4> { new Vector4(W / 2 - 2.5f, 1.8f, 0, 2.4f) }); // lift
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f), new List<Vector4> { new Vector4(D / 2 + 2f, 2.4f, 0, 2.6f) });
            // west wall opens into the range tunnel
            b.Wall(new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), Ht, 0.3f, conc, new Color(0.75f, 0.73f, 0.68f), new List<Vector4> { new Vector4(D / 2, 3f, 0, 2.6f) });
            // two-tone paint band
            foreach (var w in new[] { (new Vector3(-4.6f, 0.6f, D / 2 - 0.16f), new Vector3(6.8f, 1.2f, 0.02f)), (new Vector3(4.6f, 0.6f, D / 2 - 0.16f), new Vector3(6.8f, 1.2f, 0.02f)), (new Vector3(-3.2f, 0.6f, -D / 2 + 0.16f), new Vector3(9.6f, 1.2f, 0.02f)), (new Vector3(5.85f, 0.6f, -D / 2 + 0.16f), new Vector3(4.3f, 1.2f, 0.02f)),
                                      (new Vector3(W / 2 - 0.16f, 0.6f, 2.6f), new Vector3(0.02f, 1.2f, 6.8f)), (new Vector3(W / 2 - 0.16f, 0.6f, -4.6f), new Vector3(0.02f, 1.2f, 2.8f)) })
                b.Box(w.Item1, w.Item2, paint, Color.white, null, false);
            // pipes & cable trays
            for (int i = 0; i < 3; i++) b.Cyl(new Vector3(-W / 2, Ht - 0.25f - i * 0.12f, D / 2 - 0.4f), new Vector3(W / 2, Ht - 0.25f - i * 0.12f, D / 2 - 0.4f), 0.05f, 0.05f, 10, Maps.Metal, new Color(0.35f + i * 0.1f, 0.3f, 0.25f), false);
            b.Box(new Vector3(0, Ht - 0.3f, -D / 2 + 0.6f), new Vector3(W, 0.06f, 0.4f), Maps.Metal, new Color(0.4f, 0.4f, 0.4f), null, false);
            for (int i = -1; i <= 1; i++)
            {
                b.Box(new Vector3(i * 5, Ht - 0.08f, 0), new Vector3(1.2f, 0.08f, 0.25f), Mats.Lit("lamp_on", new Color(1f, 0.95f, 0.8f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.2f, 2f, 1.6f)), Color.white, null, false);
                b.AddLight(new Vector3(i * 5, Ht - 0.4f, 0), new Color(1f, 0.88f, 0.7f), 9f, 1.4f, i == 0);
            }
            var lampMat = Mats.Lit("lamp_on", new Color(1f, 0.95f, 0.8f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.2f, 2f, 1.6f));
            var wallTint = new Color(0.75f, 0.73f, 0.68f);
            // ---- Armory (north): weapons stash, workbench, ammo crates
            float ax = 6f, az0 = D / 2, az1 = D / 2 + 7f;
            b.Box(new Vector3(0, -0.1f, (az0 + az1) / 2), new Vector3(ax * 2, 0.2f, az1 - az0), floor, Color.white);
            b.Box(new Vector3(0, Ht + 0.1f, (az0 + az1) / 2), new Vector3(ax * 2, 0.2f, az1 - az0), conc, new Color(0.5f, 0.5f, 0.5f));
            // doorways to the expansion rooms (sealed until the Foreman builds them)
            b.Wall(new Vector3(-ax, 0, az0), new Vector3(-ax, 0, az1), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(1.5f, 2.0f, 0, 2.4f) });   // Storage Room
            b.Wall(new Vector3(-ax, 0, az1), new Vector3(ax, 0, az1), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(ax + 4.5f, 2.0f, 0, 2.4f) }); // Trophy Vault
            b.Wall(new Vector3(ax, 0, az1), new Vector3(ax, 0, az0), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(2f, 2.0f, 0, 2.4f) });      // Generator Room
            b.Box(new Vector3(0, 0.6f, az1 - 0.16f), new Vector3(ax * 2, 1.2f, 0.02f), paint, Color.white, null, false);
            b.Box(new Vector3(0, Ht - 0.08f, (az0 + az1) / 2), new Vector3(1.2f, 0.08f, 0.25f), lampMat, Color.white, null, false);
            b.AddLight(new Vector3(0, Ht - 0.4f, (az0 + az1) / 2), new Color(1f, 0.88f, 0.7f), 9f, 1.4f, true);
            Stash.Add(StashSection.Create(transform, new Vector3(0, 0, az1 - 0.25f), 0, StashKind.Weapons));
            b.Table(new Vector3(-ax + 0.6f, 0, (az0 + az1) / 2 + 0.5f), 90, new Vector2(2.4f, 0.9f), 0.9f);
            b.Box(new Vector3(-ax + 0.17f, 1.5f, (az0 + az1) / 2 + 0.5f), new Vector3(0.05f, 1.0f, 2.4f), Mats.Lit("pegboard", Color.white, 0.1f, 0.3f, Mats.Wood, null, 0.3f, 0.1f), new Color(0.5f, 0.45f, 0.35f), null, false);
            b.Crate(new Vector3(ax - 0.7f, 0, az0 + 1.2f), 5, new Vector3(0.9f, 0.5f, 0.6f));
            b.Crate(new Vector3(ax - 0.7f, 0, az0 + 2.1f), -8, new Vector3(0.9f, 0.5f, 0.6f));
            b.Crate(new Vector3(ax - 0.7f, 0.5f, az0 + 1.6f), 12, new Vector3(0.8f, 0.4f, 0.5f));
            b.Sign(new Vector3(0, 2.95f, D / 2 - 0.2f), Quaternion.identity, "ARMORY", new Color(0.9f, 0.8f, 0.6f), 0.14f);

            // ---- Armor Room (east): armour stands + backpack shelves
            float rx0 = W / 2, rx1 = W / 2 + 7f, rz0 = -D / 2 - 1f, rz1 = D / 2 - 3f, rzc = (rz0 + rz1) / 2;
            b.Box(new Vector3((rx0 + rx1) / 2, -0.1f, rzc), new Vector3(rx1 - rx0, 0.2f, rz1 - rz0), floor, Color.white);
            b.Box(new Vector3((rx0 + rx1) / 2, Ht + 0.1f, rzc), new Vector3(rx1 - rx0, 0.2f, rz1 - rz0), conc, new Color(0.5f, 0.5f, 0.5f));
            b.Wall(new Vector3(rx0, 0, rz1), new Vector3(rx1, 0, rz1), Ht, 0.3f, conc, wallTint);
            b.Wall(new Vector3(rx1, 0, rz1), new Vector3(rx1, 0, rz0), Ht, 0.3f, conc, wallTint);
            b.Wall(new Vector3(rx1, 0, rz0), new Vector3(rx0, 0, rz0), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(3.5f, 2.0f, 0, 2.4f) });  // Med Bay
            b.Wall(new Vector3(rx0, 0, rz0), new Vector3(rx0, 0, -D / 2), Ht, 0.3f, conc, wallTint);
            b.Box(new Vector3(rx1 - 0.16f, 0.6f, rzc), new Vector3(0.02f, 1.2f, rz1 - rz0), paint, Color.white, null, false);
            b.Box(new Vector3((rx0 + rx1) / 2, Ht - 0.08f, rzc), new Vector3(0.25f, 0.08f, 1.2f), lampMat, Color.white, null, false);
            b.AddLight(new Vector3((rx0 + rx1) / 2, Ht - 0.4f, rzc), new Color(1f, 0.9f, 0.78f), 9f, 1.4f, true);
            Stash.Add(StashSection.Create(transform, new Vector3(rx1 - 0.4f, 0, rzc), 90, StashKind.Armor));
            b.Box(new Vector3((rx0 + rx1) / 2, 0.45f, rz1 - 0.45f), new Vector3(2.2f, 0.06f, 0.4f), Maps.WoodPlank, new Color(0.55f, 0.45f, 0.33f), null, true, Synth.Surface.Wood); // bench
            b.Box(new Vector3((rx0 + rx1) / 2, 0.21f, rz1 - 0.45f), new Vector3(2.0f, 0.42f, 0.3f), Maps.Metal, new Color(0.3f, 0.3f, 0.3f), null, false);
            b.Sign(new Vector3(W / 2 - 0.2f, 2.95f, -2f), Quaternion.Euler(0, 90, 0), "ARMOR", new Color(0.9f, 0.8f, 0.6f), 0.14f);

            // ---- main hall: ID terminal (callsign) on the north wall where the gear wall was, kiosk on the east side
            IdPlaque.Create(transform, new Vector3(-4.6f, 0, D / 2 - 0.2f), 0);
            if (Application.isPlaying && Profile.Current != null && !Profile.Current.tutorialDone)
                Tutorial.Create(transform, new Vector3(-W / 2 - 3f, 0, 0), new Vector3(W / 2 + 3.5f, 0, -2f),
                    new Vector3(0, 0, D / 2 + 3f), new Vector3(W / 2 - 1.9f, 0, 4.2f), new Vector3(2.5f, 0, -D / 2 + 1.2f));
            BuildExpansions(floor, conc, paint, lampMat, wallTint, Ht);
            LoadStash();
            RestockMedBay();
            Kiosk.Create(transform, new Vector3(W / 2 - 0.9f, 0, 4.2f), Quaternion.Euler(0, 90, 0));
            // workbench + props
            b.Crate(new Vector3(3.6f, 0, D / 2 - 0.7f), 10, new Vector3(1.0f, 0.6f, 0.6f));
            b.Crate(new Vector3(3.4f, 0.6f, D / 2 - 0.6f), -5, new Vector3(0.8f, 0.5f, 0.5f));
            b.Barrel(new Vector3(-6.8f, 0, -5f), new Color(0.2f, 0.3f, 0.2f));
            // bed + locker
            b.Box(new Vector3(-5.8f, 0.3f, -4.6f), new Vector3(2.0f, 0.15f, 0.9f), Maps.Metal, new Color(0.3f, 0.32f, 0.3f), null, true, Synth.Surface.Metal);
            b.Box(new Vector3(-5.8f, 0.45f, -4.6f), new Vector3(1.9f, 0.15f, 0.85f), Mats.Lit("mattress", new Color(0.35f, 0.38f, 0.3f), 0, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0), Color.white, null, false, Synth.Surface.Dirt, 0.05f);
            SettingsBoard.Create(transform, new Vector3(-3.5f, 1.5f, -D / 2 + 0.2f), 180);
            // supply crate with a free stim + bandages each visit so you can always heal up
            var supply = AddCrate(new Vector3(5.8f, 0, -4.9f), 0, LootCrate.Kind.Medical);
            // travel
            // the lift up to the Market: press CALL, walk in, the doors close behind you
            Elevator.Create(b, transform, new Vector3(2.5f, 0, -D / 2), 180, "Bunker", "Market", true, () => RagoGame.I.GoTo(Area.Market));
            b.Sign(new Vector3(2.5f, 3.15f, -D / 2 + 0.3f), Quaternion.Euler(0, 180, 0), "LIFT - MARKET", new Color(0.9f, 0.8f, 0.6f), 0.12f);
            b.Sign(new Vector3(-4.6f, 3.2f, D / 2 - 0.3f), Quaternion.identity, "RAGO EXTRACT", UI.Accent, 0.22f);
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
            Spawn(new Vector3(0, 0.05f, -2f), 0);
        }
    }

    /// <summary>First launch: a dark registration room where you choose your callsign (once, forever).</summary>
    public class WelcomeMap : MapBase
    {
        public override void Build()
        {
            IsRaid = false; Safe = true;
            Ambience = Synth.Amb.Bunker;
            NavBounds = new Bounds(Vector3.zero, new Vector3(12, 6, 12));
            Lighting(new Color(1f, 0.9f, 0.75f), 0.02f, new Vector3(60, 30, 0), new Color(0.12f, 0.13f, 0.15f), new Color(0.1f, 0.1f, 0.1f), new Color(0.05f, 0.05f, 0.05f),
                new Color(0.03f, 0.03f, 0.04f), 0f, new Color(0.03f, 0.03f, 0.04f), new Color(0.06f, 0.06f, 0.07f), 0f, 0.5f);
            var b = B;
            var conc = Maps.Concrete;
            float W = 10, D = 10, Ht = 4.2f;
            var tint = new Color(0.45f, 0.45f, 0.44f);
            b.Box(new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), Maps.ConcreteDark, Color.white);
            b.Box(new Vector3(0, Ht + 0.1f, 0), new Vector3(W, 0.2f, D), conc, new Color(0.3f, 0.3f, 0.3f));
            b.Wall(new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), Ht, 0.3f, conc, tint);
            b.Wall(new Vector3(W / 2, 0, -D / 2), new Vector3(-W / 2, 0, -D / 2), Ht, 0.3f, conc, tint);
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), Ht, 0.3f, conc, tint);
            b.Wall(new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), Ht, 0.3f, conc, tint);
            // hazard line on the floor in front of the terminal, a hanging lamp over it
            for (int i = 0; i < 10; i++) b.Box(new Vector3(-2.25f + i * 0.5f, 0.012f, 3.4f), new Vector3(0.25f, 0.01f, 0.12f), Maps.Metal, i % 2 == 0 ? new Color(0.95f, 0.75f, 0.1f) : new Color(0.08f, 0.08f, 0.08f), Quaternion.Euler(0, 35, 0), false);
            b.Cyl(new Vector3(0, Ht, 3.2f), new Vector3(0, Ht - 0.9f, 3.2f), 0.01f, 0.01f, 6, Maps.Metal, Color.gray, false);
            b.Cyl(new Vector3(0, Ht - 0.9f, 3.2f), new Vector3(0, Ht - 1.1f, 3.2f), 0.06f, 0.3f, 16, Maps.Metal, new Color(0.25f, 0.27f, 0.25f), false);
            var lamp = b.AddLight(new Vector3(0, Ht - 1.2f, 3.0f), new Color(1f, 0.9f, 0.75f), 7f, 2f, true);
            b.AddLight(new Vector3(0, 2.4f, 4.2f), new Color(0.4f, 1f, 0.5f), 3f, 0.5f);
            b.Sign(new Vector3(0, 3.55f, D / 2 - 0.2f), Quaternion.identity, "RAGO EXTRACT", UI.Accent, 0.4f);
            b.Sign(new Vector3(0, 3.1f, D / 2 - 0.2f), Quaternion.identity, "OPERATOR REGISTRATION", new Color(0.8f, 0.78f, 0.7f), 0.12f);
            IdTerminal.Create(transform, new Vector3(0, 0, D / 2 - 0.2f), 0);
            b.Sign(new Vector3(0, 2.8f, D / 2 - 0.2f), Quaternion.identity, "Your callsign is how other operators see you. It can never be changed.", new Color(0.75f, 0.73f, 0.68f), 0.05f);
            Spawn(new Vector3(0, 0.05f, 2.4f), 0);
        }
    }

    /// <summary>The Market: shared hub where players meet, trade with NPC traders and queue for raids.</summary>
    public class MarketMap : MapBase
    {
        public readonly List<TraderStall> Stalls = new List<TraderStall>();
        public QuestStall Foreman;

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
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), H, 0.3f, corr, wc, new List<Vector4> { new Vector4(D / 2 + 10f, 1.8f, 0, 2.4f) }, Synth.Surface.Metal); // lift
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
            // Bruno the Foreman: bunker expansion jobs
            Foreman = QuestStall.Create(transform, new Vector3(15, 0, 4), 0, 211);
            b.AddLight(new Vector3(15, 3.6f, 3.4f), new Color(1f, 0.8f, 0.55f), 7f, 1.2f);
            // (raids are queued from the terminal in your bunker - the Market is just for trading and meeting up)
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
            Elevator.Create(b, transform, new Vector3(W / 2, 0, -10), 90, "Market", "Bunker", false, () => RagoGame.I.GoTo(Area.Bunker));
            b.Sign(new Vector3(W / 2 - 0.3f, 3.15f, -10), Quaternion.Euler(0, 90, 0), "LIFT - BUNKER", new Color(0.9f, 0.8f, 0.6f), 0.18f);
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
