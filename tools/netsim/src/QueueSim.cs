using System; using System.Collections.Generic; using System.Diagnostics; using System.Linq; using System.Reflection; using System.Threading; using System.Threading.Tasks; using UnityEngine; using Fusion; using Rago.Net;
namespace Rago
{
    /// <summary>Runs async continuations on the sim's one thread, like Unity's main thread.</summary>
    class MainThread : SynchronizationContext
    {
        readonly Queue<(SendOrPostCallback, object)> _q = new Queue<(SendOrPostCallback, object)>();
        public override void Post(SendOrPostCallback d, object s) { lock (_q) _q.Enqueue((d, s)); }
        public void Run() { while (true) { (SendOrPostCallback, object) w; lock (_q) { if (_q.Count == 0) return; w = _q.Dequeue(); } w.Item1(w.Item2); } }
    }

    public static class QueueSim
    {
        class Node { public MainThread Ctx = new MainThread(); public string Name; public Profile Prof; public FusionBackend B; public RaidStart Start; public string Session; public string Fail; }
        static readonly BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static List<Node> _n; static MainThread _ctx; static Stopwatch _sw;

        static void Use(Node n) { Profile.Current = n.Prof; SynchronizationContext.SetSynchronizationContext(n.Ctx); }
        static void Tick()
        {
            float now = (float)_sw.Elapsed.TotalSeconds;
            Time.unscaledDeltaTime = Time.deltaTime = Mathf.Max(0.001f, now - Time.realtimeSinceStartup);
            Time.realtimeSinceStartup = Time.unscaledTime = Time.time = now;
            foreach (var n in _n) { Use(n); n.Ctx.Run(); }
            foreach (var n in _n)
            {
                Use(n);
                typeof(FusionBackend).GetMethod("Update", Any).Invoke(n.B, null);
                typeof(FusionBackend).GetMethod("LateUpdate", Any).Invoke(n.B, null);
            }
            Cloud.Pump();
            foreach (var n in _n) { Use(n); n.Ctx.Run(); }
        }
        static float _lastList;
        static void Wait(float secs)
        {
            float until = (float)_sw.Elapsed.TotalSeconds + secs;
            while (_sw.Elapsed.TotalSeconds < until) { Tick(); if (_sw.Elapsed.TotalSeconds - _lastList > 1) { _lastList = (float)_sw.Elapsed.TotalSeconds; Cloud.PushLists(); } Thread.Sleep(15); }
        }
        static Node Make(string name)
        {
            var n = new Node { Name = name, Prof = new Profile { DisplayName = name } };
            n.B = new GameObject(name).AddComponent<FusionBackend>();
            typeof(FusionBackend).GetField("_ready", Any).SetValue(n.B, true);
            _n.Add(n);
            var node = n;
            n.B.OnPartyQueue += map => { Use(node); Queue(node, map); };
            Use(n); typeof(FusionBackend).GetMethod("EnterLobby", Any).Invoke(n.B, null);
            return n;
        }
        static void Queue(Node n, string map)
        {
            Use(n);
            n.B.QueueRaid(map, s => { }, rs => { n.Start = rs; n.Session = n.B.SessionId; }, why => n.Fail = why);
        }
        static bool Party(Node leader, params Node[] members)
        {
            Use(leader);
            var t = (Task<bool>)typeof(FusionBackend).GetMethod("EnterParty", Any).Invoke(leader.B, new object[] { leader.Name, true });
            Wait(0.5f);
            foreach (var m in members) { Use(m); typeof(FusionBackend).GetMethod("EnterParty", Any).Invoke(m.B, new object[] { leader.Name, false }); Wait(1f); }
            Wait(2f);
            Use(leader);
            Console.WriteLine("  party: " + string.Join(", ", leader.B.PartyMembers));
            return leader.B.PartyMembers.Count == members.Length + 1;
        }

        static int Scenario(string title, Action setup, int expectPlayers)
        {
            Console.WriteLine("\n=== " + title);
            Cloud.Rooms.Clear(); Cloud.Queue.Clear(); Cloud.Lobby.Clear(); Cloud.MislabelAsMaster = true; Cloud.DropBetweenClients = true;
            _n = new List<Node>();
            Cloud.BeforeDeliver = r => { var n = _n.FirstOrDefault(x => r.Cbs.Contains(x.B)); if (n != null) Use(n); };
            setup();
            float deadline = (float)_sw.Elapsed.TotalSeconds + 80f;
            while (_sw.Elapsed.TotalSeconds < deadline && _n.Any(x => x.Start == null && x.Fail == null)) Wait(1f);
            int fails = 0;
            var seeds = _n.Where(x => x.Start != null).Select(x => x.Start.seed).Distinct().ToList();
            foreach (var x in _n)
            {
                bool ok = x.Start != null && seeds.Count == 1 && x.Start.players == expectPlayers;
                Console.WriteLine("  " + (ok ? "ok  " : "FAIL") + " " + x.Name + ": " + (x.Start == null ? "never started (" + (x.Fail ?? "stuck") + ")" : "raid in " + x.Session + ", seed " + x.Start.seed + ", " + x.Start.players + " players, spawn " + x.Start.spawnIndex + "/" + x.Start.partySlot));
                if (!ok) fails++;
            }
            // spawns: party members share one spot on different slots; everyone else has their own spot
            var started = _n.Where(x => x.Start != null).ToList();
            for (int i = 0; i < started.Count; i++) for (int j = i + 1; j < started.Count; j++)
            {
                var a = started[i]; var b = started[j];
                string pa = a.B.PartyCode, pb = b.B.PartyCode;
                bool same = pa != null && pa == pb;
                bool bad = same ? (a.Start.spawnIndex != b.Start.spawnIndex || a.Start.partySlot == b.Start.partySlot) : a.Start.spawnIndex == b.Start.spawnIndex;
                if (bad) { fails++; Console.WriteLine("  FAIL spawn: " + a.Name + " and " + b.Name + (same ? " (same party)" : "") + " at " + a.Start.spawnIndex + "/" + a.Start.partySlot + " and " + b.Start.spawnIndex + "/" + b.Start.partySlot); }
            }
            foreach (var x in _n) { Use(x); x.B.Leave(); }
            Wait(0.5f);
            return fails;
        }

        public static int Run()
        {
            _ctx = new MainThread(); SynchronizationContext.SetSynchronizationContext(_ctx); _sw = Stopwatch.StartNew();
            int f = 0;
            f += Scenario("party of 3 queues", () => { var l = Make("LEAD#0001"); var a = Make("AMY#0002"); var b = Make("BEN#0003"); Wait(1f); if (!Party(l, a, b)) Console.WriteLine("  (party didn't form)"); Queue(l, "outskirts"); }, 3);
            f += Scenario("party of 2 + a solo player queueing at the same time", () => { var l = Make("LEAD#0001"); var a = Make("AMY#0002"); var s = Make("SOLO#0009"); Wait(1f); Party(l, a); Queue(l, "outskirts"); Queue(s, "outskirts"); }, 3);
            f += Scenario("3 solo players, a few seconds apart", () => { var a = Make("A#0001"); var b = Make("B#0002"); var c = Make("C#0003"); Wait(1f); Queue(a, "outskirts"); Wait(3f); Queue(b, "outskirts"); Wait(3f); Queue(c, "outskirts"); }, 3);
            f += Scenario("party of 3 + 1 solo who queued first", () => { var s = Make("SOLO#0009"); var l = Make("LEAD#0001"); var a = Make("AMY#0002"); var b = Make("BEN#0003"); Wait(1f); Queue(s, "outskirts"); Wait(2f); Party(l, a, b); Queue(l, "outskirts"); }, 4);
            Console.WriteLine(f == 0 ? "\nQUEUE: ALL PASSED" : "\nQUEUE: " + f + " FAILED");
            return f;
        }
    }
}
