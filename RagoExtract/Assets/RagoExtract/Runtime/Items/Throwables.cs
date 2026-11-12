using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Frag grenade or flashbang. Grab it, pull the TRIGGER to pull the pin (you hear the click and the pin comes out),
    /// then throw it: it goes off a few seconds after the pin is pulled - wherever it is, including your hand.
    /// Desktop: click once to pull the pin, click again to throw it where you're looking.
    /// </summary>
    public class Grenade : Item
    {
        public bool Flash;
        bool _armed;
        float _fuse;
        Transform _pin;
        static bool _hinted;
        public const float FragFuse = 3.6f, FlashFuse = 2.2f;

        public static Grenade Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var g = go.AddComponent<Grenade>();
            g.def = d;
            g.Flash = d.shape == 1;
            Color w = Color.white;
            var mb = new MB(2);
            Material body = g.Flash ? Mats.Lit("flash_body", Color.white, 0.6f, 0.45f, Mats.Metal, null, 0.3f, 0.3f) : Mats.Lit("frag_body", Color.white, 0.2f, 0.35f, Mats.Noise, null, 0.45f, 0.25f);
            Material metal = Mats.Lit("grenade_metal", Color.white, 0.9f, 0.6f, Mats.Metal, null, 0.2f, 0.3f);
            if (g.Flash)
            {
                mb.Sub(0).Cyl(new Vector3(0, -0.055f, 0), new Vector3(0, 0.045f, 0), 0.024f, 0.024f, 16, new Color(0.55f, 0.57f, 0.58f));
                for (int i = 0; i < 6; i++) { float a = i * 60f * Mathf.Deg2Rad; mb.Sub(0).Box(new Vector3(Mathf.Cos(a) * 0.0235f, 0f, Mathf.Sin(a) * 0.0235f), new Vector3(0.008f, 0.03f, 0.008f), new Color(0.12f, 0.12f, 0.12f)); }
                mb.Sub(0).Cyl(new Vector3(0, -0.03f, 0), new Vector3(0, -0.02f, 0), 0.0245f, 0.0245f, 16, new Color(0.15f, 0.15f, 0.15f));
            }
            else
            {
                mb.Sub(0).Sphere(new Vector3(0, -0.015f, 0), new Vector3(0.032f, 0.038f, 0.032f), 16, new Color(0.3f, 0.34f, 0.22f));
                mb.Sub(0).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, -0.054f, 0), 0.014f, 0.012f, 12, new Color(0.25f, 0.28f, 0.18f));
            }
            // fuse head + spoon down the side
            mb.Sub(1).Cyl(new Vector3(0, 0.02f, 0), new Vector3(0, 0.05f, 0), 0.012f, 0.011f, 12, w);
            mb.Sub(1).Box(new Vector3(0.018f, 0.025f, 0), new Vector3(0.008f, 0.06f, 0.014f), w * 0.85f, 0.002f, 1, Quaternion.Euler(0, 0, -8));
            mb.Build("Body", go.transform, body, metal);
            // pin ring (comes off when you pull the pin)
            g._pin = new GameObject("Pin").transform; g._pin.SetParent(go.transform, false);
            g._pin.localPosition = new Vector3(-0.016f, 0.045f, 0);
            var ring = new MB();
            for (int i = 0; i < 12; i++) { float a = i * 30f; ring.Box(Quaternion.Euler(0, 0, a) * new Vector3(0.011f, 0, 0) + new Vector3(-0.01f, 0, 0), new Vector3(0.002f, 0.006f, 0.002f), w, 0, 0, Quaternion.Euler(0, 0, a)); }
            ring.Build("Ring", g._pin, metal);
            var col = go.AddComponent<SphereCollider>(); col.radius = g.Flash ? 0.032f : 0.04f; col.center = new Vector3(0, -0.005f, 0);
            g.localBounds = new Bounds(Vector3.zero, new Vector3(0.07f, 0.11f, 0.07f));
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            g.SetupPhysics(0.4f);
            return g;
        }

        public override string Label => def.name + (_armed ? "  (PIN PULLED)" : "");

        public override void HeldUpdate(Hand h, HandInput input)
        {
            if (!_armed && input.TriggerDown) { Arm(); h.Haptic(0.5f, 0.05f); return; }
            // desktop: second click throws it where you're looking
            if (_armed && !XRInputs.VR && input.TriggerDown)
            {
                var head = RagoGame.I.Player.Head;
                h.ForceRelease(true);
                primaryHand = null;
                Drop(Vector3.zero, Vector3.zero);
                transform.position = head.position + head.forward * 0.5f;
                rb.SetVel(head.forward * 13f + Vector3.up * 2.5f);
                rb.angularVelocity = Random.insideUnitSphere * 8f;
            }
        }

        void Arm()
        {
            _armed = true;
            _fuse = Flash ? FlashFuse : FragFuse;
            if (_pin != null) _pin.gameObject.SetActive(false);
            Sfx.Play(Synth.PinPull(), transform.position, 0.7f, 1f, 0.5f, 8f);
            if (!_hinted) { _hinted = true; RagoGame.I?.Player?.Toast("Pin pulled - throw it! (" + (int)_fuse + " s)"); }
        }

        public override void AnyUpdate()
        {
            if (!_armed) return;
            _fuse -= Time.deltaTime;
            if (_fuse > 0) return;
            _armed = false;
            Vector3 p = transform.position;
            if (primaryHand != null) primaryHand.ForceRelease(true);
            if (slot != null) slot.Release(this);
            Blast.Detonate(p, Flash, 0, true);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// A knife. Swing or stab it into someone: the faster the blade moves, the harder it hits (head hits hurt most).
    /// Desktop: click to stab what you're looking at.
    /// </summary>
    public class Knife : Item
    {
        Transform _tip, _base;
        Vector3 _lastTip, _lastBase;
        bool _track;
        float _damage = 45f;
        readonly Dictionary<IDamageable, float> _cool = new Dictionary<IDamageable, float>();
        static readonly RaycastHit[] _hits = new RaycastHit[12];

        public static Knife Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var k = go.AddComponent<Knife>();
            k.def = d;
            k._damage = d.heal > 0 ? d.heal : 45f;   // knives keep their damage in the def's "heal" slot
            bool hunt = d.shape == 1;
            float blade = hunt ? 0.17f : 0.14f;
            var mb = new MB(2);
            Color w = Color.white;
            // handle along -z, guard at 0, blade along +z (the hand holds the handle like a pistol grip)
            mb.Sub(0).Box(new Vector3(0, 0, -0.055f), new Vector3(0.024f, 0.03f, 0.11f), hunt ? new Color(0.42f, 0.28f, 0.16f) : new Color(0.1f, 0.1f, 0.1f), 0.008f, 2);
            mb.Sub(1).Box(new Vector3(0, 0, 0.004f), new Vector3(0.03f, 0.05f, 0.008f), w * 0.7f, 0.003f);
            mb.Sub(1).Box(new Vector3(0, 0.002f, 0.008f + blade * 0.5f), new Vector3(0.004f, 0.028f, blade), w, 0.001f);
            mb.Sub(1).Box(new Vector3(0, -0.006f, 0.008f + blade + 0.008f), new Vector3(0.004f, 0.016f, 0.02f), w, 0.001f, 1, Quaternion.Euler(-35, 0, 0));
            mb.Sub(1).Cyl(new Vector3(0, 0, -0.115f), new Vector3(0, 0, -0.105f), 0.016f, 0.016f, 10, w * 0.6f);
            mb.Build("Model", go.transform, Mats.Lit(hunt ? "knife_wood" : "knife_grip", Color.white, 0f, 0.3f, hunt ? Mats.Wood : Mats.Noise, null, 0.3f, 0.1f),
                Mats.Lit("knife_steel", Color.white, 0.95f, 0.8f, Mats.Metal, null, 0.1f, 0.4f));
            k.grip = new GameObject("Grip").transform; k.grip.SetParent(go.transform, false); k.grip.localPosition = new Vector3(0, 0, -0.055f);
            k._tip = new GameObject("Tip").transform; k._tip.SetParent(go.transform, false); k._tip.localPosition = new Vector3(0, 0, 0.02f + blade);
            k._base = new GameObject("BladeBase").transform; k._base.SetParent(go.transform, false); k._base.localPosition = new Vector3(0, 0, 0.02f);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0, (0.008f + blade - 0.11f) * 0.5f); col.size = new Vector3(0.03f, 0.05f, 0.12f + blade);
            k.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            k.SetupPhysics(0.3f);
            return k;
        }

        public override void OnGrab(Hand h) { base.OnGrab(h); _track = false; }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (!XRInputs.VR)
            {
                if (input.TriggerDown && Physics.Raycast(pl.Head.position, pl.Head.forward, out var dh, 1.7f, ~0, QueryTriggerInteraction.Ignore)) TryHit(dh.collider, dh.point, pl.Head.forward, 5f, h);
                return;
            }
            Vector3 tip = _tip.position, bas = _base.position;
            if (_track)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.001f);
                float speed = (tip - _lastTip).magnitude / dt;
                if (speed > 1.8f)
                {
                    // sweep the blade from last frame to this one
                    for (int s = 0; s < 3; s++)
                    {
                        float t = s / 2f;
                        Vector3 a = Vector3.Lerp(_lastBase, _lastTip, t), b = Vector3.Lerp(bas, tip, t);
                        Vector3 d = b - a; float len = d.magnitude;
                        if (len < 0.001f) continue;
                        int n = Physics.SphereCastNonAlloc(a, 0.012f, d / len, _hits, len, ~0, QueryTriggerInteraction.Collide);
                        for (int i = 0; i < n; i++) if (TryHit(_hits[i].collider, _hits[i].point == Vector3.zero ? b : _hits[i].point, d / len, speed, h)) { s = 3; break; }
                    }
                }
            }
            _lastTip = tip; _lastBase = bas; _track = true;
        }

        bool TryHit(Collider c, Vector3 point, Vector3 dir, float speed, Hand h)
        {
            var zone = c.GetComponentInParent<HitZone>();
            if (zone == null || zone.owner == null || !zone.owner.Alive) return false;
            if (zone.owner == (IDamageable)RagoGame.I.Player.Vitals) return false;   // not yourself
            if (_cool.TryGetValue(zone.owner, out var until) && Time.time < until) return false;
            _cool[zone.owner] = Time.time + 0.4f;
            float mult = zone.part == BodyPart.Head ? 1.8f : zone.part == BodyPart.Thorax || zone.part == BodyPart.Stomach ? 1f : 0.6f;
            float dmg = _damage * Mathf.Clamp(speed / 4f, 0.6f, 1.5f) * mult;
            var info = new HitInfo { damage = dmg, part = zone.part, point = point, dir = dir, normal = -dir, attacker = 0, gunId = def.id, force = 2f };
            Fx.Blood(point, dir, -dir, c.transform, dmg);
            zone.owner.TakeHit(info, zone);
            Ballistics.ReportHit(info, zone);
            Sfx.UI(Synth.HitMarker(), 0.3f);
            h.Haptic(0.8f, 0.08f);
            Noise.Emit(point, 6f, 0);
            return true;
        }
    }

    /// <summary>What happens when a grenade goes off - run on every client so everyone sees and hears it.</summary>
    public static class Blast
    {
        public const float FragRadius = 9f, FlashRadius = 16f;
        static readonly Collider[] _cols = new Collider[256];

        /// <param name="local">true on the thrower's machine: it applies the damage, forces and noise, and tells the others</param>
        public static void Detonate(Vector3 p, bool flash, int attacker, bool local)
        {
            Fx.Explosion(p, flash);
            Sfx.Play(flash ? Synth.FlashBang() : Synth.Explosion(), p, 1f, 1f + Random.Range(-0.05f, 0.05f), 6f, flash ? 140f : 260f);
            // everyone works out their own flash blindness and camera shake
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead)
            {
                float d = Vector3.Distance(pl.Head.position, p);
                if (flash && d < FlashRadius && ClearLine(p, pl.Head.position))
                {
                    float facing = Mathf.InverseLerp(-0.3f, 0.9f, Vector3.Dot(pl.Head.forward, (p - pl.Head.position).normalized));
                    float strength = Mathf.Clamp01(1.15f - d / FlashRadius) * Mathf.Lerp(0.35f, 1f, facing);
                    pl.Hud.Flashbang(strength);
                }
                else if (!flash && d < FragRadius * 2.5f) pl.Hud.Suppress(Mathf.Clamp01(1.2f - d / (FragRadius * 2.5f)));
            }
            bool authority = !NetGame.Live || (NetHub.Backend != null && NetHub.Backend.IsHost);
            if (flash && authority)
            {
                // raiders who can see it are blinded and stagger
                int n = Physics.OverlapSphereNonAlloc(p, FlashRadius, _cols, ~0, QueryTriggerInteraction.Collide);
                var done = new HashSet<Npc>();
                for (int i = 0; i < n; i++)
                {
                    var npc = _cols[i].GetComponentInParent<Npc>();
                    if (npc == null || !npc.Alive || !done.Add(npc)) continue;
                    float d = Vector3.Distance(npc.transform.position, p);
                    if (ClearLine(p, npc.transform.position + Vector3.up * 1.6f)) npc.Flash(Mathf.Lerp(6f, 2f, d / FlashRadius));
                }
            }
            if (!local) return;
            Noise.Emit(p, flash ? 45f : 80f, attacker, true);
            if (NetGame.Live) NetGame.I.SendBlast(p, flash);
            if (flash) return;
            // frag: damage everyone with a clear line to the blast, push loose things and bodies
            int m = Physics.OverlapSphereNonAlloc(p, FragRadius, _cols, ~0, QueryTriggerInteraction.Collide);
            var hitOwners = new Dictionary<IDamageable, HitZone>();
            for (int i = 0; i < m; i++)
            {
                var c = _cols[i];
                var z = c.GetComponent<HitZone>();
                if (z != null && z.owner != null && z.owner.Alive)
                {
                    // the shrapnel hits the part of them nearest the blast (legs and belly for a grenade on the floor)
                    if (!hitOwners.TryGetValue(z.owner, out var cur) || (z.transform.position - p).sqrMagnitude < (cur.transform.position - p).sqrMagnitude) hitOwners[z.owner] = z;
                }
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) rb.AddExplosionForce(14f, p, FragRadius, 0.6f, ForceMode.Impulse);
            }
            foreach (var kv in hitOwners)
            {
                var z = kv.Value;
                Vector3 at = z.transform.position;
                float d = Vector3.Distance(at, p);
                if (!ClearLine(p, at)) continue;
                float k = Mathf.Clamp01(1f - d / FragRadius);
                float dmg = 25f + 210f * k * k;   // a frag within ~3 m kills an ordinary raider
                var info = new HitInfo { damage = dmg, part = z.part, point = at, dir = (at - p).normalized, normal = (p - at).normalized, attacker = attacker, gunId = "frag", force = 6f * k + 1f };
                z.owner.TakeHit(info, z);
                Ballistics.ReportHit(info, z);
            }
        }

        static bool ClearLine(Vector3 from, Vector3 to)
        {
            // walls and floors block a blast; people, items and hands don't
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            return !Physics.Linecast(from + Vector3.up * 0.15f, to, out var hit, mask, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<HitZone>() != null;
        }
    }
}
