namespace KartVR
{
    /// <summary>
    /// What a session is doing, which is also which map it's on. Every session
    /// (public or private) is always in exactly one of these, stored in
    /// <see cref="GameState.Mode"/>. Named KartMode so it never clashes with
    /// Fusion's own GameMode enum. Several maps can share a kind of game
    /// (Race and Canyon Race are both races).
    /// </summary>
    public enum KartMode : byte
    {
        Hub = 0,
        Race = 1,
        Bumper = 2,
        FreeRoam = 3,
        CanyonRace = 4,
        IceBumper = 5,
    }

    public static class KartModeInfo
    {
        public static bool IsRace(KartMode mode) => mode == KartMode.Race || mode == KartMode.CanyonRace;
        public static bool IsBumper(KartMode mode) => mode == KartMode.Bumper || mode == KartMode.IceBumper;

        public static string DisplayName(KartMode mode)
        {
            switch (mode)
            {
                case KartMode.Race: return "Race";
                case KartMode.CanyonRace: return "Canyon Race";
                case KartMode.Bumper: return "Bumper Karts";
                case KartMode.IceBumper: return "Ice Bumper";
                case KartMode.FreeRoam: return "Free Roam";
                default: return "Hub";
            }
        }
    }
}
