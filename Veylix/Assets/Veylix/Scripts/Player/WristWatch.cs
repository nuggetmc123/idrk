using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// A race watch on your left wrist: steel case, black strap and a lit screen with the time of day, your position
    /// and lap. Press the menu button (three lines) during a session and the watch's settings screen rises above it
    /// (volumes, assists and race actions); press it again and it folds away. See Menus.ShowWatch.
    /// </summary>
    public class WristWatch : MonoBehaviour
    {
        Glove glove;
        Transform face;
        TextMeshPro clock, info;
        MeshRenderer screen;
        float nextText;

        public static WristWatch Create(Glove glove)
        {
            var w = glove.gameObject.AddComponent<WristWatch>();
            w.glove = glove;
            w.Build();
            return w;
        }

        /// Where the settings screen opens: above the watch face.
        public Transform Face => face;

        void Build()
        {
            var A = VXAssets.I;
            var steel = VXAssets.Or(A != null ? A.metal : null, "Watch steel", new Color(0.72f, 0.73f, 0.75f), 0.85f);
            var strap = VXAssets.Or(A != null ? A.satinBlack : null, "Watch strap", new Color(0.04f, 0.04f, 0.05f), 0.35f);
            var root = new GameObject("Watch").transform;
            root.SetParent(glove.Model != null ? glove.Model : transform, false);
            root.localPosition = new Vector3(0f, 0f, -0.118f); // over the glove's cuff

            var k = new MeshKit();
            // Strap: an oval band round the wrist.
            var ring = new Vector3[29];
            for (int i = 0; i <= 28; i++) { float a = i / 28f * Mathf.PI * 2f; ring[i] = new Vector3(Mathf.Cos(a) * 0.044f, Mathf.Sin(a) * 0.036f, 0f); }
            k.Tube(1, ring, 0.006f, 8, false);
            // Case on the back of the wrist, with a crown and bezel.
            k.Box(0, new Vector3(0f, 0.04f, 0f), new Vector3(0.036f, 0.01f, 0.04f), 1f, 0.003f);
            k.Box(0, new Vector3(0.021f, 0.04f, 0f), new Vector3(0.006f, 0.004f, 0.004f));
            var go = new GameObject("Case");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = k.Build("watch", 2);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { steel, strap };

            face = new GameObject("Face").transform;
            face.SetParent(root, false);
            face.localPosition = new Vector3(0f, 0.0455f, 0f);
            face.localRotation = Quaternion.Euler(90f, 0f, 90f); // screen facing up off the back of the wrist
            screen = VUI.Quad(face, "Screen", Vector2.zero, new Vector2(0.034f, 0.03f), new Color(0.02f, 0.03f, 0.04f, 1f), 0f, 0.004f);
            clock = VUI.Label(face, "", new Vector2(0f, 0.004f), 0.09f, VUI.Text, TextAlignmentOptions.Midline, true, 0.034f, -0.0005f);
            info = VUI.Label(face, "", new Vector2(0f, -0.008f), 0.04f, VUI.Accent, TextAlignmentOptions.Midline, true, 0.034f, -0.0005f);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (Time.unscaledTime < nextText || clock == null) return;
            nextText = Time.unscaledTime + 0.5f;
            var sky = Sky.I;
            float h = sky != null ? sky.hour : (float)System.DateTime.Now.TimeOfDay.TotalHours;
            clock.text = Mathf.FloorToInt(h).ToString("00") + ":" + Mathf.FloorToInt(Mathf.Repeat(h, 1f) * 60f).ToString("00");
            var s = RaceSession.I;
            info.text = s != null && s.player != null && s.Timed
                ? "P" + s.player.position + "   L" + Mathf.Min(s.player.lap + 1, Mathf.Max(1, s.totalLaps)) + "/" + Mathf.Max(1, s.totalLaps)
                : "VEYLIX";
            // The screen glows brighter while its menu is open.
            bool open = Game.I != null && Game.I.menus != null && Game.I.menus.OnWatch;
            VUI.SetColor(screen, open ? new Color(0.1f, 0.06f, 0.05f, 1f) : new Color(0.02f, 0.03f, 0.04f, 1f));
        }
    }
}
