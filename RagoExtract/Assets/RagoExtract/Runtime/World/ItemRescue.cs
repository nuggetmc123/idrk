using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Safety net for loose loot: anything that ends up under the terrain (dropped by a raider on a slope, spawned a
    /// little low, knocked through a thin edge) or falling with nothing at all beneath it is put back on the ground -
    /// at the last place it was resting, or on top of the terrain. Checks a few dozen items a frame, so it costs nothing.
    /// </summary>
    public class ItemRescue : MonoBehaviour
    {
        public MapBase map;
        readonly Dictionary<Item, Vector3> _safe = new Dictionary<Item, Vector3>();
        readonly Dictionary<Item, float> _void = new Dictionary<Item, float>();
        int _next;
        float _t;

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < 0.1f) return;
            float dt = _t; _t = 0f;
            var all = Item.All;
            if (all.Count == 0) return;
            int mask = Ballistics.WallMask;
            int n = Mathf.Min(all.Count, 40);
            for (int k = 0; k < n; k++)
            {
                if (_next >= all.Count) _next = 0;
                var it = all[_next++];
                if (it == null || it.Held || it.slot != null || it.rb == null || it.rb.isKinematic || !it.gameObject.activeInHierarchy) continue;
                Vector3 p = it.transform.position;
                // under the terrain: straight back up on top of it
                float tt = map != null && map.B != null ? map.B.TerrainTop(p) : float.NegativeInfinity;
                if (tt > p.y + 0.12f && tt < p.y + 25f) { Put(it, new Vector3(p.x, tt + 0.25f, p.z)); continue; }
                // resting on something solid: remember the spot
                if (it.rb.Vel().sqrMagnitude < 0.04f && Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, 0.6f, mask, QueryTriggerInteraction.Ignore))
                { _safe[it] = p + Vector3.up * 0.05f; _void.Remove(it); continue; }
                // falling with nothing at all below it (through the world), or far below where it last rested
                bool nothingBelow = !Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, 120f, mask, QueryTriggerInteraction.Ignore);
                bool hasSafe = _safe.TryGetValue(it, out var sp);
                float v = nothingBelow ? (_void.TryGetValue(it, out var vt) ? vt : 0f) + dt * (all.Count / (float)n) : 0f;
                if (nothingBelow) _void[it] = v; else _void.Remove(it);
                if (v > 0.5f || hasSafe && p.y < sp.y - 20f)
                {
                    if (hasSafe) Put(it, sp);
                    else if (tt > float.NegativeInfinity) Put(it, new Vector3(p.x, tt + 0.25f, p.z));
                    _void.Remove(it);
                }
            }
            if (_safe.Count > 600) { var dead = new List<Item>(); foreach (var kv in _safe) if (kv.Key == null) dead.Add(kv.Key); foreach (var d in dead) _safe.Remove(d); }
        }

        static void Put(Item it, Vector3 at)
        {
            it.rb.SetVel(Vector3.zero); it.rb.angularVelocity = Vector3.zero;
            it.rb.position = at; it.transform.position = at;
            Physics.SyncTransforms();
        }
    }
}
