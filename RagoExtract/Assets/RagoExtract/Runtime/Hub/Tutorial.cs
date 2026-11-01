using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    // ======================================================================================= ID TERMINAL
    /// <summary>
    /// The terminal on the first-launch welcome screen where you register your callsign. It can never be changed afterwards.
    /// Names are 3-14 letters/numbers. Every operator also carries a 4-digit tag (REX#4821), so no two players
    /// ever show the same name; if two meet online with the same name and tag, one of them gets a new tag.
    /// </summary>
    public class IdTerminal : MonoBehaviour
    {
        public const int MinLen = 3, MaxLen = 14;
        static readonly string[] Reserved = { "OPERATOR", "BRUNO", "VIKTOR", "MARA", "SILAS", "DOC", "HALE", "RAGO", "ADMIN", "MODERATOR", "RAIDER" };
        string _name = "";
        bool _typing, _locked;
        string _armed;
        TextMesh _field, _tag, _msg, _title;
        float _blink;
        public static IdTerminal I;

        public static IdTerminal Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("IdTerminal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = go.AddComponent<IdTerminal>();
            t.Build();
            return t;
        }

        void OnEnable() { I = this; }
        void OnDisable() { if (I == this) I = null; if (_typing) Desktop.Typing = false; }

        void Build()
        {
            Color housing = UI.Lin(new Color(0.24f, 0.26f, 0.25f)), dark = UI.Lin(new Color(0.1f, 0.1f, 0.1f)), steel = UI.Lin(new Color(0.45f, 0.46f, 0.47f));
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 1.55f, 0.06f), new Vector3(1.78f, 1.36f, 0.16f), housing, 0.02f, 2);           // wall box
            mb.Sub(0).Box(new Vector3(0, 0.82f, -0.06f), new Vector3(1.5f, 0.06f, 0.3f), housing, 0.015f, 1);            // ledge
            mb.Sub(0).Box(new Vector3(0, 0.5f, 0.02f), new Vector3(0.1f, 0.64f, 0.08f), dark, 0.01f);                    // conduit down to the floor
            foreach (var x in new[] { -0.82f, 0.82f }) foreach (var y in new[] { 0.92f, 2.18f }) mb.Sub(1).Cyl(new Vector3(x, y, -0.02f), new Vector3(x, y, -0.03f), 0.012f, 0.01f, 8, steel);
            mb.Build("Housing", transform, Mats.Lit("id_housing", Color.white, 0.4f, 0.35f, Mats.Metal, Mats.MetalN, 0.45f, 0.45f), Mats.Lit("id_bolts", Color.white, 0.9f, 0.5f));
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.55f, 0.06f); col.size = new Vector3(1.78f, 1.36f, 0.16f);
            UI.Text(transform, "ID TERMINAL", new Vector3(-0.84f, 2.3f, -0.03f), 0.06f, UI.Stencil, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(transform, "OPERATOR REGISTRATION", new Vector3(0.84f, 2.3f, -0.03f), 0.035f, UI.Stencil, TextAnchor.MiddleRight);

            var s = UI.Panel(transform, "Screen", new Vector3(0, 1.57f, -0.03f), Quaternion.identity, new Vector2(1.56f, 1.1f), null, true, Skin.Crt);
            _title = UI.Text(s, "OPERATOR REGISTRATION", new Vector3(-0.72f, 0.48f, 0), 0.05f, UI.CrtGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(s, "CALLSIGN:", new Vector3(-0.72f, 0.39f, 0), 0.032f, UI.CrtDim, TextAnchor.MiddleLeft);
            _field = UI.Text(s, "", new Vector3(-0.72f, 0.3f, 0), 0.08f, UI.CrtGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            _tag = UI.Text(s, "", new Vector3(0.72f, 0.3f, 0), 0.05f, UI.CrtDim, TextAnchor.MiddleRight);
            _msg = UI.Text(s, "", new Vector3(-0.72f, 0.2f, 0), 0.03f, UI.CrtDim, TextAnchor.MiddleLeft);
            // on-screen keyboard
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL_", "ZXCVBNM" };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                {
                    char ch = rows[r][i];
                    UI.Button(s, ch == '_' ? "_" : ch.ToString(), new Vector3(-0.6075f + i * 0.135f, 0.08f - r * 0.12f, 0), new Vector2(0.12f, 0.1f), () => Type(ch), null, BtnStyle.Crt);
                }
            UI.Button(s, "DEL", new Vector3(0.405f, 0.08f - 3 * 0.12f, 0), new Vector2(0.255f, 0.1f), Backspace, null, BtnStyle.Crt);
            UI.Text(s, "3-14 LETTERS / NUMBERS", new Vector3(-0.72f, -0.45f, 0), 0.026f, UI.CrtDim, TextAnchor.MiddleLeft);
            UI.Button(s, "CONFIRM", new Vector3(0.48f, -0.45f, 0), new Vector2(0.48f, 0.1f), Confirm, null, BtnStyle.Crt);
            var p = Profile.Current;
            _name = p != null && p.nameSet ? p.name : "";
            Refresh("CHOOSE CAREFULLY - YOUR CALLSIGN IS PERMANENT.");
        }

        void Type(char c)
        {
            if (_locked) return;
            if (_name.Length >= MaxLen) { Refresh("MAX " + MaxLen + " CHARACTERS."); Sfx.UI(Synth.UiError(), 0.3f); return; }
            _name += char.ToUpperInvariant(c);
            Refresh(null);
        }

        void Backspace()
        {
            if (_locked) return;
            if (_name.Length > 0) _name = _name.Substring(0, _name.Length - 1);
            Refresh(null);
        }

        public static string Validate(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Length < MinLen) return "TOO SHORT - AT LEAST " + MinLen + " CHARACTERS.";
            if (n.Length > MaxLen) return "TOO LONG - AT MOST " + MaxLen + " CHARACTERS.";
            foreach (var ch in n) if (!(ch >= 'A' && ch <= 'Z') && !(ch >= '0' && ch <= '9') && ch != '_') return "LETTERS, NUMBERS AND _ ONLY.";
            if (n.Trim('_').Length == 0) return "NEEDS AT LEAST ONE LETTER OR NUMBER.";
            foreach (var r in Reserved) if (n == r || n.StartsWith("OPERATOR")) return "THAT CALLSIGN IS RESERVED.";
            return null;
        }

        void Confirm()
        {
            var p = Profile.Current;
            if (p == null) return;
            if (p.nameSet || _locked) return;   // a callsign is permanent
            string err = Validate(_name);
            if (err != null) { _armed = null; Refresh(err); Sfx.UI(Synth.UiError(), 0.4f); return; }
            if (_armed != _name)
            {
                // first press: warn, second press: lock it in
                _armed = _name;
                Sfx.UI(Synth.Beep(), 0.5f);
                Refresh("\"" + _name + "\" CAN NEVER BE CHANGED. PRESS CONFIRM AGAIN.");
                return;
            }
            p.name = _name;
            if (p.tag <= 0) p.tag = Profile.NewTagFor();
            p.nameSet = true;
            p.Save();
            _locked = true;
            Sfx.UI(Synth.Ding(), 0.5f);
            Refresh("WELCOME, " + p.DisplayName + ". DEPLOYING TO YOUR BUNKER...");
            Invoke(nameof(GoToBunker), 2.5f);
        }

        void GoToBunker() { if (RagoGame.I != null) RagoGame.I.GoTo(Area.Bunker); }

        void Refresh(string msg)
        {
            var p = Profile.Current;
            if (_tag != null) _tag.text = p != null && p.tag > 0 ? "#" + p.tag.ToString("0000") : "";
            if (msg != null && _msg != null) _msg.text = msg;
            Blink(true);
        }

        void Blink(bool on) { if (_field != null) _field.text = "> " + _name + (on ? "_" : " "); }

        void Update()
        {
            _blink += Time.deltaTime;
            Blink(Mathf.Repeat(_blink, 1f) < 0.5f);
            // desktop: look at the screen and just type (movement keys pause while you do)
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            bool looking = pl != null && !XRInputs.VR && Physics.Raycast(pl.Head.position, pl.Head.forward, out var hit, 3f, ~0, QueryTriggerInteraction.Collide) && hit.collider.transform.IsChildOf(transform);
            if (looking != _typing) { _typing = looking; Desktop.Typing = looking; }
            if (!looking) return;
            string typed = "";
            try { typed = Input.inputString; } catch { return; }
            foreach (char c in typed)
            {
                if (c == '\b') Backspace();
                else if (c == '\n' || c == '\r') Confirm();
                else if (char.IsLetterOrDigit(c) || c == '_') Type(c);
            }
        }
    }

    // ======================================================================================= NAMEPLATE
    /// <summary>Your permanent callsign, stamped on a steel plate in the bunker. The SKIP TRAINING button lives here too.</summary>
    public class IdPlaque : MonoBehaviour
    {
        WorldButton _skip;
        TextMesh _name;

        public static IdPlaque Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("IdPlaque");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = go.AddComponent<IdPlaque>();
            var plate = UI.Panel(go.transform, "Plate", new Vector3(0, 1.6f, -0.03f), Quaternion.identity, new Vector2(1.5f, 0.62f), new Color(0.3f, 0.31f, 0.3f));
            UI.Text(plate, "OPERATOR", new Vector3(0, 0.2f, 0), 0.05f, UI.Stencil, TextAnchor.MiddleCenter, FontStyle.Bold);
            t._name = UI.Text(plate, "", new Vector3(0, 0.04f, 0), 0.12f, new Color(0.95f, 0.93f, 0.88f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(plate, "callsign registered - permanent", new Vector3(0, -0.11f, 0), 0.034f, UI.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            t._skip = UI.Button(plate, "SKIP TRAINING", new Vector3(0, -0.22f, 0), new Vector2(0.44f, 0.08f), () => { if (Tutorial.I != null) Tutorial.I.Skip(); }, new Color(0.45f, 0.2f, 0.12f));
            t._skip.gameObject.SetActive(false);
            return t;
        }

        void Update()
        {
            var p = Profile.Current;
            if (_name != null && p != null && _name.text != p.DisplayName) _name.text = p.DisplayName;
            bool show = Tutorial.I != null;
            if (_skip != null && _skip.gameObject.activeSelf != show) _skip.gameObject.SetActive(show);
        }
    }

    // ======================================================================================= TRAINING
    /// <summary>A glowing beacon that shows where to go next during the training.</summary>
    public class TutorialMarker : MonoBehaviour
    {
        Transform _ring, _arrow;

        public static TutorialMarker Create(Transform parent)
        {
            var go = new GameObject("TrainingMarker");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<TutorialMarker>();
            var glow = Mats.Unlit("train_glow", new Color(1f, 0.75f, 0.2f, 0.85f), Mats.Glow, Mats.BlendKind.Additive);
            var beam = Mats.Unlit("train_beam", new Color(1f, 0.7f, 0.2f, 0.12f), null, Mats.BlendKind.Additive);
            m._ring = new GameObject("Ring").transform; m._ring.SetParent(go.transform, false);
            var r = new MB();
            for (int i = 0; i < 20; i++) { float a = i * 18f; r.Box(Quaternion.Euler(0, a, 0) * new Vector3(0, 0.02f, 0.55f), new Vector3(0.1f, 0.01f, 0.04f), Color.white, 0f, 0, Quaternion.Euler(0, a, 0)); }
            r.Build("Ring", m._ring, glow);
            new MB().Cyl(Vector3.zero, Vector3.up * 2.6f, 0.08f, 0.02f, 12, Color.white, false).Build("Beam", go.transform, beam);
            m._arrow = new GameObject("Arrow").transform; m._arrow.SetParent(go.transform, false);
            new MB().Cyl(new Vector3(0, 0.25f, 0), new Vector3(0, 0f, 0), 0.12f, 0.0f, 12, Color.white, true).Build("Cone", m._arrow, glow);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.3f); l.range = 2.5f; l.intensity = 0.8f;
            return m;
        }

        void Update()
        {
            _ring.localRotation = Quaternion.Euler(0, Time.time * 60f, 0);
            _arrow.localPosition = new Vector3(0, 2.0f + Mathf.Sin(Time.time * 3f) * 0.12f, 0);
        }
    }

    /// <summary>
    /// First-time training in the bunker: register a callsign, draw, shoot, reload, open your bag, put on armour,
    /// see your stash and learn how raids and extracts work. Progress is saved; you can skip it on the nameplate in the main hall.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        class Step { public string vr, desk; public Vector3? at; public System.Func<bool> done; }

        public static Tutorial I;
        readonly List<Step> _steps = new List<Step>();
        TutorialMarker _marker;
        bool _shot, _reloaded, _finished;
        float _wait;

        public static Tutorial Create(Transform parent, Vector3 range, Vector3 armor, Vector3 armory, Vector3 raidTerminal, Vector3 lift)
        {
            var go = new GameObject("Training");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Tutorial>();
            t._marker = TutorialMarker.Create(go.transform);
            var p = Profile.Current;
            PlayerRig P() => RagoGame.I.Player;
            t._steps.Add(new Step { done = () => P().HandL.Held is Gun || P().HandR.Held is Gun,
                vr = "Reach to your right hip and squeeze GRIP to draw your pistol.",
                desk = "Press 3 to draw your pistol (1 / 2 are your rifles)." });
            t._steps.Add(new Step { at = range, done = () => t._shot,
                vr = "Go to the shooting range (west tunnel), aim and pull the TRIGGER.",
                desk = "Go to the shooting range (west tunnel). Hold right mouse to aim, left click to fire." });
            t._steps.Add(new Step { done = () => t._reloaded,
                vr = "Reload: press A/X to drop the mag, grab a new one from your CHEST pouch and push it into the gun.",
                desk = "Press R to reload." });
            t._steps.Add(new Step { done = () => P().HandL.Held is Backpack || P().HandR.Held is Backpack,
                vr = "Reach over your LEFT shoulder and grip to pull your backpack off. It opens - drop things in.",
                desk = "Press Tab to take your backpack off your back. G drops what you hold into it." });
            t._steps.Add(new Step { at = armor, done = () => P().Gear.Helmet != null || P().Gear.Vest != null,
                vr = "Go to the ARMOR room (east door). Take a helmet or vest off a stand and hold it to your head / chest.",
                desk = "Go to the ARMOR room (east door). Grab a helmet or vest (E) and press H to put it on." });
            t._steps.Add(new Step { at = armory, done = () => P().transform.position.z > armory.z - 1.5f,
                vr = "Go into the ARMORY (north door). This is your stash: everything you leave here is safe.",
                desk = "Go into the ARMORY (north door). This is your stash: everything you leave here is safe." });
            t._steps.Add(new Step { at = raidTerminal, done = () => Vector3.Distance(P().transform.position, raidTerminal) < 2.2f,
                vr = "Last one: walk to the RAID TERMINAL. Pick a map and press QUEUE when you're ready to deploy.",
                desk = "Last one: walk to the RAID TERMINAL. Pick a map and press QUEUE when you're ready to deploy." });
            return t;
        }

        void OnEnable() { I = this; Gun.OnShot += Shot; Gun.OnReloaded += Reloaded; }
        void OnDisable() { if (I == this) I = null; Gun.OnShot -= Shot; Gun.OnReloaded -= Reloaded; var pl = RagoGame.I != null ? RagoGame.I.Player : null; if (pl != null && pl.Hud != null) pl.Hud.Objective(""); }
        void Shot(Gun g) { if (Profile.Current != null && Profile.Current.tutorialStep == 1) _shot = true; }
        void Reloaded(Gun g) { if (Profile.Current != null && Profile.Current.tutorialStep == 2) _reloaded = true; }

        public void Skip()
        {
            var p = Profile.Current;
            if (p == null) return;
            p.tutorialDone = true; p.Save();
            _finished = true;
            RagoGame.I?.Player?.Toast("Training skipped.");
            Destroy(gameObject);
        }

        void Update()
        {
            var p = Profile.Current;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null || pl == null || pl.Hud == null || _finished) return;
            if (p.tutorialDone) { Destroy(gameObject); return; }
            int i = Mathf.Clamp(p.tutorialStep, 0, _steps.Count);
            if (i >= _steps.Count) { Finish(p, pl); return; }
            var s = _steps[i];
            pl.Hud.Objective("TRAINING  " + (i + 1) + "/" + _steps.Count + "\n" + (XRInputs.VR ? s.vr : s.desk));
            _marker.gameObject.SetActive(s.at.HasValue);
            if (s.at.HasValue) _marker.transform.position = s.at.Value;
            _wait -= Time.deltaTime;
            bool ok = false;
            try { ok = s.done(); } catch { }
            if (ok && _wait <= 0)
            {
                p.tutorialStep = i + 1; p.Save();
                _wait = 1.2f;
                Sfx.UI(Synth.Ding(), 0.45f);
                pl.Hud.Center("DONE", 1.2f);
            }
        }

        void Finish(Profile p, PlayerRig pl)
        {
            _finished = true;
            p.tutorialDone = true; p.Save();
            pl.Hud.Objective("");
            Sfx.UI(Synth.Extracted(), 0.6f);
            pl.Hud.Center("TRAINING COMPLETE\n<size=40>In a raid: loot, fight, then stand in an EXTRACT (green smoke) for 8 s to keep your loot.\nDie and you lose what you brought. Visit the Market by the LIFT.</size>", 9f);
            Destroy(gameObject, 0.1f);
        }
    }
}
