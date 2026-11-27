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
            try { b.Rig = HumanRig.Build(Outfit.Operator, Profile.Current != null ? Profile.Current.outfitSeed : 7, go.transform, true, Cosmetics.Mine); }
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
            ArmorBuilder.Dress(Rig, _p.Gear.Helmet, _p.Gear.Vest, null, true, _p.Gear.NvgWorn ? (_p.Gear.NvgOn ? 2 : 1) : 0, _p.Gear.CamCode);
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

        // (3.52) your own body hides from your eyes when it would be in the way: your legs while you're crouched (kneeling or
        // squatting for real, your thighs and knees came up right in front of your face), and your torso and legs while you
        // look down at the ground (it filled the bottom of your view). They still cast their
        // shadow and other players see them as usual; your arms and hands always stay. SETTINGS > MORE can keep it shown.
        Renderer[] _torsoR, _legR; float _scanT; HumanRig _scanned; bool _torsoHidden, _legsHidden, _lookingDown;
        void LegsInView()
        {
            if (Rig == null) return;
            if (_torsoR == null || _scanned != Rig || Time.time > _scanT)
            {
                _scanned = Rig; _scanT = Time.time + 0.5f;   // armour put on / taken off: pick it up again soon
                var torso = new System.Collections.Generic.List<Renderer>(); var legs = new System.Collections.Generic.List<Renderer>();
                if (Rig.hips != null)
                    foreach (var r in Rig.hips.GetComponentsInChildren<Renderer>(true))
                    {
                        var t = r.transform;
                        if (t.name != "Mesh") continue;   // only the body itself: your worn vest / cam are already hidden from your eyes (and must stay so)
                        if ((Rig.uaL != null && t.IsChildOf(Rig.uaL)) || (Rig.uaR != null && t.IsChildOf(Rig.uaR)) || (Rig.neck != null && t.IsChildOf(Rig.neck))) continue;
                        if ((Rig.thL != null && t.IsChildOf(Rig.thL)) || (Rig.thR != null && t.IsChildOf(Rig.thR))) legs.Add(r); else torso.Add(r);
                    }
                _torsoR = torso.ToArray(); _legR = legs.ToArray(); _torsoHidden = _legsHidden = false;
            }
            float headH = _p.Head.position.y - _p.transform.position.y;
            // looking more than ~42 degrees below the horizon (a little hysteresis so it doesn't flicker at the edge)
            float below = Vector3.Angle(_p.Head.forward, Vector3.down);
            _lookingDown = _lookingDown ? below < 52f : below < 48f;
            bool lookHide = _lookingDown && _p.Driving == null && !(Profile.Current != null && Profile.Current.bodyLookDown);
            bool legsHide = lookHide || (_p.Driving == null && (_p.Crouched || headH < 1.2f));
            Set(_torsoR, lookHide, ref _torsoHidden);
            Set(_legR, legsHide, ref _legsHidden);
        }

        static void Set(Renderer[] rs, bool hide, ref bool state)
        {
            if (rs == null) return;
            // (re-applied when the state changes, and every scan: armour put on while hidden picks up the state too)
            if (hide == state && Time.frameCount % 15 != 0) return;
            state = hide;
            var mode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in rs) if (r != null && r.shadowCastingMode != mode) r.shadowCastingMode = mode;
        }

        void PoseNow(float dt)
        {
            LegsInView();
            // arms end at the wrists of the hands you see, so arm, hand and controller line up
            // riding: the body sits on the seat (the rig itself is placed for your real head height, standing or sitting)
            Rig.Pose(_p.SeatPoint, _yaw, _p.Head.position, _p.Head.rotation,
                _p.HandL.WristPosition, _p.HandL.WristRotation,
                _p.HandR.WristPosition, _p.HandR.WristRotation, _p.Driving != null ? Vector3.zero : _vel, dt, true, _p.Driving != null, _p.Driving != null ? _p.Driving.SeatFoot(_p.Passenger) : (Vector3?)null);
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

        /// <summary>Thrown as a ragdoll (cart, grenade) - your view rides in its head, which stays hidden from you.</summary>
        public void Knock(Vector3 launch)
        {
            Rig.SetRagdoll(true, Vector3.zero, Rig.chest.position, launch);
            foreach (var rr in Rig.headRenderers) if (rr != null) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            Sfx.Play(Synth.BodyFall(), Rig.hips.position, 0.7f, 1f, 1f, 20f);
        }

        public void Revive()
        {
            Rig.SetRagdoll(false);
            foreach (var b in Rig.bones) { }
            // rebuild a clean body (joints may have stretched)
            Destroy(Rig.root);
            Rig = HumanRig.Build(Outfit.Operator, Profile.Current != null ? Profile.Current.outfitSeed : 7, transform, true, Cosmetics.Mine);
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
        public float Focus;      // DUBBY energy drink: seconds of steadier aim (less recoil)
        public float Bleed;      // hp per second
        public float LegPenalty = 1f;   // leg wounds: < 1 slows you (and below 0.7 you can't sprint) - only a syringe on the leg fixes it
        public float ArmInjury;         // 0..1: your hands shake (aim wobbles) - a broken arm shakes until a syringe sets it
        /// <summary>Limbs: broken (hit hard, again and again, in the same arm or leg) and shot legs. Only a syringe (stim)
        /// pressed against that limb fixes them - bandages and medkits don't.</summary>
        public readonly bool[] Broken = new bool[7];
        public readonly bool[] LegShot = new bool[7];
        static bool IsLimb(int z) => z >= (int)Zone.ArmL;
        static string LimbName(int z) => z == (int)Zone.ArmL ? "LEFT ARM" : z == (int)Zone.ArmR ? "RIGHT ARM" : z == (int)Zone.LegL ? "LEFT LEG" : z == (int)Zone.LegR ? "RIGHT LEG" : "";
        void RecomputeLimbs()
        {
            bool legBroken = Broken[(int)Zone.LegL] || Broken[(int)Zone.LegR];
            bool legShot = LegShot[(int)Zone.LegL] || LegShot[(int)Zone.LegR];
            LegPenalty = legBroken ? 0.45f : legShot ? 0.65f : 1f;
            if (Broken[(int)Zone.ArmL] || Broken[(int)Zone.ArmR]) ArmInjury = 1f;
        }

        /// <summary>A syringe pressed against a hurt arm or leg: sets the break / fixes the shot leg. True if it did.</summary>
        public bool TreatLimb(Vector3 at)
        {
            var r = rig != null && rig.Body != null ? rig.Body.Rig : null;
            if (r == null) return false;
            int best = -1; float bd = 0.3f * 0.3f;
            for (int z = (int)Zone.ArmL; z <= (int)Zone.LegR; z++)
            {
                if (!Broken[z] && !LegShot[z]) continue;
                Vector3 p = z == (int)Zone.ArmL ? r.faL.position : z == (int)Zone.ArmR ? r.faR.position
                          : z == (int)Zone.LegL ? (r.thL.position + r.shL.position) * 0.5f : (r.thR.position + r.shR.position) * 0.5f;
                Vector3 p2 = z == (int)Zone.ArmL ? r.uaL.position : z == (int)Zone.ArmR ? r.uaR.position : z == (int)Zone.LegL ? r.shL.position : r.shR.position;
                float d = Mathf.Min((p - at).sqrMagnitude, (p2 - at).sqrMagnitude);
                if (d < bd) { bd = d; best = z; }
            }
            if (best < 0) return false;
            bool was = Broken[best];
            Broken[best] = false; LegShot[best] = false; PartDmg[best] = 0f;
            if (best == (int)Zone.ArmL || best == (int)Zone.ArmR) ArmInjury = 0f;
            RecomputeLimbs();
            rig.Toast(LimbName(best) + (was ? " set - not broken any more" : " fixed - no more limp"));
            return true;
        }
        public float GutInjury;         // 0..1: stamina drains faster
        public bool CanSprint => LegPenalty >= 0.7f;
        /// <summary>A medkit / surgery: every injury fixed.</summary>
        public void TreatInjuries() { GutInjury = 0f; ArmInjury = 0f; for (int i = 0; i < PartDmg.Length; i++) if (!Broken[i]) PartDmg[i] *= 0.5f; RecomputeLimbs(); }   // a medkit: not broken limbs or shot legs (that's the syringe)

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
            if (IsLimb(z) && !Broken[z] && PartDmg[z] >= PartMax[z] * 0.75f && Health - 0f > 0f)
            {
                Broken[z] = true; RecomputeLimbs();
                rig.Toast(LimbName(z) + " BROKEN - hold a syringe (stim) to it and pull the trigger");
                Sfx.UI(Synth.BodyFall(), 0.3f);
            }
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

        public void ResetFull() { System.Array.Clear(Broken, 0, Broken.Length); System.Array.Clear(LegShot, 0, LegShot.Length); System.Array.Clear(PartDmg, 0, PartDmg.Length); Hits.Clear(); Health = MaxHealth; Stamina = MaxStamina; Bleed = 0; LegPenalty = 1; ArmInjury = 0; GutInjury = 0; _hot = 0; StimBoost = 0; Focus = 0; }

        public void Heal(float hp) { Health = Mathf.Min(MaxHealth, Health + hp); float k = Mathf.Clamp01(hp / MaxHealth); for (int i = 0; i < PartDmg.Length; i++) PartDmg[i] = Mathf.Max(0f, PartDmg[i] - PartMax[i] * k); }
        public void HealOverTime(float hp, float secs) { _hot += hp; _hotRate = hp / Mathf.Max(0.1f, secs); }
        static bool Pressing(Hand h)
        {
            if (h == null || h.Held != null || !h.Input.GripHeld || h.rig == null || h.rig.Body == null || h.rig.Body.Rig == null) return false;
            return Vector3.Distance(h.TrackedPosition, h.rig.Body.Rig.chest.position) < 0.28f;
        }

        public void StopBleeding() { Bleed = 0; rig.Toast("Bleeding stopped"); }

        public void TakeHit(HitInfo h, HitZone zone)
        {
            if (!Alive || God) return;
            if (RagoGame.I != null && RagoGame.I.SafeZone) return;
            float dmg = h.damage;
            // armour you're wearing (found in raids)
            dmg *= ArmorDef.Reduction(h.part, rig.Gear.Helmet, rig.Gear.Vest, h.gunId, h.ammo);
            Health -= dmg;
            RecordHit(h, dmg);
            // run over by a cart or caught in a grenade blast (and still alive): you're thrown back as a ragdoll for a moment
            if (Health > 0f && (h.gunId == "cart" || h.gunId == "frag") && h.force >= 1.5f)
            {
                Vector3 away = h.dir.sqrMagnitude > 0.01f ? h.dir.normalized : -rig.Head.forward;
                away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : -rig.transform.forward;
                float speed = h.gunId == "cart" ? Mathf.Clamp(h.force * 0.65f, 3f, 9f) : Mathf.Clamp(2f + h.force * 1.1f, 3f, 9f);
                rig.Knockdown(away * speed + Vector3.up * (1.8f + speed * 0.25f));
            }
            rig.Hud.Damage(Mathf.Clamp01(dmg / 40f), h.dir);
            if (dmg > 12 && Random.value < (h.part == BodyPart.Thorax ? 0.7f : 0.45f)) Bleed = Mathf.Min(4f, Bleed + (h.part == BodyPart.Thorax ? 0.9f : 0.6f));
            // where you're hit matters
            if (dmg > 3f && Health > 0f)
                switch (h.part)
                {
                    case BodyPart.Leg:
                    {
                        int lz = ZoneOf(h);
                        if (!LegShot[lz] && !Broken[lz]) rig.Toast(LimbName(lz) + " SHOT - you're limping and can't sprint (a syringe on that leg fixes it)");
                        LegShot[lz] = true; RecomputeLimbs();
                        break;
                    }
                    case BodyPart.Arm:
                        if (ArmInjury <= 0f) rig.Toast("ARM HIT - your aim shakes (medkit; hit it more and it breaks)");
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
                // pressing an empty hand on your chest (grip) slows the bleeding - but it never stops on its own any more:
                // only a bandage or a medkit (IFAK, trauma kit) stops it
                bool pressure = Pressing(rig.HandL) || Pressing(rig.HandR);
                Health -= Bleed * dt * (pressure ? 0.5f : 1f);
                _clotT -= dt;
                if (_clotT <= 0f) { _clotT = 25f; rig.Toast("BLEEDING - use a bandage or a medkit"); }
                _dripT -= dt;
                if (_dripT <= 0) { _dripT = Random.Range(0.3f, 0.8f) / Bleed; Fx.Drip(rig.Body.Rig.spine.position + Random.insideUnitSphere * 0.1f); }
                if (God) { Health = MaxHealth; Bleed = 0; }   // tester god mode
                if (Health <= 0) { Health = 0; LastDeathReason = "Bled out"; rig.OnDeath(Vector3.zero, rig.Head.position); OnLocalDeath?.Invoke(-1); }
            }
            if (!rig.Sprinting) Stamina = Mathf.Min(MaxStamina, Stamina + dt * (StimBoost > 0 ? 30f : 15f));
            StimBoost = Mathf.Max(0, StimBoost - dt);
            if (Focus > 0f) { Focus -= dt; if (Focus <= 0f) { Focus = 0f; rig.Toast("FOCUS wore off"); } }
            Sfx.Heartbeat(Health < 35 ? 1f - Health / 35f : 0f);
        }
    }
}
