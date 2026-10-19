using UnityEngine;

namespace Nullbound
{
    public enum TurnStyle { Snap30 = 0, Snap45 = 1, Smooth = 2 }

    /// <summary>Settings from the wrist menu, saved on this device.</summary>
    public static class GameSettings
    {
        const string SfxKey = "nb.sfxVolume", MusicKey = "nb.musicVolume", TurnKey = "nb.turnStyle",
            VignetteKey = "nb.vignette", RoomScaleKey = "nb.roomScale", FighterKey = "nb.fighter";

        /// <summary>Voice chat: open mic (talks when you speak), push-to-talk (hold the left stick in) or muted.</summary>
        public static MicMode Mic
        {
            get => (MicMode)Mathf.Clamp(PlayerPrefs.GetInt("nb.micMode", 0), 0, 2);
            set { PlayerPrefs.SetInt("nb.micMode", (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>How loud other players' voices are, 0..1.</summary>
        public static float VoiceVolume
        {
            get => PlayerPrefs.GetFloat("nb.voiceVolume", 1f);
            set { PlayerPrefs.SetFloat("nb.voiceVolume", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Sound effects volume, 0..1.</summary>
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.85f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Music volume, 0..1.</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.4f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>How the right stick turns you.</summary>
        public static TurnStyle Turn
        {
            get => (TurnStyle)Mathf.Clamp(PlayerPrefs.GetInt(TurnKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(TurnKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Comfort: darken the edges of your view while you move, dash or get launched (less motion sickness).</summary>
        public static bool Vignette
        {
            get => PlayerPrefs.GetInt(VignetteKey, 1) == 1;
            set { PlayerPrefs.SetInt(VignetteKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Room scale: really walking around your room moves your fighter (off = your body stays under the stick).</summary>
        public static bool RoomScale
        {
            get => PlayerPrefs.GetInt(RoomScaleKey, 1) == 1;
            set { PlayerPrefs.SetInt(RoomScaleKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Gore: blood when people get hit. Off (the default) = confetti instead.</summary>
        public static bool Gore
        {
            get => PlayerPrefs.GetInt("nb.gore", 0) == 1;
            set { PlayerPrefs.SetInt("nb.gore", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>See your own full body (arms, chest, legs) when you look down. Off = just your gloves.</summary>
        public static bool FullBody
        {
            get => PlayerPrefs.GetInt("nb.fullBody", 1) == 1;
            set { PlayerPrefs.SetInt("nb.fullBody", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The fighter you picked last time.</summary>
        public static FighterKind Fighter
        {
            get => (FighterKind)Mathf.Clamp(PlayerPrefs.GetInt(FighterKey, 0), 0, FighterInfo.Count - 1);
            set { PlayerPrefs.SetInt(FighterKey, (int)value); PlayerPrefs.Save(); }
        }

        public static string TurnName(TurnStyle t) => t == TurnStyle.Snap30 ? "Snap 30°" : t == TurnStyle.Snap45 ? "Snap 45°" : "Smooth";

        /// <summary>Returns the degrees to turn this frame. Snap styles turn once per stick flick.</summary>
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
