/* ============================================================
   GameServer — the single Durable Object behind Flopzone.

   Trust model: each headset is trusted for its OWN body (where it is,
   where its hands are) and for its own hit detection, the same as most
   hobby VR shooters. The server owns everything that decides a match:
   health, kills, scores, the clock, flags, teams — plus accounts, names,
   friends, parties, chat and moderation.

   Protocol: JSON text frames, each with a "t" (type) field.
   Client -> server
     hello {id?, token?, ver}          sign in (no id = create an account)
     set_name {name}
     friend_req {target}  friend_accept {id}  friend_decline {id}  friend_remove {id}
     party_invite {id}  party_accept {partyId}  party_decline {partyId}
     party_leave  party_kick {id}
     go {dest}                         'hub' or a mode id; party leader only
     st {p:[...]}                      my pose, relayed to my room
     ev {e:{...}}                      cosmetic event (shot, reload), relayed
     hit {target, dmg, head, gun, dir} I hit someone
     die                               I fell off the map
     flag {a:'touch'|'cap', team}      CTF
     chat {msg}
     report {target, reason}
     mod {action, target, minutes, reason}   admins only
     ping
   Server -> client
     welcome, name_result, friends, party, party_invite, room, pj, pl, pn,
     st, ev, hp, kill, rs (respawn), ms (match state), me (match end), fl
     (flags), chat, toast, muted, kicked, banned, mod_result, pong, error
   ============================================================ */

import { checkChat, checkName, cleanName } from './filter.js';

const PROTOCOL = 1;

export const MODES = {
  tdm:   { name: 'Team Deathmatch', teams: true,  teamSize: 5, max: 10, killLimit: 40, time: 480 },
  ffa:   { name: 'Free For All',    teams: false, teamSize: 8, max: 8,  killLimit: 20, time: 480 },
  '1v1': { name: '1v1',             teams: true,  teamSize: 1, max: 2,  killLimit: 10, time: 300 },
  '2v2': { name: '2v2',             teams: true,  teamSize: 2, max: 4,  killLimit: 15, time: 360 },
  '3v3': { name: '3v3',             teams: true,  teamSize: 3, max: 6,  killLimit: 20, time: 420 },
  ctf:   { name: 'Capture The Flag', teams: true, teamSize: 5, max: 10, capLimit: 3,   time: 600 }
};

// Same order as MapCatalog.cs on the headset.
export const MAP_NAMES = [
  'Warehouse', 'Rooftops', 'Dune Outpost', 'Frostbite', 'Jungle Ruins',
  'Neon City', 'Shipyard', 'Foundry', 'Castle Keep', 'Moon Base'
];

const HUB_MAX = 24;
const PARTY_MAX = 6;
const RESPAWN_MS = 3000;
const SPAWN_PROTECT_MS = 1500;
const COUNTDOWN_MS = 5000;
const RESULTS_MS = 10000;
const FLAG_RETURN_MS = 20000;
const AUTO_MUTE_MIN = 15;
const MAX_DAMAGE = 260;          // a headshot from the biggest sniper
const MAX_FRIENDS = 100;
const PERMANENT = 8640000000000000;
const ID_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

function now() { return Date.now(); }
function rand(n) { return Math.floor(Math.random() * n); }
function num(v, d) { const n = Number(v); return Number.isFinite(n) ? n : d; }
function str(v, max) { return String(v == null ? '' : v).slice(0, max); }
function randomHex(bytes) {
  const a = new Uint8Array(bytes); crypto.getRandomValues(a);
  return Array.from(a, b => b.toString(16).padStart(2, '0')).join('');
}
function isPlayerId(s) { return /^FZ[A-Z0-9]{6}$/.test(String(s || '').toUpperCase()); }

export class GameServer {
  constructor(ctx, env) {
    this.ctx = ctx;
    this.env = env;
    this.storage = ctx.storage;
    this.conns = new Map();     // playerId -> conn
    this.accounts = new Map();  // playerId -> account (cache of storage)
    this.parties = new Map();   // partyId -> {id, leader, members:[], invites:Set}
    this.rooms = new Map();     // roomId -> room
    this.chatLog = new Map();   // playerId -> last few chat lines, attached to reports as evidence
    this.nextRoom = 1;
    this.nextParty = 1;
    this.tick = null;
  }

  adminIds() {
    return String(this.env.ADMIN_IDS || '').toUpperCase().split(/[\s,]+/).filter(Boolean);
  }
  isAdmin(acct) { return !!acct && (acct.admin || this.adminIds().includes(acct.id)); }

  async fetch(request) {
    const url = new URL(request.url);
    if (url.pathname === '/admin') return this.adminHttp(url);
    if (request.headers.get('Upgrade') !== 'websocket') return new Response('expected a websocket', { status: 426 });
    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    server.accept();
    this.attach(server);
    if (!this.tick) this.tick = setInterval(() => this.onTick(), 1000);
    return new Response(null, { status: 101, webSocket: client });
  }

  /* ---------------- connections ---------------- */

  attach(ws) {
    const c = { ws, id: null, acct: null, roomId: null, partyId: null, pos: [0, 0, 0], lastChat: 0, closed: false, reported: new Map() };
    ws.addEventListener('message', ev => {
      this.onMessage(c, ev.data).catch(err => console.error('message failed', err && err.stack || err));
    });
    const drop = () => this.onClose(c);
    ws.addEventListener('close', drop);
    ws.addEventListener('error', drop);
  }

  send(c, obj) {
    if (!c || c.closed) return;
    try { c.ws.send(typeof obj === 'string' ? obj : JSON.stringify(obj)); } catch (e) { }
  }

  sendId(id, obj) { this.send(this.conns.get(id), obj); }

  toast(c, msg) { this.send(c, { t: 'toast', msg }); }

  close(c, code, why) {
    try { c.ws.close(code || 1000, why || ''); } catch (e) { }
    this.onClose(c);
  }

  onClose(c) {
    if (c.closed) return;
    c.closed = true;
    if (!c.id || this.conns.get(c.id) !== c) return;
    this.leaveRoom(c);
    this.leaveParty(c, true);
    this.conns.delete(c.id);
    this.refreshFriendsOf(c.id);
    if (this.conns.size === 0 && this.tick) { clearInterval(this.tick); this.tick = null; }
  }

  async onMessage(c, data) {
    if (typeof data !== 'string' || data.length > 8192) return;
    let m;
    try { m = JSON.parse(data); } catch (e) { return; }
    if (!m || typeof m.t !== 'string') return;
    if (!c.id) { if (m.t === 'hello') await this.hello(c, m); return; }
    switch (m.t) {
      case 'st': return this.pose(c, m);
      case 'ev': return this.relayEvent(c, m);
      case 'hit': return this.hit(c, m);
      case 'die': return this.fell(c);
      case 'flag': return this.flagAction(c, m);
      case 'ping': return this.send(c, { t: 'pong', time: now() });
      case 'go': return this.go(c, str(m.dest, 16));
      case 'chat': return this.chat(c, m);
      case 'set_name': return this.setName(c, m);
      case 'friend_req': return this.friendRequest(c, str(m.target, 32));
      case 'friend_accept': return this.friendAccept(c, str(m.id, 16).toUpperCase());
      case 'friend_decline': return this.friendDecline(c, str(m.id, 16).toUpperCase());
      case 'friend_remove': return this.friendRemove(c, str(m.id, 16).toUpperCase());
      case 'friends': return this.sendFriends(c);
      case 'party_invite': return this.partyInvite(c, str(m.id, 16).toUpperCase());
      case 'party_accept': return this.partyAccept(c, str(m.partyId, 16));
      case 'party_decline': return this.partyDecline(c, str(m.partyId, 16));
      case 'party_leave': return this.leaveParty(c, false);
      case 'party_kick': return this.partyKick(c, str(m.id, 16).toUpperCase());
      case 'report': return this.report(c, m);
      case 'mod': return this.modFromClient(c, m);
    }
  }

  /* ---------------- accounts ---------------- */

  async getAcct(id) {
    if (!id) return null;
    id = String(id).toUpperCase();
    if (this.accounts.has(id)) return this.accounts.get(id);
    const a = await this.storage.get('acct:' + id);
    if (a) this.accounts.set(id, a);
    return a || null;
  }

  async saveAcct(a) {
    this.accounts.set(a.id, a);
    await this.storage.put('acct:' + a.id, a);
  }

  async newId() {
    for (let tries = 0; tries < 50; tries++) {
      let id = 'FZ';
      for (let i = 0; i < 6; i++) id += ID_CHARS[rand(ID_CHARS.length)];
      if (!(await this.getAcct(id))) return id;
    }
    throw new Error('could not make an id');
  }

  async hello(c, m) {
    let acct = null;
    if (m.id && m.token) {
      const a = await this.getAcct(m.id);
      if (a && a.token === String(m.token)) acct = a;
    }
    if (!acct) {
      acct = {
        id: await this.newId(), token: randomHex(24), name: '', created: now(),
        friends: [], incoming: [], outgoing: [], banUntil: 0, banReason: '', muteUntil: 0,
        admin: false, reports: 0
      };
      await this.saveAcct(acct);
    }
    if (acct.banUntil > now()) {
      this.send(c, { t: 'banned', until: acct.banUntil === PERMANENT ? 0 : acct.banUntil, reason: acct.banReason || '' });
      return this.close(c, 4003, 'banned');
    }
    const old = this.conns.get(acct.id);
    if (old && old !== c) {
      this.send(old, { t: 'kicked', reason: 'You signed in somewhere else.' });
      this.close(old, 4001, 'replaced');
    }
    c.id = acct.id;
    c.acct = acct;
    this.conns.set(acct.id, c);
    this.send(c, {
      t: 'welcome', proto: PROTOCOL, id: acct.id, token: acct.token, name: acct.name,
      needsName: !acct.name, admin: this.isAdmin(acct), muteUntil: acct.muteUntil > now() ? acct.muteUntil : 0,
      serverTime: now()
    });
    await this.sendFriends(c);
    this.sendParty(null, c);
    this.refreshFriendsOf(acct.id);
  }

  async setName(c, m) {
    const name = cleanName(str(m.name, 40));
    const bad = checkName(name);
    if (bad) return this.send(c, { t: 'name_result', ok: false, error: bad });
    const key = 'name:' + name.toLowerCase();
    const owner = await this.storage.get(key);
    if (owner && owner !== c.id) return this.send(c, { t: 'name_result', ok: false, error: 'Someone already has that name. Try another one.' });
    const acct = c.acct;
    if (acct.name && acct.name.toLowerCase() !== name.toLowerCase()) await this.storage.delete('name:' + acct.name.toLowerCase());
    await this.storage.put(key, acct.id);
    acct.name = name;
    await this.saveAcct(acct);
    this.send(c, { t: 'name_result', ok: true, name });
    const room = this.rooms.get(c.roomId);
    if (room) {
      const rp = room.players.get(c.id);
      if (rp) rp.name = name;
      this.broadcast(room, { t: 'pn', id: c.id, name });
    }
    this.refreshFriendsOf(c.id);
    const party = this.parties.get(c.partyId);
    if (party) this.sendParty(party);
  }

  async resolveTarget(target) {
    target = String(target || '').trim();
    if (!target) return null;
    if (isPlayerId(target)) { const a = await this.getAcct(target.toUpperCase()); if (a) return a; }
    const id = await this.storage.get('name:' + target.toLowerCase());
    return id ? this.getAcct(id) : null;
  }

  /* ---------------- friends ---------------- */

  where(id) {
    const c = this.conns.get(id);
    if (!c) return 'Offline';
    const room = this.rooms.get(c.roomId);
    if (!room) return 'Online';
    if (room.kind === 'hub') return 'In the Hub';
    return `Playing ${MODES[room.mode].name} on ${MAP_NAMES[room.map]}`;
  }

  async sendFriends(c) {
    if (!c || !c.acct) return;
    const a = c.acct;
    const describe = async id => {
      const f = await this.getAcct(id);
      return { id, name: f ? f.name || '(new player)' : '?', online: this.conns.has(id), where: this.where(id) };
    };
    const friends = []; for (const id of a.friends) friends.push(await describe(id));
    const incoming = []; for (const id of a.incoming) incoming.push(await describe(id));
    const outgoing = []; for (const id of a.outgoing) outgoing.push(await describe(id));
    friends.sort((x, y) => (y.online - x.online) || x.name.localeCompare(y.name));
    this.send(c, { t: 'friends', friends, incoming, outgoing });
  }

  refreshFriendsOf(id) {
    const a = this.accounts.get(id);
    if (!a) return;
    for (const fid of a.friends.concat(a.incoming, a.outgoing)) {
      const fc = this.conns.get(fid);
      if (fc) this.sendFriends(fc).catch(() => { });
    }
  }

  async friendRequest(c, target) {
    const me = c.acct;
    if (!me.name) return this.toast(c, 'Pick a name first.');
    const other = await this.resolveTarget(target);
    if (!other || !other.name) return this.toast(c, `No player called "${target}".`);
    if (other.id === me.id) return this.toast(c, "You can't friend yourself.");
    if (me.friends.includes(other.id)) return this.toast(c, `${other.name} is already your friend.`);
    if (me.friends.length >= MAX_FRIENDS) return this.toast(c, 'Your friends list is full.');
    if (me.incoming.includes(other.id)) return this.friendAccept(c, other.id);
    if (!me.outgoing.includes(other.id)) me.outgoing.push(other.id);
    if (!other.incoming.includes(me.id)) other.incoming.push(me.id);
    await this.saveAcct(me); await this.saveAcct(other);
    this.toast(c, `Friend request sent to ${other.name}.`);
    this.sendId(other.id, { t: 'toast', msg: `${me.name} sent you a friend request.` });
    await this.sendFriends(c);
    const oc = this.conns.get(other.id); if (oc) await this.sendFriends(oc);
  }

  async friendAccept(c, id) {
    const me = c.acct;
    if (!me.incoming.includes(id)) return;
    const other = await this.getAcct(id);
    if (!other) return;
    me.incoming = me.incoming.filter(x => x !== id);
    other.outgoing = other.outgoing.filter(x => x !== me.id);
    if (!me.friends.includes(id)) me.friends.push(id);
    if (!other.friends.includes(me.id)) other.friends.push(me.id);
    await this.saveAcct(me); await this.saveAcct(other);
    this.toast(c, `You and ${other.name} are now friends.`);
    this.sendId(id, { t: 'toast', msg: `${me.name} accepted your friend request.` });
    await this.sendFriends(c);
    const oc = this.conns.get(id); if (oc) await this.sendFriends(oc);
  }

  async friendDecline(c, id) {
    const me = c.acct;
    const other = await this.getAcct(id);
    me.incoming = me.incoming.filter(x => x !== id);
    me.outgoing = me.outgoing.filter(x => x !== id);
    if (other) { other.outgoing = other.outgoing.filter(x => x !== me.id); other.incoming = other.incoming.filter(x => x !== me.id); await this.saveAcct(other); }
    await this.saveAcct(me);
    await this.sendFriends(c);
    const oc = this.conns.get(id); if (oc) await this.sendFriends(oc);
  }

  async friendRemove(c, id) {
    const me = c.acct;
    const other = await this.getAcct(id);
    me.friends = me.friends.filter(x => x !== id);
    if (other) { other.friends = other.friends.filter(x => x !== me.id); await this.saveAcct(other); }
    await this.saveAcct(me);
    await this.sendFriends(c);
    const oc = this.conns.get(id); if (oc) await this.sendFriends(oc);
  }

  /* ---------------- parties ---------------- */

  partyPayload(party) {
    if (!party) return { t: 'party', id: null, leader: null, members: [] };
    return {
      t: 'party', id: party.id, leader: party.leader,
      members: party.members.map(id => ({ id, name: (this.accounts.get(id) || {}).name || '?' }))
    };
  }

  sendParty(party, onlyTo) {
    if (onlyTo) return this.send(onlyTo, this.partyPayload(this.parties.get(onlyTo.partyId)));
    const payload = JSON.stringify(this.partyPayload(party));
    for (const id of party.members) this.sendId(id, payload);
  }

  partyInvite(c, id) {
    if (!c.acct.friends.includes(id)) return this.toast(c, 'You can only invite friends to your party.');
    const target = this.conns.get(id);
    if (!target) return this.toast(c, 'That friend is offline.');
    let party = this.parties.get(c.partyId);
    if (party && party.leader !== c.id) return this.toast(c, 'Only the party leader can invite.');
    if (party && party.members.includes(id)) return this.toast(c, 'They are already in your party.');
    if (party && party.members.length >= PARTY_MAX) return this.toast(c, `Parties can have up to ${PARTY_MAX} people.`);
    if (!party) {
      party = { id: 'P' + (this.nextParty++), leader: c.id, members: [c.id], invites: new Set() };
      this.parties.set(party.id, party);
      c.partyId = party.id;
      this.sendParty(party);
    }
    party.invites.add(id);
    this.send(target, { t: 'party_invite', partyId: party.id, from: c.acct.name, fromId: c.id });
    this.toast(c, `Party invite sent to ${target.acct.name}.`);
  }

  partyAccept(c, partyId) {
    const party = this.parties.get(partyId);
    if (!party || !party.invites.has(c.id)) return this.toast(c, 'That party invite has expired.');
    if (party.members.length >= PARTY_MAX) return this.toast(c, 'That party is full.');
    if (c.partyId === partyId) return;
    this.leaveParty(c, false);
    party.invites.delete(c.id);
    party.members.push(c.id);
    c.partyId = party.id;
    this.sendParty(party);
    for (const id of party.members) if (id !== c.id) this.sendId(id, { t: 'toast', msg: `${c.acct.name} joined the party.` });
    // follow the leader to wherever they are right now
    const leader = this.conns.get(party.leader);
    const room = leader && this.rooms.get(leader.roomId);
    if (room && room.id !== c.roomId) {
      if (room.kind === 'hub') { if (room.players.size < HUB_MAX) this.joinRoom(c, room, -1); }
      else {
        const lp = room.players.get(leader.id);
        const fit = this.fit(room, 1, lp ? lp.team : -1);
        if (fit) this.joinRoom(c, room, fit[0]);
        else this.toast(c, "The leader's match is full, you'll go with them next time.");
      }
    }
  }

  partyDecline(c, partyId) {
    const party = this.parties.get(partyId);
    if (party) party.invites.delete(c.id);
  }

  leaveParty(c, disconnected) {
    const party = this.parties.get(c.partyId);
    c.partyId = null;
    if (!party) return;
    party.members = party.members.filter(id => id !== c.id);
    if (!disconnected) this.sendParty(null, c);
    if (party.members.length <= 1) {
      for (const id of party.members) { const mc = this.conns.get(id); if (mc) { mc.partyId = null; this.sendParty(null, mc); this.toast(mc, 'Your party was disbanded.'); } }
      this.parties.delete(party.id);
      return;
    }
    if (party.leader === c.id) {
      party.leader = party.members[0];
      this.sendId(party.leader, { t: 'toast', msg: 'You are now the party leader.' });
    }
    this.sendParty(party);
  }

  partyKick(c, id) {
    const party = this.parties.get(c.partyId);
    if (!party || party.leader !== c.id || id === c.id) return;
    const target = this.conns.get(id);
    if (!target || target.partyId !== party.id) return;
    this.toast(target, 'You were removed from the party.');
    this.leaveParty(target, false);
  }

  /* ---------------- rooms & matchmaking ---------------- */

  newRoom(kind, mode) {
    const room = {
      id: 'R' + (this.nextRoom++), kind, mode: mode || null,
      map: kind === 'hub' ? -1 : rand(MAP_NAMES.length),
      players: new Map(), state: kind === 'hub' ? 'hub' : 'waiting', stateEnd: 0, endsAt: 0,
      scores: [0, 0], flags: null, created: now()
    };
    if (mode === 'ctf') room.flags = [0, 1].map(team => ({ team, state: 'home', carrier: null, pos: null, dropAt: 0 }));
    this.rooms.set(room.id, room);
    return room;
  }

  teamCounts(room) {
    const n = [0, 0];
    for (const p of room.players.values()) if (p.team === 0 || p.team === 1) n[p.team]++;
    return n;
  }

  /* Where a group of n players would go in this room: an array of n team
     numbers, or null if they don't fit. A group that fits on one team stays
     together; a group bigger than a team (a full 6-party in 3v3) is split
     across both teams, as evenly as the free seats allow. */
  fit(room, n, preferTeam) {
    const mode = MODES[room.mode];
    if (!mode) return null;
    if (!mode.teams) return room.players.size + n <= mode.max ? new Array(n).fill(-1) : null;
    const counts = this.teamCounts(room);
    const free = [mode.teamSize - counts[0], mode.teamSize - counts[1]];
    if (preferTeam === 0 || preferTeam === 1) { if (free[preferTeam] >= n) return new Array(n).fill(preferTeam); }
    const best = free[0] >= free[1] ? 0 : 1;
    if (free[best] >= n) return new Array(n).fill(best);
    if (free[0] + free[1] < n) return null;
    // split: half (rounded up) on one team, the rest on the other
    const first = Math.min(free[best], Math.ceil(n / 2));
    const second = n - first;
    if (second > free[1 - best]) return null;
    return new Array(first).fill(best).concat(new Array(second).fill(1 - best));
  }

  go(c, dest) {
    if (!c.acct.name) return this.toast(c, 'Pick a name first.');
    const party = this.parties.get(c.partyId);
    if (party && party.leader !== c.id) return this.toast(c, 'Only the party leader picks where the party goes. Leave the party to go on your own.');
    const group = (party ? party.members : [c.id]).map(id => this.conns.get(id)).filter(Boolean);
    if (dest === 'hub') return this.moveToHub(group);
    const mode = MODES[dest];
    if (!mode) return;
    if (group.length > mode.max) return this.toast(c, `Your party has ${group.length} people, ${mode.name} fits ${mode.max}.`);
    // A group bigger than one team (e.g. 6 in 3v3) needs a fresh room so they can split evenly.
    let target = null, teams = null;
    if (!(mode.teams && group.length > mode.teamSize)) {
      let bestSize = -1;
      for (const room of this.rooms.values()) {
        if (room.kind !== 'match' || room.mode !== dest || room.state === 'ended') continue;
        if (room.state === 'live' && room.endsAt - now() < 60000) continue;
        if (group.some(g => g.roomId === room.id)) continue;
        const f = this.fit(room, group.length);
        if (f && room.players.size > bestSize) { target = room; teams = f; bestSize = room.players.size; }
      }
    }
    if (!target) { target = this.newRoom('match', dest); teams = this.fit(target, group.length); }
    group.forEach((g, i) => this.joinRoom(g, target, teams[i]));
  }

  moveToHub(group) {
    let target = null;
    for (const room of this.rooms.values()) {
      if (room.kind === 'hub' && room.players.size + group.length <= HUB_MAX && !group.some(g => g.roomId === room.id)) { target = room; break; }
    }
    if (!target) target = this.newRoom('hub');
    for (const g of group) this.joinRoom(g, target, -1);
  }

  playerPayload(p) {
    return { id: p.id, name: p.name, team: p.team, hp: p.hp, alive: p.alive, k: p.k, d: p.d, admin: p.admin };
  }

  roomPayload(room, you) {
    return {
      t: 'room', id: room.id, kind: room.kind, mode: room.mode, map: room.map, state: room.state,
      time: this.timeLeft(room), scores: room.scores, flags: room.flags,
      team: you.team, spawn: rand(1000),
      players: Array.from(room.players.values(), p => this.playerPayload(p))
    };
  }

  timeLeft(room) {
    if (room.state === 'live') return Math.max(0, Math.round((room.endsAt - now()) / 1000));
    if (room.state === 'countdown' || room.state === 'ended') return Math.max(0, Math.round((room.stateEnd - now()) / 1000));
    return 0;
  }

  joinRoom(c, room, team) {
    if (c.roomId === room.id) return;
    this.leaveRoom(c);
    const p = { id: c.id, name: c.acct.name, team, hp: 100, alive: true, k: 0, d: 0, respawnAt: 0, protectUntil: now() + SPAWN_PROTECT_MS, admin: this.isAdmin(c.acct) };
    room.players.set(c.id, p);
    c.roomId = room.id;
    c.pos = [0, 0, 0];
    this.send(c, this.roomPayload(room, p));
    this.broadcast(room, { t: 'pj', p: this.playerPayload(p) }, c.id);
    this.refreshFriendsOf(c.id);
    if (room.kind === 'match') this.checkStart(room);
  }

  leaveRoom(c) {
    const room = this.rooms.get(c.roomId);
    c.roomId = null;
    if (!room) return;
    this.dropFlag(room, c.id);
    room.players.delete(c.id);
    this.broadcast(room, { t: 'pl', id: c.id });
    if (room.players.size === 0) { this.rooms.delete(room.id); return; }
    if (room.kind === 'match' && room.state === 'live') {
      const mode = MODES[room.mode];
      const counts = this.teamCounts(room);
      if (mode.teams && (counts[0] === 0 || counts[1] === 0)) this.endMatch(room, counts[0] ? 0 : 1, null, 'The other team left.');
      else if (!mode.teams && room.players.size < 2) this.endMatch(room, -1, room.players.keys().next().value, 'Everyone else left.');
    }
  }

  broadcast(room, obj, exceptId) {
    const text = typeof obj === 'string' ? obj : JSON.stringify(obj);
    for (const id of room.players.keys()) if (id !== exceptId) this.sendId(id, text);
  }

  /* ---------------- match flow ---------------- */

  canStart(room) {
    const mode = MODES[room.mode];
    if (room.players.size < 2) return false;
    if (mode.teams) { const n = this.teamCounts(room); return n[0] > 0 && n[1] > 0; }
    return true;
  }

  matchState(room, note) {
    this.broadcast(room, { t: 'ms', state: room.state, time: this.timeLeft(room), scores: room.scores, note: note || '' });
  }

  checkStart(room) {
    if (room.state === 'waiting' && this.canStart(room)) {
      room.state = 'countdown';
      room.stateEnd = now() + COUNTDOWN_MS;
      this.matchState(room);
    }
  }

  onTick() {
    const t = now();
    for (const room of Array.from(this.rooms.values())) {
      if (room.kind !== 'match') continue;
      if (room.state === 'waiting') this.checkStart(room);
      else if (room.state === 'countdown') {
        if (!this.canStart(room)) { room.state = 'waiting'; this.matchState(room, 'Waiting for players'); }
        else if (t >= room.stateEnd) this.startMatch(room);
      } else if (room.state === 'live') {
        if (t >= room.endsAt) this.timeUp(room);
      } else if (room.state === 'ended') {
        if (t >= room.stateEnd) this.closeMatch(room);
        continue;
      }
      for (const p of room.players.values()) {
        if (!p.alive && p.respawnAt && t >= p.respawnAt) this.respawn(room, p);
      }
      if (room.flags) for (const f of room.flags) {
        if (f.state === 'dropped' && t - f.dropAt >= FLAG_RETURN_MS) { f.state = 'home'; f.pos = null; this.sendFlags(room, `The ${f.team ? 'Blue' : 'Red'} flag went home.`); }
      }
    }
  }

  startMatch(room) {
    const mode = MODES[room.mode];
    room.state = 'live';
    room.endsAt = now() + mode.time * 1000;
    room.scores = [0, 0];
    if (room.flags) room.flags.forEach(f => { f.state = 'home'; f.carrier = null; f.pos = null; });
    for (const p of room.players.values()) { p.k = 0; p.d = 0; }
    this.matchState(room, 'FIGHT!');
    for (const p of room.players.values()) this.respawn(room, p);
    if (room.flags) this.sendFlags(room);
  }

  respawn(room, p) {
    p.alive = true; p.hp = 100; p.respawnAt = 0; p.protectUntil = now() + SPAWN_PROTECT_MS;
    this.broadcast(room, { t: 'rs', id: p.id, spawn: rand(1000) });
  }

  timeUp(room) {
    const mode = MODES[room.mode];
    if (mode.teams) {
      const s = room.scores;
      this.endMatch(room, s[0] === s[1] ? -1 : (s[0] > s[1] ? 0 : 1), null, "Time's up!");
    } else {
      let best = null;
      for (const p of room.players.values()) if (!best || p.k > best.k) best = p;
      this.endMatch(room, -1, best ? best.id : null, "Time's up!");
    }
  }

  endMatch(room, winnerTeam, winnerId, note) {
    if (room.state === 'ended') return;
    room.state = 'ended';
    room.stateEnd = now() + RESULTS_MS;
    this.broadcast(room, {
      t: 'me', winnerTeam, winnerId, note: note || '', scores: room.scores,
      players: Array.from(room.players.values(), p => ({ id: p.id, name: p.name, team: p.team, k: p.k, d: p.d }))
    });
  }

  closeMatch(room) {
    // send everyone back to the hub, keeping parties together
    const done = new Set();
    for (const id of Array.from(room.players.keys())) {
      if (done.has(id)) continue;
      const c = this.conns.get(id);
      if (!c) continue;
      const party = this.parties.get(c.partyId);
      const group = (party ? party.members : [id]).map(x => this.conns.get(x)).filter(g => g && g.roomId === room.id);
      group.forEach(g => done.add(g.id));
      this.moveToHub(group);
    }
    this.rooms.delete(room.id);
  }

  /* ---------------- gameplay ---------------- */

  pose(c, m) {
    const room = this.rooms.get(c.roomId);
    if (!room || !Array.isArray(m.p) || m.p.length > 48) return;
    const p = m.p.map(v => Math.round(num(v, 0) * 1000) / 1000);
    c.pos = [p[0], p[1], p[2]];
    this.broadcast(room, '{"t":"st","id":"' + c.id + '","p":' + JSON.stringify(p) + '}', c.id);
  }

  relayEvent(c, m) {
    const room = this.rooms.get(c.roomId);
    if (!room || !m.e || typeof m.e !== 'object') return;
    const e = JSON.stringify(m.e);
    if (e.length > 600) return;
    this.broadcast(room, '{"t":"ev","id":"' + c.id + '","e":' + e + '}', c.id);
  }

  hit(c, m) {
    const room = this.rooms.get(c.roomId);
    if (!room || room.kind !== 'match' || room.state === 'countdown' || room.state === 'ended') return;
    const shooter = room.players.get(c.id);
    const target = room.players.get(str(m.target, 16));
    if (!shooter || !target || !shooter.alive || !target.alive || shooter === target) return;
    if (MODES[room.mode].teams && shooter.team === target.team) return;
    if (now() < target.protectUntil) return;
    const dmg = Math.max(0, Math.min(MAX_DAMAGE, num(m.dmg, 0)));
    if (!dmg) return;
    if (shooter.protectUntil > now()) shooter.protectUntil = 0; // shooting ends your own spawn protection
    target.hp = Math.max(0, Math.round(target.hp - dmg));
    this.broadcast(room, { t: 'hp', id: target.id, hp: target.hp, by: shooter.id });
    if (target.hp <= 0) this.kill(room, target, shooter, str(m.gun, 24), !!m.head, Array.isArray(m.dir) ? m.dir.slice(0, 3).map(v => num(v, 0)) : null);
  }

  fell(c) {
    const room = this.rooms.get(c.roomId);
    if (!room || room.kind !== 'match') return;
    const p = room.players.get(c.id);
    if (p && p.alive) this.kill(room, p, null, 'fall', false, null);
  }

  kill(room, victim, killer, gun, head, dir) {
    victim.alive = false;
    victim.hp = 0;
    victim.respawnAt = now() + RESPAWN_MS;
    const live = room.state === 'live';
    const mode = MODES[room.mode];
    if (live) {
      victim.d++;
      if (killer) {
        killer.k++;
        if (mode.teams && room.mode !== 'ctf') room.scores[killer.team]++;
      }
    }
    this.broadcast(room, {
      t: 'kill', victim: victim.id, killer: killer ? killer.id : null, gun, head, dir,
      scores: room.scores, kk: killer ? killer.k : 0, vd: victim.d
    });
    this.dropFlag(room, victim.id);
    if (!live) return;
    if (killer && mode.killLimit) {
      if (mode.teams && room.scores[killer.team] >= mode.killLimit) this.endMatch(room, killer.team, null, '');
      else if (!mode.teams && killer.k >= mode.killLimit) this.endMatch(room, -1, killer.id, '');
    }
  }

  /* ---------------- capture the flag ---------------- */

  sendFlags(room, note) {
    this.broadcast(room, { t: 'fl', flags: room.flags, scores: room.scores, note: note || '' });
  }

  dropFlag(room, carrierId) {
    if (!room.flags) return;
    for (const f of room.flags) {
      if (f.state === 'carried' && f.carrier === carrierId) {
        const c = this.conns.get(carrierId);
        f.state = 'dropped'; f.carrier = null; f.dropAt = now();
        f.pos = c ? c.pos.slice() : null;
        if (!f.pos) f.state = 'home';
        this.sendFlags(room, `The ${f.team ? 'Blue' : 'Red'} flag was dropped!`);
      }
    }
  }

  flagAction(c, m) {
    const room = this.rooms.get(c.roomId);
    if (!room || !room.flags || room.state !== 'live') return;
    const p = room.players.get(c.id);
    if (!p || !p.alive) return;
    if (m.a === 'touch') {
      const f = room.flags[num(m.team, -1)];
      if (!f) return;
      if (f.team !== p.team && (f.state === 'home' || f.state === 'dropped')) {
        f.state = 'carried'; f.carrier = p.id; f.pos = null;
        this.sendFlags(room, `${p.name} took the ${f.team ? 'Blue' : 'Red'} flag!`);
      } else if (f.team === p.team && f.state === 'dropped') {
        f.state = 'home'; f.pos = null;
        this.sendFlags(room, `${p.name} returned the ${f.team ? 'Blue' : 'Red'} flag.`);
      }
    } else if (m.a === 'cap') {
      const mine = room.flags[p.team];
      const theirs = room.flags[1 - p.team];
      if (!mine || !theirs || theirs.carrier !== p.id || mine.state !== 'home') return;
      theirs.state = 'home'; theirs.carrier = null; theirs.pos = null;
      room.scores[p.team]++;
      this.sendFlags(room, `${p.name} captured the flag!`);
      if (room.scores[p.team] >= MODES.ctf.capLimit) this.endMatch(room, p.team, null, '');
    }
  }

  /* ---------------- chat & auto-mod ---------------- */

  async chat(c, m) {
    const room = this.rooms.get(c.roomId);
    const msg = str(m.msg, 120).replace(/[\x00-\x1f\x7f]/g, '').trim();
    if (!room || !msg) return;
    const a = c.acct;
    if (a.muteUntil > now()) return this.send(c, { t: 'muted', until: a.muteUntil, reason: 'You are muted.' });
    if (now() - c.lastChat < 800) return;
    c.lastChat = now();
    const bad = checkChat(msg);
    if (bad) {
      a.muteUntil = now() + AUTO_MUTE_MIN * 60000;
      await this.saveAcct(a);
      this.logChat(a.id, '[blocked] ' + msg);
      return this.send(c, { t: 'muted', until: a.muteUntil, reason: `Auto-mod: ${bad}. You are muted for ${AUTO_MUTE_MIN} minutes.` });
    }
    this.logChat(a.id, msg);
    this.broadcast(room, { t: 'chat', id: a.id, name: a.name, msg });
  }

  logChat(id, msg) {
    const log = this.chatLog.get(id) || [];
    log.push(new Date().toISOString().slice(11, 19) + ' ' + msg);
    while (log.length > 10) log.shift();
    this.chatLog.set(id, log);
  }

  /* ---------------- reports ---------------- */

  async report(c, m) {
    const target = await this.getAcct(str(m.target, 16).toUpperCase());
    if (!target || target.id === c.id) return this.toast(c, 'Could not find that player.');
    const last = c.reported.get(target.id) || 0;
    if (now() - last < 5 * 60000) return this.toast(c, `You already reported ${target.name} recently.`);
    c.reported.set(target.id, now());
    const room = this.rooms.get(c.roomId);
    const rep = {
      id: now().toString(36) + randomHex(3), time: now(),
      by: c.id, byName: c.acct.name, target: target.id, targetName: target.name,
      reason: str(m.reason, 40), where: room ? (room.kind === 'hub' ? 'Hub' : `${MODES[room.mode].name} / ${MAP_NAMES[room.map]}`) : '',
      chat: (this.chatLog.get(target.id) || []).slice(-6)
    };
    await this.storage.put('report:' + rep.id, rep);
    target.reports = (target.reports || 0) + 1;
    await this.saveAcct(target);
    this.toast(c, `Thanks — your report on ${target.name} was sent to the moderators.`);
    for (const ac of this.conns.values()) if (this.isAdmin(ac.acct)) this.toast(ac, `New report: ${rep.byName} reported ${rep.targetName} (${rep.reason}).`);
  }

  /* ---------------- moderation ---------------- */

  async modFromClient(c, m) {
    if (!this.isAdmin(c.acct)) return this.send(c, { t: 'mod_result', ok: false, msg: 'You are not a moderator.' });
    const res = await this.doMod(str(m.action, 16), str(m.target, 32), num(m.minutes, 0), str(m.reason, 80), c.acct.name);
    this.send(c, Object.assign({ t: 'mod_result' }, res));
  }

  async adminHttp(url) {
    const key = this.env.ADMIN_KEY;
    const headers = { 'content-type': 'application/json', 'access-control-allow-origin': '*' };
    if (!key || url.searchParams.get('key') !== key) return new Response(JSON.stringify({ ok: false, msg: 'bad or missing key (set ADMIN_KEY with `npx wrangler secret put ADMIN_KEY`)' }), { status: 403, headers });
    const p = url.searchParams;
    const res = await this.doMod(p.get('action') || '', p.get('target') || '', num(p.get('minutes'), 0), p.get('reason') || '', 'owner');
    return new Response(JSON.stringify(res, null, 2), { headers });
  }

  async doMod(action, target, minutes, reason, by) {
    if (action === 'reports') {
      const list = await this.storage.list({ prefix: 'report:', reverse: true, limit: 50 });
      return { ok: true, msg: `${list.size} recent reports`, data: Array.from(list.values()) };
    }
    if (action === 'clear_report') {
      await this.storage.delete('report:' + target);
      return { ok: true, msg: 'Report cleared.' };
    }
    if (action === 'online') {
      const data = Array.from(this.conns.values(), c => ({ id: c.id, name: c.acct.name, where: this.where(c.id) }));
      return { ok: true, msg: `${data.length} online`, data };
    }
    const a = await this.resolveTarget(target);
    if (!a) return { ok: false, msg: `No player "${target}". Use their player ID (like FZ7K3Q9X) or exact name.` };
    const c = this.conns.get(a.id);
    const mins = Math.max(0, Math.floor(minutes));
    const label = `${a.name || '(no name)'} [${a.id}]`;
    switch (action) {
      case 'lookup':
        return {
          ok: true, msg: label, data: {
            id: a.id, name: a.name, online: !!c, where: this.where(a.id), created: a.created,
            banned: a.banUntil > now() ? (a.banUntil === PERMANENT ? 'permanent' : new Date(a.banUntil).toISOString()) : 'no',
            banReason: a.banReason, muted: a.muteUntil > now() ? new Date(a.muteUntil).toISOString() : 'no',
            reports: a.reports || 0, recentChat: this.chatLog.get(a.id) || []
          }
        };
      case 'kick':
        if (!c) return { ok: false, msg: `${label} is not online.` };
        this.send(c, { t: 'kicked', reason: reason || 'You were kicked by a moderator.' });
        this.close(c, 4002, 'kicked');
        return { ok: true, msg: `Kicked ${label}.` };
      case 'ban':
        a.banUntil = mins > 0 ? now() + mins * 60000 : PERMANENT;
        a.banReason = reason || 'Breaking the rules';
        await this.saveAcct(a);
        if (c) { this.send(c, { t: 'banned', until: mins > 0 ? a.banUntil : 0, reason: a.banReason }); this.close(c, 4003, 'banned'); }
        return { ok: true, msg: `Banned ${label} ${mins > 0 ? `for ${mins} minutes` : 'permanently'} (by ${by}).` };
      case 'unban':
        a.banUntil = 0; a.banReason = '';
        await this.saveAcct(a);
        return { ok: true, msg: `Unbanned ${label}.` };
      case 'mute':
        a.muteUntil = now() + (mins > 0 ? mins : 60) * 60000;
        await this.saveAcct(a);
        if (c) this.send(c, { t: 'muted', until: a.muteUntil, reason: reason || 'A moderator muted you.' });
        return { ok: true, msg: `Muted ${label} for ${mins > 0 ? mins : 60} minutes.` };
      case 'unmute':
        a.muteUntil = 0;
        await this.saveAcct(a);
        if (c) this.send(c, { t: 'muted', until: 0, reason: 'You were unmuted.' });
        return { ok: true, msg: `Unmuted ${label}.` };
      case 'rename': {
        if (a.name) await this.storage.delete('name:' + a.name.toLowerCase());
        a.name = '';
        await this.saveAcct(a);
        if (c) { c.acct = a; this.send(c, { t: 'welcome', proto: PROTOCOL, id: a.id, token: a.token, name: '', needsName: true, admin: this.isAdmin(a), muteUntil: a.muteUntil, serverTime: now() }); }
        return { ok: true, msg: `Cleared the name of ${label}; they must pick a new one.` };
      }
      case 'promote':
        a.admin = true; await this.saveAcct(a);
        return { ok: true, msg: `${label} is now a moderator.` };
      case 'demote':
        a.admin = false; await this.saveAcct(a);
        return { ok: true, msg: `${label} is no longer a moderator (ADMIN_IDS still wins).` };
    }
    return { ok: false, msg: `Unknown action "${action}". Try lookup, kick, ban, unban, mute, unmute, rename, reports, online, promote, demote.` };
  }
}
