using UnityEngine;

namespace Veylix
{
    public enum Biome { Riviera, Parkland, Desert, JapanHills, Coast, Alpine, Tropical, CityNight, Ardennes, Gulf, Farmland, Snow, Outback, Port, Neon, Highland, Airfield }
    public enum TerrainStyle { Flat, Rolling, Hills, Mountains, Dunes, Valley, Island }
    public enum WeatherKind { Clear, Cloudy, Overcast, LightRain, HeavyRain, Fog }

    [System.Flags]
    public enum Feature
    {
        None = 0,
        Street = 1,          // walls right at the track edge, buildings behind
        Floodlights = 2,     // full track lighting (night races)
        Sea = 4,             // ocean on one side
        Harbour = 8,         // marina with yachts
        GravelTraps = 16,
        Grandstands = 32,
        Skyline = 64,
        Neon = 128,
        Containers = 256,
        Cranes = 512,
        Pagoda = 1024,
        Castle = 2048,
        Windmills = 4096,
        Volcano = 8192,
        Runway = 16384,
        Lighthouse = 32768,
        StadiumBowl = 65536,
        FerrisWheel = 131072,
        Village = 262144,
        Bridge = 524288,
    }

    /// <summary>One of the 25 maps: its layout, place in the world, look, and defaults.</summary>
    public class MapDef
    {
        public int index;
        public string id, name, location, blurb;
        public bool freeRoam;
        public Biome biome;
        public TerrainStyle terrain;
        public float[] points;          // x, z, elevation triplets (metres, before scale)
        public float scale = 1f, elevScale = 1f;
        public float width = 13f;
        public float hour = 14f;
        public WeatherKind weather = WeatherKind.Clear;
        public int laps = 5;
        public Feature features;
        public float worldSize = 3000f; // terrain edge length (m)
        public float relief = 60f;      // height of the surrounding hills (m)
        public float seaLevel = -1000f; // world y of the sea (if Feature.Sea)
        public Vector2 seaDir = new Vector2(-1f, 0f); // which side the coast is on
        public SurfaceType ground = SurfaceType.Grass;
        public int extraRoads;          // free roam: additional roads off the main loop
        public int seed;
        public float ambientC = 22f;

        public bool Has(Feature f) => (features & f) != 0;
        public string KindLabel => freeRoam ? "OPEN WORLD" : "CIRCUIT";
    }

    public static class MapCatalog
    {
        // Lazy: the layout arrays below are static fields too, and C# initialises statics in file order.
        static MapDef[] all;
        public static MapDef[] All => all ?? (all = Build());
        public static int Count => All.Length;
        public static MapDef Get(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];

        static MapDef[] Build()
        {
            var m = new[]
            {
                new MapDef { id = "monaco", name = "Monaco Street Circuit", location = "Port Lumière, Riviera", blurb = "Barriers inches away, a climb to the casino, the tightest hairpin in racing and a tunnel along the sea. No room for error.",
                    biome = Biome.Riviera, terrain = TerrainStyle.Hills, points = P_monaco, scale = 1.6f, width = 10f, hour = 15f, laps = 8,
                    features = Feature.Street | Feature.Sea | Feature.Harbour | Feature.Grandstands, relief = 120f, seaLevel = 0.5f, seaDir = new Vector2(0.3f, -1f), ambientC = 24f },
                new MapDef { id = "silverstone", name = "Silverstone Inspired Circuit", location = "Ashby Park, England", blurb = "An old airfield turned temple of speed: flat-out sweepers, a blistering sequence of esses and a slow final complex.",
                    biome = Biome.Parkland, terrain = TerrainStyle.Flat, points = P_silverstone, scale = 1.6f, width = 15f, hour = 13f, weather = WeatherKind.Cloudy, laps = 5,
                    features = Feature.GravelTraps | Feature.Grandstands, relief = 25f, ambientC = 17f },
                new MapDef { id = "desert", name = "Desert Grand Prix", location = "Al Saqr, Arabian Gulf", blurb = "Long straights, heavy braking into slow hairpins and dunes all around. Raced into the night under floodlights.",
                    biome = Biome.Desert, terrain = TerrainStyle.Dunes, points = P_desert, scale = 1.25f, width = 15f, hour = 18.2f, laps = 5,
                    features = Feature.Floodlights | Feature.Grandstands, relief = 35f, ground = SurfaceType.Sand, ambientC = 31f },
                new MapDef { id = "japan", name = "Japanese Mountain Circuit", location = "Kamisato, Japan", blurb = "Snaking esses up the hillside, a hairpin, a long double-apex and a fast sweep down to the chicane. Pure flow.",
                    biome = Biome.JapanHills, terrain = TerrainStyle.Hills, points = P_japan, scale = 2f, width = 13f, hour = 10f, laps = 5,
                    features = Feature.GravelTraps | Feature.Grandstands | Feature.Pagoda | Feature.FerrisWheel, relief = 150f, ambientC = 20f },
                new MapDef { id = "coastal", name = "Coastal Grand Prix", location = "Cala Serena, Mediterranean", blurb = "From the seafront up to the clifftops and back. Gusty, fast, and beautiful at golden hour.",
                    biome = Biome.Coast, terrain = TerrainStyle.Hills, points = P_coastal, scale = 1.4f, width = 13f, hour = 18.6f, laps = 5,
                    features = Feature.Sea | Feature.Grandstands | Feature.Lighthouse, relief = 110f, seaLevel = 0.5f, seaDir = new Vector2(-1f, 0.1f), ambientC = 25f },
                new MapDef { id = "alpine", name = "Alpine Racing Circuit", location = "Hochfeld, Austria", blurb = "Short, steep and savage: a long climb to a hairpin, then a plunge back through the valley.",
                    biome = Biome.Alpine, terrain = TerrainStyle.Mountains, points = P_alpine, scale = 1.6f, width = 14f, hour = 11f, weather = WeatherKind.Cloudy, laps = 7,
                    features = Feature.GravelTraps | Feature.Grandstands, relief = 520f, ambientC = 15f },
                new MapDef { id = "tropical", name = "Tropical Island Circuit", location = "Pulau Sentang", blurb = "Wide, fast and humid. Afternoon storms roll in off the sea without warning.",
                    biome = Biome.Tropical, terrain = TerrainStyle.Rolling, points = P_tropical, scale = 1.5f, width = 15f, hour = 15.5f, weather = WeatherKind.LightRain, laps = 5,
                    features = Feature.Grandstands | Feature.Sea, relief = 60f, seaLevel = 0.3f, seaDir = new Vector2(0f, -1f), ambientC = 31f },
                new MapDef { id = "nightcity", name = "Night City Circuit", location = "Marina Vale", blurb = "Ninety-degree corners between skyscrapers, all under floodlights. Hot, bumpy and relentless.",
                    biome = Biome.CityNight, terrain = TerrainStyle.Flat, points = P_nightcity, scale = 1.6f, width = 12f, hour = 21f, laps = 6,
                    features = Feature.Street | Feature.Floodlights | Feature.Skyline | Feature.Grandstands | Feature.Bridge | Feature.Sea, relief = 10f, seaLevel = -0.5f, seaDir = new Vector2(1f, -0.4f), ambientC = 28f },
                new MapDef { id = "forest", name = "Forest Grand Prix", location = "Malvoie, Ardennes", blurb = "A hairpin, the plunge and climb through the valley, then miles of fast corners through the pines.",
                    biome = Biome.Ardennes, terrain = TerrainStyle.Valley, points = P_forest, scale = 1.65f, width = 14f, hour = 12f, weather = WeatherKind.Overcast, laps = 4,
                    features = Feature.GravelTraps | Feature.Grandstands, relief = 140f, ambientC = 16f },
                new MapDef { id = "stadium", name = "Modern Stadium Circuit", location = "Qasr Al Bahr", blurb = "Billion-dollar architecture, a hotel over the track and a twisty stadium section. Starts at sunset, ends under lights.",
                    biome = Biome.Gulf, terrain = TerrainStyle.Flat, points = P_stadium, scale = 1.3f, width = 14f, hour = 17.8f, laps = 6,
                    features = Feature.Floodlights | Feature.Grandstands | Feature.StadiumBowl | Feature.Harbour | Feature.Sea, relief = 15f, seaLevel = 0.2f, seaDir = new Vector2(1f, 0.3f), ground = SurfaceType.Sand, ambientC = 30f },
                new MapDef { id = "countryside", name = "European Countryside Circuit", location = "Valle del Santerno", blurb = "Old-school and undulating, with a banked final turn. Fields, vineyards and a castle on the hill.",
                    biome = Biome.Farmland, terrain = TerrainStyle.Rolling, points = P_countryside, scale = 2.1f, width = 13f, hour = 9.5f, laps = 6,
                    features = Feature.GravelTraps | Feature.Grandstands | Feature.Castle | Feature.Village, relief = 70f, ambientC = 21f },
                new MapDef { id = "snowcircuit", name = "Snowy Mountain Circuit", location = "Isfjell, Norway", blurb = "A winter circuit carved into a frozen valley. Cold tyres, cold brakes and snow banks that bite.",
                    biome = Biome.Snow, terrain = TerrainStyle.Mountains, points = P_snowcircuit, scale = 1.9f, width = 13f, hour = 12.5f, weather = WeatherKind.Overcast, laps = 6,
                    features = Feature.Grandstands, relief = 420f, ground = SurfaceType.Snow, ambientC = -4f },
                new MapDef { id = "outback", name = "Australian Outback Circuit", location = "Coolabah Downs", blurb = "Red earth, huge skies and long flowing corners. Fast and open with nowhere to hide.",
                    biome = Biome.Outback, terrain = TerrainStyle.Rolling, points = P_outback, scale = 1.6f, width = 14f, hour = 16.5f, laps = 5,
                    features = Feature.GravelTraps | Feature.Grandstands | Feature.Windmills, relief = 40f, ground = SurfaceType.Dirt, ambientC = 29f },
                new MapDef { id = "port", name = "Industrial Port Circuit", location = "Havenstad Docks", blurb = "Long drags between container stacks and cranes, with tight corners at the quays. Dusk on the docks.",
                    biome = Biome.Port, terrain = TerrainStyle.Flat, points = P_port, scale = 1.3f, width = 13f, hour = 19.3f, weather = WeatherKind.Cloudy, laps = 6,
                    features = Feature.Street | Feature.Containers | Feature.Cranes | Feature.Sea | Feature.Floodlights | Feature.Grandstands, relief = 8f, seaLevel = -1f, seaDir = new Vector2(1f, 0f), ground = SurfaceType.Concrete, ambientC = 14f },
                new MapDef { id = "neon", name = "Neon City Circuit", location = "Silverline Strip", blurb = "A two-kilometre straight down the strip, past every sign in town. Midnight start.",
                    biome = Biome.Neon, terrain = TerrainStyle.Flat, points = P_neon, scale = 1.25f, width = 14f, hour = 23.5f, laps = 6,
                    features = Feature.Street | Feature.Floodlights | Feature.Neon | Feature.Skyline | Feature.Grandstands, relief = 30f, ground = SurfaceType.Concrete, ambientC = 18f },

                // ---------------- free roam
                new MapDef { id = "mountainroads", name = "Open Mountain Roads", location = "Col de Verre", freeRoam = true, blurb = "Switchbacks up the mountainside to a high pass, a ridge road and a long descent home.",
                    biome = Biome.Highland, terrain = TerrainStyle.Mountains, points = P_mountainroads, scale = 1.3f, elevScale = 1.5f, width = 9f, hour = 8.5f, laps = 2,
                    worldSize = 6000f, relief = 700f, extraRoads = 4, ambientC = 14f },
                new MapDef { id = "coastalhwy", name = "Coastal Highway", location = "Pacific Rim Road", freeRoam = true, blurb = "Ten kilometres of sweeping highway along cliffs and beaches, with lanes into the hills.",
                    biome = Biome.Coast, terrain = TerrainStyle.Hills, points = P_coastalhwy, scale = 1f, width = 11f, hour = 17.5f, laps = 1,
                    features = Feature.Sea | Feature.Lighthouse | Feature.Bridge, worldSize = 8000f, relief = 260f, seaLevel = 0.4f, seaDir = new Vector2(-1f, 0f), extraRoads = 4, ambientC = 21f },
                new MapDef { id = "deserthwy", name = "Desert Highway", location = "Mesa Seca", freeRoam = true, blurb = "Arrow-straight tarmac across the desert, mesas on the horizon and nothing to slow you down.",
                    biome = Biome.Desert, terrain = TerrainStyle.Dunes, points = P_deserthwy, scale = 1f, width = 12f, hour = 12f, laps = 1,
                    worldSize = 8000f, relief = 220f, ground = SurfaceType.Sand, extraRoads = 4, ambientC = 35f },
                new MapDef { id = "countryroads", name = "Countryside Roads", location = "Haute Vallée", freeRoam = true, blurb = "Narrow lanes between fields, hedgerows and villages. Perfect for learning car control.",
                    biome = Biome.Farmland, terrain = TerrainStyle.Rolling, points = P_countryroads, scale = 1.1f, width = 8.5f, hour = 10.5f, laps = 1,
                    features = Feature.Village | Feature.Windmills | Feature.Castle, worldSize = 6000f, relief = 90f, extraRoads = 6, ambientC = 20f },
                new MapDef { id = "city", name = "Large Modern City", location = "Nova Halden", freeRoam = true, blurb = "A whole downtown grid to explore, from boulevards to back streets, under a skyline of towers.",
                    biome = Biome.CityNight, terrain = TerrainStyle.Flat, points = P_city, scale = 1f, width = 14f, hour = 19f, laps = 2,
                    features = Feature.Street | Feature.Skyline | Feature.Floodlights | Feature.Bridge, worldSize = 5000f, relief = 40f, ground = SurfaceType.Concrete, extraRoads = 0, ambientC = 19f },
                new MapDef { id = "forestroads", name = "Forest Roads", location = "Schwarzwald Loop", freeRoam = true, blurb = "Winding tarmac under tall firs, damp in the shade and slippery when the rain comes.",
                    biome = Biome.Ardennes, terrain = TerrainStyle.Hills, points = P_forestroads, scale = 1.2f, width = 8.5f, hour = 16f, weather = WeatherKind.Overcast, laps = 1,
                    worldSize = 6000f, relief = 240f, extraRoads = 5, ambientC = 13f },
                new MapDef { id = "snowpass", name = "Snowy Mountain Pass", location = "Passo Bianco", freeRoam = true, blurb = "Three hairpins up a frozen slope to the pass, then a long run back down the valley.",
                    biome = Biome.Snow, terrain = TerrainStyle.Mountains, points = P_snowpass, scale = 1.2f, elevScale = 1.6f, width = 9f, hour = 13f, weather = WeatherKind.Cloudy, laps = 1,
                    worldSize = 6000f, relief = 800f, ground = SurfaceType.Snow, extraRoads = 3, ambientC = -8f },
                new MapDef { id = "tropicalroads", name = "Tropical Island Roads", location = "Isla Verde", freeRoam = true, blurb = "Round the island past palm-lined beaches, with tracks climbing into the jungle and the old volcano.",
                    biome = Biome.Tropical, terrain = TerrainStyle.Island, points = P_tropicalroads, scale = 1.3f, width = 9f, hour = 11f, laps = 1,
                    features = Feature.Sea | Feature.Volcano | Feature.Village, worldSize = 6000f, relief = 380f, seaLevel = 0.3f, extraRoads = 4, ambientC = 30f },
                new MapDef { id = "airport", name = "Airport Test Track", location = "Veylix Proving Ground", freeRoam = true, blurb = "A disused airfield: kilometres of runway, a handling loop and endless tarmac to practise on.",
                    biome = Biome.Airfield, terrain = TerrainStyle.Flat, points = P_airport, scale = 1f, width = 16f, hour = 14f, laps = 3,
                    features = Feature.Runway, worldSize = 5000f, relief = 60f, extraRoads = 2, ambientC = 19f },
                new MapDef { id = "island", name = "Massive Free Roam Island", location = "Ilha Grande", freeRoam = true, blurb = "Thirteen kilometres of ring road around a whole island: beaches, cliffs, a town, forest and a mountain in the middle.",
                    biome = Biome.Tropical, terrain = TerrainStyle.Island, points = P_island, scale = 1f, width = 10f, hour = 9f, laps = 1,
                    features = Feature.Sea | Feature.Village | Feature.Lighthouse | Feature.Harbour | Feature.Volcano, worldSize = 8000f, relief = 520f, seaLevel = 0.3f, extraRoads = 6, ambientC = 27f },
            };
            for (int i = 0; i < m.Length; i++) { m[i].index = i; m[i].seed = 1000 + i * 77; }
            return m;
        }

        static readonly float[] P_monaco = { 0f, 0f, 4f, 120f, 50f, 4f, 230f, 95f, 5f, 262f, 108f, 6f, 290f, 96f, 8f, 330f, 90f, 12f, 420f, 110f, 22f, 500f, 140f, 30f, 540f, 165f, 35f, 560f, 195f, 38f, 580f, 215f, 40f, 620f, 215f, 40f, 670f, 195f, 34f, 700f, 160f, 27f, 705f, 130f, 23f, 690f, 110f, 20f, 665f, 100f, 18f, 650f, 88f, 17f, 655f, 70f, 16f, 672f, 66f, 15f, 700f, 60f, 13f, 720f, 40f, 11f, 715f, 15f, 10f, 695f, 0f, 9f, 630f, -40f, 8f, 560f, -80f, 7f, 480f, -110f, 6f, 430f, -125f, 5f, 415f, -118f, 5f, 400f, -128f, 5f, 330f, -140f, 4f, 300f, -130f, 4f, 260f, -112f, 3f, 245f, -98f, 3f, 225f, -100f, 3f, 205f, -90f, 3f, 185f, -92f, 3f, 130f, -95f, 3f, 20f, -100f, 3f, -20f, -95f, 3f, -35f, -80f, 3f, -30f, -62f, 3f, -28f, -40f, 3f, -18f, -18f, 4f };
        static readonly float[] P_silverstone = { 0f, 0f, 0f, 400f, 0f, 0f, 700f, 0f, 0f, 820f, -30f, 0f, 870f, -100f, 0f, 880f, -180f, 0f, 860f, -240f, 0f, 890f, -290f, 0f, 860f, -340f, 0f, 880f, -390f, 0f, 920f, -420f, 0f, 1000f, -500f, 0f, 1060f, -700f, 0f, 1060f, -780f, 0f, 1020f, -820f, 0f, 950f, -830f, 0f, 920f, -850f, 0f, 880f, -845f, 0f, 850f, -830f, 0f, 830f, -800f, 0f, 780f, -760f, 0f, 650f, -740f, 0f, 520f, -720f, 0f, 450f, -740f, 0f, 400f, -740f, 0f, 250f, -760f, 0f, 150f, -730f, 0f, 110f, -650f, 0f, 100f, -560f, 0f, 60f, -500f, 0f, 70f, -430f, 0f, 40f, -380f, 0f, 0f, -350f, 0f, -40f, -300f, 0f, -60f, -220f, 0f, -60f, -100f, 0f, -45f, -30f, 0f };
        static readonly float[] P_desert = { 0f, 0f, 0f, 0f, 500f, 0f, 0f, 900f, 0f, 10f, 960f, 0f, 50f, 975f, 0f, 80f, 950f, 0f, 100f, 920f, 0f, 150f, 900f, 0f, 300f, 880f, 0f, 450f, 870f, 0f, 500f, 850f, 0f, 505f, 800f, 0f, 480f, 720f, 0f, 510f, 650f, 0f, 485f, 580f, 0f, 470f, 520f, 0f, 480f, 480f, 0f, 510f, 470f, 0f, 530f, 500f, 0f, 540f, 560f, 0f, 560f, 590f, 0f, 600f, 580f, 0f, 800f, 450f, 0f, 950f, 350f, 0f, 1000f, 330f, 0f, 1050f, 340f, 0f, 1120f, 300f, 0f, 1150f, 220f, 0f, 1150f, 120f, 0f, 1120f, 80f, 0f, 800f, 50f, 0f, 500f, -40f, 0f, 200f, -80f, 0f, 80f, -80f, 0f, 20f, -50f, 0f };
        static readonly float[] P_japan = { 0f, 0f, 6f, 350f, 0f, 5f, 480f, -15f, 6f, 530f, -70f, 8f, 520f, -130f, 11f, 555f, -180f, 14f, 520f, -230f, 17f, 555f, -280f, 20f, 525f, -330f, 23f, 520f, -400f, 26f, 545f, -450f, 28f, 530f, -490f, 29f, 500f, -560f, 30f, 470f, -590f, 30f, 440f, -575f, 30f, 445f, -540f, 30f, 400f, -500f, 31f, 340f, -480f, 33f, 280f, -500f, 35f, 200f, -560f, 36f, 150f, -620f, 35f, 90f, -630f, 34f, 40f, -600f, 32f, -60f, -520f, 26f, -150f, -430f, 20f, -200f, -330f, 16f, -205f, -230f, 13f, -185f, -160f, 11f, -170f, -140f, 11f, -160f, -115f, 10f, -175f, -95f, 10f, -150f, -40f, 8f, -80f, -5f, 7f };
        static readonly float[] P_coastal = { 0f, 0f, 4f, 0f, 500f, 4f, 0f, 800f, 5f, 30f, 880f, 6f, 90f, 900f, 8f, 200f, 880f, 14f, 300f, 920f, 20f, 380f, 980f, 26f, 450f, 980f, 30f, 520f, 930f, 34f, 560f, 860f, 36f, 620f, 820f, 37f, 640f, 750f, 36f, 620f, 600f, 30f, 560f, 480f, 22f, 560f, 300f, 16f, 540f, 150f, 12f, 520f, 80f, 10f, 540f, 50f, 10f, 525f, 20f, 9f, 500f, -100f, 7f, 450f, -200f, 6f, 350f, -240f, 5f, 200f, -230f, 4f, 100f, -200f, 4f, 40f, -170f, 4f, 10f, -110f, 4f };
        static readonly float[] P_alpine = { 0f, 0f, 0f, 0f, 300f, 10f, 20f, 500f, 22f, 60f, 560f, 26f, 120f, 560f, 28f, 400f, 620f, 40f, 650f, 680f, 50f, 720f, 700f, 52f, 760f, 680f, 52f, 760f, 640f, 51f, 730f, 620f, 50f, 550f, 520f, 42f, 400f, 450f, 35f, 340f, 420f, 32f, 300f, 380f, 30f, 300f, 320f, 27f, 330f, 270f, 25f, 380f, 220f, 22f, 380f, 170f, 20f, 340f, 100f, 14f, 260f, 50f, 10f, 150f, 40f, 6f, 60f, 20f, 3f, 50f, -60f, 1f, 15f, -50f, 0f };
        static readonly float[] P_tropical = { 0f, 0f, 0f, 0f, 600f, 0f, 20f, 680f, 0f, 70f, 700f, 0f, 110f, 680f, 0f, 120f, 630f, 0f, 140f, 590f, 0f, 190f, 580f, 0f, 300f, 600f, 0f, 380f, 580f, 0f, 420f, 520f, 0f, 420f, 460f, 0f, 380f, 400f, 0f, 410f, 340f, 0f, 380f, 280f, 0f, 400f, 220f, 0f, 460f, 200f, 0f, 520f, 210f, 0f, 550f, 250f, 0f, 530f, 290f, 0f, 520f, 330f, 0f, 560f, 400f, 0f, 620f, 420f, 0f, 680f, 400f, 0f, 760f, 330f, 0f, 800f, 250f, 0f, 820f, 150f, 0f, 800f, 90f, 0f, 760f, 80f, 0f, 500f, -40f, 0f, 200f, -80f, 0f, 80f, -100f, 0f, 30f, -80f, 0f, 10f, -40f, 0f };
        static readonly float[] P_nightcity = { 0f, 0f, 0f, 0f, 400f, 0f, 0f, 450f, 0f, -40f, 480f, 0f, -40f, 530f, 0f, 0f, 560f, 0f, 0f, 700f, 0f, 10f, 750f, 0f, 60f, 760f, 0f, 300f, 760f, 0f, 340f, 750f, 0f, 350f, 710f, 0f, 350f, 600f, 0f, 360f, 560f, 0f, 400f, 550f, 0f, 550f, 550f, 0f, 590f, 540f, 0f, 600f, 500f, 0f, 600f, 350f, 0f, 590f, 300f, 0f, 620f, 280f, 0f, 620f, 240f, 0f, 620f, 100f, 0f, 610f, 40f, 0f, 570f, 20f, 0f, 540f, 40f, 0f, 520f, 120f, 0f, 500f, 160f, 0f, 460f, 170f, 0f, 300f, 170f, 0f, 260f, 160f, 0f, 250f, 120f, 0f, 250f, 40f, 0f, 240f, 0f, 0f, 200f, -10f, 0f, 100f, -60f, 0f, 40f, -70f, 0f, 5f, -45f, 0f };
        static readonly float[] P_forest = { 0f, 0f, 20f, 0f, 120f, 20f, 20f, 160f, 19f, 55f, 155f, 19f, 60f, 120f, 18f, 70f, 0f, 10f, 80f, -150f, 2f, 100f, -200f, 0f, 80f, -240f, 8f, 110f, -290f, 18f, 250f, -450f, 30f, 420f, -640f, 38f, 470f, -690f, 40f, 520f, -690f, 39f, 550f, -720f, 38f, 560f, -770f, 36f, 560f, -840f, 32f, 530f, -870f, 30f, 490f, -850f, 29f, 430f, -800f, 25f, 360f, -790f, 20f, 300f, -830f, 16f, 270f, -890f, 14f, 270f, -960f, 12f, 220f, -1000f, 11f, 160f, -990f, 10f, 60f, -940f, 9f, 0f, -860f, 9f, -40f, -760f, 10f, -50f, -500f, 12f, -50f, -250f, 15f, -45f, -140f, 17f, -25f, -120f, 17f, -30f, -95f, 18f, -50f, -80f, 18f, -30f, -40f, 19f, -10f, -15f, 20f };
        static readonly float[] P_stadium = { 0f, 0f, 0f, 0f, 350f, 0f, -10f, 400f, 0f, -50f, 420f, 0f, -150f, 420f, 0f, -190f, 440f, 0f, -200f, 480f, 0f, -180f, 540f, 0f, -210f, 600f, 0f, -190f, 680f, 0f, -150f, 700f, 0f, 300f, 700f, 0f, 600f, 700f, 0f, 650f, 700f, 0f, 680f, 720f, 0f, 710f, 700f, 0f, 760f, 680f, 0f, 770f, 630f, 0f, 740f, 600f, 0f, 600f, 600f, 0f, 560f, 580f, 0f, 550f, 540f, 0f, 550f, 450f, 0f, 530f, 410f, 0f, 490f, 400f, 0f, 400f, 400f, 0f, 360f, 390f, 0f, 350f, 350f, 0f, 350f, 250f, 0f, 330f, 210f, 0f, 290f, 200f, 0f, 150f, 200f, 0f, 110f, 190f, 0f, 100f, 150f, 0f, 100f, 50f, 0f, 80f, -40f, 0f, 40f, -70f, 0f, 10f, -40f, 0f };
        static readonly float[] P_countryside = { 0f, 0f, 5f, 0f, 400f, 8f, 20f, 480f, 10f, 0f, 540f, 11f, 20f, 580f, 11f, 0f, 620f, 12f, -10f, 680f, 12f, -50f, 700f, 13f, -80f, 680f, 14f, -150f, 640f, 20f, -220f, 650f, 24f, -300f, 620f, 22f, -330f, 560f, 18f, -310f, 500f, 16f, -320f, 420f, 15f, -300f, 400f, 15f, -310f, 370f, 14f, -320f, 200f, 8f, -300f, 80f, 6f, -260f, 20f, 5f, -200f, -10f, 5f, -80f, -50f, 5f, -20f, -45f, 5f, 5f, -20f, 5f };
        static readonly float[] P_snowcircuit = { 0f, 0f, 0f, 0f, 300f, 4f, 10f, 360f, 6f, 50f, 380f, 8f, 85f, 360f, 10f, 90f, 320f, 11f, 110f, 250f, 14f, 160f, 200f, 18f, 240f, 190f, 22f, 320f, 220f, 26f, 380f, 280f, 28f, 420f, 300f, 30f, 470f, 290f, 30f, 520f, 240f, 27f, 540f, 170f, 24f, 530f, 120f, 22f, 555f, 100f, 21f, 545f, 70f, 20f, 500f, 0f, 15f, 420f, -60f, 10f, 300f, -120f, 6f, 180f, -130f, 4f, 80f, -110f, 2f, 20f, -70f, 1f };
        static readonly float[] P_outback = { 0f, 0f, 5f, 0f, 600f, 8f, 40f, 720f, 10f, 130f, 770f, 12f, 250f, 760f, 14f, 340f, 720f, 14f, 380f, 640f, 13f, 360f, 560f, 12f, 330f, 520f, 12f, 340f, 480f, 12f, 380f, 470f, 12f, 500f, 480f, 10f, 600f, 440f, 9f, 640f, 380f, 8f, 630f, 300f, 8f, 600f, 220f, 9f, 620f, 140f, 11f, 600f, 60f, 14f, 540f, 20f, 15f, 480f, 30f, 15f, 450f, 0f, 14f, 460f, -40f, 13f, 420f, -90f, 10f, 330f, -110f, 8f, 200f, -100f, 6f, 80f, -80f, 5f, 20f, -40f, 5f };
        static readonly float[] P_port = { 0f, 0f, 0f, 0f, 700f, 0f, 10f, 760f, 0f, 60f, 770f, 0f, 300f, 770f, 0f, 340f, 780f, 0f, 350f, 820f, 0f, 350f, 950f, 0f, 360f, 1000f, 0f, 400f, 1010f, 0f, 430f, 990f, 0f, 430f, 950f, 0f, 430f, 700f, 0f, 440f, 660f, 0f, 480f, 650f, 0f, 700f, 650f, 0f, 740f, 640f, 0f, 750f, 600f, 0f, 750f, 200f, 0f, 740f, 150f, 0f, 700f, 140f, 0f, 500f, 140f, 0f, 460f, 130f, 0f, 450f, 90f, 0f, 450f, 0f, 0f, 430f, -60f, 0f, 370f, -80f, 0f, 200f, -80f, 0f, 80f, -80f, 0f, 25f, -50f, 0f };
        static readonly float[] P_neon = { 0f, 0f, 0f, 0f, 1000f, 0f, 0f, 1500f, 0f, -10f, 1550f, 0f, -50f, 1560f, 0f, -200f, 1560f, 0f, -260f, 1540f, 0f, -270f, 1500f, 0f, -270f, 1200f, 0f, -260f, 1150f, 0f, -230f, 1130f, 0f, -230f, 1090f, 0f, -270f, 1000f, 0f, -270f, 600f, 0f, -280f, 400f, 0f, -330f, 330f, 0f, -330f, 250f, 0f, -320f, 180f, 0f, -270f, 150f, 0f, -200f, 120f, 0f, -170f, 80f, 0f, -170f, 20f, 0f, -170f, -60f, 0f, -150f, -100f, 0f, -100f, -110f, 0f, -40f, -100f, 0f, -5f, -70f, 0f };
        static readonly float[] P_mountainroads = { 0f, 0f, 5f, 250f, 80f, 10f, 500f, 180f, 18f, 700f, 350f, 30f, 760f, 500f, 40f, 780f, 620f, 48f, 760f, 670f, 51f, 720f, 660f, 53f, 700f, 600f, 56f, 650f, 520f, 60f, 600f, 470f, 63f, 560f, 490f, 65f, 560f, 540f, 67f, 600f, 650f, 74f, 640f, 780f, 82f, 650f, 880f, 88f, 620f, 920f, 90f, 580f, 900f, 92f, 570f, 850f, 94f, 520f, 780f, 98f, 440f, 760f, 103f, 330f, 820f, 112f, 250f, 950f, 124f, 220f, 1100f, 135f, 260f, 1250f, 145f, 350f, 1350f, 150f, 500f, 1400f, 148f, 700f, 1380f, 140f, 900f, 1300f, 128f, 1050f, 1200f, 118f, 1150f, 1050f, 105f, 1300f, 950f, 92f, 1350f, 800f, 80f, 1300f, 600f, 64f, 1250f, 450f, 50f, 1150f, 350f, 40f, 1050f, 200f, 30f, 900f, 100f, 20f, 700f, -50f, 12f, 450f, -150f, 8f, 200f, -120f, 5f, 50f, -60f, 4f };
        static readonly float[] P_coastalhwy = { 0f, 0f, 0f, -50f, 600f, 0f, -200f, 1200f, 0f, -150f, 1800f, 0f, 0f, 2300f, 0f, -100f, 2900f, 0f, -50f, 3500f, 0f, 150f, 4000f, 0f, 500f, 4200f, 0f, 900f, 4100f, 0f, 1200f, 3700f, 0f, 1300f, 3100f, 0f, 1500f, 2500f, 0f, 1450f, 1800f, 0f, 1600f, 1200f, 0f, 1500f, 600f, 0f, 1200f, 100f, 0f, 800f, -200f, 0f, 400f, -200f, 0f };
        static readonly float[] P_deserthwy = { 0f, 0f, 0f, 0f, 2500f, 0f, 50f, 4000f, 0f, 400f, 4600f, 0f, 1200f, 4800f, 0f, 2500f, 4700f, 0f, 3500f, 4300f, 0f, 3900f, 3500f, 0f, 3950f, 2000f, 0f, 3800f, 800f, 0f, 3200f, 100f, 0f, 2000f, -150f, 0f, 800f, -150f, 0f };
        static readonly float[] P_countryroads = { 0f, 0f, 5f, 150f, 400f, 8f, 100f, 800f, 12f, 300f, 1100f, 18f, 600f, 1150f, 20f, 900f, 1000f, 16f, 1100f, 1200f, 14f, 1400f, 1250f, 12f, 1600f, 1000f, 10f, 1550f, 700f, 8f, 1700f, 400f, 12f, 1600f, 100f, 10f, 1300f, -100f, 8f, 1000f, 0f, 6f, 700f, -150f, 5f, 400f, -200f, 5f, 150f, -150f, 5f };
        static readonly float[] P_city = { 0f, 0f, 0f, 0f, 1170f, 0f, 8f, 1192f, 0f, 30f, 1200f, 0f, 770f, 1200f, 0f, 792f, 1192f, 0f, 800f, 1170f, 0f, 800f, 830f, 0f, 808f, 808f, 0f, 830f, 800f, 0f, 1370f, 800f, 0f, 1392f, 792f, 0f, 1400f, 770f, 0f, 1400f, 30f, 0f, 1392f, 8f, 0f, 1370f, 0f, 0f, 830f, 0f, 0f, 808f, -8f, 0f, 800f, -30f, 0f, 800f, -370f, 0f, 792f, -392f, 0f, 770f, -400f, 0f, 30f, -400f, 0f, 8f, -392f, 0f, 0f, -370f, 0f };
        static readonly float[] P_forestroads = { 0f, 0f, 5f, 200f, 350f, 12f, 100f, 700f, 20f, 300f, 1000f, 30f, 650f, 1100f, 38f, 900f, 900f, 40f, 1150f, 1000f, 35f, 1400f, 800f, 30f, 1350f, 450f, 22f, 1550f, 200f, 15f, 1300f, -100f, 10f, 900f, -50f, 8f, 600f, -250f, 6f, 250f, -200f, 5f };
        static readonly float[] P_snowpass = { 0f, 0f, 10f, 300f, 100f, 20f, 500f, 250f, 32f, 560f, 300f, 38f, 540f, 350f, 42f, 480f, 340f, 46f, 300f, 300f, 58f, 150f, 320f, 66f, 80f, 350f, 70f, 90f, 410f, 74f, 150f, 420f, 78f, 350f, 450f, 88f, 500f, 470f, 96f, 570f, 500f, 100f, 560f, 560f, 104f, 500f, 560f, 108f, 300f, 560f, 118f, 100f, 600f, 128f, -50f, 700f, 138f, -100f, 850f, 145f, -50f, 1000f, 148f, 100f, 1100f, 150f, 300f, 1200f, 140f, 500f, 1350f, 128f, 700f, 1300f, 118f, 900f, 1150f, 105f, 1000f, 900f, 92f, 1050f, 600f, 80f, 1000f, 300f, 62f, 900f, 50f, 45f, 750f, -150f, 32f, 500f, -250f, 22f, 250f, -200f, 15f, 80f, -100f, 11f };
        static readonly float[] P_tropicalroads = { 0f, 0f, 4f, 300f, -100f, 6f, 700f, -50f, 10f, 1000f, 200f, 15f, 1200f, 600f, 25f, 1150f, 1000f, 35f, 900f, 1300f, 30f, 500f, 1400f, 20f, 150f, 1300f, 12f, -150f, 1000f, 8f, -250f, 600f, 6f, -200f, 250f, 5f };
        static readonly float[] P_airport = { 0f, 100f, 0f, 0f, 1450f, 0f, 35f, 1565f, 0f, 120f, 1600f, 0f, 500f, 1600f, 0f, 585f, 1565f, 0f, 620f, 1450f, 0f, 620f, 1150f, 0f, 600f, 1100f, 0f, 640f, 1060f, 0f, 620f, 1010f, 0f, 620f, 150f, 0f, 585f, -15f, 0f, 500f, -50f, 0f, 120f, -50f, 0f, 35f, -15f, 0f };
        static readonly float[] P_island = { 0f, 0f, 0f, -300f, 800f, 0f, -200f, 1700f, 0f, 100f, 2500f, 0f, 700f, 3100f, 0f, 1500f, 3300f, 0f, 2300f, 3100f, 0f, 2900f, 2500f, 0f, 3200f, 1700f, 0f, 3100f, 900f, 0f, 3400f, 200f, 0f, 3000f, -500f, 0f, 2200f, -900f, 0f, 1400f, -800f, 0f, 800f, -1000f, 0f, 300f, -600f, 0f };
    }
}
