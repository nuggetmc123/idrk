# Nullbound name server

A tiny free Cloudflare Worker that makes player names unique: once someone has a name, nobody else can pick it.
Claims are signed with each player's secret key (the same one behind their Player ID), so a name can't be stolen.

## Turn it on (about 2 minutes, free)

```sh
cd Nullbound/Server
npx wrangler login     # opens a browser once
npx wrangler deploy
```

The last line shows its address, like `https://nullbound-names.<you>.workers.dev`. Put it in
`Nullbound/Assets/Nullbound/Resources/moderation.json` as `"nameServer"`, raise `"version"` by one, and push.
Every headset picks it up the next time the game starts (no new APK needed).

Until then, names are still checked for rude words and moderators' names, but two people could pick the same one.

Test locally without deploying: `npx wrangler dev --local`.
