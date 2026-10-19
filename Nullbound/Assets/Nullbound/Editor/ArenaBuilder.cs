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

namespace Nullbound.EditorTools
{
    /// <summary>
    /// Menu: Nullbound > Build Prototype World.
    /// Generates the textures, materials, the Fighter prefab, the VR rig and the first arena (Training Grounds:
    /// a sparring ring, training dummies, breakable pillars, crates and a wall, a KO board). Everything is made from
    /// simple shapes so it runs well on Quest; swap in real models whenever you like.
    /// </summary>
    public static partial class ArenaBuilder
    {
        const string Root = "Assets/Nullbound";
        const string ScenePath = Root + "/Scenes/Arena.unity";
        const string FighterPrefabPath = Root + "/Prefabs/Fighter.prefab";
        public const string WorldScenePath = ScenePath;

        /// <summary>Bump this whenever the generated world changes, so existing projects rebuild it once automatically.</summary>
        public const int WorldVersion = 5; // 5 = real colliders on flat cylinders / squashed spheres, no sky barrier
        const string WorldVersionFile = "ProjectSettings/NullboundWorldVersion.txt";
        public static bool WorldUpToDate => File.Exists(WorldVersionFile) && File.ReadAllText(WorldVersionFile).Trim() == WorldVersion.ToString();
        public static bool TextResourcesReady => Resources.Load<TMP_Settings>("TMP Settings") != null;

        static Material floor, ringMat, wall, concrete, metal, dark, sign, rope, crate, stone, neonRed, neonCyan, neonGold, baseLit, particle, padBlue, padRed,
            bagMat, vignetteRing, vignetteSolid;

        [MenuItem("Nullbound/Build Prototype World")]
        public static void Build()
        {
            if (!TextResourcesReady)
            {
                if (ImportTextMeshProEssentials())
                    EditorUtility.DisplayDialog("Nullbound", "Text resources are being added (click Import if Unity asks).\n\nWhen that's done, click Nullbound > Build Prototype World again.", "OK");
                else
                    EditorUtility.DisplayDialog("Nullbound", "TextMeshPro resources are missing.\n\nGo to Window > TextMeshPro > Import TMP Essential Resources, then run this again.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Nullbound", "This will (re)create the Nullbound materials, prefabs and the Arena scene.\nAny edits you made to those files will be replaced.", "Build it", "Cancel"))
                return;
            BuildWorld();
            EditorUtility.DisplayDialog("Nullbound",
                "Done! The Arena scene is open. Press Play!\n\n" +
                "No headset? W/A/S/D move, Q/E turn, hold right mouse to look, LEFT CLICK punch, G left punch, hold H block, Space jump, Shift dash, F special, Tab menu.\n\n" +
                (SessionManager.MultiplayerAvailable
                    ? "Multiplayer is ON (set your Photon App ID with Nullbound > Set Up Multiplayer)."
                    : "Online play is OFF until you import Photon Fusion 2 (Training vs bots works now)."),
                "Fight!");
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

            var fighterPrefab = BuildFighterPrefab();
            BuildManagers(fighterPrefab);
            BuildRig();
            BuildTrainingGrounds();
            BuildMainMenuRoom();
            BuildBattleground();

            EditorSceneManager.SaveScene(scene, ScenePath);
            FusionBake.BakeAndSave(scene); // networked scene objects (ArenaState) must be baked or they won't start in builds
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            File.WriteAllText(WorldVersionFile, WorldVersion.ToString());
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

        static Material Mat(string name, Color c, float smoothness = 0.2f, float metallic = 0f, Texture2D tex = null, Vector2? tiling = null, Color? emission = null)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.mainTexture = tex;
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

        /// <summary>Standard shader in Fade mode: colour and texture alpha make it see-through.</summary>
        static Material Fade(Material m)
        {
            if (m.HasProperty("_Mode"))
            {
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3100;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        delegate Color Pixel(float u, float v, System.Random rng);

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

        static float Noise(float u, float v, float scale, float seed) =>
            Mathf.PerlinNoise(u * scale + seed, v * scale + seed * 0.7f) * 0.65f + Mathf.PerlinNoise(u * scale * 3f + seed, v * scale * 3f) * 0.35f;

        static void CreateMaterials()
        {
            var tiles = Tex("ArenaTiles", 256, (u, v, r) =>
            {
                float n = Noise(u, v, 12f, 4f);
                bool line = u % 0.25f < 0.012f || v % 0.25f < 0.012f;
                float g = line ? 0.18f : 0.34f + n * 0.12f;
                return new Color(g, g, g * 1.08f);
            });
            var concreteTex = Tex("Concrete", 256, (u, v, r) =>
            {
                float n = Noise(u, v, 9f, 7f);
                float g = 0.5f + n * 0.25f + (r.NextDouble() < 0.03 ? -0.1f : 0f);
                return new Color(g, g * 0.98f, g * 0.95f);
            });
            var crateTex = Tex("Crate", 128, (u, v, r) =>
            {
                bool edge = u < 0.08f || u > 0.92f || v < 0.08f || v > 0.92f || Mathf.Abs(u - v) < 0.05f;
                float grain = Mathf.PerlinNoise(u * 3f, v * 40f) * 0.15f;
                return edge ? new Color(0.42f, 0.28f, 0.15f) : new Color(0.62f + grain, 0.45f + grain, 0.26f);
            });
            var glowTex = Tex("SoftGlow", 64, (u, v, r) =>
            {
                float d = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
                return new Color(1f, 1f, 1f, d * d);
            }, false);
            // Comfort vignette: clear in the middle, dark at the edges.
            var ringTex = Tex("Vignette", 128, (u, v, r) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.95f, d));
                return new Color(0f, 0f, 0f, a);
            }, false);

            floor = Mat("ArenaFloor", Color.white, 0.25f, 0f, tiles, new Vector2(14f, 14f));
            ringMat = Mat("RingFloor", new Color(0.2f, 0.22f, 0.35f), 0.45f, 0f, tiles, new Vector2(5f, 5f));
            wall = Mat("ArenaWall", new Color(0.26f, 0.27f, 0.32f), 0.3f, 0f, concreteTex, new Vector2(4f, 1f));
            concrete = Mat("Concrete", Color.white, 0.15f, 0f, concreteTex);
            stone = Mat("Stone", new Color(0.75f, 0.72f, 0.68f), 0.15f, 0f, concreteTex);
            metal = Mat("Metal", new Color(0.6f, 0.62f, 0.66f), 0.7f, 0.8f);
            dark = Mat("Dark", new Color(0.1f, 0.1f, 0.12f), 0.4f);
            sign = Mat("Sign", new Color(0.07f, 0.07f, 0.1f), 0.6f);
            rope = Mat("Rope", new Color(0.9f, 0.2f, 0.15f), 0.3f, 0f, null, null, new Color(0.6f, 0.08f, 0.05f));
            crate = Mat("Crate", Color.white, 0.1f, 0f, crateTex);
            bagMat = Mat("PunchBag", new Color(0.65f, 0.15f, 0.12f), 0.35f);
            neonRed = Mat("NeonRed", new Color(1f, 0.3f, 0.2f), 0.5f, 0f, null, null, new Color(1f, 0.25f, 0.15f) * 2f);
            neonCyan = Mat("NeonCyan", new Color(0.3f, 0.9f, 1f), 0.5f, 0f, null, null, new Color(0.2f, 0.8f, 1f) * 2f);
            neonGold = Mat("NeonGold", new Color(1f, 0.8f, 0.3f), 0.5f, 0f, null, null, new Color(1f, 0.7f, 0.2f) * 2f);
            padBlue = Mat("PadBlue", new Color(0.2f, 0.45f, 1f), 0.4f, 0f, null, null, new Color(0.1f, 0.25f, 0.7f));
            padRed = Mat("PadRed", new Color(1f, 0.3f, 0.25f), 0.4f, 0f, null, null, new Color(0.7f, 0.12f, 0.08f));
            baseLit = Mat("BaseLit", Color.white, 0.3f);
            vignetteRing = Fade(Mat("VignetteRing", new Color(0f, 0f, 0f, 1f), 0f, 0f, ringTex));
            vignetteSolid = Fade(Mat("ScreenFlash", new Color(1f, 0f, 0f, 0f), 0f));

            string ppath = Root + "/Materials/Particle.mat";
            var particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            particle = AssetDatabase.LoadAssetAtPath<Material>(ppath);
            if (particle == null)
            {
                particle = new Material(particleShader);
                AssetDatabase.CreateAsset(particle, ppath);
            }
            particle.shader = particleShader;
            particle.mainTexture = glowTex;
            EditorUtility.SetDirty(particle);
        }

        static void SetUpLighting()
        {
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                var sun = sunGo.GetComponent<Light>();
                sunGo.transform.rotation = Quaternion.Euler(38f, -50f, 0f);
                sun.color = new Color(1f, 0.85f, 0.7f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
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
                // Dramatic dusk.
                sky.SetFloat("_SunSize", 0.06f);
                sky.SetFloat("_AtmosphereThickness", 1.6f);
                sky.SetColor("_SkyTint", new Color(0.55f, 0.4f, 0.75f));
                sky.SetColor("_GroundColor", new Color(0.25f, 0.2f, 0.25f));
                sky.SetFloat("_Exposure", 1.1f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.5f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.42f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 380f;
            RenderSettings.fogColor = new Color(0.6f, 0.45f, 0.55f);
            QualitySettings.shadowDistance = 40f;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = true, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            else if (type == PrimitiveType.Cylinder || (type != PrimitiveType.Cube && type != PrimitiveType.Quad && type != PrimitiveType.Plane &&
                     (Mathf.Abs(scale.x - scale.y) > 0.01f || Mathf.Abs(scale.y - scale.z) > 0.01f)))
            {
                // Unity gives cylinders a capsule collider, and squashed spheres a round one sized by their biggest
                // side: a flat 36 m plaza became an invisible 18 m ball you couldn't walk onto. Use the real shape.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true;
            }
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static TextMeshPro Text(Transform parent, string name, string text, Vector3 localPos, Quaternion rot, float fontSize, Color color, Vector2 rect)
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

        /// <summary>A sign board with text on its -Z face (readable when you look toward +Z).</summary>
        static TextMeshPro Board(Transform parent, string name, Vector3 localPos, Quaternion rot, Vector2 size, float fontSize, string text, Material trim)
        {
            var b = Group(name, parent, localPos, rot);
            Prim(PrimitiveType.Cube, "Backing", b, new Vector3(0f, 0f, 0.15f), new Vector3(size.x, size.y, 0.2f), sign);
            Prim(PrimitiveType.Cube, "TrimTop", b, new Vector3(0f, size.y * 0.5f, 0.05f), new Vector3(size.x + 0.15f, 0.1f, 0.1f), trim, false);
            Prim(PrimitiveType.Cube, "TrimBottom", b, new Vector3(0f, -size.y * 0.5f, 0.05f), new Vector3(size.x + 0.15f, 0.1f, 0.1f), trim, false);
            return Text(b, "Text", text, new Vector3(0f, 0f, 0.02f), Quaternion.identity, fontSize, Color.white, size * 0.92f);
        }

        static void MarkStatic(Transform root)
        {
            if (root.GetComponent<TextMeshPro>() || root.GetComponent<Breakable>() || root.GetComponent<TrainingDummy>() || root.GetComponent<ScoreBoard>() || root.GetComponent<MainMenu>()) return;
            GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Transform child in root) MarkStatic(child);
        }

        // ================================================================== prefab, managers, rig

        static GameObject BuildFighterPrefab()
        {
            var root = new GameObject("Fighter");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            root.AddComponent<NetworkObject>();
            root.AddComponent<Fighter>();
            root.AddComponent<VoiceChat>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FighterPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildManagers(GameObject fighterPrefab)
        {
            var sm = new GameObject("SessionManager").AddComponent<SessionManager>();
            sm.fighterPrefab = fighterPrefab.GetComponent<NetworkObject>();
            sm.baseMaterial = baseLit;
            sm.particleMaterial = particle;

            var arena = new GameObject("ArenaState");
            var no = arena.AddComponent<NetworkObject>();
            arena.AddComponent<ArenaState>();
            TryTickMasterClientObject(no);
        }

        /// <summary>Best effort: tick "Is Master Client Object" so the ArenaState moves to a new owner when the master leaves.</summary>
        static void TryTickMasterClientObject(NetworkObject no)
        {
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
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.Boolean && it.name.IndexOf("MasterClient", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    it.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRig()
        {
            var rig = new GameObject("VRRig");
            rig.transform.position = new Vector3(0f, 0f, -8f);
            var vr = rig.AddComponent<VRRig>();

            var head = new GameObject("Head");
            head.tag = "MainCamera";
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            head.AddComponent<AudioListener>();
            var tpd = head.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(new InputAction("Head Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            var vignette = head.AddComponent<ComfortVignette>();
            vignette.ringMaterial = vignetteRing;
            vignette.solidMaterial = vignetteSolid;

            vr.head = head.transform;
            vr.leftHand = BuildHand(rig.transform, "LeftHand", "{LeftHand}", XRNode.LeftHand, new Vector3(-0.2f, 1.2f, 0.3f)).transform;
            vr.rightHand = BuildHand(rig.transform, "RightHand", "{RightHand}", XRNode.RightHand, new Vector3(0.2f, 1.2f, 0.3f)).transform;
            vr.leftHand.gameObject.AddComponent<WristMenu>();
            vr.rightHand.gameObject.AddComponent<FighterHUD>();
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
            return hand;
        }

        // ================================================================== the Training Grounds arena

        const float Half = 45f; // the fighting floor is 90 m across

        static void BuildTrainingGrounds()
        {
            var a = Group("TrainingGrounds", null, Vector3.zero);
            var scenery = Group("Scenery", a, Vector3.zero);
            int nextId = 0;

            // Floor, with a big ground slab under it in case anything falls.
            Prim(PrimitiveType.Cube, "Floor", scenery, new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f + 2f, 1f, Half * 2f + 2f), floor);
            Prim(PrimitiveType.Cube, "Outside", scenery, new Vector3(0f, -1.2f, 0f), new Vector3(240f, 1f, 240f), Mat("Outside", new Color(0.28f, 0.24f, 0.26f), 0.1f));

            // Walls round the edge (short enough to see over, plus a tall invisible barrier).
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 1.25f, Half + 0.5f), new Vector3(Half * 2f + 2f, 2.5f, 1f)), (new Vector3(0f, 1.25f, -Half - 0.5f), new Vector3(Half * 2f + 2f, 2.5f, 1f)),
                (new Vector3(Half + 0.5f, 1.25f, 0f), new Vector3(1f, 2.5f, Half * 2f + 2f)), (new Vector3(-Half - 0.5f, 1.25f, 0f), new Vector3(1f, 2.5f, Half * 2f + 2f)),
            })
            {
                Prim(PrimitiveType.Cube, "Wall", scenery, pos, size, wall);
                Prim(PrimitiveType.Cube, "WallGlow", scenery, pos + Vector3.up * 1.27f, new Vector3(size.x + 0.02f, 0.06f, size.z + 0.02f), neonRed, false);
                var barrier = Prim(PrimitiveType.Cube, "Barrier", scenery, pos + Vector3.up * 14f, new Vector3(size.x, 26f, size.z), wall);
                Object.DestroyImmediate(barrier.GetComponent<Renderer>());
                Object.DestroyImmediate(barrier.GetComponent<MeshFilter>());
            }

            // Tall light towers in the corners.
            foreach (var c in new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f) })
            {
                Vector3 p = c * (Half + 3f);
                Prim(PrimitiveType.Cube, "Tower", scenery, p + Vector3.up * 7f, new Vector3(0.8f, 14f, 0.8f), metal);
                Prim(PrimitiveType.Cube, "TowerLamp", scenery, p + Vector3.up * 14.3f - c.normalized * 0.4f, new Vector3(2.4f, 1.2f, 0.4f), neonGold, false,
                    Quaternion.LookRotation(-new Vector3(c.x, 0f, c.z)));
            }

            // The sparring ring in the middle: a low stage (you can step up on it), corner posts and glowing ropes.
            var ring = Group("SparringRing", scenery, Vector3.zero);
            const float r = 7f;
            Prim(PrimitiveType.Cube, "Stage", ring, new Vector3(0f, 0.15f, 0f), new Vector3(r * 2f, 0.3f, r * 2f), ringMat);
            Prim(PrimitiveType.Cube, "StageEdge", ring, new Vector3(0f, 0.16f, 0f), new Vector3(r * 2f + 0.15f, 0.26f, r * 2f + 0.15f), dark);
            Prim(PrimitiveType.Cylinder, "Logo", ring, new Vector3(0f, 0.305f, 0f), new Vector3(4.5f, 0.005f, 4.5f), neonRed, false);
            Prim(PrimitiveType.Cylinder, "LogoInner", ring, new Vector3(0f, 0.31f, 0f), new Vector3(4.1f, 0.005f, 4.1f), ringMat, false);
            Text(ring, "LogoText", "<b>NULLBOUND</b>", new Vector3(0f, 0.32f, 0f), Quaternion.Euler(90f, 0f, 0f), 9f, new Color(1f, 0.4f, 0.3f), new Vector2(4f, 1f));
            var posts = new[] { new Vector3(-r, 0f, -r), new Vector3(r, 0f, -r), new Vector3(r, 0f, r), new Vector3(-r, 0f, r) };
            for (int i = 0; i < 4; i++)
            {
                Prim(PrimitiveType.Cylinder, "Post", ring, posts[i] + Vector3.up * 1.0f, new Vector3(0.22f, 0.85f, 0.22f), i % 2 == 0 ? padRed : padBlue);
                Vector3 next = posts[(i + 1) % 4];
                for (int k = 0; k < 3; k++)
                {
                    float h = 0.75f + k * 0.42f;
                    var rp = Prim(PrimitiveType.Cylinder, "Rope", ring, Vector3.zero, Vector3.one, k == 1 ? Mat("RopeWhite", Color.white, 0.3f) : rope, false);
                    Vector3 p0 = posts[i] + Vector3.up * h, p1 = next + Vector3.up * h;
                    rp.transform.localPosition = (p0 + p1) * 0.5f;
                    rp.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (p1 - p0).normalized);
                    rp.transform.localScale = new Vector3(0.05f, (p1 - p0).magnitude * 0.5f, 0.05f);
                }
            }

            // Training dummies along the north side.
            var dummies = Group("Dummies", a, new Vector3(0f, 0f, 30f));
            for (int i = 0; i < 7; i++) Dummy(dummies, new Vector3((i - 3) * 3.4f, 0f, 0f));

            // Breakable stone pillars round the ring.
            var breakables = Group("Breakables", a, Vector3.zero);
            for (int pi = 0; pi < 8; pi++)
            {
                float pa = (pi + 0.5f) * Mathf.PI * 2f / 8f;
                var p = new Vector3(Mathf.Cos(pa) * 15f, 0f, Mathf.Sin(pa) * 15f);
                var pillar = Prim(PrimitiveType.Cylinder, "Pillar", breakables, p + Vector3.up * 2f, new Vector3(1.4f, 2f, 1.4f), stone);
                MakeBreakable(pillar, nextId++, 70f, stone);
                Prim(PrimitiveType.Cylinder, "PillarBase", scenery, p + Vector3.up * 0.1f, new Vector3(1.9f, 0.1f, 1.9f), dark);
            }
            // Crate stacks in the corners.
            foreach (var c in new[] { new Vector3(-24f, 0f, -6f), new Vector3(24f, 0f, -6f), new Vector3(-8f, 0f, -22f), new Vector3(8f, 0f, 22f), new Vector3(-36f, 0f, 2f), new Vector3(36f, 0f, 2f) })
            {
                for (int k = 0; k < 4; k++)
                {
                    Vector3 off = k < 3 ? new Vector3((k - 1) * 1.25f, 0.6f, 0f) : new Vector3(0f, 1.8f, 0f);
                    var box = Prim(PrimitiveType.Cube, "Crate", breakables, c + off, Vector3.one * 1.2f, crate, true, Quaternion.Euler(0f, k * 7f, 0f));
                    MakeBreakable(box, nextId++, 25f, crate);
                }
            }
            // A wall to smash through on the south side, built from blocks.
            for (int x = 0; x < 6; x++)
            for (int y = 0; y < 2; y++)
            {
                var block = Prim(PrimitiveType.Cube, "WallBlock", breakables, new Vector3(-7.5f + x * 3f, 0.75f + y * 1.5f, -26f), new Vector3(2.95f, 1.45f, 0.8f), concrete);
                MakeBreakable(block, nextId++, 45f, concrete);
            }

            BuildDowntown(a, breakables, ref nextId);
            BuildPlateau(scenery);
            BuildDojo(a, breakables, ref nextId);

            // KO board (east), how-to board (west), title sign (north).
            var score = Board(a, "KOBoard", new Vector3(Half - 0.2f, 4.2f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(12f, 5f), 3.2f, "KO BOARD", neonGold);
            score.alignment = TextAlignmentOptions.TopLeft;
            score.transform.parent.gameObject.AddComponent<ScoreBoard>().text = score;
            Posts(scenery, new Vector3(Half - 0.2f, 0f, 0f), true);
            Board(a, "HowTo", new Vector3(-Half + 0.2f, 4.2f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(12f, 5f), 2.6f,
                "<b><color=#ff6b4a>HOW TO FIGHT</color></b>\n" +
                "<b>PUNCH:</b> really punch! Faster swings hit harder. 4 hits in a row = LAUNCH\n" +
                "<b>BLOCK:</b> both fists up in front of your face\n" +
                "<b>A</b> jump   <b>B</b> dodge dash (you can't be hit mid-dash)\n" +
                "<b>X</b> special   <b>Menu button</b>: Quick Match, Training, fighters, settings\n" +
                "Left stick move, right stick turn. Smash walls, crates and pillars!", neonCyan);
            Posts(scenery, new Vector3(-Half + 0.2f, 0f, 0f), true);
            var title = Group("Title", scenery, new Vector3(0f, 9f, Half + 1.5f));
            Prim(PrimitiveType.Cube, "TitleBack", title, new Vector3(0f, 0f, 0.3f), new Vector3(22f, 4f, 0.4f), sign);
            Prim(PrimitiveType.Cube, "TitleGlow", title, new Vector3(0f, -2.05f, 0.1f), new Vector3(22.2f, 0.12f, 0.12f), neonRed, false);
            Text(title, "TitleText", "<b>NULLBOUND</b>", new Vector3(0f, 0.35f, 0f), Quaternion.identity, 24f, new Color(1f, 0.45f, 0.3f), new Vector2(21f, 2.6f));
            Text(title, "Subtitle", "TRAINING GROUNDS", new Vector3(0f, -1.3f, 0f), Quaternion.identity, 8f, Color.white, new Vector2(21f, 1f));
            for (int i = -1; i <= 1; i += 2) Prim(PrimitiveType.Cube, "TitlePost", title, new Vector3(i * 9f, -5f, 0.4f), new Vector3(0.5f, 6f, 0.5f), metal);

            // Spawn points: round the ring and in the open.
            var spawns = Group("SpawnPoints", a, Vector3.zero);
            for (int i = 0; i < 16; i++)
            {
                float ang = i * Mathf.PI * 2f / 16f;
                float d = i % 2 == 0 ? 4.5f : 22f;
                var sp = Group("Spawn" + i, spawns, new Vector3(Mathf.Cos(ang) * d, i % 2 == 0 ? 0.35f : 0.05f, Mathf.Sin(ang) * d));
                sp.gameObject.AddComponent<SpawnPoint>();
            }

            MarkStatic(scenery);
        }

        // ---------------- extra zones of the bigger arena

        /// <summary>North-east: a little city block with breakable buildings, a car and street lights.</summary>
        static void BuildDowntown(Transform a, Transform breakables, ref int nextId)
        {
            var town = Group("Downtown", a, new Vector3(28f, 0f, 22f));
            var road = Mat("Road", new Color(0.18f, 0.18f, 0.2f), 0.3f);
            var brick = Mat("Brick", new Color(0.62f, 0.3f, 0.22f), 0.15f);
            var glassMat = Mat("Window", new Color(0.4f, 0.6f, 0.8f), 0.9f, 0f, null, null, new Color(0.15f, 0.25f, 0.35f));
            Prim(PrimitiveType.Cube, "Road", town, new Vector3(0f, 0.01f, 0f), new Vector3(30f, 0.02f, 6f), road, false);
            for (int i = -2; i <= 2; i++) Prim(PrimitiveType.Cube, "RoadLine", town, new Vector3(i * 6f, 0.025f, 0f), new Vector3(2.5f, 0.01f, 0.2f), Mat("Line", Color.white, 0.3f), false);
            // Three buildings made of breakable blocks (smash a hole and walk through).
            for (int b = 0; b < 3; b++)
            {
                Vector3 c = new Vector3(-9f + b * 9f, 0f, 7f);
                var mat = b == 1 ? concrete : brick;
                for (int floorN = 0; floorN < 3; floorN++)
                for (int side = 0; side < 4; side++)
                {
                    Vector3 off = side == 0 ? new Vector3(0f, 0f, -2.5f) : side == 1 ? new Vector3(0f, 0f, 2.5f) : side == 2 ? new Vector3(-2.5f, 0f, 0f) : new Vector3(2.5f, 0f, 0f);
                    Vector3 size = side < 2 ? new Vector3(5.4f, 2f, 0.4f) : new Vector3(0.4f, 2f, 4.6f);
                    var w = Prim(PrimitiveType.Cube, "BuildingWall", breakables, town.position + c + off + Vector3.up * (1f + floorN * 2f), size, mat);
                    MakeBreakable(w, nextId++, 35f, mat);
                    if (side == 0) Prim(PrimitiveType.Cube, "Window", w.transform, new Vector3(0f, 0.1f, -0.55f), new Vector3(0.5f, 0.35f, 0.1f), glassMat, false);
                }
                Prim(PrimitiveType.Cube, "Roof", town, c + Vector3.up * 6.1f, new Vector3(5.6f, 0.2f, 5.6f), dark);
            }
            // A car to throw people into.
            var car = Prim(PrimitiveType.Cube, "Car", breakables, town.position + new Vector3(4f, 0.7f, -0.5f), new Vector3(4f, 1f, 1.9f), Mat("CarPaint", new Color(0.9f, 0.2f, 0.2f), 0.7f, 0.3f));
            MakeBreakable(car, nextId++, 30f, metal);
            Prim(PrimitiveType.Cube, "CarTop", car.transform, new Vector3(-0.05f, 0.85f, 0f), new Vector3(0.55f, 0.7f, 0.9f), glassMat, false);
            foreach (var wpos in new[] { new Vector3(-0.32f, -0.45f, 0.5f), new Vector3(0.32f, -0.45f, 0.5f), new Vector3(-0.32f, -0.45f, -0.5f), new Vector3(0.32f, -0.45f, -0.5f) })
                Prim(PrimitiveType.Cylinder, "Wheel", car.transform, wpos, new Vector3(0.18f, 0.06f, 0.35f), dark, false, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 lp = new Vector3(-12f + i * 8f, 0f, -3.6f);
                Prim(PrimitiveType.Cylinder, "LampPost", town, lp + Vector3.up * 2f, new Vector3(0.12f, 2f, 0.12f), metal);
                Prim(PrimitiveType.Sphere, "Lamp", town, lp + Vector3.up * 4.1f, Vector3.one * 0.4f, neonGold, false);
            }
        }

        /// <summary>South-west: raised rock platforms with ramps up (fight on high ground, knock people off).</summary>
        static void BuildPlateau(Transform scenery)
        {
            var rockMat = Mat("Rock", new Color(0.45f, 0.38f, 0.34f), 0.1f);
            var plat = Group("Plateau", scenery, new Vector3(-27f, 0f, -24f));
            Prim(PrimitiveType.Cube, "Platform", plat, new Vector3(0f, 1f, 0f), new Vector3(12f, 2f, 12f), rockMat);
            Prim(PrimitiveType.Cube, "PlatformTop", plat, new Vector3(0f, 2.02f, 0f), new Vector3(11.6f, 0.04f, 11.6f), ringMat, false);
            Ramp(plat, new Vector3(0f, 0f, 14f), new Vector3(0f, 2f, 6f), 4f, rockMat);
            Ramp(plat, new Vector3(14f, 0f, 0f), new Vector3(6f, 2f, 0f), 4f, rockMat);
            Prim(PrimitiveType.Cube, "Peak", plat, new Vector3(-3f, 3f, -3f), new Vector3(5f, 2f, 5f), rockMat);
            Ramp(plat, new Vector3(-3f, 2f, 4.5f), new Vector3(-3f, 4f, -0.5f), 2.5f, rockMat);
            foreach (var r in new[] { new Vector3(8f, 0f, 9f), new Vector3(-8f, 0f, 9f), new Vector3(9f, 0f, -8f) })
                Prim(PrimitiveType.Sphere, "Boulder", plat, r + Vector3.up * 0.8f, new Vector3(2.2f, 1.6f, 2f), rockMat);
        }

        /// <summary>North-west: a dojo with breakable paper-screen walls.</summary>
        static void BuildDojo(Transform a, Transform breakables, ref int nextId)
        {
            var dojo = Group("Dojo", a, new Vector3(-28f, 0f, 22f));
            var woodMat = Mat("DojoWood", new Color(0.45f, 0.28f, 0.16f), 0.3f);
            var paper = Mat("Paper", new Color(0.95f, 0.92f, 0.82f), 0.1f, 0f, null, null, new Color(0.25f, 0.22f, 0.15f));
            Prim(PrimitiveType.Cube, "DojoFloor", dojo, new Vector3(0f, 0.15f, 0f), new Vector3(14f, 0.3f, 12f), woodMat);
            foreach (var c in new[] { new Vector3(-7f, 0f, -6f), new Vector3(7f, 0f, -6f), new Vector3(-7f, 0f, 6f), new Vector3(7f, 0f, 6f) })
                Prim(PrimitiveType.Cylinder, "Pillar", dojo, c + Vector3.up * 2.2f, new Vector3(0.5f, 2f, 0.5f), padRed);
            var roof = Prim(PrimitiveType.Cube, "Roof", dojo, new Vector3(0f, 4.4f, 0f), new Vector3(16f, 0.4f, 14f), dark);
            Prim(PrimitiveType.Cube, "RoofTop", roof.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 2f, 0.7f), dark, false, Quaternion.Euler(0f, 45f, 0f));
            // Paper screens on three sides (front left open), all breakable.
            for (int i = 0; i < 4; i++)
            {
                var back = Prim(PrimitiveType.Cube, "Screen", breakables, dojo.position + new Vector3(-5.25f + i * 3.5f, 1.9f, 6f), new Vector3(3.4f, 3.2f, 0.12f), paper);
                MakeBreakable(back, nextId++, 15f, paper);
            }
            for (int i = 0; i < 3; i++)
                foreach (float x in new[] { -7f, 7f })
                {
                    var side = Prim(PrimitiveType.Cube, "Screen", breakables, dojo.position + new Vector3(x, 1.9f, -4f + i * 4f), new Vector3(0.12f, 3.2f, 3.9f), paper);
                    MakeBreakable(side, nextId++, 15f, paper);
                }
            Text(dojo, "DojoSign", "<b>DOJO</b>", new Vector3(0f, 3.8f, -6.1f), Quaternion.identity, 8f, new Color(1f, 0.4f, 0.3f), new Vector2(6f, 1.2f));
        }

        /// <summary>A walkable ramp from the low point to the high point.</summary>
        static void Ramp(Transform parent, Vector3 low, Vector3 high, float width, Material mat)
        {
            Vector3 d = high - low;
            float len = d.magnitude;
            var r = Prim(PrimitiveType.Cube, "Ramp", parent, (low + high) * 0.5f, new Vector3(width, 0.3f, len), mat);
            r.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            r.transform.localPosition += r.transform.localRotation * Vector3.down * 0.15f;
        }

        // ---------------- main menu room

        /// <summary>The room you stand in for the main menu, far from the arena (fog hides it).</summary>
        static void BuildMainMenuRoom()
        {
            var room = Group("MainMenuRoom", null, new Vector3(0f, 0f, -420f));
            var floorMat = Mat("MenuFloor", new Color(0.08f, 0.08f, 0.11f), 0.6f);
            Prim(PrimitiveType.Cylinder, "Floor", room, new Vector3(0f, -0.1f, 0f), new Vector3(16f, 0.1f, 16f), floorMat);
            Prim(PrimitiveType.Cylinder, "FloorRing", room, new Vector3(0f, 0.005f, 0f), new Vector3(15f, 0.01f, 15f), neonRed, false);
            Prim(PrimitiveType.Cylinder, "FloorInner", room, new Vector3(0f, 0.01f, 0f), new Vector3(14.6f, 0.01f, 14.6f), floorMat, false);
            Prim(PrimitiveType.Cylinder, "Spot", room, new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 0.01f, 1.2f), neonCyan, false);
            Prim(PrimitiveType.Cube, "Ground", room, new Vector3(0f, -1.5f, 0f), new Vector3(120f, 1f, 120f), Mat("Outside", new Color(0.28f, 0.24f, 0.26f), 0.1f));
            // Tall glowing pillars all round, and an invisible wall so you can't walk off.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * Mathf.PI * 2f / 12f;
                Vector3 p = new Vector3(Mathf.Sin(ang) * 8.5f, 0f, Mathf.Cos(ang) * 8.5f);
                Prim(PrimitiveType.Cube, "Pillar", room, p + Vector3.up * 4f, new Vector3(0.5f, 8f, 0.5f), dark);
                Prim(PrimitiveType.Cube, "PillarGlow", room, p + Vector3.up * 4f, new Vector3(0.08f, 8f, 0.55f), i % 2 == 0 ? neonRed : neonCyan, false, Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f));
                var wallPiece = Prim(PrimitiveType.Cube, "Barrier", room, p * 0.88f + Vector3.up * 3f, new Vector3(4.5f, 6f, 0.5f), dark, true, Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f));
                Object.DestroyImmediate(wallPiece.GetComponent<Renderer>());
                Object.DestroyImmediate(wallPiece.GetComponent<MeshFilter>());
            }
            var arch = Group("Arch", room, new Vector3(0f, 0f, 6f));
            for (int i = 0; i < 16; i++)
            {
                float ang = Mathf.PI * i / 15f;
                Prim(PrimitiveType.Cube, "ArchSeg", arch, new Vector3(Mathf.Cos(ang) * 4.5f, 2f + Mathf.Sin(ang) * 4.5f, 0f), new Vector3(0.5f, 0.5f, 0.3f), i % 2 == 0 ? neonRed : neonGold, false, Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg));
            }
            foreach (var lp in new[] { new Vector3(-3f, 5f, 2f), new Vector3(3f, 5f, 2f) })
            {
                var light = new GameObject("MenuLight").AddComponent<Light>();
                light.transform.SetParent(room, false);
                light.transform.localPosition = lp;
                light.type = LightType.Point;
                light.range = 12f;
                light.intensity = 1.4f;
                light.color = new Color(1f, 0.85f, 0.8f);
            }
            var menu = new GameObject("MainMenu");
            menu.transform.SetParent(room, false);
            menu.AddComponent<MainMenu>();
            MarkStatic(room);
        }

        static void Posts(Transform parent, Vector3 boardPos, bool alongZ)
        {
            foreach (float s in new[] { -4f, 4f })
            {
                Vector3 p = boardPos + (alongZ ? new Vector3(0f, 0f, s) : new Vector3(s, 0f, 0f));
                Prim(PrimitiveType.Cube, "BoardPost", parent, p + Vector3.up * 0.85f, new Vector3(0.3f, 1.7f, 0.3f), metal);
            }
        }

        static void MakeBreakable(GameObject go, int id, float toughness, Material chunks)
        {
            var b = go.AddComponent<Breakable>();
            b.id = id;
            b.toughness = toughness;
            b.chunkMaterial = chunks;
            go.AddComponent<Hurtbox>().owner = b;
        }

        static void Dummy(Transform parent, Vector3 pos)
        {
            var d = Group("Dummy", parent, pos);
            Prim(PrimitiveType.Cylinder, "Base", d, new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.1f, 0.9f), dark);
            Prim(PrimitiveType.Cylinder, "Pole", d, new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 0.4f, 0.12f), metal, false);
            var pivot = Group("Pivot", d, new Vector3(0f, 0.85f, 0f));
            var bag = Prim(PrimitiveType.Capsule, "Bag", pivot, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.55f, 0.55f), bagMat, false);
            Prim(PrimitiveType.Sphere, "Head", pivot, new Vector3(0f, 1.25f, 0f), Vector3.one * 0.34f, bagMat, false);
            Prim(PrimitiveType.Cube, "Band", pivot, new Vector3(0f, 0.75f, 0f), new Vector3(0.57f, 0.08f, 0.57f), neonGold, false);
            var hit = new GameObject("Hurtbox");
            hit.transform.SetParent(pivot, false);
            hit.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var cap = hit.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = 0.32f;
            cap.height = 1.5f;
            var dummy = d.gameObject.AddComponent<TrainingDummy>();
            dummy.bag = pivot;
            hit.AddComponent<Hurtbox>().owner = dummy;
            dummy.readout = Text(d, "Readout", "PUNCH ME", new Vector3(0f, 2.75f, 0f), Quaternion.identity, 2f, Color.white, new Vector2(2.6f, 0.8f));
            _ = bag;
        }
    }
}
