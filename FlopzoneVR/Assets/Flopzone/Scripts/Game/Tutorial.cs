using System;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// First-time setup: pick a unique, clean name, then a short hands-on
    /// lesson in the training range before heading to the hub.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        UIPanel board;
        UIKeyboard keyboard;
        TextMesh error;
        int step = -1;
        bool hitTarget, menuOpened;
        bool magWasOut;
        string pendingName;
        Action onDone;

        struct Step { public string title, vr, desktop; public Func<bool> done; }
        Step[] steps;

        public static Tutorial Begin(bool needName, Action done)
        {
            var go = new GameObject("Tutorial");
            var t = go.AddComponent<Tutorial>();
            t.onDone = done;
            t.Setup(needName);
            return t;
        }

        void Setup(bool needName)
        {
            var rig = PlayerRig.I;
            ShootingTarget.AnyHit += OnTargetHit;
            Game.I.session.NameResult += OnNameResult;
            steps = new[]
            {
                new Step { title = "Grab a gun", vr = "Reach to your LEFT hip (rifle) or RIGHT hip (pistol) and hold the GRIP button.", desktop = "Press E to take a gun.",
                    done = () => rig.hands[0].HeldGun != null || rig.hands[1].HeldGun != null },
                new Step { title = "Shoot a target", vr = "Point at a target and pull the TRIGGER.", desktop = "Left-click to shoot.", done = () => hitTarget },
                new Step { title = "Drop the mag", vr = "Press B (or grab the mag out of the gun with your other hand).", desktop = "Press F to drop the mag.",
                    done = () => { var g = Gun(); if (g != null && !g.hasMag) magWasOut = true; return magWasOut; } },
                new Step { title = "Reload", vr = "Put your EMPTY hand at your waist and HOLD TRIGGER to grab a mag, then push it into the bottom of the gun.", desktop = "Press R to reload.",
                    done = () => { var g = Gun(); return magWasOut && g != null && g.hasMag; } },
                new Step { title = "Jump", vr = "Press A to jump.", desktop = "Press Space to jump.", done = () => VRInput.I.Down(ControlAction.Jump) },
                new Step { title = "Sprint", vr = "Push the LEFT stick to walk, then click it down to sprint.", desktop = "Hold W and press Shift.", done = () => rig.Sprinting },
                new Step { title = "Crouch", vr = "Click the RIGHT stick down to crouch (and again to stand).", desktop = "Press C.", done = () => rig.Crouched },
                new Step { title = "Open the menu", vr = "Press the ≡ button on your LEFT controller. Settings, controls, friends and more live here.", desktop = "Press Tab.",
                    done = () => menuOpened },
            };
            board = UIPanel.Create("TutorialBoard", new Vector2(1.0f, 0.5f), UIKit.PanelColor);
            board.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 2.2f), Quaternion.identity);
            if (needName) ShowNameEntry();
            else NextStep();
        }

        Gun Gun() => PlayerRig.I.hands[1].HeldGun ?? PlayerRig.I.hands[0].HeldGun;

        /// <summary>The menu button works once the name is picked (one step is opening it).</summary>
        public bool AllowMenu => step >= 0 && step < steps.Length;

        void OnDestroy()
        {
            ShootingTarget.AnyHit -= OnTargetHit;
            if (Game.I != null && Game.I.session != null) Game.I.session.NameResult -= OnNameResult;
            if (board) Destroy(board.gameObject);
        }

        void OnTargetHit() => hitTarget = true;

        /* ---------------- name ---------------- */

        void ShowNameEntry()
        {
            board.Clear();
            board.Text("Welcome to Sloppy!", new Vector2(0, 0.19f), 0.06f, UIKit.Accent);
            board.Text("Pick your player name. Everyone will see it, and it has to be unique.", new Vector2(0, 0.12f), 0.026f, Color.white);
            board.Text("3–16 letters, numbers or _ · no swearing or slurs", new Vector2(0, 0.08f), 0.022f, UIKit.Muted);
            error = board.Text("", new Vector2(0, -0.225f), 0.026f, UIKit.Bad);
            keyboard = UIKeyboard.Create(board.transform, new Vector3(0, -0.06f, -0.01f), "type your name", WordFilter.NameMax, false);
            keyboard.transform.localScale = Vector3.one * 0.75f;
            keyboard.onChanged = v => { error.text = ""; };
            keyboard.onSubmit = SubmitName;
            UIPointer.I.Active = true;
        }

        void SubmitName(string v)
        {
            v = (v ?? "").Trim();
            var bad = WordFilter.CheckName(v);
            if (bad != null) { error.text = bad; AudioManager.Ui("lose", 0.4f); return; }
            pendingName = v;
            error.text = "Checking...";
            error.color = UIKit.Muted;
            Game.I.session.SetName(v);
        }

        void OnNameResult(bool ok, string err)
        {
            if (step >= 0 || board == null) return;
            if (!ok)
            {
                if (error) { error.text = err; error.color = UIKit.Bad; }
                AudioManager.Ui("lose", 0.4f);
                return;
            }
            AudioManager.Ui("win", 0.5f);
            Hud.I.Toast("Nice to meet you, " + pendingName + "!");
            if (keyboard) Destroy(keyboard.gameObject);
            UIPointer.I.Active = false;
            if (Save.TutorialDone) { Finish(); return; }
            NextStep();
        }

        /* ---------------- lesson ---------------- */

        void NextStep()
        {
            step++;
            if (step >= steps.Length) { Finish(); return; }
            var s = steps[step];
            board.Clear();
            board.Text($"Step {step + 1} of {steps.Length}", new Vector2(0, 0.2f), 0.024f, UIKit.Muted);
            board.Text(s.title, new Vector2(0, 0.13f), 0.06f, UIKit.Accent);
            bool vr = VRInput.I.XRActive;
            board.Text(UIKit.Wrap(vr ? s.vr : s.desktop, 0.9f, 0.032f), new Vector2(0, 0.0f), 0.032f, Color.white);
            var skip = board.Button("Skip tutorial", new Vector2(0.36f, -0.2f), new Vector2(0.22f, 0.06f), Finish, 0.022f);
            skip.SetColors(new Color(0.25f, 0.2f, 0.2f), UIKit.Bad);
            UIPointer.I.Active = true;
            if (step > 0) AudioManager.Ui("go", 0.5f);
        }

        void Update()
        {
            if (step < 0 || step >= steps.Length) return;
            if (Game.I.MenuOpen) menuOpened = true;
            if (steps[step].done()) NextStep();
        }

        void Finish()
        {
            if (board == null) return;
            Save.TutorialDone = true;
            board.Clear();
            board.Text("You're ready!", new Vector2(0, 0.1f), 0.07f, UIKit.Good);
            board.Text("Heading to the hub...", new Vector2(0, 0.0f), 0.035f, Color.white);
            AudioManager.Ui("win", 0.6f);
            UIPointer.I.Active = false;
            step = steps.Length;
            var done = onDone;
            onDone = null;
            Destroy(gameObject, 1.5f);
            Game.I.Invoke2(1.4f, () => done?.Invoke());
        }
    }
}
