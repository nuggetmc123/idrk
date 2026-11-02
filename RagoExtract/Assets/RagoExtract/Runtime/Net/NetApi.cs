using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    public struct QueueStatus
    {
        public int players, max, target;
        public float elapsed, maxWait;
        public string text;
    }

    public class RaidStart
    {
        public string map;
        public int seed;
        public int spawnIndex;
        public int players;
        public bool online;
        public int partySlot;   // 0 = first of your party at that spawn spot, 1 = next to them, ...
    }

    /// <summary>
    /// Matchmaking rules for raids: 4-10 players per raid. The queue tries to reach 8 players;
    /// at 10 it starts immediately, and once the queue timer hits the max wait (1 minute) it starts
    /// with however many players are in the queue.
    /// </summary>
    public static class QueueRules
    {
        public const int Min = 4, Target = 8, Max = 10;
        public const float MaxWait = 60f;      // "if the timer goes to 1 minute just put them in"
        public const float TargetGrace = 5f;   // once 8+ are in, wait a few seconds for stragglers

        /// <summary>Should the host start the raid now?</summary>
        public static bool ShouldStart(int players, float elapsed, float timeAtTarget, int partySize = 1)
        {
            if (players >= Max) return true;
            if (players < partySize && elapsed < MaxWait) return false;   // wait for the rest of the party (up to the 1:00 limit)
            if (players >= Target && timeAtTarget >= TargetGrace) return true;
            if (elapsed >= MaxWait && players >= 1) return true;
            return false;
        }

        public static string Describe(int players, float elapsed)
        {
            float left = Mathf.Max(0, MaxWait - elapsed);
            if (players >= Target) return "Raid found! Launching...";
            return "Searching for operators  " + players + "/" + Max + "   (launch in " + Mathf.CeilToInt(left) + "s)";
        }
    }

    /// <summary>Transport + matchmaking backend. The real implementation (Photon Fusion 2, shared mode) lives in the
    /// RagoExtract.Fusion assembly and registers itself once Fusion is imported from the Rago Extract tab.</summary>
    public interface INetBackend
    {
        string Status { get; }
        bool Ready { get; }
        bool InSession { get; }
        bool IsHost { get; }
        ulong LocalId { get; }
        ulong HostId { get; }
        string SessionId { get; }
        IReadOnlyList<ulong> Peers { get; }
        void Init(Action<bool> done);
        void JoinMarket(Action<bool> done);
        void QueueRaid(string map, Action<QueueStatus> status, Action<RaidStart> start, Action<string> failed);
        void CancelQueue();
        void Leave();
        void Send(byte[] data, bool reliable, ulong target = ulong.MaxValue);
        event Action<ulong, byte[]> OnData;
        event Action<ulong> OnPeerJoined;
        event Action<ulong> OnPeerLeft;
        event Action OnDisconnected;

        // ---- party: play with friends. A party is a 4-character code; everyone in it waits together in their own
        // bunkers, the leader queues, and the whole party lands in the same raid at the same spawn.
        string PartyCode { get; }               // null when not in a party
        bool PartyLeader { get; }
        IReadOnlyList<string> PartyMembers { get; }
        void CreateParty(Action<bool> done);
        void JoinParty(string code, Action<bool> done);
        void LeaveParty();
        void RejoinParty();                     // back in the bunker after a raid / the Market
        event Action OnPartyChanged;
        event Action<string> OnPartyQueue;      // the leader started queueing for this map: follow them
        event Action OnPartyQueueCancelled;
    }

    /// <summary>Voice chat backend. The built-in implementation (VoiceChat) streams your microphone over the game connection.</summary>
    public interface IVoiceBackend
    {
        bool Ready { get; }
        string Status { get; }
        void Init(string displayName, Action<bool> done);
        void Join(string channel, bool positional);
        void Leave();
        void SetMuted(bool muted);
        bool Muted { get; }
        void UpdatePosition(Transform listener);
        bool IsSpeaking(string participantName);
    }

    public static class NetHub
    {
        public static INetBackend Backend;
        public static IVoiceBackend Voice;
        public static bool Online => Backend != null && Backend.Ready;

        /// <summary>Plain-language multiplayer status for the raid terminal / settings, saying exactly what's missing.</summary>
        public static string StatusText
        {
            get
            {
                if (Backend != null) return Backend.Ready ? "ONLINE - PHOTON FUSION (" + (Backend.Status ?? "") + ")" : "NOT ONLINE: " + (Backend.Status ?? "connecting...");
#if RAGO_DISABLE_NET
                return "MULTIPLAYER SWITCHED OFF: THE FUSION MODULE HAD A COMPILE ERROR.\nOPEN RAGO EXTRACT > MULTIPLAYER AND PRESS FINISH.";
#elif RAGO_FUSION
                return "MULTIPLAYER IS ON BUT DIDN'T START.\nCHECK THE UNITY CONSOLE FOR [RAGO] ERRORS.";
#else
                return "MULTIPLAYER NOT SET UP - SOLO RAIDS WITH AI.\nIN UNITY: RAGO EXTRACT > MULTIPLAYER (3 STEPS).";
#endif
            }
        }
    }
}
