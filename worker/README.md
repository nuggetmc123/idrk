# BRAWLBOUND multiplayer relay

A small Cloudflare Worker. This is **real server code**, deployed separately from
the static game on GitHub Pages — the one thing GitHub Pages itself can never
provide. It never runs game logic; it only relays messages between players and
keeps track of who's in which lobby. See the comment at the top of `index.js`
for how the pieces fit together, and `../multiplayer.js` for the browser side.

## Deploying it

You'll need a free Cloudflare account (you already have one).

```sh
cd worker
npx wrangler login      # opens a browser to authorize once
npx wrangler deploy
```

The last line of `deploy`'s output is your Worker's URL, something like:

```
https://brawlbound-mp.<your-subdomain>.workers.dev
```

Copy it, change `https://` to `wss://`, and paste it into `RELAY_URL` at the
top of `../multiplayer.js`:

```js
const RELAY_URL = 'wss://brawlbound-mp.<your-subdomain>.workers.dev';
```

Commit that change and push — the next deploy of the game picks it up. Leaving
`RELAY_URL` blank (the default) is fully supported: every match is just you and
bots, exactly as before this feature existed.

## What it costs

Free, at this scale. Workers' free tier is 100,000 requests/day, and Durable
Objects' free tier covers what a small hobby game's worth of lobbies and
matches will use many times over. If BRAWLBOUND ever gets popular enough to
outgrow that, Cloudflare will tell you — nothing here needs a paid plan to
start.

## Testing it yourself without deploying

```sh
npx wrangler dev --local
```

Runs the exact same code against the real Workers runtime, entirely on your
own machine — no Cloudflare account touched, no deploy needed. Point a local
copy of `multiplayer.js` at `ws://127.0.0.1:8787` (wrangler's default local
port) to try it before deploying for real.

## Known limitations (v1)

- **No mid-match host migration.** If the lobby host's tab closes during a
  live match, the match ends for everyone. Recoverable by starting a new one.
- **Kill-feed text is host-perspective.** The feed line for an elimination is
  written once, on the host's machine, using the host's own point of view
  (e.g. "You eliminated Guest482"), and relayed as-is. On someone else's
  screen this can read a little oddly (it will still say "You" for whoever
  the host is, not for the viewer). Positions, health, scores, and each
  player's own battle-pass credit are all unaffected — this is a display-text
  quirk only.
- **No projectile/particle sync.** Clients see accurate positions, health,
  aim, status effects, and the kill feed, but not the fireballs/arrows/slashes
  themselves — those stay a host-side visual flourish. A worthwhile follow-up
  would be relaying lightweight "fired an attack" events so clients can play
  their own local cosmetic effects.
- **Matchmaking is a simple queue**, not skill-based — "is anyone else
  looking right now," nothing more.
