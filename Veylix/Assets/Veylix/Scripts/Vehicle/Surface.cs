using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    public enum SurfaceType { Asphalt, Kerb, SausageKerb, PaintedRunoff, Concrete, Grass, Gravel, Sand, Dirt, Snow, Ice, Metal }

    /// <summary>How a surface treats a tyre. Tuned so leaving the track costs time and control, never at random.</summary>
    public struct SurfaceInfo
    {
        public float grip, wetGrip, slide, rolling, drag, bump, bumpFreq, rumble;
        public bool dusty, sinks;
        public Color dust;
    }

    /// <summary>Tag a collider with what it's made of. Untagged colliders count as asphalt.</summary>
    public class Surface : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Asphalt;

        static readonly Dictionary<Collider, SurfaceType> cache = new Dictionary<Collider, SurfaceType>();

        public static SurfaceType Of(Collider c)
        {
            if (c == null) return SurfaceType.Asphalt;
            if (c is TerrainCollider) return TerrainType;
            if (cache.TryGetValue(c, out var t)) return t;
            var s = c.GetComponent<Surface>();
            t = s != null ? s.type : SurfaceType.Asphalt;
            cache[c] = t;
            return t;
        }

        public static void ClearCache() => cache.Clear();

        /// <summary>Terrain colliders: the map decides what the ground is (grass, sand, snow...).</summary>
        /// <remarks>Set by the map builder before the cars are placed.</remarks>
        public static SurfaceType TerrainType = SurfaceType.Grass;

        public static SurfaceInfo Info(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Kerb: return new SurfaceInfo { grip = 0.94f, wetGrip = 0.55f, slide = 0.78f, rolling = 0.012f, bump = 0.006f, bumpFreq = 22f, rumble = 0.6f };
                case SurfaceType.SausageKerb: return new SurfaceInfo { grip = 0.8f, wetGrip = 0.5f, slide = 0.7f, rolling = 0.02f, bump = 0.012f, bumpFreq = 9f, rumble = 1f };
                case SurfaceType.PaintedRunoff: return new SurfaceInfo { grip = 0.92f, wetGrip = 0.5f, slide = 0.78f, rolling = 0.012f, bump = 0.001f, bumpFreq = 30f, rumble = 0.1f };
                case SurfaceType.Concrete: return new SurfaceInfo { grip = 0.95f, wetGrip = 0.75f, slide = 0.8f, rolling = 0.012f, bump = 0.002f, bumpFreq = 14f, rumble = 0.15f };
                case SurfaceType.Grass: return new SurfaceInfo { grip = 0.48f, wetGrip = 0.32f, slide = 0.85f, rolling = 0.05f, drag = 0.04f, bump = 0.012f, bumpFreq = 5f, rumble = 0.45f, dusty = true, dust = new Color(0.36f, 0.4f, 0.22f) };
                case SurfaceType.Gravel: return new SurfaceInfo { grip = 0.4f, wetGrip = 0.36f, slide = 0.95f, rolling = 0.12f, drag = 0.22f, bump = 0.018f, bumpFreq = 11f, rumble = 0.9f, dusty = true, sinks = true, dust = new Color(0.62f, 0.58f, 0.5f) };
                case SurfaceType.Sand: return new SurfaceInfo { grip = 0.42f, wetGrip = 0.4f, slide = 0.95f, rolling = 0.14f, drag = 0.25f, bump = 0.01f, bumpFreq = 4f, rumble = 0.4f, dusty = true, sinks = true, dust = new Color(0.78f, 0.68f, 0.5f) };
                case SurfaceType.Dirt: return new SurfaceInfo { grip = 0.6f, wetGrip = 0.42f, slide = 0.9f, rolling = 0.04f, drag = 0.03f, bump = 0.014f, bumpFreq = 6f, rumble = 0.6f, dusty = true, dust = new Color(0.5f, 0.4f, 0.3f) };
                case SurfaceType.Snow: return new SurfaceInfo { grip = 0.35f, wetGrip = 0.33f, slide = 0.95f, rolling = 0.06f, drag = 0.08f, bump = 0.008f, bumpFreq = 5f, rumble = 0.3f, dusty = true, dust = new Color(0.92f, 0.94f, 0.97f) };
                case SurfaceType.Ice: return new SurfaceInfo { grip = 0.16f, wetGrip = 0.12f, slide = 0.9f, rolling = 0.01f, bump = 0.001f, bumpFreq = 10f, rumble = 0.05f };
                case SurfaceType.Metal: return new SurfaceInfo { grip = 0.7f, wetGrip = 0.45f, slide = 0.8f, rolling = 0.01f, bump = 0.003f, bumpFreq = 18f, rumble = 0.4f };
                default: return new SurfaceInfo { grip = 1f, wetGrip = 0.62f, slide = 0.8f, rolling = 0.011f, bump = 0.0012f, bumpFreq = 16f, rumble = 0.05f };
            }
        }

        public static bool IsOffTrack(SurfaceType t) => t == SurfaceType.Grass || t == SurfaceType.Gravel || t == SurfaceType.Sand || t == SurfaceType.Dirt || t == SurfaceType.Snow;
    }
}
