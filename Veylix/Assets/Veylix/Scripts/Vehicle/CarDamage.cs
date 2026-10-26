using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Damage from real collisions. Every hit is measured by its impulse (the change of speed it forced on the car) and
    /// where on the car it landed, so a gentle touch does nothing, a wall at an angle breaks a wing, and only a truly
    /// violent impact can start a fire. Damage changes how the car drives (aero, steering, power, wheels) unless the
    /// Damage setting is Visual or Off.
    /// </summary>
    public class CarDamage : MonoBehaviour
    {
        public enum Part { FrontWingL, FrontWingR, RearWing, SuspFL, SuspFR, SuspRL, SuspRR, Engine, Gearbox, Body, FuelCell, Count }

        public readonly float[] health = new float[(int)Part.Count];
        public CarParts parts;
        public Car car;

        public float Fire { get; private set; }        // 0..1 how big the fire is
        public bool Exploded { get; private set; }
        public bool Smoking => health[(int)Part.Engine] < 0.55f || Fire > 0f;
        public float TotalDamage { get; private set; }  // 0..1 for the HUD
        public float LastImpactTime { get; private set; } = -99f;
        public float LastImpactG { get; private set; }

        /// <summary>Raised for every hit the driver should feel: strength 0..1.</summary>
        public event System.Action<float, Vector3> OnImpact;
        public event System.Action OnExplode;
        public event System.Action<Part> OnPartBroken;

        float fuelLeak, explodeAt = -1f, scrapeCooldown;
        readonly float[] toe = new float[4];
        float steerPull;
        bool[] broken = new bool[(int)Part.Count];

        bool Performance => Settings.Damage == DamageMode.Full || !car.isPlayer;
        bool Enabled => Settings.Damage != DamageMode.Off || !car.isPlayer;

        public bool EngineDead => Exploded || (Performance && health[(int)Part.Engine] <= 0.02f);
        public float PowerScale => Performance ? Mathf.Lerp(0.55f, 1f, health[(int)Part.Engine]) * Mathf.Lerp(0.85f, 1f, health[(int)Part.Gearbox]) : 1f;
        public float CoolingPenalty => Performance ? Mathf.Lerp(2.2f, 1f, Mathf.Min(health[(int)Part.Engine], health[(int)Part.Body])) : 1f;
        public float FrontAero => Performance ? 0.35f + 0.325f * health[(int)Part.FrontWingL] + 0.325f * health[(int)Part.FrontWingR] : 1f;
        public float RearAero => Performance ? Mathf.Lerp(0.45f, 1f, health[(int)Part.RearWing]) * Mathf.Lerp(0.8f, 1f, health[(int)Part.Body]) : 1f;
        public float DragScale => Performance ? 1f + (1f - health[(int)Part.Body]) * 0.15f : 1f;
        public float SteerPull => Performance ? steerPull : 0f;
        public float Toe(int wheel) => Performance ? toe[wheel] : 0f;

        public bool Wrecked => Exploded || EngineDead || car.wheels[0].detached || car.wheels[1].detached || car.wheels[2].detached || car.wheels[3].detached;

        void Awake()
        {
            car = GetComponent<Car>();
            Repair();
        }

        public void Repair()
        {
            for (int i = 0; i < health.Length; i++) { health[i] = 1f; broken[i] = false; }
            for (int i = 0; i < 4; i++) toe[i] = 0f;
            steerPull = 0f;
            Fire = 0f; fuelLeak = 0f; explodeAt = -1f; Exploded = false; TotalDamage = 0f;
            if (car != null && car.wheels[0] != null)
                foreach (var w in car.wheels) { w.detached = false; w.gripScale = 1f; }
            parts?.RestoreAll();
            Fx.StopAttached(transform);
        }

        void OnCollisionEnter(Collision c) => Hit(c, true);
        void OnCollisionStay(Collision c) => Hit(c, false);

        void Hit(Collision c, bool first)
        {
            if (car == null || car.spec == null) return;
            if (c.collider != null && c.collider.gameObject.layer == Layers.Debris) return;
            Vector3 impulse = c.impulse;
            float dv = impulse.magnitude / Mathf.Max(1f, car.Body.mass); // m/s change of speed from this contact
            if (c.contactCount == 0) return;
            var contact = c.GetContact(0);
            Vector3 point = contact.point;

            // Scraping along a wall: sparks while sliding, no damage.
            float slideSpeed = Vector3.ProjectOnPlane(car.Body.GetPointVelocity(point), contact.normal).magnitude;
            if (!first || dv < 1.2f)
            {
                scrapeCooldown -= Time.fixedDeltaTime;
                if (slideSpeed > 6f && scrapeCooldown <= 0f && c.collider.gameObject.layer != Layers.Terrain)
                {
                    scrapeCooldown = 0.06f;
                    Fx.Sparks(point, contact.normal, Mathf.Clamp01(slideSpeed / 40f), car.Body.GetPointVelocity(point));
                    OnImpact?.Invoke(Mathf.Clamp01(slideSpeed / 80f) * 0.4f, point);
                }
                if (!first) return;
            }
            if (dv < 1.2f) return;

            float g = dv / Time.fixedDeltaTime / Car.Gravity * 0.02f; // rough peak g with a ~20 ms crush time
            LastImpactTime = Time.time;
            LastImpactG = g;
            float strength = Mathf.Clamp01(dv / 25f);
            OnImpact?.Invoke(strength, point);
            CrashAudio.Impact(point, strength, c.collider.gameObject.layer == Layers.Car);
            if (dv > 3f) Fx.Sparks(point, contact.normal, Mathf.Clamp01(dv / 15f), car.Body.GetPointVelocity(point));
            if (dv > 6f) Fx.DebrisBurst(point, contact.normal, Mathf.Clamp01(dv / 25f), car.parts != null ? car.parts.paint : null);
            if (!Enabled) return;

            Vector3 local = transform.InverseTransformPoint(point);
            float hb = car.spec.wheelbase * 0.5f;
            float energy = Mathf.Max(0f, dv - 2.5f) / 22f; // 0 at a nudge, ~1 at a 25 m/s stop

            // Where did it land?
            bool nose = local.z > hb - 0.1f;
            bool tail = local.z < -hb - 0.2f;
            bool leftSide = local.x < 0f;
            if (nose)
            {
                Damage(leftSide ? Part.FrontWingL : Part.FrontWingR, energy * 1.8f);
                if (Mathf.Abs(local.x) < 0.4f) { Damage(Part.FrontWingL, energy * 1.2f); Damage(Part.FrontWingR, energy * 1.2f); }
                Damage(Part.Body, energy * 0.3f);
            }
            else if (tail)
            {
                Damage(Part.RearWing, energy * 1.4f);
                Damage(Part.Gearbox, energy * 0.6f);
                Damage(Part.Body, energy * 0.2f);
            }
            else
            {
                Damage(Part.Body, energy * 0.6f);
                Damage(Part.Engine, energy * (Mathf.Abs(local.x) < 0.5f ? 0.6f : 0.35f));
                Damage(Part.FuelCell, energy * 0.4f);
            }
            // Wheels near the contact: bent suspension, or the whole corner gone.
            for (int i = 0; i < 4; i++)
            {
                var w = car.wheels[i];
                float d = Vector2.Distance(new Vector2(local.x, local.z), new Vector2(w.anchor.x, w.anchor.z));
                if (d < 0.9f) Damage(Part.SuspFL + i, energy * Mathf.Lerp(1.6f, 0.4f, d / 0.9f));
            }
            // A big enough hit anywhere hurts the engine (and the driver's day).
            if (dv > 18f) Damage(Part.Engine, energy * 0.5f);

            // Fire needs a violent impact that splits the fuel cell, not just a hard knock.
            if (dv > 24f && health[(int)Part.FuelCell] < 0.5f)
            {
                fuelLeak = Mathf.Max(fuelLeak, Mathf.InverseLerp(24f, 45f, dv));
                if (Fire <= 0f) Fire = 0.05f;
            }
            UpdateHandling();
        }

        void Damage(Part p, float amount)
        {
            if (amount <= 0f) return;
            int i = (int)p;
            health[i] = Mathf.Clamp01(health[i] - amount);
            float total = 0f;
            for (int k = 0; k < health.Length; k++) total += 1f - health[k];
            TotalDamage = Mathf.Clamp01(total / 4f);

            if (!broken[i] && ShouldBreak(p))
            {
                broken[i] = true;
                parts?.Break(p, car);
                OnPartBroken?.Invoke(p);
                if (p >= Part.SuspFL && p <= Part.SuspRR && Performance)
                {
                    var w = car.wheels[i - (int)Part.SuspFL];
                    w.detached = true;
                }
            }
            else parts?.Dent(p, 1f - health[i]);
        }

        bool ShouldBreak(Part p)
        {
            float h = health[(int)p];
            switch (p)
            {
                case Part.FrontWingL: case Part.FrontWingR: return h < 0.35f;
                case Part.RearWing: return h < 0.3f;
                case Part.SuspFL: case Part.SuspFR: case Part.SuspRL: case Part.SuspRR: return h < 0.08f;
                default: return false;
            }
        }

        void UpdateHandling()
        {
            // Bent suspension: toe changes (the car pulls) and the corner grips less.
            for (int i = 0; i < 4; i++)
            {
                float h = health[(int)Part.SuspFL + i];
                float bend = (1f - h) * (1f - h);
                float dir = ((i * 7919) % 2 == 0) ? 1f : -1f;
                toe[i] = bend * 4.5f * dir;
                if (car.wheels[i] != null) car.wheels[i].gripScale = Mathf.Lerp(0.7f, 1f, h);
            }
            steerPull = (toe[0] + toe[1]) * 0.5f;
        }

        void Update()
        {
            if (car == null) return;
            if (Fire > 0f && !Exploded)
            {
                // A fire grows while fuel feeds it, and dies down otherwise.
                Fire = Mathf.Clamp01(Fire + (fuelLeak > 0.2f ? 0.08f + fuelLeak * 0.12f : -0.05f) * Time.deltaTime);
                Fx.AttachFire(transform, car.parts != null ? car.parts.enginePoint : Vector3.up * 0.6f, Fire);
                if (Fire <= 0f) Fx.StopFire(transform);
                if (Performance && Fire > 0.3f) health[(int)Part.Engine] = Mathf.Max(0f, health[(int)Part.Engine] - Time.deltaTime * 0.15f);
                if (fuelLeak > 0.55f && Fire >= 0.95f && explodeAt < 0f) explodeAt = Time.time + Random.Range(1.2f, 3.5f);
            }
            if (explodeAt > 0f && Time.time >= explodeAt && !Exploded) Explode();
            if (Smoking && !Exploded) Fx.AttachSmoke(transform, car.parts != null ? car.parts.enginePoint : Vector3.up * 0.6f, 1f - health[(int)Part.Engine]);
        }

        /// <summary>
        /// The car tripped and is rolling (from Car.TripRollover). Roll-hoops and the body take it; a violent roll can
        /// split the fuel cell, and a split cell may catch fire, which can grow until the car explodes.
        /// </summary>
        public void Rolled(float severity)
        {
            CrashAudio.Impact(transform.position, Mathf.Clamp01(0.4f + severity), false);
            if (!Enabled) return;
            Damage(Part.Body, severity * 0.5f);
            Damage(Part.FuelCell, severity * 0.9f);
            Damage(Part.RearWing, severity * 0.6f);
            Damage(severity > 0.5f ? Part.FrontWingL : Part.FrontWingR, severity * 0.5f);
            if (severity > 0.35f && Random.value < severity * 0.65f)
            {
                fuelLeak = Mathf.Max(fuelLeak, Mathf.Lerp(0.3f, 1f, severity));
                if (Fire <= 0f) Fire = 0.05f;
            }
            UpdateHandling();
        }

        /// <summary>The fuel cell lets go: a fireball, a pressure wave, parts everywhere, and the car is done.</summary>
        public void Explode()
        {
            if (Exploded) return;
            Exploded = true;
            explodeAt = -1f;
            Vector3 c = transform.TransformPoint(car.parts != null ? car.parts.enginePoint : Vector3.up * 0.5f);
            Fx.Explosion(c, 1f);
            CrashAudio.Explosion(c);
            // Push nearby bodies, including this one (it hops and rolls a little, nothing silly).
            var cols = Physics.OverlapSphere(c, 12f, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<Rigidbody>();
            foreach (var col in cols)
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                float scale = rb == car.Body ? 0.55f : 1f;
                rb.AddExplosionForce(rb.mass * 7f * scale, c, 12f, 0.6f, ForceMode.Impulse);
            }
            for (int i = 0; i < health.Length; i++) health[i] = Mathf.Min(health[i], 0.05f);
            parts?.Char();
            parts?.Break(Part.FrontWingL, car);
            parts?.Break(Part.FrontWingR, car);
            parts?.Break(Part.RearWing, car);
            Fire = 0.8f;
            fuelLeak = 0f;
            OnExplode?.Invoke();
            OnImpact?.Invoke(1f, c);
        }

        /// <summary>For the HUD: a short report of what's broken.</summary>
        public string Summary()
        {
            var list = new List<string>();
            if (health[(int)Part.FrontWingL] < 0.6f || health[(int)Part.FrontWingR] < 0.6f) list.Add("FRONT WING");
            if (health[(int)Part.RearWing] < 0.6f) list.Add("REAR WING");
            for (int i = 0; i < 4; i++) if (health[(int)Part.SuspFL + i] < 0.6f) list.Add(new[] { "SUSP FL", "SUSP FR", "SUSP RL", "SUSP RR" }[i]);
            if (health[(int)Part.Engine] < 0.6f) list.Add("ENGINE");
            if (health[(int)Part.Gearbox] < 0.6f) list.Add("GEARBOX");
            return string.Join("  ", list);
        }
    }
}
