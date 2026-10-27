using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Level of detail for a car: the closer it is, the more of it is drawn. Up close, everything. Further away, the
    /// small pieces nobody can make out at that distance (suspension arms, the driver's arms and body inside the
    /// cockpit, calipers, paddles, mirror glass) are switched off and stop being animated. A long way off, the car
    /// also stops casting shadows from its smaller parts. The car you're driving is always at full detail. Each car
    /// checks a few times a second, at staggered times, so it costs next to nothing.
    /// </summary>
    public class CarLod : MonoBehaviour
    {
        public const float NearDistance = 32f, FarDistance = 110f;

        Car car;
        readonly List<Renderer> detail = new List<Renderer>();      // hidden beyond NearDistance
        readonly List<Behaviour> animators = new List<Behaviour>(); // stop updating beyond NearDistance
        readonly List<Renderer> shadowed = new List<Renderer>();    // stop casting shadows beyond FarDistance
        readonly List<UnityEngine.Rendering.ShadowCastingMode> shadowModes = new List<UnityEngine.Rendering.ShadowCastingMode>();
        int level = -1;
        float nextCheck;

        static readonly HashSet<string> DetailNames = new HashSet<string> { "Arm", "Caliper", "PaddleL", "PaddleR", "MirrorGlass", "UpperArm", "Forearm", "Elbow", "Hand" };

        public void Init(Car c)
        {
            car = c;
            detail.Clear(); animators.Clear(); shadowed.Clear(); shadowModes.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                string n = r.gameObject.name;
                bool inDriver = c.parts != null && c.parts.driver != null && r.transform.IsChildOf(c.parts.driver.transform);
                if (DetailNames.Contains(n) || inDriver) detail.Add(r);
                else if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off && n != "Body")
                {
                    shadowed.Add(r);
                    shadowModes.Add(r.shadowCastingMode);
                }
            }
            foreach (var s in GetComponentsInChildren<StrutSet>(true)) animators.Add(s);
            if (c.parts != null && c.parts.driver != null) animators.Add(c.parts.driver);
            nextCheck = Time.time + Random.Range(0f, 0.25f);
            level = -1;
            Apply(0);
        }

        void Update()
        {
            if (car == null || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            int want = 0;
            if (!car.isPlayer && Cameras.Main != null)
            {
                float d = (Cameras.Main.transform.position - transform.position).sqrMagnitude;
                // A little hysteresis so a car on the boundary doesn't flicker between levels.
                float near = NearDistance + (level >= 1 ? -3f : 3f), far = FarDistance + (level >= 2 ? -8f : 8f);
                want = d > far * far ? 2 : d > near * near ? 1 : 0;
            }
            Apply(want);
        }

        void Apply(int l)
        {
            if (l == level) return;
            level = l;
            foreach (var r in detail) if (r != null && r.transform.IsChildOf(transform)) r.enabled = l == 0; // flung-off bits keep their own state
            foreach (var a in animators) if (a != null) a.enabled = l == 0;
            for (int i = 0; i < shadowed.Count; i++)
                if (shadowed[i] != null) shadowed[i].shadowCastingMode = l == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : shadowModes[i];
        }

        /// The car came back to full health (a reset or repair re-enables parts): re-check what's visible.
        public void Refresh() { int l = level; level = -1; Apply(Mathf.Max(0, l)); }
    }
}
