using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// An AI racing driver. It drives the same physics car as you, through the same inputs: follows the racing line
    /// with pure-pursuit steering and a little counter-steer, brakes to a speed profile worked out from its car's grip
    /// and downforce, and races the cars around it: follows when there's no room, pulls out to overtake on the
    /// straights, leaves space alongside, gives the player an extra margin, lifts under yellow flags and steers around
    /// stricken cars. Difficulty scales how close to the limit it drives and how consistent it is.
    /// </summary>
    public class AIDriver : MonoBehaviour
    {
        public Car car;
        public RaceSession session;
        public float skill = 0.95f;      // fraction of the grip limit used
        public float aggression = 0.5f;  // how readily it goes for gaps
        public bool racing;              // false = hold on the grid
        public bool coolDown;            // after the flag: drive gently

        float[] speed;
        int hint = -1;
        float along, lateral;
        float avoid, avoidTarget;
        float launchDelay;
        float mistakeT, mistake;
        float stuckT;
        float steerSmooth;
        TrackSpline track;
        RacingLine line;

        public float Along => along;
        public float Lateral => lateral;

        public void Init(Car c, RaceSession s, float skillLevel, float aggr, float wetness)
        {
            car = c;
            session = s;
            skill = skillLevel;
            aggression = aggr;
            track = World.Current.track;
            line = World.Current.line;
            Recompute(wetness);
            launchDelay = Random.Range(0.15f, 0.45f) * (1.6f - skill);
        }

        public void Recompute(float wetness)
        {
            speed = line.SpeedProfile(car.spec, skill, wetness);
        }

        float lastWet = -1f;

        void FixedUpdate()
        {
            if (car == null || track == null || car.Frozen) return;
            if (Mathf.Abs(Env.Wetness - lastWet) > 0.1f) { lastWet = Env.Wetness; Recompute(Env.Wetness); }
            float dt = Time.fixedDeltaTime;
            Vector3 pos = car.transform.position;
            track.Project(pos, ref hint, out along, out lateral);
            float v = car.SpeedMs;
            int i = hint;

            if (!racing)
            {
                car.input.throttle = 0f; car.input.brake = 1f; car.input.steer = 0f;
                launchDelay = Random.Range(0.12f, 0.4f) * (1.6f - skill);
                return;
            }
            if (launchDelay > 0f) { launchDelay -= dt; car.input.throttle = 0.4f; car.input.brake = 0f; return; }

            // ---- where to be across the track
            float lookAhead = Mathf.Clamp(7f + v * 0.55f, 9f, 46f);
            float aheadS = along + lookAhead;
            int ai = track.IndexAt(aheadS);
            float lineLat = line.offset[ai];
            Traffic(v, out float speedCap);
            avoid = Mathf.MoveTowards(avoid, avoidTarget, dt * 2.2f);
            // Mistakes on lower skill: drift off line a little now and then.
            mistakeT -= dt;
            if (mistakeT <= 0f) { mistakeT = Random.Range(3f, 9f); mistake = (Random.value - 0.5f) * (1f - skill) * 6f; }
            mistake = Mathf.MoveTowards(mistake, 0f, dt * 0.4f);
            float hw = track.halfWidth[ai] - 1.1f;
            float targetLat = Mathf.Clamp(lineLat + avoid + mistake, -hw, hw);
            Vector3 target = track.PointAt(aheadS, targetLat);

            // ---- steering: pure pursuit + yaw damping
            Vector3 local = car.transform.InverseTransformPoint(target);
            float L2 = Mathf.Max(1f, local.x * local.x + local.z * local.z);
            float curvature = 2f * local.x / L2;
            float steerAngle = Mathf.Atan(curvature * car.spec.wheelbase) * Mathf.Rad2Deg;
            // Counter-steer when the rear steps out.
            if (car.Body != null)
            {
                float yawRate = car.Body.angularVelocity.y;
                float expected = v * curvature;
                steerAngle -= (yawRate - expected) * 4f * Mathf.Clamp01(v / 20f);
                float slip = Vector3.Dot(car.Body.linearVelocity, car.transform.right);
                steerAngle += Mathf.Clamp(slip * 1.5f, -8f, 8f) * Mathf.Clamp01(v / 15f);
            }
            float steer = Mathf.Clamp(steerAngle / car.spec.maxSteerAngle, -1f, 1f);
            steerSmooth = Mathf.MoveTowards(steerSmooth, steer, dt * 6f);
            car.input.steer = steerSmooth;

            // ---- speed
            int pi = track.IndexAt(along + Mathf.Max(4f, v * 0.25f));
            float vt = Mathf.Min(speed[pi], speed[i]);
            if (coolDown) vt = Mathf.Min(vt * 0.6f, 35f);
            if (session != null && session.IsYellow(along)) vt = Mathf.Min(vt, speed[i] * 0.82f);
            vt = Mathf.Min(vt, speedCap);
            // Off line or off track: be careful.
            if (Mathf.Abs(lateral) > track.halfWidth[i] + 0.5f) vt = Mathf.Min(vt, Mathf.Max(18f, v * 0.9f));
            float err = vt - v;
            float throttle = Mathf.Clamp01(err * 0.35f + (err > 0f ? 0.3f : 0f));
            float brake = err < -1f ? Mathf.Clamp01(-err * 0.18f) : 0f;
            // Less throttle when the car is sliding sideways.
            throttle *= 1f - Mathf.Clamp01(car.TyreSlide * 1.5f) * 0.6f;
            car.input.throttle = throttle;
            car.input.brake = brake;
            car.input.handbrake = 0f;

            // ---- stuck / wrong way recovery
            if (v < 1.5f) stuckT += dt; else stuckT = 0f;
            if (stuckT > 3f && session != null) { stuckT = 0f; session.Recover(car); }
            if (car.Upright < 0.2f && v < 3f) { stuckT += dt * 2f; }
        }

        /// <summary>Looks at nearby cars: decides whether to follow, pass on the left or right, or leave room.</summary>
        void Traffic(float v, out float speedCap)
        {
            speedCap = float.MaxValue;
            avoidTarget = 0f;
            if (session == null) return;
            float bestGap = 60f;
            Car ahead = null;
            float aheadLat = 0f, aheadAlong = 0f;
            foreach (var e in session.entrants)
            {
                var o = e.car;
                if (o == null || o == car || e.eliminated) continue;
                float oAlong = e.along, oLat = e.lateral;
                float gap = track.Ahead(along, oAlong);
                if (gap > track.Length * 0.5f) gap -= track.Length; // behind us
                float latDiff = oLat - lateral;
                bool isPlayer = o.isPlayer;
                float margin = isPlayer ? 3.4f : 2.6f;
                // Alongside: leave room.
                if (Mathf.Abs(gap) < 6f && Mathf.Abs(latDiff) < margin + 0.6f)
                    avoidTarget += -Mathf.Sign(latDiff == 0f ? 1f : latDiff) * (margin + 0.6f - Mathf.Abs(latDiff)) * 1.2f;
                if (gap > 0.5f && gap < bestGap && Mathf.Abs(latDiff) < margin + 1f) { bestGap = gap; ahead = o; aheadLat = oLat; aheadAlong = oAlong; }
                // Stricken car ahead: steer well around it.
                bool stricken = (o.damage != null && o.damage.Wrecked) || o.SpeedMs < 8f;
                if (stricken && gap > 0f && gap < 120f)
                {
                    float side = oLat > 0f ? -1f : 1f;
                    float need = margin + 1.5f - Mathf.Abs(latDiff);
                    if (need > 0f) avoidTarget += side * need;
                }
            }
            if (ahead == null) return;
            float vAhead = ahead.SpeedMs;
            float closing = v - vAhead;
            float timeGap = bestGap / Mathf.Max(5f, v);
            // Is there a straight ahead long enough to pass?
            float straightness = 0f;
            for (int k = 1; k <= 8; k++) straightness += Mathf.Abs(track.curvature[track.IndexAt(along + k * 15f)]);
            bool straight = straightness < 0.02f;
            bool wantsPass = closing > 1f && timeGap < 1.2f;
            if (wantsPass && (straight || aggression > 0.7f))
            {
                // Pick the side with more room.
                int ai = track.IndexAt(aheadAlong);
                float roomL = aheadLat + track.halfWidth[ai];
                float roomR = track.halfWidth[ai] - aheadLat;
                float side = roomR > roomL ? 1f : -1f;
                float passLat = aheadLat + side * (ahead.isPlayer ? 3.6f : 3f);
                avoidTarget += passLat - line.offset[track.IndexAt(along + 20f)];
            }
            else if (closing > -2f)
            {
                // Follow: keep a safe gap (bigger behind the player, so the AI doesn't punt you).
                float minGap = ahead.isPlayer ? 0.55f : 0.35f;
                if (timeGap < minGap + 0.3f) speedCap = vAhead - (minGap - timeGap) * 8f;
            }
        }
    }
}
