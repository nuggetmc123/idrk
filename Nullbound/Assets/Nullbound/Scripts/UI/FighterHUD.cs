using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// A display on the back of your RIGHT hand, like a watch (turn the back of your hand toward you): health, guard,
    /// special and dash. It's fixed to the hand, so it goes wherever your hand goes. Without a headset it sits at the lower right of
    /// your view. (The menu stays on the left wrist.)
    /// </summary>
    public class FighterHUD : MonoBehaviour
    {
        Transform panel, hpFill, guardFill, specialFill, dashFill;
        TextMeshPro hpText, specialText, info;
        Material hpGood, hpLow;
        Renderer hpRenderer;
        float nextText;

        void Start()
        {
            var rig = VRRig.Instance;
            panel = new GameObject("WristHUD").transform;
            StickToHand();
            NBUI.Box(panel, "Frame", new Vector3(0f, 0f, 0.006f), new Vector3(0.138f, 0.083f, 0.003f), new Color(1f, 0.75f, 0.2f));
            NBUI.Box(panel, "Back", new Vector3(0f, 0f, 0.004f), new Vector3(0.13f, 0.075f, 0.004f), new Color(0.06f, 0.06f, 0.09f));
            hpGood = NBUI.Glow(new Color(0.3f, 1f, 0.45f));
            hpLow = NBUI.Glow(new Color(1f, 0.25f, 0.2f));
            hpFill = Bar(0.022f, 0.016f, hpGood, out hpRenderer);
            guardFill = Bar(0.006f, 0.006f, NBUI.Glow(new Color(0.4f, 0.75f, 1f)), out _);
            specialFill = Bar(-0.008f, 0.01f, NBUI.Glow(new Color(1f, 0.8f, 0.25f)), out _);
            dashFill = Bar(-0.022f, 0.006f, NBUI.Glow(new Color(0.8f, 0.8f, 0.9f)), out _);
            hpText = NBUI.Label(panel, "", new Vector3(0f, 0.022f, -0.003f), 0.09f, Color.black, new Vector2(0.12f, 0.016f));
            specialText = NBUI.Label(panel, "", new Vector3(0f, -0.008f, -0.003f), 0.07f, Color.black, new Vector2(0.12f, 0.012f));
            info = NBUI.Label(panel, "", new Vector3(0f, 0.032f + 0.012f, -0.002f), 0.06f, Color.white, new Vector2(0.13f, 0.012f));
        }

        /// <summary>
        /// In VR the display is fixed to the back of your right hand, just behind the knuckles over the wrist, like a
        /// watch: it moves with your hand wherever it goes. Turn the back of your hand toward you to read it. Set every
        /// frame, so nothing (like the desktop layout before the headset woke up) can leave it out of place.
        /// </summary>
        void StickToHand()
        {
            var rig = VRRig.Instance;
            Transform hand = rig != null && rig.rightHand != null ? rig.rightHand : transform;
            if (panel.parent != hand) panel.SetParent(hand, false);
            panel.localPosition = new Vector3(0.065f, 0.005f, -0.06f);
            // Facing out of the back of the hand, turned the right way up for when you twist your wrist to look at it.
            panel.localRotation = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(0f, 0f, 180f);
            panel.localScale = Vector3.one * 0.85f;
        }

        Transform Bar(float y, float height, Material fill, out Renderer r)
        {
            NBUI.Box(panel, "Track", new Vector3(0f, y, 0.001f), new Vector3(0.12f, height, 0.002f), new Color(0.18f, 0.18f, 0.22f));
            var pivot = new GameObject("Fill").transform;
            pivot.SetParent(panel, false);
            pivot.localPosition = new Vector3(-0.06f, y, -0.001f);
            var part = NBUI.Part(pivot, PrimitiveType.Cube, new Vector3(0.5f, 0f, 0f), Vector3.one, fill);
            r = part.GetComponent<Renderer>();
            pivot.localScale = new Vector3(0.12f, height, 0.002f);
            return pivot;
        }

        static void SetFill(Transform pivot, float t)
        {
            var s = pivot.localScale;
            s.x = 0.12f * Mathf.Clamp(t, 0.001f, 1f);
            pivot.localScale = s;
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
        }

        void LateUpdate()
        {
            var me = Fighter.Local;
            panel.gameObject.SetActive(me != null && me.IsReady);
            if (me == null || !me.IsReady) return;

            if (VRRig.IsXR) StickToHand();
            else if (VRRig.Instance != null && VRRig.Instance.head != null)
            {
                // Desktop: lower right of the view (where your right arm is), not in the middle.
                var head = VRRig.Instance.head;
                panel.position = head.position + head.forward * 0.35f + head.right * 0.17f - head.up * 0.13f;
                panel.rotation = Quaternion.LookRotation(panel.position - head.position, head.up);
                panel.localScale = Vector3.one * 0.8f;
            }

            SetFill(hpFill, me.HealthFraction);
            hpRenderer.sharedMaterial = me.HealthFraction < 0.3f ? hpLow : hpGood;
            SetFill(guardFill, me.GuardBroken ? 0f : me.Guard / 100f);
            SetFill(specialFill, me.SpecialReady);
            SetFill(dashFill, me.DashReady);
            if (Time.time < nextText) return;
            nextText = Time.time + 0.1f;
            var s = me.Stats;
            hpText.text = "<b>" + Mathf.CeilToInt(me.Health) + "</b> / " + Mathf.RoundToInt(s.maxHealth);
            specialText.text = me.SpecialReady >= 1f ? "<b>X: " + s.special + "</b>" : s.special + " " + Mathf.CeilToInt((1f - me.SpecialReady) * s.specialCooldown) + "s";
            info.text = me.Down ? "<color=#ff6b5b>K.O. - back in " + Mathf.CeilToInt(me.RespawnIn) + "</color>"
                : s.name + "   " + me.KOs + " KO" + (me.Combo >= 2 ? "   <color=#ffcf4a>" + me.Combo + " combo</color>" : "") + (me.GuardBroken ? "   <color=#ff6b5b>GUARD BROKEN</color>" : "");
        }
    }
}
