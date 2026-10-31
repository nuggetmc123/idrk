using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>World-space UI toolkit: panels, crisp 3D text, and buttons you laser-point (VR) or click (desktop).</summary>
    public static class UI
    {
        public static readonly Color Accent = new Color(1f, 0.62f, 0.18f);
        public static readonly Color PanelCol = new Color(0.06f, 0.065f, 0.07f, 0.92f);
        public static readonly Color Dim = new Color(0.6f, 0.62f, 0.64f);

        public static Transform Panel(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector2 size, Color? col = null, bool frame = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos; root.localRotation = localRot;
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0, 0.01f), new Vector3(size.x, size.y, 0.02f), Color.white, 0.006f, 1);
            if (frame)
            {
                mb.Sub(1).Box(new Vector3(0, size.y * 0.5f - 0.004f, -0.001f), new Vector3(size.x, 0.008f, 0.004f), Color.white, 0);
                mb.Sub(1).Box(new Vector3(-size.x * 0.5f + 0.004f, 0, -0.001f), new Vector3(0.008f, size.y, 0.004f), Color.white, 0);
            }
            mb.Build("bg", root, Mats.Lit("ui_panel", (col ?? PanelCol), 0.3f, 0.6f, Mats.Panel, null, 0.1f, 0.05f), Mats.Lit("ui_accent", Accent, 0f, 0.4f, null, null, 0, 0, false, 0.5f, Accent * 1.5f));
            return root;
        }

        public static TextMesh Text(Transform parent, string text, Vector3 localPos, float size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + new Vector3(0, 0, -0.002f);
            var tm = go.AddComponent<TextMesh>();
            var f = Compat.DefaultFont;
            if (f != null) { tm.font = f; go.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
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

        public static WorldButton Button(Transform parent, string label, Vector3 localPos, Vector2 size, System.Action onClick, Color? col = null)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + new Vector3(0, 0, -0.012f);
            var b = go.AddComponent<WorldButton>();
            b.Init(label, size, onClick, col ?? new Color(0.16f, 0.17f, 0.18f));
            return b;
        }
    }

    public class WorldButton : MonoBehaviour
    {
        public System.Action OnClick;
        public TextMesh Label;
        public bool Interactable = true;
        MeshRenderer _bg;
        Color _base;
        float _hover, _press;
        Material _mat;

        public void Init(string label, Vector2 size, System.Action onClick, Color col)
        {
            OnClick = onClick;
            _base = col;
            var mb = new MB();
            mb.Box(Vector3.zero, new Vector3(size.x, size.y, 0.012f), Color.white, 0.004f, 1);
            var bg = mb.Build("bg", transform, Mats.Lit("btn", Color.white, 0.2f, 0.5f, null, null, 0f, 0f));
            _bg = bg.GetComponent<MeshRenderer>();
            _mat = new Material(_bg.sharedMaterial);
            _bg.sharedMaterial = _mat;
            SetColor(col);
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(size.x, size.y, 0.03f);
            bc.isTrigger = true;
            Label = UI.Text(transform, label, new Vector3(0, 0, -0.008f), Mathf.Min(size.y * 0.42f, 0.05f), Color.white);
        }

        public void SetColor(Color c) { _base = c; if (_mat != null) _mat.SetColor("_Color", c); }
        public void SetText(string t) { if (Label != null) Label.text = t; }
        public void Hover() { _hover = 0.15f; Sfx.UI(Synth.UiHover(), 0.15f); }

        public void Click()
        {
            if (!Interactable) { Sfx.UI(Synth.UiError(), 0.3f); return; }
            _press = 0.15f;
            Sfx.UI(Synth.UiClick(), 0.5f);
            OnClick?.Invoke();
        }

        void Update()
        {
            _hover -= Time.deltaTime; _press -= Time.deltaTime;
            Color c = Interactable ? _base : _base * 0.5f;
            if (_hover > 0) c = Color.Lerp(c, UI.Accent, 0.35f);
            if (_press > 0) c = UI.Accent;
            if (_mat != null) _mat.SetColor("_Color", c);
            if (_bg != null) _bg.transform.localPosition = new Vector3(0, 0, _press > 0 ? 0.004f : 0);
        }
    }

    /// <summary>Head-locked overlays (fade, damage, suppression), toasts and the wrist display.</summary>
    public class Hud : MonoBehaviour
    {
        PlayerRig _p;
        MeshRenderer _fade, _damage, _supp;
        Material _fadeM, _damageM, _suppM;
        float _fadeTarget = 0, _fadeCur = 1, _dmg, _sup;
        bool _blocked;
        TextMesh _toast, _center;
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
            _damage = Overlay(cam, quad, 0.09f, "hud_dmg", new Color(0.6f, 0, 0, 0), Mats.Vignette, out _damageM, 90);
            _supp = Overlay(cam, quad, 0.095f, "hud_sup", new Color(0, 0, 0, 0), Mats.Vignette, out _suppM, 80);
            _toast = UI.Text(cam, "", new Vector3(0, -0.12f, 0.6f), 0.02f, Color.white);
            _toast.GetComponent<MeshRenderer>().sharedMaterial = TextOverlay(_toast);
            _center = UI.Text(cam, "", new Vector3(0, 0.06f, 0.8f), 0.04f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _center.GetComponent<MeshRenderer>().sharedMaterial = TextOverlay(_center);
            // wrist watch on the left hand
            _wrist = new GameObject("Wrist").transform;
            _wrist.SetParent(_p.HandL.transform, false);
            _wrist.localPosition = new Vector3(0.0f, 0.03f, -0.07f);
            _wrist.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.right);
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
            var src = t.GetComponent<MeshRenderer>().sharedMaterial;
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
        public void Toast(string s) { _toast.text = s; _toastT = 2.5f; }
        public void Center(string s, float time = 3f) { _center.text = s; _centerT = time; }

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
            _suppM.SetColor("_Color", new Color(0, 0, 0, _sup * 0.75f));
            _supp.enabled = _sup > 0.01f;
            _toastT -= dt; if (_toastT <= 0) _toast.text = "";
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
