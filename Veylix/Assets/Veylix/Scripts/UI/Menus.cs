using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Every menu screen: main menu, mode lists, map selection (all 25 maps), session setup, garage, settings,
    /// leaderboards, online, loading, pause, crash, results and championship standings. Screens are rebuilt from
    /// scratch when shown (simple and always in sync with the data).
    /// </summary>
    public class Menus : MonoBehaviour
    {
        Transform panel;
        const float W = 1.16f, H = 0.7f;
        SessionConfig pending;
        int garageCar, garageTab, settingsTab, boardTab, mapPage;
        CarSetup garageSetup;
        System.Action<int> mapPicked;
        GameMode mapMode;

        Game G => Game.I;

        void Awake()
        {
            panel = new GameObject("Panel").transform;
            panel.SetParent(transform, false);
            gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ framework

        Transform Begin(string title, string subtitle = null, bool inSession = false, float w = W, float h = H)
        {
            VUI.Clear(panel);
            Pointer.MenuActive = true;
            Place(inSession);
            var bg = VUI.Quad(panel, "bg", Vector2.zero, new Vector2(w, h), VUI.Panel, 0.002f, 0.016f);
            VUI.Quad(panel, "topline", new Vector2(0f, h * 0.5f - 0.0015f), new Vector2(w - 0.03f, 0.003f), VUI.Accent, 0.001f, 0.0015f);
            if (!string.IsNullOrEmpty(title))
            {
                VUI.Label(panel, title, new Vector2(-w * 0.5f + 0.035f, h * 0.5f - 0.045f), 0.52f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, w * 0.7f).characterSpacing = 4f;
                if (subtitle != null) VUI.Label(panel, subtitle, new Vector2(-w * 0.5f + 0.035f, h * 0.5f - 0.082f), 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, w * 0.85f);
            }
            return panel;
        }

        void Place(bool inSession)
        {
            var rig = Rig.I;
            if (rig == null) return;
            Vector3 headPos = rig.head.position;
            Vector3 fwd = rig.head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            if (!inSession && G != null && G.state == Game.State.Menu)
            {
                // In your garage: in front of where you're looking, off to the left so the car stays in view.
                Vector3 basePos = new Vector3(headPos.x, rig.transform.position.y, headPos.z);
                float eye = rig.IsXR ? Mathf.Max(1.1f, rig.head.position.y - rig.transform.position.y) : 1.6f;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 pos = basePos + fwd * 0.95f - right * hubSideOffset + Vector3.up * (eye - 0.18f);
                Vector3 face = pos - new Vector3(headPos.x, pos.y, headPos.z);
                panel.position = pos;
                panel.rotation = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : fwd, Vector3.up);
            }
            else
            {
                panel.position = headPos + fwd * 0.85f + Vector3.down * 0.12f;
                panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            if (!rig.IsXR && Cameras.Main != null && (inSession || G == null || G.state != Game.State.Menu))
            {
                // Desktop: right in front of the camera so it fills the view.
                var c = Cameras.Main.transform;
                panel.position = c.position + c.forward * 0.95f;
                panel.rotation = Quaternion.LookRotation(c.forward, c.up);
            }
            Layers.SetRecursive(panel.gameObject, Layers.UI);
        }

        float hubSideOffset = 0.42f;
        public bool Showing => Pointer.MenuActive;

        // ------------------------------------------------------------------ garage hub stations

        /// CUSTOMISE station: step up beside the car and open the Garage.
        public void OpenGarage()
        {
            if (G.showroom != null) G.showroom.GoTo(Showroom.Spot.Customise);
            hubSideOffset = 0.55f;
            ShowGarage();
            hubSideOffset = 0.42f;
        }

        /// CHOOSE A TRACK station: straight to the map grid (then session setup and Start).
        public void OpenMapSelection() => PickMap(GameMode.QuickRace);

        /// DRIVE station by the roller door: every mode.
        public void OpenDrive() => ShowModes(true);

        public void Hide()
        {
            loadingBar = null; loadingDot = null; loadingTip = null; splash = false;
            VUI.Clear(panel);
            Pointer.MenuActive = false;
        }

        VButton Btn(string text, float x, float y, float w, float h, System.Action a, bool primary = false, string sub = null) => VButton.Make(panel, text, new Vector2(x, y), new Vector2(w, h), a, primary, sub);
        TextMeshPro Lbl(string text, float x, float y, float size, Color c, TextAlignmentOptions al = TextAlignmentOptions.MidlineLeft, bool disp = false, float width = 0.5f) => VUI.Label(panel, text, new Vector2(x, y), size, c, al, disp, width);
        void Back(System.Action a) => Btn("BACK", -W * 0.5f + 0.1f, -H * 0.5f + 0.045f, 0.15f, 0.045f, a);

        // ------------------------------------------------------------------ main menu

        public void ShowMain()
        {
            Begin(null);
            float x0 = -W * 0.5f + 0.04f;
            var title = Lbl("VEYLIX", x0, H * 0.5f - 0.075f, 1.35f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.6f);
            title.characterSpacing = 18f;
            Lbl("FORMULA RACING  -  VR", x0 + 0.004f, H * 0.5f - 0.15f, 0.25f, VUI.Accent, TextAlignmentOptions.MidlineLeft, true, 0.5f).characterSpacing = 8f;
            string[] names = { "DRIVE", "RACE", "FREE ROAM", "TIME TRIAL", "GARAGE", "MAP SELECTION", "SETTINGS", "LEADERBOARDS", "ONLINE", "QUIT" };
            System.Action[] acts =
            {
                () => ShowModes(true), () => ShowModes(false), () => PickMap(GameMode.FreeRoam), () => PickMap(GameMode.TimeTrial),
                ShowGarage, () => PickMap(GameMode.QuickRace, true), ShowSettings, ShowLeaderboards, ShowOnline, () => G.Quit(),
            };
            for (int i = 0; i < names.Length; i++)
            {
                int col = i / 5, row = i % 5;
                Btn(names[i], x0 + 0.13f + col * 0.27f, H * 0.5f - 0.235f - row * 0.075f, 0.25f, 0.06f, acts[i], false);
            }
            // Right: what's lined up, and a one-click start.
            var cfg = G.config;
            var map = MapCatalog.Get(cfg.map);
            var car = CarCatalog.Get(cfg.car);
            float rx = 0.18f;
            VUI.Quad(panel, "card", new Vector2(rx + 0.1f, -0.02f), new Vector2(0.44f, 0.56f), VUI.PanelLight, 0.001f, 0.012f);
            Lbl("NEXT SESSION", rx - 0.1f, 0.225f, 0.22f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.4f).characterSpacing = 4f;
            Lbl(map.name.ToUpperInvariant(), rx - 0.1f, 0.19f, 0.34f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.42f);
            Lbl(map.location + "  |  " + map.KindLabel, rx - 0.1f, 0.155f, 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.42f);
            var tex = Preview(cfg.map);
            if (tex != null) VUI.Quad(panel, "map", new Vector2(rx + 0.1f, 0.03f), new Vector2(0.36f, 0.2f), Color.white, 0f, 0.008f, tex);
            Lbl(car.name.ToUpperInvariant(), rx - 0.1f, -0.1f, 0.3f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.42f);
            Lbl(car.era + "  |  " + ModeName(cfg.mode), rx - 0.1f, -0.132f, 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.42f);
            Btn("QUICK START", rx + 0.1f, -0.22f, 0.38f, 0.07f, () => { var c = G.config.Clone(); if (c.mode == GameMode.Online) c.mode = GameMode.QuickRace; G.StartSession(c); }, true);
            Lbl("Hold both thumbsticks in to recenter your view", 0f, -H * 0.5f + 0.025f, 0.18f, VUI.TextDim, TextAlignmentOptions.Midline, false, W);
        }

        static Texture Preview(int i)
        {
            var A = VXAssets.I;
            return A != null && A.mapPreviews != null && i < A.mapPreviews.Length ? A.mapPreviews[i] : null;
        }

        public static string ModeName(GameMode m)
        {
            switch (m)
            {
                case GameMode.GrandPrix: return "Grand Prix";
                case GameMode.QuickRace: return "Quick Race";
                case GameMode.TimeTrial: return "Time Trial";
                case GameMode.Practice: return "Practice";
                case GameMode.Championship: return "Championship";
                case GameMode.Elimination: return "Elimination";
                case GameMode.FreeRoam: return "Free Roam";
                default: return "Online Race";
            }
        }

        static string ModeBlurb(GameMode m)
        {
            switch (m)
            {
                case GameMode.GrandPrix: return "Full race distance against the field from a standing start.";
                case GameMode.QuickRace: return "Pick the track, laps, grid and AI difficulty, and go.";
                case GameMode.TimeTrial: return "Just you, the clock and your best-lap ghost.";
                case GameMode.Practice: return "Racing line and braking markers on track. Learn the corners.";
                case GameMode.Championship: return "A season of races. Points for the top ten.";
                case GameMode.Elimination: return "The last car is knocked out at the end of every lap.";
                case GameMode.FreeRoam: return "Drive anywhere. Time and weather change as you go.";
                default: return "";
            }
        }

        void ShowModes(bool all)
        {
            Begin(all ? "DRIVE" : "RACE", all ? "Choose how you want to drive" : "Wheel to wheel against the AI field");
            var modes = all
                ? new[] { GameMode.GrandPrix, GameMode.QuickRace, GameMode.Championship, GameMode.Elimination, GameMode.TimeTrial, GameMode.Practice, GameMode.FreeRoam }
                : new[] { GameMode.GrandPrix, GameMode.QuickRace, GameMode.Championship, GameMode.Elimination };
            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                int col = i % 2, row = i / 2;
                Btn(ModeName(m).ToUpperInvariant(), -0.27f + col * 0.54f, 0.17f - row * 0.11f, 0.52f, 0.095f, () => { if (m == GameMode.Championship) ShowChampionshipSetup(); else PickMap(m); }, false, ModeBlurb(m));
            }
            Back(ShowMain);
        }

        // ------------------------------------------------------------------ maps

        void PickMap(GameMode mode, bool browseOnly = false)
        {
            mapMode = mode;
            pending = G.config.Clone();
            pending.mode = mode;
            mapPicked = i =>
            {
                pending.map = i;
                if (browseOnly) { G.config.map = i; ShowMain(); }
                else ShowSetup();
            };
            ShowMaps(browseOnly);
        }

        void ShowMaps(bool browseOnly)
        {
            Begin(browseOnly ? "MAP SELECTION" : ModeName(mapMode).ToUpperInvariant() + "  -  CHOOSE A MAP", "15 circuits and 10 open worlds. Every map works in every mode.");
            float cw = 0.2f, ch = 0.105f;
            int sel = pending.map;
            for (int i = 0; i < MapCatalog.Count; i++)
            {
                int col = i % 5, row = i / 5;
                var m = MapCatalog.Get(i);
                float x = -W * 0.5f + 0.13f + col * (cw + 0.012f);
                float y = H * 0.5f - 0.16f - row * (ch + 0.012f);
                int idx = i;
                var card = VCard.Make(panel, new Vector2(x, y), new Vector2(cw, ch), new Color(0.11f, 0.12f, 0.13f, 1f), null, () => { pending.map = idx; ShowMapDetail(idx, browseOnly); });
                card.selected = i == sel;
                card.Refresh();
                var tex = Preview(i);
                if (tex != null) VUI.Quad(card.transform, "img", new Vector2(0f, 0.012f), new Vector2(cw - 0.012f, ch - 0.036f), Color.white, -0.001f, 0.004f, tex);
                VUI.Label(card.transform, (i + 1) + "  " + m.name.ToUpperInvariant(), new Vector2(-cw * 0.5f + 0.008f, -ch * 0.5f + 0.012f), 0.16f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, cw - 0.01f, -0.002f);
                if (m.freeRoam) VUI.Quad(card.transform, "tag", new Vector2(cw * 0.5f - 0.012f, ch * 0.5f - 0.01f), new Vector2(0.01f, 0.01f), VUI.Good, -0.002f, 0.005f);
            }
            Back(ShowMain);
            Btn("SELECT  " + MapCatalog.Get(sel).name.ToUpperInvariant(), W * 0.5f - 0.2f, -H * 0.5f + 0.045f, 0.36f, 0.05f, () => mapPicked(pending.map), true);
        }

        void ShowMapDetail(int i, bool browseOnly)
        {
            var m = MapCatalog.Get(i);
            Begin(m.name.ToUpperInvariant(), m.location + "   |   " + m.KindLabel);
            var tex = Preview(i);
            if (tex != null) VUI.Quad(panel, "img", new Vector2(-0.24f, 0.02f), new Vector2(0.6f, 0.4f), Color.white, 0f, 0.01f, tex);
            float x = 0.1f;
            var lbl = Lbl(m.blurb, x, 0.17f, 0.26f, VUI.Text, TextAlignmentOptions.TopLeft, false, 0.44f);
            float lenKm = EstimateLength(m) / 1000f;
            string pb = SaveData.FormatTime(SaveData.BestLap(SaveData.D.timeTrial, m.id));
            string rows = "LENGTH      " + lenKm.ToString("0.00") + " km\n" +
                          "DEFAULT     " + m.laps + " laps\n" +
                          "TIME        " + Mathf.FloorToInt(m.hour).ToString("00") + ":" + Mathf.RoundToInt(Mathf.Repeat(m.hour, 1f) * 60f).ToString("00") + "\n" +
                          "WEATHER     " + Sky.WeatherName(m.weather) + "\n" +
                          "YOUR BEST   " + pb;
            Lbl(rows, x, -0.07f, 0.24f, VUI.TextDim, TextAlignmentOptions.TopLeft, true, 0.44f);
            Back(() => ShowMaps(browseOnly));
            Btn(browseOnly ? "SET AS NEXT MAP" : "CHOOSE", W * 0.5f - 0.17f, -H * 0.5f + 0.045f, 0.3f, 0.055f, () => mapPicked(i), true);
        }

        static float EstimateLength(MapDef m)
        {
            float len = 0f;
            var p = m.points;
            int n = p.Length / 3;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                len += new Vector2((p[j * 3] - p[i * 3]) * m.scale, (p[j * 3 + 1] - p[i * 3 + 1]) * m.scale).magnitude;
            }
            return len * 1.04f;
        }

        // ------------------------------------------------------------------ session setup

        void ShowSetup()
        {
            var m = MapCatalog.Get(pending.map);
            var c = pending;
            if (c.laps <= 0) c.laps = m.laps;
            Begin(ModeName(c.mode).ToUpperInvariant() + "  -  " + m.name.ToUpperInvariant(), "Set up the session, then start.");
            float lx = -W * 0.5f + 0.05f, cx = -0.08f;
            float y = H * 0.5f - 0.15f, dy = 0.058f;
            bool race = c.mode == GameMode.GrandPrix || c.mode == GameMode.QuickRace || c.mode == GameMode.Elimination;
            if (c.mode == GameMode.GrandPrix) { c.laps = m.laps; }
            void Row(string label) { Lbl(label, lx, y, 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f); }
            Row("CAR");
            var carNames = new string[CarCatalog.Count];
            for (int i = 0; i < carNames.Length; i++) carNames[i] = CarCatalog.Get(i).name;
            VSelector.Make(panel, new Vector2(cx, y), 0.36f, carNames, c.car, v => c.car = v);
            y -= dy;
            if (race)
            {
                if (c.mode != GameMode.GrandPrix)
                {
                    Row("LAPS");
                    VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 1, 50, c.laps, v => c.laps = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v).ToString(), 1f);
                    y -= dy;
                }
                Row("OPPONENTS");
                VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 1, Settings.Quality == 0 ? 11 : 19, c.opponents, v => c.opponents = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v).ToString(), 1f);
                y -= dy;
                Row("AI DIFFICULTY");
                VSelector.Make(panel, new Vector2(cx, y), 0.36f, SessionConfig.DifficultyNames, c.difficulty, v => c.difficulty = v);
                y -= dy;
                Row("GRID POSITION");
                VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 1, 20, c.gridPosition, v => c.gridPosition = Mathf.RoundToInt(v), v => "P" + Mathf.RoundToInt(v), 1f);
                y -= dy;
                if (c.mode == GameMode.Elimination)
                {
                    Row("ELIMINATE EVERY");
                    VSelector.Make(panel, new Vector2(cx, y), 0.36f, new[] { "1 lap", "2 laps", "3 laps" }, c.eliminationEvery - 1, v => c.eliminationEvery = v + 1);
                    y -= dy;
                }
            }
            Row("TIME OF DAY");
            VSelector.Make(panel, new Vector2(cx, y), 0.36f, SessionConfig.TimeNames, c.timePreset + 1, v => { c.timePreset = v - 1; ShowSetup(); });
            y -= dy;
            if (c.timePreset == 5)
            {
                Row("HOUR");
                VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 0f, 23.75f, c.customHour, v => c.customHour = v, v => Mathf.FloorToInt(v).ToString("00") + ":" + Mathf.RoundToInt(Mathf.Repeat(v, 1f) * 60f).ToString("00"), 0.25f);
                y -= dy;
            }
            Row("WEATHER");
            VSelector.Make(panel, new Vector2(cx, y), 0.36f, SessionConfig.WeatherNames, c.weather + 1, v => c.weather = v - 1);
            y -= dy;
            Row("TIME PASSES");
            VToggle.Make(panel, new Vector2(cx - 0.15f, y), c.dynamicTime || c.mode == GameMode.FreeRoam, v => c.dynamicTime = v);
            y -= dy;
            // Right column: the map and a summary.
            var tex = Preview(c.map);
            if (tex != null) VUI.Quad(panel, "img", new Vector2(0.36f, 0.12f), new Vector2(0.36f, 0.24f), Color.white, 0f, 0.008f, tex);
            Lbl("Damage: " + Settings.Damage + "   Assists: " + (Settings.Abs ? "ABS " : "") + (Settings.TractionControl ? "TC " : "") + (Settings.AutoGears ? "Auto gears" : "Manual gears"), 0.18f, -0.06f, 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.4f);
            Lbl("Change assists and damage in Settings.", 0.18f, -0.09f, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.4f);
            Back(() => ShowMaps(false));
            Btn("START", W * 0.5f - 0.15f, -H * 0.5f + 0.05f, 0.26f, 0.065f, () => { G.config = pending.Clone(); G.StartSession(pending); }, true);
        }

        void ShowChampionshipSetup()
        {
            var s = Championship.S;
            Begin("CHAMPIONSHIP", s.active ? "Season in progress: round " + (s.round + 1) + " of " + s.calendar.Count : "Start a new season");
            if (s.active && !Championship.Finished)
            {
                var m = MapCatalog.Get(Championship.CurrentMap);
                Lbl("NEXT ROUND:  " + m.name.ToUpperInvariant(), -W * 0.5f + 0.05f, 0.18f, 0.32f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.7f);
                Btn("CONTINUE SEASON", -0.3f, 0.08f, 0.4f, 0.07f, () => { G.config.mode = GameMode.Championship; G.NextChampionshipRound(); }, true);
                Btn("STANDINGS", 0.15f, 0.08f, 0.3f, 0.07f, () => ShowStandings(false));
                Btn("ABANDON SEASON", -0.3f, -0.02f, 0.4f, 0.06f, () => { Championship.End(); ShowChampionshipSetup(); });
                Back(() => ShowModes(false));
                return;
            }
            int rounds = 6, laps = 5, diff = G.config.difficulty, opp = Mathf.Min(G.config.opponents, 19);
            float lx = -W * 0.5f + 0.05f, cx = -0.08f, y = 0.18f;
            Lbl("ROUNDS", lx, y, 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 3, 15, rounds, v => rounds = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v).ToString(), 1f);
            y -= 0.06f;
            Lbl("LAPS PER RACE", lx, y, 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 2, 30, laps, v => laps = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v).ToString(), 1f);
            y -= 0.06f;
            Lbl("AI DIFFICULTY", lx, y, 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            VSelector.Make(panel, new Vector2(cx, y), 0.36f, SessionConfig.DifficultyNames, diff, v => diff = v);
            y -= 0.06f;
            Lbl("OPPONENTS", lx, y, 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            VSlider.Make(panel, new Vector2(cx - 0.03f, y), 0.3f, 5, Settings.Quality == 0 ? 11 : 19, Mathf.Max(5, opp), v => opp = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v).ToString(), 1f);
            Back(() => ShowModes(false));
            Btn("START SEASON", W * 0.5f - 0.17f, -H * 0.5f + 0.05f, 0.3f, 0.065f, () =>
            {
                Championship.Start(Championship.DefaultCalendar(rounds), laps, diff, opp);
                G.config.mode = GameMode.Championship;
                G.NextChampionshipRound();
            }, true);
        }

        // ------------------------------------------------------------------ garage

        void ShowGarage()
        {
            garageCar = G.config.car;
            garageSetup = SaveData.Setup(garageCar);
            BuildGarage();
        }

        void BuildGarage()
        {
            var spec = CarCatalog.Get(garageCar);
            var st = garageSetup;
            Begin("GARAGE", spec.maker + "  |  " + spec.era);
            float lx = -W * 0.5f + 0.13f;
            for (int i = 0; i < CarCatalog.Count; i++)
            {
                int idx = i;
                var b = Btn(CarCatalog.Get(i).name.ToUpperInvariant(), lx, H * 0.5f - 0.14f - i * 0.058f, 0.23f, 0.05f, () =>
                {
                    SaveData.StoreSetup(garageCar, garageSetup);
                    garageCar = idx; garageSetup = SaveData.Setup(idx); G.config.car = idx;
                    Refresh3D(); BuildGarage();
                });
                b.selected = i == garageCar; b.Refresh();
            }
            // Performance bars.
            float bx = lx - 0.1f, by = -0.17f;
            Bar("POWER", bx, by, (spec.peakTorque * spec.peakTorqueRpm / 9549f * 1.36f + spec.hybridKw * 1.36f) / 1100f);
            Bar("DOWNFORCE", bx, by - 0.035f, spec.downforceClA / 4.5f);
            Bar("GRIP", bx, by - 0.07f, (spec.gripMu - 1.2f) / 0.6f);
            Bar("LIGHTNESS", bx, by - 0.105f, (900f - spec.mass) / 350f);
            Lbl(spec.blurb, -0.27f, H * 0.5f - 0.13f, 0.2f, VUI.TextDim, TextAlignmentOptions.TopLeft, false, 0.62f);
            // Tabs.
            string[] tabs = { "PAINT", "LIVERY", "WHEELS", "AERO & TYRES", "BRAKES", "SUSPENSION" };
            for (int t = 0; t < tabs.Length; t++)
            {
                int ti = t;
                var b = Btn(tabs[t], -0.22f + t * 0.112f, H * 0.5f - 0.2f, 0.105f, 0.04f, () => { garageTab = ti; BuildGarage(); });
                b.selected = t == garageTab; b.Refresh();
            }
            float cx = -0.27f, y = H * 0.5f - 0.27f;
            switch (garageTab)
            {
                case 0:
                    Swatches("PRIMARY", cx, y, st.Primary(spec), c => { st.primary = CarSetup.Hex(c); Changed(); });
                    Swatches("SECONDARY", cx, y - 0.1f, st.Secondary(spec), c => { st.secondary = CarSetup.Hex(c); Changed(); });
                    Swatches("ACCENT", cx, y - 0.2f, st.Accent(spec), c => { st.accent = CarSetup.Hex(c); Changed(); });
                    break;
                case 1:
                    Lbl("PATTERN", cx, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSelector.Make(panel, new Vector2(cx + 0.38f, y), 0.36f, CarCatalog.LiveryNames, st.Livery(spec), v => { st.livery = v; Changed(); });
                    Lbl("RACE NUMBER", cx, y - 0.07f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSlider.Make(panel, new Vector2(cx + 0.35f, y - 0.07f), 0.3f, 0, 99, st.Number(spec), v => { st.number = Mathf.RoundToInt(v); }, v => Mathf.RoundToInt(v).ToString(), 1f);
                    Btn("APPLY NUMBER", cx + 0.38f, y - 0.14f, 0.2f, 0.045f, Changed);
                    Btn("RESET TO TEAM COLOURS", cx + 0.38f, y - 0.21f, 0.3f, 0.045f, () => { garageSetup = new CarSetup { compound = st.compound, wingLevel = st.wingLevel }; Changed(); BuildGarage(); });
                    break;
                case 2:
                    Lbl("WHEEL DESIGN", cx, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSelector.Make(panel, new Vector2(cx + 0.38f, y), 0.36f, CarCatalog.WheelNames, st.wheelStyle, v => { st.wheelStyle = v; Changed(); });
                    Lbl("RIM FINISH", cx, y - 0.07f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSelector.Make(panel, new Vector2(cx + 0.38f, y - 0.07f), 0.36f, CarCatalog.RimColorNames, st.rimColor, v => { st.rimColor = v; Changed(); });
                    break;
                case 3:
                    Lbl("WING LEVEL", cx, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSelector.Make(panel, new Vector2(cx + 0.38f, y), 0.36f, CarCatalog.WingNames, st.wingLevel, v => { st.wingLevel = v; Changed(); });
                    Lbl("TYRE COMPOUND", cx, y - 0.07f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSelector.Make(panel, new Vector2(cx + 0.38f, y - 0.07f), 0.36f, CarCatalog.CompoundNames, (int)st.compound, v => { st.compound = (TireCompound)v; Changed(); });
                    Lbl("Low wing: faster on the straights. High wing: more grip in the corners.\nSoft tyres grip most but wear fast. Inters and wets for rain.", cx, y - 0.16f, 0.19f, VUI.TextDim, TextAlignmentOptions.TopLeft, false, 0.62f);
                    break;
                case 4:
                    Lbl("BRAKE BIAS (FRONT)", cx, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSlider.Make(panel, new Vector2(cx + 0.35f, y), 0.3f, 0.5f, 0.66f, st.Bias(spec), v => st.brakeBias = v, v => (v * 100f).ToString("0.0") + "%", 0.005f);
                    Lbl("BRAKE PRESSURE", cx, y - 0.07f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSlider.Make(panel, new Vector2(cx + 0.35f, y - 0.07f), 0.3f, 0.8f, 1f, st.brakePressure, v => st.brakePressure = v, v => (v * 100f).ToString("0") + "%", 0.01f);
                    Lbl("DISCS", cx, y - 0.14f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                    VSelector.Make(panel, new Vector2(cx + 0.38f, y - 0.14f), 0.36f, new[] { "Carbon (needs heat)", "Steel (consistent)" }, (int)st.brakeType, v => st.brakeType = (BrakeType)v);
                    break;
                default:
                    SliderRow("SPRINGS", cx, y, st.springs, v => st.springs = v, "Soft", "Stiff");
                    SliderRow("RIDE HEIGHT", cx, y - 0.07f, st.rideHeight, v => st.rideHeight = v, "Low", "High");
                    SliderRow("ANTI-ROLL BARS", cx, y - 0.14f, st.antiRoll, v => st.antiRoll = v, "Soft", "Stiff");
                    SliderRow("DAMPERS", cx, y - 0.21f, st.dampers, v => st.dampers = v, "Soft", "Stiff");
                    break;
            }
            Back(() => { SaveData.StoreSetup(garageCar, garageSetup); ShowMain(); });
            Btn("SAVE SETUP", W * 0.5f - 0.15f, -H * 0.5f + 0.045f, 0.26f, 0.05f, () => { SaveData.StoreSetup(garageCar, garageSetup); CrashAudio.Chime(); }, true);
        }

        void SliderRow(string label, float x, float y, float value, System.Action<float> set, string lo, string hi)
        {
            Lbl(label, x, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            VSlider.Make(panel, new Vector2(x + 0.35f, y), 0.3f, 0f, 1f, value, set, v => v < 0.33f ? lo : v > 0.66f ? hi : "Medium", 0.05f);
        }

        void Bar(string label, float x, float y, float t)
        {
            Lbl(label, x, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.12f);
            VUI.Quad(panel, "bar", new Vector2(x + 0.15f, y), new Vector2(0.1f, 0.008f), new Color(1f, 1f, 1f, 0.1f), -0.001f, 0.004f);
            float f = Mathf.Clamp01(t);
            var fill = VUI.Quad(panel, "fill", new Vector2(x + 0.1f + 0.05f * f, y), new Vector2(0.1f * f + 0.001f, 0.008f), VUI.Accent, -0.002f, 0.004f);
        }

        void Swatches(string label, float x, float y, Color current, System.Action<Color> pick)
        {
            Lbl(label, x, y, 0.22f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
            var sw = CarCatalog.PaintSwatches;
            for (int i = 0; i < sw.Length; i++)
            {
                Color c = sw[i];
                var card = VCard.Make(panel, new Vector2(x + 0.02f + (i % 10) * 0.064f, y - 0.03f - (i / 10) * 0.032f), new Vector2(0.056f, 0.026f), c, null, () => pick(c));
                card.selected = ColorClose(c, current);
                card.Refresh();
            }
        }

        static bool ColorClose(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.03f;

        float lastRefresh;
        void Changed()
        {
            Refresh3D();
            CrashAudio.Tick();
        }

        void Refresh3D()
        {
            if (G.showroom != null) G.showroom.Show(garageCar, garageSetup);
        }

        // ------------------------------------------------------------------ settings

        void ShowSettings()
        {
            Begin("SETTINGS");
            string[] tabs = { "DRIVING", "ASSISTS", "VR & COMFORT", "GRAPHICS", "AUDIO" };
            for (int t = 0; t < tabs.Length; t++)
            {
                int ti = t;
                var b = Btn(tabs[t], -W * 0.5f + 0.12f + t * 0.17f, H * 0.5f - 0.12f, 0.16f, 0.045f, () => { settingsTab = ti; ShowSettings(); });
                b.selected = t == settingsTab; b.Refresh();
            }
            float lx = -W * 0.5f + 0.06f, cx = 0.1f, y = H * 0.5f - 0.2f, dy = 0.062f;
            void Row(string l) => Lbl(l, lx, y, 0.25f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.45f);
            switch (settingsTab)
            {
                case 0:
                    Row("STEERING SENSITIVITY"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0.5f, 1.6f, Settings.SteeringSensitivity, v => Settings.SteeringSensitivity = v, v => v.ToString("0.00"), 0.05f); y -= dy;
                    Row("STEERING LOCK (WHEEL ROTATION)"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Car default", "270 degrees", "360 degrees", "540 degrees", "720 degrees", "900 degrees" }, LockIndex(), v => Settings.WheelRotation = new[] { 0, 270, 360, 540, 720, 900 }[v]); y -= dy;
                    Row("STEERING CENTRE SOFTNESS"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.SteeringLinearity, v => Settings.SteeringLinearity = v, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("FORCE FEEDBACK (VIBRATION)"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.Haptics, v => Settings.Haptics = v, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("PEDALS"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Triggers (R throttle, L brake)", "Right thumbstick" }, (int)Settings.Pedals, v => Settings.Pedals = (PedalMode)v); y -= dy;
                    Row("SPEED UNITS"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "MPH", "KM/H" }, Settings.Kmh ? 1 : 0, v => Settings.Kmh = v == 1); y -= dy;
                    break;
                case 1:
                    Row("ABS"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.Abs, v => Settings.Abs = v); y -= dy;
                    Row("TRACTION CONTROL"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.TractionControl, v => Settings.TractionControl = v); y -= dy;
                    Row("AUTOMATIC GEARS"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.AutoGears, v => Settings.AutoGears = v); y -= dy;
                    Row("RACING LINE"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Off", "Braking zones", "Full line" }, (int)Settings.RacingLine, v => Settings.RacingLine = (RacingLineMode)v); y -= dy;
                    Row("DAMAGE"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Off", "Visual only", "Full" }, (int)Settings.Damage, v => Settings.Damage = (DamageMode)v); y -= dy;
                    Row("FUEL USE"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.FuelUse, v => Settings.FuelUse = v); y -= dy;
                    Row("TYRE WEAR"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.TyreWear, v => Settings.TyreWear = v); y -= dy;
                    break;
                case 2:
                    Row("PLAY SEATED"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.Seated, v => Settings.Seated = v); y -= dy;
                    Row("COMFORT VIGNETTE"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.Vignette, v => Settings.Vignette = v, v => v < 0.01f ? "Off" : (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("BIG CRASHES"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Stay in the cockpit", "Fade to outside view" }, (int)Settings.CrashCamera, v => Settings.CrashCamera = (CrashView)v); y -= dy;
                    Row("LEVEL HORIZON"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.HorizonLock, v => Settings.HorizonLock = v); y -= dy;
                    Row("SEAT HEIGHT"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, -0.15f, 0.15f, Settings.SeatHeight, v => { Settings.SeatHeight = v; Rig.I?.Recenter(); }, v => (v * 100f).ToString("+0;-0;0") + " cm", 0.01f); y -= dy;
                    Row("SEAT FORWARD / BACK"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, -0.15f, 0.15f, Settings.SeatForward, v => { Settings.SeatForward = v; Rig.I?.Recenter(); }, v => (v * 100f).ToString("+0;-0;0") + " cm", 0.01f); y -= dy;
                    Row("TIMING TOWER IN COCKPIT"); VToggle.Make(panel, new Vector2(cx - 0.12f, y), Settings.ShowTimingTower, v => Settings.ShowTimingTower = v); y -= dy;
                    Btn("RECENTER VIEW", cx + 0.03f, y, 0.3f, 0.045f, () => Rig.I?.Recenter());
                    break;
                case 3:
                    Row("QUALITY"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Quest (standalone)", "Balanced", "PC VR high" }, Settings.Quality, v => { Settings.Quality = v; Game.ApplyRenderScale(); }); y -= dy;
                    Row("RENDER RESOLUTION"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0.7f, 1.4f, Settings.RenderScale, v => { Settings.RenderScale = v; Game.ApplyRenderScale(); }, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("MIRRORS"); VSelector.Make(panel, new Vector2(cx + 0.03f, y), 0.4f, new[] { "Off", "Low", "High" }, Settings.Mirrors, v => Settings.Mirrors = v); y -= dy;
                    Lbl("Quality and mirrors apply from the next session.", lx, y, 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.8f);
                    break;
                default:
                    Row("MASTER"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.MasterVolume, v => Settings.MasterVolume = v, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("ENGINES"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.EngineVolume, v => Settings.EngineVolume = v, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("EFFECTS"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.EffectsVolume, v => Settings.EffectsVolume = v, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    Row("AMBIENCE"); VSlider.Make(panel, new Vector2(cx, y), 0.34f, 0f, 1f, Settings.AmbienceVolume, v => Settings.AmbienceVolume = v, v => (v * 100f).ToString("0") + "%", 0.05f); y -= dy;
                    break;
            }
            Back(() => { Settings.Save(); if (G.state == Game.State.Paused) ShowPause(); else ShowMain(); });
        }

        static int LockIndex()
        {
            int[] v = { 0, 270, 360, 540, 720, 900 };
            int i = System.Array.IndexOf(v, Settings.WheelRotation);
            return i < 0 ? 0 : i;
        }

        // ------------------------------------------------------------------ leaderboards

        void ShowLeaderboards()
        {
            Begin("LEADERBOARDS", "Your records on this headset");
            string[] tabs = { "TIME TRIAL", "RACE LAPS", "CHAMPIONSHIP", "CAREER" };
            for (int t = 0; t < tabs.Length; t++)
            {
                int ti = t;
                var b = Btn(tabs[t], -W * 0.5f + 0.13f + t * 0.2f, H * 0.5f - 0.13f, 0.19f, 0.045f, () => { boardTab = ti; ShowLeaderboards(); });
                b.selected = t == boardTab; b.Refresh();
            }
            float x0 = -W * 0.5f + 0.06f, y = H * 0.5f - 0.2f;
            if (boardTab <= 1)
            {
                var list = boardTab == 0 ? SaveData.D.timeTrial : SaveData.D.raceLaps;
                for (int i = 0; i < MapCatalog.Count; i++)
                {
                    var m = MapCatalog.Get(i);
                    float best = float.MaxValue; int car = -1;
                    foreach (var r in list) if (r.map == m.id && r.time < best) { best = r.time; car = r.car; }
                    int col = i / 13, row = i % 13;
                    float x = x0 + col * 0.54f, yy = y - row * 0.034f;
                    Lbl(m.name, x, yy, 0.19f, VUI.Text, TextAlignmentOptions.MidlineLeft, false, 0.3f);
                    Lbl(best < float.MaxValue ? SaveData.FormatTime(best) : "-", x + 0.32f, yy, 0.19f, best < float.MaxValue ? VUI.Purple : VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.1f);
                    if (car >= 0) Lbl(CarCatalog.Get(car).name, x + 0.41f, yy, 0.15f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.14f);
                }
            }
            else if (boardTab == 2)
            {
                var s = Championship.S;
                if (s.names.Count > 0)
                {
                    var order = Championship.Standings();
                    Lbl(s.active ? "CURRENT SEASON - after round " + s.round + " of " + s.calendar.Count : "LAST SEASON", x0, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.6f);
                    for (int i = 0; i < Mathf.Min(order.Count, 12); i++)
                    {
                        var kv = order[i];
                        bool me = kv.Key == s.playerIndex;
                        Lbl((i + 1) + ".  " + s.names[kv.Key], x0, y - 0.04f - i * 0.032f, 0.2f, me ? VUI.Accent : VUI.Text, TextAlignmentOptions.MidlineLeft, me, 0.3f);
                        Lbl(kv.Value + " pts", x0 + 0.33f, y - 0.04f - i * 0.032f, 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.12f);
                    }
                }
                else Lbl("No championship yet. Start one from Race > Championship.", x0, y, 0.22f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.8f);
                float hx = 0.1f;
                Lbl("HISTORY", hx, y, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.4f);
                var h = SaveData.D.championshipHistory;
                for (int i = 0; i < Mathf.Min(10, h.Count); i++) Lbl(h[h.Count - 1 - i], hx, y - 0.04f - i * 0.032f, 0.17f, VUI.Text, TextAlignmentOptions.MidlineLeft, false, 0.46f);
            }
            else
            {
                var d = SaveData.D;
                string txt = "RACES          " + d.races + "\nWINS           " + d.wins + "\nPODIUMS        " + d.podiums + "\nCHAMPIONSHIPS  " + d.championshipsWon + "\nDISTANCE       " + d.distanceKm.ToString("0") + " km";
                Lbl(txt, x0, y - 0.1f, 0.3f, VUI.Text, TextAlignmentOptions.TopLeft, true, 0.6f);
            }
            Back(ShowMain);
        }

        // ------------------------------------------------------------------ online

        public void ShowOnline()
        {
            Begin("ONLINE", Online.Available ? "Race other players. Up to 8 per room." : "Online play needs Photon Fusion 2 (see Veylix > Import Multiplayer in the Unity editor).");
            if (Online.I != null) Online.I.BuildMenu(panel, this);
            Back(ShowMain);
        }

        public void ShowOnlineStatus(string text)
        {
            if (!Pointer.MenuActive) return;
            ShowOnline();
            Lbl(text, -W * 0.5f + 0.05f, -H * 0.5f + 0.11f, 0.22f, VUI.Warn, TextAlignmentOptions.MidlineLeft, false, W - 0.1f);
        }

        // ------------------------------------------------------------------ loading

        TextMeshPro loadingStage, loadingTip, loadingPct;
        MeshRenderer loadingBar;
        Transform loadingDot;
        Vector3[] loadingPath;
        float loadingStart, tipAt;
        int tipIndex;
        float loadingBarW, loadingBarX;
        bool splash;

        static readonly string[] Tips =
        {
            "Squeeze the grip on the rim to hold the steering wheel. Use both hands for precise inputs.",
            "Brake in a straight line, then ease off the brake as you turn in.",
            "Cold tyres and brakes grip less. Build heat over the first lap.",
            "Hold B (or R) for a second to put the car back on track.",
            "Wet track? Inters or full wets in the Garage, and brake much earlier.",
            "A shifts up, X or Y shifts down. Or turn on automatic gears in Settings.",
            "Too fast into a corner and sideways over a kerb? The car can trip and roll.",
            "Gravel and grass grab the tyres. Run wide there and you can flip.",
            "A split fuel cell can catch fire. Get the car stopped and off the racing line.",
            "Low wing for the straights, high wing for the corners. Set it in CUSTOMISE.",
            "Watch your mirrors. The AI gives you room when you're alongside.",
            "Yellow flags mean slow down, no overtaking. The AI respects them too.",
            "Your garage: walk with the left stick, snap-turn with the right.",
        };

        /// <summary>Boot splash in a dark void while the garage is built.</summary>
        public void ShowSplash()
        {
            Begin(null, null, true, 1.0f, 0.5f);
            Pointer.MenuActive = false;
            splash = true;
            VUI.Clear(panel); // no panel card: just the logotype floating in the dark
            Place(true);
            var t = Lbl("VEYLIX", 0f, 0.07f, 2.2f, VUI.Text, TextAlignmentOptions.Midline, true, 1.0f);
            t.characterSpacing = 30f;
            Lbl("FORMULA RACING  -  VR", 0f, -0.03f, 0.3f, VUI.Accent, TextAlignmentOptions.Midline, true, 1.0f).characterSpacing = 10f;
            loadingBarW = 0.5f; loadingBarX = 0f;
            VUI.Quad(panel, "track", new Vector2(0f, -0.1f), new Vector2(loadingBarW, 0.004f), new Color(1f, 1f, 1f, 0.1f), -0.001f, 0.002f);
            loadingBar = VUI.Quad(panel, "bar", new Vector2(0f, -0.1f), new Vector2(loadingBarW, 0.004f), VUI.Accent, -0.002f, 0.002f);
            loadingStage = Lbl("", 0f, -0.13f, 0.2f, VUI.TextDim, TextAlignmentOptions.Midline, false, 1.0f);
            loadingPct = null; loadingTip = null; loadingDot = null; loadingPath = null;
            loadingStart = Time.unscaledTime;
        }

        public void SetSplashProgress(float p, string stage)
        {
            if (!splash || loadingBar == null) return;
            SetBar(p);
            if (loadingStage != null) loadingStage.text = stage.ToUpperInvariant();
        }

        void SetBar(float p)
        {
            p = Mathf.Clamp01(p);
            loadingBar.transform.localScale = new Vector3(Mathf.Max(0.01f, p), 1f, 1f);
            var lp = loadingBar.transform.localPosition;
            loadingBar.transform.localPosition = new Vector3(loadingBarX - loadingBarW * 0.5f * (1f - p), lp.y, lp.z);
        }

        /// <summary>
        /// Loading a map: the track drawn as a live outline with a marker lapping it, the session you're about to
        /// drive, a progress bar with what's being built, and a new tip every few seconds.
        /// </summary>
        public void ShowLoading(MapDef m)
        {
            splash = false;
            const float w = 1.2f, h = 0.62f;
            Begin(null, null, true, w, h);
            Pointer.MenuActive = false;
            // Left: the track outline.
            VUI.Quad(panel, "mapbg", new Vector2(-0.3f, 0.04f), new Vector2(0.54f, 0.44f), VUI.PanelLight, 0.001f, 0.012f);
            loadingPath = OutlinePath(m, new Vector2(-0.3f, 0.04f), new Vector2(0.46f, 0.36f));
            var lineGo = new GameObject("outline");
            lineGo.layer = Layers.UI;
            lineGo.transform.SetParent(panel, false);
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = !m.freeRoam;
            lr.positionCount = loadingPath.Length;
            lr.SetPositions(loadingPath);
            lr.startWidth = lr.endWidth = 0.006f;
            lr.numCornerVertices = 2;
            lr.sharedMaterial = new Material(Shader.Find("Veylix/UI") ?? Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.95f, 0.95f, 0.94f, 0.9f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            loadingDot = VUI.Quad(panel, "dot", Vector2.zero, new Vector2(0.018f, 0.018f), VUI.Accent, -0.006f, 0.009f).transform;
            if (loadingPath.Length > 0) { var s0 = loadingPath[0]; VUI.Quad(panel, "start", new Vector2(s0.x, s0.y), new Vector2(0.008f, 0.03f), VUI.Text, -0.005f, 0.002f); }
            Lbl(m.KindLabel + "   |   " + EstimateKm(m).ToString("0.0") + " KM", -0.55f, -0.215f, 0.2f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.5f).characterSpacing = 3f;

            // Right: what you're about to drive.
            float x = 0.02f;
            var cfg = G != null ? G.config : new SessionConfig();
            Lbl("LOADING", x, 0.26f, 0.22f, VUI.Accent, TextAlignmentOptions.MidlineLeft, true, 0.5f).characterSpacing = 8f;
            Lbl(m.name.ToUpperInvariant(), x, 0.2f, 0.6f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.56f);
            Lbl(m.location, x, 0.145f, 0.26f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.56f);
            Lbl(m.blurb, x, 0.1f, 0.2f, VUI.TextDim, TextAlignmentOptions.TopLeft, false, 0.55f);
            float hour = cfg.Hour(m);
            string rows =
                "MODE        " + ModeName(cfg.mode).ToUpperInvariant() + "\n" +
                "CAR         " + CarCatalog.Get(cfg.car).name.ToUpperInvariant() + "\n" +
                (m.freeRoam || cfg.mode == GameMode.TimeTrial || cfg.mode == GameMode.Practice ? "" : "LAPS        " + (cfg.laps > 0 ? cfg.laps : m.laps) + "\n") +
                "TIME        " + Mathf.FloorToInt(hour).ToString("00") + ":" + Mathf.RoundToInt(Mathf.Repeat(hour, 1f) * 60f).ToString("00") + "\n" +
                "WEATHER     " + Sky.WeatherName(cfg.Weather(m)).ToUpperInvariant();
            Lbl(rows, x, -0.03f, 0.22f, VUI.Text, TextAlignmentOptions.TopLeft, true, 0.56f);

            // Bottom: progress and tips.
            loadingBarW = w - 0.1f; loadingBarX = 0f;
            VUI.Quad(panel, "track", new Vector2(0f, -0.255f), new Vector2(loadingBarW, 0.006f), new Color(1f, 1f, 1f, 0.1f), -0.001f, 0.003f);
            loadingBar = VUI.Quad(panel, "bar", new Vector2(0f, -0.255f), new Vector2(loadingBarW, 0.006f), VUI.Accent, -0.002f, 0.003f);
            loadingStage = Lbl("", -w * 0.5f + 0.05f, -0.233f, 0.19f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.6f);
            loadingPct = Lbl("0%", w * 0.5f - 0.05f, -0.233f, 0.19f, VUI.Text, TextAlignmentOptions.MidlineRight, true, 0.2f);
            loadingTip = Lbl("", -w * 0.5f + 0.05f, -0.285f, 0.18f, VUI.Text, TextAlignmentOptions.MidlineLeft, false, w - 0.1f);
            tipIndex = Random.Range(0, Tips.Length);
            tipAt = 0f;
            loadingStart = Time.unscaledTime;
            SetBar(0f);
        }

        static float EstimateKm(MapDef m) => EstimateLength(m) / 1000f;

        /// The map's control points squeezed into a box on the panel (panel-local coordinates).
        static Vector3[] OutlinePath(MapDef m, Vector2 centre, Vector2 box)
        {
            int n = m.points.Length / 3;
            if (n < 2) return new Vector3[0];
            var cps = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < n; i++) cps.Add(new Vector3(m.points[i * 3], 0f, m.points[i * 3 + 1]));
            var spline = TrackSpline.FromControlPoints(cps, !m.freeRoam, 1f);
            int count = Mathf.Clamp(spline.Count, 2, 400);
            var pts = new Vector3[count];
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            for (int i = 0; i < count; i++)
            {
                var p = spline.pos[i * spline.Count / count];
                min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            Vector2 size = max - min;
            float k = Mathf.Min(box.x / Mathf.Max(1e-3f, size.x), box.y / Mathf.Max(1e-3f, size.y));
            Vector2 mid = (min + max) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var p = spline.pos[i * spline.Count / count];
                pts[i] = new Vector3(centre.x + (p.x - mid.x) * k, centre.y + (p.z - mid.y) * k, -0.004f);
            }
            return pts;
        }

        void Update()
        {
            if (loadingBar == null) return;
            if (!splash && World.Current != null && G != null && G.state == Game.State.Loading)
            {
                float p = World.Current.Progress;
                SetBar(p);
                if (loadingStage != null) loadingStage.text = World.Current.Stage.ToUpperInvariant();
                if (loadingPct != null) loadingPct.text = Mathf.RoundToInt(p * 100f) + "%";
            }
            if (loadingDot != null && loadingPath != null && loadingPath.Length > 1)
            {
                // A marker lapping the outline, about eight seconds a lap.
                float f = Mathf.Repeat((Time.unscaledTime - loadingStart) / 8f, 1f) * (loadingPath.Length - 1);
                int i = Mathf.FloorToInt(f);
                var a = loadingPath[i]; var b = loadingPath[Mathf.Min(i + 1, loadingPath.Length - 1)];
                var q = Vector3.Lerp(a, b, f - i);
                loadingDot.localPosition = new Vector3(q.x, q.y, -0.006f);
            }
            if (loadingTip != null && Time.unscaledTime >= tipAt)
            {
                loadingTip.text = "TIP:  " + Tips[tipIndex % Tips.Length];
                tipIndex++;
                tipAt = Time.unscaledTime + 6f;
            }
        }

        // ------------------------------------------------------------------ in session

        public void ShowPause()
        {
            var s = G.session;
            Begin("PAUSED", s != null ? ModeName(s.config.mode) + "  |  " + s.map.name : null, true, 0.8f, 0.6f);
            float x = -0.19f, y = 0.16f, dy = 0.068f;
            Btn("RESUME", x, y, 0.34f, 0.058f, () => G.Pause(false), true); y -= dy;
            Btn("RESTART", x, y, 0.34f, 0.058f, () => G.Restart()); y -= dy;
            Btn("RESET CAR TO TRACK", x, y, 0.34f, 0.058f, () => { G.Pause(false); G.ResetCar(); }); y -= dy;
            Btn("RECENTER VIEW", x, y, 0.34f, 0.058f, () => Rig.I?.Recenter()); y -= dy;
            Btn(Rig.I != null && Rig.I.view == Rig.View.Chase ? "COCKPIT VIEW" : "CHASE VIEW", x, y, 0.34f, 0.058f, () => { Rig.I?.ToggleChase(); ShowPause(); }); y -= dy;
            Btn("SETTINGS", x, y, 0.34f, 0.058f, ShowSettings); y -= dy;
            Btn("EXIT TO MENU", x, y, 0.34f, 0.058f, () => { if (Online.I != null) Online.I.Leave(); G.EnterMenu(); });
            // Right: free-roam controls for time and weather, or race status.
            float rx = 0.2f;
            var sky = Sky.I;
            if (sky != null)
            {
                Lbl("TIME  " + sky.TimeLabel, rx - 0.15f, 0.18f, 0.26f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                VSlider.Make(panel, new Vector2(rx + 0.02f, 0.13f), 0.3f, 0f, 23.75f, sky.hour, v => { sky.hour = v; }, v => Mathf.FloorToInt(v).ToString("00") + ":" + Mathf.RoundToInt(Mathf.Repeat(v, 1f) * 60f).ToString("00"), 0.25f);
                Lbl("WEATHER", rx - 0.15f, 0.07f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                VSelector.Make(panel, new Vector2(rx + 0.04f, 0.03f), 0.32f, new[] { "Clear", "Cloudy", "Overcast", "Light rain", "Heavy rain", "Fog" }, (int)sky.weather, v => { sky.weather = (WeatherKind)v; sky.Configure(G.session != null ? G.session.map : MapCatalog.Get(0), sky.hour, (WeatherKind)v, sky.timeScale > 0f, sky.dynamicWeather); });
                Lbl("TIME PASSES", rx - 0.15f, -0.03f, 0.24f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.3f);
                VToggle.Make(panel, new Vector2(rx + 0.12f, -0.03f), sky.timeScale > 0f, v => sky.timeScale = v ? 24f : 0f);
            }
            if (s != null && s.player != null && s.Timed)
                Lbl("BEST LAP  " + SaveData.FormatTime(s.player.bestLap) + "\nLAST LAP  " + SaveData.FormatTime(s.player.lastLap), rx - 0.15f, -0.12f, 0.22f, VUI.Text, TextAlignmentOptions.TopLeft, true, 0.4f);
        }

        public void ShowCrash()
        {
            Begin("HEAVY CRASH", "The car is too damaged to continue.", true, 0.7f, 0.36f);
            Btn("CONTINUE (BACK ON TRACK)", -0.15f, 0.02f, 0.34f, 0.065f, () => G.ResetCar(), true);
            Btn("RESTART", 0.17f, 0.02f, 0.24f, 0.065f, () => G.Restart());
            Btn("EXIT TO MENU", 0f, -0.08f, 0.3f, 0.05f, () => { if (Online.I != null) Online.I.Leave(); G.EnterMenu(); });
        }

        public void ShowResults(RaceSession s)
        {
            if (s == null) return;
            bool race = s.IsRace;
            Begin(race ? "RESULTS" : "SESSION COMPLETE", s.map.name + "  |  " + ModeName(s.config.mode), true, 1.0f, 0.66f);
            float x0 = -0.46f, y = 0.2f;
            Lbl("POS", x0, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.05f);
            Lbl("DRIVER", x0 + 0.06f, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.2f);
            Lbl("TEAM", x0 + 0.3f, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.2f);
            Lbl("TIME", x0 + 0.53f, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.14f);
            Lbl("BEST LAP", x0 + 0.68f, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.12f);
            Lbl("PEN", x0 + 0.82f, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.06f);
            Lbl("PTS", x0 + 0.88f, y, 0.18f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, true, 0.06f);
            var leader = s.entrants.Count > 0 ? s.entrants[0] : null;
            for (int i = 0; i < Mathf.Min(s.entrants.Count, 14); i++)
            {
                var e = s.entrants[i];
                float yy = y - 0.035f - i * 0.03f;
                Color c = e.isPlayer ? VUI.Accent : VUI.Text;
                string time = e.dnf ? "DNF" : e.eliminated ? "OUT (lap " + e.lap + ")" : !e.finished ? (race ? "+" + Mathf.Max(0, (leader != null ? leader.lap : 0) - e.lap) + " LAP" : "-") : i == 0 ? SaveData.FormatTime(e.RaceTime) : "+" + (e.RaceTime - leader.RaceTime).ToString("0.000");
                if (!race) time = SaveData.FormatTime(e.bestLap);
                Lbl((i + 1).ToString(), x0, yy, 0.2f, c, TextAlignmentOptions.MidlineLeft, true, 0.05f);
                Lbl(e.name, x0 + 0.06f, yy, 0.2f, c, TextAlignmentOptions.MidlineLeft, e.isPlayer, 0.24f);
                Lbl(e.team ?? "", x0 + 0.3f, yy, 0.17f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false, 0.22f);
                Lbl(time, x0 + 0.53f, yy, 0.2f, c, TextAlignmentOptions.MidlineLeft, true, 0.15f);
                bool fl = s.fastestLapBy == e.name && race;
                Lbl(SaveData.FormatTime(e.bestLap), x0 + 0.68f, yy, 0.2f, fl ? VUI.Purple : VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.13f);
                Lbl(e.penalty > 0f ? "+" + e.penalty.ToString("0") + "s" : "", x0 + 0.82f, yy, 0.18f, VUI.Warn, TextAlignmentOptions.MidlineLeft, true, 0.06f);
                int pts = race && i < RaceSession.Points.Length && !e.eliminated && !e.dnf ? RaceSession.Points[i] + (fl ? 1 : 0) : 0;
                Lbl(pts > 0 ? pts.ToString() : "", x0 + 0.88f, yy, 0.2f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.06f);
            }
            if (s.player != null)
            {
                string pb = s.newPersonalBest ? "  -  NEW PERSONAL BEST" : "";
                Lbl("YOU: P" + s.player.position + "   FASTEST LAP " + SaveData.FormatTime(s.player.bestLap) + "   RACE TIME " + (s.player.finished ? SaveData.FormatTime(s.player.RaceTime) : "-") + "   PENALTIES " + s.player.penalty.ToString("0") + "s" + pb, x0, -0.24f, 0.2f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.95f);
            }
            float by = -0.3f;
            if (s.config.mode == GameMode.Championship)
                Btn(Championship.Finished ? "FINAL STANDINGS" : "STANDINGS & NEXT ROUND", 0.27f, by, 0.38f, 0.055f, () => ShowStandings(Championship.Finished), true);
            else
                Btn("RACE AGAIN", 0.3f, by, 0.3f, 0.055f, () => G.Restart(), true);
            Btn("EXIT TO MENU", -0.05f, by, 0.3f, 0.055f, () => { if (Online.I != null) Online.I.Leave(); G.EnterMenu(); });
            Btn("KEEP DRIVING", -0.35f, by, 0.22f, 0.055f, () => { Hide(); G.state = Game.State.Session; });
        }

        public void ShowStandings(bool final)
        {
            var s = Championship.S;
            Begin(final ? "FINAL STANDINGS" : "CHAMPIONSHIP STANDINGS", "After round " + s.round + " of " + s.calendar.Count, true, 0.9f, 0.62f);
            var order = Championship.Standings();
            float x0 = -0.4f, y = 0.19f;
            for (int i = 0; i < Mathf.Min(order.Count, 13); i++)
            {
                var kv = order[i];
                bool me = kv.Key == s.playerIndex;
                Lbl((i + 1) + ".  " + s.names[kv.Key], x0, y - i * 0.032f, 0.21f, me ? VUI.Accent : VUI.Text, TextAlignmentOptions.MidlineLeft, me, 0.4f);
                Lbl(kv.Value + " pts", x0 + 0.42f, y - i * 0.032f, 0.21f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.14f);
            }
            if (final)
            {
                bool won = order.Count > 0 && order[0].Key == s.playerIndex;
                Lbl(won ? "WORLD CHAMPION" : "SEASON COMPLETE", 0.1f, 0.2f, 0.45f, won ? VUI.Accent : VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.35f);
                Btn("FINISH SEASON", 0.25f, -0.25f, 0.3f, 0.055f, () => { Championship.End(); G.EnterMenu(); }, true);
            }
            else
            {
                var next = MapCatalog.Get(Championship.CurrentMap);
                Lbl("NEXT:  " + next.name.ToUpperInvariant(), 0.1f, 0.2f, 0.26f, VUI.Text, TextAlignmentOptions.MidlineLeft, true, 0.35f);
                Btn("NEXT ROUND", 0.25f, -0.25f, 0.3f, 0.055f, () => G.NextChampionshipRound(), true);
                Btn("EXIT TO MENU", -0.1f, -0.25f, 0.3f, 0.055f, () => G.EnterMenu());
            }
        }
    }
}
