using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Frag grenade or flashbang. Grab it, pull the TRIGGER to pull the pin (you hear the click and the pin comes out),
    /// then throw it: it goes off a few seconds after the pin is pulled - wherever it is, including your hand.
    /// Desktop: click once to pull the pin, click again to throw it where you're looking.
    /// </summary>
    public class Grenade : Item
    {
        public bool Flash, Impact;
        float _releasedT = -1f, _battedT = -10f;
        bool _armed;
        float _fuse;
        Transform _pin;
        static bool _hinted;
        public const float FragFuse = 3.6f, FlashFuse = 2.2f, ImpactFuse = 8f;

        public static Grenade Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var g = go.AddComponent<Grenade>();
            g.def = d;
            g.Flash = d.shape == 1; g.Impact = d.shape == 2;
            Color w = Color.white;
            var mb = new MB(2);
            Material body = g.Flash ? Mats.Lit("flash_body", Color.white, 0.6f, 0.45f, Mats.Metal, null, 0.3f, 0.3f) : Mats.Lit("frag_body", Color.white, 0.2f, 0.35f, Mats.Noise, null, 0.45f, 0.25f);
            Material metal = Mats.Lit("grenade_metal", Color.white, 0.9f, 0.6f, Mats.Metal, null, 0.2f, 0.3f);
            if (g.Flash)
            {
                mb.Sub(0).Cyl(new Vector3(0, -0.055f, 0), new Vector3(0, 0.045f, 0), 0.024f, 0.024f, 16, new Color(0.55f, 0.57f, 0.58f));
                for (int i = 0; i < 6; i++) { float a = i * 60f * Mathf.Deg2Rad; mb.Sub(0).Box(new Vector3(Mathf.Cos(a) * 0.0235f, 0f, Mathf.Sin(a) * 0.0235f), new Vector3(0.008f, 0.03f, 0.008f), new Color(0.12f, 0.12f, 0.12f)); }
                mb.Sub(0).Cyl(new Vector3(0, -0.03f, 0), new Vector3(0, -0.02f, 0), 0.0245f, 0.0245f, 16, new Color(0.15f, 0.15f, 0.15f));
            }
            else if (g.Impact)
            {
                // round body with a black band and a yellow stripe, impact fuse on top
                mb.Sub(0).Sphere(new Vector3(0, -0.012f, 0), new Vector3(0.034f, 0.034f, 0.034f), 16, new Color(0.22f, 0.27f, 0.2f));
                mb.Sub(0).Cyl(new Vector3(0, -0.018f, 0), new Vector3(0, -0.006f, 0), 0.0352f, 0.0352f, 16, new Color(0.06f, 0.06f, 0.06f));
                mb.Sub(0).Cyl(new Vector3(0, 0.004f, 0), new Vector3(0, 0.009f, 0), 0.0338f, 0.033f, 16, new Color(0.85f, 0.7f, 0.1f));
            }
            else
            {
                mb.Sub(0).Sphere(new Vector3(0, -0.015f, 0), new Vector3(0.032f, 0.038f, 0.032f), 16, new Color(0.3f, 0.34f, 0.22f));
                mb.Sub(0).Cyl(new Vector3(0, -0.05f, 0), new Vector3(0, -0.054f, 0), 0.014f, 0.012f, 12, new Color(0.25f, 0.28f, 0.18f));
            }
            // fuse head + spoon down the side
            mb.Sub(1).Cyl(new Vector3(0, 0.02f, 0), new Vector3(0, 0.05f, 0), 0.012f, 0.011f, 12, w);
            mb.Sub(1).Box(new Vector3(0.018f, 0.025f, 0), new Vector3(0.008f, 0.06f, 0.014f), w * 0.85f, 0.002f, 1, Quaternion.Euler(0, 0, -8));
            mb.Build("Body", go.transform, body, metal);
            // pin ring (comes off when you pull the pin)
            g._pin = new GameObject("Pin").transform; g._pin.SetParent(go.transform, false);
            g._pin.localPosition = new Vector3(-0.016f, 0.045f, 0);
            var ring = new MB();
            for (int i = 0; i < 12; i++) { float a = i * 30f; ring.Box(Quaternion.Euler(0, 0, a) * new Vector3(0.011f, 0, 0) + new Vector3(-0.01f, 0, 0), new Vector3(0.002f, 0.006f, 0.002f), w, 0, 0, Quaternion.Euler(0, 0, a)); }
            ring.Build("Ring", g._pin, metal);
            var col = go.AddComponent<SphereCollider>(); col.radius = g.Flash ? 0.032f : 0.04f; col.center = new Vector3(0, -0.005f, 0);
            g.localBounds = new Bounds(Vector3.zero, new Vector3(0.07f, 0.11f, 0.07f));
            g.grip = new GameObject("Grip").transform; g.grip.SetParent(go.transform, false);   // in your fist, fuse and spoon up
            g.grip.localPosition = new Vector3(0, -0.012f, 0); g.grip.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            g.SetupPhysics(0.4f);
            return g;
        }

        public override string Label => def.name + (_armed ? "  (PIN PULLED)" : "");

        public override void HeldUpdate(Hand h, HandInput input)
        {
            if (!_armed && input.TriggerDown) { Arm(); h.Haptic(0.5f, 0.05f); return; }
            // desktop: second click throws it where you're looking
            if (_armed && !XRInputs.VR && input.TriggerDown)
            {
                var head = RagoGame.I.Player.Head;
                h.ForceRelease(true);
                primaryHand = null;
                Drop(Vector3.zero, Vector3.zero);
                transform.position = head.position + head.forward * 0.5f;
                rb.SetVel(head.forward * 13f + Vector3.up * 2.5f);
                rb.angularVelocity = Random.insideUnitSphere * 8f;
            }
        }

        /// <summary>Hit with a bat (or any blunt melee weapon): flies off along the swing.</summary>
        public bool Batted(Vector3 vel)
        {
            if (primaryHand != null || slot != null || rb == null || Time.time - _battedT < 0.25f) return false;
            _battedT = Time.time;
            if (_releasedT < 0f) _releasedT = Time.time - 1f;   // counts as thrown
            rb.isKinematic = false;
            rb.SetVel(vel);
            rb.angularVelocity = Random.insideUnitSphere * 15f;
            Sfx.Play(Synth.Impact(Synth.Surface.Wood, Random.Range(0, 3)), transform.position, 1f, 1.3f, 1f, 30f);
            return true;
        }

        /// <summary>Armed by something else (a booby-trapped Christmas present): goes off after this many seconds.</summary>
        public void ArmNow(float fuse) { bool h = _hinted; _hinted = true; Arm(); _hinted = h; _fuse = fuse; }

        void Arm()
        {
            _armed = true;
            _fuse = Flash ? FlashFuse : Impact ? ImpactFuse : FragFuse;
            if (_pin != null) _pin.gameObject.SetActive(false);
            Sfx.Play(Synth.PinPull(), transform.position, 0.7f, 1f, 0.5f, 8f);
            if (!_hinted) { _hinted = true; RagoGame.I?.Player?.Toast("Pin pulled - throw it! (" + (int)_fuse + " s)"); }
        }

        bool _wasHeld; float _bounceT;
        void OnCollisionEnter(Collision c)
        {
            // impact grenade: armed and thrown (out of your hand a moment) - goes off on the first real hit
            // a bat hit sends it flying - that hit never sets off an impact grenade (whatever it hits next does)
            if (c.collider != null && c.collider.GetComponentInParent<Knife>() != null) return;
            if (Impact && _armed && Time.time - _battedT > 0.08f && primaryHand == null && _releasedT > 0f && Time.time - _releasedT > 0.12f && c.relativeVelocity.sqrMagnitude > 4f) { _fuse = 0f; return; }
            if (Time.time < _bounceT || c.relativeVelocity.sqrMagnitude < 1.2f) return;
            _bounceT = Time.time + 0.12f;
            Sfx.Play(Synth.GrenadeBounce(Random.Range(0, 3)), transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 6f) * 0.8f, 1f + Random.Range(-0.08f, 0.08f), 1f, 25f);
        }

        public override void AnyUpdate()
        {
            // the spoon flies off with a ping as the armed grenade leaves your hand
            bool held = primaryHand != null;
            if (_wasHeld && !held) _releasedT = Time.time;
            if (_armed && _wasHeld && !held) Sfx.Play(Synth.SpoonFly(), transform.position, 0.6f, 1f + Random.Range(-0.05f, 0.05f), 0.5f, 12f);
            _wasHeld = held;
            if (!_armed) return;
            _fuse -= Time.deltaTime;
            if (_fuse > 0) return;
            _armed = false;
            Vector3 p = transform.position;
            Consumed = true;
            if (primaryHand != null) primaryHand.ForceRelease(true);
            if (slot != null) slot.Release(this);
            try { Blast.Detonate(p, Flash, 0, true); }
            finally { Destroy(gameObject); }   // gone either way
        }
    }

    /// <summary>
    /// A knife. Swing or stab it into someone: the faster the blade moves, the harder it hits (head hits hurt most).
    /// Desktop: click to stab what you're looking at.
    /// </summary>
    public class Knife : Item
    {
        Transform _tip, _base;
        Vector3 _lastTip, _lastBase;
        bool _track;
        float _damage = 45f;
        readonly Dictionary<IDamageable, float> _cool = new Dictionary<IDamageable, float>();
        float _lockCool;
        static readonly RaycastHit[] _hits = new RaycastHit[12];

        public bool Blunt;   // bats, crowbars, hammers, pans: a thud and a shove instead of a cut
        float _force = 2f;

        public static Knife Create(SimpleDef d, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(d.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var k = go.AddComponent<Knife>();
            k.def = d;
            k._damage = d.heal > 0 ? d.heal : 45f;   // melee keeps its damage in the def's "heal" slot
            if (d.shape >= 2) { k.BuildLong(d.shape); return k; }
            bool hunt = d.shape == 1;
            float blade = hunt ? 0.17f : 0.14f;
            var mb = new MB(2);
            Color w = Color.white;
            // handle along -z, guard at 0, blade along +z (the hand holds the handle like a pistol grip)
            mb.Sub(0).Box(new Vector3(0, 0, -0.055f), new Vector3(0.024f, 0.03f, 0.11f), hunt ? new Color(0.42f, 0.28f, 0.16f) : new Color(0.1f, 0.1f, 0.1f), 0.008f, 2);
            mb.Sub(1).Box(new Vector3(0, 0, 0.004f), new Vector3(0.03f, 0.05f, 0.008f), w * 0.7f, 0.003f);
            mb.Sub(1).Box(new Vector3(0, 0.002f, 0.008f + blade * 0.5f), new Vector3(0.004f, 0.028f, blade), w, 0.001f);
            mb.Sub(1).Box(new Vector3(0, -0.006f, 0.008f + blade + 0.008f), new Vector3(0.004f, 0.016f, 0.02f), w, 0.001f, 1, Quaternion.Euler(-35, 0, 0));
            mb.Sub(1).Cyl(new Vector3(0, 0, -0.115f), new Vector3(0, 0, -0.105f), 0.016f, 0.016f, 10, w * 0.6f);
            mb.Build("Model", go.transform, Mats.Lit(hunt ? "knife_wood" : "knife_grip", Color.white, 0f, 0.3f, hunt ? Mats.Wood : Mats.Noise, null, 0.3f, 0.1f),
                Mats.Lit("knife_steel", Color.white, 0.95f, 0.8f, Mats.Metal, null, 0.1f, 0.4f));
            k.grip = new GameObject("Grip").transform; k.grip.SetParent(go.transform, false); k.grip.localPosition = new Vector3(0, 0, -0.055f);
            k._tip = new GameObject("Tip").transform; k._tip.SetParent(go.transform, false); k._tip.localPosition = new Vector3(0, 0, 0.02f + blade);
            k._base = new GameObject("BladeBase").transform; k._base.SetParent(go.transform, false); k._base.localPosition = new Vector3(0, 0, 0.02f);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0, (0.008f + blade - 0.11f) * 0.5f); col.size = new Vector3(0.03f, 0.05f, 0.12f + blade);
            k.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            k.SetupPhysics(0.3f);
            return k;
        }

        /// <summary>The long melee weapons. Held at local z = 0 (the grip), the business end along +z.
        /// 2 bat, 3 crowbar, 4 machete, 5 sledgehammer, 6 fire axe, 7 frying pan, 8 candy cane.</summary>
        void BuildLong(int shape)
        {
            var go = gameObject;
            var mb = new MB(3);   // 0 wood / grip / paint, 1 steel, 2 tape
            Color w = Color.white, tape = new Color(0.08f, 0.08f, 0.08f);
            float len, hitFrom, mass; Vector3 colC, colS; Synth.Surface surf = Synth.Surface.Metal;
            switch (shape)
            {
                case 2:   // baseball bat: knob, taped handle, swelling barrel
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.1f), new Vector3(0, 0, -0.085f), 0.026f, 0.026f, 14, new Color(0.62f, 0.45f, 0.28f));
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.085f), new Vector3(0, 0, 0.25f), 0.014f, 0.019f, 14, new Color(0.62f, 0.45f, 0.28f), false);
                    mb.Sub(0).Cyl(new Vector3(0, 0, 0.25f), new Vector3(0, 0, 0.7f), 0.019f, 0.033f, 16, new Color(0.62f, 0.45f, 0.28f), false);
                    mb.Sub(0).Sphere(new Vector3(0, 0, 0.7f), new Vector3(0.033f, 0.033f, 0.02f), 14, new Color(0.62f, 0.45f, 0.28f));
                    mb.Sub(2).Cyl(new Vector3(0, 0, -0.085f), new Vector3(0, 0, 0.12f), 0.0165f, 0.0175f, 14, tape, false);
                    len = 0.72f; hitFrom = 0.35f; mass = 0.9f; colC = new Vector3(0, 0, 0.31f); colS = new Vector3(0.06f, 0.06f, 0.82f); surf = Synth.Surface.Wood; _force = 5f; Blunt = true; break;
                case 3:   // crowbar: a hex bar with a hook at the top and a flat claw at the bottom
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.42f), 0.011f, 0.011f, 6, new Color(0.6f, 0.08f, 0.06f));
                    for (int i = 0; i < 6; i++) { float a = i / 5f * Mathf.PI; mb.Sub(0).Cyl(new Vector3(0, -0.05f + Mathf.Cos(a) * 0.05f, 0.42f + Mathf.Sin(a) * 0.05f), new Vector3(0, -0.05f + Mathf.Cos(a + 0.63f) * 0.05f, 0.42f + Mathf.Sin(a + 0.63f) * 0.05f), 0.011f, 0.011f, 6, new Color(0.6f, 0.08f, 0.06f)); }
                    mb.Sub(1).Box(new Vector3(0, 0.012f, -0.13f), new Vector3(0.026f, 0.03f, 0.03f), w * 0.6f, 0.003f, 1, Quaternion.Euler(-30, 0, 0));
                    len = 0.47f; hitFrom = 0.2f; mass = 0.8f; colC = new Vector3(0, -0.02f, 0.17f); colS = new Vector3(0.03f, 0.08f, 0.62f); _force = 3.5f; Blunt = true; break;
                case 4:   // machete: wrapped grip, long slightly curved blade
                    mb.Sub(0).Box(new Vector3(0, 0, -0.06f), new Vector3(0.026f, 0.032f, 0.13f), new Color(0.12f, 0.12f, 0.12f), 0.009f, 2);
                    for (int i = 0; i < 5; i++) mb.Sub(1).Box(new Vector3(0, 0.004f + i * 0.0015f, 0.06f + i * 0.08f), new Vector3(0.0035f, 0.045f + i * 0.004f, 0.085f), w * 0.85f, 0.001f, 1, Quaternion.Euler(-1.5f * i, 0, 0));
                    len = 0.46f; hitFrom = 0.1f; mass = 0.6f; colC = new Vector3(0, 0.005f, 0.17f); colS = new Vector3(0.03f, 0.06f, 0.6f); _force = 2.2f; break;
                case 5:   // sledgehammer: long wooden handle, a heavy steel head
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.18f), new Vector3(0, 0, 0.62f), 0.017f, 0.017f, 12, new Color(0.55f, 0.4f, 0.25f));
                    mb.Sub(1).Box(new Vector3(0, 0, 0.64f), new Vector3(0.07f, 0.2f, 0.075f), new Color(0.28f, 0.28f, 0.3f), 0.012f, 2);
                    mb.Sub(2).Cyl(new Vector3(0, 0, -0.18f), new Vector3(0, 0, 0.0f), 0.019f, 0.019f, 12, tape, false);
                    len = 0.66f; hitFrom = 0.58f; mass = 2.4f; colC = new Vector3(0, 0, 0.24f); colS = new Vector3(0.08f, 0.2f, 0.9f); _force = 7f; Blunt = true; break;
                case 6:   // fire axe: red head, blade one side, spike the other
                    mb.Sub(0).Cyl(new Vector3(0, 0, -0.15f), new Vector3(0, 0, 0.6f), 0.016f, 0.017f, 12, new Color(0.55f, 0.4f, 0.25f));
                    mb.Sub(0).Box(new Vector3(0, 0.0f, 0.6f), new Vector3(0.04f, 0.06f, 0.07f), new Color(0.75f, 0.1f, 0.08f), 0.008f);
                    mb.Sub(0).Box(new Vector3(0, 0.08f, 0.6f), new Vector3(0.014f, 0.12f, 0.08f), new Color(0.75f, 0.1f, 0.08f), 0.004f);
                    mb.Sub(1).Box(new Vector3(0, 0.15f, 0.6f), new Vector3(0.006f, 0.03f, 0.13f), w * 0.9f, 0.001f);
                    mb.Sub(0).Cyl(new Vector3(0, -0.03f, 0.6f), new Vector3(0, -0.12f, 0.6f), 0.016f, 0.003f, 6, new Color(0.75f, 0.1f, 0.08f));
                    len = 0.64f; hitFrom = 0.55f; mass = 1.8f; colC = new Vector3(0, 0.02f, 0.23f); colS = new Vector3(0.06f, 0.3f, 0.82f); _force = 4f; break;
                case 7:   // frying pan: a cast-iron disc on a short handle
                    mb.Sub(0).Box(new Vector3(0, 0, 0.0f), new Vector3(0.026f, 0.018f, 0.2f), new Color(0.1f, 0.1f, 0.1f), 0.006f);
                    mb.Sub(0).Cyl(new Vector3(0, -0.012f, 0.22f), new Vector3(0, 0.02f, 0.22f), 0.12f, 0.13f, 24, new Color(0.12f, 0.12f, 0.12f));
                    mb.Sub(1).Cyl(new Vector3(0, 0.0205f, 0.22f), new Vector3(0, 0.0215f, 0.22f), 0.11f, 0.11f, 24, new Color(0.22f, 0.22f, 0.23f), false);
                    len = 0.34f; hitFrom = 0.12f; mass = 1.4f; colC = new Vector3(0, 0, 0.17f); colS = new Vector3(0.26f, 0.04f, 0.46f); _force = 4f; Blunt = true; break;
                default:  // candy cane: red and white stripes with a hook
                    for (int i = 0; i < 14; i++) mb.Sub(0).Cyl(new Vector3(0, 0, -0.12f + i * 0.045f), new Vector3(0, 0, -0.075f + i * 0.045f), 0.02f, 0.02f, 12, i % 2 == 0 ? new Color(0.9f, 0.12f, 0.12f) : new Color(0.95f, 0.95f, 0.95f), i == 0 || i == 13);
                    for (int i = 0; i < 8; i++) { float a = i / 7f * Mathf.PI, a2 = (i + 1) / 7f * Mathf.PI; if (i == 7) break; mb.Sub(0).Cyl(new Vector3(0, -0.07f + Mathf.Cos(a) * 0.07f, 0.51f + Mathf.Sin(a) * 0.07f), new Vector3(0, -0.07f + Mathf.Cos(a2) * 0.07f, 0.51f + Mathf.Sin(a2) * 0.07f), 0.02f, 0.02f, 12, i % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.9f, 0.12f, 0.12f)); }
                    len = 0.58f; hitFrom = 0.25f; mass = 0.7f; colC = new Vector3(0, -0.03f, 0.23f); colS = new Vector3(0.05f, 0.12f, 0.75f); surf = Synth.Surface.Wood; _force = 3f; Blunt = true; break;
            }
            mb.Build("Model", go.transform, Mats.Lit("melee_body", Color.white, 0.05f, 0.35f, Mats.Noise, null, 0.3f, 0.15f),
                Mats.Lit("knife_steel", Color.white, 0.95f, 0.8f, Mats.Metal, null, 0.1f, 0.4f), Mats.Lit("melee_tape", Color.white, 0f, 0.15f, Mats.Fabric, null, 0.3f, 0f));
            grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false); grip.localPosition = Vector3.zero;
            _tip = new GameObject("Tip").transform; _tip.SetParent(go.transform, false); _tip.localPosition = new Vector3(0, 0, len);
            _base = new GameObject("BladeBase").transform; _base.SetParent(go.transform, false); _base.localPosition = new Vector3(0, 0, hitFrom);
            var col = go.AddComponent<BoxCollider>(); col.center = colC; col.size = colS;
            localBounds = new Bounds(colC, colS);
            go.AddComponent<SurfaceTag>().surface = surf;
            SetupPhysics(mass);
        }

        public override void OnGrab(Hand h) { base.OnGrab(h); _track = false; }

        public override void HeldUpdate(Hand h, HandInput input)
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (!XRInputs.VR)
            {
                if (input.TriggerDown && Physics.Raycast(pl.Head.position, pl.Head.forward, out var dh, 1.7f, ~0, QueryTriggerInteraction.Ignore)) TryHit(dh.collider, dh.point, pl.Head.forward, 5f, h);
                return;
            }
            Vector3 tip = _tip.position, bas = _base.position;
            if (_track)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.001f);
                float speed = (tip - _lastTip).magnitude / dt;
                if (speed > 1.8f)
                {
                    // sweep the blade from last frame to this one
                    for (int s = 0; s < 3; s++)
                    {
                        float t = s / 2f;
                        Vector3 a = Vector3.Lerp(_lastBase, _lastTip, t), b = Vector3.Lerp(bas, tip, t);
                        Vector3 d = b - a; float len = d.magnitude;
                        if (len < 0.001f) continue;
                        int n = Physics.SphereCastNonAlloc(a, Blunt ? 0.035f : 0.012f, d / len, _hits, len, ~0, QueryTriggerInteraction.Collide);
                        for (int i = 0; i < n; i++)
                            if (Blunt && _hits[i].collider != null && _hits[i].collider.GetComponentInParent<Grenade>() is Grenade gr)
                            {
                                // batting practice: knock a grenade away (fast swings send it far)
                                Vector3 sw = (d / len) * Mathf.Clamp(speed * 2.2f, 8f, 35f) + Vector3.up * 2f;
                                if (gr.Batted(sw)) h.Haptic(1f, 0.1f);
                            }
                        for (int i = 0; i < n; i++) if (TryHit(_hits[i].collider, _hits[i].point == Vector3.zero ? b : _hits[i].point, d / len, speed, h)) { s = 3; break; }
                    }
                }
            }
            _lastTip = tip; _lastBase = bas; _track = true;
        }

        bool TryHit(Collider c, Vector3 point, Vector3 dir, float speed, Hand h)
        {
            var lk = c.GetComponentInParent<Padlock>();
            if (lk != null && speed > 2f) { if (Time.time < _lockCool) return false; _lockCool = Time.time + 0.4f; lk.Damage(_damage * (def.id == "sledge" || def.id == "crowbar" || def.id == "fireaxe" ? 1.2f : 0.4f), point); h.Haptic(0.8f, 0.08f); return true; }
            var zone = c.GetComponentInParent<HitZone>();
            if (zone == null || zone.owner == null || !zone.owner.Alive) return false;
            if (zone.owner == (IDamageable)RagoGame.I.Player.Vitals) return false;   // not yourself
            if (_cool.TryGetValue(zone.owner, out var until) && Time.time < until) return false;
            _cool[zone.owner] = Time.time + 0.4f;
            float mult = zone.part == BodyPart.Head ? 1.8f : zone.part == BodyPart.Thorax || zone.part == BodyPart.Stomach ? 1f : 0.6f;
            float dmg = _damage * Mathf.Clamp(speed / 4f, 0.6f, 1.5f) * mult;
            var info = new HitInfo { damage = dmg, part = zone.part, point = point, dir = dir, normal = -dir, attacker = 0, gunId = def.id, force = _force * Mathf.Clamp(speed / 5f, 0.6f, 1.4f) };
            Fx.Blood(point, dir, -dir, c.transform, Blunt ? dmg * 0.4f : dmg);
            if (Blunt) Sfx.Play(Synth.Impact(Synth.Surface.Flesh, Random.Range(0, 3)), point, 0.9f, def.id == "pan" ? 1.6f : 0.8f, 1f, 15f);
            zone.owner.TakeHit(info, zone);
            Ballistics.ReportHit(info, zone);
            Sfx.UI(Synth.HitMarker(), 0.3f);
            h.Haptic(0.8f, 0.08f);
            Noise.Emit(point, 6f, 0);
            return true;
        }
    }

    /// <summary>What happens when a grenade goes off - run on every client so everyone sees and hears it.</summary>
    public static class Blast
    {
        public const float FragRadius = 9f, FlashRadius = 16f;
        static readonly Collider[] _cols = new Collider[2048];

        /// <summary>
        /// Only the layers people and loose things are on. Searching every layer filled the list with walls, floors,
        /// trees and grass long before it reached the raiders, so on most maps grenades and flashbangs never touched them.
        /// </summary>
        static int ActorMask
        {
            get
            {
                int m = 0;
                foreach (int l in new[] { Layers.Npc, Layers.Player, Layers.LocalBody, Layers.Ragdoll, Layers.Items }) if (l > 0) m |= 1 << l;   // 0 = layer not set up (Default)
                return m == 0 ? ~0 : m;
            }
        }

        /// <param name="local">true on the thrower's machine: it applies the damage, forces and noise, and tells the others</param>
        public static void Detonate(Vector3 p, bool flash, int attacker, bool local)
        {
            // the visuals can never stop the blast itself (a missing effect once made every grenade a dud)
            try { Fx.Explosion(p, flash); } catch (System.Exception e) { Debug.LogWarning("[Rago] blast fx: " + e.Message); }
            try
            {
                Sfx.Play(flash ? Synth.FlashBang() : Synth.Explosion(), p, 1f, 1f + Random.Range(-0.05f, 0.05f), 6f, flash ? 140f : 260f);
                // heard from far away too: a low rolling thud with an echo off the buildings / hills
                var me = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.Head.position : p;
                float far = Vector3.Distance(me, p);
                if (far > 40f) Sfx.Play(Synth.ExplosionFar(), p, Mathf.Clamp01(1.3f - far / 600f) * (flash ? 0.5f : 1f), 1f, 30f, 700f);
                // debris pattering down after a frag
                if (!flash) Sfx.Play(Synth.Impact(Synth.Surface.Dirt, Random.Range(0, 3)), p + Vector3.up * 0.3f, 0.5f, 0.8f, 2f, 40f);
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] blast sfx: " + e.Message); }
            // everyone works out their own flash blindness and camera shake
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && !pl.Dead)
            {
                float d = Vector3.Distance(pl.Head.position, p);
                if (flash && d < FlashRadius && ClearLine(p, pl.Head.position))
                {
                    float facing = Mathf.InverseLerp(-0.3f, 0.9f, Vector3.Dot(pl.Head.forward, (p - pl.Head.position).normalized));
                    float strength = Mathf.Clamp01(1.15f - d / FlashRadius) * Mathf.Lerp(0.35f, 1f, facing);
                    pl.Hud.Flashbang(strength);
                }
                else if (!flash && d < FragRadius * 2.5f) pl.Hud.Suppress(Mathf.Clamp01(1.2f - d / (FragRadius * 2.5f)));
            }
            bool authority = true;   // each player blinds the raiders they run (puppets are skipped below)
            if (flash && authority)
            {
                // raiders who can see it are blinded and stagger
                // every living raider in range with a clear line from the bang to their head (or chest)
                if (NpcDirector.I != null)
                    foreach (var npc in NpcDirector.I.Npcs)
                    {
                        if (npc == null || !npc.Alive || npc.Puppet) continue;
                        float d = Vector3.Distance(npc.transform.position, p);
                        if (d > FlashRadius) continue;
                        if (ClearLine(p, npc.transform.position + Vector3.up * 1.6f) || ClearLine(p, npc.transform.position + Vector3.up * 1.1f))
                            npc.Flash(Mathf.Lerp(7f, 2.5f, d / FlashRadius));
                    }
            }
            // the blast shatters windows near it (every machine does this itself)
            if (!flash)
                foreach (var w in new List<Window>(Window.All))
                    if (w != null && (w.transform.position - p).sqrMagnitude < FragRadius * FragRadius * 0.7f) w.Break((w.transform.position - p).normalized);
            // gore: anyone caught right next to a frag gets torn up (visual only, every machine)
            if (!flash && NpcDirector.I != null)
                try
                {
                    foreach (var npc in NpcDirector.I.Npcs)
                    {
                        if (npc == null || npc.Rig == null || npc.Rig.chest == null) continue;
                        Vector3 c = npc.Rig.chest.position;
                        float dn = Vector3.Distance(c, p);
                        if (dn < FragRadius * 0.45f && ClearLine(p, c)) { Fx.Gibs(c, (c - p).normalized, 2f); Fx.BloodPool(c); }
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("[Rago] blast gore: " + e.Message); }
            if (!local)
            {
                // someone else's frag: it hurts us here (each player takes their own shrapnel)
                if (!flash && pl != null && !pl.Dead && RagoGame.I.Area == Area.Raid)
                {
                    Vector3 at = pl.Body != null && pl.Body.Rig != null ? pl.Body.Rig.chest.position : pl.Head.position;
                    float d2 = Vector3.Distance(at, p);
                    if (d2 < FragRadius && (ClearLine(p, at) || ClearLine(p, pl.Head.position)))
                    {
                        float k = Mathf.Clamp01(1f - d2 / FragRadius);
                        pl.Vitals.TakeHit(new HitInfo { damage = 25f + 210f * k * k, part = BodyPart.Thorax, point = at, dir = (at - p).normalized, attacker = attacker, gunId = "frag", force = 6f * k + 1f, fromNet = true }, null);
                    }
                }
                return;
            }
            Noise.Emit(p, flash ? 45f : 80f, attacker, true);
            if (NetGame.Live) NetGame.I.SendBlast(p, flash);
            if (flash) return;
            // frag: damage everyone with a clear line to the blast, push loose things and bodies
            int m = Physics.OverlapSphereNonAlloc(p, FragRadius, _cols, ActorMask, QueryTriggerInteraction.Collide);
            var hitOwners = new Dictionary<IDamageable, HitZone>();
            for (int i = 0; i < m; i++)
            {
                var c = _cols[i];
                var z = c.GetComponent<HitZone>();
                if (z != null && z.owner != null && z.owner.Alive)
                {
                    // the shrapnel hits the part of them nearest the blast (legs and belly for a grenade on the floor)
                    if (!hitOwners.TryGetValue(z.owner, out var cur) || (z.transform.position - p).sqrMagnitude < (cur.transform.position - p).sqrMagnitude) hitOwners[z.owner] = z;
                }
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) rb.AddExplosionForce(14f, p, FragRadius, 0.6f, ForceMode.Impulse);
            }
            foreach (var kv in hitOwners)
            {
                var z = kv.Value;
                Vector3 at = z.transform.position;
                float d = Vector3.Distance(at, p);
                if (!ClearLine(p, at) && !ClearLine(p, (z.owner as Component) != null ? ((Component)z.owner).transform.position + Vector3.up * 1.2f : at)) continue;
                float k = Mathf.Clamp01(1f - d / FragRadius);
                float dmg = 25f + 210f * k * k;   // a frag within ~3 m kills an ordinary raider
                var info = new HitInfo { damage = dmg, part = z.part, point = at, dir = (at - p).normalized, normal = (p - at).normalized, attacker = attacker, gunId = "frag", force = 6f * k + 1f };
                z.owner.TakeHit(info, z);
                Ballistics.ReportHit(info, z);
            }
        }

        static readonly RaycastHit[] _hits = new RaycastHit[32];
        static bool ClearLine(Vector3 from, Vector3 to)
        {
            // walls and floors block a blast; people, items and hands don't
            int mask = ~0;
            foreach (int l in new[] { Layers.Items, Layers.Hands, Layers.Npc, Layers.Ragdoll, Layers.LocalBody, Layers.Player }) if (l > 0) mask &= ~(1 << l);
            // anything belonging to a person (a raider's or player's own capsule, armour, gun) never shields them
            from += Vector3.up * 0.15f;
            var d = to - from; float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, _hits, len, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                if (c == null || c.GetComponentInParent<HitZone>() != null || c.GetComponentInParent<Npc>() != null || c.GetComponentInParent<PlayerRig>() != null || c.GetComponentInParent<RemotePlayer>() != null) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // loose crates, items, bodies
                return false;
            }
            return true;
        }
    }
}
