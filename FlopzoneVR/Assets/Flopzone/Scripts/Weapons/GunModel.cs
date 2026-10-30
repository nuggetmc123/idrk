using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// A gun's look in the world: the meshes from <see cref="GunShapes"/> (real side profiles
    /// of each gun, extruded and bevelled) plus the named points the game needs. Local frame:
    /// +Z is the barrel, +Y is up, the origin is where the hand holds the grip.
    /// </summary>
    public class GunModel : MonoBehaviour
    {
        public GunDef def;
        public Transform muzzle;
        public Transform magwell;
        public Transform scopeEye;      // back lens of the scope (where your eye goes)
        public Transform scopeFront;    // front lens (the scope's camera looks out from here)
        public float scopeRadius = 0.018f;
        /// <summary>The zoomed view's field of view in degrees (smaller = more zoom).</summary>
        public float scopeFov = 6f;
        public Transform ejectPort;
        public Transform pumpPart;
        public Transform foregrip;      // where the second hand holds on
        public Transform ammoAnchor;    // where the ammo counter sits (back of the gun, facing you)
        public Transform visual;        // everything but the mag; recoil kicks this
        public Transform laser;         // the laser's lens, if one is fitted
        public int code;                // the attachments fitted (see Attachments)

        /* ---------------- materials ---------------- */

        public static Material Mat(GM m, Color accent)
        {
            switch (m)
            {
                case GM.Metal: return Prims.Lit(new Color(0.13f, 0.135f, 0.15f), 0.6f, 0.75f, Surface.Metal);
                case GM.Dark: return Prims.Lit(new Color(0.075f, 0.078f, 0.085f), 0.45f, 0.6f, Surface.Metal);
                case GM.Polymer: return Prims.Lit(new Color(0.12f, 0.125f, 0.135f), 0.28f, 0f, Surface.Plastic);
                case GM.Wood: return Prims.Lit(new Color(0.5f, 0.29f, 0.13f), 0.5f, 0f, Surface.Wood);
                case GM.DarkWood: return Prims.Lit(new Color(0.32f, 0.17f, 0.08f), 0.5f, 0f, Surface.Wood);
                case GM.Rubber: return Prims.Lit(new Color(0.08f, 0.08f, 0.085f), 0.12f, 0f, Surface.Rubber);
                case GM.Tan: return Prims.Lit(new Color(0.66f, 0.56f, 0.4f), 0.3f, 0.05f, Surface.Plastic);
                case GM.Olive: return Prims.Lit(new Color(0.3f, 0.34f, 0.21f), 0.3f, 0.05f, Surface.Plastic);
                case GM.Green: return Prims.Lit(new Color(0.27f, 0.36f, 0.22f), 0.3f, 0f, Surface.Plastic);
                case GM.Chrome: return Prims.Lit(new Color(0.75f, 0.76f, 0.78f), 0.88f, 0.95f, Surface.Metal);
                case GM.Brass: return Prims.Lit(new Color(0.85f, 0.65f, 0.25f), 0.75f, 0.9f, Surface.Metal);
                case GM.Bronze: return Prims.Lit(new Color(0.62f, 0.42f, 0.22f), 0.7f, 0.85f, Surface.Metal);
                case GM.Glass: return Prims.Lit(new Color(0.08f, 0.16f, 0.24f), 0.95f, 0.4f, Surface.Plastic);
                case GM.Port: return Prims.Lit(new Color(0.015f, 0.015f, 0.018f), 0.2f);
                case GM.Red: return Prims.Lit(new Color(0.72f, 0.1f, 0.08f), 0.35f, 0f, Surface.Plastic);
                case GM.Glow: return Prims.Emissive(new Color(1f, 0.15f, 0.1f), 3f);
                case GM.Lens: return LensMat;
                default: return Prims.Lit(accent, 0.5f, 0.3f, Surface.PaintedMetal);
            }
        }

        static Material lensMat;
        /// <summary>See-through sight glass with a faint blue tint (shared).</summary>
        static Material LensMat
        {
            get
            {
                if (lensMat == null)
                {
                    lensMat = Prims.Fade(new Color(0.55f, 0.75f, 0.95f, 0.16f));
                    lensMat.SetFloat("_Glossiness", 0.95f);
                }
                return lensMat;
            }
        }

        /* ---------------- meshes, built once per gun and shared ---------------- */

        class Built
        {
            public readonly List<(string group, GM mat, Mesh mesh)> parts = new List<(string, GM, Mesh)>();
            public Dictionary<string, (P3 pos, float angle)> anchors;
        }

        static readonly Dictionary<string, Built> guns = new Dictionary<string, Built>(), mags = new Dictionary<string, Built>();

        static Built Make(GunMesh g)
        {
            var b = new Built { anchors = g.anchors };
            foreach (var kv in g.parts)
            {
                var p = kv.Value;
                if (p.t.Count == 0) continue;
                var mesh = new Mesh { name = "fz_gun_" + kv.Key.Item2 };
                var verts = new List<Vector3>(p.v.Count);
                var norms = new List<Vector3>(p.n.Count);
                var uvs = new List<Vector2>(p.v.Count);
                for (int i = 0; i < p.v.Count; i++)
                {
                    verts.Add(new Vector3(p.v[i].x, p.v[i].y, p.v[i].z));
                    norms.Add(new Vector3(p.n[i].x, p.n[i].y, p.n[i].z));
                    uvs.Add(new Vector2(p.u[i * 2], p.u[i * 2 + 1]));
                }
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(p.t, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                b.parts.Add((kv.Key.Item1, kv.Key.Item2, mesh));
            }
            return b;
        }

        static void Attach(Built b, Transform parent, Color accent, Dictionary<string, Transform> groups)
        {
            foreach (var (group, mat, mesh) in b.parts)
            {
                var holder = parent;
                if (group.Length > 0 && groups != null)
                {
                    if (!groups.TryGetValue(group, out holder))
                    {
                        holder = Prims.Empty(group, parent).transform;
                        groups[group] = holder;
                    }
                }
                var go = new GameObject("part:" + mat);
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Mat(mat, accent);
            }
        }

        Transform Point(string name, Transform parent, Transform fallbackParent = null, Vector3? fallback = null)
        {
            if (anchors != null && anchors.TryGetValue(name, out var a))
            {
                var t = Prims.Empty(name, parent, new Vector3(a.pos.x, a.pos.y, a.pos.z)).transform;
                t.localRotation = Quaternion.Euler(a.angle, 0, 0);
                return t;
            }
            return fallback.HasValue ? Prims.Empty(name, fallbackParent ?? parent, fallback.Value).transform : null;
        }

        Dictionary<string, (P3 pos, float angle)> anchors;

        public static GunModel Build(GunDef def, Transform parent, int code = 0)
        {
            code = Attachments.Clean(def, code);
            string key = def.id + "#" + code;
            if (!guns.TryGetValue(key, out var b) || b.parts.Count == 0 || b.parts[0].mesh == null)
                guns[key] = b = Make(GunShapes.Build(def.id, code));
            var root = new GameObject("gun:" + def.name);
            root.transform.SetParent(parent, false);
            var m = root.AddComponent<GunModel>();
            m.def = def;
            m.code = code;
            m.anchors = b.anchors;
            m.visual = Prims.Empty("visual", root.transform).transform;
            var groups = new Dictionary<string, Transform>();
            Attach(b, m.visual, def.accent, groups);
            groups.TryGetValue("pump", out m.pumpPart);

            var r = root.transform;
            m.muzzle = m.Point("muzzle", r, null, new Vector3(0, 0.07f, 0.4f));
            m.magwell = m.Point("magwell", m.visual, null, new Vector3(0, 0.02f, 0.08f));
            m.foregrip = m.Point("foregrip", r, null, new Vector3(0, -0.03f, 0.05f));
            m.ejectPort = m.Point("eject", r, null, new Vector3(0.02f, 0.07f, 0.05f));
            m.scopeEye = m.Point("scopeEye", r);
            m.scopeFront = m.Point("scopeFront", r);
            m.laser = m.Point("laser", m.visual);
            if (b.anchors.TryGetValue("scopeRadius", out var sr)) m.scopeRadius = sr.pos.y;
            if (b.anchors.TryGetValue("scopeFov", out var sf)) m.scopeFov = sf.pos.y;
            m.ammoAnchor = m.Point("ammo", m.visual, null, new Vector3(0.022f, 0.07f, -0.05f));
            if (m.ammoAnchor != null) m.ammoAnchor.localRotation = Quaternion.Euler(0, -60, 0);
            return m;
        }

        /// <summary>The detachable magazine for this gun. Its origin sits on the gun's magwell; it hangs down (-Y).</summary>
        public static GameObject BuildMag(GunDef def, Transform parent, int code = 0)
        {
            bool ext = Attachments.Get(Attachments.Clean(def, code), Slot.Mag) == 1;
            string key = def.id + (ext ? "#x" : "");
            if (!mags.TryGetValue(key, out var b) || b.parts.Count == 0 || b.parts[0].mesh == null)
                mags[key] = b = Make(GunShapes.BuildMag(def.id, ext));
            var go = new GameObject("mag:" + def.id);
            go.transform.SetParent(parent, false);
            Attach(b, go.transform, def.accent, null);
            return go;
        }
    }
}
