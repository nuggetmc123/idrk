# Rago Extract website

A home page (`index.html`) and the privacy policy (`privacy.html`), plain HTML and CSS with no build step.

## Where it goes live

The repo's GitHub Pages workflow publishes `main`, so once this folder is on `main`:

- Privacy policy: https://nuggetmc123.github.io/idrk/rago-site/privacy.html
- Home: https://nuggetmc123.github.io/idrk/rago-site/

`deploy.sh` also copies it to idrk.dev (`https://idrk.dev/rago-site/privacy.html`).

Put the privacy policy link in the Meta Developer Dashboard > your app > Distribution > App Metadata (Privacy Policy URL).
The game server still serves the same policy at `/privacy`; keep the two in step when the policy changes.
