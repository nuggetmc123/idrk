using UnityEngine;

namespace Rago
{
    /// <summary>Another player: interpolated ragdoll body with IK hands, held weapon models, name tag and hit zones.</summary>
    public class RemotePlayer : MonoBehaviour, IDamageable
    {
        public ulong NetId;
        public string DisplayName = "Operator";
        TextMesh _tape; string _tapeName;
        HumanRig _rig;
        TextMesh _tag;
        GameObject _dot;
        Vector3 _stepLast; float _stepDist; bool _stepLeft;
        Vector3 _root, _head, _hl, _hr, _vel;
        Quaternion _headR = Quaternion.identity, _hlR = Quaternion.identity, _hrR = Quaternion.identity;
        Vector3 _sRoot, _sHead, _sHl, _sHr;
        Quaternion _sHeadR = Quaternion.identity, _sHlR = Quaternion.identity, _sHrR = Quaternion.identity;
        string _heldL = "", _heldR = "";
        GameObject _modelL, _modelR;
        GunRig _gunR;
        Item _itemL, _itemR;            // display copies of what they hold (their gun with its sight, can, light / laser)
        bool _poseL, _poseR, _supL, _supR;
        Vector3 _gl, _gr, _sGl, _sGr; Quaternion _glR = Quaternion.identity, _grR = Quaternion.identity, _sGlR = Quaternion.identity, _sGrR = Quaternion.identity;
        bool _dead, _hasPose;
        public bool IsDead => _dead;
        float _packetT;
        float _yaw;
        public bool Alive => !_dead;
        public bool HiddenFromAi;   // tester: raiders ignore this player (other players still see them)
        public int ActorId => (int)NetId + 1;
        public Transform Chest => _rig != null ? _rig.chest : transform;

        /// <summary>They're on fire (their own game says so): flames on their chest while it lasts.</summary>
        public void ShowBurning()
        {
            var b = Burning.Ignite(Chest, 0.6f, null);
            if (b != null) b.timeLeft = Mathf.Max(b.timeLeft, 0.6f);
        }
        public Vector3 HeadPos => _sHead;
        public Quaternion HeadRot => _sHeadR;
        public string HoldingRight => _heldR;
        public string HoldingLeft => _heldL;

        public static RemotePlayer Create(ulong id, int seed)
        {
            var go = new GameObject("Remote_" + id);
            var rp = go.AddComponent<RemotePlayer>();
            rp.NetId = id;
            rp._rig = HumanRig.Build(Outfit.Operator, seed, go.transform);
            foreach (var c in rp._rig.colliders)
            {
                var z = c.gameObject.AddComponent<HitZone>();
                z.owner = rp;
                z.part = PlayerBody.PartFor(c.transform, rp._rig);
                z.mult = HitZone.MultFor(z.part);
            }
            rp.IgnoreLocalBody();
            rp._tag = UI.Text(go.transform, "", Vector3.zero, 0.05f, new Color(0.7f, 0.9f, 1f));
            // teammate marker: a green dot over their head, seen through walls and kept a readable size at any distance
            rp._dot = new MB().Sphere(Vector3.zero, Vector3.one * 0.5f, 10, Color.white).Build("TeamDot", go.transform, Mats.Unlit("team_dot", new Color(0.2f, 1f, 0.35f, 0.95f), null, Mats.BlendKind.Alpha, false, 50, true));
            rp._dot.SetActive(false);
            return rp;
        }

        /// <summary>
        /// Another player's body never shoves you about: standing in the same spot (arriving in the Market lift, spawning
        /// side by side) the movement capsule used to push you out of their body every frame - you jumped around.
        /// Bullets still hit them as before.
        /// </summary>
        void IgnoreLocalBody()
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null || pl.Cc == null || _rig == null) return;
            foreach (var c in _rig.colliders) if (c != null) Physics.IgnoreCollision(c, pl.Cc, true);
        }

        public void ApplyHeld(string heldL, string heldR)
        {
            if (_dead) return;
            if (heldL != _heldL) { _heldL = heldL; SetHeld(true, heldL); }
            if (heldR != _heldR) { _heldR = heldR; SetHeld(false, heldR); }
        }

        public void ApplyHeldPose(bool hasL, Vector3 gl, Quaternion glR, bool hasR, Vector3 gr, Quaternion grR, bool supL, bool supR, bool sideOn)
        {
            bool firstL = hasL && !_poseL, firstR = hasR && !_poseR;
            _poseL = hasL; _poseR = hasR; _supL = supL; _supR = supR;
            if (hasL) { _gl = gl; _glR = glR; if (firstL) { _sGl = gl; _sGlR = glR; } }
            if (hasR) { _gr = gr; _grR = grR; if (firstR) { _sGr = gr; _sGrR = grR; } }
            foreach (var it in new[] { _itemL, _itemR })
                if (it is Gun g) { g.RemoteOn = true; g.SideOn = sideOn; }
        }

        public void ApplyPose(bool dead, Vector3 root, Vector3 head, Quaternion headR, Vector3 hl, Quaternion hlR, Vector3 hr, Quaternion hrR, Vector3 vel)
        {
            if (dead && !_dead) Die();
            if (!dead && _dead) Revive();
            _root = root; _head = head; _headR = headR; _hl = hl; _hlR = hlR; _hr = hr; _hrR = hrR; _vel = vel;
            _packetT = Time.time;
            if (!_hasPose) { _hasPose = true; _sRoot = root; _sHead = head; _sHl = hl; _sHr = hr; _sHeadR = headR; _sHlR = hlR; _sHrR = hrR; }
        }

        string _gear = "";
        /// <summary>Shows the helmet, vest and backpack this player is wearing.</summary>
        public void ApplyGear(string key)
        {
            key = key ?? "";
            if (key == _gear) return;
            _gear = key;
            var parts = key.Split('|');
            string P(int i) => parts.Length > i ? parts[i] : "";
            ArmorBuilder.Dress(_rig, Db.Get<ArmorDef>(P(0)), Db.Get<ArmorDef>(P(1)), Db.Get<BackpackDef>(P(2)), false, P(3) == "2" ? 2 : P(3) == "1" ? 1 : 0);
        }

        void SetHeld(bool left, string key)
        {
            ref GameObject slot = ref left ? ref _modelL : ref _modelR;
            if (slot != null) Destroy(slot);
            slot = null;
            if (!left) _gunR = null;
            if (left) _itemL = null; else _itemR = null;
            var parts = (key ?? "").Split(';');
            string id = parts[0];
            var def = Db.Get(id);
            if (def == null) return;
            // the real item, made into a display copy: right model, attachments where they belong, lights and lasers
            // that work - but nobody can grab it, it has no physics, and it isn't part of the world's items
            string P(int i) => parts.Length > i ? parts[i] : "";
            float F(int i) => float.TryParse(P(i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
            var data = new ItemData(id) { sight = P(1), muzzle = P(2), side = P(3), gripAtt = P(4), rounds = 0, chambered = false,
                                          sightZ = F(5), sideZ = F(6), gripZ = F(7), sideLeft = P(8) == "1", skin = P(9) };
            var disp = ItemFactory.Spawn(data, transform.position, transform.rotation);
            if (disp != null)
            {
                foreach (var it in disp.GetComponentsInChildren<Item>(true))
                {
                    Item.All.Remove(it);
                    if (it.rb != null) { it.rb.isKinematic = true; it.rb.detectCollisions = false; }
                }
                foreach (var c in disp.GetComponentsInChildren<Collider>(true)) Destroy(c);
                disp.transform.SetParent(transform, true);
                if (disp is Gun dg) { dg.RemoteOn = true; _gunR = left ? _gunR : dg.rig; }
                slot = disp.gameObject;
                if (left) _itemL = disp; else _itemR = disp;
                return;
            }
            var hand = left ? _rig.handL : _rig.handR;
            if (def is GunDef g)
            {
                var rig = GunBuilder.Build(g, hand);
                slot = rig.root;
                if (!left) _gunR = rig;
            }
            else
            {
                var it = ItemFactory.Spawn(new ItemData(id), hand.position, hand.rotation);
                if (it == null) return;
                foreach (var c in it.GetComponentsInChildren<Collider>()) Destroy(c);
                Destroy(it.rb); Destroy(it);
                it.transform.SetParent(hand, false);
                it.transform.localPosition = new Vector3(0, 0, 0.05f);
                slot = it.gameObject;
            }
        }

        VoicePlayback _voice;
        public float LastVoiceTime = -10f;
        public float LastHeard;   // Time.time of their last movement update
        float _noiseT;

        public void PlayVoice(byte[] frame, bool radio = false)
        {
            if (_voice == null) _voice = VoicePlayback.Create(transform);
            _voice.Radio = radio;
            _voice.Push(frame);
            LastVoiceTime = Time.time;
        }

        public void MuzzleFlash(GunDef g)
        {
            if (_gunR != null && _gunR.muzzle != null) Fx.MuzzleFlash(_gunR.muzzle, g.TwoHanded ? 1.2f : 0.7f, g.suppressed);
        }

        public void TakeHit(HitInfo h, HitZone zone)
        {
            // damage is applied by the owning client; the shooter sends it over the network (NetGame.OnHitActor)
        }

        public void Die()
        {
            if (_dead) return;
            _dead = true;
            _rig.SetRagdoll(true, _vel * 0.5f, _rig.chest.position, _vel);
            if (_modelL != null) Destroy(_modelL);
            if (_modelR != null) Destroy(_modelR);
            _itemL = _itemR = null;
            _heldL = _heldR = "";
            Fx.BloodPool(_rig.chest.position);
            Sfx.Play(Synth.BodyFall(), _rig.hips.position, 0.8f, 1f, 1f, 25f);
        }

        void Revive()
        {
            _dead = false;
            Destroy(_rig.root);
            _rig = HumanRig.Build(Outfit.Operator, (int)NetId * 31, transform);
            foreach (var c in _rig.colliders) { var z = c.gameObject.AddComponent<HitZone>(); z.owner = this; z.part = PlayerBody.PartFor(c.transform, _rig); z.mult = HitZone.MultFor(z.part); }
            IgnoreLocalBody();
            var g = _gear; _gear = null; ApplyGear(g);
        }

        // ---- snapshot interpolation: every pose carries the sender's clock; we show them a fixed moment (~0.12 s) in
        // the past, smoothly between the two poses either side of it. Packets arriving in bunches (3+ players, a busy
        // line) no longer make players jump, overshoot and snap back - the old "keep moving and correct" did, and with
        // several players it looked like everyone teleporting into each other.
        struct Snap { public float t; public Vector3 root, head, hl, hr, gl, gr, vel; public Quaternion headR, hlR, hrR, glR, grR; }
        readonly System.Collections.Generic.List<Snap> _snaps = new System.Collections.Generic.List<Snap>();
        float _offset = float.MaxValue;   // our clock minus theirs, for the quickest packet seen (slowly relaxed)
        float _ia = 0.07f, _jit = 0.02f, _lastArrive;
        // show them just far enough behind to cover how unevenly their updates arrive: the average gap plus twice the
        // jitter. A steady line shows them ~80 ms behind; a ragged one buys more buffer instead of stuttering.
        float Delay => Mathf.Clamp(_ia + _jit * 2f + 0.005f, 0.05f, 0.36f);   // near players (15/s) ~90 ms; far ones get fewer updates, so a longer buffer keeps them smooth   // just enough buffer for one late packet (it was up to twice the packet gap: players looked behind)

        public void CommitSnap(float senderT)
        {
            if (_snaps.Count > 0 && senderT <= _snaps[_snaps.Count - 1].t) return;   // old or duplicate
            if (_snaps.Count > 0 && senderT - _snaps[_snaps.Count - 1].t > 2f) _snaps.Clear();   // a long gap: start over
            float off = Time.time - senderT;
            if (off < _offset) _offset = off;
            // how evenly poses arrive: the show-delay grows with the jitter (relayed through the host, a busy line)
            if (_lastArrive > 0f) { float gap = Time.time - _lastArrive; _jit = Mathf.Lerp(_jit, Mathf.Abs(gap - _ia), 0.1f); _ia = Mathf.Lerp(_ia, gap, 0.1f); }
            _lastArrive = Time.time;
            _snaps.Add(new Snap { t = senderT, root = _root, head = _head, hl = _hl, hr = _hr, gl = _gl, gr = _gr, vel = _vel, headR = _headR, hlR = _hlR, hrR = _hrR, glR = _glR, grR = _grR });
            if (_snaps.Count > 30) _snaps.RemoveAt(0);
        }

        bool Sample(out Snap s)
        {
            s = default;
            if (_snaps.Count == 0) return false;
            _offset += Time.deltaTime * 0.02f;   // let the clock estimate drift back up if the line got slower
            float rt = Time.time - _offset - Delay;
            int n = _snaps.Count;
            if (rt <= _snaps[0].t) { s = _snaps[0]; return true; }
            if (rt >= _snaps[n - 1].t)
            {
                s = _snaps[n - 1];
                Vector3 a = new Vector3(s.vel.x, 0, s.vel.z) * Mathf.Min(rt - s.t, 0.1f);   // a short hold-over, never far
                s.root += a; s.head += a; s.hl += a; s.hr += a; s.gl += a; s.gr += a;
                return true;
            }
            for (int i = n - 2; i >= 0; i--)
            {
                if (_snaps[i].t > rt) continue;
                var p = _snaps[i]; var q = _snaps[i + 1];
                float k = Mathf.Clamp01((rt - p.t) / Mathf.Max(0.001f, q.t - p.t));
                s.t = rt;
                s.root = Vector3.Lerp(p.root, q.root, k); s.head = Vector3.Lerp(p.head, q.head, k); s.hl = Vector3.Lerp(p.hl, q.hl, k); s.hr = Vector3.Lerp(p.hr, q.hr, k);
                s.gl = Vector3.Lerp(p.gl, q.gl, k); s.gr = Vector3.Lerp(p.gr, q.gr, k); s.vel = Vector3.Lerp(p.vel, q.vel, k);
                s.headR = Quaternion.Slerp(p.headR, q.headR, k); s.hlR = Quaternion.Slerp(p.hlR, q.hlR, k); s.hrR = Quaternion.Slerp(p.hrR, q.hrR, k);
                s.glR = Quaternion.Slerp(p.glR, q.glR, k); s.grR = Quaternion.Slerp(p.grR, q.grR, k);
                if (i > 2) _snaps.RemoveRange(0, i - 2);
                return true;
            }
            s = _snaps[n - 1];
            return true;
        }

        void LateUpdate()
        {
            if (!_hasPose) return;
            Perf.RemoteBegin();
            try { Step(); } finally { Perf.RemoteEnd(); }
        }

        void Step()
        {
            float dt = Time.deltaTime, k = 1f - Mathf.Exp(-45f * dt);
            Vector3 ahead = Vector3.zero;
            Vector3 tRoot = _root, tHead = _head, tHl = _hl, tHr = _hr; Quaternion tHeadR = _headR, tHlR = _hlR, tHrR = _hrR;
            if (Sample(out var sn))
            {
                tRoot = sn.root; tHead = sn.head; tHl = sn.hl; tHr = sn.hr; tHeadR = sn.headR; tHlR = sn.hlR; tHrR = sn.hrR;
                _gl = sn.gl; _gr = sn.gr; _glR = sn.glR; _grR = sn.grR;
            }
            if ((_sRoot - tRoot).sqrMagnitude > 9f) k = 1f;   // a real jump (respawn, fell through): go there at once
            _sRoot = Vector3.Lerp(_sRoot, tRoot + ahead, k); _sHead = Vector3.Lerp(_sHead, tHead + ahead, k);
            _sHl = Vector3.Lerp(_sHl, tHl + ahead, k); _sHr = Vector3.Lerp(_sHr, tHr + ahead, k);
            _sHeadR = Quaternion.Slerp(_sHeadR, tHeadR, k); _sHlR = Quaternion.Slerp(_sHlR, tHlR, k); _sHrR = Quaternion.Slerp(_sHrR, tHrR, k);
            if (_dead) return;
            Footsteps.Track(ref _stepLast, ref _stepDist, ref _stepLeft, _sRoot, _sHeadR * Vector3.forward, dt, 0.65f, 1.8f, 55f);   // other players' footsteps: clearly audible walking, loud running (~50 m)
            // raiders hear other players' footsteps too (unless they're crouched)
            _noiseT -= dt;
            float spd = new Vector2(_vel.x, _vel.z).magnitude;
            if (_noiseT <= 0f && spd > 0.6f && _sHead.y - _sRoot.y > 1.25f) { _noiseT = 0.5f; Noise.Emit(_sRoot, spd > 3.4f ? 16f : 8f, (int)NetId + 1); }
            float hy = _sHeadR.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, hy)) > 35f || _vel.sqrMagnitude > 0.3f) _yaw = Mathf.MoveTowardsAngle(_yaw, hy, dt * 220f);
            // what they hold sits exactly where it is for them; their hands go on its grips (like the raiders' hands)
            _sGl = Vector3.Lerp(_sGl, _gl + ahead, k); _sGr = Vector3.Lerp(_sGr, _gr + ahead, k);
            _sGlR = Quaternion.Slerp(_sGlR, _glR, k); _sGrR = Quaternion.Slerp(_sGrR, _grR, k);
            Vector3 hlP = _sHl, hrP = _sHr; Quaternion hlQ = _sHlR, hrQ = _sHrR;
            if (_itemR != null)
            {
                if (_poseR) _itemR.transform.SetPositionAndRotation(_sGr, _sGrR); else _itemR.transform.SetPositionAndRotation(_sHr, _sHrR);
                if (_itemR is Gun) { hrP = _itemR.grip != null ? _itemR.grip.position : _itemR.transform.position; hrQ = _itemR.transform.rotation; }
                if (_supL && _itemR.secondGrip != null) { hlP = _itemR.secondGrip.position; hlQ = _itemR.transform.rotation * Quaternion.Euler(0, 0, 70); }
            }
            if (_itemL != null)
            {
                if (_poseL) _itemL.transform.SetPositionAndRotation(_sGl, _sGlR); else _itemL.transform.SetPositionAndRotation(_sHl, _sHlR);
                if (_itemL is Gun) { hlP = _itemL.grip != null ? _itemL.grip.position : _itemL.transform.position; hlQ = _itemL.transform.rotation; }
                if (_supR && _itemL.secondGrip != null) { hrP = _itemL.secondGrip.position; hrQ = _itemL.transform.rotation * Quaternion.Euler(0, 0, -70); }
            }
            _rig.Pose(_sRoot, _yaw, _sHead, _sHeadR, hlP, hlQ, hrP, hrQ, _vel, dt);
            if (_voice != null) _voice.transform.position = _sHead;
            var cam = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head : null;
            if (cam != null)
            {
                _tag.transform.position = _sHead + Vector3.up * 0.35f;
                bool mate = NetHub.Backend != null && NetHub.Backend.IsTeammate(NetId);
                if (_dot.activeSelf != mate) _dot.SetActive(mate);
                if (mate)
                {
                    float dist = Vector3.Distance(cam.position, _sHead);
                    _dot.transform.position = _sHead + Vector3.up * (0.5f + dist * 0.004f);
                    _dot.transform.localScale = Vector3.one * Mathf.Clamp(dist * 0.012f, 0.06f, 1.2f);
                }
                // no name text over players (teammates keep the green dot) - it used to be rebuilt every frame while hidden
                if (_tag.gameObject.activeSelf) _tag.gameObject.SetActive(false);
                // their name sewn on the chest instead (rebuilt if the body is rebuilt)
                if (_tape == null) { _tape = HumanRig.NameTape(_rig, DisplayName); _tapeName = DisplayName; }
                else if (_tapeName != DisplayName) { HumanRig.SetTape(_tape, DisplayName); _tapeName = DisplayName; }
            }
        }
    }
}
