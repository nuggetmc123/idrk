using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Menu: KartVR > Build Prototype World.
    /// Generates textures, materials, the Kart and Player prefabs and a complete
    /// playable scene: a Hub plaza with garages and portals, a Race track, a
    /// Bumper arena and a Free Roam stunt park. Everything is made from simple
    /// shapes so it runs well on Quest; swap in real models whenever you like.
    /// </summary>
    public static class KartWorldBuilder
    {
        const string Root = "Assets/KartVR";
        const string ScenePath = Root + "/Scenes/KartWorld.unity";
        const string KartPrefabPath = Root + "/Prefabs/Kart.prefab";
        const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";
        const string RacerModelPath = Root + "/Models/Racer.fbx";

        static readonly Vector3 RaceOrigin = new Vector3(0f, 0f, 320f);
        static readonly Vector3 BumperOrigin = new Vector3(320f, 0f, 0f);
        static readonly Vector3 FreeRoamOrigin = new Vector3(-380f, 0f, 0f);

        // Materials (created as assets so the scene can reference them).
        static Material white, dark, metal, chrome, plaza, garageFloor, hubGrass, raceGrass, freeGrass, asphalt, lineWhite,
            wallRed, wallWhite, stone, water, wood, leaves, leavesDark, bark, sign, glass, boost, arena, lava, rampMat,
            rampBlue, rampRed, portalRace, portalBumper, portalFree, glowRace, glowBumper, glowFree, neonWhite, neonPink,
            neonCyan, lampGlow, checker, hazard, tire, crowdBase, particle, baseLit, rock, sand;
        static Material[] crowd;

        public const string WorldScenePath = ScenePath;

        public static bool TextResourcesReady => Resources.Load<TMP_Settings>("TMP Settings") != null;

        [MenuItem("KartVR/Build Prototype World")]
        public static void Build()
        {
            if (!TextResourcesReady)
            {
                // The text system needs its basic resources once per project. Import them for the user.
                if (ImportTextMeshProEssentials())
                    EditorUtility.DisplayDialog("KartVR",
                        "Text resources are being added to the project (click Import if Unity asks).\n\nWhen that's done, click KartVR > Build Prototype World again.",
                        "OK");
                else
                    EditorUtility.DisplayDialog("KartVR",
                        "TextMeshPro resources are missing.\n\nGo to Window > TextMeshPro > Import TMP Essential Resources, then run this again.",
                        "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("KartVR",
                    "This will (re)create the KartVR textures, materials, prefabs and the KartWorld scene.\nAny edits you made to those files will be replaced.",
                    "Build it", "Cancel"))
                return;

            BuildWorld();

            EditorUtility.DisplayDialog("KartVR",
                "Done! The KartWorld scene is open. Press Play!\n\n" +
                "No headset? W/A/S/D to walk or drive, Q/E to turn, hold right mouse to look, left click to press buttons.\n\n" +
                (KartVR.SessionManager.MultiplayerAvailable
                    ? "Multiplayer is ON (make sure your Photon App ID is set in Tools > Fusion > Fusion Hub)."
                    : "You're in OFFLINE solo mode. Import Photon Fusion 2 to play online."),
                "Let's go");
        }

        /// <summary>Builds everything with no questions asked (used by the menu and by first-time auto setup).</summary>
        public static void BuildWorld()
        {
            EnsureFolder(Root + "/Textures");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Scenes");
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam) Object.DestroyImmediate(defaultCam);
            SetUpLighting();

            // KartUI builds kart/avatar parts at runtime from these.
            KartUI.BaseMaterial = baseLit;
            KartUI.ParticleMaterial = particle;
            var racerModel = PrepareRacerModel();
            KartUI.RacerModel = racerModel;

            var kartPrefab = BuildKartPrefab();
            var playerPrefab = BuildPlayerPrefab();

            BuildManagers(kartPrefab, playerPrefab, racerModel);
            BuildRig();
            MarkStatic(BuildHub());
            MarkStatic(BuildRaceTrack());
            MarkStatic(BuildBumperArena());
            MarkStatic(BuildFreeRoam());

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        // ================================================================== helpers

        /// <summary>Runs Window > TextMeshPro > Import TMP Essential Resources. Returns false if that isn't available.</summary>
        public static bool ImportTextMeshProEssentials()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("TMPro.TMP_PackageResourceImporter", false);
                if (type == null) continue;
                var method = type.GetMethod("ImportResources", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null || method.GetParameters().Length != 3) return false;
                method.Invoke(null, new object[] { true, false, false });
                return true;
            }
            return false;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Shader LitShader()
        {
            var s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            return s;
        }

        static Material Mat(string name, Color c, float smoothness = 0.2f, float metallic = 0f, Texture2D tex = null,
            Vector2? tiling = null, Color? emission = null)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTextureScale = tiling ?? Vector2.one;
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Make a Standard-shader material see-through (for portal surfaces and glass).</summary>
        static Material MakeTransparent(Material m, float alpha)
        {
            var c = m.color;
            c.a = alpha;
            m.color = c;
            if (m.HasProperty("_Mode"))
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        delegate Color Pixel(float u, float v, System.Random rng);

        /// <summary>Paint a texture procedurally, save it as a PNG asset and return the imported texture.</summary>
        static Texture2D Tex(string name, int size, Pixel pixel, bool repeat = true)
        {
            string path = Root + "/Textures/" + name + ".png";
            var rng = new System.Random(name.GetHashCode());
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = pixel(x / (float)size, y / (float)size, rng);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.alphaIsTransparency = !repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Noise(float u, float v, float scale, float seed)
        {
            // Tileable-ish layered noise.
            return Mathf.PerlinNoise(u * scale + seed, v * scale + seed * 0.7f) * 0.65f +
                   Mathf.PerlinNoise(u * scale * 3f + seed, v * scale * 3f) * 0.35f;
        }

        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        static void CreateMaterials()
        {
            var asphaltTex = Tex("Asphalt", 256, (u, v, r) =>
            {
                float n = Noise(u, v, 18f, 3f);
                float speck = r.NextDouble() < 0.04 ? 0.15f : 0f;
                return Shade(new Color(0.3f, 0.3f, 0.33f), 0.8f + n * 0.3f + speck);
            });
            var grassTex = Tex("Grass", 256, (u, v, r) =>
            {
                float n = Noise(u, v, 8f, 11f);
                float blade = (float)r.NextDouble() * 0.12f;
                return Color.Lerp(new Color(0.25f, 0.5f, 0.18f), new Color(0.42f, 0.7f, 0.25f), n) * (0.94f + blade);
            });
            var tilesTex = Tex("PlazaTiles", 256, (u, v, r) =>
            {
                float gx = (u * 4f) % 1f, gy = (v * 4f) % 1f;
                bool grout = gx < 0.03f || gy < 0.03f;
                int tile = (int)(u * 4f) + (int)(v * 4f) * 4;
                float tint = 0.92f + ((tile * 37) % 10) * 0.012f;
                float n = Noise(u, v, 20f, 5f) * 0.08f;
                return grout ? new Color(0.55f, 0.53f, 0.5f) : Shade(new Color(0.86f, 0.83f, 0.78f), tint + n);
            });
            var garageTex = Tex("GarageFloor", 128, (u, v, r) =>
            {
                float gx = (u * 2f) % 1f, gy = (v * 2f) % 1f;
                bool seam = gx < 0.02f || gy < 0.02f;
                return seam ? new Color(0.18f, 0.18f, 0.2f) : Shade(new Color(0.32f, 0.33f, 0.36f), 0.95f + Noise(u, v, 10f, 2f) * 0.1f);
            });
            var arenaTex = Tex("Arena", 256, (u, v, r) =>
            {
                float gx = (u * 8f) % 1f, gy = (v * 8f) % 1f;
                bool line = gx < 0.04f || gy < 0.04f;
                return line ? new Color(0.35f, 0.5f, 1f) : new Color(0.13f, 0.14f, 0.24f);
            });
            var checkerTex = Tex("Checker", 64, (u, v, r) => (((int)(u * 8f) + (int)(v * 8f)) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.08f));
            var hazardTex = Tex("Hazard", 64, (u, v, r) => ((u + v) * 4f % 1f) < 0.5f ? new Color(1f, 0.78f, 0.1f) : new Color(0.08f, 0.08f, 0.08f));
            var lavaTex = Tex("Lava", 256, (u, v, r) =>
            {
                float n = Noise(u, v, 6f, 9f);
                return Color.Lerp(new Color(0.6f, 0.05f, 0.0f), new Color(1f, 0.75f, 0.1f), Mathf.Pow(n, 2.2f) * 1.6f);
            });
            var waterTex = Tex("Water", 128, (u, v, r) =>
                Color.Lerp(new Color(0.1f, 0.35f, 0.6f), new Color(0.35f, 0.7f, 0.9f), Noise(u, v, 6f, 4f)));
            var stoneTex = Tex("Stone", 128, (u, v, r) => Shade(new Color(0.72f, 0.7f, 0.66f), 0.85f + Noise(u, v, 12f, 8f) * 0.25f));
            var woodTex = Tex("Wood", 128, (u, v, r) =>
                Shade(new Color(0.55f, 0.38f, 0.22f), 0.85f + 0.15f * Mathf.Sin(v * 60f + Noise(u, v, 4f, 1f) * 6f)));
            var glowTex = Tex("SoftGlow", 64, (u, v, r) =>
            {
                float d = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
                return new Color(1f, 1f, 1f, d * d);
            }, false);

            baseLit = Mat("BaseLit", Color.white, 0.3f);
            white = Mat("White", Color.white, 0.4f);
            dark = Mat("Dark", new Color(0.12f, 0.12f, 0.14f), 0.35f);
            metal = Mat("Metal", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.8f);
            chrome = Mat("Chrome", new Color(0.85f, 0.87f, 0.9f), 0.92f, 1f);
            plaza = Mat("Plaza", Color.white, 0.25f, 0f, tilesTex, new Vector2(30f, 30f));
            garageFloor = Mat("GarageFloor", Color.white, 0.55f, 0.1f, garageTex, new Vector2(4f, 5f));
            hubGrass = Mat("HubGrass", Color.white, 0.05f, 0f, grassTex, new Vector2(8f, 8f));
            raceGrass = Mat("RaceGrass", Color.white, 0.05f, 0f, grassTex, new Vector2(60f, 45f));
            freeGrass = Mat("FreeGrass", Color.white, 0.05f, 0f, grassTex, new Vector2(80f, 80f));
            asphalt = Mat("Asphalt", Color.white, 0.3f, 0f, asphaltTex, new Vector2(4f, 1f));
            lineWhite = Mat("Line", new Color(0.95f, 0.95f, 0.95f), 0.3f);
            wallRed = Mat("WallRed", new Color(0.85f, 0.12f, 0.12f), 0.5f);
            wallWhite = Mat("WallWhite", new Color(0.95f, 0.95f, 0.95f), 0.5f);
            stone = Mat("Stone", Color.white, 0.2f, 0f, stoneTex, new Vector2(2f, 2f));
            water = Mat("Water", Color.white, 0.95f, 0.1f, waterTex, new Vector2(3f, 3f));
            wood = Mat("Wood", Color.white, 0.25f, 0f, woodTex);
            leaves = Mat("Leaves", new Color(0.25f, 0.6f, 0.22f), 0.1f);
            leavesDark = Mat("LeavesDark", new Color(0.16f, 0.45f, 0.18f), 0.1f);
            bark = Mat("Bark", new Color(0.4f, 0.27f, 0.16f), 0.1f);
            rock = Mat("Rock", new Color(0.5f, 0.5f, 0.52f), 0.15f, 0f, stoneTex);
            sand = Mat("Sand", new Color(0.9f, 0.82f, 0.6f), 0.1f);
            sign = Mat("Sign", new Color(0.09f, 0.1f, 0.14f), 0.6f);
            glass = MakeTransparent(Mat("Glass", new Color(0.6f, 0.8f, 1f), 0.95f, 0.2f), 0.25f);
            boost = Mat("Boost", new Color(1f, 0.55f, 0f), 0.6f, 0f, null, null, new Color(1f, 0.45f, 0f) * 1.5f);
            arena = Mat("Arena", Color.white, 0.6f, 0.2f, arenaTex, new Vector2(4f, 4f));
            lava = Mat("Lava", Color.white, 0.4f, 0f, lavaTex, new Vector2(6f, 6f), new Color(1f, 0.35f, 0.05f));
            rampMat = Mat("Ramp", new Color(1f, 0.8f, 0.2f), 0.4f, 0f, hazardTex, new Vector2(4f, 6f));
            rampBlue = Mat("RampBlue", new Color(0.25f, 0.55f, 1f), 0.5f);
            rampRed = Mat("RampRed", new Color(1f, 0.3f, 0.3f), 0.5f);
            portalRace = MakeTransparent(Mat("PortalRace", new Color(1f, 0.4f, 0.2f), 0.9f, 0f, null, null, new Color(1f, 0.35f, 0.15f) * 0.8f), 0.55f);
            portalBumper = MakeTransparent(Mat("PortalBumper", new Color(0.3f, 0.6f, 1f), 0.9f, 0f, null, null, new Color(0.25f, 0.5f, 1f) * 0.8f), 0.55f);
            portalFree = MakeTransparent(Mat("PortalFreeRoam", new Color(0.4f, 1f, 0.45f), 0.9f, 0f, null, null, new Color(0.3f, 0.9f, 0.35f) * 0.8f), 0.55f);
            glowRace = Mat("GlowRace", new Color(1f, 0.4f, 0.2f), 0.5f, 0f, null, null, new Color(1f, 0.4f, 0.15f) * 2f);
            glowBumper = Mat("GlowBumper", new Color(0.3f, 0.6f, 1f), 0.5f, 0f, null, null, new Color(0.25f, 0.55f, 1f) * 2f);
            glowFree = Mat("GlowFreeRoam", new Color(0.4f, 1f, 0.45f), 0.5f, 0f, null, null, new Color(0.3f, 1f, 0.35f) * 2f);
            neonWhite = Mat("NeonWhite", Color.white, 0.5f, 0f, null, null, Color.white * 1.5f);
            neonPink = Mat("NeonPink", new Color(1f, 0.3f, 0.8f), 0.5f, 0f, null, null, new Color(1f, 0.25f, 0.75f) * 2f);
            neonCyan = Mat("NeonCyan", new Color(0.2f, 0.95f, 1f), 0.5f, 0f, null, null, new Color(0.2f, 0.9f, 1f) * 2f);
            lampGlow = Mat("LampGlow", new Color(1f, 0.92f, 0.7f), 0.5f, 0f, null, null, new Color(1f, 0.85f, 0.55f) * 2f);
            checker = Mat("Checker", Color.white, 0.4f, 0f, checkerTex, new Vector2(8f, 1f));
            hazard = Mat("Hazard", Color.white, 0.4f, 0f, hazardTex, new Vector2(6f, 1f));
            tire = Mat("Tire", new Color(0.08f, 0.08f, 0.09f), 0.15f);
            crowdBase = Mat("CrowdSkin", new Color(0.95f, 0.78f, 0.62f), 0.2f);
            crowd = PlayerProfile.Palette.Select((c, i) => Mat("Crowd" + i, c, 0.3f)).ToArray();

            var particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            string ppath = Root + "/Materials/ParticleGlow.mat";
            particle = AssetDatabase.LoadAssetAtPath<Material>(ppath);
            if (particle == null)
            {
                particle = new Material(particleShader);
                AssetDatabase.CreateAsset(particle, ppath);
            }
            particle.shader = particleShader;
            particle.mainTexture = glowTex;
            if (particle.HasProperty("_TintColor")) particle.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(particle);
        }

        static void SetUpLighting()
        {
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                var sun = sunGo.GetComponent<Light>();
                sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.7f;
                RenderSettings.sun = sun;
            }

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                string path = Root + "/Materials/Sky.mat";
                var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (sky == null)
                {
                    sky = new Material(skyShader);
                    AssetDatabase.CreateAsset(sky, path);
                }
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.9f));
                sky.SetColor("_GroundColor", new Color(0.4f, 0.45f, 0.4f));
                sky.SetFloat("_Exposure", 1.25f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.25f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 520f;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.94f);
            QualitySettings.shadowDistance = 90f;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
            Material mat, bool collider = true, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return go;
        }

        static TextMeshPro Text(Transform parent, string name, string text, Vector3 localPos, Quaternion rot,
            float fontSize, Color color, Vector2 rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = rect;
            return tmp;
        }

        static Transform Group(string name, Transform parent, Vector3 localPos, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            return go.transform;
        }

        /// <summary>A sign board with text on its -Z face (readable when you look toward +Z), on two posts.</summary>
        static TextMeshPro Board(Transform parent, string name, Vector3 localPos, Quaternion rot, Vector2 size, float fontSize, string text, Material frame = null)
        {
            var b = Group(name, parent, localPos, rot);
            Prim(PrimitiveType.Cube, "Backing", b, new Vector3(0f, 0f, 0.2f), new Vector3(size.x, size.y, 0.3f), sign);
            var trim = frame ?? neonCyan;
            Prim(PrimitiveType.Cube, "TrimTop", b, new Vector3(0f, size.y * 0.5f, 0.05f), new Vector3(size.x + 0.2f, 0.12f, 0.12f), trim, false);
            Prim(PrimitiveType.Cube, "TrimBottom", b, new Vector3(0f, -size.y * 0.5f, 0.05f), new Vector3(size.x + 0.2f, 0.12f, 0.12f), trim, false);
            if (localPos.y - size.y * 0.5f > 0.5f)
                foreach (float x in new[] { -size.x * 0.35f, size.x * 0.35f })
                {
                    float h = localPos.y - size.y * 0.5f;
                    Prim(PrimitiveType.Cube, "Post", b, new Vector3(x, -size.y * 0.5f - h * 0.5f, 0.3f), new Vector3(0.3f, h, 0.3f), metal);
                }
            return Text(b, "Text", text, new Vector3(0f, 0f, 0.02f), Quaternion.identity, fontSize, Color.white, size * 0.92f);
        }

        static void Tree(Transform parent, Vector3 pos, float s, System.Random rng)
        {
            var t = Group("Tree", parent, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 1.6f * s, 0f), new Vector3(0.45f * s, 1.6f * s, 0.45f * s), bark);
            bool pine = rng.NextDouble() < 0.4;
            if (pine)
            {
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Sphere, "Needles", t, new Vector3(0f, (3.2f + i * 1.3f) * s, 0f),
                        new Vector3((3.2f - i * 0.9f) * s, 1.6f * s, (3.2f - i * 0.9f) * s), leavesDark, false);
            }
            else
            {
                Prim(PrimitiveType.Sphere, "Leaves", t, new Vector3(0f, 4.0f * s, 0f), Vector3.one * 3.6f * s, leaves, false);
                Prim(PrimitiveType.Sphere, "Leaves", t, new Vector3(0.9f * s, 3.4f * s, 0.4f * s), Vector3.one * 2.4f * s, leavesDark, false);
                Prim(PrimitiveType.Sphere, "Leaves", t, new Vector3(-0.7f * s, 3.6f * s, -0.6f * s), Vector3.one * 2.6f * s, leaves, false);
            }
        }

        static void Lamp(Transform parent, Vector3 pos)
        {
            var l = Group("Lamp", parent, pos);
            Prim(PrimitiveType.Cylinder, "Pole", l, new Vector3(0f, 2.2f, 0f), new Vector3(0.15f, 2.2f, 0.15f), dark);
            Prim(PrimitiveType.Cylinder, "Base", l, new Vector3(0f, 0.15f, 0f), new Vector3(0.45f, 0.15f, 0.45f), dark);
            Prim(PrimitiveType.Sphere, "Bulb", l, new Vector3(0f, 4.6f, 0f), Vector3.one * 0.55f, lampGlow, false);
            Prim(PrimitiveType.Cylinder, "Cap", l, new Vector3(0f, 4.95f, 0f), new Vector3(0.7f, 0.05f, 0.7f), dark, false);
        }

        /// <summary>A ring made of small boxes (portal frames, boost hoops). It lies in the local XY plane.</summary>
        static Transform Hoop(Transform parent, string name, Vector3 pos, Quaternion rot, float radius, float thickness, Material mat, int segments = 20)
        {
            var g = Group(name, parent, pos, rot);
            float seg = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                var p = Quaternion.Euler(0f, 0f, a) * new Vector3(0f, radius, 0f);
                Prim(PrimitiveType.Cube, "Seg", g, p, new Vector3(seg, thickness, thickness), mat, false, Quaternion.Euler(0f, 0f, a));
            }
            return g;
        }

        /// <summary>Mark scenery static so Unity batches it into a few draw calls (important on Quest). Moving parts are skipped.</summary>
        static void MarkStatic(Transform root)
        {
            // Skip anything that moves, changes, or holds live text.
            if (root.GetComponent<Rotator>() || root.name == "Turntable" || root.GetComponent<TextMeshPro>() ||
                root.GetComponent<GarageEditor>() || root.GetComponent<KeypadTerminal>() || root.GetComponent<StartLights>() ||
                root.GetComponent<InfoBoard>())
                return;
            GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Transform child in root) MarkStatic(child);
        }

        // ================================================================== prefabs

        static GameObject BuildKartPrefab()
        {
            var root = new GameObject("Kart");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 150f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(1.2f, 0.5f, 1.9f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<KartVisuals>();   // builds the detailed model at runtime from the player's look
            root.AddComponent<KartController>();
            root.AddComponent<KartAvatar>();     // the driver's body
            root.AddComponent<KartDashboard>();

            var t = root.transform;
            Group("Seat", t, new Vector3(0f, 0.45f, -0.3f));
            Group("HeadAnchor", t, new Vector3(0f, 1.15f, -0.3f));
            Text(t, "Dashboard", "", new Vector3(0f, 0.92f, 0.78f), Quaternion.Euler(28f, 0f, 0f), 0.35f, Color.white, new Vector2(0.75f, 0.4f));
            Text(t, "BigDisplay", "", new Vector3(0f, 1.5f, 2.4f), Quaternion.identity, 5f, new Color(1f, 0.85f, 0.2f), new Vector2(2.5f, 0.8f));
            Text(t, "NameTag", "Racer", new Vector3(0f, 1.9f, -0.3f), Quaternion.identity, 2.5f, Color.white, new Vector2(3f, 0.5f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, KartPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            root.AddComponent<NetworkObject>();
            root.AddComponent<PlayerAvatar>();
            Text(root.transform, "NameTag", "Racer", new Vector3(0f, 2.1f, 0f), Quaternion.identity, 2.2f, Color.white, new Vector2(3f, 0.5f));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ================================================================== managers + rig

        /// <summary>
        /// The player body model. Blender exports include a camera and a light; turn those off
        /// in the importer so they never end up inside every player.
        /// </summary>
        static GameObject PrepareRacerModel()
        {
            var importer = AssetImporter.GetAtPath(RacerModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[KartVR] No player model at " + RacerModelPath + "; players will use the simple shape body.");
                return null;
            }
            if (importer.importCameras || importer.importLights || importer.animationType != ModelImporterAnimationType.None)
            {
                importer.importCameras = false;
                importer.importLights = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(RacerModelPath);
        }

        static void BuildManagers(GameObject kartPrefab, GameObject playerPrefab, GameObject racerModel)
        {
            var sm = new GameObject("SessionManager").AddComponent<SessionManager>();
            sm.racerModel = racerModel;
            sm.kartPrefab = kartPrefab.GetComponent<NetworkObject>();
            sm.playerPrefab = playerPrefab.GetComponent<NetworkObject>();
            sm.baseMaterial = baseLit;
            sm.particleMaterial = particle;

            var gs = new GameObject("GameState");
            var no = gs.AddComponent<NetworkObject>();
            gs.AddComponent<GameState>();
            gs.AddComponent<RaceManager>();
            gs.AddComponent<BumperManager>();
            TryTickMasterClientObject(no);
        }

        /// <summary>Best effort: tick "Is Master Client Object" so the GameState moves to a new owner when the master leaves.</summary>
        static void TryTickMasterClientObject(NetworkObject no)
        {
            // Fusion 2 keeps these as bits in a "Flags" enum field.
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var flagsField = no.GetType().GetField("Flags", any);
            if (flagsField != null && flagsField.FieldType.IsEnum)
            {
                long bits = System.Convert.ToInt64(flagsField.GetValue(no));
                foreach (var name in new[] { "MasterClientObject", "AllowStateAuthorityOverride" })
                    if (System.Enum.IsDefined(flagsField.FieldType, name))
                        bits |= System.Convert.ToInt64(System.Enum.Parse(flagsField.FieldType, name));
                flagsField.SetValue(no, System.Enum.ToObject(flagsField.FieldType, bits));
                EditorUtility.SetDirty(no);
            }

            var so = new SerializedObject(no);
            var it = so.GetIterator();
            bool found = false;
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.Boolean &&
                    it.name.IndexOf("MasterClient", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    it.boolValue = true;
                    found = true;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!found)
                Debug.Log("[KartVR] Select the GameState object and tick 'Is Master Client Object' on its NetworkObject (if your Fusion version shows it).");
        }

        static void BuildRig()
        {
            var rig = new GameObject("KartRig");
            rig.transform.position = new Vector3(0f, 0f, -14f);
            var kr = rig.AddComponent<KartRig>();

            var head = new GameObject("Head");
            head.tag = "MainCamera";
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 700f;
            head.AddComponent<AudioListener>();
            var tpd = head.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(new InputAction("Head Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

            kr.head = head.transform;
            kr.leftHand = BuildHand(rig.transform, "LeftHand", "{LeftHand}", XRNode.LeftHand, new Vector3(-0.2f, 1.1f, 0.3f)).transform;
            kr.rightHand = BuildHand(rig.transform, "RightHand", "{RightHand}", XRNode.RightHand, new Vector3(0.2f, 1.1f, 0.3f)).transform;
            kr.leftHand.gameObject.AddComponent<WristMenu>();
        }

        static GameObject BuildHand(Transform rig, string name, string usage, XRNode node, Vector3 pos)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(rig, false);
            hand.transform.localPosition = pos;

            var tpd = hand.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(new InputAction(name + " Position", binding: "<XRController>" + usage + "/devicePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(name + " Rotation", binding: "<XRController>" + usage + "/deviceRotation", expectedControlType: "Quaternion"));

            var col = hand.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.04f;
            var rb = hand.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            hand.AddComponent<HandPoker>().node = node;

            Prim(PrimitiveType.Sphere, "Visual", hand.transform, Vector3.zero, Vector3.one * 0.07f, crowdBase, false);
            return hand;
        }

        // ================================================================== hub

        static Transform BuildHub()
        {
            var hub = Group("Hub", null, Vector3.zero);
            var rng = new System.Random(1);

            // Ground: tiled plaza with grass verges.
            Prim(PrimitiveType.Cube, "Grass", hub, new Vector3(0f, -0.52f, 0f), new Vector3(150f, 1f, 150f), hubGrass);
            Prim(PrimitiveType.Cube, "Plaza", hub, new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), plaza);
            // Hedges around the edge.
            Prim(PrimitiveType.Cube, "Hedge", hub, new Vector3(0f, 0.6f, 61f), new Vector3(124f, 1.2f, 2f), leavesDark);
            Prim(PrimitiveType.Cube, "Hedge", hub, new Vector3(0f, 0.6f, -61f), new Vector3(124f, 1.2f, 2f), leavesDark);
            Prim(PrimitiveType.Cube, "Hedge", hub, new Vector3(61f, 0.6f, 0f), new Vector3(2f, 1.2f, 124f), leavesDark);
            Prim(PrimitiveType.Cube, "Hedge", hub, new Vector3(-61f, 0.6f, 0f), new Vector3(2f, 1.2f, 124f), leavesDark);

            BuildFountain(hub, Vector3.zero);

            // Spawn: south of the fountain, facing the portals (+Z).
            var spawn = new GameObject("HubSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Hub;
            spawn.killY = -10f;
            spawn.transform.SetParent(hub, false);
            spawn.transform.localPosition = new Vector3(0f, 0f, -14f);
            for (int i = 0; i < 12; i++)
            {
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = new Vector3((i % 4 - 1.5f) * 2f, 0.05f, -(i / 4) * 2f);
            }

            // Portals across the north side.
            Portal(hub, "RacePortal", KartMode.Race, new Vector3(-20f, 0f, 40f), portalRace, glowRace, "RACE");
            Portal(hub, "BumperPortal", KartMode.Bumper, new Vector3(0f, 0f, 44f), portalBumper, glowBumper, "BUMPER KARTS");
            Portal(hub, "FreeRoamPortal", KartMode.FreeRoam, new Vector3(20f, 0f, 40f), portalFree, glowFree, "FREE ROAM");

            // Garages: 6 down each side, open towards the plaza.
            float[] zs = { -27.5f, -16.5f, -5.5f, 5.5f, 16.5f, 27.5f };
            for (int i = 0; i < 6; i++)
            {
                GarageBay(hub, i, new Vector3(-48f, 0f, zs[i]), Quaternion.Euler(0f, -90f, 0f));
                GarageBay(hub, i + 6, new Vector3(48f, 0f, zs[i]), Quaternion.Euler(0f, 90f, 0f));
            }
            Text(hub, "GaragesWest", "GARAGES", new Vector3(-40f, 7.5f, 0f), Quaternion.Euler(0f, -90f, 0f), 12f, Color.white, new Vector2(20f, 2f));
            Text(hub, "GaragesEast", "GARAGES", new Vector3(40f, 7.5f, 0f), Quaternion.Euler(0f, 90f, 0f), 12f, Color.white, new Vector2(20f, 2f));

            // Lobby code terminals beside the spawn, facing the middle.
            Terminal(hub, "CodeTerminalWest", new Vector3(-9f, 0f, -14f), Quaternion.Euler(0f, -90f, 0f));
            Terminal(hub, "CodeTerminalEast", new Vector3(9f, 0f, -14f), Quaternion.Euler(0f, 90f, 0f));

            var board = Board(hub, "LobbyBoard", new Vector3(0f, 10f, 54f), Quaternion.identity, new Vector2(20f, 7.5f), 5f, "KART VR");
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Lobby;
            info.text = board;
            info.title = "KART VR";

            Board(hub, "HelpBoard", new Vector3(-16f, 3.2f, -5f), Quaternion.Euler(0f, -62f, 0f), new Vector2(10f, 4.6f), 2.4f,
                "<b>HOW TO PLAY</b>\nWalk: left stick   Turn: right stick\nWalk into a PORTAL to play\nYOUR GARAGE: customize your kart\n" +
                "Driving: GRAB THE WHEEL (grip) and turn it! R trigger gas, L trigger brake\nX drift, A booster, B respawn, Y menu", neonPink);

            // Lamps, trees and benches around the plaza.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f + 0.2f;
                Lamp(hub, new Vector3(Mathf.Sin(a) * 17f, 0f, Mathf.Cos(a) * 17f));
            }
            foreach (var p in new[] { new Vector3(-32f, 0f, 50f), new Vector3(32f, 0f, 50f), new Vector3(-34f, 0f, -48f), new Vector3(34f, 0f, -48f),
                         new Vector3(-12f, 0f, 54f), new Vector3(12f, 0f, 54f), new Vector3(-22f, 0f, -36f), new Vector3(22f, 0f, -36f) })
            {
                Prim(PrimitiveType.Cylinder, "Planter", hub, p + new Vector3(0f, 0.4f, 0f), new Vector3(3.2f, 0.4f, 3.2f), stone);
                Tree(hub, p + new Vector3(0f, 0.8f, 0f), 0.9f, rng);
            }
            Bench(hub, new Vector3(-8f, 0f, 8f), 135f);
            Bench(hub, new Vector3(8f, 0f, 8f), 225f);
            Bench(hub, new Vector3(-8f, 0f, -6f), 45f);
            Bench(hub, new Vector3(8f, 0f, -6f), 315f);
            return hub;
        }

        static void BuildFountain(Transform parent, Vector3 pos)
        {
            var f = Group("Fountain", parent, pos);
            Prim(PrimitiveType.Cylinder, "Basin", f, new Vector3(0f, 0.35f, 0f), new Vector3(9f, 0.35f, 9f), stone);
            Prim(PrimitiveType.Cylinder, "Water", f, new Vector3(0f, 0.62f, 0f), new Vector3(8.2f, 0.05f, 8.2f), water, false);
            Prim(PrimitiveType.Cylinder, "Column", f, new Vector3(0f, 1.6f, 0f), new Vector3(1f, 1.2f, 1f), stone);
            Prim(PrimitiveType.Cylinder, "Bowl", f, new Vector3(0f, 2.9f, 0f), new Vector3(3.4f, 0.18f, 3.4f), stone);
            Prim(PrimitiveType.Cylinder, "BowlWater", f, new Vector3(0f, 3.07f, 0f), new Vector3(3f, 0.03f, 3f), water, false);
            // A golden kart trophy spinning on top.
            var trophy = Group("Trophy", f, new Vector3(0f, 3.6f, 0f));
            trophy.gameObject.AddComponent<Rotator>().degreesPerSecond = 25f;
            var gold = Mat("Gold", new Color(1f, 0.8f, 0.25f), 0.9f, 1f);
            Prim(PrimitiveType.Cube, "TrophyBody", trophy, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.3f, 1.6f), gold, false);
            Prim(PrimitiveType.Sphere, "TrophyNose", trophy, new Vector3(0f, 0.35f, 0.8f), new Vector3(0.9f, 0.3f, 0.6f), gold, false);
            foreach (var w in new[] { new Vector3(-0.55f, 0.2f, 0.55f), new Vector3(0.55f, 0.2f, 0.55f), new Vector3(-0.55f, 0.2f, -0.55f), new Vector3(0.55f, 0.2f, -0.55f) })
                Prim(PrimitiveType.Cylinder, "TrophyWheel", trophy, w, new Vector3(0.4f, 0.08f, 0.4f), gold, false, Quaternion.Euler(0f, 0f, 90f));
            Prim(PrimitiveType.Cube, "TrophyWing", trophy, new Vector3(0f, 0.8f, -0.7f), new Vector3(1.1f, 0.06f, 0.3f), gold, false);
        }

        static void Bench(Transform parent, Vector3 pos, float yaw)
        {
            var b = Group("Bench", parent, pos, Quaternion.Euler(0f, yaw, 0f));
            Prim(PrimitiveType.Cube, "Seat", b, new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.1f, 0.5f), wood);
            Prim(PrimitiveType.Cube, "Back", b, new Vector3(0f, 0.8f, 0.22f), new Vector3(2f, 0.5f, 0.08f), wood);
            foreach (float x in new[] { -0.85f, 0.85f })
                Prim(PrimitiveType.Cube, "Leg", b, new Vector3(x, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), dark);
        }

        static void Portal(Transform parent, string name, KartMode mode, Vector3 pos, Material surface, Material glow, string label)
        {
            var p = Group(name, parent, pos);
            // Stone arch with a glowing trim and a spinning ring.
            Prim(PrimitiveType.Cube, "PillarL", p, new Vector3(-3.4f, 2.6f, 0f), new Vector3(1f, 5.2f, 1.2f), stone);
            Prim(PrimitiveType.Cube, "PillarR", p, new Vector3(3.4f, 2.6f, 0f), new Vector3(1f, 5.2f, 1.2f), stone);
            Prim(PrimitiveType.Cube, "Top", p, new Vector3(0f, 5.6f, 0f), new Vector3(7.8f, 1f, 1.3f), stone);
            Prim(PrimitiveType.Cube, "GlowL", p, new Vector3(-2.85f, 2.6f, -0.62f), new Vector3(0.12f, 5.2f, 0.06f), glow, false);
            Prim(PrimitiveType.Cube, "GlowR", p, new Vector3(2.85f, 2.6f, -0.62f), new Vector3(0.12f, 5.2f, 0.06f), glow, false);
            Prim(PrimitiveType.Cube, "GlowTop", p, new Vector3(0f, 5.08f, -0.62f), new Vector3(5.8f, 0.12f, 0.06f), glow, false);
            Prim(PrimitiveType.Cube, "Step", p, new Vector3(0f, 0.08f, 0f), new Vector3(6f, 0.16f, 3f), stone);
            var field = Prim(PrimitiveType.Cube, "Field", p, new Vector3(0f, 2.6f, 0f), new Vector3(5.6f, 5f, 1.2f), surface);
            field.GetComponent<Collider>().isTrigger = true;
            field.AddComponent<ModePortal>().targetMode = mode;
            var ring = Hoop(p, "Ring", new Vector3(0f, 2.6f, -0.75f), Quaternion.identity, 2.2f, 0.12f, glow, 24);
            ring.gameObject.AddComponent<Rotator>().axis = Vector3.forward;
            Text(p, "Label", label, new Vector3(0f, 7f, -0.2f), Quaternion.identity, 9f, Color.white, new Vector2(14f, 1.6f));
            Text(p, "Hint", "walk in to play", new Vector3(0f, 6.25f, -0.66f), Quaternion.identity, 3.5f, new Color(1f, 1f, 1f, 0.85f), new Vector2(8f, 0.6f));
        }

        static void Terminal(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            // Standing-height kiosk; the keypad faces -Z.
            var t = Group(name, parent, pos, rot);
            Prim(PrimitiveType.Cube, "Stand", t, new Vector3(0f, 0.55f, 0.2f), new Vector3(0.6f, 1.1f, 0.4f), metal);
            Prim(PrimitiveType.Cube, "Back", t, new Vector3(0f, 1.45f, 0.12f), new Vector3(1.05f, 1.0f, 0.12f), dark, false);
            Prim(PrimitiveType.Cube, "Glow", t, new Vector3(0f, 1.97f, 0.05f), new Vector3(1.05f, 0.05f, 0.05f), neonCyan, false);
            var pad = new GameObject("Keypad");
            pad.transform.SetParent(t, false);
            pad.transform.localPosition = new Vector3(0f, 1.42f, 0f);
            pad.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            pad.AddComponent<KeypadTerminal>();
            Text(t, "Title", "LOBBY CODES", new Vector3(0f, 2.25f, 0.05f), Quaternion.identity, 2.4f, Color.white, new Vector2(3f, 0.4f));
        }

        static void GarageBay(Transform parent, int index, Vector3 pos, Quaternion rot)
        {
            // Local frame: +Z goes from the plaza into the bay; the opening faces -Z.
            var g = Group("Garage" + (index + 1), parent, pos, rot);
            var garage = g.gameObject.AddComponent<Garage>();
            garage.index = index;

            Prim(PrimitiveType.Cube, "Floor", g, new Vector3(0f, 0.05f, 5f), new Vector3(7.6f, 0.1f, 10f), garageFloor);
            Prim(PrimitiveType.Cube, "Lip", g, new Vector3(0f, 0.06f, 0.1f), new Vector3(7.6f, 0.11f, 0.3f), hazard);
            Prim(PrimitiveType.Cube, "BackWall", g, new Vector3(0f, 2.3f, 10.1f), new Vector3(8.4f, 4.6f, 0.3f), white);
            Prim(PrimitiveType.Cube, "WallL", g, new Vector3(-4.05f, 2.3f, 5f), new Vector3(0.3f, 4.6f, 10.4f), white);
            Prim(PrimitiveType.Cube, "WallR", g, new Vector3(4.05f, 2.3f, 5f), new Vector3(0.3f, 4.6f, 10.4f), white);
            Prim(PrimitiveType.Cube, "Roof", g, new Vector3(0f, 4.75f, 5f), new Vector3(8.8f, 0.3f, 10.8f), dark);
            Prim(PrimitiveType.Cube, "Header", g, new Vector3(0f, 4.3f, 0f), new Vector3(8.8f, 0.9f, 0.5f), dark);
            Prim(PrimitiveType.Cube, "Stripe", g, new Vector3(0f, 1.2f, 9.94f), new Vector3(8f, 0.25f, 0.02f), hazard, false);
            Prim(PrimitiveType.Cube, "CeilingLight", g, new Vector3(0f, 4.58f, 5f), new Vector3(3f, 0.04f, 0.6f), lampGlow, false);

            // Neon trim around the opening, tinted with the owner's paint color at runtime.
            var neon = new List<Renderer>
            {
                Prim(PrimitiveType.Cube, "NeonTop", g, new Vector3(0f, 3.82f, -0.28f), new Vector3(7.8f, 0.08f, 0.08f), neonWhite, false).GetComponent<Renderer>(),
                Prim(PrimitiveType.Cube, "NeonL", g, new Vector3(-3.86f, 1.9f, -0.28f), new Vector3(0.08f, 3.8f, 0.08f), neonWhite, false).GetComponent<Renderer>(),
                Prim(PrimitiveType.Cube, "NeonR", g, new Vector3(3.86f, 1.9f, -0.28f), new Vector3(0.08f, 3.8f, 0.08f), neonWhite, false).GetComponent<Renderer>(),
            };
            garage.neon = neon.ToArray();
            garage.sign = Text(g, "Sign", "GARAGE " + (index + 1), new Vector3(0f, 4.3f, -0.27f), Quaternion.identity, 4.5f, Color.white, new Vector2(8.2f, 0.85f));

            // Turntable the kart is shown on, and where it parks (facing out of the bay).
            var turntable = Prim(PrimitiveType.Cylinder, "Turntable", g, new Vector3(0f, 0.12f, 5.2f), new Vector3(4.4f, 0.04f, 4.4f), metal, false);
            Prim(PrimitiveType.Cube, "TurntableStripe", turntable.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.95f, 0.5f, 0.06f), hazard, false);
            garage.turntable = turntable.transform;
            var park = Group("ParkSpot", g, new Vector3(0f, 0.16f, 5.2f), Quaternion.Euler(0f, 180f, 0f));
            garage.parkSpot = park;

            // Workshop clutter.
            Prim(PrimitiveType.Cube, "ToolChest", g, new Vector3(-3.3f, 0.7f, 9.3f), new Vector3(1f, 1.3f, 0.7f), wallRed);
            Prim(PrimitiveType.Cube, "ToolChestTop", g, new Vector3(-3.3f, 1.4f, 9.3f), new Vector3(1.05f, 0.08f, 0.75f), chrome, false);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Tire", g, new Vector3(3.2f, 0.18f + i * 0.3f, 9.1f), new Vector3(0.9f, 0.14f, 0.9f), tire);
            Prim(PrimitiveType.Cube, "Shelf", g, new Vector3(0f, 2.6f, 9.8f), new Vector3(4f, 0.08f, 0.5f), wood, false);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "Can", g, new Vector3(-1.6f + i * 0.8f, 2.8f, 9.8f), new Vector3(0.25f, 0.35f, 0.25f), i % 2 == 0 ? wallRed : rampBlue, false);

            // Customization panel on the right wall, facing into the bay.
            var editor = new GameObject("GarageEditor");
            editor.transform.SetParent(g, false);
            editor.transform.localPosition = new Vector3(3.84f, 1.45f, 4.2f);
            editor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            editor.AddComponent<GarageEditor>().garage = garage;
        }

        // ================================================================== race track

        // A closed loop; tightest corner has a ~21 m radius and the 15 m wide road never overlaps itself.
        static Vector3 TrackPoint(float t) =>
            RaceOrigin + new Vector3(70f * Mathf.Cos(t) - 6f * Mathf.Cos(3f * t), 0f, 45f * Mathf.Sin(t) + 10f * Mathf.Sin(2f * t));

        static Vector3 TrackTangent(float t) => (TrackPoint(t + 0.001f) - TrackPoint(t - 0.001f)).normalized;

        static float StepBack(float t, float metres)
        {
            float travelled = 0f;
            while (travelled < metres)
            {
                float nt = t - 0.002f;
                travelled += Vector3.Distance(TrackPoint(t), TrackPoint(nt));
                t = nt;
            }
            return t;
        }

        static float DistanceToTrack(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 240; i++)
            {
                var q = TrackPoint(i * Mathf.PI * 2f / 240f);
                best = Mathf.Min(best, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)));
            }
            return best;
        }

        static Transform BuildRaceTrack()
        {
            var race = Group("RaceTrack", null, RaceOrigin);
            var rng = new System.Random(7);
            Prim(PrimitiveType.Cube, "Ground", race, new Vector3(0f, -0.5f, 0f), new Vector3(240f, 1f, 180f), raceGrass);

            const int segments = 120;
            const float halfWidth = 7.5f;
            var road = Group("Road", race, Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i * Mathf.PI * 2f / segments, t1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 a = TrackPoint(t0), b = TrackPoint(t1);
                Vector3 dir = b - a;
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                var seg = Prim(PrimitiveType.Cube, "Road" + i, road, Vector3.zero, new Vector3(halfWidth * 2f, 0.02f, dir.magnitude + 0.4f), asphalt, false);
                seg.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 0.01f, rot);

                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 na = Vector3.Cross(Vector3.up, TrackTangent(t0)) * side;
                    Vector3 nb = Vector3.Cross(Vector3.up, TrackTangent(t1)) * side;
                    // Painted edge line.
                    Vector3 la = a + na * (halfWidth - 0.6f), lb = b + nb * (halfWidth - 0.6f);
                    var line = Prim(PrimitiveType.Cube, "Line", road, Vector3.zero, new Vector3(0.25f, 0.02f, (lb - la).magnitude + 0.1f), lineWhite, false);
                    line.transform.SetPositionAndRotation((la + lb) * 0.5f + Vector3.up * 0.025f, Quaternion.LookRotation(lb - la, Vector3.up));
                    // Barrier wall, alternating red/white.
                    Vector3 wa = a + na * halfWidth, wb = b + nb * halfWidth;
                    Vector3 wd = wb - wa;
                    var wall = Prim(PrimitiveType.Cube, "Wall", road, Vector3.zero,
                        new Vector3(0.5f, 1f, wd.magnitude + 0.3f), i % 2 == 0 ? wallRed : wallWhite);
                    wall.transform.SetPositionAndRotation((wa + wb) * 0.5f + Vector3.up * 0.5f, Quaternion.LookRotation(wd, Vector3.up));
                }
            }

            // Checkpoints in racing order; 0 is the start/finish line.
            const int checkpointCount = 8;
            float start = -Mathf.PI / 2f;
            for (int k = 0; k < checkpointCount; k++)
            {
                float t = start + k * Mathf.PI * 2f / checkpointCount;
                var cp = new GameObject("Checkpoint" + k);
                cp.transform.SetParent(race, true);
                cp.transform.SetPositionAndRotation(TrackPoint(t), Quaternion.LookRotation(TrackTangent(t), Vector3.up));
                var col = cp.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 2f, 0f);
                col.size = new Vector3(halfWidth * 2f + 1f, 4f, 1f);
                cp.AddComponent<Checkpoint>().index = k;
                if (k == 0) StartGantry(cp.transform, halfWidth);
                else if (k == 4) Arch(cp.transform, halfWidth, "KART VR", neonPink);
            }

            // Starting grid, 2 wide, behind the line.
            var grid = new GameObject("RaceGrid").AddComponent<SpawnArea>();
            grid.mode = KartMode.Race;
            grid.killY = -5f;
            grid.transform.SetParent(race, true);
            grid.transform.position = TrackPoint(start);
            for (int i = 0; i < 12; i++)
            {
                float t = StepBack(start, 4f + (i / 2) * 4.5f);
                Vector3 tan = TrackTangent(t);
                Vector3 side = Vector3.Cross(Vector3.up, tan) * (i % 2 == 0 ? -2.8f : 2.8f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(grid.transform, true);
                slot.SetPositionAndRotation(TrackPoint(t) + side, Quaternion.LookRotation(tan, Vector3.up));
                var box = Prim(PrimitiveType.Cube, "GridBox", race, Vector3.zero, new Vector3(1.6f, 0.02f, 2.4f), lineWhite, false);
                box.transform.SetPositionAndRotation(TrackPoint(t) + side + Vector3.up * 0.022f, Quaternion.LookRotation(tan, Vector3.up));
            }

            // Boost pads on the sweeps and a jump on the far straight.
            foreach (float t in new[] { 0f, 0.5f, Mathf.PI, Mathf.PI + 0.5f })
            {
                var pad = Prim(PrimitiveType.Cube, "BoostPad", race, Vector3.zero, new Vector3(4f, 0.05f, 3f), boost);
                Vector3 offset = Vector3.Cross(Vector3.up, TrackTangent(t)) * (t > 1f && t < 4f ? 3f : -3f);
                pad.transform.SetPositionAndRotation(TrackPoint(t) + offset + Vector3.up * 0.03f, Quaternion.LookRotation(TrackTangent(t), Vector3.up));
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
                Chevrons(pad.transform);
            }
            {
                float t = Mathf.PI / 2f + 0.18f;
                var jump = Group("Jump", race, Vector3.zero);
                jump.SetPositionAndRotation(TrackPoint(t), Quaternion.LookRotation(TrackTangent(t), Vector3.up));
                Ramp(jump, "JumpRamp", Vector3.zero, 0f, 6f, halfWidth * 2f - 0.4f, 8f, rampMat);
            }

            // Grandstand outside the start straight, tire stacks and trees.
            Grandstand(race, TrackPoint(start) - RaceOrigin + new Vector3(0f, 0f, -17f), rng);
            foreach (float t in new[] { 0.35f, 1.3f, 2.4f, 3.5f, 4.3f, 5.6f })
            {
                Vector3 outward = Vector3.Cross(Vector3.up, TrackTangent(t)) * -(halfWidth + 1.6f);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cylinder, "Tires", race, TrackPoint(t) - RaceOrigin + outward + new Vector3(0f, 0.2f + i * 0.35f, 0f), new Vector3(1.1f, 0.17f, 1.1f), tire);
            }
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 220f - 110f, 0f, (float)rng.NextDouble() * 160f - 80f);
                float d = DistanceToTrack(p + RaceOrigin);
                if (d < 13f) continue;
                if (Mathf.Abs(p.x) < 18f && p.z < -50f) continue; // keep the grandstand clear
                Tree(race, p, 0.8f + (float)rng.NextDouble() * 0.6f, rng);
            }

            var board = Board(race, "RaceBoard", new Vector3(0f, 9f, 0f), Quaternion.identity, new Vector2(14f, 9f), 4f, "RACE", glowRace);
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Race;
            info.text = board;
            info.title = "RACE";
            return race;
        }

        static void Chevrons(Transform pad)
        {
            for (int i = 0; i < 2; i++)
            {
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Chevron", pad, new Vector3(s * 0.15f, 0.8f, -0.1f + i * 0.3f), new Vector3(0.35f, 0.4f, 0.06f), neonWhite, false,
                        Quaternion.Euler(0f, s * 40f, 0f));
            }
        }

        static void StartGantry(Transform cp, float halfWidth)
        {
            Prim(PrimitiveType.Cube, "FinishLine", cp, new Vector3(0f, 0.025f, 0f), new Vector3(halfWidth * 2f, 0.02f, 1.2f), checker, false);
            Prim(PrimitiveType.Cube, "GantryL", cp, new Vector3(-halfWidth - 0.8f, 3.2f, 0f), new Vector3(0.6f, 6.4f, 0.6f), metal);
            Prim(PrimitiveType.Cube, "GantryR", cp, new Vector3(halfWidth + 0.8f, 3.2f, 0f), new Vector3(0.6f, 6.4f, 0.6f), metal);
            Prim(PrimitiveType.Cube, "GantryTop", cp, new Vector3(0f, 6.3f, 0f), new Vector3(halfWidth * 2f + 2.2f, 1.2f, 0.6f), dark, false);
            Prim(PrimitiveType.Cube, "GantryChecker", cp, new Vector3(0f, 7.1f, 0f), new Vector3(halfWidth * 2f + 2.2f, 0.4f, 0.62f), checker, false);
            Text(cp, "FinishText", "START / FINISH", new Vector3(0f, 6.55f, -0.32f), Quaternion.identity, 5f, Color.white, new Vector2(15f, 0.7f));
            var lightsGo = Group("StartLights", cp, new Vector3(0f, 5.95f, -0.32f));
            var lights = new List<Renderer>();
            for (int i = 0; i < 5; i++)
                lights.Add(Prim(PrimitiveType.Sphere, "Light" + i, lightsGo, new Vector3(-2f + i, 0f, 0f), Vector3.one * 0.5f,
                    Mat("LightOff", new Color(0.15f, 0.05f, 0.05f), 0.8f), false).GetComponent<Renderer>());
            lightsGo.gameObject.AddComponent<StartLights>().lights = lights.ToArray();
        }

        static void Arch(Transform cp, float halfWidth, string label, Material glow)
        {
            var a = Group("Arch", cp, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "PostL", a, new Vector3(-halfWidth - 1f, 3f, 0f), new Vector3(0.6f, 3f, 0.6f), metal);
            Prim(PrimitiveType.Cylinder, "PostR", a, new Vector3(halfWidth + 1f, 3f, 0f), new Vector3(0.6f, 3f, 0.6f), metal);
            Prim(PrimitiveType.Cube, "Banner", a, new Vector3(0f, 6.2f, 0f), new Vector3(halfWidth * 2f + 2.6f, 1.4f, 0.3f), sign, false);
            Prim(PrimitiveType.Cube, "BannerGlow", a, new Vector3(0f, 5.45f, -0.16f), new Vector3(halfWidth * 2f + 2.6f, 0.1f, 0.05f), glow, false);
            Text(a, "Text", label, new Vector3(0f, 6.2f, -0.17f), Quaternion.identity, 9f, Color.white, new Vector2(halfWidth * 2f, 1.3f));
            Text(a, "TextBack", label, new Vector3(0f, 6.2f, 0.17f), Quaternion.Euler(0f, 180f, 0f), 9f, Color.white, new Vector2(halfWidth * 2f, 1.3f));
        }

        static void Grandstand(Transform parent, Vector3 pos, System.Random rng)
        {
            // Rows step up away from the track (-Z); spectators face the track (+Z).
            var g = Group("Grandstand", parent, pos);
            for (int row = 0; row < 5; row++)
            {
                Prim(PrimitiveType.Cube, "Step" + row, g, new Vector3(0f, 0.4f + row * 0.8f, -row * 1.3f), new Vector3(30f, 0.8f + row * 1.6f, 1.3f), stone);
                for (int i = 0; i < 18; i++)
                {
                    if (rng.NextDouble() < 0.15) continue;
                    float x = -14f + i * 1.65f + (float)rng.NextDouble() * 0.3f;
                    float y = 0.8f + row * 1.6f;
                    var shirt = crowd[rng.Next(crowd.Length)];
                    Prim(PrimitiveType.Capsule, "Fan", g, new Vector3(x, y + 0.45f, -row * 1.3f), new Vector3(0.45f, 0.45f, 0.35f), shirt, false);
                    Prim(PrimitiveType.Sphere, "FanHead", g, new Vector3(x, y + 1.1f, -row * 1.3f), Vector3.one * 0.32f, crowdBase, false);
                }
            }
            Prim(PrimitiveType.Cube, "Roof", g, new Vector3(0f, 10.5f, -3f), new Vector3(32f, 0.3f, 8f), wallRed, false, Quaternion.Euler(-8f, 0f, 0f));
            foreach (float x in new[] { -15f, 15f })
                Prim(PrimitiveType.Cube, "RoofPost", g, new Vector3(x, 5f, -6.2f), new Vector3(0.4f, 10.5f, 0.4f), metal);
        }

        // ================================================================== bumper arena

        static Transform BuildBumperArena()
        {
            var b = Group("BumperArena", null, BumperOrigin);

            // Round platform over a lava lake. Fall off = lose a point.
            var floor = Prim(PrimitiveType.Cylinder, "Platform", b, new Vector3(0f, -1f, 0f), new Vector3(44f, 1f, 44f), arena);
            Object.DestroyImmediate(floor.GetComponent<Collider>()); // cylinder primitives use a capsule collider; swap for the real shape
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Prim(PrimitiveType.Cylinder, "Ring", b, new Vector3(0f, -0.99f, 0f), new Vector3(30f, 1f, 30f), neonCyan, false);
            Prim(PrimitiveType.Cylinder, "Center", b, new Vector3(0f, -0.98f, 0f), new Vector3(29.4f, 1f, 29.4f), arena, false);
            Prim(PrimitiveType.Cylinder, "Underside", b, new Vector3(0f, -4f, 0f), new Vector3(30f, 2.5f, 30f), dark, false);
            Prim(PrimitiveType.Cube, "Lava", b, new Vector3(0f, -12f, 0f), new Vector3(260f, 1f, 260f), lava, false);

            // Glowing rim lights.
            for (int i = 0; i < 48; i++)
            {
                float a = i * 360f / 48f;
                var p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.02f, 21.6f);
                Prim(PrimitiveType.Cube, "RimLight", b, p, new Vector3(1.2f, 0.06f, 0.3f), i % 2 == 0 ? neonPink : neonCyan, false, Quaternion.Euler(0f, a, 0f));
            }

            // The sweeper: a spinning bar that shoves anyone in its way. Jump... or get swept!
            Prim(PrimitiveType.Cylinder, "Hub", b, new Vector3(0f, 0.6f, 0f), new Vector3(3f, 0.6f, 3f), metal);
            var sweeper = new GameObject("Sweeper");
            sweeper.transform.SetParent(b, false);
            var srb = sweeper.AddComponent<Rigidbody>();
            srb.isKinematic = true;
            srb.interpolation = RigidbodyInterpolation.Interpolate;
            sweeper.AddComponent<Rotator>().degreesPerSecond = 35f;
            Prim(PrimitiveType.Cube, "Bar", sweeper.transform, new Vector3(0f, 0.5f, 0f), new Vector3(36f, 0.55f, 0.7f), hazard);
            Prim(PrimitiveType.Cube, "BarGlow", sweeper.transform, new Vector3(0f, 0.82f, 0f), new Vector3(36f, 0.08f, 0.2f), neonPink, false);
            Prim(PrimitiveType.Cylinder, "Cap", sweeper.transform, new Vector3(0f, 1.3f, 0f), new Vector3(2.2f, 0.12f, 2.2f), neonCyan, false);

            // Bouncy bumpers.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                var pos = new Vector3(Mathf.Sin(a) * 12f, 0.6f, Mathf.Cos(a) * 12f);
                var bumper = Prim(PrimitiveType.Cylinder, "Bouncer" + i, b, pos, new Vector3(2.2f, 0.6f, 2.2f), wallRed);
                bumper.AddComponent<Bouncer>();
                Prim(PrimitiveType.Cylinder, "Glow", bumper.transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.8f, 0.05f, 0.8f), neonWhite, false);
                Prim(PrimitiveType.Cylinder, "Band", bumper.transform, Vector3.zero, new Vector3(1.04f, 0.25f, 1.04f), wallWhite, false);
            }

            var spawn = new GameObject("BumperSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Bumper;
            spawn.killY = -6f;
            spawn.transform.SetParent(b, false);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var pos = new Vector3(Mathf.Sin(a) * 17f, 0f, Mathf.Cos(a) * 17f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(-pos, Vector3.up);
            }

            // Light towers and the scoreboard.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var tower = Group("LightTower", b, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, -12f, 34f), Quaternion.Euler(0f, a + 180f, 0f));
                Prim(PrimitiveType.Cube, "Mast", tower, new Vector3(0f, 12f, 0f), new Vector3(0.8f, 24f, 0.8f), metal);
                Prim(PrimitiveType.Cube, "Lights", tower, new Vector3(0f, 24f, -0.5f), new Vector3(4f, 2f, 0.3f), lampGlow, false, Quaternion.Euler(20f, 0f, 0f));
            }
            var board = Board(b, "BumperBoard", new Vector3(0f, 11f, 34f), Quaternion.identity, new Vector2(14f, 9f), 4f, "BUMPER KARTS", neonPink);
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Bumper;
            info.text = board;
            info.title = "BUMPER KARTS";
            Prim(PrimitiveType.Cube, "BoardPillar", b, new Vector3(0f, -1f, 34.3f), new Vector3(1f, 22f, 1f), metal);
            return b;
        }

        // ================================================================== free roam

        static void Ramp(Transform parent, string name, Vector3 pos, float yaw, float length, float width, float angle, Material mat)
        {
            // Tilted box whose back edge is buried so the slope starts flush with the ground.
            const float thickness = 1f;
            float a = angle * Mathf.Deg2Rad;
            float y = length * 0.5f * Mathf.Sin(a) - thickness * 0.5f * Mathf.Cos(a);
            var r = Prim(PrimitiveType.Cube, name, parent, Vector3.zero, new Vector3(width, thickness, length), mat);
            r.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-angle, 0f, 0f);
            r.transform.localPosition = pos + new Vector3(0f, y, 0f);
        }

        /// <summary>A curved wall made of tilted panels (skate-park style). Opening faces local -Z.</summary>
        static void QuarterPipe(Transform parent, Vector3 pos, float yaw, float width, float radius, Material mat)
        {
            var q = Group("QuarterPipe", parent, pos, Quaternion.Euler(0f, yaw, 0f));
            const int panels = 7;
            for (int i = 0; i < panels; i++)
            {
                float a0 = i * 80f / panels, a1 = (i + 1) * 80f / panels, am = (a0 + a1) * 0.5f;
                // Point on a circle of `radius` centred at (0, radius, 0): angle 0 = ground, 90 = vertical wall.
                Vector3 p = new Vector3(0f, radius - Mathf.Cos(am * Mathf.Deg2Rad) * radius, Mathf.Sin(am * Mathf.Deg2Rad) * radius);
                float len = 2f * Mathf.PI * radius * (a1 - a0) / 360f + 0.15f;
                Prim(PrimitiveType.Cube, "Panel", q, p, new Vector3(width, 0.4f, len), mat, true, Quaternion.Euler(-am, 0f, 0f));
            }
            Prim(PrimitiveType.Cube, "Deck", q, new Vector3(0f, radius * 0.95f, radius + 1.5f), new Vector3(width, 0.4f, 3f), stone);
        }

        static Transform BuildFreeRoam()
        {
            var f = Group("FreeRoam", null, FreeRoamOrigin);
            var rng = new System.Random(3);
            Prim(PrimitiveType.Cube, "Ground", f, new Vector3(0f, -0.5f, 0f), new Vector3(320f, 1f, 320f), freeGrass);

            // A ring road around the park.
            for (int i = 0; i < 64; i++)
            {
                float a0 = i * Mathf.PI * 2f / 64f, a1 = (i + 1) * Mathf.PI * 2f / 64f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 120f, 0.01f, Mathf.Sin(a0) * 120f), p1 = new Vector3(Mathf.Cos(a1) * 120f, 0.01f, Mathf.Sin(a1) * 120f);
                var seg = Prim(PrimitiveType.Cube, "RingRoad", f, (p0 + p1) * 0.5f, new Vector3(12f, 0.02f, (p1 - p0).magnitude + 0.3f), asphalt, false);
                seg.transform.localRotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
            }

            // Jumps of every size.
            Ramp(f, "Ramp1", new Vector3(0f, 0f, 30f), 0f, 12f, 8f, 14f, rampMat);
            Ramp(f, "Ramp2", new Vector3(35f, 0f, 10f), 90f, 16f, 10f, 20f, rampBlue);
            Ramp(f, "Ramp3", new Vector3(-40f, 0f, 40f), -45f, 20f, 10f, 24f, rampRed);
            Ramp(f, "MegaRamp", new Vector3(0f, 0f, 90f), 0f, 30f, 14f, 28f, rampMat);
            // Boost hoops to fly through after the mega ramp.
            for (int i = 0; i < 3; i++)
                BoostHoop(f, new Vector3(0f, 15f - i * 3f, 112f + i * 14f));
            // Kicker chain: hop-hop-hop.
            for (int i = 0; i < 5; i++)
                Ramp(f, "Kicker" + i, new Vector3(-80f, 0f, -20f + i * 14f), 0f, 5f, 8f, 12f, i % 2 == 0 ? rampBlue : rampRed);

            // Skate-park bowl: four quarter pipes facing each other.
            var bowl = new Vector3(70f, 0f, -70f);
            QuarterPipe(f, bowl + new Vector3(0f, 0f, 14f), 0f, 24f, 6f, rampBlue);
            QuarterPipe(f, bowl + new Vector3(0f, 0f, -14f), 180f, 24f, 6f, rampBlue);
            QuarterPipe(f, bowl + new Vector3(14f, 0f, 0f), 90f, 24f, 6f, rampRed);
            QuarterPipe(f, bowl + new Vector3(-14f, 0f, 0f), -90f, 24f, 6f, rampRed);

            // Raised plateau with a ramp up to it, and a drop-off jump off the far side.
            Prim(PrimitiveType.Cube, "Plateau", f, new Vector3(-70f, 2.5f, -60f), new Vector3(30f, 5f, 30f), stone);
            Ramp(f, "PlateauRamp", new Vector3(-70f, 0f, -34f), 180f, 22f, 10f, 13.5f, rampMat);
            Ramp(f, "PlateauKicker", new Vector3(-70f, 5f, -76f), 180f, 4f, 8f, 15f, rampRed);

            // Hills, rocks and a lake.
            Prim(PrimitiveType.Sphere, "Hill1", f, new Vector3(60f, -14f, 60f), Vector3.one * 40f, freeGrass);
            Prim(PrimitiveType.Sphere, "Hill2", f, new Vector3(95f, -20f, 5f), Vector3.one * 50f, freeGrass);
            Prim(PrimitiveType.Sphere, "Hill3", f, new Vector3(-30f, -16f, -100f), Vector3.one * 40f, freeGrass);
            Prim(PrimitiveType.Cylinder, "LakeShore", f, new Vector3(-75f, 0.01f, 60f), new Vector3(40f, 0.02f, 34f), sand, false);
            Prim(PrimitiveType.Cylinder, "Lake", f, new Vector3(-75f, 0.04f, 60f), new Vector3(34f, 0.02f, 28f), water, false);
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 260f - 130f, 0f, (float)rng.NextDouble() * 260f - 130f);
                float s = 1.5f + (float)rng.NextDouble() * 3f;
                Prim(PrimitiveType.Sphere, "Rock", f, p, new Vector3(s * 1.3f, s, s), rock);
            }

            // Slalom poles and a boost strip.
            for (int i = 0; i < 8; i++)
                Prim(PrimitiveType.Cylinder, "Pole" + i, f, new Vector3(-20f + (i % 2) * 6f, 1.5f, -20f - i * 10f), new Vector3(0.6f, 1.5f, 0.6f), i % 2 == 0 ? wallRed : wallWhite);
            for (int i = 0; i < 4; i++)
            {
                var pad = Prim(PrimitiveType.Cube, "BoostPad" + i, f, new Vector3(30f, 0.03f, -30f - i * 12f), new Vector3(5f, 0.05f, 3f), boost);
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
                Chevrons(pad.transform);
            }

            // Trees, keeping the stunt areas clear.
            for (int i = 0; i < 90; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 300f - 150f, 0f, (float)rng.NextDouble() * 300f - 150f);
                float r = new Vector2(p.x, p.z).magnitude;
                if (r < 40f || (r > 112f && r < 128f)) continue;            // spawn area and ring road
                if (Vector3.Distance(p, bowl) < 26f) continue;
                if (Vector3.Distance(p, new Vector3(-75f, 0f, 60f)) < 22f) continue;
                if (Mathf.Abs(p.x) < 12f && p.z > 20f && p.z < 160f) continue; // ramp run-out
                if (Vector3.Distance(p, new Vector3(-70f, 0f, -55f)) < 30f) continue;
                Tree(f, p, 0.8f + (float)rng.NextDouble() * 0.7f, rng);
            }

            var spawn = new GameObject("FreeRoamSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.FreeRoam;
            spawn.killY = -10f;
            spawn.transform.SetParent(f, false);
            for (int i = 0; i < 12; i++)
            {
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = new Vector3((i % 4 - 1.5f) * 3.5f, 0f, -(i / 4) * 4f);
            }

            Board(f, "FreeRoamSign", new Vector3(0f, 4.5f, 14f), Quaternion.identity, new Vector2(11f, 3.2f), 2.8f,
                "<b>FREE ROAM STUNT PARK</b>\nMega ramp ahead  -  bowl to the right  -  kickers to the left\nWrist menu > Hub to go back", glowFree);
            return f;
        }

        static void BoostHoop(Transform parent, Vector3 pos)
        {
            var hoop = Hoop(parent, "BoostHoop", pos, Quaternion.identity, 3.2f, 0.35f, boost, 20);
            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(hoop, false);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(5.4f, 5.4f, 1.2f);
            var pad = trigger.AddComponent<BoostPad>();
            pad.kick = 6f;
        }
    }
}
