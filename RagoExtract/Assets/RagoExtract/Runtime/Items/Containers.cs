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
        public float reachY = 0f;         // > 0: a tall slot (a rifle rack column): takes the item anywhere this far above/below its point, in line with it
        public float fit = 0f;            // max dimension of a stored item (0 = keep full size)
        public Quaternion storeRot = Quaternion.identity;
        public System.Action<Slot> onChanged;
        public bool showGhost = true;
        public bool restOnBase;           // containers / shelves: the item sits ON the slot point instead of being centred on it
        public static readonly List<Slot> Active = new List<Slot>();
        GameObject _ghost;
        float _ghostT;

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public bool Accepts(Item it) => item == null && it != null && (filter == null || filter(it));

        public static Slot FindFor(Item it, Vector3 pos)
        {
            Slot best = null; float bd = 1e9f;
            // measure from the middle of the item, not its origin (a rifle's origin is at its grip, in your hand)
            Vector3 c = it != null && it.localBounds.size.sqrMagnitude > 0 ? it.transform.TransformPoint(it.localBounds.center) : pos;
            foreach (var s in Active)
            {
                if (!s.isActiveAndEnabled || !s.Accepts(it)) continue;
                Vector3 sp = s.transform.position;
                float d;
                if (s.reachY > 0f)
                {
                    Vector3 dv = c - sp; float vy = Mathf.Abs(dv.y); dv.y = 0;
                    if (vy > s.reachY || dv.magnitude > s.radius) continue;
                    d = dv.magnitude + vy * 0.2f;
                }
                else
                {
                    d = Mathf.Min(Vector3.Distance(sp, pos), Vector3.Distance(sp, c));
                    if (d >= s.radius) continue;
                }
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public void Store(Item it, bool sound = true)
        {
            item = it;
            it.slot = this;
            it.rb.isKinematic = true;
            it.rb.interpolation = RigidbodyInterpolation.None;   // interpolation would keep drawing it where it was: stored items must ride with the slot
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
            if (restOnBase)
            {
                // lift it so its lowest point sits on the container floor (centred, a tall item poked out the bottom)
                var m = Matrix4x4.Rotate(storeRot);
                float extY = Mathf.Abs(m.m10) * b.extents.x + Mathf.Abs(m.m11) * b.extents.y + Mathf.Abs(m.m12) * b.extents.z;
                it.transform.localPosition += Vector3.up * (extY * scale + 0.004f);
            }
            if (sound) Sfx.Play(Synth.Grab(2), transform.position, 0.35f, 0.9f, 0.5f, 6f);
            onChanged?.Invoke(this);
            NetEvents.ItemStored(it);
        }

        public void Release(Item it)
        {
            if (item != it) return;
            item = null;
            it.slot = null;
            if (it.rb != null) it.rb.interpolation = RigidbodyInterpolation.Interpolate;
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
                    s.restOnBase = true;
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
                case ItemKind.Attachment: it = AttachItem.Create((AttachDef)def, pos, rot); break;
                case ItemKind.Med: it = Simple<MedItem>((SimpleDef)def, pos, rot); break;
                case ItemKind.Valuable when def.id == "gift" || def.id == "gift_santa": it = PresentItem.Create((SimpleDef)def, pos, rot); break;
                case ItemKind.Valuable when def.id == "cc_trophy": it = TrophyItem.Create((SimpleDef)def, pos, rot); break;
                case ItemKind.Valuable when def.id == "cc_cam": it = CamItem.Create((SimpleDef)def, pos, rot); break;
                case ItemKind.Valuable when def.id == "nvg": it = NvgItem.Create((SimpleDef)def, pos, rot); break;
                case ItemKind.Throwable: it = Grenade.Create((SimpleDef)def, pos, rot); break;
                case ItemKind.Melee: it = Knife.Create((SimpleDef)def, pos, rot); break;
                case ItemKind.Armor: it = ArmorItem.Create((ArmorDef)def, pos, rot); break;
                case ItemKind.Backpack: { var bp = Backpack.Create(pos, (BackpackDef)def); bp.Load(d); it = bp; break; }
                default: it = def.id == "val_radio" ? Simple<RadioItem>((SimpleDef)def, pos, rot) : Simple<LootItem>((SimpleDef)def, pos, rot); break;
            }
            if (RagoGame.I != null && RagoGame.I.MapRoot != null) it.transform.SetParent(RagoGame.I.MapRoot, true);
            it.Unsink();   // spawned at floor level: start on the floor, not half inside it (physics would push it through)
            return it;
        }

        public static Item Spawn(string id, Vector3 pos, Quaternion rot) => Spawn(new ItemData(id), pos, rot);

        static T Simple<T>(SimpleDef d, Vector3 pos, Quaternion rot) where T : Item
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var it = go.AddComponent<T>();
            it.def = d;
            var mb = ItemModels.Build(d);
            var mats = ItemModels.Materials(d);
            var model = mb.Build("Model", go.transform, mats);
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            // streamer figurines: their name in white on the plate at the front of the base
            var plate = ItemModels.Nameplate(d, out var at);
            if (plate != null)
            {
                var t = UI.Text(model.transform, plate, Vector3.zero, 0.0085f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.transform.localPosition = at;
                t.transform.localRotation = Quaternion.Euler(-6f, 0, 0) * Quaternion.Euler(0, 180f, 0);   // facing out of the front, read from in front
            }
            var labels = ItemModels.Labels(d);
            if (labels != null)
                foreach (var l in labels)
                {
                    bool up = l.text.StartsWith("|");   // written up the side (bottom to top)
                    var t = UI.Text(model.transform, up ? l.text.Substring(1) : l.text, Vector3.zero, l.size, l.col, TextAnchor.MiddleCenter, FontStyle.Bold);
                    t.transform.localPosition = l.at;
                    t.transform.localRotation = Quaternion.Euler(0, 180f, 0) * (up ? Quaternion.Euler(0, 0, -90f) : Quaternion.identity);   // read from in front
                }
            var col = go.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center;
            col.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
            it.localBounds = mesh.bounds;
            it.SetupPhysics(0.3f);
            return it;
        }
    }
}
