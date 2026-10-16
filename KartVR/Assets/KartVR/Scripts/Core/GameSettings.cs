using UnityEngine;

namespace KartVR
{
    public enum TurnStyle { Snap30 = 0, Snap45 = 1, Smooth = 2 }

    /// <summary>Settings from the menu (three-line button), saved on this device.</summary>
    public static class GameSettings
    {
        const string SfxKey = "kartvr.sfxVolume", MusicKey = "kartvr.musicVolume", EngineKey = "kartvr.engineVolume",
            TurnKey = "kartvr.turnStyle", WheelKey = "kartvr.grabWheel", MicKey = "kartvr.micMode", VoiceKey = "kartvr.voiceVolume";

        /// <summary>Voice chat: open mic (talks when you speak), push-to-talk (hold the left stick in) or muted.</summary>
        public static MicMode Mic
        {
            get => (MicMode)Mathf.Clamp(PlayerPrefs.GetInt(MicKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(MicKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>How loud other players' voices are, 0..1.</summary>
        public static float VoiceVolume
        {
            get => PlayerPrefs.GetFloat(VoiceKey, 1f);
            set { PlayerPrefs.SetFloat(VoiceKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Sound effects volume, 0..1.</summary>
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Music volume, 0..1.</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.45f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Kart engine volume, 0..1 (on top of the sound effects volume).</summary>
        public static float EngineVolume
        {
            get => PlayerPrefs.GetFloat(EngineKey, 0.5f);
            set { PlayerPrefs.SetFloat(EngineKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>How the right stick turns you when walking or flying.</summary>
        public static TurnStyle Turn
        {
            get => (TurnStyle)Mathf.Clamp(PlayerPrefs.GetInt(TurnKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(TurnKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Grab the steering wheel with your hands (on) or steer only with the left stick (off).</summary>
        public static bool GrabWheel
        {
            get => PlayerPrefs.GetInt(WheelKey, 1) == 1;
            set { PlayerPrefs.SetInt(WheelKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string TurnName(TurnStyle t) => t == TurnStyle.Snap30 ? "Snap 30°" : t == TurnStyle.Snap45 ? "Snap 45°" : "Smooth";

        /// <summary>
        /// Shared turning for walking and build-mode flying. Returns the degrees to turn this frame.
        /// Snap styles turn once per stick flick (latched until the stick comes back to the middle).
        /// </summary>
        public static float TurnAmount(float stick, ref bool latched, float dt)
        {
            var style = Turn;
            if (style == TurnStyle.Smooth)
                return Mathf.Abs(stick) > 0.2f ? stick * 120f * dt : 0f;
            if (Mathf.Abs(stick) > 0.7f && !latched)
            {
                latched = true;
                return Mathf.Sign(stick) * (style == TurnStyle.Snap45 ? 45f : 30f);
            }
            if (Mathf.Abs(stick) < 0.3f) latched = false;
            return 0f;
        }
    }
}
