using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// One-shot sounds made at start-up from synthesis: carbon and metal impacts, the explosion, start-light beeps,
    /// UI clicks. Played through a small pool of 3D sources.
    /// </summary>
    public static class CrashAudio
    {
        static AudioClip[] impacts;
        static AudioClip explosion, beep, beepGo, click, tick, chime;
        static AudioSource[] pool;
        static int next;
        static GameObject root;

        static void Ensure()
        {
            if (root != null) return;
            root = new GameObject("OneShotAudio");
            Object.DontDestroyOnLoad(root);
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("src" + i);
                go.transform.SetParent(root.transform);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.minDistance = 3f;
                s.maxDistance = 250f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                pool[i] = s;
            }
            int sr = AudioSettings.outputSampleRate;
            impacts = new AudioClip[4];
            for (int v = 0; v < impacts.Length; v++) impacts[v] = MakeImpact(sr, v);
            explosion = MakeExplosion(sr);
            beep = MakeTone(sr, 880f, 0.18f);
            beepGo = MakeTone(sr, 1320f, 0.45f);
            click = MakeClick(sr, 2400f, 0.03f);
            tick = MakeClick(sr, 1600f, 0.015f);
            chime = MakeChime(sr);
        }

        static AudioClip MakeImpact(int sr, int variant)
        {
            int n = (int)(sr * 1.1f);
            var d = new float[n];
            var r = new System.Random(100 + variant);
            float lp = 0f;
            float[] modes = { 180f + variant * 40f, 410f + variant * 70f, 930f + variant * 60f, 2300f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * (0.25f - t * 0.15f);
                float env = Mathf.Exp(-t * 7f);
                float crack = Mathf.Exp(-t * 45f) * nz;
                float ring = 0f;
                for (int m = 0; m < modes.Length; m++) ring += Mathf.Sin(2f * Mathf.PI * modes[m] * t) * Mathf.Exp(-t * (6f + m * 6f)) * (0.35f / (m + 1));
                // Debris rattle a little later.
                float rattle = t > 0.12f && r.NextDouble() < 0.004 * Mathf.Exp(-t * 3f) * 40 ? (float)(r.NextDouble() * 2 - 1) * 0.6f : 0f;
                d[i] = Mathf.Clamp(lp * env * 0.9f + crack * 0.8f + ring * env + rattle, -1f, 1f) * 0.9f;
            }
            var c = AudioClip.Create("impact" + variant, n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeExplosion(int sr)
        {
            int n = sr * 4;
            var d = new float[n];
            var r = new System.Random(7);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * 0.06f;
                lp2 += (lp - lp2) * 0.08f;
                float env = t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 1.1f);
                float sub = Mathf.Sin(2f * Mathf.PI * (55f - t * 10f) * t) * Mathf.Exp(-t * 2.2f);
                float crackle = r.NextDouble() < 0.002 * Mathf.Exp(-t) ? (float)(r.NextDouble() * 2 - 1) : 0f;
                d[i] = Mathf.Clamp((lp2 * 5f + sub * 0.8f) * env + crackle * 0.5f, -1f, 1f);
            }
            var c = AudioClip.Create("explosion", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeTone(int sr, float f, float len)
        {
            int n = (int)(sr * len);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Min(1f, (len - t) * 60f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.15f) * env * 0.5f;
            }
            var c = AudioClip.Create("tone", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeClick(int sr, float f, float len)
        {
            int n = (int)(sr * len);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 300f) * 0.6f;
            }
            var c = AudioClip.Create("click", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeChime(int sr)
        {
            int n = (int)(sr * 0.9f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float a = Mathf.Sin(2f * Mathf.PI * 784f * t) * Mathf.Exp(-t * 5f);
                float b = t > 0.12f ? Mathf.Sin(2f * Mathf.PI * 1175f * (t - 0.12f)) * Mathf.Exp(-(t - 0.12f) * 4f) : 0f;
                d[i] = (a + b) * 0.35f;
            }
            var c = AudioClip.Create("chime", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioSource Next(Vector3 p, bool spatial)
        {
            Ensure();
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.transform.position = p;
            s.spatialBlend = spatial ? 1f : 0f;
            return s;
        }

        public static void Impact(Vector3 p, float strength, bool carToCar)
        {
            var s = Next(p, true);
            s.pitch = Random.Range(0.85f, 1.15f) * (carToCar ? 1.1f : 1f);
            s.PlayOneShot(impacts[Random.Range(0, impacts.Length)], Mathf.Clamp01(0.25f + strength) * Settings.MasterVolume * Settings.EffectsVolume);
        }

        public static void Explosion(Vector3 p)
        {
            var s = Next(p, true);
            s.pitch = 1f;
            s.maxDistance = 600f;
            s.PlayOneShot(explosion, Settings.MasterVolume * Settings.EffectsVolume);
        }

        public static void Beep(bool go)
        {
            var s = Next(Vector3.zero, false);
            s.pitch = 1f;
            s.PlayOneShot(go ? beepGo : beep, 0.6f * Settings.MasterVolume * Settings.EffectsVolume);
        }

        public static void Click() { var s = Next(Vector3.zero, false); s.pitch = 1f; s.PlayOneShot(click, 0.5f * Settings.MasterVolume); }
        public static void Tick() { var s = Next(Vector3.zero, false); s.pitch = 1f; s.PlayOneShot(tick, 0.35f * Settings.MasterVolume); }
        public static void Chime() { var s = Next(Vector3.zero, false); s.pitch = 1f; s.PlayOneShot(chime, 0.6f * Settings.MasterVolume); }
    }
}
