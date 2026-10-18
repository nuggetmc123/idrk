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
        /// <summary>Tutorial Park: where new players learn to drive and build (always solo).</summary>
        Tutorial = 6,
        /// <summary>Tag in karts: bump someone to make them IT.</summary>
        Tag = 7,
        CityRace = 8,
        RooftopBumper = 9,
        GraveyardTag = 10,
        /// <summary>The Backrooms: a yellow maze of rooms. Tag, and you can build in it.</summary>
        Backrooms = 11,
        IslandFreeRoam = 12,
    }

    public static class KartModeInfo
    {
        public static bool IsRace(KartMode mode) => mode == KartMode.Race || mode == KartMode.CanyonRace || mode == KartMode.CityRace;
        public static bool IsBumper(KartMode mode) => mode == KartMode.Bumper || mode == KartMode.IceBumper || mode == KartMode.RooftopBumper;
        public static bool IsFreeRoam(KartMode mode) => mode == KartMode.FreeRoam || mode == KartMode.IslandFreeRoam;
        public static bool IsTag(KartMode mode) => mode == KartMode.Tag || mode == KartMode.GraveyardTag || mode == KartMode.Backrooms;
        /// <summary>Maps where build mode works.</summary>
        public static bool CanBuild(KartMode mode) => IsFreeRoam(mode) || mode == KartMode.Tutorial || mode == KartMode.Backrooms;

        public static string DisplayName(KartMode mode)
        {
            switch (mode)
            {
                case KartMode.Race: return "Race";
                case KartMode.CanyonRace: return "Canyon Race";
                case KartMode.Bumper: return "Bumper Karts";
                case KartMode.IceBumper: return "Ice Bumper";
                case KartMode.FreeRoam: return "Free Roam";
                case KartMode.Tutorial: return "Tutorial Park";
                case KartMode.Tag: return "Tag";
                case KartMode.CityRace: return "Night City Race";
                case KartMode.RooftopBumper: return "Rooftop Bumper";
                case KartMode.GraveyardTag: return "Graveyard Tag";
                case KartMode.Backrooms: return "The Backrooms";
                case KartMode.IslandFreeRoam: return "Island Free Roam";
                default: return "Hub";
            }
        }
    }
}
