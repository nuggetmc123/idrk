using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Every generated material and texture the game uses at runtime. The editor's world builder (Veylix > Build Game
    /// World) creates them as real assets (so they're mip-mapped and compressed for Quest) and fills this in.
    /// Anything missing falls back to a plain material, so a half-built project still runs.
    /// </summary>
    public class VXAssets : MonoBehaviour
    {
        public static VXAssets I { get; private set; }

        /// The editor's model baker uses the asset set without entering Play mode.
        public static void UseInEditor(VXAssets a) => I = a;

        [Header("Car")]
        public Material paint, carbon, satinBlack, rimMetal, metal, interior, mirrorGlass, lightRed, lightWhite, visor, brakeDisc, plank, padding, screen, halo;
        public Material[] tyres = new Material[5]; // per compound (sidewall band colour)

        [Header("Track")]
        public Material asphalt, asphaltRoad, kerbRed, kerbBlue, kerbYellow, linePaint, runoff, concrete, wallConcrete, armco, tyreWall, fence, gravelTrap, sandTrap, grassVerge, pitLane, gridMarks, puddle, rubberMarks;

        [Header("Terrain")]
        public TerrainLayer[] terrainLayers; // grass, dryGrass, dirt, rock, sand, snow, gravel, forestFloor, tropicalGrass, mud

        [Header("Scenery")]
        public Material roofTiles, bark, palmBark, leavesConifer, leavesBroad, leavesPalm, leavesBirch, bush, rock, cactus, buildingWarm, buildingOffice, buildingBrick, buildingIndustrial, buildingStucco, buildingNight, roof, glass, grandstand, crowd, ads, gantry, lampHead, water, cloud, mountainFar, signs, neonA, neonB, neonC, road, roadLines, sidewalk, hangar, runway, container, snowCap, stars, flag, tent, boat, foam;
        public Material skybox;

        [Header("Effects")]
        public Material smoke, fire, spark, dust, rain, spray, glow, flash, debris, tyreMarks, lightCone;

        [Header("UI")]
        public Material uiPanel, uiPanelAdditive, uiIcon;
        public Font displayFont, bodyFont;
        public Texture2D[] mapPreviews; // circuit outline drawings, one per map

        void Awake()
        {
            I = this;
            // Materials the game changes while running (wet roads, lamps at night) are copied first, so playing in the
            // editor never modifies the saved assets.
            asphalt = Copy(asphalt); asphaltRoad = Copy(asphaltRoad); runoff = Copy(runoff); kerbRed = Copy(kerbRed); kerbBlue = Copy(kerbBlue);
            pitLane = Copy(pitLane); linePaint = Copy(linePaint); sidewalk = Copy(sidewalk); runway = Copy(runway); lampHead = Copy(lampHead);
            wet = new[] { asphalt, asphaltRoad, runoff, kerbRed, kerbBlue, pitLane, linePaint, sidewalk, runway };
            dryGloss = new float[wet.Length];
            dryColor = new Color[wet.Length];
            for (int i = 0; i < wet.Length; i++)
                if (wet[i] != null) { dryGloss[i] = wet[i].HasProperty("_Glossiness") ? wet[i].GetFloat("_Glossiness") : 0.2f; dryColor[i] = wet[i].color; }
        }

        static Material Copy(Material m) => m != null ? new Material(m) { name = m.name } : null;

        Material[] wet;
        float[] dryGloss;
        Color[] dryColor;
        float lastWet = -1f;

        /// <summary>Wet tarmac: darker and glossy, so it mirrors the sky and the lights.</summary>
        public void ApplyWetness(float w)
        {
            if (wet == null || Mathf.Abs(w - lastWet) < 0.01f) return;
            lastWet = w;
            for (int i = 0; i < wet.Length; i++)
            {
                if (wet[i] == null) continue;
                if (wet[i].HasProperty("_Glossiness")) wet[i].SetFloat("_Glossiness", Mathf.Lerp(dryGloss[i], 0.9f, w));
                wet[i].color = Color.Lerp(dryColor[i], dryColor[i] * 0.62f, w);
            }
        }

        public static Material Fallback(string name, Color c, float smooth = 0.3f)
        {
            var m = new Material(Shader.Find("Standard")) { name = name + " (fallback)", color = c };
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        public static Material Or(Material m, string name, Color c, float smooth = 0.3f) => m != null ? m : Fallback(name, c, smooth);

        public static TerrainLayer Layer(int i)
        {
            if (I == null || I.terrainLayers == null || I.terrainLayers.Length == 0) return null;
            return I.terrainLayers[Mathf.Clamp(i, 0, I.terrainLayers.Length - 1)];
        }
    }

    /// <summary>Indexes into VXAssets.terrainLayers.</summary>
    public static class TL
    {
        public const int Grass = 0, DryGrass = 1, Dirt = 2, Rock = 3, Sand = 4, Snow = 5, Gravel = 6, Forest = 7, Tropical = 8, Mud = 9;
    }
}
