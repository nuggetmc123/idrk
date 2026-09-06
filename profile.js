/* ============================================================
   CAREER PROFILE
   Per-player stats that survive a refresh.

   Signed out            -> localStorage, so stats stay on this device.
   Signed in with Clerk  -> the user's unsafeMetadata, so the same stats
                            follow them to any browser they sign in on.

   unsafeMetadata is the only metadata Clerk lets the browser write, which
   is what makes this work with no server at all. It is also editable by
   anyone willing to open devtools, so treat these numbers as a personal
   record, not a leaderboard anyone could cheat their way onto.
   ============================================================ */
(function(){
"use strict";

/* Clerk dashboard -> API keys -> JavaScript. Starts with pk_test_ or pk_live_.
   Leave it empty and the game quietly keeps stats in this browser only. */
const CLERK_PUBLISHABLE_KEY = 'pk_test_dmVyaWZpZWQtbWFja2VyZWwtOTQ0Ni5jbGVyay5hY2NvdW50cy5kZXYk';

const LOCAL_KEY  = 'arena-clash-career';
const META_KEY   = 'arenaClash';

/* ---------- content ---------- */

const STARTERS = ['assassin','ninja','archer','wizard','witch'];

/* Ten fighters come from the battle pass, two per page, at tiers 5 and 10
   of each. They cannot be bought. */
const PASS_FIGHTERS = [
  'berserker', 'gunslinger', 'frostmage', 'paladin', 'bomber',
  'sniper', 'duelist', 'necromancer', 'monk', 'ranger'
];

/* Five more are sold for coins and never appear on the track, so the shop
   has something the pass will never hand over. */
const SHOP_FIGHTERS = {
  samurai:     {name:'Samurai',     price:3000},
  alchemist:   {name:'Alchemist',   price:3500},
  stormcaller: {name:'Stormcaller', price:4000},
  gladiator:   {name:'Gladiator',   price:4500},
  phantom:     {name:'Phantom',     price:6000}
};

/* A skin is a palette swap, so one definition reskins any fighter. Ids are
   "<fighter>:<skin>" so a skin is always tied to who it belongs to. */
const SKIN_SETS = [
  {id:'crimson', name:'Crimson',  price:400,  color:'#ff3b5c', dark:'#6b0d1e', light:'#ffb3c0'},
  {id:'frost',   name:'Frostbit', price:400,  color:'#7fd4ff', dark:'#12455e', light:'#d6f2ff'},
  {id:'toxic',   name:'Toxic',    price:600,  color:'#a6f03c', dark:'#2c5406', light:'#e2ffb0'},
  {id:'void',    name:'Void',     price:800,  color:'#8b5cf6', dark:'#2e1065', light:'#ddd0ff'},
  {id:'gold',    name:'Gilded',   price:1200, color:'#ffcc33', dark:'#6b4a00', light:'#fff0b8'},
  {id:'ash',     name:'Ashen',    price:600,  color:'#b8b2c6', dark:'#3a3547', light:'#ecebf2'}
];

/* Character upgrades. Every fighter has three independent tracks, five
   levels each, paid for with the same coins as everything else — spent
   permanently on that ONE fighter, so switching class keeps your
   investment intact rather than resetting it. */
const UPGRADE_TRACKS = [
  {id:'dmg', name:'Damage',   perLevel:0.06, icon:'⚔️'},   // +6% outgoing damage per level
  {id:'spd', name:'Speed',    perLevel:0.04, icon:'💨'},   // +4% move speed per level
  {id:'vit', name:'Vitality', perLevel:0.08, icon:'❤️'}    // +8% max health per level
];
const UPGRADE_MAX_LEVEL = 5;
const UPGRADE_BASE_COST = {dmg:120, spd:100, vit:110};
function upgradeCost(track, level){          // cost of buying INTO this level (1..5)
  return Math.round(UPGRADE_BASE_COST[track] * Math.pow(1.6, level - 1));
}

const HATS = [
  {id:'crown',  name:'Crown'},
  {id:'horns',  name:'Horns'},
  {id:'halo',   name:'Halo'},
  {id:'antenna',name:'Antenna'},
  {id:'top',    name:'Top Hat'}
];

/* Chests roll against the fighters you already own, so a chest can never
   hand you a skin for someone you cannot play. */
const CHESTS = [
  {id:'wooden',  name:'Wooden Chest',  coins:[80,160],   skinChance:0.35, tier:0},
  {id:'iron',    name:'Iron Chest',    coins:[180,340],  skinChance:0.50, tier:1},
  {id:'silver',  name:'Silver Chest',  coins:[320,600],  skinChance:0.65, tier:2},
  {id:'gold',    name:'Gold Chest',    coins:[600,1100], skinChance:0.80, tier:3},
  {id:'mythic',  name:'Mythic Chest',  coins:[1200,2000],skinChance:1.00, tier:4, hat:true}
];

const XP_PER_TIER = 100;
const TIERS_PER_PAGE = 10;
const PAGES = 5;
const TITLES = [
  'Rookie', 'Scrapper', 'Brawler', 'Skirmisher', 'Duelist',
  'Bladebearer', 'Marauder', 'Vanguard', 'Gladiator', 'Champion',
  'Warbringer', 'Executioner', 'Ravager', 'Warlord', 'Bloodletter',
  'Dreadnought', 'Conqueror', 'Ascendant', 'Immortal', 'Arena Legend'
];

/* The 50-tier track. Each page of ten runs the same shape — two fighters, a
   skin, a hat, a chest, a title and four coin rewards that climb with the
   page — so every page is worth finishing. */
function buildTrack(){
  const track = [];
  for(let i = 0; i < TIERS_PER_PAGE * PAGES; i++){
    const tier = i + 1;
    const page = Math.floor(i / TIERS_PER_PAGE);
    const slot = tier % TIERS_PER_PAGE;        // 1..9 then 0 for the tenth
    let r;
    if(slot === 5)      r = {type:'fighter', id:PASS_FIGHTERS[page * 2]};
    else if(slot === 0) r = {type:'fighter', id:PASS_FIGHTERS[page * 2 + 1]};
    else if(slot === 3) r = {type:'skin',    id:SKIN_SETS[page % SKIN_SETS.length].id};
    else if(slot === 7) r = {type:'hat',     id:HATS[page % HATS.length].id};
    else if(slot === 8) r = {type:'chest',   id:CHESTS[Math.min(page, CHESTS.length-1)].id};
    else if(slot === 1) r = {type:'title',   id:TITLES[page * 4]};
    else                r = {type:'coins',   amount:(page + 1) * 100 + slot * 25};
    track.push(Object.assign({tier:tier, page:page}, r));
  }
  return track;
}
const TRACK = buildTrack();

/* Coins are stored as two totals that only ever grow, never as a balance.
   The merge takes the max of every counter, and a balance would come back
   from the dead after spending on another device — earned minus spent does
   not. Every list below is grow-only for the same reason. */
const blank = () => ({
  elims:0, deaths:0, matches:0, seconds:0, bestStreak:0,
  lastClass:null, title:null, byClass:{},
  earned:0, spent:0,
  owned:[], skins:[], hats:[], chests:[], opened:[], claimed:[],
  equipSkin:{}, equipHat:null, upgrades:{}
});

const GROW_LISTS = ['owned','skins','hats','chests','opened','claimed'];

/* Derived from the counters rather than stored, so the pass can never drift
   out of step with the record it is supposed to reflect. */
function xpOf(c){
  return c.elims * 10 + c.matches * 25 + Math.floor(c.seconds / 60) * 5;
}

let data      = blank();
let streak    = 0;                // elims since the last death, this life
let clerk     = null;             // the loaded Clerk instance, once ready
let settled   = false;            // Clerk has either loaded or failed to
let listeners = [];
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
   in twice can never double a total the way summing would. */
function mergeMax(a, b){
  if(!b) return a;
  const out = Object.assign(blank(), a);
  ['elims','deaths','matches','seconds','bestStreak','earned','spent'].forEach(k => {
    out[k] = Math.max(a[k] || 0, b[k] || 0);
  });
  GROW_LISTS.forEach(k => {
    const set = {};
    (a[k] || []).concat(b[k] || []).forEach(v => { set[v] = 1; });
    out[k] = Object.keys(set);
  });
  out.equipSkin = Object.assign({}, a.equipSkin || {}, b.equipSkin || {});
  out.equipHat = b.equipHat || a.equipHat;
  out.lastClass = b.lastClass || a.lastClass;
  out.title = b.title || a.title;
  out.byClass = {};
  Object.keys(a.byClass || {}).concat(Object.keys(b.byClass || {})).forEach(cls => {
    const x = (a.byClass || {})[cls] || {}, y = (b.byClass || {})[cls] || {};
    out.byClass[cls] = {
      elims:  Math.max(x.elims  || 0, y.elims  || 0),
      deaths: Math.max(x.deaths || 0, y.deaths || 0)
    };
  });
  out.upgrades = {};
  Object.keys(a.upgrades || {}).concat(Object.keys(b.upgrades || {})).forEach(cls => {
    const x = (a.upgrades || {})[cls] || {}, y = (b.upgrades || {})[cls] || {};
    out.upgrades[cls] = {};
    UPGRADE_TRACKS.forEach(t => { out.upgrades[cls][t.id] = Math.max(x[t.id] || 0, y[t.id] || 0); });
  });
  return out;
}

/* Stable stringify: two equal records always produce the same text, whatever
   order their keys happen to be in after a round trip through Clerk. */
function canon(v){
  if(v === null || typeof v !== 'object') return JSON.stringify(v);
  if(Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  return '{' + Object.keys(v).sort().map(k => JSON.stringify(k) + ':' + canon(v[k])).join(',') + '}';
}

/* Every scoring event lands here. It only touches localStorage: a match can
   score dozens of times a minute and Clerk is not a per-event write target. */
function save(){
  writeLocal();
  notify();
}

/* Send the record up to the account. Skipped when the account already holds
   exactly this, which is what stops our own write from bouncing back through
   the Clerk listener and starting again. */
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

const Career = {
  get data(){ return data; },
  get signedIn(){ return !!(clerk && clerk.user); },
  get clerkReady(){ return !!clerk; },
  get configured(){ return !!frontendApiFrom(CLERK_PUBLISHABLE_KEY); },

  /* A name worth showing another player in a lobby. Null when signed out —
     multiplayer.js falls back to a locally-remembered guest name then. */
  get displayName(){
    if(!clerk || !clerk.user) return null;
    return clerk.user.username || clerk.user.firstName
      || (clerk.user.primaryEmailAddress && clerk.user.primaryEmailAddress.emailAddress.split('@')[0])
      || null;
  },
  get userId(){ return (clerk && clerk.user) ? clerk.user.id : null; },

  elim(cls){
    data.elims++;
    streak++;
    if(streak > data.bestStreak) data.bestStreak = streak;
    bump(cls, 'elims');
    save();
  },

  death(cls){
    data.deaths++;
    streak = 0;
    bump(cls, 'deaths');
    save();
  },

  matchStarted(cls){
    data.matches++;
    streak = 0;
    if(cls) data.lastClass = cls;
    save();
  },

  matchEnded(seconds){
    data.seconds += Math.max(0, Math.round(seconds || 0));
    streak = 0;
    save();
    flush();
  },

  /* Battle pass, all derived from the counters above. */
  get xp(){ return xpOf(data); },
  get tier(){ return Math.min(TRACK.length, Math.floor(xpOf(data) / XP_PER_TIER)); },
  get xpIntoTier(){ return xpOf(data) % XP_PER_TIER; },
  get xpPerTier(){ return XP_PER_TIER; },
  get titles(){ return TITLES.slice(); },
  get title(){ return data.title; },

  /* Equipping a title you have not reached yet is refused rather than ignored,
     so a stale menu can't hand out a locked one. */
  setTitle(name){
    const i = TITLES.indexOf(name);
    if(name !== null && (i < 0 || i + 1 > this.tier)) return false;
    data.title = name;
    save();
    flush();
    return true;
  },

  /* ---------- economy ---------- */

  get coins(){ return Math.max(0, (data.earned || 0) - (data.spent || 0)); },
  get catalog(){ return {passFighters:PASS_FIGHTERS, shopFighters:SHOP_FIGHTERS,
                         skinSets:SKIN_SETS, hats:HATS, chests:CHESTS}; },
  get track(){ return TRACK; },
  get pages(){ return PAGES; },
  get tiersPerPage(){ return TIERS_PER_PAGE; },

  owns(cls){ return STARTERS.indexOf(cls) !== -1 || (data.owned || []).indexOf(cls) !== -1; },

  /* Which tier hands over this fighter, so the menu can say where to look. */
  tierOf(cls){
    for(let i = 0; i < TRACK.length; i++)
      if(TRACK[i].type === 'fighter' && TRACK[i].id === cls) return TRACK[i].tier;
    return null;
  },
  ownsSkin(id){ return (data.skins || []).indexOf(id) !== -1; },
  ownsHat(id){ return (data.hats || []).indexOf(id) !== -1; },
  get ownedFighters(){ return STARTERS.concat(data.owned || []); },
  get equippedHat(){ return data.equipHat; },
  skinFor(cls){ return (data.equipSkin || {})[cls] || null; },

  /* Skin ids owned for one fighter, as bare set ids. */
  skinsOwnedFor(cls){
    return (data.skins || [])
      .filter(id => id.indexOf(cls + ':') === 0)
      .map(id => id.split(':')[1]);
  },

  /* ---------- upgrades ---------- */

  get upgradeTracks(){ return UPGRADE_TRACKS.slice(); },
  upgradeMaxLevel: UPGRADE_MAX_LEVEL,

  upgradeLevel(cls, track){
    return (data.upgrades[cls] && data.upgrades[cls][track]) || 0;
  },

  /* 1 = no bonus. Damage and max health read this directly; move speed the
     same way. Bots and anyone else's fighters never carry a bonus — this
     is spent on and applies to your own account's fighter only. */
  upgradeMultiplier(cls, track){
    const t = UPGRADE_TRACKS.filter(x => x.id === track)[0];
    return t ? 1 + this.upgradeLevel(cls, track) * t.perLevel : 1;
  },

  /* Coin cost of buying the NEXT level, or null once maxed. */
  upgradeCostFor(cls, track){
    const lvl = this.upgradeLevel(cls, track);
    return lvl >= UPGRADE_MAX_LEVEL ? null : upgradeCost(track, lvl + 1);
  },

  buyUpgrade(cls, track){
    if(!this.owns(cls)) return false;
    const cost = this.upgradeCostFor(cls, track);
    if(cost === null || this.coins < cost) return false;
    data.spent += cost;
    if(!data.upgrades[cls]) data.upgrades[cls] = {};
    data.upgrades[cls][track] = this.upgradeLevel(cls, track) + 1;
    save(); flush();
    return true;
  },

  /* What a remote lobby member's own upgrades look like, for the join/setClass
     payload — see multiplayer.js. Absent entirely if they have none, so the
     wire format stays small for the overwhelmingly common case. */
  upgradesFor(cls){
    const row = data.upgrades[cls];
    if(!row) return null;
    const out = {};
    let any = false;
    UPGRADE_TRACKS.forEach(t => { if(row[t.id]){ out[t.id] = row[t.id]; any = true; } });
    return any ? out : null;
  },

  /* Every purchase goes through here, so the balance can only be spent once
     and never below zero. */
  buy(kind, id){
    if(kind === 'fighter'){
      const def = SHOP_FIGHTERS[id];       // pass fighters are never for sale
      if(!def || this.owns(id) || this.coins < def.price) return false;
      data.spent += def.price;
      data.owned.push(id);
    } else if(kind === 'skin'){
      const parts = String(id).split(':');
      const set = SKIN_SETS.filter(k => k.id === parts[1])[0];
      if(!set || !this.owns(parts[0]) || this.ownsSkin(id) || this.coins < set.price) return false;
      data.spent += set.price;
      data.skins.push(id);
    } else return false;
    save(); flush();
    return true;
  },

  equipSkin(cls, skinId){
    if(skinId && !this.ownsSkin(cls + ':' + skinId)) return false;
    if(skinId) data.equipSkin[cls] = skinId; else delete data.equipSkin[cls];
    save(); flush();
    return true;
  },

  equipHat(id){
    if(id && !this.ownsHat(id)) return false;
    data.equipHat = id || null;
    save(); flush();
    return true;
  },

  /* ---------- chests ---------- */

  get unopenedChests(){
    return (data.chests || []).filter(c => (data.opened || []).indexOf(c) === -1);
  },

  /* A chest id carries its kind: "<kind>:<where it came from>". */
  chestKind(chestId){
    const k = String(chestId).split(':')[0];
    return CHESTS.filter(c => c.id === k)[0] || CHESTS[0];
  },

  /* Rolls only against fighters you own, so a chest never grants a skin for
     someone unplayable. Falls back to coins when there is nothing left to win. */
  openChest(chestId){
    if((data.opened || []).indexOf(chestId) !== -1) return null;
    if((data.chests || []).indexOf(chestId) === -1) return null;
    const kind = this.chestKind(chestId);
    data.opened.push(chestId);

    const pool = [];
    this.ownedFighters.forEach(cls => SKIN_SETS.forEach(sk => {
      if(!this.ownsSkin(cls + ':' + sk.id)) pool.push(cls + ':' + sk.id);
    }));
    const unhad = HATS.filter(h => !this.ownsHat(h.id));

    let out;
    if(kind.hat && unhad.length && Math.random() < 0.4){
      const h = unhad[Math.floor(Math.random() * unhad.length)];
      data.hats.push(h.id);
      out = {type:'hat', id:h.id, name:h.name};
    } else if(pool.length && Math.random() < kind.skinChance){
      const pick = pool[Math.floor(Math.random() * pool.length)];
      data.skins.push(pick);
      out = {type:'skin', id:pick};
    } else {
      const lo = kind.coins[0], hi = kind.coins[1];
      const amt = lo + Math.floor(Math.random() * (hi - lo + 1));
      data.earned += amt;
      out = {type:'coins', amount:amt};
    }
    save(); flush();
    return out;
  },

  /* ---------- battle pass ---------- */

  claimed(tier){ return (data.claimed || []).indexOf(String(tier)) !== -1; },

  /* Refuses a tier you have not reached, so a stale menu cannot pay out. */
  claim(tier){
    const row = TRACK[tier - 1];
    if(!row || tier > this.tier || this.claimed(tier)) return null;
    data.claimed.push(String(tier));
    if(row.type === 'coins')        data.earned += row.amount;
    else if(row.type === 'fighter'){ if(!this.owns(row.id)) data.owned.push(row.id); }
    else if(row.type === 'skin'){                       // a pass skin fits every fighter
      this.ownedFighters.forEach(cls => {
        const sid = cls + ':' + row.id;
        if(!this.ownsSkin(sid)) data.skins.push(sid);
      });
    }
    else if(row.type === 'hat'){ if(!this.ownsHat(row.id)) data.hats.push(row.id); }
    else if(row.type === 'chest') data.chests.push(row.id + ':t' + tier);
    else if(row.type === 'title') data.title = row.id;
    save(); flush();
    return row;
  },

  get unclaimedCount(){
    let n = 0;
    for(let t = 1; t <= this.tier; t++) if(!this.claimed(t)) n++;
    return n;
  },

  signIn(){ if(clerk) clerk.openSignIn(); },
  onChange(fn){ listeners.push(fn); },
  flush: flush
};

function bump(cls, field){
  if(!cls) return;
  const row = data.byClass[cls] || (data.byClass[cls] = {elims:0, deaths:0});
  row[field]++;
}

function notify(){ listeners.forEach(fn => { try{ fn(data); }catch(e){} }); }

/* ---------- account bar ---------- */

function titleChip(){
  return data.title ? '<span class="acct-title">' + data.title + '</span>' : '';
}

function render(){
  const el = document.getElementById('acct');
  if(!el) return;

  if(!Career.configured){
    el.innerHTML = titleChip() + '<span class="acct-note">Stats saved on this device</span>';
  } else if(!clerk){
    // once the load has settled without a Clerk, it is never coming — say so
    // rather than leaving "Connecting…" up for good
    el.innerHTML = titleChip() + (settled
      ? '<span class="acct-note">Sign-in unavailable — stats saved on this device</span>'
      : '<span class="acct-note">Connecting…</span>');
  } else if(clerk.user){
    el.innerHTML = titleChip() + '<span class="acct-note">Stats synced</span><span id="acct-btn"></span>';
    clerk.mountUserButton(document.getElementById('acct-btn'));
  } else {
    el.innerHTML = titleChip() + '<button class="acct-in" type="button">Sign in to save stats</button>';
    el.querySelector('.acct-in').addEventListener('click', () => Career.signIn());
  }
  notify();
}

/* ---------- boot ---------- */

data = readLocal() || blank();
window.Career = Career;
render();

loadClerk().then(c => {
  clerk = c;
  settled = true;
  if(clerk) clerk.addListener(adoptUser);
  adoptUser();
});

/* Sync points: leaving a match, and the tab going away. Both are moments
   the player stops scoring, so there is nothing to batch up behind them. */
window.addEventListener('pagehide', () => { writeLocal(); flush(); });
document.addEventListener('visibilitychange', () => {
  if(document.visibilityState === 'hidden'){ writeLocal(); flush(); }
});

})();
