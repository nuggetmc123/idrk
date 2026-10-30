using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// The lobby helicopter. Outside: a big twin-rotor helicopter parked in the hub
    /// with its ramp down. Walk up the ramp and you're inside the cabin (built far
    /// above every map), the ramp closes behind you and your party is pulled in.
    /// In here: the lobby screen (party leader or solo players only) to pick a mode
    /// and public or private, six loadout stations, arcade machines, and the wait
    /// for a full match.
    /// </summary>
    public class Chopper : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(0, 500, 0);
        /// <summary>Where the parked helicopter's ramp meets the cabin (in the hub).</summary>
        public static readonly Vector3 HubSpot = new Vector3(0, 0, 27f);
        /// <summary>Where you come out in the hub if you leave: just past the foot of the ramp.</summary>
        public static readonly Vector3 HubExit = new Vector3(0, 0.05f, 21f);

        public bool Occupied { get; private set; }
        Transform door;
        float doorAngle = 0f, doorTarget = 0f;
        Material cloudsL, cloudsR, jumpLight;
        UIPanel screen;
        string screenKey = "";
        float nextScreen;
        /// <summary>The leader's choice on the lobby screen.</summary>
        bool privateMatch;

        const float OpenAngle = -100f;

        static readonly Vector2[] Seats =
        {
            new Vector2(0, -6f), new Vector2(-1.2f, -6f), new Vector2(1.2f, -6f),
            new Vector2(-1.2f, -7f), new Vector2(1.2f, -7f), new Vector2(0, -7f)
        };

        /* ======================= inside ======================= */

        public static Chopper Create()
        {
            var root = new GameObject("Chopper");
            root.transform.position = Origin;
            var a = root.AddComponent<Chopper>();
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
            UIKit.Text(t, "COCKPIT", new Vector3(1.55f, 1.7f, 7.96f), 0.06f, new Color(0.95f, 0.85f, 0.4f));
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
            string key = $"{s.RoomKind}|{s.State}|{leader}|{s.Mode}|{s.PrivateMatch}|{privateMatch}|{s.Players.Count}|{(s.InMatch ? secs : 0)}|{(s.State == "countdown" ? Mathf.CeilToInt(s.TimeLeft) : 0)}";
            if (key == screenKey) return;
            screenKey = key;
            screen.Clear();
            var c = UIKit.Accent;
            if (s.InMatch && md != null && (s.State == "waiting" || s.State == "countdown"))
            {
                bool found = s.State == "countdown";
                string head0 = found ? (s.PrivateMatch ? "PRIVATE MATCH" : "MATCH FOUND!")
                             : s.PrivateMatch ? "STARTING PRIVATE MATCH..." : "FINDING MATCH" + new string('.', 1 + secs % 3);
                var title = screen.Text(head0, new Vector2(0, 0.42f), 0.1f, found ? UIKit.Good : c);
                title.fontStyle = FontStyle.Bold;
                screen.Text(md.name.ToUpperInvariant() + (s.PrivateMatch ? "  ·  PRIVATE" : "  ·  PUBLIC"), new Vector2(0, 0.25f), 0.07f, Color.white);
                screen.Text($"{s.Players.Count} / {md.max} players", new Vector2(0, 0.12f), 0.06f, UIKit.Muted);
                if (found) screen.Text("Deploying in " + Mathf.CeilToInt(s.TimeLeft) + "...", new Vector2(0, -0.04f), 0.08f, Color.white);
                else if (s.PrivateMatch) screen.Text("Just your party. Bots fill the empty spots.", new Vector2(0, -0.04f), 0.045f, UIKit.Muted);
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
            var head = screen.Text("SELECT GAME MODE", new Vector2(0, 0.49f), 0.08f, c);
            head.fontStyle = FontStyle.Bold;
            // public or private
            var pub = screen.Button("PUBLIC", new Vector2(-0.3f, 0.37f), new Vector2(0.5f, 0.1f), () => { privateMatch = false; screenKey = ""; }, 0.045f);
            var pri = screen.Button("PRIVATE", new Vector2(0.3f, 0.37f), new Vector2(0.5f, 0.1f), () => { privateMatch = true; screenKey = ""; }, 0.045f);
            var on = new Color(0.95f, 0.5f, 0.1f);
            (privateMatch ? pri : pub).SetColors(on, Color.Lerp(on, Color.white, 0.3f));
            pub.interactable = pri.interactable = leader;
            screen.Text(!leader ? "Only your party leader can pick the mode."
                        : privateMatch ? "PRIVATE: just your party, straight in. Bots fill the empty spots."
                        : "PUBLIC: anyone can join. Finding a match takes up to 1:00, then bots fill in.",
                new Vector2(0, 0.28f), 0.033f, UIKit.Muted);
            for (int i = 0; i < Hub.Modes.Length; i++)
            {
                var p = Hub.Modes[i];
                var pos = new Vector2(i % 2 == 0 ? -0.48f : 0.48f, 0.14f - (i / 2) * 0.2f);
                var b = screen.Button(p.label + "\n<size=22>" + p.info + "</size>", pos, new Vector2(0.9f, 0.17f), () => Game.I.GoMode(p.mode, privateMatch), 0.042f);
                b.interactable = leader;
            }
            screen.Button("LEAVE HELICOPTER", new Vector2(0, -0.5f), new Vector2(0.7f, 0.11f), () => Game.I.LeaveChopper(), 0.04f);
        }

        /* ======================= outside, in the hub ======================= */

        /// <summary>
        /// The big twin-rotor helicopter parked in the hub. pos is the top of its rear ramp
        /// (the back of the cabin); the nose points along +z and the ramp comes down toward -z.
        /// </summary>
        public static void BuildExterior(MapKit k, Vector3 pos)
        {
            var root = Prims.Empty("lobby helicopter", k.S, pos).transform;
            var dyn = Prims.Empty("lobby helicopter (rotors)", k.D, pos).transform;
            var olive = MapKit.M("4f5a2a", 0.3f, 0.3f, Surface.PaintedMetal);
            var oliveDark = MapKit.M("39421d", 0.3f, 0.3f, Surface.PaintedMetal);
            var black = MapKit.M("1d1f21", 0.3f, 0.2f, Surface.Rubber);
            var glass = MilitaryProps.Glass;
            var steel = MilitaryProps.DarkSteel;
            var tire = MilitaryProps.Tire;
            var floorM = MapKit.M("4a4d50", 0.35f, 0.5f, Surface.Metal);
            const float len = 13.5f;   // cabin length

            // the long, boxy cabin with rounded top and belly
            // (it starts 1.2 m in, so the open back of the hold shows behind the ramp)
            Prims.Box(root, new Vector3(0, 2.25f, (len + 1.2f) / 2), new Vector3(4.0f, 2.7f, len - 1.2f), olive, false);
            Prims.Make(PrimitiveType.Cylinder, root, new Vector3(0, 3.6f, (len + 1.2f) / 2), new Vector3(4.0f, (len - 1.2f) / 2, 1.5f), olive, false, Quaternion.Euler(90, 0, 0));
            Prims.Make(PrimitiveType.Cylinder, root, new Vector3(0, 0.9f, len / 2 + 0.6f), new Vector3(3.8f, len / 2 - 0.6f, 0.7f), oliveDark, false, Quaternion.Euler(90, 0, 0));
            Prims.Box(root, new Vector3(0, 2.4f, (len + 1.2f) / 2), new Vector3(4.0f, 3.6f, len - 1.2f), null).GetComponent<Renderer>().enabled = false;
            // the cockpit: a rounded nose with a big windscreen
            Prims.Ball(root, new Vector3(0, 2.5f, len), new Vector3(4.0f, 3.4f, 6.0f), olive, true);
            Prims.Ball(root, new Vector3(0, 1.6f, len + 0.3f), new Vector3(3.6f, 1.6f, 5.2f), oliveDark);
            for (int i = 0; i < 4; i++)
                Prims.Box(root, new Vector3(-1.05f + i * 0.7f, 3.3f, len + 2.35f), new Vector3(0.62f, 0.75f, 0.05f), glass, false, Quaternion.Euler(-35, (i - 1.5f) * -14f, 0));
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Box(root, new Vector3(side * 1.75f, 3.0f, len + 1.4f), new Vector3(0.05f, 0.8f, 1.2f), glass, false, Quaternion.Euler(0, side * -22f, 0));
                Prims.Box(root, new Vector3(side * 1.55f, 1.6f, len + 2.0f), new Vector3(0.05f, 0.5f, 0.6f), glass, false, Quaternion.Euler(0, side * -30f, 0));
            }
            Prims.Cyl(root, new Vector3(0.9f, 3.0f, len + 3.2f), 0.12f, 2.4f, steel, new Vector3(0, 0.15f, 1f));   // refuelling probe

            // round windows, a crew door, the name and roundels down both sides; fuel pods low down
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 6; i++) Prims.Cyl(root, new Vector3(side * 2.01f, 2.7f, 2f + i * 1.8f), 0.42f, 0.04f, glass, Vector3.right);
                Prims.Box(root, new Vector3(side * 2.02f, 1.95f, len - 1.6f), new Vector3(0.04f, 1.8f, 0.95f), oliveDark, false);
                Prims.Cyl(root, new Vector3(side * 2.03f, 2.3f, 5.6f), 1.1f, 0.02f, Prims.Lit(Color.white, 0.4f), Vector3.right);
                Prims.Cyl(root, new Vector3(side * 2.05f, 2.3f, 5.6f), 0.62f, 0.02f, MapKit.M("1f3a75", 0.4f), Vector3.right);
                var nm = k.Sign(pos + new Vector3(side * 2.06f, 1.45f, 8.4f), side * 90f, "SLOPPY", 0.42f, new Color(0.08f, 0.09f, 0.06f));
                nm.fontStyle = FontStyle.Bold;
                Prims.Make(PrimitiveType.Capsule, root, new Vector3(side * 2.35f, 1.15f, 6.8f), new Vector3(0.9f, 4.0f, 1.0f), olive, false, Quaternion.Euler(90, 0, 0));
                Prims.Box(root, new Vector3(side * 2.15f, 0.4f, 2.6f), new Vector3(0.2f, 0.8f, 0.2f), steel, false);
                Prims.Cyl(root, new Vector3(side * 2.2f, 0.5f, 2.6f), 1.0f, 0.45f, tire, Vector3.right, true);
                Prims.Cyl(root, new Vector3(side * 2.2f, 0.5f, 2.6f), 0.5f, 0.48f, steel, Vector3.right);
                foreach (float z in new[] { len - 2.3f, len - 1.3f })
                {
                    Prims.Cyl(root, new Vector3(side * 1.3f, 0.45f, z), 0.9f, 0.4f, tire, Vector3.right, true);
                    Prims.Cyl(root, new Vector3(side * 1.3f, 0.45f, z), 0.45f, 0.42f, steel, Vector3.right);
                }
            }

            // the back: open hold over the lowered ramp
            Prims.Box(root, new Vector3(0, 2.4f, 1.15f), new Vector3(3.8f, 2.9f, 0.06f), Prims.Unlit(new Color(0.03f, 0.03f, 0.035f)), false);
            Prims.Box(root, new Vector3(0, 0.95f, 0.5f), new Vector3(3.6f, 0.2f, 1.4f), floorM);
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Box(root, new Vector3(side * 1.9f, 2.2f, 0.6f), new Vector3(0.22f, 2.7f, 1.6f), olive);
                Prims.Cyl(root, new Vector3(side * 1.5f, 1.9f, -1.0f), 0.1f, 2.4f, steel, new Vector3(0, 0.8f, 1f));
            }
            Prims.Box(root, new Vector3(0, 3.95f, 0.6f), new Vector3(4.0f, 0.5f, 1.6f), olive);
            var lobby = k.Sign(pos + new Vector3(0, 5.0f, 0.15f), 180, "LOBBY", 0.7f, new Color(0.95f, 0.88f, 0.55f));
            lobby.fontStyle = FontStyle.Bold;
            var ramp = Prims.Box(root, new Vector3(0, 0.5f, -2.5f), new Vector3(3.2f, 0.15f, 5.1f), floorM, true, Quaternion.Euler(-10.4f, 0, 0));
            for (int i = 0; i < 8; i++)
                Prims.Box(ramp.transform, new Vector3(0, 0.55f, -0.42f + i * 0.12f), new Vector3(0.95f, 0.1f, 0.01f), MapKit.M("202224", 0.2f, 0f, Surface.Rubber), false);
            foreach (float x in new[] { -1.45f, 1.45f })
                Prims.Box(ramp.transform, new Vector3(x / 3.2f, 0.55f, 0), new Vector3(0.04f, 0.1f, 1f), MilitaryProps.Hazard, false);

            // two rotor towers, the engines either side of the back one, and the rotors
            Prims.Box(root, new Vector3(0, 4.9f, 2.4f), new Vector3(2.0f, 2.2f, 4.4f), olive);
            Prims.Box(root, new Vector3(0, 5.9f, 1.4f), new Vector3(1.6f, 1.4f, 2.4f), olive, true, Quaternion.Euler(-12, 0, 0));
            Prims.Box(root, new Vector3(0, 4.55f, len - 1.0f), new Vector3(1.5f, 0.9f, 2.6f), olive);
            Prims.Box(root, new Vector3(0, 4.25f, len / 2), new Vector3(0.9f, 0.5f, len - 3f), oliveDark);
            foreach (float side in new[] { -1f, 1f })
            {
                Prims.Cyl(root, new Vector3(side * 1.65f, 4.85f, 2.0f), 1.05f, 3.4f, oliveDark, Vector3.forward);
                Prims.Cyl(root, new Vector3(side * 1.65f, 4.85f, 0.25f), 0.8f, 0.2f, black, Vector3.forward);
                Prims.Box(root, new Vector3(side * 1.2f, 4.85f, 2.0f), new Vector3(0.5f, 0.3f, 1.8f), steel, false);
            }
            Rotor(root, dyn, new Vector3(0, 7.05f, 1.3f), olive, steel, black, 25f);
            Rotor(root, dyn, new Vector3(0, 5.35f, len - 1.0f), olive, steel, black, -25f);
            Prims.Box(root, new Vector3(0, 4.35f, len / 2), new Vector3(0.3f, 0.2f, 0.3f), Prims.Emissive(new Color(1f, 0.15f, 0.1f), 3f), false);

            // the way in: walk up the ramp
            var trig = Prims.Empty("boarding", k.D, pos + new Vector3(0, 0, 0.5f));
            trig.AddComponent<ChopperBoarding>();
            var board = k.Sign(pos + new Vector3(0, 3.0f, 1.1f), 180, "BOARD HERE", 0.32f, new Color(0.95f, 0.85f, 0.4f));
            board.fontStyle = FontStyle.Bold;
            k.Sign(pos + new Vector3(0, 2.62f, 1.1f), 180, "walk up the ramp · your party comes with you", 0.13f, Color.white);
            k.Sign(pos + new Vector3(0, 2.3f, 1.1f), 180, "pick a mode · public or private", 0.13f, Color.white);
        }

        /// <summary>A three-bladed rotor turning slowly (the engines are idling).</summary>
        static void Rotor(Transform root, Transform dyn, Vector3 at, Material m, Material steel, Material black, float speed)
        {
            Prims.Cyl(root, at - Vector3.up * 0.35f, 0.35f, 0.7f, steel, Vector3.up);
            var spin = Prims.Empty("rotor", dyn, at).transform;
            Prims.Cyl(spin, Vector3.zero, 0.75f, 0.3f, steel, Vector3.up);
            Prims.Ball(spin, Vector3.up * 0.2f, new Vector3(0.6f, 0.35f, 0.6f), m);
            for (int b = 0; b < 3; b++)
            {
                var arm = Prims.Empty("blade", spin, Vector3.zero).transform;
                arm.localRotation = Quaternion.Euler(0, b * 120f, 0);
                Prims.Box(arm, new Vector3(4.6f, 0, 0), new Vector3(8.6f, 0.07f, 0.6f), black, false, Quaternion.Euler(4, 0, 0));
                Prims.Box(arm, new Vector3(8.75f, 0.005f, 0), new Vector3(0.3f, 0.075f, 0.61f), MilitaryProps.Hazard, false);
            }
            var sp = spin.gameObject.AddComponent<Spinner>();
            sp.speed = speed;
        }
    }

    /// <summary>The top of the parked helicopter's ramp: walk in and you board.</summary>
    public class ChopperBoarding : MonoBehaviour
    {
        float cooldown;

        void Update()
        {
            var rig = PlayerRig.I;
            var g = Game.I;
            if (rig == null || g == null || !rig.Alive || Time.time < cooldown || g.InLobby) return;
            var local = transform.InverseTransformPoint(rig.transform.position);
            if (Mathf.Abs(local.x) < 1.7f && Mathf.Abs(local.z) < 0.7f && local.y > -0.5f && local.y < 2.5f)
            {
                cooldown = Time.time + 3f;
                g.BoardChopper(true);
            }
        }
    }
}
