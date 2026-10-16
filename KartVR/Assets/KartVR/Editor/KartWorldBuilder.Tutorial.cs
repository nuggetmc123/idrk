using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Tutorial Park: where new players start. A ring road with a numbered station sign for each driving lesson
    /// (drive, steer, brake, drift, boost pad, jump), cones along the edges, and a BUILD ZONE in the middle for the
    /// building lessons. Played solo; the steps themselves are on the floating panel (Scripts/Modes/Tutorial.cs).
    /// </summary>
    public static partial class KartWorldBuilder
    {
        static readonly Vector3 TutorialOrigin = KartMaps.Get(KartMode.Tutorial).center;
        const float TutorialRing = 45f;

        static Transform BuildTutorialPark()
        {
            var t = Group("TutorialPark", null, TutorialOrigin);
            var rng = new System.Random(11);
            var cone = Mat("Cone", new Color(1f, 0.45f, 0.08f), 0.35f);
            var coneStripe = Mat("ConeStripe", Color.white, 0.4f);

            Prim(PrimitiveType.Cube, "Ground", t, new Vector3(0f, -0.5f, 0f), new Vector3(230f, 1f, 230f), freeGrass);
            RingRoad(t, TutorialRing, 48);

            // Cones on both edges of the road.
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                foreach (float r in new[] { TutorialRing - 7f, TutorialRing + 7f })
                {
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    Prim(PrimitiveType.Cylinder, "Cone", t, p + Vector3.up * 0.35f, new Vector3(0.45f, 0.35f, 0.45f), cone, false);
                    Prim(PrimitiveType.Cylinder, "ConeStripe", t, p + Vector3.up * 0.45f, new Vector3(0.47f, 0.06f, 0.47f), coneStripe, false);
                }
            }

            // Start arch where you spawn (south side of the ring, driving towards +X).
            var start = Group("StartArch", t, RingPoint(-90f), Quaternion.LookRotation(RingTangent(-90f)));
            foreach (float x in new[] { -7.5f, 7.5f })
                Prim(PrimitiveType.Cube, "ArchPost", start, new Vector3(x, 3f, 6f), new Vector3(0.6f, 6f, 0.6f), metal);
            Prim(PrimitiveType.Cube, "ArchTop", start, new Vector3(0f, 6.2f, 6f), new Vector3(15.6f, 1.2f, 0.6f), sign);
            Prim(PrimitiveType.Cube, "ArchGlow", start, new Vector3(0f, 5.55f, 5.68f), new Vector3(15.6f, 0.08f, 0.08f), neonCyan, false);
            Text(start, "ArchText", "<b>TUTORIAL PARK</b>", new Vector3(0f, 6.2f, 5.68f), Quaternion.identity, 5f, Color.white, new Vector2(15f, 1.1f));

            // Numbered stations around the ring (angles count anticlockwise, the way you drive).
            Station(t, -60f, "1  DRIVE", "Hold the RIGHT TRIGGER");
            Station(t, -25f, "2  STEER", "Grab the wheel (grips)\nor use the LEFT STICK");
            Station(t, 10f, "3  BRAKE + REVERSE", "LEFT TRIGGER\n(keep holding to back up)");
            Station(t, 45f, "4  DRIFT", "Hold X while you turn");
            Station(t, 90f, "5  BOOST", "Drive over the pad, press A");
            Station(t, 140f, "6  JUMP!", "Hit the ramp fast");
            Station(t, 190f, "7  RESPAWN", "Stuck? Press B");
            Station(t, 230f, "8  BUILD", "Click the RIGHT STICK\nand fly to the BUILD ZONE");

            // A boost pad and a jump on the road.
            for (int i = 0; i < 2; i++)
            {
                float a = 100f + i * 6f;
                var pad = Prim(PrimitiveType.Cube, "BoostPad", t, RingPoint(a) + Vector3.up * 0.03f, new Vector3(4f, 0.05f, 3f), boost);
                pad.transform.localRotation = Quaternion.LookRotation(RingTangent(a));
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
                Chevrons(pad.transform);
            }
            Ramp(t, "Jump", RingPoint(150f), Quaternion.LookRotation(RingTangent(150f)).eulerAngles.y, 10f, 9f, 9f, rampMat);

            // The build zone in the middle: a flat concrete pad with a sign and a few starter pieces of scenery.
            Prim(PrimitiveType.Cylinder, "BuildZone", t, new Vector3(0f, 0.02f, 0f), new Vector3(52f, 0.02f, 52f), plaza, false);
            Prim(PrimitiveType.Cylinder, "BuildZoneEdge", t, new Vector3(0f, 0.015f, 0f), new Vector3(53.5f, 0.02f, 53.5f), neonCyan, false);
            Board(t, "BuildZoneSign", new Vector3(0f, 4f, 8f), Quaternion.identity, new Vector2(10f, 3.2f), 2.6f,
                "<b>BUILD ZONE</b>\n<size=60%>Click the RIGHT STICK to fly. A / X pick a piece,\nRIGHT TRIGGER places it, LEFT TRIGGER deletes</size>", neonPink);

            // Trees outside the ring.
            for (int i = 0; i < 40; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = 62f + (float)rng.NextDouble() * 35f;
                Tree(t, new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), 0.9f + (float)rng.NextDouble() * 0.6f, rng);
            }

            // Spawn on the road at the start arch, facing the way you drive.
            var spawn = new GameObject("TutorialSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Tutorial;
            spawn.killY = -10f;
            spawn.transform.SetParent(t, false);
            for (int i = 0; i < 4; i++)
            {
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                Vector3 across = new Vector3(0f, 0f, -1f); // outwards at the south side of the ring
                slot.localPosition = RingPoint(-90f) + across * ((i % 2 - 0.5f) * 4f) - RingTangent(-90f) * (i / 2) * 5f;
                slot.localRotation = Quaternion.LookRotation(RingTangent(-90f));
            }
            return t;
        }

        static Vector3 RingPoint(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * TutorialRing, 0f, Mathf.Sin(a) * TutorialRing);
        }

        static Vector3 RingTangent(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        /// <summary>A numbered sign just outside the road, angled to face drivers coming towards it.</summary>
        static void Station(Transform parent, float degrees, string title, string hint)
        {
            float a = degrees * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 pos = outward * (TutorialRing + 11f) + Vector3.up * 3.2f;
            // Text is on the -Z face: point +Z away from the drivers (outwards, turned a little towards the oncoming traffic).
            Vector3 facing = (outward + RingTangent(degrees) * 0.6f).normalized;
            Board(parent, "Station " + title, pos, Quaternion.LookRotation(facing), new Vector2(8f, 3.2f), 2.6f,
                "<b>" + title + "</b>\n<size=65%>" + hint + "</size>", neonCyan);
        }
    }
}
