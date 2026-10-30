# Sloppy account server (optional)

Online play itself runs on **Photon Fusion 2** (Sloppy > Set Up Multiplayer). This small,
free Cloudflare Worker is an optional extra that remembers players between games:
names unique across everyone, friends lists, saved reports, and bans/mutes by player ID.

The Worker can also run whole matches by itself (it was Sloppy's first multiplayer), so it handles:

- **Accounts** — every player gets a permanent player ID like `FZ7K3Q9X`
- **Unique names** — no two players can have the same name (not case-sensitive), and names with swearing or slurs are refused
- **Friends** — add by name or player ID, accept/decline, see who's online and where
- **Parties** of up to 6 — wherever the leader goes, the party goes; in team modes a party stays on one team, and a full party of 6 in 3v3 is split 3 and 3
- **Matchmaking** for Team Deathmatch, Free For All, 1v1, 2v2, 3v3 and Capture the Flag, plus hub rooms of up to 24
- **Match rules** — health, kills, scores, the clock, flags
- **Chat auto-mod** — "fuck", "shit" and "damn" are allowed; slurs and other bad words mute the sender for 15 minutes
- **Reports** and **moderation** — kick, ban, mute, unban, unmute, rename

It starts on Cloudflare's free plan. Every player sends about 15 small position
updates a second while in a room, so the free daily allowance covers a few hours of
10-player play per day; if Sloppy gets busier than that, Workers Paid is $5/month.

## Turn it on

Easiest: in Unity, **Sloppy > Set Up Multiplayer**, open **Optional: account server**, and press the three buttons
(install Node.js, sign in to Cloudflare, Turn On Server). It fills in the address by itself.
Then **Sloppy > Build for Quest (APK)** so the headset gets it.

By hand instead (needs [Node.js](https://nodejs.org) and a free [Cloudflare](https://dash.cloudflare.com/sign-up) account):

```bash
cd Server
```

```bash
npm install
```

```bash
npx wrangler login
```

```bash
npx wrangler deploy
```

The last line printed is the server's address, like
`https://flopzone-server.<your-name>.workers.dev`. Paste it into the **Server address**
box in Sloppy > Set Up Multiplayer.

## Make yourself a moderator

1. Play once (online) and open the menu (≡ on the left controller) → **Profile**. Your player ID is shown there.
2. Put it in `wrangler.toml`:
   ```toml
   [vars]
   ADMIN_IDS = "FZ7K3Q9X"
   ```
   (several IDs: `"FZ7K3Q9X,FZAB12CD"`)
3. Press **Turn On Server** again (or run `npx wrangler deploy`)

You now have a **MOD** tab in the in-game menu: pick a player in your room or type
their player ID or name, then Lookup, Kick, Mute, Ban, Unban, see Reports, or see who's online.

## Moderate from your computer

Set a secret admin key once:

```bash
npx wrangler secret put ADMIN_KEY
```

Then use the **Moderation** section of Sloppy > Set Up Multiplayer in Unity (type the same key), or
any browser / terminal:

```bash
curl "https://flopzone-server.<you>.workers.dev/admin?key=YOUR_KEY&action=lookup&target=FZ7K3Q9X"
```

```bash
curl "https://flopzone-server.<you>.workers.dev/admin?key=YOUR_KEY&action=ban&target=FZ7K3Q9X&minutes=1440&reason=slurs"
```

Actions: `lookup`, `kick`, `ban` (`minutes=0` = forever), `unban`, `mute` (default 60 min),
`unmute`, `rename` (makes them pick a new name), `reports` (last 50), `clear_report`
(`target` = report id), `online`, `promote` / `demote` (moderator on/off).

## Test it locally

```bash
printf 'ADMIN_KEY=testkey\n' > .dev.vars
```

```bash
npx wrangler dev
```

and in a second terminal:

```bash
node test/smoke.mjs
```

The smoke test checks the word filter, unique names, reconnecting, friends, parties
following the leader, a 6-party splitting 3/3 in 3v3, the auto-mute, reports, admin
actions, damage and kills, and bans.

## How it works

`src/index.js` sends every connection to one Durable Object (`src/game.js`). That single
object holds all accounts (stored in its built-in storage), parties, hub rooms and
matches, so there's no cross-server messaging to go wrong. Each headset is trusted for
its own position and its own hit detection (like most hobby VR shooters); the server
owns health, kills, scores, timers, flags, chat and moderation.

`src/filter.js` is the word list. It's kept in sync by hand with
`Assets/Flopzone/Scripts/Core/WordFilter.cs`, which gives instant feedback on the headset.

## Limits

- One server object handles everyone. That's comfortable for a few dozen players online at once; past that, rooms would need splitting across objects.
- If a match's last player leaves, the match ends. There's no "host" to lose: the server runs every match.
- Hit detection is done by the shooter's headset. A modded client could cheat; reports and bans are the answer for now.
- The auto-mod reads **text chat** (typed or quick-chat). There is no voice chat in this version.
