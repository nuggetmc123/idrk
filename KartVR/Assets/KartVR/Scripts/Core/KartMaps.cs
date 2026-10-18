using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Where every map is. Each map sits inside its own sky dome; the dome's edge
    /// is also an invisible barrier: drive or walk past it and you're put back.
    /// Maps are far enough apart that no dome contains another map.
    /// </summary>
    public static class KartMaps
    {
        public struct Map
        {
            public KartMode mode;
            public string root;      // name of the map's root object in the scene
            public Vector3 center;
            public float radius;     // sky dome radius (the barrier is a little inside it)
            public SkyDomes.Theme theme;
        }

        public static readonly Map[] All =
        {
            new Map { mode = KartMode.Hub, root = "Hub", center = Vector3.zero, radius = 60f, theme = SkyDomes.Theme.Day },
            new Map { mode = KartMode.Race, root = "RaceTrack", center = new Vector3(0f, 0f, 400f), radius = 205f, theme = SkyDomes.Theme.Sunny },
            new Map { mode = KartMode.Bumper, root = "BumperArena", center = new Vector3(320f, 0f, 0f), radius = 185f, theme = SkyDomes.Theme.Night },
            new Map { mode = KartMode.CanyonRace, root = "CanyonTrack", center = new Vector3(0f, 0f, -600f), radius = 245f, theme = SkyDomes.Theme.Desert },
            new Map { mode = KartMode.IceBumper, root = "IceRink", center = new Vector3(-360f, 0f, 0f), radius = 150f, theme = SkyDomes.Theme.Ice },
            new Map { mode = KartMode.FreeRoam, root = "FreeRoam", center = new Vector3(0f, 0f, 1300f), radius = 450f, theme = SkyDomes.Theme.Sunset },
            new Map { mode = KartMode.Tag, root = "TagArena", center = new Vector3(700f, 0f, -700f), radius = 140f, theme = SkyDomes.Theme.Night },
            new Map { mode = KartMode.Tutorial, root = "TutorialPark", center = new Vector3(-480f, 0f, -480f), radius = 110f, theme = SkyDomes.Theme.Sunny },
        };

        /// <summary>The barrier sits this far inside the dome.</summary>
        public const float BarrierMargin = 4f;

        public static Map Get(KartMode mode)
        {
            foreach (var m in All)
                if (m.mode == mode) return m;
            return All[0];
        }

        /// <summary>True if a point is past the map's invisible barrier (too far out or too high).</summary>
        public static bool OutOfBounds(KartMode mode, Vector3 p)
        {
            var m = Get(mode);
            Vector3 d = p - m.center;
            float limit = m.radius - BarrierMargin;
            return new Vector2(d.x, d.z).sqrMagnitude > limit * limit || d.y > limit * 0.75f;
        }

        /// <summary>Pull a point back inside the barrier (with extra margin). Used by build-mode flying.</summary>
        public static Vector3 ClampInside(KartMode mode, Vector3 p, float margin)
        {
            var m = Get(mode);
            float limit = m.radius - BarrierMargin - margin;
            Vector3 d = p - m.center;
            var flat = new Vector2(d.x, d.z);
            if (flat.magnitude > limit) flat = flat.normalized * limit;
            float y = Mathf.Clamp(d.y, 0.5f, limit * 0.75f - margin);
            return m.center + new Vector3(flat.x, y, flat.y);
        }
    }
}
