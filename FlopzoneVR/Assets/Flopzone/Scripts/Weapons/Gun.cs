using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>A gun the local player can hold and fire.</summary>
    public class Gun : Grabbable
    {
        public GunDef def;
        public GunModel model;
        public int ammo;
        public bool hasMag = true;
        GameObject magVisual;
        float nextShot;
        int burstLeft;
        float kick;
        Camera scopeCam;
        RenderTexture scopeTex;
        GameObject scopeLens;
        /// <summary>Hub rack guns are for trying out; they don't count as your loadout.</summary>
        public bool displayOnly;
        TextMesh ammoText;
        int shownAmmo = -2;

        public static Gun Spawn(GunDef def, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Gun " + def.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var model = GunModel.Build(def, go.transform);
            // one box around the whole gun for grabbing and dropping
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                var lc = go.transform.InverseTransformPoint(b.center);
                var ls = go.transform.InverseTransformVector(b.size);
                var lb = new Bounds(lc, new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
                if (first) { bounds = lb; first = false; } else bounds.Encapsulate(lb);
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = bounds.center;
            col.size = bounds.size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = def.cls == GunClass.Pistol ? 1f : 3.5f;
            var gun = go.AddComponent<Gun>();
            gun.def = def;
            gun.model = model;
            gun.ammo = def.magSize;
            gun.ShowMag(true);
            gun.ammoText = UIKit.Text(model.ammoAnchor, "", Vector3.zero, 0.02f, Color.white);
            return gun;
        }

        void ShowMag(bool on)
        {
            hasMag = on;
            // a revolver's speed loader goes away once it's loaded (the rounds show in the cylinder)
            if (on && magVisual == null && !def.revolver)
            {
                magVisual = GunModel.BuildMag(def, model.magwell);
                magVisual.name = "mag";
            }
            if (!on && magVisual != null) { Destroy(magVisual); magVisual = null; }
        }

        public override void OnGrab(Hand h)
        {
            base.OnGrab(h);
            if (model.scopeEye != null) EnableScope(true);   // snipers, the ACOG, the AUG's built-in scope
        }

        public override void OnRelease(Hand h, Vector3 velocity, Vector3 angular)
        {
            EnableScope(false);
            burstLeft = 0;
            base.OnRelease(h, velocity, angular);
        }

        void OnDestroy()
        {
            if (scopeTex != null) scopeTex.Release();
        }

        static Texture2D scopeMask;

        /// <summary>The scope's round edge and crosshair: clear inside a circle, black outside, with thin lines and mil dots.</summary>
        static Texture2D ScopeMask()
        {
            if (scopeMask != null) return scopeMask;
            const int n = 256;
            scopeMask = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "fz_scope_mask" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.9f) / 0.06f);                       // soft dark edge
                    float line = Mathf.Abs(dx) < 0.006f || Mathf.Abs(dy) < 0.006f ? 1f : 0f;
                    if (r < 0.06f) line = 0f;                                          // gap in the middle
                    if ((Mathf.Abs(dx) < 0.02f && Mathf.Abs(dy) > 0.45f) || (Mathf.Abs(dy) < 0.02f && Mathf.Abs(dx) > 0.45f)) line = 1f;   // thick posts
                    for (int i = 1; i <= 3; i++)                                       // mil dots
                    {
                        float m = i * 0.13f;
                        if ((dx * dx + (Mathf.Abs(dy) - m) * (Mathf.Abs(dy) - m) < 0.0002f) || ((Mathf.Abs(dx) - m) * (Mathf.Abs(dx) - m) + dy * dy < 0.0002f)) line = 1f;
                    }
                    byte alpha = (byte)(Mathf.Max(a, line * 0.9f) * 255);
                    px[y * n + x] = r < 0.012f ? new Color32(255, 40, 30, 255) : new Color32(0, 0, 0, alpha);   // red centre dot
                }
            scopeMask.SetPixels32(px);
            scopeMask.Apply();
            return scopeMask;
        }

        Material scopeGlass;

        /// <summary>
        /// A real scope: a camera at the front lens with a narrow field of view draws into a texture
        /// shown on the back lens, under a round black edge with a crosshair. It only renders while
        /// your eye is up close (it's a whole extra view of the world, so it's skipped otherwise).
        /// </summary>
        void EnableScope(bool on)
        {
            if (model.scopeEye == null) return;
            if (on && scopeCam == null)
            {
                scopeTex = new RenderTexture(384, 384, 16) { name = "scope" };
                var camGo = new GameObject("scopeCam");
                camGo.transform.SetParent(model.scopeFront != null ? model.scopeFront : model.scopeEye, false);
                scopeCam = camGo.AddComponent<Camera>();
                scopeCam.enabled = false;
                scopeCam.targetTexture = scopeTex;
                scopeCam.cullingMask = ~(1 << Layers.MirrorOnly);
                // more zoom on the big rifles
                scopeCam.fieldOfView = model.scopeFov;
                scopeCam.nearClipPlane = 0.05f;
                scopeCam.farClipPlane = 300f;
                scopeCam.stereoTargetEye = StereoTargetEyeMask.None;
                scopeCam.allowHDR = false;
                scopeCam.allowMSAA = false;
                // the picture (drawn after the solid parts of the gun, so they can't cover it)
                float d = model.scopeRadius * 1.8f;
                scopeLens = UIKit.Quad(model.scopeEye, Vector3.zero, new Vector2(d, d), Color.white, 2995);
                scopeLens.transform.localRotation = Quaternion.identity;
                scopeGlass = scopeLens.GetComponent<Renderer>().sharedMaterial;
                scopeGlass.mainTexture = scopeTex;
                var mask = UIKit.Quad(scopeLens.transform, new Vector3(0, 0, -0.0005f), Vector2.one, Color.white, 2996);
                mask.GetComponent<Renderer>().sharedMaterial.mainTexture = ScopeMask();
            }
            if (scopeLens) scopeLens.SetActive(on);
        }

        /// <summary>Render the scope view this frame if an eye is close to it.</summary>
        void UpdateScope()
        {
            if (scopeCam == null || scopeLens == null || !scopeLens.activeSelf) return;
            var rig = PlayerRig.I;
            bool looking = rig != null && Vector3.Distance(rig.head.position, model.scopeEye.position) < 0.3f
                           && Vector3.Dot(rig.head.forward, model.scopeEye.forward) > 0.6f;
            if (looking)
            {
                scopeCam.Render();
                scopeGlass.color = Color.white;
            }
            else scopeGlass.color = new Color(0.12f, 0.16f, 0.2f);   // just glass when you're not looking through it
        }

        void LateUpdate() => UpdateScope();

        protected override void Update()
        {
            base.Update();
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
            model.visual.localRotation = Quaternion.Euler(-def.recoil * kick, 0, 0);
            model.visual.localPosition = new Vector3(0, 0, -0.04f * kick);
            if (ammoText != null)
            {
                int show = Held ? (hasMag ? ammo : -1) : -2;
                if (show != shownAmmo)
                {
                    shownAmmo = show;
                    ammoText.text = show == -2 ? "" : show == -1 ? "--" : show.ToString();
                    ammoText.color = show <= Mathf.Max(1, def.magSize / 4) ? UIKit.Bad : Color.white;
                }
            }
            if (!Held || holder == null) return;

            var input = VRInput.I;
            int h = holder.index;
            bool canShoot = holder.rig != null && holder.rig.CanShoot;
            if (!canShoot) { burstLeft = 0; return; }

            bool down = input.TriggerDown(h);
            bool held = input.Trigger(h);
            switch (def.mode)
            {
                case FireMode.Auto:
                    if (held) TryFire();
                    break;
                case FireMode.Burst:
                    if (down && burstLeft == 0 && Time.time >= nextShot) burstLeft = 3;
                    if (burstLeft > 0 && Time.time >= nextShot)
                    {
                        if (!TryFire()) burstLeft = 0;
                        else if (--burstLeft == 0) nextShot = Time.time + 0.28f;
                    }
                    break;
                default:
                    if (down) TryFire();
                    break;
            }
            if (input.DesktopReloadPressed) QuickReload();
        }

        bool TryFire()
        {
            if (Time.time < nextShot) return false;
            nextShot = Time.time + def.Interval;
            if (!hasMag || ammo <= 0)
            {
                AudioManager.Sfx("dry", model.muzzle.position, 0.7f);
                nextShot = Time.time + 0.25f;
                return false;
            }
            ammo--;
            Fire();
            return true;
        }

        void Fire()
        {
            var muzzle = model.muzzle;
            var origin = muzzle.position;
            var rig = holder.rig;
            var hits = new Dictionary<RemotePlayer, (float dmg, bool head, Vector3 dir)>();
            Vector3 firstEnd = origin + muzzle.forward * def.range;
            bool hitSomeone = false;
            for (int p = 0; p < def.pellets; p++)
            {
                var spread = Random.insideUnitCircle * Mathf.Tan(def.spread * Mathf.Deg2Rad);
                var dir = (muzzle.forward + muzzle.right * spread.x + muzzle.up * spread.y).normalized;
                Vector3 end = origin + dir * def.range;
                var all = Physics.RaycastAll(origin, dir, def.range, Layers.ShootMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in all)
                {
                    if (rig != null && hit.collider.transform.IsChildOf(rig.transform)) continue;
                    if (hit.collider.GetComponentInParent<Grabbable>() != null && hit.collider.GetComponentInParent<Grabbable>().Held) continue;
                    end = hit.point;
                    var hb = hit.collider.GetComponent<Hitbox>();
                    if (hb != null && hb.owner != null && hb.owner.alive)
                    {
                        float falloff = Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(def.range * 0.35f, def.range, hit.distance));
                        float dmg = def.damage * falloff * (hb.head ? def.headMult : 1f);
                        hits.TryGetValue(hb.owner, out var cur);
                        hits[hb.owner] = (cur.dmg + dmg, cur.head || hb.head, dir);
                        Fx.I.Blood(hit.point, dir, hb.head ? 1.4f : 0.8f);
                        hitSomeone = true;
                    }
                    else
                    {
                        var target = hit.collider.GetComponentInParent<ShootingTarget>();
                        if (target != null) { target.Hit(hit.point, dir); hitSomeone = true; }
                        var rbHit = hit.rigidbody;
                        if (rbHit != null && !rbHit.isKinematic) rbHit.AddForceAtPosition(dir * def.damage * 0.12f, hit.point, ForceMode.Impulse);
                        if (hit.collider.GetComponentInParent<RagdollLife>() != null) Fx.I.Blood(hit.point, dir, 0.6f);
                        else if (target == null) Fx.I.Impact(hit.point, hit.normal);
                    }
                    break;
                }
                if (p == 0) firstEnd = end;
                if (p < 3) Fx.I.Tracer(origin, end);
            }
            foreach (var kv in hits)
                Game.I.ReportHit(kv.Key, kv.Value.dmg, kv.Value.head, def, kv.Value.dir);
            if (hitSomeone) AudioManager.Ui(hits.Count > 0 && HasHead(hits) ? "hit_head" : "hitmarker", 0.8f);

            Fx.I.MuzzleFlash(muzzle, def.cls == GunClass.Shotgun ? 1.6f : def.cls == GunClass.Sniper ? 1.4f : 1f);
            AudioManager.Sfx(def.sound, origin, 1f, def.pitch);
            if (def.cls != GunClass.Shotgun || def.mode != FireMode.Pump) Fx.I.Shell(model.ejectPort, holder.Velocity);
            if (def.mode == FireMode.Bolt || def.mode == FireMode.Pump) Invoke(nameof(CycleSound), Mathf.Min(0.35f, def.Interval * 0.4f));
            kick = 1f;
            VRInput.I.Haptic(holder.index, def.cls == GunClass.Pistol ? 0.4f : 0.8f, def.cls == GunClass.AssaultRifle ? 0.04f : 0.09f);
            Game.I.ReportShot(holder.index, origin, firstEnd, def.index);
        }

        static bool HasHead(Dictionary<RemotePlayer, (float dmg, bool head, Vector3 dir)> hits)
        {
            foreach (var kv in hits) if (kv.Value.head) return true;
            return false;
        }

        void CycleSound()
        {
            AudioManager.Sfx("slide", model.muzzle.position, 0.8f);
            if (def.pump && model.pumpPart != null) kick = Mathf.Max(kick, 0.5f);
            if (def.mode == FireMode.Pump) Fx.I.Shell(model.ejectPort, holder != null ? holder.Velocity : Vector3.zero);
        }

        /// <summary>Drop the mag out of the gun (B button).</summary>
        public void DropMag()
        {
            if (!hasMag) return;
            var pos = model.magwell.position;
            var rot = model.magwell.rotation;
            ShowMag(false);
            var mag = Magazine.Spawn(def, ammo, pos, rot);
            mag.OnRelease(null, holder != null ? holder.Velocity + model.magwell.up * -1.2f : Vector3.down, Vector3.zero);
            ammo = 0;
            AudioManager.Sfx("mag_out", pos, 0.9f);
        }

        /// <summary>Pull the mag out into a hand (grabbing it from the gun).</summary>
        public Magazine TakeMag()
        {
            if (!hasMag) return null;
            var mag = Magazine.Spawn(def, ammo, model.magwell.position, model.magwell.rotation);
            ShowMag(false);
            ammo = 0;
            AudioManager.Sfx("mag_out", model.magwell.position, 0.9f);
            return mag;
        }

        public bool CanTake(Magazine mag) => !hasMag && mag != null && mag.def == def;

        public void InsertMag(Magazine mag)
        {
            ammo = mag.ammo;
            ShowMag(true);
            AudioManager.Sfx("mag_in", model.magwell.position, 1f);
            Invoke(nameof(CycleSound), 0.2f);
            if (holder != null) VRInput.I.Haptic(holder.index, 0.5f, 0.05f);
            Destroy(mag.gameObject);
        }

        /// <summary>Keyboard testing only: instant reload.</summary>
        public void QuickReload()
        {
            ammo = def.magSize;
            ShowMag(true);
            AudioManager.Sfx("mag_in", model.magwell.position, 1f);
        }

        public void Refill()
        {
            ammo = def.magSize;
            ShowMag(true);
        }
    }
}
