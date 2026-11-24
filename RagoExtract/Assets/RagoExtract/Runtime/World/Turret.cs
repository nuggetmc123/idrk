using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The machine gun on the sidecar. The passenger grabs its two spade grips (VR) and points it by moving their hands;
    /// either trigger fires. On desktop it follows your view and the mouse fires. 250-round belt, then a reload.
    /// Everyone sees and hears it fire (the shots go out like any gun's), and the gun swings to where it's shooting.
    /// </summary>
    public class Turret : MonoBehaviour
    {
        public GolfCart Vehicle;
        public Transform Pivot, Muzzle;
        readonly Transform[] _grips = new Transform[2];
        readonly bool[] _held = new bool[2];
        float _yaw, _pitch, _fireT, _reloadT, _kick, _remoteAimUntil;
        int _rounds = Belt;
        const int Belt = 250;
        const float YawLimit = 120f, PitchMin = -12f, PitchMax = 40f;
        static readonly Vector3 GripLocal = new Vector3(0, -0.05f, -0.34f);   // the grips, behind and a little below the pivot
        public static GunDef Gun => Db.Get<GunDef>("hammer");   // a 7.62 general-purpose MG: its damage, rate, sound

        public void Init(GolfCart v)
        {
            Vehicle = v;
            Pivot = new GameObject("Pivot").transform; Pivot.SetParent(transform, false);
            Color dark = new Color(0.11f, 0.11f, 0.12f), metal = new Color(0.3f, 0.31f, 0.33f), olive = new Color(0.25f, 0.29f, 0.19f), brass = new Color(0.85f, 0.66f, 0.3f);
            var mb = new MB(3);
            // yoke, receiver, feed cover, barrel in a ventilated jacket, flash hider, bipod folded up, ammo can and belt
            mb.Sub(0).Box(new Vector3(0, -0.02f, 0), new Vector3(0.12f, 0.05f, 0.1f), dark, 0.01f);
            mb.Sub(0).Box(new Vector3(0, 0.05f, -0.05f), new Vector3(0.09f, 0.11f, 0.42f), metal, 0.015f, 2);
            mb.Sub(0).Box(new Vector3(0, 0.115f, -0.02f), new Vector3(0.085f, 0.025f, 0.22f), dark, 0.01f);
            mb.Sub(0).Cyl(new Vector3(0, 0.06f, 0.16f), new Vector3(0, 0.06f, 0.62f), 0.032f, 0.032f, 14, dark);
            for (int i = 0; i < 8; i++) mb.Sub(1).Sphere(new Vector3(0.0f, 0.06f + 0.0325f, 0.2f + i * 0.05f), Vector3.one * 0.009f, 6, Color.black);   // cooling holes
            mb.Sub(0).Cyl(new Vector3(0, 0.06f, 0.62f), new Vector3(0, 0.06f, 0.8f), 0.014f, 0.014f, 10, metal);
            mb.Sub(0).Cyl(new Vector3(0, 0.06f, 0.8f), new Vector3(0, 0.06f, 0.88f), 0.024f, 0.02f, 10, dark);
            foreach (float s in new[] { -1f, 1f }) mb.Sub(0).Cyl(new Vector3(s * 0.02f, 0.03f, 0.55f), new Vector3(s * 0.03f, 0.03f, 0.2f), 0.008f, 0.008f, 6, dark);   // bipod legs, folded
            mb.Sub(0).Box(new Vector3(0, 0.13f, 0.08f), new Vector3(0.012f, 0.03f, 0.012f), dark, 0.002f);   // front sight
            mb.Sub(0).Box(new Vector3(0, 0.14f, -0.15f), new Vector3(0.03f, 0.02f, 0.02f), dark, 0.003f);    // rear sight
            mb.Sub(2).Box(new Vector3(-0.12f, 0.02f, -0.04f), new Vector3(0.1f, 0.15f, 0.24f), olive, 0.01f, 2);   // ammo can
            for (int i = 0; i < 6; i++) mb.Sub(1).Cyl(new Vector3(-0.07f + i * 0.0f, 0.1f - i * 0.004f, -0.1f + i * 0.02f), new Vector3(-0.03f, 0.1f - i * 0.004f, -0.1f + i * 0.02f), 0.006f, 0.006f, 6, brass);   // the belt going in
            // gun shield: a plate in front of the gunner with a sighting notch
            mb.Sub(2).Box(new Vector3(0, 0.05f, 0.12f), new Vector3(0.5f, 0.3f, 0.015f), olive, 0.01f, 2);
            mb.Sub(2).Box(new Vector3(0, 0.19f, 0.12f), new Vector3(0.12f, 0.02f, 0.016f), olive * 0.8f, 0.003f);
            // spade grips behind the receiver, with a butterfly trigger between them
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Sub(0).Cyl(new Vector3(s * 0.025f, 0.02f, -0.26f), new Vector3(s * 0.075f, 0.02f, -0.3f), 0.01f, 0.01f, 6, dark);
                mb.Sub(0).Cyl(new Vector3(s * 0.075f, 0.06f, -0.34f), new Vector3(s * 0.075f, -0.07f, -0.34f), 0.018f, 0.018f, 10, dark);
            }
            mb.Sub(1).Box(new Vector3(0, -0.02f, -0.3f), new Vector3(0.05f, 0.03f, 0.01f), metal, 0.004f);
            mb.Build("MG", Pivot, Mats.GunMetal, Mats.Lit("mg_brass", Color.white, 0.9f, 0.6f, Mats.Metal, null, 0.1f, 0.2f), Mats.Lit("mg_olive", Color.white, 0.2f, 0.4f, Mats.Noise, null, 0.3f, 0.2f));
            Muzzle = new GameObject("Muzzle").transform; Muzzle.SetParent(Pivot, false); Muzzle.localPosition = new Vector3(0, 0.06f, 0.9f);
            for (int i = 0; i < 2; i++) { _grips[i] = new GameObject("Grip" + i).transform; _grips[i].SetParent(Pivot, false); _grips[i].localPosition = new Vector3((i == 0 ? -1f : 1f) * 0.075f, 0f, -0.34f); }
        }

        void LateUpdate()
        {
            _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 8f);
            // nobody's on it: it drifts back to pointing ahead (unless someone else is shooting it, on their screen)
            bool mine = Vehicle != null && Vehicle.LocalInside && RagoGame.I.Player.Passenger;
            if (!mine && Time.time > _remoteAimUntil) { _yaw = Mathf.MoveTowardsAngle(_yaw, 0f, Time.deltaTime * 40f); _pitch = Mathf.MoveTowards(_pitch, 0f, Time.deltaTime * 20f); }
            Pivot.localRotation = Quaternion.Euler(-_pitch - _kick * 2f, _yaw, 0);
            if (_reloadT > 0f) { _reloadT -= Time.deltaTime; if (_reloadT <= 0f) { _rounds = Belt; Sfx.Play(Synth.MagIn(), transform.position, 0.6f, 0.8f, 1f, 15f); } }
            if (!mine && (_held[0] || _held[1])) Release(RagoGame.I != null ? RagoGame.I.Player : null);
        }

        /// <summary>Aim the gun at a world direction (kept inside its arc).</summary>
        void AimAt(Vector3 worldDir, float dt, float speed)
        {
            Vector3 l = Vehicle.transform.InverseTransformDirection(worldDir);
            float y = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -YawLimit, YawLimit);
            float p = Mathf.Clamp(Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg, PitchMin, PitchMax);
            _yaw = Mathf.MoveTowardsAngle(_yaw, y, dt * speed); _pitch = Mathf.MoveTowards(_pitch, p, dt * speed);
        }

        /// <summary>The passenger's controls, every frame while they sit in the sidecar.</summary>
        public void LocalControl(PlayerRig p, float dt)
        {
            if (p == null) return;
            bool fire = false;
            if (XRInputs.VR)
            {
                for (int i = 0; i < 2; i++)
                {
                    var hand = i == 0 ? p.HandL : p.HandR;
                    var inp = i == 0 ? XRInputs.Left : XRInputs.Right;
                    if (_held[i] && !inp.GripHeld) { _held[i] = false; hand.OnWheel = false; }
                    if (!_held[i] && inp.GripHeld && hand.Held == null && (hand.TrackedPosition - _grips[i].position).sqrMagnitude < 0.2f * 0.2f) { _held[i] = true; hand.Haptic(0.3f, 0.05f); }
                }
                int n = 0; Vector3 mid = Vector3.zero;
                for (int i = 0; i < 2; i++) if (_held[i]) { mid += (i == 0 ? p.HandL : p.HandR).TrackedPosition; n++; }
                if (n > 0)
                {
                    // the gun swings so its grips follow your hands: pull them left and the muzzle goes right, push down to aim up
                    mid /= n;
                    if (n == 1) mid -= Pivot.rotation * new Vector3(_held[0] ? -0.075f : 0.075f, 0, 0);
                    Vector3 d = Pivot.position - mid;
                    float lift = Mathf.Atan2(-GripLocal.y, -GripLocal.z) * Mathf.Rad2Deg;   // the grips sit a little below the bore
                    Vector3 flat = new Vector3(d.x, 0, d.z);
                    float elev = Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg - lift;
                    Vector3 dir = Quaternion.AngleAxis(-elev, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
                    AimAt(dir, dt, 400f);
                    for (int i = 0; i < 2; i++) if (_held[i] && (i == 0 ? XRInputs.Left : XRInputs.Right).Trigger > 0.5f) fire = true;
                }
                // the hands that hold it are drawn on its grips
                for (int i = 0; i < 2; i++)
                {
                    var hand = i == 0 ? p.HandL : p.HandR;
                    if (!_held[i]) continue;
                    hand.OnWheel = true; hand.WheelPos = _grips[i].position; hand.WheelRot = Pivot.rotation * Quaternion.Euler(-80f, 0, i == 0 ? 90f : -90f);
                }
            }
            else
            {
                // desktop: it follows where you look; left mouse fires
                AimAt(p.Head.forward, dt, 300f);
                fire = XRInputs.Right.Trigger > 0.5f;
            }
            _fireT -= dt;
            if (fire && _reloadT <= 0f && _fireT <= 0f)
            {
                if (_rounds <= 0) { _reloadT = 4.5f; p.Toast("Belt empty - reloading the gun"); Sfx.Play(Synth.MagOut(), transform.position, 0.6f, 0.8f, 1f, 15f); return; }
                var g = Gun; if (g == null) return;
                _fireT = 60f / Mathf.Max(100f, g.rpm);
                _rounds--;
                Vector3 dir = Muzzle.forward;
                dir = Quaternion.Euler(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f), 0) * dir;
                Vector3 origin = Muzzle.position;
                Ballistics.Fire(origin, dir, g, 0, Vehicle.transform, p.Vitals, false, _rounds % 3 == 0, false, AmmoType.Standard);
                Fx.MuzzleFlash(Muzzle, 1.4f, false);
                Sfx.Gunshot(g, origin, true);
                NetGame.I?.TurretFire(g.id, origin, dir);
                _kick = 1f;
                for (int i = 0; i < 2; i++) if (_held[i]) (i == 0 ? p.HandL : p.HandR).Haptic(0.5f, 0.04f);
                if (_rounds == 0) { _reloadT = 4.5f; p.Toast("Belt empty - reloading the gun"); }
            }
        }

        public void Release(PlayerRig p)
        {
            _held[0] = _held[1] = false;
            if (p != null) { if (p.HandL != null) p.HandL.OnWheel = false; if (p.HandR != null) p.HandR.OnWheel = false; }
        }

        /// <summary>Another player fired a sidecar gun: the one they're on swings round to that direction and flashes.</summary>
        public static bool RemoteShot(Vector3 origin, Vector3 dir)
        {
            foreach (var c in GolfCart.All)
            {
                if (c == null || c.Turret == null) continue;
                var t = c.Turret;
                if ((t.Muzzle.position - origin).sqrMagnitude > 2.5f * 2.5f) continue;
                t.AimAt(dir, 1f, 9999f); t._remoteAimUntil = Time.time + 1.5f; t._kick = 1f;
                Fx.MuzzleFlash(t.Muzzle, 1.4f, false);
                return true;
            }
            return false;
        }
    }
}
