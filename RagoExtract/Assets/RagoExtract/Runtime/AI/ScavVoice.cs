using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Raider voice lines: short shouted callouts in three gruff voices (Resources/Rago/Voice/{line}_{n}_v{voice}.ogg).
    /// Each raider keeps one voice; lines are played at the raider's head and sent to the other players in the raid.
    /// </summary>
    public static class ScavVoice
    {
        public enum Line : byte { Spot, Attack, Reload, Hit, Lost, Hear, Idle, Flash, Kill, ManDown }
        static readonly string[] Names = { "spot", "attack", "reload", "hit", "lost", "hear", "idle", "flash", "kill", "mandown" };
        static readonly int[] Counts = { 5, 4, 3, 3, 4, 4, 6, 2, 3, 2 };
        public const int Voices = 3;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        static float _anyT;   // the whole squad doesn't shout over each other

        public static int Count(Line l) => Counts[(int)l];

        public static AudioClip Clip(Line l, int idx, int voice)
        {
            string key = Names[(int)l] + "_" + Mathf.Clamp(idx, 0, Counts[(int)l] - 1) + "_v" + Mathf.Clamp(voice, 0, Voices - 1);
            if (!_cache.TryGetValue(key, out var c)) { c = Resources.Load<AudioClip>("Rago/Voice/" + key); _cache[key] = c; }
            return c;
        }

        /// <summary>Play a line at a raider's head. Returns its length (0 if nothing played).</summary>
        public static float Play(Line l, int idx, int voice, Vector3 at, bool urgent)
        {
            if (!urgent && Time.time < _anyT) return 0f;
            var clip = Clip(l, idx, voice);
            if (clip == null) return 0f;
            bool loud = l == Line.Spot || l == Line.Attack || l == Line.Hit || l == Line.ManDown || l == Line.Flash || l == Line.Reload;
            Sfx.Play(clip, at, loud ? 1f : 0.7f, Random.Range(0.97f, 1.03f), 2.5f, loud ? 40f : 22f);
            _anyT = Time.time + clip.length * 0.6f;
            return clip.length;
        }
    }
}
