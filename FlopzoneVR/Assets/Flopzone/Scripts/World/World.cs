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
    }

    /// <summary>The map currently loaded: where to spawn, where the flags go, how low is too low.</summary>
    public class World : MonoBehaviour
    {
        public int mapIndex = -1;
        public string mapName = "Hub";
        public float killY = -25f;
        public float gravity = 1f;
        public readonly List<Vector3>[] teamSpawns = { new List<Vector3>(), new List<Vector3>() };
        public readonly List<Vector3> ffaSpawns = new List<Vector3>();
        public Vector3[] flagBase = new Vector3[2];
        public Transform dynamicRoot;
        public MapTheme theme;
        Light sunLight;

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
        }

        void OnDestroy()
        {
            if (RenderSettings.sun == sunLight) RenderSettings.sun = null;
        }
    }
}
