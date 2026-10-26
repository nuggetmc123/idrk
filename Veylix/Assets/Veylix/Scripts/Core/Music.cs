using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The soundtrack, synthesised live (no audio files): a relaxed synthwave theme in the garage, an ambient bed while
    /// a map loads and a driving 128 BPM track for racing (optional: Settings or the watch). Each track is a little
    /// arrangement: drums (kick, clap, hats), a plucked bass, detuned pad chords, an arpeggio through a stereo delay,
    /// sidechain pumping from the kick, and sections that drop out and come back so it doesn't loop like a jingle.
    /// Tracks crossfade when the game changes state.
    /// </summary>
    public class Music : MonoBehaviour
    {
        public enum Theme { None, Garage, Loading, Race }

        public static Music I { get; private set; }

        class Track
        {
            public float bpm;
            public int[][] chords;   // per bar, MIDI notes (root first)
            public int kick, clap, hat, openHat; // 16-step bitmasks
            public int[] bass;       // 16 steps: semitones above the chord root (-1 = rest), +12 = octave
            public int[] arp;        // 16 steps: chord tone index (+3 = an octave up), -1 = rest
            public float pad, bassLevel, arpLevel, drums, cutoff;
            public int breakEvery;   // bars between breakdowns (drums drop for 2 bars), 0 = never
        }

        static readonly Track[] Tracks =
        {
            null,
            new Track // Garage: warm, unhurried
            {
                bpm = 96, chords = new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } },
                kick = Mask(0, 8), clap = Mask(4, 12), hat = Mask(2, 6, 10, 14), openHat = 0,
                bass = new[] { 0, -1, -1, 0, -1, -1, 12, -1, 0, -1, -1, 0, -1, 7, -1, -1 },
                arp = new[] { 0, -1, 1, -1, 2, -1, 3, -1, 2, -1, 1, -1, 0, -1, 1, -1 },
                pad = 0.5f, bassLevel = 0.55f, arpLevel = 0.22f, drums = 0.45f, cutoff = 0.25f, breakEvery = 8,
            },
            new Track // Loading: a slow bed, no drums
            {
                bpm = 80, chords = new[] { new[] { 52, 55, 59 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 }, new[] { 50, 54, 57 } },
                kick = 0, clap = 0, hat = 0, openHat = 0,
                bass = new[] { 0, -1, -1, -1, -1, -1, -1, -1, 0, -1, -1, -1, -1, -1, -1, -1 },
                arp = new[] { 2, -1, -1, -1, 4, -1, -1, -1, 3, -1, -1, -1, 1, -1, -1, -1 },
                pad = 0.65f, bassLevel = 0.35f, arpLevel = 0.18f, drums = 0f, cutoff = 0.18f, breakEvery = 0,
            },
            new Track // Race: driving, minor key
            {
                bpm = 128, chords = new[] { new[] { 50, 53, 57 }, new[] { 46, 50, 53 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 } },
                kick = Mask(0, 4, 8, 12), clap = Mask(4, 12), hat = Mask(0, 2, 4, 6, 8, 10, 12, 14), openHat = Mask(2, 6, 10, 14),
                bass = new[] { 0, 12, 0, 12, 0, 12, 0, 12, 0, 12, 0, 12, 0, 12, 7, 12 },
                arp = new[] { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 4, 3, 2, 1, 2, 3 },
                pad = 0.35f, bassLevel = 0.6f, arpLevel = 0.2f, drums = 0.7f, cutoff = 0.4f, breakEvery = 16,
            },
        };

        static int Mask(params int[] steps) { int m = 0; foreach (var s in steps) m |= 1 << s; return m; }

        // Main thread -> audio thread.
        volatile int wanted;
        volatile float volume;
        AudioSource src;

        // Audio thread state.
        int playing, step = -1, bar;
        double sampleInStep;
        float gain, rate;
        uint rng = 2463534242u;
        float kickEnv, kickPhase, clapEnv, hatEnv, openEnv;
        float bassFreq, bassEnv, bassPhase, bassPhase2, bassLp;
        float arpFreq, arpEnv, arpPhase;
        readonly float[] padPhase = new float[6];
        float padEnv, padLpL, padLpR, lfo, duck;
        float hatHp, hatPrev, clapBp1, clapBp2;
        float[] delayL, delayR;
        int delayPos, delayLen;
        int[] chord = { 57, 60, 64 };
        readonly float[] padFreq = { 220f, 261.6f, 329.6f };

        void Awake()
        {
            I = this;
            rate = AudioSettings.outputSampleRate;
            delayLen = Mathf.Max(1, Mathf.RoundToInt(rate * 0.75f));
            delayL = new float[(int)rate * 2];
            delayR = new float[(int)rate * 2];
            src = gameObject.AddComponent<AudioSource>();
            src.clip = AudioClip.Create("music", 1024, 1, (int)rate, false);
            src.loop = true;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true; // keeps playing on the pause screen
            src.priority = 0;
            src.Play();
        }

        public static void Play(Theme t) { if (I != null) I.wanted = (int)t; }

        void Update()
        {
            volume = Settings.MasterVolume * Settings.MusicVolume;
            if (src != null && !src.isPlaying) src.Play();
        }

        static float Midi(int n) => 440f * Mathf.Pow(2f, (n - 69) / 12f);

        float Noise()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 8388608f - 1f;
        }

        // Saw with a little smoothing at the wrap (cheap band-limiting).
        static float Saw(float ph, float dt)
        {
            float s = 2f * ph - 1f;
            if (ph < dt) { float t = ph / dt; s -= t + t - t * t - 1f; }
            else if (ph > 1f - dt) { float t = (ph - 1f) / dt; s -= t * t + t + t + 1f; }
            return s;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float vol = volume;
            int want = wanted;
            float inv = 1f / rate;
            for (int i = 0; i < data.Length; i += channels)
            {
                // Crossfade: fade out, swap, fade in.
                float target = want == playing && playing != 0 ? 1f : 0f;
                gain += (target - gain) * 0.00008f;
                if (want != playing && gain < 0.01f) { playing = want; step = -1; sampleInStep = 0; bar = 0; }
                var tr = Tracks[playing];
                if (tr == null) { for (int c = 0; c < channels; c++) data[i + c] = 0f; continue; }

                // Sequencer: sixteenth notes.
                double samplesPerStep = rate * 60.0 / tr.bpm / 4.0;
                sampleInStep += 1.0;
                if (step < 0 || sampleInStep >= samplesPerStep)
                {
                    sampleInStep = step < 0 ? 0 : sampleInStep - samplesPerStep;
                    step = (step + 1) % 16;
                    if (step == 0 || step < 0) { bar++; chord = tr.chords[bar % tr.chords.Length]; padEnv = Mathf.Min(padEnv, 0.6f); for (int v = 0; v < 3; v++) padFreq[v] = Midi(chord[v]); }
                    bool breakdown = tr.breakEvery > 0 && bar % tr.breakEvery >= tr.breakEvery - 2;
                    int bit = 1 << step;
                    if (!breakdown)
                    {
                        if ((tr.kick & bit) != 0) { kickEnv = 1f; kickPhase = 0f; duck = 1f; }
                        if ((tr.clap & bit) != 0) clapEnv = 1f;
                        if ((tr.openHat & bit) != 0) openEnv = 1f;
                        else if ((tr.hat & bit) != 0) hatEnv = 1f;
                    }
                    int b = tr.bass[step];
                    if (b >= 0 && !(breakdown && step % 4 != 0)) { bassFreq = Midi(chord[0] - 24 + b); bassEnv = 1f; }
                    int a = tr.arp[step];
                    if (a >= 0) { arpFreq = Midi(chord[a % 3] + 12 * (a / 3) + 12); arpEnv = 1f; }
                }

                // Drums.
                float d = 0f;
                if (kickEnv > 0.0005f)
                {
                    float f = 45f + 85f * kickEnv * kickEnv;
                    kickPhase += f * inv;
                    d += Mathf.Sin(kickPhase * 6.2831853f) * kickEnv * 0.9f;
                    kickEnv *= 0.9998f;
                }
                if (clapEnv > 0.0005f)
                {
                    float n = Noise();
                    clapBp1 += (n - clapBp1) * 0.35f; clapBp2 += (clapBp1 - clapBp2) * 0.35f;
                    d += (clapBp1 - clapBp2) * clapEnv * 1.2f;
                    clapEnv *= 0.9991f;
                }
                if (hatEnv > 0.0005f || openEnv > 0.0005f)
                {
                    float n = Noise();
                    float hp = n - hatPrev; hatPrev = n;
                    hatHp += (hp - hatHp) * 0.5f;
                    d += hatHp * (hatEnv * 0.2f + openEnv * 0.12f);
                    hatEnv *= 0.997f; openEnv *= 0.99965f;
                }
                d *= tr.drums;

                // Sidechain: everything else ducks under the kick.
                duck *= 0.99975f;
                float pump = 1f - duck * 0.55f * tr.drums;

                // Bass: saw + sub through an envelope-swept low-pass.
                float bass = 0f;
                if (bassEnv > 0.0005f)
                {
                    bassPhase += bassFreq * inv; if (bassPhase >= 1f) bassPhase -= 1f;
                    bassPhase2 += bassFreq * 0.5f * inv; if (bassPhase2 >= 1f) bassPhase2 -= 1f;
                    float raw = Saw(bassPhase, bassFreq * inv) * 0.6f + Mathf.Sin(bassPhase2 * 6.2831853f) * 0.6f;
                    float cut = 0.02f + 0.18f * bassEnv * bassEnv;
                    bassLp += (raw - bassLp) * cut;
                    bass = bassLp * Mathf.Min(1f, bassEnv * 3f) * tr.bassLevel;
                    bassEnv *= 0.99985f;
                }

                // Pad: three chord tones, two detuned saws each, slow swell and a breathing filter.
                padEnv += (1f - padEnv) * 0.00003f;
                lfo += 0.08f * inv; if (lfo > 1f) lfo -= 1f;
                float pl = 0f, pr = 0f;
                for (int v = 0; v < 6; v++)
                {
                    float f = padFreq[v % 3] * (v < 3 ? 0.997f : 1.003f);
                    padPhase[v] += f * inv; if (padPhase[v] >= 1f) padPhase[v] -= 1f;
                    float sv = Saw(padPhase[v], f * inv);
                    if (v < 3) pl += sv; else pr += sv;
                }
                float pc = tr.cutoff * (0.6f + 0.4f * Mathf.Sin(lfo * 6.2831853f)) * 0.25f + 0.01f;
                padLpL += (pl - padLpL) * pc; padLpR += (pr - padLpR) * pc;
                float padL = padLpL * 0.22f * padEnv * tr.pad, padR = padLpR * 0.22f * padEnv * tr.pad;

                // Arp: soft square pluck, sent to a ping-pong delay.
                float arp = 0f;
                if (arpEnv > 0.0005f)
                {
                    arpPhase += arpFreq * inv; if (arpPhase >= 1f) arpPhase -= 1f;
                    arp = (arpPhase < 0.5f ? 0.5f : -0.5f) * arpEnv * tr.arpLevel;
                    arpEnv *= 0.9996f;
                }
                int rd = (delayPos - delayLen + delayL.Length) % delayL.Length;
                float echoL = delayR[rd], echoR = delayL[rd];
                delayL[delayPos] = arp * 0.8f + echoL * 0.35f + padL * 0.15f;
                delayR[delayPos] = echoR * 0.35f + padR * 0.15f;
                delayPos = (delayPos + 1) % delayL.Length;

                float L = d + (bass + padL + arp * 0.7f) * pump + echoL * 0.5f;
                float R = d + (bass + padR + arp * 0.5f) * pump + echoR * 0.5f;
                float g = gain * vol * 0.75f;
                L *= g; R *= g;
                L = L / (1f + Mathf.Abs(L)); R = R / (1f + Mathf.Abs(R)); // soft clip
                if (channels >= 2) { data[i] = L; data[i + 1] = R; for (int c = 2; c < channels; c++) data[i + c] = 0f; }
                else data[i] = (L + R) * 0.5f;
            }
        }
    }
}
