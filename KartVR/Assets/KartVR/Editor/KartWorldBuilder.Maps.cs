using System.Collections.Generic;
using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// The newer maps: Ice Bumper rink, the big Free Roam world, and the hub
    /// portals that lead to the extra maps. (The Canyon race uses the shared race
    /// track builder with its own <see cref="Circuit"/>.)
    /// </summary>
    public static partial class KartWorldBuilder
    {
        // ================================================================== hub portals

        static void ExtraHubPortals(Transform hub)
        {
            var portalCanyon = MakeTransparent(Mat("PortalCanyon", new Color(1f, 0.7f, 0.25f), 0.9f, 0f, null, null, new Color(1f, 0.6f, 0.2f) * 0.8f), 0.55f);
            var portalIce = MakeTransparent(Mat("PortalIce", new Color(0.75f, 0.95f, 1f), 0.9f, 0f, null, null, new Color(0.6f, 0.9f, 1f) * 0.8f), 0.55f);
            Portal(hub, "CanyonPortal", KartMode.CanyonRace, new Vector3(-37f, 0f, 33f), portalCanyon, boost, "CANYON RACE");
            Portal(hub, "IcePortal", KartMode.IceBumper, new Vector3(37f, 0f, 33f), portalIce, neonCyan, "ICE BUMPER");
        }

        // ================================================================== ice bumper

        static Transform BuildIceRink()
        {
            var b = Group("IceRink", null, IceOrigin);
            var rng = new System.Random(5);
            var ice = Mat("Ice", new Color(0.78f, 0.92f, 1f), 0.95f, 0.1f);
            var snow = Mat("Snow", new Color(0.95f, 0.97f, 1f), 0.15f);
            var icyWater = Mat("IcyWater", new Color(0.18f, 0.38f, 0.55f), 0.95f, 0.2f);
            var carrot = Mat("Carrot", new Color(1f, 0.5f, 0.1f), 0.3f);

            // Round sheet of ice over freezing water. Fall off = lose a point; it's slippery up there.
            var floor = Prim(PrimitiveType.Cylinder, "Rink", b, new Vector3(0f, -1f, 0f), new Vector3(48f, 1f, 48f), ice);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Prim(PrimitiveType.Cylinder, "SnowEdge", b, new Vector3(0f, -1.3f, 0f), new Vector3(49.5f, 1f, 49.5f), snow, false);
            Prim(PrimitiveType.Cylinder, "Underside", b, new Vector3(0f, -5f, 0f), new Vector3(36f, 3f, 36f), ice, false);
            Prim(PrimitiveType.Cylinder, "CenterLine", b, new Vector3(0f, 0.005f, 0f), new Vector3(14f, 0.005f, 14f), neonCyan, false);
            Prim(PrimitiveType.Cylinder, "CenterIce", b, new Vector3(0f, 0.01f, 0f), new Vector3(13.4f, 0.005f, 13.4f), ice, false);
            Prim(PrimitiveType.Cube, "Water", b, new Vector3(0f, -12f, 0f), new Vector3(300f, 1f, 300f), icyWater, false);
            for (int i = 0; i < 48; i++)
            {
                float a = i * 360f / 48f;
                var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.02f, 23.6f);
                Prim(PrimitiveType.Cube, "RimLight", b, p, new Vector3(1.2f, 0.06f, 0.3f), i % 2 == 0 ? neonCyan : neonWhite, false, Quaternion.Euler(0f, a, 0f));
            }

            // Two crossed sweeper bars, turning slowly.
            Prim(PrimitiveType.Cylinder, "Hub", b, new Vector3(0f, 0.6f, 0f), new Vector3(3f, 0.6f, 3f), metal);
            var sweeper = new GameObject("Sweeper");
            sweeper.transform.SetParent(b, false);
            var srb = sweeper.AddComponent<Rigidbody>();
            srb.isKinematic = true;
            srb.interpolation = RigidbodyInterpolation.Interpolate;
            sweeper.AddComponent<Rotator>().degreesPerSecond = -22f;
            foreach (float yaw in new[] { 0f, 90f })
            {
                Prim(PrimitiveType.Cube, "Bar", sweeper.transform, new Vector3(0f, 0.5f, 0f), new Vector3(40f, 0.55f, 0.7f), hazard, true, Quaternion.Euler(0f, yaw, 0f));
                Prim(PrimitiveType.Cube, "BarGlow", sweeper.transform, new Vector3(0f, 0.82f, 0f), new Vector3(40f, 0.08f, 0.2f), neonCyan, false, Quaternion.Euler(0f, yaw, 0f));
            }

            // Snowman bumpers: bounce off them.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                var s = Group("Snowman" + i, b, new Vector3(Mathf.Sin(a) * 13f, 0f, Mathf.Cos(a) * 13f), Quaternion.Euler(0f, a * Mathf.Rad2Deg + 180f, 0f));
                s.gameObject.AddComponent<Bouncer>();
                Prim(PrimitiveType.Sphere, "Base", s, new Vector3(0f, 1.1f, 0f), Vector3.one * 2.4f, snow);
                Prim(PrimitiveType.Sphere, "Middle", s, new Vector3(0f, 2.6f, 0f), Vector3.one * 1.7f, snow);
                Prim(PrimitiveType.Sphere, "Head", s, new Vector3(0f, 3.75f, 0f), Vector3.one * 1.1f, snow, false);
                Prim(PrimitiveType.Cylinder, "Nose", s, new Vector3(0f, 3.75f, -0.65f), new Vector3(0.14f, 0.25f, 0.14f), carrot, false, Quaternion.Euler(90f, 0f, 0f));
                foreach (float x in new[] { -0.2f, 0.2f })
                    Prim(PrimitiveType.Sphere, "Eye", s, new Vector3(x, 3.95f, -0.48f), Vector3.one * 0.13f, dark, false);
                Prim(PrimitiveType.Cylinder, "HatBrim", s, new Vector3(0f, 4.25f, 0f), new Vector3(1.1f, 0.04f, 1.1f), dark, false);
                Prim(PrimitiveType.Cylinder, "Hat", s, new Vector3(0f, 4.6f, 0f), new Vector3(0.7f, 0.35f, 0.7f), dark, false);
            }

            var spawn = new GameObject("IceSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.IceBumper;
            spawn.killY = -6f;
            spawn.transform.SetParent(b, false);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var pos = new Vector3(Mathf.Sin(a) * 19f, 0f, Mathf.Cos(a) * 19f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(-pos, Vector3.up);
            }

            // Icebergs and snowy islands with pines in the water, mountains far off.
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = 40f + (float)rng.NextDouble() * 70f;
                var p = new Vector3(Mathf.Sin(a) * d, -11.5f, Mathf.Cos(a) * d);
                float sz = 3f + (float)rng.NextDouble() * 7f;
                if (i % 3 == 0)
                {
                    Prim(PrimitiveType.Cylinder, "Island", b, p + new Vector3(0f, 1f, 0f), new Vector3(sz * 2f, 1.5f, sz * 2f), snow, false);
                    for (int k = 0; k < 3; k++)
                        SnowyPine(b, p + new Vector3(((float)rng.NextDouble() - 0.5f) * sz, 2.5f, ((float)rng.NextDouble() - 0.5f) * sz), 0.8f + (float)rng.NextDouble() * 0.5f, snow);
                }
                else
                    Prim(PrimitiveType.Cube, "Iceberg", b, p + new Vector3(0f, sz * 0.4f, 0f), new Vector3(sz, sz * 1.3f, sz * 0.8f), ice, false,
                        Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f));
            }

            var board = Board(b, "IceBoard", new Vector3(0f, 11f, 38f), Quaternion.identity, new Vector2(14f, 9f), 4f, "ICE BUMPER", neonCyan);
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Bumper;
            info.text = board;
            info.title = "ICE BUMPER";
            Prim(PrimitiveType.Cube, "BoardPillar", b, new Vector3(0f, -1f, 38.3f), new Vector3(1f, 22f, 1f), metal);
            return b;
        }

        static void SnowyPine(Transform parent, Vector3 pos, float s, Material snow)
        {
            var t = Group("SnowyPine", parent, pos);
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 1f * s, 0f), new Vector3(0.4f * s, 1f * s, 0.4f * s), bark, false);
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Sphere, "Needles", t, new Vector3(0f, (2.4f + i * 1.2f) * s, 0f), new Vector3((3f - i * 0.85f) * s, 1.4f * s, (3f - i * 0.85f) * s), leavesDark, false);
                Prim(PrimitiveType.Sphere, "Snow", t, new Vector3(0f, (2.75f + i * 1.2f) * s, 0f), new Vector3((2.2f - i * 0.65f) * s, 0.6f * s, (2.2f - i * 0.65f) * s), snow, false);
            }
        }

        // ================================================================== free roam: one big world

        const float FreeRoamRingRoad = 250f, FreeRoamInnerRoad = 120f;
        static readonly List<(Vector3 center, float radius)> freeRoamKeepClear = new List<(Vector3, float)>();

        /// <summary>True where scenery may go: off the roads and away from the stunt areas.</summary>
        static bool FreeRoamClear(Vector3 p)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            if (r < 45f) return false;                                            // spawn plaza
            if (Mathf.Abs(p.x) < 10f || Mathf.Abs(p.z) < 10f) return false;        // cross roads
            if (Mathf.Abs(r - FreeRoamRingRoad) < 11f || Mathf.Abs(r - FreeRoamInnerRoad) < 11f) return false;
            if (r > 425f) return false;                                           // the dome's barrier
            foreach (var k in freeRoamKeepClear)
                if (Vector3.Distance(new Vector3(p.x, 0f, p.z), k.center) < k.radius) return false;
            return true;
        }

        static void RingRoad(Transform parent, float radius, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0.01f, Mathf.Sin(a0) * radius), p1 = new Vector3(Mathf.Cos(a1) * radius, 0.01f, Mathf.Sin(a1) * radius);
                var seg = Prim(PrimitiveType.Cube, "RingRoad", parent, (p0 + p1) * 0.5f, new Vector3(12f, 0.02f, (p1 - p0).magnitude + 0.3f), asphalt, false);
                seg.transform.localRotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
                // Dashed centre line.
                if (i % 2 == 0)
                {
                    var dash = Prim(PrimitiveType.Cube, "Dash", parent, (p0 + p1) * 0.5f + Vector3.up * 0.01f, new Vector3(0.3f, 0.02f, (p1 - p0).magnitude * 0.6f), lineWhite, false);
                    dash.transform.localRotation = seg.transform.localRotation;
                }
            }
        }

        static Transform BuildFreeRoam()
        {
            var f = Group("FreeRoam", null, FreeRoamOrigin);
            var rng = new System.Random(3);
            freeRoamKeepClear.Clear();
            Prim(PrimitiveType.Cube, "Ground", f, new Vector3(0f, -0.5f, 0f), new Vector3(900f, 1f, 900f), freeGrass);

            // ---------------- roads: a cross through the middle, an inner ring and a big outer ring
            Prim(PrimitiveType.Cube, "RoadNS", f, new Vector3(0f, 0.012f, 0f), new Vector3(12f, 0.02f, 840f), asphalt, false);
            Prim(PrimitiveType.Cube, "RoadEW", f, new Vector3(0f, 0.013f, 0f), new Vector3(840f, 0.02f, 12f), asphalt, false);
            RingRoad(f, FreeRoamInnerRoad, 64);
            RingRoad(f, FreeRoamRingRoad, 120);
            Prim(PrimitiveType.Cylinder, "Plaza", f, new Vector3(0f, 0.015f, 0f), new Vector3(60f, 0.01f, 60f), plaza, false);

            // ---------------- north-east: the stunt park
            var stunt = new Vector3(170f, 0f, 170f);
            freeRoamKeepClear.Add((stunt, 95f));
            Ramp(f, "Ramp1", stunt + new Vector3(-40f, 0f, -40f), 45f, 12f, 8f, 14f, rampMat);
            Ramp(f, "Ramp2", stunt + new Vector3(-10f, 0f, -55f), 90f, 16f, 10f, 20f, rampBlue);
            Ramp(f, "Ramp3", stunt + new Vector3(-55f, 0f, -10f), 0f, 20f, 10f, 24f, rampRed);
            Ramp(f, "MegaRamp", stunt + new Vector3(0f, 0f, -20f), 45f, 30f, 14f, 28f, rampMat);
            for (int i = 0; i < 3; i++)
                BoostHoop(f, stunt + new Vector3(14f + i * 10f, 15f - i * 3f, -6f + i * 10f));
            for (int i = 0; i < 5; i++)
                Ramp(f, "Kicker" + i, stunt + new Vector3(-70f + i * 14f, 0f, 40f), 90f, 5f, 8f, 12f, i % 2 == 0 ? rampBlue : rampRed);
            var bowl = stunt + new Vector3(45f, 0f, -45f);
            QuarterPipe(f, bowl + new Vector3(0f, 0f, 14f), 0f, 24f, 6f, rampBlue);
            QuarterPipe(f, bowl + new Vector3(0f, 0f, -14f), 180f, 24f, 6f, rampBlue);
            QuarterPipe(f, bowl + new Vector3(14f, 0f, 0f), 90f, 24f, 6f, rampRed);
            QuarterPipe(f, bowl + new Vector3(-14f, 0f, 0f), -90f, 24f, 6f, rampRed);
            Board(f, "StuntSign", stunt + new Vector3(-75f, 4f, -75f), Quaternion.Euler(0f, 45f, 0f), new Vector2(10f, 3f), 3f, "<b>STUNT PARK</b>", glowFree);

            // ---------------- south-east: the jump strip and a plateau with a drop-off
            var jumps = new Vector3(170f, 0f, -170f);
            freeRoamKeepClear.Add((jumps, 90f));
            for (int i = 0; i < 6; i++)
                Ramp(f, "JumpStrip" + i, jumps + new Vector3(-60f + i * 24f, 0f, 0f), 90f, 6f + i * 1.5f, 10f, 12f + i * 2.5f, i % 2 == 0 ? rampMat : rampRed);
            for (int i = 0; i < 4; i++)
            {
                var pad = Prim(PrimitiveType.Cube, "BoostPad" + i, f, jumps + new Vector3(-85f + i * 6f, 0.03f, 0f), new Vector3(3f, 0.05f, 5f), boost, true, Quaternion.Euler(0f, 90f, 0f));
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
                Chevrons(pad.transform);
            }
            Prim(PrimitiveType.Cube, "Plateau", f, jumps + new Vector3(0f, 5f, -50f), new Vector3(40f, 10f, 30f), stone);
            Ramp(f, "PlateauRamp", jumps + new Vector3(-31f, 0f, -50f), 90f, 24f, 10f, 24f, rampMat);
            Ramp(f, "PlateauKicker", jumps + new Vector3(18f, 10f, -50f), 90f, 4f, 8f, 15f, rampRed);
            Board(f, "JumpSign", jumps + new Vector3(-90f, 4f, 12f), Quaternion.Euler(0f, 90f, 0f), new Vector2(10f, 3f), 3f, "<b>JUMP STRIP</b>", boost);

            // ---------------- north-west: hills and a lake with an island
            var lake = new Vector3(-170f, 0f, 170f);
            freeRoamKeepClear.Add((lake, 48f));
            Prim(PrimitiveType.Cylinder, "LakeShore", f, lake + new Vector3(0f, 0.01f, 0f), new Vector3(96f, 0.02f, 84f), sand, false);
            Prim(PrimitiveType.Cylinder, "Lake", f, lake + new Vector3(0f, 0.04f, 0f), new Vector3(86f, 0.02f, 74f), water, false);
            Prim(PrimitiveType.Cylinder, "Island", f, lake + new Vector3(8f, 0.5f, 6f), new Vector3(16f, 0.5f, 14f), sand);
            Tree(f, lake + new Vector3(8f, 1f, 6f), 1.1f, rng);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f * Mathf.Deg2Rad;
                var p = lake + new Vector3(Mathf.Sin(a) * 95f, -14f, Mathf.Cos(a) * 95f);
                if (!FreeRoamClear(new Vector3(p.x, 0f, p.z))) continue;
                Prim(PrimitiveType.Sphere, "Hill", f, p, Vector3.one * (36f + (float)rng.NextDouble() * 24f), freeGrass);
            }

            // ---------------- south-west: the forest, with a clearing and a dirt loop
            var forest = new Vector3(-180f, 0f, -170f);
            Prim(PrimitiveType.Cylinder, "Clearing", f, forest + new Vector3(0f, 0.012f, 0f), new Vector3(50f, 0.01f, 50f), sand, false);
            freeRoamKeepClear.Add((forest, 26f));
            for (int i = 0; i < 160; i++)
            {
                var p = forest + new Vector3((float)rng.NextDouble() * 200f - 100f, 0f, (float)rng.NextDouble() * 200f - 100f);
                if (!FreeRoamClear(p)) continue;
                Tree(f, p, 0.8f + (float)rng.NextDouble() * 0.8f, rng);
            }

            // ---------------- the rest: scattered trees, rocks, and big hills round the edge
            for (int i = 0; i < 120; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 840f - 420f, 0f, (float)rng.NextDouble() * 840f - 420f);
                if (!FreeRoamClear(p)) continue;
                if (rng.NextDouble() < 0.25)
                {
                    float s = 1.5f + (float)rng.NextDouble() * 3f;
                    Prim(PrimitiveType.Sphere, "Rock", f, p, new Vector3(s * 1.3f, s, s), rock);
                }
                else Tree(f, p, 0.8f + (float)rng.NextDouble() * 0.7f, rng);
            }
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f + 0.1f;
                var p = new Vector3(Mathf.Sin(a) * 395f, -18f, Mathf.Cos(a) * 395f);
                if (Mathf.Abs(p.x) < 25f || Mathf.Abs(p.z) < 25f) continue; // leave the road ends open
                Prim(PrimitiveType.Sphere, "EdgeHill", f, p, new Vector3(70f, 60f, 70f) * (0.8f + (float)rng.NextDouble() * 0.5f), freeGrass);
            }

            // ---------------- spawn plaza in the middle
            var spawn = new GameObject("FreeRoamSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.FreeRoam;
            spawn.killY = -10f;
            spawn.transform.SetParent(f, false);
            for (int i = 0; i < 12; i++)
            {
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = new Vector3((i % 4 - 1.5f) * 3.5f, 0f, -(i / 4) * 4f);
            }
            Board(f, "FreeRoamSign", new Vector3(0f, 4.5f, 18f), Quaternion.identity, new Vector2(13f, 4f), 2.4f,
                "<b>FREE ROAM</b>\nStunt park: north-east   Jump strip: south-east   Lake: north-west   Forest: south-west\n" +
                "<b>BUILD MODE:</b> click the right stick (or B) to fly around and place ramps, roads, trees...\nWrist menu > Hub to go back", glowFree);
            return f;
        }
    }
}
