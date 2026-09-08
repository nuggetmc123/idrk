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
  renderRecipes();
  renderAchievements();
  initMultiplayerPanel();
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
let camBase = null;   // built lazily in initEngineOnce() — see note there

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
  camBase = {pos:new THREE.Vector3(0,3.3,7.2), look:new THREE.Vector3(0,1.5,-2)};
  const host = qs('canvasHost');
  scene = new THREE.Scene();
  camera = new THREE.PerspectiveCamera(52, 16/9, 0.1, 200);
  renderer = new THREE.WebGLRenderer({antialias:true});
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  host.appendChild(renderer.domElement);
  clock = new THREE.Clock();
  window.addEventListener('resize', resizeRenderer);
}

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

/* A blocky low-poly person: six boxes. Cheap, readable at a distance,
   and easy to blow apart into a ragdoll later — see triggerLaunch(). */
function buildFigure(shirtColor, skinColor){
  const g = new THREE.Group();
  const shirtMat = new THREE.MeshStandardMaterial({color: shirtColor || '#3fa7ff'});
  const skinMat  = new THREE.MeshStandardMaterial({color: skinColor || '#ffd6a5'});
  const legMat   = new THREE.MeshStandardMaterial({color:'#2b2140'});

  const torso = new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.68, 0.34), shirtMat);
  torso.position.y = 1.05;
  const head = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.4, 0.4), skinMat);
  head.position.y = 1.6;
  const armGeo = new THREE.BoxGeometry(0.17, 0.52, 0.17);
  const armL = new THREE.Mesh(armGeo, shirtMat); armL.position.set(-0.4, 1.05, 0);
  const armR = new THREE.Mesh(armGeo, shirtMat); armR.position.set(0.4, 1.05, 0);
  const legGeo = new THREE.BoxGeometry(0.22, 0.6, 0.22);
  const legL = new THREE.Mesh(legGeo, legMat); legL.position.set(-0.15, 0.38, 0);
  const legR = new THREE.Mesh(legGeo, legMat); legR.position.set(0.15, 0.38, 0);

  g.add(torso, head, armL, armR, legL, legR);
  g.userData.parts = [torso, head, armL, armR, legL, legR];
  return g;
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
      for(let i = 0; i < 3; i++){
        const bush = new THREE.Mesh(new THREE.SphereGeometry(0.7, 10, 10), new THREE.MeshStandardMaterial({color:'#3f8f4a'}));
        bush.position.set((i - 1) * 3.2, 0.5, -3.4);
        envGroup.add(bush);
      }
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
    avatarGroup = buildFigure('#ffd23f', '#ffd6a5');
    avatarGroup.position.set(0, 0, 1.1);
    avatarGroup.scale.setScalar(0.85);
    scene.add(avatarGroup);
    if(ragdollGroup){ disposeGroup(ragdollGroup); ragdollGroup = null; }

    const capacity = 1 + Save.upgradeLevel('capacity');
    G = {
      loc, capacity,
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
      spawnedRareCustomer: false
    };
    availableIngredients(loc).forEach(id => { G.frost[id] = 0; });

    Save.setLastLocation(locationId);
    buildCustomerRow(capacity);
    renderIngredientBar();
    updateCupDisplay();
    qs('mixMeterWrap').classList.remove('show');
    qs('btnStir').classList.remove('show');
    qs('btnPour').disabled = true;
    const banner = qs('twistBanner');
    banner.classList.add('hidden');
    qs('launchOverlay').classList.remove('show');
    qs('peerCountChip').classList.toggle('hidden', !(window.Net && Net.inLobby));

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
    if(window.Net) Net.sendPresence({location:'menu', anim:'idle', coins:Save.coins});
    Save.flushNow();
    document.body.classList.remove('in-game');
    qs('gameScreen').classList.add('hidden');
    qs('titleScreen').classList.remove('hidden');
    renderLocations(); renderUpgrades(); renderRecipes(); renderAchievements();
  }
};
window.Game = Game;

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
      clone.position.copy(wp);
      clone.quaternion.copy(part.getWorldQuaternion(new THREE.Quaternion()));
      clone.userData.vel = new THREE.Vector3(rand(-2,2), rand(9,13), rand(-2,2));
      clone.userData.angVel = new THREE.Vector3(rand(-8,8), rand(-8,8), rand(-8,8));
      ragdollGroup.add(clone);
    });
    scene.remove(g); g.visible = false;
    scene.add(ragdollGroup);
    ragdollGroup.userData.startedAt = performance.now();

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
  avatarGroup.position.set(0, 0, 1.1);
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
  }

  // idle bob for customers + camera
  G.customerMeshes.forEach((m, i) => { if(m) m.position.y = CUSTOMER_BASE_Y + Math.sin(now*0.003 + i) * 0.04; });
  camera.position.set(camBase.pos.x, camBase.pos.y + Math.sin(now*0.0005)*0.05, camBase.pos.z);
  camera.lookAt(camBase.look);

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
