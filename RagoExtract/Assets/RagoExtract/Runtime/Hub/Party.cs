using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The party board in your bunker, next to the raid terminal: play with friends.
    ///  - CREATE PARTY gives you a 4-character code. Tell it to your friends.
    ///  - Friends type the code on the keypad and press JOIN.
    ///  - Everyone stays in their own bunker; the board lists who's in the party.
    ///  - The leader picks a map and presses QUEUE on the raid terminal: the whole party is put into the same raid
    ///    and spawns together. The raid waits for the whole party (up to the usual 1:00 limit).
    ///  - After the raid you're back in the party automatically.
    /// </summary>
    public class PartyBoard : MonoBehaviour
    {
        const string Keys = "ACDEFHJKMNPRTUVWXY3479";
        TextMesh _info, _code;
        WorldButton _createBtn, _leaveBtn;
        string _typed = "";
        bool _busy;
        float _refresh;

        public static PartyBoard Create(Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("PartyBoard");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var b = go.AddComponent<PartyBoard>();
            b.Build();
            return b;
        }

        void Build()
        {
            var p = UI.Panel(transform, "Panel", Vector3.zero, Quaternion.identity, new Vector2(1.25f, 1.36f));
            UI.Text(p, "PARTY", new Vector3(0, 0.6f, 0), 0.065f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _info = UI.Text(p, "", new Vector3(-0.56f, 0.52f, 0), 0.036f, Color.white, TextAnchor.UpperLeft);
            _createBtn = UI.Button(p, "CREATE PARTY", new Vector3(-0.29f, 0.06f, 0), new Vector2(0.5f, 0.09f), Create, new Color(0.2f, 0.42f, 0.2f));
            _leaveBtn = UI.Button(p, "LEAVE PARTY", new Vector3(0.29f, 0.06f, 0), new Vector2(0.5f, 0.09f), Leave, new Color(0.5f, 0.15f, 0.1f));
            _code = UI.Text(p, "", new Vector3(-0.56f, -0.05f, 0), 0.038f, UI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            // keypad: the 22 code characters, then DEL and JOIN
            for (int i = 0; i < Keys.Length + 2; i++)
            {
                int col = i % 8, row = i / 8;
                var at = new Vector3(-0.4375f + col * 0.125f, -0.16f - row * 0.11f, 0);
                if (i < Keys.Length)
                {
                    char k = Keys[i];
                    UI.Button(p, k.ToString(), at, new Vector2(0.11f, 0.09f), () => Type(k), null, BtnStyle.Key);
                }
                else if (i == Keys.Length) UI.Button(p, "DEL", at, new Vector2(0.11f, 0.09f), () => { if (_typed.Length > 0) _typed = _typed.Substring(0, _typed.Length - 1); Refresh(); }, new Color(0.45f, 0.3f, 0.12f), BtnStyle.Key);
                else UI.Button(p, "JOIN", at, new Vector2(0.11f, 0.09f), Join, new Color(0.2f, 0.42f, 0.2f), BtnStyle.Key);
            }
            UI.Text(p, "The leader picks a map and presses QUEUE on the raid terminal:\nthe whole party goes into the same raid and spawns together.",
                new Vector3(0, -0.56f, 0), 0.026f, UI.Dim, TextAnchor.MiddleCenter);
            if (NetHub.Backend != null) NetHub.Backend.OnPartyChanged += Refresh;
            Refresh();
        }

        void OnDestroy() { if (NetHub.Backend != null) NetHub.Backend.OnPartyChanged -= Refresh; }

        void Update()
        {
            _refresh -= Time.deltaTime;
            if (_refresh <= 0) { _refresh = 1f; Refresh(); }
        }

        void Type(char k)
        {
            if (_typed.Length >= 4) _typed = "";
            _typed += k;
            Sfx.UI(Synth.Beep(), 0.2f);
            Refresh();
        }

        bool CanParty(out string why)
        {
            why = null;
            if (!NetHub.Online) { why = "Parties need multiplayer: set it up in Unity (Rago Extract > Multiplayer)."; return false; }
            if (Kiosk.AnyQueued) { why = "Cancel the raid queue first."; return false; }
            if (_busy) { why = "One moment..."; return false; }
            return true;
        }

        void Create()
        {
            var pl = RagoGame.I.Player;
            if (!CanParty(out var why)) { pl.Toast(why); return; }
            if (NetHub.Backend.PartyCode != null) { pl.Toast("You're already in party " + NetHub.Backend.PartyCode + " - leave it first"); return; }
            _busy = true; Refresh();
            NetHub.Backend.CreateParty(ok =>
            {
                _busy = false;
                if (this == null) return;
                pl.Toast(ok ? "Party " + NetHub.Backend.PartyCode + " created - tell your friends the code" : "Couldn't create a party: " + NetHub.Backend.Status);
                Sfx.UI(ok ? Synth.QueueFound() : Synth.UiError(), 0.4f);
                Refresh();
            });
            Refresh();
        }

        void Join()
        {
            var pl = RagoGame.I.Player;
            if (!CanParty(out var why)) { pl.Toast(why); return; }
            if (_typed.Length != 4) { pl.Toast("Type the 4-character party code first"); Sfx.UI(Synth.UiError(), 0.3f); return; }
            if (NetHub.Backend.PartyCode == _typed) { pl.Toast("You're already in that party"); return; }
            string code = _typed;
            _busy = true; Refresh();
            NetHub.Backend.JoinParty(code, ok =>
            {
                _busy = false;
                if (this == null) return;
                if (ok) { _typed = ""; pl.Toast("Joined party " + code); Sfx.UI(Synth.QueueFound(), 0.4f); }
                else { pl.Toast("No party with code " + code); Sfx.UI(Synth.UiError(), 0.4f); }
                Refresh();
            });
        }

        void Leave()
        {
            var pl = RagoGame.I.Player;
            if (NetHub.Backend == null || NetHub.Backend.PartyCode == null) { pl.Toast("You're not in a party"); return; }
            if (Kiosk.AnyQueued) { pl.Toast("Cancel the raid queue first."); return; }
            string code = NetHub.Backend.PartyCode;
            NetHub.Backend.LeaveParty();
            pl.Toast("Left party " + code);
            Refresh();
        }

        void Refresh()
        {
            if (this == null || _info == null) return;
            var be = NetHub.Backend;
            string text;
            if (!NetHub.Online) text = "Multiplayer isn't set up, so parties are off.\nIn Unity: Rago Extract > Multiplayer.";
            else if (_busy) text = "Connecting...";
            else if (be.PartyCode == null) text = "You're not in a party.\n\nCREATE one and give your friends the code,\nor type a friend's code below and press JOIN.";
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("Party code:  ").Append(be.PartyCode).Append(be.PartyLeader ? "   (you lead)" : "").Append('\n');
                var m = be.PartyMembers;
                sb.Append(m.Count).Append(m.Count == 1 ? " operator - waiting for friends\n" : " operators\n");
                for (int i = 0; i < m.Count && i < 8; i++) sb.Append("  - ").Append(m[i]).Append('\n');
                if (!be.PartyLeader) sb.Append("\nThe leader starts the queue - you'll follow them in.");
                text = sb.ToString();
            }
            _info.text = text;
            _code.text = "JOIN A FRIEND'S PARTY:   " + (_typed.Length > 0 ? _typed : "") + new string('_', Mathf.Max(0, 4 - _typed.Length));
        }
    }
}
