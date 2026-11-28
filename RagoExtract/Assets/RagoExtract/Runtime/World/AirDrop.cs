using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Air drops. A four-engine cargo plane flies over the map (you hear it coming from a long way off) and pushes a
    /// supply crate out over the drop zone; it floats down under a parachute trailing red smoke and lands as a crate
    /// full of vault-grade loot. A random air drop comes in 15% of raids (not the Underground) and The Courier - the
    /// boss with the Thompson - lands with it, guarded by two heavies. A Military Radio calls one in where you stand.
    /// Every player sees the same drop: the random one comes from the raid seed, a radio call is sent to everyone.
    /// </summary>
    public class AirDrop : MonoBehaviour
    {
        public const float RandomChance = 0.15f;
        static int _count;

        Vector3 _target, _dir, _pos;
        float _speed = 62f, _travel, _length;
        bool _dropped, _boss;
        int _index;
        Transform _plane;
        readonly System.Collections.Generic.List<Transform> _props = new System.Collections.Generic.List<Transform>();
        AudioSource _snd;

        public static bool Allowed(out string why)
        {
            why = null;
            var g = RagoGame.I;
            if (g == null || g.Area != Area.Raid || g.Map == null) { why = "Air drops only come in raids"; return false; }
            if (g.Map.Id == "underground") { why = "No signal down here - air drops can't reach the Underground"; return false; }
            return true;
        }

        /// <summary>Somewhere open on this map (sky straight above it) for a random drop.</summary>
        public static bool PickSpot(MapBase map, Rng rng, out Vector3 spot)
        {
            float h = Mathf.Max(40f, map.Half * 0.7f);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(rng.Range(-h, h), 400f, rng.Range(-h, h));
                if (!Physics.Raycast(p, Vector3.down, out var hit, 800f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.85f || SurfaceTag.Of(hit.collider) == Synth.Surface.Water) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 1.6f, 1.2f, Ballistics.WallMask, QueryTriggerInteraction.Ignore)) continue;
                spot = hit.point; return true;
            }
            spot = Vector3.zero;
            return false;
        }

        /// <summary>Send a plane over target. tell = we started it (radio): let everyone else see it too.</summary>
        public static void Call(Vector3 target, float heading, bool boss, bool tell, int index = -1)
        {
            var g = RagoGame.I;
            if (g == null || g.Map == null) return;
            var go = new GameObject("AirDropPlane");
            go.transform.SetParent(g.Map.transform, false);
            var a = go.AddComponent<AirDrop>();
            a._target = target; a._boss = boss; a._index = index >= 0 ? index : 1000 + ((_count++ * 7 + Random.Range(0, 7)) % 400);   // crate net ids 26000-32400: clear of map crates and bodies
            a._dir = Quaternion.Euler(0, heading, 0) * Vector3.forward;
            a._length = 1900f;
            a._pos = new Vector3(target.x, Mathf.Max(target.y + 120f, 125f), target.z) - a._dir * 950f;
            a.BuildPlane();
            if (tell && NetGame.Live) NetGame.I.SendAirDrop(target, heading, boss, a._index);
            var pl = g.Player;
            if (pl != null)
            {
                var to = target - pl.transform.position; to.y = 0;
                string where = to.magnitude < 40f ? "right here" : Mathf.RoundToInt(to.magnitude) + " m " + Compass(to);
                pl.Toast("AIR DROP INBOUND - " + where + ". Listen for the plane." + (boss ? " Something nasty is riding with it." : ""));
            }
        }

        public static void ResetCount() { _count = 0; }

        static string Compass(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; if (a < 0) a += 360f;
            string[] n = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return n[Mathf.RoundToInt(a / 45f) % 8];
        }

        void BuildPlane()
        {
            _plane = new GameObject("Plane").transform;
            _plane.SetParent(transform, false);
            var body = Mats.Lit("plane_body", Color.white, 0.35f, 0.35f, Mats.Metal, null, 0.4f, 0.4f);
            var mb = new MB(2);
            Color grey = new Color(0.36f, 0.4f, 0.36f), dark = new Color(0.12f, 0.13f, 0.12f), glass = new Color(0.08f, 0.1f, 0.12f);
            // fuselage, nose and an upswept tail with the loading ramp
            mb.Sub(0).Cyl(new Vector3(0, 0, -9f), new Vector3(0, 0, 9f), 2.1f, 2.1f, 18, grey);
            mb.Sub(0).Cyl(new Vector3(0, 0, 9f), new Vector3(0, -0.3f, 12.5f), 2.1f, 0.6f, 18, grey);
            mb.Sub(0).Cyl(new Vector3(0, 0, -9f), new Vector3(0, 1.2f, -16f), 2.1f, 0.5f, 18, grey);
            mb.Sub(1).Box(new Vector3(0, 0.9f, 11.2f), new Vector3(2.2f, 0.7f, 1.2f), glass, 0.2f);   // cockpit windows
            // high wing with four engines and propellers
            mb.Sub(0).Box(new Vector3(0, 1.9f, 1.5f), new Vector3(40f, 0.45f, 4.2f), grey, 0.15f);
            foreach (var x in new[] { -13f, -6.5f, 6.5f, 13f })
            {
                mb.Sub(0).Cyl(new Vector3(x, 1.5f, 1f), new Vector3(x, 1.5f, 6.2f), 0.8f, 0.65f, 14, grey);
                mb.Sub(1).Cyl(new Vector3(x, 1.5f, 6.2f), new Vector3(x, 1.5f, 6.8f), 0.45f, 0.1f, 12, dark);
            }
            // tail: fin and stabilisers
            mb.Sub(0).Box(new Vector3(0, 4.3f, -14.5f), new Vector3(0.35f, 6f, 4.5f), grey, 0.1f);
            mb.Sub(0).Box(new Vector3(0, 1.6f, -15f), new Vector3(14f, 0.3f, 3.2f), grey, 0.1f);
            // landing gear pods
            mb.Sub(0).Box(new Vector3(-2.2f, -1.4f, 0), new Vector3(1.0f, 1.2f, 6f), grey * 0.9f, 0.3f);
            mb.Sub(0).Box(new Vector3(2.2f, -1.4f, 0), new Vector3(1.0f, 1.2f, 6f), grey * 0.9f, 0.3f);
            var go = mb.Build("Hull", _plane, body, Mats.Lit("plane_dark", Color.white, 0.6f, 0.6f));
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // spinning propeller blades
            foreach (var x in new[] { -13f, -6.5f, 6.5f, 13f })
            {
                var pr = new MB();
                pr.Box(Vector3.zero, new Vector3(3.6f, 0.25f, 0.06f), dark, 0.05f);
                pr.Box(Vector3.zero, new Vector3(0.25f, 3.6f, 0.06f), dark, 0.05f);
                var p = pr.Build("Prop", _plane, Mats.Lit("plane_prop", Color.white, 0.3f, 0.4f));
                p.transform.localPosition = new Vector3(x, 1.5f, 6.85f);
                _props.Add(p.transform);
            }
            // navigation lights
            foreach (var (x, c) in new[] { (-20f, new Color(1f, 0.1f, 0.1f)), (20f, new Color(0.1f, 1f, 0.2f)) })
            {
                var l = new GameObject("NavLight").AddComponent<Light>();
                l.transform.SetParent(_plane, false); l.transform.localPosition = new Vector3(x, 1.9f, 1.5f);
                l.type = LightType.Point; l.range = 12f; l.intensity = 2f; l.color = c;
            }
            _plane.position = _pos;
            _plane.rotation = Quaternion.LookRotation(_dir, Vector3.up);
            // the drone: heard from very far away, the doppler done by Unity
            _snd = _plane.gameObject.AddComponent<AudioSource>();
            _snd.clip = Synth.PlaneDrone(); _snd.loop = true; _snd.spatialBlend = 1f; _snd.dopplerLevel = 0.6f;
            _snd.rolloffMode = AudioRolloffMode.Linear; _snd.minDistance = 40f; _snd.maxDistance = 1100f;
            _snd.volume = 0.9f * Sfx.Effects; _snd.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _travel += _speed * dt;
            _pos += _dir * _speed * dt;
            if (_plane != null)
            {
                _plane.position = _pos;
                _plane.rotation = Quaternion.LookRotation(_dir, Vector3.up) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.4f) * 2f);
                foreach (var p in _props) if (p != null) p.Rotate(0, 0, 1400f * dt, Space.Self);
            }
            if (!_dropped)
            {
                var flat = _pos - _target; flat.y = 0;
                if (Vector3.Dot(flat, _dir) >= -8f)   // the crate leaves the ramp a little before it's over the zone and drifts on
                {
                    _dropped = true;
                    DropCrate.Make(_pos - _dir * 14f + Vector3.down * 2f, _target, _index, _boss);
                }
            }
            if (_travel > _length) Destroy(gameObject);
        }
    }

    /// <summary>The supply crate under its parachute, then on the ground.</summary>
    public class DropCrate : MonoBehaviour
    {
        Vector3 _target;
        int _index;
        bool _boss, _landed;
        Transform _chute;
        float _smokeT, _life;
        Light _beacon;
        LootCrate _crate;

        public static void Make(Vector3 from, Vector3 target, int index, bool boss)
        {
            var g = RagoGame.I;
            if (g == null || g.Map == null) return;
            var go = new GameObject("AirDropCrate");
            go.transform.SetParent(g.Map.transform, false);
            go.transform.position = from;
            var d = go.AddComponent<DropCrate>();
            d._target = target; d._index = index; d._boss = boss;
            // The Courier jumps with the crate and comes down under his own canopy a few metres beside it
            if (boss)
            {
                var side = Quaternion.Euler(0, index * 37f, 0) * new Vector3(3f, 0, 0.5f);
                ParaBoss.Make(from + side + Vector3.up * 6f, target + side, index);
            }
            // the crate itself while falling (the real, lootable crate replaces it on landing)
            var mb = new MB();
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1.0f, 1.25f), new Color(0.3f, 0.34f, 0.24f), 0.05f);
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(1.84f, 0.14f, 1.29f), new Color(0.15f, 0.15f, 0.14f), 0.02f);
            mb.Build("Falling", go.transform, Mats.Lit("drop_crate", Color.white, 0.1f, 0.35f, Mats.Wood, Mats.WoodN, 0.4f, 0.2f));
            // parachute: a wide dome with lines down to the crate
            d._chute = new GameObject("Chute").transform;
            d._chute.SetParent(go.transform, false);
            var cm = new MB();
            cm.Sphere(new Vector3(0, 8.5f, 0), new Vector3(6f, 2.2f, 6f), 16, new Color(0.35f, 0.38f, 0.28f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                cm.Cyl(new Vector3(Mathf.Cos(a) * 0.7f, 1.0f, Mathf.Sin(a) * 0.7f), new Vector3(Mathf.Cos(a) * 5.4f, 7.9f, Mathf.Sin(a) * 5.4f), 0.018f, 0.018f, 4, new Color(0.8f, 0.8f, 0.75f));
            }
            cm.Build("Canopy", d._chute, Mats.Lit("chute", Color.white, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.3f, 0f));
            d._beacon = new GameObject("Beacon").AddComponent<Light>();
            d._beacon.transform.SetParent(go.transform, false); d._beacon.transform.localPosition = new Vector3(0, 1.3f, 0);
            d._beacon.type = LightType.Point; d._beacon.color = new Color(1f, 0.15f, 0.1f); d._beacon.range = 9f; d._beacon.intensity = 2.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life += dt;
            if (_beacon != null) _beacon.intensity = Mathf.Repeat(Time.time, 1f) < 0.5f ? 2.5f : 0.3f;
            _smokeT -= dt;
            if (_crate != null && _crate.opened) { if (_beacon != null) _beacon.enabled = false; _life = Mathf.Max(_life, 999f); }
            if (_smokeT <= 0f && _life < 150f) { _smokeT = 0.35f; Fx.SignalSmoke(transform.position + Vector3.up * (_landed ? 0.9f : 0.2f), new Color(0.75f, 0.12f, 0.08f, 0.55f)); }
            if (_landed) { if (_life > 300f) Destroy(this); return; }
            // drift down onto the drop zone, swaying a little under the canopy
            Vector3 p = transform.position;
            Vector3 toZone = _target - p; toZone.y = 0;
            p += Vector3.ClampMagnitude(toZone, 4f * dt) + Vector3.down * 6f * dt;
            p += new Vector3(Mathf.Sin(_life * 0.7f), 0, Mathf.Cos(_life * 0.53f)) * 0.3f * dt;
            transform.position = p;
            transform.rotation = Quaternion.Euler(Mathf.Sin(_life * 0.9f) * 5f, _life * 6f, Mathf.Cos(_life * 0.8f) * 5f);
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 0.6f + 6f * dt, Ballistics.WallMask, QueryTriggerInteraction.Ignore) || p.y < _target.y - 0.2f)
                Land(hit.collider != null ? hit.point : new Vector3(p.x, _target.y, p.z));
        }

        void Land(Vector3 at)
        {
            _landed = true; _life = 0f;
            Sfx.Play(Synth.BodyFall(), at, 1f, 0.6f, 3f, 60f);
            Sfx.Play(Synth.Impact(Synth.Surface.Wood, 0), at, 1f, 0.7f, 3f, 50f);
            foreach (Transform c in transform) if (c != _beacon.transform) Destroy(c.gameObject);
            transform.rotation = Quaternion.identity;
            transform.position = at;
            var map = RagoGame.I != null ? RagoGame.I.Map : null;
            if (map == null) return;
            // the real crate: vault-grade loot, the same on every machine (same index, same raid seed)
            var crate = LootCrate.Create(map, _index, at, _index * 37f, LootCrate.Kind.AirDrop);
            crate.vaultGrade = true;
            _crate = crate;
            crate.transform.SetParent(transform, true);
            Physics.SyncTransforms();
            // The Courier and two heavies land with a random drop (one player - the same on every machine - spawns them for everyone)
            if (_boss && NpcDirector.I != null && NetGame.FirstPlayer)
            {
                var q = Quaternion.Euler(0, _index * 37f, 0);
                Spawn(1, at + q * new Vector3(-2.5f, 0, 1.5f), _index * 37f + 150f);
                Spawn(1, at + q * new Vector3(0.5f, 0, -3f), _index * 37f + 220f);
            }
            var pl = RagoGame.I.Player;
            if (pl != null && Vector3.Distance(pl.transform.position, at) < 300f) pl.Toast(_boss ? "The air drop is down - and THE COURIER came with it" : "The air drop is down - follow the red smoke");
        }

        static void Spawn(int kind, Vector3 pos, float yaw)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
            if (NetGame.Live) NetGame.I.RequestNpc(kind, pos, yaw);
            else NpcDirector.I.SpawnNow(pos, yaw, kind >= 1, Tester.BossFor(kind));
        }
    }

    /// <summary>The Courier under his parachute; on landing he becomes the real boss (the host spawns him for everyone).</summary>
    public class ParaBoss : MonoBehaviour
    {
        Vector3 _target; int _index; float _life;
        HumanRig _rig;

        public static void Make(Vector3 from, Vector3 target, int index)
        {
            var g = RagoGame.I;
            if (g == null || g.Map == null) return;
            var go = new GameObject("CourierChute");
            go.transform.SetParent(g.Map.transform, false);
            go.transform.position = from;
            var p = go.AddComponent<ParaBoss>();
            p._target = target; p._index = index;
            try
            {
                p._rig = HumanRig.Build(Outfit.BossCourier, index * 13 + 5, go.transform);
                p._rig.SetCollidersEnabled(false);
                ArmorBuilder.Dress(p._rig, Db.Get<ArmorDef>("boss_helm_courier"), Db.Get<ArmorDef>("boss_vest_courier"), null, false);
                var gun = GunBuilder.Build(Db.Get<GunDef>("boss_tommy"), p._rig.chest);
                gun.root.transform.localPosition = new Vector3(0.12f, -0.1f, 0.18f); gun.root.transform.localRotation = Quaternion.Euler(60, -30, 0);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] courier chute model: " + e.Message); }
            var cm = new MB();
            cm.Sphere(new Vector3(0, 7.2f, 0), new Vector3(3.6f, 1.4f, 3.6f), 16, new Color(0.18f, 0.18f, 0.2f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                cm.Cyl(new Vector3(Mathf.Cos(a) * 0.25f, 1.6f, Mathf.Sin(a) * 0.25f), new Vector3(Mathf.Cos(a) * 3.2f, 6.9f, Mathf.Sin(a) * 3.2f), 0.012f, 0.012f, 4, new Color(0.8f, 0.8f, 0.75f));
            }
            cm.Build("Canopy", go.transform, Mats.Lit("chute_boss", Color.white, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.3f, 0f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life += dt;
            Vector3 p = transform.position;
            Vector3 to = _target - p; to.y = 0;
            p += Vector3.ClampMagnitude(to, 4.5f * dt) + Vector3.down * 6.5f * dt;
            transform.position = p;
            transform.rotation = Quaternion.Euler(Mathf.Sin(_life * 1.1f) * 6f, _index * 37f + 180f + Mathf.Sin(_life * 0.4f) * 20f, Mathf.Cos(_life * 0.9f) * 6f);
            bool down = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 0.6f + 7f * dt, Ballistics.WallMask, QueryTriggerInteraction.Ignore) || p.y < _target.y - 0.2f || _life > 90f;
            if (!down) return;
            Vector3 at = hit.collider != null ? hit.point : new Vector3(p.x, _target.y, p.z);
            Sfx.Play(Synth.BodyFall(), at, 0.9f, 0.9f, 2f, 40f);
            if (NpcDirector.I != null && NetGame.FirstPlayer)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(at, out var nh, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = nh.position;
                int ck = Tester.KindOf("The Courier");   // (3.57) the Courier himself (kind 6 no longer existed: a plain heavy landed)
                if (NetGame.Live) NetGame.I.RequestNpc(ck, at, _index * 37f + 180f);
                else NpcDirector.I.SpawnNow(at, _index * 37f + 180f, true, Tester.BossFor(ck));
            }
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// The Military Radio: hold it and pull the trigger to call in an air drop where you're standing (not in the
    /// Underground - no signal). It's used up. The radio is also still worth money to the traders if you'd rather sell it.
    /// </summary>
    public class RadioItem : LootItem
    {
        float _armT;
        public override void HeldUpdate(Hand h, HandInput input)
        {
            if (!input.TriggerDown || Time.time < _armT) return;
            _armT = Time.time + 1f;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (!AirDrop.Allowed(out var why)) { pl.Toast(why); Sfx.UI(Synth.UiError(), 0.3f); return; }
            // the drop zone: the open ground nearest you (a roof over your head pushes it outside)
            Vector3 zone = pl.transform.position;
            if (Physics.Raycast(zone + Vector3.up * 300f, Vector3.down, out var hit, 600f, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && hit.point.y > zone.y + 2.5f)
            {
                var rng = new Rng(Random.Range(1, 99999));
                for (int i = 0; i < 24; i++)
                {
                    var c = zone + new Vector3(rng.Range(-30f, 30f), 0, rng.Range(-30f, 30f));
                    if (Physics.Raycast(c + Vector3.up * 300f, Vector3.down, out var h2, 600f, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && h2.normal.y > 0.8f && Mathf.Abs(h2.point.y - zone.y) < 4f) { zone = h2.point; break; }
                }
            }
            else if (hit.collider != null) zone = hit.point;
            Sfx.Play(Synth.Beep(), transform.position, 0.6f, 0.8f, 0.5f, 10f);
            h.Haptic(0.5f, 0.15f);
            AirDrop.Call(zone, Random.Range(0f, 360f), Random.value < 0.3f, true);
            Consumed = true;   // used up: it must not drop to the floor for the other players (it did, and they could use it again)
            h.ForceRelease();
            Destroy(gameObject);
        }
    }
}
