using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Nullbound
{
    /// <summary>What a fighter wants to do this frame: from your controllers, or from a bot's brain.</summary>
    public struct FighterCommand
    {
        public Vector3 move;    // world space, length 0..1
        public bool jump, dash, special, block;
    }

    /// <summary>
    /// One fighter in the arena: you, another player, or a bot. Photon Fusion Shared Mode: each fighter is owned
    /// (state authority) by the game that spawned it, which moves it, detects its punches and is the only one
    /// that changes its health. Everyone else sees its head, hands and body from the networked poses.
    ///
    /// PUNCH: swing a fist fast (over ~2.4 m/s) into someone. Faster = harder. Hits in a row on the same target
    /// build a combo; the 4th launches them. BLOCK: both fists up in front of your face (takes a fifth of the
    /// damage, abilities a half; too many blocked hits break your guard). DODGE: B dashes the way the stick points,
    /// and you can't be hit while dashing. SPECIAL: X (see <see cref="FighterInfo"/>).
    /// </summary>
    public partial class Fighter : NetworkBehaviour, IHittable
    {
        public static Fighter Local { get; private set; }
        public static readonly List<Fighter> All = new List<Fighter>();
        /// <summary>Set by SessionManager around Runner.Spawn so the new fighter knows it's a bot.</summary>
        public static bool SpawningBot;
        public static FighterKind SpawningBotKind;

        [Networked] public FighterKind Kind { get; set; }
        [Networked] public NetworkBool IsBot { get; set; }
        [Networked] public NetworkString<_16> FighterName { get; set; }
        [Networked] public float Health { get; set; }
        [Networked] public NetworkBool Blocking { get; set; }
        [Networked] public NetworkBool Down { get; set; }
        [Networked] public NetworkBool Dodging { get; set; }
        [Networked] public NetworkBool Empowered { get; set; }
        [Networked] public int KOs { get; set; }
        [Networked] public int Downs { get; set; }
        [Networked] public int Streak { get; set; }
        /// <summary>Goes up every time this fighter uses its special (bots watch it to learn to dodge).</summary>
        [Networked] public int SpecialCount { get; set; }
        /// <summary>Who's playing it (see <see cref="PlayerIdentity"/>); empty for bots.</summary>
        [Networked] public NetworkString<_16> PlayerId { get; set; }
        [Networked] public NetworkString<_16> DeviceHash { get; set; }
        /// <summary>An invisible moderator (only obeyed for a proven moderator, see <see cref="Ghost"/>).</summary>
        [Networked] public NetworkBool Hidden { get; set; }
        /// <summary>Status effects everyone sees: 1 frozen, 2 burning, 4 barrier, 8 armour, 16 invincible mod, 32 tumbling.</summary>
        [Networked] public byte Status { get; set; }
        /// <summary>Special (super) charge 0..100, so everyone can see it on the fighter's right arm.</summary>
        [Networked] public byte SuperCharge { get; set; }
        /// <summary>How closed each hand is (left in the high 4 bits, right in the low 4), so others see your fingers move.</summary>
        [Networked] public byte Fingers { get; set; }
        [Networked] public Vector3 NetBody { get; set; }
        [Networked] public Vector3 NetHead { get; set; }
        [Networked] public Quaternion NetHeadRot { get; set; }
        [Networked] public Vector3 NetLeft { get; set; }
        [Networked] public Quaternion NetLeftRot { get; set; }
        [Networked] public Vector3 NetRight { get; set; }
        [Networked] public Quaternion NetRightRot { get; set; }

        public FighterStats Stats => FighterInfo.Get(Kind);
        public const byte Frozen = 1, Burning = 2, Shielded = 4, Armored = 8, Immune = 16, Tumbling = 32;
        /// <summary>How long a super hit sends you rolling (ragdoll), and how much harder supers hit.</summary>
        public const float TumbleTime = 1.3f, SuperDamage = 1.25f, SuperKnock = 1.4f;
        public bool HasStatus(byte bit) => (Status & bit) != 0;

        /// <summary>True once this fighter's game proved it owns its Player ID (a signed hello).</summary>
        public bool Verified { get; private set; }
        public bool IsVerifiedMod => !IsBot && Verified && Moderation.IsMod(PlayerId.ToString());
        /// <summary>An invisible moderator: not drawn or hittable for anyone else.</summary>
        public bool Ghost => !IsMine && Hidden && IsVerifiedMod;
        public bool IsReady => Object != null && Object.IsValid;
        public bool IsMine => IsReady && HasStateAuthority;
        public string DisplayName => FighterName.ToString();
        public float HealthFraction => Stats.maxHealth > 0f ? Mathf.Clamp01(Health / Stats.maxHealth) : 0f;

        /// <summary>Where this fighter's head and hands are drawn (owner: live tracking, others: smoothed network values).</summary>
        public Vector3 HeadPos { get; private set; }
        public Quaternion HeadRot { get; private set; } = Quaternion.identity;
        public Vector3 LeftPos { get; private set; }
        public Quaternion LeftRot { get; private set; } = Quaternion.identity;
        public Vector3 RightPos { get; private set; }
        public Quaternion RightRot { get; private set; } = Quaternion.identity;
        public Vector3 Center => HeadPos + Vector3.down * 0.4f;
        public Vector3 FacingFlat
        {
            get
            {
                Vector3 f = HeadRot * Vector3.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>Hand speeds (m/s, relative to the body): bots read these to see a punch coming.</summary>
        /// <summary>This player's hand from their controller grip and trigger; a fast swing always makes a fist.</summary>
        public float HandCurl(bool left) => (left ? LeftHandSpeed : RightHandSpeed) > 2.5f ? 1f : FightInput.HandCurl(left);
        /// <summary>How closed a hand looks on everyone's screen (bots always make fists).</summary>
        public float ShownCurl(bool left) => IsBot ? 1f : ((left ? Fingers >> 4 : Fingers & 15) / 15f);
        public float LeftHandSpeed { get; private set; }
        public float RightHandSpeed { get; private set; }

        // Owner-only state.
        /// <summary>Where the head and hands come from: your VR rig, or a bot's puppet.</summary>
        public Transform poseRoot, poseHead, poseLeft, poseRight;
        public FighterCommand command;
        /// <summary>Fired on the owner when this fighter takes a hit (bots learn from it).</summary>
        public event System.Action<Fighter, bool> HitTaken;

        CharacterController cc;
        Vector3 velocity, knock;
        float tumbleUntil;
        bool grounded, wasGrounded;
        float dashUntil = -1f, dodgeUntil = -1f, nextDash, nextSpecial, stunUntil, invulnerableUntil, respawnAt, empoweredUntil;
        Vector3 dashDir;
        float guard = 100f, guardBrokenUntil;
        float nextPunchLeft, nextPunchRight;
        Vector3 prevLeftLocal, prevRightLocal;
        bool havePrevHands;
        Fighter lastAttacker;
        float lastAttackedAt = -100f;
        IHittable comboTarget;
        int combo;
        float lastHitAt;
        bool slamPending;
        float fallStartY;
        public const float KillY = -12f;

        /// <summary>0..1 how ready the special is (for the HUD).</summary>
        public float SpecialReady => Mathf.Clamp01(1f - (nextSpecial - Time.time) / Mathf.Max(0.1f, Stats.specialCooldown));
        public float DashReady => Mathf.Clamp01(1f - (nextDash - Time.time) / Mathf.Max(0.1f, Stats.dashCooldown));
        public float Guard => guard;
        public bool GuardBroken => Time.time < guardBrokenUntil;
        public int Combo => Time.time - lastHitAt < 1f ? combo : 0;
        public float RespawnIn => Down ? Mathf.Max(0f, respawnAt - Time.time) : 0f;
        public bool Grounded => grounded;

        // ================================================================== lifecycle

        public override void Spawned()
        {
            All.Add(this);
            cc = GetComponent<CharacterController>();
            if (HasStateAuthority)
            {
                if (SpawningBot)
                {
                    IsBot = true;
                    Kind = SpawningBotKind;
                    FighterName = BotBrain.RandomName();
                }
                else
                {
                    Kind = Collection.IsUnlocked((int)GameSettings.Fighter) ? GameSettings.Fighter : FighterKind.Brawler;
                    FighterName = PlayerProfile.Nickname;
                    PlayerId = PlayerIdentity.PlayerId;
                    DeviceHash = PlayerIdentity.DeviceHash;
                    Verified = true;
                    Local = this;
                    SendHello();
                }
                Health = Stats.maxHealth;
                MoveTo(SpawnPoint.Pick(transform.position));
                invulnerableUntil = Time.time + 2f;
                if (IsBot) gameObject.AddComponent<BotBrain>();
                else if (VRRig.Instance != null) UseRig(VRRig.Instance);
            }
            else if (cc != null)
            {
                // Someone else's fighter: moved by the network, solid so you can't walk through it.
                cc.enabled = false;
                var solid = gameObject.AddComponent<CapsuleCollider>();
                solid.center = new Vector3(0f, 0.9f, 0f);
                solid.height = 1.8f;
                solid.radius = 0.28f;
                transform.position = NetBody;
                if (!IsBot) RPC_RequestHello();
            }
            BuildHurtboxes();
            gameObject.AddComponent<FighterVisual>().Init(this);
            HeadPos = transform.position + Vector3.up * 1.6f;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            if (Local == this) Local = null;
            if (VRRig.Instance != null && VRRig.Instance.IsAttachedTo(this)) VRRig.Instance.Detach();
        }

        void UseRig(VRRig rig)
        {
            poseRoot = rig.transform;
            poseHead = rig.head;
            poseLeft = rig.leftHand;
            poseRight = rig.rightHand;
            rig.AttachTo(this);
        }

        Transform bodyBox, headBox;

        void BuildHurtboxes()
        {
            bodyBox = new GameObject("BodyHurtbox").transform;
            bodyBox.SetParent(transform, false);
            var cap = bodyBox.gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = 0.24f;
            cap.height = 1.1f;
            bodyBox.gameObject.AddComponent<Hurtbox>().owner = this;

            headBox = new GameObject("HeadHurtbox").transform;
            headBox.SetParent(transform, false);
            var sph = headBox.gameObject.AddComponent<SphereCollider>();
            sph.isTrigger = true;
            sph.radius = 0.15f;
            var hb = headBox.gameObject.AddComponent<Hurtbox>();
            hb.owner = this;
            hb.damageScale = 1.25f;
        }

        // ================================================================== every frame

        void Update()
        {
            if (!IsReady) return;
            if (HasStateAuthority)
            {
                if (!IsBot && poseHead == null && VRRig.Instance != null) UseRig(VRRig.Instance);
                if (!IsBot) Hidden = ModPowers.InvisibleNow;
                UpdateStatus();
                ReadPose();
                if (!IsBot) command = PlayerCommand();
                Simulate(Time.deltaTime);
                WriteNetwork();
            }
            else ReadNetwork(Time.deltaTime);
            PlaceHurtboxes();
        }

        void ReadPose()
        {
            if (poseHead == null) return;
            HeadPos = poseHead.position; HeadRot = poseHead.rotation;
            if (poseLeft != null) { LeftPos = poseLeft.position; LeftRot = poseLeft.rotation; }
            if (poseRight != null) { RightPos = poseRight.position; RightRot = poseRight.rotation; }
        }

        void WriteNetwork()
        {
            NetBody = transform.position;
            NetHead = HeadPos; NetHeadRot = HeadRot;
            NetLeft = LeftPos; NetLeftRot = LeftRot;
            NetRight = RightPos; NetRightRot = RightRot;
        }

        /// <summary>Everyone else's fighters: glide toward the latest networked poses.</summary>
        void ReadNetwork(float dt)
        {
            float k = 1f - Mathf.Exp(-22f * dt);
            Vector3 body = NetBody;
            if ((transform.position - body).sqrMagnitude > 25f) transform.position = body; // teleported
            else transform.position = Vector3.Lerp(transform.position, body, k);
            Vector3 shift = body - transform.position; // draw the head and hands relative to where the body is drawn
            Vector3 newL = Vector3.Lerp(LeftPos, NetLeft - shift, k), newR = Vector3.Lerp(RightPos, NetRight - shift, k);
            LeftHandSpeed = (newL - LeftPos).magnitude / Mathf.Max(dt, 0.001f);
            RightHandSpeed = (newR - RightPos).magnitude / Mathf.Max(dt, 0.001f);
            HeadPos = Vector3.Lerp(HeadPos, NetHead - shift, k);
            LeftPos = newL; RightPos = newR;
            HeadRot = Quaternion.Slerp(HeadRot, NetHeadRot, k);
            LeftRot = Quaternion.Slerp(LeftRot, NetLeftRot, k);
            RightRot = Quaternion.Slerp(RightRot, NetRightRot, k);
        }

        void PlaceHurtboxes()
        {
            if (bodyBox == null) return;
            Vector3 chest = HeadPos + Vector3.down * 0.55f;
            bodyBox.position = new Vector3(chest.x, Mathf.Max(transform.position.y + 0.55f, chest.y), chest.z);
            bodyBox.rotation = Quaternion.identity;
            headBox.position = HeadPos;
            bool hittable = !Down && !(Hidden && (IsMine ? ModPowers.InvisibleNow : IsVerifiedMod));
            bodyBox.gameObject.SetActive(hittable);
            headBox.gameObject.SetActive(hittable);
        }

        // ================================================================== your controls

        bool turnLatched;

        FighterCommand PlayerCommand()
        {
            var c = new FighterCommand();
            if (WristMenu.Open && !VRRig.IsXR) return c;
            Vector2 stick = FightInput.Move;
            Vector3 fwd = FacingFlat, right = Vector3.Cross(Vector3.up, fwd);
            c.move = Vector3.ClampMagnitude(fwd * stick.y + right * stick.x, 1f);
            c.jump = FightInput.JumpPressed;
            c.dash = FightInput.DashPressed;
            c.special = FightInput.SpecialPressed;
            c.block = FightInput.KeyBlockHeld || GuardPose();

            // Turning rotates your rig around your head.
            float degrees = GameSettings.TurnAmount(FightInput.Turn, ref turnLatched, Time.deltaTime);
            if (degrees != 0f && poseRoot != null && poseHead != null) poseRoot.RotateAround(poseHead.position, Vector3.up, degrees);
            return c;
        }

        /// <summary>Both fists up in front of your face.</summary>
        bool GuardPose()
        {
            if (poseHead == null || poseLeft == null || poseRight == null || !VRRig.IsXR) return false;
            Vector3 fwd = poseHead.forward;
            bool Up(Vector3 hand)
            {
                Vector3 d = hand - poseHead.position;
                return d.magnitude < 0.42f && Vector3.Dot(d, fwd) > 0.04f && d.y > -0.32f;
            }
            return Up(poseLeft.position) && Up(poseRight.position);
        }

        // ================================================================== simulation (owner)

        void Simulate(float dt)
        {
            if (dt <= 0f) return;
            var s = Stats;
            float now = Time.time;
            byte charge = (byte)Mathf.RoundToInt(SpecialReady * 100f);
            if (charge != SuperCharge) SuperCharge = charge;
            if (!IsBot)
            {
                byte fingers = (byte)((Mathf.RoundToInt(HandCurl(true) * 15f) << 4) | Mathf.RoundToInt(HandCurl(false) * 15f));
                if (fingers != Fingers) Fingers = fingers;
            }

            if (Down)
            {
                if (now >= respawnAt) Respawn();
                Blocking = false;
                Gravity(dt);
                MoveBody(velocity * dt + knock * dt);
                knock *= Mathf.Exp(-2f * dt);
                return;
            }

            // Room scale: really stepping in your room moves your fighter (blocked by walls like the stick).
            FollowHead();

            bool stunned = now < stunUntil;
            Blocking = command.block && !stunned && !GuardBroken && now > dashUntil;
            if (!Blocking) guard = Mathf.Min(100f, guard + 25f * dt);

            // Walking (slower while blocking, none while stunned).
            Vector3 wish = stunned ? Vector3.zero : command.move * s.moveSpeed * (Blocking ? 0.45f : 1f);
            float accel = grounded ? 22f : 7f;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            flat = Vector3.MoveTowards(flat, wish, accel * dt * Mathf.Max(1f, s.moveSpeed));
            velocity.x = flat.x; velocity.z = flat.z;

            // Dodge dash.
            if (command.dash && !stunned && now >= nextDash)
            {
                dashDir = command.move.sqrMagnitude > 0.04f ? command.move.normalized : -FacingFlat;
                dashUntil = now + s.dashTime;
                dodgeUntil = now + s.dashTime + 0.1f;
                nextDash = now + s.dashCooldown;
                AudioManager.PlayAt(AudioManager.Sfx.Whoosh, transform.position + Vector3.up);
                Fx.Afterimage(transform.position, HeadPos, s.main);
                if (!IsBot) ComfortVignette.Kick(0.8f);
            }
            Dodging = now < dodgeUntil;
            if (now < dashUntil) { velocity.x = dashDir.x * s.dashSpeed; velocity.z = dashDir.z * s.dashSpeed; }

            // Jump.
            if (command.jump && grounded && !stunned)
            {
                velocity.y = s.jumpSpeed;
                AudioManager.PlayAt(AudioManager.Sfx.Jump, transform.position);
            }

            // Special.
            if (command.special && !stunned && now >= nextSpecial) UseSpecial();
            if (Empowered && now > empoweredUntil) Empowered = false;

            Gravity(dt);
            MoveBody((velocity + knock) * dt);
            // Tumbling: you keep sliding and rolling along the ground for a while instead of stopping dead.
            bool tumbling = now < tumbleUntil;
            knock *= Mathf.Exp(-(grounded ? (tumbling ? 1.6f : 6f) : 1.2f) * dt);
            if (knock.sqrMagnitude < 0.01f) knock = Vector3.zero;

            // Landing.
            if (grounded && !wasGrounded)
            {
                // Tumbling bodies bounce when they hit the ground hard.
                if (tumbling && now < tumbleUntil - 0.3f && knock.magnitude > 4f)
                {
                    velocity.y = Mathf.Min(3.2f, knock.magnitude * 0.3f);
                    grounded = false;
                    knock *= 0.8f;
                    AudioManager.PlayAt(AudioManager.Sfx.Land, transform.position);
                }
                if (slamPending) { slamPending = false; GroundSlam(); }
                else if (fallStartY - transform.position.y > 2f) AudioManager.PlayAt(AudioManager.Sfx.Land, transform.position);
            }
            if (!grounded && wasGrounded) fallStartY = transform.position.y;
            wasGrounded = grounded;

            if (transform.position.y < KillY) KnockOut(Time.time - lastAttackedAt < 8f ? lastAttacker : null);

            DetectPunches(dt);
            command.jump = command.dash = command.special = false;
        }

        void Gravity(float dt)
        {
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            velocity.y += (slamPending && velocity.y < 0f ? -40f : -20f) * dt;
        }

        void MoveBody(Vector3 delta)
        {
            if (cc == null || !cc.enabled) { transform.position += delta; return; }
            Vector3 before = transform.position;
            var flags = cc.Move(delta);
            grounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            // The rig rides along, so locomotion moves your view too.
            _ = before;
        }

        Vector3 lastHeadLocal;
        bool haveHeadLocal;

        void FollowHead()
        {
            if (IsBot || poseHead == null || poseRoot == null || !VRRig.IsXR) return;
            if (!GameSettings.RoomScale) { VRRig.Instance?.KeepHeadCentered(); return; }
            // Move the body under the head; the rig moves back the same amount so your view doesn't jump.
            Vector3 offset = poseHead.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.0004f) return;
            Vector3 before = transform.position;
            MoveBody(offset);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            poseRoot.position -= moved;
        }

        // ================================================================== punching (owner)

        static readonly Collider[] Overlaps = new Collider[16];
        const float PunchSpeed = 2.4f;

        void DetectPunches(float dt)
        {
            if (poseLeft == null || poseRight == null) return;
            var space = poseLeft.parent != null ? poseLeft.parent : transform;
            Vector3 l = poseLeft.localPosition, r = poseRight.localPosition;
            if (!havePrevHands) { prevLeftLocal = l; prevRightLocal = r; havePrevHands = true; return; }
            Vector3 vl = space.TransformVector(l - prevLeftLocal) / dt, vr = space.TransformVector(r - prevRightLocal) / dt;
            prevLeftLocal = l; prevRightLocal = r;
            LeftHandSpeed = vl.magnitude;
            RightHandSpeed = vr.magnitude;
            if (vl.magnitude > 25f || vr.magnitude > 25f) return; // tracking jump, not a punch
            if (Time.time < stunUntil || HasStatus(Frozen)) return;
            TryPunch(true, poseLeft.position, vl, ref nextPunchLeft);
            TryPunch(false, poseRight.position, vr, ref nextPunchRight);
        }

        void TryPunch(bool left, Vector3 fist, Vector3 vel, ref float nextAllowed)
        {
            float speed = vel.magnitude;
            if (speed < PunchSpeed || Time.time < nextAllowed || Blocking) return;
            int n = Physics.OverlapSphereNonAlloc(fist, 0.13f, Overlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var hb = Overlaps[i].GetComponent<Hurtbox>();
                var target = hb != null ? hb.Target : null;
                if (target == null || ReferenceEquals(target, this) || !target.CanBeHitBy(this)) continue;
                Vector3 to = target.Center - fist;
                if (Vector3.Dot(vel, to) < 0f && to.magnitude > 0.25f) continue; // pulling back, not punching
                Punch(target, left, fist, vel, hb.damageScale);
                nextAllowed = Time.time + Stats.punchCooldown;
                return;
            }
        }

        void Punch(IHittable target, bool left, Vector3 fist, Vector3 vel, float zoneScale)
        {
            var s = Stats;
            float speed = vel.magnitude;
            Vector3 dir = vel.normalized;
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            flatDir = flatDir.sqrMagnitude > 0.01f ? flatDir.normalized : FacingFlat;

            bool heavy = speed > 5.5f;
            combo = ReferenceEquals(target, comboTarget) && Time.time - lastHitAt < 1f ? combo + 1 : 1;
            comboTarget = target;
            lastHitAt = Time.time;

            float damage = Mathf.Clamp(2f + speed * 1.5f, 5f, 14f) * s.power * zoneScale;
            if (heavy) damage *= 1.15f;
            damage *= 1f + 0.1f * (Mathf.Min(combo, 6) - 1);
            var flags = heavy ? HitFlags.Heavy : HitFlags.None;
            Vector3 kb = flatDir * (1.5f + speed * 0.5f) * s.power + Vector3.up * 0.6f;
            if (heavy) kb = flatDir * (4f + speed) * s.power + Vector3.up * 2.5f;
            bool launch = combo >= 4 || Empowered;
            if (Empowered)
            {
                damage *= 2f;
                flags |= HitFlags.Empowered;
                Empowered = false;
            }
            if (launch)
            {
                flags |= HitFlags.Launch;
                kb = flatDir * 11f * s.power + Vector3.up * 7f;
                combo = 0;
            }

            target.TakeHit(this, new HitData { damage = damage, knockback = kb, flags = flags, point = fist });

            // Instant feedback on your side (the hit is confirmed by their game a moment later).
            bool blockedGuess = target is Fighter f && f.Blocking;
            Fx.Impact(fist, dir, s.accent, heavy || launch, blockedGuess);
            AudioManager.PlayAt(blockedGuess ? AudioManager.Sfx.Block : heavy || launch ? AudioManager.Sfx.HeavyPunch : AudioManager.Sfx.Punch, fist);
            if (!IsBot) FightInput.Buzz(left, heavy || launch ? 0.9f : 0.5f, heavy ? 0.12f : 0.06f);
            if (combo >= 2) { Fx.Popup(fist + Vector3.up * 0.35f, combo + " HIT COMBO", s.accent, 0.8f); AudioManager.PlayAt(AudioManager.Sfx.Combo, fist, 0.6f, 1f + combo * 0.08f); }
            if (launch) Fx.Popup(fist + Vector3.up * 0.6f, "LAUNCH!", new Color(1f, 0.5f, 0.2f), 1.2f);
        }

        // ================================================================== being hit

        public bool CanBeHitBy(Fighter attacker) => IsReady && !Down && attacker != this;

        /// <summary>Called on the attacker's game: send the hit to whoever owns this fighter.</summary>
        public void TakeHit(Fighter attacker, HitData hit)
        {
            RPC_Hit(attacker, hit.damage, hit.knockback, (byte)hit.flags, hit.point);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Hit(Fighter attacker, float damage, Vector3 knockback, byte flagBits, Vector3 point, RpcInfo info = default)
        {
            if (Down || Time.time < invulnerableUntil) return;
            var flags = (HitFlags)flagBits;
            PlayerRef src = info.Source == PlayerRef.None && Runner != null ? Runner.LocalPlayer : info.Source;
            // Moderator power: nothing hurts you.
            if (!IsBot && ModPowers.InvincibleNow) { RPC_Fx((byte)FxKind.Immune, Center, 0f, PlayerRef.None); return; }
            // Barrier: nothing gets through, and the hit bounces back (not barrier hits, so two barriers can't ping-pong).
            if (Time.time < barrierUntil)
            {
                RPC_Fx((byte)FxKind.Block, point, 0f, PlayerRef.None);
                if (attacker != null && attacker != this && (flags & HitFlags.Special) == 0)
                    attacker.TakeHit(this, new HitData { damage = damage * 0.3f, knockback = Flat(attacker.transform.position - transform.position) * 7f + Vector3.up * 2f, flags = HitFlags.Special, point = attacker.Center });
                return;
            }
            // Sanity limits: a hacked client can't one-shot anyone.
            damage = Mathf.Clamp(damage, 0f, 45f);
            knockback = Vector3.ClampMagnitude(knockback, 22f);
            PlayerRef from = info.Source == PlayerRef.None && Runner != null ? Runner.LocalPlayer : info.Source;

            if (Dodging)
            {
                RPC_Fx((byte)FxKind.Miss, Center, 0f, from);
                return;
            }

            bool front = attacker == null || Vector3.Dot(FacingFlat, Flat(attacker.transform.position - transform.position)) > -0.2f;
            if (Blocking && front)
            {
                bool special = (flags & HitFlags.Special) != 0;
                guard -= damage * (special ? 2.2f : 1.6f);
                damage *= special ? 0.5f : 0.2f;
                knockback *= special ? 0.5f : 0.25f;
                flags |= HitFlags.Blocked;
                flags &= ~HitFlags.Launch;
                if (guard <= 0f)
                {
                    guard = 0f;
                    guardBrokenUntil = Time.time + 3f;
                    stunUntil = Time.time + 1.1f;
                    flags |= HitFlags.GuardBreak;
                    Blocking = false;
                }
            }

            // A super (special) that lands clean hits extra hard and sends you flying, rolling like a ragdoll.
            bool super = (flags & HitFlags.Special) != 0 && (flags & HitFlags.Blocked) == 0 && (flags & (HitFlags.Launch | HitFlags.Heavy)) != 0
                         && (flags & HitFlags.Freeze) == 0 && Time.time >= armorUntil;
            if (super)
            {
                damage = Mathf.Min(damage * SuperDamage, 50f);
                knockback *= SuperKnock;
                if (knockback.y >= 0f) knockback.y = Mathf.Max(knockback.y, 4.5f);
                tumbleUntil = Time.time + TumbleTime;
                stunUntil = Mathf.Max(stunUntil, tumbleUntil - 0.25f);
                Blocking = false;
            }
            if (Time.time < armorUntil) damage *= 0.5f;
            if ((flags & HitFlags.Freeze) != 0 && (flags & HitFlags.Blocked) == 0) { frozenUntil = Time.time + 1.1f; stunUntil = Mathf.Max(stunUntil, frozenUntil); }
            if ((flags & HitFlags.Burn) != 0 && (flags & HitFlags.Blocked) == 0) { burnUntil = Time.time + 3f; burnBy = attacker; }
            Health -= damage;
            if (attacker != null && attacker != this) { lastAttacker = attacker; lastAttackedAt = Time.time; }
            // Sideways push decays; the upward part becomes a real jump so launches arc and fall back down.
            Vector3 k = knockback * Stats.knockbackTaken;
            if (k.y > 0.5f) { velocity.y = Mathf.Max(velocity.y, k.y); grounded = false; }
            else if (k.y < -0.5f) velocity.y = Mathf.Min(velocity.y, k.y); // slammed down
            k.y = 0f;
            knock += k;
            if ((flags & HitFlags.Blocked) == 0)
                stunUntil = Mathf.Max(stunUntil, Time.time + ((flags & HitFlags.Launch) != 0 ? 0.7f : (flags & HitFlags.Heavy) != 0 ? 0.3f : 0.12f));
            HitTaken?.Invoke(attacker, (flags & HitFlags.Blocked) != 0);
            if (!IsBot)
            {
                FightInput.Buzz(true, 0.7f, 0.08f);
                FightInput.Buzz(false, 0.7f, 0.08f);
                ScreenFx.Hit((flags & HitFlags.Blocked) != 0);
                if ((flags & HitFlags.Launch) != 0 || super) ComfortVignette.Kick(super ? 1.4f : 1f);
            }

            RPC_Fx((byte)((flags & HitFlags.GuardBreak) != 0 ? FxKind.GuardBreak : (flags & HitFlags.Blocked) != 0 ? FxKind.Block : FxKind.Hit), point, damage, from);
            if (Health <= 0f) KnockOut(attacker);
        }

        public enum FxKind : byte { Hit, Block, GuardBreak, Miss, KO, Slam, Flash, Respawn, Immune }

        /// <summary>Everyone shows the result of a hit (except the attacker, who already did).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Fx(byte kind, Vector3 pos, float amount, PlayerRef skip)
        {
            var k = (FxKind)kind;
            var s = Stats;
            bool attackerHere = Runner != null && skip == Runner.LocalPlayer;
            switch (k)
            {
                case FxKind.Hit:
                    if (!attackerHere) { Fx.Impact(pos, Vector3.up, new Color(1f, 0.8f, 0.4f), amount > 12f, false); AudioManager.PlayAt(amount > 12f ? AudioManager.Sfx.HeavyPunch : AudioManager.Sfx.Punch, pos); }
                    Fx.Popup(pos + Vector3.up * 0.2f, Mathf.RoundToInt(amount).ToString(), amount > 12f ? new Color(1f, 0.45f, 0.2f) : Color.white, amount > 12f ? 1.3f : 1f);
                    break;
                case FxKind.Block:
                    if (!attackerHere) { Fx.Impact(pos, Vector3.up, Color.cyan, false, true); AudioManager.PlayAt(AudioManager.Sfx.Block, pos); }
                    Fx.Popup(pos + Vector3.up * 0.2f, "BLOCK", new Color(0.5f, 0.85f, 1f), 0.8f);
                    break;
                case FxKind.GuardBreak:
                    AudioManager.PlayAt(AudioManager.Sfx.GuardBreak, pos);
                    Fx.Popup(pos + Vector3.up * 0.3f, "GUARD BREAK!", new Color(1f, 0.3f, 0.3f), 1.3f);
                    break;
                case FxKind.Miss:
                    Fx.Popup(pos + Vector3.up * 0.3f, "DODGED", new Color(0.7f, 1f, 0.7f), 0.9f);
                    break;
                case FxKind.KO:
                    AudioManager.PlayAt(AudioManager.Sfx.KO, pos);
                    Fx.Shockwave(transform.position, 2.5f, s.main);
                    Fx.Popup(pos + Vector3.up * 0.6f, "K.O.!", new Color(1f, 0.25f, 0.2f), 2.2f);
                    break;
                case FxKind.Slam:
                    if (!attackerHere) SlamFx(pos);
                    break;
                case FxKind.Flash:
                    if (!attackerHere) { AudioManager.PlayAt(AudioManager.Sfx.Zap, pos); Fx.Afterimage(pos, pos + Vector3.up * 1.6f, s.main); }
                    break;
                case FxKind.Respawn:
                    AudioManager.PlayAt(AudioManager.Sfx.Respawn, pos);
                    break;
                case FxKind.Immune:
                    Fx.Popup(pos + Vector3.up * 0.3f, "INVINCIBLE", new Color(1f, 0.85f, 0.3f), 0.9f);
                    break;
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero; }

        // ================================================================== KO + respawn (owner)

        void KnockOut(Fighter by)
        {
            if (Down) return;
            Down = true;
            Blocking = false;
            Health = 0f;
            Downs++;
            Streak = 0;
            respawnAt = Time.time + 4f;
            RPC_Fx((byte)FxKind.KO, Center, 0f, PlayerRef.None);
            if (by != null && by != this) by.RPC_ScoreKO(IsBot);
            if (!IsBot) ScreenFx.Message("K.O.!", "Back in the fight in a moment...", 3.5f);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ScoreKO(NetworkBool victimWasBot)
        {
            KOs++;
            Streak++;
            if (!IsBot)
            {
                // Coins only for knocking out real people (not bots).
                if (!victimWasBot)
                {
                    Collection.Earn(Collection.CoinsPerKO);
                    Fx.Popup(HeadPos + transform.forward * 0.6f, "+" + Collection.CoinsPerKO + " coins", new Color(1f, 0.85f, 0.3f), 1f);
                }
                ScreenFx.Message(Streak >= 3 ? Streak + " KO STREAK!" : "K.O.!", Streak >= 3 ? "You're on fire" : "Nice hit", 2f);
                Health = Mathf.Min(Stats.maxHealth, Health + 15f); // a little health back for a KO
            }
        }

        void Respawn()
        {
            Down = false;
            Health = Stats.maxHealth;
            guard = 100f;
            knock = velocity = Vector3.zero;
            Empowered = false;
            MoveTo(SpawnPoint.Pick(transform.position));
            invulnerableUntil = Time.time + 1.5f;
            tumbleUntil = 0f;
            RPC_Fx((byte)FxKind.Respawn, transform.position, 0f, PlayerRef.None);
        }

        /// <summary>Change fighter (from the menu): you come back in as the new one at full health.</summary>
        public void SwitchKind(FighterKind kind)
        {
            if (!IsMine || kind == Kind) return;
            if (!Collection.IsUnlocked((int)kind)) { ScreenFx.Message("LOCKED", "Open crates in the main menu to unlock " + FighterInfo.Get(kind).name, 2.5f); return; }
            if (Time.time - lastAttackedAt < 6f && !Down)
            {
                // No healing by switching mid-fight.
                ScreenFx.Message("NOT NOW", "You can switch fighter when you're out of the fight for a few seconds", 2.5f);
                return;
            }
            Kind = kind;
            Health = Stats.maxHealth;
            nextSpecial = 0f;
            Empowered = false;
        }

        /// <summary>Owner: put the fighter somewhere (spawn, teleport), taking the VR rig along.</summary>
        public void MoveTo(Vector3 pos)
        {
            Vector3 delta = pos - transform.position;
            if (cc != null && cc.enabled) { cc.enabled = false; transform.position = pos; cc.enabled = true; }
            else transform.position = pos;
            if (poseRoot != null && poseRoot.parent != transform) poseRoot.position += delta;
            havePrevHands = false;
            NetBody = pos;
        }

        // ================================================================== specials (owner)

        /// <summary>Brawler: leap, then come down hard. The landing shockwave launches everyone near you.</summary>
        void StartSlam(float radius = 5.5f, float damage = 18f)
        {
            slamPending = true;
            slamRadius = radius;
            slamDamage = damage;
            if (grounded) velocity.y = 7.5f;
            else velocity.y = -18f;
            AudioManager.PlayAt(AudioManager.Sfx.Whoosh, transform.position, 1f, 0.7f);
        }

        void GroundSlam()
        {
            Vector3 c = transform.position;
            float radius = slamRadius;
            if (radius > 6f) QuakeFx(c, radius); else SlamFx(c);
            RPC_SpecialFx((byte)(radius > 6f ? SpecialType.TitanQuake : SpecialType.GroundSlam), c, Vector3.zero, Runner != null ? Runner.LocalPlayer : PlayerRef.None);
            AreaHit(c, radius, slamDamage, 10f, 8f, HitFlags.Special | HitFlags.Launch | HitFlags.Heavy);
            foreach (var b in Breakable.All) if (!b.Broken && Vector3.Distance(b.Center, c) < radius * 0.8f) b.Smash(b.Center - c);
            if (!IsBot) { FightInput.Buzz(true, 1f, 0.25f); FightInput.Buzz(false, 1f, 0.25f); ComfortVignette.Kick(0.6f); }
        }

        void SlamFx(Vector3 c)
        {
            AudioManager.PlayAt(AudioManager.Sfx.Slam, c);
            Fx.Shockwave(c, 5.5f, Stats.accent);
        }

        /// <summary>Speedster: blink behind the enemy you're looking at (or straight ahead) and charge your next punch.</summary>
        void FlashStep()
        {
            Vector3 from = transform.position;
            Fighter target = null;
            float best = 35f;
            foreach (var f in All)
            {
                if (f == this || !f.IsReady || f.Down) continue;
                Vector3 to = f.transform.position - transform.position;
                if (to.magnitude > 14f) continue;
                float angle = Vector3.Angle(HeadRot * Vector3.forward, f.Center - HeadPos);
                if (angle < best) { best = angle; target = f; }
            }
            Vector3 dest;
            if (target != null)
            {
                Vector3 behind = target.transform.position - target.FacingFlat * 1.3f;
                dest = Clear(behind) ? behind : target.transform.position + target.FacingFlat * 1.3f;
            }
            else
            {
                Vector3 fwd = FacingFlat;
                float dist = 7f;
                if (Physics.Raycast(transform.position + Vector3.up, fwd, out var hit, dist, ~0, QueryTriggerInteraction.Ignore)) dist = Mathf.Max(0f, hit.distance - 0.6f);
                dest = transform.position + fwd * dist;
            }
            MoveTo(dest + Vector3.up * 0.05f);
            if (target != null && poseRoot != null && poseHead != null)
            {
                // Face them.
                float yaw = Vector3.SignedAngle(FacingFlat, Flat(target.transform.position - dest), Vector3.up);
                poseRoot.RotateAround(poseHead.position, Vector3.up, yaw);
            }
            Empowered = true;
            empoweredUntil = Time.time + 1.8f;
            AudioManager.PlayAt(AudioManager.Sfx.Zap, from);
            Fx.Afterimage(from, from + Vector3.up * 1.6f, Stats.main);
            Fx.Streak(from + Vector3.up, dest + Vector3.up, Stats.accent);
            RPC_Fx((byte)FxKind.Flash, from, 0f, Runner != null ? Runner.LocalPlayer : PlayerRef.None);
            if (!IsBot) ScreenFx.Flash(new Color(0.6f, 0.9f, 1f));
        }

        bool Clear(Vector3 feet)
        {
            return !Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Everything hittable within a radius (one entry each).</summary>
        List<IHittable> Hittables(Vector3 c, float radius)
        {
            var result = new List<IHittable>();
            var cols = Physics.OverlapSphere(c, radius, ~0, QueryTriggerInteraction.Collide);
            foreach (var col in cols)
            {
                var hb = col.GetComponent<Hurtbox>();
                var t = hb != null ? hb.Target : null;
                if (t == null || ReferenceEquals(t, this) || !t.CanBeHitBy(this) || result.Contains(t)) continue;
                result.Add(t);
            }
            return result;
        }

        // ================================================================== crashing through things

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Launched into a wall or crate fast enough: it breaks and you fly through.
            if (!IsMine) return;
            float speed = (velocity + knock).magnitude;
            if (speed < 9f) return;
            var b = hit.collider.GetComponentInParent<Breakable>();
            if (b != null && !b.Broken) b.Smash(knock.sqrMagnitude > 0.1f ? knock : velocity);
        }
    }
}
