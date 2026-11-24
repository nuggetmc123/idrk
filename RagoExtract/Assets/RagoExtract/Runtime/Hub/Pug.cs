using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Every bunker has a pug. It pads around the main hall, follows you about, sleeps on its bed, and eats from its
    /// bowl - which you fill by touching the dog-food sack (or the bowl) with an empty hand and pulling the trigger.
    /// Stroke it and its tail goes like mad. A hungry pug with an empty bowl comes and sits at your feet and yaps.
    /// </summary>
    public class Pug : MonoBehaviour
    {
        enum Mode { Idle, Walk, Eat, Sleep, Follow, Beg, Fetch, Return }
        Mode _mode = Mode.Idle;
        Transform _body, _head, _tail, _kibble;
        readonly Transform[] _legs = new Transform[4];
        Vector3 _bed, _bowl, _sack, _target;
        Rect _area;
        float _yaw, _speed, _modeT, _hunger = 0.3f, _energy = 1f, _wag, _happy, _snortT = 3f, _gait, _barkT;
        static readonly Color Fawn = new Color(0.8f, 0.64f, 0.44f), Black = new Color(0.08f, 0.07f, 0.07f);

        public static Pug Create(Transform parent, Vector3 bed, Vector3 bowl, Rect area)
        {
            var mat = Mats.Lit("pug", Color.white, 0f, 0.3f, Mats.Fabric, Mats.FabricN, 0.3f, 0f);
            // ---- bed: a round cushion with a raised rim
            var bmb = new MB();
            bmb.Cyl(bed + new Vector3(0, 0, 0), bed + new Vector3(0, 0.1f, 0), 0.42f, 0.4f, 20, new Color(0.45f, 0.12f, 0.12f));
            bmb.Cyl(bed + new Vector3(0, 0.1f, 0), bed + new Vector3(0, 0.12f, 0), 0.32f, 0.3f, 20, new Color(0.75f, 0.68f, 0.55f));
            bmb.Build("PugBed", parent, mat);
            // her name on a little wooden sign at the front of the bed, facing the room
            {
                Vector3 toRoom = new Vector3(-bed.x, 0, -bed.z); toRoom = toRoom.sqrMagnitude > 0.01f ? toRoom.normalized : Vector3.back;
                var sign = new GameObject("PugName").transform;
                sign.SetParent(parent, false);
                sign.localPosition = bed + toRoom * 0.47f + Vector3.up * 0.2f;
                sign.localRotation = Quaternion.LookRotation(-toRoom) * Quaternion.Euler(-12f, 0, 0);
                var nameMb = new MB();
                nameMb.Box(Vector3.zero, new Vector3(0.36f, 0.12f, 0.02f), new Color(0.55f, 0.38f, 0.22f), 0.01f);
                nameMb.Box(new Vector3(-0.14f, -0.1f, 0.012f), new Vector3(0.02f, 0.14f, 0.02f), new Color(0.4f, 0.27f, 0.15f));
                nameMb.Box(new Vector3(0.14f, -0.1f, 0.012f), new Vector3(0.02f, 0.14f, 0.02f), new Color(0.4f, 0.27f, 0.15f));
                nameMb.Build("Sign", sign, Mats.Lit("pug_sign", Color.white, 0f, 0.3f, Mats.Wood, null, 0.3f, 0.1f));
                UI.Text(sign, "MANDA", new Vector3(0, 0, -0.012f), 0.05f, new Color(1f, 0.93f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            // ---- bowl, sack of food beside it
            var steel = Mats.Lit("pug_bowl", Color.white, 0.8f, 0.6f, Mats.Metal, null, 0.2f, 0.2f);
            new MB().Cyl(bowl, bowl + new Vector3(0, 0.07f, 0), 0.11f, 0.14f, 18, new Color(0.7f, 0.72f, 0.75f)).Build("PugBowl", parent, steel);
            Vector3 sack = bowl + new Vector3(0.45f, 0, 0.1f);
            var smb = new MB();
            smb.Box(sack + new Vector3(0, 0.25f, 0), new Vector3(0.32f, 0.5f, 0.16f), new Color(0.75f, 0.55f, 0.2f), 0.03f, 2);
            smb.Box(sack + new Vector3(0, 0.51f, 0), new Vector3(0.3f, 0.04f, 0.12f), new Color(0.6f, 0.42f, 0.15f), 0.01f);
            smb.Box(sack + new Vector3(0, 0.27f, 0.081f), new Vector3(0.18f, 0.14f, 0.003f), new Color(0.9f, 0.15f, 0.12f), 0.002f);
            smb.Build("DogFood", parent, mat);
            var label = UI.Text(parent, "DOG FOOD", sack + new Vector3(0, 0.27f, 0.084f), 0.022f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            label.transform.localPosition = sack + new Vector3(0, 0.27f, 0.085f);

            var go = new GameObject("Pug");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = bed + new Vector3(0, 0.12f, 0);
            var p = go.AddComponent<Pug>();
            p._bed = bed; p._bowl = bowl; p._sack = sack; p._area = area;
            p.BuildModel(mat);
            // the kibble in the bowl: a disc that rises with how full it is
            p._kibble = new MB().Cyl(Vector3.zero, new Vector3(0, 0.01f, 0), 0.1f, 0.1f, 14, new Color(0.45f, 0.28f, 0.12f)).Build("Kibble", parent, mat).transform;
            p._kibble.localPosition = bowl + new Vector3(0, 0.015f, 0);
            p.ShowBowl();
            p._mode = Mode.Sleep; p._modeT = 6f;
            p.Invoke(nameof(EnsureBall), 1.5f);   // after the bunker's saved items are back
            return p;
        }

        /// <summary>Her tennis ball: one by her bed if there isn't one anywhere in the bunker (stash, floor, your hands).</summary>
        void EnsureBall()
        {
            foreach (var it in Item.All) if (it != null && !it.Gone && it.def != null && it.def.id == "dog_ball") return;
            var prof = Profile.Current;
            if (prof != null && prof.stash != null && prof.stash.Exists(d => d != null && d.id == "dog_ball")) return;
            ItemFactory.Spawn(new ItemData("dog_ball"), transform.parent.TransformPoint(_bed + new Vector3(0.3f, 0.25f, 0.3f)), Quaternion.identity);
        }

        void BuildModel(Material mat)
        {
            _body = new GameObject("Body").transform; _body.SetParent(transform, false);
            var mb = new MB();
            mb.Sphere(new Vector3(0, 0, 0), new Vector3(0.14f, 0.13f, 0.21f), 14, Fawn);                      // round little body
            mb.Sphere(new Vector3(0, 0.02f, 0.12f), new Vector3(0.13f, 0.12f, 0.1f), 12, Fawn);                // chest
            mb.Build("Torso", _body, mat);
            _head = new GameObject("Head").transform; _head.SetParent(_body, false); _head.localPosition = new Vector3(0, 0.08f, 0.22f);
            var hb = new MB();
            hb.Sphere(Vector3.zero, new Vector3(0.1f, 0.095f, 0.09f), 14, Fawn);                                 // big round head
            hb.Box(new Vector3(0, -0.025f, 0.075f), new Vector3(0.11f, 0.065f, 0.05f), Black, 0.02f);            // flat black muzzle
            hb.Box(new Vector3(0, 0.0f, 0.1f), new Vector3(0.035f, 0.022f, 0.012f), new Color(0.02f, 0.02f, 0.02f), 0.006f);   // nose
            foreach (int s in new[] { -1, 1 })
            {
                hb.Sphere(new Vector3(s * 0.045f, 0.025f, 0.07f), Vector3.one * 0.019f, 8, new Color(0.03f, 0.02f, 0.02f));   // googly eyes
                hb.Sphere(new Vector3(s * 0.05f, 0.03f, 0.085f), Vector3.one * 0.006f, 6, Color.white);
                hb.Box(new Vector3(s * 0.08f, 0.07f, 0.0f), new Vector3(0.045f, 0.04f, 0.015f), Black, 0.008f, 1, Quaternion.Euler(0, 0, s * 35f));   // floppy ears
            }
            hb.Box(new Vector3(0, 0.045f, 0.07f), new Vector3(0.08f, 0.012f, 0.02f), Fawn * 0.85f, 0.005f);      // forehead wrinkle
            hb.Build("Face", _head, mat);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2; int side = i % 2 == 0 ? -1 : 1;
                _legs[i] = new GameObject("Leg" + i).transform; _legs[i].SetParent(_body, false);
                _legs[i].localPosition = new Vector3(side * 0.08f, -0.06f, front ? 0.12f : -0.12f);
                new MB().Cyl(Vector3.zero, new Vector3(0, -0.12f, 0), 0.03f, 0.026f, 8, Fawn).Sphere(new Vector3(0, -0.12f, 0.01f), new Vector3(0.03f, 0.018f, 0.04f), 8, Fawn * 0.9f).Build("L", _legs[i], mat);
            }
            _tail = new GameObject("Tail").transform; _tail.SetParent(_body, false); _tail.localPosition = new Vector3(0, 0.1f, -0.18f);
            new MB().Sphere(new Vector3(0, 0.03f, 0), new Vector3(0.035f, 0.035f, 0.035f), 8, Fawn).Sphere(new Vector3(0, 0.05f, 0.02f), new Vector3(0.022f, 0.022f, 0.022f), 8, Fawn * 0.9f).Build("Curl", _tail, mat);
        }

        float Bowl { get => Profile.Current != null ? Profile.Current.pugBowl : 0f; set { if (Profile.Current != null) Profile.Current.pugBowl = Mathf.Clamp01(value); ShowBowl(); } }
        void ShowBowl()
        {
            if (_kibble == null) return;
            float b = Profile.Current != null ? Profile.Current.pugBowl : 0f;
            _kibble.gameObject.SetActive(b > 0.01f);
            _kibble.localPosition = _bowl + new Vector3(0, 0.012f + b * 0.04f, 0);
            _kibble.localScale = new Vector3(0.7f + b * 0.35f, 1, 0.7f + b * 0.35f);
        }

        Vector3 Pos { get => transform.localPosition; set => transform.localPosition = value; }

        void Update()
        {
            float dt = Time.deltaTime;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            Vector3 me = pl != null ? transform.parent.InverseTransformPoint(pl.transform.position) : Pos + Vector3.forward * 10f;
            me.y = 0;
            if (_mode != Mode.Sleep) _energy -= dt / 240f; else _energy = Mathf.Min(1f, _energy + dt / 45f);
            _hunger = Mathf.Min(1f, _hunger + dt / 420f);
            _happy = Mathf.MoveTowards(_happy, 0, dt * 0.25f);
            _modeT -= dt;
            if (pl != null) { Feed(pl.HandL); Feed(pl.HandR); Pet(pl.HandL); Pet(pl.HandR); }
            WatchForThrows();

            Vector3 flat = Pos; flat.y = 0;
            float distMe = Vector3.Distance(flat, me);
            switch (_mode)
            {
                case Mode.Sleep:
                    Steer(_bed, 0f, dt, 0.1f);
                    if (_modeT <= 0 && _energy > 0.95f) Next(me);
                    break;
                case Mode.Eat:
                    if (Vector3.Distance(flat, _bowl) > 0.35f) { Steer(_bowl + (_bed - _bowl).normalized * 0.28f, 0.7f, dt); break; }
                    Face(_bowl, dt);
                    if (Bowl > 0.01f && _hunger > 0.02f)
                    {
                        float bite = dt * 0.08f; Bowl -= bite; _hunger = Mathf.Max(0f, _hunger - bite * 6f);
                        if ((_snortT -= dt) <= 0) { _snortT = Random.Range(0.25f, 0.5f); Sfx.Play(Synth.Crunch(), transform.position + Vector3.up * 0.2f, 0.35f, Random.Range(0.9f, 1.15f), 0.3f, 6f); }
                    }
                    else { _happy = 1f; Next(me); }
                    break;
                case Mode.Walk:
                    Steer(_target, 0.65f, dt);
                    if (Vector3.Distance(flat, _target) < 0.25f || _modeT <= 0) { _mode = Mode.Idle; _modeT = Random.Range(2f, 6f); }
                    break;
                case Mode.Follow:
                    if (distMe > 1.2f) Steer(me + (flat - me).normalized * 0.9f, distMe > 3f ? 1.3f : 0.75f, dt); else { _speed = 0; Face(me, dt); }
                    if (_modeT <= 0 || distMe > 9f) Next(me);
                    break;
                case Mode.Beg:
                    if (distMe > 0.9f) Steer(me + (flat - me).normalized * 0.7f, 1f, dt); else { _speed = 0; Face(me, dt); if ((_barkT -= dt) <= 0) { _barkT = Random.Range(2.5f, 5f); Bark(); } }
                    if (Bowl > 0.05f) { _mode = Mode.Eat; }
                    else if (_modeT <= 0) Next(me);
                    break;
                case Mode.Fetch:
                {
                    // chase the thrown thing: wait for it to land, then run to it and pick it up
                    if (!Fetchable(_fetch, false)) { _fetch = null; Next(me); break; }
                    Vector3 at = transform.parent.InverseTransformPoint(_fetch.transform.position); at.y = 0;
                    bool moving = _fetch.rb != null && _fetch.rb.Vel().sqrMagnitude > 0.5f * 0.5f;
                    if (Vector3.Distance(flat, at) > 0.22f) Steer(at, moving ? 1.1f : 1.7f, dt, 0.18f);
                    else if (!moving) { PickUp(_fetch); _mode = Mode.Return; _modeT = 25f; }
                    if (_modeT <= 0) { _fetch = null; Next(me); }
                    break;
                }
                case Mode.Return:
                    // trot back to you with it and drop it at your feet
                    if (_carry == null || _carry.Held || _carry.slot != null) { _carry = null; _happy = 1f; Next(me); break; }   // you took it from her mouth
                    if (distMe > 0.75f) Steer(me + (flat - me).normalized * 0.6f, 1.4f, dt, 0.1f);
                    else { _speed = 0; Face(me, dt); DropCarry(); _happy = 1f; Bark(); _mode = Mode.Idle; _modeT = 4f; }
                    if (_modeT <= 0) { DropCarry(); Next(me); }
                    break;
                default:   // idle: sit and watch you
                    _speed = Mathf.MoveTowards(_speed, 0, dt * 2f);
                    if (distMe < 6f) Face(me, dt);
                    if (_modeT <= 0) Next(me);
                    break;
            }
            // snorts and snuffles now and then (a slow snore asleep)
            if (_mode != Mode.Eat && (_snortT -= dt) <= 0)
            {
                _snortT = _mode == Mode.Sleep ? Random.Range(2.5f, 3.5f) : Random.Range(4f, 11f);
                Sfx.Play(Synth.PugSnort(Random.Range(0, 3)), transform.position + Vector3.up * 0.25f, _mode == Mode.Sleep ? 0.25f : 0.4f, _mode == Mode.Sleep ? 0.75f : Random.Range(0.95f, 1.2f), 0.4f, 9f);
            }
            Animate(dt, me);
        }

        // ---- fetch: throw her tennis ball (or anything small) and she brings it back
        Item _fetch, _carry; Transform _mouth;
        readonly System.Collections.Generic.HashSet<Item> _ignored = new System.Collections.Generic.HashSet<Item>();
        float _scanT;

        bool Fetchable(Item it, bool needFlying)
        {
            if (it == null || it.Gone || it.Held || it.slot != null || it.rb == null || it.rb.isKinematic || !it.gameObject.activeInHierarchy) return false;
            var b = it.localBounds.size; if (Mathf.Max(b.x, Mathf.Max(b.y, b.z)) > 0.4f) return false;   // only small things fit in a pug's mouth
            Vector3 lp = transform.parent.InverseTransformPoint(it.transform.position);
            if (lp.x < _area.xMin - 1f || lp.x > _area.xMax + 1f || lp.z < _area.yMin - 1f || lp.z > _area.yMax + 1f) return false;   // out of her room
            if (needFlying) { var v = it.rb.Vel(); v.y = 0; if (v.sqrMagnitude < 2.2f * 2.2f) return false; }   // thrown (moving sideways), not just dropped
            return true;
        }

        void WatchForThrows()
        {
            if (_mode == Mode.Fetch || _mode == Mode.Return || _mode == Mode.Eat || (_mode == Mode.Sleep && _energy < 0.6f)) return;
            if ((_scanT -= Time.deltaTime) > 0f) return;
            _scanT = 0.1f;
            Item best = null;
            foreach (var it in Item.All)
            {
                if (!Fetchable(it, true)) continue;
                if (best == null || (it.def != null && it.def.id == "dog_ball")) best = it;   // her ball first
            }
            if (best == null) return;
            _fetch = best; _mode = Mode.Fetch; _modeT = 20f; Bark();
        }

        void PickUp(Item it)
        {
            _carry = it; _fetch = null;
            if (_mouth == null) { _mouth = new GameObject("Mouth").transform; _mouth.SetParent(_head, false); _mouth.localPosition = new Vector3(0, -0.04f, 0.13f); }
            it.rb.isKinematic = true;
            it.SetCollidersTrigger(true);   // doesn't knock into her while she carries it
            Sfx.Play(Synth.PugSnort(1), _head.position, 0.4f, 1.3f, 0.4f, 8f);
        }

        void DropCarry()
        {
            var it = _carry; _carry = null;
            if (it == null || it.Held || it.slot != null) return;
            it.SetCollidersTrigger(false);
            it.rb.isKinematic = false;
            it.rb.SetVel(transform.forward * 0.4f + Vector3.up * 0.3f);
        }

        void LateUpdate()
        {
            // the thing she's carrying sits in her mouth (not parented: if the bunker saves now, it's saved where it is)
            if (_carry == null || _mouth == null) return;
            if (_carry.Held || _carry.slot != null) { _carry = null; return; }
            _carry.transform.SetPositionAndRotation(_mouth.position, _mouth.rotation);
            if (_carry.rb != null) _carry.rb.position = _mouth.position;
        }

        void OnDestroy() { if (_carry != null && _carry.rb != null) { _carry.SetCollidersTrigger(false); _carry.rb.isKinematic = false; } }

        void Next(Vector3 me)
        {
            float distMe = Vector3.Distance(new Vector3(Pos.x, 0, Pos.z), me);
            if (_hunger > 0.55f && Bowl > 0.05f) { _mode = Mode.Eat; return; }
            if (_hunger > 0.75f && distMe < 12f) { _mode = Mode.Beg; _modeT = 20f; _barkT = 0.5f; return; }
            if (_energy < 0.2f) { _mode = Mode.Sleep; _modeT = Random.Range(40f, 90f); return; }
            float r = Random.value;
            if (distMe < 8f && r < 0.4f) { _mode = Mode.Follow; _modeT = Random.Range(12f, 30f); return; }
            _target = new Vector3(Random.Range(_area.xMin, _area.xMax), 0, Random.Range(_area.yMin, _area.yMax));
            _mode = Mode.Walk; _modeT = 15f;
        }

        void Steer(Vector3 to, float speed, float dt, float stopAt = 0.05f)
        {
            Vector3 p = Pos, d = to - new Vector3(p.x, 0, p.z); d.y = 0;
            float dist = d.magnitude;
            if (dist < stopAt) { _speed = Mathf.MoveTowards(_speed, 0, dt * 3f); return; }
            Face(to, dt);
            _speed = Mathf.MoveTowards(_speed, speed, dt * 2f);
            Vector3 fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            // don't walk into walls or furniture
            Vector3 wp = transform.parent.TransformPoint(p + Vector3.up * 0.15f);
            if (Physics.Raycast(wp, transform.parent.TransformDirection(fwd), 0.35f, Ballistics.WallMask, QueryTriggerInteraction.Ignore))
            {
                _yaw += 90f * dt * 3f; _speed *= 0.5f;
                if (_mode == Mode.Walk) _modeT = Mathf.Min(_modeT, 1f);
                return;
            }
            p += fwd * _speed * dt;
            p.x = Mathf.Clamp(p.x, _area.xMin - 1f, _area.xMax + 1f); p.z = Mathf.Clamp(p.z, _area.yMin - 1f, _area.yMax + 1f);
            Pos = p;
        }

        void Face(Vector3 to, float dt)
        {
            Vector3 d = to - Pos; d.y = 0;
            if (d.sqrMagnitude < 0.0004f) return;
            float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _yaw = Mathf.MoveTowardsAngle(_yaw, want, dt * 220f);
        }

        void Feed(Hand h)
        {
            if (h == null || h.Held != null || !h.Input.TriggerDown) return;
            Vector3 hp = transform.parent.InverseTransformPoint(h.TrackedPosition);
            if (Vector3.Distance(hp, _sack + Vector3.up * 0.3f) > 0.3f && Vector3.Distance(hp, _bowl + Vector3.up * 0.05f) > 0.25f) return;
            if (Bowl > 0.97f) { h.Haptic(0.15f, 0.03f); return; }
            Bowl = 1f;
            Profile.Current?.Save();
            Sfx.Play(Synth.Kibble(), transform.parent.TransformPoint(_bowl + Vector3.up * 0.1f), 0.6f, 1f, 0.5f, 8f);
            h.Haptic(0.4f, 0.25f);
            if (_mode != Mode.Sleep && _hunger > 0.2f) { _mode = Mode.Eat; Bark(); }
        }

        void Pet(Hand h)
        {
            if (h == null || h.Held != null) return;
            Vector3 hp = h.TrackedPosition;
            if (Vector3.Distance(hp, _head.position) > 0.17f && Vector3.Distance(hp, _body.position + Vector3.up * 0.1f) > 0.18f) return;
            if (h.Velocity.magnitude < 0.12f) return;
            if (_happy < 0.2f) Sfx.Play(Synth.PugSnort(Random.Range(0, 3)), _head.position, 0.45f, 1.25f, 0.4f, 8f);
            _happy = 1f;
            h.Haptic(0.12f, 0.03f);
            if (_mode == Mode.Idle || _mode == Mode.Walk) { _mode = Mode.Idle; _modeT = Mathf.Max(_modeT, 3f); }
        }

        void Bark() { Sfx.Play(Synth.PugBark(), _head != null ? _head.position : transform.position, 0.55f, Random.Range(0.95f, 1.1f), 0.8f, 18f); _happy = Mathf.Max(_happy, 0.5f); }

        void Animate(float dt, Vector3 me)
        {
            transform.localRotation = Quaternion.Euler(0, _yaw, 0);
            bool asleep = _mode == Mode.Sleep && Vector3.Distance(new Vector3(Pos.x, 0, Pos.z), _bed) < 0.2f;
            bool sitting = !asleep && _speed < 0.05f;
            _gait += dt * _speed * 14f;
            float breathe = Mathf.Sin(Time.time * (asleep ? 1.6f : 3.5f)) * (asleep ? 0.012f : 0.004f);
            float bob = Mathf.Abs(Mathf.Sin(_gait)) * 0.012f * Mathf.Clamp01(_speed * 2f);
            float floorY = asleep ? 0.12f : 0f;   // on the cushion
            float h = asleep ? 0.06f : sitting ? 0.16f : 0.18f;
            _body.localPosition = new Vector3(0, floorY + h + bob - (asleep ? 0 : 0), 0);
            _body.localRotation = Quaternion.Euler(sitting ? -18f : 0f, 0, asleep ? 8f : 0f);
            _body.localScale = new Vector3(1f + breathe, 1f + breathe, 1f);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float swing = Mathf.Sin(_gait + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 30f * Mathf.Clamp01(_speed * 2f);
                if (asleep) { _legs[i].localRotation = Quaternion.Euler(front ? -80f : 80f, 0, 0); _legs[i].localScale = new Vector3(1, 0.6f, 1); }
                else if (sitting && !front) { _legs[i].localRotation = Quaternion.Euler(-60f, 0, 0); _legs[i].localScale = new Vector3(1, 0.8f, 1); }
                else { _legs[i].localRotation = Quaternion.Euler(swing, 0, 0); _legs[i].localScale = Vector3.one; }
            }
            // head: watches you when you're near, droops asleep, dips to the bowl when eating
            Quaternion headWant = Quaternion.identity;
            if (asleep) headWant = Quaternion.Euler(25f, 0, 15f);
            else if (_mode == Mode.Eat && _speed < 0.1f) headWant = Quaternion.Euler(45f + Mathf.Sin(Time.time * 9f) * 6f, 0, 0);
            else if (RagoGame.I != null && RagoGame.I.Player != null && Vector3.Distance(transform.position, RagoGame.I.Player.Head.position) < 5f)
            {
                Vector3 look = _body.InverseTransformPoint(RagoGame.I.Player.Head.position) - _head.localPosition;
                float y = Mathf.Clamp(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, -60f, 60f), x = Mathf.Clamp(-Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg, -40f, 25f);
                headWant = Quaternion.Euler(x, y, Mathf.Sin(Time.time * 0.7f) * 8f);   // the head tilt
            }
            _head.localRotation = Quaternion.Slerp(_head.localRotation, headWant, 1f - Mathf.Exp(-6f * dt));
            // tail: a happy pug's curl wiggles hard
            float wagRate = asleep ? 0f : 6f + _happy * 22f + (_mode == Mode.Beg || _mode == Mode.Follow ? 8f : 0f);
            _wag += dt * wagRate;
            _tail.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_wag) * (asleep ? 0f : 18f + _happy * 22f));
        }
    }
}
