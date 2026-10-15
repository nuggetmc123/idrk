using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The customization panel on a garage wall. Poke it to change your kart's
    /// paint, accent color, pattern, booster, wheels and spoiler; your parked
    /// kart updates instantly and everyone sees it. Only the garage's owner can
    /// use it; for anyone else it just says whose garage this is.
    /// Builds itself at runtime. The panel faces -Z (stand on that side).
    /// </summary>
    public class GarageEditor : MonoBehaviour
    {
        public Garage garage;

        TextMeshPro title, lockText, boosterInfo;
        readonly Dictionary<string, TextMeshPro> valueLabels = new Dictionary<string, TextMeshPro>();
        Transform paintMarker, accentMarker;
        readonly List<Vector3> paintSpots = new List<Vector3>(), accentSpots = new List<Vector3>();
        GameObject controls;
        float nextRefresh;

        void Start()
        {
            if (garage == null) garage = GetComponentInParent<Garage>();
            var face = new GameObject("Face").transform;
            face.SetParent(transform, false);

            KartUI.Box(face, "Panel", new Vector3(0f, 0f, 0.03f), new Vector3(1.05f, 1.2f, 0.04f), new Color(0.1f, 0.1f, 0.14f));
            KartUI.Box(face, "Trim", new Vector3(0f, 0.6f, 0.02f), new Vector3(1.05f, 0.03f, 0.05f), new Color(1f, 0.6f, 0.1f));
            title = KartUI.Label(face, "CUSTOMIZE", new Vector3(0f, 0.53f, 0f), 0.55f, Color.white, new Vector2(1f, 0.1f));

            controls = new GameObject("Controls");
            controls.transform.SetParent(face, false);
            var c = controls.transform;

            KartUI.Label(c, "PAINT", new Vector3(-0.42f, 0.44f, 0f), 0.3f, new Color(1f, 0.8f, 0.4f), new Vector2(0.2f, 0.05f));
            KartUI.Label(c, "ACCENT", new Vector3(-0.42f, 0.27f, 0f), 0.3f, new Color(1f, 0.8f, 0.4f), new Vector2(0.2f, 0.05f));
            paintMarker = KartUI.Box(c, "PaintMarker", Vector3.zero, new Vector3(0.085f, 0.085f, 0.02f), Color.white).transform;
            accentMarker = KartUI.Box(c, "AccentMarker", Vector3.zero, new Vector3(0.085f, 0.085f, 0.02f), Color.white).transform;
            for (int i = 0; i < PlayerProfile.Palette.Length; i++)
            {
                int index = i;
                var p = new Vector3(-0.43f + i * 0.078f, 0.37f, 0f);
                var a = new Vector3(-0.43f + i * 0.078f, 0.2f, 0f);
                paintSpots.Add(p + new Vector3(0f, 0f, 0.015f));
                accentSpots.Add(a + new Vector3(0f, 0f, 0.015f));
                PokeButton.Create(c, "Paint" + i, "", p, new Vector3(0.065f, 0.065f, 0.04f), PlayerProfile.Palette[i], () => Change(l => { l.paint = index; return l; }));
                PokeButton.Create(c, "Accent" + i, "", a, new Vector3(0.065f, 0.065f, 0.04f), PlayerProfile.Palette[i], () => Change(l => { l.accent = index; return l; }));
            }

            Row(c, "PATTERN", 0.06f, d => Change(l => { l.pattern = (KartPattern)Wrap((int)l.pattern + d, KartLook.Count<KartPattern>()); return l; }));
            Row(c, "BOOSTER", -0.07f, d => Change(l => { l.booster = (KartBooster)Wrap((int)l.booster + d, KartLook.Count<KartBooster>()); return l; }));
            Row(c, "WHEELS", -0.2f, d => Change(l => { l.wheels = (KartWheels)Wrap((int)l.wheels + d, KartLook.Count<KartWheels>()); return l; }));
            Row(c, "SPOILER", -0.33f, d => Change(l => { l.spoiler = (KartSpoiler)Wrap((int)l.spoiler + d, KartLook.Count<KartSpoiler>()); return l; }));
            boosterInfo = KartUI.Label(c, "", new Vector3(0f, -0.45f, 0f), 0.26f, new Color(0.7f, 0.9f, 1f), new Vector2(1f, 0.08f));

            lockText = KartUI.Label(face, "", new Vector3(0f, 0f, -0.01f), 0.6f, Color.white, new Vector2(0.95f, 0.6f));
        }

        static int Wrap(int v, int count) => ((v % count) + count) % count;

        void Row(Transform parent, string name, float y, System.Action<int> step)
        {
            KartUI.Label(parent, name, new Vector3(0f, y + 0.045f, 0f), 0.26f, new Color(1f, 0.8f, 0.4f), new Vector2(0.6f, 0.04f));
            valueLabels[name] = KartUI.Label(parent, "", new Vector3(0f, y - 0.01f, 0f), 0.42f, Color.white, new Vector2(0.62f, 0.07f));
            var size = new Vector3(0.12f, 0.09f, 0.04f);
            PokeButton.Create(parent, name + "Prev", "<", new Vector3(-0.42f, y, 0f), size, new Color(0.55f, 0.75f, 1f), () => step(-1), 0.6f);
            PokeButton.Create(parent, name + "Next", ">", new Vector3(0.42f, y, 0f), size, new Color(0.55f, 0.75f, 1f), () => step(1), 0.6f);
        }

        /// <summary>Apply an edit to your kart's look (owner only).</summary>
        void Change(System.Func<KartLook, KartLook> edit)
        {
            if (garage == null || !garage.OwnedByLocalPlayer) return;
            var kart = KartController.Local;
            kart.SetLook(edit(kart.Look));
            nextRefresh = 0f;
        }

        void Update()
        {
            if (title == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.25f;

            bool mine = garage != null && garage.OwnedByLocalPlayer;
            controls.SetActive(mine);
            var owner = garage != null ? garage.Owner : null;
            title.text = mine ? "CUSTOMIZE YOUR KART" : "GARAGE";
            lockText.text = mine ? "" : owner != null
                ? owner.Nickname + "'s garage\n<size=60%>Only they can customize here.\nFind YOUR GARAGE sign!</size>"
                : "Open bay";
            if (!mine) return;

            var look = KartController.Local.Look;
            valueLabels["PATTERN"].text = KartParts.Name(look.pattern);
            valueLabels["BOOSTER"].text = KartParts.Name(look.booster);
            valueLabels["WHEELS"].text = KartParts.Name(look.wheels);
            valueLabels["SPOILER"].text = KartParts.Name(look.spoiler);
            boosterInfo.text = KartParts.Describe(look.booster);
            paintMarker.localPosition = paintSpots[look.paint % paintSpots.Count];
            accentMarker.localPosition = accentSpots[look.accent % accentSpots.Count];
        }
    }
}
