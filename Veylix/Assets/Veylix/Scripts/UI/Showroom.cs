using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The player's own garage: the hub you're in whenever you're not driving. A small private workshop with your car on
    /// a turntable in the middle under spotlights, a workbench and pegboard of tools, a tyre rack, a wall of every track
    /// you can race, your trophies and career numbers, and the roller door out to the track. Walk around it (left stick
    /// or WASD) and use the stations: CUSTOMISE by the car, CHOOSE A TRACK at the track wall, DRIVE at the door. The car
    /// is rebuilt live as you change it in the Garage menu.
    /// </summary>
    public class Showroom : MonoBehaviour
    {
        public Vector3 standPoint;
        public Transform turntable;
        public bool spinning = true;
        Car display;
        int shownCar = -1;
        CarSetup shownSetup;
        Transform stations;

        // Room interior, local to the origin.
        const float X0 = -5f, X1 = 5f, Z0 = -3.5f, Z1 = 8.5f, Height = 3.8f;
        static readonly Vector3 CarSpot = new Vector3(0f, 0f, 3f);

        public enum Spot { Entrance, Customise, TrackWall, Door }

        static Material M(System.Func<VXAssets, Material> f, string n, Color c, float s = 0.3f) { var A = VXAssets.I; return VXAssets.Or(A != null ? f(A) : null, n, c, s); }

        Vector3 O => transform.position;

        /// Where you can walk (world space, a little inside the walls).
        public Bounds WalkArea => new Bounds(O + new Vector3(0f, 1f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0 - 0.8f, 4f, Z1 - Z0 - 0.8f));

        void Awake() { Build(); }

        /// The editor builds the same room into the scene so you can see it without pressing Play.
        public void BuildRoom() => Build();

        // ------------------------------------------------------------------ places in the room

        public Pose SpotPose(Spot s)
        {
            Vector3 p;
            switch (s)
            {
                case Spot.Customise: p = new Vector3(-2.3f, 0f, 1.1f); break;
                case Spot.TrackWall: p = new Vector3(2.9f, 0f, 3f); break;
                case Spot.Door: p = new Vector3(1.6f, 0f, 6.6f); break;
                default: p = new Vector3(0f, 0f, -1.9f); break;
            }
            Vector3 look = s == Spot.TrackWall ? Vector3.right : s == Spot.Door ? Vector3.forward : (CarSpot - p);
            look.y = 0f;
            return new Pose(O + p, Quaternion.LookRotation(look.normalized, Vector3.up));
        }

        /// Walk (fade) the player to a spot in the garage.
        public void GoTo(Spot s)
        {
            var rig = Rig.I;
            if (rig == null) return;
            var pose = SpotPose(s);
            rig.StandAt(pose.position, pose.rotation);
        }

        // ------------------------------------------------------------------ building the room

        void Build()
        {
            transform.position = new Vector3(0f, -200f, 0f); // under the world origin, away from any map
            standPoint = SpotPose(Spot.Entrance).position;
            Vector3 o = O;
            float cx = (X0 + X1) * 0.5f, cz = (Z0 + Z1) * 0.5f, w = X1 - X0, d = Z1 - Z0;

            // Floor: sealed concrete with painted safety lines round the turntable.
            var acc = new MeshAcc();
            AddPlane(acc, o + new Vector3(cx, 0f, cz), new Vector2(w, d), Vector3.up, 0.35f);
            acc.Emit(transform, "Floor", M(a => a.pitLane, "Floor", new Color(0.36f, 0.36f, 0.37f), 0.6f), Layers.Default, true);
            var lines = new MeshAcc();
            float r = 3.6f;
            lines.Box(o + CarSpot + new Vector3(0f, 0.003f, -r), new Vector3(r * 2f, 0.004f, 0.08f), Quaternion.identity);
            lines.Box(o + CarSpot + new Vector3(0f, 0.003f, r), new Vector3(r * 2f, 0.004f, 0.08f), Quaternion.identity);
            lines.Box(o + CarSpot + new Vector3(-r, 0.003f, 0f), new Vector3(0.08f, 0.004f, r * 2f), Quaternion.identity);
            lines.Box(o + CarSpot + new Vector3(r, 0.003f, 0f), new Vector3(0.08f, 0.004f, r * 2f), Quaternion.identity);
            // Hatched bay in front of the roller door.
            for (int i = 0; i < 9; i++)
                lines.Box(o + new Vector3(-2.2f + i * 0.55f, 0.003f, 7.9f), new Vector3(0.12f, 0.004f, 0.9f), Quaternion.Euler(0f, 35f, 0f));
            lines.Emit(transform, "SafetyLines", M(a => a.kerbYellow, "Yellow", new Color(0.9f, 0.72f, 0.1f), 0.45f), Layers.Scenery, false);

            // Walls: dark lower panelling, painted upper wall, exposed steel beams and a dark ceiling.
            var lower = new MeshAcc();
            var upper = new MeshAcc();
            Wall(lower, upper, o + new Vector3(X0, 0f, cz), new Vector3(0.2f, 0f, d), true);
            Wall(lower, upper, o + new Vector3(X1, 0f, cz), new Vector3(0.2f, 0f, d), true);
            Wall(lower, upper, o + new Vector3(cx, 0f, Z0), new Vector3(w, 0f, 0.2f), false);
            // Back wall in two pieces around the roller door opening, plus the lintel.
            Wall(lower, upper, o + new Vector3(-3.85f, 0f, Z1), new Vector3(2.3f, 0f, 0.2f), false);
            Wall(lower, upper, o + new Vector3(3.85f, 0f, Z1), new Vector3(2.3f, 0f, 0.2f), false);
            upper.Box(o + new Vector3(0f, 3.5f, Z1), new Vector3(5.4f, 0.6f, 0.2f), Quaternion.identity, 0.3f);
            lower.Emit(transform, "WallPanels", M(a => a.satinBlack, "Panels", new Color(0.09f, 0.09f, 0.1f), 0.35f), Layers.Default, true);
            upper.Emit(transform, "Walls", M(a => a.wallConcrete, "Walls", new Color(0.66f, 0.66f, 0.64f), 0.2f), Layers.Default, true);
            var ceil = new MeshAcc();
            AddPlane(ceil, o + new Vector3(cx, Height, cz), new Vector2(w, d), Vector3.down, 0.3f);
            ceil.Emit(transform, "Ceiling", M(a => a.satinBlack, "Ceiling", new Color(0.07f, 0.07f, 0.08f), 0.2f), Layers.Scenery, false);
            var beams = new MeshAcc();
            for (int i = 0; i < 5; i++) beams.Box(o + new Vector3(cx, Height - 0.18f, Z0 + 1.2f + i * 2.4f), new Vector3(w, 0.3f, 0.16f), Quaternion.identity, 0.5f);
            beams.Emit(transform, "Beams", M(a => a.metal, "Steel", new Color(0.32f, 0.33f, 0.35f), 0.55f), Layers.Scenery, false);
            var strips = new MeshAcc();
            for (int i = -1; i <= 1; i++) strips.Box(o + new Vector3(i * 2.4f, Height - 0.36f, cz), new Vector3(0.14f, 0.05f, d - 2f), Quaternion.identity);
            strips.Emit(transform, "LightStrips", M(a => a.lampHead, "Strip", Color.white, 0.5f), Layers.Scenery, false);

            RollerDoor(o);
            Workbench(o);
            TyreRack(o);
            Turntable(o);
            TrackWall(o);
            TrophyWall(o);
            Lights(o);
            Stations(o);
        }

        void Wall(MeshAcc lower, MeshAcc upper, Vector3 baseCentre, Vector3 size, bool alongZ)
        {
            const float dado = 1.1f;
            lower.Box(baseCentre + Vector3.up * dado * 0.5f, new Vector3(size.x, dado, size.z), Quaternion.identity, 0.5f);
            upper.Box(baseCentre + Vector3.up * (dado + (Height - dado) * 0.5f), new Vector3(size.x, Height - dado, size.z), Quaternion.identity, 0.3f);
        }

        void RollerDoor(Vector3 o)
        {
            var slats = new MeshAcc();
            for (int i = 0; i < 26; i++)
                slats.Box(o + new Vector3(0f, 0.06f + i * 0.124f, Z1 + 0.04f), new Vector3(5.3f, 0.11f, 0.05f), Quaternion.Euler(-6f, 0f, 0f), 0.4f);
            slats.Emit(transform, "RollerDoor", M(a => a.metal, "Door", new Color(0.5f, 0.52f, 0.55f), 0.5f), Layers.Default, true);
            var frame = new MeshAcc();
            frame.Box(o + new Vector3(-2.68f, 1.6f, Z1 - 0.02f), new Vector3(0.12f, 3.2f, 0.18f), Quaternion.identity);
            frame.Box(o + new Vector3(2.68f, 1.6f, Z1 - 0.02f), new Vector3(0.12f, 3.2f, 0.18f), Quaternion.identity);
            frame.Box(o + new Vector3(0f, 3.25f, Z1 - 0.02f), new Vector3(5.5f, 0.14f, 0.2f), Quaternion.identity);
            frame.Emit(transform, "DoorFrame", M(a => a.kerbYellow, "Yellow", new Color(0.9f, 0.72f, 0.1f), 0.45f), Layers.Scenery, false);
        }

        void Workbench(Vector3 o)
        {
            float x = X0 + 0.42f;
            var top = new MeshAcc();
            top.Box(o + new Vector3(x, 0.9f, 5.6f), new Vector3(0.75f, 0.06f, 2.6f), Quaternion.identity, 0.6f);
            top.Emit(transform, "BenchTop", M(a => a.plank, "Wood", new Color(0.5f, 0.38f, 0.24f), 0.3f), Layers.Default, true);
            var body = new MeshAcc();
            body.Box(o + new Vector3(x, 0.44f, 5.6f), new Vector3(0.7f, 0.86f, 2.55f), Quaternion.identity, 0.6f);
            body.Emit(transform, "BenchDrawers", M(a => a.kerbRed, "Red", new Color(0.62f, 0.08f, 0.06f), 0.55f), Layers.Default, true);
            var handles = new MeshAcc();
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                    handles.Box(o + new Vector3(x + 0.36f, 0.2f + row * 0.2f, 4.65f + col * 0.95f), new Vector3(0.02f, 0.02f, 0.3f), Quaternion.identity);
            handles.Emit(transform, "Handles", M(a => a.metal, "Steel", new Color(0.6f, 0.6f, 0.62f), 0.7f), Layers.Scenery, false);
            // Pegboard with spanners, a torque wrench, screwdrivers and a wheel gun.
            var board = new MeshAcc();
            board.Box(o + new Vector3(X0 + 0.04f, 1.65f, 5.6f), new Vector3(0.03f, 1.1f, 2.5f), Quaternion.identity, 1.2f);
            board.Emit(transform, "Pegboard", M(a => a.satinBlack, "Pegboard", new Color(0.12f, 0.12f, 0.13f), 0.2f), Layers.Scenery, false);
            var tools = new MeshAcc();
            var rng = new Rng(11);
            for (int i = 0; i < 9; i++)
            {
                float len = 0.14f + i * 0.025f;
                tools.Box(o + new Vector3(X0 + 0.08f, 1.95f - len * 0.5f, 4.6f + i * 0.11f), new Vector3(0.015f, len, 0.025f), Quaternion.Euler(0f, 0f, rng.Range(-3f, 3f)));
            }
            for (int i = 0; i < 6; i++)
                tools.Box(o + new Vector3(X0 + 0.08f, 1.45f, 5.75f + i * 0.09f), new Vector3(0.02f, 0.2f, 0.02f), Quaternion.identity);
            tools.Box(o + new Vector3(X0 + 0.08f, 1.5f, 6.5f), new Vector3(0.02f, 0.55f, 0.03f), Quaternion.Euler(0f, 0f, 0f));
            tools.Emit(transform, "Tools", M(a => a.metal, "Chrome", new Color(0.75f, 0.76f, 0.78f), 0.85f), Layers.Scenery, false);
            var gun = new MeshKit();
            gun.matrix = Matrix4x4.TRS(o + new Vector3(x - 0.05f, 1.0f, 6.3f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            gun.Lathe(0, new[] { new Vector2(0.001f, -0.14f), new Vector2(0.045f, -0.14f), new Vector2(0.05f, 0.08f), new Vector2(0.03f, 0.1f), new Vector2(0.03f, 0.16f), new Vector2(0.001f, 0.16f) }, 16);
            MakeObject("WheelGun", gun.Build("wheelgun"), M(a => a.carbon, "Gun", new Color(0.1f, 0.1f, 0.11f), 0.5f));
            // Rolling tool chest by the bench.
            var chest = new MeshAcc();
            chest.Box(o + new Vector3(X0 + 0.5f, 0.55f, 3.5f), new Vector3(0.6f, 1.0f, 0.9f), Quaternion.identity, 0.5f);
            chest.Emit(transform, "ToolChest", M(a => a.kerbRed, "Red", new Color(0.62f, 0.08f, 0.06f), 0.55f), Layers.Default, true);
        }

        void TyreRack(Vector3 o)
        {
            var rack = new MeshAcc();
            float x = X0 + 0.45f;
            for (int i = 0; i < 3; i++) rack.Box(o + new Vector3(x, 0.35f + i * 0.75f, -1.2f), new Vector3(0.7f, 0.04f, 3.4f), Quaternion.identity);
            for (int i = 0; i < 4; i++) rack.Box(o + new Vector3(x, 1.1f, -2.85f + i * 1.1f), new Vector3(0.7f, 2.2f, 0.05f), Quaternion.identity);
            rack.Emit(transform, "TyreRack", M(a => a.metal, "Steel", new Color(0.32f, 0.33f, 0.35f), 0.55f), Layers.Default, true);
            var tyres = new MeshKit();
            var profile = new[] { new Vector2(0.2f, -0.17f), new Vector2(0.33f, -0.18f), new Vector2(0.36f, -0.12f), new Vector2(0.36f, 0.12f), new Vector2(0.33f, 0.18f), new Vector2(0.2f, 0.17f) };
            for (int shelf = 0; shelf < 3; shelf++)
                for (int k = 0; k < 4; k++)
                {
                    tyres.matrix = Matrix4x4.TRS(o + new Vector3(x, 0.73f + shelf * 0.75f, -2.5f + k * 0.4f), Quaternion.identity, Vector3.one * (shelf == 2 ? 0.92f : 1f));
                    tyres.Lathe(0, profile, 24);
                }
            var A = VXAssets.I;
            var mat = A != null && A.tyres != null && A.tyres.Length > 1 && A.tyres[1] != null ? A.tyres[1] : M(a => a.satinBlack, "Tyre", new Color(0.05f, 0.05f, 0.05f), 0.25f);
            MakeObject("Tyres", tyres.Build("tyres"), mat);
        }

        void Turntable(Vector3 o)
        {
            var tt = new MeshKit();
            tt.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            tt.Lathe(0, new[] { new Vector2(3.0f, 0f), new Vector2(3.0f, 0.05f), new Vector2(2.92f, 0.07f), new Vector2(0.01f, 0.07f) }, 56);
            tt.matrix = Matrix4x4.identity;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(transform, false);
            turntable.position = o + CarSpot;
            turntable.gameObject.AddComponent<MeshFilter>().sharedMesh = tt.Build("turntable");
            turntable.gameObject.AddComponent<MeshRenderer>().sharedMaterial = M(a => a.metal, "Turntable", new Color(0.3f, 0.3f, 0.32f), 0.75f);
        }

        void TrackWall(Vector3 o)
        {
            // Every track you can race, framed on the right-hand wall.
            var wall = new GameObject("TrackWall").transform;
            wall.SetParent(transform, false);
            wall.position = o + new Vector3(X1 - 0.12f, 1.75f, 3f);
            wall.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up); // text faces into the room
            VUI.Label(wall, "TRACKS", new Vector2(-1.55f, 1.18f), 3.2f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 2f).characterSpacing = 12f;
            VUI.Label(wall, "15 circuits  |  10 free roam", new Vector2(-1.55f, 0.98f), 1.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 2f);
            var A = VXAssets.I;
            for (int i = 0; i < MapCatalog.Count; i++)
            {
                int col = i % 5, row = i / 5;
                var p = new Vector2(-1.25f + col * 0.62f, 0.68f - row * 0.38f);
                VUI.Quad(wall, "Frame", p, new Vector2(0.58f, 0.34f), new Color(0.05f, 0.05f, 0.055f, 1f), 0.01f, 0.004f);
                var tex = A != null && A.mapPreviews != null && i < A.mapPreviews.Length ? A.mapPreviews[i] : null;
                VUI.Quad(wall, "Map", p + new Vector2(0f, 0.025f), new Vector2(0.54f, 0.26f), tex != null ? Color.white : new Color(0.2f, 0.2f, 0.22f, 1f), 0.005f, 0.003f, tex);
                VUI.Fit(VUI.Label(wall, MapCatalog.Get(i).name.ToUpperInvariant(), p + new Vector2(-0.26f, -0.135f), 0.36f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.52f), 0.52f, 0.045f);
            }
        }

        TextMeshPro statsLabel;

        static string StatsText()
        {
            var d = SaveData.D;
            return "RACES  " + d.races + "     WINS  " + d.wins + "     PODIUMS  " + d.podiums + "     TITLES  " + d.championshipsWon + "     " + d.distanceKm.ToString("0") + " KM";
        }

        void TrophyWall(Vector3 o)
        {
            // Front wall: the VEYLIX sign, your trophies and career numbers.
            var wall = new GameObject("TrophyWall").transform;
            wall.SetParent(transform, false);
            wall.position = o + new Vector3(0f, 2.55f, Z0 + 0.12f);
            wall.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            VUI.Label(wall, "VEYLIX", new Vector2(0f, 0.55f), 9f, VUI.Text, TextAlignmentOptions.Midline, true, 5f).characterSpacing = 30f;
            VUI.Quad(wall, "Band", new Vector2(0f, 0.2f), new Vector2(4.2f, 0.05f), VUI.Accent, 0.01f, 0.01f);
            var d = SaveData.D;
            statsLabel = VUI.Label(wall, StatsText(), new Vector2(0f, 0.02f), 1.3f, VUI.TextDim, TextAlignmentOptions.Midline, true, 5f);
            // Shelf with a cup per championship (an empty plinth until you win one).
            var shelf = new MeshAcc();
            shelf.Box(o + new Vector3(0f, 1.32f, Z0 + 0.25f), new Vector3(3.2f, 0.05f, 0.35f), Quaternion.identity);
            shelf.Emit(transform, "Shelf", M(a => a.plank, "Wood", new Color(0.5f, 0.38f, 0.24f), 0.3f), Layers.Scenery, false);
            int cups = Mathf.Clamp(d.championshipsWon, 0, 6);
            var cup = new MeshKit();
            var profile = new[] { new Vector2(0.001f, 0f), new Vector2(0.07f, 0f), new Vector2(0.07f, 0.04f), new Vector2(0.02f, 0.06f), new Vector2(0.02f, 0.16f), new Vector2(0.09f, 0.22f), new Vector2(0.1f, 0.34f), new Vector2(0.09f, 0.34f), new Vector2(0.08f, 0.24f), new Vector2(0.001f, 0.2f) };
            for (int i = 0; i < Mathf.Max(1, cups); i++)
            {
                cup.matrix = Matrix4x4.TRS(o + new Vector3(-1.2f + i * 0.48f, 1.345f, Z0 + 0.25f), Quaternion.identity, Vector3.one * (cups == 0 ? 0.35f : 1f));
                cup.Lathe(0, profile, 20);
            }
            var gold = new Material(M(a => a.metal, "Gold", new Color(0.85f, 0.65f, 0.2f), 0.85f)) { name = "Trophy" };
            gold.color = cups > 0 ? new Color(0.95f, 0.72f, 0.25f) : new Color(0.25f, 0.25f, 0.27f);
            MakeObject("Trophies", cup.Build("trophies"), gold);
        }

        void Lights(Vector3 o)
        {
            AddSpot(o + new Vector3(-2.6f, Height - 0.4f, 1.2f), o + CarSpot, 2.4f, new Color(1f, 0.96f, 0.9f), 55f, true);
            AddSpot(o + new Vector3(2.6f, Height - 0.4f, 1.2f), o + CarSpot, 2.4f, new Color(1f, 0.96f, 0.9f), 55f, false);
            AddSpot(o + new Vector3(0f, Height - 0.4f, 6.2f), o + CarSpot, 1.5f, new Color(0.82f, 0.9f, 1f), 60f, false);
            AddSpot(o + new Vector3(1.2f, Height - 0.3f, 3f), o + new Vector3(X1, 1.6f, 3f), 1.3f, new Color(1f, 0.95f, 0.88f), 80f, false);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.transform.position = o + new Vector3(0f, 3f, 2f);
            fill.type = LightType.Point; fill.range = 13f; fill.intensity = 0.7f; fill.color = new Color(1f, 0.92f, 0.84f);
            var bench = new GameObject("BenchLamp").AddComponent<Light>();
            bench.transform.SetParent(transform, false);
            bench.transform.position = o + new Vector3(X0 + 0.7f, 2.3f, 5.6f);
            bench.type = LightType.Point; bench.range = 4f; bench.intensity = 1.1f; bench.color = new Color(1f, 0.82f, 0.6f);
        }

        void AddSpot(Vector3 p, Vector3 target, float intensity, Color c, float angle, bool shadows)
        {
            var l = new GameObject("Spot").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.position = p;
            l.transform.rotation = Quaternion.LookRotation(target - p);
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.range = 12f;
            l.intensity = intensity;
            l.color = c;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        // ------------------------------------------------------------------ stations (point and click)

        void Stations(Vector3 o)
        {
            stations = new GameObject("Stations").transform;
            stations.SetParent(transform, false);
            Vector3 cust = new Vector3(-2.9f, 0f, 1.6f);
            Station(o + cust, CarSpot - cust, "CUSTOMISE", "Paint, livery, wheels, aero, brakes, suspension", () => Game.I?.menus.OpenGarage());
            Station(o + new Vector3(3.3f, 0f, 1.0f), Vector3.right, "CHOOSE A TRACK", "All 25 maps", () => Game.I?.menus.OpenMapSelection());
            Station(o + new Vector3(3.2f, 0f, 6.9f), Vector3.forward, "DRIVE", "Pick a mode and go", () => Game.I?.menus.OpenDrive());
        }

        /// A slim stand with an angled screen, readable by someone standing behind it looking along readerLooks.
        void Station(Vector3 basePos, Vector3 readerLooks, string title, string sub, System.Action act)
        {
            readerLooks.y = 0f;
            var rot = Quaternion.LookRotation(readerLooks.normalized, Vector3.up);
            var go = new GameObject("Station " + title).transform;
            go.SetParent(stations, false);
            go.position = basePos;
            go.rotation = rot;
            var stand = new MeshAcc();
            stand.Box(basePos + Vector3.up * 0.55f, new Vector3(0.08f, 1.1f, 0.08f), rot);
            stand.Box(basePos + Vector3.up * 0.015f, new Vector3(0.4f, 0.03f, 0.3f), rot);
            stand.Emit(stations, "Stand", M(a => a.satinBlack, "Stand", new Color(0.08f, 0.08f, 0.09f), 0.4f), Layers.Default, true);
            var screen = new GameObject("Screen").transform;
            screen.SetParent(go, false);
            screen.localPosition = new Vector3(0f, 1.18f, 0f);
            screen.localRotation = Quaternion.Euler(25f, 0f, 0f); // lectern: face tilted up towards you
            VUI.Quad(screen, "bg", Vector2.zero, new Vector2(0.46f, 0.2f), VUI.Panel, 0.004f, 0.012f);
            VUI.Quad(screen, "line", new Vector2(0f, 0.098f), new Vector2(0.43f, 0.003f), VUI.Accent, 0.002f, 0.0015f);
            VButton.Make(screen, title, new Vector2(0f, 0.022f), new Vector2(0.42f, 0.085f), act, true);
            VUI.Fit(VUI.Label(screen, sub, new Vector2(0f, -0.06f), 0.17f, VUI.TextDim, TextAlignmentOptions.Midline, false, 0.42f), 0.42f, 0.04f, 2);
            Layers.SetRecursive(screen.gameObject, Layers.UI);
        }

        // ------------------------------------------------------------------ helpers

        void MakeObject(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = Layers.Scenery;
        }

        static void AddPlane(MeshAcc acc, Vector3 c, Vector2 size, Vector3 n, float uv)
        {
            Vector3 r = Vector3.right * size.x * 0.5f, f = Vector3.forward * size.y * 0.5f;
            if (n.y < 0f) f = -f;
            int a = acc.Add(c - r - f, n, new Vector2(0, 0)), b = acc.Add(c + r - f, n, new Vector2(size.x * uv, 0));
            int d = acc.Add(c + r + f, n, new Vector2(size.x * uv, size.y * uv)), e = acc.Add(c - r + f, n, new Vector2(0, size.y * uv));
            acc.Quad(a, b, d, e, n);
        }

        // ------------------------------------------------------------------ your driver

        GameObject figure;
        TextMeshPro figureName;

        /// Your driver stands beside the car (rebuilt whenever you change your look).
        public void ShowFigure(DriverLook look)
        {
            if (figure != null) Destroy(figure);
            figure = DriverBody.BuildStanding(transform, look);
            Vector3 p = new Vector3(1.75f, 0f, 0.7f);
            Vector3 face = SpotPose(Spot.Entrance).position - (O + p);
            face.y = 0f;
            figure.transform.position = O + p;
            figure.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            // Name board at its feet.
            var board = new GameObject("Nameplate").transform;
            board.SetParent(figure.transform, false);
            board.localPosition = new Vector3(0f, 0.05f, 0.45f);
            board.localRotation = Quaternion.Euler(60f, 180f, 0f);
            VUI.Quad(board, "bg", Vector2.zero, new Vector2(0.62f, 0.16f), VUI.Panel, 0.002f, 0.01f);
            VUI.Quad(board, "line", new Vector2(-0.3f, 0f), new Vector2(0.008f, 0.12f), VUI.Accent, 0.001f, 0.002f);
            figureName = VUI.Fit(VUI.Label(board, look.DisplayName.ToUpperInvariant() + "   #" + look.number, new Vector2(-0.27f, 0f), 0.5f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.55f), 0.55f, 0.1f);
            Layers.SetRecursive(board.gameObject, Layers.UI);
        }

        // ------------------------------------------------------------------ the car

        public void Show(int carIndex, CarSetup setup = null)
        {
            if (display != null) CarFactory.Despawn(display);
            shownCar = carIndex;
            var spec = CarCatalog.Get(carIndex);
            var s = setup ?? SaveData.Setup(carIndex);
            shownSetup = s;
            display = CarFactory.Spawn(spec, s, true, "You", 0, turntable.position + Vector3.up * 0.07f, turntable.rotation * Quaternion.Euler(0f, 210f, 0f));
            display.transform.SetParent(turntable, true);
            display.Freeze(true);
            var au = display.GetComponentInChildren<CarAudio>();
            if (au != null) au.enabled = false;
            var src = display.GetComponentInChildren<AudioSource>();
            if (src != null) src.enabled = false;
            if (display.parts != null && display.parts.headSpot != null) display.parts.headSpot.enabled = false;
            // In the garage the car's parked: nobody in it.
            if (display.parts != null && display.parts.helmet != null) display.parts.helmet.SetActive(false);
            if (display.parts != null && display.parts.driver != null) display.parts.driver.gameObject.SetActive(false);
            CarFactory.All.Remove(display);
        }

        void Update()
        {
            if (turntable != null && spinning) turntable.Rotate(0f, 7f * Time.deltaTime, 0f);
            Cameras.Main = Rig.I != null ? Rig.I.cam : Cameras.Main;
        }

        void OnDisable()
        {
            if (display != null) { Destroy(display.gameObject); display = null; }
        }

        void OnEnable()
        {
            if (statsLabel != null) statsLabel.text = StatsText(); // fresh numbers every time you come back
            if (shownCar >= 0 && display == null && turntable != null) Show(shownCar, shownSetup);
        }
    }
}
