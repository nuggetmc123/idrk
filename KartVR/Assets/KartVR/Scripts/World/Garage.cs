using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// One garage bay in the hub. Every player automatically gets a free bay;
    /// their kart is parked here (with all their custom parts) whenever they're
    /// in the hub, so everyone walking past can see it. The sign and neon strip
    /// show who owns it. The <see cref="GarageEditor"/> on the wall only works
    /// for the owner.
    /// </summary>
    public class Garage : MonoBehaviour
    {
        public int index;
        [Tooltip("Where the kart parks (its forward = the way the kart faces).")]
        public Transform parkSpot;
        public TextMeshPro sign;
        public Renderer[] neon;
        [Tooltip("Optional: slowly spins under the parked kart (visual only).")]
        public Transform turntable;

        static readonly List<Garage> All = new List<Garage>();
        public static int Count => All.Count;

        public static Garage Get(int index)
        {
            foreach (var g in All) if (g.index == index) return g;
            return null;
        }

        public Vector3 ParkPosition => (parkSpot ? parkSpot.position : transform.position) + Vector3.down * 0.15f + Vector3.up * 0.01f; // kart root sits 0.15 below its wheels
        public Quaternion ParkRotation => parkSpot ? parkSpot.rotation : transform.rotation;

        public KartController Owner => KartController.All.FirstOrDefault(k => k.IsReady && k.GarageIndex == index);
        public bool OwnedByLocalPlayer => KartController.Local != null && KartController.Local.IsReady && KartController.Local.GarageIndex == index;

        float nextRefresh;
        Color shownNeon = new Color(-1f, 0f, 0f);

        void OnEnable()
        {
            All.Add(this);
            All.Sort((a, b) => a.index.CompareTo(b.index));
        }

        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (turntable) turntable.Rotate(0f, 6f * Time.deltaTime, 0f, Space.Self);
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.3f;

            var owner = Owner;
            if (sign)
            {
                if (owner == null) sign.text = "GARAGE " + (index + 1) + "\n<size=55%>open</size>";
                else if (owner == KartController.Local) sign.text = "YOUR GARAGE\n<size=55%>walk in to customize</size>";
                else sign.text = owner.DisplayName.ToUpper() + "'S GARAGE";
            }

            Color c = owner != null ? owner.Look.PaintColor : new Color(0.35f, 0.35f, 0.4f);
            if (c != shownNeon && neon != null)
            {
                shownNeon = c;
                var mat = owner != null ? KartUI.Glow(c) : KartUI.Mat(c, 0.4f);
                foreach (var r in neon) if (r) r.sharedMaterial = mat;
            }
        }
    }
}
