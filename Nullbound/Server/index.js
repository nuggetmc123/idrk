// Nullbound name server: makes sure no two players pick the same name.
//
// POST /claim  {name, id, key, time, sig}
//   key  = the player's RSA public modulus (base64), id = its Player ID (first 5 bytes of SHA-256(key), hex, as XXXXX-XXXXX)
//   sig  = RSASSA-PKCS1-v1_5 / SHA-256 signature of "NB-NAME|<name>|<time>" made with the player's secret key
//   -> {ok:true} if the name is free or already this player's (the player's old name is released),
//      {ok:false, error:"taken"} (HTTP 409) if someone else owns it.
// GET /check?name=...  -> {ok:true, owner:"<Player ID or empty>"}
//
// One Durable Object (with its built-in SQLite storage) holds every name, so claims never race each other.

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === 'OPTIONS') return cors(new Response(null, { status: 204 }));
    const stub = env.NAMES.get(env.NAMES.idFromName('all-names'));
    return cors(await stub.fetch(request));
  },
};

function cors(res) {
  const r = new Response(res.body, res);
  r.headers.set('Access-Control-Allow-Origin', '*');
  r.headers.set('Access-Control-Allow-Headers', 'Content-Type');
  return r;
}

function json(obj, status = 200) {
  return new Response(JSON.stringify(obj), { status, headers: { 'Content-Type': 'application/json' } });
}

const NAME_RE = /^[A-Za-z0-9_]{3,16}$/;

function b64ToBytes(b64) {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

function bytesToB64Url(bytes) {
  let s = '';
  for (const b of bytes) s += String.fromCharCode(b);
  return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

async function playerId(modulus) {
  const hash = new Uint8Array(await crypto.subtle.digest('SHA-256', modulus));
  const hex = [...hash.slice(0, 5)].map(b => b.toString(16).padStart(2, '0').toUpperCase()).join('');
  return hex.slice(0, 5) + '-' + hex.slice(5, 10);
}

async function verify(modulus, message, signature) {
  const key = await crypto.subtle.importKey('jwk', { kty: 'RSA', n: bytesToB64Url(modulus), e: 'AQAB', alg: 'RS256', ext: true },
    { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
  return crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, signature, new TextEncoder().encode(message));
}

export class NameBook {
  constructor(state) {
    this.sql = state.storage.sql;
    this.sql.exec('CREATE TABLE IF NOT EXISTS names (lower TEXT PRIMARY KEY, name TEXT, owner TEXT, at INTEGER)');
  }

  async fetch(request) {
    const url = new URL(request.url);
    if (request.method === 'GET' && url.pathname === '/check') {
      const name = (url.searchParams.get('name') || '').trim();
      const row = [...this.sql.exec('SELECT owner FROM names WHERE lower = ?', name.toLowerCase())][0];
      return json({ ok: true, owner: row ? row.owner : '' });
    }
    if (request.method === 'POST' && url.pathname === '/claim') {
      let body;
      try { body = await request.json(); } catch { return json({ ok: false, error: 'bad request' }, 400); }
      const name = String(body.name || '').trim();
      if (!NAME_RE.test(name)) return json({ ok: false, error: 'invalid' }, 400);
      const time = Number(body.time) || 0;
      if (Math.abs(Date.now() / 1000 - time) > 600) return json({ ok: false, error: 'old' }, 400);
      let modulus, sig;
      try { modulus = b64ToBytes(String(body.key || '')); sig = b64ToBytes(String(body.sig || '')); } catch { return json({ ok: false, error: 'bad key' }, 400); }
      const id = await playerId(modulus);
      if (id !== String(body.id || '')) return json({ ok: false, error: 'bad id' }, 400);
      let good = false;
      try { good = await verify(modulus, 'NB-NAME|' + name + '|' + time, sig); } catch { good = false; }
      if (!good) return json({ ok: false, error: 'bad signature' }, 403);

      const lower = name.toLowerCase();
      const row = [...this.sql.exec('SELECT owner FROM names WHERE lower = ?', lower)][0];
      if (row && row.owner !== id) return json({ ok: false, error: 'taken', owner: row.owner }, 409);
      // One name per player: free their old one.
      this.sql.exec('DELETE FROM names WHERE owner = ? AND lower != ?', id, lower);
      this.sql.exec('INSERT OR REPLACE INTO names (lower, name, owner, at) VALUES (?, ?, ?, ?)', lower, name, id, Math.floor(Date.now() / 1000));
      return json({ ok: true });
    }
    return json({ ok: false, error: 'not found' }, 404);
  }
}
