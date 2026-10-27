using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Your own body while you walk around the garage: your driver in their race suit, standing under the headset,
    /// with arms that reach to your VR gloves and legs that take steps as you move. Look down and you see yourself.
    /// The torso turns with your head (after a little lag, like shoulders do), you can crouch, and the feet stay planted
    /// until you move away from them. In the car the seated driver (DriverBody) takes over, so this hides there.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after the rig and the gloves have moved
    public class PlayerBody : MonoBehaviour
    {
        public static PlayerBody I { get; private set; }

        Transform torso, shoulderL, shoulderR;
        readonly Transform[] upper = new Transform[2], fore = new Transform[2], elbow = new Transform[2];
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], knee = new Transform[2], boot = new Transform[2];
        Material[] mats;
        Vector3 bodyFwd = Vector3.forward;
        Vector3 lastPelvis;
        bool placed;

        // Feet: where each one is planted, and a step in progress.
        readonly Vector3[] foot = new Vector3[2], stepFrom = new Vector3[2], stepTo = new Vector3[2];
        readonly float[] stepT = { 1f, 1f };
        const float StepTime = 0.24f, StepTrigger = 0.2f;

        public static PlayerBody Create(Transform parent)
        {
            var go = new GameObject("Your body");
            if (parent != null) go.transform.SetParent(parent, false);
            var a = go.AddComponent<PlayerBody>();
            a.Rebuild();
            return a;
        }

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }
        void OnEnable() { Application.onBeforeRender += BeforeRender; }
        void OnDisable() { Application.onBeforeRender -= BeforeRender; }

        /// Build (or rebuild after the driver profile changes) in your kit.
        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Obj.Kill(transform.GetChild(i).gameObject);
            var look = SaveData.Profile;
            mats = DriverBody.Materials(look.Suit, look.SuitAccent, look.Gloves, look.number, "VEYLIX");
            var suit = new[] { mats[0] };
            torso = DriverBody.Part(transform, "Torso", BodyKit.Torso(), suit);
            shoulderL = DriverBody.Part(transform, "ShoulderL", BodyKit.Joint(), suit);
            shoulderR = DriverBody.Part(transform, "ShoulderR", BodyKit.Joint(), suit);
            shoulderL.localScale = shoulderR.localScale = Vector3.one * 0.125f;
            for (int i = 0; i < 2; i++)
            {
                upper[i] = DriverBody.Part(transform, "UpperArm", BodyKit.UpperArmMesh(), suit);
                fore[i] = DriverBody.Part(transform, "Forearm", BodyKit.ForearmMesh(), suit);
                elbow[i] = DriverBody.Part(transform, "Elbow", BodyKit.Joint(), suit);
                elbow[i].localScale = Vector3.one * 0.088f;
                thigh[i] = DriverBody.Part(transform, "Thigh", BodyKit.ThighMesh(), suit);
                shin[i] = DriverBody.Part(transform, "Shin", BodyKit.ShinMesh(), suit);
                knee[i] = DriverBody.Part(transform, "Knee", BodyKit.Joint(), suit);
                knee[i].localScale = Vector3.one * 0.11f;
                boot[i] = DriverBody.Part(transform, "Boot", BodyKit.Boot(), new[] { mats[3], mats[4] });
            }
            placed = false;
            visible = true;
        }

        void LateUpdate() => Pose();
        void BeforeRender() { if (gameObject.activeInHierarchy) Pose(); }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        void Pose()
        {
            var rig = Rig.I;
            bool show = rig != null && rig.head != null && rig.Car == null && Game.I != null && Game.I.state == Game.State.Menu
                        && Game.I.showroom != null && Game.I.showroom.gameObject.activeInHierarchy;
            SetVisible(show);
            if (!show) { placed = false; return; }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 up = Vector3.up;
            Vector3 head = rig.head.position;
            float floor = rig.transform.position.y;
            float eye = head.y - floor;

            // The body follows the head's direction lazily: only once you've turned well away, or as you walk.
            Vector3 look = Flat(rig.head.forward);
            float off = Vector3.Angle(bodyFwd, look);
            float follow = off > 40f ? 6f : off > 15f ? 1.5f : 0.3f;
            bodyFwd = Flat(Vector3.Slerp(bodyFwd, look, 1f - Mathf.Exp(-follow * dt)));
            if (!placed) bodyFwd = look;
            Vector3 right = Vector3.Cross(up, bodyFwd);

            // Torso: from the base of the neck (below and behind your eyes) down to the pelvis. Crouch and the hips drop.
            // (Set a little low and back from where a neck really is, so looking down you see your chest, belly and
            // feet instead of the inside of your own shoulders.)
            Vector3 neck = head - up * 0.21f - bodyFwd * 0.14f;
            float hipH = Mathf.Clamp(eye - 0.6f, 0.42f, 1.0f);
            Vector3 pelvis = new Vector3(neck.x, floor + hipH, neck.z) - bodyFwd * 0.02f;
            Vector3 spine = neck - pelvis;
            float len = Mathf.Max(0.2f, spine.magnitude);
            var rot = Quaternion.LookRotation(spine / len, bodyFwd);
            float sc = Mathf.Clamp(len / (BodyKit.SpineLen * 0.97f), 0.8f, 1.15f);
            torso.SetPositionAndRotation(pelvis, rot);
            torso.localScale = new Vector3(1f, 1f, sc);
            var tm = Matrix4x4.TRS(pelvis, rot, new Vector3(1f, 1f, sc));

            // Arms: shoulders on the torso, hands at your gloves (or hanging relaxed without a headset).
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 sh = tm.MultiplyPoint3x4(new Vector3(-s * 0.172f, -0.008f, BodyKit.SpineLen * 0.84f)); // torso X points left
                (i == 0 ? shoulderL : shoulderR).SetPositionAndRotation(sh, rot);
                var glove = i == 0 ? rig.leftGlove : rig.rightGlove;
                Vector3 target = rig.IsXR && glove != null ? glove.WristPosition : sh - up * 0.52f + bodyFwd * 0.06f + right * s * 0.04f;
                float reach = (target - sh).magnitude, full = BodyKit.UpperArm + BodyKit.Forearm;
                float stretch = Mathf.Clamp(reach / (full * 0.97f), 1f, 1.35f);
                float ua = BodyKit.UpperArm * stretch, fa = BodyKit.Forearm * stretch;
                Vector3 el = DriverBody.SolveJoint(sh, target, ua, fa, right * s * 0.6f - up - bodyFwd * 0.3f);
                Vector3 toHand = target - el;
                Vector3 wrist = toHand.magnitude > fa ? el + toHand.normalized * fa : target;
                DriverBody.Place(upper[i], sh, el, BodyKit.UpperArm);
                DriverBody.Place(fore[i], el, wrist, BodyKit.Forearm);
                elbow[i].position = el;
            }

            // Feet stay planted until the hips move away from them, then step (one foot at a time).
            bool teleported = placed && (pelvis - lastPelvis).sqrMagnitude > 1.5f * 1.5f;
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 rest = new Vector3(pelvis.x, floor, pelvis.z) + right * s * 0.11f - bodyFwd * 0.01f;
                if (!placed || teleported) { foot[i] = rest; stepT[i] = 1f; continue; }
                if (stepT[i] < 1f)
                {
                    stepT[i] = Mathf.Min(1f, stepT[i] + dt / StepTime);
                    stepTo[i] = rest; // aim where the hip is now, so a walking stride keeps up
                    float t = Mathf.SmoothStep(0f, 1f, stepT[i]);
                    foot[i] = Vector3.Lerp(stepFrom[i], stepTo[i], t) + up * Mathf.Sin(t * Mathf.PI) * 0.07f;
                }
                else
                {
                    foot[i].y = floor;
                    bool otherDown = stepT[1 - i] >= 1f;
                    if (otherDown && (foot[i] - rest).sqrMagnitude > StepTrigger * StepTrigger)
                    {
                        stepFrom[i] = foot[i];
                        stepTo[i] = rest;
                        stepT[i] = 0f;
                    }
                }
            }
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 hip = pelvis + right * s * 0.09f - up * 0.02f;
                Vector3 heel = foot[i] + up * 0.043f;
                Vector3 toeDir = Quaternion.AngleAxis(s * 7f, up) * bodyFwd;
                Vector3 ankle = heel + up * 0.082f - toeDir * 0.005f;
                Vector3 kn = DriverBody.SolveJoint(hip, ankle, BodyKit.Thigh, BodyKit.Shin, bodyFwd + right * s * 0.1f);
                Limb(thigh[i], hip, kn, BodyKit.Thigh, bodyFwd);
                Limb(shin[i], kn, ankle, BodyKit.Shin, bodyFwd);
                knee[i].position = kn;
                knee[i].rotation = Quaternion.LookRotation(kn - hip, bodyFwd);
                boot[i].SetPositionAndRotation(heel, Quaternion.LookRotation(toeDir, up));
            }
            lastPelvis = pelvis;
            placed = true;
        }

        /// A limb mesh (built along +Z, length len0, its front on +Y) stretched from a to b with its front facing 'front'.
        static void Limb(Transform t, Vector3 a, Vector3 b, float len0, Vector3 front)
        {
            Vector3 d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            t.SetPositionAndRotation(a, Quaternion.LookRotation(d / len, front));
            t.localScale = new Vector3(1f, 1f, len / len0);
        }

        bool visible = true;
        void SetVisible(bool v)
        {
            if (v == visible) return;
            visible = v;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        }
    }
}
