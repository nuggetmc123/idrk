using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A hinged door in a raid doorway. Nothing to grab or press: walk into it, or push it with a hand, and it swings
    /// away from you and stays where you leave it. Raiders and other players push it the same way. Doors are left out
    /// of the navmesh bake, so raiders path straight through doorways and shove the door open as they come.
    /// Local space: origin at the hinge, the leaf runs along +x when closed, the wall's two sides are +z / -z.
    /// </summary>
    public class SwingDoor : MonoBehaviour
    {
        public static readonly List<SwingDoor> All = new List<SwingDoor>();
        const float MaxAngle = 100f;

        Transform _leaf;
        Collider _col;
        static readonly Collider[] _ov = new Collider[8];
        float _w, _h, _angle, _vel, _creakT;
        Vector3 _center;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <param name="hinge">bottom of the hinge edge, on the wall's centre line</param>
        /// <param name="along">direction from the hinge across the doorway</param>
        public static SwingDoor Create(Transform parent, Vector3 hinge, Vector3 along, float width, float height, int seed)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(hinge, Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up));
            var d = go.AddComponent<SwingDoor>();
            d.Build(width, height, seed);
            return d;
        }

        void Build(float w, float h, int seed)
        {
            _w = w; _h = h;
            var rng = new Rng(seed);
            bool wood = rng.Chance(0.45f);
            Color body = wood ? UI.Lin(new Color(0.45f, 0.33f, 0.22f)) : UI.Lin(rng.Pick(new[] { new Color(0.42f, 0.45f, 0.43f), new Color(0.32f, 0.38f, 0.34f), new Color(0.5f, 0.5f, 0.48f) }));
            _leaf = new GameObject("Leaf").transform;
            _leaf.SetParent(transform, false);
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(w * 0.5f, h * 0.5f, 0), new Vector3(w - 0.02f, h - 0.02f, 0.045f), body, 0.006f, 2);
            // panels / kick plate so it reads as a door from both sides
            foreach (float z in new[] { -0.024f, 0.024f })
            {
                mb.Sub(0).Box(new Vector3(w * 0.5f, h * 0.68f, z), new Vector3(w * 0.7f, h * 0.38f, 0.006f), body * 0.85f, 0.003f);
                mb.Sub(0).Box(new Vector3(w * 0.5f, h * 0.25f, z), new Vector3(w * 0.7f, h * 0.28f, 0.006f), body * 0.85f, 0.003f);
                mb.Sub(1).Box(new Vector3(w - 0.12f, 1.0f, z * 2.2f), new Vector3(0.12f, 0.025f, 0.03f), new Color(0.6f, 0.6f, 0.58f), 0.006f);   // lever handle
            }
            foreach (float y in new[] { 0.25f, h - 0.3f }) mb.Sub(1).Box(new Vector3(0.015f, y, 0), new Vector3(0.03f, 0.12f, 0.06f), new Color(0.3f, 0.3f, 0.3f), 0.004f);   // hinges
            var leafGo = mb.Build("DoorLeaf", _leaf, wood ? Mats.Lit("door_wood", Color.white, 0f, 0.3f, Mats.Wood, Mats.WoodN, 0.45f, 0.2f, true, 1f)
                                                           : Mats.Lit("door_steel", Color.white, 0.6f, 0.45f, Mats.Metal, Mats.MetalN, 0.4f, 0.4f),
                                   Mats.Lit("door_hw", Color.white, 0.9f, 0.6f, Mats.Metal, null, 0.2f, 0.2f));
            var col = leafGo.AddComponent<BoxCollider>(); _col = col; col.center = new Vector3(w * 0.5f, h * 0.5f, 0); col.size = new Vector3(w - 0.02f, h - 0.02f, 0.05f);
            var rb = leafGo.AddComponent<Rigidbody>(); rb.isKinematic = true;   // a moving collider: the player capsule and hands are pushed cleanly
            leafGo.AddComponent<SurfaceTag>().surface = wood ? Synth.Surface.Wood : Synth.Surface.Metal;
            // some doors are already open when the raid starts (same on every player's game: it comes from the map seed)
            if (rng.Chance(0.35f)) _angle = rng.Range(55f, 95f) * (rng.Chance(0.5f) ? 1 : -1);
            Apply();
        }

        /// <summary>Would the leaf at this angle overlap a wall, furniture or another door?</summary>
        bool Fits(float angle)
        {
            var rot = transform.rotation * Quaternion.Euler(0, angle, 0);
            var c = transform.position + rot * new Vector3(_w * 0.5f + 0.01f, _h * 0.5f + 0.06f, 0);
            var half = new Vector3(_w * 0.5f - 0.05f, _h * 0.5f - 0.14f, 0.02f);   // clear of the floor and the lintel
            int n = Physics.OverlapBoxNonAlloc(c, half, _ov, rot, Ballistics.WallMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (_ov[i] != _col) return false;
            return true;
        }

        void Start()
        {
            Physics.SyncTransforms();
            // stand on the real floor in the doorway (house floors are a slab above the wall's base)
            var hinge = transform.position;
            if (Physics.Raycast(hinge + transform.rotation * new Vector3(_w * 0.5f, 0, 0.15f) + Vector3.up * 0.6f, Vector3.down, out var fh, 1.0f, Ballistics.WallMask, QueryTriggerInteraction.Ignore) && fh.collider != _col)
            {
                float lift = Mathf.Clamp(fh.point.y - hinge.y + 0.01f, 0f, 0.3f);
                if (lift > 0.005f)
                {
                    transform.position = hinge + Vector3.up * lift;
                    float k = (_h - lift) / _h;
                    _leaf.localScale = new Vector3(1f, k, 1f); _h -= lift;
                    Physics.SyncTransforms();
                }
            }
            // a door that starts open but would stand in a wall or another door: close it until it fits
            if (_angle == 0f) { Apply(); return; }
            float a = _angle;
            while (Mathf.Abs(a) > 0.1f && !Fits(a)) a = Mathf.MoveTowards(a, 0f, 5f);
            _angle = Mathf.Abs(a) <= 0.1f ? 0f : a;
            Apply();
        }

        void Apply()
        {
            _leaf.localRotation = Quaternion.Euler(0, _angle, 0);
            _center = transform.TransformPoint(Quaternion.Euler(0, _angle, 0) * new Vector3(_w * 0.5f, 1f, 0));
        }

        // ---- who can push doors this frame (shared by every door)
        struct Pusher { public Vector3 p; public float r; }
        static readonly List<Pusher> _pushers = new List<Pusher>();
        static int _frame = -1;

        static void Gather()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _pushers.Clear();
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead)
            {
                var hp = pl.Head.position;   // in VR your body is under your head, wherever you walk in the play space
                _pushers.Add(new Pusher { p = new Vector3(hp.x, pl.transform.position.y + 1.0f, hp.z), r = 0.36f });
                if (pl.HandL != null) _pushers.Add(new Pusher { p = pl.HandL.transform.position, r = 0.08f });
                if (pl.HandR != null) _pushers.Add(new Pusher { p = pl.HandR.transform.position, r = 0.08f });
            }
            if (NpcDirector.I != null)
                foreach (var n in NpcDirector.I.Npcs)
                    if (n != null && n.Alive) _pushers.Add(new Pusher { p = n.transform.position + Vector3.up * 1.0f, r = 0.6f });   // reach a little ahead: their agents don't stop for doors
            if (NetGame.Live)
                foreach (var r in NetGame.I.Remotes.Values)
                    if (r != null && r.Alive && r.Chest != null) _pushers.Add(new Pusher { p = r.Chest.position, r = 0.4f });
        }

        void Update()
        {
            Gather();
            float dt = Time.deltaTime;
            float push = 0f;
            foreach (var u in _pushers)
            {
                if ((u.p - _center).sqrMagnitude > 4f) continue;
                Vector3 l = transform.InverseTransformPoint(u.p);
                if (l.y < -0.6f || l.y > _h + 0.3f) continue;
                float a = _angle * Mathf.Deg2Rad;
                // leaf direction and normal in hinge space (rotation about +y by angle)
                var dir = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
                var nrm = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                float along = Vector3.Dot(l, dir);
                if (along < 0.05f || along > _w + 0.15f) continue;
                float dist = Vector3.Dot(l, nrm);
                float need = u.r + 0.05f - Mathf.Abs(dist);
                if (need <= 0f) continue;
                // swing the leaf away from whoever is touching it: just enough to clear them
                float da = need / Mathf.Max(0.25f, along) * Mathf.Rad2Deg * (dist >= 0 ? 1f : -1f);
                if (Mathf.Abs(da) > Mathf.Abs(push)) push = da;
            }
            if (push != 0f)
            {
                push = Mathf.Clamp(push, -480f * dt, 480f * dt);
                _vel = Mathf.Lerp(_vel, push / Mathf.Max(dt, 0.001f), 0.5f);
                if (!Fits(_angle + push)) { _vel = 0f; return; }   // blocked by a wall or another door: doors never pass through things
                _angle += push;
                if (Time.time > _creakT && Mathf.Abs(_vel) > 60f) { _creakT = Time.time + 1.2f; Sfx.Play(Synth.SlideBack(), _center, 0.12f, 0.5f, 1f, 12f); }
            }
            else if (Mathf.Abs(_vel) > 1f)
            {
                if (!Fits(_angle + _vel * dt)) { _vel = 0f; return; }
                _angle += _vel * dt;          // a shove keeps it swinging a little
                _vel = Mathf.MoveTowards(_vel, 0, dt * 600f);
            }
            else return;
            if (Mathf.Abs(_angle) > MaxAngle) { _angle = Mathf.Sign(_angle) * MaxAngle; _vel = -_vel * 0.2f; }
            Apply();
        }
    }
}
