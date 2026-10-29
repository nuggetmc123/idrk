using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// One controller hand: follows the controller, grabs with grip, and
    /// reloads: hold the trigger with an empty hand at your waist to pull a
    /// fresh mag, then push it into the gun in your other hand.
    /// </summary>
    public class Hand : MonoBehaviour
    {
        public int index;
        public PlayerRig rig;
        public Transform attach;
        public Grabbable held;
        /// <summary>The gun in the other hand that this hand is steadying (its foregrip).</summary>
        public Gun supporting;
        bool heldByTrigger;
        GameObject glove;
        Vector3 lastPos;
        Quaternion lastRot = Quaternion.identity;
        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }

        public Hand Other => rig.hands[1 - index];
        public Gun HeldGun => held as Gun;

        public static Hand Create(PlayerRig rig, Transform parent, int index)
        {
            var go = new GameObject(index == 0 ? "LeftHand" : "RightHand");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hand>();
            h.index = index;
            h.rig = rig;
            h.attach = Prims.Empty("attach", go.transform).transform;
            h.BuildGlove();
            return h;
        }

        Transform handShape;
        Material bare;
        readonly List<Renderer> handParts = new List<Renderer>();
        /// <summary>Knuckle joints: index, middle, ring, little, then the thumb.</summary>
        readonly Transform[] fingers = new Transform[5];
        float curlIndex, curlGrip;
        readonly List<GameObject> gloveExtras = new List<GameObject>();
        int glovesOn = -1;

        /// <summary>
        /// Your own hand: the same white mannequin hand everyone else sees, a bit bigger than
        /// life so it reads well in VR. The palm faces inward like it does around the controller,
        /// and the fingers curl with the grip and trigger.
        /// </summary>
        void BuildGlove()
        {
            glove = Prims.Empty("glove", transform);
            float side = index == 0 ? -1 : 1;
            handShape = Avatar.BuildHandShape(glove.transform, side, 1.3f, out bare, handParts, fingers);
            handShape.localPosition = new Vector3(0, 0, -0.035f);
            SetGloves(0);
        }

        /// <summary>Curl the fingers: the index finger follows the trigger, the rest follow the grip.</summary>
        void AnimateFingers(VRInput input)
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime * 25f);
            bool holding = held != null;
            curlIndex = Mathf.Lerp(curlIndex, holding ? Mathf.Max(0.45f, input.TriggerValue(index)) : input.TriggerValue(index), k);
            curlGrip = Mathf.Lerp(curlGrip, holding ? 1f : input.GripValue(index), k);
            Avatar.CurlFingers(fingers, curlIndex, curlGrip, index == 0 ? -1 : 1);
        }

        /// <summary>Wear the gloves picked at the Outfitter (0 = bare white hands).</summary>
        public void SetGloves(int gloves)
        {
            if (handShape == null || gloves == glovesOn) return;
            glovesOn = gloves;
            foreach (var go in gloveExtras) if (go != null) Destroy(go);
            gloveExtras.Clear();
            foreach (var r in handParts) r.sharedMaterial = bare;
            if (gloves > 0) Cosmetics.GloveOn(handShape, handParts, gloves, gloveExtras);
        }

        /// <summary>Called by PlayerRig every frame after the pose update.</summary>
        public void Tick()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Velocity = Vector3.Lerp(Velocity, (transform.position - lastPos) / dt, 0.5f);
            var dq = transform.rotation * Quaternion.Inverse(lastRot);
            dq.ToAngleAxis(out float ang, out var axis);
            if (ang > 180) ang -= 360;
            AngularVelocity = float.IsNaN(axis.x) ? Vector3.zero : axis * (ang * Mathf.Deg2Rad / dt);
            lastPos = transform.position;
            lastRot = transform.rotation;
            if (held != null && held.holder != this) { held = null; }
            var input = VRInput.I;
            // steadying the other hand's gun: hold on while the grip is squeezed
            if (supporting != null && (input == null || !input.Grip(index) || supporting.holder != Other || !rig.Alive)) StopSupporting();
            // the hand stays visible while it holds something; the fingers wrap round it
            glove.SetActive(rig.Alive && !(held is Magazine));
            if (input != null) AnimateFingers(input);
            if (!rig.Alive) return;

            if (held == null)
            {
                if (input.GripDown(index)) TryGrab();
                else if (input.TriggerDown(index) && !rig.MenuOpen && !(UIPointer.I && UIPointer.I.OverUI)) TryPouch();
                return;
            }

            bool release = heldByTrigger ? !input.Trigger(index) : !input.Grip(index);
            if (release)
            {
                // let go with the grip hand while the other hand steadies it: the other hand keeps the gun
                if (held is Gun twoHanded && Other.supporting == twoHanded)
                {
                    Other.StopSupporting();
                    ReleaseQuietly();
                    Other.Grab(twoHanded, false);
                    return;
                }
                Release();
                return;
            }

            if (held is Magazine mag)
            {
                var gun = Other.HeldGun;
                if (gun != null && gun.CanTake(mag) && Vector3.Distance(mag.transform.position, gun.model.magwell.position) < 0.12f)
                {
                    held = null;
                    mag.holder = null;
                    gun.InsertMag(mag);
                    VRInput.I.Haptic(index, 0.5f, 0.05f);
                }
            }
        }

        void TryGrab()
        {
            // 0) steady the gun in the other hand by its foregrip (unless the hand is on the mag)
            var otherGun = Other.HeldGun;
            if (otherGun != null && otherGun.model.foregrip != null)
            {
                float dFore = Vector3.Distance(attach.position, otherGun.model.foregrip.position);
                float dMag = otherGun.hasMag ? Vector3.Distance(attach.position, otherGun.model.magwell.position) : 99f;
                if (dFore < 0.2f && (dMag > 0.13f || dFore < dMag))
                {
                    supporting = otherGun;
                    VRInput.I.Haptic(index, 0.25f, 0.03f);
                    AudioManager.Sfx("grab", attach.position, 0.4f);
                    return;
                }
            }
            // 1) pull the mag out of the gun in the other hand
            if (otherGun != null && otherGun.hasMag && Vector3.Distance(attach.position, otherGun.model.magwell.position) < 0.13f)
            {
                var mag = otherGun.TakeMag();
                if (mag != null) { Grab(mag, false); return; }
            }
            // 2) a gun in a holster
            Grabbable best = null;
            float bestD = 0.3f;
            foreach (var g in rig.LoadoutGuns())
            {
                if (g == null || g.Held || !g.AtHome) continue;
                float d = Vector3.Distance(attach.position, g.transform.position);
                if (d < bestD) { best = g; bestD = d; }
            }
            // 3) anything loose nearby
            if (best == null)
            {
                bestD = 0.2f;
                foreach (var c in Physics.OverlapSphere(attach.position, 0.2f, ~0, QueryTriggerInteraction.Collide))
                {
                    var g = c.GetComponentInParent<Grabbable>();
                    if (g == null || g.holder == this) continue;
                    float d = Vector3.Distance(attach.position, c.ClosestPoint(attach.position));
                    if (d < bestD) { best = g; bestD = d; }
                }
            }
            // keyboard testing: hands can't reach the hips, so take from the holster directly
            if (best == null && !VRInput.I.XRActive)
            {
                var g = index == 1 ? (Grabbable)rig.primary : rig.sidearm;
                if (g != null && g.AtHome) best = g;
            }
            if (best != null)
            {
                if (best.holder != null) best.holder.ReleaseQuietly();
                Grab(best, false);
            }
        }

        void TryPouch()
        {
            if (Vector3.Distance(attach.position, rig.pouch.position) > 0.26f && VRInput.I.XRActive) return;
            var gun = Other.HeldGun;
            if (gun == null) return;
            var mag = Magazine.Spawn(gun.def, gun.def.magSize, attach.position, attach.rotation);
            AudioManager.Sfx("grab", attach.position, 0.7f);
            VRInput.I.Haptic(index, 0.35f, 0.04f);
            Grab(mag, true);
        }

        void Grab(Grabbable g, bool byTrigger)
        {
            held = g;
            heldByTrigger = byTrigger;
            g.OnGrab(this);
            if (g is Magazine)
            {
                g.transform.localPosition = new Vector3(0, 0.03f, 0.02f);
                g.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            }
            VRInput.I.Haptic(index, 0.25f, 0.03f);
        }

        public void Release()
        {
            if (held == null) return;
            var g = held;
            held = null;
            g.OnRelease(this, Velocity, AngularVelocity);
        }

        /// <summary>Let go without throwing (the other hand took it).</summary>
        public void ReleaseQuietly()
        {
            if (held == null) return;
            held.holder = null;
            held = null;
        }

        public void StopSupporting()
        {
            supporting = null;
            if (glove != null) { glove.transform.localPosition = Vector3.zero; glove.transform.localRotation = Quaternion.identity; }
        }

        /// <summary>
        /// Two-handed aiming. On the grip hand: point the gun from this hand toward the support
        /// hand (rifles and shotguns; a pistol just gets cupped). On the support hand: draw the
        /// hand on the gun's foregrip.
        /// </summary>
        public void ApplyTwoHanded()
        {
            if (held is Gun g && Other.supporting == g && g.def.cls != GunClass.Pistol)
            {
                var fore = g.model.foregrip.localPosition;
                var toSupport = Other.transform.position - transform.position;
                if (toSupport.sqrMagnitude > 0.01f && fore.sqrMagnitude > 0.0001f)
                {
                    var aim = Quaternion.LookRotation(toSupport, transform.up);
                    g.transform.rotation = aim * Quaternion.Inverse(Quaternion.FromToRotation(Vector3.forward, fore.normalized));
                }
            }
            else if (held is Gun g2 && g2.transform.localRotation != Quaternion.identity && Other.supporting != g2)
                g2.transform.localRotation = Quaternion.identity;   // back to one hand
            if (supporting != null && glove != null && supporting.model.foregrip != null)
                glove.transform.SetPositionAndRotation(supporting.model.foregrip.position, supporting.transform.rotation);
        }

        /// <summary>Drop everything (dying).</summary>
        public void DropAll()
        {
            StopSupporting();
            if (held == null) return;
            var g = held;
            held = null;
            g.OnRelease(this, Vector3.zero, Vector3.zero);
            if (g is Gun gun && gun.home != null) gun.GoHome();
        }
    }
}
