/* Regression test for the Clerk write loop.

   Clerk notifies its listeners after every user.update(). An earlier version
   flushed from inside that listener, so each write triggered another one and
   the Frontend API started answering 429. This drives the real profile.js
   against a fake Clerk with the same notify-on-write behaviour and fails if
   the writes ever run away.

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
  get localStorage(){ return global.localStorage; }
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
store['arena-clash-career'] = JSON.stringify({
  elims:7, deaths:2, matches:1, seconds:90, bestStreak:4,
  lastClass:'archer', byClass:{archer:{elims:7,deaths:2}}
});

eval(fs.readFileSync('profile.js','utf8'));
const Career = global.window.Career;

const wait = ms => new Promise(r => setTimeout(r, ms));

await wait(60);
console.log('after sign-in adopt   -> writes:', updates, '(expect 1: local progress pushed up)');

Career.matchStarted('ninja');
for(let i=0;i<25;i++) Career.elim('ninja');
Career.death('ninja');
await wait(60);
console.log('after 26 score events -> writes:', updates, '(expect 1: no per-event writes)');

Career.matchEnded(240);
await wait(120);
console.log('after match end       -> writes:', updates, '(expect 2: one sync at match end)');

await wait(400);
console.log('after settling        -> writes:', updates, '(expect 2: loop did not restart)');
console.log('stored elims:', JSON.parse(store['arena-clash-career']).elims, '(expect 32)');
console.log('account elims:', Clerk.user.unsafeMetadata.arenaClash.elims, '(expect 32)');

// the pass must never hand out a tier the player has not reached
const reached = Career.titles[Career.tier - 1];
const locked  = Career.titles[Career.tier];
console.log('\ntier:', Career.tier, '| xp:', Career.xp);
console.log('equip reached title', JSON.stringify(reached), '->', Career.setTitle(reached), '(expect true)');
console.log('equip locked title ', JSON.stringify(locked),  '->', Career.setTitle(locked),  '(expect false)');
if(Career.setTitle(locked) !== false || Career.title !== reached){
  console.error('FAIL: a locked title was equippable'); process.exit(1);
}

if(updates !== 3) { console.error('FAIL: expected 3 writes, got ' + updates); process.exit(1); }
if(JSON.parse(store['arena-clash-career']).elims !== 32) { console.error('FAIL: local total wrong'); process.exit(1); }
if(Clerk.user.unsafeMetadata.arenaClash.elims !== 32) { console.error('FAIL: account total wrong'); process.exit(1); }

/* ---------- economy ---------- */
function check(label, got, want){
  const ok = JSON.stringify(got) === JSON.stringify(want);
  console.log((ok ? '  ok  ' : '  FAIL') + '  ' + label + '  -> ' + JSON.stringify(got));
  if(!ok){ console.error('       wanted ' + JSON.stringify(want)); process.exitCode = 1; }
}

console.log('\n--- economy ---');
const d = Career.data;

// a balance is earned minus spent, and never negative
d.earned = 1500; d.spent = 0;
check('coins after earning 1500', Career.coins, 1500);
check('buy Berserker (1200)', Career.buy('fighter','berserker'), true);
check('coins after the purchase', Career.coins, 300);
check('now owns Berserker', Career.owns('berserker'), true);
check('cannot buy it twice', Career.buy('fighter','berserker'), false);
check('cannot afford Frostmage (1500)', Career.buy('fighter','frostmage'), false);
check('coins unchanged by refusals', Career.coins, 300);
check('starters are always owned', Career.owns('ninja'), true);
check('unowned fighter reads false', Career.owns('ranger'), false);

// skins are tied to a fighter you own
check('cannot skin an unowned fighter', Career.buy('skin','ranger:crimson'), false);
check('buy Berserker crimson (400)', Career.buy('skin','berserker:crimson'), false); // only 300 left
d.earned += 1000;
check('buy it once affordable', Career.buy('skin','berserker:crimson'), true);
check('equip a skin you own', Career.equipSkin('berserker','crimson'), true);
check('equip one you do not', Career.equipSkin('berserker','gold'), false);
check('equipped skin reads back', Career.skinFor('berserker'), 'crimson');

// chests only roll skins for fighters you own
d.chests = []; d.opened = [];
for(let i=0;i<40;i++) d.chests.push('silver:t' + i);
const owned = Career.ownedFighters.slice();
let skinsWon = 0, strays = 0, kinds = {};
for(let i=0;i<40;i++){
  const r = Career.openChest('silver:t' + i);
  kinds[r.type] = (kinds[r.type] || 0) + 1;
  if(r.type === 'skin'){
    skinsWon++;
    if(owned.indexOf(r.id.split(':')[0]) === -1) strays++;
  }
}
console.log('  40 chests gave:', JSON.stringify(kinds));
check('every chest skin was for an owned fighter', strays, 0);
check('chests did award skins', skinsWon > 0, true);
check('a chest cannot be opened twice', Career.openChest('silver:t0'), null);
check('a chest you do not have', Career.openChest('gold:t99'), null);

// the pass pays out only tiers actually reached
d.elims = 0; d.matches = 0; d.seconds = 0; d.claimed = [];
check('tier with no xp', Career.tier, 0);
check('cannot claim tier 1 yet', Career.claim(1), null);
d.elims = 5000;                                  // 50000 xp -> past the end of the track
check('tier is capped at the track length', Career.tier, 50);
const before = Career.coins;
const row = Career.claim(1);
check('claiming tier 1 returns its reward', row !== null, true);
check('claiming it again is refused', Career.claim(1), null);
check('a tier beyond the track', Career.claim(51), null);
if(row.type === 'coins') check('coins went up by the reward', Career.coins - before, row.amount);

// spending survives a merge with an older cloud copy
d.earned = 2000; d.spent = 800;
const stale = JSON.parse(JSON.stringify(d)); stale.spent = 0;   // cloud never saw the spend
Clerk.user.unsafeMetadata = { arenaClash: stale };
listeners.forEach(f => f());
await wait(30);
check('spent coins do not come back after a merge', Career.coins, 1200);

console.log(process.exitCode ? '\nFAILED' : '\nPASS');
