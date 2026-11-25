using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>Serializable item state (stash, loadout, network).</summary>
    [System.Serializable]
    public class ItemData
    {
        public string id;
        public int rounds = -1;     // rounds in the loaded magazine (-1 = full)
        public bool chambered = true;
        public string sight = "";   // attached sight id
        public string muzzle = "", side = "", gripAtt = "";   // attached suppressor/brake, light/laser, foregrip
        public float sightZ, sideZ, gripZ;   // where along the rail each attachment was placed (metres from its default spot)
        public bool sideLeft;                // light / laser on the left side of the handguard
        public string skin = "";             // paint job from the bunker's paint booth ("" = factory finish)
        public bool cam;                     // guns: a creator cam clipped to the side
        public int count = 1;
        public string contents = "";  // backpacks: JSON of the items inside
        public int stashSec, stashSlot;   // bunker stash: which wall (1-based, 0 = none) and which spot on it, so it goes back exactly there
        public ItemData() { }
        public ItemData(string id) { this.id = id; }
        public ItemData Clone() => (ItemData)MemberwiseClone();
    }

    /// <summary>Base class for everything you can pick up.</summary>
    public class Item : MonoBehaviour
    {
        public ItemDef def;
        public Rigidbody rb;
        public Transform grip;        // snap pose for the primary hand (null = free grab)
        public Transform secondGrip;  // two-handed support point
        public Hand primaryHand, secondaryHand;
        public Slot slot;
        public int netId;             // shared id for multiplayer (0 = local only)
        public bool Consumed;         // used up (a med, a radio): never "dropped" to the other players on its way out
        public float holdOffsetScale = 1f;
        public Bounds localBounds;
        protected Collider[] cols;
        Pose _freeGrip;               // hand-relative pose for free grabs
        public bool Held => primaryHand != null;
        public bool InSlot => slot != null;
        public virtual bool SnapGrip => grip != null;
        public static readonly List<Item> All = new List<Item>();

        protected virtual void Awake()
        {
            All.Add(this);
        }
        protected virtual void OnDestroy() { All.Remove(this); }
        /// <summary>Set once the item has been saved into the stash and is on its way out (destroyed at the end of the frame).</summary>
        public bool Gone;
        /// <summary>Remove a swept item right now: off the list at once, so nothing that runs later this frame saves it twice.</summary>
        public static void Discard(Item it) { if (it == null) return; it.Gone = true; All.Remove(it); Object.Destroy(it.gameObject); }

        public void SetupPhysics(float mass)
        {
            rb = gameObject.GetOrAdd<Rigidbody>();
            rb.mass = mass;
            rb.SetDamping(0.1f, 0.3f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            cols = GetComponentsInChildren<Collider>();
            gameObject.SetLayerRecursive(Layers.Items);
        }

        public void RefreshColliders() { cols = GetComponentsInChildren<Collider>(); }

        public void SetCollidersTrigger(bool trig)
        {
            if (cols == null) return;
            foreach (var c in cols) if (c != null) c.isTrigger = trig;
        }

        // ------------------------------------------------------------ grab lifecycle
        public virtual bool CanGrab(Hand h) => true;
        /// <summary>The gun this is fitted to (a mag in it, a sight / attachment on it), or null.</summary>
        public virtual Gun MountedOn => null;

        public virtual void OnGrab(Hand h)
        {
            // picking something up from the world (not your own holsters/pouches): show what it is and what it's worth
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && primaryHand == null && def != null && !(this is Magazine) && (slot == null || pl.Gear == null || !slot.transform.IsChildOf(pl.Gear.Root)))
            {
                string c = "#" + ColorUtility.ToHtmlStringRGB(ItemDef.RarityColor(def.rarity));
                pl.Toast("<color=" + c + ">" + Label + "</color>  " + def.rarity + "  ~$" + Value.ToString("N0"));
            }
            if (slot != null) slot.Release(this);
            if (primaryHand == null)
            {
                primaryHand = h;
                rb.isKinematic = true;
                SetCollidersTrigger(true);
                transform.SetParent(null, true);
                if (!SnapGrip) _freeGrip = PalmGrip(h);
                NetEvents.ItemTaken(this);
            }
            else secondaryHand = h;
            Sfx.Play(Synth.Grab(Random.Range(0, 3)), transform.position, 0.4f, 1f, 0.5f, 8f);
            h.Haptic(0.25f, 0.04f);
        }

        Hand primaryHandWas;
        public virtual void OnRelease(Hand h, Vector3 vel, Vector3 angVel)
        {
            if (h == secondaryHand) { secondaryHand = null; return; }
            if (h != primaryHand) return;
            if (secondaryHand != null && !XRInputs.VR)
            {
                // desktop: letting go with the main hand drops the gun from both hands
                var s2 = secondaryHand; secondaryHand = null; s2.Held = null; s2.HeldAsSecondary = false;
                Desktop.LeftGripToggle = false;
            }
            if (secondaryHand != null)
            {
                // hand-over: the support hand becomes the primary hand
                var s = secondaryHand;
                secondaryHand = null;
                primaryHand = s;
                s.BecomePrimary(this);
                if (!SnapGrip) _freeGrip = PalmGrip(s);
                return;
            }
            primaryHand = null;
            primaryHandWas = h;   // let go by a hand (not thrown out by code): Drop keeps it on your side of walls
            // onto a gun first (a suppressor at the muzzle, a mag at the magwell, a sight on the rail) - otherwise a pouch
            // close by (your chest pouch is right where you fit a suppressor) took it instead and it never went on
            if (TryAttach()) return;
            if (Backpack.TryDropInto(this)) return;   // let go inside an open bag: it goes in
            if (TrySlot(h)) return;
            Drop(vel, angVel);
        }

        /// <summary>
        /// Mounted on / inserted into something (a sight on a rail, a mag in a gun): no interpolation, so it rides
        /// exactly with its parent instead of being drawn where its own physics body last was (that made it float).
        /// </summary>
        public void SetMounted(bool mounted)
        {
            if (rb == null) return;
            rb.isKinematic = true;
            rb.interpolation = mounted ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            rb.SetVel(Vector3.zero);
        }

        static readonly RaycastHit[] _floorHits = new RaycastHit[16];
        static int _worldMask = 0;
        static int WorldMask
        {
            get
            {
                if (_worldMask == 0)
                {
                    _worldMask = ~0;
                    foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) _worldMask &= ~(1 << l);
                }
                return _worldMask;
            }
        }

        /// <summary>Highest static floor surface under p within the given range (or NaN).</summary>
        static float FloorBelow(Vector3 from, float range)
        {
            int n = Physics.RaycastNonAlloc(from, Vector3.down, _floorHits, range, WorldMask, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            for (int i = 0; i < n; i++)
            {
                var h = _floorHits[i];
                if (h.collider.attachedRigidbody != null || h.normal.y < 0.5f) continue;
                if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
            }
            return best;
        }

        /// <summary>If this item's bottom is inside the floor (or table) under it, lift it out so it rests on top.</summary>
        public void Unsink()
        {
            float half = Mathf.Max(0.02f, localBounds.extents.y * transform.lossyScale.y);
            Physics.SyncTransforms();
            float floor = FloorBelow(transform.position + Vector3.up * 0.6f, 0.6f + half + 0.1f);
            if (!float.IsNaN(floor) && transform.position.y - half < floor) transform.position += Vector3.up * (floor - (transform.position.y - half) + 0.01f);
            _trackFree = false;
        }

        Vector3 _lastFree; bool _trackFree;

        protected virtual void FixedUpdate()
        {
            // anti-tunnelling: a loose item that went through a floor in one step (pushed out the wrong way after being
            // let go inside it, or just very fast) is put back on top of that floor
            if (rb == null || rb.isKinematic || primaryHand != null || slot != null) { _trackFree = false; return; }
            Vector3 now = rb.position;
            if (_trackFree && now.y < _lastFree.y - 0.01f)
            {
                Vector3 d = now - _lastFree;
                float len = d.magnitude;
                if (len > 0.001f && Physics.Raycast(_lastFree, d / len, out var hit, len + 0.05f, WorldMask, QueryTriggerInteraction.Ignore)
                    && hit.normal.y > 0.5f && hit.collider.attachedRigidbody == null && !hit.collider.transform.IsChildOf(transform))
                {
                    float half = Mathf.Max(0.02f, localBounds.extents.y);
                    rb.position = hit.point + Vector3.up * (half + 0.02f);
                    rb.SetVel(Vector3.zero);
                    now = rb.position;
                }
            }
            _lastFree = now; _trackFree = true;
        }

        public void Drop(Vector3 vel, Vector3 angVel)
        {
            transform.SetParent(RagoGame.I != null ? RagoGame.I.MapRoot : null, true);
            // let go partly inside a floor or table: lift it out first, or physics pushes it down through
            // measured from the item's real middle and its real (rotated) height, not its origin (a gun's is at the grip)
            Vector3 mid = transform.TransformPoint(localBounds.center);
            var m = Matrix4x4.Rotate(transform.rotation);
            Vector3 e = Vector3.Scale(localBounds.extents, transform.lossyScale);
            float half = Mathf.Max(0.02f, Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z);
            // (the ray starts above the floor you stand on: let go with your hand pushed deep under the floor - your real floor
            // a bit lower than the game's - and it used to start inside the floor, miss it, and the gun fell out underneath)
            float startY = mid.y + 0.4f;
            var who = RagoGame.I != null ? RagoGame.I.Player : null;
            if (who != null && Mathf.Abs(mid.y - who.transform.position.y) < 1.5f) startY = Mathf.Max(startY, who.transform.position.y + 0.3f);
            float floor = FloorBelow(new Vector3(mid.x, startY, mid.z), startY - mid.y + half + 0.05f);
            if (!float.IsNaN(floor) && mid.y - half < floor) transform.position += Vector3.up * (floor - (mid.y - half) + 0.01f);
            // let go with your hand (and the item) through a wall: it lands on your side
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && primaryHandWas != null)
            {
                Vector3 eye0 = pl.SafeEye(); mid = transform.TransformPoint(localBounds.center);
                Vector3 dd = mid - eye0; float len = dd.magnitude;
                if (len > 0.05f && Physics.Raycast(eye0, dd / len, out var wh, len, Ballistics.WallMask, QueryTriggerInteraction.Ignore)
                    && wh.collider.attachedRigidbody == null && wh.collider.GetComponentInParent<LootCrate>() == null)
                {
                    Vector3 ext = Vector3.Scale(localBounds.extents, transform.lossyScale);
                    float r = Mathf.Max(0.05f, Mathf.Max(Mathf.Abs(ext.x), Mathf.Max(Mathf.Abs(ext.y), Mathf.Abs(ext.z))));
                    transform.position += (wh.point - dd / len * Mathf.Min(r + 0.03f, wh.distance * 0.9f)) - mid;
                    vel = Vector3.zero;
                }
            }
            primaryHandWas = null;
            _trackFree = false;
            rb.isKinematic = false;
            SetCollidersTrigger(false);
            rb.SetVel(vel);
            rb.angularVelocity = angVel;
            NetEvents.ItemDropped(this);
        }

        /// <summary>Offer the item to the nearest free slot within reach.</summary>
        protected virtual bool TrySlot(Hand h)
        {
            var s = Slot.FindFor(this, transform.position);
            if (s != null) { s.Store(this); return true; }
            return false;
        }

        protected virtual bool TryAttach() => false;

        /// <summary>Target pose for the item given the hand anchor, including two-handed aim.</summary>
        /// <summary>Guns: held aimed where the controller points.</summary>
        public virtual bool AimedHold => false;

        /// <summary>
        /// How a hand holds an item without a fixed grip: small things (mags, meds, keycards, valuables) sit in the
        /// palm, squared up to the hand; big things (helmets, vests, bags) stay as grabbed, with no gap between the
        /// hand and the spot you grabbed.
        /// </summary>
        Pose PalmGrip(Hand h)
        {
            var a = h.Anchor;
            Quaternion rel = Quaternion.Inverse(a.rotation) * transform.rotation;
            Bounds wb = default; bool any = false;
            foreach (var c in GetComponentsInChildren<Collider>()) { if (!any) { wb = c.bounds; any = true; } else wb.Encapsulate(c.bounds); }
            if (!any) return new Pose(Quaternion.Inverse(a.rotation) * (transform.position - a.position), rel);
            float size = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
            if (size < 0.35f)
            {
                rel = AlongFist(rel, wb);
                Vector3 c = transform.InverseTransformPoint(wb.center);
                return new Pose(-(rel * c) + new Vector3(0, 0, 0.01f), rel);   // centred in the fist
            }
            // big: the point you grabbed goes into your hand
            Vector3 closest = a.position; float bd = float.MaxValue;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c is MeshCollider mc && !mc.convex) continue;
                var cp = c.ClosestPoint(a.position); float d = (cp - a.position).sqrMagnitude;
                if (d < bd) { bd = d; closest = cp; }
            }
            Vector3 shift = closest - a.position;   // pull the item onto the hand by this much
            return new Pose(Quaternion.Inverse(a.rotation) * (transform.position - shift - a.position), rel);
        }

        /// <summary>
        /// Small things are held with their longest side running through your fist (like a stick, a bottle, a box), the
        /// end that was up staying up; then squared up.
        /// </summary>
        Quaternion AlongFist(Quaternion rel, Bounds worldBounds)
        {
            // the item's longest local axis
            var lb = localBounds.size.sqrMagnitude > 0 ? localBounds.size : transform.InverseTransformVector(worldBounds.size);
            lb = new Vector3(Mathf.Abs(lb.x), Mathf.Abs(lb.y), Mathf.Abs(lb.z));
            Vector3 longAx = lb.x >= lb.y && lb.x >= lb.z ? Vector3.right : lb.y >= lb.z ? Vector3.up : Vector3.forward;
            Vector3 inHand = rel * longAx;                                  // where it points in the hand now
            Vector3 target = Vector3.forward * Mathf.Sign(Vector3.Dot(inHand, Vector3.forward) == 0 ? 1f : Vector3.Dot(inHand, Vector3.forward));
            var r = Quaternion.FromToRotation(inHand, target) * rel;         // long side along the fist axis
            return Snap90(r);
        }

        static Quaternion Snap90(Quaternion q)
        {
            Vector3 f = Axis(q * Vector3.forward);
            Vector3 u = Vector3.ProjectOnPlane(q * Vector3.up, f);
            if (u.sqrMagnitude < 1e-4f) return q;
            u = Axis(u);
            if (Mathf.Abs(Vector3.Dot(f, u)) > 0.5f) return q;
            return Quaternion.LookRotation(f, u);
        }

        static Vector3 Axis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (ay >= az) return new Vector3(0, Mathf.Sign(v.y), 0);
            return new Vector3(0, 0, Mathf.Sign(v.z));
        }

        /// <summary>The far end of the item (in its own space) - the muzzle of a gun, the tip of a knife: the part that
        /// would poke through a wall first.</summary>
        public virtual Vector3 LocalTip => localBounds.center + Vector3.forward * localBounds.extents.z;

        /// <summary>A held item never goes through a wall: if the line from your eyes to its tip runs into one, the whole
        /// thing slides back towards you until the tip is on your side (it was only the shots that were stopped before).</summary>
        public void KeepOutOfWalls(ref Vector3 pos, Quaternion rot)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null || float.IsNaN(pos.x)) return;
            Vector3 eye = pl.SafeEye();
            foreach (var local in new[] { LocalTip, localBounds.center })
            {
                Vector3 tip = pos + rot * Vector3.Scale(local, transform.lossyScale);
                Vector3 d = tip - eye; float len = d.magnitude;
                if (len < 0.02f) continue;
                if (Physics.Raycast(eye, d / len, out var h, len, Ballistics.WallMask, QueryTriggerInteraction.Ignore)
                    && (h.collider.attachedRigidbody == null || h.collider.attachedRigidbody.isKinematic) && h.collider.GetComponentInParent<Item>() == null)
                    pos -= tip - (h.point - d / len * 0.03f);
            }
        }

        public virtual void GetHeldPose(out Vector3 pos, out Quaternion rot)
        {
            var a = primaryHand.Anchor;
            if (SnapGrip)
            {
                Quaternion gr = Quaternion.Inverse(grip.localRotation);
                rot = primaryHand.HoldRotation(AimedHold) * gr;
                pos = a.position - rot * grip.localPosition;
                if (secondaryHand != null && secondGrip != null)
                {
                    Vector3 local = secondGrip.localPosition - grip.localPosition;
                    Vector3 cur = rot * local;
                    Vector3 want = secondaryHand.Anchor.position - a.position;
                    if (want.sqrMagnitude > 0.01f)
                    {
                        Quaternion adj = Quaternion.FromToRotation(cur, want);
                        rot = adj * rot;
                        // keep roll from the primary hand
                        pos = a.position - rot * grip.localPosition;
                    }
                }
            }
            else
            {
                rot = a.rotation * _freeGrip.rotation;
                pos = a.position + a.rotation * _freeGrip.position;
            }
        }

        public virtual void HeldUpdate(Hand h, HandInput input) { }
        public virtual void AnyUpdate() { }

        public virtual ItemData Save() => new ItemData(def != null ? def.id : "");
        public virtual void Load(ItemData d) { }
        public virtual string Label => def != null ? def.name : name;
        public virtual int Value => def != null ? def.price : 0;
        public virtual Vector3 SlotSize => localBounds.size;

        protected virtual void Update() { AnyUpdate(); }
    }

    /// <summary>Medkits, bandages and stims. Hold near your chest/head and pull the trigger.</summary>
    public class MedItem : Item
    {
        float _healLeft; bool _using;
        public override void HeldUpdate(Hand h, HandInput input)
        {
            var sd = def as SimpleDef;
            var pl = RagoGame.I.Player;
            if (_using || sd == null) return;
            bool atBody = Vector3.Distance(transform.position, pl.Head.position - Vector3.up * 0.2f) < 0.45f
                       || (pl.Body != null && pl.Body.Rig != null && Vector3.Distance(transform.position, pl.Body.Rig.chest.position) < 0.4f);
            // a syringe pressed against a broken arm / leg or a shot leg: fixes that limb (the only thing that does)
            if (input.TriggerDown && def.id == "stim" && pl.Vitals.TreatLimb(transform.position))
            {
                _using = true;
                Sfx.Play(Synth.Inject(), transform.position, 0.6f, 1f, 0.5f, 6f);
                h.Haptic(0.4f, 0.2f); Consumed = true; h.ForceRelease(); Destroy(gameObject);
                return;
            }
            if (input.TriggerDown && atBody)
            {
                _using = true;
                if (sd.energy > 0f)
                {
                    pl.Vitals.Heal(sd.heal); pl.Vitals.Stamina = pl.Vitals.MaxStamina; pl.Vitals.StimBoost = Mathf.Max(pl.Vitals.StimBoost, sd.energy);
                    Sfx.Play(Synth.Heal(), transform.position, 0.5f, 1.6f, 0.5f, 6f);
                    pl.Toast(def.name + ": stamina boosted for " + Mathf.RoundToInt(sd.energy) + " s");
                }
                else if (def.id == "stim") { pl.Vitals.Heal(sd.heal); pl.Vitals.Stamina = pl.Vitals.MaxStamina; pl.Vitals.StimBoost = 20f; Sfx.Play(Synth.Inject(), transform.position, 0.6f, 1f, 0.5f, 6f); }
                else
                {
                    Sfx.Play(Synth.Heal(), transform.position, 0.6f, 1f, 0.5f, 6f);
                    if (sd.stopsBleed) pl.Vitals.StopBleeding();
                    if (sd.heal >= 40f) { pl.Vitals.TreatInjuries(); pl.Toast(def.name + ": wounds treated - shaking and gut wound gone (a broken limb or shot leg needs a syringe)"); }   // a proper medkit
                    pl.Vitals.HealOverTime(sd.heal, sd.healTime);
                }
                h.Haptic(0.4f, 0.2f);
                Consumed = true;
                h.ForceRelease();
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Valuables, keycards, ammo boxes.</summary>
    public class LootItem : Item
    {
        static bool _ammoHint;
        public override void OnGrab(Hand h)
        {
            base.OnGrab(h);
            if (!_ammoHint && def is SimpleDef sd && sd.kind == ItemKind.Ammo)
            {
                _ammoHint = true;
                RagoGame.I?.Player?.Toast("Ammo: let go of it at the yellow AMMO pouch on either hip");
            }
        }

        protected override bool TrySlot(Hand h)
        {
            var sd = def as SimpleDef;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (sd != null && sd.kind == ItemKind.Ammo && pl != null && pl.Gear.NearMagPouch(transform.position))
            {
                pl.Gear.AddAmmo(sd.ammoCal, sd.ammoCount, sd.ammoType);
                Sfx.Play(Synth.Zipper(), transform.position, 0.5f, 1.2f, 0.5f, 6f);
                Destroy(gameObject);
                return true;
            }
            return base.TrySlot(h);
        }
    }
}
