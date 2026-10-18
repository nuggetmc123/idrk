using System.Text;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The little screen mounted on your kart, just above the steering wheel: big MPH number, plus one line for
    /// lap / position / boost / messages. The race countdown still shows big in front of you.
    /// Only shown on your own kart. (The old floating speed text is switched off.)
    /// </summary>
    public class KartDashboard : MonoBehaviour
    {
        public TextMeshPro display;
        public TextMeshPro bigDisplay;

        KartController kart;
        float nextRefresh;
        readonly StringBuilder sb = new StringBuilder();
        Transform screen;
        TextMeshPro mph, info;

        void Awake()
        {
            kart = GetComponent<KartController>();
            if (display == null)
            {
                var t = transform.Find("Dashboard");
                if (t) display = t.GetComponent<TextMeshPro>();
            }
            if (bigDisplay == null)
            {
                var t = transform.Find("BigDisplay");
                if (t) bigDisplay = t.GetComponent<TextMeshPro>();
            }
        }

        /// <summary>A real screen on the kart: dark panel with a glowing edge, built in code (no world rebuild needed).</summary>
        void BuildScreen()
        {
            screen = new GameObject("SpeedScreen").transform;
            screen.SetParent(transform, false);
            screen.localPosition = new Vector3(0f, 0.93f, 0.5f);
            screen.localRotation = Quaternion.Euler(28f, 0f, 0f); // tilted back towards your eyes
            KartUI.Box(screen, "Panel", new Vector3(0f, 0f, 0.012f), new Vector3(0.36f, 0.2f, 0.02f), new Color(0.04f, 0.05f, 0.07f));
            KartUI.Part(screen, PrimitiveType.Cube, new Vector3(0f, 0.102f, 0.01f), new Vector3(0.37f, 0.008f, 0.025f), KartUI.Glow(new Color(0.3f, 0.9f, 1f)));
            KartUI.Part(screen, PrimitiveType.Cube, new Vector3(0f, -0.08f, 0.012f), new Vector3(0.024f, 0.16f, 0.02f), KartUI.Carbon(), new Vector3(0f, 0f, 0f), "Stand");
            mph = KartUI.Label(screen, "0", new Vector3(-0.03f, 0.022f, 0f), 1.1f, Color.white, new Vector2(0.24f, 0.12f), TextAlignmentOptions.Right);
            KartUI.Label(screen, "MPH", new Vector3(0.13f, 0.005f, 0f), 0.28f, new Color(0.3f, 0.9f, 1f), new Vector2(0.09f, 0.05f), TextAlignmentOptions.Left);
            info = KartUI.Label(screen, "", new Vector3(0f, -0.065f, 0f), 0.13f, new Color(0.85f, 0.95f, 1f), new Vector2(0.34f, 0.06f));
            info.enableWordWrapping = true;
        }

        void LateUpdate()
        {
            if (kart == null || !kart.IsReady) return;
            bool show = kart.IsLocal && !kart.Parked;
            if (display) display.gameObject.SetActive(false); // old floating speed text: removed
            if (show && screen == null) BuildScreen();
            if (screen) screen.gameObject.SetActive(show);
            if (bigDisplay) bigDisplay.gameObject.SetActive(show);
            if (!show || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.1f;

            sb.Clear();
            string big = "";
            var sessions = SessionManager.Instance;

            if (KartModeInfo.IsRace(kart.CurrentMode)) big = RaceText();
            else if (KartModeInfo.IsBumper(kart.CurrentMode)) big = BumperText();
            else if (KartModeInfo.IsTag(kart.CurrentMode)) big = TagText();
            else if (kart.CurrentMode == KartMode.Tutorial)
                sb.Append(BuildMode.Active ? "BUILD MODE\n" : "TUTORIAL PARK\n");
            else if (KartModeInfo.IsFreeRoam(kart.CurrentMode))
                sb.Append(BuildMode.Active ? "BUILD MODE\n" : "FREE ROAM   (right stick click = build)\n");

            mph.text = "<b>" + Mathf.RoundToInt(kart.Speed * 2.23694f) + "</b>";
            if (kart.Look.booster != KartBooster.None)
            {
                // Boost meter: filled blocks show charge; READY means press A.
                int blocks = Mathf.RoundToInt(kart.BoostCharge * 8f);
                sb.Append("   <color=#FF9A2E>").Append(new string('|', blocks)).Append("</color>")
                  .Append("<color=#555555>").Append(new string('|', 8 - blocks)).Append("</color>");
                if (kart.Boosting) sb.Append(" BOOST!");
                else if (kart.BoostCharge >= 1f) sb.Append(" READY (A)");
            }
            sb.Append('\n');
            if (sessions != null)
            {
                string transient = sessions.TransientStatus;
                if (!string.IsNullOrEmpty(transient)) sb.Append("<color=#FFD54A>").Append(transient).Append("</color>\n");
                else if (sessions.IsPrivate) sb.Append("Lobby code: ").Append(KartUI.FormatCode(sessions.LobbyCode)).Append('\n');
                else if (!sessions.IsSolo && !sessions.IsTutorial && !string.IsNullOrEmpty(sessions.RoomCode))
                    sb.Append("Room code: ").Append(KartUI.FormatCode(sessions.RoomCode)).Append('\n');
            }

            // Small text under the speed: lap / position / boost / messages (two lines at most).
            string line = sb.ToString().Trim('\n', ' ').Replace("\n", "   ");
            info.text = line.Length > 110 ? line.Substring(0, 110) + "..." : line;
            if (bigDisplay) bigDisplay.text = big;
        }

        string RaceText()
        {
            var race = RaceManager.Instance;
            if (race == null || !race.IsReady) return "";
            switch (race.State)
            {
                case RaceState.Countdown:
                    sb.Append("GET READY - ").Append(race.totalLaps).Append(" laps\n");
                    return Mathf.CeilToInt(race.PhaseRemaining).ToString();
                case RaceState.Racing:
                    int pos = race.PositionOf(kart);
                    if (kart.Finished)
                    {
                        sb.Append("FINISHED ").Append(KartUI.FormatTime(kart.FinishTime)).Append('\n');
                        return "P" + pos;
                    }
                    sb.Append("LAP ").Append(Mathf.Max(1, kart.Lap)).Append('/').Append(race.totalLaps)
                        .Append("   P").Append(pos).Append('/').Append(KartController.All.Count)
                        .Append("   ").Append(KartUI.FormatTime(race.Elapsed)).Append('\n');
                    return race.Elapsed < 1.5f ? "GO!" : "";
                case RaceState.Results:
                    sb.Append("RESULTS (next race in ").Append(Mathf.CeilToInt(race.PhaseRemaining)).Append(")\n")
                        .Append(race.StandingsText(5));
                    return "";
            }
            return "";
        }

        string TagText()
        {
            var tag = TagManager.Instance;
            if (tag == null || !tag.IsReady) return "";
            if (tag.State == TagState.Results)
            {
                sb.Append("ROUND OVER (next in ").Append(Mathf.CeilToInt(tag.PhaseRemaining)).Append(")\n").Append(tag.StandingsText(5));
                return "";
            }
            var it = tag.ItKart;
            sb.Append("TAG  ").Append(KartUI.FormatTime(tag.PhaseRemaining)).Append("   IT: ").Append(it != null ? it.DisplayName : "?")
              .Append("   Score ").Append(kart.Score).Append('\n');
            return tag.IsIt(kart) ? "<color=#ff4433>YOU'RE IT!</color>" : "";
        }

        string BumperText()
        {
            var bumper = BumperManager.Instance;
            if (bumper == null || !bumper.IsReady) return "";
            if (bumper.State == BumperState.Results)
            {
                sb.Append("ROUND OVER (next in ").Append(Mathf.CeilToInt(bumper.PhaseRemaining)).Append(")\n")
                    .Append(bumper.StandingsText(5));
                return "";
            }
            sb.Append("BUMPER  ").Append(KartUI.FormatTime(bumper.PhaseRemaining)).Append('\n');
            return kart.Score.ToString();
        }
    }
}
