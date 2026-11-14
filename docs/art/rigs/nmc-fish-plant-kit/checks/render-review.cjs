'use strict';
/* render-review.cjs: the fish plant kit's review boards. Node 18+, built-ins only (fs, path, vm, zlib). No Unity.
     node docs/art/rigs/nmc-fish-plant-kit/checks/render-review.cjs [outDir]
   Renders the kit through its own rasteriser and writes PNGs to Evidence~/nmc-fish-plant (untracked; never commit them):
   the site board (every piece at its site A place, with a massing truck, 1.8 m figures and the set pieces kit's chandlery
   as known neighbours), native-size crops (32 px = 1 m, the bake's size), the plant after dusk, the cutaway, and a sheet
   of the small pieces at all eight facings. The massing truck and the figures are review props; they are not kit pieces. */
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib');
const KIT = path.resolve(__dirname, '..'), ROOT = path.resolve(KIT, '../../../..'), LIB = path.resolve(KIT, '../nmc-set-pieces-kit');
const OUT = path.resolve(process.argv[2] || path.join(ROOT, 'Evidence~/nmc-fish-plant'));
const ctx = vm.createContext({ console });
for (const f of [path.join(LIB, 'lib/weatherSky.js'), path.join(LIB, 'lib/coastalPass.js'), path.join(LIB, 'nmcSetPieces.js'), path.join(KIT, 'nmcFishPlant.js')]) vm.runInContext(fs.readFileSync(f, 'utf8'), ctx, { filename: path.basename(f) });
const NF = ctx.NmcFishPlant, SP = ctx.NmcSetPieces, WS = ctx.WeatherSky, H = NF.helpers;
const S = NF.PPU, SE = Math.sin(40 * Math.PI / 180);

// ---- review props, registered for this run only (NF.KEYS does not list them)
NF.PIECES.reviewTruck = { defaults: {}, build() { const out = [];   // the court study's 8.0 x 2.5 m planning truck, pivot on its rear axle, nose local -x
    H.box(out, -5.9, 2.0, -0.45, 0.45, 0.5, 0.75, 'cast', 'chassis'); H.box(out, -3.95, 2.0, -1.2, 1.2, 0.95, 3.1, 'siding', 'body'); H.box(out, -4.5, -3.95, -0.8, 0.8, 2.3, 3.0, 'alum', 'reefer');
    H.box(out, -6.0, -4.2, -1.1, 1.1, 0.55, 2.55, 'navy', 'cab'); H.panelX(out, -6.01, -1, -0.95, 0.95, 1.6, 2.35, 'glass', 0, 'screen');
    for (const x of [0, -4.47]) for (const y of [-1.25, 1.0]) H.box(out, x - 0.45, x + 0.45, y, y + 0.25, 0, 0.9, 'tyre', 'wheel');
    return { faces: out, mats: H.allMats(out), anchors: {} }; } };
NF.PIECES.reviewFigure = { defaults: {}, build() { const out = []; H.box(out, -0.22, 0.22, -0.14, 0.14, 0, 0.85, 'navy', 'legs'); H.box(out, -0.25, 0.25, -0.15, 0.15, 0.85, 1.5, 'yellow', 'oilskin'); H.box(out, -0.12, 0.12, -0.12, 0.12, 1.5, 1.8, 'board', 'head');
    return { faces: out, mats: H.allMats(out), anchors: {} }; } };

const crcT = (() => { const t = new Int32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c; } return t; })();
const crc = (b) => { let c = -1; for (let i = 0; i < b.length; i++) c = crcT[(c ^ b[i]) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; };
const chunk = (type, data) => { const len = Buffer.alloc(4); len.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]), c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([len, td, c]); };
function png(w, h, rgba) { const src = Buffer.from(rgba.buffer, rgba.byteOffset, rgba.byteLength), raw = Buffer.alloc((w * 4 + 1) * h); for (let y = 0; y < h; y++) src.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
  const head = Buffer.alloc(13); head.writeUInt32BE(w); head.writeUInt32BE(h, 4); head[8] = 8; head[9] = 6; return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', head), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]); }
function canvas(w, h, rgb) { const c = { w, h, d: new Uint8Array(w * h * 4) }; for (let i = 0; i < w * h; i++) { c.d[i * 4] = rgb[0]; c.d[i * 4 + 1] = rgb[1]; c.d[i * 4 + 2] = rgb[2]; c.d[i * 4 + 3] = 255; } return c; }
function rect(c, x0, y0, x1, y1, rgb) { for (let y = Math.max(0, y0 | 0); y < Math.min(c.h, y1 | 0); y++) for (let x = Math.max(0, x0 | 0); x < Math.min(c.w, x1 | 0); x++) { const i = (y * c.w + x) * 4; c.d[i] = rgb[0]; c.d[i + 1] = rgb[1]; c.d[i + 2] = rgb[2]; } }
function blit(c, r, x, y) { x = Math.round(x) - r.px; y = Math.round(y) - r.py; for (let j = 0; j < r.h; j++) { const yy = y + j; if (yy < 0 || yy >= c.h) continue; for (let i = 0; i < r.w; i++) { const xx = x + i; if (xx < 0 || xx >= c.w) continue; const s = (j * r.w + i) * 4, a = r.rgba[s + 3]; if (!a) continue; const d = (yy * c.w + xx) * 4, k = a / 255; c.d[d] = r.rgba[s] * k + c.d[d] * (1 - k); c.d[d + 1] = r.rgba[s + 1] * k + c.d[d + 1] * (1 - k); c.d[d + 2] = r.rgba[s + 2] * k + c.d[d + 2] * (1 - k); } } }
function half(c) { const w = c.w >> 1, h = c.h >> 1, o = { w, h, d: new Uint8Array(w * h * 4) }; for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) for (let k = 0; k < 4; k++) { const i = (y * 2 * c.w + x * 2) * 4 + k; o.d[(y * w + x) * 4 + k] = (c.d[i] + c.d[i + 4] + c.d[i + c.w * 4] + c.d[i + c.w * 4 + 4]) >> 2; } return o; }
function crop(c, x0, y0, w, h) { const o = { w, h, d: new Uint8Array(w * h * 4) }; for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) for (let k = 0; k < 4; k++) o.d[(y * w + x) * 4 + k] = c.d[((y + y0) * c.w + x + x0) * 4 + k]; return o; }
const save = (name, c) => { fs.mkdirSync(OUT, { recursive: true }); const f = path.join(OUT, name); fs.writeFileSync(f, png(c.w, c.h, c.d)); console.log('WROTE ' + f + '  ' + c.w + 'x' + c.h); };
const shade = (rgb, k) => rgb.map(v => Math.max(0, Math.min(255, Math.round(v * k))));

// ---- the site board: brief (u, v) to canvas pixels. North is up; a ground metre north is S * sin 40 pixels.
const U0 = -58, U1 = 56, V0 = -34, V1 = 34, TOP = 150;
const sx = (u) => (u - U0) * S, sy = (v) => TOP + (V1 - v) * S * SE;
function board(tag, sky, lit, o) { o = o || {}; const night = lit > 0, dim = night ? 0.34 : 1;
  const c = canvas(Math.ceil((U1 - U0) * S), Math.ceil(TOP + (V1 - V0) * S * SE) + 20, shade([104, 122, 78], dim));       // the farm's grass
  const ground = (u0, u1, v0, v1, rgb) => rect(c, sx(u0), sy(v1), sx(u1), sy(v0), shade(rgb, dim));
  ground(0, 26, -30, 30, [78, 80, 80]); ground(26, 51, -3.4, 3.4, [78, 80, 80]); ground(47, 54, -34, 34, [70, 72, 73]);     // the court, the spur, Route 91 (schematic)
  ground(-46, -22, 16, 27, [112, 108, 98]); ground(0, 26, 26, 28, [126, 122, 108]); ground(-53, -49, -18, 28, [126, 122, 108]); ground(-53, 0, -18, -16, [126, 122, 108]);   // parking, the walking strips
  const items = [];
  const put = (kit, key, u, v, dir, opts, front) => items.push({ kit, key, u, v, dir: dir == null ? 4 : dir, opts: opts || {}, front: front == null ? v : front, floor: kit.PIECES[key].layer === 'floor' });
  put(NF, 'plantDock', 0, 0, 4, {}, 99);
  put(NF, 'fishPlant', -24, 0, 4, { doors: o.doors || 'closed', cutaway: !!o.cutaway }, -14);
  put(NF, 'plantGate', 26, 0, 4, { open: o.gate !== 'shut' }, -9.2);
  for (const v of [-2.9, 2.9, 5.1, 10.9]) put(NF, 'courtBollard', 0.45, v);
  for (const [u, v] of [[0.6, -14.6], [0.6, 14.6], [-48.6, -14.6]]) put(NF, 'courtBollard', u, v);
  for (const u of [3, 9, 15, 21]) { put(NF, 'courtKerb', u, 29.8, 4, { len: 6 }); put(NF, 'courtKerb', u, -29.8, 4, { len: 6 }); }
  put(NF, 'toteStack', 4.6, -6.2, 4, { kind: 'tubs', high: 2 }); put(NF, 'toteStack', 1.9, -5.9, 4, { kind: 'tubs', high: 1 }); put(NF, 'toteStack', 5.2, 15.6, 4, { kind: 'boxes', high: 2 }); put(NF, 'toteStack', -45, -16.9, 4, { kind: 'boxes', high: 1 });
  put(NF, 'plantCondenser', -48.85, 4, 2, {});
  put(NF, 'yardLight', 24, 28, 2, {}); put(NF, 'yardLight', 24, -28, 2, {}); put(NF, 'yardLight', -34, 28.5, 4, {});
  if (!o.bare) { put(NF, 'reviewTruck', 2.5, 0, 4, {}, 1.25); put(NF, 'reviewTruck', 32, 4, 0, {}, 2.75);
    for (const [u, v] of [[-4, -15.4], [-33, -15.4], [3.6, -5], [-10, -17], [22, 10]]) put(NF, 'reviewFigure', u, v);
    put(SP, 'chandlery', 14, -22, 4, {}, -25.5); }
  items.sort((a, b) => (b.floor - a.floor) || (b.front - a.front));
  for (const it of items) { const r = it.kit.render(it.key, it.dir, Object.assign({ sky, lit }, it.opts)); blit(c, r, sx(it.u), sy(it.v)); }
  return c;
}

const DAY = WS.at({ time: 14, cloud: 0.1, wind: 0.3 }), DUSK = WS.at({ time: 20.75, cloud: 0.15 }), NIGHT = WS.at({ time: 22.5, cloud: 0.05 });
const only = process.argv[3] || 'all', t0 = Date.now();
if (only === 'all' || only === 'day') { const b = board('day', DAY, 0);
  save('01-site-day-half.png', half(b));
  save('02-east-end-native.png', crop(b, Math.round(sx(-22)), Math.round(sy(15)) - 290, 1500, 900));
  save('03-south-face-native.png', crop(b, Math.round(sx(-49)), Math.round(sy(-6)) - 190, 1500, 620)); }
if (only === 'all' || only === 'dusk') { const b = board('dusk', DUSK, 1, { doors: 'open' }); save('04-site-dusk-half.png', half(b)); save('05-south-face-dusk-native.png', crop(b, Math.round(sx(-22)), Math.round(sy(-6)) - 190, 1500, 620)); }
if (only === 'all' || only === 'night') { const b = board('night', NIGHT, 1); save('06-site-night-half.png', half(b)); }
if (only === 'all' || only === 'cutaway') { const b = board('cutaway', DAY, 0, { cutaway: true, bare: true }); save('07-cutaway-half.png', half(b)); }
if (only === 'all' || only === 'pieces') {   // the small pieces, all eight facings, native size
  const keys = ['courtBollard', 'courtKerb', 'plantCondenser', 'toteStack', 'yardLight'], rows = [];
  for (const key of keys) for (const opts of key === 'toteStack' ? [{ kind: 'tubs', high: 3 }, { kind: 'boxes', high: 2 }] : [{}]) rows.push([0, 1, 2, 3, 4, 5, 6, 7].map(d => NF.render(key, d, Object.assign({ sky: DAY }, opts))));
  const cw = Math.max(...rows.flat().map(r => r.w)) + 8, hs = rows.map(r => Math.max(...r.map(q => q.h)) + 8), c = canvas(cw * 8, hs.reduce((a, b) => a + b, 0), [104, 122, 78]);
  let y = 0; rows.forEach((r, k) => { r.forEach((q, d) => blit(c, q, d * cw + 4 + q.px, y + 4 + q.py)); y += hs[k]; }); save('08-small-pieces-8dir.png', c);
  const g = [NF.render('plantGate', 4, { sky: DAY, open: true }), NF.render('plantGate', 4, { sky: DAY, open: false }), NF.render('plantGate', 3, { sky: DAY, open: false })], gc = canvas(g.reduce((a, q) => a + q.w + 10, 10), Math.max(...g.map(q => q.h)) + 20, [104, 122, 78]);
  let x = 10; for (const q of g) { blit(gc, q, x + q.px, 10 + q.py); x += q.w + 10; } save('09-gate-open-shut.png', gc); }
if (only === 'all' || only === 'dirs') {   // the plant from the other quarters, half size: it never turns in the scene, but the model is whole
  const rs = [3, 5, 6, 0].map(d => NF.render('fishPlant', d, { sky: DAY })), c = canvas(Math.max(...rs.map(r => r.w)) + 20, rs.reduce((a, r) => a + r.h + 10, 10), [104, 122, 78]);
  let y = 10; for (const r of rs) { blit(c, r, 10 + r.px, y + r.py); y += r.h + 10; } save('10-plant-other-facings-half.png', half(c)); }
console.log('DONE in ' + ((Date.now() - t0) / 1000).toFixed(1) + ' s');
