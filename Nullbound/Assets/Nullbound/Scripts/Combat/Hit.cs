using UnityEngine;

namespace Nullbound
{
    [System.Flags]
    public enum HitFlags : byte
    {
        None = 0,
        Heavy = 1,       // a hard swing: bigger knockback and a heavier sound
        Launch = 2,      // sends them flying (combo finisher, empowered punch, slam)
        Special = 4,     // from an ability (half damage through a block instead of a fifth)
        Blocked = 8,     // set by the one who got hit
        GuardBreak = 16, // their block broke
        Empowered = 32,  // Speedster's Flash Step punch
    }

    public struct HitData
    {
        public float damage;
        public Vector3 knockback;
        public HitFlags flags;
        public Vector3 point;
    }

    /// <summary>Anything a punch or an ability can hit: fighters, training dummies, breakable walls and crates.</summary>
    public interface IHittable
    {
        /// <summary>Where to aim (chest height).</summary>
        Vector3 Center { get; }
        bool CanBeHitBy(Fighter attacker);
        /// <summary>Called on the attacker's game. Fighters forward it to their owner; props handle it locally.</summary>
        void TakeHit(Fighter attacker, HitData hit);
    }

    /// <summary>A trigger collider that marks part of something hittable (a fighter's head or body, a dummy, a crate).</summary>
    public class Hurtbox : MonoBehaviour
    {
        public MonoBehaviour owner;
        /// <summary>Extra damage for hitting here (heads take more).</summary>
        public float damageScale = 1f;

        public IHittable Target => owner as IHittable;
    }
}
