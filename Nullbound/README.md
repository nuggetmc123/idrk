# Nullbound

A multiplayer VR fighting game for Quest (Unity 6, Photon Fusion 2 Shared Mode). Fight with your real fists:
punch, block, dodge, launch people through walls, and use each fighter's special.

This is the **first playable slice** (core combat first): one arena, two fighters, punching, blocking, dodging,
one special each, AI bots, Quick Match, private lobbies and codes. More fighters, abilities, arenas, ultimates and
progression get built on top once this feels right.

## Open it

1. Unzip, then **Unity Hub > Add > Add project from disk** and pick the `Nullbound` folder (Unity 6000.0.40f1).
2. Wait for it to import. It adds the text resources and **builds the arena by itself**, then says it's ready.
3. Press **Play**. You start in **Training** with a bot to fight.

Menu (top bar) **Nullbound**:

| Item | What it does |
|---|---|
| Set Up Multiplayer | Get a free Photon App ID, paste it in, import Photon Fusion 2. Everything else is automatic. |
| Set Up VR (Quest) | Installs OpenXR and turns on the Quest settings. |
| Build for Quest (APK) | Checks everything and builds an APK. |
| Build Prototype World | Rebuilds the arena, prefabs and rig. |
| Check Multiplayer (Photon Fusion) | Says whether online play is on. |

Without Photon Fusion the game still runs (Training vs bots); a built-in stand-in takes Photon's place until you
import it.

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
| Recenter | Click the right stick (or Menu > Recenter view) | C |

- **Combos:** hits in a row on the same target do more damage; the **4th hit launches** them.
- **Heavy hits:** a really fast swing knocks them back hard.
- **Blocking** takes a fifth of punch damage (half from specials), but blocked hits wear your **guard** down; when
  it breaks you're stunned and can't block for a few seconds.
- **Dodging:** you can't be hit while dashing.
- **K.O.:** at 0 health you're out for 4 seconds, then back at a spawn point. A KO gives you a little health back;
  3 in a row is a **streak**.
- **Destruction:** pillars, crates and the south wall break when you punch them enough, slam near them, or get
  launched into them. They rebuild after half a minute.

### Fighters

| Fighter | Style | Special (X) |
|---|---|---|
| **Brawler** | Heavy punches, big knockback, tough (130 HP) but slow | **Ground Slam**: leap and smash the ground; the shockwave launches everyone near you and breaks things |
| **Speedster** | Lightning-fast punches and dashes, fragile (90 HP) | **Flash Step**: teleport behind the enemy you're looking at; your next punch does double damage and launches |

Pick in **Menu > Fighter**. (Elementalist, Psychic, Shadow Fighter and Titan come next.)

## Playing with people

- **Quick Match** (Menu > Play): free-for-all rooms of up to 8. You're put in the fullest room that still has
  space; a new room with a random code is made only when they're all full.
- **Private lobby**: Menu > Play > Create Private Lobby; friends use **Join a Code** and type its 6 digits. You can
  join a friend's Quick Match room the same way with its code (shown in the menu and on the KO board).
- **Bots**: in Training and private lobbies, **+ Add Bot** (up to 5), **Remove Bots**, and **Easy / Normal / Hard**.
  Bots punch, block, dodge and use specials, and they **learn**: every clean hit you land makes a bot better at
  blocking (up to its difficulty's limit), and if your special catches it, it learns to dodge specials when it
  sees you start one.

## Comfort

Menu > Settings: **snap or smooth turning**, a **comfort vignette** (darkens the edges of your view while you move,
dash or get launched; on by default) and **room-scale walking** (stepping in your room moves your fighter).

## The arena: Training Grounds

A big open floor with a **sparring ring** in the middle, **5 training dummies** on the north side (they show your
damage, hits and DPS), breakable pillars, crate stacks and a breakable wall, a **KO board** (east) and a **How to
fight** board (west).

## How it works (for changing things)

- `Scripts/Combat/Fighter.cs`: one networked fighter (players and bots). The owner moves it, detects its punches
  (hand speed + a small sphere at the fist against `Hurtbox` triggers) and sends hits to the target's owner with
  an RPC, which applies block/dodge/guard/damage/knockback and tells everyone to show the effects.
- `Scripts/Fighters/FighterInfo.cs`: fighter stats. Add a fighter here and its special in `Fighter.UseSpecial`.
- `Scripts/Bots/BotBrain.cs`: the AI (a puppet head and hands the normal punch code reads).
- `Scripts/Network/SessionManager.cs`: Quick Match / private lobbies / codes / training.
- `Editor/ArenaBuilder.cs`: builds the arena, prefab and rig from code.
- All sound and music is generated in code (`Scripts/Core/AudioManager.cs`), so nothing is copyrighted.

## Next steps (from the design)

- 4 abilities per fighter + **Ultimate mode** (meter from dealing/taking damage).
- **Grabs and throws** with the grip buttons.
- Elementalist, Psychic, Shadow Fighter, Titan.
- City Downtown, Volcano Arena, Sky Island (knock people off).
- 1v1 duels and team fights.
- Coins/XP, unlocks (skins, victory poses, effects), daily quests, achievements, leaderboards.
- Voice chat (the Kart VR voice code can be moved over).
