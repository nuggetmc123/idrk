using System.IO;
using System.Linq;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Veylix.EditorTools
{
    /// <summary>
    /// Menu: Veylix > Build Game World. Generates the textures, materials, terrain layers and network prefabs, and the
    /// one game scene (everything else, including all 25 maps, is built at runtime from code when you pick it). Runs
    /// by itself the first time the project is opened.
    /// </summary>
    public static class GameBuilder
    {
        public const string Root = "Assets/Veylix";
        public const string Gen = Root + "/Generated";
        public const string ScenePath = Root + "/Scenes/Veylix.unity";
        public const int WorldVersion = 7;
        const string VersionFile = "ProjectSettings/VeylixWorldVersion.txt";

        public static bool WorldUpToDate => File.Exists(VersionFile) && File.ReadAllText(VersionFile).Trim() == WorldVersion.ToString() && File.Exists(ScenePath);
        public static bool TextResourcesReady => Resources.Load<TMP_Settings>("TMP Settings") != null;

        [MenuItem("Veylix/Build Game World", priority = 20)]
        public static void Build()
        {
            if (!TextResourcesReady)
            {
                // Same path as first-time setup: add the text resources, then it builds by itself.
                VeylixAutoSetup.StartSetup();
                return;
            }
            if (!EditorUtility.DisplayDialog("Veylix", "This (re)creates the Veylix textures, materials, prefabs and the game scene.\nAny edits you made to those generated files will be replaced.", "Build it", "Cancel"))
                return;
            BuildWorld();
            EditorUtility.DisplayDialog("Veylix", ReadyMessage(), "Let's race");
        }

        static string buildNote = "";

        public static string ReadyMessage() =>
            "Veylix is ready. The scene shows your garage; press Play to drive.\n\n" + buildNote +
            "In VR (Quest Link / SteamVR): squeeze the grip on the steering wheel rim to hold it, right trigger = throttle, left trigger = brake, A shifts up, X/Y shift down, hold B = reset car, menu button = pause, hold both sticks = recenter. In your garage: left stick walks, right stick turns, point and pull the trigger on the stations.\n\n" +
            "No headset? In the garage WASD walks, Q/E turn, click the stations. Driving: W/A/S/D or arrows, E/Q shift, Space handbrake, C chase camera, hold right mouse to look, Esc pause, R (hold) reset car. A gamepad works too.\n\n" +
            (Online.Available ? "Online play is ON (set your Photon App ID with Veylix > Import Multiplayer)." : "Online play is off until you import Photon Fusion 2 (Veylix > Import Multiplayer). Everything else works offline.");

        public static void BuildWorld()
        {
            try
            {
                RenderSetup.Apply();
                EnsureFolder(Gen + "/Textures"); EnsureFolder(Gen + "/Materials"); EnsureFolder(Gen + "/Terrain"); EnsureFolder(Root + "/Prefabs"); EnsureFolder(Root + "/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);

                var go = new GameObject("Veylix");
                var assets = go.AddComponent<VXAssets>();
                Step("Painting textures (this takes a minute the first time)...", 0.05f);
                MakeAssets(assets);
                go.AddComponent<Game>();
                buildNote = "";
                Step("Building your garage...", 0.85f);
                try { BakeGarage(assets); }
                catch (System.Exception e)
                {
                    // The garage preview is only for looking at in the editor; the game itself doesn't need it.
                    Debug.LogWarning("[Veylix] Couldn't save the garage preview into the scene (the game still works, press Play): " + e.Message);
                    buildNote = "(The garage preview for the editor couldn't be saved: " + e.GetType().Name + ": " + e.Message + ". The game itself is fine. Please send this line to get it fixed.)\n\n";
                    Debug.LogException(e);
                    var leftover = GameObject.Find("Your Garage (editor preview)");
                    if (leftover != null) Object.DestroyImmediate(leftover);
                }
                var online = go.AddComponent<Online>();
                Step("Network prefabs...", 0.9f);
                online.netCarPrefab = NetPrefab<NetCar>("NetCar");
                online.netRacePrefab = NetPrefab<NetRace>("NetRace");
                RenderSettings.skybox = assets.skybox;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                LightmapSettings.lightmaps = new LightmapData[0];
                Lightmapping.bakedGI = false;
                Lightmapping.realtimeGI = false;
                AlwaysIncludeShaders();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                FusionBake.BakeAndSave(scene);
                var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                File.WriteAllText(VersionFile, WorldVersion.ToString());
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // ------------------------------------------------------------------ your garage, visible without Play

        public const string GarageFolder = Gen + "/Garage";

        /// <summary>
        /// The game builds everything from code when it runs. So the scene isn't empty in the editor, this builds your
        /// garage hub (room, stations, track wall, trophies and your car on the turntable) exactly as the game does,
        /// saves its meshes and materials in Generated/Garage, and keeps it in the scene. It's tagged EditorOnly and
        /// hides itself the moment you press Play, when the game builds the live version.
        /// </summary>
        static void BakeGarage(VXAssets assets)
        {
            AssetDatabase.DeleteAsset(Gen + "/Cars"); // the old car line-up
            AssetDatabase.DeleteAsset(GarageFolder);
            EnsureFolder(GarageFolder);
            VXAssets.UseInEditor(assets);
            VUI.ResetCache(); // use the saved fonts and a fresh panel material

            var go = new GameObject("Your Garage (editor preview)");
            go.tag = "EditorOnly";
            var room = go.AddComponent<Showroom>();
            room.BuildRoom();
            go.transform.position = Vector3.zero; // at the origin, where the Scene view looks when the scene opens
            room.ShowFigure(SaveData.Profile);
            var entrance = room.SpotPose(Showroom.Spot.Entrance); // read now: the Showroom script is removed below
            var turntable = room.turntable;

            // Your car on the turntable (model only: no physics, effects or sound in the editor).
            var carGo = new GameObject("Your Car");
            var rb = carGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var car = carGo.AddComponent<Car>();
            car.isPlayer = true;
            car.driverName = "You";
            car.carIndex = 0;
            car.Init(CarCatalog.Get(0), new CarSetup());
            var builtParts = CarModel.Build(car, false);
            if (builtParts.helmet != null) Object.DestroyImmediate(builtParts.helmet); // parked: nobody in it
            if (builtParts.driver != null) Object.DestroyImmediate(builtParts.driver.gameObject);
            foreach (var st in carGo.GetComponentsInChildren<StrutSet>(true)) st.Apply();
            carGo.transform.SetPositionAndRotation(turntable.position + Vector3.up * 0.07f, turntable.rotation * Quaternion.Euler(0f, 210f, 0f));
            carGo.transform.SetParent(turntable, true);

            // Keep only what you see. Game scripts come off (TextMeshPro text stays), colliders too.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && !(mb is TMPro.TMP_Text) && !(mb is Car) && mb.GetType().Namespace == "Veylix") Object.DestroyImmediate(mb);
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(rb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            go.AddComponent<EditorPreview>();

            // Save what was made in memory so it stays in the scene.
            string store = GarageFolder + "/garage_parts.asset";
            Object main = null;
            void Keep(Object o)
            {
                if (o == null || EditorUtility.IsPersistent(o)) return;
                if (main == null) { main = o; AssetDatabase.CreateAsset(o, store); }
                else AssetDatabase.AddObjectToAsset(o, store);
            }
            var block = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue; // TextMeshPro builds its own mesh
                // Menu panels colour themselves with property blocks, which a scene doesn't keep: bake them into materials.
                r.GetPropertyBlock(block);
                if (!block.isEmpty && r.sharedMaterial != null)
                {
                    var m = new Material(r.sharedMaterial) { name = r.name };
                    if (m.HasProperty("_Color")) m.color = block.GetColor("_Color");
                    var tex = block.GetTexture("_MainTex");
                    if (tex != null && m.HasProperty("_MainTex")) m.mainTexture = tex;
                    r.SetPropertyBlock(null);
                    r.sharedMaterial = m;
                }
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_MainTex")) Keep(m.mainTexture);
                    if (m.HasProperty("_BumpMap")) Keep(m.GetTexture("_BumpMap"));
                    Keep(m);
                }
                var f = r.GetComponent<MeshFilter>();
                if (f != null) Keep(f.sharedMesh);
            }
            AssetDatabase.SaveAssets();

            FrameGarage();
        }

        /// Point the Scene view at the garage preview (from the doorway, looking at the car).
        public static void FrameGarage()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null && SceneView.sceneViews.Count > 0) view = SceneView.sceneViews[0] as SceneView;
            if (view == null) return;
            view.LookAt(new Vector3(0f, 1.2f, 1.6f), Quaternion.Euler(14f, 0f, 0f), 7f);
            view.Repaint();
        }

        static void Step(string text, float p) => EditorUtility.DisplayProgressBar("Veylix", text, p);

        /// <summary>
        /// TextMeshPro needs its Essential Resources (settings, shaders, default font) before any text can be made.
        /// Import them straight from the package, without the import dialog. Works for Unity 6 (TextMeshPro lives in
        /// com.unity.ugui) and older versions (com.unity.textmeshpro).
        /// </summary>
        public static bool ImportTextMeshProEssentials()
        {
            foreach (var pkg in new[] { "Packages/com.unity.ugui", "Packages/com.unity.textmeshpro" })
            {
                string dir;
                try { dir = Path.GetFullPath(pkg); } catch { continue; }
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories))
                {
                    AssetDatabase.ImportPackage(file, false);
                    return true;
                }
            }
            // Fallback: TextMeshPro's own importer (method names differ between versions).
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("TMPro.TMP_PackageResourceImporter", false);
                if (type == null) continue;
                var method = type.GetMethod("ImportResources", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null || method.GetParameters().Length != 3) continue;
                method.Invoke(null, new object[] { true, false, false });
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ fonts

        public const string FontsFolder = Gen + "/Fonts";

        /// Saves TextMeshPro font assets for the game's fonts (made in memory they'd be lost from saved scenes).
        static TMP_FontAsset SaveFont(Font font, string name)
        {
            if (font == null) return null;
            EnsureFolder(FontsFolder);
            string path = FontsFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var fa = TMP_FontAsset.CreateFontAsset(font);
            if (fa == null) return null;
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            if (fa.atlasTextures != null)
                foreach (var t in fa.atlasTextures) if (t != null && !EditorUtility.IsPersistent(t)) { t.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(t, fa); }
            if (fa.material != null && !EditorUtility.IsPersistent(fa.material)) { fa.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ materials

        enum Mode { Opaque, Cutout, Fade, Transparent }

        static Material Load(string name, Shader shader)
        {
            string path = Gen + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.shaderKeywords = new string[0];
            return m;
        }

        static Material Std(string name, TexGen.Pair t, Color color, float smooth, float metal = 0f, Vector2? tiling = null, Mode mode = Mode.Opaque, Color? emission = null, float normalScale = 1f)
        {
            var m = Load(name, Shader.Find("Standard"));
            m.color = color;
            m.mainTexture = t.albedo;
            m.mainTextureScale = tiling ?? Vector2.one;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            if (t.normal != null)
            {
                m.SetTexture("_BumpMap", t.normal);
                m.SetFloat("_BumpScale", normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (t.emission != null || emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (t.emission != null) m.SetTexture("_EmissionMap", t.emission);
                m.SetColor("_EmissionColor", emission ?? Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            SetMode(m, mode);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Std(string name, Color color, float smooth, float metal = 0f) => Std(name, default(TexGen.Pair), color, smooth, metal);

        static void SetMode(Material m, Mode mode)
        {
            m.SetFloat("_Mode", (int)mode);
            switch (mode)
            {
                case Mode.Opaque:
                    m.SetOverrideTag("RenderType", ""); m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.Zero); m.SetInt("_ZWrite", 1);
                    m.renderQueue = -1; break;
                case Mode.Cutout:
                    m.SetOverrideTag("RenderType", "TransparentCutout"); m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.Zero); m.SetInt("_ZWrite", 1);
                    m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.45f); m.renderQueue = (int)RenderQueue.AlphaTest; break;
                case Mode.Fade:
                    m.SetOverrideTag("RenderType", "Transparent"); m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHABLEND_ON"); m.renderQueue = (int)RenderQueue.Transparent; break;
                default:
                    m.SetOverrideTag("RenderType", "Transparent"); m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.renderQueue = (int)RenderQueue.Transparent; break;
            }
        }

        static Material Shaded(string name, string shader, Texture tex, Color color)
        {
            var s = Shader.Find(shader) ?? Shader.Find("Standard");
            var m = Load(name, s);
            if (tex != null) m.mainTexture = tex;
            m.color = color;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Particle(string name, Texture tex, bool additive)
        {
            var s = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit");
            var m = Load(name, s);
            m.mainTexture = tex;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", additive ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Foliage(string name, Texture tex, Color tint, float wind)
        {
            var m = Shaded(name, "Veylix/Foliage", tex, tint);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.45f);
            if (m.HasProperty("_Wind")) m.SetFloat("_Wind", wind);
            return m;
        }

        static TerrainLayer Layer(string name, TexGen.Pair t, float tile, float smooth = 0.05f)
        {
            string path = Gen + "/Terrain/" + name + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            l.diffuseTexture = t.albedo;
            l.normalMapTexture = t.normal;
            l.tileSize = new Vector2(tile, tile);
            l.smoothness = smooth;
            l.metallic = 0f;
            l.normalScale = 1f;
            EditorUtility.SetDirty(l);
            return l;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        static void MakeAssets(VXAssets a)
        {
            // ---- car
            Step("Car materials...", 0.08f);
            var carbon = TexGen.Carbon();
            a.paint = Std("CarPaint", Color.white, 0.86f, 0.25f);
            a.carbon = Std("Carbon", carbon, Color.white, 0.72f, 0.1f, new Vector2(2f, 2f));
            a.satinBlack = Std("SatinBlack", new Color(0.05f, 0.05f, 0.055f), 0.38f);
            a.rimMetal = Std("Rim", new Color(0.05f, 0.05f, 0.05f), 0.72f, 0.85f);
            a.metal = Std("Metal", new Color(0.62f, 0.63f, 0.65f), 0.68f, 0.9f);
            a.interior = Std("Interior", new Color(0.035f, 0.035f, 0.04f), 0.2f);
            a.mirrorGlass = Std("MirrorGlass", new Color(0.35f, 0.4f, 0.45f), 0.97f, 0.9f);
            a.lightRed = Std("LightRed", default(TexGen.Pair), new Color(0.35f, 0.02f, 0.02f), 0.7f, 0f, null, Mode.Opaque, new Color(1f, 0.05f, 0.03f) * 2f);
            a.lightWhite = Std("LightWhite", default(TexGen.Pair), new Color(0.9f, 0.9f, 0.9f), 0.8f, 0f, null, Mode.Opaque, new Color(1f, 0.97f, 0.9f) * 2.5f);
            a.visor = Std("Visor", new Color(0.02f, 0.02f, 0.025f), 0.95f, 0.5f);
            a.brakeDisc = Std("BrakeDisc", TexGen.BrakeDisc(), new Color(0.6f, 0.6f, 0.6f), 0.35f, 0.3f);
            a.plank = Std("Plank", TexGen.Plank(), Color.white, 0.25f);
            a.padding = Std("Padding", new Color(0.12f, 0.2f, 0.45f), 0.15f);
            a.halo = Std("Accent", new Color(0.95f, 0.55f, 0.1f), 0.5f);
            a.screen = Shaded("Screen", "Veylix/UI", null, Color.white);
            a.tyres = new Material[5];
            for (int i = 0; i < 5; i++)
                a.tyres[i] = Std("Tyre" + i, new TexGen.Pair { albedo = TexGen.TyreSidewall(i, CarCatalog.CompoundColors[i]) }, Color.white, i >= 3 ? 0.3f : 0.22f);

            // ---- track
            Step("Track surfaces...", 0.2f);
            var asphalt = TexGen.Asphalt(true);
            a.asphalt = Std("Asphalt", asphalt, Color.white, 0.2f, 0f, Vector2.one, Mode.Opaque, null, 0.8f);
            var rubber = TexGen.RubberLine();
            a.asphalt.SetTexture("_DetailAlbedoMap", rubber);
            a.asphalt.SetFloat("_UVSec", 1f);
            a.asphalt.EnableKeyword("_DETAIL_MULX2");
            a.asphaltRoad = Std("AsphaltRoad", TexGen.Asphalt(false), Color.white, 0.16f, 0f, Vector2.one, Mode.Opaque, null, 0.9f);
            a.kerbRed = Std("KerbRed", TexGen.Kerb(new Color(0.78f, 0.08f, 0.06f), new Color(0.92f, 0.92f, 0.9f), "KerbRed"), Color.white, 0.32f);
            a.kerbBlue = Std("KerbBlue", TexGen.Kerb(new Color(0.08f, 0.22f, 0.6f), new Color(0.92f, 0.92f, 0.9f), "KerbBlue"), Color.white, 0.32f);
            a.kerbYellow = Std("KerbYellow", TexGen.Kerb(new Color(0.95f, 0.72f, 0.08f), new Color(0.08f, 0.08f, 0.08f), "KerbYellow"), Color.white, 0.32f);
            a.linePaint = Std("LinePaint", TexGen.LinePaint(), Color.white, 0.35f);
            a.runoff = Std("Runoff", TexGen.Runoff(), Color.white, 0.25f);
            a.concrete = Std("Concrete", TexGen.Concrete("Concrete", new Color(0.72f, 0.71f, 0.68f), false), Color.white, 0.15f);
            a.wallConcrete = Std("WallConcrete", TexGen.Concrete("WallConcrete", new Color(0.8f, 0.8f, 0.78f), true), Color.white, 0.18f);
            a.armco = Std("Armco", TexGen.Armco(), Color.white, 0.55f, 0.6f);
            a.tyreWall = Std("TyreWall", TexGen.TyreWall(), Color.white, 0.25f);
            a.fence = Std("Fence", new TexGen.Pair { albedo = TexGen.Fence() }, Color.white, 0.4f, 0.5f, new Vector2(1f, 1f), Mode.Cutout);
            a.fence.SetInt("_Cull", 0);
            a.gravelTrap = Std("GravelTrap", TexGen.Gravel(false), Color.white, 0.08f);
            a.sandTrap = Std("SandTrap", TexGen.Gravel(true), Color.white, 0.08f);
            var verge = TexGen.Grass("GrassVerge", Hex("4E6B2E"), Hex("6B7F35"), 301);
            a.grassVerge = Std("GrassVerge", verge, Color.white, 0.12f);
            a.pitLane = Std("PitLane", asphalt, new Color(0.85f, 0.85f, 0.88f), 0.25f);
            a.flag = Std("Chequer", new TexGen.Pair { albedo = TexGen.Chequer() }, Color.white, 0.3f);
            a.tyreMarks = Particle("TyreMarks", TexGen.Particle("SkidSoft", 3), false);
            a.tyreMarks.SetColor("_TintColor", new Color(0.02f, 0.02f, 0.02f, 0.35f));
            a.sidewalk = Std("Sidewalk", TexGen.Pavers(), Color.white, 0.2f);
            a.runway = Std("Runway", TexGen.Runway(), Color.white, 0.18f);
            a.gridMarks = a.linePaint;

            // ---- terrain layers
            Step("Terrain layers...", 0.38f);
            a.terrainLayers = new[]
            {
                Layer("Grass", TexGen.Grass("TGrass", Hex("47652A"), Hex("6A7B33"), 311), 6f),
                Layer("DryGrass", TexGen.Grass("TDryGrass", Hex("8C8048"), Hex("A39257"), 321), 6f),
                Layer("Dirt", TexGen.Dirt("TDirt", Hex("7A6047"), 331, 0.6f), 5f),
                Layer("Rock", TexGen.Rock("TRock", Hex("7B776F"), 341), 10f, 0.12f),
                Layer("Sand", TexGen.Dirt("TSand", Hex("CDB48A"), 351, 0.1f), 6f),
                Layer("Snow", TexGen.Snow(), 8f, 0.35f),
                Layer("Gravel", TexGen.Gravel(false), 3f),
                Layer("Forest", TexGen.ForestFloor(), 5f),
                Layer("Tropical", TexGen.Grass("TTropical", Hex("3C6B26"), Hex("5A8A2E"), 361), 6f),
                Layer("Mud", TexGen.Dirt("TMud", Hex("5A4433"), 371, 0.3f), 5f),
            };

            // ---- vegetation
            Step("Trees and plants...", 0.5f);
            a.bark = Std("Bark", TexGen.Bark("Bark", Hex("5A4636"), false, false), Color.white, 0.12f);
            a.palmBark = Std("PalmBark", TexGen.Bark("PalmBark", Hex("8A7558"), true, false), Color.white, 0.12f);
            a.leavesConifer = Foliage("LeavesConifer", TexGen.Foliage("FoliageConifer", 0, Hex("2F4A2A"), 401), Color.white, 0.15f);
            a.leavesBroad = Foliage("LeavesBroad", TexGen.Foliage("FoliageBroad", 1, Hex("4D6B2C"), 402), Color.white, 0.3f);
            a.leavesPalm = Foliage("LeavesPalm", TexGen.Foliage("FoliagePalm", 2, Hex("4E7A2F"), 403), Color.white, 0.45f);
            a.leavesBirch = Foliage("LeavesBirch", TexGen.Foliage("FoliageBirch", 3, Hex("7A9440"), 404), Color.white, 0.35f);
            a.bush = Foliage("Bush", TexGen.Foliage("FoliageBush", 4, Hex("4A6230"), 405), Color.white, 0.2f);
            a.snowCap = Foliage("LeavesSnow", TexGen.Foliage("FoliageSnow", 0, Hex("C8D2D6"), 406), Color.white, 0.1f);
            a.rock = Std("Rock", TexGen.Rock("Boulder", Hex("8A857C"), 411), Color.white, 0.15f);
            a.cactus = Std("Cactus", TexGen.Bark("Cactus", Hex("4E6B3A"), true, false), Color.white, 0.25f);

            // ---- buildings and scenery
            Step("Buildings and scenery...", 0.62f);
            a.buildingStucco = Std("FacadeStucco", TexGen.Facade("FacadeStucco", 0, 501), Color.white, 0.25f, 0f, Vector2.one, Mode.Opaque, Color.black);
            a.buildingOffice = Std("FacadeOffice", TexGen.Facade("FacadeOffice", 1, 502), Color.white, 0.75f, 0.3f, Vector2.one, Mode.Opaque, Color.black);
            a.buildingBrick = Std("FacadeBrick", TexGen.Facade("FacadeBrick", 2, 503), Color.white, 0.2f, 0f, Vector2.one, Mode.Opaque, Color.black);
            a.buildingIndustrial = Std("FacadeIndustrial", TexGen.Facade("FacadeIndustrial", 3, 504), Color.white, 0.35f, 0.2f, Vector2.one, Mode.Opaque, Color.black);
            a.buildingWarm = Std("FacadeVillage", TexGen.Facade("FacadeVillage", 4, 505), Color.white, 0.2f, 0f, Vector2.one, Mode.Opaque, Color.black);
            a.buildingNight = a.buildingOffice;
            a.roof = Std("Roof", TexGen.Roof(false), Color.white, 0.15f);
            a.roofTiles = Std("RoofTiles", TexGen.Roof(true), Color.white, 0.25f);
            a.glass = Std("Glass", new Color(0.08f, 0.1f, 0.12f), 0.95f, 0.6f);
            a.grandstand = Std("Grandstand", TexGen.Concrete("Stand", new Color(0.7f, 0.72f, 0.75f), false), Color.white, 0.3f, 0.2f);
            a.crowd = Std("Crowd", new TexGen.Pair { albedo = TexGen.Crowd() }, Color.white, 0.1f, 0f, Vector2.one, Mode.Cutout);
            a.ads = Std("Ads", new TexGen.Pair { albedo = TexGen.Ads() }, Color.white, 0.4f);
            a.signs = Std("Signs", new TexGen.Pair { albedo = TexGen.Signs() }, Color.white, 0.3f);
            a.gantry = Std("Gantry", new Color(0.18f, 0.18f, 0.2f), 0.45f, 0.7f);
            a.lampHead = Std("LampHead", default(TexGen.Pair), new Color(0.9f, 0.9f, 0.88f), 0.6f, 0f, null, Mode.Opaque, new Color(0.2f, 0.2f, 0.2f));
            var waves = TexGen.Water();
            a.water = Shaded("Water", "Veylix/Water", null, new Color(0.03f, 0.16f, 0.22f));
            a.water.SetTexture("_BumpMap", waves.normal);
            a.mountainFar = Std("FarHills", new Color(0.36f, 0.4f, 0.44f), 0.05f);
            a.boat = Std("Boat", new Color(0.95f, 0.95f, 0.95f), 0.8f);
            a.container = Std("Containers", TexGen.Containers(), Color.white, 0.4f, 0.3f);
            a.neonA = Std("NeonA", new TexGen.Pair { albedo = TexGen.Neon(1) }, Color.white, 0.6f, 0f, Vector2.one, Mode.Opaque, Color.white * 1.6f);
            a.neonA.SetTexture("_EmissionMap", a.neonA.mainTexture);
            a.neonB = Std("NeonB", new TexGen.Pair { albedo = TexGen.Neon(2) }, Color.white, 0.6f, 0f, Vector2.one, Mode.Opaque, Color.white * 1.6f);
            a.neonB.SetTexture("_EmissionMap", a.neonB.mainTexture);
            a.neonC = Std("NeonC", new TexGen.Pair { albedo = TexGen.Neon(3) }, Color.white, 0.6f, 0f, Vector2.one, Mode.Opaque, Color.white * 1.6f);
            a.neonC.SetTexture("_EmissionMap", a.neonC.mainTexture);
            a.road = a.asphaltRoad;
            a.roadLines = a.linePaint;
            a.hangar = a.buildingIndustrial;
            a.tent = Std("Tent", new Color(0.92f, 0.92f, 0.9f), 0.3f);
            a.foam = Std("Foam", new Color(0.9f, 0.92f, 0.94f), 0.4f);
            a.cloud = a.foam;
            a.stars = a.lightWhite;

            // ---- sky
            // Our sky shader (referenced here so it's always in builds); Unity's procedural sky if it can't be found.
            var skyShader = Shader.Find("Veylix/Sky");
            var sky = Load("Sky", skyShader != null && skyShader.isSupported ? skyShader : Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);
            a.skybox = sky;

            // ---- effects
            Step("Effects...", 0.78f);
            a.smoke = Particle("Smoke", TexGen.Particle("PSmoke", 0), false);
            a.fire = Particle("Fire", TexGen.Particle("PFire", 1), true);
            a.spark = Particle("Spark", TexGen.Particle("PSpark", 2), true);
            a.dust = Particle("Dust", TexGen.Particle("PDust", 0), false);
            a.rain = Particle("Rain", TexGen.Particle("PRain", 3), false);
            a.rain.SetColor("_TintColor", new Color(0.55f, 0.58f, 0.62f, 0.35f));
            a.spray = Particle("Spray", TexGen.Particle("PSpray", 0), false);
            a.glow = Particle("Glow", TexGen.Particle("PGlow", 2), true);
            a.flash = a.glow;
            a.debris = Particle("Debris", TexGen.Particle("PDebris", 5), false);
            a.lightCone = Particle("LightPool", TexGen.Particle("PPool", 4), true);
            a.lightCone.SetColor("_TintColor", new Color(0.55f, 0.52f, 0.45f, 0.35f));

            // ---- UI
            Step("Interface...", 0.86f);
            a.uiPanel = Shaded("UIPanel", "Veylix/UI", null, Color.white);
            a.uiPanelAdditive = a.uiPanel;
            a.uiIcon = Shaded("UIOverlay", "Veylix/Overlay", null, Color.white);
            a.displayFont = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold");
            a.bodyFont = Resources.Load<Font>("Fonts/BarlowCondensed-Medium");
            a.displayText = SaveFont(a.displayFont, "Veylix Display");
            a.bodyText = SaveFont(a.bodyFont, "Veylix Body");
            a.mapPreviews = new Texture2D[MapCatalog.Count];
            for (int i = 0; i < MapCatalog.Count; i++) a.mapPreviews[i] = TexGen.MapPreview(MapCatalog.Get(i));
            EditorUtility.SetDirty(a);
        }

        static NetworkObject NetPrefab<T>(string name) where T : Component
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var go = new GameObject(name);
            go.AddComponent<NetworkObject>();
            go.AddComponent<T>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab != null ? prefab.GetComponent<NetworkObject>() : null;
        }

        /// <summary>Shaders the game finds by name at runtime must be in the build.</summary>
        static void AlwaysIncludeShaders()
        {
            var names = new[] { "Veylix/UI", "Veylix/Overlay", "Veylix/Ghost", "Veylix/Water", "Veylix/Foliage", "Standard", "Skybox/Procedural", "Unlit/Texture", "Sprites/Default", "Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Alpha Blended" };
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs == null || gs.Length == 0) return;
            var so = new SerializedObject(gs[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
                if (has) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
