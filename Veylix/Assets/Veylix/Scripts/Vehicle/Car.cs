using UnityEngine;

namespace Veylix
{
    /// <summary>What a driver (player or AI) asks of the car this step.</summary>
    public struct CarInput
    {
        public float steer;      // -1..1, fraction of the car's maximum road-wheel angle (left negative)
        public float throttle;   // 0..1
        public float brake;      // 0..1
        public float handbrake;  // 0..1
        public bool shiftUp, shiftDown;
    }

    /// <summary>
    /// The car's physics. A real rigid body (Unity PhysX) carried by four ray-cast suspension corners; every corner runs
    /// a combined-slip tyre model (slip ratio + slip angle on a friction ellipse) whose force is applied at the contact
    /// patch. Nothing is scripted: oversteer, lock-ups, wheelspin, slides, spins, curb launches and roll-overs all come
    /// out of the forces, the car's mass, its inertia and the low centre of gravity. Aero, drivetrain, brakes, tyre
    /// temperature/wear and damage feed into those forces.
    ///
    /// Local axes: +Z forward, +Y up, +X right. The car's origin sits on the ground, midway between the axles.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Car : MonoBehaviour
    {
        public const float Gravity = 9.81f, AirDensity = 1.225f;

        public CarSpec spec;
        public CarSetup setup = new CarSetup();
        public CarInput input;
        public bool isPlayer;
        public string driverName = "Driver";
        public DriverLook driverLook; // a human driver's own suit and helmet (player or online); null = team kit
        public int carIndex;

        [Header("Assists")]
        public bool abs, tractionControl, autoGears = true, autoClutch = true;

        public Rigidbody Body { get; private set; }
        public Wheel[] wheels = new Wheel[4]; // FL, FR, RL, RR
        public CarDamage damage;
        public CarParts parts;
        public bool Frozen { get; private set; }

        // ---------------- telemetry (read by the dash, audio, haptics, AI, HUD)
        public float SpeedMs { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float Rpm { get; private set; }
        public int Gear { get; private set; } = 1; // -1 R, 0 N, 1..n
        public float ThrottleOut { get; private set; }
        public float EngineLoad { get; private set; }
        public float Fuel { get; set; }
        public float EngineTemp { get; private set; } = 90f;
        public float WaterTemp => EngineTemp;
        public float Battery { get; private set; } = 1f;
        public bool Deploying { get; private set; }
        public float Downforce { get; private set; }
        public float LateralG { get; private set; }
        public float LongitudinalG { get; private set; }
        public float ShiftFlash { get; private set; }
        public bool Limiter { get; private set; }
        public bool Airborne { get; private set; }
        public float Upright => transform.up.y;
        public float WheelsOnGround { get; private set; }
        public float SlipstreamFactor { get; set; } = 1f;  // drag multiplier from the car ahead (race manager)
        public float DirtyAir { get; set; }                // 0..1 downforce lost following closely
        public float SteerAngleDeg { get; private set; }
        public float AligningTorque { get; private set; }  // drives wheel self-centring and force feedback
        public float KerbRumble { get; private set; }      // 0..1 for haptics/audio
        public float Scrape { get; private set; }          // floor/plank on the ground
        public float TyreSlide { get; private set; }       // 0..1 overall sliding
        public float WheelSpinAmount { get; private set; }
        public float LockupAmount { get; private set; }
        public float OffTrackAmount { get; private set; }
        public SurfaceType DominantSurface { get; private set; }
        public float Odometer { get; private set; }

        public event System.Action<int> OnShift;

        int shiftTarget;
        float shiftTimer, cutTimer, reverseHold, throttleSmooth, limiterPhase;
        Vector3 lastVelocity;
        float clutchRpm = 3000f;

        public float RedlineFraction => Mathf.InverseLerp(spec.idleRpm, spec.redline, Rpm);

        public class Wheel
        {
            public Vector3 anchor;   // local position of the suspension top (wheel centre at full droop + travel)
            public float radius, width, restLength, travel, sag;
            public bool front, left, driven;
            public float spring, damper, arb;
            public Transform visual, steerPivot;

            // state
            public float compression, lastCompression, omega, spinAngle, steer;
            public float load, slipRatio, slipAngle, slipAngleDyn, fx, fy;
            public bool grounded;
            public Vector3 point, normal;
            public SurfaceType surface;
            public float surfaceTemp = 70f, coreTemp = 70f, wear, brakeTemp = 300f;
            public float gripScale = 1f;     // damage
            public bool detached;
            public float bottomed;
            public float bumpPhase;
        }

        // ---------------------------------------------------------------- setup

        public void Init(CarSpec s, CarSetup st)
        {
            spec = s;
            setup = st ?? new CarSetup();
            Body = GetComponent<Rigidbody>();
            Body.mass = s.mass + 40f; // + fuel
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 30f;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.05f;
            Body.centerOfMass = new Vector3(0f, s.cogHeight, (s.weightFront - 0.5f) * s.wheelbase);
            float m = s.mass;
            Body.inertiaTensor = new Vector3(
                m * (s.length * s.length * 0.55f + 0.5f) / 12f,          // pitch
                m * (s.length * s.length * 0.6f + s.width * s.width) / 12f, // yaw
                m * (s.width * s.width * 0.5f + 0.35f) / 12f);           // roll
            Body.inertiaTensorRotation = Quaternion.identity;
            Fuel = s.fuelCapacity * 0.45f;

            float hb = s.wheelbase * 0.5f;
            float springScale = Mathf.Lerp(0.7f, 1.35f, setup.springs);
            float damperScale = Mathf.Lerp(0.75f, 1.3f, setup.dampers);
            float arbScale = Mathf.Lerp(0.5f, 1.6f, setup.antiRoll);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2, left = i % 2 == 0;
                float r = front ? s.wheelRadiusFront : s.wheelRadiusRear;
                float track = front ? s.trackFront : s.trackRear;
                var w = new Wheel
                {
                    front = front, left = left, driven = !front,
                    radius = r, width = front ? s.tireWidthFront : s.tireWidthRear,
                    travel = s.suspensionTravel,
                    spring = (front ? s.springFront : s.springRear) * springScale,
                    damper = (front ? s.damperFront : s.damperRear) * damperScale,
                    arb = (front ? s.arbFront : s.arbRear) * arbScale,
                };
                // Static sag: the spring carries the corner's share of the car's weight.
                float cornerWeight = s.mass * Gravity * (front ? s.weightFront : 1f - s.weightFront) * 0.5f;
                float sag = Mathf.Min(cornerWeight / w.spring, w.travel * 0.6f);
                w.restLength = w.travel;
                w.sag = sag;
                // Anchor: at rest the tyre touches the ground with 'sag' compression and the car's origin sits RideOffset
                // above the ground (the ride-height setting raises or lowers the whole car).
                w.anchor = new Vector3((left ? -1f : 1f) * track * 0.5f, r + w.restLength - sag - RideOffset, front ? hb : -hb);
                w.compression = w.lastCompression = sag;
                wheels[i] = w;
            }
            ResetState();
        }

        /// <summary>How much the ride-height setting lifts the car above its designed floor height (m).</summary>
        public float RideOffset => spec.rideHeight * Mathf.Lerp(-0.3f, 0.45f, setup.rideHeight) ;
        /// <summary>Floor height above the ground with the car at rest (m).</summary>
        public float StaticFloorHeight => spec.rideHeight + RideOffset;

        public void ResetState()
        {
            Gear = 1; shiftTarget = 1; shiftTimer = 0f;
            Rpm = spec.idleRpm; EngineTemp = 92f; Battery = 1f;
            foreach (var w in wheels)
            {
                if (w == null) continue;
                w.omega = 0f; w.slipAngleDyn = 0f; w.fx = w.fy = 0f;
                float opt = OptimalTemp(setup.compound);
                w.surfaceTemp = w.coreTemp = opt - 15f;
                w.brakeTemp = 350f;
            }
        }

        public void Freeze(bool frozen)
        {
            Frozen = frozen;
            if (Body == null) return;
            Body.isKinematic = frozen;
            if (frozen) { foreach (var w in wheels) if (w != null) w.omega = 0f; }
        }

        public void Teleport(Vector3 pos, Quaternion rot)
        {
            bool wasKinematic = Body.isKinematic;
            Body.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
            Body.position = pos;
            Body.rotation = rot;
            if (!wasKinematic)
            {
                Body.isKinematic = false;
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            foreach (var w in wheels) if (w != null) { w.omega = 0f; w.slipAngleDyn = 0f; }
            lastVelocity = Vector3.zero;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            damage = GetComponent<CarDamage>();
        }

        // ---------------------------------------------------------------- simulation

        static int groundMask = -1;
        public static int GroundMask
        {
            get
            {
                if (groundMask == -1) groundMask = (1 << Layers.Road) | (1 << Layers.Terrain) | (1 << Layers.Default);
                return groundMask;
            }
        }

        void FixedUpdate()
        {
            if (spec == null || Body == null || Body.isKinematic) { UpdateIdleTelemetry(); return; }
            float dt = Time.fixedDeltaTime;
            Vector3 v = Body.linearVelocity;
            SpeedMs = v.magnitude;
            ForwardSpeed = Vector3.Dot(v, transform.forward);
            Odometer += SpeedMs * dt;

            Vector3 acc = (v - lastVelocity) / dt;
            lastVelocity = v;
            LateralG = Mathf.Lerp(LateralG, Vector3.Dot(acc, transform.right) / Gravity, 0.15f);
            LongitudinalG = Mathf.Lerp(LongitudinalG, Vector3.Dot(acc, transform.forward) / Gravity, 0.15f);

            Body.mass = spec.mass + Fuel * 0.74f + 75f; // driver + fuel

            HandleShifting(dt);
            Steering(dt);
            Aero(v);
            Suspension(dt);
            Stability(dt);
            Drivetrain(dt);
            Temperatures(dt);
            FuelAndEnergy(dt);
            Summaries();
        }

        void UpdateIdleTelemetry()
        {
            SpeedMs = 0f; ForwardSpeed = 0f;
            float target = spec != null ? spec.idleRpm + (spec.redline - spec.idleRpm) * 0.65f * input.throttle : 0f;
            if (spec != null) Rpm = Mathf.MoveTowards(Rpm, target, (input.throttle > Rpm / spec.redline ? 25000f : 12000f) * Time.deltaTime);
            ThrottleOut = input.throttle;
        }

        // ---- gearbox

        void HandleShifting(float dt)
        {
            ShiftFlash = Mathf.Max(0f, ShiftFlash - dt * 4f);
            if (shiftTimer > 0f)
            {
                shiftTimer -= dt;
                if (shiftTimer <= 0f) { Gear = shiftTarget; }
            }
            bool engineDead = damage != null && damage.EngineDead;

            if (input.shiftUp) RequestShift(+1);
            if (input.shiftDown) RequestShift(-1);
            input.shiftUp = input.shiftDown = false;

            if (autoGears && shiftTimer <= 0f && !engineDead)
            {
                if (Gear >= 1)
                {
                    float upAt = spec.redline * 0.985f;
                    if (Rpm > upAt && Gear < spec.GearCount && WheelSpinAmount < 0.5f) RequestShift(+1);
                    else if (Gear > 1)
                    {
                        float lower = RpmForGear(Gear - 1);
                        if (lower < spec.redline * 0.9f && Rpm < Mathf.Lerp(spec.idleRpm, spec.redline, input.brake > 0.2f ? 0.72f : 0.55f)) RequestShift(-1);
                    }
                }
                // Reverse: hold the brake while stopped; back to first the same way.
                if (Mathf.Abs(ForwardSpeed) < 0.6f && input.brake > 0.5f && input.throttle < 0.05f)
                {
                    reverseHold += dt;
                    if (reverseHold > 0.7f) { reverseHold = 0f; Gear = shiftTarget = Gear == -1 ? 1 : -1; OnShift?.Invoke(Gear); }
                }
                else reverseHold = 0f;
            }
        }

        public void RequestShift(int dir)
        {
            if (shiftTimer > 0f) return;
            int target = Gear + dir;
            if (Gear == -1 && dir > 0) target = Mathf.Abs(ForwardSpeed) < 2f ? 1 : 0;
            if (target == 0 && dir < 0) target = Mathf.Abs(ForwardSpeed) < 2f ? -1 : 1;
            target = Mathf.Clamp(target, -1, spec.GearCount);
            if (target == Gear) return;
            // Money-shift protection: refuse a downshift that would over-rev the engine.
            if (dir < 0 && target >= 1 && RpmForGear(target) > spec.limiter * 1.03f) { ShiftFlash = 0.5f; return; }
            shiftTarget = target;
            shiftTimer = spec.shiftTime;
            cutTimer = spec.shiftTime + 0.02f;
            ShiftFlash = 1f;
            OnShift?.Invoke(target);
        }

        float Ratio(int gear)
        {
            if (gear == 0) return 0f;
            if (gear < 0) return -2.9f * spec.finalDrive;
            return spec.gears[Mathf.Clamp(gear - 1, 0, spec.GearCount - 1)] * spec.finalDrive;
        }

        float RearOmega() => (wheels[2].omega + wheels[3].omega) * 0.5f;
        float RpmForGear(int g) => Mathf.Abs(RearOmega() * Ratio(g)) * 60f / (2f * Mathf.PI);

        /// <summary>Engine torque at full throttle for this rpm (Nm).</summary>
        public float TorqueCurve(float rpm)
        {
            float x = Mathf.InverseLerp(spec.idleRpm, spec.peakTorqueRpm, rpm);
            float rise = Mathf.Lerp(spec.lowEndTorque, 1f, 1f - (1f - x) * (1f - x));
            float y = Mathf.InverseLerp(spec.peakTorqueRpm, spec.limiter, rpm);
            float fall = 1f - spec.torqueFalloff * y * y;
            return spec.peakTorque * (rpm < spec.peakTorqueRpm ? rise : fall);
        }

        // ---- steering

        void Steering(float dt)
        {
            float pull = damage != null ? damage.SteerPull : 0f;
            float maxDeg = spec.maxSteerAngle;
            // Less lock at speed (a small movement of the wheel is a big turn when you're quick), and the front wheels
            // can only swing so fast, slower at speed, so the car never snaps into a turn.
            float speedF = Mathf.Clamp01(SpeedMs / 60f);
            float want = Mathf.Clamp(input.steer, -1f, 1f) * maxDeg * Mathf.Lerp(1f, 0.6f, speedF) + pull;
            float rate = Mathf.Lerp(70f, 28f, speedF); // road-wheel degrees per second
            float target = Mathf.MoveTowards(SteerAngleDeg, want, rate * dt);
            SteerAngleDeg = target;
            // Ackermann: the inside wheel turns a little more.
            float a = Mathf.Abs(target) * Mathf.Deg2Rad;
            float inner = a, outer = a;
            if (a > 0.001f)
            {
                float R = spec.wheelbase / Mathf.Tan(a);
                inner = Mathf.Atan(spec.wheelbase / Mathf.Max(0.5f, R - spec.trackFront * 0.5f));
                outer = Mathf.Atan(spec.wheelbase / (R + spec.trackFront * 0.5f));
                inner = Mathf.Lerp(a, inner, 0.6f);
                outer = Mathf.Lerp(a, outer, 0.6f);
            }
            float sgn = Mathf.Sign(target);
            bool turningLeft = target < 0f;
            wheels[0].steer = sgn * (turningLeft ? inner : outer) * Mathf.Rad2Deg;
            wheels[1].steer = sgn * (turningLeft ? outer : inner) * Mathf.Rad2Deg;
            wheels[2].steer = wheels[3].steer = 0f;
            if (damage != null)
            {
                wheels[0].steer += damage.Toe(0);
                wheels[1].steer += damage.Toe(1);
                wheels[2].steer += damage.Toe(2);
                wheels[3].steer += damage.Toe(3);
            }
        }

        // ---- aero

        float rideHeightNow = 0.05f;

        void Aero(Vector3 v)
        {
            float vf = Vector3.Dot(v, transform.forward);
            float sp = v.magnitude;
            float q = 0.5f * AirDensity * sp * sp;
            // Downforce needs clean air flowing front to back. Sideways or backwards it collapses (and turns into lift
            // at big yaw angles, which is how a spinning car can get light and take off).
            float alignment = sp > 1f ? vf / sp : 0f;
            float clean = Mathf.Clamp01(alignment);
            clean *= clean;
            float wing = setup.wingLevel == 0 ? 0.82f : setup.wingLevel == 2 ? 1.18f : 1f;
            float wingDrag = setup.wingLevel == 0 ? 0.86f : setup.wingLevel == 2 ? 1.14f : 1f;
            // Ground effect: lower is better, until the floor stalls when the nose pitches up or the car rolls a lot.
            float ground = Mathf.Lerp(1.12f, 0.92f, Mathf.InverseLerp(0.01f, 0.09f, rideHeightNow));
            float attitude = Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up)) * 4f) * Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(transform.right, Vector3.up)) * 3f);
            float frontDmg = damage != null ? damage.FrontAero : 1f, rearDmg = damage != null ? damage.RearAero : 1f;
            float dirty = 1f - DirtyAir * 0.25f;

            float total = q * spec.downforceClA * wing * ground * clean * attitude * dirty;
            float front = total * spec.aeroBalance * frontDmg;
            float rear = total * (1f - spec.aeroBalance) * rearDmg;
            Downforce = front + rear;
            float hb = spec.wheelbase * 0.5f;
            Body.AddForceAtPosition(-transform.up * front, transform.TransformPoint(new Vector3(0f, 0.2f, hb)));
            Body.AddForceAtPosition(-transform.up * rear, transform.TransformPoint(new Vector3(0f, 0.2f, -hb)));

            // Lift when the car is going sideways fast enough.
            float yaw = Mathf.Abs(Vector3.Dot(v.normalized, transform.right));
            if (sp > 35f && yaw > 0.5f)
                Body.AddForce(transform.up * q * 0.6f * (yaw - 0.5f));

            // Drag at the centre of pressure (a little above the centre of mass, so braking from drag pitches the nose).
            float dragArea = spec.dragCdA * wingDrag * (damage != null ? damage.DragScale : 1f) * SlipstreamFactor;
            if (sp > 0.5f)
                Body.AddForceAtPosition(-v.normalized * q * dragArea, transform.TransformPoint(new Vector3(0f, 0.45f, 0f)));
        }

        // ---- suspension and tyres

        static readonly RaycastHit[] hits = new RaycastHit[6];

        void Suspension(float dt)
        {
            int grounded = 0;
            float compSum = 0f;
            float kerb = 0f, scrape = 0f, slide = 0f, spin = 0f, lockup = 0f, off = 0f;
            float align = 0f;
            int surfaceVotes = 0;
            SurfaceType dominant = SurfaceType.Asphalt;
            Vector3 up = transform.up;

            // Pass 1: ray-cast each corner.
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                w.lastCompression = w.compression;
                w.grounded = false;
                if (w.detached) { w.compression = 0f; continue; }
                Vector3 origin = transform.TransformPoint(w.anchor);
                float castRadius = Mathf.Min(0.12f, w.radius * 0.35f);
                float length = w.restLength + w.radius - castRadius;
                int n = Physics.SphereCastNonAlloc(origin + up * castRadius, castRadius, -up, hits, length + castRadius, GroundMask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                RaycastHit hit = default;
                for (int h = 0; h < n; h++)
                {
                    if (hits[h].collider.attachedRigidbody == Body) continue;
                    if (hits[h].distance < best && hits[h].distance > 0f) { best = hits[h].distance; hit = hits[h]; }
                }
                if (best < float.MaxValue)
                {
                    float dist = best - castRadius; // distance from anchor to the sphere's resting point
                    float x = length - dist;
                    w.compression = Mathf.Clamp(x, 0f, w.travel);
                    w.bottomed = x > w.travel ? Mathf.Clamp01((x - w.travel) / 0.03f) : 0f;
                    w.grounded = true;
                    w.point = hit.point;
                    w.normal = hit.normal;
                    w.surface = Surface.Of(hit.collider);
                    if (Vector3.Dot(w.normal, up) < 0.35f) w.grounded = false; // a wall, not the ground
                }
                else
                {
                    w.compression = 0f;
                    w.bottomed = 0f;
                }
            }

            // Pass 2: forces.
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                if (!w.grounded) { w.load = 0f; w.fx = w.fy = 0f; FreeSpin(w, dt); continue; }
                grounded++;
                var surf = Surface.Info(w.surface);

                // Kerb and surface texture: a tiny height ripple felt through the springs.
                float speed = SpeedMs;
                w.bumpPhase += speed * dt * surf.bumpFreq;
                float ripple = surf.bump * Mathf.Sin(w.bumpPhase * Mathf.PI * 2f) * Mathf.Clamp01(speed / 8f);
                float x = w.compression + ripple;

                float vel = (w.compression - w.lastCompression) / dt;
                var other = wheels[i ^ 1];
                float arbForce = (w.compression - other.compression) * w.arb;
                float damperRate = vel > 0f ? w.damper : w.damper * 1.35f; // rebound stiffer than bump
                float force = w.spring * x + damperRate * vel + arbForce;
                if (w.bottomed > 0f)
                {
                    // Bump rubber, then the plank: very stiff, and it scrapes.
                    force += w.bottomed * w.spring * 6f * 0.03f + Mathf.Max(0f, vel) * w.damper * 2f;
                    scrape = Mathf.Max(scrape, w.bottomed * Mathf.Clamp01(speed / 30f));
                }
                force = Mathf.Max(0f, force);
                w.load = force;
                Vector3 anchorWorld = transform.TransformPoint(w.anchor);
                Body.AddForceAtPosition(up * force, anchorWorld);
                compSum += w.compression - w.sag;

                // Tyre frame at the contact patch.
                Quaternion steerRot = Quaternion.AngleAxis(w.steer, up);
                Vector3 fwd = Vector3.ProjectOnPlane(steerRot * transform.forward, w.normal).normalized;
                Vector3 side = Vector3.Cross(w.normal, fwd);
                Vector3 contact = w.point;
                Vector3 pv = Body.GetPointVelocity(contact);
                float vx = Vector3.Dot(pv, fwd), vy = Vector3.Dot(pv, side);

                // Grip budget.
                float wet = Env.WetnessAt(contact);
                float surfGrip = Mathf.Lerp(surf.grip, surf.wetGrip, wet);
                float compoundGrip = CompoundGrip(setup.compound, wet);
                float tempGrip = TempGrip(w);
                float wearGrip = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(w.wear / 0.7f)) - (w.wear > 0.75f ? (w.wear - 0.75f) * 0.8f : 0f);
                float aquaplane = wet > 0.6f && setup.compound <= TireCompound.Hard ? Mathf.Clamp01((speed - 50f) / 40f) * (wet - 0.6f) * 1.2f : 0f;
                float loadSens = Mathf.Pow(Mathf.Max(1f, w.load) / 3000f, -spec.loadSensitivity);
                float mu = spec.gripMu * surfGrip * compoundGrip * tempGrip * wearGrip * w.gripScale * loadSens * (1f - aquaplane);
                float fMax = mu * w.load;

                // Wheel rotation: implicit step so the stiff tyre/wheel loop stays stable at any speed.
                float vRef = Mathf.Max(Mathf.Abs(vx), 4f);
                float driveT = w.driven ? wheelDriveTorque[i] : 0f;
                float brakeT = WheelBrakeTorque(i);
                float inertia = spec.wheelInertia + (w.driven && clutchLocked ? spec.engineInertia * EngineRatioSq() * 0.5f : 0f);
                float kx = spec.peakSlipRatio;
                float slipNow = (w.omega * w.radius - vx) / vRef;
                // dFx/domega: steep in the linear range (where an explicit step would blow up), nearly flat once sliding.
                float slope = fMax * 1.5f / kx * w.radius / vRef * (Mathf.Abs(slipNow) < kx ? 1f : 0.05f);
                float rolling = surf.rolling * w.load * w.radius * Mathf.Sign(w.omega);
                float fxNow = Mathf.Clamp(slipNow / kx, -1f, 1f) * fMax;
                float denom = inertia + dt * slope * w.radius;
                float omegaFree = w.omega + dt * (driveT - fxNow * w.radius - rolling) / denom;
                float brakeStep = brakeT * dt / denom;
                if (Mathf.Abs(omegaFree) <= brakeStep) omegaFree = 0f;
                else omegaFree -= Mathf.Sign(omegaFree) * brakeStep;
                w.omega = omegaFree;

                // Slips.
                float slipRatio = (w.omega * w.radius - vx) / vRef;
                w.slipRatio = slipRatio;
                float rawAngle = Mathf.Atan2(vy, Mathf.Max(Mathf.Abs(vx), 1.5f));
                // Relaxation length: the carcass needs some rolling distance to build up slip.
                float relax = Mathf.Clamp01((Mathf.Abs(vx) + 2f) * dt / 0.25f);
                w.slipAngleDyn = Mathf.Lerp(w.slipAngleDyn, rawAngle, relax);
                w.slipAngle = w.slipAngleDyn;

                float peakA = Mathf.Tan(spec.peakSlipAngle * Mathf.Deg2Rad);
                float sx = slipRatio / kx;
                float sy = Mathf.Tan(w.slipAngle) / peakA;
                float s = Mathf.Sqrt(sx * sx + sy * sy);
                float fx = 0f, fy = 0f;
                if (s > 1e-4f)
                {
                    float shape = s <= 1f ? 1.5f * s - 0.5f * s * s * s : Mathf.Lerp(1f, surf.slide, Smooth01((s - 1f) / 2.5f));
                    float f = fMax * shape;
                    fx = f * sx / s;
                    fy = -f * sy / s;
                }
                // Parking grip: at walking pace stop the car creeping and jittering.
                if (Mathf.Abs(vx) < 1.2f && Mathf.Abs(vy) < 1.2f)
                {
                    float cornerMass = Body.mass * 0.25f;
                    float lat = Mathf.Clamp(-vy * cornerMass / dt * 0.4f, -fMax, fMax);
                    fy = Mathf.Lerp(lat, fy, Mathf.Clamp01(Mathf.Abs(vy) / 1.2f));
                    if (brakeT > 50f || (Gear == 0 && input.throttle < 0.01f))
                    {
                        float lon = Mathf.Clamp(-vx * cornerMass / dt * 0.4f, -fMax, fMax);
                        fx = Mathf.Lerp(lon, fx, Mathf.Clamp01(Mathf.Abs(vx) / 1.2f));
                    }
                }
                w.fx = fx; w.fy = fy;
                Vector3 tyreForce = fwd * fx + side * fy;
                // Gravel and sand: the car digs in and slows down.
                if (surf.drag > 0f) tyreForce -= pv.normalized * Mathf.Min(surf.drag * w.load, Mathf.Abs(vx) * 900f);
                Body.AddForceAtPosition(tyreForce, contact);

                // Self-aligning torque: pneumatic trail, falls away past the peak slip angle.
                if (w.front)
                {
                    float trail = 0.045f * Mathf.Clamp01(1f - Mathf.Abs(sy) * 0.6f);
                    align += -fy * trail;
                }

                float slideAmt = Mathf.Clamp01(s - 0.9f);
                slide += slideAmt;
                if (w.driven && slipRatio > kx * 1.6f) spin += Mathf.Clamp01((slipRatio - kx * 1.6f) * 3f);
                if (slipRatio < -kx * 2f) lockup += Mathf.Clamp01((-slipRatio - kx * 2f) * 3f);
                if (w.surface == SurfaceType.Kerb || w.surface == SurfaceType.SausageKerb) kerb = Mathf.Max(kerb, surf.rumble * Mathf.Clamp01(speed / 10f));
                else if (Surface.IsOffTrack(w.surface)) { off += 0.25f; kerb = Mathf.Max(kerb, surf.rumble * 0.5f * Mathf.Clamp01(speed / 10f)); }
                surfaceVotes++;
                if (Surface.IsOffTrack(w.surface) || dominant == SurfaceType.Asphalt) dominant = w.surface;

                // Tyre heat and wear from the work done sliding.
                float slipPower = Mathf.Abs(fx * (w.omega * w.radius - vx)) + Mathf.Abs(fy * vy);
                w.surfaceTemp += slipPower * 0.0000105f * dt * 60f;
                w.wear += slipPower * WearRate(setup.compound) * dt;
            }

            WheelsOnGround = grounded / 4f;
            Airborne = grounded == 0;
            if (grounded > 0) rideHeightNow = Mathf.Lerp(rideHeightNow, Mathf.Max(0f, StaticFloorHeight - compSum / grounded), 0.2f);
            KerbRumble = kerb;
            Scrape = scrape;
            TyreSlide = Mathf.Clamp01(slide / 2f);
            WheelSpinAmount = Mathf.Clamp01(spin / 2f);
            LockupAmount = Mathf.Clamp01(lockup / 2f);
            OffTrackAmount = Mathf.Clamp01(off);
            DominantSurface = surfaceVotes > 0 ? dominant : SurfaceType.Asphalt;
            AligningTorque = align;
            TripRollover(dt);
        }

        // ------------------------------------------------------------------ rolling over

        float tripCooldown;
        public float LastTripTime { get; private set; } = -100f;

        /// <summary>
        /// Carrying far too much speed into a corner. A single-seater normally just slides, but once it's going
        /// properly sideways the leading tyres can dig in, most of all on a kerb, gravel or grass, and the car trips
        /// over them and rolls. The chance grows with how fast it's sliding and what the tyres hit. A violent roll can
        /// split the fuel cell (see CarDamage.Rolled), which may start a fire and, sometimes, an explosion.
        /// </summary>
        void TripRollover(float dt)
        {
            tripCooldown -= dt;
            if (Airborne || tripCooldown > 0f || SpeedMs < 12f) return;
            Vector3 v = Body.linearVelocity;
            float lat = Vector3.Dot(v, transform.right);
            float latAbs = Mathf.Abs(lat);
            if (latAbs < 9f || Upright < 0.6f) return;
            float dig = 0f;
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                if (!w.grounded || w.detached || Mathf.Sign(w.anchor.x) != Mathf.Sign(lat)) continue; // leading side only
                float f;
                switch (w.surface)
                {
                    case SurfaceType.SausageKerb: f = 1.6f; break;
                    case SurfaceType.Gravel: case SurfaceType.Sand: f = 1.25f; break;
                    case SurfaceType.Kerb: f = 1f; break;
                    case SurfaceType.Grass: case SurfaceType.Dirt: case SurfaceType.Snow: f = 0.8f; break;
                    case SurfaceType.Ice: f = 0.05f; break;
                    default: f = 0.22f; break; // grippy tarmac: it'll mostly just spin
                }
                dig += f * 0.5f;
            }
            if (dig <= 0f) return;
            float severity = Mathf.InverseLerp(9f, 30f, latAbs) * dig;
            // Probability per second: rare on tarmac, likely when sliding sideways into a sausage kerb at speed.
            float chancePerSecond = Mathf.Clamp01(severity * severity * 1.4f);
            if (Random.value > chancePerSecond * dt) return;

            float s = Mathf.Sign(lat);
            float spin = Mathf.Lerp(3.5f, 9f, Mathf.InverseLerp(9f, 32f, latAbs));
            // Pivot over the leading tyres: roll towards the slide, a hop, and part of the sideways speed goes into it.
            Body.AddTorque(-transform.forward * s * spin, ForceMode.VelocityChange);
            Body.AddForce(Vector3.up * Mathf.Lerp(1.5f, 4.5f, severity) - transform.right * lat * 0.25f, ForceMode.VelocityChange);
            tripCooldown = 4f;
            LastTripTime = Time.time;
            if (damage != null) damage.Rolled(Mathf.Clamp01(Mathf.InverseLerp(9f, 35f, latAbs) * (0.6f + 0.4f * Mathf.Min(1f, dig))));
        }

        static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void FreeSpin(Wheel w, float dt)
        {
            float driveT = w.driven ? wheelDriveTorque[System.Array.IndexOf(wheels, w)] : 0f;
            float brakeT = WheelBrakeTorque(System.Array.IndexOf(wheels, w));
            float inertia = spec.wheelInertia;
            w.omega += driveT / inertia * dt;
            float b = brakeT / inertia * dt + 0.5f * dt;
            w.omega = Mathf.Abs(w.omega) <= b ? 0f : w.omega - Mathf.Sign(w.omega) * b;
            w.slipAngleDyn = Mathf.Lerp(w.slipAngleDyn, 0f, dt * 4f);
        }

        // ---- brakes

        float WheelBrakeTorque(int i)
        {
            var w = wheels[i];
            float bias = setup.Bias(spec);
            float share = w.front ? bias : 1f - bias;
            float pressure = setup.brakePressure;
            float b = input.brake;
            // ABS: back off a wheel that's starting to lock.
            if (abs && w.grounded && SpeedMs > 3f && w.slipRatio < -spec.peakSlipRatio * 1.25f) b *= 0.55f;
            float fade = BrakeEfficiency(w.brakeTemp);
            float torque = spec.brakeTorque * 2f * share * b * pressure * fade;
            if (!w.front && input.handbrake > 0.01f && spec.hasHandbrake)
                torque = Mathf.Max(torque, spec.brakeTorque * 1.4f * input.handbrake);
            if (damage != null && damage.EngineDead && !w.front) torque += 30f;
            return torque;
        }

        float BrakeEfficiency(float t)
        {
            if (setup.brakeType == BrakeType.Carbon)
                return Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(150f, 450f, t)) * Mathf.Lerp(1f, 0.8f, Mathf.InverseLerp(1050f, 1300f, t));
            return Mathf.Lerp(0.95f, 1f, Mathf.InverseLerp(50f, 250f, t)) * Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(650f, 900f, t)) * 0.92f;
        }

        // ---- drivetrain

        readonly float[] wheelDriveTorque = new float[4];
        bool clutchLocked;

        float EngineRatioSq() { float r = Ratio(Gear); return r * r; }

        float stabilityThrottle = 1f;

        /// <summary>
        /// Stability control (on with traction control): if the car starts rotating faster than your steering asks
        /// for, or the back starts stepping out, it gently brakes the rotation and eases off the power, so steering
        /// left and right never spins you into a donut. It only acts past the point a tidy car would go.
        /// </summary>
        void Stability(float dt)
        {
            stabilityThrottle = 1f;
            if (!tractionControl || SpeedMs < 2f) return;
            int grounded = 0;
            foreach (var w in wheels) if (w.grounded) grounded++;
            if (grounded < 3) return;
            Vector3 localV = transform.InverseTransformDirection(Body.linearVelocity);
            float vf = localV.z;
            float r = transform.InverseTransformDirection(Body.angularVelocity).y; // + = turning right
            // Yaw rate the steering asks for, capped by what the tyres can actually hold (about 2.5 g with downforce).
            float steerRad = SteerAngleDeg * Mathf.Deg2Rad;
            float want = vf * Mathf.Tan(steerRad) / spec.wheelbase;
            float cap = 2.5f * Gravity / Mathf.Max(4f, SpeedMs);
            want = Mathf.Clamp(want, -cap, cap);
            float excess = r - want;
            const float margin = 0.12f; // rad/s of slack before it steps in
            if (Mathf.Abs(excess) > margin)
            {
                float over = excess - Mathf.Sign(excess) * margin;
                float gain = 9f; // per second: how fast the extra rotation is taken out
                Body.AddRelativeTorque(Vector3.up * (-over * gain * Body.inertiaTensor.y));
            }
            // The back stepping out (body slip angle): ease off the power.
            float slip = Mathf.Abs(Mathf.Atan2(localV.x, Mathf.Max(1f, Mathf.Abs(vf)))) * Mathf.Rad2Deg;
            if (slip > 6f) stabilityThrottle = Mathf.Lerp(1f, 0.2f, Mathf.InverseLerp(6f, 20f, slip));
        }

        void Drivetrain(float dt)
        {
            bool engineDead = damage != null && damage.EngineDead;
            float powerScale = damage != null ? damage.PowerScale : 1f;
            if (EngineTemp > 128f) powerScale *= Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(128f, 145f, EngineTemp));
            if (Fuel <= 0f) { engineDead = true; }

            float thr = input.throttle * stabilityThrottle;
            throttleSmooth = Mathf.MoveTowards(throttleSmooth, thr, dt / 0.06f);
            thr = throttleSmooth;
            if (cutTimer > 0f) { cutTimer -= dt; thr *= 0.15f; }

            // Traction control: trim throttle when the rears spin up.
            if (tractionControl && Gear > 0)
            {
                float sr = Mathf.Max(wheels[2].slipRatio, wheels[3].slipRatio);
                if (sr > spec.peakSlipRatio * 1.15f) thr *= Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(spec.peakSlipRatio * 1.15f, spec.peakSlipRatio * 2.5f, sr));
            }

            bool shifting = shiftTimer > 0f;
            // Mid-shift the torque is cut and the revs fall (or blip up) to the next gear's speed.
            float ratio = shifting ? 0f : Ratio(Gear);
            float wheelRpm = RearOmega() * 60f / (2f * Mathf.PI);
            float coupledRpm = Mathf.Abs(wheelRpm * (shifting ? Ratio(shiftTarget) : ratio));
            float launchRpm = Mathf.Lerp(spec.idleRpm, spec.peakTorqueRpm, 0.55f);

            // Free-revving engine target (clutch slipping or in neutral).
            float freeTarget = engineDead ? 0f : spec.idleRpm + (spec.limiter - spec.idleRpm) * thr;
            clutchRpm = Mathf.MoveTowards(clutchRpm, Gear == 0 ? freeTarget : Mathf.Min(freeTarget, launchRpm + (spec.limiter - launchRpm) * 0.1f), dt * (thr > 0.1f ? 30000f : 16000f));

            float rpm;
            float torqueToWheels = 0f;
            clutchLocked = ratio != 0f && coupledRpm >= clutchRpm - 50f;
            if (shifting && shiftTarget != 0 && Mathf.Abs(ForwardSpeed) > 2f)
            {
                rpm = Mathf.Lerp(Rpm, Mathf.Max(coupledRpm, spec.idleRpm), 0.5f);
                clutchRpm = rpm;
            }
            else if (ratio == 0f)
            {
                rpm = Mathf.Max(spec.idleRpm * (engineDead ? 0f : 1f), clutchRpm);
            }
            else if (clutchLocked)
            {
                rpm = Mathf.Max(coupledRpm, engineDead ? 0f : spec.idleRpm * 0.85f);
            }
            else
            {
                rpm = clutchRpm; // clutch slipping: the engine sits at launch revs and the clutch passes its torque
            }

            Limiter = false;
            float engineT = 0f;
            if (!engineDead)
            {
                if (rpm >= spec.limiter)
                {
                    limiterPhase += dt * 25f;
                    Limiter = true;
                    engineT = Mathf.Repeat(limiterPhase, 1f) > 0.5f ? -spec.engineBrake : TorqueCurve(rpm) * thr * 0.2f;
                }
                else
                {
                    engineT = TorqueCurve(rpm) * thr * powerScale;
                    // Hybrid deploy: extra power at full throttle while the battery lasts.
                    Deploying = spec.hybridKw > 0f && thr > 0.95f && Battery > 0.01f && SpeedMs > 22f;
                    if (Deploying)
                    {
                        float omegaE = Mathf.Max(rpm, 1000f) * 2f * Mathf.PI / 60f;
                        engineT += spec.hybridKw * 1000f / omegaE * powerScale;
                    }
                    engineT -= spec.engineBrake * (1f - thr) * Mathf.InverseLerp(spec.idleRpm, spec.redline, rpm);
                }
            }
            else engineT = -spec.engineBrake * 0.5f;

            if (ratio != 0f)
                torqueToWheels = engineT * ratio * 0.92f;
            EngineLoad = Mathf.Clamp01(engineT / Mathf.Max(1f, spec.peakTorque));
            ThrottleOut = thr;
            Rpm = Mathf.Lerp(Rpm, Mathf.Min(rpm, spec.limiter + 150f), 0.5f);

            // Limited-slip differential: split the torque, then move some from the faster rear wheel to the slower one.
            float dOmega = wheels[2].omega - wheels[3].omega;
            float lockTorque = Mathf.Clamp(dOmega * 40f, -Mathf.Abs(torqueToWheels) * 0.35f - 80f, Mathf.Abs(torqueToWheels) * 0.35f + 80f);
            if (wheels[2].detached || wheels[3].detached) lockTorque = 0f;
            wheelDriveTorque[0] = wheelDriveTorque[1] = 0f;
            wheelDriveTorque[2] = torqueToWheels * 0.5f - lockTorque * 0.5f;
            wheelDriveTorque[3] = torqueToWheels * 0.5f + lockTorque * 0.5f;
        }

        // ---- temperatures, fuel, battery

        void Temperatures(float dt)
        {
            float ambient = Env.AmbientC;
            float track = Env.TrackC;
            float cooling = 0.018f + SpeedMs * 0.0009f;
            foreach (var w in wheels)
            {
                // Surface follows the core over time; both lose heat to the air and gain from the road.
                w.surfaceTemp += ((track - w.surfaceTemp) * 0.02f - (w.surfaceTemp - ambient) * cooling * 0.5f) * dt;
                if (Env.Wetness > 0.2f) w.surfaceTemp -= Env.Wetness * 1.6f * dt * (setup.compound >= TireCompound.Intermediate ? 0.5f : 1.4f);
                w.coreTemp = Mathf.Lerp(w.coreTemp, w.surfaceTemp, dt * 0.25f);
                // Brakes heat with the energy they absorb and cool with airflow.
                float brakePower = WheelBrakeTorque(System.Array.IndexOf(wheels, w)) * Mathf.Abs(w.omega);
                w.brakeTemp += brakePower * 0.00006f * dt - (w.brakeTemp - ambient) * (0.06f + SpeedMs * 0.004f) * dt;
                w.brakeTemp = Mathf.Clamp(w.brakeTemp, ambient, 1400f);
            }
            float heatIn = Rpm / spec.limiter * (0.4f + ThrottleOut) * 9f;
            float heatOut = (EngineTemp - ambient) * (0.03f + SpeedMs * 0.0016f * (1f - DirtyAir * 0.4f));
            if (damage != null) heatIn *= damage.CoolingPenalty;
            EngineTemp += (heatIn - heatOut) * dt * 0.6f;
        }

        void FuelAndEnergy(float dt)
        {
            float burn = (0.0018f + ThrottleOut * Rpm / spec.limiter * 0.026f) * spec.fuelPerLapRate;
            if (Settings.FuelUse) Fuel = Mathf.Max(0f, Fuel - burn * dt * (spec.peakTorque / 450f));
            if (spec.hybridKw > 0f)
            {
                if (Deploying) Battery -= dt / 34f;
                else if (input.brake > 0.1f && SpeedMs > 10f) Battery += dt * input.brake / 14f;
                else if (ThrottleOut < 0.2f) Battery += dt / 90f;
                Battery = Mathf.Clamp01(Battery);
            }
        }

        void Summaries() { }

        void Update()
        {
            if (spec == null) return;
            // Spin the visual wheels and move them with the suspension.
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                if (w == null || w.visual == null || w.detached) continue;
                w.spinAngle = Mathf.Repeat(w.spinAngle + w.omega * Mathf.Rad2Deg * Time.deltaTime, 360f);
                float y = w.anchor.y - (w.restLength - w.compression);
                if (w.steerPivot != null)
                {
                    w.steerPivot.localPosition = new Vector3(w.anchor.x, y, w.anchor.z);
                    w.steerPivot.localRotation = Quaternion.Euler(0f, w.steer, 0f);
                    w.visual.localRotation = Quaternion.Euler(w.spinAngle, 0f, 0f);
                }
                else
                {
                    w.visual.localPosition = new Vector3(w.anchor.x, y, w.anchor.z);
                    w.visual.localRotation = Quaternion.Euler(0f, w.steer, 0f) * Quaternion.Euler(w.spinAngle, 0f, 0f);
                }
            }
        }

        // ---------------------------------------------------------------- tyre compound behaviour

        public static float OptimalTemp(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return 95f;
                case TireCompound.Medium: return 102f;
                case TireCompound.Hard: return 110f;
                case TireCompound.Intermediate: return 75f;
                default: return 65f;
            }
        }

        float TempGrip(Wheel w)
        {
            float t = w.surfaceTemp * 0.6f + w.coreTemp * 0.4f;
            float opt = OptimalTemp(setup.compound);
            float d = (t - opt) / 35f;
            return Mathf.Clamp(1f - d * d * 0.22f, 0.78f, 1f);
        }

        static float CompoundGrip(TireCompound c, float wet)
        {
            float dry, wetG;
            switch (c)
            {
                case TireCompound.Soft: dry = 1.03f; wetG = 0.6f; break;
                case TireCompound.Medium: dry = 1f; wetG = 0.58f; break;
                case TireCompound.Hard: dry = 0.97f; wetG = 0.55f; break;
                case TireCompound.Intermediate: dry = 0.86f; wetG = 0.88f; break;
                default: dry = 0.8f; wetG = 0.94f; break;
            }
            return Mathf.Lerp(dry, wetG, wet);
        }

        static float WearRate(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return 1.6e-8f;
                case TireCompound.Medium: return 1.05e-8f;
                case TireCompound.Hard: return 0.7e-8f;
                case TireCompound.Intermediate: return 1.4e-8f;
                default: return 1.2e-8f;
            }
        }

        public float AverageWear { get { float s = 0f; foreach (var w in wheels) s += w.wear; return s / 4f; } }
    }
}
