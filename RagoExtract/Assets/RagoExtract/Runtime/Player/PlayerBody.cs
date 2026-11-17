using UnityEngine;
using System.Collections.Generic;

namespace Rago
{
    /// <summary>Your own full ragdoll body, driven by IK from head + hands while alive, pure physics when dead.</summary>
    public class PlayerBody : MonoBehaviour
    {
        public HumanRig Rig;
        PlayerRig _p;
        float _yaw;
        Vector3 _lastRoot;

        public static PlayerBody Create(PlayerRig p)
        {
            var go = new GameObject("PlayerBody");
            DontDestroyOnLoad(go);
            var b = go.AddComponent<PlayerBody>();
            b._p = p;
            try { b.Rig = HumanRig.Build(Outfit.Operator, Profile.Current != null ? Profile.Current.outfitSeed : 7, go.transform, true); }
            catch (System.Exception e) { Debug.LogError("[Rago Extract] building your player body failed - please send this error:\n" + e); return b; }
            foreach (var c in b.Rig.colliders)
            {
                var z = c.gameObject.AddComponent<HitZone>();
                z.owner = p.Vitals;
                z.part = PartFor(c.transform, b.Rig);
                z.mult = HitZone.MultFor(z.part);
            }
            b.Rig.root.SetLayerRecursive(Layers.LocalBody);
            p.Gear.OnGearChanged += b.ApplyGear;
            b.ApplyGear();
            return b;
        }

        public static BodyPart PartFor(Transform t, HumanRig r)
        {
            if (t == r.head || t == r.neck) return BodyPart.Head;
            if (t == r.chest) return BodyPart.Thorax;
            if (t == r.spine || t == r.hips) return BodyPart.Stomach;
            if (t == r.uaL || t == r.uaR || t == r.faL || t == r.faR || t == r.handL || t == r.handR) return BodyPart.Arm;
            return BodyPart.Leg;
        }

        /// <summary>Shows the helmet and vest you're wearing on your body.</summary>
        public void ApplyGear()
        {
            if (Rig == null || _p == null || _p.Gear == null) return;
            ArmorBuilder.Dress(Rig, _p.Gear.Helmet, _p.Gear.Vest, null, true);
            foreach (var g in Rig.gearVisuals) g.SetLayerRecursive(Layers.LocalBody);
        }

        public void Snap() { _yaw = _p.Head.eulerAngles.y; _lastRoot = _p.transform.position; }

        void LateUpdate()
        {
            if (_p == null || Rig == null || Rig.root == null || Rig.ragdoll) return;
            float dt = Time.deltaTime;
            // body yaw follows the head but only turns once you look far enough away (like a real torso)
            float headYaw = _p.Head.eulerAngles.y;
            float diff = Mathf.DeltaAngle(_yaw, headYaw);
            if (Mathf.Abs(diff) > 35f || _p.MoveVelocity.sqrMagnitude > 0.3f) _yaw = Mathf.MoveTowardsAngle(_yaw, headYaw, dt * 220f);
            Vector3 root = _p.transform.position;
            Vector3 vel = (root - _lastRoot) / Mathf.Max(dt, 0.001f);
            _lastRoot = root;
            _vel = vel;
            PoseNow(dt);
        }

        Vector3 _vel;

        void PoseNow(float dt)
        {
            // arms end at the wrists of the hands you see, so arm, hand and controller line up
            Rig.Pose(_p.transform.position, _yaw, _p.Head.position, _p.Head.rotation,
                _p.HandL.WristPosition, _p.HandL.WristRotation,
                _p.HandR.WristPosition, _p.HandR.WristRotation, _vel, dt, true);
        }

        /// <summary>Called right before the frame is drawn (VR), after the hands snapped to the controllers.</summary>
        public void Repose()
        {
            if (_p == null || Rig == null || Rig.root == null || Rig.ragdoll) return;
            PoseNow(0f);
        }

        public void Die(Vector3 impulse, Vector3 point, Vector3 vel)
        {
            Rig.SetRagdoll(true, impulse, point, vel);
            Sfx.Play(Synth.BodyFall(), Rig.hips.position, 0.8f, 1f, 1f, 20f);
            Fx.BloodPool(Rig.chest.position);
        }

        public void Revive()
        {
            Rig.SetRagdoll(false);
            foreach (var b in Rig.bones) { }
            // rebuild a clean body (joints may have stretched)
            Destroy(Rig.root);
            Rig = HumanRig.Build(Outfit.Operator, Profile.Current != null ? Profile.Current.outfitSeed : 7, transform, true);
            foreach (var c in Rig.colliders)
            {
                var z = c.gameObject.AddComponent<HitZone>();
                z.owner = _p.Vitals;
                z.part = PartFor(c.transform, Rig);
                z.mult = HitZone.MultFor(z.part);
                Physics.IgnoreCollision(_p.Cc, c);
                Physics.IgnoreCollision(_p.HandL.GetComponent<Collider>(), c);
                Physics.IgnoreCollision(_p.HandR.GetComponent<Collider>(), c);
            }
            Rig.root.SetLayerRecursive(Layers.LocalBody);
            ApplyGear();
            Snap();
        }
    }

    /// <summary>Health, bleeding, stamina and damage for the local player.</summary>
    public class Vitals : MonoBehaviour, IDamageable
    {
        public PlayerRig rig;
        public float Health = 100, MaxHealth = 100;
        public float Stamina = 100, MaxStamina = 100;
        public float StimBoost;
        public float Bleed;      // hp per second
        public float LegPenalty = 1f;   // leg wounds: < 1 slows you (and below 0.7 you can't sprint)
        public float ArmInjury;         // 0..1: your hands shake (aim wobbles)
        public float GutInjury;         // 0..1: stamina drains faster
        public bool CanSprint => LegPenalty >= 0.7f;
        /// <summary>A medkit / surgery: every injury fixed.</summary>
        public void TreatInjuries() { LegPenalty = 1f; ArmInjury = 0f; GutInjury = 0f; for (int i = 0; i < PartDmg.Length; i++) PartDmg[i] *= 0.5f; }

        // ---- body map (tap your watch): damage per part and where each hit landed
        public enum Zone { Head, Thorax, Stomach, ArmL, ArmR, LegL, LegR }
        public static readonly float[] PartMax = { 35f, 85f, 60f, 45f, 45f, 60f, 60f };
        public readonly float[] PartDmg = new float[7];
        public readonly List<(int zone, Vector2 at)> Hits = new List<(int, Vector2)>();   // 'at' = -1..1 inside that part
        public float PartCondition(int z) => Mathf.Clamp01(1f - PartDmg[z] / PartMax[z]);
        int ZoneOf(HitInfo h)
        {
            var r = rig != null && rig.Body != null ? rig.Body.Rig : null;
            switch (h.part)
            {
                case BodyPart.Head: return (int)Zone.Head;
                case BodyPart.Thorax: return (int)Zone.Thorax;
                case BodyPart.Stomach: return (int)Zone.Stomach;
                case BodyPart.Arm: return r != null && (h.point - r.faL.position).sqrMagnitude < (h.point - r.faR.position).sqrMagnitude ? (int)Zone.ArmL : (int)Zone.ArmR;
                default: return r != null && (h.point - r.thL.position).sqrMagnitude < (h.point - r.thR.position).sqrMagnitude ? (int)Zone.LegL : (int)Zone.LegR;
            }
        }
        void RecordHit(HitInfo h, float dmg)
        {
            int z = ZoneOf(h);
            PartDmg[z] += dmg;
            // where on that part: across from the body's facing, up/down from a hash of the point (bones move too much to be exact)
            Vector2 at = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f));
            if (rig != null && rig.Body != null && rig.Body.Rig != null)
            {
                var bone = z == 0 ? rig.Body.Rig.head : z == 1 ? rig.Body.Rig.chest : z == 2 ? rig.Body.Rig.spine : null;
                if (bone != null) { var lp = bone.InverseTransformPoint(h.point); at.x = Mathf.Clamp(lp.x / 0.18f, -0.85f, 0.85f); }
            }
            Hits.Add((z, at));
            if (Hits.Count > 40) Hits.RemoveAt(0);
        }
        public bool God;
        float _hot, _hotRate, _dripT, _regenDelay, _clotT = 5f;
        public bool Alive => Health > 0;
        public int ActorId => 0;
        public static System.Action<HitInfo> OnLocalDamaged;
        public static System.Action<int> OnLocalDeath; // killer actor id
        public static HitInfo LastKillHit;      // the hit that killed you (who, with what, where)
        public static string LastDeathReason;   // no killer: "Bled out", "Drowned", raid time ran out...

        public void ResetFull() { System.Array.Clear(PartDmg, 0, PartDmg.Length); Hits.Clear(); Health = MaxHealth; Stamina = MaxStamina; Bleed = 0; LegPenalty = 1; ArmInjury = 0; GutInjury = 0; _hot = 0; StimBoost = 0; }

        public void Heal(float hp) { Health = Mathf.Min(MaxHealth, Health + hp); float k = Mathf.Clamp01(hp / MaxHealth); for (int i = 0; i < PartDmg.Length; i++) PartDmg[i] = Mathf.Max(0f, PartDmg[i] - PartMax[i] * k); }
        public void HealOverTime(float hp, float secs) { _hot += hp; _hotRate = hp / Mathf.Max(0.1f, secs); }
        static bool Pressing(Hand h)
        {
            if (h == null || h.Held != null || !h.Input.GripHeld || h.rig == null || h.rig.Body == null || h.rig.Body.Rig == null) return false;
            return Vector3.Distance(h.TrackedPosition, h.rig.Body.Rig.chest.position) < 0.28f;
        }

        public void StopBleeding() { Bleed = 0; LegPenalty = Mathf.Max(LegPenalty, 0.85f); rig.Toast("Bleeding stopped"); }

        public void TakeHit(HitInfo h, HitZone zone)
        {
            if (!Alive || God) return;
            if (RagoGame.I != null && RagoGame.I.SafeZone) return;
            float dmg = h.damage;
            // armour you're wearing (found in raids)
            dmg *= ArmorDef.Reduction(h.part, rig.Gear.Helmet, rig.Gear.Vest, h.gunId, h.ammo);
            Health -= dmg;
            RecordHit(h, dmg);
            rig.Hud.Damage(Mathf.Clamp01(dmg / 40f), h.dir);
            if (dmg > 12 && Random.value < (h.part == BodyPart.Thorax ? 0.7f : 0.45f)) Bleed = Mathf.Min(4f, Bleed + (h.part == BodyPart.Thorax ? 0.9f : 0.6f));
            // where you're hit matters
            if (dmg > 3f && Health > 0f)
                switch (h.part)
                {
                    case BodyPart.Leg:
                        bool wasOk = LegPenalty >= 0.7f;
                        LegPenalty = Mathf.Max(0.45f, LegPenalty - 0.2f);
                        if (wasOk && LegPenalty < 0.7f) rig.Toast("LEG HIT - you're limping and can't sprint (bandage or medkit)");
                        break;
                    case BodyPart.Arm:
                        if (ArmInjury <= 0f) rig.Toast("ARM HIT - your aim shakes (medkit)");
                        ArmInjury = Mathf.Min(1f, ArmInjury + 0.4f);
                        break;
                    case BodyPart.Stomach:
                        if (GutInjury <= 0f) rig.Toast("GUT SHOT - you tire much faster (medkit)");
                        GutInjury = Mathf.Min(1f, GutInjury + 0.5f);
                        break;
                    case BodyPart.Head:
                        rig.Hud.Suppress(1f);
                        Sfx.UI(Synth.Ringing(), 0.35f);
                        break;
                }
            if (h.ammo == AmmoType.Incendiary && Health > 0) Ignite(h);
            OnLocalDamaged?.Invoke(h);
            if (Health <= 0)
            {
                Health = 0;
                LastKillHit = h; LastDeathReason = null;
                rig.OnDeath(h.dir * Mathf.Max(30f, h.force * 25f), h.point);
                OnLocalDeath?.Invoke(h.attacker);
            }
        }

        /// <summary>An incendiary round set you alight where it hit: it burns (and makes you bleed) until it goes out or
        /// you pat it out with your other hand.</summary>
        void Ignite(HitInfo h)
        {
            var r = rig.Body != null ? rig.Body.Rig : null;
            if (r == null) return;
            Transform bone; Hand notThis = null;
            switch (h.part)
            {
                case BodyPart.Arm:
                    bool left = (h.point - r.faL.position).sqrMagnitude < (h.point - r.faR.position).sqrMagnitude;
                    bone = left ? r.faL : r.faR; notThis = left ? rig.HandL : rig.HandR; break;
                case BodyPart.Leg: bone = (h.point - r.thL.position).sqrMagnitude < (h.point - r.thR.position).sqrMagnitude ? r.thL : r.thR; break;
                case BodyPart.Head: bone = r.chest; break;   // the collar - somewhere you can reach
                case BodyPart.Stomach: bone = r.spine; break;
                default: bone = r.chest; break;
            }
            bool first = !Burning.AnyOn(r.hips != null ? r.hips.root : rig.transform);
            Bleed = Mathf.Min(4f, Bleed + 0.4f);
            var hit = h;
            Burning.Ignite(bone, 7f, dt =>
            {
                if (!Alive || God) return;
                Health -= 4f * dt * 2f;   // 8 a second
                rig.Hud.Damage(0.25f, Vector3.up);
                if (Health <= 0f)
                {
                    Health = 0; LastKillHit = hit; LastDeathReason = null;
                    rig.OnDeath(Vector3.up * 10f, rig.Body.Rig.chest.position);
                    OnLocalDeath?.Invoke(hit.attacker);
                }
            }, true, notThis);
            if (first) rig.Toast("YOU'RE ON FIRE - pat the flames out with your other hand!");
        }

        public bool OnFire => rig != null && rig.Body != null && rig.Body.Rig != null && Burning.AnyOn(rig.Body.Rig.hips.root);

        public void Kill(string reason)
        {
            if (!Alive || God) return;   // god mode (tester): nothing kills you
            Health = 0;
            LastDeathReason = reason;
            rig.OnDeath(Vector3.zero, rig.Head.position);
            rig.Toast(reason);
            OnLocalDeath?.Invoke(-1);
        }

        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (_hot > 0) { float k = Mathf.Min(_hot, _hotRate * dt); _hot -= k; Heal(k); }
            if (Bleed > 0)
            {
                // pressing an empty hand on your chest (grip) slows the bleeding and helps it clot
                bool pressure = Pressing(rig.HandL) || Pressing(rig.HandR);
                Health -= Bleed * dt * (pressure ? 0.5f : 1f);
                // without meds it's a gamble: every few seconds a wound may clot on its own - light bleeds often do,
                // heavy ones rarely; resting helps, sprinting doesn't
                _clotT -= dt;
                if (_clotT <= 0f)
                {
                    _clotT = 5f;
                    float chance = Bleed <= 0.61f ? 0.22f : Bleed <= 1.21f ? 0.12f : Bleed <= 2.41f ? 0.06f : 0.03f;
                    if (pressure) chance *= 3f;
                    if (rig.Sprinting) chance *= 0.4f; else if (rig.MoveVelocity.sqrMagnitude < 0.2f) chance *= 1.5f;
                    if (Random.value < chance)
                    {
                        Bleed = Mathf.Max(0f, Bleed - 0.6f);
                        rig.Toast(Bleed <= 0f ? "Your bleeding stopped on its own" : "A wound clotted - still bleeding");
                    }
                }
                _dripT -= dt;
                if (_dripT <= 0) { _dripT = Random.Range(0.3f, 0.8f) / Bleed; Fx.Drip(rig.Body.Rig.spine.position + Random.insideUnitSphere * 0.1f); }
                if (God) { Health = MaxHealth; Bleed = 0; }   // tester god mode
                if (Health <= 0) { Health = 0; LastDeathReason = "Bled out"; rig.OnDeath(Vector3.zero, rig.Head.position); OnLocalDeath?.Invoke(-1); }
            }
            if (!rig.Sprinting) Stamina = Mathf.Min(MaxStamina, Stamina + dt * (StimBoost > 0 ? 30f : 15f));
            StimBoost = Mathf.Max(0, StimBoost - dt);
            Sfx.Heartbeat(Health < 35 ? 1f - Health / 35f : 0f);
        }
    }
}
