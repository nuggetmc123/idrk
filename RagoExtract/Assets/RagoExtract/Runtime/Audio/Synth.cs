using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Procedural sound design. Every sound in the game is synthesized here at load time:
    /// layered gunshots (supersonic crack + body thump + mechanism + outdoor slap-back tail),
    /// reload mechanics, footsteps per surface, bullet impacts, blood hits, UI, alarms and ambience loops.
    /// </summary>
    public static class Synth
    {
        public const int SR = 44100;
        static readonly Dictionary<string, AudioClip[]> _cache = new Dictionary<string, AudioClip[]>();
        static System.Random _r = new System.Random(1234);

        static float N() => (float)(_r.NextDouble() * 2 - 1);

        // ------------------------------------------------------------------ DSP helpers
        struct LP { float y; public float a; public LP(float hz) { a = 1 - Mathf.Exp(-2 * Mathf.PI * hz / SR); y = 0; } public float P(float x) { y += a * (x - y); return y; } }
        struct HP { LP lp; public HP(float hz) { lp = new LP(hz); } public float P(float x) => x - lp.P(x); }
        struct BP { LP a; HP b; public BP(float lo, float hi) { b = new HP(lo); a = new LP(hi); } public float P(float x) => a.P(b.P(x)); }

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * SR))];

        static AudioClip Clip(string name, float[] data, bool normalize = true, float peak = 0.95f, bool loop = false)
        {
            if (normalize)
            {
                float m = 0.0001f;
                for (int i = 0; i < data.Length; i++) m = Mathf.Max(m, Mathf.Abs(data[i]));
                float g = peak / m;
                for (int i = 0; i < data.Length; i++) data[i] *= g;
            }
            // tiny fades to avoid clicks
            int f = Mathf.Min(64, data.Length / 4);
            if (!loop)
                for (int i = 0; i < f; i++) { float k = i / (float)f; data[data.Length - 1 - i] *= k; }
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static void AddEcho(float[] d, float delay, float gain, float lpHz)
        {
            int off = (int)(delay * SR);
            var lp = new LP(lpHz);
            for (int i = d.Length - 1; i >= off; i--) { }
            // process forward on a copy so echoes don't feed back
            var src = (float[])d.Clone();
            for (int i = off; i < d.Length; i++) d[i] += lp.P(src[i - off]) * gain;
        }

        static float SoftClip(float x) => (float)Math.Tanh(x);

        public static AudioClip[] Get(string key, Func<int, AudioClip> make, int variants = 3)
        {
            if (_cache.TryGetValue(key, out var arr)) return arr;
            arr = new AudioClip[variants];
            for (int i = 0; i < variants; i++) { _r = new System.Random(key.GetHashCode() + i * 7919); arr[i] = make(i); }
            _cache[key] = arr;
            return arr;
        }

        public static AudioClip One(string key, Func<AudioClip> make) => Get(key, _ => make(), 1)[0];

        // ------------------------------------------------------------------ gunshots
        public struct ShotProfile
        {
            public float crack, bodyHz, bodyDecay, tail, bright, power;
            public bool suppressed, shotgun;
        }

        public static ShotProfile ProfileFor(GunDef g)
        {
            var p = new ShotProfile { crack = 1, bodyHz = 90, bodyDecay = 0.09f, tail = 1.1f, bright = 1, power = 1 };
            switch (g.cal)
            {
                case Caliber.C9mm: case Caliber.C57: p.crack = 0.7f; p.bodyHz = 140; p.bodyDecay = 0.06f; p.tail = 0.7f; p.bright = 1.1f; break;
                case Caliber.C45: p.crack = 0.55f; p.bodyHz = 110; p.bodyDecay = 0.08f; p.tail = 0.75f; p.bright = 0.85f; break;
                case Caliber.C357: case Caliber.C50AE: p.crack = 0.9f; p.bodyHz = 85; p.bodyDecay = 0.11f; p.tail = 1.1f; p.power = 1.3f; break;
                case Caliber.C556: case Caliber.C545: p.crack = 1f; p.bodyHz = 100; p.bodyDecay = 0.07f; p.tail = 1.2f; p.bright = 1.2f; break;
                case Caliber.C762: p.crack = 1f; p.bodyHz = 80; p.bodyDecay = 0.1f; p.tail = 1.35f; p.bright = 0.95f; p.power = 1.2f; break;
                case Caliber.C308: p.crack = 1.1f; p.bodyHz = 70; p.bodyDecay = 0.12f; p.tail = 1.6f; p.power = 1.35f; break;
                case Caliber.C338: case Caliber.C50BMG: p.crack = 1.25f; p.bodyHz = 50; p.bodyDecay = 0.17f; p.tail = 2.1f; p.power = 1.6f; p.bright = 0.8f; break;
                case Caliber.C12g: p.crack = 0.65f; p.bodyHz = 60; p.bodyDecay = 0.16f; p.tail = 1.4f; p.bright = 0.75f; p.power = 1.5f; p.shotgun = true; break;
            }
            p.suppressed = g.suppressed;
            return p;
        }

        public static AudioClip[] Gunshot(GunDef g) => Get("shot_" + g.id + (g.suppressed ? "_s" : ""), v => MakeShot(ProfileFor(g), v, false), 3);
        public static AudioClip[] GunshotDistant(GunDef g) => Get("shotfar_" + g.cal + g.suppressed, v => MakeShot(ProfileFor(g), v, true), 2);

        static AudioClip MakeShot(ShotProfile p, int variant, bool distant)
        {
            float len = (p.suppressed ? 0.45f : p.tail + 0.45f) * (distant ? 1.2f : 1f);
            var d = Buf(len);
            var hpCrack = new HP(1800f * p.bright);
            var lpBody = new LP(p.suppressed ? 420f : 900f);
            var lpTail = new LP(distant ? 320f : 700f * p.bright);
            var lpTail2 = new LP(distant ? 200f : 380f);
            float ph = 0;
            float vary = 1 + variant * 0.06f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float s = 0;
                if (!p.suppressed)
                {
                    // supersonic crack / muzzle blast
                    float crack = hpCrack.P(N()) * Mathf.Exp(-t / (0.0045f * vary)) * p.crack * 1.6f;
                    s += distant ? crack * 0.12f : crack;
                }
                // body thump with downward pitch sweep
                float f = p.bodyHz * vary * (1 + 2.2f * Mathf.Exp(-t / 0.012f));
                ph += f / SR * Mathf.PI * 2;
                float body = Mathf.Sin(ph) * Mathf.Exp(-t / p.bodyDecay) * 1.1f * p.power;
                body += lpBody.P(N()) * Mathf.Exp(-t / (p.bodyDecay * 0.7f)) * 1.8f;
                s += body * (p.suppressed ? 0.8f : 1f);
                // tail: rolling low rumble (outdoor reflections)
                if (!p.suppressed)
                {
                    float env = Mathf.Exp(-t / (p.tail * 0.32f)) * Mathf.Clamp01(t / 0.01f);
                    float rumble = lpTail2.P(lpTail.P(N())) * env * 2.4f;
                    s += rumble * (distant ? 1.6f : 1f);
                }
                if (p.shotgun && !distant) s += N() * Mathf.Exp(-t / 0.02f) * 0.4f;
                d[i] = s;
            }
            if (p.suppressed)
            {
                // mechanism clack (action cycling is the loudest part of a suppressed shot)
                MetalClick(d, 0.012f, 0.55f, 2600f, 0.012f);
                MetalClick(d, 0.045f, 0.4f, 1900f, 0.02f);
            }
            else
            {
                AddEcho(d, 0.09f + variant * 0.013f, 0.28f, 1200f);
                AddEcho(d, 0.21f + variant * 0.02f, 0.18f, 800f);
                AddEcho(d, 0.38f, 0.1f, 500f);
            }
            for (int i = 0; i < d.Length; i++) d[i] = SoftClip(d[i] * 1.4f);
            return Clip("shot", d, true, distant ? 0.6f : 0.97f);
        }

        static void MetalClick(float[] d, float at, float gain, float hz, float decay)
        {
            int s = (int)(at * SR);
            var bp = new BP(hz * 0.6f, hz * 1.6f);
            float ph1 = 0, ph2 = 0;
            for (int i = s; i < d.Length && i < s + (int)(decay * 8 * SR); i++)
            {
                float t = (i - s) / (float)SR;
                ph1 += hz / SR * 6.283f; ph2 += hz * 2.37f / SR * 6.283f;
                float e = Mathf.Exp(-t / decay);
                d[i] += (bp.P(N()) * 1.2f + Mathf.Sin(ph1) * 0.4f + Mathf.Sin(ph2) * 0.25f) * e * gain;
            }
        }

        // ------------------------------------------------------------------ mechanics
        public static AudioClip MagOut() => Get("magout", v =>
        {
            var d = Buf(0.35f);
            MetalClick(d, 0.0f, 0.9f, 2200 + v * 200, 0.01f);
            Friction(d, 0.02f, 0.18f, 0.35f, 1500, 4000);
            return Clip("magout", d);
        }, 2)[0];

        public static AudioClip MagIn() => Get("magin", v =>
        {
            var d = Buf(0.3f);
            Friction(d, 0.0f, 0.06f, 0.3f, 1200, 3500);
            MetalClick(d, 0.065f, 1f, 1700 + v * 150, 0.014f);
            MetalClick(d, 0.08f, 0.5f, 3200, 0.008f);
            Thump(d, 0.065f, 0.5f, 120, 0.03f);
            return Clip("magin", d);
        }, 2)[0];

        public static AudioClip SlideBack() => One("slideback", () =>
        {
            var d = Buf(0.25f);
            Friction(d, 0, 0.1f, 0.5f, 2000, 6000);
            MetalClick(d, 0.1f, 0.7f, 2800, 0.01f);
            return Clip("slideback", d);
        });

        public static AudioClip SlideForward() => One("slidefwd", () =>
        {
            var d = Buf(0.3f);
            Friction(d, 0, 0.04f, 0.4f, 2000, 6000);
            MetalClick(d, 0.045f, 1f, 1500, 0.02f);
            MetalClick(d, 0.05f, 0.6f, 3400, 0.01f);
            Thump(d, 0.045f, 0.6f, 160, 0.03f);
            return Clip("slidefwd", d);
        });

        public static AudioClip Pump() => One("pump", () =>
        {
            var d = Buf(0.45f);
            Friction(d, 0, 0.09f, 0.6f, 900, 3000);
            MetalClick(d, 0.1f, 1f, 1200, 0.02f);
            Friction(d, 0.2f, 0.08f, 0.5f, 900, 3000);
            MetalClick(d, 0.29f, 1f, 1500, 0.025f);
            Thump(d, 0.29f, 0.7f, 110, 0.04f);
            return Clip("pump", d);
        });

        public static AudioClip BoltCycle() => One("bolt", () =>
        {
            var d = Buf(0.6f);
            MetalClick(d, 0.0f, 0.8f, 2100, 0.015f);
            Friction(d, 0.04f, 0.12f, 0.5f, 1500, 5000);
            MetalClick(d, 0.17f, 0.7f, 2600, 0.012f);
            Friction(d, 0.3f, 0.1f, 0.5f, 1500, 5000);
            MetalClick(d, 0.41f, 1f, 1600, 0.02f);
            MetalClick(d, 0.48f, 0.6f, 2400, 0.012f);
            return Clip("bolt", d);
        });

        public static AudioClip DryFire() => One("dry", () => { var d = Buf(0.12f); MetalClick(d, 0, 1f, 3800, 0.006f); MetalClick(d, 0.01f, 0.5f, 2100, 0.008f); return Clip("dry", d, true, 0.6f); });

        public static AudioClip Casing(int v) => Get("casing", k =>
        {
            var d = Buf(0.5f);
            float[] partials = { 3100 + k * 140, 5200 + k * 230, 7900 + k * 300, 11200 };
            for (int b = 0; b < 3; b++)
            {
                float at = b == 0 ? 0 : 0.09f * b + k * 0.02f;
                float g = Mathf.Pow(0.45f, b);
                int s = (int)(at * SR);
                for (int i = s; i < d.Length; i++)
                {
                    float t = (i - s) / (float)SR;
                    float x = 0;
                    for (int p = 0; p < partials.Length; p++) x += Mathf.Sin(t * partials[p] * 6.283f) * Mathf.Exp(-t / (0.05f / (p + 1) + 0.01f)) / (p + 1);
                    d[i] += x * g;
                }
            }
            return Clip("casing", d, true, 0.5f);
        }, 4)[v % 4];

        static void Friction(float[] d, float at, float dur, float gain, float lo, float hi)
        {
            int s = (int)(at * SR), e = Mathf.Min(d.Length, s + (int)(dur * SR));
            var bp = new BP(lo, hi);
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)(e - s);
                float env = Mathf.Sin(t * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 90));
                d[i] += bp.P(N()) * env * gain;
            }
        }

        static void Thump(float[] d, float at, float gain, float hz, float decay)
        {
            int s = (int)(at * SR);
            float ph = 0;
            for (int i = s; i < d.Length; i++)
            {
                float t = (i - s) / (float)SR;
                ph += hz * (1 + Mathf.Exp(-t / 0.01f)) / SR * 6.283f;
                d[i] += Mathf.Sin(ph) * Mathf.Exp(-t / decay) * gain;
            }
        }

        // ------------------------------------------------------------------ world
        public enum Surface { Concrete, Dirt, Grass, Metal, Wood, Flesh, Water }

        /// <summary>
        /// A real footstep: heel strike, then the toe rolling down ~70 ms later (shorter and harder when running), with the
        /// surface's own texture (grit, crunch, swish, hollow boards, clang, splash) and a little clothing/gear rustle.
        /// 8 variants per surface, separate sets for walking and running.
        /// </summary>
        public static AudioClip Footstep(Surface s, int v, bool run = false) => Get("step2_" + s + (run ? "_run" : ""), k =>
        {
            var rnd = new System.Random(k * 977 + (int)s * 131 + (run ? 7 : 0));
            float R() => (float)rnd.NextDouble();
            float len = s == Surface.Water ? 0.5f : s == Surface.Metal ? 0.45f : 0.36f;
            var d = Buf(len);
            float heel = run ? 1.25f : 1f, toeAt = (run ? 0.04f : 0.065f) + R() * 0.025f, toe = run ? 0.75f : 0.55f;
            void Grit(float at, float dur, float gain, float lo, float hi, float density)
            {
                // sparse little impulses through a band-pass: gravel, crunch, twigs
                var bp = new BP(lo, hi);
                int st = (int)(at * SR), e = Mathf.Min(d.Length, st + (int)(dur * SR));
                for (int i = st; i < e; i++)
                {
                    float t = (i - st) / (float)(e - st);
                    float imp = rnd.NextDouble() < density ? (float)(rnd.NextDouble() * 2 - 1) * 3f : 0f;
                    d[i] += bp.P(imp + N() * 0.15f) * gain * (1 - t) * (1 - t);
                }
            }
            void Click(float at, float gain, float hz, float decay)
            {
                var lp = new LP(hz);
                int st = (int)(at * SR);
                for (int i = st; i < d.Length; i++) { float t = (i - st) / (float)SR; d[i] += lp.P(N()) * Mathf.Exp(-t / decay) * gain; }
            }
            switch (s)
            {
                case Surface.Metal:
                    {
                        Thump(d, 0, 0.7f * heel, 120, 0.035f);
                        float[] modes = { 410 + R() * 60, 1120 + R() * 140, 2230 + R() * 200, 3480 + R() * 300 };
                        for (int m = 0; m < modes.Length; m++)
                        {
                            float ph = R() * 6.28f, g = (0.22f / (m + 1)) * heel, dec = 0.22f / (1 + m * 0.6f);
                            for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; ph += modes[m] / SR * 6.283f; d[i] += Mathf.Sin(ph) * Mathf.Exp(-t / dec) * g; }
                        }
                        Click(0, 0.5f * heel, 5000, 0.008f);
                        Thump(d, toeAt, 0.35f * toe, 150, 0.025f);
                        Click(toeAt, 0.3f * toe, 4000, 0.006f);
                        Grit(0.005f, 0.08f, 0.15f, 2500, 8000, 0.02f);
                        break;
                    }
                case Surface.Grass:
                    {
                        // a soft thud under a swish of blades, and the odd twig
                        Thump(d, 0, 0.45f * heel, 85, 0.03f);
                        var bp = new BP(2200, 7500);
                        for (int i = 0; i < d.Length; i++)
                        {
                            float t = i / (float)SR;
                            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t / (run ? 0.07f : 0.1f));
                            d[i] += bp.P(N()) * env * 0.55f * heel;
                        }
                        Grit(0.01f, 0.12f, 0.35f, 1800, 6000, 0.012f);
                        Thump(d, toeAt, 0.25f * toe, 95, 0.025f);
                        break;
                    }
                case Surface.Dirt:
                    {
                        // crunchy gravel under the heel, then the toe
                        Thump(d, 0, 0.55f * heel, 90, 0.03f);
                        Grit(0, run ? 0.09f : 0.12f, 0.75f * heel, 900, 5500, 0.08f);
                        Click(0, 0.25f * heel, 2500, 0.012f);
                        Grit(toeAt, 0.08f, 0.5f * toe, 1200, 6000, 0.07f);
                        Thump(d, toeAt, 0.3f * toe, 100, 0.02f);
                        break;
                    }
                case Surface.Wood:
                    {
                        // hollow boards: a resonant knock, sometimes a creak
                        float body = 170 + R() * 70;
                        Thump(d, 0, 0.85f * heel, body, 0.07f);
                        Thump(d, 0, 0.35f * heel, body * 2.3f, 0.03f);
                        Click(0, 0.4f * heel, 3000, 0.01f);
                        Thump(d, toeAt, 0.45f * toe, body * 1.1f, 0.05f);
                        Click(toeAt, 0.25f * toe, 2500, 0.008f);
                        if (R() < 0.45f)
                        {
                            float f0 = 260 + R() * 200, ph = 0;
                            int st = (int)((toeAt + 0.02f) * SR), e = Mathf.Min(d.Length, st + (int)(0.16f * SR));
                            for (int i = st; i < e; i++) { float t = (i - st) / (float)(e - st); ph += (f0 + t * 140) / SR * 6.283f; d[i] += Mathf.Sin(ph) * Mathf.Sin(t * Mathf.PI) * 0.08f * (1 + 0.5f * Mathf.Sin(t * 80)); }
                        }
                        break;
                    }
                case Surface.Water:
                    {
                        // splash: a slap, spray, then a few bubbles
                        var bp = new BP(400, 3500);
                        for (int i = 0; i < d.Length; i++)
                        {
                            float t = i / (float)SR;
                            d[i] += bp.P(N()) * (Mathf.Exp(-t / 0.05f) * 1.1f + Mathf.Exp(-Mathf.Abs(t - 0.09f) / 0.04f) * 0.5f) * heel;
                        }
                        for (int b2 = 0; b2 < 4; b2++)
                        {
                            float at = 0.05f + R() * 0.25f, f0 = 500 + R() * 900, ph = 0;
                            int st = (int)(at * SR), e = Mathf.Min(d.Length, st + (int)(0.05f * SR));
                            for (int i = st; i < e; i++) { float t = (i - st) / (float)(e - st); ph += (f0 * (1 + t * 0.8f)) / SR * 6.283f; d[i] += Mathf.Sin(ph) * (1 - t) * 0.12f; }
                        }
                        break;
                    }
                default:
                    {
                        // concrete / asphalt / tile: rubber sole slap, a little grit, then the toe
                        Thump(d, 0, 0.6f * heel, 105, 0.025f);
                        Click(0, 0.75f * heel, 3200 + R() * 800, 0.014f);
                        Grit(0.004f, 0.05f, 0.3f, 3000, 9000, 0.03f);
                        Thump(d, toeAt, 0.3f * toe, 120, 0.02f);
                        Click(toeAt, 0.4f * toe, 4200, 0.009f);
                        if (run || R() < 0.4f) Grit(toeAt + 0.01f, 0.06f, 0.18f, 2500, 7000, 0.04f);   // scuff
                        break;
                    }
            }
            // clothing / gear: a faint rustle and, now and then, a tiny jingle
            Friction(d, 0.0f, run ? 0.12f : 0.16f, run ? 0.07f : 0.04f, 900, 4000);
            if (R() < (run ? 0.6f : 0.3f)) MetalClick(d, 0.02f + R() * 0.06f, run ? 0.08f : 0.05f, 3500 + R() * 2500, 0.02f);
            return Clip("step", d, true, run ? 0.75f : 0.6f);
        }, 8)[v % 8];

        /// <summary>Landing from a jump or a drop: both feet, heavier, with a gear rattle.</summary>
        public static AudioClip Land(Surface s) => One("land_" + s, () =>
        {
            var a = Footstep(s, 1, true); var b = Footstep(s, 4, true);
            var d = Buf(0.5f);
            var da = new float[a.samples]; a.GetData(da, 0);
            var db = new float[b.samples]; b.GetData(db, 0);
            int off = (int)(0.035f * SR);
            for (int i = 0; i < d.Length; i++) { float x = 0; if (i < da.Length) x += da[i]; if (i - off >= 0 && i - off < db.Length) x += db[i - off] * 0.8f; d[i] = x; }
            Thump(d, 0, 0.5f, 70, 0.06f);
            MetalClick(d, 0.03f, 0.1f, 4200, 0.03f);
            return Clip("land", d, true, 0.8f);
        });

        public static AudioClip Impact(Surface s, int v) => Get("impact_" + s, k =>
        {
            var d = Buf(s == Surface.Metal ? 0.6f : 0.35f);
            switch (s)
            {
                case Surface.Metal:
                    MetalClick(d, 0, 1f, 1400 + k * 260, 0.06f);
                    MetalClick(d, 0, 0.6f, 4200 + k * 300, 0.03f);
                    // ricochet whine sometimes
                    if (k == 1)
                    {
                        float ph = 0;
                        for (int i = (int)(0.02f * SR); i < d.Length; i++)
                        {
                            float t = i / (float)SR;
                            ph += (2600 - t * 2200) / SR * 6.283f;
                            d[i] += Mathf.Sin(ph) * Mathf.Exp(-t / 0.12f) * 0.25f;
                        }
                    }
                    break;
                case Surface.Flesh:
                    {
                        var lp = new LP(380); var bp = new BP(400, 1400);
                        for (int i = 0; i < d.Length; i++)
                        {
                            float t = i / (float)SR;
                            d[i] = lp.P(N()) * Mathf.Exp(-t / 0.05f) * 2.2f + bp.P(N()) * Mathf.Exp(-t / 0.02f) * 0.6f;
                        }
                        Thump(d, 0, 1f, 75 + k * 8, 0.06f);
                        break;
                    }
                case Surface.Wood:
                    Thump(d, 0, 1f, 260, 0.03f);
                    Friction(d, 0, 0.05f, 0.6f, 1200, 5000);
                    break;
                case Surface.Water:
                    Friction(d, 0, 0.25f, 1f, 500, 3000);
                    Thump(d, 0, 0.5f, 90, 0.05f);
                    break;
                default:
                    {
                        var lp = new LP(4500);
                        for (int i = 0; i < d.Length; i++)
                        {
                            float t = i / (float)SR;
                            d[i] = lp.P(N()) * Mathf.Exp(-t / 0.018f);
                        }
                        // debris
                        for (int c = 0; c < 6; c++) MetalClick(d, 0.02f + c * 0.018f + (float)_r.NextDouble() * 0.02f, 0.15f, 2500 + c * 400, 0.004f);
                        Thump(d, 0, 0.5f, 120, 0.02f);
                        break;
                    }
            }
            return Clip("impact", d, true, 0.85f);
        }, 3)[v % 3];

        public static AudioClip BulletCrack(int v) => Get("whizz", k =>
        {
            var d = Buf(0.25f);
            var hp = new HP(2500);
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = hp.P(N()) * Mathf.Exp(-t / 0.004f) * 1.4f;
                ph += (3200 - t * 6000 + k * 200) / SR * 6.283f;
                d[i] += Mathf.Sin(ph) * Mathf.Exp(-Mathf.Abs(t - 0.02f) / 0.02f) * 0.35f;
            }
            return Clip("whizz", d, true, 0.8f);
        }, 3)[v % 3];

        /// <summary>Builds the everyday sounds up front (during the first loading screen) so they never hitch the game.</summary>
        public static void Prewarm()
        {
            foreach (Surface s in System.Enum.GetValues(typeof(Surface)))
            {
                if (s == Surface.Flesh) continue;
                Footstep(s, 0, false); Footstep(s, 0, true); Land(s);
                Impact(s, 0);
            }
            Impact(Surface.Flesh, 0);
            BulletCrack(0); Grab(0); Beep(); UiError(); HitMarker(); MagIn(); MagOut(); SlideBack(); SlideForward(); DryFire(); Zipper(); BodyFall();
        }

        // ------------------------------------------------------------------ grenades
        /// <summary>Frag grenade: a hard crack, a deep boom and a long rumbling tail.</summary>
        public static AudioClip Explosion() => One("explosion", () =>
        {
            var d = Buf(3.2f);
            var lp = new LP(180); var lp2 = new LP(900); var lp3 = new LP(60);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float crack = N() * Mathf.Exp(-t * 60f) * 1.2f;
                float boom = lp.P(N()) * Mathf.Exp(-t * 3.2f) * 5.5f;
                float body = lp2.P(N()) * Mathf.Exp(-t * 9f) * 1.6f;
                float rumble = lp3.P(N()) * Mathf.Exp(-t * 1.1f) * 4f * Mathf.Clamp01(t * 8f);
                float sub = Mathf.Sin(t * 2 * Mathf.PI * (48f - t * 10f)) * Mathf.Exp(-t * 4f) * 0.8f;
                d[i] = crack + boom + body + rumble + sub;
            }
            return Clip("explosion", d);
        });

        /// <summary>Flashbang: a very sharp, bright bang with a short ring.</summary>
        public static AudioClip FlashBang() => One("flashbang", () =>
        {
            var d = Buf(1.6f);
            var lp = new LP(2500); var lp2 = new LP(250);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = N() * Mathf.Exp(-t * 35f) * 1.5f + lp.P(N()) * Mathf.Exp(-t * 7f) * 1.2f + lp2.P(N()) * Mathf.Exp(-t * 2.5f) * 2.5f
                     + Mathf.Sin(t * 2 * Mathf.PI * 3100f) * Mathf.Exp(-t * 3f) * 0.12f;
            }
            return Clip("flashbang", d);
        });

        /// <summary>The high whine in your ears after a flashbang.</summary>
        public static AudioClip Ringing() => One("ringing", () =>
        {
            var d = Buf(4.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t * 6f) * Mathf.Exp(-t * 0.7f);
                d[i] = (Mathf.Sin(t * 2 * Mathf.PI * 3900f) + Mathf.Sin(t * 2 * Mathf.PI * 4170f) * 0.4f) * env * 0.5f;
            }
            return Clip("ringing", d, true, 0.6f);
        });

        /// <summary>Grenade pin pulled: a metal ping and the spoon click.</summary>
        public static AudioClip PinPull() => One("pinpull", () =>
        {
            var d = Buf(0.35f);
            MetalClick(d, 0, 0.9f, 3600, 0.02f);
            MetalClick(d, 0.06f, 0.5f, 5200, 0.05f);
            Friction(d, 0.0f, 0.06f, 0.2f, 2000, 6000);
            return Clip("pinpull", d);
        });

        public static AudioClip BodyFall() => One("bodyfall", () =>
        {
            var d = Buf(0.6f);
            Thump(d, 0, 1f, 70, 0.08f);
            Thump(d, 0.12f, 0.6f, 90, 0.06f);
            Friction(d, 0.02f, 0.3f, 0.4f, 300, 1800);
            MetalClick(d, 0.15f, 0.25f, 1500, 0.03f);
            return Clip("bodyfall", d);
        });

        public static AudioClip Grab(int v) => Get("grab", k =>
        {
            // a soft, short tap - not a burst of rustling noise every time you touch something
            var d = Buf(0.09f);
            Thump(d, 0.0f, 0.35f, 180 + k * 30, 0.018f);
            Friction(d, 0.004f, 0.03f, 0.08f, 600 + k * 200, 2200);
            return Clip("grab", d, true, 0.3f);
        }, 3)[v % 3];

        public static AudioClip Zipper() => One("zip", () =>
        {
            var d = Buf(0.5f);
            var bp = new BP(1800, 6000);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float teeth = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 260 * Mathf.PI)), 8);
                d[i] = bp.P(N()) * (0.3f + teeth) * Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
            }
            return Clip("zip", d, true, 0.5f);
        });

        public static AudioClip Heal() => One("heal", () =>
        {
            var d = Buf(0.6f);
            Friction(d, 0, 0.35f, 0.7f, 600, 5000);
            MetalClick(d, 0.4f, 0.6f, 2400, 0.02f);
            return Clip("heal", d, true, 0.5f);
        });

        public static AudioClip Inject() => One("inject", () =>
        {
            var d = Buf(0.5f);
            MetalClick(d, 0, 0.8f, 3000, 0.01f);
            Friction(d, 0.03f, 0.3f, 0.5f, 3000, 9000);
            return Clip("inject", d, true, 0.5f);
        });

        // ------------------------------------------------------------------ UI & events
        static void Tone(float[] d, float at, float hz, float dur, float gain, float harm = 0.3f)
        {
            int s = (int)(at * SR), e = Mathf.Min(d.Length, s + (int)(dur * SR));
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t / (dur * 0.4f));
                d[i] += (Mathf.Sin(t * hz * 6.283f) + Mathf.Sin(t * hz * 2 * 6.283f) * harm + Mathf.Sin(t * hz * 3 * 6.283f) * harm * 0.4f) * env * gain;
            }
        }

        public static AudioClip UiClick() => One("ui_click", () => { var d = Buf(0.06f); Tone(d, 0, 1800, 0.05f, 0.6f, 0.1f); MetalClick(d, 0, 0.3f, 4000, 0.003f); return Clip("click", d, true, 0.35f); });
        public static AudioClip UiHover() => One("ui_hover", () => { var d = Buf(0.04f); Tone(d, 0, 2600, 0.03f, 0.4f, 0f); return Clip("hover", d, true, 0.12f); });
        public static AudioClip UiError() => One("ui_err", () => { var d = Buf(0.3f); Tone(d, 0, 180, 0.12f, 1, 0.8f); Tone(d, 0.14f, 150, 0.15f, 1, 0.8f); return Clip("err", d, true, 0.4f); });
        public static AudioClip Cash() => One("cash", () =>
        {
            var d = Buf(0.9f);
            Tone(d, 0, 1318, 0.6f, 0.6f, 0.2f); Tone(d, 0.08f, 1975, 0.7f, 0.5f, 0.2f);
            for (int c = 0; c < 8; c++) MetalClick(d, 0.15f + c * 0.04f + (float)_r.NextDouble() * 0.02f, 0.3f, 4500 + c * 300, 0.02f);
            return Clip("cash", d, true, 0.5f);
        });
        public static AudioClip QueueFound() => One("qfound", () => { var d = Buf(1.1f); Tone(d, 0, 523, 0.5f, 0.6f); Tone(d, 0.15f, 659, 0.5f, 0.6f); Tone(d, 0.3f, 784, 0.8f, 0.7f); return Clip("q", d, true, 0.5f); });
        public static AudioClip Ding() => One("ding", () => { var d = Buf(1.4f); Tone(d, 0, 1318, 1.2f, 0.45f); Tone(d, 0, 2637, 0.5f, 0.12f); Tone(d, 0.28f, 1046, 1.1f, 0.45f); Tone(d, 0.28f, 2093, 0.4f, 0.1f); return Clip("ding", d, true, 0.45f); });
        public static AudioClip Beep() => One("beep", () => { var d = Buf(0.15f); Tone(d, 0, 1000, 0.12f, 1, 0.05f); return Clip("beep", d, true, 0.4f); });
        public static AudioClip HitMarker() => One("hitmark", () => { var d = Buf(0.08f); MetalClick(d, 0, 1f, 5200, 0.006f); Tone(d, 0, 3000, 0.04f, 0.3f, 0); return Clip("hit", d, true, 0.35f); });
        public static AudioClip Extracted() => One("extracted", () => { var d = Buf(2f); Tone(d, 0, 392, 1.6f, 0.5f); Tone(d, 0, 494, 1.6f, 0.4f); Tone(d, 0, 587, 1.6f, 0.4f); Tone(d, 0.3f, 784, 1.5f, 0.35f); return Clip("ext", d, true, 0.5f); });

        public static AudioClip Alarm() => One("alarm", () =>
        {
            var d = Buf(2f);
            float ph = 0; var lp = new LP(2500);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float f = 650 + 450 * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI));
                ph += f / SR * 6.283f;
                float saw = (ph / 6.283f) % 1f * 2 - 1;
                d[i] = lp.P(saw) * 0.8f;
            }
            return Clip("alarm", d, true, 0.7f, true);
        });

        public static AudioClip VaultDoor() => One("vaultdoor", () =>
        {
            var d = Buf(3.2f);
            var lp = new LP(160);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = lp.P(N()) * 3f * Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((2.8f - t) / 0.4f);
            }
            Friction(d, 0.0f, 0.6f, 0.4f, 3000, 9000); // hydraulic hiss
            MetalClick(d, 0.05f, 1f, 600, 0.12f);
            Thump(d, 2.75f, 1.2f, 55, 0.25f);
            MetalClick(d, 2.75f, 0.8f, 450, 0.2f);
            return Clip("vault", d, true, 0.9f);
        });

        public static AudioClip HackTick() => One("hack", () => { var d = Buf(0.1f); Tone(d, 0, 2200, 0.05f, 0.6f, 0.0f); Tone(d, 0.05f, 1650, 0.04f, 0.4f, 0.0f); return Clip("hack", d, true, 0.3f); });

        public static AudioClip Heartbeat() => One("heart", () =>
        {
            var d = Buf(1f);
            Thump(d, 0.0f, 1f, 55, 0.07f); Thump(d, 0.22f, 0.7f, 50, 0.08f);
            return Clip("heart", d, true, 0.8f, true);
        });

        // ------------------------------------------------------------------ ambience (seamless loops)
        public enum Amb { Wind, Forest, Harbor, Town, Bunker, Market }

        public static AudioClip Ambience(Amb a) => One("amb_" + a, () =>
        {
            float len = 12f;
            var d = Buf(len);
            var lp = new LP(500); var lp2 = new LP(140); var bp = new BP(300, 1200);
            float b0 = 0, b1 = 0, b2 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                // pink-ish noise
                float w = N();
                b0 = 0.997f * b0 + w * 0.029f; b1 = 0.985f * b1 + w * 0.032f; b2 = 0.95f * b2 + w * 0.048f;
                float pink = b0 + b1 + b2 + w * 0.02f;
                float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.52f * Mathf.PI * 2 / len * 6) * Mathf.Sin(t * 0.17f * Mathf.PI * 2 / len * 6 + 1);
                float s = 0;
                switch (a)
                {
                    case Amb.Wind: case Amb.Town: s = lp.P(pink) * gust * 2.2f; if (a == Amb.Town) s += Mathf.Sin(t * 2 * Mathf.PI * 55) * 0.03f + lp2.P(N()) * 0.3f; break;
                    case Amb.Forest: s = lp.P(pink) * gust * 1.4f; break;
                    case Amb.Harbor:
                        float wave = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2 / 4f), 3);
                        s = lp.P(pink) * (0.6f + gust * 0.5f) * 1.2f + bp.P(N()) * wave * 0.5f; break;
                    case Amb.Bunker:
                        s = Mathf.Sin(t * 2 * Mathf.PI * 60) * 0.12f + Mathf.Sin(t * 2 * Mathf.PI * 120) * 0.04f + lp.P(lp.P(N())) * 0.12f; break;   // hum, barely any hiss
                    case Amb.Market:
                        float chatter = 0.5f + 0.5f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 3.1f + 2);
                        s = bp.P(N()) * chatter * 0.3f + Mathf.Sin(t * 2 * Mathf.PI * 50) * 0.04f + lp.P(pink) * 0.35f; break;
                }
                d[i] = s;
            }
            if (a == Amb.Forest)
            {
                // sparse bird chirps
                for (int k = 0; k < 9; k++)
                {
                    float at = (float)_r.NextDouble() * (len - 1);
                    float baseHz = 2600 + (float)_r.NextDouble() * 2400;
                    for (int c = 0; c < 3 + _r.Next(4); c++)
                    {
                        int s0 = (int)((at + c * 0.11f) * SR);
                        float ph = 0;
                        for (int i = s0; i < s0 + (int)(0.07f * SR) && i < d.Length; i++)
                        {
                            float t = (i - s0) / (float)SR;
                            ph += (baseHz + Mathf.Sin(t * 60) * 600 + t * 9000) / SR * 6.283f;
                            d[i] += Mathf.Sin(ph) * Mathf.Sin(t / 0.07f * Mathf.PI) * 0.12f;
                        }
                    }
                }
            }
            // crossfade loop end into start
            int xf = (int)(1.5f * SR);
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                d[i] = d[i] * k + d[d.Length - xf + i] * (1 - k);
            }
            var trimmed = new float[d.Length - xf];
            Array.Copy(d, trimmed, trimmed.Length);
            return Clip("amb", trimmed, true, 0.5f, true);
        });
    }
}
