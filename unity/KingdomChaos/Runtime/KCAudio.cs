using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomChaos
{
    // Every sound effect is synthesised into an AudioClip at startup (tone sweeps and filtered
    // noise, the same recipes the browser version played through Web Audio), so the game needs
    // no audio files.
    public class KCAudio
    {
        const int SR = 22050;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly AudioSource src;
        public bool Muted;

        public KCAudio(AudioSource source)
        {
            src = source;
            Make("swing", .1f, b => Noise(b, .09f, .12f, 3500, 'h'));
            Make("hit", .1f, b => { Tone(b, 220, 70, .09f, 'q', .1f); Noise(b, .06f, .15f, 1800, 'l'); });
            Make("heavy", .2f, b => { Tone(b, 140, 40, .18f, 'q', .14f); Noise(b, .14f, .25f, 900, 'l'); });
            Make("block", .1f, b => Tone(b, 1500, 900, .08f, 't', .12f));
            Make("brk", .26f, b => { Noise(b, .25f, .3f, 2500, 'l'); Tone(b, 600, 120, .2f, 'w', .1f); });
            Make("jump", .11f, b => Tone(b, 280, 560, .1f, 's', .1f));
            Make("roll", .16f, b => Noise(b, .15f, .1f, 800, 'l'));
            Make("hurt", .21f, b => Tone(b, 260, 90, .2f, 'w', .13f));
            Make("coin", .08f, b => Tone(b, 1050, 1500, .07f, 's', .07f));
            Make("boom", .5f, b => { Noise(b, .5f, .4f, 500, 'l'); Tone(b, 90, 30, .4f, 's', .3f); });
            Make("shoot", .08f, b => Tone(b, 900, 500, .07f, 't', .08f));
            Make("pet", .11f, b => Tone(b, 500, 900, .1f, 's', .08f));
            Make("level", .42f, b => { float[] f = { 523, 659, 784, 1046 }; for (int i = 0; i < 4; i++) Tone(b, f[i], f[i], .15f, 'q', .07f, i * .08f); });
            Make("die", .26f, b => { Noise(b, .2f, .2f, 1200, 'l'); Tone(b, 300, 60, .25f, 'q', .09f); });
            Make("eat", .16f, b => Tone(b, 400, 800, .15f, 's', .1f));
            Make("chest", .32f, b => { float[] f = { 784, 988, 1175, 1568 }; for (int i = 0; i < 4; i++) Tone(b, f[i], f[i], .12f, 't', .08f, i * .06f); });
            Make("spin", .5f, b => Noise(b, .5f, .15f, 2500, 'b'));
        }

        public void Play(string name)
        {
            if (Muted || src == null) return;
            if (clips.TryGetValue(name, out var c)) src.PlayOneShot(c);
        }

        void Make(string name, float len, Action<float[]> build)
        {
            var buf = new float[Mathf.CeilToInt(len * SR) + 64];
            build(buf);
            for (int i = 0; i < buf.Length; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
            var clip = AudioClip.Create("kc_" + name, buf.Length, 1, SR, false);
            clip.SetData(buf, 0);
            clips[name] = clip;
        }

        // type: q = square, s = sine, t = triangle, w = sawtooth
        static void Tone(float[] b, float f1, float f2, float dur, char type, float vol, float delay = 0)
        {
            int start = (int)(delay * SR), n = (int)(dur * SR);
            double ph = 0;
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float t = i / (float)n;
                float f = f1 * Mathf.Pow(f2 / f1, t);
                ph += f / SR;
                float fr = (float)(ph - Math.Floor(ph));
                float s;
                switch (type)
                {
                    case 'q': s = fr < .5f ? 1 : -1; break;
                    case 't': s = 4 * Mathf.Abs(fr - .5f) - 1; break;
                    case 'w': s = 2 * fr - 1; break;
                    default: s = Mathf.Sin(fr * Mathf.PI * 2); break;
                }
                float g = vol * Mathf.Pow(.001f / vol, t);
                b[start + i] += s * g;
            }
        }

        // type: l = lowpass, h = highpass, b = bandpass (one-pole filters)
        static void Noise(float[] b, float dur, float vol, float freq, char type)
        {
            int n = (int)(dur * SR);
            float a = 1 - Mathf.Exp(-2 * Mathf.PI * freq / SR);
            float lp = 0, lp2 = 0;
            var rng = new System.Random(n + (int)freq);
            for (int i = 0; i < n && i < b.Length; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1);
                lp += a * (x - lp);
                float y;
                if (type == 'l') y = lp;
                else if (type == 'h') y = x - lp;
                else { float hp = x - lp; lp2 += a * (hp - lp2); y = lp2 * 2; }
                float t = i / (float)n;
                b[i] += y * vol * Mathf.Pow(.001f / vol, t);
            }
        }
    }
}
