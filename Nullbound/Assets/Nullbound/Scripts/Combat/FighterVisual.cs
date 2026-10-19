using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// How a fighter looks in the arena (the body itself comes from <see cref="FighterModel"/>). Other players and
    /// bots get the full model with their name over it, and a screen on their RIGHT FOREARM showing health and super
    /// charge (the same place your own display is, so everyone knows where to look). You only see your own gloves, on your real hands.
    /// Rebuilds if they switch fighter. Invisible moderators aren't drawn for anyone else.
    /// </summary>
    public class FighterVisual : MonoBehaviour
    {
        Fighter fighter;
        FighterKind builtKind = (FighterKind)255;
        bool firstPerson;
        FighterModel model;
        Transform plate, armPanel, hpFill, superFill;
        Renderer hpRenderer, superRenderer;
        Material hpGood, hpLow, superMat, superReadyMat;
        TextMeshPro nameText;
        Vector3 lastPos, smoothVel, rollAxis = Vector3.right;
        float tumbleBlend;
        Quaternion tumbleRot = Quaternion.identity;

        public void Init(Fighter f)
        {
            fighter = f;
            lastPos = transform.position;
        }

        void Rebuild()
        {
            builtKind = fighter.Kind;
            firstPerson = fighter.IsMine && !fighter.IsBot;
            if (model != null)
            {
                if (model.leftGlove != null) Destroy(model.leftGlove.gameObject);
                if (model.rightGlove != null) Destroy(model.rightGlove.gameObject);
                if (model.root != null) Destroy(model.root.gameObject);
            }
            if (plate != null) Destroy(plate.gameObject);

            model = FighterModel.Build(transform, fighter.Stats);
            if (firstPerson)
            {
                // Just your gloves, riding on your real hands (no lag at all).
                Attach(model.leftGlove, fighter.poseLeft);
                Attach(model.rightGlove, fighter.poseRight);
                model.root.gameObject.SetActive(false);
                return;
            }

            plate = new GameObject("NamePlate").transform;
            plate.SetParent(transform, false);
            plate.gameObject.AddComponent<FaceCamera>();
            nameText = NBUI.Label(plate, "", new Vector3(0f, 0.05f, 0f), 1.6f, Color.white, new Vector2(2.4f, 0.25f));

            // The arm screen: a gold frame strapped to the outside of the right forearm, health on top, super below.
            armPanel = new GameObject("ArmScreen").transform;
            armPanel.SetParent(model.root, false);
            float k = model.Scale;
            NBUI.Part(armPanel, PrimitiveType.Cube, new Vector3(0f, 0f, 0.012f), new Vector3(0.24f, 0.1f, 0.012f) * k, NBUI.Glow(new Color(1f, 0.75f, 0.2f)), default, "Frame");
            NBUI.Part(armPanel, PrimitiveType.Cube, new Vector3(0f, 0f, 0.004f), new Vector3(0.225f, 0.085f, 0.008f) * k, NBUI.Mat(new Color(0.06f, 0.06f, 0.09f)), default, "Back");
            hpGood = NBUI.Glow(new Color(0.3f, 1f, 0.45f));
            hpLow = NBUI.Glow(new Color(1f, 0.25f, 0.2f));
            superMat = NBUI.Glow(new Color(1f, 0.7f, 0.15f));
            superReadyMat = NBUI.Glow(new Color(1f, 0.95f, 0.5f));
            hpFill = ArmBar(0.018f * k, 0.03f * k, k, hpGood, out hpRenderer);
            superFill = ArmBar(-0.022f * k, 0.022f * k, k, superMat, out superRenderer);
        }

        Transform ArmBar(float y, float height, float k, Material fill, out Renderer r)
        {
            float width = 0.2f * k;
            NBUI.Part(armPanel, PrimitiveType.Cube, new Vector3(0f, y, -0.001f), new Vector3(width, height, 0.004f), NBUI.Mat(new Color(0.18f, 0.18f, 0.22f)), default, "Track");
            var pivot = new GameObject("Fill").transform;
            pivot.SetParent(armPanel, false);
            pivot.localPosition = new Vector3(-width * 0.5f, y, -0.004f);
            r = NBUI.Part(pivot, PrimitiveType.Cube, new Vector3(0.5f, 0f, 0f), Vector3.one, fill).GetComponent<Renderer>();
            pivot.localScale = new Vector3(width, height, 0.004f);
            return pivot;
        }

        static void SetFill(Transform pivot, float t, float width)
        {
            var s = pivot.localScale;
            s.x = width * Mathf.Clamp(t, 0.001f, 1f);
            pivot.localScale = s;
        }

        /// <summary>Keeps the arm screen on the outside of the right forearm, bars running along the arm.</summary>
        void PlaceArmScreen()
        {
            var fore = model.BoneOf(Bone.ForearmR);
            var hand = model.BoneOf(Bone.HandR);
            Vector3 elbow = fore.position, wrist = hand.position;
            Vector3 arm = wrist - elbow;
            if (arm.sqrMagnitude < 0.0001f) return;
            arm.Normalize();
            // "Outside" = away from the body; fall back to the forearm's own side if the arm points that way.
            Vector3 outward = Vector3.Cross(Vector3.up, fighter.FacingFlat);
            outward -= arm * Vector3.Dot(outward, arm);
            if (outward.sqrMagnitude < 0.01f) outward = fore.right;
            outward.Normalize();
            float k = model.Scale;
            armPanel.position = Vector3.Lerp(elbow, wrist, 0.45f) + outward * (model.body.armThick * 0.6f + 0.02f) * k;
            Vector3 face = -outward; // the screen's front (-Z) points out from the arm
            armPanel.rotation = Quaternion.LookRotation(face, Vector3.Cross(face, arm));
        }

        static void Attach(Transform glove, Transform hand)
        {
            if (glove == null || hand == null) return;
            glove.SetParent(hand, false);
            glove.localPosition = Vector3.zero;
            glove.localRotation = Quaternion.identity;
        }

        void OnDestroy()
        {
            // First-person gloves live on the rig's hands, not under this object.
            if (model != null && firstPerson)
            {
                if (model.leftGlove != null) Destroy(model.leftGlove.gameObject);
                if (model.rightGlove != null) Destroy(model.rightGlove.gameObject);
            }
        }

        void LateUpdate()
        {
            if (fighter == null || !fighter.IsReady) return;
            bool fp = fighter.IsMine && !fighter.IsBot;
            if (fighter.Kind != builtKind || fp != firstPerson || model == null) Rebuild();

            bool empowered = fighter.Empowered;
            float pulse = empowered ? 1f + 0.15f * Mathf.Sin(Time.time * 25f) : 1f;
            foreach (var r in model.gloveRenderers) if (r != null) r.sharedMaterial = empowered ? model.gloveGlow : model.glove;

            if (firstPerson)
            {
                bool show = !fighter.Down;
                if (model.leftGlove != null) { model.leftGlove.gameObject.SetActive(show); model.leftGlove.localScale = Vector3.one * pulse; }
                if (model.rightGlove != null) { model.rightGlove.gameObject.SetActive(show); model.rightGlove.localScale = Vector3.one * pulse; }
                return;
            }

            bool ghost = fighter.Ghost;
            model.SetVisible(!ghost);
            plate.gameObject.SetActive(!ghost && !fighter.Down);
            if (ghost) return;

            Vector3 feet = transform.position;
            Vector3 vel = (feet - lastPos) / Mathf.Max(Time.deltaTime, 0.001f);
            lastPos = feet;
            smoothVel = Vector3.Lerp(smoothVel, vel, Mathf.Clamp01(Time.deltaTime * 12f));
            float speed = new Vector3(smoothVel.x, 0f, smoothVel.z).magnitude;
            bool air = Mathf.Abs(smoothVel.y) > 0.5f;

            // Ragdoll tumble after a super hit: the body rolls over the way it's flying, limbs flailing.
            bool tumbling = fighter.HasStatus(Fighter.Tumbling) && !fighter.Down;
            tumbleBlend = Mathf.MoveTowards(tumbleBlend, tumbling ? 1f : 0f, Time.deltaTime * (tumbling ? 8f : 3f));
            if (tumbleBlend > 0f)
            {
                Vector3 flatVel = new Vector3(smoothVel.x, 0f, smoothVel.z);
                if (flatVel.sqrMagnitude > 0.25f) rollAxis = Vector3.Cross(Vector3.up, flatVel.normalized);
                // Roll like a ball of about 0.45 m, never slower than a lazy spin.
                float rollSpeed = Mathf.Max(flatVel.magnitude / 0.45f, 4f) * Mathf.Rad2Deg;
                if (tumbling) tumbleRot = Quaternion.AngleAxis(rollSpeed * Time.deltaTime, rollAxis) * tumbleRot;
                else tumbleRot = Quaternion.Slerp(tumbleRot, Quaternion.identity, Time.deltaTime * 6f);
            }
            else tumbleRot = Quaternion.identity;

            model.handPulse = pulse;
            model.Pose(new PoseInput
            {
                feet = feet, headPos = fighter.HeadPos, headRot = fighter.HeadRot,
                leftPos = fighter.LeftPos, leftRot = fighter.LeftRot, rightPos = fighter.RightPos, rightRot = fighter.RightRot,
                fwd = fighter.FacingFlat, velocity = smoothVel, speed = speed, down = fighter.Down, airborne = air, blocking = fighter.Blocking,
                tumble = tumbleBlend, tumbleRot = tumbleRot,
            }, Time.deltaTime);

            var st = fighter.Stats;
            string rarity = "<color=#" + ColorUtility.ToHtmlStringRGB(FighterInfo.RarityColor(st.rarity)) + ">" + st.name + "</color>";
            nameText.text = (fighter.IsBot ? "<color=#ffcc66>BOT</color> " : "") + fighter.DisplayName + "  <size=70%>" + rarity + "</size>" +
                            (fighter.Streak >= 3 ? "  <color=#ff7a3d>" + fighter.Streak + " streak</color>" : "") +
                            (fighter.IsVerifiedMod ? "  <color=#ff5a4a>MOD</color>" : "");
            plate.position = (fighter.Down ? feet + Vector3.up * 1.2f : fighter.HeadPos + Vector3.up * 0.38f);

            // Health and super on the right forearm.
            PlaceArmScreen();
            float w = 0.2f * model.Scale;
            float hp = fighter.HealthFraction;
            SetFill(hpFill, hp, w);
            hpRenderer.sharedMaterial = hp < 0.3f ? hpLow : hpGood;
            float charge = fighter.SuperCharge / 100f;
            SetFill(superFill, charge, w);
            bool ready = charge >= 0.999f;
            superRenderer.sharedMaterial = ready && Mathf.Repeat(Time.time * 3f, 1f) < 0.6f ? superReadyMat : superMat;
        }
    }
}
