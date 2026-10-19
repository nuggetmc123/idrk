using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Each place has its own sky and fog: the main menu a starry night, the Training Grounds a clear blue day, the
    /// Battleground a warm sunset. Switched whenever you move between them (Unity has one sky at a time).
    /// </summary>
    public static class MapSky
    {
        enum Place { None, Menu, Training, Battleground }
        static Place current;
        static Material menuSky, trainingSky, battleSky;

        static Material Make(Color sky, Color ground, float thickness, float exposure, float sunSize)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;
            var m = new Material(shader);
            m.SetColor("_SkyTint", sky);
            m.SetColor("_GroundColor", ground);
            m.SetFloat("_AtmosphereThickness", thickness);
            m.SetFloat("_Exposure", exposure);
            m.SetFloat("_SunSize", sunSize);
            return m;
        }

        /// <summary>Call every frame; only does anything when the place changes.</summary>
        public static void Update(bool inMenu, ArenaMap map)
        {
            var want = inMenu ? Place.Menu : map == ArenaMap.Battleground ? Place.Battleground : Place.Training;
            if (want == current) return;
            current = want;
            if (menuSky == null)
            {
                menuSky = Make(new Color(0.25f, 0.2f, 0.45f), new Color(0.05f, 0.04f, 0.08f), 0.4f, 0.35f, 0.02f);
                trainingSky = Make(new Color(0.45f, 0.6f, 0.9f), new Color(0.35f, 0.38f, 0.4f), 0.9f, 1.25f, 0.05f);
                battleSky = Make(new Color(0.9f, 0.5f, 0.35f), new Color(0.3f, 0.22f, 0.2f), 1.8f, 1.1f, 0.07f);
            }
            switch (want)
            {
                case Place.Menu:
                    Apply(menuSky, new Color(0.12f, 0.08f, 0.2f), 45f, 200f, new Color(0.35f, 0.3f, 0.55f));
                    break;
                case Place.Training:
                    Apply(trainingSky, new Color(0.62f, 0.72f, 0.88f), 90f, 380f, new Color(0.6f, 0.62f, 0.7f));
                    break;
                default:
                    Apply(battleSky, new Color(0.75f, 0.52f, 0.45f), 90f, 380f, new Color(0.65f, 0.5f, 0.5f));
                    break;
            }
        }

        static void Apply(Material sky, Color fog, float fogStart, float fogEnd, Color ambient)
        {
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientSkyColor = ambient;
            DynamicGI.UpdateEnvironment();
        }
    }
}
