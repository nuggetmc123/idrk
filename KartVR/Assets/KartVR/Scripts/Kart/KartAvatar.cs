using Fusion;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The driver you see sitting in a kart: full body in the kart's colors,
    /// with head and hands following the player's real headset and controllers,
    /// plus a name tag. Hidden while the kart is parked in its garage (the
    /// player is walking around the hub instead). On your own kart the helmet
    /// is hidden so it doesn't block your view, but you can look down and see
    /// your body and gloves.
    /// </summary>
    public class KartAvatar : NetworkBehaviour
    {
        public TextMeshPro nameTag;

        // Poses relative to the kart, sent by the owner.
        [Networked] Vector3 HeadPos { get; set; }
        [Networked] Quaternion HeadRot { get; set; }
        [Networked] Vector3 LeftPos { get; set; }
        [Networked] Quaternion LeftRot { get; set; }
        [Networked] Vector3 RightPos { get; set; }
        [Networked] Quaternion RightRot { get; set; }

        KartController kart;
        AvatarBody body;
        Vector3 sHead = new Vector3(0f, 1.15f, -0.3f), sLeft = new Vector3(-0.17f, 0.75f, 0.2f), sRight = new Vector3(0.17f, 0.75f, 0.2f);
        Quaternion sHeadRot = Quaternion.identity, sLeftRot = Quaternion.identity, sRightRot = Quaternion.identity;

        // Seat geometry in kart space.
        static readonly Vector3 Hips = new Vector3(0f, 0.53f, -0.35f);
        static readonly Vector3 LeftPedal = new Vector3(-0.13f, 0.31f, 0.34f);   // where the boot soles rest
        static readonly Vector3 RightPedal = new Vector3(0.13f, 0.31f, 0.34f);

        void Awake()
        {
            kart = GetComponent<KartController>();
            var bodyGo = new GameObject("Driver");
            bodyGo.transform.SetParent(transform, false);
            body = bodyGo.AddComponent<AvatarBody>();
            if (nameTag == null)
            {
                var t = transform.Find("NameTag");
                if (t) nameTag = t.GetComponent<TextMeshPro>();
            }
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                KartRig.AfterPoses += LatePose;
                body.SetHeadVisible(false);
                if (nameTag) nameTag.gameObject.SetActive(false);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            var rig = KartRig.Instance;
            if (rig == null || !rig.IsAttachedTo(kart)) return;
            HeadPos = transform.InverseTransformPoint(rig.head.position);
            HeadRot = Quaternion.Inverse(transform.rotation) * rig.head.rotation;
            Vector3 leftWorld = rig.leftHand.position, rightWorld = rig.rightHand.position;
            if (kart.visuals != null && kart.visuals.SteeringWheel != null)
            {
                // Others see your hands on the wheel's rim while you hold it.
                if (kart.Wheel.LeftHeld) leftWorld = KartSteeringWheel.RimPoint(kart.visuals.SteeringWheel, leftWorld);
                if (kart.Wheel.RightHeld) rightWorld = KartSteeringWheel.RimPoint(kart.visuals.SteeringWheel, rightWorld);
            }
            LeftPos = transform.InverseTransformPoint(leftWorld);
            LeftRot = Quaternion.Inverse(transform.rotation) * rig.leftHand.rotation;
            RightPos = transform.InverseTransformPoint(rightWorld);
            RightRot = Quaternion.Inverse(transform.rotation) * rig.rightHand.rotation;
        }

        static bool Received(Quaternion q) => !(q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f);

        bool posingLate;

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            KartRig.AfterPoses -= LatePose;
        }

        /// <summary>Your own driver: posed after the newest controller poses, so the gloves stay on your hands.</summary>
        void LatePose()
        {
            if (this == null || Object == null || !Object.IsValid) return;
            posingLate = true;
            Render();
            posingLate = false;
        }

        public override void Render()
        {
            if (HasStateAuthority && !posingLate && KartRig.Instance != null) return; // done in LatePose instead
            if (kart == null || !kart.IsReady) return;
            var look = kart.Look;
            body.Build(look.paint, look.accent);

            bool driving = !kart.Parked && !kart.DriverOut && !kart.Ghost && !(HasStateAuthority && ModPowers.InvisibleNow); // nobody in the seat while they're out walking (or invisible)
            body.SetVisible(driving);
            if (nameTag && !HasStateAuthority)
            {
                nameTag.gameObject.SetActive(driving);
                nameTag.text = "<size=60%><color=#ffd54a>Lv " + kart.Level + "</color></size>\n" + kart.DisplayName;
            }
            if (!driving) return;

            if (HasStateAuthority)
            {
                // Your own driver: use the live rig so your body matches your hands exactly.
                var rig = KartRig.Instance;
                if (rig != null && rig.IsAttachedTo(kart))
                {
                    sHead = transform.InverseTransformPoint(rig.head.position);
                    sHeadRot = Quaternion.Inverse(transform.rotation) * rig.head.rotation;
                    sLeft = transform.InverseTransformPoint(rig.leftHand.position);
                    sLeftRot = Quaternion.Inverse(transform.rotation) * rig.leftHand.rotation;
                    sRight = transform.InverseTransformPoint(rig.rightHand.position);
                    sRightRot = Quaternion.Inverse(transform.rotation) * rig.rightHand.rotation;
                }
            }
            else if (Received(HeadRot))
            {
                float k = 1f - Mathf.Exp(-20f * Time.deltaTime);
                sHead = Vector3.Lerp(sHead, HeadPos, k); sHeadRot = Quaternion.Slerp(sHeadRot, HeadRot, k);
                sLeft = Vector3.Lerp(sLeft, LeftPos, k); sLeftRot = Quaternion.Slerp(sLeftRot, LeftRot, k);
                sRight = Vector3.Lerp(sRight, RightPos, k); sRightRot = Quaternion.Slerp(sRightRot, RightRot, k);
            }

            // Your own hands holding the steering wheel snap onto its rim, so the grip looks solid.
            if (HasStateAuthority && kart.visuals != null && kart.visuals.SteeringWheel != null)
            {
                var pivot = kart.visuals.SteeringWheel;
                if (kart.Wheel.LeftHeld) sLeft = transform.InverseTransformPoint(KartSteeringWheel.RimPoint(pivot, transform.TransformPoint(sLeft)));
                if (kart.Wheel.RightHeld) sRight = transform.InverseTransformPoint(KartSteeringWheel.RimPoint(pivot, transform.TransformPoint(sRight)));
            }

            // Keep body parts inside the kart even if someone's tracking goes wild.
            Vector3 head = transform.TransformPoint(ClampLocal(sHead, 0.6f, new Vector3(0f, 1.15f, -0.3f)));
            Vector3 left = transform.TransformPoint(ClampLocal(sLeft, 0.9f, new Vector3(-0.17f, 0.75f, 0.2f)));
            Vector3 right = transform.TransformPoint(ClampLocal(sRight, 0.9f, new Vector3(0.17f, 0.75f, 0.2f)));

            body.Pose(transform.TransformPoint(Hips), transform.rotation,
                head, transform.rotation * sHeadRot,
                left, transform.rotation * sLeftRot,
                right, transform.rotation * sRightRot,
                transform.TransformPoint(LeftPedal), transform.TransformPoint(RightPedal));

            if (nameTag && !HasStateAuthority && KartRig.Instance != null)
            {
                nameTag.transform.position = head + Vector3.up * 0.55f;
                Vector3 away = nameTag.transform.position - KartRig.Instance.head.position;
                if (away.sqrMagnitude > 0.01f) nameTag.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        static Vector3 ClampLocal(Vector3 p, float maxDistance, Vector3 center)
        {
            Vector3 d = p - center;
            return d.magnitude > maxDistance ? center + d.normalized * maxDistance : p;
        }
    }
}
