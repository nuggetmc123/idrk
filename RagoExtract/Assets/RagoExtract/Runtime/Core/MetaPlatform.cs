using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The Meta Quest platform (store) connection: started once at boot with the App ID in Resources/Rago/meta_app_id.txt.
    /// - Ownership check (a store requirement): a copy of the game the player doesn't own shows a message and closes.
    /// - Then DLC purchases (MetaPayments) and the age group (AgeGate) use it.
    /// Only active when the Meta XR Platform SDK package (com.meta.xr.sdk.platform) is installed - the RAGO_META_IAP
    /// define is set automatically by the assembly's version defines - and only on the headset (Android).
    /// </summary>
    public class MetaPlatform : MonoBehaviour
    {
        public static bool Ready { get; private set; }
        static bool _started;
        static MetaPlatform _i;

        public static string AppId
        {
            get { var t = Resources.Load<TextAsset>("Rago/meta_app_id"); return t != null ? t.text.Trim() : null; }
        }

        static int _tries;

        public static void Init()
        {
            if (_started) return;
            _started = true;
#if RAGO_META_IAP
            if (Application.platform != RuntimePlatform.Android) return;
            try
            {
                var go = new GameObject("MetaPlatform"); DontDestroyOnLoad(go); _i = go.AddComponent<MetaPlatform>();
                var id = AppId;
                if (string.IsNullOrEmpty(id)) { Debug.LogWarning("[Rago] no Meta App ID (Resources/Rago/meta_app_id.txt)"); return; }
                Oculus.Platform.Core.AsyncInitialize(id).OnComplete(init =>
                {
                    if (init == null || init.IsError) { Debug.LogWarning("[Rago] Meta platform didn't start: " + (init?.GetError()?.Message ?? "?") + " - playing on without store features"); return; }
                    Debug.Log("[Rago] Meta platform started");
                    CheckOwned();
                });
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] Meta platform: " + e); }
#endif
        }

#if RAGO_META_IAP
        /// <summary>The store's ownership check. A failed answer is asked again (it can be a slow network or the store
        /// still waking up); only after several failures in a row is the copy treated as not owned.</summary>
        static void CheckOwned()
        {
            try
            {
                Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(msg =>
                {
                    if (msg != null && !msg.IsError)
                    {
                        Ready = true;
                        Debug.Log("[Rago] Meta ownership check: ok");
                        try { AgeGate.Check(); } catch (System.Exception e) { Debug.LogWarning("[Rago] age group: " + e.Message); }
                        try { Dlc.Restore(); } catch (System.Exception e) { Debug.LogWarning("[Rago] restore purchases: " + e.Message); }
                        return;
                    }
                    _tries++;
                    Debug.LogWarning("[Rago] Meta ownership check failed (" + _tries + "/4): " + (msg?.GetError()?.Message ?? "?"));
                    if (_tries < 4) { if (_i != null) _i.Invoke(nameof(Retry), 5f); return; }
                    if (_i != null) _i.StartCoroutine(_i.NotOwned());
                });
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] ownership check: " + e); }
        }
        void Retry() => CheckOwned();
#endif

        /// <summary>
        /// Close the game when the store says this copy isn't owned. OFF while the game is being tested: friends playing a
        /// copy installed straight from the APK (not through a Meta release channel) fail the check, and closing the game
        /// on them 30 seconds in looked like a crash / being kicked from the server. Turn it on (true) for the build you
        /// submit to the store, if Meta's review asks for it (VRC.Quest.Security.1).
        /// </summary>
        public const bool EnforceOwnership = false;

        System.Collections.IEnumerator NotOwned()
        {
            float t = 0f;
            while ((RagoGame.I == null || RagoGame.I.Player == null) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (!EnforceOwnership)
            {
                // testing: just say so (once) and carry on - DLC purchases need a store copy, everything else works
                Debug.LogWarning("[Rago] not a Meta store copy - playing on (ownership not enforced)");
                if (pl != null) pl.Toast("This copy isn't from the Meta store - DLC purchases are off, everything else works");
                yield break;
            }
            // store builds: give the player time to read it (so it isn't mistaken for a crash), then close as the store requires
            if (pl != null) pl.Hud.Center("RAGO EXTRACT\n<size=40>Couldn't confirm this copy with the Meta store.\nCheck you're signed in and online, then start the game again.</size>", 15f);
            yield return new WaitForSecondsRealtime(15f);
            Application.Quit();
        }
    }
}
