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
        float _fallSpeed, _lastPlanar;
        bool _wasGrounded = true, _leftFoot;
        Vector3 _lastHead;
        float _crouchOffset;
        public bool Frozen;
        float _smoothY;

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
            GameMenu.Init();   // the LEFT MENU button menu: party, friends, recent players, settings
            // never collide with our own body parts
            Physics.IgnoreCollision(p.Cc, p.HandL.GetComponent<Collider>());
            Physics.IgnoreCollision(p.Cc, p.HandR.GetComponent<Collider>());
            Physics.IgnoreCollision(p.HandL.GetComponent<Collider>(), p.HandR.GetComponent<Collider>());
            if (p.Body != null && p.Body.Rig != null) foreach (var c in p.Body.Rig.colliders)
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
            if (!XRInputs.VR || Dead) return;
            // latest poses right before the frame is drawn: head, then hands (+ what they hold) exactly on the controllers,
            // then the gear on your body, so nothing trails a frame behind
            XRInputs.RefreshPoses();
            ApplyHead();
            HandTargets(out var lp, out var lr, out var rp, out var rr);
            HandL.RenderSnap(lp, lr);
            HandR.RenderSnap(rp, rr);
            HandL.RenderSnapHeld();   // only now: a gun held in both hands is posed from both hands' latest spots
            HandR.RenderSnapHeld();
            if (Body != null) Body.Repose();   // arms follow the snapped hands
            Gear.Tick();
            AfterRenderSnap?.Invoke();   // things that ride on your hands (the field map) go on last
        }

        public static System.Action AfterRenderSnap;

        void ApplyHead()
        {
            Head.localPosition = XRInputs.HeadPos + Vector3.down * _crouchOffset;
            Head.localRotation = XRInputs.HeadRot;
        }

        bool _wasClimbing; float _climbStartY;

        /// <summary>
        /// Let go of a climb: if your chest is up past a ledge in front of you, mantle onto it (step up and forward),
        /// so climbing onto a wall top, a container or a roof edge just works.
        /// </summary>
        public void OnClimbReleased(Hand h)
        {
            if (HandL.Climbing || HandR.Climbing || !_wasClimbing) return;
            _wasClimbing = false;
            if (transform.position.y < _climbStartY + 0.25f) return;   // didn't actually climb up: no mantle
            Vector3 fwd = Head.forward; fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) return; fwd.Normalize();
            Vector3 feet = transform.position;
            Vector3 probe = Head.position + fwd * 0.55f + Vector3.up * 0.2f;
            Vector3 neck = transform.position + Vector3.up * Mathf.Max(0.3f, Cc.height - 0.3f);
            if (Physics.Linecast(neck, Head.position, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) return;   // your head is through a wall or roof
            if (Physics.Linecast(Head.position, probe, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) return;  // the ledge is on the other side of something
            if (Physics.Raycast(probe, Vector3.down, out var hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody == null)
            {
                float rise = hit.point.y - feet.y;
                bool room = !Physics.CheckCapsule(hit.point + Vector3.up * 0.3f, hit.point + Vector3.up * 1.6f, 0.2f, Ballistics.WallMask, QueryTriggerInteraction.Ignore);
                if (room && rise > 0.3f && rise < 1.9f && hit.normal.y > 0.6f)
                {
                    Cc.Move(Vector3.up * (rise + 0.05f));
                    Cc.Move(fwd * 0.6f);
                    _vy = 0f;
                }
            }
        }

        /// <summary>
        /// Head past a wall, roof or door (leaning or climbing into it): the view goes black, so walls can't be looked
        /// through. It's a wall between your body and your eyes, or your eyes inside one.
        /// </summary>
        // ---- safety net: never stuck under the world or inside a floor
        Vector3 _groundSafe; float _groundSafeYaw; bool _hasGroundSafe; float _safeSampleT, _embedT, _voidT;

        void FallRescue(float dt)
        {
            int mask = Ballistics.WallMask;
            Vector3 p = transform.position;
            // remember solid ground: standing on something, nothing overlapping the body
            _safeSampleT -= dt;
            bool embedded = Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * Mathf.Max(0.5f, Cc.height - 0.35f), Cc.radius * 0.8f, mask, QueryTriggerInteraction.Ignore);
            if (_safeSampleT <= 0f && Cc.isGrounded && !embedded && !HandL.Climbing && !HandR.Climbing)
            {
                _safeSampleT = 0.5f;
                if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.6f, mask, QueryTriggerInteraction.Ignore)) { _groundSafe = p + Vector3.up * 0.05f; _groundSafeYaw = Head.eulerAngles.y; _hasGroundSafe = true; }
            }
            if (!_hasGroundSafe) return;
            // under the world (nothing at all beneath you) or wedged inside a floor / wall
            bool voidBelow = !Cc.isGrounded && !Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 60f, mask, QueryTriggerInteraction.Ignore);
            _voidT = voidBelow ? _voidT + dt : 0f;
            _embedT = embedded && !HandL.Climbing && !HandR.Climbing ? _embedT + dt : 0f;
            if (_voidT > 0.8f || _embedT > 1.2f || p.y < _groundSafe.y - 40f)
            {
                _voidT = _embedT = 0f;
                HandL.StopClimbing(); HandR.StopClimbing();
                _vy = 0f; MoveVelocity = Vector3.zero;
                var back = _groundSafe; var yaw = _groundSafeYaw;
                Teleport(back, yaw);
                _groundSafe = back; _groundSafeYaw = yaw; _hasGroundSafe = true;
                Toast("Back on solid ground");
            }
        }

        void HeadCheck()
        {
            Vector3 neck = transform.position + Vector3.up * Mathf.Max(0.3f, Cc.height - 0.3f);
            bool blocked = Physics.Linecast(neck, Head.position, Ballistics.WallMask, QueryTriggerInteraction.Ignore)
                        || (Physics.CheckSphere(Head.position, 0.09f, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && !OverlapsOnlySelf());
            Hud.SetBlocked(blocked);
        }

        Vector3 _safePos; float _safeYaw, _openSkyT, _safeCheckT; bool _hasSafe;
        /// <summary>
        /// Indoor maps have a roof over every place you can stand. If there's open sky over you, you got out of the map
        /// (through a roof, out of a door that leads nowhere): go back to the last spot you stood properly.
        /// </summary>
        void KeepInside(float dt)
        {
            var map = RagoGame.I != null ? RagoGame.I.Map : null;
            if (map == null || !map.Indoor) { _hasSafe = false; return; }
            _safeCheckT -= dt;
            if (_safeCheckT > 0) return;
            _safeCheckT = 0.2f;
            bool roof = Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.up, 80f, Ballistics.WallMask, QueryTriggerInteraction.Ignore);
            if (roof && Cc.isGrounded && !HandL.Climbing && !HandR.Climbing)
            {
                _safePos = transform.position + Vector3.up * 0.05f; _safeYaw = Head.eulerAngles.y; _hasSafe = true; _openSkyT = 0; return;
            }
            if (roof) { _openSkyT = 0; return; }
            _openSkyT += 0.2f;
            if (_openSkyT >= 0.6f && _hasSafe)
            {
                _openSkyT = 0;
                HandL.StopClimbing(); HandR.StopClimbing();
                Teleport(_safePos, _safeYaw);
                Toast("You can't go out there");
            }
        }

        public Vector3 LastTeleportPos; public float LastTeleportYaw;

        public void Teleport(Vector3 pos, float yaw)
        {
            LastTeleportPos = pos; LastTeleportYaw = yaw;
            _hasSafe = false; _openSkyT = 0;   // new place: find a fresh safe spot here
            _hasGroundSafe = false; _voidT = _embedT = 0f;
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
            _vy = 0; _smoothY = 0;
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
            // ---------------- move first, then the hands: their targets are where the controllers are AFTER this frame's
            // walking, so a held gun doesn't trail (and swing the opposite way) while you move
            if (!Frozen) Locomotion(dt);
            if (!Frozen) { HeadCheck(); KeepInside(dt); FallRescue(dt); }
            HandTargets(out var lp, out var lr, out var rp, out var rr);
            HandL.Tick(XRInputs.Left, lp, lr);
            HandR.Tick(XRInputs.Right, rp, rr);
            if (!XRInputs.VR) DesktopActions();
            Gear.Tick();
            Hud.Tick(dt);
        }

        void HandTargets(out Vector3 lp, out Quaternion lr, out Vector3 rp, out Quaternion rr)
        {
            if (XRInputs.VR)
            {
                // the stick-click crouch lowers your view, so it lowers your hands by the same amount
                Vector3 down = Vector3.down * _crouchOffset;
                lp = Origin.TransformPoint(XRInputs.Left.Position) + down;
                lr = Origin.rotation * XRInputs.Left.Rotation;
                rp = Origin.TransformPoint(XRInputs.Right.Position) + down;
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
                bool scoped = gun.sight != null && gun.sight.sd.mag > 0f;
                Vector3 sightLocal = gun.sight != null && gun.sight.Lens != null ? gun.rig.root.transform.InverseTransformPoint(gun.sight.Lens.position)
                    : gun.sight != null ? gun.rig.root.transform.InverseTransformPoint(gun.sight.transform.TransformPoint(new Vector3(0, 0.03f, 0))) : gun.rig.sightLine.localPosition;
                Vector3 gunPos;
                if (Desktop.Aiming) gunPos = Head.position + view * new Vector3(0, 0, scoped ? 0.075f : gun.g.TwoHanded ? 0.16f : 0.32f) - gr * sightLocal;
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
                    if (!bp.Add(it, true)) { it.Drop(Vector3.zero, Vector3.zero); Toast("Doesn't fit in the " + bp.def.name); }
                }
                Desktop.RightGripToggle = false;
            }
            if (Desktop.Reload && gun != null) Gear.DesktopReload(gun);
            try { if (gun != null && Input.GetKeyDown(KeyCode.L)) gun.ToggleSide(); } catch { }   // desktop: L switches the light / laser
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

            // climbing: a hand gripping the world stays put and the body follows it (pull down to go up)
            Hand climb = HandL.Climbing && HandR.Climbing ? (HandL.ClimbStart > HandR.ClimbStart ? HandL : HandR) : HandL.Climbing ? HandL : HandR.Climbing ? HandR : null;
            if (climb != null)
            {
                Vector3 off = climb.ClimbAnchor - climb.TrackedPosition;
                off = Vector3.ClampMagnitude(off, 0.6f);
                if (!_wasClimbing) _climbStartY = transform.position.y;
                if (off.sqrMagnitude > 1e-6f) Cc.Move(off);
                _vy = 0f; MoveVelocity = Vector3.zero; _wasClimbing = true;
                if ((climb.ClimbAnchor - climb.TrackedPosition).sqrMagnitude > 0.8f * 0.8f) climb.StopClimbing();   // stretched too far: let go
                Vitals.Stamina = Mathf.Max(0f, Vitals.Stamina - dt * 3f);
                return;
            }

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
            // never taller than the space under the ceiling: a capsule grown into a roof gets pushed out on top of it
            float room = 2.3f;
            float r0 = Cc.radius + Cc.skinWidth + 0.02f;
            if (Physics.SphereCast(transform.position + Vector3.up * r0, Cc.radius * 0.95f, Vector3.up, out var ceil, 2.3f, Ballistics.WallMask, QueryTriggerInteraction.Ignore))
                room = r0 + ceil.distance + Cc.radius * 0.95f - 0.03f;
            Cc.height = Mathf.Clamp(headH + 0.08f, 0.6f, Mathf.Max(0.6f, room));
            Cc.center = new Vector3(0, Cc.height * 0.5f + Cc.skinWidth, 0);

            // stick movement relative to head yaw
            Vector3 fwd = Head.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float speed = Sprinting ? SprintSpeed * (Vitals.StimBoost > 0 ? 1.15f : 1f) : MoveSpeed * (Crouched || headH < 1.25f ? 0.55f : 1f);
            speed *= Vitals.LegPenalty;
            Vector3 vel = (fwd * move.y + right * move.x) * speed;
            MoveVelocity = Vector3.Lerp(MoveVelocity, vel, dt * 10f);
            if (Cc.isGrounded && _vy < 0) _vy = -1f;
            // jump: A (or X) - always, gun in hand or not (the mag release is on B / Y)
            bool jump = XRInputs.VR ? XRInputs.Right.PrimaryDown || XRInputs.Left.PrimaryDown : Desktop.Jump;
            if (jump && Cc.isGrounded && !Frozen && !Dead && Vitals.Stamina > 6f)
            {
                _vy = 4.4f;                                   // about 1 m of air
                Vitals.Stamina -= 8f;
                Crouched = false; if (!XRInputs.VR) Desktop.Crouch = false;
                Sfx.Play(Synth.Footstep(GroundSurface(), Random.Range(0, 8), true), transform.position, 0.35f, 0.95f, 1f, 20f);   // push off
            }
            _vy += Physics.gravity.y * dt;
            Vector3 step = (MoveVelocity + Vector3.up * _vy) * dt;
            float yBefore = transform.position.y;
            Cc.Move(step);
            if (XRInputs.VR)
            {
                // the camera rides on the capsule: soak up its little vertical bumps (stairs, uneven ground, ground
                // snapping) and ease the view after it instead of jolting it - big falls/jumps still follow directly
                float dy = transform.position.y - yBefore;
                if (Cc.isGrounded && Mathf.Abs(dy) < 0.45f) _smoothY -= dy;
                _smoothY = Mathf.Abs(_smoothY) > 0.5f ? 0f : Mathf.Lerp(_smoothY, 0f, 1f - Mathf.Exp(-12f * dt));
                var o = Origin.localPosition; o.y = _smoothY; Origin.localPosition = o;
            }
            if (Sprinting) Vitals.Stamina -= dt * 14f;

            // footsteps: left, right, left... from each foot, heel-then-toe, heavier when running
            float planar = MoveVelocity.magnitude;
            _stepDist += planar * dt;
            bool grounded = Cc.isGrounded;
            if (!grounded) _fallSpeed = Mathf.Max(_fallSpeed, -_vy);
            if (grounded && !_wasGrounded && _fallSpeed > 2.5f)
            {
                // landing from a jump or a drop
                Sfx.Play(Synth.Land(GroundSurface()), transform.position, Mathf.Clamp(0.25f + _fallSpeed * 0.06f, 0.3f, 0.75f), Random.Range(0.94f, 1.04f), 1f, 30f);
                Noise.Emit(transform.position, 6f, 0);
                _stepDist = 0;
            }
            if (grounded) _fallSpeed = 0;
            _wasGrounded = grounded;
            if (grounded && _stepDist > (Sprinting ? 1.5f : 1.1f))
            {
                _stepDist = 0;
                PlayStep(Sprinting, Crouched ? 0.12f : Sprinting ? 0.5f : 0.33f);
                if (!Crouched) Noise.Emit(transform.position, Sprinting ? 5f : 1.8f, 0);   // scavs barely hear footsteps
            }
            // stopping short from a walk or run: one scuff
            if (grounded && _lastPlanar > 1.2f && planar < 0.35f && !Crouched) { PlayStep(_lastPlanar > 3.4f, 0.22f); _stepDist = 0; }
            _lastPlanar = planar;

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
                if (Body != null && Body.Rig != null && Body.Rig.colliders.Contains(c)) continue;
                if (c.GetComponentInParent<Item>() != null) continue;
                return false;
            }
            return true;
        }

        void PlayStep(bool run, float vol)
        {
            _leftFoot = !_leftFoot;
            Vector3 fwd = Head.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * (_leftFoot ? -0.12f : 0.12f);
            Sfx.Play(Synth.Footstep(GroundSurface(), Random.Range(0, 8), run), transform.position + side + Vector3.up * 0.05f,
                vol * Random.Range(0.85f, 1.1f), Random.Range(0.94f, 1.06f), 0.8f, 25f);
        }

        Synth.Surface GroundSurface() => Footsteps.SurfaceUnder(transform.position);

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
