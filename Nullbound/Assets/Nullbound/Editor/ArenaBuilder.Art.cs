using System.Collections.Generic;
using System.IO;
using Nullbound.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nullbound.EditorTools
{
    /// <summary>
    /// The art kit every area builder uses (the "NULLBOUND Combat Test Facility" look):
    /// - Pbr(): generated PBR texture sets (Nullbound.Art.Surfaces) written as texture assets with the right import
    ///   settings, on Standard materials with the right keywords. Tiling = 1 / metres, for meshes with METRE UVs.
    /// - The palette: ready-made materials for every surface (labPanel, labFloor, gunmetal, hazard, grassMat, ...).
    /// - Mesh kit: bevelled boxes, smooth cylinders, quads and rocks with metre UVs (Nullbound.Art.MeshKit), cached and
    ///   saved as assets. ArtPass() swaps them in for Unity's primitive meshes after an area is built (colliders stay).
    /// - Lighting: reflection probes baked per area, the place presets (LightingPresets), and a hidden
    ///   ShaderVariantKeeper so the Standard shader variants the game makes at runtime are in the build.
    /// </summary>
    public static partial class ArenaBuilder
    {
        // ================================================================== palette

        // Lab (Training, menu).
        static Material labPanel, labPanelDark, labFloor, concretePolished, concreteRaw, gunmetal, brushedSteel, orangePaint, hazard, rubber, grate,
            lightPanel, screen, glass;
        // Field test site (Battleground).
        static Material grassMat, dirtMat, gravelMat, rockMat, cliffMat, brickMat, stoneBlocksMat, woodPlanksMat, timberMat, barkMat, pineMat, asphaltMat, asphaltLaneMat;
        static Material[] plasterMats, roofMats; // plaster: cream, sand, blue. roofs: terracotta, slate, brown

        /// <summary>Generated texture sets, materials made with Pbr(), meshes and probes for this build.</summary>
        static void InitArt()
        {
            EnsureFolder(GenTextureFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(ProbeFolder);
            EnsureFolder(Root + "/Materials/Palette"); // palette materials live apart, so no area's Mat() name can clash with them
            LoadStamps();
            metreMats.Clear();
            meshCache.Clear();
            keepPrimitive.Clear();
            probes.Clear();
            builtinCube = builtinCylinder = builtinQuad = null;

            labPanel = Pbr("Palette/LabPanel", "LabPanel", Color.white, 1f, null, 1024);
            labPanelDark = Pbr("Palette/LabPanelDark", "LabPanelDark", Color.white);
            labFloor = Pbr("Palette/LabFloor", "LabFloor", Color.white, 1f, null, 1024);
            concretePolished = Pbr("Palette/ConcretePolished", "ConcretePolished", Color.white);
            concreteRaw = Pbr("Palette/ConcreteRaw", "ConcreteRaw", Color.white);
            gunmetal = Pbr("Palette/Gunmetal", "Gunmetal", Color.white);
            brushedSteel = Pbr("Palette/BrushedSteel", "BrushedSteel", Color.white);
            orangePaint = Pbr("Palette/SafetyOrange", "SafetyOrangePaint", Color.white);
            hazard = Pbr("Palette/HazardStripe", "HazardStripe", Color.white);
            rubber = Pbr("Palette/RubberMat", "RubberMat", Color.white);
            grate = Pbr("Palette/SteelGrate", "SteelGrate", Color.white);
            lightPanel = Pbr("Palette/LightPanel", "LightPanel", Color.white, 1f, new Color(1.5f, 1.5f, 1.5f));
            screen = Pbr("Palette/Screen", "Screen", Color.white, 1f, new Color(1.1f, 1.1f, 1.1f));
            glass = Fade(Pbr("Palette/Glass", "Glass", Color.white));

            grassMat = Pbr("Palette/Grass", "Grass", Color.white);
            dirtMat = Pbr("Palette/Dirt", "Dirt", Color.white);
            gravelMat = Pbr("Palette/Gravel", "Gravel", Color.white);
            rockMat = Pbr("Palette/Rock", "Rock", Color.white);
            cliffMat = Pbr("Palette/Cliff", "Cliff", Color.white);
            brickMat = Pbr("Palette/Brick", "Brick", Color.white);
            stoneBlocksMat = Pbr("Palette/StoneBlocks", "StoneBlocks", Color.white);
            woodPlanksMat = Pbr("Palette/WoodPlanks", "WoodPlanks", Color.white);
            timberMat = Pbr("Palette/Timber", "Timber", Color.white);
            barkMat = Pbr("Palette/Bark", "Bark", Color.white);
            pineMat = Pbr("Palette/PineNeedles", "PineNeedles", Color.white);
            asphaltMat = Pbr("Palette/Asphalt", "Asphalt", Color.white);
            asphaltLaneMat = Pbr("Palette/AsphaltLane", "AsphaltLane", s => Surfaces.Asphalt(s, true, 6f), 512, Color.white);
            plasterMats = new[]
            {
                Pbr("Palette/PlasterCream", "PlasterCream", s => Surfaces.Plaster(Surfaces.PlasterCream, s, "PlasterCream"), 512, Color.white),
                Pbr("Palette/PlasterSand", "PlasterSand", s => Surfaces.Plaster(Surfaces.PlasterSand, s, "PlasterSand"), 512, Color.white),
                Pbr("Palette/PlasterBlue", "PlasterBlue", s => Surfaces.Plaster(Surfaces.PlasterBlue, s, "PlasterBlue"), 512, Color.white),
            };
            roofMats = new[]
            {
                Pbr("Palette/RoofTerracotta", "RoofTerracotta", s => Surfaces.RoofTiles(Surfaces.RoofTerracotta, s, "RoofTerracotta"), 512, Color.white),
                Pbr("Palette/RoofSlate", "RoofSlate", s => Surfaces.RoofTiles(Surfaces.RoofSlate, s, "RoofSlate"), 512, Color.white),
                Pbr("Palette/RoofBrown", "RoofBrown", s => Surfaces.RoofTiles(Surfaces.RoofBrown, s, "RoofBrown"), 512, Color.white),
            };
        }

        // ================================================================== generated textures

        internal const string GenTextureFolder = Root + "/Textures/Gen";
        const string MeshFolder = Root + "/Generated/Meshes";
        const string ProbeFolder = Root + "/Generated/Probes";
        const int TextureImportVersion = 1; // bump when the import settings below change
        const string StampFile = "Library/NullboundGenerated.txt";

        /// <summary>Settings for the textures being written right now (read by the import settings hook): anisotropic level, size.</summary>
        internal static readonly Dictionary<string, Vector2Int> pendingImport = new Dictionary<string, Vector2Int>();
        static Dictionary<string, string> stamps;

        /// <summary>The maps of a texture set as imported textures. Use with PbrMaterial() or for custom materials.</summary>
        struct GenMaps
        {
            public Texture2D albedo, normal, mask, emission;
            public float metres;
        }

        /// <summary>Surfaces seen at grazing angles (floors and ground) get more anisotropic filtering.</summary>
        static readonly HashSet<string> floorSurfaces = new HashSet<string>
        {
            "LabFloor", "ConcretePolished", "ConcreteRaw", "RubberMat", "SteelGrate", "Grass", "Dirt", "Gravel", "Asphalt", "AsphaltLane", "WoodPlanks", "StoneBlocks",
        };

        static void LoadStamps()
        {
            stamps = new Dictionary<string, string>();
            if (!File.Exists(StampFile)) return;
            foreach (var line in File.ReadAllLines(StampFile))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) stamps[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
        }

        static string Stamp(string path)
        {
            if (stamps == null) LoadStamps();
            return stamps.TryGetValue(path, out var s) ? s : null;
        }

        static void SetStamp(string path, string stamp)
        {
            if (stamps == null) LoadStamps();
            stamps[path] = stamp;
            var lines = new List<string>();
            foreach (var kv in stamps) lines.Add(kv.Key + "\t" + kv.Value);
            try { File.WriteAllLines(StampFile, lines); } catch (System.Exception e) { Debug.LogWarning("[Nullbound] " + e.Message); }
        }

        /// <summary>Import settings for generated maps (named ..._A albedo, _N normal, _M mask, _E emission).</summary>
        internal static void ConfigureGenTexture(TextureImporter imp, string path, int aniso, int maxSize)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            char kind = file.Length > 2 && file[file.Length - 2] == '_' ? file[file.Length - 1] : 'A';
            imp.textureType = kind == 'N' ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (kind == 'N') imp.convertToNormalmap = false;
            imp.sRGBTexture = kind == 'A' || kind == 'E';
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = aniso;
            imp.mipmapEnabled = true;
            imp.mipmapFilter = kind == 'N' ? TextureImporterMipFilter.BoxFilter : TextureImporterMipFilter.KaiserFilter;
            imp.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(maxSize), 32, 1024);
            imp.textureCompression = kind == 'N' ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            imp.streamingMipmaps = false;
        }

        /// <summary>
        /// Writes one map as a PNG (Color32, fast) unless the same pixels were already written and imported, and
        /// returns the imported texture.
        /// </summary>
        static Texture2D WriteMap(string file, int size, Color[] px, int aniso)
        {
            string path = GenTextureFolder + "/" + file + ".png";
            var c32 = new Color32[px.Length];
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                byte r = (byte)(Mathf.Clamp01(c.r) * 255f + 0.5f), g = (byte)(Mathf.Clamp01(c.g) * 255f + 0.5f);
                byte b = (byte)(Mathf.Clamp01(c.b) * 255f + 0.5f), a = (byte)(Mathf.Clamp01(c.a) * 255f + 0.5f);
                c32[i] = new Color32(r, g, b, a);
                hash = (hash ^ (uint)(r | g << 8 | b << 16 | a << 24)) * 1099511628211UL;
            }
            string stamp = TextureImportVersion + ":" + size + ":" + aniso + ":" + hash.ToString("x16");
            if (File.Exists(path) && Stamp(path) == stamp)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (existing != null) return existing;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(c32);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            pendingImport[path] = new Vector2Int(aniso, size);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            finally { pendingImport.Remove(path); }
            SetStamp(path, stamp);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static GenMaps WriteSet(TexSet set, string file)
        {
            int aniso = floorSurfaces.Contains(set.name) || floorSurfaces.Contains(file) ? 8 : 4;
            var maps = new GenMaps
            {
                albedo = WriteMap(file + "_A", set.size, set.albedo, aniso),
                normal = WriteMap(file + "_N", set.size, set.normal, aniso),
                mask = WriteMap(file + "_M", set.size, set.mask, aniso),
                emission = set.emission != null ? WriteMap(file + "_E", set.size, set.emission, aniso) : null,
                metres = set.metres,
            };
            return maps;
        }

        /// <summary>
        /// A surface's maps, generated only when its recipe (Surfaces.Version), size or the import settings changed
        /// since last time; otherwise the existing textures are loaded straight away.
        /// </summary>
        static GenMaps CachedMaps(string key, int size, System.Func<int, TexSet> make)
        {
            string file = size == 512 ? key : key + "_" + size;
            string setPath = GenTextureFolder + "/" + file + ".set";
            string recipe = "recipe:" + Surfaces.Version + ":" + TextureImportVersion + ":" + size;
            string s = Stamp(setPath);
            if (s != null && s.StartsWith(recipe + "|"))
            {
                var parts = s.Split('|');
                if (parts.Length >= 3 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float metres))
                {
                    bool glows = parts[2] == "1";
                    var maps = new GenMaps
                    {
                        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(GenTextureFolder + "/" + file + "_A.png"),
                        normal = AssetDatabase.LoadAssetAtPath<Texture2D>(GenTextureFolder + "/" + file + "_N.png"),
                        mask = AssetDatabase.LoadAssetAtPath<Texture2D>(GenTextureFolder + "/" + file + "_M.png"),
                        emission = glows ? AssetDatabase.LoadAssetAtPath<Texture2D>(GenTextureFolder + "/" + file + "_E.png") : null,
                        metres = metres,
                    };
                    if (maps.albedo != null && maps.normal != null && maps.mask != null && (!glows || maps.emission != null)) return maps;
                }
            }
            var set = make(size);
            var result = WriteSet(set, file);
            SetStamp(setPath, recipe + "|" + set.metres.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (set.emission != null ? "1" : "0"));
            return result;
        }

        // ================================================================== materials

        /// <summary>Materials whose textures are laid out for metre UVs (made by Pbr), so ArtPass may give their objects metre-UV meshes.</summary>
        static readonly HashSet<Material> metreMats = new HashSet<Material>();

        static Material LoadOrCreateMat(string name)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static readonly string[] standardMaps = { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_ParallaxMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap", "_SpecGlossMap" };

        /// <summary>
        /// Puts a material back to a plain opaque Standard material: shader reassigned, every keyword off, every map
        /// cleared, default values. Everything that makes materials is built on this, so rebuilding an existing
        /// project never keeps stale maps, keywords or blend modes.
        /// </summary>
        static void ResetStandard(Material m)
        {
            var shader = LitShader();
            if (shader != null && m.shader != shader) m.shader = shader;
            m.shaderKeywords = new string[0];
            m.SetOverrideTag("RenderType", "");
            foreach (var map in standardMaps)
                if (m.HasProperty(map))
                {
                    m.SetTexture(map, null);
                    m.SetTextureScale(map, Vector2.one);
                    m.SetTextureOffset(map, Vector2.zero);
                }
            void F(string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }
            F("_Mode", 0f);
            F("_SrcBlend", (float)BlendMode.One);
            F("_DstBlend", (float)BlendMode.Zero);
            F("_ZWrite", 1f);
            F("_Cutoff", 0.5f);
            F("_Glossiness", 0.5f);
            F("_GlossMapScale", 1f);
            F("_SmoothnessTextureChannel", 0f);
            F("_Metallic", 0f);
            F("_BumpScale", 1f);
            F("_Parallax", 0.02f);
            F("_OcclusionStrength", 1f);
            F("_DetailNormalMapScale", 1f);
            F("_UVSec", 0f);
            F("_SpecularHighlights", 1f);
            F("_GlossyReflections", 1f);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.renderQueue = -1;
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.enableInstancing = true;
        }

        /// <summary>
        /// A Standard material with a full texture set: albedo x tint, normal map, metallic/smoothness mask (smoothness
        /// from the mask's alpha x `smoothness`), ambient occlusion (mask green), and emission when `emission` is given
        /// (x the set's emission map if it has one). Tiling = 1 / metres: use on meshes with metre UVs (the mesh kit).
        /// </summary>
        static Material PbrMaterial(string name, GenMaps maps, Color tint, float smoothness, Color? emission)
        {
            var m = LoadOrCreateMat(name);
            ResetStandard(m);
            var tiling = Vector2.one / Mathf.Max(0.01f, maps.metres);
            m.SetColor("_Color", tint);
            m.SetTexture("_MainTex", maps.albedo);
            m.SetTextureScale("_MainTex", tiling);
            if (maps.normal != null)
            {
                m.SetTexture("_BumpMap", maps.normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (maps.mask != null)
            {
                m.SetTexture("_MetallicGlossMap", maps.mask);
                m.SetFloat("_GlossMapScale", smoothness);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.EnableKeyword("_METALLICGLOSSMAP");
                m.SetTexture("_OcclusionMap", maps.mask); // the Standard shader reads occlusion from green
                m.SetFloat("_OcclusionStrength", 1f);
            }
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                if (maps.emission != null) m.SetTexture("_EmissionMap", maps.emission);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            metreMats.Add(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A PBR material from a texture set you generated yourself (e.g. Surfaces.Crate(512, 7)).</summary>
        static Material Pbr(string name, TexSet set, Color tint, float smoothness = 1f, Color? emission = null)
        {
            var maps = WriteSet(set, set.size == 512 ? set.name : set.name + "_" + set.size);
            if (!emission.HasValue && set.emission != null) emission = Color.white;
            return PbrMaterial(name, maps, tint, smoothness, emission);
        }

        /// <summary>
        /// A PBR material from a library surface (any id Surfaces.Get knows: "LabPanel", "Gunmetal", "Grass", ...).
        /// Textures are only regenerated when the recipe changed. Surfaces with an emission map glow white x the map
        /// unless you pass another emission colour.
        /// </summary>
        static Material Pbr(string name, string surfaceId, Color tint, float smoothness = 1f, Color? emission = null, int size = 512)
        {
            var maps = CachedMaps(surfaceId, size, s => Surfaces.Get(surfaceId, s));
            if (!emission.HasValue && maps.emission != null) emission = Color.white;
            return PbrMaterial(name, maps, tint, smoothness, emission);
        }

        /// <summary>
        /// A PBR material from your own recipe, cached under `cacheKey` (only regenerated when Surfaces.Version or
        /// the size changes): Pbr("Crate7", "Crate7", s => Surfaces.Crate(s, 7), 512, Color.white).
        /// </summary>
        static Material Pbr(string name, string cacheKey, System.Func<int, TexSet> make, int size, Color tint, float smoothness = 1f, Color? emission = null)
        {
            var maps = CachedMaps(cacheKey, size, make);
            if (!emission.HasValue && maps.emission != null) emission = Color.white;
            return PbrMaterial(name, maps, tint, smoothness, emission);
        }

        /// <summary>Marks a material as laid out for metre UVs (so ArtPass gives its objects the metre-UV meshes).</summary>
        static Material MetreUV(Material m)
        {
            if (m != null) metreMats.Add(m);
            return m;
        }

        // ================================================================== mesh kit

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static Mesh builtinCube, builtinCylinder, builtinQuad;

        static string Cm(float metres) => Mathf.RoundToInt(Mathf.Abs(metres) * 100f).ToString();
        static float RoundCm(float metres) => Mathf.Max(0.01f, Mathf.Round(Mathf.Abs(metres) * 100f) / 100f);

        static Mesh ToMesh(MeshData d, string name)
        {
            var mesh = new Mesh { name = name };
            if (d.vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(d.vertices);
            mesh.SetNormals(d.normals);
            mesh.SetTangents(d.tangents);
            mesh.SetUVs(0, d.uvs);
            mesh.SetTriangles(d.triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// One shared mesh asset per key (saved in Generated/Meshes so scenes and prefabs can use it). Each build
        /// regenerates it into the same asset, so changes to the mesh kit always show and references stay valid.
        /// </summary>
        static Mesh CachedMesh(string key, System.Func<MeshData> make)
        {
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var fresh = ToMesh(make(), key);
            string path = MeshFolder + "/" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = key;
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
                meshCache[key] = existing;
                return existing;
            }
            EnsureFolder(MeshFolder);
            AssetDatabase.CreateAsset(fresh, path);
            meshCache[key] = fresh;
            return fresh;
        }

        /// <summary>
        /// A bevelled box mesh for an object whose scale is `size` (metres): the mesh is a unit cube like Unity's, so
        /// the transform keeps its real size (Breakables rely on that), with rounded edges of `bevel` metres (at most
        /// 20% of the smallest side) and metre UVs. Cached by size to the centimetre, so equal boxes share a mesh.
        /// </summary>
        static Mesh BoxMesh(Vector3 size, float bevel = 0.02f)
        {
            size = new Vector3(RoundCm(size.x), RoundCm(size.y), RoundCm(size.z));
            float b = Mathf.Round(MeshKit.ClampBevel(size, bevel) * 1000f) / 1000f;
            string key = "Box_" + Cm(size.x) + "x" + Cm(size.y) + "x" + Cm(size.z) + "_b" + Mathf.RoundToInt(b * 1000f);
            return CachedMesh(key, () => MeshKit.Box(size, b));
        }

        /// <summary>
        /// A smooth cylinder mesh like Unity's (diameter 1, height 2) for an object of `size` = (diameter, HEIGHT,
        /// diameter) metres, i.e. scale (x, height / 2, z). Rounded rims, metre UVs. segments 0 = pick by size (24-40).
        /// </summary>
        static Mesh CylinderMesh(Vector3 size, int segments = 0, float bevel = 0.015f)
        {
            size = new Vector3(RoundCm(size.x), RoundCm(size.y), RoundCm(size.z));
            if (segments < 3) segments = MeshKit.CylinderSegments(size);
            float b = Mathf.Round(Mathf.Clamp(bevel, 0f, 0.2f * Mathf.Min(Mathf.Min(size.x, size.z), size.y)) * 1000f) / 1000f;
            string key = "Cyl_" + Cm(size.x) + "x" + Cm(size.y) + "x" + Cm(size.z) + "_s" + segments + "_b" + Mathf.RoundToInt(b * 1000f);
            return CachedMesh(key, () => MeshKit.Cylinder(size, segments, b));
        }

        /// <summary>A quad like Unity's (facing -Z) with metre UVs, for an object scaled to `size` metres.</summary>
        static Mesh QuadMesh(Vector2 size)
        {
            var s = new Vector2(RoundCm(size.x), RoundCm(size.y));
            return CachedMesh("Quad_" + Cm(s.x) + "x" + Cm(s.y), () => MeshKit.Quad(s));
        }

        /// <summary>
        /// A chunky faceted boulder (~320 triangles) filling a unit box, for an object scaled to `size` metres
        /// (UVs in metres of that size). Different seeds give different rocks.
        /// </summary>
        static Mesh RockMesh(int seed, Vector3 size, int detail = 2)
        {
            var s = new Vector3(Mathf.Max(0.1f, Mathf.Round(Mathf.Abs(size.x) * 10f) / 10f), Mathf.Max(0.1f, Mathf.Round(Mathf.Abs(size.y) * 10f) / 10f), Mathf.Max(0.1f, Mathf.Round(Mathf.Abs(size.z) * 10f) / 10f));
            string key = "Rock_" + seed + "_d" + detail + "_" + Cm(s.x) + "x" + Cm(s.y) + "x" + Cm(s.z);
            return CachedMesh(key, () => MeshKit.Rock(seed, detail, s));
        }

        static void FindBuiltinMeshes()
        {
            if (builtinCube != null && builtinCylinder != null && builtinQuad != null) return;
            Mesh Get(PrimitiveType type)
            {
                var go = GameObject.CreatePrimitive(type);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(go);
                return mesh;
            }
            builtinCube = Get(PrimitiveType.Cube);
            builtinCylinder = Get(PrimitiveType.Cylinder);
            builtinQuad = Get(PrimitiveType.Quad);
        }

        /// <summary>Real size in metres along each of the object's own axes (works with rotated and scaled parents).</summary>
        static Vector3 WorldSize(Transform t)
        {
            var m = t.localToWorldMatrix;
            return new Vector3(((Vector3)m.GetColumn(0)).magnitude, ((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude);
        }

        static readonly HashSet<GameObject> keepPrimitive = new HashSet<GameObject>();

        /// <summary>Tells ArtPass to leave this object's primitive mesh alone.</summary>
        static GameObject KeepPrimitive(GameObject go)
        {
            if (go != null) keepPrimitive.Add(go);
            return go;
        }

        /// <summary>True if every material on the renderer works with metre UVs (made by Pbr, or untextured).</summary>
        static bool MetreReady(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || metreMats.Contains(m)) continue;
                if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return false;
            }
            return true;
        }

        /// <summary>
        /// The art pass, run at the end of each area builder: every Unity Cube / Cylinder / Quad render mesh under
        /// `root` is swapped for the mesh kit's version at its real size (bevelled edges, smooth sides, metre UVs, so
        /// the PBR materials tile at the right scale). Colliders are never touched (a MeshCollider keeps Unity's mesh).
        /// Skipped: text, particles, objects marked with KeepPrimitive, and objects whose material has a texture laid
        /// out for 0..1 UVs (anything not made by Pbr). Then big flat things (floors, ground, ceilings, decals) stop
        /// casting shadows. `bevel` is the edge rounding in metres.
        /// </summary>
        static void ArtPass(Transform root, float bevel = 0.02f)
        {
            if (root == null) return;
            FindBuiltinMeshes();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || (mesh != builtinCube && mesh != builtinCylinder && mesh != builtinQuad)) continue;
                var go = mf.gameObject;
                if (keepPrimitive.Contains(go) || go.GetComponent<TMP_Text>() != null || go.GetComponent<ParticleSystem>() != null) continue;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null || !MetreReady(mr)) continue;
                Vector3 size = WorldSize(mf.transform);
                if (size.x < 1e-4f || size.y < 1e-4f || size.z < 1e-4f) continue;
                if (mesh == builtinCube) mf.sharedMesh = BoxMesh(size, bevel);
                else if (mesh == builtinCylinder) mf.sharedMesh = CylinderMesh(new Vector3(size.x, size.y * 2f, size.z), 0, Mathf.Min(bevel, 0.015f));
                else mf.sharedMesh = QuadMesh(new Vector2(size.x, size.y));
            }
            AutoShadows(root);
        }

        /// <summary>Stops all renderers under `go` casting shadows (floors, ceilings, decals, glowing strips).</summary>
        static GameObject NoShadow(GameObject go)
        {
            if (go == null) return null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// Big flat horizontal things (floors, ground slabs, ceilings, roads: 16 m or more across) and paper-thin
        /// decals don't cast shadows: it costs shadow-map time and adds nothing (a ceiling would even put the hall in shade).
        /// </summary>
        static void AutoShadows(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue; // inactive renderers have empty bounds
                var s = r.bounds.size;
                float across = Mathf.Max(s.x, s.z);
                if (s.y < 0.03f || (across >= 16f && s.y <= Mathf.Max(0.6f, 0.05f * across)))
                    r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// A decorative bevelled box (no collider unless asked): `size` metres becomes its scale, like Prim().
        /// The mesh is made for its real size, so give it its final parent here.
        /// </summary>
        static GameObject Block(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, Quaternion? rot = null, float bevel = 0.02f, bool collider = false)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(WorldSize(go.transform), bevel);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>
        /// A decorative cylinder `diameter` wide and `height` tall (scale = diameter, height / 2, diameter, like
        /// Unity's), standing on its axis (rotate it to lay it down). Collider (convex, Unity's shape) only if asked.
        /// </summary>
        static GameObject Cyl(string name, Transform parent, Vector3 localPos, float diameter, float height, Material mat, Quaternion? rot = null, float bevel = 0.01f, bool collider = false)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            var size = WorldSize(go.transform);
            go.AddComponent<MeshFilter>().sharedMesh = CylinderMesh(new Vector3(size.x, size.y * 2f, size.z), 0, bevel);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider)
            {
                FindBuiltinMeshes();
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = builtinCylinder;
                mc.convex = true;
            }
            return go;
        }

        /// <summary>A decorative boulder filling `size` metres (scale = size). No collider unless asked (then a box).</summary>
        static GameObject Rock(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, int seed, Quaternion? rot = null, bool collider = false)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = RockMesh(seed, WorldSize(go.transform));
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = Vector3.one * 0.9f;
            return go;
        }

        /// <summary>Deletes generated mesh assets nothing in this build used (left over from older builds).</summary>
        static void CleanUnusedMeshes()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) return;
            var used = new HashSet<string>();
            foreach (var m in meshCache.Values) if (m != null) used.Add(AssetDatabase.GetAssetPath(m));
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!used.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        // ================================================================== lighting: sky, reflections, probes

        /// <summary>The scene's Skybox/Procedural material (also what keeps that shader in builds for MapSky).</summary>
        static Material SkyMaterial()
        {
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null) return null;
            string path = Root + "/Materials/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, path);
            }
            if (sky.shader != skyShader) sky.shader = skyShader;
            return sky;
        }

        /// <summary>The soft gradient reflection cubemap of a place, saved as an asset (fallback reflections).</summary>
        static Cubemap ReflectionCubemap(LightingPreset p)
        {
            EnsureFolder(ProbeFolder);
            string path = ProbeFolder + "/Env_" + p.name + ".cubemap";
            var fresh = LightingPresets.MakeReflectionCubemap(p, 64);
            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(fresh, existing);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        /// <summary>Applies a place's lighting preset to the open scene (sky asset, sun, ambient, fog, reflections).</summary>
        static void ApplyPreset(LightingPreset p)
        {
            var sky = SkyMaterial();
            LightingPresets.Apply(p, sky, ReflectionCubemap(p));
            if (sky != null) EditorUtility.SetDirty(sky);
        }

        struct ProbeJob
        {
            public ReflectionProbe probe;
            public LightingPreset preset;
        }

        static readonly List<ProbeJob> probes = new List<ProbeJob>();

        /// <summary>The lighting preset of the place an object is in (by its top-level group).</summary>
        static LightingPreset PresetFor(Transform t)
        {
            var root = t != null ? t.root : null;
            string n = root != null ? root.name : "";
            if (n.Contains("Menu")) return LightingPresets.Menu;
            if (n.Contains("Battleground")) return LightingPresets.Battleground;
            return LightingPresets.Training;
        }

        /// <summary>
        /// A box-projected reflection probe covering `size` metres around `center` (local to `parent`): glossy floors,
        /// metal and glass inside reflect the room instead of the sky. Baked by BuildWorld with the lighting of its
        /// place (worked out from the parent unless `preset` is given). Make the box match the room's walls.
        /// </summary>
        static ReflectionProbe Probe(string name, Transform parent, Vector3 center, Vector3 size, int resolution = 128, LightingPreset preset = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var p = go.AddComponent<ReflectionProbe>();
            // Custom (baked here, by BuildWorld) keeps its cubemap in the scene without a lighting bake.
            p.mode = ReflectionProbeMode.Custom;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.boxProjection = true;
            p.size = size;
            p.center = Vector3.zero;
            p.resolution = resolution;
            p.hdr = false;
            p.importance = 1;
            p.blendDistance = 1f;
            p.intensity = 1f;
            p.renderDynamicObjects = true; // breakables (not static) show up in the bake too
            p.nearClipPlane = 0.1f;
            p.farClipPlane = Mathf.Max(50f, size.magnitude * 1.5f);
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            probes.Add(new ProbeJob { probe = p, preset = preset ?? PresetFor(parent) });
            return p;
        }

        /// <summary>
        /// Bakes every Probe() into Generated/Probes, each with its own place's lighting, then puts the Training
        /// lighting back. A probe that can't be baked falls back to its place's gradient cubemap.
        /// </summary>
        static void BakeProbes()
        {
            EnsureFolder(ProbeFolder);
            var used = new HashSet<string>();
            foreach (var job in probes)
            {
                if (job.probe == null) continue;
                string baseName = job.probe.name;
                string name = baseName;
                for (int i = 2; used.Contains(name); i++) name = baseName + i;
                used.Add(name);
                ApplyPreset(job.preset);
                string path = ProbeFolder + "/" + name + (job.probe.hdr ? ".exr" : ".png");
                Texture baked = null;
                try
                {
                    if (Lightmapping.BakeReflectionProbe(job.probe, path))
                        baked = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Nullbound] Couldn't bake reflection probe " + name + ": " + e.Message);
                }
                job.probe.customBakedTexture = baked != null ? baked : ReflectionCubemap(job.preset);
                EditorUtility.SetDirty(job.probe);
            }
            ApplyPreset(LightingPresets.Training);
        }

        // ================================================================== shader variants

        /// <summary>
        /// The fighters, UI and effects make Standard materials at runtime with keyword combinations the scene's own
        /// materials might not use, and the build only keeps the variants some material in it uses. This inactive
        /// object holds one hidden renderer per combination so they all ship.
        /// </summary>
        static void BuildVariantKeeper()
        {
            EnsureFolder(Root + "/Materials/Variants");
            var keeper = new GameObject("ShaderVariantKeeper");
            string[][] opaque =
            {
                new string[0], new[] { "_EMISSION" }, new[] { "_NORMALMAP" }, new[] { "_NORMALMAP", "_EMISSION" },
                new[] { "_NORMALMAP", "_METALLICGLOSSMAP" }, new[] { "_METALLICGLOSSMAP" }, new[] { "_METALLICGLOSSMAP", "_EMISSION" },
                new[] { "_NORMALMAP", "_METALLICGLOSSMAP", "_EMISSION" },
            };
            string[][] fade = { new string[0], new[] { "_EMISSION" }, new[] { "_NORMALMAP" } };
            var normal = gunmetal != null ? gunmetal.GetTexture("_BumpMap") : null;
            var mask = gunmetal != null ? gunmetal.GetTexture("_MetallicGlossMap") : null;
            void Keep(string name, string[] keywords, int mode) // mode: 0 opaque, 1 cutout, 2 fade, 3 transparent
            {
                var m = LoadOrCreateMat("Variants/" + name);
                ResetStandard(m);
                // Give each keyword its map too, so Unity's material inspector keeps the keyword if someone opens it.
                foreach (var k in keywords)
                {
                    if (k == "_NORMALMAP" && normal != null) m.SetTexture("_BumpMap", normal);
                    if (k == "_METALLICGLOSSMAP" && mask != null) m.SetTexture("_MetallicGlossMap", mask);
                    if (k == "_EMISSION") { m.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f)); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
                    m.EnableKeyword(k);
                }
                if (mode == 2) Fade(m);
                else if (mode == 1)
                {
                    m.SetFloat("_Mode", 1f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = 2450;
                }
                else if (mode == 3)
                {
                    m.SetFloat("_Mode", 3f);
                    m.SetInt("_SrcBlend", (int)BlendMode.One);
                    m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = 3000;
                }
                EditorUtility.SetDirty(m);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(keeper.transform, false);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.enabled = false;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < opaque.Length; i++) Keep("Keep" + string.Join("", opaque[i]).Replace("_", "") + (opaque[i].Length == 0 ? "Plain" : ""), opaque[i], 0);
            for (int i = 0; i < fade.Length; i++) Keep("KeepFade" + string.Join("", fade[i]).Replace("_", ""), fade[i], 2);
            Keep("KeepCutout", new string[0], 1);
            Keep("KeepTransparent", new string[0], 3);
            keeper.SetActive(false);
        }
    }

    /// <summary>Import settings for the generated texture maps (applied when they're first imported or rewritten).</summary>
    class GeneratedTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArenaBuilder.GenTextureFolder + "/")) return;
            var imp = assetImporter as TextureImporter;
            if (imp == null) return;
            bool rewritten = ArenaBuilder.pendingImport.TryGetValue(assetPath, out var settings);
            if (!rewritten && !imp.importSettingsMissing) return; // keep the settings it already has
            ArenaBuilder.ConfigureGenTexture(imp, assetPath, rewritten ? settings.x : 4, rewritten ? settings.y : 1024);
        }
    }
}
