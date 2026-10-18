using System.Linq;
using System.Text;
using Fusion;
using UnityEngine;

namespace KartVR
{
    public enum TagState : byte { Idle, Playing, Results }

    /// <summary>
    /// Tag, in karts. One player is IT (a red glowing ring and "IT!" floats over their kart). Bump into someone to
    /// make them IT; the new IT can't tag anyone for 3 seconds, so there are no instant tag-backs. You score a point
    /// for every second you're NOT it. Highest score when the 2.5 minute round ends wins. IT drives a little faster.
    /// </summary>
    public class TagManager : NetworkBehaviour
    {
        public static TagManager Instance { get; private set; }

        public float roundSeconds = 150f;
        public float resultsSeconds = 10f;
        public const float NoTagBackSeconds = 3f;

        [Networked] public TagState State { get; set; }
        [Networked] public int RoundId { get; set; }
        [Networked] public PlayerRef It { get; set; }
        [Networked] TickTimer Phase { get; set; }
        [Networked] TickTimer Immune { get; set; }

        public bool IsReady => Object != null && Object.IsValid;
        public float PhaseRemaining => IsReady ? Phase.RemainingTime(Runner) ?? 0f : 0f;
        public bool Playing => IsReady && State == TagState.Playing && KartModeInfo.IsTag(GameState.CurrentMode);
        /// <summary>The new IT is still in their no-tag-back time.</summary>
        public bool ItImmune => IsReady && !Immune.ExpiredOrNotRunning(Runner);

        public bool IsIt(KartController kart) => kart != null && kart.IsReady && Playing && kart.Object.StateAuthority == It;

        Transform marker;

        public override void Spawned() { Instance = this; }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            if (marker != null) Destroy(marker.gameObject);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (!KartModeInfo.IsTag(GameState.CurrentMode))
            {
                if (State != TagState.Idle) State = TagState.Idle;
                return;
            }
            switch (State)
            {
                case TagState.Idle:
                    StartRound();
                    break;
                case TagState.Playing:
                    // IT left the game: pick someone else.
                    if (!Runner.ActivePlayers.Contains(It)) PickIt();
                    if (Phase.Expired(Runner))
                    {
                        State = TagState.Results;
                        Phase = TickTimer.CreateFromSeconds(Runner, resultsSeconds);
                    }
                    break;
                case TagState.Results:
                    if (Phase.Expired(Runner)) StartRound();
                    break;
            }
        }

        void StartRound()
        {
            RoundId++;
            State = TagState.Playing;
            Phase = TickTimer.CreateFromSeconds(Runner, roundSeconds);
            PickIt();
        }

        void PickIt()
        {
            var players = Runner.ActivePlayers.ToList();
            It = players.Count > 0 ? players[Random.Range(0, players.Count)] : PlayerRef.None;
            Immune = TickTimer.CreateFromSeconds(Runner, NoTagBackSeconds);
        }

        /// <summary>IT bumped someone: they're IT now. Only IT can tag, and not during the no-tag-back time.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Tag(PlayerRef target, RpcInfo info = default)
        {
            if (State != TagState.Playing || info.Source != It || target == It) return;
            if (!Immune.ExpiredOrNotRunning(Runner)) return;
            if (!Runner.ActivePlayers.Contains(target)) return;
            It = target;
            Immune = TickTimer.CreateFromSeconds(Runner, NoTagBackSeconds);
            RPC_Tagged(target);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_Tagged(PlayerRef target)
        {
            var kart = KartController.All.FirstOrDefault(k => k != null && k.IsReady && k.Object.StateAuthority == target);
            if (kart != null) AudioManager.PlayAt(AudioManager.Sfx.Checkpoint, kart.transform.position, 1f, 0.7f);
            if (kart != null && kart.IsLocal) SessionManager.Instance?.SetStatus("YOU'RE IT! Bump someone to tag them", 4f);
        }

        public KartController ItKart => KartController.All.FirstOrDefault(k => k != null && k.IsReady && k.Object.StateAuthority == It);

        /// <summary>A glowing red ring and "IT!" over whoever is it, for everyone.</summary>
        public override void Render()
        {
            var kart = Playing ? ItKart : null;
            if (kart == null || kart.Ghost)
            {
                if (marker != null) marker.gameObject.SetActive(false);
                return;
            }
            if (marker == null)
            {
                marker = new GameObject("ItMarker").transform;
                var ring = KartUI.Part(marker, PrimitiveType.Cylinder, Vector3.zero, new Vector3(2.2f, 0.03f, 2.2f), KartUI.Glow(new Color(1f, 0.15f, 0.1f)));
                KartUI.Part(ring, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.85f, 1.2f, 0.85f), KartUI.Mat(new Color(0.1f, 0.02f, 0.02f), 0.3f));
                var label = KartUI.Label(marker, "<b>IT!</b>", new Vector3(0f, 0.9f, 0f), 6f, new Color(1f, 0.3f, 0.2f), new Vector2(3f, 1f));
                label.gameObject.AddComponent<FaceViewer>();
            }
            marker.gameObject.SetActive(true);
            // Over the kart, or over the player if they got out to walk.
            Vector3 at = kart.transform.position;
            if (kart.DriverOut)
                foreach (var a in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
                    if (a.IsReady && a.Object.StateAuthority == kart.Object.StateAuthority) { at = a.transform.position; break; }
            marker.position = at + Vector3.up * (2.6f + Mathf.Sin(Time.time * 4f) * 0.15f);
            marker.rotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            float blink = ItImmune ? (Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? 0.6f : 1f) : 1f;
            marker.localScale = Vector3.one * blink;
        }

        public string StandingsText(int max = 8)
        {
            var sb = new StringBuilder();
            var list = KartController.All.Where(k => k.IsReady).OrderByDescending(k => k.Score).ToList();
            for (int i = 0; i < list.Count && i < max; i++)
                sb.Append(i + 1).Append(". ").Append(list[i].DisplayName).Append("  ").Append(list[i].Score)
                  .Append(list[i].Object.StateAuthority == It ? "  (IT)" : "").Append('\n');
            return sb.ToString();
        }
    }

    /// <summary>Turns a label to face your camera.</summary>
    public class FaceViewer : MonoBehaviour
    {
        void LateUpdate()
        {
            var rig = KartRig.Instance;
            if (rig == null || rig.head == null) return;
            Vector3 away = transform.position - rig.head.position;
            if (away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
