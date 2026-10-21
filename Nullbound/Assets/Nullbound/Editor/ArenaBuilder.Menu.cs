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

        const float RoomHalf = 8.6f, RoomHeight = 5.6f;

        /// <summary>The room you stand in for the main menu, far from the arena (fog hides it).</summary>
        static void BuildMainMenuRoom()
        {
            var room = Group("MainMenuRoom", null, new Vector3(0f, 0f, -1500f));
            Prim(PrimitiveType.Cube, "Floor", room, new Vector3(0f, -0.25f, 0f), new Vector3(RoomHalf * 2f, 0.5f, RoomHalf * 2f), labFloor);
            // The pad you stand on: steel ring, orange line, a teal spot in the middle.
            Cyl("PadRing", room, new Vector3(0f, 0.01f, 0f), 3.2f, 0.02f, gunmetal);
            Cyl("PadLine", room, new Vector3(0f, 0.022f, 0f), 2.9f, 0.01f, orangePaint);
            Cyl("PadInner", room, new Vector3(0f, 0.026f, 0f), 2.7f, 0.01f, rubber);
            Prim(PrimitiveType.Cylinder, "Spot", room, new Vector3(0f, 0.035f, 0f), new Vector3(1.2f, 0.005f, 1.2f), neonCyan, false);
            Prim(PrimitiveType.Cube, "Ground", room, new Vector3(0f, -1.5f, 0f), new Vector3(120f, 1f, 120f), Mat("Outside", new Color(0.28f, 0.24f, 0.26f), 0.1f));
            // An invisible wall so you can't walk off the pad area.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * Mathf.PI * 2f / 12f;
                Vector3 p = new Vector3(Mathf.Sin(ang) * 8.5f, 0f, Mathf.Cos(ang) * 8.5f);
                var wallPiece = Prim(PrimitiveType.Cube, "Barrier", room, p * 0.88f + Vector3.up * 3f, new Vector3(4.5f, 6f, 0.5f), dark, true, Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f));
                Object.DestroyImmediate(wallPiece.GetComponent<Renderer>());
                Object.DestroyImmediate(wallPiece.GetComponent<MeshFilter>());
            }
            BriefingRoom(Group("BriefingRoom", room, Vector3.zero));
            foreach (var lp in new[] { new Vector3(-3f, 4.6f, 2f), new Vector3(3f, 4.6f, 2f) })
            {
                var light = new GameObject("MenuLight").AddComponent<Light>();
                light.transform.SetParent(room, false);
                light.transform.localPosition = lp;
                light.type = LightType.Point;
                light.range = 12f;
                light.intensity = 1.1f;
                light.color = new Color(1f, 0.96f, 0.92f);
            }
            // Its own sky outside the window: a closed starry box all round, so the practice map (which is in the same world) can't be seen.
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
            Probe("MenuProbe", room, new Vector3(0f, 3f, 0f), new Vector3(18f, 8f, 18f));
            ArtPass(room);
        }

        /// <summary>
        /// The briefing room round the menu: panelled walls with steel pilasters and light strips, a lit ceiling, a big
        /// window out to the stars in front, lockers and crates along the sides, a bay door behind. No colliders.
        /// </summary>
        static void BriefingRoom(Transform r)
        {
            const float H = RoomHeight, L = RoomHalf * 2f + 0.6f;
            var sides = new[]
            {
                ("Front", new Vector3(0f, 0f, RoomHalf), Quaternion.identity),
                ("Right", new Vector3(RoomHalf, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)),
                ("Back", new Vector3(0f, 0f, -RoomHalf), Quaternion.Euler(0f, 180f, 0f)),
                ("Left", new Vector3(-RoomHalf, 0f, 0f), Quaternion.Euler(0f, -90f, 0f)),
            };
            foreach (var (name, pos, rot) in sides)
            {
                // Same frame as the hall walls: +X along the wall, +Z out of the room, inside face at z = 0.
                var w = Group("Wall" + name, r, pos, rot);
                bool front = name == "Front", back = name == "Back";
                if (front)
                {
                    // The window: wall above and below, frame, glass, nothing behind but the stars.
                    Block("Below", w, new Vector3(0f, 0.6f, 0.15f), new Vector3(L, 1.2f, 0.3f), labPanel, null, 0.01f);
                    Block("Above", w, new Vector3(0f, (4.4f + H) * 0.5f, 0.15f), new Vector3(L, H - 4.4f, 0.3f), labPanel, null, 0.01f);
                    foreach (float sx in new[] { -1f, 1f })
                        Block("Side", w, new Vector3(sx * (L * 0.5f - 1.6f), 2.8f, 0.15f), new Vector3(3.2f, 3.2f, 0.3f), labPanel, null, 0.01f);
                    Block("Glass", w, new Vector3(0f, 2.8f, 0.1f), new Vector3(L - 6.4f, 3.2f, 0.03f), glass, null, 0f);
                    Block("Sill", w, new Vector3(0f, 1.15f, -0.15f), new Vector3(L - 6.2f, 0.12f, 0.5f), gunmetal, null, 0.02f);
                    Block("Head", w, new Vector3(0f, 4.46f, -0.05f), new Vector3(L - 6.2f, 0.14f, 0.3f), gunmetal, null, 0.02f);
                    for (int i = 0; i <= 4; i++)
                        Block("Mullion", w, new Vector3(-(L - 6.4f) * 0.5f + i * (L - 6.4f) / 4f, 2.8f, 0f), new Vector3(0.14f, 3.2f, 0.24f), gunmetal, null, 0.02f);
                }
                else Block("Panels", w, new Vector3(0f, H * 0.5f, 0.15f), new Vector3(L, H, 0.3f), labPanel, null, 0.01f);

                Block("Plinth", w, new Vector3(0f, 0.45f, -0.03f), new Vector3(L, 0.9f, 0.06f), labPanelDark, null, 0.005f);
                Block("PlinthCap", w, new Vector3(0f, 0.92f, -0.05f), new Vector3(L, 0.06f, 0.1f), hazard, null, 0.005f);
                Block("OrangeBand", w, new Vector3(0f, 4.75f, -0.02f), new Vector3(L, 0.3f, 0.04f), orangePaint, null, 0.005f);
                Block("Cornice", w, new Vector3(0f, H - 0.2f, -0.2f), new Vector3(L, 0.4f, 0.4f), gunmetal, null, 0.02f);
                foreach (float x in new[] { -RoomHalf + 0.3f, -RoomHalf * 0.5f, RoomHalf * 0.5f, RoomHalf - 0.3f })
                {
                    if (front && Mathf.Abs(x) < RoomHalf - 1f) continue; // keep the window clear
                    Block("Pilaster", w, new Vector3(x, H * 0.5f, -0.2f), new Vector3(0.5f, H, 0.4f), gunmetal, null, 0.02f);
                    Block("PilasterLight", w, new Vector3(x, 2.6f, -0.42f), new Vector3(0.1f, 3f, 0.04f), lightPanel, null, 0.005f);
                }

                if (back)
                {
                    Block("Door", w, new Vector3(0f, 1.75f, -0.08f), new Vector3(3.2f, 3.5f, 0.1f), labPanelDark, null, 0.01f);
                    Block("DoorSplit", w, new Vector3(0f, 1.75f, -0.14f), new Vector3(0.06f, 3.5f, 0.04f), gunmetal);
                    foreach (float sx in new[] { -1f, 1f })
                        Block("DoorJamb", w, new Vector3(sx * 1.85f, 1.8f, -0.12f), new Vector3(0.4f, 3.6f, 0.2f), hazard, null, 0.02f);
                    Block("DoorHead", w, new Vector3(0f, 3.75f, -0.12f), new Vector3(4.1f, 0.4f, 0.2f), hazard, null, 0.02f);
                    Stencil(w, "DoorText", "<b>TEST HALL A</b>", new Vector3(0f, 4.25f, -0.06f), 4f, new Vector2(3.6f, 0.4f));
                    Stencil(w, "RoomName", "<b>BRIEFING 00</b>", new Vector3(-6.45f, 2.6f, -0.06f), 8f, new Vector2(3.2f, 0.8f));
                }
            }

            // Ceiling with a grid of square light panels.
            Block("Ceiling", r, new Vector3(0f, H + 0.15f, 0f), new Vector3(L, 0.3f, L), labPanelDark, null, 0.01f);
            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
                var c = new Vector3(ix * 5f, H - 0.04f, iz * 5f);
                Block("LightFrame", r, c, new Vector3(2.6f, 0.08f, 2.6f), gunmetal, null, 0.01f);
                Block("LightPanel", r, c + Vector3.down * 0.03f, new Vector3(2.3f, 0.04f, 2.3f), lightPanel, null, 0.005f);
            }

            // Lockers down the left wall, a crate stack and a workbench on the right.
            var lockers = Group("Lockers", r, new Vector3(-RoomHalf + 0.35f, 0f, -4.2f), Quaternion.Euler(0f, 90f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float x = -1.5f + i * 0.62f;
                Block("Locker", lockers, new Vector3(x, 1f, 0f), new Vector3(0.6f, 2f, 0.55f), i % 2 == 0 ? gunmetal : labPanelDark, null, 0.015f);
                Block("Vent", lockers, new Vector3(x, 1.75f, -0.28f), new Vector3(0.36f, 0.18f, 0.02f), grate, null, 0f);
                Block("Handle", lockers, new Vector3(x + 0.2f, 1.05f, -0.29f), new Vector3(0.03f, 0.22f, 0.03f), brushedSteel, null, 0.005f);
            }
            var bench = Group("Bench", r, new Vector3(RoomHalf - 0.6f, 0f, -4.2f), Quaternion.Euler(0f, -90f, 0f));
            Block("Top", bench, new Vector3(0f, 0.9f, 0f), new Vector3(2.4f, 0.08f, 0.8f), brushedSteel, null, 0.01f);
            foreach (float sx in new[] { -1.1f, 1.1f })
                Block("Leg", bench, new Vector3(sx, 0.43f, 0f), new Vector3(0.08f, 0.86f, 0.7f), gunmetal, null, 0.01f);
            Block("Monitor", bench, new Vector3(0f, 1.25f, 0.2f), new Vector3(0.9f, 0.55f, 0.04f), screen, null, 0.005f);
            Block("MonitorBack", bench, new Vector3(0f, 1.25f, 0.23f), new Vector3(0.95f, 0.6f, 0.03f), gunmetal, null, 0.005f);
            foreach (var (p, sc) in new[] { (new Vector3(RoomHalf - 0.8f, 0.5f, -6.8f), 1f), (new Vector3(RoomHalf - 1.9f, 0.5f, -7.2f), 1f), (new Vector3(RoomHalf - 1.2f, 1.45f, -7f), 0.9f) })
                Block("Crate", r, p, Vector3.one * sc, crate, Quaternion.Euler(0f, p.x * 13f, 0f), 0.02f);

            NoShadow(r.gameObject);
        }

    }
}
