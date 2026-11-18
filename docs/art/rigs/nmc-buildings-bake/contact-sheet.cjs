'use strict';
/* contact-sheet.cjs: the owner's look at the two Nine Mile Creek building bakes, beside a building already in the game.
     node docs/art/rigs/nmc-buildings-bake/contact-sheet.cjs <outDir> [coldStorePng]
   Renders the fish plant and the community hall (roof on, roof hidden) and two massing proposals for the wholesalers
   (review props registered for this run only: NOT kit pieces, NOT assets), writes them as PNGs with an HTML page that lays
   them out at 1x and 2x (image-rendering: pixelated), and screenshots the page with headless Chrome (H9_BROWSER can name
   another Chromium). coldStorePng is the wharf cold store's baked sheet (NmcWharf_cold_store.png, cell 4: its door to the
   camera), the scale and style neighbour; the box holds it as an LFS pointer, so pass the real file. Day sky only. */
const fs = require('fs'), path = require('path'), zlib = require('zlib'), { spawnSync } = require('child_process');
const { load, png, BUILDINGS } = require('./bake.cjs');
const OUT = path.resolve(process.argv[2] || 'Evidence~/nmc-buildings');
const COLD = process.argv[3] || null;
fs.mkdirSync(OUT, { recursive: true });
const ctx = load(), NF = ctx.NmcFishPlant, H = NF.helpers, sky = ctx.WeatherSky.at({ time: 14, cloud: 0.1, wind: 0.3 });

// ---- a 1.8 m figure for scale (the plant kit's review figure)
NF.PIECES.sheetFigure = { defaults: {}, build() { const out = []; H.box(out, -0.22, 0.22, -0.14, 0.14, 0, 0.85, 'navy', 'legs'); H.box(out, -0.25, 0.25, -0.15, 0.15, 0.85, 1.5, 'yellow', 'oilskin'); H.box(out, -0.12, 0.12, -0.12, 0.12, 1.5, 1.8, 'board', 'head');
  return { faces: out, mats: H.allMats(out), anchors: {} }; } };

// ---- the proposals: massing only, in the plant kit's frame (dir 4: local +y is the south face, local +x is west)
function gableX(out, x0, x1, y0, y1, zw, rise, wall, wtex, roof, trim, tag) {   // ridge along x, gables east and west, slopes facing the camera and away
  const yc = (y0 + y1) / 2, zr = zw + rise, ov = 0.4, sl = rise / ((y1 - y0) / 2), ez = zw - ov * sl;
  H.FTz(out, [[x1, y1, 0], [x0, y1, 0], [x0, y1, zw], [x1, y1, zw]], wall, 0.1, tag + '.wall', wtex); H.FTz(out, [[x0, y0, 0], [x1, y0, 0], [x1, y0, zw], [x0, y0, zw]], wall, -0.12, tag + '.wall', wtex);
  H.FTz(out, [[x0, y1, 0], [x0, y0, 0], [x0, y0, zw], [x0, y1, zw]], wall, 0.02, tag + '.wall', wtex); H.FTz(out, [[x1, y0, 0], [x1, y1, 0], [x1, y1, zw], [x1, y0, zw]], wall, -0.2, tag + '.wall', wtex);
  H.FTz(out, [[x0, y1, zw], [x0, y0, zw], [x0, yc, zr]], wall, 0.02, tag + '.gable', wtex); H.FTz(out, [[x1, y0, zw], [x1, y1, zw], [x1, yc, zr]], wall, -0.2, tag + '.gable', wtex);
  H.FT(out, [[x1 + ov, y1 + ov, ez], [x0 - ov, y1 + ov, ez], [x0 - ov, yc, zr], [x1 + ov, yc, zr]], roof, 0.12, tag + '.roof', 'seam');
  H.FT(out, [[x0 - ov, y0 - ov, ez], [x1 + ov, y0 - ov, ez], [x1 + ov, yc, zr], [x0 - ov, yc, zr]], roof, -0.05, tag + '.roof', 'seam');
  H.panelY(out, y1 + ov, 1, x0 - ov, x1 + ov, ez - 0.18, ez, trim, 0.1, tag + '.fascia');
  return { ez, zr };
}
// A. the marine electronics and boat-gear wholesaler: a Water Street storefront, 9 m frontage x 15 m deep, two storeys behind a false front
NF.PIECES.propStorefront = { defaults: {}, build() {
  const out = [], X0 = -4.5, X1 = 4.5, Y0 = -7.5, Y1 = 7.5, ZW = 6.4, ZP = 7.6;
  H.boxT(out, X0, X1, Y0, Y1, 0, ZW, 'board', 'body', 'boards', 'boards');
  H.flat(out, X0 + 0.2, X1 - 0.2, Y0 + 0.2, Y1 - 0.4, ZW + 0.02, 'tyre', 0, 'roof.flat');
  H.FT(out, [[X1, Y1 + 0.05, 0], [X0, Y1 + 0.05, 0], [X0, Y1 + 0.05, ZP], [X1, Y1 + 0.05, ZP]], 'board', 0.1, 'false.front', 'boards');
  H.box(out, X0 - 0.15, X1 + 0.15, Y1, Y1 + 0.3, ZP, ZP + 0.2, 'navy', 'cornice');
  for (const x of [X0, X1]) H.box(out, x - 0.12, x + 0.12, Y1, Y1 + 0.2, 0, ZP, 'navy', 'pilaster');
  H.box(out, X0 + 0.4, X1 - 0.4, Y1 + 0.06, Y1 + 0.12, 3.35, 4.15, 'navy', 'sign.frame'); H.panelY(out, Y1 + 0.13, 1, X0 + 0.55, X1 - 0.55, 3.45, 4.05, 'board', 0.25, 'words.proposal_storefront');
  for (const [a, b] of [[X0 + 0.5, -1.0], [1.0, X1 - 0.5]]) { H.panelY(out, Y1 + 0.08, 1, a, b, 0.55, 2.8, 'glass', 0.25, 'display', null, 'win'); H.box(out, a, b, Y1, Y1 + 0.25, 0, 0.55, 'navy', 'stallriser'); }
  H.panelY(out, Y1 - 0.6, 1, -0.6, 0.6, 0, 2.5, 'navy', 0.1, 'door'); H.panelY(out, Y1 - 0.58, 1, -0.4, 0.4, 1.2, 2.2, 'glass', 0.2, 'door.glass', null, 'win');
  H.FT(out, [[X1 - 0.3, Y1 + 1.4, 2.9], [X0 + 0.3, Y1 + 1.4, 2.9], [X0 + 0.3, Y1 + 0.1, 3.3], [X1 - 0.3, Y1 + 0.1, 3.3]], 'navy', 0.12, 'awning', 'slats');
  for (const x of [-3, 0, 3]) H.panelY(out, Y1 + 0.08, 1, x - 0.55, x + 0.55, 4.5, 5.9, 'glass', 0.25, 'upper', null, 'win');
  // the trade's signifiers on the roof: a radar scanner on a short mast and two whip antennas (a demonstration rig)
  H.stick(out, [2.5, -3, ZW], [2.5, -3, ZW + 1.4], 0.12, 'galv', 'mast'); H.box(out, 1.4, 3.6, -3.12, -2.88, ZW + 1.4, ZW + 1.55, 'siding', 'radar');
  for (const x of [-2.5, -1.6]) H.stick(out, [x, -5, ZW], [x, -5, ZW + 2.6], 0.04, 'siding', 'whip');
  return { faces: out, mats: H.allMats(out), anchors: {} }; } };
// B. the fishing-gear wholesaler: a truck-served warehouse beside the lumber shed, 26 x 16 m, two roller doors and a dock, gear stacked out front
NF.PIECES.propWarehouse = { defaults: {}, build() {
  const out = [], X0 = -13, X1 = 13, Y0 = -8, Y1 = 8;
  H.box(out, X0 - 0.05, X1 + 0.05, Y0 - 0.05, Y1 + 0.05, -0.2, 0.15, 'conc', 'slab', { noTop: true });
  gableX(out, X0, X1, Y0, Y1, 5.5, 3.0, 'red', 'sheet', 'galv', 'trim', 'shed');
  for (const xc of [-7.5, -2.5]) { H.panelY(out, Y1 + 0.03, 1, xc - 2, xc + 2, 0.02, 4.2, 'galv', 0.1, 'roller', 'slats'); H.box(out, xc - 2.15, xc + 2.15, Y1, Y1 + 0.12, 4.2, 4.35, 'yellow', 'roller.head'); }
  H.box(out, 1.5, 12.2, Y1, Y1 + 2.4, 0, 1.2, 'conc', 'dock'); H.panelY(out, Y1 + 0.03, 1, 6.2, 7.3, 1.2, 3.4, 'navy', 0.1, 'man.door');
  H.box(out, 1.5, 12.2, Y1 + 2.3, Y1 + 2.4, 1.2, 2.2, 'galv', 'dock.rail');
  H.box(out, 2.5, 10.5, Y1 + 0.02, Y1 + 0.08, 4.4, 5.2, 'navy', 'sign.frame'); H.panelY(out, Y1 + 0.09, 1, 2.65, 10.35, 4.5, 5.1, 'board', 0.25, 'words.proposal_warehouse');
  // gear out front: lobster traps stacked three high, rope coils on pallets, a tote of floats
  for (let i = 0; i < 4; i++) for (let k = 0; k < 3; k++) H.box(out, -12.6 + i * 1.0, -11.7 + i * 1.0, Y1 + 0.6, Y1 + 1.15, k * 0.42, k * 0.42 + 0.4, k % 2 ? 'yellow' : 'tote', 'trap');
  for (const x of [3.2, 5.0, 9.6]) { H.box(out, x - 0.6, x + 0.6, Y1 + 0.6, Y1 + 1.8, 1.2, 1.35, 'wood', 'pallet'); H.cyl(out, x, Y1 + 1.2, 1.35, 1.9, 0.5, 0.5, 12, 'orange', 'rope.coil'); }
  return { faces: out, mats: H.allMats(out), anchors: {} }; } };

// ---- render everything
const R = {};
const save = (name, r) => { fs.writeFileSync(path.join(OUT, name + '.png'), png(r.w, r.h, r.rgba)); R[name] = { w: r.w, h: r.h, px: r.px, py: r.py }; };
for (const b of BUILDINGS) { save(b.name + '_exterior', NF.render(b.key, 4, Object.assign({ sky, lit: 0 }, b.exterior))); save(b.name + '_cutaway', NF.render(b.key, 4, Object.assign({ sky, lit: 0 }, b.cutaway))); }
save('figure', NF.render('sheetFigure', 4, { sky }));
save('proposal_storefront', NF.render('propStorefront', 4, { sky }));
save('proposal_warehouse', NF.render('propWarehouse', 4, { sky }));

// ---- the neighbour: the cold store's cell 4, cut out of its sheet (decoded here so the page needs nothing but files)
function unpng(buf) { let p = 8, w, h, bpp = 4, idat = []; while (p < buf.length) { const len = buf.readUInt32BE(p), type = buf.toString('ascii', p + 4, p + 8), data = buf.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); if (data[8] !== 8 || (data[9] !== 6 && data[9] !== 2)) throw new Error('only 8-bit RGB(A)'); bpp = data[9] === 6 ? 4 : 3; } else if (type === 'IDAT') idat.push(data); p += 12 + len; }
  const raw = zlib.inflateSync(Buffer.concat(idat)), bpr = w * bpp, out = Buffer.alloc(w * h * bpp);
  for (let y = 0; y < h; y++) { const f = raw[y * (bpr + 1)], line = raw.subarray(y * (bpr + 1) + 1, (y + 1) * (bpr + 1));
    for (let x = 0; x < bpr; x++) { const a = x >= bpp ? out[y * bpr + x - bpp] : 0, b = y ? out[(y - 1) * bpr + x] : 0, c = x >= bpp && y ? out[(y - 1) * bpr + x - bpp] : 0; let v = line[x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1; else if (f === 4) { const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      out[y * bpr + x] = v & 255; } }
  return { w, h, bpp, d: out }; }
let coldNote = 'not drawn: no cold store PNG was passed';
if (COLD && fs.existsSync(COLD)) {
  const s = unpng(fs.readFileSync(COLD)), cw = 714, ch = 629, cx = 4 * cw, cy = 0, d = new Uint8Array(cw * ch * 4);
  for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) for (let k = 0; k < 4; k++) d[(y * cw + x) * 4 + k] = s.d[((y + cy) * s.w + x + cx) * 4 + k];
  save('neighbour_cold_store', { w: cw, h: ch, px: 357, py: 399, rgba: d }); coldNote = 'the wharf cold store, structure.nmc_cold_store, 9.5 x 15.4 m, as baked in the game today (cell 4)';
}

// ---- the page: every image stands on one ground line per row (its pivot row), at 1x and 2x
const H1 = (t) => `<h2>${t}</h2>`;
function row(items, scale, note) {   // items: [name, label]
  const present = items.filter(([n]) => R[n]), below = Math.max(...present.map(([n]) => R[n].h - R[n].py)), above = Math.max(...present.map(([n]) => R[n].py));
  const cells = present.map(([n, label]) => { const r = R[n]; return `<figure style="margin-top:${(above - r.py) * scale}px;margin-bottom:${(below - (r.h - r.py)) * scale}px"><img src="${n}.png" width="${r.w * scale}" height="${r.h * scale}"><figcaption>${label} <i>${r.w}x${r.h} px</i></figcaption></figure>`; }).join('');
  return `<div class="row">${cells}</div>${note ? `<p class="note">${note}</p>` : ''}`;
}
const FP = 'NmcFishPlant', CH = 'NmcCommunityHall';
const html = `<!doctype html><html><head><meta charset="utf-8"><style>
body{margin:0;padding:24px 28px;background:#6a7a4e;font:15px/1.35 Georgia,serif;color:#f4f1e6}
h1{font-size:26px;margin:0 0 4px} h2{font-size:20px;margin:26px 0 8px;border-top:2px solid #f4f1e6;padding-top:10px}
.row{display:flex;align-items:flex-start;gap:36px;flex-wrap:nowrap} figure{margin:0} figcaption{font-size:13px;margin-top:4px;max-width:900px} i{opacity:.7}
img{image-rendering:pixelated;display:block} .note{font-size:14px;max-width:1700px;background:rgba(0,0,0,.18);padding:8px 10px;margin:8px 0}
.weak{background:rgba(70,20,10,.35)}
</style></head><body>
<h1>Nine Mile Creek: the fish plant and the community hall, exterior art (draft)</h1>
<div>Day palette (the bake sky every Nine Mile Creek sheet uses: 14:00, light cloud). 32 px = 1 m, the game's camera. "Roof hidden" is the sprite shown while the player is inside (walls cut at 1.1 m). Neighbour for scale and style: ${coldNote}. The figure is 1.8 m.</div>
${H1('1. The fish plant, 48 x 28 m, at 1x (roof on, roof hidden)')}
${row([['neighbour_cold_store', 'Existing: wharf cold store'], ['figure', '1.8 m'], [FP + '_exterior', 'Fish plant, roof on']], 1)}${row([[FP + '_cutaway', 'Fish plant, roof hidden']], 1)}
${H1('2. The fish plant at 2x')}
${row([['neighbour_cold_store', 'Existing: wharf cold store, 2x'], ['figure', '1.8 m, 2x']], 2)}${row([[FP + '_exterior', 'Fish plant, roof on, 2x']], 2)}
${row([[FP + '_cutaway', 'Fish plant, roof hidden, 2x']], 2, 'Public door: the half-glazed door under the little red porch roof on the south wall (right of centre), with the blank name board standing on the eave above it. The landing side is the east gable (right): two intake roller doors and the outbound door under the steel canopy, the ice plant penthouse and its chute on the roof over the east end, two retort stacks, condensers at the far west.')}
${H1('3. The community hall with the gym, 18 x 12 m, at 1x and 2x (roof on, roof hidden)')}
${row([['neighbour_cold_store', 'Existing: wharf cold store'], ['figure', '1.8 m'], [CH + '_exterior', 'Community hall, roof on'], [CH + '_cutaway', 'Community hall, roof hidden']], 1)}
${row([['neighbour_cold_store', 'Existing: wharf cold store, 2x'], [CH + '_exterior', 'Community hall, roof on, 2x'], [CH + '_cutaway', 'Community hall, roof hidden, 2x']], 2, 'Door: west wall (left), 0.5 m north of centre, square on to the lot\u2019s west gate: the gabled entry hood on two posts, the stoop and two steps, the door lamp and a notice board. Roof hidden: front hall (maple, stage at the south end), gym (dark rubber, north-east, doorway in the back wall), kitchen and washroom (closed for now).')}
${H1('4. Proposals only (no assets): the two wholesalers the owner ruled on 11-16')}
${row([['neighbour_cold_store', 'Existing: wharf cold store'], ['figure', '1.8 m'], ['proposal_storefront', 'A. Marine electronics and boat-gear wholesaler, Water Street storefront, 9 x 15 m, two storeys behind a false front'], ['proposal_warehouse', 'B. Fishing-gear wholesaler, warehouse by the lumber shed, 26 x 16 m']], 1,
 'A: tan clapboard, navy trim, two display windows either side of a recessed door, a canvas awning, a blank sign band, and on the roof a radar scanner and two whip antennas so the trade reads from the street. B: barn-red corrugated steel under a galvanised roof, two 4 m roller doors and a dock with a man door for truck pick-ups, traps, rope coils and pallets out front, a blank board. Massing and materials to agree on before a kit is drawn; neither is placed or baked.')}
${H1('What is weak (honest)')}
<p class="note weak">1. The fish plant is the art-director kit's model (#1203) baked as drawn: it reads more 1970s steel shed than 1950s clapboard, and from this camera the roof is most of the picture; the south face is only the low shingled lean-to. 2. At this camera an east or west wall is seen edge on, so the plant's intake doors and the hall's front door are not visible themselves; they read only from what sticks out (the plant's canopy and chute, the hall's entry hood and steps). 3. Both roof-hidden sprites are floor plans, not furnished rooms: the plant's has no machines (the shell's greybox blocks stay), and its south doors are still drawn shut in the cut. 4. The hall's roof is a big plain asphalt field; it may want a second ornament or a colour (green or red shingle) once seen in the lot. 5. Walls cut at 1.1 m sit on the floor's sorting order, so a partition never draws over the player's legs. 6. Not seen in the editor yet: sorting, the shadow and the inside swap are unverified until a warm box look.</p>
</body></html>`;
fs.writeFileSync(path.join(OUT, 'contact-sheet.html'), html);
const wpx = 3500, hpx = 11000;
const exe = process.env.H9_BROWSER || path.join(process.env.PROGRAMFILES || 'C:/Program Files', 'Google/Chrome/Application/chrome.exe');
const res = spawnSync(exe, ['--headless', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--user-data-dir=' + path.join(OUT, '.browser-profile'), '--window-size=' + wpx + ',' + hpx,
  '--screenshot=' + path.join(OUT, 'contact-sheet.png'), 'file:///' + path.join(OUT, 'contact-sheet.html').replace(/\\/g, '/')], { stdio: 'inherit', timeout: 120000 });
fs.rmSync(path.join(OUT, '.browser-profile'), { recursive: true, force: true });
// trim the empty page below the last row (the window is sized generously)
{ const sp = path.join(OUT, 'contact-sheet.png'), s = unpng(fs.readFileSync(sp)), B = s.bpp, bg = [s.d[0], s.d[1], s.d[2]]; let last = 0;
  for (let y = 0; y < s.h; y++) { const o = y * s.w * B; for (let x = 0; x < s.w; x++) { const i = o + x * B; if (Math.abs(s.d[i] - bg[0]) + Math.abs(s.d[i + 1] - bg[1]) + Math.abs(s.d[i + 2] - bg[2]) > 6) { last = y; break; } } }
  const hh = Math.min(s.h, last + 30), d = new Uint8Array(s.w * hh * 4); for (let y = 0; y < hh; y++) for (let x = 0; x < s.w; x++) { const i = (y * s.w + x) * B, j = (y * s.w + x) * 4; d[j] = s.d[i]; d[j + 1] = s.d[i + 1]; d[j + 2] = s.d[i + 2]; d[j + 3] = 255; }
  fs.writeFileSync(sp, png(s.w, hh, d)); console.log('trimmed to ' + s.w + 'x' + hh); }
console.log('SHEET ' + path.join(OUT, 'contact-sheet.png') + ' (chrome exit ' + res.status + ')');
