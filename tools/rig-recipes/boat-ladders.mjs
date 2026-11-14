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
// entry names a hull, a BoardingPoint id and a kind, and this script READS the point out of the
// deck asset, the waterline out of the hull-mesh asset and the top of the side out of the hull
// rig's own loft. Move the point in the Def and --check goes red until the sidecar is regenerated;
// reshape the hull rig and the pinned digest goes red the same way.
//
// An entry with status "proposed" is a ladder for a hull whose Def has NO such point yet (the
// tanker). Its position is authored here as `proposedPoint`, and --check fails the day the Def
// gains that id, so the proposal cannot quietly outlive the decision.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';

// fileURLToPath, not URL.pathname: the plates are made on a Windows box as well as in CI.
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const RIGS = 'docs/art/rigs';
/** sha256 with CRLF folded to LF, the same convention as lib/rigHost.mjs rigSha256. */
const rigSha256 = (rel) => crypto.createHash('sha256')
  .update(Buffer.from(fs.readFileSync(path.join(REPO, rel)).toString('binary').replace(/\r\n/g, '\n'), 'binary')).digest('hex');
const OUT = `${RIGS}/gameplay/boatLadderRig.ladders.json`;
const AUTHORED = ['hull', 'id', 'kind', 'status', 'hullRig', 'hullGlobal', 'deckAsset', 'hullMeshAsset',
                  'outward', 'proposedPoint', '_note'];
const r3 = (n) => Math.round(n * 1000) / 1000;
const read = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');

function load(rigFile, global) {
  if (globalThis[global] == null) (0, eval)(read(`${RIGS}/${rigFile}`));
  if (globalThis[global] == null) throw new Error(`${rigFile} ran but did not install globalThis.${global}`);
  return globalThis[global];
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
  const hull = load(entry.hullRig, entry.hullGlobal);
  const pts = boardingPoints(entry.deckAsset);
  let point;
  if (entry.status === 'matches_def') {
    if (!pts[entry.id]) throw new Error(`${entry.hull}: ${entry.deckAsset} has no boarding point '${entry.id}'`);
    if (pts[entry.id].kind !== 0) throw new Error(`${entry.hull}: '${entry.id}' is not a Ladder point in the Def`);
    point = pts[entry.id].point;
  } else if (entry.status === 'proposed') {
    if (pts[entry.id]) throw new Error(`${entry.hull}: the Def now HAS '${entry.id}' - set this entry to matches_def and drop proposedPoint`);
    point = entry.proposedPoint;
  } else throw new Error(`${entry.hull}/${entry.id}: unknown status '${entry.status}'`);
  return { hull, spec: { kind: entry.kind, at: point, outward: entry.outward, px: hull.PX,
                         zTop: r3(hull.loft.sheerZ(point[1])), zWater: scalar(entry.hullMeshAsset, 'RestingDraftMeters') } };
}

export function build() {
  const Ladder = load('boatLadderRig.js', 'BoatLadder');
  const committed = JSON.parse(read(OUT));
  const pins = { 'boatLadderRig.js': rigSha256(`${RIGS}/boatLadderRig.js`) };
  const ladders = committed.LADDERS.map((old) => {
    const entry = {};
    for (const k of AUTHORED) if (old[k] !== undefined) entry[k] = old[k];
    const { hull, spec } = specOf(entry);
    pins[entry.hullRig] = rigSha256(`${RIGS}/${entry.hullRig}`);
    const a = Ladder.anchors(spec);
    const from = entry.status === 'matches_def'
      ? `BoardingPoints['${entry.id}'].DeckLocalMeters in the deck asset`
      : 'proposedPoint (the deck Def has no such point yet)';
    return { ...entry,
      boardingPoint: a.point, px_per_m: hull.PX, zTop: spec.zTop, zWater: spec.zWater,
      freeboard_m: r3(spec.zTop - spec.zWater), head: a.head, foot: a.foot, grabTop: a.grabTop,
      rungsUnderWater: a.rungsUnderWater, faces: Ladder.faces(spec).length,
      _derivation: `boardingPoint = ${from}; zTop = ${entry.hullGlobal}.loft.sheerZ(y); zWater = RestingDraftMeters in the hull-mesh asset; head, foot, grabTop = BoatLadder.anchors(spec)` };
  });
  return {
    rig: 'boatLadderRig.js',
    derivedFromRigSha256: pins,
    _pins: 'LF sha256 of each source (CRLF folded to LF), the tools/rig-recipes convention: the ladder rig, plus every hull rig a sheer height was read from.',
    generator: 'node tools/rig-recipes/boat-ladders.mjs (--check fails on drift). Do not hand-edit the derived fields.',
    frame: { units: 'm', origin: 'amidships, keel bottom, centreline', axes: '+x starboard, +y bow, +z up',
             heading_independent: true,
             note: 'boardingPoint is [x, y] in the SAME frame as BoardingPoint.DeckLocalMeters in the deck Def.' },
    kinds: Ladder.KINDS,
    LADDERS: ladders,
  };
}

const text = (o) => JSON.stringify(o, null, 2) + '\n';
const same = (a, b, i) => a[i] === b[i] && a[i + 1] === b[i + 1] && a[i + 2] === b[i + 2] && a[i + 3] === b[i + 3];

function plates(dir) {
  const Ladder = load('boatLadderRig.js', 'BoatLadder');
  fs.mkdirSync(dir, { recursive: true });
  const byHull = new Map();
  for (const e of build().LADDERS) {
    if (!byHull.has(e.hullGlobal)) byHull.set(e.hullGlobal, []);
    byHull.get(e.hullGlobal).push(e);
  }
  for (const [global, entries] of byHull) {
    const hull = globalThis[global], { W, H } = hull.loft.cell;
    const name = entries[0].hull.replace('boat.', '');
    const befores = []; for (let d = 0; d < 8; d++) befores.push(hull.render(d, {}));
    for (const e of entries) {
      const spec = specOf(e).spec;
      const shots = [];
      for (let d = 0; d < 8; d++) {
        const before = befores[d], after = Ladder.renderOn(hull, d, { ladders: [spec] });
        let n = 0, x0 = W, y0 = H, x1 = -1, y1 = -1;
        for (let i = 0; i < W * H; i++) {
          if (same(before, after, i * 4)) continue;
          n++; const x = i % W, y = (i / W) | 0;
          if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
        }
        shots.push({ d, n, box: [x0, y0, x1, y1], before, after });
      }
      shots.sort((a, b) => b.n - a.n);
      console.log(`${e.hull}/${e.id}: ladder pixels by heading ${shots.map(s => `${hull.order[s.d]}=${s.n}`).join(' ')}`);
      for (const s of shots.slice(0, 2)) {
        if (!s.n) continue;
        // before | after, cropped round the ladder and scaled up with hard pixels
        const pad = 40, k = hull.PX >= 32 ? 4 : 3;
        const cx0 = Math.max(0, s.box[0] - pad), cy0 = Math.max(0, s.box[1] - pad);
        const cw = Math.min(W, s.box[2] + pad + 1) - cx0, ch = Math.min(H, s.box[3] + pad + 1) - cy0;
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
        fs.writeFileSync(path.join(dir, `${name}-${e.id}-${hull.order[s.d]}-before-after-x${k}.png`), encodePng(img, PW, PH));
      }
    }
    // one whole-cell plate with every ladder this hull has, at the heading that shows the most of them
    const specs = entries.map(e => specOf(e).spec);
    let best = null;
    for (let d = 0; d < 8; d++) {
      const after = Ladder.renderOn(hull, d, { ladders: specs });
      let n = 0; for (let i = 0; i < W * H; i++) if (!same(befores[d], after, i * 4)) n++;
      if (!best || n > best.n) best = { n, d, after };
    }
    fs.writeFileSync(path.join(dir, `${name}-all-ladders-${hull.order[best.d]}-full.png`),
                     encodePng(Buffer.from(best.after.buffer, best.after.byteOffset, best.after.length), W, H));
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
} else {
  fs.writeFileSync(path.join(REPO, OUT), next);
  console.log(`wrote ${OUT}`);
}
