using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace Nullbound
{
    /// <summary>The big KO board in the arena: everyone's knockouts, times knocked out and streaks.</summary>
    public class ScoreBoard : MonoBehaviour
    {
        public TextMeshPro text;
        float next;

        void Update()
        {
            if (text == null || Time.time < next) return;
            next = Time.time + 0.5f;
            var sb = new StringBuilder("<b><color=#ffcf4a>KO BOARD</color></b>\n");
            var s = SessionManager.Instance;
            if (s != null) sb.Append("<size=60%>").Append(s.DescribeRoom()).Append("</size>\n");
            int i = 0;
            foreach (var f in Fighter.All.Where(f => f.IsReady).OrderByDescending(f => f.KOs).ThenBy(f => f.Downs).Take(8))
            {
                i++;
                string name = (f.IsBot ? "<color=#ffcc66>BOT</color> " : "") + f.DisplayName;
                if (f.IsMine && !f.IsBot) name = "<color=#7CFF8A>" + name + " (you)</color>";
                sb.Append(i).Append(". ").Append(name).Append("  <size=70%>").Append(FighterInfo.Get(f.Kind).name).Append("</size>   <b>")
                  .Append(f.KOs).Append("</b> KO   ").Append(f.Downs).Append(" down").Append(f.Streak >= 3 ? "   <color=#ff7a3d>" + f.Streak + " streak</color>" : "").Append('\n');
            }
            if (i == 0) sb.Append("Waiting for fighters...");
            text.text = sb.ToString();
        }
    }
}
