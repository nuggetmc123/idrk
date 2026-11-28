using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// (3.65) The TURN-IN board in your bunker: a list of jobs, each wanting something brought back from the raids. Put it
    /// on the tray under the board - if a job wants it, it's taken, the job counts up, and when the job is done a big
    /// REWARD screen shows what you got: gun skins you can't get anywhere else, cash - and for the Swamp King's crown,
    /// Manda turns into a capybara.
    /// </summary>
    public class QuestBoard : MonoBehaviour
    {
        public class Quest { public string id, title, wants, wantName, reward; public int count; }

        public static readonly Quest[] All =
        {
            new Quest { id = "swampkey", title = "THE WAY IN", wants = "key_swamp", wantName = "a Swamp Keycard", count = 1, reward = "THE SWAMP unlocked for raids" },
            new Quest { id = "swampking", title = "THE SWAMP KING", wants = "croc_crown", wantName = "the Swamp King's Crown", count = 1, reward = "Manda becomes a CAPYBARA + the CROC HIDE gun skin" },
            new Quest { id = "teeth", title = "CROC HUNTER", wants = "croc_tooth", wantName = "Croc Teeth", count = 5, reward = "the BAYOU gun skin" },
            new Quest { id = "tags", title = "NAMES OF THE FALLEN", wants = "junk_tags", wantName = "Dog Tags", count = 5, reward = "$15,000" },
            new Quest { id = "kicks", title = "SNEAKERHEAD", wants = "sb_oliver", wantName = "Oliver Kicks (the pair)", count = 2, reward = "the GOLD TOOTH gun skin" },
        };

        ItemContainer _tray;
        TextMesh _list;
        Transform _reward; TextMesh _rewardText; float _rewardT;

        public static QuestBoard Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("QuestBoard");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var q = go.AddComponent<QuestBoard>();
            q.Build();
            return q;
        }

        void Build()
        {
            var panel = UI.Panel(transform, "Board", Vector3.zero, Quaternion.identity, new Vector2(1.1f, 0.95f), null, true, Skin.Steel);
            UI.Text(panel, "TURN-IN", new Vector3(0, 0.4f, -0.003f), 0.055f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(panel, "bring these back from your raids - put them on the tray below", new Vector3(0, 0.345f, -0.003f), 0.017f, UI.Dim, TextAnchor.MiddleCenter);
            _list = UI.Text(panel, "", new Vector3(-0.5f, 0.3f, -0.003f), 0.021f, Color.white, TextAnchor.UpperLeft);
            // the tray: a shelf under the board, 3 spots
            var shelf = new MB();
            Color steel = UI.Lin(new Color(0.3f, 0.31f, 0.32f)), yel = UI.Lin(new Color(0.95f, 0.75f, 0.1f));
            shelf.Box(new Vector3(0, -0.6f, -0.17f), new Vector3(0.9f, 0.04f, 0.34f), steel, 0.01f);
            shelf.Box(new Vector3(0, -0.575f, -0.335f), new Vector3(0.9f, 0.05f, 0.012f), yel, 0.004f);
            foreach (float sx in new[] { -0.42f, 0.42f }) shelf.Box(new Vector3(sx, -0.7f, -0.06f), new Vector3(0.03f, 0.2f, 0.12f), steel, 0.005f);   // brackets
            shelf.Build("Tray", transform, Mats.Lit("turnin_steel", Color.white, 0.6f, 0.45f, Mats.Metal, Mats.MetalN, 0.5f, 0.5f));
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, -0.6f, -0.17f); col.size = new Vector3(0.9f, 0.04f, 0.34f);
            _tray = ItemContainer.Grid(transform, new Vector3(0, -0.57f, -0.17f), Quaternion.identity, 3, 1, new Vector2(0.28f, 0.3f), 0.24f);
            _tray.onChanged = OnTray;
            // the REWARD screen: pops up over the board when a job is done
            _reward = UI.Panel(transform, "Reward", new Vector3(0, 0.02f, -0.03f), Quaternion.identity, new Vector2(1.0f, 0.55f), null, true, Skin.Steel);
            UI.Text(_reward, "JOB DONE", new Vector3(0, 0.19f, -0.004f), 0.06f, new Color(0.4f, 1f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _rewardText = UI.Text(_reward, "", new Vector3(0, 0.05f, -0.004f), 0.028f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _reward.gameObject.SetActive(false);
            Refresh();
        }

        static int Prog(Profile p, string id)
        {
            foreach (var s in p.questProg) if (s.StartsWith(id + "=") && int.TryParse(s.Substring(id.Length + 1), out int n)) return n;
            return 0;
        }
        static void SetProg(Profile p, string id, int n) { p.questProg.RemoveAll(s => s.StartsWith(id + "=")); if (n > 0) p.questProg.Add(id + "=" + n); }

        void Refresh()
        {
            var p = Profile.Current; if (p == null || _list == null) return;
            p.questProg ??= new List<string>(); p.questDone ??= new List<string>();
            var sb = new System.Text.StringBuilder();
            foreach (var q in All)
            {
                bool done = p.questDone.Contains(q.id);
                int n = Prog(p, q.id);
                sb.Append(done ? "<color=#66ff88>DONE  </color>" : "").Append("<b>").Append(q.title).Append("</b>\n");
                if (done) sb.Append("<color=#999999>   ").Append(q.reward).Append("</color>\n\n");
                else sb.Append("   bring ").Append(q.count > 1 ? q.count + " " : "").Append(q.wantName).Append(q.count > 1 ? "   (" + n + "/" + q.count + ")" : "")
                       .Append("\n   <color=#ffcc66>reward: ").Append(q.reward).Append("</color>\n\n");
            }
            _list.text = sb.ToString();
        }

        /// <summary>Something went on the tray: if a job wants it, take it and count it.</summary>
        bool _busy;
        void OnTray()
        {
            if (_busy || _tray == null) return;
            _busy = true;
            try { foreach (var s in _tray.slots) if (s != null && s.item != null && !_seen.Contains(s.item)) { _seen.Add(s.item); TurnIn(s); } }
            finally { _busy = false; _seen.RemoveWhere(x => x == null || x.slot == null); }
            Refresh();
        }
        readonly HashSet<Item> _seen = new HashSet<Item>();   // things on the tray a job didn't want (they stay yours: don't nag twice)

        void TurnIn(Slot s)
        {
            var p = Profile.Current; if (p == null || s == null || s.item == null) return;
            p.questProg ??= new List<string>(); p.questDone ??= new List<string>();
            var it = s.item;
            Quest q = null;
            foreach (var x in All) if (!p.questDone.Contains(x.id) && it.def != null && it.def.id == x.wants) { q = x; break; }
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (q == null)
            {
                bool doneAlready = false; foreach (var x in All) if (it.def != null && it.def.id == x.wants) doneAlready = true;
                pl?.Toast(doneAlready ? "That job's already done" : "No job wants a " + (it.def != null ? it.def.name : "that") + " - it's still yours");
                // off the tray, onto the floor in front of the board (things left on the tray wouldn't be saved)
                s.Release(it);
                it.transform.position = transform.TransformPoint(new Vector3(0, -0.35f, -0.55f));
                it.Drop(Vector3.zero, Vector3.zero);
                return;
            }
            int n = Prog(p, q.id) + 1;
            s.Release(it); Item.Discard(it);
            Sfx.Play(Synth.Grab(2), transform.position, 0.5f, 0.8f, 0.5f, 6f);
            if (n >= q.count) { SetProg(p, q.id, 0); p.questDone.Add(q.id); Complete(q, p); }
            else { SetProg(p, q.id, n); pl?.Toast(q.title + ": " + n + "/" + q.count); }
            p.Save();
        }

        void Complete(Quest q, Profile p)
        {
            p.skins ??= new List<string>();
            switch (q.id)
            {
                case "swampking":
                    p.capybara = true; if (!p.skins.Contains("crochide")) p.skins.Add("crochide");
                    foreach (var pug in FindObjectsByType<Pug>(FindObjectsSortMode.None)) pug.Remodel();
                    break;
                case "swampkey": p.swampUnlocked = true; break;
                case "teeth": if (!p.skins.Contains("bayou")) p.skins.Add("bayou"); break;
                case "tags": p.money += 15000; break;
                case "kicks": if (!p.skins.Contains("goldtooth")) p.skins.Add("goldtooth"); break;
            }
            _rewardText.text = q.title + "\n\n<color=#ffcc66>" + q.reward.Replace(" + ", "\n+ ") + "</color>" + (q.reward.Contains("skin") ? "\n\n<size=60%>paint it on at the booth</size>" : "");
            _reward.gameObject.SetActive(true);
            _rewardT = 7f;
            Sfx.UI(Synth.QueueFound(), 0.6f);
            RagoGame.I?.Player?.Hud?.Center("JOB DONE: " + q.title + "\n<size=34>" + q.reward + "</size>", 5f);
        }

        void Update()
        {
            if (_rewardT > 0f) { _rewardT -= Time.deltaTime; if (_rewardT <= 0f && _reward != null) _reward.gameObject.SetActive(false); }
        }
    }
}
