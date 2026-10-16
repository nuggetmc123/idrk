using System.Collections.Generic;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// All of the game's sound, made in code when the game starts: no audio files,
    /// so nothing is copyrighted. The two music loops are original tunes written
    /// here note by note and played on simple chiptune-style instruments.
    ///
    /// Volumes come from <see cref="GameSettings"/> (menu > Sound).
    /// </summary>
    public static class AudioManager
    {
        public enum Sfx { Click, Place, Delete, Boost, Bump, Checkpoint, Lap, Finish, Beep, Go, Teleport, Error }

        const int Rate = 22050;
        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static AudioClip engine, garageMusic, turboMusic;
        static Host host;
        static float sfxVolume = 0.8f, musicVolume = 0.45f, engineVolume = 0.5f;

        public static AudioClip EngineClip { get { Init(); return engine; } }
        public static float SfxVolume => sfxVolume;
        public static float EngineVolume => engineVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (host != null) return;
            RefreshVolumes();
            if (engine == null) MakeClips();
            var go = new GameObject("KartVR Audio");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
        }

        /// <summary>Re-read the volume settings (the menu calls this after changing them).</summary>
        public static void RefreshVolumes()
        {
            sfxVolume = GameSettings.SfxVolume;
            musicVolume = GameSettings.MusicVolume;
            engineVolume = GameSettings.EngineVolume;
        }

        /// <summary>A sound that plays "in your head" (menus, your own kart).</summary>
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
            readonly List<AudioSource> pool = new List<AudioSource>();
            int next;

            void Awake()
            {
                music = gameObject.AddComponent<AudioSource>();
                music.loop = true;
                music.spatialBlend = 0f;
                music.priority = 0;
                for (int i = 0; i < 12; i++)
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
                // Chill tune in the garage and free roam, fast tune when racing and bumping.
                var mode = GameState.CurrentMode;
                var want = mode == KartMode.Hub || mode == KartMode.FreeRoam ? garageMusic : turboMusic;
                if (music.clip != want)
                {
                    music.clip = want;
                    music.Play();
                }
                music.volume = musicVolume * 0.55f;
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

        static void MakeClips()
        {
            clips[Sfx.Click] = Make("Click", 0.06f, (t, d) => Square(t * 1400f, 0.3f) * Mathf.Exp(-t * 70f) * 0.5f);
            clips[Sfx.Place] = Make("Place", 0.18f, (t, d) =>
            {
                float f = Mathf.Lerp(700f, 260f, t / d);
                return (Triangle(t * f) * 0.8f + Noise() * 0.15f * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 18f);
            });
            clips[Sfx.Delete] = Make("Delete", 0.25f, (t, d) => Square(t * Mathf.Lerp(500f, 120f, t / d), 0.4f) * 0.4f * Mathf.Exp(-t * 10f));
            float boostPhase = 0f, lp = 0f;
            clips[Sfx.Boost] = Make("Boost", 0.8f, (t, d) =>
            {
                // Rising whoosh: filtered noise whose filter opens up, plus a rising rumble.
                float k = Mathf.Lerp(0.05f, 0.5f, t / d);
                lp += (Noise() - lp) * k;
                boostPhase += Mathf.Lerp(60f, 220f, t / d) / Rate;
                float env = Mathf.Min(1f, t * 12f) * Mathf.Exp(-Mathf.Max(0f, t - 0.45f) * 6f);
                return (lp * 1.2f + Saw(boostPhase) * 0.25f) * env;
            });
            float bumpPhase = 0f;
            clips[Sfx.Bump] = Make("Bump", 0.3f, (t, d) =>
            {
                bumpPhase += Mathf.Lerp(140f, 45f, Mathf.Min(1f, t * 8f)) / Rate;
                return (Mathf.Sin(bumpPhase * Mathf.PI * 2f) * Mathf.Exp(-t * 14f) + Noise() * Mathf.Exp(-t * 60f) * 0.5f) * 0.9f;
            });
            clips[Sfx.Checkpoint] = Make("Checkpoint", 0.4f, (t, d) =>
                (Triangle(t * 880f) * (t < 0.1f ? 1f : 0f) + Triangle(t * 1320f) * (t >= 0.1f ? 1f : 0f)) * 0.6f * Mathf.Exp(-(t % 0.1f) * 4f) * Mathf.Exp(-t * 3f));
            clips[Sfx.Lap] = Make("Lap", 0.6f, (t, d) => Arp(t, new[] { 72, 76, 79, 84 }, 0.11f, 0.5f));
            clips[Sfx.Finish] = Make("Finish", 1.4f, (t, d) => Arp(t, new[] { 67, 72, 76, 79, 84, 79, 84, 88 }, 0.13f, 0.5f) * Mathf.Min(1f, (d - t) * 4f));
            clips[Sfx.Beep] = Make("Beep", 0.18f, (t, d) => Square(t * 660f, 0.5f) * 0.35f * Mathf.Min(1f, (d - t) * 40f));
            clips[Sfx.Go] = Make("Go", 0.55f, (t, d) => (Square(t * 990f, 0.5f) * 0.3f + Square(t * 1485f, 0.25f) * 0.15f) * Mathf.Min(1f, (d - t) * 10f));
            float telePhase = 0f;
            clips[Sfx.Teleport] = Make("Teleport", 0.7f, (t, d) =>
            {
                telePhase += Mathf.Lerp(250f, 1400f, t / d) / Rate;
                float shimmer = 0.6f + 0.4f * Mathf.Sin(t * 70f);
                return Triangle(telePhase) * shimmer * 0.5f * Mathf.Sin(Mathf.PI * t / d);
            });
            clips[Sfx.Error] = Make("Error", 0.3f, (t, d) => Square(t * 140f, 0.5f) * 0.35f * (t % 0.15f < 0.11f ? 1f : 0f));

            // Engine: a soft, low electric-kart hum (mostly pure tones, no buzzy saw/square waves) with a gentle
            // "purr" pulse. The loop is a whole number of cycles (63 Hz = 350 samples, purr 21 Hz = 1050 samples)
            // so it never clicks when it repeats.
            var eng = new float[Rate];
            for (int i = 0; i < eng.Length; i++)
            {
                float a = i / 350f * Mathf.PI * 2f;
                float purr = 0.82f + 0.18f * Mathf.Sin(i / 1050f * Mathf.PI * 2f);
                eng[i] = (Mathf.Sin(a) * 0.6f + Mathf.Sin(a * 2f) * 0.22f + Mathf.Sin(a * 3f) * 0.06f) * purr * 0.8f;
            }
            engine = Clip("Engine", eng);

            garageMusic = Song("Garage Groove", 100f,
                new[] { 45, 41, 48, 43, 45, 41, 48, 43 },
                new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 55, 59, 62 } },
                new[]
                {
                    69, -1, 72, -1, 76, 74, 72, -1,   72, -1, 69, -1, 65, -1, 67, 69,
                    67, -1, 72, -1, 76, -1, 79, 77,   76, -1, 74, -1, 71, -1, -1, -1,
                    69, 72, 76, -1, 74, 72, 69, -1,   72, -1, 77, -1, 76, 74, 72, -1,
                    76, -1, 72, -1, 79, -1, 76, 74,   74, -1, 71, -1, 67, -1, -1, -1,
                }, fourOnFloor: false);
            turboMusic = Song("Turbo Loop", 140f,
                new[] { 38, 46, 41, 48, 38, 46, 41, 48 },
                new[] { new[] { 62, 65, 69 }, new[] { 58, 62, 65 }, new[] { 60, 65, 69 }, new[] { 60, 64, 67 } },
                new[]
                {
                    62, 65, 69, -1, 74, -1, 72, 69,   70, -1, 74, -1, 77, -1, 74, 70,
                    72, 69, 65, -1, 69, -1, 72, -1,   72, -1, 76, -1, 79, -1, 76, 72,
                    74, -1, 77, 74, 69, -1, 65, -1,   70, 74, 77, -1, 82, -1, 81, 79,
                    77, -1, 72, -1, 69, 72, 77, -1,   76, -1, 72, -1, 67, -1, -1, -1,
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

    /// <summary>Engine hum for a kart: pitch rises with speed. Added to every kart at runtime.</summary>
    public class KartEngineSound : MonoBehaviour
    {
        AudioSource source;
        KartController kart;
        Vector3 lastPos;
        float speed;

        void Start()
        {
            kart = GetComponent<KartController>();
            source = gameObject.AddComponent<AudioSource>();
            source.clip = AudioManager.EngineClip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.Play();
            lastPos = transform.position;
        }

        void Update()
        {
            if (source == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float raw = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;
            if (raw > 60f) raw = 0f; // teleported
            speed = Mathf.Lerp(speed, raw, 1f - Mathf.Exp(-6f * dt));

            bool parked = kart == null || !kart.IsReady || kart.Parked;
            float s = Mathf.Clamp01(speed / 25f);
            // A narrower pitch range and a quiet idle, so it's a background hum rather than a whine.
            source.pitch = 0.8f + s * 0.7f;
            float target = parked ? 0f : AudioManager.SfxVolume * AudioManager.EngineVolume * (0.12f + 0.28f * s);
            source.volume = Mathf.MoveTowards(source.volume, target, dt * 2f);
            // Your own kart sounds like it's around you; other karts come from where they are.
            source.spatialBlend = kart != null && kart.IsLocal ? 0.3f : 1f;
        }
    }
}
