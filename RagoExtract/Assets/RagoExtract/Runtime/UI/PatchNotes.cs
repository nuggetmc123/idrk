namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("3.11", "DLC checkout details", new[] {
                "When the Meta store can't sell a DLC pack, the message now names the pack's SKU and what the store said, so the add-on can be matched up in the developer dashboard.",
            }),
            ("3.10", "DLC store fix", new[] {
                "Fixed: buying DLC failed with \"not entitled\" / \"PackageNotInLibrary\" and the game said it wasn't a Meta store copy - it was checking the wrong Meta App ID. It now uses the game's real App ID, so the store check passes and DLC packs can be bought.",
            }),
            ("3.09", "Store check details", new[] {
                "When the Meta store check fails, the game shows what it checked: the App ID, the package name and version, and who installed this copy (the store, or a cable) - so a wrong App ID or a sideloaded copy is easy to spot.",
            }),
            ("3.08", "Clearer store errors", new[] {
                "A DLC purchase that the Meta store refuses now says why in plain words (for example: this account doesn't own this copy - install it from the Store app) instead of a wall of error codes.",
            }),
            ("3.07", "Store upload fix", new[] {
                "The Android version code now follows the game version (3.07 = 307), so every new build can be uploaded to the Meta store (it refused a second upload with version code 1).",
            }),
            ("3.06", "SEASON 2 - full wipe", new[] {
                "SEASON 2: every account is reset the first time you start this version - money, level, stash, gear, bunker and your Market shop. Everyone starts fresh with $6,000 and the starter kit. Your callsign, clothes, friends, settings and DLC packs you bought are kept.",
                "The black market on the server is cleared for the new season too (once the server is updated).",
                "Fixed: players on a slow or patchy connection could get kicked from the server - movement and voice that can't get through in time are now skipped instead of piling up until the server dropped you.",
            }),
            ("3.05", "Reload on the move, grab mags crouched", new[] {
                "Fixed: crouched, you could hardly pull a mag from your hip pouches (your hands were treated as reaching for the floor, which skips your own gear). The floor cut-off now goes down with you.",
                "Fixed: loading a mag while running kept missing - the magwell is lined up with where the gun is in your hand right now, not where it was drawn a frame ago, and the mouth of the well is more forgiving while you move. Same for shells and speedloaders.",
            }),
            ("3.04", "Straight drums", new[] {
                "Drum mags on the AK-type rifles and the box shotguns now hang straight down instead of tipping back towards the trigger (they leaned like the curved mag does). They still face forward, winding key towards the muzzle.",
            }),
            ("3.03", "Mags seated like the real thing", new[] {
                "Every magazine now goes up INSIDE its gun the way it does in real life - no more gap between the mag and the gun. Fixed on all AKs (and their drum mags), both box shotguns, the boxy SMGs, the bullpups and the anti-materiel rifle; the pistols and machine pistols were done in 3.02.",
                "The DLC STORE wall is gone from the Market - buy packs from the DLC tab in your menu instead (same packs, same prices).",
            }),
            ("3.02", "New injuries, true aim, mags that fit", new[] {
                "INJURIES: bleeding never stops on its own any more - use a bandage or a medkit. A shot leg makes you limp until you press a SYRINGE (stim) to that leg and pull the trigger. Take enough hits on one arm or leg and it BREAKS (a broken arm shakes your aim badly, a broken leg slows you right down) - hold a syringe to it to set it. Medkits still heal and fix gut wounds; the body map stays.",
                "Shots go exactly where your sight points: the line through your scope, red dot or iron sights is followed out to whatever it's on, and the bullet goes straight there - right on at every range.",
                "Fixed: running sideways while shooting threw your shots off to one side - the gun is snapped to your hand at the moment you fire.",
                "Fixed: pistol and machine pistol magazines hung under the grip - they slide right up inside it now, only the base plate showing.",
                "Fixed: getting into a vehicle in multiplayer could look like you teleported somewhere else - everyone's copy of it is put exactly where it really is when you get in (and while you sit in it).",
            }),
            ("3.01", "Manda plays fetch", new[] {
                "Your pug Manda plays FETCH: there's a tennis ball by her bed - throw it (or anything small) across the bunker and she watches it land, runs after it, picks it up in her mouth and trots it back to drop at your feet, tail going like mad. Grab it out of her mouth any time.",
            }),
            ("3.00", "Voices back, vault through windows", new[] {
                "Voice chat works again: teen accounts could only be heard by their own squad (now that's just for under-13 accounts), voice cut out whenever the connection lagged a little, and quieter headset mics never opened. Everyone near you hears you again.",
                "Climb through windows the easy way: walk up to a window with the glass broken out and keep pushing forward - you hop over the sill and land on the other side. (Glass still has to be shot or smashed out first.)",
            }),
            ("2.99", "No more getting kicked", new[] {
                "Fixed: the game closed itself about 30 seconds after starting for anyone playing a copy that wasn't installed through the Meta store (the ownership check closed it) - it looked like a crash or being kicked from the server, over and over. It never closes the game now; a copy from outside the store just can't buy DLC.",
            }),
            ("2.98", "Crash fixes", new[] {
                "Fixed: the game could crash after a while of playing. Every trip between your bunker, the Market and a raid left the old area's meshes, textures and materials in memory, so after a few raids the headset ran out of memory and closed the game. They're freed on the loading screen now. The field map's photo (4-12 MB a raid) leaked the same way and is freed too.",
                "Fixed: a scope being destroyed could break the other scopes of the same kind.",
            }),
            ("2.97", "Raid summary, creator badge", new[] {
                "RAID SUMMARY: after every raid - extracted or dead - the board in your bunker shows everyone you killed, by name (bosses marked, headshots noted, other players you took out), and if you died, who killed you and with what.",
                "CONTENT CREATORS: players who get the CC trophy from the owner now wear a red name tape with a gold-rimmed CC badge (a little camera) for everyone to see, and their watch shows CONTENT CREATOR.",
            }),
            ("2.96", "Guns stay on the floor", new[] {
                "Fixed: putting a gun down on the floor could drop it straight through and lose it under the house - if your hand went a little below the floor as you let go, it got stuck in the floor and fell out underneath. Now it's lifted onto the floor when you let go, and anything that ever ends up stuck inside a floor, table or wall pops back up onto it.",
            }),
            ("2.95", "Real nuke, sitting riders, better grabbing", new[] {
                "The NUKE is a proper show now: a missile streaks down out of the sky, a blinding flash, a fireball swells, a shockwave ring and a wall of dust race out across the ground - the blast kills you when it reaches you - and a churning mushroom cloud climbs on its stem with a condensation ring, slowly cooling from orange fire to grey smoke.",
                "Picking things up off the floor: your hand no longer grabs your own holstered gun or armour when you reach down for something, and it catches things lying on the floor even if your hand dips a little under them.",
                "Sidecar bike: taller handlebars, easy to grab. Riders really sit now - on the saddle with feet on the footboards, or down in the sidecar - whether you play standing or sitting, and that's how everyone else sees you too.",
                "Fixed: the ground flickered like a texture glitch in places (two layers of ground lay at exactly the same height), and roads flickered where they cross.",
            }),
            ("2.94", "Holiday Pack for in-game cash", new[] {
                "The HOLIDAY PACK now costs $25,000 of in-game cash instead of real money - buy it from the DLC tab or the DLC store in the Market. You still get its 3 festive skins, the Candy Cane club and 3 impact grenades.",
            }),
            ("2.93", "Hop on the bike by the throttle", new[] {
                "Grab the sidecar bike's handlebar (the right grip is the throttle) and you climb straight on with your hand already on the bars - just pull the right trigger to ride. The grips are easier to catch, too.",
            }),
            ("2.92", "Shushed figurine", new[] {
                "NEW STREAMER FIGURINE: SHUSHED - the operator in full Juggernaut armour (black plates with gold trim and rivets, shoulder plates), red helmet with a gold crest, red gloves, one finger to his lips. Epic rarity: crates and safes, sells for $9,000.",
                "Streamer figurines have their streamer's name on the front of the base: PEANUT8 and SHUSHED. (The peanut one is now called the Peanut8 Figurine.)",
            }),
            ("2.91", "No more spectating", new[] {
                "The owner can no longer watch a player's view - that feature is gone completely, and your watch never shows SPECTATED.",
            }),
            ("2.89", "Sidecar bike: proper handlebars, all bolted together", new[] {
                "The sidecar bike has real handlebars now: wide chrome bars on risers with ribbed rubber grips, levers, mirrors, a speedo, and the headlamp on the forks (it turns with them).",
                "Everything on the bike is connected: a swingarm and a sprung shock for the back wheel, a subframe under the saddle and pillion, fender stays, footboard brackets, an arm and an arched mudguard for the sidecar wheel, a carrier for the jerrycan.",
                "The gunner can see: the machine gun sits higher, at a seated gunner's eye line, and the shield has a wide window over the barrel to aim through (it stops at chin height, so you see out over it too). The ammo can sits low, out of the way.",
            }),
            ("2.88", "Sidecar bike, Skin Wheel, better paint booth", new[] {
                "NEW VEHICLE: the SIDECAR BIKE - an old army motorcycle with a sidecar and a mounted machine gun. One rider drives with the handlebars (grab the bars or the saddle to get on; right trigger gas, left trigger brake / reverse); a second player climbs into the sidecar (grab its handle) and works the gun: grab its two grips, aim by moving your hands, pull either trigger. 250-round belt, then a reload. Every third parked vehicle on the outdoor maps is a bike.",
                "SKIN WHEEL in the Market (south wall, by the casino): $10,000 a spin and you win a gun skin for good - it only ever lands on skins you don't have yet.",
                "The PAINT BOOTH has a new board: put a gun on the mat and see it in each of your skins - big, turning, with the skin's name and where it came from - before you press PAINT. There are no free starter skins any more: the booth is empty until you win skins on the wheel or own a DLC pack. (Guns you already painted keep their paint.)",
                "Trader level locks are back: better guns and gear unlock as you level up (common at level 1, then levels 5, 10, 15 and 20). Ammo, meds, grenades and knives are always for sale.",
                "Golf carts and bikes turn much tighter, need less turning of the wheel, and fly off ramps and crests instead of sticking to the ground.",
                "Fixed: the fire axe was held with its blade facing back at you - the edge now faces the way you swing.",
            }),
            ("2.87", "Stash walls remember", new[] {
                "Fixed: guns and gear hung on a bunker wall didn't stay where you put them. Coming back, everything was refilled into the first wall of its kind - guns on the Gear wall jumped to the Armory, and rifles that didn't fit there were dumped on the main hall floor. Now every item goes back on the exact wall and spot you left it on.",
            }),
            ("2.86", "New gear models", new[] {
                "Every helmet, vest, grenade and melee weapon has a new, much more detailed model. Helmets get liners, chin straps, proper ear defenders with mics, rails with slots, NVG shrouds, vents, velcro and visor hinges. Vests get MOLLE webbing, mag pouches with flaps and pull tabs, cummerbunds, padded shoulder straps, radio and tourniquet pouches and drag handles. Grenades have real fuses, curved spoons and pull rings, and the flashbang is perforated. Knives get proper blade shapes with fullers, edges and serrations; the bat, crowbar, machete, sledgehammer, fire axe, frying pan and candy cane are all rebuilt.",
            }),
            ("2.85", "Peanut Streamer Figurine", new[] {
                "NEW VALUABLE: the Peanut Streamer Figurine - a cartoon peanut in a gaming headset, mic up, waving to chat, on a LIVE display base. Epic rarity: turns up in crates and safes and sells for $8,500.",
            }),
            ("2.84", "Smarter raiders, smooth golf carts", new[] {
                "Raiders take cover: when you hit them they often duck behind something low and fight from there, crouching between shots, and they kneel to shoot at range. Badly hurt, a scav runs off somewhere you can't see, crouches and bandages up, then comes back looking for you.",
                "Everyone sees raiders bandaging, crouching and getting knocked down now - before, only the player whose game ran that raider saw it.",
                "Golf carts in multiplayer: other players now sit in the seat on your screen instead of running along behind the cart, and the cart drives smoothly instead of stuttering. Everyone sees the cart and its riders in the same place.",
            }),
            ("2.83", "Bosses fight back", new[] {
                "Bosses and scavs now react when you shoot them from anywhere - from behind, from far away, or from inside a building. They spin round to face where the shots came from, spot you from any angle up to 160 m away and shoot back. If they can't see you, they come after you. No more farming a boss that just stands there taking it.",
            }),
            ("2.82", "New item models", new[] {
                "Every med, ammo box, keycard, valuable and piece of junk has a new, much more detailed model: the medkit has latches, a handle and corner bumpers, pill bottles have ribbed caps, the stim is a proper auto-injector, drinks cans have real can shapes and ring pulls, the whiskey bottle has a label and cork, the graphics card has its fans, the laptop is a rugged case, wire comes on a reel, and so on. Screens, glass and glossy labels now shine.",
            }),
            ("2.81", "Beta Tester skin", new[] {
                "BETA TESTER SKIN: dark carbon with orange chevrons and teal pinstripes - a thank-you gift for beta testers from the owner. If you get it, paint it on any gun at the booth in your bunker.",
                "The owner can watch a player's view for moderation - your watch shows SPECTATED while that's happening.",
            }),
            ("2.80", "Mod spawn tools, smooth DLC tab", new[] {
                "Moderators: the MOD tab has a SPAWN TOOLS page - spawn any item, spawn raiders and bosses, ammo, god mode and invisible-to-AI.",
                "Fixed: the DLC tab stuttered every second or so while you looked at it. The rifle previews are built once and reused now, and the page doesn't keep redrawing itself. The Market's DLC wall flips pages without a hitch too.",
            }),
            ("2.79", "Season reset fixes", new[] {
                "The Season 1 reset now gives everyone the normal starter kit (a pistol and rifle, meds, ammo, and a helmet and vest on the stands) instead of an empty bunker with no gun.",
                "The reset can no longer get stuck and run again on every start.",
            }),
            ("2.78", "Crash fix", new[] {
                "Fixed: the game crashed as soon as it started (store and release builds). The build was stripping out parts of Unity the game builds its world with; nothing is stripped now.",
                "DLC packs are never free: they're bought through the Meta Quest store. Packs claimed for free during the test period are taken back; any you actually bought come back automatically.",
            }),
            ("2.77", "Store start-up fixes", new[] {
                "Fixed: the game could close by itself right after starting from the Meta store. The ownership check now retries if Meta's store is slow to answer, and if it really can't confirm your copy it says so on screen for 15 seconds instead of just closing.",
                "The game keeps a log file on the headset (rago_log.txt) to help track down crashes.",
            }),
            ("2.76", "Meta store build fixes", new[] {
                "Builds now pass Meta's store checks: Android target version 34 and install location \"auto\".",
            }),
            ("2.75", "Season 1: everyone starts fresh", new[] {
                "SEASON 1 - the test period is over and every account has been reset. Money is back to $6,000, level and stats to zero, and your stash, gear, ammo, bunker (rooms, floor, med cabinet) and deliveries are all gone. The black market starts empty too. Your callsign, friends, settings and any DLC packs you own are kept.",
                "The TESTER menu tab is gone.",
            }),
            ("2.74", "No level locks at the traders", new[] {
                "TRADERS sell everything from the start - no more \"reach level X\" locks. Instead the better it is, the more it costs: common gear is about 1.8x its value, uncommon 2.4x, rare 3.2x, epic 4.2x. Ammo and meds stay cheap (1.5x). Selling to traders pays the same as before. You still level up from raids (it shows on your profile and watch).",
            }),
            ("2.73", "Meta store switched on", new[] {
                "The Meta XR Platform SDK now comes with the project (Unity downloads it by itself when you open it), so the ownership check and real-money DLC are on in headset builds.",
            }),
            ("2.72", "Meta store ready", new[] {
                "Connected to the Meta Horizon Store (once the Meta XR Platform SDK is added in Unity): the game checks you own it, DLC packs are bought with real money through Meta's checkout (BUY in the DLC tab), and packs you own come back after a reinstall or on a new headset.",
            }),
            ("2.71", "Safety for younger players", new[] {
                "Younger players (Meta tells the game a player's age group): voice chat only with your own squad - strangers can't hear you and you can't hear them - and the Market casino is closed (18+). Everyone else plays as before.",
                "Privacy policy: game.idrk.dev/privacy explains what the game uses (callsign, a random player ID, voice relayed live and never stored) and who to contact.",
            }),
            ("2.70", "DLC menu, 5 new packs, better grips", new[] {
                "DLC TAB in your menu: every pack as a tile with its price. Tap one to see everything it comes with - try each skin on a rifle by tapping it, see the items and where they go - then GET IT. Packs are FREE for now; the price they'll cost later is shown.",
                "5 NEW DLC PACKS: GOLD (Solid Gold, Black Gold, Mirror Chrome + a gold chain), SYNTHWAVE (Synthwave, Vaporwave, Laser Grid), PREDATOR (Zebra, Leopard, Python), ELEMENTS (Lava, Ice Crystal, Lightning, Toxic Slime) and OPERATOR (Ranger Green, Mountain Shards, Desert Digital + 2 IFAKs and a stim).",
                "The DLC STORE wall in the Market shows 4 packs a page (< > to flip).",
                "FOREGRIPS: your support hand now actually holds the grip you fitted - wrapped round a vertical grip, leaning forward along the ramp of an angled grip with your thumb on the handguard, high and short on a stubby. Grab anywhere near the grip or the handguard.",
                "The ANGLED GRIP looks like a real one: a long rail clamp, a ramp for your palm with grip ridges, and a lip at the front.",
            }),
            ("2.69", "Camera fixed", new[] {
                "Fixed: pressing the trigger at the Camera often did nothing - you had to hit a tiny spot your controller couldn't reach (it bumps the headset first). Now anywhere up by the right side of your head works, and the controller buzzes when it starts or stops.",
                "Fixed: on PC, videos only saved if you had ffmpeg installed. Now they always save - as an .avi in Videos\\Rago Extract (plays in Films & TV, Windows Media Player and VLC), or as an .mp4 if ffmpeg is there.",
                "Fixed: on headsets that can't hand frames back from the graphics card, nothing got recorded. They now use a slower backup way, and the headset's video encoder gets a second picture format to try if it won't take the first.",
                "When you stop recording it now says how many frames were saved - or why nothing was saved, instead of saying nothing.",
            }),
            ("2.68", "Death fix, golf cart fix", new[] {
                "Fixed: sometimes after dying you never went back to your bunker (you stayed dead). Dying just as a raid finished (extracting, the raid clock running out, the nuke) skipped the trip home, and any hiccup on the way home could leave you stuck. Every step is guarded now, and if you're ever still dead after 15 seconds you're sent home anyway.",
                "Fixed: pushing a golf cart down on one side before getting in made it drive along leaning over. It settles flat on its wheels when you get in, and rights itself firmly while you drive.",
                "Fixed: the Camera kept recording (and could come back) after you died - it goes with your helmet now.",
            }),
            ("2.67", "Concert, Nuke, smoother multiplayer", new[] {
                "NEW LIVE EVENT - CONCERT: a stage goes up in the Market in front of the roller door - a DJ on the decks, a flashing LED wall, speaker stacks, moving spotlights with beams and green lasers, all on the beat of a dance track. Everyone in the Market hears the same beat. Climb up on the stage.",
                "NEW LIVE EVENT - NUKE: a 5-minute countdown in every raid, with air-raid sirens and the last seconds counted down in front of you. Extract before it hits. If you're still in a raid at zero: a white flash, a mushroom cloud climbs over the map, and the blast wave kills everyone left (raiders too). Mods press LAUNCH; STOP calls it off.",
                "SMOOTHER MULTIPLAYER: raiders run by other players now glide instead of jumping - each update is stamped with the sender's clock and played back a moment behind, the way players already were. They're also shared from twice as far away, so they don't pop in as you get close.",
                "IMPACT GRENADES go off on whatever they hit - a wall, the floor, a raider, a car - however softly. Throwing one arms it even if you forgot the pin.",
                "BACKPACK: grabbing it off your shoulder now takes GRIP + TRIGGER together (grip alone kept grabbing it by accident). It opens at the front: the flap flips away from you, so the open bag faces you.",
                "Fixed: you can sprint (click the left stick) with AUTO SPRINT off again.",
                "Fixed: tables were built inside each other in some houses (North Pole cabins), boxes stuck through shelves or floated, and wooden floors and walls were covered in shimmering stripes - they're proper boards now.",
            }),
            ("2.66", "Camera, CC Trophy", new[] {
                "The Creator Cam is now just the CAMERA - same helmet camera, same RECORD button, saves the same videos.",
                "CC TROPHY: a gold cup on a black plinth with a red play button on the front, for content creators who make videos of the game. Only the owner gives it out - it turns up on your Deliveries screen in the bunker (if you're offline, it waits until you log in). It never drops in raids or presents.",
            }),
            ("2.65", "Live events", new[] {
                "LIVE EVENTS: moderators can start an event for every player on every server at once, from the MOD tab in the menu (pick the event, how long, START - STOP ends it early). The owner can start them from the web panel too. One at a time:",
                "  DOUBLE XP - all XP counts twice.   DOUBLE CASH - the traders pay double when you sell.",
                "  BOSS HUNT - every map's boss is out in every raid.   AIR DROP RAIN - a plane drops a crate every 3-4 minutes in raids.   PRESENT RAIN - presents in crates everywhere.",
                "When an event starts everyone gets a big announcement, and your watch shows the event and the time left while it runs. Players who log in during an event are told straight away.",
            }),
            ("2.64", "Creator Cam saves videos, wounded NPCs", new[] {
                "CREATOR CAM RECORDS FOR REAL: pull the trigger at the cam and it films its steady first-person view to a video file (MP4, 30 fps, no sound). On a Quest it goes to Movies/Rago Extract (open it in Files or your headset's media). On PC it goes to your Videos\\Rago Extract folder - this needs ffmpeg.exe next to the game or installed (free from ffmpeg.org); it tells you if it can't find it. You get a message with the file name when you stop.",
                "NPCs BLEED: a solid hit leaves them bleeding - blood drips and their health drains until they deal with it, and they can bleed out (the kill is yours).",
                "NPCs PATCH THEMSELVES UP: once they're out of your sight (or not fighting) a bleeding NPC kneels, lowers their gun and wraps a bandage round the wound for a few seconds, then gets back up with the bleeding stopped and some health back. Shoot them while they do it and they give up on it.",
                "NPCs KNOCKED DOWN by a car, a grenade or a big hit now go flying back with their gun still in their hand, instead of the gun hanging in the air where they stood.",
                "Fixed: the models on the SPAWN ITEM board were huge - they now fit their tiles.",
            }),
            ("2.63", "Creator Cam, comfort settings", new[] {
                "CREATOR CAM (for content creators): an action camera that clips onto the right side of your helmet (hold it to your helmet and let go). One button: reach to it with an empty hand and pull the trigger to RECORD / stop (desktop: C). While recording its red light glows for everyone to see and your watch shows a blinking REC and the time. On PC the game window shows the camera's steady first-person view to capture with OBS; on a Quest, use the headset's own Record. Sold by Mara, found in raids, and in the tester spawn board (OTHER).",
                "AUTO SPRINT (menu > SETTINGS > MORE): push the stick right forward and you break into a run by yourself.",
                "SEATED / STANDING mode (SETTINGS > MORE): play sitting down - your operator still stands at full height (use the crouch button to crouch).",
                "HEIGHT CALIBRATION (SETTINGS > MORE): stand (or sit) up straight, press CALIBRATE and keep still for 3 seconds - your eyes line up with your operator's, so your hands, body and gun all sit right. Separate for seated and standing; RESET HEIGHT undoes it.",
            }),
            ("2.62", "Black market always open, Deliveries screen", new[] {
                "BLACK MARKET SHOPS STAY OPEN: on our game server every player's shop is kept on the server, so you can buy from (or trade with) anyone's shop even when they're offline - it says \"owner offline - still open\". They get the money or your items next time they play. Nothing can be bought twice.",
                "DELIVERIES screen in your bunker (west wall of the main hall, where the Lost & Found used to be): everything you buy or trade for - black market, traders, DLC store - and anything taken off your shop waits there with where it came from. Press TAKE and it comes out on the shelf underneath. Your sales show up too (\"SOLD ... for $\") - press OK to clear them.",
            }),
            ("2.61", "Real night vision, your market, hitboxes", new[] {
                "NIGHT VISION looks like the real thing: the picture turns monochrome phosphor green, fine grain fizzes and flickers across it, and you look down a round tube with black all round it. The dark lifts evenly instead of everything glowing flat green.",
                "The BLACK MARKET screens list YOUR MARKET first (how many items you have up, open or closed), with a MANAGE button.",
                "Hitboxes fixed on the snowmen and Evil Santa: shots that visibly hit their big snowballs, belly or beard used to go straight through - their hitboxes now match their bodies (and the thin twig arms are thin).",
            }),
            ("2.60", "Server address", new[] {
                "The game connects to the server by name now: game.idrk.dev:27015 (it keeps working if the server ever moves). The owner's admin panel is at http://game.idrk.dev:27015/.",
            }),
            ("2.59", "Moderation", new[] {
                "MODERATORS: players the owner makes moderators get a MOD tab in the menu. Find a player (type part of their callsign - matching players pop up as you type), pick a reason and a ban length, then BAN, UNBAN, WIPE or KICK (Market only). Nothing happens until the owner approves it on the admin panel.",
                "Bans follow the headset, not just the callsign: changing your name doesn't get around one. A banned player sees why and until when, and can't play online.",
                "WIPE resets a player's money, stash, gear, level and bunker (callsign, clothes, settings and DLC stay) - right away if they're online, otherwise next time they connect.",
                "Adding a friend now suggests callsigns as you type too (\"nug\" -> NUGGIE#1252).",
            }),
            ("2.58", "Anti-cheat", new[] {
                "Anti-tamper: the game checks its own APK signature. A copy someone unpacked, modded and repacked is signed with a different key (or marked debuggable) - it still runs, but online play is switched off for it.",
                "Hits from other players are checked: impossible damage for that weapon, unknown weapons and machine-gun-fast hit streams are ignored, so a modded client can't one-shot you.",
                "The game server drops clients that flood it with messages.",
            }),
            ("2.57", "Longer fights, run-overs, fixes", new[] {
                "Longer time-to-kill: every bullet does 35% less damage (players and raiders alike), so fights last longer.",
                "Golf carts plough through people: hit a raider or player at speed and they go flying as a ragdoll while you keep driving (the cart used to stop dead). Raiders get back up a couple of seconds later if they survive.",
                "Presents: just HOLD THE TRIGGER - it rattles and wobbles for a second, then bursts open (shaking was unreliable).",
                "Evil Santa drops the NORTH POLE KEYCARD itself when he dies.",
                "Evil Santa (and other bosses) no longer stand idle: they pace their lair, and look further afield when they're boxed in.",
                "The giant present vault is overpowered now: every rack slot holds a fully loaded vault gun, six vault-grade items on the shelves, and its three crates are packed with vault loot (half the time a Vault Keycard or Military Radio too).",
                "Manda's bed is back.",
                "Fixed the steering wheel turning the opposite way to the cart (it now turns with your hands, 90 degrees each way).",
                "Fixed duplicating items: a keycard used on a vault dropped a copy for the other players first, and holding an item while reaching into a crate or air drop could set it off and knock the item loose - crates now open with an empty hand.",
            }),
            ("2.56", "Shake-to-open presents, better DLC store", new[] {
                "CHRISTMAS PRESENTS are small now: over Christmas, crates often hold a little wrapped present (35% of crates, 60% on the North Pole). Pick it up and SHAKE it - five hard shakes and it bursts open, dropping the prize right there (it rattles as you shake it). Same odds as before: mostly supplies, sometimes a gun or armour, rarely a top gun, a keycard or a jackpot... and now and then a live grenade. The big floor presents are gone.",
                "Evil Santa drops SANTA'S PRESENT (a bigger gold-ribboned one) - shake it open for a guaranteed North Pole Keycard and vault-grade loot.",
                "DLC STORE redesigned: each pack card has its colour band, a showcase rifle that turns and changes into each of the pack's skins, a framed print of every skin pattern with its name, little models of any items with how many you get, and a big FREE / GET button.",
                "2 new skins: Graffiti (Street Pack) and Rust Bucket (Survivor Pack).",
                "Tester spawn board: Christmas Present and Santa's Present are real items in OTHER now.",
            }),
            ("2.55", "Easier driving, rarer Santa, boss outfits", new[] {
                "Golf carts are much easier to drive: the LEFT STICK steers (left / right) as well as the wheel; the wheel is easier to grab and needs less turning (full lock at 90 degrees); steering is sharp when slow and gentle at top speed (no more twitching); the tyres grip harder (no sliding) and it's much harder to tip over.",
                "Evil Santa is rare now: he holds Santa's house on 1 North Pole raid in 20 (5%).",
                "When Evil Santa dies he drops a Christmas present with a guaranteed NORTH POLE KEYCARD inside (plus vault-grade loot). Everyone in the raid sees the same present.",
                "Every boss has their own look again (they were all snowmen over Christmas): Doctor Kessler's lab coat with a red cross and name badge, the Harbormaster's orange coat with yellow reflective bands, the Warden's camo and leafy ghillie collar, the Banker's black suit with a gold watch chain, the Courier's pinstripes and red carnation, and Evil Santa's red suit.",
            }),
            ("2.54", "Own server switched on", new[] {
                "Multiplayer now goes through our own game server (77.42.36.49:27015, no player limit) instead of Photon. Everyone needs this version to play together.",
            }),
            ("2.53", "Our own game server", new[] {
                "Multiplayer can now run through our OWN game server (idrk.dev) instead of Photon - no 100-player limit. It keeps parties, invites, raid queues, the Market and reconnecting exactly as before; invites now reach a player wherever they are (bunker, Market or raid).",
                "Movement, voice and raiders go over UDP (fast); shots, hits, items and trades over TCP (never lost, always in order). If UDP is blocked it all goes over TCP.",
                "Switched off until the server is installed: in Unity, open Resources/Rago/server.txt and remove the # in front of idrk.dev:27015. Photon is used while it's off.",
                "Menu > RECENT shows the server connection (UDP ok / blocked, server ping, traffic).",
            }),
            ("2.52", "The North Pole", new[] {
                "NEW MAP - THE NORTH POLE: Santa's village overrun by evil elves. Snow falls the whole raid; candy-cane posts line the avenue; a giant Christmas tree in the square; elf workshops, the Toy Factory, Santa's house, igloos, snowmen and piles of presents - with snowfields and forest all round. Pick it on the raid map (the ice floe, bottom left).",
                "EVIL ELVES: every raider on the North Pole is an evil elf - pointy ears, angry red eyes, striped stockings and a hat with a bell (heavies wear red).",
                "BOSS - EVIL SANTA: holds Santa's house at the north end of the avenue, with a heavy vest and a big gun. Drops a Vault Keycard like the other bosses.",
                "THE GIANT PRESENT: a vault wrapped in red paper and gold ribbon in the middle of the village. Only the NORTH POLE KEYCARD opens it (a big red-and-green card). Inside: two vault guns and a full shelf of the best loot - and the first time you step in, NEW SKINS UNLOCKED: Aurora, Gift Wrap, Gingerbread and North Star at your paint booth.",
                "The North Pole Keycard ONLY comes out of Christmas presents (7% on the North Pole, 2% elsewhere). Presents are very common now: 32 on the North Pole, 20 on the other maps (12 underground).",
                "Tester menu: Evil Santa in the NPC spawner.",
            }),
            ("2.51", "Batting practice", new[] {
                "Hit a grenade with the baseball bat (or any blunt weapon - crowbar, sledgehammer, pan, candy cane) and it goes flying along your swing - the faster you swing, the further it goes. Toss one up and swing!",
                "An impact grenade doesn't go off from the bat hit itself - it goes off on whatever it hits next.",
                "The Street, Survivor and Collector's packs are skins only now (their melee weapons are found around the maps and sold by Mara). The Holiday Pack still comes with the Candy Cane and impact grenades.",
            }),
            ("2.50", "Melee around the maps, free DLC", new[] {
                "Melee weapons lie around every raid map now (6 a raid, 4 in the Underground): bats, crowbars and frying pans are common, machetes less so, fire axes and sledgehammers rare. They're still in loot crates and Mara still sells them.",
                "Every DLC STORE pack is FREE for now - press GET.",
                "The Candy Cane is Holiday Pack only (no longer in loot or at Mara's).",
            }),
            ("2.49", "Impact grenades, melee, skins, DLC store", new[] {
                "RGN IMPACT GRENADE: pull the pin and throw - it goes off the moment it hits something (or 8 s after the pin). Mara sells them; found in loot too.",
                "MELEE: Baseball Bat, Crowbar, Machete, Sledgehammer, Fire Axe, Frying Pan and (at Christmas) a Candy Cane club. Bats, hammers, crowbars and pans hit with a thud and a shove; heavy tools smash padlocks fast. Mara sells them; found in loot too.",
                "DLC STORE (Market, west wall): packs bought with in-game cash - Street, Survivor, Collector's, and the Holiday Pack over Christmas. A pack unlocks its gun skins at your paint booth and sends its items to the Armory floor.",
                "15 NEW SKINS: Candy Cane, Ugly Sweater, Frostbite, Neon Circuit, Hazard, Dragon Scale, Galaxy, Damascus Steel, Bone, Deep Ocean, Cherry Blossom (DLC packs), plus 4 event skins. Skins you don't own don't show at the paint booth until you buy or earn them.",
                "The Market's stalls got rugs and potted plants; at Christmas the traders wear Santa hats, garlands of lights hang along the stalls and there's a wreath over the DLC store.",
            }),
            ("2.48", "Spawn board", new[] {
                "Tester menu: SPAWN ITEM... opens a spawn board - little 3D models of the items, 12 a page, each with its name underneath (coloured by rarity), in GUNS / ARMOR / ATTACH / SIGHTS / OTHER tabs. Press one and point where it goes. The present and golf cart are in OTHER. < MENU goes back, the menu button closes it.",
            }),
            ("2.47", "Spawn menu: present and golf cart", new[] {
                "Tester menu: the Christmas present and the golf cart are now in the OTHER spawn list (first two entries). Pick one, press SPAWN, point where it goes - a see-through green box shows the spot - and pull the trigger, just like spawning an item.",
                "The separate SPAWN GOLF CART and SPAWN PRESENT buttons are gone.",
            }),
            ("2.46", "Presents from the menu", new[] {
                "Tester menu: SPAWN PRESENT puts a Christmas present on the floor in front of you - anywhere, raids, the Market or your bunker. Each one has a fresh random roll inside, so it could be a top gun, a keycard... or a live grenade.",
            }),
            ("2.45", "Quiet presents", new[] {
                "Opening a present no longer puts text on screen - you find out what's inside by looking (or by running from the grenade).",
            }),
            ("2.44", "Presents", new[] {
                "CHRISTMAS PRESENTS: wrapped presents with a ribbon and bow are hidden round every raid map (10 per raid, 6 in the Underground), spread out and in different spots each raid. Grab the lid to open one.",
                "What's inside is a gamble, and the better it is the rarer it is: mostly ammo, meds and supplies; sometimes meds and a grenade, a loaded gun or armour; rarely a really good gun with a sight; very rarely a Vault Keycard or a Military Radio; and once in a long while a JACKPOT, a whole vault's worth of loot.",
                "About 1 in 7 is a prank: a LIVE GRENADE pops out and goes off 2.5 seconds later. Run!",
                "Everyone in the raid sees the same thing in each present, and once someone opens one it's open for everybody.",
            }),
            ("2.43", "Christmas", new[] {
                "CHRISTMAS (15 November - 10 January): the ground on Dockyard, Pinewood and Old Town is covered in snow, and snow falls during raids instead of rain.",
                "Christmas trees with baubles, glowing fairy lights, a star and presents underneath: in your bunker's main hall, in the Market, and beside the extracts on every raid map (Underground too). The ones in raids have a warm light so you can spot them at night.",
                "The raiders and bosses are snowmen: top hat, carrot nose, coal eyes and buttons, scarf and twig arms. They still fight and fall the same way. Bosses get a taller hat with a gold band and a green scarf.",
            }),
            ("2.42", "Clothes that fit, no dog bed", new[] {
                "Clothes fit properly: a shirt is now one snug piece over the chest and round the waist (it was two puffy boxes with a gap between them and the chest plate showing through), and trousers sit right at the waist.",
                "Manda's bed is gone - she curls up on the floor in her corner (her name sign is still there).",
            }),
            ("2.41", "Clothes, knockdowns, a better-fitting body", new[] {
                "WARDROBE (bunker, west wall of the main hall): dress your operator - shirt (T-shirt, long sleeve, hoodie, jacket, tank top), trousers (cargo, jeans, shorts), hat (cap, beanie, bandana) and face (sunglasses, neck gaiter, gas mask), each in one of 15 colours. The mannequin next to the board wears it as you choose; you wear it too, under your armour, and other players see it.",
                "KNOCKED DOWN: getting run over by a golf cart or caught by a grenade blast (and surviving) throws you back as a ragdoll - your view rides along in its head - and you get back up where you land after a couple of seconds.",
                "Crouching no longer brings your legs up in front of you: you kneel, so you can see the floor (and the loot) in front of you.",
                "Your body and arms sit properly under your head - looking down used to swing your chest and shoulders back behind you.",
                "Other players keep their own colour when they come back from a death (it used to change to a random one).",
            }),
            ("2.40", "Loot boxes don't spawn inside furniture", new[] {
                "Fixed loot boxes spawning inside tables (and shelves, beds, other boxes...). Each box is now checked at its real size and angle against everything solid before it's placed; if it doesn't fit, a smaller case goes there instead, or nothing.",
            }),
            ("2.39", "Faster carts, parked out in the map", new[] {
                "Golf carts are faster: top speed ~54 km/h (was ~32), quicker to pick up speed, reverse a bit faster too.",
                "Carts are no longer parked next to where players spawn. Up to 8 are parked out in each map instead - near the buildings and camps where raiders hang about, at least 90 m from any spawn, on clear flat ground.",
                "No golf carts in the Underground at all (the tester spawn button won't make one there either).",
            }),
            ("2.38", "Carts really stay put, hands on the wheel, Manda, name tags", new[] {
                "Golf carts: fixed them still driving and turning on their own - your hands and your body are separate physics objects (and so is whatever you're holding), and they were shoving the cart. None of that pushes it any more.",
                "A cart with someone in it stays completely still until the driver presses the gas.",
                "Hands on the wheel: while you hold the steering wheel your hands are drawn gripping its rim, and stay there as you turn it.",
                "The bunker pug is called MANDA - her name is on a little wooden sign at the front of her bed.",
                "No more 'Script Updating Consent' popup in Unity: the golf cart code used old physics names (velocity / drag) - it now uses the same helpers as the rest of the game.",
                "Name tags: a player's name tag now sits flat on their chest (it floated out in front of the collar), and on the front of their vest when they wear one (it used to hover above it).",
            }),
            ("2.37", "Carts stay put", new[] {
                "Fixed golf carts driving off on their own: your body, hands and holstered gear sit inside the cart, and physics kept shoving the cart out from under you. The people in a cart (you and other players in its seats) no longer push it - and can still be run over once they're out.",
                "Off the gas and slow, a cart now holds still (even on a hill) instead of creeping; at speed it still rolls to a stop.",
            }),
            ("2.36", "Drive with the steering wheel", new[] {
                "Driving a golf cart in VR is now hands-on: grab the steering wheel (one or both hands) and turn it to steer - the wheel turns with your hands. RIGHT TRIGGER is the gas, LEFT TRIGGER is the brake; keep holding it once you've stopped to back up.",
                "Let go of the wheel and you can't accelerate - the cart rolls to a stop (and the wheel straightens up).",
                "Desktop: W gas, S brake (then reverse once stopped), A / D steer.",
            }),
            ("2.35", "Cart doors fit", new[] {
                "The golf cart doors now fill the whole side opening - from the front bodywork to the rear, level with the top, flush with the side - instead of a small panel floating in the middle. The handle is at the back edge.",
            }),
            ("2.34", "Cart doors and passengers", new[] {
                "Golf carts have doors: grab the handle on the driver's door (left) to drive, or the passenger door (right) to ride along. The door swings open as you get in and out, and you step out on your own side.",
                "Passengers ride in the passenger seat (no controls) - in multiplayer you go wherever your friend drives. If someone's already driving, grabbing the driver's door puts you in the passenger seat.",
                "New steering wheel: a proper rim with spokes, angled up towards you along its column (it was a solid disc lying almost flat). You can still grab it to drive.",
            }),
            ("2.33", "Golf carts and volume settings", new[] {
                "GOLF CARTS: one is parked next to every spawn on Dockyard, Pinewood and Old Town. Grab the steering wheel to get in (desktop: F). Left stick = gas / reverse and steering (desktop W A S D), A or X to get out (desktop F / Space). Your view still turns with the right stick.",
                "Run people over: hitting a raider or another player at speed hurts them - the faster, the harder (up to 90 damage). The kill feed says 'a golf cart (run over)'.",
                "TESTER menu: SPAWN GOLF CART puts one in front of you anywhere (raids, the Market, your bunker). In multiplayer everyone sees it, and everyone sees you driving.",
                "SETTINGS: separate volumes for Master, Effects (guns, footsteps, raiders...), Weather (rain, wind, background air) and Voices (other players' voice chat). There's no music in the game any more, so there's nothing to turn down there.",
            }),
            ("2.32", "Ping fix: messages no longer queue up", new[] {
                "Fixed the 1+ second ping with 3 players: shots, hits, names, pings and items were going through Photon's 'reliable data', which is made for sending big files in slices and rations what goes out - with a few players everything queued behind everything else. They now go as Photon RPCs (one copy for everyone), built for exactly this. Only big messages still use the old way.",
                "If those RPCs don't work on someone's setup, the game notices within ~10 s and switches back by itself. The network report shows +RPC when it's on.",
                "PLAYER LIMIT REACHED: Photon's 'max players' error now says so plainly on the raid terminal.",
                "Taking your headset off (asleep) while in the bunker now lets go of Photon, so a sleeping headset no longer holds one of your plan's player slots. It reconnects when you put it back on.",
            }),
            ("2.31", "Fewer movement updates", new[] {
                "Your movement is sent 12 times a second (10 with 4+ other players or a slow connection, 6 if it's really backing up) instead of 15 - less traffic for Photon and everyone's headset.",
            }),
            ("2.30", "Voice no longer clogs multiplayer", new[] {
                "Fixed the lag getting worse with friends since 2.27: voice packets had become too big for the fast lane, so whenever anyone talked their voice went the slow way - a separate copy to every player, ~16 times a second - and everything else queued behind it. Voice now goes in smaller pieces that fit.",
                "Raider updates in big raids are split into small groups for the same reason.",
                "The network report (menu > RECENT) shows 'too big: N' if anything ever falls back to the slow way again.",
            }),
            ("2.29", "FPS readout, deliveries to the Armory, pistols down again", new[] {
                "Your watch shows FPS next to the NET line. Menu > RECENT shows FPS, the worst frame in the last 5 s, how often memory clean-up runs, and how much of each frame goes on other players - send a screenshot if the game slows down with friends.",
                "Fewer hitches with other players around: voice no longer makes new memory every frame, other players' hidden name text isn't rebuilt every frame, and each player's Photon id is read once instead of on every packet.",
                "Everything you buy or trade for (and anything you take off your shop) is delivered to your bunker, on the floor in the middle of the Armory, and stays there until you pick it up.",
                "Nothing despawns in your bunker: dropped magazines stay put too (a loose mag still turns back into rounds in your reserve when you leave the bunker).",
                "Pistols toned down again: less damage (about -30% from the original), slower to fire, wider spread and more kick; damage drops off from 10 m (under half by 45 m). Revolvers a little weaker too.",
            }),
            ("2.28", "Multiplayer rebuilt", new[] {
                "The whole Photon connection layer is rebuilt from scratch, smaller and much simpler. Everyone must be on 2.28.",
                "One Photon connection per player, ever: the lobby while you're idle in your bunker (so invites reach you), or your party / the Market / a raid. It used to juggle up to four at once.",
                "Invites: invite a callsign from the menu in your bunker and it reaches them through the lobby; they accept from their menu. In a raid or the Market you can invite people who are there with you; anyone else gets it once you're back in your bunker.",
                "The party leader queues and everyone follows into the same raid, side by side at one spawn.",
                "NETWORK REPORT: menu > RECENT shows your connection, everyone you can see (and when you last heard them), FAST/SLOW, and a log of what happened - joins, failures with Photon's exact reason, players coming and going. Screenshot it when something goes wrong.",
            }),
            ("2.27", "Multiplayer safety net", new[] {
                "If other players' movement stops arriving on the fast lane, the game switches back to the reliable channel by itself after 10 s (your watch shows SLOW) instead of leaving people invisible or frozen.",
                "Fast-lane packets are smaller, well under Photon's size limit.",
                "Players idling in their bunker now use one Photon connection instead of two, so the player cap fills up half as fast (idle bunker players no longer show as online; invites still work).",
            }),
            ("2.26", "Die and your armour is gone", new[] {
                "Dying in a raid loses the armour you wore in, along with everything else - it drops on your body for whoever finds it. No free starter helmet or vest after a death either.",
            }),
            ("2.25", "Armour you wore in comes home again", new[] {
                "Back to how it was: the armour you wore into a raid comes home if you die. Only the free starter helmet and vest stay gone (they no longer reappear on your stands after a death).",
            }),
            ("2.24", "Bunker floor, dupe fixes", new[] {
                "The Lost & Found bin is gone. Anything you put down in your bunker - floor, tables, shelf tops - stays exactly where you left it, even after raids and restarts.",
                "If your racks are full when the game loads, the extra items wait on the floor by the west wall of the main hall.",
                "Fixed guns duplicating when left in the paint booth (and the same dupe with items left in Exchange bins, market stalls and trader trays).",
                "Dying now loses the armour you wore in too, and no free helmet and vest turn up on your stands after a death.",
                "Queueing for a raid no longer moves things around your bunker (it only saves).",
            }),
            ("2.23", "The screen lives in the bunker", new[] {
                "The YES, IT'S VIBE CODED screen is in your bunker now (south wall, right of the lift); the Market one is gone.",
            }),
            ("2.22", "Just the one screen", new[] {
                "Only one YES, IT'S VIBE CODED screen now: the big one on the Market's south wall (the bunker one is gone).",
            }),
            ("2.21", "Yes, it's vibe coded", new[] {
                "The CLAUDE IS THE GOAT posters are gone from the bunker and the Market.",
                "New wall screens in their place: YES, IT'S VIBE CODED (bunker, right of the lift; Market, south wall).",
            }),
            ("2.20", "Pistols toned down, ammo type in the tester menu", new[] {
                "Pistols hit about 20% softer, can't be spammed as fast, spray wider and kick a bit harder. Machine pistols (Hornet MP) got the biggest cut.",
                "Pistol and revolver rounds lose damage quickly past 12 m (half damage by 50 m), so rifles win at range.",
                "Revolvers hit a little softer (-12%) and are a bit less accurate.",
                "TESTER menu: pick STANDARD, AP or INCENDIARY with < > before pressing +AMMO.",
            }),
            ("2.19", "Big multiplayer update", new[] {
                "Your movement goes out the moment it's taken (it used to wait up to 66 ms for the next send tick), and shots, hits and deaths go out the same frame instead of with the next batch.",
                "Other players are drawn closer to real time: the smoothing buffer now sizes itself to how steady your connection really is (about 80 ms on a good line), and only grows when updates arrive unevenly.",
                "Raiders move more smoothly for everyone (their positions go out twice as often on the fast lane - it's one copy for everybody, so it's cheap).",
                "Your WATCH shows the connection while you're online: NET ping in ms, players in the session, and FAST / SLOW for the fast lane - green is good, yellow OK, red bad. Tell me what it says when something feels laggy.",
                "Flashbangs white you out now (they used to black you out).",
            }),
            ("2.18", "Paint booth and free attachment placement", new[] {
                "PAINT BOOTH on the Armory workbench in your bunker: put a gun on the red mat, pick a skin on the board (< >) and press PAINT. The spray can rattles, then sweeps up and down the gun in a cloud of paint and it comes out in its new colours. STRIP puts the factory finish back.",
                "Skins modelled on the real thing: Flat Dark Earth and Crimson Cerakote coatings, M81 Woodland, Multi-Terrain, Arctic Splinter, Urban Digital and Tiger Stripe camouflage, Gold Rush and a glossy Carbon Fibre twill. Matte paint with edges worn to bare metal, sized for a gun, and painted ON the gun so the pattern stays put when you move it. Your skin stays on the gun (stash, raids, trades) and other players see it.",
                "ATTACHMENTS go where you put them: optics, lights, lasers and foregrips stay wherever along the rail you let go of them, and a light or laser goes on whichever side of the handguard you hold it to. To adjust one, grab it, slide it and let go. Other players see them where you put them.",
                "Quest builds: input handling is now the old Input Manager only (Android refuses 'Both' and cancelled the build).",
            }),
            ("2.17", "Smoother multiplayer, fewer crashes", new[] {
                "MULTIPLAYER FAST LANE: movement, voice and raider positions now go out ONCE as an unreliable Photon RPC and Photon's server hands them to everyone. Before, each player sent a separate reliable copy to every other player (15 copies each at 16 players) and one lost packet held up everything behind it. Shots, hits, items and trades still use the reliable channel. Your console says '[Rago] fast lane on' when it's working.",
                "MULTIPLAYER with 3+ players: we were sending your movement faster than Photon can deliver it (it sends about 16 times a second), so it queued up and arrived late - and every extra player made the queue longer. Now it's 15 a second and never more than Photon can carry.",
                "Players far from you get your movement only a few times a second (8 at mid range, 4 far away) - they can barely see you, and every copy is a separate message. Near players and your squad still get every update. Far players are drawn with a slightly longer smoothing buffer so they still move smoothly.",
                "Voice now goes only to the players who can hear you (within 35 m in raids, or your squad on the radio) - it used to go to everyone once anyone was near, and it's the biggest thing we send. It also pauses sooner when the line backs up.",
                "Raider updates are sent a little less often and only for raiders someone is near.",
                "PARTIES / JOINING: each player now uses ONE Photon connection in raids and the Market (it was two or three). Every connection counts towards the Photon plan's player limit, so about 10 people could fill it and parties and joins then failed. The players-online count shows the last number heard while you're in a raid or the Market.",
                "CRASHES: the black market screens leaked memory every couple of seconds while you were in the Market (each redraw left its old buttons' meshes behind) - fixed, and the traders' price lists no longer leak when you page through them.",
                "The watch body map only opens when you tap the watch while it's turned towards you (reaching across for your left hip pouch used to pop it up).",
                "No more false 'map is different' warnings from a player whose map was still loading.",
                "CRASHES: the bunker music is gone - building it used about 250 MB of memory at once, too much for a Quest.",
            }),
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
                "New posters on the bunker and Market walls.",
                "FIXED: Logging Road (Pinewood) had no exfil laptop. Every extract now sits on the real ground, and its laptop is always put down beside the zone even on a slope or a cluttered spot.",
                "EXFIL: just touch the CALL button on the extract laptop with either hand to call your exfil - no trigger needed.",
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
