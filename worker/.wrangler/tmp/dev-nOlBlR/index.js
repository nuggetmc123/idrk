var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// index.js
var MAX_MEMBERS = 4;
var DIRECTORY_TTL_MS = 3e4;
function json(data, status) {
  return new Response(JSON.stringify(data), {
    status: status || 200,
    headers: { "content-type": "application/json", "access-control-allow-origin": "*" }
  });
}
__name(json, "json");
var LobbyRoom = class {
  static {
    __name(this, "LobbyRoom");
  }
  constructor(state, env) {
    this.state = state;
    this.sockets = /* @__PURE__ */ new Map();
    this.members = /* @__PURE__ */ new Map();
    this.hostId = null;
    this.started = false;
  }
  async fetch(request) {
    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response("expected a websocket upgrade", { status: 400 });
    }
    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    server.accept();
    this.attach(server);
    return new Response(null, { status: 101, webSocket: client });
  }
  attach(ws) {
    ws.addEventListener("message", (ev) => {
      let msg;
      try {
        msg = JSON.parse(ev.data);
      } catch (e) {
        return;
      }
      this.onMessage(ws, msg);
    });
    const drop = /* @__PURE__ */ __name(() => {
      if (ws._uid) this.removeMember(ws._uid);
    }, "drop");
    ws.addEventListener("close", drop);
    ws.addEventListener("error", drop);
  }
  send(ws, obj) {
    try {
      ws.send(JSON.stringify(obj));
    } catch (e) {
    }
  }
  broadcast(obj, exceptUid) {
    const text = JSON.stringify(obj);
    for (const [uid, ws] of this.sockets) {
      if (uid === exceptUid) continue;
      try {
        ws.send(text);
      } catch (e) {
      }
    }
  }
  rosterPayload() {
    return {
      t: "roster",
      hostId: this.hostId,
      members: Array.from(this.members, ([id, m]) => ({ id, name: m.name, cls: m.cls }))
    };
  }
  removeMember(uid) {
    this.sockets.delete(uid);
    this.members.delete(uid);
    if (uid === this.hostId) {
      if (this.started) {
        this.broadcast({ t: "host_left" });
        this.started = false;
        this.hostId = null;
        return;
      }
      this.hostId = this.members.size ? this.members.keys().next().value : null;
    }
    this.broadcast(this.rosterPayload());
  }
  onMessage(ws, msg) {
    switch (msg.t) {
      case "join": {
        const uid = String(msg.uid || "").slice(0, 64) || crypto.randomUUID();
        if (!this.members.has(uid) && this.members.size >= MAX_MEMBERS) {
          this.send(ws, { t: "full" });
          ws.close();
          return;
        }
        ws._uid = uid;
        this.sockets.set(uid, ws);
        this.members.set(uid, { name: String(msg.name || "Player").slice(0, 24), cls: msg.cls || null });
        if (!this.hostId) this.hostId = uid;
        this.send(ws, { t: "joined", uid, hostId: this.hostId });
        this.broadcast(this.rosterPayload());
        break;
      }
      case "setClass": {
        if (!ws._uid || !this.members.has(ws._uid)) return;
        this.members.get(ws._uid).cls = msg.cls || null;
        this.broadcast(this.rosterPayload());
        break;
      }
      case "leave": {
        if (ws._uid) this.removeMember(ws._uid);
        break;
      }
      case "start": {
        if (ws._uid !== this.hostId) return;
        this.started = true;
        this.broadcast({ t: "match_start", roster: msg.roster, hostId: this.hostId });
        break;
      }
      case "input": {
        const hostWs = this.sockets.get(this.hostId);
        if (hostWs && ws._uid) this.send(hostWs, { t: "input", from: ws._uid, input: msg.input });
        break;
      }
      case "snapshot": {
        if (ws._uid !== this.hostId) return;
        this.broadcast({ t: "snapshot", data: msg.data }, this.hostId);
        break;
      }
      case "event": {
        if (ws._uid !== this.hostId) return;
        this.broadcast({ t: "event", kind: msg.kind, uid: msg.uid, cls: msg.cls }, this.hostId);
        break;
      }
    }
  }
};
var Directory = class {
  static {
    __name(this, "Directory");
  }
  constructor(state, env) {
    this.state = state;
    this.open = /* @__PURE__ */ new Map();
  }
  prune() {
    const now = Date.now();
    for (const [code, e] of this.open) if (now - e.ts > DIRECTORY_TTL_MS) this.open.delete(code);
  }
  async fetch(request) {
    const url = new URL(request.url);
    this.prune();
    if (request.method === "OPTIONS") return json({});
    if (request.method === "POST" && url.pathname === "/register") {
      const body = await request.json().catch(() => ({}));
      const code = String(body.code || "").slice(0, 12);
      const openSlots = Math.max(0, Math.min(3, +body.openSlots || 0));
      if (!code) return json({ error: "missing code" }, 400);
      this.open.set(code, { code, openSlots, ts: Date.now() });
      return json({ ok: true });
    }
    if (request.method === "POST" && url.pathname === "/unregister") {
      const body = await request.json().catch(() => ({}));
      this.open.delete(String(body.code || ""));
      return json({ ok: true });
    }
    if (request.method === "GET" && url.pathname === "/find") {
      const skip = url.searchParams.get("skip") || "";
      let best = null;
      for (const e of this.open.values()) {
        if (e.code === skip || e.openSlots < 1) continue;
        if (!best || e.ts < best.ts) best = e;
      }
      return json(best ? { code: best.code } : { none: true });
    }
    return new Response("not found", { status: 404 });
  }
};
var index_default = {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith("/directory/")) {
      const id = env.DIRECTORY.idFromName("singleton");
      const stub = env.DIRECTORY.get(id);
      const inner = new Request(url.origin + url.pathname.slice("/directory".length) + url.search, request);
      return stub.fetch(inner);
    }
    if (url.pathname.startsWith("/room/")) {
      const code = decodeURIComponent(url.pathname.split("/")[2] || "");
      if (!code) return new Response("missing lobby code", { status: 400 });
      const id = env.LOBBY.idFromName(code);
      const stub = env.LOBBY.get(id);
      return stub.fetch(request);
    }
    return new Response("BRAWLBOUND multiplayer relay is running.", { status: 200 });
  }
};

// ../../../../root/.npm/_npx/d77349f55c2be1c0/node_modules/wrangler/templates/middleware/middleware-ensure-req-body-drained.ts
var drainBody = /* @__PURE__ */ __name(async (request, env, _ctx, middlewareCtx) => {
  try {
    return await middlewareCtx.next(request, env);
  } finally {
    try {
      if (request.body !== null && !request.bodyUsed) {
        const reader = request.body.getReader();
        while (!(await reader.read()).done) {
        }
      }
    } catch (e) {
      console.error("Failed to drain the unused request body.", e);
    }
  }
}, "drainBody");
var middleware_ensure_req_body_drained_default = drainBody;

// ../../../../root/.npm/_npx/d77349f55c2be1c0/node_modules/wrangler/templates/middleware/middleware-miniflare3-json-error.ts
function reduceError(e) {
  return {
    name: e?.name,
    message: e?.message ?? String(e),
    stack: e?.stack,
    cause: e?.cause === void 0 ? void 0 : reduceError(e.cause)
  };
}
__name(reduceError, "reduceError");
var jsonError = /* @__PURE__ */ __name(async (request, env, _ctx, middlewareCtx) => {
  try {
    return await middlewareCtx.next(request, env);
  } catch (e) {
    const error = reduceError(e);
    const body = JSON.stringify(error);
    const headers = {
      "Content-Type": "application/json",
      "MF-Experimental-Error-Stack": "true"
    };
    const encoded = encodeURIComponent(body);
    if (encoded.length <= 8192) {
      headers["MF-Experimental-Error-Stack-Payload"] = encoded;
    }
    return new Response(body, { status: 500, headers });
  }
}, "jsonError");
var middleware_miniflare3_json_error_default = jsonError;

// .wrangler/tmp/bundle-x6GBnL/middleware-insertion-facade.js
var __INTERNAL_WRANGLER_MIDDLEWARE__ = [
  middleware_ensure_req_body_drained_default,
  middleware_miniflare3_json_error_default
];
var middleware_insertion_facade_default = index_default;

// ../../../../root/.npm/_npx/d77349f55c2be1c0/node_modules/wrangler/templates/middleware/common.ts
var __facade_middleware__ = [];
function __facade_register__(...args) {
  __facade_middleware__.push(...args.flat());
}
__name(__facade_register__, "__facade_register__");
function __facade_invokeChain__(request, env, ctx, dispatch, middlewareChain) {
  const [head, ...tail] = middlewareChain;
  const middlewareCtx = {
    dispatch,
    next(newRequest, newEnv) {
      return __facade_invokeChain__(newRequest, newEnv, ctx, dispatch, tail);
    }
  };
  return head(request, env, ctx, middlewareCtx);
}
__name(__facade_invokeChain__, "__facade_invokeChain__");
function __facade_invoke__(request, env, ctx, dispatch, finalMiddleware) {
  return __facade_invokeChain__(request, env, ctx, dispatch, [
    ...__facade_middleware__,
    finalMiddleware
  ]);
}
__name(__facade_invoke__, "__facade_invoke__");

// .wrangler/tmp/bundle-x6GBnL/middleware-loader.entry.ts
var __Facade_ScheduledController__ = class ___Facade_ScheduledController__ {
  constructor(scheduledTime, cron, noRetry) {
    this.scheduledTime = scheduledTime;
    this.cron = cron;
    this.#noRetry = noRetry;
  }
  scheduledTime;
  cron;
  static {
    __name(this, "__Facade_ScheduledController__");
  }
  #noRetry;
  noRetry() {
    if (!(this instanceof ___Facade_ScheduledController__)) {
      throw new TypeError("Illegal invocation");
    }
    this.#noRetry();
  }
};
function wrapExportedHandler(worker) {
  if (__INTERNAL_WRANGLER_MIDDLEWARE__ === void 0 || __INTERNAL_WRANGLER_MIDDLEWARE__.length === 0) {
    return worker;
  }
  for (const middleware of __INTERNAL_WRANGLER_MIDDLEWARE__) {
    __facade_register__(middleware);
  }
  const fetchDispatcher = /* @__PURE__ */ __name(function(request, env, ctx) {
    if (worker.fetch === void 0) {
      throw new Error("Handler does not export a fetch() function.");
    }
    return worker.fetch(request, env, ctx);
  }, "fetchDispatcher");
  return {
    ...worker,
    fetch(request, env, ctx) {
      const dispatcher = /* @__PURE__ */ __name(function(type, init) {
        if (type === "scheduled" && worker.scheduled !== void 0) {
          const controller = new __Facade_ScheduledController__(
            Date.now(),
            init.cron ?? "",
            () => {
            }
          );
          return worker.scheduled(controller, env, ctx);
        }
      }, "dispatcher");
      return __facade_invoke__(request, env, ctx, dispatcher, fetchDispatcher);
    }
  };
}
__name(wrapExportedHandler, "wrapExportedHandler");
function wrapWorkerEntrypoint(klass) {
  if (__INTERNAL_WRANGLER_MIDDLEWARE__ === void 0 || __INTERNAL_WRANGLER_MIDDLEWARE__.length === 0) {
    return klass;
  }
  for (const middleware of __INTERNAL_WRANGLER_MIDDLEWARE__) {
    __facade_register__(middleware);
  }
  return class extends klass {
    #fetchDispatcher = /* @__PURE__ */ __name((request, env, ctx) => {
      this.env = env;
      this.ctx = ctx;
      if (super.fetch === void 0) {
        throw new Error("Entrypoint class does not define a fetch() function.");
      }
      return super.fetch(request);
    }, "#fetchDispatcher");
    #dispatcher = /* @__PURE__ */ __name((type, init) => {
      if (type === "scheduled" && super.scheduled !== void 0) {
        const controller = new __Facade_ScheduledController__(
          Date.now(),
          init.cron ?? "",
          () => {
          }
        );
        return super.scheduled(controller);
      }
    }, "#dispatcher");
    fetch(request) {
      return __facade_invoke__(
        request,
        this.env,
        this.ctx,
        this.#dispatcher,
        this.#fetchDispatcher
      );
    }
  };
}
__name(wrapWorkerEntrypoint, "wrapWorkerEntrypoint");
var WRAPPED_ENTRY;
if (typeof middleware_insertion_facade_default === "object") {
  WRAPPED_ENTRY = wrapExportedHandler(middleware_insertion_facade_default);
} else if (typeof middleware_insertion_facade_default === "function") {
  WRAPPED_ENTRY = wrapWorkerEntrypoint(middleware_insertion_facade_default);
}
var middleware_loader_entry_default = WRAPPED_ENTRY;
export {
  Directory,
  LobbyRoom,
  __INTERNAL_WRANGLER_MIDDLEWARE__,
  middleware_loader_entry_default as default
};
//# sourceMappingURL=index.js.map
