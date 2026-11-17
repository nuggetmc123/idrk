using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The bunker's own music, composed and played by the game itself (no samples, nothing copyrighted): a slow,
    /// warm lo-fi / synthwave loop in A minor at 84 BPM. 32 bars - an intro of pads and electric piano, then bass and
    /// soft drums come in, then a lead melody with echo; it loops seamlessly (the reverb tails wrap round to the start).
    /// Rendered on a background thread the first time you're in the bunker, then kept.
    /// </summary>
    public static class Music
    {
        const int SR = 44100;
        const float BPM = 84f;
        static readonly float Beat = 60f / BPM, Bar = 4f * 60f / BPM;
        const int Bars = 32;

        static Task<float[]> _task;
        static AudioClip _clip;

        /// <summary>The bunker track if it's ready (starts rendering it if not).</summary>
        public static AudioClip Bunker()
        {
            if (_clip != null) return _clip;
            if (_task == null) _task = Task.Run(() => Render(20241117));
            if (!_task.IsCompleted) return null;
            if (_task.IsFaulted) { Debug.LogWarning("[Rago] music: " + _task.Exception?.GetBaseException().Message); _task = null; return null; }
            var d = _task.Result;
            _clip = AudioClip.Create("bunker_music", d.Length / 2, 2, SR, false);
            _clip.SetData(d, 0);
            return _clip;
        }

        // ------------------------------------------------------------------------------------------- tables
        const int TL = 4096;
        static float[] _sine, _saw;
        static void Tables()
        {
            if (_sine != null) return;
            _sine = new float[TL + 1]; _saw = new float[TL + 1];
            for (int i = 0; i <= TL; i++)
            {
                double ph = i / (double)TL * System.Math.PI * 2;
                _sine[i] = (float)System.Math.Sin(ph);
                double s = 0; for (int h = 1; h <= 7; h++) s += System.Math.Sin(ph * h) / h * (h == 1 ? 1 : 0.8);   // soft, band-limited saw
                _saw[i] = (float)(s * 0.55);
            }
        }
        static float Tab(float[] t, double phase) { double p = phase - System.Math.Floor(phase); double x = p * TL; int i = (int)x; float f = (float)(x - i); return t[i] + (t[i + 1] - t[i]) * f; }
        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        // ------------------------------------------------------------------------------------------- the song
        // one chord per bar, an 8-bar cycle played 4 times
        static readonly int[] BassNotes = { 45, 41, 48, 43, 45, 41, 38, 40 };                   // A F C G A F D E
        static readonly int[][] Pads =
        {
            new[] { 57, 60, 64, 67, 71 },   // Am9
            new[] { 53, 57, 60, 64 },       // Fmaj7
            new[] { 55, 59, 60, 64 },       // Cmaj7
            new[] { 55, 59, 62, 64 },       // G6
            new[] { 57, 60, 64, 67, 71 },   // Am9
            new[] { 53, 57, 60, 64 },       // Fmaj7
            new[] { 53, 57, 60, 64, 65 },   // Dm9
            new[] { 56, 59, 62, 64 },       // E7
        };
        static readonly int[] Penta = { 69, 72, 74, 76, 79, 81, 84 };   // A minor pentatonic for the lead

        static float[] Render(int seed)
        {
            Tables();
            var rng = new System.Random(seed);
            float len = Bars * Bar;
            int n = (int)(len * SR), tail = (int)(5f * SR), total = n + tail;
            var padL = new float[total]; var padR = new float[total];
            var keyL = new float[total]; var keyR = new float[total];
            var leadL = new float[total]; var leadR = new float[total];
            var bass = new float[total]; var drums = new float[total]; var snareSend = new float[total];
            var kickEnv = new float[total];

            for (int bar = 0; bar < Bars; bar++)
            {
                int c = bar % 8;
                float t0 = bar * Bar;
                bool drumsOn = bar >= 8, bassOn = bar >= 8, leadOn = bar >= 16 && bar < 31;
                // ---- pad: the chord, swelling in and overlapping into the next bar
                foreach (int m in Pads[c]) Pad(padL, padR, t0, Bar + 0.9f, Hz(m), bar < 8 ? 0.055f : 0.045f);
                // ---- electric piano: a gentle arpeggio over the chord (8ths, swung), quieter once the lead is in
                var ch = Pads[c];
                int[] pat = { 0, 2, 1, 3, 2, 1, 3, 2 };
                for (int e = 0; e < 8; e++)
                {
                    if (bar < 2 && e % 2 == 1) continue;                     // a sparse first couple of bars
                    if (rng.NextDouble() < 0.12) continue;                    // breathe
                    float t = t0 + e * Beat * 0.5f + (e % 2 == 1 ? Beat * 0.08f : 0f);
                    int note = ch[pat[e] % ch.Length] + 12;
                    Keys(keyL, keyR, t, Hz(note), (leadOn ? 0.05f : 0.075f) * (e % 2 == 0 ? 1f : 0.75f), -0.25f);
                }
                // ---- bass: root on 1, a push on the "and" of 2, the fifth / octave leading to the next bar
                if (bassOn)
                {
                    int root = BassNotes[c];
                    BassNote(bass, t0, Beat * 1.4f, Hz(root - 12), 0.32f);
                    BassNote(bass, t0 + Beat * 1.5f, Beat * 0.45f, Hz(root - 12), 0.24f);
                    BassNote(bass, t0 + Beat * 2.5f, Beat * 1.0f, Hz(root), 0.22f);
                    BassNote(bass, t0 + Beat * 3.5f, Beat * 0.45f, Hz(root - 5), 0.2f);
                }
                // ---- drums: lazy hip-hop groove
                if (drumsOn)
                {
                    bool fill = bar == 15 || bar == 23 || bar == 30;
                    Kick(drums, kickEnv, t0); Kick(drums, kickEnv, t0 + Beat * 2.5f);
                    if (bar % 2 == 1) Kick(drums, kickEnv, t0 + Beat * 3.75f, 0.6f);
                    Snare(drums, snareSend, t0 + Beat, 1f); Snare(drums, snareSend, t0 + Beat * 3, 1f);
                    if (fill) for (int k = 0; k < 4; k++) Snare(drums, snareSend, t0 + Beat * 3 + k * Beat * 0.25f, 0.35f + k * 0.15f);
                    for (int e = 0; e < 8; e++)
                    {
                        float t = t0 + e * Beat * 0.5f + (e % 2 == 1 ? Beat * 0.09f : 0f);
                        Hat(drums, t, e % 2 == 0 ? 0.065f : 0.04f, e == 7 && bar % 4 == 3 ? 0.18f : 0.035f);
                    }
                }
                // ---- lead: a melody made up two bars at a time from the pentatonic, leaning on chord tones
                if (leadOn && bar % 2 == 0)
                {
                    float t = t0; float end = t0 + 2 * Bar - Beat * 0.5f;
                    int idx = 2 + rng.Next(3);
                    while (t < end)
                    {
                        float dur = Beat * (rng.NextDouble() < 0.5 ? 0.5f : rng.NextDouble() < 0.6 ? 1f : 1.5f);
                        if (t + dur > end) dur = end - t;
                        if (rng.NextDouble() < 0.22) { t += Beat * 0.5f; continue; }   // rests
                        idx = Mathf.Clamp(idx + rng.Next(-2, 3), 0, Penta.Length - 1);
                        int note = Penta[idx];
                        // on the beat, snap to a note of the chord if one is close
                        if (Mathf.Abs((t - t0) / Beat - Mathf.Round((t - t0) / Beat)) < 0.01f)
                            foreach (int m in Pads[(bar + (t - t0 >= Bar ? 1 : 0)) % 8]) { int mm = m + 12; while (mm < 66) mm += 12; if (Mathf.Abs(mm - note) <= 2) { note = mm; break; } }
                        Lead(leadL, leadR, t, dur * 0.95f, Hz(note), 0.07f);
                        t += dur;
                    }
                    // a long held note to close the phrase
                    Lead(leadL, leadR, end, Beat * 0.5f, Hz(Penta[Mathf.Clamp(idx, 0, Penta.Length - 1)]), 0.05f);
                }
            }

            // ---- effects: a slow filter sweep on the pads, ducking under the kick, echo on the lead, reverb on it all
            var lp = new OnePole(); var lp2 = new OnePole();
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)SR;
                float cut = 900f + 700f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / (Bar * 8f)));
                float duck = 1f - 0.35f * kickEnv[i];
                padL[i] = lp.Low(padL[i], cut) * duck; padR[i] = lp2.Low(padR[i], cut) * duck;
            }
            Echo(leadL, leadR, Beat * 0.75f, 0.38f);
            var wetL = new float[total]; var wetR = new float[total];
            for (int i = 0; i < total; i++)
            {
                wetL[i] = padL[i] * 0.6f + keyL[i] * 0.55f + leadL[i] * 0.5f + snareSend[i] * 0.4f;
                wetR[i] = padR[i] * 0.6f + keyR[i] * 0.55f + leadR[i] * 0.5f + snareSend[i] * 0.4f;
            }
            Reverb(wetL, 0); Reverb(wetR, 1);

            // ---- mix, wrap the tail round to the start (seamless loop), gentle tape-style saturation
            var outp = new float[n * 2];
            var bassLp = new OnePole();
            float peak = 0f;
            for (int i = 0; i < total; i++)
            {
                float b = bassLp.Low(bass[i], 300f);
                float l = padL[i] + keyL[i] + leadL[i] + b + drums[i] + wetL[i] * 0.32f;
                float r = padR[i] + keyR[i] + leadR[i] + b + drums[i] + wetR[i] * 0.32f;
                int j = i % n;
                outp[j * 2] += l; outp[j * 2 + 1] += r;
            }
            for (int i = 0; i < outp.Length; i++) { outp[i] = (float)System.Math.Tanh(outp[i] * 1.1f); peak = Mathf.Max(peak, Mathf.Abs(outp[i])); }
            float g = peak > 0 ? 0.85f / peak : 1f;
            for (int i = 0; i < outp.Length; i++) outp[i] *= g;
            return outp;
        }

        // ------------------------------------------------------------------------------------------- instruments
        static void Pad(float[] L, float[] R, float start, float dur, float hz, float amp)
        {
            int s = (int)(start * SR), e = Mathf.Min(L.Length, s + (int)((dur + 1.2f) * SR));
            double p1 = 0, p2 = 0.3, p3 = 0.6, d1 = hz / SR, d2 = hz * 1.0035 / SR, d3 = hz * 0.9968 / SR;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = t < 0.7f ? t / 0.7f : t < dur ? 1f : Mathf.Max(0f, 1f - (t - dur) / 1.2f);
                env *= env;
                float a = Tab(_saw, p1), b = Tab(_saw, p2), c = Tab(_saw, p3);
                L[i] += (a + b * 0.8f) * env * amp; R[i] += (a + c * 0.8f) * env * amp;   // detuned voices split left / right: wide
                p1 += d1; p2 += d2; p3 += d3;
            }
        }

        /// <summary>A Rhodes-like electric piano: two-operator FM with a bright attack that mellows.</summary>
        static void Keys(float[] L, float[] R, float start, float hz, float amp, float pan)
        {
            int s = (int)(start * SR), e = Mathf.Min(L.Length, s + (int)(2.2f * SR));
            double pc = 0, pm = 0, dc = hz / SR, dm = hz / SR;
            float gl = Mathf.Sqrt(0.5f * (1f - pan)), gr = Mathf.Sqrt(0.5f * (1f + pan));
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 0.9f);
                float idx = 0.4f + 2.2f * Mathf.Exp(-t / 0.12f);
                float mod = Tab(_sine, pm) * idx;
                float v = Tab(_sine, pc + mod / (2 * System.Math.PI)) * env * amp * (1f + 0.08f * Mathf.Sin(t * 2 * Mathf.PI * 4.5f));   // a touch of tremolo
                L[i] += v * gl; R[i] += v * gr;
                pc += dc; pm += dm;
            }
        }

        static void BassNote(float[] B, float start, float dur, float hz, float amp)
        {
            int s = (int)(start * SR), e = Mathf.Min(B.Length, s + (int)((dur + 0.08f) * SR));
            double p = 0, d = hz / SR;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = Mathf.Min(1f, t / 0.006f) * (t < dur ? Mathf.Exp(-t / 1.2f) : Mathf.Exp(-t / 1.2f) * Mathf.Max(0f, 1f - (t - dur) / 0.08f));
                float v = Tab(_sine, p) + Tab(_sine, p * 2) * 0.25f + Tab(_sine, p * 3) * 0.08f;
                B[i] += v * env * amp;
                p += d;
            }
        }

        static void Kick(float[] D, float[] env, float start, float amp = 1f)
        {
            int s = (int)(start * SR), e = Mathf.Min(D.Length, s + (int)(0.45f * SR));
            double ph = 0;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float f = 45f + 85f * Mathf.Exp(-t / 0.035f);
                ph += f / SR;
                float a = Mathf.Exp(-t / 0.22f);
                D[i] += Tab(_sine, ph) * a * 0.42f * amp + (t < 0.003f ? 0.15f * amp : 0f);
                env[i] = Mathf.Max(env[i], a);
            }
        }

        static uint _ns = 22222;
        static float Noise() { _ns ^= _ns << 13; _ns ^= _ns >> 17; _ns ^= _ns << 5; return (_ns / 4294967295f) * 2f - 1f; }

        static void Snare(float[] D, float[] send, float start, float amp)
        {
            int s = (int)(start * SR), e = Mathf.Min(D.Length, s + (int)(0.3f * SR));
            var hp = new OnePole(); var lp = new OnePole();
            double ph = 0;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float nz = Noise(); float body = lp.Low(nz - hp.Low(nz, 900f), 6000f);
                float v = body * Mathf.Exp(-t / 0.09f) * 0.28f + Tab(_sine, ph) * Mathf.Exp(-t / 0.04f) * 0.14f;
                ph += 185f / SR;
                D[i] += v * amp; send[i] += v * amp;
            }
        }

        static void Hat(float[] D, float start, float amp, float decay)
        {
            int s = (int)(start * SR), e = Mathf.Min(D.Length, s + (int)((decay * 5f) * SR));
            var lp = new OnePole();
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float nz = Noise(); float hi = nz - lp.Low(nz, 7000f);
                D[i] += hi * Mathf.Exp(-t / decay) * amp;
            }
        }

        /// <summary>A soft lead: a few odd harmonics with a slow vibrato, panned a little right.</summary>
        static void Lead(float[] L, float[] R, float start, float dur, float hz, float amp)
        {
            int s = (int)(start * SR), e = Mathf.Min(L.Length, s + (int)((dur + 0.35f) * SR));
            double p = 0;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = Mathf.Min(1f, t / 0.03f) * (t < dur ? 1f - 0.25f * Mathf.Min(1f, t / 1.5f) : Mathf.Max(0f, 1f - (t - dur) / 0.35f) * 0.75f);
                float vib = 1f + 0.004f * Mathf.Sin(t * 2 * Mathf.PI * 5.2f) * Mathf.Min(1f, Mathf.Max(0f, t - 0.25f) * 3f);
                p += hz * vib / SR;
                float v = Tab(_sine, p) + Tab(_sine, p * 3) * 0.18f + Tab(_sine, p * 5) * 0.06f + Tab(_sine, p * 2) * 0.12f;
                v *= env * amp;
                L[i] += v * 0.8f; R[i] += v;
            }
        }

        static void Echo(float[] L, float[] R, float delay, float fb)
        {
            int d = (int)(delay * SR);
            // ping-pong: left echoes into right and back
            for (int i = d; i < L.Length; i++) { float l = L[i - d], r = R[i - d]; L[i] += r * fb; R[i] += l * fb; }
        }

        struct OnePole { float y; public float Low(float x, float hz) { float a = 1f - Mathf.Exp(-2f * Mathf.PI * hz / SR); y += a * (x - y); return y; } }

        /// <summary>A small Schroeder reverb: parallel combs into series all-passes (slightly different per side).</summary>
        static void Reverb(float[] x, int side)
        {
            int[] combs = { 1557, 1617, 1491, 1422, 1277, 1356 };
            int[] aps = { 225, 556, 441 };
            int sp = side * 23;
            var y = new float[x.Length];
            foreach (int c0 in combs)
            {
                int c = c0 + sp; var buf = new float[c]; int bi = 0; float filt = 0f;
                for (int i = 0; i < x.Length; i++)
                {
                    float o = buf[bi];
                    filt = o * 0.75f + filt * 0.25f;            // damping
                    buf[bi] = x[i] + filt * 0.84f;
                    y[i] += o;
                    if (++bi >= c) bi = 0;
                }
            }
            foreach (int a0 in aps)
            {
                int a = a0 + sp; var buf = new float[a]; int bi = 0;
                for (int i = 0; i < y.Length; i++)
                {
                    float bo = buf[bi]; float v = -y[i] * 0.5f + bo;
                    buf[bi] = y[i] + bo * 0.5f; y[i] = v;
                    if (++bi >= a) bi = 0;
                }
            }
            for (int i = 0; i < x.Length; i++) x[i] = y[i] * 0.18f;
        }
    }
}
