/* ============================================================
   GAME DATA
   Every number and name that makes one location feel different from
   another lives here, as plain data — ingredients, recipes, locations,
   customer archetypes, shop upgrades and achievements. lemonade-game.js
   never hardcodes "the beach has coconuts"; it reads LOCATIONS and does
   whatever that location says. Add a new location, recipe or achievement
   by editing this file alone.
   ============================================================ */
(function(){
"use strict";

/* ---------- ingredients ----------
   color is the tint mixed into the pitcher liquid (averaged by amount).
   rareOf, when set, marks this as the one secret ingredient a location's
   rare drop can hand out — see LOCATIONS[].rareIngredient below. */
const INGREDIENTS = {
  lemon:        {name:'Fresh Lemon',        icon:'🍋', color:'#f5e642'},
  water:        {name:'Cold Water',         icon:'💧', color:'#cdeeff'},
  sugar:        {name:'Sugar',              icon:'🧂', color:'#ffffff'},
  honey:        {name:'Honey',              icon:'🍯', color:'#e8a33d'},
  ice:          {name:'Ice Cubes',          icon:'🧊', color:'#e3f7ff'},
  mint:         {name:'Mint Leaves',        icon:'🌿', color:'#4caf50'},
  strawberry:   {name:'Strawberry',         icon:'🍓', color:'#ff4d6d'},

  coconut:      {name:'Coconut',            icon:'🥥', color:'#fbf3d9'},
  pineapple:    {name:'Pineapple',          icon:'🍍', color:'#ffe066'},
  watermelon:   {name:'Watermelon',         icon:'🍉', color:'#ff5c7a'},

  espresso:     {name:'Espresso Shot',      icon:'☕', color:'#4b2e1e'},
  chili:        {name:'Chili Pepper',       icon:'🌶️', color:'#ff3b1f'},

  cottonCandy:  {name:'Cotton Candy Fluff', icon:'🍥', color:'#ff9de2'},
  popRocks:     {name:'Pop Rocks',          icon:'💥', color:'#ff6ec7'},
  caramel:      {name:'Caramel Drizzle',    icon:'🟤', color:'#c47a3d'},

  cinnamon:     {name:'Cinnamon',           icon:'🍂', color:'#a0522d'},
  marshmallow:  {name:'Marshmallow',        icon:'☁️', color:'#fff8f0'},
  candyCane:    {name:'Candy Cane',         icon:'🍬', color:'#ff5c7a'},

  moonberry:    {name:'Moonberry',          icon:'🔵', color:'#7b61ff'},
  nebulaFizz:   {name:'Nebula Fizz Powder', icon:'🌌', color:'#9b5de5'},
  zeroGBubbles: {name:'Zero-G Bubbles',     icon:'🫧', color:'#b8f2ff'},

  /* one rare, secret-recipe-only ingredient per location — see rareIngredient below */
  goldDust:       {name:'Golden Dust',              icon:'✨', color:'#ffd700', rare:true},
  pearlSyrup:     {name:'Pearl Shimmer Syrup',      icon:'🦪', color:'#dff6ff', rare:true},
  goldBean:       {name:'Liquid Gold Espresso Bean',icon:'🫘', color:'#caa53d', rare:true},
  rainbowFizz:    {name:'Rainbow Sprinkle Fizz',    icon:'🌈', color:'#ff8ad8', rare:true},
  frostfireGem:   {name:'Frostfire Cinnamon Crystal',icon:'❄️', color:'#8ad9ff', rare:true},
  stardustCore:   {name:'Stardust Nebula Core',     icon:'⭐', color:'#e8d3ff', rare:true}
};

/* ---------- recipes ----------
   ingredients: {id: qty}. difficulty (1-6) drives base patience time and
   pay. secret recipes need their location's rare ingredient and are not
   offered to the player until it's been found at least once. */
const RECIPES = {
  /* Neighborhood */
  classicLemonade: {name:'Classic Lemonade', location:'neighborhood', difficulty:1, pay:12,
    ingredients:{lemon:3, sugar:2, water:2, ice:1}},
  pinkLemonade: {name:'Pink Lemonade', location:'neighborhood', difficulty:2, pay:18,
    ingredients:{lemon:2, sugar:2, water:1, strawberry:2, ice:1}},
  mintySqueeze: {name:'Minty Fresh Squeeze', location:'neighborhood', difficulty:2, pay:20,
    ingredients:{lemon:3, honey:1, mint:2, water:1, ice:1}},
  goldenLemonade: {name:'Golden Lemonade', location:'neighborhood', difficulty:3, pay:60, secret:true,
    ingredients:{lemon:3, sugar:2, water:1, goldDust:1}},

  /* Beach */
  coconutCooler: {name:'Coconut Cooler', location:'beach', difficulty:2, pay:25,
    ingredients:{lemon:2, coconut:2, water:1, ice:2}},
  pineappleWave: {name:'Pineapple Wave', location:'beach', difficulty:2, pay:28,
    ingredients:{lemon:2, pineapple:2, sugar:1, ice:1}},
  watermelonSplash: {name:'Watermelon Splash', location:'beach', difficulty:3, pay:35,
    ingredients:{lemon:2, watermelon:2, mint:1, ice:1}},
  mermaidsKiss: {name:"Mermaid's Kiss", location:'beach', difficulty:4, pay:100, secret:true,
    ingredients:{lemon:2, coconut:1, watermelon:1, pearlSyrup:1}},

  /* City */
  cityCitrus: {name:'City Citrus', location:'city', difficulty:2, pay:30,
    ingredients:{lemon:3, sugar:2, water:1, ice:2}},
  espressoZest: {name:'Espresso Zest', location:'city', difficulty:3, pay:45,
    ingredients:{lemon:2, espresso:1, sugar:2, ice:1}},
  chiliZinger: {name:'Chili Zinger', location:'city', difficulty:3, pay:48,
    ingredients:{lemon:3, chili:1, honey:1, ice:1}},
  midnightZest: {name:'Midnight Zest', location:'city', difficulty:5, pay:120, secret:true,
    ingredients:{lemon:2, espresso:2, chili:1, goldBean:1}},

  /* Amusement Park */
  funnelFizz: {name:'Funnel Fizz', location:'park', difficulty:2, pay:55,
    ingredients:{lemon:2, sugar:2, caramel:1, ice:1}},
  cottonCandyCloud: {name:'Cotton Candy Cloud', location:'park', difficulty:3, pay:65,
    ingredients:{lemon:2, cottonCandy:2, water:1, ice:1}},
  popRockPopper: {name:'Pop Rock Popper', location:'park', difficulty:4, pay:75,
    ingredients:{lemon:2, popRocks:2, sugar:1, ice:1}},
  carnivalComet: {name:'Carnival Comet', location:'park', difficulty:5, pay:160, secret:true,
    ingredients:{lemon:2, cottonCandy:1, popRocks:1, rainbowFizz:1}},

  /* Snowy Mountain */
  hotLemonCider: {name:'Hot Lemon Cider', location:'mountain', difficulty:3, pay:80,
    ingredients:{lemon:3, honey:1, cinnamon:1, water:1}},
  frostyMint: {name:'Frosty Mint Squeeze', location:'mountain', difficulty:3, pay:85,
    ingredients:{lemon:2, mint:2, ice:3}},
  marshmallowMelt: {name:'Marshmallow Melt', location:'mountain', difficulty:4, pay:95,
    ingredients:{lemon:2, marshmallow:2, honey:1, water:1}},
  yetisRevenge: {name:"Yeti's Revenge", location:'mountain', difficulty:5, pay:200, secret:true,
    ingredients:{lemon:3, candyCane:2, frostfireGem:1}},

  /* Space Station */
  zeroGFizz: {name:'Zero-G Fizz', location:'space', difficulty:4, pay:110,
    ingredients:{lemon:2, nebulaFizz:2, water:1, ice:1}},
  moonberryBlast: {name:'Moonberry Blast', location:'space', difficulty:4, pay:120,
    ingredients:{lemon:2, moonberry:3, sugar:1, ice:1}},
  astroBubblePop: {name:'Astro Bubble Pop', location:'space', difficulty:5, pay:135,
    ingredients:{lemon:2, zeroGBubbles:2, sugar:1, ice:1}},
  galacticFusion: {name:'Galactic Fusion', location:'space', difficulty:6, pay:300, secret:true,
    ingredients:{lemon:2, moonberry:2, nebulaFizz:1, stardustCore:1}}
};

/* ---------- customer archetypes ----------
   patience is base seconds to serve before they storm off (scaled down
   by recipe difficulty and location twist in-game). tipMax is a bonus
   0..tipMax coins rolled on a "Perfect" serve. */
const CUSTOMER_TYPES = [
  {id:'kid',        location:'neighborhood', name:'Kid on a Bike',    color:'#3fa7ff', patience:26, tipMax:6,
    happy:["BEST. DAY. EVER!", "Whoa, thanks!"], angry:["Aw maaan!", "I'm telling my mom!"]},
  {id:'jogger',      location:'neighborhood', name:'Jogger',          color:'#57c785', patience:20, tipMax:5,
    happy:["Perfect pace fuel!", "Exactly what I needed."], angry:["Ugh, no time for this."]},
  {id:'mailcarrier', location:'neighborhood', name:'Mail Carrier',    color:'#3457d5', patience:34, tipMax:8,
    happy:["Neither rain nor sleet... this is great lemonade.", "You made my whole route!"], angry:["I have MAIL to deliver."]},
  {id:'grumpyneighbor', location:'neighborhood', name:'Grumpy Neighbor', color:'#8d6e63', patience:16, tipMax:3,
    happy:["...fine, it's good.", "Hmph. Acceptable."], angry:["Kids these days!", "Back in my day..."]},

  {id:'surfer',      location:'beach', name:'Surfer',            color:'#ff9f45', patience:22, tipMax:10,
    happy:["Cowabunga, that's tasty!", "Righteous squeeze, dude!"], angry:["Total wipeout, man."]},
  {id:'lifeguard',   location:'beach', name:'Lifeguard',         color:'#e63946', patience:18, tipMax:7,
    happy:["Nice save on that order!", "Refreshing!"], angry:["I have swimmers to watch!"]},
  {id:'tourist',     location:'beach', name:'Sunburnt Tourist',  color:'#ffb4a2', patience:24, tipMax:9,
    happy:["Better than my sunscreen budget!", "Aloha, delicious!"], angry:["My skin AND my drink hurt now."]},
  {id:'sandkid',     location:'beach', name:'Sandcastle Kid',    color:'#ffd166', patience:20, tipMax:5,
    happy:["My sandcastle approves!", "Yummy!"], angry:["My castle collapsed AND this stinks!"]},

  {id:'suit',        location:'city', name:'Businessperson',     color:'#2b2d42', patience:14, tipMax:12,
    happy:["Efficient AND tasty. Rare.", "This is going in my report."], angry:["I have a MEETING."]},
  {id:'cabbie',      location:'city', name:'Cab Driver',         color:'#f4d35e', patience:16, tipMax:10,
    happy:["Better than my last fare!", "Five stars, kid."], angry:["Meter's running, meter's running!"]},
  {id:'performer',   location:'city', name:'Street Performer',   color:'#9b5de5', patience:20, tipMax:11,
    happy:["*jazz hands of approval*", "Encore-worthy lemonade!"], angry:["Booooo! Get off the stage!"]},
  {id:'critic',      location:'city', name:'Food Critic',        color:'#ef233c', patience:12, tipMax:20,
    happy:["...four stars. Maybe five.", "I am shocked. Genuinely."], angry:["Unacceptable. UNACCEPTABLE."]},

  {id:'thrillseeker',location:'park', name:'Woozy Thrill-Seeker',color:'#7bd389', patience:18, tipMax:9,
    happy:["My stomach thanks you.", "The room stopped spinning!"], angry:["I think I'm gonna—"]},
  {id:'clown',       location:'park', name:'Clown',              color:'#ff477e', patience:22, tipMax:8,
    happy:["Honk honk, delicious!", "That's no joke, that's GOOD."], angry:["Not funny! Well— literally, no."]},
  {id:'rideop',      location:'park', name:'Ride Operator',      color:'#ffca3a', patience:16, tipMax:9,
    happy:["Keeps me going all shift!", "Ride's clear, drink's here!"], angry:["Line's backing up here!"]},
  {id:'prizekid',    location:'park', name:'Prize-Hungry Kid',   color:'#4cc9f0', patience:19, tipMax:6,
    happy:["Better than the giant stuffed bear!", "10/10 would win again!"], angry:["Worse than the rigged ring toss!"]},

  {id:'skier',       location:'mountain', name:'Skier',          color:'#00b4d8', patience:20, tipMax:10,
    happy:["Warms me right up!", "Slope-side perfection!"], angry:["Colder than the summit now."]},
  {id:'snowboarder', location:'mountain', name:'Snowboarder',    color:'#f72585', patience:18, tipMax:9,
    happy:["Sick drink, no cap!", "Shredding AND sipping!"], angry:["Bailed on that order, dude."]},
  {id:'patrol',      location:'mountain', name:'Ski Patrol',     color:'#ff5400', patience:24, tipMax:11,
    happy:["Safety AND deliciousness.", "Rescue mission successful."], angry:["That was a hazard, honestly."]},
  {id:'yeti',        location:'mountain', name:'Yeti', color:'#e5e5e5', patience:30, tipMax:60, rareSpawn:true,
    wantsRecipe:'yetisRevenge',
    happy:["*a deeply satisfied, room-shaking roar*", "The legend... approves."],
    angry:["*a very disappointed, room-shaking roar*"]},

  {id:'astronaut',   location:'space', name:'Astronaut',        color:'#adb5bd', patience:22, tipMax:14,
    happy:["Houston, we have a delicious drink.", "One small sip for a stand..."], angry:["Mission compromised."]},
  {id:'alien',       location:'space', name:'Alien Tourist',    color:'#70e000', patience:24, tipMax:16,
    happy:["Zorp! Best on this planet!", "Taking the recipe back to Zeltron-9!"], angry:["My third eye is unimpressed."]},
  {id:'robotinsp',   location:'space', name:'Robot Barista Inspector', color:'#8d99ae', patience:15, tipMax:13,
    happy:["ANALYSIS: DELICIOUS. RATING: 10/10.", "COMPLIANT. WELL DONE."], angry:["ERROR. FLAVOR MISMATCH. ERROR."]},
  {id:'spacekid',    location:'space', name:'Space Tourist Kid',color:'#ffd60a', patience:20, tipMax:9,
    happy:["Out of this world, literally!", "Best souvenir ever!"], angry:["I want a refund in space-bucks!"]}
];

/* ---------- locations ----------
   twist is a short code lemonade-game.js switches on to run that
   location's signature chaos mechanic. rareIngredient is what a random
   drop at this location can hand out, unlocking its secret recipe. */
const LOCATIONS = [
  {id:'neighborhood', name:'Neighborhood', unlockCost:0,
    tagline:'Where every lemonade empire begins.',
    sky:['#8ec9ff','#e8f7ff'], ground:'#6fbf5a', accent:'#ffcf4a',
    twist:'none',
    recipes:['classicLemonade','pinkLemonade','mintySqueeze'],
    secretRecipe:'goldenLemonade', rareIngredient:'goldDust',
    customers:['kid','jogger','mailcarrier','grumpyneighbor']},

  {id:'beach', name:'Beach', unlockCost:250,
    tagline:'Sun, sand, and seagulls with sticky fingers.',
    sky:['#5cc8ff','#bdf2ff'], ground:'#f4e2b8', accent:'#ff7a59',
    twist:'seagull',
    recipes:['coconutCooler','pineappleWave','watermelonSplash'],
    secretRecipe:'mermaidsKiss', rareIngredient:'pearlSyrup',
    customers:['surfer','lifeguard','tourist','sandkid']},

  {id:'city', name:'City', unlockCost:750,
    tagline:'Rush hour never stops. Neither do the orders.',
    sky:['#7c8db5','#c7cee0'], ground:'#5a5f6b', accent:'#ffd23f',
    twist:'rush',
    recipes:['cityCitrus','espressoZest','chiliZinger'],
    secretRecipe:'midnightZest', rareIngredient:'goldBean',
    customers:['suit','cabbie','performer','critic']},

  {id:'park', name:'Amusement Park', unlockCost:1800,
    tagline:'Bright lights, loud rides, dizzier customers.',
    sky:['#ff9de2','#ffe1f5'], ground:'#8f5fd1', accent:'#4cc9f0',
    twist:'dizzy',
    recipes:['funnelFizz','cottonCandyCloud','popRockPopper'],
    secretRecipe:'carnivalComet', rareIngredient:'rainbowFizz',
    customers:['thrillseeker','clown','rideop','prizekid']},

  {id:'mountain', name:'Snowy Mountain', unlockCost:3500,
    tagline:'The bins keep freezing. So do your fingers.',
    sky:['#dff3ff','#ffffff'], ground:'#eef7ff', accent:'#00b4d8',
    twist:'freeze',
    recipes:['hotLemonCider','frostyMint','marshmallowMelt'],
    secretRecipe:'yetisRevenge', rareIngredient:'frostfireGem',
    customers:['skier','snowboarder','patrol'],
    rareCustomer:'yeti'},

  {id:'space', name:'Space Station', unlockCost:6000,
    tagline:'Zero gravity. Zero patience. Infinite chaos.',
    sky:['#0b0f2e','#241a4d'], ground:'#1c2140', accent:'#9b5de5',
    twist:'gravity',
    recipes:['zeroGFizz','moonberryBlast','astroBubblePop'],
    secretRecipe:'galacticFusion', rareIngredient:'stardustCore',
    customers:['astronaut','alien','robotinsp','spacekid']}
];

/* ---------- shop upgrades ----------
   Five tracks, five levels each, costs climbing 1.6x per level — same
   shape BRAWLBOUND used for its fighter upgrades, because it already
   reads as fair progression: cheap first level, real investment by five. */
const UPGRADE_TRACKS = [
  {id:'speed',    name:'Quick-Squeeze Gloves', icon:'🧤', maxLevel:5, base:80,
    desc:'Every click at the counter lands faster.'},
  {id:'patience', name:"Shade Umbrella",       icon:'⛱️', maxLevel:5, base:90,
    desc:'Customers stay cool, calm, and patient longer.'},
  {id:'luck',     name:"Lucky Rabbit's Foot",  icon:'🍀', maxLevel:5, base:100,
    desc:'Better odds of a rare ingredient drop and a bigger tip.'},
  {id:'capacity', name:'Extra Stools',         icon:'🪑', maxLevel:3, base:250,
    desc:'Serve more customers waiting at once.'},
  {id:'batch',    name:'Jumbo Pitcher',        icon:'🫙', maxLevel:3, base:220,
    desc:'One mix pours more than one cup.'}
];
function upgradeCost(track, level){ // cost of buying INTO this level (1..max)
  const t = UPGRADE_TRACKS.filter(x => x.id === track)[0];
  return Math.round((t ? t.base : 100) * Math.pow(1.6, level - 1));
}

/* ---------- wardrobe ----------
   Purely cosmetic — one hat slot, visible on your own avatar in-game.
   `shape` is read by buildHat() in lemonade-game.js, which knows how to
   build each one out of primitives; adding a hat here needs a matching
   case there. */
const HATS = [
  {id:'party',  name:'Party Hat',        icon:'🎉', price:90,   shape:'party'},
  {id:'bucket', name:'Bucket Hat',       icon:'🪣', price:130,  shape:'bucket'},
  {id:'chef',   name:"Chef's Hat",       icon:'👨‍🍳', price:150,  shape:'chef'},
  {id:'straw',  name:'Straw Sunhat',     icon:'👒', price:120,  shape:'straw'},
  {id:'shades', name:'Cool Shades',      icon:'😎', price:180,  shape:'shades'},
  {id:'foil',   name:'Tinfoil Hat',      icon:'🛸', price:200,  shape:'foil'},
  {id:'mullet', name:'Majestic Mullet',  icon:'💇', price:250,  shape:'mullet'},
  {id:'fish',   name:'Fish Hat',         icon:'🐟', price:400,  shape:'fish'},
  {id:'antenna',name:'Alien Antenna',    icon:'👽', price:600,  shape:'antenna'},
  {id:'crown',  name:'Golden Crown',     icon:'👑', price:1000, shape:'crown'}
];

/* ---------- achievements ----------
   check(stats) reads the grow-only stats object from profile.js and
   returns true once earned. Order matters only for display. */
const ACHIEVEMENTS = [
  {id:'first_sale', name:'Open for Business', desc:'Serve your first customer.', reward:20,
    check:s => s.served >= 1},
  {id:'combo_10', name:'On a Roll', desc:'Reach a 10-order streak with no mess-ups.', reward:100,
    check:s => s.bestCombo >= 10},
  {id:'combo_25', name:'Unstoppable', desc:'Reach a 25-order streak with no mess-ups.', reward:250,
    check:s => s.bestCombo >= 25},
  {id:'served_100', name:'Regular Rush', desc:'Serve 100 customers.', reward:150,
    check:s => s.served >= 100},
  {id:'served_500', name:'Lemonade Legend', desc:'Serve 500 customers.', reward:500,
    check:s => s.served >= 500},
  {id:'launched_1', name:'Houston, We Have a Problem', desc:'Get launched into space for the first time.', reward:25,
    check:s => s.launched >= 1},
  {id:'launched_25', name:'Frequent Flyer', desc:'Get launched into space 25 times.', reward:200,
    check:s => s.launched >= 25},
  {id:'launched_100', name:'Certified Astronaut', desc:'Get launched into space 100 times.', reward:500,
    check:s => s.launched >= 100},
  {id:'all_locations', name:'Frequent Flyer Miles', desc:'Unlock every location.', reward:1000,
    check:s => (s.unlockedLocations || []).length >= LOCATIONS.length},
  {id:'rare_finder', name:'Ooh, Shiny', desc:'Find your first rare ingredient.', reward:100,
    check:s => (s.rareFound || []).length >= 1},
  {id:'rare_collector', name:'Secret Ingredient Hoarder', desc:'Find every rare ingredient.', reward:600,
    check:s => (s.rareFound || []).length >= LOCATIONS.length},
  {id:'secret_recipes', name:'Off the Menu', desc:'Unlock every secret recipe.', reward:800,
    check:s => (s.unlockedRecipes || []).filter(r => RECIPES[r] && RECIPES[r].secret).length >= LOCATIONS.length},
  {id:'perfect_10', name:'Flawless Pour', desc:'Serve 10 Perfect drinks in a row.', reward:300,
    check:s => s.bestPerfectStreak >= 10},
  {id:'multiplayer_friend', name:'Better With Friends', desc:'Play alongside another real player.', reward:150,
    check:s => !!s.playedMultiplayer},
  {id:'big_spender', name:'Big Spender', desc:'Spend 5,000 coins total.', reward:300,
    check:s => s.spent >= 5000},
  {id:'speed_demon', name:'Speed Demon', desc:'Serve a customer in under 5 seconds flat.', reward:150,
    check:s => s.fastestServeMs > 0 && s.fastestServeMs <= 5000},
  {id:'yeti_seen', name:'Abominable Customer', desc:'Serve the secret Yeti.', reward:400,
    check:s => !!s.yetiServed},
  {id:'gone_fishing', name:'Gone Fishing', desc:'Catch something at a fishing spot.', reward:50,
    check:s => (s.fishCaught || 0) >= 1},
  {id:'master_angler', name:'Master Angler', desc:'Catch 20 things fishing.', reward:300,
    check:s => (s.fishCaught || 0) >= 20},
  {id:'fashionista', name:'Fashion Icon', desc:'Own every hat.', reward:400,
    check:s => (s.unlockedHats || []).length >= HATS.length}
];

window.GAME_DATA = {
  INGREDIENTS, RECIPES, CUSTOMER_TYPES, LOCATIONS, UPGRADE_TRACKS, ACHIEVEMENTS, HATS,
  upgradeCost,
  recipeById: id => RECIPES[id],
  locationById: id => LOCATIONS.filter(l => l.id === id)[0],
  customerById: id => CUSTOMER_TYPES.filter(c => c.id === id)[0],
  hatById: id => HATS.filter(h => h.id === id)[0]
};

})();
