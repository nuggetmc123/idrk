using System.Collections.Generic;
using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// Heads-up info that is comfortable in VR: a lazy-follow board for match
    /// status, kill feed and toasts; a watch on the left wrist (health, ammo,
    /// clock); a red flash when hurt; full-view popups for big moments.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud I { get; private set; }

        Transform follow;           // lazily follows where you look
        TextMesh status, center, toast;
        readonly List<(TextMesh text, float until)> feed = new List<(TextMesh, float)>();
        float toastUntil, centerUntil;
        Material vignette;
        float hurt;
        Transform watch;
        TextMesh watchHp, watchInfo;
        Transform hpFill;
        UIPanel popup;
        public bool PopupOpen => popup != null;
        float followYaw;

        void Awake()
        {
            I = this;
            follow = Prims.Empty("HudFollow", transform).transform;
            status = UIKit.Text(follow, "", new Vector3(0, 0.55f, 0), 0.07f, Color.white);
            center = UIKit.Text(follow, "", new Vector3(0, 0.12f, 0), 0.16f, UIKit.Accent);
            toast = UIKit.Text(follow, "", new Vector3(0, -0.3f, 0), 0.055f, Color.white);
            for (int i = 0; i < 5; i++)
            {
                var t = UIKit.Text(follow, "", new Vector3(0.85f, 0.45f - i * 0.065f, 0), 0.045f, Color.white, TextAnchor.MiddleRight);
                feed.Add((t, 0f));
            }
        }

        public void Attach(PlayerRig rig)
        {
            // red damage flash right in front of the eyes
            var v = Prims.Make(PrimitiveType.Quad, rig.head, new Vector3(0, 0, 0.12f), new Vector3(0.5f, 0.4f, 1), null, false);
            v.name = "hurtFlash";
            vignette = Prims.Sprite(new Color(0.8f, 0, 0, 0));
            vignette.mainTexture = VignetteTexture();
            vignette.renderQueue = 4000;
            v.GetComponent<Renderer>().sharedMaterial = vignette;

            // the wrist watch
            watch = Prims.Empty("Watch", rig.hands[0].transform, new Vector3(0, 0.035f, -0.11f)).transform;
            watch.localRotation = Quaternion.Euler(90, 0, 0);
            UIKit.Quad(watch, new Vector3(0, 0, 0.002f), new Vector2(0.11f, 0.075f), new Color(0.05f, 0.06f, 0.09f, 0.92f), 2995);
            UIKit.Quad(watch, new Vector3(0, -0.026f, 0.001f), new Vector2(0.1f, 0.012f), new Color(0.2f, 0.05f, 0.05f, 1f), 2996);
            hpFill = UIKit.Quad(watch, new Vector3(0, -0.026f, 0f), new Vector2(0.1f, 0.012f), new Color(0.3f, 0.9f, 0.35f, 1f), 2997).transform;
            watchHp = UIKit.Text(watch, "100", new Vector3(0, -0.011f, -0.001f), 0.016f, Color.white);
            watchInfo = UIKit.Text(watch, "", new Vector3(0, 0.017f, -0.001f), 0.012f, UIKit.Muted);
        }

        static Texture2D VignetteTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float a = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.35f) / 0.65f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.I;
            if (rig == null) return;
            var head = rig.head;
            float yaw = head.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(followYaw, yaw)) > 25f) followYaw = Mathf.MoveTowardsAngle(followYaw, yaw, Time.deltaTime * 120f);
            var rot = Quaternion.Euler(0, followYaw, 0);
            follow.SetPositionAndRotation(head.position + rot * new Vector3(0, -0.05f, 1.6f), rot);

            for (int i = 0; i < feed.Count; i++)
            {
                var (t, until) = feed[i];
                float a = Mathf.Clamp01(until - Time.time);
                var c = t.color; c.a = a; t.color = c;
            }
            if (Time.time > toastUntil) toast.text = "";
            if (Time.time > centerUntil) center.text = "";

            hurt = Mathf.MoveTowards(hurt, 0, Time.deltaTime * 1.6f);
            if (vignette) vignette.color = new Color(0.75f, 0, 0, Mathf.Clamp01(hurt) * 0.85f);
            UpdateStatus();
            UpdateWatch(rig);
        }

        void UpdateStatus()
        {
            var g = Game.I;
            if (g == null || g.session == null) { status.text = ""; return; }
            var s = g.session;
            if (!s.InMatch) { status.text = ""; return; }
            string modeName = LocalServer.Modes.TryGetValue(s.Mode ?? "", out var md) ? md.name : s.Mode;
            string clock = Clock(s.TimeLeft);
            string line;
            if (md != null && md.teams)
                line = $"<color=#ff6b5e>RED {s.Scores[0]}</color>   {clock}   <color=#6b9bff>{s.Scores[1]} BLUE</color>";
            else
            {
                var me = s.Me;
                int lead = 0;
                foreach (var p in s.Players.Values) lead = Mathf.Max(lead, p.k);
                line = $"YOU {(me != null ? me.k : 0)}   {clock}   LEADER {lead}";
            }
            if (s.State == "waiting") line = "Waiting for more players...";
            else if (s.State == "countdown") line = "Starting in " + Mathf.CeilToInt(s.TimeLeft);
            status.text = modeName + "  ·  " + MapBuilder.Names[Mathf.Clamp(s.Map, 0, 9)] + "\n" + line;
        }

        static string Clock(float t)
        {
            int s = Mathf.CeilToInt(t);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void UpdateWatch(PlayerRig rig)
        {
            if (watch == null) return;
            var g = Game.I;
            int hp = g != null ? g.MyHp : 100;
            watchHp.text = rig.Alive ? hp + " HP" : "FLOPPED";
            float k = Mathf.Clamp01(hp / 100f);
            hpFill.localScale = new Vector3(0.1f * k, 0.012f, 1);
            hpFill.localPosition = new Vector3(-0.05f * (1 - k), -0.026f, 0);
            hpFill.GetComponent<Renderer>().sharedMaterial.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), k);
            var gun = rig.hands[1].HeldGun ?? rig.hands[0].HeldGun;
            string ammo = gun != null ? (gun.hasMag ? gun.ammo + "/" + gun.def.magSize : "NO MAG") : "";
            string clock = g != null && g.session != null && g.session.InMatch ? Clock(g.session.TimeLeft) : (g != null && g.session != null && g.session.Online ? "ONLINE" : "OFFLINE");
            watchInfo.text = clock + (ammo.Length > 0 ? "   " + ammo : "");
        }

        public void Hurt(float amount)
        {
            hurt = Mathf.Max(hurt, Mathf.Clamp01(amount));
        }

        public void Toast(string msg, float seconds = 3.5f)
        {
            toast.text = UIKit.Wrap(msg, 1.6f, 0.055f);
            toastUntil = Time.time + seconds;
            AudioManager.Ui("toast", 0.6f);
        }

        public void Big(string msg, float seconds = 2f, Color? color = null)
        {
            center.text = msg;
            center.color = color ?? UIKit.Accent;
            centerUntil = Time.time + seconds;
        }

        public void Feed(string line, Color color)
        {
            for (int i = feed.Count - 1; i > 0; i--)
            {
                feed[i].text.text = feed[i - 1].text.text;
                feed[i].text.color = feed[i - 1].text.color;
                feed[i] = (feed[i].text, feed[i - 1].until);
            }
            feed[0].text.text = line;
            feed[0].text.color = color;
            feed[0] = (feed[0].text, Time.time + 6f);
        }

        /// <summary>A panel in front of the player with a message and buttons.</summary>
        public UIPanel Popup(string title, string body, params (string label, System.Action action)[] buttons)
        {
            ClosePopup();
            var rig = PlayerRig.I;
            popup = UIPanel.Create("Popup", new Vector2(0.8f, 0.46f), UIKit.PanelColor);
            popup.Text(title, new Vector2(0, 0.16f), 0.05f, UIKit.Accent);
            popup.Text(UIKit.Wrap(body, 0.72f, 0.032f), new Vector2(0, 0.04f), 0.032f, Color.white);
            float w = 0.22f, gap = 0.03f;
            float total = buttons.Length * w + (buttons.Length - 1) * gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                popup.Button(b.label, new Vector2(-total / 2 + w / 2 + i * (w + gap), -0.15f), new Vector2(w, 0.07f), () => b.action?.Invoke());
            }
            if (rig != null) popup.PlaceInFront(rig.head, 0.9f, 0.05f);
            if (UIPointer.I) UIPointer.I.Active = true;
            return popup;
        }

        public void ClosePopup()
        {
            if (popup != null) Destroy(popup.gameObject);
            popup = null;
            if (UIPointer.I && (Game.I == null || !Game.I.MenuOpen)) UIPointer.I.Active = false;
        }
    }
}
