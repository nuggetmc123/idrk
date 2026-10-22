using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>The maps (they all live in one scene, far apart).</summary>
    public enum ArenaMap : byte { TrainingGrounds = 0, Battleground = 1 }

    /// <summary>A place fighters (re)appear on one map. Picks the one furthest from everybody else.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public ArenaMap map;
        static readonly List<SpawnPoint> Points = new List<SpawnPoint>();

        void OnEnable() { Points.Add(this); }
        void OnDisable() { Points.Remove(this); }

        static ArenaMap Current => SessionManager.Instance != null ? SessionManager.Instance.CurrentMap : ArenaMap.TrainingGrounds;

        public static Vector3 Pick(Vector3 fallback)
        {
            SpawnPoint best = null;
            float bestScore = -1f;
            var map = Current;
            foreach (var p in Points)
            {
                if (p.map != map) continue;
                float nearest = 999f;
                foreach (var f in Fighter.All)
                    if (f.IsReady && !f.IsMine) nearest = Mathf.Min(nearest, Vector3.Distance(f.transform.position, p.transform.position));
                float score = Mathf.Min(nearest, 60f) + Random.value * 8f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best != null ? best.transform.position : fallback;
        }

        /// <summary>
        /// Where a bot appears: the spawn point farthest from every real player (and not right on top of another bot),
        /// so bots start out across the map instead of next to you.
        /// </summary>
        public static Vector3 PickForBot(Vector3 fallback)
        {
            SpawnPoint best = null;
            float bestScore = float.MinValue;
            var map = Current;
            foreach (var p in Points)
            {
                if (p.map != map) continue;
                float nearestPlayer = 999f, nearestBot = 999f;
                foreach (var f in Fighter.All)
                {
                    if (!f.IsReady) continue;
                    float d = Vector3.Distance(f.transform.position, p.transform.position);
                    if (f.IsBot) nearestBot = Mathf.Min(nearestBot, d);
                    else nearestPlayer = Mathf.Min(nearestPlayer, d);
                }
                float score = Mathf.Min(nearestPlayer, 200f) + Mathf.Min(nearestBot, 25f) * 0.6f + Random.value * 6f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best != null ? best.transform.position : fallback;
        }

        /// <summary>Somewhere for a roaming bot to walk to: a random spawn point of this map, not too close.</summary>
        public static Vector3 RoamSpot(Vector3 from)
        {
            var map = Current;
            SpawnPoint pick = null;
            int seen = 0;
            foreach (var p in Points)
            {
                if (p.map != map || Vector3.Distance(p.transform.position, from) < 8f) continue;
                // Reservoir sampling: each candidate equally likely, without building a list.
                if (Random.Range(0, ++seen) == 0) pick = p;
            }
            if (pick == null) return from + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 10f;
            Vector2 jitter = Random.insideUnitCircle * 4f;
            return pick.transform.position + new Vector3(jitter.x, 0f, jitter.y);
        }

        /// <summary>The middle of the current map.</summary>
        public static Vector3 MapCenter()
        {
            var map = Current;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var p in Points) if (p.map == map) { sum += p.transform.position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }
    }
}
