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
const SAVE_DELAY = 1500;          // ms of quiet before a write goes out

const blank = () => ({
  elims:0, deaths:0, matches:0, seconds:0, bestStreak:0,
  lastClass:null, byClass:{}
});

let data      = blank();
let streak    = 0;                // elims since the last death, this life
let clerk     = null;             // the loaded Clerk instance, once ready
let settled   = false;            // Clerk has either loaded or failed to
let saveTimer = null;
let listeners = [];

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
  ['elims','deaths','matches','seconds','bestStreak'].forEach(k => {
    out[k] = Math.max(a[k] || 0, b[k] || 0);
  });
  out.lastClass = b.lastClass || a.lastClass;
  out.byClass = {};
  Object.keys(a.byClass || {}).concat(Object.keys(b.byClass || {})).forEach(cls => {
    const x = (a.byClass || {})[cls] || {}, y = (b.byClass || {})[cls] || {};
    out.byClass[cls] = {
      elims:  Math.max(x.elims  || 0, y.elims  || 0),
      deaths: Math.max(x.deaths || 0, y.deaths || 0)
    };
  });
  return out;
}

function save(){
  writeLocal();
  clearTimeout(saveTimer);
  saveTimer = setTimeout(flush, SAVE_DELAY);
  notify();
}

/* Push straight to Clerk without waiting out the debounce. */
function flush(){
  clearTimeout(saveTimer);
  if(!clerk || !clerk.user) return Promise.resolve();
  return clerk.user.update({ unsafeMetadata: Object.assign(
    {}, clerk.user.unsafeMetadata, { [META_KEY]: data }
  )}).catch(() => {});           // offline or rate-limited: localStorage still has it
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
  const cloud = (clerk.user.unsafeMetadata || {})[META_KEY];
  data = mergeMax(data, cloud);
  writeLocal();
  flush();                            // carry anything local up to the account
  render();
}

/* ---------- public API ---------- */

const Career = {
  get data(){ return data; },
  get signedIn(){ return !!(clerk && clerk.user); },
  get clerkReady(){ return !!clerk; },
  get configured(){ return !!frontendApiFrom(CLERK_PUBLISHABLE_KEY); },

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

function render(){
  const el = document.getElementById('acct');
  if(!el) return;

  if(!Career.configured){
    el.innerHTML = '<span class="acct-note">Stats saved on this device</span>';
  } else if(!clerk){
    // once the load has settled without a Clerk, it is never coming — say so
    // rather than leaving "Connecting…" up for good
    el.innerHTML = settled
      ? '<span class="acct-note">Sign-in unavailable — stats saved on this device</span>'
      : '<span class="acct-note">Connecting…</span>';
  } else if(clerk.user){
    el.innerHTML = '<span class="acct-note">Stats synced</span><span id="acct-btn"></span>';
    clerk.mountUserButton(document.getElementById('acct-btn'));
  } else {
    el.innerHTML = '<button class="acct-in" type="button">Sign in to save stats</button>';
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

window.addEventListener('beforeunload', () => { writeLocal(); });

})();
