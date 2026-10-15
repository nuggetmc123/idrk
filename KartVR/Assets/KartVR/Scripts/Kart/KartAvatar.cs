using Fusion;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// What other players see of you: a head and two hands sitting in your kart,
    /// your kart color, and a name tag. The owner copies its real headset and
    /// controller poses (relative to the kart) into networked values.
    /// </summary>
    public class KartAvatar : NetworkBehaviour
    {
        [Tooltip("Found by name (NetHead / NetLeftHand / NetRightHand / NameTag) if left empty.")]
        public Transform head, leftHand, rightHand;
        public TextMeshPro nameTag;
        [Tooltip("Renderers tinted with the player's color. Defaults to the 'Body' child.")]
        public Renderer[] colorRenderers;

        [Networked] Vector3 HeadPos { get; set; }
        [Networked] Quaternion HeadRot { get; set; }
        [Networked] Vector3 LeftPos { get; set; }
        [Networked] Quaternion LeftRot { get; set; }
        [Networked] Vector3 RightPos { get; set; }
        [Networked] Quaternion RightRot { get; set; }

        KartController kart;
        int appliedColor = -1;
        string appliedName;

        void Awake()
        {
            kart = GetComponent<KartController>();
            if (head == null) head = transform.Find("NetHead");
            if (leftHand == null) leftHand = transform.Find("NetLeftHand");
            if (rightHand == null) rightHand = transform.Find("NetRightHand");
            if (nameTag == null)
            {
                var t = transform.Find("NameTag");
                if (t) nameTag = t.GetComponent<TextMeshPro>();
            }
            if (colorRenderers == null || colorRenderers.Length == 0)
            {
                var body = transform.Find("Body");
                if (body) colorRenderers = new[] { body.GetComponent<Renderer>() };
            }
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                // You don't need to see your own avatar head (it would block the camera)
                // or your network hands (the rig shows your real hands).
                SetVisible(head, false);
                SetVisible(leftHand, false);
                SetVisible(rightHand, false);
                if (nameTag) nameTag.gameObject.SetActive(false);
            }
        }

        static void SetVisible(Transform t, bool visible)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = visible;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            var rig = KartRig.Instance;
            if (rig == null || !rig.IsAttachedTo(kart)) return;
            HeadPos = transform.InverseTransformPoint(rig.head.position);
            HeadRot = Quaternion.Inverse(transform.rotation) * rig.head.rotation;
            LeftPos = transform.InverseTransformPoint(rig.leftHand.position);
            LeftRot = Quaternion.Inverse(transform.rotation) * rig.leftHand.rotation;
            RightPos = transform.InverseTransformPoint(rig.rightHand.position);
            RightRot = Quaternion.Inverse(transform.rotation) * rig.rightHand.rotation;
        }

        public override void Render()
        {
            if (kart != null && kart.IsReady)
            {
                if (kart.ColorIndex != appliedColor)
                {
                    appliedColor = kart.ColorIndex;
                    var c = PlayerProfile.GetColor(appliedColor);
                    foreach (var r in colorRenderers) if (r) KartUI.SetColor(r.material, c);
                }
                string n = kart.Nickname.ToString();
                if (nameTag && n != appliedName)
                {
                    appliedName = n;
                    nameTag.text = n;
                }
            }

            if (HasStateAuthority) return;

            float k = 1f - Mathf.Exp(-20f * Time.deltaTime);
            Follow(head, HeadPos, HeadRot, k);
            Follow(leftHand, LeftPos, LeftRot, k);
            Follow(rightHand, RightPos, RightRot, k);

            // Name tag faces the local camera.
            if (nameTag && KartRig.Instance != null)
            {
                Vector3 away = nameTag.transform.position - KartRig.Instance.head.position;
                if (away.sqrMagnitude > 0.01f) nameTag.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        static void Follow(Transform t, Vector3 localPos, Quaternion localRot, float k)
        {
            // An all-zero quaternion means nothing has been received yet.
            if (t == null || (localRot.x == 0f && localRot.y == 0f && localRot.z == 0f && localRot.w == 0f)) return;
            t.localPosition = Vector3.Lerp(t.localPosition, localPos, k);
            t.localRotation = Quaternion.Slerp(t.localRotation, localRot, k);
        }
    }
}
