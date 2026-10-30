using System;
using UnityEngine;

namespace Flopzone
{
    /// <summary>Controller buttons an action can be bound to.</summary>
    public enum XRButton
    {
        None,
        A, B, X, Y,
        LeftStickClick, RightStickClick,
    }

    /// <summary>Actions the player can rebind in Menu > Controls.</summary>
    public enum ControlAction
    {
        Jump,
        UnloadMag,
        Sprint,
        Crouch,
    }

    public enum TurnStyle { Snap, Smooth }

    [Serializable]
    public class GameSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.6f;
        public float sfxVolume = 1f;
        public TurnStyle turnStyle = TurnStyle.Snap;
        public float snapAngle = 45f;
        public float smoothTurnSpeed = 120f;
        public bool sprintToggle = true;
        public bool crouchToggle = true;
        public int primaryGun = 16;   // index into GunCatalog.All (Rattle AR)
        public int sidearmGun = 0;    // a pistol
        public int look = 0;          // your outfit: a packed Look (hat, top, pants, shoes, gloves)
        public XRButton jump = XRButton.A;
        public XRButton unloadMag = XRButton.B;
        public XRButton sprint = XRButton.LeftStickClick;
        public XRButton crouch = XRButton.RightStickClick;

        public XRButton Get(ControlAction a)
        {
            switch (a)
            {
                case ControlAction.Jump: return jump;
                case ControlAction.UnloadMag: return unloadMag;
                case ControlAction.Sprint: return sprint;
                default: return crouch;
            }
        }

        public void Set(ControlAction a, XRButton b)
        {
            // a button can only do one thing: give the old owner this action's old button
            foreach (ControlAction other in Enum.GetValues(typeof(ControlAction)))
            {
                if (other != a && Get(other) == b) SetRaw(other, Get(a));
            }
            SetRaw(a, b);
        }

        void SetRaw(ControlAction a, XRButton b)
        {
            switch (a)
            {
                case ControlAction.Jump: jump = b; break;
                case ControlAction.UnloadMag: unloadMag = b; break;
                case ControlAction.Sprint: sprint = b; break;
                default: crouch = b; break;
            }
        }

        public void ResetControls()
        {
            var d = new GameSettings();
            jump = d.jump; unloadMag = d.unloadMag; sprint = d.sprint; crouch = d.crouch;
            turnStyle = d.turnStyle; snapAngle = d.snapAngle; smoothTurnSpeed = d.smoothTurnSpeed;
            sprintToggle = d.sprintToggle; crouchToggle = d.crouchToggle;
        }

        public void ResetAudio()
        {
            var d = new GameSettings();
            masterVolume = d.masterVolume; musicVolume = d.musicVolume; sfxVolume = d.sfxVolume;
        }

        public static string ButtonName(XRButton b)
        {
            switch (b)
            {
                case XRButton.A: return "A";
                case XRButton.B: return "B";
                case XRButton.X: return "X";
                case XRButton.Y: return "Y";
                case XRButton.LeftStickClick: return "Left Stick (press)";
                case XRButton.RightStickClick: return "Right Stick (press)";
                default: return "-";
            }
        }

        public static string ActionName(ControlAction a)
        {
            switch (a)
            {
                case ControlAction.Jump: return "Jump";
                case ControlAction.UnloadMag: return "Unload Mag";
                case ControlAction.Sprint: return "Sprint";
                default: return "Crouch";
            }
        }
    }

    /// <summary>Saved per-device data: settings, the account, tutorial progress.</summary>
    public static class Save
    {
        const string SettingsKey = "fz.settings.v1";
        public static GameSettings Settings { get; private set; }

        public static void Load()
        {
            Settings = null;
            try
            {
                string s = PlayerPrefs.GetString(SettingsKey, "");
                if (!string.IsNullOrEmpty(s)) Settings = JsonUtility.FromJson<GameSettings>(s);
            }
            catch (Exception e) { Debug.LogWarning("[Sloppy] settings reset: " + e.Message); }
            if (Settings == null) Settings = new GameSettings();
            Settings.primaryGun = Mathf.Clamp(Settings.primaryGun, 0, GunCatalog.All.Length - 1);
            Settings.sidearmGun = Mathf.Clamp(Settings.sidearmGun, 0, GunCatalog.All.Length - 1);
        }

        public static void Store()
        {
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(Settings));
            PlayerPrefs.Save();
        }

        public static string AccountId
        {
            get => PlayerPrefs.GetString("fz.id", "");
            set { PlayerPrefs.SetString("fz.id", value); PlayerPrefs.Save(); }
        }

        public static string AccountToken
        {
            get => PlayerPrefs.GetString("fz.token", "");
            set { PlayerPrefs.SetString("fz.token", value); PlayerPrefs.Save(); }
        }

        public static string OfflineName
        {
            get => PlayerPrefs.GetString("fz.offlineName", "");
            set { PlayerPrefs.SetString("fz.offlineName", value); PlayerPrefs.Save(); }
        }

        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt("fz.tutorial", 0) == 1;
            set { PlayerPrefs.SetInt("fz.tutorial", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static long OfflineMuteUntil
        {
            get => long.TryParse(PlayerPrefs.GetString("fz.offlineMute", "0"), out var v) ? v : 0;
            set { PlayerPrefs.SetString("fz.offlineMute", value.ToString()); PlayerPrefs.Save(); }
        }
    }
}
