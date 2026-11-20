using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A drivable golf cart. One is parked by every spawn point on the outdoor raid maps, and testers can spawn more
    /// (menu > TESTER > SPAWN CART).
    ///  * Get in: grab the steering wheel (grip) - on desktop walk up and press F.
    ///  * Drive: left stick forward / back = gas / reverse, left stick left / right = steer. Desktop: W A S D.
    ///  * Get out: A or X (desktop: F or Space).
    ///  * Running into people (raiders or players) at speed hurts them - the harder the hit, the more.
    /// Physics is a simple raycast suspension on a rigidbody. In multiplayer the driver's game moves the cart and sends
    /// where it is; everyone else shows it there (and can't get in while someone's driving).
    /// </summary>
    public class GolfCart : MonoBehaviour
    {
        public static readonly List<GolfCart> All = new List<GolfCart>();
        public int Id;
        public Rigidbody rb;
        public Transform Seat, PassSeat, WheelGrab;
        const float WheelTilt = 35f;
        readonly Transform[] _doors = new Transform[2], _handles = new Transform[2];
        readonly float[] _doorOpen = new float[2], _doorUntil = new float[2];
        Transform[] _wheels = new Transform[4];
        Transform _steerWheel;
        AudioSource _motor;

        const float MaxSpeed = 9f, MaxReverse = 4f;   // m/s (~32 km/h forward)
        static readonly Vector3[] Corners = { new Vector3(-0.55f, 0.45f, 0.82f), new Vector3(0.55f, 0.45f, 0.82f), new Vector3(-0.55f, 0.45f, -0.82f), new Vector3(0.55f, 0.45f, -0.82f) };
        const float SuspLen = 0.62f, Rest = 0.45f;

        // remote driving (multiplayer)
        public ulong RemoteDriver;
        float _remoteT;
        Vector3 _netPos; Quaternion _netRot;
        float _sendT;
        readonly Dictionary<object, float> _hitCooldown = new Dictionary<object, float>();
        float _wheelSpin, _steerVis;

        public float Throttle, Steer;   // set by the driver each frame
        public bool LocalDriving => RagoGame.I != null && RagoGame.I.Player != null && RagoGame.I.Player.Driving == this && !RagoGame.I.Player.Passenger;
        public bool LocalInside => RagoGame.I != null && RagoGame.I.Player != null && RagoGame.I.Player.Driving == this;
        public bool Busy => LocalDriving || (RemoteDriver != 0 && Time.time - _remoteT < 1.5f);

        // ------------------------------------------------------------------------------------------- spawning
        /// <summary>A cart parked beside every player spawn on the outdoor raid maps (the same ones on every machine).</summary>
        public static void SpawnFor(MapBase map)
        {
            Clear();
            if (map == null || !map.IsRaid || map.Id == "underground") return;
            for (int i = 0; i < map.PlayerSpawns.Count; i++)
            {
                var sp = map.PlayerSpawns[i];
                Vector3 side = sp.rotation * Vector3.right;
                Vector3 at = sp.position + side * 3.5f;
                if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out var h, 140f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) at = h.point;
                Create(i, at + Vector3.up * 0.5f, Quaternion.Euler(0, sp.rotation.eulerAngles.y, 0), map.transform);
            }
        }

        public static void Clear()
        {
            foreach (var c in new List<GolfCart>(All)) if (c != null) Destroy(c.gameObject);
            All.Clear();
        }

        public static GolfCart Find(int id) { foreach (var c in All) if (c != null && c.Id == id) return c; return null; }

        static int _spawnN;
        /// <summary>Tester: a new cart in front of you (and on everyone else's screen in multiplayer).</summary>
        public static GolfCart SpawnHere(Vector3 pos, float yaw)
        {
            ulong me = NetGame.Live && NetHub.Backend != null ? NetHub.Backend.LocalId : 0;
            int id = 1000 + (int)(me % 200) * 100 + (_spawnN++ % 100);
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var h, 20f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) pos = h.point;
            var parent = RagoGame.I != null && RagoGame.I.Map != null ? RagoGame.I.Map.transform : null;
            var c = Create(id, pos + Vector3.up * 0.5f, Quaternion.Euler(0, yaw, 0), parent);
            if (NetGame.Live) NetGame.I.CartSpawned(id, c.transform.position, yaw);
            return c;
        }

        public static GolfCart Create(int id, Vector3 pos, Quaternion rot, Transform parent)
        {
            var old = Find(id); if (old != null) Destroy(old.gameObject);
            var go = new GameObject("GolfCart_" + id);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            var c = go.AddComponent<GolfCart>();
            c.Id = id;
            c.Build();
            All.Add(c);
            return c;
        }

        void Build()
        {
            Color paint = new Color(0.92f, 0.91f, 0.86f), dark = new Color(0.1f, 0.1f, 0.11f), roofCol = new Color(0.16f, 0.36f, 0.22f), seat = new Color(0.32f, 0.2f, 0.12f);
            var mb = new MB(3);
            mb.Sub(0).Box(new Vector3(0, 0.42f, 0), new Vector3(1.2f, 0.12f, 2.3f), paint, 0.02f);                 // floor pan
            mb.Sub(0).Box(new Vector3(0, 0.66f, 0.98f), new Vector3(1.2f, 0.44f, 0.42f), paint, 0.06f);            // front cowl
            mb.Sub(0).Box(new Vector3(0, 0.64f, -0.88f), new Vector3(1.2f, 0.4f, 0.56f), paint, 0.05f);            // rear body
            mb.Sub(0).Box(new Vector3(0, 0.96f, 0.66f), new Vector3(1.12f, 0.18f, 0.18f), paint, 0.03f);           // dash
            mb.Sub(1).Box(new Vector3(0, 0.74f, -0.25f), new Vector3(1.1f, 0.14f, 0.55f), seat, 0.03f);            // seat
            mb.Sub(1).Box(new Vector3(0, 1.0f, -0.52f), new Vector3(1.1f, 0.45f, 0.1f), seat, 0.03f);              // backrest
            mb.Sub(1).Box(new Vector3(0, 0.55f, 1.21f), new Vector3(1.1f, 0.14f, 0.06f), dark, 0.02f);             // bumper
            foreach (float sx in new[] { -0.54f, 0.54f })
            {
                mb.Sub(1).Cyl(new Vector3(sx, 0.85f, 0.72f), new Vector3(sx, 1.93f, 0.55f), 0.025f, 0.025f, 8, dark);    // roof posts
                mb.Sub(1).Cyl(new Vector3(sx, 0.85f, -0.62f), new Vector3(sx, 1.93f, -0.62f), 0.025f, 0.025f, 8, dark);
            }
            mb.Sub(2).Box(new Vector3(0, 1.96f, -0.03f), new Vector3(1.26f, 0.06f, 1.5f), roofCol, 0.02f);         // canopy
            mb.Sub(1).Cyl(new Vector3(-0.28f, 0.92f, 0.62f), new Vector3(-0.28f, 1.06f, 0.42f), 0.02f, 0.02f, 8, dark);   // steering column
            mb.Build("Body", transform, Mats.Lit("cart_paint", Color.white, 0.2f, 0.55f, Mats.Noise, null, 0.3f, 0.2f), Mats.Lit("cart_dark", Color.white, 0.1f, 0.3f, Mats.Noise, null, 0.2f, 0.1f), Mats.Lit("cart_roof", Color.white, 0f, 0.2f, Mats.Noise, null, 0.3f, 0.1f));
            // steering wheel (turns with the steering) - grab it to get in
            _steerWheel = new GameObject("SteeringWheel").transform;
            _steerWheel.SetParent(transform, false);
            _steerWheel.localPosition = new Vector3(-0.28f, 1.08f, 0.40f);
            _steerWheel.localRotation = Quaternion.Euler(WheelTilt, 0, 0);   // square to the column, tipped up towards the driver
            var sw = new MB(2);
            for (int k = 0; k < 16; k++)   // the rim: a ring, not a solid disc
            {
                float a0 = k * Mathf.PI * 2f / 16f, a1 = (k + 1) * Mathf.PI * 2f / 16f;
                sw.Sub(0).Cyl(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.17f, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 0.17f, 0.017f, 0.017f, 6, dark);
            }
            foreach (float deg in new[] { 0f, 180f, 270f })   // three spokes and a hub
            {
                float a = deg * Mathf.Deg2Rad;
                sw.Sub(1).Cyl(Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.16f, 0.011f, 0.011f, 6, new Color(0.35f, 0.35f, 0.37f));
            }
            sw.Sub(1).Cyl(new Vector3(0, 0, 0.025f), new Vector3(0, 0, -0.02f), 0.04f, 0.04f, 12, new Color(0.35f, 0.35f, 0.37f));
            sw.Build("Wheel", _steerWheel, Mats.Rubber, Mats.Lit("cart_spokes", Color.white, 0.6f, 0.5f, Mats.Metal, null, 0.2f, 0.2f));
            WheelGrab = _steerWheel;
            // doors: grab one to get in (driver's side = drive, the other = ride along)
            var paintMat = Mats.Lit("cart_paint", Color.white, 0.2f, 0.55f, Mats.Noise, null, 0.3f, 0.2f);
            var darkMat = Mats.Lit("cart_dark", Color.white, 0.1f, 0.3f, Mats.Noise, null, 0.2f, 0.1f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var d = new GameObject(i == 0 ? "DoorDriver" : "DoorPassenger").transform;
                d.SetParent(transform, false);
                d.localPosition = new Vector3(side * 0.62f, 0.74f, 0.12f);   // hinged at its front edge, beside the seat
                var dm = new MB(2);
                dm.Sub(0).Box(new Vector3(0, 0, -0.36f), new Vector3(0.035f, 0.46f, 0.68f), paint, 0.015f);
                dm.Sub(1).Box(new Vector3(side * 0.035f, 0.1f, -0.6f), new Vector3(0.035f, 0.04f, 0.13f), dark, 0.01f);   // handle
                dm.Build("Door", d, paintMat, darkMat);
                var h = new GameObject("Handle").transform; h.SetParent(d, false); h.localPosition = new Vector3(side * 0.06f, 0.1f, -0.6f);
                _doors[i] = d; _handles[i] = h;
            }
            // road wheels (spin with the speed)
            for (int i = 0; i < 4; i++)
            {
                var wt = new GameObject("Wheel" + i).transform;
                wt.SetParent(transform, false);
                wt.localPosition = new Vector3(Corners[i].x + (Corners[i].x < 0 ? -0.04f : 0.04f), 0.22f, Corners[i].z);
                var wm = new MB();
                wm.Cyl(new Vector3(-0.08f, 0, 0), new Vector3(0.08f, 0, 0), 0.22f, 0.22f, 16, dark);
                wm.Cyl(new Vector3(-0.085f, 0, 0), new Vector3(0.085f, 0, 0), 0.11f, 0.11f, 12, new Color(0.7f, 0.7f, 0.7f));
                wm.Build("Tyre", wt, Mats.Rubber);
                _wheels[i] = wt;
            }
            Seat = new GameObject("DriverSeat").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(-0.28f, 0.81f, -0.22f);   // top of the cushion, driver's side
            PassSeat = new GameObject("PassengerSeat").transform;
            PassSeat.SetParent(transform, false);
            PassSeat.localPosition = new Vector3(0.28f, 0.81f, -0.22f);
            // physics: one box for the body (above the wheels), the wheels are raycasts
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.66f, 0); col.size = new Vector3(1.2f, 0.5f, 2.35f);
            var roof = gameObject.AddComponent<BoxCollider>();
            roof.center = new Vector3(0, 1.96f, -0.03f); roof.size = new Vector3(1.26f, 0.08f, 1.5f);
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 350f; rb.drag = 0.05f; rb.angularDrag = 2f;
            rb.centerOfMass = new Vector3(0, 0.35f, 0);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _motor = gameObject.AddComponent<AudioSource>();
            _motor.clip = Synth.CartMotor(); _motor.loop = true; _motor.spatialBlend = 1f; _motor.minDistance = 2f; _motor.maxDistance = 30f; _motor.volume = 0f;
        }

        void OnDestroy() { All.Remove(this); if (LocalInside) RagoGame.I.Player.LeaveCart(); }

        // ------------------------------------------------------------------------------------------- driving
        public bool WheelNear(Vector3 p, float r = 0.3f) => WheelGrab != null && (WheelGrab.position - p).sqrMagnitude < r * r;
        /// <summary>Which door's handle a hand is on: 0 driver's, 1 passenger's, -1 neither.</summary>
        public int DoorNear(Vector3 p, float r = 0.3f)
        {
            for (int i = 0; i < 2; i++) if (_handles[i] != null && (_handles[i].position - p).sqrMagnitude < r * r) return i;
            return -1;
        }
        /// <summary>Swing a door open for a moment (getting in or out).</summary>
        public void SwingDoor(int i) { if (i < 0 || i > 1) return; _doorUntil[i] = Time.time + 0.9f; Sfx.Play(Synth.Footstep(Synth.Surface.Metal, 3, false), _handles[i].position, 0.35f, 1.2f, 1f, 12f); }

        void FixedUpdate()
        {
            if (rb == null) return;
            bool remote = RemoteDriver != 0 && Time.time - _remoteT < 1.5f;
            if (remote)
            {
                // someone else is driving it: show it where their game says it is
                if (!rb.isKinematic) rb.isKinematic = true;
                rb.MovePosition(Vector3.Lerp(rb.position, _netPos, 0.35f));
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, _netRot, 0.35f));
                return;
            }
            if (rb.isKinematic) { rb.isKinematic = false; RemoteDriver = 0; }
            float dt = Time.fixedDeltaTime;
            bool driven = LocalDriving;
            float thr = driven ? Throttle : 0f, st = driven ? Steer : 0f;
            int grounded = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector3 o = transform.TransformPoint(Corners[i]);
                if (!Physics.Raycast(o, -transform.up, out var h, SuspLen, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;
                if (h.collider.attachedRigidbody == rb) continue;
                grounded++;
                float comp = Mathf.Clamp01((SuspLen - h.distance) / (SuspLen - Rest + 0.2f));
                float vUp = Vector3.Dot(rb.GetPointVelocity(o), transform.up);
                float f = comp * 9000f - vUp * 1100f;
                if (f > 0f) rb.AddForceAtPosition(transform.up * f, o);
            }
            Vector3 v = rb.velocity;
            float fwdSpeed = Vector3.Dot(v, transform.forward);
            float sideSpeed = Vector3.Dot(v, transform.right);
            if (grounded >= 2)
            {
                float target = thr >= 0f ? thr * MaxSpeed : thr * MaxReverse;
                float accel = Mathf.Clamp((target - fwdSpeed) * 2.2f, -6f, thr == 0f ? 6f : 3.5f);
                if (!driven) accel = Mathf.Clamp(-fwdSpeed * 3f, -6f, 6f);   // parked: handbrake on
                rb.AddForce(transform.forward * accel * rb.mass);
                rb.AddForce(-transform.right * Mathf.Clamp(sideSpeed * 6f, -12f, 12f) * rb.mass);   // tyres grip sideways
                float yawRate = st * Mathf.Clamp(fwdSpeed / 3f, -1f, 1f) * 1.3f;
                var av = rb.angularVelocity;
                Vector3 local = transform.InverseTransformDirection(av);
                local.y = Mathf.Lerp(local.y, yawRate, 0.25f);
                rb.angularVelocity = transform.TransformDirection(local);
            }
            // keep it from flipping on rough ground: it rights itself slowly
            Vector3 upErr = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(upErr * rb.mass * 6f);
            // multiplayer: the driver's game says where the cart is
            if (driven && NetGame.Live)
            {
                _sendT -= dt;
                if (_sendT <= 0f) { _sendT = 1f / 12f; NetGame.I.CartState(Id, rb.position, rb.rotation, true); }
            }
        }

        void Update()
        {
            float speed = rb != null ? Vector3.Dot(rb.velocity, transform.forward) : 0f;
            _wheelSpin += speed / 0.22f * Mathf.Rad2Deg * Time.deltaTime;
            for (int i = 0; i < 4; i++) if (_wheels[i] != null) _wheels[i].localRotation = Quaternion.Euler(_wheelSpin, i < 2 ? Steer * 25f * (LocalDriving ? 1f : 0f) : 0f, 0);
            _steerVis = Mathf.Lerp(_steerVis, LocalDriving ? Steer : 0f, Time.deltaTime * 8f);
            if (_steerWheel != null) _steerWheel.localRotation = Quaternion.Euler(WheelTilt, 0, -_steerVis * 120f);
            for (int i = 0; i < 2; i++)
            {
                if (_doors[i] == null) continue;
                _doorOpen[i] = Mathf.MoveTowards(_doorOpen[i], Time.time < _doorUntil[i] ? 1f : 0f, Time.deltaTime * 3f);
                _doors[i].localRotation = Quaternion.Euler(0, (i == 0 ? 1f : -1f) * 70f * Mathf.SmoothStep(0, 1, _doorOpen[i]), 0);
            }
            if (_motor != null)
            {
                bool on = Busy;
                float want = on ? (0.12f + Mathf.Abs(speed) / MaxSpeed * 0.25f) * Sfx.Effects : 0f;
                _motor.volume = Mathf.MoveTowards(_motor.volume, want, Time.deltaTime);
                _motor.pitch = 0.8f + Mathf.Abs(speed) / MaxSpeed * 0.9f;
                if (_motor.volume > 0.001f && !_motor.isPlaying) _motor.Play(); else if (_motor.volume <= 0.001f && _motor.isPlaying) _motor.Stop();
            }
        }

        /// <summary>Run someone over: damage from how fast the cart hits them (only the driver's game decides).</summary>
        void OnCollisionEnter(Collision c)
        {
            if (!LocalDriving) return;
            var zone = c.collider.GetComponent<HitZone>() ?? c.collider.GetComponentInParent<HitZone>();
            if (zone == null || zone.owner == null) return;
            var pl = RagoGame.I.Player;
            if (pl != null && zone.owner == (IDamageable)pl.Vitals) return;   // not yourself
            float speed = c.relativeVelocity.magnitude;
            if (speed < 3.5f) return;
            if (_hitCooldown.TryGetValue(zone.owner, out var t) && Time.time - t < 1f) return;
            _hitCooldown[zone.owner] = Time.time;
            Vector3 dir = rb.velocity.sqrMagnitude > 0.1f ? rb.velocity.normalized : transform.forward;
            var info = new HitInfo
            {
                damage = Mathf.Min(90f, (speed - 2.5f) * 11f), part = BodyPart.Thorax, point = c.GetContact(0).point, dir = dir, normal = -dir,
                attacker = 0, gunId = "cart", force = Mathf.Clamp(speed * 1.2f, 2f, 12f)
            };
            Fx.Blood(info.point, dir, -dir, c.collider.transform, info.damage);
            Sfx.Play(Synth.BodyFall(), info.point, 0.9f, 0.8f, 2f, 30f);
            zone.owner.TakeHit(info, zone);
            Ballistics.OnHitActor?.Invoke(info, zone);
            Sfx.UI(Synth.HitMarker(), 0.35f);
        }

        // ------------------------------------------------------------------------------------------- multiplayer
        public void NetState(ulong from, Vector3 pos, Quaternion rot, bool driving)
        {
            if (LocalDriving && driving) { if (from < (NetHub.Backend != null ? NetHub.Backend.LocalId : 0)) RagoGame.I.Player.LeaveCart(); else return; }   // both got in at once: the lower id keeps it
            if (driving) { RemoteDriver = from; _remoteT = Time.time; _netPos = pos; _netRot = rot; if (rb != null && !rb.isKinematic) { rb.isKinematic = true; } }
            else
            {
                RemoteDriver = 0;
                if (rb != null) { rb.isKinematic = false; rb.position = pos; rb.rotation = rot; transform.SetPositionAndRotation(pos, rot); rb.velocity = Vector3.zero; }
            }
        }
    }
}
