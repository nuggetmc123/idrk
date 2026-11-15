namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("1.9", "Less lag, no kick-outs", new[] {
                "Lost connection mid-raid? You stay where you are and keep playing while the game quietly gets you back in - no bunker trip, no reload.",
                "Players who stutter or load for a moment no longer vanish and 'join back'.",
                "Much less network data: voice is half the size and only goes to players within 50 m; fewer movement updates in big raids; raider updates only near players.",
                "Sniper rifles kick much harder (marksman rifles too) - bring the scope back down between shots.",
                "Patch notes added to the menu (this page).",
            }),
        };
    }
}
