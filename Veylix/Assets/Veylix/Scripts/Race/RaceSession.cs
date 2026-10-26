using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>A car in the session, with its timing.</summary>
    public class Entrant
    {
        public Car car;
        public AIDriver ai;
        public string name, team;
        public int number;
        public bool isPlayer, remote;

        // Position on track
        public int hint = -1;
        public float along, lateral;
        public float lastAlong;

        // Timing
        public int lap;                  // laps completed
        public int nextCheckpoint;       // index of the next checkpoint to cross (0..N-1); N means "go for the line"
        public float lapStart;
        public float lastLap, bestLap = float.MaxValue;
        public float[] sectorBest = { float.MaxValue, float.MaxValue, float.MaxValue };
        public float[] sectorNow = new float[3];
        public float sectorStart;
        public int sector;
        public bool lapInvalid;
        public bool finished, eliminated, dnf;
        public float finishTime;
        public float penalty;            // seconds added to the race time
        public int warnings;
        public float offTrackT;
        public int position;
        public float gapToLeader;
        public readonly List<float> cpTimes = new List<float>(); // race time at every checkpoint crossing (for gaps)
        public int checkpointsCrossed;
        public float lastValidAlong;
        public Vector3 lastSafePos;
        public Quaternion lastSafeRot;
        public float safeTimer;
        public float wreckedSince = -1f;

        public float Progress(float trackLength) => lap * trackLength + (nextCheckpoint == 0 && along > trackLength * 0.5f ? along - trackLength : along);
        public float RaceTime => finishTime + penalty;
    }

    /// <summary>
    /// Runs a session in any mode: grid, start lights, timing (laps, sectors, checkpoints that must be crossed in
    /// order, track limits and penalties), live order and gaps, elimination, the chequered flag and the results. Also
    /// yellow flags around stricken cars, putting a car back on track, and time-trial personal bests with a ghost.
    /// </summary>
    public class RaceSession : MonoBehaviour
    {
        public enum Phase { Grid, Countdown, Racing, Finished, Results }

        public static RaceSession I { get; private set; }
        public SessionConfig config;
        public MapDef map;
        public Phase phase = Phase.Grid;
        public readonly List<Entrant> entrants = new List<Entrant>();
        public Entrant player;
        public int checkpoints = 24;
        public float[] cpAlong;
        public float raceStartTime;
        public int totalLaps;
        public float RaceClock => phase == Phase.Racing || phase == Phase.Finished ? Time.time - raceStartTime : 0f;
        public float LapClock(Entrant e) => phase == Phase.Racing || (phase == Phase.Finished && !e.finished) ? Time.time - e.lapStart : 0f;
        public bool Timed => config.mode != GameMode.FreeRoam;
        public bool HasOpponents => config.mode != GameMode.TimeTrial && config.mode != GameMode.Practice && config.mode != GameMode.FreeRoam;
        public bool IsRace => HasOpponents;
        public int lightsLit;
        public string bigMessage;
        public float bigMessageUntil;
        public float fastestLap = float.MaxValue;
        public string fastestLapBy = "";
        public bool newPersonalBest;
        public int eliminationsDone;
        public bool online;

        public event System.Action OnResults;
        public event System.Action<string, Color> OnMessage;

        TrackSpline track;
        float countdownT;
        readonly List<float> yellowZones = new List<float>();
        float finishedAt;
        // Time trial ghost
        SaveData.Ghost recording, bestGhost;
        float recordT;
        GhostCar ghostCar;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        public void Setup(SessionConfig cfg, MapDef m, List<Entrant> list, Entrant playerEntrant)
        {
            config = cfg;
            map = m;
            track = World.Current.track;
            entrants.Clear();
            entrants.AddRange(list);
            player = playerEntrant;
            totalLaps = cfg.mode == GameMode.TimeTrial || cfg.mode == GameMode.Practice || cfg.mode == GameMode.FreeRoam ? 9999 : Mathf.Max(1, cfg.laps);
            if (cfg.mode == GameMode.Elimination) totalLaps = Mathf.Max(totalLaps, (entrants.Count - 1) * Mathf.Max(1, cfg.eliminationEvery));
            cpAlong = new float[checkpoints];
            float start = World.Current.startS;
            for (int k = 0; k < checkpoints; k++) cpAlong[k] = Mathf.Repeat(start + track.Length * (k + 1) / (checkpoints + 1f), track.Length);
            foreach (var e in entrants)
            {
                track.Project(e.car.transform.position, ref e.hint, out e.along, out e.lateral);
                e.lastAlong = e.along;
                e.nextCheckpoint = 0;
                e.lastSafePos = e.car.transform.position;
                e.lastSafeRot = e.car.transform.rotation;
                e.car.Freeze(true);
                if (e.ai != null) e.ai.racing = false;
            }
            phase = Phase.Grid;
            countdownT = cfg.mode == GameMode.FreeRoam || cfg.mode == GameMode.Practice ? -1f : 2.5f;
            if (cfg.mode == GameMode.TimeTrial)
            {
                bestGhost = SaveData.LoadGhost(m.id);
                if (bestGhost != null && bestGhost.pos.Count > 10) ghostCar = GhostCar.Create(bestGhost);
            }
            if (countdownT < 0f) Go();
        }

        // ------------------------------------------------------------------ start

        void Update()
        {
            if (track == null) return;
            switch (phase)
            {
                case Phase.Grid:
                    if (online) break; // the network starts the countdown
                    countdownT -= Time.deltaTime;
                    if (countdownT <= 0f) BeginCountdown();
                    break;
                case Phase.Countdown:
                    Countdown();
                    break;
                case Phase.Racing:
                case Phase.Finished:
                    foreach (var e in entrants) Track(e);
                    Order();
                    Flags();
                    Ghost();
                    if (phase == Phase.Finished && Time.time - finishedAt > (HasOpponents ? 20f : 2f) && !online) ShowResults();
                    break;
            }
            if (World.Current != null && World.Current.startLights != null) World.Current.startLights.Set(phase == Phase.Countdown ? lightsLit : 0);
        }

        public void BeginCountdown()
        {
            if (phase != Phase.Grid) return;
            phase = Phase.Countdown;
            countdownT = 0f;
            lightsLit = 0;
            lightsOutAt = 5.2f + Random.Range(0.2f, 1.6f); // real F1: a random hold after the fifth light
        }

        float lightsOutAt;

        void Countdown()
        {
            countdownT += Time.deltaTime;
            int lit = Mathf.Clamp(Mathf.FloorToInt(countdownT), 0, 5);
            if (lit > lightsLit) { lightsLit = lit; CrashAudio.Beep(false); }
            // Engines rev on the grid (throttle works while frozen).
            if (countdownT >= lightsOutAt) Go();
        }

        void Go()
        {
            phase = Phase.Racing;
            lightsLit = 0;
            raceStartTime = Time.time;
            foreach (var e in entrants)
            {
                e.car.Freeze(false);
                e.lapStart = Time.time;
                e.sectorStart = Time.time;
                if (e.ai != null) e.ai.racing = true;
            }
            if (countdownT > 0f) { CrashAudio.Beep(true); Message("GO GO GO", VUI.Good, 2f); }
            if (config.mode == GameMode.TimeTrial) Message("TIME TRIAL - beat your best", VUI.Text, 3f);
            if (config.mode == GameMode.Practice) Message("PRACTICE - follow the line, find your braking points", VUI.Text, 4f);
            StartRecording();
        }

        /// <summary>Network clients call this when the host's lights go out.</summary>
        public void RemoteGo() { if (phase != Phase.Racing) Go(); }
        public void RemoteCountdown() { BeginCountdown(); }

        // ------------------------------------------------------------------ timing

        void Track(Entrant e)
        {
            if (e.car == null) return;
            e.lastAlong = e.along;
            track.Project(e.car.transform.position, ref e.hint, out e.along, out e.lateral);
            if (e.finished || e.eliminated || !Timed) { UpdateSafe(e); return; }
            if (e.remote) return; // remote cars report their own laps

            float L = track.Length;
            float delta = e.along - e.lastAlong;
            if (delta < -L * 0.5f) delta += L; else if (delta > L * 0.5f) delta -= L;
            // Checkpoints in order (crossing forwards).
            if (e.nextCheckpoint < checkpoints && delta > 0f && delta < 40f)
            {
                float cp = cpAlong[e.nextCheckpoint];
                if (Crossed(e.lastAlong, e.along, cp, L))
                {
                    e.nextCheckpoint++;
                    e.checkpointsCrossed++;
                    e.cpTimes.Add(RaceClock);
                    // Sector splits at a third and two thirds.
                    int sectorCp = checkpoints / 3;
                    if (e.nextCheckpoint == sectorCp || e.nextCheckpoint == sectorCp * 2) SectorSplit(e);
                }
            }
            // The finish line.
            float startS = World.Current.startS;
            if (delta > 0f && delta < 40f && Crossed(e.lastAlong, e.along, startS, L))
            {
                if (e.nextCheckpoint >= checkpoints) CompleteLap(e);
                else if (e.nextCheckpoint > 0)
                {
                    // Missed part of the track: the lap doesn't count until every checkpoint is crossed.
                    if (e.isPlayer) Message("LAP NOT COUNTED - missed checkpoint " + (e.nextCheckpoint + 1), VUI.Accent, 3f);
                }
            }
            TrackLimits(e);
            UpdateSafe(e);
            Wreck(e);
        }

        static bool Crossed(float a, float b, float x, float L)
        {
            float da = Mathf.Repeat(x - a, L), db = Mathf.Repeat(b - a, L);
            return da > 0f && da <= db && db < L * 0.5f;
        }

        void SectorSplit(Entrant e)
        {
            float t = Time.time - e.sectorStart;
            int s = e.sector;
            e.sectorNow[s] = t;
            bool best = t < e.sectorBest[s];
            if (e.isPlayer)
            {
                float d = e.sectorBest[s] < float.MaxValue ? t - e.sectorBest[s] : 0f;
                Message("S" + (s + 1) + "  " + SaveData.FormatTime(t) + (e.sectorBest[s] < float.MaxValue ? "   " + SaveData.FormatDelta(d) : ""), best ? VUI.Purple : VUI.Warn, 2.5f);
            }
            if (best && !e.lapInvalid) e.sectorBest[s] = t;
            e.sector = Mathf.Min(2, s + 1);
            e.sectorStart = Time.time;
        }

        void CompleteLap(Entrant e)
        {
            float lapTime = Time.time - e.lapStart;
            e.sectorNow[2] = Time.time - e.sectorStart;
            if (e.sectorNow[2] < e.sectorBest[2] && !e.lapInvalid) e.sectorBest[2] = e.sectorNow[2];
            e.lastLap = lapTime;
            bool valid = !e.lapInvalid;
            if (valid && lapTime < e.bestLap) e.bestLap = lapTime;
            if (valid && lapTime < fastestLap && IsRace) { fastestLap = lapTime; fastestLapBy = e.name; }
            e.lap++;
            e.nextCheckpoint = 0;
            e.lapStart = Time.time;
            e.sectorStart = Time.time;
            e.sector = 0;
            if (e.isPlayer)
            {
                string tag = !valid ? "  (INVALID)" : "";
                Message("LAP " + e.lap + "   " + SaveData.FormatTime(lapTime) + tag, !valid ? VUI.Accent : lapTime <= e.bestLap ? VUI.Purple : VUI.Text, 3.5f);
                CrashAudio.Play(!valid ? CrashAudio.Stinger.Warning : lapTime <= fastestLap ? CrashAudio.Stinger.FastestLap : CrashAudio.Stinger.Lap);
                if (valid) PlayerLapRecorded(lapTime);
                SaveData.D.distanceKm += track.Length / 1000f;
            }
            e.lapInvalid = false;
            e.warnings = Mathf.Max(0, e.warnings);
            // Elimination: at the end of every Nth lap of the leader, the last car is out.
            if (config.mode == GameMode.Elimination && e.position == 1 && e.lap % Mathf.Max(1, config.eliminationEvery) == 0) Eliminate();
            // Finish?
            if (IsRace && (e.lap >= totalLaps || (phase == Phase.Finished)))
            {
                if (phase == Phase.Racing) { phase = Phase.Finished; finishedAt = Time.time; Message(e.isPlayer ? "CHEQUERED FLAG - YOU WIN" : "CHEQUERED FLAG", VUI.Text, 4f); }
                Finish(e);
            }
        }

        void Finish(Entrant e)
        {
            if (e.finished) return;
            e.finished = true;
            e.finishTime = Time.time - raceStartTime;
            if (e.ai != null) e.ai.coolDown = true;
            if (e.isPlayer && phase == Phase.Finished && !(e.position == 1)) Message("FINISHED  P" + e.position, VUI.Text, 4f);
            if (e.isPlayer) CrashAudio.Play(e.position == 1 ? CrashAudio.Stinger.Win : CrashAudio.Stinger.Finish);
            // Everyone else finishes when they next cross the line (handled in CompleteLap via phase == Finished).
            bool all = true;
            foreach (var o in entrants) if (!o.finished && !o.eliminated && !o.dnf) all = false;
            if (all) finishedAt = Mathf.Min(finishedAt, Time.time - 15f);
        }

        void PlayerLapRecorded(float lapTime)
        {
            if (config.mode == GameMode.TimeTrial || config.mode == GameMode.Practice)
            {
                bool pb = SaveData.SubmitLap(SaveData.D.timeTrial, map.id, config.car, lapTime);
                if (pb && config.mode == GameMode.TimeTrial)
                {
                    newPersonalBest = true;
                    Message("NEW PERSONAL BEST  " + SaveData.FormatTime(lapTime), VUI.Purple, 4f);
                    CrashAudio.Play(CrashAudio.Stinger.PersonalBest);
                    if (recording != null) { recording.time = lapTime; SaveData.SaveGhost(map.id, recording); bestGhost = recording; if (ghostCar != null) Destroy(ghostCar.gameObject); ghostCar = GhostCar.Create(bestGhost); }
                }
            }
            else SaveData.SubmitLap(SaveData.D.raceLaps, map.id, config.car, lapTime);
            StartRecording();
        }

        void TrackLimits(Entrant e)
        {
            int i = e.hint;
            float edge = track.halfWidth[i] + 1.4f; // the kerb counts as track, beyond it doesn't
            bool off = Mathf.Abs(e.lateral) > edge + 0.9f; // car centre ~1 m beyond = all four wheels off
            if (off) e.offTrackT += Time.deltaTime; else e.offTrackT = 0f;
            if (e.offTrackT > 0.35f && !e.lapInvalid)
            {
                e.lapInvalid = true;
                if (IsRace)
                {
                    e.warnings++;
                    if (e.isPlayer) Message(e.warnings >= 3 ? "TRACK LIMITS - 5 SECOND PENALTY" : "TRACK LIMITS WARNING " + e.warnings + "/3", VUI.Warn, 3f);
                    if (e.isPlayer) CrashAudio.Play(e.warnings >= 3 ? CrashAudio.Stinger.Penalty : CrashAudio.Stinger.Warning);
                    if (e.warnings >= 3) { e.penalty += 5f; e.warnings = 0; }
                }
                else if (e.isPlayer) Message("TRACK LIMITS - LAP INVALID", VUI.Accent, 2.5f);
            }
        }

        void UpdateSafe(Entrant e)
        {
            // Remember the last place the car was healthy on the tarmac, to put it back there after a crash.
            var c = e.car;
            if (c.Body == null || c.Body.isKinematic) return;
            bool onTrack = Mathf.Abs(e.lateral) < track.halfWidth[e.hint] && c.Upright > 0.9f && c.SpeedMs > 5f;
            e.safeTimer = onTrack ? e.safeTimer + Time.deltaTime : 0f;
            if (e.safeTimer > 1f)
            {
                e.lastSafePos = track.pos[e.hint] + track.up[e.hint] * 0.3f;
                e.lastSafeRot = Quaternion.LookRotation(track.fwd[e.hint], track.up[e.hint]);
                e.lastValidAlong = e.along;
            }
        }

        void Wreck(Entrant e)
        {
            var c = e.car;
            bool stuck = c.damage != null && c.damage.Wrecked || (c.Upright < 0.1f && c.SpeedMs < 2f);
            if (stuck) { if (e.wreckedSince < 0f) e.wreckedSince = Time.time; }
            else e.wreckedSince = -1f;
            // AI cars: back on track after a pause (or retire if the race allows damage to end it).
            if (!e.isPlayer && e.wreckedSince > 0f && Time.time - e.wreckedSince > 5f)
            {
                if (c.damage != null && c.damage.Exploded && IsRace) { e.dnf = true; c.Freeze(true); }
                else Recover(c);
            }
        }

        // ------------------------------------------------------------------ order, gaps, flags

        int lastPlayerPos;
        float nextPosSound;

        void Order()
        {
            float L = track.Length;
            entrants.Sort((a, b) =>
            {
                if (a.eliminated != b.eliminated) return a.eliminated ? 1 : -1;
                if (a.dnf != b.dnf) return a.dnf ? 1 : -1;
                if (a.finished && b.finished) return a.RaceTime.CompareTo(b.RaceTime);
                if (a.finished != b.finished && phase == Phase.Finished) return a.finished ? -1 : 1;
                int ca = a.lap * checkpoints + a.nextCheckpoint, cb = b.lap * checkpoints + b.nextCheckpoint;
                if (ca != cb) return cb.CompareTo(ca);
                float pa = a.Progress(L), pb = b.Progress(L);
                return pb.CompareTo(pa);
            });
            var leader = entrants.Count > 0 ? entrants[0] : null;
            for (int i = 0; i < entrants.Count; i++)
            {
                var e = entrants[i];
                e.position = i + 1;
                if (e.isPlayer && IsRace && phase == Phase.Racing && lastPlayerPos > 0 && e.position != lastPlayerPos && Time.time > nextPosSound)
                {
                    CrashAudio.Play(e.position < lastPlayerPos ? CrashAudio.Stinger.PositionUp : CrashAudio.Stinger.PositionDown);
                    nextPosSound = Time.time + 1.5f;
                }
                if (e.isPlayer) lastPlayerPos = e.position;
                if (leader == null || e == leader) { e.gapToLeader = 0f; continue; }
                int n = Mathf.Min(e.cpTimes.Count, leader.cpTimes.Count);
                e.gapToLeader = n > 0 ? e.cpTimes[n - 1] - leader.cpTimes[n - 1] : 0f;
                if (leader.cpTimes.Count - e.cpTimes.Count > checkpoints) e.gapToLeader = -(leader.cpTimes.Count - e.cpTimes.Count) / checkpoints; // laps down (negative = laps)
            }
        }

        void Flags()
        {
            yellowZones.Clear();
            foreach (var e in entrants)
            {
                if (e.car == null || e.finished) continue;
                bool trouble = e.wreckedSince > 0f || (e.car.SpeedMs < 6f && phase == Phase.Racing && Time.time - raceStartTime > 8f);
                if (trouble) yellowZones.Add(e.along);
            }
        }

        public bool IsYellow(float along)
        {
            foreach (var z in yellowZones)
            {
                float d = track.Ahead(along, z);
                if (d < 220f) return true;
            }
            return false;
        }

        public bool PlayerYellow => player != null && IsYellow(player.along);

        void Eliminate()
        {
            Entrant last = null;
            for (int i = entrants.Count - 1; i >= 0; i--)
                if (!entrants[i].eliminated && !entrants[i].finished && !entrants[i].dnf) { last = entrants[i]; break; }
            if (last == null) return;
            int alive = 0;
            foreach (var e in entrants) if (!e.eliminated && !e.dnf) alive++;
            if (alive <= 1) return;
            last.eliminated = true;
            eliminationsDone++;
            last.finishTime = Time.time - raceStartTime;
            Message(last.name.ToUpperInvariant() + " ELIMINATED", VUI.Accent, 3.5f);
            if (last.ai != null) { last.ai.coolDown = true; }
            if (last.car != null) { last.car.Freeze(true); last.car.gameObject.SetActive(false); }
            if (last.isPlayer) { phase = Phase.Finished; finishedAt = Time.time - 18f; }
            if (alive - 1 <= 1)
            {
                foreach (var e in entrants) if (!e.eliminated && !e.dnf) { Finish(e); }
                phase = Phase.Finished; finishedAt = Time.time - 15f;
            }
        }

        // ------------------------------------------------------------------ recovery

        /// <summary>Puts a car back on the track at its last safe spot, repaired enough to carry on.</summary>
        public void Recover(Car c)
        {
            Entrant e = entrants.Find(x => x.car == c);
            Vector3 pos; Quaternion rot;
            if (e != null && e.lastSafePos != Vector3.zero) { pos = e.lastSafePos; rot = e.lastSafeRot; }
            else
            {
                int h = -1;
                track.Project(c.transform.position, ref h, out _, out _);
                pos = track.pos[h] + Vector3.up * 0.3f;
                rot = Quaternion.LookRotation(track.fwd[h]);
            }
            // Don't drop it on top of another car.
            foreach (var o in entrants)
                if (o.car != c && o.car != null && (o.car.transform.position - pos).sqrMagnitude < 25f) pos += rot * Vector3.right * 4f;
            if (c.damage != null && (c.damage.Wrecked || Settings.Damage != DamageMode.Full || !IsRace)) c.damage.Repair();
            c.Teleport(pos, rot);
            c.ResetState();
            if (e != null) { e.wreckedSince = -1f; }
        }

        // ------------------------------------------------------------------ messages, results

        public void Message(string text, Color c, float seconds)
        {
            bigMessage = text;
            bigMessageUntil = Time.time + seconds;
            OnMessage?.Invoke(text, c);
        }

        public void ShowResults()
        {
            if (phase == Phase.Results) return;
            phase = Phase.Results;
            Order();
            foreach (var e in entrants) if (e.ai != null) e.ai.coolDown = true;
            if (player != null && IsRace)
            {
                SaveData.D.races++;
                if (player.position == 1 && !player.eliminated) SaveData.D.wins++;
                if (player.position <= 3 && !player.eliminated) SaveData.D.podiums++;
                if (config.mode == GameMode.Championship) Championship.AddResults(this);
                SaveData.Write();
            }
            OnResults?.Invoke();
        }

        public static readonly int[] Points = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };

        // ------------------------------------------------------------------ ghost

        void StartRecording()
        {
            if (config.mode != GameMode.TimeTrial || player == null) return;
            recording = new SaveData.Ghost { car = config.car, interval = 0.05f };
            recordT = 0f;
            if (ghostCar != null) ghostCar.Restart();
        }

        void Ghost()
        {
            if (recording == null || player == null || player.car == null) return;
            recordT -= Time.deltaTime;
            if (recordT <= 0f)
            {
                recordT += recording.interval;
                recording.pos.Add(player.car.transform.position);
                recording.rot.Add(player.car.transform.rotation);
                if (recording.pos.Count > 20000) recording = null; // ten-plus minute lap: stop recording
            }
        }
    }

    /// <summary>A translucent replay of your best time-trial lap.</summary>
    public class GhostCar : MonoBehaviour
    {
        SaveData.Ghost data;
        float t;

        public static GhostCar Create(SaveData.Ghost g)
        {
            var spec = CarCatalog.Get(g.car);
            var go = new GameObject("Ghost");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var car = go.AddComponent<Car>();
            car.Init(spec, new CarSetup());
            CarModel.Build(car, false);
            // Ghost look: no colliders, see-through.
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var shader = Shader.Find("Veylix/Ghost") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = new Color(0.5f, 0.8f, 1f, 0.25f) };
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Destroy(car);
            var gc = go.AddComponent<GhostCar>();
            gc.data = g;
            Layers.SetRecursive(go, Layers.Scenery);
            return gc;
        }

        public void Restart() { t = 0f; }

        void Update()
        {
            if (data == null || data.pos.Count < 2) return;
            t += Time.deltaTime;
            float f = t / data.interval;
            int i = Mathf.FloorToInt(f);
            if (i >= data.pos.Count - 1) { gameObject.SetActive(true); return; }
            float k = f - i;
            transform.SetPositionAndRotation(Vector3.Lerp(data.pos[i], data.pos[i + 1], k), Quaternion.Slerp(data.rot[i], data.rot[i + 1], k));
        }
    }
}
