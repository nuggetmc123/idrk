using UnityEngine;

namespace Nullbound
{
    /// Tiny synthesizer: every sound in the game is generated at startup, no audio files needed.
    public static class NBAudio
    {
        public static AudioClip Punch, Heavy, Whoosh, Boom, Click, Ko, Zap, Block;
        static bool s_Ready;

        public static void Init()
        {
            if (s_Ready) return;
            s_Ready = true;
            Punch = Make("punch", 0.12f, (t, k) => Noise() * Env(k, 18f) * 0.8f + Mathf.Sin(t * 2 * Mathf.PI * 110f) * Env(k, 12f));
            Heavy = Make("heavy", 0.35f, (t, k) => Noise() * Env(k, 9f) * 0.7f + Mathf.Sin(t * 2 * Mathf.PI * Mathf.Lerp(90f, 40f, k)) * Env(k, 5f));
            Whoosh = Make("whoosh", 0.25f, (t, k) => Noise() * Mathf.Sin(k * Mathf.PI) * 0.45f);
            Boom = Make("boom", 0.8f, (t, k) => Noise() * Env(k, 4f) * 0.8f + Mathf.Sin(t * 2 * Mathf.PI * Mathf.Lerp(70f, 25f, k)) * Env(k, 3f));
            Click = Make("click", 0.05f, (t, k) => Mathf.Sin(t * 2 * Mathf.PI * 1200f) * Env(k, 20f) * 0.4f);
            Ko = Make("ko", 0.9f, (t, k) => (Mathf.Sin(t * 2 * Mathf.PI * 660f) + Mathf.Sin(t * 2 * Mathf.PI * 990f) * 0.5f) * Env(k, 3f) * 0.4f);
            Zap = Make("zap", 0.3f, (t, k) => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * Mathf.Lerp(900f, 200f, k))) * Env(k, 6f) * 0.25f);
            Block = Make("block", 0.15f, (t, k) => (Mathf.Sin(t * 2 * Mathf.PI * 420f) * 0.5f + Noise() * 0.3f) * Env(k, 14f));
        }

        static float Env(float k, float speed) => Mathf.Exp(-k * speed);
        static float Noise() => Random.value * 2f - 1f;

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate, i / (float)n), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(AudioClip clip, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var g = new GameObject("snd");
            g.transform.position = pos;
            var a = g.AddComponent<AudioSource>();
            a.clip = clip; a.volume = vol; a.pitch = pitch * Random.Range(0.92f, 1.08f);
            a.spatialBlend = 1f; a.minDistance = 2f; a.maxDistance = 60f;
            a.Play();
            Object.Destroy(g, clip.length / Mathf.Max(0.1f, a.pitch) + 0.1f);
        }
    }
}
