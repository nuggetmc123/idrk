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
}
