using UnityEditor;
using UnityEngine;

namespace Rago
{
    /// <summary>Editor menu for spectator mode (Play mode only). "Allow On This PC" is stored in this computer's editor settings.</summary>
    [InitializeOnLoad]
    static class RagoSpectatorMenu
    {
        static RagoSpectatorMenu()
        {
            Spectator.AllowedOnThisPc = EditorPrefs.GetBool(Spectator.PrefKey, false);
            EditorApplication.playModeStateChanged += _ => Spectator.AllowedOnThisPc = EditorPrefs.GetBool(Spectator.PrefKey, false);
        }

        [MenuItem("Rago Extract/Spectator/Toggle (F8)", priority = 40)]
        static void Toggle()
        {
            if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Spectator", "Press Play first, join the session you want to watch, then toggle spectator.", "OK"); return; }
            Spectator.Toggle();
        }

        [MenuItem("Rago Extract/Spectator/Allow On This PC", priority = 41)]
        static void Allow()
        {
            bool on = !EditorPrefs.GetBool(Spectator.PrefKey, false);
            EditorPrefs.SetBool(Spectator.PrefKey, on);
            Spectator.AllowedOnThisPc = on;
            Debug.Log("[Rago Extract] spectator mode " + (on ? "allowed" : "no longer allowed") + " on this PC");
        }

        [MenuItem("Rago Extract/Spectator/Allow On This PC", true)]
        static bool AllowCheck() { Menu.SetChecked("Rago Extract/Spectator/Allow On This PC", EditorPrefs.GetBool(Spectator.PrefKey, false)); return true; }
    }
}
