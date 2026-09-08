/* ============================================================
   Minimal service worker — exists mainly so the browser considers this
   page an installable app (Chrome/Edge/Xbox's install criteria require
   one with a fetch handler), with real offline play as a side benefit
   once everything's been loaded at least once.

   Network-first, falling back to whatever's cached: this game changes
   often (see the cache-busting note in README.md), so always preferring
   a fresh network response over a possibly-stale cached one is the right
   default — the cache is a safety net for "offline entirely," not a
   performance optimization to fight with deploys over.
   ============================================================ */
const CACHE = 'lemonade-launch-v1';

self.addEventListener('install', () => {
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  if(event.request.method !== 'GET') return;
  event.respondWith(
    fetch(event.request)
      .then(res => {
        const copy = res.clone();
        caches.open(CACHE).then(c => c.put(event.request, copy)).catch(() => {});
        return res;
      })
      .catch(() => caches.match(event.request))
  );
});
