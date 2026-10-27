using UnityEngine;

namespace Veylix
{
    public enum DamageMode { Off, Visual, Full }
    public enum RacingLineMode { Off, BrakingOnly, Full }
    public enum CrashView { StayInCockpit, FadeToChase }
    public enum PedalMode { Triggers, Thumbstick }

    /// <summary>Player options, saved on the device (PlayerPrefs). Read anywhere; the Settings screen writes them.</summary>
    public static class Settings
    {
        static float F(string k, float d) => PlayerPrefs.GetFloat("vx." + k, d);
        static int I(string k, int d) => PlayerPrefs.GetInt("vx." + k, d);
        static void SF(string k, float v) { PlayerPrefs.SetFloat("vx." + k, v); Changed?.Invoke(); }
        static void SI(string k, int v) { PlayerPrefs.SetInt("vx." + k, v); Changed?.Invoke(); }

        public static event System.Action Changed;

        // Driving
        public static float SteeringSensitivity { get => F("steerSens", 1f); set => SF("steerSens", Mathf.Clamp(value, 0.5f, 1.6f)); }
        /// <summary>Lock-to-centre wheel rotation in degrees; 0 = the car's real lock.</summary>
        public static int WheelRotation { get => I("wheelRot", 0); set => SI("wheelRot", value); }
        /// How heavy the VR steering wheel feels: 0 = light (follows your hands exactly), 1 = heavy (real racing-car weight).
        /// Where your VR gloves sit on your controllers (lines them up with your real hands): metres and degrees.
        public static float GloveForward { get => F("gloveFwd", 0f); set => SF("gloveFwd", Mathf.Clamp(value, -0.1f, 0.1f)); }
        public static float GloveUp { get => F("gloveUp", 0f); set => SF("gloveUp", Mathf.Clamp(value, -0.1f, 0.1f)); }
        public static float GloveTilt { get => F("gloveTilt", 0f); set => SF("gloveTilt", Mathf.Clamp(value, -45f, 45f)); }
        public static float SteeringWeight { get => F("steerWeight", 0.4f); set => SF("steerWeight", Mathf.Clamp01(value)); }
        public static float SteeringLinearity { get => F("steerLin", 0.2f); set => SF("steerLin", Mathf.Clamp01(value)); }
        public static float Haptics { get => F("haptics", 0.8f); set => SF("haptics", Mathf.Clamp01(value)); }
        public static bool Abs { get => I("abs", 0) == 1; set => SI("abs", value ? 1 : 0); }
        public static bool TractionControl { get => I("tc", 0) == 1; set => SI("tc", value ? 1 : 0); }
        public static bool AutoGears { get => I("autoGears", 0) == 1; set => SI("autoGears", value ? 1 : 0); }
        public static RacingLineMode RacingLine { get => (RacingLineMode)I("line", 0); set => SI("line", (int)value); }
        public static DamageMode Damage { get => (DamageMode)I("damage", 2); set => SI("damage", (int)value); }
        public static bool FuelUse { get => I("fuel", 1) == 1; set => SI("fuel", value ? 1 : 0); }
        public static bool TyreWear { get => I("wear", 1) == 1; set => SI("wear", value ? 1 : 0); }
        public static PedalMode Pedals { get => (PedalMode)I("pedals", 0); set => SI("pedals", (int)value); }
        public static bool Kmh { get => I("kmh", 1) == 1; set => SI("kmh", value ? 1 : 0); }

        // Comfort / VR
        public static bool Seated { get => I("seated", 1) == 1; set => SI("seated", value ? 1 : 0); }
        public static float Vignette { get => F("vignette", 0.3f); set => SF("vignette", Mathf.Clamp01(value)); }
        public static CrashView CrashCamera { get => (CrashView)I("crashCam", 1); set => SI("crashCam", (int)value); }
        public static bool HorizonLock { get => I("horizon", 0) == 1; set => SI("horizon", value ? 1 : 0); }
        public static float SeatHeight { get => F("seatH", 0f); set => SF("seatH", Mathf.Clamp(value, -0.15f, 0.15f)); }
        public static float SeatForward { get => F("seatF", 0f); set => SF("seatF", Mathf.Clamp(value, -0.15f, 0.15f)); }
        public static bool ShowTimingTower { get => I("tower", 1) == 1; set => SI("tower", value ? 1 : 0); }

        // Graphics: 0 Quest (standalone), 1 Balanced, 2 PC VR High
        public static int Quality { get => I("quality", Application.isMobilePlatform ? 0 : 1); set { SI("quality", Mathf.Clamp(value, 0, 2)); } }
        public static int Mirrors { get => I("mirrors", Application.isMobilePlatform ? 1 : 2); set => SI("mirrors", Mathf.Clamp(value, 0, 2)); }
        public static float RenderScale { get => F("renderScale", 1f); set => SF("renderScale", Mathf.Clamp(value, 0.7f, 1.4f)); }

        // Audio
        public static float MasterVolume { get => F("vol", 0.9f); set => SF("vol", Mathf.Clamp01(value)); }
        public static float EngineVolume { get => F("volEngine", 0.85f); set => SF("volEngine", Mathf.Clamp01(value)); }
        public static float EffectsVolume { get => F("volFx", 0.9f); set => SF("volFx", Mathf.Clamp01(value)); }
        public static float AmbienceVolume { get => F("volAmb", 0.7f); set => SF("volAmb", Mathf.Clamp01(value)); }
        public static float MusicVolume { get => F("volMusic", 0.55f); set => SF("volMusic", Mathf.Clamp01(value)); }
        public static bool RaceMusic { get => I("raceMusic", 0) == 1; set => SI("raceMusic", value ? 1 : 0); }

        public static string PlayerName
        {
            get => PlayerPrefs.GetString("vx.name", "");
            set { PlayerPrefs.SetString("vx.name", value ?? ""); Changed?.Invoke(); }
        }

        public static void Save() => PlayerPrefs.Save();

        public static string Speed(float ms) => Kmh ? Mathf.RoundToInt(ms * 3.6f).ToString() : Mathf.RoundToInt(ms * 2.23694f).ToString();
        public static string SpeedUnit => Kmh ? "KM/H" : "MPH";
    }
}
