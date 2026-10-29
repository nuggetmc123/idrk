using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Whoever runs the room's rules on this headset (the offline LocalServer,
    /// or the Photon room's master client) and so also drives the bots.
    /// </summary>
    public interface IBotHost
    {
        void ApplyHit(string shooterId, string targetId, float dmg, string gun, bool head, float[] dir);
        void BotFell(string id);
        void SetPos(string id, Vector3 pos);
        void FlagAction(string id, string a, int team);
        string FlagCarrier(int team);
        string FlagState(int team);
        Vector3? FlagPos(int team);
        /// <summary>Share a bot's pose / shot with the other players (online only).</summary>
        void BotPose(string id, float[] pose);
        void BotShot(string id, int hand, Vector3 from, Vector3 to, int gun);
    }
}
