/* ============================================================
   Flopzone game server — a Cloudflare Worker.

   Everything lives in ONE Durable Object instance ("global"): player
   accounts, the unique-name index, friends, parties, the hub, every
   match, the chat auto-mod, reports and moderator actions. One object
   keeps every rule in one place with no cross-object messaging, which
   is plenty for a hobby game's player count (see README for limits).

   Routes:
     /ws      WebSocket the headset connects to (see protocol in game.js)
     /admin   moderator actions over plain HTTPS, guarded by ADMIN_KEY
     /        health check
   ============================================================ */

export { GameServer } from './game.js';

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === '/ws' || url.pathname === '/admin') {
      const id = env.GAME.idFromName('global');
      return env.GAME.get(id).fetch(request);
    }
    return new Response('Flopzone server is running.', {
      headers: { 'content-type': 'text/plain', 'access-control-allow-origin': '*' }
    });
  }
};
