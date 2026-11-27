using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A real mirror (3.55): a reflection camera renders the room as each of your eyes would see it in the glass, and the
    /// Rago/Mirror shader shows the right one to each eye - so in a headset the reflection has depth, like a real mirror.
    /// It only renders while you can see it and you're close, at a reduced resolution. Your own head and vest (normally
    /// hidden from your eyes so they don't block your view) are switched on just for the reflection: you see your face.
    /// </summary>
    public class Mirror : MonoBehaviour
    {
        public float Range = 9f, Scale = 0.6f;
        Camera _cam;
        RenderTexture _l, _r;
        Material _mat;
        int _frame = -1;
        static bool _busy;
        readonly List<Renderer> _hidden = new List<Renderer>();

        /// <summary>A framed mirror on a wall: pos = the middle of the glass, yaw = the way it faces, size = the glass.</summary>
        public static Mirror Create(Transform parent, Vector3 pos, float yaw, Vector2 size)
        {
            var root = new GameObject("Mirror").transform;
            root.SetParent(parent, false);
            root.localPosition = pos; root.localRotation = Quaternion.Euler(0, yaw, 0);
            // the frame: dark wood, a little proud of the wall
            var frame = Mats.Lit("mirror_frame", new Color(0.24f, 0.17f, 0.11f), 0.05f, 0.5f, Mats.Wood, Mats.WoodN, 0.25f, 0.1f);
            var fb = new MB();
            float fw = 0.06f, hx = size.x / 2f, hy = size.y / 2f;
            fb.Box(new Vector3(0, hy + fw / 2, -0.015f), new Vector3(size.x + fw * 2, fw, 0.05f), Color.white, 0.008f, 1);
            fb.Box(new Vector3(0, -hy - fw / 2, -0.015f), new Vector3(size.x + fw * 2, fw, 0.05f), Color.white, 0.008f, 1);
            fb.Box(new Vector3(-hx - fw / 2, 0, -0.015f), new Vector3(fw, size.y, 0.05f), Color.white, 0.008f, 1);
            fb.Box(new Vector3(hx + fw / 2, 0, -0.015f), new Vector3(fw, size.y, 0.05f), Color.white, 0.008f, 1);
            fb.Box(new Vector3(0, 0, -0.035f), new Vector3(size.x, size.y, 0.01f), new Color(0.1f, 0.1f, 0.1f), 0f, 0);   // backing
            fb.Build("Frame", root, frame);
            // the glass
            var g = new MB().Quad(new Vector3(-hx, -hy, 0), new Vector3(hx, -hy, 0), new Vector3(hx, hy, 0), new Vector3(-hx, hy, 0), Color.white, Vector2.one).Build("Glass", root, Mats.Mirror());
            var m = g.AddComponent<Mirror>();
            m._mat = g.GetComponent<Renderer>().sharedMaterial;
            return m;
        }

        void OnDisable() { Release(); }
        void OnDestroy() { Release(); if (_cam != null) Destroy(_cam.gameObject); }

        void Release()
        {
            if (_l != null) { _l.Release(); Destroy(_l); _l = null; }
            if (_r != null) { _r.Release(); Destroy(_r); _r = null; }
        }

        void OnWillRenderObject()
        {
            if (_busy || _mat == null || Time.frameCount == _frame) return;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            var cur = Camera.current;
            if (pl == null || cur == null || cur != pl.Cam) return;   // only your own view (not scopes or other cameras)
            Vector3 pos = transform.position, n = transform.forward;
            Vector3 eye = cur.transform.position;
            if (Vector3.Dot(eye - pos, n) <= 0f || (eye - pos).sqrMagnitude > Range * Range) return;   // behind it, or too far
            _frame = Time.frameCount;
            _busy = true;
            try
            {
                EnsureCamera(cur);
                ShowOwnHead(pl, true);
                if (cur.stereoEnabled)
                {
                    Render(cur, Camera.StereoscopicEye.Left, ref _l);
                    Render(cur, Camera.StereoscopicEye.Right, ref _r);
                }
                else Render(cur, null, ref _l);
                _mat.SetTexture("_LeftTex", _l);
                _mat.SetTexture("_RightTex", _r != null && cur.stereoEnabled ? _r : _l);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] mirror: " + e.Message); }
            finally { ShowOwnHead(pl, false); _busy = false; }
        }

        void EnsureCamera(Camera src)
        {
            if (_cam == null)
            {
                var go = new GameObject("MirrorCam");
                go.hideFlags = HideFlags.HideAndDontSave;
                _cam = go.AddComponent<Camera>();
                _cam.enabled = false;
                _cam.allowMSAA = false; _cam.allowHDR = false;
                _cam.stereoTargetEye = StereoTargetEyeMask.None;
            }
            _cam.clearFlags = src.clearFlags; _cam.backgroundColor = src.backgroundColor;
            _cam.cullingMask = src.cullingMask;
            _cam.nearClipPlane = 0.02f; _cam.farClipPlane = Mathf.Min(src.farClipPlane, 40f);
        }

        void Render(Camera src, Camera.StereoscopicEye? eye, ref RenderTexture rt)
        {
            int w, h;
            if (eye.HasValue && UnityEngine.XR.XRSettings.enabled) { w = UnityEngine.XR.XRSettings.eyeTextureWidth; h = UnityEngine.XR.XRSettings.eyeTextureHeight; }
            else { w = src.pixelWidth; h = src.pixelHeight; }
            w = Mathf.Max(64, (int)(w * Scale)); h = Mathf.Max(64, (int)(h * Scale));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "MirrorRT", hideFlags = HideFlags.HideAndDontSave };
            }
            Vector3 pos = transform.position, n = transform.forward;
            var plane = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, pos) - 0.01f);
            var refl = Reflection(plane);
            Matrix4x4 view = eye.HasValue ? src.GetStereoViewMatrix(eye.Value) : src.worldToCameraMatrix;
            Matrix4x4 proj = eye.HasValue ? src.GetStereoProjectionMatrix(eye.Value) : src.projectionMatrix;
            Vector3 eyePos = view.inverse.MultiplyPoint(Vector3.zero);
            _cam.transform.position = refl.MultiplyPoint(eyePos);   // for culling and LODs
            _cam.worldToCameraMatrix = view * refl;
            _cam.projectionMatrix = proj;
            // clip everything on the wall side of the glass: an oblique near plane right on the mirror
            Vector3 cp = _cam.worldToCameraMatrix.MultiplyPoint(pos + n * 0.02f), cn = _cam.worldToCameraMatrix.MultiplyVector(n).normalized;
            _cam.projectionMatrix = _cam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
            _cam.targetTexture = rt;
            bool inv = GL.invertCulling;
            GL.invertCulling = !inv;   // a reflection flips the winding
            _cam.Render();
            GL.invertCulling = inv;
            _cam.targetTexture = null;
        }

        /// <summary>Your head, face, helmet and vest are drawn as shadows only for your own eyes: in the reflection you see them.</summary>
        void ShowOwnHead(PlayerRig pl, bool show)
        {
            if (show)
            {
                _hidden.Clear();
                var root = pl.Body != null && pl.Body.Rig != null ? pl.Body.Rig.root : null;
                if (root == null) return;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) { _hidden.Add(r); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            }
            else
            {
                foreach (var r in _hidden) if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                _hidden.Clear();
            }
        }

        static Matrix4x4 Reflection(Vector4 p)
        {
            var m = Matrix4x4.zero;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y;       m.m02 = -2f * p.x * p.z;       m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x;       m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z;       m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x;       m.m21 = -2f * p.z * p.y;       m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m33 = 1f;
            return m;
        }
    }
}
