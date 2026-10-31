using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>A place an item can be stored: holsters, back slots, backpack cells, crate cells, stash shelves, trader trays.</summary>
    public class Slot : MonoBehaviour
    {
        public Item item;
        public System.Func<Item, bool> filter;
        public float radius = 0.12f;
        public float fit = 0f;            // max dimension of a stored item (0 = keep full size)
        public Quaternion storeRot = Quaternion.identity;
        public System.Action<Slot> onChanged;
        public bool showGhost = true;
        public static readonly List<Slot> Active = new List<Slot>();
        GameObject _ghost;
        float _ghostT;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public bool Accepts(Item it) => item == null && it != null && (filter == null || filter(it));

        public static Slot FindFor(Item it, Vector3 pos)
        {
            Slot best = null; float bd = 1e9f;
            foreach (var s in Active)
            {
                if (!s.isActiveAndEnabled || !s.Accepts(it)) continue;
                float d = Vector3.Distance(s.transform.position, pos);
                if (d < s.radius && d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public void Store(Item it, bool sound = true)
        {
            item = it;
            it.slot = this;
            it.rb.isKinematic = true;
            it.SetCollidersTrigger(true);
            it.transform.SetParent(transform, false);
            float scale = 1f;
            var b = it.localBounds;
            if (fit > 0f)
            {
                float m = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (m > fit) scale = fit / m;
            }
            it.transform.localScale = Vector3.one * scale;
            it.transform.localRotation = storeRot;
            it.transform.localPosition = -(storeRot * b.center) * scale;
            if (sound) Sfx.Play(Synth.Grab(2), transform.position, 0.35f, 0.9f, 0.5f, 6f);
            onChanged?.Invoke(this);
            NetEvents.ItemStored(it);
        }

        public void Release(Item it)
        {
            if (item != it) return;
            item = null;
            it.slot = null;
            it.transform.localScale = Vector3.one;
            it.transform.SetParent(null, true);
            onChanged?.Invoke(this);
        }

        /// <summary>Shows a faint outline when a hand holding a compatible item is close.</summary>
        public void Hint(bool on)
        {
            if (!showGhost) return;
            if (on && _ghost == null)
            {
                _ghost = new MB().Sphere(Vector3.zero, Vector3.one * 0.04f, 10, Color.white).Build("ghost", transform, Mats.Unlit("slot_ghost", new Color(1f, 0.85f, 0.4f, 0.35f), Mats.Glow, Mats.BlendKind.Additive));
            }
            if (_ghost != null) _ghost.SetActive(on);
            _ghostT = on ? 0.2f : 0;
        }

        void Update()
        {
            if (_ghost != null && _ghost.activeSelf)
            {
                _ghostT -= Time.deltaTime;
                if (_ghostT <= 0) _ghost.SetActive(false);
                else _ghost.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 8f) * 0.15f);
            }
        }
    }

    /// <summary>A grid of slots on a surface.</summary>
    public class ItemContainer : MonoBehaviour
    {
        public readonly List<Slot> slots = new List<Slot>();
        public System.Action onChanged;

        public static ItemContainer Grid(Transform parent, Vector3 localPos, Quaternion localRot, int cols, int rows, Vector2 spacing, float fit, System.Func<Item, bool> filter = null, Quaternion? storeRot = null)
        {
            var go = new GameObject("Container");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var c = go.AddComponent<ItemContainer>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var sgo = new GameObject("Slot" + (y * cols + x));
                    sgo.transform.SetParent(go.transform, false);
                    sgo.transform.localPosition = new Vector3((x - (cols - 1) * 0.5f) * spacing.x, 0, (y - (rows - 1) * 0.5f) * spacing.y);
                    var s = sgo.AddComponent<Slot>();
                    s.fit = fit;
                    s.radius = Mathf.Min(spacing.x, spacing.y) * 0.6f;
                    s.filter = filter;
                    s.storeRot = storeRot ?? Quaternion.identity;
                    s.onChanged = _ => c.onChanged?.Invoke();
                    c.slots.Add(s);
                }
            return c;
        }

        public Slot FirstFree(Item it) { foreach (var s in slots) if (s.Accepts(it)) return s; return null; }
        public bool Add(Item it, bool sound = false) { var s = FirstFree(it); if (s == null) return false; s.Store(it, sound); return true; }

        public List<ItemData> Save()
        {
            var l = new List<ItemData>();
            foreach (var s in slots) if (s.item != null) l.Add(s.item.Save());
            return l;
        }

        public void Clear() { foreach (var s in slots) if (s.item != null) { var it = s.item; s.Release(it); Destroy(it.gameObject); } }
        public int TotalValue() { int v = 0; foreach (var s in slots) if (s.item != null) v += s.item.Value; return v; }
    }

    /// <summary>Spawns items from saved data, builds simple prop models for non-gun items.</summary>
    public static class ItemFactory
    {
        public static Item Spawn(ItemData d, Vector3 pos, Quaternion rot)
        {
            if (d == null) return null;
            var def = Db.Get(d.id);
            if (def == null) return null;
            Item it;
            switch (def.kind)
            {
                case ItemKind.Gun: it = Gun.Create((GunDef)def, d, pos, rot); break;
                case ItemKind.Sight: it = SightItem.Create((SightDef)def, pos, rot); break;
                case ItemKind.Med: it = Simple<MedItem>((SimpleDef)def, pos, rot); break;
                default: it = Simple<LootItem>((SimpleDef)def, pos, rot); break;
            }
            if (RagoGame.I != null && RagoGame.I.MapRoot != null) it.transform.SetParent(RagoGame.I.MapRoot, true);
            return it;
        }

        public static Item Spawn(string id, Vector3 pos, Quaternion rot) => Spawn(new ItemData(id), pos, rot);

        static T Simple<T>(SimpleDef d, Vector3 pos, Quaternion rot) where T : Item
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var it = go.AddComponent<T>();
            it.def = d;
            var mb = new MB(3);
            Color c = d.color, w = Color.white;
            Material m0 = Mats.Lit("item_plastic", w, 0f, 0.35f, Mats.Noise, null, 0.3f, 0.1f);
            Material m1 = Mats.Lit("item_metal", w, 0.9f, 0.7f, Mats.Metal, null, 0.1f, 0.2f);
            Material m2 = Mats.Lit("item_cloth", w, 0f, 0.2f, Mats.Fabric, Mats.FabricN, 0.2f, 0f);
            switch (d.kind)
            {
                case ItemKind.Med:
                    if (d.shape == 0) { mb.Sub(2).Cyl(new Vector3(-0.04f, 0, 0), new Vector3(0.04f, 0, 0), 0.03f, 0.03f, 14, c); mb.Sub(0).Box(new Vector3(0, 0.032f, 0), new Vector3(0.05f, 0.006f, 0.02f), new Color(0.8f, 0.1f, 0.1f), 0.002f); }
                    else if (d.shape == 1)
                    {
                        mb.Sub(0).Box(Vector3.zero, new Vector3(0.18f, 0.12f, 0.07f), c, 0.015f, 2);
                        mb.Sub(0).Box(new Vector3(0, 0, 0.036f), new Vector3(0.06f, 0.018f, 0.004f), w, 0.002f);
                        mb.Sub(0).Box(new Vector3(0, 0, 0.036f), new Vector3(0.018f, 0.06f, 0.004f), w, 0.002f);
                        mb.Sub(1).Box(new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.02f, 0.02f), w, 0.005f);
                    }
                    else { mb.Sub(0).Cyl(new Vector3(0, -0.06f, 0), new Vector3(0, 0.05f, 0), 0.014f, 0.014f, 12, c); mb.Sub(1).Cyl(new Vector3(0, 0.05f, 0), new Vector3(0, 0.07f, 0), 0.004f, 0.001f, 8, w); mb.Sub(0).Box(new Vector3(0, -0.065f, 0), new Vector3(0.03f, 0.012f, 0.03f), new Color(0.9f, 0.9f, 0.9f), 0.003f); }
                    break;
                case ItemKind.Ammo:
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.12f, 0.07f, 0.08f), new Color(0.32f, 0.38f, 0.24f), 0.006f, 1);
                    mb.Sub(0).Box(new Vector3(0, 0, 0.041f), new Vector3(0.09f, 0.03f, 0.003f), new Color(0.85f, 0.75f, 0.2f), 0.001f);
                    mb.Sub(1).Box(new Vector3(0, 0.04f, 0), new Vector3(0.05f, 0.012f, 0.015f), new Color(0.3f, 0.3f, 0.3f), 0.004f);
                    break;
                case ItemKind.Key:
                    mb.Sub(0).Box(Vector3.zero, new Vector3(0.085f, 0.054f, 0.004f), c, 0.002f);
                    mb.Sub(1).Box(new Vector3(-0.02f, 0.005f, 0.0025f), new Vector3(0.022f, 0.018f, 0.002f), new Color(1f, 0.8f, 0.3f), 0.001f);
                    mb.Sub(0).Box(new Vector3(0.015f, -0.015f, 0.0025f), new Vector3(0.04f, 0.006f, 0.002f), new Color(0.1f, 0.1f, 0.1f), 0.001f);
                    break;
                default:
                    switch (d.shape)
                    {
                        case 0: for (int i = 0; i < 10; i++) mb.Sub(1).Sphere(new Vector3(Mathf.Cos(i * 0.6f) * 0.03f, Mathf.Sin(i * 0.6f) * 0.03f, 0), Vector3.one * 0.008f, 8, c); break;
                        case 1: mb.Sub(1).Cyl(new Vector3(0, 0, -0.005f), new Vector3(0, 0, 0.007f), 0.02f, 0.02f, 18, c); mb.Sub(0).Box(new Vector3(0, 0, 0.0075f), new Vector3(0.03f, 0.03f, 0.001f), new Color(0.05f, 0.05f, 0.1f)); mb.Sub(2).Box(new Vector3(0, 0.04f, 0), new Vector3(0.018f, 0.04f, 0.006f), new Color(0.15f, 0.12f, 0.1f), 0.002f); mb.Sub(2).Box(new Vector3(0, -0.04f, 0), new Vector3(0.018f, 0.04f, 0.006f), new Color(0.15f, 0.12f, 0.1f), 0.002f); break;
                        case 2: mb.Sub(0).Box(Vector3.zero, new Vector3(0.08f, 0.012f, 0.05f), c, 0.003f); mb.Sub(1).Box(new Vector3(0, 0.007f, 0), new Vector3(0.02f, 0.003f, 0.02f), new Color(0.6f, 0.6f, 0.6f), 0.001f); break;
                        case 3: mb.Sub(2).Box(Vector3.zero, new Vector3(0.24f, 0.012f, 0.31f), c, 0.003f); mb.Sub(0).Box(new Vector3(0, 0.007f, 0.05f), new Vector3(0.12f, 0.002f, 0.03f), new Color(0.75f, 0.1f, 0.08f), 0.001f); break;
                        case 4: mb.Sub(0).Box(Vector3.zero, new Vector3(0.27f, 0.04f, 0.11f), c, 0.004f); mb.Sub(1).Cyl(new Vector3(-0.06f, 0.021f, 0), new Vector3(-0.06f, 0.024f, 0), 0.04f, 0.04f, 16, new Color(0.15f, 0.15f, 0.15f)); mb.Sub(1).Cyl(new Vector3(0.06f, 0.021f, 0), new Vector3(0.06f, 0.024f, 0), 0.04f, 0.04f, 16, new Color(0.15f, 0.15f, 0.15f)); mb.Sub(1).Box(new Vector3(0, -0.012f, -0.05f), new Vector3(0.2f, 0.01f, 0.012f), new Color(0.85f, 0.7f, 0.2f)); break;
                        case 5: mb.Sub(0).Box(Vector3.zero, new Vector3(0.06f, 0.012f, 0.025f), new Color(0.1f, 0.1f, 0.1f), 0.004f); mb.Sub(1).Box(new Vector3(0.0f, 0.007f, 0), new Vector3(0.02f, 0.003f, 0.012f), c, 0.001f); break;
                        case 6: mb.Sub(1).Box(Vector3.zero, new Vector3(0.12f, 0.035f, 0.055f), c, 0.008f, 2); break;
                        default:
                            var prof = new List<Vector2> { new Vector2(0.0f, -0.02f), new Vector2(0.022f, 0.004f), new Vector2(0.016f, 0.012f), new Vector2(0.0f, 0.013f) };
                            mb.Sub(1).Lathe(Vector3.zero, Quaternion.identity, prof, 8, c);
                            break;
                    }
                    break;
            }
            var mats = new[] { m0, d.kind == ItemKind.Valuable && (d.shape == 6 || d.shape == 0) ? Mats.Gold : d.shape == 7 ? Mats.Unlit("diamond", new Color(0.5f, 0.85f, 1f, 0.85f)) : m1, m2 };
            var model = mb.Build("Model", go.transform, mats);
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var col = go.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center;
            col.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
            it.localBounds = mesh.bounds;
            it.SetupPhysics(0.3f);
            return it;
        }
    }
}
