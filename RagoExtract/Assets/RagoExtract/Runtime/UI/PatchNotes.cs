namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.1", "Bags, spawns & less delay", new[] {
                "Bags no longer flip round when you hold them: they hang from your hand facing away from you.",
                "Put things in a bag by hand: they stay exactly where and how you put them (resting on what's below). Things only go in when you let go INSIDE the bag, not near it.",
                "Less multiplayer delay: other players are shown much closer to real time, and updates go out the moment they're made.",
                "Padlocks are on both sides of a locked door.",
                "Players and parties spawn as far apart as the map allows, on every map.",
                "Underground: you now start down on the bottom level (B3) and fight your way up.",
            }),
        };
    }
}
