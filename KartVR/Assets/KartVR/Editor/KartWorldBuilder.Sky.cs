using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Sky Zones: a floating island high above every map, reached by a glowing
    /// beam (<see cref="SkyLift"/>). Jump off the edge to come back down.
    /// Hub = sky garden, Race = a rainbow "sky route" round the first bend,
    /// Bumper = a second arena in the sky, Free Roam = a sky stunt park.
    /// </summary>
    public static partial class KartWorldBuilder
    {
        // Rainbow order in PlayerProfile.Palette: red, orange, yellow, green, blue, purple.
        static readonly int[] RainbowIndices = { 0, 5, 3, 2, 1, 4 };

        // ------------------------------------------------------------------ shared pieces

        /// <summary>The beam that carries you up. topHeight is measured from pos (ground level).</summary>
        static SkyLift SkyBeam(Transform parent, Vector3 pos, float topHeight, Vector3 exitDirection)
        {
            var g = Group("SkyLift", parent, pos);
            var lift = g.gameObject.AddComponent<SkyLift>();
            lift.topHeight = topHeight;
            lift.exitDirection = exitDirection.normalized;
            float r = lift.radius;
            float h = topHeight + 3f;

            Prim(PrimitiveType.Cylinder, "Beam", g, new Vector3(0f, h * 0.5f, 0f), new Vector3(r * 2f, h * 0.5f, r * 2f), skyBeam, false);
            Prim(PrimitiveType.Cylinder, "Pad", g, new Vector3(0f, 0.03f, 0f), new Vector3(r * 2f + 0.8f, 0.02f, r * 2f + 0.8f), neonCyan, false);
            Prim(PrimitiveType.Cylinder, "PadInner", g, new Vector3(0f, 0.04f, 0f), new Vector3(r * 2f, 0.02f, r * 2f), neonWhite, false);
            for (int i = 0; i < 4; i++)
            {
                var ring = Hoop(g, "Ring" + i, new Vector3(0f, 2f + i * (topHeight - 2f) / 3f, 0f), Quaternion.Euler(90f, 0f, 0f), r + 0.25f, 0.18f,
                    i % 2 == 0 ? neonCyan : neonWhite, 16);
                var spin = ring.gameObject.AddComponent<Rotator>();
                spin.axis = Vector3.forward;
                spin.degreesPerSecond = i % 2 == 0 ? 60f : -60f;
            }

            // A spinning sign so you can read it from any side.
            var sign = Group("Sign", g, new Vector3(0f, 4.2f, 0f));
            sign.gameObject.AddComponent<Rotator>().degreesPerSecond = 30f;
            Text(sign, "Front", "SKY ZONE", new Vector3(0f, 0f, -(r + 0.3f)), Quaternion.identity, 5f, new Color(0.7f, 0.95f, 1f), new Vector2(8f, 1.6f));
            Text(sign, "Back", "SKY ZONE", new Vector3(0f, 0f, r + 0.3f), Quaternion.Euler(0f, 180f, 0f), 5f, new Color(0.7f, 0.95f, 1f), new Vector2(8f, 1.6f));
            return lift;
        }

        /// <summary>A flat round collider-backed slab whose top is at pos.y.</summary>
        static GameObject Disc(Transform parent, string name, Vector3 topCenter, float radius, Material mat)
        {
            var d = Prim(PrimitiveType.Cylinder, name, parent, topCenter + Vector3.down * 0.5f, new Vector3(radius * 2f, 0.5f, radius * 2f), mat);
            // Cylinder primitives come with a capsule collider; use the real shape instead.
            Object.DestroyImmediate(d.GetComponent<Collider>());
            d.AddComponent<MeshCollider>().sharedMesh = d.GetComponent<MeshFilter>().sharedMesh;
            return d;
        }

        /// <summary>
        /// A floating island: deck on top, rocky underside, glowing rim, clouds around it.
        /// Returns a group sitting on the deck's surface for placing things on it.
        /// </summary>
        static Transform SkyIsland(Transform parent, string name, Vector3 topCenter, float radius, Material deck, System.Random rng)
        {
            var island = Group(name, parent, topCenter);
            Disc(island, "Deck", Vector3.zero, radius, deck);
            Prim(PrimitiveType.Cylinder, "Edge", island, new Vector3(0f, -0.55f, 0f), new Vector3(radius * 2f + 0.3f, 0.45f, radius * 2f + 0.3f), stone, false);
            Prim(PrimitiveType.Sphere, "Underside", island, new Vector3(0f, -1f - radius * 0.38f, 0f), new Vector3(radius * 1.95f, radius * 0.85f, radius * 1.95f), rock, false);
            Prim(PrimitiveType.Sphere, "Root", island, new Vector3(0f, -1f - radius * 0.85f, 0f), new Vector3(radius * 0.9f, radius * 0.9f, radius * 0.9f), bark, false);

            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.25f, radius - 0.25f);
                Prim(PrimitiveType.Cube, "RimLight", island, p, new Vector3(0.35f, 0.5f, 0.35f), i % 2 == 0 ? neonCyan : neonPink, false, Quaternion.Euler(0f, a, 0f));
            }
            for (int i = 0; i < 6; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = radius * (0.9f + (float)rng.NextDouble() * 0.5f);
                Cloud(island, new Vector3(Mathf.Sin(a) * d, -3f - (float)rng.NextDouble() * 6f, Mathf.Cos(a) * d), 2.5f + (float)rng.NextDouble() * 2.5f, rng);
            }
            return island;
        }

        /// <summary>A puffy cloud made of squashed spheres (no collider).</summary>
        static void Cloud(Transform parent, Vector3 pos, float size, System.Random rng)
        {
            var c = Group("Cloud", parent, pos);
            for (int i = 0; i < 5; i++)
            {
                var off = new Vector3(((float)rng.NextDouble() - 0.5f) * size * 2f, ((float)rng.NextDouble() - 0.5f) * size * 0.4f, ((float)rng.NextDouble() - 0.5f) * size);
                float s = size * (0.8f + (float)rng.NextDouble() * 0.8f);
                Prim(PrimitiveType.Sphere, "Puff", c, off, new Vector3(s * 1.4f, s * 0.7f, s), cloud, false);
            }
        }

        /// <summary>A rainbow arch standing on the ground at pos, in the local XY plane (drive through along Z).</summary>
        static void Rainbow(Transform parent, Vector3 pos, Quaternion rot, float radius)
        {
            var g = Group("Rainbow", parent, pos, rot);
            for (int band = 0; band < RainbowIndices.Length; band++)
            {
                float r = radius - band * 0.45f;
                const int segments = 18;
                float seg = Mathf.PI * r / segments * 1.1f;
                for (int i = 0; i < segments; i++)
                {
                    float a = -90f + (i + 0.5f) * 180f / segments;
                    var p = Quaternion.Euler(0f, 0f, a) * new Vector3(0f, r, 0f);
                    Prim(PrimitiveType.Cube, "Band", g, p, new Vector3(seg, 0.45f, 0.3f), crowd[RainbowIndices[band]], false, Quaternion.Euler(0f, 0f, a));
                }
            }
        }

        static void SkyBoostPad(Transform parent, Vector3 pos, float yaw)
        {
            var pad = Prim(PrimitiveType.Cube, "BoostPad", parent, pos + Vector3.up * 0.03f, new Vector3(4f, 0.05f, 3f), boost, true, Quaternion.Euler(0f, yaw, 0f));
            pad.GetComponent<Collider>().isTrigger = true;
            pad.AddComponent<BoostPad>();
            Chevrons(pad.transform);
        }

        // ------------------------------------------------------------------ hub: sky garden

        static void BuildHubSky(Transform hub)
        {
            var rng = new System.Random(11);
            // Beam just south of the spawn; the garden floats beyond it.
            SkyBeam(hub, new Vector3(0f, 0f, -29f), 43f, Vector3.back);
            var top = SkyIsland(hub, "SkyGarden", new Vector3(0f, 40f, -48f), 16f, hubGrass, rng);

            BuildFountain(top, new Vector3(0f, 0f, -2f));
            foreach (var p in new[] { new Vector3(-9f, 0f, -6f), new Vector3(9f, 0f, -6f), new Vector3(-6f, 0f, -11f), new Vector3(6f, 0f, -11f), new Vector3(-11f, 0f, 4f) })
                Tree(top, p, 0.7f, rng);
            Bench(top, new Vector3(-6f, 0f, 3f), 135f);
            Bench(top, new Vector3(6f, 0f, 3f), 225f);
            Bench(top, new Vector3(-8f, 0f, -1f), 90f);
            Bench(top, new Vector3(8f, 0f, -1f), 270f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Lamp(top, new Vector3(Mathf.Sin(a) * 12.5f, 0f, Mathf.Cos(a) * 12.5f));
            }
            Rainbow(top, new Vector3(0f, 0f, 8f), Quaternion.identity, 5f);
            Board(top, "SkySign", new Vector3(0f, 3f, -12.5f), Quaternion.Euler(0f, 180f, 0f), new Vector2(9f, 2.6f), 2.4f,
                "<b>SKY GARDEN</b>\nJump off the edge to go back down!", neonCyan);

            for (int i = 0; i < 10; i++)
                Cloud(hub, new Vector3((float)rng.NextDouble() * 200f - 100f, 30f + (float)rng.NextDouble() * 25f, (float)rng.NextDouble() * 200f - 100f),
                    4f + (float)rng.NextDouble() * 4f, rng);
        }

        // ------------------------------------------------------------------ race: rainbow sky route

        // The route leaves the track on the outside of the first bend after the start straight's second
        // checkpoint, flies round in the sky (with its own copy of checkpoint 3) and slopes back onto the track.
        const float SkyLiftT = 0.10f, SkyRouteStart = 0.14f, SkyRouteEnd = 1.15f;
        const float SkyRouteHeight = 18f, SkyRouteWidth = 9f;
        static readonly List<Vector3> skyRouteSamples = new List<Vector3>();

        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        /// <summary>Route centre's distance from the track centre line: outside the walls, then back onto the track.</summary>
        static float RouteOffset(float t) => Mathf.Lerp(14.5f, 2.5f, Smooth(0.75f, SkyRouteEnd, t));
        static float RouteHeight(float t) => Mathf.Lerp(SkyRouteHeight, 2.6f, Smooth(0.55f, SkyRouteEnd, t));

        /// <summary>Horizontal direction pointing away from the infield at track position t.</summary>
        static Vector3 TrackOutward(float t)
        {
            Vector3 n = Vector3.Cross(Vector3.up, TrackTangent(t));
            Vector3 p = TrackPoint(t) - RaceOrigin;
            if (Vector3.Dot(n, new Vector3(p.x, 0f, p.z)) < 0f) n = -n;
            return n;
        }

        /// <summary>Point on the route's surface (race-local coordinates).</summary>
        static Vector3 RoutePoint(float t) => TrackPoint(t) - RaceOrigin + TrackOutward(t) * RouteOffset(t) + Vector3.up * RouteHeight(t);

        static bool NearSkyRoute(Vector3 raceLocal)
        {
            foreach (var s in skyRouteSamples)
                if (new Vector2(raceLocal.x - s.x, raceLocal.z - s.z).sqrMagnitude < 10f * 10f) return true;
            return false;
        }

        static void BuildRaceSky(Transform race)
        {
            var rng = new System.Random(12);
            skyRouteSamples.Clear();

            // Beam over the outer lane: optional, drive through it to take the sky route.
            Vector3 liftTrack = TrackPoint(SkyLiftT) - RaceOrigin;
            Vector3 outward = TrackOutward(SkyLiftT);
            Vector3 tangent = TrackTangent(SkyLiftT);
            var lift = SkyBeam(race, liftTrack + outward * 5f, SkyRouteHeight + 4f, outward);
            skyRouteSamples.Add(lift.transform.localPosition);
            Text(race, "SkyRouteSign", "SKY ROUTE\n<size=60%>outer lane</size>", liftTrack + outward * 5f + Vector3.up * 7.5f - tangent * 6f,
                Quaternion.LookRotation(tangent, Vector3.up), 4f, new Color(0.7f, 0.95f, 1f), new Vector2(10f, 3f));

            // Landing pad beside the beam.
            var padRot = Quaternion.LookRotation(tangent, Vector3.up);
            var pad = Group("SkyLandingPad", race, liftTrack + outward * 14.5f + Vector3.up * SkyRouteHeight, padRot);
            Prim(PrimitiveType.Cube, "Deck", pad, new Vector3(0f, -0.2f, 0f), new Vector3(12f, 0.4f, 12f), crowd[RainbowIndices[4]]);
            float side = Vector3.Dot(outward, padRot * Vector3.right) > 0f ? 1f : -1f;
            Prim(PrimitiveType.Cube, "RailBack", pad, new Vector3(0f, 0.3f, -6f), new Vector3(12f, 0.6f, 0.3f), neonPink);
            Prim(PrimitiveType.Cube, "RailOuter", pad, new Vector3(side * 6f, 0.3f, 0f), new Vector3(0.3f, 0.6f, 12f), neonCyan);
            Prim(PrimitiveType.Cube, "RailInner", pad, new Vector3(-side * 6f, 0.3f, 0f), new Vector3(0.3f, 0.6f, 12f), neonCyan);
            Prim(PrimitiveType.Cube, "Underside", pad, new Vector3(0f, -0.9f, 0f), new Vector3(11f, 1f, 11f), rock, false);
            skyRouteSamples.Add(pad.localPosition);

            // The rainbow road itself.
            var road = Group("SkyRoute", race, Vector3.zero);
            const int segments = 70;
            for (int i = 0; i < segments; i++)
            {
                float t0 = Mathf.Lerp(SkyRouteStart, SkyRouteEnd, i / (float)segments);
                float t1 = Mathf.Lerp(SkyRouteStart, SkyRouteEnd, (i + 1) / (float)segments);
                Vector3 a = RoutePoint(t0), b = RoutePoint(t1);
                Vector3 dir = b - a;
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                Vector3 up = rot * Vector3.up;
                Vector3 mid = (a + b) * 0.5f;
                var seg = Prim(PrimitiveType.Cube, "Road" + i, road, Vector3.zero, new Vector3(SkyRouteWidth, 0.4f, dir.magnitude + 0.35f),
                    crowd[RainbowIndices[(i / 2) % RainbowIndices.Length]]);
                seg.transform.SetPositionAndRotation(mid - up * 0.2f, rot);
                foreach (float s in new[] { -1f, 1f })
                {
                    var rail = Prim(PrimitiveType.Cube, "Rail", road, Vector3.zero, new Vector3(0.3f, 0.6f, dir.magnitude + 0.35f), i % 2 == 0 ? neonCyan : neonPink);
                    rail.transform.SetPositionAndRotation(mid + rot * new Vector3(s * (SkyRouteWidth * 0.5f - 0.15f), 0.3f, 0f), rot);
                }
                skyRouteSamples.Add(mid);

                // Pillars only where the road is fully outside the track walls (never on the track).
                float h = RouteHeight(t0);
                if (i % 5 == 0 && RouteOffset(t0) > 13.5f && h > 3f)
                    Prim(PrimitiveType.Cube, "Pillar", road, a + Vector3.down * (h * 0.5f + 0.2f), new Vector3(0.9f, h - 0.4f, 0.9f), metal);
                if (i % 9 == 4) Cloud(road, mid + TrackOutward(t0) * 9f + Vector3.down * 3f, 3f, rng);
            }

            // The route skips the track's checkpoint 3, so give that checkpoint a second gate up on the route.
            var cp3 = race.Find("Checkpoint3");
            if (cp3 != null)
            {
                const float tCp = Mathf.PI * 0.25f; // checkpoint 3 sits at t = -pi/2 + 3 * pi/4
                var gate = cp3.gameObject.AddComponent<BoxCollider>();
                gate.isTrigger = true;
                gate.center = cp3.InverseTransformPoint(RoutePoint(tCp) + RaceOrigin) + Vector3.up * 2.5f;
                gate.size = new Vector3(SkyRouteWidth + 2f, 5f, 2.5f);
                Arch(Group("SkyGate", race, RoutePoint(tCp), Quaternion.LookRotation(TrackTangent(tCp), Vector3.up)), SkyRouteWidth * 0.5f, "SKY", neonCyan);
            }
        }

        // ------------------------------------------------------------------ bumper: sky arena

        static void BuildBumperSky(Transform b)
        {
            var rng = new System.Random(13);
            // Beam at the platform's edge between two spawn slots; the sky arena floats over the middle.
            var liftPos = Quaternion.Euler(0f, 15f, 0f) * new Vector3(0f, 0f, 19.5f);
            SkyBeam(b, liftPos, 31f, -liftPos);
            var top = SkyIsland(b, "SkyArena", new Vector3(0f, 28f, 0f), 13f, arena, rng);

            Prim(PrimitiveType.Cylinder, "Ring", top, new Vector3(0f, 0.01f, 0f), new Vector3(18f, 0.01f, 18f), neonCyan, false);
            Prim(PrimitiveType.Cylinder, "Center", top, new Vector3(0f, 0.02f, 0f), new Vector3(17.4f, 0.01f, 17.4f), arena, false);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var bumper = Prim(PrimitiveType.Cylinder, "SkyBouncer" + i, top, new Vector3(Mathf.Sin(a) * 6f, 0.6f, Mathf.Cos(a) * 6f), new Vector3(2.2f, 0.6f, 2.2f), wallRed);
                bumper.AddComponent<Bouncer>();
                Prim(PrimitiveType.Cylinder, "Glow", bumper.transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.8f, 0.05f, 0.8f), neonWhite, false);
                Prim(PrimitiveType.Cylinder, "Band", bumper.transform, Vector3.zero, new Vector3(1.04f, 0.25f, 1.04f), wallWhite, false);
            }
            Text(top, "SkyTitle", "SKY ARENA", new Vector3(0f, 5f, 0f), Quaternion.identity, 6f, new Color(0.7f, 0.95f, 1f), new Vector2(12f, 2f))
                .gameObject.AddComponent<Rotator>().degreesPerSecond = 20f;
        }

        // ------------------------------------------------------------------ free roam: sky stunt park

        static void BuildFreeRoamSky(Transform f)
        {
            var rng = new System.Random(14);
            SkyBeam(f, new Vector3(-30f, 0f, -7f), 48f, Vector3.back);
            var top = SkyIsland(f, "SkyPark", new Vector3(-30f, 45f, -30f), 20f, plaza, rng);

            // Run-up with boost pads, under a rainbow, into a big jump off the edge.
            SkyBoostPad(top, new Vector3(0f, 0f, -1f), 180f);
            SkyBoostPad(top, new Vector3(0f, 0f, -7f), 180f);
            Rainbow(top, new Vector3(0f, 0f, -4f), Quaternion.identity, 6f);
            Ramp(top, "SkyJump", new Vector3(0f, 0f, -15f), 180f, 9f, 8f, 18f, rampMat);
            Board(top, "SkyParkSign", new Vector3(-13f, 3.2f, 8f), Quaternion.Euler(0f, 180f, 0f), new Vector2(8f, 2.6f), 2.4f,
                "<b>SKY STUNT PARK</b>\nBoost off the SKY JUMP - or drive off any edge!", glowFree);

            // Side kickers and a quarter pipe to play on.
            Ramp(top, "SkyKicker1", new Vector3(-11f, 0f, 2f), 90f, 5f, 6f, 13f, rampRed);
            Ramp(top, "SkyKicker2", new Vector3(11f, 0f, 2f), -90f, 5f, 6f, 13f, rampBlue);
            QuarterPipe(top, new Vector3(-9f, 0f, -11f), -120f, 8f, 4f, rampBlue);
            QuarterPipe(top, new Vector3(9f, 0f, -11f), 120f, 8f, 4f, rampRed);

            // Boost hoops to fly through after the jump.
            foreach (var p in new[] { new Vector3(-30f, 50.5f, -62f), new Vector3(-30f, 50.5f, -74f), new Vector3(-30f, 48f, -86f) })
                BoostHoop(f, p);

            for (int i = 0; i < 12; i++)
                Cloud(f, new Vector3((float)rng.NextDouble() * 260f - 130f, 28f + (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 260f - 130f),
                    4f + (float)rng.NextDouble() * 5f, rng);
        }
    }
}
