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

if(updates !== 2) { console.error('FAIL: expected 2 writes, got ' + updates); process.exit(1); }
if(JSON.parse(store['arena-clash-career']).elims !== 32) { console.error('FAIL: local total wrong'); process.exit(1); }
if(Clerk.user.unsafeMetadata.arenaClash.elims !== 32) { console.error('FAIL: account total wrong'); process.exit(1); }
console.log('\nPASS');
