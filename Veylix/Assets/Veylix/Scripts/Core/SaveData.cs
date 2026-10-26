using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Veylix
{
    public enum GameMode { GrandPrix, QuickRace, TimeTrial, Practice, Championship, Elimination, FreeRoam, Online }

    [Serializable]
    public class SessionConfig
    {
        public GameMode mode = GameMode.QuickRace;
        public int map;
        public int car;
        public int laps = 5;
        public int opponents = 11;
        public int difficulty = 2;       // 0 Rookie .. 4 Legend
        public int timePreset = -1;      // -1 = map default, 0 Morning, 1 Afternoon, 2 Sunset, 3 Evening, 4 Night, 5 Custom
        public float customHour = 14f;
        public int weather = -1;         // -1 = map default, else WeatherKind, 6 = Dynamic
        public bool dynamicTime;
        public int gridPosition = 6;     // 1-based
        public int eliminationEvery = 1; // laps
        public string Mode => mode.ToString();

        public SessionConfig Clone() => (SessionConfig)MemberwiseClone();

        public static readonly string[] DifficultyNames = { "Rookie", "Amateur", "Professional", "Elite", "Legend" };
        public static readonly string[] TimeNames = { "Map default", "Morning", "Afternoon", "Sunset", "Evening", "Night", "Custom" };
        public static readonly string[] WeatherNames = { "Map default", "Clear", "Cloudy", "Overcast", "Light rain", "Heavy rain", "Fog", "Dynamic" };

        public float Hour(MapDef m)
        {
            switch (timePreset)
            {
                case 0: return 8.2f;
                case 1: return 14.5f;
                case 2: return 19.0f;
                case 3: return 20.3f;
                case 4: return 23f;
                case 5: return customHour;
                default: return m.hour;
            }
        }

        public WeatherKind Weather(MapDef m) => weather < 0 || weather >= 6 ? m.weather : (WeatherKind)weather;
        public bool DynamicWeather => weather == 6 || mode == GameMode.FreeRoam && weather < 0;
    }

    [Serializable]
    public class LapRecord { public string map; public float time; public int car; public string date; }

    [Serializable]
    public class ChampionshipState
    {
        public bool active;
        public List<int> calendar = new List<int>();
        public int round;
        public List<string> names = new List<string>();
        public List<int> points = new List<int>();
        public int playerIndex;
        public int laps = 5, difficulty = 2, opponents = 11;
        public List<string> roundWinners = new List<string>();
    }

    [Serializable]
    public class Save
    {
        public List<LapRecord> timeTrial = new List<LapRecord>();
        public List<LapRecord> raceLaps = new List<LapRecord>();
        public List<string> setupKeys = new List<string>();
        public List<string> setupJson = new List<string>();
        public SessionConfig lastSession = new SessionConfig();
        public ChampionshipState championship = new ChampionshipState();
        public int races, wins, podiums, championshipsWon;
        public float distanceKm;
        public List<string> championshipHistory = new List<string>();
    }

    /// <summary>Everything that persists on the headset: personal bests, garage setups, championship, stats.</summary>
    public static class SaveData
    {
        static Save data;
        static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "veylix-save.json");

        public static Save D
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static void Load()
        {
            try { data = File.Exists(PathFile) ? JsonUtility.FromJson<Save>(File.ReadAllText(PathFile)) : new Save(); }
            catch (Exception e) { Debug.LogWarning("[Veylix] Couldn't read the save, starting fresh: " + e.Message); data = new Save(); }
            if (data == null) data = new Save();
        }

        public static void Write()
        {
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(D)); }
            catch (Exception e) { Debug.LogWarning("[Veylix] Couldn't save: " + e.Message); }
        }

        public static CarSetup Setup(int car)
        {
            string key = CarCatalog.Get(car).id;
            int i = D.setupKeys.IndexOf(key);
            if (i >= 0)
            {
                try { return JsonUtility.FromJson<CarSetup>(D.setupJson[i]) ?? new CarSetup(); }
                catch { return new CarSetup(); }
            }
            return new CarSetup();
        }

        public static void StoreSetup(int car, CarSetup s)
        {
            string key = CarCatalog.Get(car).id;
            int i = D.setupKeys.IndexOf(key);
            string json = JsonUtility.ToJson(s);
            if (i >= 0) D.setupJson[i] = json; else { D.setupKeys.Add(key); D.setupJson.Add(json); }
            Write();
        }

        public static float BestLap(List<LapRecord> list, string map, int car = -1)
        {
            float best = float.MaxValue;
            foreach (var r in list) if (r.map == map && (car < 0 || r.car == car) && r.time < best) best = r.time;
            return best;
        }

        /// <summary>Records a lap if it's a personal best for this map (and car). Returns true if it was.</summary>
        public static bool SubmitLap(List<LapRecord> list, string map, int car, float time)
        {
            float prev = BestLap(list, map, car);
            if (time >= prev) return false;
            list.RemoveAll(r => r.map == map && r.car == car);
            list.Add(new LapRecord { map = map, car = car, time = time, date = DateTime.Now.ToString("yyyy-MM-dd") });
            Write();
            return true;
        }

        // Ghost laps (time trial) are stored per map in their own small files.
        static string GhostFile(string map) => System.IO.Path.Combine(Application.persistentDataPath, "ghost-" + map + ".json");

        [Serializable] public class Ghost { public int car; public float time; public float interval; public List<Vector3> pos = new List<Vector3>(); public List<Quaternion> rot = new List<Quaternion>(); }

        public static void SaveGhost(string map, Ghost g)
        {
            try { File.WriteAllText(GhostFile(map), JsonUtility.ToJson(g)); } catch (Exception e) { Debug.LogWarning("[Veylix] Ghost not saved: " + e.Message); }
        }

        public static Ghost LoadGhost(string map)
        {
            try { return File.Exists(GhostFile(map)) ? JsonUtility.FromJson<Ghost>(File.ReadAllText(GhostFile(map))) : null; } catch { return null; }
        }

        public static string FormatTime(float t, bool showHours = false)
        {
            if (t >= float.MaxValue * 0.5f || t <= 0f) return "--:--.---";
            int m = Mathf.FloorToInt(t / 60f);
            float s = t - m * 60f;
            return m + ":" + s.ToString("00.000");
        }

        public static string FormatDelta(float d) => (d >= 0f ? "+" : "-") + Mathf.Abs(d).ToString("0.000");
    }
}
