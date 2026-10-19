using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// All of the game's sound, made in code when the game starts: no audio files, so nothing is copyrighted.
    /// Punches, blocks, slams and zaps are synthesised; the two battle loops are original tunes written here note
    /// by note. Volumes come from <see cref="GameSettings"/>.
    /// </summary>
    public static class AudioManager
    {
        public enum Sfx { Click, Error, Punch, HeavyPunch, Block, GuardBreak, Whoosh, Jump, Land, Slam, Zap, KO, Respawn, Break, Combo, Ready }

        const int Rate = 22050;
        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static AudioClip lobbyMusic, battleMusic;

        /// <summary>The fast loop plays while you're fighting someone; the calmer one otherwise.</summary>
        public static bool InBattle;
        static Host host;
        static float sfxVolume = 0.85f, musicVolume = 0.4f;

        public static float SfxVolume => sfxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (host != null) return;
            RefreshVolumes();
            if (battleMusic == null) MakeClips();
            var go = new GameObject("Nullbound Audio");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
        }

        /// <summary>Re-read the volume settings (the menu calls this after changing them).</summary>
        public static void RefreshVolumes()
        {
            sfxVolume = GameSettings.SfxVolume;
            musicVolume = GameSettings.MusicVolume;
        }

        /// <summary>A sound that plays "in your head" (menus, your own hits).</summary>
        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            Init();
            host.Play(clips[sfx], null, volume * sfxVolume, pitch);
        }

        /// <summary>A sound that comes from a place in the world (other people's bumps, built pieces...).</summary>
        public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            Init();
            host.Play(clips[sfx], position, volume * sfxVolume, pitch);
        }

        // ---------------- player object: music + a pool of one-shot sources

        class Host : MonoBehaviour
        {
            AudioSource music;
            public float MusicTime => music != null ? music.time : 0f;
            readonly List<AudioSource> pool = new List<AudioSource>();
            int next;

            void Awake()
            {
                music = gameObject.AddComponent<AudioSource>();
                music.loop = true;
                music.spatialBlend = 0f;
                music.priority = 0;
                for (int i = 0; i < 16; i++)
                {
                    var child = new GameObject("Sfx" + i);
                    child.transform.SetParent(transform, false);
                    var s = child.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.rolloffMode = AudioRolloffMode.Linear;
                    s.minDistance = 4f;
                    s.maxDistance = 60f;
                    pool.Add(s);
                }
            }

            public void Play(AudioClip clip, Vector3? position, float volume, float pitch)
            {
                if (clip == null || volume <= 0.001f) return;
                var s = pool[next];
                next = (next + 1) % pool.Count;
                if (position.HasValue)
                {
                    s.transform.position = position.Value;
                    s.spatialBlend = 1f;
                }
                else
                {
                    s.transform.localPosition = Vector3.zero;
                    s.spatialBlend = 0f;
                }
                s.pitch = pitch;
                s.volume = Mathf.Clamp01(volume);
                s.clip = clip;
                s.Play();
            }

            void Update()
            {
                var want = InBattle ? battleMusic : lobbyMusic;
                if (music.clip != want)
                {
                    music.clip = want;
                    music.Play();
                }
                music.volume = Mathf.MoveTowards(music.volume, musicVolume * 0.5f, Time.deltaTime);
                if (!music.isPlaying && musicVolume > 0f) music.Play();
            }
        }

        // ================================================================== making the sounds

        static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        static float Square(float phase, float duty = 0.5f) => (phase - Mathf.Floor(phase)) < duty ? 1f : -1f;
        static float Triangle(float phase) { float p = phase - Mathf.Floor(phase); return p < 0.5f ? p * 4f - 1f : 3f - p * 4f; }
        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase)) - 1f;

        static System.Random noiseRng = new System.Random(1234);
        static float Noise() => (float)noiseRng.NextDouble() * 2f - 1f;

        static AudioClip Clip(string name, float[] data)
        {
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.95f) for (int i = 0; i < data.Length; i++) data[i] *= 0.95f / peak;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        delegate float Synth(float t, float seconds);

        static AudioClip Make(string name, float seconds, Synth f)
        {
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int i = 0; i < data.Length; i++) data[i] = f(i / (float)Rate, seconds);
            return Clip(name, data);
        }

        /// <summary>A hit: a pitched-down sine thump plus a crack of noise.</summary>
        static Synth Thump(float from, float to, float decay, float crack, float crackDecay)
        {
            float phase = 0f;
            return (t, d) =>
            {
                phase += Mathf.Lerp(from, to, Mathf.Min(1f, t * 9f)) / Rate;
                return Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * decay) + Noise() * crack * Mathf.Exp(-t * crackDecay);
            };
        }

        static void MakeClips()
        {
            clips[Sfx.Click] = Make("Click", 0.06f, (t, d) => Square(t * 1400f, 0.3f) * Mathf.Exp(-t * 70f) * 0.5f);
            clips[Sfx.Error] = Make("Error", 0.3f, (t, d) => Square(t * 140f, 0.5f) * 0.35f * (t % 0.15f < 0.11f ? 1f : 0f));
            clips[Sfx.Punch] = Make("Punch", 0.22f, Thump(190f, 70f, 22f, 0.9f, 55f));
            var heavy = Thump(120f, 38f, 9f, 1.2f, 30f);
            clips[Sfx.HeavyPunch] = Make("HeavyPunch", 0.55f, (t, d) => heavy(t, d) * 1.1f + Saw(t * 55f) * 0.15f * Mathf.Exp(-t * 8f));
            clips[Sfx.Block] = Make("Block", 0.25f, (t, d) =>
                (Square(t * 820f, 0.3f) * 0.3f + Square(t * 1230f, 0.5f) * 0.2f + Noise() * 0.4f * Mathf.Exp(-t * 80f)) * Mathf.Exp(-t * 16f));
            clips[Sfx.GuardBreak] = Make("GuardBreak", 0.6f, (t, d) =>
                (Noise() * Mathf.Exp(-t * 10f) * 0.7f + Square(t * Mathf.Lerp(600f, 90f, t / d), 0.4f) * 0.35f) * Mathf.Exp(-t * 4f));
            float lp = 0f;
            clips[Sfx.Whoosh] = Make("Whoosh", 0.35f, (t, d) =>
            {
                lp += (Noise() - lp) * Mathf.Lerp(0.5f, 0.06f, t / d);
                return lp * 1.6f * Mathf.Sin(Mathf.PI * t / d);
            });
            clips[Sfx.Jump] = Make("Jump", 0.2f, (t, d) => Triangle(t * Mathf.Lerp(300f, 700f, t / d)) * 0.35f * Mathf.Exp(-t * 12f));
            clips[Sfx.Land] = Make("Land", 0.2f, Thump(90f, 40f, 25f, 0.4f, 60f));
            var boom = Thump(70f, 28f, 3.5f, 1.4f, 9f);
            float rumble = 0f;
            clips[Sfx.Slam] = Make("Slam", 1.3f, (t, d) =>
            {
                rumble += (Noise() - rumble) * 0.04f;
                return boom(t, d) + rumble * 3f * Mathf.Exp(-t * 2.5f);
            });
            float zap = 0f;
            clips[Sfx.Zap] = Make("Zap", 0.4f, (t, d) =>
            {
                zap += Mathf.Lerp(2200f, 300f, t / d) / Rate;
                return (Saw(zap) * 0.35f + Noise() * 0.35f * (Mathf.Sin(t * 400f) > 0f ? 1f : 0.2f)) * Mathf.Exp(-t * 7f);
            });
            clips[Sfx.KO] = Make("KO", 1.2f, (t, d) =>
                (Square(t * Mathf.Lerp(440f, 110f, t / d), 0.5f) * 0.25f + boom(t, d) * 0.8f) * Mathf.Min(1f, (d - t) * 4f));
            clips[Sfx.Respawn] = Make("Respawn", 0.7f, (t, d) => Arp(t, new[] { 60, 67, 72, 79 }, 0.12f, 0.45f));
            float lp2 = 0f;
            clips[Sfx.Break] = Make("Break", 0.7f, (t, d) =>
            {
                lp2 += (Noise() - lp2) * 0.3f;
                float crackle = Noise() * (Mathf.Sin(t * 260f) > 0.6f ? 1f : 0.15f);
                return (lp2 * 1.2f + crackle * 0.5f) * Mathf.Exp(-t * 6f) + boom(t, d) * 0.4f;
            });
            clips[Sfx.Combo] = Make("Combo", 0.18f, (t, d) => Square(t * 1320f, 0.25f) * 0.25f * Mathf.Exp(-t * 18f));
            clips[Sfx.Ready] = Make("Ready", 0.3f, (t, d) => Arp(t, new[] { 76, 83 }, 0.1f, 0.35f));

            // "Null Zone" (menus / waiting): moody minor groove.
            lobbyMusic = Song("Null Zone", 96f,
                new[] { 45, 41, 43, 40, 45, 41, 43, 44 },
                new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 }, new[] { 52, 56, 59 } },
                new[]
                {
                    69, -1, -1, 72, 71, -1, 69, -1,   65, -1, 69, -1, 72, -1, -1, -1,
                    67, -1, -1, 71, 74, -1, 72, 71,   68, -1, 64, -1, -1, -1, -1, -1,
                    69, -1, 76, -1, 74, 72, 71, -1,   72, -1, 69, -1, 65, -1, 69, 72,
                    71, -1, 74, -1, 79, -1, 76, 74,   76, -1, 71, -1, 68, -1, -1, -1,
                }, fourOnFloor: false);
            // "Breaking Point" (fighting): fast and driving.
            battleMusic = Song("Breaking Point", 160f,
                new[] { 40, 40, 43, 45, 40, 40, 48, 47 },
                new[] { new[] { 52, 55, 59 }, new[] { 52, 55, 59 }, new[] { 55, 59, 62 }, new[] { 57, 60, 64 } },
                new[]
                {
                    64, 64, 67, -1, 64, 71, -1, 69,   67, -1, 64, -1, 62, 64, -1, -1,
                    64, 64, 67, -1, 71, 74, -1, 72,   71, -1, 69, -1, 67, 69, 71, -1,
                    76, -1, 74, 71, 74, -1, 71, 67,   69, -1, 67, 64, 67, -1, 64, 62,
                    64, -1, 71, -1, 72, -1, 74, -1,   75, -1, 71, -1, 66, -1, -1, -1,
                }, fourOnFloor: true);
        }

        static float Arp(float t, int[] notes, float step, float amp)
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - i * step;
            return Square(t * Note(notes[i]), 0.25f) * amp * Mathf.Exp(-local * 6f);
        }

        /// <summary>
        /// An 8-bar loop: bass on the chord roots, a soft arpeggio, the melody (one entry per eighth note,
        /// -1 = rest, a note before rests rings longer) and drums. Notes that run past the end wrap round,
        /// so the loop is seamless.
        /// </summary>
        static AudioClip Song(string name, float bpm, int[] bassRoots, int[][] chords, int[] melody, bool fourOnFloor)
        {
            float beat = 60f / bpm, eighth = beat * 0.5f, sixteenth = beat * 0.25f, bar = beat * 4f;
            int bars = bassRoots.Length;
            var data = new float[Mathf.RoundToInt(bars * bar * Rate)];

            void AddTone(float start, float length, float freq, float amp, int wave, float release = 0.05f)
            {
                int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt((length + release) * Rate);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.005f) * (t < length ? Mathf.Lerp(1f, 0.7f, t / Mathf.Max(0.01f, length)) : 0.7f * (1f - (t - length) / release));
                    float ph = t * freq * (1f + 0.003f * Mathf.Sin(t * 30f) * (wave == 2 ? 1f : 0f));
                    float v = wave == 0 ? Square(ph, 0.5f) : wave == 1 ? Triangle(ph) : Square(ph, 0.25f);
                    data[(s0 + i) % data.Length] += v * amp * env;
                }
            }

            void AddDrum(float start, int kind)
            {
                int s0 = Mathf.RoundToInt(start * Rate);
                float len = kind == 0 ? 0.3f : kind == 1 ? 0.18f : 0.05f;
                int n = Mathf.RoundToInt(len * Rate);
                float phase = 0f, prev = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate, v;
                    if (kind == 0)
                    {
                        phase += Mathf.Lerp(130f, 45f, Mathf.Min(1f, t * 10f)) / Rate;
                        v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 12f) * 0.55f;
                    }
                    else if (kind == 1) v = (Noise() * 0.35f + Mathf.Sin(t * 190f * Mathf.PI * 2f) * 0.15f) * Mathf.Exp(-t * 18f);
                    else { float nz = Noise(); v = (nz - prev) * 0.08f * Mathf.Exp(-t * 70f); prev = nz; }
                    data[(s0 + i) % data.Length] += v;
                }
            }

            for (int b = 0; b < bars; b++)
            {
                float t0 = b * bar;
                int root = bassRoots[b];
                var chord = chords[b % chords.Length];
                // Bass: driving eighths, jumping up an octave on the off-beats.
                for (int e = 0; e < 8; e++)
                    AddTone(t0 + e * eighth, eighth * 0.8f, Note(root + (e % 2 == 1 ? 12 : 0)), 0.16f, 0, 0.02f);
                // Arpeggio: chord tones in sixteenths, quietly.
                for (int s = 0; s < 16; s++)
                    AddTone(t0 + s * sixteenth, sixteenth * 0.7f, Note(chord[s % 3] + (s / 3 % 2 == 1 ? 12 : 0)), 0.05f, 1, 0.03f);
                // Drums.
                for (int q = 0; q < 4; q++)
                {
                    if (fourOnFloor || q % 2 == 0) AddDrum(t0 + q * beat, 0);
                    if (q % 2 == 1) AddDrum(t0 + q * beat, 1);
                }
                for (int e = 0; e < 8; e++) AddDrum(t0 + e * eighth, 2);
            }

            // Melody.
            for (int i = 0; i < melody.Length; i++)
            {
                if (melody[i] < 0) continue;
                int rests = 0;
                while (rests < 2 && i + 1 + rests < melody.Length && melody[i + 1 + rests] < 0) rests++;
                AddTone(i * eighth, eighth * (1 + rests) * 0.85f, Note(melody[i]), 0.13f, 2, 0.08f);
            }
            return Clip(name, data);
        }
    }
}
