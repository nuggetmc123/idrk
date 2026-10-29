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
            if (on && magVisual == null)
            {
                magVisual = GunModel.BuildMag(def, model.magwell);
                magVisual.name = "mag";
            }
            if (!on && magVisual != null) { Destroy(magVisual); magVisual = null; }
        }

        public override void OnGrab(Hand h)
        {
            base.OnGrab(h);
            if (def.scope) EnableScope(true);
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

        void EnableScope(bool on)
        {
            if (model.scopeEye == null) return;
            if (on && scopeCam == null)
            {
                scopeTex = new RenderTexture(256, 256, 16) { name = "scope" };
                var camGo = new GameObject("scopeCam");
                camGo.transform.SetParent(model.scopeEye, false);
                camGo.transform.localPosition = new Vector3(0, 0, 0.25f);
                scopeCam = camGo.AddComponent<Camera>();
                scopeCam.targetTexture = scopeTex;
                scopeCam.fieldOfView = 7f;
                scopeCam.nearClipPlane = 0.05f;
                scopeCam.farClipPlane = 250f;
                scopeCam.stereoTargetEye = StereoTargetEyeMask.None;
                scopeCam.depth = -5;
                scopeLens = UIKit.Quad(model.scopeEye, new Vector3(0, 0, 0.001f), new Vector2(0.034f, 0.034f), Color.white, 2000);
                scopeLens.transform.localRotation = Quaternion.identity;
                var m = scopeLens.GetComponent<Renderer>().sharedMaterial;
                m.mainTexture = scopeTex;
                // crosshair
                var reticle = UIKit.Text(scopeLens.transform, "+", new Vector3(0, 0, -0.01f), 1.2f, new Color(0, 0, 0, 0.8f));
                reticle.transform.localScale = Vector3.one;
            }
            if (scopeCam != null)
            {
                scopeCam.enabled = on;
                if (scopeLens) scopeLens.SetActive(on);
            }
        }

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
