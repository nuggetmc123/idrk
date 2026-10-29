using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>A simple bot for offline matches: roams, fights whoever it can see, plays CTF.</summary>
    public class BotController : MonoBehaviour
    {
        public RemotePlayer view;
        public IBotHost server;
        float nextShare;
        public GunDef gun;
        Vector3 pos, vel, goal;
        float yaw, aimYaw, aimPitch;
        float nextThink, nextShot, stuckTimer, strafeSign = 1f, reaction;
        Transform targetT;
        string targetId;
        Vector3 targetPoint;
        bool targetVisible;
        Vector3 lastPos;
        float vy;

        public Vector3 Pos => pos;

        public void Init(RemotePlayer v, IBotHost s)
        {
            view = v;
            server = s;
            var r = Random.value;
            gun = r < 0.45f ? GunCatalog.All[Random.Range(16, 22)] : r < 0.65f ? GunCatalog.All[Random.Range(6, 11)] : r < 0.8f ? GunCatalog.All[Random.Range(11, 16)] : GunCatalog.All[Random.Range(0, 6)];
            strafeSign = Random.value < 0.5f ? -1 : 1;
        }

        public void Respawn(int spawnIndex)
        {
            var w = Game.I.world;
            w.Spawn(view.team, spawnIndex, out pos, out yaw);
            aimYaw = yaw;
            vel = Vector3.zero;
            vy = 0;
            PickGoal();
            PushPose(true);
        }

        /// <summary>Carry on from where this bot already is (another player was driving it).</summary>
        public void TakeOver()
        {
            pos = view.FeetPos;
            yaw = aimYaw = view.transform.eulerAngles.y;
            vel = Vector3.zero;
            PickGoal();
        }

        void PickGoal()
        {
            var w = Game.I.world;
            var list = w.ffaSpawns.Count > 0 ? w.ffaSpawns : new List<Vector3> { Vector3.zero };
            goal = list[Random.Range(0, list.Count)] + new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
            var s = Game.I.session;
            if (s.Mode == "ctf" && view.team >= 0)
            {
                int enemy = 1 - view.team;
                if (server.FlagCarrier(enemy) == view.id) goal = w.flagBase[view.team];
                else if (server.FlagState(view.team) == "dropped" && server.FlagPos(view.team).HasValue && Random.value < 0.5f) goal = server.FlagPos(view.team).Value;
                else if (Random.value < 0.6f) goal = server.FlagState(enemy) == "dropped" && server.FlagPos(enemy).HasValue ? server.FlagPos(enemy).Value : w.flagBase[enemy];
            }
        }

        void Update()
        {
            if (view == null || !view.alive || Game.I == null || Game.I.world == null) return;
            var w = Game.I.world;
            var s = Game.I.session;
            if (s.State == "ended") { PushPose(false); return; }
            float dt = Time.deltaTime;

            if (Time.time > nextThink) { Think(); nextThink = Time.time + 0.35f + Random.value * 0.2f; }

            // movement
            Vector3 dir;
            float speed = 3.2f;
            if (targetVisible && targetT != null)
            {
                var to = targetPoint - pos; to.y = 0;
                float d = to.magnitude;
                var side = Vector3.Cross(Vector3.up, to.normalized) * strafeSign;
                dir = (d > 14f ? to.normalized : d < 6f ? -to.normalized * 0.6f : Vector3.zero) + side * 0.8f;
                speed = 2.6f;
                if (Random.value < dt * 0.4f) strafeSign = -strafeSign;
            }
            else
            {
                dir = goal - pos; dir.y = 0;
                if (dir.magnitude < 2f) { PickGoal(); dir = goal - pos; dir.y = 0; }
                speed = 4.2f;
            }
            if (dir.sqrMagnitude > 0.01f) dir.Normalize();
            // steer around walls
            var eye = pos + Vector3.up * 0.6f;
            if (dir.sqrMagnitude > 0.01f && Physics.Raycast(eye, dir, 1.2f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var left = Quaternion.Euler(0, -70, 0) * dir;
                var right = Quaternion.Euler(0, 70, 0) * dir;
                bool lBlocked = Physics.Raycast(eye, left, 1.5f, Layers.ShootMask, QueryTriggerInteraction.Ignore);
                dir = !lBlocked ? left : right;
            }
            vel = Vector3.MoveTowards(vel, dir * speed, dt * 12f);
            var next = pos + vel * dt;
            // stay on the ground (step up small ledges, fall off big ones)
            // (cast from below the bot's own hitboxes so it never stands on itself)
            if (Physics.Raycast(next + Vector3.up * 0.5f, Vector3.down, out var hit, 6f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                float groundY = hit.point.y;
                if (groundY > pos.y + 0.6f) { next = pos; PickGoal(); }
                else if (groundY >= next.y - 0.05f) { next.y = groundY; vy = 0; }
                else { vy -= 9.81f * w.gravity * dt; next.y = Mathf.Max(groundY, pos.y + vy * dt); }
            }
            else { vy -= 9.81f * w.gravity * dt; next.y = pos.y + vy * dt; }
            pos = next;
            if (pos.y < w.killY) { server.BotFell(view.id); return; }
            if (w.OutOfBounds(pos)) { Respawn(Random.Range(0, 1000)); return; }

            // stuck?
            if ((pos - lastPos).sqrMagnitude < 0.0004f && vel.sqrMagnitude > 1f) { stuckTimer += dt; if (stuckTimer > 1f) { PickGoal(); stuckTimer = 0; } }
            else stuckTimer = 0;
            lastPos = pos;

            // look and aim
            float wantYaw = targetVisible ? Quaternion.LookRotation(Flat(targetPoint - pos)).eulerAngles.y : (vel.sqrMagnitude > 0.2f ? Quaternion.LookRotation(Flat(vel)).eulerAngles.y : yaw);
            aimYaw = Mathf.MoveTowardsAngle(aimYaw, wantYaw, dt * 220f);
            yaw = Mathf.MoveTowardsAngle(yaw, aimYaw, dt * 300f);
            aimPitch = targetVisible ? -Mathf.Atan2(targetPoint.y - (pos.y + 1.5f), Flat(targetPoint - pos).magnitude) * Mathf.Rad2Deg : 0f;

            if (targetVisible && Time.time > nextShot && Time.time > reaction && Mathf.Abs(Mathf.DeltaAngle(aimYaw, wantYaw)) < 12f && s.State != "countdown")
                Shoot();

            server.SetPos(view.id, pos);
            CtfChecks();
            PushPose(false);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 0.0001f ? Vector3.forward : v; }

        void Think()
        {
            var s = Game.I.session;
            bool teams = s.Mode != null && LocalServer.Modes.TryGetValue(s.Mode, out var md) && md.teams;
            var eye = pos + Vector3.up * 1.55f;
            Transform best = null;
            string bestId = null;
            Vector3 bestPoint = Vector3.zero;
            float bestD = 45f;
            var rig = PlayerRig.I;
            var me = s.Me;
            if (rig != null && rig.Alive && me != null && (!teams || me.team != view.team))
            {
                var p = rig.head.position + Vector3.down * 0.35f;
                float d = Vector3.Distance(eye, p);
                if (d < bestD && CanSee(eye, p, rig.transform)) { best = rig.transform; bestId = s.MyId; bestPoint = p; bestD = d; }
            }
            foreach (var other in Game.I.Remotes)
            {
                if (other == view || !other.alive) continue;
                if (teams && other.team == view.team) continue;
                var p = other.ChestPos;
                float d = Vector3.Distance(eye, p);
                if (d < bestD && CanSee(eye, p, other.transform)) { best = other.transform; bestId = other.id; bestPoint = p; bestD = d; }
            }
            bool wasVisible = targetVisible;
            targetVisible = best != null;
            if (targetVisible && (!wasVisible || bestId != targetId)) reaction = Time.time + Random.Range(0.35f, 0.7f);
            targetT = best;
            targetId = bestId;
            targetPoint = bestPoint;
            if (!targetVisible && Random.value < 0.15f) PickGoal();
        }

        bool CanSee(Vector3 eye, Vector3 p, Transform target)
        {
            if (!Physics.Linecast(eye, p, out var hit, Layers.ShootMask, QueryTriggerInteraction.Ignore)) return true;
            if (hit.collider.transform.IsChildOf(target)) return true;
            if (hit.collider.transform.IsChildOf(view.transform)) return true;
            return false;
        }

        void Shoot()
        {
            nextShot = Time.time + Mathf.Max(gun.Interval, gun.mode == FireMode.Auto ? 0.12f : 0.4f) * Random.Range(1f, 1.6f);
            var gm = view.HeldGun(1);
            var from = gm != null ? gm.muzzle.position : pos + Vector3.up * 1.3f;
            float d = Vector3.Distance(from, targetPoint);
            float chance = Mathf.Clamp(0.72f - d * 0.014f, 0.12f, 0.65f);
            if (gun.cls == GunClass.Shotgun) chance = d < 12f ? 0.75f : 0.15f;
            if (gun.cls == GunClass.Sniper) chance = Mathf.Clamp(chance + 0.15f, 0.2f, 0.7f);
            if (PlayerRig.I != null && targetT == PlayerRig.I.transform && PlayerRig.I.Sprinting) chance *= 0.7f;
            bool hitIt = Random.value < chance;
            var end = hitIt ? targetPoint : targetPoint + Random.insideUnitSphere * Mathf.Max(0.6f, d * 0.05f);
            view.PlayShot(1, from, end, gun.index);
            server.BotShot(view.id, 1, from, end, gun.index);
            if (!hitIt) return;
            bool head = Random.value < 0.12f;
            float dmg = gun.damage * (gun.cls == GunClass.Shotgun ? gun.pellets * Random.Range(0.35f, 0.75f) : 1f) * (head ? gun.headMult : 1f) * 0.85f;
            var dir = (targetPoint - from).normalized;
            if (targetId == Game.I.session.MyId) Fx.I.Blood(targetPoint + dir * -0.1f, dir, 0.6f);
            else Fx.I.Blood(targetPoint, dir, 0.8f);
            server.ApplyHit(view.id, targetId, dmg, gun.id, head, new[] { dir.x, dir.y, dir.z });
        }

        void CtfChecks()
        {
            var s = Game.I.session;
            if (s.Mode != "ctf" || view.team < 0 || s.State != "live") return;
            var w = Game.I.world;
            int enemy = 1 - view.team;
            var enemyFlagPos = server.FlagState(enemy) == "dropped" && server.FlagPos(enemy).HasValue ? server.FlagPos(enemy).Value : w.flagBase[enemy];
            if (server.FlagState(enemy) != "carried" && Flat(enemyFlagPos - pos).magnitude < 1.6f) { server.FlagAction(view.id, "touch", enemy); PickGoal(); }
            if (server.FlagState(view.team) == "dropped" && server.FlagPos(view.team).HasValue && Flat(server.FlagPos(view.team).Value - pos).magnitude < 1.6f) server.FlagAction(view.id, "touch", view.team);
            if (server.FlagCarrier(enemy) == view.id && Flat(w.flagBase[view.team] - pos).magnitude < 2.5f) { server.FlagAction(view.id, "cap", 0); PickGoal(); }
            if (server.FlagCarrier(enemy) == view.id && Flat(goal - w.flagBase[view.team]).magnitude > 1f && !targetVisible) goal = w.flagBase[view.team];
        }

        readonly float[] p = new float[PoseData.Length];

        void PushPose(bool snap)
        {
            PoseData.Put(p, PoseData.FeetX, pos);
            p[PoseData.Yaw] = yaw;
            var aim = Quaternion.Euler(aimPitch, aimYaw, 0);
            var head = pos + Vector3.up * 1.6f;
            PoseData.Put(p, PoseData.Head, head);
            PoseData.Put(p, PoseData.HeadRot, aim);
            var rHand = head + aim * new Vector3(0.12f, -0.3f, 0.42f);
            var lHand = head + aim * new Vector3(-0.08f, -0.34f, 0.62f);
            PoseData.Put(p, PoseData.RHand, rHand);
            PoseData.Put(p, PoseData.RRot, aim);
            PoseData.Put(p, PoseData.LHand, lHand);
            PoseData.Put(p, PoseData.LRot, aim * Quaternion.Euler(0, 0, 90));
            p[PoseData.Flags] = PoseData.FlagGrounded;
            p[PoseData.LGun] = -1;
            p[PoseData.RGun] = gun.index;
            p[PoseData.Speed] = new Vector3(vel.x, 0, vel.z).magnitude;
            view.ApplyPose(p, true);
            if (Time.time >= nextShare) { nextShare = Time.time + 1f / 12f; server.BotPose(view.id, (float[])p.Clone()); }
        }
    }
}
