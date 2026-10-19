using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>A place fighters (re)appear. Picks the one furthest from everybody else.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        static readonly List<SpawnPoint> Points = new List<SpawnPoint>();

        void OnEnable() { Points.Add(this); }
        void OnDisable() { Points.Remove(this); }

        public static Vector3 Pick(Vector3 fallback)
        {
            SpawnPoint best = null;
            float bestScore = -1f;
            foreach (var p in Points)
            {
                float nearest = 999f;
                foreach (var f in Fighter.All)
                    if (f.IsReady && !f.IsMine) nearest = Mathf.Min(nearest, Vector3.Distance(f.transform.position, p.transform.position));
                float score = nearest + Random.value * 3f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best != null ? best.transform.position : fallback;
        }
    }
}
