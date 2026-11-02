using UnityEngine;

namespace Rago
{
    /// <summary>Another player: interpolated ragdoll body with IK hands, held weapon models, name tag and hit zones.</summary>
    public class RemotePlayer : MonoBehaviour, IDamageable
    {
        public ulong NetId;
        public string DisplayName = "Operator";
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
        bool _dead, _hasPose;
        float _yaw;
        public bool Alive => !_dead;
        public int ActorId => (int)NetId + 1;
        public Transform Chest => _rig != null ? _rig.chest : transform;

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
            rp._tag = UI.Text(go.transform, "", Vector3.zero, 0.05f, new Color(0.7f, 0.9f, 1f));
            // teammate marker: a green dot over their head, seen through walls and kept a readable size at any distance
            rp._dot = new MB().Sphere(Vector3.zero, Vector3.one * 0.5f, 10, Color.white).Build("TeamDot", go.transform, Mats.Unlit("team_dot", new Color(0.2f, 1f, 0.35f, 0.95f), null, Mats.BlendKind.Alpha, false, 50, true));
            rp._dot.SetActive(false);
            return rp;
        }

        public void ApplyPose(bool dead, Vector3 root, Vector3 head, Quaternion headR, Vector3 hl, Quaternion hlR, Vector3 hr, Quaternion hrR, Vector3 vel, string heldL, string heldR)
        {
            if (dead && !_dead) Die();
            if (!dead && _dead) Revive();
            _root = root; _head = head; _headR = headR; _hl = hl; _hlR = hlR; _hr = hr; _hrR = hrR; _vel = vel;
            if (!_hasPose) { _hasPose = true; _sRoot = root; _sHead = head; _sHl = hl; _sHr = hr; _sHeadR = headR; _sHlR = hlR; _sHrR = hrR; }
            if (heldL != _heldL) { _heldL = heldL; SetHeld(true, heldL); }
            if (heldR != _heldR) { _heldR = heldR; SetHeld(false, heldR); }
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
            ArmorBuilder.Dress(_rig, Db.Get<ArmorDef>(P(0)), Db.Get<ArmorDef>(P(1)), Db.Get<BackpackDef>(P(2)), false);
        }

        void SetHeld(bool left, string id)
        {
            ref GameObject slot = ref left ? ref _modelL : ref _modelR;
            if (slot != null) Destroy(slot);
            slot = null;
            if (!left) _gunR = null;
            var def = Db.Get(id);
            if (def == null) return;
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

        public void PlayVoice(byte[] frame)
        {
            if (_voice == null) _voice = VoicePlayback.Create(transform);
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
            var g = _gear; _gear = null; ApplyGear(g);
        }

        void LateUpdate()
        {
            if (!_hasPose) return;
            float dt = Time.deltaTime, k = 1f - Mathf.Exp(-14f * dt);
            _sRoot = Vector3.Lerp(_sRoot, _root, k); _sHead = Vector3.Lerp(_sHead, _head, k);
            _sHl = Vector3.Lerp(_sHl, _hl, k); _sHr = Vector3.Lerp(_sHr, _hr, k);
            _sHeadR = Quaternion.Slerp(_sHeadR, _headR, k); _sHlR = Quaternion.Slerp(_sHlR, _hlR, k); _sHrR = Quaternion.Slerp(_sHrR, _hrR, k);
            if (_dead) return;
            Footsteps.Track(ref _stepLast, ref _stepDist, ref _stepLeft, _sRoot, _sHeadR * Vector3.forward, dt, 0.4f);
            float hy = _sHeadR.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, hy)) > 35f || _vel.sqrMagnitude > 0.3f) _yaw = Mathf.MoveTowardsAngle(_yaw, hy, dt * 220f);
            _rig.Pose(_sRoot, _yaw, _sHead, _sHeadR, _sHl, _sHlR, _sHr, _sHrR, _vel, dt);
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
                _tag.transform.rotation = Quaternion.LookRotation(_tag.transform.position - cam.position);
                bool talking = Time.time - LastVoiceTime < 0.35f;
                _tag.text = (talking ? "<color=#7fff7f>(( </color>" : "") + DisplayName + (talking ? "<color=#7fff7f> ))</color>" : "");
                float d = Vector3.Distance(cam.position, _sHead);
                _tag.gameObject.SetActive(d < 25f);
            }
        }
    }
}
