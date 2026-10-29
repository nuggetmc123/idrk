using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The Outfitter: a booth in the hub where you pick your hat, top, pants,
    /// shoes and gloves. A mannequin next to it wears whatever you pick (in VR
    /// you can't see your own body, so it shows you what everyone else sees).
    /// </summary>
    public class OutfitStation : MonoBehaviour
    {
        UIPanel panel;
        Avatar mannequin;
        Transform mannequinSpot;
        int shown = -1;
        float spin;

        /// <summary>The whole booth. pos is the middle of its floor; people walk in from +x.</summary>
        public static void BuildBooth(MapKit k, Vector3 pos)
        {
            var wood = MapKit.M("7a5a3a", 0.25f, 0f, Surface.Plank);
            var wall = MapKit.M("e9e2d4", 0.3f, 0f, Surface.Concrete);
            var trim = MilitaryProps.DarkSteel;
            var stripeA = MapKit.M("d23a5a", 0.35f, 0f, Surface.Fabric);
            var stripeB = MapKit.M("f2efe6", 0.35f, 0f, Surface.Fabric);
            Vector3 P(float x, float y, float z) => pos + new Vector3(x, y, z);

            k.Box(P(0, 0.08f, 0), new Vector3(6, 0.16f, 6), wood);
            k.Box(P(-3, 1.75f, 0), new Vector3(0.2f, 3.2f, 6), wall);
            foreach (float z in new[] { -3f, 3f }) k.Box(P(-1.75f, 1.75f, z), new Vector3(2.5f, 3.2f, 0.2f), wall);
            k.Box(P(0, 3.4f, 0), new Vector3(6.4f, 0.15f, 6.4f), trim);
            for (int i = 0; i < 8; i++)
                k.BoxRot(P(3.35f, 3.15f, -2.8f + i * 0.8f), new Vector3(0.7f, 0.06f, 0.8f), i % 2 == 0 ? stripeA : stripeB, Quaternion.Euler(0, 0, -35), false);
            foreach (float z in new[] { -2.9f, 2.9f }) k.Box(P(2.9f, 1.7f, z), new Vector3(0.15f, 3.4f, 0.15f), trim);
            k.Box(P(0, 3.3f, 0), new Vector3(0.12f, 0.05f, 5f), Prims.Emissive(Prims.Hex("fff1d6"), 1.6f), 0, false);
            var title = k.Sign(P(3.75f, 2.75f, 0), 90, "OUTFITTER", 0.5f, new Color(1f, 0.55f, 0.75f));
            title.fontStyle = FontStyle.Bold;
            k.Sign(P(3.75f, 2.35f, 0), 90, "hats · clothes · gloves", 0.16f, Color.white);

            // a wall of hats on pegs
            k.Box(P(-2.82f, 1.62f, 0), new Vector3(0.16f, 0.04f, 5.2f), wood, 0, false);
            string[] cols = { "d63a2f", "2e6db3", "2e9e57", "ffb02e", "6d4c9a", "d23a5a" };
            int[] hats = { 4, 5, 7, 8, 16, 12 };
            for (int i = 0; i < hats.Length; i++)
            {
                var peg = P(-2.75f, 1.62f, -2.2f + i * 0.88f);
                Prims.Cyl(k.D, peg + new Vector3(0, 0.08f, 0), 0.03f, 0.16f, trim, Vector3.up);
                var stand = Prims.Empty("hat on peg", k.D, peg + new Vector3(0, 0.04f, 0)).transform;
                stand.rotation = Quaternion.Euler(0, 90, 0);
                Cosmetics.Hat(stand, hats[i], Prims.Hex(cols[i]), null);
            }
            // a rack of clothes
            Prims.Cyl(k.S, P(-1.2f, 1.7f, -2.4f), 0.04f, 2.2f, trim, Vector3.right);
            foreach (float x in new[] { -2.25f, -0.15f }) Prims.Cyl(k.S, P(x, 0.93f, -2.4f), 0.04f, 1.55f, trim, Vector3.up);
            for (int i = 0; i < 7; i++)
            {
                var c = Prims.Hex(cols[i % cols.Length]);
                Prims.Box(k.S, P(-2.0f + i * 0.28f, 1.35f, -2.4f), new Vector3(0.05f, 0.62f, 0.42f), Prims.Lit(c, 0.3f, 0f, Surface.Fabric), false);
                Prims.Box(k.S, P(-2.0f + i * 0.28f, 1.68f, -2.4f), new Vector3(0.01f, 0.06f, 0.2f), trim, false);
            }
            // the mannequin pedestal and the dressing kiosk
            Prims.Cyl(k.S, P(-1.2f, 0.26f, 1.2f), 1.1f, 0.2f, MapKit.M("2c2f33", 0.6f, 0.6f, Surface.Metal), Vector3.up, true);
            Prims.Cyl(k.S, P(-1.2f, 0.365f, 1.2f), 0.9f, 0.01f, Prims.Emissive(Prims.Hex("ff6bd5"), 1.4f), Vector3.up);
            var spot = Prims.Empty("mannequin", k.D, P(-1.2f, 0.37f, 1.2f)).transform;
            var st = Create(k.D, P(0.9f, 0.16f, -0.6f), -90f);
            st.mannequinSpot = spot;
            st.mannequin = Avatar.Build(spot, "mannequin", new Color(1f, 0.42f, 0.84f), Save.Settings != null ? Save.Settings.look : 0);
            foreach (var c in st.mannequin.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            st.PoseMannequin();
            var tag = UIKit.Text(spot, "THIS IS YOU", new Vector3(0, 2.05f, 0), 0.09f, new Color(1f, 0.6f, 0.85f));
            tag.transform.rotation = Quaternion.Euler(0, -90, 0);   // readable from the +x side, where you walk in
        }

        /// <summary>The kiosk alone; you use it from its local -z side.</summary>
        public static OutfitStation Create(Transform parent, Vector3 pos, float yaw)
        {
            var root = Prims.Empty("outfit station", parent, pos);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var s = root.AddComponent<OutfitStation>();
            var t = root.transform;
            var shell = MapKit.M("2c2f33", 0.5f, 0.5f, Surface.Metal);
            var pink = Prims.Emissive(Prims.Hex("ff6bd5"), 1.8f);
            Prims.Box(t, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 1f, 0.45f), shell);
            Prims.Box(t, new Vector3(0, 0.98f, -0.235f), new Vector3(0.8f, 0.02f, 0.005f), pink, false);
            Prims.Box(t, new Vector3(0, 1.2f, 0.1f), new Vector3(0.1f, 0.5f, 0.1f), shell, false);
            var head = Prims.Empty("screen", t, new Vector3(0, 1.5f, 0.05f)).transform;
            head.localRotation = Quaternion.Euler(15, 0, 0);
            Prims.Box(head, new Vector3(0, 0, 0.035f), new Vector3(0.86f, 0.74f, 0.05f), shell, false);
            s.panel = UIPanel.Create("outfit", new Vector2(0.82f, 0.7f), UIKit.PanelColor, head);
            s.panel.transform.localPosition = Vector3.zero;
            return s;
        }

        void Update()
        {
            var rig = PlayerRig.I;
            if (rig == null || Save.Settings == null) return;
            int code = Save.Settings.look;
            if (code != shown) Rebuild(code);
            var d = rig.head.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 2f * 2f && UIPointer.I) UIPointer.I.KeepOn();
            if (mannequin != null)
            {
                spin += Time.deltaTime * 25f;
                PoseMannequin();
            }
        }

        /// <summary>Arms out in a T, slowly turning, like the reference picture.</summary>
        void PoseMannequin()
        {
            var feet = mannequinSpot.position;
            var rot = Quaternion.Euler(0, 90 + Mathf.Sin(spin * Mathf.Deg2Rad * 2f) * 40f, 0);
            var right = rot * Vector3.right;
            var headPos = feet + Vector3.up * 1.62f;
            var shoulder = feet + Vector3.up * 1.4f;
            mannequin.Pose(feet, rot.eulerAngles.y, headPos, rot,
                shoulder - right * 0.72f, Quaternion.LookRotation(-right, Vector3.up),
                shoulder + right * 0.72f, Quaternion.LookRotation(right, Vector3.up), 0f, false);
        }

        void Rebuild(int code)
        {
            shown = code;
            if (mannequin) mannequin.ApplyLook(code);
            var look = Look.Unpack(code);
            panel.Clear();
            var title = panel.Text("OUTFITTER", new Vector2(0, 0.3f), 0.045f, new Color(1f, 0.55f, 0.8f));
            title.fontStyle = FontStyle.Bold;
            float y = 0.22f, step = 0.068f;
            Row("HAT", Cosmetics.Hats[look.hat], y, d => { var l = Cur; l.hat = Wrap(l.hat + d, Cosmetics.Hats.Length); Set(l); });
            Row("HAT COLOUR", Cosmetics.HatTakesColor(look.hat) ? Cosmetics.Colors[look.hatColor].name : "-", y -= step,
                d => { var l = Cur; l.hatColor = Wrap(l.hatColor + d, Cosmetics.Colors.Length); Set(l); }, Cosmetics.Colors[look.hatColor].color);
            Row("TOP", Cosmetics.Tops[look.top], y -= step, d => { var l = Cur; l.top = Wrap(l.top + d, Cosmetics.Tops.Length); Set(l); });
            Row("TOP COLOUR", Cosmetics.TopTakesColor(look.top) ? Cosmetics.Colors[look.topColor].name : "-", y -= step,
                d => { var l = Cur; l.topColor = Wrap(l.topColor + d, Cosmetics.Colors.Length); Set(l); }, Cosmetics.Colors[look.topColor].color);
            Row("PANTS", Cosmetics.Pants[look.pants], y -= step, d => { var l = Cur; l.pants = Wrap(l.pants + d, Cosmetics.Pants.Length); Set(l); });
            Row("SHOES", Cosmetics.Shoes[look.shoes], y -= step, d => { var l = Cur; l.shoes = Wrap(l.shoes + d, Cosmetics.Shoes.Length); Set(l); });
            Row("GLOVES", Cosmetics.Gloves[look.gloves], y -= step, d => { var l = Cur; l.gloves = Wrap(l.gloves + d, Cosmetics.Gloves.Length); Set(l); });
            panel.Button("RANDOM", new Vector2(-0.18f, -0.29f), new Vector2(0.3f, 0.07f), () => Set(Look.Random(new System.Random())), 0.028f);
            panel.Button("PLAIN", new Vector2(0.18f, -0.29f), new Vector2(0.3f, 0.07f), () => Set(new Look()), 0.028f);
        }

        static Look Cur => Look.Unpack(Save.Settings.look);
        static int Wrap(int v, int n) => ((v % n) + n) % n;

        static void Set(Look l)
        {
            Save.Settings.look = l.Pack();
            Save.Store();
            if (PlayerRig.I) PlayerRig.I.RefreshLook();
        }

        void Row(string label, string value, float y, System.Action<int> change, Color? swatch = null)
        {
            panel.Text(label, new Vector2(-0.37f, y), 0.022f, UIKit.Muted, TextAnchor.MiddleLeft);
            panel.Button("<", new Vector2(-0.04f, y), new Vector2(0.07f, 0.055f), () => change(-1), 0.032f);
            panel.Button(">", new Vector2(0.35f, y), new Vector2(0.07f, 0.055f), () => change(1), 0.032f);
            panel.Text(value, new Vector2(0.155f, y), 0.026f, Color.white);
            if (swatch.HasValue && value != "-")
                UIKit.Quad(panel.content, new Vector3(0.28f, y, -0.003f), new Vector2(0.03f, 0.03f), swatch.Value, 2998);
        }
    }
}
