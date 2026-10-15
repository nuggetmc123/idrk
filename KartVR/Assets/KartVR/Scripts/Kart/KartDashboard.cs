using System.Text;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The screen on your kart's dashboard: speed, lap, position, countdown,
    /// scores, results and connection messages. Only shown on your own kart.
    /// </summary>
    public class KartDashboard : MonoBehaviour
    {
        public TextMeshPro display;
        public TextMeshPro bigDisplay;

        KartController kart;
        float nextRefresh;
        readonly StringBuilder sb = new StringBuilder();

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

        void LateUpdate()
        {
            if (kart == null || !kart.IsReady) return;
            bool show = kart.IsLocal;
            if (display) display.gameObject.SetActive(show);
            if (bigDisplay) bigDisplay.gameObject.SetActive(show);
            if (!show || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.1f;

            sb.Clear();
            string big = "";
            var sessions = SessionManager.Instance;

            switch (kart.CurrentMode)
            {
                case KartMode.Race:
                    big = RaceText();
                    break;
                case KartMode.Bumper:
                    big = BumperText();
                    break;
                case KartMode.FreeRoam:
                    sb.Append("FREE ROAM\n");
                    break;
                default:
                    sb.Append("HUB - drive into a portal\n");
                    break;
            }

            sb.Append(Mathf.RoundToInt(kart.Speed * 3.6f)).Append(" km/h\n");
            if (sessions != null)
            {
                string transient = sessions.TransientStatus;
                if (!string.IsNullOrEmpty(transient)) sb.Append("<color=#FFD54A>").Append(transient).Append("</color>\n");
                else if (sessions.IsPrivate) sb.Append("Lobby code: ").Append(KartUI.FormatCode(sessions.LobbyCode)).Append('\n');
            }

            if (display) display.text = sb.ToString();
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
