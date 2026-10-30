using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// A loadout kiosk: walk up to it and the lasers come on. Flip through
    /// primaries and sidearms (the ones you've unlocked), or switch to the attachments
    /// screen and fit optics, muzzles, grips and mags. A hologram of the gun spins above.
    /// </summary>
    public class LoadoutStation : MonoBehaviour
    {
        UIPanel panel;
        Transform holo;
        int shownPrimary = -1, shownSidearm = -1, shownCode = -1, shownUnlocks = -1;
        bool attachMode;
        int attachGun;   // 0 = primary, 1 = sidearm

        static List<int> primaries, sidearms;

        /// <summary>Build one at pos; you use it from its local -z side, so yaw it to face away from where people stand.</summary>
        public static LoadoutStation Create(Transform parent, Vector3 pos, float yaw)
        {
            if (primaries == null)
            {
                primaries = new List<int>(); sidearms = new List<int>();
                for (int i = 0; i < GunCatalog.All.Length; i++)
                    (GunCatalog.All[i].cls == GunClass.Pistol ? sidearms : primaries).Add(i);
            }
            var root = Prims.Empty("loadout station", parent, pos);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var s = root.AddComponent<LoadoutStation>();
            var t = root.transform;
            // the cabinet (its front, where you stand, is local -z)
            var body = MilitaryProps.OliveDark;
            var steel = MilitaryProps.DarkSteel;
            var glow = Prims.Emissive(UIKit.Accent, 2f);
            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1f, 0.45f), body);
            Prims.Box(t, new Vector3(0, 1.02f, 0.03f), new Vector3(0.74f, 0.05f, 0.5f), steel, false);
            Prims.Box(t, new Vector3(0, 0.08f, -0.05f), new Vector3(0.76f, 0.16f, 0.5f), steel, false);
            Prims.Box(t, new Vector3(0, 0.62f, -0.226f), new Vector3(0.5f, 0.02f, 0.005f), glow, false);
            Prims.Box(t, new Vector3(0, 0.3f, -0.226f), new Vector3(0.6f, 0.35f, 0.005f), MapKit.M("1e2124", 0.4f, 0.5f, Surface.Metal), false);
            for (int i = 0; i < 5; i++) Prims.Box(t, new Vector3(0, 0.18f + i * 0.06f, -0.23f), new Vector3(0.52f, 0.015f, 0.005f), steel, false);
            // a tilted screen on a stand
            var head = Prims.Empty("screen", t, new Vector3(0, 1.38f, 0.05f)).transform;
            head.localRotation = Quaternion.Euler(20, 0, 0);   // leans back, facing up at you
            Prims.Box(head, new Vector3(0, 0, 0.035f), new Vector3(0.7f, 0.5f, 0.05f), steel, false);
            Prims.Box(t, new Vector3(0, 1.15f, 0.1f), new Vector3(0.08f, 0.3f, 0.08f), steel, false);
            s.panel = UIPanel.Create("loadout", new Vector2(0.64f, 0.44f), UIKit.PanelColor, head);
            s.panel.transform.localPosition = Vector3.zero;
            // the hologram pad on top
            Prims.Cyl(t, new Vector3(0, 1.75f, 0.12f), 0.3f, 0.03f, steel, Vector3.up);
            Prims.Cyl(t, new Vector3(0, 1.767f, 0.12f), 0.24f, 0.01f, Prims.Emissive(Prims.Hex("4ee1ff"), 2f), Vector3.up);
            s.holo = Prims.Empty("holo", t, new Vector3(0, 1.98f, 0.12f)).transform;
            var sp = s.holo.gameObject.AddComponent<Spinner>();
            sp.speed = 50f; sp.bob = 0.02f;
            var title = UIKit.Text(t, "LOADOUT", new Vector3(0, 0.82f, -0.235f), 0.07f, UIKit.Accent);
            title.fontStyle = FontStyle.Bold;
            return s;
        }

        void Update()
        {
            var rig = PlayerRig.I;
            if (rig == null) return;
            var st = Save.Settings;
            int unlocks = Progression.Points * 1000 + Progression.Level;
            if (st.primaryGun != shownPrimary || st.sidearmGun != shownSidearm || Attachments.For(EditedGun) != shownCode || unlocks != shownUnlocks) Rebuild();
            var d = rig.head.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 1.8f * 1.8f && UIPointer.I) UIPointer.I.KeepOn();
        }

        int EditedGun => attachMode && attachGun == 1 ? Save.Settings.sidearmGun : Save.Settings.primaryGun;

        void Rebuild()
        {
            var st = Save.Settings;
            shownPrimary = st.primaryGun;
            shownSidearm = st.sidearmGun;
            shownCode = Attachments.For(EditedGun);
            shownUnlocks = Progression.Points * 1000 + Progression.Level;
            panel.Clear();
            var def = GunCatalog.Get(EditedGun);
            if (attachMode) BuildAttachments(def);
            else
            {
                panel.Text("LOADOUT STATION", new Vector2(0, 0.185f), 0.03f, UIKit.Accent);
                Row("PRIMARY · left hip", primaries, st.primaryGun, 0.09f, i => st.primaryGun = i);
                Row("SIDEARM · right hip", sidearms, st.sidearmGun, -0.04f, i => st.sidearmGun = i);
                if (def != null)
                    panel.Text($"{GunCatalog.ClassName(def.cls).TrimEnd('s')} · {def.damage * def.pellets:0} dmg · {Attachments.MagSize(def, shownCode)} rds · {def.rpm:0} rpm",
                        new Vector2(0, -0.125f), 0.019f, UIKit.Muted);
                panel.Button("ATTACHMENTS", new Vector2(0, -0.18f), new Vector2(0.3f, 0.055f), () => { attachMode = true; attachGun = 0; Rebuild(); }, 0.024f);
            }

            for (int i = holo.childCount - 1; i >= 0; i--) Destroy(holo.GetChild(i).gameObject);
            if (def != null)
            {
                var gm = GunModel.Build(def, holo, shownCode);
                gm.transform.localPosition = new Vector3(0, 0, -def.bodyLen * 0.3f);
                foreach (var c in gm.GetComponentsInChildren<Collider>()) Destroy(c);
            }
        }

        /// <summary>The attachments screen: one row per slot for the gun you're editing.</summary>
        void BuildAttachments(GunDef def)
        {
            if (def == null) return;
            panel.Text("ATTACHMENTS · " + def.name.ToUpperInvariant(), new Vector2(0, 0.19f), 0.026f, UIKit.Accent);
            var p = panel.Button("Primary", new Vector2(-0.12f, 0.145f), new Vector2(0.2f, 0.042f), () => { attachGun = 0; Rebuild(); }, 0.02f);
            var s = panel.Button("Sidearm", new Vector2(0.12f, 0.145f), new Vector2(0.2f, 0.042f), () => { attachGun = 1; Rebuild(); }, 0.02f);
            var on = new Color(0.95f, 0.5f, 0.1f);
            (attachGun == 0 ? p : s).SetColors(on, Color.Lerp(on, Color.white, 0.3f));
            int code = shownCode;
            for (int si = 0; si < 4; si++)
            {
                var slot = (Slot)si;
                float y = 0.085f - si * 0.062f;
                int v = Attachments.Get(code, slot);
                bool any = false;
                for (int o = 1; o < Attachments.Options(slot); o++) any |= Attachments.Fits(def, slot, o);
                panel.Text(slot.ToString().ToUpperInvariant(), new Vector2(-0.27f, y), 0.018f, UIKit.Muted, TextAnchor.MiddleLeft);
                if (!any) { panel.Text("doesn't fit this gun", new Vector2(0.03f, y), 0.019f, UIKit.Muted); continue; }
                panel.Text(Attachments.Name(slot, v), new Vector2(0.03f, y + 0.008f), 0.024f, Color.white);
                panel.Text(Attachments.Describe(slot, v), new Vector2(0.03f, y - 0.018f), 0.014f, UIKit.Muted);
                int gun = EditedGun;
                panel.Button("<", new Vector2(-0.13f, y), new Vector2(0.06f, 0.05f), () => Step(def, gun, slot, -1), 0.03f);
                panel.Button(">", new Vector2(0.2f, y), new Vector2(0.06f, 0.05f), () => Step(def, gun, slot, 1), 0.03f);
            }
            panel.Text("More attachments unlock in Menu > Skills.", new Vector2(0, -0.17f), 0.015f, UIKit.Muted);
            panel.Button("BACK", new Vector2(0.24f, -0.19f), new Vector2(0.12f, 0.04f), () => { attachMode = false; Rebuild(); }, 0.018f);
        }

        /// <summary>Next attachment in this slot that fits and is unlocked.</summary>
        void Step(GunDef def, int gun, Slot slot, int dir)
        {
            int code = Attachments.For(gun), n = Attachments.Options(slot), v = Attachments.Get(code, slot);
            for (int i = 0; i < n; i++)
            {
                v = ((v + dir) % n + n) % n;
                if (Attachments.Fits(def, slot, v) && Progression.HasAttachment(slot, v)) break;
            }
            Attachments.Set(gun, Attachments.With(code, slot, v));
            AudioManager.Ui("ui_click", 0.6f);
            if (Game.I) Game.I.LoadoutChanged();
            Rebuild();
        }

        void Row(string label, List<int> all, int current, float y, System.Action<int> set)
        {
            var list = all.FindAll(Progression.HasGun);
            if (list.Count == 0) return;
            int at = Mathf.Max(0, list.IndexOf(current));
            var g = GunCatalog.Get(list[at]);
            panel.Text(label, new Vector2(0, y + 0.045f), 0.02f, UIKit.Muted);
            panel.Text(g != null ? g.name : "?", new Vector2(0, y - 0.01f), 0.036f, Color.white);
            panel.Button("<", new Vector2(-0.25f, y - 0.01f), new Vector2(0.09f, 0.075f), () => Pick(list, at - 1, set), 0.04f);
            panel.Button(">", new Vector2(0.25f, y - 0.01f), new Vector2(0.09f, 0.075f), () => Pick(list, at + 1, set), 0.04f);
        }

        static void Pick(List<int> list, int at, System.Action<int> set)
        {
            at = ((at % list.Count) + list.Count) % list.Count;
            set(list[at]);
            Save.Store();
            if (Game.I) Game.I.LoadoutChanged();
        }
    }
}
