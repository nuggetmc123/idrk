using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace KartVR
{
    /// <summary>
    /// Sky domes: a huge painted sphere around each map, seen from the inside, so
    /// every map has its own sky and you can't see the other maps in the distance.
    ///
    /// KartVR > Build Prototype World bakes them into the scene. If the scene was
    /// built by an older version (no domes yet), they're made when you press Play,
    /// and leftovers from the old "sky island" version are removed.
    /// </summary>
    public static class SkyDomes
    {
        public enum Theme { Day, Sunny, Night, Sunset, Desert, Ice }

        public const string RootName = "SkyDomes";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        /// <summary>Make sure the KartVR world has its domes and none of the old sky islands.</summary>
        public static void Ensure()
        {
            if (GameObject.Find("Hub") == null) return; // not the KartVR world
            RemoveOldSkyIslands();
            if (GameObject.Find(RootName) != null) return;

            var root = new GameObject(RootName).transform;
            foreach (var d in KartMaps.All)
            {
                // Domes go around whatever maps this scene actually has, wherever they are.
                var mapRoot = GameObject.Find(d.root);
                if (mapRoot == null) continue;
                var map = d;
                map.center = mapRoot.transform.position;
                var tex = Paint(d.theme, 512);
                var shader = Shader.Find("Unlit/Texture");
                Material mat;
                if (shader != null) mat = new Material(shader) { mainTexture = tex };
                else
                {
                    // Builds may not include the unlit shader; a black Standard material that glows with the sky works too.
                    mat = new Material(Shader.Find("Standard")) { color = Color.black };
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", tex);
                    mat.SetColor("_EmissionColor", Color.white);
                }
                Create(root, map, InsideOutSphere(), mat);
            }
            Debug.Log("[KartVR] Added sky domes. (Run KartVR > Build Prototype World to bake them into the scene.)");
        }

        public static GameObject Create(Transform parent, KartMaps.Map d, Mesh mesh, Material mat)
        {
            var go = new GameObject(d.mode + "Dome");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = d.center;
            go.transform.localScale = Vector3.one * d.radius * 2f; // the sphere mesh is 1 m across
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>The version before this one had floating sky islands; delete anything left of them.</summary>
        static void RemoveOldSkyIslands()
        {
            int removed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                string n = t.name;
                bool old = n == "SkyGarden" || n == "SkyArena" || n == "SkyPark" || n == "SkyLandingPad" || n == "SkyRoute" ||
                           n == "SkyRouteSign" || n == "SkyGate" || n == "SkyLift" || n == "Cloud" ||
                           (n == "BoostHoop" && t.position.y > 30f);
                if (!old) continue;
                Object.Destroy(t.gameObject);
                removed++;
            }
            // The race's checkpoint 3 got a second trigger box up on the sky route.
            var cp3 = GameObject.Find("Checkpoint3");
            if (cp3 != null)
                foreach (var box in cp3.GetComponents<BoxCollider>())
                    if (box.center.y > 5f) { Object.Destroy(box); removed++; }
            if (removed > 0) Debug.Log("[KartVR] Removed the old sky islands. Run KartVR > Build Prototype World to clean them out of the scene for good.");
        }

        /// <summary>Unity's sphere with its faces turned inwards, so it's visible from inside.</summary>
        public static Mesh InsideOutSphere()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
            mesh.name = "SkyDomeMesh";

            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
            mesh.normals = normals;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int t = tris[i];
                    tris[i] = tris[i + 1];
                    tris[i + 1] = t;
                }
                mesh.SetTriangles(tris, s);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Texture2D Paint(Theme theme, int size)
        {
            var rng = new System.Random(17 + (int)theme);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Dome" + theme };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = Pixel(theme, x / (float)size, y / (float)size, rng);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ painting the skies
        // u goes once round the horizon, v from the bottom (0) to the top (1); the horizon is v = 0.5.

        static float Noise(float u, float v, float scale, float seed) =>
            Mathf.PerlinNoise(u * scale + seed, v * scale + seed * 0.7f) * 0.65f +
            Mathf.PerlinNoise(u * scale * 3f + seed, v * scale * 3f) * 0.35f;

        /// <summary>Noise that joins up seamlessly where u wraps from 1 back to 0.</summary>
        static float WrapNoise(float u, float v, float scale, float seed) =>
            Mathf.Lerp(Noise(u, v, scale, seed), Noise(u - 1f, v, scale, seed), u);

        /// <summary>Height (in v) of a smooth, wrapping skyline of hills or mountains.</summary>
        static float Skyline(float u, float height, float seed)
        {
            float a = u * Mathf.PI * 2f;
            return 0.5f + height * (0.55f + 0.25f * Mathf.Sin(a * 3f + seed) + 0.15f * Mathf.Sin(a * 7f + seed * 2f) + 0.05f * Mathf.Sin(a * 19f + seed * 3f));
        }

        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

        public static Color Pixel(Theme theme, float u, float v, System.Random rng)
        {
            switch (theme)
            {
                case Theme.Day:
                    return Sky(u, v, new Color(0.86f, 0.93f, 1f), new Color(0.28f, 0.52f, 0.93f), 0.8f,
                        new Color(0.3f, 0.55f, 0.32f), 0.035f, new Color(0.45f, 0.62f, 0.5f), 0.06f, new Color(0.27f, 0.47f, 0.24f), 1f);
                case Theme.Sunny:
                    var c = Sky(u, v, new Color(0.95f, 0.95f, 0.9f), new Color(0.2f, 0.45f, 0.9f), 0.4f,
                        new Color(0.25f, 0.45f, 0.25f), 0.03f, new Color(0.55f, 0.6f, 0.72f), 0.11f, new Color(0.3f, 0.5f, 0.25f), 4f);
                    // Snow caps on the far mountains.
                    float peak = Skyline(u, 0.11f, 4f);
                    if (v < peak && v > peak - 0.012f && v > 0.585f) c = Color.Lerp(c, Color.white, 0.8f);
                    return c;
                case Theme.Night:
                    return NightSky(u, v, rng);
                case Theme.Desert:
                    return DesertSky(u, v);
                case Theme.Ice:
                    return IceSky(u, v, rng);
                default:
                    return SunsetSky(u, v);
            }
        }

        static Color Sky(float u, float v, Color horizon, Color zenith, float clouds, Color hills, float hillHeight,
            Color far, float farHeight, Color ground, float seed)
        {
            if (v < 0.5f) return Color.Lerp(Shade(ground, 0.7f), ground, Mathf.InverseLerp(0.3f, 0.5f, v)); // far-off ground
            if (v < Skyline(u, hillHeight, seed + 2f)) return Shade(hills, 0.9f + 0.1f * WrapNoise(u, v, 40f, seed));
            if (v < Skyline(u, farHeight, seed)) return Color.Lerp(far, horizon, 0.25f * Mathf.InverseLerp(0.5f, 0.5f + farHeight, v));

            float up = Mathf.InverseLerp(0.5f, 1f, v);
            var c = Color.Lerp(horizon, zenith, Mathf.Pow(up, 0.6f));
            if (clouds > 0f && up > 0.03f && up < 0.7f)
            {
                float n = WrapNoise(u * 4f, v * 1.2f, 3f, seed);
                float band = Mathf.Sin(Mathf.InverseLerp(0.03f, 0.7f, up) * Mathf.PI);
                c = Color.Lerp(c, Color.white, Mathf.Clamp01((n - 0.52f) * 5f) * band * clouds);
            }
            c.a = 1f;
            return c;
        }

        static Color NightSky(float u, float v, System.Random rng)
        {
            if (v < 0.5f) return Color.Lerp(new Color(0.12f, 0.02f, 0.01f), new Color(0.45f, 0.1f, 0.02f), Mathf.InverseLerp(0.25f, 0.5f, v));
            // Jagged volcano rocks lit from below by the lava.
            if (v < Skyline(u, 0.05f, 9f) - 0.01f * Mathf.Abs(Mathf.Sin(u * 180f)))
                return Color.Lerp(new Color(0.35f, 0.08f, 0.03f), new Color(0.06f, 0.03f, 0.05f), Mathf.InverseLerp(0.5f, 0.56f, v));

            float up = Mathf.InverseLerp(0.5f, 1f, v);
            var c = Color.Lerp(new Color(0.55f, 0.18f, 0.35f), new Color(0.04f, 0.02f, 0.1f), Mathf.Pow(up, 0.45f));
            // A purple nebula streak.
            float n = WrapNoise(u * 3f, v * 2f, 2.5f, 21f);
            float streak = Mathf.Exp(-Mathf.Pow((v - 0.72f - 0.06f * Mathf.Sin(u * Mathf.PI * 4f)) * 14f, 2f));
            c = Color.Lerp(c, new Color(0.5f, 0.25f, 0.85f), streak * Mathf.Clamp01(n * 1.3f - 0.3f) * 0.7f);
            // Stars (fewer near the glowing horizon).
            if (rng.NextDouble() < 0.004 * up) c = Color.Lerp(c, Color.white, 0.6f + (float)rng.NextDouble() * 0.4f);
            c.a = 1f;
            return c;
        }

        static Color DesertSky(float u, float v)
        {
            if (v < 0.5f) return Color.Lerp(new Color(0.55f, 0.32f, 0.18f), new Color(0.85f, 0.6f, 0.38f), Mathf.InverseLerp(0.3f, 0.5f, v));
            // Flat-topped mesas: a stepped skyline.
            float a = u * 24f;
            float step = Mathf.Floor(a);
            float mesa = Mathf.Abs(Mathf.Sin(step * 12.9898f) * 43758.5453f) % 1f;
            float top = 0.5f + (mesa > 0.45f ? 0.03f + (mesa - 0.45f) * 0.12f : 0.008f);
            if (v < top)
            {
                float band = Mathf.Sin(v * 900f) * 0.04f;
                return Color.Lerp(new Color(0.62f, 0.3f, 0.16f), new Color(0.82f, 0.45f, 0.24f), Mathf.InverseLerp(0.5f, top, v) + band);
            }
            float up = Mathf.InverseLerp(0.5f, 1f, v);
            var c = Color.Lerp(new Color(1f, 0.85f, 0.6f), new Color(0.35f, 0.6f, 0.95f), Mathf.Pow(up, 0.5f));
            // Hot sun high in the sky.
            float du = Mathf.DeltaAngle(u * 360f, 40f) / 360f * 2f, dv = v - 0.82f;
            float d = Mathf.Sqrt(du * du + dv * dv);
            if (d < 0.03f) c = new Color(1f, 0.98f, 0.85f);
            else c = Color.Lerp(c, new Color(1f, 0.95f, 0.75f), Mathf.Clamp01(1f - d / 0.15f) * 0.5f);
            c.a = 1f;
            return c;
        }

        static Color IceSky(float u, float v, System.Random rng)
        {
            if (v < 0.5f) return Color.Lerp(new Color(0.55f, 0.65f, 0.75f), new Color(0.85f, 0.92f, 1f), Mathf.InverseLerp(0.3f, 0.5f, v));
            // Snowy mountains with icy blue shadows.
            float peak = Skyline(u, 0.12f, 13f);
            if (v < peak)
            {
                float snow = Mathf.InverseLerp(peak - 0.05f, peak, v);
                return Color.Lerp(new Color(0.35f, 0.42f, 0.55f), new Color(0.95f, 0.97f, 1f), snow * 0.9f + 0.1f * WrapNoise(u, v, 30f, 13f));
            }
            float up = Mathf.InverseLerp(0.5f, 1f, v);
            var c = Color.Lerp(new Color(0.25f, 0.3f, 0.5f), new Color(0.02f, 0.03f, 0.1f), Mathf.Pow(up, 0.5f));
            // Green and purple northern lights.
            float wave = 0.72f + 0.07f * Mathf.Sin(u * Mathf.PI * 6f) + 0.03f * Mathf.Sin(u * Mathf.PI * 14f);
            float curtain = Mathf.Exp(-Mathf.Pow((v - wave) * 18f, 2f)) * (0.5f + 0.5f * WrapNoise(u * 8f, 0f, 2f, 5f));
            c = Color.Lerp(c, new Color(0.3f, 1f, 0.6f), curtain * 0.8f);
            float curtain2 = Mathf.Exp(-Mathf.Pow((v - wave - 0.06f) * 22f, 2f)) * WrapNoise(u * 6f, 1f, 2f, 9f);
            c = Color.Lerp(c, new Color(0.7f, 0.4f, 1f), curtain2 * 0.5f);
            if (rng.NextDouble() < 0.003 * up) c = Color.Lerp(c, Color.white, 0.7f);
            c.a = 1f;
            return c;
        }

        static Color SunsetSky(float u, float v)
        {
            if (v < 0.5f) return Color.Lerp(new Color(0.18f, 0.2f, 0.12f), new Color(0.32f, 0.36f, 0.2f), Mathf.InverseLerp(0.3f, 0.5f, v));
            if (v < Skyline(u, 0.04f, 6f)) return new Color(0.16f, 0.12f, 0.2f); // hills in silhouette

            float up = Mathf.InverseLerp(0.5f, 1f, v);
            Color c = up < 0.25f
                ? Color.Lerp(new Color(1f, 0.62f, 0.25f), new Color(1f, 0.42f, 0.45f), up / 0.25f)
                : Color.Lerp(new Color(1f, 0.42f, 0.45f), new Color(0.25f, 0.2f, 0.55f), Mathf.Pow((up - 0.25f) / 0.75f, 0.7f));
            // Big setting sun.
            float du = Mathf.DeltaAngle(u * 360f, 200f) / 360f * 2f, dv = v - 0.56f;
            float d = Mathf.Sqrt(du * du + dv * dv);
            if (d < 0.035f) c = new Color(1f, 0.93f, 0.6f);
            else c = Color.Lerp(c, new Color(1f, 0.8f, 0.45f), Mathf.Clamp01(1f - d / 0.18f) * 0.6f);
            // Long thin glowing clouds.
            float n = WrapNoise(u * 5f, v * 6f, 2f, 33f);
            float band = Mathf.Sin(Mathf.InverseLerp(0.02f, 0.5f, up) * Mathf.PI);
            c = Color.Lerp(c, new Color(1f, 0.55f, 0.6f), Mathf.Clamp01((n - 0.55f) * 4f) * Mathf.Max(0f, band) * 0.7f);
            c.a = 1f;
            return c;
        }
    }
}
