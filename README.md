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

## Pages setup

Already done: **Settings → Pages → Build and deployment → Source: GitHub Actions**.

## History

This repo briefly had a landing page listing several games (Brownie Clicker, Jetpack Ride)
with a generated `games.json` index. That was removed in favor of Arena Clash alone; the
files are still in the git history if they're ever wanted back.
