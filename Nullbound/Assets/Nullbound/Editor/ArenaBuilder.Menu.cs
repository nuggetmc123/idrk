using System.IO;
using System.Linq;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Nullbound.EditorTools
{
    /// <summary>The main menu room.</summary>
    public static partial class ArenaBuilder
    {
        // ---------------- main menu room

        /// <summary>The room you stand in for the main menu, far from the arena (fog hides it).</summary>
        static void BuildMainMenuRoom()
        {
            var room = Group("MainMenuRoom", null, new Vector3(0f, 0f, -420f));
            var floorMat = Mat("MenuFloor", new Color(0.08f, 0.08f, 0.11f), 0.6f);
            Prim(PrimitiveType.Cylinder, "Floor", room, new Vector3(0f, -0.1f, 0f), new Vector3(16f, 0.1f, 16f), floorMat);
            Prim(PrimitiveType.Cylinder, "FloorRing", room, new Vector3(0f, 0.005f, 0f), new Vector3(15f, 0.01f, 15f), neonRed, false);
            Prim(PrimitiveType.Cylinder, "FloorInner", room, new Vector3(0f, 0.01f, 0f), new Vector3(14.6f, 0.01f, 14.6f), floorMat, false);
            Prim(PrimitiveType.Cylinder, "Spot", room, new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 0.01f, 1.2f), neonCyan, false);
            Prim(PrimitiveType.Cube, "Ground", room, new Vector3(0f, -1.5f, 0f), new Vector3(120f, 1f, 120f), Mat("Outside", new Color(0.28f, 0.24f, 0.26f), 0.1f));
            // Tall glowing pillars all round, and an invisible wall so you can't walk off.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * Mathf.PI * 2f / 12f;
                Vector3 p = new Vector3(Mathf.Sin(ang) * 8.5f, 0f, Mathf.Cos(ang) * 8.5f);
                Prim(PrimitiveType.Cube, "Pillar", room, p + Vector3.up * 4f, new Vector3(0.5f, 8f, 0.5f), dark);
                Prim(PrimitiveType.Cube, "PillarGlow", room, p + Vector3.up * 4f, new Vector3(0.08f, 8f, 0.55f), i % 2 == 0 ? neonRed : neonCyan, false, Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f));
                var wallPiece = Prim(PrimitiveType.Cube, "Barrier", room, p * 0.88f + Vector3.up * 3f, new Vector3(4.5f, 6f, 0.5f), dark, true, Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f));
                Object.DestroyImmediate(wallPiece.GetComponent<Renderer>());
                Object.DestroyImmediate(wallPiece.GetComponent<MeshFilter>());
            }
            var arch = Group("Arch", room, new Vector3(0f, 0f, 6f));
            for (int i = 0; i < 16; i++)
            {
                float ang = Mathf.PI * i / 15f;
                Prim(PrimitiveType.Cube, "ArchSeg", arch, new Vector3(Mathf.Cos(ang) * 4.5f, 2f + Mathf.Sin(ang) * 4.5f, 0f), new Vector3(0.5f, 0.5f, 0.3f), i % 2 == 0 ? neonRed : neonGold, false, Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg));
            }
            foreach (var lp in new[] { new Vector3(-3f, 5f, 2f), new Vector3(3f, 5f, 2f) })
            {
                var light = new GameObject("MenuLight").AddComponent<Light>();
                light.transform.SetParent(room, false);
                light.transform.localPosition = lp;
                light.type = LightType.Point;
                light.range = 12f;
                light.intensity = 1.4f;
                light.color = new Color(1f, 0.85f, 0.8f);
            }
            // Its own sky: a closed starry dome-box all round, so the practice map (which is in the same world) can't be seen.
            var stars = Tex("MenuStars", 512, (u, v, r) =>
            {
                double roll = r.NextDouble();
                if (roll < 0.0025) return Color.white;
                if (roll < 0.006) return new Color(0.7f, 0.75f, 1f);
                float n = Noise(u, v, 4f, 3f);
                return new Color(0.05f + n * 0.08f, 0.03f + n * 0.03f, 0.1f + n * 0.12f);
            });
            var unlit = Shader.Find("Unlit/Texture");
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/MenuSky.mat");
            if (skyMat == null && unlit != null) { skyMat = new Material(unlit); AssetDatabase.CreateAsset(skyMat, Root + "/Materials/MenuSky.mat"); }
            if (skyMat != null) { skyMat.mainTexture = stars; skyMat.mainTextureScale = new Vector2(3f, 2f); EditorUtility.SetDirty(skyMat); }
            const float skyHalf = 55f, skyTop = 55f, skyBottom = -5f;
            float wallH = skyTop - skyBottom, wallY = (skyTop + skyBottom) * 0.5f;
            foreach (var (pos, yaw) in new[] { (new Vector3(0f, wallY, skyHalf), 0f), (new Vector3(0f, wallY, -skyHalf), 180f), (new Vector3(skyHalf, wallY, 0f), 90f), (new Vector3(-skyHalf, wallY, 0f), -90f) })
                Prim(PrimitiveType.Quad, "SkyWall", room, pos, new Vector3(skyHalf * 2f, wallH, 1f), skyMat, false, Quaternion.Euler(0f, yaw, 0f));
            Prim(PrimitiveType.Quad, "SkyCeiling", room, new Vector3(0f, skyTop, 0f), new Vector3(skyHalf * 2f, skyHalf * 2f, 1f), skyMat, false, Quaternion.Euler(-90f, 0f, 0f));

            var menu = new GameObject("MainMenu");
            menu.transform.SetParent(room, false);
            menu.AddComponent<MainMenu>();
            MarkStatic(room);
        }

    }
}
