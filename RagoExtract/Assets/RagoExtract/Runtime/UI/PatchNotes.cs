namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.16", "Player markets on screens, clearer trade table", new[] {
                "The booths are gone - the hall is clear again. Instead there are BLACK MARKET SCREENS (by the lift and in the middle of the hall). PLAYER MARKETS lists everyone selling; OPEN one to see what they sell and what they want for each thing - money, or items (guns, armour, keycards, meds, anything on the list) and how many.",
                "MY MARKET (level 5+): press CREATE MY MARKET, put items on the red square, pick which one to sell, then pick what you want for it: MONEY (and the price) or ANY item in the game - choose the kind (guns, armour, bags, optics, attachments, meds, ammo, valuables & keys, grenades & melee, junk) and then the exact item, and how many. Press LIST IT. UNLIST sends it back to your bunker. Your market is open whenever you're in the Market.",
                "Bigger text on the black market screens. BUY holds your money until the seller's game hands it over; TRADE takes what they want off the red square. Whatever you get goes straight to your bunker (both sides).",
                "TRADE TABLE: the controls are a low console along your edge, tilted up towards you, so you can see the items on the table past it. The text no longer runs together - your name, what you give and the money are on separate lines, bigger, and it shows clearly how much money you PAY and GET. A board over the table shows the whole deal to both traders and anyone watching.",
                "The black market screens are now together in the middle of the hall, facing the lift, under a BLACK MARKET sign - you see them as soon as you step out (they used to face the walls).",
                "Nothing sells by itself any more - not at the Exchange and not at the traders: put items on the red square, check the price, then press SELL. Take things back off if you change your mind.",
                "FIXED: the traders' price boards, their sell clipboards, the Exchange receipt and Bruno's job board were tipped forward at you - they now lean back like a board propped on the counter.",
                "FIXED: guns and gear on the traders' back shelves floated above the shelf - they sit on it now (and long guns are scaled to fit their spot).",
                "FIXED: armour on the bunker armour stands sat on top of the forms - the vest now wraps the torso form and the helmet sits on the head form.",
                "LESS DELAY in the Market and raids: your movement is sent 20 times a second (was 15) and goes out the moment it's ready instead of waiting for the next batch, and other players are drawn closer to real time (a smaller smoothing buffer).",
                "PARTY INVITES from the Market or a raid now go out straight away (they used to wait until you were back in your bunker) - your friend gets them wherever they are.",
                "Items laid on the red square lie across it (guns no longer point at you or into the screen).",
            }),
            ("2.15", "Swap trades, booths for everyone, med fix", new[] {
                "BLACK MARKET SWAPS: when you LIST, press WANT to ask for an item instead of money (any gun, a vault keycard, a military radio, armour, night vision, valuables, a bag, optics, AP / incendiary ammo). Buyers put what you want on the red OFFER square and press TRADE - both items are sent straight to the two players' bunkers.",
                "You need to be LEVEL 5 to sell to other players (listing on the computer or claiming a booth).",
                "Booths for everyone: 8 little booths through the middle of the Market - claim one and it's yours to display items and set prices while you're there.",
                "FIXED: meds duplicating and piling up in the Lost & Found. The Med Bay cabinet keeps exactly what you left in it, it's stocked once, and meds you take out don't respawn.",
            }),
            ("2.14", "Traders are back, black market, bunker music, map sync", new[] {
                "ALL TRADERS ARE BACK with everything they sold (Viktor, Mara, Doc Hale, Silas) - but gear unlocks with your LEVEL: low-tier guns and gear at level 1, then every tier 5 levels further (5, 10, 15, 20). AP / incendiary rounds are still found in raids only.",
                "Your LEVEL is on your watch now (LV n, next to your money).",
                "BLACK MARKET computer in the Market (by the lift): every player listing on one screen - BUY from there (it lands on the desk, your money is held until the seller's game hands it over), TRADE (asks the seller to meet you at the trade table), or put an item on the red pad, set a price and LIST it.",
                "Player stalls moved to a row through the middle of the hall so they don't clash with the traders.",
                "BUNKER MUSIC: an original track the game composes itself (warm lo-fi / synthwave: pads, electric piano, bass, soft drums, a lead with echo). The hum / static in the bunker is gone.",
                "FIXED: players on different game versions ended up in the same raid and saw different worlds (hills and buildings one of them didn't have - they looked like they were floating). Raids and the Market now only match players on the same version, and if someone's version or map doesn't match yours your watch tells you.",
                "Doors are synced: when someone pushes or swings a door, everyone sees it move.",
            }),
            ("2.13", "Player trading, the bunker pug, gore, wounded scavs", new[] {
                "PLAYER TRADING in the Market: a TRADE TABLE - each of you stands at a side, puts items (and cash) on your half and presses ACCEPT. It only swaps when both of you have accepted exactly what's on the table; any change un-accepts. What you get lands on your RECEIVED tray.",
                "PLAYER STALLS: claim one of four stalls, hang items on the hooks and counter, dress the mannequin as a kit (helmet, vest, gun, bag - one price), set prices with + / -. Buyers press BUY: their money is held until your game hands the item over, or it's refunded. Unsold items go back to your stash when you leave.",
                "A PUG lives in your bunker: it wanders about, follows you, sleeps on its bed and eats from its bowl. Fill the bowl by touching the DOG FOOD sack (or the bowl) with an empty hand and pulling the trigger. Stroke it and its tail goes mad. Hungry with an empty bowl? It sits at your feet and yaps.",
                "Better gore: more blood, exit wounds with their own spray, wound holes, blood running down walls, headshots blow out the back, frags tear people up, bigger pools.",
                "Scavs react to where they're hit: head snaps back, chest/gut hits double them over, arm hits ruin their aim (enough and they fight one-handed), leg hits make them limp or knock them down.",
                "Night vision mounts on your HELMET now (you need one on) - you and everyone else see it flipped up or down. Take the helmet off and the goggles come with it.",
                "UNDERGROUND: much darker, and 1 minute after the first extract opens the POWER CUTS - the lights flicker and die, only red emergency lamps are left. Scavs everywhere: every room on every level plus corridor patrols.",
                "Sound through floors (Underground): you can't hear footsteps or scav voices from other levels any more, gunfire from another level is faint and muffled, and scavs can't hear you through the floors.",
                "Suppressed shots: scavs only hear them within ~8 m.",
                "NIGHT raids are really dark now - bring a light or night vision.",
                "PLAYERS ONLINE count fixed: people sitting in their bunker are counted now, and players in the Market or a raid stay on the count (it used to say 1).",
                "Bags turn with your hand when you hold them open.",
                "The mag pouch readout shows what your next mag is LOADING (STANDARD / AP / INCENDIARY) and how many AP / incendiary rounds you have per calibre.",
                "Text is back: messages on your watch again (airdrops, vaults, pickups...), pouch ammo readout, vault LOCKED / HACKING / OPEN sign, tutorial on the watch.",
            }),
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
