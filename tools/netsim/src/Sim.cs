using System; using System.Collections.Generic; using System.Linq; using System.Reflection; using UnityEngine; using Fusion; using Rago.Net;
namespace Rago
{
    public class Profile { public static Profile Current; public string DisplayName; }
    public static class RaidChoice { public static bool Night; }
    public class PlayerStub { public void Toast(string s) { } }
    public class RagoGame { public static RagoGame I; public PlayerStub Player; }

    public class Sim
    {
        class Node { public int Index; public Profile Prof; public FusionBackend B; public Dictionary<ulong, HashSet<int>> Heard = new Dictionary<ulong, HashSet<int>>(); }
        static List<Node> _nodes;
        static readonly BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static void Use(Node n) { Profile.Current = n.Prof; }

        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "queue") return QueueSim.Run();
            int fails = 0;
            fails += Run("3 players, Photon behaving", 3, false, false);
            fails += Run("4 players, Photon behaving", 4, false, false);
            fails += Run("3 players, Photon labels everything as the master", 3, true, false);
            fails += Run("4 players, Photon labels everything as the master", 4, true, false);
            fails += Run("3 players, no data straight between non-masters", 3, false, true);
            fails += Run("4 players, mislabelled AND no data between non-masters", 4, true, true);
            fails += Run("4 players, master leaves mid-raid", 4, false, true, masterLeaves: true);
            Console.WriteLine(fails == 0 ? "\nALL PASSED" : "\n" + fails + " FAILED");
            return fails;
        }

        static void Step(float dt)
        {
            Time.realtimeSinceStartup += dt; Time.unscaledTime += dt; Time.time += dt; Time.unscaledDeltaTime = dt; Time.deltaTime = dt;
            foreach (var n in _nodes.ToArray())
            {
                if (n.B == null) continue;
                Use(n);
                typeof(FusionBackend).GetMethod("Update", Any).Invoke(n.B, null);
                // a "pose" from this player: [marker][who I really am]
                if (n.B.InSession) n.B.Send(new byte[] { 77, (byte)n.Index }, false);
                typeof(FusionBackend).GetMethod("LateUpdate", Any).Invoke(n.B, null);
            }
            Cloud.Pump();
        }

        static int Run(string title, int count, bool mislabel, bool drop, bool masterLeaves = false)
        {
            Console.WriteLine("\n=== " + title);
            Cloud.Rooms.Clear(); Cloud.Queue.Clear(); Cloud.MislabelAsMaster = mislabel; Cloud.DropBetweenClients = drop;
            _nodes = new List<Node>();
            Cloud.BeforeDeliver = r => { var n = _nodes.FirstOrDefault(x => x.B != null && r.Cbs.Contains(x.B)); if (n != null) Use(n); };
            for (int i = 0; i < count; i++)
            {
                var n = new Node { Index = i, Prof = new Profile { DisplayName = "PLAYER" + i + "#" + (1000 + i) } };
                var go = new GameObject("node" + i);
                n.B = go.AddComponent<FusionBackend>();
                typeof(FusionBackend).GetField("_ready", Any).SetValue(n.B, true);
                var node = n;
                n.B.OnData += (from, data) => { if (data.Length == 2 && data[0] == 77) { if (!node.Heard.TryGetValue(from, out var s)) node.Heard[from] = s = new HashSet<int>(); s.Add(data[1]); } };
                _nodes.Add(n);
                Use(n);
                bool joined = false;
                n.B.JoinMarket(ok => joined = ok);
                if (!joined) { Console.WriteLine("  player " + i + " couldn't join"); return 1; }
                for (int k = 0; k < 10; k++) Step(1f / 30f);   // a moment between joins
            }
            for (int k = 0; k < 300; k++) Step(1f / 30f);       // 10 s of play
            int fails = Check("after 10 s", -1);
            if (masterLeaves)
            {
                var master = _nodes.First(x => x.B.IsHost);
                Console.WriteLine("  master (player " + master.Index + ") leaves");
                Use(master); master.B.Leave();
                for (int k = 0; k < 2; k++) Step(1f / 30f);
                master.B = null;
                foreach (var x in _nodes) x.Heard.Clear();
                for (int k = 0; k < 300; k++) Step(1f / 30f);
                fails += Check("after the master left", master.Index);
            }
            Console.WriteLine("  packets sent " + Cloud.Sent + ", delivered " + Cloud.Delivered);
            return fails;
        }

        static int Check(string when, int gone)
        {
            int fails = 0;
            foreach (var n in _nodes)
            {
                if (n.B == null) continue;
                var expect = _nodes.Where(x => x != n && x.B != null).Select(x => x.Index).ToHashSet();
                var seen = new HashSet<int>(); bool mixed = false;
                foreach (var kv in n.Heard) { if (kv.Value.Count > 1) mixed = true; seen.UnionWith(kv.Value); }
                bool ok = !mixed && seen.SetEquals(expect) && n.Heard.Count == expect.Count;
                // names: does this player know the right callsign for each id?
                var names = (Dictionary<ulong, string>)typeof(FusionBackend).GetField("_names", Any).GetValue(n.B);
                foreach (var kv in n.Heard)
                {
                    int who = kv.Value.First();
                    if (!names.TryGetValue(kv.Key, out var nm) || nm != _nodes[who].Prof.DisplayName) { ok = false; Console.WriteLine("    player " + n.Index + ": id " + kv.Key + " is player " + who + " but named '" + (nm ?? "?") + "'"); }
                }
                Console.WriteLine("  " + (ok ? "ok  " : "FAIL") + " player " + n.Index + (n.B.IsHost ? " (master)" : "") + " " + when + ": hears " + string.Join(", ", n.Heard.Select(kv => kv.Key + "=>[" + string.Join(",", kv.Value) + "]")) + (mixed ? "  <- MIXED" : ""));
                if (!ok) fails++;
            }
            return fails;
        }
    }
}
