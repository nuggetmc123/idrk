using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// How a fighter looks in the arena (the body itself comes from <see cref="FighterModel"/>). Other players and
    /// bots get the full model with a name and health bar over it. You only see your own gloves, on your real hands.
    /// Rebuilds if they switch fighter. Invisible moderators aren't drawn for anyone else.
    /// </summary>
    public class FighterVisual : MonoBehaviour
    {
        Fighter fighter;
        FighterKind builtKind = (FighterKind)255;
        bool firstPerson;
        FighterModel model;
        Transform plate, hpFill;
        TextMeshPro nameText;
        Vector3 lastPos;

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
            nameText = NBUI.Label(plate, "", new Vector3(0f, 0.12f, 0f), 1.6f, Color.white, new Vector2(2.4f, 0.25f));
            NBUI.Part(plate, PrimitiveType.Cube, Vector3.zero, new Vector3(0.7f, 0.06f, 0.01f), NBUI.Mat(new Color(0.1f, 0.1f, 0.12f)), default, "HpBack");
            hpFill = NBUI.Part(plate, PrimitiveType.Cube, new Vector3(0f, 0f, -0.006f), new Vector3(0.7f, 0.05f, 0.01f), NBUI.Glow(new Color(0.3f, 1f, 0.4f)), default, "HpFill");
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
            foreach (var r in model.gloveRenderers) if (r != null) r.sharedMaterial = empowered ? model.gloveGlow : (fighter.Stats.glove == GloveType.Glow ? model.gloveGlow : model.main);

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
            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            bool air = Mathf.Abs(vel.y) > 0.5f;
            model.Pose(feet, fighter.HeadPos, fighter.HeadRot, fighter.LeftPos, fighter.LeftRot, fighter.RightPos, fighter.RightRot,
                fighter.FacingFlat, speed, fighter.Down, fighter.Blocking, air, Time.deltaTime);
            float gloveScale = 1.3f * pulse * (fighter.Stats.glove == GloveType.Big ? 1.15f : 1f);
            model.leftGlove.localScale = model.rightGlove.localScale = Vector3.one * gloveScale;

            plate.position = fighter.HeadPos + Vector3.up * 0.45f;
            var st = fighter.Stats;
            string rarity = "<color=#" + ColorUtility.ToHtmlStringRGB(FighterInfo.RarityColor(st.rarity)) + ">" + st.name + "</color>";
            nameText.text = (fighter.IsBot ? "<color=#ffcc66>BOT</color> " : "") + fighter.DisplayName + "  <size=70%>" + rarity + "</size>" +
                            (fighter.Streak >= 3 ? "  <color=#ff7a3d>" + fighter.Streak + " streak</color>" : "") +
                            (fighter.IsVerifiedMod ? "  <color=#ff5a4a>MOD</color>" : "");
            float hp = fighter.HealthFraction;
            hpFill.localScale = new Vector3(0.7f * Mathf.Max(0.001f, hp), 0.05f, 0.01f);
            hpFill.localPosition = new Vector3(-0.35f * (1f - hp), 0f, -0.006f);
        }
    }
}
