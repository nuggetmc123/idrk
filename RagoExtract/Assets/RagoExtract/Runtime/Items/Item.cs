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
        public int count = 1;
        public string contents = "";  // backpacks: JSON of the items inside
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
                if (!SnapGrip)
                {
                    var a = h.Anchor;
                    _freeGrip = new Pose(Quaternion.Inverse(a.rotation) * (transform.position - a.position), Quaternion.Inverse(a.rotation) * transform.rotation);
                }
                NetEvents.ItemTaken(this);
            }
            else secondaryHand = h;
            Sfx.Play(Synth.Grab(Random.Range(0, 3)), transform.position, 0.4f, 1f, 0.5f, 8f);
            h.Haptic(0.25f, 0.04f);
        }

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
                if (!SnapGrip)
                {
                    var a = s.Anchor;
                    _freeGrip = new Pose(Quaternion.Inverse(a.rotation) * (transform.position - a.position), Quaternion.Inverse(a.rotation) * transform.rotation);
                }
                return;
            }
            primaryHand = null;
            if (Backpack.TryDropInto(this)) return;   // let go inside an open bag: it goes in
            if (TrySlot(h)) return;
            if (TryAttach()) return;
            Drop(vel, angVel);
        }

        public void Drop(Vector3 vel, Vector3 angVel)
        {
            transform.SetParent(RagoGame.I != null ? RagoGame.I.MapRoot : null, true);
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
        public virtual void GetHeldPose(out Vector3 pos, out Quaternion rot)
        {
            var a = primaryHand.Anchor;
            if (SnapGrip)
            {
                Quaternion gr = Quaternion.Inverse(grip.localRotation);
                rot = a.rotation * gr;
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
            if (input.TriggerDown && Vector3.Distance(transform.position, pl.Head.position - Vector3.up * 0.2f) < 0.45f)
            {
                _using = true;
                if (def.id == "stim") { pl.Vitals.Heal(sd.heal); pl.Vitals.Stamina = pl.Vitals.MaxStamina; pl.Vitals.StimBoost = 20f; Sfx.Play(Synth.Inject(), transform.position, 0.6f, 1f, 0.5f, 6f); }
                else
                {
                    Sfx.Play(Synth.Heal(), transform.position, 0.6f, 1f, 0.5f, 6f);
                    if (sd.stopsBleed) pl.Vitals.StopBleeding();
                    pl.Vitals.HealOverTime(sd.heal, sd.healTime);
                }
                h.Haptic(0.4f, 0.2f);
                h.ForceRelease();
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Valuables, keycards, ammo boxes.</summary>
    public class LootItem : Item
    {
        protected override bool TrySlot(Hand h)
        {
            var sd = def as SimpleDef;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (sd != null && sd.kind == ItemKind.Ammo && pl != null && pl.Gear.NearMagPouch(transform.position))
            {
                pl.Gear.AddAmmo(sd.ammoCal, sd.ammoCount);
                Sfx.Play(Synth.Zipper(), transform.position, 0.5f, 1.2f, 0.5f, 6f);
                Destroy(gameObject);
                return true;
            }
            return base.TrySlot(h);
        }
    }
}
