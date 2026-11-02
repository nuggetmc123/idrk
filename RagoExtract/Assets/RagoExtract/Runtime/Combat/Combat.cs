using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public enum BodyPart { Head, Thorax, Stomach, Arm, Leg }

    public struct HitInfo
    {
        public float damage;
        public BodyPart part;
        public Vector3 point, dir, normal;
        public int attacker;      // Actor id: 0 = local player, 1000+ = NPC, 1..999 = remote players (net id + 1)
        public string gunId;
        public bool fromNet;
        public float force;
    }

    public interface IDamageable
    {
        bool Alive { get; }
        void TakeHit(HitInfo h, HitZone zone);
        int ActorId { get; }
    }

    /// <summary>Marks a collider as a body part of a damageable actor.</summary>
    public class HitZone : MonoBehaviour
    {
        public IDamageable owner;
        public BodyPart part;
        public float mult = 1f;
        public static float MultFor(BodyPart p) => p == BodyPart.Head ? 3.2f : p == BodyPart.Thorax ? 1f : p == BodyPart.Stomach ? 1.1f : p == BodyPart.Arm ? 0.55f : 0.65f;
    }

    /// <summary>Tags a collider with an impact surface type (sound + effect).</summary>
    public class SurfaceTag : MonoBehaviour
    {
        public Synth.Surface surface = Synth.Surface.Concrete;
        public static Synth.Surface Of(Collider c)
        {
            if (c == null) return Synth.Surface.Concrete;
            if (c.GetComponentInParent<HitZone>() != null) return Synth.Surface.Flesh;
            var t = c.GetComponentInParent<SurfaceTag>();
            if (t != null) return t.surface;
            if (c.GetComponentInParent<Item>() != null) return Synth.Surface.Metal;
            return Synth.Surface.Concrete;
        }
    }

    /// <summary>Noise events let NPCs hear gunshots, footsteps and alarms.</summary>
    public static class Noise
    {
        public struct Event { public Vector3 pos; public float radius; public int source; public float time; public bool alarm; }
        public static readonly List<Event> Recent = new List<Event>();
        public static void Emit(Vector3 pos, float radius, int source, bool alarm = false)
        {
            Recent.Add(new Event { pos = pos, radius = radius, source = source, time = Time.time, alarm = alarm });
            if (Recent.Count > 64) Recent.RemoveAt(0);
        }
    }

    /// <summary>Simulated projectiles with drop and travel time (not hitscan).</summary>
    public class Ballistics : MonoBehaviour
    {
        static Ballistics _i;
        static Ballistics I { get { if (_i == null) { var go = new GameObject("Ballistics"); DontDestroyOnLoad(go); _i = go.AddComponent<Ballistics>(); } return _i; } }

        class Bullet
        {
            public Vector3 pos, vel;
            public float damage, life, travelled, force;
            public int shooter;
            public string gunId;
            public Transform ignore;
            public IDamageable ignoreOwner;
            public bool tracer, visualOnly, cracked;
        }

        readonly List<Bullet> _bullets = new List<Bullet>();
        static readonly RaycastHit[] _hits = new RaycastHit[16];
        public static System.Action<HitInfo, HitZone> OnHitActor; // hook for netcode / hit markers
        public static void ReportHit(HitInfo h, HitZone z) => OnHitActor?.Invoke(h, z);   // grenades / knives
        public static int BulletMask = ~0;

        public static void Fire(Vector3 origin, Vector3 dir, GunDef gun, int shooter, Transform ignore, IDamageable ignoreOwner, bool visualOnly, bool tracer)
        {
            int pellets = Mathf.Max(1, gun.pellets);
            for (int p = 0; p < pellets; p++)
            {
                Vector3 d = dir;
                if (pellets > 1) d = Quaternion.Euler(Random.Range(-1f, 1f) * gun.spread, Random.Range(-1f, 1f) * gun.spread, 0) * dir;
                I._bullets.Add(new Bullet
                {
                    pos = origin, vel = d.normalized * gun.velocity, damage = gun.damage, life = 2.5f, shooter = shooter, gunId = gun.id,
                    ignore = ignore, ignoreOwner = ignoreOwner, tracer = tracer && p == 0, visualOnly = visualOnly,
                    force = Mathf.Clamp(gun.damage * 0.06f, 1.2f, 9f)
                });
            }
            if (tracer)
            {
                Vector3 end = origin + dir.normalized * 350f;
                if (Physics.Raycast(origin + dir.normalized * 0.3f, dir, out var th, 350f, BulletMask, QueryTriggerInteraction.Ignore)) end = th.point;
                Fx.TracerLine(origin + dir.normalized * 0.5f, end, gun.velocity);
            }
            Noise.Emit(origin, gun.suppressed ? 18f : 140f, shooter);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 g = Physics.gravity;
            var playerHead = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head.position : new Vector3(0, -9999, 0);
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                b.life -= dt;
                Vector3 step = b.vel * dt;
                float len = step.magnitude;
                bool done = b.life <= 0;
                if (!done)
                {
                    int n = Physics.RaycastNonAlloc(b.pos, step / len, _hits, len, BulletMask, QueryTriggerInteraction.Collide);
                    if (n > 0)
                    {
                        System.Array.Sort(_hits, 0, n, HitCmp.Inst);
                        for (int h = 0; h < n; h++)
                        {
                            var hit = _hits[h];
                            if (hit.collider.isTrigger && SurfaceTag.Of(hit.collider) != Synth.Surface.Water) continue;
                            if (b.ignore != null && hit.collider.transform.IsChildOf(b.ignore)) continue;
                            var zone = hit.collider.GetComponent<HitZone>();
                            if (zone != null && b.ignoreOwner != null && zone.owner == b.ignoreOwner) continue;
                            if (zone != null && zone.owner != null && !zone.owner.Alive) zone = null; // corpses just take impacts
                            Resolve(b, hit, zone);
                            done = true;
                            break;
                        }
                    }
                }
                if (!done)
                {
                    // near-miss supersonic crack for the local player
                    if (!b.cracked && b.shooter != 0)
                    {
                        Vector3 toHead = playerHead - b.pos;
                        float t = Mathf.Clamp(Vector3.Dot(toHead, step / len), 0, len);
                        if ((b.pos + step / len * t - playerHead).sqrMagnitude < 2.2f * 2.2f)
                        {
                            b.cracked = true;
                            Sfx.Play(Synth.BulletCrack(Random.Range(0, 3)), b.pos + step / len * t, 0.8f, 1f + Random.Range(-0.1f, 0.1f), 1f, 15f);
                            RagoGame.I?.Player?.Suppress(0.5f);
                        }
                    }
                    b.pos += step;
                    b.travelled += len;
                    b.vel += g * dt;
                    b.vel *= 1f - 0.02f * dt; // drag
                }
                if (done) _bullets.RemoveAt(i);
            }
        }

        class HitCmp : IComparer<RaycastHit> { public static readonly HitCmp Inst = new HitCmp(); public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance); }

        static void Resolve(Bullet b, RaycastHit hit, HitZone zone)
        {
            Vector3 dir = b.vel.normalized;
            // falloff with distance for pistols/shotgun pellets
            float dmg = b.damage * Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(40f, 250f, b.travelled));
            var rb = hit.collider.attachedRigidbody;
            if (zone != null && zone.owner != null)
            {
                var info = new HitInfo
                {
                    damage = dmg * zone.mult, part = zone.part, point = hit.point, dir = dir, normal = hit.normal,
                    attacker = b.shooter, gunId = b.gunId, force = b.force
                };
                Fx.Blood(hit.point, dir, hit.normal, hit.collider.transform, info.damage);
                if (!b.visualOnly)
                {
                    zone.owner.TakeHit(info, zone);
                    OnHitActor?.Invoke(info, zone);
                    if (b.shooter == 0) Sfx.UI(Synth.HitMarker(), 0.35f);
                }
                return;
            }
            var surf = SurfaceTag.Of(hit.collider);
            Fx.Impact(hit.point, hit.normal, dir, surf, rb != null && !rb.isKinematic ? hit.collider.transform : (hit.collider.GetComponentInParent<HitZone>() != null ? hit.collider.transform : null), 1f);
            if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(dir * b.force * 0.6f, hit.point, ForceMode.Impulse);
            var dmgObj = hit.collider.GetComponentInParent<IBreakable>();
            if (dmgObj != null && !b.visualOnly) dmgObj.Damage(dmg, hit.point);
        }
    }

    public interface IBreakable { void Damage(float d, Vector3 p); }

    /// <summary>Footsteps for anyone walking around (you, raiders, other players): left-right steps on the right surface.</summary>
    public static class Footsteps
    {
        static int _mask;
        public static Synth.Surface SurfaceUnder(Vector3 pos)
        {
            if (_mask == 0)
            {
                _mask = ~0;
                foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) _mask &= ~(1 << l);
            }
            if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out var h, 1f, _mask, QueryTriggerInteraction.Ignore))
            {
                var s = SurfaceTag.Of(h.collider);
                return s == Synth.Surface.Flesh ? Synth.Surface.Concrete : s;
            }
            return Synth.Surface.Concrete;
        }

        /// <summary>Call every frame with the walker's feet position; plays a step every stride.</summary>
        public static void Track(ref Vector3 last, ref float dist, ref bool leftFoot, Vector3 feet, Vector3 forward, float dt, float volume)
        {
            Vector3 d = feet - last; d.y = 0;
            last = feet;
            float moved = d.magnitude;
            if (moved > 2f) { dist = 0; return; }   // teleported
            float speed = moved / Mathf.Max(dt, 0.001f);
            if (speed < 0.3f) { dist = Mathf.Min(dist, 0.5f); return; }
            dist += moved;
            bool run = speed > 2.6f;
            if (dist < (run ? 1.1f : 0.72f)) return;
            dist = 0;
            leftFoot = !leftFoot;
            forward.y = 0; forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (leftFoot ? -0.12f : 0.12f);
            Sfx.Play(Synth.Footstep(SurfaceUnder(feet), Random.Range(0, 8), run), feet + side + Vector3.up * 0.05f,
                volume * (run ? 1.3f : 1f) * Random.Range(0.85f, 1.1f), Random.Range(0.94f, 1.06f), 1f, run ? 30f : 22f);
        }
    }
}
