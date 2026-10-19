using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Halloween scare: every few minutes a tall shadow figure with a pale face and glowing eyes appears somewhere
    /// off to your side or behind you, on whatever map you're on, just standing there staring at you. Turn and look
    /// right at it and it vanishes with a whisper. Get too close and it's gone too, and it slips away by itself if
    /// you never notice it. Only you see your own (nothing is networked), only in Halloween season, never in the
    /// tutorial.
    /// </summary>
    public class HalloweenWatcher : MonoBehaviour
    {
        const float LookAngle = 14f;      // looking within this many degrees of it counts as "looking at it"
        const float StayFor = 35f;        // gone by itself after this long if you never look
        const float TooClose = 6f;

        Transform figure;
        AudioSource voice;
        AudioClip whisper;
        Material body, skin, eyes;
        float nextAppear, shownAt, vanishStart = -1f, seenFor;
        Vector3 baseScale;

        void Start()
        {
            nextAppear = Time.time + Random.Range(45f, 100f);
        }

        void Update()
        {
            var rig = KartRig.Instance;
            var head = rig != null ? rig.head : null;
            bool allowed = HalloweenDecor.IsSeason && head != null && !Tutorial.Running && GameState.Instance != null && GameState.Instance.IsReady;

            if (figure == null)
            {
                if (allowed && Time.time >= nextAppear && !TryAppear(head))
                    nextAppear = Time.time + 5f; // no good spot right now: try again soon
                return;
            }

            if (vanishStart >= 0f) { AnimateVanish(); return; }
            if (!allowed || GameState.CurrentMode != shownIn) { Hide(); return; }

            // It always faces you.
            Vector3 toHead = head.position - figure.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude > 0.01f) figure.rotation = Quaternion.LookRotation(toHead);

            Vector3 eye = figure.position + Vector3.up * 2.2f;
            Vector3 toFigure = eye - head.position;
            float angle = Vector3.Angle(head.forward, toFigure);
            bool visible = angle < LookAngle && !Blocked(head.position, eye);
            seenFor = visible ? seenFor + Time.deltaTime : 0f;
            if (seenFor > 0.15f || toFigure.magnitude < TooClose) Vanish(true);
            else if (Time.time - shownAt > StayFor) Vanish(false);
        }

        KartMode shownIn;

        bool TryAppear(Transform head)
        {
            var mode = GameState.CurrentMode;
            var map = KartMaps.Get(mode);
            bool hub = mode == KartMode.Hub;
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            for (int tries = 0; tries < 8; tries++)
            {
                // Out of view: off to one side or behind you, so you only notice it when you turn round.
                float yaw = Random.Range(60f, 160f) * (Random.value < 0.5f ? -1f : 1f);
                float distance = hub ? Random.Range(9f, 17f) : Random.Range(18f, 36f);
                Vector3 p = head.position + Quaternion.Euler(0f, yaw, 0f) * forward * distance;
                if (hub)
                {
                    // Inside the garage, out in the open (not in a bay).
                    if (Mathf.Abs(p.x) > 8f || p.z < -22f || p.z > 20f) continue;
                }
                else
                {
                    Vector2 off = new Vector2(p.x - map.center.x, p.z - map.center.z);
                    if (off.magnitude > map.radius - KartMaps.BarrierMargin - 8f) continue;
                }
                // Stand it on the floor (start under any ceiling: the hub's and the Backrooms' are low).
                Vector3 from = new Vector3(p.x, head.position.y + 2.5f, p.z);
                if (!Physics.Raycast(from, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f) continue;
                Vector3 feet = hit.point;
                if (Blocked(head.position, feet + Vector3.up * 2.2f)) continue; // you'd never be able to see it
                Show(feet, mode);
                return true;
            }
            return false;
        }

        static bool Blocked(Vector3 from, Vector3 to)
        {
            // Ignore anything right next to you (your own kart, your hands).
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 2f) return false;
            dir /= len;
            return Physics.Raycast(from + dir * 1.5f, dir, len - 2f, ~0, QueryTriggerInteraction.Ignore);
        }

        void Show(Vector3 feet, KartMode mode)
        {
            if (body == null)
            {
                body = KartUI.Mat(new Color(0.02f, 0.02f, 0.03f), 0.05f);
                skin = KartUI.Mat(new Color(0.85f, 0.85f, 0.8f), 0.1f, 0f, new Color(0.25f, 0.25f, 0.25f));
                eyes = KartUI.Glow(new Color(1f, 0.08f, 0.05f));
            }
            figure = new GameObject("TheWatcher").transform;
            figure.position = feet;
            // Tall and thin, arms hanging too long.
            KartUI.Part(figure, PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0f), new Vector3(0.55f, 1.25f, 0.4f), body);
            KartUI.Part(figure, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.75f, 1f, 0.45f), body); // ragged robe
            foreach (float side in new[] { -1f, 1f })
                KartUI.Part(figure, PrimitiveType.Capsule, new Vector3(side * 0.36f, 1.15f, 0.02f), new Vector3(0.11f, 0.85f, 0.11f), body, new Vector3(0f, 0f, side * 6f));
            KartUI.Part(figure, PrimitiveType.Sphere, new Vector3(0f, 2.62f, 0f), new Vector3(0.6f, 0.72f, 0.55f), body); // hood
            // The face: pale, long, with burning eyes and a black grin.
            KartUI.Part(figure, PrimitiveType.Sphere, new Vector3(0f, 2.55f, 0.17f), new Vector3(0.38f, 0.5f, 0.28f), skin);
            foreach (float side in new[] { -0.08f, 0.08f })
            {
                KartUI.Part(figure, PrimitiveType.Sphere, new Vector3(side, 2.65f, 0.3f), new Vector3(0.1f, 0.06f, 0.04f), body);
                KartUI.Part(figure, PrimitiveType.Sphere, new Vector3(side, 2.65f, 0.315f), new Vector3(0.045f, 0.035f, 0.02f), eyes);
            }
            KartUI.Part(figure, PrimitiveType.Cube, new Vector3(0f, 2.42f, 0.305f), new Vector3(0.2f, 0.035f, 0.02f), body, new Vector3(0f, 0f, 0f));
            for (int i = -2; i <= 2; i++)
                KartUI.Part(figure, PrimitiveType.Cube, new Vector3(i * 0.04f, 2.415f, 0.31f), new Vector3(0.012f, 0.05f, 0.02f), skin);
            // A faint red glow so it shows up in the dark maps.
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(figure, false);
            glow.transform.localPosition = new Vector3(0f, 2.65f, 0.6f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.15f, 0.1f);
            glow.range = 2.5f;
            glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;

            baseScale = figure.localScale;
            shownAt = Time.time;
            shownIn = mode;
            seenFor = 0f;
            vanishStart = -1f;
        }

        void Vanish(bool withSound)
        {
            vanishStart = Time.time;
            if (withSound) PlayWhisper(figure.position + Vector3.up * 2.2f);
        }

        /// <summary>Squashes to nothing in a flicker.</summary>
        void AnimateVanish()
        {
            float t = (Time.time - vanishStart) / 0.25f;
            if (t >= 1f) { Hide(); return; }
            figure.gameObject.SetActive(Mathf.Repeat(t * 7f, 1f) > 0.3f);
            figure.localScale = new Vector3(baseScale.x * (1f - t), baseScale.y * (1f + t * 0.4f), baseScale.z * (1f - t));
        }

        void Hide()
        {
            if (figure != null) Destroy(figure.gameObject);
            figure = null;
            vanishStart = -1f;
            nextAppear = Time.time + Random.Range(90f, 240f);
        }

        void PlayWhisper(Vector3 at)
        {
            if (whisper == null) whisper = MakeWhisper();
            if (voice == null)
            {
                voice = new GameObject("WatcherVoice").AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.spatialBlend = 1f;
                voice.minDistance = 4f;
                voice.maxDistance = 60f;
                voice.rolloffMode = AudioRolloffMode.Linear;
            }
            voice.transform.position = at;
            voice.PlayOneShot(whisper, GameSettings.SfxVolume);
        }

        /// <summary>A breathy rising hiss with a low falling groan under it, cut off sharply (made in code).</summary>
        static AudioClip MakeWhisper()
        {
            const int rate = 22050;
            const float seconds = 0.9f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            var rng = new System.Random(66);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.25f; // soften the hiss
                float hiss = (noise - lp) * 0.5f + lp * 0.5f;
                float freq = Mathf.Lerp(110f, 55f, t);
                phase += freq / rate;
                float groan = Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f + Mathf.Sin(phase * Mathf.PI * 2f * 1.5f) * 0.2f;
                float env = Mathf.Pow(t, 1.6f) * (t > 0.94f ? (1f - t) / 0.06f : 1f);
                data[i] = (hiss * 0.55f + groan * 0.45f) * env * 0.8f;
            }
            var clip = AudioClip.Create("WatcherWhisper", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (figure != null) Destroy(figure.gameObject);
        }
    }
}
