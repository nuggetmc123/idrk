using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Sky domes: a huge painted sphere around each map, seen from the inside.
    /// Each map gets its own sky and horizon, and you can't see the other maps
    /// floating in the distance. Domes have no collider and cast no shadows.
    /// </summary>
    public static partial class KartWorldBuilder
    {
        enum DomeTheme { Day, Sunny, Night, Sunset }

        // Radii are chosen so every map's ground fits inside its dome and no other map's
        // ground is inside it (maps are 320-380 m apart).
        static void BuildSkyDomes()
        {
            var group = Group("SkyDomes", null, Vector3.zero);
            Dome(group, "HubDome", Vector3.zero, 130f, DomeTheme.Day);
            Dome(group, "RaceDome", RaceOrigin, 165f, DomeTheme.Sunny);
            Dome(group, "BumperDome", BumperOrigin, 190f, DomeTheme.Night);
            Dome(group, "FreeRoamDome", FreeRoamOrigin, 235f, DomeTheme.Sunset);
        }

        static void Dome(Transform parent, string name, Vector3 center, float radius, DomeTheme theme)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = Vector3.one * radius * 2f; // the sphere mesh is 1 m across
            go.AddComponent<MeshFilter>().sharedMesh = InsideOutSphere();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = DomeMaterial(theme);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Batching only: never an occluder (it would hide the whole map inside it).
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Unity's sphere with its faces turned inwards, so it's visible from inside.</summary>
        static Mesh InsideOutSphere()
        {
            string path = Root + "/Materials/SkyDomeMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(tmp);
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
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material DomeMaterial(DomeTheme theme)
        {
            string path = Root + "/Materials/Dome" + theme + ".mat";
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            var tex = Tex("Dome" + theme, 1024, (u, v, rng) => DomePixel(theme, u, v, rng));
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ painting the skies
        // u goes once round the horizon, v from the bottom (0) to the top (1); the horizon is v = 0.5.

        /// <summary>Noise that joins up seamlessly where u wraps from 1 back to 0.</summary>
        static float WrapNoise(float u, float v, float scale, float seed) =>
            Mathf.Lerp(Noise(u, v, scale, seed), Noise(u - 1f, v, scale, seed), u);

        /// <summary>Height (in v) of a smooth, wrapping skyline of hills or mountains.</summary>
        static float Skyline(float u, float height, float seed)
        {
            float a = u * Mathf.PI * 2f;
            return 0.5f + height * (0.55f + 0.25f * Mathf.Sin(a * 3f + seed) + 0.15f * Mathf.Sin(a * 7f + seed * 2f) + 0.05f * Mathf.Sin(a * 19f + seed * 3f));
        }

        static Color DomePixel(DomeTheme theme, float u, float v, System.Random rng)
        {
            switch (theme)
            {
                case DomeTheme.Day:
                    return Sky(u, v, new Color(0.86f, 0.93f, 1f), new Color(0.28f, 0.52f, 0.93f), clouds: 0.8f,
                        hills: new Color(0.3f, 0.55f, 0.32f), hillHeight: 0.035f, far: new Color(0.45f, 0.62f, 0.5f), farHeight: 0.06f,
                        ground: new Color(0.27f, 0.47f, 0.24f), seed: 1f);
                case DomeTheme.Sunny:
                    var c = Sky(u, v, new Color(0.95f, 0.95f, 0.9f), new Color(0.2f, 0.45f, 0.9f), clouds: 0.4f,
                        hills: new Color(0.25f, 0.45f, 0.25f), hillHeight: 0.03f, far: new Color(0.55f, 0.6f, 0.72f), farHeight: 0.11f,
                        ground: new Color(0.3f, 0.5f, 0.25f), seed: 4f);
                    // Snow caps on the far mountains.
                    float peak = Skyline(u, 0.11f, 4f);
                    if (v > 0.5f && v < peak && v > peak - 0.012f && v > 0.585f) c = Color.Lerp(c, Color.white, 0.8f);
                    return c;
                case DomeTheme.Night:
                    return NightSky(u, v, rng);
                default:
                    return SunsetSky(u, v);
            }
        }

        static Color Sky(float u, float v, Color horizon, Color zenith, float clouds, Color hills, float hillHeight,
            Color far, float farHeight, Color ground, float seed)
        {
            if (v < 0.5f)
                return Color.Lerp(ground * 0.7f, ground, Mathf.InverseLerp(0.3f, 0.5f, v)); // below the horizon: far-off ground
            if (v < Skyline(u, hillHeight, seed + 2f)) return Shade(hills, 0.9f + 0.1f * WrapNoise(u, v, 40f, seed));
            if (v < Skyline(u, farHeight, seed)) return Color.Lerp(far, horizon, 0.25f * Mathf.InverseLerp(0.5f, 0.5f + farHeight, v));

            float up = Mathf.InverseLerp(0.5f, 1f, v);
            var c = Color.Lerp(horizon, zenith, Mathf.Pow(up, 0.6f));
            if (clouds > 0f && up > 0.03f && up < 0.7f)
            {
                float n = WrapNoise(u * 4f, v * 1.2f, 3f, seed);
                float band = Mathf.Sin(Mathf.InverseLerp(0.03f, 0.7f, up) * Mathf.PI);
                float puff = Mathf.Clamp01((n - 0.52f) * 5f) * band * clouds;
                c = Color.Lerp(c, new Color(1f, 1f, 1f), puff);
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
