using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Flopzone
{
    /// <summary>Plays the generated sounds and music, with the player's volume settings.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource musicA, musicB;
        string currentMusic;
        float musicFade = 1f;
        int next;
        Task<Dictionary<string, float[]>> building;

        public bool Ready { get; private set; }

        void Awake()
        {
            I = this;
            for (int i = 0; i < 40; i++)
            {
                var go = new GameObject("sfx" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 1.5f;
                src.maxDistance = 80f;
                src.dopplerLevel = 0f;
                pool.Add(src);
            }
            musicA = MakeMusicSource("musicA");
            musicB = MakeMusicSource("musicB");
            building = Task.Run(() => Synth.BuildAll());
        }

        AudioSource MakeMusicSource(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.volume = 0f;
            return s;
        }

        void Update()
        {
            if (!Ready && building != null && building.IsCompleted)
            {
                if (building.Exception != null) Debug.LogError("[Flopzone] sound generation failed: " + building.Exception);
                else
                {
                    foreach (var kv in building.Result)
                    {
                        var clip = AudioClip.Create(kv.Key, kv.Value.Length, 1, Synth.Rate, false);
                        clip.SetData(kv.Value, 0);
                        clips[kv.Key] = clip;
                    }
                }
                building = null;
                Ready = true;
                if (currentMusic != null) { var m = currentMusic; currentMusic = null; PlayMusic(m); }
            }

            var st = Save.Settings;
            AudioListener.volume = st != null ? st.masterVolume : 1f;
            float mv = st != null ? st.musicVolume * 0.55f : 0.3f;
            musicFade = Mathf.MoveTowards(musicFade, 1f, Time.unscaledDeltaTime * 0.5f);
            musicA.volume = mv * musicFade;
            musicB.volume = mv * (1f - musicFade);
            if (musicFade >= 1f && musicB.isPlaying) musicB.Stop();
        }

        public void PlayMusic(string name)
        {
            if (name == currentMusic) return;
            currentMusic = name;
            if (!Ready) return;
            if (!clips.TryGetValue(name, out var clip)) return;
            // swap: the old track fades out on B while the new one fades in on A
            var t = musicB; musicB = musicA; musicA = t;
            musicA.clip = clip;
            musicA.Play();
            musicFade = 0f;
        }

        public AudioClip Clip(string name) => clips.TryGetValue(name, out var c) ? c : null;

        /// <summary>A 3D sound at a world position.</summary>
        public void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f, float spatial = 1f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            var src = pool[next];
            next = (next + 1) % pool.Count;
            src.transform.position = pos;
            src.clip = clip;
            src.spatialBlend = spatial;
            src.pitch = pitch * Random.Range(0.96f, 1.04f);
            src.volume = volume * (Save.Settings != null ? Save.Settings.sfxVolume : 1f);
            src.Play();
        }

        /// <summary>A sound with no position (UI, music stings).</summary>
        public void Play2D(string name, float volume = 1f, float pitch = 1f)
        {
            var cam = Camera.main;
            Play(name, cam ? cam.transform.position : Vector3.zero, volume, pitch, 0f);
        }

        public static void Sfx(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (I) I.Play(name, pos, volume, pitch);
        }

        public static void Ui(string name, float volume = 1f, float pitch = 1f)
        {
            if (I) I.Play2D(name, volume, pitch);
        }
    }
}
