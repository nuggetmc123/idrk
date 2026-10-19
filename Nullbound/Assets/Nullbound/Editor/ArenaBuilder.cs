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
        public const int WorldVersion = 6; // 6 = menu has its own starry sky box
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

        /// <summary>A walkable ramp from the low point to the high point.</summary>
        static void Ramp(Transform parent, Vector3 low, Vector3 high, float width, Material mat)
        {
            Vector3 d = high - low;
            float len = d.magnitude;
            var r = Prim(PrimitiveType.Cube, "Ramp", parent, (low + high) * 0.5f, new Vector3(width, 0.3f, len), mat);
            r.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            r.transform.localPosition += r.transform.localRotation * Vector3.down * 0.15f;
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

    }
}
