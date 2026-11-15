#!/usr/bin/env node
// The boarding ladders: one shared fitting (docs/art/rigs/boatLadderRig.js) hung on each hull at the
// boarding point her deck Def already stores.
//
//   node tools/rig-recipes/boat-ladders.mjs                 # rewrite the sidecar's derived fields
//   node tools/rig-recipes/boat-ladders.mjs --check         # fail if the committed sidecar has drifted
//   node tools/rig-recipes/boat-ladders.mjs --plates <dir>  # write preview plates (no Unity)
//
// WHY A GENERATOR. The ladder you see and the point the boarding rule tests must be the same place.
// So the sidecar never authors a position for a ladder the game already has a point for: each
// entry names a hull, a BoardingPoint id, a kind and which way the skin faces there, and this
// script READS the point out of the deck asset, the waterline out of the hull-mesh asset and the
// shape of her side out of the hull rig's own loft. The ladder hangs on the skin nearest the point
// (straight out along `outward`), and the script refuses a ladder that lands outside the point's
// reach - the reach the boarding rule itself uses. Move the point in the Def and --check goes red
// until the sidecar is regenerated; reshape the hull rig and the pinned digest moves the same way.
//
// STATUS IS DERIVED, never authored. "matches_def": the deck Def has this point id. "proposed": it
// does not, and the entry's `proposedPoint` is this file's suggestion. The day the Def gains the
// id the entry flips to matches_def by itself on the next run, and follows the Def's number.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';

// fileURLToPath, not URL.pathname: the plates are made on a Windows box as well as in CI.
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const RIGS = 'docs/art/rigs';
const CONFIG = 'Assets/_Project/Data/Config/GameConfig.asset';
/** sha256 with CRLF folded to LF, the same convention as lib/rigHost.mjs rigSha256. */
const rigSha256 = (rel) => crypto.createHash('sha256')
  .update(Buffer.from(fs.readFileSync(path.join(REPO, rel)).toString('binary').replace(/\r\n/g, '\n'), 'binary')).digest('hex');
const OUT = `${RIGS}/gameplay/boatLadderRig.ladders.json`;
const AUTHORED = ['hull', 'id', 'kind', 'hullRig', 'hullGlobal', 'variant', 'deckAsset', 'hullMeshAsset',
                  'outward', 'proposedPoint', '_note'];
const r3 = (n) => Math.round(n * 1000) / 1000;
const read = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');

function load(rigFile, global) {
  const root = global.split('.')[0];
  if (globalThis[root] == null) (0, eval)(read(`${RIGS}/${rigFile}`));
  if (globalThis[root] == null) throw new Error(`${rigFile} ran but did not install globalThis.${root}`);
  return global.split('.').reduce((o, k) => o?.[k], globalThis);
}

/**
 * One hull, whatever shape her rig exports it in: a plain hull global (LobsterBoatIso), a hull
 * inside a family global (SportFisherIso2.CONVERTIBLE), or one variant of a generator
 * (LobsterBoatVariantsIso + {size, style, region}). Returns what the ladder needs and no more.
 */
function hullOf(entry) {
  const g = load(entry.hullRig, entry.hullGlobal);
  if (!g) throw new Error(`${entry.hull}: ${entry.hullRig} has no ${entry.hullGlobal}`);
  const v = entry.variant;
  const loft = v ? g.loftOf(v) : (g.loft || {});
  const pick = (k) => loft[k] || g[k];
  const cell = loft.cell || { W: g.W, H: g.H, cx: g.pivot.x, cy: g.pivot.y, S: g.PX };
  const hull = { PX: g.PX, order: g.order, defaultElev: g.defaultElev,
    render: v ? (d, o) => g.render(d, { ...v, ...o }) : (d, o) => g.render(d, o),
    faces: v ? () => g.faces(v) : () => g.faces(),
    loft: { cell, shade: loft.shade || {}, sheerZ: pick('sheerZ'), halfAtZ: pick('halfAtZ'), station: pick('station') } };
  for (const k of ['sheerZ', 'halfAtZ', 'station'])
    if (typeof hull.loft[k] !== 'function') throw new Error(`${entry.hull}: ${entry.hullGlobal} does not export loft.${k}`);
  return hull;
}

/** Every BoardingPoint a deck asset serializes: id -> { kind, point:[x,y], reach }. */
export function boardingPoints(deckAssetRel) {
  const out = {};
  const re = /- Id: (\S+)\r?\n\s+Kind: (\d+)\r?\n\s+DeckLocalMeters: \{x: ([-\d.e]+), y: ([-\d.e]+)\}\r?\n\s+ReachMetres: ([-\d.e]+)/g;
  for (const m of read(deckAssetRel).matchAll(re))
    out[m[1]] = { kind: +m[2], point: [+m[3], +m[4]], reach: +m[5] };
  return out;
}

function scalar(assetRel, field) {
  const m = read(assetRel).match(new RegExp(`^\\s*${field}: ([-\\d.e]+)`, 'm'));
  if (!m) throw new Error(`${assetRel} does not serialize ${field}`);
  return +m[1];
}

function specOf(entry) {
  const hull = hullOf(entry);
  const def = boardingPoints(entry.deckAsset)[entry.id];
  if (def && def.kind !== 0) throw new Error(`${entry.hull}: '${entry.id}' is not a Ladder point in the Def`);
  if (!def && !entry.proposedPoint) throw new Error(`${entry.hull}: ${entry.deckAsset} has no boarding point '${entry.id}' and the entry proposes none`);
  const status = def ? 'matches_def' : 'proposed';
  const point = def ? def.point : entry.proposedPoint;
  const reach = def && def.reach > 0 ? def.reach : scalar(CONFIG, 'BoardingPointReachMetres');

  // The ladder hangs on the skin, straight out from the point: the transom for a stern ladder,
  // her side at the sheer for a side ladder.
  const [nx, ny] = entry.outward;
  let at, zTop;
  if (nx === 0 && ny === -1) {
    const s0 = hull.loft.station(0);            // the transom section: {y} on most rigs, a ring of [x,y,z] on the sport fishers
    at = [point[0], Array.isArray(s0) ? s0[0][1] : s0.y]; zTop = hull.loft.sheerZ(at[1]);
  }
  else if (ny === 0 && Math.abs(nx) === 1) { zTop = hull.loft.sheerZ(point[1]); at = [nx * hull.loft.halfAtZ(point[1], zTop), point[1]]; }
  else throw new Error(`${entry.hull}/${entry.id}: outward must be [0,-1], [1,0] or [-1,0]`);
  at = at.map(r3); zTop = r3(zTop);
  const off = r3(Math.hypot(at[0] - point[0], at[1] - point[1]));
  if (off > reach) throw new Error(`${entry.hull}/${entry.id}: the skin is ${off} m from the boarding point, outside its ${reach} m reach`);

  return { hull, status, point, reach, off,
           spec: { kind: entry.kind, at, outward: entry.outward, px: hull.PX, zTop,
                   zWater: scalar(entry.hullMeshAsset, 'RestingDraftMeters') } };
}

export function build() {
  const Ladder = load('boatLadderRig.js', 'BoatLadder');
  const committed = JSON.parse(read(OUT));
  const pins = { 'boatLadderRig.js': rigSha256(`${RIGS}/boatLadderRig.js`) };
  const ladders = committed.LADDERS.map((old) => {
    const entry = {};
    for (const k of AUTHORED) if (old[k] !== undefined) entry[k] = old[k];
    const { hull, status, point, reach, off, spec } = specOf(entry);
    pins[entry.hullRig] = rigSha256(`${RIGS}/${entry.hullRig}`);
    const a = Ladder.anchors(spec);
    return { ...entry, status,
      boardingPoint: point, reach_m: reach, ladderAt: spec.at, pointToLadder_m: off,
      px_per_m: hull.PX, zTop: spec.zTop, zWater: spec.zWater, climb_m: r3(spec.zTop - spec.zWater),
      head: a.head, foot: a.foot, grabTop: a.grabTop, rungsUnderWater: a.rungsUnderWater,
      faces: Ladder.faces(spec).length };
  });
  return {
    rig: 'boatLadderRig.js',
    derivedFromRigSha256: pins,
    _pins: 'LF sha256 of each source (CRLF folded to LF), the tools/rig-recipes convention: the ladder rig, plus every hull rig a side was read from.',
    generator: 'node tools/rig-recipes/boat-ladders.mjs (--check fails on drift). Only hull, id, kind, hullRig, hullGlobal, variant, deckAsset, hullMeshAsset, outward, proposedPoint and _note are authored; every other field is derived. Do not hand-edit the derived fields.',
    _derivation: 'status = matches_def when the deck asset has BoardingPoints[id], else proposed. boardingPoint = that point\'s DeckLocalMeters (or proposedPoint). reach_m = the point\'s ReachMetres, or GameConfig.BoardingPointReachMetres when 0. ladderAt = the hull skin straight out from the point along outward: [x, loft.station(0).y] on the transom, [+-loft.halfAtZ(y, zTop), y] on her side. zTop = loft.sheerZ at the ladder. zWater = RestingDraftMeters in the hull-mesh asset. head, foot, grabTop = BoatLadder.anchors(spec).',
    frame: { units: 'm', origin: 'amidships, keel bottom, centreline', axes: '+x starboard, +y bow, +z up',
             heading_independent: true,
             note: 'boardingPoint and ladderAt are [x, y] in the SAME frame as BoardingPoint.DeckLocalMeters in the deck Def.' },
    kinds: Ladder.KINDS,
    LADDERS: ladders,
  };
}

// One entry per line: 27 hulls stay readable, and a moved point is a one-line diff.
const text = (o) => {
  const { LADDERS, ...head } = o;
  const top = JSON.stringify(head, null, 2).replace(/\n}$/, '');
  return `${top},\n  "LADDERS": [\n${LADDERS.map(e => '    ' + JSON.stringify(e)).join(',\n')}\n  ]\n}\n`;
};
const same = (a, b, i) => a[i] === b[i] && a[i + 1] === b[i + 1] && a[i + 2] === b[i + 2] && a[i + 3] === b[i + 3];

/** One before|after plate per hull CLASS: the first hull of each rig file, all her ladders, best heading. */
function plates(dir) {
  const Ladder = load('boatLadderRig.js', 'BoatLadder');
  fs.mkdirSync(dir, { recursive: true });
  const firstHull = new Map();
  for (const e of build().LADDERS) if (!firstHull.has(e.hullRig)) firstHull.set(e.hullRig, e.hull);
  const all = build().LADDERS;
  for (const [, hullId] of firstHull) {
    const entries = all.filter(e => e.hull === hullId);
    const hull = hullOf(entries[0]), { W, H } = hull.loft.cell;
    const specs = entries.map(e => specOf(e).spec);
    let best = null;
    for (let d = 0; d < 8; d++) {
      const before = hull.render(d, {}), after = Ladder.renderOn(hull, d, { ladders: specs });
      let n = 0, x0 = W, y0 = H, x1 = -1, y1 = -1;
      for (let i = 0; i < W * H; i++) {
        if (same(before, after, i * 4)) continue;
        n++; const x = i % W, y = (i / W) | 0;
        if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
      }
      if (!best || n > best.n) best = { d, n, box: [x0, y0, x1, y1], before, after };
    }
    if (!best.n) throw new Error(`${hullId}: no heading shows her ladder`);
    // before | after, cropped round the ladders and scaled up with hard pixels
    const s = best, pad = 48;
    const cx0 = Math.max(0, s.box[0] - pad), cy0 = Math.max(0, s.box[1] - pad);
    const cw = Math.min(W, s.box[2] + pad + 1) - cx0, ch = Math.min(H, s.box[3] + pad + 1) - cy0;
    const k = Math.max(1, Math.min(4, Math.floor(1400 / (cw * 2))));
    const gap = 4, PW = (cw * 2 + gap) * k, PH = ch * k, img = Buffer.alloc(PW * PH * 4);
    const sea = [44, 74, 92];
    for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) {
      const sx = (x / k) | 0, sy = (y / k) | 0, o = (y * PW + x) * 4;
      let src = null, px = sx;
      if (sx < cw) src = s.before; else if (sx >= cw + gap) { src = s.after; px = sx - cw - gap; }
      let c = [18, 22, 26];
      if (src) { const i = ((cy0 + sy) * W + cx0 + px) * 4; c = src[i + 3] ? [src[i], src[i + 1], src[i + 2]] : sea; }
      img[o] = c[0]; img[o + 1] = c[1]; img[o + 2] = c[2]; img[o + 3] = 255;
    }
    const file = `${hullId.replace('boat.', '')}-${entries.map(e => e.id).join('+')}-${hull.order[s.d]}-before-after-x${k}.png`;
    fs.writeFileSync(path.join(dir, file), encodePng(img, PW, PH));
    console.log(`${file}: ${s.n} ladder pixels`);
  }
}

const args = process.argv.slice(2);
const next = text(build());
if (args.includes('--check')) {
  if (read(OUT).replace(/\r\n/g, '\n') !== next) {
    console.error(`DRIFT: ${OUT} is not what the rigs and the deck assets say. Re-run without --check.`);
    process.exit(1);
  }
  console.log(`ok: ${OUT} matches the rigs and the deck assets (${JSON.parse(next).LADDERS.length} ladders)`);
} else if (args.includes('--plates')) {
  plates(path.resolve(args[args.indexOf('--plates') + 1]));
} else if (args.includes('--table')) {
  for (const e of JSON.parse(next).LADDERS)
    console.log(`| ${e.hull} | \`${e.id}\` | (${e.boardingPoint.join(', ')}) | ${e.kind} | ${e.climb_m} | ${e.pointToLadder_m} | ${e.status} |`);
} else {
  fs.writeFileSync(path.join(REPO, OUT), next);
  console.log(`wrote ${OUT}`);
}
