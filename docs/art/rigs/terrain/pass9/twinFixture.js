/* Hidden Harbours — terrain pass 9, PR 2: the fixture the C# twin of TerrainLight6 is tested against.

     node docs/art/rigs/terrain/pass9/twinFixture.js

   Writes fixtures/terrainLight6.twin.json for Assets/Tests/EditMode/TerrainLight6TwinTests.cs. Every
   expected byte in it is the kit's: TerrainLight6.relight, run here, through the kit's public API only.

     gbuffers   G-buffers made by the kit's own TerrainLight6.gbuf from 32 x 32 windows of PxKit8 tiles
                (a window is a tile of its own, and wraps), plus one window with an elevation, fetch and
                far field and no wrap, so relight's tide block runs. A window is chosen for what it
                exercises: marks and tips, pond depth, water. Each per-texel array is base64 of its typed
                array's bytes, little-endian, so every float32 arrives exact.
     cases      a G-buffer, a sky and relight's options, and what the kit returns: rgba (base64, row by
                row) and stable, 1 where the texel's bytes held under every nudge below. A texel that
                moves when an input moves by 1e-7 sits on a threshold, where one ULP between V8's pow,
                sin, cos or hypot and .NET's could flip it; the test compares only stable texels.
     coverage   per case, how many texels each input decided, measured on the kit by switching the
                input off and counting the texels that change: tips (o._tips emptied), marks (the tile's
                marks zeroed: tips, seams and the per-mark sparkle), puddles (G.pond zeroed), water (the
                water class made soil), snow, rain and fog (the sky's set to 0), the tide (o.tide unset),
                occ (o.occ and o.skyv unset) and lv (o.lv unset). The test requires every one somewhere,
                so a fixture that stopped exercising one cannot pass quietly.

     hypot      V8's Math.hypot where the plain square root of the sum of squares rounds differently:
                pairs shaped like the rig's two-argument calls, and triples in -1..1 (the rig's own
                (wx, wy, 1) never separates the two roundings). It pins the twin's port of V8's algorithm
                bit for bit, which the relit bytes above do not see.

   A sky's numbers are rounded to 6 decimals BEFORE the relight, so the JSON holds exactly the sky
   the kit was lit with.                                                                              */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');

const HERE = __dirname, OUT = path.join(HERE, 'fixtures', 'terrainLight6.twin.json');
const LOAD = ['lib/pixelLanguage.js', 'lib/pxKit.js', 'lib/pxKit2.js', 'lib/weatherSky.js', 'lib/terrainLight4.js', 'lib/pxKit8.js', 'terrainLight6.js'];
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
let rigSha256 = '';
for (const f of LOAD) {
  const src = fs.readFileSync(path.join(HERE, f));
  if (f === 'terrainLight6.js') rigSha256 = sha(src);
  vm.runInThisContext(src.toString('utf8'), { filename: f });
}
const K8 = globalThis.PxKit8, TL = globalThis.TerrainLight6, WS = globalThis.WeatherSky;
if (!K8 || !TL || !WS) throw new Error('the kit did not load');

const S = 32, STRIDE = 16, NUDGE = 1e-7;
const b64 = (a) => Buffer.from(a.buffer, a.byteOffset, a.byteLength).toString('base64');
const r6 = (v) => Math.round(v * 1e6) / 1e6;
const hex = (c) => '#' + c.map(v => v.toString(16).padStart(2, '0')).join('');

// ---- windows ----------------------------------------------------------------------------------------------
function crop(t, x0, y0) {
  const N = S * S, o = { n: S, m: S, pal: new Uint8Array(N), band: new Int8Array(N), h: new Float32Array(N), a: t.a ? new Uint8Array(N) : undefined, mark: new Uint16Array(N), pals: t.pals };
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const i = y * S + x, j = (y0 + y) * t.n + (x0 + x);
    o.pal[i] = t.pal[j]; o.band[i] = t.band[j]; o.h[i] = t.h[j]; o.mark[i] = t.mark[j]; if (o.a) o.a[i] = t.a[j];
  }
  return o;
}
/* the window of a tile that scores highest, on the G-buffer the window itself makes */
function pick(key, step, score, gopt) {
  const b = K8.build(key, step, { pass: 8 }), t = b.tile; let best = null;
  for (let y0 = 0; y0 + S <= t.m; y0 += STRIDE) for (let x0 = 0; x0 + S <= t.n; x0 += STRIDE) {
    const w = crop(t, x0, y0), G = TL.gbuf(w, Object.assign({ relief: b.relief }, gopt ? gopt() : {})), s = score(G);
    if (!best || s > best.s) best = { s, w, G, x0, y0 };
  }
  return { name: `${key}${['_lo', '', '_hi'][step]}`, source: `PxKit8.build('${key}', ${step}, {pass: 8}).tile, texels (${best.x0}, ${best.y0}) to (${best.x0 + S - 1}, ${best.y0 + S - 1})`, relief: b.relief, ...best };
}
const count = (G, f) => { let n = 0; for (let i = 0; i < G.N; i++) if (f(i)) n++; return n; };
const tipScore = (G) => count(G, i => G.t.mark[i] && G.proud[i] > 0.12);
const pondScore = (G) => count(G, i => G.pond[i] > 0.3);
const waterScore = (G) => { const w = count(G, i => G.cls[i] === TL.CL.WATER); return Math.min(w, G.N - w); };

// the elevation, fetch and far fields of the one scene window: a shore across it, the sea to the north
const elevOf = () => { const e = new Float32Array(S * S); for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) e[y * S + x] = -0.45 + 0.9 * y / (S - 1) + 0.08 * Math.sin(x * 0.4); return e; };
const fetchOf = () => { const f = new Float32Array(S * S); for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) f[y * S + x] = 0.3 + 0.7 * x / (S - 1); return f; };
const FAR = Float64Array.from({ length: S }, (_, y) => 0.6 * y / (S - 1));
const sceneOpt = () => ({ elev: elevOf(), fetch: fetchOf(), far: (Y) => FAR[Y], wrap: false });

const W = [
  pick('shingle', 2, tipScore),
  pick('path', 1, tipScore),
  pick('grass', 1, tipScore),
  pick('mud', 2, pondScore),
  pick('sedge', 2, waterScore),
  Object.assign(pick('ripple', 1, waterScore, sceneOpt), { scene: true }),
];
W[W.length - 1].name += '_shore';

// ---- skies --------------------------------------------------------------------------------------------------
function skyOf(key, over) {
  let s;
  if (key === 'unlit') s = Object.assign({}, TL.UNLIT);
  else { const p = WS.PRESETS.find(q => q.key === key); if (!p) throw new Error('no preset ' + key); s = WS.at({ time: p.time, cloud: p.cloud, rain: p.rain, fog: p.fog, snow: p.snow, wind: p.wind, gust: p.gust, dir: 1 }); }
  s = Object.assign(s, over || {});
  const o = { name: key, sunF: (s.sunF || s.sunW || [0, 0, 1]).map(r6), sunI: r6(s.sunI || 0), expo: r6(s.expo || 0), snow: r6(s.snow || 0), wet: r6(s.wet || 0), rain: r6(s.rain || 0), fog: r6(s.fog || 0),
    grade: s.grade !== false, kc: s.kc || '', ac: s.ac || '', wash: s.wash || '', fogC: s.fogC || '', skyC: s.skyC || '', aa: r6(s.aa || 0), amb: r6(s.amb || 0), ka: r6(s.ka || 0), wa: r6(s.wa || 0) };
  o.hasSkyI = s.skyI != null; o.skyI = o.hasSkyI ? r6(s.skyI) : 0;
  o.hasWind = !!s.wind; o.wind = o.hasWind ? r6(s.wind.w) : 0;
  return o;
}
/* the fixture's sky back into the object relight reads */
function rigSky(f) {
  const s = { sunF: f.sunF.slice(), sunI: f.sunI, expo: f.expo, snow: f.snow, wet: f.wet, rain: f.rain, fog: f.fog, grade: f.grade, aa: f.aa, amb: f.amb, ka: f.ka, wa: f.wa };
  for (const k of ['kc', 'ac', 'wash', 'fogC', 'skyC']) if (f[k]) s[k] = f[k];
  if (f.hasSkyI) s.skyI = f.skyI;
  if (f.hasWind) s.wind = { w: f.wind };
  return s;
}

// ---- options ------------------------------------------------------------------------------------------------
const occOf = () => { const o = new Uint8Array(S * S); for (let i = 0; i < o.length; i++) o[i] = (i % S) < 12 ? 1 : 0; return o; };
const skyvOf = () => { const v = new Float32Array(S * S); for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) v[y * S + x] = 0.4 + 0.6 * (x + y) / (2 * S - 2); return v; };
const lvOf = () => { const v = new Int32Array(S * S); for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) v[y * S + x] = ((x >> 3) + (y >> 3)) % 4; return v; };
const onlyOf = (G) => { const a = []; for (let i = 0; i < G.N; i++) if (G.cls[i] === TL.CL.WATER || (i % 5) === 0) a.push(i); return Int32Array.from(a); };
function rigOpt(f) {
  const o = { x0: f.x0, y0: f.y0, w: f.w, h: f.h, frame: f.frame };
  if (f.occ) o.occ = f.occ; if (f.skyv) o.skyv = f.skyv; if (f.seaDir) o.seaDir = f.seaDir; if (f.lv) o.lv = f.lv; if (f.only) o.only = f.only;
  if (f.hasTide) o.tide = f.tide; if (f.hasTideHigh) o.tideHigh = f.tideHigh;
  return o;
}

// ---- cases --------------------------------------------------------------------------------------------------
const byName = (n) => W.findIndex(w => w.name === n);
const C = [];
const add = (win, sky, opt) => C.push({ win: byName(win), sky, opt: Object.assign({ x0: 0, y0: 0, w: 0, h: 0, frame: 0, seaDir: 0, hasTide: false, tide: 0, hasTideHigh: false, tideHigh: 0 }, opt || {}) });
for (const k of ['afternoon', 'golden', 'morning', 'overcast', 'night', 'fog', 'unlit', 'snow']) add('shingle_hi', skyOf(k));
add('shingle_hi', skyOf('afternoon'), { occ: occOf(), skyv: skyvOf(), label: 'occ and skyv' });
add('shingle_hi', skyOf('afternoon'), { lv: lvOf(), label: 'canopy levels' });
add('shingle_hi', skyOf('dawn'), { x0: 8, y0: 4, w: 20, h: 24, label: 'a region inside the window' });
add('shingle_hi', skyOf('afternoon', { grade: false }), { label: 'grade off' });
for (const k of ['afternoon', 'golden', 'squall', 'snow']) add('path', skyOf(k));
for (const k of ['afternoon', 'snow', 'squall', 'dusk']) add('grass', skyOf(k));
for (const w of ['shingle_hi', 'grass', 'path']) add(w, skyOf('snow', { snow: 1 }), { label: 'deep snow' });
add('mud_hi', skyOf('snow'));
add('mud_hi', skyOf('squall'), { frame: 0 }); add('mud_hi', skyOf('squall'), { frame: 5 }); add('mud_hi', skyOf('afternoon', { wet: 0.8 }), { label: 'wet ground under sun' });
for (const [k, fr] of [['afternoon', 0], ['afternoon', 9], ['gale', 3], ['squall', 12]]) add('sedge_hi', skyOf(k), { frame: fr });
add('sedge_hi', skyOf('afternoon'), { seaDir: -1, frame: 4, label: 'seaDir -1' });
add('sedge_hi', skyOf('gale'), { only: onlyOf(W[byName('sedge_hi')].G), frame: 7, label: 'only the listed texels' });
for (const [k, fr] of [['afternoon', 0], ['afternoon', 7], ['gale', 3], ['squall', 11], ['snow', 2]]) add('ripple_shore', skyOf(k), { hasTide: true, tide: 0.1, frame: fr });
add('ripple_shore', skyOf('afternoon'), { hasTide: true, tide: 0.1, hasTideHigh: true, tideHigh: 0.3, seaDir: -1, frame: 1, label: 'tideHigh and seaDir' });

// ---- nudges -------------------------------------------------------------------------------------------------
const rot = (v, a) => [v[0] * Math.cos(a) - v[1] * Math.sin(a), v[0] * Math.sin(a) + v[1] * Math.cos(a), v[2]];
function tilt(v, d) {
  const h = Math.hypot(v[0], v[1]), r = Math.hypot(h, v[2]); if (h < 1e-9) return [v[0] + d, v[1], v[2]];
  const el = Math.atan2(v[2], h) + d; return [v[0] / h * r * Math.cos(el), v[1] / h * r * Math.cos(el), r * Math.sin(el)];
}
const NUDGES = [];
for (const sg of [1, -1]) {
  const d = sg * NUDGE, t = sg > 0 ? '+' : '-';
  NUDGES.push([`sun azimuth ${t}1e-7 rad`, (s) => { s.sunF = rot(s.sunF, d); }]);
  NUDGES.push([`sun elevation ${t}1e-7 rad`, (s) => { s.sunF = tilt(s.sunF, d); }]);
  NUDGES.push([`sunF x (1 ${t} 1e-7)`, (s) => { s.sunF = s.sunF.map(v => v * (1 + d)); }]);
  NUDGES.push([`sunI x (1 ${t} 1e-7)`, (s) => { s.sunI *= 1 + d; }]);
  NUDGES.push([`skyI x (1 ${t} 1e-7)`, (s) => { s.skyI = (s.skyI == null ? 0.6 : s.skyI) * (1 + d); }]);
  NUDGES.push([`expo x (1 ${t} 1e-7)`, (s) => { s.expo = (s.expo || 1) * (1 + d); }]);
  NUDGES.push([`wind ${t}1e-7`, (s) => { s.wind = { w: (s.wind ? s.wind.w : 0.2) + d }; }]);
  for (const k of ['wet', 'rain', 'fog', 'snow', 'aa', 'amb', 'ka', 'wa']) NUDGES.push([`${k} ${t}1e-7`, (s) => { s[k] = Math.max(0, (s[k] || 0) + d); }]);
  NUDGES.push([`tide ${t}1e-7`, null, (o) => { if (o.tide != null) o.tide += d; }]);
  NUDGES.push([`tideHigh ${t}1e-7`, null, (o) => { if (o.tideHigh != null) o.tideHigh += d; }]);
}

// ---- run ----------------------------------------------------------------------------------------------------
const TOGGLES = {
  tips: (G, s, o) => [G, s, Object.assign(o, { _tips: new Map() })],
  marks: (G, s, o) => [Object.assign({}, G, { t: Object.assign({}, G.t, { mark: new Uint16Array(G.N) }) }), s, o],
  puddles: (G, s, o) => [Object.assign({}, G, { pond: new Float32Array(G.N) }), s, o],
  water: (G, s, o) => [Object.assign({}, G, { cls: G.cls.map(c => c === TL.CL.WATER ? TL.CL.SOIL : c) }), s, o],
  snow: (G, s, o) => [G, Object.assign(s, { snow: 0 }), o],
  rain: (G, s, o) => [G, Object.assign(s, { rain: 0 }), o],
  fog: (G, s, o) => [G, Object.assign(s, { fog: 0 }), o],
  tide: (G, s, o) => [G, s, Object.assign(o, { tide: undefined, tideHigh: undefined })],
  occ: (G, s, o) => [G, s, Object.assign(o, { occ: undefined, skyv: undefined })],
  lv: (G, s, o) => [G, s, Object.assign(o, { lv: undefined })],
};
const diff = (a, b) => { let n = 0; for (let i = 0; i < a.length; i += 4) if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2] || a[i + 3] !== b[i + 3]) n++; return n; };
const cases = C.map((c) => {
  const win = W[c.win], G = win.G, sky = rigSky(c.sky), opt = rigOpt(c.opt), rgba = TL.relight(G, sky, opt);
  const n = (c.opt.w || G.n) * (c.opt.h || G.m), stable = new Uint8Array(n).fill(1);
  for (const [, ns, no] of NUDGES) {
    const s = rigSky(c.sky), o = rigOpt(c.opt); if (ns) ns(s); if (no) no(o);
    const q = TL.relight(G, s, o);
    for (let i = 0; i < n; i++) { const k = i * 4; if (q[k] !== rgba[k] || q[k + 1] !== rgba[k + 1] || q[k + 2] !== rgba[k + 2] || q[k + 3] !== rgba[k + 3]) stable[i] = 0; }
  }
  const coverage = {};
  for (const [k, f] of Object.entries(TOGGLES)) coverage[k] = diff(rgba, TL.relight(...f(G, rigSky(c.sky), rigOpt(c.opt))));
  const stableCount = stable.reduce((a, v) => a + v, 0);
  const label = `${win.name} / ${c.sky.name}${c.opt.frame ? ' / frame ' + c.opt.frame : ''}${c.opt.label ? ' / ' + c.opt.label : ''}${c.opt.hasTide ? ' / tide ' + c.opt.tide : ''}`;
  console.log(`${label.padEnd(58)} stable ${String(stableCount).padStart(4)}/${n} ` + Object.entries(coverage).map(([k, v]) => ` ${k} ${v}`).join(''));
  const o = c.opt;
  return {
    name: label, gbuffer: c.win, sky: c.sky,
    options: { x0: o.x0, y0: o.y0, w: o.w, h: o.h, frame: o.frame, occ: o.occ ? b64(o.occ) : '', skyv: o.skyv ? b64(o.skyv) : '', seaDir: o.seaDir, lv: o.lv ? b64(o.lv) : '', only: o.only ? b64(o.only) : '',
      hasTide: o.hasTide, tide: o.tide, hasTideHigh: o.hasTideHigh, tideHigh: o.tideHigh },
    rgba: b64(rgba), stable: b64(stable), stableCount,
    coverage,
  };
});
const gbuffers = W.map((w) => {
  const G = w.G;
  return {
    name: w.name, source: w.source, relief: w.relief, n: G.n, m: G.m, wrap: G.wrap, hmax: b64(Float64Array.of(G.hmax)),
    H: b64(G.H), nx: b64(G.nx), ny: b64(G.ny), nz: b64(G.nz), ao: b64(G.ao), proud: b64(G.proud), hollow: b64(G.hollow), pond: b64(G.pond),
    cls: b64(G.cls), band0: b64(G.band0), pal: b64(G.t.pal), mark: b64(G.t.mark), alpha: G.t.a ? b64(G.t.a) : '',
    pals: [].concat(...G.t.pals.map(p => p.map(hex))),
    elev: G.elev ? b64(G.elev) : '', slope: G.slope ? b64(G.slope) : '', fetch: G.fetch ? b64(G.fetch) : '', far: G.far ? b64(FAR) : '',
  };
});
// ---- hypot ----------------------------------------------------------------------------------------------------
let seed = 0x2545f491;
const rnd = () => { seed = (seed + 0x6d2b79f5) >>> 0; let t = seed; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
const H2 = [], H3 = [];
for (let k = 0; k < 100000 && (H2.length < 64 || H3.length < 64); k++) {
  const th = rnd() * 2 * Math.PI, c = 0.2 + 0.8 * rnd();
  const two = [[c * Math.cos(th), c * Math.sin(th)], [(Math.round(rnd() * 8 - 4) - rnd()) * 0.8, Math.round(rnd() * 8 - 4) - rnd()]][k & 1];
  const three = [rnd() * 2 - 1, rnd() * 2 - 1, rnd() * 2 - 1];
  if (H2.length < 64 && Math.hypot(...two) !== Math.sqrt(two[0] * two[0] + two[1] * two[1])) H2.push(two);
  if (H3.length < 64 && Math.hypot(...three) !== Math.sqrt(three[0] * three[0] + three[1] * three[1] + three[2] * three[2])) H3.push(three);
}
const hypot = { args2: b64(Float64Array.from([].concat(...H2))), want2: b64(Float64Array.from(H2.map(v => Math.hypot(...v)))),
  args3: b64(Float64Array.from([].concat(...H3))), want3: b64(Float64Array.from(H3.map(v => Math.hypot(...v)))) };
console.log(`hypot: ${H2.length} two-argument and ${H3.length} three-argument samples where V8 and sqrt(sum of squares) differ`);

const out = {
  kit: 'Hidden Harbours TerrainLight6 twin fixture', version: 1, generator: 'twinFixture.js', rig: 'terrainLight6.js', rigSha256,
  encoding: 'per-texel arrays are base64 of the typed array, little-endian: H nx ny nz ao proud hollow pond elev slope fetch skyv float32; hmax float64; far float64 per row; cls pal alpha occ stable rgba uint8; band0 int8; mark uint16; lv only int32; hypot args and wants float64',
  nudges: NUDGES.map(n => n[0]), gbuffers, cases, hypot,
};
fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
console.log(`${gbuffers.length} G-buffers, ${cases.length} cases -> ${path.relative(process.cwd(), OUT)} (${fs.statSync(OUT).size} bytes)`);
