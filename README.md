# idrk

**LEMONADE LAUNCH** — a browser game, published as a static site with GitHub Pages.

**Live site:** https://nuggetmc123.github.io/idrk/

The game is a handful of self-contained files (`index.html` plus a few `.js` files sitting
next to it) served at the site root. Open the URL and you're in the game — there is no
menu or landing page in front of it.

## Making changes

Edit the files, commit, and push to `main`. The workflow in `.github/workflows/pages.yml`
redeploys the site on every push; there is no build step.

## Cache-busting

`index.html` loads every game script with a `?v=__DEPLOY_SHA__` query string, and the
workflow substitutes the real commit SHA into each placeholder before it deploys. GitHub
Pages caches static files in the visitor's browser for a while, so a fix to, say,
`lemonade-game.js` can go live while a plain reload keeps serving the old one — this bit
the repo more than once (back when only `profile.js` carried this treatment). Because the
version string changes on every commit, a fresh `index.html` always requests script URLs
the browser has never seen, so none of them can come from the stale cache. The one moment
this can't help is the very first load after a deploy, if the browser still has the
*previous* `index.html` cached too — a hard reload (Ctrl/Cmd+Shift+R) clears that.

## Working locally

```sh
python3 -m http.server 8000   # then open http://localhost:8000
```

Opening `index.html` straight off disk mostly works too — the one thing that needs a real
HTTP origin is nothing in this game actually, since it has no fetch-based assets, so file://
is fine as well.

## What the game is

Run a lemonade stand, in first person. A customer walks up with an order (shown as
ingredient icons over their head) and a patience bar that only goes down. Click the right
ingredients into the pitcher, hit **MIX** (click **STIR!** while the meter is in the
glowing zone for a Perfect mix and a bonus tip), **POUR**, then click the customer to serve
them. Get it right before they run out of patience and you're paid; get it wrong, take too
long, or overflow the pitcher and something ridiculous happens — **you get launched into
orbit**, ragdoll and all, and respawn back at the stand a few seconds later. Coins buy
stand upgrades, new locations, and wardrobe items; achievements and rare ingredients give
you reasons to keep coming back.

The whole thing renders in real 3D (via [three.js](https://threejs.org/), loaded from
cdnjs — the one external dependency this game has), with synthesized sound effects (no
audio files — see `lemonade-sfx.js`, everything is generated on the fly with the Web Audio
API) and a chunky, rounded, flat-shaded low-poly art style — think "cheerful chaos
sandbox game," not photorealism.

### Walking around

You're not stuck behind the counter. Mouse-look (click the canvas to grab the cursor;
Esc lets go) or touch-drag steers where you're looking, WASD/arrow keys (or the on-screen
d-pad on touch devices) walk you around a small yard behind your stand. Walk up to
something and press **E** (or the on-screen **USE** button) to interact with it:

- **Ingredient stations** — a barrel/crate for each ingredient this location uses, laid
  out in the yard. Collecting from one adds it to the pitcher exactly like clicking its
  HUD button does; it's a second, more scenic way to gather the same ingredients, and it
  still respects a location's twist (a frosted mountain barrel needs cracking first, a
  space station ingredient still floats off as a bubble you have to chase).
- **The tip jar** — fills slowly with loose change while you work the counter. Walk over
  and interact to collect it before it caps out.
- **The fishing spot** — a short "reel it in" timing minigame (just like STIR, on its own
  meter) for a chance at coins or, less often, a shot at the location's rare ingredient
  without waiting on the passive drop chance.

### Wardrobe

Purely cosmetic hats — a chef's toque, a tinfoil hat, alien antennae, a fish (this game's
one direct nod to its "How to Fish"-flavored art direction) — bought with coins in the
Upgrades tab and worn on your own avatar in-game. Get launched into orbit and your hat
comes with you, tumbling off separately in the ragdoll.

### File map

| File | What it is |
| --- | --- |
| `index.html` | page shell: the menu screens, the in-game HUD overlay, and all the CSS |
| `lemonade-data.js` | every ingredient, recipe, location, customer type, upgrade and achievement — pure data |
| `lemonade-game.js` | the menu wiring + the three.js engine + the gameplay loop |
| `lemonade-sfx.js` | synthesized sound effects (Web Audio, no files) |
| `profile.js` | the save file: coins, unlocks, upgrades, achievements (see below) |
| `lemonade-net.js` | the online multiplayer client |
| `worker/` | the Cloudflare Worker that relays multiplayer messages (see `worker/README.md`) |

## The six locations

Each location has its own recipes, ingredients, customers, and one signature chaos
mechanic ("twist"). They unlock with coins, in order, each pricier than the last:

| Location | Twist | What it does |
| --- | --- | --- |
| Neighborhood | — | The tutorial-easy starting stand. Always unlocked. |
| Beach | 🐦 Seagulls | A seagull will swoop in and steal an un-served cup if it sits too long. |
| City | 🚕 Rush Hour | Periodic rush hours spawn customers faster with shorter patience. |
| Amusement Park | 🎡 Dizzy | Customers wobble in place and sometimes change their order mid-wait. |
| Snowy Mountain | ❄️ Freeze | Ingredient bins ice over if you don't use them — click through the frost first. |
| Space Station | 🌌 Zero-G | Ingredients float free as a bubble you have to click before it drifts away. |

Every location also has one **secret recipe**, gated behind a **rare ingredient** that
only that location can drop (a small, luck-upgradeable chance on any successful serve).
Finding it unlocks the recipe for good. The Snowy Mountain additionally hides a rare
cameo customer — the Yeti — who only shows up once you've found its secret ingredient,
and pays enormously if you can actually make what they came for.

## Progression

- **Coins** — paid per successful order, with a bonus for a Perfect mix and for the
  customer's own tip range. Spent on location unlocks and upgrades; never goes negative.
- **Upgrades** (`lemonade-data.js`'s `UPGRADE_TRACKS`, five tracks, coin cost climbing
  1.6x per level) — faster mixing, longer customer patience, better rare-drop/tip luck,
  more customers served at once, and a bigger pitcher that pours more than one cup per mix.
- **Achievements** — sixteen of them, from "serve your first customer" to "get launched
  into space 100 times" to finding every rare ingredient. Each pays out once, straight
  into your coin balance, the moment its condition is met.

## The launch-into-orbit failure sequence

Wrong drink, patience hitting zero, or overflowing the pitcher (it holds 10 units) all end
the same way: a squash-and-stretch anticipation beat, then the player figure blows apart
into its six low-poly limbs, each given its own upward velocity and spin — a cheap,
hand-rolled ragdoll rather than a physics engine, since nothing here needs to *collide*
with anything, just tumble dramatically off the top of the screen for about a second and a
half. A randomized caption roasts you for whatever went wrong, a countdown ticks down, and
you're back at the stand. Every mess-up resets your combo streak to zero but otherwise
costs you nothing but time — the whole point is that it's funny, not punishing.

## Online multiplayer

The Friends tab lets you create or join a lobby by a short code, or quick-match into
whoever else is looking. Unlike a synced simulation, **each player's stand, customers and
economy are simulated entirely on their own machine** — nobody's game depends on anyone
else's state, so there's no host to trust and nothing to desync. What you actually get
from playing together: you see your friends' avatars idling at their own stand if they're
in the same location as you, and a toast pops up whenever one of them serves a customer or
— more importantly — gets launched into orbit.

This needs a small piece GitHub Pages itself cannot provide: **`worker/`** is a real
Cloudflare Worker, deployed separately, that relays presence and event messages between
players in a lobby. See **`worker/README.md`** for exactly what to run — it's two commands
(`wrangler login`, `wrangler deploy`) and pasting the resulting URL into `RELAY_URL` at the
top of `lemonade-net.js`. Leaving it blank is fully supported: the game is just you and
your own stand, exactly as before this feature existed — the Friends tab says so plainly
rather than pretending to offer something that isn't configured.

## Coins, unlocks and the save file

Coins are stored as two grow-only totals, `earned` and `spent`, and the balance is the
difference — never a plain balance field. The sync-to-account merge (see below) takes the
max of each counter, so coins spent on one device can't come back from the dead on the
next merge. Unlocked locations, found rare ingredients, unlocked secret recipes, and
earned achievements are all grow-only lists that merge as a union for the same reason.

`unlockLocation`, `buyUpgrade`, `findRareIngredient` and `completeOrder` all enforce their
own rules — you cannot afford what you cannot afford, and an achievement can only ever be
earned once — so the menu and the game loop are never the thing keeping the books; `profile.js` is.

## Player accounts and saved progress

The game keeps a save file — coins, unlocked locations and recipes, upgrade levels,
achievements, and a handful of lifetime stats. It lives in `profile.js`:

- **Signed out** — progress goes to `localStorage`, so it stays in that one browser.
- **Signed in with Clerk** — progress goes to the user's `unsafeMetadata`, so it follows
  the player to any browser they sign in on.

`unsafeMetadata` is the only metadata Clerk lets the browser write, which is what makes
per-user data work here with no server at all. Two things follow from that:

- It is capped at **8 KB per user**. The save file is a handful of counters and short
  lists, nowhere near that — don't grow it into per-order history.
- It is **editable by the player** in devtools. Fine for a personal save file; do not
  build a competitive leaderboard on it without a server to verify writes.

### Configuration

The publishable key is set in `CLERK_PUBLISHABLE_KEY` at the top of `profile.js`, reusing
the same `verified-mackerel-9446` development instance this repo already had configured
and allow-listed for this origin — Clerk itself doesn't care which game is asking it to
remember a user. Publishable keys are public by design, which is why it lives in the repo
rather than in a secret.

If the sign-in window ever refuses the site, add `nuggetmc123.github.io` to the instance's
allowed origins in the Clerk dashboard.

Nothing here is load-bearing for the game: with the key removed, or with Clerk unreachable,
the account bar says so and progress keeps saving locally.

### How often it writes

Ordinary scoring events (serving a customer, getting launched) only touch `localStorage` —
a busy stand can score dozens of times a minute and Clerk's Frontend API rate-limits. A
write is pushed to the account at real checkpoints instead: unlocking a location, buying
an upgrade, finding a rare ingredient, earning an achievement, leaving the stand for the
menu, or the tab being hidden/closed. A write is also skipped when the account already
holds exactly the same record — Clerk notifies its listeners after every `user.update()`,
so flushing from inside that listener would otherwise make each write trigger the next
one. `node profile.test.mjs` drives `profile.js` against a fake Clerk with that same
notify-on-write behaviour and fails if the writes ever run away again.

### The custom-domain limit

This has to stay on a Clerk **development** instance while the site lives at
`nuggetmc123.github.io`. Clerk *production* instances require CNAME records on a domain
you own, and you can't add DNS records to `github.io`. Development instances are capped at
**100 users** and are not meant for real traffic.

To go past that: buy a domain, point it at GitHub Pages (Settings → Pages → Custom
domain), then create a Clerk production instance for it and swap in the `pk_live_` key.

## Pages setup

Already done: **Settings → Pages → Build and deployment → Source: GitHub Actions**.

## History

This repo has changed games entirely more than once — it briefly hosted a landing page
listing several small games, then a battle-royale-style fighter called BRAWLBOUND (Arena
Clash), and now this lemonade stand simulator. Each time, the previous game's files are
fully replaced rather than kept alongside a new one, on the theory that this repo is one
game at a time, not a portfolio. Nothing is lost — every earlier version is still sitting
in the git history if it's ever wanted back, `worker/` included (BRAWLBOUND's relay
protocol was fighter-roster-specific; this game's `worker/` is a from-scratch, simpler
presence relay rather than a repurposed one, precisely because nothing here needs a host
to trust the way a synced fight did).
