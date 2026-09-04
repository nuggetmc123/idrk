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

### Turning it on

1. Create an application at [dashboard.clerk.com](https://dashboard.clerk.com).
2. Copy the publishable key (**API keys → JavaScript**). It starts with `pk_test_`.
3. Paste it into `CLERK_PUBLISHABLE_KEY` at the top of `profile.js`.
4. In Clerk, add `nuggetmc123.github.io` under the instance's allowed origins.

The key is public by design — it is meant to ship in client code, and it encodes the
Clerk host the script loads from, so it is the only value needed. With it left empty the
game still runs and still saves stats locally; the sign-in button just doesn't appear.

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
