using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Each place has its own lighting (LightingPresets): the main menu and the Training Grounds are indoor test-lab
    /// looks, the Battleground a warm late afternoon (dusk in spooky season). Switched whenever you move between them
    /// (Unity has one sky, one sun and one fog at a time): sky, sun direction and colour, ambient, fog and the
    /// fallback reflections.
    /// </summary>
    public static class MapSky
    {
        enum Place { None, Menu, Training, Battleground }
        static Place current;
        static Material menuSky, trainingSky, battleSky;
        static Cubemap menuReflect, trainingReflect, battleReflect;

        static Material Make(LightingPreset p)
        {
            var shader = Shader.Find("Skybox/Procedural"); // kept in builds by the scene's Sky material
            if (shader == null) return null;
            var m = new Material(shader) { name = "Sky_" + p.name };
            LightingPresets.SetUpSky(m, p);
            return m;
        }

        /// <summary>Call every frame; only does anything when the place changes.</summary>
        public static void Update(bool inMenu, ArenaMap map)
        {
            var want = inMenu ? Place.Menu : map == ArenaMap.Battleground ? Place.Battleground : Place.Training;
            if (want == current) return;
            current = want;
            if (menuSky == null && trainingSky == null && battleSky == null)
            {
                menuSky = Make(LightingPresets.Menu);
                trainingSky = Make(LightingPresets.Training);
                battleSky = Make(LightingPresets.BattlegroundNow);
            }
            if (menuReflect == null)
            {
                menuReflect = LightingPresets.MakeReflectionCubemap(LightingPresets.Menu);
                trainingReflect = LightingPresets.MakeReflectionCubemap(LightingPresets.Training);
                battleReflect = LightingPresets.MakeReflectionCubemap(LightingPresets.BattlegroundNow);
            }
            switch (want)
            {
                case Place.Menu: LightingPresets.Apply(LightingPresets.Menu, menuSky, menuReflect); break;
                case Place.Training: LightingPresets.Apply(LightingPresets.Training, trainingSky, trainingReflect); break;
                default: LightingPresets.Apply(LightingPresets.BattlegroundNow, battleSky, battleReflect); break;
            }
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Forget the current place so the next Update applies its preset again (e.g. after a scene reload).</summary>
        public static void Reset() => current = Place.None;
    }
}
