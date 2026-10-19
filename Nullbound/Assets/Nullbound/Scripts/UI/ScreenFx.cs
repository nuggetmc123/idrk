using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Big messages that float in front of your eyes (K.O.!, GROUND SLAM!, room status) and a quick colour flash
    /// when you get hit or teleport. The text follows your view gently instead of being glued to it (more
    /// comfortable in VR).
    /// </summary>
    public class ScreenFx : MonoBehaviour
    {
        static ScreenFx instance;
        TextMeshPro title, sub;
        Transform panel;
        Renderer flash;
        Material flashMat;
        float hideAt, flashUntil, flashStart;
        Color flashColor;

        static ScreenFx Get()
        {
            if (instance != null) return instance;
            var rig = VRRig.Instance;
            if (rig == null || rig.head == null) return null;
            instance = new GameObject("ScreenFx").AddComponent<ScreenFx>();
            instance.Build(rig.head);
            return instance;
        }

        void Build(Transform head)
        {
            panel = new GameObject("Messages").transform;
            title = NBUI.Label(panel, "", Vector3.zero, 6f, Color.white, new Vector2(3f, 0.6f));
            sub = NBUI.Label(panel, "", new Vector3(0f, -0.32f, 0f), 2.2f, new Color(0.85f, 0.9f, 1f), new Vector2(3f, 0.3f));
            panel.gameObject.SetActive(false);
            // A sphere around your head that briefly tints your view (hit flash).
            var f = NBUI.Part(head, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.4f, ComfortVignette.SolidMaterial ?? NBUI.Glow(Color.red), default, "HitFlash");
            flash = f.GetComponent<Renderer>();
            flash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.enabled = false;
            flashMat = flash.material;
            // Inside-out so you see it from within.
            f.localScale = new Vector3(-0.4f, 0.4f, 0.4f);
        }

        /// <summary>A big line of text (and a smaller one under it) in front of you for a few seconds.</summary>
        public static void Message(string big, string small, float seconds)
        {
            var s = Get();
            if (s == null) return;
            s.title.text = "<b>" + big + "</b>";
            s.sub.text = small;
            s.hideAt = Time.time + seconds;
            s.panel.gameObject.SetActive(true);
            s.Place(true);
        }

        public static void Hit(bool blocked) => Flash(blocked ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.1f, 0.05f), 0.15f);

        public static void Flash(Color c, float seconds = 0.2f)
        {
            var s = Get();
            if (s == null || s.flash == null) return;
            s.flashColor = c;
            s.flashStart = Time.time;
            s.flashUntil = Time.time + seconds;
            s.flash.enabled = true;
        }

        void Place(bool snap)
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.head == null) return;
            Vector3 fwd = rig.head.forward;
            fwd.y = Mathf.Clamp(fwd.y, -0.3f, 0.3f);
            Vector3 want = rig.head.position + fwd.normalized * 2.2f + Vector3.up * 0.25f;
            panel.position = snap ? want : Vector3.Lerp(panel.position, want, 1f - Mathf.Exp(-4f * Time.deltaTime));
            panel.rotation = Quaternion.LookRotation(panel.position - rig.head.position);
        }

        void LateUpdate()
        {
            if (panel.gameObject.activeSelf)
            {
                if (Time.time > hideAt) panel.gameObject.SetActive(false);
                else Place(false);
            }
            if (flash != null && flash.enabled)
            {
                float t = Mathf.InverseLerp(flashStart, flashUntil, Time.time);
                var c = flashColor;
                c.a = 0.45f * (1f - t);
                flashMat.color = c;
                if (Time.time >= flashUntil) flash.enabled = false;
            }
        }
    }
}
