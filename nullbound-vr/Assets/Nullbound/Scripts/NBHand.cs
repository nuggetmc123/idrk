using UnityEngine;

namespace Nullbound
{
    /// A local glove. Tracks its own speed relative to your body so running doesn't count as a punch.
    public class NBHand : MonoBehaviour
    {
        public bool isRight;
        public Vector3 Velocity { get; private set; }   // world-space direction, body-relative speed
        public float Speed => Velocity.magnitude;

        Transform m_Space;
        Vector3 m_LastLocal;
        bool m_Has;
        readonly Vector3[] m_Samples = new Vector3[4];
        int m_Idx;
        Renderer m_Glove, m_Knuckle;

        public void Init(Transform trackingSpace, bool right)
        {
            m_Space = trackingSpace;
            isRight = right;
            m_Glove = NBMat.Prim(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(0.1f, 0.09f, 0.12f), NBMat.Lit(Color.white)).GetComponent<Renderer>();
            m_Knuckle = NBMat.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.02f, 0.05f), new Vector3(0.09f, 0.04f, 0.04f), NBMat.Lit(Color.gray)).GetComponent<Renderer>();
        }

        public void SetColors(Color main, Color accent)
        {
            if (m_Glove != null) m_Glove.material.color = main;
            if (m_Knuckle != null) m_Knuckle.material.color = accent;
        }

        public void SetVisible(bool v)
        {
            if (m_Glove != null) m_Glove.enabled = v;
            if (m_Knuckle != null) m_Knuckle.enabled = v;
        }

        public void SetPose(Vector3 localPos, Quaternion localRot)
        {
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }

        /// Called once per frame after the pose is applied.
        public void Sample(float dt)
        {
            var local = transform.localPosition;
            if (!m_Has) { m_LastLocal = local; m_Has = true; }
            var v = dt > 0f ? (local - m_LastLocal) / dt : Vector3.zero;
            m_LastLocal = local;
            m_Samples[m_Idx] = v;
            m_Idx = (m_Idx + 1) % m_Samples.Length;
            var avg = Vector3.zero;
            foreach (var s in m_Samples) avg += s;
            avg /= m_Samples.Length;
            Velocity = m_Space != null ? m_Space.TransformDirection(avg) : avg;
        }

        public void ResetVelocity()
        {
            for (int i = 0; i < m_Samples.Length; i++) m_Samples[i] = Vector3.zero;
            Velocity = Vector3.zero;
            m_Has = false;
        }
    }
}
