# idrk

**Arena Clash** — a browser game, published as a static site with GitHub Pages.

**Live site:** https://nuggetmc123.github.io/idrk/

The whole game is a single self-contained file, `index.html`, served at the site root.
Open the URL and you're in the game — there is no menu or landing page in front of it.

## Making changes

Edit `index.html`, commit, and push to `main`. The workflow in
`.github/workflows/pages.yml` redeploys the site on every push; there is no build step.

## Working locally

```sh
python3 -m http.server 8000   # then open http://localhost:8000
```

Opening `index.html` straight off disk works too, since the game has no external assets.

## Menu tabs

The menu is three tabs:

- **Play** — pick a fighter and start a match.
- **Stats** — the career record, plus a per-fighter breakdown.
- **Battle Pass** — tier progress, and the titles each tier unlocks.

## The battle pass

XP is **derived** from the career counters rather than stored — 10 per elimination, 25
per match, 5 per minute in the arena — so the pass can never drift out of step with the
record it reflects. A tier is 100 XP, and there are 20 of them.

Each tier grants a **title**, shown next to your name on the menu. Titles are the reward
because they are something the game can actually display: every fighter is unlocked from
the start and there are no skins to hand out. Clicking an unlocked tier equips its title,
clicking the equipped one takes it off, and `setTitle` refuses any tier you have not
reached, so a stale menu cannot grant a locked one. The equipped title is the only part
of the pass that is stored.

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
