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
            var stone = MapKit.M("e8e2d4", 0.3f);
            var trim = Prims.Emissive(c, 1.4f);
            Prims.Box(root.transform, new Vector3(-1.9f, 2.2f, 0), new Vector3(0.6f, 4.4f, 0.8f), stone);
            Prims.Box(root.transform, new Vector3(1.9f, 2.2f, 0), new Vector3(0.6f, 4.4f, 0.8f), stone);
            Prims.Box(root.transform, new Vector3(0, 4.6f, 0), new Vector3(4.6f, 0.6f, 0.9f), stone);
            Prims.Box(root.transform, new Vector3(0, 4.25f, 0.46f), new Vector3(3.2f, 0.08f, 0.02f), trim, false);
            Prims.Box(root.transform, new Vector3(-1.9f, 2.2f, 0.41f), new Vector3(0.1f, 4f, 0.02f), trim, false);
            Prims.Box(root.transform, new Vector3(1.9f, 2.2f, 0.41f), new Vector3(0.1f, 4f, 0.02f), trim, false);
            var mem = Prims.Make(PrimitiveType.Quad, root.transform, new Vector3(0, 2.1f, 0), new Vector3(3.2f, 4.0f, 1), null, false, Quaternion.identity, "membrane");
            p.membrane = Prims.Sprite(new Color(c.r, c.g, c.b, 0.45f));
            p.membrane.mainTexture = Prims.SoftDot;
            p.membrane.renderQueue = 3050;
            mem.GetComponent<Renderer>().sharedMaterial = p.membrane;
            p.baseColor = p.membrane.color;
            var pad = Prims.Cyl(root.transform, new Vector3(0, 0.04f, 1.6f), 3.2f, 0.06f, Prims.Emissive(c * 0.7f, 1f), Vector3.up);
            pad.name = "pad";
            p.ring = Prims.Cyl(root.transform, new Vector3(0, 2.1f, 0.05f), 2.6f, 0.03f, trim, Vector3.forward).transform;
            var t = UIKit.Text(root.transform, label, new Vector3(0, 5.35f, 0.2f), 0.5f, Color.white);
            t.transform.localRotation = Quaternion.Euler(0, 180, 0);
            t.fontStyle = FontStyle.Bold;
            var t2 = UIKit.Text(root.transform, info, new Vector3(0, 3.95f, 0.47f), 0.17f, c);
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
            if (Mathf.Abs(local.x) < 1.5f && Mathf.Abs(local.z) < 0.5f && local.y > 0 && local.y < 4.5f)
            {
                cooldown = Time.time + 4f;
                AudioManager.Sfx("portal", transform.position + Vector3.up * 2, 1f);
                Game.I.GoMode(mode);
            }
        }
    }
}
