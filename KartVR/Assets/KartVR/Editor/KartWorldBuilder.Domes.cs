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
            foreach (var d in SkyDomes.Domes)
            {
                var go = SkyDomes.Create(group, d, mesh, DomeMaterial(d.theme));
                // Batching only: never an occluder (it would hide the whole map inside it).
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
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
