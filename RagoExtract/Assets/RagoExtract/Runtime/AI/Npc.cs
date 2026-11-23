using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Rago
{
    public enum NpcState : byte { Idle, Patrol, Investigate, Combat, Cover, Search, Dead }

    /// <summary>
    /// Raid hostile, modelled on Ghost of Tabor's militia: patrols in squads, notices you gradually
    /// (sight cone + hearing), takes cover, peeks and fires short bursts with human-like aim error,
    /// flinches and staggers when shot, ragdolls on death and leaves a lootable body.
    /// Host-authoritative in multiplayer; clients run "puppet" copies.
    /// </summary>
    public class Npc : MonoBehaviour, IDamageable
    {
        public int Id;
        public HumanRig Rig;
        public NavMeshAgent Agent;
        public GunDef Weapon;
        public GunRig GunModel;
        public float Health = 100, MaxHealth = 100;
        public bool Heavy;
        public bool Puppet;
        public NpcState State = NpcState.Patrol;
        public Vector3 AimPoint;
        public float Yaw;
        public bool Alive => Health > 0;
        public int ActorId => 1000 + Id;
        public System.Action<Npc> OnDied;
        const float HearClose = 28f;   // gunshots closer than this make raiders come and check; farther ones only make them look
        public ArmorDef Helmet, Vest;

        Vector3 _home;
        readonly List<Vector3> _patrol = new List<Vector3>();
        int _patrolIdx;
        Vector3 _stepLast; float _stepDist; bool _stepLeft;
        float _think, _awareness, _waitT, _lastSeenT, _burstT, _reloadT, _coverT, _searchT, _flinch, _stagger, _blind;
        /// <summary>Flashbanged: can't see or shoot, and staggers about for a bit.</summary>
        public void Flash(float seconds)
        {
            bool was = _blind > 0;
            _blind = Mathf.Max(_blind, seconds); _stagger = Mathf.Max(_stagger, seconds * 0.6f);
            _awareness = Mathf.Min(_awareness, 0.4f); _aimErr = 1.5f; _burst = 0;   // they lose you and have to find you again
            if (!was) { Stop(); Say(ScavVoice.Line.Flash, 1f, true); }
        }
        int _burst, _rounds;
        Vector3 _lastKnown, _flinchDir;
        Vector3 _netPos, _netAim, _netVel; float _netYaw, _netT; bool _netInit;
        Target _target;
        bool _crouch;
        // ---- wounds: where they're hit changes how they move and shoot
        float _armHurt, _legHurt, _down, _hunch, _armJerk, _limpPhase; bool _oneArm, _armLeft;
        Vector3 _hitKick;   // which way the hit part was knocked (head / chest / hips)
        float _aimErr = 1f;
        Rng _rng;
        float _barkT, _talkT, _idleTalkT = 20f;
        int Voice => Heavy ? 2 : Id % 2;

        /// <summary>Shout a line (authority only - it's sent to everyone else). chance &lt; 1: only sometimes.</summary>
        void Say(ScavVoice.Line l, float chance = 1f, bool urgent = false)
        {
            if (Puppet || !Alive && l != ScavVoice.Line.Hit) return;
            if (!urgent && Time.time < _talkT) return;
            if (chance < 1f && Random.value > chance) return;
            int idx = Random.Range(0, ScavVoice.Count(l));
            float len = ScavVoice.Play(l, idx, Voice, Rig.head.position, urgent);
            if (len <= 0f) return;
            _talkT = Time.time + len + Random.Range(2f, 4f);
            if (NetGame.Live && !Puppet) NetGame.I.NpcVoice(Id, (byte)l, (byte)idx);
        }

        /// <summary>The host's raider said something: play it here too.</summary>
        public void RemoteVoice(byte line, byte idx) { if (Rig != null) ScavVoice.Play((ScavVoice.Line)line, idx, Voice, Rig.head.position, true); }

        struct Target { public IDamageable d; public Transform aim; public Vector3 vel; public bool valid; }

        /// <summary>A map boss (null for an ordinary raider): tough, well armed, better shot, stays near their lair.</summary>
        public string BossName;
        public bool IsBoss => BossName != null;

        public static Npc Spawn(int id, Vector3 pos, float yaw, int seed, bool heavy, Transform parent, string boss = null)
        {
            var go = new GameObject("Npc_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var n = go.AddComponent<Npc>();
            n.Id = id;
            n._rng = new Rng(seed);
            n.Heavy = heavy;
            n.BossName = boss;
            n.MaxHealth = n.Health = boss != null ? 1400 : heavy ? 150 : 100;   // a boss soaks up a magazine or two
            n.Rig = HumanRig.Build(boss != null ? BossOutfit(boss) : heavy ? Outfit.RaiderHeavy : Outfit.Raider, seed, go.transform);
            HumanRig.NameTape(n.Rig, boss != null ? boss.Replace("The ", "") : HumanRig.ScavName(seed));   // name on the chest
            foreach (var c in n.Rig.colliders)
            {
                var z = c.gameObject.AddComponent<HitZone>();
                z.owner = n;
                z.part = PlayerBody.PartFor(c.transform, n.Rig);
                z.mult = HitZone.MultFor(z.part);
            }
            // real, droppable armour: heavies always wear a helmet + vest, regular raiders sometimes
            var ar = new Rng(seed * 31 + 5);
            if (boss != null) { BossKit(boss, out var bg, out var bh, out var bv); n.Helmet = Db.Get<ArmorDef>(bh); n.Vest = Db.Get<ArmorDef>(bv); }
            else if (heavy) { n.Helmet = Db.Get<ArmorDef>(ar.Chance(0.3f) ? "helm_fast" : "helm_ballistic"); n.Vest = Db.Get<ArmorDef>(ar.Chance(0.3f) ? "vest_heavy" : "vest_carrier"); }
            else { if (ar.Chance(0.2f)) n.Helmet = Db.Get<ArmorDef>("helm_bump"); if (ar.Chance(0.3f)) n.Vest = Db.Get<ArmorDef>(ar.Chance(0.3f) ? "vest_carrier" : "vest_soft"); }
            ArmorBuilder.Dress(n.Rig, n.Helmet, n.Vest, null, false);
            n.Rig.root.SetLayerRecursive(Layers.Npc);
            string[] common = { "vostok74", "vostokm", "vanguard", "kestrel", "breacher", "wasp", "comrade", "bandit", "p9", "nomad", "falcon10", "harvester", "vostoksu" };
            string[] heavyGuns = { "sentinel", "grizzly", "lancer", "fennec", "vostokm", "riot", "hailstorm", "corsair", "tundra", "reaper" };
            string bossGun = "grizzly";
            if (boss != null) BossKit(boss, out bossGun, out _, out _);
            n.Weapon = Db.Get<GunDef>(boss != null ? bossGun : heavy ? n._rng.Pick(heavyGuns) : n._rng.Pick(common)) ?? Db.Get<GunDef>("grizzly");
            n._rounds = n.Weapon.magSize;
            // bosses carry their gun kitted out (sight, muzzle device, light / laser, grip) - shown on them, working
            // (their light and laser are on), and dropped with it all still fitted
            if (boss != null)
            {
                n._gunData = BossAttachments(boss, n.Weapon);
                var disp = ItemFactory.Spawn(new ItemData(n.Weapon.id) { sight = n._gunData.sight, muzzle = n._gunData.muzzle, side = n._gunData.side, gripAtt = n._gunData.gripAtt, rounds = n.Weapon.magSize }, pos, Quaternion.identity) as Gun;
                if (disp != null)
                {
                    foreach (var it in disp.GetComponentsInChildren<Item>(true)) { Item.All.Remove(it); if (it.rb != null) { it.rb.isKinematic = true; it.rb.detectCollisions = false; } }
                    foreach (var c in disp.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    disp.transform.SetParent(go.transform, false);
                    disp.transform.localPosition = Vector3.zero; disp.transform.localRotation = Quaternion.identity;
                    disp.RemoteOn = true; disp.SideOn = true;
                    n._dispGun = disp;
                    n.GunModel = disp.rig;
                }
            }
            if (n.GunModel == null)
            {
                n.GunModel = GunBuilder.Build(n.Weapon, go.transform);
                GunBuilder.BuildMag(n.GunModel.magStyle, n.GunModel.magwell, n.Weapon.furniture, out _);
            }
            n.Yaw = yaw;
            n._home = pos;
            n.AimPoint = pos + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 10f + Vector3.up * 1.4f;
            n.Agent = go.AddComponent<NavMeshAgent>();
            n.Agent.radius = 0.35f; n.Agent.height = 1.8f; n.Agent.speed = 1.6f; n.Agent.acceleration = 10f; n.Agent.angularSpeed = 0f;
            n.Agent.updateRotation = false; n.Agent.stoppingDistance = 0.4f; n.Agent.autoBraking = true;
            n.Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) n.Agent.Warp(hit.position);
            for (int i = 0; i < 6; i++)
            {
                Vector2 c = n._rng.InCircle() * (boss != null ? 7f : 35f);   // bosses keep to their lair
                Vector3 p = pos + new Vector3(c.x, 0, c.y);
                if (NavMesh.SamplePosition(p, out var ph, 4f, NavMesh.AllAreas)) n._patrol.Add(ph.position);
            }
            n._think = n._rng.Range(0f, 0.3f);
            n._waitT = n._rng.Range(3f, 30f);   // staggered, so the whole map doesn't start walking at once
            return n;
        }

        /// <summary>Each map boss's own gun and armour (boss-only items: the only way to get them is to kill the boss).</summary>
        public static Outfit BossOutfit(string boss)
        {
            string b = boss.ToLowerInvariant();
            return b.Contains("kessler") ? Outfit.BossKessler : b.Contains("harbor") ? Outfit.BossHarbor : b.Contains("warden") ? Outfit.BossWarden : b.Contains("banker") ? Outfit.BossBanker : b.Contains("courier") ? Outfit.BossCourier : b.Contains("santa") ? Outfit.BossSanta : Outfit.RaiderHeavy;
        }

        ItemData _gunData;
        Gun _dispGun;

        /// <summary>The attachments each boss runs on their gun.</summary>
        static ItemData BossAttachments(string boss, GunDef g)
        {
            string b = boss.ToLowerInvariant();
            var d = new ItemData(g.id);
            if (b.Contains("kessler")) { d.sight = "hex_holo"; d.side = "laser_green"; d.gripAtt = "grip_angled"; }
            else if (b.Contains("harbor")) { d.sight = "reflex_pan"; d.side = "light_wml"; d.gripAtt = "grip_vertical"; }
            else if (b.Contains("warden")) { d.sight = "scope_8x"; d.side = "laser_red"; }
            else if (b.Contains("banker")) { d.sight = "prism_3x"; d.muzzle = "comp_triport"; d.side = "combo_peq"; d.gripAtt = "grip_angled"; }
            else if (b.Contains("courier")) { d.sight = "holo_ex3"; d.muzzle = "sup_compact"; d.side = "combo_peq"; }
            else { d.sight = "holo_ex3"; d.side = "light_wml"; }
            return d;
        }

        public static void BossKit(string boss, out string gun, out string helmet, out string vest)
        {
            string b = boss.ToLowerInvariant();
            if (b.Contains("kessler")) { gun = "boss_lancet"; helmet = "boss_helm_kessler"; vest = "boss_vest_kessler"; }
            else if (b.Contains("harbor")) { gun = "boss_anchor"; helmet = "boss_helm_harbor"; vest = "boss_vest_harbor"; }
            else if (b.Contains("warden")) { gun = "boss_longbow"; helmet = "boss_helm_warden"; vest = "boss_vest_warden"; }
            else if (b.Contains("banker")) { gun = "boss_parachute"; helmet = "boss_helm_banker"; vest = "boss_vest_banker"; }
            else if (b.Contains("courier")) { gun = "boss_tommy"; helmet = "boss_helm_courier"; vest = "boss_vest_courier"; }
            else if (b.Contains("santa")) { gun = "hailstorm"; helmet = null; vest = "vest_heavy"; }   // his hat stays on
            else { gun = "grizzly"; helmet = "helm_fast"; vest = "vest_heavy"; }
        }

        bool AgentOk => Agent != null && Agent.enabled && Agent.isOnNavMesh;

        void Go(Vector3 p, float speed)
        {
            if (!AgentOk) return;
            if (_down > 0) { Stop(); return; }
            Agent.speed = speed * (_stagger > 0 ? 0.3f : 1f) * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(_legHurt));   // leg wounds: they limp along
            Agent.isStopped = false;
            Agent.SetDestination(p);
        }
        void Stop() { if (AgentOk) Agent.isStopped = true; }

        /// <summary>A spot somewhere in the general area of a player (not on top of them).</summary>
        bool RoamTarget(out Vector3 p)
        {
            p = Vector3.zero;
            var targets = new List<Vector3>();
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && pl.Vitals.Alive && !RagoGame.I.SafeZone && !Tester.Invisible) targets.Add(pl.transform.position);
            if (NetGame.Live && !Puppet) foreach (var r in NetGame.I.Remotes.Values) if (r != null && r.Alive && !r.HiddenFromAi) targets.Add(r.transform.position);
            if (targets.Count == 0) return false;
            var t = targets[_rng.Range(0, targets.Count)];
            Vector2 o = _rng.InCircle() * 30f;
            if (!NavMesh.SamplePosition(t + new Vector3(o.x, 0, o.y), out var h, 6f, NavMesh.AllAreas)) return false;
            p = h.position;
            return true;
        }

        // ------------------------------------------------------------------------------- perception
        void Perceive(float dt)
        {
            if (_blind > 0) return;   // flashbanged
            var best = new Target();
            float bestScore = 0;
            Vector3 eye = Rig.head.position + Vector3.up * 0.08f;
            Vector3 fwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            foreach (var cand in Candidates())
            {
                Vector3 to = cand.aim.position - eye;
                float dist = to.magnitude;
                if (dist > 70f * Weather.SightMul) continue;   // night and fog: they see much less far
                float ang = Vector3.Angle(fwd, to);
                bool inCone = ang < 65f || dist < 4f;
                if (!inCone) continue;
                if (!ClearLine(eye, cand.aim.position, cand.d)) continue; // blocked
                float score = (1f - dist / (70f * Weather.SightMul)) * (ang < 30f ? 1.4f : 0.8f);
                if (score > bestScore) { bestScore = score; best = cand; best.valid = true; }
            }
            if (best.valid)
            {
                float rate = Mathf.Lerp(0.6f, 6f, bestScore) * (State == NpcState.Combat ? 4f : 1f);
                _awareness = Mathf.Min(1.5f, _awareness + rate * dt);
                if (_awareness >= 1f)
                {
                    if (State != NpcState.Combat && State != NpcState.Cover) { Bark(); _aimErr = 1f; }
                    _target = best;
                    _lastKnown = best.aim.position;
                    _lastSeenT = Time.time;
                    if (State != NpcState.Cover) State = NpcState.Combat;
                    NpcDirector.I?.Alert(transform.position, _lastKnown, 12f, this);   // only their close buddies join in
                }
            }
            else _awareness = Mathf.Max(0, _awareness - dt * 0.15f);
            // hearing
            for (int i = Noise.Recent.Count - 1; i >= 0; i--)
            {
                var e = Noise.Recent[i];
                if (Time.time - e.time > 0.6f) break;
                if (e.source >= 1000) continue;
                float d = Vector3.Distance(e.pos, transform.position);
                if (d >= e.radius * Noise.Through(e.pos, transform.position + Vector3.up * 0.5f)) continue;   // Underground: floors block most of it
                // only a close noise (or an alarm) brings them over; a distant gunshot just makes them stop and look that way.
                // They only come for you if they're really close or they actually see you (vision above).
                bool close = e.alarm || d < Mathf.Min(e.radius, HearClose);
                bool calm = State == NpcState.Patrol || State == NpcState.Idle || State == NpcState.Search;
                if (close)
                {
                    _awareness = Mathf.Max(_awareness, d < e.radius * 0.4f ? 0.9f : 0.6f);
                    if (calm) { State = NpcState.Investigate; _lastKnown = e.pos; _searchT = 12f; Say(ScavVoice.Line.Hear, 0.8f); }
                }
                else if (calm)
                {
                    _awareness = Mathf.Max(_awareness, 0.3f);
                    Vector3 to = e.pos - transform.position; to.y = 0;
                    if (to.sqrMagnitude > 1f) Yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;   // turn to look; they still have to spot you
                }
            }
        }

        IEnumerable<Target> Candidates()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p != null && p.Vitals.Alive && !RagoGame.I.SafeZone && !Tester.Invisible)
                yield return new Target { d = p.Vitals, aim = p.Body.Rig.chest, vel = p.MoveVelocity };
            if (NetGame.Live && !Puppet)
                foreach (var r in NetGame.I.Remotes.Values)
                    if (r != null && r.Alive && !r.HiddenFromAi) yield return new Target { d = r, aim = r.Chest, vel = Vector3.zero };
        }

        public void HearAlert(Vector3 pos)
        {
            if (!Alive || State == NpcState.Combat || State == NpcState.Cover) return;
            State = NpcState.Investigate; _lastKnown = pos; _searchT = 15f; _awareness = Mathf.Max(_awareness, 0.7f);
        }

        void Bark()
        {
            if (Time.time < _barkT) return;
            _barkT = Time.time + 6f;
            Say(ScavVoice.Line.Spot, 1f, true);
        }

        // ------------------------------------------------------------------------------- brain
        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            _flinch = Mathf.MoveTowards(_flinch, 0, dt * 3f);
            _stagger -= dt;
            _hunch = Mathf.MoveTowards(_hunch, 0, dt * 0.5f);
            _armJerk = Mathf.MoveTowards(_armJerk, 0, dt * 4f);
            _hitKick = Vector3.MoveTowards(_hitKick, Vector3.zero, dt * 0.8f);
            if (_down > 0) { _down -= dt; if (!Puppet) Stop(); }   // knocked off their feet: down for a moment
            if (Alive) Footsteps.Track(ref _stepLast, ref _stepDist, ref _stepLeft, transform.position, transform.forward, dt, 0.45f);
            _blind -= dt;
            if (Puppet) { PuppetUpdate(dt); return; }
            _think -= dt;
            if (_think <= 0) { _think = 0.2f; Perceive(0.2f); Decide(); }
            Act(dt);
        }

        void Decide()
        {
            if (_blind > 0) { Stop(); return; }   // flashed: standing there, blinking
            bool hasLos = _target.valid && Time.time - _lastSeenT < 0.35f;
            // a leash: once they've lost you and wandered far from their post, they head back instead of hunting on
            if ((State == NpcState.Search || State == NpcState.Investigate) && !IsBoss && (transform.position - _home).sqrMagnitude > 55f * 55f)
            { State = NpcState.Patrol; _awareness = 0.1f; Go(_home, 1.6f); }
            switch (State)
            {
                case NpcState.Patrol:
                case NpcState.Idle:
                    _idleTalkT -= 0.2f;
                    if (_idleTalkT <= 0) { _idleTalkT = _rng.Range(25f, 60f); Say(ScavVoice.Line.Idle, 0.6f); }
                    if (_patrol.Count == 0) { State = NpcState.Idle; break; }
                    if (!AgentOk || Agent.remainingDistance < 0.8f)
                    {
                        _waitT -= 0.2f;
                        if (_waitT <= 0)
                        {
                            _waitT = _rng.Range(12f, 25f);
                            // now and then they roam towards where the players are, so fights actually happen
                            if (!IsBoss && _rng.Chance(0.07f) && RoamTarget(out var roam)) Go(roam, 1.6f);
                            else { _patrolIdx = (_patrolIdx + 1) % _patrol.Count; Go(_patrol[_patrolIdx], 1.4f); }
                        }
                    }
                    break;
                case NpcState.Investigate:
                    Go(_lastKnown, 2.6f);
                    _searchT -= 0.2f;
                    if (_searchT <= 0 || (AgentOk && Agent.remainingDistance < 1.5f && !Agent.pathPending)) { State = NpcState.Search; _searchT = 6f; }
                    break;
                case NpcState.Search:
                    _searchT -= 0.2f;
                    if (AgentOk && Agent.remainingDistance < 1f) { var c = _rng.InCircle() * 8f; Go(_lastKnown + new Vector3(c.x, 0, c.y), 1.8f); }
                    // gave up: back to where they were guarding (they don't hunt you across the map)
                    if (_searchT <= 0) { State = NpcState.Patrol; _awareness = 0.1f; Go(_home, 1.4f); }
                    break;
                case NpcState.Combat:
                    if (!_target.valid || !_target.d.Alive) { if (_target.valid && !_target.d.Alive) Say(ScavVoice.Line.Kill, 0.9f, true); State = NpcState.Search; _searchT = 10f; break; }
                    if (!hasLos)
                    {
                        if (Time.time - _lastSeenT > 4f) { State = NpcState.Search; _searchT = 7f; Go(_lastKnown, 2.6f); Say(ScavVoice.Line.Lost, 0.8f); }
                        else Go(_lastKnown, 2.8f);
                    }
                    else
                    {
                        float dist = Vector3.Distance(transform.position, _target.aim.position);
                        if (_rng.Chance(0.03f)) Say(ScavVoice.Line.Attack);
                        _coverT -= 0.2f;
                        if (_coverT <= 0)
                        {
                            _coverT = _rng.Range(3f, 7f);
                            if (FindCover(_target.aim.position, out var cover)) { Go(cover, 3.8f); State = NpcState.Cover; }
                            else if (dist > 28f) Go(Vector3.MoveTowards(transform.position, _target.aim.position, 8f), 2.4f);
                            else if (_rng.Chance(0.5f)) { var side = Quaternion.Euler(0, Yaw + 90f * (_rng.Chance(0.5f) ? 1 : -1), 0) * Vector3.forward; Go(transform.position + side * 3f, 2f); }
                            else Stop();
                        }
                    }
                    break;
                case NpcState.Cover:
                    if (!_target.valid || !_target.d.Alive) { State = NpcState.Search; break; }
                    if (AgentOk && !Agent.pathPending && Agent.remainingDistance < 0.6f)
                    {
                        Stop();
                        // peek: crouch behind cover, stand to shoot
                        _crouch = _reloadT > 0 || (Time.time % 4f) < 1.4f;
                    }
                    _coverT -= 0.2f;
                    if (_coverT <= 0) { State = NpcState.Combat; _crouch = false; _coverT = _rng.Range(2f, 5f); }
                    break;
            }
        }

        bool FindCover(Vector3 threat, out Vector3 cover)
        {
            cover = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float a = _rng.Range(0f, 360f);
                float r = _rng.Range(3f, 11f);
                Vector3 p = transform.position + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                if (!NavMesh.SamplePosition(p, out var h, 2f, NavMesh.AllAreas)) continue;
                Vector3 low = h.position + Vector3.up * 0.9f, high = h.position + Vector3.up * 1.55f;
                bool lowBlocked = !HitsTarget(low, threat);
                bool highClear = HitsTarget(high, threat);
                if (lowBlocked && highClear) { cover = h.position; return true; }
            }
            return false;
        }

        bool HitsTarget(Vector3 from, Vector3 to) => _target.valid && ClearLine(from, to, _target.d);

        static readonly RaycastHit[] _los = new RaycastHit[12];
        /// <summary>Line of sight that ignores our own body parts. True if nothing but the target is in the way.</summary>
        bool ClearLine(Vector3 from, Vector3 to, IDamageable target)
        {
            Vector3 d = to - from; float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, _los, len, ~0, QueryTriggerInteraction.Ignore);
            float best = 1e9f; Collider first = null;
            for (int i = 0; i < n; i++)
            {
                var c = _los[i].collider;
                if (c.transform.IsChildOf(transform)) continue;
                if (c is CharacterController) continue;   // a player's movement capsule wraps their body: look past it to the body itself
                var z = c.GetComponent<HitZone>();
                if (z != null && z.owner == (IDamageable)this) continue;
                if (_los[i].distance < best) { best = _los[i].distance; first = c; }
            }
            if (first == null) return true;
            var hz = first.GetComponent<HitZone>();
            return hz != null && hz.owner == target;
        }

        void Act(float dt)
        {
            Vector3 vel = AgentOk ? Agent.velocity : Vector3.zero;
            bool engaging = _blind <= 0 && (State == NpcState.Combat || State == NpcState.Cover) && _target.valid && Time.time - _lastSeenT < 0.6f;
            Vector3 lookAt;
            if (engaging)
            {
                // human-like aim: error shrinks while tracking, grows when the target moves or we get hit
                _aimErr = Mathf.MoveTowards(_aimErr, IsBoss ? 0.07f : 0.12f, dt * (IsBoss ? 0.8f : 0.45f));   // bosses settle their aim faster
                float dist = Vector3.Distance(Rig.head.position, _target.aim.position);
                float err = _aimErr * Mathf.Lerp(0.25f, 1.3f, dist / 60f) + _target.vel.magnitude * 0.08f + _flinch * 0.6f;
                Vector3 jitter = new Vector3(Mathf.PerlinNoise(Time.time * 1.3f, Id) - 0.5f, Mathf.PerlinNoise(Id, Time.time * 1.1f) - 0.5f, Mathf.PerlinNoise(Time.time, Id * 3) - 0.5f) * 2f * err;
                Vector3 aimT = _target.aim.position + Vector3.up * (_rng.Chance(0.02f) ? 0.45f : 0.05f) + jitter;
                AimPoint = Vector3.Lerp(AimPoint, aimT, 1f - Mathf.Exp(-6f * dt));
                lookAt = AimPoint;
                TryShoot(dt, dist);
            }
            else
            {
                Vector3 dir = vel.sqrMagnitude > 0.2f ? vel.normalized : Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
                lookAt = Rig.head.position + dir * 10f + Vector3.down * 2f;
                AimPoint = Vector3.Lerp(AimPoint, lookAt, dt * 3f);
                if (_reloadT > 0) _reloadT -= dt;
            }
            Vector3 flat = (lookAt - transform.position); flat.y = 0;
            if (flat.sqrMagnitude > 0.01f) Yaw = Mathf.MoveTowardsAngle(Yaw, Quaternion.LookRotation(flat).eulerAngles.y, dt * (engaging ? 260f : 140f));
        }

        void TryShoot(float dt, float dist)
        {
            if (_reloadT > 0)
            {
                _reloadT -= dt;
                if (_reloadT <= 0) { _rounds = Weapon.magSize; Sfx.Play(Synth.MagIn(), GunModel.magwell.position, 0.6f, 1f, 1f, 15f); }
                return;
            }
            if (_crouch && State == NpcState.Cover) return;
            if (_awareness < 1f || _blind > 0) return;
            _burstT -= dt;
            if (_burstT > 0) return;
            if (_rounds <= 0) { _reloadT = Heavy ? 2.6f : 3.2f; Say(ScavVoice.Line.Reload, 0.75f, true); Sfx.Play(Synth.MagOut(), GunModel.magwell.position, 0.6f, 1f, 1f, 15f); if (FindCover(_target.aim.position, out var c)) { Go(c, 3.8f); State = NpcState.Cover; _coverT = 4f; } return; }
            // aligned enough?
            Vector3 toAim = (AimPoint - GunModel.muzzle.position).normalized;
            if (Vector3.Angle(GunModel.muzzle.forward, toAim) > 12f) return;
            if (_burst <= 0) _burst = Weapon.action == FireAction.Auto ? _rng.Range(2, dist < 12 ? 7 : 4) : 1;
            FireOnce(toAim);
            _burst--;
            _burstT = _burst > 0 ? 60f / Weapon.rpm * 1.15f : _rng.Range(0.45f, 1.3f) + (Weapon.action == FireAction.Pump || Weapon.action == FireAction.Bolt ? 0.8f : 0f);
        }

        void FireOnce(Vector3 dir)
        {
            _rounds--;
            Vector3 o = Ballistics.SafeOrigin(Rig.head.position, GunModel.muzzle.position);
            float spr = Weapon.spread * 0.6f;
            // arm wounds wreck their aim; one arm (or shooting from the ground) even more
            spr *= 1f + Mathf.Clamp01(_armHurt) * 1.5f + (_oneArm ? 2.5f : 0f) + (_down > 0 ? 1.2f : 0f) + (_armJerk > 0.3f ? 1f : 0f);
            dir = Quaternion.Euler(Random.Range(-spr, spr), Random.Range(-spr, spr), 0) * dir;
            Ballistics.Fire(o, dir, Weapon, ActorId, transform, this, false, Random.value < 0.3f);
            Fx.MuzzleFlash(GunModel.muzzle, Weapon.TwoHanded ? 1.1f : 0.7f, Weapon.suppressed);
            Fx.EjectCasing(GunModel.ejectPort, Vector3.zero, Weapon.cal == Caliber.C12g, false);
            Sfx.Gunshot(Weapon, o, false);
            _recoil = 1f;
            if (!Puppet) NetGame.I?.NpcFire(Id, o, dir);
        }

        float _recoil;

        public void RemoteFire(Vector3 o, Vector3 dir)
        {
            // the bullet can hurt you here: the host's copy of you is a moment behind, so its hits used to miss - you
            // couldn't be hurt by raiders at all unless you were the host
            Ballistics.Fire(o, dir, Weapon, ActorId, transform, this, true, Random.value < 0.3f, true);
            Fx.MuzzleFlash(GunModel.muzzle, Weapon.TwoHanded ? 1.1f : 0.7f, Weapon.suppressed);
            Sfx.Gunshot(Weapon, o, false);
            _recoil = 1f;
        }

        // ------------------------------------------------------------------------------- puppet (MP client)
        public void ApplyNetState(Vector3 pos, float yaw, NpcState st, Vector3 aim, bool alive)
        {
            if (!Puppet) return;
            if (!alive && Alive) { Die(pos + Vector3.up, Vector3.zero, true); return; }   // missed its death message (joined late / reconnected)
            // how fast it's going between updates, so in between it keeps moving instead of stopping and jumping
            float now = Time.time;
            if (_netInit && now - _netT > 0.02f) _netVel = Vector3.ClampMagnitude((pos - _netPos) / (now - _netT), 7f);
            if ((pos - _netPos).sqrMagnitude > 25f) _netVel = Vector3.zero;   // a big jump: no extrapolating from it
            _netT = now;
            _netPos = pos; _netYaw = yaw; _netAim = aim; State = st;
            if (!_netInit) { _netInit = true; transform.position = pos; Yaw = yaw; AimPoint = aim; }
        }

        void PuppetUpdate(float dt)
        {
            if (!_netInit) return;
            Vector3 before = transform.position;
            // dead reckoning: where it will be by now if it kept going (up to 0.3 s), smoothed - no stop-start rubber-banding
            Vector3 aimPos = _netPos + _netVel * Mathf.Min(Time.time - _netT, 0.3f);
            if (State == NpcState.Dead || _netVel.sqrMagnitude < 0.01f) aimPos = _netPos;
            transform.position = Vector3.Lerp(transform.position, aimPos, 1f - Mathf.Exp(-12f * dt));
            _puppetVel = (transform.position - before) / Mathf.Max(dt, 0.001f);
            Yaw = Mathf.LerpAngle(Yaw, _netYaw, 1f - Mathf.Exp(-10f * dt));
            AimPoint = Vector3.Lerp(AimPoint, _netAim, 1f - Mathf.Exp(-10f * dt));
            _crouch = State == NpcState.Cover && false;
        }
        Vector3 _puppetVel;

        public void BecomeAuthority()
        {
            if (!Puppet) return;
            Puppet = false;
            if (Agent != null) { Agent.enabled = true; if (NavMesh.SamplePosition(transform.position, out var h, 3f, NavMesh.AllAreas)) Agent.Warp(h.position); }
        }

        public void MakePuppet()
        {
            Puppet = true;
            // handed to another player: carry on from right here until their first update arrives
            _netPos = transform.position; _netYaw = Yaw; _netAim = AimPoint; _netVel = Vector3.zero; _netT = Time.time; _netInit = true;
            if (Agent != null) Agent.enabled = false;
        }

        // ------------------------------------------------------------------------------- pose
        void LateUpdate()
        {
            if (!Alive || Rig.ragdoll) return;
            var cam = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head.position : transform.position;
            float camDist = Vector3.Distance(cam, transform.position);
            if (camDist > 80f && (Time.frameCount + Id) % 3 != 0) return;
            float dt = Time.deltaTime;
            _recoil = Mathf.MoveTowards(_recoil, 0, dt * 8f);
            Vector3 root = transform.position;
            float h = _crouch ? 1.15f : 1.62f;
            if (_down > 0) h = 0.75f;                                // knocked down: on their knees / backside
            h -= _hunch * 0.28f;                                     // gut / chest hit: doubled over
            Vector3 vel0 = Puppet ? _puppetVel : AgentOk ? Agent.velocity : Vector3.zero;
            if (_legHurt > 0.15f && vel0.sqrMagnitude > 0.05f)       // limp: a dip on the bad leg every other step
            {
                _limpPhase += dt * vel0.magnitude * 2.2f;
                h -= Mathf.Max(0f, Mathf.Sin(_limpPhase)) * 0.09f * Mathf.Clamp01(_legHurt);
            }
            Quaternion yawQ = Quaternion.Euler(0, Yaw, 0);
            Vector3 head = root + Vector3.up * h + yawQ * new Vector3(0, 0, 0.05f + _hunch * 0.18f);
            // flinch: knock the head/upper body away from the hit, the way the bullet was going
            head += _flinchDir * _flinch * 0.12f + _hitKick;
            Vector3 toAim = AimPoint - head;
            if (toAim.sqrMagnitude < 0.01f) toAim = yawQ * Vector3.forward;
            Quaternion headRot = Quaternion.LookRotation(toAim.normalized, Vector3.up);
            // gun pose: shouldered and pointing at the aim point when engaging, low ready otherwise
            bool engaging = State == NpcState.Combat || State == NpcState.Cover;
            Vector3 shoulder = head + yawQ * new Vector3(0.16f, -0.24f, -0.02f);
            Quaternion gunRot = engaging ? Quaternion.LookRotation((AimPoint - shoulder).normalized, Vector3.up)
                                         : yawQ * Quaternion.Euler(15, -45, 0);
            gunRot *= Quaternion.Euler(-_recoil * 6f, 0, 0);
            Vector3 buttLocal = GunModel.butt != null ? GunModel.butt.localPosition : new Vector3(0, 0.05f, -0.3f);
            Vector3 gunPos = engaging ? shoulder - gunRot * buttLocal - gunRot * Vector3.forward * _recoil * 0.03f
                                      : head + yawQ * new Vector3(0.15f, -0.4f, 0.16f);
            if (!Weapon.TwoHanded) gunPos = head + gunRot * new Vector3(0.0f, -0.12f, 0.45f);
            GunModel.root.transform.SetPositionAndRotation(gunPos, gunRot);
            Vector3 gripR = GunModel.root.transform.position;
            Vector3 gripL = GunModel.foregrip != null ? GunModel.foregrip.position : gripR + gunRot * new Vector3(-0.02f, -0.03f, 0.08f);
            if (_armJerk > 0) { Vector3 j = new Vector3(Mathf.Sin(Time.time * 47f), Mathf.Cos(Time.time * 39f), 0) * 0.04f * _armJerk; GunModel.root.transform.position += gunRot * j; gripR = GunModel.root.transform.position; gripL = GunModel.foregrip != null ? GunModel.foregrip.position : gripL + gunRot * j; }
            Quaternion hr = gunRot;
            Quaternion hl = gunRot * Quaternion.Euler(0, 0, 70);
            if (_oneArm)
            {
                // the hurt arm hangs by their side; the gun is held one-handed and droops
                Vector3 side = yawQ * new Vector3(_armLeft ? -0.22f : 0.22f, 0, 0.05f);
                Vector3 hang = root + Vector3.up * (h - 0.85f) + side;
                if (_armLeft) { gripL = hang; hl = yawQ * Quaternion.Euler(90, 0, 0); }
                else { gripL = gripR; hl = hr; gripR = hang; hr = yawQ * Quaternion.Euler(90, 0, 0); }   // right arm out: the left hand takes the grip
                GunModel.root.transform.rotation *= Quaternion.Euler(8f + Mathf.Sin(Time.time * 3f) * 3f, 0, 0);
            }
            Vector3 vel = Puppet ? _puppetVel : AgentOk ? Agent.velocity : Vector3.zero;
            Rig.Pose(root, Yaw, head, headRot, gripL, hl, gripR, hr, vel, dt);
        }

        // ------------------------------------------------------------------------------- damage
        /// <summary>Who landed the killing hit (Levels.MyKillerId of that player; 0 = a raider / nobody) and was it the head.</summary>
        public int KillerId; public bool KillerHeadshot; public string KillerGun;

        public void TakeHit(HitInfo h, HitZone zone)
        {
            if (!Alive) return;
            // credit goes to whoever's hit this is: our own (attacker 0) or another player's (their network id + 1)
            KillerId = h.attacker == 0 ? Levels.MyKillerId : h.attacker < 1000 ? h.attacker : 0;
            KillerHeadshot = h.part == BodyPart.Head; KillerGun = h.gunId;
            float dmg = h.damage;
            dmg *= ArmorDef.Reduction(h.part, Helmet, Vest, h.gunId, h.ammo);
            Health -= dmg;
            if (Health > 0) Wound(h, dmg);
            if (h.ammo == AmmoType.Incendiary && Health > 0f) Ignite(h);
            if (Puppet)
            {
                // every player's copy of this raider counts every hit (everyone's shots are sent to everyone), so whoever
                // lands the killing shot drops them straight away and tells the others - not only the host
                _flinch = Mathf.Clamp01(_flinch + 0.6f); _flinchDir = h.dir.normalized;
                if (Health <= 0) Die(h.point, h.dir * Mathf.Max(20f, h.force * 20f), h.fromNet);
                return;
            }
            _flinch = Mathf.Clamp01(_flinch + 0.6f);
            _flinchDir = h.dir.normalized;
            _aimErr = Mathf.Min(1.5f, _aimErr + 0.5f);
            if (dmg > 25f) _stagger = 0.6f;
            _awareness = 1.2f;
            if (State != NpcState.Combat && State != NpcState.Cover) { State = NpcState.Combat; _lastKnown = h.point - h.dir * 15f; _lastSeenT = Time.time - 0.5f; _coverT = 0; }
            if (Health <= 0) Die(h.point, h.dir * Mathf.Max(20f, h.force * 20f), false);
            else Say(ScavVoice.Line.Hit, 0.55f, true);
        }

        /// <summary>React to where the hit landed: head snaps back, chest / gut doubles them over, arm hits ruin their aim
        /// (enough and they drop the support hand and fight one-handed), leg hits make them limp or knock them down.</summary>
        void Wound(HitInfo h, float dmg)
        {
            if (dmg < 2f || Rig == null) return;
            Vector3 d = h.dir.sqrMagnitude > 0.001f ? h.dir.normalized : -transform.forward;
            float k = Mathf.Clamp01(dmg / 40f);
            switch (h.part)
            {
                case BodyPart.Head:
                    _hitKick = d * (0.12f + 0.12f * k) + Vector3.up * 0.03f; _flinch = 1f; _stagger = Mathf.Max(_stagger, 0.8f);
                    break;
                case BodyPart.Thorax:
                    _hitKick = d * (0.08f + 0.1f * k); _hunch = Mathf.Max(_hunch, 0.4f * k + 0.15f);
                    break;
                case BodyPart.Stomach:
                    _hitKick = d * 0.06f; _hunch = Mathf.Max(_hunch, 0.6f * k + 0.3f); _stagger = Mathf.Max(_stagger, 0.5f);
                    break;
                case BodyPart.Arm:
                    _armLeft = Rig.faL != null && Rig.faR != null && (h.point - Rig.faL.position).sqrMagnitude < (h.point - Rig.faR.position).sqrMagnitude;
                    _armHurt += dmg / 35f; _armJerk = 1f;
                    if (_armHurt >= 1f && !_oneArm) { _oneArm = true; Say(ScavVoice.Line.Hit, 1f, true); }
                    break;
                case BodyPart.Leg:
                    _legHurt += dmg / 45f;
                    if ((dmg > 28f || _legHurt >= 1f) && _down <= 0) { _down = Random.Range(1.8f, 3.2f); _stagger = Mathf.Max(_stagger, _down); }   // leg taken out: they go down
                    break;
            }
        }

        /// <summary>Set alight by an incendiary round: burns for a few seconds; the damage counts as the shooter's.</summary>
        void Ignite(HitInfo h)
        {
            var bone = Rig != null ? (h.part == BodyPart.Leg ? Rig.thL : h.part == BodyPart.Arm ? Rig.uaR : Rig.chest) : transform;
            var burn = h; burn.ammo = AmmoType.Standard; burn.force = 0.5f; burn.point = bone.position; burn.dir = Vector3.down;
            Burning.Ignite(bone, 6f, dt =>
            {
                if (!Alive) return;
                burn.damage = 7f * dt * 2f;
                if (!Puppet) { _flinch = Mathf.Clamp01(_flinch + 0.3f); _aimErr = Mathf.Min(2f, _aimErr + 0.3f); }
                TakeHit(burn, null);
            });
            Say(ScavVoice.Line.Hit, 0.9f, true);
        }

        public void Die(Vector3 point, Vector3 impulse, bool fromNet)
        {
            if (Rig.ragdoll) return;
            Health = 0;
            State = NpcState.Dead;
            if (!Puppet && NpcDirector.I != null)
                foreach (var o in NpcDirector.I.Npcs)
                    if (o != null && o != this && o.Alive && !o.Puppet && (o.transform.position - transform.position).sqrMagnitude < 18f * 18f) { o.Say(ScavVoice.Line.ManDown, 0.85f, true); break; }
            if (Agent != null) Agent.enabled = false;
            Rig.SetRagdoll(true, impulse, point, Puppet ? _puppetVel : Vector3.zero);
            Rig.root.SetLayerRecursive(Layers.Ragdoll);
            Sfx.Play(Synth.BodyFall(), Rig.hips.position, 0.8f, 1f, 1f, 25f);
            Fx.BloodPool(Rig.chest.position);
            // drop the weapon as a real item + loot
            var gpos = GunModel.root.transform.position; var grot = GunModel.root.transform.rotation;
            Destroy(GunModel.root);
            if (_dispGun != null) Destroy(_dispGun.gameObject);
            var gd = new ItemData(Weapon.id) { rounds = Mathf.Max(0, _rounds), chambered = _rounds > 0 };
            if (_gunData != null) { gd.sight = _gunData.sight; gd.muzzle = _gunData.muzzle; gd.side = _gunData.side; gd.gripAtt = _gunData.gripAtt; }
            var gun = ItemFactory.Spawn(gd, gpos, grot);
            if (gun != null) { gun.netId = 60000 + Id * 8; gun.rb.SetVel(impulse * 0.02f); NetGame.I?.Register(gun); }
            var rng = new Rng(Id * 7919 + 17);
            int n = rng.Range(1, Heavy ? 4 : 3);
            for (int i = 0; i < n; i++)
            {
                var def = Db.Roll(Db.LootTable.NpcBody, rng);
                if (def == null) continue;
                var it = ItemFactory.Spawn(def.id, Rig.chest.position + Vector3.up * 0.25f + Random.insideUnitSphere * 0.2f, Random.rotation);
                if (it != null) { it.netId = 60000 + Id * 8 + 1 + i; NetGame.I?.Register(it); }
            }
            // a boss carries a vault keycard and a couple of valuables (their own gun and armour drop below - no pile of random guns)
            if (IsBoss)
            {
                var key = ItemFactory.Spawn(new ItemData("key_vault"), Rig.chest.position + Vector3.up * 0.3f, Random.rotation);
                if (key != null) { key.netId = 90000 + Id * 4; NetGame.I?.Register(key); }
                for (int i = 0; i < 2; i++)
                {
                    var def = Db.Roll(Db.LootTable.Safe, rng);
                    if (def != null && def.kind == ItemKind.Key) def = Db.Roll(Db.LootTable.Safe, rng);
                    if (def == null) continue;
                    var it = ItemFactory.Spawn(def.id, Rig.chest.position + Vector3.up * 0.35f + Random.insideUnitSphere * 0.25f, Random.rotation);
                    if (it != null) { it.netId = 90000 + Id * 4 + 1 + i; NetGame.I?.Register(it); }
                }
                // Evil Santa drops a present with a North Pole Keycard in it (made the same on every machine, so it opens for everyone)
                if (BossName.ToLowerInvariant().Contains("santa") && RagoGame.I != null && RagoGame.I.Map != null)
                {
                    var gp = Rig.hips.position; gp.y = transform.position.y;
                    var gift = LootCrate.Create(RagoGame.I.Map, 20000 + Id, gp + transform.forward * 0.8f, transform.eulerAngles.y + 180f, LootCrate.Kind.Present);
                    gift.santaGift = true;
                }
                var pl = RagoGame.I != null ? RagoGame.I.Player : null;
                if (pl != null && Vector3.Distance(pl.transform.position, transform.position) < 90f) pl.Toast("BOSS DOWN: " + BossName.ToUpper());
            }
            // worn armour comes off as real items
            if (Helmet != null) DropArmor(Helmet, Rig.head, 4, impulse);
            if (Vest != null) DropArmor(Vest, Rig.chest, 5, impulse);
            ArmorBuilder.Dress(Rig, null, null, null, false);
            // ammo for the dropped gun
            var ammo = ItemFactory.Spawn(Db.AmmoIdFor(Weapon.cal), Rig.hips.position + Vector3.up * 0.3f, Random.rotation);
            if (ammo != null) { ammo.netId = 60000 + Id * 8 + 7; NetGame.I?.Register(ammo); }
            if (!fromNet) NetGame.I?.NpcDied(Id, point, impulse, KillerId, KillerHeadshot);
            OnDied?.Invoke(this);
        }

        void DropArmor(ArmorDef d, Transform bone, int k, Vector3 impulse)
        {
            var it = ItemFactory.Spawn(d.id, bone.position + Vector3.up * 0.15f, bone.rotation);
            if (it == null) return;
            it.netId = 60000 + Id * 8 + k;
            it.rb.SetVel(impulse * 0.015f + Vector3.up);
            NetGame.I?.Register(it);
        }
    }
}
