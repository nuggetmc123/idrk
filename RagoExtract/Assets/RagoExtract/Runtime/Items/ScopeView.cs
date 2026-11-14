using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A magnified scope's picture: a small camera at the front of the scope renders into the eyepiece while the
    /// local player holds the gun with their eye near it. Its field of view is set from how big the eyepiece looks
    /// to you, so the picture is exactly 3x / 4x / 8x whatever your eye relief. Costs nothing when not in use.
    /// </summary>
    public class ScopeView : MonoBehaviour
    {
        SightItem _sight;
        float _mag;
        Camera _cam;
        RenderTexture _rt;
        Material _mat;
        Transform _obj;
        float _lensR;
        bool _live;

        public void Init(SightItem s, float mag)
        {
            _sight = s; _mag = mag;
            _obj = transform.Find("SightBody/Objective");
            var mr = s.Lens != null ? s.Lens.GetComponent<MeshRenderer>() : null;
            _mat = mr != null ? mr.sharedMaterial : null;
            _lensR = _mat != null && _mat.HasProperty("_LensR") ? _mat.GetFloat("_LensR") : 0.015f;
        }

        void Make()
        {
            _rt = new RenderTexture(512, 512, 16) { name = "ScopeRT", antiAliasing = 1 };
            _cam = new GameObject("ScopeCam").AddComponent<Camera>();
            _cam.transform.SetParent(_obj != null ? _obj : transform, false);
            _cam.targetTexture = _rt;
            _cam.stereoTargetEye = StereoTargetEyeMask.None;
            _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 1500f;
            _cam.enabled = false;   // rendered by hand, only while you look through it
            int mask = ~0;
            if (Layers.Hands >= 0) mask &= ~(1 << Layers.Hands);
            if (Layers.LocalBody >= 0) mask &= ~(1 << Layers.LocalBody);
            _cam.cullingMask = mask;
            var main = Camera.main;
            if (main != null) { _cam.clearFlags = main.clearFlags; _cam.backgroundColor = main.backgroundColor; }
            // the scope shader didn't build on this device: fall back to a plain textured lens (zoom, no reticle)
            if (_mat != null && (_mat.shader == null || !_mat.shader.isSupported || _mat.shader.name != "Rago/ScopeLens"))
            {
                {
                    var m = Mats.UnlitTexture(_rt);
                    var mr = _sight.Lens.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = m;
                    _mat = m;
                    Debug.LogWarning("[Rago] scope shader unavailable - using a plain lens");
                }
            }
            if (_mat != null) _mat.SetTexture("_MainTex", _rt);
        }

        void LateUpdate()
        {
            if (_sight == null || _sight.Lens == null || _mat == null) return;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            bool want = false; float dist = 1f;
            if (pl != null && _sight.onGun != null && _sight.onGun.primaryHand != null)
            {
                dist = Vector3.Distance(pl.Head.position, _sight.Lens.position);
                want = dist < 0.4f;
            }
            if (want)
            {
                if (_cam == null) Make();
                float apparent = 2f * Mathf.Atan(_lensR / Mathf.Max(dist, 0.03f)) * Mathf.Rad2Deg;   // how big the eyepiece looks to you
                _cam.fieldOfView = Mathf.Clamp(apparent / _mag, 0.5f, 30f);
                _cam.Render();
            }
            if (want != _live) { _live = want; if (_mat.HasProperty("_Active")) _mat.SetFloat("_Active", want ? 1f : 0f); }
        }

        void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_mat != null) Destroy(_mat);
        }
    }
}
