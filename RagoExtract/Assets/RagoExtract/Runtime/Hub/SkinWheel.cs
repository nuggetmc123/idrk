using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The Market's Skin Wheel: a painted cabinet with a big wheel on top, one slice per wheel skin (painted in that
    /// skin's colour, its name on it). Pay $10,000 and SPIN: wherever it stops, that skin is yours for good - it shows up
    /// in your bunker's paint booth. It only ever lands on a skin you don't have yet.
    /// </summary>
    public class SkinWheel : MonoBehaviour
    {
        public const int Cost = 10000;
        Transform _wheel;
        TextMesh _result, _cash;
        List<GunSkins.Skin> _skins;
        bool _spinning; float _from, _to, _t, _dur, _lastTick; int _target = -1;

        public static SkinWheel Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("SkinWheel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var w = go.AddComponent<SkinWheel>(); w.Build(); return w;
        }

        void Build()
        {
            _skins = GunSkins.WheelSkins();
            Color dk = UI.Lin(new Color(0.12f, 0.1f, 0.14f)), trim = UI.Lin(new Color(0.85f, 0.65f, 0.2f)), purple = UI.Lin(new Color(0.35f, 0.15f, 0.5f));
            // the cabinet: a box with gold trim, a back board holding the wheel, a pointer at the top, lights round the rim
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.55f, 0), new Vector3(1.5f, 1.1f, 0.7f), purple, 0.04f, 2);
            mb.Sub(1).Box(new Vector3(0, 1.11f, 0), new Vector3(1.54f, 0.04f, 0.74f), trim, 0.01f);
            mb.Sub(1).Box(new Vector3(0, 0.05f, 0), new Vector3(1.54f, 0.1f, 0.74f), trim, 0.01f);
            mb.Sub(0).Box(new Vector3(0, 2.0f, 0.25f), new Vector3(1.6f, 1.8f, 0.1f), dk, 0.03f, 2);
            mb.Sub(1).Box(new Vector3(0, 2.93f, 0.25f), new Vector3(1.64f, 0.06f, 0.14f), trim, 0.01f);
            mb.Sub(1).Box(new Vector3(0, 2.86f, 0.16f), new Vector3(0.09f, 0.16f, 0.04f), trim, 0f, 1, Quaternion.Euler(0, 0, 45));   // pointer
            for (int i = 0; i < 24; i++) { float a = i * 15f * Mathf.Deg2Rad; mb.Sub(1).Sphere(new Vector3(Mathf.Sin(a) * 0.76f, 2.0f + Mathf.Cos(a) * 0.76f, 0.19f), Vector3.one * 0.025f, 6, new Color(1f, 0.9f, 0.6f)); }
            // a couple of spray cans and a painted gun stencil on the front
            mb.Sub(1).Box(new Vector3(0, 0.62f, -0.352f), new Vector3(0.9f, 0.2f, 0.004f), trim, 0.004f);
            mb.Build("Cabinet", transform, Casino.Body("skinwheel_cab", Color.white), Mats.Lit("skinwheel_trim", Color.white, 0.9f, 0.7f, Mats.Metal, null, 0.1f, 0.2f, false, 1f, new Color(0.4f, 0.3f, 0.1f)));
            Casino.Col(gameObject, new Vector3(0, 0.55f, 0), new Vector3(1.5f, 1.1f, 0.7f));
            Casino.Col(gameObject, new Vector3(0, 2.0f, 0.25f), new Vector3(1.6f, 1.8f, 0.1f));
            // the wheel: one slice per skin, in the skin's colour, its name along the slice
            _wheel = new GameObject("Wheel").transform; _wheel.SetParent(transform, false); _wheel.localPosition = new Vector3(0, 2.0f, 0.18f);
            int n = Mathf.Max(1, _skins.Count); float R = 0.7f;
            var mesh = new Mesh();
            var v = new List<Vector3>(); var c = new List<Color>(); var tr = new List<int>(); var nr = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                var s = i < _skins.Count ? _skins[i] : null;
                float a0 = (i - 0.5f) * Mathf.PI * 2 / n, a1 = (i + 0.5f) * Mathf.PI * 2 / n;
                Color col = UI.Lin(s != null ? s.spray : Color.gray) * ((i & 1) == 0 ? 1f : 0.85f); col.a = 1f;
                const int arc = 6;
                int b0 = v.Count;
                v.Add(Vector3.zero); c.Add(col); nr.Add(Vector3.back);
                for (int k = 0; k <= arc; k++) { float a = Mathf.Lerp(a0, a1, k / (float)arc); v.Add(new Vector3(Mathf.Sin(a), Mathf.Cos(a)) * R); c.Add(col); nr.Add(Vector3.back); }
                for (int k = 0; k < arc; k++) { tr.Add(b0); tr.Add(b0 + k + 1); tr.Add(b0 + k + 2); tr.Add(b0); tr.Add(b0 + k + 2); tr.Add(b0 + k + 1); }
                // its name, reading outwards along the slice, dark or light so it shows on the colour
                float am = i * Mathf.PI * 2 / n;
                bool light = s != null && (s.spray.r + s.spray.g + s.spray.b) > 1.6f;
                var lt = UI.Text(_wheel, s != null ? s.name.ToUpper() : "?", Vector3.zero, 0.028f, light ? new Color(0.08f, 0.08f, 0.1f) : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                lt.transform.localPosition = new Vector3(Mathf.Sin(am), Mathf.Cos(am), 0) * (R * 0.58f) + Vector3.back * 0.005f;
                lt.transform.localRotation = Quaternion.Euler(0, 0, 90f - am * Mathf.Rad2Deg);
            }
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetNormals(nr); mesh.SetTriangles(tr, 0); mesh.RecalculateBounds();
            _wheel.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            _wheel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit("wheel_face", Color.white, 0f, 0.5f);
            var hub = new MB().Cyl(new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.01f), 0.09f, 0.09f, 16, new Color(0.85f, 0.65f, 0.2f)).Build("Hub", _wheel, Mats.Gold);
            // signs and controls on the front of the cabinet
            UI.Text(transform, "SKIN WHEEL", new Vector3(0, 3.15f, 0.15f), 0.11f, new Color(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "$" + Cost.ToString("N0") + " a spin - win a gun skin for good", new Vector3(0, 1.06f, -0.36f), 0.03f, Color.white);
            _result = UI.Text(transform, "Spin to win a skin for your paint booth", new Vector3(0, 0.95f, -0.36f), 0.028f, new Color(0.85f, 0.85f, 0.9f));
            _cash = UI.Text(transform, "", new Vector3(0, 0.36f, -0.36f), 0.026f, new Color(0.7f, 1f, 0.6f));
            UI.Button(transform, "SPIN  $" + Cost.ToString("N0"), new Vector3(0, 0.78f, -0.36f), new Vector2(0.46f, 0.11f), Spin, new Color(0.55f, 0.2f, 0.65f), BtnStyle.Key);
            Refresh();
        }

        void Refresh()
        {
            if (_cash == null) return;
            int have = 0; foreach (var s in _skins) if (GunSkins.Owned(s)) have++;
            _cash.text = "CASH $" + Casino.Cash.ToString("N0") + "     SKINS " + have + " / " + _skins.Count;
        }

        void Spin()
        {
            if (_spinning || _skins.Count == 0) return;
            var left = new List<int>();
            for (int i = 0; i < _skins.Count; i++) if (!GunSkins.Owned(_skins[i])) left.Add(i);
            if (left.Count == 0) { _result.text = "You've won every skin on the wheel!"; Sfx.UI(Synth.UiError(), 0.4f); return; }
            if (Profile.Current == null || Profile.Current.money < Cost) { Sfx.UI(Synth.UiError(), 0.4f); RagoGame.I?.Player?.Toast("A spin costs $" + Cost.ToString("N0")); return; }
            Profile.Current.money -= Cost; Profile.Current.Save();
            Sfx.UI(Synth.Cash(), 0.5f);
            _target = left[Random.Range(0, left.Count)];
            float step = 360f / _skins.Count;
            _from = Mathf.Repeat(_wheel.localEulerAngles.z, 360f);
            float want = _target * step + Random.Range(-step * 0.3f, step * 0.3f);
            _to = _from + 360f * 5f + Mathf.Repeat(want - _from, 360f);
            _t = 0f; _dur = 5.5f; _spinning = true; _result.text = "Spinning...";
            Refresh();
        }

        void Update()
        {
            if (Time.frameCount % 30 == 0) Refresh();
            if (!_spinning) return;
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur), e = 1f - Mathf.Pow(1f - k, 3f);
            float a = Mathf.Lerp(_from, _to, e);
            _wheel.localRotation = Quaternion.Euler(0, 0, a);
            float step = 360f / _skins.Count;
            if (Mathf.Floor(a / step) != Mathf.Floor(_lastTick / step)) Sfx.Play(Synth.UiClick(), _wheel.position, 0.4f, 1.3f, 1f, 8f);
            _lastTick = a;
            if (k < 1f) return;
            _spinning = false;
            var s = _skins[_target];
            var prof = Profile.Current;
            if (prof != null) { if (prof.skins == null) prof.skins = new List<string>(); if (!prof.skins.Contains(s.id)) prof.skins.Add(s.id); prof.Save(); }
            Sfx.UI(Synth.Ding(), 0.7f);
            _result.text = "YOU WON: " + s.name.ToUpper() + "!\nIt's in your bunker's paint booth";
            _result.color = new Color(0.5f, 1f, 0.6f);
            RagoGame.I?.Player?.Toast("New skin: " + s.name + " - paint it on at your bunker's paint booth");
            Refresh();
        }
    }
}
