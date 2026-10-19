using Fusion;
using UnityEngine;

namespace Nullbound
{
    /// <summary>
    /// Proving who a player is (so moderator powers and commands can't be faked) and receiving moderator commands.
    /// </summary>
    public partial class Fighter
    {
        float lastHello = -10f;

        static long UnixNow => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        byte[] HelloBytes(long time) => System.Text.Encoding.UTF8.GetBytes("NB-HELLO|" + PlayerId + "|" + Object.StateAuthority.PlayerId + "|" +
            (SessionManager.Instance != null ? SessionManager.Instance.SessionName : "") + "|" + time);

        void SendHello()
        {
            if (IsBot) return;
            lastHello = Time.time;
            long t = UnixNow;
            RPC_Hello(t, PlayerIdentity.PublicModulus, PlayerIdentity.Sign(HelloBytes(t)));
        }

        /// <summary>"This ID is mine": the owner signs their ID, this room and the time; everyone checks it.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Hello(long time, byte[] key, byte[] signature)
        {
            if (IsMine) { Verified = true; return; }
            if (System.Math.Abs(UnixNow - time) > 600) return;
            if (PlayerIdentity.Fingerprint(key) != PlayerId.ToString()) return;
            if (PlayerIdentity.Verify(key, HelloBytes(time), signature)) Verified = true;
        }

        /// <summary>A late joiner asks the owner to prove their ID again.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestHello()
        {
            if (Time.time - lastHello > 1f) SendHello();
        }

        /// <summary>A moderator's signed command for this fighter's owner (checked in <see cref="Moderation.Receive"/>).</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Moderate(byte action, int arg, long time, byte[] modKey, byte[] signature)
        {
            if (IsBot) return;
            Moderation.Receive(action, arg, time, modKey, signature);
        }
    }
}
