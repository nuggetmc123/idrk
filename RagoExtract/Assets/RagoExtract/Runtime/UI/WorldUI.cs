using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>What a world panel is physically made of.</summary>
    public enum Skin { Steel, Crt, Chalk, Paper }
    /// <summary>What a world button physically is.</summary>
    public enum BtnStyle { Plate, Key, Tag, Stamp, Crt }

    /// <summary>World-space UI toolkit: panels, crisp 3D text, and buttons you laser-point (VR) or click (desktop).
    /// Panels are physical things - bolted steel plates, CRT terminals, chalkboards, clipboards - not floating glass.</summary>
    public static class UI
    {
        public static readonly Color Accent = new Color(1f, 0.62f, 0.18f);
        public static readonly Color PanelCol = new Color(0.17f, 0.175f, 0.18f, 1f);
        public static readonly Color Dim = new Color(0.6f, 0.62f, 0.64f);
        // inks
        public static readonly Color CrtGreen = new Color(0.45f, 1f, 0.55f), CrtDim = new Color(0.22f, 0.6f, 0.3f);
        public static readonly Color Chalk = new Color(0.93f, 0.93f, 0.88f), ChalkDim = new Color(0.66f, 0.68f, 0.64f), ChalkYellow = new Color(1f, 0.88f, 0.45f);
        public static readonly Color PaperInk = new Color(0.1f, 0.1f, 0.13f), PaperDim = new Color(0.32f, 0.32f, 0.36f), StampRed = new Color(0.72f, 0.1f, 0.08f);
        public static readonly Color Stencil = new Color(0.95f, 0.8f, 0.3f);

        /// <summary>Vertex colours are linear; palette colours here are picked by eye (sRGB).</summary>
        public static Color Lin(Color c) => new Color(Mathf.Pow(Mathf.Clamp01(c.r), 2.2f), Mathf.Pow(Mathf.Clamp01(c.g), 2.2f), Mathf.Pow(Mathf.Clamp01(c.b), 2.2f), 1f);

        /// <summary>Simple word wrap for TextMesh (which can't wrap by itself).</summary>
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder();
            foreach (var para in text.Split('\n'))
            {
                int line = 0;
                foreach (var word in para.Split(' '))
                {
                    if (line > 0 && line + 1 + word.Length > maxChars) { sb.Append('\n'); line = 0; }
                    else if (line > 0) { sb.Append(' '); line++; }
                    sb.Append(word); line += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        public static Transform Panel(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector2 size, Color? col = null, bool frame = true, Skin skin = Skin.Steel)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos; root.localRotation = localRot;
            float w = size.x, h = size.y;
            var rng = new Rng(Mathf.RoundToInt(w * 1000 + h * 37));
            var mb = new MB(3);
            switch (skin)
            {
                case Skin.Crt:
                {
                    // chunky plastic bezel around a dark phosphor screen with scanlines
                    Color bez = Lin(new Color(0.16f, 0.16f, 0.15f));
                    float b = 0.05f;
                    mb.Sub(0).Box(new Vector3(0, h / 2 + b / 2, 0.02f), new Vector3(w + b * 2, b, 0.05f), bez, 0.012f, 2);
                    mb.Sub(0).Box(new Vector3(0, -h / 2 - b / 2, 0.02f), new Vector3(w + b * 2, b, 0.05f), bez, 0.012f, 2);
                    mb.Sub(0).Box(new Vector3(-w / 2 - b / 2, 0, 0.02f), new Vector3(b, h, 0.05f), bez, 0.012f, 2);
                    mb.Sub(0).Box(new Vector3(w / 2 + b / 2, 0, 0.02f), new Vector3(b, h, 0.05f), bez, 0.012f, 2);
                    mb.Sub(1).Box(new Vector3(0, 0, 0.012f), new Vector3(w, h, 0.02f), Color.white, 0f);
                    for (float y = -h / 2 + 0.006f; y < h / 2; y += 0.011f) mb.Sub(2).Box(new Vector3(0, y, -0.0006f), new Vector3(w, 0.0035f, 0.0004f), Lin(new Color(0.02f, 0.05f, 0.03f)), 0f);
                    mb.Build("bg", root, Mats.Lit("ui_bezel", Color.white, 0.1f, 0.3f, Mats.Noise, null, 0.3f, 0.1f),
                        Mats.Lit("ui_crt", new Color(0.02f, 0.07f, 0.035f), 0f, 0.85f, null, null, 0, 0, false, 0.5f, new Color(0.01f, 0.06f, 0.02f)),
                        Mats.Lit("ui_scan", Color.white, 0f, 0.8f, null, null, 0, 0));
                    break;
                }
                case Skin.Chalk:
                {
                    // slate in a wooden frame with a chalk ledge and old smudges
                    Color wood = Lin(new Color(0.5f, 0.34f, 0.2f));
                    float b = 0.045f;
                    mb.Sub(0).Box(new Vector3(0, h / 2 + b / 2, 0.012f), new Vector3(w + b * 2, b, 0.035f), wood, 0.008f, 1);
                    mb.Sub(0).Box(new Vector3(0, -h / 2 - b / 2, 0.012f), new Vector3(w + b * 2, b, 0.035f), wood, 0.008f, 1);
                    mb.Sub(0).Box(new Vector3(-w / 2 - b / 2, 0, 0.012f), new Vector3(b, h, 0.035f), wood, 0.008f, 1);
                    mb.Sub(0).Box(new Vector3(w / 2 + b / 2, 0, 0.012f), new Vector3(b, h, 0.035f), wood, 0.008f, 1);
                    mb.Sub(0).Box(new Vector3(0, -h / 2 - b, -0.02f), new Vector3(w * 0.9f, 0.012f, 0.05f), wood, 0.004f); // ledge
                    mb.Sub(1).Box(new Vector3(0, 0, 0.01f), new Vector3(w, h, 0.02f), Lin(new Color(0.16f, 0.19f, 0.17f)), 0f);
                    // faint wiped-off chalk: many soft, overlapping smears barely lighter than the slate
                    for (int k = 0; k < 26; k++)
                        mb.Sub(1).Box(new Vector3(rng.Range(-w * 0.45f, w * 0.45f), rng.Range(-h * 0.42f, h * 0.42f), -0.0003f - k * 0.00001f), new Vector3(rng.Range(0.05f, w * 0.22f), rng.Range(0.012f, 0.05f), 0.0002f),
                            Lin(new Color(0.175f, 0.205f, 0.185f)), 0f, 0, Quaternion.Euler(0, 0, rng.Range(-25f, 25f)));
                    mb.Sub(2).Box(new Vector3(w * 0.3f, -h / 2 - b + 0.012f, -0.03f), new Vector3(0.07f, 0.012f, 0.012f), Color.white, 0.004f); // a stick of chalk
                    mb.Build("bg", root, Mats.Lit("ui_wood", Color.white, 0f, 0.3f, Mats.Wood, Mats.WoodN, 0.4f, 0.2f), Mats.Lit("ui_slate", Color.white, 0f, 0.12f, Mats.Noise, null, 0.5f, 0.1f), Mats.Lit("ui_chalk", Color.white, 0f, 0.1f));
                    break;
                }
                case Skin.Paper:
                {
                    // clipboard: hardboard backing, a sheet of paper with ruled lines, a steel clip
                    Color board = Lin(new Color(0.52f, 0.38f, 0.24f));
                    mb.Sub(0).Box(new Vector3(0, 0, 0.012f), new Vector3(w + 0.06f, h + 0.1f, 0.01f), board, 0.012f, 1);
                    mb.Sub(1).Box(new Vector3(0, -0.01f, 0.004f), new Vector3(w, h, 0.003f), Lin(new Color(0.84f, 0.82f, 0.76f)), 0f, 0, Quaternion.Euler(0, 0, rng.Range(-0.8f, 0.8f)));
                    for (float y = h / 2 - 0.12f; y > -h / 2 + 0.03f; y -= 0.045f) mb.Sub(1).Box(new Vector3(0.01f, y, 0.0022f), new Vector3(w * 0.9f, 0.0018f, 0.0004f), Lin(new Color(0.72f, 0.8f, 0.9f)), 0f);
                    mb.Sub(2).Box(new Vector3(0, h / 2 + 0.03f, 0.0f), new Vector3(0.22f, 0.06f, 0.02f), Color.white, 0.006f, 1);
                    mb.Sub(2).Cyl(new Vector3(-0.08f, h / 2 + 0.03f, -0.012f), new Vector3(0.08f, h / 2 + 0.03f, -0.012f), 0.008f, 0.008f, 8, Color.white);
                    mb.Build("bg", root, Mats.Lit("ui_board", Color.white, 0f, 0.3f, Mats.Wood, null, 0.3f, 0.1f), Mats.Lit("ui_paper", Color.white, 0f, 0.08f, Mats.Noise, null, 0.12f, 0f), Mats.Lit("ui_clip", new Color(0.7f, 0.7f, 0.72f), 0.9f, 0.6f, Mats.Metal, null, 0.1f, 0.2f));
                    break;
                }
                default:
                {
                    // matte steel plate, bolted on
                    Color c = Lin(col ?? PanelCol);
                    if (frame) mb.Sub(0).Box(new Vector3(0, 0, 0.022f), new Vector3(w + 0.03f, h + 0.03f, 0.01f), Lin(new Color(0.09f, 0.09f, 0.095f)), 0.006f);
                    mb.Sub(0).Box(new Vector3(0, 0, 0.01f), new Vector3(w, h, 0.02f), c, 0.008f, 1);
                    if (frame) foreach (var sx in new[] { -1, 1 }) foreach (var sy in new[] { -1, 1 })
                                mb.Sub(1).Cyl(new Vector3(sx * (w / 2 - 0.025f), sy * (h / 2 - 0.025f), 0.0f), new Vector3(sx * (w / 2 - 0.025f), sy * (h / 2 - 0.025f), -0.005f), 0.009f, 0.0075f, 8, Color.white);
                    mb.Build("bg", root, Mats.Lit("ui_steel", Color.white, 0.45f, 0.3f, Mats.Metal, null, 0.35f, 0.35f), Mats.Lit("ui_bolt", new Color(0.45f, 0.45f, 0.46f), 0.9f, 0.5f));
                    break;
                }
            }
            return root;
        }

        public static TextMesh Text(Transform parent, string text, Vector3 localPos, float size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + new Vector3(0, 0, -0.002f);
            var tm = go.AddComponent<TextMesh>();
            var f = Compat.DefaultFont;
            if (f != null) { tm.font = f; go.GetComponent<MeshRenderer>().sharedMaterial = Compat.TextMaterial; }
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size * 0.1f;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft ? TextAlignment.Left : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight ? TextAlignment.Right : TextAlignment.Center;
            tm.color = col;
            tm.fontStyle = style;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        public static WorldButton Button(Transform parent, string label, Vector3 localPos, Vector2 size, System.Action onClick, Color? col = null, BtnStyle style = BtnStyle.Plate, Color? text = null)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + new Vector3(0, 0, -0.012f);
            var b = go.AddComponent<WorldButton>();
            Color def = style == BtnStyle.Tag ? new Color(0.9f, 0.86f, 0.74f) : style == BtnStyle.Stamp ? new Color(0.93f, 0.91f, 0.85f) : style == BtnStyle.Crt ? new Color(0.03f, 0.1f, 0.05f) : new Color(0.22f, 0.23f, 0.24f);
            b.Init(label, size, onClick, col ?? def, style, text);
            return b;
        }
    }

    public class WorldButton : MonoBehaviour
    {
        public System.Action OnClick;
        public TextMesh Label;
        public bool Interactable = true;
        public BtnStyle Style;
        MeshRenderer _bg;
        Transform _cap;
        Color _base;
        float _hover, _press, _travel;
        Material _mat;

        public void Init(string label, Vector2 size, System.Action onClick, Color col, BtnStyle style = BtnStyle.Plate, Color? text = null)
        {
            OnClick = onClick;
            Style = style;
            _base = col;
            float w = size.x, h = size.y;
            Color ink = text ?? (style == BtnStyle.Tag ? UI.PaperInk : style == BtnStyle.Stamp ? UI.StampRed : style == BtnStyle.Crt ? UI.CrtGreen : new Color(0.92f, 0.92f, 0.88f));
            // fixed parts (not tinted): bezel for keys, string for tags, ink border for stamps, phosphor outline for CRT
            var fixedMb = new MB();
            bool hasFixed = false;
            var cap = new MB();
            switch (style)
            {
                case BtnStyle.Key:
                    fixedMb.Box(new Vector3(0, 0, 0.012f), new Vector3(w + 0.03f, h + 0.03f, 0.02f), UI.Lin(new Color(0.08f, 0.08f, 0.085f)), 0.008f, 1); hasFixed = true;
                    cap.Box(new Vector3(0, 0, -0.008f), new Vector3(w, h, 0.03f), Color.white, Mathf.Min(0.014f, h * 0.3f), 2);
                    _travel = 0.012f;
                    break;
                case BtnStyle.Tag:
                    cap.Box(Vector3.zero, new Vector3(w, h, 0.003f), Color.white, 0.006f, 1);
                    fixedMb.Cyl(new Vector3(-w / 2 + 0.018f, 0, -0.003f), new Vector3(-w / 2 + 0.018f, 0, 0.003f), 0.007f, 0.007f, 10, UI.Lin(new Color(0.55f, 0.5f, 0.42f)));
                    fixedMb.Box(new Vector3(-w / 2 + 0.01f, h * 0.35f, -0.002f), new Vector3(0.003f, h * 0.7f, 0.002f), UI.Lin(new Color(0.75f, 0.7f, 0.6f)), 0f, 0, Quaternion.Euler(0, 0, 30)); // string
                    hasFixed = true; _travel = 0.006f;
                    break;
                case BtnStyle.Stamp:
                    cap.Box(new Vector3(0, 0, 0.001f), new Vector3(w, h, 0.002f), Color.white, 0f);
                    foreach (var y in new[] { -h / 2 + 0.006f, h / 2 - 0.006f }) fixedMb.Box(new Vector3(0, y, -0.0015f), new Vector3(w - 0.004f, 0.006f, 0.001f), UI.Lin(UI.StampRed), 0f);
                    foreach (var x in new[] { -w / 2 + 0.006f, w / 2 - 0.006f }) fixedMb.Box(new Vector3(x, 0, -0.0015f), new Vector3(0.006f, h - 0.004f, 0.001f), UI.Lin(UI.StampRed), 0f);
                    hasFixed = true; _travel = 0.002f;
                    break;
                case BtnStyle.Crt:
                    cap.Box(new Vector3(0, 0, -0.0005f), new Vector3(w, h, 0.0008f), Color.white, 0f);
                    foreach (var y in new[] { -h / 2, h / 2 }) fixedMb.Box(new Vector3(0, y, -0.0012f), new Vector3(w, 0.0035f, 0.0006f), UI.Lin(UI.CrtDim), 0f);
                    foreach (var x in new[] { -w / 2, w / 2 }) fixedMb.Box(new Vector3(x, 0, -0.0012f), new Vector3(0.0035f, h, 0.0006f), UI.Lin(UI.CrtDim), 0f);
                    hasFixed = true; _travel = 0f;
                    break;
                default:
                    cap.Box(Vector3.zero, new Vector3(w, h, 0.016f), Color.white, 0.005f, 1);
                    _travel = 0.005f;
                    break;
            }
            if (hasFixed) fixedMb.Build("frame", transform, Mats.Lit("btn_fixed", Color.white, 0.2f, 0.4f, Mats.Noise, null, 0.2f, 0.1f));
            _cap = new GameObject("cap").transform; _cap.SetParent(transform, false);
            var tex = style == BtnStyle.Tag || style == BtnStyle.Stamp ? Mats.Noise : null;
            var bg = cap.Build("bg", _cap, Mats.Lit("btn_" + style, Color.white, style == BtnStyle.Key ? 0f : 0.2f, style == BtnStyle.Key ? 0.25f : 0.45f, tex, null, 0.15f, 0.05f));
            _bg = bg.GetComponent<MeshRenderer>();
            _mat = new Material(_bg.sharedMaterial);
            _bg.sharedMaterial = _mat;
            SetColor(col);
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(w, h, 0.04f);
            bc.isTrigger = true;
            float front = style == BtnStyle.Key ? -0.024f : style == BtnStyle.Plate ? -0.009f : -0.007f;   // clear of the rounded edge of a thin tag / stamp
            Label = UI.Text(_cap, label, new Vector3(0, 0, front + 0.002f), Mathf.Min(h * 0.42f, 0.05f), ink, TextAnchor.MiddleCenter, style == BtnStyle.Key || style == BtnStyle.Stamp ? FontStyle.Bold : FontStyle.Normal);
        }

        public void SetColor(Color c) { _base = c; if (_mat != null) _mat.SetColor("_Color", c); }
        public void SetText(string t) { if (Label != null) Label.text = t; }
        public void SetTextColor(Color c) { if (Label != null) Label.color = c; }
        public void Hover() { _hover = 0.15f; Sfx.UI(Synth.UiHover(), 0.15f); }

        float _lastClick = -1f;
        static float _anyClick = -1f;
        public void Click()
        {
            // one press = one click: ignore a repeat on the same key within 0.2 s, and any second click in the same instant
            // (the global gap also covers a panel that rebuilds its buttons on click - the new button isn't a repeat of the old)
            if (Time.unscaledTime - _lastClick < 0.2f || Time.unscaledTime - _anyClick < 0.25f) return;
            _lastClick = _anyClick = Time.unscaledTime;
            if (!Interactable) { Sfx.UI(Synth.UiError(), 0.3f); return; }
            _press = 0.15f;
            Sfx.UI(Synth.UiClick(), 0.5f);
            OnClick?.Invoke();
        }

        void Update()
        {
            _hover -= Time.deltaTime; _press -= Time.deltaTime;
            Color c = Interactable ? _base : Color.Lerp(_base, new Color(0.3f, 0.3f, 0.3f), 0.6f);
            if (_hover > 0) c = Style == BtnStyle.Crt ? Color.Lerp(c, UI.CrtDim, 0.5f) : Color.Lerp(c, Color.white, 0.18f);
            if (_press > 0) c = Style == BtnStyle.Crt ? UI.CrtGreen : Color.Lerp(c, Color.black, 0.2f);
            if (_mat != null) _mat.SetColor("_Color", c);
            if (_cap != null) _cap.localPosition = new Vector3(0, 0, _press > 0 ? _travel : 0);
        }
    }

    /// <summary>Head-locked overlays (fade, damage, suppression), toasts and the wrist display.</summary>
    public class Hud : MonoBehaviour
    {
        PlayerRig _p;
        MeshRenderer _fade, _damage, _supp, _flashR;
        Material _fadeM, _damageM, _suppM, _flashM;
        float _flash;
        float _fadeTarget = 0, _fadeCur = 1, _dmg, _sup;
        bool _blocked;
        TextMesh _toast, _center, _objective, _queue;
        float _toastT, _centerT;
        TextMesh _wristHp, _wristInfo, _wristAmmo;
        Transform _wrist;
        public string RaidTimer = "";
        GameObject _crosshair;

        public static Hud Create(PlayerRig p)
        {
            var h = p.gameObject.AddComponent<Hud>();
            h._p = p;
            h.Build();
            return h;
        }

        void Build()
        {
            var cam = _p.Head;
            var quad = new MB().Quad(new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0), Color.white, Vector2.one).ToMesh("hudquad");
            _fade = Overlay(cam, quad, 0.08f, "hud_fade", new Color(0, 0, 0, 1), null, out _fadeM, 100);
            _fade.transform.localScale = Vector3.one * 0.08f * 8f;   // the black covers far past the edge of any headset's view (about +-84 degrees)
            _damage = Overlay(cam, quad, 0.09f, "hud_dmg", new Color(0.6f, 0, 0, 0), Mats.Vignette, out _damageM, 90);
            _supp = Overlay(cam, quad, 0.095f, "hud_sup", new Color(0, 0, 0, 0), Mats.Vignette, out _suppM, 80);
            _flashR = Overlay(cam, quad, 0.085f, "hud_flash", new Color(1, 1, 1, 0), null, out _flashM, 95);
            _flashR.transform.localScale = Vector3.one * 0.085f * 8f;
            _flashR.enabled = false;
            _toast = UI.Text(cam, "", new Vector3(0, -0.12f, 0.6f), 0.02f, Color.white);
            _toast.GetComponent<MeshRenderer>().sharedMaterial = TextOverlay(_toast);
            _center = UI.Text(cam, "", new Vector3(0, 0.06f, 0.8f), 0.04f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _center.GetComponent<MeshRenderer>().sharedMaterial = TextOverlay(_center);
            // matchmaking status, top-left of your view
            _queue = UI.Text(cam, "", new Vector3(-0.2f, 0.13f, 0.6f), 0.02f, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _queue.GetComponent<MeshRenderer>().sharedMaterial = TextOverlay(_queue);
            // training objective, low in your view
            _objective = UI.Text(cam, "", new Vector3(0, -0.2f, 0.75f), 0.022f, new Color(1f, 0.86f, 0.5f), TextAnchor.MiddleCenter);
            _objective.GetComponent<MeshRenderer>().sharedMaterial = TextOverlay(_objective);
            // wrist watch on the left hand
            _wrist = new GameObject("Wrist").transform;
            // on the hand model you see (not the controller), on the back of the wrist (+Y of the hand model is the back
            // of the hand), screen facing out, reading along the forearm like a real watch (text runs towards the fingers,
            // top of the display on the little-finger side, away from you when you raise your wrist) - always lined up
            _wrist.SetParent(_p.HandL.Visual, false);
            _wrist.localPosition = new Vector3(0.0f, 0.036f, -0.08f);
            _wrist.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
            var w = new MB(2);
            w.Sub(0).Box(Vector3.zero, new Vector3(0.075f, 0.05f, 0.012f), Color.white, 0.006f, 2);
            w.Sub(1).Box(new Vector3(0, 0, -0.0065f), new Vector3(0.068f, 0.043f, 0.001f), Color.white, 0);
            w.Build("watch", _wrist, Mats.Polymer, Mats.Lit("watch_screen", new Color(0.02f, 0.03f, 0.03f), 0f, 0.9f));
            _wristHp = UI.Text(_wrist, "", new Vector3(-0.032f, 0.012f, -0.008f), 0.0085f, new Color(0.4f, 1f, 0.5f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _wristInfo = UI.Text(_wrist, "", new Vector3(-0.032f, -0.004f, -0.008f), 0.0062f, UI.Dim, TextAnchor.MiddleLeft);
            _wristAmmo = UI.Text(_wrist, "", new Vector3(-0.032f, -0.017f, -0.008f), 0.0062f, UI.Accent, TextAnchor.MiddleLeft);
            // desktop crosshair
            _crosshair = new MB().Sphere(Vector3.zero, Vector3.one * 0.0018f, 8, Color.white).Build("crosshair", cam, Mats.Unlit("crosshair", new Color(1, 1, 1, 0.8f), null, Mats.BlendKind.Alpha, false, 200, true));
            _crosshair.transform.localPosition = new Vector3(0, 0, 0.5f);
        }

        Material TextOverlay(TextMesh t)
        {
            // HUD text must draw over everything: start from Unity's own font material (no depth test), not the world-text one
            var src = Compat.DefaultFont != null ? Compat.DefaultFont.material : t.GetComponent<MeshRenderer>().sharedMaterial;
            if (src == null) return null;
            var m = new Material(src);
            m.renderQueue = 4000;
            return m;
        }

        MeshRenderer Overlay(Transform cam, Mesh quad, float dist, string key, Color c, Texture2D tex, out Material mat, int order)
        {
            var go = new GameObject(key);
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0, 0, dist);
            go.transform.localScale = Vector3.one * dist * 1.6f;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mat = new Material(Mats.Unlit(key, c, tex, Mats.BlendKind.Alpha, false, 900 + order, true));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        public void FadeTo(float a) { _fadeTarget = a; }
        public bool FadeDone => Mathf.Abs(_fadeCur - _fadeTarget) < 0.02f;
        public void SetBlocked(bool b) { _blocked = b; }
        public void Damage(float a, Vector3 dir) { _dmg = Mathf.Clamp01(_dmg + a * 0.9f + 0.25f); }
        public void Suppress(float a) { _sup = Mathf.Clamp01(_sup + a); }
        /// <summary>Flashbanged: white-out that fades over a few seconds, with a ringing in your ears.</summary>
        public void Flashbang(float strength)
        {
            if (strength <= 0.02f) return;
            _flash = Mathf.Max(_flash, 1f + strength * 3.5f);
            Sfx.UI(Synth.Ringing(), Mathf.Clamp01(strength) * 0.6f);
        }
        public void Toast(string s) { _toast.text = s; _toastT = 2.5f; }
        public void Center(string s, float time = 3f) { _center.text = s; _centerT = time; }
        /// <summary>Top-left matchmaking line (FINDING RAID 0:12 / RAID FOUND / JOINING RAID); empty hides it.</summary>
        public void Queue(string s, bool timer = false) { _queueLabel = s ?? ""; _queueTimer = timer && _queueLabel.Length > 0; _queueStart = Time.unscaledTime; if (_queue != null) _queue.text = _queueTimer ? _queueLabel + "  0:00" : _queueLabel; }
        string _queueLabel = ""; bool _queueTimer; float _queueStart;
        public void Objective(string s) { if (_objective != null) _objective.text = s ?? ""; }

        public void Tick(float dt)
        {
            float target = Mathf.Max(_fadeTarget, _blocked ? 0.92f : 0f);
            _fadeCur = Mathf.MoveTowards(_fadeCur, target, dt * 2.2f);
            _fadeM.SetColor("_Color", new Color(0, 0, 0, _fadeCur));
            _fade.enabled = _fadeCur > 0.002f;
            var v = _p.Vitals;
            float low = v.Alive ? Mathf.Clamp01(1f - v.Health / 45f) * 0.5f : 0.7f;
            _dmg = Mathf.MoveTowards(_dmg, 0, dt * 1.4f);
            _damageM.SetColor("_Color", new Color(0.55f, 0, 0, Mathf.Max(_dmg, low)));
            _damage.enabled = Mathf.Max(_dmg, low) > 0.01f;
            _sup = Mathf.MoveTowards(_sup, 0, dt * 0.7f);
            _flash = Mathf.MoveTowards(_flash, 0, dt);
            float fa = Mathf.Clamp01(_flash);
            _flashR.enabled = fa > 0.01f;
            if (_flashR.enabled) _flashM.SetColor("_Color", new Color(1, 1, 1, fa));
            _suppM.SetColor("_Color", new Color(0, 0, 0, _sup * 0.75f));
            _supp.enabled = _sup > 0.01f;
            _toastT -= dt; if (_toastT <= 0) _toast.text = "";
            if (_queueTimer && _queue != null) { int qt = Mathf.FloorToInt(Time.unscaledTime - _queueStart); _queue.text = _queueLabel + "  " + (qt / 60) + ":" + (qt % 60).ToString("00"); }
            _centerT -= dt; if (_centerT <= 0) _center.text = "";
            _crosshair.SetActive(!XRInputs.VR && !Desktop.Aiming && v.Alive);
            // wrist
            _wristHp.text = Mathf.CeilToInt(v.Health) + " HP" + (v.Bleed > 0 ? "  <color=#ff4040>BLEEDING</color>" : "");
            _wristHp.color = v.Health > 60 ? new Color(0.4f, 1f, 0.5f) : v.Health > 30 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.3f, 0.25f);
            var prof = Profile.Current;
            _wristInfo.text = (RaidTimer.Length > 0 ? RaidTimer + "   " : "") + "$" + (prof != null ? prof.money.ToString("N0") : "0") + "   STA " + Mathf.RoundToInt(v.Stamina);
            var gun = (_p.HandR.Held as Gun) ?? (_p.HandL.Held as Gun);
            if (gun != null) _wristAmmo.text = gun.g.name + "  " + (gun.Rounds + (gun.chambered ? 1 : 0)) + "/" + gun.Capacity + "  [" + _p.Gear.Ammo[(int)gun.g.cal] + "]";
            else _wristAmmo.text = "";
        }
    }
}
