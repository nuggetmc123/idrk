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
            for (int i = 0; i < Stash.Count; i++) if (Stash[i] != null) l.AddRange(Stash[i].Save(i));   // each item remembers its wall and spot
            // the med cabinet keeps its own contents (it used to go into the stash too, so the meds came back twice and spilled into the Lost & Found)
            if (_medCabinet != null && Profile.Current != null) Profile.Current.medCabinet = _medCabinet.Save();
            foreach (var pb in GetComponentsInChildren<PaintBooth>()) pb.SaveTo(l);   // a gun in the paint booth's clamp
            return l;
        }

        /// <summary>Everything lying loose in the bunker (floor, tables, shelf tops), with where it is. Nothing is moved.</summary>
        public List<FloorItem> SaveFloor(Transform root)
        {
            var l = new List<FloorItem>();
            foreach (var it in Item.All)
            {
                if (it == null || it.Gone || it.Held || it.slot != null || it is Magazine) continue;
                if (it is SightItem si && si.onGun != null) continue;
                if (it is AttachItem ai && ai.onGun != null) continue;
                        if (it is CamItem ci && ci.onGun != null) continue;
                if (it.transform.parent != null && it.transform.parent != root) continue;   // in a bag, on a gun, carried
                ItemData d = null;
                try { d = it.Save(); } catch (System.Exception e) { Debug.LogWarning("[Rago] floor save: " + e.Message); }
                if (d == null) continue;
                Vector3 p = it.transform.position;
                if (p.y < -2f || p.y > 6f) p = OverflowSpot(l.Count);   // fell out of the world: it waits by the west wall instead
                l.Add(new FloorItem { item = d, pos = p, rot = it.transform.rotation });
            }
            return l;
        }

        /// <summary>A free patch of floor by the west wall of the main hall, for things that have nowhere else to go.</summary>
        Vector3 OverflowSpot(int i) => transform.TransformPoint(new Vector3(-16f / 2 + 0.6f + (i % 3) * 0.5f, 0.25f, 2.4f + (i / 3 % 6) * 0.5f));

        void LoadFloor(List<ItemData> overflow)
        {
            var prof = Profile.Current;
            if (prof == null || !Application.isPlaying) return;
            if (prof.bunkerFloor == null) prof.bunkerFloor = new List<FloorItem>();
            foreach (var f in prof.bunkerFloor)
            {
                if (f == null || f.item == null) continue;
                try { ItemFactory.Spawn(f.item, f.pos + Vector3.up * 0.02f, f.rot); } catch (System.Exception e) { Debug.LogWarning("[Rago] floor item: " + e.Message); }
            }
            int k = 0;
            foreach (var d in overflow)
            {
                try { ItemFactory.Spawn(d, OverflowSpot(k++) + Vector3.up * 0.1f * (k / 18), Quaternion.Euler(0, 90, 0)); } catch (System.Exception e) { Debug.LogWarning("[Rago] overflow item: " + e.Message); }
            }
        }

        ItemContainer _medCabinet;

        void LoadStash()
        {
            if (Profile.Current == null) return;
            var overflow = new List<ItemData>();
            var rest = new List<ItemData>();
            // first, everything back exactly where it was hung (it used to go to the first wall of its kind: guns hung on the
            // Gear wall came back on the Armory, and rifles that didn't fit there ended up on the main hall floor)
            foreach (var d in new List<ItemData>(Profile.Current.stash))
            {
                if (d == null) continue;
                int si = d.stashSec - 1;
                bool ok = false;
                try { ok = si >= 0 && si < Stash.Count && Stash[si] != null && Stash[si].PlaceAt(d, d.stashSlot); } catch (System.Exception e) { Debug.LogWarning("[Rago] stash spot: " + e.Message); }
                if (!ok) rest.Add(d);
            }
            foreach (var d in rest)
            {
                // it had no spot (new, or a page that was full): its own wall first, then any wall of that kind, then the floor
                int si = d.stashSec - 1;
                if (si >= 0 && si < Stash.Count && Stash[si] != null && Stash[si].Place(d, false)) continue;
                // fill the main rooms first, then the expansion rooms; overflow goes on the first section's next page
                var k = StashSection.For(d);
                bool placed = false;
                foreach (var s in Stash) if (s.kind == k && s.Place(d, false)) { placed = true; break; }
                if (!placed && k == StashKind.Weapons) foreach (var s in Stash) if (s.kind == StashKind.Gear && s.Place(d, false)) { placed = true; break; }
                // no room on any rack or shelf: it goes on the floor by the west wall (and stays there until you move it)
                if (!placed) overflow.Add(d);
            }
            // (no free armour after a death any more: what you lose is lost)
            if (Application.isPlaying) Profile.Current.freeArmor = false;
            foreach (var s in Stash) s.Refresh();
            LoadFloor(overflow);
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
                b.Crate(new Vector3(-9.5f, 0, 6.6f), 12, new Vector3(0.9f, 0.6f, 0.6f));
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
                // diesel generator set: steel skid frame, yellow sound-proof canopy with louvres and access doors, a
                // radiator grille at one end, the control panel with gauges, a fuel tank underneath and the muffler on top
                var body = Mats.Lit("generator2", new Color(0.86f, 0.66f, 0.12f), 0.35f, 0.45f, Mats.Metal, Mats.MetalN, 0.45f, 0.45f);
                var steel = Maps.Metal;
                Color dk = new Color(0.14f, 0.14f, 0.15f), mid = new Color(0.3f, 0.3f, 0.31f);
                Vector3 gc = new Vector3(9.7f, 0, 9.0f);
                b.Box(gc + new Vector3(0, 0.09f, 0), new Vector3(3.0f, 0.18f, 1.3f), steel, dk, null, true, Synth.Surface.Metal);                       // skid
                foreach (var z in new[] { -0.6f, 0.6f }) b.Box(gc + new Vector3(0, 0.22f, z), new Vector3(3.0f, 0.1f, 0.08f), steel, mid, null, false);   // skid rails
                b.Box(gc + new Vector3(0, 0.36f, 0), new Vector3(2.6f, 0.2f, 1.1f), steel, mid, null, true, Synth.Surface.Metal);                        // fuel tank
                b.Box(gc + new Vector3(0, 1.1f, 0), new Vector3(2.7f, 1.3f, 1.2f), body, Color.white, null, true, Synth.Surface.Metal, 0.04f);         // canopy
                b.Box(gc + new Vector3(0, 1.79f, 0), new Vector3(2.75f, 0.06f, 1.25f), body, new Color(0.85f, 0.85f, 0.85f), null, false, Synth.Surface.Metal, 0.02f);   // roof lip
                // louvres down both sides and access door seams + handles
                foreach (var sz in new[] { -0.61f, 0.61f })
                {
                    for (int i = 0; i < 7; i++) b.Box(gc + new Vector3(-0.6f + i * 0.09f, 1.2f, sz), new Vector3(0.05f, 0.55f, 0.015f), steel, dk, null, false);
                    foreach (var x in new[] { 0.15f, 0.85f }) b.Box(gc + new Vector3(x, 1.1f, sz), new Vector3(0.012f, 1.1f, 0.012f), steel, dk, null, false);
                    b.Box(gc + new Vector3(0.75f, 1.1f, sz * 1.03f), new Vector3(0.12f, 0.03f, 0.03f), steel, new Color(0.6f, 0.6f, 0.6f), null, false);
                }
                // radiator grille (+x end)
                b.Box(gc + new Vector3(1.36f, 1.05f, 0), new Vector3(0.02f, 1.0f, 1.0f), steel, dk, null, false);
                for (int i = 0; i < 9; i++) b.Box(gc + new Vector3(1.375f, 0.62f + i * 0.105f, 0), new Vector3(0.02f, 0.03f, 0.98f), steel, mid, null, false);
                // control panel (-x end): dark panel, gauges, green run light, red stop button
                b.Box(gc + new Vector3(-1.36f, 1.15f, 0), new Vector3(0.02f, 0.7f, 0.8f), Mats.Lit("gen_ctrl", dk, 0.2f, 0.5f), Color.white, null, false);
                foreach (var z in new[] { -0.22f, 0f, 0.22f })
                {
                    b.Cyl(gc + new Vector3(-1.37f, 1.3f, z), gc + new Vector3(-1.39f, 1.3f, z), 0.075f, 0.075f, 16, steel, new Color(0.75f, 0.75f, 0.75f), false);
                    b.Cyl(gc + new Vector3(-1.39f, 1.3f, z), gc + new Vector3(-1.395f, 1.3f, z), 0.06f, 0.06f, 16, Mats.Lit("gauge_face", new Color(0.92f, 0.9f, 0.84f), 0f, 0.3f), Color.white, false);
                }
                b.Box(gc + new Vector3(-1.38f, 1.0f, -0.2f), new Vector3(0.03f, 0.07f, 0.07f), Mats.Lit("gen_run", new Color(0.1f, 0.4f, 0.1f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.2f, 2f, 0.3f)), Color.white, null, false);
                b.Cyl(gc + new Vector3(-1.37f, 1.0f, 0.2f), gc + new Vector3(-1.41f, 1.0f, 0.2f), 0.04f, 0.035f, 14, Mats.Lit("gen_stop", new Color(0.8f, 0.08f, 0.06f), 0.1f, 0.5f), Color.white, false);
                // exhaust: a stub from the engine through the canopy roof, an elbow into the muffler (sitting on two saddle
                // brackets), and the tailpipe rising off the muffler's top through a collar in the ceiling
                Color soot = new Color(0.22f, 0.2f, 0.18f), rust = new Color(0.36f, 0.3f, 0.25f);
                Vector3 mA = gc + new Vector3(0.55f, 1.98f, 0.25f), mB = gc + new Vector3(-0.45f, 1.98f, 0.25f);
                b.Cyl(mA, mB, 0.15f, 0.15f, 16, steel, rust, false);                                                     // muffler drum
                b.Cyl(mA, mA + new Vector3(0.02f, 0, 0), 0.155f, 0.155f, 16, steel, soot, false);                        // end caps
                b.Cyl(mB, mB - new Vector3(0.02f, 0, 0), 0.155f, 0.155f, 16, steel, soot, false);
                foreach (var x in new[] { 0.35f, -0.25f })
                {
                    b.Box(gc + new Vector3(x, 1.86f, 0.25f), new Vector3(0.05f, 0.08f, 0.3f), steel, dk, null, false);  // saddle bracket
                    b.Cyl(gc + new Vector3(x - 0.015f, 1.98f, 0.25f), gc + new Vector3(x + 0.015f, 1.98f, 0.25f), 0.158f, 0.158f, 16, steel, dk, false);   // strap
                }
                Vector3 ex0 = gc + new Vector3(0.85f, 1.83f, 0.25f), ex1 = gc + new Vector3(0.85f, 1.98f, 0.25f);
                b.Cyl(ex0 - new Vector3(0, 0.01f, 0), ex0 + new Vector3(0, 0.02f, 0), 0.1f, 0.1f, 14, steel, dk, false);  // roof flange
                b.Cyl(ex0, ex1, 0.065f, 0.065f, 12, steel, soot, false);                                                  // engine outlet
                b.Sphere(ex1, Vector3.one * 0.068f, 10, steel, soot, false);                                                                    // elbow
                b.Cyl(ex1, mA + new Vector3(0.02f, 0, 0), 0.065f, 0.065f, 12, steel, soot, false);                        // into the muffler
                Vector3 tp0 = gc + new Vector3(-0.3f, 2.1f, 0.25f), tp1 = gc + new Vector3(-0.3f, Ht, 0.25f);
                b.Cyl(tp0 - new Vector3(0, 0.03f, 0), tp1, 0.07f, 0.07f, 12, steel, soot, false);                         // tailpipe
                b.Cyl(tp1 - new Vector3(0, 0.04f, 0), tp1, 0.14f, 0.14f, 14, steel, dk, false);                           // ceiling collar
                foreach (float y in new[] { 2.6f, 3.1f }) b.Cyl(gc + new Vector3(-0.3f, y, 0.25f), gc + new Vector3(-0.3f, y + 0.03f, 0.25f), 0.078f, 0.078f, 12, steel, dk, false);   // clamps
                b.Cyl(gc + new Vector3(0, 1.82f, -0.2f), gc + new Vector3(0, 1.98f, -0.2f), 0.06f, 0.06f, 10, steel, mid, false);   // lifting eye
                // power cables: out of the bottom of the control panel, down to the floor, along it to the back wall and
                // up into a junction box (they used to hang in mid-air and run out through the wall)
                var cable = new Color(0.08f, 0.08f, 0.08f);
                for (int i = 0; i < 3; i++)
                {
                    float x = gc.x - 1.55f - i * 0.07f, y0 = 0.045f;
                    Vector3 c0 = new Vector3(gc.x - 1.39f, 0.85f, gc.z + 0.12f + i * 0.08f);
                    Vector3 c1 = new Vector3(x, 0.85f, c0.z), c2 = new Vector3(x, y0, c0.z), c3 = new Vector3(x, y0, 12.82f), c4 = new Vector3(x, 1.25f, 12.82f);
                    b.Cyl(c0, c1, 0.022f, 0.022f, 8, Maps.Metal, cable, false);
                    b.Sphere(c1, Vector3.one * 0.023f, 6, Maps.Metal, cable, false);
                    b.Cyl(c1, c2, 0.022f, 0.022f, 8, Maps.Metal, cable, false);
                    b.Sphere(c2, Vector3.one * 0.023f, 6, Maps.Metal, cable, false);
                    b.Cyl(c2, c3, 0.022f, 0.022f, 8, Maps.Metal, cable, false);
                    b.Sphere(c3, Vector3.one * 0.023f, 6, Maps.Metal, cable, false);
                    b.Cyl(c3, c4, 0.022f, 0.022f, 8, Maps.Metal, cable, false);
                }
                // cable cover strip over the floor run, wall clips and the junction box
                b.Box(new Vector3(gc.x - 1.62f, 0.03f, (gc.z + 12.82f) / 2 + 0.1f), new Vector3(0.3f, 0.025f, 12.82f - gc.z - 0.1f), Maps.Metal, new Color(0.75f, 0.62f, 0.1f), null, false);
                foreach (float y in new[] { 0.45f, 0.85f }) b.Box(new Vector3(gc.x - 1.62f, y, 12.86f), new Vector3(0.26f, 0.04f, 0.05f), Maps.Metal, mid, null, false);
                b.Box(new Vector3(gc.x - 1.62f, 1.42f, 12.85f), new Vector3(0.4f, 0.4f, 0.14f), Maps.Metal, new Color(0.35f, 0.37f, 0.35f), null, false);
                b.Box(new Vector3(gc.x - 1.62f, 1.42f, 12.775f), new Vector3(0.3f, 0.04f, 0.01f), Mats.Lit("gen_warn", new Color(0.9f, 0.75f, 0.1f), 0f, 0.5f), Color.white, null, false);
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

        /// <summary>The Med Bay cabinet: what you left in it stays exactly there. It's stocked once, the first time;
        /// meds you take out are gone (no restocking, no copies).</summary>
        void RestockMedBay()
        {
            var prof = Profile.Current;
            if (_medCabinet == null || prof == null || !Application.isPlaying) return;
            if (prof.medCabinet == null) prof.medCabinet = new List<ItemData>();
            if (!prof.medStocked)
            {
                prof.medStocked = true;
                prof.medCabinet.Clear();
                foreach (var id in new[] { "ifak", "painkillers", "bandage", "bandage" }) prof.medCabinet.Add(new ItemData(id));
            }
            foreach (var d in prof.medCabinet)
            {
                var it = ItemFactory.Spawn(d, _medCabinet.transform.position, _medCabinet.transform.rotation);
                if (it == null) continue;
                if (!_medCabinet.Add(it)) Destroy(it.gameObject);
            }
        }

        public override void Build()
        {
            IsRaid = false; Indoor = true;
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
            b.Wall(new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), Ht, 0.3f, conc, new Color(0.6f, 0.61f, 0.6f), new List<Vector4> { new Vector4(W / 2, 2.4f, 0, 2.6f) });
            b.Wall(new Vector3(W / 2, 0, -D / 2), new Vector3(-W / 2, 0, -D / 2), Ht, 0.3f, conc, new Color(0.6f, 0.61f, 0.6f), new List<Vector4> { new Vector4(W / 2 - 2.5f, 1.8f, 0, 2.4f) }); // lift
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), Ht, 0.3f, conc, new Color(0.6f, 0.61f, 0.6f), new List<Vector4> { new Vector4(D / 2 + 2f, 2.4f, 0, 2.6f) });
            // west wall opens into the range tunnel
            b.Wall(new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), Ht, 0.3f, conc, new Color(0.6f, 0.61f, 0.6f), new List<Vector4> { new Vector4(D / 2, 3f, 0, 2.6f) });
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
            var wallTint = new Color(0.6f, 0.61f, 0.6f);
            // ---- Armory (north): weapons stash, workbench, ammo crates
            float ax = 6f, az0 = D / 2, az1 = D / 2 + 7f;
            b.Box(new Vector3(0, -0.1f, (az0 + az1) / 2), new Vector3(ax * 2, 0.2f, az1 - az0), floor, Color.white);
            b.Box(new Vector3(0, Ht + 0.1f, (az0 + az1) / 2), new Vector3(ax * 2, 0.2f, az1 - az0), conc, new Color(0.5f, 0.5f, 0.5f));
            // doorways to the expansion rooms (sealed until the Foreman builds them)
            b.Wall(new Vector3(-ax, 0, az0), new Vector3(-ax, 0, az1), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(1.5f, 2.0f, 0, 2.4f) });   // Storage Room
            b.Wall(new Vector3(-ax, 0, az1), new Vector3(ax, 0, az1), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(ax + 4.5f, 2.0f, 0, 2.4f) }); // Trophy Vault
            b.Wall(new Vector3(ax, 0, az1), new Vector3(ax, 0, az0), Ht, 0.3f, conc, wallTint, new List<Vector4> { new Vector4(2f, 2.0f, 0, 2.4f) });      // Generator Room
            b.Box(new Vector3(-1.4f, 0.6f, az1 - 0.16f), new Vector3(9.2f, 1.2f, 0.02f), paint, Color.white, null, false);   // paint band, open at the Trophy Vault doorway (x 3.3-5.7)
            b.Box(new Vector3(5.9f, 0.6f, az1 - 0.16f), new Vector3(0.2f, 1.2f, 0.02f), paint, Color.white, null, false);
            b.Box(new Vector3(0, Ht - 0.08f, (az0 + az1) / 2), new Vector3(1.2f, 0.08f, 0.25f), lampMat, Color.white, null, false);
            b.AddLight(new Vector3(0, Ht - 0.4f, (az0 + az1) / 2), new Color(1f, 0.88f, 0.7f), 9f, 1.4f, true);
            Stash.Add(StashSection.Create(transform, new Vector3(0, 0, az1 - 0.25f), 0, StashKind.Weapons));
            b.Table(new Vector3(-ax + 0.6f, 0, (az0 + az1) / 2 + 0.5f), 90, new Vector2(2.4f, 0.9f), 0.9f);
            if (Application.isPlaying) PaintBooth.Create(transform, new Vector3(-ax + 0.6f, 0, (az0 + az1) / 2 + 0.5f), -90f);   // the paint booth on the workbench
            b.Box(new Vector3(-ax + 0.17f, 1.5f, (az0 + az1) / 2 + 0.5f), new Vector3(0.05f, 1.0f, 2.4f), Mats.Lit("pegboard", Color.white, 0.1f, 0.3f, Mats.Wood, null, 0.3f, 0.1f), new Color(0.5f, 0.45f, 0.35f), null, false);
            b.Crate(new Vector3(ax - 1.0f, 0, az0 + 0.9f), 5, new Vector3(0.9f, 0.5f, 0.6f));
            b.Crate(new Vector3(ax - 1.0f, 0, az0 + 1.5f), -8, new Vector3(0.9f, 0.5f, 0.6f));
            b.Crate(new Vector3(ax - 1.0f, 0.5f, az0 + 1.2f), 12, new Vector3(0.8f, 0.4f, 0.5f));
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
            // the wardrobe: clothes for your operator, on the west wall of the main hall (south of the range tunnel)
            if (Application.isPlaying) Deliveries.Create(transform, new Vector3(-W / 2 + 0.17f, 1.55f, 3.9f), -90f);   // where the Lost & Found bin used to be
            if (Application.isPlaying) Wardrobe.Create(transform, new Vector3(-W / 2 + 0.16f, 1.45f, -2.6f), -90f, new Vector3(-W / 2 + 0.6f, 0, -4.1f), 90f);
            // a wall screen on the south wall, right of the lift
            Posters.Screen(transform, new Vector3(5.6f, 1.75f, -D / 2 + 0.16f), 180f, new Vector2(1.1f, 0.65f));
            Posters.Discord(transform, new Vector3(7.15f, 1.7f, -D / 2 + 0.16f), 180f, new Vector2(0.72f, 0.95f));   // right of the VIBE CODED screen
            // the secret one: low on the wall behind the raid kiosk - you have to look behind it
            Posters.Put(transform, new Vector3(W / 2 - 0.55f, 0.5f, D / 2 - 0.16f), 0f, "#CHUDLIFE", new Color(0.1f, 0.1f, 0.12f), new Color(0.3f, 1f, 0.4f), new Vector2(0.44f, 0.26f), 0.13f);   // tucked low in the corner behind the kiosk
            if (Application.isPlaying) Pug.Create(transform, new Vector3(6.4f, 0, -4.4f), new Vector3(4.9f, 0, -5.1f), new Rect(-6.5f, -4.5f, 13f, 9f));   // the bunker pug: bed and bowl in the south-east corner   // west wall of the main hall, by the range tunnel
            // GEAR wall on the south side: more room for meds, mags, sights, attachments, loot and pistols
            Stash.Add(StashSection.Create(transform, new Vector3(-4.95f, 0, -D / 2 + 0.25f), 180, StashKind.Gear));
            LoadStash();
            RestockMedBay();
            Kiosk.Create(transform, new Vector3(W / 2 - 0.9f, 0, 4.2f), Quaternion.Euler(0, 90, 0));
            // workbench + props
            b.Crate(new Vector3(3.6f, 0, D / 2 - 0.7f), 10, new Vector3(1.0f, 0.6f, 0.6f));
            b.Crate(new Vector3(3.4f, 0.6f, D / 2 - 0.6f), -5, new Vector3(0.8f, 0.5f, 0.5f));
            b.Barrel(new Vector3(-7.45f, 0, -2.2f), new Color(0.2f, 0.3f, 0.2f));
            // bed along the west wall
            b.Box(new Vector3(-7.45f, 0.3f, -3.75f), new Vector3(0.9f, 0.15f, 2.0f), Maps.Metal, new Color(0.3f, 0.32f, 0.3f), null, true, Synth.Surface.Metal);
            b.Box(new Vector3(-7.45f, 0.45f, -3.75f), new Vector3(0.85f, 0.15f, 1.9f), Mats.Lit("mattress", new Color(0.35f, 0.38f, 0.3f), 0, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0), Color.white, null, false, Synth.Surface.Dirt, 0.05f);
            SellStation.Create(transform, new Vector3(-0.85f, 0, -D / 2 + 0.66f), 180f);   // between the column and the lift's CALL panel   // sell loot right here, without going up to the Market
            // settings are in the menu now (LEFT MENU button > SETTINGS)
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
                HubProps.SteelTarget(transform, new Vector3(x, 2.4f, -0.8f + i * 0.8f), 0.35f + i * 0.08f, 90f);   // face the firing line (+x)
                b.AddLight(new Vector3(x, 2.4f, 0), new Color(1f, 0.9f, 0.75f), 6f, 0.9f);
                b.Sign(new Vector3(x + 0.5f, 2.3f, 1.55f), Quaternion.identity, (7 + i * 8) + "m", Color.white, 0.12f);
            }
            DressBunker(W, D, Ht, ax, az0, az1, rx0, rx1, rz0, rz1, L);
            Spawn(new Vector3(0, 0.05f, -2f), 0);
        }

        /// <summary>
        /// Makes the bunker read as a real underground shelter: raw concrete with heavy ceiling beams and wall columns,
        /// strip lights in metal housings and caged bulkhead lamps, a ventilation duct, conduit and an electrical panel,
        /// hazard stripes at every doorway, a floor drain, an extinguisher, exit and sector stencils. Visual only (apart
        /// from the columns), kept clear of the terminal, stash, lift and doorways.
        /// </summary>
        void DressBunker(float W, float D, float Ht, float ax, float az0, float az1, float rx0, float rx1, float rz0, float rz1, float L)
        {
            var b = B;
            var conc = Maps.Concrete;
            Color beamCol = new Color(0.5f, 0.51f, 0.5f), colCol = new Color(0.56f, 0.57f, 0.56f);
            var metal = Mats.Lit("bunker_metal", Color.white, 0.7f, 0.45f, Mats.Metal, null, 0.35f, 0.4f);
            var galv = Mats.Lit("bunker_galv", new Color(0.62f, 0.64f, 0.66f), 0.8f, 0.5f, Mats.Metal, null, 0.3f, 0.3f);
            var glow = Mats.Lit("bulkhead_glow", new Color(1f, 0.92f, 0.75f), 0, 0.6f, null, null, 0, 0, false, 0.5f, new Color(1.6f, 1.4f, 1.0f));
            var yellow = Mats.Lit("hazard_y", new Color(0.85f, 0.68f, 0.08f), 0f, 0.3f, Mats.Noise, null, 0.5f, 0.3f);
            var black = Mats.Lit("hazard_k", new Color(0.08f, 0.08f, 0.08f), 0f, 0.3f, Mats.Noise, null, 0.5f, 0.3f);
            // ---- main hall: beams across the ceiling, columns where they meet the walls, a caged lamp on each column
            float[] beamsX = { -6.5f, -2.5f, 2.5f, 6.5f };
            foreach (float x in beamsX)
            {
                b.Box(new Vector3(x, Ht - 0.18f, 0), new Vector3(0.36f, 0.36f, D), conc, beamCol, null, false);
                foreach (float side in new[] { -1f, 1f })
                {
                    if (side < 0 && Mathf.Abs(x - 2.5f) < 1.5f) continue;   // the lift
                    if (side < 0 && x < 0f) continue;                         // the south-west wall is the Gear wall: no columns in front of it
                    float wz = side * (D / 2 - 0.15f) - side * 0.15f;
                    b.Box(new Vector3(x, Ht / 2, wz), new Vector3(0.36f, Ht, 0.3f), conc, colCol, null, true);
                    BulkheadLamp(new Vector3(x, 2.35f, wz - side * 0.2f), side < 0 ? 180f : 0f, metal, glow);   // facing into the room
                }
            }
            // strip lights get metal housings
            for (int i = -1; i <= 1; i++) b.Box(new Vector3(i * 5, Ht - 0.025f, 0), new Vector3(1.4f, 0.05f, 0.36f), metal, new Color(0.35f, 0.36f, 0.37f), null, false);
            // ventilation duct with flanges, under the beams
            float dz = -2.6f, dy = Ht - 0.62f;
            b.Box(new Vector3(0, dy, dz), new Vector3(W - 0.4f, 0.4f, 0.6f), galv, Color.white, null, false);
            for (float x = -W / 2 + 1f; x < W / 2 - 0.5f; x += 2f) b.Box(new Vector3(x, dy, dz), new Vector3(0.06f, 0.48f, 0.68f), galv, new Color(0.8f, 0.8f, 0.8f), null, false);
            b.Box(new Vector3(-4f, dy - 0.24f, dz), new Vector3(0.5f, 0.06f, 0.4f), metal, new Color(0.25f, 0.25f, 0.25f), null, false);   // vent grille
            b.Box(new Vector3(3.5f, dy - 0.24f, dz), new Vector3(0.5f, 0.06f, 0.4f), metal, new Color(0.25f, 0.25f, 0.25f), null, false);
            // electrical panel + conduit on the west wall (left of the range tunnel)
            float wx = -W / 2 + 0.17f;
            b.Box(new Vector3(wx + 0.06f, 1.55f, -4.2f), new Vector3(0.14f, 0.8f, 0.6f), metal, new Color(0.42f, 0.44f, 0.42f), null, false);
            b.Box(new Vector3(wx + 0.14f, 1.75f, -4.2f), new Vector3(0.02f, 0.12f, 0.2f), yellow, Color.white, null, false);   // warning label
            b.Cyl(new Vector3(wx + 0.04f, 1.95f, -4.05f), new Vector3(wx + 0.04f, Ht - 0.3f, -4.05f), 0.025f, 0.025f, 6, metal, new Color(0.5f, 0.5f, 0.5f), false);
            b.Cyl(new Vector3(wx + 0.04f, 1.95f, -4.35f), new Vector3(wx + 0.04f, Ht - 0.3f, -4.35f), 0.025f, 0.025f, 6, metal, new Color(0.5f, 0.5f, 0.5f), false);
            b.Sign(new Vector3(wx + 0.02f, 2.6f, -3.0f), Quaternion.Euler(0, -90, 0), "SECTOR B-2", new Color(0.85f, 0.85f, 0.8f), 0.16f);
            // fire extinguisher by the lift, exit sign over it
            float sz = -D / 2 + 0.2f;
            b.Cyl(new Vector3(4.3f, 0.45f, sz + 0.1f), new Vector3(4.3f, 1.0f, sz + 0.1f), 0.08f, 0.08f, 12, Mats.Lit("extinguisher", new Color(0.75f, 0.08f, 0.06f), 0.3f, 0.6f), Color.white, false);
            b.Cyl(new Vector3(4.3f, 1.0f, sz + 0.1f), new Vector3(4.3f, 1.08f, sz + 0.1f), 0.03f, 0.025f, 8, metal, new Color(0.15f, 0.15f, 0.15f), false);
            b.Box(new Vector3(4.3f, 1.3f, sz + 0.02f), new Vector3(0.3f, 0.2f, 0.01f), Mats.Lit("ext_sign", new Color(0.8f, 0.1f, 0.08f), 0, 0.3f), Color.white, null, false);
            b.Box(new Vector3(2.5f, 2.72f, sz + 0.06f), new Vector3(0.5f, 0.18f, 0.08f), Mats.Lit("exit_sign", new Color(0.1f, 0.6f, 0.25f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.2f, 1.4f, 0.5f)), Color.white, null, false);
            // hazard stripes at every doorway, a floor drain
            HazardStripe(new Vector3(2.5f, 0.004f, -D / 2 + 0.35f), 0f, 2.2f, yellow, black);
            HazardStripe(new Vector3(0, 0.004f, D / 2 - 0.35f), 0f, 2.6f, yellow, black);
            HazardStripe(new Vector3(W / 2 - 0.35f, 0.004f, -2f), 90f, 2.6f, yellow, black);
            HazardStripe(new Vector3(-W / 2 + 0.35f, 0.004f, 0f), 90f, 3.2f, yellow, black);
            b.Box(new Vector3(-2.6f, 0.004f, 1.2f), new Vector3(0.45f, 0.008f, 0.45f), metal, new Color(0.2f, 0.2f, 0.2f), null, false);
            for (int i = 0; i < 5; i++) b.Box(new Vector3(-2.6f - 0.18f + i * 0.09f, 0.009f, 1.2f), new Vector3(0.025f, 0.004f, 0.4f), metal, new Color(0.1f, 0.1f, 0.1f), null, false);
            // ---- armory: two beams, columns, lamp housing
            foreach (float z in new[] { az0 + 2f, az1 - 2f })
            {
                b.Box(new Vector3(0, Ht - 0.18f, z), new Vector3(ax * 2, 0.36f, 0.36f), conc, beamCol, null, false);
                if (Mathf.Abs(z - 11f) > 1.6f) b.Box(new Vector3(ax - 0.3f, Ht / 2, z), new Vector3(0.3f, Ht, 0.36f), conc, colCol, null, true);
                else b.Box(new Vector3(ax - 0.3f, Ht - 0.5f, z), new Vector3(0.3f, 1f, 0.36f), conc, colCol, null, false);   // over the Generator Room doorway: just a haunch
                b.Box(new Vector3(-ax + 0.3f, Ht - 0.5f, z), new Vector3(0.3f, 1f, 0.36f), conc, colCol, null, false);   // short haunch over the workbench
            }
            b.Box(new Vector3(0, Ht - 0.025f, (az0 + az1) / 2), new Vector3(1.4f, 0.05f, 0.36f), metal, new Color(0.35f, 0.36f, 0.37f), null, false);
            // ---- armor room: beams and a lamp housing
            foreach (float z in new[] { rz0 + 2.5f, rz1 - 2.5f })
                b.Box(new Vector3((rx0 + rx1) / 2, Ht - 0.18f, z), new Vector3(rx1 - rx0, 0.36f, 0.36f), conc, beamCol, null, false);
            b.Box(new Vector3((rx0 + rx1) / 2, Ht - 0.025f, (rz0 + rz1) / 2), new Vector3(0.36f, 0.05f, 1.4f), metal, new Color(0.35f, 0.36f, 0.37f), null, false);
            // ---- range tunnel: beams every few metres
            for (float x = -W / 2 - 3f; x > -W / 2 - L + 1f; x -= 4f)
                b.Box(new Vector3(x, 2.52f, 0), new Vector3(0.3f, 0.16f, 3.4f), conc, beamCol, null, false);
        }

        void BulkheadLamp(Vector3 at, float yaw, Material metal, Material glow)
        {
            var b = B;
            var q = Quaternion.Euler(0, yaw, 0);
            b.Box(at + q * new Vector3(0, 0, 0.03f), new Vector3(0.2f, 0.26f, 0.06f), metal, new Color(0.3f, 0.32f, 0.3f), q, false);
            b.Sphere(at + q * new Vector3(0, 0, -0.03f), new Vector3(0.07f, 0.1f, 0.06f), 8, glow, Color.white, false);
            foreach (float x in new[] { -0.06f, 0f, 0.06f }) b.Box(at + q * new Vector3(x, 0, -0.07f), new Vector3(0.012f, 0.22f, 0.012f), metal, new Color(0.2f, 0.2f, 0.2f), q, false);
            b.Box(at + q * new Vector3(0, 0, -0.07f), new Vector3(0.16f, 0.012f, 0.012f), metal, new Color(0.2f, 0.2f, 0.2f), q, false);
        }

        void HazardStripe(Vector3 center, float yaw, float length, Material yellow, Material black)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            int n = Mathf.Max(2, Mathf.RoundToInt(length / 0.25f));
            for (int i = 0; i < n; i++)
            {
                var c = center + q * new Vector3(-length / 2 + (i + 0.5f) * length / n, 0, 0);
                B.Box(c, new Vector3(length / n, 0.008f, 0.3f), i % 2 == 0 ? yellow : black, Color.white, q, false);   // alternating blocks, edge to edge
            }
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
            IsRaid = false; Indoor = true;
            Safe = true;
            Ambience = Synth.Amb.Market;
            NavBounds = new Bounds(new Vector3(0, 3, 0), new Vector3(40, 10, 32));
            Lighting(new Color(0.55f, 0.6f, 0.85f), 0.25f, new Vector3(40, 200, 0), new Color(0.24f, 0.25f, 0.3f), new Color(0.24f, 0.22f, 0.2f), new Color(0.12f, 0.11f, 0.1f),
                new Color(0.12f, 0.12f, 0.16f), 0.012f, new Color(0.03f, 0.04f, 0.08f), new Color(0.15f, 0.12f, 0.15f), 0.3f, 0.6f);
            var b = B;
            // a compact hall: step out of the lift (east wall) and everything is in view -
            // SELL right ahead, the traders along the back (north) wall, Bruno on the left (west) wall
            float W = 32, D = 24, H = 7;
            b.Box(new Vector3(0, -0.1f, 0), new Vector3(W + 1, 0.2f, D + 1), Maps.ConcreteDark, new Color(0.7f, 0.7f, 0.7f), null, true, Synth.Surface.Concrete);
            var corr = Maps.Corrugated;
            Color wc = new Color(0.45f, 0.5f, 0.52f);
            b.Wall(new Vector3(-W / 2, 0, D / 2), new Vector3(W / 2, 0, D / 2), H, 0.3f, corr, wc, null, Synth.Surface.Metal);
            b.Wall(new Vector3(W / 2, 0, -D / 2), new Vector3(-W / 2, 0, -D / 2), H, 0.3f, corr, wc, new List<Vector4> { new Vector4(W / 2 + 2f, 6f, 0, 4.8f) }, Synth.Surface.Metal);   // the garage door opening
            b.Wall(new Vector3(W / 2, 0, D / 2), new Vector3(W / 2, 0, -D / 2), H, 0.3f, corr, wc, new List<Vector4> { new Vector4(D / 2 + 6f, 1.8f, 0, 2.4f) }, Synth.Surface.Metal); // lift
            b.Wall(new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2, 0, D / 2), H, 0.3f, corr, wc, null, Synth.Surface.Metal);
            b.Box(new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, D), corr, new Color(0.3f, 0.32f, 0.33f), null, true, Synth.Surface.Metal);
            for (int i = 0; i < 5; i++)   // roof trusses
            {
                float x = -W / 2 + (i + 0.5f) * W / 5;
                b.Box(new Vector3(x, H - 0.2f, 0), new Vector3(0.3f, 0.4f, D), Maps.Metal, new Color(0.3f, 0.3f, 0.3f), null, false);
            }
            // the GARAGE behind the roller door on the south wall: the concert stage is in there, and the door rolls up
            // when a CONCERT is on (it stays shut the rest of the time)
            {
                float gz0 = -D / 2 - 0.15f, gd = 7.5f, gx = -2f, gw = 11f, gh = 6.4f;
                b.Box(new Vector3(gx, -0.1f, gz0 - gd / 2), new Vector3(gw, 0.2f, gd), Maps.ConcreteDark, new Color(0.55f, 0.55f, 0.55f), null, true, Synth.Surface.Concrete);
                b.Box(new Vector3(gx, gh + 0.1f, gz0 - gd / 2), new Vector3(gw, 0.2f, gd), corr, new Color(0.3f, 0.32f, 0.33f), null, true, Synth.Surface.Metal);
                b.Wall(new Vector3(gx - gw / 2, 0, gz0), new Vector3(gx - gw / 2, 0, gz0 - gd), gh, 0.3f, corr, wc, null, Synth.Surface.Metal);
                b.Wall(new Vector3(gx + gw / 2, 0, gz0 - gd), new Vector3(gx + gw / 2, 0, gz0), gh, 0.3f, corr, wc, null, Synth.Surface.Metal);
                b.Wall(new Vector3(gx + gw / 2, 0, gz0 - gd), new Vector3(gx - gw / 2, 0, gz0 - gd), gh, 0.3f, corr, wc, null, Synth.Surface.Metal);
                b.AddLight(new Vector3(gx, gh - 0.6f, gz0 - gd / 2), new Color(0.9f, 0.85f, 0.8f), 9f, 0.8f);
                RollerDoor.Create(transform, new Vector3(gx, 0, -D / 2 + 0.05f), 6f, 4.8f);
            }
            // string lights + bunting
            var bulb = Mats.Lit("bulb", new Color(1f, 0.8f, 0.5f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(3f, 2f, 1f));
            var bunting = Mats.Lit("bunting", Color.white, 0f, 0.1f, Mats.Fabric, null, 0.3f, 0f);
            var flagCols = new[] { new Color(0.8f, 0.25f, 0.2f), new Color(0.9f, 0.7f, 0.2f), new Color(0.25f, 0.5f, 0.3f), new Color(0.25f, 0.4f, 0.65f), new Color(0.85f, 0.85f, 0.8f) };
            for (int s = 0; s < 3; s++)
            {
                float z = -6 + s * 6;
                for (int i = 0; i < 14; i++)
                {
                    float x = -W / 2 + 1.5f + i * (W - 3) / 13f;
                    float sag = Mathf.Sin(i / 13f * Mathf.PI) * 0.7f;
                    b.Sphere(new Vector3(x, 5.6f - sag, z), Vector3.one * 0.07f, 6, bulb, Color.white, false);
                    if (i < 13) { float sm = Mathf.Sin((i + 0.5f) / 13f * Mathf.PI) * 0.7f; b.Box(new Vector3(x + (W - 3) / 26f, 5.46f - sm, z), new Vector3(0.24f, 0.24f, 0.01f), bunting, flagCols[(i + s) % flagCols.Length], Quaternion.Euler(0, 0, 45), false); }
                }
                b.AddLight(new Vector3(-7, 5f, z), new Color(1f, 0.78f, 0.5f), 12f, 1.2f);
                b.AddLight(new Vector3(7, 5f, z), new Color(1f, 0.78f, 0.5f), 12f, 1.2f);
            }

            // ---- traders along the back wall, each under a banner with their name
            var defs = TraderStall.Defaults();
            var fabric = Mats.Lit("banner", Color.white, 0f, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0f);
            float[] stallX = { -10.5f, -3.5f, 3.5f, 10.5f };
            for (int i = 0; i < defs.Count && i < stallX.Length; i++)
            {
                Stalls.Add(TraderStall.Create(transform, new Vector3(stallX[i], 0, D / 2 - 3.6f), 0f, defs[i], 100 + i * 17));
                b.Box(new Vector3(stallX[i], 4.5f, D / 2 - 0.2f), new Vector3(2.4f, 2.0f, 0.03f), fabric, defs[i].color, null, false);       // banner
                b.Box(new Vector3(stallX[i], 5.54f, D / 2 - 0.22f), new Vector3(2.6f, 0.08f, 0.08f), Maps.Metal, new Color(0.3f, 0.3f, 0.3f), null, false); // rod
                b.Sign(new Vector3(stallX[i], 4.75f, D / 2 - 0.23f), Quaternion.identity, defs[i].name.ToUpper(), new Color(0.97f, 0.95f, 0.9f), 0.4f);
                b.Sign(new Vector3(stallX[i], 4.15f, D / 2 - 0.23f), Quaternion.identity, Short(defs[i].title), new Color(0.97f, 0.95f, 0.9f), 0.14f);
                b.AddLight(new Vector3(stallX[i], 3.8f, D / 2 - 5f), new Color(1f, 0.82f, 0.6f), 6f, 1.1f);
            }
            // ---- the stalls dressed up: a rug and two potted plants at each (and at Christmas, a garland of lights along the awning)
            var pot = Mats.Lit("plant_pot", Color.white, 0f, 0.3f, Mats.Noise, null, 0.3f, 0.1f);
            for (int i = 0; i < defs.Count && i < stallX.Length; i++)
            {
                var sc = new Vector3(stallX[i], 0, D / 2 - 3.6f);
                b.Box(sc + new Vector3(0, 0.01f, -1.6f), new Vector3(3.0f, 0.02f, 1.6f), Mats.Lit("rug", Color.white, 0f, 0.1f, Mats.Fabric, null, 0.3f, 0f), Color.Lerp(defs[i].color, new Color(0.3f, 0.25f, 0.2f), 0.4f), null, false);
                foreach (float px in new[] { -2.35f, 2.35f })
                {
                    var pp = sc + new Vector3(px, 0, -0.7f);
                    b.Cyl(pp, pp + Vector3.up * 0.45f, 0.2f, 0.16f, 10, pot, new Color(0.55f, 0.3f, 0.2f), false);
                    for (int k = 0; k < 5; k++) b.Sphere(pp + new Vector3(Mathf.Cos(k * 1.3f) * 0.12f, 0.6f + (k % 2) * 0.15f, Mathf.Sin(k * 1.3f) * 0.12f), Vector3.one * 0.18f, 6, pot, new Color(0.15f, 0.38f, 0.18f), false);
                }
                if (Festive.On) Festive.Garland(transform, sc + new Vector3(-2.05f, 2.9f, -0.62f), sc + new Vector3(2.05f, 2.9f, -0.62f), 0.25f);
            }
            // (the DLC store is only in the menu now - its DLC tab). Its wall has the Discord poster instead
            Posters.Discord(transform, new Vector3(-W / 2 + 0.2f, 1.75f, 5.2f), -90f, new Vector2(0.9f, 1.2f));
            // ---- players' own trading: a trade table and four stalls anyone can claim
            // (the trade table is gone: drop an item in the Market and other players can pick it up)
            // the black market: three screens in a row in the middle of the hall, facing the lift (you see them as you
            // step out), under their own hanging sign
            for (int bm = 0; bm < 3; bm++) ShopScreen.Create(transform, new Vector3(-1.5f, 0, -0.5f + bm * 2.6f), -90f);
            HangingSign(new Vector3(-1.5f, 4.4f, 2.1f), -90f, "BLACK MARKET", "player markets - buy, sell, trade", new Color(0.4f, 1f, 0.5f));
            // ---- the Exchange: a few steps from the lift doors, facing them
            SellStation.Create(transform, new Vector3(W / 2 - 7.5f, 0, -6f), -90f);
            // ---- the casino: along the south wall, east of the roller door (customers face the wall)
            Casino.Build(transform, new Vector3(8.6f, 0, -D / 2 + 1.0f), 180f);
            b.AddLight(new Vector3(8.6f, 4.2f, -D / 2 + 2.6f), new Color(1f, 0.75f, 0.45f), 7f, 1.3f);
            // ---- the Skin Wheel: between the roller door and the casino, facing into the hall
            SkinWheel.Create(transform, new Vector3(3.3f, 0, -D / 2 + 1.0f), 180f);
            b.AddLight(new Vector3(3.3f, 4.0f, -D / 2 + 2.4f), new Color(0.85f, 0.6f, 1f), 6f, 1.3f);
            // ---- Bruno the Foreman on the west wall, facing the room
            Foreman = QuestStall.Create(transform, new Vector3(-W / 2 + 3.6f, 0, -1f), -90f, 211);
            b.AddLight(new Vector3(-W / 2 + 4.6f, 3.6f, -1f), new Color(1f, 0.8f, 0.55f), 7f, 1.2f);
            // ---- big hanging signs, readable from the lift and from the middle of the hall
            HangingSign(new Vector3(0, 6.1f, D / 2 - 6.4f), 0f, "TRADERS", "guns, gear, ammo and meds", new Color(0.95f, 0.85f, 0.55f));
            HangingSign(new Vector3(W / 2 - 7.5f, 4.6f, -6f), -90f, "SELL", "any loot - best price", UI.Accent);
            HangingSign(new Vector3(-W / 2 + 4.8f, 4.6f, -1f), -90f, "BUNKER JOBS", "Bruno builds new rooms", new Color(0.95f, 0.75f, 0.1f));
            // ---- a lounge in the south-west corner
            var fire = new Vector3(-9f, 0, -8f);
            b.Barrel(fire, new Color(0.3f, 0.2f, 0.15f));
            b.Sphere(fire + Vector3.up * 0.95f, new Vector3(0.25f, 0.15f, 0.25f), 8, Mats.Lit("embers", new Color(1f, 0.4f, 0.1f), 0, 0.2f, null, null, 0, 0, false, 0.5f, new Color(3f, 0.9f, 0.2f)), Color.white, false);
            var fl = b.AddLight(fire + Vector3.up * 1.4f, new Color(1f, 0.55f, 0.25f), 7f, 1.6f);
            fl.gameObject.AddComponent<Flicker>();
            foreach (float sx in new[] { -1.7f, 1.7f })
            {
                b.Box(fire + new Vector3(sx, 0.42f, 0), new Vector3(0.45f, 0.06f, 2.2f), Maps.WoodPlank, new Color(0.55f, 0.42f, 0.3f), null, true, Synth.Surface.Wood);
                foreach (float sz in new[] { -0.9f, 0.9f }) b.Box(fire + new Vector3(sx, 0.2f, sz), new Vector3(0.4f, 0.4f, 0.08f), Maps.Metal, new Color(0.25f, 0.25f, 0.25f), null, false);
            }
            // crates stacked in the corners, out of the way
            foreach (var c in new[] { new Vector3(-W / 2 + 0.9f, 0, -D / 2 + 0.9f), new Vector3(-W / 2 + 2.1f, 0, -D / 2 + 0.9f), new Vector3(-W / 2 + 0.9f, 0, -D / 2 + 2.1f), new Vector3(W / 2 - 1.0f, 0, D / 2 - 1.0f), new Vector3(W / 2 - 2.2f, 0, D / 2 - 1.0f) })
                b.Crate(c, b.Rng.Range(-6f, 6f), new Vector3(1f, 0.8f, 1f));
            b.Crate(new Vector3(-W / 2 + 1.4f, 0.8f, -D / 2 + 1.4f), 8, new Vector3(0.9f, 0.7f, 0.9f));
            // ---- directory board right by the lift doors
            var nb = UI.Panel(transform, "Directory", new Vector3(W / 2 - 0.2f, 1.9f, -2.2f), Quaternion.Euler(0, 90, 0), new Vector2(1.6f, 1.15f), null, true, Skin.Paper);
            UI.Text(nb, "THE MARKET", new Vector3(0, 0.47f, 0), 0.07f, UI.PaperInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(nb, "SELL  -  straight ahead (orange counter)\nTRADERS  -  along the back wall (unlock with your level)\nBLACK MARKET  -  computer by the lift\nPLAYER STALLS + TRADE TABLE  -  sell to other players\nSKIN WHEEL  -  south wall, $10,000 a spin\nBUNKER JOBS  -  Bruno, far wall\nLIFT  -  back down to your bunker\n\nSafe zone - weapons cold.", new Vector3(-0.74f, 0.33f, 0), 0.042f, UI.PaperInk, TextAnchor.UpperLeft);
            b.Sign(new Vector3(0, 6.3f, D / 2 - 0.3f), Quaternion.identity, "THE MARKET", UI.Accent, 0.55f);
            Elevator.Create(b, transform, new Vector3(W / 2, 0, -6), 90, "Market", "Bunker", false, () => RagoGame.I.GoTo(Area.Bunker));
            b.Sign(new Vector3(W / 2 - 0.3f, 3.15f, -6), Quaternion.Euler(0, 90, 0), "LIFT - BUNKER", new Color(0.9f, 0.8f, 0.6f), 0.18f);
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; Spawn(new Vector3(4 + Mathf.Cos(a) * 2.5f, 0.05f, -2 + Mathf.Sin(a) * 2f), 0); }
        }

        static string Short(string title) { int i = title.IndexOf(" - "); return i > 0 ? title.Substring(i + 3) : title; }

        /// <summary>A sign board hanging from the roof on two rods, lettered on both sides, with a lamp on it.</summary>
        void HangingSign(Vector3 at, float yaw, string big, string small, Color col)
        {
            var b = B;
            var q = Quaternion.Euler(0, yaw, 0);
            float w = Mathf.Max(2.2f, big.Length * 0.32f + 0.6f);
            b.Box(at, new Vector3(w, 0.95f, 0.06f), Maps.Metal, new Color(0.08f, 0.08f, 0.09f), q, false);
            b.Box(at + Vector3.down * 0.5f, new Vector3(w, 0.05f, 0.08f), Maps.Metal, col * 0.8f, q, false);
            foreach (float sx in new[] { -0.4f, 0.4f })
            {
                var top = at + q * new Vector3(sx * w, 0.47f, 0);
                b.Cyl(top, new Vector3(top.x, 7f, top.z), 0.015f, 0.015f, 6, Maps.Metal, Color.gray, false);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var face = at + q * new Vector3(0, 0, side * 0.035f);
                var rot = q * Quaternion.Euler(0, side < 0 ? 0 : 180, 0);
                b.Sign(face + Vector3.up * 0.12f, rot, big, col, 0.42f);
                b.Sign(face + Vector3.down * 0.27f, rot, small, new Color(0.9f, 0.9f, 0.88f), 0.12f);
            }
            b.AddLight(at + Vector3.down * 0.9f, col, 5f, 0.9f);
        }
    }

    public class Flicker : MonoBehaviour
    {
        Light _l; float _base;
        // a flickering light is a cheap vertex light: as a pixel light its changing brightness kept stealing the room's one
        // pixel-light slot (Quest) from the ceiling lamp, and the whole bunker went dark and bright again
        void Start() { _l = GetComponent<Light>(); _base = _l.intensity; _l.renderMode = LightRenderMode.ForceVertex; }
        void Update() { _l.intensity = _base * (0.75f + Mathf.PerlinNoise(Time.time * 6f, GetInstanceID()) * 0.5f); }
    }

    /// <summary>A paper poster stuck flat on a wall. The reader stands at local -Z (yaw turns it to face the room).</summary>
    public static class Posters
    {
        public static void Put(Transform parent, Vector3 pos, float yaw, string text, Color paper, Color ink, Vector2 size, float textSize)
        {
            var t = new GameObject("Poster").transform;
            t.SetParent(parent, false);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0);
            var mb = new MB();
            mb.Box(Vector3.zero, new Vector3(size.x, size.y, 0.004f), paper, 0.002f);
            mb.Box(new Vector3(0, 0, -0.0025f), new Vector3(size.x * 0.92f, size.y * 0.92f, 0.001f), Color.Lerp(paper, ink, 0.12f), 0f);   // a printed border
            foreach (var c in new[] { new Vector2(-1, 1), new Vector2(1, 1) })   // two bits of tape on the top corners
                mb.Box(new Vector3(c.x * size.x * 0.44f, c.y * size.y * 0.48f, -0.003f), new Vector3(size.x * 0.14f, 0.03f, 0.001f), new Color(0.9f, 0.88f, 0.75f, 1f), 0f, 1, Quaternion.Euler(0, 0, c.x * 25f));
            var go = mb.Build("Paper", t, Mats.Lit("poster_paper", Color.white, 0f, 0.15f, Mats.Plaster, null, 0.3f, 0f));
            UI.Text(t, text, new Vector3(0, 0, -0.004f), textSize, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        public const string DiscordUrl = "https://discord.gg/yzXkXEA4gF";
        static float _discordT;

        /// <summary>The community poster: a glowing Discord-blue screen with a JOIN button that opens the server invite in the
        /// headset's browser. The reader stands at local -Z.</summary>
        public static void Discord(Transform parent, Vector3 pos, float yaw, Vector2 size)
        {
            var t = new GameObject("DiscordPoster").transform;
            t.SetParent(parent, false);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0);
            var bez = new MB();
            bez.Box(new Vector3(0, 0, 0.02f), new Vector3(size.x + 0.06f, size.y + 0.06f, 0.05f), new Color(0.05f, 0.05f, 0.06f), 0.006f);
            bez.Box(new Vector3(0, 0, 0.055f), new Vector3(0.25f, 0.2f, 0.03f), new Color(0.08f, 0.08f, 0.09f), 0.004f);   // wall bracket
            bez.Build("Bezel", t, Mats.Lit("screen_bezel", Color.white, 0.3f, 0.6f));
            var blurple = new Color(0.345f, 0.396f, 0.949f);
            var pan = new MB();
            pan.Box(new Vector3(0, 0, -0.006f), new Vector3(size.x, size.y, 0.002f), Color.white, 0f);
            pan.Build("Panel", t, Mats.Lit("discord_screen", blurple * 0.55f, 0f, 0.9f, null, null, 0f, 0f, false, 0.5f, blurple * 0.45f));
            // a chat bubble icon: a rounded white block with two "eyes" and a little tail
            var ic = new MB(2);
            float iy = size.y * 0.26f, iw = size.x * 0.34f;
            ic.Sub(0).Box(new Vector3(0, iy, -0.009f), new Vector3(iw, iw * 0.62f, 0.004f), Color.white, iw * 0.2f);
            ic.Sub(0).Box(new Vector3(-iw * 0.28f, iy - iw * 0.36f, -0.009f), new Vector3(iw * 0.18f, iw * 0.18f, 0.004f), Color.white, 0.002f, 1, Quaternion.Euler(0, 0, 45));
            foreach (float ex in new[] { -1f, 1f }) ic.Sub(1).Sphere(new Vector3(ex * iw * 0.17f, iy + iw * 0.02f, -0.0115f), new Vector3(iw * 0.13f, iw * 0.16f, 0.002f), 10, Color.white);
            ic.Build("Icon", t, Mats.Lit("discord_icon", Color.white, 0f, 0.5f, null, null, 0f, 0f, false, 0.5f, new Color(0.6f, 0.6f, 0.6f)), Mats.Lit("discord_eyes", blurple * 0.55f, 0f, 0.5f));
            float k = size.x / 0.8f;
            UI.Text(t, "JOIN THE", new Vector3(0, -size.y * 0.02f, -0.009f), 0.075f * k, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(t, "DISCORD", new Vector3(0, -size.y * 0.13f, -0.009f), 0.13f * k, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(t, "discord.gg/yzXkXEA4gF", new Vector3(0, -size.y * 0.24f, -0.009f), 0.042f * k, new Color(0.8f, 0.84f, 1f), TextAnchor.MiddleCenter);
            UI.Button(t, "JOIN", new Vector3(0, -size.y * 0.37f, -0.006f), new Vector2(size.x * 0.55f, size.y * 0.13f), OpenDiscord, new Color(0.2f, 0.62f, 0.32f), BtnStyle.Key);
        }

        static void OpenDiscord()
        {
            if (Time.unscaledTime < _discordT) return;   // one press, one browser tab
            _discordT = Time.unscaledTime + 3f;
            RagoGame.I?.Player?.Toast("Opening the Discord invite in your browser...");
            Application.OpenURL(DiscordUrl);
        }

        /// <summary>A wall-mounted screen (black bezel, glowing panel) that owns up to how the game was made.</summary>
        public static void Screen(Transform parent, Vector3 pos, float yaw, Vector2 size)
        {
            var t = new GameObject("VibeScreen").transform;
            t.SetParent(parent, false);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0);
            var bez = new MB();
            bez.Box(new Vector3(0, 0, 0.02f), new Vector3(size.x + 0.06f, size.y + 0.06f, 0.05f), new Color(0.05f, 0.05f, 0.06f), 0.006f);
            bez.Box(new Vector3(0, 0, 0.055f), new Vector3(0.3f, 0.2f, 0.03f), new Color(0.08f, 0.08f, 0.09f), 0.004f);   // wall bracket
            bez.Build("Bezel", t, Mats.Lit("screen_bezel", Color.white, 0.3f, 0.6f));
            var pan = new MB();
            pan.Box(new Vector3(0, 0, -0.006f), new Vector3(size.x, size.y, 0.002f), Color.white, 0f);
            pan.Build("Panel", t, Mats.Lit("vibe_screen", new Color(0.02f, 0.04f, 0.07f), 0f, 0.9f, null, null, 0f, 0f, false, 0.5f, new Color(0.03f, 0.08f, 0.14f)));
            float k = size.y / 0.65f;
            UI.Text(t, "YES, IT'S", new Vector3(0, size.y * 0.24f, -0.009f), 0.13f * k, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(t, "VIBE CODED", new Vector3(0, -size.y * 0.04f, -0.009f), 0.21f * k, new Color(0.35f, 1f, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(t, "every line written by an AI", new Vector3(0, -size.y * 0.3f, -0.009f), 0.065f * k, new Color(0.6f, 0.65f, 0.7f), TextAnchor.MiddleCenter);
        }
    }
}
