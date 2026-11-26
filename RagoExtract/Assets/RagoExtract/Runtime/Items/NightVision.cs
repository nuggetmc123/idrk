using UnityEngine;

namespace Rago
{
    /// <summary>The night vision goggles as an item: a head mount with twin tubes. Let go of them at your head to wear them.</summary>
    public class NvgItem : Item
    {
        public static NvgItem Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var it = go.AddComponent<NvgItem>();
            it.def = d;
            var mb = new MB(2);
            GearModels.NvgDevice(mb, 0, 1, new Vector3(0, 0, 0.0f));   // the same goggles you see on a helmet
            var model = mb.Build("Model", go.transform, Mats.Lit("nvg_body", Color.white, 0.3f, 0.45f, Mats.Noise, null, 0.3f, 0.2f),
                                 Mats.Lit("nvg_lens", Color.white, 0.6f, 0.9f, null, null, 0, 0, false, 0.5f, new Color(0.05f, 0.25f, 0.08f)));
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.03f, 0.005f); col.size = new Vector3(0.105f, 0.11f, 0.11f);
            it.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            it.SetupPhysics(0.6f);
            return it;
        }

        protected override bool TrySlot(Hand h)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead && pl.Gear.TryWearNvg(this, h)) return true;
            return base.TrySlot(h);
        }
    }

    /// <summary>
    /// The goggles' picture: everything lit up in green (the scene's ambient light is turned right up and tinted),
    /// with the dark round edge of the tubes around it. Flipping them up puts the real light back.
    /// </summary>
    public static class NightVision
    {
        public static bool On { get; private set; }
        static Color _sky, _eq, _gr, _fog; static float _fogD; static bool _saved;

        public static void Set(bool on, PlayerRig p)
        {
            if (on == On) return;
            On = on;
            if (on)
            {
                _sky = RenderSettings.ambientSkyColor; _eq = RenderSettings.ambientEquatorColor; _gr = RenderSettings.ambientGroundColor;
                _fog = RenderSettings.fogColor; _fogD = RenderSettings.fogDensity; _saved = true;
                // amplified starlight: the darkness lifts evenly (the goggles' picture turns it all phosphor green)
                RenderSettings.ambientSkyColor = new Color(0.66f, 0.7f, 0.66f);
                RenderSettings.ambientEquatorColor = new Color(0.54f, 0.58f, 0.54f);
                RenderSettings.ambientGroundColor = new Color(0.34f, 0.38f, 0.34f);
                RenderSettings.fogColor = new Color(0.06f, 0.2f, 0.08f);
                RenderSettings.fogDensity = _fogD * 0.6f;
                Sfx.UI(Synth.Beep(), 0.25f);
            }
            else if (_saved)
            {
                RenderSettings.ambientSkyColor = _sky; RenderSettings.ambientEquatorColor = _eq; RenderSettings.ambientGroundColor = _gr;
                RenderSettings.fogColor = _fog; RenderSettings.fogDensity = _fogD; _saved = false;
            }
            if (p != null && p.Hud != null) p.Hud.SetNightVision(on);
        }

        /// <summary>Leaving a raid / dying: goggles off, real light back (the new map sets its own light anyway).</summary>
        public static void Reset(PlayerRig p) { if (On) Set(false, p); _saved = false; }
    }
}
