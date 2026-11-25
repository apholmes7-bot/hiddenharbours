// The small shed's sidecar, generated from the rig and nothing else.
//
//   node tools/rig-recipes/small-shed.mjs                 # write the lot shed's sidecar
//   node tools/rig-recipes/small-shed.mjs --check         # fail if the committed sidecar has drifted
//   node tools/rig-recipes/small-shed.mjs --plates <dir>  # write preview plates (no Unity)
//
// The rig is evaluated VERBATIM (ADR 0021 section 5). Its own validate() must come back empty or
// nothing is written. One stamp, LF-folded sha256: derivedFromRigSha256.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';
import { bytes } from './lib/rigHost.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
// sha256 with CRLF folded to LF, the repo's convention for pinning rig bytes (see camper-interior.mjs
// for why this does not use rigHost's rigSha256 on Windows).
const rigSha256 = (rel) => crypto.createHash('sha256')
  .update(Buffer.from(fs.readFileSync(path.join(REPO, rel)).toString('binary').replace(/\r\n/g, '\n'), 'binary'))
  .digest('hex');

const KIT = 'docs/art/rigs/small-shed-kit';
const RIG = `${KIT}/smallShedRig.js`;
const VARIANT = 'lot_shed';
const OUT = `${KIT}/smallShedRig.${VARIANT}.gameplay.json`;

(0, eval)(fs.readFileSync(path.join(REPO, RIG), 'utf8'));
const SH = globalThis.SmallShed;
if (!SH) throw new Error(`${RIG} ran but did not install globalThis.SmallShed`);

const problems = SH.validate({ variant: VARIANT });
if (problems.length) {
  console.error(`smallShedRig.js fails its own fit:\n  ${problems.join('\n  ')}`);
  process.exit(1);
}

function build() {
  const sc = SH.sidecar({ variant: VARIANT });
  const out = {};
  for (const [k, v] of Object.entries(sc)) {
    out[k] = v;
    if (k === 'variant') out.derivedFromRigSha256 = rigSha256(RIG);
  }
  return JSON.stringify(out, null, 2) + '\n';
}

// ---- plates: eight closed facings over eight cut facings, and the plan the numbers come from ----
function contactSheet(dir) {
  const cw = SH.W, ch = SH.H, cols = 8, rows = 3, SWd = cw * cols, SHt = ch * rows;
  const img = new Uint8ClampedArray(SWd * SHt * 4);
  for (let i = 0; i < SWd * SHt; i++) { img[i * 4] = 46; img[i * 4 + 1] = 52; img[i * 4 + 2] = 44; img[i * 4 + 3] = 255; }
  const rowsOpts = [{ variant: VARIANT }, { variant: VARIANT, door: 'open' }, { variant: VARIANT, cut: true, door: 'open' }];
  rowsOpts.forEach((o, row) => {
    for (let d = 0; d < 8; d++) {
      const px = bytes(SH.render(d, o));
      const ox = d * cw, oy = row * ch;
      for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
        const s = (y * cw + x) * 4; if (!px[s + 3]) continue;
        const t = ((oy + y) * SWd + ox + x) * 4;
        img[t] = px[s]; img[t + 1] = px[s + 1]; img[t + 2] = px[s + 2]; img[t + 3] = 255;
      }
      // the stand spot and the reach points, as red pixels, on the cut row
      if (row === 2) {
        const an = SH.anchors(d, o);
        for (const k of Object.keys(an)) {
          const p = an[k].reach, X = Math.round(p.x), Y = Math.round(p.y);
          for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
            const xx = X + dx, yy = Y + dy; if (xx < 0 || yy < 0 || xx >= cw || yy >= ch) continue;
            const t = ((oy + yy) * SWd + ox + xx) * 4; img[t] = k === 'stand' ? 20 : 220; img[t + 1] = k === 'stand' ? 160 : 30; img[t + 2] = 30;
          }
        }
      }
    }
  });
  fs.writeFileSync(path.join(dir, 'small-shed-facings.png'), encodePng(img, SWd, SHt));
}

function plan(dir) {
  const K = 120, PW = Math.round(3.4 * K), PH = Math.round(3.6 * K);
  const img = new Uint8ClampedArray(PW * PH * 4).fill(255);
  // plan: rig +y (north) up the page, +x (east) to the right, centred a little north of the page middle
  const P = (x, y) => [Math.round(PW / 2 + x * K), Math.round(PH * 0.42 - y * K)];
  const set = (x, y, c) => { if (x < 0 || y < 0 || x >= PW || y >= PH) return; const i = (y * PW + x) * 4; img[i] = c[0]; img[i + 1] = c[1]; img[i + 2] = c[2]; img[i + 3] = 255; };
  const fillRect = (b, c) => { const [x0, y1] = P(b.x0, b.y0), [x1, y0] = P(b.x1, b.y1); for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) set(x, y, c); };
  const outline = (b, c) => { const [x0, y1] = P(b.x0, b.y0), [x1, y0] = P(b.x1, b.y1); for (let x = x0; x <= x1; x++) { set(x, y0, c); set(x, y1, c); } for (let y = y0; y <= y1; y++) { set(x0, y, c); set(x1, y, c); } };
  const disc = (x, y, r, c) => { const [px, py] = P(x, y), rr = r * K; for (let dy = -rr; dy <= rr; dy++) for (let dx = -rr; dx <= rr; dx++) if (dx * dx + dy * dy <= rr * rr) set(Math.round(px + dx), Math.round(py + dy), c); };
  const ring = (x, y, r, c) => { const [px, py] = P(x, y); for (let a = 0; a < 1440; a++) { const t = a * Math.PI / 720; set(Math.round(px + Math.cos(t) * r * K), Math.round(py + Math.sin(t) * r * K), c); } };
  const B = SH.BODY, XO = B.len / 2, YO = B.dep / 2, pl = SH.planExtent();
  fillRect({ x0: -XO, x1: XO, y0: -YO, y1: YO }, [150, 50, 36]);
  const fl = SH.walkable({ variant: VARIANT }).floor.polygon;
  fillRect({ x0: fl[0][0], x1: fl[1][0], y0: fl[0][1], y1: fl[2][1] }, [214, 196, 160]);
  fillRect({ x0: B.door.u0, x1: B.door.u1, y0: -YO, y1: fl[0][1] }, [214, 196, 160]);
  fillRect(B.step, [130, 132, 138]);
  for (const f of SH.fixtures({ variant: VARIANT })) fillRect(f, f.kind === 'rack' ? [90, 70, 50] : [150, 120, 80]);
  const sl = SH.slots({ variant: VARIANT });
  for (const r of sl.rods) disc(r.foot[0], r.foot[1], 0.02, [30, 30, 30]);
  for (const g of sl.gear.filter((q) => q.shelf === 0)) disc(g.centre[0], g.centre[1], 0.03, [60, 60, 160]);
  outline(pl, [120, 120, 120]);
  ring(0, 0, SH.RESERVE.radius, [0, 0, 200]);
  for (const a of SH.interactables({ variant: VARIANT })) disc(a.reach_point[0], a.reach_point[1], 0.03, [200, 30, 30]);
  ring(SH.SPOTS.insideLanding[0], SH.SPOTS.insideLanding[1], SH.WALKER.footRadius, [0, 140, 0]);
  ring(SH.SPOTS.outsideLanding[0], SH.SPOTS.outsideLanding[1], SH.WALKER.footRadius, [0, 140, 0]);
  ring(SH.SPOTS.stand[0], SH.SPOTS.stand[1], SH.WALKER.footRadius, [0, 100, 160]);
  const lo = SH.leafPlan(180).quad, ls = SH.leafPlan(0).quad;
  for (const q of [lo, ls]) fillRect({ x0: Math.min(...q.map((p) => p[0])), x1: Math.max(...q.map((p) => p[0])), y0: Math.min(...q.map((p) => p[1])), y1: Math.max(...q.map((p) => p[1])) }, [100, 100, 96]);
  fs.writeFileSync(path.join(dir, 'small-shed-plan.png'), encodePng(img, PW, PH));
}

const args = process.argv.slice(2);
const text = build();
const outPath = path.join(REPO, OUT);
if (args.includes('--check')) {
  const have = fs.existsSync(outPath) ? fs.readFileSync(outPath, 'utf8').replace(/\r\n/g, '\n') : '';
  if (have !== text) {
    console.error(`DRIFT: ${OUT} is not what smallShedRig.js says. Re-run without --check.`);
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
