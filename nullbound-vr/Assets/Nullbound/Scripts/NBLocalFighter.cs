using Unity.Netcode;
using UnityEngine;

namespace Nullbound
{
    /// Your own fighter: turns real punches and button presses into requests to the server.
    /// Cooldowns are mirrored here so the wrist HUD can show them instantly.
    public class NBLocalFighter : MonoBehaviour
    {
        public static NBLocalFighter Instance { get; private set; }

        NBPlayer m_P;
        public readonly float[] NextMove = new float[4];
        public float NextDash, NextEvasive, NextUppercut, NextDownslam, M1LockUntil, LastHitTime;
        public int Combo;
        readonly bool[] m_Ready = { true, true };
        readonly float[] m_HandNext = new float[2];
        bool m_SentFell;

        public NBPlayer Player => m_P;

        public void Init(NBPlayer p)
        {
            m_P = p;
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var rig = NBRig.Instance;
            if (rig != null) { rig.SpeedMul = 1f; rig.LocomotionLocked = false; rig.Frozen = false; }
        }

        public void ResetCooldowns()
        {
            for (int i = 0; i < 4; i++) NextMove[i] = 0;
            NextDash = NextEvasive = NextUppercut = NextDownslam = M1LockUntil = 0;
            Combo = 0;
        }

        float CooldownScale => m_P.Awakened ? 0.5f : 1f;

        void Update()
        {
            var rig = NBRig.Instance;
            if (m_P == null || !m_P.IsSpawned || rig == null) return;
            float now = Time.time;
            var def = m_P.Def;

            bool menuOpen = NBMenu.Instance != null && NBMenu.Instance.Visible;
            rig.Frozen = m_P.Dead.Value;
            if (m_P.Dead.Value)
            {
                rig.LocomotionLocked = true;
                if (m_P.Blocking.Value) m_P.Blocking.Value = false;
                m_SentFell = false;
                return;
            }
            rig.LocomotionLocked = menuOpen;

            // block: hold both grips
            bool block = NBInput.Block && !rig.Stunned && !menuOpen;
            if (m_P.Blocking.Value != block) m_P.Blocking.Value = block;

            var buff = m_P.ActiveBuff;
            float spd = def.speed;
            if (buff == BuffKind.Speed) spd *= 1.45f;
            if (buff == BuffKind.Veil) spd *= 1.25f;
            if (m_P.Awakened) spd *= 1.12f;
            if (block) spd *= 0.35f;
            rig.SpeedMul = spd;

            // fell off the arena
            if (rig.Foot.y < NBArena.KillY && !m_SentFell) { m_SentFell = true; m_P.FellRpc(); }

            if (menuOpen) return;

            if (NBInput.Jump) rig.TryJump();

            // dash, or evasive when you're being comboed
            if (NBInput.Dash)
            {
                if (rig.Stunned)
                {
                    if (now >= NextEvasive)
                    {
                        NextEvasive = now + NBFighters.EvasiveCooldown;
                        m_P.EvasiveRpc();
                        rig.ClearStun();
                        rig.Dash(-rig.Forward, 6f, 0.2f);
                        NBHud.Instance?.Big("EVASIVE!", Color.white, 0.8f);
                    }
                }
                else if (now >= NextDash)
                {
                    NextDash = now + NBFighters.DashCooldown * (m_P.Awakened ? 0.6f : 1f);
                    var a = NBInput.MoveAxis;
                    var f = rig.Forward; var r = new Vector3(f.z, 0, -f.x);
                    var dir = a.sqrMagnitude > 0.1f ? (f * a.y + r * a.x).normalized : f;
                    rig.Dash(dir, 8.5f * (buff == BuffKind.Speed ? 1.3f : 1f), 0.2f);
                    NBAudio.Play(NBAudio.Whoosh, rig.Head.position, 0.6f);
                }
            }

            if (rig.Stunned || block) { HandsCooldown(rig); return; }

            // moves 1-4
            for (int i = 0; i < 4; i++)
            {
                if (!NBInput.Move[i]) continue;
                if (now < NextMove[i]) { NBHud.Instance?.Small(def.moves[i].name + " " + Mathf.CeilToInt(NextMove[i] - now) + "s"); continue; }
                NextMove[i] = now + def.moves[i].cooldown * CooldownScale;
                m_P.MoveRpc(i, rig.Head.position, Aim(rig));
                NBInput.Haptic(true, 0.4f, 0.08f); NBInput.Haptic(false, 0.4f, 0.08f);
            }

            // awakening (ultimate): click both thumbsticks
            if (NBInput.Ult)
            {
                if (m_P.Ult.Value >= NBFighters.UltMax - 0.01f && !m_P.Awakened)
                {
                    m_P.MoveRpc(4, rig.Head.position, Aim(rig));
                    ResetCooldowns();
                }
                else NBHud.Instance?.Small("AWAKENING NOT READY");
            }

            Punch(rig, rig.RightHand, 1, now);
            Punch(rig, rig.LeftHand, 0, now);
        }

        Vector3 Aim(NBRig rig)
        {
            // aim where you look, nudged toward where your main hand points
            var hand = NBSettings.LeftHanded ? rig.LeftHand.transform : rig.RightHand.transform;
            var look = rig.Head.forward;
            var reach = hand.position - rig.Head.position;
            if (reach.magnitude > 0.35f && Vector3.Dot(reach.normalized, look) > 0.6f)
                look = (look * 0.6f + reach.normalized * 0.4f).normalized;
            return look;
        }

        void HandsCooldown(NBRig rig)
        {
            if (rig.RightHand.Speed < 1.2f) m_Ready[1] = true;
            if (rig.LeftHand.Speed < 1.2f) m_Ready[0] = true;
        }

        /// A punch is a fast hand movement that ends inside an enemy's body.
        void Punch(NBRig rig, NBHand hand, int idx, float now)
        {
            float speed = hand.Speed;
            if (speed < 1.2f) m_Ready[idx] = true;
            if (!m_Ready[idx] || speed < 2.3f || now < m_HandNext[idx]) return;

            var v = hand.Velocity;
            var tip = hand.transform.position + v.normalized * 0.1f;
            NBPlayer target = null; float best = 0.24f;
            foreach (var p in m_P.Enemies())
            {
                float d = p.DistToBody(tip);
                if (d < best) { best = d; target = p; }
            }
            if (target == null) return;

            var dirN = v.normalized;
            byte kind = 0;
            if (dirN.y > 0.72f && speed > 2.8f && now >= NextUppercut) { kind = 1; NextUppercut = now + NBFighters.UppercutCooldown; }
            else if (dirN.y < -0.72f && !rig.Grounded && now >= NextDownslam) { kind = 2; NextDownslam = now + NBFighters.DownslamCooldown; }
            else if (now < M1LockUntil) return;

            m_Ready[idx] = false;
            m_HandNext[idx] = now + 0.2f;

            if (kind == 0)
            {
                if (now - LastHitTime > NBFighters.ComboWindow) Combo = 0;
                Combo++;
                if (Combo >= 4) { Combo = 0; M1LockUntil = now + NBFighters.FinisherCooldown; }
            }
            else Combo = 0;
            LastHitTime = now;

            var push = v; push.y = 0;
            if (push.sqrMagnitude < 0.01f) push = rig.Forward;
            m_P.M1Rpc(new NetworkObjectReference(target.NetworkObject), push.normalized, kind, speed / 4f);
            NBInput.Haptic(hand.isRight, Mathf.Clamp01(0.4f + speed * 0.1f), 0.07f);
            NBHud.Instance?.ComboPop(Combo == 0 && kind == 0 ? 4 : Combo, kind);
        }
    }
}
