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

        /// <summary>The middle of the current map (bots wander back here when nobody's around).</summary>
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
