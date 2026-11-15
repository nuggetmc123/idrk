namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.0", "Casino, injuries, locks & more", new[] {
                "CASINO in the Market (south wall, by the lift): slot machines, a blackjack table and a prize wheel. Bet $100 - $5,000 of your money.",
                "Injuries: leg hits make you limp and stop you sprinting, arm hits make your aim shake, gut shots drain stamina fast, head hits stun and ring your ears. Medkits, IFAKs and trauma kits fix them; bandages ease a limp.",
                "Padlocked doors in raids: shoot the lock off or smash it with a knife (it breaks for everyone).",
                "Squad radio: your party hears you at any distance over a crackly radio; up close it's your normal voice.",
                "Other players running are loud - you can hear them coming from 50 m. Walking stays quiet.",
                "Magazines load like a real gun: line the mag up under the magwell, push it up the well until it clicks. Drop the old mag first (B / Y).",
                "12 new junk items: duct tape, screws, copper wire, bulbs, toilet paper, matches, bolts, car battery, circuit board, coffee, dog tags, wrench.",
                "Less lag and no kick-outs on a dropped connection (you stay in the raid while it reconnects); harder-kicking snipers.",
            }),
        };
    }
}
