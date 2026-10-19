using Fusion;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Injuries and grabbing.
    /// DAZED (stars over the head, ~2.5 s): from a combo finisher, a guard break, or a big hit when low on health. You
    /// move slowly and can't block.
    /// BROKEN ARM (~10 s, the arm hangs limp): from a guard break, or blocking a super. That fist hits for a third, and
    /// you can't block.
    /// GRAB: squeeze the grip with your hand on someone who is dazed or has a broken arm. They're stuck in your hand;
    /// punch them with the other fist for extra damage, or let go while swinging to THROW them. Grab them by the head
    /// and yank your hand away hard when they're dazed or under a third of their health to RIP their head off. A hard
    /// punch to the head that knocks someone out can also knock their head clean off. (Heads only come off with
    /// Settings > Gore on; with gore off it's a confetti K.O.)
    /// Everything is decided on the hurt fighter's own game, like normal hits.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>Injuries everyone sees: 1 left arm broken, 2 right arm broken, 4 head off (until they respawn).</summary>
        [Networked] public byte Injury { get; set; }
        public const byte Dazed = 128;
        public const byte LeftArmBroken = 1, RightArmBroken = 2, HeadOff = 4;
        public const float DazeTime = 2.5f, ArmBreakTime = 10f, GrabTime = 4f, HeldDamage = 1.35f;

        float dazedUntil, leftArmUntil, rightArmUntil;
        public bool IsDazed => HasStatus(Dazed);
        public bool ArmBroken(bool left) => (Injury & (left ? LeftArmBroken : RightArmBroken)) != 0;
        public bool Grabbable => IsReady && !Down && (IsDazed || (Injury & (LeftArmBroken | RightArmBroken)) != 0);

        // ---- being held (on the held fighter's own game)
        Fighter heldBy;
        bool heldLeft, heldByHead;
        float heldUntil;
        Vector3 heldOffset;
        public bool IsHeld => heldBy != null && Time.time < heldUntil;

        // ---- holding someone (on the grabber's game)
        Fighter holding;
        bool holdingLeft, holdingHead;
        float holdingSince;
        bool gripWasL, gripWasR;
        public Fighter Holding => holding;

        void Daze(float seconds) { dazedUntil = Mathf.Max(dazedUntil, Time.time + seconds); }

        void BreakArm(bool left)
        {
            if (left) leftArmUntil = Time.time + ArmBreakTime; else rightArmUntil = Time.time + ArmBreakTime;
            AudioManager.PlayAt(AudioManager.Sfx.HeavyPunch, left ? LeftPos : RightPos, 1f, 0.6f);
            Fx.Popup((left ? LeftPos : RightPos) + Vector3.up * 0.2f, "ARM BROKEN!", new Color(1f, 0.3f, 0.3f), 1.1f);
        }

        /// <summary>Owner: injuries wear off; status bits for everyone.</summary>
        void UpdateInjuries(float now)
        {
            byte inj = (byte)(Injury & HeadOff);
            if (now < leftArmUntil) inj |= LeftArmBroken;
            if (now < rightArmUntil) inj |= RightArmBroken;
            if (inj != Injury) Injury = inj;
        }

        /// <summary>Injury rules for a hit that just landed (owner, from RPC_Hit).</summary>
        void InjuryFromHit(HitFlags flags, float damage)
        {
            bool blocked = (flags & HitFlags.Blocked) != 0;
            if ((flags & HitFlags.GuardBreak) != 0)
            {
                Daze(DazeTime);
                BreakArm(Random.value < 0.5f);
                return;
            }
            if (blocked && (flags & HitFlags.Special) != 0) BreakArm(Random.value < 0.5f);
            if (blocked) return;
            if ((flags & HitFlags.Launch) != 0 && (flags & HitFlags.Special) == 0) Daze(DazeTime);     // combo finisher
            else if ((flags & HitFlags.Heavy) != 0 && Health < Stats.maxHealth * 0.25f) Daze(2f);     // reeling
        }

        // ================================================================== grabbing (grabber side)

        void DetectGrabs()
        {
            if (IsBot || poseLeft == null || poseRight == null) return;
            bool gl = FightInput.LeftGripHeld, gr = FightInput.RightGripHeld;
            if (holding != null)
            {
                bool still = holdingLeft ? gl : gr;
                Vector3 hand = holdingLeft ? poseLeft.position : poseRight.position;
                float handSpeed = holdingLeft ? LeftHandSpeed : RightHandSpeed;
                if (!holding.IsReady || holding.Down) { holding = null; }
                else if (holdingHead && handSpeed > 3.5f && Time.time - holdingSince > 0.25f &&
                         Vector3.Dot(hand - holding.HeadPos, (holdingLeft ? lastHandVelL : lastHandVelR)) > 0f &&
                         (holding.IsDazed || holding.Health < holding.Stats.maxHealth * 0.34f))
                {
                    // Yanked hard away from them: rip.
                    holding.RPC_Rip(this);
                    FightInput.Buzz(holdingLeft, 1f, 0.3f);
                    holding = null;
                }
                else if (!still || Time.time - holdingSince > GrabTime)
                {
                    // Let go: whatever speed your hand had becomes a throw.
                    Vector3 throwVel = Vector3.ClampMagnitude((holdingLeft ? lastHandVelL : lastHandVelR) * 1.6f, 18f);
                    holding.RPC_Release(throwVel);
                    holding = null;
                }
            }
            else
            {
                if (gl && !gripWasL) TryGrab(true, poseLeft.position);
                else if (gr && !gripWasR) TryGrab(false, poseRight.position);
            }
            gripWasL = gl; gripWasR = gr;
        }

        Vector3 lastHandVelL, lastHandVelR;

        void TryGrab(bool left, Vector3 hand)
        {
            if (ArmBroken(left)) return;
            foreach (var f in All)
            {
                if (f == this || !f.Grabbable) continue;
                float toHead = Vector3.Distance(hand, f.HeadPos), toBody = Vector3.Distance(hand, f.Center);
                if (Mathf.Min(toHead, toBody) > 0.35f) continue;
                holding = f;
                holdingLeft = left;
                holdingHead = toHead < toBody;
                holdingSince = Time.time;
                f.RPC_Grab(this, left, holdingHead);
                FightInput.Buzz(left, 0.8f, 0.12f);
                AudioManager.PlayAt(AudioManager.Sfx.Block, hand, 0.8f, 0.7f);
                Fx.Popup(hand + Vector3.up * 0.2f, "GRABBED!", new Color(1f, 0.8f, 0.3f), 0.9f);
                return;
            }
        }

        // ================================================================== being grabbed (held fighter's owner)

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Grab(Fighter by, NetworkBool left, NetworkBool byHead, RpcInfo info = default)
        {
            if (by == null || Down || !(IsDazed || (Injury & (LeftArmBroken | RightArmBroken)) != 0)) return;
            if (!IsBot && ModPowers.InvincibleNow) return;
            heldBy = by;
            heldLeft = left;
            heldByHead = byHead;
            heldUntil = Time.time + GrabTime + 0.5f;
            heldOffset = (byHead ? HeadPos : Center) - transform.position;
            flyUntil = 0f;
            if (!IsBot) { ComfortVignette.Kick(1f); FightInput.Buzz(true, 0.6f, 0.2f); FightInput.Buzz(false, 0.6f, 0.2f); }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Release(Vector3 throwVelocity, RpcInfo info = default)
        {
            if (heldBy == null) return;
            heldBy = null;
            throwVelocity = Vector3.ClampMagnitude(throwVelocity, 18f);
            if (throwVelocity.magnitude > 4f)
            {
                knock += new Vector3(throwVelocity.x, 0f, throwVelocity.z);
                velocity.y = Mathf.Max(velocity.y, throwVelocity.y + 2f);
                grounded = false;
                tumbleUntil = Time.time + 1f; // thrown: rolls on landing
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Rip(Fighter by, RpcInfo info = default)
        {
            if (Down || heldBy == null || !heldByHead) return;
            if (!IsBot && ModPowers.InvincibleNow) return;
            if (!(IsDazed || Health < Stats.maxHealth * 0.34f)) return;
            heldBy = null;
            Health = 0f;
            if (by != null && by != this) { lastAttacker = by; lastAttackedAt = Time.time; }
            KnockOut(by, true);
        }

        /// <summary>Held: pinned in the grabber's hand (no walking, no gravity). Returns true while held.</summary>
        bool HeldStep(float dt)
        {
            if (heldBy == null) return false;
            if (Time.time >= heldUntil || !heldBy.IsReady || heldBy.Down || Down) { heldBy = null; return false; }
            Vector3 hand = heldLeft ? heldBy.LeftPos : heldBy.RightPos;
            Vector3 want = hand - heldOffset;
            Vector3 delta = Vector3.ClampMagnitude(want - transform.position, 6f * dt + 0.05f);
            velocity = Vector3.zero;
            knock = Vector3.zero;
            stunUntil = Mathf.Max(stunUntil, Time.time + 0.15f);
            Blocking = false;
            MoveBody(delta);
            return true;
        }
    }
}
