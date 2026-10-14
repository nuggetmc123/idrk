using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomChaos
{
    /* =====================================================================
       KINGDOM CHAOS: first playable slice, Unity port.
       One hero (Knight), three weapons, World 1 (four stages + King Slime),
       two pets, XP / levels / coins / shop, saved with PlayerPrefs.

       Just press Play: AutoStart adds the game to whatever scene is open if it
       isn't there already (Tools > Kingdom Chaos > Create Game Scene also makes a
       dedicated scene). It sets up its own camera, draws everything from code
       and needs no other assets.

       The simulation works in "canvas pixels", the same units as the browser
       version: the screen is 960 x 540, x runs along the level, y is depth in
       the walking lane (300..505, bigger = closer) and z is height above the
       ground. A thing is drawn at screen (x - camX, y - z).
       ===================================================================== */
    public class KingdomChaosGame : MonoBehaviour
    {
        [Tooltip("Optional. A material whose shader uses vertex colours (Sprites/Default works). Created automatically if left empty.")]
        public Material material;

        const float W = 960, H = 540, GROUND_TOP = 300, GROUND_BOT = 505, G = 1900, TAU = Mathf.PI * 2;
        const string SAVE_KEY = "kingdomChaos.save.v1";

        /* ---------------------------------------------------------------- runtime types */

        class Ent
        {
            public string type, state = "move", last;
            public float x, y, z, vx, vy, vz, kbx, hp, maxHp, w, h, d, spd;
            public int face = -1, id, slash, hops, waves;
            public float t, cd, hurtT, flash, slowT, burnT, burnDps, burnAcc, deadT, shieldT, offY, walkT, turnT, hopDelay, landDmg, landR, wobble;
            public bool dead, entering, boss, mini, armor, prop, shield, shieldUp, hopHit, didHit, r1, r2, ph2;
        }

        class Atk
        {
            public string kind, phase = "su";
            public int step;
            public AtkData d;
            public float t, rt, tick;
            public readonly HashSet<int> hit = new HashSet<int>();
            public bool fired, queued, landed, swung;
        }

        class Roll { public float t, dy; public int dx; }

        class Player
        {
            public float x = 120, y = 410, z, vz, hp, maxHp, inv, rollCd, hurtT, kbx, specialCd, deadT, walkT, comboT;
            public int face = 1, jumps, airHits, combo;
            public Atk atk;
            public Roll roll;
            public bool dead, moving;
        }

        class Pet
        {
            public PetDef def;
            public float x, y, z, cd, fury, furyT, bob;
            public int furyShots;
        }

        class Proj
        {
            public string owner, kind;
            public float x, y, z, vx, vy, dmg, slow, life, burnDps, burnT;
            public int pierce, target;
            public bool breaks, big, pet;
            public readonly HashSet<int> hit = new HashSet<int>();
        }

        class Pickup
        {
            public string kind;
            public int value;
            public float x, y, z, vx, vy, vz, age, openT;
            public bool dead, opened;
        }

        class Particle { public float x, y, z, vx, vy, vz, life, max, size, grav; public Color color; }
        class FText { public float x, y, z, life, max; public string text; public Color color; public int size; }

        class HitOpts
        {
            public float fromX = float.NaN, kb, launch, stun, pop, shake, slow, burnDps, burnT;
            public bool breaks, big;
        }

        class Level
        {
            public int waveIdx;
            public bool locked, ended, bossKilled;
            public float lockX, spawnT, go, endT, bossIntro;
            public readonly List<string> queue = new List<string>();
            public readonly List<Vector2> dps = new List<Vector2>();   // x = time, y = damage
        }

        class Run { public int coins, xp, kills, maxCombo; public float time; }

        struct GText
        {
            public string s;
            public float x, y, alpha;
            public int size, align;
            public Color color;
            public bool outline;
        }

        /* ---------------------------------------------------------------- state */

        string state = "title";     // title | map | play | paused | result
        StageDef stageDef;
        int stageIdx = -1;
        Player player;
        Pet pet;
        List<Ent> enemies = new List<Ent>();
        List<Proj> projectiles = new List<Proj>();
        List<Pickup> pickups = new List<Pickup>();
        List<Particle> particles = new List<Particle>();
        List<FText> texts = new List<FText>();
        Level level;
        Run run = new Run();
        float camX, shake, hitstop, banner, msgLife;
        string msg;
        int eid;
        SaveData save;

        readonly HashSet<Act> pressed = new HashSet<Act>();
        static readonly Act[] BufferedActs = { Act.Jump, Act.Dodge, Act.Light, Act.Heavy, Act.Special, Act.Interact, Act.Pet };

        KCPainter g;
        KCAudio sfx;
        Camera cam;
        GameObject meshGo;
        readonly List<GText> gtexts = new List<GText>();
        float shakeX, shakeY;

        /* ---------------------------------------------------------------- helpers */

        static float Rand(float a, float b) => a + UnityEngine.Random.value * (b - a);
        static float Chance => UnityEngine.Random.value;
        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static int Sign(float v) => v < 0 ? -1 : 1;
        static float Hash(float n) { double s = Math.Sin(n * 127.1 + 311.7) * 43758.5453; return (float)(s - Math.Floor(s)); }
        static Color C(string hex) => KCPainter.Hex(hex);
        static Color CA(string hex, float a) { var c = KCPainter.Hex(hex); c.a = a; return c; }
        static readonly Color OUT = KCPainter.Outline;

        float DmgMult => 1 + (save.level - 1) * 0.06f;
        WeaponDef Weapon => KCData.Weapon(save.weapon);
        bool Tapped(Act a) => pressed.Contains(a);

        /* ---------------------------------------------------------------- unity lifecycle */

        static KingdomChaosGame instance;

        // Starts the game when Play is pressed in any scene that doesn't already have it, so a
        // fresh project needs no setup beyond installing the package.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (instance == null) new GameObject("Kingdom Chaos").AddComponent<KingdomChaosGame>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { enabled = false; Destroy(gameObject); return; }
            instance = this;
            save = LoadSave();
            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cg.AddComponent<Camera>();
                cg.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.transform.SetPositionAndRotation(new Vector3(W / 2, -H / 2, -10), Quaternion.identity);
            cam.nearClipPlane = .1f; cam.farClipPlane = 100;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            if (material == null)
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                material = new Material(sh);
            }
            g = new KCPainter();
            meshGo = new GameObject("Kingdom Chaos Canvas");
            meshGo.transform.SetParent(transform, false);
            meshGo.AddComponent<MeshFilter>().sharedMesh = g.Mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sfx = new KCAudio(src);
            Application.targetFrameRate = Mathf.Max(Application.targetFrameRate, 60);
        }

        void OnApplicationPause(bool paused) { if (paused) WriteSave(); }
        void OnApplicationQuit() { WriteSave(); }

        void Update()
        {
            if (KCInput.Pressed(Act.Mute)) { sfx.Muted = !sfx.Muted; FlashText(sfx.Muted ? "Sound off" : "Sound on"); }
            if (KCInput.Pressed(Act.Pause))
            {
                if (state == "play") PauseGame();
                else if (state == "paused") ResumeGame();
            }
            if (KCInput.Pressed(Act.Confirm) && state == "title") OpenMap();
            if (state == "play")
                foreach (var a in BufferedActs) if (KCInput.Pressed(a)) pressed.Add(a);

            Step(Mathf.Min(.033f, Time.deltaTime));
        }

        void LateUpdate()
        {
            float aspect = cam.aspect > 0 ? cam.aspect : 16f / 9f;
            cam.orthographicSize = Mathf.Max(H / 2, (W / 2) / aspect);
            cam.transform.position = new Vector3(W / 2, -H / 2, -10);
            gtexts.Clear();
            g.Begin();
            Render();
            // letterbox anything outside the 960 x 540 play area
            g.Rect(-4000, -4000, 4000, 8540, Color.black);
            g.Rect(W, -4000, 4000, 8540, Color.black);
            g.Rect(0, -4000, W, 4000, Color.black);
            g.Rect(0, H, W, 4000, Color.black);
            g.End();
        }

        /* ---------------------------------------------------------------- save */

        SaveData LoadSave()
        {
            try
            {
                var json = PlayerPrefs.GetString(SAVE_KEY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var s = JsonUtility.FromJson<SaveData>(json);
                    if (s != null && s.v == 1)
                    {
                        if (s.weapons == null || s.weapons.Count == 0) s.weapons = new List<string> { "iron" };
                        if (s.pets == null) s.pets = new List<string>();
                        if (s.cleared == null) s.cleared = new List<string>();
                        if (!s.weapons.Contains(s.weapon)) s.weapon = "iron";
                        if (s.pet == null) s.pet = "";
                        return s;
                    }
                }
            }
            catch (Exception) { }
            return new SaveData();
        }

        void WriteSave()
        {
            if (save == null) return;
            PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        /* ---------------------------------------------------------------- stage setup */

        void StartStage(StageDef def, int idx)
        {
            stageDef = def; stageIdx = idx;
            float hp = KCData.MaxHpFor(save.level);
            player = new Player { hp = hp, maxHp = hp };
            enemies.Clear(); projectiles.Clear(); pickups.Clear(); particles.Clear(); texts.Clear();
            var pd = KCData.Pet(save.pet);
            pet = pd != null ? new Pet { def = pd, x = 60, y = 400, z = 40, cd = 2 } : null;
            level = new Level();
            camX = 0; shake = 0; hitstop = 0; run = new Run(); banner = 2.4f;
            foreach (var x in def.crates) SpawnEnemy("crate", x, Rand(340, 470));
            if (def.training)
            {
                SpawnEnemy("dummy", 520, 360); SpawnEnemy("dummy", 660, 430); SpawnEnemy("dummy", 800, 380);
            }
            state = "play";
            pressed.Clear();
        }

        Ent SpawnEnemy(string type, float x, float y, float z = 0, string st = "move", bool entering = false)
        {
            var D = KCData.Enemies[type];
            var e = new Ent
            {
                type = type, x = x, y = y, z = z, hp = D.hp, maxHp = D.hp, w = D.w, h = D.h, d = D.d, spd = D.spd,
                state = st, cd = Rand(.4f, 1.4f), entering = entering,
                boss = D.boss, mini = D.mini, armor = D.armor, prop = D.prop, shield = D.shield, shieldUp = D.shield,
                offY = Rand(-70, 70), walkT = Rand(0, 10), id = ++eid,
            };
            enemies.Add(e);
            return e;
        }

        /* ---------------------------------------------------------------- fx */

        void AddText(float x, float y, float z, string text, Color color, int size = 18)
            => texts.Add(new FText { x = x, y = y, z = z, text = text, color = color, size = size, life = .9f, max = .9f });

        void Burst(float x, float y, float z, int n, Color color, float spd = 220, float size = 4, float grav = 1)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rand(0, TAU), s = Rand(spd * .3f, spd);
                particles.Add(new Particle
                {
                    x = x, y = y, z = z, vx = Mathf.Cos(a) * s, vy = Mathf.Sin(a) * s * .3f, vz = Rand(50, spd),
                    life = Rand(.3f, .7f), max = .7f, color = color, size = Rand(size * .6f, size * 1.3f), grav = grav,
                });
            }
        }

        void Ring(float x, float y, int n, Color color, float spd = 300)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * TAU;
                particles.Add(new Particle { x = x, y = y, z = 4, vx = Mathf.Cos(a) * spd, vy = Mathf.Sin(a) * spd * .35f, vz = 60, life = .45f, max = .45f, color = color, size = 6, grav = .3f });
            }
        }

        void Puff(float x, float y, float z, float vx, float vz, float life, Color color, float size, float grav)
            => particles.Add(new Particle { x = x, y = y, z = z, vx = vx, vz = vz, life = life, max = life, color = color, size = size, grav = grav });

        void FlashText(string t) { msg = t; msgLife = 1.6f; }

        /* ---------------------------------------------------------------- progression */

        void GainXp(int n)
        {
            save.xp += n; run.xp += n;
            while (save.xp >= KCData.XpNeed(save.level))
            {
                save.xp -= KCData.XpNeed(save.level);
                save.level++;
                var P = player;
                if (P != null)
                {
                    P.maxHp = KCData.MaxHpFor(save.level);
                    P.hp = Mathf.Min(P.maxHp, P.hp + 30);
                    AddText(P.x, P.y, P.z + 110, "LEVEL UP!", C("#ffcc4d"), 28);
                    Ring(P.x, P.y, 24, C("#ffcc4d"), 260);
                }
                sfx.Play("level");
            }
        }

        void GainCoins(int n) { save.coins += n; run.coins += n; }

        void DropCoins(float x, float y, int total, int max = 12)
        {
            int n = Mathf.Min(max, Mathf.Max(1, Mathf.CeilToInt(total / 4f)));
            int left = total;
            for (int i = 0; i < n; i++)
            {
                int v = i == n - 1 ? left : Mathf.Max(1, total / n);
                left -= v;
                if (v <= 0) continue;
                pickups.Add(new Pickup { kind = "coin", value = v, x = x, y = y + Rand(-6, 6), z = 20, vx = Rand(-160, 160), vy = Rand(-40, 40), vz = Rand(250, 480) });
            }
        }

        /* ---------------------------------------------------------------- combat */

        // returns 0 = no effect, 1 = hit, 2 = blocked
        int DamageEnemy(Ent e, float dmg, HitOpts o)
        {
            if (e.dead) return 0;
            var P = player;
            int fromDir = Sign((float.IsNaN(o.fromX) ? P.x : o.fromX) - e.x);   // side the hit came from
            if (e.type == "crate")
            {
                e.dead = true; e.deadT = 1;
                Burst(e.x, e.y, 20, 14, C("#a0703c"), 260, 5);
                sfx.Play("brk");
                if (Chance < .55f) pickups.Add(new Pickup { kind = "food", x = e.x, y = e.y, z = 10, vz = 260 });
                else DropCoins(e.x, e.y, 8);
                return 1;
            }
            if (e.type == "dummy")
            {
                e.flash = .1f; e.wobble = 1;
                AddText(e.x + Rand(-10, 10), e.y, e.h + 10, Mathf.RoundToInt(dmg).ToString(), Color.white);
                level.dps.Add(new Vector2(run.time, dmg));
                return 1;
            }
            if (e.shield && e.shieldUp && fromDir == e.face)
            {
                if (o.breaks)
                {
                    e.shieldUp = false; e.shieldT = 3.2f; e.hurtT = .9f; e.state = "move";
                    AddText(e.x, e.y, e.h + 20, "SHIELD BREAK!", C("#ffcc4d"), 22);
                    Burst(e.x + e.face * 18, e.y, 40, 16, C("#c0c8d4"), 280, 4);
                    sfx.Play("brk"); shake = Mathf.Max(shake, 7);
                }
                else
                {
                    dmg *= .15f;
                    e.hp -= dmg; e.flash = .05f;
                    AddText(e.x, e.y, e.h + 14, "BLOCK", C("#9fb4ff"), 16);
                    Burst(e.x + e.face * 18, e.y, 44, 6, Color.white, 180, 3);
                    sfx.Play("block");
                    if (e.hp <= 0) KillEnemy(e, fromDir);
                    return 2;
                }
            }
            e.hp -= dmg; e.flash = .12f;
            AddText(e.x + Rand(-10, 10), e.y, e.z + e.h + 8, Mathf.RoundToInt(dmg).ToString(), Color.white, o.big ? 24 : 18);
            bool slimy = e.type.Contains("slime") || e.type == "king";
            Burst(e.x, e.y, e.z + e.h * .55f, o.big ? 10 : 5, slimy ? C("#7df07a") : C("#ffd9a0"), 220, 4);
            if (o.slow > 0)
            {
                if (e.slowT <= 0) AddText(e.x, e.y, e.z + e.h + 26, "SLOWED", C("#9fe8ff"), 13);
                e.slowT = Mathf.Max(e.slowT, o.slow);
            }
            if (o.burnT > 0) { e.burnT = o.burnT; e.burnDps = o.burnDps; }

            bool armored = e.boss || ((e.armor || e.state == "charge") && !o.breaks && !(e.shield && !e.shieldUp));
            if (!armored)
            {
                if (e.type != "slime" && e.type != "minislime") e.state = "move";
                e.hurtT = Mathf.Max(e.hurtT, o.stun > 0 ? o.stun : .3f);
                e.kbx = -fromDir * (o.kb > 0 ? o.kb : 90) * (e.type == "boar" ? .6f : 1);
                if (o.launch > 0) e.vz = Mathf.Max(e.vz, o.launch);
                else if (e.z > 0 && o.pop > 0) e.vz = Mathf.Max(e.vz, o.pop);
                if (o.stun > .6f) AddText(e.x, e.y, e.z + e.h + 26, "STUNNED", C("#ffe36e"), 14);
            }
            if (e.hp <= 0) KillEnemy(e, fromDir);
            return 1;
        }

        void KillEnemy(Ent e, int fromDir)
        {
            e.dead = true; e.deadT = 0; e.hp = 0;
            e.vz = e.boss ? 0 : 380; e.kbx = -fromDir * 220;
            var D = KCData.Enemies[e.type];
            GainXp(D.xp);
            DropCoins(e.x, e.y, D.coins, e.boss ? 20 : 12);
            AddText(e.x, e.y, e.h + 30, "+" + D.xp + " XP", C("#8fb4ff"), 14);
            run.kills++; save.kills++;
            bool slimy = e.type.Contains("slime") || e.type == "king";
            Burst(e.x, e.y, e.h * .5f, e.boss ? 60 : 16, slimy ? C("#6fe06a") : C("#7cc24a"), e.boss ? 500 : 280, e.boss ? 7 : 5);
            sfx.Play(e.boss || e.mini ? "boom" : "die");
            if (e.boss || e.mini) { shake = 16; hitstop = .25f; }
            if (e.mini) pickups.Add(new Pickup { kind = "chest", x = e.x, y = e.y, z = 60, vz = 200 });
            if (e.boss)
            {
                level.bossKilled = true;
                foreach (var m in enemies.ToArray()) if (!m.dead && !m.prop) KillEnemy(m, 1);
            }
        }

        bool HurtPlayer(float dmg, float fromX, float kb = 160)
        {
            var P = player;
            if (P.dead || P.inv > 0 || P.roll != null || (P.atk != null && P.atk.kind == "special")) return false;
            P.hp -= dmg;
            P.inv = .75f; P.hurtT = .28f; P.atk = null;
            P.kbx = Sign(P.x - fromX) * kb;
            P.combo = 0;
            AddText(P.x, P.y, P.z + 92, "-" + Mathf.RoundToInt(dmg), C("#ff5a6e"), 20);
            Burst(P.x, P.y, P.z + 45, 8, C("#ff5a6e"), 220, 4);
            shake = Mathf.Max(shake, 6);
            sfx.Play("hurt");
            if (P.hp <= 0)
            {
                P.hp = 0; P.dead = true; P.deadT = 0; P.vz = 420;
                sfx.Play("die");
            }
            return true;
        }

        int PlayerHit(Ent e, float dmg, HitOpts o)
        {
            if (float.IsNaN(o.fromX)) o.fromX = player.x;
            int r = DamageEnemy(e, dmg * DmgMult, o);
            if (r != 0 && e.type != "crate")
            {
                var P = player;
                P.combo++; P.comboT = 1.4f;
                if (P.combo > run.maxCombo) run.maxCombo = P.combo;
                if (P.combo > save.bestCombo) save.bestCombo = P.combo;
            }
            if (r != 0)
            {
                hitstop = Mathf.Max(hitstop, o.big ? .085f : .045f);
                if (o.shake > 0) shake = Mathf.Max(shake, o.shake);
                sfx.Play(o.big ? "heavy" : "hit");
            }
            return r;
        }

        static bool InMelee(Player P, Ent e, float range)
        {
            float dx = (e.x - P.x) * P.face;
            return dx > -18 && dx < range + e.w && Mathf.Abs(e.y - P.y) < 28 + e.d && e.z < P.z + 78 && e.z + e.h > P.z + 4;
        }

        void StartAttack(string kind, int step = 0)
        {
            var P = player;
            var w = Weapon;
            AtkData d;
            if (kind == "light") d = w.light;
            else if (kind == "heavy") d = w.heavy;
            else if (kind == "air") d = w.kind == "ranged" ? w.light : new AtkData { dmg = w.light.dmg * .9f, su = .04f, ac = .1f, re = .12f, range = w.light.range > 0 ? w.light.range : 70, kb = 60, pop = 330 };
            else if (kind == "slam") d = new AtkData { su = 0, ac = 0, re = .28f };
            else d = new AtkData { su = .05f, ac = .6f, re = .15f };   // special
            P.atk = new Atk { kind = kind, step = step, d = d };
            if (kind == "slam") { P.vz = -1500; sfx.Play("swing"); }
            if (kind == "special") { P.specialCd = 8; sfx.Play("spin"); }
        }

        void FireArrows(Player P, AtkData d, string kind)
        {
            int n = Mathf.Max(1, d.spread);
            for (int i = 0; i < n; i++)
            {
                float off = n > 1 ? i - (n - 1) / 2f : 0;
                projectiles.Add(new Proj
                {
                    owner = "p", kind = "arrow", x = P.x + P.face * 30, y = P.y + off * 14, z = P.z + 44,
                    vx = P.face * d.speed, vy = off * 90, dmg = d.dmg, slow = d.slow, breaks = d.breaks,
                    pierce = d.pierce, life = 1.1f, big = kind == "heavy",
                });
            }
            sfx.Play("shoot");
        }

        void UpdateAttack(float dt)
        {
            var P = player;
            var a = P.atk;
            if (a == null) return;
            var w = Weapon;
            var d = a.d;
            a.t += dt;
            if (a.kind == "slam")
            {
                if (!a.landed) return;
                a.rt += dt;
                if (a.rt > d.re) P.atk = null;
                return;
            }
            float total = d.su + d.ac + d.re;
            a.phase = a.t < d.su ? "su" : a.t < d.su + d.ac ? "ac" : "re";

            if (w.kind == "ranged" && a.kind != "special")
            {
                if (!a.fired && a.t >= d.su) { a.fired = true; FireArrows(P, d, a.kind); }
            }
            else if (a.phase == "ac")
            {
                if (a.kind == "special")
                {
                    a.tick -= dt;
                    if (a.tick <= 0)
                    {
                        a.tick = .2f;
                        bool lastTick = a.t > d.su + d.ac - .2f;
                        foreach (var e in enemies.ToArray())
                        {
                            if (e.dead || e.entering) continue;
                            if (Mathf.Abs(e.x - P.x) < 118 + e.w && Mathf.Abs(e.y - P.y) < 50 + e.d && e.z < P.z + 80)
                                PlayerHit(e, 20, new HitOpts { kb = 170, launch = lastTick ? 300 : 0, breaks = true, big = lastTick });
                        }
                    }
                }
                else
                {
                    if (!a.swung) { a.swung = true; sfx.Play("swing"); }
                    bool finisher = a.kind == "light" && a.step == w.combo - 1 && w.combo > 1;
                    foreach (var e in enemies.ToArray())
                    {
                        if (e.dead || e.entering || a.hit.Contains(e.id)) continue;
                        if (!InMelee(P, e, d.range)) continue;
                        a.hit.Add(e.id);
                        var o = new HitOpts
                        {
                            kb = finisher ? 280 : d.kb, launch = d.launch, stun = d.stun, breaks = d.breaks, pop = d.pop,
                            big = a.kind == "heavy", shake = d.shake > 0 ? d.shake : (a.kind == "heavy" ? 4 : 0),
                        };
                        if (PlayerHit(e, d.dmg, o) != 0 && a.kind == "air" && P.airHits < 3) { P.vz = Mathf.Max(P.vz, 150); P.airHits++; }
                    }
                }
            }
            if (a.t >= total)
            {
                if (a.queued && a.kind == "light" && a.step + 1 < w.combo) StartAttack("light", a.step + 1);
                else if (a.queued && a.kind == "light" && w.kind == "ranged") StartAttack("light");
                else P.atk = null;
            }
        }

        void SlamImpact()
        {
            var P = player;
            shake = Mathf.Max(shake, 8);
            Ring(P.x, P.y, 22, C("#e8d6a8"), 380);
            sfx.Play("heavy");
            foreach (var e in enemies.ToArray())
            {
                if (e.dead || e.entering) continue;
                if (Mathf.Abs(e.x - P.x) < 125 + e.w && Mathf.Abs(e.y - P.y) < 55 + e.d && e.z < 50)
                    PlayerHit(e, 30, new HitOpts { kb = 260, launch = 280, stun = .5f, big = true });
            }
        }

        /* ---------------------------------------------------------------- player */

        static bool ZPhysics(ref float z, ref float vz, float dt, float grav = G)
        {
            if (z > 0 || vz > 0)
            {
                vz -= grav * dt;
                z += vz * dt;
                if (z <= 0) { z = 0; vz = 0; return true; }
            }
            return false;
        }

        void UpdatePlayer(float dt)
        {
            var P = player;
            P.inv = Mathf.Max(0, P.inv - dt);
            P.rollCd = Mathf.Max(0, P.rollCd - dt);
            P.specialCd = Mathf.Max(0, P.specialCd - dt);
            P.comboT -= dt; if (P.comboT <= 0) P.combo = 0;

            if (P.dead)
            {
                P.deadT += dt;
                ZPhysics(ref P.z, ref P.vz, dt);
                if (P.deadT > 1.6f && state == "play") EndStage(false);
                return;
            }

            int ix = (KCInput.Held(Act.Right) ? 1 : 0) - (KCInput.Held(Act.Left) ? 1 : 0);
            int iy = (KCInput.Held(Act.Down) ? 1 : 0) - (KCInput.Held(Act.Up) ? 1 : 0);
            bool grounded = P.z <= 0 && P.vz <= 0;
            bool down = KCInput.Held(Act.Down);
            bool light = Tapped(Act.Light), heavy = Tapped(Act.Heavy);
            P.moving = false;

            if (P.hurtT > 0)
            {
                P.hurtT -= dt;
                P.x += P.kbx * dt; P.kbx *= Mathf.Pow(.02f, dt);
            }
            else if (P.roll != null)
            {
                P.roll.t -= dt;
                P.x += P.roll.dx * 540 * dt; P.y += P.roll.dy * 240 * dt;
                if (P.roll.t <= 0) P.roll = null;
            }
            else
            {
                bool canCancel = P.atk == null || P.atk.phase == "re";
                if (Tapped(Act.Dodge) && P.rollCd <= 0 && grounded && canCancel)
                {
                    P.atk = null;
                    int dx = ix != 0 ? ix : P.face;
                    P.roll = new Roll { t = .36f, dx = dx, dy = iy * .7f };
                    P.face = dx; P.inv = Mathf.Max(P.inv, .36f); P.rollCd = .55f;
                    Burst(P.x, P.y, 4, 6, C("#d9c7a0"), 120, 4);
                    sfx.Play("roll");
                }
                else
                {
                    if (Tapped(Act.Jump) && (canCancel || (P.atk != null && P.atk.kind == "air")))
                    {
                        if (grounded) { P.vz = 650; P.jumps = 1; P.airHits = 0; P.atk = null; sfx.Play("jump"); }
                        else if (P.jumps < 2) { P.vz = 580; P.jumps = 2; P.atk = null; Burst(P.x, P.y, P.z, 8, Color.white, 120, 3); sfx.Play("jump"); }
                    }
                    bool inAir = !(P.z <= 0 && P.vz <= 0);
                    if ((light || heavy) && inAir && (down || heavy) && (P.atk == null || P.atk.kind == "air")) StartAttack("slam");
                    else if (light)
                    {
                        if (inAir) { if (P.atk == null) StartAttack("air"); }
                        else if (P.atk != null && P.atk.kind == "light") P.atk.queued = true;
                        else if (P.atk == null) StartAttack("light");
                    }
                    else if (heavy && P.atk == null && !inAir) StartAttack("heavy");
                    if (Tapped(Act.Special) && P.atk == null && P.specialCd <= 0) StartAttack("special");
                    else if (Tapped(Act.Special) && P.specialCd > 0) FlashText("Royal Spin recharging");
                }

                if (P.roll == null)
                {
                    var a = P.atk;
                    float mult = a == null ? 1 : a.kind == "special" ? .7f : (a.kind == "air" || a.kind == "slam") ? .8f : .12f;
                    if (ix != 0 && (a == null || a.kind == "special")) P.face = ix;
                    if (ix != 0 || iy != 0)
                    {
                        P.x += ix * 240 * mult * dt;
                        P.y += iy * 165 * mult * dt;
                        P.moving = a == null;
                        P.walkT += dt;
                    }
                }
            }

            bool wasAir = P.z > 0;
            ZPhysics(ref P.z, ref P.vz, dt);
            if (wasAir && P.z <= 0)
            {
                P.jumps = 0; P.airHits = 0;
                if (P.atk != null && P.atk.kind == "slam" && !P.atk.landed) { P.atk.landed = true; SlamImpact(); }
                else if (P.atk != null && P.atk.kind == "air") P.atk = null;
            }

            UpdateAttack(dt);

            if (Tapped(Act.Interact)) Interact();
            if (Tapped(Act.Pet) && pet != null) PetFury();

            P.y = Clamp(P.y, GROUND_TOP + 10, GROUND_BOT);
            float lo = level.locked ? level.lockX + 22 : camX + 22;
            float hi = level.locked ? level.lockX + W - 22 : Mathf.Min(stageDef.len - 22, camX + W - 22);
            P.x = Clamp(P.x, lo, hi);
        }

        void Interact()
        {
            var P = player;
            Pickup best = null;
            float bd = 1e9f;
            foreach (var k in pickups)
            {
                if (k.kind == "coin" || k.opened) continue;
                float d = Mathf.Abs(k.x - P.x) + Mathf.Abs(k.y - P.y) * 1.5f;
                if (d < 80 && d < bd) { best = k; bd = d; }
            }
            if (best == null) return;
            if (best.kind == "food")
            {
                float heal = Mathf.Min(30, P.maxHp - P.hp);
                P.hp += heal;
                AddText(P.x, P.y, P.z + 100, "+" + Mathf.RoundToInt(heal) + " HP", C("#6fe06a"), 20);
                best.dead = true; sfx.Play("eat");
            }
            else if (best.kind == "chest")
            {
                best.opened = true; best.openT = 0;
                DropCoins(best.x, best.y, 120, 20);
                Burst(best.x, best.y, 30, 30, C("#ffcc4d"), 300, 4);
                AddText(best.x, best.y, 90, "TREASURE!", C("#ffcc4d"), 24);
                sfx.Play("chest");
            }
        }

        /* ---------------------------------------------------------------- pet */

        Ent NearestEnemy(float x, float y, float maxD)
        {
            Ent best = null;
            float bd = maxD;
            foreach (var e in enemies)
            {
                if (e.dead || e.prop || e.entering) continue;
                float d = Mathf.Sqrt((e.x - x) * (e.x - x) + (e.y - y) * (e.y - y) * 2.25f);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        bool PetShoot(float power = 1)
        {
            var D = pet.def;
            var t = NearestEnemy(pet.x, pet.y, 520);
            if (t == null && stageDef.training) t = enemies.Find(e => e.type == "dummy");
            if (t == null) return false;
            float dx = t.x - pet.x, dy = t.y - pet.y, dist = Mathf.Max(1, Mathf.Sqrt(dx * dx + dy * dy));
            projectiles.Add(new Proj
            {
                owner = "p", kind = D.proj, x = pet.x, y = pet.y, z = pet.z, vx = dx / dist * 520, vy = dy / dist * 520,
                target = t.id, dmg = D.dmg * power, burnDps = D.burnDps, burnT = D.burnT, life = 1.6f, pet = true,
            });
            sfx.Play("pet");
            return true;
        }

        void PetFury()
        {
            if (pet.fury > 0) { FlashText("Pet fury recharging"); return; }
            pet.fury = KCData.PetFuryCd; pet.furyShots = 3; pet.furyT = 0;
            AddText(pet.x, pet.y, pet.z + 30, "FURY!", pet.def.color, 18);
        }

        void UpdatePet(float dt)
        {
            if (pet == null) return;
            var P = player;
            float tx = P.x - P.face * 52, ty = P.y - 14;
            pet.x = Lerp(pet.x, tx, Mathf.Min(1, dt * 4));
            pet.y = Lerp(pet.y, ty, Mathf.Min(1, dt * 4));
            pet.bob += dt;
            pet.z = 46 + Mathf.Sin(pet.bob * 4) * 6 + P.z * .6f;
            pet.cd -= dt;
            pet.fury = Mathf.Max(0, pet.fury - dt);
            if (pet.cd <= 0 && PetShoot()) pet.cd = pet.def.cd;
            if (pet.furyShots > 0)
            {
                pet.furyT -= dt;
                if (pet.furyT <= 0) { pet.furyT = .22f; if (PetShoot(1)) pet.furyShots--; else pet.furyShots = 0; }
            }
            if (pet.def.key == "fox" && Chance < .5f)
                Puff(pet.x - P.face * 24, pet.y, pet.z + 14, 0, 40, .3f, C("#ffb36b"), 3, -.2f);
        }

        /* ---------------------------------------------------------------- enemies */

        int TokensUsed()
        {
            int n = 0;
            foreach (var e in enemies) if (!e.dead && (e.state == "windup" || e.state == "atk")) n++;
            return n;
        }

        static void MoveToward(Ent e, float tx, float ty, float spd, float dt)
        {
            float dx = tx - e.x, dy = ty - e.y, dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < 3) return;
            float s = Mathf.Min(spd * dt, dist);
            e.x += dx / dist * s; e.y += dy / dist * s * .85f;
            e.walkT += dt;
        }

        bool EnemyReach(Ent e, float range, float tol)
        {
            var P = player;
            float dx = (P.x - e.x) * e.face;
            return dx > -12 && dx < range && Mathf.Abs(P.y - e.y) < tol && P.z < 55;
        }

        void RunAI(Ent e, float dt)
        {
            switch (e.type)
            {
                case "slime": case "minislime": AiSlime(e, dt); break;
                case "scout": AiScout(e, dt); break;
                case "archer": AiArcher(e, dt); break;
                case "boar": AiBoar(e, dt); break;
                case "captain": AiCaptain(e, dt); break;
                case "king": AiKing(e, dt); break;
            }
        }

        void AiSlime(Ent e, float dt)
        {
            var P = player;
            if (e.z <= 0 && e.vz <= 0)
            {
                e.vx = 0; e.vy = 0;
                e.t += dt;
                if (e.t > (e.hopDelay > 0 ? e.hopDelay : .8f))
                {
                    e.t = 0; e.hopDelay = Rand(.6f, 1.1f);
                    float dx = P.x - e.x, dy = P.y - e.y;
                    e.vz = 380;
                    e.vx = Sign(dx) * Mathf.Min(170, Mathf.Abs(dx) * 1.2f + 40);
                    e.vy = Sign(dy) * Mathf.Min(90, Mathf.Abs(dy) * 1.5f);
                    e.face = Sign(dx); e.hopHit = false;
                }
            }
            else
            {
                e.x += e.vx * dt; e.y += e.vy * dt;
                if (!e.hopHit && Mathf.Abs(P.x - e.x) < e.w + 16 && Mathf.Abs(P.y - e.y) < 24 && P.z < e.z + e.h)
                {
                    e.hopHit = true; HurtPlayer(e.type == "minislime" ? 6 : 8, e.x, 140);
                }
            }
        }

        void AiScout(Ent e, float dt)
        {
            var P = player;
            int side = Sign(e.x - P.x);
            if (e.state == "move")
            {
                e.face = -side; e.cd -= dt;
                bool busy = TokensUsed() >= 2 || P.dead;
                float tx = P.x + side * (busy ? 150 : 54), ty = P.y + (busy ? e.offY : 0);
                MoveToward(e, tx, ty, e.spd, dt);
                if (!busy && Mathf.Abs(e.x - tx) < 16 && Mathf.Abs(e.y - P.y) < 16 && e.cd <= 0) { e.state = "windup"; e.t = 0; }
            }
            else if (e.state == "windup")
            {
                e.t += dt;
                if (e.t > .4f) { e.state = "atk"; e.t = 0; e.didHit = false; sfx.Play("swing"); }
            }
            else if (e.state == "atk")
            {
                e.t += dt;
                if (!e.didHit) { e.didHit = true; if (EnemyReach(e, 66, 28)) HurtPlayer(10, e.x, 190); }
                if (e.t > .14f) { e.state = "rec"; e.t = 0; }
            }
            else if (e.state == "rec")
            {
                e.t += dt;
                if (e.t > .5f) { e.state = "move"; e.cd = Rand(.5f, 1.4f); }
            }
        }

        void AiArcher(Ent e, float dt)
        {
            var P = player;
            int side = Sign(e.x - P.x);
            e.face = -side;
            if (e.state == "move")
            {
                e.cd -= dt;
                float tx = P.x + side * 300;
                if (level.locked)
                {
                    float lo = level.lockX + 40, hi = level.lockX + W - 40;
                    tx = Clamp(tx, lo, hi);
                    float alt = Clamp(P.x - side * 300, lo, hi);
                    if (Mathf.Abs(alt - P.x) > Mathf.Abs(tx - P.x) + 60) tx = alt;
                }
                bool near = Mathf.Abs(e.x - P.x) < 150;
                MoveToward(e, tx, P.y + e.offY * .15f, e.spd * (near ? 1.35f : 1), dt);
                if (Mathf.Abs(e.y - P.y) < 22 && Mathf.Abs(e.x - P.x) > 160 && e.cd <= 0 && !P.dead) { e.state = "windup"; e.t = 0; }
            }
            else if (e.state == "windup")
            {
                e.t += dt;
                if (e.t > .6f)
                {
                    projectiles.Add(new Proj { owner = "e", kind = "earrow", x = e.x + e.face * 22, y = e.y, z = 40, vx = e.face * 430, dmg = 8, life = 2.4f });
                    sfx.Play("shoot"); e.state = "rec"; e.t = 0;
                }
            }
            else if (e.state == "rec")
            {
                e.t += dt;
                if (e.t > .45f) { e.state = "move"; e.cd = Rand(1.4f, 2.4f); }
            }
        }

        void AiBoar(Ent e, float dt)
        {
            var P = player;
            int side = Sign(e.x - P.x);
            if (e.state == "move")
            {
                e.face = -side; e.cd -= dt;
                MoveToward(e, P.x + side * 250, P.y, e.spd, dt);
                if (Mathf.Abs(e.y - P.y) < 18 && e.cd <= 0 && Mathf.Abs(e.x - P.x) > 120 && !P.dead) { e.state = "windup"; e.t = 0; }
            }
            else if (e.state == "windup")
            {
                e.t += dt;
                if (Chance < .4f) Puff(e.x - e.face * 20, e.y, 2, -e.face * Rand(40, 120), Rand(40, 140), .4f, C("#c9a46b"), 4, 1);
                if (e.t > .7f) { e.state = "charge"; e.t = 0; e.didHit = false; sfx.Play("roll"); }
            }
            else if (e.state == "charge")
            {
                e.t += dt;
                e.x += e.face * 540 * dt;
                e.walkT += dt * 2;
                if (!e.didHit && Mathf.Abs(P.x - e.x) < e.w + 16 && Mathf.Abs(P.y - e.y) < 26 && P.z < 40)
                {
                    if (HurtPlayer(18, e.x - e.face * 50, 420)) e.didHit = true;
                }
                bool outside = level.locked && (e.x < level.lockX + 20 || e.x > level.lockX + W - 20);
                if (e.t > 1.1f || outside) { e.state = "dizzy"; e.t = 0; if (outside) shake = Mathf.Max(shake, 4); }
            }
            else if (e.state == "dizzy")
            {
                e.t += dt;
                if (e.t > .9f) { e.state = "move"; e.cd = Rand(1.5f, 2.6f); }
            }
        }

        void AiCaptain(Ent e, float dt)
        {
            var P = player;
            int side = Sign(e.x - P.x);
            if (!e.shieldUp)
            {
                e.shieldT -= dt;
                if (e.shieldT <= 0) { e.shieldUp = true; AddText(e.x, e.y, e.h + 14, "SHIELD UP", C("#9fb4ff"), 14); }
            }
            if (!e.r1 && e.hp < e.maxHp * .66f) { e.r1 = true; Reinforce(e); }
            if (!e.r2 && e.hp < e.maxHp * .33f) { e.r2 = true; Reinforce(e); }
            if (e.state == "move")
            {
                e.cd -= dt;
                e.turnT -= dt;
                if (e.turnT <= 0) { e.face = -side; e.turnT = .5f; }
                float dist = Mathf.Abs(e.x - P.x);
                bool aligned = Mathf.Abs(e.y - P.y) < 18;
                if (dist > 160 && dist < 320 && aligned && e.cd <= 0 && !P.dead) { e.face = -side; e.state = "bashwind"; e.t = 0; }
                else
                {
                    MoveToward(e, P.x + side * 64, P.y, e.spd, dt);
                    if (dist < 90 && aligned && e.cd <= 0 && !P.dead) { e.face = -side; e.state = "windup"; e.t = 0; e.slash = 0; }
                }
            }
            else if (e.state == "windup")
            {
                e.t += dt;
                if (e.t > (e.slash > 0 ? .22f : .45f)) { e.state = "atk"; e.t = 0; e.didHit = false; sfx.Play("swing"); }
            }
            else if (e.state == "atk")
            {
                e.t += dt;
                if (!e.didHit) { e.didHit = true; if (EnemyReach(e, 84, 30)) HurtPlayer(15, e.x, 220); }
                if (e.t > .15f)
                {
                    if (e.slash < 1) { e.slash++; e.state = "windup"; e.t = 0; }
                    else { e.state = "rec"; e.t = 0; }
                }
            }
            else if (e.state == "bashwind")
            {
                e.t += dt;
                if (e.t > .55f) { e.state = "bash"; e.t = 0; e.didHit = false; }
            }
            else if (e.state == "bash")
            {
                e.t += dt;
                e.x += e.face * 440 * dt;
                if (!e.didHit && Mathf.Abs(P.x - e.x) < e.w + 18 && Mathf.Abs(P.y - e.y) < 28 && P.z < 50)
                {
                    if (HurtPlayer(18, e.x - e.face * 40, 360)) e.didHit = true;
                }
                if (e.t > .45f) { e.state = "rec"; e.t = 0; }
            }
            else if (e.state == "rec")
            {
                e.t += dt;
                if (e.t > .8f) { e.state = "move"; e.cd = Rand(.8f, 1.6f); }
            }
        }

        void AiKing(Ent e, float dt)
        {
            var P = player;
            if (!e.ph2 && e.hp < e.maxHp * .5f)
            {
                e.ph2 = true; shake = 12;
                AddText(e.x, e.y, e.h + 40, "KING SLIME IS ENRAGED!", C("#ff6a6a"), 24);
                sfx.Play("boom");
            }
            float sp = e.ph2 ? 1.45f : 1, ddt = dt * sp;
            bool grounded = e.z <= 0 && e.vz <= 0;
            switch (e.state)
            {
                case "intro": break;
                case "idle":
                    e.t += ddt;
                    e.face = Sign(P.x - e.x);
                    if (e.t > .75f)
                    {
                        e.t = 0;
                        int minis = 0;
                        foreach (var m in enemies) if (m.type == "minislime" && !m.dead) minis++;
                        var opts = new List<string> { "hop", "hop", "big", "wave" };
                        if (minis < 3) opts.Add("summon");
                        string next = opts[UnityEngine.Random.Range(0, opts.Count)];
                        if (next == e.last && next != "hop") next = "hop";
                        e.last = next;
                        if (next == "hop") { e.state = "hop"; e.hops = 3; }
                        else if (next == "big") e.state = "bigwind";
                        else if (next == "wave") { e.state = "wavewind"; e.waves = e.ph2 ? 2 : 1; }
                        else e.state = "summon";
                    }
                    break;
                case "hop":
                    if (grounded)
                    {
                        e.t += ddt;
                        if (e.t > .32f)
                        {
                            e.t = 0;
                            if (e.hops <= 0) { e.state = "idle"; break; }
                            e.hops--;
                            e.vz = 560;
                            e.vx = Clamp((P.x - e.x) * 1.4f, -270, 270) * sp;
                            e.vy = Clamp((P.y - e.y) * 1.6f, -130, 130);
                            e.landDmg = 15; e.landR = 110;
                        }
                    }
                    else { e.x += e.vx * dt; e.y += e.vy * dt; }
                    break;
                case "bigwind":
                    e.t += ddt;
                    if (e.t > .65f) { e.state = "big"; e.t = 0; e.vz = 1250; e.landDmg = 25; e.landR = 150; }
                    break;
                case "big":
                    if (!grounded)
                    {
                        float k = Mathf.Min(1, dt * 2.2f * sp) * (e.vz > -200 ? 1 : .3f);
                        e.x += (P.x - e.x) * k;
                        e.y += (P.y - e.y) * k;
                    }
                    break;
                case "wavewind":
                    e.t += ddt;
                    if (e.t > .6f)
                    {
                        foreach (int dir in new[] { -1, 1 })
                            projectiles.Add(new Proj { owner = "e", kind = "wave", x = e.x + dir * 60, y = e.y, z = 0, vx = dir * 400, dmg = 20, life = 2.2f });
                        shake = Mathf.Max(shake, 6); sfx.Play("heavy");
                        e.waves--;
                        if (e.waves > 0) e.t = .25f; else { e.state = "idle"; e.t = 0; }
                    }
                    break;
                case "summon":
                    e.t += ddt;
                    if (e.t > .8f)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var m = SpawnEnemy("minislime", e.x + Rand(-40, 40), Clamp(e.y + Rand(-50, 50), GROUND_TOP + 15, GROUND_BOT), 90);
                            m.vz = 380; m.kbx = (i - 1) * 260;
                        }
                        AddText(e.x, e.y, e.h + 30, "SLIMELETS!", C("#7df07a"), 18);
                        sfx.Play("pet");
                        e.state = "idle"; e.t = 0;
                    }
                    break;
            }
        }

        void OnEnemyLand(Ent e)
        {
            var P = player;
            if (e.type == "king")
            {
                if (e.state == "intro")
                {
                    e.state = "idle"; e.t = -.4f; shake = 14; Ring(e.x, e.y, 30, C("#7df07a"), 420); sfx.Play("boom");
                    return;
                }
                if (e.landDmg > 0)
                {
                    bool big = e.state == "big";
                    shake = Mathf.Max(shake, big ? 14 : 6);
                    Ring(e.x, e.y, big ? 30 : 16, C("#7df07a"), big ? 440 : 300);
                    sfx.Play(big ? "boom" : "heavy");
                    if (Mathf.Abs(P.x - e.x) < e.landR && Mathf.Abs(P.y - e.y) < e.d + 40 && P.z < 40) HurtPlayer(e.landDmg, e.x, 380);
                    e.landDmg = 0;
                }
                e.vx = 0; e.vy = 0;
                if (e.state == "big") { e.state = "idle"; e.t = -.5f; }
                return;
            }
            if (e.hurtT > 0) e.hurtT = Mathf.Max(e.hurtT, .3f);
        }

        void Reinforce(Ent e)
        {
            AddText(e.x, e.y, e.h + 30, "TO ME, LADS!", C("#ffcc4d"), 20);
            foreach (var t in new[] { "scout", "scout", "archer" }) SpawnFromEdge(t);
        }

        Ent SpawnFromEdge(string type)
        {
            bool left = Chance < .35f;
            float bx = level.locked ? level.lockX : camX;
            float x = left ? bx - 50 : bx + W + 50;
            var e = SpawnEnemy(type, x, Rand(GROUND_TOP + 20, GROUND_BOT - 10), 0, "move", true);
            e.face = left ? 1 : -1;
            return e;
        }

        void UpdateEnemies(float dt)
        {
            var P = player;
            foreach (var e in enemies.ToArray())
            {
                e.flash = Mathf.Max(0, e.flash - dt);
                if (e.wobble > 0) e.wobble = Mathf.Max(0, e.wobble - dt * 2);
                if (e.prop) { if (e.dead) e.deadT += dt; continue; }

                if (e.burnT > 0 && !e.dead)
                {
                    e.burnT -= dt; e.burnAcc += e.burnDps * dt;
                    if (Chance < .3f) Puff(e.x + Rand(-10, 10), e.y, e.z + Rand(10, e.h), 0, 60, .4f, C("#ff8a3d"), 4, -.2f);
                    if (e.burnAcc >= 1)
                    {
                        int n = Mathf.FloorToInt(e.burnAcc); e.burnAcc -= n;
                        e.hp -= n; AddText(e.x, e.y, e.z + e.h, n.ToString(), C("#ff8a3d"), 13);
                        if (e.hp <= 0) KillEnemy(e, 1);
                    }
                }
                e.slowT = Mathf.Max(0, e.slowT - dt);
                float sdt = dt * (e.slowT > 0 ? .5f : 1);

                bool wasAir = e.z > 0;
                bool landed = ZPhysics(ref e.z, ref e.vz, dt, e.boss ? 2100 : G);
                if (landed || (wasAir && e.z <= 0)) OnEnemyLand(e);
                if (e.kbx != 0) { e.x += e.kbx * dt; e.kbx *= Mathf.Pow(.004f, dt); if (Mathf.Abs(e.kbx) < 5) e.kbx = 0; }

                if (e.dead) { e.deadT += dt; continue; }
                if (e.hurtT > 0)
                {
                    e.hurtT -= dt;
                    if (e.z > 0) e.hurtT = Mathf.Max(e.hurtT, .05f);
                }
                else RunAI(e, sdt);

                e.y = Clamp(e.y, GROUND_TOP + 10, GROUND_BOT);
                if (level.locked)
                {
                    if (e.entering)
                    {
                        if (e.x > level.lockX + 30 && e.x < level.lockX + W - 30) e.entering = false;
                        else e.x += Sign(P.x - e.x) * 120 * dt;
                    }
                    else e.x = Clamp(e.x, level.lockX + 16, level.lockX + W - 16);
                }
                else if (e.entering && e.x > camX && e.x < camX + W) e.entering = false;
            }
            enemies.RemoveAll(e => e.dead && e.deadT > 1.1f);
        }

        /* ---------------------------------------------------------------- projectiles / pickups / particles */

        void UpdateProjectiles(float dt)
        {
            var P = player;
            foreach (var p in projectiles)
            {
                p.life -= dt;
                if (p.target != 0)
                {
                    var t = enemies.Find(e => e.id == p.target && !e.dead);
                    if (t != null)
                    {
                        float dx = t.x - p.x, dy = t.y - p.y, d = Mathf.Max(1, Mathf.Sqrt(dx * dx + dy * dy)), sp = Mathf.Sqrt(p.vx * p.vx + p.vy * p.vy);
                        float k = Mathf.Min(1, dt * 6);
                        p.vx = Lerp(p.vx, dx / d * sp, k);
                        p.vy = Lerp(p.vy, dy / d * sp, k);
                        p.z = Lerp(p.z, t.z + t.h * .5f, Mathf.Min(1, dt * 4));
                    }
                }
                p.x += p.vx * dt; p.y += p.vy * dt;
                if (p.owner == "p")
                {
                    foreach (var e in enemies.ToArray())
                    {
                        if (e.dead || e.entering || p.hit.Contains(e.id) || p.life <= 0) continue;
                        if (Mathf.Abs(e.x - p.x) < e.w + 10 && Mathf.Abs(e.y - p.y) < e.d + 18 && p.z > e.z - 12 && p.z < e.z + e.h + 12)
                        {
                            p.hit.Add(e.id);
                            var o = new HitOpts { fromX = p.x - Sign(p.vx) * 40, kb = p.pet ? 60 : 70, slow = p.slow, burnDps = p.burnDps, burnT = p.burnT, breaks = p.breaks, big = p.big };
                            if (p.pet) { DamageEnemy(e, p.dmg, o); sfx.Play("hit"); }
                            else PlayerHit(e, p.dmg, o);
                            Burst(p.x, p.y, p.z, 6, p.kind == "fire" ? C("#ff8a3d") : p.kind == "blob" ? C("#6fe06a") : C("#bfefff"), 160, 3);
                            if (p.pierce-- <= 0) p.life = 0;
                        }
                    }
                }
                else if (!P.dead)
                {
                    bool hit = false;
                    if (p.kind == "earrow") hit = Mathf.Abs(P.x - p.x) < 20 && Mathf.Abs(P.y - p.y) < 22 && P.z < p.z - 6;
                    else if (p.kind == "wave") hit = Mathf.Abs(P.x - p.x) < 28 && Mathf.Abs(P.y - p.y) < 40 && P.z < 24;
                    if (hit && HurtPlayer(p.dmg, p.x - p.vx, 200) && p.kind == "earrow") p.life = 0;
                    if (p.kind == "wave" && Chance < .5f) Puff(p.x, p.y + Rand(-20, 20), 4, 0, Rand(80, 200), .35f, C("#7df07a"), 5, 1);
                }
                if (p.kind == "arrow" && Chance < .5f) Puff(p.x, p.y, p.z, 0, 0, .2f, C("#bfefff"), 3, 0);
                if (p.x < camX - 150 || p.x > camX + W + 150) p.life = 0;
            }
            projectiles.RemoveAll(p => p.life <= 0);
        }

        void UpdatePickups(float dt)
        {
            var P = player;
            foreach (var k in pickups)
            {
                k.age += dt;
                if (ZPhysics(ref k.z, ref k.vz, dt)) { k.vx = 0; k.vy = 0; }
                if (k.z > 0) { k.x += k.vx * dt; k.y += k.vy * dt; }
                k.y = Clamp(k.y, GROUND_TOP + 10, GROUND_BOT);
                if (k.kind == "coin" && k.age > .45f && !P.dead)
                {
                    float dx = P.x - k.x, dy = P.y - k.y, d = Mathf.Max(.01f, Mathf.Sqrt(dx * dx + dy * dy));
                    const float range = 150;
                    if (d < range)
                    {
                        float s = Mathf.Min(520 * dt * (1 + (range - d) / range), d);
                        k.x += dx / d * s; k.y += dy / d * s;
                    }
                    if (d < 24) { k.dead = true; GainCoins(k.value); sfx.Play("coin"); }
                }
                if (k.kind == "chest" && k.opened) k.openT += dt;
            }
            pickups.RemoveAll(k => k.dead);
        }

        void UpdateParticles(float dt)
        {
            foreach (var p in particles)
            {
                p.life -= dt;
                p.x += p.vx * dt; p.y += p.vy * dt;
                p.vz -= G * .6f * p.grav * dt; p.z += p.vz * dt;
                if (p.z < 0) { p.z = 0; p.vz *= -.3f; p.vx *= .6f; }
            }
            particles.RemoveAll(p => p.life <= 0);
            foreach (var t in texts) { t.life -= dt; t.z += 50 * dt; }
            texts.RemoveAll(t => t.life <= 0);
        }

        /* ---------------------------------------------------------------- level flow / camera */

        void UpdateLevel(float dt)
        {
            var P = player;
            var def = stageDef;
            level.go = Mathf.Max(0, level.go - dt);
            if (def.training) return;

            if (!level.locked && level.waveIdx < def.waves.Length && P.x > def.waves[level.waveIdx].x - 260)
            {
                var wv = def.waves[level.waveIdx];
                level.locked = true;
                level.lockX = Clamp(wv.x - W / 2, 0, def.len - W);
                level.queue.Clear();
                level.queue.AddRange(wv.list);
                level.spawnT = .3f;
                if (wv.boss)
                {
                    level.queue.Clear();
                    var k = SpawnEnemy("king", level.lockX + W * .72f, 400, 700, "intro");
                    k.face = -1;
                    level.bossIntro = 2.2f;
                }
            }
            if (level.locked)
            {
                if (level.queue.Count > 0)
                {
                    level.spawnT -= dt;
                    if (level.spawnT <= 0) { SpawnFromEdge(level.queue[0]); level.queue.RemoveAt(0); level.spawnT = .55f; }
                }
                else if (!enemies.Exists(e => !e.dead && !e.prop))
                {
                    var wv = def.waves[level.waveIdx];
                    level.locked = false;
                    level.waveIdx++;
                    if (!wv.boss) level.go = 3;
                }
            }
            if (level.bossIntro > 0) level.bossIntro = Mathf.Max(0, level.bossIntro - dt);

            if (!level.ended)
            {
                bool done = level.waveIdx >= def.waves.Length;
                if (def.boss && level.bossKilled) { level.ended = true; level.endT = 3; }
                else if (!def.boss && done && P.x > def.len - 90) { level.ended = true; level.endT = .2f; }
            }
            else
            {
                level.endT -= dt;
                if (level.endT <= 0 && state == "play") EndStage(true);
            }
        }

        void UpdateCamera(float dt)
        {
            var P = player;
            if (level.locked) camX = Lerp(camX, level.lockX, Mathf.Min(1, dt * 5));
            else
            {
                float target = Clamp(P.x - W * .42f, 0, Mathf.Max(0, stageDef.len - W));
                if (target > camX) camX = Lerp(camX, target, Mathf.Min(1, dt * 8));
            }
            shake = Mathf.Max(0, shake - dt * 30);
        }

        void Step(float dt)
        {
            if (msg != null) { msgLife -= dt; if (msgLife <= 0) msg = null; }
            if (state != "play") return;
            if (hitstop > 0) { hitstop -= dt; return; }
            run.time += dt;
            banner = Mathf.Max(0, banner - dt);
            UpdatePlayer(dt);
            pressed.Clear();
            UpdatePet(dt);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdatePickups(dt);
            UpdateParticles(dt);
            UpdateLevel(dt);
            UpdateCamera(dt);
        }

        /* ---------------------------------------------------------------- drawing shorthands */

        void Circ(float x, float y, float r, Color c, bool s = true) => g.Circle(x, y, r, c, s);
        void Ell(float x, float y, float rx, float ry, Color c, bool s = true, float rot = 0) => g.Ellipse(x, y, rx, ry, c, s, rot);
        void RR(float x, float y, float w, float h, float r, Color c, bool s = true) => g.RRect(x, y, w, h, r, c, s);
        void Poly(Color c, params float[] p) => g.Poly(p, c, true);
        void PolyNS(Color c, params float[] p) => g.Poly(p, c, false);

        // Text is drawn by OnGUI on top of the mesh. align: 0 left, 1 centre, 2 right; y is the baseline.
        void Txt(string s, float x, float y, int size, Color color, int align = 1, bool outline = true, bool world = true)
        {
            gtexts.Add(new GText
            {
                s = s, x = x + (world ? shakeX : 0), y = y + (world ? shakeY : 0), size = size, align = align,
                color = color, outline = outline, alpha = g.Alpha,
            });
        }

        readonly List<Vector2> path = new List<Vector2>(64);

        /* ---------------------------------------------------------------- background */

        void DrawBG()
        {
            bool boss = stageDef.boss, train = stageDef.training;
            if (boss) g.GradientRect(0, 0, W, GROUND_TOP, C("#4a2c6b"), C("#c97fa8"));
            else if (train) g.GradientRect(0, 0, W, GROUND_TOP, C("#3a2a1e"), C("#6b4a30"));
            else g.GradientRect(0, 0, W, GROUND_TOP, C("#6fc0f2"), C("#e2f5ff"));

            if (train)
            {
                for (int i = 0; i < 12; i++)
                    for (int j = 0; j < 6; j++)
                        g.Rect(i * 90 - (j % 2) * 45, j * 50, 88, 48, (i + j) % 2 == 1 ? C("#5d4128") : C("#664730"));
                foreach (float x in new float[] { 160, 480, 800 })
                {
                    RR(x - 40, 60, 80, 110, 6, C("#3b2a1c"));
                    Txt(x == 480 ? "TRAINING" : "X", x, 120, 14, C("#c9a46b"), 1, false);
                }
                g.Rect(0, GROUND_TOP - 30, W, H, C("#8a6a48"));
                for (int i = 0; i < 20; i++) g.Rect(i * 50, GROUND_TOP - 30, 50, H, i % 2 == 1 ? C("#7d5f40") : C("#94734f"));
                return;
            }

            Circ(780 - camX * .02f, 86, 40, boss ? C("#ffd6f2") : C("#fff6c2"), false);
            var cloud = boss ? CA("#ffffff", .13f) : CA("#ffffff", .8f);
            for (int i = 0; i < 7; i++)
            {
                float span = W + 400;
                float x = ((i * 330 - camX * .08f) % span + span) % span - 200, y = 40 + Hash(i) * 90;
                Ell(x, y, 60, 18, cloud, false); Ell(x + 40, y - 10, 40, 18, cloud, false); Ell(x - 35, y - 6, 34, 14, cloud, false);
            }
            // hills, as vertical strips so the wavy top edge needs no polygon triangulation
            var hills = new[] { (.12f, 230f, 40f, boss ? C("#6b3f7a") : C("#93cf7a")), (.25f, 260f, 30f, boss ? C("#53305f") : C("#6fb65a")) };
            foreach (var (par, bs, amp, col) in hills)
            {
                float HillY(float x) { float wx = x + camX * par; return bs - Mathf.Sin(wx * .006f) * amp - Mathf.Sin(wx * .017f) * amp * .4f; }
                for (float x = 0; x < W; x += 16) g.Quad(x, HillY(x), x + 16, HillY(x + 16), x + 16, GROUND_TOP, x, GROUND_TOP, col);
            }
            // trees
            {
                const float par = .55f, sp = 130;
                int i0 = Mathf.FloorToInt(camX * par / sp) - 1;
                for (int i = i0; i < i0 + W / sp + 3; i++)
                {
                    float x = i * sp - camX * par + Hash(i) * 50, s = .8f + Hash(i + 9) * .5f;
                    float bs = GROUND_TOP - 6;
                    RR(x - 8 * s, bs - 70 * s, 16 * s, 70 * s, 4, boss ? C("#3a2340") : C("#6b4429"));
                    Color l0 = boss ? C("#5b2f6b") : C("#2f8a3c"), l1 = boss ? C("#6e3a80") : C("#3fa24a");
                    Circ(x, bs - 95 * s, 38 * s, l0);
                    Circ(x - 24 * s, bs - 76 * s, 26 * s, l1);
                    Circ(x + 26 * s, bs - 78 * s, 27 * s, l1);
                }
            }
            // ground and dirt path
            g.Rect(0, GROUND_TOP - 22, W, H, boss ? C("#4f7a3a") : C("#5fae45"));
            g.Rect(0, GROUND_TOP - 22, W, 8, boss ? C("#3f6a2e") : C("#4f9a3a"));
            var dirt = boss ? C("#8d7a52") : C("#cfae75");
            for (float x = 0; x < W; x += 20)
            {
                float t0 = 352 + Mathf.Sin((x + camX) * .01f) * 6, t1 = 352 + Mathf.Sin((x + 20 + camX) * .01f) * 6;
                float b0 = 462 + Mathf.Sin((x + camX) * .013f + 2) * 6, b1 = 462 + Mathf.Sin((x + 20 + camX) * .013f + 2) * 6;
                g.Quad(x, t0, x + 20, t1, x + 20, b1, x, b0, dirt);
            }
            // tufts and flowers
            int j0 = Mathf.FloorToInt(camX / 55) - 1;
            var grass = boss ? C("#2e5122") : C("#3f8a2e");
            for (int i = j0; i < j0 + W / 55 + 3; i++)
            {
                float x = i * 55 - camX + Hash(i * 3) * 40, y = GROUND_TOP + 4 + Hash(i * 7) * 200;
                if (y > 345 && y < 470) continue;
                g.Line(x, y, x - 4, y - 9, 2, grass); g.Line(x, y, x + 1, y - 11, 2, grass); g.Line(x, y, x + 5, y - 8, 2, grass);
                if (Hash(i * 11) > .7f)
                {
                    string[] fl = { "#ffe14d", "#ff7ab8", "#ffffff" };
                    Circ(x + 6, y - 10, 3.5f, C(fl[Mathf.Min(2, (int)(Hash(i) * 3))]), false);
                }
            }
            // stage exit gate
            float gx = stageDef.len - 70 - camX;
            if (!stageDef.boss && gx < W + 60)
            {
                RR(gx - 6, GROUND_TOP - 140, 14, 220, 3, C("#6b4429"));
                RR(gx + 66, GROUND_TOP - 140, 14, 220, 3, C("#6b4429"));
                RR(gx - 16, GROUND_TOP - 156, 106, 24, 5, C("#8a5a2b"));
                Txt("EXIT", gx + 37, GROUND_TOP - 139, 13, Color.white, 1, false);
            }
        }

        void DrawFG()
        {
            if (stageDef.training) return;
            const float sp = 300, par = 1.3f;
            int i0 = Mathf.FloorToInt(camX * par / sp) - 1;
            var c = stageDef.boss ? C("#2a1a33") : C("#26602b");
            for (int i = i0; i < i0 + W / sp + 3; i++)
            {
                if (Hash(i * 5) < .45f) continue;
                float x = i * sp - camX * par + Hash(i) * 120;
                Circ(x, H + 14, 46, c, false); Circ(x + 40, H + 22, 38, c, false); Circ(x - 38, H + 24, 34, c, false);
            }
        }

        /* ---------------------------------------------------------------- character drawing */

        float ArmAngle(Player P)
        {
            var a = P.atk;
            bool ranged = Weapon.kind == "ranged";
            if (a == null) return ranged ? .25f : (P.z > 0 ? -1.2f : -.8f);
            var d = a.d;
            float t = a.t;
            if (a.kind == "slam") return 1.5f;
            if (a.kind == "special") return 0;
            if (ranged) return a.kind == "heavy" ? -.12f : 0;
            float A = a.kind == "heavy" ? -2.6f : -2.1f, B = a.kind == "heavy" ? 1.5f : 1.2f;
            if (a.kind == "light" && a.step == 1) { float tmp = A; A = B; B = tmp + .5f; }
            if (a.kind == "air") { A = -2.4f; B = 2.2f; }
            if (t < d.su) return Lerp(-.8f, A, Mathf.Sin(t / d.su * Mathf.PI / 2));
            if (t < d.su + d.ac) return Lerp(A, B, (t - d.su) / d.ac);
            return B;
        }

        void DrawWeapon(string key, float pull = 0)
        {
            if (key == "iron")
            {
                RR(6, -3, 10, 6, 2, C("#6b4429"));
                RR(15, -10, 6, 20, 2, C("#b8892f"));
                Poly(C("#e6edf5"), 21, -4.5f, 66, -4.5f, 76, 0, 66, 4.5f, 21, 4.5f);
                g.Line(24, -1, 64, -1, 1.5f, CA("#ffffff", .6f));
            }
            else if (key == "hammer")
            {
                RR(4, -3.5f, 66, 7, 3, C("#7a5230"));
                RR(58, -20, 26, 40, 5, C("#9aa3b5"));
                RR(56, -14, 30, 6, 2, C("#c9a33a")); RR(56, 8, 30, 6, 2, C("#c9a33a"));
            }
            else if (key == "bow")
            {
                g.Arc(10, 0, 30, -1.15f, 1.15f, 6, OUT);
                g.Arc(10, 0, 30, -1.15f, 1.15f, 3.5f, g.Flash ? Color.white : C("#7fd3ff"));
                float ex = 10 + Mathf.Cos(1.15f) * 30, ey = Mathf.Sin(1.15f) * 30;
                g.Polyline(new[] { ex, -ey, ex - 12 * pull, 0, ex, ey }, 1.5f, C("#eeeeff"));
                if (pull > 0) g.Line(ex - 12 * pull, 0, ex + 34, 0, 2, C("#dddddd"));
            }
        }

        void DrawKnight(Player P)
        {
            float sx = P.x - camX, sy = P.y - P.z;
            g.Save();
            g.Translate(sx, sy);
            if (P.dead) g.Rotate(-P.face * Mathf.Min(1.5f, P.deadT * 4));
            int fx = P.face;
            var a = P.atk;
            if (a != null && a.kind == "special") fx = Mathf.Cos(a.t * 30) > 0 ? 1 : -1;
            g.Scale(fx, 1);
            if (P.inv > 0 && P.roll == null && Mathf.FloorToInt(P.inv * 18) % 2 == 0) g.Alpha = .45f;

            if (P.roll != null)
            {
                g.Translate(0, -24);
                g.Rotate((.36f - P.roll.t) / .36f * TAU);
                Circ(0, 0, 22, C("#3c6ee8"));
                Circ(8, -6, 14, C("#c9d2de"));
                g.Restore();
                return;
            }
            bool walking = P.moving && P.z <= 0;
            float wt = P.walkT * 13, sw = walking ? Mathf.Sin(wt) * 7 : 0, bob = walking ? Mathf.Abs(Mathf.Sin(wt)) * 2 : 0;
            bool air = P.z > 0;
            RR(-10 + sw * .6f, -24 + (air ? -4 : 0), 9, 24, 3, C("#2e2a44"));
            RR(1 - sw * .6f, -24, 9, 24, 3, C("#2e2a44"));
            RR(-17, -44 - bob, 8, 18, 4, C("#aab4c4"));
            RR(-15, -50 - bob, 30, 30, 8, C("#3c6ee8"));
            RR(-15, -28 - bob, 30, 5, 2, C("#5a3a1e"));
            RR(-3, -46 - bob, 6, 16, 2, C("#ffd34d"), false);
            g.Save(); g.Translate(0, -bob);
            Ell(-9, -90, 7, 13, C("#e8384f"), true, -.5f);
            Circ(0, -68, 21, C("#c9d2de"));
            RR(3, -73, 18, 7, 2, C("#1b1726"));
            g.Rect(11, -73, 2, 15, C("#1b1726"));
            Circ(-10, -62, 2, C("#8a93a3"), false); Circ(-10, -74, 2, C("#8a93a3"), false);
            g.Restore();
            g.Save();
            g.Translate(4, -42 - bob);
            float ang = ArmAngle(P);
            if (a != null && a.phase == "ac" && Weapon.kind == "melee" && a.kind != "special")
            {
                float A0 = a.kind == "air" ? -2.4f : a.kind == "heavy" ? -2.6f : (a.step == 1 ? 1.2f : -2.1f);
                var trail = a.kind == "heavy" ? new Color(1, .86f, .47f, .55f) : new Color(1, 1, 1, .55f);
                g.Arc(0, 0, save.weapon == "hammer" ? 72 : 58, Mathf.Min(A0, ang), Mathf.Max(A0, ang), a.kind == "heavy" ? 16 : 10, trail);
            }
            if (a != null && a.kind == "special") g.EllipseStroke(0, 0, 95, 30, 14, new Color(1, .86f, .47f, .5f));
            g.Rotate(ang);
            RR(0, -4, 16, 8, 4, C("#c9d2de"));
            float pull = 0;
            if (a != null && Weapon.kind == "ranged" && a.kind != "special") pull = a.fired ? 0 : Mathf.Min(1, a.t / Mathf.Max(.01f, a.d.su));
            DrawWeapon(save.weapon, pull);
            g.Restore();
            g.Restore();
        }

        void DrawGoblin(Ent e, float sx, float sy)
        {
            float sc = e.type == "captain" ? 1.35f : 1;
            g.Save();
            g.Translate(sx, sy);
            g.Scale(e.face * sc, sc);
            bool moving = e.state == "move" && e.hurtT <= 0;
            float sw = moving ? Mathf.Sin(e.walkT * 14) * 6 : 0;
            var skin = C("#7cc24a");
            RR(-9 + sw * .6f, -20, 8, 20, 3, C("#4a3520"));
            RR(1 - sw * .6f, -20, 8, 20, 3, C("#4a3520"));
            var body = e.type == "archer" ? C("#3d6b35") : e.type == "captain" ? C("#8d96a8") : C("#7a5230");
            RR(-13, -42, 26, 26, 7, body);
            if (e.type == "captain") RR(-13, -24, 26, 4, 2, C("#c9a33a"), false);
            Poly(skin, -8, -54, -28, -66, -9, -45);
            Poly(skin, 8, -54, 26, -64, 9, -45);
            Circ(0, -52, 15, skin);
            Circ(7, -54, 4, C("#ffe14d"), false); Circ(8, -54, 2, C("#111111"), false);
            g.Rect(4, -45, 3, 3, Color.white); g.Rect(9, -45, 3, 3, Color.white);
            if (e.type == "archer")
            {
                path.Clear();
                for (int i = 0; i <= 12; i++) { float an = Mathf.PI * 1.05f + i / 12f * Mathf.PI * .9f; path.Add(new Vector2(Mathf.Cos(an) * 17, -54 + Mathf.Sin(an) * 17)); }
                g.Shape(path, C("#2f5a2a"));
            }
            if (e.type == "captain")
            {
                path.Clear();
                for (int i = 0; i <= 12; i++) { float an = Mathf.PI + i / 12f * Mathf.PI; path.Add(new Vector2(Mathf.Cos(an) * 16, -55 + Mathf.Sin(an) * 16)); }
                g.Shape(path, C("#8d96a8"));
                Poly(C("#c9a33a"), -4, -70, 0, -84, 4, -70);
            }
            g.Save();
            g.Translate(6, -32);
            float ang = .6f;
            if (e.state == "windup" || e.state == "bashwind") ang = -2.0f;
            else if (e.state == "atk") ang = 1.3f;
            if (e.type == "archer") ang = e.state == "windup" ? -.05f : .4f;
            g.Rotate(ang);
            RR(0, -3, 12, 6, 3, skin);
            if (e.type == "archer")
            {
                g.Arc(8, 0, 22, -1.1f, 1.1f, 4, g.Flash ? Color.white : C("#6b4429"));
                float pull = e.state == "windup" ? Mathf.Min(1, e.t / .6f) : 0;
                float ex = 8 + Mathf.Cos(1.1f) * 22, ey = Mathf.Sin(1.1f) * 22;
                g.Polyline(new[] { ex, -ey, ex - 10 * pull, 0, ex, ey }, 1.2f, C("#eeeeee"));
            }
            else if (e.type == "captain")
            {
                RR(10, -6, 5, 12, 2, C("#b8892f"));
                Poly(C("#d0d6df"), 15, -4, 52, -4, 58, 0, 52, 4, 15, 4);
            }
            else Poly(C("#d0d6df"), 10, -3, 34, -3, 40, 0, 34, 3, 10, 3);
            g.Restore();
            if (e.type == "captain" && e.shieldUp)
            {
                Ell(16, -32, 9, 22, C("#8a5a2b"));
                Ell(16, -32, 4, 9, C("#c0c8d4"), false);
            }
            g.Restore();
        }

        void DrawSlime(Ent e, float sx, float sy, float scale, bool crowned)
        {
            float stretch = Clamp(e.vz / 900, -.35f, .4f);
            float squash = 0;
            if (e.type == "king" && (e.state == "bigwind" || e.state == "wavewind")) squash = Mathf.Sin(e.t * 30) * .05f + .18f;
            float rx = 24 * scale * (1 - stretch * .5f + squash), ry = 20 * scale * (1 + stretch - squash);
            g.Save();
            g.Translate(sx, sy);
            var col = crowned ? (e.ph2 ? C("#7fd04a") : C("#5fd35a")) : C("#6fe06a");
            path.Clear();
            KCPainter.AddBezier(path, new Vector2(-rx, 0), new Vector2(-rx, -ry * 1.9f), new Vector2(rx, -ry * 1.9f), new Vector2(rx, 0), 24);
            g.Shape(path, col);
            Ell(-rx * .35f, -ry * 1.05f, rx * .18f, ry * .25f, CA("#ffffff", .53f), false);
            float f = e.face;
            Circ(f * rx * .25f, -ry * .75f, 4.5f * scale, Color.white, false);
            Circ(f * rx * .55f, -ry * .75f, 4.5f * scale, Color.white, false);
            Circ(f * rx * .28f, -ry * .72f, 2.2f * scale, C("#111111"), false);
            Circ(f * rx * .58f, -ry * .72f, 2.2f * scale, C("#111111"), false);
            if (crowned)
            {
                g.Line(f * rx * .1f, -ry, f * rx * .4f, -ry * .9f, 3, OUT);
                g.Line(f * rx * .72f, -ry, f * rx * .45f, -ry * .9f, 3, OUT);
                float cy = -ry * 1.45f;
                Poly(C("#ffcc4d"), -28, cy, -32, cy - 30, -14, cy - 14, 0, cy - 36, 14, cy - 14, 32, cy - 30, 28, cy);
                Circ(0, cy - 10, 4, C("#e8384f"), false);
            }
            g.Restore();
        }

        void DrawBoar(Ent e, float sx, float sy)
        {
            g.Save();
            g.Translate(sx, sy);
            g.Scale(e.face, 1);
            float runT = e.state == "charge" ? e.walkT * 30 : e.walkT * 14;
            float sw = (e.state == "move" || e.state == "charge") ? Mathf.Sin(runT) * 5 : 0;
            foreach (float lx in new float[] { -22, -10, 10, 22 }) RR(lx - 3 + (lx > 0 ? sw : -sw), -14, 7, 14, 2, C("#4a2e1c"));
            for (int i = 0; i < 5; i++) Poly(C("#2f6a2a"), -24 + i * 10, -36, -20 + i * 10, -52, -16 + i * 10, -36);
            Ell(0, -26, 34, 18, C("#7a4b2e"));
            Ell(30, -24, 14, 12, C("#8a5a38"));
            Ell(42, -22, 5, 6, C("#c98a6a"));
            Poly(C("#fffbe6"), 36, -16, 48, -24, 40, -12);
            Circ(30, -30, 3, e.state == "windup" || e.state == "charge" ? C("#ff3b3b") : C("#111111"), false);
            if (e.state == "dizzy")
                for (int i = 0; i < 3; i++)
                {
                    float an = runT * .2f + i * TAU / 3;
                    Circ(20 + Mathf.Cos(an) * 14, -52 + Mathf.Sin(an) * 4, 3, C("#ffe14d"), false);
                }
            g.Restore();
        }

        void DrawDummy(Ent e, float sx, float sy)
        {
            g.Save(); g.Translate(sx, sy);
            g.Rotate(Mathf.Sin(e.wobble * 18) * e.wobble * .25f);
            RR(-4, -70, 8, 70, 2, C("#6b4429"));
            RR(-22, -56, 44, 8, 3, C("#6b4429"));
            Ell(0, -50, 17, 22, C("#d9c08a"));
            Circ(0, -80, 13, C("#d9c08a"));
            Circ(0, -50, 10, C("#e8384f")); Circ(0, -50, 4, Color.white, false);
            g.Restore();
        }

        void DrawCrate(float sx, float sy)
        {
            RR(sx - 20, sy - 40, 40, 40, 3, C("#a0703c"));
            g.Line(sx - 20, sy - 40, sx + 20, sy, 2, OUT);
            g.Line(sx + 20, sy - 40, sx - 20, sy, 2, OUT);
        }

        void DrawEnemy(Ent e)
        {
            float sx = e.x - camX, sy = e.y - e.z;
            if (sx < -200 || sx > W + 200) return;
            if (e.type == "crate") { if (!e.dead) DrawCrate(sx, sy); return; }
            g.Save();
            if (e.dead)
            {
                if (e.deadT > .6f && Mathf.FloorToInt(e.deadT * 20) % 2 == 1) { g.Restore(); return; }
                g.Alpha = Mathf.Max(0, 1 - e.deadT);
            }
            g.Flash = e.flash > 0;
            switch (e.type)
            {
                case "slime": DrawSlime(e, sx, sy, 1, false); break;
                case "minislime": DrawSlime(e, sx, sy, .7f, false); break;
                case "king": DrawSlime(e, sx, sy, 3.2f, true); break;
                case "boar": DrawBoar(e, sx, sy); break;
                case "dummy": DrawDummy(e, sx, sy); break;
                default: DrawGoblin(e, sx, sy); break;
            }
            g.Flash = false;
            g.Restore();
            if (e.dead || e.prop) return;
            float top = sy - e.h * (e.type == "captain" ? 1.05f : 1) - 14;
            if (e.state == "windup" || e.state == "bashwind" || e.state == "bigwind" || e.state == "wavewind")
                Txt("!", sx, top - 6, 26, C("#ffe14d"));
            if (e.hurtT > .6f)
                for (int i = 0; i < 3; i++)
                {
                    float an = run.time * 6 + i * TAU / 3;
                    Circ(sx + Mathf.Cos(an) * 16, top + 6 + Mathf.Sin(an) * 4, 3, C("#ffe14d"), false);
                }
            if (e.slowT > 0) Ell(sx, sy - e.h / 2, e.w + 8, e.h / 2 + 6, CA("#9fe8ff", .33f), false);
            if (!e.boss && !e.mini && e.hp < e.maxHp)
            {
                const float bw = 36;
                g.Rect(sx - bw / 2, top, bw, 5, CA("#000000", .67f));
                g.Rect(sx - bw / 2, top, bw * Mathf.Max(0, e.hp / e.maxHp), 5, C("#e8384f"));
            }
        }

        void DrawPetBody(string key)
        {
            if (key == "slime")
            {
                path.Clear();
                KCPainter.AddBezier(path, new Vector2(-13, 0), new Vector2(-13, -22), new Vector2(13, -22), new Vector2(13, 0), 14);
                g.Shape(path, C("#6fe06a"));
                Circ(3, -9, 2.5f, C("#111111"), false); Circ(8, -9, 2.5f, C("#111111"), false);
            }
            else
            {
                Ell(0, -10, 14, 9, C("#ff8a3d"));
                Poly(C("#ffb36b"), -12, -12, -28, -20, -24, -4);
                Circ(12, -16, 9, C("#ff8a3d"));
                Poly(C("#ff8a3d"), 8, -22, 10, -32, 14, -23);
                Poly(C("#ff8a3d"), 14, -22, 18, -31, 19, -20);
                Circ(15, -17, 2, C("#111111"), false);
            }
        }

        void DrawPet()
        {
            g.Save();
            g.Translate(pet.x - camX, pet.y - pet.z);
            g.Scale(player.face, 1);
            DrawPetBody(pet.def.key);
            g.Restore();
        }

        void DrawProjectile(Proj p)
        {
            float sx = p.x - camX, sy = p.y - p.z;
            if (p.kind == "arrow" || p.kind == "earrow")
            {
                int d = Sign(p.vx);
                g.Line(sx - d * 22, sy - p.vy * .02f, sx, sy, 3, p.kind == "arrow" ? C("#bfefff") : C("#6b4429"));
                PolyNS(p.kind == "arrow" ? C("#e6f9ff") : C("#c0c8d4"), sx, sy - 4, sx + d * 9, sy, sx, sy + 4);
            }
            else if (p.kind == "blob") Circ(sx, sy, 7, C("#6fe06a"));
            else if (p.kind == "fire") { Circ(sx, sy, 9, C("#ff8a3d"), false); Circ(sx, sy, 5, C("#ffe14d"), false); }
            else if (p.kind == "wave")
            {
                path.Clear();
                KCPainter.AddQuadratic(path, new Vector2(sx - 26, sy), new Vector2(sx, sy - 70), new Vector2(sx + 26, sy), 14);
                g.Shape(path, CA("#6fe06a", .8f));
            }
        }

        void DrawPickup(Pickup k)
        {
            float sx = k.x - camX, sy = k.y - k.z;
            if (k.kind == "coin")
            {
                float w = Mathf.Abs(Mathf.Cos(k.age * 8)) * 7 + 1;
                Ell(sx, sy - 8, w, 8, C("#ffcc4d"));
            }
            else if (k.kind == "food")
            {
                Circ(sx, sy - 12, 11, C("#e8384f"));
                RR(sx - 1, sy - 28, 3, 7, 1, C("#6b4429"), false);
                Ell(sx + 5, sy - 26, 5, 3, C("#4fbf3a"), false, -.5f);
            }
            else if (k.kind == "chest")
            {
                RR(sx - 24, sy - 30, 48, 30, 4, C("#8a5a2b"));
                float open = k.opened ? Mathf.Min(1, k.openT * 4) : 0;
                RR(sx - 26, sy - 40 - open * 14, 52, 14, 5, C("#a0703c"));
                RR(sx - 5, sy - 22, 10, 10, 2, C("#ffcc4d"));
            }
            if (k.kind != "coin" && !k.opened && !player.dead && Mathf.Abs(k.x - player.x) + Mathf.Abs(k.y - player.y) * 1.5f < 80)
            {
                RR(sx - 11, sy - 70, 22, 22, 5, Color.white);
                Txt("E", sx, sy - 53, 15, OUT, 1, false);
            }
        }

        /* ---------------------------------------------------------------- HUD */

        void HudBar(float x, float y, float w, float h, float frac, Color col)
        {
            RR(x, y, w, h, h / 2, CA("#000000", .6f), false);
            if (frac > 0) RR(x, y, Mathf.Max(h, w * Mathf.Clamp01(frac)), h, h / 2, col, false);
        }

        void CdIcon(float x, float y, string label, string key, float frac, Color col)
        {
            Circ(x, y, 20, C("#1d1630"));
            if (frac > 0) g.Pie(x, y, 18, -Mathf.PI / 2, -Mathf.PI / 2 + TAU * frac, CA("#000000", .67f));
            else g.Arc(x, y, 18, 0, TAU, 3, col);
            Txt(key, x, y + 5, 15, frac > 0 ? C("#888888") : Color.white, 1, false, false);
            Txt(label, x, y + 34, 11, Color.white, 1, false, false);
        }

        void DrawHUD()
        {
            var P = player;
            RR(12, 12, 270, 76, 12, CA("#140e22", .8f), false);
            Txt("KNIGHT", 24, 34, 16, C("#ffcc4d"), 0, false, false);
            Txt("Lv " + save.level, 92, 34, 13, Color.white, 0, false, false);
            Txt("● " + save.coins, 270, 34, 15, C("#ffcc4d"), 2, false, false);
            HudBar(24, 42, 246, 14, P.hp / P.maxHp, P.hp / P.maxHp < .3f ? C("#ff4a4a") : C("#e8384f"));
            Txt(Mathf.CeilToInt(P.hp) + " / " + Mathf.RoundToInt(P.maxHp), 147, 53, 11, Color.white, 1, false, false);
            HudBar(24, 62, 246, 6, save.xp / (float)KCData.XpNeed(save.level), C("#5c8cff"));
            Txt(Weapon.name + (pet != null ? " · " + pet.def.name : ""), 24, 81, 10, C("#aaaabb"), 0, false, false);

            CdIcon(320, 40, "Spin", "Q", P.specialCd / 8, C("#ffcc4d"));
            if (pet != null) CdIcon(370, 40, "Fury", "F", pet.fury / KCData.PetFuryCd, pet.def.color);

            if (P.combo >= 2)
            {
                int sz = Mathf.RoundToInt(34 * (1 + Mathf.Min(.5f, P.combo * .02f)));
                Txt(P.combo.ToString(), W - 30, 70, sz, C("#ffcc4d"), 2, true, false);
                Txt("HIT COMBO", W - 30, 88, 13, Color.white, 2, false, false);
            }

            Ent boss = enemies.Find(e => (e.boss || e.mini) && !e.dead);
            if (boss != null)
            {
                const float bw = 520;
                float x = (W - bw) / 2, y = H - 40;
                Txt(KCData.Enemies[boss.type].name.ToUpper() + (boss.ph2 ? "  (ENRAGED)" : ""), W / 2, y - 6, 15, Color.white, 1, true, false);
                HudBar(x, y, bw, 14, boss.hp / boss.maxHp, boss.ph2 ? C("#ff6a2a") : C("#b02e48"));
            }

            if (level.go > 0 && Mathf.FloorToInt(level.go * 3) % 2 == 0) Txt("GO ->", W - 30, 200, 40, C("#ffcc4d"), 2, true, false);

            if (banner > 0)
            {
                g.Alpha = Mathf.Min(1, banner);
                Txt(stageDef.training ? "Hit the dummies. Esc to leave." : "Stage " + stageDef.id, W / 2, 150, 18, Color.white, 1, true, false);
                Txt(stageDef.name.ToUpper(), W / 2, 200, 48, C("#ffcc4d"), 1, true, false);
                g.Alpha = 1;
            }
            if (level.bossIntro > 0)
            {
                g.Alpha = Mathf.Min(1, level.bossIntro);
                Txt("KING SLIME", W / 2, 190, 60, C("#7df07a"), 1, true, false);
                Txt("Ruler of Greenleaf Forest · jump the slime waves!", W / 2, 220, 16, Color.white, 1, true, false);
                g.Alpha = 1;
            }
            if (level.ended && level.bossKilled) Txt("VICTORY!", W / 2, 200, 64, C("#ffcc4d"), 1, true, false);
            if (stageDef.training)
            {
                float now = run.time, tot = 0;
                level.dps.RemoveAll(hh => now - hh.x >= 5);
                foreach (var hh in level.dps) tot += hh.y;
                RR(W - 210, 100, 190, 56, 10, CA("#140e22", .8f), false);
                Txt("DAMAGE, LAST 5 S", W - 196, 120, 12, C("#aaaabb"), 0, false, false);
                Txt(Mathf.RoundToInt(tot) + "  (" + (tot / 5).ToString("0.0") + " DPS)", W - 196, 145, 20, C("#ffcc4d"), 0, false, false);
            }
            if (msg != null)
            {
                g.Alpha = Mathf.Min(1, msgLife * 2);
                Txt(msg, W / 2, 110, 16, Color.white, 1, true, false);
                g.Alpha = 1;
            }
        }

        /* ---------------------------------------------------------------- render */

        struct DrawItem { public float y; public int kind; public object o; }
        readonly List<DrawItem> drawList = new List<DrawItem>(256);

        void Shadow(float x, float y, float r, float z)
        {
            float s = Mathf.Max(.35f, 1 - z / 500);
            Ell(x - camX, y, r * s, r * .3f * s, CA("#000000", .22f), false);
        }

        void Render()
        {
            if (stageDef == null) { g.Rect(0, 0, W, H, C("#120d1c")); return; }
            g.Save();
            shakeX = shake > 0 ? Rand(-shake, shake) : 0;
            shakeY = shake > 0 ? Rand(-shake, shake) : 0;
            g.Translate(shakeX, shakeY);
            DrawBG();

            foreach (var e in enemies) if (!(e.dead && e.type == "crate")) Shadow(e.x, e.y, e.w + 6, e.z);
            Shadow(player.x, player.y, 22, player.z);
            if (pet != null) Shadow(pet.x, pet.y, 10, pet.z);
            foreach (var k in pickups) Shadow(k.x, k.y, 9, k.z);

            drawList.Clear();
            foreach (var e in enemies) drawList.Add(new DrawItem { y = e.y, kind = 0, o = e });
            foreach (var k in pickups) drawList.Add(new DrawItem { y = k.y - .5f, kind = 1, o = k });
            foreach (var p in projectiles) drawList.Add(new DrawItem { y = p.y + .5f, kind = 2, o = p });
            drawList.Add(new DrawItem { y = player.y + .1f, kind = 3, o = player });
            if (pet != null) drawList.Add(new DrawItem { y = pet.y, kind = 4, o = pet });
            drawList.Sort((a, b) => a.y.CompareTo(b.y));
            foreach (var it in drawList)
            {
                switch (it.kind)
                {
                    case 0: DrawEnemy((Ent)it.o); break;
                    case 1: DrawPickup((Pickup)it.o); break;
                    case 2: DrawProjectile((Proj)it.o); break;
                    case 3: DrawKnight((Player)it.o); break;
                    default: DrawPet(); break;
                }
            }

            foreach (var p in particles)
            {
                g.Alpha = Mathf.Clamp01(p.life / p.max * 1.5f);
                g.Rect(p.x - camX - p.size / 2, p.y - p.z - p.size / 2, p.size, p.size, p.color);
            }
            g.Alpha = 1;
            foreach (var t in texts)
            {
                g.Alpha = Mathf.Clamp01(t.life / t.max * 2);
                Txt(t.text, t.x - camX, t.y - t.z, t.size, t.color);
            }
            g.Alpha = 1;
            DrawFG();
            g.Restore();
            DrawHUD();
        }

        /* ---------------------------------------------------------------- menus (IMGUI) */

        string shopTab = "weapons";
        bool confirmReset;
        float gs = 1, gox, goy;
        GUIStyle sLabel, sBtn, sBtnGhost, sBtnStage;
        Texture2D texGold, texGoldHover, texGoldActive, texPanel, texPanel2, texPanel2Hover, texWhite;

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (sLabel != null) return;
            texGold = Tex(C("#f5bd45")); texGoldHover = Tex(C("#ffd866")); texGoldActive = Tex(C("#d8962a"));
            texPanel = Tex(C("#1d1630")); texPanel2 = Tex(C("#271e40")); texPanel2Hover = Tex(C("#352a55"));
            texWhite = Tex(Color.white);
            sLabel = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Overflow };
            sBtn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            sBtn.normal.background = texGold; sBtn.hover.background = texGoldHover; sBtn.active.background = texGoldActive; sBtn.focused.background = texGold;
            sBtn.normal.textColor = sBtn.hover.textColor = sBtn.active.textColor = sBtn.focused.textColor = C("#2b1606");
            sBtnGhost = new GUIStyle(sBtn);
            sBtnGhost.normal.background = texPanel2; sBtnGhost.hover.background = texPanel2Hover; sBtnGhost.active.background = texPanel; sBtnGhost.focused.background = texPanel2;
            sBtnGhost.normal.textColor = sBtnGhost.hover.textColor = sBtnGhost.active.textColor = sBtnGhost.focused.textColor = C("#f3ead5");
            sBtnStage = new GUIStyle(sBtnGhost);
        }

        Rect V(float x, float y, float w, float h) => new Rect(gox + x * gs, goy + y * gs, w * gs, h * gs);

        void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(V(x, y, w, h), texWhite);
            GUI.color = old;
        }

        void Label(string s, float x, float y, float w, float h, int size, Color c, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
        {
            sLabel.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * gs));
            sLabel.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            sLabel.alignment = anchor;
            sLabel.wordWrap = wrap;
            sLabel.normal.textColor = c;
            GUI.Label(V(x, y, w, h), s, sLabel);
        }

        void BigText(string s, float x, float y, float w, float h, int size, Color c, TextAnchor anchor)
        {
            float o = 2.5f / gs * Mathf.Max(1, size / 30f);
            Color shadow = C("#2b1606");
            Label(s, x, y + o * 1.6f, w, h, size, shadow, anchor, true);
            Label(s, x, y, w, h, size, c, anchor, true);
        }

        bool Button(string s, float x, float y, float w, float h, int size, bool primary = true, bool enabled = true)
        {
            var st = primary ? sBtn : sBtnGhost;
            st.fontSize = Mathf.RoundToInt(size * gs);
            bool was = GUI.enabled;
            GUI.enabled = enabled;
            bool r = GUI.Button(V(x, y, w, h), s, st);
            GUI.enabled = was;
            if (r) sfx.Play("coin");
            return r;
        }

        void OnGUI()
        {
            InitStyles();
            gs = Mathf.Min(Screen.width / W, Screen.height / H);
            gox = (Screen.width - W * gs) / 2;
            goy = (Screen.height - H * gs) / 2;

            if (Event.current.type == EventType.Repaint && stageDef != null) DrawGameText();

            switch (state)
            {
                case "title": DrawTitle(); break;
                case "map": DrawMap(); break;
                case "paused": DrawPause(); break;
                case "result": DrawResult(); break;
            }
        }

        void DrawGameText()
        {
            var old = GUI.color;
            foreach (var t in gtexts)
            {
                sLabel.fontSize = Mathf.Max(1, Mathf.RoundToInt(t.size * gs));
                sLabel.fontStyle = FontStyle.Bold;
                sLabel.wordWrap = false;
                sLabel.alignment = t.align == 0 ? TextAnchor.MiddleLeft : t.align == 2 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                float cy = t.y - t.size * .36f;
                float w = 900;
                float x = t.align == 0 ? t.x : t.align == 2 ? t.x - w : t.x - w / 2;
                var r = V(x, cy - t.size, w, t.size * 2);
                GUI.color = new Color(1, 1, 1, t.alpha);
                if (t.outline)
                {
                    sLabel.normal.textColor = OUT;
                    float o = Mathf.Max(1, 2 * gs * Mathf.Max(1, t.size / 24f));
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * o, r.y + dy * o, r.width, r.height), t.s, sLabel);
                }
                sLabel.normal.textColor = t.color;
                GUI.Label(r, t.s, sLabel);
            }
            GUI.color = old;
        }

        void Dim(float a = .9f) => Box(0, 0, W, H, CA("#0d0915", a));

        void DrawTitle()
        {
            Dim(.96f);
            BigText("KINGDOM CHAOS", 0, 40, W, 90, 72, C("#ffcc4d"), TextAnchor.MiddleCenter);
            Label("A CURSED KINGDOM BEAT 'EM UP", 0, 122, W, 24, 16, C("#f3ead5"), TextAnchor.MiddleCenter, true);
            Label("The royal family has been taken and the kingdom is crawling with monsters. Grab a sword, fight through Greenleaf Forest and knock the crown off King Slime.",
                220, 156, 520, 50, 14, C("#a99fc0"), TextAnchor.UpperCenter, false, true);
            string[] keys =
            {
                "A / D", "move", "W / S", "move up / down the lane",
                "Space", "jump, twice to double jump", "Shift", "dodge roll",
                "Left click / J", "light combo", "Right click / K", "heavy attack",
                "S + attack in air", "ground slam", "Q", "Royal Spin",
                "E", "pick up / open chests", "F", "pet fury",
                "Esc", "pause", "M", "mute",
            };
            Box(150, 222, 660, 166, CA("#000000", .4f));
            for (int i = 0; i < keys.Length / 2; i++)
            {
                float x = 170 + (i % 2) * 320, y = 234 + (i / 2) * 24;
                Label(keys[i * 2], x, y, 130, 22, 13, C("#f3ead5"), TextAnchor.MiddleLeft, true);
                Label(keys[i * 2 + 1], x + 132, y, 180, 22, 13, C("#a99fc0"), TextAnchor.MiddleLeft);
            }
            if (Button("PLAY", W / 2 - 90, 410, 180, 52, 24)) OpenMap();
            Label("Enter also works", 0, 470, W, 20, 12, C("#a99fc0"), TextAnchor.MiddleCenter);
        }

        void DrawMap()
        {
            Dim(.97f);
            BigText("KINGDOM CHAOS", 24, 12, 400, 44, 34, C("#ffcc4d"), TextAnchor.MiddleLeft);
            Label("World 1 · Greenleaf Forest", 26, 56, 300, 20, 14, C("#a99fc0"));

            // hero card
            Box(600, 14, 336, 62, C("#1d1630"));
            Label("Lv " + save.level, 614, 18, 80, 30, 24, C("#ffcc4d"), TextAnchor.MiddleLeft, true);
            Label("KNIGHT", 614, 48, 80, 18, 11, C("#a99fc0"));
            int need = KCData.XpNeed(save.level);
            Label("XP " + save.xp + " / " + need, 700, 20, 150, 16, 12, C("#a99fc0"));
            Box(700, 38, 140, 8, CA("#000000", .5f));
            Box(700, 38, 140 * Mathf.Clamp01(save.xp / (float)need), 8, C("#5c8cff"));
            Label(KCData.MaxHpFor(save.level) + " HP · +" + Mathf.RoundToInt((DmgMult - 1) * 100) + "% dmg", 700, 50, 150, 16, 12, C("#a99fc0"));
            Label("● " + save.coins, 846, 20, 84, 40, 16, C("#ffcc4d"), TextAnchor.MiddleRight, true);

            // stages
            Box(24, 88, 440, 322, C("#1d1630"));
            Label("GREENLEAF FOREST", 38, 96, 300, 20, 14, C("#ffcc4d"), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < KCData.Stages.Length; i++)
            {
                var s = KCData.Stages[i];
                bool unlocked = i == 0 || save.cleared.Contains(KCData.Stages[i - 1].id);
                bool done = save.cleared.Contains(s.id);
                float y = 122 + i * 50;
                bool hasMini = false;
                foreach (var wv in s.waves) if (wv.mini) hasMini = true;
                if (Button("", 38, y, 412, 44, 12, false, unlocked)) StartStage(s, i);
                var old = GUI.color;
                if (!unlocked) GUI.color = new Color(1, 1, 1, .4f);
                if (s.boss) Box(38, y, 4, 44, C("#b02e48"));
                Label(s.id, 50, y, 40, 44, 16, C("#ffcc4d"), TextAnchor.MiddleLeft, true);
                Label(s.name, 96, y + 4, 240, 20, 14, C("#f3ead5"), TextAnchor.MiddleLeft, true);
                string hint = s.boss ? "Boss: King Slime" : hasMini ? "Mini-boss: Goblin Captain" : s.waves.Length + " waves";
                Label(hint, 96, y + 23, 240, 16, 11, done ? C("#6fe06a") : C("#a99fc0"), TextAnchor.MiddleLeft);
                Label(done ? "cleared" : unlocked ? "PLAY >" : "locked", 340, y, 100, 44, 12, done ? C("#6fe06a") : C("#a99fc0"), TextAnchor.MiddleRight, true);
                GUI.color = old;
            }
            if (Button("Training Room: test weapons on dummies", 38, 374, 300, 28, 12, false)) StartStage(KCData.Training, -1);

            // shop
            Box(480, 88, 456, 322, C("#1d1630"));
            if (Button("Armory", 494, 96, 90, 26, 13, shopTab == "weapons")) { shopTab = "weapons"; confirmReset = false; }
            if (Button("Pets", 590, 96, 90, 26, 13, shopTab == "pets")) { shopTab = "pets"; confirmReset = false; }
            bool isW = shopTab == "weapons";
            int count = isW ? KCData.Weapons.Length : KCData.Pets.Length;
            for (int i = 0; i < count; i++)
            {
                string key, name, rarity, desc;
                int price;
                if (isW) { var w = KCData.Weapons[i]; key = w.key; name = w.name; rarity = w.rarity; desc = w.desc + "  " + w.stat; price = w.price; }
                else { var p = KCData.Pets[i]; key = p.key; name = p.name; rarity = p.rarity; desc = p.desc; price = p.price; }
                bool owned = isW ? save.weapons.Contains(key) : save.pets.Contains(key);
                bool equipped = isW ? save.weapon == key : save.pet == key;
                float y = 132 + i * 74;
                Box(494, y, 428, 68, C("#271e40"));
                Box(494, y, 4, 68, KCData.RarityColor(rarity));
                Label(name, 508, y + 6, 200, 20, 14, C("#f3ead5"), TextAnchor.MiddleLeft, true);
                Label(rarity.ToUpper(), 640, y + 6, 100, 20, 10, KCData.RarityColor(rarity), TextAnchor.MiddleLeft, true);
                Label(desc, 508, y + 26, 300, 40, 11, C("#a99fc0"), TextAnchor.UpperLeft, false, true);
                if (!owned)
                {
                    if (Button((isW ? "Buy " : "Hatch ") + "● " + price, 816, y + 18, 98, 30, 12, true, save.coins >= price))
                    {
                        save.coins -= price;
                        if (isW) { save.weapons.Add(key); save.weapon = key; } else { save.pets.Add(key); save.pet = key; }
                        sfx.Play("chest"); WriteSave();
                    }
                }
                else if (equipped)
                {
                    if (Button(isW ? "Equipped" : "Unequip", 816, y + 18, 98, 30, 12, false, !isW)) { save.pet = ""; WriteSave(); }
                }
                else if (Button("Equip", 816, y + 18, 98, 30, 12, false))
                {
                    if (isW) save.weapon = key; else save.pet = key;
                    WriteSave();
                }
            }
            if (!isW) Label("One pet at a time. Pets attack on their own; press F for a three-shot Fury (20 s cooldown).", 494, 284, 428, 40, 11, C("#a99fc0"), TextAnchor.UpperLeft, false, true);

            // future worlds
            Box(24, 420, 912, 92, C("#1d1630"));
            Label("THE ROAD AHEAD", 38, 426, 300, 18, 13, C("#ffcc4d"), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < KCData.FutureWorlds.GetLength(0); i++)
            {
                float x = 38 + i * 128;
                Box(x, 448, 122, 58, C("#271e40"));
                Label("World " + (i + 2) + " · locked", x + 6, 450, 116, 14, 10, C("#a99fc0"));
                Label(KCData.FutureWorlds[i, 0], x + 6, 464, 116, 16, 11, C("#f3ead5"), TextAnchor.UpperLeft, true);
                Label("Boss: " + KCData.FutureWorlds[i, 1], x + 6, 484, 116, 16, 10, C("#a99fc0"));
            }

            Label("Progress saves on this device.", 26, 516, 300, 18, 11, C("#a99fc0"), TextAnchor.MiddleLeft);
            if (Button("Controls", 720, 514, 90, 22, 11, false)) { state = "title"; confirmReset = false; }
            if (Button(confirmReset ? "Click to confirm" : "Reset save", 816, 514, 120, 22, 11, false))
            {
                if (confirmReset) { save = new SaveData(); WriteSave(); confirmReset = false; }
                else confirmReset = true;
            }
        }

        void DrawPause()
        {
            Dim(.75f);
            BigText("PAUSED", 0, 170, W, 70, 56, C("#ffcc4d"), TextAnchor.MiddleCenter);
            if (Button("Resume", W / 2 - 180, 260, 170, 46, 18)) ResumeGame();
            if (Button(stageDef != null && stageDef.training ? "Leave training" : "Retreat to map", W / 2 + 10, 260, 170, 46, 16, false)) { WriteSave(); OpenMap(); }
            Label("Retreating keeps the coins and XP you've picked up.", 0, 320, W, 20, 13, C("#a99fc0"), TextAnchor.MiddleCenter);
        }

        bool resWon, resFirst, resAllDone;
        int resBonus;

        void DrawResult()
        {
            Dim(.8f);
            BigText(resWon ? (resAllDone ? "WORLD 1 CLEARED!" : "STAGE CLEAR!") : "YOU FELL", 0, 70, W, 70, 52, resWon ? C("#ffcc4d") : C("#e8384f"), TextAnchor.MiddleCenter);
            string sub = resAllDone ? "King Slime is down and Greenleaf Forest is free. The Forgotten Graveyard waits beyond the hills..."
                : !resWon ? "You keep every coin and bit of XP you picked up. Level up or try a different weapon, then go again." : "";
            Label(sub, 230, 140, 500, 40, 13, C("#a99fc0"), TextAnchor.UpperCenter, false, true);
            int mins = Mathf.FloorToInt(run.time / 60), secs = Mathf.FloorToInt(run.time % 60);
            var rows = new List<string[]>
            {
                new[] { "Enemies defeated", run.kills.ToString() },
                new[] { "Best combo", run.maxCombo.ToString() },
                new[] { "Coins collected", "● " + (run.coins - resBonus) },
            };
            if (resWon) rows.Add(new[] { "Clear bonus", "● " + resBonus + (resFirst ? " + 50 XP" : "") });
            rows.Add(new[] { "XP earned", run.xp.ToString() });
            rows.Add(new[] { "Time", mins + ":" + secs.ToString("00") });
            Box(330, 186, 300, rows.Count * 24 + 20, CA("#000000", .45f));
            for (int i = 0; i < rows.Count; i++)
            {
                Label(rows[i][0], 346, 196 + i * 24, 160, 22, 14, C("#f3ead5"), TextAnchor.MiddleLeft);
                Label(rows[i][1], 470, 196 + i * 24, 144, 22, 14, C("#ffcc4d"), TextAnchor.MiddleRight, true);
            }
            float by = 210 + rows.Count * 24 + 20;
            StageDef next = resWon && stageIdx >= 0 && stageIdx + 1 < KCData.Stages.Length ? KCData.Stages[stageIdx + 1] : null;
            float bx = next != null ? W / 2 - 330 : W / 2 - 160;
            if (next != null && Button("Next: " + next.id + " " + next.name, bx, by, 280, 44, 15)) { StartStage(next, stageIdx + 1); return; }
            if (next != null) bx += 290;
            if (Button(resWon ? "Replay" : "Retry", bx, by, 150, 44, 15, next == null)) { StartStage(stageDef, stageIdx); return; }
            if (Button("World map", bx + 160, by, 150, 44, 15, false)) OpenMap();
        }

        /* ---------------------------------------------------------------- flow */

        void OpenMap()
        {
            state = "map";
            confirmReset = false;
            WriteSave();
        }

        void EndStage(bool won)
        {
            if (state != "play") return;
            state = "result";
            resBonus = 0; resFirst = false;
            if (won && !stageDef.training)
            {
                resFirst = !save.cleared.Contains(stageDef.id);
                resBonus = 25 + 15 * stageIdx;
                GainCoins(resBonus);
                if (resFirst) { save.cleared.Add(stageDef.id); GainXp(50); }
            }
            resWon = won;
            resAllDone = won && stageDef.boss;
            WriteSave();
        }

        void PauseGame() { state = "paused"; }
        void ResumeGame() { state = "play"; pressed.Clear(); }
    }
}
