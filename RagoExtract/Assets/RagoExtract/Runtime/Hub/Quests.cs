using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    // ======================================================================================= BUNKER JOBS
    /// <summary>One bunker expansion: bring the Foreman these items and pay the fee, and the room gets built.</summary>
    public class BunkerJob
    {
        public string id, name, room, desc;
        public string[] items;   // item ids
        public int[] counts;     // how many of each
        public int money;
    }

    public static class BunkerJobs
    {
        public const int GeneratorPerRaid = 1500, GeneratorCap = 15000;

        public static readonly BunkerJob[] All =
        {
            new BunkerJob { id = "storage", name = "Clear the Storage Room", room = "STORAGE ROOM",
                desc = "There's an old storage room off your Armory,\nfull of rubble. Bring scrap to shore it up and\nbatteries for the lights.\nUnlocks: two more racks with shelves.",
                items = new[] { "junk_scrap", "junk_tin", "junk_battery" }, counts = new[] { 5, 3, 2 }, money = 4000 },
            new BunkerJob { id = "medbay", name = "Build a Med Bay", room = "MED BAY",
                desc = "Doc Hale wants proper supplies before he'll\nkit out a med bay for you. Whiskey's for him.\nUnlocks: a med cabinet that restocks\nmeds after every raid.",
                items = new[] { "bandage", "ifak", "junk_whiskey" }, counts = new[] { 3, 1, 2 }, money = 10000 },
            new BunkerJob { id = "generator", name = "Wire up a Generator", room = "GENERATOR ROOM",
                desc = "Get me batteries, radios and a graphics card\nand I'll hook a generator to the grid.\nUnlocks: earns $" + GeneratorPerRaid.ToString("N0") + " every raid -\ncollect it in your bunker.",
                items = new[] { "junk_battery", "val_radio", "val_gpu" }, counts = new[] { 4, 2, 1 }, money = 22000 },
            new BunkerJob { id = "vault", name = "Dig a Trophy Vault", room = "TROPHY VAULT",
                desc = "The big one. A vault under your Armory\nfor your best gear. It isn't cheap.\nUnlocks: more armor stands and gun racks.",
                items = new[] { "val_laptop", "val_watch", "val_idol" }, counts = new[] { 1, 2, 1 }, money = 50000 },
        };

        public static BunkerJob Current(Profile p) => p != null && p.bunkerLevel < All.Length ? All[p.bunkerLevel] : null;
        public static bool Built(Profile p, string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return p != null && p.bunkerLevel > i; return false; }

        public static int Progress(Profile p, int i)
        {
            while (p.jobProgress.Count <= i) p.jobProgress.Add(0);
            return p.jobProgress[i];
        }

        /// <summary>How many more of this item the current job needs (0 = not needed).</summary>
        public static int Needed(Profile p, string id)
        {
            var j = Current(p);
            if (j == null || id == null) return 0;
            for (int i = 0; i < j.items.Length; i++) if (j.items[i] == id) return Mathf.Max(0, j.counts[i] - Progress(p, i));
            return 0;
        }

        public static bool ItemsDone(Profile p)
        {
            var j = Current(p);
            if (j == null) return false;
            for (int i = 0; i < j.items.Length; i++) if (Progress(p, i) < j.counts[i]) return false;
            return true;
        }

        public static void TurnIn(Profile p, string id)
        {
            var j = Current(p);
            if (j == null) return;
            for (int i = 0; i < j.items.Length; i++) if (j.items[i] == id && Progress(p, i) < j.counts[i]) { p.jobProgress[i]++; return; }
        }

        /// <summary>Called when a raid ends (extracted or killed): the generator makes money.</summary>
        public static void RaidFinished(Profile p)
        {
            if (Built(p, "generator")) p.genCash = Mathf.Min(GeneratorCap, p.genCash + GeneratorPerRaid);
        }
    }

    // ======================================================================================= THE FOREMAN
    /// <summary>Bruno the Foreman: a stall in the Market where you turn in items to expand your bunker.</summary>
    public class QuestStall : MonoBehaviour
    {
        Transform _panel;
        TextMesh _title, _desc, _reqs, _list, _money;
        WorldButton _build;
        ItemContainer _tray;
        HumanRig _npc;
        float _yaw;
        Vector3 _npcPos;
        static readonly Color Hivis = new Color(1f, 0.55f, 0.08f);

        public static QuestStall Create(Transform parent, Vector3 pos, float yaw, int seed)
        {
            var go = new GameObject("Foreman");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<QuestStall>();
            s.Build(seed);
            return s;
        }

        void Build(int seed)
        {
            var wood = Mats.Lit("stall_wood", Color.white, 0f, 0.3f, Mats.Wood, Mats.WoodN, 0.45f, 0.2f, true, 1f);
            var mb = new MB(3);
            // site office: plywood counter, hazard-striped kick plate, tarp awning, blueprint board behind
            mb.Sub(0).Box(new Vector3(0, 0.5f, 0), new Vector3(3.4f, 1.0f, 0.7f), new Color(0.62f, 0.5f, 0.34f), 0.02f, 2);
            for (int i = 0; i < 12; i++)
                mb.Sub(1).Box(new Vector3(-1.62f + i * 0.295f, 0.12f, -0.36f), new Vector3(0.13f, 0.2f, 0.01f), i % 2 == 0 ? new Color(0.95f, 0.75f, 0.1f) : new Color(0.08f, 0.08f, 0.08f), 0f, 0, Quaternion.Euler(0, 0, 35));
            mb.Sub(1).Box(new Vector3(0, 1.02f, -0.02f), new Vector3(3.5f, 0.05f, 0.8f), new Color(0.3f, 0.3f, 0.32f), 0.01f);
            mb.Sub(2).Box(new Vector3(0, 3.1f, 0.7f), new Vector3(4.4f, 0.06f, 2.8f), Hivis, 0.02f, 1, Quaternion.Euler(-12, 0, 0));
            foreach (var x in new[] { -2.1f, 2.1f }) foreach (var z in new[] { -0.5f, 2f }) mb.Sub(1).Cyl(new Vector3(x, 0, z), new Vector3(x, 3.1f, z), 0.05f, 0.05f, 8, new Color(0.25f, 0.25f, 0.25f));
            mb.Sub(0).Box(new Vector3(0, 1.4f, 2.0f), new Vector3(3.8f, 2.4f, 0.12f), new Color(0.5f, 0.42f, 0.3f), 0.01f);
            // blueprints pinned to the board
            for (int i = 0; i < 4; i++)
            {
                var bp = new Vector3(-1.35f + i * 0.9f, 1.7f + (i % 2) * 0.25f, 1.93f);
                mb.Sub(1).Box(bp, new Vector3(0.75f, 0.55f, 0.005f), new Color(0.12f, 0.25f, 0.5f), 0f);
                for (int k = 0; k < 4; k++) mb.Sub(1).Box(bp + new Vector3(-0.25f + k * 0.17f, 0, -0.004f), new Vector3(0.008f, 0.4f, 0.002f), new Color(0.6f, 0.75f, 0.95f), 0f);
                mb.Sub(1).Box(bp + new Vector3(0, 0.05f, -0.004f), new Vector3(0.55f, 0.008f, 0.002f), new Color(0.6f, 0.75f, 0.95f), 0f);
            }
            // tools: shovel + pickaxe leaning on the board, sandbags
            mb.Sub(0).Cyl(new Vector3(1.75f, 0.0f, 1.7f), new Vector3(1.65f, 1.3f, 1.85f), 0.018f, 0.018f, 8, new Color(0.55f, 0.4f, 0.25f));
            mb.Sub(1).Box(new Vector3(1.76f, 0.12f, 1.69f), new Vector3(0.2f, 0.26f, 0.02f), new Color(0.3f, 0.3f, 0.3f), 0.01f, 1, Quaternion.Euler(-7, 0, 0));
            mb.Sub(0).Cyl(new Vector3(-1.8f, 0.0f, 1.65f), new Vector3(-1.7f, 1.0f, 1.85f), 0.02f, 0.02f, 8, new Color(0.55f, 0.4f, 0.25f));
            mb.Sub(1).Box(new Vector3(-1.7f, 1.0f, 1.85f), new Vector3(0.5f, 0.05f, 0.05f), new Color(0.3f, 0.3f, 0.3f), 0.01f, 1, Quaternion.Euler(0, 0, 10));
            mb.Build("Stall", transform, wood, Mats.Lit("stall_metal", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.4f, 0.4f), Mats.Lit("awning", Color.white, 0f, 0.1f, Mats.Fabric, Mats.FabricN, 0.4f, 0f));
            AddCol(new Vector3(0, 0.5f, 0), new Vector3(3.4f, 1.04f, 0.7f));
            AddCol(new Vector3(0, 1.4f, 2.0f), new Vector3(3.8f, 2.4f, 0.12f));
            // spray-painted plywood sign zip-tied to the awning
            var sb = new MB(2);
            sb.Sub(0).Box(new Vector3(0, 2.72f, -0.72f), new Vector3(2.0f, 0.44f, 0.025f), UI.Lin(new Color(0.72f, 0.6f, 0.42f)), 0.004f);
            for (int i = 0; i < 2; i++) sb.Sub(1).Box(new Vector3(0, 2.52f + i * 0.4f, -0.734f), new Vector3(2.0f, 0.03f, 0.002f), UI.Lin(new Color(0.95f, 0.7f, 0.1f)), 0f);
            sb.Build("SignBoard", transform, Mats.Lit("sign_ply", Color.white, 0f, 0.3f, Mats.Wood, null, 0.5f, 0.2f), Mats.Lit("sign_spray", Color.white, 0f, 0.3f, Mats.Noise, null, 0.3f, 0.1f));
            var sign = new GameObject("Sign").transform; sign.SetParent(transform, false); sign.localPosition = new Vector3(0, 2.72f, -0.735f);
            UI.Text(sign, "BRUNO", new Vector3(0, 0.06f, 0), 0.17f, new Color(0.12f, 0.12f, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(sign, "foreman - bunker work", new Vector3(0, -0.12f, 0), 0.055f, new Color(0.75f, 0.2f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);

            // job board on the counter
            // work order on a clipboard, propped on the counter
            _panel = UI.Panel(transform, "Jobs", new Vector3(0.3f, 1.64f, 0.08f), Quaternion.Euler(-12, 0, 0), new Vector2(1.5f, 1.0f), null, true, Skin.Paper);
            UI.Text(_panel, "WORK ORDER", new Vector3(-0.69f, 0.44f, 0), 0.032f, UI.PaperDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            _title = UI.Text(_panel, "", new Vector3(-0.69f, 0.38f, 0), 0.056f, UI.PaperInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            _money = UI.Text(_panel, "", new Vector3(0.69f, 0.44f, 0), 0.034f, UI.PaperDim, TextAnchor.MiddleRight);
            _desc = UI.Text(_panel, "", new Vector3(-0.69f, 0.33f, 0), 0.036f, UI.PaperDim, TextAnchor.UpperLeft, FontStyle.Italic);
            _reqs = UI.Text(_panel, "", new Vector3(-0.69f, 0.17f, 0), 0.044f, UI.PaperInk, TextAnchor.UpperLeft);
            _list = UI.Text(_panel, "", new Vector3(0.22f, 0.17f, 0), 0.034f, UI.PaperDim, TextAnchor.UpperLeft);
            UI.Text(_panel, "signed:  Bruno", new Vector3(-0.69f, -0.41f, 0), 0.04f, UI.PaperDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            _build = UI.Button(_panel, "APPROVED - BUILD IT", new Vector3(0.38f, -0.4f, 0), new Vector2(0.6f, 0.11f), Build, null, BtnStyle.Stamp);

            // turn-in tray: only takes what the current job still needs
            _tray = ItemContainer.Grid(transform, new Vector3(-1.2f, 1.08f, -0.05f), Quaternion.identity, 2, 2, new Vector2(0.36f, 0.3f), 0.3f,
                it => it.def != null && BunkerJobs.Needed(Profile.Current, it.def.id) > 0);
            var tray = new MB();
            tray.Box(Vector3.zero, new Vector3(0.78f, 0.02f, 0.66f), Hivis * 0.5f, 0.005f);
            var tgo = tray.Build("Tray", transform, Mats.Lit("tray_jobs", Color.white, 0.3f, 0.4f));
            tgo.transform.localPosition = new Vector3(-1.2f, 1.05f, -0.05f);
            UI.Text(transform, "TURN IN", new Vector3(-1.2f, 1.07f, -0.41f), 0.025f, Hivis).transform.localRotation = Quaternion.Euler(60, 0, 0);
            _tray.onChanged = Refresh;
            var tp = UI.Panel(transform, "TurnInPanel", new Vector3(-1.2f, 1.32f, 0.34f), Quaternion.Euler(-25, 0, 0), new Vector2(0.66f, 0.24f), null, true, Skin.Paper);
            UI.Text(tp, "Put the job's items on the tray", new Vector3(0, 0.035f, 0), 0.026f, UI.PaperInk);
            UI.Button(tp, "TURN IN", new Vector3(0, -0.06f, 0), new Vector2(0.3f, 0.08f), TurnIn, null, BtnStyle.Stamp);

            // Bruno: hi-vis vest and cap
            _npcPos = transform.TransformPoint(new Vector3(-0.2f, 0, 1.0f));
            _npc = HumanRig.Build(Outfit.Foreman, seed, transform);
            _yaw = transform.eulerAngles.y + 180f;
            Refresh();
        }

        void AddCol(Vector3 c, Vector3 s) { var b = gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        void Update() { if (Time.frameCount % 20 == 0) Refresh(); }

        void Refresh()
        {
            var p = Profile.Current;
            if (p == null || _title == null) return;
            var j = BunkerJobs.Current(p);
            _money.text = "your cash  $" + p.money.ToString("N0");
            // all jobs with their status
            var sb = new System.Text.StringBuilder("BUNKER PLANS\n");
            for (int i = 0; i < BunkerJobs.All.Length; i++)
            {
                var job = BunkerJobs.All[i];
                string mark = i < p.bunkerLevel ? "<color=#1d6b2a>done</color>" : i == p.bunkerLevel ? "<color=#b05a00>in progress</color>" : "later";
                sb.Append(i < p.bunkerLevel ? "[x] " : "[ ] ").Append(job.room).Append("\n      ").Append(mark).Append('\n');
            }
            _list.text = sb.ToString();
            if (j == null)
            {
                _title.text = "ALL DONE";
                _desc.text = "Your bunker is fully expanded.\nGood doing business with you.";
                _reqs.text = "";
                _build.gameObject.SetActive(false);
                return;
            }
            _title.text = "JOB " + (p.bunkerLevel + 1) + ": " + j.name.ToUpper();
            _desc.text = j.desc;
            var r = new System.Text.StringBuilder("BRING ME\n");
            for (int i = 0; i < j.items.Length; i++)
            {
                var def = Db.Get(j.items[i]);
                int have = BunkerJobs.Progress(p, i);
                bool ok = have >= j.counts[i];
                r.Append(ok ? "[x] <color=#1d6b2a>" : "[ ] ").Append(def != null ? def.name : j.items[i]).Append("   ").Append(have).Append('/').Append(j.counts[i]).Append(ok ? "</color>" : "").Append('\n');
            }
            bool cash = p.money >= j.money;
            r.Append(cash ? "[x] <color=#1d6b2a>" : "[ ] <color=#a01818>").Append("Fee  $").Append(j.money.ToString("N0")).Append("</color>");
            int onTray = 0; foreach (var s in _tray.slots) if (s.item != null) onTray++;
            if (onTray > 0) r.Append("\n\n").Append(onTray).Append(" item(s) on the tray");
            _reqs.text = r.ToString();
            _build.gameObject.SetActive(true);
            _build.Interactable = BunkerJobs.ItemsDone(p) && cash;
        }

        void TurnIn()
        {
            var p = Profile.Current;
            int n = 0;
            foreach (var s in _tray.slots)
            {
                if (s.item == null || s.item.def == null) continue;
                if (BunkerJobs.Needed(p, s.item.def.id) <= 0) continue;
                BunkerJobs.TurnIn(p, s.item.def.id);
                var it = s.item; s.Release(it); Destroy(it.gameObject);
                n++;
            }
            if (n == 0) { Sfx.UI(Synth.UiError(), 0.3f); RagoGame.I.Player.Toast("Put items the job needs on the TURN IN tray"); return; }
            p.Save();
            Sfx.UI(Synth.Cash(), 0.5f);
            RagoGame.I.Player.Toast("Turned in " + n + " item(s)" + (BunkerJobs.ItemsDone(p) ? " - pay the fee to build" : ""));
            Refresh();
        }

        void Build()
        {
            var p = Profile.Current;
            var j = BunkerJobs.Current(p);
            if (j == null) return;
            if (!BunkerJobs.ItemsDone(p)) { Sfx.UI(Synth.UiError(), 0.3f); RagoGame.I.Player.Toast("Bring the items first"); return; }
            if (p.money < j.money) { Sfx.UI(Synth.UiError(), 0.3f); RagoGame.I.Player.Toast("Not enough money - fee is $" + j.money.ToString("N0")); return; }
            p.money -= j.money;
            p.bunkerLevel++;
            p.jobProgress.Clear();
            p.Save();
            Sfx.UI(Synth.Extracted(), 0.6f);
            RagoGame.I.Player.Hud.Center(j.room + " BUILT\n<size=40>Go and see it in your bunker</size>", 5f);
            Refresh();
        }

        public void SweepToStash(List<ItemData> into)
        {
            foreach (var s in _tray.slots) if (s.item != null) { into.Add(s.item.Save()); var it = s.item; s.Release(it); Destroy(it.gameObject); }
        }

        void LateUpdate()
        {
            if (_npc == null || RagoGame.I == null || RagoGame.I.Player == null) return;
            var pl = RagoGame.I.Player;
            float t = Time.time + 1.7f;
            Vector3 head = _npcPos + Vector3.up * (1.62f + Mathf.Sin(t * 1.6f) * 0.006f);
            Vector3 look = pl.Head.position;
            bool near = Vector3.Distance(look, _npcPos) < 7f;
            Vector3 dir = near ? (look - head).normalized : transform.rotation * Quaternion.Euler(0, Mathf.Sin(t * 0.3f) * 30f, 0) * Vector3.back;
            Quaternion hr = Quaternion.Slerp(transform.rotation * Quaternion.Euler(0, 180, 0), Quaternion.LookRotation(dir), 0.8f);
            Quaternion yaw = Quaternion.Euler(0, _yaw, 0);
            // one hand on the counter, the other tapping the plans
            Vector3 hl = transform.TransformPoint(new Vector3(-0.5f, 1.1f, 0.25f));
            Vector3 hrp = transform.TransformPoint(new Vector3(0.15f + Mathf.Sin(t * 0.9f) * 0.06f, 1.12f, 0.2f));
            Quaternion handRot = yaw * Quaternion.Euler(70, 0, 0);
            _npc.Pose(_npcPos, _yaw, head, hr, hl, handRot, hrp, handRot, Vector3.zero, Time.deltaTime);
        }
    }

    // ======================================================================================= BUNKER ROOM PROPS
    /// <summary>A sealed steel door in front of a bunker expansion that hasn't been built yet.</summary>
    public static class SealedDoor
    {
        public static void Create(WorldBuilder b, Transform parent, Vector3 pos, float yaw, string room, int jobNumber)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var steel = Mats.Lit("sealed_door", new Color(0.32f, 0.33f, 0.33f), 0.7f, 0.35f, Mats.Metal, Mats.MetalN, 0.5f, 0.5f);
            b.Box(pos + Vector3.up * 1.2f, new Vector3(2.3f, 2.5f, 0.12f), steel, Color.white, q, true, Synth.Surface.Metal, 0.01f);
            // hazard stripes and welded bars
            var mb = new MB();
            for (int i = 0; i < 9; i++) mb.Box(new Vector3(-1.0f + i * 0.25f, 0.25f, -0.07f), new Vector3(0.11f, 0.42f, 0.01f), i % 2 == 0 ? new Color(0.95f, 0.75f, 0.1f) : new Color(0.06f, 0.06f, 0.06f), 0f, 0, Quaternion.Euler(0, 0, 35));
            foreach (var y in new[] { 0.8f, 1.7f }) mb.Box(new Vector3(0, y, -0.08f), new Vector3(2.2f, 0.08f, 0.03f), new Color(0.2f, 0.2f, 0.2f), 0.005f);
            var go = mb.Build("Hazard", parent, Mats.Lit("hazard", Color.white, 0.3f, 0.35f, Mats.Noise, null, 0.6f, 0.4f));
            go.transform.SetPositionAndRotation(parent.TransformPoint(pos), parent.rotation * q);
            var sign = new GameObject("DoorSign").transform; sign.SetParent(parent, false);
            sign.localPosition = pos + q * new Vector3(0, 1.45f, -0.08f); sign.localRotation = q;
            UI.Text(sign, room, new Vector3(0, 0.12f, 0), 0.07f, new Color(1f, 0.75f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(sign, "SEALED", new Vector3(0, 0f, 0), 0.05f, new Color(1f, 0.35f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(sign, "Bunker job " + jobNumber + " - see Bruno the Foreman in the Market", new Vector3(0, -0.1f, 0), 0.028f, UI.Dim);
        }
    }

    /// <summary>The generator: earns money every raid. Collect it here.</summary>
    public class GeneratorPanel : MonoBehaviour
    {
        TextMesh _txt;
        public static GeneratorPanel Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("GeneratorPanel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var g = go.AddComponent<GeneratorPanel>();
            var p = UI.Panel(go.transform, "Panel", Vector3.zero, Quaternion.identity, new Vector2(0.9f, 0.5f));
            UI.Text(p, "GENERATOR", new Vector3(0, 0.18f, 0), 0.04f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            g._txt = UI.Text(p, "", new Vector3(0, 0.05f, 0), 0.026f, Color.white);
            UI.Button(p, "COLLECT", new Vector3(0, -0.15f, 0), new Vector2(0.4f, 0.09f), g.Collect, new Color(0.2f, 0.5f, 0.2f));
            return g;
        }

        void Collect()
        {
            var p = Profile.Current;
            if (p == null || p.genCash <= 0) { Sfx.UI(Synth.UiError(), 0.3f); RagoGame.I.Player.Toast("Nothing yet - it makes $" + BunkerJobs.GeneratorPerRaid.ToString("N0") + " every raid"); return; }
            int n = p.genCash;
            p.money += n; p.genCash = 0; p.Save();
            Sfx.UI(Synth.Cash(), 0.7f);
            RagoGame.I.Player.Toast("Collected $" + n.ToString("N0"));
        }

        void Update()
        {
            if (Time.frameCount % 15 != 0 || _txt == null || Profile.Current == null) return;
            var p = Profile.Current;
            _txt.text = "<color=#80ff80>$" + p.genCash.ToString("N0") + "</color> ready\n+$" + BunkerJobs.GeneratorPerRaid.ToString("N0") + " per raid (holds $" + BunkerJobs.GeneratorCap.ToString("N0") + ")";
        }
    }
}
