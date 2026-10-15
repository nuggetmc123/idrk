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
        public Tuning hubTuning = new Tuning { maxSpeed = 11, reverseSpeed = 5, acceleration = 10, braking = 25, coastDecel = 4, turnRate = 110, grip = 9, driftGrip = 2.5f, bumpPower = 0.6f };
        public Tuning raceTuning = new Tuning { maxSpeed = 24, reverseSpeed = 7, acceleration = 14, braking = 30, coastDecel = 5, turnRate = 95, grip = 10, driftGrip = 2.5f, bumpPower = 0.6f };
        public Tuning bumperTuning = new Tuning { maxSpeed = 12, reverseSpeed = 8, acceleration = 13, braking = 22, coastDecel = 4, turnRate = 130, grip = 4, driftGrip = 2, bumpPower = 1.7f };
        public Tuning freeRoamTuning = new Tuning { maxSpeed = 22, reverseSpeed = 7, acceleration = 13, braking = 28, coastDecel = 5, turnRate = 100, grip = 9, driftGrip = 2.5f, bumpPower = 0.8f };

        [Header("Parts (found by name if left empty)")]
        public Transform seatAnchor;
        public Transform headAnchor;
        public Transform steeringWheel;
        public Transform[] frontWheels;
        public Transform[] allWheels;

        // ---- Networked state ----
        [Networked] public NetworkString<_16> Nickname { get; set; }
        [Networked] public int ColorIndex { get; set; }
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

        Rigidbody rb;
        bool placed;
        int seenRaceId = -1, seenRoundId = -1, seenTeleport;
        Checkpoint lastCheckpoint;
        readonly RaycastHit[] hits = new RaycastHit[8];
        readonly Dictionary<KartController, float> lastHitTime = new Dictionary<KartController, float>();

        // Input cached in Update for use in FixedUpdate.
        float gas, brake, steer;
        bool driftHeld;

        bool grounded;
        Vector3 groundNormal = Vector3.up;
        bool drifting;
        float driftStart;
        float boostUntil, stunUntil, upsideDownSince = -1f;
        float wheelSpin;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (seatAnchor == null) seatAnchor = transform.Find("Seat");
            if (headAnchor == null) headAnchor = transform.Find("HeadAnchor");
            if (steeringWheel == null) steeringWheel = transform.Find("SteeringWheel");
            if (frontWheels == null || frontWheels.Length == 0)
                frontWheels = new[] { transform.Find("WheelFL"), transform.Find("WheelFR") }.Where(t => t).ToArray();
            if (allWheels == null || allWheels.Length == 0)
                allWheels = new[] { "WheelFL", "WheelFR", "WheelBL", "WheelBR" }
                    .Select(n => transform.Find(n)).Where(t => t).ToArray();

            var mat = new PhysicsMaterial("KartBody")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0.3f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
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
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.centerOfMass = new Vector3(0f, 0.25f, 0f);
                Nickname = PlayerProfile.Nickname;
                ColorIndex = PlayerProfile.ColorIndex;
                NetPosition = transform.position;
                NetRotation = transform.rotation;
                if (KartRig.Instance != null) KartRig.Instance.AttachTo(this);
            }
            else
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                seenTeleport = TeleportCount;
            }
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
                switch (CurrentMode)
                {
                    case KartMode.Race: return raceTuning;
                    case KartMode.Bumper: return bumperTuning;
                    case KartMode.FreeRoam: return freeRoamTuning;
                    default: return hubTuning;
                }
            }
        }

        /// <summary>True while the kart isn't allowed to drive (race countdown, results screens).</summary>
        public bool Frozen
        {
            get
            {
                if (CurrentMode == KartMode.Race && RaceManager.Instance != null && RaceManager.Instance.IsReady)
                    return RaceManager.Instance.State == RaceState.Countdown;
                if (CurrentMode == KartMode.Bumper && BumperManager.Instance != null && BumperManager.Instance.IsReady)
                    return BumperManager.Instance.State == BumperState.Results;
                return false;
            }
        }

        // ---------------- Local (owner) logic ----------------

        void Update()
        {
            if (!IsReady || !IsLocal) return;

            gas = KartInput.Gas;
            brake = KartInput.Brake;
            steer = KartInput.Steer;
            driftHeld = KartInput.DriftHeld;
            if (KartInput.RespawnPressed) Respawn();

            TrackModeChanges();

            var area = SpawnArea.Get(CurrentMode);
            if (area != null && transform.position.y < area.killY)
            {
                if (CurrentMode == KartMode.Bumper && BumperManager.Instance != null &&
                    BumperManager.Instance.State == BumperState.Playing)
                    Score -= 1;
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

        void TrackModeChanges()
        {
            var gs = GameState.Instance;
            if (gs == null || !gs.IsReady) return;

            if (!placed || gs.Mode != CurrentMode)
            {
                placed = true;
                CurrentMode = gs.Mode;
                ResetProgress();
                TeleportToSlot();
                if (RaceManager.Instance != null && RaceManager.Instance.IsReady) seenRaceId = RaceManager.Instance.RaceId;
                if (BumperManager.Instance != null && BumperManager.Instance.IsReady) seenRoundId = BumperManager.Instance.RoundId;
            }

            if (CurrentMode == KartMode.Race && RaceManager.Instance != null && RaceManager.Instance.IsReady &&
                RaceManager.Instance.RaceId != seenRaceId)
            {
                // A new race started: line up on the grid.
                seenRaceId = RaceManager.Instance.RaceId;
                ResetProgress();
                TeleportToSlot();
            }

            if (CurrentMode == KartMode.Bumper && BumperManager.Instance != null && BumperManager.Instance.IsReady &&
                BumperManager.Instance.RoundId != seenRoundId)
            {
                seenRoundId = BumperManager.Instance.RoundId;
                ResetProgress();
                TeleportToSlot();
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
        }

        int SlotIndex()
        {
            // Everyone sorts the player list the same way, so slots don't collide.
            var players = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            int i = players.IndexOf(Object.StateAuthority);
            return i < 0 ? 0 : i;
        }

        void TeleportToSlot()
        {
            var area = SpawnArea.Get(CurrentMode) ?? SpawnArea.Get(KartMode.Hub);
            if (area == null) return;
            var pose = area.GetSlot(SlotIndex());
            Teleport(pose.position + Vector3.up * 0.3f, pose.rotation);
        }

        public void Respawn()
        {
            if (!IsLocal) return;
            if (CurrentMode == KartMode.Race && lastCheckpoint != null)
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            NetPosition = position;
            NetRotation = rotation;
            NetVelocity = Vector3.zero;
            TeleportCount++;
            drifting = false;
        }

        public void SetColor(int index)
        {
            PlayerProfile.ColorIndex = index;
            if (IsLocal) ColorIndex = PlayerProfile.ColorIndex;
        }

        void FixedUpdate()
        {
            if (!IsReady || !IsLocal) return;
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
            float maxSpeed = t.maxSpeed * (boosting ? 1.35f : 1f);

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
                    if (Time.time - driftStart > 1.0f) boostUntil = Time.time + 0.8f;
                }

                // --- Grip: kill sideways sliding ---
                float grip = stunned ? 1f : drifting ? t.driftGrip : t.grip;
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
            NetVelocity = rb.linearVelocity;
            NetSteer = steer;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsReady || !IsLocal) return;

            if (other.TryGetComponent(out Checkpoint cp)) PassCheckpoint(cp);

            if (other.TryGetComponent(out BoostPad pad))
            {
                boostUntil = Time.time + pad.boostSeconds;
                rb.AddForce(transform.forward * pad.kick, ForceMode.VelocityChange);
                Buzz(XRNode.RightHand, 0.5f, 0.15f);
                Buzz(XRNode.LeftHand, 0.5f, 0.15f);
            }

            if (other.TryGetComponent(out ModePortal portal)) portal.Activate(this);
        }

        void PassCheckpoint(Checkpoint cp)
        {
            var race = RaceManager.Instance;
            if (CurrentMode != KartMode.Race || race == null || !race.IsReady) return;
            if (race.State != RaceState.Racing || Finished || race.CheckpointCount < 2) return;
            if (cp.index != NextCheckpoint) return; // wrong order (or going backwards)

            lastCheckpoint = cp;
            if (cp.index == 0)
            {
                if (Lap == 0) Lap = 1; // crossing the start line begins lap 1
                else if (Lap >= race.totalLaps)
                {
                    Finished = true;
                    FinishTime = race.Elapsed;
                    Buzz(XRNode.RightHand, 0.8f, 0.4f);
                    Buzz(XRNode.LeftHand, 0.8f, 0.4f);
                }
                else Lap++;
            }
            NextCheckpoint = (cp.index + 1) % race.CheckpointCount;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsReady || !IsLocal || collision.rigidbody == null) return;
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
            Vector3 knock = toOther * closing * t.bumpPower + Vector3.up * (CurrentMode == KartMode.Bumper ? 2f : 0.5f);
            other.RPC_ReceiveBump(knock);
            Buzz(XRNode.RightHand, 0.7f, 0.12f);
            Buzz(XRNode.LeftHand, 0.7f, 0.12f);

            if (CurrentMode == KartMode.Bumper && BumperManager.Instance != null &&
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
            if (rb == null || rb.isKinematic) return;
            rb.AddForce(velocityChange, ForceMode.VelocityChange);
            stunUntil = Time.time + 0.5f;
            drifting = false;
            Buzz(XRNode.RightHand, 1f, 0.25f);
            Buzz(XRNode.LeftHand, 1f, 0.25f);
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

            float steerVisual = IsLocal ? steer : NetSteer;
            float forward = Vector3.Dot(IsLocal ? rb.linearVelocity : NetVelocity, transform.forward);
            wheelSpin += forward / 0.22f * Mathf.Rad2Deg * Time.deltaTime;
            foreach (var w in allWheels)
                if (w) w.localRotation = Quaternion.Euler(wheelSpin, 0f, 90f);
            foreach (var w in frontWheels)
                if (w) w.localRotation = Quaternion.Euler(0f, steerVisual * 25f, 0f) * Quaternion.Euler(wheelSpin, 0f, 90f);
            if (steeringWheel) steeringWheel.localRotation = Quaternion.Euler(-60f, 0f, 0f) * Quaternion.Euler(0f, steerVisual * -90f, 0f);
        }
    }
}
