namespace Flopzone
{
    public static partial class GunShapes
    {
        static void Pick(GunMesh g, string id)
        {
            switch (id)
            {
                case "flop9": Glock(g); break;
                case "deagle": Deagle(g); break;
                case "buzz": Uzi(g); break;
                case "revolt": Revolver(g); break;
                case "squeak": TargetPistol(g); break;
                case "hammer": Beretta93R(g); break;
                case "boom": Remington870(g, false); break;
                case "slug": Remington870(g, true); break;
                case "sawed": SawedOff(g); break;
                case "thunder": BenelliM4(g); break;
                case "brute": Aa12(g); break;
                case "longshot": BoltRifle(g, GM.Olive, false); break;
                case "elephant": BoltRifle(g, GM.Wood, true); break;
                case "needle": Awm(g); break;
                case "whisper": Vss(g); break;
                case "marksman": Sr25(g); break;
                case "bulldog": Aug(g); break;
                case "viper": Ak(g); break;
                case "bravo": Scar(g); break;
                case "hmr": M249(g); break;
                case "spitfire": Vector(g); break;
                default: Ar15(g); break;
            }
        }

        static void PickMag(GunMesh g, string id)
        {
            switch (id)
            {
                case "flop9": PistolMag(g, 0.112f, 0.03f, GM.Polymer); break;
                case "deagle": PistolMag(g, 0.118f, 0.036f, GM.Chrome); break;
                case "squeak": PistolMag(g, 0.104f, 0.026f, GM.Metal); break;
                case "hammer": PistolMag(g, 0.17f, 0.03f, GM.Dark); break;
                case "buzz": PistolMag(g, 0.2f, 0.034f, GM.Dark); break;
                case "spitfire": PistolMag(g, 0.17f, 0.03f, GM.Polymer); break;
                case "revolt": SpeedLoader(g); break;
                case "viper": AkMag(g); break;
                case "bravo": Stanag(g, GM.Tan); break;
                case "bulldog": Stanag(g, GM.Polymer); break;
                case "marksman": BoxMag(g, 0.11f, 0.068f, GM.Tan, true); break;
                case "whisper": BoxMag(g, 0.075f, 0.05f, GM.Dark, true); break;
                case "longshot": case "elephant": case "needle": BoxMag(g, 0.055f, 0.08f, GM.Dark, false); break;
                case "brute": Drum(g); break;
                case "hmr": AmmoPouch(g); break;
                case "boom": case "slug": case "thunder": ShellHolder(g, 4); break;
                case "sawed": ShellHolder(g, 2); break;
                default: Stanag(g); break;
            }
        }

        /* ======================= pistols ======================= */

        static void PistolAnchors(GunMesh g, float boreY, float muzzleZ, float gripLean)
        {
            g.Anchor("muzzle", boreY, muzzleZ + 0.006f);
            g.Anchor("magwell", 0.026f, -0.004f, gripLean);
            g.Anchor("foregrip", -0.045f, 0.03f);
            g.Anchor("eject", boreY + 0.006f, 0.05f, 0, 0.016f);
            g.Anchor("ammo", boreY, -0.03f, 0, 0.02f);
        }

        static void Serrations(GunMesh g, float w, float y0, float y1, float z0, int n, float step)
        {
            for (int i = 0; i < n; i++)
                foreach (float s in new[] { -1f, 1f })
                    g.Box(GM.Port, s * w * 0.5f, (y0 + y1) * 0.5f, z0 + i * step, 0.0012f, y1 - y0, 0.0018f, 0.0002f);
        }

        static void Glock(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.028f, 0.1f, 0.036f, 0.029f, 0.042f);
            g.Profile(GM.Polymer, 0, 0.026f, 0.003f,
                -0.044f, 0.03f, 0.13f, 0.03f, 0.13f, 0.014f, 0.07f, 0.013f, 0.064f, 0.02f, 0.014f, 0.022f, -0.032f, 0.022f, -0.046f, 0.027f);
            for (int i = 0; i < 3; i++) g.Box(GM.Port, 0, 0.0145f, 0.088f + i * 0.012f, 0.02f, 0.003f, 0.005f, 0.0005f);   // rail slots
            TriggerGuard(g, 0.02f, 0.012f, 0.07f, GM.Polymer, true);
            g.Profile(GM.Dark, 0, 0.0255f, 0.0035f,
                -0.047f, 0.03f, 0.142f, 0.03f, 0.142f, 0.055f, 0.136f, 0.061f, -0.041f, 0.061f, -0.047f, 0.055f);
            Serrations(g, 0.0255f, 0.034f, 0.056f, -0.04f, 7, 0.0045f);
            g.Box(GM.Port, 0.0118f, 0.056f, 0.048f, 0.003f, 0.012f, 0.044f, 0.0005f);      // ejection port
            g.Box(GM.Metal, 0.009f, 0.058f, 0.048f, 0.006f, 0.006f, 0.04f, 0.0008f);       // barrel hood
            g.Box(GM.Dark, -0.0135f, 0.032f, 0.045f, 0.003f, 0.006f, 0.022f, 0.0005f);     // slide stop
            IronSights(g, 0.061f, 0.132f, -0.036f);
            g.Bore(0.045f, 0.142f, 0.0045f);
            PistolAnchors(g, 0.045f, 0.142f, 20f);
        }

        static void Deagle(GunMesh g)
        {
            PistolGrip(g, GM.Rubber, 0.03f, 0.112f, 0.03f, 0.034f, 0.047f, false);
            g.Profile(GM.Chrome, 0, 0.03f, 0.003f,
                -0.045f, 0.032f, 0.215f, 0.032f, 0.215f, 0.014f, 0.078f, 0.01f, 0.068f, 0.022f, 0.014f, 0.024f, -0.036f, 0.022f);
            TriggerGuard(g, 0.024f, 0.012f, 0.074f, GM.Chrome);
            g.Profile(GM.Chrome, 0, 0.034f, 0.0035f,
                -0.052f, 0.032f, 0.088f, 0.032f, 0.088f, 0.075f, -0.046f, 0.075f, -0.052f, 0.068f);
            g.Profile(GM.Chrome, 0, 0.03f, 0.004f, 0.086f, 0.034f, 0.224f, 0.034f, 0.224f, 0.07f, 0.086f, 0.074f);
            g.Profile(GM.Chrome, 0, 0.014f, 0.003f, 0.0f, 0.074f, 0.22f, 0.07f, 0.22f, 0.078f, 0.0f, 0.082f);   // the triangular rib
            for (int i = 0; i < 6; i++) g.Box(GM.Port, 0, 0.079f - i * 0.0006f, 0.095f + i * 0.02f, 0.0145f, 0.002f, 0.009f, 0.0003f);
            Serrations(g, 0.034f, 0.038f, 0.07f, -0.045f, 8, 0.005f);
            g.Profile(GM.Chrome, 0, 0.01f, 0.002f, -0.05f, 0.06f, -0.062f, 0.078f, -0.056f, 0.082f, -0.044f, 0.07f);   // hammer
            g.Box(GM.Port, 0.0165f, 0.062f, 0.04f, 0.002f, 0.014f, 0.05f, 0.0004f);
            IronSights(g, 0.082f, 0.212f, -0.04f, GM.Chrome);
            g.Bore(0.052f, 0.224f, 0.0065f);
            PistolAnchors(g, 0.052f, 0.224f, 17f);
        }

        static void Uzi(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.03f, 0.11f, 0.008f, 0.034f, 0.045f, false);
            g.Box(GM.Dark, 0, -0.01f, -0.024f, 0.03f, 0.05f, 0.01f, 0.002f);   // grip safety
            g.Profile(GM.Dark, 0, 0.046f, 0.004f, -0.1f, 0.03f, 0.172f, 0.03f, 0.172f, 0.086f, -0.095f, 0.088f, -0.1f, 0.082f);
            for (int i = 0; i < 10; i++) g.Box(GM.Metal, 0, 0.09f, -0.08f + i * 0.026f, 0.044f, 0.005f, 0.012f, 0.0015f);   // ribbed top cover
            g.Ball(GM.Metal, 0, 0.095f, 0.02f, 0.008f, 0.007f, 0.012f, 10);                                                      // cocking knob
            foreach (float s in new[] { -1f, 1f })
            {
                g.Box(GM.Dark, s * 0.012f, 0.1f, 0.155f, 0.005f, 0.022f, 0.012f, 0.001f);   // front sight ears
                g.Box(GM.Dark, s * 0.01f, 0.098f, -0.085f, 0.005f, 0.018f, 0.01f, 0.001f);
                for (int i = 0; i < 6; i++) g.Box(GM.Port, s * 0.0232f, 0.058f, -0.07f + i * 0.04f, 0.0012f, 0.035f, 0.018f, 0.0003f);
            }
            g.Box(GM.Dark, 0, 0.096f, 0.155f, 0.003f, 0.016f, 0.004f, 0.0005f);
            g.TubeZ(GM.Dark, 0, 0.06f, 0.172f, 0.19f, 0.016f, 0.016f, 16);   // barrel nut
            g.TubeZ(GM.Metal, 0, 0.06f, 0.19f, 0.235f, 0.0085f, -1, 14);
            g.Bore(0.06f, 0.235f, 0.0045f);
            TriggerGuard(g, 0.03f, 0.02f, 0.09f, GM.Dark, true);
            g.Profile(GM.Dark, 0, 0.044f, 0.004f, -0.1f, 0.04f, -0.125f, 0.04f, -0.125f, 0.075f, -0.1f, 0.078f);   // stock hinge block
            g.Box(GM.Port, 0.0232f, 0.07f, 0.06f, 0.0012f, 0.016f, 0.04f, 0.0003f);
            g.Anchor("muzzle", 0.06f, 0.245f);
            g.Anchor("magwell", 0.025f, -0.003f, 6f);
            g.Anchor("foregrip", 0.0f, 0.12f);
            g.Anchor("eject", 0.07f, 0.06f, 0, 0.024f);
            g.Anchor("ammo", 0.06f, -0.08f, 0, 0.025f);
        }

        static void Revolver(GunMesh g)
        {
            PistolGrip(g, GM.Wood, 0.028f, 0.1f, 0.045f, 0.034f, 0.044f, false);
            g.Profile(GM.Metal, 0, 0.028f, 0.003f,
                -0.044f, 0.028f, -0.052f, 0.052f, -0.036f, 0.074f, 0.074f, 0.076f, 0.074f, 0.03f, 0.064f, 0.024f, 0.014f, 0.022f);
            g.TubeZ(GM.Metal, 0, 0.049f, 0.003f, 0.052f, 0.0225f, -1, 24);   // cylinder
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * (float)System.Math.PI / 180f;
                g.Box(GM.Port, (float)System.Math.Cos(a) * 0.0222f, 0.049f + (float)System.Math.Sin(a) * 0.0222f, 0.028f, 0.004f, 0.004f, 0.03f, 0.0008f);
                float b = i * 60f * (float)System.Math.PI / 180f;
                g.TubeZ(GM.Brass, (float)System.Math.Cos(b) * 0.0135f, 0.049f + (float)System.Math.Sin(b) * 0.0135f, 0.0525f, 0.053f, 0.0048f, -1, 8);
            }
            g.TubeZ(GM.Metal, 0, 0.062f, 0.074f, 0.215f, 0.009f, -1, 16);    // barrel
            g.Profile(GM.Metal, 0, 0.02f, 0.003f, 0.074f, 0.036f, 0.21f, 0.044f, 0.21f, 0.064f, 0.074f, 0.064f);   // full underlug
            g.Box(GM.Metal, 0, 0.074f, 0.145f, 0.009f, 0.006f, 0.14f, 0.0012f);   // vent rib
            g.Profile(GM.Metal, 0, 0.005f, 0.001f, 0.188f, 0.076f, 0.21f, 0.076f, 0.21f, 0.087f, 0.2f, 0.087f);
            g.Box(GM.Accent, 0, 0.083f, 0.205f, 0.0055f, 0.006f, 0.006f, 0.0005f);
            g.TubeZ(GM.Metal, 0, 0.034f, 0.052f, 0.09f, 0.004f, -1, 8);           // ejector rod
            g.Profile(GM.Metal, 0, 0.008f, 0.0015f, -0.042f, 0.066f, -0.054f, 0.078f, -0.05f, 0.082f, -0.036f, 0.072f);   // hammer
            TriggerGuard(g, 0.024f, 0.006f, 0.066f, GM.Metal);
            g.Box(GM.Dark, -0.0142f, 0.05f, -0.012f, 0.002f, 0.01f, 0.012f, 0.0003f);   // cylinder release
            g.Bore(0.062f, 0.215f, 0.0045f);
            g.Anchor("muzzle", 0.062f, 0.222f);
            g.Anchor("magwell", 0.049f, 0.045f, 90f);   // the speed loader's rounds point into the cylinder
            g.Anchor("foregrip", -0.045f, 0.03f);
            g.Anchor("eject", 0.05f, 0.028f, 0, 0.02f);
            g.Anchor("ammo", 0.07f, -0.03f, 0, 0.018f);
        }

        static void TargetPistol(GunMesh g)
        {
            PistolGrip(g, GM.Wood, 0.03f, 0.104f, 0.048f, 0.031f, 0.043f, false);
            g.Profile(GM.Metal, 0, 0.026f, 0.003f, -0.05f, 0.036f, 0.065f, 0.036f, 0.065f, 0.026f, 0.05f, 0.016f, 0.014f, 0.022f, -0.042f, 0.02f);
            TriggerGuard(g, 0.022f, 0.012f, 0.064f, GM.Metal);
            g.TubeZ(GM.Metal, 0, 0.052f, -0.062f, 0.065f, 0.0165f, -1, 20);   // round receiver
            g.Box(GM.Dark, 0, 0.052f, -0.068f, 0.03f, 0.008f, 0.01f, 0.0015f);   // bolt ears
            g.TubeZ(GM.Metal, 0, 0.052f, 0.065f, 0.205f, 0.0115f, -1, 18);     // bull barrel
            g.Box(GM.Accent, 0, 0.069f, 0.197f, 0.003f, 0.012f, 0.008f, 0.0005f);
            g.Box(GM.Dark, 0, 0.071f, -0.05f, 0.016f, 0.008f, 0.012f, 0.0012f);   // adjustable rear sight
            g.Box(GM.Port, 0.0158f, 0.055f, 0.02f, 0.002f, 0.012f, 0.03f, 0.0004f);
            g.Bore(0.052f, 0.205f, 0.003f);
            PistolAnchors(g, 0.052f, 0.205f, 25f);
        }

        static void Beretta93R(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.028f, 0.1f, 0.03f, 0.03f, 0.042f);
            g.Profile(GM.Dark, 0, 0.027f, 0.003f, -0.044f, 0.03f, 0.14f, 0.03f, 0.14f, 0.016f, 0.075f, 0.012f, 0.066f, 0.02f, 0.014f, 0.022f, -0.034f, 0.022f);
            TriggerGuard(g, 0.022f, 0.012f, 0.075f, GM.Dark, true);
            g.Profile(GM.Dark, 0, 0.026f, 0.003f,
                -0.05f, 0.03f, 0.156f, 0.03f, 0.156f, 0.056f, 0.122f, 0.058f, 0.116f, 0.05f, 0.036f, 0.05f, 0.03f, 0.062f, -0.044f, 0.062f, -0.05f, 0.056f);
            g.TubeZ(GM.Metal, 0, 0.047f, 0.03f, 0.12f, 0.0075f, -1, 14);   // barrel showing through the open slide
            Serrations(g, 0.026f, 0.034f, 0.058f, -0.044f, 6, 0.0045f);
            g.Profile(GM.Dark, 0, 0.028f, 0.003f, 0.156f, 0.03f, 0.2f, 0.032f, 0.2f, 0.06f, 0.156f, 0.058f);   // compensator
            for (int i = 0; i < 3; i++) g.Box(GM.Port, 0, 0.0605f, 0.167f + i * 0.011f, 0.012f, 0.002f, 0.005f, 0.0003f);
            g.Profile(GM.Dark, 0, 0.012f, 0.002f, 0.096f, 0.016f, 0.114f, 0.016f, 0.114f, -0.038f, 0.096f, -0.034f);   // folding foregrip
            g.Box(GM.Dark, 0, -0.04f, 0.1f, 0.03f, 0.007f, 0.016f, 0.002f);
            g.Box(GM.Accent, -0.014f, 0.038f, -0.02f, 0.003f, 0.008f, 0.01f, 0.0005f);   // burst selector
            IronSights(g, 0.062f, 0.112f, -0.04f);
            g.Bore(0.047f, 0.2f, 0.0045f);
            PistolAnchors(g, 0.047f, 0.2f, 20f);
            g.Anchor("foregrip", -0.015f, 0.105f);
        }

        /* ======================= shotguns ======================= */

        static void ShotgunAnchors(GunMesh g, float boreY, float muzzleZ, float foreY, float foreZ, float wellY, float wellZ)
        {
            g.Anchor("muzzle", boreY, muzzleZ);
            g.Anchor("magwell", wellY, wellZ);
            g.Anchor("foregrip", foreY, foreZ);
            g.Anchor("eject", boreY, 0.09f, 0, 0.02f);
            g.Anchor("ammo", boreY, -0.03f, 0, 0.022f);
        }

        static void Remington870(GunMesh g, bool slug)
        {
            var wood = slug ? GM.Polymer : GM.Wood;
            var steel = slug ? GM.Dark : GM.Metal;
            float by = 0.072f;
            RifleStock(g, by, 0.37f, wood);
            g.Profile(steel, 0, 0.036f, 0.004f,
                -0.012f, 0.036f, 0.205f, 0.036f, 0.21f, 0.05f, 0.21f, 0.09f, 0.18f, 0.096f, 0.02f, 0.098f, -0.012f, 0.088f);
            g.Box(GM.Port, 0, 0.035f, 0.1f, 0.026f, 0.002f, 0.07f, 0.0004f);              // loading port
            g.Box(GM.Port, 0.0182f, 0.075f, 0.09f, 0.002f, 0.02f, 0.06f, 0.0004f);        // ejection port
            TriggerGuard(g, 0.036f, 0.02f, 0.08f, steel);
            g.Tube(GM.Dark, new P3(-0.019f, 0.03f, 0.07f), new P3(0.019f, 0.03f, 0.07f), 0.0045f, 0.0045f, 10);   // cross-bolt safety
            float end = slug ? 0.72f : 0.69f;
            g.TubeZ(steel, 0, by, 0.205f, end, 0.0125f, -1, 18);
            g.Bore(by, end, 0.009f);
            g.TubeZ(steel, 0, 0.044f, 0.205f, end - 0.08f, 0.011f, -1, 16);              // magazine tube
            g.TubeZ(steel, 0, 0.044f, end - 0.08f, end - 0.055f, 0.0125f, -1, 16);
            g.Box(steel, 0, 0.058f, end - 0.07f, 0.008f, 0.02f, 0.01f, 0.0015f);          // barrel ring
            if (slug)
            {
                g.Box(GM.Dark, 0, by + 0.016f, 0.45f, 0.03f, 0.008f, 0.42f, 0.002f);   // heat shield
                for (int i = 0; i < 10; i++) g.Box(GM.Port, 0, by + 0.0205f, 0.27f + i * 0.04f, 0.022f, 0.002f, 0.016f, 0.0003f);
                Rail(g, 0.098f, 0.02f, 0.17f);
                RedDot(g, 0.1055f, 0.1f);
                IronSights(g, by + 0.02f, end - 0.02f, 0.0f);
            }
            else
            {
                g.Box(steel, 0, by + 0.0145f, 0.45f, 0.008f, 0.004f, end - 0.21f, 0.001f);   // vent rib
                g.Ball(GM.Brass, 0, by + 0.019f, end - 0.012f, 0.003f, 0.003f, 0.003f, 8);
            }
            g.group = "pump";
            g.Profile(wood, 0, 0.044f, 0.008f, 0.27f, 0.025f, 0.44f, 0.025f, 0.445f, 0.04f, 0.44f, 0.063f, 0.27f, 0.063f, 0.265f, 0.04f);
            for (int i = 0; i < 9; i++) g.Box(slug ? GM.Rubber : GM.DarkWood, 0, 0.025f, 0.29f + i * 0.017f, 0.04f, 0.003f, 0.006f, 0.001f);
            g.group = "";
            g.Anchor("pump", 0.044f, 0.355f);
            ShotgunAnchors(g, by, end + 0.01f, 0.02f, 0.355f, 0.03f, 0.1f);
        }

        static void SawedOff(GunMesh g)
        {
            PistolGrip(g, GM.Wood, 0.04f, 0.1f, 0.04f, 0.036f, 0.05f);
            g.Profile(GM.Wood, 0, 0.038f, 0.009f, 0.0f, 0.03f, -0.045f, 0.03f, -0.05f, 0.05f, -0.03f, 0.072f, 0.0f, 0.075f);   // wrist into the action
            g.Profile(GM.Metal, 0, 0.044f, 0.004f, -0.012f, 0.03f, 0.08f, 0.03f, 0.08f, 0.082f, 0.0f, 0.082f, -0.012f, 0.072f);   // action
            TriggerGuard(g, 0.03f, 0.004f, 0.066f, GM.Metal);
            foreach (float s in new[] { -1f, 1f })
            {
                g.TubeZ(GM.Metal, s * 0.0125f, 0.064f, 0.08f, 0.33f, 0.0125f, -1, 18);
                g.Bore(0.064f, 0.33f, 0.0095f, s * 0.0125f);
                g.Profile(GM.Metal, s * 0.006f, 0.005f, 0.001f, -0.03f, 0.074f, -0.016f, 0.074f, -0.014f, 0.09f, -0.03f, 0.094f);   // hammers
            }
            g.Box(GM.Metal, 0, 0.078f, 0.2f, 0.008f, 0.006f, 0.25f, 0.0012f);     // rib between the barrels
            g.Box(GM.Metal, 0, 0.05f, 0.2f, 0.006f, 0.006f, 0.25f, 0.0012f);
            g.Profile(GM.Wood, 0, 0.046f, 0.01f, 0.085f, 0.02f, 0.2f, 0.024f, 0.228f, 0.04f, 0.225f, 0.054f, 0.085f, 0.054f);   // forend
            g.Tube(GM.Dark, new P3(-0.023f, 0.05f, 0.075f), new P3(0.023f, 0.05f, 0.075f), 0.008f, 0.008f, 12);    // hinge pin
            g.Box(GM.Metal, 0, 0.086f, 0.0f, 0.01f, 0.006f, 0.03f, 0.0015f);       // top lever
            g.Anchor("muzzle", 0.064f, 0.34f);
            g.Anchor("magwell", 0.03f, 0.04f);
            g.Anchor("foregrip", 0.018f, 0.15f);
            g.Anchor("eject", 0.064f, 0.08f, 0, 0.02f);
            g.Anchor("ammo", 0.07f, -0.04f, 0, 0.024f);
        }

        static void BenelliM4(GunMesh g)
        {
            float by = 0.074f;
            PistolGrip(g, GM.Polymer, 0.035f);
            M4Stock(g, by, -0.01f, GM.Polymer);
            g.Profile(GM.Dark, 0, 0.036f, 0.004f, -0.012f, 0.035f, 0.215f, 0.035f, 0.22f, 0.05f, 0.215f, 0.092f, -0.006f, 0.094f, -0.012f, 0.086f);
            Rail(g, 0.094f, 0.0f, 0.2f);
            g.Profile(GM.Dark, 0, 0.026f, 0.002f, 0.02f, 0.103f, 0.045f, 0.103f, 0.042f, 0.124f, 0.024f, 0.124f);
            g.Tube(GM.Dark, new P3(0, 0.117f, 0.03f), new P3(0, 0.117f, 0.036f), 0.009f, 0.009f, 14);   // ghost ring
            g.Box(GM.Port, 0.0182f, 0.07f, 0.1f, 0.002f, 0.022f, 0.065f, 0.0004f);
            g.Box(GM.Accent, 0.019f, 0.05f, 0.05f, 0.004f, 0.01f, 0.018f, 0.001f);   // bolt release
            TriggerGuard(g, 0.035f, 0.004f, 0.064f, GM.Dark);
            g.TubeZ(GM.Dark, 0, by, 0.215f, 0.66f, 0.0115f, -1, 18);
            g.TubeZ(GM.Dark, 0, 0.046f, 0.215f, 0.58f, 0.011f, -1, 16);
            g.TubeZ(GM.Dark, 0, 0.046f, 0.58f, 0.6f, 0.0125f, -1, 16);
            g.Profile(GM.Polymer, 0, 0.046f, 0.008f, 0.23f, 0.03f, 0.42f, 0.03f, 0.425f, 0.05f, 0.42f, 0.066f, 0.23f, 0.066f);
            for (int i = 0; i < 6; i++) g.Box(GM.Rubber, 0, 0.048f, 0.25f + i * 0.028f, 0.048f, 0.032f, 0.005f, 0.001f);
            g.Profile(GM.Dark, 0, 0.012f, 0.002f, 0.635f, by + 0.01f, 0.65f, by + 0.01f, 0.646f, by + 0.03f, 0.638f, by + 0.03f);
            g.Bore(by, 0.66f, 0.009f);
            ShotgunAnchors(g, by, 0.67f, 0.025f, 0.32f, 0.033f, 0.1f);
        }

        static void Aa12(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.035f, 0.1f, 0.02f, 0.032f, 0.042f, false);
            g.Profile(GM.Dark, 0, 0.052f, 0.005f,
                -0.29f, -0.03f, -0.29f, 0.1f, -0.26f, 0.112f, 0.24f, 0.112f, 0.255f, 0.1f, 0.255f, 0.035f, -0.04f, 0.035f, -0.12f, 0.03f, -0.26f, -0.03f);
            g.Profile(GM.Rubber, 0, 0.054f, 0.004f, -0.305f, -0.035f, -0.288f, -0.035f, -0.288f, 0.106f, -0.305f, 0.102f);
            for (int i = 0; i < 7; i++) g.Box(GM.Port, 0.0262f, 0.075f, -0.18f + i * 0.055f, 0.0012f, 0.045f, 0.02f, 0.0003f);
            // carry handle with sights
            g.Profile(GM.Metal, 0, 0.014f, 0.002f, -0.04f, 0.112f, -0.025f, 0.112f, -0.025f, 0.15f, 0.13f, 0.15f, 0.13f, 0.112f, 0.145f, 0.112f, 0.145f, 0.162f, -0.04f, 0.162f);
            g.TubeZ(GM.Dark, 0, 0.074f, 0.255f, 0.44f, 0.019f, -1, 18);
            for (int i = 0; i < 5; i++) g.Box(GM.Port, 0, 0.093f, 0.28f + i * 0.032f, 0.02f, 0.002f, 0.014f, 0.0004f);
            g.TubeZ(GM.Dark, 0, 0.074f, 0.44f, 0.47f, 0.022f, -1, 18);
            g.Bore(0.074f, 0.47f, 0.01f);
            g.Profile(GM.Polymer, 0, 0.054f, 0.008f, 0.26f, 0.03f, 0.43f, 0.03f, 0.43f, 0.06f, 0.26f, 0.06f);
            TriggerGuard(g, 0.035f, 0.004f, 0.07f, GM.Dark, true);
            g.Box(GM.Accent, 0.027f, 0.098f, 0.1f, 0.003f, 0.012f, 0.05f, 0.001f);
            g.Anchor("muzzle", 0.074f, 0.48f);
            g.Anchor("magwell", 0.03f, 0.13f);
            g.Anchor("foregrip", 0.02f, 0.34f);
            g.Anchor("eject", 0.08f, 0.06f, 0, 0.026f);
            g.Anchor("ammo", 0.09f, -0.06f, 0, 0.027f);
        }

        /* ======================= rifles ======================= */

        static void RifleAnchors(GunMesh g, float boreY, float muzzleZ, float foreY, float foreZ, float wellY, float wellZ, float wellAngle = 0f)
        {
            g.Anchor("muzzle", boreY, muzzleZ);
            g.Anchor("magwell", wellY, wellZ, wellAngle);
            g.Anchor("foregrip", foreY, foreZ);
            g.Anchor("eject", boreY, 0.04f, 0, 0.02f);
            g.Anchor("ammo", boreY, -0.06f, 0, 0.022f);
        }

        static void Sr25(GunMesh g)
        {
            ArReceivers(g, GM.Tan, GM.Tan, 0.24f);
            M4Stock(g, 0.078f, -0.075f, GM.Tan);
            float by = 0.078f;
            Handguard(g, by, 0.165f, 0.47f, 0.024f, GM.Tan);
            g.TubeZ(GM.Metal, 0, by, 0.47f, 0.62f, 0.01f, 0.009f, 16);
            MuzzleBrake(g, by, 0.62f, 0.0095f);
            Scope(g, 0.1055f, -0.06f, 0.24f);
            Bipod(g, by - 0.024f, 0.45f);
            RifleAnchors(g, by, 0.69f, by - 0.03f, 0.3f, 0.0f, 0.09f);
        }

        static void BoltRifle(GunMesh g, GM stock, bool bigBore)
        {
            float by = 0.062f;
            RifleStock(g, by, 0.38f, stock, false, true);
            g.Profile(stock, 0, 0.048f, 0.012f, 0.02f, by - 0.062f, 0.41f, by - 0.05f, 0.44f, by - 0.035f, 0.44f, by - 0.008f, 0.02f, by - 0.008f);   // fore-end
            g.TubeZ(GM.Metal, 0, by, -0.085f, 0.13f, 0.017f, -1, 20);     // action
            g.TubeZ(GM.Metal, 0, by, -0.115f, -0.085f, 0.012f, 0.014f, 16); // bolt shroud
            g.Tube(GM.Metal, new P3(0.012f, by + 0.004f, -0.05f), new P3(0.048f, by - 0.02f, -0.058f), 0.005f, 0.005f, 10);   // bolt handle
            g.Ball(GM.Metal, 0.05f, by - 0.022f, -0.058f, 0.011f, 0.011f, 0.011f, 12);
            g.Box(GM.Port, 0.016f, by + 0.004f, 0.03f, 0.004f, 0.016f, 0.07f, 0.0006f);
            TriggerGuard(g, 0.012f, 0.016f, 0.074f, GM.Metal);
            float end = bigBore ? 0.64f : 0.74f;
            g.TubeZ(GM.Metal, 0, by, 0.13f, end, bigBore ? 0.015f : 0.0125f, bigBore ? 0.013f : 0.0105f, 18);
            if (bigBore)
            {
                MuzzleBrake(g, by, end, 0.012f, 0.07f);
                g.TubeZ(GM.Metal, 0, by - 0.012f, 0.35f, 0.37f, 0.016f, -1, 14);   // barrel band
                Scope(g, by + 0.017f, -0.07f, 0.2f, 0.0152f, 0.024f);
                end += 0.07f;
            }
            else
            {
                g.Bore(by, end, 0.004f);
                Scope(g, by + 0.017f, -0.07f, 0.24f, 0.0152f, 0.028f);
                Bipod(g, by - 0.062f, 0.4f);
            }
            RifleAnchors(g, by, end + 0.01f, by - 0.07f, 0.3f, 0.002f, 0.12f);
        }

        static void Awm(GunMesh g)
        {
            float by = 0.062f;
            RifleStock(g, by, 0.39f, GM.Green, true);
            g.Profile(GM.Green, 0, 0.052f, 0.01f, 0.02f, by - 0.066f, 0.37f, by - 0.05f, 0.38f, by - 0.03f, 0.37f, by - 0.008f, 0.02f, by - 0.008f);
            g.TubeZ(GM.Metal, 0, by, -0.09f, 0.14f, 0.018f, -1, 20);
            g.TubeZ(GM.Metal, 0, by, -0.12f, -0.09f, 0.012f, 0.014f, 16);
            g.Tube(GM.Metal, new P3(0.012f, by + 0.004f, -0.055f), new P3(0.05f, by - 0.02f, -0.062f), 0.005f, 0.005f, 10);
            g.Ball(GM.Rubber, 0.052f, by - 0.022f, -0.062f, 0.012f, 0.012f, 0.012f, 12);
            g.Box(GM.Port, 0.017f, by + 0.004f, 0.03f, 0.004f, 0.016f, 0.07f, 0.0006f);
            Rail(g, by + 0.017f, -0.09f, 0.14f);
            TriggerGuard(g, 0.0f, 0.016f, 0.074f, GM.Dark);
            g.TubeZ(GM.Metal, 0, by, 0.14f, 0.74f, 0.0125f, 0.011f, 18);
            for (int i = 0; i < 6; i++)   // fluting
            {
                float a = i * 60f * (float)System.Math.PI / 180f;
                g.Box(GM.Port, (float)System.Math.Cos(a) * 0.0118f, by + (float)System.Math.Sin(a) * 0.0118f, 0.47f, 0.003f, 0.003f, 0.4f, 0.0004f);
            }
            MuzzleBrake(g, by, 0.74f, 0.011f, 0.065f);
            Scope(g, by + 0.025f, -0.08f, 0.26f, 0.017f, 0.03f);
            Bipod(g, by - 0.066f, 0.34f);
            RifleAnchors(g, by, 0.815f, by - 0.075f, 0.28f, -0.004f, 0.12f);
        }

        static void Vss(GunMesh g)
        {
            float by = 0.064f;
            PistolGrip(g, GM.Wood, 0.03f, 0.096f, 0.034f, 0.031f, 0.042f, false);
            g.Profile(GM.Metal, 0, 0.034f, 0.004f, -0.06f, 0.03f, 0.14f, 0.03f, 0.14f, 0.088f, -0.05f, 0.09f, -0.06f, 0.082f);
            g.Box(GM.Port, 0.0172f, 0.07f, 0.04f, 0.002f, 0.018f, 0.05f, 0.0004f);
            g.Box(GM.Metal, 0.02f, 0.072f, 0.0f, 0.008f, 0.008f, 0.02f, 0.0012f);
            // skeleton wooden stock: top bar, sloped bottom bar, butt
            g.Profile(GM.Wood, 0, 0.026f, 0.005f, -0.055f, 0.082f, -0.3f, 0.07f, -0.3f, 0.055f, -0.055f, 0.066f);
            g.Profile(GM.Wood, 0, 0.026f, 0.005f, -0.04f, 0.03f, -0.3f, -0.02f, -0.3f, -0.04f, -0.05f, 0.012f);
            g.Profile(GM.Wood, 0, 0.034f, 0.006f, -0.32f, -0.05f, -0.29f, -0.05f, -0.29f, 0.076f, -0.32f, 0.072f);
            g.Profile(GM.Rubber, 0, 0.036f, 0.004f, -0.332f, -0.052f, -0.318f, -0.052f, -0.318f, 0.074f, -0.332f, 0.07f);
            g.Profile(GM.Wood, 0, 0.044f, 0.008f, 0.14f, 0.03f, 0.27f, 0.034f, 0.27f, 0.05f, 0.14f, 0.05f);   // forend
            Suppressor(g, by, 0.14f, 0.36f, 0.021f);
            Rail(g, 0.088f, -0.03f, 0.12f);
            Scope(g, 0.0955f, -0.04f, 0.17f, 0.0145f, 0.022f);
            TriggerGuard(g, 0.03f, 0.004f, 0.064f, GM.Metal);
            RifleAnchors(g, by, 0.51f, 0.025f, 0.2f, 0.03f, 0.085f);
        }

        static void Aug(GunMesh g)
        {
            var body = GM.Green;
            PistolGrip(g, body, 0.035f, 0.1f, 0.012f, 0.032f, 0.042f);
            g.Profile(body, 0, 0.05f, 0.009f,
                -0.34f, -0.035f, -0.34f, 0.09f, -0.29f, 0.098f, 0.04f, 0.098f, 0.08f, 0.084f, 0.08f, 0.045f, 0.03f, 0.035f,
                -0.025f, 0.035f, -0.045f, 0.008f, -0.3f, 0.004f, -0.32f, -0.03f);
            g.Profile(GM.Rubber, 0, 0.052f, 0.005f, -0.355f, -0.038f, -0.338f, -0.038f, -0.338f, 0.09f, -0.355f, 0.086f);
            g.Profile(body, 0, 0.045f, 0.008f, -0.19f, 0.098f, -0.05f, 0.098f, -0.05f, 0.108f, -0.19f, 0.112f);   // cheek rest
            // the big trigger guard that wraps the whole hand, ending in the foregrip
            g.Profile(body, 0, 0.026f, 0.005f, -0.035f, -0.072f, 0.135f, -0.072f, 0.135f, -0.056f, -0.03f, -0.056f);
            g.Profile(body, 0, 0.026f, 0.006f, 0.11f, 0.04f, 0.14f, 0.04f, 0.138f, -0.07f, 0.112f, -0.07f);
            g.Profile(GM.Dark, 0, 0.006f, 0.001f, 0.022f, 0.035f, 0.028f, 0.035f, 0.032f, 0.01f, 0.026f, 0.004f);   // trigger
            // integrated scope on its housing
            g.Profile(body, 0, 0.034f, 0.006f, -0.08f, 0.098f, 0.07f, 0.098f, 0.055f, 0.13f, -0.065f, 0.13f);
            g.TubeZ(body, 0, 0.143f, -0.1f, 0.09f, 0.019f, -1, 18);
            g.TubeZ(body, 0, 0.143f, 0.06f, 0.095f, 0.022f, 0.024f, 18);
            g.TubeZ(GM.Glass, 0, 0.143f, 0.095f, 0.096f, 0.02f, -1, 18);
            g.TubeZ(GM.Rubber, 0, 0.143f, -0.11f, -0.1f, 0.02f, -1, 18);
            // barrel with gas cylinder above
            g.TubeZ(GM.Dark, 0, 0.066f, 0.08f, 0.39f, 0.0115f, -1, 16);
            g.TubeZ(GM.Dark, 0, 0.066f, 0.08f, 0.13f, 0.016f, -1, 16);
            g.TubeZ(GM.Dark, 0, 0.083f, 0.08f, 0.2f, 0.007f, -1, 10);
            FlashHider(g, 0.066f, 0.39f, 0.0105f);
            g.Box(GM.Dark, -0.026f, 0.07f, -0.02f, 0.005f, 0.012f, 0.03f, 0.001f);   // cocking handle
            g.Box(GM.Port, 0.0255f, 0.06f, -0.12f, 0.002f, 0.02f, 0.05f, 0.0004f);
            g.Anchor("muzzle", 0.066f, 0.445f);
            g.Anchor("magwell", 0.004f, -0.1f);
            g.Anchor("foregrip", -0.02f, 0.125f);
            g.Anchor("eject", 0.06f, -0.12f, 0, 0.026f);
            g.Anchor("ammo", 0.07f, -0.22f, 0, 0.027f);
        }

        static void Ak(GunMesh g)
        {
            PistolGrip(g, GM.DarkWood, 0.03f, 0.095f, 0.03f, 0.03f, 0.04f, false);
            g.Profile(GM.Metal, 0, 0.034f, 0.003f, -0.062f, 0.03f, 0.2f, 0.03f, 0.2f, 0.082f, 0.19f, 0.088f, -0.05f, 0.09f, -0.062f, 0.082f);
            for (int i = 0; i < 5; i++) g.Box(GM.Dark, 0, 0.0905f, -0.03f + i * 0.04f, 0.022f, 0.0025f, 0.006f, 0.0008f);
            g.Box(GM.Metal, 0.0185f, 0.064f, 0.075f, 0.003f, 0.01f, 0.11f, 0.0006f);    // safety lever
            g.Box(GM.Metal, 0.024f, 0.074f, 0.13f, 0.012f, 0.008f, 0.014f, 0.0015f);    // charging handle
            g.Box(GM.Port, 0.0172f, 0.08f, 0.1f, 0.002f, 0.012f, 0.07f, 0.0004f);
            // the wooden stock, dropping toward the shoulder
            g.Profile(GM.Wood, 0, 0.034f, 0.006f, -0.055f, 0.085f, -0.375f, 0.062f, -0.385f, 0.052f, -0.385f, -0.042f, -0.37f, -0.048f, -0.055f, 0.032f);
            g.Profile(GM.Metal, 0, 0.036f, 0.003f, -0.398f, -0.046f, -0.385f, -0.046f, -0.385f, 0.06f, -0.398f, 0.056f);
            // lower handguard, upper handguard over the gas tube, gas block, front sight and slant brake
            g.Profile(GM.Wood, 0, 0.04f, 0.008f, 0.2f, 0.034f, 0.335f, 0.038f, 0.345f, 0.05f, 0.335f, 0.072f, 0.2f, 0.072f);
            g.Profile(GM.Wood, 0, 0.028f, 0.006f, 0.24f, 0.078f, 0.33f, 0.08f, 0.33f, 0.096f, 0.24f, 0.098f);
            g.TubeZ(GM.Metal, 0, 0.088f, 0.33f, 0.39f, 0.0085f, -1, 12);
            g.Profile(GM.Metal, 0, 0.026f, 0.003f, 0.385f, 0.052f, 0.41f, 0.052f, 0.41f, 0.098f, 0.385f, 0.098f);
            g.Profile(GM.Metal, 0, 0.02f, 0.002f, 0.2f, 0.088f, 0.245f, 0.088f, 0.245f, 0.1f, 0.2f, 0.097f);   // rear sight block
            g.TubeZ(GM.Metal, 0, 0.062f, 0.2f, 0.52f, 0.0085f, -1, 14);
            g.Profile(GM.Metal, 0, 0.02f, 0.002f, 0.49f, 0.054f, 0.51f, 0.054f, 0.51f, 0.1f, 0.49f, 0.1f);
            g.TubeZ(GM.Dark, 0, 0.062f, 0.52f, 0.56f, 0.012f, -1, 14);
            g.Box(GM.Port, 0, 0.074f, 0.545f, 0.012f, 0.004f, 0.022f, 0.0004f);
            g.Bore(0.062f, 0.56f, 0.005f);
            TriggerGuard(g, 0.03f, 0.004f, 0.062f, GM.Metal);
            RifleAnchors(g, 0.062f, 0.57f, 0.03f, 0.27f, 0.03f, 0.095f);
        }

        static void Scar(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.028f);
            g.Profile(GM.Polymer, 0, 0.028f, 0.003f, -0.062f, 0.066f, 0.14f, 0.066f, 0.14f, 0.05f, 0.13f, 0.004f, 0.06f, -0.002f, 0.055f, 0.028f, -0.03f, 0.028f, -0.062f, 0.045f);
            TriggerGuard(g, 0.028f, 0.0f, 0.054f, GM.Polymer, true);
            g.Profile(GM.Tan, 0, 0.038f, 0.004f, -0.08f, 0.066f, 0.335f, 0.066f, 0.335f, 0.108f, -0.08f, 0.108f);
            Rail(g, 0.108f, -0.078f, 0.333f);
            foreach (float s in new[] { -1f, 1f })
            {
                g.Box(GM.Dark, s * 0.021f, 0.086f, 0.265f, 0.006f, 0.014f, 0.13f, 0.001f);
                for (int i = 0; i < 10; i++) g.Box(GM.Dark, s * 0.025f, 0.086f, 0.205f + i * 0.0125f, 0.003f, 0.018f, 0.005f, 0.0006f);
            }
            g.Box(GM.Dark, -0.024f, 0.098f, 0.05f, 0.012f, 0.012f, 0.02f, 0.002f);   // side charging handle
            g.Box(GM.Port, 0.0192f, 0.084f, 0.04f, 0.002f, 0.02f, 0.06f, 0.0004f);
            g.Profile(GM.Tan, 0, 0.034f, 0.005f, -0.08f, 0.106f, -0.33f, 0.108f, -0.34f, 0.098f, -0.34f, 0.066f, -0.1f, 0.078f, -0.08f, 0.078f);
            g.Profile(GM.Tan, 0, 0.03f, 0.005f, -0.08f, 0.072f, -0.08f, 0.052f, -0.3f, -0.016f, -0.34f, -0.012f, -0.34f, 0.01f);
            g.Profile(GM.Tan, 0, 0.034f, 0.005f, -0.345f, -0.014f, -0.315f, -0.014f, -0.315f, 0.104f, -0.345f, 0.1f);
            g.Box(GM.Dark, 0, 0.09f, -0.09f, 0.03f, 0.03f, 0.02f, 0.003f);   // folding hinge
            g.Profile(GM.Rubber, 0, 0.036f, 0.004f, -0.354f, -0.014f, -0.338f, -0.014f, -0.338f, 0.104f, -0.354f, 0.1f);
            g.Profile(GM.Tan, 0, 0.028f, 0.004f, -0.12f, 0.108f, -0.28f, 0.11f, -0.28f, 0.122f, -0.12f, 0.12f);   // cheek riser
            g.TubeZ(GM.Metal, 0, 0.087f, 0.335f, 0.45f, 0.009f, -1, 14);
            FlashHider(g, 0.087f, 0.45f, 0.0105f);
            g.Profile(GM.Dark, 0, 0.016f, 0.002f, 0.31f, 0.116f, 0.33f, 0.116f, 0.326f, 0.138f, 0.314f, 0.138f);   // folding front sight
            RedDot(g, 0.1155f, 0.05f);
            g.Box(GM.Accent, -0.015f, 0.04f, -0.01f, 0.003f, 0.008f, 0.01f, 0.0005f);
            RifleAnchors(g, 0.087f, 0.505f, 0.05f, 0.26f, 0.0f, 0.09f);
        }

        static void M249(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.035f);
            g.Profile(GM.Dark, 0, 0.05f, 0.004f, -0.07f, 0.035f, 0.21f, 0.035f, 0.21f, 0.1f, -0.06f, 0.1f, -0.07f, 0.09f);
            g.Profile(GM.Metal, 0, 0.052f, 0.004f, -0.04f, 0.1f, 0.18f, 0.1f, 0.18f, 0.116f, -0.03f, 0.12f);   // feed tray cover
            Rail(g, 0.116f, -0.02f, 0.16f);
            g.Box(GM.Port, 0.0252f, 0.065f, 0.06f, 0.002f, 0.03f, 0.08f, 0.0004f);
            g.Profile(GM.Polymer, 0, 0.03f, 0.004f, -0.07f, 0.095f, -0.32f, 0.095f, -0.33f, 0.085f, -0.33f, -0.02f, -0.31f, -0.025f, -0.12f, 0.05f, -0.07f, 0.05f);
            g.Profile(GM.Rubber, 0, 0.034f, 0.004f, -0.345f, -0.024f, -0.328f, -0.024f, -0.328f, 0.092f, -0.345f, 0.088f);
            g.Profile(GM.Polymer, 0, 0.052f, 0.008f, 0.21f, 0.03f, 0.42f, 0.04f, 0.42f, 0.062f, 0.21f, 0.062f);   // forend
            g.TubeZ(GM.Metal, 0, 0.078f, 0.21f, 0.62f, 0.013f, 0.0115f, 18);
            g.Box(GM.Dark, 0, 0.098f, 0.33f, 0.03f, 0.008f, 0.2f, 0.002f);      // heat shield
            for (int i = 0; i < 6; i++) g.Box(GM.Port, 0, 0.1025f, 0.25f + i * 0.032f, 0.022f, 0.002f, 0.014f, 0.0003f);
            g.Profile(GM.Metal, 0, 0.012f, 0.002f, 0.22f, 0.1f, 0.24f, 0.1f, 0.24f, 0.13f, 0.34f, 0.13f, 0.34f, 0.1f, 0.36f, 0.1f, 0.36f, 0.142f, 0.22f, 0.142f);   // carry handle
            FlashHider(g, 0.078f, 0.62f, 0.012f);
            Bipod(g, 0.06f, 0.56f);
            TriggerGuard(g, 0.035f, 0.004f, 0.066f, GM.Dark, true);
            g.Box(GM.Accent, -0.026f, 0.07f, 0.0f, 0.004f, 0.012f, 0.02f, 0.0008f);
            RifleAnchors(g, 0.078f, 0.68f, 0.025f, 0.32f, 0.035f, 0.1f);
        }

        static void Vector(GunMesh g)
        {
            PistolGrip(g, GM.Polymer, 0.03f);
            g.Profile(GM.Polymer, 0, 0.042f, 0.005f,
                -0.075f, 0.045f, -0.075f, 0.108f, 0.2f, 0.108f, 0.215f, 0.088f, 0.175f, 0.045f, 0.13f, 0.04f,
                0.135f, 0.0f, 0.084f, -0.006f, 0.07f, 0.03f, 0.02f, 0.03f, -0.045f, 0.035f);
            foreach (float s in new[] { -1f, 1f })
            {
                g.Box(GM.Accent, s * 0.0212f, 0.078f, 0.06f, 0.0015f, 0.006f, 0.2f, 0.0004f);
                g.Box(GM.Accent, s * 0.0212f, 0.05f, 0.14f, 0.0015f, 0.006f, 0.05f, 0.0004f);
            }
            TriggerGuard(g, 0.03f, 0.004f, 0.062f, GM.Polymer, true);
            Rail(g, 0.108f, -0.07f, 0.19f);
            g.TubeZ(GM.Dark, 0, 0.088f, 0.2f, 0.27f, 0.015f, -1, 16);
            Suppressor(g, 0.088f, 0.27f, 0.12f, 0.018f);
            M4Stock(g, 0.088f, -0.075f, GM.Polymer);
            RedDot(g, 0.1155f, 0.03f);
            g.Box(GM.Port, 0.0212f, 0.09f, 0.1f, 0.002f, 0.016f, 0.04f, 0.0004f);
            RifleAnchors(g, 0.088f, 0.4f, 0.0f, 0.15f, -0.004f, 0.11f, -10f);
        }

        /* ======================= magazines ======================= */

        /// <summary>A straight pistol / SMG mag with a base plate.</summary>
        static void PistolMag(GunMesh g, float len, float depth, GM m)
        {
            g.Profile(m, 0, 0.023f, 0.002f, -depth * 0.5f, 0.0f, depth * 0.5f, 0.0f, depth * 0.5f, -len, -depth * 0.5f, -len);
            g.Profile(GM.Polymer, 0, 0.027f, 0.003f, -depth * 0.5f - 0.002f, -len + 0.002f, depth * 0.5f + 0.004f, -len + 0.002f, depth * 0.5f + 0.004f, -len - 0.01f, -depth * 0.5f - 0.002f, -len - 0.01f);
            g.TubeZ(GM.Brass, 0, 0.004f, -depth * 0.3f, depth * 0.35f, 0.0042f, 0.003f, 10);
            for (int i = 0; i < 3; i++) g.Tube(GM.Port, new P3(0.0112f, -len * (0.3f + i * 0.2f), 0), new P3(0.0118f, -len * (0.3f + i * 0.2f), 0), 0.0025f, 0.0025f, 8);
        }

        /// <summary>A short box mag for a bolt-action or marksman rifle.</summary>
        static void BoxMag(GunMesh g, float len, float depth, GM m, bool slightCurve)
        {
            float c = slightCurve ? 0.01f : 0f;
            g.Profile(m, 0, 0.026f, 0.002f, -depth * 0.5f, 0.008f, depth * 0.5f, 0.008f, depth * 0.5f + c, -len, -depth * 0.5f + c, -len);
            g.Profile(GM.Polymer, 0, 0.03f, 0.003f, -depth * 0.5f + c - 0.002f, -len + 0.002f, depth * 0.5f + c + 0.002f, -len + 0.002f, depth * 0.5f + c + 0.002f, -len - 0.009f, -depth * 0.5f + c - 0.002f, -len - 0.009f);
            g.TubeZ(GM.Brass, 0, 0.012f, -depth * 0.4f, depth * 0.4f, 0.005f, 0.0035f, 10);
        }

        static void AkMag(GunMesh g)
        {
            g.Profile(GM.DarkWood, 0, 0.026f, 0.002f,
                -0.03f, 0.012f, 0.03f, 0.012f, 0.036f, -0.05f, 0.058f, -0.11f, 0.092f, -0.17f,
                0.03f, -0.2f, 0.004f, -0.14f, -0.018f, -0.07f);
            for (int i = 0; i < 4; i++)
                g.Box(GM.DarkWood, 0.0128f, -0.04f - i * 0.035f, 0.005f + i * 0.013f, 0.001f, 0.006f, 0.035f, 0.0002f);
            g.TubeZ(GM.Brass, 0, 0.014f, -0.02f, 0.02f, 0.0045f, 0.003f, 10);
        }

        static void SpeedLoader(GunMesh g)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * (float)System.Math.PI / 180f;
                float x = (float)System.Math.Cos(a) * 0.0135f, z = (float)System.Math.Sin(a) * 0.0135f;
                g.Tube(GM.Brass, new P3(x, -0.03f, z), new P3(x, 0.0f, z), 0.0048f, 0.0042f, 10);
                g.Ball(GM.Bronze, x, 0.001f, z, 0.0042f, 0.004f, 0.0042f, 8);
            }
            g.Tube(GM.Dark, new P3(0, -0.04f, 0), new P3(0, -0.028f, 0), 0.022f, 0.022f, 18);
            g.Tube(GM.Rubber, new P3(0, -0.06f, 0), new P3(0, -0.04f, 0), 0.01f, 0.012f, 14);
        }

        static void Drum(GunMesh g)
        {
            g.Profile(GM.Dark, 0, 0.03f, 0.003f, -0.02f, 0.006f, 0.02f, 0.006f, 0.02f, -0.03f, -0.02f, -0.03f);
            g.Tube(GM.Polymer, new P3(-0.035f, -0.09f, 0), new P3(0.035f, -0.09f, 0), 0.07f, 0.07f, 28);
            g.Tube(GM.Dark, new P3(0.035f, -0.09f, 0), new P3(0.04f, -0.09f, 0), 0.03f, 0.026f, 18);
            g.Tube(GM.Dark, new P3(-0.04f, -0.09f, 0), new P3(-0.035f, -0.09f, 0), 0.06f, 0.06f, 28);
        }

        static void AmmoPouch(GunMesh g)
        {
            g.Profile(GM.Olive, 0, 0.06f, 0.014f, -0.065f, -0.01f, 0.06f, -0.01f, 0.065f, -0.13f, -0.06f, -0.135f);
            g.Profile(GM.Olive, 0, 0.064f, 0.006f, -0.06f, -0.004f, 0.055f, -0.004f, 0.055f, -0.03f, -0.06f, -0.03f);
            g.Box(GM.Dark, 0, 0.0f, 0.0f, 0.03f, 0.02f, 0.03f, 0.002f);
            for (int i = 0; i < 5; i++) g.Box(GM.Brass, 0, 0.012f, -0.02f + i * 0.01f, 0.03f, 0.006f, 0.008f, 0.002f);   // the belt
        }

        static void ShellHolder(GunMesh g, int n)
        {
            float span = n * 0.024f;
            g.Profile(GM.Polymer, 0, 0.026f, 0.003f, -span * 0.5f - 0.004f, -0.012f, span * 0.5f + 0.004f, -0.012f, span * 0.5f + 0.004f, -0.05f, -span * 0.5f - 0.004f, -0.05f);
            for (int i = 0; i < n; i++)
            {
                float z = (i - (n - 1) * 0.5f) * 0.024f;
                g.Tube(GM.Red, new P3(0, -0.07f, z), new P3(0, -0.012f, z), 0.0105f, 0.0105f, 14);
                g.Tube(GM.Brass, new P3(0, -0.012f, z), new P3(0, 0.002f, z), 0.011f, 0.011f, 14);
            }
        }
    }
}
