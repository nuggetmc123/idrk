using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// A loadout kiosk: walk up to it and the lasers come on. Flip through
    /// primaries and sidearms; a hologram of the gun spins above the screen.
    /// </summary>
    public class LoadoutStation : MonoBehaviour
    {
        UIPanel panel;
        Transform holo;
        int shownPrimary = -1, shownSidearm = -1;

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
            if (st.primaryGun != shownPrimary || st.sidearmGun != shownSidearm) Rebuild();
            var d = rig.head.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 1.8f * 1.8f && UIPointer.I) UIPointer.I.KeepOn();
        }

        void Rebuild()
        {
            var st = Save.Settings;
            shownPrimary = st.primaryGun;
            shownSidearm = st.sidearmGun;
            panel.Clear();
            panel.Text("LOADOUT STATION", new Vector2(0, 0.185f), 0.03f, UIKit.Accent);
            Row("PRIMARY · left hip", primaries, st.primaryGun, 0.08f, i => st.primaryGun = i);
            Row("SIDEARM · right hip", sidearms, st.sidearmGun, -0.07f, i => st.sidearmGun = i);
            var def = GunCatalog.Get(st.primaryGun);
            if (def != null)
                panel.Text($"{GunCatalog.ClassName(def.cls).TrimEnd('s')} · {def.damage * def.pellets:0} dmg · {def.magSize} rds · {def.rpm:0} rpm",
                    new Vector2(0, -0.185f), 0.019f, UIKit.Muted);

            for (int i = holo.childCount - 1; i >= 0; i--) Destroy(holo.GetChild(i).gameObject);
            if (def != null)
            {
                var gm = GunModel.Build(def, holo);
                gm.transform.localPosition = new Vector3(0, 0, -def.bodyLen * 0.3f);
                foreach (var c in gm.GetComponentsInChildren<Collider>()) Destroy(c);
            }
        }

        void Row(string label, List<int> list, int current, float y, System.Action<int> set)
        {
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
