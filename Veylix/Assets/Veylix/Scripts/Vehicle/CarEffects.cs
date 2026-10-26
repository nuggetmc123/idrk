using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Per-car visual effects driven by the physics: tyre smoke when sliding or locking up, dust and grass clippings off
    /// track, spray in the wet, plank sparks when the floor hits the ground, skid marks, glowing brake discs, the
    /// flashing rain light and headlights.
    /// </summary>
    public class CarEffects : MonoBehaviour
    {
        Car car;
        CarParts parts;
        ParticleSystem smoke, spray;
        float dustTimer, sparkTimer;
        readonly SkidTrail[] trails = new SkidTrail[4];
        MaterialPropertyBlock mpb;

        public void Init(Car c)
        {
            car = c;
            parts = c.parts;
            var A = VXAssets.I;
            smoke = Fx.MakeSystem("TyreSmoke", VXAssets.Or(A != null ? A.smoke : null, "smoke", new Color(0.8f, 0.8f, 0.8f, 0.5f)), 220, false, true, Fxroot());
            var m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            m.gravityModifier = -0.02f;
            Fx.Fade(smoke, new Color(0.85f, 0.85f, 0.86f, 0.5f), new Color(0.9f, 0.9f, 0.9f, 0.28f), new Color(1f, 1f, 1f, 0f));
            Fx.Grow(smoke, 0.5f, 3.2f);
            spray = Fx.MakeSystem("Spray", VXAssets.Or(A != null ? A.spray : null, "spray", new Color(0.85f, 0.88f, 0.9f, 0.4f)), 300, false, true, Fxroot());
            var sm = spray.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            sm.gravityModifier = 0.15f;
            Fx.Fade(spray, new Color(0.9f, 0.92f, 0.95f, 0.35f), new Color(0.9f, 0.92f, 0.95f, 0.18f), new Color(1f, 1f, 1f, 0f));
            Fx.Grow(spray, 0.6f, 2.8f);
            for (int i = 0; i < 4; i++) trails[i] = new SkidTrail(car.wheels[i].width * 0.85f);
            mpb = new MaterialPropertyBlock();
        }

        static Transform fxRoot;
        static Transform Fxroot()
        {
            if (fxRoot == null) { fxRoot = new GameObject("CarFx").transform; }
            return fxRoot;
        }

        void OnDestroy()
        {
            if (smoke != null) Destroy(smoke.gameObject);
            if (spray != null) Destroy(spray.gameObject);
            foreach (var t in trails) t?.Dispose();
        }

        void LateUpdate()
        {
            if (car == null || car.spec == null) return;
            float dt = Time.deltaTime;
            Vector3 v = car.Body != null ? car.Body.linearVelocity : Vector3.zero;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 4; i++)
            {
                var w = car.wheels[i];
                bool mark = false;
                if (w.grounded && !w.detached)
                {
                    float sx = Mathf.Abs(w.slipRatio) / car.spec.peakSlipRatio;
                    float sy = Mathf.Abs(Mathf.Tan(w.slipAngle)) / Mathf.Tan(car.spec.peakSlipAngle * Mathf.Deg2Rad);
                    float slide = Mathf.Clamp01(Mathf.Sqrt(sx * sx + sy * sy) - 1.3f);
                    bool tarmac = !Surface.IsOffTrack(w.surface);
                    if (tarmac && slide > 0.05f && Env.Wetness < 0.4f && car.SpeedMs > 2f)
                    {
                        if (Random.value < slide * dt * 40f)
                        {
                            ep.position = w.point + Vector3.up * 0.2f;
                            ep.velocity = v * 0.25f + Random.insideUnitSphere * 0.8f + Vector3.up * 0.5f;
                            ep.startColor = Color.white;
                            smoke.Emit(ep, 1);
                        }
                        mark = slide > 0.25f;
                    }
                    if (!tarmac && car.SpeedMs > 4f)
                    {
                        dustTimer -= dt;
                        if (dustTimer <= 0f)
                        {
                            dustTimer = 0.03f;
                            var info = Surface.Info(w.surface);
                            if (info.dusty) Fx.Dust(w.point, v, info.dust, Mathf.Clamp01(car.SpeedMs / 30f));
                        }
                        mark = true;
                    }
                    if (Env.Wetness > 0.15f && car.SpeedMs > 12f && !w.front && Random.value < Env.Wetness * dt * car.SpeedMs * 0.8f)
                    {
                        ep.position = w.point + Vector3.up * 0.3f - car.transform.forward * 0.4f;
                        ep.velocity = v * 0.55f + Random.insideUnitSphere * 1.2f + Vector3.up * 1.2f;
                        ep.startColor = Color.white;
                        spray.Emit(ep, 2);
                    }
                }
                trails[i].Update(mark, w.point + w.normal * 0.02f, w.normal, car.transform.right);
            }

            // Floor/plank sparks.
            if (car.Scrape > 0.1f && car.SpeedMs > 25f)
            {
                sparkTimer -= dt;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = 0.02f;
                    Vector3 p = car.transform.TransformPoint(new Vector3(Random.Range(-0.15f, 0.15f), 0.01f, Random.Range(-1.2f, 0.4f)));
                    Fx.Sparks(p, Vector3.up * 0.3f - car.transform.forward, car.Scrape, v * 0.9f);
                }
            }

            // Brake glow: discs go orange above ~550 C (very visible at night).
            if (parts != null && parts.discMaterial != null)
            {
                float t = 0f;
                foreach (var w in car.wheels) t = Mathf.Max(t, w.brakeTemp);
                float glow = Mathf.InverseLerp(500f, 1050f, t);
                parts.discMaterial.EnableKeyword("_EMISSION");
                parts.discMaterial.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.08f) * glow * glow * 3f);
            }
            // Rain light flashes in the wet, and at night.
            if (parts != null && parts.rainLight != null)
            {
                bool on = Env.Wetness > 0.25f || Env.Fog > 0.3f || Env.IsNight;
                float blink = on && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? 1f : 0.05f;
                parts.rainLight.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", new Color(1f, 0.03f, 0.02f) * blink * 3f);
                parts.rainLight.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>A skid mark strip that follows one tyre while it's sliding. Pooled vertices, faded by age.</summary>
    public class SkidTrail
    {
        const int Max = 160;
        readonly Vector3[] verts = new Vector3[Max * 2];
        readonly Vector3[] normals = new Vector3[Max * 2];
        readonly Vector2[] uvs = new Vector2[Max * 2];
        readonly Color[] colors = new Color[Max * 2];
        readonly int[] tris = new int[(Max - 1) * 6];
        readonly Mesh mesh;
        readonly GameObject go;
        readonly float width;
        int count;
        bool wasMarking;
        Vector3 last;
        readonly List<int> breaks = new List<int>();

        public SkidTrail(float width)
        {
            this.width = width;
            mesh = new Mesh { name = "skid" };
            mesh.MarkDynamic();
            go = new GameObject("SkidMarks");
            go.layer = Layers.Scenery;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = VXAssets.Or(VXAssets.I != null ? VXAssets.I.tyreMarks : null, "Skid", new Color(0.02f, 0.02f, 0.02f, 0.6f), 0.1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void Update(bool marking, Vector3 p, Vector3 n, Vector3 right)
        {
            if (!marking) { wasMarking = false; return; }
            if (wasMarking && (p - last).sqrMagnitude < 0.09f) return;
            if (!wasMarking && count > 0) breaks.Add(count);
            if (count >= Max) Shift();
            int i = count;
            verts[i * 2] = p - right * width * 0.5f;
            verts[i * 2 + 1] = p + right * width * 0.5f;
            normals[i * 2] = normals[i * 2 + 1] = n;
            uvs[i * 2] = new Vector2(0, i * 0.5f);
            uvs[i * 2 + 1] = new Vector2(1, i * 0.5f);
            colors[i * 2] = colors[i * 2 + 1] = new Color(1, 1, 1, wasMarking ? 0.85f : 0f);
            count++;
            last = p;
            wasMarking = true;
            Rebuild();
        }

        void Shift()
        {
            int drop = 20;
            System.Array.Copy(verts, drop * 2, verts, 0, (Max - drop) * 2);
            System.Array.Copy(normals, drop * 2, normals, 0, (Max - drop) * 2);
            System.Array.Copy(uvs, drop * 2, uvs, 0, (Max - drop) * 2);
            System.Array.Copy(colors, drop * 2, colors, 0, (Max - drop) * 2);
            count -= drop;
            for (int i = breaks.Count - 1; i >= 0; i--) { breaks[i] -= drop; if (breaks[i] <= 0) breaks.RemoveAt(i); }
        }

        void Rebuild()
        {
            int t = 0;
            for (int i = 0; i < count - 1; i++)
            {
                if (breaks.Contains(i + 1)) continue;
                int a = i * 2;
                tris[t++] = a; tris[t++] = a + 2; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = a + 2; tris[t++] = a + 3;
            }
            for (int k = t; k < tris.Length; k++) tris[k] = 0;
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(last, Vector3.one * 2000f);
        }

        public void Dispose() { if (go != null) Object.Destroy(go); }
    }
}
