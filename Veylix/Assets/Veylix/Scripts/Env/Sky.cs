using UnityEngine;
using UnityEngine.Rendering;

namespace Veylix
{
    /// <summary>
    /// Time of day and weather. Moves the sun along a real arc (with the moon taking over at night), colours the
    /// sky, ambient light and fog through morning, afternoon, sunset, evening and night, and runs the weather:
    /// clouds, rain that soaks the track (and dries off again), fog. Night switches on headlights, floodlights and
    /// lit windows. In free roam (or with "Dynamic" chosen) time and weather keep changing by themselves.
    /// </summary>
    public class Sky : MonoBehaviour
    {
        public static Sky I { get; private set; }

        [Header("Time")]
        public float hour = 14f;
        public float timeScale = 0f;       // in-game hours per real hour (0 = frozen)
        public bool dynamicWeather;

        [Header("Weather")]
        public WeatherKind weather = WeatherKind.Clear;
        public float cloud, rainTarget, fogTarget;

        Light sun, moon;
        Material skyMat;
        ReflectionProbe probe;
        ParticleSystem rain;
        Transform rainFollow;
        float weatherTimer, probeTimer, lastProbeHour = -99f;
        float latitudeTilt = 35f, sunAzimuth = 110f;
        readonly System.Collections.Generic.List<Light> poolLights = new System.Collections.Generic.List<Light>();

        void Awake()
        {
            I = this;
            var sgo = new GameObject("Sun");
            sgo.transform.SetParent(transform, false);
            sun = sgo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
            var mgo = new GameObject("Moon");
            mgo.transform.SetParent(transform, false);
            moon = mgo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.color = new Color(0.55f, 0.65f, 0.9f);
            RenderSettings.sun = sun;

            var A = VXAssets.I;
            skyMat = A != null && A.skybox != null ? new Material(A.skybox) : new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            var pgo = new GameObject("SkyProbe");
            pgo.transform.SetParent(transform, false);
            probe = pgo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = Settings.Quality == 0 ? 64 : 128;
            probe.cullingMask = 0; // sky only: cheap, and what wet roads and paint mostly reflect
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.size = new Vector3(20000f, 4000f, 20000f);
            probe.importance = 0;
            probe.hdr = false;

            BuildRain();
            for (int i = 0; i < 4; i++)
            {
                var l = new GameObject("FloodPool" + i).AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.range = 38f;
                l.intensity = 0f;
                l.color = new Color(1f, 0.95f, 0.86f);
                l.shadows = LightShadows.None;
                poolLights.Add(l);
            }
        }

        void OnDestroy() { if (I == this) I = null; }

        public void Configure(MapDef map, float startHour, WeatherKind w, bool dynamicTime, bool dynamicWx)
        {
            hour = startHour;
            weather = w;
            timeScale = dynamicTime ? 24f : 0f;   // one in-game day every real hour
            dynamicWeather = dynamicWx;
            latitudeTilt = map.biome == Biome.Tropical || map.biome == Biome.Gulf || map.biome == Biome.Desert ? 18f : map.biome == Biome.Snow ? 55f : 38f;
            sunAzimuth = 90f + map.seed % 60;
            ApplyWeatherTargets(true);
            Env.Wetness = rainTarget > 0.1f ? Mathf.Clamp01(rainTarget * 1.2f) : 0f;
            Env.Rain = rainTarget;
            Env.Fog = fogTarget;
            lastProbeHour = -99f;
            UpdateSky(true);
        }

        void ApplyWeatherTargets(bool instant)
        {
            switch (weather)
            {
                case WeatherKind.Clear: cloud = 0.08f; rainTarget = 0f; fogTarget = 0.05f; break;
                case WeatherKind.Cloudy: cloud = 0.45f; rainTarget = 0f; fogTarget = 0.12f; break;
                case WeatherKind.Overcast: cloud = 0.8f; rainTarget = 0f; fogTarget = 0.22f; break;
                case WeatherKind.LightRain: cloud = 0.85f; rainTarget = 0.4f; fogTarget = 0.3f; break;
                case WeatherKind.HeavyRain: cloud = 1f; rainTarget = 1f; fogTarget = 0.45f; break;
                case WeatherKind.Fog: cloud = 0.6f; rainTarget = 0f; fogTarget = 0.95f; break;
            }
            if (instant) { Env.Rain = rainTarget; Env.Fog = fogTarget; }
        }

        void BuildRain()
        {
            rainFollow = new GameObject("Rain").transform;
            rainFollow.SetParent(transform, false);
            var mat = VXAssets.Or(VXAssets.I != null ? VXAssets.I.rain : null, "Rain", new Color(0.7f, 0.75f, 0.8f, 0.4f));
            rain = rainFollow.gameObject.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.maxParticles = Settings.Quality == 0 ? 1500 : 4000;
            main.startLifetime = 1.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(-11f, -9f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);
            shape.position = new Vector3(0f, 9f, 6f);
            var em = rain.emission;
            em.rateOverTime = 0f;
            var r = rain.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1f;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var col = rain.collision;
            col.enabled = Settings.Quality > 0;
            col.type = ParticleSystemCollisionType.World;
            col.lifetimeLoss = 1f;
            col.quality = ParticleSystemCollisionQuality.Low;
            rain.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (timeScale > 0f) hour = Mathf.Repeat(hour + dt * timeScale / 3600f, 24f);
            if (dynamicWeather)
            {
                weatherTimer -= dt;
                if (weatherTimer <= 0f)
                {
                    weatherTimer = Random.Range(180f, 420f);
                    // Weather drifts to a neighbouring state rather than jumping.
                    int w = (int)weather + Random.Range(-1, 2);
                    if (Random.value < 0.12f) w = (int)WeatherKind.Fog;
                    weather = (WeatherKind)Mathf.Clamp(w, 0, 4);
                    ApplyWeatherTargets(false);
                }
            }
            Env.Rain = Mathf.MoveTowards(Env.Rain, rainTarget, dt / 60f);
            Env.Fog = Mathf.MoveTowards(Env.Fog, fogTarget, dt / 90f);
            // The track soaks up while it rains and dries off slowly (faster in sun and with cars on it).
            if (Env.Rain > 0.05f) Env.Wetness = Mathf.MoveTowards(Env.Wetness, Mathf.Clamp01(Env.Rain * 1.15f), dt / 50f);
            else Env.Wetness = Mathf.MoveTowards(Env.Wetness, 0f, dt / (Env.Daylight > 0.5f ? 240f : 480f));
            UpdateSky(false);
            if (VXAssets.I != null) VXAssets.I.ApplyWetness(Env.Wetness);
            UpdateRain();
            UpdateNightLights();
        }

        void UpdateSky(bool force)
        {
            Env.Hour = hour;
            // Sun position: rises in the east around 6:00, peaks at noon, sets about 19:30 (seasonal summer day).
            float dayT = (hour - 6.2f) / 13.4f;                    // 0 sunrise .. 1 sunset
            float elevation = Mathf.Sin(dayT * Mathf.PI) * (90f - latitudeTilt);
            if (dayT < 0f || dayT > 1f) elevation = -Mathf.Sin(Mathf.Repeat(dayT, 1f) * Mathf.PI) * 25f - 2f;
            float azimuth = sunAzimuth + dayT * 180f;
            sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
            moon.transform.rotation = Quaternion.Euler(Mathf.Max(18f, -elevation + 30f), azimuth + 180f, 0f);

            float day = Mathf.Clamp01((elevation + 4f) / 14f);       // 0 night .. 1 day (soft twilight)
            float golden = Mathf.Clamp01(1f - Mathf.Abs(elevation - 6f) / 14f) * (elevation > -4f ? 1f : 0f);
            Env.Daylight = day;
            float overcast = cloud;

            // Sun colour and strength.
            Color sunCol = Color.Lerp(new Color(1f, 0.96f, 0.9f), new Color(1f, 0.55f, 0.28f), golden);
            sun.color = sunCol;
            sun.intensity = Mathf.Lerp(0f, 1.35f, day) * Mathf.Lerp(1f, 0.28f, overcast);
            sun.shadowStrength = Mathf.Lerp(0.95f, 0.35f, overcast);
            sun.enabled = sun.intensity > 0.01f;
            moon.intensity = (1f - day) * 0.16f * Mathf.Lerp(1f, 0.4f, overcast);
            moon.enabled = moon.intensity > 0.005f;

            // Procedural sky: thicker atmosphere at sunset, grey under cloud.
            if (skyMat != null)
            {
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.85f, 1.45f, golden) + overcast * 0.6f);
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", Color.Lerp(new Color(0.48f, 0.53f, 0.6f), new Color(0.55f, 0.56f, 0.58f), overcast));
                if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", Color.Lerp(new Color(0.08f, 0.08f, 0.1f), new Color(0.42f, 0.4f, 0.38f), day));
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1.25f, day) * Mathf.Lerp(1f, 0.62f, overcast));
                if (skyMat.HasProperty("_SunSize")) skyMat.SetFloat("_SunSize", Mathf.Lerp(0.035f, 0.0f, overcast));
            }

            // Ambient: sky, horizon, ground.
            Color skyAmb = Color.Lerp(new Color(0.02f, 0.03f, 0.06f), new Color(0.52f, 0.6f, 0.75f), day);
            Color eqAmb = Color.Lerp(new Color(0.02f, 0.025f, 0.04f), new Color(0.48f, 0.47f, 0.45f), day);
            skyAmb = Color.Lerp(skyAmb, Color.Lerp(skyAmb, new Color(0.9f, 0.55f, 0.4f), 0.35f), golden);
            Color grey = new Color(0.45f, 0.47f, 0.5f) * Mathf.Max(0.05f, day);
            RenderSettings.ambientSkyColor = Color.Lerp(skyAmb, grey, overcast * 0.7f);
            RenderSettings.ambientEquatorColor = Color.Lerp(eqAmb, grey * 0.95f, overcast * 0.7f);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.02f, 0.02f, 0.02f), new Color(0.22f, 0.2f, 0.17f), day);

            // Fog: aerial perspective in clear weather, thick in fog and rain.
            Color fogCol = Color.Lerp(new Color(0.04f, 0.05f, 0.07f), new Color(0.66f, 0.72f, 0.8f), day);
            fogCol = Color.Lerp(fogCol, Color.Lerp(fogCol, new Color(0.95f, 0.65f, 0.45f), 0.5f), golden * (1f - overcast));
            fogCol = Color.Lerp(fogCol, new Color(0.62f, 0.64f, 0.66f) * Mathf.Max(0.08f, day), overcast * 0.8f);
            RenderSettings.fogColor = fogCol;
            float density = Mathf.Lerp(0.00018f, 0.0038f, Env.Fog * Env.Fog) + Env.Rain * 0.0012f;
            RenderSettings.fogDensity = density;
            Env.TrackC = Env.AmbientC + Mathf.Lerp(-2f, 14f, day * (1f - overcast * 0.7f)) - Env.Wetness * 6f;

            // Refresh the sky reflections when the light has changed enough (one face per frame, no hitch).
            probeTimer -= Time.deltaTime;
            if (force || (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, lastProbeHour * 15f)) > 3.5f && probeTimer <= 0f))
            {
                lastProbeHour = hour;
                probeTimer = 2f;
                probe.RenderProbe();
            }
            float exposure = Mathf.Lerp(0.25f, 1f, day);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.35f, 1f, day);
        }

        void UpdateRain()
        {
            var cam = Cameras.Main;
            if (cam != null)
            {
                rainFollow.position = cam.transform.position;
                Vector3 f = cam.transform.forward; f.y = 0f;
                if (f.sqrMagnitude > 0.01f) rainFollow.rotation = Quaternion.LookRotation(f);
            }
            var em = rain.emission;
            em.rateOverTime = Env.Rain * (Settings.Quality == 0 ? 1200f : 3200f);
        }

        void UpdateNightLights()
        {
            float night = 1f - Env.Daylight;
            // Lit windows and lamps.
            foreach (var m in Buildings.NightMaterials)
                if (m != null) m.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.55f) * night * 1.4f);
            var A = VXAssets.I;
            if (A != null && A.lampHead != null) A.lampHead.SetColor("_EmissionColor", Color.Lerp(new Color(0.2f, 0.2f, 0.2f), new Color(1f, 0.96f, 0.88f) * 3f, night));
            // Headlights.
            foreach (var c in CarFactory.All)
            {
                if (c == null || c.parts == null || c.parts.headSpot == null) continue;
                c.parts.headSpot.intensity = night > 0.4f || Env.Fog > 0.5f ? 2.6f : 0f;
            }
            // Floodlights: a few real point lights follow the camera between the nearest lamp heads.
            var w = World.Current;
            var cam = Cameras.Main;
            bool on = w != null && w.lamps.Count > 0 && night > 0.35f && cam != null;
            if (!on) { foreach (var l in poolLights) l.intensity = 0f; return; }
            Vector3 cp = cam.transform.position;
            // Find the four nearest lamps (simple partial sort; lamps list is small).
            var best = new int[4] { -1, -1, -1, -1 };
            var bestD = new float[4] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
            for (int i = 0; i < w.lamps.Count; i++)
            {
                float d = (w.lamps[i] - cp).sqrMagnitude;
                for (int k = 0; k < 4; k++)
                {
                    if (d >= bestD[k]) continue;
                    for (int j = 3; j > k; j--) { bestD[j] = bestD[j - 1]; best[j] = best[j - 1]; }
                    bestD[k] = d; best[k] = i;
                    break;
                }
            }
            int count = Settings.Quality == 0 ? 2 : 4;
            for (int k = 0; k < poolLights.Count; k++)
            {
                var l = poolLights[k];
                if (k >= count || best[k] < 0) { l.intensity = 0f; continue; }
                l.transform.position = w.lamps[best[k]] + Vector3.down * 1.5f;
                l.intensity = 2.2f * Mathf.Clamp01((night - 0.35f) * 3f);
            }
        }

        public string TimeLabel => Mathf.FloorToInt(hour).ToString("00") + ":" + Mathf.FloorToInt(Mathf.Repeat(hour, 1f) * 60f).ToString("00");

        public static string WeatherName(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Clear: return "Clear";
                case WeatherKind.Cloudy: return "Cloudy";
                case WeatherKind.Overcast: return "Overcast";
                case WeatherKind.LightRain: return "Light rain";
                case WeatherKind.HeavyRain: return "Heavy rain";
                default: return "Fog";
            }
        }
    }
}
