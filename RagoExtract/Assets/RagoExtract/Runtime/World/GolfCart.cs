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
    public partial class GolfCart : MonoBehaviour
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

        /// <summary>A motorcycle with a sidecar (and a machine gun on the sidecar) instead of a golf cart.</summary>
        public bool Bike;
        public Turret Turret;
        float MaxSpeed = 15f, MaxReverse = 5f;   // m/s (~54 km/h forward; the bike does 20)
        Vector3[] Corners = { new Vector3(-0.55f, 0.45f, 0.82f), new Vector3(0.55f, 0.45f, 0.82f), new Vector3(-0.55f, 0.45f, -0.82f), new Vector3(0.55f, 0.45f, -0.82f) };
        float _wheelR = 0.22f;
        /// <summary>How far your hands sit from the middle of the wheel / bars, and how far they turn it for full lock.</summary>
        public float WheelR => Bike ? 0.33f : 0.17f;
        /// <summary>The middle of the handlebar where the grips are (they sit a little up and back from the head stock).</summary>
        Vector3 BarCentre => WheelGrab.TransformPoint(new Vector3(0, 0.075f, -0.1f));
        public float SteerLock => Bike ? 30f : 60f;   // less turning of the wheel / bars for full lock: easier to steer
        /// <summary>Which way you step out (driver to the left; on the bike the sidecar passenger steps out further right).</summary>
        public float ExitSide(bool passenger) => passenger ? (Bike ? 1.55f : 1.3f) : (Bike ? -1.2f : -1.3f);
        const float SuspLen = 0.62f, Rest = 0.45f;

        // remote driving (multiplayer)
        public ulong RemoteDriver;
        float _remoteT;
        Vector3 _netPos, _netVel, _netAng; Quaternion _netRot; float _netSentT, _netClock = float.MaxValue, _netBase;
        float _sendT;
        readonly Dictionary<object, float> _hitCooldown = new Dictionary<object, float>();
        float _wheelSpin, _steerVis;

        public float Throttle, Steer, Brake;   // set by the driver each frame (throttle < 0 = reverse)
        /// <summary>The steering wheel's centre and its plane (for turning it by hand).</summary>
        public void WheelFrame(out Vector3 center, out Vector3 right, out Vector3 up)
        {
            var q = transform.rotation * (Bike ? Quaternion.Euler(90f, 0, 0) : Quaternion.Euler(WheelTilt, 0, 0));   // handlebars turn flat, about the head stock
            center = WheelGrab == null ? transform.position : Bike ? BarCentre : WheelGrab.position;
            right = q * Vector3.right; up = q * Vector3.up;
        }
        public float ForwardSpeed => rb != null ? Vector3.Dot(rb.Vel(), transform.forward) : 0f;
        public bool LocalDriving => RagoGame.I != null && RagoGame.I.Player != null && RagoGame.I.Player.Driving == this && !RagoGame.I.Player.Passenger;
        public bool LocalInside => RagoGame.I != null && RagoGame.I.Player != null && RagoGame.I.Player.Driving == this;
        public bool Busy => LocalDriving || (RemoteDriver != 0 && Time.time - _remoteT < 1.5f);

        // ------------------------------------------------------------------------------------------- spawning
        /// <summary>A cart parked beside every player spawn on the outdoor raid maps (the same ones on every machine).</summary>
        public static void SpawnFor(MapBase map)
        {
            Clear();
            if (map == null || !map.IsRaid || map.Id == "underground") return;   // never in the Underground
            // parked out in the map - by the buildings and camps where raiders hang about - never near a player spawn.
            // The same spots on every machine (same map, same order), so multiplayer agrees which cart is which.
            var rng = new Rng(map.Seed * 31 + 7);
            var spots = new List<Vector3>();
            foreach (var ns in map.NpcSpawns)
            {
                if (spots.Count >= 8) break;
                bool nearSpawn = false;
                foreach (var ps in map.PlayerSpawns) if ((ps.position - ns.pos).sqrMagnitude < 90f * 90f) nearSpawn = true;
                if (nearSpawn) continue;
                bool crowded = false;
                foreach (var q in spots) if ((q - ns.pos).sqrMagnitude < 70f * 70f) crowded = true;
                if (crowded) continue;
                // a clear patch of ground 10-18 m from the spot (not inside a building)
                float a0 = rng.Range(0f, 360f);
                for (int k = 0; k < 8; k++)
                {
                    Vector3 off = Quaternion.Euler(0, a0 + k * 45f, 0) * Vector3.forward * (k % 2 == 0 ? 12f : 17f);
                    Vector3 at = ns.pos + off;
                    if (!Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out var h, 140f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;
                    if (h.normal.y < 0.9f) continue;   // too steep
                    Vector3 p = h.point;
                    if (Physics.CheckBox(p + Vector3.up * 1.1f, new Vector3(0.9f, 0.85f, 1.5f), Quaternion.identity, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;   // something in the way
                    Create(spots.Count, p + Vector3.up * 0.5f, Quaternion.Euler(0, rng.Range(0f, 360f), 0), map.transform, spots.Count % 3 == 1);   // every third one's a sidecar bike
                    spots.Add(ns.pos);
                    break;
                }
            }
            Debug.Log("[Rago] " + spots.Count + " vehicles parked on " + map.Id);
        }

        public static void Clear()
        {
            foreach (var c in new List<GolfCart>(All)) if (c != null) Destroy(c.gameObject);
            All.Clear();
        }

        public static GolfCart Find(int id) { foreach (var c in All) if (c != null && c.Id == id) return c; return null; }

        static int _spawnN;
        /// <summary>Tester: a new cart in front of you (and on everyone else's screen in multiplayer).</summary>
        public static GolfCart SpawnHere(Vector3 pos, float yaw, bool bike = false)
        {
            var map = RagoGame.I != null ? RagoGame.I.Map : null;
            if (map != null && map.Id == "underground") { RagoGame.I.Player?.Toast("No golf carts in the Underground"); return null; }
            ulong me = NetGame.Live && NetHub.Backend != null ? NetHub.Backend.LocalId : 0;
            int id = 1000 + (int)(me % 200) * 100 + (_spawnN++ % 100);
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var h, 20f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) pos = h.point;
            var parent = RagoGame.I != null && RagoGame.I.Map != null ? RagoGame.I.Map.transform : null;
            var c = Create(id, pos + Vector3.up * 0.5f, Quaternion.Euler(0, yaw, 0), parent, bike);
            if (NetGame.Live) NetGame.I.CartSpawned(id, c.transform.position, yaw, bike);
            return c;
        }

        public static GolfCart Create(int id, Vector3 pos, Quaternion rot, Transform parent, bool bike = false)
        {
            var old = Find(id); if (old != null) Destroy(old.gameObject);
            var go = new GameObject((bike ? "SidecarBike_" : "GolfCart_") + id);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            var c = go.AddComponent<GolfCart>();
            c.Id = id; c.Bike = bike;
            if (bike) c.BuildBike(); else c.Build();
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
                // fills the side opening exactly: from the back of the front cowl (z 0.77) to the front of the rear body
                // (z -0.6), floor pan (y 0.48) to the cowl's top (y 0.88), flush with the body's side (x 0.6)
                d.localPosition = new Vector3(side * 0.583f, 0.68f, 0.765f);   // hinged at its front edge, against the cowl
                var dm = new MB(2);
                dm.Sub(0).Box(new Vector3(0, 0, -0.68f), new Vector3(0.035f, 0.4f, 1.36f), paint, 0.012f);
                dm.Sub(1).Box(new Vector3(side * 0.03f, 0.08f, -1.2f), new Vector3(0.035f, 0.04f, 0.14f), dark, 0.01f);   // handle, at the back edge
                dm.Build("Door", d, paintMat, darkMat);
                var h = new GameObject("Handle").transform; h.SetParent(d, false); h.localPosition = new Vector3(side * 0.06f, 0.08f, -1.2f);
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
            rb.mass = 350f; rb.SetDamping(0.05f, 0.6f);   // light angular drag: it pitches over crests and flies off ramps
            rb.centerOfMass = new Vector3(0, 0.15f, 0);   // low: hard to tip over in a turn
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _motor = gameObject.AddComponent<AudioSource>();
            _motor.clip = Synth.CartMotor(); _motor.loop = true; _motor.spatialBlend = 1f; _motor.minDistance = 2f; _motor.maxDistance = 30f; _motor.volume = 0f;
        }

        void OnDestroy() { All.Remove(this); if (LocalInside) RagoGame.I.Player.LeaveCart(); }

        // ------------------------------------------------------------------------------------------- driving
        public bool WheelNear(Vector3 p, float r = 0.3f)
        {
            if (WheelGrab == null) return false;
            if (!Bike) return (WheelGrab.position - p).sqrMagnitude < r * r;
            // handlebars: anywhere along the bar (the grips are out at the ends)
            // handlebars: anywhere along the bar, and a good handful round each grip (the right one is the throttle)
            Vector3 a = BarCentre - transform.right * WheelR, b = BarCentre + transform.right * WheelR;
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            if ((a + ab * t - p).sqrMagnitude < r * r * 0.5f) return true;
            return (a - p).sqrMagnitude < 0.18f * 0.18f || (b - p).sqrMagnitude < 0.18f * 0.18f;
        }
        /// <summary>Which door's handle a hand is on: 0 driver's, 1 passenger's, -1 neither.</summary>
        public int DoorNear(Vector3 p, float r = 0.3f)
        {
            for (int i = 0; i < 2; i++) if (_handles[i] != null && (_handles[i].position - p).sqrMagnitude < r * r) return i;
            return -1;
        }
        /// <summary>Swing a door open for a moment (getting in or out).</summary>
        public void SwingDoor(int i) { if (i < 0 || i > 1) return; _doorUntil[i] = Time.time + 0.9f; Sfx.Play(Synth.Footstep(Synth.Surface.Metal, 3, false), _handles[i].position, 0.35f, 1.2f, 1f, 12f); }

        // ---- the people sitting in it never push it. The driver's (and passengers') body, hands and holstered gear sit
        // inside the cart's collider; physics kept shoving the cart out from under them - it drove off on its own.
        readonly List<Collider> _mine = new List<Collider>();
        readonly HashSet<Collider> _ignored = new HashSet<Collider>();
        float _ignoreT;
        bool _holding;   // frozen in place while someone sits in it without pressing the gas
        void IgnoreRiders()
        {
            _ignoreT -= Time.fixedDeltaTime;
            if (_ignoreT > 0f) return;
            _ignoreT = 0.5f;
            if (_mine.Count == 0) GetComponents(_mine);
            var riders = new List<Collider>();
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && pl.Driving == this)
            {
                // everything that's you: the rig, your visible body and your hands are separate objects, and so is
                // whatever you're holding - the physics hands on the wheel were shoving the cart about
                riders.AddRange(pl.GetComponentsInChildren<Collider>(true));
                if (pl.Body != null) riders.AddRange(pl.Body.GetComponentsInChildren<Collider>(true));
                foreach (var h in new[] { pl.HandL, pl.HandR })
                {
                    if (h == null) continue;
                    riders.AddRange(h.GetComponentsInChildren<Collider>(true));
                    if (h.Held != null) riders.AddRange(h.Held.GetComponentsInChildren<Collider>(true));
                }
            }
            // other players sitting in a seat (they ride it on their own screen)
            if (NetGame.I != null)
                foreach (var kv in NetGame.I.Remotes)
                {
                    var rp = kv.Value; if (rp == null) continue;
                    Vector3 rr = rp.transform.position;
                    bool seated = (rr - Seat.position).sqrMagnitude < 0.9f * 0.9f || (rr - PassSeat.position).sqrMagnitude < 0.9f * 0.9f || (rp.HeadPos - Seat.position).sqrMagnitude < 1.1f || (rp.HeadPos - PassSeat.position).sqrMagnitude < 1.1f;
                    if (seated) riders.AddRange(rp.GetComponentsInChildren<Collider>(true));
                }
            var now = new HashSet<Collider>(riders);
            foreach (var c in riders) if (c != null && !_ignored.Contains(c)) { foreach (var m in _mine) if (m != null) Physics.IgnoreCollision(m, c, true); _ignored.Add(c); }
            // riders who got out can be hit again (so they can still be run over later)
            var gone = new List<Collider>();
            foreach (var c in _ignored) if (c == null || !now.Contains(c)) gone.Add(c);
            foreach (var c in gone) { if (c != null) foreach (var m in _mine) if (m != null) Physics.IgnoreCollision(m, c, false); _ignored.Remove(c); }
        }

        public void RidersChanged() { _ignoreT = 0f; IgnoreRiders(); }

        void FixedUpdate()
        {
            if (rb == null) return;
            IgnoreRiders();
            bool remote = RemoteDriver != 0 && Time.time - _remoteT < 1.5f;
            if (remote)
            {
                // someone else is driving it: show it where their game says it is
                if (!rb.isKinematic) rb.isKinematic = true;
                // where it is right now: the last update moved on by its speed (it used to stop at each update and wait for the
                // next, 12 times a second - that's the stutter), then eased onto
                float age = Mathf.Clamp(Time.time - _remoteT + RemoteLag, 0f, 0.3f);
                Vector3 tp = _netPos + _netVel * age;
                Quaternion tr = _netAng.sqrMagnitude > 1e-4f ? Quaternion.AngleAxis(_netAng.magnitude * Mathf.Rad2Deg * age, _netAng.normalized) * _netRot : _netRot;
                if ((rb.position - tp).sqrMagnitude > 16f) { rb.position = tp; rb.rotation = tr; }
                rb.MovePosition(Vector3.Lerp(rb.position, tp, 0.4f));
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, tr, 0.4f));
                return;
            }
            // someone's sitting in it and nobody's pressing the gas: it doesn't move at all (frozen in place) - it used to
            // drift and turn on its own while you sat there
            bool inside = LocalInside;
            bool input = LocalDriving && (Mathf.Abs(Throttle) > 0.02f || Brake > 0.02f);
            // (only once it sits level: pushed down on one side before you got in, it used to freeze tilted like that and
            // then drive along leaning over)
            bool level = Vector3.Angle(transform.up, Vector3.up) < 4f || Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var gh, 3f, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && Vector3.Angle(transform.up, gh.normal) < 4f;
            if (inside && !input && level && rb.Vel().sqrMagnitude < 0.5f * 0.5f && rb.angularVelocity.sqrMagnitude < 0.3f * 0.3f)
            {
                if (!rb.isKinematic) { rb.SetVel(Vector3.zero); rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
                _holding = true;
                return;
            }
            if (rb.isKinematic) { rb.isKinematic = false; if (!_holding) RemoteDriver = 0; }
            _holding = false;
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
            Vector3 v = rb.Vel();
            float fwdSpeed = Vector3.Dot(v, transform.forward);
            float sideSpeed = Vector3.Dot(v, transform.right);
            if (grounded >= 2)
            {
                float accel;
                if (!driven) accel = Mathf.Clamp(-fwdSpeed * 3f, -6f, 6f);   // parked: handbrake on
                else
                {
                    if (thr > 0.02f) accel = Mathf.Clamp((thr * MaxSpeed - fwdSpeed) * 2.2f, -1.5f, 5f);           // gas
                    else if (thr < -0.02f) accel = Mathf.Clamp((thr * MaxReverse - fwdSpeed) * 2.2f, -2.5f, 1.5f); // reverse
                    else if (Mathf.Abs(fwdSpeed) < 1.5f) accel = Mathf.Clamp(-fwdSpeed * 4f, -6f, 6f);              // off the gas and slow: holds still (no creeping, even on a hill)
                    else accel = -Mathf.Sign(fwdSpeed) * 1.4f;                                                     // off the gas: rolls to a stop
                    if (Brake > 0.02f) accel += -Mathf.Sign(fwdSpeed) * Mathf.Min(Mathf.Abs(fwdSpeed) / Time.fixedDeltaTime * 0.5f, Brake * 7f);   // brakes
                }
                rb.AddForce(transform.forward * accel * rb.mass);
                rb.AddForce(-transform.right * Mathf.Clamp(sideSpeed * 9f, -20f, 20f) * rb.mass);   // tyres grip sideways (firmly: no sliding about)
                // steering: sharp at low speed, gentle at top speed (it used to turn just as hard flat out - twitchy)
                float yawRate = st * Mathf.Clamp(fwdSpeed / 1.5f, -1f, 1f) * Mathf.Lerp(2.3f, 0.95f, Mathf.Clamp01(Mathf.Abs(fwdSpeed) / MaxSpeed));   // turns tighter (it used to take a lot of room)
                var av = rb.angularVelocity;
                Vector3 local = transform.InverseTransformDirection(av);
                local.y = Mathf.Lerp(local.y, yawRate, 0.2f);
                local.x *= 0.93f; local.z *= 0.9f;   // damp rocking and tipping a little (hard damping glued it to the ground: it couldn't pitch up a ramp's lip)
                rb.angularVelocity = transform.TransformDirection(local);
            }
            LateFixed();
            // keep it from flipping on rough ground: it rights itself (firmly while someone's in it, so it never drives leaning)
            Vector3 upErr = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(upErr * rb.mass * (grounded >= 2 ? (inside ? 40f : 14f) : 5f));   // in the air: only a gentle nudge, so it flies and lands naturally
            // multiplayer: the driver's game says where the cart is
            if (driven && NetGame.Live)
            {
                _sendT -= dt;
                if (_sendT <= 0f) { _sendT = 1f / 15f; NetGame.I.CartState(Id, rb.position, rb.rotation, true, rb.Vel(), rb.angularVelocity); }
            }
        }

        void Update()
        {
            float speed = rb != null ? Vector3.Dot(rb.Vel(), transform.forward) : 0f;
            _wheelSpin += speed / _wheelR * Mathf.Rad2Deg * Time.deltaTime;
            _steerVis = Mathf.Lerp(_steerVis, LocalDriving ? Steer : 0f, Time.deltaTime * 8f);
            if (Bike)
            {
                // the front wheel turns with the bars; the rear and sidecar wheels just roll
                for (int i = 0; i < 3; i++) if (_wheels[i] != null) _wheels[i].localRotation = Quaternion.Euler(0, i == 0 ? _steerVis * 28f : 0f, 0) * Quaternion.Euler(_wheelSpin, 0, 0);
                if (_steerWheel != null) _steerWheel.localRotation = Quaternion.Euler(0, _steerVis * 28f, 0);
            }
            else
            {
                for (int i = 0; i < 4; i++) if (_wheels[i] != null) _wheels[i].localRotation = Quaternion.Euler(_wheelSpin, i < 2 ? Steer * 25f * (LocalDriving ? 1f : 0f) : 0f, 0);
                if (_steerWheel != null) _steerWheel.localRotation = Quaternion.Euler(WheelTilt, 0, -_steerVis * 90f);   // turns the same way as your hands (and the cart)
            }
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
        /// <summary>Getting in: the cart settles flat on its wheels (whatever was pushing one side down - a hand, a body -
        /// doesn't leave it leaning for the whole drive).</summary>
        public void Settle()
        {
            if (rb == null || RemoteDriver != 0) return;
            Vector3 f = transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            var up = Quaternion.LookRotation(f.normalized, Vector3.up);
            if (Vector3.Angle(transform.up, Vector3.up) > 25f) return;   // on a real slope / tipped right over: leave that to physics
            bool kin = rb.isKinematic;
            rb.isKinematic = false;
            rb.angularVelocity = Vector3.zero;
            // match the ground under it (a hill keeps its slope), else dead level
            Quaternion want = up;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var h, 3f, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && h.collider.attachedRigidbody != rb && h.normal.y > 0.85f)
                want = Quaternion.FromToRotation(Vector3.up, h.normal) * up;
            rb.rotation = want; transform.rotation = want;
            rb.isKinematic = kin;
        }

        void OnCollisionEnter(Collision c)
        {
            if (!LocalDriving) return;
            var zone = c.collider.GetComponent<HitZone>() ?? c.collider.GetComponentInParent<HitZone>();
            if (zone == null || zone.owner == null) return;
            var pl = RagoGame.I.Player;
            if (pl != null && zone.owner == (IDamageable)pl.Vitals) return;   // not yourself
            float speed = c.relativeVelocity.magnitude;
            if (speed < 3.5f) return;
            // you drive straight through: they go flying (ragdoll), the cart keeps its speed instead of stopping dead
            PassThrough(zone.owner as Component);
            rb.SetVel(_lastVel * 0.9f); rb.angularVelocity = _lastAng;
            if (_hitCooldown.TryGetValue(zone.owner, out var t) && Time.time - t < 1f) return;
            _hitCooldown[zone.owner] = Time.time;
            Vector3 dir = rb.Vel().sqrMagnitude > 0.1f ? rb.Vel().normalized : transform.forward;
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

        Vector3 _lastVel, _lastAng;
        void LateFixed() { if (rb != null && !rb.isKinematic) { _lastVel = rb.Vel(); _lastAng = rb.angularVelocity; } }

        /// <summary>Stop the cart bumping into this body for a few seconds (so it drives on through).</summary>
        void PassThrough(Component who)
        {
            if (who == null) return;
            var theirs = who.GetComponentsInChildren<Collider>(true);
            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var a in mine) foreach (var b in theirs) if (a != null && b != null) Physics.IgnoreCollision(a, b, true);
            StartCoroutine(Restore(mine, theirs));
        }
        System.Collections.IEnumerator Restore(Collider[] mine, Collider[] theirs)
        {
            yield return new WaitForSeconds(3f);
            foreach (var a in mine) foreach (var b in theirs) if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
        }

        // ------------------------------------------------------------------------------------------- multiplayer
        /// <summary>How old the driver's last update already was when it got here (one-way delay, from their clock).</summary>
        float RemoteLag => _netClock == float.MaxValue ? 0.05f : Mathf.Clamp(_netClock, 0f, 0.25f);

        public void NetState(ulong from, Vector3 pos, Quaternion rot, bool driving, Vector3 vel = default, Vector3 ang = default, float sentT = -1f)
        {
            if (driving)
            {
                _netVel = vel; _netAng = ang;
                if (sentT >= 0f)
                {
                    // the line's delay, judged by the quickest updates (their clock vs ours): new drivers start fresh
                    float off = Time.time - sentT;
                    if (from != RemoteDriver || _netClock == float.MaxValue) { _netBase = off; _netClock = 0.05f; }
                    else { if (off < _netBase) _netBase = off; else _netBase += 0.002f; _netClock = Mathf.Lerp(_netClock, Mathf.Clamp(off - _netBase + 0.04f, 0f, 0.25f), 0.2f); }
                    _netSentT = sentT;
                }
            }
            else { _netVel = Vector3.zero; _netAng = Vector3.zero; }
            if (LocalDriving && driving) { if (from < (NetHub.Backend != null ? NetHub.Backend.LocalId : 0)) RagoGame.I.Player.LeaveCart(); else return; }   // both got in at once: the lower id keeps it
            if (driving) { RemoteDriver = from; _remoteT = Time.time; _netPos = pos; _netRot = rot; if (rb != null && !rb.isKinematic) { rb.isKinematic = true; } }
            else
            {
                RemoteDriver = 0;
                if (rb != null) { rb.isKinematic = false; rb.position = pos; rb.rotation = rot; transform.SetPositionAndRotation(pos, rot); rb.SetVel(Vector3.zero); }
            }
        }
    }
}
