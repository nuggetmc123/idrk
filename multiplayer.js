/* ============================================================
   ONLINE MULTIPLAYER
   Talks to the Cloudflare Worker in worker/index.js over a plain
   WebSocket. No WebRTC, no third-party SDK — just JSON messages
   through a relay the Worker owns.

   The trust model matches how this game already worked before any of
   this existed: bots have always been simulated locally by whoever's
   machine is running the match. Online, that "whoever" is the lobby's
   host — their browser runs the exact same simulation, just with some
   of the "bot" slots now fed by real people's input instead of AI, and
   it narrates the fight to everyone else over the relay. Nobody else's
   machine runs physics or scoring; they render what the host reports.

   RELAY_URL is left blank until the Worker is deployed (see
   worker/README.md). Blank means every match is solo-with-bots, i.e.
   the game behaves exactly as it did before this file existed.
   ============================================================ */
(function(){
"use strict";

/* wss://your-worker.your-subdomain.workers.dev — set after `wrangler deploy`. */
const RELAY_URL = '';

const ID_KEY   = 'brawlbound-guest-id';
const NAME_KEY = 'brawlbound-guest-name';
const SEARCH_WINDOW_MS = 6000;     // how long a host waits for backfill before using bots
const RESPAWN_GRACE = 8;           // seconds a networked player gets to choose their next class

function randomId(){
  return 'g-' + Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
}
function randomCode(){
  const abc = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';   // no 0/O/1/I — easier to read aloud
  let s = '';
  for(let i = 0; i < 5; i++) s += abc[Math.floor(Math.random() * abc.length)];
  return s;
}

function readLocal(key, fallback){
  try{ return localStorage.getItem(key) || fallback; }catch(e){ return fallback; }
}
function writeLocal(key, val){
  try{ localStorage.setItem(key, val); }catch(e){}
}

let myId = readLocal(ID_KEY, null);
if(!myId){ myId = randomId(); writeLocal(ID_KEY, myId); }

let guestName = readLocal(NAME_KEY, null);
if(!guestName){ guestName = 'Guest' + Math.floor(Math.random() * 9000 + 1000); writeLocal(NAME_KEY, guestName); }

/* ---------- connection state ---------- */

let ws = null;
let code = null;
let hostId = null;
let members = [];              // [{id, name, cls}]
let role = 'solo';             // 'solo' | 'lobby' | 'host' | 'client'
let myClass = null;
let searchTimer = null;

const lobbyListeners = [];
const matchBeginListeners = [];
const snapshotListeners = [];
const eventListeners = [];
const statusListeners = [];    // short human-readable status strings while matchmaking

function notify(list, arg){ list.forEach(fn => { try{ fn(arg); }catch(e){} }); }
function setStatus(text){ notify(statusListeners, text); }

function myName(){
  return (window.Career && Career.signedIn && Career.displayName) || guestName;
}

/* ---------- low-level socket ---------- */

function send(obj){
  if(ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
}

/* Opens a socket to a lobby room and resolves once the server confirms we
   joined it (or rejects if the room is full or the relay is unreachable). */
function openRoom(roomCode){
  return new Promise((resolve, reject) => {
    let settled = false;
    let sock;
    try{ sock = new WebSocket(RELAY_URL + '/room/' + encodeURIComponent(roomCode)); }
    catch(e){ reject(e); return; }

    const timeout = setTimeout(() => { if(!settled){ settled = true; sock.close(); reject(new Error('timeout')); } }, 6000);

    sock.addEventListener('open', () => {
      sock.send(JSON.stringify({t:'join', uid: myId, name: myName(), cls: myClass, upg: Net.getMyUpgrades(myClass)}));
    });
    sock.addEventListener('message', ev => {
      let msg; try{ msg = JSON.parse(ev.data); }catch(e){ return; }
      if(!settled && msg.t === 'joined'){
        settled = true;
        clearTimeout(timeout);
        ws = sock; code = roomCode; hostId = msg.hostId; role = 'lobby';
        resolve();
      }
      if(!settled && msg.t === 'full'){
        settled = true;
        clearTimeout(timeout);
        reject(new Error('full'));
      }
      onMessage(msg);
    });
    sock.addEventListener('close', () => {
      if(!settled){ settled = true; clearTimeout(timeout); reject(new Error('closed')); }
      if(ws === sock) teardown();
    });
    sock.addEventListener('error', () => {
      if(!settled){ settled = true; clearTimeout(timeout); reject(new Error('error')); }
    });
  });
}

function teardown(){
  ws = null; code = null; hostId = null; members = []; role = 'solo';
  clearTimeout(searchTimer);
  notify(lobbyListeners);
}

function onMessage(msg){
  switch(msg.t){
    case 'roster':
      hostId = msg.hostId;
      members = msg.members;
      notify(lobbyListeners);
      break;
    case 'host_left':
      setStatus('The host disconnected — match ended.');
      teardown();
      break;
    case 'match_start':
      role = (msg.hostId === myId) ? 'host' : 'client';
      notify(matchBeginListeners, {role, roster: msg.roster, hostId: msg.hostId});
      break;
    case 'input':
      // a respawn choice rides the same channel as ordinary input (both are
      // "a client telling the host something"), tagged so the host can tell
      // them apart without a third message type
      if(msg.input && msg.input.__respawnCls !== undefined){
        if(Net._respawnCb) Net._respawnCb(msg.from, msg.input.__respawnCls);
      } else {
        lastInputs[msg.from] = msg.input;
      }
      break;
    case 'snapshot':
      notify(snapshotListeners, msg.data);
      break;
    case 'event':
      notify(eventListeners, {kind: msg.kind, uid: msg.uid, cls: msg.cls});
      break;
  }
}

const lastInputs = {};   // uid -> latest {ax,ay,atk,sp,aim} — read by the host each tick

/* ---------- Directory (quick match) ---------- */

function directoryCall(path, opts){
  return fetch(RELAY_URL + '/directory' + path, opts).then(r => r.json()).catch(() => ({none:true, error:true}));
}

/* ---------- public API ---------- */

const Net = {
  get configured(){ return !!RELAY_URL; },
  get myId(){ return myId; },
  get myName(){ return myName(); },
  get role(){ return role; },
  get inMatch(){ return role === 'host' || role === 'client'; },
  get inLobby(){ return role === 'lobby' || this.inMatch; },
  get isHost(){ return role === 'lobby' ? hostId === myId : role === 'host'; },
  get lobby(){ return ws ? {code, hostId, members: members.slice()} : null; },

  onLobbyChange(fn){ lobbyListeners.push(fn); },
  onMatchBegin(fn){ matchBeginListeners.push(fn); },
  onSnapshot(fn){ snapshotListeners.push(fn); },
  onEvent(fn){ eventListeners.push(fn); },
  onStatus(fn){ statusListeners.push(fn); },

  /* ---- lobby lifecycle ---- */

  createLobby(cls){
    myClass = cls;
    return openRoom(randomCode()).then(() => code);
  },

  joinLobby(rawCode, cls){
    myClass = cls;
    const c = String(rawCode || '').trim().toUpperCase();
    if(!c) return Promise.reject(new Error('empty code'));
    return openRoom(c).then(() => code);
  },

  leaveLobby(){
    if(ws){ send({t:'leave'}); ws.close(); }
    teardown();
  },

  setMyClass(cls){
    myClass = cls;
    if(ws) send({t:'setClass', cls, upg: Net.getMyUpgrades(cls)});
  },

  /* index.html supplies this — multiplayer.js has no access to Career's
     data, only the shape a "your own upgrade levels for this fighter"
     lookup should have. Called right before every join/setClass so a
     remote human's upgrades are always current on whoever's hosting. */
  getMyUpgrades: function(){ return null; },

  /* Called when a match finishes or is left for the menu. Friends stay
     grouped in their lobby for a rematch — only an explicit leaveLobby()
     actually disconnects. A no-op outside a match. */
  matchEnded(){
    if(role === 'host' || role === 'client') role = ws ? 'lobby' : 'solo';
  },

  /* ---- starting a match ----
     Always resolves to {role:'solo'} or {role:'pending'}. 'solo' means
     there is nobody else at all — the caller should proceed exactly as it
     always has, no networking involved. 'pending' means a match_start is
     either already on its way to everyone (including us, if we ended up
     hosting) or we're waiting on someone else's — either way, the actual
     moment of entering the arena arrives uniformly through onMatchBegin,
     so the host and every client act on the exact same message. */
  begin(myClsKey, wantTotal){
    myClass = myClsKey;

    if(!this.configured){
      return Promise.resolve({role:'solo'});
    }

    const alreadyHosting = role === 'lobby' && hostId === myId;
    const iAmMember = role === 'lobby' && hostId !== myId;

    if(iAmMember){
      // not the host of this lobby — nothing to start, just keep waiting
      setStatus('Waiting for the host to start…');
      return Promise.resolve({role:'pending'});
    }

    const setup = alreadyHosting ? Promise.resolve() : this._findOrHost(wantTotal);
    return setup.then(() => this._searchAndStart(wantTotal));
  },

  /* Look for someone else's open game before opening our own. */
  _findOrHost(wantTotal){
    return directoryCall('/find?want=' + (wantTotal - 1))
      .then(res => {
        if(res && res.code){
          return this.joinLobby(res.code, myClass).then(() => {
            setStatus('Found a match — waiting for the host to start…');
          }).catch(() => this.createLobby(myClass));
        }
        return this.createLobby(myClass);
      })
      .catch(() => this.createLobby(myClass));
  },

  /* We're the host. If our lobby is short of humans, register with the
     directory and give it a few seconds before backfilling with bots.
     Always resolves to 'solo' (network never involved, act immediately)
     or 'pending' (a 'start' went out — the real entry happens later, off
     the *same* match_start broadcast every machine including this one
     receives, via onMatchBegin, so host and clients enter in lockstep). */
  _searchAndStart(wantTotal){
    if(role !== 'lobby' || hostId !== myId){
      // we ended up joining someone else's game instead of hosting
      return {role:'pending'};
    }
    const openSlots = wantTotal - members.length;
    if(openSlots <= 0) return this._startNow(wantTotal);

    directoryCall('/register', {method:'POST', headers:{'content-type':'application/json'},
      body: JSON.stringify({code, openSlots})});
    setStatus('Looking for other players…');

    return new Promise(resolve => {
      searchTimer = setTimeout(() => {
        directoryCall('/unregister', {method:'POST', headers:{'content-type':'application/json'},
          body: JSON.stringify({code})});
        resolve((role === 'lobby' && hostId === myId) ? this._startNow(wantTotal) : {role:'pending'});
      }, SEARCH_WINDOW_MS);
    });
  },

  /* Bot slots must be decided ONCE, here, by the host, and shipped as part
     of the roster — never re-rolled independently on each machine, or the
     host and its clients would each imagine a different bot in that slot.
     index.html supplies the actual picking logic (it owns BOT_NAMES and
     the class list); this only calls whatever it registered. */
  fillBots(n){ return []; },

  _startNow(wantTotal){
    if(members.length <= 1){
      // nobody joined — no reason to run the match over the network at all
      this.leaveLobby();
      return {role:'solo'};
    }
    const roster = members.map(m => ({
      uid: m.id, name: m.name, cls: m.cls || 'assassin', isBot:false
    }));
    this.fillBots(Math.max(0, wantTotal - roster.length)).forEach(b => {
      roster.push({uid:null, name:b.name, cls:b.cls, isBot:true});
    });
    send({t:'start', roster});
    return {role:'pending'};
  },

  /* ---- during a match ---- */

  sendInput(input){ if(this.inMatch) send({t:'input', input}); },
  latestInputFrom(uid){ return lastInputs[uid] || null; },
  sendSnapshot(data){ if(role === 'host') send({t:'snapshot', data}); },
  sendEvent(kind, uid, cls){ if(role === 'host') send({t:'event', kind, uid, cls}); },
  /* Rides the same 'input' relay as ordinary movement (the server only
     ever forwards 'input' to the host) — tagged so onMessage's 'input'
     case can tell a respawn choice apart from a movement frame. */
  sendRespawnChoice(cls){ send({t:'input', input:{__respawnCls: cls}}); },
  onRespawnChoice(fn){ this._respawnCb = fn; },   // host-only: fn(uid, cls)

  quitMatch(){
    this.leaveLobby();
  }
};

window.Net = Net;

})();
