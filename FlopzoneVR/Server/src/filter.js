/* ============================================================
   Flopzone word filter — names and the chat auto-mod.

   Kept in sync by hand with Assets/Flopzone/Scripts/Core/WordFilter.cs,
   which runs the same lists on the headset (before a name or message is
   ever sent, and for offline play). This copy is the one that counts:
   a modified client could skip its own check, so the server re-checks
   everything it relays.

   Chat is allowed to say "fuck", "shit" and "damn" (and words built from
   them, like "fucking" or "bullshit"). Anything else on these lists gets
   the sender auto-muted for 15 minutes. Names are stricter: no swearing
   at all, including those three.
   ============================================================ */

// Words that are blocked wherever they appear inside a word ("niggas",
// "retarded"). Written already collapsed: repeated letters squash to one,
// so "fagot" also catches "faggot" and "faaagggot".
const SUBSTRING_WORDS = [
  'niger', 'niga', 'nigr', 'negro', 'fagot', 'faget', 'trany', 'tranie', 'retard',
  'chink', 'kike', 'beaner', 'wetback', 'raghead', 'towelhead', 'cunt', 'whore',
  'slut', 'bitch', 'pusy', 'penis', 'vagina', 'dildo', 'porn', 'nazi', 'hitler',
  'wank', 'twat', 'bastard', 'jiz', 'ashole', 'dick', 'molest', 'pedophil',
  'bolock', 'skank', 'hentai', 'blowjob', 'handjob', 'nude', 'naked', 'orgasm', 'clit'
];

// Words that are only blocked as a whole word, because they hide inside
// innocent ones ("class", "peacock", "scum", "raccoon", "therapist",
// "pistol", "thorny", "basement", "torpedo").
// Compared with runs of 3+ letters squashed to 2, so "asssss" is "ass".
const WHOLE_WORDS = [
  'ass', 'asses', 'dumbass', 'jackass', 'smartass', 'fatass', 'badass', 'kissass',
  'cock', 'cocks', 'cum', 'cums', 'cumming', 'tit', 'tits', 'titty', 'titties',
  'boob', 'boobs', 'fag', 'fags', 'spic', 'spics', 'coon', 'coons', 'gook', 'gooks',
  'dyke', 'dykes', 'homo', 'kys', 'rape', 'raped', 'rapist', 'raping', 'hoe', 'hoes',
  'thot', 'cuck', 'sex', 'sexy', 'milf', 'chode', 'jap', 'japs', 'paki', 'pakis',
  'wop', 'kraut', 'tard', 'tards', 'piss', 'pissed', 'pissing', 'anal', 'horny',
  'boner', 'boners', 'semen', 'prick', 'pricks', 'pedo', 'pedos'
];

// Checked against the whole message with every space and symbol removed,
// so spacing a phrase out doesn't get it past.
const PHRASES = ['kilyourself', 'kilurself', 'gokilyourself', 'hangyourself', 'unalive'];

// Allowed in chat (not in names). Words made from them are fine too, unless
// what's left after taking them out is itself blocked ("shitass" is not ok).
const CHAT_ALLOWED_ROOTS = ['fuck', 'shit', 'damn'];

// Names block the allowed roots too, anywhere inside the name.
const NAME_EXTRA = ['fuck', 'shit', 'damn', 'fck', 'fuk', 'sht', 'dumbass', 'jackass', 'cock', 'boob', 'titty', 'tities', 'sexy'];

function collapse1(s) { return s.replace(/(.)\1+/g, '$1'); }
function collapse2(s) { return s.replace(/(.)\1{2,}/g, '$1$1'); }

function leet(s) {
  return s.toLowerCase()
    .replace(/[@4]/g, 'a').replace(/3/g, 'e').replace(/[1!|]/g, 'i').replace(/0/g, 'o')
    .replace(/[5$]/g, 's').replace(/[7+]/g, 't').replace(/8/g, 'b').replace(/9/g, 'g');
}

function stripInvisible(s) {
  return s.replace(/[\x00-\x1f\x7f​-‏‪-‮⁠-⁤﻿]/g, '');
}

function wordIsBad(word, allowRoots) {
  let w2 = collapse2(word);
  if (allowRoots) {
    for (const root of CHAT_ALLOWED_ROOTS) {
      // match the root even with doubled letters ("fuuck"), then take it out
      const re = new RegExp(root.split('').map(ch => ch + '+').join(''), 'g');
      w2 = w2.replace(re, ' ');
    }
  }
  for (const part of w2.split(' ')) {
    if (!part) continue;
    if (WHOLE_WORDS.includes(part)) return true;
    const p1 = collapse1(part);
    if (SUBSTRING_WORDS.some(b => p1.includes(b))) return true;
  }
  return false;
}

/** Returns a reason string if the chat message breaks the rules, else null. */
export function checkChat(text) {
  const clean = leet(stripInvisible(String(text || '')));
  const letters = collapse1(clean.replace(/[^a-z]/g, ''));
  if (PHRASES.some(p => letters.includes(p))) return 'harmful phrase';
  const words = clean.split(/[^a-z]+/).filter(Boolean);
  for (const w of words) if (wordIsBad(w, true)) return 'bad language';
  // Spaced-out slurs ("n i g g e r"): join runs of single letters back together.
  const joined = clean.split(/[^a-z]+/).filter(Boolean)
    .reduce((acc, w) => { if (w.length === 1 && acc.length && acc[acc.length - 1].single) { acc[acc.length - 1].s += w; } else { acc.push({ s: w, single: w.length === 1 }); } return acc; }, [])
    .map(x => x.s);
  for (const w of joined) if (w.length > 2 && wordIsBad(w, true)) return 'bad language';
  return null;
}

export const NAME_MIN = 3;
export const NAME_MAX = 16;

/** Returns an error message for a bad name, or null if it's allowed. */
export function checkName(name) {
  const raw = stripInvisible(String(name || '')).trim();
  if (raw.length < NAME_MIN) return `Names need at least ${NAME_MIN} characters.`;
  if (raw.length > NAME_MAX) return `Names can be at most ${NAME_MAX} characters.`;
  if (!/^[A-Za-z0-9_]+$/.test(raw)) return 'Use only letters, numbers and _ in your name.';
  const norm = leet(raw).replace(/_/g, '');
  const c1 = collapse1(norm.replace(/[^a-z]/g, ''));
  const c2 = collapse2(norm.replace(/[^a-z]/g, ''));
  if (SUBSTRING_WORDS.some(b => c1.includes(b))) return 'That name is not allowed.';
  if (NAME_EXTRA.some(b => c1.includes(collapse1(b)))) return 'That name is not allowed.';
  if (PHRASES.some(p => c1.includes(p))) return 'That name is not allowed.';
  // Whole-word list: the name may be made of several words glued together
  // (Big_Ass_Guy), so check each _-separated piece and the whole thing.
  const pieces = leet(raw).split(/[_0-9]+/).filter(Boolean).map(collapse2);
  if (pieces.some(p => WHOLE_WORDS.includes(p)) || WHOLE_WORDS.includes(c2)) return 'That name is not allowed.';
  if (/^(admin|moderator|mod|owner|flopzone|system|server)\d*$/i.test(raw)) return 'That name is reserved.';
  return null;
}

export function cleanName(name) {
  return stripInvisible(String(name || '')).trim();
}
