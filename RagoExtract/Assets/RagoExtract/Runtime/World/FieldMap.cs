using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The raid's field map: a real top-down photo of the map taken when the raid starts, printed on a sheet of paper,
    /// with every extract marked in green and a red arrow for you. Open it with Y (VR) or M (desktop):
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

        Texture2D Photograph(float camY = 400f, float near = 1f, float far = 1400f)
        {
            const int Res = 1024;
            var camGo = new GameObject("MapCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = _half;
            cam.transform.SetPositionAndRotation(new Vector3(0, camY, 0), Quaternion.Euler(90, 0, 0));
            cam.nearClipPlane = near; cam.farClipPlane = far;   // tall cliffs and the giant maps / one floor of a building
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.18f, 0.27f, 0.33f);
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            cam.cullingMask = mask;
            var rt = new RenderTexture(Res, Res, 24);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            // photographed in plain daylight, whatever the weather / time of day
            Color aS = RenderSettings.ambientSkyColor, aE = RenderSettings.ambientEquatorColor, aG = RenderSettings.ambientGroundColor;
            var sunL = RenderSettings.sun; float sunI = sunL != null ? sunL.intensity : 0f;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.65f, 0.7f); RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f); RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.25f);
            if (sunL != null) sunL.intensity = Mathf.Max(sunI, 1f);
            // indoor maps: take the photo without the ceiling (its material is named *_ceiling)
            var hidden = new List<Renderer>();
            foreach (var r in transform.parent.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.sharedMaterial != null && r.sharedMaterial.name.EndsWith("_ceiling")) { r.enabled = false; hidden.Add(r); }
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
            RenderSettings.ambientSkyColor = aS; RenderSettings.ambientEquatorColor = aE; RenderSettings.ambientGroundColor = aG;
            if (sunL != null) sunL.intensity = sunI;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(camGo);
            return tex;
        }

        readonly List<Transform> _pages = new List<Transform>();
        readonly List<Object> _made = new List<Object>();   // the photos and their materials: freed with the map (they leaked every raid)
        void OnDestroy() { foreach (var o in _made) if (o != null) Destroy(o); _made.Clear(); }
        float[] _floors;
        int _page = -1;

        void Build(MapBase map, string title)
        {
            _sheet = new GameObject("Sheet").transform;
            _sheet.SetParent(transform, false);
            float h = Size / 2;
            _floors = map.MapFloors;
            int pages = _floors != null && _floors.Length > 0 ? _floors.Length : 1;
            for (int pg = 0; pg < pages; pg++)
            {
                var page = new GameObject("Page" + pg).transform;
                page.SetParent(_sheet, false);
                _pages.Add(page);
                // one floor: the camera just under that floor's ceiling, looking down to just below its floor
                var tex = _floors != null ? Photograph(_floors[pg] + map.MapFloorHeight - 0.05f, 0.01f, map.MapFloorHeight + 0.4f) : Photograph();
                if (tex != null && pg == 0) SaveShot(map.Id, tex);
                if (tex != null)
                {
                    var img = new MB().Quad(new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), Color.white, Vector2.one);
                    var pm = Mats.Unlit(null, Color.white, tex, Mats.BlendKind.Alpha, true);
                    _made.Add(tex); _made.Add(pm);
                    img.Build("Photo", page, pm);
                }
                string floorName = _floors != null && map.MapFloorNames != null && pg < map.MapFloorNames.Length ? "  -  " + map.MapFloorNames[pg] : "";
                UI.Text(page, title.ToUpper() + floorName + "  -  FIELD MAP", new Vector3(-h, h + 0.035f, -0.002f), 0.022f, UI.PaperInk, TextAnchor.MiddleLeft, FontStyle.Bold);
                BuildExtracts(map, page, pg);
            }
            BuildCommon(map);
        }

        // ---- the raid terminal shows these: the real photo of each map, kept from the last raid you played there
        public const int ShotVersion = 1;
        public static string ShotPath(string id) => System.IO.Path.Combine(Application.persistentDataPath, "mapshots", id + "_v" + ShotVersion + ".png");
        static void SaveShot(string id, Texture2D tex)
        {
            if (string.IsNullOrEmpty(id)) return;
            try
            {
                string path = ShotPath(id);
                if (System.IO.File.Exists(path)) return;   // the layouts are fixed: once is enough
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                // a small copy (256 px is plenty for the terminal's board)
                var rt = RenderTexture.GetTemporary(256, 256, 0);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var small = new Texture2D(256, 256, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); small.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                System.IO.File.WriteAllBytes(path, small.EncodeToPNG());
                Destroy(small);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] map photo for the terminal: " + e.Message); }
        }

        /// <summary>The saved photo of a map, or null if you haven't been there yet.</summary>
        public static Texture2D LoadShot(string id)
        {
            try
            {
                string path = ShotPath(id);
                if (!System.IO.File.Exists(path)) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!t.LoadImage(System.IO.File.ReadAllBytes(path))) { Destroy(t); return null; }
                t.wrapMode = TextureWrapMode.Clamp;
                return t;
            }
            catch { return null; }
        }

        /// <summary>Which page (floor) a height belongs to: the highest floor at or below it.</summary>
        int FloorOf(float y)
        {
            if (_floors == null) return 0;
            int best = _floors.Length - 1;
            for (int i = 0; i < _floors.Length; i++) if (y >= _floors[i] - 0.6f) { best = i; break; }
            return best;
        }

        void BuildExtracts(MapBase map, Transform page, int pg)
        {
            float h = Size / 2;
            // paper backing with a title strip at the top
            BuildExtractMarks(map, page, pg);
        }

        void BuildCommon(MapBase map)
        {
            float h = Size / 2;
            var paper = new MB();
            paper.Box(new Vector3(0, 0.025f, 0.004f), new Vector3(Size + 0.04f, Size + 0.1f, 0.004f), UI.Lin(new Color(0.86f, 0.82f, 0.7f)), 0f);
            paper.Build("Paper", _sheet, Mats.Lit("map_paper", Color.white, 0f, 0.1f, Mats.Noise, null, 0.25f, 0f));
            UI.Text(_sheet, "N", new Vector3(h - 0.01f, h + 0.045f, -0.002f), 0.024f, UI.StampRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(_sheet, "^", new Vector3(h - 0.01f, h + 0.025f, -0.002f), 0.024f, UI.StampRed, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(_sheet, "(o) extract", new Vector3(-h, -h - 0.02f, -0.002f), 0.016f, new Color(0.1f, 0.45f, 0.15f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Text(_sheet, "you", new Vector3(-h + 0.11f, -h - 0.02f, -0.002f), 0.016f, UI.StampRed, TextAnchor.MiddleLeft, FontStyle.Bold);
            BuildArrow();
        }

        void BuildExtractMarks(MapBase map, Transform page, int pg)
        {
            float h = Size / 2;
            // extracts (on a floor map: only the ones on this floor)
            var green = Mats.Unlit("map_extract", new Color(0.1f, 0.7f, 0.2f, 1f));
            var dots = new List<Vector3>();
            foreach (var e in map.Extracts) if (e != null && FloorOf(e.transform.position.y) == pg) dots.Add(ToSheet(e.transform.position));
            var taken = new List<Rect>();
            foreach (var e in map.Extracts)
            {
                if (e == null || FloorOf(e.transform.position.y) != pg) continue;
                var p = ToSheet(e.transform.position);
                var m = new MB();
                m.Sector(Vector3.zero, Quaternion.identity, 0.009f, 0, 360, 18, Color.white);
                var dot = m.Build("Extract", page, green);
                dot.transform.localPosition = p + new Vector3(0, 0, -0.003f);
                // ring around it
                var ring = new MB();
                for (int i = 0; i < 16; i++) { float a = i * 22.5f; ring.Box(Quaternion.Euler(0, 0, a) * new Vector3(0.016f, 0, 0), new Vector3(0.003f, 0.006f, 0.0005f), Color.white, 0f, 0, Quaternion.Euler(0, 0, a)); }
                var rg = ring.Build("Ring", page, green);
                rg.transform.localPosition = p + new Vector3(0, 0, -0.003f);
                // the name on one line, when it opens underneath; put beside the dot wherever it doesn't run into
                // another extract or another label (right, left, above, below), staying on the sheet
                int om = Mathf.RoundToInt(e.OpensAt / 60f);
                string name = e.Name.ToUpper(), when = "opens " + om + " min";
                const float sz = 0.015f;
                float lw = Mathf.Max(name.Length, when.Length) * sz * 0.62f, lh = sz * 2.3f;
                Vector3 best = Vector3.zero; TextAnchor bestA = TextAnchor.MiddleLeft; Rect bestR = default; float bestScore = float.MaxValue;
                var opts = new (Vector2 off, TextAnchor a, Rect r)[]
                {
                    (new Vector2(0.022f, 0), TextAnchor.MiddleLeft, new Rect(p.x + 0.022f, p.y - lh / 2, lw, lh)),
                    (new Vector2(-0.022f, 0), TextAnchor.MiddleRight, new Rect(p.x - 0.022f - lw, p.y - lh / 2, lw, lh)),
                    (new Vector2(0, 0.024f + lh / 2), TextAnchor.MiddleCenter, new Rect(p.x - lw / 2, p.y + 0.024f, lw, lh)),
                    (new Vector2(0, -0.024f - lh / 2), TextAnchor.MiddleCenter, new Rect(p.x - lw / 2, p.y - 0.024f - lh, lw, lh)),
                };
                foreach (var o in opts)
                {
                    float score = 0;
                    foreach (var t in taken) if (t.Overlaps(o.r)) score += 10;
                    foreach (var d in dots) if (d != p && o.r.Contains(new Vector2(d.x, d.y)) || d != p && Vector2.Distance(o.r.center, d) < 0.03f) score += 10;
                    foreach (var d in dots) if (d != p && new Rect(o.r.x - 0.018f, o.r.y - 0.018f, o.r.width + 0.036f, o.r.height + 0.036f).Contains(new Vector2(d.x, d.y))) score += 5;
                    float outX = Mathf.Max(0, o.r.xMax - h) + Mathf.Max(0, -h - o.r.xMin), outY = Mathf.Max(0, o.r.yMax - h) + Mathf.Max(0, -h - o.r.yMin);
                    score += (outX + outY) * 200f;
                    if (score < bestScore) { bestScore = score; best = o.off; bestA = o.a; bestR = o.r; }
                }
                taken.Add(bestR);
                var ink = new Color(0.05f, 0.35f, 0.1f);
                var lbl = UI.Text(page, name + "\n<size=48>" + when + "</size>", p + best + new Vector3(0, 0, -0.003f), sz, ink, bestA, FontStyle.Bold);
                lbl.richText = true;
            }
        }

        void BuildArrow()
        {
            float h = Size / 2;
            // you
            _arrow = new GameObject("You").transform; _arrow.SetParent(_sheet, false);
            var arrow = new MB();
            // an arrowhead pointing the way you face: tip ahead, wide end behind (a wedge drawn from the tip back)
            arrow.Sector(new Vector3(0, 0.013f, 0), Quaternion.Euler(0, 0, 270 - 20), 0.024f, 0, 40, 6, Color.white);
            arrow.Build("Arrow", _arrow, Mats.Unlit("map_you", new Color(0.85f, 0.1f, 0.08f, 1f)));
            var youLegend = new MB().Sector(new Vector3(0, 0.008f, 0), Quaternion.Euler(0, 0, 270 - 20), 0.015f, 0, 40, 6, Color.white).Build("YouLegend", _sheet, Mats.Unlit("map_you", new Color(0.85f, 0.1f, 0.08f, 1f)));
            youLegend.transform.localPosition = new Vector3(-h + 0.1f, -h - 0.025f, -0.003f);
            _sheet.gameObject.SetActive(false);
        }

        Vector3 ToSheet(Vector3 world) => new Vector3(world.x / (_half * 2) * Size, world.z / (_half * 2) * Size, 0);

        void Update()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null || _sheet == null) return;
            if (!_hinted && Time.timeSinceLevelLoad > 0) { _hinted = true; p.Toast(XRInputs.VR ? "Your map: press Y" : "Your map: press M"); }
            // Y on the left controller (unless the left hand is holding a gun - then Y works the gun)
            bool toggle = XRInputs.VR ? XRInputs.Left.SecondaryDown && !(p.HandL.Held is Gun && !p.HandL.HeldAsSecondary) && !(p.HandL.Held == null && p.Gear.NearMagPouch(p.HandL.transform.position)) : Desktop.MapKey;
            if (toggle) { _open = !_open; _sheet.gameObject.SetActive(_open); Sfx.Play(Synth.Grab(1), p.Head.position, 0.3f, 1.4f, 0f, 3f); }
            if (!_open) return;
            if (p.Dead) { _open = false; _sheet.gameObject.SetActive(false); return; }
            Place(p);
        }

        void OnEnable() { PlayerRig.AfterRenderSnap += AfterSnap; }
        void OnDisable() { PlayerRig.AfterRenderSnap -= AfterSnap; }
        void AfterSnap() { var p = RagoGame.I != null ? RagoGame.I.Player : null; if (p != null && _open && _sheet != null) Place(p); }   // again on the freshly snapped hand

        void Place(PlayerRig p)
        {
            // place it: just above the left hand (VR) or in front of your face (desktop), always turned towards your eyes
            Vector3 head = p.Head.position;
            Vector3 pos;
            if (XRInputs.VR) pos = p.HandL.transform.position + Vector3.up * 0.12f;
            else { var f = p.Head.forward; pos = head + f * 0.55f - p.Head.up * 0.05f; }
            _sheet.position = pos;
            var look = pos - head;
            if (look.sqrMagnitude > 0.0001f) _sheet.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            // the page for the floor you're on
            int want = FloorOf(p.transform.position.y);
            if (want != _page) { _page = want; for (int i = 0; i < _pages.Count; i++) _pages[i].gameObject.SetActive(i == want); }
            // you
            var me = ToSheet(p.transform.position);
            me.x = Mathf.Clamp(me.x, -Size / 2, Size / 2); me.y = Mathf.Clamp(me.y, -Size / 2, Size / 2);
            _arrow.localPosition = me + new Vector3(0, 0, -0.004f);
            float yaw = p.Head.eulerAngles.y;
            _arrow.localRotation = Quaternion.Euler(0, 0, -yaw);
        }
    }
}
