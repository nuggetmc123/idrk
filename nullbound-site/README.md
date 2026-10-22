# Nullbound website

The game's website: a home page (`index.html`) and the privacy policy (`privacy.html`). It is plain HTML and CSS
with no build step, and only uses files from `assets/`.

## Where it goes live

This repo's GitHub Pages workflow (`.github/workflows/pages.yml`) publishes the whole repo from `main`, so once this
folder is on `main` the site is at:

- Home: https://nuggetmc123.github.io/idrk/nullbound-site/
- Privacy policy: https://nuggetmc123.github.io/idrk/nullbound-site/privacy.html

`deploy.sh` copies the same files to idrk.dev, so they also appear at `https://idrk.dev/nullbound-site/`.

For the Meta Quest store, put the privacy policy link in Developer Dashboard > your app > Distribution > App Metadata
(Privacy Policy URL).

## Editing

- Text: edit `index.html` / `privacy.html`. Update the "Effective date" at the top of the privacy policy whenever
  you change it.
- Look: `site.css`. Fonts are Rajdhani and Barlow (`assets/fonts`, SIL Open Font License).
- Images, the trailer and the logo come from the store-art renders. Swap in real headset captures when you have them.

## Keep the privacy policy true

It describes what the game does today: local saves, Photon for online play and voice, the moderation list from
GitHub, the optional Cloudflare name server, no ads or analytics. If you add something that collects or shares
data (analytics, ads, accounts, purchases, a new server, the Meta Platform SDK, text chat), update the policy too.
