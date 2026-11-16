using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Something on fire (an incendiary round): flames and a flickering light on that part of the body, damage every
    /// half second for a few seconds. On YOU it can be patted out: slap the flames on your arm or chest with your other
    /// hand. Works on the player, raiders and (as a look only) other players.
    /// </summary>
    public class Burning : MonoBehaviour
    {
        public static readonly List<Burning> All = new List<Burning>();
        public float timeLeft = 7f;
        public System.Action<float> tick;   // damage callback (every 0.5 s)
        public bool patOnPlayer;           // the local player's own fire: hands can put it out
        public Hand notThisHand;            // a burning arm can't pat itself
        Light _light;
        float _tick, _puff;

        public static Burning Ignite(Transform bone, float secs, System.Action<float> tick, bool patOnPlayer = false, Hand notThisHand = null)
        {
            if (bone == null) return null;
            var b = bone.GetComponentInChildren<Burning>();
            if (b != null) { b.timeLeft = Mathf.Max(b.timeLeft, secs); return b; }   // already alight: burns longer
            var go = new GameObject("Burning");
            go.transform.SetParent(bone, false);
            b = go.AddComponent<Burning>();
            b.timeLeft = secs; b.tick = tick; b.patOnPlayer = patOnPlayer; b.notThisHand = notThisHand;
            b._light = go.AddComponent<Light>();
            b._light.type = LightType.Point; b._light.color = new Color(1f, 0.55f, 0.2f); b._light.range = 3f; b._light.intensity = 1.2f;
            b._light.renderMode = LightRenderMode.ForceVertex;
            Sfx.Play(Synth.Explosion(), bone.position, 0.15f, 2.2f, 1f, 15f);   // a soft whump as it catches
            return b;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            float dt = Time.deltaTime;
            timeLeft -= dt;
            _light.intensity = 0.9f + Mathf.PerlinNoise(Time.time * 9f, GetInstanceID()) * 0.9f;
            _puff -= dt;
            if (_puff <= 0f) { _puff = 0.05f; Fx.Flames(transform.position, 1f); }
            _tick -= dt;
            if (_tick <= 0f) { _tick = 0.5f; tick?.Invoke(0.5f); }
            if (patOnPlayer) CheckPat();
            if (timeLeft <= 0f) Out(false);
        }

        // ---- patting it out: a quick slap from your other hand on the flames
        Vector3 _lastL, _lastR; bool _init; int _pats;
        void CheckPat()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            Vector3 l = p.HandL.transform.position, r = p.HandR.transform.position;
            if (_init)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.001f);
                foreach (var (h, now, last) in new[] { (p.HandL, l, _lastL), (p.HandR, r, _lastR) })
                {
                    if (h == notThisHand) continue;
                    float speed = (now - last).magnitude / dt - p.MoveVelocity.magnitude;
                    if (speed > 1.1f && (now - transform.position).sqrMagnitude < 0.16f * 0.16f)
                    {
                        _pats++;
                        h.Haptic(0.6f, 0.05f);
                        Sfx.Play(Synth.Impact(Synth.Surface.Flesh, Random.Range(0, 3)), transform.position, 0.4f, 1.3f, 1f, 8f);
                        timeLeft -= 2.5f;
                        if (_pats >= 2 || timeLeft <= 0f) { Out(true); return; }
                    }
                }
            }
            _lastL = l; _lastR = r; _init = true;
        }

        public void Out(bool patted)
        {
            if (patted) { RagoGame.I?.Player?.Toast("Fire's out"); for (int i = 0; i < 4; i++) Fx.Flames(transform.position, 0.5f); }
            Destroy(gameObject);
        }

        public static bool AnyOn(Transform root)
        {
            foreach (var b in All) if (b != null && b.transform.IsChildOf(root)) return true;
            return false;
        }
    }
}
