using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>Spawns and coordinates raid NPC squads; relays alerts (squad mates hear each other, vault alarms).</summary>
    public class NpcDirector : MonoBehaviour
    {
        public static NpcDirector I;
        public readonly List<Npc> Npcs = new List<Npc>();
        readonly Dictionary<int, Npc> _byId = new Dictionary<int, Npc>();
        public int Kills;

        public static NpcDirector Create(Transform parent)
        {
            var go = new GameObject("NpcDirector");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<NpcDirector>();
            return I;
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>Spawn squads at the given points. Deterministic so every client builds the same NPC set.</summary>
        public const int MaxNpcs = 16;   // raiders per raid (spread over the whole map)

        public void SpawnSquads(List<MapBase.NpcSpawn> spawns, int seed, bool puppets, List<Pose> avoid = null, float safeRadius = 110f, int maxNpcs = MaxNpcs)
        {
            float SafeRadius = safeRadius;   // no raiders this close to a spawn spot
            // bosses live in a fixed lair, so they only have to be clear of a player's own spawn spot (30 m), not the big
            // safe radius for ordinary raiders - that radius covered every lair, so bosses never turned up at all
            var bosses = spawns.FindAll(s => s.boss != null || s.guard);
            if (avoid != null) bosses = bosses.FindAll(s => { foreach (var a in avoid) { var d = a.position - s.pos; if (Mathf.Abs(d.y) > 3f) continue; d.y = 0; if (d.sqrMagnitude < 30f * 30f) return false; } return true; });
            if (avoid != null) spawns = spawns.FindAll(s => { foreach (var a in avoid) { var d = a.position - s.pos; if (Mathf.Abs(d.y) > 3f) continue; d.y = 0; if (d.sqrMagnitude < SafeRadius * SafeRadius) return false; } return true; });
            spawns = spawns.FindAll(s => s.boss == null && !s.guard);
            var rng = new Rng(seed * 13 + 5);
            int id = 0;
            // the maps are huge: spread a limited number of raiders over randomly picked squad spots, in smaller groups
            var order = new List<MapBase.NpcSpawn>(spawns);
            for (int k = order.Count - 1; k > 0; k--) { int j = rng.Range(0, k + 1); var t = order[k]; order[k] = order[j]; order[j] = t; }
            int total = 0;
            foreach (var s in order)
            {
                if (total >= maxNpcs) break;
                int count = Mathf.Min(maxNpcs - total, Mathf.Max(1, s.count - 1));
                total += count;
                for (int i = 0; i < count; i++)
                {
                    Vector2 off = rng.InCircle() * 2.5f;
                    SpawnOne(ref id, s.pos + new Vector3(off.x, 0, off.y), rng.Range(0f, 360f), seed, s.heavy || rng.Chance(0.15f), null, puppets);
                }
            }
            // bosses: each map's boss only turns up in some raids, with heavy guards round them
            var brng = new Rng(seed * 31 + 11);
            foreach (var b in bosses)
            {
                if (!brng.Chance(b.chance)) continue;
                if (b.guard)
                {
                    for (int g = 0; g < b.count; g++) { Vector2 go = brng.InCircle() * 1.6f; SpawnOne(ref id, b.pos + new Vector3(go.x, 0, go.y), brng.Range(0f, 360f), seed, true, null, puppets); }
                    continue;
                }
                SpawnOne(ref id, b.pos, brng.Range(0f, 360f), seed, true, b.boss, puppets);
                for (int g = 0; g < b.count; g++)
                {
                    Vector2 off = brng.InCircle() * 2.2f;
                    SpawnOne(ref id, b.pos + new Vector3(off.x, 0, off.y), brng.Range(0f, 360f), seed, true, null, puppets);
                }
            }
        }

        int _testerId = 9000;
        /// <summary>Tester: spawn one raider (or a boss) right now.</summary>
        public void SpawnNow(Vector3 pos, float yaw, bool heavy, string boss)
        {
            int id = _testerId;
            SpawnOne(ref id, pos, yaw, Random.Range(1, 99999), heavy, boss, false);
            _testerId = id;
        }

        void SpawnOne(ref int id, Vector3 pos, float yaw, int seed, bool heavy, string boss, bool puppets)
        {
            var n = Npc.Spawn(id, pos, yaw, seed * 1000 + id, heavy, transform, boss);
            if (puppets) n.MakePuppet();
            Synth.Gunshot(n.Weapon); Synth.GunshotDistant(n.Weapon); // pre-generate so the first shot doesn't hitch
            n.OnDied = OnNpcDied;
            Npcs.Add(n);
            _byId[id] = n;
            id++;
        }

        void OnNpcDied(Npc n)
        {
            Kills++;
            if (Profile.Current != null) Profile.Current.kills++;
            if (n.IsBoss) Levels.Award(Levels.BossXp, "boss killed");
            else Levels.Award(Levels.KillXp, "kill");
        }

        public Npc Get(int id) => _byId.TryGetValue(id, out var n) ? n : null;

        public void Alert(Vector3 from, Vector3 threat, float radius, Npc source)
        {
            foreach (var n in Npcs)
                if (n != source && n.Alive && Vector3.Distance(n.transform.position, from) < radius) n.HearAlert(threat);
        }

        public void AlertAll(Vector3 pos, float radius)
        {
            foreach (var n in Npcs) if (n.Alive && Vector3.Distance(n.transform.position, pos) < radius) n.HearAlert(pos);
        }

        public void BecomeAuthority() { foreach (var n in Npcs) n.BecomeAuthority(); }
    }
}
