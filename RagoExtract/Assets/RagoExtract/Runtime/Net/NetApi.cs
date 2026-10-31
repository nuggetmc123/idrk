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
        public static bool ShouldStart(int players, float elapsed, float timeAtTarget)
        {
            if (players >= Max) return true;
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

    /// <summary>Transport + matchmaking backend. The real implementation (Netcode for GameObjects + Unity Lobby/Relay)
    /// lives in the RagoExtract.Net assembly and registers itself when those packages are installed.</summary>
    public interface INetBackend
    {
        string Status { get; }
        bool Ready { get; }
        bool InSession { get; }
        bool IsHost { get; }
        ulong LocalId { get; }
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
    }

    /// <summary>Voice chat backend (Vivox). Implemented in RagoExtract.Voice when the Vivox package is present.</summary>
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
    }
}
