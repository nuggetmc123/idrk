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
                    if (init == null || init.IsError) { Debug.LogWarning("[Rago] Meta platform didn't start: " + (init?.GetError()?.Message ?? "?")); return; }
                    Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(msg =>
                    {
                        if (msg == null || msg.IsError)
                        {
                            Debug.LogError("[Rago] this Meta account doesn't own Rago Extract - closing");
                            _i.StartCoroutine(_i.NotOwned());
                            return;
                        }
                        Ready = true;
                        AgeGate.Check();
                        Dlc.Restore();
                    });
                });
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] Meta platform: " + e.Message); }
#endif
        }

        System.Collections.IEnumerator NotOwned()
        {
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null) pl.Hud.Center("RAGO EXTRACT\n<size=40>This copy isn't linked to your Meta account.\nGet the game from the Meta Horizon Store.</size>", 8f);
            yield return new WaitForSecondsRealtime(6f);
            Application.Quit();
        }

#if RAGO_META_IAP
        void Update() { try { Oculus.Platform.Request.RunCallbacks(); } catch { } }   // deliver the store's answers
#endif
    }
}
