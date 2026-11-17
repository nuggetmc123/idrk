namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.12", "Raid timer and item prices on the watch", new[] {
                "The RAID TIMER is back on your watch: big, top right, and it turns red in the last 5 minutes.",
                "Grab anything and your watch shows what it is and how much it sells for at the Exchange (guns still show their ammo).",
                "FIXED: the Exchange now buys everything again - guns, armour and valuables were worth $0 after the gunsmith and fence left.",
                "Bleeding: your HP on the watch flashes red (with a + mark) instead of the long BLEEDING word, so the timer fits.",
            }),
            ("2.11", "Body map on your watch, name tapes", new[] {
                "TAP YOUR WATCH with your empty right hand: a little hologram of your body pops up over it. Each part goes green, yellow, red as it's hurt, a red dot marks every hit you've taken, the part hit most pulses, and a bar underneath shows your health (it flashes red while you're bleeding). Tap again to hide it (it hides itself after 15 s).",
                "Meds heal the body map too; a new raid starts it clean.",
                "NAME TAPES: every player has their name sewn on their chest (the name, not the #tag callsign), and every scav has a surname - bosses wear theirs.",
                "The AMMO label on your mag pouch is back.",
            }),
            ("2.10", "No pop-up text at all", new[] {
                "No pop-up messages anywhere any more - not on your screen and not on your watch. Pickups, airdrops, vaults, raid start, death, errors: you get a buzz in your left hand instead.",
                "No floating labels: the held item's name / value, the AMMO label and readout on your mag pouch, the vault's LOCKED / HACKING / OPEN sign, the tutorial line and player name tags are gone (teammates still have the green dot).",
                "Menus, shop boards, the raid terminal and wall signs still have their text so you can use them.",
            }),
            ("2.9", "Market cut down, zero screen text", new[] {
                "The Market now has just three: MARA sells ammo (standard rounds only - AP / incendiary are found in raids) and grenades, DOC HALE sells bandages, field medkits and stims (syringes), and BRUNO still does your bunker upgrades.",
                "The gunsmith (Viktor) and the fence (Silas) are gone - guns, armour, optics and bags now come from raids. Sell loot at the Exchange.",
                "NO text on your screen at all in VR: death, loading and every other banner go on your wrist watch. The floating reserve text over your mag pouch is gone too (the watch shows your ammo).",
            }),
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
