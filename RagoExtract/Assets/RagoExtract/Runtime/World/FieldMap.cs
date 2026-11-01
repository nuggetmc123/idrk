using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The raid's field map: a real top-down photo of the map taken when the raid starts, printed on a sheet of paper,
    /// with every extract marked in green and a red arrow for you. Open it with the LEFT MENU button (VR) or M (desktop):
    /// in VR it floats just above your left hand, turned towards your eyes; on desktop it comes up in front of you.
    /// </summary>
    public class FieldMap : MonoBehaviour
    {
        const float Size = 0.42f;   // printed area, metres
        Transform _sheet, _arrow;
        float _half;
        bool _open;
        static bool _hinted;

        public static FieldMap Create(MapBase map, string title)
        {
            var go = new GameObject("FieldMap");
            go.transform.SetParent(map.transform, false);
            var f = go.AddComponent<FieldMap>();
            f._half = map.Half > 0 ? map.Half : Mathf.Max(map.NavBounds.extents.x, map.NavBounds.extents.z);
            f.Build(map, title);
            return f;
        }

        Texture2D Photograph()
        {
            const int Res = 1024;
            var camGo = new GameObject("MapCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = _half;
            cam.transform.SetPositionAndRotation(new Vector3(0, 400, 0), Quaternion.Euler(90, 0, 0));
            cam.nearClipPlane = 1f; cam.farClipPlane = 700f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.18f, 0.27f, 0.33f);
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            cam.cullingMask = mask;
            var rt = new RenderTexture(Res, Res, 24);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            Texture2D tex = null;
            try
            {
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(Res, Res, TextureFormat.RGB24, true);
                tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
                // print it on paper: wash the photo towards a warm paper tone, a bit flatter, like an old field map
                var px = tex.GetPixels32();
                var paper = new Color(0.86f, 0.82f, 0.7f);
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    c = Color.Lerp(c, new Color(lum, lum, lum), 0.35f);
                    px[i] = Color.Lerp(c * 1.15f, paper, 0.32f);
                }
                tex.SetPixels32(px);
                tex.Apply(true);
                RenderTexture.active = prev;
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago Extract] field map photo failed: " + e.Message); }
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(camGo);
            return tex;
        }

        void Build(MapBase map, string title)
        {
            var tex = Photograph();
            _sheet = new GameObject("Sheet").transform;
            _sheet.SetParent(transform, false);
            float h = Size / 2;
            // paper backing with a title strip at the top
            var paper = new MB();
            paper.Box(new Vector3(0, 0.025f, 0.004f), new Vector3(Size + 0.04f, Size + 0.1f, 0.004f), UI.Lin(new Color(0.86f, 0.82f, 0.7f)), 0f);
            paper.Build("Paper", _sheet, Mats.Lit("map_paper", Color.white, 0f, 0.1f, Mats.Noise, null, 0.25f, 0f));
            if (tex != null)
            {
                var img = new MB().Quad(new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), Color.white, Vector2.one);
                img.Build("Photo", _sheet, Mats.Unlit(null, Color.white, tex, Mats.BlendKind.Alpha, true));
            }
            UI.Text(_sheet, title.ToUpper() + "  -  FIELD MAP", new Vector3(-h, h + 0.035f, -0.002f), 0.022f, UI.PaperInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(_sheet, "N", new Vector3(h - 0.01f, h + 0.045f, -0.002f), 0.024f, UI.StampRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(_sheet, "^", new Vector3(h - 0.01f, h + 0.025f, -0.002f), 0.024f, UI.StampRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(_sheet, "(o) extract", new Vector3(-h, -h - 0.02f, -0.002f), 0.016f, new Color(0.1f, 0.45f, 0.15f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(_sheet, "you", new Vector3(-h + 0.11f, -h - 0.02f, -0.002f), 0.016f, UI.StampRed, TextAnchor.MiddleLeft, FontStyle.Bold);
            // extracts
            var green = Mats.Unlit("map_extract", new Color(0.1f, 0.7f, 0.2f, 1f));
            foreach (var e in map.Extracts)
            {
                if (e == null) continue;
                var p = ToSheet(e.transform.position);
                var m = new MB();
                m.Sector(Vector3.zero, Quaternion.identity, 0.009f, 0, 360, 18, Color.white);
                var dot = m.Build("Extract", _sheet, green);
                dot.transform.localPosition = p + new Vector3(0, 0, -0.003f);
                // ring around it
                var ring = new MB();
                for (int i = 0; i < 16; i++) { float a = i * 22.5f; ring.Box(Quaternion.Euler(0, 0, a) * new Vector3(0.016f, 0, 0), new Vector3(0.003f, 0.006f, 0.0005f), Color.white, 0f, 0, Quaternion.Euler(0, 0, a)); }
                var rg = ring.Build("Ring", _sheet, green);
                rg.transform.localPosition = p + new Vector3(0, 0, -0.003f);
                bool right = p.x < h * 0.4f;
                UI.Text(_sheet, e.Name.ToUpper(), p + new Vector3(right ? 0.022f : -0.022f, 0, -0.003f), 0.017f, new Color(0.05f, 0.35f, 0.1f), right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, FontStyle.Bold);
            }
            // you
            _arrow = new GameObject("You").transform; _arrow.SetParent(_sheet, false);
            var arrow = new MB();
            arrow.Sector(Vector3.zero, Quaternion.Euler(0, 0, 90 - 22), 0.018f, 0, 44, 6, Color.white);
            arrow.Build("Arrow", _arrow, Mats.Unlit("map_you", new Color(0.85f, 0.1f, 0.08f, 1f)));
            var youLegend = new MB().Sector(Vector3.zero, Quaternion.Euler(0, 0, 90 - 22), 0.012f, 0, 44, 6, Color.white).Build("YouLegend", _sheet, Mats.Unlit("map_you", new Color(0.85f, 0.1f, 0.08f, 1f)));
            youLegend.transform.localPosition = new Vector3(-h + 0.1f, -h - 0.025f, -0.003f);
            _sheet.gameObject.SetActive(false);
        }

        Vector3 ToSheet(Vector3 world) => new Vector3(world.x / (_half * 2) * Size, world.z / (_half * 2) * Size, 0);

        void Update()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null || _sheet == null) return;
            if (!_hinted && Time.timeSinceLevelLoad > 0) { _hinted = true; p.Toast(XRInputs.VR ? "Your map: press the LEFT MENU button" : "Your map: press M"); }
            bool toggle = XRInputs.VR ? XRInputs.Left.MenuDown : Desktop.MapKey;
            if (toggle) { _open = !_open; _sheet.gameObject.SetActive(_open); Sfx.Play(Synth.Grab(1), p.Head.position, 0.3f, 1.4f, 0f, 3f); }
            if (!_open) return;
            if (p.Dead) { _open = false; _sheet.gameObject.SetActive(false); return; }
            // place it: just above the left hand (VR) or in front of your face (desktop), always turned towards your eyes
            Vector3 head = p.Head.position;
            Vector3 pos;
            if (XRInputs.VR) pos = p.HandL.transform.position + Vector3.up * 0.12f;
            else { var f = p.Head.forward; pos = head + f * 0.55f - p.Head.up * 0.05f; }
            _sheet.position = pos;
            var look = pos - head;
            if (look.sqrMagnitude > 0.0001f) _sheet.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            // you
            var me = ToSheet(p.transform.position);
            me.x = Mathf.Clamp(me.x, -Size / 2, Size / 2); me.y = Mathf.Clamp(me.y, -Size / 2, Size / 2);
            _arrow.localPosition = me + new Vector3(0, 0, -0.004f);
            float yaw = p.Head.eulerAngles.y;
            _arrow.localRotation = Quaternion.Euler(0, 0, -yaw);
        }
    }
}
