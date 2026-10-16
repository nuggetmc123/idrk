using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// The five lights on the start gantry: they turn red one by one during the
    /// countdown and all go green at GO.
    /// </summary>
    public class StartLights : MonoBehaviour
    {
        public Renderer[] lights;

        int shown = -2;

        void Update()
        {
            var race = RaceManager.Instance;
            if (lights == null || lights.Length == 0) return;
            int state; // -1 = off, 0..n = that many red, 99 = green
            if (race == null || !race.IsReady || !KartModeInfo.IsRace(GameState.CurrentMode)) state = -1;
            else if (race.State == RaceState.Countdown)
            {
                float total = race.countdownSeconds;
                float done = Mathf.Clamp01(1f - race.PhaseRemaining / total);
                state = Mathf.Clamp(Mathf.FloorToInt(done * (lights.Length + 1)), 0, lights.Length);
            }
            else if (race.State == RaceState.Racing && race.Elapsed < 3f) state = 99;
            else state = -1;

            if (state == shown) return;
            shown = state;
            var off = KartUI.Mat(new Color(0.15f, 0.05f, 0.05f), 0.8f);
            var red = KartUI.Glow(new Color(1f, 0.1f, 0.05f));
            var green = KartUI.Glow(new Color(0.1f, 1f, 0.2f));
            for (int i = 0; i < lights.Length; i++)
                if (lights[i]) lights[i].sharedMaterial = state == 99 ? green : (state > i ? red : off);
        }
    }
}
