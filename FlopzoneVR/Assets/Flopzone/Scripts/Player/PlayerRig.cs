using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The local player: headset camera, hands, holsters and movement.
    /// Left stick moves, click it to sprint; right stick turns, click it to
    /// crouch; A jumps; B drops the mag (all rebindable in Menu > Controls).
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public static PlayerRig I { get; private set; }

        public CharacterController cc;
        public Transform trackingSpace, head, body, pouch;
        public Camera cam;
        public Hand[] hands = new Hand[2];
        public Transform[] holsters = new Transform[2];
        public Gun primary, sidearm;

        public bool Alive { get; private set; } = true;
        public bool MenuOpen { get; set; }
        public float GravityScale { get; set; } = 1f;
        public bool CanShoot => Alive && !MenuOpen && !(UIPointer.I != null && UIPointer.I.OverUI);
        public bool Sprinting => sprinting;
        public bool Crouched => crouched;

        float vy;
        bool sprinting, crouched, snapLatched, wasGrounded = true;
        float crouchY, stepDist, bodyYaw;
        Avatar selfAvatar;
        int team = -1;

        public static PlayerRig Create()
        {
            var go = new GameObject("PlayerRig");
            var rig = go.AddComponent<PlayerRig>();
            rig.Build();
            return rig;
        }

        void Build()
        {
            I = this;
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 0.22f;
            cc.height = 1.7f;
            cc.center = new Vector3(0, 0.87f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;

            trackingSpace = Prims.Empty("TrackingSpace", transform).transform;
            var camGo = new GameObject("Head");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(trackingSpace, false);
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            head = camGo.transform;

            for (int i = 0; i < 2; i++) hands[i] = Hand.Create(this, trackingSpace, i);

            body = Prims.Empty("Body", transform).transform;
            var ringM = Prims.Fade(new Color(1f, 0.6f, 0.2f, 0.25f));
            for (int i = 0; i < 2; i++)
            {
                holsters[i] = Prims.Empty(i == 0 ? "PrimaryHolster" : "SidearmHolster", body,
                    new Vector3(i == 0 ? -0.22f : 0.22f, 0, 0.02f)).transform;
                holsters[i].localRotation = Quaternion.Euler(75, i == 0 ? -10 : 10, 0);
                var marker = Prims.Cyl(body, holsters[i].localPosition, 0.11f, 0.012f, ringM, Vector3.up);
                marker.name = "holsterMarker";
            }
            pouch = Prims.Empty("MagPouch", body, new Vector3(0, 0.03f, 0.16f)).transform;
            var pouchVis = Prims.Box(pouch, Vector3.zero, new Vector3(0.14f, 0.07f, 0.05f), Prims.Lit(new Color(0.2f, 0.22f, 0.18f), 0.2f), false);
            pouchVis.name = "pouch";
            for (int i = 0; i < 3; i++)
                Prims.Box(pouch, new Vector3((i - 1) * 0.04f, 0.045f, 0), new Vector3(0.026f, 0.03f, 0.04f), Prims.Lit(new Color(0.1f, 0.1f, 0.1f), 0.5f), false);

            if (UIPointer.I != null) { UIPointer.I.hands[0] = hands[0].transform; UIPointer.I.hands[1] = hands[1].transform; }
            Application.onBeforeRender += OnBeforeRender;
            SetTeam(-1);
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        void OnBeforeRender()
        {
            if (VRInput.I == null) return;
            VRInput.I.Poll();
            ApplyPoses();
        }

        void ApplyPoses()
        {
            var input = VRInput.I;
            head.localPosition = input.HeadPos;
            head.localRotation = input.HeadRot;
            for (int i = 0; i < 2; i++)
            {
                hands[i].transform.localPosition = input.HandPos(i);
                hands[i].transform.localRotation = input.HandRot(i);
            }
        }

        public void SetTeam(int t)
        {
            team = t;
            if (selfAvatar) Destroy(selfAvatar.gameObject);
            selfAvatar = Avatar.Build(transform, Game.I != null && Game.I.session != null ? Game.I.session.MyId : "me", Avatar.TeamColor(t), Save.Settings.look);
            selfAvatar.SetVisible(false);
            RefreshLook();
        }

        /// <summary>Put on the outfit from the Outfitter (your hidden body for your ragdoll, and the gloves on your own hands).</summary>
        public void RefreshLook()
        {
            int code = Save.Settings.look;
            if (selfAvatar) selfAvatar.ApplyLook(code);
            var (main, cuff) = Cosmetics.GloveColors(Look.Unpack(code).gloves);
            foreach (var h in hands) if (h) h.SetGloveColors(main, cuff);
        }

        public IEnumerable<Gun> LoadoutGuns()
        {
            if (primary) yield return primary;
            if (sidearm) yield return sidearm;
        }

        /// <summary>Put fresh guns in the holsters.</summary>
        public void GiveLoadout(int primaryIdx, int sidearmIdx)
        {
            foreach (var h in hands) if (h.held is Gun) h.ReleaseQuietly();
            if (primary) Destroy(primary.gameObject);
            if (sidearm) Destroy(sidearm.gameObject);
            primary = Gun.Spawn(GunCatalog.Get(primaryIdx) ?? GunCatalog.All[16], holsters[0].position, holsters[0].rotation);
            sidearm = Gun.Spawn(GunCatalog.Get(sidearmIdx) ?? GunCatalog.All[0], holsters[1].position, holsters[1].rotation);
            primary.home = holsters[0];
            sidearm.home = holsters[1];
            primary.homeOnBody = sidearm.homeOnBody = true;
            primary.GoHome();
            sidearm.GoHome();
        }

        void Update()
        {
            var input = VRInput.I;
            if (input == null) return;
            ApplyPoses();
            var st = Save.Settings;
            float dt = Time.deltaTime;

            // ---- body (holsters follow the head's facing, lazily) ----
            float headYaw = head.eulerAngles.y;
            float diff = Mathf.DeltaAngle(bodyYaw, headYaw);
            if (Mathf.Abs(diff) > 40f || input.MoveStick.sqrMagnitude > 0.05f) bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, headYaw, dt * 220f);
            float headH = Mathf.Max(0.6f, head.position.y - transform.position.y);
            body.SetPositionAndRotation(new Vector3(head.position.x, transform.position.y + headH * 0.56f, head.position.z),
                Quaternion.Euler(0, bodyYaw, 0));

            // ---- buttons ----
            if (Alive && !MenuOpen)
            {
                if (input.Down(ControlAction.Sprint)) sprinting = st.sprintToggle ? !sprinting : true;
                if (!st.sprintToggle && input.Up(ControlAction.Sprint)) sprinting = false;
                if (input.Down(ControlAction.Crouch)) { crouched = st.crouchToggle ? !crouched : true; sprinting = false; }
                if (!st.crouchToggle && input.Up(ControlAction.Crouch)) crouched = false;
                if (input.Down(ControlAction.UnloadMag))
                {
                    var g = hands[1].HeldGun ?? hands[0].HeldGun;
                    if (g != null) g.DropMag();
                }
            }

            // ---- locomotion ----
            var stick = Alive ? input.MoveStick : Vector2.zero;
            if (stick.magnitude < 0.15f) stick = Vector2.zero;
            if (stick.magnitude < 0.2f && st.sprintToggle) sprinting = false;
            var yawRot = Quaternion.Euler(0, headYaw, 0);
            var move = yawRot * new Vector3(stick.x, 0, stick.y);
            if (move.sqrMagnitude > 1f) move.Normalize();
            float speed = crouched ? 1.7f : sprinting ? 5.4f : 3.1f;
            bool grounded = cc.isGrounded;
            if (grounded && vy < 0) vy = -1.5f;
            if (Alive && !MenuOpen && grounded && input.Down(ControlAction.Jump))
            {
                vy = 4.6f;
                crouched = false;
                AudioManager.Sfx("jump", transform.position, 0.5f);
            }
            vy -= 9.81f * GravityScale * dt;
            vy = Mathf.Max(vy, -40f);
            var before = transform.position;
            if (cc.enabled) cc.Move((move * speed + Vector3.up * vy) * dt);
            if (grounded && !wasGrounded && vy < -4f) AudioManager.Sfx("land", transform.position, 0.6f);
            wasGrounded = grounded;

            var flat = transform.position - before;
            flat.y = 0;
            if (grounded && flat.magnitude > 0.001f)
            {
                stepDist += flat.magnitude;
                if (stepDist > (sprinting ? 2.1f : 1.5f)) { stepDist = 0; AudioManager.Sfx("step", transform.position, sprinting ? 0.55f : 0.35f, Random.Range(0.9f, 1.1f)); }
            }

            // ---- turning ----
            float tx = Alive && !MenuOpen ? input.TurnStick.x : 0f;
            if (st.turnStyle == TurnStyle.Snap)
            {
                if (!snapLatched && Mathf.Abs(tx) > 0.7f) { transform.RotateAround(head.position, Vector3.up, Mathf.Sign(tx) * st.snapAngle); snapLatched = true; }
                if (Mathf.Abs(tx) < 0.3f) snapLatched = false;
            }
            else if (Mathf.Abs(tx) > 0.15f) transform.RotateAround(head.position, Vector3.up, tx * st.smoothTurnSpeed * dt);

            // ---- crouch height + room-scale walking ----
            crouchY = Mathf.MoveTowards(crouchY, crouched ? -0.55f : 0f, dt * 3f);
            var ts = trackingSpace.localPosition;
            trackingSpace.localPosition = new Vector3(ts.x, crouchY, ts.z);
            var headFlat = head.position - transform.position;
            headFlat.y = 0;
            if (cc.enabled && headFlat.sqrMagnitude > 0.0004f)
            {
                var p0 = transform.position;
                cc.Move(headFlat);
                var moved = transform.position - p0;
                moved.y = 0;
                trackingSpace.position -= moved;
            }
            float h = Mathf.Clamp(head.position.y - transform.position.y + 0.12f, 0.9f, 2.1f);
            cc.height = h;
            cc.center = new Vector3(0, h * 0.5f + cc.skinWidth, 0);

            foreach (var hand in hands) hand.Tick();

            if (Alive && Game.I != null && Game.I.world != null)
            {
                if (transform.position.y < Game.I.world.killY) Game.I.LocalFell();
                else if (!Game.I.InPlane && Game.I.world.OutOfBounds(transform.position)) Game.I.OutOfBounds();
            }
        }

        public float HorizontalSpeed => new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;

        /// <summary>This player's pose for the network.</summary>
        public float[] BuildPose()
        {
            var p = new float[PoseData.Length];
            PoseData.Put(p, PoseData.FeetX, transform.position);
            p[PoseData.Yaw] = bodyYaw;
            PoseData.Put(p, PoseData.Head, head.position);
            PoseData.Put(p, PoseData.HeadRot, head.rotation);
            PoseData.Put(p, PoseData.LHand, hands[0].transform.position);
            PoseData.Put(p, PoseData.LRot, hands[0].transform.rotation);
            PoseData.Put(p, PoseData.RHand, hands[1].transform.position);
            PoseData.Put(p, PoseData.RRot, hands[1].transform.rotation);
            p[PoseData.Flags] = (crouched ? PoseData.FlagCrouch : 0) | (sprinting ? PoseData.FlagSprint : 0) | (cc.isGrounded ? PoseData.FlagGrounded : 0);
            var lg = hands[0].HeldGun;
            var rg = hands[1].HeldGun;
            p[PoseData.LGun] = lg != null ? lg.def.index : -1;
            p[PoseData.RGun] = rg != null ? rg.def.index : -1;
            p[PoseData.Speed] = HorizontalSpeed;
            PoseData.PutLook(p, Save.Settings.look);
            return p;
        }

        /// <summary>Move the player so their head ends up over pos, facing yaw.</summary>
        public void Teleport(Vector3 pos, float yaw)
        {
            cc.enabled = false;
            float headLocalYaw = head.localEulerAngles.y;
            transform.rotation = Quaternion.Euler(0, yaw - headLocalYaw, 0);
            transform.position = pos;
            var hl = head.localPosition;
            trackingSpace.localPosition = new Vector3(-hl.x, crouchY, -hl.z);
            vy = 0;
            cc.enabled = true;
            bodyYaw = yaw;
        }

        public void Die(Vector3 dir, bool headshot)
        {
            if (!Alive) return;
            Alive = false;
            crouched = sprinting = false;
            foreach (var h in hands) h.DropAll();
            if (selfAvatar != null)
            {
                selfAvatar.Pose(transform.position, bodyYaw, head.position, head.rotation,
                    hands[0].transform.position, hands[0].transform.rotation, hands[1].transform.position, hands[1].transform.rotation, 0, false);
                selfAvatar.SpawnRagdoll(cc.velocity, (dir.sqrMagnitude > 0.01f ? dir.normalized : -head.forward) * 30f + Vector3.up * 6f, headshot);
            }
        }

        public void Respawn(Vector3 pos, float yaw)
        {
            Teleport(pos, yaw);
            Alive = true;
            foreach (var g in LoadoutGuns()) { g.Refill(); if (!g.Held) g.GoHome(); }
            AudioManager.Sfx("spawn", pos, 0.6f);
        }
    }
}
