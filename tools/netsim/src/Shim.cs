// Minimal managed re-implementation of the UnityEngine API surface used by the procedural builders,
// so the game's real model/map generation code can run outside Unity for preview renders.
using System; using System.Collections; using System.Collections.Generic;
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16, UInt32 } public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly } public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor, SrcAlpha, OneMinusSrcColor, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate, OneMinusSrcAlpha } public enum CompareFunction { Disabled, Never, Less, Equal, LessEqual, Greater, NotEqual, GreaterEqual, Always } }
namespace UnityEngine {
public static class Mathf {
 public const float PI = (float)Math.PI, Deg2Rad = PI / 180f, Rad2Deg = 180f / PI, Infinity = float.PositiveInfinity;
 public static float Sin(float f) => (float)Math.Sin(f); public static float Cos(float f) => (float)Math.Cos(f); public static float Tan(float f)=>(float)Math.Tan(f);
 public static float Atan2(float y, float x) => (float)Math.Atan2(y, x); public static float Atan(float x)=>(float)Math.Atan(x); public static float Acos(float x)=>(float)Math.Acos(x);
 public static float Sqrt(float f) => (float)Math.Sqrt(f); public static float Abs(float f) => Math.Abs(f); public static int Abs(int f) => Math.Abs(f);
 public static float Min(float a, float b) => a < b ? a : b; public static float Max(float a, float b) => a > b ? a : b; public static int Min(int a, int b)=>a<b?a:b; public static int Max(int a,int b)=>a>b?a:b; public static float Max(params float[] v){float m=v[0];foreach(var x in v)if(x>m)m=x;return m;}
 public static float Pow(float a, float b) => (float)Math.Pow(a, b); public static float Exp(float a) => (float)Math.Exp(a); public static float Log(float a)=>(float)Math.Log(a);
 public static float Floor(float f) => (float)Math.Floor(f); public static float Ceil(float f)=>(float)Math.Ceiling(f); public static float Round(float f) => (float)Math.Round(f);
 public static int FloorToInt(float f) => (int)Math.Floor(f); public static int CeilToInt(float f) => (int)Math.Ceiling(f); public static int RoundToInt(float f) => (int)Math.Round(f);
 public static float Sign(float f) => f >= 0 ? 1 : -1;
 public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v; public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v; public static float Clamp01(float v) => Clamp(v, 0, 1);
 public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t); public static float LerpUnclamped(float a,float b,float t)=>a+(b-a)*t;
 public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0;
 public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
 public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
 public static float Repeat(float t, float l) => Clamp(t - Floor(t / l) * l, 0, l);
 public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
 public static float MoveTowardsAngle(float c, float t, float m) { float d = DeltaAngle(c, t); if (-m < d && d < m) return t; t = c + d; return MoveTowards(c, t, m); }
 public static float LerpAngle(float a, float b, float t) { float d = Repeat(b - a, 360); if (d > 180) d -= 360; return a + d * Clamp01(t); }
 public static float PerlinNoise(float x, float y) => 0.5f;
}
public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
 public static Vector2 zero => new Vector2(0, 0); public static Vector2 one => new Vector2(1, 1);
 public float magnitude => Mathf.Sqrt(x * x + y * y); public float sqrMagnitude => x * x + y * y; public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
 public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y); public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
 public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y); public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d); public static Vector2 operator *(float d, Vector2 a) => a * d; public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
 public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y; public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude; public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
 public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0); public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y); }
public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } }
public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
 public static Vector3 zero => new Vector3(0, 0, 0); public static Vector3 one => new Vector3(1, 1, 1); public static Vector3 up => new Vector3(0, 1, 0); public static Vector3 down => new Vector3(0, -1, 0);
 public static Vector3 forward => new Vector3(0, 0, 1); public static Vector3 back => new Vector3(0, 0, -1); public static Vector3 right => new Vector3(1, 0, 0); public static Vector3 left => new Vector3(-1, 0, 0);
 public float magnitude => Mathf.Sqrt(x * x + y * y + z * z); public float sqrMagnitude => x * x + y * y + z * z;
 public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } } public void Normalize() { this = normalized; }
 public float this[int i] { get => i == 0 ? x : i == 1 ? y : z; set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; } }
 public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
 public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z); public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d); public static Vector3 operator *(float d, Vector3 a) => a * d; public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
 public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f; public static bool operator !=(Vector3 a, Vector3 b) => !(a == b); public override bool Equals(object o) => o is Vector3 v && v == this; public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
 public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z; public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
 public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
 public static Vector3 Slerp(Vector3 a, Vector3 b, float t) { float la = a.magnitude, lb = b.magnitude; var d = Lerp(a.normalized, b.normalized, t).normalized; if (d.sqrMagnitude < 1e-6f) d = Lerp(a, b, t).normalized; return d * Mathf.Lerp(la, lb, t); }
 public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float s = Dot(n, n); if (s < 1e-10f) return v; return v - n * (Dot(v, n) / s); }
 public static Vector3 Project(Vector3 v, Vector3 n) { float s = Dot(n, n); return s < 1e-10f ? zero : n * (Dot(v, n) / s); }
 public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)); public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
 public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d) { var v = t - c; float m = v.magnitude; return m <= d || m == 0 ? t : c + v / m * d; }
 public static Vector3 ClampMagnitude(Vector3 v, float m) => v.magnitude > m ? v.normalized * m : v; public static float Angle(Vector3 a, Vector3 b) { float d = Mathf.Clamp(Dot(a.normalized, b.normalized), -1, 1); return Mathf.Acos(d) * Mathf.Rad2Deg; }
 public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})"; }
public struct Quaternion { public float x, y, z, w; public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
 public static Quaternion identity => new Quaternion(0, 0, 0, 1);
 public static Quaternion operator *(Quaternion l, Quaternion r) => new Quaternion(l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y, l.w * r.y + l.y * r.w + l.z * r.x - l.x * r.z, l.w * r.z + l.z * r.w + l.x * r.y - l.y * r.x, l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z);
 public static Vector3 operator *(Quaternion q, Vector3 v) { var u = new Vector3(q.x, q.y, q.z); var t = Vector3.Cross(u, v) * 2f; return v + t * q.w + Vector3.Cross(u, t); }
 public static Quaternion AngleAxis(float deg, Vector3 axis) { axis = axis.normalized; float h = deg * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h); return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h)); }
 public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
 public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);
 public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
 public Quaternion normalized { get { float m = Mathf.Sqrt(x * x + y * y + z * z + w * w); return m < 1e-8f ? identity : new Quaternion(x / m, y / m, z / m, w / m); } }
 public static Quaternion LookRotation(Vector3 f, Vector3 up) { f = f.normalized; if (f.sqrMagnitude < 1e-8f) return identity; var r = Vector3.Cross(up, f); if (r.sqrMagnitude < 1e-8f) r = Vector3.Cross(Mathf.Abs(f.y) < 0.99f ? Vector3.up : Vector3.right, f); r = r.normalized; var u = Vector3.Cross(f, r); return FromBasis(r, u, f); }
 public static Quaternion LookRotation(Vector3 f) => LookRotation(f, Vector3.up);
 static Quaternion FromBasis(Vector3 r, Vector3 u, Vector3 f) { float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z; float tr = m00 + m11 + m22; Quaternion q;
  if (tr > 0) { float s = Mathf.Sqrt(tr + 1f) * 2; q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
  else if (m00 > m11 && m00 > m22) { float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2; q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
  else if (m11 > m22) { float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2; q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
  else { float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2; q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); } return q.normalized; }
 public static Quaternion FromToRotation(Vector3 a, Vector3 b) { a = a.normalized; b = b.normalized; float d = Vector3.Dot(a, b); if (d > 0.99999f) return identity; if (d < -0.99999f) { var ax = Vector3.Cross(Vector3.right, a); if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(Vector3.up, a); return AngleAxis(180, ax); } var c = Vector3.Cross(a, b); return new Quaternion(c.x, c.y, c.z, 1 + d).normalized; }
 public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
 public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { t = Mathf.Clamp01(t); float d = Dot(a, b); if (d < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; } if (d > 0.9995f) return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized; float th = (float)Math.Acos(d), s = Mathf.Sin(th); float wa = Mathf.Sin((1 - t) * th) / s, wb = Mathf.Sin(t * th) / s; return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb); }
 public Vector3 eulerAngles { get { var f = this * Vector3.forward; var u = this * Vector3.up; float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; float pitch = -Mathf.Atan2(f.y, Mathf.Sqrt(f.x * f.x + f.z * f.z)) * Mathf.Rad2Deg; var r0 = Inverse(Euler(pitch, yaw, 0)) * u; float roll = Mathf.Atan2(-r0.x, r0.y) * Mathf.Rad2Deg; return new Vector3((pitch + 360) % 360, (yaw + 360) % 360, (roll + 360) % 360); } }
}
public struct Matrix4x4 { public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
 public static Matrix4x4 identity => new Matrix4x4 { m00 = 1, m11 = 1, m22 = 1, m33 = 1 };
 public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) { var r = q * Vector3.right * s.x; var u = q * Vector3.up * s.y; var f = q * Vector3.forward * s.z; return new Matrix4x4 { m00 = r.x, m10 = r.y, m20 = r.z, m01 = u.x, m11 = u.y, m21 = u.z, m02 = f.x, m12 = f.y, m22 = f.z, m03 = p.x, m13 = p.y, m23 = p.z, m33 = 1 }; }
 public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) { var r = new Matrix4x4(); var A = a.Arr(); var B = b.Arr(); var R = new float[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += A[i * 4 + k] * B[k * 4 + j]; R[i * 4 + j] = s; } r.Set(R); return r; }
 float[] Arr() => new[] { m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33 };
 void Set(float[] R) { m00 = R[0]; m01 = R[1]; m02 = R[2]; m03 = R[3]; m10 = R[4]; m11 = R[5]; m12 = R[6]; m13 = R[7]; m20 = R[8]; m21 = R[9]; m22 = R[10]; m23 = R[11]; m30 = R[12]; m31 = R[13]; m32 = R[14]; m33 = R[15]; }
 public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(m00 * p.x + m01 * p.y + m02 * p.z + m03, m10 * p.x + m11 * p.y + m12 * p.z + m13, m20 * p.x + m21 * p.y + m22 * p.z + m23);
 public Vector3 MultiplyVector(Vector3 v) => new Vector3(m00 * v.x + m01 * v.y + m02 * v.z, m10 * v.x + m11 * v.y + m12 * v.z, m20 * v.x + m21 * v.y + m22 * v.z); }
public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
 public static Color white => new Color(1, 1, 1); public static Color black => new Color(0, 0, 0); public static Color gray => new Color(0.5f, 0.5f, 0.5f); public static Color grey => gray; public static Color red => new Color(1, 0, 0); public static Color clear => new Color(0,0,0,0);
 public static Color operator *(Color c, float d) => new Color(c.r * d, c.g * d, c.b * d, c.a * d); public static Color operator *(float d, Color c) => c * d; public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
 public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
 public static implicit operator Color32(Color c) => new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255), 0, 255)); }
public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
public struct Bounds { public Vector3 center, size; public Bounds(Vector3 c, Vector3 s) { center = c; size = s; } public Vector3 extents => size * 0.5f; public Vector3 min => center - extents; public Vector3 max => center + extents; public bool Contains(Vector3 p) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z; }
public struct Pose { public Vector3 position; public Quaternion rotation; public Pose(Vector3 p, Quaternion r) { position = p; rotation = r; } }
public class Object { public string name; public static void Destroy(Object o) { if (o is GameObject g) g.destroyed = true; } public static void DestroyImmediate(Object o) => Destroy(o); public static void DontDestroyOnLoad(Object o) { } public static implicit operator bool(Object o) => o != null; }
public class Shader : Object { public static Shader Find(string n) => new Shader { name = n }; }
public enum TextureFormat { RGBA32, ARGB32 } public enum TextureWrapMode { Repeat, Clamp } public enum FilterMode { Point, Bilinear, Trilinear }
public class Texture : Object { public int width, height; public TextureWrapMode wrapMode; public FilterMode filterMode; public int anisoLevel; }
public class Texture2D : Texture { public static readonly List<Texture2D> All = new List<Texture2D>(); public Color32[] px; public bool linear;
 public Texture2D(int w, int h, TextureFormat f, bool mips, bool linear) { width = w; height = h; this.linear = linear; All.Add(this); }
 public void SetPixels32(Color32[] p) { px = (Color32[])p.Clone(); } public void Apply(bool a = true, bool b = false) { }
 static Texture2D _white; public static Texture2D whiteTexture { get { if (_white == null) { _white = new Texture2D(4, 4, TextureFormat.RGBA32, false, false) { name = "white" }; var p = new Color32[16]; for (int i = 0; i < 16; i++) p[i] = new Color32(255, 255, 255, 255); _white.px = p; } return _white; } } }
public class Material : Object { public Dictionary<string, Color> colors = new Dictionary<string, Color>(); public Dictionary<string, float> floats = new Dictionary<string, float>(); public Dictionary<string, Texture> texs = new Dictionary<string, Texture>(); public Dictionary<string, Vector4> vecs = new Dictionary<string, Vector4>(); public Shader shader; public int renderQueue; public bool enableInstancing;
 public Material(Shader s) { shader = s; name = s?.name; } public Material(Material m) { shader = m.shader; name = m.name; colors = new Dictionary<string, Color>(m.colors); floats = new Dictionary<string, float>(m.floats); texs = new Dictionary<string, Texture>(m.texs); vecs = new Dictionary<string, Vector4>(m.vecs); renderQueue = m.renderQueue; }
 public void SetColor(string n, Color c) => colors[n] = c; public Color GetColor(string n) => colors.TryGetValue(n, out var c) ? c : Color.white; public void SetFloat(string n, float f) => floats[n] = f; public float GetFloat(string n) => floats.TryGetValue(n, out var f) ? f : 0; public void SetTexture(string n, Texture t) => texs[n] = t; public void SetVector(string n, Vector3 v) => vecs[n] = new Vector4(v.x, v.y, v.z, 0); public Color color { get => GetColor("_Color"); set => SetColor("_Color", value); } }
public static class Resources { public static T Load<T>(string p) where T : Object => null; }
public class Mesh : Object { public Vector3[] vertices, normals; public Vector2[] uv; public Color[] colors; public List<int[]> subs = new List<int[]>(); public Rendering.IndexFormat indexFormat; public Bounds bounds; public int subMeshCount { get => subs.Count; set { while (subs.Count < value) subs.Add(new int[0]); } }
 public void SetVertices(List<Vector3> v) => vertices = v.ToArray(); public void SetNormals(List<Vector3> n) => normals = n.ToArray(); public void SetUVs(int c, List<Vector2> u) => uv = u.ToArray(); public void SetColors(List<Color> c) => colors = c.ToArray();
 public void SetTriangles(List<int> t, int sub, bool calc = true) { while (subs.Count <= sub) subs.Add(new int[0]); subs[sub] = t.ToArray(); }
 public void RecalculateBounds() { if (vertices == null || vertices.Length == 0) return; var mn = vertices[0]; var mx = vertices[0]; foreach (var v in vertices) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); } bounds = new Bounds((mn + mx) * 0.5f, mx - mn); } public void RecalculateTangents() { } public void RecalculateNormals() { } }
public class Component : Object { public GameObject gameObject; public Transform transform => gameObject.transform; public T GetComponent<T>() where T : class => gameObject.GetComponent<T>(); public T[] GetComponentsInChildren<T>(bool inc = false) where T : class => gameObject.GetComponentsInChildren<T>(); public T GetComponentInParent<T>() where T : class { var t = transform; while (t != null) { var c = t.gameObject.GetComponent<T>(); if (c != null) return c; t = t.parent; } return null; } public string tag; }
public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled; }
public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator e) => null; public void StopCoroutine(Coroutine c) { } }
public class Coroutine { }
public class GameObject : Object { public readonly Transform transform; public readonly List<Component> comps = new List<Component>(); public bool isStatic, destroyed, active = true; public int layer; public string tag;
 public static readonly List<GameObject> All = new List<GameObject>();
 public GameObject() : this("GameObject") { } public GameObject(string n) { name = n; transform = new Transform(); transform.gameObject = this; comps.Add(transform); All.Add(this); }
 public T AddComponent<T>() where T : Component, new() { var c = new T(); c.gameObject = this; comps.Add(c); return c; }
 public Component AddComponent(Type t) { var c = (Component)Activator.CreateInstance(t); c.gameObject = this; comps.Add(c); return c; }
 public T GetComponent<T>() where T : class { foreach (var c in comps) if (c is T t) return t; return null; }
 public T[] GetComponentsInChildren<T>() where T : class { var l = new List<T>(); Collect(transform, l); return l.ToArray(); } static void Collect<T>(Transform t, List<T> l) where T : class { foreach (var c in t.gameObject.comps) if (c is T x) l.Add(x); foreach (var ch in t.children) Collect(ch, l); }
 public void SetActive(bool a) { active = a; } public bool activeSelf => active; public Scene scene => default; }
public struct Scene { public bool IsValid() => true; }
public class Transform : Component, IEnumerable { public Transform parent; public readonly List<Transform> children = new List<Transform>(); public Vector3 localPosition; public Quaternion localRotation = Quaternion.identity; public Vector3 localScale = Vector3.one;
 public IEnumerator GetEnumerator() => children.GetEnumerator(); public int childCount => children.Count; public Transform GetChild(int i) => children[i]; public Transform root { get { var t = this; while (t.parent != null) t = t.parent; return t; } }
 public Matrix4x4 localToWorldMatrix { get { var m = Matrix4x4.TRS(localPosition, localRotation, localScale); return parent != null ? parent.localToWorldMatrix * m : m; } }
 public Quaternion rotation { get => parent != null ? parent.rotation * localRotation : localRotation; set => localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * value : value; }
 public Vector3 lossyScale { get { var s = localScale; var p = parent; while (p != null) { s = Vector3.Scale(s, p.localScale); p = p.parent; } return s; } }
 public Vector3 position { get => parent != null ? parent.localToWorldMatrix.MultiplyPoint3x4(localPosition) : localPosition; set => localPosition = parent != null ? parent.InverseTransformPoint(value) : value; }
 public Vector3 InverseTransformPoint(Vector3 p) { var local = Quaternion.Inverse(rotation) * (p - position); var s = lossyScale; return new Vector3(local.x / s.x, local.y / s.y, local.z / s.z); }
 public Vector3 TransformPoint(Vector3 p) => localToWorldMatrix.MultiplyPoint3x4(p); public Vector3 TransformDirection(Vector3 d) => rotation * d; public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
 public Vector3 forward => rotation * Vector3.forward; public Vector3 up => rotation * Vector3.up; public Vector3 right => rotation * Vector3.right; public Vector3 eulerAngles { get => rotation.eulerAngles; set => rotation = Quaternion.Euler(value); }
 public void SetParent(Transform p, bool worldStays = true) { var wp = position; var wr = rotation; if (parent != null) parent.children.Remove(this); parent = p; if (p != null) p.children.Add(this); if (worldStays) { position = wp; rotation = wr; } } public void SetParent(Transform p) => SetParent(p, true);
 public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
 public Transform Find(string n) { foreach (var c in children) if (c.gameObject.name == n) return c; return null; } public bool IsChildOf(Transform t) { var x = this; while (x != null) { if (x == t) return true; x = x.parent; } return false; } }
public class MeshFilter : Component { public Mesh sharedMesh; public Mesh mesh { get => sharedMesh; set => sharedMesh = value; } }
public class Renderer : Component { public Material[] sharedMaterials = new Material[0]; public Material sharedMaterial { get => sharedMaterials.Length > 0 ? sharedMaterials[0] : null; set => sharedMaterials = new[] { value }; } public Material material { get => sharedMaterial; set => sharedMaterial = value; } public Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows = true; public bool enabled = true; }
public class MeshRenderer : Renderer { }
public class Collider : Component { public bool isTrigger, enabled = true; public Rigidbody attachedRigidbody => null; }
public class BoxCollider : Collider { public Vector3 center, size = Vector3.one; } public class SphereCollider : Collider { public Vector3 center; public float radius; }
public class CapsuleCollider : Collider { public Vector3 center; public float radius, height; public int direction; } public class MeshCollider : Collider { public Mesh sharedMesh;
 Vector3[] _w; System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<int>> _grid; System.Collections.Generic.List<int> _tris; const float G = 8f;
 static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
 void Build() { var m = sharedMesh; var t = gameObject.transform; _w = new Vector3[m.vertices.Length]; for (int i = 0; i < _w.Length; i++) _w[i] = t.TransformPoint(m.vertices[i]);
  _tris = new System.Collections.Generic.List<int>(); foreach (var sub in m.subs) _tris.AddRange(sub); _grid = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<int>>();
  for (int i = 0; i + 2 < _tris.Count; i += 3) { var a = _w[_tris[i]]; var b = _w[_tris[i + 1]]; var c = _w[_tris[i + 2]];
   int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / G), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / G), z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / G), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / G);
   for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) { var k = Key(x, z); if (!_grid.TryGetValue(k, out var l)) _grid[k] = l = new System.Collections.Generic.List<int>(); l.Add(i); } } }
 bool Tri(int i, Ray r, ref float best, ref RaycastHit h) { var a = _w[_tris[i]]; var b = _w[_tris[i + 1]]; var c = _w[_tris[i + 2]];
   var e1 = b - a; var e2 = c - a; var pv = Vector3.Cross(r.direction, e2); float det = Vector3.Dot(e1, pv); if (System.Math.Abs(det) < 1e-9f) return false; float id = 1f / det;
   var tv = r.origin - a; float u = Vector3.Dot(tv, pv) * id; if (u < 0 || u > 1) return false; var qv = Vector3.Cross(tv, e1); float v = Vector3.Dot(r.direction, qv) * id; if (v < 0 || u + v > 1) return false;
   float dd = Vector3.Dot(e2, qv) * id; if (dd < 0 || dd > best) return false; best = dd; var n = Vector3.Cross(e1, e2).normalized; if (n.y < 0) n = -n; h = new RaycastHit { point = r.origin + r.direction * dd, normal = n, distance = dd }; return true; }
 public bool Raycast(Ray r, out RaycastHit h, float max) { h = default; var m = sharedMesh; if (m == null || m.vertices == null) return false; if (_w == null) Build(); float best = max; bool hit = false; r.direction = r.direction.normalized;
  if (System.Math.Abs(r.direction.x) < 0.01f && System.Math.Abs(r.direction.z) < 0.01f) { if (_grid.TryGetValue(Key(Mathf.FloorToInt(r.origin.x / G), Mathf.FloorToInt(r.origin.z / G)), out var l)) foreach (var i in l) hit |= Tri(i, r, ref best, ref h); }
  else for (int i = 0; i + 2 < _tris.Count; i += 3) hit |= Tri(i, r, ref best, ref h);
  return hit; } }
public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative } public enum RigidbodyInterpolation { None, Interpolate, Extrapolate } public enum ForceMode { Force, Impulse, VelocityChange, Acceleration }
public class Rigidbody : Component { public float mass = 1; public bool isKinematic, useGravity = true; public CollisionDetectionMode collisionDetectionMode; public RigidbodyInterpolation interpolation; public Vector3 angularVelocity, velocity, worldCenterOfMass; public float maxAngularVelocity, drag, angularDrag;
 public void AddForceAtPosition(Vector3 f, Vector3 p, ForceMode m = ForceMode.Force) { } public void AddForce(Vector3 f, ForceMode m = ForceMode.Force) { } }
public struct SoftJointLimit { public float limit; } public class Joint : Component { public Rigidbody connectedBody; public Vector3 axis, anchor; public bool enablePreprocessing; }
public class CharacterJoint : Joint { public Vector3 swingAxis; public SoftJointLimit lowTwistLimit, highTwistLimit, swing1Limit, swing2Limit; public bool enableProjection; }
public enum LightType { Directional, Point, Spot } public enum LightShadows { None, Hard, Soft } public enum LightRenderMode { Auto, ForcePixel, ForceVertex }
public class Light : Behaviour { public LightType type; public Color color; public float range, intensity, shadowStrength, shadowBias, shadowNormalBias; public LightShadows shadows; public LightRenderMode renderMode; }
public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight } public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
public class TextMesh : Component { public string text; public Color color; }
public static class Debug { public static void Log(object o) => Console.WriteLine(o); public static void LogWarning(object o) => Console.WriteLine("WARN " + o); }
public static class Random { static System.Random r = new System.Random(5); public static float value => (float)r.NextDouble(); public static float Range(float a, float b) => a + (b - a) * value; public static int Range(int a, int b) => r.Next(a, b); public static Vector3 insideUnitSphere => new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)) * 0.57f; }
}
namespace UnityEngine {
 public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
 public static class Physics {
  public static void SyncTransforms() { }
  // a real ray test against every box collider (and terrain meshes) in the scene, so map code that settles things
  // onto floors behaves like it does in the game
  static int _cacheN = -1, _calls; static readonly System.Collections.Generic.List<(GameObject, Component)> _cache = new System.Collections.Generic.List<(GameObject, Component)>();
  public static System.Collections.Generic.List<RaycastHit> AllHits(Vector3 o, Vector3 d, float max) {
   var res = new System.Collections.Generic.List<RaycastHit>(); d = d.normalized;
   if (_cacheN != GameObject.All.Count || ++_calls % 200 == 0) { _cacheN = GameObject.All.Count; _cache.Clear(); foreach (var g in GameObject.All) foreach (var cc in g.comps) if (cc is BoxCollider || (cc is MeshCollider && g.name == "Terrain")) _cache.Add((g, cc)); }
   foreach (var (go, c) in _cache) {
    {
     if (c is BoxCollider bc && bc.enabled && !bc.isTrigger) {
      var t = go.transform; var rot = t.rotation; var sc = t.lossyScale; var inv = Quaternion.Inverse(rot);
      var lo = inv * (o - t.position); lo = new Vector3(lo.x / sc.x, lo.y / sc.y, lo.z / sc.z); lo -= bc.center;
      var ld = inv * d; ld = new Vector3(ld.x / sc.x, ld.y / sc.y, ld.z / sc.z);
      var he = bc.size * 0.5f; float tmin = -1e9f, tmax = 1e9f; int ax = -1; float sgn = 0;
      bool miss = false;
      for (int i = 0; i < 3; i++) {
       float oi = i == 0 ? lo.x : i == 1 ? lo.y : lo.z, di = i == 0 ? ld.x : i == 1 ? ld.y : ld.z, hi = i == 0 ? he.x : i == 1 ? he.y : he.z;
       if (System.Math.Abs(di) < 1e-9f) { if (oi < -hi || oi > hi) { miss = true; break; } continue; }
       float t1 = (-hi - oi) / di, t2 = (hi - oi) / di; float s1 = -1; if (t1 > t2) { var tt = t1; t1 = t2; t2 = tt; s1 = 1; }
       if (t1 > tmin) { tmin = t1; ax = i; sgn = s1; } if (t2 < tmax) tmax = t2; if (tmin > tmax) { miss = true; break; }
      }
      if (miss || tmin < 0 || ax < 0) continue;
      var lp = lo + ld * tmin + bc.center; var wp = t.position + rot * new Vector3(lp.x * sc.x, lp.y * sc.y, lp.z * sc.z);
      float dist = (wp - o).magnitude; if (dist > max) continue;
      var ln = ax == 0 ? new Vector3(sgn, 0, 0) : ax == 1 ? new Vector3(0, sgn, 0) : new Vector3(0, 0, sgn);
      res.Add(new RaycastHit { point = wp, normal = (rot * ln).normalized, distance = dist, colliderObj = go });
     }
     else if (c is MeshCollider mc && mc.sharedMesh != null && mc.sharedMesh.vertices != null && go.name == "Terrain") {
      if (mc.Raycast(new Ray(o, d), out var mh, max)) { mh.colliderObj = go; res.Add(mh); }
     }
    }
   }
   res.Sort((x, y) => x.distance.CompareTo(y.distance)); return res;
  }
  public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float max, int mask, QueryTriggerInteraction q) { var l = AllHits(o, d, max); h = l.Count > 0 ? l[0] : default; return l.Count > 0; }
  public static bool CheckBox(Vector3 c, Vector3 he, Quaternion r, int mask, QueryTriggerInteraction q) => false;
 }
}
namespace UnityEngine { public struct RaycastHit { public Vector3 point, normal; public float distance; public GameObject colliderObj; } public struct Ray { public Vector3 origin, direction; public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; } } }
