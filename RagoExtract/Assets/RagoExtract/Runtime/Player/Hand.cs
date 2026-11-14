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

        public void AddRecoil(float strength, bool rifle)
        {
            _recoilVel += new Vector3(Random.Range(-0.15f, 0.15f) * strength, 0.25f * strength, -1.0f * strength) * (rifle ? 0.7f : 1f);
            _recoilAngVel += new Vector3(-(rifle ? 220f : 380f) * strength, Random.Range(-60f, 60f) * strength, Random.Range(-40f, 40f) * strength);
        }

        // ---------------------------------------------------------------------------------- per frame
        public void Tick(HandInput input, Vector3 targetPos, Quaternion targetRot)
        {
            Input = input;
            TrackedPosition = targetPos;
            TrackedRotation = Compat.SafeRot(targetRot);
            Anchor.localRotation = Quaternion.Euler(GripPitch, 0, 0);

            if (rig.Dead) { if (Held != null) ForceRelease(); return; }

            // recoil spring
            float dt = Time.deltaTime;
            _recoilVel += (-_recoilPos * 260f - _recoilVel * 22f) * dt;
            _recoilPos += _recoilVel * dt;
            _recoilAngVel += (-_recoilRot * 280f - _recoilAngVel * 24f) * dt;
            _recoilRot += _recoilAngVel * dt;

            // rack with the free hand on the slide / charging handle: hold the TRIGGER (or grip), pull back, let go
            if (Racking == null && Held == null && _interact == null && input.TriggerDown && Other.Held is Gun rg && !Other.HeldAsSecondary && rg.TryStartRack(this))
            {
                BeginRack(rg); Haptic(0.2f, 0.03f);
            }
            if (Racking != null)
            {
                Racking.RackUpdate(this, input.GripHeld || input.Trigger > 0.3f);
                if (!Racking.IsRacking(this)) Racking = null;
            }
            else if (input.GripDown && Held == null && _interact == null) TryGrab();
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
            tp += _sag + tr * _recoilPos * 0.04f;
            tr = Compat.SafeRot(tr * Quaternion.Euler(_recoilRot * 0.05f));
            if ((rb.position - tp).sqrMagnitude > 0.6f * 0.6f) return;   // a big jump (teleport, respawn): let physics catch up first
            transform.SetPositionAndRotation(tp, tr);
            var held = Held != null && !HeldAsSecondary ? Held : null;
            if (held != null && held.Held && held.primaryHand == this && held.rb != null)
            {
                held.GetHeldPose(out var p, out var r);
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
            tp += tr * _recoilPos * 0.04f;
            tr = Compat.SafeRot(tr * Quaternion.Euler(_recoilRot * 0.05f));
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
                if (_stuckTime > 0.4f || delta.magnitude > 1.5f) { rb.position = tp; rb.rotation = tr; rb.SetVel(Vector3.zero); _stuckTime = 0; _aim = tp; }
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

        void UpdateVisualPose()
        {
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
                else if (HeldAsSecondary && Held.secondGrip != null)
                {
                    _visual.position = Held.secondGrip.position;
                    _visual.rotation = Quaternion.LookRotation(Held.transform.forward, Held.transform.up) * Quaternion.Euler(0, 0, left ? 70 : -70);
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
            // 1. support grip on the gun held in the other hand
            var otherGun = Other.Held as Gun;
            if (otherGun != null && !Other.HeldAsSecondary)
            {
                if (otherGun.TryStartRack(this)) { BeginRack(otherGun); return; }
                if (otherGun.secondGrip != null && Vector3.Distance(p, otherGun.secondGrip.position) < 0.16f)
                {
                    Held = otherGun; HeldAsSecondary = true;
                    otherGun.secondaryHand = this;
                    Haptic(0.2f, 0.03f);
                    return;
                }
            }
            // 2. nearest item / interactable
            int n = Physics.OverlapSphereNonAlloc(p, 0.09f, _overlap, ~0, QueryTriggerInteraction.Collide);
            Item best = null; Interactable bestI = null; float bd = 1e9f, bdi = 1e9f;
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
                    float d = (c.ClosestPoint(p) - p).sqrMagnitude;
                    // prefer magazines in guns and sights on guns when very close
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
            if (best != null)
            {
                // gun held in the other hand: taking it swaps hands
                if (best.Held && best.primaryHand == Other) { Other.ForceRelease(true); }
                Grab(best);
                return;
            }
            if (bestI != null) { bestI.OnGrab(this); _interact = bestI; return; }
            // 3. gear on your body (holsters, back, pouches)
            var fromBody = rig.Gear.TakeFromBody(this);
            if (fromBody != null) { Grab(fromBody); return; }
            // 4. pointing at something further away: pull it into your hand (or open the crate you point at)
            if (_remote != null && _remote.primaryHand == null) { StartPull(_remote); return; }
            if (_remoteCrate != null && !_remoteCrate.opened) { _remoteCrate.Open(); Haptic(0.3f, 0.04f); return; }
            // 5. nothing to pick up: grab the world itself and climb (walls, ledges, ladders, fences, rocks)
            // (not in the bunker or the Market: there's nothing to climb, and gripping a counter pinned you in place)
            if (XRInputs.VR && RagoGame.I != null && RagoGame.I.Area == Area.Raid && CanClimbHere(p)) { Climbing = true; ClimbAnchor = TrackedPosition; ClimbStart = Time.time; Haptic(0.35f, 0.05f); }
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

        void Release()
        {
            if (_interact != null) { _interact.OnRelease(this); _interact = null; }
            if (Held == null) return;
            var it = Held;
            Held = null;
            if (HeldAsSecondary) { HeldAsSecondary = false; it.secondaryHand = null; return; }
            Vector3 v = Velocity, w = rb.angularVelocity;
            it.OnRelease(this, v * 1.1f, w);
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
            float targetCurl = Held != null || Racking != null ? 1f : input.Grip;
            float targetTrig = Held is Gun && !HeldAsSecondary ? 0.4f + input.Trigger * 0.4f : input.Trigger;
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
                int n = Physics.SphereCastNonAlloc(origin, 0.09f, dir, _rhits, RemoteRange, _remoteMask, QueryTriggerInteraction.Collide);
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
                        best = it; break;
                    }
                    var lc = c.GetComponentInParent<LootCrate>();
                    if (lc != null) { if (!lc.opened) { crate = lc; break; } continue; }   // an open crate: look for the loot inside it
                    if (c.isTrigger || c.GetComponentInParent<HitZone>() != null) continue;
                    if (_rhits[i].normal.y > 0.6f) continue;   // a floor or tabletop the sweep grazes on the way to a crate sitting on it doesn't block
                    break;   // a wall blocks the view
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

        void StartPull(Item it)
        {
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
