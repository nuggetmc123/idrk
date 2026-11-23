using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Younger players (Meta says the account is a child's or a teen's): voice chat only with their own squad (no strangers),
    /// and the play-money casino is closed. Adults - and everyone until Meta answers, or on builds without the Meta
    /// Platform SDK - get the full game. Asked once at start-up (needs the RAGO_META_IAP define and the SDK, like DLC).
    /// </summary>
    public static class AgeGate
    {
        public static bool Restricted { get; private set; }
        static bool _asked;

        public static void Check()
        {
            if (_asked) return;
            _asked = true;
#if RAGO_META_IAP
            try
            {
                Oculus.Platform.UserAgeCategory.Get().OnComplete(msg =>
                {
                    if (msg == null || msg.IsError || msg.Data == null) return;
                    var c = msg.Data.AgeCategory;
                    Restricted = c == Oculus.Platform.AccountAgeCategory.Ch || c == Oculus.Platform.AccountAgeCategory.Tn;
                    if (Restricted) Debug.Log("[Rago] younger player: voice chat with squad only, casino closed");
                });
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] age group: " + e.Message); }
#endif
        }
    }
}
