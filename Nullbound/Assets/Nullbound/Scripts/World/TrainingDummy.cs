using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// A punching bag on a spring in the Training Grounds. Hit it to see your damage, combo and damage-per-second.
    /// Local only (everyone has their own copy), so it never needs the network.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IHittable
    {
        public Transform bag;
        public TextMeshPro readout;

        Vector3 tilt, tiltVel;
        float total, windowStart = -10f, lastHit = -10f, best;
        int hits;

        public Vector3 Center => bag != null ? bag.position : transform.position + Vector3.up * 1.2f;
        public bool CanBeHitBy(Fighter attacker) => true;

        public void TakeHit(Fighter attacker, HitData hit)
        {
            if (Time.time - lastHit > 2f) { total = 0f; hits = 0; windowStart = Time.time; }
            lastHit = Time.time;
            total += hit.damage;
            hits++;
            Vector3 k = hit.knockback;
            k.y = 0f;
            tiltVel += k * 18f;
            Fx.Popup(hit.point + Vector3.up * 0.25f, Mathf.RoundToInt(hit.damage).ToString(), (hit.flags & HitFlags.Heavy) != 0 ? new Color(1f, 0.5f, 0.2f) : Color.white);
            float seconds = Mathf.Max(0.5f, Time.time - windowStart);
            best = Mathf.Max(best, total / seconds);
        }

        void Update()
        {
            // Springy wobble.
            tiltVel += -tilt * 60f * Time.deltaTime;
            tiltVel *= Mathf.Exp(-5f * Time.deltaTime);
            tilt += tiltVel * Time.deltaTime;
            tilt = Vector3.ClampMagnitude(tilt, 35f);
            if (bag != null) bag.localRotation = Quaternion.Euler(tilt.z, 0f, -tilt.x);
            if (readout != null)
            {
                bool active = Time.time - lastHit < 2f;
                float seconds = Mathf.Max(0.5f, Time.time - windowStart);
                readout.text = active
                    ? "<b>" + Mathf.RoundToInt(total) + "</b> damage\n" + hits + " hits   " + Mathf.RoundToInt(total / seconds) + " DPS"
                    : "PUNCH ME\n<size=70%>best " + Mathf.RoundToInt(best) + " DPS</size>";
            }
        }
    }
}
