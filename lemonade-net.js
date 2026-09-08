/* ============================================================
   ONLINE MULTIPLAYER
   Talks to the Cloudflare Worker in worker/index.js over a plain
   WebSocket. No WebRTC, no third-party SDK — just JSON messages
   through a relay the Worker owns.

   Unlike this repo's previous game, there is no "host" here. Your
   customers, your recipes and your coins are simulated entirely on
   your own machine — nobody else's game depends on them, so nobody
   needs to be trusted to narrate anyone else's state. Every member of
   a lobby just broadcasts its own PRESENCE (where your avatar is, what
   it's doing) and EVENTS (you served someone, you got launched into
   orbit) straight to the room, and renders whatever everyone else
   broadcasts. See worker/README.md for the fuller version of this.

   RELAY_URL is left blank until the Worker is deployed (see
   worker/README.md). Blank means the game is solo — every screen is
   just your own stand, i.e. the game behaves exactly as it did before
   this file existed.
   ============================================================ */
(function(){
"use strict";

/* wss://your-worker.your-subdomain.workers.dev — set after `wrangler deploy`. */
const RELAY_URL = '';

const ID_KEY          = 'lemonade-guest-id';
const NAME_KEY        = 'lemonade-guest-name';        // auto-generated "GuestNNNN" fallback
const CUSTOM_NAME_KEY = 'lemonade-custom-name';        // what setMyName() actually saves
const MAX_NAME_LEN    = 16;
const PRESENCE_HZ     = 8;           // outgoing presence packets per second, at most
const REGISTER_EVERY_MS = 15000;     // keep a quick-match listing alive while waiting

/* A lightweight, English-only word filter — normalizes common leetspeak
   and collapses repeated letters before checking for a blocked word as a
   substring, so "fuuuck"/"fu(k"/"F.U.C.K" all still get caught. This is
   not real moderation — it exists to stop the ordinary case of someone
   typing a slur or cuss word into the name box, on both this client and
   (see worker/index.js, which keeps its own copy of this same list) the
   relay itself, so a modified client bypassing this file can't get an
   ugly name to other real players either. */
const BLOCKED_NAME_WORDS = [
  'fuck','shit','bitch','asshole','bastard','cunt','dick','pussy','whore','slut',
  'fag','faggot','nigger','nigga','chink','spic','kike','gook','tranny','retard',
  'communis'
].map(collapseRepeats);

function collapseRepeats(s){ return s.replace(/(.)\1+/g, '$1'); }

function normalizeForFilter(s){
  return collapseRepeats(
    s.toLowerCase()
      .replace(/[04]/g, 'o').replace(/1/g, 'i').replace(/3/g, 'e')
      .replace(/5/g, 's').replace(/7/g, 't').replace(/\$/g, 's').replace(/@/g, 'a')
      .replace(/[^a-z]/g, '')
  );
}

function hasBlockedWord(s){
  const norm = normalizeForFilter(s);
  return BLOCKED_NAME_WORDS.some(w => norm.includes(w));
}

function stripInvisible(s){
  return s.replace(/[\x00-\x1f\x7f\u200b-\u200f\u202a-\u202e\ufeff]/g, '');
}

function sanitizeName(raw){
  const cleaned = stripInvisible(String(raw == null ? '' : raw)).trim().slice(0, MAX_NAME_LEN);
  if(!cleaned) return {ok:false, reason:'Type a name first.'};
  if(hasBlockedWord(cleaned)) return {ok:false, reason:"That name isn't allowed."};
  return {ok:true, name:cleaned};
}

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
function removeLocal(key){
  try{ localStorage.removeItem(key); }catch(e){}
}

let myId = readLocal(ID_KEY, null);
if(!myId){ myId = randomId(); writeLocal(ID_KEY, myId); }

let guestName = readLocal(NAME_KEY, null);
if(!guestName){ guestName = 'Guest' + Math.floor(Math.random() * 9000 + 1000); writeLocal(NAME_KEY, guestName); }

let customName = readLocal(CUSTOM_NAME_KEY, null);   // set via Net.setMyName() — beats everything else

/* ---------- connection state ---------- */

let ws = null;
let code = null;
let members = [];              // [{id, name}]
let registerTimer = null;
let lastPresenceSent = 0;

const lobbyListeners = [];
const presenceListeners = [];
const eventListeners = [];
const peerLeftListeners = [];
const statusListeners = [];

function notify(list, arg){ list.forEach(fn => { try{ fn(arg); }catch(e){} }); }
function setStatus(text){ notify(statusListeners, text); }

function myName(){
  return customName || (window.Save && window.Save.signedIn && window.Save.displayName) || guestName;
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
      sock.send(JSON.stringify({t:'join', uid: myId, name: myName()}));
    });
    sock.addEventListener('message', ev => {
      let msg; try{ msg = JSON.parse(ev.data); }catch(e){ return; }
      if(!settled && msg.t === 'joined'){
        settled = true;
        clearTimeout(timeout);
        ws = sock; code = roomCode;
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
  ws = null; code = null; members = [];
  clearInterval(registerTimer); registerTimer = null;
  notify(lobbyListeners);
}

function onMessage(msg){
  switch(msg.t){
    case 'roster':
      members = msg.members;
      notify(lobbyListeners);
      break;
    case 'left':
      notify(peerLeftListeners, msg.uid);
      break;
    case 'presence':
      notify(presenceListeners, {from: msg.from, p: msg.p});
      break;
    case 'event':
      notify(eventListeners, {from: msg.from, e: msg.e});
      if(window.Save) window.Save.markMultiplayerPlayed();
      break;
  }
}

/* ---------- Directory (quick match) ---------- */

function directoryCall(path, opts){
  return fetch(RELAY_URL.replace(/^wss:/, 'https:').replace(/^ws:/, 'http:') + '/directory' + path, opts)
    .then(r => r.json()).catch(() => ({none:true, error:true}));
}

/* ---------- public API ---------- */

const Net = {
  get configured(){ return !!RELAY_URL; },
  get myId(){ return myId; },
  get myName(){ return myName(); },
  get myCustomName(){ return customName || ''; },   // '' means "no custom name set" — for prefilling the name field
  get inLobby(){ return !!ws; },
  get lobby(){ return ws ? {code, members: members.slice()} : null; },
  get playerCount(){ return ws ? members.length : 1; },

  onLobbyChange(fn){ lobbyListeners.push(fn); },
  onPresence(fn){ presenceListeners.push(fn); },
  onEvent(fn){ eventListeners.push(fn); },
  onPeerLeft(fn){ peerLeftListeners.push(fn); },
  onStatus(fn){ statusListeners.push(fn); },

  /* ---- lobby lifecycle ---- */

  createLobby(){
    return openRoom(randomCode()).then(() => code);
  },

  joinLobby(rawCode){
    const c = String(rawCode || '').trim().toUpperCase();
    if(!c) return Promise.reject(new Error('empty code'));
    return openRoom(c).then(() => code);
  },

  /* Try to find someone else's open lobby first; if nobody's out there,
     host one ourselves and keep it listed until someone joins or we
     give up. Resolves {found:true, code} or {found:false, code}. */
  quickMatch(){
    if(!this.configured) return Promise.reject(new Error('not configured'));
    setStatus('Looking for other players…');
    return directoryCall('/find').then(res => {
      if(res && res.code){
        return this.joinLobby(res.code).then(() => {
          setStatus('Joined a lobby!');
          return {found:true, code};
        }).catch(() => this._hostAndList());
      }
      return this._hostAndList();
    });
  },

  _hostAndList(){
    return this.createLobby().then(() => {
      setStatus('Waiting for other players to find you…');
      this._registerListing();
      registerTimer = setInterval(() => this._registerListing(), REGISTER_EVERY_MS);
      return {found:false, code};
    });
  },

  _registerListing(){
    if(!code) return;
    directoryCall('/register', {method:'POST', headers:{'content-type':'application/json'},
      body: JSON.stringify({code, openSlots: 7})});
  },

  leaveLobby(){
    if(code) directoryCall('/unregister', {method:'POST', headers:{'content-type':'application/json'},
      body: JSON.stringify({code})});
    if(ws){ send({t:'leave'}); ws.close(); }
    teardown();
  },

  /* Validates and saves a name someone typed into the name field — see
     sanitizeName()/BLOCKED_NAME_WORDS above. Returns {ok:true, name} or
     {ok:false, reason} for the UI to show right back; nothing is changed
     on a rejection. An empty/whitespace `raw` clears the custom name and
     falls back to the account name or the random guest one instead of
     erroring, since "I don't want a custom name" is a valid choice. */
  setMyName(raw){
    if(String(raw == null ? '' : raw).trim() === ''){
      customName = null;
      removeLocal(CUSTOM_NAME_KEY);
      if(ws) send({t:'setName', name: myName()});
      return {ok:true, name:''};
    }
    const r = sanitizeName(raw);
    if(!r.ok) return r;
    customName = r.name;
    writeLocal(CUSTOM_NAME_KEY, customName);
    if(ws) send({t:'setName', name: myName()});
    return r;
  },

  /* ---- while in a lobby ----
     Throttled to PRESENCE_HZ regardless of how often the render loop
     calls it, so a 60fps game loop doesn't flood the relay. */
  sendPresence(p){
    if(!ws) return;
    const t = performance.now();
    if(t - lastPresenceSent < 1000 / PRESENCE_HZ) return;
    lastPresenceSent = t;
    send({t:'presence', p});
  },

  sendEvent(kind, detail){
    if(!ws) return;
    send({t:'event', e:{kind, detail: detail || ''}});
  }
};

window.Net = Net;

window.addEventListener('pagehide', () => { if(ws) Net.leaveLobby(); });

})();
