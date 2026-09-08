/* Regression test for the Clerk write loop, adapted for the lemonade
   stand's save schema.

   Clerk notifies its listeners after every user.update(). An earlier
   version of this pattern (on a previous game in this repo) flushed from
   inside that listener, so each write triggered another one and the
   Frontend API started answering 429. This drives the real profile.js
   against a fake Clerk with the same notify-on-write behaviour and
   throws (crashing the test with a non-zero exit) if the writes ever
   run away.

   It also exercises the merge math (max-of-counters, union-of-lists,
   min-of-fastest-time) and confirms which operations are "scoring events"
   (localStorage only) versus "checkpoints" (also sync to the account) —
   see the long comment over flush() in profile.js.

   Run it with:  node profile.test.mjs   */
import fs from 'node:fs';

let updates = 0, store = {};
const listeners = [];

const Clerk = {
  load: () => Promise.resolve(),
  addListener: fn => listeners.push(fn),
  openSignIn(){},
  user: {
    unsafeMetadata: {},
    update(patch){
      updates++;
      if(updates > 50) throw new Error('RUNAWAY LOOP: ' + updates + ' writes');
      Clerk.user.unsafeMetadata = patch.unsafeMetadata;
      return Promise.resolve().then(() => listeners.forEach(f => f()));
    }
  }
};

const scripts = [];
global.window = {
  addEventListener(){}, Clerk,
  get localStorage(){ return global.localStorage; },
  /* A tiny stand-in for lemonade-data.js — just enough shape for
     profile.js's location/upgrade/achievement lookups to work. */
  GAME_DATA: {
    locationById: id => ({beach:{id:'beach', unlockCost:100, secretRecipe:'mermaidsKiss'}})[id],
    UPGRADE_TRACKS: [{id:'speed', maxLevel:2, base:50}],
    upgradeCost: (track, level) => 50 * level,
    ACHIEVEMENTS: [
      {id:'first_sale', reward:20, check: s => s.served >= 1},
      {id:'served_5',   reward:50, check: s => s.served >= 5}
    ]
  }
};
global.document = {
  head:{ appendChild(el){ scripts.push(el); setTimeout(() => el.onload(), 0); } },
  createElement: () => ({ setAttribute(){}, set onload(f){ this._l = f; }, get onload(){ return this._l; } }),
  getElementById: () => null,          // no DOM: render() bails out early
  addEventListener(){}, visibilityState:'visible'
};
global.localStorage = {
  getItem: k => (k in store ? store[k] : null),
  setItem: (k,v) => { store[k] = String(v); },
  removeItem: k => { delete store[k]; }
};
global.atob = s => Buffer.from(s, 'base64').toString('binary');

// pretend this browser already has some signed-out progress to carry up
store['lemonade-stand-save'] = JSON.stringify({
  earned:40, spent:0, unlockedLocations:['neighborhood'],
  unlockedRecipes:[], rareFound:[], achievements:[], purchasedUpgrades:{},
  served:2, failed:1, launched:1, bestCombo:2, bestPerfectStreak:0,
  fastestServeMs:0, yetiServed:0, playedMultiplayer:0, lastLocation:'neighborhood'
});

eval(fs.readFileSync('profile.js','utf8'));
const Save = global.window.Save;

const wait = ms => new Promise(r => setTimeout(r, ms));

await wait(60);
console.log('after sign-in adopt        -> writes:', updates, '(expect 1: local progress pushed up)');

// Scoring events touch localStorage only — no network write — UNLESS one
// crosses an achievement threshold, which is itself a checkpoint worth
// syncing immediately. served goes 2 -> 3 here, past "first_sale" (>=1
// already true before this call from the seeded save) — served_5 is not
// yet reached, so this alone shouldn't double-fire first_sale (grow-only
// achievements list guards against re-earning it).
Save.completeOrder({pay: 12, comboAfter: 3, ms: 4200});
await wait(60);
console.log('after 1 completed order    -> writes:', updates, '(expect 2: first_sale achievement is a checkpoint)');

Save.completeOrder({pay: 15, comboAfter: 4, ms: 3900});
Save.recordFail();
await wait(60);
console.log('after more scoring events  -> writes:', updates, '(expect 2: no new achievement, no extra writes)');

// Two more completed orders push served from 4 to 6, crossing 5 on the
// way — "served_5" fires on the first of these two, not the second.
Save.completeOrder({pay: 10, comboAfter: 0, ms: 6000});
Save.completeOrder({pay: 10, comboAfter: 1, ms: 5000});
await wait(60);
console.log('after reaching served=6    -> writes:', updates, '(expect 3: served_5 achievement is a checkpoint, fires once)');

// Unlocking a location and buying an upgrade are explicit checkpoints.
const unlockedOk = Save.unlockLocation('beach');
await wait(60);
console.log('unlockLocation("beach")    ->', unlockedOk, '| writes:', updates, '(expect true, 4)');

const boughtOk = Save.buyUpgrade('speed');
await wait(60);
console.log('buyUpgrade("speed")        ->', boughtOk, '| writes:', updates, '(expect true, 5)');

await wait(400);
console.log('after settling             -> writes:', updates, '(expect 5: loop did not restart)');

const final = JSON.parse(store['lemonade-stand-save']);
console.log('\nfinal served:', final.served, '(expect 6)');
console.log('final coins:', Save.coins, '(expect 40+12+15+10+10+20+50-100-50 = 7)');
console.log('achievements:', final.achievements, '(expect first_sale, served_5)');
console.log('account coins match local:', canon(Clerk.user.unsafeMetadata.lemonadeStand) === canon(final));

function canon(v){
  if(v === null || typeof v !== 'object') return JSON.stringify(v);
  if(Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  return '{' + Object.keys(v).sort().map(k => JSON.stringify(k) + ':' + canon(v[k])).join(',') + '}';
}
