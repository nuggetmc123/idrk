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
        public Quaternion TrackedRotation { get; private set; }
        public Hand Other => left ? rig.HandR : rig.HandL;
        public static float GripPitch = 0f; // tweakable grip angle (bunker settings)

        Transform _visual;
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
            palm.Sub(0).Cyl(new Vector3(0, 0, -0.085f), new Vector3(0, 0, -0.13f), 0.032f, 0.03f, 12, body); // forearm stub
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

            if (Racking != null)
            {
                Racking.RackUpdate(this, input.GripHeld);
                if (!Racking.IsRacking(this)) Racking = null;
            }
            else if (input.GripDown && Held == null && _interact == null) TryGrab();
            else if (!input.GripHeld && (Held != null || _interact != null)) Release();

            if (Held != null)
            {
                if (!HeldAsSecondary) Held.HeldUpdate(this, input);
                else if (Held is Gun g) g.SupportUpdate(this);
                if (Held != null && !HeldAsSecondary && Held.slot != null) { Held = null; }
            }
            UpdateHints();
            UpdateLaser(input);
            UpdateFingers(input);
        }

        const float SnapDist = 0.07f;
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
            if ((rb.position - tp).sqrMagnitude > SnapDist * SnapDist) return;
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
            _snapOk = (rb.position - tp).sqrMagnitude < SnapDist * SnapDist;

            if (!XRInputs.VR || rig.Dead)
            {
                rb.isKinematic = true;
                rb.MovePosition(tp);
                rb.MoveRotation(tr);
            }
            else
            {
                rb.isKinematic = false;
                Vector3 delta = tp - rb.position;
                if (delta.magnitude > 0.5f) _stuckTime += dt; else _stuckTime = 0;
                if (_stuckTime > 0.4f || delta.magnitude > 1.5f) { rb.position = tp; rb.rotation = tr; rb.SetVel(Vector3.zero); _stuckTime = 0; }
                else
                {
                    rb.SetVel(Vector3.ClampMagnitude(delta / dt, 30f) * (1f - heavy * 0.35f) + rig.PlatformVelocity * 0f);
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
                _visual.localRotation = Quaternion.Euler(0, 0, s * -90f);
                _visual.localPosition = new Vector3(s * 0.028f, -0.005f, -0.015f);
            }
            else { _visual.localPosition = Vector3.zero; _visual.localRotation = Quaternion.identity; }
        }

        void UpdateVisualPose()
        {
            // snap the hand visual onto the grip when holding something
            if (Held != null && Held.Held)
            {
                if (!HeldAsSecondary && Held.SnapGrip)
                {
                    _visual.position = Held.grip.position;
                    _visual.rotation = Held.grip.rotation * Quaternion.Inverse(Anchor.localRotation);
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
                _visual.position = Racking.rig.slide.position;
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
            if (fromBody != null) Grab(fromBody);
        }

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
                var ray = new Ray(Anchor.position + Anchor.forward * 0.05f, transform.rotation * Quaternion.Euler(30, 0, 0) * Vector3.forward); // natural pointing angle
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
