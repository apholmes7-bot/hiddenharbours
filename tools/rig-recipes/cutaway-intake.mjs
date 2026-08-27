#!/usr/bin/env node
// THE CUTAWAY IMPORT, RE-PROVEN IN OUR OWN TREE. This is the check that says the batch-1 hulls
// landed whole — the kit's pass-3 cutaway data AND, on the lobster, our 12-scheme paint kit.
//
//   node tools/rig-recipes/cutaway-intake.mjs                 # the tables, and a pass/fail exit code
//   node tools/rig-recipes/cutaway-intake.mjs --facings 8     # the full turntable instead of four
//   node tools/rig-recipes/cutaway-intake.mjs --hull lobster  # one hull instead of all three
//   node tools/rig-recipes/cutaway-intake.mjs --json          # the same measurements, machine-readable
//
// WHY THIS FILE EXISTS. The cutaway kit arrived with its own adjudication (`ALL CHECKS PASS,
// failCount 0`), run upstream, against upstream's copies. That receipt is worth reading and worth
// nothing as a guarantee about THIS repository: the rigs we import are the ones in `docs/art/rigs/`,
// and one of them — the lobster — is not upstream's file at all but a THREE-WAY MERGE of upstream's
// pass 3 with our own paint kit. A merge is exactly the operation that can drop a side silently, and
// "upstream says it passed" cannot see that. So every claim the import rests on is re-measured here,
// against the bytes actually committed.
//
// WHAT IS CHECKED, and why each one is here:
//   A. STRUCTURE — the two asks the kit answers, held to their own contract. `geometry()` publishes
//      one record per walkable level; a level is ceilinged (`ceilingZ` + `ceiling.kind`) or it is
//      EXPLICITLY open (`ceilingZ: null` + `kind:'open'`), and an ABSENT field is a failure rather
//      than a shrug — an absent field and an open sky must never look the same in this data. Both
//      shared-sole ties (trawler and packet, house vs main_deck) are broken in the data, not in a
//      comment. Every face carries `lv`; rigging is a class of its own; `hull` is a silhouette, not
//      a room, so no cut can take it.
//   B. BYTE-DISCIPLINE — the kit's central claim, which is that pass 3 ADDED data and MOVED nothing.
//      Face stream vs the kit-bundled pass 2 (the sidecars' pinned parents): equal outside `lv`, in
//      count and in order, field by field. Then the pixels: 0 differing bytes across several facings
//      at door 0 and door 1. If pass 3 nudged a vertex, this is where it shows.
//   C. THE MERGE PROOF — the lobster only, and the reason this script was written. The paint kit's
//      own contract says the default 'gelcoat' scheme bakes pixel-identical to the pre-paint rig, so
//      if the merged rig's DEFAULT render is byte-identical to upstream's pass 3, both sides
//      survived: the pass-3 half because the pixels match, the paint half because it is still there
//      to be defaulted. Then all 12 schemes render, share ONE face stream, and differ from one
//      another only in colour — proven by palette-indexing the renders rather than by trusting it.
//
// Every number comes from actually running the committed rigs and actually rendering the cells
// (node's V8 is the same engine ClearScript gives the in-editor baker — ADR 0021), never from a
// table beside them. Nothing here reads the kit's README: a receipt is not evidence of itself.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { REPO } from './lib/csharp.mjs';

const ROOT_RIGS = 'docs/art/rigs';
const PASS2_RIGS = 'docs/art/rigs/boat-interiors-kit/hull-rigs';   // the sidecars' pinned parents
const PASS3_RIGS = 'docs/art/rigs/boat-cutaway-kit/hull-rigs';     // the drop, as it arrived

/** The batch-1 hulls, and what each one is expected to be. */
export const HULLS = [
  {
    key: 'lobster', file: 'lobsterBoatIsoRig.js', global: 'LobsterBoatIso',
    // The one that was MERGED rather than replaced: root canon carried our paint kit, which pass 3
    // does not have, so root is neither side's file and both sides have to be proven present.
    merged: true,
    rooms: ['house', 'cuddy', 'cockpit', 'foredeck'],
    open: ['cockpit', 'foredeck'],
    tie: null,                                   // no shared sole on this hull
  },
  {
    key: 'trawler', file: 'sternTrawlerIsoRig.js', global: 'SternTrawlerIso',
    merged: false,
    rooms: ['house', 'bridge', 'below', 'main_deck'],
    open: ['main_deck'],
    tie: { open: 'main_deck', closed: 'house' },  // both publish soleZ 3.50 — the ceilings break it
  },
  {
    key: 'packet', file: 'coastalPacketIsoRig.js', global: 'CoastalPacketIso',
    merged: false,
    rooms: ['house', 'bridge', 'below', 'main_deck'],
    open: ['main_deck'],
    tie: { open: 'main_deck', closed: 'house' },  // both publish soleZ 5.00
  },
];

/** Every level whose faces a cut may take is a ROOM; these two classes are not rooms. */
const NEVER_CULLED = 'hull';
const RIGGING = 'rigging';

// ---- running a rig --------------------------------------------------------------------------

const read = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');

/** LF-normalised sha256 — the repo's convention for pinning rig bytes (core.autocrlf checkouts). */
export function lfSha256(rel) {
  return crypto.createHash('sha256').update(read(rel).replace(/\r\n/g, '\n'), 'utf8').digest('hex');
}

/**
 * Evaluates a rig VERBATIM and hands back the object it installed. Indirect eval, global scope, no
 * shims — ADR 0021 §5 makes "his file is what runs" the whole point, and this harness has no more
 * licence to patch one than the baker does.
 *
 * Re-running a second version of the same rig simply reassigns the global; the object captured from
 * the first run keeps its own closure, which is what lets pass 2 and pass 3 be compared in one
 * process.
 */
function run(source, globalName, whence) {
  globalThis[globalName] = undefined;
  (0, eval)(source);
  const rig = globalThis[globalName];
  if (rig == null) throw new Error(`${whence} ran but did not install globalThis.${globalName}`);
  return rig;
}

const loadRoot = (h) => run(read(`${ROOT_RIGS}/${h.file}`), h.global, `${ROOT_RIGS}/${h.file}`);
const loadPass3 = (h) => run(read(`${PASS3_RIGS}/${h.file}`), h.global, `${PASS3_RIGS}/${h.file}`);

/**
 * Pass 2 predates `faces()`/`doorFaces` being exported, so its face stream is closure-scoped and
 * unreachable from outside. This is the SAME problem the in-editor extractor has with pre-convention
 * rigs, and this is its answer: widen the single exported object literal IN MEMORY — see
 * `RigMeshExtractor.WidenExportedLiteral`, which does exactly this insertion and refuses on anything
 * but a single anchor.
 *
 * ⚠️ Applied to the read-only PASS-2 BASELINE only, and never written to disk. Pass 3 and the
 * committed root rigs export `faces()` themselves and are run verbatim — a widened file is not the
 * file under test, and the files under test are those.
 */
function loadPass2Widened(h) {
  const rel = `${PASS2_RIGS}/${h.file}`;
  const src = read(rel);
  const anchor = new RegExp(`root\\.${h.global}\\s*=\\s*\\{`, 'g');
  const hits = src.match(anchor);
  if (!hits || hits.length !== 1)
    throw new Error(`${rel}: expected exactly one \`root.${h.global} = {\`, found ${hits ? hits.length : 0}` +
                    ' — the widening aims at a SINGLE exported literal and will not guess between several');
  return run(src.replace(anchor, (m) => `${m} faces:function(){return F;}, doorFaces:doorFaces,`),
             h.global, `${rel} (widened in memory)`);
}

// ---- comparing faces ------------------------------------------------------------------------

/** Every key a face carries EXCEPT the pass-3 addition — the surface that must not have moved. */
const faceKeys = (f) => Object.keys(f).filter(k => k !== 'lv').sort();

/** Deep structural equality over the plain data a face is made of (numbers, strings, arrays). */
function same(a, b) {
  if (a === b) return true;
  if (Array.isArray(a) !== Array.isArray(b)) return false;
  if (Array.isArray(a)) return a.length === b.length && a.every((x, i) => same(x, b[i]));
  if (a && b && typeof a === 'object' && typeof b === 'object') {
    const ka = Object.keys(a).sort(), kb = Object.keys(b).sort();
    return same(ka, kb) && ka.every(k => same(a[k], b[k]));
  }
  return false;
}

/** A value with every function stripped out, so a published table that MIXES data and helpers (the
 *  `loft` block: constants beside `station`/`skin`/`sheerZ`) can still be compared as data. */
function dataOnly(v, depth = 0) {
  if (typeof v === 'function' || depth > 4) return undefined;
  if (Array.isArray(v)) return v.map(x => dataOnly(x, depth + 1));
  if (v && typeof v === 'object') {
    const out = {};
    for (const k of Object.keys(v)) {
      const d = dataOnly(v[k], depth + 1);
      if (d !== undefined) out[k] = d;
    }
    return out;
  }
  return v;
}

/**
 * Two face streams compared as STREAMS — count first, then index by index, field by field, `lv`
 * excluded. Order is part of the claim: the rasteriser is a painter's algorithm over this list, so a
 * reordering that kept every face would still be a different picture.
 */
function diffFaceStreams(a, b, label) {
  const bad = [];
  if (a.length !== b.length) {
    bad.push(`${label}: face COUNT ${a.length} vs ${b.length}`);
    return bad;
  }
  for (let i = 0; i < a.length && bad.length < 8; i++) {
    const ka = faceKeys(a[i]), kb = faceKeys(b[i]);
    if (!same(ka, kb)) { bad.push(`${label}: face ${i} keys [${ka}] vs [${kb}] (outside lv)`); continue; }
    for (const k of ka)
      if (!same(a[i][k], b[i][k])) {
        bad.push(`${label}: face ${i} .${k} ${JSON.stringify(a[i][k])} vs ${JSON.stringify(b[i][k])}`);
        break;
      }
  }
  return bad;
}

// ---- comparing pixels -----------------------------------------------------------------------

/** How many RGBA bytes differ between two renders of the same cell. 0 is the only passing answer. */
function pixelDiff(a, b) {
  if (a.length !== b.length) return { bytes: -1, note: `cell size ${a.length} vs ${b.length} bytes` };
  let bytes = 0, px = 0;
  for (let i = 0; i < a.length; i += 4) {
    let d = 0;
    for (let c = 0; c < 4; c++) if (a[i + c] !== b[i + c]) { d++; bytes++; }
    if (d) px++;
  }
  return { bytes, px };
}

const rgbaKey = (a, i) => ((a[i] << 24 | a[i + 1] << 16 | a[i + 2] << 8 | a[i + 3]) >>> 0);
const hex = (k) => '#' + (k >>> 8).toString(16).padStart(6, '0') + ':' + (k & 255);

/**
 * "The schemes differ only in the ramp colours", stated as something a machine can refuse.
 *
 * The claim is that a scheme swap SUBSTITUTES colours into an unchanged picture. So: read both
 * renders together and build the map from the default's colour at each pixel to the scheme's colour
 * at the same pixel. If that map is a FUNCTION — every occurrence of one default colour becomes the
 * same scheme colour, everywhere — then the only thing that changed is the palette, and the
 * silhouette, the flat-facet shading, the ordered dither and the keyline are all exactly where they
 * were. A SPLIT (one default colour becoming two different colours in different places) is the
 * failure: that is shading or geometry moving, dressed up as paint.
 *
 * ⚠️ A COLLAPSE is not a failure, and this was worth learning the hard way. Two distinct ramp entries
 * are free to resolve to the same hex — TAR BLACK's white cove stripe and her white house genuinely
 * meet at #898c90 — so a scheme may render in FEWER distinct colours than the default. That is a
 * property of the paint the artist chose, not of the mesh, so it is counted and reported rather than
 * refused. (A palette-INDEX comparison, which is the obvious first thing to write, cannot tell the
 * two apart and fails on tarblack.)
 *
 * ⚠️ And a collision does one thing more, which is why `splits` alone is not the verdict either.
 * `matsFor` builds RINDEX — the REVERSE map, colour → {ramp, step} — by walking the ramps in order,
 * so a hex living in two ramps resolves to whichever was indexed last. The edge/keyline pass darkens
 * a pixel one step DOWN the ramp RINDEX names, so on a colliding scheme it walks down the other
 * ramp, and one default colour can legitimately land on two. That is a property of the paint kit
 * (#497) and predates this import: the same tarblack split, the same two colours, the same 43→42, is
 * measurable on the pre-merge rig. So it is ATTRIBUTED — `checkMerge` tolerates a split only where
 * the scheme's own ramps actually collide and both landing colours are in that scheme's palette, and
 * names the colliding hex when it does. Every other split is a failure.
 */
function substitution(refRgba, rgba) {
  if (refRgba.length !== rgba.length)
    return { alphaMoved: -1, splits: [{ why: `cell size ${refRgba.length} vs ${rgba.length} bytes` }] };
  const map = new Map(), splits = [], seen = new Set();
  let alphaMoved = 0;
  for (let i = 0; i < refRgba.length; i += 4) {
    if (refRgba[i + 3] !== rgba[i + 3]) alphaMoved++;
    const from = rgbaKey(refRgba, i), to = rgbaKey(rgba, i);
    const had = map.get(from);
    if (had === undefined) map.set(from, to);
    else if (had !== to && !seen.has(from)) {
      seen.add(from);
      splits.push({ from, a: had, b: to, why: `${hex(from)} → both ${hex(had)} and ${hex(to)}` });
    }
  }
  return { alphaMoved, splits, from: map.size, to: new Set(map.values()).size };
}

/** Every colour a scheme is allowed to put on screen: its four painted ramps, the paint-independent
 *  ones, and the keyline. A pixel outside this set did not come from the palette at all. */
function paletteOf(rig, id) {
  const R = rig.paintRamps(id);
  const shared = [rig.DECKF, rig.GRIP, rig.GLAS, rig.STEEL, rig.IRON].filter(Array.isArray);
  const painted = [R.top, R.boot, R.stripe, R.house];
  const all = [].concat(...painted, ...shared, [rig.KEY]).filter(Boolean).map(c => c.toLowerCase());
  const flat = [].concat(...painted).map(c => c.toLowerCase());
  return { set: new Set(all), collisions: [...new Set(flat.filter((c, i) => flat.indexOf(c) !== i))] };
}

const sha = (buf) => crypto.createHash('sha256').update(Buffer.from(buf.buffer, buf.byteOffset, buf.byteLength)).digest('hex').slice(0, 12);

/** A value as a short string. `HOUSE` and `loft` are pages of nested JSON, and a failure that dumps
 *  both sides of one whole is a failure nobody reads — so it says WHICH FIELD moved instead. */
function brief(v, cap = 160) {
  const s = JSON.stringify(v);
  return s == null ? String(v) : s.length <= cap ? s : s.slice(0, cap) + `… (${s.length} chars)`;
}

/** The dotted paths at which two data values disagree — the readable half of a big-table failure. */
function wherever(a, b, at = '', out = []) {
  if (out.length >= 4 || same(a, b)) return out;
  const objs = a && b && typeof a === 'object' && typeof b === 'object' &&
               Array.isArray(a) === Array.isArray(b);
  if (!objs) { out.push(`${at || '(value)'}: ${brief(a, 60)} → ${brief(b, 60)}`); return out; }
  for (const k of new Set([...Object.keys(a), ...Object.keys(b)])) {
    if (out.length >= 4) break;
    wherever(a[k], b[k], at ? `${at}.${k}` : k, out);
  }
  return out;
}

// ---- A. structure ---------------------------------------------------------------------------

/**
 * The two asks, held to their own contract against the COMMITTED root rig.
 *
 * The strictness that matters is `hasOwnProperty`: a level that simply omits `ceilingZ` reads as
 * `undefined`, which a lenient check would happily call "no ceiling" and pass. The kit's own rider
 * is that an absent field and an open sky must never look the same, so absence fails here.
 */
export function checkStructure(h) {
  const bad = [], rig = loadRoot(h);
  const note = {};

  if (typeof rig.geometry !== 'function') { bad.push('geometry() is not exported'); return { bad, note }; }
  if (typeof rig.faces !== 'function') bad.push('faces() is not exported');
  if (typeof rig.doorFaces !== 'function') bad.push('doorFaces() is not exported');

  let G;
  try { G = rig.geometry(); } catch (e) { bad.push(`geometry() threw: ${e.message}`); return { bad, note }; }

  // --- the ids table: the shared vocabulary the TexCoord1.x bake will use ---
  if (!G.ids || typeof G.ids !== 'object' || Object.keys(G.ids).length === 0)
    bad.push('geometry().ids table is missing or empty');
  else {
    note.ids = G.ids;
    for (const [k, v] of Object.entries(G.ids))
      if (!Number.isInteger(v)) bad.push(`geometry().ids.${k} is ${JSON.stringify(v)}, not an int`);
    if (!(NEVER_CULLED in G.ids)) bad.push(`geometry().ids has no '${NEVER_CULLED}' class`);
    if (!(RIGGING in G.ids)) bad.push(`geometry().ids has no '${RIGGING}' class`);
  }

  // --- one record per walkable level, each ceilinged or EXPLICITLY open ---
  const levels = Array.isArray(G.levels) ? G.levels : [];
  if (!levels.length) bad.push('geometry().levels is missing or empty');
  const seen = [];
  for (const L of levels) {
    const id = L && L.id;
    seen.push(id);
    if (!id) { bad.push('a level record has no id'); continue; }
    if (G.ids && !(id in G.ids)) bad.push(`level '${id}' is not in the ids table`);
    if (!L.deck) bad.push(`level '${id}' has no deck id`);
    if (!Object.prototype.hasOwnProperty.call(L, 'soleZ')) bad.push(`level '${id}' declares no soleZ`);

    if (!Object.prototype.hasOwnProperty.call(L, 'ceilingZ')) {
      bad.push(`level '${id}' has NO ceilingZ field — an absent field is not an open sky`);
      continue;
    }
    const c = L.ceiling;
    if (!c || typeof c.kind !== 'string') { bad.push(`level '${id}' has no ceiling.kind`); continue; }
    if (L.ceilingZ === null) {
      if (c.kind !== 'open') bad.push(`level '${id}' has ceilingZ null but kind '${c.kind}', not 'open'`);
    } else if (typeof L.ceilingZ === 'number') {
      if (c.kind === 'open') bad.push(`level '${id}' is kind 'open' but publishes a ceilingZ`);
      if (c.kind === 'raked' && !(typeof c.zAft === 'number' && typeof c.zFwd === 'number'))
        bad.push(`level '${id}' is raked but publishes no zAft/zFwd`);
      if (c.kind === 'raked' && L.ceilingZ > Math.min(c.zAft, c.zFwd) + 1e-9)
        bad.push(`level '${id}' raked ceilingZ ${L.ceilingZ} is not the honest minimum of ${c.zAft}/${c.zFwd}`);
    } else {
      bad.push(`level '${id}' ceilingZ is ${JSON.stringify(L.ceilingZ)} — expected a number or null`);
    }
  }
  for (const want of h.rooms) if (!seen.includes(want)) bad.push(`no level record for '${want}'`);
  for (const want of h.open) {
    const L = levels.find(x => x && x.id === want);
    if (L && !(L.ceilingZ === null && L.ceiling && L.ceiling.kind === 'open'))
      bad.push(`level '${want}' should be explicitly open`);
  }
  note.levels = levels.map(L => `${L.id}:${L.ceilingZ === null ? 'OPEN' : L.ceilingZ}${L.ceiling ? '/' + L.ceiling.kind : ''}`);

  // --- the shared-sole tie, broken in DATA rather than in prose ---
  if (h.tie) {
    const o = levels.find(x => x && x.id === h.tie.open);
    const c = levels.find(x => x && x.id === h.tie.closed);
    if (!o || !c) bad.push('the tied pair is not both present');
    else {
      if (!(Math.abs(o.soleZ - c.soleZ) < 1e-9))
        bad.push(`'${h.tie.open}' and '${h.tie.closed}' no longer share a sole (${o.soleZ} vs ${c.soleZ}) — ` +
                 'if that is intended, this expectation is what has to change');
      if (o.ceilingZ !== null) bad.push(`the tie is unbroken: '${h.tie.open}' is not open`);
      if (typeof c.ceilingZ !== 'number') bad.push(`the tie is unbroken: '${h.tie.closed}' has no hard ceiling`);
      if (typeof G.tieBreak !== 'string' || !G.tieBreak)
        bad.push('geometry() states no tieBreak — the tie must be broken in the file, not inferred');
      note.tie = `${h.tie.closed} ${c && c.ceilingZ} vs ${h.tie.open} OPEN (shared sole ${o && o.soleZ})`;
    }
  } else if (G.tieBreak) {
    bad.push('geometry() states a tieBreak on a hull with no shared sole');
  }

  // --- ASK B: every face DECLARES its level ---
  const F = rig.faces();
  const tagged = F.filter(f => Object.prototype.hasOwnProperty.call(f, 'lv') &&
                              typeof f.lv === 'string' && f.lv.length > 0);
  note.faces = F.length;
  note.tagged = tagged.length;
  if (tagged.length !== F.length)
    bad.push(`${F.length - tagged.length} of ${F.length} faces carry no lv`);

  const byLv = {};
  for (const f of F) byLv[f.lv] = (byLv[f.lv] || 0) + 1;
  note.byLv = byLv;
  for (const lv of Object.keys(byLv))
    if (G.ids && !(lv in G.ids)) bad.push(`faces are tagged '${lv}', which the ids table does not know`);
  if (!byLv[RIGGING]) bad.push(`no '${RIGGING}' faces — rigging is a dedicated class, not an empty one`);
  if (!byLv[NEVER_CULLED]) bad.push(`no '${NEVER_CULLED}' faces`);

  // The door leaf is house enclosure: it cuts WITH the room, so it must be tagged like one.
  for (const open of [0, 1]) {
    const D = rig.doorFaces({ doorOpen: open });
    if (!D.length) { bad.push(`doorFaces(doorOpen:${open}) is empty`); continue; }
    const untagged = D.filter(f => !Object.prototype.hasOwnProperty.call(f, 'lv')).length;
    if (untagged) bad.push(`doorFaces(doorOpen:${open}) has ${untagged} untagged faces`);
    if (open === 0) note.door = D.length;
  }

  // --- `hull` is a silhouette, not a room: no cut can take it ---
  if (seen.includes(NEVER_CULLED))
    bad.push(`'${NEVER_CULLED}' is published as a walkable level — it is the exterior silhouette`);
  const cutAll = F.filter(f => !new Set(h.rooms.concat([RIGGING])).has(f.lv));
  if (cutAll.filter(f => f.lv === NEVER_CULLED).length !== (byLv[NEVER_CULLED] || 0))
    bad.push(`culling every room removed '${NEVER_CULLED}' faces`);

  return { bad, note };
}

// ---- B. byte-discipline ---------------------------------------------------------------------

/**
 * The kit's central claim, re-measured against OUR committed rig: pass 3 added `lv` and `geometry()`
 * and moved nothing else. Face stream first (which says WHY if it fails), pixels second (which says
 * whether the player would ever see it).
 */
export function checkByteDiscipline(h, dirs) {
  const bad = [], note = {};
  const p2 = loadPass2Widened(h);
  const root = loadRoot(h);

  const a = p2.faces(), b = root.faces();
  note.pass2Faces = a.length;
  note.rootFaces = b.length;
  bad.push(...diffFaceStreams(a, b, 'F'));

  for (const open of [0, 1])
    bad.push(...diffFaceStreams(p2.doorFaces({ doorOpen: open }), root.doorFaces({ doorOpen: open }),
                                `doorFaces(doorOpen:${open})`));

  // Every ANCHOR and published table pass 2 exposed must still be exposed and still say the same
  // thing — the helm, the hauler, the tubs, the nav lights, the door threshold, and each ship's own
  // gear (gantry/gallows/drum, crane/hold). Iterating pass 2's keys is the direction that matters:
  // pass 3 is free to ADD (it adds geometry/faces/LEVEL_IDS, and on the lobster we add the paint
  // kit), and is not free to move anything that was already published, because the interiors and the
  // deck-gear placements are measured off exactly these.
  const anchors = [];
  for (const k of Object.keys(p2)) {
    if (['render', 'faces', 'doorFaces', 'rock'].includes(k)) continue;   // proven by pixels above
    if (!(k in root)) { bad.push(`root no longer publishes '${k}', which pass 2 did`); continue; }
    if (typeof p2[k] === 'function') {
      if (typeof root[k] !== 'function') { bad.push(`'${k}' was a function in pass 2, now ${typeof root[k]}`); continue; }
      for (const dir of dirs) {
        let a, b;
        try { a = dataOnly(p2[k](dir, {})); b = dataOnly(root[k](dir, {})); }
        catch (e) { bad.push(`'${k}'(${dir}) threw: ${e.message}`); break; }
        if (!same(a, b)) { bad.push(`anchor '${k}'(dir ${dir}) moved at ${wherever(a, b).join('; ')}`); break; }
      }
    } else if (!same(dataOnly(p2[k]), dataOnly(root[k]))) {
      bad.push(`published table '${k}' changed at ${wherever(dataOnly(p2[k]), dataOnly(root[k])).join('; ')}`);
      continue;
    }
    anchors.push(k);
  }
  note.anchors = anchors.length;
  note.anchorNames = anchors;

  let renders = 0, worst = 0;
  for (const dir of dirs) for (const doorOpen of [0, 1]) {
    const d = pixelDiff(p2.render(dir, { doorOpen }), root.render(dir, { doorOpen }));
    renders++;
    if (d.bytes !== 0) {
      worst = Math.max(worst, d.bytes);
      if (bad.length < 12)
        bad.push(`dir ${dir} door ${doorOpen}: ${d.bytes < 0 ? d.note : `${d.px} px / ${d.bytes} bytes differ`}`);
    }
  }
  note.renders = renders;
  note.worstBytes = worst;
  return { bad, note };
}

// ---- C. the merge proof ----------------------------------------------------------------------

/**
 * The lobster, and the whole reason this file is committed rather than run once and thrown away.
 *
 * Root is a three-way merge: base + OUR paint kit + THEIR pass 3. Each half is proven by a different
 * measurement, and neither measurement can be passed by dropping the other side:
 *   · the pass-3 half — the merged rig's DEFAULT render must be byte-identical to the kit's own pass
 *     3. The paint kit's contract is that 'gelcoat' bakes pixel-identical to the pre-paint rig, so a
 *     merge that lost pass-3 geometry, or that let paint leak into the default, shows up here.
 *   · the paint half — all 12 schemes must render, must share ONE face stream (a scheme swap is a
 *     re-raster, not a rebuild), and must differ from one another in colour ONLY.
 */
export function checkMerge(h, dirs) {
  const bad = [], note = {};
  const kit3 = loadPass3(h);
  const root = loadRoot(h);

  if (!Array.isArray(root.PAINTS) || root.PAINTS.length !== 12)
    bad.push(`root exposes ${root.PAINTS ? root.PAINTS.length : 'no'} paint schemes, expected 12`);
  if (typeof root.paintRamps !== 'function') bad.push('root does not export paintRamps()');
  if (kit3.PAINTS) bad.push('the kit pass-3 rig unexpectedly carries paint — the merge premise is wrong');
  for (const sym of ['geometry', 'faces', 'doorFaces'])
    if (typeof root[sym] !== 'function') bad.push(`root lost the pass-3 export '${sym}'`);
  if (!root.loft || !root.HOUSE || !root.DOOR) bad.push('root lost the pass-2 published loft/HOUSE/DOOR');

  // --- the pass-3 half: default render is upstream's, byte for byte ---
  bad.push(...diffFaceStreams(kit3.faces(), root.faces(), 'F (kit pass 3 vs merged root)'));
  let renders = 0;
  for (const dir of dirs) for (const doorOpen of [0, 1]) {
    const d = pixelDiff(kit3.render(dir, { doorOpen }), root.render(dir, { doorOpen }));
    renders++;
    if (d.bytes !== 0 && bad.length < 12)
      bad.push(`DEFAULT render dir ${dir} door ${doorOpen}: ` +
               `${d.bytes < 0 ? d.note : `${d.px} px / ${d.bytes} bytes differ from kit pass 3`}`);
  }
  note.defaultRenders = renders;

  // --- the paint half: 12 schemes, one face stream, colour the only difference ---
  const dir = dirs[0];
  const before = JSON.stringify(root.faces());
  const ref = root.render(dir, {});
  const schemes = [];
  const pixelSha = new Map();
  for (const p of (root.PAINTS || [])) {
    let rgba;
    try { rgba = root.render(dir, { paint: p.id }); }
    catch (e) { bad.push(`scheme '${p.id}' threw: ${e.message}`); continue; }

    if (JSON.stringify(root.faces()) !== before)
      bad.push(`scheme '${p.id}' mutated the face stream — a scheme swap must be a re-raster`);

    const sub = substitution(ref, rgba);
    const pal = paletteOf(root, p.id);
    if (sub.alphaMoved)
      bad.push(`scheme '${p.id}' moved ALPHA on ${sub.alphaMoved} px — the silhouette is not the same picture`);
    // A split is tolerated ONLY where this scheme's own ramps collide (see `substitution` above) and
    // both landing colours are in this scheme's palette. Anything else is shading or geometry moving.
    for (const s of sub.splits) {
      const inPalette = [s.a, s.b].every(k => pal.set.has(hex(k).split(':')[0]));
      if (pal.collisions.length && inPalette) continue;
      bad.push(`scheme '${p.id}' changes more than its ramp colours: ${s.why}` +
               (inPalette ? ' (and its ramps do not collide, so nothing explains it)'
                          : ' (a colour that is not in this scheme\'s palette)'));
    }

    const s = sha(rgba);
    if (pixelSha.has(s) && p.id !== 'gelcoat')
      bad.push(`scheme '${p.id}' renders identically to '${pixelSha.get(s)}' — two schemes, one hull colour`);
    pixelSha.set(s, p.id);
    schemes.push({ id: p.id, label: p.label, colours: sub.to, collapsed: sub.from - sub.to,
                   rampCollisions: pal.collisions, sha: s });
  }
  if (schemes.length && schemes[0].id !== 'gelcoat')
    bad.push(`the first scheme is '${schemes[0].id}', not the default 'gelcoat'`);
  const dflt = schemes.find(s => s.id === (root.defaultPaint || 'gelcoat'));
  if (dflt && dflt.sha !== sha(root.render(dir, {})))
    bad.push('render() with no paint is not the same as render() with the default scheme');

  note.schemes = schemes;
  note.paletteColours = substitution(ref, ref).from;
  return { bad, note };
}

// ---- the run --------------------------------------------------------------------------------

export function verify({ hulls = HULLS, facings = 4 } = {}) {
  const step = Math.max(1, Math.round(8 / facings));
  const dirs = [];
  for (let d = 0; d < 8 && dirs.length < facings; d += step) dirs.push(d);

  const report = { dirs, hulls: [], problems: [] };
  for (const h of hulls) {
    const row = { hull: h.key, file: h.file, lfSha256: lfSha256(`${ROOT_RIGS}/${h.file}`) };
    for (const [name, fn] of [['structure', () => checkStructure(h)],
                              ['byteDiscipline', () => checkByteDiscipline(h, dirs)],
                              ['merge', () => (h.merged ? checkMerge(h, dirs) : null)]]) {
      let r;
      try { r = fn(); } catch (e) { r = { bad: [`${name} threw: ${e.message}`], note: {} }; }
      if (r === null) continue;
      row[name] = r.note;
      for (const b of r.bad) report.problems.push(`${h.key} · ${name}: ${b}`);
    }
    report.hulls.push(row);
  }
  return report;
}

function print(report) {
  console.log(`facings ${report.dirs.join(',')} · door 0 and 1 · pass-2 baseline ${PASS2_RIGS}\n`);
  for (const r of report.hulls) {
    console.log(`── ${r.hull}  ${ROOT_RIGS}/${r.file}`);
    console.log(`   LF sha256      ${r.lfSha256}`);
    if (r.structure) {
      const s = r.structure;
      console.log(`   ids            ${JSON.stringify(s.ids)}`);
      console.log(`   levels         ${(s.levels || []).join('  ')}`);
      if (s.tie) console.log(`   tie broken     ${s.tie}`);
      console.log(`   faces          ${s.faces} (${s.tagged} carry lv)  door leaf ${s.door}`);
      console.log(`   by level       ${Object.entries(s.byLv || {}).map(([k, v]) => `${k} ${v}`).join('  ')}`);
    }
    if (r.byteDiscipline) {
      const b = r.byteDiscipline;
      console.log(`   vs pass 2      ${b.pass2Faces} faces vs ${b.rootFaces}; ` +
                  `${b.renders} renders, ${b.worstBytes} differing bytes`);
      console.log(`   anchors held   ${b.anchors}: ${(b.anchorNames || []).join(' ')}`);
    }
    if (r.merge) {
      const m = r.merge;
      console.log(`   merge proof    ${m.defaultRenders} default renders vs kit pass 3; ` +
                  `${m.schemes.length} schemes over ${m.paletteColours} default palette entries`);
      console.log(`   schemes        ${m.schemes.map(s => `${s.id}:${s.sha.slice(0, 6)}` +
                                                          (s.collapsed ? `(−${s.collapsed})` : '')).join('  ')}`);
      for (const s of m.schemes.filter(x => x.rampCollisions.length))
        console.log(`   ⚠ ramp clash   '${s.id}' resolves two ramp steps to ${s.rampCollisions.join(', ')}, ` +
                    'so its reverse index — and its edge pass — is ambiguous (paint-kit property, predates this import)');
    }
    console.log();
  }
  if (report.problems.length === 0) {
    console.log(`✓ ${report.hulls.length} hulls: geometry declared, every face tagged, both ties broken, ` +
                'nothing moved off pass 2, and the lobster carries BOTH the paint kit and pass 3.');
  } else {
    console.log(`✗ ${report.problems.length} failure(s):`);
    for (const p of report.problems) console.log(`  · ${p}`);
  }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const argv = process.argv.slice(2);
  const at = (flag) => { const i = argv.indexOf(flag); return i >= 0 ? argv[i + 1] : null; };
  const only = at('--hull');
  const hulls = only ? HULLS.filter(h => h.key === only) : HULLS;
  if (only && !hulls.length) {
    console.error(`no such hull '${only}' — known: ${HULLS.map(h => h.key).join(', ')}`);
    process.exit(2);
  }
  const report = verify({ hulls, facings: Number(at('--facings') || 4) });
  if (argv.includes('--json')) console.log(JSON.stringify(report, null, 1));
  else print(report);
  process.exit(report.problems.length ? 1 : 0);
}
