using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// Spectator mode - only inside the Unity editor (never in a build you give out). Press F8 in Play mode:
    /// you stop being sent to anyone (no body, no name, no voice), raiders ignore you, and you fly a free camera.
    /// ] / [: next / previous player   V: view (free / behind / their eyes)   WASD + mouse: fly   Space / Ctrl: up / down   Shift: fast.
    /// F8 again: back to playing (you appear to the others again).
    /// </summary>
    public class Spectator : MonoBehaviour
    {
        public static bool Active { get; private set; }
        static Spectator _i;
        int _target = -1, _mode;   // mode 0 free, 1 behind, 2 eyes
        bool _wasInvisible;
        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isEditor) return;   // editor only: there is no way to turn this on in a build
            var go = new GameObject("Spectator"); DontDestroyOnLoad(go); _i = go.AddComponent<Spectator>();
        }

        static bool Key(KeyCode k) { try { return Input.GetKey(k); } catch { return false; } }
        static bool KeyDown(KeyCode k) { try { return Input.GetKeyDown(k); } catch { return false; } }

        void Update()
        {
            if (!Application.isEditor) return;
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl == null) return;
            if (KeyDown(KeyCode.F8)) Set(!Active, pl);
            if (!Active) return;
            var remotes = NetGame.I != null ? new List<RemotePlayer>(NetGame.I.Remotes.Values) : new List<RemotePlayer>();
            remotes.RemoveAll(r => r == null);
            if (KeyDown(KeyCode.RightBracket) && remotes.Count > 0) { _target = (_target + 1) % remotes.Count; if (_mode == 0) _mode = 1; }
            if (KeyDown(KeyCode.LeftBracket) && remotes.Count > 0) { _target = (_target - 1 + remotes.Count) % remotes.Count; if (_mode == 0) _mode = 1; }
            if (KeyDown(KeyCode.V)) _mode = (_mode + 1) % 3;
            if (_target >= remotes.Count) _target = remotes.Count - 1;
            var t = _target >= 0 && remotes.Count > 0 ? remotes[_target] : null;
            float dt = Time.unscaledDeltaTime;
            if (t != null && _mode > 0)
            {
                Vector3 hp = t.HeadPos; var hr = t.HeadRot;
                Vector3 fwd = hr * Vector3.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
                Vector3 eye = _mode == 2 ? hp + hr * new Vector3(0, 0, 0.08f) : hp - fwd * 2.4f + Vector3.up * 0.7f;
                PlaceEye(pl, eye);
                if (!XRInputs.VR)
                {
                    var look = _mode == 2 ? hr : Quaternion.LookRotation((hp - eye).normalized, Vector3.up);
                    var e = look.eulerAngles; Desktop.Yaw = e.y; Desktop.Pitch = e.x > 180 ? e.x - 360 : e.x;
                }
                return;
            }
            // free flight
            Vector3 m = XRInputs.VR ? new Vector3(XRInputs.Left.Stick.x, 0, XRInputs.Left.Stick.y) : new Vector3(Desktop.Move.x, 0, Desktop.Move.y);
            if (Key(KeyCode.Space)) m.y += 1; if (Key(KeyCode.LeftControl)) m.y -= 1;
            float speed = Key(KeyCode.LeftShift) ? 18f : 6f;
            var head = pl.Head;
            Vector3 v = head.forward * m.z + head.right * m.x + Vector3.up * m.y;
            pl.transform.position += v * speed * dt;
        }

        static void PlaceEye(PlayerRig pl, Vector3 eye) { pl.transform.position += eye - pl.Head.position; }

        void Set(bool on, PlayerRig pl)
        {
            Active = on;
            pl.Frozen = on;
            if (pl.Cc != null) pl.Cc.enabled = !on;
            pl.HandL.SetVisible(!on); pl.HandR.SetVisible(!on);
            if (pl.Body != null && pl.Body.Rig != null && pl.Body.Rig.root != null) pl.Body.Rig.root.SetActive(!on);
            if (on) { _wasInvisible = Tester.Invisible; Tester.Invisible = true; _target = -1; _mode = 0; pl.Toast("SPECTATING - F8 to stop"); }
            else
            {
                Tester.Invisible = _wasInvisible;
                // land somewhere solid below where you are
                if (Physics.Raycast(pl.transform.position + Vector3.up * 0.5f, Vector3.down, out var h, 200f, Ballistics.WallMask, QueryTriggerInteraction.Ignore))
                    pl.Teleport(h.point + Vector3.up * 0.05f, pl.Head.eulerAngles.y);
                NetGame.I?.Announce();
                pl.Toast("Back in the game");
            }
        }

        void OnGUI()
        {
            if (!Active || !Application.isEditor) return;
            if (_style == null) { _style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true }; _style.normal.textColor = Color.white; }
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            var sb = new System.Text.StringBuilder("<b>SPECTATING</b>  (F8 stop)   ] / [ next / previous player   V view: " + (_mode == 0 ? "free" : _mode == 1 ? "behind" : "their eyes") + "   WASD + mouse fly, Space / Ctrl up / down, Shift fast\n");
            sb.Append("Area: ").Append(RagoGame.I != null ? RagoGame.I.Area.ToString() : "-");
            if (NpcDirector.I != null) sb.Append("   Raiders alive: ").Append(NpcDirector.I.Npcs.FindAll(n => n != null && n.Alive).Count);
            sb.Append('\n');
            if (NetGame.I != null)
            {
                int i = 0;
                foreach (var r in NetGame.I.Remotes.Values)
                {
                    if (r == null) continue;
                    float d = pl != null ? Vector3.Distance(pl.Head.position, r.HeadPos) : 0;
                    sb.Append(i == _target ? "> " : "   ").Append(r.DisplayName).Append(r.Alive ? "" : "  (dead)").Append("   ").Append(d.ToString("0")).Append(" m");
                    string hold = r.HoldingRight; var def = string.IsNullOrEmpty(hold) ? null : Db.Get(hold);
                    if (def != null) sb.Append("   holding ").Append(def.name);
                    sb.Append('\n'); i++;
                }
                if (i == 0) sb.Append("(nobody else here - join their party, raid or the Market)\n");
            }
            GUI.Label(new Rect(12, 12, 900, 400), sb.ToString(), _style);
        }
    }
}
