# Nullbound

A multiplayer VR fighting game for Quest (Unity 6, Photon Fusion 2 Shared Mode). Fight with your real fists:
punch, block, dodge, launch people through walls, and use each fighter's special. 47 fighters to collect from
crates, a big arena, AI bots that learn, voice chat and moderators.

## Open it

1. Unzip, then **Unity Hub > Add > Add project from disk** and pick the `Nullbound` folder (Unity 6000.0.40f1).
2. Wait for it to import. It adds the text resources and **builds the world by itself**, then says it's ready.
3. Press **Play**. You start in the **main menu**.

Menu (top bar) **Nullbound**:

| Item | What it does |
|---|---|
| Set Up Multiplayer | Get a free Photon App ID, paste it in, import Photon Fusion 2. Everything else is automatic. |
| Set Up VR (Quest) | Installs OpenXR and turns on the Quest settings. |
| Build for Quest (APK) | Checks everything and builds an APK. |
| Build Prototype World | Rebuilds the arena, the main menu room, prefabs and rig. |
| Check Multiplayer (Photon Fusion) | Says whether online play is on. |

Without Photon Fusion the game still runs (main menu and Training Grounds vs bots).

## Main menu

You stand in a round room with three big panels (point with a controller and pull the trigger, or poke):

- **First time:** a keyboard pops up and you **pick your name** before you can play. Names have to be clean (rude
  words get blocked) and **nobody else can have the same one** (see *Unique names* below). It's locked after that;
  only a moderator can make you change it.
- **Middle - PLAY:** Quick Match, Private Lobby, Join a Code, **Training Grounds** (you vs bots and dummies, works
  offline). Also your name, **Player ID**, coins and (for mods) the **Mod Menu** button.
- **Left - FIGHTERS:** all 47. Press one you own to fight as it; locked ones show as a dark shape. A 3D preview
  stands next to the panel.
- **Right - CRATES:** spend coins to unlock fighters. The crate shakes, bursts, and the new fighter appears in a
  beam of its rarity's colour.

The **menu button** (three lines) opens the wrist menu anywhere: your name and ID on top, **Main Menu** to leave a
match, fighters, settings (turning, comfort vignette, room scale, voice: open mic / push-to-talk / muted, volume)
and **Mod Menu** in Settings for moderators.

## Coins and crates

- **Earn coins:** landing hits (bots pay half), **KOs** (30 for a player, 10 for a bot) and **streaks** (+20 from 3
  in a row). New players start with 300 (two crates).
- **Fighter Crate (150):** Common 55%, Rare 28%, Epic 12%, Legendary 4.5%, Mythic 0.5%.
- **Elite Crate (500):** Rare 50%, Epic 32%, Legendary 15%, Mythic 3%.
- Each crate gives a fighter you **don't have yet**. Own them all and you get coins back instead.
- **Fair, not pay-to-win:** there's no real money, and stats come from the body type (Normal, Slim, Bulky, Giant,
  Tiny, Robot, Beast, Spirit), so a Mythic looks amazing but isn't stronger than a Common with the same build.
- Coins and owned fighters are saved on the headset.

## Fighters

Each has its own model built from parts (body build, head, hair / horns / crown / halo / ears, cape / wings / tail /
jetpack / blades / orbiting orbs, glove style, colours) and one of 16 specials:
Ground Slam, Flash Step, Fireball (burns), Ice Shard (freezes), Lightning, Shockwave, Telekinesis, Shadow Strike,
Clones, Titan Quake, Uppercut, Whirlwind, Beam, Meteor, Heal, Barrier.

| # | Fighter | Rarity | Body | Special |
|---|---|---|---|---|
| 0 | **Brawler** | Common | Bulky | Ground Slam (GroundSlam) |
| 1 | **Speedster** | Common | Slim | Flash Step (FlashStep) |
| 2 | **Elementalist** | Epic | Normal | Fire Blast (Fireball) |
| 3 | **Psychic** | Epic | Slim | Telekinesis (Telekinesis) |
| 4 | **Shadow Fighter** | Legendary | Slim | Shadow Strike (ShadowStrike) |
| 5 | **Titan** | Legendary | Giant | Titan Quake (TitanQuake) |
| 6 | **Frostbite** | Rare | Normal | Ice Shard (IceShard) |
| 7 | **Voltage** | Rare | Slim | Thunderclap (Lightning) |
| 8 | **Inferno** | Epic | Bulky | Hellfire Meteor (Meteor) |
| 9 | **Mecha-7** | Rare | Robot | Laser Cannon (Beam) |
| 10 | **Wildclaw** | Common | Beast | Claw Frenzy (Whirlwind) |
| 11 | **Monk** | Common | Normal | Inner Peace (Heal) |
| 12 | **Bulwark** | Rare | Bulky | Iron Wall (Barrier) |
| 13 | **Rocket** | Common | Normal | Rocket Uppercut (Uppercut) |
| 14 | **Phantom** | Epic | Spirit | Haunting (Clones) |
| 15 | **Sandstorm** | Rare | Normal | Dust Devil (Whirlwind) |
| 16 | **Stormcaller** | Epic | Slim | Storm Strike (Lightning) |
| 17 | **Gearhead** | Common | Robot | Steam Blast (Shockwave) |
| 18 | **Viper** | Rare | Slim | Viper Strike (ShadowStrike) |
| 19 | **Colossus** | Epic | Giant | Colossal Stomp (TitanQuake) |
| 20 | **Nova** | Legendary | Normal | Supernova Beam (Beam) |
| 21 | **Glacier** | Epic | Bulky | Glacial Spike (IceShard) |
| 22 | **Kitsune** | Legendary | Beast | Fox Fire (Fireball) |
| 23 | **Golem** | Rare | Giant | Boulder Slam (GroundSlam) |
| 24 | **Pixie** | Common | Tiny | Pixie Dash (FlashStep) |
| 25 | **Samurai** | Rare | Normal | Blade Storm (Whirlwind) |
| 26 | **Ninja** | Common | Slim | Smoke Strike (ShadowStrike) |
| 27 | **Paladin** | Epic | Bulky | Holy Light (Heal) |
| 28 | **Gravity** | Legendary | Normal | Singularity (Telekinesis) |
| 29 | **Brute** | Common | Bulky | Brute Smash (GroundSlam) |
| 30 | **Blitz** | Common | Slim | Blitz Step (FlashStep) |
| 31 | **Reaper** | Mythic | Spirit | Soul Harvest (ShadowStrike) |
| 32 | **Dragonheart** | Mythic | Bulky | Dragon Meteor (Meteor) |
| 33 | **Cyber Ronin** | Legendary | Robot | Neon Slash (FlashStep) |
| 34 | **Tidecaller** | Epic | Normal | Tidal Wave (Shockwave) |
| 35 | **Bonecrusher** | Rare | Giant | Skull Cracker (Uppercut) |
| 36 | **Spark** | Common | Tiny | Zap (Lightning) |
| 37 | **Echo** | Rare | Slim | Echo Squad (Clones) |
| 38 | **Thunder Ox** | Rare | Beast | Stampede (GroundSlam) |
| 39 | **Mirage** | Epic | Slim | Desert Illusion (Clones) |
| 40 | **Iron Fist** | Rare | Normal | Iron Uppercut (Uppercut) |
| 41 | **Solar** | Legendary | Normal | Solar Flare (Beam) |
| 42 | **Lunar** | Legendary | Slim | Moon Shield (Barrier) |
| 43 | **Venom** | Epic | Beast | Toxic Burst (Shockwave) |
| 44 | **Juggernaut** | Epic | Giant | Unstoppable (Barrier) |
| 45 | **Zephyr** | Rare | Tiny | Cyclone (Whirlwind) |
| 46 | **Omega** | Mythic | Robot | Omega Strike (Meteor) |

Brawler and Speedster are free; the rest come from crates. Change names, colours, looks and specials in
`Scripts/Fighters/FighterInfo.cs`.

## How to fight

| Action | VR | Keyboard (no headset) |
|---|---|---|
| Punch | Really punch! Faster = harder | Left click (right fist), G (left fist) |
| Block | Both fists up in front of your face | Hold H |
| Move / turn | Left stick / right stick | W A S D / Q E |
| Jump | A | Space |
| Dodge dash | B (the way the stick points, or backwards) | Shift |
| Special | X | F |
| Menu | Menu button (three lines, left controller) | Tab / Esc |
| Talk (push-to-talk mode) | Hold the left stick in | Hold V |
| Recenter | Click the right stick | C |

Combos (4th hit launches), heavy hits, blocking with a guard meter (guard break stuns you), dodging (can't be hit
mid-dash), K.O. and respawn, streaks. Pillars, crates, walls, buildings, the car and the dojo's paper walls break.

## The arena: Training Grounds (now 90 m across)

- **Middle:** the sparring ring, with 8 breakable pillars round it.
- **North:** 7 training dummies (damage, hits, DPS).
- **North-east, Downtown:** three buildings made of breakable walls, a road, street lights and a car to throw
  people into.
- **North-west, Dojo:** a wooden dojo with paper walls you can smash through.
- **South-west, Plateau:** raised rock platforms with ramps (high ground!).
- **South:** a breakable wall; crate stacks around the floor; the KO board (east) and How to Fight board (west).

## Moderators

Every player has a **Player ID** (like `3F9A2-C01B7`), shown in the main menu and at the top of the wrist menu.
To make someone a mod, add their ID to `Assets/Nullbound/Resources/moderation.json` and **raise `version`**:

```json
{ "version": 2, "mods": [ { "name": "TheirName", "id": "3F9A2-C01B7" } ], "bans": [], "blockedNames": [], "pardons": [], "nameServer": "" }
```

Push it to GitHub: the game downloads the list when it starts, so it works on every headset without a new APK.
(Send the ID to Claude and it'll do this for you.)

**Mod Menu** (main menu, or wrist menu > Settings): everyone in your room. **Press a name to see their Player ID**
and headset code, then **Kick**, **Mute 15 min**, **Ban 1 day**, **Ban 7 days**, **Ban forever** (bans need two
presses) or **Force rename**. Commands are signed with the mod's secret key and checked by the player's own game, so
nobody can fake them. Banned / kicked players can't go online (Training still works).

**Mod powers:** **Invincible** (nothing hurts you), **Invisible** (nobody else can see or hit you, and bots ignore
you) and **every fighter unlocked**. Mods show a red MOD tag over their head.

To undo an in-game ban, add a pardon: `"pardons": [{ "id": "3F9A2-C01B7", "before": "2026-10-20 12:00" }]` (UTC).

## Unique names

No two players can have the same name once the free **name server** is running (it's in `Nullbound/Server`):

```sh
cd Nullbound/Server
npx wrangler login
npx wrangler deploy
```

Put the address it prints (like `https://nullbound-names.<you>.workers.dev`) in `moderation.json` as
`"nameServer"`, raise `version` and push. Each claim is signed with the player's secret key, so names can't be
stolen. **Until the server is set up, names are still checked for rude words and mods' names, but two people could
pick the same name.**

## Playing with people

- **Quick Match:** free-for-all rooms of up to 8; you join the fullest room with space.
- **Private lobby:** make one, friends type its 6-digit code in **Join a Code**. You can also join a friend's
  Quick Match room by its code.
- **Bots** (Training and private lobbies): + Add Bot (up to 5), Remove Bots, Easy / Normal / Hard. Bots can be
  any fighter, use their specials, block and dodge, and **learn** from the hits you land.
- **Voice chat:** open mic by default; push-to-talk or muted in Settings. Voices come from each player's head.

## Comfort

Snap or smooth turning, a comfort vignette (on by default) and room-scale walking, all in Settings.

## How it works (for changing things)

- `Scripts/Combat/Fighter.cs` (+ `Fighter.Specials.cs`, `Fighter.Safety.cs`): one networked fighter. The owner
  moves it, detects its punches and specials, and sends hits to the target's owner with an RPC.
- `Scripts/Fighters/FighterInfo.cs` / `FighterModel.cs`: the roster and the part-built models.
- `Scripts/Progress/Collection.cs`: coins, crates and unlocks.
- `Scripts/UI/MainMenu.cs`: the main menu room. `Scripts/Safety/*`: IDs, moderation, names, mod menu.
- `Scripts/Network/SessionManager.cs`: main menu / Quick Match / private lobbies / codes / training.
- `Editor/ArenaBuilder.cs`: builds everything from code. All sound and music is generated in code.

## Next steps

- 4 abilities per fighter + Ultimate mode; grabs and throws.
- More arenas (City Downtown, Volcano Arena, Sky Island); 1v1 duels and team fights.
- XP and levels, skins, victory poses, daily quests, achievements, leaderboards.
