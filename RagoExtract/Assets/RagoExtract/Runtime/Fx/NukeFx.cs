using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The nuke, played out: a missile streaks down out of the sky with a smoke trail; a white-hot flash and a fireball
    /// swell up; a shockwave ring and a wall of dust race out across the ground (the blast arrives when it reaches you);
    /// then the fireball rises on a churning column of smoke, rolls into a mushroom cap with a condensation ring round the
    /// stem, and slowly cools from orange to grey smoke. Every part is built from puffs (spheres) that grow, drift and
    /// churn, coloured by their own emissive material.
    /// </summary>
    public class NukeFx : MonoBehaviour
    {
        public const float FallTime = 2.2f;          // the missile's drop
        public const float WaveSpeed = 170f;         // m/s across the ground
        public float T => _t - FallTime;             // time since the blast (negative while it's falling)
        public float WaveRadius => T <= 0f ? 0f : T * WaveSpeed;

        float _t;
        Transform _missile, _trail, _fireball, _flash, _stem, _cap, _collar, _wave, _dust;
        readonly List<(Transform t, Vector3 home, float seed, float size)> _capPuffs = new List<(Transform, Vector3, float, float)>();
        readonly List<(Transform t, float h, float seed, float size)> _stemPuffs = new List<(Transform, float, float, float)>();
        readonly List<(Transform t, float a, float seed)> _dustPuffs = new List<(Transform, float, float)>();
        Light _light;
        Material _hot, _smoke, _cond, _waveMat, _dustMat, _flashMat;
        Vector3 _from;

        public static NukeFx Play(Vector3 groundZero)
        {
            var go = new GameObject("Nuke");
            go.transform.position = groundZero;
            var fx = go.AddComponent<NukeFx>();
            fx.Build();
            return fx;
        }

        static Material Glow(string key, Color c) => new Material(Mats.Lit(key, Color.white, 0f, 0.1f, Mats.Noise, null, 0.2f, 0f, false, 0.5f, c));

        void Build()
        {
            _hot = Glow("nuke_hot", new Color(3f, 1.5f, 0.45f));
            _smoke = Glow("nuke_smoke", new Color(1.6f, 0.6f, 0.15f));
            _cond = new Material(Mats.Lit("nuke_cond", new Color(0.95f, 0.95f, 0.95f), 0f, 0.2f, Mats.Noise, null, 0.2f, 0f));
            _waveMat = Glow("nuke_wave", new Color(1.2f, 1.0f, 0.8f));
            _dustMat = new Material(Mats.Lit("nuke_dust", new Color(0.55f, 0.46f, 0.36f), 0f, 0.1f, Mats.Noise, null, 0.3f, 0f));
            _flashMat = new Material(Mats.Unlit("nuke_flash", new Color(1f, 1f, 0.95f)));
            Transform Puff(Transform parent, Material m, int seg = 10)
            {
                var g = new MB().Sphere(Vector3.zero, Vector3.one, seg, Color.white).Build("Puff", parent, m);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return g.transform;
            }
            Transform Node(string n) { var t = new GameObject(n).transform; t.SetParent(transform, false); return t; }
            // the missile coming in, with a trail of smoke behind it
            _from = new Vector3(-180f, 900f, 120f);
            _missile = new MB().Cyl(new Vector3(0, 0, -6f), new Vector3(0, 0, 6f), 0.9f, 0.9f, 12, new Color(0.85f, 0.85f, 0.82f))
                .Cyl(new Vector3(0, 0, 6f), new Vector3(0, 0, 9f), 0.9f, 0.05f, 12, new Color(0.2f, 0.2f, 0.2f))
                .Build("Missile", transform, Mats.Lit("nuke_missile", Color.white, 0.6f, 0.5f, Mats.Metal, null, 0.2f, 0.2f)).transform;
            _trail = new MB().Cyl(Vector3.zero, new Vector3(0, 0, -1f), 0.6f, 2.5f, 12, Color.white).Build("Trail", transform, new Material(Mats.Lit("nuke_trail", new Color(0.92f, 0.92f, 0.9f), 0f, 0.1f))).transform;
            _flash = Puff(Node("FlashRoot"), _flashMat, 16); _flash.gameObject.SetActive(false);
            _fireball = Node("Fireball");
            for (int i = 0; i < 14; i++)
            {
                var p = Puff(_fireball, _hot);
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f;
                _capPuffs.Add((p, d * 0.55f, Random.value * 10f, Random.Range(0.45f, 0.75f)));
            }
            _cap = _fireball;
            // the stem: a column of puffs that rises under the fireball
            _stem = Node("Stem");
            for (int i = 0; i < 18; i++) { var p = Puff(_stem, _smoke); _stemPuffs.Add((p, i / 17f, Random.value * 10f, Random.Range(0.8f, 1.2f))); }
            // the condensation ring that forms round the stem
            _collar = new MB().Tube(new Vector3(0, -0.06f, 0), new Vector3(0, 0.06f, 0), 1f, 0.72f, 32, Color.white).Build("Collar", transform, _cond).transform;
            _collar.gameObject.SetActive(false);
            // the shockwave ring racing out along the ground, and the dust it throws up behind it
            _wave = new MB().Tube(new Vector3(0, 0, 0), new Vector3(0, 1f, 0), 1f, 0.94f, 64, Color.white).Build("Shockwave", transform, _waveMat).transform;
            _wave.gameObject.SetActive(false);
            _dust = Node("Dust");
            for (int i = 0; i < 36; i++) { var p = Puff(_dust, _dustMat, 8); _dustPuffs.Add((p, i / 36f * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f), Random.value * 10f)); }
            _dust.gameObject.SetActive(false);
            _light = new GameObject("Glow").AddComponent<Light>(); _light.transform.SetParent(transform, false);
            _light.type = LightType.Point; _light.range = 3000f; _light.intensity = 0f; _light.color = new Color(1f, 0.78f, 0.5f); _light.shadows = LightShadows.None;
            _fireball.gameObject.SetActive(false); _stem.gameObject.SetActive(false);
        }

        void Update()
        {
            _t += Time.deltaTime;
            float t = T;
            if (t < 0f)
            {
                // the missile: down out of the sky along a slight curve, nose first, trail stretching behind
                float k = Mathf.Clamp01(_t / FallTime), e = k * k;
                Vector3 p = Vector3.Lerp(_from, Vector3.up * 8f, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 40f;
                Vector3 pNext = Vector3.Lerp(_from, Vector3.up * 8f, Mathf.Min(1f, (k + 0.02f) * (k + 0.02f)));
                _missile.localPosition = p; _missile.localRotation = Quaternion.LookRotation((pNext - p).sqrMagnitude > 1e-4f ? (pNext - p).normalized : Vector3.down);
                _trail.localPosition = p; _trail.localRotation = _missile.localRotation;
                _trail.localScale = new Vector3(1f, 1f, Mathf.Min(600f, (p - _from).magnitude));
                return;
            }
            if (_missile != null) { Destroy(_missile.gameObject); _missile = null; }
            if (_trail != null) { var tr = _trail; StartCoroutine(FadeTrail(tr)); _trail = null; }
            // the flash: a blinding white bubble for a moment, then gone
            bool flash = t < 0.7f;
            if (_flash.gameObject.activeSelf != flash) _flash.gameObject.SetActive(flash);
            if (flash) _flash.localScale = Vector3.one * Mathf.Lerp(10f, 160f, Mathf.Sqrt(t / 0.7f));
            _light.intensity = Mathf.Lerp(12f, 0.5f, Mathf.Clamp01(t / 14f));
            // the shockwave: a bright ring hugging the ground, and the dust wall it kicks up
            float wr = WaveRadius;
            bool waveOn = t < 9f;
            _wave.gameObject.SetActive(waveOn); _dust.gameObject.SetActive(t < 22f);
            if (waveOn) _wave.localScale = new Vector3(wr, Mathf.Lerp(18f, 4f, t / 9f), wr);
            float dr = Mathf.Min(wr * 0.85f, 900f);
            for (int i = 0; i < _dustPuffs.Count; i++)
            {
                var (pt, a, s) = _dustPuffs[i];
                float churn = Mathf.Sin(_t * 1.3f + s) * 0.15f;
                float size = Mathf.Lerp(8f, 70f, Mathf.Clamp01(t / 10f)) * (1f + churn) * Mathf.Clamp01((22f - t) / 8f);   // settles away at the end
                pt.localPosition = new Vector3(Mathf.Cos(a) * dr, size * 0.35f, Mathf.Sin(a) * dr);
                pt.localScale = new Vector3(size * 1.4f, size, size * 1.4f);
            }
            // the fireball: swells fast, then rises on its stem and flattens into a rolling cap
            _fireball.gameObject.SetActive(true); _stem.gameObject.SetActive(t > 0.6f);
            float rise = Mathf.Lerp(0f, 420f, 1f - Mathf.Exp(-t * 0.12f));          // how high the cap has climbed
            float r = Mathf.Lerp(20f, 95f, 1f - Mathf.Exp(-t * 0.9f)) + t * 2.5f;     // its radius
            float flat = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(t / 8f));                 // squashes into a mushroom cap
            _fireball.localPosition = Vector3.up * (r * 0.8f + rise);
            for (int i = 0; i < _capPuffs.Count; i++)
            {
                var (pt, home, s, size) = _capPuffs[i];
                // the cap rolls: puffs circle up the outside and back in over the top (a toroidal churn)
                float roll = _t * 0.35f + s;
                Vector3 rp = new Vector3(home.x * (1f + 0.25f * Mathf.Sin(roll)), home.y * flat + 0.12f * Mathf.Cos(roll), home.z * (1f + 0.25f * Mathf.Sin(roll)));
                pt.localPosition = rp * r;
                float sc = r * size * (1f + 0.08f * Mathf.Sin(_t * 1.7f + s * 3f));
                pt.localScale = new Vector3(sc, sc * Mathf.Lerp(1f, 0.8f, 1f - flat), sc);
            }
            // the stem: puffs from the ground up into the bottom of the cap, narrowest in the middle
            float top = r * 0.8f + rise - r * 0.4f * flat;
            for (int i = 0; i < _stemPuffs.Count; i++)
            {
                var (pt, h, s, size) = _stemPuffs[i];
                float y = h * Mathf.Max(5f, top);
                float waist = Mathf.Lerp(0.55f, 0.32f, Mathf.Sin(h * Mathf.PI)) * r * 0.45f;
                float wob = Mathf.Sin(_t * 0.9f + s) * waist * 0.12f;
                pt.localPosition = new Vector3(wob, y, Mathf.Cos(_t * 0.7f + s) * waist * 0.12f);
                float sc = waist * size * Mathf.Clamp01((t - 0.6f) * 0.8f) * 1.8f;
                pt.localScale = new Vector3(sc, Mathf.Max(sc, top / _stemPuffs.Count * 2.2f), sc);
            }
            // the condensation collar: a white ring that forms part way up the stem, swells and fades
            bool collar = t > 3f && t < 20f;
            _collar.gameObject.SetActive(collar);
            if (collar)
            {
                float ck = (t - 3f) / 17f;
                float cr = Mathf.Lerp(r * 0.6f, r * 1.5f, Mathf.Sqrt(ck));
                _collar.localPosition = Vector3.up * top * 0.55f;
                _collar.localScale = new Vector3(cr, Mathf.Lerp(6f, 18f, ck) * Mathf.Clamp01((1f - ck) * 3f), cr);   // thins away
            }
            // cooling: white-hot -> orange -> dull red -> grey-brown smoke
            float cool = Mathf.Clamp01(t / 18f);
            Color hot = cool < 0.3f ? Color.Lerp(new Color(4f, 2.8f, 1.6f), new Color(3f, 1.3f, 0.3f), cool / 0.3f)
                                    : Color.Lerp(new Color(3f, 1.3f, 0.3f), new Color(0.08f, 0.06f, 0.05f), (cool - 0.3f) / 0.7f);
            _hot.SetColor("_EmissionColor", hot);
            _hot.color = Color.Lerp(new Color(1f, 0.8f, 0.5f), new Color(0.35f, 0.3f, 0.27f), cool);
            _smoke.SetColor("_EmissionColor", hot * 0.55f);
            _smoke.color = Color.Lerp(new Color(0.9f, 0.6f, 0.35f), new Color(0.3f, 0.27f, 0.25f), cool);
            _waveMat.SetColor("_EmissionColor", new Color(1.2f, 1.0f, 0.8f) * Mathf.Clamp01(1f - t / 9f));
            if (t > 40f) Destroy(gameObject);
        }

        System.Collections.IEnumerator FadeTrail(Transform tr)
        {
            float k = 0f;
            var r = tr.GetComponent<Renderer>();
            while (k < 1f && tr != null)
            {
                k += Time.deltaTime / 4f;
                tr.localScale = new Vector3((1f + k * 3f) * (1f - k), (1f + k * 3f) * (1f - k), tr.localScale.z);   // spreads, then thins out
                yield return null;
            }
            if (tr != null) Destroy(tr.gameObject);
        }

        void OnDestroy()
        {
            foreach (var m in new[] { _hot, _smoke, _cond, _waveMat, _dustMat, _flashMat }) if (m != null) Destroy(m);
        }
    }
}
