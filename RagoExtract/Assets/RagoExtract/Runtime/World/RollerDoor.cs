using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The Market's garage door: a slatted roller door that rolls up into its drum when a CONCERT is on (the stage is in
    /// the garage behind it) and comes back down afterwards. Solid while it's shut.
    /// </summary>
    public class RollerDoor : MonoBehaviour
    {
        Transform _curtain;
        float _open, _h;

        public static RollerDoor Create(Transform parent, Vector3 localPos, float width, float height)
        {
            var go = new GameObject("RollerDoor");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var d = go.AddComponent<RollerDoor>();
            d._h = height;
            // the curtain hangs from its top edge, so rolling up = squashing it towards the drum
            d._curtain = new GameObject("Curtain").transform;
            d._curtain.SetParent(go.transform, false);
            d._curtain.localPosition = new Vector3(0, height, 0);
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, -height / 2, 0), new Vector3(width, height, 0.08f), new Color(0.42f, 0.36f, 0.22f), 0f);
            for (int i = 0; i < 16; i++) mb.Sub(1).Box(new Vector3(0, -height + 0.15f + i * 0.3f, 0.045f), new Vector3(width, 0.03f, 0.02f), new Color(0.3f, 0.26f, 0.16f), 0f);
            mb.Sub(1).Box(new Vector3(0, -height + 0.05f, 0.05f), new Vector3(width, 0.1f, 0.05f), new Color(0.2f, 0.2f, 0.2f), 0f);   // bottom rail
            var m = mb.Build("Slats", d._curtain, Maps.Metal, Maps.Metal);
            var col = d._curtain.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, -height / 2, 0); col.size = new Vector3(width, height, 0.12f);
            // the drum box above the opening
            new MB().Box(new Vector3(0, height + 0.3f, 0.1f), new Vector3(width + 0.3f, 0.6f, 0.5f), new Color(0.32f, 0.3f, 0.26f), 0.03f).Build("Drum", go.transform, Maps.Metal);
            return d;
        }

        void Update()
        {
            bool want = LiveEvent.Is(LiveEvent.Concert);
            float t = Mathf.MoveTowards(_open, want ? 1f : 0f, Time.deltaTime / 4f);   // about 4 s to roll up
            if (Mathf.Abs(t - _open) < 0.0001f && _curtain.localScale.y != 0f) return;
            bool started = (t > 0f) != (_open > 0f);
            _open = t;
            float k = Mathf.Lerp(1f, 0.03f, Mathf.SmoothStep(0, 1, _open));
            _curtain.localScale = new Vector3(1f, k, 1f);
            if (started) Sfx.Play(Synth.SlideBack(), transform.position + Vector3.up * _h, 0.5f, 0.35f, 2f, 25f);
        }
    }
}
