/* ============================================================
   SAVE FILE
   Per-player progress that survives a refresh: coins, unlocked
   locations, secret recipes found, upgrades bought, and achievements.

   Signed out            -> localStorage, so progress stays on this device.
   Signed in with Clerk  -> the user's unsafeMetadata, so the same save
                            follows them to any browser they sign in on.

   unsafeMetadata is the only metadata Clerk lets the browser write, which
   is what makes this work with no server at all. It is also editable by
   anyone willing to open devtools, so treat this as a personal save file,
   not a leaderboard anyone could cheat their way onto.

   This keeps the same architecture the previous game on this repo used
   for its career record (grow-only counters merged by max, grow-only
   lists merged by union, a canonical-JSON guard against Clerk's
   notify-after-every-write turning into an infinite loop) — it's a
   proven shape, just pointed at a different save shape.
   ============================================================ */
(function(){
"use strict";

/* Clerk dashboard -> API keys -> JavaScript. Starts with pk_test_ or pk_live_.
   Leave it empty and the game quietly keeps progress in this browser only.
   (Reusing the same development instance this repo already had configured
   and allow-listed for this origin — Clerk itself doesn't care what game
   is asking it to remember a user.) */
const CLERK_PUBLISHABLE_KEY = 'pk_test_dmVyaWZpZWQtbWFja2VyZWwtOTQ0Ni5jbGVyay5hY2NvdW50cy5kZXYk';

const LOCAL_KEY = 'lemonade-stand-save';
const META_KEY  = 'lemonadeStand';

/* Achievement definitions live in lemonade-data.js, which loads first —
   but this file is also driven standalone by profile.test.mjs without it,
   so every lookup falls back to an empty list rather than throwing. */
function achievementDefs(){ return (window.GAME_DATA && window.GAME_DATA.ACHIEVEMENTS) || []; }

/* Coins are stored as two grow-only totals rather than a balance — the
   merge takes the max of each counter, so coins spent on one device can
   never come back from the dead when it merges with another. Every list
   below (unlockedLocations, unlockedRecipes, rareFound, achievements) is
   grow-only for the same reason and merges as a union. */
const blank = () => ({
  earned:0, spent:0,
  unlockedLocations:['neighborhood'],
  unlockedRecipes:[],
  rareFound:[],
  achievements:[],
  purchasedUpgrades:{},
  served:0, failed:0, launched:0,
  bestCombo:0, bestPerfectStreak:0,
  fastestServeMs:0,          // 0 means "never timed one" — see mergeMax
  yetiServed:0, playedMultiplayer:0,
  lastLocation:'neighborhood'
});

const MAX_FIELDS = ['earned','spent','served','failed','launched',
  'bestCombo','bestPerfectStreak','yetiServed','playedMultiplayer'];
const GROW_LISTS = ['unlockedLocations','unlockedRecipes','rareFound','achievements'];

let data       = blank();
let pending    = [];              // achievement objects earned this page load, not yet shown
let clerk      = null;            // the loaded Clerk instance, once ready
let settled    = false;           // Clerk has either loaded or failed to
let listeners  = [];
let pushing    = false;           // a write to Clerk is in flight
let lastPushed = null;            // canonical form of what the account already holds

/* ---------- storage ---------- */

function readLocal(){
  try{
    const raw = localStorage.getItem(LOCAL_KEY);
    return raw ? Object.assign(blank(), JSON.parse(raw)) : null;
  }catch(e){ return null; }         // private mode, blocked storage, bad JSON
}

function writeLocal(){
  try{ localStorage.setItem(LOCAL_KEY, JSON.stringify(data)); }catch(e){}
}

/* Counters only ever go up, so merging by max is safe to repeat — signing
   in twice can never double a total the way summing would. fastestServeMs
   is the one field where smaller is better, and 0 means "unset" rather
   than "instant", so it gets its own rule instead of joining MAX_FIELDS. */
function mergeMax(a, b){
  if(!b) return a;
  const out = Object.assign(blank(), a);
  MAX_FIELDS.forEach(k => { out[k] = Math.max(a[k] || 0, b[k] || 0); });
  GROW_LISTS.forEach(k => {
    const set = {};
    (a[k] || []).concat(b[k] || []).forEach(v => { set[v] = 1; });
    out[k] = Object.keys(set);
  });
  const av = a.fastestServeMs || 0, bv = b.fastestServeMs || 0;
  out.fastestServeMs = (av && bv) ? Math.min(av, bv) : (av || bv);
  out.purchasedUpgrades = {};
  Object.keys(a.purchasedUpgrades || {}).concat(Object.keys(b.purchasedUpgrades || {})).forEach(k => {
    out.purchasedUpgrades[k] = Math.max((a.purchasedUpgrades || {})[k] || 0, (b.purchasedUpgrades || {})[k] || 0);
  });
  out.lastLocation = b.lastLocation || a.lastLocation;
  return out;
}

/* Stable stringify: two equal records always produce the same text, whatever
   order their keys happen to be in after a round trip through Clerk. */
function canon(v){
  if(v === null || typeof v !== 'object') return JSON.stringify(v);
  if(Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  return '{' + Object.keys(v).sort().map(k => JSON.stringify(k) + ':' + canon(v[k])).join(',') + '}';
}

/* Every scoring event lands here. It only touches localStorage: a busy
   stand can score dozens of times a minute and Clerk is not a per-event
   write target. */
function save(){
  writeLocal();
  notify();
}

/* Send the record up to the account. Skipped when the account already holds
   exactly this, which is what stops our own write from bouncing back through
   the Clerk listener and starting again. Reserved for real checkpoints
   (a purchase, an unlock, an achievement, leaving the stand) rather than
   every mix and pour. */
function flush(){
  if(!clerk || !clerk.user) return Promise.resolve();
  const payload = canon(data);
  if(payload === lastPushed || pushing) return Promise.resolve();
  pushing = true;
  lastPushed = payload;
  return clerk.user.update({ unsafeMetadata: Object.assign(
    {}, clerk.user.unsafeMetadata, { [META_KEY]: data }
  )}).catch(() => { lastPushed = null; })   // failed: allow a later retry
    .then(() => { pushing = false; }, () => { pushing = false; });
}

/* Runs after any mutation that could satisfy a new achievement. Awards
   the reward straight into `earned`, queues it so the UI can toast it,
   and treats "an achievement just fired" as worth an immediate sync. */
function runAchievements(){
  let any = false;
  achievementDefs().forEach(a => {
    if(data.achievements.indexOf(a.id) !== -1) return;
    let earned;
    try{ earned = !!a.check(data); }catch(e){ earned = false; }
    if(!earned) return;
    data.achievements.push(a.id);
    data.earned += a.reward || 0;
    pending.push(a);
    any = true;
  });
  if(any){ save(); flush(); }
}

/* ---------- Clerk ---------- */

/* A publishable key is base64 of the instance's frontend API host, which is
   also where Clerk serves its own script from. One value configures both. */
function frontendApiFrom(key){
  const m = /^pk_(?:test|live)_(.+)$/.exec(key || '');
  if(!m) return null;
  try{
    const host = atob(m[1]).replace(/\$+$/, '');
    return /^[a-z0-9.-]+$/i.test(host) ? host : null;
  }catch(e){ return null; }
}

function loadClerk(){
  const api = frontendApiFrom(CLERK_PUBLISHABLE_KEY);
  if(!api) return Promise.resolve(null);
  return new Promise(resolve => {
    const s = document.createElement('script');
    s.async = true;
    s.crossOrigin = 'anonymous';
    s.setAttribute('data-clerk-publishable-key', CLERK_PUBLISHABLE_KEY);
    s.src = 'https://' + api + '/npm/@clerk/clerk-js@latest/dist/clerk.browser.js';
    s.onload = () => window.Clerk.load()
      .then(() => resolve(window.Clerk))
      .catch(() => resolve(null));
    s.onerror = () => resolve(null);   // blocked or offline: stay local-only
    document.head.appendChild(s);
  });
}

/* Called on load and again every time the signed-in user changes. */
function adoptUser(){
  if(!clerk || !clerk.user){ render(); return; }
  const cloud = (clerk.user.unsafeMetadata || {})[META_KEY] || null;
  data = mergeMax(data, cloud);
  writeLocal();

  if(canon(data) === canon(cloud)){
    lastPushed = canon(data);         // account is already current — nothing to send
  } else {
    flush();                          // local play happened signed out; carry it up
  }
  render();
}

/* ---------- public API ---------- */

const Save = {
  get data(){ return data; },
  get signedIn(){ return !!(clerk && clerk.user); },
  get clerkReady(){ return !!clerk; },
  get configured(){ return !!frontendApiFrom(CLERK_PUBLISHABLE_KEY); },

  /* A name worth showing another player in a lobby. Null when signed out —
     lemonade-net.js falls back to a locally-remembered guest name then. */
  get displayName(){
    if(!clerk || !clerk.user) return null;
    return clerk.user.username || clerk.user.firstName
      || (clerk.user.primaryEmailAddress && clerk.user.primaryEmailAddress.emailAddress.split('@')[0])
      || null;
  },
  get userId(){ return (clerk && clerk.user) ? clerk.user.id : null; },

  /* ---------- economy ---------- */

  get coins(){ return Math.max(0, (data.earned || 0) - (data.spent || 0)); },

  isLocationUnlocked(id){ return (data.unlockedLocations || []).indexOf(id) !== -1; },

  unlockLocation(id){
    const loc = window.GAME_DATA && window.GAME_DATA.locationById(id);
    if(!loc || this.isLocationUnlocked(id) || this.coins < loc.unlockCost) return false;
    data.spent += loc.unlockCost;
    data.unlockedLocations.push(id);
    save(); flush(); runAchievements();
    return true;
  },

  /* ---------- upgrades ---------- */

  upgradeLevel(trackId){ return data.purchasedUpgrades[trackId] || 0; },

  /* Coin cost of buying the NEXT level, or null once maxed / unknown track. */
  upgradeCostFor(trackId){
    const tracks = (window.GAME_DATA && window.GAME_DATA.UPGRADE_TRACKS) || [];
    const track = tracks.filter(t => t.id === trackId)[0];
    if(!track) return null;
    const lvl = this.upgradeLevel(trackId);
    return lvl >= track.maxLevel ? null : window.GAME_DATA.upgradeCost(trackId, lvl + 1);
  },

  buyUpgrade(trackId){
    const cost = this.upgradeCostFor(trackId);
    if(cost === null || this.coins < cost) return false;
    data.spent += cost;
    data.purchasedUpgrades[trackId] = this.upgradeLevel(trackId) + 1;
    save(); flush(); runAchievements();
    return true;
  },

  /* ---------- recipes & rare ingredients ---------- */

  hasSecretRecipe(id){ return (data.unlockedRecipes || []).indexOf(id) !== -1; },
  get rareFound(){ return data.rareFound.slice(); },

  /* A rare ingredient drop, tied to whichever location it dropped at —
     finding it also unlocks that location's secret recipe on the spot,
     so there's no separate "spend coins to learn it" step; the surprise
     of the drop IS the unlock. */
  findRareIngredient(locationId, ingredientId){
    let changed = false;
    if(data.rareFound.indexOf(ingredientId) === -1){ data.rareFound.push(ingredientId); changed = true; }
    const loc = window.GAME_DATA && window.GAME_DATA.locationById(locationId);
    if(loc && loc.secretRecipe && data.unlockedRecipes.indexOf(loc.secretRecipe) === -1){
      data.unlockedRecipes.push(loc.secretRecipe);
      changed = true;
    }
    if(changed){ save(); flush(); runAchievements(); }
    return changed;
  },

  /* ---------- scoring ---------- */

  /* comboAfter/perfectStreakAfter are the streak values AFTER this serve,
     computed by the game loop (which is the one thing tracking "in a row"
     live) — this just remembers the best one ever seen. */
  completeOrder(o){
    o = o || {};
    data.served++;
    data.earned += Math.max(0, Math.round(o.pay || 0));
    if((o.comboAfter || 0) > data.bestCombo) data.bestCombo = o.comboAfter;
    if((o.perfectStreakAfter || 0) > data.bestPerfectStreak) data.bestPerfectStreak = o.perfectStreakAfter;
    if(o.ms > 0 && (!data.fastestServeMs || o.ms < data.fastestServeMs)) data.fastestServeMs = o.ms;
    if(o.yeti) data.yetiServed = 1;
    save();
    runAchievements();
  },

  /* Every mess-up — wrong drink, spill, or the patience timer hitting
     zero — ends the same way: launched into orbit. */
  recordFail(){
    data.failed++;
    data.launched++;
    save();
    runAchievements();
  },

  markMultiplayerPlayed(){
    if(data.playedMultiplayer) return;
    data.playedMultiplayer = 1;
    save();
    runAchievements();
  },

  setLastLocation(id){ data.lastLocation = id; save(); },

  /* Drains achievements earned since the last drain, for the UI to toast
     one at a time without re-showing anything already shown this session. */
  drainNewAchievements(){ const out = pending; pending = []; return out; },

  /* Explicit checkpoint — call when leaving the stand for the menu, same
     spirit as the old game flushing at match end. */
  flushNow(){ writeLocal(); return flush(); },

  signIn(){ if(clerk) clerk.openSignIn(); },
  onChange(fn){ listeners.push(fn); }
};

function notify(){ listeners.forEach(fn => { try{ fn(data); }catch(e){} }); }

/* ---------- account bar ---------- */

function render(){
  const el = document.getElementById('acct');
  if(!el) return;

  if(!Save.configured){
    el.innerHTML = '<span class="acct-note">Progress saved on this device</span>';
  } else if(!clerk){
    // once the load has settled without a Clerk, it is never coming — say so
    // rather than leaving "Connecting…" up for good
    el.innerHTML = settled
      ? '<span class="acct-note">Sign-in unavailable — progress saved on this device</span>'
      : '<span class="acct-note">Connecting…</span>';
  } else if(clerk.user){
    el.innerHTML = '<span class="acct-note">Progress synced</span><span id="acct-btn"></span>';
    clerk.mountUserButton(document.getElementById('acct-btn'));
  } else {
    el.innerHTML = '<button class="acct-in" type="button">Sign in to save progress</button>';
    el.querySelector('.acct-in').addEventListener('click', () => Save.signIn());
  }
  notify();
}

/* ---------- boot ---------- */

data = readLocal() || blank();
window.Save = Save;
render();

loadClerk().then(c => {
  clerk = c;
  settled = true;
  if(clerk) clerk.addListener(adoptUser);
  adoptUser();
});

/* Sync points: leaving the stand, and the tab going away. Both are moments
   the player stops scoring, so there is nothing to batch up behind them. */
window.addEventListener('pagehide', () => { writeLocal(); flush(); });
document.addEventListener('visibilitychange', () => {
  if(document.visibilityState === 'hidden'){ writeLocal(); flush(); }
});

})();
