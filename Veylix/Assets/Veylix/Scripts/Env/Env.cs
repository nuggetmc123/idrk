using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The shared state of the world that physics, audio and visuals read: how wet the track is, temperatures, wind.
    /// TimeOfDay and Weather write it every frame.
    /// </summary>
    public static class Env
    {
        /// <summary>0 dry .. 1 standing water. Grip and braking follow this.</summary>
        public static float Wetness;
        public static float Rain;        // 0..1 how hard it is raining now
        public static float Fog;         // 0..1
        public static float AmbientC = 22f;
        public static float TrackC = 32f;
        public static float Hour = 14f;  // 0..24
        public static float Daylight = 1f; // 0 night .. 1 full day
        public static Vector3 Wind;

        /// <summary>Puddles collect in the dips: a little wetter in places, so a wet lap isn't uniformly slippery.</summary>
        public static float WetnessAt(Vector3 p)
        {
            if (Wetness <= 0.001f) return 0f;
            float puddle = Mathf.Clamp01(Noise.Perlin(p.x * 0.05f, p.z * 0.05f, 0, 9) * 1.5f + 0.2f);
            return Mathf.Clamp01(Wetness * (0.85f + puddle * 0.3f));
        }

        public static bool IsNight => Daylight < 0.25f;
    }
}
