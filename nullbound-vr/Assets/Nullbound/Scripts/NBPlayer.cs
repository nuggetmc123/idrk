using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Nullbound
{
    /// Head + hands + feet, sent by whoever controls the fighter.
    public struct NBPose : INetworkSerializeByMemcpy
    {
        public Vector3 foot, head, lh, rh;
        public Quaternion headRot, lhRot, rhRot;
    }

    public enum FxKind : byte { Trail, Ring, Burst, Marker, Explosion, Beam, Blink, Buff, Pull, Evade, Awaken, GuardBreak }

    /// One fighter in a match: a human in VR or a bot. The server owns health, damage and moves;
    /// the owner moves its own body and sends its pose.
    public class NBPlayer : NetworkBehaviour
    {
        public static readonly List<NBPlayer> All = new List<NBPlayer>();
        public static NBPlayer Local { get; private set; }

        const NetworkVariableReadPermission R = NetworkVariableReadPermission.Everyone;
        const NetworkVariableWritePermission Owner = NetworkVariableWritePermission.Owner;
        const NetworkVariableWritePermission Server = NetworkVariableWritePermission.Server;

        public readonly NetworkVariable<NBPose> Pose = new NetworkVariable<NBPose>(default, R, Owner);
        public readonly NetworkVariable<int> FighterId = new NetworkVariable<int>(0, R, Owner);
        public readonly NetworkVariable<FixedString32Bytes> DisplayName = new NetworkVariable<FixedString32Bytes>(default, R, Owner);
        public readonly NetworkVariable<bool> Blocking = new NetworkVariable<bool>(false, R, Owner);
        public readonly NetworkVariable<bool> IsBot = new NetworkVariable<bool>(false, R, Server);
        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(NBFighters.MaxHealth, R, Server);
        public readonly NetworkVariable<float> Ult = new NetworkVariable<float>(0f, R, Server);
        public readonly NetworkVariable<bool> Dead = new NetworkVariable<bool>(false, R, Server);
        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>(0, R, Server);
        public readonly NetworkVariable<int> Deaths = new NetworkVariable<int>(0, R, Server);
        public readonly NetworkVariable<double> AwakenEnd = new NetworkVariable<double>(0, R, Server);
        public readonly NetworkVariable<int> Buff = new NetworkVariable<int>(0, R, Server);
        public readonly NetworkVariable<double> BuffEnd = new NetworkVariable<double>(0, R, Server);

        // set by the server before spawning a bot
        [System.NonSerialized] public bool pendingBot;
        [System.NonSerialized] public int pendingFighter;
        [System.NonSerialized] public string pendingName;

        public bool IsLocalHuman => IsOwner && !IsBot.Value && !pendingBot;
        public FighterDef Def => NBFighters.Get(FighterId.Value);
        public string Name => DisplayName.Value.ToString();
        public bool Awakened => Now < AwakenEnd.Value;
        public BuffKind ActiveBuff => Now < BuffEnd.Value ? (BuffKind)Buff.Value : BuffKind.None;
        public Vector3 Foot => transform.position;
        public Vector3 HeadPos => m_Head != null ? m_Head.position : transform.position + Vector3.up * 1.6f;
        public Vector3 Center => (Foot + HeadPos) * 0.5f;
        public Vector3 Forward { get { var f = (m_Head != null ? m_Head.forward : transform.forward); f.y = 0; return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized; } }
        public bool IsAirborne => m_Airborne;

        public static double Now
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
            }
        }

        // ---- visuals ----
        Transform m_Avatar, m_Head, m_Torso, m_LH, m_RH, m_Tag, m_HpFill, m_Shield, m_Aura;
        TextMesh m_NameText;
        readonly List<Renderer> m_BodyRenderers = new List<Renderer>();
        Renderer m_HeadR, m_TorsoR, m_LHR, m_RHR, m_VisorR, m_BeltR;
        bool m_Airborne;
        float m_LastFootY;

        // ---- server state ----
        double m_SrvStunUntil, m_IFrameUntil, m_LastM1, m_SrvNextEvasive, m_LastHitTime;
        readonly double[] m_SrvNext = new double[8];   // 0-3 moves, 5 uppercut, 6 downslam, 7 finisher lock
        int m_Combo;
        NBPlayer m_LastAttacker;
        static int s_NextProjectileId = 1;

        // =================================================================
        // lifecycle
        // =================================================================
        void Awake() { BuildAvatar(); }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            FighterId.OnValueChanged += OnFighterChanged;
            AwakenEnd.OnValueChanged += OnAwakenChanged;
            Dead.OnValueChanged += OnDeadChanged;

            if (IsServer && pendingBot)
            {
                IsBot.Value = true;
                FighterId.Value = pendingFighter;
                DisplayName.Value = new FixedString32Bytes(pendingName ?? "BOT");
                var bot = gameObject.AddComponent<NBBot>();
                bot.Init(this);
            }
            else if (IsOwner)
            {
                DisplayName.Value = new FixedString32Bytes(NBSettings.PlayerName);
                FighterId.Value = NBSettings.Fighter;
                Local = this;
                gameObject.AddComponent<NBLocalFighter>().Init(this);
                NBGame.Instance?.OnLocalPlayerSpawned(this);
                ReadyRpc();
            }
            ApplyColors();
            if (Pose.Value.head != Vector3.zero) SnapToPose();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            FighterId.OnValueChanged -= OnFighterChanged;
            AwakenEnd.OnValueChanged -= OnAwakenChanged;
            Dead.OnValueChanged -= OnDeadChanged;
            if (Local == this) Local = null;
        }

        public override void OnDestroy()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            base.OnDestroy();
        }

        void OnFighterChanged(int a, int b) => ApplyColors();

        void OnAwakenChanged(double a, double b)
        {
            if (b > Now)
            {
                NBFX.Burst(Center, Def.main, 2.2f, 16);
                NBAudio.Play(NBAudio.Boom, Center, 0.8f, 1.3f);
                if (IsLocalHuman) NBHud.Instance?.Big("AWAKENED!", Def.main, 2f);
            }
        }

        void OnDeadChanged(bool a, bool b)
        {
            if (b) NBFX.Burst(Center, Color.white, 2f, 14);
        }

        // =================================================================
        // avatar
        // =================================================================
        void BuildAvatar()
        {
            m_Avatar = new GameObject("Avatar").transform;
            m_Avatar.SetParent(transform, false);

            m_Head = NBMat.Prim(PrimitiveType.Sphere, m_Avatar, new Vector3(0, 1.6f, 0), Vector3.one * 0.28f, NBMat.Lit(Color.white)).transform;
            m_HeadR = m_Head.GetComponent<Renderer>();
            m_VisorR = NBMat.Prim(PrimitiveType.Cube, m_Head, new Vector3(0, 0.08f, 0.38f), new Vector3(0.85f, 0.28f, 0.3f), NBMat.Lit(new Color(0.05f, 0.05f, 0.08f), 0.9f)).GetComponent<Renderer>();
            m_Torso = NBMat.Prim(PrimitiveType.Capsule, m_Avatar, new Vector3(0, 1.05f, 0), new Vector3(0.42f, 0.42f, 0.28f), NBMat.Lit(Color.white)).transform;
            m_TorsoR = m_Torso.GetComponent<Renderer>();
            m_BeltR = NBMat.Prim(PrimitiveType.Cylinder, m_Torso, new Vector3(0, -0.45f, 0), new Vector3(1.02f, 0.08f, 1.02f), NBMat.Lit(Color.gray)).GetComponent<Renderer>();
            m_LH = NBMat.Prim(PrimitiveType.Sphere, m_Avatar, new Vector3(-0.25f, 1.2f, 0.3f), new Vector3(0.12f, 0.11f, 0.14f), NBMat.Lit(Color.white)).transform;
            m_RH = NBMat.Prim(PrimitiveType.Sphere, m_Avatar, new Vector3(0.25f, 1.2f, 0.3f), new Vector3(0.12f, 0.11f, 0.14f), NBMat.Lit(Color.white)).transform;
            m_LHR = m_LH.GetComponent<Renderer>(); m_RHR = m_RH.GetComponent<Renderer>();

            m_Shield = NBMat.Prim(PrimitiveType.Sphere, m_Avatar, Vector3.zero, new Vector3(0.7f, 0.9f, 0.15f), NBMat.Glow(new Color(0.6f, 0.85f, 1f, 0.35f))).transform;
            m_Shield.gameObject.SetActive(false);
            m_Aura = NBMat.Prim(PrimitiveType.Sphere, m_Avatar, Vector3.zero, new Vector3(1.1f, 2f, 1.1f), NBMat.Glow(new Color(1, 1, 1, 0.15f))).transform;
            m_Aura.gameObject.SetActive(false);

            m_Tag = new GameObject("Tag").transform;
            m_Tag.SetParent(m_Avatar, false);
            m_NameText = NBMat.Text(m_Tag, "", new Vector3(0, 0.12f, 0), 0.09f, Color.white);
            NBMat.Prim(PrimitiveType.Quad, m_Tag, Vector3.zero, new Vector3(0.6f, 0.06f, 1), NBMat.Glow(new Color(0, 0, 0, 0.6f)));
            var fillRoot = new GameObject("HpRoot").transform;
            fillRoot.SetParent(m_Tag, false);
            fillRoot.localPosition = new Vector3(-0.29f, 0, -0.002f);
            NBMat.Prim(PrimitiveType.Quad, fillRoot, new Vector3(0.29f, 0, 0), new Vector3(0.58f, 0.045f, 1), NBMat.Glow(new Color(0.3f, 1f, 0.4f, 0.95f)));
            m_HpFill = fillRoot; // scaled from its left edge

            m_BodyRenderers.AddRange(new[] { m_HeadR, m_VisorR, m_TorsoR, m_BeltR, m_LHR, m_RHR });
        }

        void ApplyColors()
        {
            var d = Def;
            m_HeadR.material.color = d.main;
            m_TorsoR.material.color = Color.Lerp(d.main, Color.black, 0.25f);
            m_BeltR.material.color = d.accent;
            m_LHR.material.color = d.accent;
            m_RHR.material.color = d.accent;
            m_Aura.GetComponent<Renderer>().material.color = new Color(d.main.r, d.main.g, d.main.b, 0.18f);
            if (IsLocalHuman && NBRig.Instance != null)
            {
                NBRig.Instance.LeftHand.SetColors(d.accent, d.main);
                NBRig.Instance.RightHand.SetColors(d.accent, d.main);
            }
        }

        void SnapToPose()
        {
            var p = Pose.Value;
            transform.position = p.foot;
            m_Head.position = p.head; m_Head.rotation = p.headRot;
            m_LH.position = p.lh; m_LH.rotation = p.lhRot;
            m_RH.position = p.rh; m_RH.rotation = p.rhRot;
        }

        void Update()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;

            if (IsLocalHuman)
            {
                var rig = NBRig.Instance;
                if (rig != null)
                {
                    transform.position = rig.Foot;
                    m_Head.position = rig.Head.position; m_Head.rotation = rig.Head.rotation;
                    m_LH.position = rig.LeftHand.transform.position; m_RH.position = rig.RightHand.transform.position;
                    Pose.Value = new NBPose
                    {
                        foot = rig.Foot,
                        head = rig.Head.position, headRot = rig.Head.rotation,
                        lh = rig.LeftHand.transform.position, lhRot = rig.LeftHand.transform.rotation,
                        rh = rig.RightHand.transform.position, rhRot = rig.RightHand.transform.rotation,
                    };
                }
                m_Avatar.gameObject.SetActive(false);
                return;
            }

            // everyone else: follow the received pose smoothly
            var pose = Pose.Value;
            bool botOnServer = IsServer && IsBot.Value;
            float k = botOnServer ? 1f : 1f - Mathf.Exp(-20f * dt);
            if (!botOnServer)
            {
                if ((transform.position - pose.foot).sqrMagnitude > 25f) SnapToPose();
                transform.position = Vector3.Lerp(transform.position, pose.foot, k);
            }
            m_Head.position = Vector3.Lerp(m_Head.position, pose.head, k);
            m_Head.rotation = Quaternion.Slerp(m_Head.rotation, pose.headRot, k);
            m_LH.position = Vector3.Lerp(m_LH.position, pose.lh, k);
            m_LH.rotation = Quaternion.Slerp(m_LH.rotation, pose.lhRot, k);
            m_RH.position = Vector3.Lerp(m_RH.position, pose.rh, k);
            m_RH.rotation = Quaternion.Slerp(m_RH.rotation, pose.rhRot, k);

            m_Airborne = transform.position.y - m_LastFootY > 0.02f || (transform.position.y > 0.3f && Mathf.Abs(transform.position.y - m_LastFootY) > 0.001f);
            m_LastFootY = transform.position.y;

            // torso hangs under the head, facing where the head faces
            var yaw = Quaternion.Euler(0, m_Head.eulerAngles.y, 0);
            float headH = m_Head.position.y - transform.position.y;
            m_Torso.position = new Vector3(m_Head.position.x, transform.position.y + Mathf.Max(0.6f, headH - 0.55f), m_Head.position.z) - yaw * Vector3.forward * 0.05f;
            m_Torso.rotation = yaw;

            bool show = !Dead.Value && ActiveBuff != BuffKind.Veil;
            m_Avatar.gameObject.SetActive(show || ActiveBuff == BuffKind.Veil);
            foreach (var r in m_BodyRenderers) r.enabled = show;
            m_Tag.gameObject.SetActive(show);

            m_Shield.gameObject.SetActive(show && Blocking.Value);
            if (Blocking.Value) { m_Shield.position = m_Head.position + yaw * new Vector3(0, -0.35f, 0.42f); m_Shield.rotation = yaw; }

            bool aura = show && Awakened;
            m_Aura.gameObject.SetActive(aura);
            if (aura)
            {
                m_Aura.position = m_Torso.position;
                float s = 1f + Mathf.Sin(Time.time * 8f) * 0.06f;
                m_Aura.localScale = new Vector3(1.1f * s, 2f * s, 1.1f * s);
            }

            var cam = NBRig.Instance != null ? NBRig.Instance.Head : null;
            m_Tag.position = m_Head.position + Vector3.up * 0.35f;
            if (cam != null) m_Tag.rotation = Quaternion.LookRotation(m_Tag.position - cam.position);
            m_NameText.text = (Awakened ? "* " : "") + Name;
            m_HpFill.localScale = new Vector3(Mathf.Clamp01(Health.Value / NBFighters.MaxHealth), 1, 1);
        }

        /// Distance from a point to this fighter's body capsule surface (no colliders needed).
        public float DistToBody(Vector3 p)
        {
            var a = Foot + Vector3.up * 0.35f;
            var b = HeadPos;
            if (b.y < a.y + 0.2f) b = a + Vector3.up * 0.4f;
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + ab * t) - 0.33f;
        }

        public bool Alive => IsSpawned && !Dead.Value;

        public IEnumerable<NBPlayer> Enemies()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var p = All[i];
                if (p != null && p != this && p.Alive) yield return p;
            }
        }

        // =================================================================
        // requests from the owner (client -> server)
        // =================================================================
        [Rpc(SendTo.Server)]
        void ReadyRpc()
        {
            var spawn = NBArena.PickSpawn(this);
            TeleportSelfRpc(spawn, -spawn);
        }

        [Rpc(SendTo.Server)]
        public void M1Rpc(NetworkObjectReference target, Vector3 dir, byte kind, float power)
        {
            if (target.TryGet(out var no) && no.TryGetComponent<NBPlayer>(out var p))
                ServerM1(p, dir, kind, power);
        }

        [Rpc(SendTo.Server)]
        public void MoveRpc(int slot, Vector3 origin, Vector3 dir) => ServerUseMove(slot, origin, dir);

        [Rpc(SendTo.Server)]
        public void EvasiveRpc() => ServerEvasive();

        [Rpc(SendTo.Server)]
        public void FellRpc() { if (!Dead.Value) { Health.Value = 0; ServerDie(); } }

        // =================================================================
        // server -> owner
        // =================================================================
        [Rpc(SendTo.Owner)]
        void KnockRpc(Vector3 vel, float stun)
        {
            if (IsBot.Value) { GetComponent<NBBot>()?.Knock(vel, stun); return; }
            var rig = NBRig.Instance;
            if (rig == null) return;
            rig.Knock(vel, stun);
            NBInput.Haptic(true, 0.7f, 0.12f); NBInput.Haptic(false, 0.7f, 0.12f);
            NBHud.Instance?.HitFlash();
        }

        [Rpc(SendTo.Owner)]
        void DashSelfRpc(Vector3 dir, float dist, float time)
        {
            if (IsBot.Value) { GetComponent<NBBot>()?.Dash(dir, dist, time); return; }
            NBRig.Instance?.Dash(dir, dist, time);
        }

        [Rpc(SendTo.Owner)]
        void TeleportSelfRpc(Vector3 pos, Vector3 face)
        {
            if (IsBot.Value) { GetComponent<NBBot>()?.Teleport(pos, face); return; }
            NBRig.Instance?.Teleport(pos, face);
        }

        [Rpc(SendTo.Owner)]
        void ImpulseSelfRpc(Vector3 vel, float lockTime)
        {
            if (IsBot.Value) { GetComponent<NBBot>()?.Knock(vel, lockTime); return; }
            var rig = NBRig.Instance;
            if (rig == null) return;
            rig.Impulse(vel);
            if (lockTime > 0) rig.Knock(Vector3.zero, lockTime);
        }

        // =================================================================
        // server -> everyone (effects)
        // =================================================================
        [Rpc(SendTo.Everyone)]
        void HitFxRpc(Vector3 pos, float dmg, bool blocked, int attackerFighter, bool heavy)
        {
            var c = NBFighters.Get(attackerFighter).main;
            NBFX.Burst(pos, blocked ? new Color(0.7f, 0.85f, 1f) : c, heavy ? 1.5f : 0.8f, heavy ? 12 : 6);
            NBFX.DamageNumber(pos + Vector3.up * 0.3f, dmg, heavy ? Color.yellow : Color.white, blocked);
            NBAudio.Play(blocked ? NBAudio.Block : heavy ? NBAudio.Heavy : NBAudio.Punch, pos, heavy ? 1f : 0.8f);
        }

        [Rpc(SendTo.Everyone)]
        void FxRpc(FxKind kind, Vector3 a, Vector3 b, float size, float time)
        {
            var c = Def.main;
            switch (kind)
            {
                case FxKind.Trail: StartCoroutine(TrailFor(time)); NBAudio.Play(NBAudio.Whoosh, a, 0.9f); break;
                case FxKind.Ring: NBFX.Ring(a, size, c, time); NBAudio.Play(NBAudio.Heavy, a, 0.9f, 0.8f); break;
                case FxKind.Burst: NBFX.Burst(a, c, size); break;
                case FxKind.Marker: NBFX.Marker(a, size, c, time); NBAudio.Play(NBAudio.Zap, a, 0.6f, 0.6f); break;
                case FxKind.Explosion:
                    NBFX.Ring(a, size, c, 0.45f); NBFX.Pillar(a, size * 0.6f, size * 2.5f, c, 0.6f); NBFX.Burst(a + Vector3.up, c, size * 0.6f, 14);
                    NBAudio.Play(NBAudio.Boom, a, 1f);
                    break;
                case FxKind.Beam: StartCoroutine(BeamFx(a, b, size, time)); break;
                case FxKind.Blink: NBFX.Burst(a, c, 1.2f, 10); NBFX.Burst(b, c, 1.2f, 10); NBAudio.Play(NBAudio.Zap, b, 0.8f, 1.5f); break;
                case FxKind.Buff: NBFX.Burst(a, c, 1.6f, 14); NBFX.Ring(transform.position, 2f, c); NBAudio.Play(NBAudio.Zap, a, 0.7f, 0.8f); break;
                case FxKind.Pull: NBFX.Trail(a, b, c, 0.25f); break;
                case FxKind.Evade: NBFX.Burst(a, Color.white, 1.2f, 10); NBAudio.Play(NBAudio.Whoosh, a, 1f, 1.4f); break;
                case FxKind.GuardBreak: NBFX.Text3D(a, "GUARD BREAK", new Color(1f, 0.5f, 0.2f), 0.18f, 0.9f); NBAudio.Play(NBAudio.Heavy, a, 1f, 1.4f); break;
            }
        }

        [Rpc(SendTo.Everyone)]
        void ProjectileRpc(int id, int fighter, int slot, Vector3 pos, Vector3 dir)
        {
            var def = slot < 4 ? NBFighters.Get(fighter).moves[slot] : NBFighters.Get(fighter).ult;
            NBProjectile.Spawn(this, def, NBFighters.Get(fighter).main, id, pos, dir, IsServer);
            NBAudio.Play(NBAudio.Whoosh, pos, 0.7f, 1.3f);
        }

        [Rpc(SendTo.Everyone)]
        public void ProjectileEndRpc(int id, Vector3 pos, float size)
        {
            NBProjectile.End(id, pos, size, Def.main);
        }

        [Rpc(SendTo.Everyone)]
        void KillFeedRpc(string killer, string victim, bool victimIsMe)
        {
            NBHud.Instance?.Feed(killer, victim);
            NBAudio.Play(NBAudio.Ko, Center, 0.7f);
        }

        IEnumerator TrailFor(float time)
        {
            var last = Center;
            float t = 0;
            while (t < time + 0.1f && this != null)
            {
                t += Time.deltaTime;
                var now = Center;
                NBFX.Trail(last, now, Def.main, 0.7f);
                last = now;
                yield return null;
            }
        }

        IEnumerator BeamFx(Vector3 origin, Vector3 dir, float length, float time)
        {
            // charge
            var charge = NBMat.Prim(PrimitiveType.Sphere, null, origin + dir * 0.6f, Vector3.one * 0.2f, NBMat.Glow(new Color(1, 1, 1, 0.8f)));
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.45f)
            {
                charge.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.2f, k);
                yield return null;
            }
            Destroy(charge);
            NBFX.Beam(origin, dir, length, 1.4f, Def.main, time);
            NBAudio.Play(NBAudio.Boom, origin + dir * 4f, 1f, 0.7f);
            for (float t = 0; t < time; t += 0.25f)
            {
                NBAudio.Play(NBAudio.Zap, origin + dir * 6f, 0.5f, 0.5f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        // =================================================================
        // server combat
        // =================================================================
        public void ServerM1(NBPlayer target, Vector3 dir, byte kind, float power)
        {
            if (!IsServer || Dead.Value || target == null || target == this || !target.Alive) return;
            double now = Now;
            if (now < m_SrvStunUntil - 0.08) return;
            if (now - m_LastM1 < 0.13) return;
            if (target.DistToBody(HeadPos) > 3.2f && target.DistToBody(Center) > 3.2f) return;

            dir.y = 0;
            dir = dir.sqrMagnitude < 1e-4f ? (target.Foot - Foot).normalized : dir.normalized;
            float dmg = NBFighters.M1Damage * Def.punch * Mathf.Clamp(power, 0.8f, 1.5f);
            bool heavy = false, guardBreak = false;
            Vector3 knock; float stun;

            if (kind == 1 && now >= m_SrvNext[5])
            {
                m_SrvNext[5] = now + NBFighters.UppercutCooldown;
                dmg *= 1.6f; knock = dir * 2f + Vector3.up * 14f; stun = 1.1f; heavy = true; guardBreak = true; m_Combo = 0;
            }
            else if (kind == 2 && now >= m_SrvNext[6])
            {
                m_SrvNext[6] = now + NBFighters.DownslamCooldown;
                dmg *= 1.8f; knock = dir * 3f + Vector3.down * 14f; stun = 1.3f; heavy = true; m_Combo = 0;
            }
            else
            {
                if (now < m_SrvNext[7]) return;
                if (now - m_LastM1 > NBFighters.ComboWindow) m_Combo = 0;
                m_Combo++;
                if (m_Combo >= 4)
                {
                    m_Combo = 0;
                    m_SrvNext[7] = now + NBFighters.FinisherCooldown;
                    dmg *= 1.5f; knock = dir * 17f + Vector3.up * 5f; stun = 1.0f; heavy = true; guardBreak = true;
                }
                else
                {
                    knock = dir * 2.2f + Vector3.up * (target.IsAirborne ? 3f : 0.5f); stun = NBFighters.M1Stun;
                }
            }
            m_LastM1 = now;
            ApplyHit(target, this, dmg, knock, stun, false, guardBreak, heavy, (target.Center + HeadPos) * 0.5f);
        }

        public void ServerUseMove(int slot, Vector3 origin, Vector3 dir)
        {
            if (!IsServer || Dead.Value || slot < 0 || slot > 4) return;
            double now = Now;
            if (now < m_SrvStunUntil - 0.08) return;
            if ((origin - HeadPos).sqrMagnitude > 4f) origin = HeadPos;
            dir = dir.sqrMagnitude < 1e-4f ? Forward : dir.normalized;
            MoveDef def;
            if (slot == 4)
            {
                if (Ult.Value < NBFighters.UltMax - 0.01f) return;
                Ult.Value = 0;
                AwakenEnd.Value = now + NBFighters.AwakenTime;
                def = Def.ult;
            }
            else
            {
                if (now < m_SrvNext[slot] - 0.3) return;
                def = Def.moves[slot];
                m_SrvNext[slot] = now + def.cooldown * (Awakened ? 0.5f : 1f);
            }
            StartCoroutine(RunMove(def, slot, origin, dir));
        }

        public bool ServerEvasive()
        {
            double now = Now;
            if (Dead.Value || now < m_SrvNextEvasive) return false;
            m_SrvNextEvasive = now + NBFighters.EvasiveCooldown;
            m_SrvStunUntil = 0;
            m_IFrameUntil = now + 0.6;
            FxRpc(FxKind.Evade, Center, Vector3.zero, 1f, 0f);
            return true;
        }

        public double ServerCooldownLeft(int slot) => System.Math.Max(0, m_SrvNext[slot] - Now);
        public double ServerEvasiveLeft => System.Math.Max(0, m_SrvNextEvasive - Now);
        public bool ServerStunned => Now < m_SrvStunUntil;
        public int ServerCombo => m_Combo;

        void RefundCooldown(int slot) { if (slot < 4) m_SrvNext[slot] = Now + 1.0; else Ult.Value = NBFighters.UltMax; }

        NBPlayer FindTarget(Vector3 origin, Vector3 dir, float range, float cone)
        {
            NBPlayer best = null; float bestScore = float.MaxValue;
            foreach (var p in Enemies())
            {
                var to = p.Center - origin;
                float d = to.magnitude;
                if (d > range) continue;
                float ang = Vector3.Angle(dir, to);
                if (ang > cone) continue;
                float score = ang * 0.5f + d;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        NBPlayer Nearest(float range)
        {
            NBPlayer best = null; float bd = range;
            foreach (var p in Enemies())
            {
                float d = Vector3.Distance(p.Foot, Foot);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        void HitArea(MoveDef def, Vector3 center, float radius, bool fromCenter, Vector3 pushDir, float dmgMul = 1f)
        {
            foreach (var p in new List<NBPlayer>(Enemies()))
            {
                if (p.DistToBody(center) > radius) continue;
                var away = p.Foot - center; away.y = 0;
                var flat = fromCenter ? (away.sqrMagnitude < 0.01f ? Forward : away.normalized) : pushDir;
                ApplyHit(p, this, def.damage * dmgMul, flat * def.knock + Vector3.up * def.lift, def.stun, def.unblockable, def.guardBreak, true, p.Center);
            }
        }

        IEnumerator RunMove(MoveDef def, int slot, Vector3 origin, Vector3 dir)
        {
            var flat = dir; flat.y = 0; flat = flat.sqrMagnitude < 1e-4f ? Forward : flat.normalized;
            switch (def.kind)
            {
                case MoveKind.Projectile:
                {
                    for (int i = 0; i < def.count; i++)
                    {
                        float ang = (i - (def.count - 1) * 0.5f) * def.spread;
                        var d = Quaternion.AngleAxis(ang, Vector3.up) * dir;
                        ProjectileRpc(s_NextProjectileId++, FighterId.Value, slot, origin + d * 0.7f, d);
                    }
                    break;
                }
                case MoveKind.DashStrike:
                {
                    DashSelfRpc(flat, def.range, def.duration);
                    FxRpc(FxKind.Trail, Center, Vector3.zero, 1f, def.duration);
                    var hit = new HashSet<NBPlayer>();
                    float t = 0;
                    while (t < def.duration + 0.2f)
                    {
                        foreach (var p in new List<NBPlayer>(Enemies()))
                        {
                            if (hit.Contains(p) || p.DistToBody(Center) > def.radius) continue;
                            hit.Add(p);
                            ApplyHit(p, this, def.damage, flat * def.knock + Vector3.up * def.lift, def.stun, def.unblockable, def.guardBreak, true, p.Center);
                        }
                        t += Time.deltaTime;
                        yield return null;
                    }
                    break;
                }
                case MoveKind.Launcher:
                {
                    FxRpc(FxKind.Ring, Foot, Vector3.zero, 1.5f, 0.25f);
                    ImpulseSelfRpc(Vector3.up * 5f, 0f);
                    foreach (var p in new List<NBPlayer>(Enemies()))
                    {
                        var to = p.Center - Center;
                        if (p.DistToBody(Center) > def.range || Vector3.Angle(flat, new Vector3(to.x, 0, to.z)) > 80f) continue;
                        ApplyHit(p, this, def.damage, flat * def.knock + Vector3.up * def.lift, def.stun, def.unblockable, def.guardBreak, true, p.Center);
                    }
                    break;
                }
                case MoveKind.Slam:
                {
                    if (def.delay > 0)
                    {
                        FxRpc(FxKind.Marker, Foot, Vector3.zero, def.radius, def.delay);
                        if (slot == 4) ImpulseSelfRpc(Vector3.up * 3f, def.delay + 0.2f);
                        yield return new WaitForSeconds(def.delay);
                    }
                    if (Dead.Value) break;
                    FxRpc(FxKind.Explosion, Foot, Vector3.zero, def.radius, 0.4f);
                    HitArea(def, Foot + Vector3.up * 0.5f, def.radius, true, flat);
                    break;
                }
                case MoveKind.Beam:
                {
                    var o = origin; var d = dir;
                    FxRpc(FxKind.Beam, o, d, def.range, def.duration);
                    ImpulseSelfRpc(Vector3.zero, def.delay + def.duration);
                    yield return new WaitForSeconds(def.delay);
                    for (float t = 0; t < def.duration; t += 0.1f)
                    {
                        if (Dead.Value) break;
                        foreach (var p in new List<NBPlayer>(Enemies()))
                        {
                            var c = p.Center;
                            float along = Mathf.Clamp(Vector3.Dot(c - o, d), 0, def.range);
                            if (Vector3.Distance(c, o + d * along) > def.radius + 0.35f) continue;
                            ApplyHit(p, this, def.damage, d * def.knock + Vector3.up * def.lift, def.stun, def.unblockable, false, false, c);
                        }
                        yield return new WaitForSeconds(0.1f);
                    }
                    break;
                }
                case MoveKind.Teleport:
                {
                    var target = FindTarget(origin, dir, def.range, 30f) ?? Nearest(def.range * 0.6f);
                    if (target == null) { RefundCooldown(slot); break; }
                    var behind = target.Foot - target.Forward * 1.1f;
                    FxRpc(FxKind.Blink, Center, behind + Vector3.up, 1f, 0f);
                    TeleportSelfRpc(behind, target.Foot - behind);
                    yield return new WaitForSeconds(0.15f);
                    if (target.Alive)
                    {
                        var push = target.Foot - behind; push.y = 0; push = push.sqrMagnitude < 0.01f ? flat : push.normalized;
                        ApplyHit(target, this, def.damage, push * def.knock + Vector3.up * def.lift, def.stun, def.unblockable, true, true, target.Center);
                    }
                    break;
                }
                case MoveKind.Buff:
                {
                    Buff.Value = (int)def.buff;
                    BuffEnd.Value = Now + def.duration;
                    FxRpc(FxKind.Buff, Center, Vector3.zero, 1f, 0f);
                    break;
                }
                case MoveKind.Meteor:
                {
                    var point = AimPoint(origin, dir, def.range);
                    FxRpc(FxKind.Marker, point, Vector3.zero, def.radius, def.delay);
                    yield return new WaitForSeconds(def.delay);
                    FxRpc(FxKind.Explosion, point, Vector3.zero, def.radius, 0.5f);
                    HitArea(def, point + Vector3.up * 0.5f, def.radius, true, flat);
                    break;
                }
                case MoveKind.Pull:
                {
                    foreach (var p in new List<NBPlayer>(Enemies()))
                    {
                        var to = p.Center - Center;
                        if (to.magnitude > def.range || Vector3.Angle(flat, new Vector3(to.x, 0, to.z)) > 45f) continue;
                        FxRpc(FxKind.Pull, Center, p.Center, 0.3f, 0f);
                        var back = -new Vector3(to.x, 0, to.z).normalized;
                        float strength = Mathf.Clamp(to.magnitude * 1.4f, 4f, def.knock);
                        ApplyHit(p, this, def.damage, back * strength + Vector3.up * def.lift, def.stun, true, false, true, p.Center);
                    }
                    FxRpc(FxKind.Burst, Center + flat, Vector3.zero, 1.2f, 0f);
                    break;
                }
                case MoveKind.Flurry:
                {
                    var target = FindTarget(origin, dir, def.range, 40f) ?? Nearest(def.range);
                    if (target == null) { RefundCooldown(slot); break; }
                    for (int i = 0; i < def.count && target.Alive && !Dead.Value; i++)
                    {
                        var toMe = Foot - target.Foot; toMe.y = 0;
                        toMe = toMe.sqrMagnitude < 0.01f ? -target.Forward : toMe.normalized;
                        var side = Quaternion.Euler(0, (i % 2 == 0 ? 25f : -25f), 0) * toMe;
                        var pos = target.Foot + side * 1.2f;
                        FxRpc(FxKind.Blink, Center, pos + Vector3.up, 1f, 0f);
                        TeleportSelfRpc(pos, i == 0 ? target.Foot - pos : Vector3.zero);
                        bool last = i == def.count - 1;
                        var push = (target.Foot - pos); push.y = 0; push.Normalize();
                        ApplyHit(target, this, def.damage * (last ? 3f : 1f), last ? push * def.knock + Vector3.up * def.lift : Vector3.up * 1.5f,
                                 last ? 1.2f : def.stun, true, false, last, target.Center);
                        yield return new WaitForSeconds(def.duration / def.count);
                    }
                    break;
                }
            }
        }

        Vector3 AimPoint(Vector3 origin, Vector3 dir, float range)
        {
            if (Physics.Raycast(origin, dir, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;
            var p = origin + dir * range;
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, 0, p.z);
        }

        public static void ApplyHit(NBPlayer victim, NBPlayer attacker, float dmg, Vector3 knock, float stun,
                                    bool unblockable, bool guardBreak, bool heavy, Vector3 fxPos)
        {
            if (victim == null || !victim.IsServer || !victim.Alive) return;
            double now = Now;
            if (now < victim.m_IFrameUntil) return;
            bool blocked = false;

            if (attacker != null)
            {
                if (attacker.Awakened) dmg *= 1.3f;
                if (attacker.ActiveBuff == BuffKind.Power) dmg *= 1.4f;
            }
            if (victim.ActiveBuff == BuffKind.Armor)
            {
                dmg *= 0.45f; stun = 0f; knock *= 0.25f;
            }
            else if (victim.Blocking.Value && !unblockable)
            {
                if (guardBreak)
                {
                    stun = 1.5f; dmg *= 0.6f; knock *= 0.3f;
                    victim.FxRpc(FxKind.GuardBreak, victim.HeadPos + Vector3.up * 0.5f, Vector3.zero, 1f, 0f);
                }
                else
                {
                    blocked = true; dmg *= 0.15f; knock *= 0.1f; stun = 0f;
                }
            }

            victim.Health.Value = Mathf.Max(0f, victim.Health.Value - dmg);
            if (attacker != null && attacker != victim && !attacker.Awakened)
                attacker.Ult.Value = Mathf.Min(NBFighters.UltMax, attacker.Ult.Value + dmg * 0.9f);
            if (!victim.Awakened)
                victim.Ult.Value = Mathf.Min(NBFighters.UltMax, victim.Ult.Value + dmg * 0.35f);

            if (attacker != null && attacker != victim) { victim.m_LastAttacker = attacker; victim.m_LastHitTime = now; }
            if (stun > 0) victim.m_SrvStunUntil = System.Math.Max(victim.m_SrvStunUntil, now + stun);

            victim.KnockRpc(knock, stun);
            victim.HitFxRpc(fxPos, dmg, blocked, attacker != null ? attacker.FighterId.Value : 0, heavy);

            if (victim.Health.Value <= 0f) victim.ServerDie();
        }

        void ServerDie()
        {
            if (Dead.Value) return;
            Dead.Value = true;
            Health.Value = 0;
            Deaths.Value++;
            m_Combo = 0;
            var killer = (m_LastAttacker != null && m_LastAttacker.IsSpawned && Now - m_LastHitTime < 12.0) ? m_LastAttacker : null;
            if (killer != null)
            {
                killer.Kills.Value++;
                killer.Ult.Value = Mathf.Min(NBFighters.UltMax, killer.Ult.Value + 25f);
            }
            KillFeedRpc(killer != null ? killer.Name : "THE VOID", Name, false);
            StartCoroutine(RespawnAfter(NBFighters.RespawnTime));
        }

        IEnumerator RespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!IsSpawned) yield break;
            Health.Value = NBFighters.MaxHealth;
            AwakenEnd.Value = 0;
            BuffEnd.Value = 0;
            m_SrvStunUntil = 0;
            m_IFrameUntil = Now + 2.0;
            m_LastAttacker = null;
            var spawn = NBArena.PickSpawn(this);
            TeleportSelfRpc(spawn, -spawn);
            yield return new WaitForSeconds(0.2f);
            Dead.Value = false;
        }

        // a gift for testing in the editor: fill your ult bar
        public void ServerDebugFillUlt() { if (IsServer) Ult.Value = NBFighters.UltMax; }
    }
}
