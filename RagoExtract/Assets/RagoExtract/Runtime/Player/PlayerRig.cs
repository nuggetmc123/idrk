using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The VR player: tracked head + physics hands, smooth locomotion with sprint (stamina) and crouch,
    /// snap/smooth turn, room-scale collision, full ragdoll body, vitals and HUD.
    /// Controls: Left stick = move (click = sprint), Right stick = turn (click = crouch toggle, down = crouch while held).
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public Transform Origin, Head;
        public Camera Cam;
        public Hand HandL, HandR;
        public CharacterController Cc;
        public PlayerBody Body;
        public Vitals Vitals;
        public Gear Gear;
        public Hud Hud;
        public bool Dead => Vitals != null && !Vitals.Alive;
        public Vector3 PlatformVelocity;
        public Vector3 MoveVelocity { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Crouched;
        public static bool SmoothTurn = false;
        public static float SnapAngle = 45f;
        public static float MoveSpeed = 2.3f, SprintSpeed = 4.6f;
        float _vy, _snapCooldown, _stepDist, _deathT;
        bool _sprintLatch;
        Vector3 _lastHead;
        float _crouchOffset;
        public bool Frozen;

        public static PlayerRig Create()
        {
            var go = new GameObject("Player");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<PlayerRig>();
            p.Cc = go.AddComponent<CharacterController>();
            p.Cc.radius = 0.2f; p.Cc.height = 1.7f; p.Cc.center = new Vector3(0, 0.85f, 0);
            p.Cc.stepOffset = 0.35f; p.Cc.slopeLimit = 50f; p.Cc.skinWidth = 0.03f; p.Cc.minMoveDistance = 0f;
            go.layer = Layers.Player < 0 ? 0 : Layers.Player;
            p.Origin = new GameObject("TrackingOrigin").transform;
            p.Origin.SetParent(go.transform, false);
            var cam = new GameObject("Head");
            cam.transform.SetParent(p.Origin, false);
            cam.transform.localPosition = new Vector3(0, 1.65f, 0);
            p.Head = cam.transform;
            p.Cam = cam.AddComponent<Camera>();
            p.Cam.nearClipPlane = 0.03f;
            p.Cam.farClipPlane = 900f;
            p.Cam.clearFlags = CameraClearFlags.Skybox;
            p.Cam.tag = "MainCamera";
            cam.AddComponent<AudioListener>();
            p.HandL = Hand.Create(p, true);
            p.HandR = Hand.Create(p, false);
            p.Vitals = go.AddComponent<Vitals>();
            p.Vitals.rig = p;
            p.Gear = go.AddComponent<Gear>();
            p.Gear.Init(p);
            p.Body = PlayerBody.Create(p);
            p.Hud = Hud.Create(p);
            // never collide with our own body parts
            Physics.IgnoreCollision(p.Cc, p.HandL.GetComponent<Collider>());
            Physics.IgnoreCollision(p.Cc, p.HandR.GetComponent<Collider>());
            Physics.IgnoreCollision(p.HandL.GetComponent<Collider>(), p.HandR.GetComponent<Collider>());
            foreach (var c in p.Body.Rig.colliders)
            {
                Physics.IgnoreCollision(p.Cc, c);
                Physics.IgnoreCollision(p.HandL.GetComponent<Collider>(), c);
                Physics.IgnoreCollision(p.HandR.GetComponent<Collider>(), c);
            }
            Application.onBeforeRender += p.OnBeforeRender;
            return p;
        }

        void OnDestroy() { Application.onBeforeRender -= OnBeforeRender; }

        void OnBeforeRender()
        {
            // late head update for lowest latency
            if (XRInputs.VR && !Dead) ApplyHead();
        }

        void ApplyHead()
        {
            Head.localPosition = XRInputs.HeadPos + Vector3.down * _crouchOffset;
            Head.localRotation = XRInputs.HeadRot;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Cc.enabled = false;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Origin.localPosition = Vector3.zero;
            if (XRInputs.VR)
            {
                // put the tracked head over the spawn point
                Vector3 h = Origin.TransformPoint(XRInputs.HeadPos);
                Vector3 off = (pos - h); off.y = 0;
                Origin.position += off;
                // align view yaw to spawn yaw
                float headYaw = (Origin.rotation * XRInputs.HeadRot).eulerAngles.y;
                Origin.RotateAround(pos, Vector3.up, Mathf.DeltaAngle(headYaw, yaw));
            }
            else { Desktop.Yaw = yaw; Desktop.Pitch = 0; }
            Cc.enabled = true;
            _vy = 0;
            _lastHead = Head.position;
            Body.Snap();
        }

        void Update()
        {
            XRInputs.Update();
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (Dead) { DeathCam(dt); Hud.Tick(dt); return; }
            // ---------------- head
            if (XRInputs.VR) ApplyHead();
            else
            {
                float h = Desktop.Crouch ? 1.05f : 1.65f;
                Head.localPosition = new Vector3(0, Mathf.Lerp(Head.localPosition.y, h, dt * 10f), 0);
                Head.localRotation = Quaternion.Euler(Desktop.Pitch, 0, 0);
                transform.rotation = Quaternion.Euler(0, Desktop.Yaw, 0);
                Origin.localRotation = Quaternion.identity;
            }
            // ---------------- hands
            HandTargets(out var lp, out var lr, out var rp, out var rr);
            HandL.Tick(XRInputs.Left, lp, lr);
            HandR.Tick(XRInputs.Right, rp, rr);
            if (!XRInputs.VR) DesktopActions();
            if (!Frozen) Locomotion(dt);
            Gear.Tick();
            Hud.Tick(dt);
        }

        void HandTargets(out Vector3 lp, out Quaternion lr, out Vector3 rp, out Quaternion rr)
        {
            if (XRInputs.VR)
            {
                lp = Origin.TransformPoint(XRInputs.Left.Position);
                lr = Origin.rotation * XRInputs.Left.Rotation;
                rp = Origin.TransformPoint(XRInputs.Right.Position);
                rr = Origin.rotation * XRInputs.Right.Rotation;
                return;
            }
            // desktop: hands float in front of the camera, guns line up with the view when aiming
            Quaternion view = Head.rotation;
            rp = Head.position + view * new Vector3(0.17f, -0.2f, 0.38f);
            rr = view * Quaternion.Euler(-5, -4, 0);
            lp = Head.position + view * new Vector3(-0.2f, -0.28f, 0.3f);
            lr = view * Quaternion.Euler(10, 10, 0);
            var gun = HandR.Held as Gun;
            if (gun != null && !HandR.HeldAsSecondary)
            {
                Quaternion gr = view;
                Vector3 sightLocal = gun.sight != null ? gun.rig.root.transform.InverseTransformPoint(gun.sight.transform.TransformPoint(new Vector3(0, 0.03f, 0))) : gun.rig.sightLine.localPosition;
                Vector3 gunPos;
                if (Desktop.Aiming) gunPos = Head.position + view * new Vector3(0, 0, gun.g.TwoHanded ? 0.16f : 0.32f) - gr * sightLocal;
                else gunPos = Head.position + view * new Vector3(0.16f, -0.2f, 0.3f) - gr * new Vector3(0, gun.rig.boreY, 0);
                rp = gunPos + gr * gun.grip.localPosition;
                rr = gr * gun.grip.localRotation * Quaternion.Inverse(HandR.Anchor.localRotation);
                if (gun.secondGrip != null) { lp = gunPos + gr * gun.secondGrip.localPosition; lr = gr * Quaternion.Euler(0, 0, -60); }
            }
        }

        void DesktopActions()
        {
            // auto two-hand long guns in desktop mode
            var gun = HandR.Held as Gun;
            if (gun != null && gun.g.TwoHanded && gun.secondGrip != null && HandL.Held == null && HandL.Racking == null)
            {
                HandL.Held = gun; HandL.HeldAsSecondary = true; gun.secondaryHand = HandL; Desktop.LeftGripToggle = true;
            }
            if (Desktop.Interact && (HandR.Held == null)) DesktopInteract();
            if (Desktop.Click || (Desktop.Interact && HandR.Held == null))
            {
                // click world buttons under the crosshair
                if (Physics.Raycast(Head.position, Head.forward, out var h, 6f, ~0, QueryTriggerInteraction.Collide))
                {
                    var b = h.collider.GetComponent<WorldButton>();
                    if (b != null) b.Click();
                    var ia = h.collider.GetComponentInParent<Interactable>();
                    if (ia != null && Desktop.Interact) ia.OnGrab(HandR);
                }
            }
            if (Desktop.RightGripToggle && HandR.Held == null)
            {
                // E: pull the item you're looking at into your hand
                if (Physics.SphereCast(Head.position, 0.06f, Head.forward, out var h, 3f, ~0, QueryTriggerInteraction.Collide))
                {
                    var it = h.collider.GetComponentInParent<Item>();
                    if (it != null) HandR.Grab(it);
                }
                if (HandR.Held == null) Desktop.RightGripToggle = false;
            }
            if (!Desktop.RightGripToggle && HandL.HeldAsSecondary) { HandL.ForceRelease(); Desktop.LeftGripToggle = false; }
            if (Desktop.Drop && HandR.Held != null)
            {
                // G: drop, or stash into the open backpack held in the left hand
                if (HandL.Held is Backpack bp && !(HandR.Held is Gun g2 && g2.g.TwoHanded))
                {
                    var it = HandR.Held;
                    HandR.ForceRelease(true);
                    if (!bp.Container.Add(it, true)) it.Drop(Vector3.zero, Vector3.zero);
                }
                Desktop.RightGripToggle = false;
            }
            if (Desktop.Reload && gun != null) Gear.DesktopReload(gun);
            if (Desktop.Equip >= 0) Gear.DesktopEquip(Desktop.Equip);
            if (Desktop.Backpack) Gear.DesktopBackpack();
            if (Desktop.Armor) Gear.DesktopArmor();
        }

        void DesktopInteract() { }

        void Locomotion(float dt)
        {
            Vector2 move = XRInputs.VR ? XRInputs.Left.Stick : Desktop.Move;
            Vector2 turn = XRInputs.VR ? XRInputs.Right.Stick : Vector2.zero;
            if (move.magnitude < 0.15f) move = Vector2.zero;
            // sprint: click the left stick, stays on while you keep moving forward
            if (XRInputs.VR) { if (XRInputs.Left.StickClickDown) _sprintLatch = !_sprintLatch; if (move.magnitude < 0.2f) _sprintLatch = false; }
            else _sprintLatch = Desktop.Sprint;
            Sprinting = _sprintLatch && move.y > 0.3f && Vitals.Stamina > 1f && !Crouched;
            // crouch toggle: right stick click (and physically crouching always works)
            if (XRInputs.VR && XRInputs.Right.StickClickDown) Crouched = !Crouched;
            if (!XRInputs.VR) Crouched = Desktop.Crouch;
            _crouchOffset = Mathf.MoveTowards(_crouchOffset, Crouched && XRInputs.VR ? 0.55f : 0f, dt * 2.5f);

            // room scale: move the capsule under the head
            Vector3 headW = Head.position;
            Vector3 delta = headW - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > 0.0004f)
            {
                Vector3 before = transform.position;
                Cc.Move(delta);
                Vector3 moved = transform.position - before; moved.y = 0;
                Origin.position -= moved;
            }
            // capsule height from head height
            float headH = Mathf.Clamp(Head.position.y - transform.position.y, 0.6f, 2.1f);
            Cc.height = Mathf.Max(0.6f, headH + 0.08f);
            Cc.center = new Vector3(0, Cc.height * 0.5f + Cc.skinWidth, 0);

            // stick movement relative to head yaw
            Vector3 fwd = Head.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float speed = Sprinting ? SprintSpeed * (Vitals.StimBoost > 0 ? 1.15f : 1f) : MoveSpeed * (Crouched || headH < 1.25f ? 0.55f : 1f);
            speed *= Vitals.LegPenalty;
            Vector3 vel = (fwd * move.y + right * move.x) * speed;
            MoveVelocity = Vector3.Lerp(MoveVelocity, vel, dt * 10f);
            if (Cc.isGrounded && _vy < 0) _vy = -1f;
            _vy += Physics.gravity.y * dt;
            Vector3 step = (MoveVelocity + Vector3.up * _vy) * dt;
            Cc.Move(step);
            if (Sprinting) Vitals.Stamina -= dt * 14f;

            // footsteps
            float planar = MoveVelocity.magnitude;
            _stepDist += planar * dt;
            if (Cc.isGrounded && _stepDist > (Sprinting ? 1.5f : 1.1f))
            {
                _stepDist = 0;
                var surf = GroundSurface();
                Sfx.Play(Synth.Footstep(surf, Random.Range(0, 4)), transform.position, Sprinting ? 0.55f : Crouched ? 0.15f : 0.35f, 1f + Random.Range(-0.08f, 0.08f), 1f, 25f);
                Noise.Emit(transform.position, Sprinting ? 16f : Crouched ? 3f : 8f, 0);
            }

            // turning
            if (XRInputs.VR)
            {
                _snapCooldown -= dt;
                if (SmoothTurn) { if (Mathf.Abs(turn.x) > 0.2f) Origin.RotateAround(Head.position, Vector3.up, turn.x * 120f * dt); }
                else if (Mathf.Abs(turn.x) > 0.7f && _snapCooldown <= 0)
                {
                    Origin.RotateAround(Head.position, Vector3.up, Mathf.Sign(turn.x) * SnapAngle);
                    _snapCooldown = 0.3f;
                }
                if (Mathf.Abs(turn.x) < 0.3f) _snapCooldown = 0;
            }
            // head inside a wall -> fade to black
            bool blocked = Physics.CheckSphere(Head.position, 0.09f, ~0, QueryTriggerInteraction.Ignore) && !OverlapsOnlySelf();
            Hud.SetBlocked(blocked);
            _lastHead = Head.position;
        }

        static readonly Collider[] _ov = new Collider[8];
        bool OverlapsOnlySelf()
        {
            int n = Physics.OverlapSphereNonAlloc(Head.position, 0.09f, _ov, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _ov[i];
                if (c == Cc) continue;
                if (c.attachedRigidbody != null && (c.attachedRigidbody == HandL.rb || c.attachedRigidbody == HandR.rb)) continue;
                if (Body.Rig.colliders.Contains(c)) continue;
                if (c.GetComponentInParent<Item>() != null) continue;
                return false;
            }
            return true;
        }

        Synth.Surface GroundSurface()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var h, 1f, ~0, QueryTriggerInteraction.Ignore))
                return SurfaceTag.Of(h.collider);
            return Synth.Surface.Concrete;
        }

        public bool ShoulderNear(Vector3 p)
        {
            Vector3 r = Head.position + Head.rotation * new Vector3(0.17f, -0.22f, -0.02f);
            Vector3 l = Head.position + Head.rotation * new Vector3(-0.17f, -0.22f, -0.02f);
            return Vector3.Distance(p, r) < 0.16f || Vector3.Distance(p, l) < 0.16f;
        }

        public void Suppress(float a) { Hud.Suppress(a); }
        public void Toast(string msg) { Hud.Toast(msg); }

        // ---------------------------------------------------------------------------------- death
        public void OnDeath(Vector3 impulse, Vector3 point)
        {
            HandL.ForceRelease(); HandR.ForceRelease();
            HandL.SetVisible(false); HandR.SetVisible(false);
            Body.Die(impulse, point, MoveVelocity);
            _deathT = 0;
            Cc.enabled = false;
        }

        public void Revive()
        {
            Origin.localPosition = Vector3.zero;
            Origin.localRotation = Quaternion.identity;
            Head.localPosition = new Vector3(0, 1.65f, 0);
            Body.Revive();
            HandL.SetVisible(true); HandR.SetVisible(true);
            Cc.enabled = true;
        }

        void DeathCam(float dt)
        {
            _deathT += dt;
            // third-person view orbiting your ragdoll
            var c = Body.Rig.chest.position;
            Quaternion orbit = Quaternion.Euler(30f, _deathT * 12f, 0);
            Vector3 want = c + orbit * new Vector3(0, 0.6f, -2.6f);
            if (Physics.Linecast(c, want, out var h, ~0, QueryTriggerInteraction.Ignore) && h.collider.attachedRigidbody == null) want = h.point + (c - want).normalized * 0.2f;
            if (XRInputs.VR)
            {
                // in VR the headset owns the camera rotation: move the tracking origin instead
                ApplyHead();
                Origin.position += (want - Head.position) * Mathf.Clamp01(dt * 3f);
            }
            else
            {
                Head.position = Vector3.Lerp(Head.position, want, dt * 3f);
                Head.rotation = Quaternion.Slerp(Head.rotation, Quaternion.LookRotation(c - Head.position), dt * 4f);
            }
        }
    }
}
