// The camper interior sidecar, generated from the rig and nothing else.
//
//   node tools/rig-recipes/camper-interior.mjs                 # write the Clipper interior sidecar
//   node tools/rig-recipes/camper-interior.mjs --check         # fail if the committed sidecar has drifted
//   node tools/rig-recipes/camper-interior.mjs --plates <dir>  # write preview plates (no Unity)
//
// Both rigs are evaluated VERBATIM (ADR 0021 section 5): camperIsoRig.js first, because the room
// measures against CamperIso.loft and throws without it. The rig's own validate() must come back
// empty or nothing is written. Two stamps, both LF-folded sha256: derivedFromRigSha256 (the room)
// and shellRigSha256 (the shell it measures against).
import fs from 'node:fs';
import path from 'node:path';
import { encodePng } from './lib/png.mjs';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { bytes } from './lib/rigHost.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
// sha256 with CRLF folded to LF, the repo's convention for pinning rig bytes. (rigHost's rigSha256
// takes its REPO from a URL pathname, which doubles the drive letter on a Windows checkout.)
const rigSha256 = (rel) => crypto.createHash('sha256')
  .update(Buffer.from(fs.readFileSync(path.join(REPO, rel)).toString('binary').replace(/\r\n/g, '\n'), 'binary'))
  .digest('hex');

const KIT = 'docs/art/rigs/camper-iso-kit';
const SHELL = `${KIT}/camperIsoRig.js`;
const ROOM = `${KIT}/camperInteriorRig.js`;
const VARIANT = 'clipper';
const OUT = `${KIT}/camperInteriorRig.${VARIANT}.interior.json`;

(0, eval)(fs.readFileSync(path.join(REPO, SHELL), 'utf8'));
(0, eval)(fs.readFileSync(path.join(REPO, ROOM), 'utf8'));
const CI = globalThis.CamperInterior;
if (!CI) throw new Error(`${ROOM} ran but did not install globalThis.CamperInterior`);

const problems = CI.validate({ variant: VARIANT });
if (problems.length) {
  console.error(`camperInteriorRig.js fails its own fit:\n  ${problems.join('\n  ')}`);
  process.exit(1);
}

function build() {
  const sc = CI.sidecar({ variant: VARIANT });
  const out = {};
  for (const [k, v] of Object.entries(sc)) {
    out[k] = v;
    if (k === 'variant') {
      out.derivedFromRigSha256 = rigSha256(ROOM);
      out.shellRigSha256 = rigSha256(SHELL);
    }
  }
  return JSON.stringify(out, null, 2) + '\n';
}

// ---- plates: the eight cut facings, and the plan the numbers come from ----
function contactSheet(dir) {
  const cw = CI.W, ch = CI.H, cols = 4, rows = 2, SW = cw * cols, SH = ch * rows;
  const img = new Uint8ClampedArray(SW * SH * 4);
  for (let i = 0; i < SW * SH; i++) { img[i * 4] = 46; img[i * 4 + 1] = 52; img[i * 4 + 2] = 44; img[i * 4 + 3] = 255; }
  for (let d = 0; d < 8; d++) {
    const px = bytes(CI.render(d, { variant: VARIANT }));
    const ox = (d % cols) * cw, oy = Math.floor(d / cols) * ch;
    for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
      const s = (y * cw + x) * 4; if (!px[s + 3]) continue;
      const t = ((oy + y) * SW + ox + x) * 4;
      img[t] = px[s]; img[t + 1] = px[s + 1]; img[t + 2] = px[s + 2]; img[t + 3] = 255;
    }
  }
  fs.writeFileSync(path.join(dir, 'camper-interior-facings.png'), encodePng(img, SW, SH));
}

function plan(dir) {
  const K = 96, PW = Math.round(2.4 * K) + 40, PH = Math.round(7.4 * K) + 40;
  const img = new Uint8ClampedArray(PW * PH * 4).fill(255);
  // plan: rig +y up the page, rig -x (north) to the left
  const P = (x, y) => [Math.round(PW / 2 + x * K), Math.round(PH / 2 - y * K)];
  const set = (x, y, c) => { if (x < 0 || y < 0 || x >= PW || y >= PH) return; const i = (y * PW + x) * 4; img[i] = c[0]; img[i + 1] = c[1]; img[i + 2] = c[2]; img[i + 3] = 255; };
  const fillPoly = (poly, c) => {
    const pts = poly.map((p) => P(p[0], p[1]));
    const y0 = Math.min(...pts.map((p) => p[1])), y1 = Math.max(...pts.map((p) => p[1]));
    for (let y = y0; y <= y1; y++) {
      const xs = [];
      for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
        const a = pts[i], b = pts[j];
        if ((a[1] > y) !== (b[1] > y)) xs.push(a[0] + (y - a[1]) * (b[0] - a[0]) / (b[1] - a[1]));
      }
      xs.sort((a, b) => a - b);
      for (let k = 0; k + 1 < xs.length; k += 2) for (let x = Math.ceil(xs[k]); x <= Math.floor(xs[k + 1]); x++) set(x, y, c);
    }
  };
  const disc = (x, y, r, c) => { const [px, py] = P(x, y), rr = r * K; for (let dy = -rr; dy <= rr; dy++) for (let dx = -rr; dx <= rr; dx++) if (dx * dx + dy * dy <= rr * rr) set(Math.round(px + dx), Math.round(py + dy), c); };
  const ring = (x, y, r, c) => { const [px, py] = P(x, y); for (let a = 0; a < 720; a++) { const t = a * Math.PI / 360; set(Math.round(px + Math.cos(t) * r * K), Math.round(py + Math.sin(t) * r * K), c); } };
  const wk = CI.walkable({ variant: VARIANT });
  fillPoly(wk.saloon.polygon, [214, 196, 160]);
  const tone = { berth: [92, 116, 160], room: [150, 156, 160], counter: [120, 90, 60], tall: [90, 70, 50], chest: [235, 235, 228], seat: [160, 150, 110] };
  for (const o of wk.saloon.obstructions) if (o.kind !== 'overhead') fillPoly(o.footprint, tone[o.kind] || [80, 80, 80]);
  fillPoly(wk.head.polygon, [190, 205, 210]);
  for (const o of wk.head.obstructions) fillPoly(o.footprint, [250, 250, 250]);
  const rr = wk.saloon.obstructions.find((o) => o.id === 'rod_rack');
  for (const c of rr.footprint) disc(c[0], c[1], 0.03, [200, 40, 40]);
  for (const a of CI.interactables({ variant: VARIANT })) disc(a.reach_point[0], a.reach_point[1], 0.04, a.level === 'head' ? [20, 120, 200] : [200, 30, 30]);
  ring(0.56, 0.08, CI.WALKER.footRadius, [0, 140, 0]);
  fs.writeFileSync(path.join(dir, 'camper-interior-plan.png'), encodePng(img, PW, PH));
}

const args = process.argv.slice(2);
const text = build();
const outPath = path.join(REPO, OUT);
if (args.includes('--check')) {
  const have = fs.existsSync(outPath) ? fs.readFileSync(outPath, 'utf8').replace(/\r\n/g, '\n') : '';
  if (have !== text) {
    console.error(`DRIFT: ${OUT} is not what camperInteriorRig.js says. Re-run without --check.`);
    process.exit(1);
  }
  console.log(`ok: ${OUT} matches the rig`);
} else {
  fs.writeFileSync(outPath, text);
  console.log(`wrote ${OUT}`);
}
const pi = args.indexOf('--plates');
if (pi >= 0) {
  const dir = args[pi + 1];
  if (!dir) throw new Error('--plates needs a directory');
  fs.mkdirSync(dir, { recursive: true });
  contactSheet(dir);
  plan(dir);
  console.log(`plates in ${dir}`);
}
