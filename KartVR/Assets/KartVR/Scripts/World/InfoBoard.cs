using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>A big sign that shows lobby info, race standings or bumper scores.</summary>
    public class InfoBoard : MonoBehaviour
    {
        public enum Kind { Lobby, Race, Bumper, Tag, Daily }

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
                    if (race != null && race.IsReady && KartModeInfo.IsRace(GameState.CurrentMode))
                    {
                        string header = race.State == RaceState.Countdown ? "Starting in " + Mathf.CeilToInt(race.PhaseRemaining)
                            : race.State == RaceState.Racing ? "Race time " + KartUI.FormatTime(race.Elapsed)
                            : race.State == RaceState.Results ? "Results" : "";
                        body = header + "\n" + race.StandingsText();
                    }
                    break;
                case Kind.Bumper:
                    var bumper = BumperManager.Instance;
                    if (bumper != null && bumper.IsReady && KartModeInfo.IsBumper(GameState.CurrentMode))
                    {
                        string header = bumper.State == BumperState.Playing ? "Time left " + KartUI.FormatTime(bumper.PhaseRemaining) : "Round over";
                        body = header + "\n" + bumper.StandingsText();
                    }
                    break;
                case Kind.Daily:
                    var sb = new System.Text.StringBuilder("<b><color=#ffd54a>TODAY'S CHALLENGES</color></b>   <size=70%>(new ones every day, Level " + Progress.Level +
                        (Progress.Streak > 1 ? ", " + Progress.Streak + " day streak" : "") + ")</size>\n");
                    for (int i = 0; i < 3; i++)
                    {
                        string c = Progress.ChallengeText(i, out float p, out float target, out bool done, out int xp);
                        sb.Append(done ? "<color=#7CFF8A>DONE: " + c + "</color>" : c + " <size=70%>(" + Mathf.FloorToInt(Mathf.Min(p, target)) + "/" + target + ")</size>")
                          .Append(" <size=70%><color=#ffd54a>+" + xp + " XP</color></size>").Append(i < 2 ? "      " : "");
                    }
                    body = sb.ToString();
                    break;
                case Kind.Tag:
                    var tag = TagManager.Instance;
                    if (tag != null && tag.IsReady && KartModeInfo.IsTag(GameState.CurrentMode))
                    {
                        var it = tag.ItKart;
                        string header = tag.State == TagState.Playing ? "Time left " + KartUI.FormatTime(tag.PhaseRemaining) + "\nIT: " + (it != null ? it.DisplayName : "?")
                            : "Round over";
                        body = header + "\n" + tag.StandingsText();
                    }
                    break;
            }
            text.text = string.IsNullOrEmpty(title) ? body : "<b>" + title + "</b>\n" + body;
        }
    }
}
