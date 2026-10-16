using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.XR;

namespace KartVR
{
    /// <summary>
    /// The player's kart. Each player owns (has state authority over) their own
    /// kart and simulates it locally with arcade physics. Its pose is sent over
    /// the network; everyone else's karts are kinematic and smoothly follow.
    ///
    /// In the Hub the kart is parked in its owner's garage (you walk around as
    /// your <see cref="PlayerAvatar"/>). In Race / Bumper / Free Roam you drive it.
    ///
    /// Bumping: when your kart rams someone, you send an RPC to that kart's
    /// owner, who applies the knock to their own kart.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : NetworkBehaviour
    {
        public static readonly List<KartController> All = new List<KartController>();
        public static KartController Local { get; private set; }

        [System.Serializable]
        public struct Tuning
        {
            public float maxSpeed;     // m/s
            public float reverseSpeed;
            public float acceleration;
            public float braking;
            public float coastDecel;
            public float turnRate;     // degrees per second at full lock
            public float grip;         // how fast sideways sliding is killed
            public float driftGrip;
            public float bumpPower;
        }

        [Header("Handling per mode")]
        public Tuning raceTuning = new Tuning { maxSpeed = 24, reverseSpeed = 7, acceleration = 14, braking = 30, coastDecel = 5, turnRate = 95, grip = 10, driftGrip = 2.5f, bumpPower = 0.6f };
        public Tuning bumperTuning = new Tuning { maxSpeed = 12, reverseSpeed = 8, acceleration = 13, braking = 22, coastDecel = 4, turnRate = 130, grip = 4, driftGrip = 2, bumpPower = 1.7f };
        public Tuning freeRoamTuning = new Tuning { maxSpeed = 22, reverseSpeed = 7, acceleration = 13, braking = 28, coastDecel = 5, turnRate = 100, grip = 9, driftGrip = 2.5f, bumpPower = 0.8f };

        [Header("Parts (found by name if left empty)")]
        public Transform seatAnchor;
        public Transform headAnchor;
        public KartVisuals visuals;

        // ---- Networked state ----
        [Networked] public NetworkString<_16> Nickname { get; set; }
        /// <summary>The owner's Player ID and headset code (for the mod menu and bans).</summary>
        [Networked] public NetworkString<_16> PlayerId { get; set; }
        [Networked] public NetworkString<_16> DeviceHash { get; set; }

        string checkedName, shownName;
        /// <summary>The name to show for this player. A rude name (only possible with a hacked game) shows as "Player".</summary>
        public string DisplayName
        {
            get
            {
                string n = Nickname.ToString();
                if (n != checkedName)
                {
                    checkedName = n;
                    shownName = NameRules.Valid(n, out _) || n.StartsWith("Racer") || n.StartsWith("Player") ? n : "Player";
                }
                return shownName;
            }
        }
        [Networked] public int LookPacked { get; set; }
        [Networked] public int GarageIndex { get; set; }
        [Networked] public NetworkBool Boosting { get; set; }
        [Networked] Vector3 NetPosition { get; set; }
        [Networked] Quaternion NetRotation { get; set; }
        [Networked] Vector3 NetVelocity { get; set; }
        [Networked] float NetSteer { get; set; }
        [Networked] int TeleportCount { get; set; }

        // Race progress (written by the owner, read by everyone for standings).
        [Networked] public int Lap { get; set; }
        [Networked] public int NextCheckpoint { get; set; }
        [Networked] public NetworkBool Finished { get; set; }
        [Networked] public float FinishTime { get; set; }

        // Bumper score.
        [Networked] public int Score { get; set; }

        public bool IsReady => Object != null && Object.IsValid;
        public bool IsLocal { get; private set; }
        public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;
        public KartMode CurrentMode { get; private set; } = KartMode.Hub;
        public KartLook Look => KartLook.Unpack(LookPacked);
        public bool Parked => CurrentMode == KartMode.Hub;

        /// <summary>0..1 booster charge (local kart only).</summary>
        public float BoostCharge { get; private set; } = 1f;

        Rigidbody rb;
        bool placed;
        int seenRaceId = -1, seenRoundId = -1, seenTeleport;
        Checkpoint lastCheckpoint;
        readonly RaycastHit[] hits = new RaycastHit[8];
        readonly Dictionary<KartController, float> lastHitTime = new Dictionary<KartController, float>();

        // Input cached in Update for use in FixedUpdate.
        float gas, brake, steer;
        bool driftHeld;
        readonly KartSteeringWheel wheel = new KartSteeringWheel();

        /// <summary>The grabbable steering wheel (local kart only).</summary>
        public KartSteeringWheel Wheel => wheel;

        bool grounded;
        Vector3 groundNormal = Vector3.up;
        bool drifting;
        float driftStart;
        float boostUntil, boostSpeedMul = 1.35f, stunUntil, upsideDownSince = -1f;
        float wheelSpin;
        float nextGarageCheck;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (seatAnchor == null) seatAnchor = transform.Find("Seat");
            if (headAnchor == null) headAnchor = transform.Find("HeadAnchor");
            if (visuals == null) visuals = GetComponent<KartVisuals>();
            if (visuals == null) visuals = gameObject.AddComponent<KartVisuals>();
            if (GetComponent<KartEngineSound>() == null) gameObject.AddComponent<KartEngineSound>();

            var mat = new PhysicsMaterial("KartBody")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0.3f,
                bounceCombine = PhysicsMaterialCombine.Minimum, // bounce off other karts, not off ramps and walls
            };
            foreach (var c in GetComponents<Collider>()) c.sharedMaterial = mat;
        }

        public override void Spawned()
        {
            All.Add(this);
            IsLocal = HasStateAuthority;

            if (IsLocal)
            {
                Local = this;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.centerOfMass = new Vector3(0f, 0.25f, 0f);
                Nickname = PlayerProfile.Nickname;
                PlayerId = PlayerIdentity.PlayerId;
                DeviceHash = PlayerIdentity.DeviceHash;
                LookPacked = PlayerProfile.Look.Pack();
                GarageIndex = -1;
                NetPosition = transform.position;
                NetRotation = transform.rotation;
            }
            else
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                seenTeleport = TeleportCount;
            }
            visuals.Apply(Look);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            if (Local == this)
            {
                Local = null;
                if (KartRig.Instance != null) KartRig.Instance.Detach();
            }
        }

        Tuning CurrentTuning
        {
            get
            {
                if (KartModeInfo.IsRace(CurrentMode)) return raceTuning;
                if (KartModeInfo.IsBumper(CurrentMode)) return bumperTuning;
                return freeRoamTuning;
            }
        }

        /// <summary>True while the kart isn't allowed to drive (parked, race countdown, results screens).</summary>
        public bool Frozen
        {
            get
            {
                if (Parked) return true;
                if (IsLocal && BuildMode.Active) return true; // flying around in build mode
                if (KartModeInfo.IsRace(CurrentMode) && RaceManager.Instance != null && RaceManager.Instance.IsReady)
                    return RaceManager.Instance.State == RaceState.Countdown;
                if (KartModeInfo.IsBumper(CurrentMode) && BumperManager.Instance != null && BumperManager.Instance.IsReady)
                    return BumperManager.Instance.State == BumperState.Results;
                return false;
            }
        }

        /// <summary>Change your kart's look (called by your garage editor). Saved on this device.</summary>
        public void SetLook(KartLook look)
        {
            PlayerProfile.Look = look;
            if (IsLocal) LookPacked = look.Pack();
        }

        // ---------------- Local (owner) logic ----------------

        void Update()
        {
            if (!IsReady || !IsLocal) return;

            gas = KartInput.Gas;
            brake = KartInput.Brake;
            // Steering comes from the wheel: grab it in VR (grip) and turn it, or it follows the stick smoothly.
            var rig = KartRig.Instance;
            bool seated = KartRig.IsXR && rig != null && rig.IsAttachedTo(this) && !Parked && GameSettings.GrabWheel;
            wheel.Tick(visuals.SteeringWheel, seated ? rig.leftHand : null, seated ? rig.rightHand : null,
                KartInput.LeftGripHeld, KartInput.RightGripHeld, Parked ? 0f : KartInput.Steer, seated, Time.deltaTime);
            steer = wheel.Steer;
            driftHeld = KartInput.DriftHeld;

            ClaimGarage();
            TrackModeChanges();

            if (Parked)
            {
                Boosting = false;
                return;
            }

            if (KartInput.RespawnPressed && !BuildMode.Active) Respawn(); // in build mode B means undo
            UpdateBooster();

            var area = SpawnArea.Get(CurrentMode);
            if (area != null && transform.position.y < area.killY)
            {
                if (KartModeInfo.IsBumper(CurrentMode) && BumperManager.Instance != null &&
                    BumperManager.Instance.State == BumperState.Playing)
                    Score -= 1;
                Respawn();
            }

            // The invisible barrier at the edge of the map's sky dome.
            if (area != null && KartMaps.OutOfBounds(CurrentMode, transform.position))
            {
                if (KartModeInfo.IsBumper(CurrentMode) && BumperManager.Instance != null &&
                    BumperManager.Instance.State == BumperState.Playing)
                    Score -= 1;
                if (SessionManager.Instance != null) SessionManager.Instance.SetStatus("Out of bounds! Back you go.", 2f);
                AudioManager.Play(AudioManager.Sfx.Error);
                Respawn();
            }

            // Auto-flip if stuck upside down.
            if (Vector3.Dot(transform.up, Vector3.up) < 0.3f)
            {
                if (upsideDownSince < 0f) upsideDownSince = Time.time;
                else if (Time.time - upsideDownSince > 2f) Respawn();
            }
            else upsideDownSince = -1f;
        }

        void UpdateBooster()
        {
            var look = Look;
            var stats = KartParts.Stats(look.booster);
            bool active = Time.time < boostUntil;
            if (look.booster != KartBooster.None)
            {
                if (!active) BoostCharge = Mathf.Min(1f, BoostCharge + Time.deltaTime / stats.recharge);
                if (KartInput.BoostHeld && BoostCharge >= 1f && !Frozen)
                {
                    BoostCharge = 0f;
                    boostUntil = Time.time + stats.seconds;
                    boostSpeedMul = stats.speedMul;
                    AudioManager.Play(AudioManager.Sfx.Boost);
                    rb.AddForce(transform.forward * 4f, ForceMode.VelocityChange);
                    Buzz(XRNode.RightHand, 0.6f, 0.2f);
                }
            }
            Boosting = Time.time < boostUntil;
        }

        /// <summary>Each player takes the lowest free garage bay. If two pick the same one, the lower player id keeps it.</summary>
        void ClaimGarage()
        {
            if (Time.time < nextGarageCheck || Garage.Count == 0) return;
            nextGarageCheck = Time.time + 0.5f;
            int me = Object.StateAuthority.PlayerId;
            bool conflict = GarageIndex < 0 || GarageIndex >= Garage.Count || All.Any(k =>
                k != this && k.IsReady && k.GarageIndex == GarageIndex && k.Object.StateAuthority.PlayerId < me);
            if (!conflict) return;
            for (int i = 0; i < Garage.Count; i++)
            {
                if (!All.Any(k => k != this && k.IsReady && k.GarageIndex == i))
                {
                    GarageIndex = i;
                    if (Parked) ParkInGarage();
                    return;
                }
            }
        }

        void TrackModeChanges()
        {
            var gs = GameState.Instance;
            if (gs == null || !gs.IsReady) return;

            if (!placed || gs.Mode != CurrentMode)
            {
                placed = true;
                CurrentMode = gs.Mode;
                ResetProgress();
                if (Parked) ParkInGarage();
                else
                {
                    rb.isKinematic = false;
                    TeleportToSlot();
                }
                if (RaceManager.Instance != null && RaceManager.Instance.IsReady) seenRaceId = RaceManager.Instance.RaceId;
                if (BumperManager.Instance != null && BumperManager.Instance.IsReady) seenRoundId = BumperManager.Instance.RoundId;
            }

            if (KartModeInfo.IsRace(CurrentMode) && RaceManager.Instance != null && RaceManager.Instance.IsReady &&
                RaceManager.Instance.RaceId != seenRaceId)
            {
                // A new race started: line up on the grid.
                seenRaceId = RaceManager.Instance.RaceId;
                ResetProgress();
                TeleportToSlot();
            }

            if (KartModeInfo.IsBumper(CurrentMode) && BumperManager.Instance != null && BumperManager.Instance.IsReady &&
                BumperManager.Instance.RoundId != seenRoundId)
            {
                seenRoundId = BumperManager.Instance.RoundId;
                ResetProgress();
                TeleportToSlot();
            }
        }

        void ParkInGarage()
        {
            rb.isKinematic = true;
            var garage = Garage.Get(GarageIndex);
            if (garage != null)
            {
                Teleport(garage.ParkPosition, garage.ParkRotation);
            }
            else
            {
                // No bay free (more players than garages): park out of sight.
                Teleport(new Vector3(0f, -50f, 0f), Quaternion.identity);
            }
        }

        void ResetProgress()
        {
            Lap = 0;
            NextCheckpoint = 0;
            Finished = false;
            FinishTime = 0f;
            Score = 0;
            lastCheckpoint = null;
            BoostCharge = 1f;
            boostUntil = 0f;
        }

        public int SlotIndex()
        {
            // Everyone sorts the player list the same way, so slots don't collide.
            var players = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            int i = players.IndexOf(Object.StateAuthority);
            return i < 0 ? 0 : i;
        }

        void TeleportToSlot()
        {
            var area = SpawnArea.Get(CurrentMode);
            if (area == null) return;
            var pose = area.GetSlot(SlotIndex());
            Teleport(pose.position + Vector3.up * 0.3f, pose.rotation);
        }

        public void Respawn()
        {
            if (!IsLocal || Parked) return;
            if (KartModeInfo.IsRace(CurrentMode) && lastCheckpoint != null)
            {
                var t = lastCheckpoint.transform;
                Teleport(t.position - t.forward * 3f + Vector3.up * 0.5f,
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(t.forward, Vector3.up), Vector3.up));
            }
            else
            {
                TeleportToSlot();
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!IsLocal) return;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            NetPosition = position;
            NetRotation = rotation;
            NetVelocity = Vector3.zero;
            TeleportCount++;
            drifting = false;
        }

        void FixedUpdate()
        {
            if (!IsReady || !IsLocal || rb.isKinematic) return;
            var t = CurrentTuning;
            float dt = Time.fixedDeltaTime;

            UpdateGrounded();

            bool frozen = Frozen;
            bool stunned = Time.time < stunUntil;
            float control = stunned ? 0.3f : 1f;
            float throttle = frozen || Finished ? 0f : (gas - brake) * control;
            float steerIn = frozen ? 0f : steer;

            if (frozen)
            {
                // Hold still on the grid.
                rb.linearVelocity = new Vector3(0f, Mathf.Min(rb.linearVelocity.y, 0f), 0f);
                rb.angularVelocity = Vector3.zero;
                Stabilize(dt);
                return;
            }

            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            float forwardSpeed = localVel.z;
            bool boosting = Time.time < boostUntil;
            float maxSpeed = t.maxSpeed * (boosting ? boostSpeedMul : 1f);

            if (grounded)
            {
                // --- Forward / reverse ---
                float target = throttle >= 0f ? throttle * maxSpeed : throttle * t.reverseSpeed;
                if (boosting) target = Mathf.Max(target, maxSpeed);
                float rate;
                if (Mathf.Abs(throttle) < 0.05f && !boosting) rate = t.coastDecel;
                else if (Mathf.Abs(forwardSpeed) > 0.5f && Mathf.Sign(target) != Mathf.Sign(forwardSpeed)) rate = t.braking;
                else rate = t.acceleration * (boosting ? 2f : 1f);
                localVel.z = Mathf.MoveTowards(forwardSpeed, target, rate * dt);

                // --- Drifting ---
                bool wantDrift = driftHeld && Mathf.Abs(steerIn) > 0.3f && forwardSpeed > 7f;
                if (wantDrift && !drifting) { drifting = true; driftStart = Time.time; }
                else if (!wantDrift && drifting)
                {
                    drifting = false;
                    // Mini-turbo for a long drift.
                    if (Time.time - driftStart > 1.0f) { boostUntil = Time.time + 0.8f; boostSpeedMul = 1.3f; }
                }

                // --- Grip: kill sideways sliding ---
                float grip = stunned ? 1f : drifting ? t.driftGrip : t.grip;
                if (CurrentMode == KartMode.IceBumper) grip *= 0.3f; // slippery!
                localVel.x = Mathf.Lerp(localVel.x, 0f, 1f - Mathf.Exp(-grip * dt));

                rb.linearVelocity = transform.TransformDirection(localVel);

                // --- Steering (yaw around the kart's up axis) ---
                float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
                float highSpeedDamp = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / t.maxSpeed));
                float direction = forwardSpeed >= -0.1f ? 1f : -1f;
                float yawRate = steerIn * t.turnRate * speedFactor * highSpeedDamp * direction * (drifting ? 1.35f : 1f);
                Vector3 up = transform.up;
                Vector3 av = rb.angularVelocity;
                av += up * (yawRate * Mathf.Deg2Rad - Vector3.Dot(av, up));
                rb.angularVelocity = av;

                // Keep it planted.
                rb.AddForce(-groundNormal * 12f, ForceMode.Acceleration);
            }
            else
            {
                // A little air steering so jumps don't feel dead.
                Vector3 av = rb.angularVelocity;
                av.y = Mathf.Lerp(av.y, steerIn * t.turnRate * 0.3f * Mathf.Deg2Rad, 0.1f);
                rb.angularVelocity = av;
                if (boosting) rb.AddForce(transform.forward * 6f, ForceMode.Acceleration);
            }

            Stabilize(dt);
        }

        void UpdateGrounded()
        {
            grounded = false;
            groundNormal = Vector3.up;
            Vector3 origin = transform.position + transform.up * 0.6f;
            int count = Physics.RaycastNonAlloc(origin, -transform.up, hits, 1.0f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].rigidbody == rb) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    grounded = true;
                    groundNormal = hits[i].normal;
                }
            }

            // A second probe under the nose: as soon as the front reaches a ramp, start tilting up to meet it.
            Vector3 nose = transform.position + transform.up * 0.6f + transform.forward * 0.9f;
            count = Physics.RaycastNonAlloc(nose, -transform.up, hits, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            best = float.MaxValue;
            Vector3 noseNormal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].rigidbody == rb || hits[i].distance >= best) continue;
                best = hits[i].distance;
                noseNormal = hits[i].normal;
            }
            if (noseNormal != Vector3.zero)
            {
                groundNormal = grounded ? (groundNormal + noseNormal).normalized : noseNormal;
                grounded = true;
            }
        }

        /// <summary>Gently rotate the kart to match the ground (or level out in the air). Rolling around is bad for VR comfort.</summary>
        void Stabilize(float dt)
        {
            Vector3 targetUp = grounded ? groundNormal : Vector3.up;
            Vector3 axis = Vector3.Cross(transform.up, targetUp);
            rb.AddTorque(axis * 60f, ForceMode.Acceleration);
            Vector3 av = rb.angularVelocity;
            Vector3 nonYaw = av - transform.up * Vector3.Dot(av, transform.up);
            rb.AddTorque(-nonYaw * 10f, ForceMode.Acceleration);
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsLocal) return;
            NetPosition = rb.position;
            NetRotation = rb.rotation;
            NetVelocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity;
            NetSteer = steer;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsReady || !IsLocal || Parked) return;

            if (other.TryGetComponent(out Checkpoint cp)) PassCheckpoint(cp);

            if (other.TryGetComponent(out BoostPad pad))
            {
                boostUntil = Time.time + pad.boostSeconds;
                boostSpeedMul = 1.35f;
                rb.AddForce(transform.forward * pad.kick, ForceMode.VelocityChange);
                AudioManager.Play(AudioManager.Sfx.Boost, 0.7f, 1.15f);
                Buzz(XRNode.RightHand, 0.5f, 0.15f);
                Buzz(XRNode.LeftHand, 0.5f, 0.15f);
            }

            if (other.TryGetComponent(out ModePortal portal)) portal.Activate();
        }

        void PassCheckpoint(Checkpoint cp)
        {
            var race = RaceManager.Instance;
            if (!KartModeInfo.IsRace(CurrentMode) || cp.map != CurrentMode || race == null || !race.IsReady) return;
            if (race.State != RaceState.Racing || Finished || race.CheckpointCount < 2) return;
            if (cp.index != NextCheckpoint) return; // wrong order (or going backwards)

            lastCheckpoint = cp;
            if (cp.index != 0) AudioManager.Play(AudioManager.Sfx.Checkpoint);
            if (cp.index == 0)
            {
                if (Lap == 0) Lap = 1; // crossing the start line begins lap 1
                else if (Lap < race.totalLaps) AudioManager.Play(AudioManager.Sfx.Lap);
                else if (Lap >= race.totalLaps)
                {
                    Finished = true;
                    FinishTime = race.Elapsed;
                    AudioManager.Play(AudioManager.Sfx.Finish);
                    Buzz(XRNode.RightHand, 0.8f, 0.4f);
                    Buzz(XRNode.LeftHand, 0.8f, 0.4f);
                }
                else Lap++;
            }
            NextCheckpoint = (cp.index + 1) % race.CheckpointCount;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsReady || !IsLocal || rb.isKinematic) return;

            // Bouncy bumpers in the arena.
            var bouncer = collision.collider.GetComponentInParent<Bouncer>();
            if (bouncer != null)
            {
                Vector3 away = transform.position - collision.collider.transform.position;
                away.y = 0f;
                rb.AddForce(away.normalized * bouncer.power + Vector3.up * bouncer.lift, ForceMode.VelocityChange);
                AudioManager.Play(AudioManager.Sfx.Bump, 1f, 1.3f);
                stunUntil = Time.time + 0.4f;
                Buzz(XRNode.RightHand, 0.8f, 0.15f);
                Buzz(XRNode.LeftHand, 0.8f, 0.15f);
                return;
            }

            if (collision.rigidbody == null) return;
            var other = collision.rigidbody.GetComponent<KartController>();
            if (other == null || other == this || !other.IsReady) return;

            Vector3 toOther = other.transform.position - transform.position;
            toOther.y = 0f;
            if (toOther.sqrMagnitude < 0.001f) return;
            toOther.Normalize();

            // How hard am *I* driving into them? (Their own speed is handled on their side.)
            float closing = Vector3.Dot(rb.linearVelocity, toOther);
            if (closing < 2.5f) return;

            var t = CurrentTuning;
            Vector3 knock = toOther * closing * t.bumpPower + Vector3.up * (KartModeInfo.IsBumper(CurrentMode) ? 2f : 0.5f);
            AudioManager.PlayAt(AudioManager.Sfx.Bump, (transform.position + other.transform.position) * 0.5f);
            other.RPC_ReceiveBump(knock);
            Buzz(XRNode.RightHand, 0.7f, 0.12f);
            Buzz(XRNode.LeftHand, 0.7f, 0.12f);

            if (KartModeInfo.IsBumper(CurrentMode) && BumperManager.Instance != null &&
                BumperManager.Instance.State == BumperState.Playing)
            {
                lastHitTime.TryGetValue(other, out float last);
                if (Time.time - last > 0.75f)
                {
                    lastHitTime[other] = Time.time;
                    Score += 1;
                }
            }
        }

        /// <summary>Runs on the owner of the kart that got hit.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReceiveBump(Vector3 velocityChange)
        {
            velocityChange = Vector3.ClampMagnitude(velocityChange, 20f); // anti-cheat: no launching people into space
            AudioManager.PlayAt(AudioManager.Sfx.Bump, transform.position);
            if (rb == null || rb.isKinematic) return;
            rb.AddForce(velocityChange, ForceMode.VelocityChange);
            stunUntil = Time.time + 0.5f;
            drifting = false;
            Buzz(XRNode.RightHand, 1f, 0.25f);
            Buzz(XRNode.LeftHand, 1f, 0.25f);
        }

        /// <summary>A moderator's signed command for this kart's owner (checked in <see cref="Moderation.Receive"/>).</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Moderate(byte action, int arg, long time, byte[] modKey, byte[] signature)
        {
            Moderation.Receive(action, arg, time, modKey, signature);
        }

        static void Buzz(XRNode node, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0, amplitude, duration);
        }

        // ---------------- Remote (proxy) smoothing + visuals ----------------

        public override void Render()
        {
            if (!IsLocal)
            {
                if (TeleportCount != seenTeleport)
                {
                    seenTeleport = TeleportCount;
                    transform.SetPositionAndRotation(NetPosition, NetRotation);
                }
                else
                {
                    // Follow the latest received pose, nudged forward by velocity to hide lag.
                    Vector3 target = NetPosition + NetVelocity * 0.05f;
                    if ((transform.position - target).sqrMagnitude > 64f)
                        transform.SetPositionAndRotation(NetPosition, NetRotation);
                    else
                    {
                        float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
                        transform.position = Vector3.Lerp(transform.position, target, k);
                        transform.rotation = Quaternion.Slerp(transform.rotation, NetRotation, k);
                    }
                }
            }

            visuals.Apply(Look);
            visuals.SetBoosting(Boosting);

            float steerVisual = IsLocal ? steer : NetSteer;
            if (Parked) steerVisual = 0f;
            float forward = Vector3.Dot(IsLocal && !rb.isKinematic ? rb.linearVelocity : NetVelocity, transform.forward);
            wheelSpin += forward / 0.23f * Mathf.Rad2Deg * Time.deltaTime;
            foreach (var w in visuals.AllWheels)
                if (w) w.GetChild(0).localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);
            foreach (var w in visuals.FrontWheels)
                if (w) w.localRotation = Quaternion.Euler(0f, steerVisual * 25f, 0f);
            if (visuals.SteeringWheel)
            {
                // Turn the wheel model by the same angle your hands turned it (+ = clockwise as the driver sees it:
                // the top of the wheel moves to the right).
                float wheelAngle = IsLocal ? wheel.Angle : NetSteer * KartSteeringWheel.FullSteerAngle;
                visuals.SteeringWheel.localRotation = Quaternion.AngleAxis(wheelAngle, KartSteeringWheel.Tilt * Vector3.up);
            }
        }
    }
}
