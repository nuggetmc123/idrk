using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Comfort setting: darkens the edges of your view while the stick moves you, when you dash and when you get
    /// launched, which cuts down motion sickness a lot. Turn it off in Menu > Settings. The builder puts it on the
    /// camera with a soft see-through ring material (so its shader is included in builds).
    /// </summary>
    public class ComfortVignette : MonoBehaviour
    {
        /// <summary>Plain see-through material (also used for the hit flash).</summary>
        public static Material SolidMaterial;

        public Material ringMaterial;
        public Material solidMaterial;
        static float kick;
        Renderer ring;
        Material mat;
        Vector3 lastBody;
        float strength;

        /// <summary>A short burst (dash, launch, teleport).</summary>
        public static void Kick(float amount) { kick = Mathf.Max(kick, amount); }

        void Awake()
        {
            if (solidMaterial != null) SolidMaterial = solidMaterial;
            if (ringMaterial == null) { enabled = false; return; }
            var q = NBUI.Part(transform, PrimitiveType.Quad, new Vector3(0f, 0f, 0.12f), new Vector3(0.36f, 0.28f, 1f), ringMaterial, default, "Vignette");
            ring = q.GetComponent<Renderer>();
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat = ring.material;
            ring.enabled = false;
        }

        void LateUpdate()
        {
            if (ring == null) return;
            float want = 0f;
            var me = Fighter.Local;
            if (me != null && me.IsReady)
            {
                Vector3 v = (me.transform.position - lastBody) / Mathf.Max(Time.deltaTime, 0.001f);
                lastBody = me.transform.position;
                float speed = new Vector3(v.x, 0f, v.z).magnitude;
                if (speed < 30f) want = Mathf.Clamp01((speed - 0.5f) / 5f) * 0.7f;
            }
            want = Mathf.Max(want, kick);
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 2.5f);
            if (!GameSettings.Vignette || !VRRig.IsXR) want = 0f;
            strength = Mathf.MoveTowards(strength, want, Time.deltaTime * 4f);
            ring.enabled = strength > 0.02f;
            if (ring.enabled)
            {
                var c = mat.color;
                c.a = strength;
                mat.color = c;
            }
        }
    }
}
