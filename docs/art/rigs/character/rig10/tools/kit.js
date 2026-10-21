/* Hidden Harbours — character kit 10.3, tools/kit.js: everything the kit's files are made from, in one place.
   Plain JavaScript, no packages. Node loads it with require('./kit.js'); a page loads it with <script src="tools/kit.js"> (window.HHKit).
   Every writer is pure: it takes the rig's API (globalThis.CharacterIso10) and the stamps, and returns a string or pixels. Hashing,
   deflate and the file system belong to the caller (tools/check.cjs in Node, the bake page in a browser). */
(function (root, factory) { if (typeof module === 'object' && module.exports) module.exports = factory(); else root.HHKit = factory(); })(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const R7 = (k, v) => typeof v === 'number' ? +v.toFixed(7) : v;
  const j1 = (o) => JSON.stringify(o, null, 1);
  function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  /* st = { rig, poses, checks }: the SHA-256 of Art/characterIsoRig10.js, .poses.js and .checks.js */
  const head = (C, st, schema, authoring, rigName) => ({ schema, rig: rigName || 'characterIsoRig10.js', exportSymbol: 'CharacterIso10', derivedFromRigSha256: st.rig, posesDerivedFromRigSha256: st.poses, revision: C.revision, authoring });

  /* ---------------- the data files ---------------- */
  function buildFile(C, key, st) {
    return JSON.stringify(Object.assign(head(C, st, 'hidden-harbours/character-export@2', 'CharacterIso10.exportBuild(key); numbers rounded to 7 decimals. Do not hand-edit — re-generate.', 'characterIsoRig10'), C.exportBuild(key)), R7); }
  function gameplayFile(C, key, st) {
    return JSON.stringify(Object.assign({ derivedFromRigSha256: st.rig, posesDerivedFromRigSha256: st.poses }, C.gameplay(key)), R7); }
  function presetsFile(C, st) { const builds = {};
    for (const k of C.CAST) { const n = C.normBuild(k); builds[k] = { label: n.label, set: C.CAST10.indexOf(k) >= 0 ? 'core' : 'npc', key: C.buildKey(n), build: n }; }
    return j1(Object.assign(head(C, st, 'hidden-harbours/character-builds@2', 'Generated from CharacterIso10.normBuild() for every CAST key. Do not hand-edit.'), {
      storage: "a player build: localStorage['hh.player.build.v10'] = normBuild() + name; store buildKey(build)", fields: C.FIELDS, cast: C.CAST, core: C.CAST10, npcs: C.NPCS, builds })); }
  const RANDOM_RULES = [
    'sex drawn first (m, f), unless given; age adult 3 in 6, youth, elder, child 1 in 6 each, unless given',
    'a man never draws a garment or bottom marked fem (skirt and shawl, dress, gown, sundress, pinafore, swimsuit, bikini; skirt, long skirt); a woman may draw anything',
    'swimwear one draw in twelve; trunks or briefs on a woman come with a top (swimTop f)',
    'beards only on adult and elder men, 3 in 5; a youth: none, none, stubble or moustache',
    'elders draw salt, grey or white hair 5 in 6: 3 in 4 from those three, else from all nine colours (which hold the three); nobody else draws grey or white, and salt only on adults',
    'bald only on adult and elder men',
    'a hat 3 in 5' ];
  function optionsFile(C, st) { const garments = {};
    for (const [k, g] of Object.entries(JSON.parse(JSON.stringify(C.GARMENTS)))) garments[k] = Object.assign(g, { group: g.swim ? 'swimwear' : g.top ? 'top' : 'outfit' });
    return j1(Object.assign(head(C, st, 'hidden-harbours/character-options@2', "Generated from the rig's tables. Do not hand-edit."), {
      options: C.OPTIONS, fields: C.FIELDS, garments, bottoms: C.BOTTOMS, beards: C.BEARDS, hats: C.HATS, sex: C.SEX, frames: C.FRAMES, ages: C.AGES, heads: C.HEADS,
      eyeShapes: Object.keys(C.EYE_SHAPES), palettes: C.palettes, randomBuild: { call: 'CharacterIso10.randomBuild(rnd, { sex, age })', rules: RANDOM_RULES } })); }
  function shadingFile(C, st) {
    return j1(Object.assign({ schema: 'hidden-harbours/character-shading@3', rig: 'characterIsoRig10.js', revision: C.revision, derivedFromRigSha256: st.rig, build: 'fisher' }, C.shadingContract('fisher'),
      { dither: C.DITHER, tolerance: C.TOL, ink: { colours: C.INK, roles: C.INK_ROLES, eyeWhite: C.EYE_WHITE }, aim: C.AIM })); }

  /* ---------------- the golden report ---------------- */
  function runBuild(C, build) { return C.runChecks(build).map((r) => ({ id: r.id, title: r.title, gate: r.gate, pass: r.pass, value: r.value, detail: r.detail })); }
  function goldenFile(C, st, results) { const builds = {}, failures = []; let gated = 0, pass = 0;
    for (const k of C.CAST) { const rows = results[k], g = rows.filter((r) => r.gate); gated += g.length; pass += g.filter((r) => r.pass).length;
      for (const r of g) if (!r.pass) failures.push(k + ' · ' + r.id + ' · ' + r.value);
      const s = C.sizes(k); builds[k] = { label: C.normBuild(k).label, set: C.CAST10.indexOf(k) >= 0 ? 'core' : 'npc', gated: g.filter((r) => r.pass).length + ' / ' + g.length, sizes: s, checks: rows }; }
    return j1({ schema: 'hidden-harbours/character-golden@3', rig: 'characterIsoRig10.js', revision: C.revision, derivedFromRigSha256: st.rig, posesDerivedFromRigSha256: st.poses, checksSha256: st.checks,
      authoring: 'CharacterIso10.CHECKS run to the end on every CAST build (tools/check.cjs; the review page steps the same generators). Residuals print with toFixed(7).',
      summary: { builds: C.CAST.length, checks: C.CHECKS.length, gated, gatedPass: pass, failures }, builds }); }

  /* ---------------- the renders ---------------- */
  const LIMITS = [[-60, 0], [60, 0], [0, -15], [0, 20], [-60, 20], [60, 20], [-60, -15], [60, -15]];
  const TARGETS = [[-50, 0], [-30, 0], [-15, 0], [0, 0], [15, 0], [30, 0], [50, 0], [0, -0.35]];
  /* the cells of one strip, row by row; each cell is a render() call */
  function cellsOf(C, kind, key) {
    if (kind === 'idle8') return [[0, 1, 2, 3, 4, 5, 6, 7].map((d) => ({ clip: 'idle', frame: 0, dir: d }))];
    if (kind === 'walkS') return [[0, 1, 2, 3, 4, 5, 6, 7].map((f) => ({ clip: 'walk', frame: f, dir: 4 }))];
    const S = C.evalClip('idle', 0, C.buildOf(key)), f0 = S.I.face;
    if (kind === 'blink') return [2, 3, 4, 5, 6].map((d) => C.BLINK.frames.map((e) => ({ clip: 'idle', frame: 0, dir: d, face: { eyes: e, brows: f0.brows, mouth: f0.mouth } })));
    if (kind === 'look') { const B = S.B, ix = B.sk.ix, W = S.W[ix.head], hm = B.D.headMid, e0 = [W.p[0] + W.R[0] * hm[0] + W.R[3] * hm[1] + W.R[6] * hm[2], W.p[1] + W.R[1] * hm[0] + W.R[4] * hm[1] + W.R[7] * hm[2], W.p[2] + W.R[2] * hm[0] + W.R[5] * hm[1] + W.R[8] * hm[2]];
      const lim = LIMITS.map(([y, p]) => ({ clip: 'idle', frame: 0, dir: 4, look: { yaw: y, pitch: p }, face: { eyes: f0.eyes, brows: f0.brows, mouth: f0.mouth } }));
      const tgt = TARGETS.map(([b, dz]) => { const T = [e0[0] + 2 * Math.sin(b * Math.PI / 180), e0[1] + 2 * Math.cos(b * Math.PI / 180), e0[2] + dz], L = C.lookAt(S, T);
        return { clip: 'idle', frame: 0, dir: 4, look: { yaw: L.yaw, pitch: L.pitch }, face: { eyes: f0.eyes === 'open' ? L.eyes : f0.eyes, brows: f0.brows, mouth: f0.mouth }, target: { bearing_deg: b, dz_m: dz, distance_m: 2 } }; });
      return [lim, tgt]; }
    throw new Error('no strip kind ' + kind); }
  function renderGrid(C, key, rows, scale) { const cw = C.W * scale, ch = C.H * scale, cols = Math.max(...rows.map((r) => r.length)), Wd = cw * cols, Hd = ch * rows.length, out = new Uint8Array(Wd * Hd * 4);
    rows.forEach((row, ri) => row.forEach((c, ci) => { const R = C.render(Object.assign({ build: key }, c));
      for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) { const a = (Math.floor(y / scale) * C.W + Math.floor(x / scale)) * 4, d = ((ri * ch + y) * Wd + ci * cw + x) * 4;
        out[d] = R.rgba[a]; out[d + 1] = R.rgba[a + 1]; out[d + 2] = R.rgba[a + 2]; out[d + 3] = R.rgba[a + 3]; } }));
    return { W: Wd, H: Hd, rgba: out }; }
  /* cast.png: every CAST build at S then SE, ten builds to a row, x2 */
  function castGrid(C) { const rows = []; for (let i = 0; i < C.CAST.length; i += 10) rows.push(C.CAST.slice(i, i + 10));
    const s = 2, cw = C.W * s, ch = C.H * s, Wd = cw * 20, Hd = ch * rows.length, out = new Uint8Array(Wd * Hd * 4);
    rows.forEach((keys, ri) => keys.forEach((k, bi) => [4, 3].forEach((d, di) => { const R = C.render({ build: k, clip: 'idle', frame: 0, dir: d });
      for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) { const a = (Math.floor(y / s) * C.W + Math.floor(x / s)) * 4, o = ((ri * ch + y) * Wd + (bi * 2 + di) * cw + x) * 4;
        out[o] = R.rgba[a]; out[o + 1] = R.rgba[a + 1]; out[o + 2] = R.rgba[a + 2]; out[o + 3] = R.rgba[a + 3]; } })));
    return { W: Wd, H: Hd, rgba: out }; }
  const KINDS = ['idle8', 'walkS', 'blink', 'look'];
  /* every render the kit ships: { file, kind, preset, scale, make() } */
  function renderPlan(C) { const out = [];
    for (const k of C.CAST) for (const kind of KINDS) for (const s of [1, 4]) out.push({ file: 'renders/' + s + 'x/' + k + '.' + kind + '.png', kind, preset: k, scale: s, make: () => renderGrid(C, k, cellsOf(C, kind, k), s) });
    out.push({ file: 'renders/cast.png', kind: 'cast', preset: null, scale: 2, make: () => castGrid(C) });
    return out; }
  const STRIPS = {
    idle8: { rows: 1, cols: 8, cell: 'render({ build, clip: "idle", frame: 0, dir }) for dir 0..7 (N, NE, E, SE, S, SW, W, NW), left to right' },
    walkS: { rows: 1, cols: 8, cell: 'render({ build, clip: "walk", frame, dir: 4 }) for frame 0..7 (110 ms each)' },
    blink: { rows: 5, cols: 4, cell: 'render({ build, clip: "idle", frame: 0, dir, face: { eyes: BLINK.frames[k], brows, mouth } }): rows dir 2..6 (E, SE, S, SW, W, the facings with an eye), columns k = 0..3 (half, shut, shut, half; 40 ms each, the faceClips.blink track); brows and mouth are idle f0\'s' },
    look: { rows: 2, cols: 8, cell: 'render({ build, clip: "idle", frame: 0, dir: 4, look: { yaw, pitch }, face }): row 0 the head turned to each limit in the order of checkHelpers.LIMITS (yaw, pitch: -60 0, 60 0, 0 -15, 0 20, -60 20, 60 20, -60 -15, 60 -15), eyes as idle f0 has them; row 1 lookAt(S, target) with the default share (LOOK.headShare 0.7) for targets 2 m from the head point at bearings -50, -30, -15, 0, 15, 30, 50 deg level, and 0 deg 0.35 m down, eyes.open replaced by lookAt\'s eyes; each image\'s turns lists the angles used' },
    cast: { rows: 3, cols: 20, cell: 'every CAST build in CAST order, ten to a row, each at S (dir 4) then SE (dir 3), idle f0, x2' } };
  function manifestFile(C, st, entries) {
    return j1(Object.assign(head(C, st, 'hidden-harbours/character-renders@3', 'tools/check.cjs --write renders every PNG from tools/kit.js renderPlan(); 4x is the 1x grid scaled by 4, nearest neighbour. rgbaSha256 is the SHA-256 of the pixels as straight RGBA bytes, row-major, empty pixels 0,0,0,0; pngSha256 the file\'s as shipped (another PNG encoder writes other bytes for the same pixels; tools/check.cjs holds the pixels and only notes a pngSha256 that differs).'), {
      render: { px_per_m: C.PX, elev_deg: C.ELEV, cell: [C.W, C.H], pivot: [C.pivot.x, C.pivot.y], keyline: false, contour: true, headSnap: true, background: 'transparent (0,0,0,0)', rock: 'none (on land)', sky: 'none (the afternoon key the rig is authored at)' },
      strips: STRIPS, images: entries })); }

  /* ---------------- PNG: 8-bit RGBA, filter 0, one IDAT; the caller deflates (zlib format) ---------------- */
  const CRC = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
  const crc32 = (b) => { let c = 0xFFFFFFFF; for (let i = 0; i < b.length; i++) c = CRC[(c ^ b[i]) & 255] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0; };
  function scanlines(W, H, rgba) { const o = new Uint8Array(H * (W * 4 + 1)); for (let y = 0; y < H; y++) { o[y * (W * 4 + 1)] = 0; o.set(rgba.subarray(y * W * 4, (y + 1) * W * 4), y * (W * 4 + 1) + 1); } return o; }
  function pngBytes(W, H, idat) { const chunk = (t, d) => { const o = new Uint8Array(12 + d.length), dv = new DataView(o.buffer); dv.setUint32(0, d.length); for (let i = 0; i < 4; i++) o[4 + i] = t.charCodeAt(i); o.set(d, 8); dv.setUint32(8 + d.length, crc32(o.subarray(4, 8 + d.length))); return o; };
    const ih = new Uint8Array(13), dv = new DataView(ih.buffer); dv.setUint32(0, W); dv.setUint32(4, H); ih[8] = 8; ih[9] = 6; ih[10] = 0; ih[11] = 0; ih[12] = 0;
    const parts = [new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', idat), chunk('IEND', new Uint8Array(0))], n = parts.reduce((s, p) => s + p.length, 0), out = new Uint8Array(n); let k = 0; for (const p of parts) { out.set(p, k); k += p.length; } return out; }
  function decodePNG(b, inflate) { const u32 = (o) => ((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]) >>> 0; const sig = [137, 80, 78, 71, 13, 10, 26, 10]; for (let i = 0; i < 8; i++) if (b[i] !== sig[i]) throw new Error('not a PNG');
    let o = 8, w = 0, h = 0, depth = 0, type = 0, inter = 0, plte = null, trns = null; const idat = [];
    while (o < b.length) { const len = u32(o), t = String.fromCharCode(b[o + 4], b[o + 5], b[o + 6], b[o + 7]), d = b.subarray(o + 8, o + 8 + len);
      if (t === 'IHDR') { w = ((d[0] << 24) | (d[1] << 16) | (d[2] << 8) | d[3]) >>> 0; h = ((d[4] << 24) | (d[5] << 16) | (d[6] << 8) | d[7]) >>> 0; depth = d[8]; type = d[9]; inter = d[12]; } else if (t === 'PLTE') plte = d; else if (t === 'tRNS') trns = d; else if (t === 'IDAT') idat.push(d); else if (t === 'IEND') break; o += 12 + len; }
    if (depth !== 8 || inter !== 0 || [2, 3, 6].indexOf(type) < 0) throw new Error('unsupported PNG (depth ' + depth + ', type ' + type + ', interlace ' + inter + ')');
    const z = new Uint8Array(idat.reduce((s, x) => s + x.length, 0)); let k = 0; for (const x of idat) { z.set(x, k); k += x.length; }
    const raw = inflate(z), bpp = type === 6 ? 4 : type === 2 ? 3 : 1, stride = w * bpp, px = new Uint8Array(h * stride);
    for (let y = 0; y < h; y++) { const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1, dst = y * stride;
      for (let x = 0; x < stride; x++) { const a = x >= bpp ? px[dst + x - bpp] : 0, up = y ? px[dst - stride + x] : 0, c = (x >= bpp && y) ? px[dst - stride + x - bpp] : 0, r = raw[src + x]; let v;
        if (f === 0) v = r; else if (f === 1) v = r + a; else if (f === 2) v = r + up; else if (f === 3) v = r + ((a + up) >> 1); else if (f === 4) { const p = a + up - c, pa = Math.abs(p - a), pb = Math.abs(p - up), pc = Math.abs(p - c); v = r + (pa <= pb && pa <= pc ? a : pb <= pc ? up : c); } else throw new Error('bad filter ' + f);
        px[dst + x] = v & 255; } }
    const rgba = new Uint8Array(w * h * 4);
    for (let i = 0; i < w * h; i++) { if (type === 6) { rgba.set(px.subarray(i * 4, i * 4 + 4), i * 4); continue; }
      if (type === 2) { rgba[i * 4] = px[i * 3]; rgba[i * 4 + 1] = px[i * 3 + 1]; rgba[i * 4 + 2] = px[i * 3 + 2]; rgba[i * 4 + 3] = 255; continue; }
      const q = px[i]; rgba[i * 4] = plte[q * 3]; rgba[i * 4 + 1] = plte[q * 3 + 1]; rgba[i * 4 + 2] = plte[q * 3 + 2]; rgba[i * 4 + 3] = trns && q < trns.length ? trns[q] : 255; }
    return { W: w, H: h, rgba }; }
  /* straight RGBA as hashed: empty pixels (alpha 0) are 0,0,0,0 */
  const canon = (rgba) => { const o = new Uint8Array(rgba); for (let i = 0; i < o.length; i += 4) if (!o[i + 3]) { o[i] = o[i + 1] = o[i + 2] = 0; } return o; };

  /* ---------------- the random sweep: CharacterIso10.randomBuild(mulberry32(seed)) for seeds 1..n, every gate ---------------- */
  function randomSweep(C, n, from) { const out = []; for (let s = from || 1; s < (from || 1) + n; s++) { const b = C.randomBuild(mulberry32(s)), rows = runBuild(C, b);
    out.push({ seed: s, build: b, fails: rows.filter((r) => r.gate && !r.pass).map((r) => ({ id: r.id, value: r.value, detail: r.detail })) }); } return out; }

  return { R7, mulberry32, buildFile, gameplayFile, presetsFile, optionsFile, shadingFile, runBuild, goldenFile, cellsOf, renderGrid, castGrid, renderPlan, manifestFile, STRIPS, LIMITS, TARGETS,
    scanlines, pngBytes, decodePNG, crc32, canon, randomSweep, RANDOM_RULES };
});
