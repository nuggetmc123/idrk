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
    ///   CONCERT       - a stage, a DJ, lights and lasers in the Market
    ///   NUKE          - a 5-minute countdown in every raid: extract before it hits, or die in the blast
    /// </summary>
    public class LiveEvent : MonoBehaviour
    {
        public const byte None = 0, DoubleXp = 1, DoubleCash = 2, BossHunt = 3, AirDrops = 4, Presents = 5, Concert = 6, Nuke = 7;
        public static readonly string[] Names = { "", "DOUBLE XP", "DOUBLE CASH", "BOSS HUNT", "AIR DROP RAIN", "PRESENT RAIN", "CONCERT", "NUKE" };
        public static readonly string[] Blurb = { "", "all XP counts twice", "traders pay double", "every boss is out in every raid", "air drops every few minutes in raids", "presents in crates everywhere",
            "live DJ, lights and lasers in the Market - come down!", "every raid gets nuked when the timer hits zero - EXTRACT NOW" };
        public const int NukeMinutes = 5;
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
            if (kind != old || !was) _detonated = false;
            Kind = kind < Names.Length ? kind : None; By = by ?? "";
            _ends = Time.realtimeSinceStartup + Mathf.Max(0, seconds);
            if (_i == null) { var go = new GameObject("LiveEvent"); DontDestroyOnLoad(go); _i = go.AddComponent<LiveEvent>(); }
            _i._nextDrop = Time.time + 60f;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (Active && Kind == Nuke && (!was || old != Kind))
            {
                Sfx.UI(Synth.Alarm(), 0.8f);
                pl.Hud.Center("NUCLEAR STRIKE INBOUND\n<size=40>every raid is hit in " + Clock() + " - extract or die</size>", 7f);
                pl.Toast("NUKE: every raid gets hit in " + Clock() + " - get to an extract!");
                _i._nextSiren = Time.time + 25f;
            }
            else if (Active && (!was || old != Kind))
            {
                Sfx.UI(Synth.Extracted(), 0.6f);
                pl.Hud.Center("LIVE EVENT: " + Names[Kind] + "\n<size=40>" + Blurb[Kind] + " - for " + Left() + "</size>", 6f);
                pl.Toast("LIVE EVENT: " + Names[Kind] + " - " + Blurb[Kind] + " (" + Left() + ")");
            }
            else if (!Active && was && old == Nuke) pl.Toast("The nuke was called off");
            else if (!Active && was) pl.Toast("The live event (" + Names[old] + ") is over");
        }

        public static string Clock()
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(_ends - Time.realtimeSinceStartup));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        static bool InRaid { get { var g = RagoGame.I; return g != null && g.Area == Area.Raid && g.Map != null && RaidManager.I != null && !RaidManager.I.Done; } }

        float _nextDrop, _nextSiren; static bool _detonated; int _lastSec = -1;
        void Update()
        {
            ConcertStage.Tick();
            NukeTick();
            // AIR DROP RAIN: the raid's host calls a drop every 3-4 minutes, everyone sees it
            if (!Is(AirDrops) || !NetGame.IsHost || Time.time < _nextDrop) return;
            _nextDrop = Time.time + Random.Range(180f, 240f);
            var g = RagoGame.I;
            if (g == null || !AirDrop.Allowed(out _) || RaidManager.I == null || RaidManager.I.Done) return;
            if (AirDrop.PickSpot(g.Map, new Rng(Random.Range(1, 999999)), out var spot))
                AirDrop.Call(spot, Random.Range(0f, 360f), Random.value < 0.25f, true);
        }
    
        // ---------------------------------------------------------------------------------------------- the nuke
        void NukeTick()
        {
            if (Kind != Nuke || _detonated) return;
            float left = _ends - Time.realtimeSinceStartup;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (left <= 0f)
            {
                _detonated = true;
                if (InRaid && !pl.Dead) StartCoroutine(Detonate(pl));
                return;
            }
            if (!InRaid) return;
            // the air-raid siren every 30 s, the countdown in your face for the last minute
            if (Time.time > _nextSiren) { _nextSiren = Time.time + 30f; Sfx.Play(Synth.Alarm(), pl.Head.position + Vector3.up * 30f, 0.6f, 0.55f, 40f, 400f); }
            int sec = Mathf.CeilToInt(left);
            if (sec != _lastSec)
            {
                _lastSec = sec;
                if (sec == 60 || sec == 30 || sec <= 10) { pl.Hud.Center("NUKE IN " + sec + "\n<size=40>get to an extract!</size>", 1.1f); if (sec <= 10) Sfx.UI(Synth.Beep(), 0.5f); }
            }
        }

        System.Collections.IEnumerator Detonate(PlayerRig pl)
        {
            var g = RagoGame.I;
            // ground zero: the middle of the map - or well out in front of you if you're standing on it, so you see it go up
            Vector3 gz = g.Map.transform.position;
            Vector3 fl = pl.Head.forward; fl.y = 0; if (fl.sqrMagnitude < 0.01f) fl = Vector3.forward;
            if ((gz - pl.transform.position).magnitude < 250f) gz = pl.transform.position + fl.normalized * 450f;
            gz.y = pl.transform.position.y;
            // the missile comes down, the flash, then the shockwave rolls out - you're gone when it reaches you
            var fx = NukeFx.Play(gz);
            Sfx.Play(Synth.Alarm(), pl.Head.position + Vector3.up * 30f, 0.7f, 0.7f, 40f, 400f);
            while (fx != null && fx.T < 0f) yield return null;
            pl.Hud.Flashbang(3f);
            Sfx.UI(Synth.ExplosionFar(), 1f);
            Sfx.Play(Synth.Explosion(), pl.Head.position + (gz - pl.Head.position).normalized * 40f, 1f, 0.35f, 60f, 800f, 0.3f);
            float dist = new Vector2(pl.transform.position.x - gz.x, pl.transform.position.z - gz.z).magnitude;
            while (fx != null && fx.WaveRadius < dist) yield return null;
            // the blast wave: everyone still in the raid is gone, raiders and all
            if (InRaid && !pl.Dead)
            {
                Sfx.UI(Synth.Explosion(), 1f);
                pl.Hud.Flashbang(1.5f);
                if (NpcDirector.I != null)
                    foreach (var n in NpcDirector.I.Npcs.ToArray())
                        if (n != null && n.Alive && !n.Puppet) n.Die(n.transform.position + Vector3.up, (n.transform.position - gz).normalized * 60f + Vector3.up * 20f, false);
                pl.Vitals.Kill("NUKED - the strike hit before you got out");
            }
        }
    }
}
