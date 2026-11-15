namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.2", "Armor, casino & floor maps", new[] {
                "Armor now works by penetration: every armour has a class (soft vest ~1.5 up to Juggernaut ~6.5) and every round a punch. Pistols mostly bounce off good armour (you still take a bruising), rifle rounds go through, .338 / .50 go through almost anything - the same for players, raiders and you.",
                "Shots from other players now hurt properly through armour the same way raiders' shots do.",
                "Casino: slots are completely random (every symbol equally likely), the prize wheel has fewer blanks and breaks even on average.",
                "Blackjack: DOUBLE works and tells you when it can't (deal first, only on your first two cards); bigger buttons.",
                "Underground field map shows the floor you're on (B1 / B2 / B3) and changes as you go up and down the stairs.",
                "Also: steadier bags, put things in bags by hand, less delay, two-sided padlocks, spread-out spawns, Underground spawns on B3.",
            }),
        };
    }
}
