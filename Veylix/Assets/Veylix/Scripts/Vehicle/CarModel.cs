using System.Collections.Generic;
using UnityEngine;
using S = Veylix.MeshKit.Station;

namespace Veylix
{
    /// <summary>
    /// Builds a car's 3D model from its spec: smooth lofted bodywork (nose, tub, sidepods, engine cover), multi-element
    /// aerofoil wings, the halo, a floor with plank and diffuser, suspension arms, proper tyres with sidewall
    /// markings, rims, brake discs and calipers, mirrors, lights, a helmeted driver and a working cockpit (wheel with
    /// display, paddles, padding). Proportions follow the car's era so the seven cars read as different machines.
    /// Also adds the physics colliders.
    /// </summary>
    public static class CarModel
    {
        // Material slots of the body mesh.
        const int Paint = 0, Carbon = 1, Satin = 2, Interior = 3, Pad = 4, Plank = 5, Metal = 6, Red = 7, White = 8;

        struct Shape
        {
            public float hb, noseZ, tailZ, headZ, eyeY, rimY, tubHW, noseY, noseHigh;
            public float podHW, podHH, podFront, podBack, podX;
            public float coverY, coverHW, airboxY;
            public float fwSpan, fwY, fwZ, rwSpan, rwY, rwZ, rwChord;
            public bool halo, classic, modern, midera, exposedEngine;
        }

        public static CarParts Build(Car car, bool cockpitDetail)
        {
            var spec = car.spec;
            var setup = car.setup;
            var root = car.transform;
            var parts = Obj.Need<CarParts>(root.gameObject);
            var A = VXAssets.I;

            Shape sh = ShapeFor(spec);

            // ---- per-car materials
            var paintMat = new Material(VXAssets.Or(A != null ? A.paint : null, "Paint", Color.white, 0.82f)) { name = car.driverName + " paint" };
            string sponsor = Teams.SponsorFor(car), sponsor2 = Teams.Sponsor2For(car);
            parts.livery = LiveryPainter.Paint(spec, setup, sponsor, sponsor2);
            paintMat.mainTexture = parts.livery;
            paintMat.color = Color.white;
            parts.paint = paintMat;
            var rimMat = new Material(VXAssets.Or(A != null ? A.rimMetal : null, "Rim", Color.gray, 0.7f));
            rimMat.color = RimColor(setup, spec);
            parts.rim = rimMat;
            var carbon = VXAssets.Or(A != null ? A.carbon : null, "Carbon", new Color(0.08f, 0.08f, 0.09f), 0.6f);
            var satin = VXAssets.Or(A != null ? A.satinBlack : null, "Satin", new Color(0.05f, 0.05f, 0.055f), 0.35f);
            var interior = VXAssets.Or(A != null ? A.interior : null, "Interior", new Color(0.03f, 0.03f, 0.035f), 0.15f);
            var pad = VXAssets.Or(A != null ? A.padding : null, "Padding", new Color(0.12f, 0.2f, 0.42f), 0.2f);
            var plank = VXAssets.Or(A != null ? A.plank : null, "Plank", new Color(0.55f, 0.45f, 0.3f), 0.25f);
            var metal = VXAssets.Or(A != null ? A.metal : null, "Metal", new Color(0.6f, 0.6f, 0.62f), 0.7f);
            var red = VXAssets.Or(A != null ? A.lightRed : null, "Rain light", new Color(0.4f, 0f, 0f), 0.6f);
            var white = VXAssets.Or(A != null ? A.lightWhite : null, "Head light", Color.white, 0.6f);
            var bodyMats = new[] { paintMat, carbon, satin, interior, pad, plank, metal, red, white };

            // ---- body
            var k = new MeshKit();
            BuildChassis(k, sh, spec);
            BuildSidepods(k, sh, spec);
            BuildCover(k, sh, spec);
            BuildFloor(k, sh, spec);
            if (sh.halo) BuildHalo(k, sh);
            else BuildRollHoopScreen(k, sh);
            BuildCockpitInterior(k, sh, cockpitDetail);
            BuildLights(k, sh);
            var body = MakeObject(root, "Body", k.Build(car.driverName + " body", bodyMats.Length), bodyMats);
            parts.bodyRenderers = new[] { body.GetComponent<Renderer>() };
            // Rear rain light (flashes in the wet) under the rear wing: its own renderer so it can blink.
            var rl = new MeshKit();
            rl.Box(0, new Vector3(0f, 0.36f, sh.tailZ + (sh.classic ? 0.05f : 0.18f)), new Vector3(0.16f, 0.035f, 0.02f));
            var rainLight = MakeObject(root, "RainLight", rl.Build("rain light"), new[] { red });
            parts.rainLight = rainLight.GetComponent<Renderer>();
            car.parts = parts;

            // ---- wings (separate so they can break off)
            parts.frontWingL = BuildFrontWing(root, sh, spec, -1, paintMat, carbon);
            parts.frontWingR = BuildFrontWing(root, sh, spec, +1, paintMat, carbon);
            parts.rearWing = BuildRearWing(root, sh, spec, setup, paintMat, carbon);

            // ---- wheels, uprights, suspension
            BuildWheels(car, parts, sh, rimMat, carbon, metal, setup);

            // ---- driver, mirrors, cockpit
            parts.helmet = BuildHelmet(root, sh, car, parts);
            BuildMirrors(root, sh, parts, carbon);
            BuildSteeringWheel(root, sh, spec, parts, carbon, satin, metal);
            // The driver after the wheel, so the hands start on the rim.
            float recline = sh.modern ? 1f : sh.midera ? 0.65f : sh.classic ? 0f : spec.body == BodyStyle.Junior ? 0.7f : 0.2f;
            var look = car.driverLook;
            parts.driver = DriverBody.Build(car, parts, root, sh.eyeY, sh.headZ, recline,
                look != null ? look.Suit : setup.Primary(spec), look != null ? look.SuitAccent : setup.Secondary(spec), look != null ? look.Gloves : new Color(0.04f, 0.04f, 0.045f));
            if (spec.hasHandbrake) BuildHandbrake(root, sh, parts, metal, satin);

            parts.eyePoint = new GameObject("EyePoint").transform;
            parts.eyePoint.SetParent(root, false);
            parts.eyePoint.localPosition = new Vector3(0f, sh.eyeY, sh.headZ + 0.06f);
            parts.chaseAnchor = new GameObject("ChaseAnchor").transform;
            parts.chaseAnchor.SetParent(root, false);
            parts.chaseAnchor.localPosition = new Vector3(0f, 1.6f, sh.tailZ - 4.8f);
            parts.enginePoint = new Vector3(0f, sh.coverY, sh.headZ - 0.9f);

            BuildColliders(car, sh);
            parts.Remember();
            Layers.SetRecursive(root.gameObject, Layers.Car);
            foreach (var w in car.wheels) if (w.steerPivot != null) Layers.SetRecursive(w.steerPivot.gameObject, Layers.Car);
            foreach (Transform t in root) if (t.name.StartsWith("Bumper")) t.gameObject.layer = Layers.CarWheel;
            return parts;
        }

        static Color RimColor(CarSetup setup, CarSpec spec)
        {
            switch (setup.rimColor)
            {
                case 1: return new Color(0.28f, 0.29f, 0.31f);
                case 2: return new Color(0.78f, 0.6f, 0.25f);
                case 3: return new Color(0.75f, 0.76f, 0.78f);
                case 4: return setup.Primary(spec);
                default: return new Color(0.04f, 0.04f, 0.045f);
            }
        }

        static GameObject MakeObject(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var used = new Material[mesh.subMeshCount];
            for (int i = 0; i < used.Length; i++) used[i] = mats[Mathf.Min(i, mats.Length - 1)];
            r.sharedMaterials = used;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        // ------------------------------------------------------------------ proportions

        static Shape ShapeFor(CarSpec s)
        {
            float hb = s.wheelbase * 0.5f;
            float over = s.length - s.wheelbase;
            var sh = new Shape { hb = hb, noseZ = hb + over * 0.58f, tailZ = -hb - over * 0.42f };
            switch (s.body)
            {
                case BodyStyle.Modern:
                    sh.modern = true; sh.halo = true;
                    sh.headZ = -hb + 1.72f; sh.eyeY = 0.8f; sh.rimY = 0.67f; sh.tubHW = 0.34f; sh.noseY = 0.17f;
                    sh.podHW = 0.2f; sh.podHH = 0.17f; sh.podX = 0.56f; sh.podFront = sh.headZ + 0.55f; sh.podBack = -hb + 0.25f;
                    sh.coverY = 0.55f; sh.coverHW = 0.3f; sh.airboxY = 0.88f;
                    sh.fwSpan = 1.0f; sh.fwY = 0.08f; sh.fwZ = sh.noseZ + 0.02f;
                    sh.rwSpan = 0.5f; sh.rwY = 0.8f; sh.rwZ = sh.tailZ + 0.42f; sh.rwChord = 0.34f;
                    break;
                case BodyStyle.Midera:
                    sh.midera = true; sh.halo = false; sh.noseHigh = 1f;
                    sh.headZ = -hb + 1.5f; sh.eyeY = 0.84f; sh.rimY = 0.7f; sh.tubHW = 0.31f; sh.noseY = 0.42f;
                    sh.podHW = 0.2f; sh.podHH = 0.19f; sh.podX = 0.5f; sh.podFront = sh.headZ + 0.62f; sh.podBack = -hb + 0.3f;
                    sh.coverY = 0.57f; sh.coverHW = 0.28f; sh.airboxY = 0.95f;
                    sh.fwSpan = 0.9f; sh.fwY = 0.1f; sh.fwZ = sh.noseZ - 0.02f;
                    sh.rwSpan = 0.42f; sh.rwY = 0.85f; sh.rwZ = sh.tailZ + 0.35f; sh.rwChord = 0.3f;
                    break;
                case BodyStyle.Classic:
                    sh.classic = true; sh.halo = false; sh.exposedEngine = true;
                    sh.headZ = -hb + 0.92f; sh.eyeY = 0.92f; sh.rimY = 0.66f; sh.tubHW = 0.3f; sh.noseY = 0.22f;
                    sh.podHW = 0.14f; sh.podHH = 0.15f; sh.podX = 0.42f; sh.podFront = sh.headZ + 0.35f; sh.podBack = -hb + 0.25f;
                    sh.coverY = 0.48f; sh.coverHW = 0.22f; sh.airboxY = 1.05f;
                    sh.fwSpan = 0.78f; sh.fwY = 0.14f; sh.fwZ = sh.noseZ - 0.05f;
                    sh.rwSpan = 0.55f; sh.rwY = 0.92f; sh.rwZ = sh.tailZ + 0.25f; sh.rwChord = 0.36f;
                    break;
                case BodyStyle.Junior:
                    sh.halo = true;
                    sh.headZ = -hb + 1.18f; sh.eyeY = 0.86f; sh.rimY = 0.69f; sh.tubHW = 0.32f; sh.noseY = 0.24f;
                    sh.podHW = 0.17f; sh.podHH = 0.16f; sh.podX = 0.47f; sh.podFront = sh.headZ + 0.5f; sh.podBack = -hb + 0.3f;
                    sh.coverY = 0.52f; sh.coverHW = 0.26f; sh.airboxY = 0.9f;
                    sh.fwSpan = 0.82f; sh.fwY = 0.1f; sh.fwZ = sh.noseZ;
                    sh.rwSpan = 0.42f; sh.rwY = 0.78f; sh.rwZ = sh.tailZ + 0.3f; sh.rwChord = 0.28f;
                    break;
                default: // Hillclimb
                    sh.halo = false;
                    sh.headZ = -hb + 1.15f; sh.eyeY = 0.97f; sh.rimY = 0.76f; sh.tubHW = 0.34f; sh.noseY = 0.3f;
                    sh.podHW = 0.2f; sh.podHH = 0.17f; sh.podX = 0.5f; sh.podFront = sh.headZ + 0.48f; sh.podBack = -hb + 0.3f;
                    sh.coverY = 0.6f; sh.coverHW = 0.28f; sh.airboxY = 1.02f;
                    sh.fwSpan = 0.95f; sh.fwY = 0.16f; sh.fwZ = sh.noseZ;
                    sh.rwSpan = 0.55f; sh.rwY = 1.05f; sh.rwZ = sh.tailZ + 0.3f; sh.rwChord = 0.38f;
                    break;
            }
            // Ride height setting lifts the bodywork with the car (Car.RideOffset is applied by the physics).
            return sh;
        }

        // ------------------------------------------------------------------ chassis: nose + tub

        static void BuildChassis(MeshKit k, Shape sh, CarSpec spec)
        {
            float cockpitFront = sh.headZ + 0.78f;
            float rideY = spec.rideHeight;
            S[] nose;
            if (sh.noseHigh > 0f)
            {
                // Raised nose (2000s): the nose sits high, the wing hangs underneath on pylons.
                nose = new[]
                {
                    new S(sh.noseZ, sh.noseY - 0.06f, 0.06f, 0.05f, 2.4f, 0, 0.3f),
                    new S(sh.noseZ - 0.35f, sh.noseY - 0.01f, 0.11f, 0.08f, 2.6f, 0, 0.3f),
                    new S(sh.hb + 0.1f, sh.noseY + 0.03f, 0.16f, 0.12f, 3f, 0, 0.4f),
                    new S(sh.hb - 0.5f, 0.48f, 0.22f, 0.16f, 3.4f, 0, 0.5f),
                    new S(cockpitFront + 0.15f, 0.47f, 0.29f, 0.21f, 3.6f, 0, 0.5f),
                    new S(cockpitFront - 0.05f, 0.45f, sh.tubHW, 0.23f, 3.6f, 0, 0.6f),
                };
            }
            else if (sh.classic)
            {
                // 1970s: a flat wedge nose that widens into the tub.
                nose = new[]
                {
                    new S(sh.noseZ, sh.noseY, 0.22f, 0.05f, 4f, 0, 0.7f),
                    new S(sh.noseZ - 0.35f, sh.noseY + 0.04f, 0.24f, 0.09f, 4f, 0, 0.7f),
                    new S(sh.hb - 0.1f, 0.33f, 0.24f, 0.14f, 3.5f, 0, 0.6f),
                    new S(cockpitFront + 0.1f, 0.38f, 0.27f, 0.2f, 3f, 0, 0.5f),
                    new S(cockpitFront - 0.05f, 0.38f, sh.tubHW, 0.23f, 3f, 0, 0.5f),
                };
            }
            else
            {
                nose = new[]
                {
                    new S(sh.noseZ, sh.noseY, 0.07f, 0.045f, 2.6f, 0, 0.5f),
                    new S(sh.noseZ - 0.3f, sh.noseY + 0.04f, 0.11f, 0.075f, 2.8f, 0, 0.5f),
                    new S(sh.hb + 0.15f, sh.noseY + 0.13f, 0.17f, 0.12f, 3.1f, 0, 0.5f),
                    new S(sh.hb - 0.45f, 0.4f, 0.23f, 0.16f, 3.3f, 0, 0.55f),
                    new S(cockpitFront + 0.12f, 0.45f, 0.29f, 0.2f, 3.5f, 0, 0.6f),
                    new S(cockpitFront - 0.05f, 0.45f, sh.tubHW, 0.22f, 3.6f, 0, 0.6f),
                };
            }
            var noseUv = new Rect(LiveryPainter.Chassis.x, LiveryPainter.Chassis.y, LiveryPainter.Chassis.width * 0.55f, LiveryPainter.Chassis.height);
            k.Loft(Paint, nose, 28, 36, noseUv, Carbon, Interior);

            // Tub: open on top. Rim height and width follow the era.
            float tubCy = sh.rimY - 0.25f;
            var tub = new[]
            {
                new S(cockpitFront, tubCy, sh.tubHW, 0.27f, 3.6f, 0, 0.6f),
                new S(cockpitFront - 0.35f, tubCy, sh.tubHW + 0.01f, 0.27f, 3.8f, 0, 0.6f),
                new S(sh.headZ - 0.05f, tubCy, sh.tubHW + 0.01f, 0.27f, 3.8f, 0, 0.6f),
                new S(sh.headZ - 0.2f, tubCy, sh.tubHW, 0.27f, 3.6f, 0, 0.6f),
            };
            var tubUv = new Rect(LiveryPainter.Chassis.x + LiveryPainter.Chassis.width * 0.55f, LiveryPainter.Chassis.y, LiveryPainter.Chassis.width * 0.45f, LiveryPainter.Chassis.height);
            k.Loft(Paint, tub, 32, 12, tubUv, openTop: 0.5f, wall: 0.025f, innerSlot: Interior);
            // Cockpit floor.
            k.Box(Interior, new Vector3(0f, rideY + 0.06f, (cockpitFront + sh.headZ) * 0.5f), new Vector3(sh.tubHW * 1.7f, 0.02f, cockpitFront - sh.headZ + 0.2f));
        }

        // ------------------------------------------------------------------ sidepods

        static void BuildSidepods(MeshKit k, Shape sh, CarSpec spec)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * sh.podX;
                S[] pod;
                if (sh.classic)
                {
                    pod = new[]
                    {
                        new S(sh.podFront, 0.3f, sh.podHW, sh.podHH, 4.5f, x, 0.8f),
                        new S(sh.podFront - 0.25f, 0.3f, sh.podHW + 0.02f, sh.podHH, 4.5f, x * 1.02f, 0.8f),
                        new S(sh.podBack + 0.3f, 0.28f, sh.podHW, sh.podHH * 0.9f, 4f, x, 0.8f),
                        new S(sh.podBack, 0.25f, sh.podHW * 0.7f, sh.podHH * 0.7f, 3.5f, x * 0.9f, 0.8f),
                    };
                }
                else
                {
                    float len = sh.podFront - sh.podBack;
                    pod = new[]
                    {
                        new S(sh.podFront, 0.36f, sh.podHW * 0.85f, sh.podHH * 0.9f, 4.2f, x, 0.7f),
                        new S(sh.podFront - len * 0.12f, 0.37f, sh.podHW, sh.podHH, 3.9f, x * 1.03f, 0.7f),
                        new S(sh.podFront - len * 0.4f, 0.34f, sh.podHW * 0.95f, sh.podHH * 0.98f, 3.3f, x * 1.0f, 0.75f),
                        new S(sh.podFront - len * 0.72f, 0.25f, sh.podHW * 0.62f, sh.podHH * 0.7f, 2.8f, x * 0.8f, 0.8f),
                        new S(sh.podBack, 0.18f, sh.podHW * 0.3f, sh.podHH * 0.45f, 2.5f, x * 0.55f, 0.8f),
                    };
                }
                k.Loft(Paint, pod, 24, 24, LiveryPainter.Pods, Interior, Carbon);
                // Inlet lip: a thin carbon frame around the intake.
                if (!sh.classic)
                {
                    k.matrix = Matrix4x4.TRS(new Vector3(x, 0.36f, sh.podFront + 0.005f), Quaternion.identity, Vector3.one);
                    k.Box(Carbon, new Vector3(0f, sh.podHH * 0.9f, 0f), new Vector3(sh.podHW * 1.75f, 0.015f, 0.03f));
                    k.Box(Carbon, new Vector3(side * sh.podHW * 0.86f, 0f, 0f), new Vector3(0.015f, sh.podHH * 1.8f, 0.03f));
                    k.matrix = Matrix4x4.identity;
                }
                // Bargeboard / floor edge vanes ahead of the pods.
                if (sh.modern || sh.midera)
                {
                    float z0 = sh.podFront + 0.25f;
                    k.matrix = Matrix4x4.TRS(new Vector3(side * (sh.podX + 0.05f), 0.16f, z0), Quaternion.Euler(0f, side * 8f, 0f), Vector3.one);
                    k.Box(Carbon, Vector3.zero, new Vector3(0.012f, 0.2f, 0.45f));
                    k.Box(Carbon, new Vector3(side * 0.08f, -0.02f, -0.05f), new Vector3(0.012f, 0.16f, 0.35f));
                    k.matrix = Matrix4x4.identity;
                }
            }
        }

        // ------------------------------------------------------------------ engine cover, airbox, fin, exhaust

        static void BuildCover(MeshKit k, Shape sh, CarSpec spec)
        {
            float gearEnd = sh.tailZ + (sh.classic ? 0.1f : 0.3f);
            if (sh.exposedEngine)
            {
                // Classic: tall airbox over the driver's head, then a bare V8 with its exhausts and the gearbox.
                var hood = new[]
                {
                    new S(sh.headZ - 0.12f, sh.coverY, sh.coverHW + 0.06f, 0.2f, 3.2f, 0, 0.4f),
                    new S(sh.headZ - 0.4f, sh.coverY - 0.02f, sh.coverHW, 0.19f, 3f, 0, 0.4f),
                    new S(sh.headZ - 0.62f, sh.coverY - 0.06f, sh.coverHW * 0.8f, 0.14f, 2.8f, 0, 0.4f),
                };
                k.Loft(Paint, hood, 24, 12, LiveryPainter.Cover, Carbon, Satin);
                var classicBox = new[]
                {
                    new S(sh.headZ - 0.05f, sh.airboxY - 0.04f, 0.13f, 0.1f, 3.4f),
                    new S(sh.headZ - 0.3f, sh.airboxY - 0.02f, 0.16f, 0.12f, 3f),
                    new S(sh.headZ - 0.55f, sh.airboxY - 0.18f, 0.12f, 0.1f, 2.6f),
                };
                k.Loft(Paint, classicBox, 20, 10, LiveryPainter.Fin, Interior, Paint);
                // Engine block, cam covers and exhaust headers.
                float ez = sh.headZ - 0.95f;
                k.Box(Metal, new Vector3(0f, 0.3f, ez), new Vector3(0.36f, 0.22f, 0.6f), 2f);
                for (int s = -1; s <= 1; s += 2)
                {
                    k.Box(Satin, new Vector3(s * 0.14f, 0.45f, ez), new Vector3(0.1f, 0.06f, 0.56f));
                    for (int i = 0; i < 4; i++)
                    {
                        float z = ez + 0.2f - i * 0.13f;
                        k.Tube(Metal, new[] { new Vector3(s * 0.2f, 0.32f, z), new Vector3(s * 0.3f, 0.3f, z - 0.05f), new Vector3(s * 0.33f, 0.28f, ez - 0.4f), new Vector3(s * 0.2f + s * 0.02f * i, 0.32f, sh.tailZ + 0.12f) }, 0.022f, 8, false);
                    }
                }
                k.Box(Satin, new Vector3(0f, 0.3f, sh.tailZ + 0.35f), new Vector3(0.32f, 0.26f, 0.5f));
                return;
            }

            var cover = new[]
            {
                new S(sh.headZ - 0.1f, sh.coverY - 0.03f, sh.coverHW + 0.04f, 0.22f, 3.4f, 0, 0.5f),
                new S(sh.headZ - 0.45f, sh.coverY, sh.coverHW, 0.25f, 3.1f, 0, 0.5f),
                new S((sh.headZ - 0.45f + -sh.hb) * 0.5f, sh.coverY - 0.08f, sh.coverHW * 0.75f, 0.22f, 2.8f, 0, 0.5f),
                new S(-sh.hb, sh.coverY - 0.18f, sh.coverHW * 0.45f, 0.16f, 2.6f, 0, 0.5f),
                new S(gearEnd, 0.3f, 0.07f, 0.09f, 2.6f, 0, 0.5f),
            };
            k.Loft(Paint, cover, 28, 32, LiveryPainter.Cover, Carbon, Carbon);
            // Airbox / roll hoop above the driver's head with the intake opening.
            float a0 = sh.headZ - 0.12f;
            var airbox = new[]
            {
                new S(a0, sh.airboxY - 0.03f, 0.085f, 0.075f, 3f),
                new S(a0 - 0.22f, sh.airboxY - 0.02f, 0.1f, 0.095f, 2.8f),
                new S(a0 - 0.55f, sh.airboxY - 0.12f, 0.08f, 0.1f, 2.6f),
                new S(a0 - 0.85f, sh.coverY + 0.12f, 0.05f, 0.08f, 2.4f),
            };
            k.Loft(Paint, airbox, 20, 14, LiveryPainter.Fin, Interior, Paint);
            // Engine-cover fin (carries the race number) and the T-cam on top.
            var fin = new[] { new Vector2(0f, 0f), new Vector2(-1.1f, 0f), new Vector2(-1.25f, 0.18f), new Vector2(-0.25f, 0.18f) };
            k.matrix = Matrix4x4.TRS(new Vector3(0f, sh.airboxY - 0.12f, a0 - 0.55f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
            for (int i = 0; i < fin.Length; i++) fin[i] = new Vector2(fin[i].x * (sh.modern ? 0.9f : 0.75f), fin[i].y * (sh.midera ? 1.2f : 0.8f));
            k.Plate(Paint, fin, 0.008f, Carbon, LiveryPainter.Fin);
            k.matrix = Matrix4x4.identity;
            k.Box(Satin, new Vector3(0f, sh.airboxY + 0.07f, a0 - 0.12f), new Vector3(0.08f, 0.035f, 0.12f));
            // Exhaust.
            k.Tube(Metal, new[] { new Vector3(0f, 0.42f, gearEnd + 0.15f), new Vector3(0f, 0.44f, gearEnd - 0.02f) }, 0.05f, 12, false);
            k.Disc(Interior, new Vector3(0f, 0.44f, gearEnd - 0.02f), Vector3.back, 0.045f, 12);
        }

        // ------------------------------------------------------------------ floor, plank, diffuser

        static void BuildFloor(MeshKit k, Shape sh, CarSpec spec)
        {
            float y = spec.rideHeight;
            float front = sh.podFront + (sh.classic ? 0.0f : 0.35f);
            float back = -sh.hb + 0.15f;
            float w = sh.classic ? 0.5f : sh.modern ? 0.86f : 0.72f;
            var outline = new[]
            {
                new Vector2(-0.32f, front), new Vector2(0.32f, front),
                new Vector2(w * 0.9f, front - 0.5f), new Vector2(w, back + 0.6f), new Vector2(w * 0.78f, back),
                new Vector2(-w * 0.78f, back), new Vector2(-w, back + 0.6f), new Vector2(-w * 0.9f, front - 0.5f),
            };
            k.matrix = Matrix4x4.TRS(new Vector3(0f, y + 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            k.Plate(Carbon, outline, 0.024f, Carbon);
            k.matrix = Matrix4x4.identity;
            // Plank along the centre line (and its titanium skids).
            k.Box(Plank, new Vector3(0f, y - 0.004f, (front + back) * 0.5f), new Vector3(0.3f, 0.012f, front - back - 0.2f), 3f);
            if (!sh.classic)
            {
                // Floor edge fences.
                for (int s = -1; s <= 1; s += 2)
                    k.Box(Carbon, new Vector3(s * w * 0.95f, y + 0.04f, (front + back) * 0.5f - 0.2f), new Vector3(0.01f, 0.06f, (front - back) * 0.7f));
                // Diffuser: ramps up behind the rear axle, with vertical strakes.
                float d0 = back, d1 = sh.tailZ + 0.25f;
                for (int s = -1; s <= 1; s += 2)
                {
                    k.QuadFace(Carbon, new Vector3(0f, y + 0.02f, d0), new Vector3(s * w * 0.75f, y + 0.02f, d0), new Vector3(s * w * 0.62f, y + 0.26f, d1), new Vector3(0f, y + 0.24f, d1), true);
                    for (int i = 1; i <= 3; i++)
                    {
                        float x = s * w * 0.18f * i;
                        k.QuadFace(Carbon, new Vector3(x, y + 0.02f, d0), new Vector3(x, y + 0.02f, d1), new Vector3(x, y + 0.27f, d1), new Vector3(x, y + 0.04f, d0), true);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ halo / roll hoop

        static void BuildHalo(MeshKit k, Shape sh)
        {
            float ry = sh.rimY;
            float top = sh.eyeY + 0.13f;
            float front = sh.headZ + 0.62f;
            float back = sh.headZ - 0.08f;
            var path = new Vector3[17];
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;                   // 0 rear-left mount .. 1 rear-right mount
                float ang = Mathf.Lerp(-Mathf.PI * 0.95f, Mathf.PI * 0.95f, t);
                float x = Mathf.Sin(ang) * 0.29f;
                float z = Mathf.Lerp(back, front, (Mathf.Cos(ang) + 1f) * 0.5f);
                float yy = Mathf.Lerp(ry + 0.02f, top, Mathf.Pow(Mathf.Clamp01((Mathf.Cos(ang) + 1.05f) / 1.4f), 0.35f));
                path[i] = new Vector3(x, yy, z);
            }
            path[0] = new Vector3(-0.3f, ry - 0.02f, back - 0.03f);
            path[16] = new Vector3(0.3f, ry - 0.02f, back - 0.03f);
            k.Tube(Carbon, path, 0.022f, 12);
            // Central pylon down to the chassis, in front of the driver's eyes like the real thing.
            k.Tube(Carbon, new[] { new Vector3(0f, top - 0.005f, front), new Vector3(0f, top - 0.06f, front + 0.12f), new Vector3(0f, ry - 0.04f, front + 0.3f) }, 0.02f, 10, true, 0.028f);
        }

        static void BuildRollHoopScreen(MeshKit k, Shape sh)
        {
            // Small wind deflector ahead of the cockpit (older cars had a little screen).
            float front = sh.headZ + 0.78f;
            k.QuadFace(Satin, new Vector3(-0.22f, sh.rimY - 0.01f, front), new Vector3(0.22f, sh.rimY - 0.01f, front), new Vector3(0.18f, sh.rimY + 0.07f, front - 0.06f), new Vector3(-0.18f, sh.rimY + 0.07f, front - 0.06f), true);
        }

        // ------------------------------------------------------------------ cockpit

        static void BuildCockpitInterior(MeshKit k, Shape sh, bool detail)
        {
            float hz = sh.headZ;
            float seatY = sh.rimY - 0.42f;
            // Seat shell and back.
            k.matrix = Matrix4x4.TRS(new Vector3(0f, seatY + 0.18f, hz - 0.12f), Quaternion.Euler(-18f, 0f, 0f), Vector3.one);
            k.Box(Interior, Vector3.zero, new Vector3(0.44f, 0.5f, 0.04f));
            k.matrix = Matrix4x4.identity;
            k.Box(Interior, new Vector3(0f, seatY, hz + 0.2f), new Vector3(0.42f, 0.04f, 0.6f));
            // Head-protection padding around the rim (the blue foam blocks).
            for (int s = -1; s <= 1; s += 2)
                k.Box(Pad, new Vector3(s * (sh.tubHW - 0.07f), sh.rimY - 0.02f, hz - 0.02f), new Vector3(0.1f, 0.1f, 0.36f), 3f);
            k.Box(Pad, new Vector3(0f, sh.rimY - 0.02f, hz - 0.2f), new Vector3(0.4f, 0.12f, 0.08f), 3f);
            // Front bulkhead (dash) with a cut-out for the legs.
            float dashZ = hz + 0.74f;
            k.Box(Interior, new Vector3(0f, sh.rimY - 0.08f, dashZ), new Vector3(sh.tubHW * 1.7f, 0.12f, 0.04f));
            if (detail)
            {
                // Harness straps.
                k.matrix = Matrix4x4.TRS(new Vector3(0f, seatY + 0.36f, hz + 0.02f), Quaternion.Euler(-15f, 0f, 0f), Vector3.one);
                for (int s = -1; s <= 1; s += 2) k.Box(Red, new Vector3(s * 0.09f, 0f, 0f), new Vector3(0.05f, 0.4f, 0.006f));
                k.matrix = Matrix4x4.identity;
                // Switch panel on the dash bulkhead.
                k.Box(Satin, new Vector3(0f, sh.rimY - 0.1f, dashZ - 0.025f), new Vector3(0.24f, 0.08f, 0.01f));
            }
        }

        static void BuildLights(MeshKit k, Shape sh)
        {
            // LED day/night running lights for the night races (a Veylix series requirement).
            for (int s = -1; s <= 1; s += 2)
                k.Box(White, new Vector3(s * 0.08f, sh.noseY + 0.06f, sh.noseZ - 0.42f), new Vector3(0.06f, 0.012f, 0.01f));
        }

        // ------------------------------------------------------------------ wings

        static GameObject BuildFrontWing(Transform root, Shape sh, CarSpec spec, int side, Material paint, Material carbon)
        {
            var k = new MeshKit();
            float x0 = side * 0.12f, x1 = side * sh.fwSpan;
            float baseY = sh.fwY, z = sh.fwZ;
            int elements = sh.modern ? 4 : sh.midera ? 3 : 2;
            float[] chord = { 0.3f, 0.17f, 0.13f, 0.1f };
            float[] angle = { 3f, 13f, 24f, 34f };
            float[] dy = { 0f, 0.035f, 0.085f, 0.13f };
            float[] dz = { 0f, -0.24f, -0.36f, -0.45f };
            if (sh.classic) { chord = new[] { 0.32f, 0.16f }; angle = new[] { 6f, 18f }; dy = new[] { 0f, 0.06f }; dz = new[] { 0f, -0.24f }; }
            for (int e = 0; e < elements; e++)
            {
                // Modern wings sweep up towards the tips and dip at the nose.
                System.Func<float, float> yAt = t => sh.modern ? Mathf.Lerp(-0.01f, 0.03f, t) - (e > 0 ? (1f - t) * 0.04f * e : 0f) : 0f;
                System.Func<float, float> zAt = t => sh.modern ? -(1f - t) * 0.06f * e : 0f;
                var uv = new Rect(LiveryPainter.Wings.x, LiveryPainter.Wings.y + LiveryPainter.Wings.height * e / elements, LiveryPainter.Wings.width, LiveryPainter.Wings.height / elements);
                int slot = e == elements - 1 ? 0 : 1;
                if (side < 0) k.Wing(slot, x1, x0, chord[e], angle[e], baseY + dy[e], z + dz[e], 0.1f, 0.08f, t => yAt(1f - t), t => zAt(1f - t), 10, uv);
                else k.Wing(slot, x0, x1, chord[e], angle[e], baseY + dy[e], z + dz[e], 0.1f, 0.08f, yAt, zAt, 10, uv);
            }
            // Endplate.
            var plate = sh.classic
                ? new[] { new Vector2(0.05f, -0.04f), new Vector2(-0.45f, -0.04f), new Vector2(-0.45f, 0.12f), new Vector2(0.0f, 0.1f) }
                : new[] { new Vector2(0.08f, -0.06f), new Vector2(-0.6f, -0.06f), new Vector2(-0.62f, 0.2f), new Vector2(-0.4f, 0.27f), new Vector2(-0.05f, 0.14f) };
            k.matrix = Matrix4x4.TRS(new Vector3(x1 + side * 0.006f, baseY, z), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
            k.Plate(0, plate, 0.012f, 1, LiveryPainter.Plates);
            k.matrix = Matrix4x4.identity;
            // Pylon to the nose (raised-nose cars hang the wing under the nose).
            if (sh.noseHigh > 0f)
            {
                k.matrix = Matrix4x4.TRS(new Vector3(side * 0.13f, (baseY + sh.noseY) * 0.5f, z - 0.3f), Quaternion.identity, Vector3.one);
                k.Box(1, Vector3.zero, new Vector3(0.012f, sh.noseY - baseY, 0.3f));
                k.matrix = Matrix4x4.identity;
            }
            var go = MakeObject(root, side < 0 ? "FrontWingL" : "FrontWingR", k.Build("front wing"), new[] { paint, carbon });
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3((x0 + x1) * 0.5f, baseY + 0.08f, z - 0.25f);
            bc.size = new Vector3(Mathf.Abs(x1 - x0), 0.18f, 0.6f);
            return go;
        }

        static GameObject BuildRearWing(Transform root, Shape sh, CarSpec spec, CarSetup setup, Material paint, Material carbon)
        {
            var k = new MeshKit();
            float lvl = setup.wingLevel == 0 ? -4f : setup.wingLevel == 2 ? 6f : 0f;
            float span = sh.rwSpan, y = sh.rwY, z = sh.rwZ;
            float chordMain = sh.rwChord, chordFlap = sh.rwChord * (setup.wingLevel == 2 ? 0.72f : 0.6f);
            k.Wing(1, -span, span, chordMain, 8f + lvl * 0.5f, y, z, 0.11f, 0.09f, null, null, 10, LiveryPainter.Wings);
            k.Wing(0, -span, span, chordFlap, 26f + lvl * 1.5f, y + 0.07f + chordFlap * 0.15f, z - chordMain * 0.72f, 0.1f, 0.08f, null, null, 10, LiveryPainter.Wings);
            if (sh.modern || sh.midera)
            {
                // Beam wing low down and the central swan-neck pylon.
                k.Wing(1, -span * 0.75f, span * 0.75f, 0.14f, 10f, 0.38f, z - 0.05f, 0.1f, 0.06f);
                k.matrix = Matrix4x4.TRS(new Vector3(0f, (0.4f + y) * 0.5f, z - 0.05f), Quaternion.Euler(-12f, 0f, 0f), Vector3.one);
                k.Box(1, Vector3.zero, new Vector3(0.02f, y - 0.36f, 0.08f));
                k.matrix = Matrix4x4.identity;
            }
            else
            {
                // Classic: two struts from the gearbox.
                for (int s = -1; s <= 1; s += 2)
                    k.Tube(1, new[] { new Vector3(s * 0.12f, 0.36f, sh.tailZ + 0.45f), new Vector3(s * 0.15f, y - 0.02f, z - 0.1f) }, 0.018f, 8);
            }
            // Endplates.
            float plateH = sh.classic ? 0.32f : 0.62f;
            var plate = new[] { new Vector2(0.12f, -plateH * 0.7f), new Vector2(-0.48f, -plateH * 0.7f), new Vector2(-0.5f, plateH * 0.25f), new Vector2(-0.3f, plateH * 0.32f), new Vector2(0.1f, plateH * 0.25f) };
            for (int s = -1; s <= 1; s += 2)
            {
                k.matrix = Matrix4x4.TRS(new Vector3(s * (span + 0.006f), y + 0.05f, z + 0.05f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
                k.Plate(0, plate, 0.012f, 1, LiveryPainter.Plates);
            }
            k.matrix = Matrix4x4.identity;
            var go = MakeObject(root, "RearWing", k.Build("rear wing"), new[] { paint, carbon });
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, y, z - 0.2f);
            bc.size = new Vector3(span * 2f + 0.04f, sh.classic ? 0.3f : 0.4f, 0.55f);
            return go;
        }

        // ------------------------------------------------------------------ wheels

        static void BuildWheels(Car car, CarParts parts, Shape sh, Material rimMat, Material carbon, Material metal, CarSetup setup)
        {
            var A = VXAssets.I;
            var spec = car.spec;
            int compound = (int)setup.compound;
            Material tyre = A != null && A.tyres != null && A.tyres.Length > compound && A.tyres[compound] != null ? A.tyres[compound] : VXAssets.Fallback("Tyre", new Color(0.06f, 0.06f, 0.06f), 0.25f);
            var disc = new Material(VXAssets.Or(A != null ? A.brakeDisc : null, "Brake disc", new Color(0.18f, 0.17f, 0.17f), 0.3f));
            parts.discMaterial = disc;
            var meshes = new Mesh[2];
            for (int axle = 0; axle < 2; axle++)
            {
                bool front = axle == 0;
                float r = front ? spec.wheelRadiusFront : spec.wheelRadiusRear;
                float w = front ? spec.tireWidthFront : spec.tireWidthRear;
                meshes[axle] = WheelMesh(r, w, sh, setup.wheelStyle);
            }
            var caliperMesh = CaliperMesh();
            for (int i = 0; i < 4; i++)
            {
                var wd = car.wheels[i];
                bool front = i < 2;
                var pivot = new GameObject(new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" }[i]).transform;
                pivot.SetParent(car.transform, false);
                float y = wd.anchor.y - (wd.restLength - wd.compression);
                pivot.localPosition = new Vector3(wd.anchor.x, y, wd.anchor.z);
                var spin = new GameObject("Spin").transform;
                spin.SetParent(pivot, false);
                if (wd.left) spin.localScale = new Vector3(-1f, 1f, 1f);
                spin.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[front ? 0 : 1];
                var mr = spin.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { tyre, rimMat, disc, metal };
                // Caliper: fixed to the upright (doesn't spin).
                var cal = new GameObject("Caliper").transform;
                cal.SetParent(pivot, false);
                cal.localPosition = new Vector3((wd.left ? 1f : -1f) * (wd.width * 0.5f - 0.04f), wd.radius * 0.38f, -wd.radius * 0.18f);
                cal.gameObject.AddComponent<MeshFilter>().sharedMesh = caliperMesh;
                cal.gameObject.AddComponent<MeshRenderer>().sharedMaterial = VXAssets.Or(A != null ? A.lightRed : null, "Caliper", new Color(0.5f, 0.05f, 0.05f), 0.5f);
                wd.steerPivot = pivot;
                wd.visual = spin;

                // Suspension: upper and lower wishbones plus the push/pull rod, following the wheel.
                var chassisPt = car.transform;
                float inX = Mathf.Sign(wd.anchor.x) * (front ? 0.22f : 0.25f);
                float outX = (wd.left ? 1f : -1f) * 0.06f;
                MakeStrut(car.transform, carbon, chassisPt, new Vector3(inX, front ? 0.42f : 0.38f, wd.anchor.z + 0.12f), pivot, new Vector3(outX, wd.radius * 0.45f, 0f), 0.022f);
                MakeStrut(car.transform, carbon, chassisPt, new Vector3(inX, front ? 0.42f : 0.38f, wd.anchor.z - 0.18f), pivot, new Vector3(outX, wd.radius * 0.45f, 0f), 0.022f);
                MakeStrut(car.transform, carbon, chassisPt, new Vector3(inX, 0.16f, wd.anchor.z + 0.16f), pivot, new Vector3(outX, -wd.radius * 0.3f, 0f), 0.024f);
                MakeStrut(car.transform, carbon, chassisPt, new Vector3(inX, 0.16f, wd.anchor.z - 0.2f), pivot, new Vector3(outX, -wd.radius * 0.3f, 0f), 0.024f);
                MakeStrut(car.transform, carbon, chassisPt, new Vector3(inX * 0.7f, front ? 0.5f : 0.2f, wd.anchor.z), pivot, new Vector3(outX, front ? -wd.radius * 0.25f : wd.radius * 0.4f, 0f), 0.018f);
                // Wheel tether / track rod at the front.
                if (front) MakeStrut(car.transform, carbon, chassisPt, new Vector3(inX, 0.3f, wd.anchor.z + 0.05f), pivot, new Vector3(outX, 0.02f, wd.radius * 0.2f), 0.015f);

                // Wheel bumper collider (touches other cars and walls, never the road).
                var bumper = new GameObject("Bumper" + i);
                bumper.transform.SetParent(car.transform, false);
                bumper.transform.localPosition = new Vector3(wd.anchor.x, wd.radius + spec.rideHeight * 0.2f, wd.anchor.z);
                var cap = bumper.AddComponent<CapsuleCollider>();
                cap.direction = 0;
                cap.radius = wd.radius * 0.97f;
                cap.height = Mathf.Max(cap.radius * 2f + 0.01f, wd.width);
                cap.material = BodyPhysicsMaterial(0.6f);
            }
            parts.brakeDiscs = null;
        }

        static readonly System.Collections.Generic.Dictionary<string, Mesh> wheelCache = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>Tyre, rim, brake disc and nut in one mesh (slots: 0 tyre, 1 rim, 2 disc, 3 metal), outer face +X.</summary>
        static Mesh WheelMesh(float r, float w, Shape sh, int style)
        {
            string key = r.ToString("F3") + "_" + w.ToString("F3") + "_" + style + "_" + sh.classic;
            if (wheelCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var k = new MeshKit();
            float rimR = sh.classic ? r * 0.58f : sh.modern ? r * 0.64f : r * 0.6f;
            float hw = w * 0.5f;
            // Tyre: inner bead -> sidewall -> shoulder -> tread -> shoulder -> sidewall -> outer bead.
            var prof = new[]
            {
                new Vector2(rimR + 0.004f, -hw * 0.9f), new Vector2(rimR + (r - rimR) * 0.45f, -hw * 1.0f), new Vector2(r - 0.035f, -hw * 0.98f),
                new Vector2(r - 0.008f, -hw * 0.9f), new Vector2(r, -hw * 0.75f), new Vector2(r, hw * 0.75f), new Vector2(r - 0.008f, hw * 0.9f),
                new Vector2(r - 0.035f, hw * 0.98f), new Vector2(rimR + (r - rimR) * 0.45f, hw * 1.0f), new Vector2(rimR + 0.004f, hw * 0.9f),
            };
            k.Lathe(0, prof, 40, 1f, true, 2f);
            // Rim: outer lip, barrel, dish.
            var rimProf = new[]
            {
                new Vector2(rimR + 0.012f, hw * 0.9f), new Vector2(rimR - 0.004f, hw * 0.92f), new Vector2(rimR - 0.012f, hw * 0.8f),
                new Vector2(rimR - 0.02f, -hw * 0.85f), new Vector2(rimR + 0.01f, -hw * 0.9f),
            };
            k.Lathe(1, rimProf, 36, 1f, false);
            float face = hw * 0.62f;
            if (style == 4 || (sh.modern && style == 0))
            {
                // Aero wheel cover with a small hub window.
                k.Lathe(1, new[] { new Vector2(rimR - 0.01f, face + 0.02f), new Vector2(rimR * 0.55f, face + 0.005f), new Vector2(0.06f, face - 0.01f) }, 36, 1f, true);
            }
            else
            {
                int spokes = style == 1 ? 5 : style == 2 ? 12 : style == 3 ? 8 : 10;
                float spokeW = style == 1 ? 0.05f : style == 2 ? 0.018f : 0.03f;
                for (int i = 0; i < spokes; i++)
                {
                    float a = i * 360f / spokes;
                    k.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(a + (style == 2 ? 0f : 0f), 0f, 0f), Vector3.one);
                    var twist = style == 2 ? Quaternion.Euler(0f, 0f, 0f) : Quaternion.identity;
                    k.matrix = k.matrix * Matrix4x4.TRS(new Vector3(face - 0.01f, (rimR + 0.07f) * 0.5f, 0f), Quaternion.Euler(style == 2 ? 25f : 0f, 0f, 0f), Vector3.one);
                    k.Box(1, Vector3.zero, new Vector3(0.022f, rimR - 0.07f, spokeW));
                }
                k.matrix = Matrix4x4.identity;
            }
            // Hub and centre-lock nut.
            k.Lathe(3, new[] { new Vector2(0.075f, face - 0.03f), new Vector2(0.07f, face + 0.02f), new Vector2(0.04f, face + 0.045f), new Vector2(0.001f, face + 0.05f) }, 6, 1f, false);
            // Brake disc inside the rim (it spins with the wheel; drilled look comes from its texture).
            float discR = rimR * 0.82f;
            k.Lathe(2, new[] { new Vector2(discR * 0.55f, -hw * 0.35f), new Vector2(discR, -hw * 0.35f), new Vector2(discR, -hw * 0.35f - 0.032f), new Vector2(discR * 0.55f, -hw * 0.35f - 0.032f), new Vector2(discR * 0.55f, -hw * 0.35f) }, 28, 1f, false);
            var mesh = k.Build("wheel", 4);
            wheelCache[key] = mesh;
            return mesh;
        }

        static Mesh caliper;
        static Mesh CaliperMesh()
        {
            if (caliper != null) return caliper;
            var k = new MeshKit();
            k.Box(0, Vector3.zero, new Vector3(0.05f, 0.09f, 0.16f));
            caliper = k.Build("caliper");
            return caliper;
        }

        static void MakeStrut(Transform parent, Material mat, Transform a, Vector3 aOff, Transform b, Vector3 bOff, float thick)
        {
            var go = new GameObject("Arm");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = UnitTube();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var s = go.AddComponent<Strut>();
            s.a = a; s.aOffset = aOff; s.b = b; s.bOffset = bOff; s.thickness = thick;
        }

        static Mesh unitTube;
        static Mesh UnitTube()
        {
            if (unitTube != null) return unitTube;
            var k = new MeshKit();
            // Streamlined aerofoil-section arm, unit length along Z.
            k.matrix = Matrix4x4.Scale(new Vector3(0.5f, 1f, 1f));
            k.Tube(0, new[] { new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f) }, 0.5f, 8, true);
            unitTube = k.Build("arm");
            return unitTube;
        }

        // ------------------------------------------------------------------ driver helmet

        static GameObject BuildHelmet(Transform root, Shape sh, Car car, CarParts parts)
        {
            var mats = HelmetMaterials(car.driverLook, parts.livery);
            parts.helmetPaint = mats[0];
            var go = MakeObject(root, "Helmet", HelmetMesh(), mats);
            go.transform.localPosition = new Vector3(0f, sh.eyeY - 0.03f, sh.headZ);
            return go;
        }

        static Mesh helmetMesh;

        /// <summary>
        /// The helmet, centred on the head and facing +Z: a rounded shell (lower at the back, with a chin bar), a curved
        /// visor across the front, a dark lining underneath and a collar ring. Sub-meshes: 0 shell, 1 visor, 2 lining /
        /// collar. Shell UVs: u front to back, v around (0 chin, 0.5 crown), placed in the livery's Helmet rect.
        /// </summary>
        public static Mesh HelmetMesh()
        {
            if (helmetMesh != null) return helmetMesh;
            const float rx = 0.132f, ry = 0.148f, rz = 0.158f, lift = 0.02f;
            const int seg = 40, rings = 22;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var shell = new List<int>(); var visor = new List<int>(); var lining = new List<int>();
            Rect r = LiveryPainter.Helmet;
            Vector3 P(float th, float ph, float scale) => new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * rx, Mathf.Sin(ph) * ry + lift, Mathf.Cos(th) * Mathf.Cos(ph) * rz) * scale;
            Vector3 N(Vector3 p) => new Vector3(p.x / (rx * rx), (p.y - lift) / (ry * ry), p.z / (rz * rz)).normalized;
            float PhiMin(float th) => Mathf.Lerp(-58f, -80f, (1f - Mathf.Cos(th)) * 0.5f) * Mathf.Deg2Rad; // chin higher than the back
            Vector2 UV(Vector3 p)
            {
                Vector3 d = new Vector3(p.x / rx, (p.y - lift) / ry, p.z / rz).normalized;
                float u = Mathf.Acos(Mathf.Clamp(d.z, -1f, 1f)) / Mathf.PI;
                float v = Mathf.Atan2(d.x, d.y) / (Mathf.PI * 2f) + 0.5f;
                return new Vector2(r.x + u * r.width, r.y + Mathf.Clamp01(v) * r.height);
            }
            // Shell: a grid over azimuth (around Y) and elevation, from the cut-off at the bottom up to the crown.
            int start = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float th = i / (float)seg * Mathf.PI * 2f;
                for (int j = 0; j <= rings; j++)
                {
                    float ph = Mathf.Lerp(PhiMin(th), Mathf.PI * 0.5f, j / (float)rings);
                    var p = P(th, ph, 1f);
                    verts.Add(p); norms.Add(N(p)); uvs.Add(UV(p));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < rings; j++)
                {
                    int a0 = start + i * (rings + 1) + j, a1 = a0 + rings + 1;
                    shell.Add(a0); shell.Add(a0 + 1); shell.Add(a1);
                    shell.Add(a1); shell.Add(a0 + 1); shell.Add(a1 + 1);
                }
            // Visor: a patch just proud of the shell across the front.
            int vs = 18, vr = 6;
            int vstart = verts.Count;
            for (int i = 0; i <= vs; i++)
            {
                float th = Mathf.Lerp(-64f, 64f, i / (float)vs) * Mathf.Deg2Rad;
                for (int j = 0; j <= vr; j++)
                {
                    float ph = Mathf.Lerp(-14f, 24f, j / (float)vr) * Mathf.Deg2Rad;
                    var p = P(th, ph, 1.012f);
                    verts.Add(p); norms.Add(N(p)); uvs.Add(new Vector2(i / (float)vs, j / (float)vr));
                }
            }
            for (int i = 0; i < vs; i++)
                for (int j = 0; j < vr; j++)
                {
                    int a0 = vstart + i * (vr + 1) + j, a1 = a0 + vr + 1;
                    visor.Add(a0); visor.Add(a0 + 1); visor.Add(a1);
                    visor.Add(a1); visor.Add(a0 + 1); visor.Add(a1 + 1);
                }
            // Lining: close the opening underneath with a dark disc (seen from below).
            int centre = verts.Count;
            verts.Add(new Vector3(0f, P(Mathf.PI, PhiMin(Mathf.PI), 1f).y * 0.85f + P(0f, PhiMin(0f), 1f).y * 0.15f, -0.01f)); norms.Add(Vector3.down); uvs.Add(Vector2.zero);
            int rim = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float th = i / (float)seg * Mathf.PI * 2f;
                verts.Add(P(th, PhiMin(th), 0.99f)); norms.Add(Vector3.down); uvs.Add(Vector2.zero);
            }
            for (int i = 0; i < seg; i++) { lining.Add(centre); lining.Add(rim + i + 1); lining.Add(rim + i); }
            // Collar / neck brace ring under the helmet.
            var k = new MeshKit();
            var ring = new Vector3[25];
            for (int i = 0; i <= 24; i++) { float a = i / 24f * Mathf.PI * 2f; ring[i] = new Vector3(Mathf.Sin(a) * 0.105f, -0.155f, Mathf.Cos(a) * 0.095f - 0.02f); }
            k.Tube(0, ring, 0.03f, 10, false);
            var collar = k.Build("collar");
            int cbase = verts.Count;
            verts.AddRange(collar.vertices); norms.AddRange(collar.normals);
            foreach (var u in collar.uv) uvs.Add(u);
            foreach (var t in collar.triangles) lining.Add(cbase + t);

            var m = new Mesh { name = "helmet" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs);
            m.subMeshCount = 3;
            m.SetTriangles(shell, 0); m.SetTriangles(visor, 1); m.SetTriangles(lining, 2);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return helmetMesh = m;
        }

        /// Shell (your design, or the team's from the livery), visor (tinted to your choice) and HANS.
        public static Material[] HelmetMaterials(DriverLook look, Texture2D livery)
        {
            var A = VXAssets.I;
            var shell = new Material(VXAssets.Or(A != null ? A.paint : null, "Helmet", Color.white, 0.85f)) { name = "Helmet" };
            if (look != null)
            {
                shell.mainTexture = LiveryPainter.HelmetTexture(look);
                shell.mainTextureScale = new Vector2(4f, 4f);
                shell.mainTextureOffset = new Vector2(-2f, -1f);
            }
            else shell.mainTexture = livery;
            shell.color = Color.white;
            var visor = VXAssets.Or(A != null ? A.visor : null, "Visor", new Color(0.02f, 0.02f, 0.03f), 0.95f);
            if (look != null && look.visor != 0) visor = new Material(visor) { name = "Visor", color = look.VisorColor };
            return new[] { shell, visor, VXAssets.Or(A != null ? A.interior : null, "Suit", new Color(0.05f, 0.05f, 0.06f), 0.2f) };
        }

        // ------------------------------------------------------------------ mirrors

        static void BuildMirrors(Transform root, Shape sh, CarParts parts, Material carbon)
        {
            var A = VXAssets.I;
            var glassMat = VXAssets.Or(A != null ? A.mirrorGlass : null, "Mirror", new Color(0.4f, 0.45f, 0.5f), 0.95f);
            parts.mirrorGlass = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                int s = i == 0 ? -1 : 1;
                var k = new MeshKit();
                Vector3 c = new Vector3(s * (sh.halo ? 0.52f : 0.42f), sh.rimY + (sh.halo ? 0.06f : 0.1f), sh.headZ + 0.62f);
                k.Loft(0, new[] { new S(c.z + 0.07f, c.y, 0.07f, 0.03f, 3f, c.x), new S(c.z + 0.02f, c.y, 0.095f, 0.04f, 3.5f, c.x), new S(c.z - 0.02f, c.y, 0.1f, 0.042f, 4f, c.x) }, 20, 6, null, 0, 0);
                // Stalk.
                k.Tube(0, new[] { new Vector3(c.x - s * 0.02f, c.y - 0.03f, c.z + 0.02f), new Vector3(s * sh.tubHW * 0.95f, sh.rimY - 0.05f, c.z + 0.12f) }, 0.012f, 6);
                var mirrorGo = MakeObject(root, "Mirror", k.Build("mirror"), new[] { carbon });
                parts.looseBits.Add(mirrorGo); // can be knocked off in a crash
                // Glass faces backwards (shows the mirror camera).
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                if (Application.isPlaying) Object.Destroy(g.GetComponent<Collider>()); else Object.DestroyImmediate(g.GetComponent<Collider>());
                g.name = "MirrorGlass";
                g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(c.x, c.y, c.z - 0.0215f);
                g.transform.localRotation = Quaternion.Euler(0f, 180f + s * 6f, 0f);
                g.transform.localScale = new Vector3(0.185f, 0.07f, 1f);
                var mr = g.GetComponent<MeshRenderer>();
                mr.sharedMaterial = glassMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parts.mirrorGlass[i] = g.transform;
                g.transform.SetParent(mirrorGo.transform, true); // the glass goes with the mirror if it's knocked off
            }
        }

        // ------------------------------------------------------------------ steering wheel

        static void BuildSteeringWheel(Transform root, Shape sh, CarSpec spec, CarParts parts, Material carbon, Material satin, Material metal)
        {
            var A = VXAssets.I;
            var pivot = new GameObject("SteeringWheel").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, sh.eyeY - (sh.classic ? 0.3f : 0.26f), sh.headZ + (sh.classic ? 0.45f : 0.47f));
            pivot.localRotation = Quaternion.Euler(sh.classic ? -25f : -18f, 0f, 0f); // top leans towards the driver
            var k = new MeshKit();
            bool round = spec.body == BodyStyle.Classic || spec.body == BodyStyle.Hillclimb;
            float radius;
            if (round)
            {
                radius = sh.classic ? 0.16f : 0.15f;
                // Leather-wrapped rim and three spokes.
                var ring = new Vector3[33];
                for (int i = 0; i <= 32; i++) { float a = i / 32f * Mathf.PI * 2f; ring[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f); }
                k.Tube(1, ring, 0.014f, 10, false);
                foreach (float a in new[] { 0f, 180f, 270f })
                {
                    float rad = a * Mathf.Deg2Rad;
                    k.Tube(2, new[] { Vector3.zero, new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f) }, 0.008f, 6, false);
                }
                k.Lathe(2, new[] { new Vector2(0.04f, 0.02f), new Vector2(0.05f, 0f), new Vector2(0.03f, -0.03f) }, 12);
            }
            else
            {
                // Modern rectangular wheel: carbon body, rubber grips, central display, rotaries and buttons.
                radius = 0.135f;
                var outline = new[] { new Vector2(-0.13f, -0.06f), new Vector2(-0.07f, -0.085f), new Vector2(0.07f, -0.085f), new Vector2(0.13f, -0.06f), new Vector2(0.14f, 0.05f), new Vector2(0.1f, 0.075f), new Vector2(-0.1f, 0.075f), new Vector2(-0.14f, 0.05f) };
                k.Plate(0, outline, 0.03f, 0);
                for (int s = -1; s <= 1; s += 2)
                {
                    k.Tube(1, new[] { new Vector3(s * 0.12f, -0.07f, -0.01f), new Vector3(s * 0.145f, -0.01f, -0.012f), new Vector3(s * 0.135f, 0.06f, -0.01f) }, 0.018f, 10, true);
                    // Rotary knobs and buttons.
                    for (int j = 0; j < 2; j++) k.Lathe(3, new[] { new Vector2(0.011f, 0.0f), new Vector2(0.011f, 0.012f), new Vector2(0.001f, 0.013f) }, 10);
                }
                for (int j = 0; j < 6; j++)
                {
                    float bx = (j % 3 - 1) * 0.03f + (j < 3 ? -0.075f : 0.075f) * 0f;
                    float by = j < 3 ? -0.055f : 0.06f;
                    k.matrix = Matrix4x4.TRS(new Vector3(bx, by, 0.018f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
                    k.Lathe(j % 2 == 0 ? 4 : 5, new[] { new Vector2(0.007f, 0f), new Vector2(0.007f, 0.006f), new Vector2(0.001f, 0.007f) }, 10);
                    k.matrix = Matrix4x4.identity;
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    k.matrix = Matrix4x4.TRS(new Vector3(s * 0.085f, -0.01f, 0.018f), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
                    k.Lathe(3, new[] { new Vector2(0.014f, 0f), new Vector2(0.014f, 0.012f), new Vector2(0.002f, 0.014f) }, 12);
                    k.matrix = Matrix4x4.identity;
                }
            }
            var mats = new[] { carbon, VXAssets.Or(A != null ? A.interior : null, "Grip", new Color(0.04f, 0.04f, 0.04f), 0.25f), metal, VXAssets.Or(A != null ? A.metal : null, "Knob", Color.gray, 0.5f), VXAssets.Or(A != null ? A.lightRed : null, "Button red", Color.red, 0.5f), VXAssets.Or(A != null ? A.halo : null, "Button", new Color(0.9f, 0.6f, 0.1f), 0.5f) };
            var wheelGo = MakeObject(pivot, "Wheel", k.Build("steering wheel"), mats);
            wheelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the driver
            parts.steeringWheel = pivot;
            parts.steeringWheelGrip = wheelGo.transform;
            parts.wheelRadius = radius;

            // Display anchor (the Dashboard component puts text and the shift lights here).
            var disp = new GameObject("Display").transform;
            disp.SetParent(pivot, false);
            disp.localPosition = new Vector3(0f, round ? -0.05f : 0.005f, round ? -0.03f : -0.017f);
            disp.localRotation = Quaternion.Euler(0f, 180f, 0f);
            parts.displayAnchor = disp;

            // Shift paddles behind the wheel.
            if (!round || spec.body == BodyStyle.Hillclimb)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var pk = new MeshKit();
                    pk.Box(0, Vector3.zero, new Vector3(0.05f, 0.075f, 0.006f));
                    var paddle = MakeObject(pivot, s < 0 ? "PaddleL" : "PaddleR", pk.Build("paddle"), new[] { carbon });
                    paddle.transform.localPosition = new Vector3(s * 0.1f, 0.02f, 0.03f);
                    if (s < 0) parts.paddleLeft = paddle.transform; else parts.paddleRight = paddle.transform;
                }
            }
        }

        static void BuildHandbrake(Transform root, Shape sh, CarParts parts, Material metal, Material satin)
        {
            var pivot = new GameObject("Handbrake").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(sh.tubHW - 0.1f, sh.rimY - 0.2f, sh.headZ + 0.18f);
            var k = new MeshKit();
            k.Tube(0, new[] { Vector3.zero, new Vector3(0f, 0.2f, 0.04f) }, 0.012f, 8);
            k.Tube(1, new[] { new Vector3(0f, 0.17f, 0.035f), new Vector3(0f, 0.25f, 0.05f) }, 0.018f, 10);
            MakeObject(pivot, "Lever", k.Build("handbrake"), new[] { metal, satin });
            pivot.localRotation = Quaternion.Euler(10f, 0f, 0f);
            parts.handbrakeLever = pivot;
        }

        // ------------------------------------------------------------------ colliders

        static PhysicsMaterial bodyMat;
        static PhysicsMaterial BodyPhysicsMaterial(float friction = 0.35f)
        {
            if (bodyMat == null)
                bodyMat = new PhysicsMaterial("Car body") { dynamicFriction = 0.32f, staticFriction = 0.4f, bounciness = 0.08f, frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            return bodyMat;
        }

        static void AddBox(Transform root, Vector3 c, Vector3 size)
        {
            var bc = root.gameObject.AddComponent<BoxCollider>();
            bc.center = c;
            bc.size = size;
            bc.sharedMaterial = BodyPhysicsMaterial();
        }

        static void BuildColliders(Car car, Shape sh)
        {
            var root = car.transform;
            var spec = car.spec;
            float y0 = spec.rideHeight;
            // Tub and nose.
            float noseLen = sh.noseZ - sh.headZ;
            AddBox(root, new Vector3(0f, (y0 + sh.rimY) * 0.5f + 0.02f, (sh.noseZ + sh.headZ) * 0.5f - 0.2f), new Vector3(0.6f, sh.rimY - y0 - 0.04f, noseLen - 0.3f));
            // Sidepods and floor.
            AddBox(root, new Vector3(0f, y0 + 0.18f, (sh.podFront + sh.podBack) * 0.5f), new Vector3((sh.podX + sh.podHW) * 2f, 0.3f, sh.podFront - sh.podBack));
            // Engine cover / airbox.
            AddBox(root, new Vector3(0f, (y0 + sh.airboxY) * 0.5f, (sh.headZ - 0.1f + sh.tailZ + 0.3f) * 0.5f), new Vector3(sh.coverHW * 1.6f, sh.airboxY - y0, sh.headZ - sh.tailZ - 0.4f));
            // Floor plate (lets the car bottom out and scrape on landing).
            AddBox(root, new Vector3(0f, y0 + 0.03f, (sh.podFront + (-sh.hb)) * 0.5f), new Vector3(sh.modern ? 1.6f : 1.3f, 0.04f, sh.podFront + sh.hb));
        }
    }
}
