# idrk

IDRK Games — a collection of browser games, published as a static site with GitHub Pages.

**Live site:** https://nuggetmc123.github.io/idrk/

## Adding a game

1. Drop the file into `games/`:
   - single file → `games/my-game.html`
   - game with its own assets → `games/my-game/index.html` (put images, audio, etc. beside it)
2. Commit and push to `main`.

That's it. On every push to `main`, the workflow regenerates `games.json` and redeploys the
site, so the new game shows up on the landing page automatically — no editing `index.html`.

The card on the landing page is built from the game's own HTML:

- **Title** — the `<title>` tag (falls back to a prettified filename)
- **Blurb** — `<meta name="description" content="...">`, if present

## Repo layout

| Path | What it is |
| --- | --- |
| `index.html` | Landing page; fetches `games.json` and renders the grid |
| `games/` | The games themselves |
| `games.json` | Generated index — do not hand-edit |
| `tools/build-index.mjs` | Scans `games/` and writes `games.json` |
| `.github/workflows/pages.yml` | Builds the index and deploys to Pages |
| `.nojekyll` | Serves files verbatim (keeps Jekyll from ignoring `_`-prefixed paths) |

## One-time setup

In the repo on GitHub: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
The first deploy runs on the next push to `main`.

## Working locally

```sh
node tools/build-index.mjs   # refresh games.json
python3 -m http.server 8000  # then open http://localhost:8000
```

Games must be self-contained or reference only files inside the repo — Pages serves static
files, so there is no server-side code.
