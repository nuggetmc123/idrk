using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// How a fighter looks, built from simple shapes at runtime (cheap on Quest). Other players and bots get a full
    /// stylized body that follows their head and hands, with a name and health bar over it. You only see your own
    /// big gloves on your hands. Colours come from the fighter's kind and update if they switch fighter.
    /// </summary>
    public class FighterVisual : MonoBehaviour
    {
        Fighter fighter;
        FighterKind builtKind = (FighterKind)255;
        bool firstPerson;

        Transform body, head, torso, hips, leftArm, rightArm, leftGlove, rightGlove, leftLeg, rightLeg, shield, plate;
        Transform hpFill, hpBack;
        TextMeshPro nameText;
        Renderer[] gloveRenderers;
        Material main, accent, dark, skin, gloveGlow;
        float walkPhase;
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
            if (body != null) Destroy(body.gameObject);
            if (leftGlove != null) Destroy(leftGlove.gameObject);
            if (rightGlove != null) Destroy(rightGlove.gameObject);
            if (plate != null) Destroy(plate.gameObject);

            var s = fighter.Stats;
            main = NBUI.Mat(s.main, 0.55f, 0.1f);
            accent = NBUI.Glow(s.accent);
            dark = NBUI.Mat(new Color(0.12f, 0.12f, 0.15f), 0.4f);
            skin = NBUI.Mat(new Color(0.95f, 0.8f, 0.68f), 0.3f);
            gloveGlow = NBUI.Glow(Color.Lerp(s.main, Color.white, 0.4f));

            if (firstPerson)
            {
                // Just your gloves, riding on your real hands (no lag at all).
                leftGlove = Glove(fighter.poseLeft != null ? fighter.poseLeft : transform);
                rightGlove = Glove(fighter.poseRight != null ? fighter.poseRight : transform);
                gloveRenderers = new[] { leftGlove.GetChild(0).GetComponent<Renderer>(), rightGlove.GetChild(0).GetComponent<Renderer>() };
                return;
            }

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            hips = NBUI.Part(body, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.36f, 0.2f, 0.26f), dark, default, "Hips");
            torso = NBUI.Part(body, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.44f, 0.32f, 0.3f), main, default, "Torso");
            NBUI.Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0.42f), new Vector3(0.5f, 0.12f, 0.12f), accent, default, "ChestStripe");
            head = new GameObject("Head").transform;
            head.SetParent(body, false);
            NBUI.Part(head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), skin, default, "Face");
            NBUI.Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.075f), new Vector3(0.21f, 0.07f, 0.12f), accent, default, "Visor");
            // Spiky anime hair.
            for (int i = 0; i < 7; i++)
            {
                float a = (i - 3) * 22f;
                NBUI.Part(head, PrimitiveType.Cube, Quaternion.Euler(-30f + Mathf.Abs(i - 3) * 6f, a, 0f) * new Vector3(0f, 0.11f, -0.04f),
                    new Vector3(0.06f, 0.2f, 0.06f), fighter.Kind == FighterKind.Speedster ? accent : dark, new Vector3(-35f, a, (i - 3) * -8f), "Spike");
            }
            leftArm = NBUI.Part(body, PrimitiveType.Capsule, Vector3.zero, Vector3.one, main, default, "LeftArm");
            rightArm = NBUI.Part(body, PrimitiveType.Capsule, Vector3.zero, Vector3.one, main, default, "RightArm");
            leftLeg = NBUI.Part(body, PrimitiveType.Capsule, Vector3.zero, Vector3.one, dark, default, "LeftLeg");
            rightLeg = NBUI.Part(body, PrimitiveType.Capsule, Vector3.zero, Vector3.one, dark, default, "RightLeg");
            leftGlove = Glove(body);
            rightGlove = Glove(body);
            gloveRenderers = new[] { leftGlove.GetChild(0).GetComponent<Renderer>(), rightGlove.GetChild(0).GetComponent<Renderer>() };
            shield = NBUI.Part(body, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.55f, 0.01f, 0.55f), NBUI.Glow(new Color(0.4f, 0.75f, 1f)), default, "Shield");
            shield.gameObject.SetActive(false);

            // Name and health bar over the head.
            plate = new GameObject("NamePlate").transform;
            plate.SetParent(transform, false);
            plate.gameObject.AddComponent<FaceCamera>();
            nameText = NBUI.Label(plate, "", new Vector3(0f, 0.12f, 0f), 1.6f, Color.white, new Vector2(2f, 0.25f));
            hpBack = NBUI.Part(plate, PrimitiveType.Cube, Vector3.zero, new Vector3(0.7f, 0.06f, 0.01f), dark, default, "HpBack");
            hpFill = NBUI.Part(plate, PrimitiveType.Cube, new Vector3(0f, 0f, -0.006f), new Vector3(0.7f, 0.05f, 0.01f), NBUI.Glow(new Color(0.3f, 1f, 0.4f)), default, "HpFill");
        }

        Transform Glove(Transform parent)
        {
            var g = new GameObject("Glove").transform;
            g.SetParent(parent, false);
            NBUI.Part(g, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.02f), new Vector3(0.13f, 0.12f, 0.15f), main, default, "Fist");
            NBUI.Part(g, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.085f), new Vector3(0.12f, 0.035f, 0.03f), accent, default, "Knuckles");
            NBUI.Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.06f), new Vector3(0.1f, 0.03f, 0.1f), dark, new Vector3(90f, 0f, 0f), "Cuff");
            return g;
        }

        void LateUpdate()
        {
            if (fighter == null || !fighter.IsReady) return;
            if (fighter.Kind != builtKind || (fighter.IsMine && !fighter.IsBot) != firstPerson) Rebuild();

            bool empowered = fighter.Empowered;
            float pulse = empowered ? 1f + 0.15f * Mathf.Sin(Time.time * 25f) : 1f;
            if (gloveRenderers != null)
                foreach (var r in gloveRenderers) if (r != null) r.sharedMaterial = empowered ? gloveGlow : main;

            if (firstPerson)
            {
                bool showGloves = !fighter.Down;
                leftGlove.gameObject.SetActive(showGloves);
                rightGlove.gameObject.SetActive(showGloves);
                leftGlove.localScale = rightGlove.localScale = Vector3.one * pulse;
                return;
            }

            Pose();
            leftGlove.localScale = rightGlove.localScale = Vector3.one * 1.3f * pulse;

            // Name plate.
            plate.position = fighter.HeadPos + Vector3.up * 0.42f;
            nameText.text = (fighter.IsBot ? "<color=#ffcc66>BOT</color> " : "") + fighter.DisplayName +
                            (fighter.Streak >= 3 ? "  <color=#ff7a3d>" + fighter.Streak + " streak</color>" : "");
            float hp = fighter.HealthFraction;
            hpFill.localScale = new Vector3(0.7f * Mathf.Max(0.001f, hp), 0.05f, 0.01f);
            hpFill.localPosition = new Vector3(-0.35f * (1f - hp), 0f, -0.006f);
            plate.gameObject.SetActive(!fighter.Down);
        }

        /// <summary>Lay the body out under the head, arms reaching to the gloves, legs stepping as it moves.</summary>
        void Pose()
        {
            Vector3 feet = transform.position;
            Vector3 headPos = fighter.HeadPos;
            Vector3 fwd = fighter.FacingFlat, right = Vector3.Cross(Vector3.up, fwd);
            Quaternion yaw = Quaternion.LookRotation(fwd);

            float height = Mathf.Clamp(headPos.y - feet.y, 0.9f, 2.1f);
            Vector3 neck = headPos + Vector3.down * 0.2f - fwd * 0.05f;
            Vector3 chest = neck + Vector3.down * 0.28f;
            Vector3 pelvis = new Vector3(headPos.x, feet.y + height * 0.52f, headPos.z) - fwd * 0.05f;

            if (fighter.Down)
            {
                // Knocked out: lie flat on the floor.
                body.position = feet;
                body.rotation = yaw;
                head.position = feet + fwd * 0.75f + Vector3.up * 0.15f;
                head.rotation = yaw * Quaternion.Euler(-80f, 0f, 0f);
                torso.position = feet + fwd * 0.35f + Vector3.up * 0.15f;
                torso.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);
                hips.position = feet + Vector3.up * 0.15f;
                foreach (var t in new[] { leftArm, rightArm, leftGlove, rightGlove, leftLeg, rightLeg }) t.gameObject.SetActive(false);
                shield.gameObject.SetActive(false);
                return;
            }
            foreach (var t in new[] { leftArm, rightArm, leftGlove, rightGlove, leftLeg, rightLeg }) t.gameObject.SetActive(true);

            head.position = headPos;
            head.rotation = fighter.HeadRot;
            torso.position = (neck + pelvis) * 0.5f + Vector3.up * 0.05f;
            torso.rotation = Quaternion.FromToRotation(Vector3.up, (neck - pelvis).normalized) * yaw;
            torso.localScale = new Vector3(0.44f, Mathf.Max(0.2f, (neck - pelvis).magnitude * 0.55f), 0.3f);
            hips.position = pelvis;
            hips.rotation = yaw;

            // Arms from the shoulders to the gloves.
            Vector3 ls = chest - right * 0.22f + Vector3.up * 0.06f, rs = chest + right * 0.22f + Vector3.up * 0.06f;
            leftGlove.position = fighter.LeftPos;
            leftGlove.rotation = fighter.LeftRot;
            rightGlove.position = fighter.RightPos;
            rightGlove.rotation = fighter.RightRot;
            NBUI.Span(leftArm, ls, Vector3.MoveTowards(fighter.LeftPos, ls, 0.06f), 0.11f);
            NBUI.Span(rightArm, rs, Vector3.MoveTowards(fighter.RightPos, rs, 0.06f), 0.11f);

            // Legs: a simple stepping cycle from how fast the body moves.
            Vector3 vel = (feet - lastPos) / Mathf.Max(Time.deltaTime, 0.001f);
            lastPos = feet;
            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            walkPhase += speed * Time.deltaTime * 3.2f;
            float swing = Mathf.Clamp01(speed / 4f) * 0.32f;
            bool air = feet.y > 0.02f && Mathf.Abs(vel.y) > 0.5f;
            Vector3 lh = pelvis - right * 0.11f, rh = pelvis + right * 0.11f;
            Vector3 lf = new Vector3(lh.x, feet.y + (air ? 0.35f : 0.05f), lh.z) + fwd * Mathf.Sin(walkPhase) * swing;
            Vector3 rf = new Vector3(rh.x, feet.y + (air ? 0.25f : 0.05f), rh.z) - fwd * Mathf.Sin(walkPhase) * swing;
            NBUI.Span(leftLeg, lh, lf, 0.15f);
            NBUI.Span(rightLeg, rh, rf, 0.15f);

            // A glowing guard between the gloves while blocking.
            bool blocking = fighter.Blocking;
            shield.gameObject.SetActive(blocking);
            if (blocking)
            {
                shield.position = (fighter.LeftPos + fighter.RightPos) * 0.5f + fwd * 0.08f;
                shield.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }
}
