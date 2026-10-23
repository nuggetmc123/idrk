using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nullbound
{
    /// <summary>
    /// Spooky season (all of October and Halloween night into November 1st): the briefing room, the Training hall and
    /// the Battleground get the <see cref="HalloweenPlan"/> decorations: glowing jack-o'-lanterns with flickering faces,
    /// graveyards, scarecrows, cauldrons, cobwebs, string lights, banners, floating ghosts and circling bats.
    ///
    /// Built on each headset when the world scene loads (nothing networked, no colliders, so fights aren't affected).
    /// Everything that doesn't move is merged into one mesh per material per area, so it costs a handful of draw calls.
    /// To see it outside October: set <see cref="Force"/> (or PlayerPrefs "nb.halloween" = 1; 2 turns it off).
    /// </summary>
    public class HalloweenDecor : MonoBehaviour
    {
        public static bool Force;

        public static bool IsSeason
        {
            get
            {
                int pref = PlayerPrefs.GetInt("nb.halloween", 0);
                if (pref == 2) return false;
                if (Force || pref == 1) return true;
                var now = DateTime.Now;
                return now.Month == 10 || (now.Month == 11 && now.Day == 1);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsSeason || FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0) return; // only the world scene
            if (FindAnyObjectByType<HalloweenDecor>() != null) return;
            var go = new GameObject("Halloween");
            SceneManager.MoveGameObjectToScene(go, scene); // goes away with the scene
            try { go.AddComponent<HalloweenDecor>().Build(); }
            catch (Exception e) { Debug.LogException(e); Destroy(go); } // decorations must never break the game
        }

        // ------------------------------------------------------------------ building

        static readonly Dictionary<HShape, Mesh> meshes = new Dictionary<HShape, Mesh>();
        static Dictionary<HMat, Material> materials;
        static Material faceMat;

        static Mesh MeshOf(HShape s)
        {
            if (meshes.TryGetValue(s, out var m) && m != null) return m;
            var d = HalloweenShapes.Build(s);
            m = new Mesh { name = "Halloween " + s };
            m.SetVertices(d.vertices);
            m.SetNormals(d.normals);
            m.SetTangents(d.tangents);
            m.SetUVs(0, d.uvs);
            m.SetTriangles(d.triangles, 0);
            m.RecalculateBounds();
            meshes[s] = m;
            return m;
        }

        static Material MatOf(HMat m)
        {
            if (materials == null)
            {
                materials = new Dictionary<HMat, Material>();
                Color orange = new Color(1f, 0.42f, 0.04f), purple = new Color(0.55f, 0.2f, 0.95f);
                void Put(HMat k, Material mat) { mat.name = "Halloween " + k; materials[k] = mat; }
                Put(HMat.Pumpkin, NBUI.Mat(new Color(0.95f, 0.43f, 0.06f), 0.45f));
                Put(HMat.Stem, NBUI.Mat(new Color(0.3f, 0.24f, 0.1f), 0.15f));
                Put(HMat.Face, NBUI.Glow(new Color(1f, 0.72f, 0.18f)));
                Put(HMat.Stone, NBUI.Mat(new Color(0.55f, 0.56f, 0.58f), 0.18f));
                Put(HMat.StoneDark, NBUI.Mat(new Color(0.36f, 0.37f, 0.4f), 0.15f));
                Put(HMat.Soil, NBUI.Mat(new Color(0.24f, 0.17f, 0.11f), 0.05f));
                Put(HMat.Ghost, NBUI.Mat(new Color(0.93f, 0.95f, 1f), 0.4f, 0f, new Color(0.32f, 0.36f, 0.45f)));
                Put(HMat.Black, NBUI.Mat(new Color(0.05f, 0.04f, 0.06f), 0.25f));
                Put(HMat.Web, NBUI.Mat(new Color(0.85f, 0.86f, 0.9f), 0.2f, 0f, new Color(0.25f, 0.25f, 0.28f)));
                Put(HMat.Wire, NBUI.Mat(new Color(0.06f, 0.06f, 0.07f), 0.3f));
                Put(HMat.BulbOrange, NBUI.Glow(orange));
                Put(HMat.BulbPurple, NBUI.Glow(purple));
                Put(HMat.BulbGreen, NBUI.Glow(new Color(0.3f, 1f, 0.25f)));
                Put(HMat.Wood, NBUI.Mat(new Color(0.33f, 0.24f, 0.16f), 0.12f));
                Put(HMat.Shirt, NBUI.Mat(new Color(0.62f, 0.14f, 0.1f), 0.15f));
                Put(HMat.Denim, NBUI.Mat(new Color(0.18f, 0.25f, 0.42f), 0.15f));
                Put(HMat.Straw, NBUI.Mat(new Color(0.86f, 0.72f, 0.33f), 0.1f));
                Put(HMat.Hat, NBUI.Mat(new Color(0.22f, 0.15f, 0.09f), 0.1f));
                Put(HMat.Bark, NBUI.Mat(new Color(0.17f, 0.14f, 0.12f), 0.08f));
                Put(HMat.Candle, NBUI.Mat(new Color(0.95f, 0.9f, 0.78f), 0.3f));
                Put(HMat.Flame, NBUI.Glow(new Color(1f, 0.6f, 0.12f)));
                Put(HMat.Iron, NBUI.Mat(new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.6f));
                Put(HMat.Brew, NBUI.Glow(new Color(0.35f, 1f, 0.2f)));
                Put(HMat.BannerBack, NBUI.Mat(new Color(0.16f, 0.06f, 0.24f), 0.3f));
                Put(HMat.FlagOrange, NBUI.Mat(orange, 0.3f, 0f, orange * 0.25f));
                Put(HMat.FlagPurple, NBUI.Mat(purple, 0.3f, 0f, purple * 0.25f));
                faceMat = materials[HMat.Face];
            }
            return materials[m];
        }

        class PhysicsPlace : IHalloweenPlace
        {
            readonly List<Vector3> spawns = new List<Vector3>();
            readonly RaycastHit[] hits = new RaycastHit[16];
            readonly Collider[] overlaps = new Collider[4];

            public PhysicsPlace()
            {
                foreach (var s in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)) spawns.Add(s.transform.position);
                spawns.Add(HalloweenPlan.MenuOrigin); // where you stand in the main menu
            }

            public bool Ground(int area, float x, float z, out float y)
            {
                // The first thing straight down from just under the ceiling (indoors) or high up (outdoors) has to be
                // flat floor at about floor height: a roof, a treetop, a crate or a smashable house means "not here".
                float top = area == HalloweenPlan.Battle ? 40f : area == HalloweenPlan.Training ? 7f : 4.8f;
                float highest = area == HalloweenPlan.Battle ? 3.5f : 0.35f;
                int n = Physics.RaycastNonAlloc(new Vector3(x, top, z), Vector3.down, hits, top + 20f, ~0, QueryTriggerInteraction.Ignore);
                y = 0f;
                int first = -1;
                for (int i = 0; i < n; i++)
                    if (first < 0 || hits[i].point.y > hits[first].point.y) first = i;
                if (first < 0) return false;
                var h = hits[first];
                if (h.normal.y < 0.6f || h.point.y > highest || h.collider.GetComponentInParent<Breakable>() != null) return false;
                y = h.point.y;
                return true;
            }

            public bool Clear(Vector3 bottom, float radius, float height)
            {
                Vector3 a = bottom + Vector3.up * (radius + 0.06f), b = bottom + Vector3.up * Mathf.Max(radius + 0.07f, height - radius);
                return Physics.OverlapCapsuleNonAlloc(a, b, radius, overlaps, ~0, QueryTriggerInteraction.Ignore) == 0;
            }

            public bool NearSpawn(Vector3 p, float distance)
            {
                foreach (var s in spawns)
                {
                    Vector2 d = new Vector2(s.x - p.x, s.z - p.z);
                    if (d.sqrMagnitude < distance * distance && Mathf.Abs(s.y - p.y) < 4f) return true;
                }
                return false;
            }
        }

        class Ghost { public Transform t; public HGhost spec; }
        class Bat { public Transform t, wingL, wingR; public Vector3 centre; public float radius, speed, phase, height; }

        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Bat> bats = new List<Bat>();

        void Build()
        {
            float started = Time.realtimeSinceStartup;
            var plan = HalloweenPlan.Make(new PhysicsPlace());

            // Everything still: one merged mesh per (area, material, casts shadows).
            var groups = new Dictionary<(int, HMat, bool), List<CombineInstance>>();
            foreach (var p in plan.pieces)
            {
                var key = (p.area, p.mat, p.shadow);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = MeshOf(p.shape), transform = Matrix4x4.TRS(p.pos, p.rot, p.scale) });
            }
            foreach (var g in groups)
            {
                var mesh = new Mesh { name = "Halloween " + g.Key.Item1 + " " + g.Key.Item2, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(g.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = MatOf(g.Key.Item2);
                r.shadowCastingMode = g.Key.Item3 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = g.Key.Item3;
            }

            foreach (var b in plan.banners)
            {
                var t = NBUI.Label(transform, b.text, b.pos, b.fontSize, new Color(1f, 0.55f, 0.12f), new Vector2(b.width, b.fontSize * 0.2f));
                t.transform.rotation = b.rot;
                NBUI.UseFont(t, true);
                NBUI.Fit(t, b.fontSize * 0.4f);
                t.characterSpacing = 8f;
            }
            foreach (var g in plan.ghosts) MakeGhost(g);
            var rng = new System.Random(77);
            foreach (var f in plan.flocks)
                for (int i = 0; i < f.count; i++) MakeBat(f, (float)rng.NextDouble(), rng);

            ErrorLog.Note("Halloween decorations: " + plan.pieces.Count + " pieces, " + groups.Count + " meshes, " + ghosts.Count + " ghosts, " + bats.Count +
                          " bats in " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("F0") + " ms");
        }

        void MakeGhost(HGhost spec)
        {
            var root = new GameObject("Ghost").transform;
            root.SetParent(transform, false);
            Part(root, HShape.Ghost, HMat.Ghost, Vector3.zero, Vector3.one);
            foreach (float sx in new[] { -0.12f, 0.12f })
                Part(root, HShape.Sphere, HMat.Black, new Vector3(sx, 1.06f, 0.318f), new Vector3(0.09f, 0.14f, 0.05f));
            Part(root, HShape.Sphere, HMat.Black, new Vector3(0f, 0.86f, 0.335f), new Vector3(0.1f, 0.13f, 0.05f));
            root.localScale = Vector3.one * spec.size;
            ghosts.Add(new Ghost { t = root, spec = spec });
        }

        void MakeBat(HFlock f, float r01, System.Random rng)
        {
            var root = new GameObject("Bat").transform;
            root.SetParent(transform, false);
            Part(root, HShape.Sphere, HMat.Black, Vector3.zero, new Vector3(0.09f, 0.075f, 0.15f));
            Part(root, HShape.Sphere, HMat.Black, new Vector3(0f, 0.02f, 0.085f), Vector3.one * 0.065f);
            foreach (float sx in new[] { -0.018f, 0.018f })
                Part(root, HShape.Cone, HMat.Black, new Vector3(sx, 0.04f, 0.09f), new Vector3(0.022f, 0.045f, 0.022f));
            Transform Wing(float side)
            {
                var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(side * 0.035f, 0.01f, 0f);
                Part(pivot, HShape.BatWing, HMat.Black, Vector3.zero, new Vector3(side, 1f, 1f));
                return pivot;
            }
            var bat = new Bat
            {
                t = root, wingL = Wing(-1f), wingR = Wing(1f), centre = f.centre,
                radius = f.radius * (0.55f + 0.45f * r01), speed = (0.5f + (float)rng.NextDouble() * 0.5f) * (rng.Next(2) == 0 ? 1f : -1f),
                phase = (float)rng.NextDouble() * Mathf.PI * 2f, height = ((float)rng.NextDouble() - 0.5f) * 2.5f,
            };
            root.localScale = Vector3.one * (1.2f + (float)rng.NextDouble() * 0.6f);
            bats.Add(bat);
        }

        static Transform Part(Transform parent, HShape shape, HMat mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(shape.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshOf(shape);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MatOf(mat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ moving

        void Update()
        {
            float t = Time.time;
            foreach (var g in ghosts)
            {
                if (g.t == null) continue;
                var s = g.spec;
                float a = s.phase + t * s.speed;
                Vector3 p = s.centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s.radius + Vector3.up * Mathf.Sin(t * 1.3f + s.phase) * 0.25f;
                Vector3 vel = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(s.speed);
                g.t.SetPositionAndRotation(p, Quaternion.LookRotation(vel) * Quaternion.Euler(Mathf.Sin(t * 0.9f + s.phase) * 6f, 0f, Mathf.Sin(t * 1.1f) * 5f));
            }
            foreach (var b in bats)
            {
                if (b.t == null) continue;
                float a = b.phase + t * b.speed;
                float r = b.radius * (1f + 0.15f * Mathf.Sin(t * 0.7f + b.phase));
                Vector3 p = b.centre + new Vector3(Mathf.Cos(a) * r, b.height + 0.6f * Mathf.Sin(t * 1.1f + b.phase * 2f), Mathf.Sin(a) * r);
                Vector3 vel = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(b.speed);
                b.t.SetPositionAndRotation(p, Quaternion.LookRotation(vel) * Quaternion.Euler(0f, 0f, -Mathf.Sign(b.speed) * 18f));
                float flap = Mathf.Sin(t * 15f + b.phase * 3f) * 50f;
                b.wingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
                b.wingR.localRotation = Quaternion.Euler(0f, 0f, flap);
            }
            // every carved face flickers like a candle inside
            if (faceMat != null)
            {
                float k = 0.8f + 0.12f * Mathf.Sin(t * 9.1f) + 0.08f * Mathf.Sin(t * 23.7f + 1.3f);
                faceMat.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.18f) * 1.6f * k);
            }
        }
    }
}
