/* ============================================================
   LEMONADE LAUNCH — menu wiring + the 3D game itself.

   Layout of this file:
     1. small DOM/format helpers
     2. menu screens (location select, upgrades, recipes, achievements,
        multiplayer panel) — all data-driven off lemonade-data.js + profile.js
     3. the Three.js engine: one renderer/scene/camera built once, an
        "environment" (ground, sky, stand, props) rebuilt per location,
        and a game loop that only runs while a stand is open
     4. the gameplay itself: customers, the mixing minigame, serving,
        the location twists, and the launch-into-orbit failure sequence
   ============================================================ */
(function(){
"use strict";

const D = window.GAME_DATA;
const qs = id => document.getElementById(id);
const on = (el, ev, fn) => el && el.addEventListener(ev, fn);
const fmt = n => Math.round(n).toLocaleString();
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const pick = arr => arr[Math.floor(Math.random() * arr.length)];
const rand = (a, b) => a + Math.random() * (b - a);

/* ============================================================
   1. ACHIEVEMENT TOASTS + shared coin displays
   ============================================================ */
let achQueue = [];
let achShowing = false;

function queueAchToast(a){
  achQueue.push(a);
  if(!achShowing) showNextAchToast();
}
function showNextAchToast(){
  const a = achQueue.shift();
  if(!a){ achShowing = false; return; }
  achShowing = true;
  const el = qs('achToast');
  el.textContent = '🏆 ' + a.name + '  +' + a.reward + '🪙';
  el.classList.add('show');
  if(window.SFX) SFX.achievement();
  setTimeout(() => {
    el.classList.remove('show');
    setTimeout(showNextAchToast, 350);
  }, 2600);
}

function updateCoinDisplays(){
  const c = window.Save ? Save.coins : 0;
  const a = qs('titleCoins'), b = qs('hudCoins');
  if(a) a.textContent = '🪙 ' + fmt(c);
  if(b) b.textContent = '🪙 ' + fmt(c);
}

if(window.Save){
  Save.onChange(() => {
    Save.drainNewAchievements().forEach(queueAchToast);
    updateCoinDisplays();
  });
}

/* ============================================================
   2. MENU
   ============================================================ */
function initMenu(){
  updateCoinDisplays();

  document.querySelectorAll('.tabbtn').forEach(btn => {
    on(btn, 'click', () => {
      document.querySelectorAll('.tabbtn').forEach(b => b.classList.toggle('on', b === btn));
      ['play','shop','recipes','achievements','multiplayer'].forEach(name => {
        qs('tab-' + name).classList.toggle('hidden', name !== btn.dataset.tab);
      });
      if(window.SFX) SFX.uiOpen();
    });
  });

  renderLocations();
  renderUpgrades();
  renderWardrobe();
  renderRecipes();
  renderAchievements();
  initMultiplayerPanel();
  initWarningScreen();
}

const WARNING_SEEN_KEY = 'lemonade-warning-seen';
function initWarningScreen(){
  let seen = false;
  try{ seen = localStorage.getItem(WARNING_SEEN_KEY) === '1'; }catch(e){}
  if(seen) return;
  qs('warnScreen').classList.remove('hidden');
  on(qs('btnWarnOk'), 'click', () => {
    qs('warnScreen').classList.add('hidden');
    try{ localStorage.setItem(WARNING_SEEN_KEY, '1'); }catch(e){}
  });
}

function renderLocations(){
  const grid = qs('locGrid');
  grid.innerHTML = '';
  D.LOCATIONS.forEach(loc => {
    const unlocked = Save.isLocationUnlocked(loc.id);
    const card = document.createElement('div');
    card.className = 'loccard' + (unlocked ? '' : ' locked');
    card.innerHTML =
      '<div class="swatch" style="background:linear-gradient(180deg,' + loc.sky[0] + ',' + loc.sky[1] + ')"></div>' +
      (unlocked ? '' : '<div class="lock-badge">🔒</div>') +
      '<b>' + loc.name + '</b>' +
      '<div class="sub">' + loc.tagline + '</div>';
    if(unlocked){
      const go = document.createElement('button');
      go.className = 'go';
      go.textContent = '▶ Serve here';
      on(go, 'click', () => Game.start(loc.id));
      card.appendChild(go);
    } else {
      const buy = document.createElement('button');
      buy.className = 'buy';
      buy.textContent = 'Unlock — ' + fmt(loc.unlockCost) + '🪙';
      buy.disabled = Save.coins < loc.unlockCost;
      on(buy, 'click', () => {
        if(Save.unlockLocation(loc.id)){
          if(window.SFX) SFX.coinDrop();
          renderLocations();
        }
      });
      card.appendChild(buy);
    }
    grid.appendChild(card);
  });
}

function renderUpgrades(){
  const list = qs('upgList');
  list.innerHTML = '';
  D.UPGRADE_TRACKS.forEach(track => {
    const level = Save.upgradeLevel(track.id);
    const cost = Save.upgradeCostFor(track.id);
    const row = document.createElement('div');
    row.className = 'upgrow';
    row.innerHTML =
      '<div class="ic">' + track.icon + '</div>' +
      '<div class="info"><b>' + track.name + '</b><div class="d">' + track.desc + '</div></div>' +
      '<div class="lv">Lv ' + level + '/' + track.maxLevel + '</div>';
    const buy = document.createElement('button');
    buy.className = 'buy';
    if(cost === null){ buy.textContent = 'MAX'; buy.disabled = true; }
    else{
      buy.textContent = fmt(cost) + '🪙';
      buy.disabled = Save.coins < cost;
      on(buy, 'click', () => {
        if(Save.buyUpgrade(track.id)){ if(window.SFX) SFX.coinDrop(); renderUpgrades(); }
      });
    }
    row.appendChild(buy);
    list.appendChild(row);
  });
}

function renderWardrobe(){
  const grid = qs('hatGrid');
  if(!grid) return;
  grid.innerHTML = '';
  D.HATS.forEach(hat => {
    const owned = Save.ownsHat(hat.id);
    const equipped = Save.equippedHat === hat.id;
    const card = document.createElement('div');
    card.className = 'hatcard';
    card.innerHTML = '<div class="ic">' + hat.icon + '</div><b>' + hat.name + '</b>';
    const btn = document.createElement('button');
    if(!owned){
      btn.textContent = fmt(hat.price) + '🪙';
      btn.disabled = Save.coins < hat.price;
      on(btn, 'click', () => { if(Save.buyHat(hat.id)){ if(window.SFX) SFX.coinDrop(); renderWardrobe(); } });
    } else if(equipped){
      btn.textContent = 'Equipped';
      btn.className = 'equipped';
      on(btn, 'click', () => { Save.equipHat(null); renderWardrobe(); });
    } else {
      btn.textContent = 'Equip';
      on(btn, 'click', () => { Save.equipHat(hat.id); renderWardrobe(); });
    }
    card.appendChild(btn);
    grid.appendChild(card);
  });
}
if(window.Save) Save.onChange(renderWardrobe);

function renderRecipes(){
  const grid = qs('recipeGrid');
  grid.innerHTML = '';
  D.LOCATIONS.forEach(loc => {
    loc.recipes.concat(loc.secretRecipe).forEach(rid => {
      const r = D.RECIPES[rid];
      const isSecret = !!r.secret;
      const known = !isSecret || Save.hasSecretRecipe(rid);
      const card = document.createElement('div');
      card.className = 'reccard' + (known ? '' : ' locked');
      const ingHtml = known
        ? Object.keys(r.ingredients).map(id => D.INGREDIENTS[id].icon).join(' ')
        : '???';
      card.innerHTML =
        '<div class="loc">' + loc.name + (isSecret ? ' · secret' : '') + '</div>' +
        '<b>' + (known ? r.name : 'Undiscovered Recipe') + '</b>' +
        '<div class="ings">' + ingHtml + '</div>' +
        '<div class="pay">' + (known ? ('Pays ~' + fmt(r.pay) + '🪙') :
          ('Find the rare ingredient at ' + loc.name)) + '</div>';
      grid.appendChild(card);
    });
  });
}

function renderAchievements(){
  const grid = qs('achGrid');
  grid.innerHTML = '';
  const done = Save.data.achievements || [];
  D.ACHIEVEMENTS.forEach(a => {
    const got = done.indexOf(a.id) !== -1;
    const row = document.createElement('div');
    row.className = 'achrow' + (got ? ' done' : '');
    row.innerHTML =
      '<div class="ic">' + (got ? '🏆' : '🔒') + '</div>' +
      '<div><b>' + a.name + '</b><div class="d">' + a.desc + '</div></div>' +
      '<div class="rw">+' + a.reward + '🪙</div>';
    grid.appendChild(row);
  });
}
if(window.Save) Save.onChange(renderAchievements);

/* ---------- multiplayer panel ---------- */
function initMultiplayerPanel(){
  const configured = window.Net && Net.configured;
  qs('mpUnconfigured').classList.toggle('hidden', !!configured);
  qs('mpControls').classList.toggle('hidden', !configured);
  if(!configured) return;

  qs('nameField').value = Net.myCustomName || '';
  on(qs('btnSetName'), 'click', () => {
    const r = Net.setMyName(qs('nameField').value);
    qs('mpStatus').textContent = r.ok ? 'Name saved: ' + (r.name || Net.myName) : r.reason;
  });
  on(qs('btnQuickMatch'), 'click', () => {
    qs('mpStatus').textContent = 'Searching…';
    Net.quickMatch().then(res => {
      qs('mpStatus').textContent = res.found ? 'Joined a lobby! Code: ' + Net.lobby.code
                                              : 'Hosting — share code ' + Net.lobby.code;
      refreshLobbyButtons();
    }).catch(() => { qs('mpStatus').textContent = "Couldn't reach the relay."; });
  });
  on(qs('btnCreateLobby'), 'click', () => {
    Net.createLobby().then(code => {
      qs('mpStatus').textContent = 'Lobby created — share code ' + code;
      refreshLobbyButtons();
    }).catch(() => { qs('mpStatus').textContent = "Couldn't reach the relay."; });
  });
  on(qs('btnJoinLobby'), 'click', () => {
    Net.joinLobby(qs('codeField').value).then(code => {
      qs('mpStatus').textContent = 'Joined lobby ' + code;
      refreshLobbyButtons();
    }).catch(() => { qs('mpStatus').textContent = 'Could not join that lobby.'; });
  });
  on(qs('btnLeaveLobby'), 'click', () => {
    Net.leaveLobby();
    qs('mpStatus').textContent = 'Left the lobby.';
    refreshLobbyButtons();
  });
  on(qs('nameField'), 'keydown', e => { if(e.key === 'Enter') qs('btnSetName').click(); });

  Net.onLobbyChange(() => {
    const lobby = Net.lobby;
    qs('mpMembers').innerHTML = lobby
      ? lobby.members.map(m => '<span class="mpchip">' + escapeHtml(m.name) + (m.id === Net.myId ? ' (you)' : '') + '</span>').join('')
      : '';
    if(lobby && lobby.members.length > 1 && window.Save) Save.markMultiplayerPlayed();
    refreshLobbyButtons();
  });

  function refreshLobbyButtons(){
    const inLobby = Net.inLobby;
    qs('btnLeaveLobby').classList.toggle('hidden', !inLobby);
  }
}

function escapeHtml(s){
  return String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
}

/* ============================================================
   3. THREE.JS ENGINE
   ============================================================ */
let renderer, scene, camera, clock;
let envGroup, avatarGroup, ragdollGroup;
let rafId = null;

/* ---------- first-person player ----------
   The camera IS the player — no third-person follow rig. yaw/pitch use
   Three's 'YXZ' Euler order, the standard FPS convention (yaw first, so
   pitching up/down never rolls the horizon). Movement is relative to yaw. */
const EYE_HEIGHT = 1.62;
const PLAYER_SPAWN = {x:0, z:2.6, yaw:0, pitch:0};
const PLAYER_BOUNDS = {minX:-7, maxX:7, minZ:-0.9, maxZ:8.5};
const MOVE_SPEED = 3.4;
const LOOK_SENS_MOUSE = 0.0022;
const LOOK_SENS_TOUCH = 0.0055;
const PITCH_LIMIT = 1.3;

const keys = {};              // lowercased KeyboardEvent.key -> held?
const pad = {up:false, down:false, left:false, right:false};   // on-screen d-pad
let touchLook = null;         // {id, x, y} of the finger currently dragging the view

/* THREE loads from a CDN (see index.html) — deliberately never referenced
   at this file's top level, so a blocked/offline CDN only breaks starting
   a stand, not the whole menu (location list, shop, recipes, achievements,
   multiplayer panel all work with zero THREE dependency). */
/* Reads #canvasHost's current box and applies it to the camera/renderer.
   Called on window resize, but ALSO explicitly right after #gameScreen is
   unhidden in Game.start() — a display:none element reports 0x0 for its
   clientWidth/Height, so sizing the renderer while the screen is still
   hidden (which initEngineOnce()'s first-ever call necessarily does) would
   otherwise leave the canvas permanently stuck at 0x0. */
function resizeRenderer(){
  const host = qs('canvasHost');
  if(!renderer || !host.clientWidth || !host.clientHeight) return;
  camera.aspect = host.clientWidth / host.clientHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(host.clientWidth, host.clientHeight);
}

function initEngineOnce(){
  if(renderer) return;
  const host = qs('canvasHost');
  scene = new THREE.Scene();
  camera = new THREE.PerspectiveCamera(62, 16/9, 0.1, 200);
  camera.rotation.order = 'YXZ';
  renderer = new THREE.WebGLRenderer({antialias:true});
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  host.appendChild(renderer.domElement);
  clock = new THREE.Clock();
  window.addEventListener('resize', resizeRenderer);
  initControls(host);
}

/* Mouse look (via Pointer Lock — click the canvas to grab the cursor,
   Esc releases it, both browser-native) and touch look (drag anywhere on
   the canvas that isn't one of the on-screen buttons, which sit in their
   own DOM elements and simply intercept the touch before it gets here). */
function initControls(host){
  const canvas = renderer.domElement;

  on(canvas, 'click', () => {
    if(G && !G.launching) canvas.requestPointerLock();
  });
  document.addEventListener('pointerlockchange', () => {
    qs('lookHint').classList.toggle('hidden', document.pointerLockElement === canvas);
  });
  document.addEventListener('mousemove', e => {
    if(document.pointerLockElement !== canvas || !G || G.launching) return;
    G.player.yaw -= e.movementX * LOOK_SENS_MOUSE;
    G.player.pitch = clamp(G.player.pitch - e.movementY * LOOK_SENS_MOUSE, -PITCH_LIMIT, PITCH_LIMIT);
  });

  canvas.addEventListener('touchstart', e => {
    if(touchLook || !e.changedTouches.length) return;
    const t = e.changedTouches[0];
    touchLook = {id: t.identifier, x: t.clientX, y: t.clientY};
  }, {passive:true});
  canvas.addEventListener('touchmove', e => {
    if(!touchLook || !G || G.launching) return;
    for(const t of e.changedTouches){
      if(t.identifier !== touchLook.id) continue;
      G.player.yaw -= (t.clientX - touchLook.x) * LOOK_SENS_TOUCH;
      G.player.pitch = clamp(G.player.pitch - (t.clientY - touchLook.y) * LOOK_SENS_TOUCH, -PITCH_LIMIT, PITCH_LIMIT);
      touchLook.x = t.clientX; touchLook.y = t.clientY;
    }
  }, {passive:true});
  const endTouch = e => {
    if(!touchLook) return;
    for(const t of e.changedTouches) if(t.identifier === touchLook.id) touchLook = null;
  };
  canvas.addEventListener('touchend', endTouch);
  canvas.addEventListener('touchcancel', endTouch);
}

window.addEventListener('keydown', e => {
  const k = e.key.toLowerCase();
  keys[k] = true;
  if(k === 'e' && !e.repeat) tryInteract();
});
window.addEventListener('keyup', e => { keys[e.key.toLowerCase()] = false; });

if('ontouchstart' in window) document.body.classList.add('touchable');

function wirePad(id, key){
  const el = qs(id);
  const set = v => ev => { ev.preventDefault(); pad[key] = v; };
  on(el, 'pointerdown', set(true));
  on(el, 'pointerup', set(false));
  on(el, 'pointerleave', set(false));
  on(el, 'pointercancel', set(false));
}
wirePad('padUp', 'up'); wirePad('padDown', 'down'); wirePad('padLeft', 'left'); wirePad('padRight', 'right');
on(qs('btnInteractMobile'), 'click', () => tryInteract());

function disposeGroup(group){
  if(!group) return;
  group.traverse(obj => {
    if(obj.geometry) obj.geometry.dispose();
    if(obj.material){
      if(Array.isArray(obj.material)) obj.material.forEach(m => m.dispose());
      else obj.material.dispose();
    }
  });
  if(group.parent) group.parent.remove(group);
}

/* A chunky, rounded-head low-poly person — flatShading everywhere for that
   faceted, hand-modeled look (think "How to Fish"'s cast) rather than the
   smooth-shaded boxes this started as. Cheap, readable at a distance, and
   easy to blow apart into a ragdoll later — see triggerLaunch(). Optional
   hatShape (see buildHat()) rides along as one more ragdoll piece, so
   getting launched into orbit knocks your hat off too. */
function buildFigure(shirtColor, skinColor, hatShape){
  const g = new THREE.Group();
  const shirtMat = new THREE.MeshStandardMaterial({color: shirtColor || '#3fa7ff', flatShading:true});
  const skinMat  = new THREE.MeshStandardMaterial({color: skinColor || '#ffd6a5', flatShading:true});
  const legMat   = new THREE.MeshStandardMaterial({color:'#2b2140', flatShading:true});
  const eyeMat   = new THREE.MeshStandardMaterial({color:'#1a1420'});

  const torso = new THREE.Mesh(new THREE.BoxGeometry(0.58, 0.62, 0.36), shirtMat);
  torso.position.y = 1.02;
  const head = new THREE.Mesh(new THREE.SphereGeometry(0.28, 10, 8), skinMat);
  head.position.y = 1.58;
  const eyeGeo = new THREE.SphereGeometry(0.045, 6, 6);
  const eyeL = new THREE.Mesh(eyeGeo, eyeMat); eyeL.position.set(-0.1, 1.6, 0.25);
  const eyeR = new THREE.Mesh(eyeGeo, eyeMat); eyeR.position.set(0.1, 1.6, 0.25);
  const armGeo = new THREE.BoxGeometry(0.17, 0.5, 0.17);
  const armL = new THREE.Mesh(armGeo, shirtMat); armL.position.set(-0.4, 1.02, 0);
  const armR = new THREE.Mesh(armGeo, shirtMat); armR.position.set(0.4, 1.02, 0);
  const legGeo = new THREE.BoxGeometry(0.22, 0.58, 0.22);
  const legL = new THREE.Mesh(legGeo, legMat); legL.position.set(-0.15, 0.35, 0);
  const legR = new THREE.Mesh(legGeo, legMat); legR.position.set(0.15, 0.35, 0);

  g.add(torso, head, eyeL, eyeR, armL, armR, legL, legR);
  g.userData.parts = [torso, head, armL, armR, legL, legR];   // eyes stay put — they'd look odd tumbling solo

  if(hatShape){
    const hat = buildHat(hatShape);
    hat.position.y = 1.58;
    g.add(hat);
    g.userData.parts.push(hat);   // the hat flies off in the ragdoll too
  }
  return g;
}

/* One hat, built out of primitives and positioned relative to the head's
   center (y=0 here means "head height"). Add a shape here and a matching
   entry in lemonade-data.js's HATS to make it choosable in the Wardrobe. */
function buildHat(shape){
  const g = new THREE.Group();
  const m = (color, opts) => new THREE.MeshStandardMaterial(Object.assign({color, flatShading:true}, opts || {}));
  switch(shape){
    case 'party': {
      const cone = new THREE.Mesh(new THREE.ConeGeometry(0.19, 0.4, 10), m('#ff4d6d'));
      cone.position.y = 0.35;
      const pom = new THREE.Mesh(new THREE.SphereGeometry(0.06, 8, 8), m('#ffd23f'));
      pom.position.y = 0.56;
      g.add(cone, pom);
      break;
    }
    case 'bucket': {
      const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.3, 0.05, 14), m('#7bb661'));
      brim.position.y = 0.24;
      const top = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.26, 0.2, 14), m('#7bb661'));
      top.position.y = 0.34;
      g.add(brim, top);
      break;
    }
    case 'chef': {
      const base = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.16, 12), m('#ffffff'));
      base.position.y = 0.26;
      const puff = new THREE.Mesh(new THREE.SphereGeometry(0.23, 10, 8), m('#ffffff'));
      puff.position.y = 0.48; puff.scale.set(1, 0.85, 1);
      g.add(base, puff);
      break;
    }
    case 'straw': {
      const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.42, 0.04, 16), m('#e0b976'));
      brim.position.y = 0.22;
      const top = new THREE.Mesh(new THREE.CylinderGeometry(0.19, 0.19, 0.16, 12), m('#e0b976'));
      top.position.y = 0.34;
      g.add(brim, top);
      break;
    }
    case 'shades': {
      const lensGeo = new THREE.BoxGeometry(0.15, 0.08, 0.04);
      const lensMat = m('#141414', {metalness:0.4, roughness:0.2});
      const lensL = new THREE.Mesh(lensGeo, lensMat); lensL.position.set(-0.1, 0.02, 0.27);
      const lensR = new THREE.Mesh(lensGeo, lensMat); lensR.position.set(0.1, 0.02, 0.27);
      g.add(lensL, lensR);
      break;
    }
    case 'foil': {
      const cone = new THREE.Mesh(new THREE.ConeGeometry(0.24, 0.32, 8), m('#c9d3da', {metalness:0.8, roughness:0.25}));
      cone.position.y = 0.34;
      g.add(cone);
      break;
    }
    case 'mullet': {
      const blob = new THREE.Mesh(new THREE.SphereGeometry(0.2, 8, 8), m('#6b4423'));
      blob.position.set(0, 0.02, -0.2); blob.scale.set(1, 0.9, 0.7);
      g.add(blob);
      break;
    }
    case 'fish': {
      const body = new THREE.Mesh(new THREE.SphereGeometry(0.16, 8, 8), m('#5aa9e6'));
      body.scale.set(1.6, 0.8, 0.8); body.position.y = 0.24;
      const tail = new THREE.Mesh(new THREE.ConeGeometry(0.1, 0.16, 6), m('#5aa9e6'));
      tail.rotation.z = Math.PI/2; tail.position.set(-0.28, 0.24, 0);
      g.add(body, tail);
      break;
    }
    case 'antenna': {
      const stalkGeo = new THREE.CylinderGeometry(0.02, 0.02, 0.32, 6);
      const stalkMat = m('#3a3a3a');
      const ballGeo = new THREE.SphereGeometry(0.05, 8, 8);
      const ballMat = m('#7bd389');
      [-0.09, 0.09].forEach(x => {
        const stalk = new THREE.Mesh(stalkGeo, stalkMat);
        stalk.position.set(x, 0.36, 0); stalk.rotation.z = x > 0 ? -0.25 : 0.25;
        const ball = new THREE.Mesh(ballGeo, ballMat);
        ball.position.set(x * 1.6, 0.5, 0);
        g.add(stalk, ball);
      });
      break;
    }
    case 'crown': {
      const band = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.25, 0.16, 10), m('#ffcc33', {metalness:0.6, roughness:0.3}));
      band.position.y = 0.28;
      g.add(band);
      for(let i = 0; i < 5; i++){
        const spike = new THREE.Mesh(new THREE.ConeGeometry(0.05, 0.12, 6), m('#ffcc33', {metalness:0.6, roughness:0.3}));
        const a = (i / 5) * Math.PI * 2;
        spike.position.set(Math.cos(a) * 0.2, 0.42, Math.sin(a) * 0.2);
        g.add(spike);
      }
      break;
    }
  }
  return g;
}

/* A simple low-poly conifer: a trunk cylinder and two stacked cone tiers,
   flat-shaded to match the figures. Used wherever a location doesn't have
   a more specific background prop of its own. */
function addTree(x, z, scale){
  scale = scale || 1;
  const g = new THREE.Group();
  const trunkMat = new THREE.MeshStandardMaterial({color:'#6b4423', flatShading:true});
  const leafMat = new THREE.MeshStandardMaterial({color:'#3f8f4a', flatShading:true});
  const trunk = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.15, 0.7, 8), trunkMat);
  trunk.position.y = 0.35;
  const leafLow = new THREE.Mesh(new THREE.ConeGeometry(0.8, 1.1, 8), leafMat);
  leafLow.position.y = 1.1;
  const leafHigh = new THREE.Mesh(new THREE.ConeGeometry(0.6, 0.9, 8), leafMat);
  leafHigh.position.y = 1.7;
  g.add(trunk, leafLow, leafHigh);
  g.position.set(x, 0, z);
  g.scale.setScalar(scale);
  envGroup.add(g);
}

/* A bright sun disc and a couple of drifting cloud puffs — cheap MeshBasic
   blobs that ignore lighting entirely, so they read clearly against any
   location's sky without needing their own light rig. Skipped for the
   space station, which has its own starfield instead. */
function addSky(loc){
  const sun = new THREE.Mesh(
    new THREE.SphereGeometry(2.2, 14, 14),
    new THREE.MeshBasicMaterial({color: loc.accent})
  );
  sun.position.set(-14, 16, -30);
  envGroup.add(sun);

  const cloudMat = new THREE.MeshBasicMaterial({color:'#ffffff', transparent:true, opacity:0.9});
  [[8, 12, -28], [16, 15, -34], [-4, 17, -32]].forEach(([x,y,z], i) => {
    const cloud = new THREE.Group();
    for(let j = 0; j < 3; j++){
      const puff = new THREE.Mesh(new THREE.SphereGeometry(1.1 - j*0.15, 8, 8), cloudMat);
      puff.position.set(j * 1.1, Math.sin(j) * 0.2, 0);
      cloud.add(puff);
    }
    cloud.position.set(x, y, z);
    cloud.scale.setScalar(1 + i * 0.2);
    envGroup.add(cloud);
  });
}

function addStars(){
  const n = 400;
  const pos = new Float32Array(n * 3);
  for(let i = 0; i < n; i++){
    pos[i*3] = (Math.random() - 0.5) * 90;
    pos[i*3+1] = Math.random() * 45;
    pos[i*3+2] = (Math.random() - 0.5) * 90 - 20;
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  const mat = new THREE.PointsMaterial({color:0xffffff, size:0.16});
  envGroup.add(new THREE.Points(geo, mat));
}

/* Builds everything specific to one location: sky, ground, lights, the
   stand/counter, and a couple of cheap decorative props so six locations
   don't just look like six colors of the same room. */
function buildEnvironment(loc){
  disposeGroup(envGroup);
  envGroup = new THREE.Group();
  scene.add(envGroup);

  scene.background = new THREE.Color(loc.sky[1]);
  scene.fog = new THREE.Fog(loc.sky[1], 14, 45);

  const hemi = new THREE.HemisphereLight(loc.sky[0], loc.ground, loc.id === 'space' ? 0.7 : 1.1);
  const sun = new THREE.DirectionalLight(0xffffff, loc.id === 'space' ? 0.5 : 0.9);
  sun.position.set(4, 8, 5);
  envGroup.add(hemi, sun);

  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(60, 60),
    new THREE.MeshStandardMaterial({color: loc.ground})
  );
  ground.rotation.x = -Math.PI / 2;
  envGroup.add(ground);

  // the stand: a counter box + a pitcher, roughly at world origin
  const counterMat = new THREE.MeshStandardMaterial({color:'#8a5a34'});
  const counter = new THREE.Mesh(new THREE.BoxGeometry(3.6, 0.9, 0.9), counterMat);
  counter.position.set(0, 0.45, -1.6);
  envGroup.add(counter);
  const awning = new THREE.Mesh(new THREE.BoxGeometry(3.8, 0.12, 1.1), new THREE.MeshStandardMaterial({color: loc.accent}));
  awning.position.set(0, 2.65, -1.9);
  envGroup.add(awning);

  const pitcherGroup = new THREE.Group();
  const pitcherGlass = new THREE.Mesh(
    new THREE.CylinderGeometry(0.32, 0.28, 0.6, 16, 1, true),
    new THREE.MeshStandardMaterial({color:'#dff3ff', transparent:true, opacity:0.35, side:THREE.DoubleSide})
  );
  pitcherGroup.add(pitcherGlass);
  const liquid = new THREE.Mesh(
    new THREE.CylinderGeometry(0.29, 0.26, 0.02, 16),
    new THREE.MeshStandardMaterial({color:'#f5e642'})
  );
  liquid.position.y = -0.29;
  pitcherGroup.add(liquid);
  pitcherGroup.position.set(0.9, 1.2, -1.6);
  envGroup.add(pitcherGroup);
  envGroup.userData.pitcher = pitcherGroup;
  envGroup.userData.liquid = liquid;

  if(loc.twist !== 'gravity') addSky(loc);

  // location flavor props — cheap primitives, just enough to read distinct
  switch(loc.twist){
    case 'seagull': {
      const water = new THREE.Mesh(new THREE.PlaneGeometry(60, 20), new THREE.MeshStandardMaterial({color:'#2f9fd8'}));
      water.rotation.x = -Math.PI/2; water.position.set(0, 0.02, -14);
      envGroup.add(water);
      break;
    }
    case 'rush': {
      for(let i = 0; i < 6; i++){
        const h = rand(3, 7);
        const b = new THREE.Mesh(new THREE.BoxGeometry(1.4, h, 1.4), new THREE.MeshStandardMaterial({color:'#3d4152'}));
        b.position.set((i - 2.5) * 2.6, h/2, -16 - (i % 2) * 3);
        envGroup.add(b);
      }
      break;
    }
    case 'dizzy': {
      for(let i = 0; i < 4; i++){
        const t = new THREE.Mesh(new THREE.ConeGeometry(1, 2.4, 12), new THREE.MeshStandardMaterial({color: i % 2 ? '#ff6b9d' : '#4cc9f0'}));
        t.position.set((i - 1.5) * 4, 1.2, -13);
        envGroup.add(t);
      }
      break;
    }
    case 'freeze': {
      for(let i = 0; i < 3; i++){
        const m = new THREE.Mesh(new THREE.ConeGeometry(3.2, 6 + i, 8), new THREE.MeshStandardMaterial({color:'#eef7ff'}));
        m.position.set((i - 1) * 7, (6 + i)/2 - 1, -20);
        envGroup.add(m);
      }
      break;
    }
    case 'gravity': {
      addStars();
      const planet = new THREE.Mesh(new THREE.SphereGeometry(3, 20, 20), new THREE.MeshStandardMaterial({color:'#ff8ad8', emissive:'#4a1a3a'}));
      planet.position.set(8, 10, -25);
      envGroup.add(planet);
      break;
    }
    default: {
      addTree(-4.5, -3, 1);
      addTree(4.5, -3.4, 1.15);
      addTree(-6.5, -5, 0.85);
    }
  }
}

/* ============================================================
   4. GAMEPLAY
   ============================================================ */
const PITCHER_CAPACITY = 10;
const LAUNCH_RISE_MS = 1600;
const CUSTOMER_SPACING = 1.9;   // world units between customer slots, kept in sync with the dizzy-twist wobble below
const CUSTOMER_BASE_Y = 0.35;   // stands customers up a bit so they clear the counter on camera
const TIP_JAR_CAP = 40;         // coins the jar can hold before it just... overflows onto the counter, unclaimed
const TIP_JAR_RATE = 0.7;       // coins/second, loose change customers leave behind

const FAIL_CAPTIONS = {
  wrong:   ["Wrong drink. SO wrong.", "That was NOT what they ordered.", "Order mixed up. You are now also mixed up, in the sky."],
  timeout: ["Too slow!", "Patience: gone. You: also gone.", "The wait was real. So is the launch."],
  spill:   ["Pitcher overflow detected.", "That's a lot of lemonade on the floor.", "Spillage logged. Trajectory: up."]
};
const RESPAWN_LINES = ['Respawning at the stand…', 'Reassembling…', 'Gravity, please resume.'];

let G = null;   // the live game state, created fresh in Game.start()

function availableIngredients(loc){
  const ids = new Set();
  loc.recipes.forEach(rid => Object.keys(D.RECIPES[rid].ingredients).forEach(i => ids.add(i)));
  if(Save.hasSecretRecipe(loc.secretRecipe)){
    Object.keys(D.RECIPES[loc.secretRecipe].ingredients).forEach(i => ids.add(i));
  }
  return Array.from(ids);
}

function findMatchingRecipe(mix){
  const mixKeys = Object.keys(mix).filter(k => mix[k] > 0);
  for(const id in D.RECIPES){
    const ing = D.RECIPES[id].ingredients;
    const keys = Object.keys(ing);
    if(keys.length !== mixKeys.length) continue;
    let ok = true;
    for(const k of keys){ if(mix[k] !== ing[k]){ ok = false; break; } }
    if(ok) return id;
  }
  return null;
}

function recipePool(loc){
  const pool = [];
  loc.recipes.forEach(id => { pool.push(id, id, id); });
  if(Save.hasSecretRecipe(loc.secretRecipe)) pool.push(loc.secretRecipe);
  return pool;
}

const Game = {
  start(locationId){
    const loc = D.locationById(locationId);
    if(!loc || !Save.isLocationUnlocked(locationId)) return;
    if(typeof THREE === 'undefined'){
      // toast() lives inside #gameScreen, which we haven't shown yet — this
      // is the one message that has to interrupt instead of float past
      alert("Couldn't load the 3D engine (three.js) — check your connection and reload the page.");
      return;
    }
    initEngineOnce();
    buildEnvironment(loc);

    disposeGroup(avatarGroup);
    const hatDef = Save.equippedHat && D.hatById(Save.equippedHat);
    avatarGroup = buildFigure('#ffd23f', '#ffd6a5', hatDef && hatDef.shape);
    avatarGroup.scale.setScalar(0.85);
    scene.add(avatarGroup);
    if(ragdollGroup){ disposeGroup(ragdollGroup); ragdollGroup = null; }

    const capacity = 1 + Save.upgradeLevel('capacity');
    G = {
      loc, capacity,
      player: Object.assign({}, PLAYER_SPAWN),
      slots: new Array(capacity).fill(null),
      customerMeshes: new Array(capacity).fill(null),
      currentMix: {}, cupsReady: null,
      combo: 0, perfectStreak: 0,
      frost: {}, bubbles: [],
      launching: false, locked: false,
      nextSpawnAt: performance.now() + 900,
      rushActive: false, rushUntil: 0, nextRushAt: performance.now() + rand(16000, 24000),
      nextSeagullAt: performance.now() + rand(9000, 16000),
      cupsPourTime: 0,
      lastFrostTick: performance.now(),
      lastPresenceSend: 0,
      lastPeerRailAt: 0,
      spawnedRareCustomer: false,
      tipJar: 0, fishing: null, stations: []
    };
    availableIngredients(loc).forEach(id => { G.frost[id] = 0; });
    buildStations(loc);
    placeAvatarAtPlayer();

    Save.setLastLocation(locationId);
    buildCustomerRow(capacity);
    renderIngredientBar();
    updateCupDisplay();
    qs('mixMeterWrap').classList.remove('show');
    qs('btnStir').classList.remove('show');
    qs('btnPour').disabled = true;
    qs('fishPanel').classList.remove('show');
    const banner = qs('twistBanner');
    banner.classList.add('hidden');
    qs('launchOverlay').classList.remove('show');
    qs('peerCountChip').classList.toggle('hidden', !(window.Net && Net.inLobby));
    qs('lookHint').classList.remove('hidden');

    document.body.classList.add('in-game');
    qs('titleScreen').classList.add('hidden');
    qs('gameScreen').classList.remove('hidden');
    resizeRenderer();   // #canvasHost was display:none until the line above
    updateCoinDisplays();
    updateComboDisplay();

    clock.start();
    if(rafId) cancelAnimationFrame(rafId);
    rafId = requestAnimationFrame(loop);
  },

  stop(){
    if(rafId) cancelAnimationFrame(rafId);
    rafId = null;
    if(document.pointerLockElement) document.exitPointerLock();
    if(window.Net) Net.sendPresence({location:'menu', anim:'idle', coins:Save.coins});
    Save.flushNow();
    document.body.classList.remove('in-game');
    qs('gameScreen').classList.add('hidden');
    qs('titleScreen').classList.remove('hidden');
    renderLocations(); renderUpgrades(); renderRecipes(); renderAchievements(); renderWardrobe();
  }
};
window.Game = Game;

function placeAvatarAtPlayer(){
  if(!avatarGroup || !G) return;
  avatarGroup.position.set(G.player.x, 0, G.player.z);
  avatarGroup.rotation.y = G.player.yaw;
}

/* ---------- interactive world stations ----------
   Ingredient stations are auto-laid-out in an arc behind the player's
   spawn point, one per ingredient this location's recipes actually use —
   walking up and pressing E/USE adds one unit to the pitcher, exactly like
   clicking its button in the HUD bar does (both call commitIngredient()).
   The tip jar and fishing spot are fixed props off to the side. */
const STATION_RADIUS = 1.5;   // how close (world units) counts as "in range"

function buildStations(loc){
  G.stations.forEach(s => { if(s.mesh) disposeGroup(s.mesh); });
  G.stations = [];

  const ids = availableIngredients(loc);
  const arcR = 4.2;
  ids.forEach((id, i) => {
    const t = ids.length > 1 ? i / (ids.length - 1) : 0.5;
    const angle = (t - 0.5) * 2.1;   // spread across roughly the back half of the yard
    const x = Math.sin(angle) * arcR;
    const z = 4.4 + Math.cos(angle) * 1.6;
    const mesh = buildIngredientStationMesh(id);
    mesh.position.set(x, 0, z);
    envGroup.add(mesh);
    G.stations.push({type:'ingredient', id, x, z, mesh, label:'Collect ' + D.INGREDIENTS[id].name});
  });

  const jarMesh = buildTipJarMesh();
  jarMesh.position.set(2.6, 0, -0.3);
  envGroup.add(jarMesh);
  G.stations.push({type:'tip', x:2.6, z:-0.3, mesh:jarMesh, label:'Collect tip jar'});

  const dock = buildFishingSpotMesh();
  dock.position.set(-3, 0, 7.2);
  envGroup.add(dock);
  G.stations.push({type:'fishing', x:-3, z:7.2, mesh:dock, label:'Go fishing'});
}

function buildIngredientStationMesh(id){
  const def = D.INGREDIENTS[id];
  const g = new THREE.Group();
  const crate = new THREE.Mesh(
    new THREE.CylinderGeometry(0.4, 0.45, 0.7, 10),
    new THREE.MeshStandardMaterial({color: def.color, flatShading:true})
  );
  crate.position.y = 0.35;
  const rim = new THREE.Mesh(
    new THREE.TorusGeometry(0.4, 0.05, 6, 12),
    new THREE.MeshStandardMaterial({color:'#5a3d22', flatShading:true})
  );
  rim.rotation.x = Math.PI/2; rim.position.y = 0.7;
  g.add(crate, rim);
  return g;
}

function buildTipJarMesh(){
  const g = new THREE.Group();
  const glass = new THREE.Mesh(
    new THREE.CylinderGeometry(0.3, 0.26, 0.55, 12, 1, true),
    new THREE.MeshStandardMaterial({color:'#dff3ff', transparent:true, opacity:0.35, side:THREE.DoubleSide})
  );
  glass.position.y = 0.38;
  const coins = new THREE.Mesh(
    new THREE.CylinderGeometry(0.24, 0.24, 0.05, 12),
    new THREE.MeshStandardMaterial({color:'#ffd23f', flatShading:true})
  );
  coins.position.y = 0.14;
  g.add(glass, coins);
  g.userData.coins = coins;
  return g;
}

function buildFishingSpotMesh(){
  const g = new THREE.Group();
  const pond = new THREE.Mesh(new THREE.CircleGeometry(2.2, 20), new THREE.MeshStandardMaterial({color:'#2f9fd8'}));
  pond.rotation.x = -Math.PI/2; pond.position.set(1.5, 0.02, 0.5);
  const dock = new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.12, 2.2), new THREE.MeshStandardMaterial({color:'#8a5a34', flatShading:true}));
  dock.position.set(0, 0.06, 0.5);
  const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 1.4, 6), new THREE.MeshStandardMaterial({color:'#6b4423'}));
  rod.rotation.z = Math.PI/3.2; rod.position.set(0.2, 0.9, -0.7);
  g.add(pond, dock, rod);
  return g;
}

function nearestStation(){
  if(!G) return null;
  let best = null, bestD = STATION_RADIUS;
  G.stations.forEach(s => {
    const d = Math.hypot(s.x - G.player.x, s.z - G.player.z);
    if(d < bestD){ bestD = d; best = s; }
  });
  return best;
}

/* WASD/arrows + the on-screen d-pad, relative to which way the camera is
   looking (yaw only — you don't sprint faster by staring at your feet). */
function updatePlayerMovement(dt){
  let moveF = 0, moveR = 0;
  if(keys.w || keys.arrowup || pad.up) moveF += 1;
  if(keys.s || keys.arrowdown || pad.down) moveF -= 1;
  if(keys.d || keys.arrowright || pad.right) moveR += 1;
  if(keys.a || keys.arrowleft || pad.left) moveR -= 1;
  if(!moveF && !moveR) return;

  const len = Math.hypot(moveF, moveR);
  moveF /= len; moveR /= len;
  const yaw = G.player.yaw;
  const fx = Math.sin(yaw), fz = -Math.cos(yaw);
  const rx = Math.cos(yaw), rz = Math.sin(yaw);
  G.player.x = clamp(G.player.x + (fx*moveF + rx*moveR) * MOVE_SPEED * dt, PLAYER_BOUNDS.minX, PLAYER_BOUNDS.maxX);
  G.player.z = clamp(G.player.z + (fz*moveF + rz*moveR) * MOVE_SPEED * dt, PLAYER_BOUNDS.minZ, PLAYER_BOUNDS.maxZ);
  placeAvatarAtPlayer();
}

function updateInteractHint(){
  const el = qs('interactHint');
  const s = nearestStation();
  if(!s){ el.classList.remove('show'); return; }
  el.textContent = '[E] ' + s.label;
  el.classList.add('show');
}

function tryInteract(){
  if(!G || G.launching) return;
  const s = nearestStation();
  if(!s) return;
  if(s.type === 'ingredient'){
    if(G.fishing) return;
    addIngredient(s.id);   // same frost/zero-G rules as clicking its HUD button — see addIngredient()
  } else if(s.type === 'tip'){
    if(G.tipJar <= 0){ toast('The jar is empty for now.'); return; }
    Save.collectTip(G.tipJar);
    if(window.SFX) SFX.coinDrop();
    toast('Collected 🪙' + Math.round(G.tipJar) + ' in tips!');
    G.tipJar = 0;
    updateCoinDisplays();
  } else if(s.type === 'fishing'){
    startFishing();
  }
}

/* ---------- fishing minigame ----------
   Same "click in the sweet zone" skill test as mixing's STIR, on its own
   small meter so it can never collide with an in-progress mix. */
function startFishing(){
  if(!G || G.locked || G.fishing) return;
  G.locked = true;
  if(window.SFX) SFX.splash();
  const sweetStart = rand(40, 60);
  const sweetWidth = 20;
  qs('fishSweet').style.left = sweetStart + '%';
  qs('fishSweet').style.width = sweetWidth + '%';
  qs('fishPanel').classList.add('show');
  G.fishing = {start: performance.now(), duration: 1500, sweetStart, sweetEnd: sweetStart + sweetWidth, done:false};

  function tick(){
    if(!G || !G.fishing || G.fishing.done) return;
    const p = clamp((performance.now() - G.fishing.start) / G.fishing.duration, 0, 1);
    qs('fishFill').style.width = (p*100) + '%';
    if(p >= 1){ finishFish(null); return; }
    requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);
}

function finishFish(power){
  if(!G || !G.fishing || G.fishing.done) return;
  const f = G.fishing;
  f.done = true;
  qs('fishPanel').classList.remove('show');
  G.locked = false;

  const hit = power !== null && power >= f.sweetStart && power <= f.sweetEnd;
  if(!hit){
    toast('🎣 The one that got away…');
    G.fishing = null;
    return;
  }
  Save.recordFish();
  const rare = G.loc.rareIngredient;
  if(rare && Save.rareFound.indexOf(rare) === -1 && Math.random() < 0.35){
    Save.findRareIngredient(G.loc.id, rare);
    if(window.SFX) SFX.perfectDing();
    toast('🎣 Reeled in a rare ' + D.INGREDIENTS[rare].name + '!');
    renderIngredientBar();
  } else {
    const amt = Math.round(rand(15, 40));
    Save.collectTip(amt);
    if(window.SFX) SFX.chaChing();
    toast('🎣 Caught something worth 🪙' + amt + '!');
  }
  updateCoinDisplays();
  G.fishing = null;
}
on(qs('btnReel'), 'click', () => {
  if(!G || !G.fishing || G.fishing.done) return;
  const p = clamp((performance.now() - G.fishing.start) / G.fishing.duration, 0, 1) * 100;
  finishFish(p);
});

function buildCustomerRow(capacity){
  const row = qs('customerRow');
  row.innerHTML = '';
  for(let i = 0; i < capacity; i++){
    const card = document.createElement('div');
    card.className = 'custcard';
    card.style.display = 'none';
    card.dataset.slot = i;
    card.innerHTML = '<div class="who"></div><div class="order"></div><div class="pat"><i></i></div>';
    on(card, 'click', () => serveCustomer(i));
    row.appendChild(card);
  }
}

function renderIngredientBar(){
  const bar = qs('ingredientBar');
  bar.innerHTML = '';
  availableIngredients(G.loc).forEach(id => {
    const def = D.INGREDIENTS[id];
    const btn = document.createElement('button');
    btn.className = 'ingbtn' + (def.rare ? ' rare' : '');
    btn.dataset.id = id;
    btn.innerHTML = def.icon + '<span class="count"></span>';
    btn.title = def.name;
    on(btn, 'click', () => addIngredient(id));
    bar.appendChild(btn);
  });
  updateIngredientBarUI();
}

function updateIngredientBarUI(){
  document.querySelectorAll('#ingredientBar .ingbtn').forEach(btn => {
    const id = btn.dataset.id;
    const n = G.currentMix[id] || 0;
    btn.querySelector('.count').textContent = n > 0 ? n : '';
    btn.classList.toggle('frosted', G.loc.twist === 'freeze' && (G.frost[id] || 0) >= 1);
  });
}

function updatePitcherLiquid(){
  if(!envGroup || !envGroup.userData.liquid) return;
  const total = Object.values(G.currentMix).reduce((a,b) => a+b, 0);
  const liquid = envGroup.userData.liquid;
  const h = clamp(total / PITCHER_CAPACITY, 0.02, 1) * 0.55;
  liquid.scale.y = h / 0.02;
  liquid.position.y = -0.29 + (h - 0.02) / 2;
  // blend a color from whatever's in the mix, for a bit of visual feedback
  let r=0,g=0,b=0,n=0;
  Object.keys(G.currentMix).forEach(id => {
    const c = new THREE.Color(D.INGREDIENTS[id].color);
    const amt = G.currentMix[id];
    r += c.r*amt; g += c.g*amt; b += c.b*amt; n += amt;
  });
  if(n > 0) liquid.material.color.setRGB(r/n, g/n, b/n);
}

function updateCupDisplay(){
  const el = qs('cupDisplay');
  if(G.cupsReady && G.cupsReady.count > 0){
    const r = D.RECIPES[G.cupsReady.recipeId];
    el.textContent = '🥤 x' + G.cupsReady.count + ' ready: ' + (r ? r.name : 'Mystery Mix') + ' — click a customer!';
  } else {
    el.textContent = '';
  }
}

function updateComboDisplay(){
  qs('hudCombo').textContent = '🔥 ' + (G ? G.combo : 0);
}

function toast(text){
  const area = qs('toastArea');
  const el = document.createElement('div');
  el.className = 'toast';
  el.textContent = text;
  area.appendChild(el);
  setTimeout(() => el.remove(), 2400);
}

/* ---------- ingredients & the mixing minigame ---------- */

function addIngredient(id){
  if(!G || G.locked) return;
  if(G.loc.twist === 'freeze' && (G.frost[id] || 0) >= 1){
    G.frost[id] = 0;
    if(window.SFX) SFX.freeze();
    updateIngredientBarUI();
    return;
  }
  G.frost[id] = 0;
  if(G.loc.twist === 'gravity'){ spawnBubble(id); return; }
  commitIngredient(id);
}

function commitIngredient(id){
  const total = Object.values(G.currentMix).reduce((a,b) => a+b, 0);
  if(total >= PITCHER_CAPACITY){ triggerSpill(); return; }
  G.currentMix[id] = (G.currentMix[id] || 0) + 1;
  if(window.SFX) SFX.addIngredient();
  updateIngredientBarUI();
  updatePitcherLiquid();
}

let bubbleSeq = 0;
function spawnBubble(id){
  const def = D.INGREDIENTS[id];
  const layer = qs('floatLayer');
  const el = document.createElement('div');
  el.className = 'bubble3d';
  el.textContent = def.icon;
  const startX = 20 + Math.random() * 60;
  el.style.left = startX + '%';
  el.style.bottom = '18%';
  layer.appendChild(el);
  const bid = ++bubbleSeq;
  const born = performance.now();
  const drift = (Math.random() - 0.5) * 30;
  const bubble = {id: bid, el, born, ingredientId: id, claimed:false};
  G.bubbles.push(bubble);
  if(window.SFX) SFX.float();
  el.style.transition = 'transform 1.7s linear, opacity 1.7s linear';
  requestAnimationFrame(() => {
    el.style.transform = 'translate(' + drift + 'px, -220px)';
    el.style.opacity = '0';
  });
  on(el, 'click', () => {
    if(bubble.claimed) return;
    bubble.claimed = true;
    commitIngredient(id);
    el.remove();
  });
  setTimeout(() => {
    if(!bubble.claimed){ el.remove(); toast(def.icon + ' drifted off into space!'); }
    G.bubbles = G.bubbles.filter(b => b.id !== bid);
  }, 1750);
}

function triggerSpill(){
  if(window.SFX) SFX.splat();
  toast('The pitcher overflowed everywhere!');
  G.currentMix = {};
  updateIngredientBarUI();
  updatePitcherLiquid();
  triggerLaunch('spill');
}

function startMix(){
  if(!G || G.locked) return;
  if(G.pendingPour){ toast('Pour what you already mixed first!'); return; }
  const total = Object.values(G.currentMix).reduce((a,b) => a+b, 0);
  if(total === 0) return;
  G.locked = true;
  if(window.SFX) SFX.mixWhirl();
  const speed = Save.upgradeLevel('speed');
  const duration = clamp(1300 - speed * 90, 650, 1300);
  const sweetStart = rand(48, 62);
  const sweetWidth = 16;
  const fillEl = qs('mixFill'), sweetEl = qs('mixSweet');
  sweetEl.style.left = sweetStart + '%';
  sweetEl.style.width = sweetWidth + '%';
  qs('mixMeterWrap').classList.add('show');
  qs('btnStir').classList.add('show');

  const mix = {start: performance.now(), duration, sweetStart, sweetEnd: sweetStart + sweetWidth, done:false};
  G.mix = mix;

  function tick(){
    if(!G || G.mix !== mix || mix.done) return;
    const p = clamp((performance.now() - mix.start) / mix.duration, 0, 1);
    fillEl.style.width = (p*100) + '%';
    if(p >= 1){ finishMix(null); return; }
    requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);
}

function stirNow(){
  if(!G || !G.mix || G.mix.done) return;
  const p = clamp((performance.now() - G.mix.start) / G.mix.duration, 0, 1) * 100;
  finishMix(p);
}

function finishMix(power){
  if(!G || !G.mix || G.mix.done) return;
  const mix = G.mix;
  mix.done = true;
  qs('mixMeterWrap').classList.remove('show');
  qs('btnStir').classList.remove('show');

  let quality = 'sloppy';
  if(power !== null){
    if(power >= mix.sweetStart && power <= mix.sweetEnd) quality = 'perfect';
    else if(power >= mix.sweetStart - 16 && power <= mix.sweetEnd + 16) quality = 'good';
  }

  const recipeId = findMatchingRecipe(G.currentMix);
  const batch = Save.upgradeLevel('batch');
  G.pendingPour = {recipeId, quality, count: 1 + batch};
  G.currentMix = {};
  updateIngredientBarUI();
  updatePitcherLiquid();

  const name = recipeId ? D.RECIPES[recipeId].name : 'a mysterious sludge';
  qs('cupDisplay').textContent = 'Mixed ' + (quality === 'perfect' ? '✨ Perfectly ✨ ' : '') + name + ' — click POUR!';
  qs('btnPour').disabled = false;
  G.locked = false;
}

function pourNow(){
  if(!G || !G.pendingPour) return;
  if(window.SFX) SFX.pour();
  G.cupsReady = G.pendingPour;
  G.cupsPourTime = performance.now();
  G.pendingPour = null;
  qs('btnPour').disabled = true;
  updateCupDisplay();
}
on(qs('btnMix'), 'click', startMix);
on(qs('btnStir'), 'click', stirNow);
on(qs('btnPour'), 'click', pourNow);
window.addEventListener('keydown', e => { if(e.code === 'Space' && qs('btnStir').classList.contains('show')){ e.preventDefault(); stirNow(); } });

/* ---------- customers ---------- */

function spawnCustomer(){
  const idx = G.slots.findIndex(s => s === null);
  if(idx === -1) return;

  let custId, recipeId;
  if(G.loc.rareCustomer && Save.hasSecretRecipe(G.loc.secretRecipe) && !G.spawnedRareCustomer && Math.random() < 0.10){
    custId = G.loc.rareCustomer;
    recipeId = G.loc.secretRecipe;
    G.spawnedRareCustomer = true;
  } else {
    custId = pick(G.loc.customers);
    recipeId = pick(recipePool(G.loc));
  }
  const custType = D.customerById(custId);
  const recipe = D.RECIPES[recipeId];

  let patience = (custType.patience + recipe.difficulty * 2) * 1000;
  patience *= 1 + Save.upgradeLevel('patience') * 0.08;
  if(G.rushActive) patience *= 0.8;

  const slot = {
    custId, recipeId, custType,
    spawnedAt: performance.now(),
    patienceTotal: patience, patienceLeft: patience,
    wobbleSeed: Math.random() * 10,
    swapAt: G.loc.twist === 'dizzy' ? performance.now() + rand(4000, 9000) : null,
    hasSwapped: false
  };
  G.slots[idx] = slot;

  const mesh = buildFigure(custType.color, '#ffe3c2');
  const x = (idx - (G.capacity - 1) / 2) * CUSTOMER_SPACING;
  mesh.position.set(x, CUSTOMER_BASE_Y, -2.9);
  mesh.scale.setScalar(1.05);
  scene.add(mesh);
  G.customerMeshes[idx] = mesh;

  const card = qs('customerRow').children[idx];
  card.style.display = 'block';
  card.classList.remove('empty');
  card.querySelector('.who').textContent = custType.name;
  card.querySelector('.order').textContent = orderIcon(recipe);
  card.querySelector('.pat i').style.width = '100%';
  card.querySelector('.pat i').style.background = '';
}

function orderIcon(recipe){
  const ids = Object.keys(recipe.ingredients);
  return ids.slice(0, 3).map(id => D.INGREDIENTS[id].icon).join('');
}

function freeSlot(idx){
  G.slots[idx] = null;
  if(G.customerMeshes[idx]){ scene.remove(G.customerMeshes[idx]); disposeGroup(G.customerMeshes[idx]); G.customerMeshes[idx] = null; }
  const card = qs('customerRow').children[idx];
  card.style.display = 'none';
}

function serveCustomer(idx){
  if(!G || G.launching) return;
  const slot = G.slots[idx];
  if(!slot) return;
  if(!G.cupsReady || G.cupsReady.count <= 0){ toast('No drink ready yet!'); return; }

  const correct = G.cupsReady.recipeId === slot.recipeId;
  const quality = G.cupsReady.quality;
  G.cupsReady.count--;
  if(G.cupsReady.count <= 0) G.cupsReady = null;
  updateCupDisplay();

  if(correct) successServe(idx, slot, quality);
  else failOrder('wrong', idx);
}

function successServe(idx, slot, quality){
  const recipe = D.RECIPES[slot.recipeId];
  const tipFrac = quality === 'perfect' ? 1 : (quality === 'good' ? 0.4 : 0);
  const luck = Save.upgradeLevel('luck');
  const tip = Math.round(slot.custType.tipMax * tipFrac * (1 + luck * 0.1));
  const pay = recipe.pay + tip;
  const ms = performance.now() - slot.spawnedAt;

  G.combo++;
  if(quality === 'perfect') G.perfectStreak++; else G.perfectStreak = 0;
  const isYeti = slot.custType.id === 'yeti';

  Save.completeOrder({pay, comboAfter:G.combo, perfectStreakAfter:G.perfectStreak, ms, yeti:isYeti});
  if(window.SFX){ SFX.chaChing(); if(quality === 'perfect') SFX.perfectDing(); }
  toast((slot.custType.happy && pick(slot.custType.happy)) || 'Thanks!');
  toast('+' + fmt(pay) + '🪙' + (tip ? (' (+' + tip + ' tip)') : ''));
  updateComboDisplay(); updateCoinDisplays();

  const dropChance = 0.08 + luck * 0.02;
  if(G.loc.rareIngredient && Math.random() < dropChance && Save.rareFound.indexOf(G.loc.rareIngredient) === -1){
    Save.findRareIngredient(G.loc.id, G.loc.rareIngredient);
    toast('✨ Found a rare ingredient: ' + D.INGREDIENTS[G.loc.rareIngredient].name + '!');
    renderIngredientBar();
  }

  if(window.Net) Net.sendEvent('served', '');
  freeSlot(idx);
}

function failOrder(reason, idx){
  const slot = G.slots[idx];
  if(slot && slot.custType && slot.custType.angry) toast(pick(slot.custType.angry));
  if(idx !== undefined && idx !== null) freeSlot(idx);
  triggerLaunch(reason);
}

/* ---------- location twists ---------- */

function tickTwists(now, dt){
  const loc = G.loc;

  if(loc.twist === 'freeze'){
    if(now - G.lastFrostTick > 400){
      G.lastFrostTick = now;
      Object.keys(G.frost).forEach(id => { G.frost[id] = clamp(G.frost[id] + 0.07, 0, 1); });
      updateIngredientBarUI();
    }
  }

  if(loc.twist === 'seagull' && now >= G.nextSeagullAt){
    G.nextSeagullAt = now + rand(10000, 18000);
    if(G.cupsReady && G.cupsReady.count > 0 && now - G.cupsPourTime > 4000){
      G.cupsReady.count--;
      if(G.cupsReady.count <= 0) G.cupsReady = null;
      updateCupDisplay();
      if(window.SFX) SFX.seagull();
      toast('🐦 A seagull stole a cup of lemonade!');
      flyBird();
    }
  }

  if(loc.twist === 'rush'){
    const banner = qs('twistBanner');
    if(!G.rushActive && now >= G.nextRushAt){
      G.rushActive = true; G.rushUntil = now + 8000;
      banner.textContent = '🚕 RUSH HOUR!'; banner.classList.remove('hidden');
    } else if(G.rushActive && now >= G.rushUntil){
      G.rushActive = false; banner.classList.add('hidden');
      G.nextRushAt = now + rand(18000, 26000);
    }
  }

  if(loc.twist === 'dizzy'){
    G.slots.forEach((slot, i) => {
      if(!slot) return;
      const card = qs('customerRow').children[i];
      const wob = Math.sin(now * 0.003 + slot.wobbleSeed) * 6;
      card.style.transform = 'translateX(' + wob + 'px)';
      if(slot.swapAt && !slot.hasSwapped && now >= slot.swapAt){
        slot.hasSwapped = true;
        const options = G.loc.recipes.filter(r => r !== slot.recipeId);
        slot.recipeId = pick(options.length ? options : G.loc.recipes);
        slot.patienceLeft = Math.min(slot.patienceTotal, slot.patienceLeft + 4000);
        card.querySelector('.order').textContent = orderIcon(D.RECIPES[slot.recipeId]);
        toast(slot.custType.name + ' changed their mind!');
      }
      if(G.customerMeshes[i]) G.customerMeshes[i].position.x = ((i - (G.capacity-1)/2) * CUSTOMER_SPACING) + Math.sin(now*0.003+slot.wobbleSeed)*0.15;
    });
  }
}

function flyBird(){
  const layer = qs('floatLayer');
  const el = document.createElement('div');
  el.style.position = 'absolute';
  el.style.top = '20%';
  el.style.left = '-8%';
  el.style.fontSize = '28px';
  el.style.transition = 'transform 1.1s linear';
  el.textContent = '🐦';
  layer.appendChild(el);
  requestAnimationFrame(() => { el.style.transform = 'translateX(120vw)'; });
  setTimeout(() => el.remove(), 1150);
}

/* ---------- the launch-into-orbit failure sequence ---------- */

function triggerLaunch(reason){
  if(!G || G.launching) return;
  G.launching = true;
  G.locked = true;
  Save.recordFail();
  G.combo = 0; G.perfectStreak = 0;
  updateComboDisplay();
  G.slots.forEach(s => { if(s) s.patienceLeft = Math.min(s.patienceTotal, s.patienceLeft + 3000); });

  if(window.SFX) SFX.boing();
  if(window.Net) Net.sendEvent('launched', reason);

  // squash-and-stretch anticipation, then blow the avatar apart
  const g = avatarGroup;
  const squashStart = performance.now();
  function squash(){
    const p = clamp((performance.now() - squashStart) / 150, 0, 1);
    g.scale.set(0.85 + p*0.3, 0.85 - p*0.4, 0.85 + p*0.3);
    if(p < 1) requestAnimationFrame(squash); else launchNow();
  }
  requestAnimationFrame(squash);

  function launchNow(){
    if(window.SFX) SFX.launch();
    ragdollGroup = new THREE.Group();
    g.userData.parts.forEach(part => {
      const wp = new THREE.Vector3();
      part.getWorldPosition(wp);
      const clone = part.clone();
      clone.visible = true;   // the head is normally see-through from inside in first-person — the ragdoll never should be
      clone.position.copy(wp);
      clone.quaternion.copy(part.getWorldQuaternion(new THREE.Quaternion()));
      clone.userData.vel = new THREE.Vector3(rand(-2,2), rand(9,13), rand(-2,2));
      clone.userData.angVel = new THREE.Vector3(rand(-8,8), rand(-8,8), rand(-8,8));
      ragdollGroup.add(clone);
    });
    scene.remove(g); g.visible = false;
    scene.add(ragdollGroup);
    ragdollGroup.userData.startedAt = performance.now();

    // Pull the camera out of first-person for this one moment — otherwise
    // there is nothing to watch, since the ragdoll launches from right
    // where your eyes just were. A fixed spectator shot behind and above
    // the launch point, held until respawn() hands control back to the
    // normal first-person camera in the main loop.
    const yaw = G.player.yaw;
    const fx = Math.sin(yaw), fz = -Math.cos(yaw);
    camera.position.set(G.player.x - fx * 3.5, EYE_HEIGHT + 2.4, G.player.z - fz * 3.5);
    camera.lookAt(G.player.x, EYE_HEIGHT + 1, G.player.z);

    const overlay = qs('launchOverlay');
    qs('launchTitle').textContent = 'LAUNCHED INTO ORBIT!';
    qs('launchCap').textContent = pick(FAIL_CAPTIONS[reason] || FAIL_CAPTIONS.wrong);
    qs('launchCountdown').textContent = '';
    overlay.classList.add('show');

    setTimeout(() => runCountdown(3), LAUNCH_RISE_MS);
  }
}

function runCountdown(n){
  if(n <= 0){ respawn(); return; }
  qs('launchCountdown').textContent = pick(RESPAWN_LINES) + ' ' + n + '…';
  if(window.SFX) SFX.countdownBeep();
  setTimeout(() => runCountdown(n - 1), 700);
}

function respawn(){
  qs('launchOverlay').classList.remove('show');
  // NOTE: ragdoll pieces are clones that share geometry/material instances
  // with the real avatar (buildFigure reuses a handful of materials across
  // limbs) — disposing them here would free GPU resources the reused
  // avatarGroup still needs, so just drop them from the scene graph.
  if(ragdollGroup){ scene.remove(ragdollGroup); ragdollGroup = null; }
  avatarGroup.visible = true;
  avatarGroup.scale.setScalar(0.85);
  // "respawns back at their stand" — walking off is undone along with
  // everything else the mess-up cost you, not just the combo streak.
  Object.assign(G.player, PLAYER_SPAWN);
  placeAvatarAtPlayer();
  scene.add(avatarGroup);
  if(window.SFX) SFX.thud();
  G.launching = false;
  G.locked = false;
}

/* ============================================================
   MAIN LOOP
   ============================================================ */
function loop(){
  rafId = requestAnimationFrame(loop);
  if(!G) return;
  const now = performance.now();
  const dt = clock.getDelta();

  if(!G.launching){
    for(let i = 0; i < G.slots.length; i++){
      const slot = G.slots[i];
      if(!slot) continue;
      const rushMul = (G.loc.twist === 'rush' && G.rushActive) ? 1.3 : 1;
      slot.patienceLeft -= dt * 1000 * rushMul;
      const card = qs('customerRow').children[i];
      const pct = clamp(slot.patienceLeft / slot.patienceTotal, 0, 1);
      const bar = card.querySelector('.pat i');
      bar.style.width = (pct*100) + '%';
      bar.style.background = pct < 0.25 ? 'var(--bad)' : (pct < 0.5 ? 'var(--lemon)' : 'var(--good)');
      if(slot.patienceLeft <= 0){ failOrder('timeout', i); break; }
    }
    if(now >= G.nextSpawnAt){
      const interval = (G.loc.twist === 'rush' && G.rushActive) ? rand(1400, 2600) : rand(2600, 4600);
      G.nextSpawnAt = now + interval;
      spawnCustomer();
    }
    tickTwists(now, dt);

    // tip jar slowly fills with loose change; the jar's coin stack visibly
    // grows so there's a reason to glance over and remember to collect it
    G.tipJar = Math.min(TIP_JAR_CAP, G.tipJar + dt * TIP_JAR_RATE);
    const jarStation = G.stations.filter(s => s.type === 'tip')[0];
    if(jarStation) jarStation.mesh.userData.coins.scale.y = 0.2 + (G.tipJar / TIP_JAR_CAP) * 3;

    updatePlayerMovement(dt);
    updateInteractHint();
  }

  // idle bob for customers
  G.customerMeshes.forEach((m, i) => { if(m) m.position.y = CUSTOMER_BASE_Y + Math.sin(now*0.003 + i) * 0.04; });

  if(!G.launching){
    camera.position.set(G.player.x, EYE_HEIGHT, G.player.z);
    camera.rotation.set(G.player.pitch, G.player.yaw, 0);
  }
  // while launching, the camera holds the spectator shot launchNow() set —
  // see triggerLaunch() — until respawn() clears G.launching next frame.

  if(ragdollGroup){
    ragdollGroup.children.forEach(part => {
      part.userData.vel.y -= 6 * dt;
      part.position.addScaledVector(part.userData.vel, dt);
      part.rotation.x += part.userData.angVel.x * dt;
      part.rotation.y += part.userData.angVel.y * dt;
      part.rotation.z += part.userData.angVel.z * dt;
    });
  }

  // multiplayer presence + peer rendering (peer rail is a DOM rebuild, so
  // it's throttled independently of the 60fps render loop)
  if(window.Net && Net.inLobby){
    Net.sendPresence({location: G.loc.id, anim: G.launching ? 'launched' : 'idle', coins: Save.coins});
    if(now - G.lastPeerRailAt > 400){ G.lastPeerRailAt = now; renderPeerRail(); }
  }

  renderer.render(scene, camera);
}

/* ---------- multiplayer: peers sharing this location ---------- */
const peerState = {}; // uid -> {p, lastSeen}
if(window.Net){
  Net.onPresence(({from, p}) => { peerState[from] = {p, lastSeen: performance.now()}; });
  Net.onPeerLeft(uid => { delete peerState[uid]; });
  Net.onEvent(({from, e}) => {
    const lobby = Net.lobby;
    const m = lobby && lobby.members.find(x => x.id === from);
    const name = m ? m.name : 'Someone';
    if(e.kind === 'launched') toast('🚀 ' + name + ' got launched into orbit!');
    else if(e.kind === 'served') toast('✅ ' + name + ' served a customer!');
  });
}

function renderPeerRail(){
  const rail = qs('peerRail');
  const chip = qs('peerCountChip');
  const lobby = Net.lobby;
  if(!lobby){ rail.innerHTML = ''; chip.classList.add('hidden'); return; }
  chip.classList.remove('hidden');
  chip.textContent = '🌐 ' + lobby.members.length;
  const here = lobby.members.filter(m => m.id !== Net.myId && peerState[m.id] && peerState[m.id].p.location === G.loc.id);
  rail.innerHTML = here.map(m => '<div class="peerchip"><span class="dot"></span>' + escapeHtml(m.name) + '</div>').join('');
}

on(qs('exitStand'), 'click', () => Game.stop());

/* ============================================================
   boot
   ============================================================ */
initMenu();

})();
