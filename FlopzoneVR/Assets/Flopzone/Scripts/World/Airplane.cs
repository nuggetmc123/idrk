using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The transport plane. Outside: a big grey four-engine cargo plane parked on
    /// the hub's airfield with its ramp down. Walk up the ramp and you're inside
    /// the cargo bay (built far above every map), the ramp closes behind you and
    /// your party is pulled in. In here: the mode screen (party leader or solo
    /// players only), six loadout stations, arcade machines, and the wait for a
    /// full match.
    /// </summary>
    public class Airplane : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(0, 500, 0);
        /// <summary>Where you come out in the hub if you leave the plane: at the foot of the ramp.</summary>
        public static readonly Vector3 HubExit = new Vector3(0, 0.05f, 44.5f);

        public bool Occupied { get; private set; }
        Transform door;
        float doorAngle = 0f, doorTarget = 0f;
        Material cloudsL, cloudsR, jumpLight;
        UIPanel screen;
        string screenKey = "";
        float nextScreen;

        const float OpenAngle = -100f;

        static readonly Vector2[] Seats =
        {
            new Vector2(0, -6f), new Vector2(-1.2f, -6f), new Vector2(1.2f, -6f),
            new Vector2(-1.2f, -7f), new Vector2(1.2f, -7f), new Vector2(0, -7f)
        };

        /* ======================= inside ======================= */

        public static Airplane Create()
        {
            var root = new GameObject("Airplane");
            root.transform.position = Origin;
            var a = root.AddComponent<Airplane>();
            a.BuildInterior();
            return a;
        }

        /// <summary>Put the player in a seat by the ramp and close the door behind them.</summary>
        public void Enter(PlayerRig rig, int seat, bool closeDoor = true)
        {
            Occupied = true;
            var s = Seats[Mathf.Abs(seat) % Seats.Length];
            rig.Respawn(transform.TransformPoint(new Vector3(s.x, 0.05f, s.y)), 0f);
            if (closeDoor)
            {
                doorAngle = OpenAngle;
                doorTarget = 0f;
                AudioManager.Sfx("slide", transform.TransformPoint(new Vector3(0, 1.5f, -8f)), 0.9f, 0.35f);
            }
            else doorAngle = doorTarget = 0f;
            screenKey = "";
        }

        public void Exit() => Occupied = false;

        void BuildInterior()
        {
            var t = transform;
            var floor = MapKit.M("4a4d50", 0.35f, 0.5f, Surface.Metal);
            var quilt = MapKit.M("6b7653", 0.1f, 0f, Surface.Fabric);
            var rib = MilitaryProps.DarkSteel;
            var steel = MilitaryProps.Steel;
            var red = MapKit.M("8a2a22", 0.1f, 0f, Surface.Fabric);
            var yellow = MilitaryProps.Hazard;
            var strip = Prims.Emissive(Prims.Hex("fff1d6"), 1.6f);

            // floor, roller tracks and tie-down rings
            Prims.Box(t, new Vector3(0, -0.1f, 0), new Vector3(4.6f, 0.2f, 16.4f), floor);
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                Prims.Box(t, new Vector3(x, 0.01f, 0), new Vector3(0.18f, 0.02f, 16f), rib, false);
                for (int i = 0; i < 26; i++) Prims.Cyl(t, new Vector3(x, 0.025f, -7.5f + i * 0.6f), 0.07f, 0.16f, steel, Vector3.right);
            }
            for (int i = 0; i < 6; i++)
                foreach (float x in new[] { -1.7f, 1.7f })
                    Prims.Cyl(t, new Vector3(x, 0.006f, -6.5f + i * 2.6f), 0.12f, 0.01f, yellow, Vector3.up);
            Prims.Box(t, new Vector3(0, 0.005f, -7.6f), new Vector3(4.2f, 0.01f, 0.25f), yellow, false);

            // the curved hull: walls, sloped upper panels and the ceiling, with ribs every 1.6 m
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Box(t, new Vector3(side * 2.4f, 1.1f, 0), new Vector3(0.2f, 2.2f, 16.4f), quilt);
                Prims.Box(t, new Vector3(side * 1.82f, 2.62f, 0), new Vector3(0.2f, 1.3f, 16.4f), quilt, true, Quaternion.Euler(0, 0, side * 51f));
                for (int i = 0; i <= 10; i++)
                {
                    float z = -8f + i * 1.6f;
                    Prims.Box(t, new Vector3(side * 2.27f, 1.1f, z), new Vector3(0.08f, 2.2f, 0.12f), rib, false);
                    Prims.Box(t, new Vector3(side * 1.72f, 2.56f, z), new Vector3(0.08f, 1.3f, 0.12f), rib, false, Quaternion.Euler(0, 0, side * 51f));
                }
                // windows with clouds rushing by
                var cloud = Prims.Sprite(Color.white);
                cloud.mainTexture = CloudTexture();
                cloud.renderQueue = 2980;
                if (side < 0) cloudsL = cloud; else cloudsR = cloud;
                for (int i = 0; i < 5; i++)
                {
                    float z = -5.6f + i * 2.4f + (i >= 2 ? 0.8f : 0f);
                    Prims.Box(t, new Vector3(side * 2.28f, 1.55f, z), new Vector3(0.06f, 0.46f, 0.46f), steel, false);
                    Prims.Make(PrimitiveType.Quad, t, new Vector3(side * 2.245f, 1.55f, z), new Vector3(0.34f, 0.34f, 1), cloud, false,
                        Quaternion.Euler(0, side * 90f, 0), "window");
                }
                // cargo straps and a fire extinguisher for good measure
                for (int i = 0; i < 4; i++)
                    Prims.Box(t, new Vector3(side * 2.27f, 1.1f, -7.2f + i * 0.25f), new Vector3(0.03f, 2.0f, 0.05f), yellow, false);
                Prims.Cyl(t, new Vector3(side * 2.2f, 0.45f, 3.9f), 0.14f, 0.5f, MapKit.M("c62020", 0.5f, 0.1f, Surface.PaintedMetal), Vector3.up);
            }
            Prims.Box(t, new Vector3(0, 3.1f, 0), new Vector3(2.6f, 0.2f, 16.4f), quilt);
            for (int i = 0; i <= 10; i++) Prims.Box(t, new Vector3(0, 2.98f, -8f + i * 1.6f), new Vector3(2.5f, 0.06f, 0.12f), rib, false);
            foreach (float x in new[] { -0.7f, 0.7f }) Prims.Box(t, new Vector3(x, 2.97f, 0), new Vector3(0.12f, 0.04f, 15f), strip, false);
            Prims.Cyl(t, new Vector3(0.9f, 2.85f, 0), 0.1f, 16f, steel, Vector3.forward);
            Prims.Cyl(t, new Vector3(-0.9f, 2.85f, 0), 0.06f, 16f, MapKit.M("d0c060", 0.4f, 0.2f), Vector3.forward);

            // lighting
            foreach (float z in new[] { -3.5f, 3.5f })
            {
                var lg = Prims.Empty("lamp", t, new Vector3(0, 2.6f, z));
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 1.5f;
                l.color = new Color(1f, 0.92f, 0.8f);
                l.shadows = LightShadows.None;
            }

            // a double row of red jump seats down the middle
            for (int i = 0; i < 5; i++)
            {
                float z = -3.6f + i * 1.4f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Prims.Box(t, new Vector3(side * 0.36f, 0.46f, z), new Vector3(0.44f, 0.06f, 1.25f), red);
                    Prims.Cyl(t, new Vector3(side * 0.55f, 0.23f, z - 0.5f), 0.04f, 0.46f, steel, Vector3.up);
                    Prims.Cyl(t, new Vector3(side * 0.55f, 0.23f, z + 0.5f), 0.04f, 0.46f, steel, Vector3.up);
                    Prims.Box(t, new Vector3(side * 0.12f, 0.85f, z), new Vector3(0.04f, 0.65f, 1.25f), red, false);
                }
                Prims.Cyl(t, new Vector3(0, 0.75f, z), 0.05f, 1.5f, steel, Vector3.up);
            }
            Prims.Cyl(t, new Vector3(0, 1.2f, -0.8f), 0.05f, 7.4f, steel, Vector3.forward);

            // six loadout stations along the walls
            for (int i = 0; i < 3; i++)
            {
                float z = -3.2f + i * 2.6f;
                LoadoutStation.Create(t, new Vector3(-1.95f, 0, z), -90f);
                LoadoutStation.Create(t, new Vector3(1.95f, 0, z), 90f);
            }

            // the front: bulkhead, crew door, the mode screen and two arcade machines
            var bulk = MapKit.M("5a6048", 0.3f, 0.3f, Surface.PaintedMetal);
            Prims.Box(t, new Vector3(0, 1.55f, 8.1f), new Vector3(4.6f, 3.1f, 0.2f), bulk);
            Prims.Box(t, new Vector3(1.55f, 1.0f, 7.99f), new Vector3(0.75f, 1.9f, 0.04f), rib, false);
            UIKit.Text(t, "CREW ONLY", new Vector3(1.55f, 1.7f, 7.96f), 0.06f, new Color(0.95f, 0.85f, 0.4f));
            ArcadeMachine.Create(t, new Vector3(-1.7f, 0, 7.1f), 0f, "2a7fff");
            ArcadeMachine.Create(t, new Vector3(1.7f, 0, 6.2f), -20f, "ff4fa3");
            Prims.Box(t, new Vector3(0, 1.75f, 8.02f), new Vector3(2.2f, 1.32f, 0.06f), rib, false);
            screen = UIPanel.Create("mode screen", new Vector2(2.05f, 1.18f), new Color(0.04f, 0.05f, 0.08f, 1f), t);
            screen.transform.localPosition = new Vector3(0, 1.75f, 7.97f);

            // the rear: the ramp door (hinged at the floor) and the jump lights
            var pivot = Prims.Empty("ramp", t, new Vector3(0, 0, -8.05f)).transform;
            door = pivot;
            Prims.Box(pivot, new Vector3(0, 1.55f, 0), new Vector3(4.6f, 3.1f, 0.16f), bulk);
            for (int i = 0; i < 6; i++) Prims.Box(pivot, new Vector3(0, 0.3f + i * 0.5f, 0.1f), new Vector3(4.2f, 0.06f, 0.04f), rib, false);
            foreach (float x in new[] { -1.5f, 1.5f }) Prims.Cyl(pivot, new Vector3(x, 1.4f, 0.14f), 0.1f, 2.4f, steel, Vector3.up);
            var blocker = Prims.Empty("ramp blocker", t, new Vector3(0, 1.5f, -8.2f));
            blocker.AddComponent<BoxCollider>().size = new Vector3(4.6f, 3f, 0.1f);
            jumpLight = Prims.Sprite(new Color(1f, 0.15f, 0.1f));
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                Prims.Box(t, new Vector3(x, 2.5f, -7.85f), new Vector3(0.3f, 0.3f, 0.12f), rib, false);
                Prims.Make(PrimitiveType.Quad, t, new Vector3(x, 2.5f, -7.78f), new Vector3(0.22f, 0.22f, 1), jumpLight, false, Quaternion.Euler(0, 180, 0), "jump light");
            }
            var sign = UIKit.Text(t, "RAMP", new Vector3(0, 2.75f, -7.86f), 0.09f, new Color(0.95f, 0.85f, 0.4f));
            sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }

        static Texture2D cloudTex;

        /// <summary>Blue sky with soft white clouds, tiling sideways so it can scroll forever.</summary>
        static Texture2D CloudTexture()
        {
            if (cloudTex != null) return cloudTex;
            const int w = 128, h = 64;
            cloudTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "fz_clouds" };
            var px = new Color32[w * h];
            var rnd = new System.Random(5);
            var blobs = new Vector3[18];
            for (int i = 0; i < blobs.Length; i++) blobs[i] = new Vector3((float)rnd.NextDouble() * w, 10 + (float)rnd.NextDouble() * 40, 6 + (float)rnd.NextDouble() * 10);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = Color.Lerp(new Color(0.62f, 0.8f, 0.97f), new Color(0.3f, 0.55f, 0.9f), (float)y / h);
                    float cl = 0;
                    foreach (var b in blobs)
                    {
                        float dx = Mathf.Abs(x - b.x); dx = Mathf.Min(dx, w - dx);
                        float dy = (y - b.y) * 1.8f;
                        cl += Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) / b.z);
                    }
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(cl * 1.3f));
                    px[y * w + x] = c;
                }
            cloudTex.SetPixels32(px);
            cloudTex.Apply();
            return cloudTex;
        }

        void Update()
        {
            // the ramp swings shut
            if (door != null && !Mathf.Approximately(doorAngle, doorTarget))
            {
                float before = doorAngle;
                doorAngle = Mathf.MoveTowards(doorAngle, doorTarget, Time.deltaTime * 70f);
                door.localRotation = Quaternion.Euler(doorAngle, 0, 0);
                if (before != 0 && doorAngle == 0) AudioManager.Sfx("impact", door.position + Vector3.up * 1.5f, 1f, 0.45f);
            }
            float scroll = Time.time * 0.08f;
            if (cloudsL) cloudsL.mainTextureOffset = new Vector2(scroll, 0);
            if (cloudsR) cloudsR.mainTextureOffset = new Vector2(-scroll, 0);
            if (!Occupied) return;

            var g = Game.I;
            var s = g != null ? g.session : null;
            bool found = s != null && s.InMatch && s.State == "countdown";
            if (jumpLight) jumpLight.color = found ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 2f)));
            var rig = PlayerRig.I;
            if (rig != null && UIPointer.I && transform.InverseTransformPoint(rig.head.position).z > 3.6f) UIPointer.I.KeepOn();
            if (Time.time >= nextScreen && s != null) { nextScreen = Time.time + 0.25f; RefreshScreen(s); }
        }

        /* ======================= the mode screen ======================= */

        void RefreshScreen(Session s)
        {
            bool leader = s.IsLeaderOrSolo;
            var md = s.Mode != null && LocalServer.Modes.TryGetValue(s.Mode, out var m) ? m : null;
            int secs = Mathf.FloorToInt(Mathf.Max(0, RoomHost.WaitForPlayers - s.TimeLeft));
            string key = $"{s.RoomKind}|{s.State}|{leader}|{s.Mode}|{s.Players.Count}|{(s.InMatch ? secs : 0)}|{(s.State == "countdown" ? Mathf.CeilToInt(s.TimeLeft) : 0)}";
            if (key == screenKey) return;
            screenKey = key;
            screen.Clear();
            var c = UIKit.Accent;
            if (s.InMatch && md != null && (s.State == "waiting" || s.State == "countdown"))
            {
                bool found = s.State == "countdown";
                var title = screen.Text(found ? "MATCH FOUND!" : "FINDING MATCH" + new string('.', 1 + secs % 3), new Vector2(0, 0.42f), 0.11f, found ? UIKit.Good : c);
                title.fontStyle = FontStyle.Bold;
                screen.Text(md.name.ToUpperInvariant(), new Vector2(0, 0.25f), 0.07f, Color.white);
                screen.Text($"{s.Players.Count} / {md.max} players", new Vector2(0, 0.12f), 0.06f, UIKit.Muted);
                if (found) screen.Text("Deploying in " + Mathf.CeilToInt(s.TimeLeft) + "...", new Vector2(0, -0.04f), 0.08f, Color.white);
                else
                {
                    screen.Text($"{secs / 60}:{secs % 60:00} / 1:00", new Vector2(0, -0.03f), 0.09f, Color.white);
                    screen.Text("Not full by 1:00? You'll start with the players who joined, plus bots.", new Vector2(0, -0.16f), 0.035f, UIKit.Muted);
                    if (leader)
                    {
                        if (!s.Online) screen.Button("START NOW WITH BOTS", new Vector2(-0.42f, -0.4f), new Vector2(0.7f, 0.14f), () => s.StartNow(), 0.045f);
                        screen.Button("CANCEL", new Vector2(s.Online ? 0 : 0.42f, -0.4f), new Vector2(0.5f, 0.14f), () => Game.I.GoHub(), 0.045f);
                    }
                    else screen.Text("Your party leader can cancel.", new Vector2(0, -0.4f), 0.04f, UIKit.Muted);
                }
                return;
            }
            var head = screen.Text("SELECT GAME MODE", new Vector2(0, 0.47f), 0.085f, c);
            head.fontStyle = FontStyle.Bold;
            screen.Text(leader ? (s.InParty ? "You lead the party: everyone comes with you." : "Point and pull the trigger to pick.")
                               : "Only your party leader can pick the mode.", new Vector2(0, 0.37f), 0.036f, UIKit.Muted);
            for (int i = 0; i < Hub.Portals.Length; i++)
            {
                var p = Hub.Portals[i];
                var pos = new Vector2(i % 2 == 0 ? -0.48f : 0.48f, 0.2f - (i / 2) * 0.22f);
                var b = screen.Button(p.label + "\n<size=22>" + p.info + "</size>", pos, new Vector2(0.9f, 0.19f), () => Game.I.GoMode(p.mode), 0.045f);
                b.interactable = leader;
            }
            screen.Button("LEAVE PLANE", new Vector2(0, -0.5f), new Vector2(0.6f, 0.11f), () => Game.I.LeavePlane(), 0.04f);
        }

        /* ======================= outside, on the hub's airfield ======================= */

        /// <summary>The parked plane. pos is the back end of the main cabin; the nose points along +z, the ramp comes down toward -z.</summary>
        public static void BuildExterior(MapKit k, Vector3 pos)
        {
            var root = Prims.Empty("transport plane", k.S, pos).transform;
            var dyn = Prims.Empty("transport plane (moving parts)", k.D, pos).transform;
            var grey = MapKit.M("6c757b", 0.35f, 0.35f, Surface.PaintedMetal);
            var greyDark = MapKit.M("4f575c", 0.35f, 0.35f, Surface.PaintedMetal);
            var black = MapKit.M("1d1f21", 0.3f, 0.2f, Surface.Rubber);
            var glass = MilitaryProps.Glass;
            var steel = MilitaryProps.DarkSteel;
            var tire = MilitaryProps.Tire;

            // fuselage: round body, flat belly, rounded nose with a radome
            Prims.Cyl(root, new Vector3(0, 3.3f, 11f), 4.8f, 19.6f, grey, Vector3.forward);
            Prims.Box(root, new Vector3(0, 3.3f, 11.1f), new Vector3(4.6f, 4.6f, 19.8f), null).GetComponent<Renderer>().enabled = false;
            Prims.Box(root, new Vector3(0, 1.5f, 11.1f), new Vector3(4.0f, 1.3f, 19.8f), grey);
            Prims.Ball(root, new Vector3(0, 3.35f, 20f), new Vector3(4.8f, 4.6f, 8f), grey, true);
            Prims.Ball(root, new Vector3(0, 2.4f, 20.4f), new Vector3(4.2f, 3.2f, 6.5f), grey);
            Prims.Ball(root, new Vector3(0, 2.95f, 23.6f), new Vector3(1.8f, 1.7f, 1.4f), black);
            for (int i = 0; i < 4; i++)
                Prims.Box(root, new Vector3(-0.9f + i * 0.6f, 4.75f, 21.75f), new Vector3(0.5f, 0.34f, 0.05f), glass, false, Quaternion.Euler(-40, (i - 1.5f) * -12f, 0));
            foreach (float side in new[] { -1f, 1f })
                Prims.Box(root, new Vector3(side * 1.75f, 4.55f, 21.2f), new Vector3(0.05f, 0.34f, 0.6f), glass, false, Quaternion.Euler(0, side * -25f, 0));
            // a row of little windows, the crew door, roundels and the name
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 6; i++) Prims.Box(root, new Vector3(side * 2.38f, 3.7f, 3.6f + i * 2.4f), new Vector3(0.05f, 0.36f, 0.36f), glass, false);
                Prims.Box(root, new Vector3(side * 2.39f, 2.4f, 17.2f), new Vector3(0.04f, 1.8f, 0.9f), greyDark, false);
                Prims.Cyl(root, new Vector3(side * 2.41f, 3.9f, 7f), 1.3f, 0.02f, Prims.Lit(Color.white, 0.4f), Vector3.right);
                Prims.Cyl(root, new Vector3(side * 2.43f, 3.9f, 7f), 0.8f, 0.02f, MapKit.M("1f3a75", 0.4f), Vector3.right);
                var name = k.Sign(pos + new Vector3(side * 2.45f, 4.5f, 13f), side * 90f, "FLOPZONE AIR", 0.35f, new Color(0.15f, 0.15f, 0.17f));
                name.fontStyle = FontStyle.Bold;
            }

            // upswept tail over the open ramp
            var tailDir = Quaternion.Euler(20, 0, 0) * Vector3.forward;
            Prims.Cyl(root, new Vector3(0, 5.2f, -3.2f), 3.9f, 8.4f, grey, tailDir);
            Prims.Box(root, new Vector3(0, 5.1f, -3.0f), new Vector3(3.9f, 1.4f, 7.2f), grey, true, Quaternion.Euler(20, 0, 0));
            Prims.Box(root, new Vector3(0, 2.6f, 1.15f), new Vector3(4.2f, 3.3f, 0.06f), Prims.Unlit(new Color(0.03f, 0.03f, 0.035f)), false);
            Prims.Box(root, new Vector3(0, 0.95f, 0.5f), new Vector3(3.6f, 0.2f, 1.4f), MapKit.M("4a4d50", 0.35f, 0.5f, Surface.Metal));
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Box(root, new Vector3(side * 2.05f, 2.0f, 0.6f), new Vector3(0.3f, 2.4f, 1.6f), grey);
                Prims.Cyl(root, new Vector3(side * 1.6f, 1.9f, -1.2f), 0.12f, 2.6f, steel, new Vector3(0, 0.8f, 1f));
            }
            // the ramp, down on the ground, with grip strips and chevrons
            var ramp = Prims.Box(root, new Vector3(0, 0.55f, -2.75f), new Vector3(3.4f, 0.15f, 5.6f), MapKit.M("4a4d50", 0.35f, 0.5f, Surface.Metal), true, Quaternion.Euler(-9.8f, 0, 0));
            for (int i = 0; i < 8; i++)
                Prims.Box(ramp.transform, new Vector3(0, 0.55f, -0.42f + i * 0.12f), new Vector3(0.95f, 0.1f, 0.01f), MapKit.M("202224", 0.2f, 0f, Surface.Rubber), false);
            foreach (float x in new[] { -1.55f, 1.55f })
                Prims.Box(ramp.transform, new Vector3(x / 3.4f, 0.55f, 0), new Vector3(0.04f, 0.1f, 1f), MilitaryProps.Hazard, false);

            // wings, engines with idling props, tail planes
            Prims.Box(root, new Vector3(0, 5.95f, 10.5f), new Vector3(34f, 0.4f, 4.4f), grey);
            Prims.Box(root, new Vector3(0, 5.75f, 10.5f), new Vector3(5f, 0.8f, 5f), grey);
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Box(root, new Vector3(side * 18.5f, 5.95f, 10.2f), new Vector3(3f, 0.3f, 3.2f), grey);
                Prims.Box(root, new Vector3(side * 20f, 5.95f, 10.0f), new Vector3(0.3f, 0.3f, 0.5f), Prims.Emissive(side < 0 ? new Color(1f, 0.1f, 0.1f) : new Color(0.1f, 1f, 0.2f), 3f), false);
                Prims.Box(root, new Vector3(side * 11f, 5.75f, 8.6f), new Vector3(9f, 0.06f, 0.6f), greyDark, false);
                foreach (float x in new[] { 5.5f, 10.5f })
                {
                    var ex = side * x;
                    Prims.Cyl(root, new Vector3(ex, 5.3f, 12.2f), 1.3f, 4.6f, grey, Vector3.forward);
                    Prims.Cyl(root, new Vector3(ex, 5.3f, 14.45f), 1.15f, 0.12f, black, Vector3.forward);
                    Prims.Box(root, new Vector3(ex, 4.85f, 10.6f), new Vector3(0.5f, 0.5f, 1.4f), black, false);
                    Prims.Ball(root, new Vector3(ex, 5.3f, 14.85f), new Vector3(0.6f, 0.6f, 0.9f), steel);
                    var prop = Prims.Empty("prop", dyn, new Vector3(ex, 5.3f, 14.75f)).transform;
                    for (int b = 0; b < 4; b++)
                        Prims.Box(prop, Vector3.zero, new Vector3(0.24f, 3.8f, 0.05f), black, false, Quaternion.Euler(0, 0, b * 45f + (b % 2) * 45f));
                    var spin = prop.gameObject.AddComponent<Spinner>();
                    spin.axis = Vector3.forward;
                    spin.speed = x < 8 ? 160f : 0f;   // the inner engines idle
                }
            }
            Prims.Box(root, new Vector3(0, 6.75f, -6.6f), new Vector3(14f, 0.3f, 3.4f), grey);
            Prims.Box(root, new Vector3(0, 9.6f, -6.9f), new Vector3(0.4f, 6.4f, 4.0f), grey, true, Quaternion.Euler(-15, 0, 0));
            foreach (float side in new[] { -1f, 1f })
                k.Sign(pos + new Vector3(side * 0.23f, 10.2f, -7.1f), side * 90f, "FZ-130", 0.35f, Color.white);

            // landing gear pods, wheels and the nose wheel
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Box(root, new Vector3(side * 2.55f, 1.45f, 9f), new Vector3(1.1f, 1.7f, 7f), grey);
                foreach (float z in new[] { 7.5f, 10.5f })
                {
                    Prims.Cyl(root, new Vector3(side * 2.75f, 0.6f, z), 1.2f, 0.5f, tire, Vector3.right, true);
                    Prims.Cyl(root, new Vector3(side * 3.01f, 0.6f, z), 0.6f, 0.04f, steel, Vector3.right);
                }
            }
            Prims.Cyl(root, new Vector3(0, 0.95f, 19f), 0.18f, 1.2f, steel, Vector3.up);
            Prims.Cyl(root, new Vector3(0, 0.45f, 19f), 0.9f, 0.5f, tire, Vector3.right, true);
            Prims.Box(root, new Vector3(0, 5.72f, 6f), new Vector3(0.3f, 0.2f, 0.3f), Prims.Emissive(new Color(1f, 0.15f, 0.1f), 3f), false);

            // the way in: walk up the ramp
            var trig = Prims.Empty("boarding", k.D, pos + new Vector3(0, 0, 0.5f));
            trig.AddComponent<PlaneBoarding>();
            k.Sign(pos + new Vector3(0, 3.4f, 1.1f), 180, "BOARD HERE", 0.3f, new Color(0.95f, 0.85f, 0.4f));
            k.Sign(pos + new Vector3(0, 3.1f, 1.1f), 180, "walk up the ramp · your party comes with you", 0.13f, Color.white);
        }
    }

    /// <summary>The top of the parked plane's ramp: walk in and you board.</summary>
    public class PlaneBoarding : MonoBehaviour
    {
        float cooldown;

        void Update()
        {
            var rig = PlayerRig.I;
            var g = Game.I;
            if (rig == null || g == null || !rig.Alive || Time.time < cooldown || g.InPlane) return;
            var local = transform.InverseTransformPoint(rig.transform.position);
            if (Mathf.Abs(local.x) < 1.7f && Mathf.Abs(local.z) < 0.7f && local.y > -0.5f && local.y < 2.5f)
            {
                cooldown = Time.time + 3f;
                g.BoardPlane(true);
            }
        }
    }
}
