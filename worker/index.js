/* ============================================================
   BRAWLBOUND multiplayer relay
   A Cloudflare Worker, deployed separately from the static game on
   GitHub Pages. This is real server code — the one thing GitHub Pages
   itself can never provide — and it does exactly two jobs:

     1. LobbyRoom  — one Durable Object instance per lobby code. Holds
        that lobby's live WebSocket connections and its member list,
        and relays match traffic (input from clients to the host,
        state snapshots from the host to everyone else) once a match
        starts. The room never simulates the fight itself — the host's
        own browser does that, same as it already does for bots today.

     2. Directory  — one singleton Durable Object that acts as a small
        bulletin board of "lobbies currently looking for more players",
        so quick-match can pair strangers together without anyone
        needing to share a code.

   Neither object ever runs game logic. They are dumb, honest relays —
   which is what keeps this small enough to actually finish, and keeps
   the trust model simple: the host's machine is trusted the same way
   it already is in local play against bots.
   ============================================================ */

const MAX_MEMBERS = 8;             // matches the game's 8-fighter arena
const DIRECTORY_TTL_MS = 30000;    // an entry nobody refreshed in 30s is dead
const UPGRADE_TRACKS = ['dmg', 'spd', 'vit'];
const UPGRADE_MAX_LEVEL = 5;

/* A player's own upgrade levels ride along on join/setClass so the host
   can apply the right bonus to a remote human's fighter. This room never
   checks whether the levels claimed were actually paid for — that trust
   boundary is the same one bots already crossed (the host is trusted to
   run the fight honestly) — it only clamps the SHAPE so one bad or hostile
   client can't send oversized/malformed JSON into every other player's
   browser via the roster broadcast. */
function sanitizeUpg(u){
  if(!u || typeof u !== 'object') return null;
  const out = {};
  let any = false;
  for(const k of UPGRADE_TRACKS){
    const v = u[k];
    if(typeof v === 'number' && v > 0){ out[k] = Math.min(UPGRADE_MAX_LEVEL, Math.floor(v)); any = true; }
  }
  return any ? out : null;
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
    this.sockets = new Map();   // uid -> WebSocket
    this.members = new Map();   // uid -> {name, cls}
    this.hostId = null;
    this.started = false;
    this.roster = null;         // the live match roster once 'start' fires — see 'join' below
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
      t:'roster', hostId:this.hostId,
      members: Array.from(this.members, ([id, m]) => ({id, name:m.name, cls:m.cls, upg:m.upg}))
    };
  }

  removeMember(uid){
    this.sockets.delete(uid);
    this.members.delete(uid);
    if(uid === this.hostId){
      if(this.started){
        // no mid-match host migration in v1 — say so plainly and stop
        this.broadcast({t:'host_left'});
        this.started = false;
        this.hostId = null;
        return;
      }
      // still in the lobby, not fighting yet — hand the room to whoever's left
      this.hostId = this.members.size ? this.members.keys().next().value : null;
    }
    this.broadcast(this.rosterPayload());
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
        const name = String(msg.name || 'Player').slice(0, 24);
        this.members.set(uid, {name, cls: msg.cls || null, upg: sanitizeUpg(msg.upg)});
        if(!this.hostId) this.hostId = uid;

        // A match is already running and has an open bot seat — drop this
        // person straight into it instead of parking them in the lobby for
        // the next round. They inherit that seat's CURRENT fighter (class,
        // score, position, HP all live only in the host's own simulation)
        // rather than whatever they picked on the menu — swapping character
        // mid-fight out from under a live HP bar would be its own kind of
        // bug. Everyone else just gets a lightweight name/uid patch; only
        // the new arrival needs the full roster to enter the match at all.
        // (A genuine reconnect of an already-human seat isn't handled here —
        // this only ever claims a seat still flagged as a bot.)
        if(this.started && this.roster){
          const seat = this.roster.findIndex(r => r.isBot);
          if(seat !== -1){
            this.roster[seat] = {uid, name, cls: this.roster[seat].cls, isBot:false};
            this.send(ws, {t:'joined', uid, hostId:this.hostId});
            this.send(ws, {t:'match_start', roster: this.roster, hostId: this.hostId});
            this.broadcast({t:'seat_taken', seat, uid, name}, uid);
            return;
          }
        }

        this.send(ws, {t:'joined', uid, hostId:this.hostId});
        this.broadcast(this.rosterPayload());
        break;
      }
      case 'setClass': {
        if(!ws._uid || !this.members.has(ws._uid)) return;
        const m = this.members.get(ws._uid);
        m.cls = msg.cls || null;
        m.upg = sanitizeUpg(msg.upg);
        this.broadcast(this.rosterPayload());
        break;
      }
      case 'leave': {
        if(ws._uid) this.removeMember(ws._uid);
        break;
      }
      case 'start': {
        if(ws._uid !== this.hostId) return;      // only the host may start
        this.started = true;
        this.roster = msg.roster;
        this.broadcast({t:'match_start', roster: msg.roster, hostId: this.hostId});
        break;
      }
      case 'matchEnded': {
        // the host leaving the game screen (back to menu, or about to send a
        // fresh 'start' for a rematch) — stop offering this match's roster
        // as a bot seat to hand to the next 'join' that comes in
        if(ws._uid !== this.hostId) return;
        this.started = false;
        this.roster = null;
        break;
      }
      case 'input': {
        // clients only ever talk to the host — this room does not read it
        const hostWs = this.sockets.get(this.hostId);
        if(hostWs && ws._uid) this.send(hostWs, {t:'input', from: ws._uid, input: msg.input});
        break;
      }
      case 'snapshot': {
        if(ws._uid !== this.hostId) return;      // only the host may narrate the fight
        this.broadcast({t:'snapshot', data: msg.data}, this.hostId);
        break;
      }
      case 'event': {
        // per-player moments (an elimination, a death) so each machine can
        // credit its own career/battle-pass progress — see README
        if(ws._uid !== this.hostId) return;
        this.broadcast({t:'event', kind: msg.kind, uid: msg.uid, cls: msg.cls}, this.hostId);
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

    return new Response('BRAWLBOUND multiplayer relay is running.', {status:200});
  }
};
