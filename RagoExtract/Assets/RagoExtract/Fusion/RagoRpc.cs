using Fusion;

namespace Rago.Net
{
    /// <summary>
    /// The fast lane for frequent updates (movement, voice, raider positions): ONE unreliable Photon RPC per update that
    /// Photon's server hands to every player in the session. Before, every player sent a separate reliable copy to every
    /// other player, and one lost packet held up everything behind it - that's what made 3+ players lag.
    /// Shots, hits, items and trades still go on the reliable channel.
    ///
    /// If Unity ever reports an error in this file, just delete it: FusionBackend finds it by name and falls back to the
    /// old (reliable-only) way on its own.
    /// </summary>
    public class RagoRpc : SimulationBehaviour
    {
        /// <summary>Called by FusionBackend (by reflection, so this file is optional).</summary>
        public static void Send(NetworkRunner runner, string payload) => Rpc_Fast(runner, payload);

        [Rpc(Channel = RpcChannel.Unreliable, InvokeLocal = false)]
        static void Rpc_Fast(NetworkRunner runner, string payload)
        {
            FusionBackend.OnFastData(runner, payload);
        }
    }
}
