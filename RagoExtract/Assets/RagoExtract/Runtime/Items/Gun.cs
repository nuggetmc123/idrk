using UnityEngine;

namespace Rago
{
    /// <summary>
    /// VR firearm: physical magazines, chambering, slide/charging-handle racking (grab it with your other hand),
    /// pump and bolt actions, fire modes, recoil into the physics hands, holo-sight rail.
    /// Controls (hand holding the gun): Trigger = fire, A/X = drop magazine,
    /// B/Y = rack / release slide (or switch fire mode when a round is chambered).
    /// </summary>
    public class Gun : Item
    {
        public GunDef g;
        public GunRig rig;
        public Magazine mag;
        public SightItem sight;
        public bool chambered;
        public bool slideLocked;
        public bool needsCycle;
        public int internalRounds;
        public bool autoMode = true;
        float _nextShot, _slideAnim, _pump, _rackPull;
        int _burstLeft;
        bool _pumpedBack, _rackingBack, _hasSpent;
        Hand _rackHand;
        Vector3 _rackStart;
        public static System.Action<Gun, Vector3, Vector3> OnLocalFire; // netcode hook

        public bool Internal => rig.magStyle == MagStyle.ShellCaddy || rig.magStyle == MagStyle.Speedloader || rig.magStyle == MagStyle.Clip;
        public int Rounds => Internal ? internalRounds : mag != null ? mag.rounds : 0;
        public int Capacity => g.magSize;
        public override string Label => g.name + (sight != null ? " + " + sight.def.name : "");
        public override int Value => g.price + (sight != null ? sight.def.price : 0);

        public static Gun Create(GunDef def, ItemData data, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(def.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var gun = go.AddComponent<Gun>();
            gun.def = def; gun.g = def;
            gun.rig = GunBuilder.Build(def, go.transform);
            gun.grip = new GameObject("Grip").transform;
            gun.grip.SetParent(go.transform, false);
            gun.secondGrip = gun.rig.foregrip;
            var b = gun.rig.bounds;
            gun.localBounds = b;
            // colliders: receiver/barrel box + grip box
            var main = go.AddComponent<BoxCollider>();
            main.center = new Vector3(0, Mathf.Max(b.center.y, gun.rig.boreY * 0.7f), b.center.z);
            main.size = new Vector3(Mathf.Max(0.03f, b.size.x), Mathf.Max(0.05f, b.size.y * 0.55f), b.size.z * 0.96f);
            var gripCol = go.AddComponent<BoxCollider>();
            gripCol.center = new Vector3(0, -0.035f, -0.01f);
            gripCol.size = new Vector3(0.03f, 0.08f, 0.05f);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            float mass = def.TwoHanded ? Mathf.Lerp(2.5f, 9f, Mathf.InverseLerp(0.2f, 0.85f, def.barrel)) : 1.1f;
            if (def.cls == GunClass.LMG) mass = 9f;
            gun.SetupPhysics(mass);
            gun.Load(data ?? new ItemData(def.id));
            return gun;
        }

        public override void Load(ItemData d)
        {
            int r = d.rounds < 0 ? g.magSize : Mathf.Clamp(d.rounds, 0, g.magSize);
            if (Internal) internalRounds = r;
            else
            {
                var m = Magazine.Create(this, r, transform.position, transform.rotation);
                InsertMag(m, false);
            }
            chambered = d.chambered && (r > 0 || d.rounds < 0);
            if (chambered && Internal && internalRounds > 0 && g.action != FireAction.Revolver && g.action != FireAction.Break) internalRounds--;
            else if (chambered && !Internal && mag != null && mag.rounds > 0) mag.rounds--;
            if (!string.IsNullOrEmpty(d.sight))
            {
                var sd = Db.Get<SightDef>(d.sight);
                if (sd != null && rig.rail != null)
                {
                    var s = SightItem.Create(sd, transform.position, transform.rotation);
                    s.AttachTo(this, false);
                }
            }
            autoMode = g.action == FireAction.Auto || g.action == FireAction.Burst;
        }

        public override ItemData Save()
        {
            int r = Rounds;
            return new ItemData(g.id) { rounds = r, chambered = chambered, sight = sight != null ? sight.def.id : "" };
        }

        // ------------------------------------------------------------ magazines
        public bool Accepts(Magazine m) => m != null && m.cal == g.cal && (Internal || m.style == rig.magStyle) && (Internal || mag == null);

        public void InsertMag(Magazine m, bool sound = true)
        {
            if (Internal)
            {
                int take = Mathf.Min(m.rounds, g.magSize - internalRounds);
                internalRounds += take;
                m.rounds -= take;
                if (sound) Sfx.Play(Synth.MagIn(), transform.position, 0.7f, 1.15f, 0.5f, 10f);
                if (!chambered && (g.action == FireAction.Revolver || g.action == FireAction.Break) && internalRounds > 0) { chambered = true; internalRounds--; }
                if (m.rounds <= 0) { m.primaryHand?.ForceRelease(); Destroy(m.gameObject); }
                return;
            }
            mag = m;
            m.inGun = this;
            if (m.primaryHand != null) m.primaryHand.ForceRelease(true);
            m.rb.isKinematic = true;
            m.SetCollidersTrigger(true);
            m.transform.SetParent(rig.magwell, false);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            if (sound)
            {
                Sfx.Play(Synth.MagIn(), transform.position, 0.8f, 1f, 0.5f, 10f);
                primaryHand?.Haptic(0.5f, 0.05f);
            }
        }

        public void EjectMag()
        {
            if (mag == null || Internal) return;
            var m = mag;
            mag = null;
            m.inGun = null;
            m.transform.SetParent(RagoGame.I != null ? RagoGame.I.MapRoot : null, true);
            m.rb.isKinematic = false;
            m.SetCollidersTrigger(false);
            m.rb.SetVel(primaryHand != null ? primaryHand.Velocity + transform.TransformDirection(Vector3.down) * 0.8f : Vector3.down);
            Compat.IgnoreAll(m.gameObject, gameObject);
            Sfx.Play(Synth.MagOut(), transform.position, 0.8f, 1f, 0.5f, 10f);
            primaryHand?.Haptic(0.3f, 0.04f);
            m.MarkDropped();
        }

        // ------------------------------------------------------------ actions
        void Chamber()
        {
            if (Internal)
            {
                if (internalRounds > 0) { internalRounds--; chambered = true; }
            }
            else if (mag != null && mag.rounds > 0) { mag.rounds--; chambered = true; }
            slideLocked = !chambered && !Internal && (g.action == FireAction.Semi || g.action == FireAction.Auto || g.action == FireAction.Burst) && mag != null && mag.rounds == 0;
        }

        public void Rack()
        {
            if (chambered) { chambered = false; Fx.EjectCasing(rig.ejectPort, Vel(), g.cal == Caliber.C12g, g.cal == Caliber.C50BMG); } // eject live round
            else if (_hasSpent) { _hasSpent = false; Fx.EjectCasing(rig.ejectPort, Vel(), g.cal == Caliber.C12g, g.cal == Caliber.C50BMG); }
            needsCycle = false;
            slideLocked = false;
            Chamber();
            _slideAnim = 1f;
            var clip = g.action == FireAction.Pump ? Synth.Pump() : g.action == FireAction.Bolt ? Synth.BoltCycle() : Synth.SlideForward();
            Sfx.Play(clip, transform.position, 0.8f, 1f, 0.5f, 12f);
            primaryHand?.Haptic(0.35f, 0.06f);
        }

        Vector3 Vel() => primaryHand != null ? primaryHand.Velocity : Vector3.zero;

        bool CanFire => chambered && !needsCycle && !slideLocked;

        void Fire()
        {
            chambered = false;
            _hasSpent = true;
            Vector3 dir = rig.muzzle.forward;
            float spr = g.pellets > 1 ? 0f : g.spread * 0.12f;
            dir = Quaternion.Euler(Random.Range(-spr, spr), Random.Range(-spr, spr), 0) * dir;
            bool tracer = g.cls == GunClass.LMG || (g.TwoHanded && Random.value < 0.25f);
            Ballistics.Fire(rig.muzzle.position, dir, g, 0, transform, RagoGame.I.Player.Vitals, false, tracer);
            Fx.MuzzleFlash(rig.muzzle, g.TwoHanded ? (g.cal == Caliber.C50BMG ? 2f : 1.2f) : 0.7f, g.suppressed);
            Sfx.Gunshot(g, rig.muzzle.position, true);
            OnLocalFire?.Invoke(this, rig.muzzle.position, dir);
            // recoil
            float rec = g.recoil;
            bool twoHand = secondaryHand != null;
            if (twoHand) rec *= 0.45f;
            if (g.TwoHanded && !twoHand) rec *= 1.6f;
            if (rig.butt != null && RagoGame.I.Player.ShoulderNear(rig.butt.position)) rec *= 0.6f;
            primaryHand.AddRecoil(rec, g.TwoHanded);
            secondaryHand?.AddRecoil(rec * 0.6f, g.TwoHanded);
            primaryHand.Haptic(Mathf.Clamp01(0.4f + g.recoil * 0.3f), 0.06f);
            secondaryHand?.Haptic(Mathf.Clamp01(0.3f + g.recoil * 0.2f), 0.05f);
            _slideAnim = 1f;
            switch (g.action)
            {
                case FireAction.Bolt:
                case FireAction.Pump:
                    needsCycle = true; break;
                case FireAction.Break:
                case FireAction.Revolver:
                    _hasSpent = false; // casings stay in the cylinder / barrel
                    if (internalRounds > 0) { internalRounds--; chambered = true; }
                    break;
                default:
                    Fx.EjectCasing(rig.ejectPort, Vel(), g.cal == Caliber.C12g, g.cal == Caliber.C50BMG || g.cal == Caliber.C338);
                    _hasSpent = false;
                    Chamber();
                    break;
            }
        }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            if (h != primaryHand) return;
            // trigger (the Market is a safe zone: weapons are cold)
            if (RagoGame.I != null && RagoGame.I.SafeZone)
            {
                if (input.TriggerDown) { Sfx.Play(Synth.DryFire(), transform.position, 0.5f, 1f, 0.5f, 6f); RagoGame.I.Player.Toast("Safe zone - weapons cold"); }
                if (input.PrimaryDown) EjectMag();
                return;
            }
            if (input.TriggerDown)
            {
                if (CanFire)
                {
                    if (g.action == FireAction.Burst && autoMode) _burstLeft = 3;
                    else if (Time.time >= _nextShot) { Fire(); _nextShot = Time.time + 60f / Mathf.Max(30, g.rpm); }
                }
                else Sfx.Play(Synth.DryFire(), transform.position, 0.6f, 1f, 0.5f, 8f);
            }
            if (input.Trigger > 0.7f && autoMode && g.action == FireAction.Auto && CanFire && Time.time >= _nextShot)
            {
                Fire(); _nextShot = Time.time + 60f / g.rpm;
            }
            if (_burstLeft > 0 && Time.time >= _nextShot)
            {
                if (CanFire) { Fire(); _burstLeft--; _nextShot = Time.time + 60f / g.rpm; if (_burstLeft == 0) _nextShot += 0.12f; }
                else _burstLeft = 0;
            }
            // mag release
            if (input.PrimaryDown) EjectMag();
            // contextual secondary: fire mode or rack/release
            if (input.SecondaryDown)
            {
                if (chambered && !needsCycle && (g.action == FireAction.Auto || g.action == FireAction.Burst))
                {
                    autoMode = !autoMode;
                    Sfx.Play(Synth.DryFire(), transform.position, 0.4f, 1.6f, 0.5f, 5f);
                    h.Haptic(0.2f, 0.03f);
                    RagoGame.I.Player.Toast(autoMode ? (g.action == FireAction.Burst ? "BURST" : "AUTO") : "SEMI");
                }
                else Rack();
            }
        }

        /// <summary>Support-hand interactions: pump, slide/charging handle pull, magazine insertion.</summary>
        public void SupportUpdate(Hand h)
        {
            if (rig == null) return;
            // pump action: drag the forend back and forth
            if (g.action == FireAction.Pump && rig.pump != null && h == secondaryHand)
            {
                Vector3 local = transform.InverseTransformPoint(h.TrackedPosition);
                float back = Mathf.Clamp(rig.foregrip.localPosition.z - local.z, 0, -rig.pumpTravel.z);
                _pump = back;
                if (back > -rig.pumpTravel.z * 0.8f && !_pumpedBack)
                {
                    _pumpedBack = true;
                    if (_hasSpent || chambered) { Fx.EjectCasing(rig.ejectPort, Vel(), true, false); _hasSpent = false; chambered = false; }
                    Sfx.Play(Synth.SlideBack(), transform.position, 0.6f, 1f, 0.5f, 10f);
                    h.Haptic(0.3f, 0.04f);
                }
                if (back < 0.01f && _pumpedBack)
                {
                    _pumpedBack = false;
                    needsCycle = false;
                    Chamber();
                    Sfx.Play(Synth.Pump(), transform.position, 0.7f, 1.1f, 0.5f, 10f);
                    h.Haptic(0.5f, 0.05f);
                }
            }
        }

        public bool TryStartRack(Hand h)
        {
            if (rig.slide == null || h == primaryHand) return false;
            if (Vector3.Distance(h.Anchor.position, rig.slide.position) > 0.07f) return false;
            _rackHand = h;
            _rackStart = transform.InverseTransformPoint(h.TrackedPosition);
            _rackingBack = false;
            Sfx.Play(Synth.Grab(0), transform.position, 0.3f, 1.3f, 0.5f, 5f);
            return true;
        }

        public void RackUpdate(Hand h, bool stillGripping)
        {
            if (_rackHand != h) return;
            Vector3 local = transform.InverseTransformPoint(h.TrackedPosition);
            float travel = -rig.slideTravel.z;
            _rackPull = Mathf.Clamp(_rackStart.z - local.z, 0, travel);
            if (_rackPull > travel * 0.85f && !_rackingBack)
            {
                _rackingBack = true;
                Sfx.Play(Synth.SlideBack(), transform.position, 0.7f, 1f, 0.5f, 10f);
                h.Haptic(0.3f, 0.04f);
            }
            if (!stillGripping || (_rackingBack && _rackPull < travel * 0.2f))
            {
                if (_rackingBack) Rack();
                _rackHand = null;
                _rackPull = 0;
            }
        }

        public bool IsRacking(Hand h) => _rackHand == h;

        public override void AnyUpdate()
        {
            if (rig == null) return;
            // animate slide / bolt / pump
            _slideAnim = Mathf.MoveTowards(_slideAnim, 0, Time.deltaTime * 14f);
            float slideK = slideLocked || needsCycle && g.action == FireAction.Bolt ? 0.9f : Mathf.Sin(_slideAnim * Mathf.PI) * (g.action == FireAction.Bolt || g.action == FireAction.Pump ? 0f : 1f);
            if (_rackHand != null) slideK = Mathf.Max(slideK, _rackPull / Mathf.Max(0.001f, -rig.slideTravel.z));
            if (rig.slide != null) rig.slide.localPosition = rig.slideTravel * slideK;
            if (rig.pump != null) rig.pump.localPosition = new Vector3(0, 0, -_pump);
            if (secondaryHand == null) _pump = Mathf.MoveTowards(_pump, 0, Time.deltaTime * 0.5f);
        }

        public override void OnGrab(Hand h)
        {
            if (primaryHand != null && h != primaryHand && TryStartRack(h)) { h.BeginRack(this); return; }
            base.OnGrab(h);
        }
    }

    /// <summary>Magazine / speed loader / shell caddy / stripper clip.</summary>
    public class Magazine : Item
    {
        public Caliber cal;
        public MagStyle style;
        public int rounds, capacity;
        public string gunId;
        public Gun inGun;
        float _despawn = -1;

        public static Magazine Create(Gun forGun, int rounds, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Mag_" + forGun.g.id);
            go.transform.SetPositionAndRotation(pos, rot);
            var m = go.AddComponent<Magazine>();
            m.cal = forGun.g.cal; m.style = forGun.rig.magStyle; m.capacity = forGun.g.magSize; m.rounds = Mathf.Min(rounds, m.capacity); m.gunId = forGun.g.id;
            var model = GunBuilder.BuildMag(m.style, go.transform, forGun.g.furniture, out var size);
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var col = go.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center; col.size = mesh.bounds.size;
            m.localBounds = mesh.bounds;
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            m.SetupPhysics(0.35f);
            return m;
        }

        public override string Label => Db.CalName(cal) + "  " + rounds + "/" + capacity;
        public override int Value => 0;

        public override bool CanGrab(Hand h)
        {
            // pulling a magazine out of a gun by hand is allowed on detachable guns
            return true;
        }

        public override void OnGrab(Hand h)
        {
            if (inGun != null) { var g = inGun; g.mag = null; inGun = null; Sfx.Play(Synth.MagOut(), transform.position, 0.6f, 1f, 0.5f, 8f); }
            _despawn = -1;
            base.OnGrab(h);
        }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            // auto-insert when brought to the magwell of a gun held in the other hand
            var other = h.Other;
            var gun = other != null ? other.Held as Gun : null;
            if (gun == null) return;
            if (!gun.Accepts(this)) return;
            if (Vector3.Distance(transform.position, gun.rig.magwell.position) < (gun.Internal ? 0.08f : 0.055f))
                gun.InsertMag(this);
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I.Player;
            if (pl != null && pl.Gear.NearChestPouch(transform.position))
            {
                // put it back in the pouch: rounds return to your reserve
                pl.Gear.AddAmmo(cal, rounds);
                Sfx.Play(Synth.Grab(1), transform.position, 0.5f, 0.9f, 0.5f, 6f);
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        public void MarkDropped() { _despawn = 25f; }

        public override void AnyUpdate()
        {
            if (_despawn > 0 && !Held && inGun == null)
            {
                _despawn -= Time.deltaTime;
                if (_despawn <= 0) Destroy(gameObject);
            }
        }
    }

    /// <summary>Holographic / red dot sight. Bring it to a gun's top rail and let go to mount it.</summary>
    public class SightItem : Item
    {
        public SightDef sd;
        public Gun onGun;

        public static SightItem Create(SightDef def, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(def.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var s = go.AddComponent<SightItem>();
            s.def = def; s.sd = def;
            GunBuilder.BuildSight(def, go.transform);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.025f, 0); col.size = new Vector3(0.04f, 0.05f, 0.08f);
            s.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            s.SetupPhysics(0.25f);
            return s;
        }

        public void AttachTo(Gun g, bool sound = true)
        {
            if (g.sight != null && g.sight != this) return;
            onGun = g;
            g.sight = this;
            rb.isKinematic = true;
            SetCollidersTrigger(true);
            transform.SetParent(g.rig.rail, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (sound) { Sfx.Play(Synth.MagIn(), transform.position, 0.6f, 1.4f, 0.5f, 8f); }
        }

        public override void OnGrab(Hand h)
        {
            if (onGun != null) { onGun.sight = null; onGun = null; Sfx.Play(Synth.MagOut(), transform.position, 0.5f, 1.4f, 0.5f, 8f); }
            base.OnGrab(h);
        }

        protected override bool TryAttach()
        {
            Gun best = null; float bd = 0.08f;
            foreach (var it in All)
            {
                var g = it as Gun;
                if (g == null || g.rig.rail == null || g.sight != null) continue;
                float d = Vector3.Distance(g.rig.rail.position, transform.position);
                if (d < bd) { bd = d; best = g; }
            }
            if (best == null) return false;
            AttachTo(best);
            return true;
        }
    }
}
