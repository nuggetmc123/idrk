using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// A full-length mirror. A second camera sits where your eyes would be if they
    /// were reflected through the glass and looks back out through it, framed
    /// exactly on the glass, so the picture lines up like a real mirror. It only
    /// renders while you're close and in front of it, and it can see your own body
    /// (which your headset camera never draws).
    /// </summary>
    public class Mirror : MonoBehaviour
    {
        Camera cam;
        RenderTexture rt;
        Transform glass;
        Vector2 half;
        Material glassMat;

        /// <summary>A standing mirror at pos (on the floor) facing along yaw; size is the glass in metres.</summary>
        public static Mirror Create(Transform parent, Vector3 pos, float yaw, Vector2 size)
        {
            var root = Prims.Empty("mirror", parent, pos);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var m = root.AddComponent<Mirror>();
            var t = root.transform;
            float bottom = 0.25f, w = size.x, h = size.y;
            var frame = Prims.Lit(Prims.Hex("c9a14a"), 0.75f, 0.85f, Surface.Metal);
            var back = MapKit.M("3a2a1c", 0.3f, 0f, Surface.Wood);
            // a gold frame on a wooden back, with feet
            Prims.Box(t, new Vector3(0, bottom + h / 2, -0.06f), new Vector3(w + 0.16f, h + 0.16f, 0.06f), back);
            Prims.Box(t, new Vector3(0, bottom - 0.02f, 0.0f), new Vector3(w + 0.2f, 0.08f, 0.08f), frame, false);
            Prims.Box(t, new Vector3(0, bottom + h + 0.02f, 0.0f), new Vector3(w + 0.2f, 0.08f, 0.08f), frame, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prims.Box(t, new Vector3(s * (w / 2 + 0.06f), bottom + h / 2, 0.0f), new Vector3(0.08f, h + 0.12f, 0.08f), frame, false);
                Prims.Box(t, new Vector3(s * (w / 2 - 0.05f), 0.12f, -0.15f), new Vector3(0.08f, 0.25f, 0.4f), back, false);
            }
            Prims.Ball(t, new Vector3(0, bottom + h + 0.12f, 0.0f), new Vector3(0.18f, 0.14f, 0.06f), frame);
            // the glass: a quad facing forward (+z of the mirror)
            m.half = new Vector2(w / 2, h / 2);
            m.rt = new RenderTexture(512, Mathf.RoundToInt(512 * h / w), 16) { name = "fz_mirror", antiAliasing = 2 };
            m.glassMat = Prims.Sprite(Color.white);
            m.glassMat.mainTexture = m.rt;
            // the camera behind the glass sees the world flipped left-to-right: flip it back
            m.glassMat.mainTextureScale = new Vector2(-1, 1);
            m.glassMat.mainTextureOffset = new Vector2(1, 0);
            m.glassMat.renderQueue = 2000;
            var q = Prims.Make(PrimitiveType.Quad, t, new Vector3(0, bottom + h / 2, 0.005f), new Vector3(w, h, 1), m.glassMat, false, Quaternion.Euler(0, 180, 0), "glass");
            q.layer = Layers.MirrorGlass;
            m.glass = q.transform;
            var cg = new GameObject("mirror camera");
            cg.transform.SetParent(t, false);
            m.cam = cg.AddComponent<Camera>();
            m.cam.enabled = false;
            m.cam.targetTexture = m.rt;
            m.cam.cullingMask = ~(1 << Layers.MirrorGlass);
            m.cam.clearFlags = CameraClearFlags.Skybox;
            m.cam.allowHDR = false;
            m.cam.allowMSAA = false;
            m.cam.useOcclusionCulling = false;
            m.cam.stereoTargetEye = StereoTargetEyeMask.None;
            m.cam.farClipPlane = 80f;
            return m;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.I;
            if (rig == null || cam == null || !Application.isPlaying) return;
            var eye = rig.head.position;
            var n = glass.forward * -1f;    // the quad's visible side faces -its own forward
            var c = glass.position;
            float dist = Vector3.Dot(eye - c, n);
            if (dist < 0.05f || dist > 9f || (eye - c).sqrMagnitude > 12f * 12f) return;   // behind it or too far: don't bother
            rig.ShowInMirror();

            // reflect the eye through the glass and look back out through it
            var up = glass.up;
            var mirrored = eye - 2f * dist * n;
            cam.transform.SetPositionAndRotation(mirrored, Quaternion.LookRotation(n, up));
            var right = cam.transform.right;
            var rel = c - mirrored;
            float near = Vector3.Dot(rel, n);
            float cx = Vector3.Dot(rel, right), cy = Vector3.Dot(rel, up);
            cam.nearClipPlane = Mathf.Max(0.02f, near - 0.01f);
            float k = cam.nearClipPlane / near;
            cam.projectionMatrix = Matrix4x4.Frustum((cx - half.x) * k, (cx + half.x) * k, (cy - half.y) * k, (cy + half.y) * k, cam.nearClipPlane, cam.farClipPlane);
            cam.Render();
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
        }
    }
}
