using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartVR
{
    /// <summary>What each player sees and hears during a live event (see <see cref="EventManager"/>).</summary>
    public class LiveEventFx : MonoBehaviour
    {
        // An original song for the Disco Sing-Along: one line per bar of the party music.
        static readonly string[] Lyrics =
        {
            "Engines humming, lights are on,",
            "we've been racing since the dawn,",
            "spin the wheel and feel the beat,",
            "burning rubber, dancing feet!",
            "Kart VR, we're flying high,",
            "boost so fast we touch the sky,",
            "everybody sing along,",
            "this is our karting song!",
        };

        LiveEvent shown = LiveEvent.None;
        int shownId = -1;
        Vector3 normalGravity;
        bool gravityChanged;

        // Banner when an event starts or ends.
        Transform banner;
        TextMeshPro bannerText;
        float bannerUntil;

        // Disco.
        Transform disco;
        TextMeshPro lyricNow, lyricNext;
        readonly List<Light> lights = new List<Light>();

        // Last Kart Standing ring.
        Transform ring;
        readonly List<Transform> ringPosts = new List<Transform>();
        string shownWinner = "";

        // Ball rain.
        readonly List<GameObject> balls = new List<GameObject>();
        float nextBall;

        void Start() { normalGravity = Physics.gravity; }

        void Update()
        {
            var ev = EventManager.Instance;
            var now = EventManager.Running;
            int id = ev != null && ev.IsReady ? ev.EventId : -1;
            if (now != shown || (now != LiveEvent.None && id != shownId))
            {
                if (now != LiveEvent.None) Banner("<size=55%>LIVE EVENT!</size>\n<b>" + EventManager.Names[(int)now].ToUpper() + "</b>\n<size=45%>" + Hint(now) + "</size>");
                else if (shown != LiveEvent.None) Banner("<size=70%>" + EventManager.Names[(int)shown] + " is over!</size>");
                shown = now;
                shownId = id;
                shownWinner = "";
                AudioManager.Play(now != LiveEvent.None ? AudioManager.Sfx.Finish : AudioManager.Sfx.Lap);
            }

            AudioManager.PartyMode = now == LiveEvent.DiscoSingAlong;
            Disco(now == LiveEvent.DiscoSingAlong);
            Gravity(now == LiveEvent.LowGravity);
            Ring(now == LiveEvent.LastKartStanding ? ev : null);
            BallRain(now == LiveEvent.BallRain);
            UpdateBanner();
        }

        static string Hint(LiveEvent e)
        {
            switch (e)
            {
                case LiveEvent.DiscoSingAlong: return "Sing along with the words in the sky!";
                case LiveEvent.LastKartStanding: return "Stay inside the red ring as it closes. Last kart in wins!";
                case LiveEvent.LowGravity: return "Everything floats. Hit a ramp!";
                case LiveEvent.TurboFrenzy: return "Everyone is super fast!";
                case LiveEvent.BallRain: return "Beach balls are falling from the sky!";
            }
            return "";
        }

        // ---------------- banner

        void Banner(string text)
        {
            if (banner == null)
            {
                banner = new GameObject("EventBanner").transform;
                DontDestroyOnLoad(banner.gameObject);
                bannerText = KartUI.Label(banner, "", Vector3.zero, 1.2f, new Color(1f, 0.9f, 0.3f), new Vector2(3f, 1.2f));
            }
            bannerText.text = text;
            bannerUntil = Time.time + 5f;
            banner.gameObject.SetActive(true);
        }

        void UpdateBanner()
        {
            if (banner == null) return;
            bool show = Time.time < bannerUntil;
            banner.gameObject.SetActive(show);
            var rig = KartRig.Instance;
            if (!show || rig == null || rig.head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            banner.position = rig.head.position + fwd * 2.5f + Vector3.up * 0.6f;
            banner.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // ---------------- disco sing-along

        void Disco(bool on)
        {
            var rig = KartRig.Instance;
            if (!on || rig == null || rig.head == null)
            {
                if (disco != null) disco.gameObject.SetActive(false);
                return;
            }
            if (disco == null)
            {
                disco = new GameObject("Disco").transform;
                DontDestroyOnLoad(disco.gameObject);
                var ball = KartUI.Part(disco, PrimitiveType.Sphere, new Vector3(0f, 3.2f, 0f), Vector3.one * 0.8f, KartUI.Mat(new Color(0.85f, 0.85f, 0.95f), 0.95f, 1f));
                ball.name = "DiscoBall";
                Color[] colors = { new Color(1f, 0.2f, 0.6f), new Color(0.2f, 0.8f, 1f), new Color(1f, 0.85f, 0.2f) };
                foreach (var c in colors)
                {
                    var l = new GameObject("DiscoLight").AddComponent<Light>();
                    l.transform.SetParent(disco, false);
                    l.type = LightType.Point;
                    l.color = c;
                    l.range = 14f;
                    l.intensity = 2.5f;
                    lights.Add(l);
                }
                lyricNow = KartUI.Label(disco, "", Vector3.zero, 1.1f, new Color(1f, 0.95f, 0.4f), new Vector2(4f, 0.5f));
                lyricNext = KartUI.Label(disco, "", Vector3.zero, 0.7f, new Color(1f, 1f, 1f, 0.55f), new Vector2(4f, 0.4f));
            }
            disco.gameObject.SetActive(true);
            disco.position = rig.head.position;
            for (int i = 0; i < lights.Count; i++)
            {
                float a = Time.time * 1.6f + i * Mathf.PI * 2f / lights.Count;
                lights[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 4f, 2.5f, Mathf.Sin(a) * 4f);
            }
            disco.Find("DiscoBall")?.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

            // Lyrics in front of you, in time with the song.
            int line = Mathf.FloorToInt(AudioManager.MusicTime / AudioManager.PartyBar) % Lyrics.Length;
            lyricNow.text = "<b>" + Lyrics[line] + "</b>";
            lyricNext.text = Lyrics[(line + 1) % Lyrics.Length];
            Vector3 fwd = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up).normalized;
            var face = Quaternion.LookRotation(fwd, Vector3.up);
            lyricNow.transform.SetPositionAndRotation(rig.head.position + fwd * 3f + Vector3.up * 1.1f, face);
            lyricNext.transform.SetPositionAndRotation(rig.head.position + fwd * 3f + Vector3.up * 0.65f, face);
        }

        // ---------------- low gravity

        void Gravity(bool low)
        {
            if (low && !gravityChanged) { Physics.gravity = normalGravity * 0.3f; gravityChanged = true; }
            else if (!low && gravityChanged) { Physics.gravity = normalGravity; gravityChanged = false; }
        }

        // ---------------- last kart standing

        void Ring(EventManager ev)
        {
            if (ev == null)
            {
                if (ring != null) ring.gameObject.SetActive(false);
                return;
            }
            if (ring == null)
            {
                ring = new GameObject("EventRing").transform;
                DontDestroyOnLoad(ring.gameObject);
                var glow = KartUI.Glow(new Color(1f, 0.15f, 0.1f));
                for (int i = 0; i < 64; i++) ringPosts.Add(KartUI.Part(ring, PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 7f, 0.5f), glow));
            }
            ring.gameObject.SetActive(true);
            float r = ev.RingRadius;
            for (int i = 0; i < ringPosts.Count; i++)
            {
                float a = i * Mathf.PI * 2f / ringPosts.Count + Time.time * 0.2f;
                ringPosts[i].position = ev.Center + new Vector3(Mathf.Cos(a) * r, 3.5f, Mathf.Sin(a) * r);
                ringPosts[i].localScale = new Vector3(Mathf.Max(0.5f, r * 0.04f), 7f, 0.5f);
                ringPosts[i].rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            string winner = ev.Winner.ToString();
            if (winner.Length > 0 && winner != shownWinner)
            {
                shownWinner = winner;
                Banner("<size=55%>LAST KART STANDING</size>\n<b>" + (winner == "Nobody" ? "Nobody survived!" : winner.ToUpper() + " WINS!") + "</b>");
                AudioManager.Play(AudioManager.Sfx.Finish);
            }
            // A warning when you're near the edge.
            var kart = KartController.Local;
            if (kart != null && kart.IsReady && !kart.Eliminated && winner.Length == 0)
            {
                Vector3 d = kart.transform.position - ev.Center;
                d.y = 0f;
                if (d.magnitude > r - 8f && Time.frameCount % 60 == 0) SessionManager.Instance?.SetStatus("The ring is closing! Get to the middle!", 1.5f);
            }
        }

        // ---------------- ball rain

        void BallRain(bool on)
        {
            balls.RemoveAll(b => b == null);
            var rig = KartRig.Instance;
            if (!on || rig == null || rig.head == null || Time.time < nextBall || balls.Count >= 50) return;
            nextBall = Time.time + 0.2f;
            Vector2 offset = Random.insideUnitCircle * 25f;
            Vector3 at = rig.head.position + new Vector3(offset.x, 30f, offset.y);
            var ball = KartUI.Part(null, PrimitiveType.Sphere, at, Vector3.one * Random.Range(0.8f, 1.8f),
                KartUI.Mat(Color.HSVToRGB(Random.value, 0.7f, 1f), 0.7f)).gameObject;
            var col = ball.AddComponent<SphereCollider>();
            col.sharedMaterial = new PhysicsMaterial("Ball") { bounciness = 0.8f, bounceCombine = PhysicsMaterialCombine.Maximum };
            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.linearDamping = 0.1f;
            balls.Add(ball);
            Destroy(ball, 12f);
        }
    }
}
