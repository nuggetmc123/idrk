using UnityEngine;

namespace Nullbound
{
    /// Server-only brain for a bot fighter. Uses the same server combat calls as a human.
    public class NBBot : MonoBehaviour
    {
        NBPlayer m_P;
        CharacterController m_Body;
        NBPlayer m_Target;
        Vector3 m_Vel, m_Ext, m_DashVel;
        float m_DashUntil, m_StunUntil, m_NextThink, m_NextPunch, m_NextMove, m_BlockUntil, m_PunchT = -1f, m_Strafe = 1f, m_NextJump;
        bool m_PunchRight;
        float m_Yaw;
        float m_Skill;

        public void Init(NBPlayer p)
        {
            m_P = p;
            gameObject.layer = 2; // Ignore Raycast
            m_Body = gameObject.AddComponent<CharacterController>();
            m_Body.height = 1.7f; m_Body.radius = 0.28f; m_Body.center = new Vector3(0, 0.85f, 0);
            m_Body.stepOffset = 0.4f;
            m_Skill = Random.Range(0.55f, 1f);
            m_Strafe = Random.value < 0.5f ? -1f : 1f;
        }

        public void Knock(Vector3 v, float stun)
        {
            m_Ext = new Vector3(v.x, 0, v.z);
            if (v.y != 0) m_Vel.y = v.y;
            m_DashUntil = 0;
            if (stun > 0) m_StunUntil = Mathf.Max(m_StunUntil, Time.time + stun);
            // sometimes block the next hits, sometimes escape
            if (Random.value < 0.25f * m_Skill) m_BlockUntil = Time.time + Random.Range(0.4f, 1f);
        }

        public void Dash(Vector3 dir, float dist, float time)
        {
            dir.y = 0;
            m_DashVel = dir.normalized * dist / Mathf.Max(0.05f, time);
            m_DashUntil = Time.time + time;
        }

        public void Teleport(Vector3 pos, Vector3 face)
        {
            m_Body.enabled = false;
            transform.position = pos;
            m_Body.enabled = true;
            m_Vel = m_Ext = Vector3.zero;
            face.y = 0;
            if (face.sqrMagnitude > 0.01f) m_Yaw = Quaternion.LookRotation(face).eulerAngles.y;
        }

        void Update()
        {
            if (m_P == null || !m_P.IsSpawned) return;
            float dt = Time.deltaTime, now = Time.time;
            bool stunned = now < m_StunUntil;

            if (m_P.Dead.Value) { WritePose(); return; }

            if (now >= m_NextThink)
            {
                m_NextThink = now + 0.8f;
                m_Target = PickTarget();
                if (Random.value < 0.3f) m_Strafe = -m_Strafe;
            }

            var move = Vector3.zero;
            float dist = 999f;
            if (m_Target != null && m_Target.Alive)
            {
                var to = m_Target.Foot - transform.position; to.y = 0;
                dist = to.magnitude;
                var dir = dist > 0.01f ? to / dist : transform.forward;
                m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, Quaternion.LookRotation(dir).eulerAngles.y, 400f * dt);
                var side = new Vector3(dir.z, 0, -dir.x) * m_Strafe;
                if (dist > 1.6f) move = dir + side * 0.25f;
                else move = side * 0.6f - dir * 0.1f;
                move = move.normalized * NBRig.BaseSpeed * 0.82f * m_P.Def.speed * (m_P.ActiveBuff == BuffKind.Speed ? 1.4f : 1f);
            }

            // stay on the arena
            var pos = transform.position;
            if (Mathf.Abs(pos.x) > NBArena.Half - 5f || Mathf.Abs(pos.z) > NBArena.Half - 5f)
                move = (-new Vector3(pos.x, 0, pos.z)).normalized * NBRig.BaseSpeed;

            bool blocking = now < m_BlockUntil && !stunned;
            if (m_P.Blocking.Value != blocking) m_P.Blocking.Value = blocking;
            if (blocking) move *= 0.35f;
            if (stunned) move = Vector3.zero;

            if (stunned && m_P.ServerStunned && Random.value < 0.004f * m_Skill && m_P.ServerEvasiveLeft <= 0 && m_P.ServerEvasive())
            {
                m_StunUntil = 0;
                Dash(-transform.forward, 6f, 0.2f);
            }

            if (m_Body.isGrounded && m_Vel.y < 0) m_Vel.y = -2f;
            m_Vel.y += NBRig.Gravity * dt;
            if (!stunned && m_Body.isGrounded && now >= m_NextJump && Random.value < 0.004f)
            {
                m_Vel.y = NBRig.JumpSpeed; m_NextJump = now + 3f;
            }

            var total = move + m_Ext + Vector3.up * m_Vel.y;
            if (now < m_DashUntil) total = m_DashVel + Vector3.up * Mathf.Max(0, m_Vel.y);
            m_Body.Move(total * dt);
            m_Ext = Vector3.MoveTowards(m_Ext, Vector3.zero, (m_Body.isGrounded ? 16f : 5f) * dt);

            if (transform.position.y < NBArena.KillY) m_P.FellRpc();

            if (!stunned && !blocking && m_Target != null && m_Target.Alive) Fight(dist, now);
            WritePose();
        }

        NBPlayer PickTarget()
        {
            NBPlayer best = null; float bd = float.MaxValue;
            foreach (var p in m_P.Enemies())
            {
                float d = Vector3.Distance(p.Foot, transform.position) * (p.IsBot.Value ? 1.15f : 1f);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        void Fight(float dist, float now)
        {
            var aim = (m_Target.Center - (transform.position + Vector3.up * 1.6f)).normalized;

            if (m_P.Ult.Value >= NBFighters.UltMax && dist < 14f && !m_P.Awakened)
            {
                m_P.ServerUseMove(4, transform.position + Vector3.up * 1.6f, aim);
                return;
            }

            if (dist < 2.1f && now >= m_NextPunch)
            {
                m_PunchRight = !m_PunchRight;
                m_PunchT = 0f;
                var push = m_Target.Foot - transform.position; push.y = 0;
                m_P.ServerM1(m_Target, push, 0, Random.Range(0.9f, 1.25f));
                m_NextPunch = now + Mathf.Lerp(0.65f, 0.38f, m_Skill) + (m_P.ServerCombo == 0 ? 0.5f : 0f);
            }

            if (now >= m_NextMove)
            {
                m_NextMove = now + Random.Range(0.6f, 1.6f) / m_Skill;
                int start = Random.Range(0, 4);
                for (int k = 0; k < 4; k++)
                {
                    int i = (start + k) % 4;
                    if (m_P.ServerCooldownLeft(i) > 0) continue;
                    if (!Suits(m_P.Def.moves[i], dist)) continue;
                    m_P.ServerUseMove(i, transform.position + Vector3.up * 1.6f, aim);
                    break;
                }
            }
        }

        static bool Suits(MoveDef m, float dist)
        {
            switch (m.kind)
            {
                case MoveKind.Projectile: return dist > 4f && dist < m.range * 0.6f;
                case MoveKind.DashStrike: return dist > 3f && dist < m.range * 0.9f;
                case MoveKind.Slam: return dist < m.radius * 0.8f;
                case MoveKind.Launcher: return dist < 2.6f;
                case MoveKind.Teleport: return dist > 5f && dist < m.range;
                case MoveKind.Buff: return dist < 12f;
                case MoveKind.Meteor: return dist > 3f && dist < m.range * 0.8f;
                case MoveKind.Pull: return dist > 4f && dist < m.range;
                case MoveKind.Beam: return dist < m.range * 0.8f;
                case MoveKind.Flurry: return dist < m.range;
            }
            return false;
        }

        void WritePose()
        {
            var foot = transform.position;
            var rot = Quaternion.Euler(m_P.Dead.Value ? 60f : 6f, m_Yaw, 0);
            var head = foot + Vector3.up * (m_P.Dead.Value ? 0.4f : 1.58f + Mathf.Sin(Time.time * 7f) * 0.015f);
            float ext = 0f;
            if (m_PunchT >= 0f)
            {
                m_PunchT += Time.deltaTime;
                float k = m_PunchT / 0.18f;
                ext = k < 0.5f ? k * 2f : (1 - k) * 2f;
                if (m_PunchT > 0.18f) m_PunchT = -1f;
            }
            bool guard = m_P.Blocking.Value;
            var yawRot = Quaternion.Euler(0, m_Yaw, 0);
            var l = new Vector3(-0.22f, guard ? -0.05f : -0.3f, guard ? 0.25f : 0.3f + (!m_PunchRight ? ext * 0.45f : 0));
            var r = new Vector3(0.22f, guard ? -0.05f : -0.3f, guard ? 0.25f : 0.3f + (m_PunchRight ? ext * 0.45f : 0));
            m_P.Pose.Value = new NBPose
            {
                foot = foot, head = head, headRot = rot,
                lh = head + yawRot * l, lhRot = yawRot,
                rh = head + yawRot * r, rhRot = yawRot,
            };
        }
    }
}
