namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.5", "20mm, special ammo & wall-banging", new[] {
                "ANZIO 20mm: a huge anti-materiel rifle (Viktor sells it, 20mm ammo at Mara's). 260 damage, goes through any armour - and punches straight through a wall into whoever is behind it.",
                "WALL-BANGING: every round can go through one wall if it hits hard enough. Wood: almost anything. Sheet metal: rifle rounds. Concrete: only AP rifle rounds, .50 and the 20mm. Ground and rock stop everything. You see the hole on both sides.",
                "AMMO TYPES: STANDARD, AP (armour-piercing: much better against armour and walls, slightly less damage) and INCENDIARY (sets people on fire, a bit weaker against armour). To switch: put an empty hand on your mag pouch and press B / Y - your next mags come out with that ammo (T on desktop; tapping B / Y with an empty magwell works too).",
                "BURNING: an incendiary hit sets that part of the body alight - it burns, you bleed, you see the flames. Slap the flames on your arm or chest with your OTHER hand to pat them out. Raiders burn too, and everyone sees a burning player.",
            }),
        };
    }
}
