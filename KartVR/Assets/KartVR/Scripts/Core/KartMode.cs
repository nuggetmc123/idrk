namespace KartVR
{
    /// <summary>
    /// The four things you can be doing. Every session (public or private) is
    /// always in exactly one of these, stored in <see cref="GameState.Mode"/>.
    /// Named KartMode so it never clashes with Fusion's own GameMode enum.
    /// </summary>
    public enum KartMode : byte
    {
        Hub = 0,
        Race = 1,
        Bumper = 2,
        FreeRoam = 3,
    }

    public static class KartModeInfo
    {
        public static string DisplayName(KartMode mode)
        {
            switch (mode)
            {
                case KartMode.Race: return "Race";
                case KartMode.Bumper: return "Bumper Karts";
                case KartMode.FreeRoam: return "Free Roam";
                default: return "Hub";
            }
        }
    }
}
