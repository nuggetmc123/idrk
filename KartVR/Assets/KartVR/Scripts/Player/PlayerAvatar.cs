using Fusion;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// You on foot in the Hub. Walk with the left stick (smooth, in the
    /// direction you look), snap-turn with the right stick, walk through a
    /// portal to go play. Everyone sees your full body (helmet, suit, arms
    /// following your real hands, legs that step as you move) and your name.
    /// Outside the Hub this is hidden and you're in your kart instead.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAvatar : NetworkBehaviour
    {
        public static PlayerAvatar Local { get; private set; }

        public float walkSpeed = 2.6f;
        public float snapTurnDegrees = 45f;
        public TextMeshPro nameTag;

        // Everything in world space; sent by the owner.
        [Networked] Vector3 HeadPos { get; set; }
        [Networked] Quaternion HeadRot { get; set; }
        [Networked] Vector3 LeftPos { get; set; }
        [Networked] Quaternion LeftRot { get; set; }
        [Networked] Vector3 RightPos { get; set; }
        [Networked] Quaternion RightRot { get; set; }
        [Networked] Vector3 FeetPos { get; set; }
        [Networked] float MoveSpeed { get; set; }
        [Networked] int TeleportCount { get; set; }

        public bool IsReady => Object != null && Object.IsValid;
        public bool IsLocal { get; private set; }
        public bool InHub => GameState.CurrentMode == KartMode.Hub;

        CharacterController controller;
        AvatarBody body;
        float verticalVelocity;
        bool turnLatched;
        bool placed;
        KartMode lastMode = KartMode.Race;
        int seenTeleport;

        // Smoothed copies for drawing remote players.
        Vector3 sHead, sLeft, sRight, sFeet;
        Quaternion sHeadRot = Quaternion.identity, sLeftRot = Quaternion.identity, sRightRot = Quaternion.identity;
        float walkPhase, bodyYaw;
        Vector3 lastFeet;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            var bodyGo = new GameObject("Body");
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
            IsLocal = HasStateAuthority;
            controller.enabled = IsLocal;
            if (IsLocal)
            {
                Local = this;
                body.SetHeadVisible(false);
                if (nameTag) nameTag.gameObject.SetActive(false);
                FeetPos = transform.position;
            }
            else seenTeleport = TeleportCount;
            sFeet = lastFeet = transform.position;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this) Local = null;
        }

        /// <summary>The kart owned by the same player (for name and colors).</summary>
        KartController OwnKart()
        {
            if (!IsReady) return null;
            foreach (var k in KartController.All)
                if (k.IsReady && k.Object.StateAuthority == Object.StateAuthority) return k;
            return null;
        }

        public void Teleport(Vector3 feet, Quaternion facing)
        {
            if (!IsLocal) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
            TeleportCount++;
            FeetPos = feet;
        }

        public void RespawnInHub()
        {
            var area = SpawnArea.Get(KartMode.Hub);
            var kart = OwnKart();
            if (area == null) return;
            var pose = area.GetSlot(kart != null ? kart.SlotIndex() : 0);
            Teleport(pose.position, pose.rotation);
        }

        void Update()
        {
            if (!IsReady || !IsLocal) return;

            // Arriving back in the hub: start at the spawn plaza.
            var mode = GameState.CurrentMode;
            if (GameState.Instance != null && GameState.Instance.IsReady && (mode != lastMode || !placed))
            {
                placed = true;
                lastMode = mode;
                if (mode == KartMode.Hub) RespawnInHub();
            }
            controller.enabled = InHub;
            if (!InHub) return;

            if (KartInput.RespawnPressed) RespawnInHub();

            var rig = KartRig.Instance;
            if (rig == null || !rig.IsAttachedTo(this)) return;

            // Snap turn around your head so you don't swing sideways.
            float turn = KartInput.Turn;
            if (Mathf.Abs(turn) > 0.7f && !turnLatched)
            {
                turnLatched = true;
                controller.enabled = false;
                transform.RotateAround(rig.head.position, Vector3.up, Mathf.Sign(turn) * snapTurnDegrees);
                controller.enabled = true;
            }
            else if (Mathf.Abs(turn) < 0.3f) turnLatched = false;

            // Smooth walking in the direction you look.
            Vector2 stick = KartInput.Move;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 move = (fwd * stick.y + right * stick.x) * walkSpeed;

            // Keep the capsule under your real head when you walk around your room.
            Vector3 headLocal = transform.InverseTransformPoint(rig.head.position);
            controller.center = new Vector3(headLocal.x, controller.height * 0.5f, headLocal.z);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);

            if (transform.position.y < -10f) RespawnInHub();
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsLocal) return;
            var rig = KartRig.Instance;
            if (rig == null || !rig.IsAttachedTo(this)) return;
            HeadPos = rig.head.position;
            HeadRot = rig.head.rotation;
            LeftPos = rig.leftHand.position;
            LeftRot = rig.leftHand.rotation;
            RightPos = rig.rightHand.position;
            RightRot = rig.rightHand.rotation;
            FeetPos = new Vector3(rig.head.position.x, transform.position.y, rig.head.position.z);
            Vector3 v = controller.velocity;
            v.y = 0f;
            MoveSpeed = v.magnitude;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsReady || !IsLocal || !InHub) return;
            if (other.TryGetComponent(out ModePortal portal)) portal.Activate();
        }

        public override void Render()
        {
            var kart = OwnKart();
            if (kart != null)
            {
                var look = kart.Look;
                body.Build(look.paint, look.accent);
                if (nameTag && !IsLocal) nameTag.text = kart.Nickname.ToString();
            }

            bool show = InHub && (!IsLocal || (KartRig.Instance != null && KartRig.Instance.IsAttachedTo(this)));
            body.SetVisible(show);
            if (nameTag && !IsLocal) nameTag.gameObject.SetActive(show);
            if (!show) return;

            Vector3 head, left, right, feet;
            Quaternion headRot, leftRot, rightRot;
            float speed;
            if (IsLocal)
            {
                var rig = KartRig.Instance;
                head = rig.head.position; headRot = rig.head.rotation;
                left = rig.leftHand.position; leftRot = rig.leftHand.rotation;
                right = rig.rightHand.position; rightRot = rig.rightHand.rotation;
                feet = new Vector3(head.x, transform.position.y, head.z);
                Vector3 v = controller.velocity; v.y = 0f;
                speed = v.magnitude;
            }
            else
            {
                if (HeadRot.w == 0f && HeadRot.x == 0f && HeadRot.y == 0f && HeadRot.z == 0f) return; // nothing received yet
                bool snap = TeleportCount != seenTeleport || (sFeet - FeetPos).sqrMagnitude > 25f;
                seenTeleport = TeleportCount;
                float k = snap ? 1f : 1f - Mathf.Exp(-18f * Time.deltaTime);
                sHead = Vector3.Lerp(sHead, HeadPos, k); sHeadRot = Quaternion.Slerp(sHeadRot, HeadRot, k);
                sLeft = Vector3.Lerp(sLeft, LeftPos, k); sLeftRot = Quaternion.Slerp(sLeftRot, LeftRot, k);
                sRight = Vector3.Lerp(sRight, RightPos, k); sRightRot = Quaternion.Slerp(sRightRot, RightRot, k);
                sFeet = Vector3.Lerp(sFeet, FeetPos, k);
                head = sHead; headRot = sHeadRot; left = sLeft; leftRot = sLeftRot; right = sRight; rightRot = sRightRot; feet = sFeet;
                speed = MoveSpeed;
                transform.position = feet; // keeps the name tag and body together
            }

            // Body faces where you look, but lazily, like a real person.
            float headYaw = headRot.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(bodyYaw, headYaw)) > 35f || speed > 0.2f)
                bodyYaw = Mathf.LerpAngle(bodyYaw, headYaw, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Quaternion bodyRot = Quaternion.Euler(0f, bodyYaw, 0f);
            Vector3 fwd = bodyRot * Vector3.forward, sideways = bodyRot * Vector3.right;

            // Walk cycle: feet swing forward/back and lift, faster when moving faster.
            float headHeight = Mathf.Clamp(head.y - feet.y, 1.0f, 2.0f);
            float stride = Mathf.Clamp01(speed / walkSpeed);
            walkPhase += Time.deltaTime * (2f + speed * 2.6f) * (stride > 0.05f ? 1f : 0f);
            float swing = Mathf.Sin(walkPhase) * 0.28f * stride;
            float liftL = Mathf.Max(0f, Mathf.Cos(walkPhase)) * 0.12f * stride;
            float liftR = Mathf.Max(0f, -Mathf.Cos(walkPhase)) * 0.12f * stride;
            Vector3 footL = feet - sideways * 0.12f + fwd * swing + Vector3.up * (0.04f + liftL);
            Vector3 footR = feet + sideways * 0.12f - fwd * swing + Vector3.up * (0.04f + liftR);
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.03f * stride;
            Vector3 hips = new Vector3(head.x, feet.y + headHeight * 0.55f + bob, head.z) - fwd * 0.08f;

            body.Pose(hips, bodyRot, head, headRot, left, leftRot, right, rightRot, footL, footR);

            if (nameTag && !IsLocal && KartRig.Instance != null)
            {
                nameTag.transform.position = head + Vector3.up * 0.45f;
                Vector3 away = nameTag.transform.position - KartRig.Instance.head.position;
                if (away.sqrMagnitude > 0.01f) nameTag.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }
    }
}
