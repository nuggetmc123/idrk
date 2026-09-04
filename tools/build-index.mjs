#!/usr/bin/env node
// Scans games/ and writes games.json, which index.html renders.
// Supported layouts:
//   games/<name>.html            -> single-file game
//   games/<name>/index.html      -> game with its own assets
import { readdir, readFile, stat, writeFile } from "node:fs/promises";
import { join } from "node:path";

const GAMES_DIR = "games";
const OUT = "games.json";

function tag(html, re) {
  const m = html.match(re);
  return m ? m[1].replace(/\s+/g, " ").trim() : "";
}

function titleFromFile(path) {
  const base = path.split("/").pop().replace(/\.html$/i, "");
  const name = base === "index" ? path.split("/").slice(-2)[0] : base;
  return name
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

async function describe(path) {
  const html = await readFile(path, "utf8");
  const info = await stat(path);
  return {
    path,
    title: tag(html, /<title[^>]*>([\s\S]*?)<\/title>/i) || titleFromFile(path),
    description: tag(html, /<meta[^>]+name=["']description["'][^>]+content=["']([^"']*)["']/i),
    updated: info.mtime.toISOString().slice(0, 10),
  };
}

const found = [];
let entries = [];
try {
  entries = await readdir(GAMES_DIR, { withFileTypes: true });
} catch {
  console.warn(`no ${GAMES_DIR}/ directory yet`);
}

for (const entry of entries) {
  if (entry.name.startsWith(".")) continue;
  if (entry.isFile() && /\.html?$/i.test(entry.name)) {
    found.push(await describe(join(GAMES_DIR, entry.name)));
  } else if (entry.isDirectory()) {
    const index = join(GAMES_DIR, entry.name, "index.html");
    try {
      await stat(index);
      found.push(await describe(index));
    } catch {
      console.warn(`skipping ${entry.name}/ — no index.html`);
    }
  }
}

found.sort((a, b) => a.title.localeCompare(b.title));
await writeFile(OUT, JSON.stringify({ games: found }, null, 2) + "\n");
console.log(`${OUT}: ${found.length} game(s)`);
for (const g of found) console.log(`  ${g.title} -> ${g.path}`);
