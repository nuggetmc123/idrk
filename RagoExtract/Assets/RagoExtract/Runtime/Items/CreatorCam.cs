using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The CREATOR CAM (for content creators): a little action camera that clips onto the side of your helmet. It only
    /// has one button - RECORD. Reach to it with an empty hand and pull the trigger to start / stop (desktop: C); grab it
    /// to take it off. While it records, its red light blinks for everyone to see, your watch shows REC and the time, and
    /// on PC the game window shows the camera's steady first-person view (record it with OBS or similar; on a Quest,
    /// use the headset's own Record). Bought from Mara, or found in raids.
    /// </summary>
    public class CamItem : Item
    {
        public static CamItem Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var it = go.AddComponent<CamItem>();
            it.def = d;
            Model(go.transform, false);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0, 0.005f); col.size = new Vector3(0.045f, 0.05f, 0.075f);
            it.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            it.SetupPhysics(0.25f);
            return it;
        }

        /// <summary>The camera body: a small black box, lens to the front (+Z), a record button on top, the red light.</summary>
        public static GameObject Model(Transform parent, bool rec)
        {
            var mb = new MB(2);
            Color body = new Color(0.08f, 0.08f, 0.09f), rim = new Color(0.35f, 0.36f, 0.38f), red = rec ? new Color(1f, 0.05f, 0.05f) : new Color(0.35f, 0.04f, 0.04f);
            mb.Sub(0).Box(Vector3.zero, new Vector3(0.04f, 0.045f, 0.06f), body, 0.006f);
            mb.Sub(1).Cyl(new Vector3(0, -0.004f, 0.03f), new Vector3(0, -0.004f, 0.04f), 0.014f, 0.013f, 14, rim);
            mb.Sub(1).Cyl(new Vector3(0, -0.004f, 0.04f), new Vector3(0, -0.004f, 0.0415f), 0.01f, 0.01f, 14, new Color(0.05f, 0.08f, 0.15f));
            mb.Sub(1).Cyl(new Vector3(0, 0.0225f, -0.01f), new Vector3(0, 0.027f, -0.01f), 0.007f, 0.007f, 10, red);   // the RECORD button
            mb.Sub(1).Sphere(new Vector3(0.012f, 0.016f, 0.031f), Vector3.one * 0.004f, 6, red);                        // the light
            return mb.Build("CamModel", parent, Mats.Lit("cam_body", Color.white, 0.2f, 0.4f, Mats.Noise, null, 0.2f, 0.1f),
                Mats.Lit("cam_parts", Color.white, 0.6f, 0.8f, null, null, 0f, 0f, false, 0.5f, rec ? new Color(1.5f, 0.1f, 0.1f) : (Color?)null));
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead && pl.Gear.TryWearCam(this, h)) return true;
            return base.TrySlot(h);
        }
    }

    /// <summary>The recording side: the REC time, and on PC a steadied first-person camera on the game window.</summary>
    public class CreatorCamRecorder : MonoBehaviour
    {
        public static float Since = -1f;
        Camera _cam; Transform _head;
        Quaternion _rot; Vector3 _pos;

        public static void Set(PlayerRig p, bool on)
        {
            var r = p.GetComponent<CreatorCamRecorder>() ?? p.gameObject.AddComponent<CreatorCamRecorder>();
            r._head = p.Head;
            Since = on ? Time.unscaledTime : -1f;
            // PC only: the PC's game window gets a steady view (on a Quest there's no window - its own recorder films the eyes)
            bool pc = Application.platform != RuntimePlatform.Android;
            if (on && pc && r._cam == null)
            {
                var go = new GameObject("CreatorCamView");
                go.transform.SetParent(r.transform, false);
                r._cam = go.AddComponent<Camera>();
                r._cam.stereoTargetEye = StereoTargetEyeMask.None;
                r._cam.depth = 50; r._cam.fieldOfView = 75f; r._cam.nearClipPlane = 0.05f;
                r._pos = p.Head.position; r._rot = p.Head.rotation;
            }
            if (r._cam != null) r._cam.enabled = on && pc;
        }

        void LateUpdate()
        {
            if (_cam == null || !_cam.enabled || _head == null) return;
            // first person, from the helmet: follows your head but smoothed, so the footage isn't shaky
            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime), kr = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
            _pos = Vector3.Lerp(_pos, _head.position + _head.up * 0.06f, k);
            _rot = Quaternion.Slerp(_rot, _head.rotation, kr);
            _cam.transform.SetPositionAndRotation(_pos, _rot);
        }

        public static string Clock() { if (Since < 0f) return ""; int s = Mathf.FloorToInt(Time.unscaledTime - Since); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }
    }
}
