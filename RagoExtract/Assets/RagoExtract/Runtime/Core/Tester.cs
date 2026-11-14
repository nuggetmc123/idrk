using UnityEngine;

namespace Rago
{
    /// <summary>Tester tools (menu > TESTER): spawn items and raiders, be invisible to raiders. God mode is Vitals.God.</summary>
    public static class Tester
    {
        static bool _invisible;
        /// <summary>Raiders don't see, target or hunt you. Only the AI: other players still see you normally.</summary>
        public static bool Invisible
        {
            get => _invisible;
            set { _invisible = value; NetGame.I?.SendHidden(value); }
        }

        static Vector3 InFront(float dist, out Quaternion rot)
        {
            var p = RagoGame.I.Player;
            Vector3 f = p.Head.forward; f.y = 0; f = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            rot = Quaternion.LookRotation(f, Vector3.up);
            return p.Head.position + f * dist + Vector3.down * 0.35f;
        }

        /// <summary>Menu SPAWN: point where it should go - a see-through preview follows your pointer - and pull the trigger.</summary>
        public static void BeginPlace(ItemDef d, int npcKind = -1)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (d == null && npcKind >= 0)
            {
                if (NpcDirector.I == null || RagoGame.I.Area != Area.Raid) { pl.Toast("NPCs can only be spawned in a raid"); return; }
                if (NetGame.Live && !NetGame.IsHost) { pl.Toast("Only the host can spawn NPCs"); return; }
            }
            SpawnPlacer.Begin(d, npcKind);
        }

        public static void SpawnItem(ItemDef d)
        {
            if (d == null || RagoGame.I == null || RagoGame.I.Player == null) return;
            var pos = InFront(0.6f, out var rot);
            var it = ItemFactory.Spawn(new ItemData(d.id), pos, rot);
            if (it == null) return;
            it.transform.SetParent(RagoGame.I.MapRoot, true);
            if (it.rb != null) { it.rb.isKinematic = false; it.SetCollidersTrigger(false); }
            RagoGame.I.Player.Toast("Spawned " + d.name);
        }

        public static void SpawnNpcAt(int kind, Vector3 pos, float yaw)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null || NpcDirector.I == null) return;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 3f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
            string boss = kind >= 2 ? new[] { "Doctor Kessler", "The Harbormaster", "The Warden", "The Banker" }[kind - 2] : null;
            NpcDirector.I.SpawnNow(pos, yaw, kind >= 1, boss);
            pl.Toast("Spawned " + (boss ?? (kind == 1 ? "a heavy raider" : "a raider")));
        }

        public static void SpawnNpc(int kind)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (NpcDirector.I == null || RagoGame.I.Area != Area.Raid) { pl.Toast("NPCs can only be spawned in a raid"); return; }
            if (NetGame.Live && !NetGame.IsHost) { pl.Toast("Only the host can spawn NPCs"); return; }
            var pos = InFront(5f, out var rot);
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
            string boss = kind >= 2 ? new[] { "Doctor Kessler", "The Harbormaster", "The Warden", "The Banker" }[kind - 2] : null;
            NpcDirector.I.SpawnNow(pos, rot.eulerAngles.y + 180f, kind >= 1, boss);
            pl.Toast("Spawned " + (boss ?? (kind == 1 ? "a heavy raider" : "a raider")));
        }
    }

    /// <summary>
    /// The tester spawn pointer: a beam from your free hand (or the middle of the screen on desktop) with a see-through
    /// preview of the item - or a marker for an NPC - where it points. Trigger / click spawns it there; grip or B/Y cancels.
    /// </summary>
    public class SpawnPlacer : MonoBehaviour
    {
        static SpawnPlacer _i;
        ItemDef _def; int _npc = -1;
        Item _ghost; GameObject _marker;
        LineRenderer _beam;
        Vector3 _pos; float _yaw; bool _valid;
        float _armT;

        public static void Begin(ItemDef d, int npcKind)
        {
            if (_i == null) { var go = new GameObject("SpawnPlacer"); _i = go.AddComponent<SpawnPlacer>(); }
            _i.Clear();
            _i._def = d; _i._npc = d == null ? npcKind : -1;
            _i._armT = Time.time + 0.25f;   // the trigger that pressed SPAWN doesn't place it straight away
            if (d != null)
            {
                _i._ghost = ItemFactory.Spawn(new ItemData(d.id), Vector3.down * 500f, Quaternion.identity);
                if (_i._ghost != null)
                {
                    _i._ghost.transform.SetParent(_i.transform, true);   // not part of the map: never saved or swept up
                    if (_i._ghost.rb != null) _i._ghost.rb.isKinematic = true;
                    foreach (var c in _i._ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
                    foreach (var r in _i._ghost.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            else
            {
                _i._marker = new MB().Cyl(Vector3.zero, Vector3.up * 1.8f, 0.3f, 0.3f, 16, Color.white).Build("NpcMarker", _i.transform,
                    Mats.Unlit("spawn_marker", new Color(1f, 0.3f, 0.2f, 0.35f), null, Mats.BlendKind.Alpha));
            }
            RagoGame.I?.Player?.Toast(XRInputs.VR ? "Point and pull the TRIGGER to spawn - GRIP or B/Y cancels" : "Look where it goes and CLICK - right click cancels");
        }

        void Clear()
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            if (_marker != null) Destroy(_marker);
            _ghost = null; _marker = null; _def = null; _npc = -1;
            if (_beam != null) _beam.enabled = false;
        }

        bool Active => _ghost != null || _marker != null;

        void Update()
        {
            if (!Active) return;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null || pl.Dead) { Clear(); return; }
            // the pointing hand: the free one (right first)
            Hand h = pl.HandR.Held == null ? pl.HandR : pl.HandL.Held == null ? pl.HandL : pl.HandR;
            Vector3 o, dir;
            if (XRInputs.VR) { dir = h.transform.rotation * h.AimLocal * Vector3.forward; o = h.Anchor.position + dir * 0.05f; }
            else { o = pl.Head.position; dir = pl.Head.forward; }
            if (_beam == null)
            {
                _beam = new GameObject("Beam").AddComponent<LineRenderer>();
                _beam.transform.SetParent(transform, false);
                _beam.positionCount = 2; _beam.widthMultiplier = 0.006f; _beam.useWorldSpace = true;
                _beam.sharedMaterial = Mats.Unlit("spawn_beam", new Color(0.4f, 1f, 0.5f, 0.6f), null, Mats.BlendKind.Additive);
                _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _valid = Physics.Raycast(o, dir, out var hit, 40f, Ballistics.WallMask, QueryTriggerInteraction.Ignore);
            Vector3 end = _valid ? hit.point : o + dir * 6f;
            _beam.enabled = XRInputs.VR; _beam.SetPosition(0, o); _beam.SetPosition(1, end);
            Vector3 flat = pl.Head.position - end; flat.y = 0;
            _yaw = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized).eulerAngles.y : 0f;
            _pos = end;
            if (_valid && hit.normal.y < 0.5f) _pos += hit.normal * 0.25f;   // pointing at a wall: just in front of it
            if (_ghost != null)
            {
                var b = _ghost.localBounds;
                var rot = Quaternion.Euler(0, _yaw + 90f, 0);
                _ghost.transform.rotation = rot;
                Vector3 lowest = rot * new Vector3(b.center.x, b.min.y, b.center.z);
                _ghost.transform.position = _pos - lowest + Vector3.up * 0.02f;
            }
            if (_marker != null) _marker.transform.position = _pos;
            // confirm / cancel
            var inp = XRInputs.VR ? (h == pl.HandR ? XRInputs.Right : XRInputs.Left) : default;
            bool place = Time.time > _armT && (XRInputs.VR ? inp.TriggerDown : Desktop.Click);
            bool cancel = XRInputs.VR ? inp.GripDown || inp.SecondaryDown : Input.GetMouseButtonDown(1);
            if (cancel) { Clear(); pl.Toast("Spawn cancelled"); return; }
            if (!place || !_valid) return;
            if (_ghost != null)
            {
                var it = _ghost; _ghost = null;
                it.transform.SetParent(RagoGame.I.MapRoot, true);
                foreach (var c in it.GetComponentsInChildren<Collider>()) c.enabled = true;
                it.Drop(Vector3.zero, Vector3.zero);
                pl.Toast("Spawned " + (it.def != null ? it.def.name : "item"));
            }
            else Tester.SpawnNpcAt(_npc, _pos, _yaw);
            Clear();
        }
    }
}
