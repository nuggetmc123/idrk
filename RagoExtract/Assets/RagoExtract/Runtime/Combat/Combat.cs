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
        public AmmoType ammo;     // incendiary sets them on fire; AP punches armour
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

        /// <summary>How much of a sound gets from a to b through the Underground's concrete floors: 1 on the same level,
        /// ~10% one floor away, ~2% two floors away. Everywhere else 1.</summary>
        public static float Through(Vector3 a, Vector3 b)
        {
            if (!(RagoGame.I != null && RagoGame.I.Map is UndergroundMap)) return 1f;
            int la = Mathf.FloorToInt((-a.y + 3.5f) / 3.9f), lb = Mathf.FloorToInt((-b.y + 3.5f) / 3.9f);
            int n = Mathf.Abs(la - lb);
            return n == 0 ? 1f : n == 1 ? 0.1f : 0.02f;
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
            public bool tracer, visualOnly, cracked, localOnly, passCapsule;
            public AmmoType ammo;
            public float punch;    // what's left to push through walls (calibre + AP)
            public int walls;      // walls it has gone through
        }

        readonly List<Bullet> _bullets = new List<Bullet>();
        static readonly RaycastHit[] _hits = new RaycastHit[16];
        public static System.Action<HitInfo, HitZone> OnHitActor; // hook for netcode / hit markers
        public static void ReportHit(HitInfo h, HitZone z) => OnHitActor?.Invoke(h, z);   // grenades / knives
        public static int BulletMask = ~0;

        static int _wallMask = 0;
        /// <summary>World geometry only (not items, hands, bodies or players).</summary>
        public static int WallMask
        {
            get
            {
                if (_wallMask != 0) return _wallMask;
                int m = ~0;
                foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) m &= ~(1 << l);
                m &= ~(1 << Window.Layer);   // window glass: you (and raiders) see through it, lights shine through - bullets still hit it
                return _wallMask = m;
            }
        }

        /// <summary>
        /// Where a shot really starts: the muzzle, unless the barrel is poking through a wall (or door) between the
        /// shooter's eye and the muzzle - then the shot starts at that wall and hits it. No shooting through walls.
        /// </summary>
        public static Vector3 SafeOrigin(Vector3 eye, Vector3 muzzle)
        {
            Vector3 d = muzzle - eye; float len = d.magnitude;
            if (len < 0.01f) return muzzle;
            if (Physics.Raycast(eye, d / len, out var h, len, WallMask, QueryTriggerInteraction.Ignore)) return h.point - d / len * 0.03f;
            return muzzle;
        }

        public static void Fire(Vector3 origin, Vector3 dir, GunDef gun, int shooter, Transform ignore, IDamageable ignoreOwner, bool visualOnly, bool tracer, bool localOnly = false, AmmoType ammo = AmmoType.Standard)
        {
            if (ammo == AmmoType.Incendiary) tracer = true;   // incendiary rounds glow as they fly
            int pellets = Mathf.Max(1, gun.pellets);
            for (int p = 0; p < pellets; p++)
            {
                Vector3 d = dir;
                if (pellets > 1) d = Quaternion.Euler(Random.Range(-1f, 1f) * gun.spread, Random.Range(-1f, 1f) * gun.spread, 0) * dir;
                I._bullets.Add(new Bullet
                {
                    pos = origin, vel = d.normalized * gun.velocity, life = 2.5f, shooter = shooter, gunId = gun.id, ammo = ammo,
                    damage = gun.damage * (ammo == AmmoType.AP ? 0.9f : ammo == AmmoType.Incendiary ? 0.85f : 1f),
                    punch = (pellets > 1 ? 0.6f : ArmorDef.Penetration(gun.id)) + (ammo == AmmoType.AP ? 2.5f : 0f),
                    ignore = ignore, ignoreOwner = ignoreOwner, tracer = tracer && p == 0, visualOnly = visualOnly, localOnly = localOnly, passCapsule = Random.value < 0.5f,
                    force = Mathf.Clamp(gun.damage * 0.06f, 1.2f, 9f)
                });
            }
            if (tracer)
            {
                Vector3 end = origin + dir.normalized * 350f;
                if (Physics.Raycast(origin + dir.normalized * 0.3f, dir, out var th, 350f, BulletMask, QueryTriggerInteraction.Ignore)) end = th.point;
                Fx.TracerLine(origin + dir.normalized * 0.5f, end, gun.velocity, ammo == AmmoType.Incendiary ? new Color(1f, 0.45f, 0.1f) : (Color?)null);
            }
            Noise.Emit(origin, gun.suppressed ? 8f : 140f, shooter);   // a suppressed shot: raiders only hear it within ~8 m (and not through floors)
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
                    pierced = false;
                    int n = Physics.RaycastNonAlloc(b.pos, step / len, _hits, len, BulletMask, QueryTriggerInteraction.Collide);
                    if (n > 0)
                    {
                        System.Array.Sort(_hits, 0, n, HitCmp.Inst);
                        for (int h = 0; h < n; h++)
                        {
                            var hit = _hits[h];
                            if (hit.collider.isTrigger && SurfaceTag.Of(hit.collider) != Synth.Surface.Water) continue;
                            // a player's movement capsule wraps their body: your own shots and other players' shots go on to the body's
                            // hit zones; a raider's shot gets through half the time (raiders hitting less often, as asked earlier) -
                            // stopping them all at the capsule made you invincible to everything
                            if (hit.collider is CharacterController && (b.shooter < 1000 || b.passCapsule)) continue;
                            if (b.ignore != null && hit.collider.transform.IsChildOf(b.ignore)) continue;
                            // window glass shatters and the bullet carries on through
                            var win = hit.collider.GetComponent<Window>();
                            if (win != null) { win.Break(dir: step / len, tell: b.shooter == 0 && !b.visualOnly); continue; }
                            var zone = hit.collider.GetComponent<HitZone>();
                            if (zone != null && b.ignoreOwner != null && zone.owner == b.ignoreOwner) continue;
                            if (zone != null && zone.owner != null && !zone.owner.Alive) zone = null; // corpses just take impacts
                            // wall banging: a round with enough punch for this material goes through one layer of it
                            if (zone == null && TryPierce(b, hit, step / len)) { pierced = true; break; }
                            Resolve(b, hit, zone);
                            done = true;
                            break;
                        }
                    }
                }
                if (!done && pierced) { b.life -= dt; continue; }   // carried on out the far side of the wall
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

        static bool pierced;

        /// <summary>How hard a material is to shoot through (same scale as a round's punch). Ground and rock stop everything.</summary>
        static float Resist(Collider c)
        {
            if (c is TerrainCollider || (c is MeshCollider mc && mc.gameObject.name == "Terrain")) return 99f;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return 99f;   // loose objects just get knocked about
            switch (SurfaceTag.Of(c))
            {
                case Synth.Surface.Wood: return 1.6f;
                case Synth.Surface.Metal: return 3.2f;
                case Synth.Surface.Concrete: return 7.2f;
                case Synth.Surface.Flesh: return 99f;
                default: return 99f;   // dirt, grass, water
            }
        }

        /// <summary>The round goes through this wall: holes on both sides, it keeps going a little weaker. One wall only
        /// (the 20mm too - "one layer").</summary>
        static bool TryPierce(Bullet b, RaycastHit hit, Vector3 dir)
        {
            if (b.walls >= 1) return false;
            float r = Resist(hit.collider);
            if (b.punch <= r) return false;
            var surf = SurfaceTag.Of(hit.collider);
            float big = b.punch >= 10f ? 2.2f : 1f;
            Fx.Impact(hit.point, hit.normal, dir, surf, null, big);
            // exit hole: come back from the far side
            Vector3 far = hit.point + dir * 1.2f;
            var back = Physics.RaycastAll(far, -dir, 1.2f, BulletMask, QueryTriggerInteraction.Ignore);
            Vector3 exit = hit.point + dir * 0.3f;
            foreach (var bh in back) if (bh.collider == hit.collider) { exit = bh.point; Fx.Impact(bh.point, bh.normal, dir, surf, null, big); break; }
            if (!b.visualOnly)
            {
                var br = hit.collider.GetComponentInParent<IBreakable>();
                if (br != null) br.Damage(b.damage, hit.point);
                else { var sd = hit.collider.GetComponentInParent<SwingDoor>(); if (sd != null && sd.LockNear(hit.point)) sd.DamageLock(b.damage, true); }
            }
            b.walls++;
            b.damage *= Mathf.Clamp(1f - r / b.punch * 0.55f, 0.35f, 0.85f);
            b.punch -= r;
            b.pos = exit + dir * 0.02f;
            Noise.Emit(hit.point, 25f, b.shooter);
            return true;
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
                    attacker = b.shooter, gunId = b.gunId, force = b.force, ammo = b.ammo
                };
                Fx.Blood(hit.point, dir, hit.normal, hit.collider.transform, info.damage);
                // localOnly: a raider's shot replayed on this machine - it can hurt the player here, nobody else
                // (each player works out the raiders' bullets against themselves, so they always land where you are)
                bool mine = b.localOnly && RagoGame.I != null && RagoGame.I.Player != null && zone.owner == (IDamageable)RagoGame.I.Player.Vitals;
                if (mine) { zone.owner.TakeHit(info, zone); return; }
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
            else if (!b.visualOnly) { var sd = hit.collider.GetComponentInParent<SwingDoor>(); if (sd != null && sd.LockNear(hit.point)) sd.DamageLock(dmg, true); }   // close enough to the padlock
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
        public static void Track(ref Vector3 last, ref float dist, ref bool leftFoot, Vector3 feet, Vector3 forward, float dt, float volume, float runMul = 1.3f, float runRange = 30f)
        {
            Vector3 d = feet - last; d.y = 0;
            last = feet;
            float moved = d.magnitude;
            if (moved > 2f) { dist = 0; return; }   // teleported
            float speed = moved / Mathf.Max(dt, 0.001f);
            if (speed < 0.3f) { dist = Mathf.Min(dist, 0.5f); return; }
            dist += moved;
            bool run = speed > 3.4f;
            if (dist < (run ? 1.5f : 1.1f)) return;
            dist = 0;
            leftFoot = !leftFoot;
            forward.y = 0; forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (leftFoot ? -0.12f : 0.12f);
            Sfx.Play(Synth.Footstep(SurfaceUnder(feet), Random.Range(0, 8), run), feet + side + Vector3.up * 0.05f,
                volume * (run ? runMul : 1f) * Random.Range(0.85f, 1.1f), Random.Range(0.94f, 1.06f), 1f, run ? runRange : 22f);
        }
    }
}
