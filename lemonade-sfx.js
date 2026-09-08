/* ============================================================
   SILLY SOUND EFFECTS
   Every sound in this game is synthesized on the fly with the Web Audio
   API — oscillators, noise buffers and gain envelopes glued together at
   call time. No audio files, so nothing to download and nothing that can
   404: SFX.rocket() just works the moment the page does.

   The AudioContext is created lazily and resumed on the first user
   gesture (autoplay policies block sound before that everywhere), so
   every public function is safe to call at any time — before the
   context exists, it's a no-op.
   ============================================================ */
(function(){
"use strict";

let ctx = null;
function ac(){
  if(ctx) return ctx;
  const C = window.AudioContext || window.webkitAudioContext;
  if(!C) return null;
  ctx = new C();
  return ctx;
}

/* Call this from the first click/tap the page gets. */
function unlock(){
  const c = ac();
  if(c && c.state === 'suspended') c.resume().catch(() => {});
}

function now(){ return ac() ? ac().currentTime : 0; }

/* A tone that glides from `f0` to `f1` Hz over `dur` seconds, shaped by
   `env` (0..1 -> gain), through one of the built-in oscillator shapes. */
function tone(f0, f1, dur, opts){
  const c = ac();
  if(!c) return;
  opts = opts || {};
  const type = opts.type || 'sine';
  const vol = opts.vol == null ? 0.18 : opts.vol;
  const delay = opts.delay || 0;
  const t0 = c.currentTime + delay;

  const osc = c.createOscillator();
  const gain = c.createGain();
  osc.type = type;
  osc.frequency.setValueAtTime(f0, t0);
  osc.frequency.exponentialRampToValueAtTime(Math.max(1, f1), t0 + dur);
  gain.gain.setValueAtTime(0, t0);
  gain.gain.linearRampToValueAtTime(vol, t0 + Math.min(0.02, dur * 0.2));
  gain.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
  osc.connect(gain).connect(c.destination);
  osc.start(t0);
  osc.stop(t0 + dur + 0.02);
}

/* White noise burst — used for splats, static, and the rocket's roar. */
function noiseBurst(dur, opts){
  const c = ac();
  if(!c) return;
  opts = opts || {};
  const vol = opts.vol == null ? 0.15 : opts.vol;
  const delay = opts.delay || 0;
  const t0 = c.currentTime + delay;

  const len = Math.max(1, Math.floor(c.sampleRate * dur));
  const buf = c.createBuffer(1, len, c.sampleRate);
  const data = buf.getChannelData(0);
  for(let i = 0; i < len; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / len);

  const src = c.createBufferSource();
  src.buffer = buf;
  const filt = c.createBiquadFilter();
  filt.type = opts.filter || 'lowpass';
  filt.frequency.setValueAtTime(opts.freq || 1200, t0);
  if(opts.sweepTo) filt.frequency.exponentialRampToValueAtTime(opts.sweepTo, t0 + dur);
  const gain = c.createGain();
  gain.gain.setValueAtTime(vol, t0);
  gain.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
  src.connect(filt).connect(gain).connect(c.destination);
  src.start(t0);
}

/* A short sequence of plain notes, each `{f, dur, delay, type, vol}`. */
function notes(seq){ seq.forEach(n => tone(n.f, n.f, n.dur, n)); }

const SFX = {
  unlock,

  click(){ tone(520, 700, 0.05, {type:'square', vol:0.08}); },

  /* An ingredient plinks into the pitcher — pitch wiggles a bit per call
     so mashing the same bin ten times in a row doesn't sound identical. */
  addIngredient(){
    const wobble = 1 + (Math.random() - 0.5) * 0.3;
    tone(320 * wobble, 480 * wobble, 0.09, {type:'triangle', vol:0.14});
  },

  mixWhirl(){ noiseBurst(0.4, {vol:0.10, filter:'bandpass', freq:900, sweepTo:1600}); },

  pour(){ noiseBurst(0.5, {vol:0.12, filter:'lowpass', freq:2200, sweepTo:400}); },

  /* A successful, on-time, correct serve. */
  chaChing(){
    notes([
      {f:660, dur:0.09, type:'square', vol:0.12},
      {f:880, dur:0.09, delay:0.08, type:'square', vol:0.12},
      {f:1320,dur:0.22, delay:0.16, type:'square', vol:0.14}
    ]);
  },

  /* A Perfect-mix bonus — brighter, longer sparkle on top of chaChing. */
  perfectDing(){
    notes([
      {f:988, dur:0.12, type:'sine', vol:0.14},
      {f:1318,dur:0.12, delay:0.09, type:'sine', vol:0.14},
      {f:1976,dur:0.30, delay:0.18, type:'sine', vol:0.16}
    ]);
  },

  /* Wrong ingredient, or the pitcher overflows. */
  splat(){ noiseBurst(0.35, {vol:0.20, filter:'lowpass', freq:600, sweepTo:150}); },

  /* A cartoon "wah-wah-wah" for a botched or missed order. */
  failBuzzer(){
    notes([
      {f:300, dur:0.18, type:'sawtooth', vol:0.14},
      {f:260, dur:0.18, delay:0.19, type:'sawtooth', vol:0.14},
      {f:180, dur:0.35, delay:0.38, type:'sawtooth', vol:0.16}
    ]);
  },

  boing(){ tone(180, 900, 0.28, {type:'sine', vol:0.20}); },

  /* Full "you have been launched into orbit" fanfare: boing, ignition
     roar, and a screaming pitch-rising whistle as the player disappears
     upward off the top of the screen. */
  launch(){
    tone(160, 60, 0.15, {type:'sine', vol:0.22});
    noiseBurst(0.9, {delay:0.1, vol:0.22, filter:'lowpass', freq:300, sweepTo:2200});
    tone(300, 2400, 1.1, {delay:0.15, type:'sawtooth', vol:0.10});
  },

  /* The comedic thud of respawning back at the stand. */
  thud(){ noiseBurst(0.18, {vol:0.22, filter:'lowpass', freq:250}); tone(90, 55, 0.15, {vol:0.14}); },

  customerHappy(){
    notes([
      {f:784, dur:0.1, type:'square', vol:0.10},
      {f:988, dur:0.16, delay:0.1, type:'square', vol:0.10}
    ]);
  },

  customerAngry(){
    notes([
      {f:220, dur:0.14, type:'sawtooth', vol:0.12},
      {f:180, dur:0.22, delay:0.12, type:'sawtooth', vol:0.12}
    ]);
  },

  /* Beach twist: a seagull swoops in and yoinks an unclaimed cup. */
  seagull(){
    tone(1200, 1800, 0.12, {type:'square', vol:0.08});
    tone(1800, 900, 0.18, {delay:0.1, type:'square', vol:0.08});
  },

  coinDrop(){ tone(1400, 1800, 0.07, {type:'square', vol:0.09}); },

  achievement(){
    notes([
      {f:523, dur:0.12, type:'triangle', vol:0.15},
      {f:659, dur:0.12, delay:0.11, type:'triangle', vol:0.15},
      {f:784, dur:0.12, delay:0.22, type:'triangle', vol:0.15},
      {f:1046,dur:0.35, delay:0.33, type:'triangle', vol:0.18}
    ]);
  },

  countdownBeep(){ tone(700, 700, 0.08, {type:'square', vol:0.10}); },

  uiOpen(){ tone(440, 660, 0.08, {type:'sine', vol:0.08}); },
  uiClose(){ tone(660, 440, 0.08, {type:'sine', vol:0.08}); },

  freeze(){ tone(2000, 1200, 0.2, {type:'sine', vol:0.08}); },

  /* Zero-G twist: an ingredient bubble drifts free of the bin. */
  float(){ tone(500, 900, 0.3, {type:'sine', vol:0.06}); },

  /* A fishing line hitting water — a soft plop into a burst of ripples. */
  splash(){
    tone(220, 90, 0.12, {type:'sine', vol:0.10});
    noiseBurst(0.3, {delay:0.05, vol:0.10, filter:'bandpass', freq:1400, sweepTo:600});
  }
};

window.SFX = SFX;
window.addEventListener('pointerdown', unlock, {once:true});
window.addEventListener('keydown', unlock, {once:true});

})();
