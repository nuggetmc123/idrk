using Fusion;

namespace Rago.Net
{
    /// <summary>
    /// Photon RPCs for the game's messages (Photon's server hands each one to every player in the session):
    ///  * Rpc_Fast (unreliable): frequent, replaceable updates - movement, voice, raider positions.
    ///  * Rpc_Rel (reliable, in order): the small important ones - shots, hits, names, pings, items, trades. These used to
    ///    go through Photon's "reliable data" API, which is made for sending big files in slices and rations how much
    ///    goes out: with 3 players the messages queued up and the ping read over a second.
    /// Each payload says who it's for; players it isn't for ignore it. Big messages still use reliable data.
    ///
    /// If Unity ever reports an error in this file, just delete it: FusionBackend finds it by name and falls back to the
    /// old way on its own.
    /// </summary>
    public class RagoRpc : SimulationBehaviour
    {
        /// <summary>Called by FusionBackend (by reflection, so this file is optional).</summary>
        public static void Send(NetworkRunner runner, string payload) => Rpc_Fast(runner, payload);
        public static void SendRel(NetworkRunner runner, string payload) => Rpc_Rel(runner, payload);

        [Rpc(Channel = RpcChannel.Unreliable, InvokeLocal = false)]
        static void Rpc_Fast(NetworkRunner runner, string payload)
        {
            FusionBackend.OnFastData(runner, payload);
        }

        [Rpc(Channel = RpcChannel.Reliable, InvokeLocal = false)]
        static void Rpc_Rel(NetworkRunner runner, string payload)
        {
            FusionBackend.OnRelData(runner, payload);
        }
    }
}
