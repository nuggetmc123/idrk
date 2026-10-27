# Veylix website

The game's website: a home page (`index.html`) and the privacy policy (`privacy.html`). Plain HTML, CSS and a little
JavaScript with no build step. Fonts come from Google Fonts.

## Where it goes live

This repo's GitHub Pages workflow publishes the repo from `main`, so once this folder is on `main` the site is at:

- Home: https://nuggetmc123.github.io/idrk/veylix-site/
- Privacy policy: https://nuggetmc123.github.io/idrk/veylix-site/privacy.html

`deploy.sh` copies the same files to idrk.dev, so they also appear at `https://idrk.dev/veylix-site/`.

For the Meta Quest store, put the privacy policy link in Developer Dashboard > your app > Distribution > App Metadata
(Privacy Policy URL).

## Keep the privacy policy true

It describes what the game does today: local saves, Photon for online play, no accounts, ads, analytics, purchases or
chat. If you add something that collects or shares data, update the policy and its effective date.
