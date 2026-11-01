using UnityEngine;

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
        public float LegPenalty = 1f;
        public bool God;
        float _hot, _hotRate, _dripT, _regenDelay;
        public bool Alive => Health > 0;
        public int ActorId => 0;
        public static System.Action<HitInfo> OnLocalDamaged;
        public static System.Action<int> OnLocalDeath; // killer actor id

        public void ResetFull() { Health = MaxHealth; Stamina = MaxStamina; Bleed = 0; LegPenalty = 1; _hot = 0; StimBoost = 0; }

        public void Heal(float hp) { Health = Mathf.Min(MaxHealth, Health + hp); }
        public void HealOverTime(float hp, float secs) { _hot += hp; _hotRate = hp / Mathf.Max(0.1f, secs); }
        public void StopBleeding() { Bleed = 0; LegPenalty = Mathf.Max(LegPenalty, 0.85f); rig.Toast("Bleeding stopped"); }

        public void TakeHit(HitInfo h, HitZone zone)
        {
            if (!Alive || God) return;
            if (RagoGame.I != null && RagoGame.I.SafeZone) return;
            float dmg = h.damage;
            // armour you're wearing (found in raids)
            dmg *= ArmorDef.Reduction(h.part, rig.Gear.Helmet, rig.Gear.Vest);
            Health -= dmg;
            rig.Hud.Damage(Mathf.Clamp01(dmg / 40f), h.dir);
            if (dmg > 12 && Random.value < 0.45f) Bleed = Mathf.Min(4f, Bleed + 0.6f);
            if (h.part == BodyPart.Leg) LegPenalty = Mathf.Max(0.6f, LegPenalty - 0.15f);
            OnLocalDamaged?.Invoke(h);
            if (Health <= 0)
            {
                Health = 0;
                rig.OnDeath(h.dir * Mathf.Max(30f, h.force * 25f), h.point);
                OnLocalDeath?.Invoke(h.attacker);
            }
        }

        public void Kill(string reason)
        {
            if (!Alive) return;
            Health = 0;
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
                Health -= Bleed * dt;
                _dripT -= dt;
                if (_dripT <= 0) { _dripT = Random.Range(0.3f, 0.8f) / Bleed; Fx.Drip(rig.Body.Rig.spine.position + Random.insideUnitSphere * 0.1f); }
                if (Health <= 0) { Health = 0; rig.OnDeath(Vector3.zero, rig.Head.position); OnLocalDeath?.Invoke(-1); }
            }
            if (!rig.Sprinting) Stamina = Mathf.Min(MaxStamina, Stamina + dt * (StimBoost > 0 ? 30f : 12f));
            StimBoost = Mathf.Max(0, StimBoost - dt);
            Sfx.Heartbeat(Health < 35 ? 1f - Health / 35f : 0f);
        }
    }
}
