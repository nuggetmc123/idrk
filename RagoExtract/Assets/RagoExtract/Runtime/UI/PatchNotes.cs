namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.3", "Multiplayer lag fix", new[] {
                "Fixed the lag and delay that came back in 2.1: the game was sending a separate packet every frame (hundreds a second with 3+ players) and the line backed up. Updates are batched again, 20 a second.",
                "The player passing data on for everyone now sends each other player ONE combined packet per tick instead of one per incoming packet - far less traffic on their connection.",
                "Other players get a slightly bigger smoothing buffer again, so a late packet doesn't make them jump.",
                "From 2.2: armour penetration by calibre, random slots, working blackjack DOUBLE, per-floor Underground map.",
            }),
        };
    }
}
