namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.8", "Found ammo, watch messages, armour comes home", new[] {
                "AP and INCENDIARY rounds are no longer sold - find them in raids. The box says exactly what's in it (e.g. \"5.56x45mm AP x20\"). Put it in your mag pouch to add it.",
                "Each type has its own reserve per calibre. Switching at the pouch (B / Y) only offers AP / incendiary if you have some for the gun in your other hand, and shows how many.",
                "Run out of special rounds and the pouch drops back to standard. Mags you put back return rounds to the right type.",
                "Special rounds go into every raid with you: extract and you keep the rest, die and they're gone.",
                "DIE IN A RAID and you get back the armour you wore in (your starter armour or whatever was on your armour stand). It isn't dropped on your body any more.",
                "No more text on your screen in VR: pickups, the airdrop, vault messages, raid banners, the tutorial and everything else show on your wrist watch (with a buzz). The held item's name and value are on the watch too.",
                "The watch is a bit bigger to fit the messages.",
            }),
        };
    }
}
