using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// How a fighter looks in the arena (the body itself comes from <see cref="FighterModel"/>). Other players and
    /// bots get the full model with their name over it, and a screen on their RIGHT FOREARM showing health and super
    /// charge (the same place your own display is, so everyone knows where to look). Your own fighter shows your full
    /// body without the head (Settings > Body), with the gloves exactly on your real hands. You only see your own gloves, on your real hands.
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
        Vector3 lastPos, smoothVel;
        float nextTrail;
        Transform stars;
        bool headWasOff;
        // The last hit shown on this fighter: a ragdoll that starts just after gets knocked at that spot.
        Vector3 lastHitPoint, lastHitDir;
        float lastHitStrength, lastHitAt = -10f;

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
                model.Dispose();
                if (model.leftGlove != null) Destroy(model.leftGlove.gameObject);
                if (model.rightGlove != null) Destroy(model.rightGlove.gameObject);
                if (model.root != null) Destroy(model.root.gameObject);
            }
            if (plate != null) Destroy(plate.gameObject);

            model = FighterModel.Build(transform, fighter.Stats);
            if (firstPerson)
            {
                // Your gloves ride your real hands (no lag at all). The rest of your body hangs under your head so you
                // see your arms, chest and legs when you look down; your own head is hidden so it can't block the view.
                Attach(model.leftGlove, fighter.poseLeft);
                Attach(model.rightGlove, fighter.poseRight);
                model.BoneOf(Bone.Head).gameObject.SetActive(false);
                model.BoneOf(Bone.Neck).gameObject.SetActive(false);
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

        Transform MakeStars()
        {
            var t = new GameObject("DazedStars").transform;
            t.SetParent(transform, false);
            var gold = NBUI.Glow(new Color(1f, 0.9f, 0.3f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                NBUI.Part(t, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.2f, Mathf.Sin(a * 2f) * 0.03f, Mathf.Sin(a) * 0.2f), Vector3.one * 0.05f, gold, new Vector3(0f, 0f, 45f), "Star");
                NBUI.Part(t, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.2f, Mathf.Sin(a * 2f) * 0.03f, Mathf.Sin(a) * 0.2f), Vector3.one * 0.05f, gold, new Vector3(45f, 0f, 0f), "Star");
            }
            return t;
        }

        /// <summary>Every fighter's solid colliders (their character controllers), which ragdolls must not push against.</summary>
        static readonly List<Collider> characterColliders = new List<Collider>();
        static List<Collider> CharacterColliders()
        {
            characterColliders.Clear();
            foreach (var f in Fighter.All)
                if (f != null)
                    foreach (var c in f.GetComponentsInChildren<Collider>())
                        if (!c.isTrigger) characterColliders.Add(c);
            return characterColliders;
        }

        /// <summary>
        /// A hit shown on this fighter (everyone sees it): the body reacts physically, snapping back from the point
        /// of impact, harder for bigger hits (or the nearest limb of a ragdoll gets knocked).
        /// </summary>
        public void ShowHit(Vector3 point, float damage, bool blocked)
        {
            if (model == null) return;
            Vector3 dir = fighter.Center - point;
            dir.y *= 0.3f;
            if (dir.sqrMagnitude < 0.0001f) dir = -fighter.FacingFlat;
            float strength = (blocked ? 0.4f : 0.9f) + damage * (blocked ? 0.02f : 0.07f);
            lastHitPoint = point; lastHitDir = dir; lastHitStrength = strength; lastHitAt = Time.time;
            // Your own body only reacts once it's a ragdoll (otherwise it's your real head and hands).
            if (!firstPerson || model.Ragdolled) model.Flinch(point, dir, strength);
        }

        /// <summary>
        /// Go limp. Every part starts moving with the hit's launch (on the fighter's own game the exact knockback, on others
        /// the speed it's seen moving at), and the spot that was just hit gets knocked, so it falls the way the hit
        /// sends it. Nothing is animated. free = the fighter's own game: no spring, the fighter follows this body.
        /// </summary>
        void GoLimp(bool free)
        {
            Vector3 v = fighter.IsMine && Time.time - fighter.LaunchAt < 0.5f ? fighter.LaunchVelocity : smoothVel;
            model.StartRagdoll(v, CharacterColliders(), free);
            if (model.Ragdolled && Time.time - lastHitAt < 0.6f) model.Flinch(lastHitPoint, lastHitDir, lastHitStrength);
        }

        /// <summary>The ragdoll's feet (under its hips): where the fighter's own game puts the fighter while ragdolled.</summary>
        public bool TryGetRagdollFeet(out Vector3 feet)
        {
            feet = default;
            if (model == null || !model.TryGetRagdollHips(out var hips, out _)) return false;
            feet = hips - Vector3.up * 0.25f * model.Scale;
            return true;
        }

        /// <summary>Your own first-person gloves: on your real hands normally, on your ragdoll while you're limp.</summary>
        void GlovesOnBody(bool onBody)
        {
            foreach (var (glove, hand) in new[] { (model.leftGlove, fighter.poseLeft), (model.rightGlove, fighter.poseRight) })
            {
                if (glove == null) continue;
                if (onBody) { glove.SetParent(model.root, true); glove.gameObject.SetActive(true); }
                else Attach(glove, hand);
            }
        }

        /// <summary>
        /// You got ragdolled: your whole body (gloves included) goes limp and your view rides in its head, so you see
        /// yourself fly and tumble (Settings > Ragdoll cam: inside / level / off). Returns false when you're not limp.
        /// </summary>
        bool FirstPersonRagdoll(bool limp)
        {
            if (limp && !model.Ragdolled && fighter.poseHead != null && fighter.poseLeft != null && fighter.poseRight != null)
            {
                model.root.gameObject.SetActive(true);
                PoseFirstPerson(); // start from where your body really is right now
                GlovesOnBody(true);
                GoLimp(true);
                if (!model.Ragdolled) GlovesOnBody(false);
            }
            if (!model.Ragdolled) return false;
            if (!limp)
            {
                model.StopRagdoll(true);
                GlovesOnBody(false);
                return false;
            }
            model.Pose(new PoseInput { feet = transform.position, velocity = smoothVel, down = fighter.Down }, Time.deltaTime);
            if (GameSettings.RagdollCam != RagdollCamera.Off && VRRig.Instance != null)
            {
                model.EyePose(out var eye, out var rot);
                VRRig.Instance.SetRagdollView(eye, rot, GameSettings.RagdollCam == RagdollCamera.Inside);
            }
            return true;
        }

        /// <summary>Your first-person body under your real head and hands.</summary>
        void PoseFirstPerson()
        {
            Vector3 facing = fighter.FacingFlat;
            var head = fighter.poseHead;
            model.Pose(new PoseInput
            {
                // Body a little behind your eyes (where your real neck is), so looking down doesn't put you inside your chest.
                feet = transform.position, headPos = head.position - facing * 0.14f, headRot = head.rotation,
                leftPos = fighter.poseLeft.position, leftRot = fighter.poseLeft.rotation,
                rightPos = fighter.poseRight.position, rightRot = fighter.poseRight.rotation,
                fwd = facing, velocity = smoothVel, speed = new Vector3(smoothVel.x, 0f, smoothVel.z).magnitude,
                airborne = Mathf.Abs(smoothVel.y) > 0.5f, blocking = false,
            }, Time.deltaTime);
            // Your own neck and head are shrunk to nothing so the skin can't show up in front of your eyes.
            model.BoneOf(Bone.Neck).localScale = Vector3.one * 0.001f;
            model.BoneOf(Bone.Head).localScale = Vector3.one * 0.001f;
        }

        /// <summary>A copy of their head flies off, tumbling, and rolls away.</summary>
        static void ThrowHead(Transform headBone)
        {
            var copy = Instantiate(headBone.gameObject, headBone.position, headBone.rotation);
            copy.SetActive(true);
            copy.transform.localScale = headBone.lossyScale;
            var life = copy.AddComponent<FxLife>();
            life.life = 6f;
            life.startScale = life.endScale = copy.transform.localScale;
            life.shrinkAtEnd = true;
            life.gravity = true;
            life.velocity = Vector3.up * Random.Range(4f, 6.5f) + Random.insideUnitSphere * 2.5f;
            life.spin = Random.insideUnitSphere * 540f;
            life.drag = 0.4f;
            copy.AddComponent<HeadBounce>();
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
            model?.Dispose();
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
                Vector3 at = transform.position;
                Vector3 v = (at - lastPos) / Mathf.Max(Time.deltaTime, 0.001f);
                lastPos = at;
                smoothVel = Vector3.Lerp(smoothVel, v, Mathf.Clamp01(Time.deltaTime * 12f));
                if (FirstPersonRagdoll(fighter.HasStatus(Fighter.Tumbling) || fighter.Down)) return;
                bool show = !fighter.Down;
                if (model.leftGlove != null) { model.leftGlove.gameObject.SetActive(show); model.leftGlove.localScale = Vector3.one * pulse; }
                if (model.rightGlove != null) { model.rightGlove.gameObject.SetActive(show); model.rightGlove.localScale = Vector3.one * pulse; }
                model.SetCurl(fighter.HandCurl(true), fighter.HandCurl(false));
                bool body = show && GameSettings.FullBody && fighter.poseHead != null && fighter.poseLeft != null && fighter.poseRight != null;
                model.root.gameObject.SetActive(body);
                if (body) PoseFirstPerson();
                return;
            }

            bool ghost = fighter.Ghost;
            model.SetVisible(!ghost);
            plate.gameObject.SetActive(!ghost && !fighter.Down);
            if (ghost) return;

            Vector3 feet = transform.position;
            // A teleport (respawn, switching map): stand straight up there, no ragdoll sliding across the map.
            bool teleported = (feet - lastPos).sqrMagnitude > 9f;
            if (teleported) { model.StopRagdoll(true); smoothVel = Vector3.zero; lastPos = feet; }
            Vector3 vel = (feet - lastPos) / Mathf.Max(Time.deltaTime, 0.001f);
            lastPos = feet;
            smoothVel = Vector3.Lerp(smoothVel, vel, Mathf.Clamp01(Time.deltaTime * 12f));
            float speed = new Vector3(smoothVel.x, 0f, smoothVel.z).magnitude;
            bool air = Mathf.Abs(smoothVel.y) > 0.5f;

            // Physics ragdoll: knocked flying by a special, thrown, or knocked out. Gets back up when it's over. On the
            // fighter's own game (bots) the ragdoll runs free and the fighter follows it; elsewhere it follows the fighter.
            bool limp = fighter.HasStatus(Fighter.Tumbling) || fighter.Down;
            if (limp && !model.Ragdolled && !teleported) GoLimp(fighter.IsMine);
            else if (!limp && model.Ragdolled) model.StopRagdoll(false);

            // Flying: a trail of after-images behind them.
            if (fighter.HasStatus(Fighter.Flying) && Time.time >= nextTrail)
            {
                nextTrail = Time.time + 0.06f;
                Fx.Afterimage(feet, fighter.HeadPos, fighter.Stats.accent);
            }
            // Dazed: stars spinning round their head.
            bool dazed = fighter.IsDazed && !fighter.Down;
            if (dazed && stars == null) stars = MakeStars();
            if (stars != null)
            {
                stars.gameObject.SetActive(dazed);
                if (dazed) { stars.position = fighter.HeadPos + Vector3.up * 0.22f; stars.Rotate(0f, 300f * Time.deltaTime, 0f, Space.World); }
            }
            // Head knocked / ripped off: hide it and send a copy flying (with gore on; otherwise it's just confetti).
            bool headOff = (fighter.Injury & Fighter.HeadOff) != 0 && GameSettings.Gore;
            var headBone = model.BoneOf(Bone.Head);
            if (headOff && !headWasOff && headBone != null) ThrowHead(headBone);
            if (headBone != null) headBone.gameObject.SetActive(!headOff);
            headWasOff = headOff;

            // Broken arm: it hangs limp at their side instead of following their hand.
            Vector3 leftHand = fighter.LeftPos, rightHand = fighter.RightPos;
            Quaternion leftRot = fighter.LeftRot, rightRot = fighter.RightRot;
            Vector3 side = Vector3.Cross(Vector3.up, fighter.FacingFlat);
            float sway = Mathf.Sin(Time.time * 3f) * 0.03f;
            if (fighter.ArmBroken(true)) { leftHand = fighter.Center - side * 0.28f + Vector3.down * 0.42f + fighter.FacingFlat * sway; leftRot = Quaternion.LookRotation(Vector3.down, fighter.FacingFlat); }
            if (fighter.ArmBroken(false)) { rightHand = fighter.Center + side * 0.28f + Vector3.down * 0.42f - fighter.FacingFlat * sway; rightRot = Quaternion.LookRotation(Vector3.down, fighter.FacingFlat); }

            model.handPulse = pulse;
            model.SetCurl(fighter.ShownCurl(true), fighter.ShownCurl(false));
            model.Pose(new PoseInput
            {
                feet = feet, headPos = fighter.HeadPos, headRot = fighter.HeadRot,
                leftPos = leftHand, leftRot = leftRot, rightPos = rightHand, rightRot = rightRot,
                fwd = fighter.FacingFlat, velocity = smoothVel, speed = speed, down = fighter.Down, airborne = air, blocking = fighter.Blocking,
                // No canned tumble animation: a limp fighter is always the physics ragdoll.
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

    /// <summary>Keeps a flying head from falling through the floor: it bounces and rolls to a stop.</summary>
    public class HeadBounce : MonoBehaviour
    {
        FxLife life;

        void Start() => life = GetComponent<FxLife>();

        void LateUpdate()
        {
            if (life == null || life.velocity.y > 0f) return;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.42f, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * 0.12f;
                life.velocity = new Vector3(life.velocity.x * 0.6f, Mathf.Abs(life.velocity.y) * 0.35f, life.velocity.z * 0.6f);
                life.spin *= 0.6f;
                if (life.velocity.y < 0.6f) { life.velocity.y = 0f; life.gravity = false; }
            }
        }
    }
}
