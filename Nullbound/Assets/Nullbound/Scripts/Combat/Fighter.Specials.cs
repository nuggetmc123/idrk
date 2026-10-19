using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Every fighter's special (X), status effects (frozen, burning, barrier, armour) and coins for landing hits.
    /// Like punches, specials are worked out on the owner's game; everyone else just sees the effects
    /// (<see cref="RPC_SpecialFx"/>) and takes the hits through <see cref="RPC_Hit"/>.
    /// </summary>
    public partial class Fighter
    {
        float slamRadius = 5.5f, slamDamage = 18f;
        float barrierUntil, armorUntil, frozenUntil, burnUntil, nextBurnTick;
        Fighter burnBy;
        float whirlUntil, nextWhirlTick, uppercutAt = -1f, beamAt = -1f;

        // ================================================================== status effects (owner)

        void UpdateStatus()
        {
            float now = Time.time;
            if (now < burnUntil && now >= nextBurnTick && !Down)
            {
                nextBurnTick = now + 0.5f;
                Health -= 2f;
                RPC_Fx((byte)FxKind.Hit, Center, 2f, PlayerRef.None);
                if (Health <= 0f) KnockOut(burnBy);
            }
            byte st = 0;
            if (now < frozenUntil) st |= Frozen;
            if (now < burnUntil) st |= Burning;
            if (now < barrierUntil) st |= Shielded;
            if (now < armorUntil) st |= Armored;
            if (!IsBot && ModPowers.InvincibleNow) st |= Immune;
            Status = st;

            if (whirlUntil > now && now >= nextWhirlTick)
            {
                nextWhirlTick = now + 0.2f;
                AreaHit(transform.position, 2.8f, 4.5f, 3f, 1.5f, HitFlags.Special);
            }
            if (uppercutAt > 0f && now >= uppercutAt)
            {
                uppercutAt = -1f;
                Vector3 c = transform.position + FacingFlat * 1f + Vector3.up;
                AreaHit(c, 1.9f, 17f, 3f, 15f, HitFlags.Special | HitFlags.Launch | HitFlags.Heavy);
                Fx.Shockwave(c, 1.5f, Stats.accent);
            }
            if (beamAt > 0f && now >= beamAt)
            {
                beamAt = -1f;
                FireBeam();
            }
        }

        // ================================================================== specials

        void UseSpecial()
        {
            nextSpecial = Time.time + Stats.specialCooldown;
            SpecialCount++;
            var s = Stats;
            switch (s.specialType)
            {
                case SpecialType.GroundSlam: StartSlam(); break;
                case SpecialType.TitanQuake: StartSlam(8.5f, 22f); break;
                case SpecialType.FlashStep: FlashStep(); break;
                case SpecialType.ShadowStrike: ShadowStrike(); break;
                case SpecialType.Fireball:
                    Launch(SpecialType.Fireball, 22f, new HitData { damage = 14f * s.power, flags = HitFlags.Special | HitFlags.Burn | HitFlags.Heavy }, 2f);
                    break;
                case SpecialType.IceShard:
                    Launch(SpecialType.IceShard, 30f, new HitData { damage = 10f * s.power, flags = HitFlags.Special | HitFlags.Freeze }, 0f);
                    break;
                case SpecialType.Lightning: StartCoroutine(Strike(SpecialType.Lightning, 0.35f, 2.4f, 18f)); break;
                case SpecialType.Meteor: StartCoroutine(Strike(SpecialType.Meteor, 1f, 4f, 24f)); break;
                case SpecialType.Shockwave: Shockwave(); break;
                case SpecialType.Telekinesis: StartCoroutine(Telekinesis()); break;
                case SpecialType.Clones: Clones(); break;
                case SpecialType.Uppercut:
                    dashDir = FacingFlat;
                    dashUntil = Time.time + 0.2f;
                    uppercutAt = Time.time + 0.2f;
                    AudioManager.PlayAt(AudioManager.Sfx.Whoosh, transform.position);
                    Announce("UPPERCUT!");
                    break;
                case SpecialType.Whirlwind:
                    whirlUntil = Time.time + 1.4f;
                    nextWhirlTick = Time.time;
                    SpecialFxLocal(SpecialType.Whirlwind, transform.position, Vector3.zero);
                    RPC_SpecialFx((byte)SpecialType.Whirlwind, transform.position, Vector3.zero, Me);
                    Announce("WHIRLWIND!");
                    break;
                case SpecialType.Beam:
                    beamAt = Time.time + 0.35f;
                    AudioManager.PlayAt(AudioManager.Sfx.Zap, HeadPos, 0.7f, 0.6f);
                    Announce("CHARGING...");
                    break;
                case SpecialType.Heal:
                    Health = Mathf.Min(s.maxHealth, Health + s.maxHealth * 0.35f);
                    armorUntil = Time.time + 3f;
                    SpecialFxLocal(SpecialType.Heal, transform.position, Vector3.zero);
                    RPC_SpecialFx((byte)SpecialType.Heal, transform.position, Vector3.zero, Me);
                    Announce("HEALED!");
                    break;
                case SpecialType.Barrier:
                    barrierUntil = Time.time + 2.5f;
                    SpecialFxLocal(SpecialType.Barrier, transform.position, Vector3.zero);
                    RPC_SpecialFx((byte)SpecialType.Barrier, transform.position, Vector3.zero, Me);
                    Announce("BARRIER!");
                    break;
            }
        }

        PlayerRef Me => Runner != null ? Runner.LocalPlayer : PlayerRef.None;

        void Announce(string text)
        {
            if (!IsBot) ScreenFx.Message(text, "", 0.8f);
        }

        /// <summary>The enemy closest to where you're looking (within range and a cone), or null.</summary>
        Fighter AimTarget(float range, float cone)
        {
            Fighter best = null;
            float bestAngle = cone;
            foreach (var f in All)
            {
                if (f == this || !f.IsReady || f.Down || f.Ghost) continue;
                if ((f.transform.position - transform.position).magnitude > range) continue;
                float angle = Vector3.Angle(HeadRot * Vector3.forward, f.Center - HeadPos);
                if (angle < bestAngle) { bestAngle = angle; best = f; }
            }
            return best;
        }

        /// <summary>Where you're looking on the ground, up to range away.</summary>
        Vector3 AimPoint(float range)
        {
            Vector3 dir = HeadRot * Vector3.forward;
            if (Physics.Raycast(HeadPos, dir, out var hit, range, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            Vector3 p = HeadPos + dir * range;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var down, 30f, ~0, QueryTriggerInteraction.Ignore)) return down.point;
            return new Vector3(p.x, transform.position.y, p.z);
        }

        /// <summary>Hit everything within a radius: damage falls off toward the edge, knocked outward and up.</summary>
        void AreaHit(Vector3 c, float radius, float damage, float outward, float up, HitFlags flags)
        {
            foreach (var target in Hittables(c, radius))
            {
                Vector3 to = target.Center - c;
                float d = new Vector3(to.x, 0f, to.z).magnitude;
                float falloff = 1f - Mathf.Clamp01(d / radius) * 0.5f;
                Vector3 dir = Flat(to);
                if (dir == Vector3.zero) dir = FacingFlat;
                target.TakeHit(this, new HitData { damage = damage * Stats.power * falloff, knockback = (dir * outward + Vector3.up * up) * falloff, flags = flags, point = target.Center });
            }
        }

        void QuakeFx(Vector3 c, float radius)
        {
            AudioManager.PlayAt(AudioManager.Sfx.Slam, c, 1f, 0.75f);
            Fx.Shockwave(c, radius, Stats.accent);
            Fx.Shockwave(c, radius * 0.6f, Stats.main);
        }

        void ShadowStrike()
        {
            var target = AimTarget(14f, 40f);
            Vector3 from = transform.position;
            if (target == null) { FlashStep(); Empowered = false; return; }
            Vector3 behind = target.transform.position - target.FacingFlat * 1.1f;
            MoveTo((Clear(behind) ? behind : target.transform.position + target.FacingFlat * 1.1f) + Vector3.up * 0.05f);
            if (poseRoot != null && poseHead != null)
                poseRoot.RotateAround(poseHead.position, Vector3.up, Vector3.SignedAngle(FacingFlat, Flat(target.transform.position - transform.position), Vector3.up));
            target.TakeHit(this, new HitData { damage = 16f * Stats.power, knockback = Flat(target.transform.position - transform.position) * 8f + Vector3.up * 3f, flags = HitFlags.Special | HitFlags.Heavy, point = target.Center });
            Fx.Afterimage(from, from + Vector3.up * 1.6f, new Color(0.15f, 0.05f, 0.25f));
            SpecialFxLocal(SpecialType.ShadowStrike, from, transform.position);
            RPC_SpecialFx((byte)SpecialType.ShadowStrike, from, transform.position, Me);
            Announce("SHADOW STRIKE!");
        }

        void Launch(SpecialType type, float speed, HitData hit, float blastRadius)
        {
            Vector3 dir = HeadRot * Vector3.forward;
            var target = AimTarget(25f, 12f);
            if (target != null) dir = (target.Center - HeadPos).normalized; // a little aim help
            Vector3 start = HeadPos + dir * 0.5f + Vector3.down * 0.2f;
            Projectile.Fire(this, type, start, dir * speed, hit, blastRadius, Stats.accent);
            RPC_SpecialFx((byte)type, start, dir * speed, Me);
            AudioManager.PlayAt(type == SpecialType.Fireball ? AudioManager.Sfx.Whoosh : AudioManager.Sfx.Zap, start);
        }

        IEnumerator Strike(SpecialType type, float delay, float radius, float damage)
        {
            var target = AimTarget(20f, 30f);
            Vector3 p = target != null ? target.transform.position : AimPoint(type == SpecialType.Meteor ? 20f : 14f);
            SpecialFxLocal(type, p, Vector3.zero);
            RPC_SpecialFx((byte)type, p, Vector3.zero, Me);
            Announce(type == SpecialType.Meteor ? "METEOR!" : "LIGHTNING!");
            yield return new WaitForSeconds(delay);
            if (!IsReady) yield break;
            AreaHit(p + Vector3.up * 0.5f, radius, damage, type == SpecialType.Meteor ? 9f : 4f, type == SpecialType.Meteor ? 9f : 6f,
                HitFlags.Special | HitFlags.Launch | (type == SpecialType.Meteor ? HitFlags.Heavy | HitFlags.Burn : HitFlags.None));
            if (type == SpecialType.Meteor) foreach (var b in Breakable.All) if (!b.Broken && Vector3.Distance(b.Center, p) < radius) b.Smash(b.Center - p);
        }

        void Shockwave()
        {
            Vector3 fwd = FacingFlat;
            foreach (var target in Hittables(transform.position + fwd * 3.5f, 4f))
            {
                Vector3 to = target.Center - transform.position;
                if (Vector3.Angle(fwd, Flat(to)) > 60f) continue;
                target.TakeHit(this, new HitData { damage = 12f * Stats.power, knockback = Flat(to) * 14f + Vector3.up * 4f, flags = HitFlags.Special | HitFlags.Launch, point = target.Center });
            }
            SpecialFxLocal(SpecialType.Shockwave, transform.position + Vector3.up, fwd);
            RPC_SpecialFx((byte)SpecialType.Shockwave, transform.position + Vector3.up, fwd, Me);
            Announce("SHOCKWAVE!");
        }

        IEnumerator Telekinesis()
        {
            var target = AimTarget(15f, 30f);
            if (target == null) { Announce("NO TARGET"); nextSpecial = Time.time + 1f; yield break; }
            Announce("TELEKINESIS!");
            target.TakeHit(this, new HitData { damage = 6f * Stats.power, knockback = Vector3.up * 13f, flags = HitFlags.Special | HitFlags.Launch, point = target.Center });
            SpecialFxLocal(SpecialType.Telekinesis, HeadPos, target.Center);
            RPC_SpecialFx((byte)SpecialType.Telekinesis, HeadPos, target.Center, Me);
            yield return new WaitForSeconds(0.75f);
            if (!IsReady || target == null || !target.IsReady) yield break;
            target.TakeHit(this, new HitData { damage = 14f * Stats.power, knockback = Vector3.down * 22f + Flat(target.transform.position - transform.position) * 3f, flags = HitFlags.Special | HitFlags.Heavy, point = target.Center });
        }

        void Clones()
        {
            var targets = new List<Fighter>();
            foreach (var f in All)
                if (f != this && f.IsReady && !f.Down && !f.Ghost && Vector3.Distance(f.transform.position, transform.position) < 14f) targets.Add(f);
            targets.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
            for (int i = 0; i < 2; i++)
            {
                var t = targets.Count > 0 ? targets[i % targets.Count] : null;
                Vector3 side = Vector3.Cross(Vector3.up, FacingFlat) * (i == 0 ? -0.8f : 0.8f);
                Clone.Send(this, transform.position + side, t, Stats.main, true);
                RPC_SpecialFx((byte)SpecialType.Clones, transform.position + side, t != null ? t.transform.position : transform.position + FacingFlat * 8f, Me);
            }
            AudioManager.PlayAt(AudioManager.Sfx.Zap, transform.position, 0.8f, 0.7f);
            Announce("CLONES!");
        }

        void FireBeam()
        {
            Vector3 dir = HeadRot * Vector3.forward;
            var aim = AimTarget(20f, 10f);
            if (aim != null) dir = (aim.Center - HeadPos).normalized;
            Vector3 start = HeadPos + Vector3.down * 0.3f;
            float length = 20f;
            if (Physics.Raycast(start, dir, out var wall, length, ~0, QueryTriggerInteraction.Ignore)) length = wall.distance;
            var done = new HashSet<IHittable>();
            foreach (var h in Physics.SphereCastAll(start, 0.5f, dir, length, ~0, QueryTriggerInteraction.Collide))
            {
                var hb = h.collider.GetComponent<Hurtbox>();
                var t = hb != null ? hb.Target : null;
                if (t == null || ReferenceEquals(t, this) || !t.CanBeHitBy(this) || !done.Add(t)) continue;
                t.TakeHit(this, new HitData { damage = 20f * Stats.power, knockback = dir * 10f + Vector3.up * 3f, flags = HitFlags.Special | HitFlags.Heavy | HitFlags.Launch, point = t.Center });
            }
            SpecialFxLocal(SpecialType.Beam, start, start + dir * length);
            RPC_SpecialFx((byte)SpecialType.Beam, start, start + dir * length, Me);
            Announce("BEAM!");
        }

        // ================================================================== what everyone sees

        /// <summary>Show a special's effect on everyone's game (the one who used it already did).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_SpecialFx(byte type, Vector3 a, Vector3 b, PlayerRef skip)
        {
            if (Runner != null && skip == Runner.LocalPlayer && skip != PlayerRef.None) return;
            var t = (SpecialType)type;
            if (t == SpecialType.GroundSlam) { SlamFx(a); return; }
            if (t == SpecialType.TitanQuake) { QuakeFx(a, 8.5f); return; }
            if (t == SpecialType.Fireball || t == SpecialType.IceShard) { Projectile.Fire(null, t, a, b, default, 0f, Stats.accent); return; }
            if (t == SpecialType.Clones) { Clone.SendVisual(a, b, Stats.main); return; }
            SpecialFxLocal(t, a, b);
        }

        void SpecialFxLocal(SpecialType t, Vector3 a, Vector3 b)
        {
            var s = Stats;
            switch (t)
            {
                case SpecialType.Lightning:
                    Fx.Telegraph(a, 2.4f, new Color(0.5f, 0.9f, 1f), 0.35f);
                    Fx.Delayed(0.35f, () => { Fx.Streak(a + Vector3.up * 14f, a, new Color(0.7f, 0.95f, 1f)); Fx.Shockwave(a, 2.4f, new Color(0.6f, 0.9f, 1f)); AudioManager.PlayAt(AudioManager.Sfx.Zap, a, 1f, 0.7f); });
                    break;
                case SpecialType.Meteor:
                    Fx.Telegraph(a, 4f, new Color(1f, 0.4f, 0.1f), 1f);
                    Fx.Falling(a + Vector3.up * 18f + Vector3.right * 4f, a, 1f, s.accent, () => { Fx.Shockwave(a, 4f, new Color(1f, 0.45f, 0.1f)); AudioManager.PlayAt(AudioManager.Sfx.Slam, a); });
                    break;
                case SpecialType.Shockwave:
                    Fx.Cone(a, b, 7f, s.accent);
                    AudioManager.PlayAt(AudioManager.Sfx.Slam, a, 0.6f, 1.4f);
                    break;
                case SpecialType.Telekinesis:
                    Fx.Streak(a, b, s.accent);
                    Fx.Shockwave(b + Vector3.down * 1.2f, 1.2f, s.accent);
                    AudioManager.PlayAt(AudioManager.Sfx.Zap, b, 0.7f, 0.5f);
                    break;
                case SpecialType.ShadowStrike:
                    Fx.Streak(a + Vector3.up, b + Vector3.up, new Color(0.5f, 0.2f, 0.8f));
                    AudioManager.PlayAt(AudioManager.Sfx.HeavyPunch, b);
                    break;
                case SpecialType.Whirlwind:
                    Fx.Spin(transform, 1.4f, s.accent);
                    AudioManager.PlayAt(AudioManager.Sfx.Whoosh, a, 1f, 0.6f);
                    break;
                case SpecialType.Beam:
                    Fx.Beam(a, b, s.accent);
                    AudioManager.PlayAt(AudioManager.Sfx.Zap, a, 1f, 0.4f);
                    break;
                case SpecialType.Heal:
                    Fx.Rings(transform, new Color(0.4f, 1f, 0.5f), 1.2f);
                    AudioManager.PlayAt(AudioManager.Sfx.Respawn, a);
                    break;
                case SpecialType.Barrier:
                    Fx.Bubble(transform, new Color(0.45f, 0.75f, 1f), 2.5f);
                    AudioManager.PlayAt(AudioManager.Sfx.Block, a, 1f, 0.6f);
                    break;
            }
        }
    }

    /// <summary>A fireball or ice shard. The owner's copy does the damage; everyone else's copy is just for looks.</summary>
    public class Projectile : MonoBehaviour
    {
        Fighter owner;
        SpecialType type;
        Vector3 velocity;
        HitData hit;
        float blast, age;
        Color color;

        public static void Fire(Fighter owner, SpecialType type, Vector3 start, Vector3 velocity, HitData hit, float blast, Color color)
        {
            var go = new GameObject(type.ToString());
            go.transform.position = start;
            var p = go.AddComponent<Projectile>();
            p.owner = owner; p.type = type; p.velocity = velocity; p.hit = hit; p.blast = blast; p.color = color;
            bool fire = type == SpecialType.Fireball;
            NBUI.Part(go.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * (fire ? 0.35f : 0.18f), NBUI.Glow(fire ? new Color(1f, 0.5f, 0.1f) : new Color(0.6f, 0.9f, 1f)));
            NBUI.Part(go.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * (fire ? 0.55f : 0.28f), NBUI.Glow((fire ? new Color(1f, 0.25f, 0.05f) : new Color(0.3f, 0.6f, 1f)) * 0.6f));
            if (!fire) go.transform.GetChild(0).localScale = new Vector3(0.12f, 0.12f, 0.45f);
            go.transform.rotation = Quaternion.LookRotation(velocity);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            Vector3 step = velocity * dt;
            Vector3 pos = transform.position;
            if (age > 2.5f) { Explode(pos); return; }
            // Hit something solid, or someone.
            foreach (var h in Physics.SphereCastAll(pos, 0.25f, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Collide))
            {
                var hb = h.collider.GetComponent<Hurtbox>();
                var target = hb != null ? hb.Target : null;
                if (target != null)
                {
                    if (owner != null && (ReferenceEquals(target, owner) || !target.CanBeHitBy(owner))) continue;
                    if (owner != null && blast <= 0f)
                    {
                        var data = hit;
                        data.knockback = velocity.normalized * 6f + Vector3.up * 2.5f;
                        data.point = h.point;
                        target.TakeHit(owner, data);
                    }
                    Explode(h.point);
                    return;
                }
                if (h.collider.isTrigger || (owner != null && h.collider.transform.IsChildOf(owner.transform))) continue;
                Explode(h.point);
                return;
            }
            transform.position = pos + step;
        }

        void Explode(Vector3 at)
        {
            if (owner != null && blast > 0f && owner.IsReady)
            {
                foreach (var col in Physics.OverlapSphere(at, blast, ~0, QueryTriggerInteraction.Collide))
                {
                    var hb = col.GetComponent<Hurtbox>();
                    var t = hb != null ? hb.Target : null;
                    if (t == null || ReferenceEquals(t, owner) || !t.CanBeHitBy(owner)) continue;
                    var data = hit;
                    Vector3 away = t.Center - at;
                    away.y = 0f;
                    data.knockback = away.normalized * 7f + Vector3.up * 4f;
                    data.point = t.Center;
                    t.TakeHit(owner, data);
                }
            }
            bool fire = type == SpecialType.Fireball;
            Fx.Impact(at, -velocity, fire ? new Color(1f, 0.45f, 0.1f) : new Color(0.6f, 0.9f, 1f), fire, false);
            if (fire) Fx.Shockwave(at, Mathf.Max(1f, blast), new Color(1f, 0.4f, 0.1f));
            AudioManager.PlayAt(fire ? AudioManager.Sfx.HeavyPunch : AudioManager.Sfx.Block, at);
            Destroy(gameObject);
        }
    }

    /// <summary>A shadow clone that rushes an enemy and explodes. Damage only from the owner's copy.</summary>
    public class Clone : MonoBehaviour
    {
        Fighter owner, target;
        Vector3 destination;
        float age;

        public static void Send(Fighter owner, Vector3 start, Fighter target, Color color, bool real)
        {
            var c = Make(start, color);
            c.owner = real ? owner : null;
            c.target = target;
            c.destination = target != null ? target.transform.position : start + owner.FacingFlat * 8f;
        }

        public static void SendVisual(Vector3 start, Vector3 destination, Color color)
        {
            var c = Make(start, color);
            c.destination = destination;
        }

        static Clone Make(Vector3 start, Color color)
        {
            var go = new GameObject("ShadowClone");
            go.transform.position = start;
            var glow = NBUI.Glow(color * 0.5f);
            NBUI.Part(go.transform, PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.45f, 0.6f, 0.3f), glow);
            NBUI.Part(go.transform, PrimitiveType.Sphere, new Vector3(0f, 1.65f, 0f), Vector3.one * 0.3f, glow);
            return go.AddComponent<Clone>();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (target != null && target.IsReady && !target.Down) destination = target.transform.position;
            Vector3 to = destination - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.9f || age > 1.6f) { Boom(); return; }
            transform.position += to.normalized * 12f * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(to);
        }

        void Boom()
        {
            Vector3 c = transform.position + Vector3.up;
            Fx.Shockwave(transform.position, 1.8f, new Color(0.5f, 0.3f, 0.8f));
            AudioManager.PlayAt(AudioManager.Sfx.HeavyPunch, c);
            if (owner != null && owner.IsReady)
                foreach (var col in Physics.OverlapSphere(c, 1.9f, ~0, QueryTriggerInteraction.Collide))
                {
                    var hb = col.GetComponent<Hurtbox>();
                    var t = hb != null ? hb.Target : null;
                    if (t == null || ReferenceEquals(t, owner) || !t.CanBeHitBy(owner)) continue;
                    Vector3 away = t.Center - c;
                    away.y = 0f;
                    t.TakeHit(owner, new HitData { damage = 11f * owner.Stats.power, knockback = away.normalized * 8f + Vector3.up * 6f, flags = HitFlags.Special | HitFlags.Launch, point = t.Center });
                }
            Destroy(gameObject);
        }
    }
}
