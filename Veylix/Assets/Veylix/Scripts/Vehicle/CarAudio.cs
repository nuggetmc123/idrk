using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Real-time engine, tyre, wind and kerb sound, synthesised on the audio thread (no samples needed). The engine is
    /// built from its firing frequency: a stack of harmonics shaped per engine layout (a V10 screams with bright upper
    /// orders, a cross-plane V8 burbles with half orders, the hybrid V6 adds a turbo whistle, the four-pot buzzes),
    /// brightened and saturated by load, chopped by the limiter, with overrun crackle on lift-off.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CarAudio : MonoBehaviour
    {
        public Car car;
        public bool cockpit; // the listener sits in this car

        AudioSource src;
        const int Table = 4096;
        static float[] sine;

        // Parameters copied from the main thread every frame.
        volatile float pRpm, pLoad, pThrottle, pSpeed, pSlide, pOff, pKerb, pScrape, pVolume, pDoppler = 1f, pLimiter, pWet;
        volatile int pCyl = 6;
        volatile int pLayout;
        volatile float pShiftKick;

        // Audio-thread state.
        double phase, turboPhase, kerbPhase;
        float rate = 48000f;
        float lp1, lp2, bp1, bp2, windLp, rumbleLp, hp, squealPhase, crackle, gearThump;
        float smoothRpm = 3000f, smoothLoad, smoothVol;
        uint rnd = 22222;
        Vector3 lastPos;

        void Awake()
        {
            if (sine == null)
            {
                sine = new float[Table + 1];
                for (int i = 0; i <= Table; i++) sine[i] = Mathf.Sin(i / (float)Table * Mathf.PI * 2f);
            }
            src = GetComponent<AudioSource>();
            rate = AudioSettings.outputSampleRate;
            // A silent looping clip keeps the source "playing" so the filter below runs.
            src.clip = AudioClip.Create("silence", 1024, 1, (int)rate, false);
            src.loop = true;
            src.playOnAwake = true;
            src.dopplerLevel = 0f; // we do our own Doppler on the synthesis
            src.rolloffMode = AudioRolloffMode.Custom;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            src.maxDistance = 420f;
            src.minDistance = 4f;
            src.Play();
        }

        public void Configure(Car c, bool inCockpit)
        {
            car = c;
            cockpit = inCockpit;
            src.spatialBlend = inCockpit ? 0f : 1f;
            src.spread = inCockpit ? 0f : 40f;
            src.priority = inCockpit ? 0 : 128;
            switch (c.spec.engine)
            {
                case EngineLayout.V10: pCyl = 10; pLayout = 1; break;
                case EngineLayout.V8: pCyl = 8; pLayout = 2; break;
                case EngineLayout.V8Classic: pCyl = 8; pLayout = 3; break;
                case EngineLayout.I4Turbo: pCyl = 4; pLayout = 4; break;
                case EngineLayout.V12Classic: pCyl = 12; pLayout = 5; break;
                default: pCyl = 6; pLayout = 0; break;
            }
            c.OnShift -= OnShift;
            c.OnShift += OnShift;
            lastPos = transform.position;
        }

        void OnShift(int g) { pShiftKick = 1f; }

        void Update()
        {
            if (car == null || car.spec == null) return;
            pRpm = car.Rpm;
            pLoad = car.EngineLoad;
            pThrottle = car.ThrottleOut;
            pSpeed = car.SpeedMs;
            pSlide = car.TyreSlide * (Surface.IsOffTrack(car.DominantSurface) ? 0.2f : 1f) + car.LockupAmount * 0.6f + car.WheelSpinAmount * 0.5f;
            pOff = car.OffTrackAmount;
            pKerb = car.KerbRumble;
            pScrape = car.Scrape;
            pLimiter = car.Limiter ? 1f : 0f;
            pWet = Env.Wetness;
            float vol = Settings.MasterVolume * Settings.EngineVolume * (cockpit ? 0.9f : 1.4f);
            if (car.damage != null && car.damage.EngineDead) vol *= 0f;
            pVolume = vol;

            // Cars out of earshot stop synthesising their engine (saves a lot of audio CPU with a full grid).
            if (!cockpit && AudioListenerRef.Listener != null && src != null)
            {
                float far = (AudioListenerRef.Listener.position - transform.position).sqrMagnitude;
                if (far > 460f * 460f) { if (src.isPlaying) src.Stop(); return; }
                if (!src.isPlaying && enabled) src.Play();
            }

            // Doppler for cars passing the listener.
            if (!cockpit && AudioListenerRef.Listener != null)
            {
                Vector3 v = (transform.position - lastPos) / Mathf.Max(1e-4f, Time.deltaTime);
                Vector3 toListener = AudioListenerRef.Listener.position - transform.position;
                float d = toListener.magnitude;
                if (d > 0.1f)
                {
                    Vector3 dir = toListener / d;
                    float vs = Vector3.Dot(v, dir);
                    float vl = Vector3.Dot(AudioListenerRef.Velocity, -dir);
                    pDoppler = Mathf.Clamp((343f + vl) / Mathf.Max(60f, 343f - vs), 0.6f, 1.6f);
                }
            }
            else pDoppler = 1f;
            lastPos = transform.position;
        }

        float Noise()
        {
            rnd ^= rnd << 13; rnd ^= rnd >> 17; rnd ^= rnd << 5;
            return (rnd & 0xFFFF) / 32768f - 1f;
        }

        static float Sin(double ph)
        {
            double f = ph - System.Math.Floor(ph);
            float x = (float)(f * Table);
            int i = (int)x;
            float t = x - i;
            return sine[i] + (sine[i + 1] - sine[i]) * t;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sine == null) return;
            float dt = 1f / rate;
            int layout = pLayout, cyl = pCyl;
            float targetRpm = pRpm, targetLoad = pThrottle * 0.6f + Mathf.Max(0f, pLoad) * 0.4f;
            float speed = pSpeed;
            float vol = pVolume;
            float doppler = pDoppler;
            int n = data.Length / channels;
            for (int s = 0; s < n; s++)
            {
                smoothRpm += (targetRpm - smoothRpm) * 0.0015f;
                smoothLoad += (targetLoad - smoothLoad) * 0.0008f;
                smoothVol += (vol - smoothVol) * 0.001f;
                float rpm = Mathf.Max(400f, smoothRpm);
                float fire = rpm / 60f * cyl * 0.5f * doppler; // firing frequency
                phase += fire * dt;
                float load = smoothLoad;

                // Harmonic stack.
                float e = 0f;
                float bright = 0.35f + load * 0.65f;
                switch (layout)
                {
                    case 1: // V10: bright, howling upper orders
                        e = Sin(phase) * 0.55f + Sin(phase * 2) * 0.4f * bright + Sin(phase * 3) * 0.32f * bright + Sin(phase * 4) * 0.22f * bright + Sin(phase * 5) * 0.16f * bright + Sin(phase * 0.5) * 0.12f;
                        break;
                    case 2: // modern V8: hard edged
                        e = Sin(phase) * 0.6f + Sin(phase * 2) * 0.35f * bright + Sin(phase * 3) * 0.22f * bright + Sin(phase * 0.5) * 0.2f + Sin(phase * 1.5) * 0.12f;
                        break;
                    case 3: // classic V8: lumpy burble with half orders
                        e = Sin(phase) * 0.55f + Sin(phase * 0.5) * 0.38f + Sin(phase * 1.5) * 0.25f + Sin(phase * 2) * 0.22f * bright + Sin(phase * 0.25) * 0.1f;
                        e *= 0.85f + 0.15f * Sin(phase * 0.25 + 0.3);
                        break;
                    case 4: // turbo four: buzzy second order
                        e = Sin(phase) * 0.5f + Sin(phase * 2) * 0.45f + Sin(phase * 3) * 0.2f * bright + Sin(phase * 4) * 0.12f * bright;
                        break;
                    case 5: // V12: silky
                        e = Sin(phase) * 0.6f + Sin(phase * 2) * 0.28f * bright + Sin(phase * 3) * 0.18f * bright + Sin(phase * 4) * 0.08f;
                        break;
                    default: // hybrid V6 turbo: muted, with a whistle on top
                        e = Sin(phase) * 0.6f + Sin(phase * 2) * 0.3f * bright + Sin(phase * 3) * 0.16f * bright + Sin(phase * 0.5) * 0.15f;
                        break;
                }
                // Combustion roughness and intake noise.
                float nz = Noise();
                lp1 += (nz - lp1) * Mathf.Clamp01(fire * 6f * dt * 6.28f);
                e += lp1 * (0.12f + load * 0.25f);
                // Saturation grows with load.
                float drive = 1.2f + load * 2.2f;
                e = (float)System.Math.Tanh(e * drive) * (0.55f + load * 0.45f);
                // Limiter chop.
                if (pLimiter > 0.5f) e *= Mathf.Repeat((float)phase * 0.02f, 1f) > 0.45f ? 1f : 0.15f;
                // Overrun crackle when lifting at high revs (older engines especially).
                if (load < 0.1f && rpm > 6000f && (layout == 1 || layout == 3 || layout == 2))
                {
                    if (Noise() > 0.9993f) crackle = 1f;
                }
                crackle *= 0.9985f;
                e += crackle * Noise() * 0.6f;
                // Turbo whistle.
                if (layout == 0 || layout == 4)
                {
                    turboPhase += (2200f + rpm * 0.55f) * doppler * dt;
                    e += Sin(turboPhase) * 0.035f * load;
                }
                // Gear change thump.
                if (pShiftKick > 0.5f) { gearThump = 1f; pShiftKick = 0f; }
                gearThump *= 0.9994f;
                e += Sin(phase * 0.25) * gearThump * 0.25f;

                float outS = e * 0.28f;

                if (cockpit)
                {
                    // Wind roar.
                    float w = Noise();
                    float cutoff = Mathf.Clamp01(0.002f + speed * 0.0004f);
                    windLp += (w - windLp) * cutoff;
                    outS += windLp * Mathf.Min(1.2f, speed * speed * 0.00012f) * 0.6f;
                    // Tyre squeal (band-passed, wavering).
                    if (pSlide > 0.05f && speed > 3f)
                    {
                        squealPhase += (950f + Noise() * 60f + speed * 2f) * dt;
                        bp1 += (Noise() - bp1) * 0.12f;
                        outS += (Sin(squealPhase) * 0.6f + bp1 * 0.4f) * Mathf.Clamp01(pSlide) * (1f - pWet * 0.7f) * 0.28f;
                    }
                    // Kerb and off-road rumble, plank scraping.
                    float rumbleF = 12f + speed * 1.5f;
                    kerbPhase += rumbleF * dt;
                    float kerb = (Sin(kerbPhase) > 0f ? 1f : -1f) * pKerb * 0.18f;
                    rumbleLp += (Noise() - rumbleLp) * 0.02f;
                    outS += kerb + rumbleLp * pOff * Mathf.Clamp01(speed / 15f) * 0.5f;
                    if (pScrape > 0.05f) { hp = Noise() - hp * 0.3f; outS += hp * pScrape * 0.15f; }
                    // Spray hiss in the wet.
                    if (pWet > 0.2f) { bp2 += (Noise() - bp2) * 0.35f; outS += bp2 * pWet * Mathf.Clamp01(speed / 40f) * 0.1f; }
                }
                outS *= smoothVol;
                for (int c = 0; c < channels; c++) data[s * channels + c] += outS;
            }
        }
    }

    /// <summary>Where the ears are (the VR camera), for Doppler and distance.</summary>
    public static class AudioListenerRef
    {
        public static Transform Listener;
        public static Vector3 Velocity;
    }
}
