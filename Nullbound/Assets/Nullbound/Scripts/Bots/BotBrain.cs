using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// An AI fighter for Training and private lobbies. It runs on the game that spawned it and fights with the same
    /// rules as players: it has a puppet head and two hands, and its punches are real swings that the normal punch
    /// code detects. It chases the nearest enemy, throws short combos, blocks and dodges, and uses its special.
    ///
    /// It LEARNS while you fight it: every clean hit you land makes it a bit better at blocking (up to its
    /// difficulty's limit), and if your special catches it, it gets better at dodging specials when it sees you
    /// start one.
    /// </summary>
    public class BotBrain : MonoBehaviour
    {
        /// <summary>0 = easy, 1 = normal, 2 = hard (from the menu, for bots added after the change).</summary>
        public static int Difficulty = 1;

        static readonly string[] Names = { "Rex", "Nova", "Blitz", "Kaiju", "Vex", "Echo", "Rook", "Zen", "Brick", "Volt", "Onyx", "Fang" };
        public static string RandomName() => Names[Random.Range(0, Names.Length)] + Random.Range(10, 99);

        Fighter me;
        Transform root, head, left, right;
        Vector3 guardL = new Vector3(-0.18f, 1.38f, 0.32f), guardR = new Vector3(0.18f, 1.38f, 0.32f);
        Vector3 blockL = new Vector3(-0.09f, 1.55f, 0.24f), blockR = new Vector3(0.09f, 1.55f, 0.24f);

        // Skill (changes as it learns).
        float blockSkill, maxBlockSkill, dodgeSkill, specialDodgeSkill, learnRate, attackGap;
        int comboMax;
        public int Level => Mathf.RoundToInt(blockSkill * 10f + specialDodgeSkill * 5f);

        Fighter target;
        float nextThink, nextAttack, blockUntil, nextThreatRoll, strafeSign = 1f, nextStrafeFlip, stuckSince;
        int seenSpecials;
        float lastTargetSpecialAt = -10f;
        Vector3 lastPos;

        // Punch animation.
        enum PunchPhase { None, Windup, Strike, Recover }
        PunchPhase phase;
        bool punchLeft;
        float phaseStart, strikeTime;
        int punchesLeft;
        Vector3 strikeFrom, strikeTo;

        void Start()
        {
            me = GetComponent<Fighter>();
            int d = Mathf.Clamp(Difficulty, 0, 2);
            blockSkill = new[] { 0.08f, 0.2f, 0.35f }[d];
            maxBlockSkill = new[] { 0.4f, 0.65f, 0.85f }[d];
            dodgeSkill = new[] { 0.03f, 0.1f, 0.2f }[d];
            learnRate = new[] { 0.025f, 0.045f, 0.07f }[d];
            attackGap = new[] { 1.5f, 0.95f, 0.55f }[d];
            comboMax = new[] { 2, 3, 4 }[d];

            // The puppet: a head and two hands that the fighter reads like a VR rig.
            root = new GameObject("BotPuppet").transform;
            root.SetParent(transform, false);
            head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, 1.62f, 0f);
            left = new GameObject("LeftHand").transform;
            left.SetParent(root, false);
            left.localPosition = guardL;
            right = new GameObject("RightHand").transform;
            right.SetParent(root, false);
            right.localPosition = guardR;
            me.poseRoot = root;
            me.poseHead = head;
            me.poseLeft = left;
            me.poseRight = right;
            me.HitTaken += OnHitTaken;
            lastPos = transform.position;
        }

        void OnDestroy()
        {
            if (me != null) me.HitTaken -= OnHitTaken;
        }

        /// <summary>Learning: clean hits teach it to block; getting caught by a special teaches it to dodge them.</summary>
        void OnHitTaken(Fighter attacker, bool blocked)
        {
            if (blocked) return;
            blockSkill = Mathf.Min(maxBlockSkill, blockSkill + learnRate);
            dodgeSkill = Mathf.Min(maxBlockSkill * 0.6f, dodgeSkill + learnRate * 0.4f);
            if (Time.time - lastTargetSpecialAt < 1.5f)
                specialDodgeSkill = Mathf.Min(maxBlockSkill, specialDodgeSkill + 0.2f);
        }

        void Update()
        {
            if (me == null || !me.IsMine) return;
            var cmd = new FighterCommand();
            if (me.Down)
            {
                phase = PunchPhase.None;
                me.command = cmd;
                return;
            }

            if (Time.time >= nextThink)
            {
                nextThink = Time.time + 0.25f;
                target = FindTarget();
            }

            if (target == null)
            {
                // Nobody to fight: wander back to the middle.
                Vector3 home = SpawnPoint.MapCenter() - transform.position;
                home.y = 0f;
                if (home.magnitude > 3f) cmd.move = home.normalized * 0.5f;
                Face(transform.position + (home.sqrMagnitude > 0.01f ? home : Vector3.forward));
                AnimateHands();
                me.command = cmd;
                return;
            }

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
            Face(target.transform.position);

            // Watch for their special starting.
            if (target.SpecialCount != seenSpecials)
            {
                seenSpecials = target.SpecialCount;
                lastTargetSpecialAt = Time.time;
                if (dist < 8f && Random.value < specialDodgeSkill) { cmd.dash = true; cmd.move = -dir; }
            }

            // Incoming punch: maybe block or dodge (more likely the more it has learned).
            bool threat = dist < 1.6f && Mathf.Max(target.LeftHandSpeed, target.RightHandSpeed) > 1.8f;
            if (threat && Time.time >= nextThreatRoll && phase == PunchPhase.None)
            {
                nextThreatRoll = Time.time + 0.45f;
                float roll = Random.value;
                if (roll < blockSkill) blockUntil = Time.time + Random.Range(0.5f, 0.9f);
                else if (roll < blockSkill + dodgeSkill && me.DashReady >= 1f)
                {
                    cmd.dash = true;
                    cmd.move = (Random.value < 0.5f ? 1f : -1f) * Vector3.Cross(Vector3.up, dir) * 0.8f - dir * 0.6f;
                }
            }
            bool blocking = Time.time < blockUntil && !me.GuardBroken;
            cmd.block = blocking;

            // Movement: close in to punching range, circle a little once there.
            if (!cmd.dash)
            {
                if (Time.time > nextStrafeFlip) { strafeSign = Random.value < 0.5f ? -1f : 1f; nextStrafeFlip = Time.time + Random.Range(1f, 2.5f); }
                Vector3 side = Vector3.Cross(Vector3.up, dir) * strafeSign;
                if (dist > 1.05f) cmd.move = (dir + side * 0.25f).normalized;
                else if (dist < 0.6f) cmd.move = -dir * 0.6f;
                else cmd.move = side * 0.35f;
            }

            // Stuck behind something: hop.
            float moved = (transform.position - lastPos).magnitude;
            lastPos = transform.position;
            if (cmd.move.sqrMagnitude > 0.2f && moved < 0.002f) { if (stuckSince < 0f) stuckSince = Time.time; }
            else stuckSince = -1f;
            if (stuckSince > 0f && Time.time - stuckSince > 0.6f) { cmd.jump = true; stuckSince = -1f; }

            // Special.
            if (me.SpecialReady >= 1f && !blocking)
            {
                switch (me.Stats.specialType)
                {
                    case SpecialType.GroundSlam: case SpecialType.TitanQuake: case SpecialType.Whirlwind:
                    case SpecialType.Uppercut: case SpecialType.Shockwave: case SpecialType.Clones:
                        cmd.special = dist < 3f; break;                                    // up close
                    case SpecialType.Heal:
                        cmd.special = me.HealthFraction < 0.45f; break;
                    case SpecialType.Barrier:
                        cmd.special = threat && me.HealthFraction < 0.7f; break;
                    default:
                        cmd.special = dist > 3.5f && dist < 12f; break;                    // from range
                }
            }

            // Fly into them from range now and then.
            if (!blocking && dist > 6f && dist < 20f && me.FlyReady >= 1f && Random.value < 0.5f * Time.deltaTime) cmd.fly = true;

            // Attack strings.
            if (!blocking && phase == PunchPhase.None && dist < 1.35f && Time.time >= nextAttack && !target.Down)
            {
                punchesLeft = Random.Range(1, comboMax + 1);
                StartPunch();
                nextAttack = Time.time + attackGap * Random.Range(0.8f, 1.3f);
            }

            AnimateHands(blocking);
            me.command = cmd;
        }

        Fighter FindTarget()
        {
            Fighter best = null;
            float bestD = 40f;
            foreach (var f in Fighter.All)
            {
                if (f == me || !f.IsReady || f.Down || f.Hidden && f.IsVerifiedMod || f.HasStatus(Fighter.Immune)) continue;
                if (f.IsBot && SessionManager.Instance != null && !SessionManager.Instance.BotsFightEachOther) continue;
                float d = Vector3.Distance(f.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(d), 540f * Time.deltaTime);
        }

        void StartPunch()
        {
            punchLeft = Random.value < 0.5f;
            phase = PunchPhase.Windup;
            phaseStart = Time.time;
            // Faster swings on harder bots, and sometimes a big one.
            strikeTime = Random.value < 0.25f ? 0.08f : Random.Range(0.11f, 0.16f);
        }

        void AnimateHands(bool blocking = false)
        {
            Transform hand = punchLeft ? left : right;
            Vector3 guard = punchLeft ? guardL : guardR;
            float t = Time.time - phaseStart;
            switch (phase)
            {
                case PunchPhase.Windup:
                    hand.localPosition = Vector3.Lerp(hand.localPosition, guard + new Vector3(0f, 0.02f, -0.12f), Time.deltaTime * 20f);
                    if (t > 0.1f)
                    {
                        phase = PunchPhase.Strike;
                        phaseStart = Time.time;
                        strikeFrom = hand.localPosition;
                        Vector3 aim = target != null ? root.InverseTransformPoint(target.Center + Random.insideUnitSphere * 0.08f) : guard + Vector3.forward * 0.6f;
                        Vector3 shoulder = new Vector3(punchLeft ? -0.2f : 0.2f, 1.42f, 0f);
                        Vector3 reach = aim - shoulder;
                        strikeTo = shoulder + Vector3.ClampMagnitude(reach + reach.normalized * 0.15f, 0.8f);
                    }
                    break;
                case PunchPhase.Strike:
                    hand.localPosition = Vector3.Lerp(strikeFrom, strikeTo, Mathf.Clamp01(t / strikeTime));
                    if (t >= strikeTime) { phase = PunchPhase.Recover; phaseStart = Time.time; }
                    break;
                case PunchPhase.Recover:
                    hand.localPosition = Vector3.Lerp(hand.localPosition, guard, Time.deltaTime * 12f);
                    if (t > 0.16f)
                    {
                        if (--punchesLeft > 0) StartPunch();
                        else phase = PunchPhase.None;
                    }
                    break;
            }
            // The other hand (or both, when not punching) holds a guard; up at the face while blocking.
            float k = Time.deltaTime * 14f;
            if (phase == PunchPhase.None || !punchLeft) left.localPosition = Vector3.Lerp(left.localPosition, blocking ? blockL : guardL, k);
            if (phase == PunchPhase.None || punchLeft) right.localPosition = Vector3.Lerp(right.localPosition, blocking ? blockR : guardR, k);
            left.localRotation = right.localRotation = Quaternion.identity;
            head.localRotation = Quaternion.identity;
        }
    }
}
