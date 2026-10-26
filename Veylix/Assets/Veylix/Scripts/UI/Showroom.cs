using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The menu space: a team garage at dusk. Polished concrete, a back wall with the team's colours, light strips,
    /// tool trolleys and tyre stacks, and your car on a slowly turning platform under spotlights. Rebuilt live
    /// when you change the car or its paint in the Garage.
    /// </summary>
    public class Showroom : MonoBehaviour
    {
        public Vector3 standPoint = new Vector3(0f, 0f, 0f);
        public Transform turntable;
        Car display;
        int shownCar = -1;
        float spin;
        public bool spinning = true;

        static Material M(System.Func<VXAssets, Material> f, string n, Color c, float s = 0.3f) { var A = VXAssets.I; return VXAssets.Or(A != null ? f(A) : null, n, c, s); }

        void Awake() { Build(); }

        void Build()
        {
            transform.position = new Vector3(0f, -200f, 0f); // under the world origin, away from any map
            standPoint = transform.position + new Vector3(0f, 0f, -0.2f);
            var acc = new MeshAcc();
            Vector3 o = transform.position;
            // Floor and ceiling.
            AddPlane(acc, o + new Vector3(0f, 0f, 3f), new Vector2(16f, 16f), Vector3.up, 0.25f);
            acc.Emit(transform, "Floor", M(a => a.pitLane, "Floor", new Color(0.35f, 0.35f, 0.36f), 0.65f), Layers.Default, true);
            AddPlane(acc, o + new Vector3(0f, 6f, 3f), new Vector2(16f, 16f), Vector3.down, 0.25f);
            acc.Emit(transform, "Ceiling", M(a => a.satinBlack, "Ceiling", new Color(0.08f, 0.08f, 0.09f), 0.2f), Layers.Scenery, false);
            // Walls.
            acc.Box(o + new Vector3(0f, 3f, 11f), new Vector3(16f, 6f, 0.3f), Quaternion.identity, 0.25f);
            acc.Box(o + new Vector3(-8f, 3f, 3f), new Vector3(0.3f, 6f, 16f), Quaternion.identity, 0.25f);
            acc.Box(o + new Vector3(8f, 3f, 3f), new Vector3(0.3f, 6f, 16f), Quaternion.identity, 0.25f);
            acc.Box(o + new Vector3(0f, 3f, -5f), new Vector3(16f, 6f, 0.3f), Quaternion.identity, 0.25f);
            acc.Emit(transform, "Walls", M(a => a.buildingOffice, "Walls", new Color(0.62f, 0.63f, 0.66f), 0.3f), Layers.Scenery, false);
            // Team-colour band and a big VEYLIX board on the back wall (from the ad atlas).
            acc.Box(o + new Vector3(0f, 1.2f, 10.8f), new Vector3(15.8f, 0.5f, 0.05f), Quaternion.identity);
            acc.Emit(transform, "Band", M(a => a.kerbRed, "Band", new Color(0.8f, 0.2f, 0.1f), 0.4f), Layers.Scenery, false);
            var board = new MeshAcc();
            int b0 = board.Add(o + new Vector3(-3.2f, 2.4f, 10.82f), Vector3.back, new Vector2(0f, 0.75f));
            int b1 = board.Add(o + new Vector3(3.2f, 2.4f, 10.82f), Vector3.back, new Vector2(0.5f, 0.75f));
            int b2 = board.Add(o + new Vector3(3.2f, 4.0f, 10.82f), Vector3.back, new Vector2(0.5f, 1f));
            int b3 = board.Add(o + new Vector3(-3.2f, 4.0f, 10.82f), Vector3.back, new Vector2(0f, 1f));
            board.Quad(b0, b1, b2, b3, Vector3.back);
            board.Emit(transform, "Board", M(a => a.ads, "Board", Color.white, 0.4f), Layers.Scenery, false);
            // Light strips.
            var strips = new MeshAcc();
            for (int i = -2; i <= 2; i++) strips.Box(o + new Vector3(i * 2.6f, 5.85f, 3f), new Vector3(0.18f, 0.06f, 9f), Quaternion.identity);
            strips.Emit(transform, "LightStrips", M(a => a.lampHead, "Strip", Color.white, 0.5f), Layers.Scenery, false);
            // Tyre stacks and a tool trolley for some life.
            var tyres = new MeshKit();
            for (int s = 0; s < 3; s++)
                for (int k = 0; k < 4; k++)
                {
                    tyres.matrix = Matrix4x4.TRS(new Vector3(-6.2f + s * 0.75f, 0.14f + k * 0.29f, 8.5f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                    tyres.Lathe(0, new[] { new Vector2(0.2f, -0.14f), new Vector2(0.33f, -0.15f), new Vector2(0.36f, -0.1f), new Vector2(0.36f, 0.1f), new Vector2(0.33f, 0.15f), new Vector2(0.2f, 0.14f) }, 20);
                }
            var tg = new GameObject("Tyres");
            tg.transform.SetParent(transform, false);
            tg.transform.position = o;
            tg.AddComponent<MeshFilter>().sharedMesh = tyres.Build("tyres");
            var A2 = VXAssets.I;
            tg.AddComponent<MeshRenderer>().sharedMaterial = A2 != null && A2.tyres != null && A2.tyres.Length > 1 && A2.tyres[1] != null ? A2.tyres[1] : M(a => a.satinBlack, "Tyre", new Color(0.05f, 0.05f, 0.05f), 0.25f);
            var trolley = new MeshAcc();
            trolley.Box(o + new Vector3(5.5f, 0.5f, 8.6f), new Vector3(1.2f, 1f, 0.6f), Quaternion.identity, 0.5f);
            trolley.Box(o + new Vector3(5.5f, 1.02f, 8.6f), new Vector3(1.25f, 0.04f, 0.65f), Quaternion.identity, 0.5f);
            trolley.Emit(transform, "Trolley", M(a => a.kerbRed, "Trolley", new Color(0.7f, 0.1f, 0.08f), 0.5f), Layers.Scenery, false);
            // Turntable.
            var tt = new MeshKit();
            tt.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            tt.Lathe(0, new[] { new Vector2(3.2f, 0f), new Vector2(3.2f, 0.06f), new Vector2(3.1f, 0.08f), new Vector2(0.01f, 0.08f) }, 48);
            tt.matrix = Matrix4x4.identity;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(transform, false);
            turntable.position = o + new Vector3(0f, 0f, 4.4f);
            turntable.gameObject.AddComponent<MeshFilter>().sharedMesh = tt.Build("turntable");
            turntable.gameObject.AddComponent<MeshRenderer>().sharedMaterial = M(a => a.metal, "Turntable", new Color(0.3f, 0.3f, 0.32f), 0.75f);
            // Lights.
            AddLight(o + new Vector3(-3f, 5.5f, 2.5f), new Vector3(60f, 30f, 0f), 2.2f, new Color(1f, 0.96f, 0.9f));
            AddLight(o + new Vector3(3f, 5.5f, 2.5f), new Vector3(60f, -30f, 0f), 2.2f, new Color(1f, 0.96f, 0.9f));
            AddLight(o + new Vector3(0f, 5.5f, 8f), new Vector3(120f, 0f, 0f), 1.4f, new Color(0.8f, 0.88f, 1f));
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.transform.position = o + new Vector3(0f, 3.5f, 0f);
            fill.type = LightType.Point; fill.range = 14f; fill.intensity = 0.6f; fill.color = new Color(1f, 0.9f, 0.8f);
        }

        void AddLight(Vector3 p, Vector3 euler, float intensity, Color c)
        {
            var l = new GameObject("Spot").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.position = p;
            l.transform.rotation = Quaternion.Euler(euler);
            l.type = LightType.Spot;
            l.spotAngle = 70f;
            l.range = 14f;
            l.intensity = intensity;
            l.color = c;
            l.shadows = LightShadows.Soft;
        }

        static void AddPlane(MeshAcc acc, Vector3 c, Vector2 size, Vector3 n, float uv)
        {
            Vector3 r = Vector3.right * size.x * 0.5f, f = Vector3.forward * size.y * 0.5f;
            int a = acc.Add(c - r - f, n, new Vector2(0, 0)), b = acc.Add(c + r - f, n, new Vector2(size.x * uv, 0));
            int d = acc.Add(c + r + f, n, new Vector2(size.x * uv, size.y * uv)), e = acc.Add(c - r + f, n, new Vector2(0, size.y * uv));
            acc.Quad(a, b, d, e, n);
        }

        public void Show(int carIndex, CarSetup setup = null)
        {
            if (display != null) CarFactory.Despawn(display);
            shownCar = carIndex;
            var spec = CarCatalog.Get(carIndex);
            var s = setup ?? SaveData.Setup(carIndex);
            display = CarFactory.Spawn(spec, s, false, "Display", 99, turntable.position + Vector3.up * 0.08f, turntable.rotation * Quaternion.Euler(0f, 210f, 0f));
            display.transform.SetParent(turntable, true);
            display.Freeze(true);
            var au = display.GetComponentInChildren<CarAudio>();
            if (au != null) au.enabled = false;
            var src = display.GetComponentInChildren<AudioSource>();
            if (src != null) src.enabled = false;
            CarFactory.All.Remove(display);
        }

        void Update()
        {
            if (turntable != null && spinning) turntable.Rotate(0f, 9f * Time.deltaTime, 0f);
            Cameras.Main = Rig.I != null ? Rig.I.cam : Cameras.Main;
        }

        void OnDisable()
        {
            if (display != null) { Destroy(display.gameObject); display = null; }
        }

        void OnEnable()
        {
            if (shownCar >= 0 && display == null && turntable != null) Show(shownCar);
        }
    }
}
