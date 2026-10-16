using System;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The first-time tutorial. New players start here instead of the hub: a private, solo copy of the Free Roam
    /// map, with a panel floating in front of them that walks through driving and then building. Each step
    /// finishes by itself when you do it (or press SKIP STEP). Finishing unlocks the game: you go to the hub and
    /// from then on start there.
    ///
    /// Started by <see cref="SessionManager"/>; the "done" flag is saved on this device.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        const string DoneKey = "kartvr.tutorialDone";

        /// <summary>True once this device has finished the tutorial.</summary>
        public static bool Done
        {
            get => PlayerPrefs.GetInt(DoneKey, 0) == 1;
            set { PlayerPrefs.SetInt(DoneKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static Tutorial Instance { get; private set; }
        public static bool Running => Instance != null;

        class Step
        {
            public string title, text;
            public Action begin;
            public Func<bool> done;
        }

        Step[] steps;
        int index = -1;
        float stepStarted;
        Transform panel;
        TextMeshPro title, body, progress;
        PokeButton skipButton;

        // Things the steps measure.
        float turned, drifted, lastYaw;
        Vector3 lastKartPos, flyStart;
        int piecesAtStart;
        bool pickedPiece, boostSkipped;

        public static void Begin()
        {
            if (Instance != null) return;
            var go = new GameObject("Tutorial");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Tutorial>();
        }

        /// <summary>Stops the tutorial without finishing it (you'll get it again next time the game starts).</summary>
        public static void Stop()
        {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static KartController Kart => KartController.Local;
        static float Mph => Kart != null ? Kart.Speed * 2.23694f : 0f;

        float createdAt;

        void Awake()
        {
            createdAt = Time.time;
            steps = new[]
            {
                // ---------------- Driving
                new Step { title = "WELCOME TO KART VR!", text = "This quick tutorial teaches you to <b>drive</b> and to <b>build</b>.\n\nHold the <b>RIGHT TRIGGER</b> to drive forward.",
                    done = () => Mph > 8f },
                new Step { title = "STEERING", text = "Grab the steering wheel with both <b>GRIPS</b> and turn it like a real wheel,\nor push the <b>LEFT STICK</b> left and right.\n\nDrive in a full circle!",
                    begin = () => turned = 0f, done = () => turned > 300f },
                new Step { title = "BRAKE + REVERSE", text = "The <b>LEFT TRIGGER</b> is your brake.\nKeep holding it after you stop to <b>reverse</b>.\n\nBack up a little!",
                    done = () => Reversing() },
                new Step { title = "DRIFTING", text = "Drive fast, turn, and hold <b>X</b> to <b>drift</b>: the kart slides sideways around the corner.\n\nDrift for a moment!",
                    begin = () => drifted = 0f, done = () => drifted > 0.8f },
                new Step { title = "BOOST", text = "Press <b>A</b> to <b>boost</b> when the meter on your speed screen says READY.",
                    begin = () => boostSkipped = Kart != null && Kart.Look.booster == KartBooster.None,
                    done = () => Kart != null && Kart.Boosting || boostSkipped && Time.time - stepStarted > 5f },
                new Step { title = "TOP SPEED", text = "The screen on your kart shows how fast you're going in <b>MPH</b>.\n\nGet up to <b>30 MPH</b>!",
                    done = () => Mph >= 30f },
                new Step { title = "RESPAWN", text = "Stuck or upside down? Press <b>B</b> to put your kart back on the track.\n\nTry it now.",
                    done = () => KartInput.RespawnPressed },
                // ---------------- Building
                new Step { title = "BUILD MODE", text = "In Free Roam you can <b>build</b>: ramps, roads, trees and more. Everyone on your server sees it.\n\nClick the <b>RIGHT STICK</b> (press it in) to hop out and fly.",
                    done = () => BuildMode.Active },
                new Step { title = "FLYING", text = "<b>LEFT STICK</b> flies around, <b>RIGHT STICK</b> up/down goes up and down, left/right turns.\nHold the <b>LEFT GRIP</b> to fly fast.\n\nFly around a bit!",
                    begin = () => flyStart = HeadPosition(), done = () => !BuildMode.Active || (HeadPosition() - flyStart).magnitude > 8f },
                new Step { title = "PICK A PIECE", text = "Press <b>A</b> for the next piece and <b>X</b> for the one before.\nThe ghost in front of your right hand shows what you'll place.",
                    begin = () => pickedPiece = false, done = () => pickedPiece },
                new Step { title = "PLACE IT", text = "Point your right hand and press the <b>RIGHT TRIGGER</b> to place the piece.\nThe <b>RIGHT GRIP</b> rotates it.\n\nPlace 2 pieces!",
                    begin = () => piecesAtStart = PieceCount(), done = () => PieceCount() >= piecesAtStart + 2 },
                new Step { title = "DELETE + UNDO", text = "Point at a piece and press the <b>LEFT TRIGGER</b> to delete it.\n<b>B</b> undoes the last piece you placed.\n\nRemove one!",
                    begin = () => piecesAtStart = PieceCount(), done = () => PieceCount() < piecesAtStart },
                new Step { title = "BACK TO YOUR KART", text = "Click the <b>RIGHT STICK</b> again to jump back into your kart.\nTry driving over what you built!",
                    done = () => !BuildMode.Active },
                // ---------------- Finish
                new Step { title = "YOU'RE READY!", text = "The <b>three-lines button</b> opens the menu: settings, voice chat and private lobbies.\nIn the hub, walk with the left stick and <b>click the right stick to run</b>.\n\nPress <b>PLAY!</b> to go to the hub and race with everyone.",
                    done = () => false },
            };
        }

        void Update()
        {
            // Pick your name first; the tutorial waits (and hides) until you have.
            if (NameEntry.Showing)
            {
                if (panel != null) panel.gameObject.SetActive(false);
                createdAt = Time.time;
                return;
            }
            if (panel != null && !panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            if (index < 0)
            {
                // Wait until you're in your kart in the tutorial world (but never more than 12 s: then just start).
                bool inKart = Kart != null && Kart.IsReady && Kart.CurrentMode == KartMode.FreeRoam;
                if (KartRig.Instance == null || !inKart && Time.time - createdAt < 12f) return;
                Go(0);
            }
            if (panel == null) BuildPanel();
            Measure();
            FollowView();
            var step = steps[index];
            // A short pause on every step so you can read it before it ticks off.
            if (Time.time - stepStarted > 1.5f && step.done()) Next();
        }

        void Go(int i)
        {
            index = i;
            stepStarted = Time.time;
            lastYaw = Kart != null ? Kart.transform.eulerAngles.y : 0f;
            lastKartPos = Kart != null ? Kart.transform.position : Vector3.zero;
            steps[i].begin?.Invoke();
            if (title == null) return;
            title.text = "<b>" + steps[i].title + "</b>";
            body.text = steps[i].text;
            progress.text = "TUTORIAL  " + (i + 1) + " / " + steps.Length;
            bool last = i == steps.Length - 1;
            skipButton.SetLabel(last ? "<b>PLAY!</b>" : "Skip step");
        }

        void Next()
        {
            AudioManager.Play(AudioManager.Sfx.Click, 1f, 1.3f);
            if (index + 1 < steps.Length) Go(index + 1);
            else Finish();
        }

        void Finish()
        {
            Done = true;
            AudioManager.Play(AudioManager.Sfx.Teleport);
            if (BuildMode.Active) BuildMode.Toggle();
            Destroy(gameObject);
            if (SessionManager.Instance != null) SessionManager.Instance.FinishTutorial();
        }

        void OnSkip()
        {
            if (index < 0) return;
            if (index == steps.Length - 1) Finish();
            else Next();
        }

        // ---------------- measuring what you do

        void Measure()
        {
            var kart = Kart;
            if (kart == null) return;
            float yaw = kart.transform.eulerAngles.y;
            if (!BuildMode.Active && kart.Speed > 2f) turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            lastYaw = yaw;
            if (KartInput.DriftHeld && kart.Speed > 5f && Mathf.Abs(KartInput.Steer) > 0.3f) drifted += Time.deltaTime;
            if (BuildMode.Active && (KartInput.BuildNextHeld || KartInput.BuildPrevHeld)) pickedPiece = true;
        }

        bool Reversing()
        {
            var kart = Kart;
            if (kart == null) return false;
            Vector3 moved = kart.transform.position - lastKartPos;
            lastKartPos = kart.transform.position;
            return Time.deltaTime > 0f && Vector3.Dot(moved / Time.deltaTime, kart.transform.forward) < -1.5f;
        }

        static int PieceCount() => BuildManager.Instance != null ? BuildManager.Instance.Count : 0;

        static Vector3 HeadPosition() => KartRig.Instance != null && KartRig.Instance.head != null ? KartRig.Instance.head.position : Vector3.zero;

        // ---------------- the panel

        void BuildPanel()
        {
            panel = new GameObject("TutorialPanel").transform;
            panel.SetParent(transform, false);
            panel.localScale = Vector3.one * 0.8f; // big enough to read, small enough to see the road past it
            const float w = 1.3f, h = 0.62f;
            KartUI.Box(panel, "Backing", new Vector3(0f, 0f, 0.02f), new Vector3(w, h, 0.02f), new Color(0.06f, 0.07f, 0.1f));
            KartUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, h * 0.5f, 0.015f), new Vector3(w + 0.02f, 0.015f, 0.025f), KartUI.Glow(new Color(0.3f, 0.9f, 1f)));
            KartUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, -h * 0.5f, 0.015f), new Vector3(w + 0.02f, 0.015f, 0.025f), KartUI.Glow(new Color(1f, 0.3f, 0.8f)));
            progress = KartUI.Label(panel, "", new Vector3(0f, 0.255f, 0f), 0.28f, new Color(0.3f, 0.9f, 1f), new Vector2(1.2f, 0.06f));
            title = KartUI.Label(panel, "", new Vector3(0f, 0.19f, 0f), 0.6f, new Color(1f, 0.85f, 0.25f), new Vector2(1.2f, 0.1f));
            body = KartUI.Label(panel, "", new Vector3(0f, -0.03f, 0f), 0.36f, Color.white, new Vector2(1.2f, 0.34f));
            body.enableWordWrapping = true;
            skipButton = PokeButton.Create(panel, "Skip", "Skip step", new Vector3(0.48f, -0.25f, 0f), new Vector3(0.28f, 0.08f, 0.03f),
                new Color(0.45f, 0.75f, 1f), OnSkip, 0.3f);
            skipButton.cooldown = 1f;
            Go(index); // fill in the text
        }

        Vector3 panelLocal;
        bool placed;

        /// <summary>
        /// Floats in front of you, a little above eye level, and lazily follows where you look. It follows in the
        /// rig's own space, so it rides along with your kart instead of being left behind when you drive fast.
        /// </summary>
        void FollowView()
        {
            var rig = KartRig.Instance;
            if (panel == null || rig == null || rig.head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = rig.transform.forward;
            fwd.Normalize();
            Vector3 target = rig.transform.InverseTransformPoint(rig.head.position + fwd * 2f + Vector3.up * 0.55f);
            float k = placed ? 1f - Mathf.Exp(-2.5f * Time.deltaTime) : 1f;
            placed = true;
            panelLocal = Vector3.Lerp(panelLocal, target, k);
            panel.position = rig.transform.TransformPoint(panelLocal);
            Vector3 away = panel.position - rig.head.position;
            if (away.sqrMagnitude > 0.01f) panel.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
