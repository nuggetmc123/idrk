namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.4", "New guns & remodels", new[] {
                "Every gun remodelled with real detail: textured grips with a beavertail and base, proper trigger guards and curved triggers, rubber buttpads, sling swivels, crowned muzzles.",
                "ARs: ejection port + dust cover, forward assist, bolt catch, mag release, selector, takedown pins, flared magwell, castle nut. AKs: rivets, ribbed dust cover, hooded front sight, cleaning rod, vented handguard.",
                "Pistols: raked grips, chamfered slides, front + rear serrations, real ejection port, slide stop, guide rod, hammer and grip safety on the .45s. Revolvers show their chambers, ejector rod and latch.",
                "Bolt rifles: proper actions, bolt shroud, checkering; shotguns: loading port, slide bars, mag cap; SMGs, LMGs and .50s got their own details too.",
                "13 NEW GUNS: Falcon Compact, Warhawk .45, Frontier Six (.357), Vespa 9, Reaper 45, Corsair CQB, Vostok SU, Tundra AK-12, Bulwark 308, Sentry SR-25, Glacier .338, Anvil LMG, Harvester Pump. In the shops, in loot, and some raiders carry them.",
            }),
        };
    }
}
