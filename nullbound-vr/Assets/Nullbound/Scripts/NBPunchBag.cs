using UnityEngine;

namespace Nullbound
{
    /// Lobby punching bag: shows how hard you punch (used by the VR setup test step).
    public class NBPunchBag : MonoBehaviour
    {
        public static float LastSpeed;
        public static int Hits;

        Transform m_Bag;
        TextMesh m_Label;
        Vector3 m_Swing, m_SwingVel;
        float m_Cooldown;

        void Start()
        {
            NBMat.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 2.4f, 0), new Vector3(0.04f, 0.3f, 0.04f), NBMat.Lit(Color.gray));
            m_Bag = new GameObject("Bag").transform;
            m_Bag.SetParent(transform, false);
            m_Bag.localPosition = new Vector3(0, 2.1f, 0);
            NBMat.Prim(PrimitiveType.Capsule, m_Bag, new Vector3(0, -0.75f, 0), new Vector3(0.45f, 0.55f, 0.45f), NBMat.Lit(new Color(0.75f, 0.15f, 0.2f)));
            NBMat.Prim(PrimitiveType.Cylinder, m_Bag, new Vector3(0, -0.75f, 0), new Vector3(0.47f, 0.05f, 0.47f), NBMat.Lit(Color.white));
            m_Label = NBMat.Text(transform, "PUNCH ME", new Vector3(0, 2.25f, 0), 0.1f, Color.white);
        }

        void Update()
        {
            var rig = NBRig.Instance;
            if (rig != null)
            {
                var cam = rig.Head;
                m_Label.transform.rotation = Quaternion.LookRotation(m_Label.transform.position - cam.position);
                m_Cooldown -= Time.deltaTime;
                Check(rig.RightHand); Check(rig.LeftHand);
            }
            // spring back
            m_SwingVel += (-m_Swing * 30f - m_SwingVel * 3f) * Time.deltaTime;
            m_Swing += m_SwingVel * Time.deltaTime;
            m_Bag.localRotation = Quaternion.Euler(m_Swing.z * 40f, 0, -m_Swing.x * 40f);
        }

        void Check(NBHand hand)
        {
            if (m_Cooldown > 0 || hand.Speed < 1.8f) return;
            var center = m_Bag.position + m_Bag.rotation * new Vector3(0, -0.75f, 0);
            var p = hand.transform.position;
            var d = p - center; d.y *= 0.5f;
            if (d.magnitude > 0.42f) return;
            LastSpeed = hand.Speed;
            Hits++;
            m_Cooldown = 0.2f;
            var push = hand.Velocity; push.y = 0;
            m_SwingVel += push * 0.25f;
            string rating = LastSpeed > 6f ? "MONSTER!" : LastSpeed > 4f ? "STRONG" : LastSpeed > 2.8f ? "GOOD" : "LIGHT";
            m_Label.text = LastSpeed.ToString("0.0") + " m/s  " + rating;
            NBFX.Burst(p, new Color(1f, 0.8f, 0.3f), 0.6f, 6);
            NBAudio.Play(LastSpeed > 4f ? NBAudio.Heavy : NBAudio.Punch, p);
            NBInput.Haptic(hand.isRight, 0.8f, 0.08f);
        }
    }
}
