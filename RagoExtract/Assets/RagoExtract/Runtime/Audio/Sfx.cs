using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>Pooled 3D audio playback.</summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx _i;
        readonly List<AudioSource> _pool = new List<AudioSource>();
        int _next;
        AudioSource _ambience, _ambience2, _heart;
        public static float MasterVolume = 1f;

        public static Sfx I
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    _i = go.AddComponent<Sfx>();
                }
                return _i;
            }
        }

        void Awake()
        {
            for (int i = 0; i < 40; i++)
            {
                var go = new GameObject("src" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0f;
                var lp = go.AddComponent<AudioLowPassFilter>(); lp.enabled = false; lp.cutoffFrequency = 900f;   // used to muffle sounds behind walls
                _pool.Add(s);
            }
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true; _ambience.spatialBlend = 0; _ambience.volume = 0.35f;
            _ambience2 = gameObject.AddComponent<AudioSource>();
            _ambience2.loop = true; _ambience2.spatialBlend = 0; _ambience2.volume = 0f;
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.loop = true; _heart.spatialBlend = 0; _heart.volume = 0f;
        }

        AudioSource NextSource()
        {
            for (int k = 0; k < _pool.Count; k++)
            {
                var s = _pool[_next];
                _next = (_next + 1) % _pool.Count;
                if (!s.isPlaying) return s;
            }
            var r = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            return r;
        }

        public static AudioSource Play(AudioClip clip, Vector3 pos, float vol = 1f, float pitch = 1f, float minDist = 1.5f, float maxDist = 60f, float spatial = 1f)
        {
            if (clip == null) return null;
            var s = I.NextSource();
            s.transform.position = pos;
            s.clip = clip;
            s.volume = vol * MasterVolume;
            s.pitch = pitch;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.spatialBlend = spatial;
            s.loop = false;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            var lp = s.GetComponent<AudioLowPassFilter>(); if (lp != null) lp.enabled = false;
            s.Play();
            return s;
        }

        /// <summary>
        /// A voice in the world: fades to silence at maxDist (logarithmic falloff never does - it stays faintly audible
        /// across the whole map) and is muffled when 'muffled' (a wall in between).
        /// </summary>
        public static AudioSource PlayVoice(AudioClip clip, Vector3 pos, float vol, float pitch, float minDist, float maxDist, bool muffled)
        {
            var s = Play(clip, pos, vol, pitch, minDist, maxDist);
            if (s == null) return null;
            s.rolloffMode = AudioRolloffMode.Linear;
            var lp = s.GetComponent<AudioLowPassFilter>(); if (lp != null) lp.enabled = muffled;
            return s;
        }

        public static AudioSource Play(AudioClip[] clips, Vector3 pos, float vol = 1f, float pitchVar = 0.05f, float minDist = 1.5f, float maxDist = 60f)
        {
            if (clips == null || clips.Length == 0) return null;
            return Play(clips[Random.Range(0, clips.Length)], pos, vol, 1f + Random.Range(-pitchVar, pitchVar), minDist, maxDist);
        }

        public static void UI(AudioClip clip, float vol = 0.6f)
        {
            var cam = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head.position : Vector3.zero;
            Play(clip, cam, vol, 1f, 1, 10, 0f);
        }

        /// <summary>Gunshot with distance-dependent layering (close crack vs distant boom).</summary>
        public static void Gunshot(GunDef g, Vector3 pos, bool local)
        {
            var listener = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head.position : pos;
            float dist = Vector3.Distance(listener, pos);
            float loud = g.suppressed ? 0.45f : 1f;
            if (dist < 80f || local)
                Play(Synth.Gunshot(g), pos, (local ? 0.9f : 1f) * loud, 0.04f, g.suppressed ? 2f : 6f, g.suppressed ? 60f : 400f);
            if (dist > 35f && !g.suppressed)
                Play(Synth.GunshotDistant(g), pos, Mathf.Clamp01(dist / 120f) * 0.9f, 0.05f, 25f, 900f);
        }

        public static void SetAmbience(AudioClip clip, float vol)
        {
            var a = I._ambience;
            if (a.clip == clip) { a.volume = vol; return; }
            a.clip = clip; a.volume = vol;
            if (clip != null) a.Play(); else a.Stop();
        }

        public static void SetAmbience2(AudioClip clip, float vol)
        {
            var a = I._ambience2;
            if (a.clip != clip) { a.clip = clip; if (clip != null) a.Play(); else a.Stop(); }
            a.volume = vol;
        }

        public static void Heartbeat(float intensity)
        {
            var h = I._heart;
            if (h.clip == null) { h.clip = Synth.Heartbeat(); h.Play(); }
            h.volume = Mathf.Clamp01(intensity) * 0.8f;
            h.pitch = 1f + intensity * 0.4f;
        }
    }
}
