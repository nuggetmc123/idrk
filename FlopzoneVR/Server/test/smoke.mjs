// End-to-end smoke test against a running local server:
//   npx wrangler dev        (in another terminal)
//   node test/smoke.mjs     (ADMIN_KEY in .dev.vars must be "testkey")
import assert from 'node:assert/strict';
import { checkChat, checkName } from '../src/filter.js';

const URL_BASE = process.env.FZ_URL || 'ws://127.0.0.1:8787';
const HTTP_BASE = URL_BASE.replace(/^ws/, 'http');

// ---- word filter ----
for (const ok of ['fuck yeah', 'oh shit', 'damn it', 'that was fucking sick', 'bullshit', 'nice pistol', 'great class', 'grab the basement flag', 'gg', 'peacock'])
  assert.equal(checkChat(ok), null, `should allow: ${ok}`);
for (const bad of ['you bitch', 'n i g g e r', 'kys', 'dumbass', 'shitass', 'go kill yourself', 'faggot', 'what a retard', 'b1tch', 'asshole'])
  assert.notEqual(checkChat(bad), null, `should block: ${bad}`);
for (const ok of ['Flopper', 'Big_Bass', 'Pistol_Pete', 'Sniper99'])
  assert.equal(checkName(ok), null, `name ok: ${ok}`);
for (const bad of ['fuckboy', 'ShitLord', 'damnit', 'Big_Ass', 'xX_n1gga_Xx', 'ab', 'has space', 'admin'])
  assert.notEqual(checkName(bad), null, `name bad: ${bad}`);
console.log('filter ok');

// ---- live server ----
const sleep = ms => new Promise(r => setTimeout(r, ms));

class Client {
  constructor(label) { this.label = label; this.msgs = []; this.waiters = []; }
  async open(hello = {}) {
    this.ws = new WebSocket(URL_BASE + '/ws');
    await new Promise((res, rej) => { this.ws.onopen = res; this.ws.onerror = rej; });
    this.ws.onmessage = ev => {
      const m = JSON.parse(ev.data);
      this.msgs.push(m);
      for (const w of this.waiters.slice()) if (w.pred(m)) { this.waiters.splice(this.waiters.indexOf(w), 1); w.res(m); }
    };
    this.send({ t: 'hello', ver: 1, ...hello });
    const w = await this.wait(m => m.t === 'welcome' || m.t === 'banned');
    if (w.t === 'welcome') { this.id = w.id; this.token = w.token; }
    return w;
  }
  send(m) { this.ws.send(JSON.stringify(m)); }
  wait(pred, ms = 8000) {
    const found = this.msgs.find(pred);
    if (found) { this.msgs.splice(this.msgs.indexOf(found), 1); return Promise.resolve(found); }
    return new Promise((res, rej) => {
      const w = { pred, res: m => { this.msgs.splice(this.msgs.indexOf(m), 1); res(m); } };
      this.waiters.push(w);
      setTimeout(() => rej(new Error(`${this.label}: timed out waiting`)), ms);
    });
  }
  clear() { this.msgs.length = 0; }
  close() { this.ws.close(); }
}

const tag = Math.random().toString(36).slice(2, 6);
const A = new Client('A'), B = new Client('B');
await A.open(); await B.open();
A.send({ t: 'set_name', name: 'Alpha' + tag });
assert.equal((await A.wait(m => m.t === 'name_result')).ok, true);
B.send({ t: 'set_name', name: ('alpha' + tag).toUpperCase() });
assert.equal((await B.wait(m => m.t === 'name_result')).ok, false, 'duplicate names must be refused');
B.send({ t: 'set_name', name: 'fuckboy' });
assert.equal((await B.wait(m => m.t === 'name_result')).ok, false);
B.send({ t: 'set_name', name: 'Bravo' + tag });
assert.equal((await B.wait(m => m.t === 'name_result')).ok, true);
console.log('names ok');

// reconnect keeps the account
A.close(); await sleep(200);
const A2 = new Client('A2');
const w = await A2.open({ id: A.id, token: A.token });
assert.equal(w.name, 'Alpha' + tag);
console.log('reconnect ok');

// friends + party
A2.send({ t: 'friend_req', target: 'Bravo' + tag });
await B.wait(m => m.t === 'friends' && m.incoming.length === 1);
B.send({ t: 'friend_accept', id: A2.id });
await A2.wait(m => m.t === 'friends' && m.friends.length === 1 && m.friends[0].online);
A2.send({ t: 'party_invite', id: B.id });
const inv = await B.wait(m => m.t === 'party_invite');
B.send({ t: 'party_accept', partyId: inv.partyId });
await A2.wait(m => m.t === 'party' && m.members.length === 2);
console.log('friends + party ok');

// members can't steer, leader takes everyone along
B.send({ t: 'go', dest: 'tdm' });
await B.wait(m => m.t === 'toast' && /leader/.test(m.msg));
A2.send({ t: 'go', dest: '3v3' });
const ra = await A2.wait(m => m.t === 'room' && m.kind === 'match');
const rb = await B.wait(m => m.t === 'room' && m.kind === 'match');
assert.equal(ra.id, rb.id); assert.equal(ra.team, rb.team, 'a small party stays on one team');
console.log('party follow ok');

// chat + automod
A2.clear(); B.clear();
A2.send({ t: 'chat', msg: 'fuck yeah lets go' });
assert.equal((await B.wait(m => m.t === 'chat')).msg, 'fuck yeah lets go');
await sleep(900);
A2.send({ t: 'chat', msg: 'you bitch' });
const muted = await A2.wait(m => m.t === 'muted');
assert.ok(muted.until > Date.now() + 14 * 60000);
await sleep(900);
A2.send({ t: 'chat', msg: 'hello?' });
await A2.wait(m => m.t === 'muted');
console.log('automod ok');

// report + admin http
B.send({ t: 'report', target: A2.id, reason: 'Bad language' });
await B.wait(m => m.t === 'toast' && /report/i.test(m.msg));
let r = await (await fetch(`${HTTP_BASE}/admin?key=testkey&action=reports`)).json();
assert.ok(r.data.some(x => x.target === A2.id));
r = await (await fetch(`${HTTP_BASE}/admin?key=testkey&action=lookup&target=${A2.id}`)).json();
assert.equal(r.data.reports >= 1, true);
r = await (await fetch(`${HTTP_BASE}/admin?key=wrong&action=lookup&target=${A2.id}`));
assert.equal(r.status, 403);
console.log('reports + admin ok');

// a full party of 6 in 3v3 splits 3 / 3
const six = [];
for (let i = 0; i < 6; i++) { const c = new Client('P' + i); await c.open(); c.send({ t: 'set_name', name: `Six${i}${tag}` }); await c.wait(m => m.t === 'name_result' && m.ok); six.push(c); }
for (let i = 1; i < 6; i++) { six[0].send({ t: 'friend_req', target: six[i].id }); await six[i].wait(m => m.t === 'friends' && m.incoming.length); six[i].send({ t: 'friend_accept', id: six[0].id }); await six[0].wait(m => m.t === 'toast' && /accepted/.test(m.msg)); }
for (let i = 1; i < 6; i++) { six[0].send({ t: 'party_invite', id: six[i].id }); const iv = await six[i].wait(m => m.t === 'party_invite'); six[i].send({ t: 'party_accept', partyId: iv.partyId }); }
await six[0].wait(m => m.t === 'party' && m.members.length === 6);
six[0].send({ t: 'go', dest: '3v3' });
const rooms = await Promise.all(six.map(c => c.wait(m => m.t === 'room' && m.kind === 'match')));
assert.equal(new Set(rooms.map(x => x.id)).size, 1);
const teams = rooms.map(x => x.team);
assert.equal(teams.filter(t => t === 0).length, 3); assert.equal(teams.filter(t => t === 1).length, 3);
console.log('6-party 3v3 split ok');

// the match starts, hits land, kills count
await six[0].wait(m => m.t === 'ms' && m.state === 'live', 9000);
const me = six[0], enemy = six[teams.indexOf(1 - teams[0])];
await sleep(1700); // spawn protection
me.send({ t: 'hit', target: enemy.id, dmg: 60, head: false, gun: 'test' });
assert.equal((await enemy.wait(m => m.t === 'hp' && m.id === enemy.id)).hp, 40);
me.send({ t: 'hit', target: enemy.id, dmg: 60, head: true, gun: 'test' });
const k = await enemy.wait(m => m.t === 'kill');
assert.equal(k.killer, me.id); assert.equal(k.scores[teams[0]], 1);
await enemy.wait(m => m.t === 'rs' && m.id === enemy.id, 5000);
console.log('combat ok');

// ban kicks and blocks
r = await (await fetch(`${HTTP_BASE}/admin?key=testkey&action=ban&target=${enemy.id}&minutes=5&reason=test`)).json();
assert.equal(r.ok, true);
await enemy.wait(m => m.t === 'banned');
const again = new Client('again');
assert.equal((await again.open({ id: enemy.id, token: enemy.token })).t, 'banned');
await fetch(`${HTTP_BASE}/admin?key=testkey&action=unban&target=${enemy.id}`);
console.log('ban ok');

for (const c of [A2, B, ...six]) try { c.close(); } catch (e) { }
console.log('ALL OK');
process.exit(0);
