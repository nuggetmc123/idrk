using UnityEngine;

namespace Rago
{
    /// <summary>
    /// VR firearm: physical magazines, chambering, slide/charging-handle racking (grab it with your other hand),
    /// pump and bolt actions, fire modes, recoil into the physics hands, holo-sight rail.
    /// VR controls (hand holding the gun): Trigger = fire, B/Y tap = drop magazine, B/Y hold = switch fire mode.
    /// A/X is jump. Rack by grabbing the slide / charging handle / bolt with your other hand and pulling it back.
    /// Desktop: X = drop magazine, F = rack / fire mode.
    /// </summary>
    public class Gun : Item
    {
        public GunDef g;
        public GunRig rig;
        public Magazine mag;
        public SightItem sight;
        public AttachItem muzzleAtt, sideAtt, gripAtt;
        public CamItem camAtt;   // a creator cam clipped to the side
        public float sightZ, sideZ, gripZ; public bool sideLeft;   // where the attachments sit on their rails
        public string skin = "";   // paint job (GunSkins)

        /// <summary>How far an attachment of this kind may slide along its rail from its default spot (metres).</summary>
        public Vector2 RailRange(AttachSlot? s)
        {
            if (s == null) return g.TwoHanded ? new Vector2(-0.07f, 0.07f) : new Vector2(-0.015f, 0.015f);   // optic rail
            return s == AttachSlot.Muzzle ? Vector2.zero : new Vector2(-0.08f, 0.03f);                     // handguard rail
        }
        public bool SideOn = true;   // light / laser switched on
        public bool RemoteOn;        // another player's gun shown here: its light / laser follows what they send
        public void ToggleSide()
        {
            if (sideAtt == null) return;
            SideOn = !SideOn;
            Sfx.Play(Synth.DryFire(), transform.position, 0.25f, 1.6f, 0.5f, 4f);
            primaryHand?.Haptic(0.15f, 0.02f);
        }
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
        public override string Label
        {
            get
            {
                string s = g.name + (sight != null ? " + " + sight.def.name : "");
                int n = (muzzleAtt != null ? 1 : 0) + (sideAtt != null ? 1 : 0) + (gripAtt != null ? 1 : 0);
                return n > 0 ? s + " (+" + n + ")" : s;
            }
        }
        public override int Value => g.price + (sight != null ? sight.def.price : 0) + (muzzleAtt != null ? muzzleAtt.def.price : 0) + (sideAtt != null ? sideAtt.def.price : 0) + (gripAtt != null ? gripAtt.def.price : 0);

        // ------------------------------------------------------------ attachments
        Transform _muzzlePt, _sidePt, _gripPt;
        bool _ptsMade;
        public AttachItem Attached(AttachSlot s) => s == AttachSlot.Muzzle ? muzzleAtt : s == AttachSlot.Side ? sideAtt : gripAtt;
        public void SetAttached(AttachSlot s, AttachItem a)
        {
            if (s == AttachSlot.Muzzle)
            {
                muzzleAtt = a;
                if (rig.muzzleDevice != null) rig.muzzleDevice.SetActive(a == null);   // the gun's own brake / flash hider comes off for the can
            }
            else if (s == AttachSlot.Side) sideAtt = a; else { gripAtt = a; RefreshSupport(); }
        }

        Transform _supportPt;
        /// <summary>Where the support hand goes: on the fitted foregrip (vertical, angled, stubby), else the handguard.</summary>
        public void RefreshSupport()
        {
            if (rig == null || rig.foregrip == null) return;
            if (gripAtt == null || gripAtt.transform.parent == null) { secondGrip = rig.foregrip; return; }
            if (_supportPt == null) { _supportPt = new GameObject("SupportGrip").transform; _supportPt.SetParent(transform, false); }
            int m = gripAtt.ad.model;
            // the middle of where your fist closes on it (in the attachment's space: it hangs down -y, the muzzle is +z)
            Vector3 hold = m == 1 ? new Vector3(0, -0.03f, -0.004f) : m == 2 ? new Vector3(0, -0.034f, 0) : new Vector3(0, -0.052f, -0.004f);
            _supportPt.localPosition = transform.InverseTransformPoint(gripAtt.transform.TransformPoint(hold));
            _supportPt.localRotation = Quaternion.identity;
            secondGrip = _supportPt;
        }

        /// <summary>The fitted foregrip's kind (0 vertical, 1 angled, 2 stubby), or -1 for none.</summary>
        public int GripKind => gripAtt != null && gripAtt.transform.parent != null ? gripAtt.ad.model : -1;
        public bool Quiet => g.suppressed || muzzleAtt != null && muzzleAtt.ad.suppress;
        float RecoilMul => (muzzleAtt != null ? muzzleAtt.ad.recoilMul : 1f) * (gripAtt != null ? gripAtt.ad.recoilMul : 1f) * (sideAtt != null ? sideAtt.ad.recoilMul : 1f);
        float SpreadMul => (muzzleAtt != null ? muzzleAtt.ad.spreadMul : 1f) * (gripAtt != null ? gripAtt.ad.spreadMul : 1f) * (sideAtt != null ? sideAtt.ad.spreadMul : 1f);

        /// <summary>Where an attachment of this kind goes on this gun (null = this gun can't take one).</summary>
        /// <summary>
        /// Will this attachment go on this gun? Muzzle devices come in sizes: the compact can is for pistol-calibre guns
        /// (pistols, SMGs), the long can, brake and compensator for rifle calibres.
        /// </summary>
        public bool CanTake(AttachDef a)
        {
            if (a == null || AttachPoint(a.slot) == null) return false;
            if (a.slot != AttachSlot.Muzzle) return true;
            bool pistolCal = g.cal == Caliber.C9mm || g.cal == Caliber.C45 || g.cal == Caliber.C57 || g.cal == Caliber.C50AE || g.cal == Caliber.C357;
            return a.model == 0 ? pistolCal : !pistolCal;
        }

        public Transform AttachPoint(AttachSlot s)
        {
            if (!_ptsMade) MakeAttachPoints();
            return s == AttachSlot.Muzzle ? _muzzlePt : s == AttachSlot.Side ? _sidePt : _gripPt;
        }

        void MakeAttachPoints()
        {
            _ptsMade = true;
            var st = g.style;
            // threaded barrels only: not built-in cans, revolvers, shotguns, belt-fed MGs or the .50s' giant brakes
            if (!g.suppressed && rig.muzzle != null && st != GunStyle.Revolver && st != GunStyle.Double && st != GunStyle.Pump && st != GunStyle.BoxShotgun
                && st != GunStyle.Lmg && st != GunStyle.AntiMat && g.cal != Caliber.C12g && g.cal != Caliber.C50BMG)
                _muzzlePt = rig.muzzle;
            if (rig.under != null) _sidePt = rig.under;
            if (rig.foregrip == null || !g.TwoHanded) return;
            // only guns with a railed handguard take lights / lasers and foregrips
            bool railed = st == GunStyle.AR || st == GunStyle.Battle || st == GunStyle.Bullpup || st == GunStyle.Lmg || st == GunStyle.BoxShotgun || st == GunStyle.AntiMat || st == GunStyle.SmgVector;
            if (!railed) return;
            // find the outside of the handguard from the gun's own mesh
            var body = rig.root.transform.Find("Body");
            var mf = body != null ? body.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return;
            var v = mf.sharedMesh.vertices;
            float fz = rig.foregrip.localPosition.z, mz = rig.muzzle != null ? rig.muzzle.localPosition.z - 0.08f : fz + 0.1f;
            float sz = Mathf.Min(fz + 0.07f, mz), gz = Mathf.Min(fz + 0.065f, mz);
            float maxX = 0f, minY = float.MaxValue; bool sideOk = false, gripOk = false;
            float by = rig.boreY;
            foreach (var p in v)
            {
                if (Mathf.Abs(p.z - sz) < 0.025f && p.y > by - 0.035f && p.y < by + 0.02f && p.x > maxX) { maxX = p.x; sideOk = true; }
                if (Mathf.Abs(p.z - gz) < 0.02f && Mathf.Abs(p.x) < 0.01f && p.y < by && p.y > by - 0.09f && p.y < minY) { minY = p.y; gripOk = true; }
            }
            if (sideOk && maxX > 0.008f)
            {
                _sidePt = new GameObject("SideRail").transform;
                _sidePt.SetParent(rig.root.transform, false);
                _sidePt.localPosition = new Vector3(maxX, by - 0.008f, sz);
            }
            if (gripOk && st != GunStyle.Pump && st != GunStyle.Double && st != GunStyle.SmgVector)
            {
                _gripPt = new GameObject("GripRail").transform;
                _gripPt.SetParent(rig.root.transform, false);
                _gripPt.localPosition = new Vector3(0, minY + 0.002f, gz);
            }
        }

        void LoadAttachment(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var ad = Db.Get<AttachDef>(id);
            if (ad == null || !CanTake(ad)) return;
            AttachItem.Create(ad, transform.position, transform.rotation).AttachTo(this, false);
        }

        public static Gun Create(GunDef def, ItemData data, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(def.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var gun = go.AddComponent<Gun>();
            gun.def = def; gun.g = def;
            gun.rig = GunBuilder.Build(def, go.transform);
            gun.grip = new GameObject("Grip").transform;
            gun.grip.SetParent(go.transform, false);
            gun.grip.localPosition = new Vector3(0f, -0.015f, -0.015f);   // the middle of the pistol grip, where your fist closes
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
            // saved rounds include the chambered one (a revolver's round under the hammer, a double's loaded barrel too)
            if (chambered && Internal && internalRounds > 0) internalRounds--;
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
            sightZ = d.sightZ; sideZ = d.sideZ; gripZ = d.gripZ; sideLeft = d.sideLeft;
            if (sight != null) sight.Place(sightZ);
            LoadAttachment(d.muzzle); LoadAttachment(d.side); LoadAttachment(d.gripAtt);
            if (sideAtt != null) sideAtt.Place(sideZ, sideLeft);
            if (gripAtt != null) gripAtt.Place(gripZ, false);
            if (d.cam && camAtt == null) { var ci = ItemFactory.Spawn("cc_cam", transform.position, transform.rotation) as CamItem; if (ci != null) ci.MountOn(this); }
            RefreshSupport();
            GunSkins.Apply(this, d.skin);
            autoMode = g.action == FireAction.Auto || g.action == FireAction.Burst;
        }

        public override ItemData Save()
        {
            int r = Rounds + (chambered ? 1 : 0);   // everything in the gun, the chambered round included (Load takes it back out)
            return new ItemData(g.id) { rounds = r, chambered = chambered, sight = sight != null ? sight.def.id : "",
                muzzle = muzzleAtt != null ? muzzleAtt.def.id : "", side = sideAtt != null ? sideAtt.def.id : "", gripAtt = gripAtt != null ? gripAtt.def.id : "",
                sightZ = sight != null ? sightZ : 0f, sideZ = sideAtt != null ? sideZ : 0f, gripZ = gripAtt != null ? gripZ : 0f, sideLeft = sideAtt != null && sideLeft, skin = skin ?? "", cam = camAtt != null };
        }

        // ------------------------------------------------------------ magazines
        public bool Accepts(Magazine m) => m != null && m.cal == g.cal && (Internal || mag == null);   // any mag of the right calibre fits (it's reshaped to this gun's mag)

        public void InsertMag(Magazine m, bool sound = true)
        {
            if (primaryHand != null) OnReloaded?.Invoke(this);
            if (Internal)
            {
                // the round under the hammer / in a barrel counts: a 6-shot revolver holds 6, a double barrel 2
                bool chamberCounts = g.action == FireAction.Revolver || g.action == FireAction.Break;
                int take = Mathf.Max(0, Mathf.Min(m.rounds, g.magSize - internalRounds - (chamberCounts && chambered ? 1 : 0)));
                internalRounds += take;
                m.rounds -= take;
                internalAmmo = m.ammoType;
                if (sound) Sfx.Play(Synth.MagIn(), transform.position, 0.7f, 1.15f, 0.5f, 10f);
                if (!chambered && (g.action == FireAction.Revolver || g.action == FireAction.Break) && internalRounds > 0) { chambered = true; internalRounds--; }
                if (m.rounds <= 0) { m.primaryHand?.ForceRelease(); Destroy(m.gameObject); }
                return;
            }
            if (m.style != rig.magStyle || m.capacity != g.magSize) m.Restyle(this);
            mag = m;
            m.inGun = this;
            if (m.primaryHand != null) m.primaryHand.ForceRelease(true);
            m.SetMounted(true);
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
            m.SetMounted(false);
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
        public override bool AimedHold => true;

        public static System.Action<Gun> OnShot, OnReloaded;   // local player's guns (used by the training)

        // the muzzle (or the end of a screwed-on suppressor) is the part that would go through a wall
        public override Vector3 LocalTip => rig != null && rig.muzzle != null ? transform.InverseTransformPoint(MuzzleEnd.position) : base.LocalTip;

        int _burst; float _lastShotT = -9f;
        public AmmoType internalAmmo;
        /// <summary>The round about to be fired: whatever the mag (or the tube / cylinder) is loaded with.</summary>
        public AmmoType CurrentAmmo => Internal ? internalAmmo : mag != null ? mag.ammoType : AmmoType.Standard;

        void CycleAmmo(Hand h) => RagoGame.I.Player.Gear.CycleAmmo(h);
        static float DriftOf(string id) { int h = 17; foreach (char c in id ?? "") h = h * 31 + c; return (h & 1023) / 1023f; }

        void Fire()
        {
            if (primaryHand != null && RagoGame.I != null && RagoGame.I.Player != null) RagoGame.I.Player.SnapHandsNow();   // fire from where you see it
            OnShot?.Invoke(this);
            chambered = false;
            _hasSpent = true;
            Vector3 dir = ZeroedDir();
            float spr = g.pellets > 1 ? 0f : g.spread * 0.12f * SpreadMul;
            var shot = Quiet ? g.Quiet : g;   // a screwed-on suppressor makes it a quiet gun
            dir = Quaternion.Euler(Random.Range(-spr, spr), Random.Range(-spr, spr), 0) * dir;
            bool tracer = g.cls == GunClass.LMG || (g.TwoHanded && Random.value < 0.25f);
            Vector3 origin = Ballistics.SafeOrigin(RagoGame.I.Player.SafeEye(), rig.muzzle.position);   // barrel through a wall: you shoot the wall
            Ballistics.Fire(origin, dir, shot, 0, transform, RagoGame.I.Player.Vitals, false, tracer, false, CurrentAmmo);
            Fx.MuzzleFlash(muzzleAtt != null && muzzleAtt.ad.suppress ? MuzzleEnd : rig.muzzle, g.TwoHanded ? (g.cal == Caliber.C50BMG ? 2f : 1.2f) : 0.7f, shot.suppressed);
            Sfx.Gunshot(shot, rig.muzzle.position, true);
            OnLocalFire?.Invoke(this, origin, dir);
            // recoil
            float rec = g.recoil * RecoilMul;
            bool twoHand = secondaryHand != null;
            if (twoHand) rec *= 0.45f;
            if (g.TwoHanded && !twoHand) rec *= 1.6f;
            if (rig.butt != null && RagoGame.I.Player.ShoulderNear(rig.butt.position)) rec *= 0.6f;
            // full-power rifle rounds out of bolt guns / marksman rifles: a big shove into the shoulder and the scope
            // jumps right off the target - you have to bring it back down for the next shot
            float heavy = g.cls == GunClass.Sniper ? 1.7f : g.cls == GunClass.DMR ? 1.3f : 1f;
            rec *= heavy;
            primaryHand.AddRecoil(rec, g.TwoHanded);
            secondaryHand?.AddRecoil(rec * 0.6f, g.TwoHanded);
            // muzzle climb: every gun walks up and drifts to its own side (most a little right, some left), more with
            // each shot of a burst; it stays climbed until you stop and bring it back down. Shouldered with both hands
            // it's controllable; one-handed pistols and rifles fired from the hip climb hard.
            float now = Time.time;
            _burst = now - _lastShotT < 0.3f ? _burst + 1 : 0;
            _lastShotT = now;
            float bias = (DriftOf(g.id) - 0.35f) * 0.9f;
            float pitch = (g.TwoHanded ? 1.6f : 2.8f) * rec * (1f + Mathf.Min(_burst, 10) * 0.08f) * (heavy > 1f ? 1.6f : 1f);
            float yaw = (bias + Random.Range(-0.5f, 0.5f)) * rec * (g.TwoHanded ? 0.9f : 1.2f);
            primaryHand.Climb(new Vector2(pitch, yaw), Vector3.zero);
            if (secondaryHand != null)
            {
                float reach = Mathf.Clamp(Vector3.Distance(primaryHand.Anchor.position, secondaryHand.Anchor.position), 0.15f, 0.6f);
                Vector3 up = transform.up, right = transform.right;
                secondaryHand.Climb(Vector2.zero, (up * Mathf.Sin(pitch * Mathf.Deg2Rad) + right * Mathf.Sin(yaw * Mathf.Deg2Rad)) * reach);
            }
            // a sharp jolt you feel, bigger guns harder
            primaryHand.Haptic(Mathf.Clamp01(0.55f + g.recoil * 0.25f), 0.07f);
            secondaryHand?.Haptic(Mathf.Clamp01(0.4f + g.recoil * 0.2f), 0.06f);
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

        float _bHeld; bool _bWas, _bDone;
        /// <summary>VR B / Y on the gun hand: true on a quick tap (drop the mag); holding it half a second switches fire mode.</summary>
        bool MagButton(Hand h, HandInput input)
        {
            if (!XRInputs.VR) return input.PrimaryDown;
            bool tap = false;
            if (input.Secondary)
            {
                if (!_bWas) { _bHeld = 0f; _bDone = false; }
                _bHeld += Time.deltaTime;
                if (!_bDone && _bHeld >= 0.45f && (g.action == FireAction.Auto || g.action == FireAction.Burst))
                {
                    _bDone = true;
                    autoMode = !autoMode;
                    Sfx.Play(Synth.DryFire(), transform.position, 0.4f, 1.6f, 0.5f, 5f);
                    h.Haptic(0.2f, 0.03f);
                    RagoGame.I.Player.Toast(autoMode ? (g.action == FireAction.Burst ? "BURST" : "AUTO") : "SEMI");
                }
            }
            else if (_bWas && !_bDone) tap = true;
            _bWas = input.Secondary;
            return tap;
        }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            if (h != primaryHand) return;
            // trigger (the Market is a safe zone: weapons are cold)
            if (RagoGame.I != null && RagoGame.I.SafeZone)
            {
                if (input.TriggerDown) { Sfx.Play(Synth.DryFire(), transform.position, 0.5f, 1f, 0.5f, 6f); RagoGame.I.Player.Toast("Safe zone - weapons cold"); }
                if (MagButton(h, input)) EjectMag();
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
            if (XRInputs.VR)
            {
                // B / Y: tap drops the mag, hold switches fire mode. With the well empty (or a tube / cylinder gun), a tap
                // switches the ammo type for your next mag. No rack button: rack it by hand.
                if (MagButton(h, input)) { if (Internal || mag == null) CycleAmmo(h); else EjectMag(); }
                return;
            }
            // desktop: X drops the mag, F racks or switches fire mode, T switches ammo type
            if (input.PrimaryDown) EjectMag();
            if (Desktop.AmmoKey) CycleAmmo(h);
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
            // anywhere on the slide / charging handle / bolt: its top or either side, not just one exact point
            Vector3 l = transform.InverseTransformPoint(h.Anchor.position), s = transform.InverseTransformPoint(RackPoint);
            if (Mathf.Abs(l.x - s.x) > 0.09f || l.y < s.y - 0.06f || l.y > s.y + 0.09f || l.z < s.z - 0.14f || l.z > s.z + 0.12f) return false;
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

        /// <summary>
        /// Sights are zeroed: the bullet leaves the barrel angled up a hair so it crosses the line of sight at the
        /// zero distance (irons 25 m, red dots / holos 50 m, magnified scopes 100 m) - it hits where you aim.
        /// </summary>
        /// <summary>
        /// Where the shot goes: exactly where the sight is looking. The line through the scope / red dot / iron sights is
        /// followed out into the world to whatever it lands on, and the bullet leaves the muzzle straight for that point -
        /// so the reticle is right at every range (it used to be zeroed at one fixed distance, off a little everywhere
        /// else, and a scope sitting a touch crooked on its rail missed).
        /// </summary>
        Vector3 ZeroedDir()
        {
            Vector3 f = rig.muzzle.forward;
            Vector3 eye, axis;
            if (sight != null && sight.Lens != null) { eye = sight.Lens.position; axis = sight.transform.forward; }
            else if (rig.sightLine != null) { eye = rig.sightLine.position; axis = transform.forward; }
            else return f;
            Vector3 target = eye + axis * 300f;
            int n = Physics.RaycastNonAlloc(eye + axis * 0.05f, axis, _aimHits, 800f, AimMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _aimHits[i];
                if (h.collider.GetComponentInParent<Item>() != null) continue;   // the gun itself, its attachments, things in your hands
                if (h.distance < best) { best = h.distance; target = h.point; }
            }
            Vector3 d = target - rig.muzzle.position;
            if (d.sqrMagnitude < 0.25f) return f;   // right up against something: straight out of the barrel
            d.Normalize();
            return Vector3.Angle(d, f) < 6f ? d : f;   // never wildly off the barrel (a sight knocked sideways)
        }
        static readonly RaycastHit[] _aimHits = new RaycastHit[16];
        static int _aimMask;
        static int AimMask
        {
            get
            {
                if (_aimMask != 0) return _aimMask;
                _aimMask = ~0;
                foreach (int l in new[] { Layers.Hands, Layers.LocalBody, Layers.Player, Layers.Items }) if (l > 0) _aimMask &= ~(1 << l);
                return _aimMask;
            }
        }

        Transform _muzzleEnd;
        /// <summary>The end of a screwed-on suppressor (the flash comes out there).</summary>
        Transform MuzzleEnd
        {
            get
            {
                if (_muzzleEnd == null) { _muzzleEnd = new GameObject("MuzzleEnd").transform; _muzzleEnd.SetParent(rig.muzzle, false); }
                _muzzleEnd.localPosition = new Vector3(0, 0, muzzleAtt != null && muzzleAtt.ad.model == 1 ? 0.16f : 0.095f);
                return _muzzleEnd;
            }
        }

        Vector3 _rackLocal; bool _rackLocalSet;
        /// <summary>
        /// Where the slide / charging handle / bolt handle really is (the middle of its mesh). Its transform sits at the
        /// gun's origin by the trigger, which is where racking used to be grabbed and where the hand was drawn.
        /// </summary>
        public Vector3 RackPoint
        {
            get
            {
                if (rig == null || rig.slide == null) return transform.position;
                if (!_rackLocalSet)
                {
                    _rackLocalSet = true;
                    var mf = rig.slide.GetComponent<MeshFilter>();
                    _rackLocal = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
                }
                return rig.slide.TransformPoint(_rackLocal);
            }
        }

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
        public AmmoType ammoType;
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
            m._model = model;
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var col = go.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center; col.size = mesh.bounds.size;
            m.localBounds = mesh.bounds;
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            m.grip = new GameObject("Grip").transform; m.grip.SetParent(go.transform, false);
            m.PlaceGrip(mesh.bounds);
            m.SetupPhysics(0.35f);
            return m;
        }

        /// <summary>
        /// Held like a real mag: the body in your fist, the feed lips (top) up through the top of your fist and the
        /// rounds facing forward - ready to go straight up into a magwell.
        /// </summary>
        void PlaceGrip(Bounds b)
        {
            grip.localPosition = new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, 0.45f), b.center.z);
            grip.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);   // hand's fist axis = mag's up, hand's back = mag's back
        }

        public override string Label => Db.CalName(cal) + (ammoType == AmmoType.AP ? " AP" : ammoType == AmmoType.Incendiary ? " INCENDIARY" : "") + "  " + rounds + "/" + capacity;
        public override int Value => 0;

        GameObject _model;

        public override bool CanGrab(Hand h)
        {
            // a mag in a gun someone is holding stays in: drop it with the mag release (B / Y). Otherwise your support hand
            // would pull it out every time it came near the magwell. Mags in guns lying around can be pulled out by hand.
            if (inGun != null && inGun.Held) return false;
            return true;
        }

        /// <summary>Reshape a same-calibre mag from another gun to fit this one (extra rounds go back to your reserve).</summary>
        public void Restyle(Gun gun)
        {
            if (_model != null) Destroy(_model);
            _model = GunBuilder.BuildMag(gun.rig.magStyle, transform, gun.g.furniture, out _);
            var mesh = _model.GetComponent<MeshFilter>().sharedMesh;
            var col = GetComponent<BoxCollider>();
            if (col != null) { col.center = mesh.bounds.center; col.size = mesh.bounds.size; }
            localBounds = mesh.bounds;
            if (grip != null) PlaceGrip(mesh.bounds);
            style = gun.rig.magStyle; gunId = gun.g.id;
            capacity = gun.g.magSize;
            if (rounds > capacity)
            {
                var pl = RagoGame.I != null ? RagoGame.I.Player : null;
                if (pl != null) pl.Gear.Reserve(ammoType)[(int)cal] += rounds - capacity;
                rounds = capacity;
            }
        }

        public override Gun MountedOn => inGun;

        public override void OnGrab(Hand h)
        {
            if (inGun != null) { var g = inGun; g.mag = null; inGun = null; SetMounted(false); Sfx.Play(Synth.MagOut(), transform.position, 0.6f, 1f, 0.5f, 8f); }
            _despawn = -1;
            base.OnGrab(h);
        }

        // Loading like a real gun: line the mag up under the magwell (upright, rounds forward), get its top into the
        // mouth of the well, and push it up - it slides along the well and clicks in when it seats. Pull it back down or
        // twist it off line and it comes out again. The well has to be empty first (drop the old mag with B / Y).
        // Revolvers and tube-fed guns (internal mags) still just load when you bring the rounds close.
        Gun _rail;   // the gun whose magwell this mag is sliding in

        public override void GetHeldPose(out Vector3 pos, out Quaternion rot)
        {
            base.GetHeldPose(out pos, out rot);
            if (_rail == null || _rail.rig == null || _rail.rig.magwell == null) return;
            // drawn on the gun as you SEE it (its magwell transform), so the mag never hangs in the air beside it; where
            // it sits along the well comes from the hand (lined up against the gun's current held pose, in HeldUpdate)
            var w = _rail.rig.magwell;
            WellFrame(_rail, out var wp, out var wr);
            float y = Mathf.Clamp((Quaternion.Inverse(wr) * (pos - wp)).y, Mathf.Min(-0.14f, _rail.rig.magEntry - 0.02f), 0f);
            pos = w.TransformPoint(new Vector3(0, y, 0));
            rot = w.rotation;
        }

        /// <summary>The gun's magwell where the gun is being held THIS frame (from its hand), not where it was drawn last
        /// frame - running, those differ by several cm and the mag kept missing the well.</summary>
        static void WellFrame(Gun gun, out Vector3 wp, out Quaternion wr)
        {
            var w = gun.rig.magwell;
            wp = w.position; wr = w.rotation;
            if (!gun.Held || gun.primaryHand == null) return;
            gun.GetHeldPose(out var gp, out var gr);
            var inv = Quaternion.Inverse(gun.transform.rotation);
            wp = gp + gr * (inv * (w.position - gun.transform.position));
            wr = gr * (inv * w.rotation);
        }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            var other = h.Other;
            var gun = other != null ? other.Held as Gun : null;
            if (gun == null || cal != gun.g.cal || gun.rig == null || gun.rig.magwell == null) { _rail = null; return; }
            if (gun.Internal)
            {
                WellFrame(gun, out var iwp, out _); base.GetHeldPose(out var ip, out _);
                if (Vector3.Distance(ip, iwp) > 0.13f) return;
                if (gun.Accepts(this)) { gun.InsertMag(this); other.Haptic(0.5f, 0.06f); }
                return;
            }
            if (gun.mag != null && gun.mag != this) { _rail = null; return; }   // full: drop the old one first
            // where the hand is putting the mag, in the magwell's frame
            var saved = _rail; _rail = null;
            base.GetHeldPose(out var wantPos, out var wantRot);
            _rail = saved;
            WellFrame(gun, out var wp, out var wr);
            Vector3 l = Quaternion.Inverse(wr) * (wantPos - wp);
            float upAng = Vector3.Angle(wantRot * Vector3.up, wr * Vector3.up), fwdAng = Vector3.Angle(wantRot * Vector3.forward, wr * Vector3.forward);
            // moving, hands shake: a more forgiving mouth
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            float slack = pl != null && pl.MoveVelocity.sqrMagnitude > 1f ? 1.4f : 1.15f;
            if (_rail == null)
            {
                // engage: top of the mag in the mouth of the well, roughly upright and facing the right way
                if (Mathf.Abs(l.x) < 0.03f * slack && Mathf.Abs(l.z) < 0.035f * slack && l.y > gun.rig.magEntry * slack && l.y < 0.015f && upAng < 35f * slack && fwdAng < 55f * slack)
                {
                    _rail = gun;
                    other.Haptic(0.2f, 0.02f); h.Haptic(0.2f, 0.02f);
                    Sfx.Play(Synth.Grab(1), wp, 0.25f, 1.4f, 0.5f, 5f);
                }
                return;
            }
            if (_rail != gun) { _rail = null; return; }
            // on the rail: pulled away or twisted off line - it comes back out
            if (Mathf.Abs(l.x) > 0.07f * slack || Mathf.Abs(l.z) > 0.08f * slack || l.y < Mathf.Min(-0.17f, gun.rig.magEntry - 0.05f) || upAng > 50f) { _rail = null; return; }
            // pushed home: click
            if (l.y > -0.006f)
            {
                _rail = null;
                gun.InsertMag(this);
                other.Haptic(0.7f, 0.07f); h.Haptic(0.5f, 0.05f);
            }
        }

        public override void OnRelease(Hand h, Vector3 vel, Vector3 angVel) { _rail = null; base.OnRelease(h, vel, angVel); }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I.Player;
            if (pl != null && pl.Gear.NearMagPouch(transform.position))
            {
                // put it back in the pouch: rounds return to your reserve
                pl.Gear.AddAmmo(cal, rounds, ammoType);
                Sfx.Play(Synth.Grab(1), transform.position, 0.5f, 0.9f, 0.5f, 6f);
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        public void MarkDropped() { _despawn = 25f; }

        public override void AnyUpdate()
        {
            if (_despawn > 0 && !Held && inGun == null && !(RagoGame.I != null && RagoGame.I.Area == Area.Bunker))   // nothing despawns in your bunker
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
        public override Gun MountedOn => onGun;
        public Transform Lens;   // the holo window / the scope's eyepiece

        public static SightItem Create(SightDef def, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(def.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var s = go.AddComponent<SightItem>();
            s.def = def; s.sd = def;
            s.Lens = GunBuilder.BuildSight(def, go.transform);
            if (def.mag > 0f) go.AddComponent<ScopeView>().Init(s, def.mag);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.025f, 0); col.size = new Vector3(0.04f, 0.05f, 0.08f);
            var bodyMf = go.transform.Find("SightBody")?.GetComponent<MeshFilter>();
            if (def.mag > 0f && bodyMf != null && bodyMf.sharedMesh != null) { var bb = bodyMf.sharedMesh.bounds; col.center = bb.center; col.size = bb.size; }   // scopes are long
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
            SetMounted(true);
            SetCollidersTrigger(true);
            // placed by hand: it stays where along the rail you let go of it (slide it by grabbing it and letting go again)
            if (sound) { var r = g.RailRange(null); g.sightZ = Mathf.Clamp(g.rig.rail.InverseTransformPoint(transform.position).z, r.x, r.y); }
            transform.SetParent(g.rig.rail, false);
            Place(g.sightZ);
            if (sound) { Sfx.Play(Synth.MagIn(), transform.position, 0.6f, 1.4f, 0.5f, 8f); }
        }
        public void Place(float z) { transform.localPosition = new Vector3(0, 0, z); transform.localRotation = Quaternion.identity; }

        public override void OnGrab(Hand h)
        {
            if (onGun != null) { onGun.sight = null; onGun = null; SetMounted(false); Sfx.Play(Synth.MagOut(), transform.position, 0.5f, 1.4f, 0.5f, 8f); }
            base.OnGrab(h);
        }

        protected override bool TryAttach()
        {
            Gun best = null; float bd = 0.08f;
            foreach (var it in All)
            {
                var g = it as Gun;
                if (g == null || g.rig.rail == null || g.sight != null) continue;
                // anywhere along the optic rail counts (not just its middle)
                var lp = g.rig.rail.InverseTransformPoint(transform.position); var rr = g.RailRange(null);
                float d = Vector3.Distance(g.rig.rail.TransformPoint(new Vector3(0, 0, Mathf.Clamp(lp.z, rr.x, rr.y))), transform.position);
                if (d < bd) { bd = d; best = g; }
            }
            if (best == null) return false;
            AttachTo(best);
            return true;
        }
    }
}
