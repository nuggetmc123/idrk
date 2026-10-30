namespace Flopzone
{
    /// <summary>The guns added with the skill tree: M1911, MP5, UMP45, P90, KSG, Barrett M82, Mosin-Nagant, G36C.</summary>
    public static partial class GunShapes
    {
        static bool PickNew(GunMesh g, string id)
        {
            switch (id)
            {
                case "classic": M1911(g); return true;
                case "hornet": Mp5(g); return true;
                case "slab": Ump(g); return true;
                case "bullhorn": P90(g); return true;
                case "twintube": Ksg(g); return true;
                case "fiftycal": Barrett(g); return true;
                case "mosin": Mosin(g); return true;
                case "gecko": G36(g); return true;
            }
            return false;
        }

        static bool PickNewMag(GunMesh g, string id)
        {
            switch (id)
            {
                case "classic": PistolMag(g, 0.104f, 0.03f, GM.Metal); return true;
                case "hornet": Mp5Mag(g); return true;
                case "slab": PistolMag(g, 0.16f, 0.036f, GM.Polymer); return true;
                case "bullhorn": P90Mag(g); return true;
                case "twintube": ShellHolder(g, 4); return true;
                case "fiftycal": BoxMag(g, 0.09f, 0.11f, GM.Dark, false); return true;
                case "mosin": BoxMag(g, 0.04f, 0.075f, GM.Metal, false); return true;
                case "gecko": Stanag(g, GM.Polymer); return true;
            }
            return false;
        }

        /* ---------------- M1911 ---------------- */

        static void M1911(GunMesh g)
        {
            PistolGrip(g, GM.Wood, 0.028f, 0.098f, 0.032f, 0.03f, 0.04f);
            g.Profile(GM.Metal, 0, 0.026f, 0.003f, -0.048f, 0.03f, 0.15f, 0.03f, 0.15f, 0.018f, 0.075f, 0.014f, 0.066f, 0.02f, 0.014f, 0.022f, -0.036f, 0.022f, -0.05f, 0.028f);
            g.Profile(GM.Metal, 0, 0.022f, 0.003f, -0.034f, 0.03f, -0.058f, 0.024f, -0.062f, 0.032f, -0.04f, 0.04f);   // beavertail grip safety
            TriggerGuard(g, 0.022f, 0.012f, 0.068f, GM.Metal);
            g.Profile(GM.Metal, 0, 0.0245f, 0.003f, -0.052f, 0.03f, 0.165f, 0.03f, 0.165f, 0.058f, 0.16f, 0.062f, -0.046f, 0.062f, -0.052f, 0.056f);
            Serrations(g, 0.0245f, 0.034f, 0.058f, -0.046f, 8, 0.004f);
            Serrations(g, 0.0245f, 0.036f, 0.056f, 0.12f, 6, 0.004f);
            g.Box(GM.Port, 0.0113f, 0.055f, 0.045f, 0.002f, 0.012f, 0.04f, 0.0004f);
            g.Profile(GM.Metal, 0, 0.007f, 0.0015f, -0.05f, 0.058f, -0.064f, 0.074f, -0.058f, 0.077f, -0.044f, 0.064f);   // hammer
            g.Box(GM.Metal, -0.0135f, 0.034f, 0.04f, 0.003f, 0.008f, 0.03f, 0.0006f);   // slide stop
            g.TubeZ(GM.Metal, 0, 0.045f, 0.163f, 0.168f, 0.0075f, -1, 14);   // barrel bushing
            IronSights(g, 0.062f, 0.155f, -0.04f);
            g.Bore(0.045f, 0.168f, 0.0055f);
            PistolAnchors(g, 0.045f, 0.168f, 18f);
            g.Anchor("under", 0.012f, 0.11f);
        }

        /* ---------------- MP5 ---------------- */

        static void Mp5(GunMesh g)
        {
            float by = 0.07f;
            PistolGrip(g, GM.Polymer, 0.034f);
            g.Profile(GM.Polymer, 0, 0.03f, 0.004f, -0.06f, 0.05f, 0.065f, 0.05f, 0.07f, 0.034f, 0.04f, 0.03f, -0.04f, 0.03f, -0.06f, 0.04f);   // trigger group
            TriggerGuard(g, 0.034f, 0.005f, 0.064f, GM.Polymer);
            g.Profile(GM.Dark, 0, 0.032f, 0.006f, -0.08f, 0.05f, 0.2f, 0.05f, 0.2f, 0.09f, 0.19f, 0.098f, -0.07f, 0.098f, -0.08f, 0.09f);   // receiver tube
            g.Box(GM.Dark, 0, 0.068f, 0.11f, 0.028f, 0.03f, 0.05f, 0.003f);   // magwell
            g.TubeZ(GM.Dark, 0, 0.09f, 0.2f, 0.3f, 0.009f, -1, 14);           // cocking tube
            g.Box(GM.Dark, -0.012f, 0.092f, 0.24f, 0.012f, 0.008f, 0.012f, 0.0015f);   // cocking handle
            g.Profile(GM.Polymer, 0, 0.042f, 0.009f, 0.2f, 0.04f, 0.33f, 0.044f, 0.335f, 0.06f, 0.33f, 0.08f, 0.2f, 0.08f);   // slim handguard
            g.TubeZ(GM.Dark, 0, by, 0.33f, 0.38f, 0.0085f, -1, 14);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * (float)System.Math.PI / 180f;
                g.Box(GM.Dark, (float)System.Math.Cos(a) * 0.009f, by + (float)System.Math.Sin(a) * 0.009f, 0.37f, 0.004f, 0.004f, 0.012f, 0.0006f);   // 3-lug
            }
            // hooded front sight and drum rear sight
            g.Profile(GM.Dark, 0, 0.022f, 0.002f, 0.3f, 0.09f, 0.325f, 0.09f, 0.322f, 0.122f, 0.303f, 0.122f);
            g.Tube(GM.Dark, new P3(-0.012f, 0.112f, -0.05f), new P3(0.012f, 0.112f, -0.05f), 0.011f, 0.011f, 14);
            g.Box(GM.Dark, 0, 0.1f, -0.05f, 0.02f, 0.012f, 0.02f, 0.002f);
            // retractable stock: two rails and a butt plate
            foreach (float s in new[] { -1f, 1f }) g.TubeZ(GM.Metal, s * 0.012f, 0.072f, -0.27f, -0.08f, 0.004f, -1, 8);
            g.Profile(GM.Dark, 0, 0.034f, 0.004f, -0.29f, 0.0f, -0.27f, 0.0f, -0.27f, 0.095f, -0.29f, 0.095f);
            g.Profile(GM.Rubber, 0, 0.036f, 0.003f, -0.298f, 0.0f, -0.289f, 0.0f, -0.289f, 0.095f, -0.298f, 0.095f);
            g.Box(GM.Port, 0.0162f, 0.075f, 0.06f, 0.002f, 0.016f, 0.04f, 0.0004f);
            g.Bore(by, 0.38f, 0.0045f);
            RifleAnchors(g, by, 0.39f, 0.03f, 0.26f, 0.052f, 0.11f, 0f);
            g.Anchor("optic", 0.1f, 0.04f);
            g.Anchor("under", 0.04f, 0.27f);
        }

        /* ---------------- UMP45 ---------------- */

        static void Ump(GunMesh g)
        {
            float by = 0.075f;
            PistolGrip(g, GM.Polymer, 0.032f);
            g.Profile(GM.Polymer, 0, 0.044f, 0.006f,
                -0.08f, 0.03f, 0.08f, 0.03f, 0.08f, 0.0f, 0.15f, 0.0f, 0.15f, 0.04f, 0.29f, 0.04f, 0.3f, 0.06f, 0.29f, 0.108f, -0.07f, 0.108f, -0.08f, 0.098f);
            Rail(g, 0.108f, -0.06f, 0.24f);
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 5; i++) g.Box(GM.Port, s * 0.0222f, 0.075f, 0.16f + i * 0.026f, 0.0015f, 0.02f, 0.014f, 0.0003f);
            g.Box(GM.Dark, -0.024f, 0.09f, 0.2f, 0.01f, 0.01f, 0.016f, 0.0015f);   // cocking handle
            TriggerGuard(g, 0.032f, 0.004f, 0.068f, GM.Polymer, true);
            g.TubeZ(GM.Dark, 0, by, 0.3f, 0.345f, 0.01f, -1, 14);
            g.Bore(by, 0.345f, 0.0055f);
            // side-folding skeleton stock
            g.Profile(GM.Polymer, 0, 0.03f, 0.005f, -0.08f, 0.1f, -0.32f, 0.1f, -0.33f, 0.09f, -0.33f, 0.072f, -0.1f, 0.08f, -0.08f, 0.08f);
            g.Profile(GM.Polymer, 0, 0.028f, 0.005f, -0.08f, 0.05f, -0.32f, -0.01f, -0.33f, -0.005f, -0.33f, 0.01f, -0.09f, 0.068f);
            g.Profile(GM.Rubber, 0, 0.034f, 0.004f, -0.345f, -0.01f, -0.328f, -0.01f, -0.328f, 0.1f, -0.345f, 0.096f);
            RifleAnchors(g, by, 0.355f, 0.0f, 0.2f, 0.0f, 0.115f);
            g.Anchor("optic", 0.1155f, 0.06f);
            g.Anchor("under", 0.04f, 0.22f);
        }

        /* ---------------- P90 ---------------- */

        static void P90(GunMesh g)
        {
            var body = GM.Polymer;
            // the rounded bullpup shell with its thumbhole grip and a front loop for the other hand
            g.Profile(body, 0, 0.056f, 0.012f,
                -0.3f, -0.035f, -0.3f, 0.07f, -0.27f, 0.09f, 0.16f, 0.09f, 0.2f, 0.075f, 0.21f, 0.04f, 0.19f, 0.02f,
                0.18f, -0.06f, 0.15f, -0.07f, 0.12f, -0.03f, 0.08f, 0.0f, -0.03f, 0.0f, -0.05f, -0.06f, -0.09f, -0.075f,
                -0.13f, -0.04f, -0.24f, -0.03f);
            g.Profile(GM.Port, 0, 0.0565f, 0.002f, 0.13f, -0.02f, 0.15f, -0.05f, 0.165f, -0.045f, 0.16f, 0.005f);
            g.Profile(GM.Port, 0, 0.0565f, 0.002f, -0.015f, -0.005f, -0.03f, -0.045f, -0.06f, -0.05f, -0.055f, -0.005f);
            g.Profile(GM.Rubber, 0, 0.058f, 0.005f, -0.315f, -0.04f, -0.298f, -0.04f, -0.298f, 0.072f, -0.315f, 0.068f);
            g.Box(GM.Dark, 0, 0.1f, 0.0f, 0.03f, 0.014f, 0.16f, 0.003f);   // the mag sits on top, on its rails
            RedDot(g, 0.107f, 0.05f);
            g.TubeZ(GM.Dark, 0, 0.055f, 0.2f, 0.25f, 0.009f, -1, 14);
            FlashHider(g, 0.055f, 0.25f, 0.009f);
            g.Box(GM.Port, 0, -0.0f, -0.1f, 0.03f, 0.002f, 0.06f, 0.0004f);   // spent cases drop out the bottom
            g.Anchor("muzzle", 0.055f, 0.305f);
            g.Anchor("magwell", 0.122f, 0.16f, 90f);   // the long mag lies along the top, pointing back
            g.Anchor("foregrip", -0.04f, 0.14f);
            g.Anchor("eject", 0.0f, -0.1f, 0, 0.0f);
            g.Anchor("ammo", 0.06f, -0.2f, 0, 0.03f);
        }

        /* ---------------- KSG ---------------- */

        static void Ksg(GunMesh g)
        {
            float by = 0.07f;
            PistolGrip(g, GM.Polymer, 0.035f);
            g.Profile(GM.Dark, 0, 0.05f, 0.008f,
                -0.33f, -0.02f, -0.33f, 0.095f, 0.1f, 0.095f, 0.12f, 0.08f, 0.12f, 0.02f, 0.06f, 0.02f, 0.05f, 0.035f, -0.02f, 0.035f, -0.05f, 0.0f, -0.3f, -0.02f);
            g.Profile(GM.Rubber, 0, 0.052f, 0.005f, -0.345f, -0.024f, -0.328f, -0.024f, -0.328f, 0.097f, -0.345f, 0.093f);
            Rail(g, 0.095f, -0.2f, 0.12f);
            TriggerGuard(g, 0.035f, 0.004f, 0.064f, GM.Dark, true);
            g.TubeZ(GM.Dark, 0, by + 0.012f, 0.12f, 0.5f, 0.0115f, -1, 16);
            foreach (float s in new[] { -1f, 1f }) g.TubeZ(GM.Dark, s * 0.013f, 0.04f, 0.12f, 0.44f, 0.011f, -1, 14);   // the twin tubes
            g.Bore(by + 0.012f, 0.5f, 0.009f);
            g.group = "pump";
            g.Profile(GM.Polymer, 0, 0.058f, 0.009f, 0.2f, 0.018f, 0.34f, 0.018f, 0.345f, 0.04f, 0.34f, 0.07f, 0.2f, 0.07f);
            g.Profile(GM.Polymer, 0, 0.024f, 0.005f, 0.28f, 0.018f, 0.3f, 0.018f, 0.296f, -0.05f, 0.282f, -0.05f);   // pump grip
            g.group = "";
            g.Anchor("pump", 0.04f, 0.27f);
            ShotgunAnchors(g, by + 0.012f, 0.51f, -0.02f, 0.29f, 0.0f, -0.12f);
            g.Anchor("optic", 0.1025f, 0.0f);
        }

        /* ---------------- Barrett M82 ---------------- */

        static void Barrett(GunMesh g)
        {
            float by = 0.075f;
            PistolGrip(g, GM.Polymer, 0.035f, 0.105f);
            // big two-piece steel receiver
            g.Profile(GM.Dark, 0, 0.05f, 0.005f, -0.08f, 0.035f, 0.3f, 0.035f, 0.32f, 0.05f, 0.32f, 0.1f, -0.08f, 0.1f);
            g.Profile(GM.Dark, 0, 0.044f, 0.005f, -0.08f, 0.035f, -0.08f, 0.1f, -0.33f, 0.1f, -0.35f, 0.08f, -0.35f, -0.03f, -0.3f, -0.04f, -0.18f, 0.0f, -0.12f, 0.035f);
            g.Profile(GM.Rubber, 0, 0.05f, 0.005f, -0.37f, -0.045f, -0.348f, -0.045f, -0.348f, 0.085f, -0.37f, 0.08f);
            for (int i = 0; i < 6; i++) g.Box(GM.Port, 0.0252f, 0.07f, -0.04f + i * 0.055f, 0.0012f, 0.04f, 0.025f, 0.0003f);
            Rail(g, 0.1f, -0.06f, 0.3f);
            g.Profile(GM.Dark, 0, 0.012f, 0.002f, 0.03f, 0.1f, 0.05f, 0.1f, 0.05f, 0.13f, 0.18f, 0.13f, 0.18f, 0.1f, 0.2f, 0.1f, 0.2f, 0.14f, 0.03f, 0.14f);   // carry handle
            TriggerGuard(g, 0.035f, 0.004f, 0.07f, GM.Dark, true);
            g.Tube(GM.Dark, new P3(0, -0.03f, -0.25f), new P3(0, -0.11f, -0.25f), 0.008f, 0.008f, 10);   // monopod
            // fluted heavy barrel and the big double-chamber brake
            g.TubeZ(GM.Metal, 0, by, 0.32f, 0.86f, 0.017f, 0.015f, 20);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * (float)System.Math.PI / 180f;
                g.Box(GM.Port, (float)System.Math.Cos(a) * 0.0158f, by + (float)System.Math.Sin(a) * 0.0158f, 0.6f, 0.0035f, 0.0035f, 0.4f, 0.0005f);
            }
            g.Profile(GM.Dark, 0, 0.07f, 0.006f, 0.86f, by - 0.02f, 0.95f, by - 0.02f, 0.95f, by + 0.02f, 0.86f, by + 0.02f);
            foreach (float z in new[] { 0.88f, 0.92f })
                foreach (float s in new[] { -1f, 1f }) g.Box(GM.Port, s * 0.0352f, by, z, 0.002f, 0.03f, 0.022f, 0.0004f);
            g.Bore(by, 0.95f, 0.007f);
            Bipod(g, by - 0.02f, 0.5f);
            Scope(g, 0.1075f, -0.06f, 0.26f, 0.017f, 0.032f, 4f, true);
            RifleAnchors(g, by, 0.96f, by - 0.04f, 0.4f, 0.035f, 0.12f);
        }

        /* ---------------- Mosin-Nagant ---------------- */

        static void Mosin(GunMesh g)
        {
            float by = 0.062f;
            RifleStock(g, by, 0.36f, GM.Wood, false, false);
            // the long one-piece fore-end with its barrel bands
            g.Profile(GM.Wood, 0, 0.042f, 0.01f, 0.02f, by - 0.058f, 0.62f, by - 0.03f, 0.64f, by - 0.02f, 0.64f, by + 0.004f, 0.02f, by - 0.008f);
            g.Profile(GM.Wood, 0, 0.032f, 0.006f, 0.16f, by + 0.004f, 0.4f, by + 0.006f, 0.4f, by + 0.016f, 0.16f, by + 0.016f);   // top handguard
            foreach (float z in new[] { 0.33f, 0.58f }) g.TubeZ(GM.Metal, 0, by - 0.01f, z, z + 0.012f, 0.024f, -1, 14);
            g.TubeZ(GM.Metal, 0, by, -0.09f, 0.13f, 0.016f, -1, 18);     // receiver
            g.Tube(GM.Metal, new P3(0.012f, by + 0.004f, -0.06f), new P3(0.04f, by - 0.03f, -0.065f), 0.0045f, 0.0045f, 10);   // the bent bolt handle
            g.Ball(GM.Metal, 0.041f, by - 0.032f, -0.065f, 0.009f, 0.009f, 0.009f, 10);
            g.Box(GM.Port, 0.0155f, by + 0.004f, 0.03f, 0.004f, 0.016f, 0.07f, 0.0006f);
            g.Profile(GM.Metal, 0, 0.024f, 0.002f, -0.01f, 0.015f, 0.1f, 0.015f, 0.1f, 0.0f, -0.01f, 0.0f);   // magazine box
            TriggerGuard(g, 0.0f, 0.016f, 0.07f, GM.Metal);
            g.TubeZ(GM.Metal, 0, by, 0.13f, 0.72f, 0.012f, 0.0095f, 18);
            g.Profile(GM.Metal, 0, 0.014f, 0.002f, 0.69f, by + 0.006f, 0.715f, by + 0.006f, 0.712f, by + 0.03f, 0.694f, by + 0.03f);   // hooded front sight
            g.Bore(by, 0.72f, 0.004f);
            Scope(g, by + 0.018f, -0.06f, 0.13f, 0.013f, 0.017f, 7f);   // a small PU-style scope
            RifleAnchors(g, by, 0.73f, by - 0.07f, 0.35f, 0.0f, 0.06f);
        }

        /* ---------------- G36C ---------------- */

        static void G36(GunMesh g)
        {
            float by = 0.075f;
            PistolGrip(g, GM.Polymer, 0.03f);
            g.Profile(GM.Polymer, 0, 0.034f, 0.005f,
                -0.08f, 0.03f, 0.075f, 0.03f, 0.075f, 0.0f, 0.13f, 0.0f, 0.13f, 0.045f, 0.32f, 0.045f, 0.33f, 0.06f, 0.33f, 0.105f, -0.08f, 0.105f);
            for (int i = 0; i < 6; i++)   // handguard vents
                foreach (float s in new[] { -1f, 1f }) g.Box(GM.Port, s * 0.0172f, 0.07f, 0.17f + i * 0.025f, 0.0012f, 0.02f, 0.012f, 0.0003f);
            Rail(g, 0.105f, -0.06f, 0.3f);
            g.Box(GM.Dark, 0, 0.11f, 0.04f, 0.012f, 0.012f, 0.03f, 0.002f);   // charging handle (top)
            g.Box(GM.Port, 0.0172f, 0.088f, 0.06f, 0.002f, 0.016f, 0.05f, 0.0004f);
            TriggerGuard(g, 0.03f, 0.004f, 0.068f, GM.Polymer, true);
            g.TubeZ(GM.Dark, 0, by, 0.33f, 0.44f, 0.009f, -1, 14);
            FlashHider(g, by, 0.44f, 0.0105f);
            // folding skeleton stock
            g.Profile(GM.Polymer, 0, 0.028f, 0.005f, -0.08f, 0.1f, -0.32f, 0.1f, -0.33f, 0.09f, -0.33f, 0.074f, -0.1f, 0.082f, -0.08f, 0.082f);
            g.Profile(GM.Polymer, 0, 0.026f, 0.005f, -0.08f, 0.05f, -0.31f, -0.01f, -0.33f, -0.005f, -0.33f, 0.01f, -0.09f, 0.07f);
            g.Profile(GM.Rubber, 0, 0.032f, 0.004f, -0.345f, -0.01f, -0.328f, -0.01f, -0.328f, 0.1f, -0.345f, 0.096f);
            RifleAnchors(g, by, 0.5f, 0.0f, 0.24f, 0.0f, 0.105f);
            g.Anchor("optic", 0.1125f, 0.06f);
            g.Anchor("under", 0.045f, 0.25f);
        }

        /* ---------------- their magazines ---------------- */

        static void Mp5Mag(GunMesh g)
        {
            g.Profile(GM.Dark, 0, 0.022f, 0.002f,
                -0.02f, 0.012f, 0.02f, 0.012f, 0.026f, -0.05f, 0.045f, -0.1f, 0.07f, -0.15f,
                0.04f, -0.165f, 0.018f, -0.11f, 0.0f, -0.055f);
            g.TubeZ(GM.Brass, 0, 0.014f, -0.014f, 0.014f, 0.004f, 0.003f, 10);
        }

        static void P90Mag(GunMesh g)
        {
            // a long translucent box; in its own frame it hangs down (the gun lays it along the top)
            g.Profile(GM.Lens, 0, 0.05f, 0.004f, -0.016f, 0.0f, 0.016f, 0.0f, 0.016f, -0.22f, -0.016f, -0.22f);
            g.Profile(GM.Dark, 0, 0.052f, 0.003f, -0.017f, -0.208f, 0.017f, -0.208f, 0.017f, -0.224f, -0.017f, -0.224f);
            for (int i = 0; i < 8; i++)
                foreach (float s in new[] { -1f, 1f }) g.Tube(GM.Brass, new P3(s * 0.012f, -0.02f - i * 0.024f, -0.012f), new P3(s * 0.012f, -0.02f - i * 0.024f, 0.012f), 0.0028f, 0.0028f, 8);
        }
    }
}
