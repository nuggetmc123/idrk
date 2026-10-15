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
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Menu: KartVR > Build Prototype World.
    /// Generates the Kart prefab and a complete playable scene (Hub, Race Track,
    /// Bumper Arena, Free Roam) out of simple shapes, so you can play right
    /// away and then replace the blocks with real art.
    /// </summary>
    public static class KartWorldBuilder
    {
        const string Root = "Assets/KartVR";
        const string ScenePath = Root + "/Scenes/KartWorld.unity";
        const string PrefabPath = Root + "/Prefabs/Kart.prefab";

        static readonly Vector3 RaceOrigin = new Vector3(0f, 0f, 260f);
        static readonly Vector3 BumperOrigin = new Vector3(260f, 0f, 0f);
        static readonly Vector3 FreeRoamOrigin = new Vector3(-320f, 0f, 0f);

        static Material white, dark, skin, hubGround, grass, asphalt, wallRed, wallWhite, portalRace,
            portalBumper, portalFree, boost, arena, lava, rampMat, kartBody, sign;

        [MenuItem("KartVR/Build Prototype World")]
        public static void Build()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("KartVR",
                    "TextMeshPro resources are missing.\n\nGo to Window > TextMeshPro > Import TMP Essential Resources, then run this again.",
                    "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("KartVR",
                    "This will (re)create " + PrefabPath + " and " + ScenePath + ".\nAny edits you made to those two files will be replaced.",
                    "Build it", "Cancel"))
                return;

            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Scenes");
            CreateMaterials();

            var kartPrefab = BuildKartPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam) Object.DestroyImmediate(defaultCam);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 400f;
            RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);

            BuildManagers(kartPrefab);
            BuildRig();
            BuildHub();
            BuildRaceTrack();
            BuildBumperArena();
            BuildFreeRoam();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("KartVR",
                "Done! KartWorld scene is open and set as the first scene in Build Settings.\n\n" +
                "Make sure you've set your Photon App ID (Fusion Hub), then press Play.\n" +
                "No headset? Use W/A/S/D, hold right mouse to look, click buttons with left mouse.",
                "Let's go");
        }

        // ------------------------------------------------------------------ helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static Material Mat(string name, Color c)
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
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateMaterials()
        {
            white = Mat("White", Color.white);
            dark = Mat("Dark", new Color(0.15f, 0.15f, 0.17f));
            skin = Mat("Avatar", new Color(1f, 0.85f, 0.7f));
            hubGround = Mat("HubGround", new Color(0.55f, 0.6f, 0.7f));
            grass = Mat("Grass", new Color(0.35f, 0.65f, 0.3f));
            asphalt = Mat("Asphalt", new Color(0.25f, 0.25f, 0.28f));
            wallRed = Mat("WallRed", new Color(0.85f, 0.15f, 0.15f));
            wallWhite = Mat("WallWhite", new Color(0.95f, 0.95f, 0.95f));
            portalRace = Mat("PortalRace", new Color(1f, 0.35f, 0.2f));
            portalBumper = Mat("PortalBumper", new Color(0.3f, 0.6f, 1f));
            portalFree = Mat("PortalFreeRoam", new Color(0.4f, 0.9f, 0.4f));
            boost = Mat("Boost", new Color(1f, 0.6f, 0f));
            arena = Mat("Arena", new Color(0.3f, 0.3f, 0.45f));
            lava = Mat("Lava", new Color(1f, 0.3f, 0.05f));
            rampMat = Mat("Ramp", new Color(0.95f, 0.75f, 0.2f));
            kartBody = Mat("KartBody", new Color(0.9f, 0.2f, 0.2f));
            sign = Mat("Sign", new Color(0.1f, 0.1f, 0.15f));
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
            go.GetComponent<Renderer>().sharedMaterial = mat;
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

        static Transform Group(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            return go.transform;
        }

        /// <summary>A sign board with text on its -Z face (readable when you look toward +Z).</summary>
        static TextMeshPro Board(Transform parent, string name, Vector3 localPos, Quaternion rot, Vector2 size, float fontSize, string text)
        {
            var b = new GameObject(name).transform;
            b.SetParent(parent, false);
            b.localPosition = localPos;
            b.localRotation = rot;
            Prim(PrimitiveType.Cube, "Backing", b, new Vector3(0f, 0f, 0.2f), new Vector3(size.x, size.y, 0.3f), sign);
            return Text(b, "Text", text, new Vector3(0f, 0f, 0.02f), Quaternion.identity, fontSize, Color.white, size * 0.94f);
        }

        // ------------------------------------------------------------------ kart prefab

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
            root.AddComponent<KartController>();
            root.AddComponent<KartAvatar>();
            root.AddComponent<KartDashboard>();

            var t = root.transform;
            Prim(PrimitiveType.Cube, "Body", t, new Vector3(0f, 0.45f, 0.05f), new Vector3(1.1f, 0.3f, 1.7f), kartBody, false);
            Prim(PrimitiveType.Cube, "Nose", t, new Vector3(0f, 0.4f, 1.0f), new Vector3(1.0f, 0.2f, 0.35f), dark, false);
            Prim(PrimitiveType.Cube, "RearBumper", t, new Vector3(0f, 0.4f, -0.95f), new Vector3(1.2f, 0.2f, 0.15f), dark, false);
            Prim(PrimitiveType.Cube, "SeatBack", t, new Vector3(0f, 0.8f, -0.62f), new Vector3(0.6f, 0.55f, 0.1f), dark, false);
            Prim(PrimitiveType.Cube, "Dash", t, new Vector3(0f, 0.68f, 0.55f), new Vector3(0.9f, 0.18f, 0.3f), dark, false);
            Prim(PrimitiveType.Cylinder, "SteeringWheel", t, new Vector3(0f, 0.85f, 0.22f), new Vector3(0.32f, 0.015f, 0.32f), dark, false,
                Quaternion.Euler(-60f, 0f, 0f));

            var wheelScale = new Vector3(0.44f, 0.08f, 0.44f);
            var wheelRot = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, "WheelFL", t, new Vector3(-0.64f, 0.37f, 0.65f), wheelScale, dark, false, wheelRot);
            Prim(PrimitiveType.Cylinder, "WheelFR", t, new Vector3(0.64f, 0.37f, 0.65f), wheelScale, dark, false, wheelRot);
            Prim(PrimitiveType.Cylinder, "WheelBL", t, new Vector3(-0.64f, 0.37f, -0.6f), wheelScale, dark, false, wheelRot);
            Prim(PrimitiveType.Cylinder, "WheelBR", t, new Vector3(0.64f, 0.37f, -0.6f), wheelScale, dark, false, wheelRot);

            new GameObject("Seat").transform.SetParent(t, false);
            t.Find("Seat").localPosition = new Vector3(0f, 0.45f, -0.3f);
            new GameObject("HeadAnchor").transform.SetParent(t, false);
            t.Find("HeadAnchor").localPosition = new Vector3(0f, 1.15f, -0.3f);

            Text(t, "Dashboard", "", new Vector3(0f, 0.98f, 0.55f), Quaternion.Euler(25f, 0f, 0f), 0.4f, Color.white, new Vector2(0.8f, 0.45f));
            Text(t, "BigDisplay", "", new Vector3(0f, 1.5f, 2.2f), Quaternion.identity, 5f, new Color(1f, 0.85f, 0.2f), new Vector2(2.5f, 0.8f));
            Text(t, "NameTag", "Racer", new Vector3(0f, 1.9f, -0.3f), Quaternion.identity, 2.5f, Color.white, new Vector2(3f, 0.5f));

            // What other players see of you.
            var head = Prim(PrimitiveType.Sphere, "NetHead", t, new Vector3(0f, 1.15f, -0.3f), new Vector3(0.24f, 0.27f, 0.26f), skin, false);
            Prim(PrimitiveType.Cube, "Visor", head.transform, new Vector3(0f, 0.1f, 0.4f), new Vector3(0.8f, 0.28f, 0.3f), dark, false);
            Prim(PrimitiveType.Sphere, "NetLeftHand", t, new Vector3(-0.2f, 0.85f, 0.15f), Vector3.one * 0.1f, skin, false);
            Prim(PrimitiveType.Sphere, "NetRightHand", t, new Vector3(0.2f, 0.85f, 0.15f), Vector3.one * 0.1f, skin, false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ managers + rig

        static void BuildManagers(GameObject kartPrefab)
        {
            var sm = new GameObject("SessionManager").AddComponent<SessionManager>();
            sm.kartPrefab = kartPrefab.GetComponent<NetworkObject>();
            sm.baseMaterial = white;

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
            rig.transform.position = new Vector3(0f, 0f, -24f);
            var kr = rig.AddComponent<KartRig>();

            var head = new GameObject("Head");
            head.tag = "MainCamera";
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 600f;
            head.AddComponent<AudioListener>();
            var tpd = head.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(new InputAction("Head Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

            kr.head = head.transform;
            kr.leftHand = BuildHand(rig.transform, "LeftHand", "{LeftHand}", XRNode.LeftHand, new Vector3(-0.2f, 0.9f, 0.3f)).transform;
            kr.rightHand = BuildHand(rig.transform, "RightHand", "{RightHand}", XRNode.RightHand, new Vector3(0.2f, 0.9f, 0.3f)).transform;
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

            Prim(PrimitiveType.Sphere, "Visual", hand.transform, Vector3.zero, Vector3.one * 0.07f, skin, false);
            return hand;
        }

        // ------------------------------------------------------------------ hub

        static void BuildHub()
        {
            var hub = Group("Hub", Vector3.zero);
            Prim(PrimitiveType.Cube, "Ground", hub, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), hubGround);
            Prim(PrimitiveType.Cube, "WallN", hub, new Vector3(0f, 0.5f, 40f), new Vector3(80f, 1f, 0.6f), wallWhite);
            Prim(PrimitiveType.Cube, "WallS", hub, new Vector3(0f, 0.5f, -40f), new Vector3(80f, 1f, 0.6f), wallWhite);
            Prim(PrimitiveType.Cube, "WallE", hub, new Vector3(40f, 0.5f, 0f), new Vector3(0.6f, 1f, 80f), wallWhite);
            Prim(PrimitiveType.Cube, "WallW", hub, new Vector3(-40f, 0.5f, 0f), new Vector3(0.6f, 1f, 80f), wallWhite);

            // Spawn grid: 3 rows of 4, facing the portals (+Z).
            var spawn = new GameObject("HubSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Hub;
            spawn.killY = -10f;
            spawn.transform.SetParent(hub, false);
            spawn.transform.localPosition = new Vector3(0f, 0f, -22f);
            for (int i = 0; i < 12; i++)
            {
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = new Vector3((i % 4 - 1.5f) * 3.5f, 0f, -(i / 4) * 4f);
            }

            Portal(hub, "RacePortal", KartMode.Race, new Vector3(-15f, 0f, 24f), portalRace, "RACE");
            Portal(hub, "BumperPortal", KartMode.Bumper, new Vector3(0f, 0f, 24f), portalBumper, "BUMPER KARTS");
            Portal(hub, "FreeRoamPortal", KartMode.FreeRoam, new Vector3(15f, 0f, 24f), portalFree, "FREE ROAM");

            Terminal(hub, "CodeTerminalLeft", new Vector3(-9f, 0f, -10f));
            Terminal(hub, "CodeTerminalRight", new Vector3(9f, 0f, -10f));

            var board = Board(hub, "LobbyBoard", new Vector3(0f, 8f, 36f), Quaternion.identity, new Vector2(18f, 7f), 5f, "KART VR");
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Lobby;
            info.text = board;
            info.title = "KART VR";

            Board(hub, "HelpBoard", new Vector3(-24f, 3f, -14f), Quaternion.Euler(0f, -60f, 0f), new Vector2(9f, 4.5f), 2.6f,
                "<b>CONTROLS</b>\nRight trigger: gas\nLeft trigger: brake / reverse\nLeft stick: steer\nGrip: drift (hold through corners)\n" +
                "B: respawn   X: recenter   Y: wrist menu");
        }

        static void Portal(Transform parent, string name, KartMode mode, Vector3 pos, Material mat, string label)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            Prim(PrimitiveType.Cube, "PostL", p, new Vector3(-3f, 2.25f, 0f), new Vector3(0.6f, 4.5f, 0.6f), mat);
            Prim(PrimitiveType.Cube, "PostR", p, new Vector3(3f, 2.25f, 0f), new Vector3(0.6f, 4.5f, 0.6f), mat);
            Prim(PrimitiveType.Cube, "Top", p, new Vector3(0f, 4.75f, 0f), new Vector3(6.6f, 0.6f, 0.6f), mat);
            var field = Prim(PrimitiveType.Cube, "Field", p, new Vector3(0f, 2.2f, 0f), new Vector3(5.4f, 4.4f, 0.3f), mat);
            field.GetComponent<Collider>().isTrigger = true;
            field.AddComponent<ModePortal>().targetMode = mode;
            Text(p, "Label", label, new Vector3(0f, 5.8f, 0f), Quaternion.identity, 9f, Color.white, new Vector2(12f, 1.5f));
        }

        static void Terminal(Transform parent, string name, Vector3 pos)
        {
            // The keypad overhangs the kart's nose so you can reach it from the seat.
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            Prim(PrimitiveType.Cube, "Post", t, new Vector3(0f, 0.9f, 1.25f), new Vector3(0.25f, 1.8f, 0.25f), dark);
            Prim(PrimitiveType.Cube, "Arm", t, new Vector3(0f, 1.7f, 0.65f), new Vector3(0.12f, 0.12f, 1.25f), dark, false);
            var pad = new GameObject("Keypad");
            pad.transform.SetParent(t, false);
            pad.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            pad.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            pad.AddComponent<KeypadTerminal>();
            Text(t, "Title", "LOBBY CODES\n<size=60%>drive up and poke the keys</size>", new Vector3(0f, 2.2f, 0.1f), Quaternion.identity, 3f,
                Color.white, new Vector2(4f, 1.2f));
        }

        // ------------------------------------------------------------------ race track

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

        static void BuildRaceTrack()
        {
            var race = Group("RaceTrack", RaceOrigin);
            Prim(PrimitiveType.Cube, "Ground", race, new Vector3(0f, -0.5f, 0f), new Vector3(220f, 1f, 160f), grass);

            const int segments = 120;
            const float halfWidth = 7.5f;
            var road = Group("Road", RaceOrigin);
            road.SetParent(race, true);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i * Mathf.PI * 2f / segments, t1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 a = TrackPoint(t0), b = TrackPoint(t1);
                Vector3 dir = b - a;
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                var seg = Prim(PrimitiveType.Cube, "Road" + i, road, Vector3.zero, new Vector3(halfWidth * 2f, 0.02f, dir.magnitude + 0.4f), asphalt, false);
                seg.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 0.01f, rot);

                // Walls on both sides, alternating red/white.
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 na = Vector3.Cross(Vector3.up, TrackTangent(t0)) * side * halfWidth;
                    Vector3 nb = Vector3.Cross(Vector3.up, TrackTangent(t1)) * side * halfWidth;
                    Vector3 wa = a + na, wb = b + nb;
                    Vector3 wd = wb - wa;
                    var wall = Prim(PrimitiveType.Cube, "Wall" + i + (side < 0 ? "R" : "L"), road, Vector3.zero,
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
                if (k == 0)
                {
                    Prim(PrimitiveType.Cube, "FinishLine", cp.transform, new Vector3(0f, 0.025f, 0f), new Vector3(halfWidth * 2f, 0.02f, 1f), wallWhite, false);
                    Prim(PrimitiveType.Cube, "GantryL", cp.transform, new Vector3(-halfWidth - 0.8f, 3f, 0f), new Vector3(0.5f, 6f, 0.5f), dark);
                    Prim(PrimitiveType.Cube, "GantryR", cp.transform, new Vector3(halfWidth + 0.8f, 3f, 0f), new Vector3(0.5f, 6f, 0.5f), dark);
                    Prim(PrimitiveType.Cube, "GantryTop", cp.transform, new Vector3(0f, 6f, 0f), new Vector3(halfWidth * 2f + 2f, 0.8f, 0.5f), dark, false);
                    Text(cp.transform, "FinishText", "START / FINISH", new Vector3(0f, 6f, -0.3f), Quaternion.identity, 6f, Color.white, new Vector2(15f, 0.8f));
                }
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
            }

            // Boost pads on the two long sweeps.
            foreach (float t in new[] { 0f, Mathf.PI })
            {
                var pad = Prim(PrimitiveType.Cube, "BoostPad", race, Vector3.zero, new Vector3(5f, 0.05f, 3f), boost);
                pad.transform.SetPositionAndRotation(TrackPoint(t) + Vector3.up * 0.03f, Quaternion.LookRotation(TrackTangent(t), Vector3.up));
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
            }

            var board = Board(race, "RaceBoard", new Vector3(0f, 8f, 0f), Quaternion.identity, new Vector2(14f, 9f), 4f, "RACE");
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Race;
            info.text = board;
            info.title = "RACE";
        }

        // ------------------------------------------------------------------ bumper arena

        static void BuildBumperArena()
        {
            var b = Group("BumperArena", BumperOrigin);

            // Flat round platform floating over lava. Fall off = lose a point.
            var floor = Prim(PrimitiveType.Cylinder, "Platform", b, new Vector3(0f, -1f, 0f), new Vector3(44f, 1f, 44f), arena);
            Object.DestroyImmediate(floor.GetComponent<Collider>()); // cylinder primitives use a capsule collider; swap for the real shape
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Prim(PrimitiveType.Cylinder, "Ring", b, new Vector3(0f, -0.99f, 0f), new Vector3(30f, 1f, 30f), wallWhite, false);
            Prim(PrimitiveType.Cylinder, "Center", b, new Vector3(0f, -0.98f, 0f), new Vector3(28f, 1f, 28f), arena, false);
            Prim(PrimitiveType.Cube, "Lava", b, new Vector3(0f, -12f, 0f), new Vector3(240f, 1f, 240f), lava, false);

            // A few tire stacks to bounce around.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                Prim(PrimitiveType.Cylinder, "Tires" + i, b, new Vector3(Mathf.Sin(a) * 8f, 0.5f, Mathf.Cos(a) * 8f), new Vector3(1.6f, 0.5f, 1.6f), dark);
            }

            var spawn = new GameObject("BumperSpawn").AddComponent<SpawnArea>();
            spawn.mode = KartMode.Bumper;
            spawn.killY = -6f;
            spawn.transform.SetParent(b, false);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var pos = new Vector3(Mathf.Sin(a) * 15f, 0f, Mathf.Cos(a) * 15f);
                var slot = new GameObject("Slot" + i).transform;
                slot.SetParent(spawn.transform, false);
                slot.localPosition = pos;
                slot.localRotation = Quaternion.LookRotation(-pos, Vector3.up);
            }

            var board = Board(b, "BumperBoard", new Vector3(0f, 9f, 32f), Quaternion.identity, new Vector2(14f, 9f), 4f, "BUMPER KARTS");
            var info = board.transform.parent.gameObject.AddComponent<InfoBoard>();
            info.kind = InfoBoard.Kind.Bumper;
            info.text = board;
            info.title = "BUMPER KARTS";
        }

        // ------------------------------------------------------------------ free roam

        static void Ramp(Transform parent, string name, Vector3 pos, float yaw, float length, float width, float angle)
        {
            // Tilted box whose back edge is buried so the slope starts flush with the ground.
            const float thickness = 1f;
            float a = angle * Mathf.Deg2Rad;
            float y = length * 0.5f * Mathf.Sin(a) - thickness * 0.5f * Mathf.Cos(a);
            var r = Prim(PrimitiveType.Cube, name, parent, Vector3.zero, new Vector3(width, thickness, length), rampMat);
            r.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-angle, 0f, 0f);
            r.transform.localPosition = pos + new Vector3(0f, y, 0f);
        }

        static void BuildFreeRoam()
        {
            var f = Group("FreeRoam", FreeRoamOrigin);
            Prim(PrimitiveType.Cube, "Ground", f, new Vector3(0f, -0.5f, 0f), new Vector3(300f, 1f, 300f), grass);

            Ramp(f, "Ramp1", new Vector3(0f, 0f, 30f), 0f, 12f, 8f, 14f);
            Ramp(f, "Ramp2", new Vector3(35f, 0f, 10f), 90f, 16f, 10f, 20f);
            Ramp(f, "Ramp3", new Vector3(-40f, 0f, 40f), -45f, 20f, 10f, 24f);
            Ramp(f, "MegaRamp", new Vector3(0f, 0f, 90f), 0f, 30f, 14f, 28f);

            // Raised plateau with a ramp up to it.
            Prim(PrimitiveType.Cube, "Plateau", f, new Vector3(-70f, 2.5f, -40f), new Vector3(30f, 5f, 30f), arena);
            Ramp(f, "PlateauRamp", new Vector3(-70f, 0f, -14f), 180f, 22f, 10f, 13.5f);

            // Rolling hills (buried spheres).
            Prim(PrimitiveType.Sphere, "Hill1", f, new Vector3(60f, -14f, 60f), Vector3.one * 40f, grass);
            Prim(PrimitiveType.Sphere, "Hill2", f, new Vector3(80f, -20f, -50f), Vector3.one * 50f, grass);
            Prim(PrimitiveType.Sphere, "Hill3", f, new Vector3(-30f, -16f, -90f), Vector3.one * 40f, grass);

            // Slalom poles.
            for (int i = 0; i < 8; i++)
                Prim(PrimitiveType.Cylinder, "Pole" + i, f, new Vector3(-20f + (i % 2) * 6f, 1.5f, -20f - i * 10f), new Vector3(0.6f, 1.5f, 0.6f), wallRed);

            // Boost strip.
            for (int i = 0; i < 4; i++)
            {
                var pad = Prim(PrimitiveType.Cube, "BoostPad" + i, f, new Vector3(30f, 0.03f, -30f - i * 12f), new Vector3(5f, 0.05f, 3f), boost);
                pad.GetComponent<Collider>().isTrigger = true;
                pad.AddComponent<BoostPad>();
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

            Board(f, "FreeRoamSign", new Vector3(0f, 4f, 14f), Quaternion.identity, new Vector2(10f, 3f), 3f,
                "<b>FREE ROAM</b>\nJump the ramps! Wrist menu > Hub to go back.");
        }
    }
}
