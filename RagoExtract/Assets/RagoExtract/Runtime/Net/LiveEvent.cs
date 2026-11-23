using UnityEngine;

namespace Rago
{
    /// <summary>
    /// LIVE EVENTS for everyone on the game server at once. A moderator starts one from the MOD tab (or the owner from the
    /// web panel); the server tells every player, and players who join while it's on. One at a time:
    ///   DOUBLE XP     - every bit of XP counts twice
    ///   DOUBLE CASH   - selling to the traders pays double
    ///   BOSS HUNT     - every map's boss turns up in every raid
    ///   AIR DROP RAIN - a plane drops a crate every few minutes in every raid
    ///   PRESENT RAIN  - crates everywhere hold presents
    /// </summary>
    public class LiveEvent : MonoBehaviour
    {
        public const byte None = 0, DoubleXp = 1, DoubleCash = 2, BossHunt = 3, AirDrops = 4, Presents = 5;
        public static readonly string[] Names = { "", "DOUBLE XP", "DOUBLE CASH", "BOSS HUNT", "AIR DROP RAIN", "PRESENT RAIN" };
        public static readonly string[] Blurb = { "", "all XP counts twice", "traders pay double", "every boss is out in every raid", "air drops every few minutes in raids", "presents in crates everywhere" };
        public static readonly int[] Lengths = { 30, 60, 120, 360, 1440 };

        public static byte Kind { get; private set; }
        public static string By = "";
        static float _ends;
        static LiveEvent _i;

        public static bool Active => Kind != None && Time.realtimeSinceStartup < _ends;
        public static bool Is(byte k) => Active && Kind == k;
        public static int Mult(byte k) => Is(k) ? 2 : 1;

        public static string Left()
        {
            if (!Active) return "";
            int m = Mathf.CeilToInt((_ends - Time.realtimeSinceStartup) / 60f);
            return m >= 60 ? (m / 60) + "h " + (m % 60).ToString("00") + "m" : m + "m";
        }

        /// <summary>From the server: kind (0 = none), seconds left, who started it.</summary>
        public static void Set(byte kind, int seconds, string by)
        {
            bool was = Active; byte old = Kind;
            Kind = kind < Names.Length ? kind : None; By = by ?? "";
            _ends = Time.realtimeSinceStartup + Mathf.Max(0, seconds);
            if (_i == null) { var go = new GameObject("LiveEvent"); DontDestroyOnLoad(go); _i = go.AddComponent<LiveEvent>(); }
            _i._nextDrop = Time.time + 60f;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (Active && (!was || old != Kind))
            {
                Sfx.UI(Synth.Extracted(), 0.6f);
                pl.Hud.Center("LIVE EVENT: " + Names[Kind] + "\n<size=40>" + Blurb[Kind] + " - for " + Left() + "</size>", 6f);
                pl.Toast("LIVE EVENT: " + Names[Kind] + " - " + Blurb[Kind] + " (" + Left() + ")");
            }
            else if (!Active && was) pl.Toast("The live event (" + Names[old] + ") is over");
        }

        float _nextDrop;
        void Update()
        {
            // AIR DROP RAIN: the raid's host calls a drop every 3-4 minutes, everyone sees it
            if (!Is(AirDrops) || !NetGame.IsHost || Time.time < _nextDrop) return;
            _nextDrop = Time.time + Random.Range(180f, 240f);
            var g = RagoGame.I;
            if (g == null || !AirDrop.Allowed(out _) || RaidManager.I == null || RaidManager.I.Done) return;
            if (AirDrop.PickSpot(g.Map, new Rng(Random.Range(1, 999999)), out var spot))
                AirDrop.Call(spot, Random.Range(0f, 360f), Random.value < 0.25f, true);
        }
    }
}
