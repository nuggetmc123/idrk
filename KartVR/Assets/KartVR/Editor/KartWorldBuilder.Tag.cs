using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Tag Arena: a big round neon arena at night with things to dodge round and hide behind: pillars, broken
    /// walls, a raised centre platform with ramps, bouncy bumpers and boost pads.
    /// </summary>
    public static partial class KartWorldBuilder
    {
        static readonly Vector3 TagOrigin = KartMaps.Get(KartMode.Tag).center;

        static Transform BuildTagArena()
        {
            var t = Group("TagArena", null, TagOrigin);
            var floorMat = Mat("TagFloor", new Color(0.12f, 0.13f, 0.18f), 0.6f, 0.1f);
            var block = Mat("TagBlock", new Color(0.22f, 0.24f, 0.32f), 0.4f, 0.2f);
            var ramp = rampBlue;

            // Floor (a cylinder top at y = 0) on a big dark ground.
            Prim(PrimitiveType.Cube, "Ground", t, new Vector3(0f, -0.6f, 0f), new Vector3(300f, 1f, 300f), dark);
            var floor = Prim(PrimitiveType.Cylinder, "Floor", t, new Vector3(0f, -0.5f, 0f), new Vector3(230f, 0.5f, 230f), floorMat);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            // Grid lines.
            for (int i = -5; i <= 5; i++)
            {
                Prim(PrimitiveType.Cube, "GridX", t, new Vector3(i * 20f, 0.01f, 0f), new Vector3(0.15f, 0.01f, 210f), i % 2 == 0 ? neonCyan : neonPink, false);
                Prim(PrimitiveType.Cube, "GridZ", t, new Vector3(0f, 0.012f, i * 20f), new Vector3(210f, 0.01f, 0.15f), i % 2 == 0 ? neonCyan : neonPink, false);
            }

            // A low glowing wall round the edge.
            const int rimSegments = 72;
            for (int i = 0; i < rimSegments; i++)
            {
                float a = i * 360f / rimSegments;
                var rot = Quaternion.Euler(0f, a, 0f);
                Prim(PrimitiveType.Cube, "Rim", t, rot * new Vector3(0f, 0.7f, 113f), new Vector3(10.2f, 1.4f, 1f), block, true, rot);
                Prim(PrimitiveType.Cube, "RimGlow", t, rot * new Vector3(0f, 1.45f, 113f), new Vector3(10.2f, 0.12f, 1.05f), i % 2 == 0 ? neonPink : neonCyan, false, rot);
            }

            // Raised centre platform with four ramps up to it.
            Prim(PrimitiveType.Cylinder, "Platform", t, new Vector3(0f, 1.2f, 0f), new Vector3(22f, 1.2f, 22f), block);
            Prim(PrimitiveType.Cylinder, "PlatformGlow", t, new Vector3(0f, 2.42f, 0f), new Vector3(22.4f, 0.02f, 22.4f), neonCyan, false);
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                Ramp(t, "PlatformRamp" + i, Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -18f), yaw, 14f, 7f, 12f, ramp); // tops out level with the platform
            }

            // Pillars in two rings.
            for (int i = 0; i < 16; i++)
            {
                float a = (i * 360f / 16f + (i % 2) * 11f) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? 42f : 74f;
                var p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                Prim(PrimitiveType.Cylinder, "Pillar", t, p + Vector3.up * 3f, new Vector3(3f, 3f, 3f), block);
                Prim(PrimitiveType.Cylinder, "PillarGlow", t, p + Vector3.up * 6.05f, new Vector3(3.2f, 0.06f, 3.2f), i % 4 == 0 ? neonPink : neonCyan, false);
            }

            // Broken walls to hide behind (with gaps to sneak through).
            for (int i = 0; i < 8; i++)
            {
                float yaw = i * 45f + 22.5f;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Prim(PrimitiveType.Cube, "CoverWall", t, rot * new Vector3(0f, 1.25f, 58f), new Vector3(16f, 2.5f, 1.2f), block, true, rot);
                Prim(PrimitiveType.Cube, "CoverGlow", t, rot * new Vector3(0f, 2.55f, 58f), new Vector3(16f, 0.1f, 1.25f), neonPink, false, rot);
            }

            // Bouncy bumpers and boost pads.
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                var b = Group("Bumper" + i, t, new Vector3(Mathf.Sin(a) * 92f, 0f, Mathf.Cos(a) * 92f));
                b.gameObject.AddComponent<Bouncer>();
                Prim(PrimitiveType.Cylinder, "BumperBody", b, new Vector3(0f, 0.8f, 0f), new Vector3(3.5f, 0.8f, 3.5f), hazard);
                Prim(PrimitiveType.Cylinder, "BumperTop", b, new Vector3(0f, 1.65f, 0f), new Vector3(3.6f, 0.06f, 3.6f), neonCyan, false);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var rot = Quaternion.Euler(0f, a + 90f, 0f);
                var pad = Prim(PrimitiveType.Cube, "BoostPad", t, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.03f, 30f), new Vector3(4f, 0.05f, 3f), boost);
                pad.transform.localRotation = rot;
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
                Chevrons(pad.transform);
            }

            // Spawn ring facing the middle.
            var spawn = new GameObject("TagSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Tag;
            spawn.killY = -8f;
            spawn.transform.SetParent(t, false);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 84f, 0f, Mathf.Cos(a) * 84f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(-pos, Vector3.up);
            }

            // Score board high over the edge.
            var board = Board(t, "TagBoard", new Vector3(0f, 14f, 118f), Quaternion.identity, new Vector2(16f, 10f), 4f, "TAG", neonPink);
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Tag;
            info.text = board;
            info.title = "TAG";
            Prim(PrimitiveType.Cube, "BoardPillar", t, new Vector3(0f, 2f, 118.3f), new Vector3(1f, 14f, 1f), metal);
            return t;
        }
    }
}
