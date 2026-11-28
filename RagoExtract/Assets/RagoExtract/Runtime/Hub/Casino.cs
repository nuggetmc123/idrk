using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The Market's casino corner: slots, blackjack and the prize wheel. In-game money only. Each machine's customer
    /// stands at its local -Z, facing +Z. Odds favour the house a little (slots ~93%, wheel ~96%, blackjack ~99%).
    /// </summary>
    public static class Casino
    {
        public static readonly int[] Bets = { 100, 250, 500, 1000, 2500, 5000 };
        public static void Build(Transform parent, Vector3 at, float yaw)
        {
            var root = new GameObject("Casino").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            if (AgeGate.Restricted)
            {
                // younger players: the casino's closed (shutters down, a sign)
                var shut = new MB(); shut.Box(new Vector3(-0.5f, 1.3f, 0.3f), new Vector3(5.6f, 2.6f, 0.08f), new Color(0.3f, 0.31f, 0.33f), 0.01f);
                shut.Build("Shutter", root, Mats.Lit("casino_shutter", Color.white, 0.6f, 0.4f, Mats.Metal, null, 0.3f, 0.3f));
                UI.Text(root, "CASINO - 18+ ONLY", new Vector3(-0.5f, 1.8f, 0.25f), 0.14f, new Color(1f, 0.75f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);   // read from the room side (-z), like the other signs
                return;
            }
            SlotMachine.Create(root, new Vector3(-3.0f, 0, 0), 0);
            SlotMachine.Create(root, new Vector3(-2.0f, 0, 0), 0);
            BlackjackTable.Create(root, new Vector3(0.4f, 0, -0.2f), 0);
            PrizeWheel.Create(root, new Vector3(3.2f, 0, 0.3f), 0);
            UI.Text(root, "CASINO", new Vector3(0, 3.3f, 0.6f), 0.35f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(root, "slots  -  blackjack  -  prize wheel", new Vector3(0, 2.95f, 0.6f), 0.09f, new Color(0.95f, 0.9f, 0.8f));
        }

        public static int Cash => Profile.Current != null ? Profile.Current.money : 0;
        public static bool Take(int amount)
        {
            if (Profile.Current == null || Profile.Current.money < amount) { Sfx.UI(Synth.UiError(), 0.4f); RagoGame.I?.Player?.Toast("Not enough money for that bet"); return false; }
            Profile.Current.money -= amount; Profile.Current.Save();
            return true;
        }
        public static void Pay(int amount)
        {
            if (amount <= 0 || Profile.Current == null) return;
            Profile.Current.money += amount; Profile.Current.Save();
            Sfx.UI(Synth.Cash(), 0.6f);
        }
        public static Material Body(string key, Color c) => Mats.Lit(key, c, 0.3f, 0.5f, Mats.Metal, Mats.MetalN, 0.2f, 0.2f);
        public static void Col(GameObject go, Vector3 c, Vector3 s) { var b = go.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

        /// <summary>BET - / BET + buttons and a label; returns the label so the owner can refresh it.</summary>
        public static TextMesh BetControls(Transform parent, Vector3 at, System.Func<int> get, System.Action<int> set, System.Func<bool> locked)
        {
            var label = UI.Text(parent, "", at + new Vector3(0, 0.07f, 0), 0.03f, Color.white);
            UI.Button(parent, "BET -", at + new Vector3(-0.13f, 0, 0), new Vector2(0.12f, 0.06f), () => { if (!locked()) { set(Mathf.Max(0, get() - 1)); Sfx.UI(Synth.UiClick(), 0.4f); } }, new Color(0.3f, 0.3f, 0.34f), BtnStyle.Key);
            UI.Button(parent, "BET +", at + new Vector3(0.13f, 0, 0), new Vector2(0.12f, 0.06f), () => { if (!locked()) { set(Mathf.Min(Bets.Length - 1, get() + 1)); Sfx.UI(Synth.UiClick(), 0.4f); } }, new Color(0.3f, 0.3f, 0.34f), BtnStyle.Key);
            return label;
        }
    }

    // ============================================================================================ SLOTS
    public class SlotMachine : MonoBehaviour
    {
        static readonly string[] Sym = { "7", "BAR", "BELL", "CHERRY", "SKULL" };
        // every symbol is equally likely on every reel - completely random (pays back ~94% on average)
        static readonly Color[] SymCol = { new Color(1f, 0.25f, 0.2f), new Color(0.9f, 0.9f, 0.9f), new Color(1f, 0.85f, 0.2f), new Color(0.95f, 0.3f, 0.45f), new Color(0.6f, 0.6f, 0.65f) };
        TextMesh[] _reels = new TextMesh[3];
        TextMesh _info, _bet, _result;
        int _betIx = 1; bool _spinning; float _spinT; int[] _final = new int[3];

        public static SlotMachine Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("SlotMachine");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var s = go.AddComponent<SlotMachine>(); s.Build(); return s;
        }

        void Build()
        {
            var mb = new MB(2);
            Color red = UI.Lin(new Color(0.55f, 0.08f, 0.08f)), gold = UI.Lin(new Color(0.85f, 0.65f, 0.2f)), dk = UI.Lin(new Color(0.06f, 0.06f, 0.07f));
            mb.Sub(0).Box(new Vector3(0, 0.55f, 0), new Vector3(0.78f, 1.1f, 0.62f), red, 0.02f);              // base
            mb.Sub(0).Box(new Vector3(0, 1.45f, 0.06f), new Vector3(0.78f, 0.7f, 0.5f), red, 0.02f);           // head
            mb.Sub(1).Box(new Vector3(0, 1.82f, 0.06f), new Vector3(0.8f, 0.06f, 0.52f), gold, 0.01f);         // top trim
            mb.Sub(1).Box(new Vector3(0, 1.1f, -0.2f), new Vector3(0.8f, 0.05f, 0.24f), gold, 0.01f);          // ledge
            mb.Sub(0).Box(new Vector3(0, 1.45f, -0.195f), new Vector3(0.66f, 0.3f, 0.01f), dk, 0f);            // reel window
            mb.Sub(1).Cyl(new Vector3(0.42f, 1.2f, 0.05f), new Vector3(0.42f, 1.65f, 0.05f), 0.02f, 0.02f, 8, gold); // lever
            mb.Sub(1).Sphere(new Vector3(0.42f, 1.68f, 0.05f), new Vector3(0.05f, 0.05f, 0.05f), 10, red);
            mb.Build("Slots", transform, Casino.Body("slot_body", Color.white), Mats.Lit("slot_trim", Color.white, 0.8f, 0.6f));
            Casino.Col(gameObject, new Vector3(0, 0.9f, 0.02f), new Vector3(0.8f, 1.8f, 0.62f));
            for (int i = 0; i < 3; i++)
            {
                _reels[i] = UI.Text(transform, Sym[3], new Vector3(-0.21f + i * 0.21f, 1.45f, -0.205f), 0.045f, SymCol[3], TextAnchor.MiddleCenter, FontStyle.Bold);
                if (i < 2) UI.Text(transform, "|", new Vector3(-0.105f + i * 0.21f, 1.45f, -0.205f), 0.06f, new Color(0.4f, 0.4f, 0.4f));
            }
            UI.Text(transform, "SLOTS", new Vector3(0, 1.7f, -0.2f), 0.07f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _result = UI.Text(transform, "", new Vector3(0, 1.26f, -0.2f), 0.028f, Color.white);
            UI.Text(transform, "777 x20   BAR x12   BELL x8\nCHERRY x6   2 cherries x2   1 cherry = bet back", new Vector3(0, 0.98f, -0.32f), 0.018f, new Color(0.95f, 0.85f, 0.6f));
            _bet = Casino.BetControls(transform, new Vector3(-0.08f, 1.14f, -0.33f), () => _betIx, v => _betIx = v, () => _spinning);
            UI.Button(transform, "SPIN", new Vector3(0.26f, 1.14f, -0.33f), new Vector2(0.16f, 0.09f), Spin, new Color(0.15f, 0.5f, 0.15f), BtnStyle.Key);
            Refresh();
        }

        void Refresh() { if (_bet != null) _bet.text = "BET $" + Casino.Bets[_betIx].ToString("N0") + "    CASH $" + Casino.Cash.ToString("N0"); }

        static int Roll() => Random.Range(0, Sym.Length);

        void Spin()
        {
            if (_spinning) return;
            if (!Casino.Take(Casino.Bets[_betIx])) return;
            for (int i = 0; i < 3; i++) _final[i] = Roll();
            _spinning = true; _spinT = 0f; _result.text = "";
            Sfx.Play(Synth.Pump(), transform.position + Vector3.up * 1.4f, 0.5f, 1.3f, 1f, 8f);
            Refresh();
        }

        float _tick;
        void Update()
        {
            if (!_spinning) return;
            _spinT += Time.deltaTime;
            _tick -= Time.deltaTime;
            for (int i = 0; i < 3; i++)
            {
                bool stopped = _spinT > 0.9f + i * 0.45f;
                int s = stopped ? _final[i] : Random.Range(0, Sym.Length);
                if (!stopped && _tick > 0f) continue;
                _reels[i].text = Sym[s]; _reels[i].color = SymCol[s];
            }
            if (_tick <= 0f) { _tick = 0.07f; Sfx.UI(Synth.UiClick(), 0.15f); }
            if (_spinT > 0.9f + 2 * 0.45f + 0.15f) Finish();
        }

        void Finish()
        {
            _spinning = false;
            int a = _final[0], b = _final[1], c = _final[2], bet = Casino.Bets[_betIx];
            int cherries = (a == 3 ? 1 : 0) + (b == 3 ? 1 : 0) + (c == 3 ? 1 : 0);
            int mult = 0; string what = "no luck";
            if (a == b && b == c && a != 4) { mult = new[] { 20, 12, 8, 6, 0 }[a]; what = Sym[a] + " x3!"; }
            else if (cherries == 2) { mult = 2; what = "two cherries"; }
            else if (cherries == 1) { mult = 1; what = "a cherry - bet back"; }
            int win = bet * mult;
            Casino.Pay(win);
            if (mult >= 6) Sfx.UI(Synth.Ding(), 0.6f);
            _result.text = win > 0 ? what + "  +$" + win.ToString("N0") : what;
            _result.color = win > bet ? new Color(0.4f, 1f, 0.5f) : Color.white;
            Refresh();
        }
    }

    // ============================================================================================ BLACKJACK
    public class BlackjackTable : MonoBehaviour
    {
        TextMesh _dealer, _player, _status, _bet;
        readonly List<int> _deck = new List<int>(), _d = new List<int>(), _p = new List<int>();
        int _betIx = 1, _stake; bool _inHand, _hideHole;

        public static BlackjackTable Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Blackjack");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = go.AddComponent<BlackjackTable>(); t.Build(); return t;
        }

        void Build()
        {
            var mb = new MB(3);
            Color felt = UI.Lin(new Color(0.08f, 0.35f, 0.18f)), wood = UI.Lin(new Color(0.35f, 0.2f, 0.1f)), dk = UI.Lin(new Color(0.08f, 0.08f, 0.08f));
            mb.Sub(1).Box(new Vector3(0, 0.45f, 0.1f), new Vector3(0.5f, 0.9f, 0.4f), wood, 0.02f);              // pedestal
            mb.Sub(0).Box(new Vector3(0, 0.92f, 0.1f), new Vector3(1.7f, 0.04f, 0.9f), felt, 0.005f);              // felt top
            mb.Sub(1).Box(new Vector3(0, 0.9f, -0.37f), new Vector3(1.8f, 0.08f, 0.08f), wood, 0.02f);             // armrest
            mb.Sub(2).Box(new Vector3(0, 1.45f, 0.5f), new Vector3(1.4f, 0.9f, 0.04f), dk, 0.01f);                 // dealer board
            mb.Build("Table", transform, Mats.Lit("bj_felt", Color.white, 0f, 0.1f, Mats.Fabric, null, 0.2f, 0f), Mats.Lit("bj_wood", Color.white, 0.1f, 0.5f, Mats.Wood, Mats.WoodN, 0.2f, 0.1f), Casino.Body("bj_board", Color.white));
            Casino.Col(gameObject, new Vector3(0, 0.47f, 0.1f), new Vector3(1.8f, 0.95f, 0.95f));
            UI.Text(transform, "BLACKJACK", new Vector3(0, 1.82f, 0.47f), 0.07f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(transform, "pays 3:2  -  dealer stands on 17", new Vector3(0, 1.73f, 0.47f), 0.024f, new Color(0.9f, 0.85f, 0.7f));
            _dealer = UI.Text(transform, "", new Vector3(0, 1.6f, 0.47f), 0.045f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _player = UI.Text(transform, "", new Vector3(0, 1.4f, 0.47f), 0.045f, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _status = UI.Text(transform, "Place your bet and DEAL", new Vector3(0, 1.27f, 0.47f), 0.03f, new Color(0.95f, 0.85f, 0.4f));
            _bet = Casino.BetControls(transform, new Vector3(-0.45f, 1.1f, 0.47f), () => _betIx, v => _betIx = v, () => _inHand);
            float x0 = -0.12f;
            UI.Button(transform, "DEAL", new Vector3(x0, 1.1f, 0.465f), new Vector2(0.16f, 0.08f), Deal, new Color(0.15f, 0.5f, 0.15f), BtnStyle.Key);
            UI.Button(transform, "HIT", new Vector3(x0 + 0.19f, 1.1f, 0.465f), new Vector2(0.16f, 0.08f), Hit, new Color(0.25f, 0.35f, 0.55f), BtnStyle.Key);
            UI.Button(transform, "STAND", new Vector3(x0 + 0.38f, 1.1f, 0.465f), new Vector2(0.16f, 0.08f), Stand, new Color(0.55f, 0.25f, 0.15f), BtnStyle.Key);
            UI.Button(transform, "DOUBLE", new Vector3(x0 + 0.57f, 1.1f, 0.465f), new Vector2(0.16f, 0.08f), Double, new Color(0.5f, 0.4f, 0.1f), BtnStyle.Key);
            Show();
        }

        int Draw()
        {
            if (_deck.Count < 15) { _deck.Clear(); for (int i = 0; i < 52 * 4; i++) _deck.Add(i % 52); for (int i = _deck.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (_deck[i], _deck[j]) = (_deck[j], _deck[i]); } }
            int c = _deck[_deck.Count - 1]; _deck.RemoveAt(_deck.Count - 1); return c;
        }
        static string Name(int c) { int r = c % 13; string rk = r == 0 ? "A" : r >= 10 ? new[] { "J", "Q", "K" }[r - 10] : (r + 1).ToString(); return rk + "♠♥♦♣"[c / 13]; }
        static int Total(List<int> h, out bool soft)
        {
            int t = 0, aces = 0;
            foreach (var c in h) { int r = c % 13; if (r == 0) { aces++; t += 11; } else t += Mathf.Min(10, r + 1); }
            while (t > 21 && aces > 0) { t -= 10; aces--; }
            soft = aces > 0; return t;
        }
        static int Total(List<int> h) => Total(h, out _);

        void Show()
        {
            string dh = "";
            for (int i = 0; i < _d.Count; i++) dh += (i == 1 && _hideHole ? "??" : Name(_d[i])) + "  ";
            string ph = ""; foreach (var c in _p) ph += Name(c) + "  ";
            _dealer.text = _d.Count == 0 ? "DEALER" : "DEALER  " + dh + (_hideHole ? "" : "(" + Total(_d) + ")");
            _player.text = _p.Count == 0 ? "YOU" : "YOU  " + ph + "(" + Total(_p) + ")";
            _bet.text = "BET $" + Casino.Bets[_betIx].ToString("N0") + "    CASH $" + Casino.Cash.ToString("N0");
        }

        void Deal()
        {
            if (_inHand) return;
            int bet = Casino.Bets[_betIx];
            if (!Casino.Take(bet)) return;
            _stake = bet; _inHand = true; _hideHole = true;
            _d.Clear(); _p.Clear();
            _p.Add(Draw()); _d.Add(Draw()); _p.Add(Draw()); _d.Add(Draw());
            Sfx.UI(Synth.UiClick(), 0.4f);
            bool pbj = Total(_p) == 21, dbj = Total(_d) == 21;
            if (pbj || dbj)
            {
                _hideHole = false;
                if (pbj && dbj) End("Both blackjack - push", _stake);
                else if (pbj) End("BLACKJACK! pays 3:2", _stake + _stake * 3 / 2);
                else End("Dealer blackjack", 0);
                return;
            }
            _status.text = "HIT, STAND or DOUBLE";
            Show();
        }

        void Hit()
        {
            if (!_inHand) { _status.text = "DEAL first"; Sfx.UI(Synth.UiError(), 0.3f); return; }
            _p.Add(Draw()); Sfx.UI(Synth.UiClick(), 0.4f);
            if (Total(_p) > 21) { _hideHole = false; End("Bust", 0); return; }
            if (Total(_p) == 21) { Stand(); return; }
            Show();
        }

        void Double()
        {
            if (!_inHand) { _status.text = "DEAL first - DOUBLE doubles your bet for exactly one more card"; Sfx.UI(Synth.UiError(), 0.3f); return; }
            if (_p.Count != 2) { _status.text = "You can only double on your first two cards"; Sfx.UI(Synth.UiError(), 0.3f); return; }
            if (!Casino.Take(_stake)) { _status.text = "Not enough cash to double ($" + _stake.ToString("N0") + ")"; return; }
            _status.text = "Doubled - one card"; Sfx.UI(Synth.Cash(), 0.4f);
            _stake *= 2;
            _p.Add(Draw());
            if (Total(_p) > 21) { _hideHole = false; End("Bust (doubled)", 0); return; }
            Stand();
        }

        void Stand()
        {
            if (!_inHand) { _status.text = "DEAL first"; Sfx.UI(Synth.UiError(), 0.3f); return; }
            _hideHole = false;
            while (Total(_d) < 17) _d.Add(Draw());
            int p = Total(_p), d = Total(_d);
            if (d > 21) End("Dealer busts - you win", _stake * 2);
            else if (p > d) End("You win", _stake * 2);
            else if (p == d) End("Push - bet back", _stake);
            else End("Dealer wins", 0);
        }

        void End(string msg, int payout)
        {
            _inHand = false;
            Casino.Pay(payout);
            _status.text = msg + (payout > 0 ? "  (+$" + payout.ToString("N0") + ")" : "");
            _status.color = payout > _stake ? new Color(0.4f, 1f, 0.5f) : payout == _stake ? Color.white : new Color(1f, 0.5f, 0.4f);
            Show();
        }
    }

    // ============================================================================================ PRIZE WHEEL
    public class PrizeWheel : MonoBehaviour
    {
        // 24 slices, each equally likely: 11 blanks, 7 x1 (bet back), 3 x2, 2 x3, 1 x5 - breaks even on average
        static readonly int[] Seg = { 0, 1, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 5, 0, 1, 0, 2, 0, 1, 3, 0, 1, 0, 1 };
        Transform _wheel;
        TextMesh _bet, _result;
        int _betIx = 1; bool _spinning; float _from, _to, _t, _dur, _lastTick;

        public static PrizeWheel Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("PrizeWheel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var w = go.AddComponent<PrizeWheel>(); w.Build(); return w;
        }

        static Color ColorOf(int m) => m == 0 ? new Color(0.15f, 0.15f, 0.18f) : m == 1 ? new Color(0.25f, 0.45f, 0.7f) : m == 2 ? new Color(0.2f, 0.6f, 0.3f) : m == 3 ? new Color(0.7f, 0.45f, 0.15f) : m == 5 ? new Color(0.7f, 0.2f, 0.6f) : new Color(0.9f, 0.75f, 0.15f);

        void Build()
        {
            var mb = new MB(1);
            Color dk = UI.Lin(new Color(0.12f, 0.12f, 0.14f)), gold = UI.Lin(new Color(0.85f, 0.65f, 0.2f));
            mb.Box(new Vector3(0, 1.0f, 0.12f), new Vector3(0.25f, 2.0f, 0.12f), dk, 0.02f);                  // post
            mb.Box(new Vector3(0, 0.05f, 0.12f), new Vector3(1.0f, 0.1f, 0.6f), dk, 0.02f);                   // foot
            mb.Box(new Vector3(0, 2.92f, -0.06f), new Vector3(0.08f, 0.16f, 0.04f), gold, 0f, 1, Quaternion.Euler(0, 0, 45)); // pointer
            mb.Box(new Vector3(0, 1.0f, -0.75f), new Vector3(0.5f, 1.0f, 0.3f), dk, 0.02f);                   // control stand
            mb.Build("WheelStand", transform, Casino.Body("wheel_stand", Color.white));
            Casino.Col(gameObject, new Vector3(0, 1.0f, 0.12f), new Vector3(0.3f, 2.0f, 0.15f));
            Casino.Col(gameObject, new Vector3(0, 0.5f, -0.75f), new Vector3(0.5f, 1.0f, 0.3f));
            // the wheel: a coloured disc of 24 segments (mesh with vertex colours), each with its prize written on it
            _wheel = new GameObject("Wheel").transform;
            _wheel.SetParent(transform, false); _wheel.localPosition = new Vector3(0, 2.0f, -0.04f);
            var mesh = new Mesh();
            int n = Seg.Length; float R = 0.8f;
            var v = new List<Vector3>(); var c = new List<Color>(); var tr = new List<int>(); var nr = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a0 = (i - 0.5f) * Mathf.PI * 2 / n, a1 = (i + 0.5f) * Mathf.PI * 2 / n;
                Color col = UI.Lin((i & 1) == 0 ? ColorOf(Seg[i]) : ColorOf(Seg[i]) * 0.85f);
                int b0 = v.Count;
                v.Add(Vector3.zero); v.Add(new Vector3(Mathf.Sin(a0), Mathf.Cos(a0)) * R); v.Add(new Vector3(Mathf.Sin(a1), Mathf.Cos(a1)) * R);
                for (int k = 0; k < 3; k++) { c.Add(col); nr.Add(Vector3.back); }
                tr.Add(b0); tr.Add(b0 + 1); tr.Add(b0 + 2);
                tr.Add(b0); tr.Add(b0 + 2); tr.Add(b0 + 1);   // both sides
                var lt = UI.Text(_wheel, Seg[i] == 0 ? "X" : "x" + Seg[i], Vector3.zero, Seg[i] >= 5 ? 0.07f : 0.05f, Seg[i] == 0 ? new Color(0.6f, 0.6f, 0.6f) : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                float am = i * Mathf.PI * 2 / n;
                lt.transform.localPosition = new Vector3(Mathf.Sin(am), Mathf.Cos(am), 0) * (R * 0.75f) + Vector3.back * 0.005f;
                lt.transform.localRotation = Quaternion.Euler(0, 0, -am * Mathf.Rad2Deg);
            }
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetNormals(nr); mesh.SetTriangles(tr, 0); mesh.RecalculateBounds();
            var mf = _wheel.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            _wheel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit("wheel_face", Color.white, 0f, 0.5f);
            UI.Text(transform, "PRIZE WHEEL", new Vector3(0, 3.1f, -0.06f), 0.08f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _result = UI.Text(transform, "x5 x3 x2 x1 - or nothing", new Vector3(0, 1.62f, -0.91f), 0.026f, Color.white);
            _bet = Casino.BetControls(transform, new Vector3(0, 1.43f, -0.91f), () => _betIx, x => _betIx = x, () => _spinning);
            UI.Button(transform, "SPIN", new Vector3(0, 1.3f, -0.91f), new Vector2(0.2f, 0.08f), Spin, new Color(0.15f, 0.5f, 0.15f), BtnStyle.Key);
            Refresh();
        }

        void Refresh() { if (_bet != null) _bet.text = "BET $" + Casino.Bets[_betIx].ToString("N0") + "    CASH $" + Casino.Cash.ToString("N0"); }

        void Spin()
        {
            if (_spinning || !Casino.Take(Casino.Bets[_betIx])) return;
            int target = Random.Range(0, Seg.Length);
            float step = 360f / Seg.Length;
            _from = Mathf.Repeat(_wheel.localEulerAngles.z, 360f);
            // rotating by +a about z brings the segment at angle a (clockwise from the top) up to the pointer
            float want = target * step + Random.Range(-step * 0.35f, step * 0.35f);
            _to = _from + 360f * 4f + Mathf.Repeat(want - _from, 360f);
            _t = 0f; _dur = 4.5f; _spinning = true; _result.text = "";
            Refresh();
        }

        void Update()
        {
            if (!_spinning) return;
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur), e = 1f - Mathf.Pow(1f - k, 3f);
            float a = Mathf.Lerp(_from, _to, e);
            _wheel.localRotation = Quaternion.Euler(0, 0, a);
            float step = 360f / Seg.Length;
            if (Mathf.Floor(a / step) != Mathf.Floor(_lastTick / step)) Sfx.Play(Synth.UiClick(), _wheel.position, 0.35f, 1.2f, 1f, 8f);
            _lastTick = a;
            if (k >= 1f)
            {
                _spinning = false;
                int idx = Mathf.RoundToInt(Mathf.Repeat(a, 360f) / step) % Seg.Length;
                int mult = Seg[idx], win = Casino.Bets[_betIx] * mult;
                Casino.Pay(win);
                if (mult >= 3) Sfx.UI(Synth.Ding(), 0.6f);
                _result.text = mult == 0 ? "Nothing this time" : "x" + mult + "  +$" + win.ToString("N0");
                _result.color = mult > 1 ? new Color(0.4f, 1f, 0.5f) : Color.white;
                Refresh();
            }
        }
    }
}
