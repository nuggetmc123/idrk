using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>A big sign that shows lobby info, race standings or bumper scores.</summary>
    public class InfoBoard : MonoBehaviour
    {
        public enum Kind { Lobby, Race, Bumper }

        public Kind kind;
        public TextMeshPro text;
        public string title = "";

        float nextRefresh;

        void Awake()
        {
            if (text == null) text = GetComponentInChildren<TextMeshPro>();
        }

        void Update()
        {
            if (text == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.3f;

            string body = "";
            switch (kind)
            {
                case Kind.Lobby:
                    var s = SessionManager.Instance;
                    if (s != null)
                    {
                        body = s.DescribeLobby();
                        if (!s.IsPrivate) body += "\n\nWant to play with friends?\nUse the keypad: CREATE a lobby and share the code.";
                        else body += "\n\nPortals take the whole lobby together.";
                    }
                    break;
                case Kind.Race:
                    var race = RaceManager.Instance;
                    if (race != null && race.IsReady && GameState.CurrentMode == KartMode.Race)
                    {
                        string header = race.State == RaceState.Countdown ? "Starting in " + Mathf.CeilToInt(race.PhaseRemaining)
                            : race.State == RaceState.Racing ? "Race time " + KartUI.FormatTime(race.Elapsed)
                            : race.State == RaceState.Results ? "Results" : "";
                        body = header + "\n" + race.StandingsText();
                    }
                    break;
                case Kind.Bumper:
                    var bumper = BumperManager.Instance;
                    if (bumper != null && bumper.IsReady && GameState.CurrentMode == KartMode.Bumper)
                    {
                        string header = bumper.State == BumperState.Playing ? "Time left " + KartUI.FormatTime(bumper.PhaseRemaining) : "Round over";
                        body = header + "\n" + bumper.StandingsText();
                    }
                    break;
            }
            text.text = string.IsNullOrEmpty(title) ? body : "<b>" + title + "</b>\n" + body;
        }
    }
}
