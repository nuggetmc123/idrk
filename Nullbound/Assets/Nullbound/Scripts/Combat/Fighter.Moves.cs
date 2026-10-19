using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Moves every fighter has besides punching and their special:
    /// DASH HITS: dashing (B) into someone rams them.
    /// FLY (X): lock on to the enemy you're looking at and fly straight into them (they take a big hit and get
    /// launched), or with nobody in your sights, fly where you're looking. Giants are too heavy to fly: they charge
    /// along the ground instead.
    /// </summary>
    public partial class Fighter
    {
        public const float FlyCooldown = 4f, FlySpeed = 17f, FlyLockRange = 28f;
        readonly HashSet<Fighter> dashHits = new HashSet<Fighter>();
        float flyUntil, nextFly;
        Vector3 flyDir;
        Fighter flyTarget;

        public float FlyReady => Mathf.Clamp01(1f - (nextFly - Time.time) / FlyCooldown);
        bool FliesLow => Stats.body == BodyType.Giant;

        /// <summary>Anyone you run into mid-dash takes a ram (once per dash each).</summary>
        void DashHits()
        {
            Vector3 dir = new Vector3(dashDir.x, 0f, dashDir.z).normalized;
            foreach (var t in Hittables(Center + dir * 0.45f, 0.75f))
            {
                if (!(t is Fighter f) || f == this || dashHits.Contains(f)) continue;
                dashHits.Add(f);
                f.TakeHit(this, new HitData { damage = 7f * Stats.power, knockback = dir * 9f + Vector3.up * 2.5f, flags = HitFlags.Heavy, point = f.Center });
                AudioManager.PlayAt(AudioManager.Sfx.HeavyPunch, f.Center);
                Fx.Impact(f.Center, -dir, Stats.accent, true, false);
                if (!IsBot) { FightInput.Buzz(true, 0.6f, 0.1f); FightInput.Buzz(false, 0.6f, 0.1f); }
            }
        }

        void StartFly()
        {
            nextFly = Time.time + FlyCooldown;
            flyTarget = AimTarget(FlyLockRange, 25f);
            Vector3 look = HeadRot * Vector3.forward;
            flyDir = flyTarget != null ? (flyTarget.Center - Center).normalized : look.normalized;
            if (FliesLow) flyDir = Flat(flyDir);
            if (flyDir == Vector3.zero) flyDir = FacingFlat;
            // Locked on: as long as it takes to reach them (up to 1.6 s). Free flight: a short burst.
            flyUntil = Time.time + (flyTarget != null ? 1.6f : 0.55f);
            grounded = false;
            AudioManager.PlayAt(AudioManager.Sfx.Whoosh, transform.position + Vector3.up, 1f, 0.8f);
            Fx.Afterimage(transform.position, HeadPos, Stats.main);
            if (!IsBot) ComfortVignette.Kick(1f);
        }

        /// <summary>While flying: steer at the target, ram them on contact. Returns true while flying (no gravity).</summary>
        bool FlyStep(float dt)
        {
            if (Time.time >= flyUntil) return false;
            if (flyTarget != null)
            {
                if (!flyTarget.IsReady || flyTarget.Down) { EndFly(false); return false; }
                Vector3 to = flyTarget.Center - Center;
                if (FliesLow) to.y = 0f;
                if (to.magnitude < 1.1f)
                {
                    var t = flyTarget;
                    t.TakeHit(this, new HitData { damage = 12f * Stats.power, knockback = flyDir * 13f + Vector3.up * 5f, flags = HitFlags.Heavy | HitFlags.Launch, point = t.Center });
                    AudioManager.PlayAt(AudioManager.Sfx.HeavyPunch, t.Center);
                    Fx.Impact(t.Center, -flyDir, Stats.accent, true, false);
                    Fx.Shockwave(t.Center, 2.5f, Stats.accent);
                    if (!IsBot) { FightInput.Buzz(true, 0.9f, 0.15f); FightInput.Buzz(false, 0.9f, 0.15f); }
                    EndFly(true);
                    return false;
                }
                flyDir = Vector3.Slerp(flyDir, to.normalized, Mathf.Clamp01(dt * 8f));
            }
            float speed = FliesLow ? 13f : FlySpeed;
            velocity = flyDir * speed;
            if (FliesLow) velocity.y = Mathf.Min(velocity.y, 0f);
            return !FliesLow;
        }

        void EndFly(bool hit)
        {
            flyUntil = 0f;
            flyTarget = null;
            // Bounce back off them a little after a hit, otherwise just carry on falling.
            velocity = hit ? -flyDir * 3f + Vector3.up * 3f : velocity * 0.4f;
        }
    }
}
