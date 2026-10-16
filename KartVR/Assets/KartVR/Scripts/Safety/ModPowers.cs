using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// Extra powers for moderators (Mod Menu > tools). Other players' games only obey them for a moderator who
    /// proved their ID (<see cref="KartController.IsVerifiedMod"/>), so copying a mod's ID gets a cheater nothing.
    /// </summary>
    public static class ModPowers
    {
        /// <summary>Nobody else can see you, your kart, your name or your talk light (or bump into you).</summary>
        public static bool Invisible;
        /// <summary>Build mode on every map (not just Free Roam / Tutorial Park); click the right stick as usual.</summary>
        public static bool BuildAnywhere;
        /// <summary>A blaster in your right hand while walking: right trigger shoots, hits shove karts and players.</summary>
        public static bool Blaster;

        /// <summary>You're an invisible moderator: your own body is hidden from you too, and you get ball hands.</summary>
        public static bool InvisibleNow => Invisible && Moderation.IAmMod;

        public static void Reset() { Invisible = BuildAnywhere = Blaster = false; }
    }

    /// <summary>
    /// The moderator blaster. Yours sits in your right hand while you're walking (in the hub or out of your kart);
    /// pull the right trigger to shoot a laser bolt that shoves karts and knocks players back (no damage, it's a
    /// kart game). Everyone sees the bolt and the blaster in the mod's hand.
    /// </summary>
    public class ModBlaster : MonoBehaviour
    {
        static ModBlaster instance;
        const float Range = 70f, Cooldown = 0.3f;
        Transform mine;
        readonly Dictionary<KartController, Transform> others = new Dictionary<KartController, Transform>();
        float nextShot;
        bool triggerWas;

        void Awake() { instance = this; }

        void Update()
        {
            var rig = KartRig.Instance;
            var me = PlayerAvatar.Local;
            bool holding = ModPowers.Blaster && Moderation.IAmMod && rig != null && me != null && me.Walking && rig.IsAttachedTo(me);
            if (holding && mine == null) mine = Model("MyBlaster");
            if (mine != null) mine.gameObject.SetActive(holding);
            if (holding)
            {
                mine.SetPositionAndRotation(rig.rightHand.position, rig.rightHand.rotation);
                bool trigger = KartInput.Gas > 0.6f;
                if (trigger && !triggerWas && Time.time >= nextShot) Shoot(mine);
                triggerWas = trigger;
            }
            ShowOthers();
        }

        void Shoot(Transform gun)
        {
            nextShot = Time.time + Cooldown;
            Vector3 from = gun.TransformPoint(new Vector3(0f, 0.03f, 0.2f));
            Vector3 dir = gun.forward;
            Vector3 to = from + dir * Range;
            var hits = Physics.RaycastAll(from, dir, Range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var hit in hits)
            {
                var kart = hit.collider.GetComponentInParent<KartController>();
                var walker = hit.collider.GetComponentInParent<PlayerAvatar>();
                if (kart != null && kart.IsLocal || walker != null && walker.IsLocal) continue; // never your own body or kart
                to = hit.point;
                if (kart != null && kart.IsReady) kart.RPC_ReceiveBump(dir * 14f + Vector3.up * 5f);
                else if (walker != null && walker.IsReady) walker.RPC_Knock(dir * 10f + Vector3.up * 4f);
                break;
            }
            ShowShot(from, to);
            KartController.Local?.RPC_BlastFx(from, to);
            AudioManager.PlayAt(AudioManager.Sfx.Boost, from, 0.6f, 1.8f);
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (device.isValid) device.SendHapticImpulse(0, 0.6f, 0.08f);
        }

        /// <summary>Other moderators' blasters, in their right hands.</summary>
        void ShowOthers()
        {
            foreach (var kart in KartController.All)
            {
                if (kart == null || kart.IsLocal || !kart.IsReady) continue;
                PlayerAvatar walker = null;
                foreach (var a in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
                    if (a.IsReady && a.Object.StateAuthority == kart.Object.StateAuthority) { walker = a; break; }
                bool show = kart.BlasterOut && kart.IsVerifiedMod && !kart.Ghost && walker != null && walker.Walking;
                others.TryGetValue(kart, out var model);
                if (show && model == null) others[kart] = model = Model("ModBlaster");
                if (model == null) continue;
                model.gameObject.SetActive(show);
                if (show) model.SetPositionAndRotation(walker.RightHandPosition, walker.RightHandRotation);
            }
        }

        /// <summary>A chunky toy blaster made of shapes. Points along +Z from the hand.</summary>
        static Transform Model(string name)
        {
            var root = new GameObject(name).transform;
            var body = KartUI.Mat(new Color(0.15f, 0.16f, 0.2f), 0.6f, 0.3f);
            var glow = KartUI.Glow(new Color(1f, 0.25f, 0.2f));
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.06f), new Vector3(0.05f, 0.06f, 0.2f), body);
            KartUI.Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.18f), new Vector3(0.035f, 0.05f, 0.035f), glow, new Vector3(90f, 0f, 0f));
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.0f), new Vector3(0.04f, 0.09f, 0.05f), body, new Vector3(15f, 0f, 0f));
            KartUI.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0.06f), new Vector3(0.02f, 0.015f, 0.12f), glow);
            return root;
        }

        /// <summary>A glowing bolt from the muzzle to where it hit, fading out quickly.</summary>
        public static void ShowShot(Vector3 from, Vector3 to)
        {
            var bolt = KartUI.Part(null, PrimitiveType.Capsule, Vector3.zero, Vector3.one, KartUI.Glow(new Color(1f, 0.3f, 0.2f)));
            KartUI.Span(bolt, from, to, 0.03f);
            var flash = KartUI.Part(null, PrimitiveType.Sphere, to, Vector3.one * 0.35f, KartUI.Glow(new Color(1f, 0.6f, 0.3f)));
            Destroy(bolt.gameObject, 0.12f);
            Destroy(flash.gameObject, 0.2f);
        }
    }
}
