using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Physics hand: a rigidbody that chases the tracked controller (so it collides with the world),
    /// grabs items, two-hands guns, racks slides, pulls gear off your body, points at UI, and carries recoil.
    /// </summary>
    public class Hand : MonoBehaviour
    {
        public bool left;
        public PlayerRig rig;
        public Rigidbody rb;
        public Transform Anchor;          // palm pose held items align to
        public Item Held;
        public bool HeldAsSecondary;
        public Gun Racking;
        public HandInput Input;
        public Vector3 Velocity { get; private set; }
        public Vector3 TrackedPosition { get; private set; }
        /// <summary>Gripping the world to climb: the hand stays at ClimbAnchor and your body moves instead.</summary>
        public bool Climbing { get; private set; }
        public Vector3 ClimbAnchor { get; private set; }
        public float ClimbStart { get; private set; }
        public Quaternion TrackedRotation { get; private set; }
        public Hand Other => left ? rig.HandR : rig.HandL;

        /// <summary>The controller's aim offset: guns point where the controller points, not along its handle.</summary>
        public Quaternion AimLocal => !XRInputs.VR ? Quaternion.identity : Input.HasAim ? Input.AimLocal : Quaternion.Euler(30, 0, 0);   // no aim pose reported: the usual ~30 degrees
        /// <summary>World rotation for a held item's grip: the hand's anchor, turned to the aim direction for guns.</summary>
        public Quaternion HoldRotation(bool aim) => aim ? transform.rotation * AimLocal * Anchor.localRotation : Anchor.rotation;
        public static float GripPitch = 0f; // tweakable grip angle (bunker settings)

        Transform _visual;
        /// <summary>The hand model you see (it's rotated / offset from the controller to wrap around the grip).</summary>
        public Transform Visual => _visual != null ? _visual : transform;
        readonly List<Transform[]> _fingers = new List<Transform[]>();
        Transform _thumb;
        BoxCollider _col;
        LineRenderer _laser;
        GameObject _laserDot;
        WorldButton _hoverBtn;
        Vector3 _recoilPos; Vector3 _recoilRot; Vector3 _recoilVel; Vector3 _recoilAngVel;
        Vector3 _climbRot, _climbPos; float _lastKickT = -9f;   // muzzle climb that stays until you pull it back down
        Vector3 _lastPos;
        float _stuckTime;
        Slot _hintSlot;
        Vector3 _sag;
        static readonly Collider[] _overlap = new Collider[32];

        public static Hand Create(PlayerRig rig, bool left)
        {
            var go = new GameObject(left ? "HandL" : "HandR");
            DontDestroyOnLoad(go);
            var h = go.AddComponent<Hand>();
            h.left = left; h.rig = rig;
            h.rb = go.AddComponent<Rigidbody>();
            h.rb.useGravity = false;
            h.rb.mass = 1.5f;
            h.rb.interpolation = RigidbodyInterpolation.Interpolate;
            h.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            h.rb.SetDamping(0f, 0f);
            h.rb.maxAngularVelocity = 50f;
            h._col = go.AddComponent<BoxCollider>();
            h._col.size = new Vector3(0.07f, 0.035f, 0.09f);
            h._col.center = new Vector3(0, 0, 0.0f);
            go.SetLayerRecursive(Layers.Hands);
            h.Anchor = new GameObject("Anchor").transform;
            h.Anchor.SetParent(go.transform, false);
            h.BuildVisual();
            h.BuildLaser();
            return h;
        }

        void BuildVisual()
        {
            // ragdoll test-dummy hand: plastic fingers on metal knuckle joints, fingerless tactical glove
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            float s = left ? -1 : 1;
            Color body = HumanRig.BodyColor(Outfit.Operator, Profile.Current != null ? Profile.Current.outfitSeed : 7);
            Color glove = new Color(0.13f, 0.12f, 0.11f);
            var mats = new[] { HumanRig.PlasticMat, HumanRig.JointMat, Mats.Lit("glove", Color.white, 0.05f, 0.3f, Mats.Fabric, Mats.FabricN, 0.4f, 0.1f) };
            var palm = new MB(3);
            palm.Sub(0).Box(new Vector3(0, 0, -0.01f), new Vector3(0.082f, 0.03f, 0.092f), body, 0.012f, 2);
            palm.Sub(2).Box(new Vector3(0, 0.004f, -0.018f), new Vector3(0.088f, 0.034f, 0.07f), glove, 0.012f, 2); // fingerless glove
            palm.Sub(1).Sphere(new Vector3(0, 0, -0.07f), Vector3.one * 0.03f, 12, HumanRig.JointCol); // wrist ball
            palm.Build("Palm", _visual, mats);
            for (int f = 0; f < 4; f++)
            {
                var segs = new Transform[3];
                Transform parent = _visual;
                Vector3 basePos = new Vector3(s * (-0.03f + f * 0.02f), 0.0f, 0.037f);
                float[] lens = { 0.035f, 0.025f, 0.02f };
                for (int k = 0; k < 3; k++)
                {
                    var j = new GameObject("F" + f + k).transform;
                    j.SetParent(parent, false);
                    j.localPosition = k == 0 ? basePos : new Vector3(0, 0, lens[k - 1]);
                    var mb = new MB(2);
                    float w = 0.0092f - k * 0.0008f - (f == 3 ? 0.0012f : 0);
                    mb.Sub(1).Sphere(Vector3.zero, Vector3.one * w * 0.95f, 8, HumanRig.JointCol); // knuckle joint
                    mb.Sub(0).Capsule(new Vector3(0, 0, w * 0.9f), new Vector3(0, 0, lens[k] - w * 0.6f), w * 0.92f, w * 0.85f, 8, body);
                    mb.Build("seg", j, mats[0], mats[1]);
                    segs[k] = j;
                    parent = j;
                }
                _fingers.Add(segs);
            }
            _thumb = new GameObject("Thumb").transform;
            _thumb.SetParent(_visual, false);
            _thumb.localPosition = new Vector3(s * -0.04f, -0.008f, 0.0f);
            _thumb.localRotation = Quaternion.Euler(0, s * -45f, s * 30f);
            var tb = new MB(2);
            tb.Sub(1).Sphere(Vector3.zero, Vector3.one * 0.011f, 8, HumanRig.JointCol);
            tb.Sub(0).Capsule(new Vector3(0, 0, 0.01f), new Vector3(0, 0, 0.045f), 0.0105f, 0.009f, 8, body);
            tb.Build("thumb", _thumb, mats[0], mats[1]);
            foreach (var r in _visual.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildLaser()
        {
            var go = new GameObject("Laser");
            go.transform.SetParent(transform, false);
            _laser = go.AddComponent<LineRenderer>();
            _laser.positionCount = 2;
            _laser.startWidth = 0.004f; _laser.endWidth = 0.002f;
            _laser.sharedMaterial = Mats.Unlit("laser", new Color(1f, 0.75f, 0.3f, 0.6f), Mats.Glow, Mats.BlendKind.Additive);
            _laser.enabled = false;
            _laserDot = new MB().Sphere(Vector3.zero, Vector3.one * 0.008f, 8, Color.white).Build("dot", transform, Mats.Unlit("laserdot", new Color(1.5f, 1f, 0.4f, 1f), null, Mats.BlendKind.Additive));
            _laserDot.SetActive(false);
        }

        public void Haptic(float amp, float dur) => XRInputs.Haptic(left, amp, dur);

        // ---- fists: swing an empty hand into someone. The faster the swing, the harder it lands (head hits hurt most).
        Vector3 _punchLast, _punchPrevFist; bool _punchTrack;
        float _lockPunchT;
        static readonly Collider[] _tmpPunch = new Collider[16];
        static int CopyPunch(int m, int at) { int k = 0; for (int i = 0; i < m && at + k < _punchCols.Length; i++) _punchCols[at + k++] = _tmpPunch[i]; return k; }
        readonly System.Collections.Generic.Dictionary<IDamageable, float> _punchCool = new System.Collections.Generic.Dictionary<IDamageable, float>();
        static readonly Collider[] _punchCols = new Collider[16];
        void Punch(float dt)
        {
            if (!XRInputs.VR || Held != null || Climbing || Racking != null || rig.Dead || dt <= 0f) { _punchTrack = false; return; }
            Vector3 p = TrackedPosition;
            float speed = _punchTrack ? (p - _punchLast).magnitude / dt - rig.MoveVelocity.magnitude : 0f;   // your own walking doesn't count
            Vector3 dir = _punchTrack ? (p - _punchLast).normalized : transform.forward;
            _punchLast = p; _punchTrack = true;
            if (speed < 2.4f) { _punchPrevFist = p + TrackedRotation * Vector3.forward * 0.05f; return; }
            Vector3 fist = p + TrackedRotation * Vector3.forward * 0.05f;   // knuckles, a little ahead of the grip
            if (!rig.CanReach(fist)) return;   // no punching through walls
            Vector3 from = _punchPrevFist; _punchPrevFist = fist;
            Vector3 sweep = fist - from; float sl = sweep.magnitude;
            int n;
            if (sl > 0.01f && sl < 1f)
            {
                var hs = Physics.SphereCastAll(from, 0.09f, sweep / sl, sl, ~0, QueryTriggerInteraction.Collide);
                n = 0; foreach (var hh in hs) if (n < _punchCols.Length) _punchCols[n++] = hh.collider;
                if (n < _punchCols.Length) n += Physics.OverlapSphereNonAlloc(fist, 0.1f, _tmpPunch, ~0, QueryTriggerInteraction.Collide) is int m2 ? CopyPunch(m2, n) : 0;
            }
            else n = Physics.OverlapSphereNonAlloc(fist, 0.1f, _punchCols, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var lk = _punchCols[i] != null ? _punchCols[i].GetComponentInParent<Padlock>() : null;
                if (lk != null && Time.time > _lockPunchT) { _lockPunchT = Time.time + 0.4f; lk.Damage(10f, p); Haptic(0.9f, 0.08f); break; }   // punch a padlock off (takes a few)
                var zone = _punchCols[i].GetComponentInParent<HitZone>();
                if (zone == null || zone.owner == null || !zone.owner.Alive || zone.owner == (IDamageable)rig.Vitals) continue;
                if (_punchCool.TryGetValue(zone.owner, out var until) && Time.time < until) continue;
                _punchCool[zone.owner] = Time.time + 0.45f;
                float mult = zone.part == BodyPart.Head ? 1.7f : zone.part == BodyPart.Thorax || zone.part == BodyPart.Stomach ? 1f : 0.6f;
                float dmg = Mathf.Lerp(8f, 22f, Mathf.InverseLerp(2.4f, 7f, speed)) * mult;
                Vector3 at = _punchCols[i].ClosestPoint(p);
                var info = new HitInfo { damage = dmg, part = zone.part, point = at, dir = dir, normal = -dir, attacker = 0, gunId = "fist", force = 3f };
                zone.owner.TakeHit(info, zone);
                Ballistics.ReportHit(info, zone);
                Sfx.Play(Synth.Impact(Synth.Surface.Flesh, Random.Range(0, 3)), at, 0.9f, 0.8f, 1f, 15f);
                Haptic(0.9f, 0.08f);
                Noise.Emit(at, 5f, 0);
                break;
            }
        }

        /// <summary>Muzzle climb that doesn't spring straight back: each shot walks the gun up (and a little to the side),
        /// full auto keeps walking it, and it only settles once you stop firing - like a real shooter pulling it back
        /// down. rot: degrees (pitch up, yaw right) for the grip hand; pos: metres (world) for the support hand, which is
        /// what actually tips a two-handed gun.</summary>
        /// <summary>An arm wound: the hand shakes (a slow wobble plus a fine tremor), a few degrees at worst.</summary>
        Vector3 Tremor()
        {
            float a = rig != null && rig.Vitals != null ? rig.Vitals.ArmInjury : 0f;
            if (a <= 0f || Held == null) return Vector3.zero;
            float t = Time.time, s = (this == rig.HandL ? 7.3f : 0f);
            return new Vector3(Mathf.PerlinNoise(t * 0.9f, s) - 0.5f + (Mathf.PerlinNoise(t * 9f, s + 3f) - 0.5f) * 0.3f,
                               Mathf.PerlinNoise(s + 11f, t * 0.8f) - 0.5f + (Mathf.PerlinNoise(s + 5f, t * 8f) - 0.5f) * 0.3f, 0f) * (5f * a);
        }

        public void Climb(Vector2 rot, Vector3 pos)
        {
            _climbRot += new Vector3(-rot.x, rot.y, 0);
            _climbPos += pos;
            _climbRot = Vector3.ClampMagnitude(_climbRot, 28f);
            _climbPos = Vector3.ClampMagnitude(_climbPos, 0.16f);
            _lastKickT = Time.time;
        }

        public void AddRecoil(float strength, bool rifle)
        {
            var vit = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Vitals : null;
            if (vit != null && vit.Focus > 0f) strength *= 0.67f;   // DUBBY FOCUS: a third less kick
            // real kick: the gun drives back into your hand (cm) and the muzzle flips up (degrees), then the spring brings it
            // back. It used to be scaled down to a millimetre and a fraction of a degree - you couldn't feel any recoil.
            // Full auto stacks the kicks, so the muzzle climbs unless you hold it with both hands on the shoulder.
            // (stronger again: about 1.6x the last version)
            _recoilVel += new Vector3(Random.Range(-0.12f, 0.12f) * strength, 0.1f * strength, -1.45f * strength) * (rifle ? 0.8f : 1f);
            _recoilAngVel += new Vector3(-(rifle ? 245f : 400f) * strength, Random.Range(-48f, 48f) * strength, Random.Range(-30f, 30f) * strength);
        }

        // ---------------------------------------------------------------------------------- per frame
        public void Tick(HandInput input, Vector3 targetPos, Quaternion targetRot)
        {
            Input = input;
            TrackedPosition = targetPos;
            _trk[_trkN % _trk.Length] = (Time.time, targetPos); _trkN++;   // for throwing: the controller's own movement
            TrackedRotation = Compat.SafeRot(targetRot);
            Anchor.localRotation = Quaternion.Euler(GripPitch, 0, 0);

            if (rig.Dead) { if (Held != null) ForceRelease(); return; }

            // recoil spring
            float dt = Time.deltaTime;
            _recoilVel += (-_recoilPos * 260f - _recoilVel * 22f) * dt;
            _recoilPos += _recoilVel * dt;
            _recoilAngVel += (-_recoilRot * 220f - _recoilAngVel * 22f) * dt;
            _recoilRot += _recoilAngVel * dt;
            _recoilPos = Vector3.ClampMagnitude(_recoilPos, 0.11f);
            _recoilRot = Vector3.ClampMagnitude(_recoilRot, 32f);
            // climb recovery: barely while the trigger's still working (a burst keeps climbing), then you bring it down
            float rec = Time.time - _lastKickT < 0.15f ? 0.6f : 4f;
            float kd = 1f - Mathf.Exp(-rec * dt);
            _climbRot = Vector3.Lerp(_climbRot, Vector3.zero, kd);
            _climbPos = Vector3.Lerp(_climbPos, Vector3.zero, kd);

            Punch(dt);
            // an empty hand at your mag pouch + B / Y: switch the ammo type your next mags come out with
            if (XRInputs.VR && Held == null && input.SecondaryDown && rig.Gear != null && rig.Gear.NearMagPouch(TrackedPosition)) rig.Gear.CycleAmmo(this);
            // night vision: an empty hand at your forehead + trigger flips the goggles down / up
            if (Held == null && Racking == null && input.TriggerDown && rig.Gear.NvgWorn && Vector3.Distance(TrackedPosition, rig.Gear.NvgPoint) < 0.16f
                && !(rig.Gear.CamWorn && Vector3.Distance(TrackedPosition, rig.Gear.CamPoint) < Vector3.Distance(TrackedPosition, rig.Gear.NvgPoint)))
                rig.Gear.ToggleNvg();
            // camera: an empty hand up by the right side of your helmet + trigger = RECORD / stop. A generous spot: the
            // controller bumps the headset long before its tracking point gets within a few cm of the camera
            else if (Held == null && Racking == null && input.TriggerDown && rig.Gear.CamWorn && rig.Gear.NearCam(TrackedPosition))
            {
                rig.Gear.ToggleRec();
                Haptic(0.5f, 0.08f);
            }
            // a camera on the gun in your other hand: an empty hand on it + trigger = RECORD / stop
            else if (Held == null && Racking == null && input.TriggerDown && Other != null && Other.Held is Gun cg && cg.camAtt != null && Vector3.Distance(TrackedPosition, cg.camAtt.transform.position) < 0.09f)
            {
                cg.camAtt.ToggleRec(rig);
                Haptic(0.5f, 0.08f);
            }
            // rack with the free hand on the slide / charging handle: hold the TRIGGER (or grip), pull back, let go
            if (Racking == null && Held == null && _interact == null && input.TriggerDown && Other.Held is Gun rg && !Other.HeldAsSecondary && rg.TryStartRack(this))
            {
                BeginRack(rg); Haptic(0.2f, 0.03f);
            }
            // light / laser on the gun: ONLY while this hand is on the gun's handguard (support grip) - an empty off hand
            // pulling its trigger used to switch it too
            else if (input.TriggerDown && Racking == null && _interact == null && _hoverBtn == null)
            {
                Gun lg = HeldAsSecondary ? Held as Gun : null;
                if (lg != null && lg.sideAtt != null) { lg.ToggleSide(); Haptic(0.15f, 0.02f); }
            }
            if (Racking != null)
            {
                Racking.RackUpdate(this, input.GripHeld || input.Trigger > 0.3f);
                if (!Racking.IsRacking(this)) Racking = null;
            }
            else if (input.GripDown && Held == null && _interact == null) TryGrab();
            // grip held, then the trigger: a second try (the backpack needs both - whichever you press first)
            else if (input.TriggerDown && input.GripHeld && Held == null && _interact == null && rig.Gear != null && rig.Gear.PackSlot.item != null) TryGrab();
            else if (!input.GripHeld && (Held != null || _interact != null)) Release();
            if (Climbing && (!input.GripHeld || Held != null)) { Climbing = false; rig.OnClimbReleased(this); }

            if (Held != null)
            {
                if (!HeldAsSecondary) Held.HeldUpdate(this, input);
                else if (Held is Gun g) g.SupportUpdate(this);
                if (Held != null && !HeldAsSecondary && Held.slot != null) { Held = null; }
            }
            UpdateHints();
            UpdateSlotMarker();
            UpdateLaser(input);
            UpdateRemote(input);
            UpdateFingers(input);
        }

        Transform _slotMark;
        /// <summary>Holding something near a rack / shelf / holster spot that will take it: a green marker shows where it goes if you let go.</summary>
        void UpdateSlotMarker()
        {
            Slot target = null;
            if (Held != null && !HeldAsSecondary && Held.primaryHand == this && !rig.Dead) target = Slot.FindFor(Held, Held.transform.position);
            if (target == null || target.item != null) { if (_slotMark != null) _slotMark.gameObject.SetActive(false); return; }
            if (_slotMark == null)
            {
                _slotMark = new MB().Sphere(Vector3.zero, Vector3.one * 0.035f, 10, Color.white, 0f, 0.5f)
                    .Build("SlotMarker", null, Mats.Lit("slot_marker", new Color(0.3f, 1f, 0.4f), 0f, 0.5f, null, null, 0, 0, false, 0.5f, new Color(0.4f, 2.2f, 0.6f))).transform;
                foreach (var c in _slotMark.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            _slotMark.gameObject.SetActive(true);
            _slotMark.position = target.transform.position;
            _slotMark.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 8f));
        }

        const float SnapDist = 0.07f;
        Vector3 _aim;   // where the physics hand was sent last step
        bool _snapOk = true;

        /// <summary>
        /// Called right before the frame is drawn (VR): put the hand - and whatever it holds - exactly on the controller,
        /// so it never trails behind. If a wall is holding the physics hand back, the hand stays against the wall.
        /// </summary>
        public void RenderSnap(Vector3 tp, Quaternion tr)
        {
            if (rig.Dead || rb == null || !_snapOk) return;
            tr = Compat.SafeRot(tr);
            tp += _sag + tr * _recoilPos + _climbPos;
            tr = Compat.SafeRot(tr * Quaternion.Euler(_recoilRot + _climbRot + Tremor()));
            if ((rb.position - tp).sqrMagnitude > 0.6f * 0.6f) return;   // a big jump (teleport, respawn): let physics catch up first
            transform.SetPositionAndRotation(tp, tr);
        }

        /// <summary>After BOTH hands are snapped: put what this hand holds on them (a two-handed gun needs both hands' latest spots).</summary>
        public void RefreshVisual() { if (rig != null && !rig.Dead) UpdateVisualPose(); }

        public void RenderSnapHeld()
        {
            if (rig.Dead || rb == null) return;
            var held = Held != null && !HeldAsSecondary ? Held : null;
            if (held != null && held.Held && held.primaryHand == this && held.rb != null)
            {
                held.GetHeldPose(out var p, out var r);
                held.KeepOutOfWalls(ref p, r);
                if (!float.IsNaN(p.x)) held.transform.SetPositionAndRotation(p, Compat.SafeRot(r));
            }
            UpdateVisualPose();
        }

        void FixedUpdate()
        {
            if (rig == null) return;
            float dt = Time.fixedDeltaTime;
            Vector3 tp = TrackedPosition;
            Quaternion tr = Compat.SafeRot(TrackedRotation);
            // weight: heavy long guns held one-handed sag and lag (ragdoll feel)
            var held = Held != null && !HeldAsSecondary ? Held : null;
            float heavy = 0f;
            if (held != null && held.rb != null && held.secondaryHand == null && held is Gun gg && gg.g.TwoHanded) heavy = Mathf.Clamp01(held.rb.mass / 9f);
            _sag = Vector3.Lerp(_sag, Vector3.down * heavy * 0.06f, dt * 6f);
            tp += _sag;
            tp += tr * _recoilPos + _climbPos;
            tr = Compat.SafeRot(tr * Quaternion.Euler(_recoilRot + _climbRot + Tremor()));
            // the hand draws exactly on the controller unless something solid stopped it last step (it ended up well short
            // of where it was sent): then it stays against the wall. Comparing with the aim - not with the controller -
            // means walking and running never count as "blocked", however fast you go.
            _snapOk = (rb.position - _aim).sqrMagnitude < SnapDist * SnapDist;

            if (!XRInputs.VR || rig.Dead)
            {
                rb.isKinematic = true;
                _aim = tp;
                rb.MovePosition(tp);
                rb.MoveRotation(tr);
            }
            else
            {
                rb.isKinematic = false;
                Vector3 delta = tp - rb.position;
                if (delta.magnitude > 0.5f) _stuckTime += dt; else _stuckTime = 0;
                // stuck on something for a moment: jump to the controller - but never through a wall
                if ((_stuckTime > 0.4f || delta.magnitude > 1.5f) && !rig.CanReach(tp)) { _stuckTime = 0; if (delta.magnitude > 1.5f) { rb.position = rig.SafeEye(); rb.SetVel(Vector3.zero); _aim = rb.position; } }
                else if (_stuckTime > 0.4f || delta.magnitude > 1.5f) { rb.position = tp; rb.rotation = tr; rb.SetVel(Vector3.zero); _stuckTime = 0; _aim = tp; }
                else
                {
                    // carry the hand along with your body's movement, then close the gap to the controller; a heavy gun
                    // held one-handed only lags your arm movements, never your walking
                    Vector3 carry = rig.MoveVelocity;
                    Vector3 v = carry + Vector3.ClampMagnitude(delta / dt - carry, 30f) * (1f - heavy * 0.35f);
                    rb.SetVel(v);
                    _aim = rb.position + v * dt;
                    Quaternion dq = tr * Quaternion.Inverse(rb.rotation);
                    dq.ToAngleAxis(out float ang, out Vector3 axis);
                    if (ang > 180f) ang -= 360f;
                    if (Mathf.Abs(ang) > 0.01f && !float.IsNaN(axis.x)) rb.angularVelocity = axis * (ang * Mathf.Deg2Rad / dt) * 0.9f;
                    else rb.angularVelocity = Vector3.zero;
                }
            }
            Velocity = (rb.position - _lastPos) / dt;
            _lastPos = rb.position;

            // drive the held item from the (physics) hand
            if (held != null && held.Held && held.primaryHand == this)
            {
                held.GetHeldPose(out var p, out var r);
                held.KeepOutOfWalls(ref p, r);
                if (!float.IsNaN(p.x)) held.rb.MovePosition(p);
                held.rb.MoveRotation(Compat.SafeRot(r));
            }
        }

        void LateUpdate() { UpdateVisualPose(); }

        /// <summary>
        /// Empty hand: wrap it around the controller's grip the way you actually hold it - palm facing in, knuckles out,
        /// fingers along the handle - instead of a flat hand floating over it. In VR the controller's grip pose
        /// sits in the middle of the handle, so the palm is pushed out by half a handle's width.
        /// </summary>
        void RestPose()
        {
            if (XRInputs.VR)
            {
                float s = left ? -1 : 1;
                // fingers along the controller's aim direction (where your real index finger points), not up its handle
                var aim = AimLocal;
                _visual.localRotation = aim * Quaternion.Euler(0, 0, s * -90f);
                _visual.localPosition = aim * new Vector3(s * 0.028f, -0.005f, -0.015f);
            }
            else { _visual.localPosition = Vector3.zero; _visual.localRotation = Quaternion.identity; }
        }

        /// <summary>On a golf cart's steering wheel: the hand is drawn gripping the rim here (set by the driver's rig each frame).</summary>
        public bool OnWheel; public Vector3 WheelPos; public Quaternion WheelRot;

        void UpdateVisualPose()
        {
            if (OnWheel && Held == null) { _visual.position = WheelPos; _visual.rotation = WheelRot; return; }
            // snap the hand visual onto the grip when holding something
            if (Held != null && Held.Held)
            {
                if (!HeldAsSecondary && Held.SnapGrip && !Held.AimedHold) RestPose();   // a mag, grenade, knife...: your fist wraps it like the controller
                else if (!HeldAsSecondary && Held.SnapGrip)
                {
                    // a real pistol-grip hold: palm against the side of the grip, the back of your hand facing out,
                    // knuckles running down the grip's slant, thumb on top and fingers wrapped round the front
                    float s = left ? -1f : 1f;
                    var t = Held.transform;
                    Quaternion slant = t.rotation * Quaternion.Euler(16f, 0f, 0f);
                    _visual.rotation = Quaternion.LookRotation(slant * Vector3.forward, slant * Vector3.right * s);
                    _visual.position = Held.grip.position + slant * new Vector3(s * 0.03f, 0.004f, -0.012f);
                }
                else if (HeldAsSecondary && Held is Gun fg && fg.GripKind >= 0)
                {
                    // a foregrip: your fist closes round it like a pistol grip - straight down on a vertical grip, leaning
                    // forward along the ramp on an angled grip (thumb up on the handguard), high and short on a stubby
                    float s = left ? -1f : 1f;
                    int k = fg.GripKind;
                    float pitch = k == 1 ? -42f : k == 2 ? 2f : 8f;
                    Quaternion slant = fg.transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
                    _visual.rotation = Quaternion.LookRotation(slant * Vector3.forward, slant * Vector3.right * s);
                    _visual.position = fg.secondGrip.position + slant * new Vector3(s * 0.03f, k == 1 ? 0.012f : 0.004f, -0.012f);
                }
                else if (HeldAsSecondary && Held.secondGrip != null)
                {
                    // no foregrip: hold the handguard from UNDERNEATH - palm up against it, the back of your hand facing the
                    // ground, fingers wrapping up the far side and thumb up the near side (it used to be rolled 70 degrees
                    // onto the side of the handguard). With a foregrip fitted, the branch above wraps your fist round it.
                    var t = Held.transform;
                    _visual.rotation = Quaternion.LookRotation(t.forward, -t.up) * Quaternion.Euler(0, 0, left ? -18f : 18f);
                    _visual.position = Held.secondGrip.position - t.up * 0.028f - t.forward * 0.01f;
                }
                else RestPose();
            }
            else if (Racking != null && Racking.rig.slide != null)
            {
                _visual.position = Racking.RackPoint;   // on the slide / charging handle itself, which moves back as you pull
                _visual.rotation = Racking.transform.rotation * Quaternion.Euler(0, 0, left ? 90 : -90);
            }
            else RestPose();
        }

        // ---------------------------------------------------------------------------------- grabbing
        void TryGrab()
        {
            Vector3 p = Anchor.position;
            // driving: a grip near the wheel / handlebars takes the wheel - never your slung rifle, your holster or
            // something lying on the seat, which are all right there in your lap
            if (rig.Driving != null && !rig.Passenger && rig.Driving.WheelNear(TrackedPosition, 0.45f)) return;
            // 1. support grip on the gun held in the other hand
            var otherGun = Other.Held as Gun;
            if (otherGun != null && !Other.HeldAsSecondary)
            {
                if (otherGun.TryStartRack(this)) { BeginRack(otherGun); return; }
                // (3.52) a grab low on its mag pulls the mag out (step 2) instead of taking the support grip
                var gm = otherGun.mag;
                bool atMag = gm != null && gm.CanGrab(this) && Vector3.Distance(gm.transform.TransformPoint(gm.localBounds.center), p) < 0.09f;
                if (!atMag && otherGun.secondGrip != null && (Vector3.Distance(p, otherGun.secondGrip.position) < 0.16f || (otherGun.rig.foregrip != null && Vector3.Distance(p, otherGun.rig.foregrip.position) < 0.16f)))
                {
                    Held = otherGun; HeldAsSecondary = true;
                    otherGun.secondaryHand = this;
                    Haptic(0.2f, 0.03f);
                    return;
                }
            }
            // 2. nearest item / interactable. Something lying in the world always beats your own holstered gear (reaching
            // down past your hip for a gun on the floor used to pull your holstered pistol or your armour instead), and
            // with your hand down by the floor your body gear isn't picked at all.
            // (relative to your head too: crouched, your hip pouches are below half a metre and used to count as "the floor")
            bool low = p.y < Mathf.Min(rig.transform.position.y + 0.5f, rig.Head.position.y - 0.88f);
            // reaching for the floor: a bigger reach, a little up from the hand (with the play area's floor a few cm off,
            // your hand ends up just under things lying on the floor and missed them)
            // (3.52) easier off the ground: a wider reach that also goes 30 cm on down below your hand, so you don't have to
            // get your hand right down to the floor
            int n = low ? Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.06f, p - Vector3.up * 0.3f, 0.2f, _overlap, ~0, QueryTriggerInteraction.Collide)
                        : Physics.OverlapSphereNonAlloc(p, 0.09f, _overlap, ~0, QueryTriggerInteraction.Collide);
            Item best = null, bestBody = null; Interactable bestI = null; float bd = 1e9f, bdb = 1e9f, bdi = 1e9f;
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                if (c == _col) continue;
                var it = c.GetComponentInParent<Item>();
                if (it != null)
                {
                    if (it == Other.Held && !Other.HeldAsSecondary) { if (it is Gun) continue; }
                    if (it is Magazine mm && mm.inGun != null && mm.inGun.Held && mm.inGun.primaryHand == this) continue;
                    if (!it.CanGrab(this)) continue;
                    if (!rig.CanReach(c.ClosestPoint(p))) continue;   // on the other side of a wall
                    float d = (c.ClosestPoint(p) - p).sqrMagnitude;
                    // a mag / sight / attachment fitted to a gun: never off a gun that's holstered or slung on you, and
                    // only when your hand is right on it (3.5 cm) - reaching for the gun, a pouch or something next to it
                    // used to pull the scope or the mag off
                    var mg = it.MountedOn;
                    if (mg != null)
                    {
                        if (!mg.Held && OnMyBody(mg)) continue;
                        float reach = it is CamItem ? 0.09f : it is Magazine && mg.Held ? 0.085f : it is Magazine ? 0.06f : 0.035f;   // a camera clipped on: easy to take back off; a mag (3.52: easier) from the gun in your other hand or one lying around
                        if (d > reach * reach) continue;
                        d += 0.01f;   // and anything else just as close wins
                    }
                    if (OnMyBody(it)) { if (!low && d < bdb) { bdb = d; bestBody = it; } continue; }
                    if (d < bd) { bd = d; best = it; }
                    continue;
                }
                var ia = c.GetComponentInParent<Interactable>();
                if (ia != null && ia.CanGrab(this))
                {
                    float d = (c.ClosestPoint(p) - p).sqrMagnitude;
                    if (d < bdi) { bdi = d; bestI = ia; }
                }
            }
            if (best == null) best = bestBody;
            if (best != null)
            {
                // gun held in the other hand: taking it swaps hands
                if (best.Held && best.primaryHand == Other) { Other.ForceRelease(true); }
                Grab(best);
                return;
            }
            if (bestI != null) { bestI.OnGrab(this); _interact = bestI; return; }
            // 3. gear on your body (holsters, back, pouches)
            var fromBody = low ? null : rig.Gear.TakeFromBody(this);   // reaching for the floor: never your own gear
            if (fromBody != null) { Grab(fromBody); return; }
            // 4. pointing at something further away: pull it into your hand (or open the crate you point at)
            if (_remote != null && _remote.primaryHand == null) { StartPull(_remote); return; }
            if (_remoteCrate != null && !_remoteCrate.opened) { _remoteCrate.Open(); Haptic(0.3f, 0.04f); return; }
            // 5. nothing to pick up: grab the world itself and climb (walls, ledges, ladders, fences, rocks)
            if (XRInputs.VR && RagoGame.I != null && CanClimbHere(p)) { Climbing = true; ClimbAnchor = TrackedPosition; ClimbStart = Time.time; Haptic(0.35f, 0.05f); }
        }

        /// <summary>In one of your own holsters / pouches / your hands (not lying in the world or on a rack).</summary>
        bool OnMyBody(Item it)
        {
            if (it == null) return false;
            if (it.Held) return true;
            if (it.slot == null) return false;
            var t = it.slot.transform;
            return t.IsChildOf(rig.transform) || (rig.Body != null && t.IsChildOf(rig.Body.transform)) || (rig.Gear != null && rig.Gear.Root != null && t.IsChildOf(rig.Gear.Root));   // holsters, pouches, your worn bag
        }

        static int _worldMask = 0;
        bool CanClimbHere(Vector3 p)
        {
            if (p.y < rig.transform.position.y + 0.55f) return false;   // not the floor at your feet
            if (_worldMask == 0)
            {
                _worldMask = ~0;
                foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) _worldMask &= ~(1 << l);
            }
            int n = Physics.OverlapSphereNonAlloc(p, 0.07f, _overlap, _worldMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                if (c == null || c == _col || c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // not loose physics props
                if (c.GetComponentInParent<Item>() != null || c.GetComponentInParent<LootCrate>() != null) continue;
                if (c.transform.IsChildOf(rig.transform)) continue;
                // never other people: gripping another player (or a raider) grabbed onto them and dragged you around
                if (c.GetComponentInParent<HitZone>() != null || c.GetComponentInParent<RemotePlayer>() != null || c.GetComponentInParent<Npc>() != null) continue;
                return true;
            }
            return false;
        }

        public void StopClimbing() { if (Climbing) { Climbing = false; rig.OnClimbReleased(this); } }

        Interactable _interact;

        public void Grab(Item it)
        {
            if (it == null) return;
            if (it.Held && it.primaryHand != null && it.primaryHand != this) it.primaryHand.ForceRelease(true);
            Held = it; HeldAsSecondary = false;
            it.OnGrab(this);
            if (Racking != null) return;
            Compat.IgnoreAll(it.gameObject, gameObject);
            Compat.IgnoreAll(it.gameObject, Other.gameObject);
        }

        public void BeginRack(Gun g) { Racking = g; Held = null; }

        public void BecomePrimary(Item it) { Held = it; HeldAsSecondary = false; }

        readonly (float t, Vector3 p)[] _trk = new (float, Vector3)[12]; int _trkN;
        /// <summary>How fast your real hand was moving as you let go: over the last ~70 ms of the controller's track. (The
        /// physics hand only moves on physics steps and can be held back mid-swing, so a throw often read as "not moving"
        /// and the thing dropped at your feet.)</summary>
        Vector3 ThrowVelocity()
        {
            if (_trkN < 2) return Velocity;
            var now = _trk[(_trkN - 1) % _trk.Length];
            for (int k = 2; k <= Mathf.Min(_trkN, _trk.Length); k++)
            {
                var e = _trk[(_trkN - k) % _trk.Length];
                float dt = now.t - e.t;
                if (dt >= 0.07f || k == Mathf.Min(_trkN, _trk.Length)) return dt > 0.005f ? (now.p - e.p) / dt : Velocity;
            }
            return Velocity;
        }

        void Release()
        {
            if (_interact != null) { _interact.OnRelease(this); _interact = null; }
            if (Held == null) return;
            var it = Held;
            Held = null;
            if (HeldAsSecondary) { HeldAsSecondary = false; it.secondaryHand = null; return; }
            Vector3 v = ThrowVelocity(), w = rb.angularVelocity;
            if (v.sqrMagnitude < Velocity.sqrMagnitude) v = Velocity;
            // a real throw gets a boost (VR throws feel weak without one); just letting go stays a gentle drop
            float sp = v.magnitude;
            it.OnRelease(this, v * (sp > 1.2f ? 1.45f : 1f), w);
        }

        /// <summary>Let go without throwing (used by code). keepItem: the item is being transferred somewhere.</summary>
        public void ForceRelease(bool keepItem = false)
        {
            if (Held == null) return;
            var it = Held;
            Held = null;
            if (HeldAsSecondary) { HeldAsSecondary = false; it.secondaryHand = null; return; }
            if (keepItem) { it.primaryHand = null; if (it.secondaryHand != null) { it.secondaryHand.Held = null; it.secondaryHand.HeldAsSecondary = false; it.secondaryHand = null; } return; }
            it.OnRelease(this, Velocity, Vector3.zero);
        }

        void Update()
        {
            if (_interact != null) _interact.HoldUpdate(this);
        }

        // ---------------------------------------------------------------------------------- hints / UI laser
        void UpdateHints()
        {
            if (Held == null || HeldAsSecondary) return;
            var s = Slot.FindFor(Held, Held.transform.position);
            if (s != null) s.Hint(true);
        }

        void UpdateLaser(HandInput input)
        {
            bool canPoint = Held == null && Racking == null && XRInputs.VR;
            WorldButton hit = null;
            Vector3 end = Vector3.zero;
            if (canPoint)
            {
                var ray = new Ray(Anchor.position + Anchor.forward * 0.05f, transform.rotation * AimLocal * Vector3.forward); // natural pointing angle
                if (Physics.Raycast(ray, out var h, 6f, ~0, QueryTriggerInteraction.Collide))
                {
                    hit = h.collider.GetComponent<WorldButton>();
                    end = h.point;
                }
                if (hit != null)
                {
                    _laser.enabled = true;
                    _laser.SetPosition(0, ray.origin); _laser.SetPosition(1, end);
                    _laserDot.SetActive(true); _laserDot.transform.position = end;
                    if (hit != _hoverBtn) { hit.Hover(); Haptic(0.08f, 0.01f); }
                    if (input.TriggerDown) { hit.Click(); Haptic(0.3f, 0.03f); }
                }
            }
            if (hit == null) { _laser.enabled = false; _laserDot.SetActive(false); }
            _hoverBtn = hit;
        }

        float _curl, _trig;
        void UpdateFingers(HandInput input)
        {
            float targetCurl = Held != null || Racking != null || OnWheel ? 1f : input.Grip;
            // on a gun: the index finger rests curled on the trigger (it hovered nearly straight in front of it) and
            // squeezes it as you pull
            float targetTrig = Held is Gun && !HeldAsSecondary ? 0.58f + input.Trigger * 0.32f : input.Trigger;
            _curl = Mathf.MoveTowards(_curl, targetCurl, Time.deltaTime * 10f);
            _trig = Mathf.MoveTowards(_trig, targetTrig, Time.deltaTime * 12f);
            for (int f = 0; f < _fingers.Count; f++)
            {
                float c = f == 0 ? _trig : _curl;
                if (Held is Gun && !HeldAsSecondary && f == 0) c = _trig;
                for (int k = 0; k < 3; k++) _fingers[f][k].localRotation = Quaternion.Euler(c * (k == 0 ? 70f : 80f), 0, 0);
            }
            _thumb.localRotation = Quaternion.Euler(_curl * 25f, (left ? 1 : -1) * (45f - _curl * 20f), (left ? -1 : 1) * 30f);
        }

        /// <summary>Where your body's forearm should end: the wrist of the hand you see (not the controller's centre).</summary>
        public Vector3 WristPosition => _visual != null ? _visual.TransformPoint(0, 0, -0.07f) : transform.position;
        public Quaternion WristRotation => _visual != null ? _visual.rotation : transform.rotation;

        // ---------------------------------------------------------------------------------- distance grab
        // Point an empty hand at an item up to 3.5 m away: it gets a glowing marker (and a little buzz). Squeeze grip
        // and it flies into your hand. Point at a closed crate and squeeze to open it.
        Item _remote, _pulling;
        LootCrate _remoteCrate;
        float _pullT;
        GameObject _remoteMark;
        static readonly RaycastHit[] _rhits = new RaycastHit[24];
        static int _remoteMask;
        const float RemoteRange = 3.5f;

        void UpdateRemote(HandInput input)
        {
            if (_pulling != null) { PullStep(input); return; }
            Item best = null; LootCrate crate = null;
            bool can = XRInputs.VR && Held == null && Racking == null && _interact == null && !Climbing && _hoverBtn == null && !rig.Dead;
            if (can)
            {
                if (_remoteMask == 0)
                {
                    _remoteMask = ~0;
                    foreach (int l in new[] { Layers.Hands, Layers.LocalBody, Layers.Player }) if (l > 0) _remoteMask &= ~(1 << l);
                }
                Vector3 dir = transform.rotation * AimLocal * Vector3.forward;   // same natural pointing angle as the laser
                Vector3 origin = Anchor.position + dir * 0.05f;
                int n = Physics.SphereCastNonAlloc(origin, 0.13f, dir, _rhits, RemoteRange, _remoteMask, QueryTriggerInteraction.Collide);   // (3.52) a fatter sweep: easier to point at
                System.Array.Sort(_rhits, 0, n, RemoteCmp.Inst);
                for (int i = 0; i < n; i++)
                {
                    var c = _rhits[i].collider;
                    if (c == null || c == _col) continue;
                    var it = c.GetComponentInParent<Item>();
                    if (it != null)
                    {
                        if (_rhits[i].distance < 0.2f) break;   // within normal reach: a normal grab handles it
                        if (it.primaryHand != null || !it.CanGrab(this)) continue;
                        if (it.slot != null && it.slot.transform.IsChildOf(rig.Gear.Root)) continue;   // your own holsters/pouches
                        if (it is Magazine mm && mm.inGun != null) continue;
                        if (it is SightItem si && si.onGun != null) continue;
                        if (it is AttachItem ai && ai.onGun != null) continue;
                        if (it is CamItem ci && ci.onGun != null) continue;
                        best = it; break;
                    }
                    var lc = c.GetComponentInParent<LootCrate>();
                    if (lc != null) { if (!lc.opened) { crate = lc; break; } continue; }   // an open crate: look for the loot inside it
                    if (c.isTrigger || c.GetComponentInParent<HitZone>() != null) continue;
                    if (_rhits[i].normal.y > 0.6f) continue;   // a floor or tabletop the sweep grazes on the way to a crate sitting on it doesn't block
                    break;   // a wall blocks the view
                }
                // (3.52) pointing at the floor next to something: take the nearest loose thing within 35 cm of where you point
                if (best == null && crate == null && Physics.Raycast(origin, dir, out var fh, RemoteRange, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && fh.normal.y > 0.6f)
                {
                    int m = Physics.OverlapSphereNonAlloc(fh.point + Vector3.up * 0.12f, 0.35f, _overlap, _remoteMask, QueryTriggerInteraction.Collide);
                    float bd2 = 1e9f;
                    for (int i = 0; i < m; i++)
                    {
                        var it = _overlap[i].GetComponentInParent<Item>();
                        if (it == null || it.Held || it.primaryHand != null || it.slot != null || it.MountedOn != null || !it.CanGrab(this)) continue;
                        float d2 = (it.transform.position - fh.point).sqrMagnitude;
                        if (d2 < bd2 && rig.CanReach(it.transform.position)) { bd2 = d2; best = it; }
                    }
                }
            }
            if ((best != null && best != _remote) || (crate != null && crate != _remoteCrate)) Haptic(0.12f, 0.015f);
            _remote = best; _remoteCrate = crate;
            if (_remoteMark == null)
                _remoteMark = new MB().Sphere(Vector3.zero, Vector3.one * 0.5f, 10, Color.white).Build("RemoteMark", null, Mats.Unlit("remote_mark", new Color(1f, 0.85f, 0.4f, 0.55f), Mats.Glow, Mats.BlendKind.Additive, false, 60, true));
            Vector3 at = best != null ? best.transform.TransformPoint(best.localBounds.center) : crate != null ? crate.transform.position + Vector3.up * 0.4f : Vector3.zero;
            bool show = best != null || crate != null;
            if (_remoteMark.activeSelf != show) _remoteMark.SetActive(show);
            if (show)
            {
                _remoteMark.transform.position = at;
                float pulse = 0.09f + Mathf.Sin(Time.time * 8f) * 0.015f;
                _remoteMark.transform.localScale = Vector3.one * pulse;
            }
        }

        class RemoteCmp : IComparer<RaycastHit> { public static readonly RemoteCmp Inst = new RemoteCmp(); public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance); }

        /// <summary>The item this hand is pulling towards itself (distance grab), if any.</summary>
        public Item Pulling => _pulling;

        void StartPull(Item it)
        {
            if (_pulling != null && _pulling != it && _pulling.primaryHand == null) _pulling.Drop(Vector3.zero, Vector3.zero);   // a pull cut short: it falls, it doesn't hang in the air
            if (it.slot != null) it.slot.Release(it);
            it.rb.isKinematic = true;
            it.SetCollidersTrigger(true);
            it.transform.SetParent(null, true);
            _pulling = it; _pullT = 0f;
            if (_remoteMark != null) _remoteMark.SetActive(false);
            Sfx.Play(Synth.Grab(1), it.transform.position, 0.35f, 1.3f, 0.5f, 8f);
            Haptic(0.25f, 0.04f);
        }

        void PullStep(HandInput input)
        {
            var it = _pulling;
            if (it == null || it.primaryHand != null) { _pulling = null; return; }
            if (!input.GripHeld)
            {
                // let go mid-flight: it drops where it is
                _pulling = null;
                it.Drop(Vector3.zero, Vector3.zero);
                return;
            }
            _pullT += Time.deltaTime;
            Vector3 target = Anchor.position;
            float dist = Vector3.Distance(it.transform.position, target);
            float step = Mathf.Max(7f, dist * 9f) * Time.deltaTime;
            it.transform.position = Vector3.MoveTowards(it.transform.position, target, step);
            it.transform.rotation = Quaternion.Slerp(it.transform.rotation, Anchor.rotation, Time.deltaTime * 10f);
            if (dist < 0.12f || _pullT > 0.7f)
            {
                _pulling = null;
                Grab(it);
                Haptic(0.35f, 0.05f);
            }
        }

        public void SetVisible(bool v) { _visual.gameObject.SetActive(v); }
    }

    /// <summary>Things you grab that aren't items: levers, card readers, hack panels, crate lids.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual bool CanGrab(Hand h) => true;
        public virtual void OnGrab(Hand h) { }
        public virtual void HoldUpdate(Hand h) { }
        public virtual void OnRelease(Hand h) { }
    }
}
