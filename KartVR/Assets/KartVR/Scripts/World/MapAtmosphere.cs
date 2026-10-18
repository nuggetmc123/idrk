using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Gives each map its own mood: haze that matches its sky (so the far edges melt into the dome instead of
    /// ending sharply), sunlight colour and ambient light. Changes when you go to a different map.
    /// </summary>
    public class MapAtmosphere : MonoBehaviour
    {
        KartMode applied = (KartMode)255;
        Color baseSun = Color.white;
        float baseIntensity = 1.2f;
        bool captured;

        void Update()
        {
            var mode = GameState.CurrentMode;
            if (mode == applied) return;
            applied = mode;
            var sun = RenderSettings.sun;
            if (sun != null && !captured) { baseSun = sun.color; baseIntensity = sun.intensity; captured = true; }

            var map = KartMaps.Get(mode);
            Color fog, sky, equator, ground, sunColor = baseSun;
            float sunIntensity = baseIntensity;
            switch (map.theme)
            {
                case SkyDomes.Theme.Night:
                    fog = new Color(0.16f, 0.07f, 0.14f); sky = new Color(0.35f, 0.25f, 0.55f); equator = new Color(0.35f, 0.18f, 0.25f); ground = new Color(0.15f, 0.06f, 0.05f);
                    sunColor = new Color(0.75f, 0.6f, 1f); sunIntensity = baseIntensity * 0.55f; break;
                case SkyDomes.Theme.Sunset:
                    fog = new Color(0.95f, 0.68f, 0.52f); sky = new Color(0.75f, 0.6f, 0.75f); equator = new Color(0.9f, 0.6f, 0.45f); ground = new Color(0.35f, 0.25f, 0.2f);
                    sunColor = new Color(1f, 0.78f, 0.55f); break;
                case SkyDomes.Theme.Desert:
                    fog = new Color(0.95f, 0.8f, 0.6f); sky = new Color(0.65f, 0.72f, 0.9f); equator = new Color(0.85f, 0.65f, 0.45f); ground = new Color(0.45f, 0.3f, 0.2f);
                    sunColor = new Color(1f, 0.9f, 0.75f); sunIntensity = baseIntensity * 1.1f; break;
                case SkyDomes.Theme.Ice:
                    fog = new Color(0.78f, 0.88f, 1f); sky = new Color(0.65f, 0.75f, 0.95f); equator = new Color(0.65f, 0.75f, 0.85f); ground = new Color(0.4f, 0.45f, 0.55f);
                    sunColor = new Color(0.85f, 0.92f, 1f); break;
                default: // Day / Sunny
                    fog = new Color(0.75f, 0.85f, 0.96f); sky = new Color(0.6f, 0.7f, 0.85f); equator = new Color(0.55f, 0.55f, 0.52f); ground = new Color(0.28f, 0.25f, 0.22f);
                    break;
            }
            RenderSettings.fog = mode != KartMode.Hub; // indoors: no haze
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = map.radius * 0.45f;
            RenderSettings.fogEndDistance = map.radius * 3f;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
        }
    }
}
