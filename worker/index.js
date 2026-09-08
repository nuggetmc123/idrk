/* ============================================================
   LEMONADE LAUNCH multiplayer relay
   A Cloudflare Worker, deployed separately from the static game on
   GitHub Pages. This is real server code — the one thing GitHub Pages
   itself can never provide — and it does exactly two jobs:

     1. LobbyRoom  — one Durable Object instance per lobby code. Holds
        that lobby's live WebSocket connections and member list, and
        relays two kinds of message between them: presence (where you
        are, what you're doing) and events (you served a customer, you
        just got launched into orbit). See "why no host" below.

     2. Directory  — one singleton Durable Object that acts as a small
        bulletin board of "lobbies currently looking for more players",
        so quick-match can pair strangers together without anyone
        needing to share a code.

   Why no host, unlike this repo's previous game: each player's stand,
   customers and economy are simulated entirely on their own machine —
   there is no shared physics or shared score to keep everyone in sync
   about. Multiplayer here is a shared space to hang out in: you see
   your friends' stands, their avatars, and the chaos when one of them
   gets launched into the stratosphere. That means every member can
   broadcast their own presence/events directly — nobody needs to be
   trusted to narrate anyone else's game, because nobody's game depends
   on anyone else's state.

   Neither Durable Object ever runs game logic. They are dumb, honest
   relays, which is what keeps this small enough to actually finish.
   ============================================================ */

const MAX_MEMBERS = 8;             // a shared stand row only has so much room
const DIRECTORY_TTL_MS = 30000;    // an entry nobody refreshed in 30s is dead
const MAX_NAME_LEN = 16;

/* Kept in sync by hand with lemonade-net.js's copy of this same list and
   the same normalize/collapse logic — see the long comment over there for
   why it exists and what it isn't. This room's copy is the backstop: the
   client already filters before a name ever leaves the browser, but this
   is a public relay, and a modified client could skip that and send
   anything directly. A name that fails here is never rejected outright
   (a stranger's bad name shouldn't be able to kick anyone off the relay)
   — it's just quietly swapped for a neutral fallback before it ever
   reaches another player's screen. */
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
  const cleaned = stripInvisible(String(raw || '')).trim().slice(0, MAX_NAME_LEN);
  if(!cleaned || hasBlockedWord(cleaned)) return 'Player';
  return cleaned;
}

/* Shape-clamps a presence packet so one bad or hostile client can't send
   oversized/malformed JSON into every other player's browser via the
   broadcast. Values themselves are never trusted for anything beyond
   rendering — nobody's score or inventory depends on this. */
function sanitizePresence(p){
  if(!p || typeof p !== 'object') return null;
  const num = v => (typeof v === 'number' && isFinite(v)) ? Math.max(-9999, Math.min(9999, v)) : 0;
  return {
    x: num(p.x), y: num(p.y), z: num(p.z),
    rot: num(p.rot),
    anim: String(p.anim || 'idle').slice(0, 24),
    location: String(p.location || 'neighborhood').slice(0, 24),
    coins: Math.max(0, Math.min(999999, Math.floor(Number(p.coins) || 0)))
  };
}

function sanitizeEvent(e){
  if(!e || typeof e !== 'object') return null;
  return {
    kind: String(e.kind || '').slice(0, 24),
    detail: String(e.detail || '').slice(0, 80)
  };
}

function json(data, status){
  return new Response(JSON.stringify(data), {
    status: status || 200,
    headers: {'content-type': 'application/json', 'access-control-allow-origin': '*'}
  });
}

/* ---------- LobbyRoom ---------- */

export class LobbyRoom {
  constructor(state, env){
    this.state = state;
    this.sockets = new Map();     // uid -> WebSocket
    this.members = new Map();     // uid -> {name}
    this.presence = new Map();    // uid -> last sanitizePresence() result, for newcomers
  }

  async fetch(request){
    if(request.headers.get('Upgrade') !== 'websocket'){
      return new Response('expected a websocket upgrade', {status:400});
    }
    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    server.accept();
    this.attach(server);
    return new Response(null, {status:101, webSocket:client});
  }

  attach(ws){
    ws.addEventListener('message', ev => {
      let msg;
      try{ msg = JSON.parse(ev.data); }catch(e){ return; }
      this.onMessage(ws, msg);
    });
    const drop = () => { if(ws._uid) this.removeMember(ws._uid); };
    ws.addEventListener('close', drop);
    ws.addEventListener('error', drop);
  }

  send(ws, obj){
    try{ ws.send(JSON.stringify(obj)); }catch(e){}
  }

  broadcast(obj, exceptUid){
    const text = JSON.stringify(obj);
    for(const [uid, ws] of this.sockets){
      if(uid === exceptUid) continue;
      try{ ws.send(text); }catch(e){}
    }
  }

  rosterPayload(){
    return {
      t:'roster',
      members: Array.from(this.members, ([id, m]) => ({id, name:m.name}))
    };
  }

  removeMember(uid){
    this.sockets.delete(uid);
    this.members.delete(uid);
    this.presence.delete(uid);
    this.broadcast({t:'roster', members: Array.from(this.members, ([id, m]) => ({id, name:m.name}))});
    this.broadcast({t:'left', uid});
  }

  onMessage(ws, msg){
    switch(msg.t){
      case 'join': {
        const uid = String(msg.uid || '').slice(0, 64) || crypto.randomUUID();
        if(!this.members.has(uid) && this.members.size >= MAX_MEMBERS){
          this.send(ws, {t:'full'});
          ws.close();
          return;
        }
        ws._uid = uid;
        this.sockets.set(uid, ws);
        this.members.set(uid, {name: sanitizeName(msg.name)});

        this.send(ws, {t:'joined', uid});
        // catch the newcomer up on everyone already here before they get
        // their first live presence tick
        this.send(ws, {t:'roster', members: Array.from(this.members, ([id, m]) => ({id, name:m.name}))});
        for(const [id, p] of this.presence) this.send(ws, {t:'presence', from:id, p});
        this.broadcast(this.rosterPayload(), uid);
        break;
      }
      case 'setName': {
        if(!ws._uid || !this.members.has(ws._uid)) return;
        this.members.get(ws._uid).name = sanitizeName(msg.name);
        this.broadcast(this.rosterPayload());
        break;
      }
      case 'leave': {
        if(ws._uid) this.removeMember(ws._uid);
        break;
      }
      case 'presence': {
        if(!ws._uid || !this.members.has(ws._uid)) return;
        const p = sanitizePresence(msg.p);
        if(!p) return;
        this.presence.set(ws._uid, p);
        this.broadcast({t:'presence', from: ws._uid, p}, ws._uid);
        break;
      }
      case 'event': {
        // one-off moments — served a customer, got launched, unlocked an
        // achievement — rendered as a toast/effect on every OTHER screen.
        // Never cached: a newcomer doesn't need to see history replay.
        if(!ws._uid || !this.members.has(ws._uid)) return;
        const e = sanitizeEvent(msg.e);
        if(!e) return;
        this.broadcast({t:'event', from: ws._uid, e}, ws._uid);
        break;
      }
    }
  }
}

/* ---------- Directory ---------- */

export class Directory {
  constructor(state, env){
    this.state = state;
    this.open = new Map();    // code -> {code, openSlots, ts}
  }

  prune(){
    const now = Date.now();
    for(const [code, e] of this.open) if(now - e.ts > DIRECTORY_TTL_MS) this.open.delete(code);
  }

  async fetch(request){
    const url = new URL(request.url);
    this.prune();

    if(request.method === 'OPTIONS') return json({});

    if(request.method === 'POST' && url.pathname === '/register'){
      const body = await request.json().catch(() => ({}));
      const code = String(body.code || '').slice(0, 12);
      const openSlots = Math.max(0, Math.min(MAX_MEMBERS - 1, +body.openSlots || 0));
      if(!code) return json({error:'missing code'}, 400);
      this.open.set(code, {code, openSlots, ts: Date.now()});
      return json({ok:true});
    }

    if(request.method === 'POST' && url.pathname === '/unregister'){
      const body = await request.json().catch(() => ({}));
      this.open.delete(String(body.code || ''));
      return json({ok:true});
    }

    if(request.method === 'GET' && url.pathname === '/find'){
      const skip = url.searchParams.get('skip') || '';
      let best = null;
      for(const e of this.open.values()){
        if(e.code === skip || e.openSlots < 1) continue;
        if(!best || e.ts < best.ts) best = e;
      }
      return json(best ? {code: best.code} : {none:true});
    }

    return new Response('not found', {status:404});
  }
}

/* ---------- router ---------- */

export default {
  async fetch(request, env){
    const url = new URL(request.url);

    if(url.pathname.startsWith('/directory/')){
      const id = env.DIRECTORY.idFromName('singleton');
      const stub = env.DIRECTORY.get(id);
      const inner = new Request(url.origin + url.pathname.slice('/directory'.length) + url.search, request);
      return stub.fetch(inner);
    }

    if(url.pathname.startsWith('/room/')){
      const code = decodeURIComponent(url.pathname.split('/')[2] || '');
      if(!code) return new Response('missing lobby code', {status:400});
      const id = env.LOBBY.idFromName(code);
      const stub = env.LOBBY.get(id);
      return stub.fetch(request);
    }

    return new Response('LEMONADE LAUNCH multiplayer relay is running.', {status:200});
  }
};
