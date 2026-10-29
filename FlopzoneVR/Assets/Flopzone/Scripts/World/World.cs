using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>Look and feel of a map: sky, sun, fog, ambient light.</summary>
    public class MapTheme
    {
        public Color skyTint = new Color(0.5f, 0.6f, 0.8f), groundTint = new Color(0.4f, 0.37f, 0.33f);
        public float atmosphere = 1f, exposure = 1.25f, sunSize = 0.04f;
        public Color sun = new Color(1f, 0.95f, 0.85f);
        public float sunIntensity = 1.15f;
        public Vector3 sunEuler = new Vector3(50, -30, 0);
        public Color ambSky = new Color(0.55f, 0.62f, 0.75f), ambEquator = new Color(0.45f, 0.45f, 0.45f), ambGround = new Color(0.25f, 0.22f, 0.2f);
        public Color fog = new Color(0.7f, 0.75f, 0.82f);
        public float fogDensity = 0.006f;
        public SkyStyle sky = SkyStyle.Day;
    }

    /// <summary>The map currently loaded: where to spawn, where the flags go, how low is too low.</summary>
    public class World : MonoBehaviour
    {
        public int mapIndex = -1;
        public string mapName = "Hub";
        public float killY = -25f;
        public float gravity = 1f;
        /// <summary>The bigger version of the map (for everything but 1v1 and 2v2).</summary>
        public bool expanded;
        public readonly List<Vector3>[] teamSpawns = { new List<Vector3>(), new List<Vector3>() };
        public readonly List<Vector3> ffaSpawns = new List<Vector3>();
        public Vector3[] flagBase = new Vector3[2];
        public Transform dynamicRoot;
        public MapTheme theme;
        /// <summary>The play area (half sizes). Anyone outside it is put back at a spawn point.</summary>
        public Vector2 bounds = new Vector2(40, 40);
        Light sunLight;
        System.Threading.Tasks.Task<Color32[]> skyTask;
        Texture2D skyTex;
        ReflectionProbe probe;

        public Vector3 Center => Vector3.zero;

        /// <summary>A spawn point (on the ground) and the direction to face.</summary>
        public void Spawn(int team, int index, out Vector3 pos, out float yaw)
        {
            List<Vector3> list = (team == 0 || team == 1) && teamSpawns[team].Count > 0 ? teamSpawns[team] : ffaSpawns;
            if (list.Count == 0) list = new List<Vector3> { Vector3.zero };
            var p = list[Mathf.Abs(index) % list.Count];
            pos = Ground(p);
            var toCenter = Center - pos;
            toCenter.y = 0;
            yaw = toCenter.sqrMagnitude > 0.1f ? Quaternion.LookRotation(toCenter).eulerAngles.y : 0f;
            if ((team == 0 || team == 1) && Mathf.Abs(flagBase[team].z) > 1f) yaw = flagBase[team].z < 0 ? 0f : 180f;
        }

        /// <summary>Drop a point onto whatever floor is under it.</summary>
        public Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 30f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return p;
        }

        public void ApplyTheme(MapTheme t)
        {
            theme = t;
            if (!Application.isPlaying)
            {
                // editor preview: light it, but leave the scene's saved sky/fog alone
                var pl = new GameObject("Sun");
                pl.transform.SetParent(transform, false);
                pl.transform.rotation = Quaternion.Euler(t.sunEuler);
                var l = pl.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = t.sun;
                l.intensity = t.sunIntensity;
                l.shadows = LightShadows.Soft;
                return;
            }
            var sky = new Material(GameAssets.I.skybox);
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", t.skyTint);
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", t.groundTint);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", t.atmosphere);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", t.exposure);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", t.sunSize);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = t.ambSky;
            RenderSettings.ambientEquatorColor = t.ambEquator;
            RenderSettings.ambientGroundColor = t.ambGround;
            RenderSettings.fog = t.fogDensity > 0;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = t.fog;
            RenderSettings.fogDensity = t.fogDensity;

            var lg = new GameObject("Sun");
            lg.transform.SetParent(transform, false);
            lg.transform.rotation = Quaternion.Euler(t.sunEuler);
            sunLight = lg.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = t.sun;
            sunLight.intensity = t.sunIntensity;
            sunLight.shadows = LightShadows.Hard;
            sunLight.shadowStrength = 0.7f;
            sunLight.shadowBias = 0.05f;
            RenderSettings.sun = sunLight;

            // paint the real sky in the background, then swap it in
            skyTask = Sky.Paint(Sky.Make(t.sky, -lg.transform.forward, mapIndex * 31 + 7));
        }

        void Update()
        {
            if (skyTask == null || !skyTask.IsCompleted) return;
            var task = skyTask;
            skyTask = null;
            if (task.Exception != null) { Debug.LogWarning("[Flopzone] sky: " + task.Exception.Message); return; }
            skyTex = new Texture2D(Sky.W, Sky.H, TextureFormat.RGBA32, false) { name = "fz_sky", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            skyTex.SetPixels32(task.Result);
            skyTex.Apply(false, true);
            var pano = GameAssets.I.skyPanoramic != null ? new Material(GameAssets.I.skyPanoramic) : null;
            if (pano == null) return;
            pano.mainTexture = skyTex;
            if (pano.HasProperty("_Exposure")) pano.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = pano;
            DynamicGI.UpdateEnvironment();
            // one reflection snapshot so metal and wet surfaces reflect this map's sky and buildings
            if (probe == null)
            {
                var pg = new GameObject("Reflections");
                pg.transform.SetParent(transform, false);
                pg.transform.localPosition = Vector3.up * 3f;
                probe = pg.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.resolution = 128;
                probe.size = new Vector3(Mathf.Max(80, bounds.x * 2.4f), 60, Mathf.Max(80, bounds.y * 2.4f));
                probe.boxProjection = false;
                probe.importance = 1;
            }
            probe.RenderProbe();
        }

        /// <summary>True if a point is outside the play area (with a little slack).</summary>
        public bool OutOfBounds(Vector3 p) => Mathf.Abs(p.x) > bounds.x + 3f || Mathf.Abs(p.z) > bounds.y + 3f;

        void OnDestroy()
        {
            if (RenderSettings.sun == sunLight) RenderSettings.sun = null;
            if (skyTex != null) Destroy(skyTex);
        }
    }
}
