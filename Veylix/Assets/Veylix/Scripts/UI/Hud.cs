using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The heads-up information, placed in the cockpit as small floating panels so it's comfortable in VR: lap
    /// counter, current / best / last lap, live delta, race clock and position on the left; the timing tower
    /// (everyone's position and gap) on the right; messages, flags and the start lights above the wheel. Practice
    /// mode (or the Racing Line assist) draws the racing line on the track, coloured where to brake.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        RaceSession s;
        Car car;
        Transform left, right, top;
        TextMeshPro lapT, timeT, bestT, lastT, deltaT, posT, clockT, msgT, flagT, towerT, lightsT, speedT;
        MeshRenderer flagBg;
        GameObject lineGo;
        float refresh;
        bool visible = true;

        public void Show(RaceSession session, Car c)
        {
            Hide();
            s = session; car = c;
            gameObject.SetActive(true);
            var eye = c.parts != null ? c.parts.eyePoint : c.transform;
            left = Panel("Left", c.transform, eye.localPosition + new Vector3(-0.33f, -0.16f, 0.62f), new Vector2(0.2f, 0.17f), -18f);
            right = Panel("Right", c.transform, eye.localPosition + new Vector3(0.36f, -0.12f, 0.6f), new Vector2(0.2f, 0.3f), 20f);
            top = Panel("Top", c.transform, eye.localPosition + new Vector3(0f, 0.035f, 0.85f), new Vector2(0.34f, 0.05f), 0f, false);
            float lx = -0.09f;
            posT = Txt(left, "P1/12", new Vector2(lx, 0.06f), 0.42f, VUI.Text, true);
            lapT = Txt(left, "LAP 1/5", new Vector2(lx + 0.1f, 0.06f), 0.24f, VUI.TextDim, true);
            timeT = Txt(left, "0:00.000", new Vector2(lx, 0.022f), 0.36f, VUI.Text, true);
            deltaT = Txt(left, "", new Vector2(lx + 0.11f, 0.022f), 0.24f, VUI.Good, true);
            bestT = Txt(left, "BEST  --:--.---", new Vector2(lx, -0.015f), 0.2f, VUI.Purple, true);
            lastT = Txt(left, "LAST  --:--.---", new Vector2(lx, -0.042f), 0.2f, VUI.TextDim, true);
            clockT = Txt(left, "", new Vector2(lx, -0.069f), 0.18f, VUI.TextDim, false);
            towerT = Txt(right, "", new Vector2(-0.09f, 0.135f), 0.16f, VUI.Text, true);
            towerT.alignment = TextAlignmentOptions.TopLeft;
            towerT.lineSpacing = -8f;
            flagBg = VUI.Quad(top, "flag", Vector2.zero, new Vector2(0.34f, 0.05f), new Color(0f, 0f, 0f, 0f), 0.001f, 0.01f);
            msgT = Txt(top, "", Vector2.zero, 0.26f, VUI.Text, true);
            msgT.alignment = TextAlignmentOptions.Midline;
            lightsT = Txt(top, "", new Vector2(0f, 0.045f), 0.5f, new Color(1f, 0.1f, 0.05f), true);
            lightsT.alignment = TextAlignmentOptions.Midline;
            speedT = Txt(left, "", new Vector2(lx + 0.1f, -0.069f), 0.18f, VUI.TextDim, false);
            if (!s.Timed) { lapT.text = ""; posT.text = ""; }
            right.gameObject.SetActive(s.HasOpponents && Settings.ShowTimingTower);
            if (s.config.mode == GameMode.Practice || Settings.RacingLine != RacingLineMode.Off) BuildLine(s.config.mode == GameMode.Practice ? RacingLineMode.Full : Settings.RacingLine);
        }

        Transform Panel(string name, Transform parent, Vector3 localPos, Vector2 size, float yaw, bool bg = true)
        {
            var t = new GameObject("Hud" + name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(-8f, yaw, 0f);
            if (bg) VUI.Quad(t, "bg", Vector2.zero, size, new Color(0.04f, 0.045f, 0.05f, 0.72f), 0.001f, 0.01f);
            Layers.SetRecursive(t.gameObject, Layers.Scenery);
            return t;
        }

        TextMeshPro Txt(Transform p, string text, Vector2 pos, float size, Color c, bool display)
        {
            var t = VUI.Label(p, text, pos, size, c, TextAlignmentOptions.MidlineLeft, display, 0f);
            t.gameObject.layer = Layers.Scenery;
            return t;
        }

        public void Hide()
        {
            foreach (var t in new[] { left, right, top }) if (t != null) Destroy(t.gameObject);
            left = right = top = null;
            if (lineGo != null) Destroy(lineGo);
            s = null; car = null;
        }

        public void SetVisible(bool v)
        {
            visible = v;
            foreach (var t in new[] { left, right, top }) if (t != null) t.gameObject.SetActive(v && (t != right || (s != null && s.HasOpponents && Settings.ShowTimingTower)));
        }

        void Update()
        {
            if (s == null || car == null || left == null) return;
            var p = s.player;
            // Messages and start lights update every frame; the rest at 15 Hz.
            bool msg = Time.time < s.bigMessageUntil && !string.IsNullOrEmpty(s.bigMessage);
            bool yellow = s.PlayerYellow && !msg, blue = !yellow && s.PlayerBlue && !msg;
            msgT.text = msg ? s.bigMessage : yellow ? "YELLOW FLAG - NO OVERTAKING" : blue ? "BLUE FLAG - LET THE FASTER CAR BY" : s.PitRequested ? "BOX THIS LAP" : "";
            VUI.SetColor(flagBg, yellow ? new Color(0.95f, 0.8f, 0.1f, 0.85f) : blue ? new Color(0.1f, 0.35f, 0.9f, 0.85f) : msg || s.PitRequested ? new Color(0.04f, 0.045f, 0.05f, 0.75f) : new Color(0f, 0f, 0f, 0f));
            msgT.color = yellow ? Color.black : VUI.Text;
            lightsT.text = s.phase == RaceSession.Phase.Countdown ? new string('O', s.lightsLit).Replace("O", "<color=#FF2A12>●</color> ") + new string('O', 5 - s.lightsLit).Replace("O", "<color=#331010>●</color> ") : "";
            refresh -= Time.deltaTime;
            if (refresh > 0f) return;
            refresh = 1f / 15f;
            if (p == null) return;
            if (s.Timed)
            {
                int total = s.totalLaps;
                posT.text = s.HasOpponents ? "P" + p.position + "<size=60%>/" + s.entrants.Count + "</size>" : "";
                lapT.text = total > 999 ? "LAP " + (p.lap + 1) : "LAP " + Mathf.Min(p.lap + 1, total) + "/" + total;
                timeT.text = SaveData.FormatTime(s.LapClock(p)) + (p.lapInvalid ? " <size=60%><color=#FF5533>INV</color></size>" : "");
                bestT.text = "BEST  " + SaveData.FormatTime(p.bestLap);
                lastT.text = "LAST  " + SaveData.FormatTime(p.lastLap);
                clockT.text = s.IsRace ? "RACE  " + SaveData.FormatTime(s.RaceClock) + (p.penalty > 0f ? "   +" + p.penalty.ToString("0") + "s" : "") : "";
                // Live delta to your best lap, estimated from how far round the lap you are.
                if (p.bestLap < float.MaxValue && s.phase == RaceSession.Phase.Racing)
                {
                    float frac = Mathf.Clamp01(World.Current.track.Ahead(World.Current.startS, p.along) / World.Current.track.Length);
                    float d = s.LapClock(p) - p.bestLap * frac;
                    deltaT.text = SaveData.FormatDelta(d);
                    deltaT.color = d <= 0f ? VUI.Good : VUI.Accent;
                    if (Game.I.dashboard != null) { Game.I.dashboard.deltaText = deltaT.text; Game.I.dashboard.deltaColor = deltaT.color; }
                }
                else deltaT.text = "";
                if (Game.I.dashboard != null)
                {
                    Game.I.dashboard.posText = s.HasOpponents ? "P" + p.position : "";
                    Game.I.dashboard.lapText = lapT.text;
                }
            }
            else
            {
                timeT.text = Sky.I != null ? Sky.I.TimeLabel : "";
                bestT.text = Sky.I != null ? Sky.WeatherName(Sky.I.weather).ToUpperInvariant() : "";
                lastT.text = "HOLD B TO RESET";
                lapT.text = ""; posT.text = ""; deltaT.text = ""; clockT.text = "";
            }
            speedT.text = Env.Wetness > 0.2f ? "TRACK WET " + Mathf.RoundToInt(Env.Wetness * 100f) + "%" : "";
            if (right.gameObject.activeSelf) Tower();
        }

        void Tower()
        {
            var sb = new System.Text.StringBuilder();
            int n = s.entrants.Count;
            // Show the top few and the cars around you.
            int me = s.entrants.IndexOf(s.player);
            for (int i = 0; i < n; i++)
            {
                bool near = Mathf.Abs(i - me) <= 3;
                if (i >= 5 && !near) continue;
                if (i == 5 && me > 8) sb.Append("<color=#666>   ...</color>\n");
                var e = s.entrants[i];
                string name = Teams.Short(e.name);
                string gap;
                if (e.eliminated) gap = "OUT";
                else if (e.dnf) gap = "DNF";
                else if (i == 0) gap = s.phase == RaceSession.Phase.Racing ? "LAP " + (e.lap + 1) : "";
                else if (e.gapToLeader < 0f) gap = "+" + Mathf.RoundToInt(-e.gapToLeader) + " LAP";
                else gap = "+" + e.gapToLeader.ToString("0.0");
                string line = (i + 1).ToString().PadLeft(2) + "  " + name + "  " + gap;
                if (e.isPlayer) line = "<color=#FF5A2E>" + line + "</color>";
                else if (e.car != null && e.car.damage != null && e.car.damage.Wrecked) line = "<color=#888>" + line + "</color>";
                sb.Append(line).Append('\n');
            }
            towerT.text = sb.ToString();
        }

        // ------------------------------------------------------------------ racing line

        void BuildLine(RacingLineMode mode)
        {
            var w = World.Current;
            if (w == null || w.line == null) return;
            var speed = s.player != null ? w.line.SpeedProfile(s.player.car.spec, 0.97f, Env.Wetness) : w.referenceSpeed;
            var acc = new MeshAcc();
            var t = w.track;
            int prev = -1;
            for (int i = 0; i <= t.Count; i++)
            {
                int si = t.Wrap(i);
                float dv = speed[t.Wrap(si + 6)] - speed[si];
                // Green where you accelerate, yellow lift, red brake.
                Color c = dv < -1.2f ? new Color(0.95f, 0.15f, 0.1f, 0.75f) : dv < -0.2f ? new Color(1f, 0.8f, 0.15f, 0.6f) : new Color(0.2f, 0.9f, 0.35f, 0.45f);
                bool show = mode == RacingLineMode.Full || dv < -0.2f;
                if (!show) { prev = -1; continue; }
                Vector3 p = w.line.Point(si) + t.up[si] * 0.065f;
                int a = acc.Add(p - t.right[si] * 0.35f, t.up[si], new Vector2(0, i), new Vector2(0, i), c);
                int b = acc.Add(p + t.right[si] * 0.35f, t.up[si], new Vector2(1, i), new Vector2(1, i), c);
                if (prev >= 0) acc.Quad(prev, prev + 1, b, a, Vector3.up);
                prev = a;
            }
            var shader = Shader.Find("Veylix/UI") ?? Shader.Find("Sprites/Default");
            lineGo = acc.Emit(w.transform, "RacingLine", new Material(shader), Layers.Scenery, false, null, false);
        }
    }
}
