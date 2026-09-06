#!/usr/bin/env bash
# deploy.sh — lives at /home/idrk/deploy.sh, runs AS the idrk user (no sudo/root).
#
# Pulls the latest main branch of https://github.com/nuggetmc123/idrk into
# ~/repo, copies the static site files into the CloudPanel docroot, stamps
# the deploy SHA into index.html for cache-busting, and verifies the result
# with a local curl. Mirrors what .github/workflows/pages.yml already does
# for GitHub Pages, but targets this server's docroot instead.
set -euo pipefail

REPO_DIR="$HOME/repo"
DOCROOT="$HOME/htdocs/idrk.dev"
DOMAIN="idrk.dev"

echo "==> Fetching latest main"
git -C "$REPO_DIR" fetch --depth=1 origin main
git -C "$REPO_DIR" reset --hard origin/main

DEPLOY_SHA="$(git -C "$REPO_DIR" rev-parse HEAD)"
echo "==> Deploying commit: $DEPLOY_SHA"

# rsync the static site into the docroot. worker/ is a separate Cloudflare
# Worker deployed via wrangler elsewhere, not part of this static site, and
# .git/.github/README/tests/this script are dev-only — none of them belong
# in the docroot.
#
# Deliberately NOT using --delete (or any rm -r) here: the repo owner has a
# hard rule against recursive deletion after past data-loss incidents. Any
# files removed from the repo will simply linger in the docroot until
# someone cleans them up by hand — safer than risking an accidental wipe.
echo "==> Syncing files to $DOCROOT"
rsync -av \
  --exclude='.git' \
  --exclude='.github' \
  --exclude='worker' \
  --exclude='README.md' \
  --exclude='profile.test.mjs' \
  --exclude='.nojekyll' \
  --exclude='deploy.sh' \
  "$REPO_DIR"/ "$DOCROOT"/

# Stamp the deploy SHA into the *copied* index.html only — never touch the
# repo's own copy, so the next `git reset --hard` above starts clean again.
echo "==> Stamping __DEPLOY_SHA__ -> $DEPLOY_SHA"
sed -i "s/__DEPLOY_SHA__/${DEPLOY_SHA}/" "$DOCROOT/index.html"

echo "==> Deployed SHA: $DEPLOY_SHA"

# Local sanity check: hit nginx on this box over HTTPS (plain HTTP just 301s
# to https), pinning the hostname to 127.0.0.1 so it works before external
# DNS points here, and ignoring the cert since it may still be self-signed.
# Confirm a 200 and that profile.js's cache-busting query string made it in.
echo "==> Verifying local response"
RESPONSE="$(curl -sk -o /tmp/idrk-deploy-check.html -w '%{http_code}' --resolve "${DOMAIN}:443:127.0.0.1" "https://${DOMAIN}/")"

if [ "$RESPONSE" != "200" ]; then
  echo "!! Verification failed: expected HTTP 200, got ${RESPONSE}" >&2
  exit 1
fi

if ! grep -q "profile.js?v=${DEPLOY_SHA}" /tmp/idrk-deploy-check.html; then
  echo "!! Verification failed: profile.js query string does not contain ${DEPLOY_SHA}" >&2
  exit 1
fi

rm -f /tmp/idrk-deploy-check.html
echo "==> OK: HTTP 200, profile.js?v=${DEPLOY_SHA} present. Deploy complete."
