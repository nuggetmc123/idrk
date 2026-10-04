using UnityEngine;

/// <summary>
/// Drivable survival-style boat (Rust / Stranded Deep feel).
///
/// Setup:
///  1. Drop Boat.fbx in the scene. Add a Rigidbody (mass ~350) and this script to the
///     top object (the prefab root that holds Boat, Motor and Propeller).
///  2. Collision is added automatically (convex MeshCollider on "Boat"). For a cheaper one,
///     drop Boat_Collider.fbx in as a child instead; its renderer is hidden at runtime.
///  3. Set waterLevel to your ocean height (or override GetWaterHeight for waves).
///     Seat, exit and float points are created automatically if you leave them empty.
///
/// Controls: E enter/exit (when near), W/S throttle, A/D steer, Space = stop engine.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BoatController : MonoBehaviour
{
    [Header("Parts (auto-found by name)")]
    public Transform motorPivot;      // "Motor"     - origin is on the steering axis
    public Transform propeller;       // "Propeller" - origin is on the shaft
    public Transform driverSeat;      // auto-created at the rear bench
    public Transform exitPoint;       // auto-created on the left side
    public Transform[] floatPoints;   // auto-created under the hull

    [Header("Water")]
    public float waterLevel = 0f;
    public float floatStrength = 40f;    // buoyancy accel at 1 m depth (must be > gravity)
    public float floatDamping = 1.5f;
    public float waterDrag = 1.2f;       // linear drag while floating
    public float waterAngularDrag = 2.5f;

    [Header("Engine")]
    public float maxThrust = 3200f;      // newtons at full throttle
    public float reverseFactor = 0.4f;
    public float throttleRate = 0.8f;    // how fast the throttle lever moves
    public float turnTorque = 900f;
    public float maxMotorAngle = 35f;
    public float sideGrip = 2.5f;        // kills sideways sliding like a keel
    public float propRpm = 1800f;

    [Header("Driver")]
    public Transform player;             // your player root (optional)
    public float enterDistance = 3f;
    public KeyCode useKey = KeyCode.E;

    public bool Driving { get; private set; }
    public float Throttle { get; private set; }  // -1..1

    Rigidbody rb;
    Quaternion motorRest;
    float steer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb.mass < 10f) rb.mass = 350f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = new Vector3(0f, -0.15f, 0f);

        motorPivot = motorPivot ? motorPivot : Find("Motor");
        propeller  = propeller  ? propeller  : Find("Propeller");
        // The FBX keeps every part top-level so nothing gets offset on import;
        // hook the propeller onto the motor here so it swings with the steering.
        if (motorPivot && propeller && propeller.parent != motorPivot)
            propeller.SetParent(motorPivot, true);

        // Points in boat space (metres, +Z = bow).
        driverSeat = driverSeat ? driverSeat : Point("Seat_Driver", new Vector3(0f, 0.25f, -1.65f));
        exitPoint  = exitPoint  ? exitPoint  : Point("Exit_Left", new Vector3(-1.4f, 0.4f, 0f));
        if (floatPoints == null || floatPoints.Length == 0)
            floatPoints = new[]
            {
                Point("Float_FL", new Vector3(-0.55f, -0.25f,  1.21f)),
                Point("Float_FR", new Vector3( 0.55f, -0.25f,  1.21f)),
                Point("Float_RL", new Vector3(-0.60f, -0.25f, -1.84f)),
                Point("Float_RR", new Vector3( 0.60f, -0.25f, -1.84f)),
                Point("Float_Center", new Vector3(0f, -0.25f, -0.38f)),
            };
        if (motorPivot) motorRest = motorPivot.localRotation;

        var col = Find("Boat_Collider");
        if (!col) col = Find("Boat");
        if (col)
        {
            if (col.name == "Boat_Collider" && col.TryGetComponent(out MeshRenderer mr)) mr.enabled = false;
            if (!col.GetComponent<Collider>()) col.gameObject.AddComponent<MeshCollider>().convex = true;
        }
    }

    Transform Point(string n, Vector3 localPos)
    {
        var t = new GameObject(n).transform;
        t.SetParent(transform, false);
        t.localPosition = localPos;
        return t;
    }

    Transform Find(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    /// Override / replace this to sample your wave system.
    protected virtual float GetWaterHeight(Vector3 worldPos) => waterLevel;

    void Update()
    {
        if (player && Input.GetKeyDown(useKey))
        {
            if (Driving) Exit();
            else if (Vector3.Distance(player.position, transform.position) < enterDistance) Enter();
        }

        float targetSteer = 0f;
        if (Driving)
        {
            float v = Input.GetAxisRaw("Vertical");
            Throttle = Mathf.Clamp(Throttle + v * throttleRate * Time.deltaTime, -1f, 1f);
            if (Input.GetKeyDown(KeyCode.Space)) Throttle = 0f;
            targetSteer = Input.GetAxisRaw("Horizontal");
        }
        else
        {
            Throttle = Mathf.MoveTowards(Throttle, 0f, Time.deltaTime);
        }
        steer = Mathf.MoveTowards(steer, targetSteer, Time.deltaTime * 3f);

        // Visuals: swing the outboard (tiller steering = motor turns opposite the bow)
        if (motorPivot)
        {
            Vector3 upLocal = motorPivot.parent.InverseTransformDirection(transform.up);
            motorPivot.localRotation = Quaternion.AngleAxis(-steer * maxMotorAngle, upLocal) * motorRest;
        }
        if (propeller)
            propeller.Rotate(transform.forward, Throttle * propRpm * 6f * Time.deltaTime, Space.World);
    }

    void FixedUpdate()
    {
        int submerged = 0;
        float perPoint = rb.mass * floatStrength / Mathf.Max(1, floatPoints.Length);
        foreach (var p in floatPoints)
        {
            Vector3 pos = p.position;
            float depth = GetWaterHeight(pos) - pos.y;
            if (depth <= 0f) continue;
            submerged++;
            Vector3 vel = rb.GetPointVelocity(pos);
            float force = perPoint * Mathf.Clamp(depth, 0f, 1f) - vel.y * floatDamping * rb.mass / floatPoints.Length;
            rb.AddForceAtPosition(Vector3.up * Mathf.Max(0f, force), pos, ForceMode.Force);
        }

        bool inWater = submerged > 0;
        rb.linearDamping = inWater ? waterDrag : 0.05f;   // use rb.drag on Unity < 6
        rb.angularDamping = inWater ? waterAngularDrag : 0.05f;
        if (!inWater) return;

        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 propPos = propeller ? propeller.position : transform.position - transform.forward * 2.3f;

        // Thrust is only applied if the prop is actually under water
        if (GetWaterHeight(propPos) > propPos.y)
        {
            float thrust = Throttle * maxThrust * (Throttle < 0 ? reverseFactor : 1f);
            rb.AddForce(fwd * thrust);
            float speed = Vector3.Dot(rb.linearVelocity, fwd);
            float turnFactor = Mathf.Clamp01(Mathf.Abs(speed) / 3f + Mathf.Abs(Throttle) * 0.5f);
            rb.AddTorque(Vector3.up * steer * turnTorque * turnFactor * Mathf.Sign(speed + 0.01f));
            // slight bow lift with speed
            rb.AddTorque(-transform.right * Mathf.Max(0f, speed) * 25f);
        }

        // keel: resist sideways motion
        Vector3 side = Vector3.Project(rb.linearVelocity, transform.right);
        rb.AddForce(-side * sideGrip * rb.mass * Time.fixedDeltaTime * 10f);
    }

    public void Enter()
    {
        Driving = true;
        if (!player || !driverSeat) return;
        if (player.TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (player.TryGetComponent(out Rigidbody prb)) prb.isKinematic = true;
        player.SetParent(driverSeat, false);
        player.localPosition = Vector3.zero;
        player.localRotation = Quaternion.identity;
    }

    public void Exit()
    {
        Driving = false;
        if (!player) return;
        player.SetParent(null, true);
        if (exitPoint) player.position = exitPoint.position;
        player.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (player.TryGetComponent(out CharacterController cc)) cc.enabled = true;
        if (player.TryGetComponent(out Rigidbody prb)) prb.isKinematic = false;
    }
}
