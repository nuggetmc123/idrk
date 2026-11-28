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
        public static float MasterVolume = 1f;   // everything (the listener)
        public static float Effects = 1f;        // guns, footsteps, impacts, raiders, doors, carts...
        public static float Ambience = 1f;       // weather (rain, wind) and background air
        public static float Voices = 1f;         // other players' voice chat
        float _ambBase = 0.35f, _amb2Base;

        /// <summary>Settings changed: apply the volumes to the sounds that are already playing.</summary>
        public static void ApplyVolumes()
        {
            AudioListener.volume = MasterVolume;
            var i = I;
            i._ambience.volume = i._ambBase * Ambience;
            i._ambience2.volume = i._amb2Base * Ambience;
        }

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

        static readonly System.Collections.Generic.Dictionary<int, AnimationCurve> _curves = new System.Collections.Generic.Dictionary<int, AnimationCurve>();
        /// <summary>Volume over distance as a fraction of maxDist: 1 inside minDist, then ~minDist/d (logarithmic), eased to 0 at maxDist.</summary>
        static AnimationCurve Falloff(float ratio)
        {
            ratio = Mathf.Clamp(ratio, 0.005f, 0.9f);
            int k = Mathf.RoundToInt(ratio * 400f);
            if (_curves.TryGetValue(k, out var c)) return c;
            c = new AnimationCurve();
            const int n = 14;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n; t = t * t;   // more keys up close where it changes fast
                t = Mathf.Max(t, i == 0 ? 0f : ratio * 0.5f);
                float v = t <= ratio ? 1f : ratio / t;
                v *= 1f - Mathf.Pow(t, 3f);            // the tail fades out completely by maxDist
                c.AddKey(new Keyframe(t, Mathf.Clamp01(v)));
            }
            _curves[k] = c;
            return c;
        }

        public static AudioSource Play(AudioClip clip, Vector3 pos, float vol = 1f, float pitch = 1f, float minDist = 1.5f, float maxDist = 60f, float spatial = 1f)
        {
            if (clip == null) return null;
            var s = I.NextSource();
            s.transform.position = pos;
            s.clip = clip;
            s.volume = vol * Effects;   // the master volume is the listener's
            s.pitch = pitch;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.spatialBlend = spatial;
            s.loop = false;
            // like the natural (logarithmic) falloff up close, but it reaches SILENCE at maxDist: plain logarithmic falloff
            // stops getting quieter past maxDist, so footsteps, reloads and other players could be heard from across the map
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Falloff(minDist / Mathf.Max(0.01f, maxDist)));
            var lp = s.GetComponent<AudioLowPassFilter>(); if (lp != null) lp.enabled = false;
            // Underground: footsteps, voices and small sounds don't carry through the concrete floors; gunshots and
            // blasts come through faint and muffled
            if (spatial > 0.5f && RagoGame.I != null && RagoGame.I.Player != null)
            {
                float f = Noise.Through(pos, RagoGame.I.Player.transform.position + Vector3.up * 0.3f);
                if (f < 1f)
                {
                    if (maxDist >= 100f) f = Mathf.Sqrt(f) * 0.6f;
                    s.volume *= f;
                    if (lp != null) lp.enabled = true;
                }
            }
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
            var lp = s.GetComponent<AudioLowPassFilter>(); if (lp != null) lp.enabled = muffled || lp.enabled;   // keeps the through-the-floor muffling
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
            // suppressed: quiet for you, and for everyone else (other players, raiders) only heard close by
            // (a bit louder than before: a real suppressed shot is still a sharp crack, just not deafening)
            float loud = g.suppressed ? (local ? 0.3f : 0.24f) : 1f;
            if (dist < 80f || local)
                Play(Synth.Gunshot(g), pos, (local ? 0.9f : 1f) * loud, 0.04f, g.suppressed ? 2.5f : 6f, g.suppressed ? (local ? 55f : 45f) : 400f);
            if (dist > 35f && !g.suppressed)
                Play(Synth.GunshotDistant(g), pos, Mathf.Clamp01(dist / 120f) * 0.9f, 0.05f, 25f, 900f);
        }

        public static void SetAmbience(AudioClip clip, float vol)
        {
            var a = I._ambience; I._ambBase = vol;
            if (a.clip == clip) { a.volume = vol * Ambience; return; }
            a.clip = clip; a.volume = vol * Ambience;
            if (clip != null) a.Play(); else a.Stop();
        }

        public static void SetAmbience2(AudioClip clip, float vol)
        {
            var a = I._ambience2; I._amb2Base = vol;
            if (a.clip != clip) { a.clip = clip; if (clip != null) a.Play(); else a.Stop(); }
            a.volume = vol * Ambience;
        }

        public static void Heartbeat(float intensity)
        {
            var h = I._heart;
            if (h.clip == null) { h.clip = Synth.Heartbeat(); h.Play(); }
            h.volume = Mathf.Clamp01(intensity) * 0.8f * Effects;
            h.pitch = 1f + intensity * 0.4f;
        }
    }
}
