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
            if (avoid != null) spawns = spawns.FindAll(s => { foreach (var a in avoid) { var d = a.position - s.pos; if (Mathf.Abs(d.y) > 3f) continue; d.y = 0; if (d.sqrMagnitude < SafeRadius * SafeRadius) return false; } return true; });
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
                    bool heavy = s.heavy || rng.Chance(0.15f);
                    var n = Npc.Spawn(id, s.pos + new Vector3(off.x, 0, off.y), rng.Range(0f, 360f), seed * 1000 + id, heavy, transform);
                    if (puppets) n.MakePuppet();
                    Synth.Gunshot(n.Weapon); Synth.GunshotDistant(n.Weapon); // pre-generate so the first shot doesn't hitch
                    n.OnDied = OnNpcDied;
                    Npcs.Add(n);
                    _byId[id] = n;
                    id++;
                }
            }
        }

        void OnNpcDied(Npc n)
        {
            Kills++;
            if (Profile.Current != null) Profile.Current.kills++;
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
