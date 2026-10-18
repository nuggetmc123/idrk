using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// Free Roam build mode. Click the right thumbstick (or press B on the
    /// keyboard) to hop out of your kart and fly around; point with your right
    /// hand and place ramps, roads, trees and more. Everyone in the session sees
    /// what you build. You can't fly or build past the map's barrier.
    ///
    /// VR: left stick fly (hold left grip = fast), right stick up/down = fly up/down,
    /// right stick left/right = turn, right trigger = place, right grip = rotate,
    /// A / X = next / previous item, left trigger = delete what you point at, B = undo.
    /// Keyboard: WASD fly, Space / Ctrl up / down, Q/E turn, F place, T rotate,
    /// 1 / 2 change item, G delete, R undo, Shift fast, B leave.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        public static bool Active { get; private set; }
        static BuildMode instance;

        public float flySpeed = 14f;
        public float fastFlySpeed = 40f;
        public float verticalSpeed = 9f;
        public float reach = 80f;

        int type;
        float yaw;
        bool wasPlace, wasDelete, wasRotate, wasNext, wasPrev, turnLatched;
        float nextPlace;
        GameObject ghost;
        int ghostType = -1;
        LineRenderer laser;
        TextMeshPro hud;
        BuildPiece pointed;
        Vector3 target;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            Active = false; // in case Unity kept the old value between Play sessions
            var go = new GameObject("BuildMode");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BuildMode>();
        }

        /// <summary>Build mode works in Free Roam, once your kart is in the world.</summary>
        public static bool Available =>
            KartModeInfo.CanBuild(GameState.CurrentMode) && !OnFoot.Active && KartController.Local != null && KartController.Local.IsReady &&
            KartRig.Instance != null;

        /// <summary>Enter or leave build mode (wrist menu button, right stick click, B key).</summary>
        public static void Toggle()
        {
            if (instance == null) Create();
            if (Active) instance.SetActive(false);
            else if (!Available) Status("Build mode works in Free Roam. Go through the FREE ROAM portal in the hub!");
            else if (BuildManager.Instance == null) Status("Build mode needs the newest world: in Unity click KartVR > Build Prototype World.");
            else instance.SetActive(true);
        }

        static void Status(string text)
        {
            if (SessionManager.Instance != null) SessionManager.Instance.SetStatus(text, 5f);
            Debug.Log("[KartVR] " + text);
        }

        void SetActive(bool on)
        {
            Active = on;
            AudioManager.Play(AudioManager.Sfx.Teleport, 0.6f, on ? 1.2f : 0.8f);
            var rig = KartRig.Instance;
            if (on)
            {
                rig.Detach();
                rig.transform.position += Vector3.up * 3f;
                Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up);
                yaw = fwd.sqrMagnitude > 0.001f ? Mathf.Round(Quaternion.LookRotation(fwd).eulerAngles.y / 45f) * 45f : 0f;
                EnsureVisuals(rig);
                ghostType = -1;
                Status("BUILD MODE: fly around and place things. Click the right stick again to drive.");
            }
            else
            {
                if (ghost) Destroy(ghost);
                ghost = null;
                if (laser) laser.enabled = false;
                if (hud) hud.gameObject.SetActive(false);
                // KartRig puts you back in your kart's seat on its next update.
            }
        }

        void EnsureVisuals(KartRig rig)
        {
            if (laser == null)
            {
                var lg = new GameObject("BuildLaser");
                lg.transform.SetParent(transform, false);
                laser = lg.AddComponent<LineRenderer>();
                laser.positionCount = 2;
                laser.useWorldSpace = true;
                laser.startWidth = 0.015f;
                laser.endWidth = 0.04f;
                laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                laser.receiveShadows = false;
            }
            laser.enabled = true;
            if (hud == null || hud.transform.parent != rig.head)
            {
                if (hud) Destroy(hud.gameObject);
                hud = KartUI.Label(rig.head, "", new Vector3(0f, -0.32f, 0.9f), 0.28f, Color.white, new Vector2(1.1f, 0.4f));
                hud.gameObject.name = "BuildHud";
            }
            hud.gameObject.SetActive(true);
        }

        void Update()
        {
            // In the hub the right stick click means "run", so it only toggles build mode in Free Roam.
            if (KartInput.BuildTogglePressed && GameState.CurrentMode != KartMode.Hub && !OnFoot.Active) Toggle(); // on foot it means "run"
            if (Active && !Available) SetActive(false);
            if (!Active) return;

            var rig = KartRig.Instance;
            EnsureVisuals(rig);
            Fly(rig);
            Point(rig);
            Buttons();
            Hud();
        }

        // ---------------- flying (never past the barrier)

        void Fly(KartRig rig)
        {
            float dt = Time.deltaTime;
            Vector2 stick = KartInput.Move;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float speed = KartInput.BuildFastHeld ? fastFlySpeed : flySpeed;
            Vector3 v = (fwd * stick.y + right * stick.x) * speed + Vector3.up * KartInput.FlyVertical * verticalSpeed;
            rig.transform.position += v * dt;

            // Snap turn on the right stick (pushed mostly sideways).
            float turn = Mathf.Abs(KartInput.FlyVertical) < 0.6f ? KartInput.Turn : 0f;
            float degrees = GameSettings.TurnAmount(turn, ref turnLatched, dt); // snap or smooth (menu > Controls)
            if (degrees != 0f) rig.transform.RotateAround(rig.head.position, Vector3.up, degrees);

            // The barrier: keep your head inside the dome and above the ground.
            Vector3 head = rig.head.position;
            Vector3 inside = KartMaps.ClampInside(GameState.CurrentMode, head, 3f);
            inside.y = Mathf.Max(inside.y, KartMaps.Get(GameState.CurrentMode).center.y + 1.2f);
            if (GameState.CurrentMode == KartMode.Backrooms) inside.y = Mathf.Min(inside.y, 4.6f); // stay under the ceiling
            rig.transform.position += inside - head;
        }

        // ---------------- pointing

        void Point(KartRig rig)
        {
            Ray ray;
            if (KartRig.IsXR)
                ray = new Ray(rig.rightHand.position, rig.rightHand.rotation * Quaternion.Euler(35f, 0f, 0f) * Vector3.forward);
            else
            {
                var cam = rig.head.GetComponent<Camera>();
                ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.45f, 0f)) : new Ray(rig.head.position, rig.head.forward);
            }

            pointed = null;
            bool hitSomething = false;
            RaycastHit best = default(RaycastHit);
            float bestDist = float.MaxValue;
            foreach (var h in Physics.RaycastAll(ray, reach, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                var piece = h.collider.GetComponentInParent<BuildPiece>();
                if (h.collider.isTrigger && piece == null) continue; // portals, checkpoints, boost triggers in the map
                if (h.collider.attachedRigidbody != null && h.collider.attachedRigidbody.GetComponent<KartController>() != null) continue;
                if (h.distance >= bestDist) continue;
                bestDist = h.distance;
                best = h;
                hitSomething = true;
                pointed = piece;
            }

            if (hitSomething)
                target = best.normal.y > 0.6f ? best.point : best.point + best.normal * 1f;
            else
                target = ray.origin + ray.direction * 15f; // nothing there: build in the air
            Vector3 inside = KartMaps.ClampInside(GameState.CurrentMode, target + Vector3.up * 0.5f, 6f) + Vector3.down * 0.5f;
            target = new Vector3(inside.x, Mathf.Max(target.y, KartMaps.Get(GameState.CurrentMode).center.y), inside.z);

            if (ghostType != type || ghost == null)
            {
                if (ghost) Destroy(ghost);
                ghost = BuildCatalog.Make(type, true);
                ghost.transform.SetParent(transform, false);
                ghostType = type;
            }
            ghost.SetActive(pointed == null);
            ghost.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));

            laser.SetPosition(0, ray.origin);
            laser.SetPosition(1, hitSomething ? best.point : target);
            var c = pointed != null ? new Color(1f, 0.25f, 0.25f) : new Color(0.3f, 0.85f, 1f);
            laser.sharedMaterial = KartUI.Glow(c);
        }

        // ---------------- buttons (act when pressed, not every frame they're held)

        static bool Pressed(bool now, ref bool was)
        {
            bool edge = now && !was;
            was = now;
            return edge;
        }

        void Buttons()
        {
            var manager = BuildManager.Instance;
            if (Pressed(KartInput.BuildNextHeld, ref wasNext)) type = (type + 1) % BuildCatalog.Count;
            if (Pressed(KartInput.BuildPrevHeld, ref wasPrev)) type = (type + BuildCatalog.Count - 1) % BuildCatalog.Count;
            if (Pressed(KartInput.BuildRotateHeld, ref wasRotate)) yaw = (yaw + 45f) % 360f;

            if (Pressed(KartInput.BuildPlaceHeld, ref wasPlace) && pointed == null && Time.time >= nextPlace && manager != null &&
                !UIPointer.IsPointingAtButton(XRNode.RightHand)) // clicking a menu button, not building
            {
                nextPlace = Time.time + 0.25f;
                manager.Place(type, target, yaw);
                Buzz(XRNode.RightHand, 0.4f, 0.08f);
            }
            if (Pressed(KartInput.BuildDeleteHeld, ref wasDelete) && pointed != null && manager != null)
            {
                manager.Remove(pointed.id);
                Buzz(XRNode.LeftHand, 0.5f, 0.1f);
            }
            if (KartInput.RespawnPressed && manager != null && manager.UndoMine()) Buzz(XRNode.RightHand, 0.3f, 0.08f);
        }

        void Hud()
        {
            var manager = BuildManager.Instance;
            string count = manager != null ? manager.Count + "/" + BuildManager.MaxPieces : "";
            string item = "<b><  " + BuildCatalog.Names[type].ToUpper() + "  ></b>   " + count;
            string help = KartRig.IsXR
                ? "R trigger place   R grip rotate   A / X change item\nL trigger delete (point at it)   B undo   L grip fly fast\nRight stick click: back to your kart"
                : "F place   T rotate   1 / 2 change item   G delete   R undo\nWASD fly   Space / Ctrl up / down   Shift fast   B back to your kart";
            hud.text = "<color=#7FE3FF>BUILD MODE</color>\n" + (pointed != null ? "<color=#FF6060>Delete this? (L trigger / G)</color>" : item) + "\n<size=70%>" + help + "</size>";
        }

        static void Buzz(XRNode node, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
