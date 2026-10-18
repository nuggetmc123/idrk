using UnityEditor;
using UnityEngine;

namespace KartVR.EditorTools
{
    /// <summary>
    /// Bakes the sky domes (see <see cref="SkyDomes"/>) into the scene: one painted,
    /// inside-out sphere around each map so you can't see the other maps.
    /// </summary>
    public static partial class KartWorldBuilder
    {
        static void BuildSkyDomes()
        {
            var group = Group(SkyDomes.RootName, null, Vector3.zero);
            var mesh = DomeMesh();
            foreach (var d in KartMaps.All)
            {
                var go = SkyDomes.Create(group, d, mesh, DomeMaterial(d.theme));
                // Batching only: never an occluder (it would hide the whole map inside it).
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            BuildBoundaryWalls(group);
        }

        /// <summary>
        /// A solid, invisible wall just inside every map's barrier (all but the hub, which has real walls), tall
        /// enough that no jump clears it. Karts and walkers simply can't get out; the barrier's reset is the backup.
        /// </summary>
        static void BuildBoundaryWalls(Transform parent)
        {
            var walls = Group("BoundaryWalls", parent, Vector3.zero);
            foreach (var m in KartMaps.All)
            {
                if (m.mode == KartMode.Hub) continue;
                float r = m.radius - KartMaps.BarrierMargin - 1f;
                int segments = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * r / 12f), 32, 256);
                float chord = 2f * Mathf.PI * r / segments + 1f;
                var ring = Group("Walls_" + m.mode, walls, m.center);
                for (int i = 0; i < segments; i++)
                {
                    float a = (i + 0.5f) * 360f / segments;
                    var wall = new GameObject("Wall" + i);
                    wall.transform.SetParent(ring, false);
                    var rot = Quaternion.Euler(0f, a, 0f);
                    wall.transform.localPosition = rot * new Vector3(0f, 40f, r + 1f);
                    wall.transform.localRotation = rot;
                    wall.AddComponent<BoxCollider>().size = new Vector3(chord, 100f, 2f);
                }
            }
        }

        static Mesh DomeMesh()
        {
            string path = Root + "/Materials/SkyDomeMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = SkyDomes.InsideOutSphere();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material DomeMaterial(SkyDomes.Theme theme)
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
            var tex = Tex("Dome" + theme, 1024, (u, v, rng) => SkyDomes.Pixel(theme, u, v, rng));
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
