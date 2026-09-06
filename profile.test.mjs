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
let d = Career.data;

// a balance is earned minus spent, and never negative
d.earned = 1500; d.spent = 0;
check('coins after earning 1500', Career.coins, 1500);
check('starters are always owned', Career.owns('ninja'), true);
check('unowned fighter reads false', Career.owns('ranger'), false);

// pass fighters are rewards, never merchandise
check('a pass fighter is not for sale', Career.buy('fighter','berserker'), false);
check('coins untouched by that', Career.coins, 1500);
check('every pass fighter sits on the track',
      Career.catalog.passFighters.every(f => Career.tierOf(f) !== null), true);

// the shop five are the mirror image: sold, and never on the track
const shopIds = Object.keys(Career.catalog.shopFighters);
check('there are five shop fighters', shopIds.length, 5);
check('none of them are on the track', shopIds.every(f => Career.tierOf(f) === null), true);
check('cannot afford Samurai (3000)', Career.buy('fighter','samurai'), false);
d.earned += 3000;
check('buy Samurai once affordable', Career.buy('fighter','samurai'), true);
check('coins went down by his price', Career.coins, 1500);
check('and he is playable', Career.owns('samurai'), true);
check('cannot buy him twice', Career.buy('fighter','samurai'), false);
check('a made-up fighter is refused', Career.buy('fighter','nobody'), false);
check('Berserker is the tier 5 reward', Career.tierOf('berserker'), 5);
check('Ranger is the tier 50 reward', Career.tierOf('ranger'), 50);

// claiming that tier is the only way to get him
d.elims = 5000;                                   // enough xp for the whole track
d.owned = d.owned.filter(x => x !== 'berserker');
check('cannot own him before claiming', Career.owns('berserker'), false);
const won = Career.claim(5);
check('tier 5 pays out a fighter', won && won.type, 'fighter');
check('and he is now playable', Career.owns('berserker'), true);

// skins are tied to a fighter you own
check('cannot skin an unowned fighter', Career.buy('skin','ranger:crimson'), false);
check('buy Berserker crimson (400)', Career.buy('skin','berserker:crimson'), true);
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
d.elims = 0; d.matches = 0; d.seconds = 0; d.claimed = []; d.owned = [];
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
d = Career.data;   // the merge above reassigned Career's internal data object —
                    // this stale-reference footgun is exactly what buyUpgrade's own
                    // merge test below is checking does NOT happen to real players

/* ---------- character upgrades ---------- */
console.log('\n--- upgrades ---');
d.owned = ['berserker'];
d.earned += 5000;

check('level 0 has no bonus', Career.upgradeMultiplier('berserker', 'dmg'), 1);
check('cannot upgrade a fighter you do not own', Career.buyUpgrade('ranger', 'dmg'), false);

const c1 = Career.upgradeCostFor('berserker', 'dmg');
const before2 = Career.coins;
check('first level purchase succeeds', Career.buyUpgrade('berserker', 'dmg'), true);
check('coins dropped by the first level cost', before2 - Career.coins, c1);
check('level is now 1', Career.upgradeLevel('berserker', 'dmg'), 1);
check('a 6% bonus at level 1', Math.round((Career.upgradeMultiplier('berserker','dmg')-1)*100), 6);

// costs climb, and a track cannot pass its cap
let lastCost = c1;
for(let i = 2; i <= 5; i++){
  const cost = Career.upgradeCostFor('berserker', 'dmg');
  check('level ' + i + ' costs more than the last', cost > lastCost, true);
  check('level ' + i + ' purchase succeeds', Career.buyUpgrade('berserker', 'dmg'), true);
  lastCost = cost;
}
check('maxed out at level 5', Career.upgradeLevel('berserker', 'dmg'), 5);
check('no price past the cap', Career.upgradeCostFor('berserker', 'dmg'), null);
check('buying past the cap is refused', Career.buyUpgrade('berserker', 'dmg'), false);
check('a 30% bonus at level 5', Math.round((Career.upgradeMultiplier('berserker','dmg')-1)*100), 30);

// tracks are independent, and switching fighters does not touch them
check('speed track is untouched by the damage track', Career.upgradeLevel('berserker', 'spd'), 0);
check('a different fighter starts at zero', Career.upgradeLevel('ninja', 'dmg'), 0);

// what rides along on join/setClass for a remote human
check('upgradesFor reports the levels bought', Career.upgradesFor('berserker'), {dmg:5});
check('upgradesFor is null with nothing bought', Career.upgradesFor('ninja'), null);

// merging takes the higher level per track, same rule as everything else
const cloudCopy = JSON.parse(JSON.stringify(d));
cloudCopy.upgrades.berserker = {dmg:2, spd:3};      // an older device, ahead on speed only
Clerk.user.unsafeMetadata = { arenaClash: cloudCopy };
listeners.forEach(f => f());
await wait(30);
check('kept the higher damage level after merging', Career.upgradeLevel('berserker','dmg'), 5);
check('picked up the higher speed level from the other device', Career.upgradeLevel('berserker','spd'), 3);

console.log(process.exitCode ? '\nFAILED' : '\nPASS');
