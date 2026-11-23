using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A small wrapped CHRISTMAS PRESENT you find inside loot crates (over Christmas, and always on the North Pole).
    /// Pick it up and SHAKE it - a few hard shakes and it bursts open, dropping what's inside right there. Desktop: click.
    /// The better the prize the rarer it is; now and then it's a live grenade. Evil Santa drops a special one with a
    /// North Pole Keycard inside.
    /// </summary>
    public class PresentItem : Item
    {
        Vector3 _lastPos, _lastVel; float _lastFlip = -10f; int _shakes; bool _opened;

        static readonly Color[] Wraps = { new Color(0.75f, 0.08f, 0.08f), new Color(0.1f, 0.42f, 0.18f), new Color(0.18f, 0.28f, 0.7f), new Color(0.85f, 0.68f, 0.18f), new Color(0.82f, 0.82f, 0.86f), new Color(0.5f, 0.12f, 0.55f) };

        public static PresentItem Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var p = go.AddComponent<PresentItem>();
            p.def = d;
            bool santa = d.id == "gift_santa";
            int k = Random.Range(0, Wraps.Length);
            Color wrap = santa ? new Color(0.72f, 0.05f, 0.05f) : Wraps[k], ribbon = santa ? new Color(0.95f, 0.78f, 0.25f) : Wraps[(k + 3) % Wraps.Length];
            Vector3 sz = santa ? new Vector3(0.2f, 0.16f, 0.2f) : new Vector3(0.14f, 0.11f, 0.14f);
            var mb = new MB();
            mb.Box(Vector3.zero, sz, wrap, 0.006f);
            mb.Box(Vector3.zero, new Vector3(sz.x * 0.18f, sz.y + 0.003f, sz.z + 0.003f), ribbon, 0f);
            mb.Box(Vector3.zero, new Vector3(sz.x + 0.003f, sz.y + 0.003f, sz.z * 0.18f), ribbon, 0f);
            mb.Sphere(new Vector3(-sz.x * 0.16f, sz.y * 0.5f + 0.012f, 0), new Vector3(sz.x * 0.2f, sz.y * 0.13f, sz.x * 0.09f), 8, ribbon);
            mb.Sphere(new Vector3(sz.x * 0.16f, sz.y * 0.5f + 0.012f, 0), new Vector3(sz.x * 0.2f, sz.y * 0.13f, sz.x * 0.09f), 8, ribbon);
            mb.Build("Gift", go.transform, Mats.Lit("present_wrap", Color.white, 0.1f, 0.5f, Mats.Noise, null, 0.08f, 0f));
            var col = go.AddComponent<BoxCollider>(); col.size = sz;
            p.localBounds = new Bounds(Vector3.zero, sz + new Vector3(0, 0.03f, 0));
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Wood;
            p.SetupPhysics(santa ? 0.8f : 0.5f);
            return p;
        }

        public override void OnGrab(Hand h) { base.OnGrab(h); _lastPos = transform.position; _lastVel = Vector3.zero; _shakes = 0; }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            if (_opened) return;
            if (!XRInputs.VR) { if (input.TriggerDown) Open(h); return; }
            // shaking: count sharp changes of direction while it's moving fast
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 vel = (transform.position - _lastPos) / dt;
            _lastPos = transform.position;
            if (vel.magnitude > 1.1f && _lastVel.magnitude > 1.1f && Vector3.Dot(vel.normalized, _lastVel.normalized) < -0.2f && Time.time - _lastFlip > 0.08f)
            {
                _shakes = Time.time - _lastFlip < 0.6f ? _shakes + 1 : 1;
                _lastFlip = Time.time;
                Sfx.Play(Synth.CanRattle(), transform.position, 0.35f, 1.6f + Random.Range(-0.1f, 0.1f), 0.5f, 6f);
                h.Haptic(0.35f, 0.04f);
                if (_shakes >= 5) Open(h);
            }
            if (vel.sqrMagnitude > 0.25f) _lastVel = vel;
        }

        void Open(Hand h)
        {
            if (_opened) return;
            _opened = true;
            Vector3 at = transform.position;
            Sfx.Play(Synth.Grab(2), at, 0.8f, 1.5f, 1f, 10f);
            Sfx.UI(Synth.UiClick(), 0.5f);
            h?.Haptic(0.9f, 0.12f);
            bool pole = RagoGame.I != null && RagoGame.I.Map != null && RagoGame.I.Map.Id == "northpole";
            var items = Roll(new Rng(Random.Range(1, int.MaxValue)), pole, def.id == "gift_santa", out bool grenade);
            // the wrapper is gone (for everyone)
            Consumed = true;
            if (netId != 0) NetGame.I?.OnItemTaken(this);
            if (primaryHand != null) primaryHand.ForceRelease(true);
            if (slot != null) slot.Release(this);
            // the prize pops out where it was
            for (int i = 0; i < items.Count; i++)
            {
                var it = ItemFactory.Spawn(items[i], at + Vector3.up * 0.05f + Random.insideUnitSphere * 0.06f, Random.rotation);
                if (it == null) continue;
                if (it.rb != null) { it.rb.isKinematic = false; it.SetCollidersTrigger(false); it.rb.SetVel(Vector3.up * 1.2f + Random.insideUnitSphere * 0.5f); }
                if (NetGame.Live && RagoGame.I != null && RagoGame.I.Area == Area.Raid) NetGame.I.OnItemDropped(it);
            }
            if (grenade && ItemFactory.Spawn(new ItemData("frag"), at + Vector3.up * 0.1f, Quaternion.identity) is Grenade gr)
            {
                gr.ArmNow(2.5f);
                if (gr.rb != null) { gr.rb.isKinematic = false; gr.SetCollidersTrigger(false); gr.rb.SetVel(Vector3.up * 3f + Random.insideUnitSphere * 0.6f); gr.rb.angularVelocity = Random.insideUnitSphere * 6f; }
            }
            Destroy(gameObject);
        }

        /// <summary>What's in a present: one roll, and the better it is the rarer. (Also used by the big present crates.)</summary>
        public static List<ItemData> Roll(Rng rng, bool pole, bool santa, out bool grenade)
        {
            grenade = false;
            var items = new List<ItemData>();
            float r = rng.Value;
            void Gun(System.Predicate<GunDef> which)
            {
                var pool = Db.Guns.FindAll(which);
                if (pool.Count == 0) return;
                var g = pool[rng.Range(0, pool.Count)];
                items.Add(new ItemData(g.id) { rounds = g.magSize, chambered = false });   // a present comes loaded
            }
            void Add(Db.LootTable t, int n) { for (int i = 0; i < n; i++) { var d = Db.Roll(t, rng); if (d != null) items.Add(new ItemData(d.id)); } }
            // the North Pole Keycard only ever comes out of a present (more often on the North Pole), and always from Santa's
            if (santa) { items.Add(new ItemData("key_xmas")); Add(Db.LootTable.Vault, 1); Add(Db.LootTable.Crate, 1); }
            else if (rng.Chance(pole ? 0.07f : 0.02f)) { items.Add(new ItemData("key_xmas")); Add(Db.LootTable.Crate, 1); }
            else if (r < 0.32f) Add(Db.LootTable.Crate, 2);
            else if (r < 0.46f) grenade = true;   // the prank: a live grenade
            else if (r < 0.6f) { Add(Db.LootTable.Medical, 2); items.Add(new ItemData(rng.Chance(0.6f) ? "frag" : "flash")); }
            else if (r < 0.78f) { Gun(g => g.rarity <= Rarity.Uncommon); Add(Db.LootTable.Crate, 1); }
            else if (r < 0.88f) { Add(Db.LootTable.Armor, 1); Add(Db.LootTable.Medical, 1); }
            else if (r < 0.945f) { Gun(g => g.rarity == Rarity.Rare || g.rarity == Rarity.Epic); Add(Db.LootTable.OpticsBox, 1); }
            else if (r < 0.975f) items.Add(new ItemData("key_vault"));
            else if (r < 0.993f) items.Add(new ItemData("val_radio"));
            else Add(Db.LootTable.Vault, 4);
            return items;
        }
    }
}
