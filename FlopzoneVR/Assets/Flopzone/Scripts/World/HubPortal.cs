using UnityEngine;

namespace Flopzone
{
    /// <summary>A walk-in portal that sends the player (and their party) to a game mode.</summary>
    public class HubPortal : MonoBehaviour
    {
        public string mode;
        Material membrane;
        Color baseColor;
        float cooldown;
        Transform ring;

        static readonly string[] Colors = { "ff5a4e", "ffc23d", "4ee1ff", "7cff6b", "b06bff", "ff6bd5" };

        public static HubPortal Create(MapKit k, Vector3 pos, float yaw, string mode, string label, string info, int colorIndex)
        {
            var root = Prims.Empty("portal " + mode, k.D, pos);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var p = root.AddComponent<HubPortal>();
            p.mode = mode;
            var c = Prims.Hex(Colors[colorIndex % Colors.Length]);
            var frame = MilitaryProps.OliveDark;
            var steel = MilitaryProps.DarkSteel;
            var glow = Prims.Emissive(c, 1.8f);
            var t = root.transform;
            // a heavy steel blast-door frame, opened up, with a glowing field across it
            Prims.Box(t, new Vector3(-2.75f, 2.5f, 0), new Vector3(0.5f, 5f, 0.7f), frame);
            Prims.Box(t, new Vector3(2.75f, 2.5f, 0), new Vector3(0.5f, 5f, 0.7f), frame);
            Prims.Box(t, new Vector3(0, 5.2f, 0), new Vector3(6f, 0.4f, 0.7f), frame);
            Prims.Box(t, new Vector3(-2.52f, 2.5f, 0.36f), new Vector3(0.06f, 4.8f, 0.02f), glow, false);
            Prims.Box(t, new Vector3(2.52f, 2.5f, 0.36f), new Vector3(0.06f, 4.8f, 0.02f), glow, false);
            Prims.Box(t, new Vector3(0, 4.98f, 0.36f), new Vector3(5f, 0.06f, 0.02f), glow, false);
            for (int i = 0; i < 9; i++)
                Prims.Box(t, new Vector3(-2.75f, 0.3f + i * 0.55f, 0.36f), new Vector3(0.5f, 0.18f, 0.02f), i % 2 == 0 ? MilitaryProps.Hazard : steel, false, Quaternion.Euler(0, 0, 35));
            for (int i = 0; i < 9; i++)
                Prims.Box(t, new Vector3(2.75f, 0.3f + i * 0.55f, 0.36f), new Vector3(0.5f, 0.18f, 0.02f), i % 2 == 0 ? MilitaryProps.Hazard : steel, false, Quaternion.Euler(0, 0, -35));
            // the field
            var mem = Prims.Make(PrimitiveType.Quad, t, new Vector3(0, 2.5f, 0), new Vector3(5f, 4.95f, 1), null, false, Quaternion.identity, "membrane");
            p.membrane = Prims.Sprite(new Color(c.r, c.g, c.b, 0.45f));
            p.membrane.mainTexture = Prims.SoftDot;
            p.membrane.renderQueue = 3050;
            mem.GetComponent<Renderer>().sharedMaterial = p.membrane;
            p.baseColor = p.membrane.color;
            p.ring = Prims.Cyl(t, new Vector3(0, 2.5f, 0.05f), 3.2f, 0.03f, glow, Vector3.forward).transform;
            // floor pad with chevrons pointing in
            var pad = Prims.Box(t, new Vector3(0, 0.05f, 1.6f), new Vector3(5f, 0.04f, 3f), MapKit.M("2a2c2e", 0.4f, 0f, Surface.Rubber), false);
            pad.name = "pad";
            for (int i = 0; i < 3; i++)
            {
                Prims.Box(t, new Vector3(-0.55f, 0.075f, 2.6f - i * 0.8f), new Vector3(1.3f, 0.01f, 0.18f), glow, false, Quaternion.Euler(0, 40, 0));
                Prims.Box(t, new Vector3(0.55f, 0.075f, 2.6f - i * 0.8f), new Vector3(1.3f, 0.01f, 0.18f), glow, false, Quaternion.Euler(0, -40, 0));
            }
            // a lit screen above the gate with the mode
            Prims.Box(t, new Vector3(0, 5.75f, 0.3f), new Vector3(4.6f, 0.75f, 0.12f), steel, false);
            Prims.Box(t, new Vector3(0, 5.75f, 0.37f), new Vector3(4.4f, 0.6f, 0.01f), Prims.Unlit(new Color(c.r * 0.12f, c.g * 0.12f, c.b * 0.12f)), false);
            var title = UIKit.Text(t, label, new Vector3(0, 5.82f, 0.39f), 0.32f, Color.Lerp(c, Color.white, 0.6f));
            title.transform.localRotation = Quaternion.Euler(0, 180, 0);
            title.fontStyle = FontStyle.Bold;
            var t2 = UIKit.Text(t, info, new Vector3(0, 5.56f, 0.39f), 0.14f, c);
            t2.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return p;
        }

        void Update()
        {
            float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 2.5f + transform.position.x);
            membrane.color = new Color(baseColor.r, baseColor.g, baseColor.b, pulse);
            if (ring) ring.Rotate(0, 60 * Time.deltaTime, 0, Space.Self);
            var rig = PlayerRig.I;
            if (rig == null || !rig.Alive || Time.time < cooldown) return;
            var local = transform.InverseTransformPoint(rig.head.position);
            if (Mathf.Abs(local.x) < 2.3f && Mathf.Abs(local.z) < 0.5f && local.y > 0 && local.y < 5f)
            {
                cooldown = Time.time + 4f;
                AudioManager.Sfx("portal", transform.position + Vector3.up * 2, 1f);
                Game.I.GoMode(mode);
            }
        }
    }
}
