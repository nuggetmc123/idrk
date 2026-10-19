using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// A wall, pillar or crate that breaks: punch it enough, hit it with a slam, or get launched into it. It bursts
    /// into chunks for everyone (through <see cref="ArenaState"/>) and builds itself back up a while later.
    /// Things can rest on others (<see cref="restsOn"/>): a roof falls when enough of its walls are gone, and a
    /// tower's upper floors come down when the floor under them breaks. Every game works that out the same way
    /// from the walls it saw break, so it needs no extra network messages.
    /// </summary>
    public class Breakable : MonoBehaviour, IHittable
    {
        public static readonly List<Breakable> All = new List<Breakable>();

        public int id;
        public float toughness = 40f;
        public float regrowSeconds = 35f;
        public Material chunkMaterial;
        /// <summary>What holds this up. It collapses when at least <see cref="collapseWhen"/> of them are broken.</summary>
        public Breakable[] restsOn = new Breakable[0];
        public int collapseWhen = 1;

        float damage, brokenAt;
        public bool Broken { get; private set; }
        Collider[] colliders;
        Renderer[] renderers;

        public Vector3 Center => GetComponent<Collider>() != null ? GetComponent<Collider>().bounds.center : transform.position;
        public bool CanBeHitBy(Fighter attacker) => !Broken;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void TakeHit(Fighter attacker, HitData hit)
        {
            if (Broken) return;
            damage += hit.damage * ((hit.flags & HitFlags.Special) != 0 ? 2.5f : 1f);
            Fx.Popup(hit.point, "CRACK", new Color(0.85f, 0.8f, 0.7f), 0.6f);
            if (damage >= toughness) Smash(hit.knockback);
        }

        /// <summary>Break it now (for everyone).</summary>
        public void Smash(Vector3 push)
        {
            if (Broken) return;
            if (ArenaState.Instance != null && ArenaState.Instance.IsReady) ArenaState.Instance.RPC_Break(id, push);
            else BreakLocal(push);
        }

        /// <summary>Everyone's game runs this when it breaks.</summary>
        public void BreakLocal(Vector3 push)
        {
            if (Broken) return;
            Broken = true;
            brokenAt = Time.time;
            var size = GetComponent<Collider>() != null ? GetComponent<Collider>().bounds.size : Vector3.one;
            Fx.Debris(Center, size, chunkMaterial != null ? chunkMaterial : NBUI.Mat(new Color(0.6f, 0.58f, 0.55f), 0.1f), push.normalized * 7f,
                Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * size.z * 3f), 6, 16));
            AudioManager.PlayAt(AudioManager.Sfx.Break, Center);
            foreach (var c in colliders) c.enabled = false;
            foreach (var r in renderers) r.enabled = false;
        }

        void Update()
        {
            if (!Broken && restsOn != null && restsOn.Length > 0)
            {
                int gone = 0;
                foreach (var r in restsOn) if (r != null && r.Broken) gone++;
                if (gone >= Mathf.Max(1, collapseWhen)) BreakLocal(Vector3.down * 3f);
                return;
            }
            if (!Broken || Time.time - brokenAt < regrowSeconds) return;
            // Rebuild from the bottom up: wait until whatever this rests on is back.
            if (restsOn != null) foreach (var r in restsOn) if (r != null && r.Broken) return;
            // Grow back, unless someone is standing in it.
            if (Physics.CheckBox(Center, transform.lossyScale * 0.45f, transform.rotation, ~0, QueryTriggerInteraction.Ignore)) { brokenAt += 2f; return; }
            Broken = false;
            damage = 0f;
            foreach (var c in colliders) c.enabled = true;
            foreach (var r in renderers) r.enabled = true;
        }
    }
}
