using UnityEngine;

namespace Flopzone
{
    /// <summary>
    /// An arcade cabinet that plays "Flappy Flop": tap the big button (or pull
    /// the trigger with your hand near the controls) to flap through the pipes.
    /// The game is drawn pixel by pixel into a small texture.
    /// </summary>
    public class ArcadeMachine : MonoBehaviour
    {
        const int W = 72, H = 96, Ground = 10;
        const float Gap = 0.3f, PipeW = 0.16f, Speed = 0.32f, Gravity = -2.3f, FlapV = 0.78f;

        Texture2D tex;
        Color32[] px, backdrop;
        Transform button;
        TextMesh scoreText;
        float birdY = 0.55f, vy, deadAt, nextDraw;
        readonly float[] pipeX = new float[3], pipeGap = new float[3];
        bool[] scored = new bool[3];
        int score, best;
        string state = "ready";
        bool[] handWasIn = new bool[2];
        float pressAnim;

        /// <summary>Build one standing at pos; players stand on its local -z side.</summary>
        public static ArcadeMachine Create(Transform parent, Vector3 pos, float yaw, string colorHex)
        {
            var root = Prims.Empty("arcade", parent, pos);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var a = root.AddComponent<ArcadeMachine>();
            var t = root.transform;
            var shell = Prims.Lit(Prims.Hex(colorHex), 0.55f, 0.1f, Surface.Plastic);
            var black = Prims.Lit(new Color(0.05f, 0.05f, 0.06f), 0.5f, 0f, Surface.Plastic);
            var trim = Prims.Emissive(Color.Lerp(Prims.Hex(colorHex), Color.white, 0.4f), 2f);
            // cabinet body, the angled screen bezel, control deck and marquee
            Prims.Box(t, new Vector3(0, 0.45f, 0.05f), new Vector3(0.72f, 0.9f, 0.6f), shell);
            Prims.Box(t, new Vector3(0, 1.35f, 0.12f), new Vector3(0.72f, 0.9f, 0.46f), shell);
            Prims.Box(t, new Vector3(0, 1.9f, 0.05f), new Vector3(0.72f, 0.24f, 0.6f), shell);
            foreach (float x in new[] { -0.365f, 0.365f })
            {
                Prims.Box(t, new Vector3(x, 1.0f, 0.05f), new Vector3(0.03f, 2.0f, 0.62f), black, false);
                Prims.Box(t, new Vector3(x * 1.03f, 1.0f, -0.255f), new Vector3(0.012f, 2.0f, 0.012f), trim, false);
            }
            Prims.Box(t, new Vector3(0, 0.95f, -0.12f), new Vector3(0.7f, 0.08f, 0.36f), black, true, Quaternion.Euler(-12, 0, 0));
            Prims.Box(t, new Vector3(0, 1.36f, -0.155f), new Vector3(0.62f, 0.62f, 0.02f), black, false, Quaternion.Euler(8, 0, 0));
            var marquee = Prims.Box(t, new Vector3(0, 1.9f, -0.255f), new Vector3(0.66f, 0.16f, 0.01f), Prims.Emissive(Prims.Hex("ffd34d"), 1.4f), false);
            marquee.name = "marquee";
            var title = UIKit.Text(t, "FLAPPY FLOP", new Vector3(0, 1.9f, -0.262f), 0.07f, new Color(0.3f, 0.12f, 0.02f));
            title.fontStyle = FontStyle.Bold;
            // the screen
            a.tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "fz_arcade" };
            a.px = new Color32[W * H];
            a.backdrop = Backdrop();
            var mat = Prims.Sprite(Color.white);
            mat.mainTexture = a.tex;
            mat.renderQueue = 2990;
            var screen = Prims.Make(PrimitiveType.Quad, t, new Vector3(0, 1.365f, -0.168f), new Vector3(0.42f, 0.56f, 1), mat, false, Quaternion.Euler(8, 0, 0), "screen");
            screen.layer = Layers.Default;
            a.scoreText = UIKit.Text(t, "", new Vector3(0, 1.72f, -0.2f), 0.035f, Color.white);
            // the big flap button
            var deck = Prims.Empty("deck", t, new Vector3(0, 0.995f, -0.16f)).transform;
            deck.localRotation = Quaternion.Euler(-12, 0, 0);
            Prims.Cyl(deck, Vector3.zero, 0.13f, 0.02f, black, Vector3.up);
            a.button = Prims.Cyl(deck, new Vector3(0, 0.025f, 0), 0.1f, 0.035f, Prims.Emissive(Prims.Hex("ff3b30"), 1.6f), Vector3.up).transform;
            var hint = UIKit.Text(deck, "FLAP", new Vector3(0, 0.003f, -0.11f), 0.025f, Color.white);
            hint.transform.localRotation = Quaternion.Euler(90, 0, 0);
            a.best = PlayerPrefs.GetInt("fz_flappy_best", 0);
            a.ResetGame();
            a.Draw();
            return a;
        }

        static Color32[] Backdrop()
        {
            var b = new Color32[W * H];
            var top = new Color(0.32f, 0.72f, 0.95f);
            var bottom = new Color(0.72f, 0.92f, 1f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = Color.Lerp(bottom, top, (float)y / H);
                    // a skyline of bushes and clouds
                    float hill = Ground + 7 + 3 * Mathf.Sin(x * 0.35f) + 2 * Mathf.Sin(x * 0.9f + 1);
                    if (y < hill) c = new Color(0.45f, 0.8f, 0.42f);
                    float cl = Mathf.Sin(x * 0.2f) * 3 + 70;
                    if (y > cl && y < cl + 5 + 2 * Mathf.Sin(x * 0.7f)) c = Color.Lerp(c, Color.white, 0.85f);
                    if (y < Ground) c = (x + y) % 6 < 3 ? new Color(0.85f, 0.72f, 0.42f) : new Color(0.78f, 0.64f, 0.36f);
                    if (y == Ground) c = new Color(0.4f, 0.75f, 0.3f);
                    b[y * W + x] = c;
                }
            return b;
        }

        void ResetGame()
        {
            birdY = 0.55f; vy = 0; score = 0; state = "ready";
            for (int i = 0; i < 3; i++) { pipeX[i] = 1.2f + i * 0.6f; pipeGap[i] = Random.Range(0.3f, 0.75f); scored[i] = false; }
        }

        void Flap()
        {
            pressAnim = 1f;
            if (state == "dead")
            {
                if (Time.time - deadAt < 0.7f) return;
                ResetGame();
            }
            if (state == "ready") state = "playing";
            vy = FlapV;
            AudioManager.Sfx("jump", transform.position + Vector3.up * 1.2f, 0.35f, 1.6f);
        }

        void Update()
        {
            var rig = PlayerRig.I;
            if (rig == null) return;
            float dist = Vector3.Distance(rig.head.position, transform.position + Vector3.up * 1.3f);
            if (dist > 8f) return;   // nobody's watching: pause
            ReadInput(rig, dist);
            Step(Mathf.Min(Time.deltaTime, 0.05f));
            pressAnim = Mathf.MoveTowards(pressAnim, 0, Time.deltaTime * 8f);
            button.localPosition = new Vector3(0, 0.025f - pressAnim * 0.012f, 0);
            if (Time.time >= nextDraw) { nextDraw = Time.time + 1f / 30f; Draw(); }
        }

        void ReadInput(PlayerRig rig, float dist)
        {
            var input = VRInput.I;
            for (int h = 0; h < 2; h++)
            {
                var hand = rig.hands[h].transform.position;
                bool inside = Vector3.Distance(hand, button.position) < 0.09f;
                if (inside && !handWasIn[h]) { Flap(); if (input) input.Haptic(h, 0.4f, 0.04f); }
                handWasIn[h] = inside;
                // or pull the trigger with a free hand over the controls
                if (input && input.TriggerDown(h) && rig.hands[h].held == null && Vector3.Distance(hand, button.position) < 0.35f) Flap();
            }
            if (input && !input.XRActive && dist < 1.6f && input.TriggerDown(1)) Flap();
        }

        void Step(float dt)
        {
            if (state == "ready") { birdY = 0.55f + Mathf.Sin(Time.time * 4f) * 0.02f; return; }
            if (state == "dead")
            {
                if (birdY > Ground / (float)H) { vy += Gravity * dt; birdY = Mathf.Max(Ground / (float)H, birdY + vy * dt); }
                return;
            }
            vy += Gravity * dt;
            birdY += vy * dt;
            for (int i = 0; i < 3; i++)
            {
                pipeX[i] -= Speed * dt;
                if (pipeX[i] < -PipeW)
                {
                    int far = 0;
                    for (int j = 1; j < 3; j++) if (pipeX[j] > pipeX[far]) far = j;
                    pipeX[i] = pipeX[far] + 0.6f;
                    pipeGap[i] = Random.Range(0.3f, 0.75f);
                    scored[i] = false;
                }
                const float birdX = 0.3f, r = 0.035f;
                if (!scored[i] && pipeX[i] + PipeW < birdX)
                {
                    scored[i] = true; score++;
                    AudioManager.Sfx("beep", transform.position + Vector3.up * 1.4f, 0.3f, 1.4f);
                }
                if (birdX + r > pipeX[i] && birdX - r < pipeX[i] + PipeW &&
                    (birdY - r * 0.75f < pipeGap[i] - Gap / 2 || birdY + r * 0.75f > pipeGap[i] + Gap / 2)) Die();
            }
            if (birdY < (Ground + 2) / (float)H || birdY > 1.05f) Die();
        }

        void Die()
        {
            if (state != "playing") return;
            state = "dead";
            deadAt = Time.time;
            vy = Mathf.Min(vy, 0.2f);
            AudioManager.Sfx("hit_body", transform.position + Vector3.up * 1.3f, 0.4f, 1.3f);
            if (score > best) { best = score; PlayerPrefs.SetInt("fz_flappy_best", best); PlayerPrefs.Save(); }
        }

        void Draw()
        {
            System.Array.Copy(backdrop, px, px.Length);
            var pipe = new Color32(92, 190, 60, 255);
            var pipeDark = new Color32(40, 110, 30, 255);
            var pipeLight = new Color32(150, 230, 110, 255);
            for (int i = 0; i < 3; i++)
            {
                int x0 = Mathf.RoundToInt(pipeX[i] * W), x1 = Mathf.RoundToInt((pipeX[i] + PipeW) * W);
                int gy0 = Mathf.RoundToInt((pipeGap[i] - Gap / 2) * H), gy1 = Mathf.RoundToInt((pipeGap[i] + Gap / 2) * H);
                for (int x = Mathf.Max(0, x0 - 1); x < Mathf.Min(W, x1 + 1); x++)
                    for (int y = Ground + 1; y < H; y++)
                    {
                        if (y >= gy0 && y < gy1) continue;
                        bool lip = (y >= gy0 - 4 && y < gy0) || (y >= gy1 && y < gy1 + 4);
                        if (!lip && (x < x0 || x >= x1)) continue;
                        bool edge = x == x0 - 1 || x == x1 || x == x0 || x == x1 - 1 || y == gy0 - 4 || y == gy1 + 3;
                        px[y * W + x] = edge ? pipeDark : (x - x0 < 3 ? pipeLight : pipe);
                    }
            }
            // the bird: a yellow ball with an eye, a beak and a flapping wing
            int bx = Mathf.RoundToInt(0.3f * W), by = Mathf.RoundToInt(birdY * H);
            float tilt = Mathf.Clamp(vy * 3f, -3f, 3f);
            for (int dy = -5; dy <= 5; dy++)
                for (int dx = -6; dx <= 7; dx++)
                {
                    int x = bx + dx, y = by + dy;
                    if (x < 0 || x >= W || y < 0 || y >= H) continue;
                    float ex = dx / 6f, ey = (dy - dx * tilt * 0.08f) / 5f;
                    float d = ex * ex + ey * ey;
                    Color32? c = null;
                    if (d < 1f) c = d > 0.7f ? new Color32(120, 70, 10, 255) : new Color32(255, 214, 40, 255);
                    if (dx >= 1 && dx <= 3 && dy >= 1 && dy <= 3) c = (dx == 3 && dy == 2) ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
                    if (dx >= 5 && dx <= 7 && dy >= -2 && dy <= 0) c = new Color32(255, 110, 20, 255);
                    bool wingUp = state != "dead" && (Time.time * 8f) % 2f < 1f;
                    if (dx >= -4 && dx <= -1 && (wingUp ? dy >= 0 && dy <= 2 : dy >= -2 && dy <= 0)) c = new Color32(250, 245, 220, 255);
                    if (c.HasValue) px[y * W + x] = c.Value;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            scoreText.text = state == "ready" ? "PRESS FLAP!\n<size=20>BEST " + best + "</size>"
                : state == "dead" ? "GAME OVER  " + score + "\n<size=20>BEST " + best + "</size>"
                : score.ToString();
        }
    }
}
