using UnityEngine;

namespace Rago
{
    /// <summary>
    /// A gun attachment you can pick up: a suppressor / brake / compensator (muzzle), a light or laser (side rail, or
    /// under the frame on a pistol) or a foregrip (under a long gun's handguard). Bring it to that spot on a gun and
    /// let go to mount it; grab it again to take it off. Lights and lasers are on while someone holds the gun.
    /// </summary>
    public class AttachItem : Item
    {
        public AttachDef ad;
        public Gun onGun;
        Transform _emit;
        Light _light;
        LineRenderer _beam;
        Transform _dot;

        public static AttachItem Create(AttachDef def, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(def.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var a = go.AddComponent<AttachItem>();
            a.def = def; a.ad = def;
            a._emit = GunBuilder.BuildAttachment(def, go.transform);
            var mf = go.GetComponentInChildren<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.05f);
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center; col.size = Vector3.Max(b.size, Vector3.one * 0.025f);
            a.localBounds = new Bounds(col.center, col.size);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            a.SetupPhysics(def.slot == AttachSlot.Muzzle && def.model <= 1 ? 0.5f : 0.2f);
            if (def.light && a._emit != null)
            {
                a._light = new GameObject("Light").AddComponent<Light>();
                a._light.transform.SetParent(a._emit, false);
                a._light.type = LightType.Spot; a._light.spotAngle = 48f; a._light.innerSpotAngle = 18f;
                a._light.range = 28f; a._light.intensity = 3.2f; a._light.color = new Color(1f, 0.97f, 0.9f);
                a._light.shadows = LightShadows.None;
                a._light.enabled = false;
            }
            if (def.laser && a._emit != null)
            {
                a._beam = new GameObject("Laser").AddComponent<LineRenderer>();
                a._beam.transform.SetParent(a._emit, false);
                a._beam.useWorldSpace = true; a._beam.positionCount = 2;
                a._beam.widthMultiplier = 0.0025f;
                a._beam.sharedMaterial = Mats.Unlit("laser_" + def.id, new Color(def.beam.r, def.beam.g, def.beam.b, 0.35f), null, Mats.BlendKind.Additive);
                a._beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                a._beam.enabled = false;
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(dot.GetComponent<Collider>());
                dot.transform.localScale = Vector3.one * 0.012f;
                dot.GetComponent<MeshRenderer>().sharedMaterial = Mats.Unlit("laserdot_" + def.id, new Color(def.beam.r * 3f, def.beam.g * 3f, def.beam.b * 3f, 1f), null, Mats.BlendKind.Additive);
                dot.SetActive(false);
                a._dot = dot.transform;
            }
            return a;
        }

        void OnDestroy() { if (_dot != null) Destroy(_dot.gameObject); }

        public void AttachTo(Gun g, bool sound = true)
        {
            var at = g.AttachPoint(ad.slot);
            if (at == null || g.Attached(ad.slot) != null && g.Attached(ad.slot) != this) return;
            onGun = g;
            g.SetAttached(ad.slot, this);
            SetMounted(true);
            SetCollidersTrigger(true);
            transform.SetParent(at, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (sound) Sfx.Play(Synth.MagIn(), transform.position, 0.6f, 1.3f, 0.5f, 8f);
        }

        public override void OnGrab(Hand h)
        {
            if (onGun != null)
            {
                onGun.SetAttached(ad.slot, null); onGun = null; SetMounted(false);
                Sfx.Play(Synth.MagOut(), transform.position, 0.5f, 1.3f, 0.5f, 8f);
            }
            base.OnGrab(h);
        }

        protected override bool TryAttach()
        {
            // near enough: the attachment's mount end or its middle within ~13 cm of the spot (a can is long)
            Gun best = null; float bd = 0.13f;
            Vector3 mid = transform.TransformPoint(localBounds.center);
            foreach (var it in All)
            {
                var g = it as Gun;
                if (g == null || g.Attached(ad.slot) != null) continue;
                var at = g.AttachPoint(ad.slot);
                if (at == null) continue;
                float d = Mathf.Min(Vector3.Distance(at.position, transform.position), Vector3.Distance(at.position, mid));
                if (d < bd) { bd = d; best = g; }
            }
            if (best == null) return false;
            AttachTo(best);
            return true;
        }

        void LateUpdate()
        {
            bool on = onGun != null && onGun.primaryHand != null;
            if (_light != null && _light.enabled != on) _light.enabled = on;
            if (_beam == null) return;
            if (!on) { if (_beam.enabled) { _beam.enabled = false; _dot.gameObject.SetActive(false); } return; }
            Vector3 o = _emit.position, d = onGun.rig.muzzle != null ? onGun.rig.muzzle.forward : _emit.forward;
            int mask = Ballistics.WallMask | (Layers.Npc >= 0 ? 1 << Layers.Npc : 0) | (Layers.Ragdoll >= 0 ? 1 << Layers.Ragdoll : 0);
            bool hit = Physics.Raycast(o, d, out var h, 80f, mask, QueryTriggerInteraction.Ignore);
            Vector3 end = hit ? h.point : o + d * 80f;
            _beam.enabled = true;
            _beam.SetPosition(0, o); _beam.SetPosition(1, end);
            _dot.gameObject.SetActive(hit);
            if (hit) _dot.position = h.point + h.normal * 0.003f;
        }
    }
}
