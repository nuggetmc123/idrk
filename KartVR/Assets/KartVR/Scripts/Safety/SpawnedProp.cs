using Fusion;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// A physics object a moderator spawned from the Spawn Menu: a kart, beach ball, crate, barrel, football or
    /// giant cone. Anyone can drive into it and push it around; the moderator's game runs its physics and everyone
    /// else follows along. Only shown if the spawner is a proven moderator (so a hacked game can't spam them).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SpawnedProp : NetworkBehaviour
    {
        public enum Kind : byte { Kart, BeachBall, Crate, Barrel, Football, GiantCone }
        public static readonly string[] Names = { "Kart", "Beach Ball", "Crate", "Barrel", "Football", "Giant Cone" };

        [Networked] public byte KindIndex { get; set; }
        [Networked] public int Seed { get; set; }
        [Networked] Vector3 NetPosition { get; set; }
        [Networked] Quaternion NetRotation { get; set; }

        Rigidbody rb;
        bool built, allowed = true;
        float nextCheck;

        public override void Spawned()
        {
            rb = GetComponent<Rigidbody>();
            if (!HasStateAuthority)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            // Built on the first update, not here: the spawner sets KindIndex right after spawning.
        }

        void Build()
        {
            if (built) return;
            built = true;
            var kind = (Kind)Mathf.Clamp(KindIndex, 0, Names.Length - 1);
            var t = transform;
            switch (kind)
            {
                case Kind.Kart:
                    var visuals = gameObject.AddComponent<KartVisuals>();
                    var look = KartLook.Unpack(Seed);
                    visuals.Apply(look);
                    Box(new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.6f, 1.9f), 150f, 0f);
                    break;
                case Kind.BeachBall:
                    var ball = KartUI.Part(t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.6f, KartUI.Mat(new Color(1f, 0.3f, 0.3f), 0.7f));
                    KartUI.Part(ball, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.01f, 0.18f, 1.01f), KartUI.Mat(Color.white, 0.7f), new Vector3(0f, 0f, 90f));
                    KartUI.Part(ball, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.01f, 0.18f, 1.01f), KartUI.Mat(new Color(0.2f, 0.5f, 1f), 0.7f), new Vector3(90f, 0f, 0f));
                    Sphere(0.8f, 3f, 0.85f);
                    break;
                case Kind.Crate:
                    KartUI.Part(t, PrimitiveType.Cube, Vector3.zero, Vector3.one * 1.1f, KartUI.Mat(new Color(0.65f, 0.45f, 0.25f), 0.2f));
                    foreach (float y in new[] { -0.45f, 0.45f })
                        KartUI.Part(t, PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(1.13f, 0.12f, 1.13f), KartUI.Mat(new Color(0.45f, 0.3f, 0.15f), 0.2f));
                    Box(Vector3.zero, Vector3.one * 1.1f, 25f, 0.1f);
                    break;
                case Kind.Barrel:
                    KartUI.Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.8f, 0.6f, 0.8f), KartUI.Mat(new Color(0.15f, 0.45f, 0.85f), 0.5f, 0.3f));
                    foreach (float y in new[] { -0.35f, 0.35f })
                        KartUI.Part(t, PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(0.83f, 0.04f, 0.83f), KartUI.Chrome());
                    var cap = gameObject.AddComponent<CapsuleCollider>();
                    cap.radius = 0.4f;
                    cap.height = 1.2f;
                    rb.mass = 20f;
                    break;
                case Kind.Football:
                    KartUI.Part(t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.7f, KartUI.Mat(Color.white, 0.5f));
                    for (int i = 0; i < 6; i++)
                        KartUI.Part(t, PrimitiveType.Sphere, Quaternion.Euler(i * 60f, i * 75f, 0f) * Vector3.forward * 0.3f, Vector3.one * 0.16f, KartUI.Mat(new Color(0.1f, 0.1f, 0.1f), 0.5f));
                    Sphere(0.35f, 1f, 0.7f);
                    break;
                case Kind.GiantCone:
                    KartUI.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.9f), KartUI.Mat(new Color(1f, 0.45f, 0.08f), 0.4f));
                    KartUI.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.93f, 0.12f, 0.93f), KartUI.Mat(Color.white, 0.4f));
                    KartUI.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(1.4f, 0.1f, 1.4f), KartUI.Mat(new Color(0.15f, 0.15f, 0.15f), 0.3f));
                    Box(new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f), 30f, 0.2f);
                    break;
            }
        }

        void Box(Vector3 center, Vector3 size, float mass, float bounce)
        {
            var c = gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            c.sharedMaterial = Bouncy(bounce);
            rb.mass = mass;
        }

        void Sphere(float radius, float mass, float bounce)
        {
            var c = gameObject.AddComponent<SphereCollider>();
            c.radius = radius;
            c.sharedMaterial = Bouncy(bounce);
            rb.mass = mass;
        }

        static PhysicsMaterial Bouncy(float bounce) => new PhysicsMaterial("Prop")
        {
            bounciness = bounce,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            dynamicFriction = 0.5f,
            staticFriction = 0.5f,
        };

        public override void FixedUpdateNetwork()
        {
            if (!built) Build();
            if (!HasStateAuthority) return;
            NetPosition = transform.position;
            NetRotation = transform.rotation;
            // Fell off the map: gone.
            if (transform.position.y < KartMaps.Get(GameState.CurrentMode).center.y - 40f) Runner.Despawn(Object);
        }

        public override void Render()
        {
            if (!built) Build();
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 1f;
                bool ok = HasStateAuthority || SpawnerIsMod();
                if (ok != allowed)
                {
                    allowed = ok;
                    foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = ok;
                    foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = ok;
                }
            }
            if (HasStateAuthority) return;
            float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
            if ((transform.position - NetPosition).sqrMagnitude > 100f) transform.SetPositionAndRotation(NetPosition, NetRotation);
            else transform.SetPositionAndRotation(Vector3.Lerp(transform.position, NetPosition, k), Quaternion.Slerp(transform.rotation, NetRotation, k));
        }

        bool SpawnerIsMod()
        {
            foreach (var kart in KartController.All)
                if (kart != null && kart.IsReady && kart.Object.StateAuthority == Object.StateAuthority) return kart.IsVerifiedMod;
            return false;
        }
    }
}
