using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Veylix
{
    /// <summary>
    /// Drives the player's car from the cockpit. The steering wheel is a physical object: reach out, squeeze the grip
    /// on the rim to take hold, and turn it with your real hands (one or both). Let go and it self-centres with the
    /// tyres' aligning torque, like a real wheel. Right trigger is the throttle, left trigger the brake (or the
    /// thumbsticks, in Settings); A and X/Y are the paddles (B is held to reset) (they animate on the wheel); in cars that have one,
    /// grab the handbrake lever by your right leg and pull. Force feedback comes through both controllers.
    /// Without a headset: keyboard (WASD / arrows, Q/E or Shift/Ctrl to shift, Space handbrake) or a gamepad.
    /// </summary>
    public class Cockpit : MonoBehaviour
    {
        public Car car;
        public bool inputEnabled = true;

        // Steering wheel state (degrees of wheel rotation, + = clockwise = right).
        public float WheelAngle { get; private set; }
        float wheelVel;
        float lockDeg;
        float handTarget; // where your hands are turning the wheel to (smoothed)

        struct Grab
        {
            public bool holding;
            public float startHandAngle, startWheelAngle;
            public float lastHandAngle, unwrapped;
        }
        Grab gl, gr;
        bool lHandbrake, rHandbrake;
        float handbrakeAmount;
        float keyboardSteer;
        float paddleL, paddleR;
        float lastAlign;
        float rumbleT;

        public bool LeftOnWheel => gl.holding;
        public bool RightOnWheel => gr.holding;

        public void Attach(Car c)
        {
            car = c;
            lockDeg = Settings.WheelRotation > 0 ? Settings.WheelRotation * 0.5f : c.spec.wheelLock;
            WheelAngle = 0f; wheelVel = 0f;
            gl = default; gr = default;
            if (car.damage != null)
            {
                car.damage.OnImpact -= OnImpact;
                car.damage.OnImpact += OnImpact;
            }
        }

        void OnDestroy()
        {
            if (car != null && car.damage != null) car.damage.OnImpact -= OnImpact;
        }

        void OnImpact(float strength, Vector3 at)
        {
            var rig = Rig.I;
            if (rig == null) return;
            float amp = Mathf.Clamp01(0.3f + strength);
            float dur = 0.06f + strength * 0.25f;
            // The hand nearest the impact feels it most.
            float side = car.transform.InverseTransformPoint(at).x;
            rig.Haptic(true, amp * (side < 0f ? 1f : 0.6f), dur);
            rig.Haptic(false, amp * (side > 0f ? 1f : 0.6f), dur);
            if (InputHub.Pad != null) { InputHub.Pad.SetMotorSpeeds(amp, amp * 0.6f); padRumbleUntil = Time.time + dur; }
        }
        float padRumbleUntil;

        void Update()
        {
            if (car == null || car.spec == null) return;
            InputHub.Poll();
            float dt = Time.deltaTime;
            var rig = Rig.I;
            bool xr = rig != null && rig.IsXR;
            var parts = car.parts;

            float throttle = 0f, brake = 0f, handbrake = 0f;
            bool up = false, down = false;

            if (xr && parts != null && parts.steeringWheel != null)
            {
                SteerVR(rig, parts, dt);
                var L = InputHub.Left; var R = InputHub.Right;
                if (Settings.Pedals == PedalMode.Triggers)
                {
                    throttle = Pedal(R.trigger);
                    brake = Pedal(L.trigger);
                }
                else
                {
                    float y = R.stick.y;
                    throttle = Mathf.Clamp01(y);
                    brake = Mathf.Clamp01(-y);
                }
                up = R.primaryDown;
                down = L.primaryDown || L.secondaryDown; // right B is held to reset the car
                handbrake = HandbrakeVR(rig, parts);
            }
            else
            {
                SteerDesktop(dt);
                bool kbUp = InputHub.Key(Key.W) || InputHub.Key(Key.UpArrow);
                bool kbDown = InputHub.Key(Key.S) || InputHub.Key(Key.DownArrow);
                throttle = Mathf.Max(kbUp ? 1f : 0f, InputHub.PadThrottle);
                brake = Mathf.Max(kbDown ? 1f : 0f, InputHub.PadBrake);
                up = InputHub.KeyDown(Key.E) || InputHub.KeyDown(Key.LeftShift) || (InputHub.Pad != null && InputHub.Pad.buttonSouth.wasPressedThisFrame) || (InputHub.Pad != null && InputHub.Pad.rightShoulder.wasPressedThisFrame);
                down = InputHub.KeyDown(Key.Q) || InputHub.KeyDown(Key.LeftCtrl) || (InputHub.Pad != null && InputHub.Pad.buttonWest.wasPressedThisFrame) || (InputHub.Pad != null && InputHub.Pad.leftShoulder.wasPressedThisFrame);
                handbrake = InputHub.Key(Key.Space) || (InputHub.Pad != null && InputHub.Pad.buttonEast.isPressed) ? 1f : 0f;
                PlaceDesktopGloves(rig, parts);
            }

            if (!inputEnabled) { throttle = car.Frozen ? throttle : 0f; brake = car.Frozen ? 0f : 1f; up = down = false; }

            // Steering ratio: wheel rotation -> road-wheel angle, with sensitivity and an optional softer centre.
            float norm = Mathf.Clamp(WheelAngle / lockDeg, -1f, 1f);
            float lin = Settings.SteeringLinearity;
            float shaped = Mathf.Sign(norm) * Mathf.Lerp(Mathf.Abs(norm), Mathf.Pow(Mathf.Abs(norm), 1.8f), lin);
            car.input.steer = Mathf.Clamp(shaped * Settings.SteeringSensitivity, -1f, 1f);
            car.input.throttle = throttle;
            car.input.brake = brake;
            car.input.handbrake = car.spec.hasHandbrake ? handbrake : 0f;
            if (up) { car.input.shiftUp = true; paddleR = 1f; }
            if (down) { car.input.shiftDown = true; paddleL = 1f; }

            AnimateWheel(parts, dt);
            ForceFeedback(rig, xr, dt);
            if (InputHub.Pad != null && Time.time > padRumbleUntil) InputHub.Pad.SetMotorSpeeds(car.KerbRumble * 0.25f, car.KerbRumble * 0.35f + car.TyreSlide * 0.15f);
        }

        static float Pedal(float t) => Mathf.Clamp01((t - 0.04f) / 0.92f);

        // ------------------------------------------------------------------ the steering wheel

        /// <summary>Angle (degrees) of a hand around the wheel's axis, in the wheel plane (0 = top, + = clockwise from the driver's view).</summary>
        float HandAngle(Transform wheelPivot, Vector3 handPos, out float radial, out float axial)
        {
            Vector3 local = wheelPivot.InverseTransformPoint(handPos);
            // The pivot's local Z points from the driver towards the dash (the wheel faces -Z).
            axial = local.z;
            radial = new Vector2(local.x, local.y).magnitude;
            return Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
        }

        void SteerVR(Rig rig, CarParts parts, float dt)
        {
            var pivot = parts.steeringWheel;
            // Measure hands against the wheel's unrotated frame (the pivot itself doesn't spin; its child does).
            UpdateGrab(ref gl, InputHub.Left, rig.leftHand, rig.leftGlove, pivot, parts, true);
            UpdateGrab(ref gr, InputHub.Right, rig.rightHand, rig.rightGlove, pivot, parts, false);

            if (gl.holding || gr.holding)
            {
                float sum = 0f; int n = 0;
                if (gl.holding) { sum += gl.startWheelAngle + gl.unwrapped; n++; }
                if (gr.holding) { sum += gr.startWheelAngle + gr.unwrapped; n++; }
                float target = Mathf.Clamp(sum / n, -lockDeg, lockDeg);
                // Filter out the tiny shakes of a hand held in the air.
                handTarget = Mathf.Lerp(handTarget, target, 1f - Mathf.Exp(-dt * 30f));
                WeightedWheel(handTarget, n, dt);
            }
            else { handTarget = WheelAngle; SelfCentre(dt); }
        }

        /// <summary>
        /// The wheel has weight: it follows your hands through a spring (it trails them a little when you move fast),
        /// it can only turn so quickly, and the tyres pull it back towards straight, more the faster you go, so holding
        /// a corner takes effort. One hand on the wheel feels heavier than two. Settings > Steering wheel weight.
        /// </summary>
        void WeightedWheel(float target, int hands, float dt)
        {
            float w = Settings.SteeringWeight;
            if (w < 0.02f)
            {
                float prev = WheelAngle;
                WheelAngle = target;
                wheelVel = (WheelAngle - prev) / Mathf.Max(1e-4f, dt);
                return;
            }
            float speedF = Mathf.Clamp01(car.SpeedMs / 55f);
            // Spring stiffness (how hard your hands pull the wheel round): softer = heavier.
            float k = Mathf.Lerp(1400f, 260f, w) * Mathf.Lerp(1f, 0.7f, speedF) * (hands == 2 ? 1f : 0.7f);
            float c = 2f * Mathf.Sqrt(k) * 1.05f; // just over critical: no wobble
            // The tyres' self-centring (caster and aligning torque): stronger at speed and with grip.
            float centre = w * Mathf.Lerp(6f, 35f, speedF);
            float align = car.AligningTorque * 0.0004f * w * speedF;
            float acc = (target - WheelAngle) * k - wheelVel * c - WheelAngle * centre + align;
            wheelVel += acc * dt;
            float maxRate = Mathf.Lerp(900f, 420f, w); // degrees per second
            wheelVel = Mathf.Clamp(wheelVel, -maxRate, maxRate);
            WheelAngle = Mathf.Clamp(WheelAngle + wheelVel * dt, -lockDeg, lockDeg);
            if (Mathf.Abs(WheelAngle) >= lockDeg) wheelVel = 0f;
        }

        void UpdateGrab(ref Grab g, InputHub.Hand h, Transform hand, Glove glove, Transform pivot, CarParts parts, bool left)
        {
            if (!h.valid) { if (g.holding) Release(ref g, glove); return; }
            float ang = HandAngle(pivot, hand.position, out float radial, out float axial);
            float r = parts.wheelRadius;
            bool near = Mathf.Abs(radial - r) < 0.06f + (radial < r ? 0.02f : 0f) && Mathf.Abs(axial) < 0.09f;
            // Don't grab the wheel if this hand is on the handbrake.
            bool onHandbrake = left ? lHandbrake : rHandbrake;
            if (!g.holding && h.gripDown && near && !onHandbrake)
            {
                g.holding = true;
                g.startHandAngle = ang;
                g.lastHandAngle = ang;
                g.unwrapped = 0f;
                g.startWheelAngle = WheelAngle;
                Rig.I.Haptic(left, 0.4f, 0.03f);
                CrashAudio.Tick();
            }
            if (g.holding)
            {
                if (!h.gripHeld) { Release(ref g, glove); return; }
                // Unwrap the angle so turning past 180 degrees keeps counting.
                float d = Mathf.DeltaAngle(g.lastHandAngle, ang);
                // Ignore wild jumps when the hand crosses the hub.
                if (radial > 0.04f) g.unwrapped += d;
                g.lastHandAngle = ang;
                // Too far from the rim (stretched away): let go, like a real hand slipping off.
                if (Mathf.Abs(radial - r) > 0.25f || Mathf.Abs(axial) > 0.3f) { Release(ref g, glove); return; }
                // Pin the glove onto the rim where it was grabbed (rotates with the wheel).
                var grip = parts.steeringWheelGrip != null ? parts.steeringWheelGrip : pivot;
                RimPose(pivot, r, g.startHandAngle - g.startWheelAngle, out Vector3 world, out Quaternion rot);
                glove.Pin(grip, world, rot);
            }
        }

        // Where the rim sits inside a gripping glove (glove model space: back of the hand +Y, fingers +Z, knuckles at 0).
        static readonly Vector3 RimInGlove = new Vector3(0f, -0.026f, 0.022f);

        /// <summary>
        /// Pose of a glove holding the rim at 'onRim' degrees (0 = top, + clockwise), turning with the wheel: fingers
        /// reaching forward past the rim and curled round it, palm towards the hub, back of the hand outwards (thumb
        /// on top at quarter to three), the wrist on the driver's side.
        /// </summary>
        void RimPose(Transform pivot, float r, float onRim, out Vector3 world, out Quaternion rot)
        {
            float a = onRim * Mathf.Deg2Rad;
            var spin = Quaternion.Euler(0f, 0f, -WheelAngle);
            Vector3 outward = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);  // in the wheel's plane (pivot space)
            Vector3 rim = outward * r;
            // Fingers mostly forward (into the wheel's plane), tipped a little outwards; back of the hand outwards and
            // slightly back towards the driver.
            var local = Quaternion.LookRotation((Vector3.forward + outward * 0.2f).normalized, (outward - Vector3.forward * 0.25f).normalized);
            rot = pivot.rotation * spin * local;
            world = pivot.TransformPoint(spin * rim) - rot * RimInGlove;
        }

        void Release(ref Grab g, Glove glove)
        {
            g.holding = false;
            glove.Unpin();
        }

        /// <summary>No hands on the wheel: the tyres' aligning torque turns it back towards straight.</summary>
        void SelfCentre(float dt)
        {
            float speedFactor = Mathf.Clamp01(car.SpeedMs / 25f);
            // Where the front wheels "want" the steering to be: following the car's direction of travel (caster effect).
            float k = 18f * (0.15f + speedFactor);
            float c = 6f;
            float alignDeg = car.AligningTorque * 0.02f;
            float acc = -WheelAngle * k - wheelVel * c + alignDeg * speedFactor;
            wheelVel += acc * dt;
            WheelAngle = Mathf.Clamp(WheelAngle + wheelVel * dt, -lockDeg, lockDeg);
        }

        void SteerDesktop(float dt)
        {
            float pad = InputHub.PadSteer;
            float key = (InputHub.Key(Key.D) || InputHub.Key(Key.RightArrow) ? 1f : 0f) - (InputHub.Key(Key.A) || InputHub.Key(Key.LeftArrow) ? 1f : 0f);
            float target;
            if (Mathf.Abs(pad) > 0.05f) target = Mathf.Sign(pad) * Mathf.Pow(Mathf.Abs(pad), 1.5f);
            else
            {
                // Keyboard: steer faster at low speed, and limit lock at high speed so the car stays drivable.
                float limit = Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(car.SpeedMs / 70f));
                float rate = key != 0f ? Mathf.Lerp(3.5f, 1.6f, Mathf.Clamp01(car.SpeedMs / 70f)) : 5f;
                keyboardSteer = Mathf.MoveTowards(keyboardSteer, key * limit, rate * dt);
                target = keyboardSteer;
            }
            float prev = WheelAngle;
            WheelAngle = target * lockDeg;
            wheelVel = (WheelAngle - prev) / Mathf.Max(1e-4f, dt);
        }

        void PlaceDesktopGloves(Rig rig, CarParts parts)
        {
            if (rig == null || parts == null || parts.steeringWheel == null) return;
            var grip = parts.steeringWheelGrip != null ? parts.steeringWheelGrip : parts.steeringWheel;
            float r = parts.wheelRadius;
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                RimPose(parts.steeringWheel, r, left ? -90f : 90f, out Vector3 world, out Quaternion rot);
                (left ? rig.leftGlove : rig.rightGlove).Pin(grip, world, rot);
            }
        }

        void AnimateWheel(CarParts parts, float dt)
        {
            if (parts == null || parts.steeringWheelGrip == null) return;
            parts.steeringWheelGrip.localRotation = Quaternion.Euler(0f, 180f, WheelAngle);
            paddleL = Mathf.MoveTowards(paddleL, 0f, dt * 6f);
            paddleR = Mathf.MoveTowards(paddleR, 0f, dt * 6f);
            if (parts.paddleLeft != null) parts.paddleLeft.localRotation = Quaternion.Euler(-paddleL * 12f, 0f, 0f);
            if (parts.paddleRight != null) parts.paddleRight.localRotation = Quaternion.Euler(-paddleR * 12f, 0f, 0f);
            if (parts.handbrakeLever != null) parts.handbrakeLever.localRotation = Quaternion.Euler(10f - handbrakeAmount * 35f, 0f, 0f);
        }

        // ------------------------------------------------------------------ handbrake

        float HandbrakeVR(Rig rig, CarParts parts)
        {
            if (parts.handbrakeLever == null) { handbrakeAmount = 0f; return 0f; }
            var h = InputHub.Right;
            Vector3 lever = parts.handbrakeLever.TransformPoint(new Vector3(0f, 0.22f, 0.045f));
            float d = Vector3.Distance(rig.rightHand.position, lever);
            if (!rHandbrake && h.gripDown && d < 0.12f && !gr.holding) { rHandbrake = true; rig.Haptic(false, 0.4f, 0.03f); }
            if (rHandbrake && !h.gripHeld) { rHandbrake = false; rig.rightGlove.Unpin(); }
            if (rHandbrake)
            {
                // Pulling the hand back/up from the lever's rest position = more handbrake.
                Vector3 local = parts.handbrakeLever.parent.InverseTransformPoint(rig.rightHand.position) - parts.handbrakeLever.localPosition;
                handbrakeAmount = Mathf.Clamp01((-local.z + local.y * 0.5f - 0.05f) / 0.14f);
                rig.rightGlove.Pin(parts.handbrakeLever, lever, parts.handbrakeLever.rotation * Quaternion.Euler(0f, 0f, -90f));
            }
            else handbrakeAmount = Mathf.MoveTowards(handbrakeAmount, 0f, Time.deltaTime * 6f);
            return handbrakeAmount;
        }

        // ------------------------------------------------------------------ force feedback

        void ForceFeedback(Rig rig, bool xr, float dt)
        {
            if (!xr || rig == null) return;
            float strength = Settings.Haptics;
            if (strength <= 0.01f) return;
            rumbleT -= dt;
            if (rumbleT > 0f) return;
            rumbleT = 0.05f; // 20 Hz pulse train
            float speed = Mathf.Clamp01(car.SpeedMs / 40f);
            float align = car.AligningTorque;
            float alignChange = Mathf.Abs(align - lastAlign) * 0.002f;
            lastAlign = align;
            // Steering load through the rim (stronger with grip and speed), kerbs, sliding and lock-ups.
            float load = Mathf.Clamp01(Mathf.Abs(align) * 0.00025f) * speed * 0.25f;
            float kerb = car.KerbRumble * 0.7f;
            float slide = car.TyreSlide * 0.3f + car.LockupAmount * 0.35f;
            float scrape = car.Scrape * 0.5f;
            float engine = car.Rpm / car.spec.limiter * 0.04f + (car.Limiter ? 0.15f : 0f);
            float onWheel = Mathf.Clamp01(load + kerb + slide + alignChange + scrape + engine);
            float offWheel = Mathf.Clamp01(kerb * 0.3f + scrape * 0.3f);
            InputHub.Haptic(XRNode.LeftHand, (gl.holding ? onWheel : offWheel) * strength, 0.055f);
            InputHub.Haptic(XRNode.RightHand, (gr.holding ? onWheel : offWheel) * strength, 0.055f);
        }
    }
}
