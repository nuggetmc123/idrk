using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// FIND RACE: searches for other drivers while a timer counts up. Online, everyone searching shares a matchmaking
    /// room. The room's host sends the room to a race as soon as it's full (12), or when the minute is up and at
    /// least one other driver has turned up. If nobody has come after a minute (or online play is off), you race
    /// anyway on your own against the AI field. Every race has at most 12 cars.
    /// </summary>
    public class RaceFinder : MonoBehaviour
    {
        public static RaceFinder I { get; private set; }

        public const int MaxRacers = 12;
        public const float SearchSeconds = 60f;

        public bool Searching { get; private set; }
        public float Elapsed { get; private set; }
        public string Status { get; private set; } = "";
        /// Other drivers in the search room with you.
        public int OthersFound => Online.I != null && Online.I.InLobby ? Mathf.Max(0, Online.I.LobbyPlayers - 1) : 0;

        void Awake() { I = this; }

        public void Begin()
        {
            if (Searching) return;
            Searching = true;
            Elapsed = 0f;
            var on = Online.I;
            if (on != null && Online.Available)
            {
                if (on.InParty)
                {
                    // In a party the leader's search takes everyone to a public race; the others follow the leader.
                    if (on.IsPartyLeader) { _ = on.PartyGo(RaceConfig(), true); Stop("Taking your party to a race..."); }
                    else Stop("Your party leader starts the search.");
                    return;
                }
                Status = "Searching for drivers...";
                _ = on.StartMatchmaking();
            }
            else Status = "Searching for a race...";
        }

        public void Cancel()
        {
            if (!Searching) return;
            Searching = false;
            Status = "";
            if (Online.I != null) Online.I.StopMatchmaking();
        }

        /// The search ended because a race was found (Online calls this when the room heads off to a race).
        public void Stop(string status)
        {
            Searching = false;
            Status = status;
        }

        void Update()
        {
            if (!Searching) return;
            if (Game.I == null || Game.I.state != Game.State.Menu) { Cancel(); return; }
            Elapsed += Time.unscaledDeltaTime;
            var on = Online.I;
            bool online = on != null && on.InLobby;
            if (online)
            {
                int players = on.LobbyPlayers;
                Status = players > 1 ? (players - 1) + (players == 2 ? " driver" : " drivers") + " found. Waiting for more..." : "Searching for drivers...";
                if (on.IsLobbyMaster && (players >= MaxRacers || (Elapsed >= SearchSeconds && players >= 2)))
                {
                    var cfg = RaceConfig();
                    Stop("Race found! Heading to " + MapCatalog.Get(cfg.map).name + "...");
                    _ = on.LaunchLobbyRace(cfg.map, cfg.laps);
                    return;
                }
                // The host decides; if the host has gone quiet, don't wait forever.
                if (Elapsed >= SearchSeconds + (players >= 2 && !on.IsLobbyMaster ? 20f : 0f)) RaceAlone();
            }
            else if (Elapsed >= SearchSeconds && (on == null || !on.Busy)) RaceAlone();
        }

        /// Nobody turned up: a race against the AI, 12 cars on a random circuit.
        void RaceAlone()
        {
            Searching = false;
            if (Online.I != null) Online.I.StopMatchmaking();
            var cfg = RaceConfig();
            cfg.mode = GameMode.QuickRace;
            cfg.opponents = MaxRacers - 1;
            cfg.gridPosition = Random.Range(4, MaxRacers + 1);
            Status = "No other drivers right now. Racing the field on " + MapCatalog.Get(cfg.map).name + ".";
            Game.I.StartSession(cfg);
        }

        /// A random circuit (not an open world), your car, a sprint distance.
        static SessionConfig RaceConfig()
        {
            var cfg = Game.I.config.Clone();
            int tries = 0, map;
            do { map = Random.Range(0, MapCatalog.Count); } while (MapCatalog.Get(map).freeRoam && ++tries < 50);
            cfg.map = map;
            cfg.laps = Mathf.Clamp(MapCatalog.Get(map).laps, 3, 5);
            cfg.timePreset = -1;
            cfg.weather = -1;
            return cfg;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
