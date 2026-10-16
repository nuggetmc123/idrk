using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Moderator Spawn Menu (Mod Menu > Spawn Menu). A board in front of you: tap anything and it appears a few
    /// metres ahead of you, on any map (the hub too). Top rows are physics things people can push around
    /// (karts, balls, crates...); the rest are the build pieces (ramps, roads, trees, walls...). Everyone sees them.
    /// </summary>
    public class SpawnMenu : MonoBehaviour
    {
        const int MaxProps = 25;
        static SpawnMenu instance;
        static readonly List<NetworkObjectRef> myProps = new List<NetworkObjectRef>();
        static readonly List<bool> history = new List<bool>(); // true = a prop, false = a build piece (for Undo)

        struct NetworkObjectRef { public Fusion.NetworkObject obj; }

        public static bool Open => instance != null;

        public static void Toggle()
        {
            if (instance != null) { Close(); return; }
            if (!Moderation.IAmMod) return;
            var go = new GameObject("SpawnMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SpawnMenu>();
        }

        public static void Close()
        {
            if (instance == null) return;
            var menu = instance;
            instance = null;
            Destroy(menu.gameObject);
        }

        Transform panel;

        void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
            if (instance == this) instance = null;
        }

        void Start()
        {
            panel = new GameObject("SpawnPanel").transform;
            var rig = KartRig.Instance;
            if (rig != null && rig.head != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                panel.SetParent(rig.transform, true);
                // A little to the left, so it doesn't sit on top of the mod menu.
                panel.position = rig.head.position + fwd * 1.15f - side * 0.25f - Vector3.up * 0.15f;
                panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            panel.localScale = Vector3.one * 0.85f;

            const int columns = 6;
            int total = SpawnedProp.Names.Length + BuildCatalog.Count;
            int rows = (total + columns - 1) / columns;
            float w = 1.32f, h = 0.2f + rows * 0.105f + 0.12f;
            KartUI.Box(panel, "Backing", new Vector3(0f, 0f, 0.02f), new Vector3(w, h, 0.02f), new Color(0.05f, 0.08f, 0.1f));
            KartUI.Part(panel, PrimitiveType.Cube, new Vector3(0f, h * 0.5f, 0.015f), new Vector3(w + 0.02f, 0.015f, 0.025f), KartUI.Glow(new Color(0.4f, 1f, 0.6f)));
            float top = h * 0.5f;
            KartUI.Label(panel, "<b>SPAWN MENU</b>", new Vector3(-0.25f, top - 0.06f, 0f), 0.55f, new Color(0.5f, 1f, 0.65f), new Vector2(0.8f, 0.08f));
            PokeButton.Create(panel, "Close", "Close", new Vector3(0.54f, top - 0.06f, 0f), new Vector3(0.18f, 0.065f, 0.03f), new Color(0.6f, 0.6f, 0.7f), Close, 0.28f).cooldown = 0.8f;

            var size = new Vector3(0.2f, 0.09f, 0.03f);
            for (int i = 0; i < total; i++)
            {
                int index = i;
                bool prop = i < SpawnedProp.Names.Length;
                string label = prop ? SpawnedProp.Names[i] : BuildCatalog.Names[i - SpawnedProp.Names.Length];
                var pos = new Vector3((i % columns - (columns - 1) * 0.5f) * 0.212f, top - 0.16f - (i / columns) * 0.105f, 0f);
                PokeButton.Create(panel, "Spawn" + i, label, pos, size, prop ? new Color(1f, 0.75f, 0.35f) : new Color(0.5f, 0.85f, 1f),
                    () => Spawn(index), 0.2f).cooldown = 0.4f;
            }
            float bottom = -h * 0.5f + 0.06f;
            PokeButton.Create(panel, "Undo", "Undo last", new Vector3(-0.3f, bottom, 0f), new Vector3(0.36f, 0.07f, 0.03f), new Color(1f, 0.85f, 0.4f), Undo, 0.26f).cooldown = 0.4f;
            PokeButton.Create(panel, "Clear", "Remove all mine", new Vector3(0.3f, bottom, 0f), new Vector3(0.36f, 0.07f, 0.03f), new Color(1f, 0.5f, 0.45f), ClearAll, 0.26f).cooldown = 1f;
        }

        void Update()
        {
            if (!Moderation.IAmMod) Close();
        }

        // ---------------- spawning

        static void Spawn(int index)
        {
            var sessions = SessionManager.Instance;
            var rig = KartRig.Instance;
            if (sessions == null || !sessions.IsConnected || rig == null) return;
            bool prop = index < SpawnedProp.Names.Length;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 spot = Ground(rig.head.position + fwd * (prop ? 3.5f : 7f));
            Quaternion facing = Quaternion.LookRotation(-fwd); // pieces face you

            if (prop)
            {
                var prefab = sessions.propPrefab;
                if (prefab == null) { sessions.SetStatus("Spawning needs the newest world: KartVR > Build Prototype World", 6f); return; }
                myProps.RemoveAll(p => p.obj == null);
                if (myProps.Count >= MaxProps)
                {
                    sessions.Runner.Despawn(myProps[0].obj); // too many: the oldest goes
                    myProps.RemoveAt(0);
                }
                var obj = sessions.Runner.Spawn(prefab, spot + Vector3.up * 1.2f, facing, sessions.Runner.LocalPlayer);
                var p = obj != null ? obj.GetComponent<SpawnedProp>() : null;
                if (p == null) return;
                p.KindIndex = (byte)index;
                p.Seed = new KartLook { paint = Random.Range(0, PlayerProfile.Palette.Length), accent = Random.Range(0, PlayerProfile.Palette.Length) }.Pack();
                myProps.Add(new NetworkObjectRef { obj = obj });
                history.Add(true);
            }
            else
            {
                var build = BuildManager.Instance;
                if (build == null || !build.IsReady) return;
                build.Place(index - SpawnedProp.Names.Length, spot, facing.eulerAngles.y);
                history.Add(false);
            }
            AudioManager.Play(AudioManager.Sfx.Place);
        }

        static Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 15f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = new Vector3(p.x, p.y - 1.5f, p.z);
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<KartController>() != null || h.collider.GetComponentInParent<PlayerAvatar>() != null) continue;
                if (h.distance < best) { best = h.distance; ground = h.point; }
            }
            return ground;
        }

        static void Undo()
        {
            var sessions = SessionManager.Instance;
            if (history.Count == 0 || sessions == null || !sessions.IsConnected) return;
            bool prop = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            if (prop)
            {
                myProps.RemoveAll(p => p.obj == null);
                if (myProps.Count == 0) return;
                sessions.Runner.Despawn(myProps[myProps.Count - 1].obj);
                myProps.RemoveAt(myProps.Count - 1);
            }
            else BuildManager.Instance?.UndoMine();
            AudioManager.Play(AudioManager.Sfx.Delete);
        }

        static void ClearAll()
        {
            var sessions = SessionManager.Instance;
            if (sessions == null || !sessions.IsConnected) return;
            foreach (var p in myProps) if (p.obj != null) sessions.Runner.Despawn(p.obj);
            myProps.Clear();
            BuildManager.Instance?.RemoveAllMine();
            history.Clear();
            AudioManager.Play(AudioManager.Sfx.Delete);
        }
    }
}
