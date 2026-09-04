# idrk

**BRAWLBOUND** — a browser game, published as a static site with GitHub Pages.

**Live site:** https://nuggetmc123.github.io/idrk/

The whole game is a single self-contained file, `index.html`, served at the site root.
Open the URL and you're in the game — there is no menu or landing page in front of it.

## Making changes

Edit `index.html`, commit, and push to `main`. The workflow in
`.github/workflows/pages.yml` redeploys the site on every push; there is no build step.

## Cache-busting

`index.html` loads `profile.js?v=__DEPLOY_SHA__`, and the workflow substitutes the real
commit SHA into that placeholder before it deploys. GitHub Pages caches static files in
the visitor's browser for a while, so a fix to `profile.js` can go live while a plain
reload keeps serving the old one — this bit the repo more than once. Because the version
string changes on every commit, a fresh `index.html` always requests a `profile.js` URL
the browser has never seen, so it can't come from the stale cache. The one moment this
can't help is the very first load after a deploy, if the browser still has the *previous*
`index.html` cached too — a hard reload (Ctrl/Cmd+Shift+R) clears that.

## Working locally

```sh
python3 -m http.server 8000   # then open http://localhost:8000
```

Opening `index.html` straight off disk works too, since the game has no external assets.

## The roster

Twenty fighters, each with a main weapon and a special ability, defined in `CLASSES` and
implemented in `doAttack` / `doSpecial`:

| | Fighters |
| --- | --- |
| Starting five | Assassin, Ninja, Archer, Wizard, Witch |
| Battle pass, tiers 5-50 | Berserker, Gunslinger, Frostmage, Paladin, Bomber, Sniper, Duelist, Necromancer, Monk, Ranger |
| Shop only | Samurai, Alchemist, Stormcaller, Gladiator, Phantom |

Adding a fighter means a `CLASSES` entry, a `case` in `doAttack` and `doSpecial`, and an
icon in `classIcon`. Everything else is shared: close-range weapons all go through
`meleeSwing`, ranged ones through `addProj`, and area abilities through the existing
`meteor` and `cloud` effects, so a new fighter needs no new engine code. In the arena the
unlockable roster shares one helmet told apart by crest and colour, so it stays readable
without hand-drawn art per fighter.

Bots pick from the whole roster and drive their own behaviour from `botRange`, `botSp`
and `melee` on the class, rather than from a list of names.

## Menu tabs

The menu is three tabs:

- **Play** — the match summary and the PLAY button. Picking a fighter happens on the
  select screen that PLAY opens, not here.
- **Stats** — the career record, plus a per-fighter breakdown.
- **Battle Pass** — tier progress across five pages, chests to open, and rewards to claim.
- **Shop** — buy the five shop fighters and any skin, with coins.
- **Locker** — everything you own: the hat you wear, and the skin each fighter wears.

## Starting a match

PLAY opens a fighter select screen showing all twenty. So does the panel shown on death
mid-match — `ownedClassKeys()` is the single source of truth for "what can this player
respawn as", used by the panel's grid, its keyboard/gamepad cycling, and the class the
Deploy button actually sends out. A human is never offered a fighter they have not
unlocked; bots are unaffected and keep drawing from the full roster. Locked cards say where the fighter
comes from — a pass tier or a coin price — and clicking one takes you to the tab that has
it. START MATCH is disabled until an owned fighter is chosen.

## Matches

A match runs for **five minutes** (`MATCH_SECS`), counted down by a clock in the HUD. When
it ends, a card totals up eliminations, deaths and the battle pass XP earned.

XP stays derived from the career counters, so that card is a report of what the match
added rather than a second source of truth — a five minute match is worth 50 XP flat
(25 for the match, 25 for the time) plus 10 for each elimination.

`bankMatch` folds the match into the career exactly once, whether the clock ran out or the
player quit to the menu part way through.

## Online multiplayer

The Play tab has a **Play with friends** panel: create a lobby, get a short code, send it
to someone, they enter it and join. Up to 4 players total (matching the arena's 4-fighter
cap) — real people fill the roster first, and pressing PLAY looks briefly for other people
searching for a match before filling anything still open with bots.

This needs a small piece GitHub Pages itself cannot provide: **`worker/`** is a real
Cloudflare Worker, deployed separately, that relays lobby and match messages between
players. See **`worker/README.md`** for exactly what to run — it's two commands
(`wrangler login`, `wrangler deploy`) and pasting the resulting URL into
`RELAY_URL` at the top of `multiplayer.js`. Leaving it blank is fully supported: every
match is just you and bots, exactly as before this feature existed.

**How a match actually runs:** whoever creates the lobby is the host, and their browser
runs the exact same simulation it always has for bots — real people just occupy some of
those slots now instead of AI, fed by input relayed over the connection. Everyone else's
screen renders what the host reports (positions, health, who's still standing) rather
than simulating the fight themselves. This is the same trust model the game already had;
online play doesn't add a "server decides who's cheating" layer, just more players in the
existing loop.

Each player's own battle pass and stats update on **their own account**, wherever they're
playing from — an elimination or death is relayed as an event tagged with whose it was,
and each machine reacts only to events about itself. `worker/README.md` lists what's
intentionally cut from this first version (no mid-match host migration, kill-feed text
reads from the host's perspective, no projectile/particle sync for anyone but the host).

This is genuinely tested end-to-end: two independent headless browsers, a real deploy of
the Worker running locally via `wrangler dev`, lobby creation and joining, a full match
starting with a real remote player and bots, an elimination correctly crediting each
player's own career on their own machine, and a networked respawn choice round-tripping
back into the live match — all verified with zero errors before this shipped, not assumed.

## Coins, the shop and chests

Coins are stored as two grow-only totals, `earned` and `spent`, and the balance is the
difference. A plain balance would be wrong: the sync merges by taking the max of each
counter, so coins spent on one device would come back from the dead on the next merge.
Every collection (`owned`, `skins`, `hats`, `chests`, `opened`, `claimed`) is grow-only
for the same reason and merges as a union.

- **Fighters** — ten come from the battle pass, two per page at tiers 5 and 10 of each,
  and are never sold. Five more are sold only in the shop and never appear on the track,
  so each route has something the other cannot give you.
- **Skins** — a palette swap, so one definition reskins any fighter. Ids are
  `<fighter>:<skin>`, and a skin can only be bought for a fighter you own.
- **Hats** — not sold. They come from chests and the pass; the shop row is for wearing them.
- **Chests** — five kinds. A chest rolls against the fighters you already own, so it can
  never hand you a skin for someone unplayable, and falls back to coins when there is
  nothing new left to win.

`buy`, `claim`, `equipSkin`, `equipHat` and `openChest` all enforce their own rules — you
cannot afford what you cannot afford, claim a tier you have not reached, open a chest
twice, or equip something you do not own — so the menu is never the thing keeping the
books.

## The battle pass

XP is **derived** from the career counters rather than stored — 10 per elimination, 25
per match, 5 per minute in the arena — so the pass can never drift out of step with the
record it reflects. A tier is 100 XP; there are 50 of them across 5 pages.

Every page of ten runs the same shape — two fighters, a skin, a hat, a chest, a title and
four coin rewards that climb with the page — so all ten fighters live on the track. Reaching a tier does not grant it — you
claim it, and a claim is refused for any tier above the one you have reached.

## Player accounts and saved stats

The game keeps a career record — eliminations, deaths, best streak, matches, time in
the arena, and a per-class breakdown. It lives in `profile.js`:

- **Signed out** — stats go to `localStorage`, so they stay in that one browser.
- **Signed in with Clerk** — stats go to the user's `unsafeMetadata`, so they follow the
  player to any browser they sign in on.

`unsafeMetadata` is the only metadata Clerk lets the browser write, which is what makes
per-user data work here with no server at all. Two things follow from that:

- It is capped at **8 KB per user**. The record is a handful of counters, so it is nowhere
  near that, but don't grow it into match history.
- It is **editable by the player** in devtools. Fine for a personal record; do not build a
  competitive leaderboard on it without a server to verify writes.

### Configuration

The publishable key is set in `CLERK_PUBLISHABLE_KEY` at the top of `profile.js`, pointing
at the `verified-mackerel-9446` development instance. Publishable keys are public by
design — they are meant to ship in client code — so it lives in the repo rather than in a
secret. It also encodes the Clerk host the script is fetched from, which is why it is the
only value needed.

If the sign-in window ever refuses the site, add `nuggetmc123.github.io` to the instance's
allowed origins in the Clerk dashboard.

Nothing here is load-bearing for the game: with the key removed, or with Clerk unreachable,
the account bar says so and stats keep saving locally.

### How often it writes

Scoring events only touch `localStorage`. The record is pushed to Clerk at two points —
when you leave a match, and when the tab is hidden or closed — because a busy match scores
far too often to be a per-event write target, and Clerk's Frontend API rate-limits.

A write is also skipped when the account already holds exactly the same record. That check
is what stops a write from bouncing: Clerk notifies its listeners after every
`user.update()`, so flushing from inside that listener makes each write trigger the next
one. `node profile.test.mjs` drives `profile.js` against a fake Clerk with that same
notify-on-write behaviour and fails if the writes run away again.

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

This repo briefly had a landing page listing several games (Brownie Clicker, Jetpack Ride)
with a generated `games.json` index. That was removed in favor of Arena Clash alone; the
files are still in the git history if they're ever wanted back.
