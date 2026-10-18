using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Driving effects on every kart: sparks from the back wheels while drifting (blue, then orange, then purple
    /// the longer the drift), tyre smoke, and on your own kart speed streaks rushing past when you go fast.
    /// Also the confetti burst used for wins and level-ups.
    /// </summary>
    public class KartFx : MonoBehaviour
    {
        KartController kart;
        ParticleSystem sparksL, sparksR, smoke, streaks;
        static readonly Color[] SparkColors = { Color.clear, new Color(0.3f, 0.75f, 1f), new Color(1f, 0.55f, 0.1f), new Color(0.8f, 0.3f, 1f) };

        void Start()
        {
            kart = GetComponent<KartController>();
            sparksL = Sparks(new Vector3(-0.62f, 0.12f, -0.7f));
            sparksR = Sparks(new Vector3(0.62f, 0.12f, -0.7f));
            smoke = Smoke(new Vector3(0f, 0.2f, -0.8f));
        }

        ParticleSystem Sparks(Vector3 at)
        {
            var ps = Make("DriftSparks", transform, at, 0.05f, 0.35f, 6f, 300);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.rotation = new Vector3(-150f, 0f, 0f); // back and up
            var main = ps.main;
            main.gravityModifier = 1.5f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.5f;
            return ps;
        }

        ParticleSystem Smoke(Vector3 at)
        {
            var ps = Make("TyreSmoke", transform, at, 0.6f, 1.2f, 1.2f, 120);
            var main = ps.main;
            main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.35f);
            main.gravityModifier = -0.05f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2f));
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 0.1f, 0.2f);
            return ps;
        }

        static ParticleSystem Make(string name, Transform parent, Vector3 at, float size, float life, float speed, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (KartUI.ParticleMaterial != null) r.sharedMaterial = KartUI.ParticleMaterial;
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (kart == null || !kart.IsReady) return;
            int level = kart.Ghost || kart.Parked ? 0 : kart.DriftLevel;
            float rate = level > 0 ? 40f + level * 25f : 0f;
            foreach (var s in new[] { sparksL, sparksR })
            {
                var e = s.emission;
                e.rateOverTime = rate;
                var main = s.main;
                if (level > 0) main.startColor = new ParticleSystem.MinMaxGradient(SparkColors[level], Color.white);
            }
            var se = smoke.emission;
            se.rateOverTime = level > 0 ? 18f : 0f;

            if (kart.IsLocal) SpeedStreaks();
        }

        /// <summary>White streaks flying past your head when you're going fast (your own kart only).</summary>
        void SpeedStreaks()
        {
            var rig = KartRig.Instance;
            if (rig == null || rig.head == null || !rig.IsAttachedTo(kart)) return;
            if (streaks == null)
            {
                streaks = Make("SpeedStreaks", rig.head, new Vector3(0f, 0f, 9f), 0.03f, 0.45f, 22f, 200);
                streaks.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // fly towards you
                var shape = streaks.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 3.5f;
                shape.radiusThickness = 0.3f; // a ring round your view, never right in your face
                var main = streaks.main;
                main.startColor = new Color(1f, 1f, 1f, 0.35f);
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 6f;
            }
            float speed = kart.Speed;
            var e = streaks.emission;
            e.rateOverTime = speed > 16f ? (speed - 16f) * 8f : 0f;
        }

        /// <summary>A one-off burst of colourful confetti (wins, level-ups, the welcome screen).</summary>
        public static void Confetti(Vector3 at, float scale = 1f)
        {
            var go = new GameObject("Confetti");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * scale, 0.09f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.3f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                                 new GradientColorKey(new Color(0.3f, 0.9f, 1f), 0.66f), new GradientColorKey(new Color(0.5f, 1f, 0.4f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(160 * scale)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (KartUI.ParticleMaterial != null) r.sharedMaterial = KartUI.ParticleMaterial;
            ps.Play();
            Destroy(go, 4f);
        }
    }
}
