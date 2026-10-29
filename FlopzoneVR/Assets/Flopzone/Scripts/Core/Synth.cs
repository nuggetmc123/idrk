using System;
using System.Collections.Generic;

namespace Flopzone
{
    /// <summary>
    /// Makes every sound effect and both music tracks from scratch, so the
    /// project ships with no audio files. Pure math (no Unity calls), so it
    /// runs on a worker thread while the welcome screen is up.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;

        public static Dictionary<string, float[]> BuildAll()
        {
            var d = new Dictionary<string, float[]>();
            d["shot_pistol"] = Gunshot(0.32f, 900f, 0.55f, 180f, 1);
            d["shot_smg"] = Gunshot(0.22f, 1400f, 0.45f, 210f, 2);
            d["shot_ar"] = Gunshot(0.42f, 1100f, 0.7f, 140f, 3);
            d["shot_shotgun"] = Gunshot(0.7f, 600f, 1.0f, 90f, 4);
            d["shot_sniper"] = Gunshot(1.1f, 750f, 1.0f, 70f, 5, crackDelay: 0.0f, tail: 0.9f);
            d["mag_out"] = Click(0.09f, 2600f, 0.6f, 11, 2);
            d["mag_in"] = Click(0.12f, 1900f, 0.9f, 12, 3);
            d["slide"] = Slide();
            d["dry"] = Click(0.05f, 3500f, 0.5f, 13, 1);
            d["grab"] = Click(0.06f, 1200f, 0.4f, 14, 1);
            d["holster"] = Thud(0.12f, 160f, 0.5f, 15);
            d["hit_body"] = Thud(0.18f, 110f, 0.9f, 16);
            d["hit_head"] = Ding(1320f, 0.35f, 0.7f);
            d["hitmarker"] = Ding(2600f, 0.06f, 0.5f);
            d["flop"] = Flop();
            d["hurt"] = Hurt();
            d["impact"] = Click(0.08f, 800f, 0.5f, 17, 1);
            d["ricochet"] = Ricochet();
            // footsteps: four slightly different ones so walking doesn't sound like a loop
            for (int i = 0; i < StepVariants; i++) d["step" + i] = Footstep(40 + i * 7);
            d["step"] = d["step0"];
            d["jump"] = Sweep(220f, 420f, 0.14f, 0.35f, Wave.Triangle);
            d["land"] = Thud(0.14f, 70f, 0.6f, 19);
            d["ui_click"] = Ding(1800f, 0.05f, 0.45f);
            d["ui_hover"] = Ding(2400f, 0.025f, 0.15f);
            d["ui_back"] = Sweep(900f, 500f, 0.08f, 0.35f, Wave.Square);
            d["toast"] = Arp(new[] { 784f, 1046f }, 0.08f, 0.35f);
            d["beep"] = Tone(880f, 0.15f, 0.4f, Wave.Square);
            d["go"] = Arp(new[] { 523f, 659f, 784f, 1046f }, 0.07f, 0.5f);
            d["win"] = Arp(new[] { 523f, 659f, 784f, 1046f, 1318f, 1568f }, 0.12f, 0.5f);
            d["lose"] = Arp(new[] { 392f, 330f, 262f, 196f }, 0.2f, 0.45f);
            d["flag_take"] = Arp(new[] { 659f, 880f, 1175f }, 0.09f, 0.5f);
            d["flag_cap"] = Arp(new[] { 523f, 784f, 1046f, 784f, 1046f, 1568f }, 0.1f, 0.55f);
            d["portal"] = Whoosh();
            d["spawn"] = Sweep(300f, 1200f, 0.35f, 0.3f, Wave.Sine);
            d["chat"] = Ding(1500f, 0.08f, 0.3f);
            d["music_hub"] = HubMusic();
            d["music_match"] = MatchMusic();
            return d;
        }

        enum Wave { Sine, Square, Saw, Triangle }

        static float Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Saw: return (float)(2 * p - 1);
                case Wave.Triangle: return (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p);
                default: return (float)Math.Sin(2 * Math.PI * p);
            }
        }

        static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

        static void Normalize(float[] b, float peak)
        {
            float m = 0.0001f;
            foreach (var s in b) m = Math.Max(m, Math.Abs(s));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        /// <summary>Noise crack + low thump + filtered tail.</summary>
        static float[] Gunshot(float len, float cutoff, float body, float thumpHz, int seed, float crackDelay = 0f, float tail = 0.5f)
        {
            var r = new Random(seed);
            var b = Buf(len);
            float lp = 0, lp2 = 0;
            float a = cutoff / (cutoff + Rate / (2f * (float)Math.PI));
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)(r.NextDouble() * 2 - 1);
                lp += a * (n - lp);
                lp2 += 0.08f * (lp - lp2);
                float crack = n * (float)Math.Exp(-t * 90) * 0.9f;
                float bodyEnv = (float)Math.Exp(-t * (7f / Math.Max(0.1f, tail)));
                float thump = (float)Math.Sin(2 * Math.PI * thumpHz * t * (1 - t * 1.5f)) * (float)Math.Exp(-t * 18) * body;
                b[i] = crack + lp * bodyEnv * 1.4f + lp2 * bodyEnv * 2f + thump;
                if (i < 30) b[i] *= i / 30f;
            }
            Normalize(b, 0.95f);
            return b;
        }

        static float[] Click(float len, float freq, float vol, int seed, int clicks)
        {
            var r = new Random(seed);
            var b = Buf(len);
            for (int c = 0; c < clicks; c++)
            {
                int start = (int)(c * len / clicks * Rate);
                float f = freq * (0.8f + 0.4f * (float)r.NextDouble());
                for (int i = start; i < b.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = (float)Math.Exp(-t * 160);
                    b[i] += ((float)Math.Sin(2 * Math.PI * f * t) * 0.6f + (float)(r.NextDouble() * 2 - 1) * 0.5f) * env;
                }
            }
            Normalize(b, vol);
            return b;
        }

        static float[] Slide()
        {
            var r = new Random(21);
            var b = Buf(0.22f);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)(r.NextDouble() * 2 - 1);
                lp += 0.3f * (n - lp);
                float env = t < 0.12f ? (float)Math.Sin(t / 0.12f * Math.PI) * 0.4f : 0f;
                float snap = t > 0.13f ? (float)Math.Exp(-(t - 0.13f) * 140) * n : 0f;
                b[i] = lp * env + snap;
            }
            Normalize(b, 0.7f);
            return b;
        }

        public const int StepVariants = 4;

        /// <summary>
        /// A boot on hard ground: the heel lands (a dull knock with a gritty crunch),
        /// then the toe rolls down a moment later (a quieter scuff of grit).
        /// </summary>
        static float[] Footstep(int seed)
        {
            var r = new Random(seed);
            var b = Buf(0.24f);
            float knockHz = 85f + (float)r.NextDouble() * 30f;
            float toeAt = 0.055f + (float)r.NextDouble() * 0.03f;
            float grit = 0.6f + (float)r.NextDouble() * 0.5f;
            float lowA = 0, lowB = 0, mid = 0;
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)(r.NextDouble() * 2 - 1);
                // crackle: now and then a sharper grain of sand or gravel
                if (r.NextDouble() < 0.025) n *= 3f;
                lowA += 0.08f * (n - lowA);      // dull body of the impact
                lowB += 0.3f * (n - lowB);
                mid += 0.5f * ((lowB - lowA) - mid);   // a band of grit in the middle
                // heel: very fast attack, quick decay, the knock drops in pitch as the sole flattens
                float heelEnv = t < 0.0015f ? t / 0.0015f : (float)Math.Exp(-(t - 0.0015f) * 48);
                phase += knockHz * (1 + 0.6 * Math.Exp(-t * 70)) / Rate;
                float knock = (float)Math.Sin(2 * Math.PI * phase);
                float heel = (knock * 0.55f + lowA * 2.2f + mid * grit) * heelEnv;
                // toe: a softer, brighter scuff
                float td = t - toeAt;
                float toeEnv = td < 0 ? 0 : td < 0.004f ? td / 0.004f : (float)Math.Exp(-(td - 0.004f) * 38);
                float toe = (lowA * 1.1f + mid * 1.4f * grit) * toeEnv * 0.45f;
                b[i] = heel + toe;
            }
            Normalize(b, 0.8f);
            return b;
        }

        static float[] Thud(float len, float freq, float vol, int seed)
        {
            var r = new Random(seed);
            var b = Buf(len);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                lp += 0.05f * ((float)(r.NextDouble() * 2 - 1) - lp);
                float env = (float)Math.Exp(-t * 30);
                b[i] = ((float)Math.Sin(2 * Math.PI * freq * t * (1 - t)) + lp * 3f) * env;
            }
            Normalize(b, vol);
            return b;
        }

        static float[] Ding(float freq, float len, float vol)
        {
            var b = Buf(len);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * (5f / len)) * Math.Min(1f, i / 40f);
                b[i] = ((float)Math.Sin(2 * Math.PI * freq * t) + 0.3f * (float)Math.Sin(2 * Math.PI * freq * 2.01f * t)) * env;
            }
            Normalize(b, vol);
            return b;
        }

        static float[] Tone(float freq, float len, float vol, Wave w)
        {
            var b = Buf(len);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Math.Min(1f, i / 80f) * Math.Min(1f, (b.Length - i) / 200f);
                b[i] = Osc(w, freq * t) * env;
            }
            Normalize(b, vol);
            return b;
        }

        static float[] Sweep(float f0, float f1, float len, float vol, Wave w)
        {
            var b = Buf(len);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float k = (float)i / b.Length;
                float f = f0 + (f1 - f0) * k;
                ph += f / Rate;
                float env = Math.Min(1f, i / 60f) * (1 - k);
                b[i] = Osc(w, ph) * env;
            }
            Normalize(b, vol);
            return b;
        }

        static float[] Arp(float[] notes, float step, float vol)
        {
            var b = Buf(step * notes.Length + 0.35f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < b.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = (float)Math.Exp(-t * 9) * Math.Min(1f, (i - start) / 60f);
                    b[i] += (Osc(Wave.Square, notes[n] * t) * 0.35f + Osc(Wave.Sine, notes[n] * t) * 0.65f) * env;
                }
            }
            Normalize(b, vol);
            return b;
        }

        /// <summary>The comedic "flop" of a body hitting the floor.</summary>
        static float[] Flop()
        {
            var r = new Random(31);
            var b = Buf(0.45f);
            float lp = 0;
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 260f * (float)Math.Exp(-t * 7) + 60f;
                ph += f / Rate;
                lp += 0.1f * ((float)(r.NextDouble() * 2 - 1) - lp);
                float env = (float)Math.Exp(-t * 9);
                b[i] = (Osc(Wave.Sine, ph) * 0.8f + lp * 2.5f) * env;
            }
            Normalize(b, 0.9f);
            return b;
        }

        static float[] Hurt()
        {
            var b = Buf(0.25f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 180f - t * 300f;
                ph += f / Rate;
                float env = (float)Math.Exp(-t * 12) * Math.Min(1f, i / 100f);
                b[i] = (Osc(Wave.Saw, ph) * 0.5f + Osc(Wave.Sine, ph * 2) * 0.5f) * env;
            }
            Normalize(b, 0.6f);
            return b;
        }

        static float[] Ricochet()
        {
            var b = Buf(0.35f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 3200f * (float)Math.Exp(-t * 4) + 400f;
                ph += f / Rate;
                float env = (float)Math.Exp(-t * 10);
                b[i] = Osc(Wave.Sine, ph) * env;
            }
            Normalize(b, 0.4f);
            return b;
        }

        static float[] Whoosh()
        {
            var r = new Random(41);
            var b = Buf(0.8f);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float k = (float)i / b.Length;
                float cut = 0.02f + 0.25f * (float)Math.Sin(k * Math.PI);
                lp += cut * ((float)(r.NextDouble() * 2 - 1) - lp);
                b[i] = lp * (float)Math.Sin(k * Math.PI);
            }
            Normalize(b, 0.6f);
            return b;
        }

        /* ---------------- music ---------------- */

        static float Midi(int note) => 440f * (float)Math.Pow(2, (note - 69) / 12.0);

        class Song
        {
            public float[] buf;
            public float spb; // seconds per beat
            public Song(float bpm, int bars) { spb = 60f / bpm; buf = Buf(bars * 4 * spb); }

            public void Note(float beat, float beats, float freq, float vol, Wave w, float attack, float decay, float cutoff = 1f)
            {
                int start = (int)(beat * spb * Rate);
                int len = (int)(beats * spb * Rate);
                int rel = (int)(0.08f * Rate);
                float lp = 0;
                double ph = 0, ph2 = 0;
                for (int i = 0; i < len + rel && start + i < buf.Length; i++)
                {
                    float t = (float)i / Rate;
                    float env = Math.Min(1f, t / Math.Max(0.001f, attack)) * (float)Math.Exp(-t * decay);
                    if (i > len) env *= 1f - (float)(i - len) / rel;
                    ph += freq / Rate;
                    ph2 += freq * 1.006f / Rate;
                    float s = (Osc(w, ph) + Osc(w, ph2)) * 0.5f;
                    lp += cutoff * (s - lp);
                    buf[start + i] += lp * env * vol;
                }
            }

            public void Kick(float beat, float vol)
            {
                int start = (int)(beat * spb * Rate);
                double ph = 0;
                for (int i = 0; i < Rate / 4 && start + i < buf.Length; i++)
                {
                    float t = (float)i / Rate;
                    ph += (50f + 120f * (float)Math.Exp(-t * 30)) / Rate;
                    buf[start + i] += (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 14) * vol;
                }
            }

            public void Noise(float beat, float len, float vol, float decay, float hp, Random r)
            {
                int start = (int)(beat * spb * Rate);
                float last = 0, lp = 0;
                for (int i = 0; i < (int)(len * Rate) && start + i < buf.Length; i++)
                {
                    float t = (float)i / Rate;
                    float n = (float)(r.NextDouble() * 2 - 1);
                    lp += 0.5f * (n - lp);
                    float s = hp > 0 ? n - last : lp;
                    last = n;
                    buf[start + i] += s * (float)Math.Exp(-t * decay) * vol;
                }
            }

            public float[] Finish()
            {
                // soft limiter + tiny fade at the loop point
                for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * 1.2f);
                Normalize(buf, 0.8f);
                int fade = Rate / 50;
                for (int i = 0; i < fade; i++) { buf[i] *= (float)i / fade; buf[buf.Length - 1 - i] *= (float)i / fade; }
                return buf;
            }
        }

        static float[] HubMusic()
        {
            var r = new Random(7);
            var s = new Song(96, 16);
            int[][] chords = { new[] { 57, 60, 64, 67 }, new[] { 53, 57, 60, 64 }, new[] { 48, 52, 55, 59 }, new[] { 55, 59, 62, 65 } };
            int[] scale = { 69, 72, 74, 76, 79, 81, 84 };
            for (int bar = 0; bar < 16; bar++)
            {
                var ch = chords[bar % 4];
                float b0 = bar * 4;
                foreach (int n in ch) s.Note(b0, 4, Midi(n), 0.09f, Wave.Saw, 0.4f, 0.3f, 0.05f);
                s.Note(b0, 1.5f, Midi(ch[0] - 12), 0.35f, Wave.Triangle, 0.01f, 1.2f);
                s.Note(b0 + 2, 1.5f, Midi(ch[0] - 12), 0.3f, Wave.Triangle, 0.01f, 1.2f);
                s.Note(b0 + 3.5f, 0.5f, Midi(ch[2] - 12), 0.25f, Wave.Triangle, 0.01f, 2f);
                for (int k = 0; k < 8; k++)
                    s.Note(b0 + k * 0.5f, 0.4f, Midi(ch[k % 4] + 12), 0.08f, Wave.Square, 0.005f, 7f, 0.2f);
                if (bar >= 4)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        s.Kick(b0 + k, k % 2 == 0 ? 0.55f : 0.0f);
                        s.Noise(b0 + k + 0.5f, 0.05f, 0.08f, 60, 1, r);
                    }
                    s.Noise(b0 + 1, 0.15f, 0.12f, 25, 0, r);
                    s.Noise(b0 + 3, 0.15f, 0.12f, 25, 0, r);
                }
                if (bar >= 8 && bar % 2 == 0)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        if (r.NextDouble() < 0.3) continue;
                        s.Note(b0 + k * 0.75f, 0.6f, Midi(scale[r.Next(scale.Length)]), 0.12f, Wave.Sine, 0.01f, 3f);
                    }
                }
            }
            return s.Finish();
        }

        static float[] MatchMusic()
        {
            var r = new Random(13);
            var s = new Song(132, 16);
            int[][] chords = { new[] { 52, 55, 59 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 }, new[] { 50, 54, 57 } };
            int[] scale = { 64, 67, 69, 71, 74, 76, 79 };
            int[] riff = { 0, 0, 12, 0, 0, 10, 0, 7 };
            for (int bar = 0; bar < 16; bar++)
            {
                var ch = chords[bar % 4];
                float b0 = bar * 4;
                for (int k = 0; k < 8; k++)
                    s.Note(b0 + k * 0.5f, 0.45f, Midi(ch[0] - 24 + riff[k]), 0.3f, Wave.Saw, 0.005f, 4f, 0.12f);
                foreach (int n in ch) s.Note(b0, 4, Midi(n), 0.06f, Wave.Square, 0.05f, 0.6f, 0.06f);
                for (int k = 0; k < 4; k++)
                {
                    s.Kick(b0 + k, 0.65f);
                    s.Noise(b0 + k + 0.5f, 0.04f, 0.07f, 80, 1, r);
                    s.Noise(b0 + k + 0.25f, 0.03f, 0.04f, 90, 1, r);
                    s.Noise(b0 + k + 0.75f, 0.03f, 0.04f, 90, 1, r);
                }
                s.Noise(b0 + 1, 0.18f, 0.2f, 20, 0, r);
                s.Noise(b0 + 3, 0.18f, 0.2f, 20, 0, r);
                if (bar >= 4)
                {
                    int phrase = bar % 4;
                    for (int k = 0; k < 8; k++)
                    {
                        if ((k + phrase) % 3 == 2) continue;
                        int note = scale[(k * 2 + phrase * 3 + r.Next(2)) % scale.Length];
                        s.Note(b0 + k * 0.5f, 0.4f, Midi(note), 0.1f, Wave.Square, 0.005f, 5f, 0.3f);
                    }
                }
            }
            return s.Finish();
        }
    }
}
