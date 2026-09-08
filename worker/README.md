# LEMONADE LAUNCH multiplayer relay

A small Cloudflare Worker. This is **real server code**, deployed separately from
the static game on GitHub Pages — the one thing GitHub Pages itself can never
provide. It never runs game logic; it only relays presence and event messages
between players in the same lobby. See the comment at the top of `index.js`
for how the pieces fit together, and `../lemonade-net.js` for the browser side.

## Deploying it

You'll need a free Cloudflare account (you already have one).

```sh
cd worker
npx wrangler login      # opens a browser to authorize once
npx wrangler deploy
```

The last line of `deploy`'s output is your Worker's URL, something like:

```
https://lemonade-launch-mp.<your-subdomain>.workers.dev
```

Copy it, change `https://` to `wss://`, and paste it into `RELAY_URL` at the
top of `../lemonade-net.js`:

```js
const RELAY_URL = 'wss://lemonade-launch-mp.<your-subdomain>.workers.dev';
```

Commit that change and push — the next deploy of the game picks it up. Leaving
`RELAY_URL` blank (the default) is fully supported: the game is just you and
your own stand, exactly as before this feature existed.

## What it costs

Free, at this scale. Workers' free tier is 100,000 requests/day, and Durable
Objects' free tier covers what a small hobby game's worth of lobbies will use
many times over. If LEMONADE LAUNCH ever gets popular enough to outgrow that,
Cloudflare will tell you — nothing here needs a paid plan to start.

## Testing it yourself without deploying

```sh
npx wrangler dev --local
```

Runs the exact same code against the real Workers runtime, entirely on your
own machine — no Cloudflare account touched, no deploy needed. Point a local
copy of `lemonade-net.js` at `ws://127.0.0.1:8787` (wrangler's default local
port) to try it before deploying for real.

## Why this relay is simpler than it looks

The previous game on this repo (a fighting game) needed a "host" browser to
run the whole match and narrate it to everyone else, because every player's
health bar depended on the same fight. Lemonade stands don't have that
problem: your customers, your recipes, and your coins are entirely your own
business, simulated only on your machine. So this relay never elects a host
or trusts one client's word over another's — every member just broadcasts
its own **presence** (where your avatar is, what it's doing) and **events**
(you served someone, you got launched into orbit) directly to the room, and
the room fans it out to everyone else. Nobody's game state depends on
anything arriving correctly, which is what makes this safe to keep this
simple.

## Known limitations (v1)

- **No moderation beyond the name filter.** A modified client could still
  send nonsense presence/event data — it's shape-clamped so it can't break
  anyone's renderer, but nothing stops a joker from spamming fake "launched
  into orbit" events. Cosmetic only; nobody's coins or progress are at risk
  from another player's session, since each player's save lives in their own
  browser/account (see `../profile.js`).
- **Matchmaking is a simple queue**, not skill- or location-based — "is
  anyone else looking right now," nothing more.
- **No reconnect grace period.** A dropped connection just leaves the lobby;
  rejoining with the same code is the recovery path.
