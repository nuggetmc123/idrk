using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nullbound
{
    /// Wrist display (health, awakening bar, cooldowns) on your off hand, plus big messages that
    /// float in front of your face (KO!, combo counter, kill feed).
    public class NBHud : MonoBehaviour
    {
        public static NBHud Instance { get; private set; }

        Transform m_Wrist, m_HpFill, m_UltFill, m_Face;
        TextMesh m_HpText, m_UltText, m_Moves, m_Stats, m_Big, m_SmallText, m_Combo, m_FeedText, m_Center;
        float m_BigUntil, m_SmallUntil, m_ComboUntil, m_FlashUntil;
        readonly List<(string, float)> m_Feed = new List<(string, float)>();
        Renderer m_Flash;

        public static NBHud Create()
        {
            var g = new GameObject("NB_Hud");
            var h = g.AddComponent<NBHud>();
            Instance = h;
            h.Build();
            h.SetMatchVisible(false);
            return h;
        }

        void Build()
        {
            // ---- wrist ----
            m_Wrist = new GameObject("Wrist").transform;
            m_Wrist.SetParent(transform, false);
            NBMat.Prim(PrimitiveType.Quad, m_Wrist, new Vector3(0, 0, 0.002f), new Vector3(0.2f, 0.15f, 1), NBMat.Glow(new Color(0.05f, 0.04f, 0.1f, 0.85f)));
            m_HpFill = Bar(m_Wrist, 0.055f, new Color(0.3f, 1f, 0.4f));
            m_HpText = NBMat.Text(m_Wrist, "", new Vector3(-0.09f, 0.055f, -0.004f), 0.012f, Color.white, TextAnchor.MiddleLeft);
            m_UltFill = Bar(m_Wrist, 0.035f, new Color(0.7f, 0.35f, 1f));
            m_UltText = NBMat.Text(m_Wrist, "", new Vector3(-0.09f, 0.035f, -0.004f), 0.011f, Color.white, TextAnchor.MiddleLeft);
            m_Moves = NBMat.Text(m_Wrist, "", new Vector3(-0.092f, 0.018f, -0.004f), 0.011f, Color.white, TextAnchor.UpperLeft);
            m_Stats = NBMat.Text(m_Wrist, "", new Vector3(-0.092f, -0.05f, -0.004f), 0.01f, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperLeft);

            // ---- face (lazy-follow in front of the head) ----
            m_Face = new GameObject("Face").transform;
            m_Face.SetParent(transform, false);
            m_Big = NBMat.Text(m_Face, "", new Vector3(0, 0.12f, 0), 0.12f, Color.white);
            m_SmallText = NBMat.Text(m_Face, "", new Vector3(0, -0.28f, 0), 0.035f, new Color(1f, 0.9f, 0.6f));
            m_Combo = NBMat.Text(m_Face, "", new Vector3(0.38f, 0.0f, 0), 0.08f, new Color(1f, 0.85f, 0.3f));
            m_FeedText = NBMat.Text(m_Face, "", new Vector3(0.55f, 0.32f, 0), 0.03f, Color.white, TextAnchor.UpperRight);
            m_Center = NBMat.Text(m_Face, "", new Vector3(0, -0.05f, 0), 0.05f, Color.white);

            var rig = NBRig.Instance;
            var flash = NBMat.Prim(PrimitiveType.Quad, rig.Head, new Vector3(0, 0, 0.11f), new Vector3(0.6f, 0.6f, 1), NBMat.Glow(new Color(1, 0, 0, 0.25f)));
            m_Flash = flash.GetComponent<Renderer>();
            m_Flash.material.renderQueue = 4001;
            m_Flash.enabled = false;
        }

        static Transform Bar(Transform parent, float y, Color c)
        {
            NBMat.Prim(PrimitiveType.Quad, parent, new Vector3(0, y, -0.001f), new Vector3(0.18f, 0.016f, 1), NBMat.Glow(new Color(0.2f, 0.2f, 0.25f, 0.9f)));
            var root = new GameObject("Fill").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(-0.09f, y, -0.002f);
            NBMat.Prim(PrimitiveType.Quad, root, new Vector3(0.09f, 0, 0), new Vector3(0.18f, 0.016f, 1), NBMat.Glow(c));
            return root;
        }

        public void SetMatchVisible(bool v) => m_Wrist.gameObject.SetActive(v);

        public void Big(string s, Color c, float time) { m_Big.text = s; m_Big.color = c; m_BigUntil = Time.time + time; }
        public void Small(string s) { m_SmallText.text = s; m_SmallUntil = Time.time + 1.2f; }
        public void HitFlash() { m_FlashUntil = Time.time + 0.12f; }

        public void ComboPop(int n, byte kind)
        {
            m_Combo.text = kind == 1 ? "UPPERCUT!" : kind == 2 ? "DOWNSLAM!" : n >= 4 ? "FINISHER!" : n + " HIT";
            m_ComboUntil = Time.time + 1.1f;
        }

        public void Feed(string killer, string victim)
        {
            m_Feed.Add((killer + "  KO  " + victim, Time.time + 6f));
            if (m_Feed.Count > 4) m_Feed.RemoveAt(0);
            var me = NBPlayer.Local;
            if (me != null)
            {
                if (victim == me.Name) Big("KO'D BY " + killer, new Color(1f, 0.35f, 0.3f), 3f);
                else if (killer == me.Name) Big("KO!", new Color(1f, 0.85f, 0.3f), 1.5f);
            }
        }

        void LateUpdate()
        {
            var rig = NBRig.Instance;
            if (rig == null) return;
            float now = Time.time;

            // face anchor lazily follows where you look
            var target = rig.Head.position + rig.Head.forward * 1.3f;
            m_Face.position = Vector3.Lerp(m_Face.position, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            m_Face.rotation = Quaternion.LookRotation(m_Face.position - rig.Head.position);
            m_Big.gameObject.SetActive(now < m_BigUntil);
            m_SmallText.gameObject.SetActive(now < m_SmallUntil);
            m_Combo.gameObject.SetActive(now < m_ComboUntil);
            m_Feed.RemoveAll(f => f.Item2 < now);
            m_FeedText.text = string.Join("\n", m_Feed.Select(f => f.Item1));
            m_Flash.enabled = now < m_FlashUntil && NBInput.XRActive;

            var p = NBPlayer.Local;
            var lf = NBLocalFighter.Instance;
            if (p == null || !p.IsSpawned || lf == null) { m_Center.text = ""; return; }

            if (p.Dead.Value) m_Center.text = "RESPAWNING...";
            else if (rig.Stunned && rig.StunLeft > 0.3f) m_Center.text = "STUNNED - DASH TO EVADE" + (Time.time < lf.NextEvasive ? " (" + Mathf.CeilToInt(lf.NextEvasive - now) + ")" : "");
            else m_Center.text = "";

            // wrist sits above the off-hand glove, tilted toward your eyes
            var hand = NBSettings.LeftHanded ? rig.RightHand.transform : rig.LeftHand.transform;
            m_Wrist.position = hand.position + hand.rotation * new Vector3(NBSettings.LeftHanded ? -0.02f : 0.02f, 0.06f, -0.12f);
            var toEye = m_Wrist.position - rig.Head.position;
            m_Wrist.rotation = Quaternion.LookRotation(toEye, hand.rotation * Vector3.up);
            if (!NBInput.XRActive)
            {
                // desktop: pin it to the bottom-left of the screen
                m_Wrist.position = rig.Head.position + rig.Head.rotation * new Vector3(-0.22f, -0.15f, 0.45f);
                m_Wrist.rotation = rig.Head.rotation;
            }

            float hp = p.Health.Value / NBFighters.MaxHealth;
            m_HpFill.localScale = new Vector3(Mathf.Clamp01(hp), 1, 1);
            m_HpText.text = "HP " + Mathf.CeilToInt(p.Health.Value);
            float ult = p.Ult.Value / NBFighters.UltMax;
            m_UltFill.localScale = new Vector3(p.Awakened ? (float)((p.AwakenEnd.Value - NBPlayer.Now) / NBFighters.AwakenTime) : Mathf.Clamp01(ult), 1, 1);
            m_UltText.text = p.Awakened ? "AWAKENED!" : ult >= 0.999f ? "AWAKEN READY - CLICK BOTH STICKS" : "AWAKEN " + Mathf.FloorToInt(ult * 100) + "%";

            var def = p.Def;
            var sb = new System.Text.StringBuilder();
            string[] keys = NBSettings.LeftHanded ? new[] { "A", "B", "RT", "LT" } : new[] { "X", "Y", "LT", "RT" };
            for (int i = 0; i < 4; i++)
            {
                float left = lf.NextMove[i] - now;
                sb.Append(keys[i]).Append(' ').Append(def.moves[i].name).Append(left > 0 ? "  " + Mathf.CeilToInt(left) + "s" : "  READY").Append('\n');
            }
            float dash = lf.NextDash - now, ev = lf.NextEvasive - now;
            sb.Append("DASH ").Append(dash > 0 ? dash.ToString("0.0") : "OK").Append("   EVADE ").Append(ev > 0 ? Mathf.CeilToInt(ev).ToString() : "OK");
            m_Moves.text = sb.ToString();
            var buff = p.ActiveBuff;
            m_Stats.text = def.name + "   KO " + p.Kills.Value + "  DOWN " + p.Deaths.Value + (buff != BuffKind.None ? "   " + buff.ToString().ToUpperInvariant() : "")
                           + (NBNet.Instance != null ? "\n" + NBNet.Instance.RoomLabel : "");
        }
    }
}
