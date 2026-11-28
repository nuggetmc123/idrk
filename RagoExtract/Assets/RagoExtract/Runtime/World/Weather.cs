using UnityEngine;

namespace Rago
{
    /// <summary>What the player picked on the raid terminal for their next raid.</summary>
    public static class RaidChoice
    {
        public static bool Night;
    }

    /// <summary>
    /// Raid weather and time of day. NIGHT is picked on the raid terminal (players who pick night are matched
    /// together); rain and fog are a chance every raid - the same for everyone in it (from the raid seed). The
    /// Underground has none of it. Raiders see much less far at night and in fog.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public static bool Night, Fog, Rain, Snow;
        /// <summary>How far raiders can spot you, as a share of normal (night / fog / rain).</summary>
        public static float SightMul = 1f;
        Transform _rain;
        AudioSource _rainSnd;

        public static void Clear() { Night = Fog = Rain = Snow = false; SightMul = 1f; }

        public static Weather Apply(MapBase map, int seed, bool night)
        {
            Clear();
            if (map == null || map.KeepFog || map.Id == "underground" || map.Sun == null) return null;
            var r = new Rng(seed * 131 + 77);
            Night = night;
            Rain = r.Chance(0.3f);
            Fog = r.Chance(0.25f);
            if (Festive.On || map.Id == "northpole") { Snow = true; Rain = false; }   // Christmas: it snows instead of raining
            var sun = map.Sun;
            Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor;
            Color skyTop = new Color(0.32f, 0.45f, 0.62f), skyHor = new Color(0.68f, 0.72f, 0.75f);
            float clouds = 0.5f;
            if (Rain || Fog)
            {
                // overcast: grey sky, flat light
                sun.intensity *= 0.55f; sun.shadowStrength *= 0.5f;
                sky *= 0.75f; eq *= 0.75f; gr *= 0.75f;
                skyTop = new Color(0.42f, 0.45f, 0.48f); skyHor = new Color(0.6f, 0.62f, 0.64f); clouds = 1f;
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.52f, 0.55f, 0.58f);
                RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, Fog ? 0.016f : 0.0035f);
            }
            if (Night)
            {
                // moonlight: a faint cold light from high up, a dark blue sky; you'll want a weapon light (or night vision)
                // really dark now: barely any moon, you can just make out shapes - a light or NVG is close to a must
                sun.intensity = 0.03f; sun.color = new Color(0.5f, 0.6f, 0.95f); sun.shadowStrength = 0.3f;
                sun.transform.rotation = Quaternion.Euler(52f, 210f, 0f);
                sky = new Color(0.011f, 0.014f, 0.026f); eq = new Color(0.007f, 0.009f, 0.016f); gr = new Color(0.003f, 0.004f, 0.006f);
                skyTop = new Color(0.004f, 0.006f, 0.014f); skyHor = new Color(0.016f, 0.02f, 0.035f); clouds = Rain || Fog ? 0.9f : 0.35f;
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.006f, 0.008f, 0.014f);
                RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, Fog ? 0.025f : 0.007f);
            }
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
            RenderSettings.skybox = Mats.Sky(skyTop, skyHor, gr * 0.8f, sun.color, sun.transform.forward, clouds, Night ? new Color(0.06f, 0.07f, 0.1f) : Color.Lerp(skyHor, Color.white, 0.4f));
            RenderSettings.reflectionIntensity = Night ? 0.15f : 0.45f;
            DynamicGI.UpdateEnvironment();
            SightMul = (Night ? 0.32f : 1f) * (Fog ? 0.5f : 1f) * (Rain ? 0.85f : 1f);
            var go = new GameObject("Weather");
            go.transform.SetParent(map.transform, false);
            var w = go.AddComponent<Weather>();
            if (Rain) w.MakeRain();
            if (Snow) w.MakeSnow();
            return w;
        }

        /// <summary>A one-line description for the raid start banner ("" for a clear day).</summary>
        public static string Describe()
        {
            string s = Night ? "NIGHT" : "";
            if (Rain) s += (s.Length > 0 ? ", " : "") + "RAIN";
            if (Snow) s += (s.Length > 0 ? ", " : "") + "SNOW";
            if (Fog) s += (s.Length > 0 ? ", " : "") + "FOG";
            return s;
        }

        void MakeRain()
        {
            // rain falling around you (it follows your head), and its hiss
            _rain = new GameObject("Rain").transform;
            _rain.SetParent(transform, false);
            var ps = _rain.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = 1.3f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 2500; main.gravityModifier = 0f;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, Night ? 0.18f : 0.35f);
            var em = ps.emission; em.rateOverTime = 1600f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26f, 0.5f, 26f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(-15f, -13f); vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var rend = _rain.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch; rend.velocityScale = 0.06f; rend.lengthScale = 2.5f;
            rend.sharedMaterial = Mats.Unlit("rain_drop", new Color(0.75f, 0.8f, 0.9f, 0.4f), null, Mats.BlendKind.Alpha);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            _rainSnd = gameObject.AddComponent<AudioSource>();
            _rainSnd.clip = Synth.RainLoop(); _rainSnd.loop = true; _rainSnd.spatialBlend = 0f;
            _rainSnd.volume = 0.28f * Sfx.Ambience; _rainSnd.Play();
        }

        void MakeSnow()
        {
            // big soft flakes drifting down around you (they follow your head)
            _rain = new GameObject("Snow").transform;
            _rain.SetParent(transform, false);
            var ps = _rain.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = 9f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 3000; main.gravityModifier = 0f;
            main.startColor = new Color(1f, 1f, 1f, Night ? 0.35f : 0.85f);
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 320f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 0.5f, 30f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f); vel.y = new ParticleSystem.MinMaxCurve(-1.6f, -1.0f); vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.3f; noise.quality = ParticleSystemNoiseQuality.Low;
            var rend = _rain.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = Mats.Unlit("snow_flake", new Color(1f, 1f, 1f, 0.9f), Mats.Soft, Mats.BlendKind.Alpha);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        void LateUpdate()
        {
            if (_rain == null) return;
            if (_rainSnd != null) _rainSnd.volume = 0.28f * Sfx.Ambience;   // follows the settings
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null) _rain.position = pl.Head.position + Vector3.up * (Snow ? 9f : 11f);
        }

        void OnDestroy() { Clear(); }
    }
}
