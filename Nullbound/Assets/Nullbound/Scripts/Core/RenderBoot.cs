using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nullbound
{
    /// <summary>
    /// Graphics settings applied when the game starts, whatever the project's quality levels say: 4x MSAA (close to
    /// free on Quest's tiled GPU and the biggest single fix for shimmering edges in VR), real shadows, and cameras with
    /// HDR off (there's no post-processing to use it, and float eye buffers cost bandwidth) and MSAA on.
    /// </summary>
    public static class RenderBoot
    {
        public const int MsaaSamples = 4;
        public const float ShadowDistance = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            QualitySettings.antiAliasing = MsaaSamples;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = ShadowDistance;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
#if UNITY_ANDROID && !UNITY_EDITOR
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
#else
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = ShadowResolution.High;
#endif
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.realtimeReflectionProbes = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUpCameras();

        /// <summary>HDR off and MSAA on for every camera in the loaded scenes (also call this after making a camera).</summary>
        public static void SetUpCameras()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                SetUp(cam);
        }

        public static void SetUp(Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = false;
            cam.allowMSAA = true;
        }
    }
}
