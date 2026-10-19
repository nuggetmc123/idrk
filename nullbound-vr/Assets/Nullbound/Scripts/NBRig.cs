using UnityEngine;

namespace Nullbound
{
    /// The local VR player: headset camera, two gloves, a CharacterController body that follows
    /// your real head around the play space, stick movement, turning, jumping and knockback.
    public class NBRig : MonoBehaviour
    {
        public static NBRig Instance { get; private set; }

        public Transform Space { get; private set; }
        public Camera Cam { get; private set; }
        public Transform Head => Cam.transform;
        public NBHand LeftHand { get; private set; }
        public NBHand RightHand { get; private set; }
        public CharacterController Body { get; private set; }

        public bool Grounded => Body.isGrounded;
        public Vector3 Foot => transform.position;
        public Vector3 Forward { get { var f = Head.forward; f.y = 0; return f.sqrMagnitude < 1e-4f ? transform.forward : f.normalized; } }
        public bool Stunned => Time.time < m_StunUntil;
        public float StunLeft => Mathf.Max(0, m_StunUntil - Time.time);
        public float SpeedMul = 1f;
        public bool LocomotionLocked;   // menus etc.
        public bool Frozen;             // KO'd: no movement at all until respawn

        Vector3 m_Vel;       // vertical + jump
        Vector3 m_Ext;       // knockback
        Vector3 m_DashVel;
        float m_DashUntil, m_StunUntil;
        bool m_SnapReady = true;
        Renderer m_Vignette;
        float m_VigAmount;

        public const float Gravity = -22f;
        public const float BaseSpeed = 6.2f;
        public const float JumpSpeed = 7.2f;

        public static NBRig Create(Vector3 pos)
        {
            var g = new GameObject("NB_Rig");
            g.transform.position = pos;
            var rig = g.AddComponent<NBRig>();
            rig.Build();
            return rig;
        }

        void Build()
        {
            Instance = this;
            gameObject.layer = 2; // Ignore Raycast: shots and aim rays pass through bodies
            Body = gameObject.AddComponent<CharacterController>();
            Body.height = 1.7f; Body.radius = 0.28f; Body.center = new Vector3(0, 0.85f, 0);
            Body.stepOffset = 0.4f; Body.slopeLimit = 50f; Body.skinWidth = 0.04f;

            Space = new GameObject("TrackingSpace").transform;
            Space.SetParent(transform, false);

            var camGo = new GameObject("Head");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(Space, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.05f; Cam.farClipPlane = 400f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.09f, 0.06f, 0.16f);
            camGo.AddComponent<AudioListener>();

            LeftHand = new GameObject("LeftHand").AddComponent<NBHand>();
            LeftHand.transform.SetParent(Space, false);
            LeftHand.Init(Space, false);
            RightHand = new GameObject("RightHand").AddComponent<NBHand>();
            RightHand.transform.SetParent(Space, false);
            RightHand.Init(Space, true);

            BuildVignette();
            ApplyHeight();
        }

        void BuildVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01((r - 0.45f) / 0.35f) * 255);
                    px[y * n + x] = new Color32(0, 0, 0, a);
                }
            tex.SetPixels32(px); tex.Apply();
            var m = NBMat.Glow(Color.white);
            m.mainTexture = tex;
            m.renderQueue = 4000;
            var q = NBMat.Prim(PrimitiveType.Quad, Head, new Vector3(0, 0, 0.12f), new Vector3(0.42f, 0.42f, 1f), m);
            q.name = "Vignette";
            m_Vignette = q.GetComponent<Renderer>();
            m_Vignette.enabled = false;
        }

        public void ApplyHeight()
        {
            var p = Space.localPosition;
            Space.localPosition = new Vector3(p.x, NBSettings.HeightOffset, p.z);
        }

        void OnEnable() { Application.onBeforeRender += OnBeforeRender; }
        void OnDisable() { Application.onBeforeRender -= OnBeforeRender; }

        void OnBeforeRender()
        {
            NBInput.PollPosesOnly();
            ApplyPoses();
        }

        void ApplyPoses()
        {
            Head.localPosition = NBInput.HeadPos;
            Head.localRotation = NBInput.HeadRot;
            LeftHand.SetPose(NBInput.Left.pos, NBInput.Left.rot);
            RightHand.SetPose(NBInput.Right.pos, NBInput.Right.rot);
        }

        void Update()
        {
            NBInput.Poll();
            ApplyPoses();
            FollowHead();
            Turn();
            Locomotion();
            LeftHand.Sample(Time.deltaTime);
            RightHand.Sample(Time.deltaTime);
            UpdateVignette();
        }

        /// Keep the collision capsule under your real head as you walk around your room.
        void FollowHead()
        {
            var d = Head.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 1e-6f) return;
            var before = transform.position;
            Body.Move(d);
            var moved = transform.position - before;
            moved.y = 0;
            Space.position -= moved;
        }

        void Turn()
        {
            if (LocomotionLocked) return;
            float yaw = 0f;
            if (!NBInput.XRActive) yaw = NBInput.DesktopYawDelta;
            else
            {
                float x = NBInput.TurnAxis.x;
                if (NBSettings.SnapTurn)
                {
                    if (m_SnapReady && Mathf.Abs(x) > 0.7f) { yaw = Mathf.Sign(x) * NBSettings.SnapAngle; m_SnapReady = false; }
                    if (Mathf.Abs(x) < 0.3f) m_SnapReady = true;
                }
                else if (Mathf.Abs(x) > 0.15f)
                {
                    yaw = x * NBSettings.TurnSpeed * Time.deltaTime;
                    m_VigAmount = Mathf.Max(m_VigAmount, 0.6f);
                }
            }
            if (yaw != 0f) transform.RotateAround(Head.position, Vector3.up, yaw);
        }

        void Locomotion()
        {
            float dt = Time.deltaTime;
            if (Frozen) { m_Vel = Vector3.zero; m_Ext = Vector3.zero; m_DashUntil = 0; return; }
            var move = Vector3.zero;
            if (!LocomotionLocked && !Stunned)
            {
                var a = NBInput.MoveAxis;
                if (a.sqrMagnitude > 0.02f)
                {
                    var f = Forward; var r = new Vector3(f.z, 0, -f.x);
                    move = (f * a.y + r * a.x) * BaseSpeed * SpeedMul;
                    if (move.sqrMagnitude > BaseSpeed * BaseSpeed * SpeedMul * SpeedMul) move = move.normalized * BaseSpeed * SpeedMul;
                    m_VigAmount = Mathf.Max(m_VigAmount, a.magnitude * 0.7f);
                }
            }

            if (Body.isGrounded && m_Vel.y < 0) m_Vel.y = -2f;
            m_Vel.y += Gravity * dt;

            var total = move + m_Ext + new Vector3(0, m_Vel.y, 0);
            if (Time.time < m_DashUntil) { total = m_DashVel + new Vector3(0, Mathf.Max(0, m_Vel.y), 0); m_VigAmount = 1f; }

            var flags = Body.Move(total * dt);
            if ((flags & CollisionFlags.Above) != 0 && m_Vel.y > 0) m_Vel.y = 0;

            float decel = Body.isGrounded ? 16f : 5f;
            m_Ext = Vector3.MoveTowards(m_Ext, Vector3.zero, decel * dt);
            if (m_Ext.sqrMagnitude > 1f) m_VigAmount = Mathf.Max(m_VigAmount, 0.8f);
        }

        void UpdateVignette()
        {
            bool on = NBSettings.Vignette && NBInput.XRActive && m_VigAmount > 0.05f;
            m_Vignette.enabled = on;
            if (on)
            {
                float s = Mathf.Lerp(0.9f, 0.42f, m_VigAmount);
                m_Vignette.transform.localScale = new Vector3(s, s, 1f);
            }
            m_VigAmount = Mathf.MoveTowards(m_VigAmount, 0f, Time.deltaTime * 3f);
        }

        // ---------------- actions used by combat ----------------

        public bool TryJump()
        {
            if (!Body.isGrounded || Stunned || LocomotionLocked) return false;
            m_Vel.y = JumpSpeed;
            return true;
        }

        public void Dash(Vector3 dir, float distance, float time)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-4f) dir = Forward;
            m_DashVel = dir.normalized * (distance / Mathf.Max(0.05f, time));
            m_DashUntil = Time.time + time;
        }

        public void Knock(Vector3 velocity, float stun)
        {
            float scale = NBSettings.ReducedKnockback ? 0.5f : 1f;
            m_Ext = new Vector3(velocity.x, 0, velocity.z) * scale;
            if (velocity.y != 0) m_Vel.y = velocity.y * (NBSettings.ReducedKnockback ? 0.6f : 1f);
            m_DashUntil = 0;
            if (stun > 0) m_StunUntil = Mathf.Max(m_StunUntil, Time.time + stun);
        }

        public void Impulse(Vector3 velocity)
        {
            m_Ext += new Vector3(velocity.x, 0, velocity.z);
            if (velocity.y > 0) m_Vel.y = Mathf.Max(m_Vel.y, velocity.y);
            if (velocity.y < 0) m_Vel.y = velocity.y;
        }

        public void ClearStun() { m_StunUntil = 0; m_Ext = Vector3.zero; }

        public void Teleport(Vector3 foot, Vector3? face = null)
        {
            Body.enabled = false;
            var off = Head.position - transform.position; off.y = 0;
            transform.position = foot - off;
            Body.enabled = true;
            m_Vel = Vector3.zero; m_Ext = Vector3.zero; m_DashUntil = 0;
            if (face.HasValue)
            {
                var f = face.Value; f.y = 0;
                if (f.sqrMagnitude > 1e-4f)
                {
                    float delta = Vector3.SignedAngle(Forward, f, Vector3.up);
                    transform.RotateAround(Head.position, Vector3.up, delta);
                }
            }
            LeftHand.ResetVelocity(); RightHand.ResetVelocity();
        }
    }
}
