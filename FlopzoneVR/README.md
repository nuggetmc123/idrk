# Flopzone

A VR ragdoll shooter for Meta Quest, made in Unity. Grab guns off your hips, reload by
pulling mags from your waist, and send other players flopping across 10 maps in Team
Deathmatch, Free For All, 1v1, 2v2, 3v3 and Capture the Flag. Meet in the hub, party up
with friends, and go.

## Open it

1. Install **Unity 6 (6000.0 LTS)** from Unity Hub, with the **Android Build Support** module
   (tick *OpenJDK* and *Android SDK & NDK Tools* too).
2. Extract `Flopzone.zip`. Unity Hub → **Add** → **Add project from disk** → pick the extracted
   folder (the one with `Assets`, `Packages` and `ProjectSettings` directly inside).
3. Open it. **Everything imports and sets itself up** (and again after updates):
   - makes the game scene and its assets, adds the scene to the build and opens it
   - sets the Quest/Android player settings (IL2CPP, ARM64, Android 12L+, internet permission)
   - imports the VR packages (XR Plug-in Management + OpenXR) and turns on OpenXR with
     Meta Quest support and the Touch controller profile

Then press Play (keyboard and mouse work without a headset).

## The Flopzone menu

At the top of Unity, next to Window and Help:

| Menu item | What it does |
| --- | --- |
| **Set Up Multiplayer** | Like Nullbound: 1) get a free Photon App ID, 2) paste it in, 3) import Photon Fusion 2 from the Asset Store. Flopzone switches online play on and fills in your App ID by itself. Until then the game plays offline with bots. Below that: an optional account server (names unique everywhere, friends lists, saved reports, bans) and moderation tools. |
| **Set Up VR (Quest)** | Imports XR Plug-in Management + OpenXR (if they aren't in yet) and switches on Quest support and PC VR (so Quest Link works in Play mode). |
| **Build for Quest (APK)** | Switches to Android and builds `Builds/Flopzone.apk`. |

## Playing

**First launch:** a "Welcome to Flopzone — Loading to hub..." popup, then the tutorial:
pick a name (must be unique; slurs and swearing are refused), then learn to grab, shoot,
drop the mag, reload, jump, sprint, crouch and open the menu. After that, the hub.

**Hub:** a forward operating base. Board the lobby helicopter to play, try all 22 guns in the armory, shoot targets on the live-fire
range, play Flappy Flop on the arcade machines, change your guns at the loadout station, and check
out the helipad, barracks, motor pool and watchtowers.

**The lobby helicopter:** a big twin-rotor helicopter stands on the landing pad at the north end
of the base, where you'd expect a deployment centre. Walk up its ramp and you're inside. The ramp
closes behind you, and if you lead a party, everyone in it is pulled in too. Inside there are six
loadout stations, two arcade machines, and the lobby screen at the front. Only the party leader (or
a solo player) can use it:

- **Public:** anyone can join. The top left of your view says **FINDING MATCH** and counts up to
  1:00, with how many players have joined. If the match isn't full after a minute, it starts with
  whoever joined and bots fill the empty spots (offline, the leader can also press *Start now with bots*).
- **Private:** just your party, no strangers. You go in straight away and bots fill the empty spots.

When the match starts, you deploy onto the map. Outside, by the ramp, there are four more arcade
machines and a loadout station.

**Players** are white, floppy mannequins: a tall rounded head with dot eyes and a smile, a long
body, thin arms with fingers, and flat feet. They're all white; your team shows as a coloured band
on each arm. When you're killed you go full ragdoll.

**The Outfitter** (the striped booth on the west side of the parade ground) is where you dress
up. Walk up to the kiosk and pick:

- **Hats (19):** baseball cap, backwards cap, beanie, cowboy hat, top hat, party hat, Viking helmet,
  crown, hard hat, chef hat, halo, propeller cap, headphones, army helmet, pirate hat, wizard hat,
  bucket hat, traffic cone, Santa hat
- **Tops (10):** T-shirt, hoodie, tank top, suit & tie, camo jacket, Hawaiian shirt, hi-vis vest,
  sports jersey, puffer jacket, lab coat
- **Pants (5):** jeans, shorts, camo, track pants, cargo pants
- **Shoes (4):** sneakers, boots, combat boots, high-tops
- **Gloves (7):** fingerless, boxing gloves, work gloves, rubber gloves, cartoon gloves, tactical, mittens

Caps, beanies, bucket hats, the party hat, the propeller cap, headphones and the wizard hat come in
12 colours, and so do the T-shirt, hoodie, tank top, Hawaiian shirt, jersey and puffer jacket.
A mannequin in the booth wears your outfit in a T-pose, so you can see it. **Random** and
**Plain** buttons are there too. Your gloves show on your own hands, everyone else sees your
outfit, and it stays on your ragdoll when you flop. Bots dress up at random.

### Controls (Quest)

| | |
| --- | --- |
| Move | Left stick |
| Sprint | Click the left stick |
| Turn | Right stick (snap or smooth) |
| Crouch | Click the right stick |
| Jump | A |
| Grab a gun | Reach to your hip, hold Grip (left hip: primary, right hip: sidearm) |
| Shoot | Trigger |
| Drop the mag | B, or grab the mag out of the gun with your other hand |
| Reload | Put your empty hand at your waist and **hold Trigger** to pull a mag, push it into the gun |
| Menu | ≡ button on the left controller |

Jump, Unload Mag, Sprint and Crouch can be rebound in **Menu → Controls**, with
**Reset to default**. Volume (master, music, sound effects), turning, and toggle/hold
options are in **Menu → Settings**.

**Keyboard testing in the editor** (no headset): WASD move, click to look, left-click
shoot, E / Q grab right / left, R reload, F drop mag, Space jump, Shift sprint, C crouch,
Tab menu.

### The menu (≡)

Play · Loadout (pick your primary and sidearm) · Friends (add by name or player ID,
invite to party) · Party (up to 6, leader takes everyone) · Chat (keyboard + quick chat) ·
**Report** (pick a player and a reason) · Settings · Controls · Profile (your name and
**player ID**) · **MOD** (moderators only).

### Game modes

| Mode | Players | Win |
| --- | --- | --- |
| Team Deathmatch | 5 v 5 | first team to 40 kills |
| Free For All | 8 | first to 20 kills |
| 1v1 | 1 v 1 | first to 10 |
| 2v2 | 2 v 2 | first to 15 |
| 3v3 | 3 v 3 | first to 20 |
| Capture the Flag | 5 v 5 | 3 captures |

Parties stay on one team. A full party of 6 that picks 3v3 is split 3 and 3.
Offline, empty seats are filled with bots.

### Maps

Warehouse · Rooftops · Dune Outpost · Frostbite · Jungle Ruins · Neon City · Shipyard ·
Foundry · Castle Keep · Moon Base (low gravity) — plus the Hub and the Training Range.

1v1 and 2v2 play on the normal, tight maps. Every other mode (Team Deathmatch, Free For All,
3v3, Capture the Flag) plays on a bigger version of the same maps: 35% wider, with extra cover.
The spawns, flags and barrier move out with them.

Every map has its own painted 360° sky (clear day, sunset, night with stars and moon, overcast,
desert haze, space), textured surfaces (concrete, metal, brick, sand, snow, grass and more, with
bump detail), a reflection snapshot for shiny metal, and a glowing energy barrier around the edge
that nobody can get past.

### Guns (22)

- **Pistols:** Flop 9, Duck Deagle, Buzz Mini, Revolt Revolver, Squeak .22, Twin Hammer
- **Shotguns:** Boomstick, Sawed Slam, Auto Thunder, Drum Brute, Slug Lord
- **Snipers** (working scopes): Longshot, Whisper, Elephant Gun, Marksman DMR, Needle
- **Assault rifles:** Rattle AR, Bulldog, Viper Carbine, Burst Bravo, Heavy HMR, Spitfire

Every gun has muzzle flash, tracer, shell ejection, recoil, haptics, its own sound and an
ammo counter. Hits spray blood and leave splats; kills turn players into physics
ragdolls you can keep shooting around.

## Moderation

- **Auto-mod:** in chat, "fuck", "shit" and "damn" are fine; slurs and other bad words
  mute the sender for 15 minutes. Names allow no swearing at all.
- **Player IDs:** every player has one (Menu → Profile).
- **Moderators:** put your player ID in `Assets/Flopzone/Resources/moderation.json` under
  `"admins"`. You get a **MOD** tab in the game menu: kick, mute and ban people in your room.
- **Bans everywhere:** add the player's ID to `"bans"` in the same file and raise `"version"` by one,
  for example `{ "id": "FZAB12CD", "reason": "slurs", "until": "" }` (`until` empty = forever).
  Headsets download the newest file from GitHub (the `main` branch) each time the game starts, so no new APK is needed.
- **Reports:** Menu → Report → player → reason. Moderators in the room are told right away; with the
  optional account server they're also saved (see [`Server/README.md`](Server/README.md)).

## How it's built

There are no imported art or sound files: every model (guns, players, maps) is built
from shapes in code, and every sound effect and both music tracks are synthesised when
the game starts. Packages: Unity's XR Plug-in Management and OpenXR, plus Photon Fusion 2
for online play (each hub and match is a Photon room; the room's master client runs the rules).

```
Assets/Flopzone/
  Editor/      auto-setup on load, the Flopzone menu, APK builds
  Scripts/
    Core/      JSON, word filter, settings, materials, sound synthesis, audio
    Input/     headset + controllers (Unity XR input), keyboard fallback
    Player/    rig + movement, hands, avatars, ragdolls, other players
    Weapons/   the 22 guns, gun models, mags, effects
    World/     the 10 maps, the hub, portals, targets
    Net/       Photon rooms + room rules, session state, offline mode with bots, account server link
    Game/      game flow, tutorial, bots, capture the flag
    UI/        VR UI kit, keyboard, menu, HUD
Server/        the optional account server, a Cloudflare Worker (see its README)
Assets/Flopzone/Setup/   switches Photon on/off by itself (compiles even if the game has errors)
```
