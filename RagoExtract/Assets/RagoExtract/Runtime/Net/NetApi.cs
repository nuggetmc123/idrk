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

        // per map: the Underground Sector is a tight 6-player raid with squads of up to 2
        public static int MaxFor(string map) => map == "underground" ? 6 : Max;
        public static int TargetFor(string map) => map == "underground" ? 6 : Target;
        public static int PartyMaxFor(string map) => map == "underground" ? 2 : Max;

        /// <summary>Should the host start the raid now?</summary>
        public static bool ShouldStart(int players, float elapsed, float timeAtTarget, int partySize = 1, string map = null)
        {
            int max = MaxFor(map), target = TargetFor(map);
            if (players >= max) return true;
            if (players < partySize && elapsed < MaxWait) return false;   // wait for the rest of the party (up to the 1:00 limit)
            if (players >= target && timeAtTarget >= TargetGrace) return true;
            if (elapsed >= MaxWait && players >= 1) return true;
            return false;
        }

        public static string Describe(int players, float elapsed, string map = null)
        {
            float left = Mathf.Max(0, MaxWait - elapsed);
            if (players >= TargetFor(map)) return "Raid found! Launching...";
            return "Searching for operators  " + players + "/" + MaxFor(map) + "   (launch in " + Mathf.CeilToInt(left) + "s)";
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

        // ---- party: play with friends. Invite people by callsign (friends, or players you've raided with); they
        // accept from their menu. The party waits together in their own bunkers, the leader queues, and the whole
        // party lands in the same raid at the same spawn.
        string PartyCode { get; }               // the party's id (its leader's callsign); null when not in a party
        bool PartyLeader { get; }
        IReadOnlyList<string> PartyMembers { get; }
        IReadOnlyList<PartyInvite> Invites { get; }
        void InviteToParty(string callsign);    // from the bunker or from inside a raid
        void AcceptInvite(PartyInvite inv);
        void DeclineInvite(PartyInvite inv);
        void LeaveParty();
        void RejoinParty();                     // back in the bunker: rejoin your party, or listen for invites
        bool IsTeammate(ulong peer);            // in your party (raid: green dot over their head)
        IReadOnlyList<string> SessionPlayers { get; }   // callsigns of everyone else in your current session
        event Action OnPartyChanged;
        event Action<string> OnPartyQueue;      // the leader started queueing for this map: follow them
        event Action OnPartyQueueCancelled;
        event Action<string> OnMetPlayer;       // someone in your raid told you their callsign (recent players)
    }

    public class PartyInvite
    {
        public string from;       // who invited you
        public string party;      // the party's id (its leader)
        public float time;
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
